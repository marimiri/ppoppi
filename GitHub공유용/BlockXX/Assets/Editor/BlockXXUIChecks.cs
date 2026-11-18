#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static class BlockXXUIChecks
{
    public static void Run()
    {
        foreach(var shape in BlockXXPrototype.Shapes)
        {
            foreach(bool yaw in new[]{true,false})
            {
                var rotated=shape;
                for(int turn=0;turn<4;turn++)rotated=BlockXXPrototype.Turn(rotated,yaw,1);
                var a=new HashSet<Vector3Int>(shape);var b=new HashSet<Vector3Int>(rotated);
                if(!a.SetEquals(b))throw new Exception("Four rotations must preserve each mino.");
            }
        }
        var pieces=new List<BlockXXPrototype.Piece>();
        pieces.Add(new BlockXXPrototype.Piece{action=0,cells=BlockXXPrototype.Shapes[0]});
        if(BlockXXPrototype.CountLoad(pieces)!=1)throw new Exception("A T mino occupies four voxels over three time columns: load must be 1.");
        pieces.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(0,2,1)}});
        if(BlockXXPrototype.CountLoad(pieces)!=2)throw new Exception("One extra voxel in an occupied time column must increase load by 1.");
        pieces.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(1,0,1)}});
        if(BlockXXPrototype.CountLoad(pieces)!=2)throw new Exception("A voxel on another day must not increase this column's load.");
        pieces.Add(new BlockXXPrototype.Piece{action=4,cells=new[]{new Vector3Int(0,3,1),new Vector3Int(0,4,1),new Vector3Int(1,1,1),new Vector3Int(1,2,1)}});
        if(BlockXXPrototype.CountLoad(pieces)!=2)throw new Exception("Rest stacked on activities must not increase raw load.");
        var restOnly=new List<BlockXXPrototype.Piece>{new BlockXXPrototype.Piece{action=4,cells=BlockXXPrototype.Shapes[4]}};
        if(BlockXXPrototype.CountLoad(restOnly)!=0)throw new Exception("Rest alone must never create load, even in its own vertical stack.");
        restOnly.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(0,2,0)}});
        if(BlockXXPrototype.CountLoad(restOnly)!=0)throw new Exception("A single non-rest voxel above rest must not create load.");
        foreach(float angle in new[]{0f,90f,180f,270f,360f,-360f})
        {
            Quaternion a=BlockXXPrototype.BoardViewRotation(angle,30);
            Quaternion b=BlockXXPrototype.BoardViewRotation(angle+360,390);
            if(Quaternion.Angle(a,b)>.05f)throw new Exception("Board orbit must wrap continuously through full revolutions.");
        }
        if(Quaternion.Angle(BlockXXPrototype.BoardViewRotation(89.9f,30),BlockXXPrototype.BoardViewRotation(90.1f,30))>1f)
            throw new Exception("Crossing the top pole must not flip the camera's up axis.");
        var gesture=new BlockXXPrototype.BoardDragGesture();
        gesture.Begin(new Vector2(100,100));
        if(gesture.Move(new Vector2(102,103))!=Vector2.zero||!gesture.End())
            throw new Exception("Small pointer jitter must remain a click so a block can be picked up.");
        gesture.Begin(new Vector2(100,100));
        if(gesture.Move(new Vector2(80,110))!=new Vector2(-20,10)||!gesture.Dragging)
            throw new Exception("An empty-hand diagonal drag must provide both orbit axes.");
        gesture.Move(new Vector2(100,100));
        if(gesture.End())throw new Exception("Dragging back to the starting point must not pick up a block.");
        gesture.Begin(Vector2.zero);gesture.Cancel();
        if(gesture.Active||gesture.End())throw new Exception("Cancelled gestures must not become clicks after focus loss.");
        var schedule=new List<BlockXXPrototype.Piece>();
        schedule.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(0,0,0)}});
        schedule.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(0,0,1)}});
        schedule.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(0,0,2)}});
        if(BlockXXPrototype.EvaluateWeekStart(schedule,false)!=BlockXXPrototype.WeekStartDecision.IncompleteBoard)
            throw new Exception("Overmedication must not bypass an incomplete schedule.");
        schedule.Clear();
        for(int d=0;d<BlockXXPrototype.DayCount;d++)
        {
            for(int s=0;s<BlockXXPrototype.SlotsPerDay;s++)
                schedule.Add(new BlockXXPrototype.Piece{action=2,cells=new[]{new Vector3Int(d,0,s)}});
        }
        schedule.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(0,1,0)}});
        schedule.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(0,1,1)}});
        if(BlockXXPrototype.EvaluateWeekStart(schedule,false)!=BlockXXPrototype.WeekStartDecision.Ready)
            throw new Exception("A full board with at most two medicines per day must be allowed to begin.");
        if(BlockXXPrototype.EvaluateWeekStart(schedule,true)!=BlockXXPrototype.WeekStartDecision.HoldingBlock)
            throw new Exception("A held preview must not count as placed schedule coverage.");
        schedule.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(0,1,2)}});
        if(BlockXXPrototype.EvaluateWeekStart(schedule,false)!=BlockXXPrototype.WeekStartDecision.MedicationWarning)
            throw new Exception("A full board with three medicines in one day must require an explicit choice.");
        schedule.Clear();for(int i=0;i<BlockXXPrototype.BoardCellCount;i++)schedule.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(0,i,0)}});
        if(BlockXXPrototype.EvaluateWeekStart(schedule,false)!=BlockXXPrototype.WeekStartDecision.IncompleteBoard)
            throw new Exception("Seventy voxels in one time column must not count as a full board.");
        schedule.Clear();for(int d=0;d<BlockXXPrototype.DayCount;d++)for(int s=0;s<6;s++)schedule.Add(new BlockXXPrototype.Piece{action=2,cells=new[]{new Vector3Int(d,0,s)}});
        if(BlockXXPrototype.EvaluateWeekStart(schedule,false)!=BlockXXPrototype.WeekStartDecision.IncompleteBoard)
            throw new Exception("Filling the old six-slot board must no longer allow the week to start.");
        foreach(var shape in BlockXXPrototype.Shapes)
        {
            var turned=shape;
            for(int turn=0;turn<4;turn++)
            {
                int anchor=BlockXXPrototype.BoardNearestAnchor(turned);
                foreach(var c in turned)if(c.y<turned[anchor].y)throw new Exception("Anchor must be on the tier closest to the board after rotation.");
                turned=BlockXXPrototype.Turn(turned,false,1);
            }
        }
        var voxel=new Vector3Int(3,2,2);
        Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        var surfacePieces=new List<BlockXXPrototype.Piece>{new BlockXXPrototype.Piece{action=5,cells=new[]{voxel}}};
        foreach(Vector3 normal in normals)
        {
            var ray=new Ray(BlockXXPrototype.World(voxel)+normal*3,-normal);
            Vector3Int expectedNormal=new Vector3Int(Mathf.RoundToInt(normal.x),-Mathf.RoundToInt(normal.y),Mathf.RoundToInt(normal.z));
            if(!BlockXXPrototype.TryHitVoxel(ray,voxel,out float distance,out Vector3Int gridNormal)||gridNormal!=expectedNormal||distance<0)
                throw new Exception("Ray hit must identify the correct one of all six cube faces.");
            if(!BlockXXPrototype.TrySnapToSurface(surfacePieces,ray,out Vector3Int target)||target!=voxel+expectedNormal)
                throw new Exception("Placement must snap outside the selected block face, rather than into the occupied voxel.");
        }
        if(!BlockXXPrototype.TrySnapToSurface(surfacePieces,new Ray(new Vector3(0,4,0),Vector3.down),out Vector3Int emptyTarget)||emptyTarget!=Vector3Int.zero)
            throw new Exception("An empty board tile must place the anchor below the board at depth zero.");
        if(BlockXXPrototype.TrySnapToSurface(surfacePieces,new Ray(new Vector3(-2,4,0),Vector3.down),out _))
            throw new Exception("A ray outside the board must not be clamped onto its edge.");
        if(!BlockXXPrototype.TrySnapToSurface(surfacePieces,new Ray(new Vector3(0,4,9),Vector3.down),out Vector3Int tenth)||tenth!=new Vector3Int(0,0,9))
            throw new Exception("The added far edge of the ten-slot board must accept pointer hits.");
        if(BlockXXPrototype.TrySnapToSurface(surfacePieces,new Ray(new Vector3(0,4,10),Vector3.down),out _))
            throw new Exception("The eleventh slot must remain outside the board.");
        surfacePieces.Add(new BlockXXPrototype.Piece{action=5,cells=new[]{new Vector3Int(3,4,2)}});
        surfacePieces.Reverse();
        if(!BlockXXPrototype.TrySnapToSurface(surfacePieces,new Ray(new Vector3(3,4,2),Vector3.down),out Vector3Int closest)||closest!=new Vector3Int(3,1,2))
            throw new Exception("The nearest visible block face must win regardless of insertion order or a virtual board plane.");
        var multiRest=new List<BlockXXPrototype.Piece>{
            new BlockXXPrototype.Piece{action=0,cells=new[]{new Vector3Int(0,0,0)}},
            new BlockXXPrototype.Piece{action=4,cells=new[]{new Vector3Int(0,1,0),new Vector3Int(0,2,0),new Vector3Int(0,3,0),new Vector3Int(0,4,0)}}
        };
        var restReport=BlockXXPrototype.EvaluateRest(multiRest);
        if(restReport.rawLoad!=0||restReport.recoveryCapacity!=4||restReport.unusedRestCells!=4||restReport.emptiness!=2)
            throw new Exception("Two-task rest must give 2/2 recovery and 1/2 unused emptiness per cell, without changing raw load.");
        multiRest[0].cells=new[]{new Vector3Int(0,0,0),new Vector3Int(0,5,0)};
        restReport=BlockXXPrototype.EvaluateRest(multiRest);
        if(restReport.rawLoad!=1||restReport.recoveryUsed!=1||restReport.remainingLoad!=0||restReport.unusedRestCells!=3||restReport.emptiness!=1.5f)
            throw new Exception("One two-task rest cell cancels one load; the other three give 1.5 emptiness.");
        multiRest.Insert(0,new BlockXXPrototype.Piece{action=1,cells=new[]{new Vector3Int(0,6,0)}});
        restReport=BlockXXPrototype.EvaluateRest(multiRest);
        if(restReport.rawLoad!=2||Mathf.Abs(restReport.recoveryCapacity-2.67f)>.001f||restReport.remainingLoad!=0||restReport.unusedRestCells!=1||Mathf.Abs(restReport.emptiness-.33f)>.001f)
            throw new Exception("Three-task rest recovers 2/3 per cell; cancelling two load leaves one unused cell worth 0.33 emptiness.");
        multiRest[0].cells=new[]{new Vector3Int(0,6,0),new Vector3Int(0,7,0),new Vector3Int(0,8,0)};
        restReport=BlockXXPrototype.EvaluateRest(multiRest);
        if(restReport.rawLoad!=4||Mathf.Abs(restReport.remainingLoad-1.33f)>.001f||restReport.emptiness!=0)
            throw new Exception("Raw load remains four even with three activity kinds; recovery alone reduces the remainder to 1.33.");
        if(BlockXXPrototype.RemovalBudget(2,1.33f)!=0||BlockXXPrototype.RemovalBudget(2,2)!=0||BlockXXPrototype.RemovalBudget(2.5f,1)!=1)
            throw new Exception("Fractional residual load must be discarded after recovery.");
        var removalSource=new List<BlockXXPrototype.Piece>{
            new BlockXXPrototype.Piece{action=0,cells=new[]{new Vector3Int(0,0,0),new Vector3Int(0,1,0),new Vector3Int(0,2,0)}},
            new BlockXXPrototype.Piece{action=4,cells=new[]{new Vector3Int(0,3,0),new Vector3Int(0,4,0)}}
        };
        var removalPlan=BlockXXPrototype.PlanOverload(removalSource,2,42);
        if(removalPlan.Count!=2)throw new Exception("Two units of overload must produce exactly two removals.");
        foreach(var entry in removalPlan)
            if(entry.piece.action==4||entry.cell.y==0)throw new Exception("Rest and visible surface cells must be protected from overload removal.");
        float removalVoid=0;foreach(var entry in removalPlan)removalVoid+=entry.emptiness;
        if(removalVoid!=2)throw new Exception("Two removed cells in a two-kind column must add 2/2 each, for two total emptiness.");
        if(removalSource[0].cells.Length!=3||removalSource[1].cells.Length!=2)
            throw new Exception("Planning the removal effect must not mutate the schedule before execution.");
        if(BlockXXPrototype.PlanOverload(removalSource,0,42).Count!=0)
            throw new Exception("Recovered overload must never remove any cells.");
        var soloRest=new List<BlockXXPrototype.Piece>{new BlockXXPrototype.Piece{action=4,cells=BlockXXPrototype.Shapes[4]}};
        if(BlockXXPrototype.EvaluateRest(soloRest).emptiness!=4)
            throw new Exception("Rest without multitasking must retain full emptiness per unused cell.");
        foreach(int load in new[]{0,6,7,8,9})
        {
            var tower=new Vector3Int[load+1];for(int i=0;i<tower.Length;i++)tower[i]=new Vector3Int(0,i,0);
            var soloCells=(Vector3Int[])BlockXXPrototype.Shapes[4].Clone();
            for(int i=0;i<soloCells.Length;i++)soloCells[i]+=new Vector3Int(1,0,2);
            var example=new List<BlockXXPrototype.Piece>{new BlockXXPrototype.Piece{action=0,cells=tower},new BlockXXPrototype.Piece{action=4,cells=soloCells}};
            var report=BlockXXPrototype.EvaluateRest(example);
            if(report.rawLoad!=load||report.recoveryCapacity!=8||report.recoveryUsed!=Math.Min(load,8)||report.remainingLoad!=Math.Max(0,load-8)||report.emptiness!=Math.Max(0,8-load)/2f)
                throw new Exception("A solo four-cell rest block must recover eight, with fractional unused rest converted proportionally to emptiness.");
        }
        Debug.Log("BLOCKXX_UI_CHECKS_PASSED: doubled recovery, proportional unused rest, unchanged raw load, multitasking-scaled emptiness");
    }
}
#endif
