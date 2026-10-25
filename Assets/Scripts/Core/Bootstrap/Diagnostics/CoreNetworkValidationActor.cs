// 두 실행 파일 검사의 Host 측 조작 상태 fixture입니다. 정식 Player 생성/복제를 대체하지 않습니다.
// 명시적 개발 검사에서만 생성하며 서버의 실제 Transform을 생산용 등록 API에 제공합니다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class CoreNetworkValidationActor : MonoBehaviour, IPlayerInteractionActor
    {
        public bool TryCaptureInteraction(out PlayerInteractionSnapshot snapshot)
        {
            Vector3 position = transform.position;
            snapshot = new PlayerInteractionSnapshot(new BattlefieldPoint(position.x, position.y), true, true);
            return isActiveAndEnabled;
        }
    }
}
#endif
