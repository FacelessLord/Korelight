using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelSpace.EditorTools
{
    /// <summary>Создаёт сцену Main и настраивает проект. Меню: Korelight → Setup Project.</summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Korelight/Setup Project")]
        public static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Game").AddComponent<GameManager>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "PixelSpace";
            PlayerSettings.productName = "Korelight";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.korelight.game");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.korelight.game");
            ApplyDesktopWindow();
            AssetDatabase.SaveAssets();
            Debug.Log("Korelight: project setup done");
        }

        /// <summary>Окно десктопной сборки: 1280×720, оконный режим, можно менять размер.</summary>
        [MenuItem("Korelight/Desktop Window Settings")]
        public static void ApplyDesktopWindow()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
            Debug.Log("Korelight: desktop window settings applied");
        }
    }
}
