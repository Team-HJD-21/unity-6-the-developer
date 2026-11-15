// 표시 전용 거리장입니다. 확정 Snapshot을 변경하거나 중간 표시를 게임 Grid로 발행하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using UnityEngine;

public sealed class TerritoryExpansionState
{
    public enum Receipt { Ignored, Baseline, Metadata, Capture }
    public RegionGridSnapshot Snapshot { get; private set; }
    public float SweepSeconds = .65f, HoldSeconds = .12f, SettleSeconds = .28f, LossSeconds = .16f, MergeSeconds = .18f;
    public float BandWidth = 3.5f;
    public int Version { get; private set; }
    public Color[] Pixels { get; private set; }
    public Rect Bounds { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float MaximumArrival { get; private set; }
    public bool IsLoss { get; private set; }
    public double SweepEndsAt => _started + (IsLoss ? _loss : _sweep);
    public double EndsAt => _started + (IsLoss ? _loss : _sweep + _hold + _settle);
    public bool HasPending => _pending;
    private double _started = double.NegativeInfinity;
    private float _sweep, _hold, _settle, _loss, _margin, _dx, _dy;
    private int _samples, _padX, _padY;
    private long _noticeRevision = -1;
    private bool _pending;
    private RegionGridSnapshot _resting;
    private const float Far = 1e20f;

    public bool Animating(double now) => Pixels != null && now < EndsAt;
    public float Travel(double now) => Mathf.Lerp(-_margin, MaximumArrival + _margin,
        Ease((float)((now - _started) / Math.Max(.001, _sweep))));
    public float Blend(double now) => !Animating(now) ? 1 : IsLoss
        ? Ease((float)((now - _started) / Math.Max(.001, _loss)))
        : Ease((float)((now - _started - _sweep - _hold) / Math.Max(.001, _settle)));
    public float Glow(double now) => !Animating(now) || IsLoss ? 0 : 1 - Blend(now);
    public float CarryFade(double now) => !Animating(now) || IsLoss ? 0 : 1 - Ease((float)((now - _started) / Math.Max(.001, _settle)));
    public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }

    public Receipt Accept(RegionGridSnapshot next, double now)
    {
        if (next == null) { Reset(null); return Receipt.Baseline; }
        var previous = Snapshot;
        bool identity = previous != null && previous.MatchId == next.MatchId && previous.GridId == next.GridId && previous.Mode == next.Mode &&
            SameGeometry(previous.Definition.Geometry, next.Definition.Geometry);
        if (identity && next.Revision == previous.Revision) return Receipt.Ignored;
        if (!identity || next.Change.Kind == RegionChangeKind.Baseline)
        { Reset(next); return Receipt.Baseline; }
        if (next.Revision <= previous.Revision) return Receipt.Ignored;
        Snapshot = next;
        bool changed = false, lost = false;
        for (int i = 0; i < next.CapturedCells.Count; i++)
        { changed |= next.CapturedCells[i] != previous.CapturedCells[i]; lost |= previous.CapturedCells[i] && !next.CapturedCells[i]; }
        // 전송이 여러 revision을 합칠 수 있습니다. gap을 복원으로 추측하지 않고 Core의 Baseline 사유를 따릅니다.
        bool notice = next.Change.IsCaptureChange && next.Change.Revision > _noticeRevision && next.Change.Revision > previous.Revision;
        if (!notice)
        {
            if (changed) { Reset(next); return Receipt.Baseline; }
            return Receipt.Metadata;
        }
        _noticeRevision = next.Change.Revision;
        if (changed)
        {
            // 지나치게 긴 Grid는 거대 Texture 대신 정확한 기존 경계로 즉시 정착합니다.
            var grid = next.Definition.Geometry;
            if (grid.CellsX > 512 || grid.CellsY > 512) { Reset(next); return Receipt.Capture; }
            if (!lost && Animating(now) && now - _started < MergeSeconds) _pending = true;
            else Build(previous, now, lost);
        }
        return Receipt.Capture;
    }

    private static bool SameGeometry(BattlefieldGridConfiguration a, BattlefieldGridConfiguration b)
        => a.CellsX == b.CellsX && a.CellsY == b.CellsY && a.MapWidth == b.MapWidth && a.MapHeight == b.MapHeight &&
            a.Origin.X == b.Origin.X && a.Origin.Y == b.Origin.Y && a.OriginZ == b.OriginZ;

