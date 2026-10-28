using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    /// <summary>CU interaction UI의 Button.onClick에 연결하는 진입점.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TeamHJD.Game.Turrets", sourceAssembly: "TeamHJD.Game.Turrets", sourceClassName: "TurretAutoActivationButton")]
    public sealed class FieldTurretAutoActivationButton : MonoBehaviour
    {
        [SerializeField] private FieldTurretController controller;
        public bool LastRequestAccepted { get; private set; }

        public void ActivateAutomatically()
        {
            if (controller == null)
                controller = FieldTurretController.GetOrCreateForScene(gameObject.scene);
            LastRequestAccepted = controller.TryStartAutoActivation();
        }
    }
}
