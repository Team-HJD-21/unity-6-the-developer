// Match composition이 Player actor를 생성할 위치와 협동용 슬롯 인덱스를 제공합니다.

using UnityEngine;

namespace TeamHJD.Game.Presentation.Scene
{
    public sealed class PlayerStart : MonoBehaviour
    {
        [SerializeField, Min(0)] private int slotIndex;

        public int SlotIndex => slotIndex;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;
    }
}
