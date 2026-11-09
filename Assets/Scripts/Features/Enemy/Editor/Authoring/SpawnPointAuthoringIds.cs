// Scene에서 추가/복제한 SpawnPoint의 authoring ID를 자동으로 구분합니다.
// 기존 지점의 ID를 우선 보존하고 수정은 Undo 및 Scene 저장에 포함합니다.
// Play Mode/Prefab asset은 변경하지 않으며 런타임 복제는 Registry가 처리합니다.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬의 SpawnPoint ID를 검사하고 제작 중 중복된 ID를 정리한다.
/// </summary>
[InitializeOnLoad]
public static class SpawnPointAuthoringIds
{
    private static readonly HashSet<SpawnPoint> KnownPoints = new();
    private static bool _pending;

    static SpawnPointAuthoringIds()
    {
        EditorApplication.hierarchyChanged += Schedule;
        EditorSceneManager.sceneOpened += (_, __) => Schedule();
        Schedule();
    }

    private static void Schedule()
    {
        if (_pending) return;
        _pending = true;
        EditorApplication.delayCall += NormalizeOpenScenes;
    }

    /// <summary>
    /// 에디터가 안정된 시점에 열린 씬의 제작 ID를 검사하고 사라진 지점 참조를 정리한다.
    /// </summary>
    private static void NormalizeOpenScenes()
    {
        _pending = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene)) NormalizeScene(scene);
        }
        KnownPoints.RemoveWhere(point => point == null);
    }

    /// <summary>
    /// 씬의 SpawnPoint 제작 ID를 정규화하고 변경한 수를 반환한다.
    /// </summary>
    public static int NormalizeScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.IsPreviewScene(scene)) return 0;
        var points = new List<SpawnPoint>();
        foreach (GameObject root in scene.GetRootGameObjects())
            points.AddRange(root.GetComponentsInChildren<SpawnPoint>(true));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int changed = 0;
        // 복제 이전부터 관측한 객체를 먼저 처리해 원본 ID가 복제본 때문에 바뀌지 않게 합니다.
        foreach (SpawnPoint point in points)
            if (KnownPoints.Contains(point)) changed += Normalize(point, ids);
        foreach (SpawnPoint point in points)
            if (!KnownPoints.Contains(point)) changed += Normalize(point, ids);
        foreach (SpawnPoint point in points) KnownPoints.Add(point);
        if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);
        return changed;
    }

    /// <summary>
    /// 기존 고유 ID는 유지하고 비어 있거나 중복된 ID만 Undo 가능한 변경으로 교체한다.
    /// </summary>
    private static int Normalize(SpawnPoint point, HashSet<string> ids)
    {
        var serialized = new SerializedObject(point);
        SerializedProperty property = serialized.FindProperty("spawnPointId");
        if (!string.IsNullOrWhiteSpace(property.stringValue) && ids.Add(property.stringValue)) return 0;
        Undo.RecordObject(point, "Assign unique SpawnPoint ID");
        string id;
        do { id = "SpawnPoint-" + Guid.NewGuid().ToString("N"); } while (!ids.Add(id));
        property.stringValue = id;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(point);
        return 1;
    }
}
