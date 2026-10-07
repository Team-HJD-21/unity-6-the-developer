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
            if (!BattlefieldDebugSession.Display.IsCaptureMode)
                BattlefieldDiagnosticPanels.DrawDisplayControls();
            else if (GUILayout.Button("Exit Capture mode"))
            {
                BattlefieldDebugSession.Display.IsCaptureMode = false;
                BattlefieldDebugSession.NotifyDisplayChanged();
            }
            var snapshot = BattlefieldDebugSession.Snapshot;
            EditorGUILayout.LabelField("Revision / occupied",
                $"{snapshot?.Revision ?? 0} / {BattlefieldDebugSession.Grid?.OccupiedCells.Count ?? 0}");
            if (GUILayout.Button("Open Debug Window")) BattlefieldDebugWindow.Open();
        }
    }
}
