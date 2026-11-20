// ブロックの中のスキンメッシュだけを描画から外し、スキンメッシュを持つブロック種類を数える
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using UnityEngine;
var byType = new Dictionary<string, int>();
var n = 0;
foreach (var b in ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values)
{
    var skinned = b.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    if (skinned.Length == 0) continue;
    foreach (var s in skinned) { s.enabled = false; n++; }
    var e = b.BlockMasterElement;
    var name = (e.GetType().GetProperty("Name")?.GetValue(e) ?? e.GetType().GetField("Name")?.GetValue(e) ?? e.BlockGuid.ToString()).ToString();
    byType[name] = (byType.TryGetValue(name, out var c) ? c : 0) + skinned.Length;
}
return "skinned off=" + n + " byType: " + string.Join(", ", byType.OrderByDescending(x => x.Value).Take(12).Select(x => x.Key + "=" + x.Value));
