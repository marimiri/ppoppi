using System;
using System.Collections.Generic;
using UnityEngine;

public static class EveningBoardChecks
{
    static void Require(bool condition,string message){if(!condition)throw new Exception("evening model: "+message);}
    public static void Run()
    {
        foreach(var source in EveningBoard.Shapes)
        {
            Require(new HashSet<Vector2Int>(source).Count==4,"every action must retain its four-cell mino");
            var rotated=(Vector2Int[])source.Clone();for(int i=0;i<4;i++)rotated=EveningBoard.Rotate(rotated,1);
            Require(new HashSet<Vector2Int>(rotated).SetEquals(source),"four turns must restore the shape");
            Require(new HashSet<Vector2Int>(EveningBoard.Mirror(EveningBoard.Mirror(source))).SetEquals(source),"two mirrors must restore the shape");
        }
        var board=new EveningBoard();var first=(Vector2Int[])EveningBoard.Shapes[0].Clone();
        Require(board.TryPlace(0,first,out var plant),"first T placement must succeed");
        first[0]=new Vector2Int(99,99);Require(plant.Cells[0]==Vector2Int.zero,"placement must own a copy of its coordinates");
        Require(board.TryPlace(1,EveningBoard.Shapes[1],out var book),"different actions must overlap freely");
        var heights=board.Heights();
        Require(heights[1,0]==2&&heights[2,0]==2&&heights[1,1]==2&&heights[0,0]==1&&heights[0,1]==1,"only overlapping cells must grow taller");
        Require(board.MaxHeight==2&&board.Coverage==5,"height and footprint must be independent");
        var layers=new Dictionary<Vector2Int,int>();int total=0;
        foreach(var v in board.Voxels())
        {
            var key=new Vector2Int(v.X,v.Z);int expected=layers.TryGetValue(key,out int value)?value:0;
            Require(v.Layer==expected,"columns must have no holes or floating cells");layers[key]=expected+1;total++;
            if(v.Layer==0&&v.X==1&&v.Z==0)Require(v.Action==0,"earlier pieces must remain at the bottom");
            if(v.Layer==1&&v.X==1&&v.Z==0)Require(v.Action==1,"later pieces must appear above earlier pieces");
        }
        Require(total==8,"every placed mino must produce exactly four result cubes");
        Require(board.TopAt(new Vector2Int(1,0))==book&&board.At(new Vector2Int(1,0)).Count==2,"flat selection must identify the latest owner");
        Require(board.Remove(plant.Id)&&board.MaxHeight==1&&board.Voxels().Count==4,"removing a covered piece must compact the derived result");
        Require(board.TopAt(new Vector2Int(1,0))==book,"removing lower pieces must preserve upper ownership");
        int count=board.Pieces.Count;
        Require(!board.TryPlace(0,new[]{new Vector2Int(-1,0),new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(1,1)},out _),"negative coordinates must fail");
        Require(!board.TryPlace(0,new[]{new Vector2Int(4,0),new Vector2Int(5,0),new Vector2Int(4,1),new Vector2Int(4,2)},out _),"past-edge coordinates must fail");
        Require(!board.TryPlace(0,new[]{Vector2Int.zero,Vector2Int.zero,Vector2Int.one,new Vector2Int(1,0)},out _),"duplicate footprint cells must fail");
        Require(board.Pieces.Count==count,"rejected placements must not mutate the board");
        Require(board.Undo()&&board.Pieces.Count==0&&!board.Undo(),"undo must handle both populated and empty plans");
        for(int i=0;i<25;i++)Require(board.TryPlace(i%5,EveningBoard.Shapes[4],out _),"repeated overlaps must not impose an invented height limit");
        Require(board.MaxHeight==25&&board.Coverage==4&&board.Voxels().Count==100,"same coordinate must support many layers without expanding the footprint");
        board.Clear();Require(board.MaxHeight==0&&board.Coverage==0&&board.Pieces.Count==0,"clear must reset the whole plan");
        Debug.Log("EVENING_MODEL_CHECKS_PASSED: 5x5 bounds, mino turns/mirrors, overlap-derived heights, layer order, deletion compaction, no coordinate aliasing, undo and reset");
    }
}
