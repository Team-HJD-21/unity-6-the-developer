// 실제 피해 관측이 오버킬/중복 사망을 성과로 세지 않는지 검사합니다.
using System.Collections.Generic;
using NUnit.Framework;
using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

public sealed class EnemyDamageObservationTests
{
    private GameObject _actor;
    private EnemyHealth _health;

    [SetUp]
    public void SetUp()
    {
        _actor = new GameObject("Enemy damage observation");
        _health = _actor.AddComponent<EnemyHealth>();
        _health.Initialize();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_actor);

    [Test]
    public void ActualDamageExcludesOverkillAndKeepsTheContributor()
    {
        var damage = new List<int>();
        var sources = new List<AttackSourceType>();
        int deaths = 0;
        _health.DamageApplied += (_, amount, source) => { damage.Add(amount); sources.Add(source); };
        _health.Died += _ => deaths++;
        _health.TryTakeDamage(5, AttackSourceType.Turret);
        _health.TryTakeDamage(int.MaxValue, AttackSourceType.Player);
        _health.TryTakeDamage(int.MaxValue, AttackSourceType.Player);
        Assert.That(damage, Is.EqualTo(new[] { 5, 15 }));
        Assert.That(sources, Is.EqualTo(new[] { AttackSourceType.Turret, AttackSourceType.Player }));
        Assert.That(deaths, Is.EqualTo(1));
    }

    [Test]
    public void ReentrantDamageCannotReportDeathTwice()
    {
        int deaths = 0, total = 0;
        _health.DamageApplied += (health, amount, _) =>
        {
            total += amount;
            health.TryTakeDamage(int.MaxValue, AttackSourceType.Player);
        };
        _health.Died += _ => deaths++;
        _health.TryTakeDamage(1, AttackSourceType.Player);
        Assert.That(total, Is.EqualTo(_health.MaxHealth));
        Assert.That(deaths, Is.EqualTo(1));
    }
}
