using System;
using System.Collections.Generic;
using UnityEngine;

public static class EveningStageChecks
{
    static void Require(bool condition,string message){if(!condition)throw new Exception("evening stage: "+message);}
    public static void Run()
    {
        Require(EveningStages.All.Length==7,"there must be seven authored stages");
        for(int stageIndex=0;stageIndex<EveningStages.All.Length;stageIndex++)
        {
            var stage=EveningStages.All[stageIndex];
            var board=new EveningBoard();int volume=0,footprint=0;
            foreach(int height in stage.Target){Require(height>=0&&height<=6,"goals must remain between zero and six layers");volume+=height;if(height>0)footprint++;}
            Require(volume==stage.ReferencePieceCount*4&&footprint>=4,"goals must match their reference volume");
            Require(stage.ReferencePieceCount==new[]{1,2,3,4,5,5,6}[stageIndex],"reference complexity must progress through the introductory and bridge stages");
            if(stageIndex==6)Require(stage.Name=="겹친 테라스"&&stage.MaxHeight==4,"the former first stage must be the finale");
            if(stageIndex==4||stageIndex==5)
            {
                foreach(int count in stage.SolutionCounts)Require(count==1,"both bridge stages must have a solution using every block shape once");
                Require(stage.MaxHeight==(stageIndex==4?3:4),"bridge stages must introduce varied shapes before the final terrace");
            }
            foreach(var step in stage.HintSteps)Require(board.TryPlace(step.Action,step.Cells(),out _),stage.Name+": reference solution must respect the board bounds");
            Require(stage.Complete(board)&&stage.Mismatches(board)==0,stage.Name+": hint solution must match the separately authored height map");
            var last=board.Pieces[board.Pieces.Count-1];int action=last.Action;
            Require(board.Remove(last.Id)&&!stage.Complete(board),"deleting any piece must invalidate completion");
            Require(board.TryPlace(action,last.Cells,out _)&&stage.Complete(board),"replacing a picked piece must restore the target");
            board.Clear();Require(board.Pieces.Count==0&&!stage.Complete(board),"empty board must never count as a solved stage");
            for(int i=stage.HintSteps.Length-1;i>=0;i--){var step=stage.HintSteps[i];Require(board.TryPlace(step.Action,step.Cells(),out _),"reverse placement must remain legal");}
            Require(stage.Complete(board),"goal matching must ignore colors and layer ownership order");
            Require(board.TryPlace(4,EveningBoard.Shapes[4],out _)&&stage.Mismatches(board)>0&&!stage.Complete(board),"extra blocks are allowed but must not count as a solved shape");
            Require(Solve(stage,out int visited),stage.Name+": independent rotation/mirror search must find a complete solution");
            Debug.Log("EVENING_STAGE_SOLVER_PASSED: "+stage.Name+" / "+stage.ReferencePieceCount+" reference pieces / "+visited+" search states");
        }
        var unlimited=new EveningBoard();
        for(int i=0;i<150;i++)Require(unlimited.TryPlace(i%5,EveningBoard.Shapes[i%5],out _),"every activity must remain unlimited beyond any reference solution count");
        Require(unlimited.Pieces.Count==150&&unlimited.Undo(),"unlimited plans must still support undo");unlimited.Clear();
        Require(unlimited.Pieces.Count==0,"retry must clear unlimited plans");
        Debug.Log("EVENING_STAGE_CHECKS_PASSED: seven targets including two varied-shape bridge stages and original terrace finale, unlimited activities, exact shape matching independent of color/order and retry");
    }
    static bool Solve(EveningStage stage,out int visited)
    {
        var choices=new List<int[]>[5];var tasks=new List<int>();
        for(int action=0;action<5;action++)
        {
            choices[action]=new List<int[]>();var unique=new HashSet<string>();
            for(int mirror=0;mirror<2;mirror++)
            {
                var orientation=mirror==0?(Vector2Int[])EveningBoard.Shapes[action].Clone():EveningBoard.Mirror(EveningBoard.Shapes[action]);
                for(int turn=0;turn<4;turn++)
                {
                    for(int x=0;x<5;x++)for(int y=0;y<5;y++)
                    {
                        var indices=new int[4];bool fits=true;
                        for(int i=0;i<4;i++){var c=orientation[i]+new Vector2Int(x,y);if(!EveningBoard.Inside(c)||stage.Target[c.x,c.y]==0){fits=false;break;}indices[i]=c.y*5+c.x;}
                        if(!fits)continue;Array.Sort(indices);if(unique.Add(string.Join(",",indices)))choices[action].Add(indices);
                    }
                    orientation=EveningBoard.Rotate(orientation,1);
                }
            }
            for(int i=0;i<stage.SolutionCounts[action];i++)tasks.Add(action);
        }
        tasks.Sort((a,b)=>choices[a].Count.CompareTo(choices[b].Count));
        var target=new int[25];for(int x=0;x<5;x++)for(int y=0;y<5;y++)target[y*5+x]=stage.Target[x,y];
        int explored=0;var seen=new HashSet<string>();var heights=new int[25];
        bool Search(int index)
        {
            explored++;
            if(index==tasks.Count){for(int c=0;c<25;c++)if(heights[c]!=target[c])return false;return true;}
            string state=index+":"+string.Join(",",heights);if(!seen.Add(state))return false;
            foreach(var candidate in choices[tasks[index]])
            {
                bool fits=true;foreach(int c in candidate)if(heights[c]>=target[c]){fits=false;break;}
                if(!fits)continue;foreach(int c in candidate)heights[c]++;
                if(Search(index+1))return true;
                foreach(int c in candidate)heights[c]--;
            }
            return false;
        }
        bool solved=Search(0);visited=explored;return solved;
    }
}
