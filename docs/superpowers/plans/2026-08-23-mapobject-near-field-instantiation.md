# mapObject起動生成の近傍先行＋時間予算分散化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 約79,000個のmapObject全量生成を待っていた起動を、PlayerPos中心・半径150mの近傍のみの待機に変え、残りはゲーム開始後に距離順・時間予算制でバックグラウンド生成する（ADR 0030 / bd: moorestech-4z88）。

**Architecture:** `MapObjectGameObjectDatastore` の生成ループを「距離順ソート→近傍レンジ（16ms/フレーム予算）→起動待機解除→残りレンジ（4ms/フレーム予算）」の2段保持タスクに再構成する。個体生成の本体（prefab解決・Instantiate・スナップショット適用・保留イベント適用・索引登録）は `MapObjectLayoutInstantiator` へ抽出し、未生成個体宛イベントは `MapObjectPendingStateLedger` が保留する。後着個体は既存の `MapObjectNearestSearcher.Register`→dirty経路にそのまま乗る。

**Tech Stack:** Unity C# (`Client.Game` asmdef) / UniTask / NUnit EditMode (`Client.Tests`)

## Requirements

裁定原本: `.decisions/2026-08-23-mapObject起動生成は近傍先行と時間予算分散にする.md` / `docs/adr/0030-mapobject-near-field-first-instantiation.md`（worktree作成後、両ファイルとこのplanをコピーして最初にコミットすること）

- R1. 起動待機境界（`IInitialEventApplyWaitTarget.WaitForInitialApplyAsync`）は `InitialHandshakeResponse.PlayerPos` 中心・半径150m以内のmapObject生成完了のみで解除される — 受け入れ: 近傍件数算出（境界値: ちょうど150mは近傍に含む）の単体テストが通り、EditModeInPlayingテストで近傍待機後に近傍layout全件が生成済み
- R2. 残り（150m超）は起動をブロックせず、ゲーム開始後にPlayerPosから近い順で生成される — 受け入れ: 距離順ソートの単体テストが通り、`WaitForAllInstantiatedAsync()` 後に既存の回転/スケール検証（任意個体）が通る
- R3. フレーム分散は個数固定（100個/フレーム）を廃止し時間予算制にする。近傍（ローディング中）16ms/フレーム・後着（ゲーム開始後）4ms/フレーム — 受け入れ: `FrameTimeBudget` 単体テストが通り、`MapObjectGameObjectDatastore` に個数間隔の定数が存在しない
- R4. 未生成個体宛の破壊/HPイベントは捨てず、instanceId単位の最新状態台帳に保留し、生成時にhandshakeスナップショットより優先適用する — 受け入れ: 台帳（上書き・消費で消える・destroy/hpの合成）の単体テストが通る
- R5. 全量生成完了の待機を正規API `WaitForAllInstantiatedAsync()` として公開し、既存の `MapObjectRotationTest` はこれを待つ — 受け入れ: 同テストが緑
- R6. 後着個体は既存の `MapObjectNearestSearcher.Register`→dirty→次探索時再構築の経路で索引に載る。索引側の設計変更はしない — 受け入れ: `MapObjectNearestSearcher.cs` / `NearestSearch/` 配下に差分が無い
- R7. 露頭（`OutcropGameObjectDatastore`）は変更しない — 受け入れ: 同ファイルに差分が無い
- R8. `MapObjectGameObject.Initialize` のGetComponentsInChildren走査を計測し、個体あたり生成時間の30%を超えて支配的なら `MapObjectRayTarget` のlazy解決へ是正する — 受け入れ: 計測値がplanのチェックボックス更新時に記録され、是正した場合は既存Mining系テストが緑
- R9. 変更後の各ファイルは200行以下・既存テスト全緑

**やらないこと（スコープ境界）:**
- 距離ストリーミング化（`ColliderDistanceCullingManager` のチャンク体系へのmapObject登録・離脱時非活性化）
- 小石等超大量種のGPUインスタンシング化
- 露頭の近傍先行化
- 後着中のk-d tree毎フレーム再構築の間引き（k-d treeセッションへ申し送り済み・bd登録済み）

## Global Constraints

- **起点ブランチ: `feature/nearest-search-kd-tree`**（`MapObjectNearestSearcher`・パース済み`MapObjectGuid`が前提）。worktree作成は `moores-wt new feature/mapobject-near-field-instantiation --from feature/nearest-search-kd-tree`
- partial禁止・`Func<>`禁止（LINQのラムダ引数は既存前例どおり可。自作APIのシグネチャに`Func<>`を置かない）・try-catch禁止（本planに外部境界は無い）
- 1ファイル200行以下・1ディレクトリ10ファイル以下（`Map/MapObject/` は現5ファイル→9ファイルになる）
- コメントは日本語・英語の2行セット（各1行）を約3〜10行ごと
- `#region Internal` はメソッド内ローカル関数のまとめ用途のみ
- 実時間API: `FrameTimeBudget` のStopwatchは規約適用外（クライアント描画分散。ADR 0030でユーザー裁定済み）。サーバーロジックには一切触れない
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client`（worktree側Editorで）
- テスト実行は `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`（**既定はPlayModeなので--test-mode EditMode必須**。EditModeInPlayingTestもEditModeとして実行される。実行後にドメインリロードエラーが出たら45秒待ってリトライ）
- 各タスク末尾でコミット。マージはSquashせず通常マージ

## 配置と前例（spec-architecture-review済み）

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `FrameTimeBudget`（新規） | `Client.Game/InGame/Map/MapObject/` | Stopwatch | 汎用機構だが現利用者はmapObject生成のみ（YAGNIでローカル配置。第2利用者が出たら昇格） |
| 2 | `MapObjectPendingStateLedger`（新規） | 同上 | Dictionary台帳 | ドメイン語彙を持つためドメイン層。`MapObjectNearestSearcher` と同型のplain collaborator |
| 3 | `MapObjectLayoutDistanceOrder`（新規） | 同上 | 静的ソートutil | 同上 |
| 4 | `MapObjectLayoutInstantiator`（新規） | 同上 | plain class（datastoreから抽出） | `MapObjectNearestSearcher` と同型（datastore配下のcollaborator） |
| 5 | `WaitForAllInstantiatedAsync`（既存クラスへ追加） | `MapObjectGameObjectDatastore` | UniTask.Preserve | 既存 `WaitForInitialApplyAsync` と対 |

**データフロー地図:** （サーバーイベント）→（`OnUpdateMapObject`＝唯一の書き手）→［`_allMapObjects`＋保留台帳］→（生成ループが台帳を消費）→（個体状態）。台帳は共有モデルへの第2の書き込み経路ではなく、既存の書き手内部のバッファ（交差点なし）。近傍/全量の2段待機は新規パターンだが、ADR 0030でユーザー裁定済み。

**機能パリティ（死活表）:** ピン探索=生きる（近傍は待機済み・後着はRegister→dirty）／採掘・フォーカス=生きる（近傍生成済み。遠方は開始直後に届かない）／開幕スキットの世界非表示=生きる（`SetActive(false)`はdatastore rootごと。後着個体もroot配下）／破壊/HP同期=生きる（台帳で強化）／回転・スケール適用=生きる（生成コード自体は移動のみ）。

---

### Task 1: `FrameTimeBudget`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/FrameTimeBudget.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Map/FrameTimeBudgetTest.cs`

