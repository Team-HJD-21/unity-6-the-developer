// 현재 Scene의 실제 생성기를 명시적인 Encounter 설정에 연결합니다.
// Additive 로드 중 아직 바뀌지 않은 Active Scene에서 생성기를 찾는 오류를 방지합니다.
// Inspector/Menu 작업은 dirty 표시만 하고 CLI 전용 진입점에서만 Scene을 저장합니다.
using System;
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EncounterSceneSetup
{
    [MenuItem("Tools/TeamHJD/The Developer/Configure Encounter Scene")]
    public static void InstallOnActiveScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Configure Encounter outside Play Mode.");
        Scene scene = SceneManager.GetActiveScene();
        var behaviours = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
        var roots = behaviours.OfType<InitialStageCompositionRoot>().Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
        var executors = behaviours.OfType<EnemySpawnExecutor>().Where(item => item.enabled && item.gameObject.activeInHierarchy).ToArray();
        if (roots.Length != 1 || executors.Length == 0)
            throw new InvalidOperationException("The Scene requires one Core root and at least one enabled Enemy executor.");
        var configuration = roots[0].GetComponent<EncounterSceneConfiguration>();
        var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<EnemySpawnExecutor>() : null;
        if (selected == null && configuration != null) selected = configuration.SpawnExecutor;
        if (selected == null && executors.Length == 1) selected = executors[0];
        if (selected == null || !executors.Contains(selected))
            throw new InvalidOperationException("Select the Scene executor to use. Multiple authored executors are not selected by discovery order.");
        if (configuration == null) configuration = Undo.AddComponent<EncounterSceneConfiguration>(roots[0].gameObject);
        var serialized = new SerializedObject(configuration);
        serialized.FindProperty("spawnExecutor").objectReferenceValue = selected;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    /// <summary>CLI 또는 의도적인 제작 도구가 호출합니다. 기존 SpawnPoint·적 정의·배치는 변경하지 않습니다.</summary>
    public static void ConfigureInitialStage()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity", OpenSceneMode.Single);
        // 현행 Enemy 팀의 제작 Root를 사용합니다. 이전 Debug Spawner는 제거하거나 비활성화하지 않습니다.
        var production = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemySpawnExecutor>(true))
            .Where(item => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject) == "Assets/Prefabs/Enemies/Spawning/EnemySpawnRoot.prefab").ToArray();
        if (production.Length != 1) throw new InvalidOperationException("Initial_Stage requires its authored production EnemySpawnRoot instance.");
        Selection.activeGameObject = production[0].gameObject;
        InstallOnActiveScene();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The Encounter Scene could not be saved.");
        Debug.Log("ENCOUNTER_SCENE_CONFIGURED");
    }
}
