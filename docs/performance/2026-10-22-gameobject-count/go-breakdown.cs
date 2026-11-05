// シーン内のGameObjectを「ルート直下の系統」と「主要コンポーネント」で集計する
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
var sb = new StringBuilder();
var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
int active = all.Count(t => t.gameObject.activeInHierarchy);
sb.AppendLine("transforms=" + all.Length + " activeInHierarchy=" + active);
// 系統：最も近い祖先の「分類できるコンポーネント」で分ける
var cls = new Dictionary<string, int[]>();
foreach (var t in all) {
  string key = null; var cur = t;
  while (cur != null && key == null) {
    if (cur.GetComponent<Client.Game.InGame.Block.BlockGameObject>()) key = "Block:" + cur.GetComponent<Client.Game.InGame.Block.BlockGameObject>().BlockMasterElement.Name;
    cur = cur.parent;
  }
  if (key == null) { var root = t.root.name; key = "Root:" + new string(root.TakeWhile(c => !char.IsDigit(c) && c != '(' ).ToArray()).Trim(); 
    if (t.parent != null && t.root != t.parent) { var top = t; while (top.parent != t.root) top = top.parent; key += "/" + new string(top.name.TakeWhile(c => !char.IsDigit(c) && c != '(' && c != '_').ToArray()).Trim(); } }
  if (!cls.TryGetValue(key, out var c)) { c = new int[3]; cls[key] = c; }
  c[0]++; if (t.GetComponent<Renderer>()) c[1]++; if (t.GetComponent<Collider>()) c[2]++;
}
var blockTotal = cls.Where(k => k.Key.StartsWith("Block:")).Aggregate(new int[3], (a, k) => { a[0] += k.Value[0]; a[1] += k.Value[1]; a[2] += k.Value[2]; return a; });
var blockCount = Object.FindObjectsByType<Client.Game.InGame.Block.BlockGameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
sb.AppendLine("ALL BLOCKS blocks=" + blockCount + " gameObjects=" + blockTotal[0] + " renderers=" + blockTotal[1] + " colliders=" + blockTotal[2]);
sb.AppendLine("-- top groups (gameObjects / renderers / colliders)");
foreach (var kv in cls.OrderByDescending(k => k.Value[0]).Take(30)) sb.AppendLine(kv.Value[0].ToString().PadLeft(7) + " " + kv.Value[1].ToString().PadLeft(7) + " " + kv.Value[2].ToString().PadLeft(7) + "  " + kv.Key);
// 主要コンポーネント数
sb.AppendLine("-- components");
System.Action<string, int> line = (n, v) => sb.AppendLine(v.ToString().PadLeft(8) + "  " + n);
line("MeshRenderer", Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
line("SkinnedMeshRenderer", Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
line("Collider", Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
line("MeshCollider", Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
line("Rigidbody", Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
line("Animator", Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
line("VisualEffect", Object.FindObjectsByType<UnityEngine.VFX.VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
line("ParticleSystem", Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
line("Light", Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
line("Canvas", Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
var mbs = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
line("MonoBehaviour(all)", mbs.Length);
sb.AppendLine("-- MonoBehaviour types top 20");
foreach (var g in mbs.GroupBy(m => m.GetType().Name).OrderByDescending(g => g.Count()).Take(20)) line(g.Key, g.Count());
// 描画中のRendererの割合
var rs = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
sb.AppendLine("renderers(active)=" + rs.Length + " enabled=" + rs.Count(r => r.enabled) + " isVisible=" + rs.Count(r => r.isVisible));
System.IO.File.WriteAllText(@"C:\moorestech-perf\go-breakdown.txt", sb.ToString());
return "ok";
