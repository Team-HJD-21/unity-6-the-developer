// 목록 선택·접힘·스크롤은 진단 영역별로 보존하는 Editor 표시 상태입니다.
using System;
using UnityEngine;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebugMonitorState
    {
        [NonSerialized] private GameDebugListCache _lists;
        public GameDebugListCache Lists => _lists ??= new GameDebugListCache();
        public Vector2 TurretScroll, RequestScroll, TimelineScroll;
        public long SelectedRequestSequence=-1, SelectedEventSequence=-1;
        public bool IsConnectionsExpanded=true, IsNotesExpanded, IsRequestDetailsExpanded=true, IsEventDetailsExpanded=true;
    }
}
