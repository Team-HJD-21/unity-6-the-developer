using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class playerguncontrol : MonoBehaviour
{
    public GameObject gunsideL;  // gunside 오브젝트 참조
    public GameObject gunsideR;  // gunside 오브젝트 참조
    public GameObject gunup;    // gunup 오브젝트 참조
    public GameObject gundown;  // gundown 오브젝트 참조
    public Transform playerpoint; // player의 좌표
    private Vector2 _mousePosition; // 마우스 위치 저장 변수
    private SpriteRenderer gunsideRenderer; // gunside의 SpriteRenderer

    // Start is called before the first frame update
    void Start()
    {
        // gunside의 SpriteRenderer를 가져오기
        if (gunsideL != null)
        {
            gunsideRenderer = gunsideL.GetComponent<SpriteRenderer>();
        }

        // 기본적으로 모든 오브젝트를 비활성화
        gunsideL.SetActive(false);
        gunsideR.SetActive(false);
        gunup.SetActive(false);
        gundown.SetActive(true); // 기본 상태로 gundown 활성화
    }

    // Update is called once per frame
    void Update()
    {
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

        // 각도에 따라 오브젝트 활성화
        if (angle > -45 && angle <= 45)
        {
            // 마우스가 오른쪽 방향에 있을 때 gunside 활성화
            ActivateGun(gunsideR);
        }
        else if (angle > 45 && angle <= 135)
        {
            // 마우스가 위쪽 방향에 있을 때 gunup 활성화
            ActivateGun(gunup);
        }
        else if (angle > -135 && angle <= -45)
        {
            // 마우스가 아래쪽 방향에 있을 때 gundown 활성화
            ActivateGun(gundown);
        }
        else
        {
            // 마우스가 왼쪽 방향에 있을 때 gunside 활성화
            ActivateGun(gunsideL);
            
        }
    }

    // 특정 오브젝트를 활성화하고 나머지는 비활성화하는 함수
    void ActivateGun(GameObject activeGun)
    {
        gunsideL.SetActive(activeGun == gunsideL);
        gunsideR.SetActive(activeGun == gunsideR);
        gunup.SetActive(activeGun == gunup);
        gundown.SetActive(activeGun == gundown);
    }
}
