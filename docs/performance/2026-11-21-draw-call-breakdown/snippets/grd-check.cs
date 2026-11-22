// GPU Resident Drawer が動いているかと、画面に映る Renderer のうち GRD の対象外になるものの数を返す
// 対象外: SkinnedMeshRenderer など MeshRenderer 以外 / MaterialPropertyBlock 付き / DOTS インスタンシングのキーワードを持たないシェーダーの材質
// 画面に映る = isVisible（影だけのものも含む）
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Rendering;
var grdType = typeof(GPUResidentDrawer);
var inst = grdType.GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(r => r.enabled && r.isVisible).ToList();
var notMesh = renderers.Count(r => !(r is MeshRenderer));
var mpb = renderers.Count(r => r is MeshRenderer && r.HasPropertyBlock());
var badShader = new Dictionary<string, int>();
var supported = 0;
var cache = new Dictionary<Shader, bool>();
foreach (var r in renderers)
{
    if (!(r is MeshRenderer) || r.HasPropertyBlock()) continue;
    var ok = true;
    foreach (var m in r.sharedMaterials)
    {
        if (m == null || m.shader == null) continue;
        if (!cache.TryGetValue(m.shader, out var dots)) cache[m.shader] = dots = m.shader.keywordSpace.FindKeyword("DOTS_INSTANCING_ON").isValid;
        if (!dots) { ok = false; badShader[m.shader.name] = badShader.TryGetValue(m.shader.name, out var n) ? n + 1 : 1; }
    }
    if (ok) supported++;
}
return "grdInstance=" + (inst != null) + " visible=" + renderers.Count + " supportedMeshRenderers=" + supported + " notMeshRenderer=" + notMesh + " propertyBlock=" + mpb
    + " unsupportedShaders=" + string.Join(", ", badShader.OrderByDescending(kv => kv.Value).Take(15).Select(kv => kv.Key + ":" + kv.Value));
