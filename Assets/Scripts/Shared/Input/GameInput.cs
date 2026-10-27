using UnityEngine;
using UnityEngine.InputSystem;

public enum GameKey
{
    Escape,
    Space,
    E,
    F1,
    W,
    A,
    S,
    D,
    Q,
    LeftShift,
    RightShift,
    UpArrow,
    DownArrow,
    LeftArrow,
    RightArrow,
    F,
    Tab,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Numpad1,
    Numpad2,
    Numpad3,
    Numpad4
}

public static class GameInput
{
    public static Vector2 PointerPosition =>
        Mouse.current?.position.ReadValue() ?? Vector2.zero;

    public static bool PrimaryPointerWasPressedThisFrame =>
        Mouse.current?.leftButton.wasPressedThisFrame ?? false;

    public static bool SecondaryPointerWasPressedThisFrame =>
        Mouse.current?.rightButton.wasPressedThisFrame ?? false;

    public static float ScrollY
    {
        get
        {
            float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            return Mathf.Approximately(scroll, 0f) ? 0f : Mathf.Sign(scroll);
        }
    }

    public static Vector2 Move
    {
        get
        {
            Vector2 keyboardMove = Vector2.zero;

            if (IsPressed(GameKey.W) || IsPressed(GameKey.UpArrow)) keyboardMove.y += 1f;
            if (IsPressed(GameKey.S) || IsPressed(GameKey.DownArrow)) keyboardMove.y -= 1f;
            if (IsPressed(GameKey.A) || IsPressed(GameKey.LeftArrow)) keyboardMove.x -= 1f;
            if (IsPressed(GameKey.D) || IsPressed(GameKey.RightArrow)) keyboardMove.x += 1f;

            keyboardMove = Vector2.ClampMagnitude(keyboardMove, 1f);
            Vector2 gamepadMove = Gamepad.current?.leftStick.ReadValue() ?? Vector2.zero;

            return gamepadMove.sqrMagnitude > keyboardMove.sqrMagnitude
                ? gamepadMove
                : keyboardMove;
        }
    }

    public static bool IsPressed(GameKey key)
    {
        var control = GetControl(key);
        return control?.isPressed ?? false;
    }

    public static bool WasPressedThisFrame(GameKey key)
    {
        var control = GetControl(key);
        return control?.wasPressedThisFrame ?? false;
    }

    private static UnityEngine.InputSystem.Controls.KeyControl GetControl(GameKey key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return null;
        }

        return key switch
        {
            GameKey.Escape => keyboard.escapeKey,
            GameKey.Space => keyboard.spaceKey,
            GameKey.E => keyboard.eKey,
            GameKey.F1 => keyboard.f1Key,
            GameKey.F => keyboard.fKey,
            GameKey.Tab => keyboard.tabKey,
            GameKey.Digit1 => keyboard.digit1Key,
            GameKey.Digit2 => keyboard.digit2Key,
            GameKey.Digit3 => keyboard.digit3Key,
            GameKey.Digit4 => keyboard.digit4Key,
            GameKey.Numpad1 => keyboard.numpad1Key,
            GameKey.Numpad2 => keyboard.numpad2Key,
            GameKey.Numpad3 => keyboard.numpad3Key,
            GameKey.Numpad4 => keyboard.numpad4Key,
            GameKey.W => keyboard.wKey,
            GameKey.A => keyboard.aKey,
            GameKey.S => keyboard.sKey,
            GameKey.D => keyboard.dKey,
            GameKey.Q => keyboard.qKey,
            GameKey.LeftShift => keyboard.leftShiftKey,
            GameKey.RightShift => keyboard.rightShiftKey,
            GameKey.UpArrow => keyboard.upArrowKey,
            GameKey.DownArrow => keyboard.downArrowKey,
            GameKey.LeftArrow => keyboard.leftArrowKey,
            GameKey.RightArrow => keyboard.rightArrowKey,
            _ => null
        };
    }
}
