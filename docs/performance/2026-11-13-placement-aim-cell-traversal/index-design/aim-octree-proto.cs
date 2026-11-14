// 設置照準の八分木の試作計測。根を広げる八分木（葉は 4^3 を ulong で持つ）を作り、走査の時間・回数・作る時間・大きさを、今の実装・ブリック案と比べる
// Octree prototype for the placement aim: build a growing-root octree (4^3 leaves as ulong) and compare walk time/counts, build time and size against the current index and the brick variant
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
var cellOrder = new List<Vector3Int>();
foreach (var block in store.BlockGameObjectDictionary.Values)
{
    var p = block.BlockPosInfo;
    for (var x = p.MinPos.x; x <= p.MaxPos.x; x++)
    for (var y = p.MinPos.y; y <= p.MaxPos.y; y++)
    for (var z = p.MinPos.z; z <= p.MaxPos.z; z++)
    {
        var c = new Vector3Int(x, y, z);
        if (occupied.Add(c)) cellOrder.Add(c);
    }
}
index.TryGetBounds(out var minCell, out var maxCell);
var bmin = new Vector3(minCell.x, minCell.y, minCell.z);
var bmax = new Vector3(maxCell.x + 1, maxCell.y + 1, maxCell.z + 1);

// ---- 八分木（配列で持つ）。ノードは 2^L の大きさで、その倍数の位置から始まる立方体。L=2 が葉（4^3 を ulong 1つ）
// ---- Array-backed octree; a node at level L is the 2^L cube aligned to multiples of its size. Level 2 is the leaf (one ulong per 4^3)
var child = new int[8 * 1024];
var nodeCount = 0;
var leafBits = new ulong[1024];
var leafCount = 0;
var rootNode = -1;
var rootLevel = 0;
var origin = Vector3Int.zero;
var growths = 0;
// 2^3 の小立方体8つそれぞれのビットの組（葉の中で空の 2^3 を飛ばす）
// Bit masks for each of the eight 2^3 sub-cubes inside a leaf (to skip empty 2^3 inside a leaf)
var subCubeMask = new ulong[8];
for (var lz = 0; lz < 4; lz++)
for (var ly = 0; ly < 4; ly++)
for (var lx = 0; lx < 4; lx++)
    subCubeMask[(lx >> 1) | ((ly >> 1) << 1) | ((lz >> 1) << 2)] |= 1UL << (lx | (ly << 2) | (lz << 4));

var buildSw = Stopwatch.StartNew();
foreach (var c in cellOrder) OctAdd(c);
var buildMs = buildSw.Elapsed.TotalMilliseconds;
sb.Append("\"cells\":" + occupied.Count + ",\"octBuildMs\":" + buildMs.ToString("F1", inv) + ",\"internalNodes\":" + nodeCount + ",\"leaves\":" + leafCount + ",\"rootLevel\":" + rootLevel + ",\"origin\":\"" + origin + "\",\"growths\":" + growths
          + ",\"approxBytes\":" + (nodeCount * 8 * 4 + leafCount * 8));

// 追加後の整合：全占有セルのビットが立ち、ランダムな空セルは立っていない
// Consistency after build: every occupied cell's bit is set and random empty cells are not
{
    var bad = 0;
    foreach (var c in cellOrder) if (!OctOccupiedSlow(c)) bad++;
    var rnd = new System.Random(3);
    for (var i = 0; i < 200000; i++)
    {
        var c = new Vector3Int(rnd.Next(minCell.x - 50, maxCell.x + 50), rnd.Next(minCell.y - 20, maxCell.y + 20), rnd.Next(minCell.z - 50, maxCell.z + 50));
        if (OctOccupiedSlow(c) != occupied.Contains(c)) bad++;
    }
    sb.Append(",\"consistencyErrors\":" + bad);
}

