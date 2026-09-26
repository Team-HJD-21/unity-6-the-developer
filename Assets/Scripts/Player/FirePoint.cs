using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirePoint : MonoBehaviour
{
    private Vector2 _mousePosition; // 마우스 위치 저장 변수
    public Transform playerpoint;
    private Vector3 oriPosition;
    // Start is called before the first frame update
    void Start()
    {
        oriPosition = playerpoint.position;
    }

    // Update is called once per frame
    void Update()
    {
        oriPosition = playerpoint.position;
        if (GeneralManager.Instance.inGameManager.isWave)
        {
            CheckDirectionToMouse();
        }
    }
    private void CheckDirectionToMouse()
    {
        // 마우스 위치를 월드 좌표로 변환
        _mousePosition = Camera.main.ScreenToWorldPoint(GameInput.PointerPosition);
        // 현재 위치와 마우스 위치 간의 방향 계산
        Vector2 direction = _mousePosition - (Vector2)playerpoint.position;
        // 방향의 각도를 계산
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 현재 오브젝트의 위치 가져오기

        Vector3 newPosition =new Vector3(0,0,0);
        
        // 각도에 따라 오브젝트 활성화
        if (angle > -45 && angle <= 45)
        {
            newPosition.x += 2f;
            transform.position = oriPosition + newPosition;
        }
        else if (angle > 45 && angle <= 135)
        {
            newPosition.y += 2f;
            transform.position = oriPosition + newPosition;
        }
        else if (angle > -135 && angle <= -45)
        {
            // 마우스가 아래쪽 방향에 있을 때 gundown 활성화
            newPosition.y -= 2f;
            transform.position = oriPosition + newPosition;
        }
        else
        {
            // 마우스가 왼쪽 방향에 있을 때 gunside 활성화
            newPosition.x-= 2f;
            transform.position = oriPosition + newPosition;
            
        }
    }
}