**Interfaces:**
- Consumes: なし
- Produces: `public sealed class FrameTimeBudget` — `public FrameTimeBudget(double budgetMilliseconds)` ／ `public bool IsExhausted { get; }`（Restart/生成からの経過実時間が予算以上でtrue）／ `public void Restart()`

- [x] **Step 1: 失敗するテストを書く**

```csharp
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;

namespace Client.Tests.Map
{
    /// <summary>
    ///     フレーム時間予算の枯渇判定を検証
    ///     Verifies the frame time budget exhaustion decision
    /// </summary>
    public class FrameTimeBudgetTest
    {
        [Test]
        public void 予算ゼロは即座に枯渇する()
        {
            var budget = new FrameTimeBudget(0.0);
            Assert.IsTrue(budget.IsExhausted);
        }

        [Test]
        public void 十分大きい予算は枯渇しない()
        {
            var budget = new FrameTimeBudget(60000.0);
            Assert.IsFalse(budget.IsExhausted);
        }

        [Test]
        public void Restartで計測が仕切り直される()
        {
            var budget = new FrameTimeBudget(60000.0);
            budget.Restart();
            Assert.IsFalse(budget.IsExhausted);
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `FrameTimeBudget` が存在しないエラー

- [x] **Step 3: 最小限の実装を書く**

```csharp
using System.Diagnostics;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     フレームあたりの処理時間予算。Restartからの経過実時間が予算以上ならフレームを跨ぐ判断に使う
    ///     A per-frame processing time budget; elapsed real time since Restart at or over the budget means "cross a frame"
    /// </summary>
    public sealed class FrameTimeBudget
    {
        // 実時間API禁止規約はサーバーゲームロジック対象。クライアントの描画分散であるここは適用外（ADR 0030）
        // The no-realtime-API rule targets server game logic; client-side render spreading here is exempt (ADR 0030)
        private readonly double _budgetMilliseconds;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public FrameTimeBudget(double budgetMilliseconds)
        {
            _budgetMilliseconds = budgetMilliseconds;
        }

        public bool IsExhausted => _budgetMilliseconds <= _stopwatch.Elapsed.TotalMilliseconds;

        public void Restart()
        {
            _stopwatch.Restart();
        }
    }
}
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "FrameTimeBudgetTest"`
Expected: 3件PASS

- [x] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/FrameTimeBudget.cs* moorestech_client/Assets/Scripts/Client.Tests/Map/FrameTimeBudgetTest.cs*
git commit -m "feat(map-object): フレーム時間予算FrameTimeBudgetを追加"
```

（`.meta` はUnityコンパイル時に自動生成されるので一緒にコミットする。以降のタスクも同様）

---

### Task 2: `MapObjectPendingStateLedger`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectPendingStateLedger.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectPendingStateLedgerTest.cs`

**Interfaces:**
- Consumes: なし
- Produces: `public sealed class MapObjectPendingStateLedger` — `public void RecordDestroy(int instanceId)` ／ `public void RecordHp(int instanceId, int hp)` ／ `public bool TryConsume(int instanceId, out MapObjectPendingState state)`（未記録ならfalse。trueで台帳から消える）。`public readonly struct MapObjectPendingState`（`public readonly bool IsDestroyed; public readonly bool HasHp; public readonly int Hp;`）は同ファイルに置く

- [x] **Step 1: 失敗するテストを書く**

