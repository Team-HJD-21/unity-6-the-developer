// 개별 터렛의 냉각 기한과 생성 발사체를 Controller의 Match 시간에 연결합니다.
// 활성/과열/파괴의 원본 상태와 전력 계산은 기존 Feature에 유지합니다.
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    public abstract partial class FieldTurretBase
    {
        protected double FieldSeconds => _turretController?.FieldSeconds ?? Time.timeAsDouble;
        protected bool CanSimulateField => HasStateAuthority && (_turretController == null || _turretController.CanSimulate);

        protected FieldTurretController CheckpointOwner => _configuredPowerSource as FieldTurretController;
        /// <summary>무기가 산포를 계산할 때만 사용합니다. 독립 Feature 시험의 기존 Unity 난수는 유지합니다.</summary>
        protected float NextScatterOffset(float minimum, float maximum)
        {
            if (minimum == maximum) return minimum;
            var random = _turretController?.CombatRandom;
            return random != null ? random.NextFloat(minimum, maximum) : Random.Range(minimum, maximum);
        }

        /// <summary>Controller가 시계 연결/해제 때 호출해 남은 냉각 시간을 그대로 유지합니다.</summary>
        internal void RebaseSimulationTime(double previous, double current)
        {
            if (_coolingEndsAt > previous)
                _coolingEndsAt = current + System.Math.Max(0, _coolingEndsAt - previous);
            else
                _coolingEndsAt = current;
            OnSimulationTimeRebased(previous, current);
        }

        protected virtual void OnSimulationTimeRebased(double previous, double current) { }

        /// <summary>기존 무기가 Instantiate 직후 호출해 탄환의 물리·수명·종료를 같은 Match에 묶습니다.</summary>
        protected virtual void BindProjectileSimulation(GameObject projectile)
        {
            if (_turretController?.FieldTimeSource != null)
                FieldSimulationTime.BindSpawned(projectile, this);
        }
    }
}
