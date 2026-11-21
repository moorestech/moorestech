// 回転する Transform のうち、祖先にも回転する Transform があるもの（入れ子）を数える。全ワールド（距離外も含む）
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.StateProcessor;
var all = Object.FindObjectsByType<GearStateChangeProcessor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
var infos = all.SelectMany(p => p.RotationInfos.Where(r => r != null).Select(r => (p, r))).ToList();
var set = new HashSet<Transform>(infos.Select(x => x.r.RotationTransform).Where(t => t != null));
var nested = new Dictionary<string, int>();
var nullT = infos.Count(x => x.r.RotationTransform == null);
var dup = infos.Count - infos.Select(x => x.r.RotationTransform).Distinct().Count();
foreach (var t in set)
{
    for (var a = t.parent; a != null; a = a.parent)
    {
        if (!set.Contains(a)) continue;
        var block = t.GetComponentInParent<Client.Game.InGame.Block.BlockGameObject>();
        var key = (block != null ? block.name : "?") + " " + a.name + ">" + t.name;
        nested[key] = nested.TryGetValue(key, out var n) ? n + 1 : 1;
        break;
    }
}
var perProc = all.GroupBy(p => p.RotationInfos.Count).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count());
return "rotations=" + set.Count + " nullTransform=" + nullT + " sameTransformTwice=" + dup + " nested=" + nested.Values.Sum()
    + " infosPerProcessor=" + string.Join(",", perProc) + " | " + string.Join(" / ", nested.OrderByDescending(kv => kv.Value).Take(15).Select(kv => kv.Key + ":" + kv.Value));
