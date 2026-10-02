// 등록된 작동 가능 터렛의 위치 스냅샷을 Battlefield용 불변 입력으로 변환합니다.
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

        public event Action LayoutChanged;

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
                // Edit Mode previews intended placement; the running Match only consumes operational Turrets.
                if (Application.isPlaying && !turret.IsOperational)
                    continue;

                int instanceId = Application.isPlaying && turret.InstanceId > 0
                    ? turret.InstanceId
                    : turret.GetInstanceID();
                var id = new DomainEntityId(Application.isPlaying
                    ? $"turret-{instanceId}"
                    : $"preview-turret-{instanceId}");
                Vector3 position = turret.transform.position;
                turrets.Add(new TurretSpatialInput(id, new BattlefieldPoint(position.x, position.y)));
            }

            turrets.Sort(CompareTurrets);
            return new BattlefieldSpatialInput(turrets);
        }

        private void OnTurretSnapshotChanged(int instanceId)
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
