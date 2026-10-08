using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/*
 *  모든 Manager, Controller를 총괄하는 스크립트입니다.
 *  반드시 존재해야 하며 항상 존재해야합니다.
 *  [원칙] GM의 컴포넌트로 편입시킵니다. 또한 모든 매니저는 이 객체를 가집니다.
 */
public class GeneralManager : Singleton<GeneralManager>
{
    //  Global Managers ->  모두 GameManager가 가지고 있는 Components
    [Header("Global Manager")]
    public GameManager gameManager;
    public SceneController sceneController;
    public CursorManager cursorManager;
    public AudioManager audioManager;
    public SettingManager settingManager;
    
    //  InGame Managers (Local)
    [Space]
    [Header("In Game Manager")]
    public CameraController cameraController;
    public MiniMapController minimapController;
    public AlertManager alertManager;
    public InGameManager inGameManager;
    
    //  Shop Manager (Local)
    [Space]
    [Header("Shop Manager")]
    public ShopManager shopManager;
    
    //  UI Managements
    [Space]
    [Header("UI Managements")]
    public UICUInfo uiCUInfo;
    public UIPlayerHp uiPlayerHp;
    
    //  부가기능 Managers (Local)
    [Space]
    [Header("Local Manager")]
    public LoadingManager loadingManager;
    public SiteManager siteManager;
    public StageInfoManager stageInfoManager;
    public StageSelectManager stageSelectManager;

    //  Global Manager들은 프로그램 시작과 함께 할당
    private void Start()
    {
        //  GM과 SC는 GetComponent로 할당해도 무방
        gameManager = GameManager.Instance;
        sceneController = SceneController.Instance;
        cursorManager = GetComponent<CursorManager>();
        audioManager = AudioManager.Instance;
        settingManager = GetComponent<SettingManager>();

        // if (gameManager == null) Debug.LogError("GameManager 스크립트 오류");
        // if (sceneController == null) Debug.LogError("SceneController 스크립트 오류");
        // if (audioManager == null) Debug.LogError("AudioManager 스크립트 오류");
        // if (cursorManager == null) Debug.LogError("CursorManager 스크립트 오류");
        
        //  TalkManager
        StageInfoManager.SetPlanet();
        TalkManager.SetTalkData();
    }
    
    private void FixedUpdate()
    {
        FindManagers();
    }

    private void FindManagers()
    {
        if (GameManager.InGame) //  Static이라 자체 접근
        {
            FindInGameManagers();
            FindUIManagements();
        }
        else
        {
            DeallocateInGameManagers();
            DeallocateUIManagements();
        }
        
        AdditionalManagers();
    }

    private void FindInGameManagers()
    {
        cameraController = GameObject.Find("Main Camera").GetComponent<CameraController>();
        minimapController = GameObject.Find("UIMiniMap").GetComponent<MiniMapController>();
        alertManager = GameObject.Find("AlertManager").GetComponent<AlertManager>();
        inGameManager = GameObject.Find("InGameManager").GetComponent<InGameManager>();
        
        // if(cameraController == null) Debug.LogError("Camera Controller 스크립트 오류");
        // if(minimapController == null) Debug.LogError("MiniMap Controller 스크립트 오류");
        // if(alertManager == null) Debug.LogError("Alert Manager 스크립트 오류");
        // if(inGameManager == null) Debug.LogError("InGame Manager 스크립트 오류");
    }

    private void DeallocateInGameManagers()
    {
        cameraController = null;
        minimapController = null;
        alertManager = null;
    }

    private void FindUIManagements()
    {
        uiCUInfo = GameObject.Find("UICUPower").GetComponent<UICUInfo>();
        uiPlayerHp = GameObject.Find("UIPlayerHp").GetComponent<UIPlayerHp>();
        
        // if(uiCUInfo == null) Debug.LogError("UICUInfo Manager 스크립트 오류");
        // if(uiPlayerHp == null) Debug.LogError("UIPlayerHp Manager 스크립트 오류");
    }

    private void DeallocateUIManagements()
    {
        uiCUInfo = null;
        uiCUInfo = null;
    }

    //  Deallocate 기능도 함께 포함 -> GM에 속하는 컴포넌트(필요할 때마다 AddComponent함.)
    private void AdditionalManagers()
    {
        if (SceneController.NowScene == "Loading")
        {
            if (loadingManager == null)
            {
                loadingManager = gameObject.AddComponent<LoadingManager>();
            }
        }
        else
        {
            if (loadingManager != null)
            {
                Destroy(loadingManager);
            }
        }

        if (SceneController.NowScene == "Main")
        {
            if (siteManager == null)
            {
                siteManager = gameObject.AddComponent<SiteManager>();
            }
        }
        else
        {
            if (siteManager != null)
            {
                Destroy(siteManager);
            }
        }
        
        if (SceneController.NowScene == "StageMenu")
        {
            if (stageInfoManager == null)
            {
                stageInfoManager = gameObject.AddComponent<StageInfoManager>();
            }
        }
        else
        {
            if (stageInfoManager != null)
            {
                Destroy(stageInfoManager);
            }
        }
        
        //  얘는 추가가 아니다.
        if (SceneController.NowScene == "StageMenu")
        {
            if (stageSelectManager == null)
            {
                stageSelectManager = GameObject.Find("StageSelectManager").GetComponent<StageSelectManager>();
            }
        }
        else
        {
            stageInfoManager = null;
        }

        if (SceneController.NowScene == "StageMenu")
        {
            if (shopManager == null)
            {
                shopManager = GameObject.Find("ShopManager").GetComponent<ShopManager>();
            }
        }
        else
        {
            shopManager = null;
        }
    }
    
}
