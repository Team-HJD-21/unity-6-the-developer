using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WitchSound : MonoBehaviour
{
    public LayerMask layerMask;
    // Start is called before the first frame update
    private float _time;
    private String _witchSound;
    void Start()
    {
        // Debug.Log("ExplodeisCalled");
        Collider2D hits = Physics2D.OverlapCircle(transform.position, 50, layerMask);
        if (hits != null)
        {
            float distance = Vector2.Distance(transform.position, hits.transform.position);
            // AudioManager.Instance.PlaySfx(AudioManager.Sfx.WitchLaughing, distance, 50);
        }

        _time = 20f;
    }

    private void Update()
    {
        _time += Time.deltaTime;
        if(_time>20f)
        {
            Collider2D hits = Physics2D.OverlapCircle(transform.position, 50, layerMask);
            if (hits != null)
            {
                float distance = Vector2.Distance(transform.position, hits.transform.position);
                // _witchSound = AudioManager.Instance.PlaySfx(AudioManager.Sfx.WitchLaughing, distance, 50);
                _time = 0f;
            }
        }
        WitchSoundMove();
    }

    private void WitchSoundMove()
    {
        Collider2D hits = Physics2D.OverlapCircle(transform.position, 50, layerMask);
        if (hits != null&&_witchSound!=null)
        {
            float distance = Vector2.Distance(transform.position, hits.transform.position);
            // AudioManager.Instance.ChangeVolume(_witchSound,distance,50);
        }

    }
}