```csharp
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;

namespace Client.Tests.Map
{
    /// <summary>
    ///     未生成個体宛イベントの保留台帳を検証
    ///     Verifies the pending-state ledger for events addressed to not-yet-instantiated objects
    /// </summary>
    public class MapObjectPendingStateLedgerTest
    {
        [Test]
        public void 未記録のinstanceIdはfalse()
        {
            var ledger = new MapObjectPendingStateLedger();
            Assert.IsFalse(ledger.TryConsume(1, out _));
        }

        [Test]
        public void 破壊とHPは同一instanceIdへ合成される()
        {
            var ledger = new MapObjectPendingStateLedger();
            ledger.RecordHp(1, 30);
            ledger.RecordDestroy(1);

            Assert.IsTrue(ledger.TryConsume(1, out var state));
            Assert.IsTrue(state.IsDestroyed);
            Assert.IsTrue(state.HasHp);
            Assert.AreEqual(30, state.Hp);
        }

        [Test]
        public void HPは最新値で上書きされる()
        {
            var ledger = new MapObjectPendingStateLedger();
            ledger.RecordHp(1, 30);
            ledger.RecordHp(1, 10);

            Assert.IsTrue(ledger.TryConsume(1, out var state));
            Assert.AreEqual(10, state.Hp);
        }

        [Test]
        public void 消費すると台帳から消える()
        {
            var ledger = new MapObjectPendingStateLedger();
            ledger.RecordDestroy(1);

            Assert.IsTrue(ledger.TryConsume(1, out _));
            Assert.IsFalse(ledger.TryConsume(1, out _));
        }

        [Test]
        public void 別instanceIdへは波及しない()
        {
            var ledger = new MapObjectPendingStateLedger();
            ledger.RecordDestroy(1);
            Assert.IsFalse(ledger.TryConsume(2, out _));
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `MapObjectPendingStateLedger` が存在しないエラー

- [x] **Step 3: 最小限の実装を書く**

```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     未生成個体宛の破壊/HPイベントをinstanceId単位で保留する台帳。生成時に消費されスナップショットより優先される（ADR 0030）
    ///     Holds destroy/HP events for not-yet-instantiated objects per instanceId; consumed at instantiation and overrides the snapshot (ADR 0030)
    /// </summary>
    public sealed class MapObjectPendingStateLedger
    {
        private readonly Dictionary<int, MapObjectPendingState> _statesByInstanceId = new();

        public void RecordDestroy(int instanceId)
        {
            // 既存の保留HPを保ったまま破壊フラグだけ立てる（未記録ならdefault合成）
            // Keep any pending HP and raise only the destroyed flag (merging onto default when unrecorded)
            _statesByInstanceId.TryGetValue(instanceId, out var current);
            _statesByInstanceId[instanceId] = new MapObjectPendingState(true, current.HasHp, current.Hp);
        }

        public void RecordHp(int instanceId, int hp)
        {
            // 最新HPで上書きし、既存の破壊フラグは保つ
            // Overwrite with the latest HP while keeping any destroyed flag
            _statesByInstanceId.TryGetValue(instanceId, out var current);
            _statesByInstanceId[instanceId] = new MapObjectPendingState(current.IsDestroyed, true, hp);
        }

        public bool TryConsume(int instanceId, out MapObjectPendingState state)
        {
            if (!_statesByInstanceId.TryGetValue(instanceId, out state)) return false;
            _statesByInstanceId.Remove(instanceId);
            return true;
        }
    }

    /// <summary>
    ///     保留された破壊/HPの合成状態
    ///     The merged pending destroy/HP state
    /// </summary>
    public readonly struct MapObjectPendingState
    {
        public readonly bool IsDestroyed;
        public readonly bool HasHp;
        public readonly int Hp;

        public MapObjectPendingState(bool isDestroyed, bool hasHp, int hp)
        {
            IsDestroyed = isDestroyed;
            HasHp = hasHp;
            Hp = hp;
        }
    }
}
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectPendingStateLedgerTest"`
Expected: 5件PASS

- [x] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectPendingStateLedger.cs* moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectPendingStateLedgerTest.cs*
git commit -m "feat(map-object): 未生成個体宛イベントの保留台帳を追加"
```

---

### Task 3: `MapObjectLayoutDistanceOrder`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectLayoutDistanceOrder.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectLayoutDistanceOrderTest.cs`

