// 기존 Game Debugger 안에서 World 생성 의도와 Match 시간 지연을 공통 Feature API로 요청합니다.
// Editor가 직접 적을 생성하거나 물리/지형의 유효성을 추측하지 않습니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class EncounterOriginDebugControls
    {
        [SerializeField] private bool _expanded;
        [SerializeField] private Vector2 _center, _size = new Vector2(10, 10);
        [SerializeField] private float _visualZ, _delay;
        [SerializeField] private SpawnDeliveryMode _delivery;
        private string _status;
        public void Draw(IEncounterOriginRequestTarget target, bool ready, string preset, int maximum)
        {
            _expanded = EditorGUILayout.Foldout(_expanded, "추가 생성 · World origin / Match 시간 지연", true);
            if (!_expanded) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("대기 중 요청", target == null ? "연결 없음" : target.PendingSpawnRequests.ToString());
                _center = EditorGUILayout.Vector2Field("World center XY", _center);
                _size = EditorGUILayout.Vector2Field("Width / Height (0 = point)", _size);
                _visualZ = EditorGUILayout.FloatField("Visual Z", _visualZ);
                _delivery = (SpawnDeliveryMode)EditorGUILayout.EnumPopup("Delivery", _delivery);
                _delay = EditorGUILayout.FloatField("Delay · Match seconds", _delay);
                EditorGUILayout.HelpBox("현재 Host의 Grid·전선·환경과 Enemy 위치 검사를 거칩니다. 지점 없는 생성도 공통 추적에 포함됩니다. GroundEmergence/AirDrop 연출은 아직 미지원입니다.", MessageType.None);
                using (new EditorGUI.DisabledScope(!ready || target == null))
                    if (GUILayout.Button(_delay > 0 ? "Schedule World origin request" : "Request World origin"))
                    {
                        try
                        {
                            if (float.IsNaN(_delay) || float.IsInfinity(_delay) || _delay < 0) throw new ArgumentException("Delay는 유한한 0 이상의 게임 시간이어야 합니다.");
                            var origin = EncounterSpawnOrigin.AtWorld(new SpawnArea(new BattlefieldPoint(_center.x, _center.y),
                                _size.x, _size.y, _visualZ), _delivery);
                            if (_delay > 0)
                                _status = target.TryScheduleOrigin(preset, maximum, EncounterRequestSource.DebugWindow, origin, Guid.NewGuid(), _delay, out var id)
                                    ? $"대기 등록: {id:N}. Timeline의 Scheduled/Cancelled와 실행 결과를 확인하세요." : "현재 Match/source에서 대기 요청을 승인하지 않았습니다.";
                            else
                            {
                                var result = target.RequestOrigin(preset, maximum, EncounterRequestSource.DebugWindow, origin, Guid.NewGuid());
                                _status = $"{result.Status} · {result.PlannedCount} planned / {result.SpawnedCount} actual · {result.Reason}";
                            }
                        }
                        catch (ArgumentException error) { _status = error.Message; }
                    }
                if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
            }
        }
    }
}
