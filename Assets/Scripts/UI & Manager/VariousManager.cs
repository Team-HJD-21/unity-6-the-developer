using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class VariousManager : MonoBehaviour
{
    [Header("For Main")]
    public GameObject settingButtonMain;
    public GameObject settingsMain;
    public bool settingsMainVisible;

    public GameObject aboutUsButtonMain;
    public GameObject aboutUsMain;
    public bool aboutUsMainVisible;
    
    public GameObject blindMain;

    public GameObject noticeMain;
    public GameObject noticeMainButton;

    public void Start()
    {
        if (settingButtonMain != null)
        {
            settingButtonMain.GetComponent<Button>().onClick.AddListener(ShowSettings);
        }

        if (aboutUsButtonMain != null)
        {
            aboutUsButtonMain.GetComponent<Button>().onClick.AddListener(ShowAboutUs);
        }

        if (GameManager.ReadNotice)
        {
            noticeMain.SetActive(false);
        }

        if (noticeMainButton != null)
        {
            noticeMainButton.GetComponent<Button>().onClick.AddListener((HideNotice));
        }
    }

    public void Update()
    {
        CheckKeyBoard();
    }

    public void CheckKeyBoard()
    {
        if (GameInput.WasPressedThisFrame(GameKey.Escape))
        {
            if (settingsMain != null && settingsMainVisible)
            {
                settingsMainVisible = false;
                blindMain.SetActive(false);
                settingsMain.SetActive(false);
            }

            if (aboutUsMain != null && aboutUsMainVisible)
            {
                aboutUsMainVisible = false;
                blindMain.SetActive(false);
                aboutUsMain.SetActive(false);
            }
        }
    }
    
    public void ShowSettings()
    {
        settingsMainVisible = true;
        blindMain.SetActive(true);
        settingsMain.SetActive(true);
    }

    public void ShowAboutUs()
    {
        aboutUsMainVisible = true;
        blindMain.SetActive(true);
        aboutUsMain.SetActive(true);
    }

    public void HideNotice()
    {
        GameManager.ReadNotice = true;
        noticeMain.SetActive(false);
    }
}
