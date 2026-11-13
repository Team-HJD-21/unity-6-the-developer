using System;
using UnityEngine;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    public enum CreditsChapterKind { Welding, Flight, Asteroids, TurretDrop, Drone }

    /// <summary>시각 에셋/카메라/효과 트랙만 소유하며 게임의 Player·Turret AI는 실행하지 않습니다.</summary>
    public sealed class CreditsCinematicChapter : MonoBehaviour
    {
        [Serializable] public sealed class Effect
        {
            public ParticleSystem System;
            public float Start, End;
            [NonSerialized] public bool Active;
        }
        [Serializable] public sealed class Sound
        {
            public AudioSource Source;
            public float Start, End, Volume = .1f, Attack = .04f, Release = .2f;
            [NonSerialized] public bool Active;
        }
        public CreditsChapterKind Kind;
        [Min(3)] public float Duration = 11;
        public Vector2 CameraFrom, CameraTo;
        public float ZoomFrom = 6, ZoomTo = 5, RollFrom, RollTo;
        public SpriteRenderer Hero, Vehicle, Tool, Drone, Contact, Glow;
        public SpriteRenderer Ground;
        public SpriteRenderer[] GroundLayers = Array.Empty<SpriteRenderer>();
        public Sprite[] HeroFrames, LookFrames;
        public Transform HeroRig, VehicleRig, DroneRig, ToolRig;
        public SpriteRenderer[] Flames = Array.Empty<SpriteRenderer>();
        public Transform[] Props = Array.Empty<Transform>();
        public Vector3[] PropStart = Array.Empty<Vector3>(), PropEnd = Array.Empty<Vector3>();
        public Vector3[] PropScale = Array.Empty<Vector3>();
        public Vector3[] PropVelocity = Array.Empty<Vector3>();
        public float[] PropSpin = Array.Empty<float>();
        public SpriteRenderer[] Shadows = Array.Empty<SpriteRenderer>();
        public Vector3[] ShadowScale = Array.Empty<Vector3>();
        public AnimationCurve DropCurve = new AnimationCurve(
            new Keyframe(0, 0, 0, 0), new Keyframe(.72f, .78f, 1.4f, 1.4f),
            new Keyframe(.92f, .985f, .25f, .25f), new Keyframe(1, 1, 0, 0));
        public Vector4 HeroVisor = new Vector4(.5f, .64f, .29f, .13f);
        public Transform WeldNozzle;
        public Renderer WeldingArc;
        public SpriteRenderer Workplate;
        private MaterialPropertyBlock _arcProperty;
        private MaterialPropertyBlock _plateProperty;
        public CanvasGroup NameGroup, RoleGroup, Eyes;
        [Min(0)] public float CaptionStart = 5.4f;
        public RectTransform[] EyeShapes = Array.Empty<RectTransform>();
        public RectTransform ScreenScan;
        public CreditsNameReveal NameReveal;
        public LineRenderer Beam;
        public Effect[] Effects = Array.Empty<Effect>();
        public Sound[] Sounds = Array.Empty<Sound>();
        public Transform[] Ambient = Array.Empty<Transform>();
        public Vector3[] AmbientStart = Array.Empty<Vector3>(), AmbientVelocity = Array.Empty<Vector3>();
        public SpriteRenderer BackgroundSlime;
        public Sprite[] BackgroundSlimeFrames = Array.Empty<Sprite>();
        public Vector2 SlimeFrom = new Vector2(13, 1.8f), SlimeTo = new Vector2(-13, 3.2f);
        public Vector2 SlimePass = new Vector2(1.2f, 8.5f);
        public float InternalFade { get; private set; }
        private MaterialPropertyBlock _property;
        private float _lastTime = -1;
        private static float Ease(float t) => CreditsCinematicDirector.Ease(t);
        private void OnEnable()
        {
            if(GroundLayers.Length>0){foreach(var layer in GroundLayers)ClampTile(layer);}
            else ClampTile(Ground);
        }
        private static void ClampTile(SpriteRenderer layer)
        {
            if(layer==null||layer.sprite==null)return;
            var rect=layer.sprite.rect;var texture=layer.sprite.texture;var sampling=new MaterialPropertyBlock();
            sampling.SetVector("_UvClamp",new Vector4((rect.x+.5f)/texture.width,(rect.y+.5f)/texture.height,(rect.xMax-.5f)/texture.width,(rect.yMax-.5f)/texture.height));layer.SetPropertyBlock(sampling);
        }

        public void Evaluate(float time, Camera camera, bool seek, bool playAudio = false)
        {
            float u = Ease(time / Duration);
            InternalFade = 0;
            var focus = Vector2.Lerp(CameraFrom, CameraTo, u);
            camera.transform.position = new Vector3(focus.x, focus.y, -50);
            camera.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(RollFrom, RollTo, u));
            camera.orthographicSize = Mathf.Lerp(ZoomFrom, ZoomTo, u);
            if (RoleGroup != null) RoleGroup.alpha = Ease((time-CaptionStart)/.8f)*(1-Ease((time-Duration+1.1f)/.4f));
            if (NameGroup != null) NameGroup.alpha = 1;
            if (Hero != null && HeroFrames.Length > 0) Hero.sprite = HeroFrames[(int)(time*6)%HeroFrames.Length];
            switch (Kind)
            {
                case CreditsChapterKind.Welding: Weld(time, camera); break;
                case CreditsChapterKind.Flight: Fly(time, camera); break;
                case CreditsChapterKind.Asteroids: Rocks(time); break;
                case CreditsChapterKind.TurretDrop: Drop(time, camera); break;
                case CreditsChapterKind.Drone: Greet(time, camera); break;
            }
            for (int i=0;i<Ambient.Length;i++)
            {
                Ambient[i].localPosition=AmbientStart[i]+AmbientVelocity[i]*time;
                Ambient[i].localRotation=Quaternion.Euler(0,0,i*71+time*(i%2==0?3:-4));
            }
            foreach (var effect in Effects)
            {
                if (effect.System == null) continue;
                bool on = time >= effect.Start && time < effect.End;
                if (seek)
                {
                    effect.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (time >= effect.Start) effect.System.Simulate(time-effect.Start, true, true, true);
                }
                else if (on && (!effect.Active || time < _lastTime || !effect.System.isPlaying))
                { effect.System.Clear(true); effect.System.Play(true); }
                else if (!on && effect.Active) effect.System.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                effect.Active = on;
            }
            foreach (var cue in Sounds)
            {
                if (cue.Source == null || cue.Source.clip == null) continue;
                bool on = playAudio && time >= cue.Start && time < cue.End;
                if (on && (!cue.Active || time < _lastTime))
                {
                    float age = time - cue.Start;
                    cue.Source.time = cue.Source.loop ? age % cue.Source.clip.length : Mathf.Min(age, cue.Source.clip.length - .01f);
                    cue.Source.Play();
                }
                if (!on && cue.Active) cue.Source.Stop();
                cue.Source.volume = on ? cue.Volume * Ease((time - cue.Start) / Mathf.Max(.01f, cue.Attack)) *
                    (1 - Ease((time - cue.End + cue.Release) / Mathf.Max(.01f, cue.Release))) : 0;
                cue.Active = on;
            }
            _lastTime = time;
        }
        private void HeroLight(float weld, float shine, float strength)
        {
            if (_property == null) _property = new MaterialPropertyBlock();
            Hero.GetPropertyBlock(_property);
            var rect = Hero.sprite.textureRect; var texture = Hero.sprite.texture;
            _property.SetVector("_SpriteRect", new Vector4(rect.x/texture.width, rect.y/texture.height, rect.width/texture.width, rect.height/texture.height));
            _property.SetVector("_VisorRegion", HeroVisor);
            _property.SetFloat("_WeldLight", weld);
            _property.SetFloat("_Shine", shine);
            _property.SetFloat("_ShineStrength", strength);
            Hero.SetPropertyBlock(_property);
        }
        private void Weld(float time, Camera camera)
        {
            bool portrait=time<3.8f;
            HeroRig.gameObject.SetActive(portrait);Vehicle.gameObject.SetActive(!portrait);
            if(Workplate!=null)Workplate.gameObject.SetActive(portrait);
            if(ToolRig!=null)ToolRig.gameObject.SetActive(false);if(Beam!=null)Beam.enabled=false;
            if(Contact!=null)Contact.enabled=false;
            HeroRig.localPosition=new Vector3(.008f*Mathf.Sin(time*3),-2.25f,0);
            HeroRig.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*3)*.25f);
            if(portrait){camera.transform.position=new Vector3(CameraFrom.x,CameraFrom.y,-50);camera.orthographicSize=ZoomFrom*Mathf.Lerp(1,.96f,Ease(time/3.4f));camera.transform.rotation=Quaternion.Euler(0,0,RollFrom);}
            else{camera.transform.position=new Vector3(CameraTo.x,CameraTo.y,-50);camera.orthographicSize=ZoomTo*Mathf.Lerp(1,.94f,Ease((time-4.2f)/5));camera.transform.rotation=Quaternion.Euler(0,0,RollTo+Ease((time-4.2f)/5)*.5f);}
            InternalFade=portrait?Ease((time-3.45f)/.35f):1-Ease((time-3.8f)/.55f);
            NameReveal.Fade(portrait?0:Ease((time-4.5f)/.65f));
            bool work=time>.65f&&time<3.35f;
            float pulse=work ? .1f+.38f*Mathf.Pow(Mathf.Abs(Mathf.Sin(time*29)*Mathf.Sin(time*13)),2) : 0;
            HeroLight(pulse, 0, 0);
            if(Glow!=null)Glow.enabled=false;
            var point=WeldNozzle!=null?WeldNozzle.position:new Vector3(-1.45f,-1.95f,0);
            foreach(var effect in Effects)if(effect.System!=null)effect.System.transform.position=point;
            if(WeldingArc!=null)
            {
                WeldingArc.gameObject.SetActive(portrait&&work);WeldingArc.transform.position=point;
                if(_arcProperty==null)_arcProperty=new MaterialPropertyBlock();
                _arcProperty.SetFloat("_Phase",time*53);_arcProperty.SetFloat("_Intensity",work?.6f+pulse:0);WeldingArc.SetPropertyBlock(_arcProperty);
            }
            if(Workplate!=null)
            {
                if(_plateProperty==null)_plateProperty=new MaterialPropertyBlock();
                var rect=Workplate.sprite.textureRect;var texture=Workplate.sprite.texture;
                var hit=Workplate.transform.InverseTransformPoint(point);var size=Workplate.sprite.bounds.size;
                _plateProperty.SetVector("_SpriteRect",new Vector4(rect.x/texture.width,rect.y/texture.height,rect.width/texture.width,rect.height/texture.height));
                _plateProperty.SetVector("_ContactRegion",new Vector4(hit.x/size.x+.5f,hit.y/size.y+.5f,.035f,.28f));
                _plateProperty.SetFloat("_ContactGlow",work?.3f+pulse:0);Workplate.SetPropertyBlock(_plateProperty);
            }
        }
        private void Fly(float time, Camera camera)
        {
            float enter = Ease(time/2.2f), leave = Ease((time-4.5f)/1.6f);
            // 기존 출발 시각 전에 짧게 후진하고 가속 중 원래 궤적으로 복귀한다. OST와 이름 cue는 유지한다.
            float pullback=Ease((time-3.75f)/.5f)*(1-Ease((time-4.5f)/.85f));
            float preparation=Ease((time-3.5f)/.25f)*(1-Ease((time-4.5f)/.45f));
            float thrust=Mathf.Lerp(1,.3f,preparation);
            VehicleRig.localPosition = new Vector3(Mathf.Lerp(-11,1,enter)+leave*27-pullback*.55f,.7f+.12f*Mathf.Sin(time*1.3f)+leave*2,0);
            VehicleRig.localRotation = Quaternion.Euler(0,0,Mathf.Lerp(-3,8,leave)+Mathf.Sin(time)*1.2f);
            float ignition = Ease((time-.2f)/1.1f);
            float wide=Ease((time-1.9f)/1.8f);
            camera.transform.position=Vector3.Lerp(new Vector3(VehicleRig.position.x+CameraFrom.x,VehicleRig.position.y+CameraFrom.y,-50),new Vector3(CameraTo.x,CameraTo.y,-50),wide);
            camera.orthographicSize=Mathf.Lerp(ZoomFrom,ZoomTo,wide);camera.transform.rotation=Quaternion.Euler(0,0,Mathf.Lerp(RollFrom,RollTo,wide));
            for (int i=0;i<Flames.Length;i++)
            {
                var flame=Flames[i];
                flame.color = new Color(1,1,1,ignition*Mathf.Lerp(1,.7f,preparation));
                flame.transform.localScale = new Vector3((1.4f+.09f*Mathf.Sin(time*31+i*1.7f)+.04f*Mathf.Sin(time*57+i)+leave*.7f)*ignition*thrust,(.59f+.035f*Mathf.Sin(time*23+i*2.2f))*Mathf.Lerp(1,.85f,preparation),1);
                if(_property==null)_property=new MaterialPropertyBlock();flame.GetPropertyBlock(_property);
                _property.SetFloat("_ExhaustMotion",1);_property.SetFloat("_ExhaustPhase",time*24+i*2.3f);flame.SetPropertyBlock(_property);
            }
            NameReveal.Fade(Ease((time-6.05f)/.2f));
            NameReveal.Reveal(Ease((time-6.05f)/.95f));
            if(Effects.Length>3)Effects[3].System.transform.position=new Vector3(Mathf.Lerp(-5,5,Ease((time-5.95f)/1)),.2f,0);
            if (NameGroup != null) NameGroup.alpha=1-Ease((time-Duration+1.2f)/.5f);
        }
        private void Rocks(float time)
        {
            for(int i=0;i<Props.Length;i++)
            {
                var velocity=i<PropVelocity.Length?PropVelocity[i]:new Vector3(-.1f,0,0);
                Props[i].localPosition=PropStart[i]+velocity*time;
                Props[i].localRotation=Quaternion.Euler(0,0,i*47+time*(i<PropSpin.Length?PropSpin[i]:2));
            }
            if(BackgroundSlime!=null)
            {
                float pass=Mathf.InverseLerp(SlimePass.x,SlimePass.y,time);
                var position=Vector2.Lerp(SlimeFrom,SlimeTo,pass);
                BackgroundSlime.transform.localPosition=new Vector3(position.x,position.y+.08f*Mathf.Sin(time*1.4f),0);
                BackgroundSlime.transform.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-18,24,pass));
                BackgroundSlime.enabled=time>=SlimePass.x&&time<SlimePass.y;
                if(BackgroundSlimeFrames.Length>0)BackgroundSlime.sprite=BackgroundSlimeFrames[(int)(time*6)%BackgroundSlimeFrames.Length];
            }
            NameReveal.Fade(Ease((time-3.45f)/.6f));
        }
        private void Drop(float time, Camera camera)
        {
            float shake=0;
            for(int i=0;i<Props.Length;i++)
            {
                float delay=1.05f+i*.85f, fall=Mathf.Clamp01((time-delay)/1.35f);
                float landed=time-delay-1.35f;
                Props[i].localPosition=Vector3.Lerp(PropStart[i],PropEnd[i],DropCurve.Evaluate(fall));
                float bounce=landed>=0 ? Mathf.Sin(landed*20)*Mathf.Exp(-landed*9)*.11f : 0;
                Props[i].localPosition+=Vector3.up*Mathf.Abs(bounce);
                Props[i].localScale=Vector3.Scale(PropScale[i],new Vector3(1+bounce*.3f,1-bounce*.6f,1));
                if(landed>=0 && landed<.5f)shake+=Mathf.Sin(landed*55)*Mathf.Exp(-landed*10)*.04f;
                if(i<Effects.Length)Effects[i].System.transform.position=PropEnd[i];
                if(i<Flames.Length){Flames[i].gameObject.SetActive(fall>.5f&&landed<.06f);Flames[i].transform.localScale=new Vector3((.42f+.04f*Mathf.Sin(time*43))*(1-Ease((fall-.8f)/.24f)),.38f,1);}
                if(i<Shadows.Length){float near=Ease(fall);Shadows[i].transform.localScale=ShadowScale[i]*Mathf.Lerp(.25f,1,near);Shadows[i].color=new Color(0,0,0,Mathf.Lerp(.04f,.28f,near));}
            }
            float approach=Ease((time-4.4f)/2);
            var focus=Vector2.Lerp(CameraFrom,CameraTo,approach);camera.transform.position=new Vector3(focus.x,focus.y,-50);camera.orthographicSize=Mathf.Lerp(ZoomFrom,ZoomTo,approach);
            camera.transform.position+=new Vector3(shake,shake*.5f,0);
            NameReveal.Fade(Ease((time-6)/.6f));
        }
        private void Greet(float time, Camera camera)
        {
            float close=Ease((time-3.3f)/3.1f);
            var focus=Vector2.Lerp(CameraFrom,CameraTo,close);camera.transform.position=new Vector3(focus.x,focus.y,-50);
            camera.orthographicSize=Mathf.Lerp(ZoomFrom,ZoomTo,close);
            DroneRig.localPosition=new Vector3(2,.7f+Mathf.Sin(time*2)*.035f,0);
            DroneRig.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*1.4f)*1.8f*(1-close));
            HeroRig.localPosition=new Vector3(-1.8f,-1.55f+.008f*Mathf.Sin(time*3),0);
            HeroRig.localRotation=Quaternion.Euler(0,0,-1.5f*Ease((time-2.25f)/.3f));
            if(time>2.5f && LookFrames.Length>0)Hero.sprite=LookFrames[(int)(time*6)%LookFrames.Length];
            float shine=Mathf.Clamp01((time-1.1f)/.65f);HeroLight(0,shine,time>=1.1f&&time<1.75f?.85f:0);
            if(Eyes!=null)Eyes.alpha=1-Ease((time-5.7f)/.45f);
            float blink=1-.92f*(1-Mathf.Clamp01(Mathf.Min(Mathf.Abs(time-1.9f),Mathf.Abs(time-4.4f))/.12f));
            foreach(var eye in EyeShapes)eye.localScale=new Vector3(1,blink,1);
            if(ScreenScan!=null){ScreenScan.gameObject.SetActive(time>5.7f&&time<6.45f);ScreenScan.anchoredPosition=new Vector2(0,Mathf.Lerp(41,-41,Mathf.Clamp01((time-5.7f)/.75f)));}
            NameReveal.Fade(Ease((time-6.35f)/.8f));
        }
        public void StopAudio()
        {
            foreach (var cue in Sounds) if(cue.Source!=null){cue.Source.Stop();cue.Active=false;}
        }
        private void OnDisable()
        {
            StopAudio();
            foreach(var e in Effects)if(e.System!=null){e.System.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);e.Active=false;}
            _lastTime=-1;
        }
    }
}
