using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/*
 *  게임 전체를 관할하는 GameManager입니다.
 *  오브젝트로써의 역할도 필요하지만 static으로 선언해야 하는 method도 많으니 참고바랍니다.
 */

public enum AvailableLanguage
{
    English,
    Korean
};

public class GameManager : Singleton<GameManager>
{
    // [Header("Management")] 
    //  Load Game이 없어졌므로 삭제한다.
    // public static bool IsNewGame;
    
    public static bool TutorialEnd;
    
    [Header("Game")]
    public GameObject player;
    //  현재 플레이 가능한 스테이지 정보 (오류 방지를 위한 즉시 초기화)
    public static int CurStage;
    //  현재 선택하고 플레이 중인 스테이지 정보
    // public static int CurSelectedStage = 0;
    
    //  인게임 상태인지 확인
    public static bool InGame;
    //  인게임에서 필요한 모든 초기화가 가능한지 확인(true : 초기화 안됨, false : 초기화 됨)
    public static bool InGameInit;
    
    [Space]
    [Header("Loading")]
    //  로딩을 스킵할 수 있는지 확인
    public static bool LoadingSkip;

    [Space]
    [Header("Language")] public static AvailableLanguage SelectedLanguage = AvailableLanguage.English;

    [Space]
    [Header("Notice")] public static bool ReadNotice = false;

    private void Start()
    {
        //  다음 씬에서도 동일하게 유지하기 위함
        DontDestroyOnLoad(this.gameObject);
        
        //  게임 시작 시 초기화 목록
        InGame = false;
        InGameInit = false;
        
        CurStage = DataManager.CurStage;
        
        //  Audio Start
        AudioManager.Instance.PlayBGM(AudioManager.Bgm.StartingScene, true);
    }

    private void FixedUpdate()
    {
        //  For Debug
        if (!InGame)
        {
            foreach (var e in SceneController.stageList)
            {
                if (SceneManager.GetActiveScene().name == e)
                {
                    if (e != SceneController.stageList[3])
                    {
                        InGame = true;
                    }
                }
            }
        }
        
        //  인게임인지 확인
        if (InGame)
        {
            //  플레이어 재검색 (혹시 모를 오류 대비)
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    // Debug.LogError("No player found");
                }
            }
        }
    }

    private void Update()
    {
        //  지속해서 현재 씬이 Loading인지 확인 (또한 스킵이 가능한지 확인)
        if (SceneController.NowScene == "Loading" && LoadingSkip)
        {
            // AudioManager.Instance.PlayBGM(AudioManager.Bgm.StartingScene,false);
            // AudioManager.Instance.PlayBGM(AudioManager.Bgm.StageSelection,false);

            CheckSpaceKey();
        }
    }

    //  Loading 창에서 Space 입력 받기
    private void CheckSpaceKey()
    {
        if (GameInput.WasPressedThisFrame(GameKey.Space))
        {
            // Debug.Log("Press!");
            if (SceneController.NowScene == "Loading")
            {
                SceneController.LoadNextScene();
            }
            else return;
        }
    }

    public void NewGame()
    {
        // IsNewGame = true;
        
        //  디버깅할 때 true 바꾸면 튜토리얼 스킵 가능
        TutorialEnd = false;
    }

    public void SetLanguageSetting(int lang)
    {
        SelectedLanguage = (AvailableLanguage)lang;
    }
    
}
