#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class BlockXXBuilder
{
    [MenuItem("Block XX/Build Windows")]
    public static void BuildWindows()
    {
        BlockXXUIChecks.Run();
        const string scenePath="Assets/Scenes/BlockXX.unity";
        if(!File.Exists(scenePath))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Block XX Prototype").AddComponent<BlockXXPrototype>();
            EditorSceneManager.SaveScene(scene,scenePath);
        }
        PlayerSettings.productName="Block XX";
        PlayerSettings.defaultScreenWidth=1440;
        PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.resizableWindow=true;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};
        Directory.CreateDirectory("Builds");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{scenePath},locationPathName="Builds/BlockXX.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None
        });
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("Block XX build failed: "+report.summary.result);
        Debug.Log("Block XX Windows build created: Builds/BlockXX.exe");
    }
}
#endif
