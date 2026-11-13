// 내부 추가 출현의 실제 월드 예고를 기존 원형 Sprite로 표시합니다.
// Field 게임 시각으로만 점멸하고 Enemy/충돌체/전투 객체로 등록하지 않습니다.
// 예고 원은 실제 사각 생성 영역 전체를 감싸며 요청 종료/취소에 연결부가 제거합니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Enemies.Encounter
{
    internal sealed class EncounterWarningView : MonoBehaviour
    {
        private IMatchClock _clock;
        private SpriteRenderer _renderer;
        private double _started;

        public static EncounterWarningView Create(Sprite sprite, SpawnArea area, IMatchClock clock, Scene scene)
        {
            if (sprite == null || area == null || clock == null || !scene.IsValid() || !scene.isLoaded)
                throw new ArgumentException("A configured warning Sprite, area, Field clock and loaded Scene are required.");
            var root = new GameObject("Encounter Warning") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3((float)area.Center.X, (float)area.Center.Y, (float)area.VisualZ);
            double diameter = Math.Max(.5, Math.Sqrt(area.Width * area.Width + area.Height * area.Height));
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 30000;
            root.transform.localScale = new Vector3((float)(diameter / sprite.bounds.size.x), (float)(diameter / sprite.bounds.size.y), 1);
            var view = root.AddComponent<EncounterWarningView>();
            view._clock = clock; view._renderer = renderer; view._started = clock.ElapsedSeconds;
            view.Update();
            return view;
        }
        private void Update()
        {
            if (_clock == null || _renderer == null) return;
            float alpha = .6f + .25f * (float)Math.Sin((_clock.ElapsedSeconds - _started) * Math.PI * 4);
            _renderer.color = new Color(1, .3f, .08f, alpha);
        }
    }
}
