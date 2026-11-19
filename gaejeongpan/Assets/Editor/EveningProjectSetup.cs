#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class EveningProjectSetup
{
    public static void CreateStarterScene()
    {
        const string scenePath="Assets/Scenes/Main.unity";
        PlayerSettings.productName="evening";
        EditorSettings.serializationMode=SerializationMode.ForceText;
        Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh();
        if(!File.Exists(scenePath))
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            if(!EditorSceneManager.SaveScene(scene,scenePath))throw new System.Exception("Could not save the evening starter scene.");
        }
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};
        AssetDatabase.SaveAssets();
        Debug.Log("EVENING_PROJECT_READY: evening / Assets/Scenes/Main.unity / default 3D camera and light");
    }
}
#endif
