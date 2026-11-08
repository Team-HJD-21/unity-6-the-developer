// 미사일 폭발 표현의 진행·제거를 필드 시간에 연결합니다.
using System.Collections;
using TeamHJD.Game.Presentation.Scene;
using System.Collections.Generic;
using UnityEngine;

public class Explode : MonoBehaviour
{
    public LayerMask layerMask;
    private Animator _animator;
    // Start is called before the first frame update
    void Start()
    {
        // Debug.Log("ExplodeisCalled");
        Collider2D hits = Physics2D.OverlapCircle(transform.position, 50, layerMask);
        if (hits != null)
        {
            float distance = Vector2.Distance(transform.position, hits.transform.position);
            // AudioManager.Instance.PlaySfx(AudioManager.Sfx.PlayerMine, distance, 50);
        }

        _animator = GetComponent<Animator>();
        StartCoroutine(DestroyExplosion());
    }

    public void TriggerExplosion()
    {
        // Debug.Log("ExplodeisTriggered");
        if (_animator != null)
        {
            _animator.SetBool("isExplode", true);
        }
        // Invoke("DestroyExplosion",0.1f);
        // StartCoroutine(DestroyExplosion());
    }

    private IEnumerator DestroyExplosion()
    {
        var simulation = GetComponent<FieldSimulationActor>();
        double end = (simulation?.Seconds ?? Time.timeAsDouble) + 0.8;
        while ((simulation?.Seconds ?? Time.timeAsDouble) < end) yield return null;
        Destroy(gameObject);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
