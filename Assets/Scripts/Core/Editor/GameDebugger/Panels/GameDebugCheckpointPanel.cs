// Game Debugger에서 실제 Host 저장 API와 필수 writer 연결 상태를 확인합니다.
// 자동 저장 조건이나 우회 캡처를 만들지 않고 Runtime과 같은 Save/Restore를 호출합니다.
using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    internal static class GameDebugCheckpointPanel
    {
        private static string _slot = "manual";
        private static bool _restoreCurrency;
        private static bool _pending;
        private static CheckpointOperationResult _result;
        private static CheckpointService _displayedService;
        private static string _error;
        private static Vector2 _scroll;
        private static bool _growthExpanded;

        public static void Draw(InitialStageCompositionRoot scene)
        {
            if (scene == null || scene.Checkpoints == null) return;
            var service = scene.Checkpoints;
            if (!ReferenceEquals(_displayedService, service))
            {
                _displayedService = service;
                _result = null;
                _error = null;
            }
            using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            EditorGUILayout.LabelField("CHECKPOINT · HOST", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("등록 원본 / 필수 누락", $"{service.ParticipantCount} / {service.MissingParticipantKeys.Count}");
            if (service.MissingParticipantKeys.Count > 0)
            {
                EditorGUILayout.HelpBox("필수 Feature writer가 연결되어야 전체 저장/복원을 실행합니다. Fixture나 일부 원본만 저장한 성공으로 바꾸지 않습니다.", MessageType.Warning);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Min(110, 22 * service.MissingParticipantKeys.Count)));
                foreach (string key in service.MissingParticipantKeys) EditorGUILayout.LabelField(key, EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
            }
            _growthExpanded = EditorGUILayout.Foldout(_growthExpanded, "개인 / Host 성장 저장 · " + scene.GrowthSaves.Count, true);
            if (_growthExpanded)
            {
                foreach (var growth in scene.GrowthSaves)
                {
                    using var growthBox = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(string.Join(" / ", growth.SourceKeys), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("요청 / 디스크 완료", growth.IssuedRevision + " / " + growth.SavedRevision);
                    EditorGUILayout.LabelField("상태", growth.LastError != null ? "실패" : growth.HasPendingChanges ? "저장 대기" : "동기화됨");
                    if (growth.LastError != null) EditorGUILayout.HelpBox(growth.LastError.Message, MessageType.Error);
                }
            }
            if (scene.Recovery != null)
            {
                using var recoveryBox = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                var ledger = scene.Recovery.Ledger;
                EditorGUILayout.LabelField("구조 / 안전장치", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("실제 Player 원본", scene.Recovery.RegisteredPlayers + " / " + scene.Recovery.RequiredPlayers);
                EditorGUILayout.LabelField("팀 구조 잔여 / 사용", ledger.RescueSeconds.ToString("F1") + "초 / " + ledger.SafetyUses + " / 3");
                EditorGUILayout.LabelField("실행", ledger.RunId.ToString("N"), EditorStyles.miniLabel);
                if (scene.RecoveryError != null) EditorGUILayout.HelpBox(scene.RecoveryError, MessageType.Error);
                using (new EditorGUI.DisabledScope(scene.IsRecovering || !scene.Recovery.IsRecoveryPending))
                    if (GUILayout.Button("같은 복귀 재시도 · 추가 차감 없음")) _ = scene.RetryRecoveryAsync();
            }
            _slot = EditorGUILayout.TextField("슬롯", _slot);
            if (scene.StageProgress != null)
            {
                var stage = scene.StageProgress;
                using var stageBox = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                EditorGUILayout.LabelField("STAGE · 실제 점유 / 사건", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("점유 / 최고 도달", stage.CaptureRatio.ToString("P1") + " / " + stage.HighestCaptureRatio.ToString("P1"));
                EditorGUILayout.LabelField("33% / 66% 생성", (stage.FirstFieldBoss.IsEmpty ? "미확인" : stage.FirstFieldBoss.Value) + " / " +
                    (stage.SecondFieldBoss.IsEmpty ? "미확인" : stage.SecondFieldBoss.Value));
                EditorGUILayout.LabelField("최종 진입 조건", stage.CanEnterFinalBattle ? "준비됨 · 실제 Boss Scene 연결 필요" : "미충족");
                if (stage.PendingReason != null) EditorGUILayout.HelpBox(stage.PendingReason, MessageType.Warning);
            }
            _restoreCurrency = EditorGUILayout.Toggle("재화도 체크포인트로 복원", _restoreCurrency);
            EditorGUILayout.LabelField("기본: 현재 경험치/재화 유지. 자동 실행 사건은 등록하지 않았습니다.", EditorStyles.wordWrappedMiniLabel);
            bool validSlot = TrySlot(out _);
            using (new EditorGUI.DisabledScope(_pending || service.IsBusy || !validSlot || service.MissingParticipantKeys.Count > 0))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("명시 저장")) Execute(scene, false);
                if (GUILayout.Button("명시 복원")) Execute(scene, true);
            }
            if (!validSlot) EditorGUILayout.HelpBox("슬롯은 영문·숫자·-·_를 사용합니다. 경로나 Windows 장치 이름은 허용하지 않습니다.", MessageType.Info);
            if (_pending) EditorGUILayout.LabelField("처리 중…", EditorStyles.boldLabel);
            if (_result != null) EditorGUILayout.HelpBox(_result.Status + " · " + _result.Reason,
                _result.IsSuccess ? MessageType.Info : MessageType.Warning);
            if (_error != null) EditorGUILayout.HelpBox(_error, MessageType.Error);
        }

        private static bool TrySlot(out CheckpointSlot slot)
        {
            try { slot = new CheckpointSlot(_slot); return true; }
            catch (ArgumentException) { slot = default; return false; }
        }

        private static async void Execute(InitialStageCompositionRoot scene, bool restore)
        {
            if (_pending || !TrySlot(out var slot) || scene == null || scene.Checkpoints == null) return;
            var service = scene.Checkpoints;
            var policy = new CheckpointRestorePolicy(_restoreCurrency ? CurrencyRestorePolicy.RestoreCheckpoint : CurrencyRestorePolicy.PreserveCurrent);
            _pending = true;
            _error = null;
            try
            {
                var result = restore ? await service.RestoreAsync(slot, policy) : await service.SaveAsync(slot);
                if (scene != null && ReferenceEquals(scene.Checkpoints, service)) _result = result;
            }
            catch (Exception error) { if (ReferenceEquals(_displayedService, service)) _error = error.Message; }
            finally { _pending = false; }
        }
    }
}
