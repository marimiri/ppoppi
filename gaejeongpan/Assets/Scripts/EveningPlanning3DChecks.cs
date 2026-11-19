using System;
using System.Collections;
using System.IO;
using UnityEngine;

public sealed partial class EveningPrototype
{
    void PlanningRequire(bool value,string reason){if(!value)throw new Exception("3D planning: "+reason);}
    void CheckBoardRays()
    {
        FrameCamera();
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)
        {
            var screen=resultCamera.WorldToScreenPoint(new Vector3(x,0,4-y));
            PlanningRequire(TryPlanningCell(resultCamera.ScreenPointToRay(screen),out var cell)&&cell==new Vector2Int(x,y),"world-camera rays must select base-plane cells rather than ghost surfaces");
        }
        PlanningRequire(!TryPlanningCell(new Ray(new Vector3(-3,8,0),Vector3.down),out _),"outside rays must not clamp onto the board");
    }
    void CheckUnionOutline()
    {
        var volume=new System.Collections.Generic.HashSet<Vector3Int>{Vector3Int.zero};
        var mesh=BuildVolumeOutline(volume);PlanningRequire(mesh.GetIndices(0).Length==24,"one cube must have twelve outline edges");Destroy(mesh);
        volume.Add(Vector3Int.right);mesh=BuildVolumeOutline(volume);
        PlanningRequire(mesh.GetIndices(0).Length==24,"two joined cubes must have twelve cuboid edges without their shared boundary");Destroy(mesh);
        volume.Add(Vector3Int.up);mesh=BuildVolumeOutline(volume);
        PlanningRequire(mesh.GetIndices(0).Length==36,"L-shaped union must preserve eighteen convex and concave edges");Destroy(mesh);
        volume.Clear();for(int x=0;x<3;x++)for(int y=0;y<4;y++)for(int z=0;z<2;z++)volume.Add(new Vector3Int(x,y,z));
        mesh=BuildVolumeOutline(volume);PlanningRequire(mesh.GetIndices(0).Length==24,"solid 3x4x2 volume must have no voxel grid seams");Destroy(mesh);
        Debug.Log("EVENING_UNION_OUTLINE_CHECKS_PASSED");
    }
    void CheckSplitPlacement()
    {
        PlanningRequire(viewport.xMax<placementBoard.x&&placementBoard.yMax<794,"preview, placement and bottom stage navigation must not overlap");
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)
        {
            var at=PlanningCellRect(new Vector2Int(x,y)).center;
            PlanningRequire(TryFlatPlanningCell(at,out var cell)&&cell==new Vector2Int(x,y),"flat board cell picking must cover every cell");
        }
        PlanningRequire(!TryFlatPlanningCell(viewport.center,out _)&&!TryFlatPlanningCell(new Vector2(placementBoard.xMax,placementBoard.yMax),out _),"preview and outside edges must never be placement targets");
        Select(4);PlanningRequire(!HandlePlacementClick(viewport.center)&&board.Pieces.Count==0,"clicking the 3D preview must never place a block");
        var cursor=PlanningCellRect(new Vector2Int(1,1)).center;
        PlanningRequire(HandlePlacementClick(cursor)&&board.Pieces.Count==1&&predictionVoxelCount==4,"a flat-board click must commit and immediately refresh the left preview");
        yaw+=120;elevation-=25;FrameCamera();
        PlanningRequire(TryFlatPlanningCell(cursor,out var same)&&same==new Vector2Int(1,1),"orbiting the 3D camera must not change 2D picking");
        PlanningRequire(HandlePlacementClick(cursor)&&held==4&&board.Pieces.Count==0,"empty-hand flat-board click must retrieve the top block");
        hover=new Vector2Int(2,2);hasCell=true;RefreshCandidate3D();
        PlanningRequire(candidateVisuals.Count>0&&board.Pieces.Count==0&&predictionVoxelCount==0,"moving a retrieved block must forecast without committing");
        ReturnHeld();hasCell=false;ResetBoardCamera();RebuildPlanning();
        float previous=2;
        for(int n=0;n<10;n++){var c=PlanningDensityColor(n);float brightness=c.grayscale;PlanningRequire(brightness<previous&&c.a==1,"equivalent transparent layers must become monotonically darker");previous=brightness;}
        PlanningRequire(PlanningDensityColor(0)==Color.white,"unoccupied flat cells must remain white");
        Debug.Log("EVENING_SPLIT_UI_CHECKS_PASSED: independent top-view picking, preview-only orbit, retrieval, moving ghost, density without numbers and bottom navigation");
    }
    void CaptureBoard(string path)
    {
        // An explicit offscreen render works even when the native player window is hidden.
        var texture=new RenderTexture(900,600,24);var image=new Texture2D(900,600,TextureFormat.RGB24,false);
        var previousActive=RenderTexture.active;var previousTarget=resultCamera.targetTexture;var previousRect=resultCamera.rect;
        try
        {
            texture.Create();resultCamera.targetTexture=texture;resultCamera.rect=new Rect(0,0,1,1);resultCamera.Render();
            RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,900,600),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {resultCamera.targetTexture=previousTarget;resultCamera.rect=previousRect;RenderTexture.active=previousActive;texture.Release();Destroy(texture);Destroy(image);}
    }
    IEnumerator CheckRuntime(string path)
    {
        Debug.Log("EVENING_RUNTIME_CHECKS_STARTED");EveningBoardChecks.Run();EveningStageChecks.Run();CheckUnionOutline();CheckSplitPlacement();yield return null;
        PlanningRequire(!result&&resultCamera.enabled&&visuals.Count>0&&targetVisuals.Count>0,"stages must open on a visible 3D board with the target already present");
        PlanningRequire(predictionVoxelCount==0&&targetVoxelCount==Stage.ReferencePieceCount*4,"empty planning must contain the complete target but no predicted cubes");
        CheckBoardRays();yaw=110;elevation=68;FrameCamera();CheckBoardRays();ResetBoardCamera();
        var first=Stage.HintSteps[0];Select(first.Action);PlanningRequire(Commit(first.Cells()),"a first block must be placeable without an inventory quota");
        PlanningRequire(predictionVoxelCount==4&&predictionVisuals.Count>0&&targetVoxelCount==Stage.ReferencePieceCount*4,"committing must refresh the full lavender predicted volume");
        var properties=new MaterialPropertyBlock();predictionVisuals[0].GetComponent<MeshRenderer>().GetPropertyBlock(properties);var tint=properties.GetColor("_Color");
        PlanningRequire(Mathf.Abs(tint.a-.30f)<.001f&&tint.b>tint.r&&tint.r>tint.g&&predictionVisuals[0].GetComponent<MeshFilter>().sharedMesh.GetTopology(0)==MeshTopology.Triangles,"prediction must have a filled lavender surface at 70 percent transparency");
        targetVisuals[0].GetComponent<MeshRenderer>().GetPropertyBlock(properties);var targetTint=properties.GetColor("_Color");
        PlanningRequire(targetTint.a>=.85f&&targetTint.grayscale<.4f&&targetVisuals[0].GetComponent<MeshFilter>().sharedMesh.GetTopology(0)==MeshTopology.Triangles,"target must have a dark high-contrast stroke with visible physical width");
        int count=board.Pieces.Count;Select(4);hover=new Vector2Int(1,1);hasCell=true;RefreshCandidate3D();
        PlanningRequire(candidateVisuals.Count>0&&board.Pieces.Count==count&&predictionVoxelCount==4,"hover forecasts must not mutate the committed plan");ReturnHeld();hasCell=false;
        PlanningRequire(candidateVisuals.Count==0,"returning a held block must remove its provisional ghost");
        ToggleTarget();foreach(var go in targetVisuals)PlanningRequire(!go.activeSelf,"target toggle must hide target renderers");ToggleTarget();
        TogglePrediction();foreach(var go in predictionVisuals)PlanningRequire(!go.activeSelf,"prediction toggle must hide predicted renderers");TogglePrediction();
        PlanningRequire(board.Pieces.Count==count,"visibility toggles must never change the puzzle state");
        CaptureBoard(Path.Combine(Path.GetDirectoryName(path),"planning-3d.png"));
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(path),"split-ui.png"));yield return new WaitForSeconds(.25f);
        for(int index=0;index<EveningStages.All.Length;index++)
        {
            SwitchStage(index);RestartStage();
            PlanningRequire(!result&&resultCamera.enabled&&board.Pieces.Count==0&&predictionVoxelCount==0,"stage entry/retry must preserve the 3D target and clear the plan");
            for(int a=0;a<5;a++)PlanningRequire(CanSelect(a),"every activity must be selectable in every stage without quotas");
            NextHint();PlanningRequire(hintIndex==0&&resultCamera.enabled,"hints must stay on the 3D board");
            Select(Stage.HintSteps[0].Action);PlanningRequire(!StartResult(),"held blocks must still prevent execution");ReturnHeld();
            if(index==2)
            {
                // Independent solution through the actual top-view click handler, not HintSteps.
                var positions=new[]{new Vector2Int(1,1),new Vector2Int(1,1),new Vector2Int(2,1)};
                foreach(var position in positions){Select(4);PlanningRequire(HandlePlacementClick(PlanningCellRect(position).center),"stage three square solution must be placeable through UI picking");}
                PlanningRequire(board.Pieces.Count==3&&Stage.Complete(board),"stage three must be solvable with three squares");
                CaptureBoard(Path.Combine(Path.GetDirectoryName(path),"stage-three-solution.png"));
            }
            else foreach(var step in Stage.HintSteps){Select(step.Action);PlanningRequire(Commit(step.Cells()),"reference solution must be placeable");}
            if(index==4||index==5)CaptureBoard(Path.Combine(Path.GetDirectoryName(path),"stage-"+(index+1)+"-solution.png"));
            for(int a=0;a<5;a++)PlanningRequire(CanSelect(a),"using a reference solution must not exhaust any activity");
            PlanningRequire(predictionVoxelCount==Stage.ReferencePieceCount*4&&Stage.Complete(board),"live prediction must match the goal before execution");
            PlanningRequire(StartResult(),"valid plans must begin growth");while(animating)yield return null;
            PlanningRequire(stageEvaluated&&stageAttemptPassed&&clearedStages[index],"an exact goal must succeed after growth");
            if(index==2){Debug.Log("EVENING_STAGE_THREE_UI_CLEAR_CONFIRMED: three square blocks placed through top-view clicks, action started and success flag set");CaptureBoard(Path.Combine(Path.GetDirectoryName(path),"stage-three-cleared.png"));}
            foreach(var entry in growing)PlanningRequire(Mathf.Abs(entry.Key.position.y-entry.Value-.5f)<.001f,"executed cubes must finish at the derived height");
            BackToPlan();PlanningRequire(resultCamera.enabled&&predictionVoxelCount==Stage.ReferencePieceCount*4,"returning to planning must restore the lavender predicted shape");
            Select(0);PlanningRequire(Commit(EveningBoard.Shapes[0]),"extra blocks must remain allowed beyond the reference piece count");
            PlanningRequire(!Stage.Complete(board)&&Stage.Mismatches(board)>0,"unlimited placement must still reject an overfilled goal");
            PlanningRequire(StartResult(),"overfilled plans must allow inspection");while(animating)yield return null;
            PlanningRequire(stageEvaluated&&!stageAttemptPassed&&stageIndex==index,"overfilling must fail without advancing the stage");
            BackToPlan();Undo();PlanningRequire(Stage.Complete(board),"undo must restore both the goal and its live preview");
            RestartStage();PlanningRequire(board.Pieces.Count==0&&targetVisuals.Count>0,"retry must retain only the target and board");
        }
        PlanningRequire(clearedStages.Length==7&&AllStagesCleared,"all seven stage completion flags must work through the new finale");
        SwitchStage(6);for(int i=0;i<Stage.HintSteps.Length;i++){var step=Stage.HintSteps[i];Select(step.Action);Commit(step.Cells());}
        CaptureBoard(Path.Combine(Path.GetDirectoryName(path),"complex-preview.png"));
        SwitchStage(0);
        Debug.Log("EVENING_STAGE_RUNTIME_CHECKS_PASSED: seven stages, varied-shape bridge progression and moved terrace finale, unlimited types/counts, success/failure, retry, undo and navigation");
        Debug.Log("EVENING_RUNTIME_CHECKS_PASSED: split UI, fixed 2D top-view placement, gray target, live lavender outline prediction, candidate isolation, camera orbit and return to planning");
        Application.Quit();
    }
}
