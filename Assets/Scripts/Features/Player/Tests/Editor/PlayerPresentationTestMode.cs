using UnityEditor;
using UnityEngine;

// Existing authored-frame tests explicitly exercise the retained V2 mode;
// PlayerContinuousAimTests exercise the current Player prefab presentation.
internal static class PlayerPresentationTestMode
{
    internal static void UseAuthoredPoses(GameObject player)
    {
        var visual = new SerializedObject(player.GetComponent<PlayerWeaponVisual>());
        visual.FindProperty("_continuousAim").boolValue = false;
        visual.ApplyModifiedPropertiesWithoutUndo();
        var motion = new SerializedObject(player.GetComponent<PlayerSpriteMotion>());
        motion.FindProperty("_useSpaceManBody").boolValue = false;
        motion.ApplyModifiedPropertiesWithoutUndo();
    }
}
