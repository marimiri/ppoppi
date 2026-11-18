#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
internal static class BlockXXProjectSetup
{
    private const string ScenePath = "Assets/Scenes/BlockXX.unity";

    static BlockXXProjectSetup()
    {
        EditorApplication.delayCall += EnsurePlayableScene;
    }

    private static void EnsurePlayableScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(ScenePath)) return;
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Block XX Prototype");
        root.AddComponent<BlockXXPrototype>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("Block XX prototype scene created at " + ScenePath);
    }
}
#endif
