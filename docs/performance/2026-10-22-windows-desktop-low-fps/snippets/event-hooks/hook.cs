using System;
using System.Collections.Generic;
using System.Reflection;
using UniRx;
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
var ev = Client.Game.InGame.Context.ClientContext.VanillaApi.Event;
var dispatcher = ev.GetType().GetField("_dispatcher", bf).GetValue(ev);
var dict = (Dictionary<string, Subject<byte[]>>)dispatcher.GetType().GetField("_eventResponseSubjects", bf).GetValue(dispatcher);
var stats = new Dictionary<string, double[]>();
var originals = new Dictionary<string, Subject<byte[]>>(dict);
foreach (var kv in originals) {
  var tag = kv.Key; var orig = kv.Value; var wrap = new Subject<byte[]>();
  stats[tag] = new double[3];
  wrap.Subscribe(p => { var sw = System.Diagnostics.Stopwatch.StartNew(); orig.OnNext(p); var s = stats[tag]; s[0]++; s[1] += sw.Elapsed.TotalMilliseconds; s[2] += p.Length; });
  dict[tag] = wrap;
}
AppDomain.CurrentDomain.SetData("perf_stats", stats);
AppDomain.CurrentDomain.SetData("perf_orig", originals);
AppDomain.CurrentDomain.SetData("perf_dict", dict);
AppDomain.CurrentDomain.SetData("perf_frame", UnityEngine.Time.frameCount);
AppDomain.CurrentDomain.SetData("perf_time", UnityEngine.Time.realtimeSinceStartupAsDouble);
return "hooked " + dict.Count + " tags";
