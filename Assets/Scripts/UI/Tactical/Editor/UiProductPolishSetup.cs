using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>기존 프리팹에서 확인된 표시 차이만 교정합니다. 전체 Main/HUD 재생성은 하지 않습니다.</summary>
public static class UiProductPolishSetup
{
    [MenuItem("Tools/TeamHJD/The Developer/Apply UI product polish")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
        var style=AssetDatabase.LoadAssetAtPath<CombatHudStyle>("Assets/Art/UI/HudV6/CombatHudStyle.asset");
        Patch("Assets/Prefabs/UI/Common/SettingsScreen.prefab",root=>HudUiReviewSetup.ApplySettingsPreview(root,style));
        Patch("Assets/Prefabs/UI/Common/LoadingScreen.prefab",ApplyLoading);
        Patch("Assets/Prefabs/UI/PlayerSkillHUD.prefab",root=>
        {
            var header=root.GetComponentInChildren<CombatHudHeader>(true);
            header.OccupancyTitle=header.transform.Find("Territory Lettering")?.gameObject;
            header.OccupancyDivider=header.transform.Find("Separator")?.gameObject;
            header.HeaderBackplate=header.transform.Find("Header Backplate") as RectTransform;
            header.SetConfirmedOccupancy(-1,0);
        });
        Debug.Log("UI_PRODUCT_POLISH_AUTHORED");
    }
    public static void ApplyLoading(GameObject root)
    {
        var panel=root.transform.Find("Content/Controls") as RectTransform;
        if(panel==null)throw new InvalidOperationException("The authored loading controls are missing.");
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,490);
        Key("M",new Vector2(70,-325)); Caption("Tactical","전술 화면",new Vector2(150,-325));
        Key("G",new Vector2(710,-325)); Caption("Turret interaction","터렛 조작",new Vector2(790,-325));
        void Key(string letter,Vector2 position)
        {
            var rect=panel.Find("Key "+letter) as RectTransform;
            if(rect==null){rect=UnityEngine.Object.Instantiate(panel.Find("Key Q"),panel,false) as RectTransform;rect.name="Key "+letter;}
            rect.anchoredPosition=position; rect.GetComponentInChildren<TMP_Text>(true).text=letter;
        }
        void Caption(string name,string text,Vector2 position)
        {
            var rect=panel.Find(name) as RectTransform;
            if(rect==null){rect=UnityEngine.Object.Instantiate(panel.Find("Attack"),panel,false) as RectTransform;rect.name=name;}
            rect.anchoredPosition=position;rect.GetComponent<TMP_Text>().text=text;
        }
    }
    private static void Patch(string path,Action<GameObject> change)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try{change(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
