# ブロック状態のtick束ね送信 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ブロック状態の変化を座標ごとの最新1件に溜め、tick末尾に1イベント（ブロック状態の束）で送り、クライアントは座標→BlockGameObjectの表で振り分ける。

**Architecture:** サーバーの `ChangeBlockStateEventPacket` は即時送信をやめ、`_pendingStates`（座標→最新の `BlockStateMessagePack`）へ上書きで溜める。`MasterTickUpdater` が tick末尾（操作適用・別スレッド処理・網の再構築の後、境界ハッシュの前）に `BroadcastPendingStates()` を1回呼び、`BlockStatesTickBundleMessagePack` を1イベントで全員へ送る。クライアントは新設の `BlockStateEventHandler` が束を1回だけデシリアライズし、`BlockGameObjectDataStore.TryGetBlockGameObject(座標)` で引いた `BlockGameObject.ApplyBlockState` へ渡す。WebUI の `BlockInventoryTopic` は `BlockGameObject.OnBlockStateChanged` を購読する。

**Tech Stack:** C# / Unity 6000.3 / MessagePack-CSharp / UniRx / VContainer / NUnit

## Requirements

- R1: サーバーはブロック状態の変化をその場で送らず、座標ごとの最新1件へ上書きで溜める。受け入れ: 同じtick（＝`BroadcastPendingStates` の間）に同じ座標が2回変わったら、束にはその座標が1件だけ、後の状態で入る。
- R2: 溜めた分は tick末尾（`WorldMutationTickEndUpdater`・`ServerThreadActionQueue`・`RebuildTopologiesIfDirty` の後、`_trainUpdateService.CompleteTick()` の前）に1イベント `va:event:blockStatesTickBundle` で全員へ送る。受け入れ: `GameUpdater.UpdateOneTick()` の後、そのtickに状態が変わったブロックは全部1イベントに入っている。変化0件のtickではイベントが送られない。
- R3: 前回と同じ中身なら溜めない判定（`ChangeState`）を今どおり残す。`ForceChangeState`（`RequestBlockState` の初期状態の問い合わせ）は判定なしで溜める。受け入れ: 既存の Dedup テスト群を束の形へ書き換えて通る。
- R4: 撤去・設置のとき、その座標に溜めた分を捨てる。受け入れ: 状態変化→撤去→送信で、その座標が束に入らない。
- R5: クライアントは束を1か所で購読して1回だけデシリアライズし、座標でブロックを引いて状態プロセッサを呼ぶ。座標入りタグ（`CreateSpecifiedBlockEventTag`）の購読と、そのメソッド自体を廃止する。座標にブロックが無い件は、理由をログに出して読み飛ばす。受け入れ: リポジトリ全体で `CreateSpecifiedBlockEventTag` の参照が0件。unityプレイ録画テストで、パイプの水位が届くこと・撤去して再設置した同じ座標にも状態が届くことを確認する。
- R6: WebUI の `BlockInventoryTopic` は、同じ振り分け経路（`BlockGameObject.OnBlockStateChanged`）から再配信のきっかけを受ける。受け入れ: タグを直接購読するコードが消え、コンパイルが通る。プレイ録画テストで `OnBlockStateChanged` の発火を確認する。
- R7: 完了基準として、windows-desktop の固定ベンチマークを変更の前後で回し、`docs/performance/benchmark/results/` と README の結果一覧に残す。数値目標は置かない。受け入れ: 前後それぞれ3回分の JSON と、compare.py の表を README に記録する。
- やらないこと: 束の1件の中身（`Dictionary<string, byte[]>`）の型付き化。パイプの状態配信の間引き・量子化。ワールドデータの定期全量取得の見直し。設置・撤去イベントの束ね。`EventMessagePack.MessagePacks`（Key 2。未使用）の整理。

## Global Constraints

- コメントは「// 日本語 → // English」の2行セットを約3〜10行ごとに入れる。各言語1行に収める。
- 1ファイル200行以下、1ディレクトリ10ファイルまで。`partial`・`Func<>`・デフォルト引数は禁止。
- イベント通知は UniRx（`Subject`／`IObservable`）。C# の `event`／`Action` を発火に使わない。
- try-catch は原則禁止。例外は既存の隔離（`BlockGameObject` の状態プロセッサ呼び出しごとの try-catch）を移すときだけ。移すときはログの文言を変えない。
- fail-closed で読み飛ばす経路は、理由をログに出す（無音の縮退は禁止）。
- MessagePack の DTO は、既存の `BlockStateMessagePack` と同じく `[Key(n)] { get; set; }` と `[Obsolete]` 付きの引数なしコンストラクタを持つ。Event の DTO は Key(0) から始める。
- `.meta` を手で作らない。Prefab・シーンを手で編集しない。
- C# を変えたら必ず `uloop compile --project-path ./moorestech_client` を実行する（サーバーのコードもクライアントのプロジェクトからコンパイルされる）。
- サーバーの経過時間は tick で扱う（今回の変更では新しい時間計測は入れない）。
- セーブ形式は変えない（`WorldSaveAllInfo.CurrentVersion` は触らない）。
- 作業ブランチは `performance`。タスクごとにコミットする（プレーンな `git commit`。`--author`・trailer を付けない）。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / 前例引用の誤り（購読駆動の前例を明示呼び出しの根拠にしていた）を新規パターンとして比較付きで書き直し、撤去アニメ中の退化は前提宣言で確定。取りこぼし3件（events-collect.cs・README の events 列・keys.cs の注記）を追加、ハンドラを World/ へ移動
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱3・第3バケツ0 / 弱3件をユーザーに1問で提示し、③の重複だけ SerializeState・QueueState・ForgetPosition にまとめた。ADR 矛盾なし。修正範囲の再検査（fresh-context）: 違反1（(b) 却下理由の事実誤り→差し替え）・弱1（取り出しの重複→③の範囲でまとめた）

---

## 配置と前例

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `ChangeBlockStateEventPacket` の溜め込み・`BroadcastPendingStates` | Server.Event（既存クラスの変更） | `EventProtocolProvider.AddBroadcastEvent` を tick末尾に1回 | 「tick単位で1イベントにまとめる」点は `TrainUnitTickDiffBundleEventPacket`・`BeltTransportTickEventPacket` と同じ。駆動の仕方は異なる（#3） |
| 2 | `BlockStatesTickBundleMessagePack` | Server.Event（`ChangeBlockStateEventPacket.cs` 内。`BlockStateMessagePack` の隣） | MessagePack DTO | `BlockStateMessagePack`（同じファイル） |
| 3 | `MasterTickUpdater` からの送信呼び出し | Server.Boot | tick順の明示呼び出し（購読順に頼らない） | **新規パターン**（下の「送信の駆動の比較」） |
| 4 | `BlockStateEventHandler` | Client.Game/InGame/World（新規。`WorldDataHandler` の隣） | `IInitializable` + `RegisterEntryPoint`、ctor で `SubscribeEventResponse` | `WorldDataHandler`（同じ `BlockGameObjectDataStore` を受け、ctor で購読） |
| 5 | `BlockGameObject.ApplyBlockState`／`OnBlockStateChanged` | Client.Game/InGame/Block（既存クラスの変更） | 公開メソッド＋UniRx `Subject` | `BlockGameObject.OnFinishedPlaceAnimation`（同じクラスの `Subject` 公開） |
| 6 | `BlockInventoryTopic` の購読先変更 | Client.WebUiHost（既存の変更） | `block.OnBlockStateChanged.Subscribe` | 同ファイルの `_blockStateSubscription` の Dispose 管理をそのまま使う |
| 7 | テスト補助 `EventTestUtil.ExtractBlockStates` | Tests/CombinedTest/Server/PacketTest/Event（既存の変更） | 静的ヘルパー | `EventTestUtil.RegisterCaptureSink` |

