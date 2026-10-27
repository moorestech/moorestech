// 受信イベントをタグ種別（座標を除く）とブロック状態キーの組み合わせごとに数えるフックを差し込む
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UniRx;
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
var api = Client.Game.InGame.Context.ClientContext.VanillaApi;
var pem = api.Response.GetType().GetField("_packetExchangeManager", bf).GetValue(api.Response);
var field = pem.GetType().GetField("_eventPacketSubject", bf);
var original = (Subject<Server.Event.EventMessagePack>)field.GetValue(pem);
var counts = new Dictionary<string, double[]>();
var comboByTag = new Dictionary<string, string>();
var blockStates = new double[1];
var wrap = new Subject<Server.Event.EventMessagePack>();
wrap.Subscribe(e => {
  var tag = e.Tag; var paren = tag.IndexOf(":(");
  var kind = paren < 0 ? tag : tag.Substring(0, paren);
  if (kind == Server.Event.EventReceive.ChangeBlockStateEventPacket.EventTag) {
    // 束の中の各件を、状態キーの組み合わせ別に数える。組み合わせは座標ごとに初回だけ求める
    var bundle = MessagePack.MessagePackSerializer.Deserialize<Server.Event.EventReceive.BlockStatesTickBundleMessagePack>(e.Payload);
    foreach (var m in bundle.States) {
      var key = m.Position.ToString();
      if (!comboByTag.TryGetValue(key, out var combo)) {
        combo = string.Join("+", m.CurrentStateDetail.Keys.OrderBy(x => x));
        comboByTag[key] = combo;
      }
      var stateKind = "blockState|" + combo;
      if (!counts.TryGetValue(stateKind, out var sc)) { sc = new double[2]; counts[stateKind] = sc; }
      sc[0]++;
      blockStates[0]++;
    }
  }
  if (!counts.TryGetValue(kind, out var c)) { c = new double[2]; counts[kind] = c; }
  c[0]++; c[1] += e.Payload.Length;
  original.OnNext(e);
});
field.SetValue(pem, wrap);
var dom = AppDomain.CurrentDomain;
dom.SetData("bench_ev_counts", counts); dom.SetData("bench_ev_tags", comboByTag);
dom.SetData("bench_ev_blockstates", blockStates);
dom.SetData("bench_ev_pem", pem); dom.SetData("bench_ev_original", original);
dom.SetData("bench_ev_time", UnityEngine.Time.realtimeSinceStartupAsDouble);
return "event hook installed";
