using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;

// One-click scene builder. Run from the Unity menu:
//   EcoDefender → Create Game Scene
// It creates an empty scene with a single GameBootstrap object (which builds
// the whole game at Play time), saves it as Assets/Scenes/GameScene.unity and
// adds it to Build Settings. Nothing else to wire by hand.
public static class EcoDefenderSetup
{
    private const string ScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("EcoDefender/Create Game Scene")]
    public static void CreateGameScene()
    {
        // Fresh empty scene (just the default camera + light)
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Add the bootstrap object — it builds everything else at runtime
        var go = new GameObject("GameBootstrap");
        go.AddComponent<GameBootstrap>();

        // Make sure the Scenes folder exists
        var dir = Path.GetDirectoryName(ScenePath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "EcoDefender",
            "GameScene criada em Assets/Scenes/GameScene.unity!\n\n" +
            "Ela ja esta aberta. Agora e so apertar PLAY.",
            "OK");
    }

    private static void AddSceneToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
