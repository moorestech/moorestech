// スキンメッシュを持つブロック種類ごとに、ブロック数・スキンメッシュ数・頂点数・ボーン数・ブレンドシェイプ数を返し、戻す順を AppDomain に残す
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using UnityEngine;
var stats = new Dictionary<string, (int blocks, int skinned, long verts, int maxBones, int maxBlend, string mesh)>();
foreach (var b in ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values)
{
    var skinned = b.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    if (skinned.Length == 0) continue;
    var e = b.BlockMasterElement;
    var name = (e.GetType().GetProperty("Name")?.GetValue(e) ?? e.GetType().GetField("Name")?.GetValue(e) ?? e.BlockGuid.ToString()).ToString();
    var cur = stats.TryGetValue(name, out var c) ? c : (blocks: 0, skinned: 0, verts: 0L, maxBones: 0, maxBlend: 0, mesh: "");
    var verts = skinned.Sum(s => (long)(s.sharedMesh ? s.sharedMesh.vertexCount : 0));
    var bones = skinned.Max(s => s.bones.Length);
    var blend = skinned.Max(s => s.sharedMesh ? s.sharedMesh.blendShapeCount : 0);
    var mesh = skinned.OrderByDescending(s => s.sharedMesh ? s.sharedMesh.vertexCount : 0).First();
    stats[name] = (cur.blocks + 1, cur.skinned + skinned.Length, cur.verts + verts, Mathf.Max(cur.maxBones, bones), Mathf.Max(cur.maxBlend, blend), mesh.sharedMesh ? mesh.sharedMesh.name + ":" + mesh.sharedMesh.vertexCount : "");
}
System.AppDomain.CurrentDomain.SetData("moorestech.skinnedOrder", stats.Keys.OrderBy(k => stats[k].skinned).ToList());
return string.Join("\n", stats.OrderBy(x => x.Value.skinned).Select(x => x.Key + ": blocks=" + x.Value.blocks + " skinned=" + x.Value.skinned + " verts=" + x.Value.verts + " maxBones=" + x.Value.maxBones + " maxBlendShapes=" + x.Value.maxBlend + " biggest=" + x.Value.mesh))
  + "\nquality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + " skinWeights=" + QualitySettings.skinWeights;