// ---- ブリック案（前回の最良）を比較用に作る
// ---- Brick variant (previous best) for comparison
var bricksByLong = new Dictionary<long, ulong[]>();
var top256Long = new HashSet<long>();
foreach (var cell in cellOrder)
{
    var key = new Vector3Int(cell.x >> 4, cell.y >> 4, cell.z >> 4);
    if (!bricksByLong.TryGetValue(Pack(key), out var brick)) { brick = new ulong[65]; bricksByLong[Pack(key)] = brick; }
    int lx = cell.x & 15, ly = cell.y & 15, lz = cell.z & 15;
    var bit = lx | (ly << 4) | (lz << 8);
    brick[bit >> 6] |= 1UL << (bit & 63);
    brick[64] |= 1UL << ((lx >> 2) | ((ly >> 2) << 2) | ((lz >> 2) << 4));
    top256Long.Add(Pack(new Vector3Int(cell.x >> 8, cell.y >> 8, cell.z >> 8)));
}

// 走査中にたどった道（段ごとのノードとキー）。直前の位置から登ってたどり直す
// Path cache during a walk (node and key per level); re-descend from the lowest still-valid ancestor
var pathNode = new int[40];
var pathKey = new Vector3Int[40];
var pathLowest = 40;

var top = maxCell.y + 0.5f;
var worstRays = new (string, Ray)[]
{
    ("x", new Ray(new Vector3(bmin.x - 0.5f, top, Mathf.Floor((bmin.z + bmax.z) / 2) + 0.5f), Vector3.right)),
    ("z", new Ray(new Vector3(Mathf.Floor((bmin.x + bmax.x) / 2) + 0.5f, top, bmin.z - 0.5f), Vector3.forward)),
    ("diagA", new Ray(new Vector3(bmin.x - 0.5f, top, bmin.z - 0.5f), (new Vector3(bmax.x + 0.5f, top, bmax.z + 0.5f) - new Vector3(bmin.x - 0.5f, top, bmin.z - 0.5f)).normalized)),
    ("diagB", new Ray(new Vector3(bmax.x + 0.5f, top, bmin.z - 0.5f), (new Vector3(bmin.x - 0.5f, top, bmax.z + 0.5f) - new Vector3(bmax.x + 0.5f, top, bmin.z - 0.5f)).normalized)),
    ("lowDiag", new Ray(new Vector3(bmin.x - 0.5f, 90.5f, bmin.z - 0.5f), (new Vector3(bmax.x + 0.5f, 90.5f, bmax.z + 0.5f) - new Vector3(bmin.x - 0.5f, 90.5f, bmin.z - 0.5f)).normalized)),
};
var variants = new[] { "brick256", "octCached", "octCachedNoSub", "octFromRoot" };

sb.Append(",\"worstRays\":{");
var first = true;
foreach (var (rname, ray) in worstRays)
{
    sb.Append((first ? "" : ",") + "\"" + rname + "\":{");
    first = false;
    var rsw = Stopwatch.StartNew();
    for (var i = 0; i < 200; i++) PlacementAimCellTracer.TryTrace(index, ray, float.PositiveInfinity, out _, out _);
    sb.Append("\"current\":" + (rsw.Elapsed.TotalMilliseconds / 200).ToString("F4", inv));
    long n0 = 0, s0 = 0;
    var refHit = RefWalk(ray, float.PositiveInfinity, ref n0, out var refT, out var refFace);
    sb.Append(",\"cellOnlySteps\":" + n0);
    foreach (var v in variants)
    {
        long probes = 0, skips = 0, steps = 0;
        var hit = RunVariant(v, ray, float.PositiveInfinity, ref probes, ref skips, ref steps, out var t, out var face);
        var same = hit == refHit && (!hit || (Mathf.Abs(t - refT) < 1e-4f && face == refFace));
        long a = 0, b = 0, c = 0;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 200; i++) RunVariant(v, ray, float.PositiveInfinity, ref a, ref b, ref c, out _, out _);
        sb.Append(",\"" + v + "\":{\"ms\":" + (sw.Elapsed.TotalMilliseconds / 200).ToString("F4", inv) + ",\"probes\":" + probes + ",\"skips\":" + skips + ",\"steps\":" + steps + ",\"same\":" + (same ? "true" : "false") + "}");
    }
    sb.Append("}");
}
sb.Append("}");

