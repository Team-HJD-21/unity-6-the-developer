// 긴 논리 ID를 Scene/진단 라벨에서만 짧게 표시합니다.
// 원본 ID·요청·Capture 데이터·복사 내용은 변경하지 않습니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Editor
{
    public static class EntityLabelFormatter
    {
        public static string Format(EntityId entityId)
        {
            string value = entityId.Value;
            if (string.IsNullOrEmpty(value)) return "—";
            const string turretPrefix = "turret/";
            if (value.StartsWith(turretPrefix, StringComparison.Ordinal) && value.Length > turretPrefix.Length + 8)
                return "T:" + value.Substring(turretPrefix.Length, 8);
            return value.Length <= 24 ? value : value.Substring(0, 12) + "…" + value.Substring(value.Length - 8);
        }
    }
}
