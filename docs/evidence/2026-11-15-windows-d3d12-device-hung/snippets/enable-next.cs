// analyze-view.cs が残した順で、次のブロック種類の描画を戻す（その種類の全ブロック）。戻した種類を返す
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using UnityEngine;
var order = System.AppDomain.CurrentDomain.GetData("moorestech.gpuBisectOrder") as List<string>;
if (order == null || order.Count == 0) return "END";
var guid = order[0];
order.RemoveAt(0);
var n = 0;
var exclude = System.AppDomain.CurrentDomain.GetData("moorestech.gpuBisectExclude") as string;
foreach (var b in ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values)
{
    var g = b.BlockMasterElement.BlockGuid.ToString();
    // "REST" は除外した種類以外の全種類
    // "REST" means every type except the excluded one
    if (guid == "REST" ? g == exclude : g != guid) continue;
    foreach (var r in b.GetComponentsInChildren<Renderer>(true)) { r.enabled = true; n++; }
}
return "enabled " + guid.Substring(0, 8) + " renderers=" + n + " remaining=" + order.Count;
