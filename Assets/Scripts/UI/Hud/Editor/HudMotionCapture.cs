// 검수자가 한 입력을 보내기 전에 켜는 읽기 전용 연속 캡처입니다. UI 함수/게임 시간은 변경하지 않습니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TeamHJD.Game.Presentation;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HudMotionCapture
{
    [Serializable] private sealed class Frame
    {
        public int frame;
        public float seconds, wheel, disk, tooltip, menuAlpha, reloadAngle;
        public bool dragging, reloading;
        public float[] cards, rotations;
        public float mainAlpha;
        public bool mainReady;
        public float[] introProgress;
        public float weaponNameAlpha, cursorPulse;
        public string weaponName;
        public bool aiming;
        public string clock, alert;
        public bool timeWarning;
        public float clockScale, alertAlpha;
        public float[] hpFill, hpTrail, hitX, hitY, keyAlpha, keyScale, pickupScale;
        public Vector2 qBadge, eBadge;
        public float alertBottom, alertTop;
        public bool alertOverSkills, alertOverTooltip, alertOverDisk;
        public int rounds;
        public float ammoPulse, ammoAlpha, ammoCountScale;
        public Vector2 ammoPosition;
    }
    [Serializable] private sealed class Recording
    { public string source = "Live Game View, normal time, downsampled to width 960"; public List<Frame> samples = new(); }
    private static Recording _recording;
    private static double _start, _next, _end, _interval;
    private static string _folder;
    static HudMotionCapture() => EditorApplication.playModeStateChanged += state =>
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Finish();
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            string label = SessionState.GetString("HUDv6.CaptureNextPlay", "");
            if (label.Length == 0) return;
            SessionState.EraseString("HUDv6.CaptureNextPlay"); Start(label, 4.8f, 20);
        }
    };
    public static string ArmNextPlay(string label)
    {
        if (!Regex.IsMatch(label, "^[a-zA-Z0-9_-]+$")) throw new ArgumentException("Use a validation label.");
        SessionState.SetString("HUDv6.CaptureNextPlay", label); return "Armed next Play, including initial rendered frames.";
    }

    public static string Start(string label, float seconds = 1, float fps = 20)
    {
        if (!EditorApplication.isPlaying || _recording != null) throw new InvalidOperationException("Use Play Mode with no active recording.");
        if (!Regex.IsMatch(label, "^[a-zA-Z0-9_-]+$")) throw new ArgumentException("Use a validation label.");
        _folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Local/Validation/HudV6/manual/motion", label));
        Directory.CreateDirectory(_folder); _recording = new Recording();
        _start = _next = EditorApplication.timeSinceStartup;
        _end = _start + Mathf.Clamp(seconds, .1f, 5); _interval = 1 / Mathf.Clamp(fps, 2, 30);
        EditorApplication.update += Capture;
        return "Recording live Game View: " + label;
    }
    private static void Capture()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now >= _end) { Finish(); return; }
        if (now < _next) return;
        _next = now + _interval;
        var type = typeof(Editor).Assembly.GetType("UnityEditor.PlayModeView");
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic;
        var view = type?.GetMethod("GetMainPlayModeView", flags | BindingFlags.Static)?.Invoke(null, null);
        var source = view != null ? type.GetField("m_TargetTexture", flags | BindingFlags.Instance)?.GetValue(view) as RenderTexture : null;
        if (source == null || !source.IsCreated()) return;
        var previous = RenderTexture.active;
        var small = RenderTexture.GetTemporary(960, Mathf.RoundToInt(960f * source.height / source.width), 0);
        Texture2D texture = null;
        try
        {
            if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(source, small, new Vector2(1, -1), new Vector2(0, 1));
            else Graphics.Blit(source, small);
            RenderTexture.active = small;
            texture = new Texture2D(small.width, small.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, small.width, small.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(_folder, $"{_recording.samples.Count:000}.png"), texture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(small); if (texture != null) UnityEngine.Object.DestroyImmediate(texture); }
        var picker = UnityEngine.Object.FindFirstObjectByType<CombatSkillPicker>();
        var main = UnityEngine.Object.FindFirstObjectByType<StartupControlsView>();
        var stack = UnityEngine.Object.FindFirstObjectByType<CombatWeaponStack>();
        var cursor = UnityEngine.Object.FindFirstObjectByType<CombatFieldCursor>();
        var indicators = UnityEngine.Object.FindFirstObjectByType<CombatPlayerIndicators>();
        var panel = UnityEngine.Object.FindFirstObjectByType<AppPanelView>();
        var header = UnityEngine.Object.FindFirstObjectByType<CombatHudHeader>();
        var alert = UnityEngine.Object.FindFirstObjectByType<AlertStripView>();
        var bars = UnityEngine.Object.FindObjectsByType<CombatVitalsGroup>(FindObjectsSortMode.None).OrderBy(v => v.RightEdge).ToArray();
        var keys = UnityEngine.Object.FindObjectsByType<CombatSkillSlotFeedback>(FindObjectsSortMode.None).OrderBy(v => v.name).ToArray();
        Rect alertBounds = alert != null ? ScreenBounds((RectTransform)alert.transform) : default;
        bool showingAlert = alert != null && alert.Group.alpha > .001f;
        _recording.samples.Add(new Frame { frame = Time.frameCount, seconds = (float)(now - _start),
            wheel = picker != null ? picker.Reveal : 0, disk = picker != null ? picker.DiskGroup.alpha : 0,
            tooltip = picker != null ? picker.TooltipGroup.alpha : 0, dragging = picker != null && picker.IsDragging,
            cards = stack != null ? stack.Groups.Select(g => g.alpha).ToArray() : null,
            rotations = stack != null ? stack.Slots.Select(s => s.transform.localEulerAngles.z).ToArray() : null,
            menuAlpha = panel != null ? panel.Group.alpha : 0,
            mainAlpha = main != null && main.Group != null ? main.Group.alpha : 0, mainReady = main != null && main.IsRevealed,
            weaponNameAlpha = stack != null && stack.NameGroup != null ? stack.NameGroup.alpha : 0,
            weaponName = stack != null && stack.NameLabel != null ? stack.NameLabel.text : null,
            aiming = cursor != null && cursor.IsAiming, cursorPulse = cursor != null && cursor.View != null ? cursor.View.Pulse : 0,
            clock = header != null ? header.ClockLabel.text : null, timeWarning = header != null && header.IsTimeWarning,
            clockScale = header != null ? header.ClockLabel.rectTransform.localScale.x : 1,
            alert = alert != null ? alert.Message.text : null, alertAlpha = alert != null ? alert.Group.alpha : 0,
            alertBottom = alertBounds.yMin, alertTop = alertBounds.yMax,
            alertOverSkills = showingAlert && picker != null &&
                ((picker.PickerGroup.alpha > .001f && alertBounds.Overlaps(ScreenBounds(picker.PickerRect))) ||
                 (picker.CompactGroup.alpha > .001f && alertBounds.Overlaps(ScreenBounds(picker.CompactRect)))),
            alertOverTooltip = showingAlert && picker != null && picker.TooltipGroup.alpha > .001f && alertBounds.Overlaps(ScreenBounds(picker.TooltipRect)),
            alertOverDisk = showingAlert && picker != null && picker.DiskGroup.alpha > .001f && alertBounds.Overlaps(ScreenBounds(picker.DiskBase.rectTransform)),
            rounds = indicators != null ? indicators.DisplayedRounds : -1,
            ammoPulse = indicators != null ? indicators.ShotPulse : 0,
            ammoAlpha = indicators != null ? indicators.AmmoGroup.alpha : 0,
            ammoCountScale = indicators != null && indicators.RoundCount != null ? indicators.RoundCount.transform.localScale.x : 1,
            ammoPosition = indicators != null ? indicators.AmmoRoot.anchoredPosition : Vector2.zero,
            hpFill = bars.Select(v => v.Primary.Fill.fillAmount).ToArray(), hpTrail = bars.Select(v => v.Primary.DamageTrail != null ? v.Primary.DamageTrail.fillAmount : 0).ToArray(),
            hitX = bars.Select(v => v.Slide.anchoredPosition.x).ToArray(), hitY = bars.Select(v => v.Slide.anchoredPosition.y).ToArray(),
            keyAlpha = keys.Select(v => v.Overlay.color.a).ToArray(), keyScale = keys.Select(v => v.transform.localScale.x).ToArray(),
            pickupScale = picker != null ? picker.Wedges.Select(w => w.Art != null ? w.Art.localScale.x : 1).ToArray() : null,
            qBadge = picker != null && picker.QKeyBadge != null ? picker.QKeyBadge.anchoredPosition : Vector2.zero,
            eBadge = picker != null && picker.EKeyBadge != null ? picker.EKeyBadge.anchoredPosition : Vector2.zero,
            introProgress = UnityEngine.Object.FindObjectsByType<CombatHudIntro>(FindObjectsSortMode.None).Select(i => i.Progress).ToArray(),
            reloading = indicators != null && indicators.ReloadVisible, reloadAngle = indicators != null ? indicators.ReloadArrows.localEulerAngles.z : 0 });
    }
    private static Rect ScreenBounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var canvas = rect.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var p in corners)
        { Vector2 s = RectTransformUtility.WorldToScreenPoint(camera, p); min = Vector2.Min(min, s); max = Vector2.Max(max, s); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    public static string Finish()
    {
        EditorApplication.update -= Capture;
        if (_recording == null) return "No recording.";
        File.WriteAllText(Path.Combine(_folder, "frames.json"), JsonUtility.ToJson(_recording, true));
        int count = _recording.samples.Count; _recording = null; return $"Saved {count} actual frames.";
    }
}
