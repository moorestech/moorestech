// フックを外し、毎秒のイベント件数・payloadバイト数をJSONで返す
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
var tags = (Dictionary<string, string>)dom.GetData("bench_ev_tags");
var ci = System.Globalization.CultureInfo.InvariantCulture;
var total = counts.Values.Sum(v => v[0]); var bytes = counts.Values.Sum(v => v[1]);
var items = counts.OrderByDescending(k => k.Value[0]).Select(k =>
  "\"" + k.Key + "\":{\"perSecond\":" + (k.Value[0] / secs).ToString("F1", ci) + ",\"payloadBytesPerSecond\":" + (k.Value[1] / secs).ToString("F0", ci) + "}");
return "{\"seconds\":" + secs.ToString("F3", ci) + ",\"eventsPerSecond\":" + (total / secs).ToString("F1", ci) + ",\"payloadBytesPerSecond\":" + (bytes / secs).ToString("F0", ci) + ",\"blockStateSenders\":" + tags.Count + ",\"byKind\":{" + string.Join(",", items) + "}}";