### 送信の駆動の比較（新規パターン）

前例の `BeltTransportTickEventPacket`・`TrainUnitTickDiffBundleEventPacket` は、EventPacket が自分の `Load()` でドメインの IObservable を購読して送る（ベルトは `BeltTransportTickUpdater.OnTransportTickCompleted` で送る。列車は `TrainUpdateService.OnHashEvent` でハッシュを覚えるだけで、送るのは次tickの `OnPreSimulationDiffEvent`）。`MasterTickUpdater` が呼ぶのはドメインの Updater だけで、EventPacket を直接は呼ばない。今回は `MasterTickUpdater`（Server.Boot）が `ChangeBlockStateEventPacket.BroadcastPendingStates()`（Server.Event）を直接呼ぶ。

- (a) 明示呼び出し（採用）: 送る位置が `MasterTickUpdater.Tick()` の1ファイルに見え、「網の再構築の後・境界ハッシュの前」が並びで保証される。`MasterTickUpdater` の「処理順を購読や登録順に頼らない」方針（ADR 0087）と合う。
- (b) 購読: tick末尾に「ブロック状態を送ってよい点」を知らせるドメインの通知が無い。既存で近いのは `TrainUpdateService.OnHashEvent`（`CompleteTick()` の中で発火）だが、これは列車の境界の通知で、ブロック状態の送信を乗せるとドメインの意味が合わない。また同じ通知の購読者どうしの順は登録順に依存し、`MasterTickUpdater` の「処理順を購読や登録順に頼らない」方針（ADR 0087）に反する。新しい通知を作ると、その発火位置を `MasterTickUpdater` に置くことになり、(a) と同じ位置に購読を1段挟むだけになる。

出所: agent前提（ADR 0088「送り口は MasterTickUpdater の tick末尾から明示的に呼ぶ」）。

データフロー（変更後）:

```
ブロックの状態変化 → WorldBlockDatastore.OnBlockStateChange → ChangeBlockStateEventPacket(書き手: 溜める)
  → [MasterTickUpdater tick末尾] BroadcastPendingStates → EventProtocolProvider → 各sink
  → クライアント VanillaApiEvent → BlockStateEventHandler(読み手) → BlockGameObjectDataStore[座標]
  → BlockGameObject.ApplyBlockState → 状態プロセッサ群 / OnBlockStateChanged → BlockInventoryTopic(読み手)
```

---

### Task 1: 変更前のベンチマークを取る

**Files:**
- Create: `docs/performance/benchmark/results/<日時>_before-tick-bundle_<コミット>_r1.json`〜`_r3.json`（スクリプトが作る）

**Interfaces:**
- Consumes: なし
- Produces: 基準 label `before-tick-bundle` の結果 JSON 3つ（Task 6 の比較で使う）

- [ ] **Step 1: ブランチを push し、windows-desktop を同じコミットにする**

```bash
git push origin performance
ssh windows-desktop "git -C G:\GitHub\moorestech status --short"
ssh windows-desktop "git -C G:\GitHub\moorestech fetch origin"
ssh windows-desktop "git -C G:\GitHub\moorestech checkout performance"
ssh windows-desktop "git -C G:\GitHub\moorestech reset --hard origin/performance"
ssh windows-desktop "uloop compile --project-path G:\GitHub\moorestech\moorestech_client"
```
Expected: 1つ目の status は空（未コミットの変更があれば reset せずに止めて報告する）。compile の ErrorCount 0。Editor が落ちていたら `docs/performance/2026-10-22-windows-desktop-low-fps/README.md` の「再計測の手順」2 の方法で起動する。

- [ ] **Step 2: ベンチマークを回す**

```bash
docs/performance/benchmark/run-benchmark.sh before-tick-bundle
```
Expected: `results/*_before-tick-bundle_*_r1.json`〜`r3.json` が作られる（約5分）。

- [ ] **Step 3: コミットする**

```bash
git add docs/performance/benchmark/results/*_before-tick-bundle_*.json
git commit -m "docs: ブロック状態のtick束ね送信の前のベンチマーク結果を残す"
```

---

