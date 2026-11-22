using UnityEngine;

public static class AerHints
{
    public enum Kind { Place,Move,Execute }
    public readonly struct Advice
    {
        public readonly Kind Type;
        public readonly int Step,PieceId;
        public readonly Vector2Int Anchor;
        public Advice(Kind type,int step,int pieceId,Vector2Int anchor){Type=type;Step=step;PieceId=pieceId;Anchor=anchor;}
    }
    public static bool SameCells(Vector2Int[] a,Vector2Int[] b)
    {
        if(a.Length!=b.Length)return false;
        foreach(var cell in a){bool found=false;foreach(var other in b)if(cell==other){found=true;break;}if(!found)return false;}
        return true;
    }
    public static Advice Next(AerStage stage,EveningBoard board)
    {
        if(stage.Complete(board))return new Advice(Kind.Execute,-1,-1,default);
        var used=new bool[board.Pieces.Count];var matched=new bool[stage.Solution.Length];
        for(int i=0;i<stage.Solution.Length;i++)
        {
            var step=stage.Solution[i];var cells=step.Cells();
            for(int j=0;j<board.Pieces.Count;j++)
            {
                var piece=board.Pieces[j];
                if(!used[j]&&piece.Action==step.Action&&SameCells(piece.Cells,cells)){used[j]=true;matched[i]=true;break;}
            }
        }
        // First reconcile placements outside this example solution. A marked
        // pickup cell always belongs to the topmost, actually clickable piece.
        int buried=-1;
        for(int j=board.Pieces.Count-1;j>=0;j--)if(!used[j])
        {
            var piece=board.Pieces[j];buried=j;
            foreach(var cell in piece.Cells)if(board.TopAt(cell).Id==piece.Id)return new Advice(Kind.Move,-1,piece.Id,cell);
        }
        if(buried>=0)
        {
            var cell=board.Pieces[buried].Cells[0];var covering=board.TopAt(cell);
            return new Advice(Kind.Move,-1,covering.Id,cell);
        }
        for(int i=0;i<matched.Length;i++)if(!matched[i])return new Advice(Kind.Place,i,-1,stage.Solution[i].Cells()[0]);
        return new Advice(Kind.Execute,-1,-1,default);
    }
}
