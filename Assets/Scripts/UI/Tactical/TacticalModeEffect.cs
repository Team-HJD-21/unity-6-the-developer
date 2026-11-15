using System;
using TeamHJD.Game.Presentation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>전체 육각막 아래에서 카메라를 교체하고 중앙 파동이 끝난 뒤 조작 UI를 공개합니다.</summary>
[DefaultExecutionOrder(-300)]
public sealed class TacticalModeEffect : MonoBehaviour
{
    public Image Surface;
    public CanvasGroup Commands;
    [Min(0)] public float CoverSeconds=.12f,HoldSeconds=.06f,EnterSeconds=.42f,ExitSeconds=.30f;
    [Range(1,2)] public float WaveTimeScale=1.25f;
    public float Progress { get; private set; }
    public bool Opened { get; private set; }
    public bool Animating=>_phase!=0;
    public float Cover { get; private set; }
    public float Reveal { get; private set; }
    public bool ContentIsMap { get; private set; }
    private Material _instance;
    private Canvas _canvas;
    private float _started,_coverFrom,_revealFrom,_modeFrom;
    private int _phase;
    private Action _covered,_completed;
    private IDisposable _hudSuppression;
    private Rect _revealViewport=new(0,0,1,1);
    private void Awake()
    {
        _canvas=GetComponent<Canvas>();
        if(Surface!=null && Surface.material!=null)
        {_instance=new Material(Surface.material){name="Tactical frame (Local)"};Surface.material=_instance;}
        Reset(false);
    }
    public void SetOpen(bool open,Action covered=null,Action completed=null)
    {
        if(Opened==open && !Animating){completed?.Invoke();return;}
        _hudSuppression??=HudPresentationVisibility.Suppress();
        bool wasMap=ContentIsMap;
        if(!open && !Animating){Cover=1;Reveal=1;}
        // 첫 전환 프레임부터 이전 필드를 차단합니다. 카메라 교체는 이 막 아래에서만 합니다.
        Cover=1;
        Opened=open;_covered=covered;_completed=completed;
        _coverFrom=Cover;_revealFrom=Reveal;_modeFrom=Progress;_started=Time.unscaledTime;_phase=1;
        if(Commands!=null){Commands.alpha=0;Commands.interactable=false;Commands.blocksRaycasts=false;}
        if(Surface!=null){Surface.enabled=true;Surface.raycastTarget=true;}
        if(_canvas!=null)_canvas.sortingOrder=800;
        if(open)
        {
            // 이미 지도인 역전 요청은 현재 파동에서 바로 이어갑니다.
            _phase=wasMap?3:2;
            Handoff();
        }
        else if(Reveal<=.001f)
        {
            // 지도가 아직 드러나지 않은 취소는 불필요한 안쪽 파동을 기다리지 않습니다.
            _phase=3;Handoff();
        }
        Apply();
    }
    public void SetRevealViewport(Rect viewport)
    {
        float x=Mathf.Clamp01(viewport.xMin),y=Mathf.Clamp01(viewport.yMin);
        _revealViewport=Rect.MinMaxRect(x,y,Mathf.Clamp(viewport.xMax,x,1),Mathf.Clamp(viewport.yMax,y,1));
        Apply();
    }
    public void Reset(bool open)
    {
        ReleaseHud();
        _phase=0;_covered=_completed=null;Opened=open;ContentIsMap=open;Progress=open?1:0;Cover=0;Reveal=0;
        if(!open)_revealViewport=new Rect(0,0,1,1);
        if(Surface!=null){Surface.enabled=open;Surface.raycastTarget=false;}
        if(Commands!=null){Commands.alpha=open?1:0;Commands.interactable=open;Commands.blocksRaycasts=open;}
        if(_canvas!=null)_canvas.sortingOrder=-900;Apply();
    }
    private void Update()
    {
        if(_phase==1)
        {
            float duration=ExitSeconds*Mathf.Max(1,WaveTimeScale);
            float t=duration<=0?1:Mathf.Clamp01((Time.unscaledTime-_started)/duration);
            float ease=TransitionEase(t);
            Cover=Mathf.Lerp(_coverFrom,1,ease);Reveal=Mathf.Lerp(_revealFrom,0,ease);
            if(t>=1){_phase=2;_started=Time.unscaledTime;Handoff();}
        }
        else if(_phase==2)
        {if(Time.unscaledTime-_started>=HoldSeconds){_phase=3;_started=Time.unscaledTime;_revealFrom=Reveal;_coverFrom=Cover;}}
        else if(_phase==3)
        {
            float duration=Opened?EnterSeconds*Mathf.Max(1,WaveTimeScale):CoverSeconds;
            float t=duration<=0?1:Mathf.Clamp01((Time.unscaledTime-_started)/duration);
            float ease=TransitionEase(t);
            Reveal=Opened?Mathf.Lerp(_revealFrom,1,ease):0;Cover=Opened?1:Mathf.Lerp(_coverFrom,0,ease);
            Progress=Mathf.Lerp(_modeFrom,Opened?1:0,ease);
            if(t>=1)
            {
                _phase=0;Cover=0;Reveal=0;Progress=Opened?1:0;
                if(Surface!=null){Surface.enabled=Opened;Surface.raycastTarget=false;}
                if(_canvas!=null)_canvas.sortingOrder=-900;
                if(Commands!=null){Commands.alpha=Opened?1:0;Commands.interactable=Opened;Commands.blocksRaycasts=Opened;}
                ReleaseHud();
                var done=_completed;_completed=null;done?.Invoke();
            }
        }
        Apply();
    }
    private void Apply()
    {
        if(_instance==null)return;
        _instance.SetFloat("_Progress",Progress);_instance.SetFloat("_Cover",Cover);_instance.SetFloat("_Reveal",Reveal);
        _instance.SetVector("_Resolution",new Vector4(Screen.width,Screen.height,0,0));
        _instance.SetVector("_RevealViewport",new Vector4(_revealViewport.xMin,_revealViewport.yMin,_revealViewport.xMax,_revealViewport.yMax));
    }
    private void Handoff()
    {
        ContentIsMap=Opened;
        var action=_covered;_covered=null;action?.Invoke();
    }
    // 5차 SmoothStep을 두 번 적용해 시작·도착의 감속을 더 분명하게 만듭니다.
    public static float TransitionEase(float t)
    {
        t=Mathf.Clamp01(t);
        double value=t;
        value=value*value*value*(value*(value*6-15)+10);
        return Mathf.Clamp01((float)(value*value*value*(value*(value*6-15)+10)));
    }
    private void ReleaseHud(){_hudSuppression?.Dispose();_hudSuppression=null;}
    private void OnDisable()=>Reset(false);
    private void OnDestroy(){ReleaseHud();if(_instance!=null)Destroy(_instance);}
}
