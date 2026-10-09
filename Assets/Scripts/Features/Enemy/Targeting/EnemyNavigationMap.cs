using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 바닥 Tilemap과 고정 장애물 콜라이더를 기준으로 이동 가능 셀을 판정하고,
/// 월드 좌표와 셀 좌표 사이의 변환을 제공한다.
/// 선택된 상태에서는 주변 셀의 판정 결과를 Scene 뷰에 표시한다.
/// </summary>
public class EnemyNavigationMap : MonoBehaviour
{
    [Header("이동 가능 셀 판정에 사용할 Tilemap")]
    [SerializeField] private Tilemap groundTilemap; // 바닥이 있는 셀만 이동 후보가 된다.

    [Header("Scene 뷰 표시")]
    [SerializeField] private Transform debugCenter; // 비어 있으면 groundTilemap의 위치를 표시 영역의 중심으로 사용한다.
    [SerializeField, Range(1, 20)] private int debugRadius = 8; // 중심 셀에서 가로·세로로 확인할 범위.
    
    /// <summary>
    /// 월드 좌표를 groundTilemap의 셀 좌표로 변환한다.
    /// Tilemap이 없으면 변환 기준을 정할 수 없어 원점 셀을 반환한다.
    /// </summary>
    /// <param name="worldPosition">셀 좌표로 변환할 월드 위치.</param>
    /// <returns>월드 위치에 해당하는 셀 좌표. Tilemap이 없으면 원점 셀.</returns>
    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        if (groundTilemap == null)
            return Vector3Int.zero;

        // 셀 크기와 Tilemap의 배치를 반영해 월드 위치가 속한 셀을 찾는다.
        return groundTilemap.WorldToCell(worldPosition);
    }

    /// <summary>
    /// 셀 좌표를 해당 셀 중앙의 월드 좌표로 변환한다.
    /// Tilemap이 없으면 이 컴포넌트의 위치를 반환한다.
    /// </summary>
    /// <param name="cell">월드 좌표로 변환할 셀 좌표.</param>
    /// <returns>셀 중앙의 월드 위치. Tilemap이 없으면 이 컴포넌트의 위치.</returns>
    public Vector3 CellToWorld(Vector3Int cell)
    {
        if (groundTilemap == null)
            return transform.position;

        // 셀 중앙 좌표를 반환하므로 셀 중심에 오브젝트를 배치할 때 사용할 수 있다.
        return groundTilemap.GetCellCenterWorld(cell);
    }

    /// <summary>
    /// 콜라이더 쿼리에 사용할 셀의 월드 크기. 현재 맵은 회전하지 않은 직사각형 Grid다.
    /// </summary>
    public Vector2 CellSizeWorld => groundTilemap == null
        ? Vector2.zero
        : new Vector2(
            groundTilemap.cellSize.x * Mathf.Abs(groundTilemap.transform.lossyScale.x),
            groundTilemap.cellSize.y * Mathf.Abs(groundTilemap.transform.lossyScale.y));

    /// <summary>
    /// 바닥 타일이 있는지 확인한다. 장애물 콜라이더는 EnemyPositionChecker가 검사한다.
    /// </summary>
    /// <param name="cell">이동 가능 여부를 확인할 셀 좌표.</param>
    /// <returns>바닥 타일이 있으면 <see langword="true"/>.</returns>
    public bool IsWalkable(Vector3Int cell)
    {
        if (groundTilemap == null)
            return false;

        // Water Tilemap은 땅 아래에도 배치될 수 있으므로 바닥 타일만 기준으로 삼는다.
        return groundTilemap.HasTile(cell);
    }

    /// <summary>
    /// 오브젝트가 선택된 동안 중심 주변 셀의 이동 판정 결과를 Scene 뷰에 그린다.
    /// 초록색은 바닥이 있고 셀 전체에 고정 장애물 콜라이더가 닿지 않음을 나타낸다.
    /// 빨간색은 바닥이 없거나 셀에 장애물 콜라이더가 닿음을 나타낸다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (groundTilemap == null)
            return;

        Vector3 center = debugCenter != null
            ? debugCenter.position
            : groundTilemap.transform.position;

        Vector3Int centerCell = groundTilemap.WorldToCell(center);

        // 중심에서 가로·세로로 같은 범위를 순회해 원형이 아닌 정사각형 영역을 확인한다.
        for (int x = -debugRadius; x <= debugRadius; x++)
        {
            for (int y = -debugRadius; y <= debugRadius; y++)
            {
                Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                // Outline 경계가 셀을 조금이라도 지나가면 그 셀을 막힌 셀로 표시한다.
                bool walkable = EnemyPositionChecker.CanOccupy(this, cell);

                Gizmos.color = walkable ? Color.green : Color.red;

                Vector3 cellSize = groundTilemap.cellSize;

                // 2D 셀에도 와이어 큐브가 보이도록 z축에 작은 두께를 준다.
                cellSize.z = 0.01f;

                // 판정한 셀의 중앙에 셀 크기보다 조금 작은 테두리를 그려 인접 셀과 구분한다.
                Gizmos.DrawWireCube(
                    groundTilemap.GetCellCenterWorld(cell),
                    cellSize * 0.9f);
            }
        }
    }
}
