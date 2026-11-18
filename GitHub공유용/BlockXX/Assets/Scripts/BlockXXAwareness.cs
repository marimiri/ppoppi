using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BlockXXPrototype
{
    public enum Awareness { Invalid, Preconscious, Unconscious, Fixed }
    Dictionary<Piece,Awareness> frozenAwareness;
    bool medicationEnabled=true;
    const int InitialEngineeringSupply=5,RestLimit=3;

    // Visibility is always measured along board depth, independently of the orbit camera.
    public static Dictionary<Piece,Awareness> Classify(IList<Piece> source,bool allowUnconscious=true)
    {
        var tops=new Dictionary<Vector2Int,int>();
        foreach(var p in source)foreach(var c in p.cells)Top(tops,c);
        var result=new Dictionary<Piece,Awareness>();
        foreach(var p in source)
        {
            if(p.action>=7){result[p]=Awareness.Fixed;continue;}
            bool sameDay=p.cells.Length>0,inside=sameDay;
            foreach(var c in p.cells)
            {if(c.x!=p.cells[0].x)sameDay=false;if(c.x<0||c.x>=DayCount||c.z<0||c.z>=SlotsPerDay||c.y<0)inside=false;}
            bool visible=p.cells.Length>0&&Exposed(p.cells,tops);
            result[p]=!inside?Awareness.Invalid:visible&&sameDay?Awareness.Preconscious:
                !visible&&p.action<4&&allowUnconscious?Awareness.Unconscious:Awareness.Invalid;
        }
        return result;
    }
    Dictionary<Piece,Awareness> CurrentAwareness()=>started&&frozenAwareness!=null?frozenAwareness:Classify(pieces,mode!=PlayMode.Music);
    List<Piece> CountedPieces(Dictionary<Piece,Awareness> states)
    {var result=new List<Piece>();foreach(var p in pieces)if(states.TryGetValue(p,out var state)&&state!=Awareness.Invalid)result.Add(p);return result;}
    int ValidCount(int action)
    {int n=0;var states=CurrentAwareness();foreach(var p in pieces)if(p.action==action&&states.TryGetValue(p,out var state)&&state!=Awareness.Invalid)n++;return n;}
    int InvalidCount()
    {int n=0;foreach(var state in CurrentAwareness().Values)if(state==Awareness.Invalid)n++;return n;}
    bool FocusEligible(Piece piece,Dictionary<Piece,Awareness> states,Dictionary<Vector2Int,HashSet<int>> kinds)
    {
        // An unconscious action keeps its score exemption, but never earns a focus bonus.
        return states.TryGetValue(piece,out var state)&&state==Awareness.Preconscious&&IsPure(piece,kinds);
    }
    string AwarenessSummary()
    {
        int pre=0,un=0,invalid=0;
        foreach(var state in CurrentAwareness().Values){if(state==Awareness.Preconscious)pre++;else if(state==Awareness.Unconscious)un++;else if(state==Awareness.Invalid)invalid++;}
        return "전의식 "+pre+" · 무의식 "+un+" · 미확정 "+invalid+" · 휴식 "+PlacedCount(4)+"/"+RestLimit;
    }
    bool StartRulesReady(out string reason)
    {
        if(InvalidCount()>0){reason="미확정 "+InvalidCount()+"개 · 요일/윗면 노출 조건을 확인해주세요. 음악 루트·생활 블록은 무의식 불가.";return false;}
        if(PlacedCount(4)>RestLimit){reason="아무것도 안 하기는 일주일에 3개까지 배치할 수 있어요.";return false;}
        if(!medicationEnabled&&PlacedCount(5)>0){reason="약 사용이 꺼져 있어요. 약을 회수하거나 사용 설정을 켜주세요.";return false;}
        reason="";return true;
    }
    void ToggleMedication()
    {
        if(started||resolving||medicationWarning||badEnding||RouteIsOver)return;
        medicationEnabled=!medicationEnabled;
        if(!medicationEnabled&&held==5)ReturnHeld();
        message=medicationEnabled?"약 사용을 켰어요.":PlacedCount(5)>0?"약 사용을 껐어요. 배치한 약을 회수해야 행동을 시작할 수 있어요.":"약 사용을 껐어요.";
    }

    public static float WeightedLoad(IList<Piece> source,Dictionary<Piece,Awareness> states)
    {
        var groups=new Dictionary<Vector3Int,float>();var tops=new Dictionary<Vector2Int,int>();
        foreach(var p in source)
        {
            if(p.action==4||p.action>=7||!states.TryGetValue(p,out var state)||state==Awareness.Invalid)continue;
            float weight=state==Awareness.Unconscious?.5f:1;
            foreach(var c in p.cells)
            {Top(tops,c);groups[c]=groups.TryGetValue(c,out float previous)?Mathf.Max(previous,weight):weight;}
        }
        float load=0;
        foreach(var cell in groups)if(cell.Key.y>tops[new Vector2Int(cell.Key.x,cell.Key.z)])load+=cell.Value;
        return load;
    }
    RestResult CurrentRest()
    {
        var states=CurrentAwareness();var counted=CountedPieces(states);
        return EvaluateRest(counted,LoadFactor,VoidFactor,states);
    }
    string TargetDescription=>"메인 "+ScoreTarget(MainAction)+" / 나머지 각 50";
}
