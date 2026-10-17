// Game Debugger에서 지역 정책을 선택하고 확정 Grid와 임시 판정 자료를 구분해 표시합니다.
// 실제 변경은 Match 바인딩에 요청하며 Editor가 별도 지역 상태를 만들지 않습니다.
using System.Linq;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class RegionDebugControls
    {
        public static bool DrawCapturedCells = true;
        public static bool DrawInputTriangles;
        public static RegionSceneBinding FindBinding()
        {
            var bindings = UnityEngine.Object.FindObjectsByType<RegionSceneBinding>(FindObjectsSortMode.None)
                .Where(binding => binding.isActiveAndEnabled && binding.Source != null).ToArray();
            if (bindings.Length != 1) return null;
            return bindings[0];
        }

        public static void Draw()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("지역 Grid · 점령 원본", EditorStyles.boldLabel);
                RegionSceneBinding binding = FindBinding();
                if (binding == null)
                {
                    EditorGUILayout.HelpBox("지역 바인딩이 있는 Match를 시작하세요. 기본 정책은 Static입니다.", MessageType.Info);
                    if (!EditorApplication.isPlaying && GUILayout.Button("현재 Scene의 CU/터렛 지역 바인딩 연결"))
                        RegionSceneSetup.InstallOnActiveScene();
                    return;
                }
                RegionGridSnapshot snapshot = binding.Source.Snapshot;
                EditorGUILayout.LabelField("점령 셀", snapshot.CapturedCellCount + " / " + snapshot.CapturedCells.Count);
                EditorGUILayout.LabelField("Grid revision", snapshot.Revision.ToString());
                using (new EditorGUI.DisabledGroupScope(!binding.HasWriteAuthority))
                {
                    var requested = (RegionPolicyMode)EditorGUILayout.EnumPopup("지역 정책", snapshot.Mode);
                    if (requested != snapshot.Mode && ConfirmReset())
                        binding.ResetPolicy(requested, true);
                    if (GUILayout.Button("CU 지역 Grid 설정 다시 읽기 / Reset") && ConfirmReset())
                        binding.ResetPolicy(snapshot.Mode, true);
                    if (snapshot.Mode == RegionPolicyMode.Static)
                    {
                        int highest = 0;
                        for (int index = 0; index < snapshot.CapturedCells.Count; index++)
                            if (snapshot.CapturedCells[index]) highest = Mathf.Max(highest, snapshot.Definition.RegionIds[index]);
                        if (GUILayout.Button("다음 정적 구역 해금")) binding.TryUnlockThroughZone(highest + 1);
                    }
                }
                EditorGUILayout.HelpBox(snapshot.Mode == RegionPolicyMode.Dynamic
                    ? "터렛은 잠기지 않습니다. 활성 터렛으로 셀을 점령하고 OFF/파괴로 면적이 사라지면 점령만 회수합니다. 전력/냉각은 유지합니다."
                    : "기존 CU 거리 구역을 지역 Grid로 적용합니다. 아래 Battlefield Grid는 참가자 밀도용이며 지역 점령과 다른 데이터입니다.",
                    MessageType.None);
                DrawCapturedCells = EditorGUILayout.Toggle("Scene 점령 Grid", DrawCapturedCells);
                DrawInputTriangles = EditorGUILayout.Toggle("임시 판정 삼각형", DrawInputTriangles);
                if (snapshot.Mode == RegionPolicyMode.Dynamic && binding.LastEvaluation != null)
                    EditorGUILayout.LabelField("활성 정점 / 임시 삼각형", binding.LastEvaluation.Geometry.Vertices.Count +
                        " / " + binding.LastEvaluation.Geometry.Triangles.Count);
            }
        }

        private static bool ConfirmReset() => EditorUtility.DisplayDialog("지역 정책 / Grid 초기화",
            "점령 데이터를 초기화하고 새 Grid 세대로 계산합니다. Static으로 전환하면 구역 밖 터렛이 잠기고 꺼질 수 있습니다. 계속할까요?",
            "초기화 후 적용", "취소");
    }
}
