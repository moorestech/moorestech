// Renderer を種類ごとに分け、種類ごとに描画を止めた窓と止めない窓（基準）を交互に並べて、URP の描画時間などを窓ごとに平均する
// 並び：周ごとに [A X1 A X2 ... Xn A]、X の順番は周ごとに入れ替える。窓の最初の DISCARD 秒は切り替えの余波として捨てる
// 切り替えと記録は PlayerLoop の Update の先頭で行う（スニペットを呼ぶたびに Editor が止まるのを避ける）
// 結果は AppDomain の "camprobe.results"（窓1つにつき JSON 1行）。camprobe-collect.cs が回収して元に戻す
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Player;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.View.Object.Core;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.LowLevel;

const float WindowSec = 10f;
const float DiscardSec = 3f;
const int Cycles = 2;
const int TopBlocks = 10;
var ci = System.Globalization.CultureInfo.InvariantCulture;
var d = System.AppDomain.CurrentDomain;
if (d.GetData("camprobe.results") != null) return "FAIL already running; run camprobe-collect.cs first";

// ---- 種類分け ----
string Classify(Renderer r)
{
    var typeName = r.GetType().Name;
    if (r is ParticleSystemRenderer || typeName.Contains("VFX")) return "effect";
    for (var t = r.transform; t != null; t = t.parent) if (t.name == "BeltTransportItems") return "belt";
    var block = r.GetComponentInParent<BlockGameObject>(true);
    if (block != null) return "block:" + block.BlockMasterElement.Name;
    if (r.GetComponentInParent<MapObjectGameObject>(true) != null) return "mapobject";
    if (r.GetComponentInParent<OutcropGameObject>(true) != null) return "outcrop";
    if (r.GetComponentInParent<TrainCarEntityObject>(true) != null) return "train";
    if (r.GetComponentInParent<RailObjectIdCarrier>(true) != null) return "rail";
    if (r.GetComponentInParent<PlayerObjectController>(true) != null) return "player";
    return "other:" + r.transform.root.name;
}
long Triangles(Renderer r)
{
    Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null);
    if (m == null) return 0;
    long n = 0;
    for (var i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3;
    return n;
}

var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
var groups = new Dictionary<string, List<Renderer>>();
foreach (var r in all)
{
    var c = Classify(r);
    if (!groups.TryGetValue(c, out var l)) groups[c] = l = new List<Renderer>();
    l.Add(r);
}
// 統計：全数・有効・描画対象（isVisible：影だけのものも含む）・描画対象の三角形・影を落とす数
var stats = new List<string>();
var visibleOf = new Dictionary<string, int>();
foreach (var kv in groups)
{
    var vis = kv.Value.Where(r => r.enabled && r.gameObject.activeInHierarchy && r.isVisible).ToList();
    visibleOf[kv.Key] = vis.Count;
    stats.Add("{\"category\":\"" + kv.Key.Replace("\"", "'") + "\",\"total\":" + kv.Value.Count
        + ",\"enabled\":" + kv.Value.Count(r => r.enabled && r.gameObject.activeInHierarchy)
        + ",\"visible\":" + vis.Count + ",\"visibleTriangles\":" + vis.Sum(Triangles)
        + ",\"visibleShadowCasters\":" + vis.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) + "}");
}

// 止める対象：ブロックは描画対象の数の上位 TopBlocks 種、残りのブロックは block:(others)。other:* は上位3つ、残りは other:(others)
var conditions = new List<(string name, List<Renderer> renderers, bool belt, int terrain)>();
var blockKeys = groups.Keys.Where(k => k.StartsWith("block:")).OrderByDescending(k => visibleOf[k]).ToList();
foreach (var k in blockKeys.Take(TopBlocks)) conditions.Add((k, groups[k], false, 0));
conditions.Add(("block:(others)", blockKeys.Skip(TopBlocks).SelectMany(k => groups[k]).ToList(), false, 0));
foreach (var k in new[] { "mapobject", "outcrop", "train", "rail", "player", "effect" })
    if (groups.ContainsKey(k)) conditions.Add((k, groups[k], false, 0));
