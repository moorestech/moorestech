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
var occupied = new HashSet<Vector3Int>();
foreach (var block in store.BlockGameObjectDictionary.Values)
{
    var p = block.BlockPosInfo;
    for (var x = p.MinPos.x; x <= p.MaxPos.x; x++)
    for (var y = p.MinPos.y; y <= p.MaxPos.y; y++)
    for (var z = p.MinPos.z; z <= p.MaxPos.z; z++)
        occupied.Add(new Vector3Int(x, y, z));
}
index.TryGetBounds(out var minCell, out var maxCell);
var levelSets = new Dictionary<int, HashSet<Vector3Int>>();
for (var s = 1; s <= 9; s++)
{
    var set = new HashSet<Vector3Int>();
    foreach (var cell in occupied) set.Add(new Vector3Int(cell.x >> s, cell.y >> s, cell.z >> s));
    levelSets[s] = set;
}

// ブリック：16^3 チャンクごとに 4096 ビットのセル占有と、4^3 サブブリック 64 個の占有マスク
// Brick: per 16^3 chunk, 4096 occupancy bits plus a 64-bit mask of non-empty 4^3 sub-bricks
var bricks = new Dictionary<Vector3Int, ulong[]>();
var bricksByLong = new Dictionary<long, ulong[]>();
var buildSw = Stopwatch.StartNew();
foreach (var cell in occupied)
{
    var key = new Vector3Int(cell.x >> 4, cell.y >> 4, cell.z >> 4);
    if (!bricks.TryGetValue(key, out var brick)) { brick = new ulong[65]; bricks[key] = brick; bricksByLong[Pack(key)] = brick; }
    int lx = cell.x & 15, ly = cell.y & 15, lz = cell.z & 15;
    var bit = lx | (ly << 4) | (lz << 8);
    brick[bit >> 6] |= 1UL << (bit & 63);
    brick[64] |= 1UL << ((lx >> 2) | ((ly >> 2) << 2) | ((lz >> 2) << 4));
}
var buildMs = buildSw.Elapsed.TotalMilliseconds;
var top256 = levelSets[8];
var top256Long = new HashSet<long>();
foreach (var k in top256) top256Long.Add(Pack(k));
sb.Append("\"bricks\":" + bricks.Count + ",\"brickBuildMs\":" + buildMs.ToString("F1", inv) + ",\"brickBytes\":" + (bricks.Count * 65 * 8));

var bmin = new Vector3(minCell.x, minCell.y, minCell.z);
var bmax = new Vector3(maxCell.x + 1, maxCell.y + 1, maxCell.z + 1);
var top = maxCell.y + 0.5f;
var worstRays = new (string, Ray)[]
{
    ("x", new Ray(new Vector3(bmin.x - 0.5f, top, Mathf.Floor((bmin.z + bmax.z) / 2) + 0.5f), Vector3.right)),
    ("z", new Ray(new Vector3(Mathf.Floor((bmin.x + bmax.x) / 2) + 0.5f, top, bmin.z - 0.5f), Vector3.forward)),
    ("diagA", new Ray(new Vector3(bmin.x - 0.5f, top, bmin.z - 0.5f), (new Vector3(bmax.x + 0.5f, top, bmax.z + 0.5f) - new Vector3(bmin.x - 0.5f, top, bmin.z - 0.5f)).normalized)),
    ("diagB", new Ray(new Vector3(bmax.x + 0.5f, top, bmin.z - 0.5f), (new Vector3(bmin.x - 0.5f, top, bmax.z + 0.5f) - new Vector3(bmax.x + 0.5f, top, bmin.z - 0.5f)).normalized)),
    ("lowDiag", new Ray(new Vector3(bmin.x - 0.5f, 90.5f, bmin.z - 0.5f), (new Vector3(bmax.x + 0.5f, 90.5f, bmax.z + 0.5f) - new Vector3(bmin.x - 0.5f, 90.5f, bmin.z - 0.5f)).normalized)),
};
var variants = new[] { "hashLevels4", "brick", "brickLong", "brick256Long" };
sb.Append(",\"worstRays\":{");
var first = true;
foreach (var (rname, ray) in worstRays)
{
    sb.Append((first ? "" : ",") + "\"" + rname + "\":{");
    first = false;
    var rsw = Stopwatch.StartNew();
    for (var i = 0; i < 200; i++) PlacementAimCellTracer.TryTrace(index, ray, float.PositiveInfinity, out _, out _);
    sb.Append("\"current\":" + (rsw.Elapsed.TotalMilliseconds / 200).ToString("F4", inv));
    long l0 = 0, s0 = 0, st0 = 0;
    var refHit = Walk(new int[0], ray, float.PositiveInfinity, ref l0, ref s0, ref st0, out var refT);
    foreach (var v in variants)
    {
        long lk = 0, sk = 0, stp = 0;
        bool hit; float t;
        if (v == "hashLevels4") hit = Walk(new[] { 2, 4, 6, 8 }, ray, float.PositiveInfinity, ref lk, ref sk, ref stp, out t);
        else hit = BrickWalk(v, ray, float.PositiveInfinity, ref lk, ref sk, ref stp, out t);
        var same = hit == refHit && (!hit || Mathf.Abs(t - refT) < 1e-4f);
        long a = 0, b = 0, c = 0;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 200; i++)
        {
            if (v == "hashLevels4") Walk(new[] { 2, 4, 6, 8 }, ray, float.PositiveInfinity, ref a, ref b, ref c, out _);
            else BrickWalk(v, ray, float.PositiveInfinity, ref a, ref b, ref c, out _);
        }
        sb.Append(",\"" + v + "\":{\"ms\":" + (sw.Elapsed.TotalMilliseconds / 200).ToString("F4", inv) + ",\"dictLookups\":" + lk + ",\"skips\":" + sk + ",\"steps\":" + stp + ",\"sameAsCellWalk\":" + (same ? "true" : "false") + "}");
    }
    sb.Append("}");
}
sb.Append("}");

