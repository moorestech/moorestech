// 有効な VisualEffect の数と、アセットごとの生きている粒子数・カリングされていない数を返す
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.VFX;
var d = new Dictionary<string, (int n, int on, long alive, int visible)>();
foreach (var v in Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    var k = v.visualEffectAsset != null ? v.visualEffectAsset.name : "null";
    d.TryGetValue(k, out var e);
    var on = v.enabled && v.gameObject.activeInHierarchy;
    var r = v.GetComponent<Renderer>();
    d[k] = (e.n + 1, e.on + (on ? 1 : 0), e.alive + (on ? v.aliveParticleCount : 0), e.visible + (r != null && r.isVisible ? 1 : 0));
}
return string.Join(" | ", d.OrderByDescending(x => x.Value.n).Select(x => x.Key + " n=" + x.Value.n + " on=" + x.Value.on + " alive=" + x.Value.alive + " visible=" + x.Value.visible));