// 実プレイに近い照準（前回と同じ乱数・同じ作り方）
// Realistic aims (same seed and generation as before)
{
    var rnd = new System.Random(7);
    var mask = LayerConst.Without_Player_MapObject_Block_LayerMask;
    var ms = new Dictionary<string, List<double>> { ["current"] = new List<double>() };
    foreach (var v in variants) ms[v] = new List<double>();
    var mismatches = 0;
    var hits = 0;
    var samples = 0;
    for (var attempt = 0; attempt < 4000 && samples < 1000; attempt++)
    {
        var anchor = cellOrder[rnd.Next(cellOrder.Count)];
        var angle = (float)(rnd.NextDouble() * Mathf.PI * 2);
        var radius = (float)(rnd.NextDouble() * 20);
        var feet = new Vector3(anchor.x + Mathf.Cos(angle) * radius, 600f, anchor.z + Mathf.Sin(angle) * radius);
        if (!Physics.Raycast(feet, Vector3.down, out var ground, 1000f, mask)) continue;
        var eye = ground.point + Vector3.up * 1.6f;
        if (occupied.Contains(Vector3Int.FloorToInt(eye))) continue;
        var yaw = (float)(rnd.NextDouble() * 360);
        var pitch = (float)(-60 + rnd.NextDouble() * 75);
        var ray = new Ray(eye, Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward);
        samples++;
        var limit = Physics.Raycast(ray, out var hitInfo, float.PositiveInfinity, mask) ? hitInfo.distance : float.PositiveInfinity;

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++) PlacementAimCellTracer.TryTrace(index, ray, limit, out _, out _);
        ms["current"].Add(sw.Elapsed.TotalMilliseconds / 10);
        long n0 = 0;
        var refHit = RefWalk(ray, limit, ref n0, out var refT, out var refFace);
        if (refHit) hits++;
        foreach (var v in variants)
        {
            long a = 0, b = 0, c = 0;
            var hit = false; var t = 0f; var face = -1;
            sw.Restart();
            for (var i = 0; i < 10; i++) hit = RunVariant(v, ray, limit, ref a, ref b, ref c, out t, out face);
            ms[v].Add(sw.Elapsed.TotalMilliseconds / 10);
            if (hit != refHit || (hit && (Mathf.Abs(t - refT) > 1e-4f || face != refFace))) mismatches++;
        }
    }
    sb.Append(",\"realistic\":{\"samples\":" + samples + ",\"hits\":" + hits + ",\"mismatches\":" + mismatches);
    foreach (var kv in ms) sb.Append(",\"" + kv.Key + "\":" + Stats(kv.Value, "F4"));
    sb.Append("}");
}

// ランダムな向き・位置のレイ（ワールドの外から・中から・上から）でも結果を突き合わせる
// Also cross-check on random rays from outside, inside and above the world
{
    var rnd = new System.Random(11);
    var mismatches = 0;
    var hits = 0;
    for (var i = 0; i < 3000; i++)
    {
        var o = new Vector3(Mathf.Lerp(bmin.x - 100, bmax.x + 100, (float)rnd.NextDouble()), Mathf.Lerp(bmin.y - 20, bmax.y + 60, (float)rnd.NextDouble()), Mathf.Lerp(bmin.z - 100, bmax.z + 100, (float)rnd.NextDouble()));
        if (occupied.Contains(Vector3Int.FloorToInt(o))) continue;
        var target = (Vector3)cellOrder[rnd.Next(cellOrder.Count)] + new Vector3((float)rnd.NextDouble() * 3 - 1, (float)rnd.NextDouble() * 3 - 1, (float)rnd.NextDouble() * 3 - 1);
        var ray = new Ray(o, target - o);
        long n0 = 0;
        var refHit = RefWalk(ray, float.PositiveInfinity, ref n0, out var refT, out var refFace);
        if (refHit) hits++;
        foreach (var v in variants)
        {
            long a = 0, b = 0, c = 0;
            var hit = RunVariant(v, ray, float.PositiveInfinity, ref a, ref b, ref c, out var t, out var face);
            if (hit != refHit || (hit && (Mathf.Abs(t - refT) > 1e-4f || face != refFace))) mismatches++;
        }
    }
    sb.Append(",\"randomCrossCheck\":{\"hits\":" + hits + ",\"mismatches\":" + mismatches + "}");
}