**Interfaces:**
- Consumes: `Server.Protocol.PacketResponse.MapData.MapObjectLayoutMessagePack`（X/Y/Zプロパティと15引数ctor。`Client.Game` から参照可能・既存datastoreが使用済み）
- Produces: `public static class MapObjectLayoutDistanceOrder` — `public static List<Entry> Sort(IReadOnlyList<MapObjectLayoutMessagePack> layouts, Vector3 origin)`（近い順）／ `public static int CountWithinRadius(List<Entry> sortedEntries, float radius)`（ちょうどradiusは含む）。`public readonly struct Entry`（`public readonly MapObjectLayoutMessagePack Layout; public readonly float SqrDistance;`）は同ファイルに置く

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     距離順ソートと近傍境界の件数算出を検証
    ///     Verifies the distance ordering and the near-field boundary count
    /// </summary>
    public class MapObjectLayoutDistanceOrderTest
    {
        [Test]
        public void 原点から近い順に並ぶ()
        {
            var layouts = new List<MapObjectLayoutMessagePack>
            {
                CreateLayout(1, 100f, 0f, 0f),
                CreateLayout(2, 10f, 0f, 0f),
                CreateLayout(3, 50f, 0f, 0f),
            };

            var sorted = MapObjectLayoutDistanceOrder.Sort(layouts, Vector3.zero);

            Assert.AreEqual(2, sorted[0].Layout.InstanceId);
            Assert.AreEqual(3, sorted[1].Layout.InstanceId);
            Assert.AreEqual(1, sorted[2].Layout.InstanceId);
        }

        [Test]
        public void 距離はY成分も含む3Dで測る()
        {
            var layouts = new List<MapObjectLayoutMessagePack>
            {
                CreateLayout(1, 10f, 100f, 0f),
                CreateLayout(2, 20f, 0f, 0f),
            };

            var sorted = MapObjectLayoutDistanceOrder.Sort(layouts, Vector3.zero);
            Assert.AreEqual(2, sorted[0].Layout.InstanceId);
        }

        [Test]
        public void 半径ちょうどの個体は近傍に含む()
        {
            var layouts = new List<MapObjectLayoutMessagePack>
            {
                CreateLayout(1, 150f, 0f, 0f),
                CreateLayout(2, 150.001f, 0f, 0f),
            };

            var sorted = MapObjectLayoutDistanceOrder.Sort(layouts, Vector3.zero);
            Assert.AreEqual(1, MapObjectLayoutDistanceOrder.CountWithinRadius(sorted, 150f));
        }

        [Test]
        public void 全件が半径内なら全数を返す()
        {
            var layouts = new List<MapObjectLayoutMessagePack>
            {
                CreateLayout(1, 1f, 0f, 0f),
                CreateLayout(2, 2f, 0f, 0f),
            };

            var sorted = MapObjectLayoutDistanceOrder.Sort(layouts, Vector3.zero);
            Assert.AreEqual(2, MapObjectLayoutDistanceOrder.CountWithinRadius(sorted, 150f));
        }

        [Test]
        public void 空のlayoutでも成立する()
        {
            var sorted = MapObjectLayoutDistanceOrder.Sort(new List<MapObjectLayoutMessagePack>(), Vector3.zero);
            Assert.AreEqual(0, sorted.Count);
            Assert.AreEqual(0, MapObjectLayoutDistanceOrder.CountWithinRadius(sorted, 150f));
        }

        private static MapObjectLayoutMessagePack CreateLayout(int instanceId, float x, float y, float z)
        {
            return new MapObjectLayoutMessagePack(
                instanceId, "00000000-0000-0000-0000-000000000001", x, y, z,
                0f, 0f, 0f, 1f,
                1f, 1f, 1f,
                -1, 0f, 0f);
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `MapObjectLayoutDistanceOrder` が存在しないエラー

- [ ] **Step 3: 最小限の実装を書く**

```csharp
using System.Collections.Generic;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObjectのlayoutを基準点からの距離順に並べ、近傍境界の件数を算出する（ADR 0030）
    ///     Orders map object layouts by distance from an origin and counts the near-field boundary (ADR 0030)
    /// </summary>
    public static class MapObjectLayoutDistanceOrder
    {
        public static List<Entry> Sort(IReadOnlyList<MapObjectLayoutMessagePack> layouts, Vector3 origin)
        {
            // 79,000件規模でも一度きりのソートなので距離は前計算して焼き込む
            // Even at the 79,000 scale this sorts once, so distances are precomputed and baked in
            var entries = new List<Entry>(layouts.Count);
            foreach (var layout in layouts)
            {
                var sqrDistance = (new Vector3(layout.X, layout.Y, layout.Z) - origin).sqrMagnitude;
                entries.Add(new Entry(layout, sqrDistance));
            }

            entries.Sort(static (a, b) => a.SqrDistance.CompareTo(b.SqrDistance));
            return entries;
        }

        public static int CountWithinRadius(List<Entry> sortedEntries, float radius)
        {
            // ソート済み前提で先頭から数え、半径ちょうどは近傍に含める
            // Assumes sorted input; counts from the head, a distance exactly at the radius counts as near
            var sqrRadius = radius * radius;
            for (var index = 0; index < sortedEntries.Count; index++)
            {
                if (sqrRadius < sortedEntries[index].SqrDistance) return index;
            }

            return sortedEntries.Count;
        }

        /// <summary>
        ///     距離を焼き込んだソート用エントリ
        ///     A sort entry with its distance baked in
        /// </summary>
        public readonly struct Entry
        {
            public readonly MapObjectLayoutMessagePack Layout;
            public readonly float SqrDistance;

            public Entry(MapObjectLayoutMessagePack layout, float sqrDistance)
            {
                Layout = layout;
                SqrDistance = sqrDistance;
            }
        }
    }
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectLayoutDistanceOrderTest"`
Expected: 5件PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectLayoutDistanceOrder.cs* moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectLayoutDistanceOrderTest.cs*
git commit -m "feat(map-object): layoutの距離順ソートと近傍境界算出を追加"
```

---

### Task 4: `MapObjectLayoutInstantiator`（生成本体の抽出）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectLayoutInstantiator.cs`

**Interfaces:**
- Consumes: Task 2の `MapObjectPendingStateLedger`（`TryConsume`）、k-dブランチ既存の `MapObjectNearestSearcher`（`Register`）、`GetMapObjectInfoProtocol.MapObjectsInfoMessagePack`（namespace `Server.Protocol.PacketResponse`）
- Produces: `public sealed class MapObjectLayoutInstantiator` — ctor `(Transform parent, Dictionary<int, MapObjectGameObject> allMapObjects, Dictionary<int, GetMapObjectInfoProtocol.MapObjectsInfoMessagePack> snapshotByInstanceId, MapObjectNearestSearcher nearestSearcher, MapObjectPendingStateLedger pendingStateLedger)` ／ `public void InstantiateFromLayout(MapObjectLayoutMessagePack layout)`

**注意:** 生成ロジックは現行 `MapObjectGameObjectDatastore.Construct` 内のローカル関数群からの**移動**であり、コメント含め挙動を変えない（保留台帳の適用のみ新規）。このタスク時点ではdatastoreは旧実装のままでよい（新クラスは未使用でもコンパイルは通る）。

- [ ] **Step 1: 実装を書く**

```csharp
using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Core.Master;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     layout1件からmapObject個体を生成し、スナップショット・保留イベントの適用と索引登録まで担う
    ///     Instantiates one map object from a layout, applying its snapshot and pending events and registering it for search
    /// </summary>
    public sealed class MapObjectLayoutInstantiator
    {
        private readonly Transform _parent;
        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects;
        private readonly Dictionary<int, GetMapObjectInfoProtocol.MapObjectsInfoMessagePack> _snapshotByInstanceId;
        private readonly MapObjectNearestSearcher _nearestSearcher;
        private readonly MapObjectPendingStateLedger _pendingStateLedger;
        private readonly Dictionary<Guid, GameObject> _prefabCacheByMapObjectGuid = new();

        public MapObjectLayoutInstantiator(
            Transform parent,
            Dictionary<int, MapObjectGameObject> allMapObjects,
            Dictionary<int, GetMapObjectInfoProtocol.MapObjectsInfoMessagePack> snapshotByInstanceId,
            MapObjectNearestSearcher nearestSearcher,
            MapObjectPendingStateLedger pendingStateLedger)
        {
            _parent = parent;
            _allMapObjects = allMapObjects;
            _snapshotByInstanceId = snapshotByInstanceId;
            _nearestSearcher = nearestSearcher;
            _pendingStateLedger = pendingStateLedger;
        }

        public void InstantiateFromLayout(MapObjectLayoutMessagePack layout)
        {
            // guidは正常データ前提でparseする（不正guidはT8のデータ修正対象・ここでの防御は過剰）
            // Parse guid assuming valid data (malformed guids are a T8 data fix; defending here is overkill)
            var mapObjectGuid = new Guid(layout.MapObjectGuid);

            // master欠落・load失敗はResolvePrefabOrNull内でLogError済み。個体だけskipし残りは生成しきる
            // Master-missing or load-failure is already logged inside; skip just this one and keep generating the rest
            var prefab = ResolvePrefabOrNull(mapObjectGuid);
            if (prefab == null) return;

            // スナップショット欠落はInstantiate前に検出し、orphan instanceを作らずskipする
            // Detect a missing snapshot before Instantiate so no orphan instance is created, then skip
            if (!_snapshotByInstanceId.TryGetValue(layout.InstanceId, out var snapshot))
            {
                Debug.LogError($"MapObject snapshot missing. InstanceId:{layout.InstanceId} MapObjectGuid:{mapObjectGuid}");
                return;
            }

            // 生成時のRotation/Scaleを実インスタンスへ戻す。既定値のままだと全個体が同じ向きで直立し裸地も生成時サイズで広がる
            // Restore the generated rotation and scale; the defaults face every instance alike and spread bare ground at the generated size
            var rotation = new Quaternion(layout.RotationX, layout.RotationY, layout.RotationZ, layout.RotationW);
            var instance = Object.Instantiate(prefab, new Vector3(layout.X, layout.Y, layout.Z), rotation, _parent);
            instance.transform.localScale = new Vector3(layout.ScaleX, layout.ScaleY, layout.ScaleZ);

            // rootにMapObjectGameObjectが無いのはprefab authoring不正。生成物を破棄してskipする
            // Missing MapObjectGameObject on root is invalid prefab authoring; destroy the instance and skip
            var mapObject = instance.GetComponent<MapObjectGameObject>();
            if (mapObject == null)
            {
                Debug.LogError($"MapObject prefab has no MapObjectGameObject on root. MapObjectGuid:{mapObjectGuid}");
                Object.Destroy(instance);
                return;
            }

            // instanceId重複はTryAddで検出し、重複個体を破棄してskipする（Addのthrowは起動ハングを招くため不可）
            // Detect duplicate instanceId via TryAdd; destroy the duplicate and skip (Add's throw would hang startup)
            mapObject.SetRuntimeIdentity(layout.InstanceId, layout.MapObjectGuid);
            if (!_allMapObjects.TryAdd(layout.InstanceId, mapObject))
            {
                Debug.LogError($"MapObject duplicate InstanceId:{layout.InstanceId} MapObjectGuid:{mapObjectGuid}");
                Object.Destroy(instance);
                return;
            }

            // 登録後にスナップショットで初期状態（破壊/HP）を適用する
            // Apply the initial state (destroy/HP) from the snapshot after registration
            mapObject.Initialize(snapshot);

            // 生成前に届いた破壊/HPイベントをスナップショットより優先して適用する（ADR 0030）
            // Apply destroy/HP events that arrived before instantiation, overriding the snapshot (ADR 0030)
            if (_pendingStateLedger.TryConsume(layout.InstanceId, out var pendingState))
            {
                if (pendingState.HasHp) mapObject.UpdateHp(pendingState.Hp);
                if (pendingState.IsDestroyed && !mapObject.IsDestroyed) mapObject.DestroyMapObject();
            }

            // 最寄り探索の候補へ登録する（破壊済みは探索時の生存フィルタで除かれる）
            // Register as a nearest-search candidate (destroyed ones drop out at the live filter on search)
            _nearestSearcher.Register(mapObject);
        }

        private GameObject ResolvePrefabOrNull(Guid mapObjectGuid)
        {
            // 失敗もnullとしてキャッシュする。同一guidが千個規模で並ぶため同期loadとLogErrorはguidごと1回に抑える
            // Failures are cached as null too; a guid can repeat by the thousand so keep the sync load and LogError once per guid
            if (_prefabCacheByMapObjectGuid.TryGetValue(mapObjectGuid, out var cachedPrefab)) return cachedPrefab;

            // master欠落はLogError+nullでskipさせる（サーバMapObjectDatastoreと対称）
            // Master-missing returns null after LogError to skip (symmetric with server MapObjectDatastore)
            var element = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(mapObjectGuid);
            if (element == null)
            {
                Debug.LogError($"MapObject master missing. MapObjectGuid:{mapObjectGuid}");
                _prefabCacheByMapObjectGuid[mapObjectGuid] = null;
                return null;
            }

            // load失敗（有料アセット不在等）もLogError+nullでskipさせる
            // Load failure (e.g. missing paid asset) also returns null after LogError to skip
            var loaded = AddressableLoader.LoadDefault<GameObject>(element.AddressablePath);
            if (loaded == null)
            {
                Debug.LogError($"MapObject prefab load failed. MapObjectGuid:{mapObjectGuid} AddressablePath:{element.AddressablePath}");
                _prefabCacheByMapObjectGuid[mapObjectGuid] = null;
                return null;
            }

            _prefabCacheByMapObjectGuid[mapObjectGuid] = loaded;
            return loaded;
        }
    }
}
```

- [ ] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（新クラスは未使用でも可）

- [ ] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectLayoutInstantiator.cs*
git commit -m "refactor(map-object): 個体生成の本体をMapObjectLayoutInstantiatorへ抽出（保留イベント適用を含む）"
```

---

### Task 5: Datastoreの2段生成化と既存テスト更新

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs`（全面書き換え）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MapObjects/MapObjectRotationTest.cs:64`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MapObjects/MapObjectNearFieldStartupTest.cs`（新規）

**Interfaces:**
- Consumes: Task 1 `FrameTimeBudget`、Task 3 `MapObjectLayoutDistanceOrder`、Task 4 `MapObjectLayoutInstantiator`、Task 2 `MapObjectPendingStateLedger`
- Produces: `MapObjectGameObjectDatastore` に `public UniTask WaitForAllInstantiatedAsync()` を追加。`WaitForInitialApplyAsync()` の意味が「近傍のみ完了」に変わる（シグネチャ不変）

- [ ] **Step 1: Datastoreを書き換える（全文）**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Network.API;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObjectをLayout応答から実行時Instantiateし、破壊/HPの状態同期を担うデータストア
    ///     Instantiates map objects at runtime from the layout response and keeps their destroy/HP state synced
    /// </summary>
    public class MapObjectGameObjectDatastore : MonoBehaviour, IInitialEventApplyWaitTarget, ISkitWorldObjectControl
    {
        // 起動待機を解除する近傍の半径。残りはゲーム開始後に距離順で後着生成する（ADR 0030）
        // Radius of the near field that releases the startup wait; the rest streams in by distance after the game starts (ADR 0030)
        private const float NearFieldRadius = 150f;

        // ローディング中（近傍）とゲーム開始後（後着）のフレームあたり生成時間予算
        // Per-frame instantiation time budgets while loading (near field) and after the game starts (background)
        private const double NearFieldFrameBudgetMilliseconds = 16.0;
        private const double BackgroundFrameBudgetMilliseconds = 4.0;

        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects = new();
        private readonly MapObjectNearestSearcher _nearestSearcher = new();
        private readonly MapObjectPendingStateLedger _pendingStateLedger = new();

        // 近傍完了（起動待機の解除点）と全量完了を別々にawaitできる形で保持する
        // Retain near-field completion (the startup wait release) and full completion as separately awaitable tasks
        private UniTask? _initialApplyTask;
        private UniTask? _allInstantiatedTask;

        public UniTask WaitForInitialApplyAsync()
        {
            // 開始前の待機要求は順序バグ。既定値タスク（完了扱い）で素通りさせず失敗させる
            // Waiting before the start is an ordering bug; never let the default (completed) task slip through
            if (_initialApplyTask == null)
                throw new InvalidOperationException("[MapObjectGameObjectDatastore] Construct前に待機が要求されました");
            return _initialApplyTask.Value;
        }

        public UniTask WaitForAllInstantiatedAsync()
        {
            // 全量完了の正規待機API。全個体前提の検証・テストはこちらを待つ（ADR 0030）
            // The official wait for full instantiation; checks and tests that assume every object await this (ADR 0030)
            if (_allInstantiatedTask == null)
                throw new InvalidOperationException("[MapObjectGameObjectDatastore] Construct前に全量待機が要求されました");
            return _allInstantiatedTask.Value;
        }

        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // イベント購読は同期で確定させ、生成本体は近傍→後着の2段の保持タスクへ委譲する
            // Subscribe synchronously, then delegate instantiation to the two retained near-field → background tasks
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MapObjectUpdateEventPacket.EventTag, OnUpdateMapObject);

            // 破壊/HPの初期状態はva:mapObjectInfoスナップショットをinstanceIdで引く（Layoutと同一集合が前提）
            // Initial destroy/HP state comes from the va:mapObjectInfo snapshot keyed by instanceId (same set as the layout)
            var snapshotByInstanceId = handshakeResponse.MapObjects.ToDictionary(info => info.InstanceId);
            var instantiator = new MapObjectLayoutInstantiator(transform, _allMapObjects, snapshotByInstanceId, _nearestSearcher, _pendingStateLedger);
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            // 全layoutを一度だけPlayerPosからの距離順に並べ、近傍→遠方の順で生成する（ADR 0030）
            // Sort every layout once by distance from PlayerPos and instantiate near to far (ADR 0030)
            var sortedEntries = MapObjectLayoutDistanceOrder.Sort(handshakeResponse.MapLayout.MapObjects, handshakeResponse.PlayerPos);
            var nearFieldCount = MapObjectLayoutDistanceOrder.CountWithinRadius(sortedEntries, NearFieldRadius);

            _initialApplyTask = InstantiateRangeAsync(0, nearFieldCount, NearFieldFrameBudgetMilliseconds).Preserve();
            _allInstantiatedTask = InstantiateBackgroundAsync().Preserve();

            // 後着の失敗を誰もawaitしない起動経路でもConsoleへ出す
            // Surface background failures in the Console even when nothing on the startup path awaits them
            _allInstantiatedTask.Value.Forget();

            #region Internal

            async UniTask InstantiateBackgroundAsync()
            {
                // 近傍の完了（と失敗）を引き継いでから残り全量を後着させる
                // Take over near-field completion (and failure) before streaming in the remainder
                await _initialApplyTask.Value;
                await InstantiateRangeAsync(nearFieldCount, sortedEntries.Count, BackgroundFrameBudgetMilliseconds);
            }

            async UniTask InstantiateRangeAsync(int startIndex, int endIndexExclusive, double frameBudgetMilliseconds)
            {
                // 時間予算を使い切るまで同一フレームで生成し続け、超えたらフレームを跨ぐ（ADR 0030）
                // Keep instantiating within the frame until the time budget runs out, then cross a frame (ADR 0030)
                var budget = new FrameTimeBudget(frameBudgetMilliseconds);
                for (var index = startIndex; index < endIndexExclusive; index++)
                {
                    instantiator.InstantiateFromLayout(sortedEntries[index].Layout);

                    if (!budget.IsExhausted) continue;
                    await UniTask.Yield(cancellationToken);
                    budget.Restart();
                }
            }

            #endregion
        }

        private void OnUpdateMapObject(byte[] payLoad)
        {
            var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(payLoad);

            // 未生成宛は捨てず台帳へ保留し、後着生成時にスナップショットより優先して適用する（ADR 0030）
            // Events for not-yet-instantiated objects are held in the ledger and override the snapshot at late instantiation (ADR 0030)
            if (!_allMapObjects.TryGetValue(data.InstanceId, out var mapObject))
            {
                switch (data.EventType)
                {
                    case MapObjectUpdateEventMessagePack.DestroyEventType:
                        _pendingStateLedger.RecordDestroy(data.InstanceId);
                        break;
                    case MapObjectUpdateEventMessagePack.HpUpdateEventType:
                        _pendingStateLedger.RecordHp(data.InstanceId, data.CurrentHp);
                        break;
                    default:
                        throw new Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
                }

                return;
            }

            switch (data.EventType)
            {
                case MapObjectUpdateEventMessagePack.DestroyEventType:
                    mapObject.DestroyMapObject();
                    // 破壊は索引へ即時反映せず、次の探索で該当guidだけ再構築する
                    // Destruction isn't applied to the index immediately; the next search rebuilds just this guid
                    _nearestSearcher.MarkDirty(mapObject.MapObjectGuid);
                    break;
                case MapObjectUpdateEventMessagePack.HpUpdateEventType:
                    mapObject.UpdateHp(data.CurrentHp);
                    break;
                default:
                    throw new Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
            }
        }

        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }

        public MapObjectGameObject SearchNearestMapObject(Guid mapObjectGuid, Vector3 position)
        {
            return _nearestSearcher.SearchNearest(mapObjectGuid, position);
        }
    }
}
```

書き換え後に `wc -l` で200行以下を確認する（超えたらコメントの冗長箇所を削るのではなく、`OnUpdateMapObject` の台帳分岐を `MapObjectPendingStateLedger` 側の `Record(MapObjectUpdateEventMessagePack)` へ寄せて縮める）。

- [ ] **Step 2: `MapObjectRotationTest` を全量待機へ切り替える**

`MapObjectRotationTest.cs` の以下を置換:

```csharp
                // 初期化と同じawait経路を通し、生成が終わってから姿勢とスケールを見る
                // Use the same await path as startup so the facings and scales are read after instantiation finishes
                await datastore.WaitForInitialApplyAsync();
