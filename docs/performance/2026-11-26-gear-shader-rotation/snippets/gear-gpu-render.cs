// 動いているシェーダー回転の歯車の前に一時カメラを置き、シェーダーの時刻だけを 0.25 秒ずらして2回描いて PNG に保存し、差の画素数を返す
using System.IO;
using System.Linq;
using Client.Game.InGame.BlockSystem.StateProcessor;
var main = Camera.main;
var target = Object.FindObjectsByType<GearStateChangeProcessor>(FindObjectsSortMode.None)
    .Where(p => p.DebugCurrentGearState != null && p.DebugCurrentGearState.CurrentRpm > 0)
    .SelectMany(p => p.RotationInfos.OfType<TransformRotationInfo>().Where(i => i.IsGpuDriven).Select(i => (p, i)))
    .Select(x => (x.p, x.i, host: x.i.RotationTransform.parent.Find(x.i.RotationTransform.name + " (GearRotation)")))
    .Where(x => x.host != null)
    .OrderBy(x => Vector3.Distance(main.transform.position, x.host.position)).First();
var renderer = target.host.GetComponent<MeshRenderer>();
var axisWorld = target.host.TransformDirection(GearWorldRotationSign.ToAxisVector(target.i.Axis));
var radius = renderer.bounds.extents.magnitude;
var go = new GameObject("GearCheckCamera");
var cam = go.AddComponent<Camera>();
cam.CopyFrom(main);
cam.fieldOfView = 40f;
cam.transform.position = renderer.bounds.center + axisWorld * (radius * 3f) + Vector3.up * 0.01f;
cam.transform.LookAt(renderer.bounds.center);
var rt = new RenderTexture(512, 512, 24);
cam.targetTexture = rt;
var timeId = Shader.PropertyToID("_GearRotationTime");
var now = Shader.GetGlobalFloat(timeId);
byte[] Shot(float t, string path)
{
    Shader.SetGlobalFloat(timeId, t);
    cam.Render();
    RenderTexture.active = rt;
    var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
    tex.Apply();
    RenderTexture.active = null;
    File.WriteAllBytes(path, tex.EncodeToPNG());
    var raw = tex.GetRawTextureData();
    Object.DestroyImmediate(tex);
    return raw;
}
var a = Shot(now, "C:/moorestech-perf/gear-a.png");
var b = Shot(now + 0.25f, "C:/moorestech-perf/gear-b.png");
var a2 = Shot(now, "C:/moorestech-perf/gear-a2.png");
Shader.SetGlobalFloat(timeId, now);
int Diff(byte[] x, byte[] y) { var n = 0; for (var k = 0; k < x.Length; k += 3) if (System.Math.Abs(x[k] - y[k]) + System.Math.Abs(x[k + 1] - y[k + 1]) + System.Math.Abs(x[k + 2] - y[k + 2]) > 30) n++; return n; }
cam.targetTexture = null; Object.DestroyImmediate(go); rt.Release();
return target.host.name + " block=" + target.p.transform.root.name + " rpm=" + target.p.DebugCurrentGearState.CurrentRpm + " axis=" + target.i.Axis + " speed=" + target.i.SignedSpeedFactor
    + " diff(t, t+0.25)=" + Diff(a, b) + " diff(t, t again)=" + Diff(a, a2) + " of " + (512 * 512);
