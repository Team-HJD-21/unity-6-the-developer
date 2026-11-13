// 승인된 v6 스프라이트·폰트·표시 파라미터입니다. 런타임은 에셋이나 UI 계층을 재생성하지 않습니다.
using System;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "The Developer/UI/Combat HUD Style")]
public sealed class CombatHudStyle : ScriptableObject
{
    [Serializable] public struct SpriteEntry { public string Id; public Sprite Sprite; }
    public SpriteEntry[] Sprites;
    public TMP_FontAsset HeaderFont, LabelFont, BodyFont;
    public Color Text = new(.933f, .918f, .859f);
    public Color Accent = new(.839f, .749f, .486f);
    public Color Health = new(.839f, .365f, .314f);
    public Color Power = new(.839f, .706f, .373f);
    public Color Cancel = new(.831f, .482f, .408f);
    [Min(.01f)] public float SlideSeconds = .22f;
    [Min(.01f)] public float HoverSeconds = .12f;
    [Min(.01f)] public float EquipSeconds = .24f;
    [Min(0)] public float AutoHideSeconds = 2.5f;
    [Min(0)] public float AmmoHideSeconds = 1.8f;
    [Min(0)] public float PickerLeaveSeconds = .24f;
    public Sprite Get(string id)
    {
        if (Sprites != null) foreach (var entry in Sprites) if (entry.Id == id) return entry.Sprite;
        return null;
    }
}