### Task 2: サーバーでブロック状態を溜めてtick末尾に1イベントで送る

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/ChangeBlockStateEventPacket.cs`（全体）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MasterTickUpdater.cs`（ctor・`Tick()` の末尾）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RequestBlockStateProtocol.cs`（コメントのみ）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/EventTestUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/ChangeBlockStateEventPacketDedupTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/ChangeBlockEventPacketTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RequestBlockStateProtocolTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ChainProtocolTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/BlockStatesTickBundleTest.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `public const string ChangeBlockStateEventPacket.EventTag = "va:event:blockStatesTickBundle";`
  - `public void ChangeBlockStateEventPacket.BroadcastPendingStates()`
  - `public class BlockStatesTickBundleMessagePack { [Key(0)] public BlockStateMessagePack[] States { get; set; } }`（namespace `Server.Event.EventReceive`）
  - `public static List<BlockStateMessagePack> EventTestUtil.ExtractBlockStates(List<EventMessagePack> events)`
  - `CreateSpecifiedBlockEventTag` はこのタスクでは**残す**（クライアントがまだ参照しているため。Task 3 で消す）

- [ ] **Step 1: `ChangeBlockStateEventPacket.cs` を書き換える**

ファイル全体を次の内容にする（`CreateSpecifiedBlockEventTag` は Task 3 まで残す）。

```csharp
using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    // ブロック状態の変化を座標ごとの最新1件に溜め、tick末尾に1イベントへ束ねて全員へ送る（ADR 0088）
    // Keeps the latest block state per position and broadcasts them as one bundled event at tick end (ADR 0088)
    public class ChangeBlockStateEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:blockStatesTickBundle";

        private readonly EventProtocolProvider _eventProtocolProvider;

        // 位置ごとの最終送信ペイロード（差分検知用）
        // Last sent payload per position, for diff detection
        private readonly Dictionary<Vector3Int, byte[]> _lastBroadcastPayloads = new();

        // tick末尾の送信まで溜める、座標ごとの最新状態
        // Latest state per position, held until the tick-end broadcast
        private readonly Dictionary<Vector3Int, BlockStateMessagePack> _pendingStates = new();

        public ChangeBlockStateEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void Load()
        {
            ServerContext.WorldBlockDatastore.OnBlockStateChange.Subscribe(ChangeState);
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }

        // 購読経路用。前回と同一ペイロードなら溜めない
        // Subscription path: skip when the payload matches the last sent
        public void ChangeState((BlockState state, WorldBlockData blockData) state)
        {
            var (pos, messagePack, payload) = SerializeState(state);

            if (_lastBroadcastPayloads.TryGetValue(pos, out var lastPayload) && payload.AsSpan().SequenceEqual(lastPayload)) return;

            QueueState(pos, messagePack, payload);
        }

        // 初期pull経路専用。差分スキップ禁止
        // Initial-pull path only: diff-skip is forbidden
        public void ForceChangeState((BlockState state, WorldBlockData blockData) state)
        {
            var (pos, messagePack, payload) = SerializeState(state);
            QueueState(pos, messagePack, payload);
        }

        // tick末尾にMasterTickUpdaterから1回だけ呼ぶ。変化0件のtickは送らない
        // Called once at tick end by MasterTickUpdater; nothing is sent for a tick without changes
        public void BroadcastPendingStates()
        {
            if (_pendingStates.Count == 0) return;

            var states = new BlockStateMessagePack[_pendingStates.Count];
            _pendingStates.Values.CopyTo(states, 0);
            _pendingStates.Clear();

            var payload = MessagePackSerializer.Serialize(new BlockStatesTickBundleMessagePack(states));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        public static string CreateSpecifiedBlockEventTag(BlockPositionInfo posInfo)
        {
            return $"{EventTag}:{posInfo.OriginalPos}";
        }

        // ブロック削除時に差分記録と溜めた状態を掃除する。放置すると同座標に再設置された別ブロックへ旧状態が届きうる
        // Clean up the diff record and the pending state on removal; otherwise a block re-placed at the same position could receive stale state
        private void OnBlockRemove(BlockRemoveProperties properties)
        {
            ForgetPosition(properties.BlockData.BlockPositionInfo.OriginalPos);
        }

        // Destroy中発火でstale再登録された場合に備え、設置時にも同座標の記録を掃除する
        // Also clear on placement in case Destroy() re-registers a stale entry at this position
        private void OnBlockPlace(BlockPlaceProperties properties)
        {
            ForgetPosition(properties.BlockData.BlockPositionInfo.OriginalPos);
        }

        // 座標・送る形・差分判定用のバイト列を1か所で作る
        // Builds the position, the message and the diff bytes in one place
        private static (Vector3Int pos, BlockStateMessagePack messagePack, byte[] payload) SerializeState((BlockState state, WorldBlockData blockData) state)
        {
            var pos = state.blockData.BlockPositionInfo.OriginalPos;
            var messagePack = new BlockStateMessagePack(state.state, pos);
            return (pos, messagePack, MessagePackSerializer.Serialize(messagePack));
        }

        // 差分記録と溜めた状態は必ず一緒に更新する
        // The diff record and the pending state are always updated together
        private void QueueState(Vector3Int pos, BlockStateMessagePack messagePack, byte[] payload)
        {
            _lastBroadcastPayloads[pos] = payload;
            _pendingStates[pos] = messagePack;
        }

        private void ForgetPosition(Vector3Int pos)
        {
            _lastBroadcastPayloads.Remove(pos);
            _pendingStates.Remove(pos);
        }
    }

    [MessagePackObject]
    public class BlockStateMessagePack
    {
        /// <summary>
        /// key Component key, value Component state
        /// </summary>
        [Key(0)] public Dictionary<string,byte[]> CurrentStateDetail { get; set; }

        [Key(1)] public Vector3IntMessagePack Position { get; set; } // TODO ここをinstanceIdに変更する？

        public TBlockState GetStateDetail<TBlockState>(string stateKey)
        {
            return CurrentStateDetail.GetStateDetail<TBlockState>(stateKey);
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockStateMessagePack()
        {
        }

        public BlockStateMessagePack(BlockState state, Vector3Int pos)
        {
            CurrentStateDetail = state.CurrentStateDetails;
            Position = new Vector3IntMessagePack(pos);
        }
    }

    // 1tick分のブロック状態の束。座標ごとに最新の1件だけが入る
    // One tick's bundle of block states; holds only the latest entry per position
    [MessagePackObject]
    public class BlockStatesTickBundleMessagePack
    {
        [Key(0)] public BlockStateMessagePack[] States { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockStatesTickBundleMessagePack()
        {
        }

        public BlockStatesTickBundleMessagePack(BlockStateMessagePack[] states)
        {
            States = states;
        }
    }
}
```

`BlockStateMessagePack` は `GetBlockState()` が毎回新しい辞書を作るので、溜めている間に中身が書き換わることはない（`moorestech_server/Assets/Scripts/Game.Block/Blocks/BlockSystem.cs` の `GetBlockState`）。

- [ ] **Step 2: `MasterTickUpdater` に送信を入れる**

`moorestech_server/Assets/Scripts/Server.Boot/MasterTickUpdater.cs` を次のとおり変える。

1. `using Server.Event.EventReceive;` を足す。
2. フィールドに `private readonly ChangeBlockStateEventPacket _changeBlockStateEventPacket;` を足す（`_serverThreadActionQueue` の下）。
3. ctor の引数に `ChangeBlockStateEventPacket changeBlockStateEventPacket` を足し（`ServerThreadActionQueue serverThreadActionQueue,` の次）、`_changeBlockStateEventPacket = changeBlockStateEventPacket;` を代入する。DI は `AddSingleton<MasterTickUpdater>()` が型で解決するので、登録側の変更は不要。
4. `Tick()` の `_worldTopologyRebuilder.RebuildTopologiesIfDirty();` の直後（`// 境界:` コメントの前）に次を入れる。

```csharp
            // 操作の適用と再構築で出た分まで含め、このtickのブロック状態を1イベントで送る（ADR 0088）
            // Send this tick's block states as one event, including those raised by input and rebuilds (ADR 0088)
            _changeBlockStateEventPacket.BroadcastPendingStates();
```

5. クラス先頭のコメント2行の「末尾(操作の一括適用→別スレッド処理→派生網の再構築)」を「末尾(操作の一括適用→別スレッド処理→派生網の再構築→ブロック状態の束の送信)」に、英語側の「the tick end (apply input, drain other-thread work, rebuild derived networks)」を「the tick end (apply input, drain other-thread work, rebuild derived networks, send the block-state bundle)」に変える。

- [ ] **Step 3: `RequestBlockStateProtocol` のコメントを直す**

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RequestBlockStateProtocol.cs` のクラス先頭のコメント1行を次の2行にする。

```csharp
    // ブロックのステートをリクエストする。返答は直接返さず、そのtick末尾のブロック状態の束に載せて返す
    // Requests a block's state; the reply is not returned directly but rides on this tick's block-state bundle
```

- [ ] **Step 4: テスト補助を足す**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/EventTestUtil.cs` の `using` に `using MessagePack;` と `using Server.Event.EventReceive;` を足し、`EventTestUtil` クラスに次のメソッドを足す。

```csharp
        // ブロック状態の束イベントから、中身の状態を到着順に取り出す
        // Extracts the block states from bundle events in arrival order
        public static List<BlockStateMessagePack> ExtractBlockStates(List<EventMessagePack> events)
        {
            var states = new List<BlockStateMessagePack>();
            foreach (var eventMessage in events)
            {
                if (eventMessage.Tag != ChangeBlockStateEventPacket.EventTag) continue;
                states.AddRange(MessagePackSerializer.Deserialize<BlockStatesTickBundleMessagePack>(eventMessage.Payload).States);
            }
            return states;
        }
```

- [ ] **Step 5: Dedup テストを束の形に書き換える**

`ChangeBlockStateEventPacketDedupTest.cs` の各テストを次のように変える（Arrange 部分は今のまま）。

`SamePayloadIsBroadcastOnceTest`: 2回の `ChangeState` の後に送信し、状態が1件。

```csharp
            changeBlockStateEventPacket.ChangeState((blockState, blockData));
            changeBlockStateEventPacket.ChangeState((blockState, blockData));
            changeBlockStateEventPacket.BroadcastPendingStates();

            var states = EventTestUtil.ExtractBlockStates(sink.TakeAll());
            Assert.AreEqual(1, states.Count, "同一ペイロードなら1件のみ載るべき");
```

`ChangedPayloadIsBroadcastEachTimeTest` は名前を `ChangedPayloadWithinOneTickKeepsLatestTest` に変え、同じtick内では後の状態の1件だけになることを確かめる。さらに、送信を挟めば2回とも載ることを確かめる。

```csharp
        [Test]
        // 同じtick内でペイロードが2回変わったら、後の状態の1件だけが載る
        // When the payload changes twice within one tick, only the later state is bundled
        public void ChangedPayloadWithinOneTickKeepsLatestTest()
        {
            // （Arrange は既存の ChangedPayloadIsBroadcastEachTimeTest と同じ。pos=(6,0,6)、firstState/secondState を作る）

            changeBlockStateEventPacket.ChangeState((firstState, blockData));
            changeBlockStateEventPacket.ChangeState((secondState, blockData));
            changeBlockStateEventPacket.BroadcastPendingStates();

            var states = EventTestUtil.ExtractBlockStates(sink.TakeAll());
            Assert.AreEqual(1, states.Count, "同じtick内の2回の変化は1件に畳まれるべき");
            Assert.AreEqual(2, states[0].CurrentStateDetail["dummy"][0], "後の状態が残るべき");
        }

        [Test]
        // 送信を挟んでペイロードが変わったら、それぞれのtickの束に載る
        // When the payload changes across a broadcast, each tick's bundle carries it
        public void ChangedPayloadAcrossTicksIsBroadcastEachTimeTest()
        {
            // （Arrange は上と同じ。pos=(6,0,6)）

            changeBlockStateEventPacket.ChangeState((firstState, blockData));
            changeBlockStateEventPacket.BroadcastPendingStates();
            changeBlockStateEventPacket.ChangeState((secondState, blockData));
            changeBlockStateEventPacket.BroadcastPendingStates();

            var events = sink.TakeAll();
            Assert.AreEqual(2, events.Count, "tickごとに束が1件ずつ送られるべき");
            Assert.AreEqual(2, EventTestUtil.ExtractBlockStates(events).Count, "両方の状態が載るべき");
        }
```

Arrange はコメントの指示どおり、既存テストの行（DIコンテナの生成から `sink.TakeAll();` まで）をそのまま写す。

`ForceChangeStateAlwaysBroadcastsTest`: 送信を挟んで2回強制すると、同じ中身でも2つの束に載る。

```csharp
            changeBlockStateEventPacket.ForceChangeState((blockState, blockData));
            changeBlockStateEventPacket.BroadcastPendingStates();
            changeBlockStateEventPacket.ForceChangeState((blockState, blockData));
            changeBlockStateEventPacket.BroadcastPendingStates();

            var states = EventTestUtil.ExtractBlockStates(sink.TakeAll());
            Assert.AreEqual(2, states.Count, "強制送信経路は同一ペイロードでも毎回載るべき");
```

`InvokeBlockStateEventProtocolAlwaysBroadcastsEvenWhenUnchangedTest`: 「前回ペイロードとして記録させる」`ChangeState` の直後に `changeBlockStateEventPacket.BroadcastPendingStates();` を入れる（sink 登録前に送り切る）。`packet.GetPacketResponse(...)` の後に `changeBlockStateEventPacket.BroadcastPendingStates();` を入れ、検査を次にする。

```csharp
            var states = EventTestUtil.ExtractBlockStates(sink.TakeAll());
            Assert.AreEqual(1, states.Count, "変化がなくてもpull経路では必ず1件載るべき");
```

`RePlacedBlockBroadcastsAfterStaleReRegistrationTest`: (6) の `ChangeState` の後に `changeBlockStateEventPacket.BroadcastPendingStates();` を入れ、(7) を次にする。

```csharp
            var states = EventTestUtil.ExtractBlockStates(sink.TakeAll());
            Assert.AreEqual(1, states.Count, "再設置後の初回送信は必ず載るべき");
```

- [ ] **Step 6: 束の新しい性質のテストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/BlockStatesTickBundleTest.cs` を作る。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    // ブロック状態の束（ADR 0088）の送信タイミングと掃除を検証
    // Verifies when the block-state bundle (ADR 0088) is sent and how it is cleaned up
    public class BlockStatesTickBundleTest
    {
        [Test]
        // tickを進めると、そのtickに溜めた状態が1イベントで届く
        // Advancing a tick delivers the states pending in that tick as one event
        public void TickEndBroadcastsPendingStatesAsOneEventTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var firstPos = new Vector3Int(20, 0, 20);
            var secondPos = new Vector3Int(22, 0, 20);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, firstPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, secondPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 1);
            GameUpdater.UpdateOneTick();
            sink.TakeAll();

            // 2ブロックへ同じtick内に状態を溜める
            // Queue states for two blocks within the same tick
            changeBlockStateEventPacket.ChangeState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } }), world.GetOriginPosBlock(firstPos)));
            changeBlockStateEventPacket.ChangeState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } }), world.GetOriginPosBlock(secondPos)));
            Assert.AreEqual(0, sink.Events.Count(e => e.Tag == ChangeBlockStateEventPacket.EventTag), "tick末尾まで送られないべき");

            GameUpdater.UpdateOneTick();

            var events = sink.TakeAll().Where(e => e.Tag == ChangeBlockStateEventPacket.EventTag).ToList();
            Assert.AreEqual(1, events.Count, "1tickの状態は1イベントにまとまるべき");
            var positions = EventTestUtil.ExtractBlockStates(events).Select(s => (Vector3Int)s.Position).ToList();
            CollectionAssert.Contains(positions, firstPos);
            CollectionAssert.Contains(positions, secondPos);
        }

        [Test]
        // 変化が無いときは束を送らない
        // No bundle is sent when nothing changed
        public void NothingPendingSendsNoEventTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 1);
            changeBlockStateEventPacket.BroadcastPendingStates();
            sink.TakeAll();

            changeBlockStateEventPacket.BroadcastPendingStates();

            Assert.AreEqual(0, sink.TakeAll().Count, "溜めた状態が無ければ何も送らないべき");
        }

        [Test]
        // 溜めた後に撤去したブロックの状態は束に載らない
        // A block removed after its state was queued is left out of the bundle
        public void RemovedBlockStateIsDroppedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();

            var pos = new Vector3Int(24, 0, 24);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 1);
            changeBlockStateEventPacket.BroadcastPendingStates();
            sink.TakeAll();

            changeBlockStateEventPacket.ChangeState((new BlockState(new Dictionary<string, byte[]> { ["dummy"] = new byte[] { 1 } }), world.GetOriginPosBlock(pos)));
            world.RemoveBlock(pos, BlockRemoveReason.ManualRemove);
            changeBlockStateEventPacket.BroadcastPendingStates();

            var positions = EventTestUtil.ExtractBlockStates(sink.TakeAll()).Select(s => (Vector3Int)s.Position).ToList();
            CollectionAssert.DoesNotContain(positions, pos, "撤去したブロックの状態は捨てられるべき");
        }
    }
}
```

- [ ] **Step 7: 座標タグに頼っていたサーバーテストを直す**

`RequestBlockStateProtocolTest.InvokeTest`: `packet.GetPacketResponse(...)` の後に送信を入れ、イベント検査を次にする。`serviceProvider.GetService<ChangeBlockStateEventPacket>()` を Arrange で取る。

```csharp
            serviceProvider.GetService<ChangeBlockStateEventPacket>().BroadcastPendingStates();

            // イベントが発行されたことを確認
            // Ensure the bundle event was published
            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChangeBlockStateEventPacket.EventTag, events[0].Tag);

            // 束の中身の確認
            // Check the bundle content
            var states = EventTestUtil.ExtractBlockStates(events);
            Assert.AreEqual(1, states.Count);
            Assert.AreEqual(blockPosition, (Vector3Int)states[0].Position);
```

`RequestBlockStateProtocolTest.NotInvokeTest`: 「イベントが発行されていないことを確認」の前に `serviceProvider.GetService<ChangeBlockStateEventPacket>().BroadcastPendingStates();` を入れる（検査はそのまま）。

`ChainProtocolTest.ConnectChainProtocolRespondsAndBroadcasts`: `var events = sink.TakeAll();` の前に `serviceProvider.GetService<ChangeBlockStateEventPacket>().BroadcastPendingStates();` を入れ、検査を次にする。

```csharp
            var events = sink.TakeAll();
            var positions = EventTestUtil.ExtractBlockStates(events).Select(s => (Vector3Int)s.Position).ToList();
            Assert.IsTrue(positions.Contains(posA) || positions.Contains(posB), "Block state change event should be published");
```

`ChangeBlockEventPacketTest.MachineChangeStateEvent`: `expectedTag` を消し、ループを束の中身の走査にする。

```csharp
            BlockStateMessagePack transitionStateData = null;
            BlockStateMessagePack latestStateData = null;
            foreach (var stateData in EventTestUtil.ExtractBlockStates(events))
            {
                if ((Vector3Int)stateData.Position != machine.BlockPositionInfo.OriginalPos) continue;
                latestStateData = stateData;
                // （以降のループ本体は今のまま。`var stateData = MessagePackSerializer.Deserialize...` の行だけ消す）
```

このテストは「idle→processing の遷移が、どこかの tick の束に入っている」ことを確かめている。座標ごとに最新だけ残す形では、遷移した tick のうちに同じ機械の状態がもう一度変わると、遷移が上書きされる。テストが失敗したら、`ExtractBlockStates` の結果を tick ごとに `Debug.Log` で出し、遷移が同じtickで上書きされたかどうかを確かめて報告する（推測でアサーションを緩めない）。

- [ ] **Step 8: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ChangeBlockStateEventPacketDedupTest|BlockStatesTickBundleTest|RequestBlockStateProtocolTest|ChainProtocolTest|ChangeBlockEventPacketTest"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 9: サーバーテスト全体で回帰を見る**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\." --timeout-seconds 1500`
Expected: 失敗0。失敗したテストがあれば、テスト名を控えて Task 1 時点のコミット（`git log` で確認）を別の worktree で同じフィルタで回し、そこでも落ちるものは既存の失敗として報告に書く。今回の変更で落ちるようになったものだけを直す。

- [ ] **Step 10: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Event/EventReceive/ChangeBlockStateEventPacket.cs moorestech_server/Assets/Scripts/Server.Boot/MasterTickUpdater.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RequestBlockStateProtocol.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/
git commit -m "perf: ブロック状態を座標ごとの最新に溜め、tick末尾に1イベントへ束ねて送る"
```
（Unity が `BlockStatesTickBundleTest.cs.meta` を作っていれば一緒にコミットする）

---

### Task 3: クライアントで束を受けて座標で振り分ける

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/World/BlockStateEventHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs`（`SubscribeBlockState` と状態の適用）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs:181-186`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs:97`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/ChangeBlockStateEventPacket.cs`（`CreateSpecifiedBlockEventTag` を消す）

**Interfaces:**
- Consumes: `ChangeBlockStateEventPacket.EventTag`、`BlockStatesTickBundleMessagePack.States`（Task 2）
- Produces:
  - `public void BlockGameObject.ApplyBlockState(BlockStateMessagePack state)`
  - `public IObservable<BlockStateMessagePack> BlockGameObject.OnBlockStateChanged`
  - `public class BlockStateEventHandler : IInitializable`（namespace `Client.Game.InGame.World`）

- [ ] **Step 1: `BlockGameObject` に状態の適用口と通知を作る**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs` を次のとおり変える。

1. `OnFinishedPlaceAnimation` の2行の下に足す。

```csharp
        // サーバーから状態が届いた直後に発火する。WebUIの再配信などが購読する
        // Fires right after a state arrives from the server; WebUI republishing subscribes to it
        public IObservable<BlockStateMessagePack> OnBlockStateChanged => _onBlockStateChanged;
        private readonly Subject<BlockStateMessagePack> _onBlockStateChanged = new();
```

2. `Initialize` 内の `SubscribeBlockState()` のローカル関数を次に置き換え（購読をやめ、初期状態の問い合わせだけ残す）、呼び出し側のコメント2行も合わせる。

```csharp
            // ブロックの初期状態をサーバーに問い合わせる。返事はtick末尾の束でBlockStateEventHandlerから届く
            // Ask the server for the initial block state; the reply arrives via BlockStateEventHandler in the tick-end bundle
            RequestInitialBlockState();
```

```csharp
            void RequestInitialBlockState()
            {
                ClientContext.VanillaApi.SendOnly.RequestBlockState(BlockPosInfo.OriginalPos);
            }
```

3. `GetStateDetail` の上に、元の購読ラムダの中身を移したメソッドを足す（try-catch とログの文言は元のまま）。

```csharp
        // 届いた状態を全プロセッサへ渡す。1プロセッサの例外で他のプロセッサや他のブロックの反映を止めない
        // Passes an arrived state to every processor; one processor's exception never stops the others or other blocks
        public void ApplyBlockState(BlockStateMessagePack state)
        {
            foreach (var processor in BlockStateChangeProcessors)
            {
                try
                {
                    using (BlockStateProfilerMarkers.ForProcessor(processor).Auto()) processor.OnChangeState(state);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Name:{BlockMasterElement.Name} Pos: {BlockPosInfo.OriginalPos} BlockStateChangeProcessorの{processor.GetType().Name}で例外が発生しました。\n{e.Message}\n{e.StackTrace}");
                }
            }
            _blockStateMessagePack = state;
            _onBlockStateChanged.OnNext(state);
        }
```

4. `using Server.Event.EventReceive;` は残す（`BlockStateMessagePack` がこの名前空間にある）。`using MessagePack;` は、ファイル内で `MessagePackSerializer` を他に使っていなければ消す。

- [ ] **Step 2: `BlockStateEventHandler` を作る**

`moorestech_client/Assets/Scripts/Client.Game/InGame/World/BlockStateEventHandler.cs`（同じ役割の `WorldDataHandler` の隣）:

```csharp
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.World
{
    /// <summary>
    ///     tick末尾に届くブロック状態の束を1回だけ読み、座標でブロックへ振り分ける（ADR 0088）
    ///     Reads the tick-end block-state bundle once and routes each entry to its block by position (ADR 0088)
    /// </summary>
    public class BlockStateEventHandler : IInitializable
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        public BlockStateEventHandler(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;

            // WorldDataHandlerと同じくctorで購読する。InitializeDispatchまでの受信はVanillaApiEventがバッファする
            // Subscribe in the ctor like WorldDataHandler; VanillaApiEvent buffers anything received before InitializeDispatch
            ClientContext.VanillaApi.Event.SubscribeEventResponse(ChangeBlockStateEventPacket.EventTag, OnBlockStatesTickBundle);
        }

        public void Initialize()
        {
        }

        private void OnBlockStatesTickBundle(byte[] payload)
        {
            BlockStatesTickBundleMessagePack bundle;
            using (BlockStateProfilerMarkers.DeserializeBlockState.Auto()) bundle = MessagePackSerializer.Deserialize<BlockStatesTickBundleMessagePack>(payload);

            foreach (var state in bundle.States)
            {
                // 撤去直後などで座標にブロックが無い件は読み飛ばす
                // Skip entries whose position has no block, e.g. right after removal
                Vector3Int position = state.Position;
                if (!_blockGameObjectDataStore.TryGetBlockGameObject(position, out var block))
                {
                    Debug.Log($"[BlockStateEventHandler] 座標{position}にブロックが無いため状態を読み飛ばしました");
                    continue;
                }
                block.ApplyBlockState(state);
            }
        }
    }
}
```

`Vector3IntMessagePack` から `Vector3Int` への暗黙変換は既存（`BlockGameObject` の旧コードの `data.Position != BlockPosInfo.OriginalPos` が使っていた）。

- [ ] **Step 3: DI に登録する**

`moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs` の `builder.RegisterEntryPoint<WorldDataHandler>();` の次の行に足す（`WorldDataHandler` と同じ名前空間 `Client.Game.InGame.World` なので using の追加は不要）。

```csharp
            builder.RegisterEntryPoint<BlockStateEventHandler>();
