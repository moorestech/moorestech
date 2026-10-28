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
var comboByPos = new Dictionary<UnityEngine.Vector3Int, string>();
var blockStates = new double[1];
var isFirstBundle = new bool[] { true };
var wrap = new Subject<Server.Event.EventMessagePack>();
wrap.Subscribe(e => {
  var tag = e.Tag; var paren = tag.IndexOf(":(");
  var kind = paren < 0 ? tag : tag.Substring(0, paren);
  if (kind == Server.Event.EventReceive.ChangeBlockStateEventPacket.EventTag) {
    // 最初の1束だけ全件復号し、座標→状態キーの組み合わせ表を作る（旧フックのタグ初見時だけの復号に相当）
    if (isFirstBundle[0]) {
      isFirstBundle[0] = false;
      var bundle = MessagePack.MessagePackSerializer.Deserialize<Server.Event.EventReceive.BlockStatesTickBundleMessagePack>(e.Payload);
      foreach (var m in bundle.States) comboByPos[m.Position] = string.Join("+", m.CurrentStateDetail.Keys.OrderBy(x => x));
    }
    // 件数は外箱→Statesの配列ヘッダで数える。内訳は中身を復号せず座標だけ読んで表から引き、表に無い座標は未分類にする
    var reader = new MessagePack.MessagePackReader(new ReadOnlyMemory<byte>(e.Payload));
    reader.ReadArrayHeader();
    var stateCount = reader.ReadArrayHeader();
    blockStates[0] += stateCount;
    for (var i = 0; i < stateCount; i++) {
      reader.ReadArrayHeader(); reader.Skip();
      reader.ReadArrayHeader();
      var pos = new UnityEngine.Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
      var stateKind = "blockState|" + (comboByPos.TryGetValue(pos, out var combo) ? combo : "未分類");
      if (!counts.TryGetValue(stateKind, out var sc)) { sc = new double[2]; counts[stateKind] = sc; }
      sc[0]++;
    }
  }
  if (!counts.TryGetValue(kind, out var c)) { c = new double[2]; counts[kind] = c; }
  c[0]++; c[1] += e.Payload.Length;
  original.OnNext(e);
});
field.SetValue(pem, wrap);
var dom = AppDomain.CurrentDomain;
dom.SetData("bench_ev_counts", counts); dom.SetData("bench_ev_tags", comboByPos);
dom.SetData("bench_ev_blockstates", blockStates);
dom.SetData("bench_ev_pem", pem); dom.SetData("bench_ev_original", original);
dom.SetData("bench_ev_time", UnityEngine.Time.realtimeSinceStartupAsDouble);
return "event hook installed";
