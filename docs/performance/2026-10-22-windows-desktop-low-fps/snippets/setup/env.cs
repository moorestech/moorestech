using UnityEngine;
var sb = new System.Text.StringBuilder();
var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
sb.AppendLine("targetFrameRate=" + Application.targetFrameRate + " vSync=" + QualitySettings.vSyncCount + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + " rp=" + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "builtin"));
foreach (var c in cams) sb.AppendLine("cam " + c.name + " enabled=" + c.isActiveAndEnabled + " px=" + c.pixelWidth + "x" + c.pixelHeight + " tex=" + (c.targetTexture ? c.targetTexture.name + c.targetTexture.width + "x" + c.targetTexture.height : "-") + " far=" + c.farClipPlane);
sb.AppendLine("renderers=" + Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length + " gameObjects=" + Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length + " lights=" + Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length);
sb.AppendLine("unityTime fps(approx)=" + (1f / Time.smoothDeltaTime).ToString("F1") + " frameCount=" + Time.frameCount);
return sb.ToString();