```

- [ ] **Step 4: `BlockInventoryTopic` の購読先を変える**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs` の次の部分

```csharp
            // uGUI BlockGameObject と同じ per-block イベントタグを直接購読して再配信トリガにする
            // Directly subscribe the same per-block event tag as uGUI BlockGameObject as the republish trigger
            var eventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(block.BlockPosInfo);
            _blockStateSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                eventTag,
                _ => OnTrackedBlockStateChanged());
```

を次に置き換える。

```csharp
            // 束の振り分けで追跡ブロックに状態が届いたら再配信する
            // Republish whenever bundle routing delivers a state to the tracked block
            _blockStateSubscription = block.OnBlockStateChanged.Subscribe(_ => OnTrackedBlockStateChanged());
```

ファイル内で `Server.Event.EventReceive` の型を他に使っていなければ `using Server.Event.EventReceive;` を消す。`ClientContext` を他で使っていなければ `using Client.Game.InGame.Context;` も消す（コンパイルの警告で確かめる）。

- [ ] **Step 5: 座標タグのメソッドを消す**

`moorestech_server/Assets/Scripts/Server.Event/EventReceive/ChangeBlockStateEventPacket.cs` から `CreateSpecifiedBlockEventTag` メソッドを消す。`using Game.Block.Interface;` が他で不要になっていれば消す（`BlockPositionInfo` の参照が無くなる。`BlockRemoveProperties` などの型がどの名前空間にあるかはコンパイルで確かめる）。

