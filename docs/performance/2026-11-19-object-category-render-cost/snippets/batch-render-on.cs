// batchmode の Editor では Game ビューが無くカメラが描かれないので、PostLateUpdate の最後で Camera.main を 1920x1080 の RenderTexture へ毎フレーム描かせる
// 描画にかかった時間（Stopwatch）は AppDomain の "camprobe.batchRenderMs" に毎フレーム上書きする
using System.Linq;
using UnityEngine;
using UnityEngine.LowLevel;
var key = "camprobe.batchRender";
var d = System.AppDomain.CurrentDomain;
if (d.GetData(key) != null) return "already installed";
if (!Application.isBatchMode) return "skip: not batchmode";
var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { name = "camprobe.batchTarget" };
rt.Create();
d.SetData("camprobe.batchRenderMs", 0.0);
d.SetData("camprobe.batchRenderCount", 0);
var sw = new System.Diagnostics.Stopwatch();
PlayerLoopSystem.UpdateFunction tick = () =>
{
    var cam = Camera.main;
    if (cam == null) return;
    if (cam.targetTexture != rt) cam.targetTexture = rt;
    sw.Restart();
    cam.Render();
    sw.Stop();
    d.SetData("camprobe.batchRenderMs", sw.Elapsed.TotalMilliseconds);
    d.SetData("camprobe.batchRenderCount", (int)d.GetData("camprobe.batchRenderCount") + 1);
};
var loop = PlayerLoop.GetCurrentPlayerLoop();
var subs = loop.subSystemList;
var idx = System.Array.FindIndex(subs, s => s.type == typeof(UnityEngine.PlayerLoop.PostLateUpdate));
var list = subs[idx].subSystemList.ToList();
list.Add(new PlayerLoopSystem { type = typeof(System.Diagnostics.Stopwatch), updateDelegate = tick });
subs[idx].subSystemList = list.ToArray();
loop.subSystemList = subs;
PlayerLoop.SetPlayerLoop(loop);
d.SetData(key, "on");
return "batch render installed";
