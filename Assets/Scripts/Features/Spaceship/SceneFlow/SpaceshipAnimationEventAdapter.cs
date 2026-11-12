using UnityEngine;

namespace PoC.Spaceship.Scripts.MutiScene
{
    public class SpaceshipAnimationEventAdapter : MonoBehaviour
    {
        private SpaceshipEnter _enter;
        
        void Awake()
        {
            _enter = GetComponentInChildren<SpaceshipEnter>();
        }

        public void DeliveryNearPlayerFlag(int active)
        {
            _enter.ActivateDoorCollider(active);
        }
    }
}