// 기존 문자열 기반 터렛 피드백 중 실제 요청 결과만 공통 Alert에 보냅니다.
// 진행 중/거리/냉각 등 지속 상태를 프레임마다 알림으로 재생하지 않습니다.
using TeamHJD.Game.Application;

internal static class StageTurretAlertFeedback
{
    public static bool IsPending(string message) => message == "Waiting for Host confirmation/state...";

    public static bool Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || IsPending(message)) return false;
        bool success = message == "Applied." || message == "Turret activated." || message == "Turret deactivated." ||
            message == "Level increased." || message == "Level decreased." || message == "Upgrade increased." ||
            message == "Upgrade decreased." || message == "Automatic activation started." || message == "Automatic activation accepted by Host.";
        bool unchanged = message == "Already in the requested state." || message == "Already in the requested upgrade state.";
        return GameAlerts.Show(Localize(message), success ? AlertTone.Success : unchanged ? AlertTone.Info : AlertTone.Error, message);
    }

    private static string Localize(string message) => message switch
    {
        "Applied." => "적용했습니다.",
        "Turret activated." => "터렛을 활성화했습니다.",
        "Turret deactivated." => "터렛을 비활성화했습니다.",
        "Level increased." => "터렛 레벨을 올렸습니다.",
        "Level decreased." => "터렛 레벨을 낮췄습니다.",
        "Upgrade increased." => "터렛을 강화했습니다.",
        "Upgrade decreased." => "터렛 강화를 낮췄습니다.",
        "Automatic activation started." or "Automatic activation accepted by Host." => "자동 활성화를 시작했습니다.",
        "Not enough available power." or "Host rejected: InsufficientPower" => "사용할 수 있는 전력이 부족합니다.",
        "Move closer to the target (Host validation)." => "대상에 더 가까이 이동하세요.",
        "Cannot interact while dead." => "쓰러진 상태에서는 조작할 수 없습니다.",
        "Player interaction is currently blocked." => "지금은 조작할 수 없습니다.",
        "Interaction target is unavailable." => "조작할 대상을 찾을 수 없습니다.",
        "This turret is destroyed." => "파괴된 터렛입니다.",
        "This turret is locked." => "아직 해금되지 않은 터렛입니다.",
        "Already in the requested state." or "Already in the requested upgrade state." => "이미 적용된 상태입니다.",
        "Request unavailable or already pending." => "요청을 보낼 수 없거나 이미 처리 중입니다.",
        _ => message
    };
}
