using System;
using UnityEngine;

public sealed class AerStage
{
    public readonly string Name,Lesson,Code;
    public readonly struct Step
    {
        public readonly int Action,X,Y,Turns;
        public readonly bool Mirrored;
        public Step(int action,int x,int y,int turns=0,bool mirrored=false){Action=action;X=x;Y=y;Turns=turns;Mirrored=mirrored;}
        public Vector2Int[] Cells(){var cells=(Vector2Int[])EveningBoard.Shapes[Action].Clone();if(Mirrored)cells=EveningBoard.Mirror(cells);for(int i=0;i<Turns;i++)cells=EveningBoard.Rotate(cells,1);for(int i=0;i<cells.Length;i++)cells[i]+=new Vector2Int(X,Y);return cells;}
    }
    public readonly Step[] Solution;
    public readonly int[,] Target=new int[5,5];
    public int Supply=>Solution.Length;
    public bool Weather=>Code.StartsWith("WEATHER");
    public int ClearPercent=>Code=="WEATHER-02"?90:Weather?85:100;
    public AerStage(string name,string code,string lesson,params Vector2Int[] solution):this(name,code,lesson,Array.ConvertAll(solution,p=>new Step(4,p.x,p.y))){}
    public AerStage(string name,string code,string lesson,params Step[] solution)
    {
        Name=name;Code=code;Lesson=lesson;Solution=solution;
        var board=new EveningBoard();foreach(var step in solution)board.TryPlace(step.Action,step.Cells(),out _);Target=board.Heights();
    }
    public int Differences(EveningBoard board)
    {
        int n=0;var actual=board.Heights();
        for(int y=0;y<5;y++)for(int x=0;x<5;x++)if(actual[x,y]!=Target[x,y])n++;
        return n;
    }
    public int Similarity(EveningBoard board)
    {
        int overlap=0,total=0;var actual=board.Heights();
        for(int y=0;y<5;y++)for(int x=0;x<5;x++){overlap+=Math.Min(actual[x,y],Target[x,y]);total+=Math.Max(actual[x,y],Target[x,y]);}
        return total==0?100:Mathf.RoundToInt(100f*overlap/total);
    }
    public int SupplyOf(int action){int n=0;foreach(var step in Solution)if(step.Action==action)n++;return n;}
    // Unlocks follow region progress, not the number of pieces in an example solution.
    public int RequiredAction=>Code=="NAME-03"?3:Code=="NAME-05"?2:Code=="NAME-07"?1:Code=="NAME-08"?0:-1;
    public bool Allows(int action)
    {
        // Stage-specific teaching sets.
        if(Code=="NAME-04")return action==3;
        if(Code=="NAME-07")return action==4||action==3||action==1;
        if(Code=="NAME-08")return action==4||action==2||action==0;
        int index=Array.IndexOf(AerStages.All,this);
        return action==4||action==3&&index>=5||action==2&&index>=8||action==1&&index>=10||action==0&&index>=11;
    }
    public bool RequiredMinoUsed(EveningBoard board)
    {
        if(RequiredAction<0)return true;if(board.Count(RequiredAction)==0)return false;
        var without=new EveningBoard();foreach(var piece in board.Pieces)if(piece.Action!=RequiredAction)without.TryPlace(piece.Action,piece.Cells,out _);
        var actual=board.Heights();var other=without.Heights();
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)if(actual[x,y]!=other[x,y])return true;
        return false;
    }
    public bool Complete(EveningBoard board)=>board.Pieces.Count>0&&RequiredMinoUsed(board)&&(Weather?Similarity(board)>=ClearPercent:Differences(board)==0);
}

public static class AerStages
{
    public static readonly AerStage[] All={
        new AerStage("첫 번째 평면","01 / FOOTPRINT","블록 하나를 배치해보세요. 평면의 네 칸이 높이 1의 입체 구조가 됩니다.",new Vector2Int(1,1)),
        new AerStage("같은 자리, 다른 높이","02 / OVERLAP","같은 자리에 두 블록을 놓아보세요. 두 번 겹친 칸은 높이 2가 됩니다.",new Vector2Int(1,1),new Vector2Int(1,1)),
        new AerStage("겹침으로 만든 계단","03 / RECONSTRUCT","",new Vector2Int(1,1),new Vector2Int(1,1),new Vector2Int(2,1)),
        // Names introduce offset overlap, turned L, crossing I, local
        // minimum/maximum leveling. Tutorials and regions 1-5 are unchanged.
        new AerStage("기상 관측소","NAME-01","",new Vector2Int(0,1),new Vector2Int(1,1),new Vector2Int(1,2)),
        new AerStage("공동 건조장","NAME-02","",new Vector2Int(1,0),new Vector2Int(1,1),new Vector2Int(1,2),new Vector2Int(2,1)),
        new AerStage("온실","NAME-03","",M(4,1,1),M(4,2,2),M(3,0,1,1)),
        new AerStage("통신 중계소","NAME-04","",M(3,1,0,2),M(3,0,1,0,true),M(3,2,1,1),M(3,0,1,3)),
        new AerStage("일조 복원","WEATHER-01","재현도 85% 이상",M(4,1,1),M(4,2,1),M(3,0,1),M(3,2,0,1),M(3,1,2,1),M(4,2,2)),
        new AerStage("기록 보관소","NAME-05","",M(4,1,1),M(3,0,0),M(2,0,2),M(4,2,2)),
        // One extra O overlap and one vertical I; fewer competing orientations.
        new AerStage("급수 관리소","NAME-06","",M(4,1,1),M(4,2,1),M(3,0,1),M(2,3,0,1),M(4,2,2)),
        // Each introduction uses three ordinary pieces and one local operator.
        new AerStage("바람길 제어소","NAME-07","",M(4,1,1),M(4,2,1),M(3,0,1),M(1,1,2)),
        new AerStage("환경 제어실","NAME-08","",M(4,1,1),M(4,2,1),M(2,0,3),M(0,0,1)),
        // Both local effects matter; their two shared cells retain raw heights.
        new AerStage("강수 복원","WEATHER-02","복원도 90% 이상",M(4,1,1),M(4,2,1),M(3,0,1),M(2,0,2),M(1,1,2),M(0,0,2))
    };
    public static readonly int[] BuildingStages={3,4,5,6,8,9,10,11};
    static AerStage.Step M(int action,int x,int y,int turns=0,bool mirrored=false)=>new AerStage.Step(action,x,y,turns,mirrored);
}
