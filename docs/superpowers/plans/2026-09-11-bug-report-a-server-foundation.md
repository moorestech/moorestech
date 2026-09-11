# バグ報告 A: サーバー基盤（決定的乱数・tick付きセーブ・スナップショットリング・パケットログ・再生検査）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** サーバーが「30秒周期のスナップショットリング＋処理tick付き受信パケットログ」を体感できる引っかかり無しで常時記録し、任意のスナップショットからパケットを流し直して次世代スナップショットと一致することを機械的に検査できる状態にする（ADR 0057 の取得側サーバー部分と再現側の再生器）。

**Architecture:** (1) `Core.Update.GameRandom`（決定的乱数、状態をセーブに含める）と `GameUpdater.RestoreCurrentTick` で「同一スナップショット＋同一パケット列＝同一状態」を成立させる。(2) ブロックコンポーネントのセーブ状態を文字列でなくオブジェクトで返す形へ改め、`AssembleSaveJsonText` を「tickスレッドで取り込み（`Capture`）→別スレッドでJSON化・書き込み（`SaveWriteWorker`）」に分割して停止時間をtick予算内に収める。通常セーブも同じ経路に乗る。(3) `WorldSnapshotRing` が `FinalTickEndUpdates` から周期・即時スナップショットを取り、`ReceivedPacketLog` がサーバーのtick末尾処理点で「処理tick＋生バイト列」を記録する。(4) `SnapshotReplayer` がスナップショットをロードしてパケットを同じtickへ流し直し、`SnapshotJsonComparer` が次世代スナップショットと突き合わせる。(5) クライアントからの即時取得は `BugReportCaptureProtocol`（Operation enum）＋完了イベント `BugReportCaptureCompletedEventPacket`（`SaveProtocol`＋`WorldSaveCompletedEventPacket` と同型）。

**Tech Stack:** Unity C#（`moorestech_server` / `moorestech_client`）、Newtonsoft.Json、MessagePack、UniRx、NUnit（CombinedTest / UnitTest / PacketTest）、`uloop` CLI、プレイテストDSL（`Client.Playtest`）、Python 3（既存セーブの移行スクリプト）。

## Requirements

- R1. 決定的乱数: `Core.Update.GameRandom`（静的・xoshiro256**）を新設し、`Reseed(ulong)`／`ExportState()`／`RestoreState(ulong[])`／`NextDouble()`／`Next(int,int)`／`NextInt()`／`NextLong()`／`NextGuid()` を持つ。受入: 同じseedから同じ列、`ExportState`→`RestoreState` 後は続きの列が一致する単体テストが通る。
- R2. 乱数の集約: サーバーの世界状態に影響する乱数・ID採番（`BlockInstanceId.Create`・`TrainCarInstanceId.Create`・`TrainUnitInstanceId.Create`・`RailNode` ctor の Guid・`TrainDiagramEntry` の entryId・`BlueprintCreateService` の Guid・`MachineOutputFactoryUtil`・`VanillaStaticMapObject`・`MachineCurrentPowerToSubSecond`・`GearOverloadBreakageComponent`・`FuelGearGeneratorFuelService`・`GearNetworkId.CreateNetworkId`）を全て `GameRandom` へ置き換え、`new Random()`／`Guid.NewGuid()` をそれらから排除する。`ProbabilityCalculator` は使用箇所がテストのみなので削除する。受入: 上記ファイルに `new Random(`・`Guid.NewGuid()`・`System.Random` が残らない（`grep` で0件）。`ItemInstanceId`（クライアントも同一プロセスで消費する）は対象外とし判断記録に残す。
- R3. セーブへの tick と乱数状態の追加: `WorldSaveAllInfoV1` に `currentTick`（ulong）と `randomState`（ulong[4]）を追加し、`WorldLoaderFromJson.Load` の先頭で `GameUpdater.RestoreCurrentTick` と `GameRandom.RestoreState` を行う。受入: セーブ→別DIでロード後に `GameUpdater.CurrentTick` と `GameRandom.ExportState()` がセーブ時と一致する。
- R4. ブロックセーブ状態のオブジェクト化: `IBlockSaveState.GetSaveState()` の戻り値を `object`、`IBlock.GetSaveState()` を `Dictionary<string, object>`、`BlockJsonObject.ComponentStates` を `Dictionary<string, object>`、`IBlockTemplate.Load`／`BlockFactory.Load` の引数を `Dictionary<string, object>` に改め、26コンポーネントの `JsonConvert.SerializeObject(x)`／`JsonUtility.ToJson(x)` を「`x` をそのまま返す」に、ロード側の `DeserializeObject<T>(componentStates[Key])` を `BlockComponentStateReader.Read<T>`／`TryRead<T>` に置き換える。受入: 既存の SaveLoad 系テスト（`Tests/UnitTest/Game/SaveLoad/*` 32本、`Tests/CombinedTest/Game/*SaveLoadTest.cs` 5本）が全て通り、`grep -rn "JsonConvert.SerializeObject\|JsonUtility.ToJson" moorestech_server/Assets/Scripts/Game.Block/Blocks` が0件。
- R5. 取り込みと書き出しの分離: `AssembleSaveJsonText` を `Capture()`（tickスレッド、`WorldSaveAllInfoV1` を返す）と `static Serialize(WorldSaveAllInfoV1)` に分け、`SaveWriteWorker`（専用スレッド1本）が JSON化とアトミック書き込みを行う。`WorldSaveCoordinator` は `SaveIfRequested` で取り込みとジョブ投入だけを行い、完了通知はtickスレッド側で完了キューを排出して `OnWorldSaveCompleted` を発火する。受入: 「取り込み後に世界を変更してから書き出しても、取り込み直後にJSON化した文字列と一致する」CombinedTest が通る（生きた参照を返しているコンポーネントを検出する）。`WorldSaveCoordinatorTest`／`TickEndSaveConsistencyTest` は `WaitForPendingWrites()` を挟んで通る。
- R6. スナップショットリング: `WorldSnapshotRing` が `Start(periodTicks, generations)` 後、周期ごとおよび `RequestImmediateSnapshot()` 要求時に `FinalTickEndUpdates` 内でスナップショットを取り、`<セーブファイルのディレクトリ>/snapshots/tick_<T>.json` へ書き、世代数を超えた古い世代とそれより前のパケットログ区間を削除する。周期30秒（600tick）・4世代は `SnapshotRingConfig` の定数。通常起動はオン、`StartServerSettings.CaptureRing=false` でオフ（プレイテストDSL・EditModeInPlayingTest・NUnitテストはオフ）。受入: CombinedTest で period=10・generations=3 で 40tick 進めると `tick_20/30/40.json` だけが残る。
- R7. パケットログ: `ReceivedPacketLog` が `ReceiveQueueProcessor.ProcessPacket` の直前で「`GameUpdater.CurrentTick`＋生バイト列」を `snapshots/packets_<fromTick>.bin` へ追記し、スナップショット書き出しごとに区間を切り替える。`ReceivedPacketLogReader` が全レコードを読み戻せる。受入: CombinedTest で投入したパケットが処理tick付きで読み戻せる。
- R8. 即時取得プロトコル: `va:bugReportCapture`（Operation=CaptureNow）が `RequestImmediateSnapshot()` の要求IDを返し、書き出し完了時に `va:event:bugReportCaptureCompleted`（要求ID・tick・スナップショットディレクトリ・スナップショットファイル名一覧・パケットログファイル名一覧）を全員へ配信する。クライアント `VanillaApiWithResponse.RequestBugReportCapture(ct)` を追加する。受入: PacketTest で要求→1tick→完了イベント受信。
- R9. 再生器と比較器: `SnapshotReplayer.Replay(ReplayRequest)` がスナップショットをロードし、`(loadedTick, targetTick]` のパケットを同じ処理tickへ流して `targetTick` まで進め、`WorldSaveAllInfoV1` のJSONを返す。`SnapshotJsonComparer.Compare(expected, actual)` は `setting`（実時刻を含む）を除外して深い比較を行い、不一致パスを返す。受入: CombinedTest「スナップショットkから再生してk+1と一致する」が通る。
- R10. 引っかかり無し: world_1 複製（8048ブロック）でリングを有効にした90秒のプレイテストで、`GameUpdater.CurrentTick` の増分が壁時計換算の期待値から3tick以上欠けず、`Capture()` の所要が20ms以下。受入: シナリオ `snapshot-ring-no-hitch.cs` の Assert が全てPASS（結果は判断記録へ数値を残す）。
- R11. 既存セーブの移行: `scripts/save_migration/migrate_block_state_objects.py` が `world[].state` の文字列値をJSONオブジェクトへ展開し、`currentTick`・`randomState` を付与する。受入: `~/Library/Application Support/moorestech/Saves/world_1/save.json` の複製を移行し、`moorestech-save-migration` スキルの `load_test.cs` で `LOAD OK` を確認する。
- やらないこと: クライアントの録画・ログリング・バンドル組み立て・報告UI（plan B）／Mac mini側の運搬・inbox・自動修正ラン（plan C）／`ItemInstanceId` の決定化／`WorldSettingsDatastore` の `DateTime` 撤去／ブロック内部の入れ子文字列（ベルトの `item.GetSaveJsonString()`）のオブジェクト化／セーブ形式のバージョン分岐／リング周期の実行時設定UI。

## Global Constraints

- 作業ブランチ: `feature/bug-report-auto-fix`（設計コミット `b9140cce5` 以降、`origin/master` をマージ済み）。plan B・C は本planの完了コミットを土台にする。
- `../moorestech_master` は本planでは変更しない。作業ツリーのピン `.moorestech-external-revisions.json` はUnityが実チェックアウト値へ自動書き戻しする（コミット済みの値が正）。どのタスクでも `git add` しない。`git add` は常にファイル指定で行う。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client`。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。Domain Reload エラー時は45秒待って再実行。**新規サーバー側 `.cs` は Unity 再起動（`uloop launch ./moorestech_client --restart`）が必要**（file: パッケージのため Refresh では検出されない。creating-server-tests スキル）。
- サーバーtickは `Server.Boot/Loop/ServerGameUpdater.cs` の専用スレッド（1tick=50ms、追いつき処理なし）。tickスレッドで走るのは `GameUpdater.Update()` の4段（AdditionalUpdates→Update→TickEndUpdates→FinalTickEndUpdates）。パケットは `WorldMutationTickEndUpdater.Update()`（TickEndUpdates）で処理され、セーブ・スナップショットは FinalTickEndUpdates。
- 経過時間は `GameUpdater.CurrentTick` の差分のみ。`Stopwatch` を使ってよいのはプレイテストシナリオ（計測専用・ゲームロジック外）だけ。
- コメントは「// 日本語 → // English」2行セット、各1行。1ファイル200行以下、1ディレクトリ10ファイル以下。partial・`Func<>`・try-catch（外部境界を除く）・デフォルト引数・単純getter/setter プロパティ禁止。イベントは UniRx `Subject<T>`＋`IObservable<T>`。fail-closed 経路（早期return・無視）は必ず `Debug.Log*` で理由を出す。
- `#region Internal` はメソッド内ローカル関数の集約用途のみ。クラス直下のprivateメソッド群を囲わない。
- DTO（MessagePack）はプロトコルクラス内ネスト（`#region MessagePack`）。`[Obsolete]` 引数なしctor必須。`Server.Protocol/PacketResponse/` 直下は `IPacketResponse` 実装のみ。
- 命名: 型名・ファイル名は本plan記載のとおり（`GameRandom`・`BlockComponentStateReader`・`SaveWriteWorker`・`SaveWriteJob`・`SaveWriteCompletion`・`WorldSnapshotRing`・`SnapshotRingConfig`・`SnapshotWritten`・`ReceivedPacketLog`・`ReceivedPacketLogReader`・`ReceivedPacketRecord`・`SnapshotJsonComparer`・`SnapshotComparison`・`BugReportCaptureProtocol`・`BugReportCaptureCompletedEventPacket`・`SnapshotReplayer`・`ReplayRequest`・`ReplayResult`・`ReplayPacketEntry`）。
- 各タスク末尾でコミット。コミットメッセージ末尾に以下を付ける:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SE4dG7rpvBJhbf1rsbiQXN
  ```

---

### Task 1: `GameRandom`（決定的乱数）と `GameUpdater.RestoreCurrentTick`

**Files:**
- Create: `moorestech_server/Assets/Scripts/Core.Update/GameRandom.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Update/GameUpdater.cs:17`（`CurrentTick` の直後に `RestoreCurrentTick` を追加）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Update/GameRandomTest.cs`（新規）

**Interfaces:**
- Consumes: なし
- Produces:
  - `public static class Core.Update.GameRandom { public const int StateLength = 4; public static void Reseed(ulong seed); public static ulong[] ExportState(); public static void RestoreState(ulong[] state); public static ulong NextUlong(); public static double NextDouble(); public static int Next(int minInclusive, int maxExclusive); public static int NextInt(); public static long NextLong(); public static Guid NextGuid(); }`
  - `public static void Core.Update.GameUpdater.RestoreCurrentTick(ulong tick)`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Update/GameRandomTest.cs`:

```csharp
using System;
using Core.Update;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Update
{
    public class GameRandomTest
    {
        [Test]
        public void 同じシードからは同じ列が出る()
        {
            GameRandom.Reseed(12345UL);
            var first = new[] { GameRandom.NextUlong(), GameRandom.NextUlong(), GameRandom.NextUlong() };
            GameRandom.Reseed(12345UL);
            var second = new[] { GameRandom.NextUlong(), GameRandom.NextUlong(), GameRandom.NextUlong() };
            CollectionAssert.AreEqual(first, second, "同じシードで列が一致しない");
        }

        [Test]
        public void 状態を書き出して戻すと続きの列が一致する()
        {
            GameRandom.Reseed(777UL);
            GameRandom.NextUlong();
            var state = GameRandom.ExportState();
            var expected = new[] { GameRandom.NextInt(), GameRandom.Next(0, 10), GameRandom.NextInt() };
            var expectedGuid = GameRandom.NextGuid();
            var expectedDouble = GameRandom.NextDouble();

            GameRandom.RestoreState(state);
            var actual = new[] { GameRandom.NextInt(), GameRandom.Next(0, 10), GameRandom.NextInt() };
            CollectionAssert.AreEqual(expected, actual, "復元後の列が一致しない");
            Assert.AreEqual(expectedGuid, GameRandom.NextGuid(), "復元後のGuidが一致しない");
            Assert.AreEqual(expectedDouble, GameRandom.NextDouble(), "復元後のdoubleが一致しない");
        }

        [Test]
        public void 範囲付き乱数は範囲内に収まりdoubleは0以上1未満()
        {
            GameRandom.Reseed(1UL);
            for (var i = 0; i < 10000; i++)
            {
                var value = GameRandom.Next(-3, 5);
                Assert.IsTrue(-3 <= value && value < 5, $"範囲外: {value}");
                var d = GameRandom.NextDouble();
                Assert.IsTrue(0d <= d && d < 1d, $"範囲外: {d}");
            }
        }

        [Test]
        public void 状態の長さが不正なら例外()
        {
            Assert.Throws<ArgumentException>(() => GameRandom.RestoreState(new ulong[3]));
        }

        [Test]
        public void tickを復元できる()
        {
            GameUpdater.RestoreCurrentTick(4242UL);
            Assert.AreEqual(4242UL, GameUpdater.CurrentTick);
        }
    }
}
```

- [ ] **Step 2: Unity を再起動してテストを実行し失敗を確認する**

Run: `uloop launch ./moorestech_client --restart` → `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー（`GameRandom` が存在しない）

- [ ] **Step 3: `GameRandom` を実装する**

`moorestech_server/Assets/Scripts/Core.Update/GameRandom.cs`:

