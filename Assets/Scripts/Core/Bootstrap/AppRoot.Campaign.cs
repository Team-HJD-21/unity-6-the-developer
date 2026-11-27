// 새 게임/이어하기가 사용하는 Host 전장 성장 슬롯을 App 수명에 보관합니다.
// 개인 성장은 이 슬롯과 별개이며 활성 Match 도중 다른 캠페인으로 바꾸지 않습니다.
using System;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine.SceneManagement;
namespace TeamHJD.Game.Bootstrap
{
    public sealed partial class AppRoot
    {
        private CheckpointSlot _campaignSlot = new CheckpointSlot("campaign-1");
        public CheckpointSlot CampaignSlot => _campaignSlot;
        public int? Stage1TokenBalance => Stage1Tokens?.IsLoaded == true ? Stage1Tokens.Balance : null;
        public bool HasStage1TokenStorageError => Stage1Tokens?.StorageError != null;
        public bool HasStage1CuArmor => Stage1Tokens?.HasCuArmor(CampaignSlot) == true;
        public bool CanBuyStage1CuArmor =>
            CanUseStage1TokenShop && !HasStage1TokenStorageError &&
            Stage1TokenBalance >= Stage1TokenProgress.CuArmorCost && !HasStage1CuArmor;
        public bool CanRetryStage1TokenStorage => CanUseStage1TokenShop && HasStage1TokenStorageError;

        private bool CanUseStage1TokenShop =>
            CurrentMatchId.IsEmpty && NetworkMatches?.Descriptor == null &&
            (NetworkSession == null || NetworkSession.State == NetworkConnectionState.Disconnected || NetworkSession.IsServer) &&
            (GameFlow != null
                ? GameFlow.Phase == GameFlowPhase.Spaceship && !GameFlow.IsTransitioning
                : SceneManager.GetActiveScene().path == DefaultHubScene);

        /// <summary>우주선 연구 화면이 호출합니다. 구매가 저장된 뒤에만 true를 반환합니다.</summary>
        public Task<bool> BuyStage1CuArmorAsync() => CanBuyStage1CuArmor
            ? Stage1Tokens.BuyCuArmorAsync(CampaignSlot, destroyCancellationToken)
            : Task.FromResult(false);

        /// <summary>우주선 상태창이 실패한 토큰 불러오기나 처치 보상 저장을 다시 요청합니다.</summary>
        public Task<bool> RetryStage1TokenStorageAsync() => CanRetryStage1TokenStorage
            ? Stage1Tokens.LoadAsync(destroyCancellationToken)
            : Task.FromResult(false);

        /// <summary>시작/허브 흐름이 새 캠페인을 고를 때 Mission 입장 전에 호출합니다. 기존 파일을 삭제하지 않습니다.</summary>
        public void SelectCampaign(CheckpointSlot slot)
        {
            ThrowIfUnavailable();
            if (slot.IsEmpty || _currentMatch != null) throw new InvalidOperationException("Choose a valid campaign before starting a Match.");
            _campaignSlot = slot;
        }
    }
}