```

↓

```csharp
                // 任意の（遠方も含む）個体を突き合わせるため、近傍待機ではなく全量生成の完了を待つ（ADR 0030）
                // Wait for full instantiation, not the near-field gate, since far objects are matched too (ADR 0030)
                await datastore.WaitForAllInstantiatedAsync();
```

- [ ] **Step 3: 近傍先行の実機テストを書く**

`MapObjectNearFieldStartupTest.cs`（`MapObjectRotationTest` と同じboot型。`EnterPlayModeUtil()`→`EnterPlayMode`→`LogAssert.ignoreFailingMessages = true`→`Body().ToCoroutine()`→`ExitPlayMode`→`SessionState.SetBool("DebugObjectsBootstrap_Disabled", false)` の枠組みをそのまま使う）:

```csharp
using System;
using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest.MapObjects
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// 近傍待機の解除時点でPlayerPosから150m以内のmapObjectが全て生成済みであることを実機検証する。
    /// This test runs in EditMode but switches to PlayMode during execution.
    /// Verifies every map object within 150m of PlayerPos already exists when the near-field wait releases.
    /// </summary>
    public class MapObjectNearFieldStartupTest
    {
        // datastore側のNearFieldRadius=150fと同じ値。定数公開はテスト専用publicになるため値を重ねる
        // Mirrors the datastore's NearFieldRadius=150f; exposing the constant would be a test-only public
        private const float NearFieldRadius = 150f;

        [UnityTest]
        public IEnumerator NearFieldMapObjectsExistWhenInitialApplyCompletes()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                var datastore = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>(FindObjectsInactive.Include);
                Assert.IsNotNull(datastore, "MapObjectGameObjectDatastore was not found in scene");

                // 近傍待機（起動と同じ解除点）の直後に近傍layout全件の生存を突き合わせる
                // Right after the near-field wait (the same release point as startup), match every near layout
                await datastore.WaitForInitialApplyAsync();

                var handshake = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>();
                var playerPos = handshake.PlayerPos;
                var checkedCount = 0;

                foreach (var layout in handshake.MapLayout.MapObjects)
                {
                    var position = new Vector3(layout.X, layout.Y, layout.Z);
                    if (NearFieldRadius * NearFieldRadius < (position - playerPos).sqrMagnitude) continue;

                    // 破壊済みは探索から外れるため、位置一致の最近傍が本人であることまでは求めず存在のみ確かめる
                    // A destroyed one drops out of search, so only existence is asserted, not identity of the nearest hit
                    var found = datastore.SearchNearestMapObject(new Guid(layout.MapObjectGuid), position);
                    Assert.IsNotNull(found, $"near-field map object {layout.InstanceId} was not instantiated before the initial-apply wait released");
                    checkedCount++;
                }

                // 近傍0件のワールドでは検証が素通りしてしまうので先に落とす
                // With zero near objects every assertion would pass vacuously, so fail here first
                Assert.Greater(checkedCount, 0, "test world has no map objects within the near field");

                // 全量待機も正規APIとして完走することを固定する
                // Also pin that the full-instantiation wait, the official API, runs to completion
                await datastore.WaitForAllInstantiatedAsync();
            }

            #endregion
        }
    }
}
```

（注: テストワールドに初期破壊済み個体がある場合 `SearchNearestMapObject` がnullを返しうる。落ちたら破壊済みlayoutをスキップする条件を足す — スナップショットは `handshake.MapObjects` の `IsDestroyed` で引ける）

- [ ] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectRotationTest|MapObjectNearFieldStartupTest"`
Expected: 2件PASS（PlayMode遷移テストなので実行後のドメインリロードエラーは45秒待ってリトライ）