```csharp
using System;

namespace Core.Update
{
    // 世界状態に影響する乱数の唯一の供給源。状態をセーブへ含め、同一スナップショット＋同一パケット列で同一結果にする
    // Sole source of world-affecting randomness; its state is saved so the same snapshot plus packets replays identically
    public static class GameRandom
    {
        public const int StateLength = 4;

        private static ulong _s0, _s1, _s2, _s3;

        static GameRandom()
        {
            Reseed(0UL);
        }

        // SplitMix64で4語の状態を初期化する（移行スクリプトと同じ手順であること）
        // Seed the four state words with SplitMix64 (must match the migration script)
        public static void Reseed(ulong seed)
        {
            var x = seed;
            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);
            _s2 = SplitMix64(ref x);
            _s3 = SplitMix64(ref x);
        }

        public static ulong[] ExportState()
        {
            return new[] { _s0, _s1, _s2, _s3 };
        }

        public static void RestoreState(ulong[] state)
        {
            if (state == null || state.Length != StateLength) throw new ArgumentException($"randomState は {StateLength} 語でなければならない");
            _s0 = state[0];
            _s1 = state[1];
            _s2 = state[2];
            _s3 = state[3];
        }

        // xoshiro256**
        public static ulong NextUlong()
        {
            var result = RotateLeft(_s1 * 5UL, 7) * 9UL;
            var t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);
            return result;
        }

        // 上位53bitを使い [0,1) を作る
        // Build [0,1) from the top 53 bits
        public static double NextDouble()
        {
            return (NextUlong() >> 11) * (1.0 / 9007199254740992.0);
        }

        public static int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentException("maxExclusive は minInclusive より大きくなければならない");
            var range = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextUlong() % range));
        }

        public static int NextInt()
        {
            return (int)(uint)(NextUlong() >> 32);
        }

        public static long NextLong()
        {
            return (long)NextUlong();
        }

        // 128bitを取りRFC4122のversion4形式に整えて決定的なGuidを作る
        // Take 128 bits and shape them as an RFC4122 version-4 GUID deterministically
        public static Guid NextGuid()
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(NextUlong()).CopyTo(bytes, 0);
            BitConverter.GetBytes(NextUlong()).CopyTo(bytes, 8);
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            return new Guid(bytes);
        }

        private static ulong SplitMix64(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            var z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong RotateLeft(ulong x, int k)
        {
            return (x << k) | (x >> (64 - k));
        }
    }
}
```

- [ ] **Step 4: `GameUpdater.RestoreCurrentTick` を追加する**

`GameUpdater.cs` の `public static ulong CurrentTick { get; private set; }` の直後に追加:

```csharp
        // セーブのロード時にだけ呼ぶ。tickスレッド開始前に呼ぶこと（走行中に巻き戻すと計測中の差分が負に回り込む）
        // Called only when loading a save, before the tick thread starts (rewinding mid-run wraps in-flight differences)
        public static void RestoreCurrentTick(ulong tick)
        {
            CurrentTick = tick;
        }
```

- [ ] **Step 5: コンパイルしてテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.UnitTest\.Core\.Update\.GameRandomTest$"`
Expected: 5件 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Core.Update/GameRandom.cs moorestech_server/Assets/Scripts/Core.Update/GameUpdater.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Update/GameRandomTest.cs
git commit -m "feat(server): 決定的乱数GameRandomとGameUpdater.RestoreCurrentTickを追加"
```
（`.meta` はUnityが生成したものをまとめて `git add` する。以降のタスクも同じ）

---

### Task 2: 世界状態に影響する乱数・ID採番を `GameRandom` へ集約する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/BlockInstanceId.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Train/TrainUnitInstanceId.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainCarInstanceId.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Train/RailGraph/RailNode.cs:73`
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Diagram/TrainDiagramEntry.cs:104`
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCreateService.cs:42`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/Util/MachineOutputFactoryUtil.cs:14,24,51`
- Modify: `moorestech_server/Assets/Scripts/Game.Map/VanillaStaticMapObject.cs:32,48,107`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Util/MachineCurrentPowerToSubSecond.cs:12-13,30`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearOverloadBreakageComponent.cs:19-20,56`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/FuelGearGeneratorFuelService.cs:17-18,245`
- Modify: `moorestech_server/Assets/Scripts/Game.Gear/Common/GearNetworkId.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.Block/ProbabilityCalculator.cs`（＋`.meta`）と `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/UtilTest.cs` 内の当該テスト
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Game.Block.Interface.asmdef`・`moorestech_server/Assets/Scripts/Game.Blueprint/Game.Blueprint.asmdef`（`references` に `"Core.Update"` を追加。`Game.Train`・`Game.Map`・`Game.Gear`・`Game.Block` は既に参照済み）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/GameRandomIdDeterminismTest.cs`（新規）