    public void Tick(double now)
    {
        if (_pending && now - _started >= MergeSeconds) Build(_resting, now, false);
    }

    public void Reset(RegionGridSnapshot snapshot)
    {
        Snapshot = _resting = snapshot; _noticeRevision = snapshot?.Change.Revision ?? -1;
        _pending = false; Pixels = null; Width = Height = 0; Bounds = default;
        _started = double.NegativeInfinity; MaximumArrival = 0; IsLoss = false; Version++;
    }

    private void Build(RegionGridSnapshot previous, double now, bool lost)
    {
        var grid = Snapshot.Definition.Geometry;
        bool continuing = Animating(now);
        var oldPixels = Pixels;
        float oldTravel = Travel(now), oldBlend = Blend(now), oldGlow = Glow(now), oldCarry = CarryFade(now);
        bool oldLoss = IsLoss;
        _samples = Math.Max(1, Math.Min(8, 512 / Math.Max(grid.CellsX, grid.CellsY)));
        _dx = (float)grid.CellWidth / _samples; _dy = (float)grid.CellHeight / _samples;
        _margin = Mathf.Max(_dx, _dy) + Mathf.Min(Mathf.Max(.001f, BandWidth), (float)Math.Min(grid.CellWidth, grid.CellHeight) * .45f) * 2;
        _padX = Mathf.CeilToInt(_margin / _dx) + 2; _padY = Mathf.CeilToInt(_margin / _dy) + 2;
        int width = grid.CellsX * _samples + _padX * 2, height = grid.CellsY * _samples + _padY * 2;
        bool reusable = oldPixels != null && Width == width && Height == height;
        Width = width; Height = height;
        Bounds = new Rect((float)grid.MinimumX - _padX * _dx, (float)grid.MinimumY - _padY * _dy, Width * _dx, Height * _dy);
        var target = Raster(Snapshot);
        var targetDistance = SignedDistance(target, Width, Height, _dx, _dy);
        float[] oldDistance;
        if (reusable)
        {
            oldDistance = new float[oldPixels.Length];
            for (int i = 0; i < oldDistance.Length; i++)
                oldDistance[i] = continuing ? Displayed(oldPixels[i], oldTravel, oldBlend, oldLoss) : oldPixels[i].g;
        }
        else oldDistance = SignedDistance(Raster(previous ?? Snapshot), Width, Height, _dx, _dy);
        bool added = false;
        for (int i = 0; i < Snapshot.CapturedCells.Count; i++)
            added |= Snapshot.CapturedCells[i] && !(previous ?? _resting ?? Snapshot).CapturedCells[i];
        IsLoss = lost && !added;
        // 동시 확보·상실은 잃은 면의 축하를 즉시 취소하고 남은 확장만 이어갑니다.
        if (lost && added) for (int i = 0; i < oldDistance.Length; i++) oldDistance[i] = Mathf.Min(oldDistance[i], targetDistance[i]);
        var visible = new bool[grid.TotalCellCount];
        for (int y = 0; y < grid.CellsY; y++) for (int x = 0; x < grid.CellsX; x++)
            visible[y * grid.CellsX + x] = oldDistance[(_padY + y * _samples + _samples / 2) * Width + _padX + x * _samples + _samples / 2] > 0;
        float[] arrivals = ArrivalDistances(Snapshot.CapturedCells, visible, grid.CellsX, grid.CellsY, (float)grid.CellWidth, (float)grid.CellHeight);
        Pixels = new Color[Width * Height]; MaximumArrival = 0;
        var resting = _resting ?? previous ?? Snapshot;
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        {
            int at = y * Width + x;
            float gx = (x - _padX + .5f) / _samples - .5f, gy = (y - _padY + .5f) / _samples - .5f;
            int cx = Mathf.Clamp(Mathf.FloorToInt(gx + .5f), 0, grid.CellsX - 1), cy = Mathf.Clamp(Mathf.FloorToInt(gy + .5f), 0, grid.CellsY - 1);
            int cell = cy * grid.CellsX + cx;
            float arrival = SampleArrival(arrivals, grid.CellsX, grid.CellsY, gx, gy);
            float glow = 0;
            if (target[at])
            {
                bool carried = continuing && reusable && oldPixels[at].a != 0;
                if (!resting.CapturedCells[cell] || (carried && oldDistance[at] <= 0)) glow = 1;
                else if (carried)
                    glow = -Mathf.Max(0, oldPixels[at].a >= 0 ? oldPixels[at].a * oldGlow : -oldPixels[at].a * oldCarry);
            }
            // 음수 A는 이미 드러난 땅의 잔광입니다. 새 파동이 와도 밝기를 다시 1로 올리지 않습니다.
            Pixels[at] = new Color(oldDistance[at], targetDistance[at], arrival, glow);
            if (target[at]) MaximumArrival = Mathf.Max(MaximumArrival, arrival);
        }
        _started = now; _sweep = Mathf.Max(.01f, SweepSeconds); _hold = Mathf.Max(0, HoldSeconds);
        _settle = Mathf.Max(.01f, SettleSeconds); _loss = Mathf.Max(.01f, LossSeconds);
        _pending = false; _resting = Snapshot; Version++;
    }

