using System.IO;
using System.Linq;
using TeamHjd.Game.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHjd.Game.Editor
{
    [InitializeOnLoad]
    internal static class TurretTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/TestScene/TurretTest.unity";
        private const string SourceScenePath = "Assets/Scenes/Main.unity";
        private const string RequestPath = "Temp/TurretTestSceneBuilder.request";

        private static readonly (string Path, string Name, Vector3 Position)[] TurretPlacements =
        {
            ("Assets/Prefabs/Tower/Turrets/CanonTurret/stage1/CanonTurret LV1.prefab", "CanonTurret LV1", new Vector3(-6f, 2.5f, 0f)),
            ("Assets/Prefabs/Tower/Turrets/CanonTurret/stage1/CanonTurret LV2.prefab", "CanonTurret LV2", new Vector3(0f, 2.5f, 0f)),
            ("Assets/Prefabs/Tower/Turrets/CanonTurret/stage1/CanonTurret LV3.prefab", "CanonTurret LV3", new Vector3(6f, 2.5f, 0f)),
            ("Assets/Prefabs/Tower/Turrets/MissileTurret/Stage1/MissileTurret LV1.prefab", "MissileTurret LV1", new Vector3(-6f, -2.5f, 0f)),
            ("Assets/Prefabs/Tower/Turrets/MissileTurret/Stage1/MissileTurret LV2.prefab", "MissileTurret LV2", new Vector3(0f, -2.5f, 0f)),
            ("Assets/Prefabs/Tower/Turrets/MissileTurret/Stage1/MissileTurret LV3.prefab", "MissileTurret LV3", new Vector3(6f, -2.5f, 0f))
        };

        static TurretTestSceneBuilder()
        {
            if (File.Exists(RequestPath))
            {
                EditorApplication.delayCall += RebuildRequestedScene;
            }
        }

        [MenuItem("Tools/The Developer/Rebuild Turret Test Scene")]
        private static void RebuildFromMenu()
        {
            RebuildScene();
        }

        private static void RebuildRequestedScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RebuildRequestedScene;
                return;
            }

            try
            {
                RebuildScene();
                File.Delete(RequestPath);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void RebuildScene()
        {
            Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CopyConfiguredAudioManager(testScene);

            foreach ((string path, string name, Vector3 position) in TurretPlacements)
            {
                InstantiatePrefab(testScene, path, name, position);
            }

            InstantiatePrefab(
                testScene,
                "Assets/Prefabs/Tower/ControlUnit.prefab",
                "ControlUnit",
                new Vector3(0f, -6f, 0f));

            GameObject controllerObject = new("[Debug] Turret Test Controller");
            controllerObject.AddComponent<TurretTestController>();

            EditorSceneManager.MarkSceneDirty(testScene);
            if (!EditorSceneManager.SaveScene(testScene, ScenePath))
            {
                throw new IOException($"Failed to save {ScenePath}");
            }

            Selection.activeGameObject = controllerObject;
            Debug.Log($"[TurretTestSceneBuilder] Rebuilt {ScenePath} with 6 turrets and ControlUnit.");
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, -1.5f, -10f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f, 1f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            cameraObject.AddComponent<AudioListener>();
        }

        private static void CopyConfiguredAudioManager(Scene testScene)
        {
            Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            AudioManager source = sourceScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<AudioManager>(true))
                .FirstOrDefault();

            if (source == null)
            {
                EditorSceneManager.CloseScene(sourceScene, true);
                throw new MissingReferenceException($"AudioManager was not found in {SourceScenePath}");
            }

            SceneManager.SetActiveScene(testScene);
            GameObject audioObject = new("AudioManager");
            AudioManager destination = audioObject.AddComponent<AudioManager>();
            EditorUtility.CopySerialized(source, destination);

            EditorSceneManager.CloseScene(sourceScene, true);
        }

        private static GameObject InstantiatePrefab(
            Scene scene,
            string prefabPath,
            string instanceName,
            Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new FileNotFoundException("Prefab not found", prefabPath);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = instanceName;
            instance.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            return instance;
        }
    }
}
