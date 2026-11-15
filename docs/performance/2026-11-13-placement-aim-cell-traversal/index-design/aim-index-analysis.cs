// 設置照準の索引の設計検討用。占有セルの分布、段の構成ごとの走査回数と時間、実プレイに近い照準の分布を測る
// For designing the aim index: occupied-cell distribution, per-configuration walk counts and time, and realistic aim distribution
using System.Collections.Generic;
using System.Diagnostics;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using UnityEngine;

var store = ClientDIContext.BlockGameObjectDataStore;
var index = store.OccupiedCells;
var inv = System.Globalization.CultureInfo.InvariantCulture;
var axisOrder = new[] { 1, 0, 2 };
var sb = new System.Text.StringBuilder("{");

// 1. 占有セルとブロックの大きさの分布
// 1. Occupied cells and block size distribution
var occupied = new HashSet<Vector3Int>();
var volumeHistogram = new SortedDictionary<int, int>();
var blockCount = 0;
var maxVolume = 0;
var maxSize = Vector3Int.zero;
var yHistogram = new SortedDictionary<int, int>();
foreach (var block in store.BlockGameObjectDictionary.Values)
{
    var p = block.BlockPosInfo;
    blockCount++;
    var size = p.MaxPos - p.MinPos + Vector3Int.one;
    var volume = size.x * size.y * size.z;
    volumeHistogram[volume] = volumeHistogram.TryGetValue(volume, out var c) ? c + 1 : 1;
    if (volume > maxVolume) { maxVolume = volume; maxSize = size; }
    for (var x = p.MinPos.x; x <= p.MaxPos.x; x++)
    for (var y = p.MinPos.y; y <= p.MaxPos.y; y++)
    for (var z = p.MinPos.z; z <= p.MaxPos.z; z++)
    {
        if (occupied.Add(new Vector3Int(x, y, z))) yHistogram[y >> 3] = yHistogram.TryGetValue(y >> 3, out var yc) ? yc + 1 : 1;
    }
}
index.TryGetBounds(out var minCell, out var maxCell);
sb.Append("\"blocks\":" + blockCount + ",\"occupiedCells\":" + occupied.Count + ",\"boundsMin\":\"" + minCell + "\",\"boundsMax\":\"" + maxCell + "\"");
sb.Append(",\"maxBlockSize\":\"" + maxSize + "\",\"volumeHistogram\":{");
var first = true;
foreach (var kv in volumeHistogram) { sb.Append((first ? "" : ",") + "\"" + kv.Key + "\":" + kv.Value); first = false; }
sb.Append("},\"cellsByY8\":{");
first = true;
foreach (var kv in yHistogram) { sb.Append((first ? "" : ",") + "\"" + (kv.Key * 8) + "\":" + kv.Value); first = false; }
sb.Append("}");

// 2. 段の大きさごとの空でないノード数（3次元と XZ の柱）
// 2. Non-empty node counts per node size (3D and XZ columns)
var levelSets = new Dictionary<int, HashSet<Vector3Int>>();
sb.Append(",\"nonEmptyNodes3D\":{");
for (var s = 1; s <= 9; s++)
{
    var set = new HashSet<Vector3Int>();
    foreach (var cell in occupied) set.Add(new Vector3Int(cell.x >> s, cell.y >> s, cell.z >> s));
    levelSets[s] = set;
    sb.Append((s == 1 ? "" : ",") + "\"" + (1 << s) + "\":" + set.Count);
}
sb.Append("},\"nonEmptyColumnsXZ\":{");
for (var s = 0; s <= 8; s++)
{
    var set = new HashSet<Vector2Int>();
    foreach (var cell in occupied) set.Add(new Vector2Int(cell.x >> s, cell.z >> s));
    sb.Append((s == 0 ? "" : ",") + "\"" + (1 << s) + "\":" + set.Count);
}
sb.Append("}");

