// 정식 Scene 연결 API를 Inspector에서 명시적으로 실행하는 제작·검증 UI입니다.
// 런타임 연결/Match 정책을 다시 구현하지 않고 조립부의 공개 API만 호출합니다.
using System;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    [CustomEditor(typeof(InitialStageNetworkBinding))]
    public sealed class InitialStageNetworkBindingEditor : UnityEditor.Editor
    {
        private string _result;
        public override bool RequiresConstantRepaint() => UnityEngine.Application.isPlaying;
        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(UnityEngine.Application.isPlaying)) DrawDefaultInspector();
            EditorGUILayout.HelpBox("Manual: 연결 → 현재 접속자 준비 → Host Match 시작. 싱글 진입은 같은 API를 자동 호출합니다.", MessageType.Info);
            var binding = (InitialStageNetworkBinding)target;
            if (!UnityEngine.Application.isPlaying || binding.Connection == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("네트워크 연결", binding.Connection.State.ToString());
            EditorGUILayout.LabelField("Match", binding.Matches?.Phase.ToString() ?? "미준비");
            EditorGUILayout.LabelField("참가자", binding.Matches?.Descriptor?.Participants.Count.ToString() ?? "—");
            EditorGUILayout.LabelField("Host 시작 가능", binding.Matches?.CanStart == true ? "모든 참가자 준비됨" : "아니요");
            bool canConnect = binding.Connection.State == NetworkConnectionState.Disconnected ||
                binding.Connection.State == NetworkConnectionState.Faulted;
            using (new EditorGUI.DisabledScope(!canConnect))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Host 연결")) Execute("Host 연결", binding.TryStartHost);
                if (GUILayout.Button("Client 연결")) Execute("Client 연결", binding.TryStartClient);
            }
            using (new EditorGUI.DisabledScope(!binding.Connection.IsServer))
            {
                if (GUILayout.Button("현재 접속자로 Match 준비")) Execute("Match 준비", binding.TryPrepareMatch);
                using (new EditorGUI.DisabledScope(binding.Matches?.CanStart != true))
                    if (GUILayout.Button("Host Match 시작")) Execute("Match 시작", binding.TryStartMatch);
                if (GUILayout.Button("Match 종료 · 연결 유지")) Execute("Match 종료", binding.TryEndMatch);
            }
            using (new EditorGUI.DisabledScope(canConnect || binding.Connection.State == NetworkConnectionState.Disposed))
                if (GUILayout.Button("연결 종료"))
                    Execute("연결 종료", () => { binding.ShutdownConnection(); return true; });
            if (!string.IsNullOrEmpty(_result)) EditorGUILayout.HelpBox(_result, MessageType.None);
            if (!string.IsNullOrEmpty(binding.LastFailure)) EditorGUILayout.HelpBox(binding.LastFailure, MessageType.Error);
        }
        private void Execute(string label, Func<bool> request)
        {
            try { _result = label + (request() ? ": 요청 수락" : ": 현재 상태에서 거부됨"); }
            catch (Exception error) { _result = label + ": " + error.Message; Debug.LogException(error, target); }
        }
    }
}
