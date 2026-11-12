// 設置プレビュー枠と当たり判定の数を、ブロック配下とそれ以外に分けて数える（変更前後どちらのコミットでもコンパイルできるよう型は名前で引く）
// Count placement preview frames and hit faces under blocks and elsewhere (types are looked up by name so this compiles on both commits)
// rendered は有効かつ配下の Renderer が1つでも enabled の数（設置モードで実際に描かれている枠。半径20m外は有効のまま描画だけ切られる）
// rendered counts active ones with at least one enabled Renderer below (frames actually drawn in build mode; outside 20 m they stay active with renderers off)
using System.Linq;
using UnityEngine;
var blocks = Object.FindObjectsByType<Client.Game.InGame.Block.BlockGameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
var sb = new System.Text.StringBuilder("{\"blocks\":" + blocks.Length);
foreach (var typeName in new[] { "BlockPreviewBoundingBox", "BlockPreviewBoundingBoxSurface", "BlockPlacementHitFaces", "PreviewOnlyObject" })
{
    var type = typeof(Client.Game.InGame.Block.BlockGameObject).Assembly.GetTypes().FirstOrDefault(t => t.Name == typeName && typeof(Component).IsAssignableFrom(t));
    if (type == null) { sb.Append(",\"" + typeName + "\":null"); continue; }
    var found = Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>().ToArray();
    var underBlock = found.Count(c => c.GetComponentInParent<Client.Game.InGame.Block.BlockGameObject>(true) != null);
    var active = found.Count(c => c.gameObject.activeInHierarchy);
    var rendered = found.Count(c => c.gameObject.activeInHierarchy && c.GetComponentsInChildren<Renderer>().Any(r => r.enabled));
    sb.Append(",\"" + typeName + "\":{\"total\":" + found.Length + ",\"underBlock\":" + underBlock + ",\"activeInHierarchy\":" + active + ",\"rendered\":" + rendered + "}");
}
sb.Append("}");
return sb.ToString();
