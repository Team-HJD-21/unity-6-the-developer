using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Map
{
    [CustomEditor(typeof(FieldPlayerOcclusion)), CanEditMultipleObjects]
    public sealed class FieldPlayerOcclusionEditor : UnityEditor.Editor
    {
        private static readonly string[] DefaultSheetGuids =
        {
            "f97fe04c2c897ac4fa0c1e463eb889d2", // Overworld_Trees
            "333803740730c404f81e6d6afdf392ed"  // Overworld_Objects
        };

        private Sprite[] _defaults;
        private bool _showDefaults;

        private void OnEnable() => EditorApplication.projectChanged += RefreshDefaults;
        private void OnDisable() => EditorApplication.projectChanged -= RefreshDefaults;

        private void RefreshDefaults()
        {
            _defaults = null;
            Repaint();
        }

        internal static Sprite[] LoadDefaultSprites()
        {
            var sprites = DefaultSheetGuids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<Sprite>()
                .Where(FieldPlayerOcclusion.IsDefaultOccluder)
                .ToArray();
            Array.Sort(sprites, (a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return sprites;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            _defaults ??= LoadDefaultSprites();
            _showDefaults = EditorGUILayout.Foldout(_showDefaults, $"Default Occluders ({_defaults.Length})", true);
            if (!_showDefaults) return;

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(true))
            {
                foreach (var sprite in _defaults)
                    EditorGUILayout.ObjectField(sprite, typeof(Sprite), false);
            }
        }
    }
}
