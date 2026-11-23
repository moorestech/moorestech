// 設置モード中に、プレビュー半径の判定を 1m ずつずらしたときの1回あたりの時間を測る（プレイヤーが歩いたときの再評価に当たる）
// buildmode-enter-only.cs で設置モードへ入った後に実行する。プレイヤー位置から全ブロックの中心へ向かって 300m ずらし、最後に元の位置で判定し直す
// Time each preview-radius re-evaluation when the center moves 1 m at a time in build mode (what a walking player triggers)
// Run after entering build mode with buildmode-enter-only.cs; shifts 300 m from the player toward the center of all blocks, then re-evaluates at the original spot
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using UnityEngine;
using VContainer;

var inv = System.Globalization.CultureInfo.InvariantCulture;
var bf = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
var control = Object.FindFirstObjectByType<UIStateControl>();
if (control == null || control.CurrentState != UIStateEnum.PlaceBlock) return "FAIL not in PlaceBlock: " + control?.CurrentState;

// 半径の判定のチャンク表を取り出す（BlockPreviewRadiusCullingManager.Culling._chunkTable）
// Pull out the radius culling chunk table (BlockPreviewRadiusCullingManager.Culling._chunkTable)
var manager = ClientDIContext.DIContainer.DIContainerResolver.Resolve<BlockPreviewRadiusCullingManager>();
var culling = typeof(BlockPreviewRadiusCullingManager).GetField("Culling", bf).GetValue(manager);
var table = culling.GetType().GetField("_chunkTable", bf).GetValue(culling);
var update = table.GetType().GetMethod("UpdateForCenter", bf);
var center = (Vector3)table.GetType().GetField("_lastCenter", bf).GetValue(table);
const float radius = 20f;

// 全ブロックの中心へ向かう向き（水平）
// Horizontal direction toward the center of all blocks
var blocks = ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values.ToList();
var mid = blocks.Aggregate(Vector3.zero, (s, b) => s + b.transform.position) / blocks.Count;
var direction = mid - center;
direction.y = 0f;
direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.right;

var stepMs = new List<double>();
for (var k = 1; k <= 300; k++)
{
    var sw = Stopwatch.StartNew();
    update.Invoke(table, new object[] { center + direction * k, radius });
    stepMs.Add(sw.Elapsed.TotalMilliseconds);
}
update.Invoke(table, new object[] { center, radius });

var sum = stepMs.Sum();
var sorted = stepMs.OrderBy(v => v).ToList();
string P(double q) => sorted[Mathf.Min(sorted.Count - 1, (int)(q * sorted.Count))].ToString("F3", inv);
return "{\"steps\":" + stepMs.Count + ",\"totalMs\":" + sum.ToString("F1", inv) + ",\"meanMs\":" + (sum / stepMs.Count).ToString("F3", inv)
       + ",\"p50\":" + P(0.5) + ",\"p90\":" + P(0.9) + ",\"p99\":" + P(0.99) + ",\"max\":" + sorted[sorted.Count - 1].ToString("F3", inv)
       + ",\"from\":\"" + center.ToString("F1") + "\",\"direction\":\"" + direction.ToString("F2") + "\"}";
