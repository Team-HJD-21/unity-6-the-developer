// 표시 영역과 드롭/부채꼴 판정을 같은 기준 좌표로 계산합니다. CanvasScaler 배율은 호출 측에서 변환합니다.
using UnityEngine;

public static class CombatHudGeometry
{
    public const float Radius = 352f, InnerRadius = 164f;
    public static int Wedge(Vector2 fromBottomCenter)
    {
        float radius = fromBottomCenter.magnitude;
        if (fromBottomCenter.y < 0 || radius < InnerRadius || radius > Radius) return -1;
        float angle = Mathf.Atan2(fromBottomCenter.y, fromBottomCenter.x) * Mathf.Rad2Deg;
        return Mathf.Clamp(3 - Mathf.FloorToInt(angle / 45f), 0, 3);
    }
    public static int DropSlot(Vector2 fromBottomLeft, Vector2 canvasSize)
    {
        if (canvasSize.x <= 0 || canvasSize.y <= 0) return -1;
        Vector2 p = new(fromBottomLeft.x / canvasSize.x, fromBottomLeft.y / canvasSize.y);
        if (p.x < 0 || p.x > 1 || p.y < 1 - 648f / 1080f || p.y > 1 - 258f / 1080f) return -1;
        return p.x < .5f ? 0 : 1;
    }
    public static float SkillAngle(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => 150f, PlayerSkillId.PlayerTurret => 112.5f,
        PlayerSkillId.EnergyBarrier => 70f, PlayerSkillId.LureBeacon => 22.5f, _ => 90f
    };
    public static Vector2 Radial(float radius, float degrees) => new(
        radius * Mathf.Cos(degrees * Mathf.Deg2Rad), radius * Mathf.Sin(degrees * Mathf.Deg2Rad));
}
