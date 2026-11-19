using System;
using UnityEngine;

public sealed class EveningStage
{
    public readonly struct Step
    {
        public readonly int Action,X,Y,Turns;
        public Step(int action,int x,int y,int turns=0){Action=action;X=x;Y=y;Turns=turns;}
        public Vector2Int[] Cells()
        {
            var cells=(Vector2Int[])EveningBoard.Shapes[Action].Clone();
            for(int i=0;i<Turns;i++)cells=EveningBoard.Rotate(cells,1);
            for(int i=0;i<cells.Length;i++)cells[i]+=new Vector2Int(X,Y);
            return cells;
        }
    }
    public readonly string Name,Lesson;
    public readonly int[,] Target=new int[5,5];
    public readonly Step[] HintSteps;
    public int ReferencePieceCount=>HintSteps.Length;
    public int[] SolutionCounts{get{var counts=new int[5];foreach(var step in HintSteps)counts[step.Action]++;return counts;}}
    public int MaxHeight{get{int n=0;foreach(int height in Target)n=Math.Max(n,height);return n;}}
    public EveningStage(string name,string lesson,string[] rows,params Step[] hints)
    {
        if(rows.Length!=5)throw new ArgumentException("Stages must use the 5x5 board.");
        Name=name;Lesson=lesson;HintSteps=hints;
        for(int y=0;y<5;y++)
        {
            if(rows[y].Length!=5)throw new ArgumentException("Each target row must contain five heights.");
            for(int x=0;x<5;x++)Target[x,y]=rows[y][x]-'0';
        }
    }
    public int Mismatches(EveningBoard board)
    {int n=0;var actual=board.Heights();for(int x=0;x<5;x++)for(int y=0;y<5;y++)if(actual[x,y]!=Target[x,y])n++;return n;}
    public bool Complete(EveningBoard board)
    {
        return Mismatches(board)==0;
    }
}

public static class EveningStages
{
    public static readonly EveningStage[] All={
        new EveningStage("첫 네모","정사각형 블록 하나로 낮은 네모를 만들어보세요.",
            new[]{"00000","01100","01100","00000","00000"},
            new EveningStage.Step(4,1,1)),
        new EveningStage("두 층 쌓기","같은 위치에 정사각형 블록을 두 번 놓아보세요.",
            new[]{"00000","02200","02200","00000","00000"},
            new EveningStage.Step(4,1,1),new EveningStage.Step(4,1,1)),
        new EveningStage("작은 계단","네모 세 개를 이용해 높은 쪽과 낮은 쪽을 만들어보세요.",
            new[]{"00000","02310","02310","00000","00000"},
            new EveningStage.Step(4,1,1),new EveningStage.Step(4,1,1),new EveningStage.Step(4,2,1)),
        new EveningStage("작은 봉우리","네모 네 개를 조금씩 겹쳐 가운데가 높은 봉우리를 만들어보세요.",
            new[]{"00000","01210","02420","01210","00000"},
            new EveningStage.Step(4,1,1),new EveningStage.Step(4,2,1),new EveningStage.Step(4,1,2),new EveningStage.Step(4,2,2)),
        new EveningStage("모양 모으기","서로 다른 모양들을 포개 낮은 층과 작은 단을 만들어보세요.",
            new[]{"01110","11310","23310","11000","00000"},
            new EveningStage.Step(4,1,1),new EveningStage.Step(2,0,2),new EveningStage.Step(0,1,0),new EveningStage.Step(1,1,1),new EveningStage.Step(3,0,1)),
        new EveningStage("엇갈린 능선","긴 블록과 꺾인 블록을 회전해 높낮이가 다른 능선을 연결해보세요.",
            new[]{"00100","12420","12410","00110","00000"},
            new EveningStage.Step(4,1,1),new EveningStage.Step(2,2,0,1),new EveningStage.Step(0,2,1,1),new EveningStage.Step(1,1,1),new EveningStage.Step(3,0,1,1)),
        new EveningStage("겹친 테라스","길이 다른 층을 포개 테라스와 작은 봉우리를 만드세요.",
            new[]{"00000","13320","02420","02221","00000"},
            new EveningStage.Step(2,0,1),new EveningStage.Step(2,1,3),new EveningStage.Step(4,1,1),new EveningStage.Step(4,2,2),new EveningStage.Step(0,1,1),new EveningStage.Step(3,1,2,1))
    };
}