    public float DisplayedAtPixel(int index, double now)
        => Pixels == null ? 0 : !Animating(now) ? Pixels[index].g : Displayed(Pixels[index], Travel(now), Blend(now), IsLoss);
    private static float Displayed(Color pixel, float travel, float blend, bool loss)
        => loss ? Mathf.Lerp(pixel.r, pixel.g, blend) : Mathf.Max(pixel.r, Mathf.Min(pixel.g, travel - pixel.b));

    private bool[] Raster(RegionGridSnapshot snapshot)
    {
        var mask = new bool[Width * Height]; var g = snapshot.Definition.Geometry;
        for (int y = 0; y < g.CellsY; y++) for (int x = 0; x < g.CellsX; x++)
        {
            if (!snapshot.CapturedCells[y * g.CellsX + x]) continue;
            for (int yy = 0; yy < _samples; yy++) for (int xx = 0; xx < _samples; xx++)
                mask[(y * _samples + yy + _padY) * Width + x * _samples + xx + _padX] = true;
        }
        return mask;
    }

    private static float SampleArrival(float[] values, int width, int height, float x, float y)
    {
        x = Mathf.Clamp(x, 0, width - 1); y = Mathf.Clamp(y, 0, height - 1);
        int a = (int)x, b = (int)y, right = Math.Min(a + 1, width - 1), top = Math.Min(b + 1, height - 1);
        return Mathf.Lerp(Mathf.Lerp(values[b * width + a], values[b * width + right], x - a),
            Mathf.Lerp(values[top * width + a], values[top * width + right], x - a), y - b);
    }

    // 두 번의 1차원 거리 변환. 직사각형 셀도 실제 월드 XY 거리를 사용합니다.
    public static float[] SignedDistance(bool[] mask, int width, int height, float dx, float dy)
    {
        var inside = Distance(mask, true, width, height, dx, dy);
        var outside = Distance(mask, false, width, height, dx, dy);
        float half = Math.Min(dx, dy) * .5f;
        float limit = (width * dx + height * dy) * 2;
        for (int i = 0; i < inside.Length; i++)
            inside[i] = mask[i] ? Mathf.Min(limit, Mathf.Sqrt(outside[i])) - half : -Mathf.Min(limit, Mathf.Sqrt(inside[i])) + half;
        return inside;
    }

