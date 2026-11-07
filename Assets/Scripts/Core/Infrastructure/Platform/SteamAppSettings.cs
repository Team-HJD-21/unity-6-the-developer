// 개발 실행과 실제 Steam SDK 실행을 명시적으로 선택하는 앱 설정입니다.
// 후보 AppID를 보관하되 Steam 선택 시 초기화 실패를 Fake로 대체하지 않습니다.
using UnityEngine;

namespace TeamHJD.Game.Infrastructure
{
    [CreateAssetMenu(menuName = "TeamHJD/Core/Steam App Settings")]
    public sealed class SteamAppSettings : ScriptableObject
    {
        public const string ResourceKey = "Core/SteamAppSettings";
        [SerializeField]
        private bool useSteam;
        [SerializeField]
        private uint appId = 4336820;
        public bool UseSteam => useSteam;
        public uint AppId => appId;
    }
}
