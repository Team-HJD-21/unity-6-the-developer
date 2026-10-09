using System.Collections.Generic;
using UnityEngine;

public static class AStarPathfinder
{
    private static readonly Vector3Int[] Directions =
    {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down,
    };
    
    public static List<Vector3Int> FindPath(
        EnemyNavigationMap map,
        Vector3Int startCell,
        Vector3Int goalCell)
    {
        if (map == null)
            return null;

        var walkabilityCache = new Dictionary<Vector3Int, bool>();

        if (!CanOccupy(map, startCell, walkabilityCache) ||
            !CanOccupy(map, goalCell, walkabilityCache))
        {
            return null;
        }

        var openSet = new List<Vector3Int>(){startCell};
        var closedSet = new HashSet<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, int>()
        {
            [startCell] = 0
        };

        while (openSet.Count > 0)
        {
            Vector3Int currentCell =
                FindOpenCellWithLowestEstimatedTotalCost(
                    openSet,
                    gScore,
                    goalCell);

            if (currentCell == goalCell)
                return ReconstructPath(cameFrom, currentCell);
            
            openSet.Remove(currentCell);
            closedSet.Add(currentCell);

            foreach (Vector3Int direction in Directions)
            {
                Vector3Int neighborCell = currentCell + direction;

                if (closedSet.Contains(neighborCell) ||
                    !CanOccupy(map, neighborCell, walkabilityCache))
                {
                    continue;
                }
                
                int tentativeGScore = gScore[currentCell] + 10;

                if (gScore.TryGetValue(neighborCell, out int oldGScore) &&
                    tentativeGScore >= oldGScore)
                {
                    continue;
                }
                
                cameFrom[neighborCell] = currentCell;
                gScore[neighborCell] = tentativeGScore;
                
                if(!openSet.Contains(neighborCell))
                    openSet.Add(neighborCell);
            }

        }

        return null;
    }

    private static bool CanOccupy(
        EnemyNavigationMap map,
        Vector3Int cell,
        Dictionary<Vector3Int, bool> cache)
    {
        if (cache.TryGetValue(cell, out bool canOccupy))
            return canOccupy;

        canOccupy = EnemyPositionChecker.CanOccupy(map, cell);
        cache.Add(cell, canOccupy);
        return canOccupy;
    }

    private static Vector3Int FindOpenCellWithLowestEstimatedTotalCost(
        List<Vector3Int> openSet,
        Dictionary<Vector3Int, int> gScore,
        Vector3Int goalCell)
    {
        Vector3Int bestCell = openSet[0];
        int bestHScore = GetManhattanDistance(bestCell, goalCell);
        int bestFScore = gScore[bestCell] + bestHScore;

        for (int i = 1; i < openSet.Count; i++)
        {
            Vector3Int candidate = openSet[i];
            int hScore = GetManhattanDistance(candidate, goalCell);
            int fScore = gScore[candidate] + hScore;

            if (fScore < bestFScore ||
                (fScore == bestFScore && hScore < bestHScore))
            {
                bestCell = candidate;
                bestFScore = fScore;
                bestHScore = hScore;
            }
        }

        return bestCell;
    }

    private static int GetManhattanDistance(Vector3Int from, Vector3Int to)
    {
        return (Mathf.Abs(from.x - to.x) +
                Mathf.Abs(from.y - to.y)) * 10;
    }

    private static List<Vector3Int> ReconstructPath(
        Dictionary<Vector3Int, Vector3Int> cameFrom,
        Vector3Int goalCell)
    {
        var path = new List<Vector3Int>();
        Vector3Int currentCell = goalCell;
        
        path.Add(currentCell);

        while (cameFrom.TryGetValue(
                   currentCell,
                   out Vector3Int previousCell))
        {
            currentCell = previousCell;
            path.Add(currentCell);
        }
        
        path.Reverse();
        return path;
    }
}
