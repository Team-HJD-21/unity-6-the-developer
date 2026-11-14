using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class PlayerArmoryRuntimeTests
{
    private GameObject _player;
    private PlayerArmory _armory;
    private PlayerArmoryStation _station;
    private InputLockService _locks;
    private Scene _scene;
    private Keyboard _keyboard, _previousKeyboard;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _previousKeyboard = Keyboard.current; _keyboard = InputSystem.AddDevice<Keyboard>();
        _scene = SceneManager.CreateScene("Armory integration");
        _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), new Vector3(42000,42000), Quaternion.identity);
        SceneManager.MoveGameObjectToScene(_player, _scene);
        _player.GetComponent<PlayerInputHandler>().enabled = false;
        _armory = _player.GetComponent<PlayerArmory>(); _locks = new InputLockService();
        _player.GetComponent<PlayerControlState>().BindHub(_locks); _armory.BindHub(_locks);
        var stationObject = new GameObject("WeaponChoice"); stationObject.transform.position = _player.transform.position;
        SceneManager.MoveGameObjectToScene(stationObject, _scene); _station = stationObject.AddComponent<PlayerArmoryStation>();
        var data = new SerializedObject(_station); data.FindProperty("_viewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PlayerArmoryView>("Assets/Prefabs/UI/PlayerArmory.prefab"); data.ApplyModifiedPropertiesWithoutUndo();
        yield return null;
        Assert.That(_armory.CanOpen, Is.True, $"spaceship={_armory.IsInSpaceship}, alive={_armory.IsAlive}, locks={_locks.Count}, configured={_armory.IsConfigured}");
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_station != null) _station.Close();
        foreach(var root in _scene.GetRootGameObjects()) Object.Destroy(root);
        yield return null; yield return SceneManager.UnloadSceneAsync(_scene);
        _locks.Dispose(); InputSystem.RemoveDevice(_keyboard); if(_previousKeyboard != null) _previousKeyboard.MakeCurrent();
        yield return new ExitPlayMode();
    }
    private static void Set(object target,string field,object value) => target.GetType().GetField(field,Private).SetValue(target,value);
    private IEnumerator KeyFrame(Key key)
    {
        _keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(_keyboard,new KeyboardState(key)); yield return null;
        InputSystem.QueueStateEvent(_keyboard,new KeyboardState()); yield return null; yield return null;
    }
    [UnityTest]
    public IEnumerator FOpensAuthoredUiAndEscapeOrLeavingReleasesOnlyOwnInputLock()
    {
        Assert.That(_station.View.IsConfigured, Is.True);
        yield return KeyFrame(Key.F);
        Assert.That(_station.View.IsOpen, Is.True); Assert.That(_locks.Count, Is.EqualTo(1));
        Assert.That(_player.GetComponent<PlayerControlState>().CanMove, Is.False);
        var other = _locks.Acquire("Other menu");
        yield return KeyFrame(Key.Escape);
        Assert.That(_station.View.IsOpen, Is.False); Assert.That(_locks.Count, Is.EqualTo(1));
        other.Dispose(); Assert.That(_locks.Count, Is.Zero);
        yield return KeyFrame(Key.F); Assert.That(_station.View.IsOpen, Is.True);
        _player.transform.position += Vector3.right*10; yield return null;
        Assert.That(_station.View.IsOpen, Is.False); Assert.That(_locks.Count, Is.Zero);
    }
    [UnityTest]
    public IEnumerator LevelUpPaysTokensAndButtonsUpgradeActualStatsWithoutChangingInterval()
    {
        Assert.That(_player.GetComponent<PlayerExperience>().AddExperience(300), Is.True);
        Assert.That(_armory.State.Tokens, Is.EqualTo(3));
        yield return KeyFrame(Key.F);
        Assert.That(_armory.TryGetStats(WeaponType.DefaultGun,false,out var before), Is.True);
        Assert.That(_armory.TryGetStats(WeaponType.DefaultGun,true,out var preview), Is.True);
        Assert.That(preview.ProjectileCount, Is.EqualTo(3)); Assert.That(preview.Interval, Is.EqualTo(before.Interval));
        var upgrade = System.Array.Find(_station.View.GetComponentsInChildren<Button>(true),b=>b.name=="Upgrade");
        upgrade.onClick.Invoke();
        Assert.That(_armory.State.Level(WeaponType.DefaultGun), Is.EqualTo(2)); Assert.That(_armory.State.Tokens, Is.EqualTo(2));
        Assert.That(_armory.TryGetStats(WeaponType.DefaultGun,false,out var actual), Is.True);
        Assert.That(actual.Damage, Is.EqualTo(preview.Damage).Within(.001f)); Assert.That(actual.ProjectileCount, Is.EqualTo(3));
        Assert.That(actual.Interval, Is.EqualTo(before.Interval));
        upgrade.onClick.Invoke(); upgrade.onClick.Invoke();
        Assert.That(_armory.State.Level(WeaponType.DefaultGun), Is.EqualTo(3)); Assert.That(_armory.State.Tokens, Is.EqualTo(1));
        Assert.That(upgrade.interactable, Is.False); Assert.That(_station.View.ComparisonText, Does.Contain("최고"));
        _station.Close(); Assert.That(_armory.TryUpgrade(WeaponType.Sniper), Is.False);
    }
    [UnityTest]
    public IEnumerator EquipChangesAvailabilityAndTabWheelAndNumericSelectionCannotReachUnequippedGun()
    {
        var selection = _player.GetComponent<PlayerWeaponSelection>();
        yield return KeyFrame(Key.F); _station.View.Select(WeaponType.Sniper);
        var equip = System.Array.Find(_station.View.GetComponentsInChildren<Button>(true),b=>b.name=="Equip Gun 1"); equip.onClick.Invoke();
        Assert.That(_armory.State.First, Is.EqualTo(WeaponType.Sniper));
        _station.Close();
        // 독립 fixture를 허브에서 필드 입력 상태로 바꿉니다. 실제 Scene 왕복 인수와는 별개입니다.
        var controls = _player.GetComponent<PlayerControlState>(); Set(controls,"_hubOnly",false);Set(controls,"_wasBound",false);
        Assert.That(selection.IsWeaponAvailable(WeaponType.DefaultGun), Is.False);
        Assert.That(selection.TrySelectWeaponSlot(2), Is.False);
        Assert.That(selection.TrySelectWeapon(WeaponType.Spanner), Is.True);
        selection.SelectNextWeapon(); Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.CryoBlaster));
        selection.SelectNextWeapon(); Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Sniper));
        selection.SelectNextWeapon(); Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
        Assert.That(selection.SelectAdjacentWeapon(-1), Is.True); Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Sniper));
        Assert.That(_armory.TryEquip(0,WeaponType.DefaultGun), Is.False);
    }

    [UnityTest]
    public IEnumerator UpgradedDefaultGunActuallySpawnsFivePelletsAndPreviewMatchesTheirDamage()
    {
        _player.GetComponent<PlayerExperience>().AddExperience(200);
        yield return KeyFrame(Key.F);
        Assert.That(_armory.TryUpgrade(WeaponType.DefaultGun), Is.True);
        Assert.That(_armory.TryUpgrade(WeaponType.DefaultGun), Is.True);
        _armory.TryGetStats(WeaponType.DefaultGun,false,out var stats);
        // 실제 캔버스 프레임 캡처. 테스트 fixture이며 실제 게임 Scene 인수를 대신하지 않습니다.
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot("Docs/Local/PlayerArmory/armory-max.png");
        yield return null; yield return null;
        _station.Close();
        var controls=_player.GetComponent<PlayerControlState>(); Set(controls,"_hubOnly",false);Set(controls,"_wasBound",false);
        var selection=_player.GetComponent<PlayerWeaponSelection>(); Assert.That(selection.TrySelectWeapon(WeaponType.DefaultGun),Is.True);
        Assert.That(_player.GetComponent<PlayerAttack>().TryFire(),Is.True);
        var bullets=Object.FindObjectsByType<PlayerBullet>(FindObjectsSortMode.None);
        int count=0;
        foreach(var bullet in bullets)
        {
            if(bullet.gameObject.scene!=_scene)continue;
            count++; Assert.That((float)typeof(PlayerBullet).GetField("_damage",Private).GetValue(bullet),Is.EqualTo(stats.Damage).Within(.001f));
        }
        Assert.That(count,Is.EqualTo(5));
        Assert.That(_player.GetComponent<PlayerWeaponAmmo>().CurrentRounds,Is.EqualTo(14),"Five pellets still consume a single round.");
    }

    [UnityTest]
    public IEnumerator CheckpointRestoresAndRollsBackLoadoutLevelsAndSpentTokens()
    {
        var identity=_player.GetComponent<PlayerActorIdentity>() ?? _player.AddComponent<PlayerActorIdentity>();
        var id=new PlayerId("armory-test");identity.Bind(id,0);
        var config=new MatchConfig(new MatchId("armory-test"),MatchMode.Story,new StageId("stage"),new DefinitionId("normal"),"test",1,
            new[]{new PlayerLoadoutSnapshot(id,null)});
        var context=new CheckpointCaptureContext(config,0,System.Guid.NewGuid());
        var codec=new BinaryCheckpointPayloadCodec();
        _player.GetComponent<PlayerExperience>().AddExperience(300);yield return KeyFrame(Key.F);
        Assert.That(_armory.TryUpgrade(WeaponType.DefaultGun),Is.True);
        var writer=(ICheckpointParticipant)_armory;var saved=writer.Capture(context,codec);
        Assert.That(_armory.TryUpgrade(WeaponType.DefaultGun),Is.True);
        Assert.That(_armory.TryEquip(0,WeaponType.Sniper),Is.True);
        var target=new CheckpointData(System.Guid.NewGuid(),System.DateTimeOffset.UtcNow,config.MatchId,config.StageId,
            config.DifficultyProfileId,config.Mode,config.RandomSeed,config.ContentVersion,0,1,new[]{saved});
        using var step=writer.PrepareRestore(new CheckpointRestoreContext(context,target,CheckpointRestorePolicy.Default),saved,codec);
        step.Apply();
        Assert.That(_armory.State.First,Is.EqualTo(WeaponType.DefaultGun));Assert.That(_armory.Level(WeaponType.DefaultGun),Is.EqualTo(2));
        Assert.That(_armory.State.Tokens,Is.EqualTo(2));Assert.That(_armory.IsOpen,Is.False);
        step.Rollback();
        Assert.That(_armory.State.First,Is.EqualTo(WeaponType.Sniper));Assert.That(_armory.Level(WeaponType.DefaultGun),Is.EqualTo(3));
        Assert.That(_armory.State.Tokens,Is.EqualTo(1));
    }
}
