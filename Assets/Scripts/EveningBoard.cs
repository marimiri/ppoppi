using System;
using System.Collections.Generic;
using UnityEngine;

// The planning model has only two coordinates. Heights are derived, never placed.
public sealed class EveningBoard
{
    public const int Size=5;
    public static readonly string[] Names={"T","S","I","L","O"};
    public static int Effect(int action)=>action<2?0:1;
    // Action order: T, S, I, L, O. Keep exact user-supplied swatches.
    public static readonly Color[] Colors={new Color32(0xfe,0xec,0xb1,255),new Color32(0xff,0xb2,0xbc,255),new Color32(0xd4,0xb4,0xef,255),new Color32(0x94,0xe8,0xe9,255),new Color32(0xff,0xdb,0xee,255)};
    public static readonly Vector2Int[][] Shapes={
        new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(2,0),new Vector2Int(1,1)},
        new[]{new Vector2Int(1,0),new Vector2Int(2,0),new Vector2Int(0,1),new Vector2Int(1,1)},
        new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(2,0),new Vector2Int(3,0)},
        new[]{new Vector2Int(0,0),new Vector2Int(0,1),new Vector2Int(0,2),new Vector2Int(1,2)},
        new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(0,1),new Vector2Int(1,1)}
    };
    public sealed class Piece
    {
        public readonly int Id,Action;
        public readonly Vector2Int[] Cells;
        public Piece(int id,int action,Vector2Int[] cells){Id=id;Action=action;Cells=(Vector2Int[])cells.Clone();}
    }
    public readonly struct Voxel
    {
        public readonly int X,Z,Layer,Action,PieceId;
        public Voxel(int x,int z,int layer,int action,int id){X=x;Z=z;Layer=layer;Action=action;PieceId=id;}
    }
    readonly List<Piece> pieces=new List<Piece>();
    int nextId=1;
    public IReadOnlyList<Piece> Pieces=>pieces;
    public int Count(int action){int n=0;foreach(var p in pieces)if(p.Action==action)n++;return n;}
    public static bool Inside(Vector2Int c)=>c.x>=0&&c.x<Size&&c.y>=0&&c.y<Size;
    public static bool ValidCells(Vector2Int[] cells)
    {
        if(cells==null||cells.Length!=4)return false;
        var unique=new HashSet<Vector2Int>();foreach(var c in cells)if(!Inside(c)||!unique.Add(c))return false;
        return true;
    }
    public bool TryPlace(int action,Vector2Int[] cells,out Piece piece)
    {
        piece=null;if(action<0||action>=Shapes.Length||!ValidCells(cells))return false;
        piece=new Piece(nextId++,action,cells);pieces.Add(piece);return true;
    }
    public bool Remove(int id)
    {for(int i=0;i<pieces.Count;i++)if(pieces[i].Id==id){pieces.RemoveAt(i);return true;}return false;}
    public bool Undo()
    {if(pieces.Count==0)return false;pieces.RemoveAt(pieces.Count-1);return true;}
    public void Clear(){pieces.Clear();nextId=1;}
    public Piece TopAt(Vector2Int cell)
    {for(int i=pieces.Count-1;i>=0;i--)foreach(var c in pieces[i].Cells)if(c==cell)return pieces[i];return null;}
    public List<Piece> At(Vector2Int cell)
    {var result=new List<Piece>();foreach(var p in pieces)foreach(var c in p.Cells)if(c==cell){result.Add(p);break;}return result;}
    void ModifierMasks(out bool[,] inverter,out bool[,] amplifier)
    {
        inverter=new bool[Size,Size];amplifier=new bool[Size,Size];
        foreach(var p in pieces)if(p.Action<2)foreach(var c in p.Cells)(p.Action==1?inverter:amplifier)[c.x,c.y]=true;
    }
    static int OutputOf(Piece piece,bool[,] inverter,bool[,] amplifier)
    {
        if(piece.Action<2)return 0;
        bool reversed=false,doubled=false;
        foreach(var c in piece.Cells){reversed|=inverter[c.x,c.y];doubled|=amplifier[c.x,c.y];}
        return (reversed?-1:1)*(doubled?2:1);
    }
    // Any shared cell modifies the entire ordinary mino. S/T never modify one
    // another or stack their own effect; masks make placement order irrelevant.
    public int OutputOf(Piece piece)
    {ModifierMasks(out var inverter,out var amplifier);return OutputOf(piece,inverter,amplifier);}
    public int[,] Heights()
    {
        ModifierMasks(out var inverter,out var amplifier);var h=new int[Size,Size];
        foreach(var p in pieces){int output=OutputOf(p,inverter,amplifier);foreach(var c in p.Cells)h[c.x,c.y]+=output;}
        for(int x=0;x<Size;x++)for(int y=0;y<Size;y++)h[x,y]=Math.Max(0,h[x,y]);return h;
    }
    public List<Voxel> Voxels()
    {
        var heights=Heights();var result=new List<Voxel>();ModifierMasks(out var inverter,out var amplifier);
        // Color positive layers by their source mino; subtraction removes top layers.
        // The evaluated height remains the order-independent sum of all effects.
        for(int x=0;x<Size;x++)for(int y=0;y<Size;y++)
        {
            int layer=0;foreach(var p in At(new Vector2Int(x,y)))
            {
                int amount=OutputOf(p,inverter,amplifier);if(amount<=0)continue;
                for(int i=0;i<amount&&layer<heights[x,y];i++,layer++)result.Add(new Voxel(x,y,layer,p.Action,p.Id));
                if(layer>=heights[x,y])break;
            }
        }
        return result;
    }
    public int Coverage{get{int count=0;foreach(int n in Heights())if(n>0)count++;return count;}}
    public int MaxHeight{get{int max=0;foreach(int n in Heights())max=Math.Max(max,n);return max;}}
    public static Vector2Int[] Rotate(Vector2Int[] source,int direction)
    {var r=new Vector2Int[source.Length];for(int i=0;i<r.Length;i++)r[i]=new Vector2Int(-direction*source[i].y,direction*source[i].x);Normalize(r);return r;}
    public static Vector2Int[] Mirror(Vector2Int[] source)
    {var r=new Vector2Int[source.Length];for(int i=0;i<r.Length;i++)r[i]=new Vector2Int(-source[i].x,source[i].y);Normalize(r);return r;}
    public static void Normalize(Vector2Int[] cells)
    {var min=cells[0];foreach(var c in cells)min=Vector2Int.Min(min,c);for(int i=0;i<cells.Length;i++)cells[i]-=min;}
}