Run: `grep -rn "CreateSpecifiedBlockEventTag" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts`
Expected: 出力なし

- [ ] **Step 6: コンパイルし、クライアントとサーバーの関連テストを回す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockStatesTickBundleTest|ChangeBlockStateEventPacketDedupTest|RequestBlockStateProtocolTest|FluidPipeStateEntryTest|GearRotationCullingTargetTest|BlockDetailDtoBuilderTest"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs moorestech_client/Assets/Scripts/Client.Game/InGame/World/ moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs moorestech_server/Assets/Scripts/Server.Event/EventReceive/ChangeBlockStateEventPacket.cs
git commit -m "perf: クライアントはブロック状態の束を1回だけ読み、座標でブロックへ振り分ける"
```

---

### Task 4: unityプレイ録画テストで状態の到達を確かめる

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/block-state-tick-bundle.cs`

**Interfaces:**
- Consumes: `BlockGameObject.OnBlockStateChanged`（Task 3）、`ChangeBlockStateEventPacket.EventTag`（Task 2）
- Produces: なし

- [ ] **Step 1: シナリオを書く**

前例は `.agents/skills/unity-playmode-recorded-playtest/scenarios/graphics-quality/quality-ui-switching.cs`（パイプに水を入れて水位の受信を待つ）。書き方は同スキルの `references/write-scenario.md` を読んでから書く。

