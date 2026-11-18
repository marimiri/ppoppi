using System;
using UnityEngine;

public sealed partial class BlockXXPrototype
{
    static readonly string[] ScoreNames={"식물 친화 지수","문학소녀 지수","악장 지수","공돌이 지수"};
    float ScoreRowY(int action)=>RouteEnabled?597+action*40+(action>MainAction?16:0):597+action*44;
    int[] PlantDailyCounts()
    {
        var result=new int[7];
        var states=CurrentAwareness();var kinds=started&&scheduledKinds!=null?scheduledKinds:ActivityKinds(CountedPieces(states));
        foreach(var piece in pieces)
        {
            if(piece.action!=0||piece.cells.Length==0)continue;
            bool pure=started&&scheduledKinds!=null?purePieces.Contains(piece.id):FocusEligible(piece,states,kinds);
            int d=piece.cells[0].x;if(pure&&d>=0&&d<7)result[d]++;
        }
        return result;
    }
    int PlantFocusedDays()
    {int count=0;foreach(int n in PlantDailyCounts())if(n>0)count++;return count;}
    int ExpectedEngineeringSupply()
    {
        if(started&&!resolving)return engineeringSupply;
        var states=CurrentAwareness();var kinds=started&&scheduledKinds!=null?scheduledKinds:ActivityKinds(CountedPieces(states));int completedCells=0;
        foreach(var piece in pieces)
            if(piece.action==3&&(started&&scheduledKinds!=null?purePieces.Contains(piece.id):FocusEligible(piece,states,kinds)))completedCells+=piece.cells.Length;
        return engineeringSupply+completedCells/4;
    }
    string RouteProgressText
    {
        get
        {
            if(mode==PlayMode.Plant)return "집중 "+PlantFocusedDays()+"/7일 · 매일 1회";
            if(mode==PlayMode.Music)
            {
                int music=started?scheduledCounts[2]:ValidCount(2);
                int other=started?scheduledCounts[0]+scheduledCounts[1]+scheduledCounts[3]:ValidCount(0)+ValidCount(1)+ValidCount(3);
                return "음악 "+music+"/25 · 타 활동 "+other+"/10";
            }
            if(mode==PlayMode.Reading)
                return "중첩 "+SharedBookCells(CountedPieces(CurrentAwareness()))+"/30칸 · 청소 "+((started?weekCleaningDone:ValidCount(6)>0)?1:0)+"/1";
            if(mode==PlayMode.Engineering)
                return "다음 지급 "+(started&&!resolving?"확정 ":"예상 ")+ExpectedEngineeringSupply()+"개";
            return "";
        }
    }
    Rect RouteProgressRect=>new Rect(52,ScoreRowY(MainAction)+34,210,16);
    void DrawRouteProgress()
    {
        if(!RouteEnabled)return;
        Label(RouteProgressRect,RouteProgressText,new GUIStyle(small){fontSize=12});
    }
    void DrawRouteProgressTooltip()
    {
        if(mode!=PlayMode.Plant||!RouteProgressRect.Contains(Event.current.mousePosition))return;
        int[] counts=PlantDailyCounts();string[] detail=new string[7];
        for(int d=0;d<7;d++)detail[d]=days[d]+" "+Math.Min(1,counts[d])+"/1";
        Rect bubble=new Rect(280,Mathf.Min(RouteProgressRect.y,814),390,68);
        Fill(bubble,Color.white);Border(bubble,Ink);
        Label(new Rect(bubble.x+12,bubble.y+7,366,22),"요일별 집중 활동 달성",small);
        Label(new Rect(bubble.x+12,bubble.y+32,366,22),string.Join("   ",detail),new GUIStyle(small){fontSize=12});
    }
}
