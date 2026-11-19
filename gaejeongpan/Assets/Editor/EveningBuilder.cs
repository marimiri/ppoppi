#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal static class EveningBuilder
{
    [MenuItem("evening/Build Windows prototype")]
    public static void BuildWindows()
    {
        EveningBoardChecks.Run();EveningStageChecks.Run();EveningProjectSetup.CreateStarterScene();
        PlayerSettings.productName="evening";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        Directory.CreateDirectory("Builds");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName="Builds/evening.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("evening build failed: "+report.summary.result);
        Debug.Log("EVENING_WINDOWS_BUILD_SUCCEEDED: Builds/evening.exe");
    }
}
#endif