// 実プレイに近い照準でも同じ比較をし、セル単位の素直な走査と結果が一致するかも数える
// Same comparison on realistic aims, also counting agreement with the plain cell walk
{
    var rnd = new System.Random(7);
    var cellList = new List<Vector3Int>(occupied);
    var mask = LayerConst.Without_Player_MapObject_Block_LayerMask;
    var ms = new Dictionary<string, List<double>>();
    ms["current"] = new List<double>();
    foreach (var v in variants) ms[v] = new List<double>();
    var mismatches = 0;
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
        var ray = new Ray(eye, Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward);
        samples++;
        var limit = Physics.Raycast(ray, out var hitInfo, float.PositiveInfinity, mask) ? hitInfo.distance : float.PositiveInfinity;

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++) PlacementAimCellTracer.TryTrace(index, ray, limit, out _, out _);
        ms["current"].Add(sw.Elapsed.TotalMilliseconds / 10);
        long l0 = 0, s0 = 0, st0 = 0;
        var refHit = Walk(new int[0], ray, limit, ref l0, ref s0, ref st0, out var refT);
        foreach (var v in variants)
        {
            long a = 0, b = 0, c = 0;
            bool hit = false; float t = 0f;
            sw.Restart();
            for (var i = 0; i < 10; i++)
            {
                if (v == "hashLevels4") hit = Walk(new[] { 2, 4, 6, 8 }, ray, limit, ref a, ref b, ref c, out t);
                else hit = BrickWalk(v, ray, limit, ref a, ref b, ref c, out t);
            }
            ms[v].Add(sw.Elapsed.TotalMilliseconds / 10);
            if (hit != refHit || (hit && Mathf.Abs(t - refT) > 1e-4f)) mismatches++;
        }
    }
    sb.Append(",\"realistic\":{\"samples\":" + samples + ",\"mismatches\":" + mismatches);
    foreach (var kv in ms) sb.Append(",\"" + kv.Key + "\":" + Stats(kv.Value, "F4"));
    sb.Append("}");
}

sb.Append("}");
return sb.ToString();

#region Internal

long Pack(Vector3Int k)
{
    return ((long)(k.x & 0x1FFFFF) << 42) | ((long)(k.y & 0x1FFFFF) << 21) | (long)(k.z & 0x1FFFFF);
}

// ブリック版の走査。チャンクをまたぐときだけ辞書を引き、チャンクの中はビットで判定する
// Brick walk: a dictionary lookup only when crossing chunks; inside a chunk everything is bit tests
bool BrickWalk(string variant, Ray ray, float maxDistance, ref long lookups, ref long skips, ref long steps, out float hitT)
{
    hitT = 0f;
    if (!SlabBounds(ray, bmin, bmax, out var tEnter, out var tExit)) return false;
    var tEnd = Mathf.Min(tExit, maxDistance);
    var o = ray.origin;
    var d = ray.direction;
    var cell = tEnter > 0f ? CellJustBefore(o, d, tEnter) : Vector3Int.FloorToInt(o);
    var lastT = float.NegativeInfinity;
    var useLong = variant != "brick";
    var use256 = variant == "brick256Long";
    var brickKey = new Vector3Int(int.MinValue, 0, 0);
    ulong[] brick = null;
    while (true)
    {
        var key = new Vector3Int(cell.x >> 4, cell.y >> 4, cell.z >> 4);
        if (key != brickKey)
        {
            brickKey = key;
            lookups++;
            if (useLong) bricksByLong.TryGetValue(Pack(key), out brick);
            else bricks.TryGetValue(key, out brick);
        }

        // 空の最も大きい単位（256 → 16 → 4）を選ぶ
        // Pick the largest empty unit (256 -> 16 -> 4)
        var emptyShift = 0;
        if (brick == null)
        {
            emptyShift = 4;
            if (use256)
            {
                lookups++;
                if (!top256Long.Contains(Pack(new Vector3Int(cell.x >> 8, cell.y >> 8, cell.z >> 8)))) emptyShift = 8;
            }
        }
        else
        {
            var sub = ((cell.x & 15) >> 2) | (((cell.y & 15) >> 2) << 2) | (((cell.z & 15) >> 2) << 4);
            if ((brick[64] & (1UL << sub)) == 0UL) emptyShift = 2;
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

        // 入ったセルの占有をビットで調べる（チャンクが変わっていれば先にブリックを引き直す）
        // Test the entered cell's bit (re-fetch the brick first if the chunk changed)
        var nkey = new Vector3Int(cell.x >> 4, cell.y >> 4, cell.z >> 4);
        if (nkey != brickKey)
        {
            brickKey = nkey;
            lookups++;
            if (useLong) bricksByLong.TryGetValue(Pack(nkey), out brick);
            else bricks.TryGetValue(nkey, out brick);
        }
        if (brick == null) continue;
        var bit = (cell.x & 15) | ((cell.y & 15) << 4) | ((cell.z & 15) << 8);
        if ((brick[bit >> 6] & (1UL << (bit & 63))) == 0UL) continue;
        hitT = t;
        return true;
    }
}



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
