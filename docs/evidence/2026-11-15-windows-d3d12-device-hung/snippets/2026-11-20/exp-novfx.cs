// ブロックの VisualEffect（VFX Graph）を全部止め、種類ごとの数を返す
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.VFX;
var byAsset = new Dictionary<string, int>(); var n = 0; var alive = 0;
foreach (var v in Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    if (v.enabled && v.gameObject.activeInHierarchy) alive += v.aliveParticleCount;
    v.enabled = false; n++;
    var k = v.visualEffectAsset != null ? v.visualEffectAsset.name : "null";
    byAsset[k] = (byAsset.TryGetValue(k, out var c) ? c : 0) + 1;
}
return "vfx off=" + n + " aliveBefore=" + alive + " byAsset: " + string.Join(", ", byAsset.OrderByDescending(x => x.Value).Select(x => x.Key + "=" + x.Value));
