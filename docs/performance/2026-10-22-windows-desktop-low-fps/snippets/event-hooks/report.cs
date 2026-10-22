using System;
using System.Linq;
using System.Collections.Generic;
using UniRx;
var stats = (Dictionary<string, double[]>)AppDomain.CurrentDomain.GetData("perf_stats");
var dict = (Dictionary<string, Subject<byte[]>>)AppDomain.CurrentDomain.GetData("perf_dict");
var orig = (Dictionary<string, Subject<byte[]>>)AppDomain.CurrentDomain.GetData("perf_orig");
foreach (var kv in orig) dict[kv.Key] = kv.Value;
int frames = UnityEngine.Time.frameCount - (int)AppDomain.CurrentDomain.GetData("perf_frame");
double secs = UnityEngine.Time.realtimeSinceStartupAsDouble - (double)AppDomain.CurrentDomain.GetData("perf_time");
var sb = new System.Text.StringBuilder();
sb.AppendLine("frames=" + frames + " secs=" + secs.ToString("F1") + " fps=" + (frames / secs).ToString("F1"));
foreach (var kv in stats.Where(k => k.Value[0] > 0).OrderByDescending(k => k.Value[1]))
  sb.AppendLine(kv.Key.PadRight(48) + " n/s=" + (kv.Value[0] / secs).ToString("F0").PadLeft(6) + " ms/frame=" + (kv.Value[1] / frames).ToString("F2").PadLeft(7) + " KB/s=" + (kv.Value[2] / 1024 / secs).ToString("F0"));
return sb.ToString();
