// D의 실제 터렛 생성 경로입니다. 생성 의존성과 저장 ID를 Awake 전에 지정합니다.
// 복원은 활성화 명령을 재실행하지 않고 Feature가 검증한 상태와 예약을 이어 적용합니다.
using System;
using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.FieldTurrets
{
    public static class FieldTurretFactory
    {
        /// <summary>Feature 생성자가 해석한 정의·전력 공급자를 Awake 전에 지정합니다. 공급자와 같은 Scene에 생성합니다.</summary>
        public static FieldTurretBase Create(
            FieldTurretBase prefab, FieldTurretInitializationData data, IFieldTurretPowerSource powerSource,
            Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var scene = powerSource is FieldTurretController owner ? owner.gameObject.scene :
                parent != null ? parent.gameObject.scene : SceneManager.GetActiveScene();
            return CreateCore(prefab, data, powerSource, position, rotation, parent, scene, default, null);
        }

        // 전체 Feature 복원 전용입니다. Scene writer가 예산/무기 상태까지 검증한 뒤 이 경로를 사용합니다.
        internal static FieldTurretBase CreateForCheckpoint(FieldTurretBase prefab, FieldTurretActorState state,
            FieldTurretController owner, FieldTurretUpgradeDefinition upgrade, Transform parent = null)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            prefab.ValidateActorState(state, upgrade);
            CheckpointActorFactory.ValidatePrefab(prefab.gameObject, state.EntityId);
            var runtime = state.Runtime;
            var data = new FieldTurretInitializationData(prefab.Definition, runtime.CurrentHealth, runtime.IsLocked,
                false, runtime.DamageBonus, upgrade, runtime.UpgradeLevel);
            var created = CreateCore(prefab, data, owner, state.Pose.Position, state.Pose.Rotation, parent,
                owner.gameObject.scene, state.EntityId, state.Pose);
            try { created.RestoreActorState(state, upgrade); return created; }
            catch { created.DetachCheckpointReservation(); CheckpointActorFactory.Retire(created.gameObject); throw; }
        }

        private static FieldTurretBase CreateCore(FieldTurretBase prefab, FieldTurretInitializationData data,
            IFieldTurretPowerSource powerSource, Vector3 position, Quaternion rotation, Transform parent,
            Scene ownerScene, EntityId identity, CheckpointTransformState? pose)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Validate();
            if (prefab.Definition != data.Definition)
                throw new ArgumentException("Initialization definition must match the selected prefab.", nameof(data));
            if (powerSource == null || powerSource is UnityEngine.Object source && source == null)
                throw new ArgumentNullException(nameof(powerSource));
            if (parent != null && !parent.gameObject.activeInHierarchy)
                throw new ArgumentException("Creation requires an active parent.", nameof(parent));
            if (!ownerScene.IsValid() || !ownerScene.isLoaded || (parent != null && parent.gameObject.scene != ownerScene))
                throw new ArgumentException("Creation requires a loaded owner Scene and a parent in the same Scene.", nameof(parent));
            if (!prefab.enabled)
                throw new ArgumentException("Creation requires an enabled turret component.", nameof(prefab));

            var staging = new GameObject("Turret Initialization Staging");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, ownerScene);
            GameObject created = null;
            try
            {
                created = UnityEngine.Object.Instantiate(prefab.gameObject, position, rotation, staging.transform);
                created.SetActive(false);
                FieldTurretBase turret = created.GetComponent<FieldTurretBase>();
                if (!identity.IsEmpty)
                    (created.GetComponent<SceneEntityIdentity>() ?? created.AddComponent<SceneEntityIdentity>()).Initialize(identity);
                turret.PrepareInitialization(data, powerSource);
                created.transform.SetParent(parent, !pose.HasValue);
                if (pose.HasValue) pose.Value.Apply(created.transform);
                created.SetActive(true);
                // Concrete turret Awake must enter the common ConfigureSceneActivation path.
                if (!turret.IsInitialized || !turret.isActiveAndEnabled ||
                    !FieldTurretInstanceRegistry.TryGet(turret.InstanceId, out FieldTurretBase registered) || registered != turret)
                    throw new InvalidOperationException("Turret did not complete initialization and registration.");
                return turret;
            }
            catch
            {
                if (created != null)
                {
                    created.SetActive(false);
                    UnityEngine.Object.Destroy(created);
                }
                throw;
            }
            finally
            {
                UnityEngine.Object.Destroy(staging);
            }
        }
    }
}
