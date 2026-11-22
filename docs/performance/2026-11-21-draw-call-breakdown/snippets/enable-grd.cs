// GPU Resident Drawer を有効にする設定をアセットへ書いて保存する（PlayMode の外で実行する）
// 1) 品質設定が使う URP アセット全部の GPU Resident Drawer を Instanced Drawing に 2) その renderer data を全部 Forward+ に 3) BatchRendererGroup のバリアントを全部残す
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
var log = new List<string>();
var assets = new HashSet<UniversalRenderPipelineAsset>();
for (var i = 0; i < QualitySettings.count; i++)
    if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset a) assets.Add(a);
if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset def) assets.Add(def);
foreach (var a in assets)
{
    var so = new SerializedObject(a);
    so.FindProperty("m_GPUResidentDrawerMode").intValue = (int)GPUResidentDrawerMode.InstancedDrawing;
    so.ApplyModifiedPropertiesWithoutUndo();
    var list = so.FindProperty("m_RendererDataList");
    for (var r = 0; r < list.arraySize; r++)
    {
        var data = list.GetArrayElementAtIndex(r).objectReferenceValue as UniversalRendererData;
        if (data == null) continue;
        data.renderingMode = RenderingMode.ForwardPlus;
        EditorUtility.SetDirty(data);
        log.Add("renderer " + AssetDatabase.GetAssetPath(data) + " -> " + data.renderingMode);
    }
    EditorUtility.SetDirty(a);
    log.Add("asset " + AssetDatabase.GetAssetPath(a) + " grd=" + a.gpuResidentDrawerMode + " supported=" + a.IsGPUResidentDrawerSupportedBySRP(out var msg, out _) + " " + msg);
}
// EditorGraphicsSettings の値は読み取り専用なので、GraphicsSettings の SerializedObject で書く
var gs = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
gs.FindProperty("m_BrgStripping").intValue = (int)UnityEditor.Rendering.BatchRendererGroupStrippingMode.KeepAll;
gs.ApplyModifiedPropertiesWithoutUndo();
log.Add("brgStripping=" + UnityEditor.Rendering.EditorGraphicsSettings.batchRendererGroupShaderStrippingMode);
AssetDatabase.SaveAssets();
return string.Join("\n", log.Distinct());
