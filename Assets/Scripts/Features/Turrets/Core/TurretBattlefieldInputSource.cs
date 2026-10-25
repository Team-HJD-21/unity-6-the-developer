// 배치된 터렛의 위치 스냅샷을 Battlefield용 불변 입력으로 변환합니다.
// Transform이나 TurretBase 참조는 Match Runtime으로 전달하지 않습니다.

using System.Collections.Generic;
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using DomainEntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Turrets
{
    public sealed class TurretBattlefieldInputSource : MonoBehaviour, IBattlefieldTurretInputSource
    {
        private BattlefieldSpatialInput _lastPublishedInput;
        private ITurretEntityIdResolver _entityIds;
        private bool _dirty;

        public event Action LayoutChanged;

        public void SetEntityIdResolver(ITurretEntityIdResolver entityIds)
        {
            _entityIds = entityIds;
            _lastPublishedInput = null;
            _dirty = true;
        }

        public void RefreshLayout() => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            PublishIfChanged();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                TurretInstanceRegistry.SnapshotChanged += OnTurretSnapshotChanged;
            _lastPublishedInput = CaptureTurretLayout();
        }

        private void OnDisable()
        {
            TurretInstanceRegistry.SnapshotChanged -= OnTurretSnapshotChanged;
        }

        public BattlefieldSpatialInput CaptureTurretLayout()
        {
            var turrets = new List<TurretSpatialInput>();
            TurretBase[] sceneTurrets = FindObjectsByType<TurretBase>(FindObjectsSortMode.None);
            foreach (TurretBase turret in sceneTurrets)
            {
                if (turret == null || !turret.isActiveAndEnabled || turret.gameObject.scene != gameObject.scene)
                    continue;
                // Operational includes transient suspension (for example, overheat cooldown).
                // Battlefield membership follows placement, not firing availability.
                if (Application.isPlaying && turret.IsDestroyed)
                    continue;

                int instanceId = Application.isPlaying && turret.InstanceId > 0
                    ? turret.InstanceId
                    : turret.GetInstanceID();
                DomainEntityId id;
                if (Application.isPlaying && _entityIds != null)
                {
                    // 새 Feature 개체는 명시적으로 등록되기 전까지 Core 배치에 편입하지 않습니다.
                    if (!_entityIds.TryResolveEntityId(instanceId, out id)) continue;
                }
                else if (!turret.TryGetEntityId(out id))
                    id = new DomainEntityId(Application.isPlaying ? $"turret-{instanceId}" : $"preview-turret-{instanceId}");
                Vector3 position = turret.transform.position;
                turrets.Add(new TurretSpatialInput(id, new BattlefieldPoint(position.x, position.y)));
            }

            turrets.Sort(CompareTurrets);
            return new BattlefieldSpatialInput(turrets);
        }

        private void OnTurretSnapshotChanged(int instanceId)
        {
            // 승급은 같은 프레임에 해제/등록/ID 이전을 수행합니다. 중간 빈 배치를 발행하지 않습니다.
            if (_entityIds != null) { _dirty = true; return; }
            PublishIfChanged();
        }

        private void PublishIfChanged()
        {
            BattlefieldSpatialInput nextInput = CaptureTurretLayout();
            if (HasSameLayout(_lastPublishedInput, nextInput)) return;

            _lastPublishedInput = nextInput;
            LayoutChanged?.Invoke();
        }

        private static int CompareTurrets(TurretSpatialInput left, TurretSpatialInput right)
        {
            int result = StringComparer.Ordinal.Compare(left.EntityId.Value, right.EntityId.Value);
            if (result != 0) return result;
            result = left.Position.X.CompareTo(right.Position.X);
            return result != 0 ? result : left.Position.Y.CompareTo(right.Position.Y);
        }

        private static bool HasSameLayout(BattlefieldSpatialInput left, BattlefieldSpatialInput right)
        {
            if (left == null || left.Turrets.Count != right.Turrets.Count) return false;
            for (int index = 0; index < left.Turrets.Count; index++)
            {
                TurretSpatialInput previous = left.Turrets[index];
                TurretSpatialInput current = right.Turrets[index];
                if (!previous.EntityId.Equals(current.EntityId) || previous.Position != current.Position)
                    return false;
            }
            return true;
        }
    }
}
