using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamHJD.Game.Presentation
{
    /// <summary>전투 HUD가 닫기에 사용한 ESC를 같은 프레임의 Core 메뉴가 다시 처리하지 않게 합니다.</summary>
    public static class UIEscapeInput
    {
        private static int _consumedFrame = -1;
        public static bool WasPressedThisFrame => _consumedFrame != Time.frameCount &&
            Keyboard.current?.escapeKey.wasPressedThisFrame == true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _consumedFrame = -1;

        public static bool TryConsume()
        {
            if (!WasPressedThisFrame) return false;
            _consumedFrame = Time.frameCount;
            return true;
        }
    }
}