sb.Append("}");
return sb.ToString();

#region Internal

bool RunVariant(string v, Ray ray, float limit, ref long probes, ref long skips, ref long steps, out float t, out int face)
{
    if (v == "brick256") return BrickWalk(ray, limit, ref probes, ref skips, ref steps, out t, out face);
    return OctWalk(ray, limit, v != "octFromRoot", v != "octCachedNoSub", ref probes, ref skips, ref steps, out t, out face);
}

int NewNode()
{
    if ((nodeCount + 1) * 8 > child.Length) System.Array.Resize(ref child, child.Length * 2);
    for (var k = 0; k < 8; k++) child[nodeCount * 8 + k] = -1;
    return nodeCount++;
}

int NewLeaf()
{
    if (leafCount + 1 > leafBits.Length) System.Array.Resize(ref leafBits, leafBits.Length * 2);
    leafBits[leafCount] = 0UL;
    return leafCount++;
}

Vector3Int Shr(Vector3Int c, int s)
{
    return new Vector3Int(c.x >> s, c.y >> s, c.z >> s);
}

int ChildSlot(Vector3Int c, int level)
{
    // level の段のノードの中で、c がどの子（level-1 の段）に入るか
    // Which child (at level-1) of a level-L node contains c
    var s = level - 1;
    return ((c.x >> s) & 1) | (((c.y >> s) & 1) << 1) | (((c.z >> s) & 1) << 2);
}

void OctAdd(Vector3Int c)
{
    // 根は基準点 origin からの相対座標 [0, 2^rootLevel) を覆う。空なら c を基準点にした 8^3（段3）を根にする
    // The root covers relative coordinates [0, 2^rootLevel) from origin; when empty, root an 8^3 (level 3) node at c
    if (rootNode < 0)
    {
        rootLevel = 3;
        origin = c;
        rootNode = NewNode();
    }

    // 根の外なら、外れた向きへ根を2倍に広げる。負の側へ広げる軸は基準点を根の大きさだけ下げ、古い根を上半分に置く
    // Outside the root, double it toward c; on axes grown to the negative side, lower origin by the root size and put the old root in the upper half
    while (!InsideRoot(c - origin))
    {
        var r = c - origin;
        var size = 1 << rootLevel;
        var parent = NewNode();
        var slot = 0;
        for (var axis = 0; axis < 3; axis++)
        {
            if (r[axis] >= 0) continue;
            origin[axis] -= size;
            slot |= 1 << axis;
        }
        child[parent * 8 + slot] = rootNode;
        rootNode = parent;
        rootLevel++;
        growths++;
    }

    var rel = c - origin;
    var node = rootNode;
    for (var level = rootLevel; level > 3; level--)
    {
        var slot = ChildSlot(rel, level);
        var next = child[node * 8 + slot];
        if (next < 0) { next = NewNode(); child[node * 8 + slot] = next; }
        node = next;
    }
    var leafSlot = ChildSlot(rel, 3);
    var leaf = child[node * 8 + leafSlot];
    if (leaf < 0) { leaf = NewLeaf(); child[node * 8 + leafSlot] = leaf; }
    leafBits[leaf] |= 1UL << ((rel.x & 3) | ((rel.y & 3) << 2) | ((rel.z & 3) << 4));
}

bool InsideRoot(Vector3Int r)
{
    var size = 1 << rootLevel;
    return r.x >= 0 && r.y >= 0 && r.z >= 0 && r.x < size && r.y < size && r.z < size;
}

