// 영역별 Snapshot 선택·필터·지도 탐색 상태를 보존합니다.
// 기록 자료나 실제 게임 상태는 소유하지 않으며 Match가 다른 요청 ID를 혼동하지 않습니다.
using System;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebugSpawnViewState
    {
        public string MatchId = "", MonsterFilter = "All", Search = "";
        public string SelectedPoint = "", SelectedEntity = "";
        public long SelectedRequestId = -1;
        public bool IsFollowingLatest = true;
        public float Zoom = 1;
        public Vector2 Pan;

        public void Validate()
        {
            MatchId ??= "";
            MonsterFilter ??= "All";
            Search ??= "";
            SelectedPoint ??= "";
            SelectedEntity ??= "";
            SelectedRequestId = Math.Max(-1, SelectedRequestId);
            Zoom = float.IsNaN(Zoom) || float.IsInfinity(Zoom) ? 1 : Mathf.Clamp(Zoom, 0.5f, 8);
            if (!IsFinite(Pan.x) || !IsFinite(Pan.y)) Pan = Vector2.zero;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
