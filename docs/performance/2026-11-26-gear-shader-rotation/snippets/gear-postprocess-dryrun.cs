// 歯車を持つ prefab 全部に後処理をかけ、CPU に残った部品と理由、読み取り不可のメッシュの FBX を集める
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Common.PrefabPostProcess;
using Client.Game.InGame.Block.GearRotation.PrefabPostProcess;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEditor;
using UnityEngine;
var settings = AssetDatabase.LoadAssetAtPath<GearRotationRenderSettings>("Assets/Asset/Common/Shader/GearRotation/GearRotationRenderSettings.asset");
var pipeline = new PrefabPostProcessPipeline("DryRunTemplates", new IPrefabPostProcessor[] { new GearRotationPrefabPostProcessor(settings) });
var sb = new StringBuilder();
var reasons = new Dictionary<string, int>();
var unreadable = new HashSet<string>();
int gpu = 0, cpu = 0;
GameObject root = null;
foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/AddressableResources" }))
{
    var path = AssetDatabase.GUIDToAssetPath(guid);
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    if (prefab == null || prefab.GetComponentInChildren<GearStateChangeProcessor>(true) == null) continue;
    var template = pipeline.Process(prefab);
    root = template.transform.parent.gameObject;
    foreach (var p in template.GetComponentsInChildren<GearStateChangeProcessor>(true))
    foreach (var info in p.RotationInfos.OfType<TransformRotationInfo>())
    {
        if (info.IsGpuDriven) { gpu++; continue; }
        cpu++;
        GearRotationMergeEligibility.TryCollect(template, info, new PrefabPostProcessContext(), settings.SourceLitShader, out _, out var reason);
        var key = System.Text.RegularExpressions.Regex.Replace(reason ?? "?", @"(メッシュ |マテリアル)\S+", "$1*");
        reasons[key] = reasons.TryGetValue(key, out var c) ? c + 1 : 1;
        sb.AppendLine(prefab.name + "/" + (info.RotationTransform == null ? "null" : info.RotationTransform.name) + ": " + reason);
        if (info.RotationTransform != null)
            foreach (var mf in info.RotationTransform.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !mf.sharedMesh.isReadable) unreadable.Add(AssetDatabase.GetAssetPath(mf.sharedMesh));
    }
}
if (root != null) Object.DestroyImmediate(root);
return "gpu=" + gpu + " cpu=" + cpu + "\nREASONS\n" + string.Join("\n", reasons.OrderByDescending(kv => kv.Value).Select(kv => kv.Value + " " + kv.Key)) + "\nUNREADABLE\n" + string.Join("\n", unreadable.OrderBy(x => x)) + "\nDETAIL\n" + sb;
