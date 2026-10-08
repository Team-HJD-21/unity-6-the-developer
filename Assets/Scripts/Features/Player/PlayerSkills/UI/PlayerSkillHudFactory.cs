using TMPro;
using UnityEngine;

/// <summary>플레이어에 스킬 HUD를 생성하고 표시용 에셋을 연결합니다.</summary>
public static class PlayerSkillHudFactory
{
    public static PlayerSkillHud Create(PlayerSkillController owner, Sprite bomb, Sprite turret,
        TMP_FontAsset font, Sprite barrier, Sprite beacon, Sprite recoveryKey)
    {
        var hudObject = new GameObject("Player Skill HUD", typeof(RectTransform));
        hudObject.transform.SetParent(owner.transform, false);
        var hud = hudObject.AddComponent<PlayerSkillHud>();
        hud.Bind(owner, bomb, turret, font, barrier, beacon, recoveryKey);
        return hud;
    }
}