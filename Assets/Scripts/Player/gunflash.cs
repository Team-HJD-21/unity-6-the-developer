using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gunflash : MonoBehaviour
{
    public GameObject flashObject; // 플래시 오브젝트
    public float flashDelay = 0.1f; // 공격 제한 시간
    public float flashDuration = 0.05f; // 플래시 활성화 시간
    public bool attackAble = true; // 공격 가능 여부
    public bool isFlashing; // 플래시 활성화 여부
    
    void Start()
    {
        Initialize();
    }

    void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        attackAble = true;
        isFlashing = false;

        // 플래시 오브젝트 비활성화
        if (flashObject != null)
        {
            flashObject.SetActive(false);
        }
    }

    void Update()
    {
        if (GameInput.IsPressed(GameKey.Space))
        {
            if (GeneralManager.Instance.inGameManager.isWave)
            {
                GunFlash();
            }
        }
    }

    private void GunFlash()
    {
        // 스페이스바 입력 시 플래시 동작
        if (!GeneralManager.Instance.inGameManager.isTalking)
        {
            if (attackAble && !isFlashing)
            {
                StartCoroutine(GunFlashCoroutine());
            }
        }
    }

    private IEnumerator GunFlashCoroutine()
    {
        attackAble = false; // 공격 제한
        isFlashing = true; // 플래시 활성화 중

        // 플래시 오브젝트 활성화
        if (flashObject != null)
        {
            flashObject.SetActive(true);
        }

        // flashDuration 동안 플래시 활성화 유지
        yield return new WaitForSeconds(flashDuration);

        // 플래시 오브젝트 비활성화
        if (flashObject != null)
        {
            flashObject.SetActive(false);
        }

        isFlashing = false; // 플래시 비활성화
        yield return new WaitForSeconds(flashDelay - flashDuration);

        attackAble = true; // 공격 가능 상태로 복원
    }
}
