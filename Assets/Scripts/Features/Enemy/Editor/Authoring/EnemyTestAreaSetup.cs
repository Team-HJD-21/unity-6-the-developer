using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>EnemyTest의 기존 바닥 위에 출현 지점과 표적을 묶은 관찰 구역을 제작한다.</summary>
public static class EnemyTestAreaSetup
{
    [MenuItem("Tools/TeamHJD/The Developer/Configure Enemy Test Areas")]
    public static void InstallOnActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Tests/EnemyTest.unity")
            throw new InvalidOperationException("EnemyTest 씬의 Edit Mode에서 구성하세요.");
        EnemyDamageTestTarget template = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "EnemyTestAreas")
            {
                Selection.activeGameObject = root;
                return;
            }
            if (template == null) template = root.GetComponentInChildren<EnemyDamageTestTarget>();
        }
        if (template == null) throw new InvalidOperationException("복제할 기존 테스트 표적이 필요합니다.");

        string[] names = { "01_CloseCombat", "02_Support", "03_Movement" };
        float[] centers = { -50f, -10f, 30f };
        // 제작 전 모든 출현/표적 위치와 연결이 NavMesh 위인지 확인한다.
        foreach (float x in centers)
        {
            var start = new Vector3(x - 8f, -75f, 0f);
            var end = new Vector3(x + 8f, -75f, 0f);
            if (!NavMesh.SamplePosition(start, out var startHit, 0.25f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(end, out var endHit, 0.25f, NavMesh.AllAreas))
                throw new InvalidOperationException("시험 구역에 유효한 NavMesh가 없습니다. 바닥 베이크를 확인하세요.");
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("시험 출현 지점에서 표적까지 완전한 경로가 없습니다.");
        }

        var areaRoot = new GameObject("EnemyTestAreas");
        Undo.RegisterCreatedObjectUndo(areaRoot, "Create Enemy test areas");
        SceneManager.MoveGameObjectToScene(areaRoot, scene);
        for (int i = 0; i < names.Length; i++)
        {
            var areaObject = new GameObject(names[i]);
            Undo.RegisterCreatedObjectUndo(areaObject, "Create Enemy test area");
            areaObject.transform.SetParent(areaRoot.transform);
            areaObject.transform.position = new Vector3(centers[i], -75f, 0f);
            var marker = Undo.AddComponent<EnemyTestArea>(areaObject);
            var spawnObject = new GameObject("Spawn");
            Undo.RegisterCreatedObjectUndo(spawnObject, "Create test spawn point");
            spawnObject.transform.SetParent(areaObject.transform);
            spawnObject.transform.localPosition = Vector3.left * 8f;
            var point = Undo.AddComponent<SpawnPoint>(spawnObject);
            var pointData = new SerializedObject(point);
            pointData.FindProperty("spawnPointId").stringValue = "EnemyTest-" + names[i];
            pointData.FindProperty("spawnRadius").floatValue = 0f;
            pointData.ApplyModifiedProperties();

            var target = UnityEngine.Object.Instantiate(template, areaObject.transform);
            target.name = "Target";
            Undo.RegisterCreatedObjectUndo(target.gameObject, "Create test target");
            target.transform.localPosition = Vector3.right * 8f;
            var targetData = new SerializedObject(target);
            targetData.FindProperty("maxHealth").intValue = 1000;
            targetData.ApplyModifiedProperties();
            target.ResetHealth();
            var areaData = new SerializedObject(marker);
            areaData.FindProperty("spawnPoint").objectReferenceValue = point;
            areaData.FindProperty("initialTarget").objectReferenceValue = target.GetComponent<TargetableComponent>();
            areaData.ApplyModifiedProperties();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = areaRoot;
    }
}
