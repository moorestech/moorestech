// tick-probe-start.cs で差し替えた Tick の一覧を元に戻し、Tick ごとの 1フレームあたり ms を多い順に JSON で返す
using System;
using System.Collections.Generic;
using System.Linq;
var d = AppDomain.CurrentDomain;
var restore = (List<Action>)d.GetData("tickprobe.restore");
if (restore == null) return "{\"error\":\"not running\"}";
foreach (var r in restore) r();
var stats = (Dictionary<string, double[]>)d.GetData("tickprobe.stats");
var frames = UnityEngine.Time.frameCount - (int)d.GetData("tickprobe.frame");
d.SetData("tickprobe.restore", null); d.SetData("tickprobe.stats", null);
var ci = System.Globalization.CultureInfo.InvariantCulture;
string J(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
return "{\"frames\":" + frames + ",\"totalMsPerFrame\":" + (stats.Values.Sum(v => v[0]) / frames).ToString("F3", ci) + ",\"ticks\":[" + string.Join(",", stats.OrderByDescending(kv => kv.Value[0]).Select(kv =>
    "{\"name\":" + J(kv.Key) + ",\"msPerFrame\":" + (kv.Value[0] / frames).ToString("F3", ci) + ",\"callsPerFrame\":" + (kv.Value[1] / frames).ToString("F2", ci) + "}")) + "]}";
