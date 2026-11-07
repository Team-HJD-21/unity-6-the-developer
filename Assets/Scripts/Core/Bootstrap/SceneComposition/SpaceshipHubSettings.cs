// 시작 우주선의 로컬 Player prefab·입장 표식·카메라 배율을 제작 데이터로 보관합니다.
// 실제 필드의 임시 우주선 방문은 기존 Player/카메라를 사용하며 이 설정으로 중복 생성하지 않습니다.
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    [CreateAssetMenu(menuName = "TeamHJD/Core/Spaceship Hub Settings")]
    public sealed class SpaceshipHubSettings : ScriptableObject
    {
        public const string ResourceKey = "Core/SpaceshipHubSettings";
        public GameObject playerPrefab;
        public string spawnMarker = "Capsule";
        [Min(.1f)]
        public float orthographicSize = 18;
        [Tooltip("Initial_Stage Main Camera와 동일한 표시 레이어입니다.")]
        public LayerMask cameraCullingMask = 4190711;
    }
}
