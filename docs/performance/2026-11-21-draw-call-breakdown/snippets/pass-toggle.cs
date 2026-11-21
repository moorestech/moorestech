// 描画のパスを1つずつ切り替える。run.sh が先頭の COND 行を書き換えた写しを送る
// COND: base（全部戻す）/ shadows（全ライトの影を切る）/ ssao / outlines（ScreenSpaceOutlines と OutlinePass）/ camtex（カメラの深度・不透明テクスチャ要求を切る）
// 元の値は AppDomain に1回だけ覚え、base で全部戻す。ProjectSettings やアセットは保存しない（実行時の複製の URP アセットだけを触る）
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
var COND = "base";
var d = System.AppDomain.CurrentDomain;
var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
var renderer = asset.scriptableRenderer;
var features = (List<ScriptableRendererFeature>)typeof(ScriptableRenderer).GetProperty("rendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(renderer);
var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
var cams = Object.FindObjectsByType<UniversalAdditionalCameraData>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

// 元の値を最初の1回だけ覚える
if (d.GetData("pass.lights") == null)
{
    d.SetData("pass.lights", lights.ToDictionary(l => l.GetInstanceID(), l => (int)l.shadows));
    d.SetData("pass.features", features.ToDictionary(f => f.name, f => f.isActive));
    d.SetData("pass.cams", cams.ToDictionary(c => c.GetInstanceID(), c => ((int)c.requiresDepthOption, (int)c.requiresColorOption)));
}
var origLights = (Dictionary<int, int>)d.GetData("pass.lights");
var origFeatures = (Dictionary<string, bool>)d.GetData("pass.features");
var origCams = (Dictionary<int, (int, int)>)d.GetData("pass.cams");

// まず全部元へ戻してから、指定の1つだけ切る
foreach (var l in lights) if (origLights.TryGetValue(l.GetInstanceID(), out var s)) l.shadows = (LightShadows)s;
foreach (var f in features) if (origFeatures.TryGetValue(f.name, out var a)) f.SetActive(a);
foreach (var c in cams) if (origCams.TryGetValue(c.GetInstanceID(), out var o)) { c.requiresDepthOption = (CameraOverrideOption)o.Item1; c.requiresColorOption = (CameraOverrideOption)o.Item2; }

switch (COND)
{
    case "shadows": foreach (var l in lights) l.shadows = LightShadows.None; break;
    case "ssao": foreach (var f in features) if (f.name == "ScreenSpaceAmbientOcclusion") f.SetActive(false); break;
    case "outlines": foreach (var f in features) if (f.name == "ScreenSpaceOutlines" || f.name == "OutlinePass") f.SetActive(false); break;
    case "camtex": foreach (var c in cams) { c.requiresDepthOption = CameraOverrideOption.Off; c.requiresColorOption = CameraOverrideOption.Off; } break;
}
return "cond=" + COND + " renderer=" + renderer.GetType().Name + " features=" + string.Join("/", features.Select(f => f.name + ":" + f.isActive))
    + " shadowLights=" + lights.Count(l => l.shadows != LightShadows.None) + "/" + lights.Length
    + " cams=" + string.Join("/", cams.Select(c => c.name + ":" + c.requiresDepthOption + "," + c.requiresColorOption));
