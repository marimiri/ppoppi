using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BlockXXPrototype
{
    const int DreamsPerWeek=7;
    bool suppressRandomDreams; // Only isolates the existing hand-authored diagnostic fixtures.
    static readonly Vector3Int[] DreamDirections={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};

    // Each perpendicular pair makes the same three-cell L in one of its twelve cube rotations.
    static List<Vector3Int[]> DreamCandidates()
    {
        var result=new List<Vector3Int[]>();var unique=new HashSet<string>();
        foreach(var u in DreamDirections)foreach(var v in DreamDirections)
        {
            if(u.x*v.x+u.y*v.y+u.z*v.z!=0)continue;
            var shape=new[]{Vector3Int.zero,u,v};
            var min=Vector3Int.Min(shape[0],Vector3Int.Min(u,v));
            for(int i=0;i<3;i++)shape[i]-=min;
            Array.Sort(shape,(a,b)=>a.x!=b.x?a.x.CompareTo(b.x):a.y!=b.y?a.y.CompareTo(b.y):a.z.CompareTo(b.z));
            string key=string.Join(";",Array.ConvertAll(shape,c=>c.ToString()));if(!unique.Add(key))continue;
            for(int d=0;d<DayCount;d++)for(int s=0;s<SlotsPerDay;s++)
            {
                var cells=new Vector3Int[3];bool inside=true;
                for(int i=0;i<3;i++){cells[i]=shape[i]+new Vector3Int(d,0,s);if(cells[i].x>=DayCount||cells[i].z>=SlotsPerDay)inside=false;}
                if(inside)result.Add(cells);
            }
        }
        return result;
    }
    void GenerateRandomDreams()
    {
        var selected=RandomDreamLayout(new System.Random(Guid.NewGuid().GetHashCode()));
        foreach(var cells in selected)pieces.Add(new Piece{action=7,id=nextId++,fixedPlacement=true,label="꿈꾸기",cells=cells});
    }
    static List<Vector3Int[]> RandomDreamLayout(System.Random random)
    {
        var candidates=DreamCandidates();
        for(int attempt=0;attempt<100;attempt++)
        {
            for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);var swap=candidates[i];candidates[i]=candidates[j];candidates[j]=swap;}
            var blocked=new HashSet<Vector3Int>();var selected=new List<Vector3Int[]>();
            foreach(var cells in candidates)
            {
                bool fits=true;foreach(var c in cells)if(blocked.Contains(c)){fits=false;break;}
                if(!fits)continue;
                selected.Add(cells);
                foreach(var c in cells){blocked.Add(c);foreach(var direction in DreamDirections)blocked.Add(c+direction);}
                if(selected.Count==DreamsPerWeek)return selected;
            }
        }
        throw new InvalidOperationException("Could not place seven separated board-attached dreams.");
    }

    void CheckRandomDreams()
    {
        suppressRandomDreams=false;
        var layouts=new HashSet<string>();bool crossedDay=false,horizontal=false,vertical=false;
        foreach(PlayMode candidateMode in Enum.GetValues(typeof(PlayMode)))
        {
            ChangeMode(candidateMode);
            for(int turn=1;turn<=4;turn++)
            {
                week=turn;Reset(false);
                Verify(PlacedCount(7)==7,"every mode and every week must contain exactly seven dreams");
                var all=new Dictionary<Vector3Int,int>();string signature="";
                foreach(var p in pieces)if(p.action==7)
                {
                    Verify(p.fixedPlacement&&p.cells.Length==3,"dreams must be fixed L triominoes");
                    int contacts=0,edges=0;bool attached=false;
                    foreach(var c in p.cells)
                    {
                        Verify(c.x>=0&&c.x<DayCount&&c.z>=0&&c.z<SlotsPerDay&&c.y>=0,"dreams must stay within board bounds");
                        Verify(!all.ContainsKey(c),"dreams must not overlap");all.Add(c,p.id);
                        if(c.y==0){attached=true;contacts++;}
                        foreach(var other in p.cells)if((c-other).sqrMagnitude==1)edges++;
                        signature+=c.ToString();
                        if(c.x!=p.cells[0].x)crossedDay=true;
                    }
                    Verify(attached&&edges==4&&(p.cells[0]-p.cells[1]).sqrMagnitude+(p.cells[0]-p.cells[2]).sqrMagnitude+(p.cells[1]-p.cells[2]).sqrMagnitude==4,"dreams must retain a right-angle L and touch the board");
                    if(contacts==3)horizontal=true;else vertical=true;
                }
                foreach(var entry in all)foreach(var direction in DreamDirections)
                    Verify(!all.TryGetValue(entry.Key+direction,out int owner)||owner==entry.Value,"different dreams must not share a face");
                Verify(CountLoad(pieces)==0&&ActivityKinds(pieces).Count==0&&EvaluateRest(pieces).emptiness==0&&PlanOverload(pieces,100,1).Count==0,"dreams alone must not produce activity, load, void or removable cells");
                var actual=new List<Piece>(pieces);
                Seed(0,0,0);Seed(4,1,0);Seed(3,2,0);
                var withDreams=EvaluateRest(pieces);float gain=Gain(0);int focus=PlantFocusedDays();int supply=ExpectedEngineeringSupply();
                pieces.RemoveAll(p=>p.action>=7);
                var without=EvaluateRest(pieces);
                Verify(gain==Gain(0)&&focus==PlantFocusedDays()&&supply==ExpectedEngineeringSupply()&&withDreams.rawLoad==without.rawLoad&&withDreams.recoveryCapacity==without.recoveryCapacity&&withDreams.emptiness==without.emptiness,"dreams must not change gains, focus, engineering supply or rest");
                pieces.Clear();pieces.AddRange(actual);
                foreach(var p in pieces)if(p.action==7){Choose(1);Verify(!CanShareWith(p,1,p.cells[0]),"reading cannot share dream volume");ReturnHeld();break;}
                layouts.Add(signature);
            }
        }
        Verify(layouts.Count==20&&crossedDay&&horizontal&&vertical,"layouts must vary across modes/weeks and use 3D rotations including cross-day positions");
        for(int seed=0;seed<200;seed++)Verify(RandomDreamLayout(new System.Random(seed)).Count==7,"dream generation must reliably place all seven pieces");
        ChangeMode(PlayMode.Basic);
        Debug.Log("BLOCKXX_DREAM_CHECKS_PASSED: seven separated board-attached random Ls in every mode/week, white fixed obstacles, no load/multitasking/focus/rest effects; 200 seeded layouts");
    }
}
