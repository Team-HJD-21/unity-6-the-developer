using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using TeamHJD.Game.Turrets;
using Unity.Properties;
using Unity.VisualScripting;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SearchService;
using UnityEngine.UI;

/*
 *  InGame 내에서 Wave에 대한 정보와 보상 관련 정보를 다루는 스크립트입니다.
 *  GameManager에서 모든 일을 담당할 수 없기에 스크립트를 분리했습니다.
 */

public class InGameManager : MonoBehaviour
{
    [Header("Status")] 
    public int curWave;
    public int maxWave;
    public int curSceneId;

    //  웨이브 중인지 판단하는 변수
    [Space]
    [Header("Wave")] 
    public bool isWave;
    public bool spawnEnd;
    public bool isClear;

    //  Wave 진행 버튼
    [Space]
    public GameObject waveStart;
    public GameObject startWrapper;
    public GameObject waveStartText;

    //  Wave 출력 Text
    [Space]
    [Header("Wave Info")] 
    public TMP_Text waveInfo;
    public GameObject waveWrapper;

    //  Wave 몬스터 수
    [Space]
    public int maxSpawn;
    public int dieSpawn;
    public int curSpawn;

    //  Wave 몬스터 수
    // public int dieBossSpawn;
    // public int curBossSpawn;
    [Space]
    public bool isBossWave;

    [Space]
    [Header("Talk Management")]
    //  대화창 끝남을 확인
    private bool talkEnd;

    //  대화 중 움직임 차단
    public bool isTalking;

    //  현재 대화의 진전도
    private int talkIdx;

    [Space]
    [Header("Talk UIs")] public GameObject talkWrapper;
    public GameObject talkBox;
    public TMP_Text talkText;

    [Space]
    [Header("Pause and Setting")] public GameObject pauseSet;
    public bool pauseVisible;
    public GameObject settings;
    public bool settingVisible;
    public GameObject operationKey;
    public bool operationKeyVisible;
    public GameObject blind;

    [Space]
    [Header("Pause Buttons")] public GameObject goToStageMenu;
    public GameObject restartButton;

    [Space]
    [Header("Stage Clear")] public GameObject stageClearWrapper;
    public TMP_Text stageClearText;
    public TMP_Text stageContentText;
    public TMP_Text stageCoinText;
    public GameObject stageCoinImage;
    public Button clearGoToMain;
    public Button clearGoToStageSelect;
    public Button clearRestart;

    [Space]
    [Header("Player")] 
    public GameObject player;
    public GameObject playerSpawnPoint;

    [Space]
    [Header("Player Bomb")] 
    public TMP_Text bombCount;
    public GameObject bombImage;

    [Space]
    [Header("Monster Spawners")] 
    public MonsterSpawner[] monsterSpawners;
    
    [Space]
    [Header("Boss Animation")] 
    public GameObject bossAnimationManager;

    [Space]
    [Header("Game Over")] 
    public bool isGameOver;
    public float clear;

    private void Start()
    {
        isGameOver = false;
        clear = 0f;
        
        //  Pause, Settings
        pauseVisible = false;
        settingVisible = false;

        //  Wave를 위한 Static 호출
        StageInfoManager.SetStageInfo();
        StageInfoManager.SetWaveInfo();

        curWave = 1;
        maxWave = StageInfoManager.GetStageInfo();
        isWave = false;
        isClear = false;

        // AudioManager.Instance.sfxChannelIndex = 20;
        talkEnd = false;
        isTalking = false;

        //  Button 연결
        goToStageMenu.GetComponent<Button>().onClick.AddListener(() => SceneController.ChangeScene("StageMenu"));
        restartButton.GetComponent<Button>().onClick.AddListener(() => SceneController.Instance.ReStartGame());

        //  진전도 초기화
        talkIdx = 1;

        //  진전도 표시
        // if (SceneController.Instance.curSelectStage + 1 >= DataManager.CurStage)
        // {
            // Debug.Log("Talk Process!!");
            talkWrapper.GetComponent<Animator>().SetBool("isShow", true);
            StartCoroutine(TalkProcess());
        // }
        // else
        // {
        //     ShowButton();
        //     ShowAlerts();
        //     talkIdx++;
        // }

        //  Stage Clear Button 연결
        clearGoToMain.onClick.AddListener(() => SceneController.ChangeScene("Main"));
        clearGoToStageSelect.onClick.AddListener(() => SceneController.ChangeScene("StageMenu"));
        clearRestart.onClick.AddListener(() => SceneController.Instance.ReStartGame());

        //  player 검색
        if (player == null)
        {
            player = GameObject.Find("Player");
        }

        if (playerSpawnPoint == null)
        {
            playerSpawnPoint = GameObject.Find("PlayerSpawnPoint");
        }
        
        //  Bomb Count init
        bombCount.SetText(player.GetComponent<Player>().maxBombCount.ToString());
    }

