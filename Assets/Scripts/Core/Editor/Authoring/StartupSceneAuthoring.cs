// 보관한 Main의 시각 계층을 복제해 Core 소유 시작 Scene을 제작합니다.
// 원본 Scene/Asset은 저장하지 않으며 복제본의 레거시 동작·미연결 버튼만 제거/비활성화합니다.
using System;
using System.IO;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Editor
{
    public static class StartupSceneAuthoring
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap/Startup.unity";
        private const string ReferencePath = "Assets/Scenes/Reference/Main.unity";

        [MenuItem("Tools/TeamHJD/The Developer/Create Core Startup Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create the startup Scene in Edit Mode.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("The startup Scene already exists; inspect it instead of overwriting it.");
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Scene target = default;
            try
            {
                // 빈 batch Editor의 이름 없는 Scene은 additive 제작의 기반으로 사용할 수 없습니다.
                var source = EditorSceneManager.OpenScene(ReferencePath, OpenSceneMode.Single);
                target = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                foreach (var root in source.GetRootGameObjects().Where(item => item.name == "Canvas" || item.name == "Main Camera" || item.name == "EventSystem"))
                {
                    var clone = Object.Instantiate(root); clone.name = root.name; SceneManager.MoveGameObjectToScene(clone, target);
                    foreach (var transform in clone.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                    foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                        if (behaviour != null && !behaviour.GetType().Assembly.GetName().Name.StartsWith("Unity", StringComparison.Ordinal))
                            Object.DestroyImmediate(behaviour);
                }
                var buttons = target.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Button>(true)).ToArray();
                var start = buttons.Single(button => button.name == "Start");
                var exit = buttons.Single(button => button.name == "Exit");
                foreach (var button in buttons)
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    button.interactable = button == start || button == exit;
                }
                var composition = new GameObject("Core Startup"); SceneManager.MoveGameObjectToScene(composition, target);
                var view = composition.AddComponent<StartMenuPresenter>(); view.Configure(start, exit);
                composition.AddComponent<StartupSceneRoot>().Configure(view);
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                if (!EditorSceneManager.SaveScene(target, ScenePath)) throw new IOException("Could not save the new startup Scene.");
                var scenes = EditorBuildSettings.scenes.Where(item => item.path != ScenePath).ToList();
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.Refresh();
                Debug.Log("Core startup Scene created from the existing Main visuals: " + ScenePath);
            }
            finally
            {
                if (target.IsValid() && target.isLoaded) EditorSceneManager.CloseScene(target, true);
                if (setup.Length > 0 && setup.All(item => !string.IsNullOrEmpty(item.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [MenuItem("Tools/TeamHJD/The Developer/Open Core Startup Scene")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath)) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
