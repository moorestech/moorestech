// 設置プレビュー枠と当たり判定の数を、ブロック配下とそれ以外に分けて数える（変更前後どちらのコミットでもコンパイルできるよう型は名前で引く）
// Count placement preview frames and hit faces under blocks and elsewhere (types are looked up by name so this compiles on both commits)
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
    sb.Append(",\"" + typeName + "\":{\"total\":" + found.Length + ",\"underBlock\":" + underBlock + ",\"activeInHierarchy\":" + active + "}");
}
sb.Append("}");
return sb.ToString();
