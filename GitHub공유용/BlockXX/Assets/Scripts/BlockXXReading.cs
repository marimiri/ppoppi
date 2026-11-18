using System.Collections.Generic;
using UnityEngine;

public sealed partial class BlockXXPrototype
{
    public static bool SharedAt(Vector3Int cell,IEnumerable<Piece> source)
    {
        bool book=false,other=false;
        foreach(var p in source)foreach(var c in p.cells)if(c==cell)
        {if(p.action==1&&p.sharedVolume)book=true;else if(p.action<7)other=true;}
        return book&&other;
    }
    static Bounds PieceBounds(Piece p,Vector3Int cell,IEnumerable<Piece> source)
    {
        bool shared=SharedAt(cell,source);
        return new Bounds(World(cell)+(shared?Vector3.right*(p.action==1?-.25f:.25f):Vector3.zero),new Vector3(shared?.46f:.96f,.96f,.96f));
    }
    public static int SharedBookCells(IEnumerable<Piece> source)
    {
        var cells=new HashSet<Vector3Int>();foreach(var p in source)if(p.action==1&&p.sharedVolume)foreach(var c in p.cells)if(SharedAt(c,source))cells.Add(c);
        return cells.Count;
    }
    bool CanShareWith(Piece existing,int incoming,Vector3Int cell)
    {
        if(mode!=PlayMode.Reading||existing.action>=7||incoming>=7)return false;
        if((existing.action==1)==(incoming==1))return false;
        int occupants=0;foreach(var p in pieces)foreach(var c in p.cells)if(c==cell)occupants++;
        return occupants==1;
    }
    bool CanPreviewShared(Vector3Int cell,int incoming)
    {
        foreach(var p in pieces)foreach(var c in p.cells)if(c==cell&&CanShareWith(p,incoming,cell))return true;
        return false;
    }
    bool ValidateRoutePlacement(Vector3Int[] cells,out string reason)
    {
        reason="";
        if(!RouteEnabled)return true;
        foreach(var c in cells)foreach(var p in pieces)if(p.action==8)foreach(var oc in p.cells)
            if(c.x==oc.x&&c.z==oc.z){reason="랜덤 이벤트가 차지한 시간대에는 배치할 수 없어요.";return false;}
        if(mode==PlayMode.Reading)
        {
            var proposed=new List<Piece>(pieces){new Piece{action=held,cells=cells,sharedVolume=held==1}};
            if(SharedBookCells(proposed)>30){reason="이번 주 책 중첩은 최대 30칸까지 가능해요.";return false;}
        }
        return true;
    }
}
