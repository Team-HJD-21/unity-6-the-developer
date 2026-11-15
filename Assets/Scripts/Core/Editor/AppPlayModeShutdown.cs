// Editor 재생 종료 알림에서 App을 먼저 정리해 지연된 시작/요청이 종료 프레임에 실행되지 않게 합니다.
// 런타임 종료는 AppRoot.OnApplicationQuit/OnDestroy가 같은 idempotent 정리를 수행합니다.
// Editor용 임시 저장은 성장 쓰기를 마친 뒤 AppServices의 Store.Dispose에서 삭제합니다.
using System;
using TeamHJD.Game.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace TeamHJD.Game.Editor
{
    [InitializeOnLoad]
    public static class AppPlayModeShutdown
    {
        static AppPlayModeShutdown()
        {
            EditorApplication.playModeStateChanged -= OnStateChanged;
            EditorApplication.playModeStateChanged += OnStateChanged;
        }
        private static void OnStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // 과거 검수에서 남긴 Play 시작 씬 지정만 교체합니다. 현재 편집 씬은 저장/변경하지 않습니다.
                if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == "Assets/Scenes/Gameplay/Initial_Stage.unity")
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Gameplay/Stage_1.unity");
            }
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            foreach (AppRoot root in UnityEngine.Object.FindObjectsByType<AppRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                try { root.Dispose(); }
                catch (Exception error) { Debug.LogException(error, root); }
        }
    }
}
