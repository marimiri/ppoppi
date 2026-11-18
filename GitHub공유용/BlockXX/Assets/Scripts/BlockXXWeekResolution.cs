using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BlockXXPrototype
{
    public sealed class RemovalCell
    {
        public Piece piece;
        public Vector3Int cell;
        public float emptiness;
        public List<Piece> companions;
    }
    sealed class RemovalEffect
    {
        public GameObject visual;
        public Vector3 origin,velocity;
        public Color color;
        public float born;
    }

    readonly Dictionary<Vector3Int,GameObject[]> cellVisuals=new Dictionary<Vector3Int,GameObject[]>();
    readonly List<RemovalEffect> removalEffects=new List<RemovalEffect>();
    bool resolving;
    Coroutine weekExecution;
    int removedCells;
    float executedLoad,executedRest,executedNet,executedVoid;

    public sealed class RestResult
    {
        public float rawLoad,unusedRestCells,recoveryCapacity,recoveryUsed,remainingLoad,emptiness;
    }
    public static RestResult EvaluateRest(IList<Piece> source,float loadFactor=1,float voidFactor=1,Dictionary<Piece,Awareness> states=null)
    {
        var result=new RestResult{rawLoad=Round2((states==null?CountLoad(source):WeightedLoad(source,states))*loadFactor)};
        var kinds=new Dictionary<Vector2Int,HashSet<int>>();
        var restCells=new List<Vector3Int>();
        foreach(var piece in source)foreach(var cell in piece.cells)
        {
            if(piece.action>=7)continue;
            var key=new Vector2Int(cell.x,cell.z);
            if(!kinds.TryGetValue(key,out var actions)){actions=new HashSet<int>();kinds[key]=actions;}
            actions.Add(piece.action);
            if(piece.action==4)restCells.Add(cell);
        }
        // Each rest cell recovers 2/k load and adds 1/k emptiness if entirely unused.
        // Partially used cells contribute only their unused fraction to emptiness.
        restCells.Sort((a,b)=>a.x!=b.x?a.x.CompareTo(b.x):a.z!=b.z?b.z.CompareTo(a.z):a.y.CompareTo(b.y));
        float remaining=result.rawLoad,capacity=0;
        foreach(var cell in restCells)
        {
            float multiplier=1f/kinds[new Vector2Int(cell.x,cell.z)].Count;
            float recovery=2f*multiplier;
            capacity+=recovery;
            float used=Mathf.Min(remaining,recovery);
            remaining=Mathf.Max(0,remaining-used);
            float unusedFraction=1f-used/recovery;
            result.unusedRestCells+=unusedFraction;
            result.emptiness+=unusedFraction*multiplier;
        }
        result.recoveryCapacity=(float)Math.Round(capacity,2,MidpointRounding.AwayFromZero);
        result.remainingLoad=(float)Math.Round(remaining,2,MidpointRounding.AwayFromZero);
        result.emptiness=(float)Math.Round(result.emptiness*voidFactor,2,MidpointRounding.AwayFromZero);
        result.unusedRestCells=(float)Math.Round(result.unusedRestCells,2,MidpointRounding.AwayFromZero);
        result.recoveryUsed=result.rawLoad-result.remainingLoad;
        return result;
    }

    public static int RemovalBudget(float load,float rest)
    {
        load=Mathf.Max(0,load);
        return Mathf.Max(0,Mathf.FloorToInt(Mathf.Max(0,load-rest)+.0001f));
    }

    // A unit of load removes one hidden, non-rest voxel. Keep every visible cap,
    // and at least one non-rest voxel per column, so each removal reduces load by one.
    public static List<RemovalCell> PlanOverload(IList<Piece> source,int budget,int seed,Dictionary<Piece,Awareness> states=null)
    {
        var tops=new Dictionary<Vector2Int,int>();
        var nonRestCounts=new Dictionary<Vector2Int,int>();
        var kinds=new Dictionary<Vector2Int,HashSet<int>>();
        var groups=new Dictionary<Vector3Int,List<Piece>>();
        foreach(var p in source)foreach(var c in p.cells)
        {
            if(p.action==7||(states!=null&&states.TryGetValue(p,out var state)&&state==Awareness.Invalid))continue;
            Top(tops,c);
            if(p.action>=7)continue;
            var key=new Vector2Int(c.x,c.z);
            if(!kinds.TryGetValue(key,out var actions)){actions=new HashSet<int>();kinds[key]=actions;}
            actions.Add(p.action);
            if(p.action==4)continue;
            if(!groups.TryGetValue(c,out var owners)){owners=new List<Piece>();groups[c]=owners;nonRestCounts[key]=nonRestCounts.TryGetValue(key,out int n)?n+1:1;}
            owners.Add(p);
        }
        var candidates=new List<RemovalCell>();
        foreach(var group in groups)
        {
            var c=group.Key;var key=new Vector2Int(c.x,c.z);var owners=group.Value;
            if(c.y>tops[key]&&nonRestCounts[key]>1)
                candidates.Add(new RemovalCell{piece=owners[0],cell=c,emptiness=2f/kinds[key].Count,companions=owners.Count>1?owners.GetRange(1,owners.Count-1):null});
        }
        var random=new System.Random(seed);
        for(int i=candidates.Count-1;i>0;i--)
        {int other=random.Next(i+1);var swap=candidates[i];candidates[i]=candidates[other];candidates[other]=swap;}
        var result=new List<RemovalCell>();
        budget=Mathf.Clamp(budget,0,CountLoad(source));
        foreach(var entry in candidates)
        {
            if(result.Count>=budget)break;
            var key=new Vector2Int(entry.cell.x,entry.cell.z);
            if(nonRestCounts[key]<=1)continue;
            result.Add(entry);nonRestCounts[key]--;
        }
        return result;
    }

    void StartWeekResolution()
    {
        boardDrag.Cancel();resolving=true;removedCells=0;
        var rest=CurrentRest();
        executedLoad=rest.rawLoad;executedRest=rest.recoveryCapacity;
        executedNet=RemovalBudget(rest.remainingLoad,0);
        executedVoid=rest.emptiness;
        message="남은 부하 "+executedNet.ToString("0.##")+" · 안쪽 블록 칸이 사라지는 모습을 확인해주세요.";
        var counted=CountedPieces(frozenAwareness);
        var plan=PlanOverload(counted,RemovalBudget(executedLoad,rest.recoveryUsed),Environment.TickCount,frozenAwareness);
        foreach(var entry in plan)entry.emptiness*=VoidFactor;
        weekExecution=StartCoroutine(ExecuteWeek(plan));
    }

    IEnumerator ExecuteWeek(List<RemovalCell> plan)
    {
        if(plan.Count==0)yield return null;
        else
        {
            float duration=Mathf.Min(2.3f,.75f+plan.Count*.07f);
            float began=Time.unscaledTime;int index=0;
            while(Time.unscaledTime-began<duration)
            {
                float elapsed=Time.unscaledTime-began;
                int target=Math.Min(plan.Count,Mathf.FloorToInt(elapsed/(duration-.45f)*plan.Count)+1);
                while(index<target)EraseOverloadedCell(plan[index++]);
                yield return null;
            }
            while(index<plan.Count)EraseOverloadedCell(plan[index++]);
        }
        executedVoid=(float)Math.Round(executedVoid,2,MidpointRounding.AwayFromZero);
        ClearRemovalEffects();Rebuild();resolving=false;weekExecution=null;
        message=removedCells>0
            ?"부하 "+executedNet.ToString("0.##")+" → "+removedCells+"칸 제거 · 공허함 +"+executedVoid.ToString("0.##")+" · 결과를 확인한 뒤 다음 주차로 진행하세요."
            :"남은 부하가 없어 제거된 칸이 없어요. 결과를 확인한 뒤 다음 주차로 진행하세요.";
        FinishRouteWeek();
    }

    void EraseOverloadedCell(RemovalCell removal)
    {
        int index=Array.IndexOf(removal.piece.cells,removal.cell);
        if(index<0)return;
        var cells=new List<Vector3Int>(removal.piece.cells);cells.RemoveAt(index);
        removal.piece.cells=cells.ToArray();
        if(cells.Count==0)pieces.Remove(removal.piece);
        if(removal.companions!=null)foreach(var companion in removal.companions)
        {
            var remaining=new List<Vector3Int>(companion.cells);remaining.Remove(removal.cell);companion.cells=remaining.ToArray();
            if(remaining.Count==0)pieces.Remove(companion);
        }
        if(cellVisuals.TryGetValue(removal.cell,out GameObject[] objects))
        {
            foreach(var o in objects){solids.Remove(o);o.SetActive(false);Destroy(o);}
            cellVisuals.Remove(removal.cell);
        }
        // The multiplier was captured from the schedule before any cells vanished.
        removedCells++;executedVoid+=removal.emptiness;
        SpawnRemovalEffect(removal.cell,removal.piece.action);
    }

    void SpawnRemovalEffect(Vector3Int cell,int action)
    {
        Vector3 center=World(cell);
        for(int i=0;i<6&&removalEffects.Count<240;i++)
        {
            Vector3 direction=UnityEngine.Random.onUnitSphere;
            Color color=Color.Lerp(BlockColors[action],Color.white,.35f);
            var go=MakeMesh(cubeMesh,ghostMaterial,center+direction*.15f,Vector3.one*.17f,color);
            removalEffects.Add(new RemovalEffect{visual=go,origin=go.transform.position,velocity=direction*1.05f+Vector3.up*.3f,color=color,born=Time.unscaledTime});
        }
    }

    void TickRemovalEffects()
    {
        for(int i=removalEffects.Count-1;i>=0;i--)
        {
            var effect=removalEffects[i];float age=Time.unscaledTime-effect.born,t=age/.65f;
            if(t>=1){Destroy(effect.visual);removalEffects.RemoveAt(i);continue;}
            effect.visual.transform.position=effect.origin+effect.velocity*age+Vector3.down*(age*age*.6f);
            effect.visual.transform.localScale=Vector3.one*(.17f*(1-t));
            effect.visual.transform.Rotate(new Vector3(45,70,25)*Time.unscaledDeltaTime,Space.Self);
            Color color=effect.color;color.a=1-t;
            pulseProperties.SetColor("_Color",color);effect.visual.GetComponent<MeshRenderer>().SetPropertyBlock(pulseProperties);
        }
    }
    void ClearRemovalEffects()
    {foreach(var effect in removalEffects)if(effect.visual!=null)Destroy(effect.visual);removalEffects.Clear();}
    void CancelWeekResolution()
    {
        if(weekExecution!=null)StopCoroutine(weekExecution);
        weekExecution=null;resolving=false;ClearRemovalEffects();
        removedCells=0;executedLoad=executedRest=executedNet=executedVoid=0;
    }

    IEnumerator CheckWeekExecution()
    {
        Reset(false);int originalWeek=week;
        for(int d=0;d<7;d++){Seed(0,d,0);Seed(6,d,3);}FillExtraCheckColumns();Rebuild();
        BeginWeek();
        if(!started||!resolving)throw new Exception("Starting a full week must lock editing and begin the result sequence.");
        while(resolving)yield return null;
        int survivors=0;foreach(var p in pieces)survivors+=p.cells.Length;
        if(removedCells!=7||survivors!=BoardCellCount||Coverage()!=BoardCellCount||CountLoad(pieces)!=0||Gain(0)!=21||executedVoid!=14)
            throw new Exception("Overload must remove seven hidden T cells, preserve all caps, and exclude lost cells from results.");
        if(week!=originalWeek||!started)throw new Exception("Finishing the effect must wait on results instead of advancing automatically.");
        Reset(true);
        if(week!=originalWeek+1||pieces.Count!=0||started||resolving||removalEffects.Count!=0)
            throw new Exception("Next week must clear the board and execution effects exactly once.");
        week=originalWeek;
        for(int d=0;d<7;d++){Seed(6,d,0);Seed(6,d,3);}FillExtraCheckColumns();Rebuild();
        BeginWeek();while(resolving)yield return null;
        if(removedCells!=0||Coverage()!=BoardCellCount||week!=originalWeek)throw new Exception("A week without overload must preserve every block and wait for next week.");
        foreach(int load in new[]{6,7,8})
        {
            Reset(false);
            for(int d=0;d<6;d++){Seed(0,d,0);Seed(6,d,3);}
            Seed(4,6,4);
            if(load==6)Seed(2,6,0);
            else if(load==7){Seed(0,6,0);Seed(5,6,3);}
            else{Seed(3,6,0);Seed(5,6,2);Seed(5,6,3);}
            FillExtraCheckColumns();Rebuild();
            if(CountLoad(pieces)!=load||Coverage()!=BoardCellCount)throw new Exception("Rest example must start from a full valid schedule with the requested load.");
            BeginWeek();while(resolving)yield return null;
            float expectedVoid=(8-load)/2f;
            if(removedCells!=0||executedRest!=8||executedNet!=0||executedVoid!=expectedVoid||!started||week!=originalWeek)
                throw new Exception("One non-multitasking O rest block must recover 8, and loads 6/7/8 must leave emptiness 1/0.5/0 in the actual week result.");
        }
        Reset(false);
        Debug.Log("BLOCKXX_WEEK_CHECKS_PASSED: removal effects, result pause, next-week reset, doubled recovery, loads 6/7/8 produce emptiness 1/0.5/0");
    }
    void FillExtraCheckColumns()
    {for(int d=0;d<DayCount;d++)Seed(2,d,6);}
}
