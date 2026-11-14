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
        if (skill == PlayerSkillId.DualWield) id = "skill_dual_wield";
        if (skill == PlayerSkillId.Portal) id = "skill_portal";
        Main.sprite = Style.Get(id); Main.enabled = Main.sprite != null;
        Base.sprite = Style.Get(skill == PlayerSkillId.DualWield ? "skill_dual_wield" : "skill_turret_base");
        Base.enabled = skill == PlayerSkillId.PlayerTurret || skill == PlayerSkillId.DualWield;
        if (skill == PlayerSkillId.DualWield)
        { Base.rectTransform.anchorMin = new Vector2(0, 0); Base.rectTransform.anchorMax = new Vector2(.8f, .65f); Base.rectTransform.offsetMin = Base.rectTransform.offsetMax = Vector2.zero; }
        else { Base.rectTransform.anchorMin = new Vector2(.14f, .04f); Base.rectTransform.anchorMax = new Vector2(.78f, .7f); Base.rectTransform.offsetMin = Base.rectTransform.offsetMax = Vector2.zero; }
        var rect = Main.rectTransform;
        rect.anchorMin = skill == PlayerSkillId.PlayerTurret ? new Vector2(.03f, .25f) : Vector2.zero;
        rect.anchorMax = skill == PlayerSkillId.PlayerTurret ? new Vector2(.97f, 1) : Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (skill == PlayerSkillId.DualWield) { rect.anchorMin = new Vector2(.2f, .35f); rect.anchorMax = Vector2.one; }
    }
}
