using System;
using System.Collections.Generic;
using UnityEngine;

// The planning model has only two coordinates. Heights are derived, never placed.
public sealed class EveningBoard
{
    public const int Size=5;
    public static readonly string[] Names={"식물 키우기","소설책 읽기","하모니카 불기","교신 시도","아무것도 안 하기"};
    public static readonly Color[] Colors={new Color(.66f,.85f,.37f),new Color(.79f,.69f,.92f),new Color(.97f,.64f,.78f),new Color(.53f,.79f,.95f),new Color(.08f,.08f,.10f)};
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
    public int[,] Heights()
    {var h=new int[Size,Size];foreach(var p in pieces)foreach(var c in p.Cells)h[c.x,c.y]++;return h;}
    public List<Voxel> Voxels()
    {
        var heights=new int[Size,Size];var result=new List<Voxel>();
        foreach(var p in pieces)foreach(var c in p.Cells)result.Add(new Voxel(c.x,c.y,heights[c.x,c.y]++,p.Action,p.Id));
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
