using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    /// <summary>CU interaction UI의 Button.onClick에 연결하는 진입점.</summary>
    public sealed class TurretAutoActivationButton : MonoBehaviour
    {
        [SerializeField] private TurretController controller;
        public bool LastRequestAccepted { get; private set; }

        public void ActivateAutomatically()
        {
            if (controller == null)
                controller = TurretController.GetOrCreateForScene(gameObject.scene);
            LastRequestAccepted = controller.TryStartAutoActivation();
        }
    }
}