- [ ] **Step 6: 既存の待機境界・mapObject系テストの回帰を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "InitialEventApplyWaiterTest|MapObjectNearestSearcherTest|MiningAimTest|MapObjectHpBarScaleTest"`
Expected: 全PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MapObjects/
git commit -m "feat(map-object): 起動待機を近傍150mに限定し残りを距離順・時間予算で後着生成する (ADR 0030)"
```

---

### Task 6: `MapObjectGameObject.Initialize` 走査の計測と条件付き是正

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs`（是正時のみ）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectRayTarget.cs`（是正時のみ）

**Interfaces:**
- Consumes: Task 5完了後のdatastore
- Produces: 是正時 `MapObjectRayTarget.MapObjectGameObject` はlazy解決（`GetComponentInParent`を初回参照時に1回）になる。`Initialize(MapObjectGameObject)` は露頭経路（`OutcropMiningAimTest.cs:112` 参照）のため残す

- [ ] **Step 1: 計測用の一時コードを入れて測る（コミットしない）**

`MapObjectLayoutInstantiator.InstantiateFromLayout` 内の `mapObject.Initialize(snapshot)` を一時的に挟み込みで計測する:

```csharp
// ここから計測用（コミット禁止） / measurement only, never commit
_initializeStopwatch.Start();
mapObject.Initialize(snapshot);
_initializeStopwatch.Stop();
_initializedCount++;
if (_initializedCount % 500 == 0)
    Debug.Log($"[Measure] Initialize avg: {_initializeStopwatch.Elapsed.TotalMilliseconds / _initializedCount * 1000.0:F1}us over {_initializedCount}");
// ここまで計測用 / end measurement
```

