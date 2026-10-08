// Editor session 안에서 domain reload를 넘어 유지하는 bounded 기록 묶음입니다.
// 앱 재시작/세션 종료의 영속 저장은 사용자의 JSON Export로 수행합니다.
using System;
using System.Collections.Generic;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebugArchive
    {
        public List<GameDebugRun> Runs = new List<GameDebugRun>();
    }
}
