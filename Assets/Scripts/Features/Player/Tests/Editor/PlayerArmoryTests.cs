using NUnit.Framework;

public sealed class PlayerArmoryTests
{
    [Test]
    public void SpaceshipWeaponChoiceReferencesTheAuthoredArmoryView()
    {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/Spaceship/SpaceShipGrayBox.unity");
        try
        {
            PlayerArmoryStation station=null;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var candidate in root.GetComponentsInChildren<PlayerArmoryStation>(true))
                { Assert.That(station,Is.Null,"Only one armory station is authored."); station=candidate; }
            Assert.That(station,Is.Not.Null);Assert.That(station.name,Is.EqualTo("WeaponChoice"));
            var data=new UnityEditor.SerializedObject(station);
            var view=(PlayerArmoryView)data.FindProperty("_viewPrefab").objectReferenceValue;
            Assert.That(view,Is.Not.Null);Assert.That(view.IsConfigured,Is.True);
        }
        finally {UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
    }
    [Test]
    public void TwoDistinctGunsAndFixedSpannerAreEquippedAndDuplicateSelectionSwapsSlots()
    {
        var state = new PlayerArmoryState();
        Assert.That(state.IsEquipped(WeaponType.Spanner), Is.True);
        Assert.That(state.IsEquipped(WeaponType.Sniper), Is.False);
        Assert.That(state.Equip(0, WeaponType.Sniper), Is.True);
        Assert.That(state.First, Is.EqualTo(WeaponType.Sniper));
        Assert.That(state.IsEquipped(WeaponType.DefaultGun), Is.False);
        Assert.That(state.Equip(1, WeaponType.Sniper), Is.True);
        Assert.That(state.First, Is.EqualTo(WeaponType.CryoBlaster));
        Assert.That(state.Second, Is.EqualTo(WeaponType.Sniper));
        Assert.That(state.Equip(0, WeaponType.Spanner), Is.False);
        Assert.That(state.Equip(2, WeaponType.DefaultGun), Is.False);
    }

    [Test]
    public void LevelJumpsGrantExactlyOnceAndEveryUpgradeCostsOneToken()
    {
        var state = new PlayerArmoryState();
        Assert.That(state.Upgrade(WeaponType.DefaultGun), Is.False);
        Assert.That(state.GrantLevelRewards(4, 1), Is.True);
        Assert.That(state.Tokens, Is.EqualTo(3));
        Assert.That(state.GrantLevelRewards(4, 1), Is.False);
        Assert.That(state.GrantLevelRewards(2, 1), Is.False);
        Assert.That(state.Upgrade(WeaponType.DefaultGun), Is.True);
        Assert.That(state.Upgrade(WeaponType.DefaultGun), Is.True);
        Assert.That(state.Upgrade(WeaponType.DefaultGun), Is.False);
        Assert.That(state.Tokens, Is.EqualTo(1));
        Assert.That(state.Level(WeaponType.DefaultGun), Is.EqualTo(3));
        Assert.That(state.Upgrade(WeaponType.Spanner), Is.False);
        Assert.That(state.Upgrade((WeaponType)99), Is.False);
        Assert.That(state.Upgrade(WeaponType.Sniper), Is.True);
        Assert.That(state.Upgrade(WeaponType.CryoBlaster), Is.False);
    }

    [Test]
    public void NewActorStartsAtLevelOneWithNoTokens()
    {
        var old = new PlayerArmoryState(); old.GrantLevelRewards(10, 1); old.Upgrade(WeaponType.Sniper);
        var fresh = new PlayerArmoryState();
        Assert.That(fresh.Level(WeaponType.Sniper), Is.EqualTo(1)); Assert.That(fresh.Tokens, Is.Zero);
    }

    [TestCase(1, 1, 2, .8f)] [TestCase(2, 3, 3, .65f)] [TestCase(3, 5, 4, .5f)]
    public void AuthoredTraitsHaveThreeLevels(int level, int pellets, int targets, float slow)
    {
        var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerArmorySettings>("Assets/Data/Player/PlayerArmorySettings.asset");
        Assert.That(settings.IsValid, Is.True);
        Assert.That(settings.PelletCount(level), Is.EqualTo(pellets));
        Assert.That(settings.SniperTargets(level), Is.EqualTo(targets));
        Assert.That(settings.SlowMultiplier(level), Is.EqualTo(slow).Within(.001f));
    }
}
