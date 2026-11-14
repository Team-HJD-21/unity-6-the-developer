using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>전체 육각막 아래에서 카메라를 교체하고 중앙 파동이 끝난 뒤 조작 UI를 공개합니다.</summary>
[DefaultExecutionOrder(-300)]
public sealed class TacticalModeEffect : MonoBehaviour
{
    public Image Surface;
    public CanvasGroup Commands;
    [Min(0)] public float CoverSeconds=.12f,HoldSeconds=.06f,EnterSeconds=.42f,ExitSeconds=.30f;
    public float Progress { get; private set; }
    public bool Opened { get; private set; }
    public bool Animating=>_phase!=0;
    public float Cover { get; private set; }
    public float Reveal { get; private set; }
    private Material _instance;
    private Canvas _canvas;
    private float _started,_coverFrom,_revealFrom,_modeFrom;
    private int _phase;
    private Action _covered,_completed;
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
        Opened=open;_covered=covered;_completed=completed;
        _coverFrom=Cover;_revealFrom=Reveal;_modeFrom=Progress;_started=Time.unscaledTime;_phase=1;
        if(Commands!=null){Commands.alpha=0;Commands.interactable=false;Commands.blocksRaycasts=false;}
        if(Surface!=null){Surface.enabled=true;Surface.raycastTarget=true;}
        if(_canvas!=null)_canvas.sortingOrder=800;
        Apply();
    }
    public void Reset(bool open)
    {
        _phase=0;_covered=_completed=null;Opened=open;Progress=open?1:0;Cover=0;Reveal=0;
        if(Surface!=null){Surface.enabled=open;Surface.raycastTarget=false;}
        if(Commands!=null){Commands.alpha=open?1:0;Commands.interactable=open;Commands.blocksRaycasts=open;}
        if(_canvas!=null)_canvas.sortingOrder=-900;Apply();
    }
    private void Update()
    {
        if(_phase==1)
        {
            float t=CoverSeconds<=0?1:Mathf.Clamp01((Time.unscaledTime-_started)/CoverSeconds);
            Cover=Mathf.Lerp(_coverFrom,1,Mathf.SmoothStep(0,1,t));Reveal=Mathf.Lerp(_revealFrom,0,Mathf.SmoothStep(0,1,t));
            if(t>=1){_phase=2;_started=Time.unscaledTime;var action=_covered;_covered=null;action?.Invoke();}
        }
        else if(_phase==2)
        {if(Time.unscaledTime-_started>=HoldSeconds){_phase=3;_started=Time.unscaledTime;}}
        else if(_phase==3)
        {
            float duration=Opened?EnterSeconds:ExitSeconds;
            float t=duration<=0?1:Mathf.Clamp01((Time.unscaledTime-_started)/duration);
            Reveal=Mathf.SmoothStep(0,1,t);Progress=Mathf.Lerp(_modeFrom,Opened?1:0,Reveal);
            if(t>=1)
            {
                _phase=0;Cover=0;Reveal=0;Progress=Opened?1:0;
                if(Surface!=null){Surface.enabled=Opened;Surface.raycastTarget=false;}
                if(_canvas!=null)_canvas.sortingOrder=-900;
                if(Commands!=null){Commands.alpha=Opened?1:0;Commands.interactable=Opened;Commands.blocksRaycasts=Opened;}
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
    }
    private void OnDestroy(){if(_instance!=null)Destroy(_instance);}
}