```csharp
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Client.Playtest;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Server.Event.EventReceive;
using UniRx;
using UnityEngine;

return PlaytestRunner.Run("block-state-tick-bundle", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();

    // 束イベントの受信回数を数える
    // Count received bundle events
    var bundleCount = 0;
    var bundleSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(ChangeBlockStateEventPacket.EventTag, _ => bundleCount++);

    var origin = new Vector3Int(3, 32, 3);
    await PlacePipeAndCheckWater(origin, 100f, "1回目");

    // 撤去して同じ座標に置き直しても状態が届く
    // State still arrives after removing and re-placing at the same position
    p.RemoveBlock(origin);
    await p.Until(() => p.GetBlock(origin) == null, 10f, "パイプ撤去");
    await p.WaitSeconds(1f);
    await PlacePipeAndCheckWater(origin, 50f, "再設置後");

    p.Assert(bundleCount > 0, $"束イベントを受信した count={bundleCount}");
    p.Note($"[BUNDLE] received={bundleCount}");
    bundleSubscription.Dispose();

    #region Internal
    async UniTask PlacePipeAndCheckWater(Vector3Int pos, float amount, string label)
    {
        var block = p.PlaceBlockDirect("鉄のパイプ", pos, BlockDirection.East);
        var view = await p.WaitBlockGameObject(pos);
        await p.Until(() => !view.IsPlayingPlaceAnimation, 15f, $"{label} 設置アニメ完了");
        var changedCount = 0;
        var changedSubscription = view.OnBlockStateChanged.Subscribe(_ => changedCount++);
        var processor = view.GetComponentInChildren<FluidPipeStateChangeProcessor>();
        var water = MasterHolder.FluidMaster.GetFluidId(new System.Guid("9eae6979-d56a-4991-9107-b8161acec430"));
        block.GetComponent<FluidPipeComponent>().Node.AddExternal(new FluidStack(amount, water));
        await p.Until(() => processor.DebugCurrentFluidPipeState != null && processor.DebugCurrentFluidPipeState.Amount == amount, 15f, $"{label} 水位受信 {amount}");
        p.Assert(changedCount > 0, $"{label} OnBlockStateChangedが発火 count={changedCount}");
        await p.Screenshot($"water-{label}");
        changedSubscription.Dispose();
    }
    #endregion
});
```

- [ ] **Step 2: 実行する**

Run（スキルの一発実行。master は同スキルの規約どおりブランチ互換のピンで）:
```bash
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" "$(pwd)/moorestech_client" "$SKILL/scenarios/misc/block-state-tick-bundle.cs"
```
Expected: result.json が Success。mp4 とスクショで、パイプに水が入った見た目を確かめる。`uloop get-logs --project-path ./moorestech_client --log-type Error` に今回の変更に由来するエラーが無い。

