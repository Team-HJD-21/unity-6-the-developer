using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

/*
 *  Main Camera의 전반적인 부분을 다루는 스크립트입니다.
 *  인스펙터의 Camera를 만지기보다는 스크립트의 내용을 바꾸어 주세요.
 */
public class CameraController : MonoBehaviour
{
    // 타겟 지정
    public GameObject target;
    public Tilemap map;

    [Tooltip("Default : 0.125f")]
    [SerializeField] private float smoothSpeed = 0.125f;

    private float _cameraHalfHeight;
    private float _cameraHalfWidth;

    // 흔들림 관련 변수
    private bool isShaking = false; // 흔들림 중인지 확인
    private Vector3 originalPosition; // 흔들림 이전의 위치 저장

    private void Awake()
    {
        gameObject.GetComponent<Camera>().orthographicSize = 18f;
        map.CompressBounds();
    }

    private void Start()
    {
        if (target == null)
        {
            target = GameObject.FindGameObjectWithTag("Player");
        }

        _cameraHalfHeight = Camera.main.orthographicSize;
        _cameraHalfWidth = Camera.main.aspect * Camera.main.orthographicSize;
    }

    private void FixedUpdate()
    {
        if (GeneralManager.Instance.inGameManager.isWave)
        {
            // 흔들림 중이 아니면 카메라 이동
            if (!isShaking)
            {
                CameraMove();
            }
        }
        else
        {
            if (!GeneralManager.Instance.inGameManager.isTalking)
            {
                CameraMoveAlone();
            }
        }
    }

    private void CameraMove()
    {
        Bounds tilemapBounds = map.GetComponent<Renderer>().bounds;

        Vector3 desiredPosition = new Vector3(
            Mathf.Clamp(target.transform.position.x, tilemapBounds.min.x + _cameraHalfWidth, tilemapBounds.max.x - _cameraHalfWidth),
            Mathf.Clamp(target.transform.position.y, tilemapBounds.min.y + _cameraHalfHeight, tilemapBounds.max.y - _cameraHalfHeight),
            transform.position.z
        );

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
    }

    private void CameraMoveAlone()
    {
        Bounds tilemapBounds = map.GetComponent<Renderer>().bounds;
        
        Vector2 moveInput = GameInput.Move;
        float horizontal = moveInput.x;
        float vertical = moveInput.y;

        Vector3 desiredPosition = new Vector3(
            Mathf.Clamp(transform.position.x + horizontal * 2.5f, tilemapBounds.min.x + _cameraHalfWidth, tilemapBounds.max.x - _cameraHalfWidth),
            Mathf.Clamp(transform.position.y + vertical * 2.5f, tilemapBounds.min.y + _cameraHalfHeight, tilemapBounds.max.y - _cameraHalfHeight),
            transform.position.z
        );

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
    }

    // 흔들림 효과 구현
    public IEnumerator Shake(float duration, float magnitude)
    {
        isShaking = true; // 흔들림 시작
        originalPosition = transform.position; // 기존 위치 저장

        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = Random.Range(-1f, 1f) * magnitude;

            transform.position = new Vector3(originalPosition.x + offsetX, originalPosition.y + offsetY, originalPosition.z);

            elapsed += Time.deltaTime;

            yield return null;
        }

        transform.position = originalPosition; // 흔들림 종료 후 원래 위치 복원
        isShaking = false; // 흔들림 종료
    }
}