（フィールド `private readonly System.Diagnostics.Stopwatch _initializeStopwatch = new(); private int _initializedCount;` も一時追加）

同様に `InstantiateFromLayout` 全体を測る第2のStopwatchを入れ、`uloop run-tests ... --filter-value "MapObjectRotationTest"` を1回実行して `uloop get-logs --project-path ./moorestech_client --log-type Log` の `[Measure]` 行から **Initialize比率（Initialize平均µs ÷ 全体平均µs）** を読む。

- [ ] **Step 2: 判定して記録する**

- 比率が30%以下 → 一時コードを `git checkout -- <file>` で戻し、このタスクはここで完了。計測値をこのplanのチェックボックス横に追記する
- 比率が30%超 → Step 3へ

- [ ] **Step 3: （支配的な場合のみ）lazy解決へ是正する**

`MapObjectRayTarget.cs` を以下へ変更:

```csharp
using Client.Game.InGame.Mining;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    public class MapObjectRayTarget : MonoBehaviour, IMiningRayTarget
    {
        private MapObjectGameObject _mapObjectGameObject;

        public MapObjectGameObject MapObjectGameObject
        {
            get
            {
                // 生成時の全子走査をやめ、初回参照時に親から1回だけ解決する（露頭経路はInitializeが先に確定させる）
                // Skip the spawn-time child scan; resolve from the parent once on first access (the outcrop path sets it via Initialize first)
                if (_mapObjectGameObject == null) _mapObjectGameObject = GetComponentInParent<MapObjectGameObject>();
                return _mapObjectGameObject;
            }
        }

        public IMiningTargetObject MiningTargetObject => MapObjectGameObject;

        public void Initialize(MapObjectGameObject mapObjectGameObject)
        {
            _mapObjectGameObject = mapObjectGameObject;
        }
    }
}
```

