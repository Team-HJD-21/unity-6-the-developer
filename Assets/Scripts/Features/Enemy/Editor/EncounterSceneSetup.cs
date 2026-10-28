// Initial_Stage의 실행 대상을 제작 참조로 저장하는 일회성 Editor 구성 도구입니다.
// Runtime 이름 검색을 추가하지 않고 현재 EnemySpawnRoot prefab의 인스턴스를 명시적으로 연결합니다.
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class EncounterSceneSetup
{
    [MenuItem("Tools/The Developer/Setup/Configure Initial Stage Encounter")]
    public static void ConfigureInitialStage()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        var executors = Object.FindObjectsByType<EnemySpawnExecutor>(FindObjectsSortMode.None)
            .Where(item => item.gameObject.scene == scene &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item) == "Assets/Prefabs/Enemies/Spawning/EnemySpawnRoot.prefab").ToArray();
        if (executors.Length != 1) throw new System.InvalidOperationException("Select one authored EnemySpawnRoot executor.");
        var root = Object.FindObjectsByType<InitialStageCompositionRoot>(FindObjectsSortMode.None).Single(item => item.gameObject.scene == scene);
        var configuration = root.GetComponent<EncounterSceneConfiguration>();
        if (configuration == null) configuration = root.gameObject.AddComponent<EncounterSceneConfiguration>();
        var serialized = new SerializedObject(configuration);
        serialized.FindProperty("spawnExecutor").objectReferenceValue = executors[0];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new System.InvalidOperationException("The Encounter configuration was not saved.");
        Debug.Log("Initial_Stage Encounter uses its explicitly authored EnemySpawnRoot executor.");
    }
}
