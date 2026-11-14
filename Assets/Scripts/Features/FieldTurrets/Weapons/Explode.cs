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
        TeamHJD.Game.Presentation.GameAudioManager.Play(TeamHJD.Game.Presentation.GameSound.Explosion, this);

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
