using System;
using System.IO;
using System.Linq;
using TeamHJD.Game.Debugging;
using TeamHJD.Game.Turrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Editor
{
    [InitializeOnLoad]
    internal static class TurretTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/TestScene/TurretTest.unity";
        private const string SourceScenePath = "Assets/Scenes/Main.unity";
        private const string RequestPath = "Temp/TurretTestSceneBuilder.request";
        private const string BackupDirectory = "Temp/TurretTestSceneBuilder/Backups";
        private const string ControlUnitPrefabPath = "Assets/Prefabs/Tower/ControlUnit.prefab";
        private const string HighFirepowerPath = "Assets/Scripts/Tower/TurretDefinitions/UpgradeDefinitions/Canon_HighFirepower.asset";
        private const string LowPowerPath = "Assets/Scripts/Tower/TurretDefinitions/UpgradeDefinitions/Canon_LowPower.asset";
        private const string LevelUpgradeCatalogPath = "Assets/Scripts/Tower/TurretDefinitions/TurretLevelUpgrade_Stage1.asset";

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
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[TurretTestSceneBuilder] Exit Play Mode before rebuilding the scene.");
                return;
            }

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
            Scene testScene = SceneManager.GetSceneByPath(ScenePath);
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("TurretTest scene was not found", ScenePath);

            if (!testScene.IsValid() || !testScene.isLoaded)
                testScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            if (testScene.isDirty)
            {
                Debug.LogWarning("[TurretTestSceneBuilder] Save or discard pending TurretTest scene changes before rebuilding.");
                return;
            }

            SceneManager.SetActiveScene(testScene);

            CreateCamera();
            CopyConfiguredAudioManager(testScene);

            foreach ((string path, string name, Vector3 position) in TurretPlacements)
            {
                InstantiatePrefab(testScene, path, name, position);
            }

            InstantiatePrefab(
                testScene,
                ControlUnitPrefabPath,
                "ControlUnit",
                new Vector3(0f, -6f, 0f));

            TurretTestController controller = testScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TurretTestController>(true))
                .FirstOrDefault();
            if (controller == null)
            {
                GameObject controllerObject = new("[Debug] Turret Test Controller");
                controller = controllerObject.AddComponent<TurretTestController>();
            }

            ConfigureController(controller);

            Directory.CreateDirectory(BackupDirectory);
            string backupPath = Path.Combine(
                BackupDirectory,
                $"TurretTest-{DateTime.Now:yyyyMMdd-HHmmss-fff}.unity");
            File.Copy(ScenePath, backupPath);

            EditorSceneManager.MarkSceneDirty(testScene);
            if (!EditorSceneManager.SaveScene(testScene, ScenePath))
                throw new IOException($"Failed to save {ScenePath}; backup: {backupPath}");

            Selection.activeGameObject = controller.gameObject;
            Debug.Log($"[TurretTestSceneBuilder] Preserved existing map and objects. Backup: {backupPath}");
        }

        private static void CreateCamera()
        {
            bool hasMainCamera = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Any(camera => camera.CompareTag("MainCamera"));
            if (hasMainCamera)
            {
                return;
            }

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
            bool hasAudioManager = testScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<AudioManager>(true))
                .Any();
            if (hasAudioManager)
                return;

            Scene sourceScene = SceneManager.GetSceneByPath(SourceScenePath);
            bool openedSourceScene = !sourceScene.IsValid() || !sourceScene.isLoaded;
            if (openedSourceScene)
                sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);

            try
            {
                AudioManager source = sourceScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<AudioManager>(true))
                    .FirstOrDefault();
                if (source == null)
                    throw new MissingReferenceException($"AudioManager was not found in {SourceScenePath}");

                SceneManager.SetActiveScene(testScene);
                GameObject audioObject = new("AudioManager");
                AudioManager destination = audioObject.AddComponent<AudioManager>();
                EditorUtility.CopySerialized(source, destination);
            }
            finally
            {
                if (openedSourceScene)
                    EditorSceneManager.CloseScene(sourceScene, true);
            }
        }

        private static void ConfigureController(TurretTestController controller)
        {
            SerializedObject serializedController = new(controller);
            serializedController.Update();

            SerializedProperty upgrades = serializedController.FindProperty("sampleUpgrades");
            if (upgrades != null && upgrades.arraySize == 0)
            {
                UnityEngine.Object highFirepower = AssetDatabase.LoadMainAssetAtPath(HighFirepowerPath);
                UnityEngine.Object lowPower = AssetDatabase.LoadMainAssetAtPath(LowPowerPath);
                if (highFirepower != null && lowPower != null)
                {
                    upgrades.arraySize = 2;
                    upgrades.GetArrayElementAtIndex(0).objectReferenceValue = highFirepower;
                    upgrades.GetArrayElementAtIndex(1).objectReferenceValue = lowPower;
                }
                else
                {
                    Debug.LogWarning("[TurretTestSceneBuilder] Sample upgrade assets were not found.");
                }
            }

            SerializedProperty catalog = serializedController.FindProperty("levelUpgradeCatalog");
            if (catalog != null && catalog.objectReferenceValue == null)
            {
                catalog.objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(LevelUpgradeCatalogPath);
                if (catalog.objectReferenceValue == null)
                    Debug.LogWarning("[TurretTestSceneBuilder] Level upgrade catalog was not found.");
            }

            serializedController.ApplyModifiedProperties();
        }

        private static GameObject InstantiatePrefab(
            Scene scene,
            string prefabPath,
            string instanceName,
            Vector3 position)
        {
            if (prefabPath == ControlUnitPrefabPath)
            {
                ControlUnitStatus existingUnit = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ControlUnitStatus>(true))
                    .FirstOrDefault(unit => unit.name == "ControlUnit");
                if (existingUnit != null)
                    return existingUnit.gameObject;
            }
            else
            {
                TurretBase existingTurret = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TurretBase>(true))
                    .FirstOrDefault(turret => string.Equals(
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(turret.gameObject),
                        prefabPath,
                        StringComparison.OrdinalIgnoreCase));
                if (existingTurret != null)
                    return existingTurret.gameObject;
            }

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
