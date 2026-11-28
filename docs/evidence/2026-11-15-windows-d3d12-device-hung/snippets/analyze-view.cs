// ブロックの描画を切った状態で、今のカメラの視錐台に入るブロック種類を、映る頂点数の多い順に集計し、その順を AppDomain に残す（enable-next.cs が使う）
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using UnityEngine;
var cam = Camera.main;
var planes = GeometryUtility.CalculateFrustumPlanes(cam);
var blocksInView = new Dictionary<string, int>();
var renderersInView = new Dictionary<string, int>();
var vertsInView = new Dictionary<string, long>();
var nameOf = new Dictionary<string, string>();
var shaders = new Dictionary<string, long>();
var top = new List<(int verts, string desc)>();
foreach (var b in ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values)
{
    var guid = b.BlockMasterElement.BlockGuid.ToString();
    var inView = false;
    foreach (var r in b.GetComponentsInChildren<Renderer>(true))
    {
        if (!r.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
        inView = true;
        var mf = r.GetComponent<MeshFilter>();
        var v = mf && mf.sharedMesh ? mf.sharedMesh.vertexCount : (r is SkinnedMeshRenderer s && s.sharedMesh ? s.sharedMesh.vertexCount : 0);
        var shader = r.sharedMaterial ? r.sharedMaterial.shader.name : "(none)";
        renderersInView[guid] = (renderersInView.TryGetValue(guid, out var rc) ? rc : 0) + 1;
        vertsInView[guid] = (vertsInView.TryGetValue(guid, out var vc) ? vc : 0) + v;
        shaders[shader] = (shaders.TryGetValue(shader, out var sc) ? sc : 0) + v;
        top.Add((v, r.name + "/" + shader));
    }
    if (!inView) continue;
    blocksInView[guid] = (blocksInView.TryGetValue(guid, out var bc) ? bc : 0) + 1;
    if (!nameOf.ContainsKey(guid))
    {
        var e = b.BlockMasterElement;
        var p = e.GetType().GetProperty("Name");
        var f = e.GetType().GetField("Name");
        nameOf[guid] = (p?.GetValue(e) ?? f?.GetValue(e) ?? guid.Substring(0, 8)).ToString();
    }
}
var order = vertsInView.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
System.AppDomain.CurrentDomain.SetData("moorestech.gpuBisectOrder", order);
return "cam=" + cam.transform.position.ToString("F1") + " euler=" + cam.transform.eulerAngles.ToString("F1") + " types=" + order.Count
  + "\n" + string.Join("\n", order.Select((g, i) => i + " " + g.Substring(0, 8) + " blocks=" + blocksInView[g] + " renderers=" + renderersInView[g] + " verts=" + vertsInView[g] + " name=" + nameOf[g]))
  + "\nshaders: " + string.Join(" | ", shaders.OrderByDescending(x => x.Value).Take(10).Select(x => x.Key + ":" + x.Value))
  + "\ntopMeshes: " + string.Join(" | ", top.OrderByDescending(x => x.verts).Take(8).Select(x => x.desc + ":" + x.verts));
