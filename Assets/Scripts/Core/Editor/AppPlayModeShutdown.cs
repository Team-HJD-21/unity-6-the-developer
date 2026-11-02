// Editor 재생 종료 알림에서 App을 먼저 정리해 지연된 시작/요청이 종료 프레임에 실행되지 않게 합니다.
// 런타임 종료는 AppRoot.OnApplicationQuit/OnDestroy가 같은 idempotent 정리를 수행합니다.
using System;
using TeamHJD.Game.Bootstrap;
using UnityEditor;
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
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            foreach (AppRoot root in UnityEngine.Object.FindObjectsByType<AppRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                try { root.Dispose(); }
                catch (Exception error) { Debug.LogException(error, root); }
        }
    }
}
