using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BlockXXPrototype
{
    void Verify(bool condition,string message)
    {if(!condition)throw new Exception("Route check: "+message);}
    void FixtureSettle(float emptiness=0)
    {if(mode==PlayMode.Reading&&PlacedCount(6)==0)Seed(6,6,3);CaptureRouteSchedule();started=true;executedVoid=emptiness;FinishRouteWeek();}
    void SeedVerticalMusic(int d,int s)
    {
        var cells=Turn(Shapes[2],false,1);
        for(int i=0;i<cells.Length;i++)cells[i]+=new Vector3Int(d,0,s);
        pieces.Add(new Piece{action=2,cells=cells,id=nextId++});
    }
}
