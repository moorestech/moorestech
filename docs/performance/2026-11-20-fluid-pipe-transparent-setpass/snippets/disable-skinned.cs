// パイプの負荷だけを比べるため、プレイヤー以外の SkinnedMeshRenderer を無効にする（GPU のスキン変形でデバイス喪失が起きる疑いがあるため）
// 何度呼んでもよい。今回切った数と、切った後に有効なまま残る数、ブロック名ごとの内訳（上位5）を返す
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Player;
using UnityEngine;

var all = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
var targets = all.Where(r => r.GetComponentInParent<PlayerObjectController>(true) == null).ToList();
var disabledNow = 0;
foreach (var r in targets)
{
    if (!r.enabled) continue;
    r.enabled = false;
    disabledNow++;
}
var byBlock = targets.GroupBy(r => r.GetComponentInParent<BlockGameObject>(true) is { } b ? b.BlockMasterElement.Name : "(not block)")
    .OrderByDescending(g => g.Count()).Take(5).Select(g => g.Key + "=" + g.Count());
return $"skinned total={all.Length} targets={targets.Count} disabledNow={disabledNow} stillEnabled={targets.Count(r => r.enabled)} top=[{string.Join(", ", byBlock)}]";