// 3. 1回の検索の素の時間（HashSet<Vector3Int>・本物の索引・配列）
// 3. Raw cost of a single lookup (HashSet<Vector3Int>, the real index, an array)
{
    var rnd = new System.Random(1);
    var probes = new Vector3Int[100000];
    for (var i = 0; i < probes.Length; i++) probes[i] = new Vector3Int(rnd.Next(minCell.x, maxCell.x + 1), rnd.Next(minCell.y, maxCell.y + 1), rnd.Next(minCell.z, maxCell.z + 1));
    var hits = 0;
    var sw = Stopwatch.StartNew();
    foreach (var pr in probes) if (occupied.Contains(pr)) hits++;
    var hashNs = sw.Elapsed.TotalMilliseconds * 1e6 / probes.Length;
    sw.Restart();
    foreach (var pr in probes) if (index.TryGetBlockAt(pr, out _)) hits++;
    var indexNs = sw.Elapsed.TotalMilliseconds * 1e6 / probes.Length;
    sw.Restart();
    foreach (var pr in probes) if (!index.IsChunkEmpty(pr)) hits++;
    var chunkNs = sw.Elapsed.TotalMilliseconds * 1e6 / probes.Length;
    var array = new int[1 << 20];
    sw.Restart();
    foreach (var pr in probes) hits += array[(pr.x * 73856093 ^ pr.y * 19349663 ^ pr.z * 83492791) & ((1 << 20) - 1)];
    var arrayNs = sw.Elapsed.TotalMilliseconds * 1e6 / probes.Length;
    sb.Append(",\"lookupNs\":{\"hashSet\":" + hashNs.ToString("F1", inv) + ",\"indexTryGetBlockAt\":" + indexNs.ToString("F1", inv) + ",\"indexIsChunkEmpty\":" + chunkNs.ToString("F1", inv) + ",\"intArray\":" + arrayNs.ToString("F1", inv) + ",\"hits\":" + hits + "}");
}

