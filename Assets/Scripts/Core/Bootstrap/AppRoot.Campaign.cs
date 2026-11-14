// 새 게임/이어하기가 사용하는 Host 전장 성장 슬롯을 App 수명에 보관합니다.
// 개인 성장은 이 슬롯과 별개이며 활성 Match 도중 다른 캠페인으로 바꾸지 않습니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Bootstrap
{
    public sealed partial class AppRoot
    {
        private CheckpointSlot _campaignSlot = new CheckpointSlot("campaign-1");
        public CheckpointSlot CampaignSlot => _campaignSlot;
        /// <summary>시작/허브 흐름이 새 캠페인을 고를 때 Mission 입장 전에 호출합니다. 기존 파일을 삭제하지 않습니다.</summary>
        public void SelectCampaign(CheckpointSlot slot)
        {
            ThrowIfUnavailable();
            if (slot.IsEmpty || _currentMatch != null) throw new InvalidOperationException("Choose a valid campaign before starting a Match.");
            _campaignSlot = slot;
        }
    }
}
