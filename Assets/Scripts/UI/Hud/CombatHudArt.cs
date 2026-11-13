// 실제 장비 원본을 표시합니다. 터렛만 기존 바닥/헤드 두 레이어를 조합하며 새 장비 그림을 만들지 않습니다.
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatHudArt : MonoBehaviour
{
    public CombatHudStyle Style;
    public Image Main, Base;
    private PlayerSkillId _shown = (PlayerSkillId)(-1);
    public void Show(PlayerSkillId skill)
    {
        if (_shown == skill || Style == null) return;
        _shown = skill;
        string id = skill switch
        {
            PlayerSkillId.Bomb => "skill_bomb", PlayerSkillId.PlayerTurret => "skill_turret_head",
            PlayerSkillId.EnergyBarrier => "skill_barrier", PlayerSkillId.LureBeacon => "skill_beacon", _ => ""
        };
        Main.sprite = Style.Get(id); Main.enabled = Main.sprite != null;
        Base.sprite = Style.Get("skill_turret_base"); Base.enabled = skill == PlayerSkillId.PlayerTurret;
        var rect = Main.rectTransform;
        rect.anchorMin = skill == PlayerSkillId.PlayerTurret ? new Vector2(.03f, .25f) : Vector2.zero;
        rect.anchorMax = skill == PlayerSkillId.PlayerTurret ? new Vector2(.97f, 1) : Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
