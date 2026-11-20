// skinned-stats.cs が残した順（スキンメッシュの少ない種類から）で、次の種類のスキンメッシュを戻す
using System.Collections.Generic;
using Client.Game.InGame.Context;
using UnityEngine;
var order = System.AppDomain.CurrentDomain.GetData("moorestech.skinnedOrder") as List<string>;
if (order == null || order.Count == 0) return "END";
var name = order[0];
order.RemoveAt(0);
var n = 0;
foreach (var b in ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values)
{
    var e = b.BlockMasterElement;
    var bn = (e.GetType().GetProperty("Name")?.GetValue(e) ?? e.GetType().GetField("Name")?.GetValue(e) ?? e.BlockGuid.ToString()).ToString();
    if (bn != name) continue;
    foreach (var s in b.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { s.enabled = true; n++; }
}
return "enabled skinned of " + name + " count=" + n + " remaining=" + order.Count;
