using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap.SceneComposition;
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
        InputSystem.QueueStateEvent(_keyboard,new KeyboardState(key)); InputSystem.Update();
        typeof(PlayerArmoryStation).GetMethod("Update", Private).Invoke(_station, null);
        InputSystem.QueueStateEvent(_keyboard,new KeyboardState()); InputSystem.Update();
        yield return null; yield return null;
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
        _player.transform.position += Vector3.right*10;
        typeof(PlayerArmoryStation).GetMethod("Update", Private).Invoke(_station, null);
        yield return null;
        Assert.That(_station.View.IsOpen, Is.False); Assert.That(_locks.Count, Is.Zero);
    }
    [UnityTest]
    public IEnumerator FirstHubWithoutEventSystemAcceptsRealPointerClicksAndCleansUpOwnedInput()
    {
        var previous = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        var previousEnabled = System.Array.ConvertAll(previous, events => events.enabled);
        var mouseBefore = Mouse.current;
        var mouse = InputSystem.AddDevice<Mouse>();
        var cursorBefore = Cursor.lockState;
        try
        {
            foreach (var events in previous) events.enabled = false;
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.Null);
            Cursor.lockState = CursorLockMode.None;
            yield return KeyFrame(Key.F);
            Assert.That(_station.View.IsOpen, Is.True);
            var created = UnityEngine.EventSystems.EventSystem.current;
            Assert.That(created, Is.Not.Null);
            Assert.That(created.transform.parent, Is.EqualTo(_station.transform));
            Assert.That(created.currentInputModule, Is.TypeOf<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());
            _station.View.Select(WeaponType.Sniper);
            Canvas.ForceUpdateCanvases();
            var button = System.Array.Find(_station.View.GetComponentsInChildren<Button>(), b => b.name == "Equip Gun 1");
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            mouse.MakeCurrent();
            void Pointer(MouseState state)
            {
                InputSystem.QueueStateEvent(mouse, state); InputSystem.Update();
                typeof(UnityEngine.EventSystems.EventSystem).GetMethod("Update", Private).Invoke(created, null);
            }
            Pointer(new MouseState { position = point });
            Pointer(new MouseState { position = point }.WithButton(MouseButton.Left));
            Pointer(new MouseState { position = point });
            Assert.That(_armory.State.First, Is.EqualTo(WeaponType.Sniper), "Exercise the real input module, raycast and button callback.");
            _station.Close(); yield return null;
            Assert.That(created == null, Is.True);
            Assert.That(_locks.Count, Is.Zero);
        }
        finally
        {
            _station.Close(); Cursor.lockState = cursorBefore;
            for (int i = 0; i < previous.Length; i++) if (previous[i] != null) previous[i].enabled = previousEnabled[i];
            InputSystem.RemoveDevice(mouse); if (mouseBefore != null) mouseBefore.MakeCurrent();
        }
    }

    [UnityTest]
    public IEnumerator LevelUpPaysTokensAndButtonsUpgradeActualStatsWithoutChangingInterval()
    {
        Assert.That(_player.GetComponent<PlayerExperience>().AddExperience(300), Is.True);
        Assert.That(_armory.State.Tokens, Is.EqualTo(4));
        yield return KeyFrame(Key.F);
        Assert.That(_armory.TryGetStats(WeaponType.DefaultGun,false,out var before), Is.True);
        Assert.That(_armory.TryGetStats(WeaponType.DefaultGun,true,out var preview), Is.True);
        Assert.That(preview.ProjectileCount, Is.EqualTo(3)); Assert.That(preview.Interval, Is.EqualTo(before.Interval));
        var upgrade = System.Array.Find(_station.View.GetComponentsInChildren<Button>(true),b=>b.name=="Upgrade");
        upgrade.onClick.Invoke();
        Assert.That(_armory.State.Level(WeaponType.DefaultGun), Is.EqualTo(2)); Assert.That(_armory.State.Tokens, Is.EqualTo(3));
        Assert.That(_armory.TryGetStats(WeaponType.DefaultGun,false,out var actual), Is.True);
        Assert.That(actual.Damage, Is.EqualTo(preview.Damage).Within(.001f)); Assert.That(actual.ProjectileCount, Is.EqualTo(3));
        Assert.That(actual.Interval, Is.EqualTo(before.Interval));
        upgrade.onClick.Invoke(); upgrade.onClick.Invoke();
        Assert.That(_armory.State.Level(WeaponType.DefaultGun), Is.EqualTo(3)); Assert.That(_armory.State.Tokens, Is.EqualTo(2));
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
        Assert.That(_armory.State.Tokens,Is.EqualTo(3));Assert.That(_armory.IsOpen,Is.False);
        step.Rollback();
        Assert.That(_armory.State.First,Is.EqualTo(WeaponType.Sniper));Assert.That(_armory.Level(WeaponType.DefaultGun),Is.EqualTo(3));
        Assert.That(_armory.State.Tokens,Is.EqualTo(2));
    }

    private PlayerArmory SpawnReplacement(bool hub, InputLockService input)
    {
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),_station.transform.position,Quaternion.identity);
        SceneManager.MoveGameObjectToScene(actor,_scene);
        actor.GetComponent<PlayerInputHandler>().enabled=false;actor.GetComponent<PlayerMovement>().enabled=false;actor.GetComponent<Rigidbody2D>().simulated=false;
        var armory=actor.GetComponent<PlayerArmory>();
        if(hub){actor.GetComponent<PlayerControlState>().BindHub(input);armory.BindHub(input);}
        else armory.BindInputBlocker(input);
        return armory;
    }
    private void AssertDisplayedTokens(int tokens)
    {
        var label=(TMPro.TMP_Text)typeof(PlayerArmoryView).GetField("_tokens",Private).GetValue(_station.View);
        Assert.That(label.text,Is.EqualTo($"강화 토큰  {tokens}개"));
    }
    [UnityTest]
    public IEnumerator FirstHubAtRestoredLevelFiveKeepsSpentTokensAcrossActorReplacement()
    {yield return FirstOpenAtRestoredLevel(5);}
    [UnityTest]
    public IEnumerator FirstHubAtRestoredLevelTenKeepsSpentTokensAcrossActorReplacement()
    {yield return FirstOpenAtRestoredLevel(10);}
    private IEnumerator FirstOpenAtRestoredLevel(int level)
    {
        var id=new PlayerId("local-player");(_player.GetComponent<PlayerActorIdentity>()??_player.AddComponent<PlayerActorIdentity>()).Bind(id,0);
        var config=new MatchConfig(new MatchId("first-hub-restored"),MatchMode.Story,new StageId("stage"),new DefinitionId("normal"),"test",1,new[]{new PlayerLoadoutSnapshot(id,null)});
        var context=new CheckpointCaptureContext(config,0,System.Guid.NewGuid());var codec=new BinaryCheckpointPayloadCodec();
        var store=new HubGrowthStore();var slot=GrowthSaveService.SlotForOwner("player/"+id.Value);
        using(var saved=new GrowthSaveService(store,codec,slot,()=>context,_player.GetComponentsInChildren<MonoBehaviour>(true).OfType<IGrowthCheckpointParticipant>()))
        {
            saved.Load();Assert.That(_player.GetComponent<PlayerExperience>().AddExperience((level-1)*100),Is.True);
            Assert.That(_player.GetComponent<PlayerBaseStats>().TryClaimReward(2),Is.True);
            Assert.That(_player.GetComponent<PlayerBaseStats>().TryInvest(PlayerBaseStat.Attack),Is.True);saved.Flush();
        }
        var freshInput=new InputLockService();var first=SpawnReplacement(true,freshInput);RestoreHubGrowth(first,store,codec,id);_player.SetActive(false);
        yield return null;
        Assert.That(first.GetComponent<PlayerExperience>().Level,Is.EqualTo(level));Assert.That(_station.TryOpen(first),Is.True);AssertDisplayedTokens(level);
        Assert.That(first.GetComponent<PlayerBaseStats>().Invested(PlayerBaseStat.Attack),Is.EqualTo(1));Assert.That(first.GetComponent<PlayerBaseStats>().IsRewardClaimed(2),Is.True);
        Assert.That(first.GetComponent<PlayerSkillResearch>().Tokens,Is.EqualTo(level));
        Assert.That(first.TryUpgrade(WeaponType.DefaultGun),Is.True);Assert.That(first.TryUpgrade(WeaponType.DefaultGun),Is.True);AssertDisplayedTokens(level-2);_station.Close();
        var returned=SpawnReplacement(true,freshInput);RestoreHubGrowth(returned,store,codec,id);first.gameObject.SetActive(false);
        yield return null;
        Assert.That(returned.GetComponent<PlayerExperience>().Level,Is.EqualTo(level));
        Assert.That(_station.TryOpen(returned),Is.True);AssertDisplayedTokens(level-2);Assert.That(returned.Level(WeaponType.DefaultGun),Is.EqualTo(3));_station.Close();
        var field=SpawnReplacement(false,freshInput);returned.gameObject.SetActive(false);
        Assert.That(field.GetComponent<PlayerExperience>().AddExperience((level-1)*100),Is.True);
        Assert.That(field.State.Tokens,Is.EqualTo(level-2),"Restoring the same achieved level cannot refund upgrades or pay it again.");
    }
    private void RestoreHubGrowth(PlayerArmory owner,ICheckpointStore store,ICheckpointPayloadCodec codec,PlayerId id)
    {
        (owner.GetComponent<PlayerActorIdentity>()??owner.gameObject.AddComponent<PlayerActorIdentity>()).Bind(id,0);
        var root=new GameObject("Hub growth composition",typeof(SpaceshipHubCompositionRoot));SceneManager.MoveGameObjectToScene(root,_scene);
        var hub=root.GetComponent<SpaceshipHubCompositionRoot>();typeof(SpaceshipHubCompositionRoot).GetProperty("Player").SetValue(hub,owner.gameObject);
        typeof(SpaceshipHubCompositionRoot).GetMethod("BindPersonalGrowth",Private).Invoke(hub,new object[]{store,codec,id});
    }
    private sealed class HubGrowthStore : ICheckpointStore
    {
        private readonly System.Collections.Generic.Dictionary<CheckpointSlot,CheckpointData> _data=new();
        public System.Threading.Tasks.Task SaveAsync(CheckpointSlot slot,CheckpointData data,System.Threading.CancellationToken cancellationToken=default)
        {_data[slot]=data;return System.Threading.Tasks.Task.CompletedTask;}
        public System.Threading.Tasks.Task<CheckpointData> LoadAsync(CheckpointSlot slot,System.Threading.CancellationToken cancellationToken=default)
        {return System.Threading.Tasks.Task.FromResult(_data.TryGetValue(slot,out var data)?data:null);}
    }
    [UnityTest]
    public IEnumerator FirstHubDepartureAndReturnKeepTokensUpgradesAndLoadoutAcrossNewActors()
    {
        Assert.That(_station.TryOpen(_armory),Is.True);AssertDisplayedTokens(1);
        Assert.That(_player.GetComponent<PlayerExperience>().AddExperience(300),Is.True);AssertDisplayedTokens(4);
        Assert.That(_armory.TryUpgrade(WeaponType.DefaultGun),Is.True);AssertDisplayedTokens(3);
        Assert.That(_armory.TryEquip(0,WeaponType.Sniper),Is.True);_station.Close();
        var field=SpawnReplacement(false,_locks);
        Assert.That(field.State.Tokens,Is.EqualTo(3));Assert.That(field.Level(WeaponType.DefaultGun),Is.EqualTo(2));Assert.That(field.State.First,Is.EqualTo(WeaponType.Sniper));
        Assert.That(field.GetComponent<PlayerExperience>().AddExperience(400),Is.True);
        Assert.That(field.State.Tokens,Is.EqualTo(4));
        field.BindInputBlocker(_locks);Assert.That(field.State.Tokens,Is.EqualTo(4));
        // The old scene may unload after its replacement has already earned rewards.
        _player.SetActive(false);
        var returned=SpawnReplacement(true,_locks);
        Assert.That(_station.TryOpen(returned),Is.True);AssertDisplayedTokens(4);
        Assert.That(returned.Level(WeaponType.DefaultGun),Is.EqualTo(2));Assert.That(returned.State.First,Is.EqualTo(WeaponType.Sniper));
        Assert.That(returned.TryUpgrade(WeaponType.DefaultGun),Is.True);AssertDisplayedTokens(3);_station.Close();
        field.gameObject.SetActive(false);
        returned.BindHub(_locks);Assert.That(returned.State.Tokens,Is.EqualTo(3));
        returned.UnbindHub();returned.gameObject.SetActive(false);
        var next=SpawnReplacement(false,_locks);
        Assert.That(next.State.Tokens,Is.EqualTo(3));Assert.That(next.Level(WeaponType.DefaultGun),Is.EqualTo(3));Assert.That(next.State.First,Is.EqualTo(WeaponType.Sniper));
        yield break;
    }
    [UnityTest]
    public IEnumerator DifferentLocalScopeStartsWithOneTokenAndDoesNotCopyTheOtherArmory()
    {
        Assert.That(_player.GetComponent<PlayerExperience>().AddExperience(500),Is.True);
        Assert.That(_station.TryOpen(_armory),Is.True);Assert.That(_armory.TryUpgrade(WeaponType.DefaultGun),Is.True);_station.Close();
        var independent=SpawnReplacement(true,new InputLockService());
        Assert.That(independent.State.Tokens,Is.EqualTo(1));Assert.That(independent.Level(WeaponType.DefaultGun),Is.EqualTo(1));
        var returned=SpawnReplacement(true,_locks);
        Assert.That(returned.State.Tokens,Is.EqualTo(5));Assert.That(returned.Level(WeaponType.DefaultGun),Is.EqualTo(2));
        yield break;
    }
    [UnityTest]
    public IEnumerator LegacyArmoryCheckpointGrantsTheInitialTokenOnceAndPreservesSpentUpgrades()
    {
        var id=new PlayerId("legacy-armory");(_player.GetComponent<PlayerActorIdentity>()??_player.AddComponent<PlayerActorIdentity>()).Bind(id,0);
        var config=new MatchConfig(new MatchId("legacy-armory"),MatchMode.Story,new StageId("stage"),new DefinitionId("normal"),"test",1,new[]{new PlayerLoadoutSnapshot(id,null)});
        var context=new CheckpointCaptureContext(config,0,System.Guid.NewGuid());var codec=new BinaryCheckpointPayloadCodec();var writer=(ICheckpointParticipant)_armory;
        CheckpointSection Section(int tokens)=>codec.Write(writer.Key,1,CheckpointSectionKind.Progression,w=>
        {codec.WriteText(w,id.Value);w.Write(1);w.Write((int)WeaponType.DefaultGun);w.Write((int)WeaponType.CryoBlaster);w.Write(4);w.Write(tokens);w.Write(2);w.Write(1);w.Write(2);});
        CheckpointRestoreContext Restore(CheckpointSection section)=>new(context,new CheckpointData(System.Guid.NewGuid(),System.DateTimeOffset.UtcNow,config.MatchId,config.StageId,config.DifficultyProfileId,config.Mode,1,"test",0,1,new[]{section}),CheckpointRestorePolicy.Default);
        var saved=Section(1);
        using(var step=writer.PrepareRestore(Restore(saved),saved,codec))
        {
            step.Apply();Assert.That(_armory.State.Tokens,Is.EqualTo(2));Assert.That(_armory.Level(WeaponType.Sniper),Is.EqualTo(2));
            step.Apply();Assert.That(_armory.State.Tokens,Is.EqualTo(2));step.Rollback();Assert.That(_armory.State.Tokens,Is.EqualTo(1));
        }
        var invalid=Section(2);Assert.Throws<System.IO.InvalidDataException>(()=>writer.PrepareRestore(Restore(invalid),invalid,codec));
        Assert.That(writer.Capture(context,codec).Version,Is.EqualTo(2));
        yield break;
    }
}
