// 実プレイに近い設置照準の時間の分布と、全ブロックを占有セルの索引へ登録する時間（読み込みでの増分）を測る
// Measure the time distribution of realistic placement aims and the cost of registering every block into the occupied-cell index (the load-time share)
// 照準は、ブロックから 20m 以内の地面に立ち、目の高さ（+1.6m）から俯角 -60°〜+15°・方位ランダムに撃ち、物理レイの距離で区切る（乱数の種は固定）
// Aims stand on the ground within 20 m of a block and shoot from eye height (+1.6 m) at pitch -60..+15 degrees and random yaw, bounded by the physics hit (fixed seed)
using System.Collections.Generic;
using System.Diagnostics;
using Client.Common;
using Client.Game.InGame.Block.Index;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using UnityEngine;

var store = ClientDIContext.BlockGameObjectDataStore;
var index = store.OccupiedCells;
var inv = System.Globalization.CultureInfo.InvariantCulture;
var blocks = new List<Game.Block.Interface.BlockPositionInfo>();
foreach (var block in store.BlockGameObjectDictionary.Values) blocks.Add(block.BlockPosInfo);

// 全ブロックを新しい索引へ登録する時間（3回の中央値）
// Time to register every block into a fresh index (median of 3)
var registerMs = new List<double>();
for (var i = 0; i < 3; i++)
{
    var fresh = new BlockOccupiedCellIndex();
    var sw = Stopwatch.StartNew();
    foreach (var posInfo in blocks) fresh.Register(posInfo);
    registerMs.Add(sw.Elapsed.TotalMilliseconds);
}
registerMs.Sort();

// 実プレイに近い照準
// Realistic aims
var rnd = new System.Random(7);
var mask = LayerConst.Without_Player_MapObject_Block_LayerMask;
var traceMs = new List<double>();
var hits = 0;
var samples = 0;
for (var attempt = 0; attempt < 4000 && samples < 1000; attempt++)
{
    var anchor = blocks[rnd.Next(blocks.Count)].MinPos;
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
    traceMs.Add(sw.Elapsed.TotalMilliseconds / 10);
    if (PlacementAimCellTracer.TryTrace(index, ray, limit, out _, out _)) hits++;
}
traceMs.Sort();
double sum = 0;
foreach (var v in traceMs) sum += v;
string P(double q) => traceMs[Mathf.Min(traceMs.Count - 1, (int)(q * traceMs.Count))].ToString("F4", inv);

return "{\"blocks\":" + blocks.Count + ",\"registerAllMsMedian\":" + registerMs[1].ToString("F1", inv)
       + ",\"realistic\":{\"samples\":" + samples + ",\"hits\":" + hits + ",\"meanMs\":" + (sum / traceMs.Count).ToString("F4", inv)
       + ",\"p50\":" + P(0.5) + ",\"p90\":" + P(0.9) + ",\"p99\":" + P(0.99) + ",\"max\":" + traceMs[traceMs.Count - 1].ToString("F4", inv) + "}}";
