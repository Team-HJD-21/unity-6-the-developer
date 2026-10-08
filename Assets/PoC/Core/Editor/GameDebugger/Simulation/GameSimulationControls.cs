// Editor의 로컬 시간 배율·정지·Step을 제어하고 종료/reload 때 소유한 변경만 복원합니다.
using System;
using UnityEditor;
using UnityEngine;
namespace TeamHJD.Game.Editor
{
    [InitializeOnLoad]
    public static class GameSimulationControls
    {
        private static readonly TimeScaleLease _scale = new TimeScaleLease(() => Time.timeScale, value => Time.timeScale=value);
        private static bool _ownsPause, _originalPause;
        static GameSimulationControls()
        {
            EditorApplication.playModeStateChanged += state => { if (state==PlayModeStateChange.ExitingPlayMode) Restore(); };
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.quitting += Restore;
        }
        public static void SetScale(float value)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("시간 제어는 Play Mode 전용입니다.");
            _scale.Apply(value);
        }
        public static void Pause()
        {
            if (!EditorApplication.isPlaying) return;
            if (!_ownsPause) { _originalPause=EditorApplication.isPaused; _ownsPause=true; }
            EditorApplication.isPaused=true;
        }
        public static void Resume() { if (EditorApplication.isPlaying) EditorApplication.isPaused=false; }
        public static void Step()
        {
            if (!EditorApplication.isPlaying || Time.timeScale<=0) return;
            Pause();
            EditorApplication.Step();
        }
        public static void Restore()
        {
            _scale.Dispose();
            if (_ownsPause && EditorApplication.isPaused) EditorApplication.isPaused=_originalPause;
            _ownsPause=false;
        }
        public static void DrawToolbar()
        {
            using (new EditorGUI.DisabledGroupScope(!EditorApplication.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                foreach (float value in new[] { 0.25f, 0.5f, 1f, 2f, 4f })
                    if (GUILayout.Button(value+"×", GUILayout.Width(46))) SetScale(value);
                float next=EditorGUILayout.FloatField(Time.timeScale, GUILayout.Width(54));
                if (next!=Time.timeScale && !float.IsNaN(next) && !float.IsInfinity(next)) SetScale(Mathf.Clamp(next,0,8));
                if (GUILayout.Button(EditorApplication.isPaused ? "Resume" : "Pause"))
                { if (EditorApplication.isPaused) Resume(); else Pause(); }
                using (new EditorGUI.DisabledGroupScope(Time.timeScale<=0))
                    if (GUILayout.Button("Step")) Step();
                if (GUILayout.Button("Restore")) Restore();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField($"Local scale {Time.timeScale:F2} | fixed step {Time.fixedDeltaTime:F4}s | Pause ≠ network pause", EditorStyles.miniLabel);
        }
    }
}
