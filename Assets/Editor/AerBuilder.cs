#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AerBuilder
{
    [MenuItem("Protocol Aer/Build Windows")]
    public static void BuildWindows()
    {
        CheckRules();
        PlayerSettings.companyName="Student Project";PlayerSettings.productName="Protocol Aer";
        PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.resizableWindow=true;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        var input=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var activeInput=input.FindProperty("activeInputHandler");if(activeInput!=null){activeInput.intValue=0;input.ApplyModifiedProperties();}
        var pixelFont=AssetImporter.GetAtPath("Assets/Resources/Fonts/Pixel.ttf") as TrueTypeFontImporter;
        if(pixelFont==null)throw new Exception("Pixel font missing");
        pixelFont.fontRenderingMode=FontRenderingMode.HintedRaster;pixelFont.fontSize=16;pixelFont.SaveAndReimport();
        foreach(string path in new[]{"Assets/Resources/Art/Sky.png","Assets/Resources/Art/Character.png","Assets/Resources/Art/TitleChalk.png"})
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
            importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;
            importer.filterMode=path.Contains("Character")?FilterMode.Point:FilterMode.Bilinear;importer.SaveAndReimport();
        }
        Directory.CreateDirectory("Assets/Scenes");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(!EditorSceneManager.SaveScene(scene,"Assets/Scenes/Main.unity"))throw new Exception("Could not save scene");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Main.unity",true)};
        AssetDatabase.SaveAssets();Directory.CreateDirectory("Builds");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName="Builds/ProtocolAer.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
        Debug.Log("AER_BUILD_SUCCEEDED: Builds/ProtocolAer.exe");
    }
    static void CheckRules()
    {
        var shape=new int[5,5];shape[0,0]=1;
        foreach(var expected in new[]{24,32,40})
        {
            if(expected==32)shape[1,0]=1;
            if(expected==40){shape[0,1]=1;shape[1,1]=1;}
            var outline=AerTargetOutline.Build(shape);
            if(outline.vertexCount!=expected)throw new Exception("Exterior outline contains coplanar voxel seams");
            UnityEngine.Object.DestroyImmediate(outline);
        }
        foreach(var stage in AerStages.All)
        {
            var board=new EveningBoard();foreach(var step in stage.Solution)
            {if(!board.TryPlace(step.Action,step.Cells(),out _))throw new Exception("Invalid solution: "+stage.Name);}
            if(!stage.Complete(board)||stage.Similarity(board)!=100)throw new Exception("Solution evaluation");
            board.Undo();if(stage.Similarity(board)>=100||!stage.Weather&&stage.Complete(board))throw new Exception("Incomplete exact board accepted");
        }
        var test=new EveningBoard();if(test.TryPlace(4,new[]{new Vector2Int(4,4),new Vector2Int(5,4),new Vector2Int(4,5),new Vector2Int(5,5)},out _))throw new Exception("Boundary violation accepted");
        foreach(var at in new[]{new Vector2Int(1,1),new Vector2Int(1,1)}){var cells=(Vector2Int[])EveningBoard.Shapes[4].Clone();for(int i=0;i<4;i++)cells[i]+=at;test.TryPlace(4,cells,out _);}
        if(test.Heights()[1,1]!=2||test.Coverage!=4||test.MaxHeight!=2)throw new Exception("Overlap must become height");
        foreach(var pair in new[]{new[]{5,3},new[]{8,2},new[]{10,1},new[]{11,0}})
        {
            var stage=AerStages.All[pair[0]];if(stage.RequiredAction!=pair[1]||stage.SupplyOf(pair[1])==0)throw new Exception("New mino must be required");
            var missing=new EveningBoard();foreach(var step in stage.Solution)if(step.Action!=pair[1])missing.TryPlace(step.Action,step.Cells(),out _);
            if(stage.Complete(missing)||stage.RequiredMinoUsed(missing))throw new Exception("New mino requirement bypassed");
        }
        var wind=AerStages.All[10];int windVolume=0;foreach(int height in wind.Target)windVolume+=height;
        if(windVolume!=6||windVolume%4==0||wind.Allows(0))throw new Exception("Wind target must intrinsically require S: positive-only volume is a multiple of four");
        var oldWindExploit=new EveningBoard();
        foreach(var step in new[]{new AerStage.Step(3,0,0,2),new AerStage.Step(4,1,1),new AerStage.Step(3,1,2,2),new AerStage.Step(3,2,2,2)})oldWindExploit.TryPlace(step.Action,step.Cells(),out _);
        if(wind.Differences(oldWindExploit)==0||wind.Complete(oldWindExploit))throw new Exception("Old S-free wind reconstruction still accepted");
        var repeated=new EveningBoard();var repeatStage=AerStages.All[1];
        repeated.TryPlace(4,repeatStage.Solution[0].Cells(),out _);
        var repeatAdvice=AerHints.Next(repeatStage,repeated);
        if(repeatAdvice.Type!=AerHints.Kind.Place||repeatAdvice.Step!=1)throw new Exception("Hints must count duplicate placements separately");
        if(AerStages.All.Length!=13||AerStages.BuildingStages.Length!=8||!AerStages.All[7].Weather||!AerStages.All[12].Weather)throw new Exception("Progression layout changed");
        int[] nameSteps={3,4,3,4,4,5,4,4};
        for(int i=0;i<8;i++)
        {
            var stage=AerStages.All[AerStages.BuildingStages[i]];int max=0;foreach(int h in stage.Target)max=Math.Max(max,h);
            var weather=AerStages.All[i<4?7:12];
            if(stage.Supply!=nameSteps[i]||stage.Supply>=weather.Supply||max>3)throw new Exception("Name difficulty envelope: "+stage.Name);
        }
        foreach(int index in new[]{7,12})
        {
            int max=0;foreach(int h in AerStages.All[index].Target)max=Math.Max(max,h);
            if(max!=(index==7?4:2))throw new Exception("Weather height envelope changed");
        }
        foreach(int action in new[]{0,1,2,3,4})
        {
            int unlock=action==0?11:action==1?10:action==2?8:action==3?5:0;
            for(int i=0;i<AerStages.All.Length;i++)if(AerStages.All[i].Allows(action)!=(i>=unlock))throw new Exception("Unlock timing changed");
        }
        var unlimited=new EveningBoard();for(int i=0;i<100;i++)if(!unlimited.TryPlace(4,(Vector2Int[])EveningBoard.Shapes[4].Clone(),out _))throw new Exception("Unexpected piece cap");
        if(unlimited.MaxHeight!=100)throw new Exception("Unlimited overlap failed");
        var effectTest=new EveningBoard();effectTest.TryPlace(0,(Vector2Int[])EveningBoard.Shapes[0].Clone(),out _);
        effectTest.TryPlace(1,(Vector2Int[])EveningBoard.Shapes[1].Clone(),out _);
        if(effectTest.MaxHeight!=0||effectTest.Voxels().Count!=0)throw new Exception("S/T operators must not create their own volume");
        var inverted=new EveningBoard();
        foreach(var s in new[]{new AerStage.Step(4,1,1),new AerStage.Step(1,0,1)})inverted.TryPlace(s.Action,s.Cells(),out _);
        if(inverted.OutputOf(inverted.Pieces[0])!=-1||inverted.Heights()[2,2]!=0)throw new Exception("S must reverse all four ordinary cells, including cells outside its footprint");
        var amplifier=new EveningBoard();
        foreach(var s in new[]{new AerStage.Step(4,1,1),new AerStage.Step(0,1,1)})amplifier.TryPlace(s.Action,s.Cells(),out _);
        if(amplifier.OutputOf(amplifier.Pieces[0])!=2||amplifier.Heights()[1,2]!=2||amplifier.Heights()[3,1]!=0)throw new Exception("T must amplify the whole ordinary mino but not fill its own empty cells");
        amplifier.TryPlace(0,new AerStage.Step(0,1,1).Cells(),out _);
        if(amplifier.OutputOf(amplifier.Pieces[0])!=2||amplifier.MaxHeight!=2)throw new Exception("Repeated T must not amplify recursively");
        foreach(var v in amplifier.Voxels())if(v.Action!=4||v.PieceId!=amplifier.Pieces[0].Id)throw new Exception("Amplified voxels must retain their ordinary source mino");
        var combined=new[]{new AerStage.Step(4,1,1),new AerStage.Step(1,0,1),new AerStage.Step(0,1,1),new AerStage.Step(4,2,2)};
        var forward=new EveningBoard();var reverse=new EveningBoard();
        foreach(var s in combined)forward.TryPlace(s.Action,s.Cells(),out _);
        for(int i=combined.Length-1;i>=0;i--)reverse.TryPlace(combined[i].Action,combined[i].Cells(),out _);
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)if(forward.Heights()[x,y]!=reverse.Heights()[x,y])throw new Exception("Modifier effects must be placement-order independent");
        if(forward.OutputOf(forward.Pieces[0])!=-2||forward.OutputOf(forward.Pieces[3])!=2||forward.Heights()[2,2]!=0||forward.Heights()[3,3]!=2)throw new Exception("S+T must produce -2 without chaining through neighboring minos");
        forward.TryPlace(1,new AerStage.Step(1,0,1).Cells(),out _);forward.TryPlace(0,new AerStage.Step(0,1,1).Cells(),out _);
        if(forward.OutputOf(forward.Pieces[0])!=-2||forward.OutputOf(forward.Pieces[3])!=2)throw new Exception("Duplicate modifiers must apply only once");
        foreach(var voxel in forward.Voxels())if(voxel.Action!=4)throw new Exception("Voxel source color lost");
        // Learning stages isolate the new effect; the finale combines two
        // simple -2 carving operation with meaningful target improvement.
        foreach(int index in new[]{10,11,12})
        {
            var stage=AerStages.All[index];var board=new EveningBoard();
            foreach(var step in stage.Solution)board.TryPlace(step.Action,step.Cells(),out _);
            int invertedCount=0,amplifiedCount=0,combinedCount=0;
            foreach(var piece in board.Pieces)
            {
                int output=board.OutputOf(piece);
                if(output==-2){combinedCount++;if(index!=12)throw new Exception("Only the finale teaches combined S/T");}
                if(output<0)invertedCount++;if(output==2)amplifiedCount++;
            }
            if(invertedCount!=(index==11?0:1)||amplifiedCount!=(index==12?2:index==11?1:0)||combinedCount!=(index==12?1:0))throw new Exception("Each new effect must have one readable subject");
            if(stage.Supply!=(index==12?7:4))throw new Exception("Late solution complexity increased");
            foreach(int action in index==12?new[]{1,0}:new[]{stage.RequiredAction})
            {
                var without=new EveningBoard();foreach(var step in stage.Solution)if(step.Action!=action)without.TryPlace(step.Action,step.Cells(),out _);
                if(stage.Complete(without))throw new Exception("Removing a taught effect must not solve the target");
            }
        }
        // Match the waterworks target scale, without enforcing a play-time
        // piece limit or an operator-specific completion condition.
        var finale=AerStages.All[12];var finalBoard=new EveningBoard();var waterBoard=new EveningBoard();
        foreach(var step in finale.Solution)finalBoard.TryPlace(step.Action,step.Cells(),out _);
        foreach(var step in AerStages.All[9].Solution)waterBoard.TryPlace(step.Action,step.Cells(),out _);
        if(finalBoard.MaxHeight!=waterBoard.MaxHeight||finalBoard.Voxels().Count!=waterBoard.Voxels().Count||Math.Abs(finalBoard.Coverage-waterBoard.Coverage)>2)throw new Exception("Finale exceeds waterworks target scale");
        CheckPositiveShortcut(finale);
        var withoutCut=new EveningBoard();
        foreach(var piece in finalBoard.Pieces)if(finalBoard.OutputOf(piece)!=-2)withoutCut.TryPlace(piece.Action,piece.Cells,out _);
        if(withoutCut.Voxels().Count-finalBoard.Voxels().Count<4||finale.Complete(withoutCut))throw new Exception("Minus-two cut must visibly remove a two-layer pair");
        // With unlimited supply, doubled outputs can always be replaced by
        // duplicate ordinary pieces. Preserve such creative alternatives.
        var alternative=new EveningBoard();
        foreach(var piece in finalBoard.Pieces)if(piece.Action!=0)
        {
            alternative.TryPlace(piece.Action,piece.Cells,out _);
            if(Math.Abs(finalBoard.OutputOf(piece))==2)alternative.TryPlace(piece.Action,piece.Cells,out _);
        }
        if(finale.Similarity(alternative)!=100||!finale.Complete(alternative))throw new Exception("Alternative finale solution must remain valid");
        var swatches=new[]{"FEECB1","FFB2BC","D4B4EF","94E8E9","FFDBEE"};
        for(int i=0;i<5;i++)if(ColorUtility.ToHtmlStringRGB(EveningBoard.Colors[i])!=swatches[i])throw new Exception("Palette mismatch");
        Debug.Log("AER_RULES_PASSED: 13 solutions; whole-mino inversion/amplification; -2 combinations; no stacking/chaining; order independence; source colors; unlocks and required effects");
    }    // For a 20-voxel target, 85% IoU with positive four-cell pieces can only
    // occur at volume 20 with at most one missing and one surplus voxel.
    // T-only outputs are repeated positive pieces, so this also covers T alone.
    static void CheckPositiveShortcut(AerStage stage)
    {
        int[] target=new int[25];for(int y=0;y<5;y++)for(int x=0;x<5;x++)target[x+y*5]=stage.Target[x,y];
        var placements=new List<int[]>();var seen=new HashSet<string>();
        foreach(int action in new[]{2,3,4})for(int mirror=0;mirror<2;mirror++)for(int turn=0;turn<4;turn++)
        {
            var shape=(Vector2Int[])EveningBoard.Shapes[action].Clone();
            if(mirror==1)shape=EveningBoard.Mirror(shape);
            for(int r=0;r<turn;r++)shape=EveningBoard.Rotate(shape,1);
            for(int y=0;y<5;y++)for(int x=0;x<5;x++)
            {
                var cells=(Vector2Int[])shape.Clone();for(int i=0;i<4;i++)cells[i]+=new Vector2Int(x,y);
                if(!EveningBoard.ValidCells(cells))continue;
                int[] ids=Array.ConvertAll(cells,c=>c.x+c.y*5);Array.Sort(ids);
                if(seen.Add(string.Join(",",ids)))placements.Add(ids);
            }
        }
        if(PositiveFive(target,0,0,false,placements,new HashSet<string>()))throw new Exception("Final weather has a positive-only 85% shortcut");
    }
    static bool PositiveFive(int[] remaining,int depth,int surplus,bool skipped,List<int[]> placements,HashSet<string> memo)
    {
        if(depth==5){int missing=0;foreach(int h in remaining)missing+=h;return missing<=1;}
        int first=Array.FindIndex(remaining,h=>h>0);if(first<0)return false;
        string key=string.Join(",",remaining)+"/"+depth+"/"+surplus+"/"+skipped;
        if(!memo.Add(key))return false;
        if(!skipped){var rest=(int[])remaining.Clone();rest[first]--;if(PositiveFive(rest,depth,surplus,true,placements,memo))return true;}
        foreach(var ids in placements)
        {
            if(Array.IndexOf(ids,first)<0)continue;
            int extra=0;foreach(int i in ids)if(remaining[i]==0)extra++;
            if(extra+surplus>1)continue;
            var rest=(int[])remaining.Clone();foreach(int i in ids)rest[i]=Math.Max(0,rest[i]-1);
            if(PositiveFive(rest,depth+1,surplus+extra,skipped,placements,memo))return true;
        }
        return false;
    }
}
#endif
