using Tactics.Core.Board;

namespace Tactics.Core.Pathfinding;

/// <summary>
/// Domain-neutral deterministic Dijkstra mechanics. Callers own neighbour ordering,
/// traversal rules, and step costs so battle and adventure policies can evolve independently.
/// </summary>
internal static class DeterministicGridPathSearch
{
    public static IReadOnlyList<GridPoint> FindPath(
        GridPoint origin,
        GridPoint destination,
        Func<GridPoint, IEnumerable<GridPoint>> getNeighbours,
        Func<GridPoint, bool, bool> canEnter,
        Func<GridPoint, float> getStepCost)
    {
        ArgumentNullException.ThrowIfNull(getNeighbours);
        ArgumentNullException.ThrowIfNull(canEnter);
        ArgumentNullException.ThrowIfNull(getStepCost);
        if (origin == destination) return Array.Empty<GridPoint>();

        var frontier = new StablePriorityQueue();
        var cameFrom = new Dictionary<GridPoint, GridPoint>();
        var costSoFar = new Dictionary<GridPoint, float> { [origin] = 0f };
        frontier.Enqueue(origin, 0f);

        while (frontier.Count != 0)
        {
            GridPoint current = frontier.Dequeue();
            if (current == destination) break;

            foreach (GridPoint neighbour in getNeighbours(current))
            {
                bool isDestination = neighbour == destination;
                if (!canEnter(neighbour, isDestination)) continue;

                float nextCost = costSoFar[current] + getStepCost(neighbour);
                if (costSoFar.TryGetValue(neighbour, out float previousCost) && nextCost >= previousCost)
                    continue;

                costSoFar[neighbour] = nextCost;
                cameFrom[neighbour] = current;
                frontier.Enqueue(neighbour, nextCost);
            }
        }

        if (!cameFrom.ContainsKey(destination)) return Array.Empty<GridPoint>();

        var path = new List<GridPoint>();
        for (GridPoint current = destination; current != origin; current = cameFrom[current])
            path.Add(current);
        path.Reverse();
        return path;
    }

    /// <summary>Preserves the final Unity heap's equal-priority behavior.</summary>
    private sealed class StablePriorityQueue
    {
        private readonly List<QueueItem> _queue = new();

        public int Count => _queue.Count;

        public void Enqueue(GridPoint point, float priority)
        {
            _queue.Add(new QueueItem(point, priority));
            int childIndex = _queue.Count - 1;
            while (childIndex > 0)
            {
                int parentIndex = (childIndex - 1) / 2;
                if (_queue[childIndex].Priority >= _queue[parentIndex].Priority) break;
                (_queue[parentIndex], _queue[childIndex]) = (_queue[childIndex], _queue[parentIndex]);
                childIndex = parentIndex;
            }
        }

        public GridPoint Dequeue()
        {
            int lastIndex = _queue.Count - 1;
            QueueItem front = _queue[0];
            _queue[0] = _queue[lastIndex];
            _queue.RemoveAt(lastIndex);

            lastIndex--;
            int parentIndex = 0;
            while (true)
            {
                int childIndex = parentIndex * 2 + 1;
                if (childIndex > lastIndex) break;

                int rightChildIndex = childIndex + 1;
                if (rightChildIndex <= lastIndex &&
                    _queue[rightChildIndex].Priority < _queue[childIndex].Priority)
                    childIndex = rightChildIndex;
                if (_queue[parentIndex].Priority <= _queue[childIndex].Priority) break;

                (_queue[parentIndex], _queue[childIndex]) = (_queue[childIndex], _queue[parentIndex]);
                parentIndex = childIndex;
            }
            return front.Point;
        }

        private readonly record struct QueueItem(GridPoint Point, float Priority);
    }
}
