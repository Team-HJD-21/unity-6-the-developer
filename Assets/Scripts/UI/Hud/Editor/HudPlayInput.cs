// Unity CLI에서 한 입력씩 직접 검수할 때 쓰는 Editor 도구입니다.
// UI/장착 메서드를 호출하지 않고 실제 InputSystem 이벤트를 다음 정상 프레임에 전달합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class HudPlayInput
{
    private static Mouse _mouse, _previousMouse;
    private static Keyboard _keyboard, _previousKeyboard;
    private static readonly HashSet<UnityEngine.InputSystem.Key> HeldKeys = new();
    private static readonly HashSet<Action> Pending = new();
    private static bool _left, _right, _middle;
    static HudPlayInput() => EditorApplication.playModeStateChanged += state =>
    { if (state == PlayModeStateChange.ExitingPlayMode) Stop(); };
    public static string Start()
    {
        RequirePlay();
        if (_mouse == null)
        {
            _previousMouse = Mouse.current; _previousKeyboard = Keyboard.current;
            _mouse = InputSystem.AddDevice<Mouse>("HUD manual review mouse");
            _keyboard = InputSystem.AddDevice<Keyboard>("HUD manual review keyboard");
        }
        _mouse.MakeCurrent(); _keyboard.MakeCurrent();
        return "Dedicated review devices ready; events use normal Game View frames.";
    }
    public static string Stop()
    {
        foreach (var callback in Pending) InputSystem.onBeforeUpdate -= callback;
        Pending.Clear();
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        _mouse = null; _keyboard = null;
        HeldKeys.Clear(); _left = _right = _middle = false;
        if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
        if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
        return "Review devices removed.";
    }
    public static string InFrames(Action input, int frames = 5)
    {
        RequirePlay(); if (input == null) throw new ArgumentNullException(nameof(input));
        frames = Mathf.Max(1, frames);
        Action callback = null;
        callback = () =>
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || --frames > 0) return;
            InputSystem.onBeforeUpdate -= callback; Pending.Remove(callback); input();
        };
        Pending.Add(callback); InputSystem.onBeforeUpdate += callback;
        return "Input scheduled after normal frames; eval compilation stall is outside the transition.";
    }
    public static string Key(string name, bool down)
    {
        RequirePlay();
        var keyboard = _keyboard != null ? _keyboard : Keyboard.current;
        if (keyboard == null || !Enum.TryParse<UnityEngine.InputSystem.Key>(name, true, out var key) || key == UnityEngine.InputSystem.Key.None)
            throw new ArgumentException("Use an available InputSystem key.");
        if (down) HeldKeys.Add(key); else HeldKeys.Remove(key);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(HeldKeys.ToArray()));
        return name + (down ? " down queued" : " up queued");
    }
    public static string Pointer(float x, float y, string action = "move", string button = "left")
    {
        RequirePlay();
        var mouse = _mouse != null ? _mouse : Mouse.current;
        if (mouse == null || !float.IsFinite(x) || !float.IsFinite(y)) throw new ArgumentException("An available mouse and finite coordinates are required.");
        if ((button != "left" && button != "right" && button != "middle") || (action != "move" && action != "down" && action != "up")) throw new ArgumentException("Use move/down/up and left/right/middle.");
        if (action != "move")
        {
            if (button == "left") _left = action == "down";
            else if (button == "right") _right = action == "down";
            else _middle = action == "down";
        }
        // Editor와 Player는 별도 상태 버퍼를 사용합니다. Editor StateEvent.From의 0값으로
        // 드래그 중 게임 버튼 상태를 덮지 않도록 검수자가 누른 상태를 명시적으로 유지합니다.
        var state = new MouseState { position = new Vector2(x, y) };
        if (_left) state = state.WithButton(MouseButton.Left);
        if (_right) state = state.WithButton(MouseButton.Right);
        if (_middle) state = state.WithButton(MouseButton.Middle);
        InputSystem.QueueStateEvent(mouse, state);
        return $"pointer {action} queued at {x}, {y}";
    }
    public static string Scroll(float delta)
    {
        RequirePlay(); if (Mouse.current == null) throw new InvalidOperationException("No mouse.");
        InputSystem.QueueDeltaStateEvent(Mouse.current.scroll, new Vector2(0, delta)); return "scroll queued";
    }
    private static void RequirePlay() { if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use the running Game View."); }
}
