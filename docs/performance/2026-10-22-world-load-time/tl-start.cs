using System;
using System.Collections.Generic;
using UnityEngine;
var t0 = Time.realtimeSinceStartupAsDouble;
var logs = new List<string>();
var samples = new List<string>();
Application.LogCallback cb = (msg, stack, type) => {
  var line = msg.Split('\n')[0]; if (line.Length > 160) line = line.Substring(0, 160);
  lock (logs) logs.Add((Time.realtimeSinceStartupAsDouble - t0).ToString("F1") + "s [" + type + "] " + line);
};
Application.logMessageReceivedThreaded += cb;
double lastSample = -10; int lastFrame = Time.frameCount;
UnityEditor.EditorApplication.CallbackFunction upd = () => {
  var now = Time.realtimeSinceStartupAsDouble - t0;
  if (now - lastSample < 2) return;
  var frames = Time.frameCount - lastFrame; var fps = frames / (now - lastSample);
  lastSample = now; lastFrame = Time.frameCount;
  var r = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
  samples.Add(now.ToString("F1") + "s fps=" + fps.ToString("F1") + " renderers=" + r + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
};
UnityEditor.EditorApplication.update += upd;
var dom = AppDomain.CurrentDomain;
dom.SetData("tl_cb", cb); dom.SetData("tl_upd", upd); dom.SetData("tl_logs", logs); dom.SetData("tl_samples", samples);
return "timeline started";
