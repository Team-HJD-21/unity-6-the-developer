// Scene 조립에 사용할 공용 Battlefield Grid 기본값을 저장합니다.
// Editor에서 수정한 설정은 새 Match에 적용되며, 실행 중 변경은 별도 오버라이드로 다룹니다.

using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Content.Authoring
{
    [CreateAssetMenu(menuName = "TeamHJD/Game/Battlefield Grid Settings")]
    public sealed class BattlefieldGridSettings : ScriptableObject
    {
        [SerializeField] private double _originX;
        [SerializeField] private double _originY;
        [SerializeField] private double _originZ;
        [SerializeField] private double _mapWidth = 16d;
        [SerializeField] private double _mapHeight = 16d;
        [SerializeField] private int _cellsX = BattlefieldGridConfiguration.DefaultCellsX;
        [SerializeField] private int _cellsY = BattlefieldGridConfiguration.DefaultCellsY;

        public BattlefieldGridConfiguration CreateConfiguration() =>
            new BattlefieldGridConfiguration(
                new BattlefieldPoint(_originX, _originY),
                _originZ,
                _mapWidth,
                _mapHeight,
                _cellsX,
                _cellsY);
    }
}
