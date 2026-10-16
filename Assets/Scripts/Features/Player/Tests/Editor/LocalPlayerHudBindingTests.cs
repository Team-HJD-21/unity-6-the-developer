// 명시적 로컬 actor 주입·대기·교체·다른 Match 해제 거부를 실제 HUD에서 검증합니다.
// Player의 이동/공격 네트워크 복제는 이 계약의 범위가 아닙니다.
using System.Collections;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
public sealed class LocalPlayerHudBindingTests
{
    [UnityTearDown]
    public IEnumerator StopScene()
    {
        var root = Object.FindAnyObjectByType<TeamHJD.Game.Bootstrap.AppRoot>();
        if (root != null) root.EndCurrentMatch();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator InitialStageInjectsActorAndHudDoesNotFallBackToAnotherParticipant()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        var hud = Object.FindAnyObjectByType<InitialStagePlayerHUD>();
        var root = Object.FindAnyObjectByType<TeamHJD.Game.Bootstrap.AppRoot>();
        var vitals = Object.FindAnyObjectByType<StagePlayerVitalsView>();
        Assert.That(hud, Is.Not.Null);
        Assert.That(root, Is.Not.Null);
        Assert.That(vitals, Is.Not.Null);
        float readyDeadline = Time.realtimeSinceStartup + 3;
        while (vitals.Player == null && Time.realtimeSinceStartup < readyDeadline) yield return null;
        var original = vitals.Player;
        Assert.That(original, Is.Not.Null, "Scene composition must supply the local actor before HUD assertions.");
        Assert.That(hud.BoundMatchId, Is.EqualTo(root.CurrentMatchId));
        Assert.That(hud.BoundPlayerId, Is.EqualTo(original.GetComponent<PlayerActorIdentity>().PlayerId));
        Debug.Log("HUD_BINDING_TEST: original actor verified");
        var other = new GameObject("Other participant", typeof(PlayerInfo), typeof(PlayerActorIdentity));
        var otherId = new PlayerId("client-42"); other.GetComponent<PlayerActorIdentity>().Bind(otherId);
        var otherMatch = new MatchId("replacement-match");
        hud.PrepareLocalActor(otherMatch, otherId);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(vitals.Player, Is.Null, "While waiting, do not choose the old local-player or an arbitrary actor.");
        hud.BindLocalActor(otherMatch, otherId, other);
        Assert.That(vitals.Player, Is.SameAs(other.GetComponent<PlayerInfo>()));
        hud.UnbindLocalActor(root.CurrentMatchId);
        Assert.That(hud.BoundMatchId, Is.EqualTo(otherMatch));
        Assert.That(vitals.Player, Is.SameAs(other.GetComponent<PlayerInfo>()));
        hud.gameObject.SetActive(false); hud.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(vitals.Player, Is.SameAs(other.GetComponent<PlayerInfo>()), "A temporary HUD disable must not lose ownership.");
        AssertWrongIdentityRejected(hud, otherMatch, other);
        hud.UnbindLocalActor(otherMatch);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(vitals.Player, Is.Null);
        Assert.That(original, Is.Not.Null, "UI unbind must not destroy an actor.");
        Assert.That(other, Is.Not.Null);
        Debug.Log("HUD_BINDING_TEST: replacement, disable and Match guard verified");
        root.EndCurrentMatch();
        yield return new ExitPlayMode();
    }
    private static void AssertWrongIdentityRejected(InitialStagePlayerHUD hud, MatchId match, GameObject actor)
    {
        // EnterPlayMode 이전에 만들어진 iterator display-class는 도메인 재로드로 사라질 수 있습니다.
        // 캡처는 재생 진입이 끝난 뒤 호출하는 일반 메서드 내부로 제한합니다.
        Assert.Throws<System.ArgumentException>(() => hud.BindLocalActor(match, new PlayerId("wrong"), actor));
    }
}