    //  대화 중에는 스킵 불가능
    private void Update()
    {
        if (!isTalking)
        {
            CheckKeyBoardInput();
        }

        if (isWave)
        {
            CheckWaveClear();
        }

        if (!spawnEnd)
        {
            CheckCurSpawn();
        }
    }

    //  Blind를 통해 다른 UI 클릭을 방지한다.
    private void CheckKeyBoardInput()
    {
        if (GameInput.WasPressedThisFrame(GameKey.Escape))
        {
            if (settingVisible)
            {
                settingVisible = false;
                settings.SetActive(settingVisible);
            }
            else if (operationKeyVisible)
            {
                operationKeyVisible = false;
                operationKey.SetActive(operationKeyVisible);
            }
            else
            {
                pauseVisible = !pauseVisible;
                pauseSet.SetActive(pauseVisible);
                blind.SetActive(pauseVisible);

                if (pauseVisible)
                {
                    AudioManager.Instance.StopAllSfx();
                    Time.timeScale = 0f;
                }
                else
                {
                    Time.timeScale = 1f;
                }
            }
        }
    }

    public void ShowSettings()
    {
        settingVisible = true;

        settings.SetActive(settingVisible);

        GeneralManager.Instance.settingManager.AllocateSetting();
    }

    public void ShowOperationKey()
    {
        operationKeyVisible = true;
        operationKey.SetActive(operationKeyVisible);
    }

    private IEnumerator TalkProcess()
    {
        CheckSceneId();

        isTalking = true;
        int idx = 0;

        talkWrapper.SetActive(true);

        while (true)
        {
            string buff = TalkManager.GetTalk(curSceneId + talkIdx, idx);

            if (buff == null)
            {
                break;
            }

            //  이 코루틴이 종료될 때까지 대기후 idx++ (연산 오류 발생 방지)
            yield return StartCoroutine(TalkCoroutine(buff));
            idx++;

            yield return new WaitUntil(() => GameInput.WasPressedThisFrame(GameKey.Space));
        }

        talkIdx++;
        talkEnd = true;
        isTalking = false;

        talkWrapper.GetComponent<Animator>().SetBool("isShow", false);

        if (curWave != maxWave)
        {
            ShowAlerts();
            ShowButton();
        }
    }

    private IEnumerator TalkCoroutine(string buff)
    {
        talkText.text = "";
        StringBuilder stringBuilder = new StringBuilder();

        for (int i = 0; i < buff.Length; i++)
        {
            stringBuilder.Append(buff[i]);
            talkText.text = stringBuilder.ToString();
            yield return new WaitForSeconds(0.005f);
        }
    }

    private void CheckSceneId()
    {
        //  현재 씬 분석
        int cnt = 1;
        foreach (var e in SceneController.stageList)
        {
            if (SceneController.NowScene == e)
            {
                curSceneId = 100 * cnt;
                break;
            }

            cnt++;
        }
    }

    public void ListenMonsterDie()
    {
        dieSpawn++;
    }

    public void ListenMonsterSpawn()
    {
        curSpawn++;
    }

    public void CheckCurSpawn()
    {
        if (curSpawn > StageInfoManager.GetWaveInfo(curWave))
        {
            spawnEnd = true;
        }
    }

    // public void ListenBossDie()
    // {
    //     Debug.LogWarning("BossDie");
    //     dieBossSpawn++;
    // }

