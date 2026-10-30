// フックを外し集計をJSON返却
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
var dom = AppDomain.CurrentDomain;
var pem = dom.GetData("bench_ev_pem");
pem.GetType().GetField("_eventPacketSubject", bf).SetValue(pem, dom.GetData("bench_ev_original"));
double secs = UnityEngine.Time.realtimeSinceStartupAsDouble - (double)dom.GetData("bench_ev_time");
var counts = (Dictionary<string, double[]>)dom.GetData("bench_ev_counts");
var tags = (Dictionary<UnityEngine.Vector3Int, string>)dom.GetData("bench_ev_tags");
var blockStates = (double[])dom.GetData("bench_ev_blockstates");
var ci = System.Globalization.CultureInfo.InvariantCulture;
var total = counts.Where(k => !k.Key.StartsWith("blockState|")).Sum(k => k.Value[0]); var bytes = counts.Values.Sum(v => v[1]);
var items = counts.OrderByDescending(k => k.Value[0]).Select(k =>
  "\"" + k.Key + "\":{\"perSecond\":" + (k.Value[0] / secs).ToString("F1", ci) + ",\"payloadBytesPerSecond\":" + (k.Value[1] / secs).ToString("F0", ci) + "}");
return "{\"seconds\":" + secs.ToString("F3", ci) + ",\"eventsPerSecond\":" + (total / secs).ToString("F1", ci) + ",\"payloadBytesPerSecond\":" + (bytes / secs).ToString("F0", ci) + ",\"blockStatesPerSecond\":" + (blockStates[0] / secs).ToString("F1", ci) + ",\"blockStateSenders\":" + tags.Count + ",\"byKind\":{" + string.Join(",", items) + "}}";