bool OctOccupiedSlow(Vector3Int c)
{
    if (rootNode < 0 || !InsideRoot(c - origin)) return false;
    var rel = c - origin;
    var node = rootNode;
    for (var level = rootLevel; level > 3; level--)
    {
        node = child[node * 8 + ChildSlot(rel, level)];
        if (node < 0) return false;
    }
    var leaf = child[node * 8 + ChildSlot(rel, 3)];
    return leaf >= 0 && (leafBits[leaf] & (1UL << ((rel.x & 3) | ((rel.y & 3) << 2) | ((rel.z & 3) << 4)))) != 0UL;
}

// c を含む空の最も大きいノードの段（0 = c 自体は空だが飛ばせない）と、c が埋まっているかを返す
// Returns the level of the largest empty node containing c (0 = c is empty but nothing to skip) and whether c is occupied
int OctProbe(Vector3Int cell, bool cached, bool subCubes, out bool occ)
{
    occ = false;
    var c = cell - origin;
    // 根の外は、走査の入口（外接直方体の直前のセル）でしか起きないので飛ばさない
    // Outside the root only happens at the walk's entry cell, so nothing is skipped
    if (!InsideRoot(c)) { pathLowest = 40; return 0; }

    // 直前にたどった道のうち、まだ c を含む最も深いノードから下りる
    // Descend from the deepest node on the previous path that still contains c
    var level = rootLevel;
    var node = rootNode;
    if (cached && pathLowest < 40)
    {
        var l = pathLowest;
        while (l < rootLevel && Shr(c, l) != pathKey[l]) l++;
        if (l < rootLevel) { level = l; node = pathNode[l]; }
    }
    for (; level > 3; level--)
    {
        var next = child[node * 8 + ChildSlot(c, level)];
        if (next < 0) { pathLowest = level; pathNode[level] = node; pathKey[level] = Shr(c, level); return level - 1; }
        node = next;
        pathNode[level - 1] = node;
        pathKey[level - 1] = Shr(c, level - 1);
    }
    pathLowest = 3;
    var leaf = child[node * 8 + ChildSlot(c, 3)];
    if (leaf < 0) return 2;
    var bits = leafBits[leaf];
    if ((bits & (1UL << ((c.x & 3) | ((c.y & 3) << 2) | ((c.z & 3) << 4)))) != 0UL) { occ = true; return 0; }
    if (subCubes && (bits & subCubeMask[((c.x >> 1) & 1) | (((c.y >> 1) & 1) << 1) | (((c.z >> 1) & 1) << 2)]) == 0UL) return 1;
    return 0;
}