あわせて `uloop get-logs --project-path ./moorestech_client --log-type Log` で `[BlockStateEventHandler]` の読み飛ばしログを数える。期待値は撤去1回につき0件（`BlockSystem.Destroy()` 配下の各コンポーネントの `Destroy()` は購読の `Dispose()` だけで、状態変化を発火しないことをコードで確認済み）。1件でも出たら、その行と直前の操作を報告に書き、新しい発見として Task 7 のレビューで扱う（このタスクでは直さない）。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/block-state-tick-bundle.cs
git commit -m "test: ブロック状態の束がパイプの水位と再設置後のブロックに届くことをプレイ録画テストで確かめる"
```

---

### Task 5: ベンチマークを束の形でも数えられるようにする

**Files:**
- Modify: `docs/performance/benchmark/snippets/events-start.cs`
- Modify: `docs/performance/benchmark/snippets/events-collect.cs`（全体）
- Modify: `docs/performance/benchmark/compare.py`
- Modify: `docs/performance/benchmark/README.md`（「### 注意」と「結果 JSON の中身」の `events` 行）
- Modify: `docs/performance/2026-10-22-windows-desktop-low-fps/README.md`（「再計測の手順」への注記）

**Interfaces:**
- Consumes: `BlockStatesTickBundleMessagePack`（Task 2）
- Produces: 結果 JSON の `events.blockStatesPerSecond`（束の中身の件数/秒）。`events.byKind` のブロック状態は、束の中の各件を状態キーの組み合わせ別に数える

- [ ] **Step 1: `events-start.cs` を束に対応させる**

`if (kind == ...ChangeBlockStateEventPacket.EventTag) { ... }` のブロックを次に置き換える。束1件は「イベント1件」として数え、中の各件は `blockState|<キーの組み合わせ>` に件数だけ足す（payload バイトは束の側にだけ数える）。座標ごとの組み合わせはキャッシュして、2回目からは数えるだけにする。

```csharp
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
```

あわせて `events-start.cs` の `var comboByTag = new Dictionary<string, string>();` の次の行に `var blockStates = new double[1];` を足し、`dom.SetData("bench_ev_counts", counts); ...` の行の次に `dom.SetData("bench_ev_blockstates", blockStates);` を足す。

`events-collect.cs` は全体を次にする（`blockState|*` は束の中身の件数なので、`eventsPerSecond` の合計からは外す）。

```csharp
// フックを外し、毎秒のイベント件数・payloadバイト数・ブロック状態の件数をJSONで返す
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
var blockStates = (double[])dom.GetData("bench_ev_blockstates");
var ci = System.Globalization.CultureInfo.InvariantCulture;
var total = counts.Where(k => !k.Key.StartsWith("blockState|")).Sum(k => k.Value[0]); var bytes = counts.Values.Sum(v => v[1]);
var items = counts.OrderByDescending(k => k.Value[0]).Select(k =>
  "\"" + k.Key + "\":{\"perSecond\":" + (k.Value[0] / secs).ToString("F1", ci) + ",\"payloadBytesPerSecond\":" + (k.Value[1] / secs).ToString("F0", ci) + "}");
return "{\"seconds\":" + secs.ToString("F3", ci) + ",\"eventsPerSecond\":" + (total / secs).ToString("F1", ci) + ",\"payloadBytesPerSecond\":" + (bytes / secs).ToString("F0", ci) + ",\"blockStatesPerSecond\":" + (blockStates[0] / secs).ToString("F1", ci) + ",\"blockStateSenders\":" + tags.Count + ",\"byKind\":{" + string.Join(",", items) + "}}";
```

`blockStateSenders` は、束の前はタグ（＝ブロック）の数、束の後は座標の数で、どちらも「状態を出したブロック数」になる。

- [ ] **Step 2: `compare.py` に、束の前後で比べられる行を足す**

`per_event_us` の下に足す。

```python
def dispatch_ms_excluding_response(result):
    total = marker(result, "PacketExchange.DispatchReceivedPackets")
    response = marker(result, "PacketExchange.RouteResponse") or 0
    if total is None:
        return None
    return total - response


def block_states_per_second(result):
    # 束の前は状態イベント1件＝1ブロック分なので、ブロック状態の件数を合算して同じ物差しにする
    events = result["events"]
    if "blockStatesPerSecond" in events:
        return events["blockStatesPerSecond"]
    return sum(v["perSecond"] for k, v in events["byKind"].items() if k.startswith("va:event:changeBlockState"))
```

`row("受信処理 µs/件（応答を除く）", ...)` の次に足す。

```python
    # 束ねるとイベント件数の意味が変わるので、フレームあたりの受信処理時間とブロック状態の件数で比べる
    row("受信処理 ms/フレーム（応答を除く）", lambda r: dispatch_ms_excluding_response(r))
    row("ブロック状態 件/秒", lambda r: block_states_per_second(r), 0, lower_is_better=False)
