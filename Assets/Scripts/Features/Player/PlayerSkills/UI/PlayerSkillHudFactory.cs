using TMPro;
using UnityEngine;

/// <summary>플레이어에 스킬 HUD를 생성하고 표시용 에셋을 연결합니다.</summary>
public static class PlayerSkillHudFactory
{
    public static PlayerSkillHud Create(PlayerSkillController owner, Sprite bomb, Sprite turret,
        TMP_FontAsset font, Sprite barrier, Sprite beacon, Sprite recoveryKey, PlayerSkillHud prefab)
    {
        if (owner == null) return null;
        if (prefab == null || !prefab.IsConfigured)
        {
            Debug.LogWarning("Player skill HUD needs a Canvas prefab with placement text references.", owner);
            return null;
        }

        var hud = Object.Instantiate(prefab, owner.transform, false);
        hud.name = prefab.name;
        hud.Bind(owner, bomb, turret, font, barrier, beacon, recoveryKey);
        return hud;
    }
}