    public void ListenBossSpawn()
    {
        float bgmVolume = AudioManager.Instance.bgmVolume;
        
        // Debug.LogWarning("BossSpawn");
        
        talkWrapper.GetComponent<Animator>().SetBool("isShow", true);
        StartCoroutine(TalkProcess());

        StartCoroutine(BossSfxCoroutine(bgmVolume));
    }

    public IEnumerator BossSfxCoroutine(float bgmVolume)
    {
        AudioManager.Instance.bgmVolume = AudioManager.Instance.sfxVolume * 0.04f;
        
        yield return new WaitForSeconds(7f);
        
        AudioManager.Instance.bgmVolume = bgmVolume;
    }

    public void StartWave()
    {
        //  오작동 방지
        if (GameInput.WasPressedThisFrame(GameKey.Space))
        {
            return;
        }
        
        InitWave();

        if (curWave == maxWave)
        {
            // Debug.Log("Entering Boss Stage: Deactivating all towers and activating BossAnimationManager.");

            StopAllTurrets();

            // BossAnimationManager 활성화
            if (bossAnimationManager != null)
            {
                bossAnimationManager.SetActive(true);
                // Debug.Log("BossAnimationManager 활성화됨.");
            }
            else
            {
                // Debug.LogError("BossAnimationManager가 할당되지 않았습니다.");
            }
            
            isBossWave = true;
            //  Spawner ON (wave 1, 2)
        }
        

        // Debug.Log("Wave Start");
        spawnEnd = false;
        
        StartCoroutine(ShowInfo()); // 코루틴으로 호출
        
        if (curWave < maxWave - 1 || curWave == maxWave)
        {
            HideAlerts();
            isWave = true;
        }
        else if(curWave == maxWave - 1)
        {
            //  Boss Spawn 여기서 함
            HideAlertAtFinalWave();
            isWave = true;
        }
        
        HideButton();
    }

