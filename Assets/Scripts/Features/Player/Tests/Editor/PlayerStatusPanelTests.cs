using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class PlayerStatusPanelTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject _hud, _player, _replacement;
    private PlayerStatusPanel _panel;
    private StagePlayerVitalsView _vitals;
    private Keyboard _keyboard, _previousKeyboard;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _previousKeyboard = Keyboard.current;
        _keyboard = InputSystem.AddDevice<Keyboard>();
        _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        _hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab"));
        _hud.GetComponent<InitialStagePlayerHUD>().enabled = false;
        _vitals = _hud.GetComponent<StagePlayerVitalsView>();
        _vitals.BindPlayer(_player.GetComponent<PlayerInfo>());
        _panel = _hud.GetComponent<PlayerStatusPanel>();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_hud != null) Object.DestroyImmediate(_hud);
        if (_player != null) Object.DestroyImmediate(_player);
        if (_replacement != null) Object.DestroyImmediate(_replacement);
        if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator IOpensAndEscapeClosesWithoutPassingTheSamePressToCore()
    {
        int count = _hud.GetComponentsInChildren<Transform>(true).Length;
        Assert.That(_panel.IsOpen, Is.False);
        Press(Key.I);
        Tick();
        Assert.That(_panel.IsOpen, Is.True);
        Press();
        yield return null;
        Press(Key.Escape);
        Tick();
        Assert.That(_panel.IsOpen, Is.False);
        Assert.That(UIEscapeInput.WasPressedThisFrame, Is.False, "Core must not also open its menu");
        Assert.That(GameInput.WasPressedThisFrame(GameKey.Escape), Is.False);
        Assert.That(_player.GetComponent<PlayerControlState>().IsBlocked, Is.False);
        Assert.That(_hud.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        Press();
        yield return null;
        Press(Key.Escape);
        Tick();
        Assert.That(UIEscapeInput.WasPressedThisFrame, Is.True, "A later ESC with the panel closed still reaches Core");
    }

    [UnityTest]
    public IEnumerator SkillTreeNavigationOnlyChangesTheViewAndEscapeReturnsToStatusFirst()
    {
        Assert.That(_panel.TryOpen(), Is.True);
        var tree = Field<PlayerSkillTreePanel>("_skillTree");
        var experience = _player.GetComponent<PlayerExperience>();
        var stats = _player.GetComponent<PlayerBaseStats>();
        long previousExperience = experience.TotalExperience;
        int previousPoints = stats.AvailablePoints;
        var open = (Button)typeof(PlayerSkillTreePanel).GetField("_openButton", Private).GetValue(tree);
        var back = (Button)typeof(PlayerSkillTreePanel).GetField("_backButton", Private).GetValue(tree);
        var scroll = (ScrollRect)typeof(PlayerSkillTreePanel).GetField("_scroll", Private).GetValue(tree);
        var sections = (GameObject[])typeof(PlayerSkillTreePanel).GetField("_statusSections", Private).GetValue(tree);
        open.onClick.Invoke();
        Assert.That(tree.IsOpen, Is.True);
        Assert.That(_panel.IsOpen, Is.True);
        foreach (var section in sections) Assert.That(section.activeSelf, Is.False);
        var titles = scroll.content.GetComponentsInChildren<TMP_Text>();
        foreach (var skill in new[] { PlayerSkillId.Bomb, PlayerSkillId.PlayerTurret, PlayerSkillId.EnergyBarrier, PlayerSkillId.LureBeacon })
            Assert.That(System.Array.Exists(titles, text => text.text == PlayerSkillDescriptions.Name(skill)), Is.True);
        foreach (float position in new[] { 0f, 1f })
        {
            scroll.verticalNormalizedPosition = position;
            Canvas.ForceUpdateCanvases();
            var bar = scroll.verticalScrollbar;
            var corners = new Vector3[4];
            bar.handleRect.GetWorldCorners(corners);
            var track = (RectTransform)bar.transform;
            foreach (var corner in corners)
            {
                Vector3 point = track.InverseTransformPoint(corner);
                Assert.That(point.x, Is.InRange(track.rect.xMin - .1f, track.rect.xMax + .1f));
                Assert.That(point.y, Is.InRange(track.rect.yMin - .1f, track.rect.yMax + .1f));
            }
            Assert.That(bar.handleRect.rect.height, Is.GreaterThan(0));
        }
        scroll.content.anchoredPosition = new Vector2(0, 1000);
        back.onClick.Invoke();
        Assert.That(tree.IsOpen, Is.False);
        foreach (var section in sections) Assert.That(section.activeSelf, Is.True);
        open.onClick.Invoke();
        Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0).Within(.01f));
        Press(Key.Escape);
        Tick();
        Assert.That(tree.IsOpen, Is.False);
        Assert.That(_panel.IsOpen, Is.True);
        Assert.That(UIEscapeInput.WasPressedThisFrame, Is.False);
        Assert.That(experience.TotalExperience, Is.EqualTo(previousExperience));
        Assert.That(stats.AvailablePoints, Is.EqualTo(previousPoints));
        Press();
        yield return null;
        Press(Key.Escape);
        Tick();
        Assert.That(_panel.IsOpen, Is.False);
        Assert.That(tree.IsOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator IAndTheAuthoredCloseButtonBothCloseThePanel()
    {
        Assert.That(_panel.TryOpen(), Is.True);
        Press(Key.I);
        Tick();
        Assert.That(_panel.IsOpen, Is.False);
        Press();
        yield return null;
        Assert.That(_panel.TryOpen(), Is.True);
        Field<Button>("_closeButton").onClick.Invoke();
        Assert.That(_panel.IsOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator WeaponArrowsChangeEquipmentAndShowItsActualSettingsAndMagazine()
    {
        // 이 시험은 무기 표시를 검사합니다. 실제 마우스 유지 입력의 사격은 분리합니다.
        _player.GetComponent<PlayerAttack>().enabled = false;
        var ammo = _player.GetComponent<PlayerWeaponAmmo>();
        typeof(PlayerWeaponAmmo).GetMethod("RestoreCheckpoint", Private).Invoke(ammo, new object[] { null });
        var selection = _player.GetComponent<PlayerWeaponSelection>();
        Assert.That(selection.TrySelectWeapon(WeaponType.DefaultGun), Is.True);
        Assert.That(_panel.TryOpen(), Is.True);
        typeof(PlayerWeaponAmmo).GetMethod("TryConsumeRound", Private).Invoke(ammo, new object[] { WeaponType.DefaultGun });
        Invoke("LateUpdate");
        Assert.That(Field<TMP_Text>("_ammoValue").text, Is.EqualTo("14 / 15"));
        Field<Button>("_nextWeaponButton").onClick.Invoke();
        Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.CryoBlaster));
        Assert.That(Field<TMP_Text>("_ammoValue").text, Is.EqualTo("100%"));
        typeof(PlayerWeaponAmmo).GetMethod("TryConsumeRound", Private).Invoke(ammo, new object[] { WeaponType.CryoBlaster });
        Invoke("LateUpdate");
        Assert.That(Field<TMP_Text>("_ammoValue").text, Is.EqualTo("94%"));
        Assert.That(Field<TMP_Text>("_reloadValue").text, Is.EqualTo("0.75초"));
        Assert.That(_player.GetComponent<PlayerAttack>().TryGetCurrentWeaponStats(out var stats), Is.True);
        Assert.That(Field<TMP_Text>("_damageValue").text, Is.EqualTo(stats.Damage.ToString("0.#")));
        Assert.That(stats.IsSpray, Is.True);
        Assert.That(Field<TMP_Text>("_projectilesValue").text, Is.EqualTo("연속 분사"));
        Assert.That(Field<TMP_Text>("_weaponTitle").text, Is.EqualTo("냉각총"));
        Field<Button>("_previousWeaponButton").onClick.Invoke();
        Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.DefaultGun));
        Assert.That(Field<TMP_Text>("_ammoValue").text, Is.EqualTo("14 / 15"));
        Field<Button>("_previousWeaponButton").onClick.Invoke();
        Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
        Assert.That(Field<TMP_Text>("_ammoValue").text, Is.EqualTo("∞"));
        Assert.That(Field<TMP_Text>("_projectileSpeedValue").text, Is.EqualTo("—"));
        yield return null;
    }

    [UnityTest]
    public IEnumerator WeaponArrowAndCloseButtonConsumeTheirClicks()
    {
        var previousMouse = Mouse.current;
        var mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            Assert.That(_panel.TryOpen(), Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            InputSystem.Update();
            Field<Button>("_nextWeaponButton").onClick.Invoke();
            Assert.That(GameInput.PrimaryPointerWasConsumed, Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            InputSystem.Update();
            Field<Button>("_closeButton").onClick.Invoke();
            Assert.That(_panel.IsOpen, Is.False);
            Assert.That(GameInput.PrimaryPointerWasConsumed, Is.True);
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
    }

    [UnityTest]
    public IEnumerator OwnerReplacementAndUnbindCloseThePanelInsteadOfShowingTheOldPlayer()
    {
        Assert.That(_panel.TryOpen(), Is.True);
        _replacement = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        var replacement = _replacement.GetComponent<PlayerInfo>();
        replacement.curHp = 123;
        _vitals.BindPlayer(replacement);
        Tick();
        Assert.That(_panel.IsOpen, Is.False);
        Assert.That(_panel.Owner, Is.SameAs(replacement));
        Assert.That(_panel.TryOpen(), Is.True);
        Assert.That(Field<TMP_Text>("_healthValue").text, Is.EqualTo($"123 / {replacement.MaxHealth}"));
        _vitals.BindPlayer(null);
        Tick();
        Assert.That(_panel.IsOpen, Is.False);
        Assert.That(_panel.TryOpen(), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DebugExperienceRewardClaimAndStatButtonsAffectTheActualPlayer()
    {
        Assert.That(_panel.TryOpen(), Is.True);
        var debug = Field<PlayerProgressionDebugPanel>("_debugPanel");
        var addXp = (Button)typeof(PlayerProgressionDebugPanel).GetField("_addHundredButton", Private).GetValue(debug);
        addXp.onClick.Invoke();
        var experience = _player.GetComponent<PlayerExperience>();
        var stats = _player.GetComponent<PlayerBaseStats>();
        Assert.That(experience.Level, Is.EqualTo(2));
        Assert.That(stats.AvailablePoints, Is.Zero, "Level-up must not silently auto-claim a reward");
        var rewards = Field<PlayerLevelRewardsView>("_levelRewards");
        typeof(PlayerLevelRewardsView).GetMethod("LateUpdate", Private).Invoke(rewards, null);
        var rows = (PlayerLevelRewardRow[])typeof(PlayerLevelRewardsView).GetField("_rows", Private).GetValue(rewards);
        var claim = (Button)typeof(PlayerLevelRewardRow).GetField("_claimButton", Private).GetValue(rows[0]);
        claim.onClick.Invoke(); claim.onClick.Invoke();
        Assert.That(stats.AvailablePoints, Is.EqualTo(1));
        var movement = _player.GetComponent<PlayerMovement>();
        Field<Button[]>("_investButtons")[1].onClick.Invoke();
        Assert.That(movement.MoveSpeed, Is.EqualTo(movement.BaseMoveSpeed * 1.02f).Within(.001f));
        Assert.That(stats.AvailablePoints, Is.Zero);
        Field<Button[]>("_refundButtons")[1].onClick.Invoke();
        Assert.That(movement.MoveSpeed, Is.EqualTo(movement.BaseMoveSpeed));
        Field<Button[]>("_investButtons")[0].onClick.Invoke();
        Assert.That(_player.GetComponent<PlayerAttack>().TryGetCurrentWeaponStats(out var weapon), Is.True);
        Assert.That(weapon.Damage, Is.EqualTo(13 * 1.05f).Within(.001f));
        Assert.That(stats.ScaleWeaponDamage(20), Is.EqualTo(21).Within(.001f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator CriticalDamageUsesTheInjectedSourceAndStopsUsingItWhenUnavailable()
    {
        var experience = _player.GetComponent<PlayerExperience>();
        var stats = _player.GetComponent<PlayerBaseStats>();
        Assert.That(experience.AddExperience(5000), Is.True);
        for (int level = 2; level <= 51; level++)
        { Assert.That(stats.TryClaimReward(level), Is.True); Assert.That(stats.TryInvest(PlayerBaseStat.CriticalChance), Is.True); }
        var random = new CriticalRandom();
        typeof(PlayerBaseStats).GetField("_criticalRandom", Private).SetValue(stats, random);
        Assert.That(stats.CriticalChance, Is.EqualTo(.5f).Within(.0001f));
        Assert.That(stats.ResolveWeaponDamage(20), Is.EqualTo(30));
        Assert.That(random.Calls, Is.EqualTo(1));
        random.Available = false;
        Assert.That(stats.ResolveWeaponDamage(20), Is.EqualTo(20));
        Assert.That(random.Calls, Is.EqualTo(1));
        yield return null;
    }

    private sealed class CriticalRandom : TeamHJD.Game.Contracts.IRandomSource
    {
        public bool Available = true;
        public int Calls;
        public bool IsAvailable => Available;
        public double NextDouble() { Calls++; return 0; }
        public double NextDouble(double min, double max) => min;
        public float NextFloat() => 0;
        public float NextFloat(float min, float max) => min;
        public int NextInt(int min, int max) => min;
        public uint NextUInt32() => 0;
    }

    [UnityTest]
    public IEnumerator CoreInputLockClosesThePanelAndIsNotReleasedByClosingIt()
    {
        Assert.That(_panel.TryOpen(), Is.True);
        var controls = _player.GetComponent<PlayerControlState>();
        controls.SetInputBlocked(true);
        Tick();
        Assert.That(_panel.IsOpen, Is.False);
        Assert.That(_panel.TryOpen(), Is.False);
        _panel.Close();
        Assert.That(controls.IsBlocked, Is.True);
        controls.SetInputBlocked(false);
        Assert.That(_panel.TryOpen(), Is.True);
        yield return null;
    }

    private T Field<T>(string name) => (T)typeof(PlayerStatusPanel).GetField(name, Private).GetValue(_panel);
    private void Invoke(string name) => typeof(PlayerStatusPanel).GetMethod(name, Private).Invoke(_panel, null);
    private void Tick() => Invoke("Update");
    private void Press(params Key[] keys)
    {
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }
}
