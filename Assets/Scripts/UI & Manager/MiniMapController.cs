using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 *  MiniMap 컨트롤러입니다.
 *  반드시 UIMiniMap Object에 포함되어야 합니다.
 *  isVisible은 토글로 각각의 버튼과 토글관계입니다.
 *  !Important 반드시 스크립트 뿐만 아니라 버튼의 작동 상태도 확인해야 합니다.
 */
public class MiniMapController : MonoBehaviour
{
    [Header("Handler")]
    public bool isVisible;
    
    [SerializeField] private GameObject mapInvisible;
    [SerializeField] private GameObject mapVisible;
    [SerializeField] private GameObject miniMap;
    
    private Animator _animator;
    private void Awake()
    {
        if (mapVisible == null || mapInvisible == null)
        {
            // Debug.LogError("Error : MiniMap Script에 버튼을 추가해주세요.");
        }
        
        //  애니메이터 변수 초기화
        if (miniMap == null)
        {
            // Debug.Log("MiniMap Object 찾는 중..");
            miniMap = GameObject.FindGameObjectWithTag("MiniMap");
        }
        _animator = miniMap.GetComponent<Animator>();
        
        //  보임 상태로 초기화
        SetVisible(true);
    }

    //  함수 재활용을 위한 함수 축약
    public void SetVisible(bool status)
    {
        //  키보드 입력으로 인해 MiniMap 오작동을 방지함.
        if (GameInput.IsPressed(GameKey.Space))
        {
            return;
        }
        
        // Debug.Log("Toggled!");
        isVisible = status;
        mapInvisible.SetActive(isVisible);
        mapVisible.SetActive(!isVisible);
        
        _animator.SetBool("isVisible", isVisible);
    }
}
