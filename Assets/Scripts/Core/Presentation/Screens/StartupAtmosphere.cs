using System;
using TeamHJD.Game.Application;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    /// <summary>저장된 Main 장식만 재생합니다. 화면 읽기·텍스처 생성·검수 콜백은 Runtime에 사용하지 않습니다.</summary>
    public sealed class StartupAtmosphere : MonoBehaviour
    {
        public RectTransform Frame;
        public StartupControlsView Controls;
        public CreditsSceneLauncher Credits;
        public TMP_Text Logo;
        public Material LogoShine;
        public Image Meteor;
        public Sprite[] MeteorFrames;
        public Vector4 MeteorInk = new Vector4(0, 0, 1, 1);
        public RectTransform[] Protected = Array.Empty<RectTransform>();
        public CanvasGroup[] Stars = Array.Empty<CanvasGroup>();
        public Vector2[] StarSites = Array.Empty<Vector2>();
        public Vector2 MeteorInterval = new Vector2(7, 12);
        public Vector2 ShineInterval = new Vector2(22, 35);
        private ScreenManager _screens;
        private Material _original, _material;
        private System.Random _random;
        private readonly Vector3[] _corners = new Vector3[4];
        private Rect[] _occupied;
        private float _clock, _nextMeteor, _nextStar, _nextShine, _shineAt = -99;
        private float _meteorAt, _meteorLife, _meteorSize;
        private Vector2 _from, _to;
        private int _lastZone = -1;
        private bool _meteorActive, _paused;
        private float[] _starAt, _starLife, _starPeak;
        private bool[] _starActive;
        private static readonly int Sweep = Shader.PropertyToID("_MainSweep"), Strength = Shader.PropertyToID("_MainStrength");

        public void Bind(ScreenManager screens) => _screens = screens;
        private void OnEnable()
        {
            _random = new System.Random(unchecked(Environment.TickCount ^ GetInstanceID()));
            _occupied = new Rect[Protected.Length];
            _starAt = new float[Stars.Length]; _starLife = new float[Stars.Length];
            _starPeak = new float[Stars.Length]; _starActive = new bool[Stars.Length];
            _clock = 0; _nextMeteor = 2.3f; _nextStar = 4.1f; _nextShine = .5f; _shineAt = -99;
            if (Logo != null && LogoShine != null)
            {
                _original = Logo.fontSharedMaterial; _material = new Material(LogoShine);
                Logo.fontSharedMaterial = _material;
            }
            Quiet();
        }
        private float Random(float min, float max) => min + (max-min)*(float)_random.NextDouble();
        private void Quiet()
        {
            if (Meteor != null) Meteor.enabled = false;
            _meteorActive = false;
            for (int i=0;i<Stars.Length;i++) { Stars[i].alpha = 0; _starActive[i] = false; }
            if (_material != null) _material.SetFloat(Strength, 0);
        }
        private void Update()
        {
            bool pause = Controls == null || !Controls.IsRevealed || Credits != null && Credits.IsOpen || _screens?.Top != null;
            if (pause) { if (!_paused) Quiet(); _paused = true; return; }
            if (_paused) { _paused = false; _nextMeteor = _clock+2.3f; _nextStar = _clock+4.1f; }
            _clock += Time.unscaledDeltaTime;
            bool starVisible = false;
            for (int i=0;i<Stars.Length;i++)
            {
                if (!_starActive[i]) continue;
                float age=(_clock-_starAt[i])/_starLife[i];
                if (age>=1) { _starActive[i]=false; Stars[i].alpha=0; }
                else { float a=Mathf.Sin(Mathf.PI*age); Stars[i].alpha=_starPeak[i]*a*a; starVisible=true; }
            }
            float shine=(_clock-_shineAt)/.7f;
            bool reflecting=shine>=0 && shine<1;
            if (_material != null)
            {
                _material.SetFloat(Strength, reflecting?1:0);
                if (reflecting)
                {
                    Logo.rectTransform.GetWorldCorners(_corners);
                    float lo=RectTransformUtility.WorldToScreenPoint(null,_corners[0]).x/Screen.width-.2f;
                    float hi=RectTransformUtility.WorldToScreenPoint(null,_corners[2]).x/Screen.width+.2f;
                    _material.SetFloat(Sweep, Mathf.Lerp(lo,hi,shine));
                }
            }
            if (_meteorActive)
            {
                float age=(_clock-_meteorAt)/_meteorLife;
                if(age>=1) { _meteorActive=false; Meteor.enabled=false; _nextMeteor=_clock+Random(MeteorInterval.x,MeteorInterval.y); }
                else
                {
                    Meteor.rectTransform.anchoredPosition=Vector2.Lerp(_from,_to,age);
                    int frame=Mathf.Min(MeteorFrames.Length-1,(int)(age*MeteorFrames.Length));
                    Meteor.sprite=MeteorFrames[frame];Meteor.enabled=frame<MeteorFrames.Length-1;
                    float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.12f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-.7f)/.3f)));
                    Meteor.color=new Color(1,1,1,.75f*alpha);
                }
            }
            if (!_meteorActive && !starVisible && !reflecting && _clock>=_nextShine)
            { _shineAt=_clock;_nextShine=_clock+Random(ShineInterval.x,ShineInterval.y);return; }
            if (!_meteorActive && !starVisible && !reflecting && _clock>=_nextMeteor)
            {
                RefreshProtected();
                if(PickMeteor()) { _meteorActive=true;_meteorAt=_clock;Meteor.rectTransform.sizeDelta=Vector2.one*_meteorSize; }
                else _nextMeteor=_clock+2;
            }
            if (!_meteorActive && !reflecting && _clock<_nextMeteor-.6f && _clock>=_nextStar)
            {
                RefreshProtected();
                for(int i=0;i<Stars.Length;i++) if(!_starActive[i] && PickStar(i))
                { _starActive[i]=true;_starAt[i]=_clock;_starLife[i]=Random(1.2f,1.9f);_starPeak[i]=Random(.6f,.8f);break; }
                _nextStar=_clock+Random(.6f,1.35f);
            }
        }
        private void RefreshProtected()
        {
            for(int i=0;i<Protected.Length;i++)
            {
                if(Protected[i]==null) { _occupied[i]=default;continue; }
                Protected[i].GetWorldCorners(_corners);
                Vector2 lo=Frame.InverseTransformPoint(_corners[0]),hi=Frame.InverseTransformPoint(_corners[2]);
                _occupied[i]=Rect.MinMaxRect(Mathf.Min(lo.x,hi.x)-34,Mathf.Min(lo.y,hi.y)-34,Mathf.Max(lo.x,hi.x)+34,Mathf.Max(lo.y,hi.y)+34);
            }
        }
        private bool Safe(Rect bounds)
        {
            if(bounds.xMin < -952 || bounds.xMax > 952 || bounds.yMin < -532 || bounds.yMax > 532)return false;
            foreach(var rect in _occupied)if(rect.Overlaps(bounds))return false;
            return true;
        }
        private Rect Ink(Vector2 p,float size) => Rect.MinMaxRect(p.x+(MeteorInk.x-.5f)*size-4,p.y+(MeteorInk.y-.5f)*size-4,p.x+(MeteorInk.z-.5f)*size+4,p.y+(MeteorInk.w-.5f)*size+4);
        private bool PickMeteor()
        {
            for(int attempt=0;attempt<80;attempt++)
            {
                int zone=_lastZone<0?_random.Next(4):(_lastZone+1+_random.Next(3))%4;
                Vector2 origin=zone==0?new Vector2(220,500):zone==1?new Vector2(-310,495):zone==2?new Vector2(-740,-50):new Vector2(870,-30);
                var from=origin+new Vector2(Random(-35,35),Random(-12,12));
                float distance=zone==0?Random(195,245):zone==1?Random(185,235):zone==2?Random(135,170):Random(135,185),size=Random(112,156);
                var to=from+new Vector2(-distance,-distance*.83f);bool safe=true;
                for(int j=0;j<=24;j++)if(!Safe(Ink(Vector2.Lerp(from,to,j/24f),size))) { safe=false;break; }
                if(!safe)continue;
                _lastZone=zone;_from=from;_to=to;_meteorSize=size;_meteorLife=Random(.78f,1.04f);return true;
            }
            return false;
        }
        private bool PickStar(int index)
        {
            for(int attempt=0;attempt<80;attempt++)
            {
                var p=StarSites.Length>0?StarSites[_random.Next(StarSites.Length)]:new Vector2(Mathf.Round(Random(-890,890)),Mathf.Round(Random(-440,480)));
                if(!Safe(new Rect(p.x-10,p.y-10,20,20)))continue;
                bool near=false;
                for(int i=0;i<Stars.Length;i++)if(_starActive[i]&&Vector2.Distance(p,((RectTransform)Stars[i].transform).anchoredPosition)<180)near=true;
                if(near)continue;
                ((RectTransform)Stars[index].transform).anchoredPosition=p;return true;
            }
            return false;
        }
        private void OnDisable()
        {
            Quiet();_screens=null;
            if(Logo!=null&&Logo.fontSharedMaterial==_material)Logo.fontSharedMaterial=_original;
            if(_material!=null)Destroy(_material);_material=null;
        }
    }
}
