using System;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    public static class FieldTurretFactory
    {
        /// <summary>Apply resolved initial state before Awake/registration can observe the new turret.</summary>
        public static FieldTurretBase Create(
            FieldTurretBase prefab, FieldTurretInitializationData data, IFieldTurretPowerSource powerSource,
            Vector3 position, Quaternion rotation, Transform parent = null)
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
            if (!prefab.enabled)
                throw new ArgumentException("Creation requires an enabled turret component.", nameof(prefab));

            var staging = new GameObject("Turret Initialization Staging");
            staging.SetActive(false);
            GameObject created = null;
            try
            {
                created = UnityEngine.Object.Instantiate(prefab.gameObject, position, rotation, staging.transform);
                created.SetActive(false);
                FieldTurretBase turret = created.GetComponent<FieldTurretBase>();
                turret.PrepareInitialization(data, powerSource);
                created.transform.SetParent(parent, true);
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