conditions.Add(("belt", new List<Renderer>(), true, 0));
var otherKeys = groups.Keys.Where(k => k.StartsWith("other:")).OrderByDescending(k => visibleOf[k]).ToList();
foreach (var k in otherKeys.Take(3)) conditions.Add((k, groups[k], false, 0));
conditions.Add(("other:(others)", otherKeys.Skip(3).SelectMany(k => groups[k]).ToList(), false, 0));
conditions.Add(("terrain:heightmap", new List<Renderer>(), false, 1));
conditions.Add(("terrain:trees+foliage", new List<Renderer>(), false, 2));
conditions.Add(("X0:noop", new List<Renderer>(), false, 0));
conditions.Add(("ALL", all.Where(r => Classify(r) != "belt").ToList(), true, 3));

// 元の forceRenderingOff を覚えて、戻すときはその値にする
var originalForceOff = all.ToDictionary(r => r, r => r.forceRenderingOff);
var terrains = Terrain.activeTerrains.ToArray();
var terrainOriginal = terrains.Select(t => (t.drawHeightmap, t.drawTreesAndFoliage)).ToArray();
Transform beltRoot = GameObject.Find("BeltTransportItems")?.transform;

void Apply((string name, List<Renderer> renderers, bool belt, int terrain) c, bool off)
{
    foreach (var r in c.renderers) if (r != null) r.forceRenderingOff = off || originalForceOff[r];
    if (c.terrain == 1 || c.terrain == 3) for (var i = 0; i < terrains.Length; i++) terrains[i].drawHeightmap = off ? false : terrainOriginal[i].drawHeightmap;
    if (c.terrain == 2 || c.terrain == 3) for (var i = 0; i < terrains.Length; i++) terrains[i].drawTreesAndFoliage = off ? false : terrainOriginal[i].drawTreesAndFoliage;
    if (c.belt && beltRoot != null) foreach (var r in beltRoot.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = off;
}

// ---- 記録するもの ----
ProfilerRecorder Rec(string name)
{
    var handles = new List<ProfilerRecorderHandle>();
    ProfilerRecorderHandle.GetAvailable(handles);
    foreach (var h in handles)
        if (ProfilerRecorderHandle.GetDescription(h).Name == name) { var rec = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default); rec.Start(); return rec; }
    return default;
}
var recNames = new[] { "Inl_UniversalRenderTotal", "RenderPlayModeViewCameras", "Draw Calls Count", "Batches Count", "Triangles Count", "SetPass Calls Count" };
var recs = recNames.Select(Rec).ToArray();
var recFound = string.Join(",", recNames.Where((n, i) => recs[i].Valid));

// ---- 予定表 ----
var rng = new System.Random(12345);
var schedule = new List<int>(); // -1 = A（基準）、それ以外は conditions の番号
for (var cyc = 0; cyc < Cycles; cyc++)
{
    var order = Enumerable.Range(0, conditions.Count).OrderBy(_ => rng.Next()).ToList();
    foreach (var i in order) { schedule.Add(-1); schedule.Add(i); }
}
schedule.Add(-1);

var results = new List<string>();
d.SetData("camprobe.results", results);
d.SetData("camprobe.stats", stats);
d.SetData("camprobe.done", false);
d.SetData("camprobe.error", "");
var win = -1;
var winStart = 0f;
int n = 0; double sumDt = 0, sumBatch = 0; var sums = new double[recs.Length];
var lastBatchCount = -1;
(string, List<Renderer>, bool, int) current = default;
var hasCurrent = false;

