// 현재 터렛 UI가 요청할 수 있는 의도를 구분합니다. 피해·실제 생성/삭제는 Feature 경로입니다.
namespace TeamHJD.Game.Contracts
{
    public enum TurretCommandKind
    {
        SetActivation, StartAutoActivation, UpgradeLevel, DowngradeLevel, ApplyUpgrade, DowngradeUpgrade
    }
}