```

- [ ] **Step 3: README に注意を足す**

`docs/performance/benchmark/README.md` の「### 注意」の末尾に足す。

```markdown
- **ブロック状態の束ね（ADR 0088）の前後で、イベントの数え方が変わる。** 束ねた後は「受信イベント 件/秒」に束1件が1件として入り、「受信処理 µs/件」は束1件あたりの値になるので、束の前とは比べられない。前後の比較は「受信処理 ms/フレーム（応答を除く）」と「ブロック状態 件/秒」で行う。`snippets/events-start.cs` は束の型を参照するので、束の前のコミットを測るときは、そのコミットの時点の `events-start.cs` と `events-collect.cs` を使う（`git show <commit>:docs/performance/benchmark/snippets/events-start.cs`）。Profiler の `BlockState.Deserialize` は、束の前はブロック1件ごと、束の後は束1回ごとの計測になるので、ms は比べられるが calls は比べられない。
```

同じ README の「## 結果 JSON の中身（schemaVersion 1）」の表の `events` 行を次にする。

```markdown
| `events` | 受信イベントの件数/秒・payload バイト/秒、ブロック状態の件数/秒（`blockStatesPerSecond`。束の中身の件数。束の前の結果には無い）、状態を出したブロック数（`blockStateSenders`。束の前はタグ数、束の後は座標数）。`byKind` はタグ種別ごとの内訳で、ブロック状態は状態キーの組み合わせ別（束の後は `blockState|<組み合わせ>` に件数だけ入る） |
```

`docs/performance/2026-10-22-windows-desktop-low-fps/README.md` の「## 再計測の手順」の 6 の行末に次を足す（`snippets/event-hooks/keys.cs` が座標入りタグの接頭辞で絞っており、束ねた後は何も数えなくなるため）。

```markdown
`event-hooks/keys.cs` は座標入りタグ `va:event:changeBlockState:` 前提なので、ブロック状態の束ね（ADR 0088）より前のコミット専用。
```

- [ ] **Step 4: 比較スクリプトが既存の結果で動くことを確かめる**

Run: `python3 docs/performance/benchmark/compare.py docs/performance/benchmark/results/*_before-tick-bundle_*.json`
Expected: 表が出て、「受信処理 ms/フレーム（応答を除く）」と「ブロック状態 件/秒」の行に数値が入る。

- [ ] **Step 5: コミットする**

```bash
git add docs/performance/benchmark/snippets/events-start.cs docs/performance/benchmark/snippets/events-collect.cs docs/performance/benchmark/compare.py docs/performance/benchmark/README.md docs/performance/2026-10-22-windows-desktop-low-fps/README.md
git commit -m "docs: ベンチマークをブロック状態の束でも前後比較できるようにする"
```

---

### Task 6: 変更後のベンチマークを取り、前後を記録する

**Files:**
- Create: `docs/performance/benchmark/results/<日時>_tick-bundle_<コミット>_r1.json`〜`_r3.json`
- Modify: `docs/performance/benchmark/README.md`（結果一覧）

**Interfaces:**
- Consumes: Task 1 の `before-tick-bundle` 結果、Task 5 のスニペット
- Produces: なし

- [ ] **Step 1: windows-desktop を最新にしてコンパイルする**

```bash
git push origin performance
ssh windows-desktop "git -C G:\GitHub\moorestech status --short"
ssh windows-desktop "git -C G:\GitHub\moorestech fetch origin"
ssh windows-desktop "git -C G:\GitHub\moorestech reset --hard origin/performance"
ssh windows-desktop "uloop compile --project-path G:\GitHub\moorestech\moorestech_client"
```
Expected: status は空、compile の ErrorCount 0。

- [ ] **Step 2: ベンチマークを回して比べる**

```bash
docs/performance/benchmark/run-benchmark.sh tick-bundle
python3 docs/performance/benchmark/compare.py docs/performance/benchmark/results/*_before-tick-bundle_*.json docs/performance/benchmark/results/*_tick-bundle_*.json
```
Expected: 結果 JSON 3つ。比較表が出る。`compare.py` は基準 label の同じ glob に `before-tick-bundle` も含まれるので、2つ目の glob は `*_tick-bundle_*` が `before-tick-bundle` を拾わないことを `ls` で確かめる（拾う場合は `results/2026-*_tick-bundle_<コミット>_*.json` のようにコミットで絞る）。

- [ ] **Step 3: README の結果一覧に記録する**

`docs/performance/benchmark/README.md` の「## 結果一覧」の表に、`before-tick-bundle` と `tick-bundle` の各回を1行ずつ足す。列は既存どおり（fps・PlayerLoop ms・受信イベント 件/秒・受信処理 µs/件）。メモ欄の `tick-bundle` の1行目に「ブロック状態を tick末尾に1イベントへ束ねる（ADR 0088）。受信処理 X→Y ms/フレーム、ブロック状態 N件/秒」と実測値を書く。表の下に compare.py の Markdown 表を `### 2026-10-22 ブロック状態の束ね（ADR 0088）` の見出しで貼る。改善しなかった指標・悪化した指標もそのまま書く。

- [ ] **Step 4: コミットする**

```bash
git add docs/performance/benchmark/results/*_tick-bundle_*.json docs/performance/benchmark/README.md
git commit -m "docs: ブロック状態のtick束ね送信の前後をベンチマークで計測した結果を残す"
```

---

### Task 7: 全ブランチレビュー

- [ ] **Step 1: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 4 の unityプレイ録画テストを反映後のバイナリで再実施してから完了とする。

---

### Task 8: PR作成

- [ ] **Step 1: セッション終了可能状態にすること**

pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR本文には Task 6 の比較表を貼る。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0088-block-states-bundled-per-tick.md`（範囲・送信タイミング・クライアントの振り分け・完了基準）。`.decisions/2026-10-22-ブロック状態はtick末尾に座標ごとの最新を1イベントへ束ねて送る.md`、`.decisions/2026-10-22-ブロック状態の束ね送信の完了基準はベンチマーク前後の記録.md`。
- grill の Codex 独立質問の段は省略した。出所: ユーザー裁定 2026-10-22 選択「省略して進める」（個人スキル codex-audit がこのマシンに無かったため）。
- クラス名 `ChangeBlockStateEventPacket` とファイルは変えず、タグだけを `va:event:blockStatesTickBundle` に変える。出所: agent前提（クラスは今も「ブロック状態の変化を送るイベント」で、名前と処理は一致している）
- 束の1件は型付きの `BlockStateMessagePack[]` で持ち、サーバーは差分判定用と束用で2回シリアライズする。出所: agent前提（クライアントのメインスレッドが律速。サーバーのシリアライズは別スレッドで、クライアント側で `byte[]` を1件ずつ読み直す形より単純）
- `BlockGameObject` の状態プロセッサ呼び出しごとの try-catch は、購読ラムダから `ApplyBlockState` へそのまま移す。`_onBlockStateChanged.OnNext` は隔離の外に置き、`OnBlockStateChanged` の購読者は例外を投げない前提とする（今の唯一の購読者 `BlockInventoryTopic.OnTrackedBlockStateChanged` は `SchedulePublish` で非同期に流すだけ）。購読者が投げると `EventResponseDispatcher` の購読者単位の隔離で、その束の残りのブロックへの配送が止まる。出所: agent前提（既存の隔離の移設。try-catch の新設は Global Constraints に反するため、購読者側の契約にする）
- 束を受けるハンドラの前例は、役割が同じ `TrainUnitTickDiffBundleEventNetworkHandler`・`BeltTransportEventNetworkHandler`（`Initialize()` で購読・`IDisposable`）とも突き合わせたうえで、`WorldDataHandler`（ctor で購読）に合わせる。列車・ベルトの `IInitialEventApplyWaitTarget` 相当は要らない（ブロック状態の初期データは各ブロックの `RequestBlockState` による pull で、初期イベントの適用を待つ対象が無い）。出所: agent前提（user-simulator review の Warning を受けて突合）
- 座標にブロックが無い件は `Debug.Log` を出して読み飛ばす。出所: agent前提（AGENTS.md「無音の縮退は禁止」。クライアントは接続時に全ブロックを作るので、起きるのは撤去直後などに限られ、ログが溢れない見込み）
- 同じtick内の状態の遷移（`CommonMachineBlockStateDetail.PreviousStateType`）は、上書きで失われることがある。クライアントの該当分岐は今は空で、実害は無い。出所: agent前提（`CommonMachineBlockStateChangeProcessor.OnChangeState` の確認。ADR 0088 の「座標ごとに最新1件」の帰結）
- `ChangeBlockStateEventPacket.cs` の改修（即時送信→座標ごとの最新を溜めて `BroadcastPendingStates` で送る、`BlockStatesTickBundleMessagePack` の追加、`CreateSpecifiedBlockEventTag` の削除）。サーバー可変状態の同期の3点セットは崩さない（イベント＝束、初期データ＝`RequestBlockState` の強制送信、購読＝`BlockStateEventHandler`）。出所: ユーザー裁定 2026-10-22（ADR 0088）
- `RequestBlockStateProtocol.cs` はコメントだけを変える（応答は今どおり `ForceChangeState` 経由で、そのtick末尾の束に載る）。出所: agent前提（パケットは全部 tick末尾に処理される。`ReceiveQueueProcessor` → `TickEndPacketQueue` の確認）
- `ChangeBlockStateEventPacket.cs` の座標・中身の取り出しと、記録の更新・掃除は、private の `SerializeState`・`QueueState`・`ForgetPosition` に1本ずつまとめる（取り出しは再検査の弱い指摘で、同じ回答③の範囲として追加）。テストの `TryAddBlock(..., out _)` と、束の型を Server.Event に置くことは、既存の書き方に合わせて据え置く。出所: シミュレーター予測→ユーザー承認 2026-10-22（Phase 2.6 の弱い指摘3件への回答「③の重複だけまとめる」）
- 撤去アニメーション中のブロックは、最後の状態のまま止まってよい（座標の表から外れた時点で状態を受けない）。出所: シミュレーター予測（確信高）→前提宣言（拒否権つき、2026-10-22。ユーザーの拒否なし）
- 送信の駆動は (a) `MasterTickUpdater` からの明示呼び出しを採る（新規パターン。比較は「送信の駆動の比較」）。前例の2件は EventPacket 側の購読で駆動しており、当初の前例引用は誤りだったので直した。出所: agent前提（配置検査の指摘。ADR 0087 の tick順の明示方針）
- `BlockStateEventHandler` は `Client.Game/InGame/World/` に置く（同じ役割の `WorldDataHandler` の隣）。出所: agent前提（配置検査の注目点。前例を役割で選ぶ）
- `_pendingStates` はロックを持たない。`ChangeState` はサーバーの tick スレッドからだけ呼ばれる前提で、既存の `_lastBroadcastPayloads` と同じ前提に乗る。出所: agent前提（パケットは tick末尾に処理され、ブロック更新も tick 内。前提が崩れる経路があれば既存の辞書も同じく壊れる）
- ランタイム挙動（クライアントの状態反映）に触れるので、unityプレイ録画テスト（Task 4）を入れる。
- ベンチマークの物差しを足す（Task 5）。束ねるとイベント件数の意味が変わり、既存の「受信処理 µs/件」が前後で比べられなくなるため。出所: agent前提（README の「改善の効果はまずこの2つで判断」の2指標が束で意味を変えるため）
