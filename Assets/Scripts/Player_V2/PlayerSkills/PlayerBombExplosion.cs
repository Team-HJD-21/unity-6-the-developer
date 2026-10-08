using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>폭탄의 지연 폭발과 단일 대상 중복 피해 방지, 폭발 연출의 수명을 관리합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerBombExplosion : MonoBehaviour
{
    [FormerlySerializedAs("bomb")]
    [SerializeField] private GameObject _bombVisual;
    [FormerlySerializedAs("bombDamage")]
    [SerializeField, Min(0f)] private float _damage = 250f;
    [FormerlySerializedAs("explosionRange")]
    [SerializeField, Min(.01f)] private float _radius = 3f;
    [SerializeField, Min(0f)] private float _fuseSeconds = 2.1f;
    [SerializeField, Min(.01f)] private float _effectSeconds = .8f;
    [FormerlySerializedAs("animator")]
    [SerializeField] private Animator _animator;
    private static readonly int ExplosionHash = Animator.StringToHash("isboom");
    private readonly HashSet<EnemyHealth> _hitEnemies = new HashSet<EnemyHealth>();
    private readonly HashSet<Monster> _hitMonsters = new HashSet<Monster>();
    private float _elapsed;
    private bool _exploded;
    private bool _hasExplosionParameter;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator != null && _animator.runtimeAnimatorController != null)
            foreach (var parameter in _animator.parameters)
                if (parameter.nameHash == ExplosionHash && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasExplosionParameter = true;
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        _exploded = false;
        _hitEnemies.Clear();
        _hitMonsters.Clear();
        if (_hasExplosionParameter) _animator.SetBool(ExplosionHash, false);
        if (_bombVisual != null && _bombVisual != gameObject) _bombVisual.SetActive(true);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (!_exploded && _elapsed >= _fuseSeconds) Explode();
        if (_exploded && _elapsed >= _fuseSeconds + _effectSeconds) Destroy(gameObject);
    }

    public bool Explode()
    {
        if (_exploded || !isActiveAndEnabled) return false;
        _exploded = true;
        _elapsed = Mathf.Max(_elapsed, _fuseSeconds);
        if (_hasExplosionParameter) _animator.SetBool(ExplosionHash, true);
        foreach (var hit in Physics2D.OverlapCircleAll(transform.position, Mathf.Max(.01f, _radius)))
        {
            if (hit == null) continue;
            var health = hit.GetComponentInParent<EnemyHealth>();
            if (health != null)
            {
                if (health.CurrentHealth > 0 && _hitEnemies.Add(health)) health.TakeDamage(Mathf.RoundToInt(Mathf.Max(0f, _damage)));
                continue;
            }
            var monster = hit.GetComponentInParent<Monster>();
            if (monster != null && !monster.isDead && _hitMonsters.Add(monster)) monster.TakeDamage(Mathf.Max(0f, _damage));
        }
        return true;
    }
}
