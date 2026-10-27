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
    [SerializeField, Min(.01f)] private float _radius = 9f;
    [SerializeField, Min(0f)] private float _fuseSeconds = 2.1f;
    [SerializeField, Min(.01f)] private float _effectSeconds = .8f;
    [FormerlySerializedAs("animator")]
    [SerializeField] private Animator _animator;
    [Header("Body Collision")]
    [SerializeField] private Collider2D _bodyCollider;
    [SerializeField] private PlayerFieldBodyCollision _bodyCollision;
    private static readonly int ExplosionHash = Animator.StringToHash("isboom");
    private readonly PlayerBombAreaDamage _areaDamage = new();
    private float _elapsed;
    private bool _exploded;
    private bool _hasExplosionParameter;
    private PlayerTimeBombVisual _presentation;

    private void Awake()
    {
        _presentation = GetComponent<PlayerTimeBombVisual>();
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
        _areaDamage.Reset();
        if (_hasExplosionParameter) _animator.SetBool(ExplosionHash, false);
        if (_bombVisual != null && _bombVisual != gameObject) _bombVisual.SetActive(true);
        if (_presentation != null) _presentation.BeginFuse(_fuseSeconds, _radius);
        if (_bodyCollider != null) _bodyCollider.enabled = true;
        if (_bodyCollision != null)
        {
            _bodyCollision.enabled = true;
            _bodyCollision.AllowInitialExit();
        }
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (!_exploded && _presentation != null) _presentation.UpdateFuse(_elapsed, _fuseSeconds, _radius);
        if (!_exploded && _elapsed >= _fuseSeconds) Explode();
        if (_exploded && _elapsed >= _fuseSeconds + _effectSeconds) Destroy(gameObject);
    }

    public bool Explode()
    {
        if (_exploded || !isActiveAndEnabled) return false;
        _exploded = true;
        _elapsed = Mathf.Max(_elapsed, _fuseSeconds);
        // 연출이 끝날 때까지 보이지 않는 본체가 길을 막지 않도록 즉시 해제합니다.
        if (_bodyCollision != null) _bodyCollision.enabled = false;
        if (_bodyCollider != null) _bodyCollider.enabled = false;
        if (_hasExplosionParameter) _animator.SetBool(ExplosionHash, true);
        if (_presentation != null) _presentation.PlayExplosion(_radius, _effectSeconds);
        _areaDamage.Apply(transform.position, _radius, _damage);
        return true;
    }
}