**Interfaces:**
- Consumes: Task 1 の `GameRandom`
- Produces: 各 `Create()` の戻り値が `GameRandom` 由来になる（シグネチャ不変）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/UnitTest/Game/GameRandomIdDeterminismTest.cs`:

```csharp
using Core.Update;
using Game.Block.Interface;
using Game.Gear.Common;
using Game.Train.Unit;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class GameRandomIdDeterminismTest
    {
        [Test]
        public void 同じ乱数状態からのID採番は一致する()
        {
            GameRandom.Reseed(99UL);
            var block1 = BlockInstanceId.Create();
            var car1 = TrainCarInstanceId.Create();
            var unit1 = TrainUnitInstanceId.Create();
            var gear1 = GearNetworkId.CreateNetworkId();

            GameRandom.Reseed(99UL);
            Assert.AreEqual(block1, BlockInstanceId.Create(), "BlockInstanceId が乱数状態に従っていない");
            Assert.AreEqual(car1, TrainCarInstanceId.Create(), "TrainCarInstanceId が乱数状態に従っていない");
            Assert.AreEqual(unit1, TrainUnitInstanceId.Create(), "TrainUnitInstanceId が乱数状態に従っていない");
            Assert.AreEqual(gear1, GearNetworkId.CreateNetworkId(), "GearNetworkId が乱数状態に従っていない");
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop launch ./moorestech_client --restart` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.UnitTest\.Game\.GameRandomIdDeterminismTest$"`
Expected: FAIL（各IDが `new Random()` 由来で一致しない）

- [ ] **Step 3: ID採番4件を置き換える**

`BlockInstanceId.cs`:
```csharp
using Core.Update;
using UnitGenerator;

namespace Game.Block.Interface
{
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public readonly partial struct BlockInstanceId
    {
        public static BlockInstanceId Create()
        {
            return new BlockInstanceId(GameRandom.NextInt());
        }
    }
}
```
（既存の `partial` は UnitGenerator 生成のため据え置く。新規に partial を書かない）

`TrainUnitInstanceId.cs`:
```csharp
using System;
using Core.Update;
using UnitGenerator;

namespace Game.Train.Unit
{
    [UnitOf(typeof(Guid), UnitGenerateOptions.MessagePackFormatter)]
    public readonly partial struct TrainUnitInstanceId
    {
        public static TrainUnitInstanceId Create()
        {
            return new TrainUnitInstanceId(GameRandom.NextGuid());
        }
    }
}
```

`TrainCarInstanceId.cs`:
```csharp
using Core.Update;
using UnitGenerator;

namespace Game.Train.Unit
{
    [UnitOf(typeof(long), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public readonly partial struct TrainCarInstanceId
    {
        public static TrainCarInstanceId Create()
        {
            return new TrainCarInstanceId(GameRandom.NextLong());
        }
    }
}
```

`GearNetworkId.cs`:
```csharp
using Core.Update;
using UnitGenerator;

namespace Game.Gear.Common
{
    [UnitOf(typeof(int))]
    public readonly partial struct GearNetworkId
    {
        public static GearNetworkId CreateNetworkId()
        {
            return new GearNetworkId(GameRandom.NextInt());
        }
    }
}
```

- [ ] **Step 4: Guid採番3件を置き換える**

`RailNode.cs:73` の `Guid = Guid.NewGuid();` → `Guid = GameRandom.NextGuid();`（`using Core.Update;` を追加）
`TrainDiagramEntry.cs:104` の `entryId = Guid.NewGuid();` → `entryId = GameRandom.NextGuid();`（`using Core.Update;` を追加）
`BlueprintCreateService.cs:42` の `System.Guid.NewGuid()` → `GameRandom.NextGuid()`（`using Core.Update;` を追加、`Game.Blueprint.asmdef` の `references` に `"Core.Update"` を追加）

- [ ] **Step 5: 確率系4件を置き換える**

`MachineOutputFactoryUtil.cs`: L14 の `private static readonly Random Random = new();` を削除し、L24 `Random.NextDouble()` と L51 `Random.NextDouble()` を `GameRandom.NextDouble()` に置き換える（`using Core.Update;` 追加。`using System;` が他で不要なら削除）。

`VanillaStaticMapObject.cs`: L32 `private readonly Random _random;` と L48 `_random = new Random();` を削除し、L107 を `var itemCount = GameRandom.Next(earnItemConfig.MinCount, earnItemConfig.MaxCount + 1);` に置き換える（`using Core.Update;` 追加）。コメント「採掘設定と乱数生成器を準備する」は「採掘設定を準備する / Prepare the mining settings」に直す。

`MachineCurrentPowerToSubSecond.cs`: L12-13 の `RandomSeed`/`SharedRandom` を削除し L30 を `if (GameRandom.NextDouble() < remainder) wholeTicks++;` に。
`GearOverloadBreakageComponent.cs`: L19-20 を削除し L56 を `if (GameRandom.NextDouble() <= chance) RequestRemove();` に。
`FuelGearGeneratorFuelService.cs`: L17-18 を削除し L245 を `ticksToConsume = GameRandom.NextDouble() < operatingRate ? 1u : 0u;` に。
（3ファイルとも `using Core.Update;` を追加。`using System;` は他で使っていなければ削除）

- [ ] **Step 6: `ProbabilityCalculator` を削除する**

`Game.Block/ProbabilityCalculator.cs` と `.meta` を `git rm`。`Tests/UnitTest/Core/Other/UtilTest.cs` の `ProbabilityCalculator` を使うテストメソッドを削除する（ファイル内に他のテストが無ければファイルごと削除）。

- [ ] **Step 7: 残存確認・コンパイル・テスト**

Run:
```bash
grep -rn "new Random(\|Guid.NewGuid()\|System.Random" moorestech_server/Assets/Scripts --include='*.cs' | grep -v "/Tests/\|/Tests.Module/\|/Game.MapGeneration/\|ItemInstanceId.cs\|WorldSettingsDatastore.cs"
```
Expected: 0件（`Game.MapGeneration` は明示seedなので対象外、`ItemInstanceId` は対象外）

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.UnitTest\.Game\.GameRandomIdDeterminismTest$|^Tests\.CombinedTest\.Game\..*Train.*|^Tests\.UnitTest\.Game\.Gear\..*"`
Expected: PASS（列車・歯車の既存テストが乱数集約後も通る）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface moorestech_server/Assets/Scripts/Game.Train moorestech_server/Assets/Scripts/Game.Blueprint moorestech_server/Assets/Scripts/Game.Block/Blocks moorestech_server/Assets/Scripts/Game.Map/VanillaStaticMapObject.cs moorestech_server/Assets/Scripts/Game.Gear/Common/GearNetworkId.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/GameRandomIdDeterminismTest.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other
git rm moorestech_server/Assets/Scripts/Game.Block/ProbabilityCalculator.cs moorestech_server/Assets/Scripts/Game.Block/ProbabilityCalculator.cs.meta
git commit -m "refactor(server): 世界状態に影響する乱数とID採番をGameRandomへ集約"
```

---

### Task 3: ブロックのセーブ状態を文字列からオブジェクトへ改める（26コンポーネント一括）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/IBlockSaveState.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/BlockComponentStateReader.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/IBlock.cs:33`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/IBlockTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.World.Interface/DataStore/BlockJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BlockSystem.cs:72-86`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockFactory.cs:57`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/**`（`Dictionary<string, string> componentStates` を持つテンプレート31ファイルと `BlockTemplateUtil.cs`）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/**`（`IBlockSaveState` 実装26ファイル。一覧は Step 4 の表）
- Modify: テスト（`Dictionary<string, string>` で状態を組んでいる6ファイル: `Tests/CombinedTest/Core/Transport/FilterSplitterTest.cs`・`Tests/CombinedTest/Core/CleanRoom/CleanRoomHatchTest.cs`・`Tests/CombinedTest/Core/CleanRoom/CleanRoomAirFilterTest.cs`・`Tests/UnitTest/Game/SaveLoad/FluidPipeSaveLoadTest.cs`・`Tests/UnitTest/Game/SaveLoad/ChestSaveLoadTest.cs`・`Tests/UnitTest/Game/SaveLoad/BeltConveyorSaveLoadTest.cs`）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/BlockComponentStateReaderTest.cs`（新規）

**Interfaces:**
- Consumes: なし
- Produces:
  - `object IBlockSaveState.GetSaveState()`（Newtonsoftで直列化できる、生きたコレクション参照を含まない新規オブジェクト）
  - `Dictionary<string, object> IBlock.GetSaveState()`
  - `Dictionary<string, object> BlockJsonObject.ComponentStates`（`[JsonProperty("state")]`）
  - `IBlock IBlockTemplate.Load(Dictionary<string, object> componentStates, BlockMasterElement, BlockInstanceId, BlockPositionInfo)`
  - `IBlock BlockFactory.Load(Guid blockGuid, BlockInstanceId, Dictionary<string, object> state, BlockPositionInfo)`
  - `public static class Game.Block.Interface.Component.BlockComponentStateReader { public static T Read<T>(IReadOnlyDictionary<string, object> componentStates, string saveKey); public static bool TryRead<T>(IReadOnlyDictionary<string, object> componentStates, string saveKey, out T value); }`

- [ ] **Step 1: リーダーの失敗するテストを書く**

`Tests/UnitTest/Game/SaveLoad/BlockComponentStateReaderTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class BlockComponentStateReaderTest
    {
        private class SampleState
        {
            public int Count;
            public List<string> Names = new();
        }

        [Test]
        public void JTokenの値はToObjectで読める()
        {
            var token = JObject.Parse("{\"Count\":3,\"Names\":[\"a\",\"b\"]}");
            var states = new Dictionary<string, object> { { "k", token } };
            var read = BlockComponentStateReader.Read<SampleState>(states, "k");
            Assert.AreEqual(3, read.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, read.Names);
        }

        [Test]
        public void 同一プロセス内のオブジェクトはそのまま読める()
        {
            var original = new SampleState { Count = 7 };
            var states = new Dictionary<string, object> { { "k", original } };
            Assert.AreSame(original, BlockComponentStateReader.Read<SampleState>(states, "k"));
        }

        [Test]
        public void 旧形式の文字列は移行を促す例外になる()
        {
            var states = new Dictionary<string, object> { { "k", "{\"Count\":3}" } };
            var e = Assert.Throws<InvalidOperationException>(() => BlockComponentStateReader.Read<SampleState>(states, "k"));
            StringAssert.Contains("migrate_block_state_objects.py", e.Message);
        }

        [Test]
        public void キーが無ければTryReadはfalse()
        {
            var states = new Dictionary<string, object>();
            Assert.IsFalse(BlockComponentStateReader.TryRead<SampleState>(states, "k", out _));
        }
    }
}
```

- [ ] **Step 2: `BlockComponentStateReader` と interface 変更を実装する**

`Game.Block.Interface/Component/BlockComponentStateReader.cs`:

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.Block.Interface.Component
{
    // セーブ状態の値は「ロード時はJToken」「同一プロセス内の往復ではオブジェクト」の2形。文字列は旧形式として拒否する
    // A state value is a JToken when loaded from disk or the object itself for in-process round trips; strings are the old format
    public static class BlockComponentStateReader
    {
        public static T Read<T>(IReadOnlyDictionary<string, object> componentStates, string saveKey)
        {
            if (!TryRead<T>(componentStates, saveKey, out var value))
            {
                throw new KeyNotFoundException($"ブロックのセーブ状態にキー {saveKey} がありません");
            }
            return value;
        }

        public static bool TryRead<T>(IReadOnlyDictionary<string, object> componentStates, string saveKey, out T value)
        {
            if (!componentStates.TryGetValue(saveKey, out var raw))
            {
                value = default;
                return false;
            }
            if (raw is string)
            {
                throw new InvalidOperationException($"キー {saveKey} のセーブ状態が旧形式（JSON文字列）です。scripts/save_migration/migrate_block_state_objects.py で移行してください");
            }
            if (raw is JToken token)
            {
                value = token.ToObject<T>();
                return true;
            }
            value = (T)raw;
            return true;
        }
    }
}
```

`IBlockSaveState.cs`:
```csharp
namespace Game.Block.Interface.Component
{
    public interface IBlockSaveState : IBlockComponent
    {
        public string SaveKey { get; }

        // 新規に組み立てた、生きたコレクション参照を含まないオブジェクトを返す（JSON化は別スレッドで行われる）
        // Return a freshly built object with no live collection references (serialization happens on another thread)
        object GetSaveState();
    }
}
```

`IBlock.cs:33` → `public Dictionary<string, object> GetSaveState();`
`IBlockTemplate.cs` の `Load` 引数 → `Dictionary<string, object> componentStates`
`BlockJsonObject.cs`: `[JsonProperty("state")] public Dictionary<string, object> ComponentStates;`、コンストラクタ引数も `Dictionary<string, object> componentStates`
`BlockSystem.cs:72-86`: `var result = new Dictionary<string, object>();`（戻り値型も `Dictionary<string, object>`）
`BlockFactory.cs:57`: `Dictionary<string, object> state`

- [ ] **Step 3: テンプレート31ファイルと `BlockTemplateUtil` の引数型を置き換える**

Run（機械置換。対象は `Game.Block/Factory/BlockTemplate/` 配下のみ）:
```bash
grep -rl "Dictionary<string, string> componentStates" moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate | xargs sed -i '' 's/Dictionary<string, string> componentStates/Dictionary<string, object> componentStates/g'
```
`BlockTemplateUtil.MachineLoadState` の先頭2行を置き換える:
```csharp
            var jsonObject = BlockComponentStateReader.Read<VanillaMachineJsonObject>(componentStates, VanillaMachineSaveComponent.SaveKeyStatic);
```
（`var state = componentStates[...]` の行は削除。`using Game.Block.Interface.Component;` を追加、`Newtonsoft.Json` の using は他で不要なら削除）

- [ ] **Step 4: 26コンポーネントを一括で置き換える**

変換規則は3つだけ。全ファイルにこの規則を機械的に適用する:
- (a) `public string GetSaveState()` → `public object GetSaveState()`。本体の `return JsonConvert.SerializeObject(x);` → `return x;`。`JsonUtility.ToJson(x)` も同じく `x` を返す。
- (b) ロード側 `JsonConvert.DeserializeObject<T>(componentStates[SaveKey])` → `BlockComponentStateReader.Read<T>(componentStates, SaveKey)`。
- (c) ロード側 `if (!componentStates.TryGetValue(SaveKey, out var raw)) return; var data = JsonConvert.DeserializeObject<T>(raw);` → `if (!BlockComponentStateReader.TryRead<T>(componentStates, SaveKey, out var data)) return;`（`JsonUtility.FromJson<T>(raw)` も同じ）。
- 引数型 `Dictionary<string, string> componentStates` → `Dictionary<string, object> componentStates`（フィールド `_componentStates` も同型に）。
- 返すオブジェクトが**フィールドの生きたコレクションをそのまま抱えていない**ことを各ファイルで確認する（例: `new GearChainPoleSaveDataJsonObject(_chainTargets)` がリスト参照を保持するなら、DTO側のctorで `new List<>(source)` にコピーする。`Dictionary<Guid,double>` を新規生成している箇所はそのままでよい）。

| # | ファイル（`Game.Block/Blocks/` 相対） | 保存側の型 | ロード側 |
|---|---|---|---|
| 1 | `BaseCamp/BaseCampComponent.cs` | `List<ItemStackSaveJsonObject>` | (b) |
| 2 | `BeltConveyor/VanillaBeltConveyorComponent.cs` | `List<string>`（要素の入れ子文字列は据え置き） | (b) |
| 3 | `Chest/VanillaChestComponent.cs` | `List<ItemStackSaveJsonObject>` | (b) |
| 4 | `CleanRoom/CleanRoomAirFilterComponent.cs` | `CleanRoomAirFilterSaveJsonObject` | (c) |
| 5 | `CleanRoom/CleanRoomItemHatchComponent.cs` | `List<ItemStackSaveJsonObject>` | (b) |
| 6 | `CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs`＋`CleanRoomMachineProcessorSaveState.cs` | `CleanRoomMachineProcessorSaveJsonObject`（`Build` の戻り値をそのまま返す。`Restore` の第1引数を `Dictionary<string, object>` にし内部を (b)/(c) へ） | Restore内 |
| 7 | `ElectricToGear/ElectricToGearGeneratorComponent.cs` | `ElectricToGearGeneratorSaveJsonObject` | (c) |
| 8 | `ElectricWire/ElectricWireConnectorComponent.cs` | `ElectricWireSaveDataJsonObject`（ctorがリスト参照を持つならコピー） | (c) |
| 9 | `FilterSplitter/VanillaFilterSplitterComponent.cs` | 入れ子 `SaveJsonObject` | (c) |
| 10 | `Fluid/FluidPipeSaveComponent.cs` | `FluidPipeSaveJsonObject` | `FluidPipeComponent.cs` 側 (b) |
| 11 | `Gear/FuelGearGeneratorComponent.cs` | `FuelGearGeneratorSaveData`（`JsonUtility.ToJson` を廃止して直接返す） | (c)（`JsonUtility.FromJson` を廃止） |
| 12 | `Gear/FuelGearGeneratorFluidComponent.cs` | `FuelGearGeneratorFluidSaveData` | (c) |
| 13 | `Gear/FuelGearGeneratorItemComponent.cs` | `List<ItemStackSaveJsonObject>` | (c) |
| 14 | `GearChainPole/GearChainPoleComponent.cs` | `GearChainPoleSaveDataJsonObject`（ctorがリスト参照を持つならコピー） | (c) |
| 15 | `GearToElectric/GearToElectricGeneratorComponent.cs` | `GearToElectricGeneratorSaveJsonObject` | (c) |
| 16 | `Machine/VanillaMachineSaveComponent.cs` | `VanillaMachineJsonObject` | `BlockTemplateUtil.MachineLoadState`（Step 3） |
| 17 | `MapObjectMiner/VanillaGearMapObjectMinerProcessorComponent.cs` | `Dictionary<Guid, double>` | (b) |
| 18 | `Miner/VanillaMinerProcessorComponent.cs` | `VanillaElectricMinerSaveJsonObject` | (b) |
| 19 | `PowerGenerator/VanillaElectricGeneratorComponent.cs` | `VanillaElectricGeneratorSaveJsonObject` | (c) |
| 20 | `Pump/PumpFluidOutputComponent.cs` | `FluidContainerSaveJsonObject` | (c) |
| 21 | `TrainRail/ContainerComponents/TrainPlatformFluidContainerComponent.cs` | `TrainPlatformFluidContainerSaveJsonObject` | (c) |
| 22 | `TrainRail/ContainerComponents/TrainPlatformItemContainerComponent.cs` | `TrainPlatformItemContainerSaveJsonObject` | (c) |
| 23 | `TrainRail/RailComponentStateDetailComponent.cs` | `RailComponentSaveJsonObject` | (b) |
| 24 | `TrainRail/TrainPlatformDockingComponent.cs` | `TrainPlatformDockingComponentSaveData` | (b) |
| 25 | `TrainRail/TrainPlatformTransferComponent.cs` | `TrainPlatformTransferComponentSaveData` | (b) |
| 26 | `TrainRail/TrainStationComponent.cs` | `TrainStationComponentSaveData` | (b) |

規則 (a)+(b) の完成形（#3 チェスト）:
```csharp
        public VanillaChestComponent(Dictionary<string, object> componentStates, BlockInstanceId blockInstanceId, int slotNum, IBlockInventoryInserter blockInventoryInserter) :
            this(blockInstanceId, slotNum, blockInventoryInserter)
        {
            var itemJsons = BlockComponentStateReader.Read<List<ItemStackSaveJsonObject>>(componentStates, SaveKey);
            if (itemJsons == null) return;
            // （以降は既存のまま）
        }

        public string SaveKey { get; } = typeof(VanillaChestComponent).FullName;
        public object GetSaveState()
        {
            CheckDestroy(this);

            var itemJson = new List<ItemStackSaveJsonObject>();
            foreach (var item in _itemDataStoreService.InventoryItems)
            {
                itemJson.Add(new ItemStackSaveJsonObject(item));
            }

            return itemJson;
        }
```

規則 (a)+(c) の完成形（#11 燃料歯車発電機）:
```csharp
            if (!BlockComponentStateReader.TryRead<FuelGearGeneratorSaveData>(componentStates, SaveKey, out var saveData)) return;

            _fuelService.Restore(saveData);
            _stateService.Restore(saveData);
            GenerateRpm = _stateService.CurrentGeneratedRpm;
            GenerateTorque = _stateService.CurrentGeneratedTorque;
```
```csharp
        public object GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return new FuelGearGeneratorSaveData(_stateService, _fuelService);
        }
```

- [ ] **Step 5: テスト6ファイルの状態辞書を `Dictionary<string, object>` にする**

各ファイルの `new Dictionary<string, string>` （セーブ状態を入れる箇所のみ）を `new Dictionary<string, object>` に置き換える。値が `GetSaveState()` の戻り値ならそのまま（オブジェクト）。値がJSON文字列リテラルの箇所は `JToken.Parse(文字列)` に包む（`using Newtonsoft.Json.Linq;` 追加）。`Debug.Log(save)` のように文字列を期待していた行は `Debug.Log(JsonConvert.SerializeObject(save))` に直す。

- [ ] **Step 6: 残存確認・コンパイル・テスト**

Run:
```bash
grep -rn "JsonConvert.SerializeObject\|JsonUtility.ToJson\|JsonUtility.FromJson" moorestech_server/Assets/Scripts/Game.Block/Blocks moorestech_server/Assets/Scripts/Game.Block/Factory
grep -rn "Dictionary<string, string>" moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Game.Block.Interface moorestech_server/Assets/Scripts/Game.World.Interface/DataStore/BlockJsonObject.cs
```
Expected: どちらも0件

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.UnitTest\.Game\.SaveLoad\..*|^Tests\.CombinedTest\.Game\..*SaveLoadTest$|^Tests\.CombinedTest\.Core\.(CleanRoom|Transport|Machine|Fluid)\..*" --timeout-seconds 1200`
Expected: 全PASS（`BlockComponentStateReaderTest` 4件を含む）

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface moorestech_server/Assets/Scripts/Game.World.Interface/DataStore/BlockJsonObject.cs moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Tests
git commit -m "refactor(server): ブロックのセーブ状態を文字列からオブジェクトへ改めJSON化を取り込みから分離可能にする"
```

---

### Task 4: セーブへ `currentTick`・`randomState` を追加し、取り込み（Capture）と直列化（Serialize）を分ける

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs:75-102`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`（`Load` 先頭）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`（`references` に `"Core.Update"` を追加）
- Create: `scripts/save_migration/migrate_block_state_objects.py`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveTickAndRandomStateTest.cs`（新規）

**Interfaces:**
- Consumes: Task 1 `GameRandom`／`GameUpdater.RestoreCurrentTick`、Task 3 のオブジェクト状態
- Produces:
  - `WorldSaveAllInfoV1`: コンストラクタ末尾に `ulong currentTick, ulong[] randomState` を追加。`[JsonProperty("currentTick")] public ulong CurrentTick { get; }`、`[JsonProperty("randomState")] public ulong[] RandomState { get; }`
  - `public WorldSaveAllInfoV1 AssembleSaveJsonText.Capture()`（tickスレッド専用）
  - `public static string AssembleSaveJsonText.Serialize(WorldSaveAllInfoV1 data)`（任意スレッド）
  - `public string AssembleSaveJsonText.AssembleSaveJson()` は `Serialize(Capture())` に等しい（既存呼び出し互換）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Game/SaveTickAndRandomStateTest.cs`:

```csharp
using Core.Update;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class SaveTickAndRandomStateTest
    {
        [Test]
        public void tickと乱数状態がセーブロードで一致する()
        {
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            GameRandom.Reseed(31337UL);
            GameRandom.NextUlong();
            for (var i = 0; i < 17; i++) GameUpdater.UpdateOneTick();
            var savedTick = GameUpdater.CurrentTick;
            var savedState = GameRandom.ExportState();
            var json = saveProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

            // 別コンテナで状態を乱してからロードし、復元されることを見る
            // Disturb the state in a fresh container, then load and observe restoration
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            GameRandom.Reseed(1UL);
            GameUpdater.UpdateOneTick();
            (loadProvider.GetRequiredService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);

            Assert.AreEqual(savedTick, GameUpdater.CurrentTick, "currentTick が復元されていない");
            CollectionAssert.AreEqual(savedState, GameRandom.ExportState(), "randomState が復元されていない");
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop launch ./moorestech_client --restart` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Game\.SaveTickAndRandomStateTest$"`
Expected: FAIL（tick が復元されない）

- [ ] **Step 3: `WorldSaveAllInfoV1` にフィールドを追加する**

コンストラクタ末尾の引数 `List<CleanRoomSaveData> cleanRoomRooms` の後に `ulong currentTick, ulong[] randomState` を追加し、本体で `CurrentTick = currentTick; RandomState = randomState;`。プロパティ群の末尾に:
```csharp
        // スナップショットからの再生に必要な時刻と乱数状態。ロードの先頭で復元する
        // Tick and random state required to replay from a snapshot; restored first on load
        [JsonProperty("currentTick")] public ulong CurrentTick { get; }
        [JsonProperty("randomState")] public ulong[] RandomState { get; }
```

- [ ] **Step 4: `AssembleSaveJsonText` を分割する**

`AssembleSaveJson()` を次の3メソッドに置き換える（DataStore の並びは既存のまま）:
```csharp
        public string AssembleSaveJson()
        {
            return Serialize(Capture());
        }

        // tickスレッドで世界の保存像を取り込む。ここで返す木は生きた参照を含まない
        // Capture the world's save image on the tick thread; the returned tree holds no live references
        public WorldSaveAllInfoV1 Capture()
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var mapObjectDatastore = ServerContext.MapObjectDatastore;

            return new WorldSaveAllInfoV1(
                worldBlockDatastore.GetSaveJsonObject(),
                _inventoryDataStore.GetSaveJsonObject(),
                _entitiesDatastore.GetSaveJsonObject(),
                _worldSettingsDatastore.GetSaveJsonObject(),
                mapObjectDatastore.GetSaveJsonObject(),
                _challengeDatastore.GetSaveJsonObject(),
                _gameUnlockStateDataController.GetSaveJsonObject(),
                _researchDataStore.GetSaveJsonObject(),
                _trainSaveLoadService.GetSaveJsonObject(),
                _railGraphSaveLoadService.GetSaveData(),
                _playerRidingDatastore.GetSaveData(),
                _blueprintDatastore.GetSaveJsonObject(),
                _hotbarAssignmentDatastore.GetSaveJsonObject(),
                _remainingPlacementCountDataStore.GetSaveJsonObject(),
                _constructionPayerDataStore.GetSaveJsonObject(),
                _itemStackLevelDataStore.GetSaveJsonObject(),
                _playerInventorySlotLevelDataStore.GetSaveLevel(),
                _cleanRoomDatastore.GetSaveData(),
                GameUpdater.CurrentTick,
                GameRandom.ExportState()
            );
        }

        // JSON化はどのスレッドでもよい（取り込んだ木だけを読む）
        // Serialization may run on any thread; it reads only the captured tree
        public static string Serialize(WorldSaveAllInfoV1 data)
        {
            return JsonConvert.SerializeObject(data);
        }
```
（`using Core.Update;` を追加）

- [ ] **Step 5: ローダーで先頭復元する**

`WorldLoaderFromJson.Load` の `var load = JsonConvert.DeserializeObject<WorldSaveAllInfoV1>(jsonText);` の直後に:
```csharp
            // 時刻と乱数状態を最初に戻す。以降の復元（残りtick等）がこの時刻を基準にする
            // Restore the clock and random state first; later restorations reference this tick
            GameUpdater.RestoreCurrentTick(load.CurrentTick);
            GameRandom.RestoreState(load.RandomState);
```
（`using Core.Update;` を追加。`Game.SaveLoad.asmdef` の `references` に `"Core.Update"` を追加）

- [ ] **Step 6: テストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Game\.SaveTickAndRandomStateTest$|^Tests\.CombinedTest\.Game\.SaveJsonFileTest$"`
Expected: PASS

- [ ] **Step 7: 既存セーブの移行スクリプトを書く**

`scripts/save_migration/migrate_block_state_objects.py`:

```python
#!/usr/bin/env python3
"""ブロックstateの文字列をオブジェクトへ展開し currentTick / randomState を付与する。

使い方: python3 migrate_block_state_objects.py <save.json> --seed <world.jsonのseed>
元ファイルは <save.json>.pre-state-objects.bak として残す。
"""
import argparse
import json
import shutil
import sys

MASK = (1 << 64) - 1


def splitmix64(x):
    # C# の GameRandom.Reseed と同じ手順（変更したら両方を直す）
    x = (x + 0x9E3779B97F4A7C15) & MASK
    z = x
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK
    return x, z ^ (z >> 31)


def random_state_from_seed(seed):
    x = seed & MASK
    state = []
    for _ in range(4):
        x, word = splitmix64(x)
        state.append(word)
    return state


def migrate(save, seed):
    converted = 0
    for block in save["world"]:
        state = block.get("state") or {}
        for key, value in list(state.items()):
            if isinstance(value, str):
                state[key] = json.loads(value)
                converted += 1
        block["state"] = state
    if "currentTick" not in save:
        save["currentTick"] = 0
    if "randomState" not in save:
        save["randomState"] = random_state_from_seed(seed)
    return converted


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("save_path")
    parser.add_argument("--seed", type=int, required=True)
    args = parser.parse_args()

    with open(args.save_path, encoding="utf-8") as f:
        save = json.load(f)
    backup = args.save_path + ".pre-state-objects.bak"
    shutil.copyfile(args.save_path, backup)
    converted = migrate(save, args.seed)
    with open(args.save_path, "w", encoding="utf-8") as f:
        json.dump(save, f, ensure_ascii=False)
    print(f"converted state values: {converted}, backup: {backup}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 8: world_1 の複製で移行と実ロードを確認する**

Run:
```bash
W=moorestech_client/PlaytestResults/worlds/world_1-migrated
rm -rf "$W" && mkdir -p "$W" && cp ~/Library/Application\ Support/moorestech/Saves/world_1/{save.json,world.json,map.json} "$W/"
python3 scripts/save_migration/migrate_block_state_objects.py "$W/save.json" --seed 0
python3 -c "import json;d=json.load(open('$W/save.json'));print(d['currentTick'],len(d['randomState']),type(d['world'][0]['state']))"
```
Expected: `0 4 <class 'dict'>`（`world.json` の `seed` は 0）

次に `moorestech-save-migration` スキルの `references/load_test.cs` を `uloop execute-dynamic-code --code-file` で実行し（セーブパスを `$W/save.json`、サーバーディレクトリを `/Users/katsumi/moorestech_master/server_v8` に書き換える）、`LOAD OK | blocks=8048` を確認する。
Expected: `LOAD OK`

- [ ] **Step 9: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveTickAndRandomStateTest.cs scripts/save_migration/migrate_block_state_objects.py
git commit -m "feat(server): セーブにcurrentTickとrandomStateを追加し取り込みと直列化を分離・旧セーブ移行スクリプト"
```

---

### Task 5: `SaveWriteWorker`（別スレッド書き出し）と `WorldSaveCoordinator` の非同期化

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Writer/SaveWriteKind.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Writer/SaveWriteJob.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Writer/SaveWriteCompletion.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Writer/SaveWriteWorker.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/WorldSaveCoordinator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs:251`（`services.AddSingleton<SaveWriteWorker>();` を `WorldSaveCoordinator` 登録の直前に追加）
- Modify: テスト `Tests/UnitTest/Game/SaveLoad/WorldSaveCoordinatorTest.cs`・`Tests/CombinedTest/Game/SaveJsonFileTest.cs`・`Tests/CombinedTest/Server/PacketTest/TickEndSaveConsistencyTest.cs`（書き出し待ちを挟む）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveCaptureDetachedTest.cs`（新規）

**Interfaces:**
- Consumes: Task 4 `AssembleSaveJsonText.Capture()`／`Serialize()`
- Produces:
  - `public enum Game.SaveLoad.Writer.SaveWriteKind { PlayerSave = 0, Snapshot = 1 }`
  - `public sealed class SaveWriteJob { public long Generation { get; } public SaveWriteKind Kind { get; } public WorldSaveAllInfoV1 Data { get; } public string TargetPath { get; } public bool KeepBackup { get; } public SaveWriteJob(long generation, SaveWriteKind kind, WorldSaveAllInfoV1 data, string targetPath, bool keepBackup) }`
  - `public sealed class SaveWriteCompletion { public long Generation { get; } public SaveWriteKind Kind { get; } public ulong Tick { get; } public string TargetPath { get; } public bool Success { get; } }`
  - `public sealed class SaveWriteWorker { public void Enqueue(SaveWriteJob job); public bool TryDequeueCompletion(SaveWriteKind kind, out SaveWriteCompletion completion); public bool HasInFlight { get; } public void WaitForIdle(); }`
  - `WorldSaveCoordinator`: `RequestSave()`／`SaveIfRequested()`／`HasPendingSave`／`OnWorldSaveCompleted` は既存どおり。追加 `public void WaitForPendingWrites()`（書き出し完了まで待って完了通知を排出する。テスト・終了時用）

- [ ] **Step 1: 「取り込み後の変更が書き出しに混ざらない」失敗するテストを書く**

`Tests/CombinedTest/Game/SaveCaptureDetachedTest.cs`:

```csharp
using System;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Game
{
    // 取り込んだ保存像が生きた参照を含んでいれば、取り込み後の世界変更が後からのJSON化に混ざる
    // If a captured image held live references, world changes after capture would leak into a later serialization
    public class SaveCaptureDetachedTest
    {
        [Test]
        public void 取り込み後に世界を変えても直列化結果は変わらない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(3, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(6, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            SetItem(GetInventory(serviceProvider), 0, MasterHolder.ItemMaster.GetItemMaster(new ItemId(1)).ItemGuid, 5);

            var assembler = serviceProvider.GetRequiredService<AssembleSaveJsonText>();
            var captured = assembler.Capture();
            var immediately = AssembleSaveJsonText.Serialize(captured);

            // 取り込み後にあらゆる種類の変更を加える
            // Apply every kind of mutation after the capture
            world.RemoveBlock(new Vector3Int(0, 0));
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(9, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);
            SetItem(GetInventory(serviceProvider), 0, MasterHolder.ItemMaster.GetItemMaster(new ItemId(2)).ItemGuid, 9);
            for (var i = 0; i < 20; i++) Core.Update.GameUpdater.UpdateOneTick();

            var later = AssembleSaveJsonText.Serialize(captured);
            Assert.AreEqual(immediately, later, "取り込んだ保存像が後の世界変更で書き換わった（生きた参照が残っている）");
        }
    }
}
```
（`world.RemoveBlock` のシグネチャは `IWorldBlockDatastore` を確認し、無ければ `Tests/CombinedTest/Server/PacketTest/` の撤去テストが使うAPIに合わせる）

- [ ] **Step 2: 実行して結果を記録する**

Run: `uloop launch ./moorestech_client --restart` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Game\.SaveCaptureDetachedTest$"`
Expected: Task 3 で生きた参照を残したコンポーネントがあれば FAIL。FAIL したら差分に現れたDataStore／コンポーネントの `GetSaveState`・`GetSaveJsonObject` でコレクションをコピーして PASS にする（Task 3 の規則(a)の見落とし修正）。

- [ ] **Step 3: `Writer/` の4ファイルを実装する**

`SaveWriteKind.cs`:
```csharp
namespace Game.SaveLoad.Writer
{
    public enum SaveWriteKind
    {
        PlayerSave = 0,
        Snapshot = 1,
    }
}
```

`SaveWriteJob.cs`:
```csharp
using Game.SaveLoad.Json.WorldVersions;

namespace Game.SaveLoad.Writer
{
    // tickスレッドで取り込んだ保存像と書き出し先。JSON化と書き込みは書き出しスレッドが行う
    // A save image captured on the tick thread plus its destination; the writer thread serializes and writes it
    public sealed class SaveWriteJob
    {
        public long Generation { get; }
        public SaveWriteKind Kind { get; }
        public WorldSaveAllInfoV1 Data { get; }
        public string TargetPath { get; }
        public bool KeepBackup { get; }

        public SaveWriteJob(long generation, SaveWriteKind kind, WorldSaveAllInfoV1 data, string targetPath, bool keepBackup)
        {
            Generation = generation;
            Kind = kind;
            Data = data;
            TargetPath = targetPath;
            KeepBackup = keepBackup;
        }
    }
}
```

`SaveWriteCompletion.cs`:
```csharp
namespace Game.SaveLoad.Writer
{
    public sealed class SaveWriteCompletion
    {
        public long Generation { get; }
        public SaveWriteKind Kind { get; }
        public ulong Tick { get; }
        public string TargetPath { get; }
        public bool Success { get; }

        public SaveWriteCompletion(long generation, SaveWriteKind kind, ulong tick, string targetPath, bool success)
        {
            Generation = generation;
            Kind = kind;
            Tick = tick;
            TargetPath = targetPath;
            Success = success;
        }
    }
}
```

`SaveWriteWorker.cs`:
```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Game.SaveLoad.Json;
using UnityEngine;

namespace Game.SaveLoad.Writer
{
    // 保存像のJSON化と書き込みをtickスレッドの外で直列に行う。完了は種別ごとのキューで返し、消費側がtickスレッドで排出する
    // Serializes and writes save images off the tick thread in order; completions return via per-kind queues drained on the tick thread
    public sealed class SaveWriteWorker
    {
        private readonly BlockingCollection<SaveWriteJob> _jobs = new();
        private readonly ConcurrentQueue<SaveWriteCompletion> _playerSaveCompletions = new();
        private readonly ConcurrentQueue<SaveWriteCompletion> _snapshotCompletions = new();
        private int _inFlight;

        public SaveWriteWorker()
        {
            var thread = new Thread(Run) { Name = "[moorestech] セーブ書き出しスレッド", IsBackground = true };
            thread.Start();
        }

        public bool HasInFlight => Volatile.Read(ref _inFlight) != 0;

        public void Enqueue(SaveWriteJob job)
        {
            Interlocked.Increment(ref _inFlight);
            _jobs.Add(job);
        }

        public bool TryDequeueCompletion(SaveWriteKind kind, out SaveWriteCompletion completion)
        {
            var queue = kind == SaveWriteKind.PlayerSave ? _playerSaveCompletions : _snapshotCompletions;
            return queue.TryDequeue(out completion);
        }

        // テストと終了時の待ち合わせ専用。tickスレッドからは呼ばない
        // For tests and shutdown only; never call from the tick thread
        public void WaitForIdle()
        {
            while (HasInFlight) Thread.Sleep(1);
        }

        private void Run()
        {
            foreach (var job in _jobs.GetConsumingEnumerable())
            {
                var success = Write(job);
                var queue = job.Kind == SaveWriteKind.PlayerSave ? _playerSaveCompletions : _snapshotCompletions;
                queue.Enqueue(new SaveWriteCompletion(job.Generation, job.Kind, job.Data.CurrentTick, job.TargetPath, success));
                Interlocked.Decrement(ref _inFlight);
            }
        }

        private static bool Write(SaveWriteJob job)
        {
            var json = AssembleSaveJsonText.Serialize(job.Data);
            var tmpPath = job.TargetPath + ".tmp";

            // ディスク書き込みは外部境界。失敗は完了通知に載せ、要求側が未消化のまま次回へ持ち越す
            // Disk I/O is an external boundary; a failure rides the completion so the requester keeps it pending for retry
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(job.TargetPath));
                File.WriteAllText(tmpPath, json);
                if (job.KeepBackup && File.Exists(job.TargetPath))
                {
                    File.Replace(tmpPath, job.TargetPath, job.TargetPath + ".bak");
                }
                else
                {
                    // .NET Standard 2.1 には上書き付き Move が無いので削除してから移動する
                    // .NET Standard 2.1 lacks an overwriting Move, so delete then move
                    if (File.Exists(job.TargetPath)) File.Delete(job.TargetPath);
                    File.Move(tmpPath, job.TargetPath);
                }
                return true;
            }
            catch (IOException e)
            {
                Debug.LogError($"セーブの書き出しに失敗しました path:{job.TargetPath} kind:{job.Kind} generation:{job.Generation} message:{e.Message}");
                return false;
            }
        }
    }
}
```

- [ ] **Step 4: `WorldSaveCoordinator` を書き換える**

```csharp
using System;
using System.Threading;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Writer;
using UniRx;

namespace Game.SaveLoad
{
    public sealed class WorldSaveCoordinator : IWorldSaveRequest, IWorldSaveCompletionNotifier
    {
        private readonly AssembleSaveJsonText _assembleSaveJsonText;
        private readonly WorldDataDirectory _worldDataDirectory;
        private readonly SaveWriteWorker _saveWriteWorker;
        private readonly Subject<long> _onWorldSaveCompleted = new();
        private long _requestedGeneration;
        private long _completedGeneration;
        private long _enqueuedGeneration;

        public WorldSaveCoordinator(WorldDataDirectory worldDataDirectory, AssembleSaveJsonText assembleSaveJsonText, SaveWriteWorker saveWriteWorker)
        {
            _worldDataDirectory = worldDataDirectory;
            _assembleSaveJsonText = assembleSaveJsonText;
            _saveWriteWorker = saveWriteWorker;
        }

        // 要求済みだがまだ書き出しが完了していない保存が残っているか。終了時の待ち合わせに使う
        // Whether a requested save has not finished writing; used to wait for the flush at shutdown
        public bool HasPendingSave => Volatile.Read(ref _requestedGeneration) != Volatile.Read(ref _completedGeneration);

        // 書き出しが完了した要求番号を流す。tickスレッド上で発火する
        // Emits the generation whose write completed; fired on the tick thread
        public IObservable<long> OnWorldSaveCompleted => _onWorldSaveCompleted;

        public long RequestSave()
        {
            return Interlocked.Increment(ref _requestedGeneration);
        }

        // tick末尾で呼ぶ。完了通知を排出し、未投入の要求があれば取り込んで書き出しを投入する
        // Called at tick end: drain completions, then capture and enqueue a write for any un-enqueued request
        public void SaveIfRequested()
        {
            DrainCompletions();

            var targetGeneration = Volatile.Read(ref _requestedGeneration);
            if (targetGeneration == Volatile.Read(ref _completedGeneration)) return;
            if (targetGeneration == _enqueuedGeneration) return;

            _enqueuedGeneration = targetGeneration;
            var data = _assembleSaveJsonText.Capture();
            _saveWriteWorker.Enqueue(new SaveWriteJob(targetGeneration, SaveWriteKind.PlayerSave, data, _worldDataDirectory.SaveJsonFilePath, true));
        }

        // テストと終了時用。書き出しスレッドが空くまで待ち、完了通知をこのスレッドで排出する
        // For tests and shutdown: wait until the writer is idle, then drain completions on this thread
        public void WaitForPendingWrites()
        {
            _saveWriteWorker.WaitForIdle();
            DrainCompletions();
        }

        private void DrainCompletions()
        {
            while (_saveWriteWorker.TryDequeueCompletion(SaveWriteKind.PlayerSave, out var completion))
            {
                if (!completion.Success)
                {
                    // 失敗した要求は未投入へ戻し、次のtick末尾で再取り込みする
                    // Return a failed request to the un-enqueued state so the next tick end recaptures it
                    _enqueuedGeneration = Volatile.Read(ref _completedGeneration);
                    continue;
                }
                Volatile.Write(ref _completedGeneration, completion.Generation);
                UnityEngine.Debug.Log("ワールドを保存しました");
                _onWorldSaveCompleted.OnNext(completion.Generation);
            }
        }
    }
}
```

- [ ] **Step 5: DI 登録とテストの待ち合わせを追加する**

`MoorestechServerDIContainerGenerator.cs` の `services.AddSingleton<WorldSaveCoordinator>();` の直前に `services.AddSingleton<SaveWriteWorker>();`（`using Game.SaveLoad.Writer;`）。

`WorldSaveCoordinatorTest.cs` を次に置き換える:
```csharp
using Game.Paths;
using System;
using System.IO;
using Game.SaveLoad;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class WorldSaveCoordinatorTest
    {
        [Test]
        public void 複数の保存要求を一回の保存へまとめる()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-coordinator-{Guid.NewGuid():N}.json");
            var coordinator = CreateCoordinator(savePath);

            coordinator.RequestSave();
            coordinator.RequestSave();
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsTrue(File.Exists(savePath));
            Assert.IsFalse(coordinator.HasPendingSave);

            // 消化済み要求で再保存されないことをファイルが再生成されないことで観測する
            // Verify consumed requests trigger no re-save by checking the file is not recreated
            File.Delete(savePath);
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsFalse(File.Exists(savePath));
        }

        [Test]
        public void 書き出しに失敗した要求は次回に再実行する()
        {
            // 保存先ディレクトリの位置にファイルを置き、ディレクトリ作成を失敗させる
            // Put a file where the save directory should be so directory creation fails
            var saveDirectory = Path.Combine(Path.GetTempPath(), $"moorestech-coordinator-{Guid.NewGuid():N}");
            File.WriteAllText(saveDirectory, "blocker");
            var savePath = Path.Combine(saveDirectory, "save.json");
            var coordinator = CreateCoordinator(savePath);

            coordinator.RequestSave();
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsTrue(coordinator.HasPendingSave, "失敗した書き出しが完了扱いになっている");

            File.Delete(saveDirectory);
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsTrue(File.Exists(savePath));
            Assert.IsFalse(coordinator.HasPendingSave);
            Directory.Delete(saveDirectory, true);
        }

        private static WorldSaveCoordinator CreateCoordinator(string savePath)
        {
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            return provider.GetRequiredService<WorldSaveCoordinator>();
        }
    }
}
```

`SaveJsonFileTest.cs` の `GameUpdater.UpdateOneTick();` の直後と、`TickEndSaveConsistencyTest.cs` の `GameUpdater.UpdateOneTick();` の直後に、それぞれ `saveServiceProvider.GetRequiredService<WorldSaveCoordinator>().WaitForPendingWrites();`／`saveProvider.GetRequiredService<WorldSaveCoordinator>().WaitForPendingWrites();` を追加する（`using Game.SaveLoad;`）。他に `RequestSave()`＋tick でファイル存在を見るテストが無いか `grep -rn "RequestSave()" moorestech_server/Assets/Scripts/Tests` で確認し、同型があれば同じ待ちを入れる。

- [ ] **Step 6: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.UnitTest\.Game\.SaveLoad\.WorldSaveCoordinatorTest$|^Tests\.CombinedTest\.Game\.(SaveJsonFileTest|SaveCaptureDetachedTest|SaveTickAndRandomStateTest)$|^Tests\.CombinedTest\.Server\.PacketTest\.TickEndSaveConsistencyTest$"`
Expected: 全PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests
git commit -m "feat(server): セーブの書き出しを専用スレッドへ分離しtick停止を取り込みだけにする"
```

---

### Task 6: スナップショットリングとパケットログ（常時記録）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Paths/WorldDataDirectory.cs`（`SnapshotDirectory`・`SnapshotFilePath(ulong)`・`PacketLogSegmentFilePath(ulong)` を追加）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/SnapshotRingConfig.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad.Interface/SnapshotWritten.cs`・`moorestech_server/Assets/Scripts/Game.SaveLoad.Interface/ISnapshotCaptureRequest.cs`・`moorestech_server/Assets/Scripts/Game.SaveLoad.Interface/ISnapshotWrittenNotifier.cs`（プロトコル・イベント側は `Game.SaveLoad.Interface` だけを見る。`SaveProtocol`＋`IWorldSaveRequest`／`WorldSaveCompletedEventPacket`＋`IWorldSaveCompletionNotifier` と同型）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/WorldSnapshotRing.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/ReceivedPacketLog.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/ReceivedPacketRecord.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/ReceivedPacketLogReader.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Loop/PacketProcessing/ReceiveQueueProcessor.cs`（ctor に `ReceivedPacketLog` を追加し `ProcessPacket` 先頭で `Append`）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Loop/ServerListenAcceptor.cs`（`StartServer` に `ReceivedPacketLog receivedPacketLog` 引数を追加して `ReceiveQueueProcessor` へ渡す）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs`（`StartServer` 呼び出しに渡す。`settings.CaptureRing` なら `WorldSnapshotRing.Start`）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Args/StartServerSettings.cs`（`CaptureRing` を追加）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（`ReceivedPacketLog`・`WorldSnapshotRing` の登録と `FinalTickEndUpdates` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestWorldBootSession.cs:41-45`・`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Util/EditModeInPlayingTestUtil.cs:78-84`・`moorestech_client/Assets/Scripts/Client.Starter/Editor/SkipSaveLoadPlayModeSettings.cs:22`・`moorestech_client/Assets/Scripts/Client.Starter/StandaloneQa/StandaloneTerrainQaSettings.cs:69-76`（`AutoSave = false` の隣に `CaptureRing = false`）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Snapshot/WorldSnapshotRingTest.cs`・`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/ReceivedPacketLogTest.cs`（新規）

**Interfaces:**
- Consumes: Task 4 `Capture()`、Task 5 `SaveWriteWorker`／`SaveWriteJob`／`SaveWriteKind.Snapshot`
- Produces:
  - `WorldDataDirectory`: `public string SnapshotDirectory { get; }`（`Path.Combine(Path.GetDirectoryName(SaveJsonFilePath), "snapshots")`）、`public string SnapshotFilePath(ulong tick)`（`tick_<tick>.json`）、`public string PacketLogSegmentFilePath(ulong fromTick)`（`packets_<fromTick>.bin`）
  - `public static class SnapshotRingConfig { public const uint PeriodTicks = 600; public const int Generations = 4; }`
  - `Game.SaveLoad.Interface`: `public sealed class SnapshotWritten { public long RequestId { get; } public ulong Tick { get; } public string FilePath { get; } }`（`RequestId` は周期スナップショットなら 0）、`public interface ISnapshotCaptureRequest { long RequestImmediateSnapshot(); }`、`public interface ISnapshotWrittenNotifier { IObservable<SnapshotWritten> OnSnapshotWritten { get; } }`
  - `public sealed class WorldSnapshotRing : ISnapshotCaptureRequest, ISnapshotWrittenNotifier { public bool IsActive { get; } public IObservable<SnapshotWritten> OnSnapshotWritten { get; } public IReadOnlyList<ulong> WrittenTicks { get; } public void Start(uint periodTicks, int generations); public long RequestImmediateSnapshot(); public void Update(); public void WaitForPendingWrites(); }`（DIで `ISnapshotCaptureRequest`／`ISnapshotWrittenNotifier` を `WorldSnapshotRing` へ転送登録）
  - `public sealed class ReceivedPacketLog { public bool IsActive { get; } public void Start(string directory, ulong fromTick); public void Append(ulong tick, byte[] payload); public void Flush(); public void Rotate(ulong fromTick); public void DeleteSegmentsBefore(ulong oldestSnapshotTick); public IReadOnlyList<string> SegmentFilePaths(); public static bool TryParseSegmentFromTick(string fileName, out ulong fromTick); }`
  - `public readonly struct ReceivedPacketRecord { public ulong Tick { get; } public byte[] Payload { get; } }`
  - `public static class ReceivedPacketLogReader { public static List<ReceivedPacketRecord> ReadAll(IEnumerable<string> segmentFilePaths); }`（tick 昇順・同tick内はファイル順）
  - `StartServerSettings.CaptureRing`（`--captureRing`、既定 `true`）
  - `ReceiveQueueProcessor(PacketResponseCreator, SendQueueProcessor, PacketResponseContext, TickEndPacketQueue, ReceivedPacketLog)`
  - `ServerListenAcceptor.StartServer(Socket, PacketResponseCreator, PlayerConnectionRegistry, EventProtocolProvider, TickEndPacketQueue, ReceivedPacketLog, CancellationToken)`

- [ ] **Step 1: 失敗するテストを書く（パケットログ）**

`Tests/UnitTest/Game/SaveLoad/ReceivedPacketLogTest.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using Game.SaveLoad.Snapshot;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ReceivedPacketLogTest
    {
        [Test]
        public void 追記したレコードをtick付きで読み戻せる()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Append(1, new byte[] { 1, 2, 3 });
            log.Append(3, new byte[] { 9 });
            log.Rotate(4);
            log.Append(4, new byte[] { 4, 4 });
            log.Flush();

            var records = ReceivedPacketLogReader.ReadAll(log.SegmentFilePaths());
            Assert.AreEqual(3, records.Count);
            Assert.AreEqual(1UL, records[0].Tick);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, records[0].Payload);
            Assert.AreEqual(3UL, records[1].Tick);
            Assert.AreEqual(4UL, records[2].Tick);
            Directory.Delete(dir, true);
        }

        [Test]
        public void 最古スナップショット以前の区間だけ削除される()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-packetlog-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Rotate(11);
            log.Rotate(21);
            log.Rotate(31);
            log.DeleteSegmentsBefore(20);

            var names = log.SegmentFilePaths().Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "packets_21.bin", "packets_31.bin" }, names);
            Directory.Delete(dir, true);
        }

        [Test]
        public void 開始前のAppendは無視される()
        {
            var log = new ReceivedPacketLog();
            log.Append(1, new byte[] { 1 });
            log.Append(2, new byte[] { 1 });
            Assert.IsFalse(log.IsActive);
        }
    }
}
```

- [ ] **Step 2: 失敗するテストを書く（リング）**

`Tests/CombinedTest/Game/Snapshot/WorldSnapshotRingTest.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game.Snapshot
{
    public class WorldSnapshotRingTest
    {
        [Test]
        public void 周期ごとに書き世代数を超えた古い世代を消す()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-ring-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var directory = provider.GetRequiredService<WorldDataDirectory>();
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(0);
            ring.Start(10, 3);

            for (var i = 0; i < 40; i++) GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();

            var names = Directory.GetFiles(directory.SnapshotDirectory, "tick_*.json").Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "tick_20.json", "tick_30.json", "tick_40.json" }, names);
            CollectionAssert.AreEqual(new ulong[] { 20, 30, 40 }, ring.WrittenTicks);

            // 最古スナップショット20より前の区間は消え、21以降の区間が残る
            // Segments before the oldest snapshot (20) are gone; segments from 21 remain
            var segments = Directory.GetFiles(directory.SnapshotDirectory, "packets_*.bin").Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "packets_21.bin", "packets_31.bin", "packets_41.bin" }, segments);
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }

        [Test]
        public void 即時要求は同じtickの末尾で書かれ要求IDが通知される()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-ring-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(100);
            ring.Start(600, 4);

            SnapshotWritten written = null;
            ring.OnSnapshotWritten.Subscribe(w => written = w);
            var requestId = ring.RequestImmediateSnapshot();
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();

            Assert.IsNotNull(written, "完了通知が来ていない");
            Assert.AreEqual(requestId, written.RequestId);
            Assert.AreEqual(101UL, written.Tick);
            Assert.IsTrue(File.Exists(written.FilePath));
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }

        [Test]
        public void 未開始のリングは即時要求を無視して0を返す()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            Assert.IsFalse(ring.IsActive);
            Assert.AreEqual(0L, ring.RequestImmediateSnapshot());
        }
    }
}
```

- [ ] **Step 3: Unity再起動後に実行して失敗を確認する**

Run: `uloop launch ./moorestech_client --restart` → `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー（型が無い）

- [ ] **Step 4: `WorldDataDirectory` にスナップショット配置を足す**

プロパティに `public string SnapshotDirectory { get; }` を追加し、private ctor で `SnapshotDirectory = saveJsonFilePath == null ? null : Path.Combine(Path.GetDirectoryName(saveJsonFilePath), "snapshots");` を設定。メソッドを追加:
```csharp
        // スナップショットとパケットログはセーブファイルの隣の snapshots/ に置く。ファイル名規則の定義はここだけ
        // Snapshots and packet logs live in snapshots/ beside the save file; the naming rule lives only here
        public string SnapshotFilePath(ulong tick)
        {
            return Path.Combine(SnapshotDirectory, $"tick_{tick}.json");
        }

        public string PacketLogSegmentFilePath(ulong fromTick)
        {
            return Path.Combine(SnapshotDirectory, ReceivedPacketLogFileName(fromTick));
        }

        public static string ReceivedPacketLogFileName(ulong fromTick)
        {
            return $"packets_{fromTick}.bin";
        }
```

- [ ] **Step 5: `Snapshot/` の6ファイルを実装する**

`SnapshotRingConfig.cs`:
```csharp
namespace Game.SaveLoad.Snapshot
{
    // 30秒周期・直近2分（ADR 0057）。変えるときはここだけ
    // 30-second period, last two minutes (ADR 0057); change only here
    public static class SnapshotRingConfig
    {
        public const uint PeriodTicks = 600;
        public const int Generations = 4;
    }
}
```

`Game.SaveLoad.Interface/SnapshotWritten.cs`:
```csharp
namespace Game.SaveLoad.Interface
{
    public sealed class SnapshotWritten
    {
        public long RequestId { get; }
        public ulong Tick { get; }
        public string FilePath { get; }

        public SnapshotWritten(long requestId, ulong tick, string filePath)
        {
            RequestId = requestId;
            Tick = tick;
            FilePath = filePath;
        }
    }
}
```

`Game.SaveLoad.Interface/ISnapshotCaptureRequest.cs`:
```csharp
namespace Game.SaveLoad.Interface
{
    // 即時スナップショットを要求し要求IDを返す。完了は ISnapshotWrittenNotifier で突き合わせる
    // Requests an immediate snapshot and returns its id; completion is matched via ISnapshotWrittenNotifier
    public interface ISnapshotCaptureRequest
    {
        long RequestImmediateSnapshot();
    }
}
```

`Game.SaveLoad.Interface/ISnapshotWrittenNotifier.cs`:
```csharp
using System;

namespace Game.SaveLoad.Interface
{
    public interface ISnapshotWrittenNotifier
    {
        IObservable<SnapshotWritten> OnSnapshotWritten { get; }
    }
}
```

`ReceivedPacketRecord.cs`:
```csharp
namespace Game.SaveLoad.Snapshot
{
    public readonly struct ReceivedPacketRecord
    {
        public ulong Tick { get; }
        public byte[] Payload { get; }

        public ReceivedPacketRecord(ulong tick, byte[] payload)
        {
            Tick = tick;
            Payload = payload;
        }
    }
}
```

`ReceivedPacketLog.cs`:
```csharp
using System.Collections.Generic;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Game.SaveLoad.Snapshot
{
    // 受信パケットを処理tick付きで区間ファイルへ追記する。区間はスナップショットごとに切り替わる
    // Appends received packets with their processing tick to segment files; a new segment starts at each snapshot
    public sealed class ReceivedPacketLog
    {
        private readonly object _lock = new();
        private string _directory;
        private BinaryWriter _writer;
        private ulong _currentSegmentFromTick;
        private bool _inactiveLogged;

        public bool IsActive { get; private set; }

        public void Start(string directory, ulong fromTick)
        {
            _directory = directory;
            Directory.CreateDirectory(directory);
            Rotate(fromTick);
            IsActive = true;
        }

        public void Append(ulong tick, byte[] payload)
        {
            if (!IsActive)
            {
                // 無効は正常運用（テスト・プレイテスト）なので理由は1回だけ出す
                // Being disabled is normal (tests, playtests), so log the reason only once
                if (!_inactiveLogged) Debug.Log("パケットログは未開始のため記録しません（常時記録が無効）");
                _inactiveLogged = true;
                return;
            }
            lock (_lock)
            {
                _writer.Write(tick);
                _writer.Write(payload.Length);
                _writer.Write(payload);
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                _writer?.Flush();
            }
        }

        public void Rotate(ulong fromTick)
        {
            lock (_lock)
            {
                _writer?.Flush();
                _writer?.Dispose();
                var path = Path.Combine(_directory, WorldDataDirectory.ReceivedPacketLogFileName(fromTick));
                _writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
                _currentSegmentFromTick = fromTick;
            }
        }

        // 最古スナップショット以前で終わる区間を消す。書き込み中の区間は残す
        // Delete segments that end at or before the oldest snapshot; keep the segment being written
        public void DeleteSegmentsBefore(ulong oldestSnapshotTick)
        {
            foreach (var path in SegmentFilePaths())
            {
                if (!TryParseSegmentFromTick(Path.GetFileName(path), out var fromTick)) continue;
                if (fromTick > oldestSnapshotTick || fromTick == _currentSegmentFromTick) continue;
                File.Delete(path);
            }
        }

        public IReadOnlyList<string> SegmentFilePaths()
        {
            var result = new List<string>();
            if (_directory == null || !Directory.Exists(_directory)) return result;
            foreach (var path in Directory.GetFiles(_directory, "packets_*.bin"))
            {
                if (TryParseSegmentFromTick(Path.GetFileName(path), out _)) result.Add(path);
            }
            result.Sort((a, b) => ParseFromTick(a).CompareTo(ParseFromTick(b)));
            return result;
        }

        public static bool TryParseSegmentFromTick(string fileName, out ulong fromTick)
        {
            fromTick = 0;
            if (!fileName.StartsWith("packets_") || !fileName.EndsWith(".bin")) return false;
            var core = fileName.Substring("packets_".Length, fileName.Length - "packets_".Length - ".bin".Length);
            return ulong.TryParse(core, out fromTick);
        }

        private static ulong ParseFromTick(string path)
        {
            TryParseSegmentFromTick(Path.GetFileName(path), out var fromTick);
            return fromTick;
        }
    }
}
```

`ReceivedPacketLogReader.cs`:
```csharp
using System.Collections.Generic;
using System.IO;

namespace Game.SaveLoad.Snapshot
{
    public static class ReceivedPacketLogReader
    {
        // 区間ファイルを与えられた順に読み、tick昇順で返す（同tick内は記録順）
        // Read segment files in the given order and return records ordered by tick (record order within a tick)
        public static List<ReceivedPacketRecord> ReadAll(IEnumerable<string> segmentFilePaths)
        {
            var result = new List<ReceivedPacketRecord>();
            foreach (var path in segmentFilePaths)
            {
                using var reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    var tick = reader.ReadUInt64();
                    var length = reader.ReadInt32();
                    var payload = reader.ReadBytes(length);
                    result.Add(new ReceivedPacketRecord(tick, payload));
                }
            }
            result.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            return result;
        }
    }
}
```
（`List.Sort` は不安定ソートなので、同tick内の順序保持が必要。`Sort` の代わりに `result = result.OrderBy(r => r.Tick).ToList()`（LINQの `OrderBy` は安定）を使う。`using System.Linq;` を追加）

`WorldSnapshotRing.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Writer;
using UniRx;
using UnityEngine;

namespace Game.SaveLoad.Snapshot
{
    // 周期および即時要求でワールドの保存像を取り込み、別スレッドで書き出し、世代数を超えた古い世代を消す
    // Captures world images periodically or on request, writes them off-thread, and prunes generations beyond the limit
    public sealed class WorldSnapshotRing : ISnapshotCaptureRequest, ISnapshotWrittenNotifier
    {
        private readonly AssembleSaveJsonText _assembler;
        private readonly SaveWriteWorker _worker;
        private readonly WorldDataDirectory _directory;
        private readonly ReceivedPacketLog _packetLog;
        private readonly Subject<SnapshotWritten> _onSnapshotWritten = new();
        private readonly List<ulong> _writtenTicks = new();
        private readonly Dictionary<ulong, List<long>> _requestIdsByTick = new();
        private readonly List<long> _pendingImmediateRequestIds = new();
        private readonly object _requestLock = new();
        private uint _periodTicks;
        private int _generations;
        private ulong _nextPeriodicTick;
        private long _immediateRequestCounter;

        public WorldSnapshotRing(AssembleSaveJsonText assembler, SaveWriteWorker worker, WorldDataDirectory directory, ReceivedPacketLog packetLog)
        {
            _assembler = assembler;
            _worker = worker;
            _directory = directory;
            _packetLog = packetLog;
        }

        public bool IsActive { get; private set; }
        public IObservable<SnapshotWritten> OnSnapshotWritten => _onSnapshotWritten;
        public IReadOnlyList<ulong> WrittenTicks => _writtenTicks;

        public void Start(uint periodTicks, int generations)
        {
            _periodTicks = periodTicks;
            _generations = generations;
            _nextPeriodicTick = GameUpdater.CurrentTick + periodTicks;
            Directory.CreateDirectory(_directory.SnapshotDirectory);
            _packetLog.Start(_directory.SnapshotDirectory, GameUpdater.CurrentTick + 1);
            IsActive = true;
            Debug.Log($"常時記録を開始しました period:{periodTicks}tick generations:{generations} dir:{_directory.SnapshotDirectory}");
        }

        // 次のtick末尾で取る。戻り値の要求IDは完了通知の RequestId と突き合わせる
        // Taken at the next tick end; match the returned request id against SnapshotWritten.RequestId
        public long RequestImmediateSnapshot()
        {
            if (!IsActive)
            {
                Debug.LogWarning("常時記録が無効のため即時スナップショット要求を無視しました");
                return 0;
            }
            var id = Interlocked.Increment(ref _immediateRequestCounter);
            lock (_requestLock)
            {
                _pendingImmediateRequestIds.Add(id);
            }
            return id;
        }

        // FinalTickEndUpdates から毎tick呼ばれる。完了通知の排出→取り込み判定の順
        // Called every tick from FinalTickEndUpdates: drain completions, then decide whether to capture
        public void Update()
        {
            DrainCompletions();
            if (!IsActive) return;

            var tick = GameUpdater.CurrentTick;
            var requestIds = TakePendingRequestIds();
            var periodicDue = tick >= _nextPeriodicTick;
            if (requestIds.Count == 0 && !periodicDue) return;
            if (periodicDue) _nextPeriodicTick += _periodTicks;

            _requestIdsByTick[tick] = requestIds;
            var data = _assembler.Capture();
            _packetLog.Flush();
            _packetLog.Rotate(tick + 1);
            _worker.Enqueue(new SaveWriteJob(0, SaveWriteKind.Snapshot, data, _directory.SnapshotFilePath(tick), false));
        }

        public void WaitForPendingWrites()
        {
            _worker.WaitForIdle();
            DrainCompletions();
        }

        private List<long> TakePendingRequestIds()
        {
            lock (_requestLock)
            {
                var taken = new List<long>(_pendingImmediateRequestIds);
                _pendingImmediateRequestIds.Clear();
                return taken;
            }
        }

        private void DrainCompletions()
        {
            while (_worker.TryDequeueCompletion(SaveWriteKind.Snapshot, out var completion))
            {
                var requestIds = _requestIdsByTick.TryGetValue(completion.Tick, out var ids) ? ids : new List<long>();
                _requestIdsByTick.Remove(completion.Tick);
                if (!completion.Success)
                {
                    Debug.LogError($"スナップショットの書き出しに失敗しました tick:{completion.Tick} 要求ID:{string.Join(",", requestIds)}");
                    continue;
                }
                _writtenTicks.Add(completion.Tick);
                Prune();
                if (requestIds.Count == 0) requestIds.Add(0);
                foreach (var requestId in requestIds)
                {
                    _onSnapshotWritten.OnNext(new SnapshotWritten(requestId, completion.Tick, completion.TargetPath));
                }
            }
        }

        private void Prune()
        {
            while (_writtenTicks.Count > _generations)
            {
                var oldest = _writtenTicks[0];
                _writtenTicks.RemoveAt(0);
                File.Delete(_directory.SnapshotFilePath(oldest));
                _packetLog.DeleteSegmentsBefore(_writtenTicks[0]);
            }
        }
    }
}
```
（200行制限に注意。超える場合は `Prune` と `DrainCompletions` を `WorldSnapshotRingCompletionDrainer` として同ディレクトリへ分離する）

- [ ] **Step 6: 配線する**

`StartServerSettings.cs` の `AutoSave` の直後:
```csharp
        // 常時記録（スナップショットリング＋パケットログ）。テスト・プレイテストは false で起動する
        // Always-on capture (snapshot ring + packet log); tests and playtests boot with false
        [Option(isFlag: false, "--captureRing")]
        public bool CaptureRing { get; set; } = true;
```

`MoorestechServerDIContainerGenerator.cs`: `services.AddSingleton<SaveWriteWorker>();` の直後に `services.AddSingleton<ReceivedPacketLog>(); services.AddSingleton<WorldSnapshotRing>(); services.AddSingleton<ISnapshotCaptureRequest>(provider => provider.GetRequiredService<WorldSnapshotRing>()); services.AddSingleton<ISnapshotWrittenNotifier>(provider => provider.GetRequiredService<WorldSnapshotRing>());`（`using Game.SaveLoad.Snapshot;`）。L313 の `SaveIfRequested` 登録の直後に:
```csharp
            // 常時記録のスナップショットはセーブと同じ安定点で取る（Startされるまで何もしない）
            // Always-on snapshots are captured at the same stable point as saves (inert until Start)
            GameUpdater.FinalTickEndUpdates.Add(serviceProvider.GetRequiredService<WorldSnapshotRing>().Update);
```

`ReceiveQueueProcessor.cs`: フィールド `private readonly ReceivedPacketLog _receivedPacketLog;` と ctor 引数を追加。`ProcessPacket` の先頭に:
```csharp
            // 再生の真実はここ（tick末尾の処理点）。クライアント送信時刻ではなく処理tickで記録する
            // Replay truth lives here at the tick-end processing point; record the processing tick, not the client send time
            _receivedPacketLog.Append(GameUpdater.CurrentTick, packet);
```
（`using Core.Update; using Game.SaveLoad.Snapshot;`。`Server.Boot.asmdef` の `references` に `Game.SaveLoad` と `Core.Update` が無ければ追加）

`ServerListenAcceptor.StartServer` に `ReceivedPacketLog receivedPacketLog` 引数（`TickEndPacketQueue` の直後）を追加し、`new ReceiveQueueProcessor(packetResponseCreator, sendQueueProcessor, packetResponseContext, tickEndPacketQueue, receivedPacketLog)` に渡す。

`ServerInstanceManager.Start`: `var tickEndPacketQueue = ...;` の直後に `var receivedPacketLog = serviceProvider.GetRequiredService<ReceivedPacketLog>();` を追加し `StartServer(..., tickEndPacketQueue, receivedPacketLog, token)` に渡す。`if (settings.AutoSave) {...}` の直後に:
```csharp
            // 常時記録はtickスレッド開始前に開始し、開始tickの次から区間を切る
            // Start always-on capture before the tick thread so the first segment begins right after the start tick
            if (settings.CaptureRing)
            {
                serviceProvider.GetRequiredService<WorldSnapshotRing>().Start(SnapshotRingConfig.PeriodTicks, SnapshotRingConfig.Generations);
            }
```

クライアント側4ファイル（`PlaytestWorldBootSession.cs`・`EditModeInPlayingTestUtil.cs`・`SkipSaveLoadPlayModeSettings.cs`・`StandaloneTerrainQaSettings.cs`）の `AutoSave = false` の直後の行に `CaptureRing = false,`（オブジェクト初期化子）または `settings.CaptureRing = false;`（代入）を追加する。`grep -rn "AutoSave = false" moorestech_client/Assets/Scripts` で同型が他に無いか確認し、あれば同じく追加する。

テストで `new ReceiveQueueProcessor(` を直接生成している箇所があれば（`grep -rn "new ReceiveQueueProcessor(" moorestech_server/Assets/Scripts/Tests`）第5引数に `new ReceivedPacketLog()` を渡す。

- [ ] **Step 7: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.UnitTest\.Game\.SaveLoad\.ReceivedPacketLogTest$|^Tests\.CombinedTest\.Game\.Snapshot\.WorldSnapshotRingTest$|^Tests\.UnitTest\.Game\.WorldDataDirectoryTest$|^Tests\.CombinedTest\.Server\.PacketTest\.TickEnd.*"`
Expected: 全PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Paths/WorldDataDirectory.cs moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Playtest/PlaytestWorldBootSession.cs moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Util/EditModeInPlayingTestUtil.cs moorestech_client/Assets/Scripts/Client.Starter/Editor/SkipSaveLoadPlayModeSettings.cs moorestech_client/Assets/Scripts/Client.Starter/StandaloneQa/StandaloneTerrainQaSettings.cs
git commit -m "feat(server): 30秒周期スナップショットリングと処理tick付きパケットログの常時記録"
```

---

### Task 7: 即時取得プロトコル `va:bugReportCapture` と完了イベント

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BugReportCaptureProtocol.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/BugReportCaptureCompletedEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs:41`（`SaveProtocol` の登録行の直後に追加）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs:276`（`WorldSaveCompletedEventPacket` 登録の直後に追加）
- Modify: `moorestech_server/Assets/Scripts/Server.Event/Server.Event.asmdef`（`references` に `"Game.Paths"` を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs:46`（`Save` の直後にメソッド追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BugReportCaptureProtocolTest.cs`（新規）

**Interfaces:**
- Consumes: Task 6 `ISnapshotCaptureRequest`／`ISnapshotWrittenNotifier`／`WorldDataDirectory.SnapshotDirectory`
- Produces:
  - `BugReportCaptureProtocol.ProtocolTag = "va:bugReportCapture"`、ネスト `BugReportCaptureRequest : ProtocolMessagePackBase`（`[Key(2)] BugReportCaptureOperation Operation`、`static CreateCaptureNowRequest()`）、`BugReportCaptureResponse : ProtocolMessagePackBase`（`[Key(2)] long RequestedCaptureId`）、`enum BugReportCaptureOperation { CaptureNow = 0 }`
  - `BugReportCaptureCompletedEventPacket.EventTag = "va:event:bugReportCaptureCompleted"`、ネスト `BugReportCaptureCompletedMessagePack`（`[Key(0)] long CaptureId`、`[Key(1)] ulong Tick`、`[Key(2)] string SnapshotDirectory`、`[Key(3)] List<string> SnapshotFileNames`、`[Key(4)] List<string> PacketLogFileNames`）
  - クライアント `UniTask<BugReportCaptureProtocol.BugReportCaptureResponse> VanillaApiWithResponse.RequestBugReportCapture(CancellationToken ct)`

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/BugReportCaptureProtocolTest.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BugReportCaptureProtocolTest
    {
        [Test]
        public void 即時取得を要求すると次のtick末尾で書かれ完了イベントに一覧が載る()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-capture-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(10);
            ring.Start(600, 4);
            var sink = EventTestUtil.RegisterCaptureSink(provider, 1);

            var request = MessagePackSerializer.Serialize(BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest());
            var responseBytes = packet.GetPacketResponse(request, new PacketResponseContext(null));
            var response = MessagePackSerializer.Deserialize<BugReportCaptureProtocol.BugReportCaptureResponse>(responseBytes[0]);
            Assert.Greater(response.RequestedCaptureId, 0L);

            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();

            var completed = sink.TakeAll().Where(e => e.Tag == BugReportCaptureCompletedEventPacket.EventTag).ToList();
            Assert.AreEqual(1, completed.Count, "完了イベントが1件届いていない");
            var payload = MessagePackSerializer.Deserialize<BugReportCaptureCompletedEventPacket.BugReportCaptureCompletedMessagePack>(completed[0].Payload);
            Assert.AreEqual(response.RequestedCaptureId, payload.CaptureId);
            Assert.AreEqual(11UL, payload.Tick);
            Assert.AreEqual(provider.GetRequiredService<WorldDataDirectory>().SnapshotDirectory, payload.SnapshotDirectory);
            CollectionAssert.Contains(payload.SnapshotFileNames, "tick_11.json");
            CollectionAssert.Contains(payload.PacketLogFileNames, "packets_11.bin");
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }
    }
}
```

- [ ] **Step 2: Unity再起動後に実行して失敗を確認する**

Run: `uloop launch ./moorestech_client --restart` → `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー

- [ ] **Step 3: プロトコルを実装する**

`BugReportCaptureProtocol.cs`:
```csharp
using System;
using Game.SaveLoad.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // バグ報告用の即時スナップショットを要求する。書き出しはtick末尾のリングが行い、完了はイベントで通知する
    // Requests an immediate snapshot for a bug report; the tick-end ring writes it and completion arrives as an event
    public class BugReportCaptureProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:bugReportCapture";

        private readonly ISnapshotCaptureRequest _snapshotCaptureRequest;

        public BugReportCaptureProtocol(ServiceProvider serviceProvider)
        {
            _snapshotCaptureRequest = serviceProvider.GetRequiredService<ISnapshotCaptureRequest>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<BugReportCaptureRequest>(payload);
            switch (request.Operation)
            {
                case BugReportCaptureOperation.CaptureNow:
                    return new BugReportCaptureResponse(_snapshotCaptureRequest.RequestImmediateSnapshot());
            }

            Debug.LogError($"未知のバグ報告取得操作です operation:{request.Operation}");
            return new BugReportCaptureResponse(0);
        }

        #region MessagePack

        [MessagePackObject]
        public class BugReportCaptureRequest : ProtocolMessagePackBase
        {
            [Key(2)] public BugReportCaptureOperation Operation { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BugReportCaptureRequest() { }

            private BugReportCaptureRequest(BugReportCaptureOperation operation)
            {
                Tag = ProtocolTag;
                Operation = operation;
            }

            public static BugReportCaptureRequest CreateCaptureNowRequest()
            {
                return new BugReportCaptureRequest(BugReportCaptureOperation.CaptureNow);
            }
        }

        [MessagePackObject]
        public class BugReportCaptureResponse : ProtocolMessagePackBase
        {
            [Key(2)] public long RequestedCaptureId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BugReportCaptureResponse() { }

            public BugReportCaptureResponse(long requestedCaptureId)
            {
                Tag = ProtocolTag;
                RequestedCaptureId = requestedCaptureId;
            }
        }

        public enum BugReportCaptureOperation
        {
            CaptureNow = 0,
        }

        #endregion
    }
}
```
`PacketResponseCreator.cs` の `SaveProtocol` 登録行の直後に:
```csharp
            _packetResponseDictionary.Add(BugReportCaptureProtocol.ProtocolTag, new BugReportCaptureProtocol(serviceProvider));
```

- [ ] **Step 4: 完了イベントを実装する**

`BugReportCaptureCompletedEventPacket.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 即時スナップショットの書き出し完了を、バンドル組み立てに必要なファイル一覧付きで配信する
    // Broadcasts that an immediate snapshot finished writing, with the file list needed to assemble a bundle
    public class BugReportCaptureCompletedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:bugReportCaptureCompleted";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly ISnapshotWrittenNotifier _snapshotWrittenNotifier;
        private readonly WorldDataDirectory _worldDataDirectory;

        public BugReportCaptureCompletedEventPacket(EventProtocolProvider eventProtocolProvider, ISnapshotWrittenNotifier snapshotWrittenNotifier, WorldDataDirectory worldDataDirectory)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _snapshotWrittenNotifier = snapshotWrittenNotifier;
            _worldDataDirectory = worldDataDirectory;
        }

        public void Load()
        {
            // 周期スナップショット（RequestId=0）は配信しない。要求付きの完了だけを流す
            // Periodic snapshots (RequestId=0) are not broadcast; only requested completions are
            _snapshotWrittenNotifier.OnSnapshotWritten.Where(w => w.RequestId != 0).Subscribe(OnSnapshotWritten);

            #region Internal

            void OnSnapshotWritten(SnapshotWritten written)
            {
                var directory = _worldDataDirectory.SnapshotDirectory;
                var snapshots = Directory.GetFiles(directory, "tick_*.json").Select(Path.GetFileName).OrderBy(n => n).ToList();
                var packetLogs = Directory.GetFiles(directory, "packets_*.bin").Select(Path.GetFileName).OrderBy(n => n).ToList();
                var payload = MessagePackSerializer.Serialize(new BugReportCaptureCompletedMessagePack(written.RequestId, written.Tick, directory, snapshots, packetLogs));
                _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
            }

            #endregion
        }

        [MessagePackObject]
        public class BugReportCaptureCompletedMessagePack
        {
            [Key(0)] public long CaptureId { get; set; }
            [Key(1)] public ulong Tick { get; set; }
            [Key(2)] public string SnapshotDirectory { get; set; }
            [Key(3)] public List<string> SnapshotFileNames { get; set; }
            [Key(4)] public List<string> PacketLogFileNames { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BugReportCaptureCompletedMessagePack() { }

            public BugReportCaptureCompletedMessagePack(long captureId, ulong tick, string snapshotDirectory, List<string> snapshotFileNames, List<string> packetLogFileNames)
            {
                CaptureId = captureId;
                Tick = tick;
                SnapshotDirectory = snapshotDirectory;
                SnapshotFileNames = snapshotFileNames;
                PacketLogFileNames = packetLogFileNames;
            }
        }
    }
}
```
DI: `services.AddSingleton<WorldSaveCompletedEventPacket>();` の直後に `services.AddSingleton<BugReportCaptureCompletedEventPacket>();`。`Server.Event.asmdef` の `references` に `"Game.Paths"` を追加。

- [ ] **Step 5: クライアントAPIを追加する**

`VanillaApiWithResponse.cs` の `Save` の直後:
```csharp
        // バグ報告用の即時スナップショットを要求する。完了は BugReportCaptureCompletedEventPacket.EventTag で届く
        // Requests an immediate snapshot for a bug report; completion arrives via BugReportCaptureCompletedEventPacket.EventTag
        public async UniTask<BugReportCaptureProtocol.BugReportCaptureResponse> RequestBugReportCapture(CancellationToken ct)
        {
            var request = BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest();
            return await _packetExchangeManager.GetPacketResponse<BugReportCaptureProtocol.BugReportCaptureResponse>(request, ct);
        }
```

- [ ] **Step 6: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Server\.PacketTest\.BugReportCaptureProtocolTest$"`
Expected: PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Server.Event moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BugReportCaptureProtocolTest.cs moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs
git commit -m "feat(server): バグ報告の即時スナップショット要求プロトコルと完了イベント"
```

---

### Task 8: 再生器 `SnapshotReplayer` と比較器 `SnapshotJsonComparer`、決定性検査テスト

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/SnapshotComparison.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot/SnapshotJsonComparer.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Replay/ReplayRequest.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Replay/ReplayResult.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Replay/ReplayPacketEntry.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Replay/SnapshotReplayer.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SnapshotJsonComparerTest.cs`・`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/Replay/SnapshotReplayDeterminismTest.cs`（新規）

**Interfaces:**
- Consumes: Task 4 `Capture`／`Serialize`、Task 6 `ReceivedPacketLogReader`／`ReceivedPacketRecord`／`WorldSnapshotRing`、`TickEndPacketQueue`／`ITickEndPacketEntry`／`PacketResponseCreator.GetPacketResponse`
- Produces:
  - `public sealed class SnapshotComparison { public bool Equal { get; } public IReadOnlyList<string> Differences { get; } }`
  - `public static class SnapshotJsonComparer { public const string ExcludedTopLevelKey = "setting"; public static SnapshotComparison Compare(string expectedJson, string actualJson); }`
  - `public sealed class ReplayRequest { public string ServerDataDirectory { get; } public string SnapshotFilePath { get; } public IReadOnlyList<string> PacketLogFilePaths { get; } public ulong TargetTick { get; } }`
  - `public sealed class ReplayResult { public ulong LoadedTick { get; } public ulong ReachedTick { get; } public int ReplayedPacketCount { get; } public string SnapshotJson { get; } }`
  - `public static class SnapshotReplayer { public static ReplayResult Replay(ReplayRequest request); }`

- [ ] **Step 1: 比較器の失敗するテストを書く**

`Tests/UnitTest/Game/SaveLoad/SnapshotJsonComparerTest.cs`:
```csharp
using Game.SaveLoad.Snapshot;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SnapshotJsonComparerTest
    {
        [Test]
        public void settingの差は無視しそれ以外の差はパスで報告する()
        {
            var a = "{\"world\":[{\"X\":1,\"state\":{\"k\":{\"v\":1}}}],\"setting\":{\"t\":\"2026-01-01\"},\"currentTick\":5}";
            var b = "{\"world\":[{\"X\":1,\"state\":{\"k\":{\"v\":2}}}],\"setting\":{\"t\":\"2026-09-11\"},\"currentTick\":5}";
            var same = "{\"world\":[{\"X\":1,\"state\":{\"k\":{\"v\":1}}}],\"setting\":{\"t\":\"2099-01-01\"},\"currentTick\":5}";

            Assert.IsTrue(SnapshotJsonComparer.Compare(a, same).Equal);
            var diff = SnapshotJsonComparer.Compare(a, b);
            Assert.IsFalse(diff.Equal);
            Assert.AreEqual(1, diff.Differences.Count);
            StringAssert.Contains("world[0].state.k.v", diff.Differences[0]);
        }
    }
}
```

- [ ] **Step 2: 決定性検査の失敗するテストを書く**

`Tests/CombinedTest/Server/Replay/SnapshotReplayDeterminismTest.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Replay;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.Replay
{
    // スナップショットkからパケットを流し直すとk+1と一致する。これが再生の忠実性の唯一の検査
    // Replaying packets from snapshot k must reproduce snapshot k+1; this is the only fidelity check for replay
    public class SnapshotReplayDeterminismTest
    {
        [Test]
        public void スナップショットkから再生するとk_plus_1と一致する()
        {
            var saveRoot = Path.Combine(Path.GetTempPath(), $"moorestech-replay-{Guid.NewGuid():N}");
            var savePath = Path.Combine(saveRoot, "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var directory = provider.GetRequiredService<WorldDataDirectory>();
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            var packetLog = provider.GetRequiredService<ReceivedPacketLog>();
            GameRandom.Reseed(2026UL);
            GameUpdater.RestoreCurrentTick(0);
            ring.Start(10, 4);
            GrantRequiredItems(provider, ForUnitTestModBlockId.BlockId, 3);
            GrantRequiredItems(provider, ForUnitTestModBlockId.ChestId, 1);
            UnlockBlock(provider, ForUnitTestModBlockId.ChestId);

            // tick末尾で処理される経路（ログ点）を通すため、受信プロセッサ相当の処理をtick中に行う
            // Route packets through the tick-end path (the log point), as the receive processor would
            var context = new PacketResponseContext(null);
            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            void Send(byte[] payload) => queue.Enqueue(new ReplayPacketEntry(packet, context, payload, packetLog));

            for (var tick = 1; tick <= 45; tick++)
            {
                if (tick == 3) Send(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (10, 0)));
                if (tick == 12) Send(CreatePlaceBlockPayload(ForUnitTestModBlockId.ChestId, (14, 0)));
                if (tick == 12) Send(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (12, 0)));
                if (tick == 27) Send(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (16, 0)));
                GameUpdater.UpdateOneTick();
            }
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            CollectionAssert.AreEqual(new ulong[] { 10, 20, 30, 40 }, ring.WrittenTicks);

            var expected20 = File.ReadAllText(directory.SnapshotFilePath(20));
            var expected40 = File.ReadAllText(directory.SnapshotFilePath(40));
            var segments = packetLog.SegmentFilePaths().ToList();

            // 10→20（設置を跨ぐ）と 10→40（複数世代）を検査する
            // Check 10→20 (crossing placements) and 10→40 (spanning generations)
            var result20 = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory, directory.SnapshotFilePath(10), segments, 20));
            Assert.AreEqual(20UL, result20.ReachedTick);
            Assert.AreEqual(2, result20.ReplayedPacketCount);
            var comparison20 = SnapshotJsonComparer.Compare(expected20, result20.SnapshotJson);
            Assert.IsTrue(comparison20.Equal, "10→20 が一致しない:\n" + string.Join("\n", comparison20.Differences));

            var result40 = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory, directory.SnapshotFilePath(10), segments, 40));
            var comparison40 = SnapshotJsonComparer.Compare(expected40, result40.SnapshotJson);
            Assert.IsTrue(comparison40.Equal, "10→40 が一致しない:\n" + string.Join("\n", comparison40.Differences));

            Directory.Delete(saveRoot, true);
        }
    }
}
```
（`ReplayPacketEntry` を「通常受信と同じログ点を通す」テスト用送信経路として使う。`ReplayPacketEntry` の第4引数 `ReceivedPacketLog` が null なら記録しない）

- [ ] **Step 3: Unity再起動後に実行して失敗を確認する**

Run: `uloop launch ./moorestech_client --restart` → `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー

- [ ] **Step 4: 比較器を実装する**

`SnapshotComparison.cs`:
```csharp
using System.Collections.Generic;

namespace Game.SaveLoad.Snapshot
{
    public sealed class SnapshotComparison
    {
        public bool Equal => Differences.Count == 0;
        public IReadOnlyList<string> Differences { get; }

        public SnapshotComparison(IReadOnlyList<string> differences)
        {
            Differences = differences;
        }
    }
}
```

`SnapshotJsonComparer.cs`:
```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Snapshot
{
    // 2つのスナップショットJSONを深く比較し、不一致のパスを返す。setting は実時刻を含むため除外する
    // Deep-compares two snapshot JSONs and lists differing paths; "setting" is excluded because it carries wall-clock times
    public static class SnapshotJsonComparer
    {
        public const string ExcludedTopLevelKey = "setting";
        private const int MaxDifferences = 50;

        public static SnapshotComparison Compare(string expectedJson, string actualJson)
        {
            var expected = JObject.Parse(expectedJson);
            var actual = JObject.Parse(actualJson);
            expected.Remove(ExcludedTopLevelKey);
            actual.Remove(ExcludedTopLevelKey);

            var differences = new List<string>();
            Walk(expected, actual, "", differences);
            return new SnapshotComparison(differences);
        }

        private static void Walk(JToken expected, JToken actual, string path, List<string> differences)
        {
            if (differences.Count >= MaxDifferences) return;
            if (expected.Type != actual.Type)
            {
                differences.Add($"{path}: 型が違う expected={expected.Type} actual={actual.Type}");
                return;
            }
            switch (expected)
            {
                case JObject expectedObject:
                    var actualObject = (JObject)actual;
                    foreach (var property in expectedObject.Properties())
                    {
                        var childPath = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                        if (!actualObject.TryGetValue(property.Name, out var actualValue))
                        {
                            differences.Add($"{childPath}: actual に無い");
                            continue;
                        }
                        Walk(property.Value, actualValue, childPath, differences);
                    }
                    foreach (var property in actualObject.Properties())
                    {
                        if (!expectedObject.ContainsKey(property.Name)) differences.Add($"{path}.{property.Name}: expected に無い");
                    }
                    return;
                case JArray expectedArray:
                    var actualArray = (JArray)actual;
                    if (expectedArray.Count != actualArray.Count)
                    {
                        differences.Add($"{path}: 要素数が違う expected={expectedArray.Count} actual={actualArray.Count}");
                        return;
                    }
                    for (var i = 0; i < expectedArray.Count; i++) Walk(expectedArray[i], actualArray[i], $"{path}[{i}]", differences);
                    return;
                default:
                    if (!JToken.DeepEquals(expected, actual)) differences.Add($"{path}: expected={expected} actual={actual}");
                    return;
            }
        }
    }
}
```

- [ ] **Step 5: 再生器を実装する**

`Replay/ReplayRequest.cs`:
```csharp
using System.Collections.Generic;

namespace Server.Boot.Replay
{
    public sealed class ReplayRequest
    {
        public string ServerDataDirectory { get; }
        public string SnapshotFilePath { get; }
        public IReadOnlyList<string> PacketLogFilePaths { get; }
        public ulong TargetTick { get; }

        public ReplayRequest(string serverDataDirectory, string snapshotFilePath, IReadOnlyList<string> packetLogFilePaths, ulong targetTick)
        {
            ServerDataDirectory = serverDataDirectory;
            SnapshotFilePath = snapshotFilePath;
            PacketLogFilePaths = packetLogFilePaths;
            TargetTick = targetTick;
        }
    }
}
```

`Replay/ReplayResult.cs`:
```csharp
namespace Server.Boot.Replay
{
    public sealed class ReplayResult
    {
        public ulong LoadedTick { get; }
        public ulong ReachedTick { get; }
        public int ReplayedPacketCount { get; }
        public string SnapshotJson { get; }

        public ReplayResult(ulong loadedTick, ulong reachedTick, int replayedPacketCount, string snapshotJson)
        {
            LoadedTick = loadedTick;
            ReachedTick = reachedTick;
            ReplayedPacketCount = replayedPacketCount;
            SnapshotJson = snapshotJson;
        }
    }
}
```

`Replay/ReplayPacketEntry.cs`:
```csharp
using Core.Update;
using Game.SaveLoad.Snapshot;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;

namespace Server.Boot.Replay
{
    // 記録済みパケットをtick末尾で処理する項目。応答は捨てる（再生に受信者はいない）
    // Processes a recorded packet at tick end; responses are discarded since replay has no receiver
    public sealed class ReplayPacketEntry : ITickEndPacketEntry
    {
        private readonly PacketResponseCreator _packetResponseCreator;
        private readonly PacketResponseContext _context;
        private readonly byte[] _payload;
        private readonly ReceivedPacketLog _packetLog;

        public bool IsActive => true;

        public ReplayPacketEntry(PacketResponseCreator packetResponseCreator, PacketResponseContext context, byte[] payload, ReceivedPacketLog packetLog)
        {
            _packetResponseCreator = packetResponseCreator;
            _context = context;
            _payload = payload;
            _packetLog = packetLog;
        }

        public void Process()
        {
            _packetLog?.Append(GameUpdater.CurrentTick, _payload);
            _packetResponseCreator.GetPacketResponse(_payload, _context);
        }
    }
}
```

`Replay/SnapshotReplayer.cs`:
```csharp
using System.IO;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.Replay
{
    // スナップショットをロードし、記録済みパケットを同じ処理tickへ流し直して目標tickまで進める
    // Loads a snapshot, feeds recorded packets back at their original processing ticks, and advances to the target tick
    public static class SnapshotReplayer
    {
        public static ReplayResult Replay(ReplayRequest request)
        {
            // ロード経路は通常起動と同じ（save.jsonの位置にスナップショットを置く）。常時記録は開始しない
            // Same load path as a normal boot (the snapshot sits where save.json would); capture stays off
            var tempRoot = Path.Combine(Path.GetTempPath(), $"moorestech-replay-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            var savePath = Path.Combine(tempRoot, "save.json");
            File.Copy(request.SnapshotFilePath, savePath);

            var options = new MoorestechServerDIContainerOptions(request.ServerDataDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(request.ServerDataDirectory, savePath),
            };
            var (packetResponseCreator, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var loadedTick = GameUpdater.CurrentTick;

            var records = ReceivedPacketLogReader.ReadAll(request.PacketLogFilePaths);
            var queue = provider.GetRequiredService<TickEndPacketQueue>();
            var context = new PacketResponseContext(null);
            var replayed = 0;
            var next = 0;

            // 記録tick == 次のtick のパケットを積んでから Update する。tick末尾でまとめて処理される
            // Enqueue packets whose recorded tick equals the next tick, then Update; they are processed together at tick end
            while (GameUpdater.CurrentTick < request.TargetTick)
            {
                var nextTick = GameUpdater.CurrentTick + 1;
                while (next < records.Count && records[next].Tick < nextTick) next++;
                while (next < records.Count && records[next].Tick == nextTick)
                {
                    queue.Enqueue(new ReplayPacketEntry(packetResponseCreator, context, records[next].Payload, null));
                    replayed++;
                    next++;
                }
                GameUpdater.Update();
            }

            var json = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
            Debug.Log($"再生完了 loaded:{loadedTick} reached:{GameUpdater.CurrentTick} packets:{replayed}");
            Directory.Delete(tempRoot, true);
            return new ReplayResult(loadedTick, GameUpdater.CurrentTick, replayed, json);
        }
    }
}
```
（`GameUpdater.Update()` は public でビルドでも使える。`UpdateOneTick` はEditor専用なので再生器では使わない）

- [ ] **Step 6: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.UnitTest\.Game\.SaveLoad\.SnapshotJsonComparerTest$|^Tests\.CombinedTest\.Server\.Replay\.SnapshotReplayDeterminismTest$"`
Expected: PASS。10→20 が不一致なら `Differences` のパスが発散源（DataStore／コンポーネント）を指すので、そこの非決定性（列挙順・未シード乱数・保存されない過渡状態）を直してから再実行する。原因と修正はコミットメッセージと判断記録に残す。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad/Snapshot moorestech_server/Assets/Scripts/Server.Boot/Replay moorestech_server/Assets/Scripts/Tests
git commit -m "feat(server): スナップショット再生器と比較器・決定性検査テスト"
```

---

### Task 9: 引っかかり無しの実測（world_1 複製・90秒プレイテスト）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/snapshot-ring-no-hitch.cs`
- Modify: `docs/superpowers/plans/2026-09-11-bug-report-a-server-foundation.md`（末尾「判断記録」へ計測値を追記）

**Interfaces:**
- Consumes: Task 6 `WorldSnapshotRing`（`ServerContext.GetService<WorldSnapshotRing>()`）、Task 4 `AssembleSaveJsonText.Capture()`

- [ ] **Step 1: 計測シナリオを書く**

```csharp
// スナップショットリング有効時にtickを取りこぼさないことと、取り込み時間を実測する（ADR 0057 の初版条件）
// Measures that the snapshot ring drops no ticks and how long a capture takes (ADR 0057 v1 condition)
using System.Diagnostics;
using Client.Playtest;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.SaveLoad.Json;
using Game.SaveLoad.Snapshot;
using UnityEngine;

var options = new PlaytestRunOptions { Record = false };

return PlaytestRunner.Run("snapshot-ring-no-hitch", options, async p =>
{
    await p.WaitSeconds(3f);
    var ring = ServerContext.GetService<WorldSnapshotRing>();
    ring.Start(SnapshotRingConfig.PeriodTicks, SnapshotRingConfig.Generations);
    p.Note($"blocks={ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count} ring started");

    var assembler = ServerContext.GetService<AssembleSaveJsonText>();
    var worst = 0d;
    for (var i = 0; i < 5; i++)
    {
        var sw = Stopwatch.StartNew();
        assembler.Capture();
        var ms = sw.Elapsed.TotalMilliseconds;
        if (worst < ms) worst = ms;
        p.Note($"capture#{i}: {ms:F1}ms");
        await p.WaitSeconds(0.5f);
    }
    p.Assert(worst <= 20d, $"取り込みが20ms以内 (worst {worst:F1}ms)");

    // 90秒で3回の周期スナップショットを跨ぎ、tickの取りこぼしを数える
    // Span three periodic snapshots over 90 seconds and count dropped ticks
    var startTick = GameUpdater.CurrentTick;
    var wall = Stopwatch.StartNew();
    await p.WaitSeconds(90f);
    var elapsedTicks = (double)(GameUpdater.CurrentTick - startTick);
    var expectedTicks = wall.Elapsed.TotalSeconds * GameUpdater.TicksPerSecond;
    var dropped = expectedTicks - elapsedTicks;
    p.Note($"ticks elapsed={elapsedTicks} expected={expectedTicks:F1} dropped={dropped:F1} snapshots={ring.WrittenTicks.Count}");
    p.Assert(ring.WrittenTicks.Count >= 3, "90秒で3回以上スナップショットが書かれた");
    p.Assert(dropped < 3d, $"取りこぼしが3tick未満 (dropped {dropped:F1})");
});
```
（`PlaytestWorldBootSession` は `CaptureRing=false` で起動するので、シナリオ内で明示的に `Start` する）

- [ ] **Step 2: world_1 複製で実行する**

Run（`moorestech_master` は作業ツリーのピンと同じ `61cd90f` の `/Users/katsumi/moorestech_master/server_v8`）:
```bash
W=moorestech_client/PlaytestResults/worlds/world_1-migrated   # Task 4 Step 8 で移行済みの複製
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.agents/skills/unity-playmode-recorded-playtest
PLAYTEST_WORLD_DIRECTORY="$PWD/$W" PLAYTEST_MAP_MODE=template PLAYTEST_SEED=0 \
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/misc/snapshot-ring-no-hitch.cs" /Users/katsumi/moorestech_master/server_v8
```
Expected: `Success: true`、Asserts 3件 PASS。`capture#n` の値と `dropped` を本plan末尾「判断記録」に転記する。20ms を超える場合は `Capture()` 内で重い `GetSaveJsonObject`（ブロック部）のコンポーネント側コピーを見直す（`WorldBlockDatastore.GetSaveJsonObject` の `BlockJsonObject` 生成はそのままで、各 `GetSaveState` が `JsonConvert` を呼んでいないことを再確認する）。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/snapshot-ring-no-hitch.cs docs/superpowers/plans/2026-09-11-bug-report-a-server-foundation.md
git commit -m "test(playtest): スナップショットリングの引っかかり実測シナリオと計測結果"
```

---

### Task 10: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1:** `moores-code-review` スキルを起動し、`origin/master..HEAD` の全変更をレビューする（対象: 本plan A の全コミット）。
- [ ] **Step 2:** 機械的指摘を反映して再コンパイル・関連テストを再実行する。レビュー反映が判定経路（`SaveIfRequested`・`WorldSnapshotRing.Update`・`SnapshotReplayer` のtick境界・`BlockComponentStateReader`）に触れたら、Task 8 の決定性テストと Task 9 の実機シナリオを反映後のバイナリで再実施してから完了とする。
- [ ] **Step 3:** 設計判断が要る指摘だけを AskUserQuestion で裁定に出す。

### Task 11: セッション終了可能状態にすること

- [ ] **Step 1:** `git status` で未コミットが無いことを確認し、`.moorestech-external-revisions.json` と `_CompileRequester.cs` の自動書き換え差分だけが残っていることを確認する（前者は `git checkout --`、後者は本planでスキーマ変更が無いので `git checkout --`）。
- [ ] **Step 2:** `bd note moorestech-yoag "plan A 完了: <最終コミット> / 計測値 capture=…ms dropped=…"` を残す。
- [ ] **Step 3:** plan B（クライアント取得・報告UI・バンドル）の開始プロンプトを出力する。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置 | 前例・根拠 |
|---|---|---|---|
| 1 | `GameRandom`（静的・決定的乱数） | `Core.Update` | 同アセンブリの `GameUpdater`（世界時計の静的供給源）と同役割。`Core.Update` は UniRx 以外に依存が無く、`Game.Block.Interface`・`Game.Blueprint` から参照を足しても循環しない |
| 2 | `GameUpdater.RestoreCurrentTick` | `Core.Update/GameUpdater.cs` | 既存の `CurrentTick { get; private set; }` の所有者。ロード時のみ呼ぶ契約をコメントに明記 |
| 3 | `BlockComponentStateReader` | `Game.Block.Interface/Component/` | `IBlockSaveState` と同ディレクトリ。ドメイン語彙を持たない読み出しユーティリティ |
| 4 | `WorldSaveAllInfoV1.currentTick/randomState` | `Game.SaveLoad/Json/WorldVersions/` | 層マップ「グローバル最小状態は `WorldSaveAllInfoV1` に素のフィールドで追加してよい」（`inventorySlotLevel` 前例） |
| 5 | `SaveWriteWorker`／`SaveWriteJob`／`SaveWriteCompletion`／`SaveWriteKind` | `Game.SaveLoad/Writer/` | `WorldSaveCoordinator` の書き込み部を切り出したもの。`SendQueueProcessor`（専用スレッド＋`BlockingCollection`）と同じ機構 |
| 6 | `WorldSnapshotRing`／`ReceivedPacketLog`／`ReceivedPacketLogReader`／`ReceivedPacketRecord`／`SnapshotRingConfig`／`SnapshotJsonComparer`／`SnapshotComparison` | `Game.SaveLoad/Snapshot/`（7ファイル） | セーブJSONの派生（周期保存・比較）は `Game.SaveLoad` の責務。`FinalTickEndUpdates` への登録は `WorldSaveCoordinator.SaveIfRequested` と同じ位置（DI生成器 L313 のコメント「将来の初回 snapshot 取得もこの位置に登録する」） |
| 7 | `SnapshotWritten`／`ISnapshotCaptureRequest`／`ISnapshotWrittenNotifier` | `Game.SaveLoad.Interface/` | `IWorldSaveRequest`／`IWorldSaveCompletionNotifier` と同型。プロトコル・イベント側は Interface だけを参照する |
| 8 | パケットログの記録点 | `Server.Boot/Loop/PacketProcessing/ReceiveQueueProcessor.ProcessPacket` | tick末尾の処理点＝再生の真実。`ReceivedPacketLog` は `Game.SaveLoad` の型だが受信プロセッサは既に `Game.SaveLoad` を参照する `Server.Boot` にある |
| 9 | `BugReportCaptureProtocol` | `Server.Protocol/PacketResponse/`（DTOはネスト） | `SaveProtocol`（要求番号を返し実処理はtick末尾）＋ `FilterSplitterStateProtocol`（Operation enum・private ctor＋static Create）と同型 |
| 10 | `BugReportCaptureCompletedEventPacket` | `Server.Event/EventReceive/` | `WorldSaveCompletedEventPacket`（`IBootInitializable`・DI AddSingleton・`AddBroadcastEvent`）と同型。3点セットの①。②初期データは不要（一過性の完了通知で可変状態ではない）、③クライアント購読は plan B |
| 11 | `SnapshotReplayer`／`ReplayRequest`／`ReplayResult`／`ReplayPacketEntry` | `Server.Boot/Replay/` | DI生成器と `PacketResponseCreator` を組み合わせる起動系の一種。`ServerInstanceManager` と同じ層 |
| 12 | `StartServerSettings.CaptureRing` | `Server.Boot/Args/` | `AutoSave` と同じ `[Option]` 属性。オフにする箇所は `AutoSave = false` の同型掃引 |
| 13 | 移行スクリプト | `scripts/save_migration/` | `moorestech-save-migration` スキルの手順（backup→変換→実ロード検証）に従う。スキルの references は雛形なので実体は `scripts/` に置く |

データフロー（Phase 1.5）: `クライアント送信 → (受信スレッド) TickEndPacketQueue → [tick末尾] ReceiveQueueProcessor.ProcessPacket ──ログ点──> PacketResponseCreator → 各プロトコル → DataStore群 → [FinalTickEnd] WorldSaveCoordinator.SaveIfRequested / WorldSnapshotRing.Update（Capture）→ SaveWriteWorker（別スレッド）→ ファイル → 完了キュー → [次tick] 完了通知 → EventPacket`。新規コンポーネントはすべて「書き手」（記録）か「読み手」（完了通知）で、既存フローへの分岐・逆流は無い。

機構選択（検査4）: 「取り込みをtickスレッドに残しJSON化だけを別スレッドへ」は、既存の同期セーブを抑止せず同じ安定点で動かす受動的統合。対案「セーブ形式を差分（dirty）化して周期保存を軽くする」は、全DataStoreへの変更検知の追加が要るため棄却（判断記録に記載）。

死活表（Phase 2.5）: 通常セーブ（`va:save`）→ 生きる（同じ worker 経由、完了通知は従来どおり）／オートセーブ5分 → 生きる／終了時 flush（`HasPendingSave` 待ち）→ 生きる（書き出し完了まで pending）／既存セーブのロード → **移行スクリプト必須**（旧形式は `BlockComponentStateReader` が明示例外で拒否）／プレイテストDSL・EditModeInPlayingTest → 生きる（`CaptureRing=false`）。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md`、裁定: `.decisions/2026-09-11-バグ報告*.md`（18件）、`.decisions/2026-09-11-スナップショットは体感できる引っかかりを出さないことを初版の条件にする.md`、`.decisions/2026-09-11-Tailscaleに繋がらない機材の運搬は初版では何もしない.md`
- **plan分割**（agent前提）: 取得側サーバー基盤（本plan A）／クライアント取得と報告UI（plan B）／Mac mini運搬と自動修正ラン（plan C）の3本。各planは単独でテスト可能な成果物で終わる。
- **`GameRandom` は `Core.Update` の静的クラス**（agent前提・前例 `GameUpdater`）。DataStore分離規約（staticに変更系を露出しない）の対象外だが、消費はサーバー世界ロジックに限る契約をコメントで明記。
- **`ItemInstanceId` は決定化しない**（agent前提）: クライアントも同一プロセスで `ItemStack` を生成し消費順が世界ロジック外で変わるため。`ItemStackSaveJsonObject` に載らずスナップショット比較に現れない。決定性検査で発散が出た場合に限り再検討する。
- **`WorldSettingsDatastore` の `DateTime` は据え置き、比較器で `setting` を除外**（agent前提）。AGENTS.md が「実世界の日時の記録」用途として許容している値。
- **ブロック内部の入れ子文字列（ベルトの `GetSaveJsonString()`）は据え置き**（agent前提）: 外側の文字列化を消せば取り込み80msの主因は消える。入れ子はブロック数比例だが要素あたり小さい。
- **セーブ形式にバージョン分岐は作らず移行スクリプトで一括変換**（agent前提・AGENTS.md「後方互換考慮不要」、moorestech-save-migration スキルの方針）。
- **差分（dirty）セーブは採らない**（agent前提）: 全DataStoreへの変更検知追加が要り、本planの範囲を超える。実測で「取り込み≤20ms・取りこぼし<3tick」を満たさなければ再検討。
- **`GameUpdater.Update()` を再生器で直接呼ぶ**（agent前提）: `UpdateOneTick` はEditor専用。ビルド版の再生（将来）に備えるのではなく、Server.Boot はランタイム側なのでEditor専用APIを呼べないため。
- **完了通知はtickスレッドで発火**（agent前提）: 書き出しスレッドから `Subject.OnNext` を呼ぶと購読側（イベント配信・`HasPendingSave` 判定）のスレッド前提が崩れるため、完了キューをtick末尾で排出する。
- Task 9 の計測値（2026-09-12・Mac mini・シナリオ `misc/snapshot-ring-no-hitch.cs`・2ラン一致）:
  - **ワールドは `world_generated` の複製**（`PlaytestResults/worlds/world_generated-ring/`、移行スクリプト適用・seed 196・mapMode generated）。plan が前提した「`world_1` の 8048 ブロック」はこのマシンに存在しない（`world_1` に `save.json` が無く、実在する最大の save は `world_generated` の **6ブロック**。Task 4 でも同じズレを報告済み）。そこで**シナリオ内で 基本土台7048 + 木のチェスト1000 を直接設置し、8054 ブロックの世界を作って**測った。map objects は 34227・mapVeins 1416。
  - `Capture()` 所要（同一プロセス・ブロック数を3段で切り分け）: **6ブロック 7.6〜7.7ms**（初回19.7ms）／**7054ブロック（土台のみ）14.3〜17.7ms**（初回137ms・2回目73ms・3回目62msはウォームアップ/GCの外れ値）／**8054ブロック（チェスト1000込み）19.1〜21.1ms、最悪25.5ms**（別ランでは16.5〜24.5ms、最悪61.8ms）。→ **受入「20ms以下」は 8054 ブロック規模では満たさない**。内訳は「固定費 ≈7.7ms（mapObjects 34227 の複製が主）＋ 土台7048 で約+7ms ＋ チェスト1000 で約+5ms」。
  - ブロック側の `GetSaveState` に `JsonConvert` は**無い**（`WorldBlockDatastore.GetSaveJsonObject` → `BlockSystem.GetSaveState` → 各コンポーネントはオブジェクトを返すだけ。brief の想定原因は否定）。ただし `TrainCar.CreateSaveData` は `ContainerSaveData = JsonConvert.SerializeObject(...)` を**取り込み経路で**呼ぶ（本計測のワールドは列車0なので未計上）。列車の多い世界では tick スレッドで JSON 直列化が走るため、20ms を詰めるならここが次の削り代。
  - tick の取りこぼし（90秒・8054ブロック・リング有効）: **elapsed=1769 / expected=1805.6 / dropped=36.6**（別ラン 1765 / 1805.6 / 40.6）。→ **受入「3tick未満」を満たさない**。
  - ただし**同一世界でリングを止めた30秒の基準測定**でも dropped=17.6（別ラン 32.3）＝ **2.9%/5.0% の恒常的な遅れ**があり、リング有効時の遅れ率（2.0%/2.25%）は**基準より低い**。リング起因分（基準率で正規化した差）は **-15.5 tick / -50.1 tick**＝**測定可能な増加なし**。周期スナップショットは90秒で3回とも書かれた（`snapshots=3`・PASS）。
  - 原因は `ServerGameUpdater.StartUpdate` が `FrameInterval - 実行時間` を `Thread.Sleep` するだけで**取り戻さない**こと。Sleep のオーバーシュートがそのまま累積するため、「壁時計換算で3tick以上欠けない」は**リングの有無と無関係に構造上達成できない**。R10 の閾値はリングではなく tick ループのドリフトを測っている。
  - 結論: 「スナップショットが体感できる引っかかりを出さない」という初版条件そのものは満たす（リング起因のtick損失ゼロ・周期書き出し成功）。R10 の**数値の書き方**（絶対dropped<3・capture≤20ms）は要再裁定。差分（dirty）セーブの再検討条件（上記）に該当するのは capture 20ms の方のみ。