    private static float[] Distance(IReadOnlyList<bool> mask, bool seed, int width, int height, float dx, float dy, int[] nearest = null)
    {
        var result = new float[width * height];
        int length = Math.Max(width, height);
        var source = new float[length]; var output = new float[length]; var sites = new int[length]; var cuts = new double[length + 1];
        var selected = nearest != null ? new int[length] : null;
        var rowOwners = nearest != null ? new int[result.Length] : null;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) source[x] = mask[y * width + x] == seed ? 0 : Far;
            Transform(source, output, width, dx * dx, sites, cuts, selected);
            for (int x = 0; x < width; x++)
            {
                result[y * width + x] = output[x];
                if (nearest != null) rowOwners[y * width + x] = selected[x] < 0 ? -1 : y * width + selected[x];
            }
        }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++) source[y] = result[y * width + x];
            Transform(source, output, height, dy * dy, sites, cuts, selected);
            for (int y = 0; y < height; y++)
            {
                result[y * width + x] = output[y];
                if (nearest != null) nearest[y * width + x] = selected[y] < 0 ? -1 : rowOwners[selected[y] * width + x];
            }
        }
        return result;
    }

    private static void Transform(float[] input, float[] output, int count, double scale, int[] sites, double[] cuts, int[] selected = null)
    {
        int end = -1;
        for (int q = 0; q < count; q++)
        {
            if (input[q] >= Far * .5f) continue;
            double crossing = double.NegativeInfinity;
            while (end >= 0)
            {
                int p = sites[end];
                crossing = ((double)input[q] - input[p] + scale * ((double)q * q - (double)p * p)) / (2 * scale * (q - p));
                if (crossing > cuts[end]) break;
                end--;
            }
            end++; sites[end] = q; cuts[end] = end == 0 ? double.NegativeInfinity : crossing; cuts[end + 1] = double.PositiveInfinity;
        }
        if (end < 0)
        {
            for (int q = 0; q < count; q++) { output[q] = Far; if (selected != null) selected[q] = -1; }
            return;
        }
        int segment = 0;
        for (int q = 0; q < count; q++)
        {
            while (segment < end && cuts[segment + 1] < q) segment++;
            int delta = q - sites[segment]; output[q] = (float)(scale * delta * delta + input[sites[segment]]);
            if (selected != null) selected[q] = sites[segment];
        }
    }

    public static float[] ArrivalDistances(IReadOnlyList<bool> target, bool[] visible, int width, int height, float dx, float dy)
    {
        var distance = new float[target.Count]; for (int i = 0; i < distance.Length; i++) distance[i] = Far;
        var heap = new MinHeap(); var seen = new bool[target.Count]; var queue = new Queue<int>();
        var nearest = Distance(visible, true, width, height, dx, dy);
        for (int start = 0; start < target.Count; start++)
        {
            if (!target[start] || seen[start]) continue;
            queue.Enqueue(start); seen[start] = true; bool seeded = false; int closest = start;
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (nearest[at] < nearest[closest]) closest = at;
                if (visible[at]) { distance[at] = 0; heap.Push(at, 0); seeded = true; }
                for (int side = 0; side < 4; side++)
                {
                    int next = Neighbour(at, side, width, height);
                    if (next < 0 || seen[next] || !target[next]) continue;
                    seen[next] = true; queue.Enqueue(next);
                }
            }
            // 분리된 섬은 가장 가까운 쪽에서 시작하며 미점유 셀을 경유하지 않습니다.
            if (!seeded) { distance[closest] = 0; heap.Push(closest, 0); }
        }
        while (heap.Count > 0)
        {
            var entry = heap.Pop(); if (entry.distance > distance[entry.cell]) continue;
            for (int side = 0; side < 4; side++)
            {
                int next = Neighbour(entry.cell, side, width, height); if (next < 0 || !target[next]) continue;
                float value = entry.distance + (side < 2 ? dx : dy);
                if (value >= distance[next]) continue;
                distance[next] = value; heap.Push(next, value);
            }
        }
        // 외부도 가장 가까운 점유 셀의 도달 시각을 이어야 먼 외곽의 붉은 띠가 먼저 나타나지 않습니다.
        var nearestTarget = new int[target.Count];
        Distance(target, true, width, height, dx, dy, nearestTarget);
        for (int i = 0; i < distance.Length; i++)
            if (!target[i]) distance[i] = nearestTarget[i] >= 0 ? distance[nearestTarget[i]] : 0;
        return distance;
    }
    private static int Neighbour(int at, int side, int width, int height)
        => side == 0 ? at % width > 0 ? at - 1 : -1 : side == 1 ? at % width + 1 < width ? at + 1 : -1 :
            side == 2 ? at >= width ? at - width : -1 : at + width < width * height ? at + width : -1;

    private sealed class MinHeap
    {
        private readonly List<(int cell, float distance)> _items = new();
        public int Count => _items.Count;
        public void Push(int cell, float distance)
        {
            var item = (cell, distance); int at = _items.Count; _items.Add(item);
            while (at > 0) { int parent = (at - 1) / 2; if (_items[parent].distance <= distance) break; _items[at] = _items[parent]; at = parent; }
            _items[at] = item;
        }
        public (int cell, float distance) Pop()
        {
            var result = _items[0]; var item = _items[_items.Count - 1]; _items.RemoveAt(_items.Count - 1);
            int at = 0;
            while (at * 2 + 1 < _items.Count)
            {
                int child = at * 2 + 1; if (child + 1 < _items.Count && _items[child + 1].distance < _items[child].distance) child++;
                if (_items[child].distance >= item.distance) break;
                _items[at] = _items[child]; at = child;
            }
            if (_items.Count > 0) _items[at] = item;
            return result;
        }
    }
}
