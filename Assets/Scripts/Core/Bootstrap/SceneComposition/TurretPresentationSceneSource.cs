// Host의 D API에서 UI용 전체 투영을 만들며 전력·열·강화 규칙은 재구현하지 않습니다.
// 제한된 주기로 조회하고 의미 있는 값이 바뀔 때만 새 revision을 발행합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Turrets;
using UnityEngine;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class TurretPresentationSceneSource : ITurretPresentationSource, IDisposable
    {
        private readonly MatchId _matchId;
        private readonly Guid _descriptorId;
        private readonly TurretMatchBinding _binding;
        private readonly TurretController _controller;
        private readonly Transform _controlUnit;
        private readonly IControlUnitVitals _controlVitals;
        private readonly MonoBehaviour _controlVitalsBehaviour;
        private readonly Func<bool> _isAuthority;
        private bool _disposed;
        private long _confirmedCommandSequence;
        private long _revision;
        public TurretPresentationFrame Snapshot { get; private set; }
        public event Action<TurretPresentationFrame> Changed;
        public TurretPresentationSceneSource(MatchId matchId, Guid descriptorId, TurretMatchBinding binding,
            Transform controlUnit, Func<bool> isAuthority)
        {
            if (matchId.IsEmpty || descriptorId == Guid.Empty) throw new ArgumentException("Provide a Match preparation.");
            _binding = binding != null ? binding : throw new ArgumentNullException(nameof(binding));
            _controller = binding.Controller != null ? binding.Controller : throw new ArgumentException("Provide a D controller.", nameof(binding));
            _controlUnit = controlUnit != null ? controlUnit : throw new ArgumentNullException(nameof(controlUnit));
            _isAuthority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
            var sources = controlUnit.GetComponents<MonoBehaviour>().OfType<IControlUnitVitals>().ToArray();
            if (sources.Length != 1) throw new ArgumentException("Provide one CU vitals source.", nameof(controlUnit));
            _controlVitals = sources[0]; _controlVitalsBehaviour = (MonoBehaviour)_controlVitals;
            _matchId = matchId; _descriptorId = descriptorId;
        }
        public void Refresh()
        {
            if (_disposed) return;
            if (!_isAuthority() || _binding == null || _binding.BoundMatchId != _matchId || _controller == null || !_controller.HasWriteAuthority || _controlUnit == null)
            { Clear(); return; }
            var power = _controller.GetSnapshot(); Vector3 cu = _controlUnit.position;
            var controller = new TurretControllerPresentationSnapshot(power.MaximumPower, power.AvailablePower, power.ReservedPower,
                power.PendingRecoveryPower, _controlVitals.CurrentHealth, _controlVitals.MaxHealth, new BattlefieldPoint(cu.x, cu.y),
                _controller.IsAutoActivationRunning, _controller.AutoActivationCooldownRemaining,
                _controller.Regions?.Snapshot.Mode ?? RegionPolicyMode.Static,
                _controlVitalsBehaviour != null && _controlVitalsBehaviour.isActiveAndEnabled);
            var rows = new List<TurretPresentationSnapshot>();
            foreach (var registration in _binding.Registrations)
            {
                if (!TurretInstanceRegistry.TryGet(registration.Handle, out var turret) || !_controller.OwnsTurret(turret) || turret.Definition == null) continue;
                var flags = TurretPresentationFlags.None;
                if (turret.IsInitialized) flags |= TurretPresentationFlags.Initialized;
                if (turret.IsActivated) flags |= TurretPresentationFlags.Activated;
                if (turret.IsOperational) flags |= TurretPresentationFlags.Operational;
                if (turret.IsDestroyed) flags |= TurretPresentationFlags.Destroyed;
                if (turret.IsLocked) flags |= TurretPresentationFlags.Locked;
                if (turret.IsManualOperationBlocked) flags |= TurretPresentationFlags.ManualOperationBlocked;
                Vector3 position = turret.transform.position;
                string displayName = string.IsNullOrWhiteSpace(turret.DisplayName) ? turret.Definition.Id : turret.DisplayName;
                if (displayName.Length > 256) displayName = displayName.Substring(0, 255) + "…";
                rows.Add(new TurretPresentationSnapshot(registration.EntityId, new DefinitionId(turret.Definition.Id), displayName,
                    new BattlefieldPoint(position.x, position.y), turret.Definition.Level, turret.CurrentHealth, turret.MaxHealth,
                    turret.EffectiveDamage, turret.EffectivePower, turret.EffectiveRange, turret.CoolingDownRemaining,
                    new DefinitionId(turret.RuntimeState.SelectedUpgradeId), turret.RuntimeState.UpgradeLevel, flags));
            }
            var frame = new TurretPresentationFrame(_matchId, _descriptorId, checked(_revision + 1), controller, rows, _confirmedCommandSequence);
            if (frame.HasSameContent(Snapshot)) return;
            Snapshot = frame; _revision = frame.Revision; Changed?.Invoke(frame);
        }
        public void ObserveCommand(TurretCommandResult result)
        {
            if (_disposed || result == null || result.MatchId != _matchId ||
                (!result.WasAccepted && result.Status != TurretCommandStatus.ExecutionFailed)) return;
            _confirmedCommandSequence = Math.Max(_confirmedCommandSequence, result.ExecutionSequence);
            Refresh();
        }
        private void Clear() { if (Snapshot == null) return; Snapshot = null; Changed?.Invoke(null); }
        public void Dispose() { if (_disposed) return; _disposed = true; try { Clear(); } finally { Changed = null; } }
    }
}