PlayerLoopSystem.UpdateFunction tick = () =>
{
    if ((bool)d.GetData("camprobe.done")) return;
    try
    {
        var now = Time.realtimeSinceStartup;
        if (win < 0 || now - winStart >= WindowSec)
        {
            if (win >= 0)
            {
                var cond = schedule[win] < 0 ? "A" : conditions[schedule[win]].name;
                var line = "{\"window\":" + win + ",\"cycle\":" + (win / (2 * conditions.Count)) + ",\"condition\":\"" + cond.Replace("\"", "'") + "\",\"frames\":" + n
                    + ",\"frameMs\":" + (n > 0 ? sumDt / n * 1000 : 0).ToString("F4", ci) + ",\"batchRenderMs\":" + (n > 0 ? sumBatch / n : 0).ToString("F4", ci);
                for (var i = 0; i < recs.Length; i++)
                {
                    var isTime = recNames[i].StartsWith("Inl_") || recNames[i] == "RenderPlayModeViewCameras";
                    var v = n > 0 ? sums[i] / n : 0;
                    line += ",\"" + recNames[i] + "\":" + (isTime ? v / 1e6 : v).ToString("F4", ci);
                }
                results.Add(line + "}");
                if (hasCurrent) { Apply(current, false); hasCurrent = false; }
            }
            win++;
            if (win >= schedule.Count) { d.SetData("camprobe.done", true); return; }
            winStart = now;
            n = 0; sumDt = 0; sumBatch = 0; System.Array.Clear(sums, 0, sums.Length);
            if (schedule[win] >= 0) { current = conditions[schedule[win]]; hasCurrent = true; Apply(current, true); }
            return;
        }
        // 窓の途中で生まれたベルト上のアイテムにも付ける
        if (hasCurrent && current.Item3 && beltRoot != null) foreach (var r in beltRoot.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = true;
        if (now - winStart < DiscardSec) return;
        n++;
        sumDt += Time.unscaledDeltaTime;
        var bc = (int)(d.GetData("camprobe.batchRenderCount") ?? 0);
        sumBatch += (double)(d.GetData("camprobe.batchRenderMs") ?? 0.0);
        for (var i = 0; i < recs.Length; i++) if (recs[i].Valid) sums[i] += recs[i].LastValue;
    }
    catch (System.Exception e)
    {
        d.SetData("camprobe.error", e.ToString());
        d.SetData("camprobe.done", true);
    }
};

var loop = PlayerLoop.GetCurrentPlayerLoop();
var subs = loop.subSystemList;
var updateIndex = System.Array.FindIndex(subs, s => s.type == typeof(UnityEngine.PlayerLoop.Update));
var updateSubs = subs[updateIndex].subSystemList.ToList();
updateSubs.Insert(0, new PlayerLoopSystem { type = typeof(System.Text.StringBuilder), updateDelegate = tick });
subs[updateIndex].subSystemList = updateSubs.ToArray();
loop.subSystemList = subs;
PlayerLoop.SetPlayerLoop(loop);

// 回収側から呼ぶ後始末（戻し・PlayerLoop から外す・Recorder の破棄）
System.Action cleanup = () =>
{
    if (hasCurrent) Apply(current, false);
    foreach (var r in all) if (r != null) r.forceRenderingOff = originalForceOff[r];
    for (var i = 0; i < terrains.Length; i++) { terrains[i].drawHeightmap = terrainOriginal[i].drawHeightmap; terrains[i].drawTreesAndFoliage = terrainOriginal[i].drawTreesAndFoliage; }
    var l2 = PlayerLoop.GetCurrentPlayerLoop();
    var s2 = l2.subSystemList;
    var u2 = System.Array.FindIndex(s2, s => s.type == typeof(UnityEngine.PlayerLoop.Update));
    s2[u2].subSystemList = s2[u2].subSystemList.Where(s => s.type != typeof(System.Text.StringBuilder)).ToArray();
    l2.subSystemList = s2;
    PlayerLoop.SetPlayerLoop(l2);
    foreach (var r in recs) if (r.Valid) r.Dispose();
};
d.SetData("camprobe.cleanup", cleanup);
var totalSec = schedule.Count * WindowSec;
return "installed conditions=" + conditions.Count + " windows=" + schedule.Count + " estSec=" + totalSec.ToString("F0", ci) + " renderers=" + all.Length + " recorders=" + recFound + " batch=" + Application.isBatchMode;
