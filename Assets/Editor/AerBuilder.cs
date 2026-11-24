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
            {if(!stage.CanPlace(board,step.Action)||!board.TryPlace(step.Action,step.Cells(),out _))throw new Exception("Invalid or unavailable solution: "+stage.Name);}
            if(!stage.Complete(board)||stage.Similarity(board)!=100)throw new Exception("Solution evaluation");
            board.Undo();if(stage.Similarity(board)>=100||!stage.Weather&&stage.Complete(board))throw new Exception("Incomplete exact board accepted");
        }
        var test=new EveningBoard();if(test.TryPlace(4,new[]{new Vector2Int(4,4),new Vector2Int(5,4),new Vector2Int(4,5),new Vector2Int(5,5)},out _))throw new Exception("Boundary violation accepted");
        foreach(var at in new[]{new Vector2Int(1,1),new Vector2Int(1,1)}){var cells=(Vector2Int[])EveningBoard.Shapes[4].Clone();for(int i=0;i<4;i++)cells[i]+=at;test.TryPlace(4,cells,out _);}
        if(test.Heights()[1,1]!=2||test.Coverage!=4||test.MaxHeight!=2)throw new Exception("Overlap must become height");
        foreach(var pair in new[]{new[]{5,3},new[]{8,2},new[]{10,0},new[]{11,1}})
        {
            var stage=AerStages.All[pair[0]];if(stage.RequiredAction!=pair[1]||stage.SupplyOf(pair[1])==0)throw new Exception("New mino must be required");
            var missing=new EveningBoard();foreach(var step in stage.Solution)if(step.Action!=pair[1])missing.TryPlace(step.Action,step.Cells(),out _);
            if(stage.Complete(missing)||stage.RequiredMinoUsed(missing))throw new Exception("New mino requirement bypassed");
        }
        var repeated=new EveningBoard();var repeatStage=AerStages.All[1];
        repeated.TryPlace(4,repeatStage.Solution[0].Cells(),out _);
        var repeatAdvice=AerHints.Next(repeatStage,repeated);
        if(repeatAdvice.Type!=AerHints.Kind.Place||repeatAdvice.Step!=1)throw new Exception("Hints must count duplicate placements separately");
        if(AerStages.All.Length!=13||AerStages.BuildingStages.Length!=8||!AerStages.All[7].Weather||!AerStages.All[12].Weather)throw new Exception("Progression layout changed");
        int[] nameSteps={3,4,3,4,4,5,4,4};
        for(int i=0;i<8;i++)
        {
            var stage=AerStages.All[AerStages.BuildingStages[i]];
            if(stage.Supply!=nameSteps[i]||stage.Supply>=AerStages.All[i<4?7:12].Supply)throw new Exception("Name difficulty envelope: "+stage.Name);
            foreach(int h in stage.Target)if(h>3)throw new Exception("Name height envelope");
        }
        var water=AerStages.All[9];
        if(water.Supply!=AerStages.All[8].Supply+1||water.SupplyOf(4)!=3||water.SupplyOf(3)!=1||water.SupplyOf(2)!=1)throw new Exception("Waterworks should add one overlap, with familiar pieces");
        int rotations=0;
        foreach(var step in water.Solution)if(step.Turns!=0){rotations++;if(step.Action!=2||step.Turns!=1)throw new Exception("Waterworks should rotate only its I vertically");}
        if(rotations!=1)throw new Exception("Waterworks must preserve one rotation task");
        foreach(int action in new[]{0,1,2,3,4})
        {
            int unlock=action==0?10:action==1?11:action==2?8:action==3?5:0;
            for(int i=0;i<AerStages.All.Length;i++)
            {
                bool expected=i==6?action==3:i==10?(action==4||action==2||action==0):i==11?(action==4||action==3||action==1):i>=unlock;
                if(AerStages.All[i].Allows(action)!=expected)throw new Exception("Stage teaching set/unlock timing changed");
            }
        }
        var sIntro=AerStages.All[11];var sProof=new EveningBoard();
        foreach(var step in sIntro.Solution)
        {
            if(step.Action==2||step.Action==0)throw new Exception("S introduction should provide only O L S");
            sProof.TryPlace(step.Action,step.Cells(),out _);
        }
        if(sIntro.Name!="환경 제어실"||sIntro.Similarity(sProof)!=100||!sIntro.Complete(sProof))throw new Exception("S stage order/name or solution mismatch");
        var tIntro=AerStages.All[10];
        if(tIntro.Name!="바람길 제어소"||tIntro.SupplyOf(4)!=2||tIntro.SupplyOf(2)!=1||tIntro.SupplyOf(0)!=1)throw new Exception("T must come first with O O I T at the unchanged building name");
        foreach(int action in new[]{4,2,0})
        {
            var without=new EveningBoard();foreach(var step in tIntro.Solution)if(step.Action!=action)without.TryPlace(step.Action,step.Cells(),out _);
            if(tIntro.Complete(without))throw new Exception("Each O I T type must contribute to the reference reconstruction");
        }
        // The L-only teaching set keeps the old relay target exactly.
        var oldRelay=new AerStage("previous relay","TEST","",new AerStage.Step(4,1,1),new AerStage.Step(4,0,2),new AerStage.Step(3,1,0,2),new AerStage.Step(3,2,1,1));
        var relay=AerStages.All[6];
        if(relay.SupplyOf(3)!=4)throw new Exception("Relay must have four L in its example solution");
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)if(relay.Target[x,y]!=oldRelay.Target[x,y])throw new Exception("L-only relay changed the target");
        var nine=new AerStage.Step[9];var ten=new AerStage.Step[10];
        for(int i=0;i<10;i++){ten[i]=new AerStage.Step(4,0,0);if(i<9)nine[i]=ten[i];}
        var final89=new AerStage("cutoff","WEATHER-02","",nine);var first89=new AerStage("cutoff","WEATHER-01","",nine);var final90=new AerStage("cutoff","WEATHER-02","",ten);
        var eight=new EveningBoard();for(int i=0;i<8;i++)Add(eight,4,0,0);
        if(final89.Similarity(eight)!=89||final89.Complete(eight)||!first89.Complete(eight))throw new Exception("Final 90 / first weather 85 thresholds incorrect");
        Add(eight,4,0,0);
        if(final90.Similarity(eight)!=90||!final90.Complete(eight))throw new Exception("Final must accept 90 percent");
        if(AerStages.All[12].ClearPercent!=90||AerStages.All[7].ClearPercent!=85)throw new Exception("Weather stage thresholds changed");
        foreach(var stage in AerStages.All)
        {
            if(stage.LimitedSupply==stage.Weather)throw new Exception("Only weather may have unlimited supply");
            var stock=new EveningBoard();foreach(var step in stage.Solution)stock.TryPlace(step.Action,step.Cells(),out _);
            if(!stage.WithinSupply(stock))throw new Exception("Reference solution exceeds supply");
            if(stage.LimitedSupply)
            {
                int action=stage.Solution[0].Action;
                if(stage.Remaining(stock,action)!=0||stage.CanPlace(stock,action))throw new Exception("Stock should be exhausted by the reference solution");
                stock.TryPlace(action,stage.Solution[0].Cells(),out _);
                if(stage.WithinSupply(stock)||stage.Complete(stock))throw new Exception("Over-quota solution accepted");
            }
            else if(stage.Remaining(stock,4)!=-1||!stage.CanPlace(stock,4))throw new Exception("Weather supply must remain unlimited");
        }
        var unlimited=new EveningBoard();for(int i=0;i<100;i++)if(!unlimited.TryPlace(4,(Vector2Int[])EveningBoard.Shapes[4].Clone(),out _))throw new Exception("Unexpected piece cap");
        if(unlimited.MaxHeight!=100)throw new Exception("Unlimited overlap failed");
        var empty=new EveningBoard();Add(empty,0,0,0);Add(empty,1,0,0);
        if(empty.MaxHeight!=0||empty.Voxels().Count!=0)throw new Exception("Operators need ordinary source heights");
        var lower=new EveningBoard();Add(lower,4,1,1);Add(lower,1,1,1);
        if(lower.Heights()[1,1]!=1||lower.Heights()[1,2]!=0||lower.Heights()[2,2]!=0||lower.Heights()[3,1]!=0)throw new Exception("S must set only its own footprint to the original minimum, including zero");
        var raise=new EveningBoard();Add(raise,4,1,1);Add(raise,0,1,1);
        if(raise.MaxHeight!=1||raise.Heights()[3,1]!=1||raise.Heights()[1,2]!=1||raise.Heights()[0,0]!=0)throw new Exception("T must set only its own footprint to the original maximum");
        bool raisedEmpty=false;foreach(var v in raise.Voxels())if(v.X==3&&v.Z==1){raisedEmpty=true;if(v.Action!=0)throw new Exception("T-created layer must use T swatch");}
        if(!raisedEmpty||raise.Voxels().Count!=5)throw new Exception("Raised empty cell missing from voxel geometry");
        var combined=new[]{new AerStage.Step(4,1,1),new AerStage.Step(4,2,2),new AerStage.Step(1,1,1),new AerStage.Step(0,1,1)};
        var forward=new EveningBoard();var reverse=new EveningBoard();
        foreach(var step in combined)forward.TryPlace(step.Action,step.Cells(),out _);
        for(int i=combined.Length-1;i>=0;i--)reverse.TryPlace(combined[i].Action,combined[i].Cells(),out _);
        if(forward.Heights()[2,2]!=2||forward.Heights()[3,1]!=0||forward.Heights()[2,1]!=1||forward.Heights()[1,1]!=2||forward.Heights()[1,2]!=0)throw new Exception("Mixed overlap must preserve original common cells and apply exclusive S/T cells");
        var before=forward.Heights();Add(forward,0,1,1);Add(forward,1,1,1);
        for(int x=0;x<5;x++)for(int y=0;y<5;y++)if(before[x,y]!=reverse.Heights()[x,y]||before[x,y]!=forward.Heights()[x,y])throw new Exception("Order dependence or duplicate operator stacking");
        foreach(var stage in AerStages.All)
        {
            var board=new EveningBoard();foreach(var step in stage.Solution)board.TryPlace(step.Action,step.Cells(),out _);
            int count=0;foreach(int h in board.Heights())count+=h;
            if(board.Voxels().Count!=count)throw new Exception("Voxel height/geometry mismatch");
        }
        var final=AerStages.All[12];var full=new EveningBoard();foreach(var step in final.Solution)full.TryPlace(step.Action,step.Cells(),out _);
        if(full.Heights()[2,2]!=full.BaseHeights()[2,2]||full.Heights()[1,3]!=full.BaseHeights()[1,3])throw new Exception("Final shared cells should retain original heights");
        foreach(int action in new[]{0,1})
        {
            var without=new EveningBoard();foreach(var step in final.Solution)if(step.Action!=action)without.TryPlace(step.Action,step.Cells(),out _);
            if(final.Complete(without))throw new Exception("Final reference should use both leveling effects meaningfully");
        }
        var swatches=new[]{"FEECB1","FFB2BC","D4B4EF","94E8E9","FFDBEE"};
        for(int i=0;i<5;i++)if(ColorUtility.ToHtmlStringRGB(EveningBoard.Colors[i])!=swatches[i])throw new Exception("Palette mismatch");
        Debug.Log("AER_RULES_PASSED: 13 solutions; eased waterworks; local minimum/maximum leveling; original mixed overlap; zero cells; no order dependence or duplicate stacking; complete voxel geometry");
    }
    static void Add(EveningBoard board,int action,int x,int y)
    {var step=new AerStage.Step(action,x,y);if(!board.TryPlace(action,step.Cells(),out _))throw new Exception("Invalid test placement");}
}
#endif