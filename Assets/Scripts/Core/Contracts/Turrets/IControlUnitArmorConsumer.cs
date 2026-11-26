namespace TeamHJD.Game.Contracts
{
    /// <summary>Host 조립부가 캠페인 연구를 실제 CU 체력 원본에 전달합니다.</summary>
    public interface IControlUnitArmorConsumer
    {
        void ApplyCampaignArmor(bool researched);
    }
}
