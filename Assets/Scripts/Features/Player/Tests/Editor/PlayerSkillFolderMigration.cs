using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor에서 스킬과 테스트를 역할별 폴더로 옮기며 기존 GUID를 보존합니다.</summary>
public static class PlayerSkillFolderMigration
{
    private const string Root = "Assets/Scripts/Features/Player";
    // 구조 이동 후 구형 경로로 자동 정리되지 않도록 명시적인 메뉴 호출만 허용합니다.

    [MenuItem("Tools/Player/Organize Skill Scripts")]
    public static void Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += Migrate;
            return;
        }
        MoveGroup("Controllers", "PlayerSkillController");
        MoveGroup("UI", "PlayerSkillHud PlayerFieldSkillIcon PlayerSkillDescriptions PlayerSkillHudSceneBinding PlayerSkillCardView PlayerSkillSelectionPanelView PlayerSkillSlotView PlayerSkillTooltipView PlayerTurretRecoveryPromptView");
        MoveGroup("Common", "PlayerSkillBase PlayerSkillCharges PlayerSkillLoadout IPlayerFieldTarget PlayerFieldLineFactory PlayerTimedEffect");
        MoveGroup("Devices", "PlayerFieldDevice PlayerFieldDeviceRegistry PlayerFieldDeviceVisual");
        MoveGroup("Enemy", "PlayerFieldEnemyAdapter PlayerFieldEnemyAnimation PlayerFieldEnemyBehaviour PlayerFieldEnemyControlLease PlayerFieldEnemyScanner");
        MoveGroup("Skills/Bomb", "PlayerBomb PlayerBombExplosion PlayerBombAreaDamage PlayerTimeBombVisual");
        MoveGroup("Skills/Barrier", "PlayerBarrierSkill PlayerBarrierGeometry PlayerBarrierPlacementPreview PlayerBarrierPlacementRules PlayerBarrierEnemyBehaviour");
        MoveGroup("Skills/Beacon", "PlayerBeaconSkill PlayerBeaconEnemyBehaviour");
        MoveGroup("Skills/Turret", "PlayerTurretSkill PlayerTurretInventory TemporaryTurret TemporaryTurretHealth TemporaryTurretProjectile PlayerSupportTurretTargeting PlayerSupportTurretWeapon");

        string source = Root + "/Editor";
        if (AssetDatabase.IsValidFolder(source))
        {
            EnsureFolder(Root + "/Tests");
            string destination = Root + "/Tests/Editor";
            if (!AssetDatabase.IsValidFolder(destination)) Move(source, destination);
        }
    }

    private static void MoveGroup(string folder, string names)
    {
        foreach (string name in names.Split(' '))
        {
            string source = Root + "/PlayerSkills/" + name + ".cs";
            if (!File.Exists(source)) continue;
            string directory = Root + "/PlayerSkills/" + folder;
            EnsureFolder(directory);
            Move(source, directory + "/" + name + ".cs");
        }
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static void Move(string source, string destination)
    {
        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
    }
}
