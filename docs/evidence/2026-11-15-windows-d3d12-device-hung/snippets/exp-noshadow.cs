using UnityEngine;
QualitySettings.shadows = ShadowQuality.Disable;
var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
foreach (var l in lights) l.shadows = LightShadows.None;
return "shadows off, lights=" + lights.Length;
