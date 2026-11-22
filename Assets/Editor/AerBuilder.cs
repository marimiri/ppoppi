#if UNITY_EDITOR
using System;
using System.IO;
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
        if(windVolume!=17||windVolume%4==0||wind.Allows(0))throw new Exception("Wind target must intrinsically require S: positive-only volume is a multiple of four");
        var oldWindExploit=new EveningBoard();
        foreach(var step in new[]{new AerStage.Step(3,0,0,2),new AerStage.Step(4,1,1),new AerStage.Step(3,1,2,2),new AerStage.Step(3,2,2,2)})oldWindExploit.TryPlace(step.Action,step.Cells(),out _);
        if(wind.Differences(oldWindExploit)==0||wind.Complete(oldWindExploit))throw new Exception("Old S-free wind reconstruction still accepted");
        var repeated=new EveningBoard();var repeatStage=AerStages.All[1];
        repeated.TryPlace(4,repeatStage.Solution[0].Cells(),out _);
        var repeatAdvice=AerHints.Next(repeatStage,repeated);
        if(repeatAdvice.Type!=AerHints.Kind.Place||repeatAdvice.Step!=1)throw new Exception("Hints must count duplicate placements separately");
        if(AerStages.All.Length!=13||AerStages.BuildingStages.Length!=8||!AerStages.All[7].Weather||!AerStages.All[12].Weather)throw new Exception("Progression layout changed");
        int[] nameSteps={3,4,3,4,4,5,6,7};
        for(int i=0;i<8;i++)
        {
            var stage=AerStages.All[AerStages.BuildingStages[i]];int max=0;foreach(int h in stage.Target)max=Math.Max(max,h);
            var weather=AerStages.All[i<4?7:12];
            if(stage.Supply!=nameSteps[i]||stage.Supply>=weather.Supply||max>3)throw new Exception("Name difficulty envelope: "+stage.Name);
        }
        foreach(int index in new[]{7,12})
        {
            int max=0;foreach(int h in AerStages.All[index].Target)max=Math.Max(max,h);
            if(max!=4)throw new Exception("Weather milestones should combine four height levels");
        }
        foreach(int action in new[]{0,1,2,3,4})
        {
            int unlock=action==0?11:action==1?10:action==2?8:action==3?5:0;
            for(int i=0;i<AerStages.All.Length;i++)if(AerStages.All[i].Allows(action)!=(i>=unlock))throw new Exception("Unlock timing changed");
        }
        var unlimited=new EveningBoard();for(int i=0;i<100;i++)if(!unlimited.TryPlace(4,(Vector2Int[])EveningBoard.Shapes[4].Clone(),out _))throw new Exception("Unexpected piece cap");
        if(unlimited.MaxHeight!=100)throw new Exception("Unlimited overlap failed");
        var effectTest=new EveningBoard();effectTest.TryPlace(0,(Vector2Int[])EveningBoard.Shapes[0].Clone(),out _);
        if(effectTest.MaxHeight!=2)throw new Exception("T mino must add 2");
        var combined=new[]{new AerStage.Step(4,1,1),new AerStage.Step(1,0,1)};
        var forward=new EveningBoard();var reverse=new EveningBoard();
        foreach(var s in combined)forward.TryPlace(s.Action,s.Cells(),out _);
        for(int i=combined.Length-1;i>=0;i--)reverse.TryPlace(combined[i].Action,combined[i].Cells(),out _);
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)if(forward.Heights()[x,y]!=reverse.Heights()[x,y])throw new Exception("Subtraction must be order independent");
        if(forward.Heights()[1,1]!=0||forward.Heights()[2,2]!=1)throw new Exception("S mino must subtract one and clamp final height to zero");
        foreach(var voxel in forward.Voxels())if(voxel.Action!=4)throw new Exception("Voxel source color lost");
        var amplifier=new EveningBoard();amplifier.TryPlace(0,(Vector2Int[])EveningBoard.Shapes[0].Clone(),out _);foreach(var v in amplifier.Voxels())if(v.Action!=0)throw new Exception("T voxel color lost");
        var swatches=new[]{"FEECB1","FFB2BC","D4B4EF","94E8E9","FFDBEE"};
        for(int i=0;i<5;i++)if(ColorUtility.ToHtmlStringRGB(EveningBoard.Colors[i])!=swatches[i])throw new Exception("Palette mismatch");
        Debug.Log("AER_RULES_PASSED: 13 solutions, exact/85% matching, unlocks, required effects, unlimited supply, palette, source colors, S/T effects and boundaries");
    }
}
#endif
