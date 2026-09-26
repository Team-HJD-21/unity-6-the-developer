using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private Transform pos;
    public GameObject bullet;
    
    public float cooltime;
    private float curtime;
    private Transform _target;

    // Update is called once per frame
    void Update()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(GameInput.PointerPosition) - transform.position;
        
        float z = Mathf.Atan2(mousePos.y, mousePos.x) * Mathf.Rad2Deg;
        transform.rotation=Quaternion.Euler(0,0,z);
        if (curtime <= 0)
        {
            if (GameInput.IsPressed(GameKey.Space))
            {
                Instantiate(bullet, pos.position, Quaternion.Euler(0,0,z));
            }

            curtime = cooltime;
        }

        curtime -= Time.deltaTime;
    }
}
