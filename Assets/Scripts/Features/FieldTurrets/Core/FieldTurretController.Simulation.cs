// Core가 임대한 Match 시간을 터렛과 이후 생성/승급된 개체에 전달합니다.
// 전력·활성화 정책은 Controller에 남기고 시계 바인딩과 정리만 분리합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    public sealed partial class FieldTurretController : IFieldSimulationConsumer
    {
        private IMatchTime _fieldTime;
        private readonly Dictionary<FieldTurretBase, FieldSimulationActor> _simulationActors = new();
        public CancellationToken GameplayCancellation { get; private set; }
        public IMatchTime FieldTimeSource => _fieldTime;
        public double FieldSeconds => _fieldTime?.ElapsedSeconds ?? Time.timeAsDouble;
        public bool CanSimulate => !GameplayCancellation.IsCancellationRequested &&
            (_fieldTime == null || (_fieldTime.IsAvailable && !_fieldTime.Snapshot.IsPaused && _fieldTime.Snapshot.Rate > 0));

        /// <summary>Core Scene 조립부가 호출합니다. 반환값을 해제하면 등록 구독과 모든 기존 바인딩을 닫습니다.</summary>
        public IDisposable BindFieldSimulation(IMatchTime time, CancellationToken gameplay)
        {
            if (time == null) throw new ArgumentNullException(nameof(time));
            if (_fieldTime != null) throw new InvalidOperationException("Turret simulation is already bound.");
            double previous = FieldSeconds;
            _fieldTime = time;
            GameplayCancellation = gameplay;
            _nextAutoActivationTime = FieldSeconds + Math.Max(0, _nextAutoActivationTime - previous);
            try
            {
                foreach (var turret in FieldTurretInstanceRegistry.RegisteredInstances.Values)
                    if (OwnsTurret(turret)) BindSimulationActor(turret, previous);
                FieldTurretInstanceRegistry.Registered += OnSimulationActorRegistered;
                FieldTurretInstanceRegistry.Unregistered += OnSimulationActorUnregistered;
                return new SimulationLease(this, time);
            }
            catch
            {
                ReleaseSimulation(time);
                throw;
            }
        }

        private void BindSimulationActor(FieldTurretBase turret, double previous)
        {
            if (_simulationActors.ContainsKey(turret)) return;
            turret.RebaseSimulationTime(previous, FieldSeconds);
            _simulationActors.Add(turret, FieldSimulationActor.Attach(turret.gameObject, _fieldTime, GameplayCancellation));
        }

        private void OnSimulationActorRegistered(FieldTurretBase turret)
        {
            if (_fieldTime != null && OwnsTurret(turret)) BindSimulationActor(turret, FieldSeconds);
        }

        private void OnSimulationActorUnregistered(FieldTurretBase turret)
        {
            if (!_simulationActors.TryGetValue(turret, out var actor)) return;
            _simulationActors.Remove(turret);
            if (actor != null) actor.Unbind(_fieldTime);
        }

        private void ReleaseSimulation(IMatchTime time)
        {
            if (!ReferenceEquals(_fieldTime, time)) return;
            double previous = FieldSeconds;
            FieldTurretInstanceRegistry.Registered -= OnSimulationActorRegistered;
            FieldTurretInstanceRegistry.Unregistered -= OnSimulationActorUnregistered;
            _fieldTime = null;
            GameplayCancellation = default;
            _nextAutoActivationTime = FieldSeconds + Math.Max(0, _nextAutoActivationTime - previous);
            foreach (var pair in _simulationActors)
            {
                if (pair.Key == null) continue;
                pair.Key.RebaseSimulationTime(previous, FieldSeconds);
                if (pair.Value != null) pair.Value.Unbind(time);
            }
            _simulationActors.Clear();
        }

        private sealed class SimulationLease : IDisposable
        {
            private FieldTurretController _owner;
            private readonly IMatchTime _time;
            public SimulationLease(FieldTurretController owner, IMatchTime time) { _owner = owner; _time = time; }
            public void Dispose()
            {
                var owner = _owner;
                _owner = null;
                if (owner != null) owner.ReleaseSimulation(_time);
            }
        }
    }
}