// 4. 段の構成ごとに、同じレイで何回たどるか・時間はどうかをまねて比べる
// 4. Simulate each level configuration on the same rays and compare step counts and time
var configs = new (string name, int[] shifts)[]
{
    ("cellOnly", new int[0]),
    ("s16", new[] { 4 }),
    ("s4_16_64_256", new[] { 2, 4, 6, 8 }),
    ("s2to256", new[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
    ("s8_64_512", new[] { 3, 6, 9 }),
    ("s16_256", new[] { 4, 8 }),
};
var bmin = new Vector3(minCell.x, minCell.y, minCell.z);
var bmax = new Vector3(maxCell.x + 1, maxCell.y + 1, maxCell.z + 1);
var top = maxCell.y + 0.5f;
var worstRays = new (string, Ray)[]
{
    ("x", new Ray(new Vector3(bmin.x - 0.5f, top, Mathf.Floor((bmin.z + bmax.z) / 2) + 0.5f), Vector3.right)),
    ("z", new Ray(new Vector3(Mathf.Floor((bmin.x + bmax.x) / 2) + 0.5f, top, bmin.z - 0.5f), Vector3.forward)),
    ("diagA", new Ray(new Vector3(bmin.x - 0.5f, top, bmin.z - 0.5f), (new Vector3(bmax.x + 0.5f, top, bmax.z + 0.5f) - new Vector3(bmin.x - 0.5f, top, bmin.z - 0.5f)).normalized)),
    ("diagB", new Ray(new Vector3(bmax.x + 0.5f, top, bmin.z - 0.5f), (new Vector3(bmin.x - 0.5f, top, bmax.z + 0.5f) - new Vector3(bmax.x + 0.5f, top, bmin.z - 0.5f)).normalized)),
};
sb.Append(",\"worstRays\":{");
first = true;
foreach (var (rname, ray) in worstRays)
{
    sb.Append((first ? "" : ",") + "\"" + rname + "\":{");
    first = false;
    var realSw = Stopwatch.StartNew();
    for (var i = 0; i < 200; i++) PlacementAimCellTracer.TryTrace(index, ray, float.PositiveInfinity, out _, out _);
    sb.Append("\"realTryTraceMs\":" + (realSw.Elapsed.TotalMilliseconds / 200).ToString("F4", inv));
    foreach (var (cname, shifts) in configs)
    {
        long lookups = 0, skips = 0, steps = 0;
        var hit = Walk(shifts, ray, float.PositiveInfinity, ref lookups, ref skips, ref steps, out var hitT);
        var reps = shifts.Length == 0 ? 20 : 200;
        long l2 = 0, s2 = 0, st2 = 0;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < reps; i++) Walk(shifts, ray, float.PositiveInfinity, ref l2, ref s2, ref st2, out _);
        sb.Append(",\"" + cname + "\":{\"ms\":" + (sw.Elapsed.TotalMilliseconds / reps).ToString("F4", inv) + ",\"lookups\":" + lookups + ",\"skips\":" + skips + ",\"steps\":" + steps + ",\"hit\":" + (hit ? "true" : "false") + "}");
    }
    sb.Append("}");
}
sb.Append("}");

// 5. 実プレイに近い照準：ブロックの近く（20m 以内）の地面に立ち、目の高さから下向き寄りに撃つ。物理レイの距離で区切る
// 5. Realistic aims: stand on the ground within 20 m of a block, shoot from eye height biased downward, bounded by the physics hit
{
    var rnd = new System.Random(7);
    var cellList = new List<Vector3Int>(occupied);
    var mask = LayerConst.Without_Player_MapObject_Block_LayerMask;
    var realMs = new List<double>();
    var physicsMs = new List<double>();
    var cellCounts = new List<int>();
    var configMs = new Dictionary<string, List<double>>();
    foreach (var (cname, _) in configs) configMs[cname] = new List<double>();
    var noPhysicsHit = 0;
    var traceHits = 0;
    var samples = 0;
    for (var attempt = 0; attempt < 4000 && samples < 1000; attempt++)
    {
        var anchor = cellList[rnd.Next(cellList.Count)];
        var angle = (float)(rnd.NextDouble() * Mathf.PI * 2);
        var radius = (float)(rnd.NextDouble() * 20);
        var feet = new Vector3(anchor.x + Mathf.Cos(angle) * radius, 600f, anchor.z + Mathf.Sin(angle) * radius);
        if (!Physics.Raycast(feet, Vector3.down, out var ground, 1000f, mask)) continue;
        var eye = ground.point + Vector3.up * 1.6f;
        if (index.TryGetBlockAt(Vector3Int.FloorToInt(eye), out _)) continue;
        var yaw = (float)(rnd.NextDouble() * 360);
        var pitch = (float)(-60 + rnd.NextDouble() * 75);
        var direction = Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward;
        var ray = new Ray(eye, direction);
        samples++;

        var psw = Stopwatch.StartNew();
        var physicsHit = Physics.Raycast(ray, out var hitInfo, float.PositiveInfinity, mask);
        physicsMs.Add(psw.Elapsed.TotalMilliseconds);
        var limit = physicsHit ? hitInfo.distance : float.PositiveInfinity;
        if (!physicsHit) noPhysicsHit++;

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++) PlacementAimCellTracer.TryTrace(index, ray, limit, out _, out _);
        realMs.Add(sw.Elapsed.TotalMilliseconds / 10);
        if (PlacementAimCellTracer.TryTrace(index, ray, limit, out _, out _)) traceHits++;

        long lookups = 0, skips = 0, steps = 0;
        Walk(new int[0], ray, limit, ref lookups, ref skips, ref steps, out _);
        cellCounts.Add((int)steps);
        foreach (var (cname, shifts) in configs)
        {
            if (shifts.Length == 0) continue;
            long a = 0, b = 0, d = 0;
            var csw = Stopwatch.StartNew();
            for (var i = 0; i < 10; i++) Walk(shifts, ray, limit, ref a, ref b, ref d, out _);
            configMs[cname].Add(csw.Elapsed.TotalMilliseconds / 10);
        }
    }
    sb.Append(",\"realistic\":{\"samples\":" + samples + ",\"noPhysicsHit\":" + noPhysicsHit + ",\"traceHits\":" + traceHits);
    sb.Append(",\"cellsWalked\":" + Stats(cellCounts.ConvertAll(v => (double)v), "F0"));
    sb.Append(",\"physicsRaycastMs\":" + Stats(physicsMs, "F4"));
    sb.Append(",\"realTryTraceMs\":" + Stats(realMs, "F4"));
    foreach (var (cname, shifts) in configs)
    {
        if (shifts.Length == 0) continue;
        sb.Append(",\"" + cname + "Ms\":" + Stats(configMs[cname], "F4"));
    }
    sb.Append("}");
}

sb.Append("}");
return sb.ToString();

#region Internal