`MapObjectGameObject.Initialize` から以下の走査を削除する:

```csharp
            var rayTargets = GetComponentsInChildren<MapObjectRayTarget>();
            foreach (var rayTarget in rayTargets)
            {
                rayTarget.Initialize(this);
            }
```

- [ ] **Step 4: （是正した場合のみ）コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MiningAimTest|MiningEquipmentSwitchTest|OutcropMiningAimTest|MapObjectRayTargetTest"`
Expected: 全PASS

- [ ] **Step 5: （是正した場合のみ）コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectRayTarget.cs
git commit -m "perf(map-object): RayTargetの所有者解決をlazy化しInitializeの全子走査を削除"
```

---

### Task 7: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1: moores-code-review スキルを起動し、`feature/nearest-search-kd-tree` との差分（本ブランチの全コミット）を対象にレビューを完了する**
- [ ] **Step 2: 指摘対応後、全変更がコミット済みであることを `git status` で確認する**

---

## 判断記録（ADR）

設計セッションの裁定: `docs/adr/0030-mapobject-near-field-first-instantiation.md` ／ `.decisions/2026-08-23-mapObject起動生成は近傍先行と時間予算分散にする.md`（近傍=PlayerPos中心150m・距離順後着・16ms/4ms予算・保留台帳・全量待機API・k-dブランチ起点・露頭対象外・Initialize計測ゲート・Stopwatch規約適用外は全てユーザー裁定 2026-08-23）

planning中に新たに生じた判断:

- **生成本体を `MapObjectLayoutInstantiator` へ抽出**（出所: agent前提・200行規約） — 2段化＋台帳適用を旧datastoreに足すと200行を超える。`MapObjectNearestSearcher` と同型のplain collaborator分割が前例
- **近傍/後着を「近傍タスク→awaitして続きを走らせる後続タスク」の直列2タスクで表現**（出所: agent前提） — 単一ループ内でCompletionSourceを完了させる形は、ループ例外時に近傍待機が永久にPendingになる穴をtry-catch（規約禁止）なしに塞げない。直列2タスクなら近傍例外は両待機へ自然に伝播する
- **後着タスクに `.Forget()` を併用**（出所: agent前提） — プロダクション起動経路では誰も全量待機をawaitしないため、後着中の例外をConsoleへ出す観測点が必要。`Preserve()` 済みタスクへのForgetは待機側と両立する
- **近傍境界の件数算出・距離ソートを `MapObjectLayoutDistanceOrder`（静的クラス）へ分離**（出所: agent前提） — EditModeInPlayingでしか検証できないdatastoreから境界値ロジックを外し、単体テスト可能にする
- **`FrameTimeBudget` はMap/MapObject配下にローカル配置**（出所: agent前提・YAGNI） — 汎用機構だが現利用者は1箇所。共有層昇格は第2利用者が出た時
- **近傍実機テストは「近傍layout全件の存在」のみを検証**（出所: agent前提） — 「遠方が未生成であること」の検証は後着進行とのレースで不安定になるため張らない。境界の正確性は単体テスト側が担う
- **Initialize計測の支配判定は30%閾値・計測コードはコミットしない**（出所: agent前提。裁定「計測して支配的なら直す」の具体化）
- **是正時のlazy解決は露頭経路の `Initialize` を温存**（出所: agent前提） — `OutcropMiningAimTest` が明示Initializeで所有者を注入しており、lazyは未注入時のフォールバックに限定する

**申し送り（本planのスコープ外・bd登録済み）**: k-d treeセッションへ「後着生成中は探索中guidが毎フレーム再構築（最大7,000点・概算1〜2ms）になる。許容かdirty間引きかの確認と、plan/ADRの『点集合は起動後静的・追加なし』前提文の更新」
