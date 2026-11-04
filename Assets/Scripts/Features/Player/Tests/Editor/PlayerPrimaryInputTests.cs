using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class PlayerPrimaryInputTests
{
    [UnityTest]
    public IEnumerator UiClosingClickAndConsumedPlacementCannotLeakIntoAttack()
    {
        yield return new EnterPlayMode();
        Mouse mouse = null;
        GameObject owner = null;
        GameObject ui = null;
        GameObject events = null;
        try
        {
            mouse = InputSystem.AddDevice<Mouse>();
            owner = new GameObject("Primary input test owner", typeof(PlayerInputHandler));
            var input = owner.GetComponent<PlayerInputHandler>();
            if (EventSystem.current == null) events = new GameObject("Input test events", typeof(EventSystem));
            ui = new GameObject("Blocking UI", typeof(Canvas), typeof(GraphicRaycaster));
            ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var image = new GameObject("Raycast target", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(ui.transform, false);
            var rect = image.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();

            var pointerPosition = new Vector2(Screen.width * .5f, Screen.height * .5f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointerPosition }.WithButton(MouseButton.Left));
            InputSystem.Update();
            Assert.That(GameInput.IsPointerOverUi(), Is.True);
            // Input samples the UI before an onClick handler hides it later in Update.
            typeof(PlayerInputHandler).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, null);
            ui.SetActive(false);
            Assert.That(GameInput.PrimaryPointerWasConsumed, Is.True);
            Assert.That(input.ResolvePrimaryAttack(true), Is.False);
            yield return null;
            Assert.That(input.ResolvePrimaryAttack(true), Is.False, "Holding a UI click after closing must not fire.");

            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointerPosition });
            InputSystem.Update();
            input.ResolvePrimaryAttack(true);
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointerPosition }.WithButton(MouseButton.Left));
            InputSystem.Update();
            GameInput.ReservePrimaryPointerForPlacement();
            Assert.That(GameInput.TryConsumePrimaryPointerPress(), Is.True);
            Assert.That(GameInput.TryConsumePrimaryPointerPress(), Is.False, "Only one consumer can confirm a click.");
            Assert.That(input.ResolvePrimaryAttack(true), Is.False);
            yield return null;
            Assert.That(input.ResolvePrimaryAttack(true), Is.False, "A finished or failed placement must not start held fire.");

            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointerPosition });
            InputSystem.Update();
            input.ResolvePrimaryAttack(true);
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointerPosition }.WithButton(MouseButton.Left));
            InputSystem.Update();
            Assert.That(input.ResolvePrimaryAttack(true), Is.True, "A new world click can attack.");
            Assert.That(input.ResolvePrimaryAttack(false), Is.False, "Control/selection locks stop an existing attack.");
            yield return null;
            Assert.That(input.ResolvePrimaryAttack(true), Is.False, "Unlocking while held must not resume it.");
            input.enabled = false;
            Assert.That(input.IsAttackPressed, Is.False);
        }
        finally
        {
            if (ui != null) Object.DestroyImmediate(ui);
            if (events != null) Object.DestroyImmediate(events);
            if (owner != null) Object.DestroyImmediate(owner);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
        }
        yield return new ExitPlayMode();
    }
}
