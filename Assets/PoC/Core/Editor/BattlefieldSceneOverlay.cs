// Scene View에서 창과 동일한 레이어 설정을 조작하는 Editor Overlay입니다.
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace TeamHJD.Game.Editor
{
    [Overlay(typeof(SceneView), "Battlefield", true)]
    public sealed class BattlefieldSceneOverlay : Overlay
    {
        public override VisualElement CreatePanelContent() => new IMGUIContainer(DrawControls);

        private static void DrawControls()
        {
            EditorGUILayout.LabelField("Battlefield / " + BattlefieldDebugSession.Mode, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            BattlefieldDebugDisplaySettings display = BattlefieldDebugSession.Display;
            display.IsEnabled = EditorGUILayout.Toggle("Scene layers", display.IsEnabled);
            display.ShowGrid = EditorGUILayout.Toggle("Grid", display.ShowGrid);
            display.ShowTerritory = EditorGUILayout.Toggle("Territory", display.ShowTerritory);
            display.ShowFrontline = EditorGUILayout.Toggle("Frontline", display.ShowFrontline);
            display.ShowOccupancy = EditorGUILayout.Toggle("Occupancy", display.ShowOccupancy);
            if (EditorGUI.EndChangeCheck()) BattlefieldDebugSession.NotifyDisplayChanged();
            if (GUILayout.Button("Open Debug Window")) BattlefieldDebugWindow.Open();
        }
    }
}
