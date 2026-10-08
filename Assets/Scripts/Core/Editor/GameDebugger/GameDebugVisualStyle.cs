// Editor 표·모니터·그래프가 사용하는 글꼴 계층과 의미 색상을 제공합니다.
// 기본 Editor 글꼴을 유지하고 Dark/Light 배경 모두에서 데이터가 식별되도록 합니다.
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class GameDebugVisualStyle
    {
        private static GUIStyle _number;
        private static bool _isDarkNumber;

        public static GUIStyle Number
        {
            get
            {
                if (_number == null || _isDarkNumber != EditorGUIUtility.isProSkin)
                {
                    _isDarkNumber = EditorGUIUtility.isProSkin;
                    _number = new GUIStyle(EditorStyles.boldLabel) { fontSize = 17, clipping = TextClipping.Clip };
                }
                return _number;
            }
        }

        public static Color Series(GameDebugMetric metric, bool isDark)
        {
            switch (metric)
            {
                case GameDebugMetric.Requests:
                    return isDark ? new Color(.90f, .69f, .30f) : new Color(.55f, .32f, .04f);
                case GameDebugMetric.Died:
                    return isDark ? new Color(.92f, .39f, .43f) : new Color(.65f, .13f, .18f);
                case GameDebugMetric.Removed:
                    return isDark ? new Color(.65f, .57f, .84f) : new Color(.40f, .24f, .65f);
                default:
                    return isDark ? new Color(.36f, .77f, .64f) : new Color(.04f, .38f, .27f);
            }
        }

        public static Color Frontline => EditorGUIUtility.isProSkin
            ? new Color(1f, .45f, .15f) : new Color(.65f, .28f, .05f);
        public static Color AllocatedPoint => EditorGUIUtility.isProSkin
            ? new Color(.30f, .60f, .85f) : new Color(.10f, .32f, .65f);
        public static Color UnallocatedPoint => EditorGUIUtility.isProSkin
            ? new Color(.75f, .75f, .75f) : new Color(.32f, .32f, .32f);
        public static Color Selection => EditorGUIUtility.isProSkin ? Color.white : new Color(.15f, .15f, .15f);
    }
}