    private void UpdateMiniMapElement(GameObject tower, bool isActive)
    {
        if (tower == null)
        {
            // Debug.LogWarning("Tower is null. Cannot update minimap element.");
            return;
        }
    
        // 타워의 모든 자식 중 "MapElement" 태그를 가진 오브젝트 찾기
        Transform[] children = tower.GetComponentsInChildren<Transform>();
        foreach (Transform child in children)
        {
            if (child.CompareTag("MapElement"))
            {
                SpriteRenderer spriteRenderer = child.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = isActive ? Color.green : Color.yellow;
                    // Debug.Log($"MiniMap Element '{child.name}' color set to {(isActive ? "green" : "yellow")}.");
                }
                else
                {
                    // Debug.LogWarning($"MapElement '{child.name}' does not have a SpriteRenderer.");
                }
            }
        }
    }

    private void InitWave()
    {
        maxSpawn = StageInfoManager.GetWaveInfo(curWave);
        dieSpawn = 0;
        curSpawn = 0;
        
        // dieBossSpawn = 0;
        // curBossSpawn = 0;
        isClear = false;
        isBossWave = false;
    }

    private void CheckWaveClear()
    {
        // 만약 현재 웨이브가 마지막 웨이브(보스 웨이브)이고, 보스를 모두 처치했다면 스테이지 클리어
        if (dieSpawn == curSpawn && curWave == maxWave && isBossWave && curSpawn > 0)
        {
            AudioManager.Instance.StopAllSfx();
            // Debug.Log("Wave is End");
            // Debug.Log("curWave3: " + curWave);
            // Debug.Log("maxWave3: " + maxWave);
            if (!isClear)
            {
                // Debug.Log("isClear 진입");
                clear += 0.2f;
                if (CheckStageClear())
                {
                    // Debug.Log("isStageClear 진입");
                    isClear = true;
                }
            }

            isWave = false;
        }
        // 보스 웨이브가 아닌 일반 웨이브 클리어 처리
        else if (dieSpawn == curSpawn && spawnEnd && curWave < maxWave && !isBossWave)
        {
            if (!isClear)
            {
                if (!CheckStageClear())
                {
                    isClear = true;

                    // Debug.Log("Wave ++");
                    curWave++;
                    ShowWaveClear();

                    //  Player 회복
                    GameManager.Instance.player.GetComponent<PlayerInfo>().RecoverHp();

                    if (curWave == maxWave - 1)
                    {
                        ShowAlertAtFinalWave();
                    }
                    else
                    {
                        ShowAlerts();
                    }
                    
                    StopAllTurrets();
                    StopAllSpawners();
                    clear += 0.2f;

                    StartCoroutine(MovePlayCoroutine());
                }
            }
            
            isWave = false;
        }
        
       
    }

    private void StopAllTurrets()
    {
        // 모든 타워 비활성화
        for (int i = 0; i < TowerManager.towerList.Count; i++)
        {
            GameObject towerObject = TowerManager.towerList[i];
            if (towerObject != null)
            {
                if (towerObject.TryGetComponent(out TurretBase turret))
                {
                    turret.RequestActivation(false);
                }

                // 미니맵 요소 색상 업데이트
                UpdateMiniMapElement(towerObject, false); // 비활성화 상태
            }
            else
            {
                // Debug.LogWarning($"Tower at index {i} in towerList is null.");
            }
        }
    }


    public void ShowAlerts()
    {
        for (int i = 0; i < monsterSpawners.Length; i++)
        {
            if (curWave == monsterSpawners[i].GetWaveId())
            {
                monsterSpawners[i].ShowAlert();
            }
        }
    }

    public void ShowAlertAtFinalWave()
    {
        for (int i = 0; i < monsterSpawners.Length; i++)
        {
            if (monsterSpawners[i].GetWaveId() == 2 || monsterSpawners[i].GetWaveId() == 3)
            {
                monsterSpawners[i].ShowAlert();
            }
        }
    }

    public void HideAlerts()
    {
        for (int i = 0; i < monsterSpawners.Length; i++)
        {
            monsterSpawners[i].HideAlert();
            ActivateFitWaveSpawner(monsterSpawners[i], false);
            if (curWave == monsterSpawners[i].GetWaveId())
            {
                ActivateFitWaveSpawner(monsterSpawners[i], true);
            }
            else
            {
                ActivateFitWaveSpawner(monsterSpawners[i], false);
            }
        }
    }

    public void StopAllSpawners()
    {
        for (int i = 0; i < monsterSpawners.Length; i++)
        {
            ActivateFitWaveSpawner(monsterSpawners[i], false);
        }
    }

    public void HideAlertAtFinalWave()
    {
        for (int i = 0; i < monsterSpawners.Length; i++)
        {
            monsterSpawners[i].HideAlert();
            
            if (monsterSpawners[i].GetWaveId() == 2 || monsterSpawners[i].GetWaveId() == 3)
            {
                ActivateFitWaveSpawner(monsterSpawners[i], true);
            }
            else
            {
                ActivateFitWaveSpawner(monsterSpawners[i], false);
            }
        }
    }

    public void ActivateFitWaveSpawner(MonsterSpawner spawner, bool work)
    {
        spawner.SetWorkable(work);
    }

    private IEnumerator MovePlayCoroutine()
    {
        //  대화중이라고 판정
        isTalking = true;
        yield return new WaitForSeconds(1f);

        MovePlayer();

        yield return new WaitForSeconds(1f);
        isTalking = false;
        isWave = false;
    }

    private void MovePlayer()
    {
        player.transform.position = new Vector3(playerSpawnPoint.transform.position.x,
            playerSpawnPoint.transform.position.y, player.transform.position.z);
    }

    private bool CheckStageClear()
    {
        if (!isGameOver)
        {
            if (curWave >= maxWave)
            {
                //  재 입장 방지용
                curWave = 1;

                // Debug.Log("Stage Clear");

                blind.SetActive(true);
                stageClearWrapper.SetActive(true);
                AudioManager.Instance.StopAllSfx();
                //  다음 스테이지 해금
                if (DataManager.CurStage <= SceneController.Instance.curSelectStage + 1)
                {
                    DataManager.CurStage++;
                }
            
                //  Synchronize
                GameManager.CurStage = DataManager.CurStage;

                int reward = CalculateCoin();
                StartCoroutine(CoinCoroutine(reward));
                return true;
            }
        }
        return false;
        //  TODO : 이후 스테이지 클리어 화면과 함께 스테이지 선택 씬으로 넘어갈 것.
    }

    private IEnumerator CoinCoroutine(int reward)
    {
        int cnt = 0;
        while (true)
        {
            if (reward == cnt)
            {
                Time.timeScale = 0f;
                yield break;
            }

            yield return new WaitForSeconds(0.01f);
            cnt++;
            stageCoinText.SetText("+ " + cnt);
        }
    }

    private int CalculateCoin()
    {
        int curCUHp = GeneralManager.Instance.towerManager.controlUnit.GetCurHp();
        int maxCUHp = GeneralManager.Instance.towerManager.controlUnit.GetMaxHp();

        float ratio = (float)curCUHp / maxCUHp;
        int reward = (int)(ratio * StageInfoManager.GetReward() * clear);

        // Debug.Log("Reward : " + reward);

        DataManager.Coin += reward;

        return reward;
    }

    public void GameOver()
    {
        if (!isGameOver)
        {
            isGameOver = true;
            // Debug.Log("Game Over");
            AudioManager.Instance.StopAllSfx();
            AudioManager.Instance.PlayBGM(AudioManager.Bgm.GameOver, true);
            
            int reward = CalculateCoin();
            
            blind.SetActive(true);
            stageClearWrapper.SetActive(true);

            stageClearText.SetText("Game Over");
            stageContentText.SetText("Mission failed.");
            
            StartCoroutine(CoinCoroutine(reward));
        }
    }

    private IEnumerator ShowInfo()
    {
        if (curWave == maxWave - 1)
        {
            waveInfo.SetText("Final Wave");
        }
        else if (curWave == maxWave)
        {
            yield return new WaitForSeconds(24f);
            
            isWave = true;
            waveInfo.SetText("Boss Wave");
        }

        else
        {
            waveInfo.SetText("Wave " + curWave);
        }

        waveWrapper.SetActive(true);

        StartCoroutine(ShowInfoCoroutine(false));
    }

    private void ShowWaveClear()
    {
        waveInfo.SetText("Wave Clear");
        waveWrapper.SetActive(true);

        StartCoroutine(ShowInfoCoroutine(true));
    }

    //  Animation은 Alpha값 조정으로 임시 결정
    private IEnumerator ShowInfoCoroutine(bool isClear)
    {
        var alpha = 0f;

        while (true)
        {
            if (alpha >= 1f)
            {
                StartCoroutine(HideInfoCoroutine());
                break;
            }

            alpha += 0.005f;
            waveInfo.color = new Color(waveInfo.color.r, waveInfo.color.g, waveInfo.color.b, alpha);
            yield return new WaitForSeconds(0.006f);
        }

        //  빠른 버튼 클릭으로 인한 버그 방지
        if (isClear)
        {
            ShowButton();
        }
    }

    private IEnumerator HideInfoCoroutine()
    {
        var alpha = 1f;
        while (true)
        {
            if (alpha <= 0)
            {
                waveWrapper.SetActive(false);
                yield break;
            }

            alpha -= 0.005f;
            waveInfo.color = new Color(waveInfo.color.r, waveInfo.color.g, waveInfo.color.b, alpha);
            yield return new WaitForSeconds(0.004f);
        }
    }

    private void ShowButton()
    {
        waveStart.GetComponent<Button>().interactable = true;
        startWrapper.GetComponent<Animator>().SetBool("visible", true);

        StartCoroutine(StartTextCoroutine());
    }

    private IEnumerator StartTextCoroutine()
    {
        while (true)
        {
            if (!waveStart.GetComponent<Button>().interactable)
            {
                yield break;
            }

            waveStartText.GetComponent<Animator>().SetBool("big", true);
            yield return new WaitForSeconds(1f);

            waveStartText.GetComponent<Animator>().SetBool("big", false);
            yield return new WaitForSeconds(1f);
        }
    }

    private void HideButton()
    {
        waveStart.GetComponent<Button>().interactable = false;
        startWrapper.GetComponent<Animator>().SetBool("visible", false);
    }

    //  Player Bomb
    public void ValidateBombCount(int count)
    {
        bombCount.SetText(count.ToString());
    }

    public void ChargeBombImage(int curTime, int maxTime)
    {
        float ratio = (float)curTime / maxTime;

        bombImage.GetComponent<Image>().fillAmount = ratio;
    }
}