bool OctWalk(Ray ray, float maxDistance, bool cached, bool subCubes, ref long probes, ref long skips, ref long steps, out float hitT, out int faceAxis)
{
    hitT = 0f;
    faceAxis = -1;
    if (!SlabBounds(ray, bmin, bmax, out var tEnter, out var tExit)) return false;
    var tEnd = Mathf.Min(tExit, maxDistance);
    var o = ray.origin;
    var d = ray.direction;
    var cell = tEnter > 0f ? CellJustBefore(o, d, tEnter) : Vector3Int.FloorToInt(o);
    var lastT = float.NegativeInfinity;
    pathLowest = 40;
    probes++;
    var emptyLevel = OctProbe(cell, cached, subCubes, out _);
    while (true)
    {
        if (emptyLevel > 0)
        {
            var tNode = OctNodeExit(o, d, cell, emptyLevel);
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
        if (t != lastT) faceAxis = best;
        lastT = t;
        steps++;
        probes++;
        emptyLevel = OctProbe(cell, cached, subCubes, out var occ);
        if (!occ) continue;
        hitT = t;
        return true;
    }
}

// セルを1つずつ進む素直な走査（基準）。同値の組の最初の軸を面の軸にする
// Plain cell-by-cell walk (reference); the first axis of a tie group is the face axis
bool RefWalk(Ray ray, float maxDistance, ref long steps, out float hitT, out int faceAxis)
{
    hitT = 0f;
    faceAxis = -1;
    if (!SlabBounds(ray, bmin, bmax, out var tEnter, out var tExit)) return false;
    var tEnd = Mathf.Min(tExit, maxDistance);
    var o = ray.origin;
    var d = ray.direction;
    var cell = tEnter > 0f ? CellJustBefore(o, d, tEnter) : Vector3Int.FloorToInt(o);
    var lastT = float.NegativeInfinity;
    while (true)
    {
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
        if (t != lastT) faceAxis = best;
        lastT = t;
        steps++;
        if (!occupied.Contains(cell)) continue;
        hitT = t;
        return true;
    }
}

long Pack(Vector3Int k)
{
    return ((long)(k.x & 0x1FFFFF) << 42) | ((long)(k.y & 0x1FFFFF) << 21) | (long)(k.z & 0x1FFFFF);
}

bool BrickWalk(Ray ray, float maxDistance, ref long lookups, ref long skips, ref long steps, out float hitT, out int faceAxis)
{
    hitT = 0f;
    faceAxis = -1;
    if (!SlabBounds(ray, bmin, bmax, out var tEnter, out var tExit)) return false;
    var tEnd = Mathf.Min(tExit, maxDistance);
    var o = ray.origin;
    var d = ray.direction;
    var cell = tEnter > 0f ? CellJustBefore(o, d, tEnter) : Vector3Int.FloorToInt(o);
    var lastT = float.NegativeInfinity;
    var brickKey = new Vector3Int(int.MinValue, 0, 0);
    ulong[] brick = null;
    while (true)
    {
        var key = new Vector3Int(cell.x >> 4, cell.y >> 4, cell.z >> 4);
        if (key != brickKey) { brickKey = key; lookups++; bricksByLong.TryGetValue(Pack(key), out brick); }
        var emptyShift = 0;
        if (brick == null)
        {
            emptyShift = 4;
            lookups++;
            if (!top256Long.Contains(Pack(new Vector3Int(cell.x >> 8, cell.y >> 8, cell.z >> 8)))) emptyShift = 8;
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
            if (tNode > lastT) { cell = CellJustBefore(o, d, tNode); lastT = float.NegativeInfinity; skips++; }
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
        if (t != lastT) faceAxis = best;
        lastT = t;
        steps++;
        var nkey = new Vector3Int(cell.x >> 4, cell.y >> 4, cell.z >> 4);
        if (nkey != brickKey) { brickKey = nkey; lookups++; bricksByLong.TryGetValue(Pack(nkey), out brick); }
        if (brick == null) continue;
        var bit = (cell.x & 15) | ((cell.y & 15) << 4) | ((cell.z & 15) << 8);
        if ((brick[bit >> 6] & (1UL << (bit & 63))) == 0UL) continue;
        hitT = t;
        return true;
    }
}

float OctNodeExit(Vector3 o, Vector3 d, Vector3Int cell, int shift)
{
    // ノードの位置は基準点からの相対で決まるので、相対で揃えてから絶対座標へ戻す
    // Node positions are relative to origin, so align in relative coordinates and convert back
    var size = 1 << shift;
    var tExit = float.PositiveInfinity;
    for (var axis = 0; axis < 3; axis++)
    {
        if (d[axis] == 0f) continue;
        var start = origin[axis] + (((cell[axis] - origin[axis]) >> shift) << shift);
        tExit = Mathf.Min(tExit, NextBoundary(o[axis], d[axis], d[axis] > 0f ? start + size - 1 : start));
    }
    return tExit;
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

string Stats(List<double> values, string format)
{
    if (values.Count == 0) return "null";
    values.Sort();
    double sum = 0;
    foreach (var v in values) sum += v;
    string P(double q) => values[Mathf.Min(values.Count - 1, (int)(q * values.Count))].ToString(format, inv);
    return "{\"mean\":" + (sum / values.Count).ToString(format, inv) + ",\"p50\":" + P(0.5) + ",\"p90\":" + P(0.9) + ",\"p99\":" + P(0.99) + ",\"max\":" + values[values.Count - 1].ToString(format, inv) + "}";
}

#endregion