string Stats(List<double> values, string format)
{
    if (values.Count == 0) return "null";
    values.Sort();
    double sum = 0;
    foreach (var v in values) sum += v;
    string P(double q) => values[Mathf.Min(values.Count - 1, (int)(q * values.Count))].ToString(format, inv);
    return "{\"mean\":" + (sum / values.Count).ToString(format, inv) + ",\"p50\":" + P(0.5) + ",\"p90\":" + P(0.9) + ",\"p99\":" + P(0.99) + ",\"max\":" + values[values.Count - 1].ToString(format, inv) + "}";
}

// 本番の走査と同じ規則（入る直前のセルから・同値 Y→X→Z・空ノードは出口の直前へ飛ぶ）を、任意の段の構成でまねる
// Mirrors the production walk rules (start just before entry, ties Y->X->Z, skip empty nodes to just before exit) for any level set
bool Walk(int[] shifts, Ray ray, float maxDistance, ref long lookups, ref long skips, ref long steps, out float hitT)
{
    hitT = 0f;
    if (!SlabBounds(ray, bmin, bmax, out var tEnter, out var tExit)) return false;
    var tEnd = Mathf.Min(tExit, maxDistance);
    var o = ray.origin;
    var d = ray.direction;
    var cell = tEnter > 0f ? CellJustBefore(o, d, tEnter) : Vector3Int.FloorToInt(o);
    var lastT = float.NegativeInfinity;
    while (true)
    {
        // 小さい段から見て、空の最も大きいノードを探す
        // Find the largest empty node, checking from the smallest level
        var emptyShift = 0;
        foreach (var s in shifts)
        {
            lookups++;
            if (levelSets[s].Contains(new Vector3Int(cell.x >> s, cell.y >> s, cell.z >> s))) break;
            emptyShift = s;
        }
        if (emptyShift > 0)
        {
            var tNode = NodeExit(o, d, cell, emptyShift);
            if (tNode > tEnd) return false;
            if (tNode > lastT)
            {
                cell = CellJustBefore(o, d, tNode);
                lastT = float.NegativeInfinity;
                skips++;
            }
        }

        var best = -1;
        var t = float.PositiveInfinity;
        foreach (var axis in axisOrder)
        {
            if (d[axis] == 0f) continue;
            var ta = NextBoundary(o[axis], d[axis], cell[axis]);
            if (ta < t) { t = ta; best = axis; }
        }
        if (best < 0 || t > tEnd) return false;
        cell[best] += d[best] > 0f ? 1 : -1;
        lastT = t;
        steps++;
        lookups++;
        if (occupied.Contains(cell)) { hitT = t; return true; }
    }
}

float NodeExit(Vector3 o, Vector3 d, Vector3Int cell, int shift)
{
    var size = 1 << shift;
    var tExit = float.PositiveInfinity;
    for (var axis = 0; axis < 3; axis++)
    {
        if (d[axis] == 0f) continue;
        var start = (cell[axis] >> shift) * size;
        tExit = Mathf.Min(tExit, NextBoundary(o[axis], d[axis], d[axis] > 0f ? start + size - 1 : start));
    }
    return tExit;
}

Vector3Int CellJustBefore(Vector3 o, Vector3 d, float t)
{
    var cell = Vector3Int.FloorToInt(o + d * t);
    for (var axis = 0; axis < 3; axis++)
    {
        if (d[axis] == 0f) continue;
        var step = d[axis] > 0f ? 1 : -1;
        while (NextBoundary(o[axis], d[axis], cell[axis]) < t) cell[axis] += step;
        while (NextBoundary(o[axis], d[axis], cell[axis] - step) >= t) cell[axis] -= step;
    }
    return cell;
}

float NextBoundary(float o, float d, int cell)
{
    return ((d > 0f ? cell + 1 : cell) - o) / d;
}

bool SlabBounds(Ray ray, Vector3 lo, Vector3 hi, out float tEnter, out float tExit)
{
    tEnter = float.NegativeInfinity;
    tExit = float.PositiveInfinity;
    for (var axis = 0; axis < 3; axis++)
    {
        var d = ray.direction[axis];
        var o = ray.origin[axis];
        if (d == 0f) { if (o < lo[axis] || o > hi[axis]) return false; continue; }
        var t1 = (lo[axis] - o) / d;
        var t2 = (hi[axis] - o) / d;
        tEnter = Mathf.Max(tEnter, Mathf.Min(t1, t2));
        tExit = Mathf.Min(tExit, Mathf.Max(t1, t2));
    }
    return tEnter <= tExit && tExit >= 0f;
}

#endregion
