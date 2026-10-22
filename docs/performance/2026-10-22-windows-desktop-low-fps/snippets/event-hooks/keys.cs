using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UniRx;
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
var ev = Client.Game.InGame.Context.ClientContext.VanillaApi.Event;
var dispatcher = ev.GetType().GetField("_dispatcher", bf).GetValue(ev);
var dict = (Dictionary<string, Subject<byte[]>>)dispatcher.GetType().GetField("_eventResponseSubjects", bf).GetValue(dispatcher);
var keyStats = new Dictionary<string, int[]>();
var last = new Dictionary<string, byte[]>();
var subs = new List<IDisposable>();
foreach (var kv in dict) {
  if (!kv.Key.StartsWith("va:event:changeBlockState:")) continue;
  var tag = kv.Key;
  subs.Add(kv.Value.Subscribe(p => {
    var m = MessagePack.MessagePackSerializer.Deserialize<Server.Event.EventReceive.BlockStateMessagePack>(p);
    foreach (var d in m.CurrentStateDetail) {
      var lk = tag + "|" + d.Key;
      bool changed = !last.TryGetValue(lk, out var prev) || !prev.AsSpan().SequenceEqual(d.Value);
      last[lk] = d.Value;
      if (!keyStats.TryGetValue(d.Key, out var s)) { s = new int[2]; keyStats[d.Key] = s; }
      s[0]++; if (changed) s[1]++;
    }
  }));
}
AppDomain.CurrentDomain.SetData("k_stats", keyStats);
AppDomain.CurrentDomain.SetData("k_subs", subs);
return "subscribed " + subs.Count;
