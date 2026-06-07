# クリーンルーム フェーズ2（純度シミュレーション）実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 asmdef を認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」で失敗したら uloop で Unity 再起動してから再試行。「Domain Reload in progress」なら45秒待って再試行。
> - blockType／マスタスキーマ追加・SourceGenerator の手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル編集時は AGENTS.md の「文字化け防止ワークフロー」を順守。`.cs` は UTF-16 LE BOM と UTF-8 系が混在する。編集前にエンコーディングを確認し、編集後に同じエンコーディングへ戻す。`git diff` に `縺`/`繧`/`繝` 等の化け文字が連続したら破棄してやり直す。
> - **APIシグネチャ確認の原則:** 本プランのコードは既存コードベースのパターンから書いているが、メソッド名・名前空間・引数順は推定を含む。各 `.cs` を書く前に、本文で「確認」と指示した既存ファイルを開いて実シグネチャに合わせること。コンパイル/テストのチェックポイントが安全網。

**Goal:** フェーズ1で検出した各クリーンルームに「純度の継続状態」を持たせ、`dN/dt = A_total − n·q·C` を tick 積分して平衡濃度 `C_eq = A_total/(n·q)` に収束させる。二条件（濃度閾値 `C ≤ classThreshold` ＋ 換気回数 `ACH = n·q/V ≥ requiredAirChangeRate`）にヒステリシスを足してクラス A〜D/Out を決定し、Valid/Degraded/Invalid＋猶予（5.0秒）を運用する。再検出（リーク・結合・分割）をまたいで状態をセル重なりで引き継ぎ、セーブ/ロードで永続化する。汚染源 `A_total` と清浄機 `n·q` はこのフェーズでは**注入インターフェース**のみ用意し、実数供給はフェーズ3。

**Architecture:** 新しい DI singleton `CleanRoomPurityService`（eager）が、純度の継続状態 `CleanRoomPurityState`（N・派生C・クラス・段階・猶予タイマ・Cells スナップショット）を部屋同一性キー（`Cells` 重なり）で保持する。サービスは `GameUpdater.UpdateObservable`（UniRx `IObservable<Unit>`、20/秒・50ms）を購読して毎tick `dN = (A_total − n·q·(N/V)) · SecondsPerTick` を積分する。汚染源 `A_total` は per-room クエリ `CleanRoomPollutionInput.GetPollutionPerSecond(CleanRoom)`、清浄機 `n·q` は `ICleanRoomPurificationSource`（代表占有セルを公開し、サービスが `TryGetRoomAt` で部屋に振り分ける）から取る。**フェーズ1の `CleanRoomDetectionSystem` を本フェーズで一点改修し、`RebuildAll()` 完了時に再検出イベントを発火させる**。サービスはこれを購読し、旧状態を新部屋へ Cells 重なりで引き継ぐ（マッチ→N継続、結合→N合算、分割→濃度按分 N=C·V_new、消滅→Degraded化して猶予開始）。永続化は鉄道 `RailGraphSaveLoadService` を前例に、`CleanRoomSaveLoadService.GetSaveData/Restore` ＋ `WorldSaveAllInfoV1`/`AssembleSaveJsonText`/`WorldLoaderFromJson` の3点改修。ロード復元は **`LoadBlockDataList` → `RebuildAll()` → `Restore()`** の順で「ブロック生成・部屋検出の後」に置く。

**Tech Stack:** C# (Unity, moorestech_server), UniRx `IObservable<Unit>`（`GameUpdater.UpdateObservable` / テストは `GameUpdater.RunFrames(uint)`）, NUnit (Server.Tests), Newtonsoft.Json（セーブ）, Mooresmaster Source Generator（`cleanRoomClasses.yml` → クラス閾値マスタ）。

**数値ソース:** 全ての数値（クラス閾値・ヒステリシス0.8倍・必要換気回数・猶予5.0秒・汚染係数・worked example）は `docs/superpowers/plans/2026-06-06-cleanroom-balance-parameters.md` を唯一のソースとする。テストの期待値はそこの worked example（V=75 → A_total=16 → C_eq=3.2 → クラスA）で固定する。

---

## 後続プランのロードマップ（このプランの対象外）

| フェーズ | 内容 | 主産物 |
|---|---|---|
| 1（完了） | 境界ブロック＋3D密閉部屋検出＋部屋レジストリ/クエリ | 壁で囲うと部屋検出、壊すと無効化 |
| **2（本プラン）** | 純度シミュ（N/V/S、A_total注入、清浄機 n·q·C 除去、平衡、クラス閾値＋ACH、ヒステリシス、Valid/Degraded/Invalid＋猶予）＋再検出時の同一性引き継ぎ＋永続化 | 部屋にクラスが付き、汚染/清浄に応答し、再検出・セーブをまたいで継続 |
| 3 | 空気清浄機ブロック＋フィルター仕事量消費＋電力＋汚染源4種。注入インターフェースに実数を供給 | 維持ループが回る |
| 4 | 製造機統合（最大グレード天井・down-bin・Invalid停止＋猶予） | 半導体生産が部屋クラスに依存 |
| 5 | I/Oブロック挙動（ハッチ/コネクタ/ドア）＋I/O固有セーブ | 完全な遊べる形 |

---

## 本プランの前提（フェーズ1の確定API）

本プランは `docs/superpowers/plans/2026-06-05-cleanroom-phase1-detection.md` で実装済みの次のAPIに載る。**書く前に各実ファイルを開いてシグネチャを確認すること。**

- `Game.CleanRoom.CleanRoom`（`int Id` / `IReadOnlyCollection<Vector3Int> Cells` / `int Volume`(=Cells数) / `int SurfaceArea` / `bool IsValid` / `bool Contains(Vector3Int)`）
- `Game.CleanRoom.CleanRoomDetectionSystem`（`IReadOnlyList<CleanRoom> Rooms` / `void RebuildAll()` / `bool TryGetRoomAt(Vector3Int, out CleanRoom)` / `bool TryGetRoomContainingBlock(IBlock, out CleanRoom)`、DI singleton・eager）
- `Game.Block.Interface.Component.ICleanRoomBoundaryComponent`（`CleanRoomBoundaryKind BoundaryKind`）、enum `CleanRoomBoundaryKind { Wall, Door, ItemHatch, PipeConnector }`
- テスト mod の境界ブロックID `Tests.Module.TestMod.ForUnitTestModBlockId.CleanRoomWall`/`CleanRoomDoor`/`CleanRoomItemHatch`/`CleanRoomPipeConnector` と `BuildWallShell(world, min, max)` ヘルパ（フェーズ1テストに実装済み）

> **⚠ フェーズ1にrooms-rebuilt通知は無い（本プランで追加する）。** コードマップ §1.1 は `CleanRoomPurityService.OnRoomsRebuilt` を「再検出イベント購読」と書くが、フェーズ1の `CleanRoomDetectionSystem` はイベントを公開していない。本プラン **Task 6** で `CleanRoomDetectionSystem` に `RebuildAll()` 完了通知（UniRx `Subject<IReadOnlyList<CleanRoom>>`）を一点追加する。これはフェーズ1ファイルへの後方改修であり、フェーズ1の既存テストは壊さない（純粋な追加）。

---

## File Structure（フェーズ2で作成/変更するファイル）

**マスタ（クラス閾値）**
- Modify: `VanillaSchema/cleanRoomClasses.yml`（新規スキーマファイル）— クラスごとの濃度閾値・最大グレード・down-bin率・必要換気回数
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ
- Create: `moorestech_server/Assets/Scripts/Core.Master/CleanRoomClassMasterAccessor`（`MasterHolder` 経由のアクセサ）

> マスタ JSON は本フェーズのバランスを**ハードコード定数**で持つ方針でも成立する（Task 1 参照）。`cleanRoomClasses.yml` でのマスタ化は将来のチューニング容易化のためのオプションで、テストの数値固定にはどちらでも良い。**本プランは「閾値テーブルを `CleanRoomClass.cs` 内の定数テーブルで持つ」最小実装を採り、マスタ化はフェーズ3以降の任意拡張とする**（コードマップ §6「より良い動作を優先、調整は後から」に整合）。

**純度（新規 Game.CleanRoom/Purity）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomClass.cs` — クラス列挙＋判定純関数（閾値テーブル・ACH・ヒステリシス）
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomRoomStatus.cs` — Valid/Degraded/Invalid 列挙
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityState.cs` — 部屋ごとの継続純度状態
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPollutionInput.cs` — `A_total` 注入（per-room クエリ。フェーズ3が実装供給）
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/ICleanRoomPurificationSource.cs` — `n·q` 注入（清浄機が実装）
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityService.cs` — DI singleton, tick 更新＋同一性引き継ぎ

**再検出通知（フェーズ1ファイルへの一点改修）**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs` — `RebuildAll()` 完了時に発火する `OnRoomsRebuilt` を追加

**セーブ（新規 Game.CleanRoom/SaveLoad ＋ 3点改修）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/SaveLoad/CleanRoomPuritySaveData.cs` — 保存レコード
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/SaveLoad/CleanRoomSaveLoadService.cs` — GetSaveData / Restore（セル重なり照合）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`

**DI / 参照**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` — `CleanRoomPurityService` / `CleanRoomSaveLoadService` を登録＋eager
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`（コンパイルで判明したら `Game.CleanRoom` 参照を追加）

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurityPersistenceTest.cs`

> 各新規 `.cs` は Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## Task 1: クラス列挙とクラス判定純関数（閾値＋二条件＋ヒステリシス）

純度の最小ピースから始める。クラス列挙と「濃度C・換気ACH・現クラスから次クラスを決める」純関数を作る。数値は balance-parameters §1 / §1.1 をハードコード。tick もサービスも要らないので最初に固定できる。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomClass.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`（新規作成）

### 仕様（balance-parameters §1 / §1.1 より確定）

| クラス | 濃度上限 `classThreshold`（個/m³） | 必要換気 `requiredAirChangeRate`（1/秒） |
|---|---|---|
| A | ≤ 10 | 0.017 |
| B | ≤ 50 | 0.0083 |
| C | ≤ 200 | 0.0042 |
| D | ≤ 1000 | 0.0014 |
| Out | > 1000 | — |

- **クラス成立の二条件:** `C ≤ classThreshold(cls)` **かつ** `ACH ≥ requiredAirChangeRate(cls)` を満たす**最良**（最も清浄な）クラス。濃度が足りても換気が足りなければ降格。
- **ヒステリシス（クラス揺れ防止）:** `classThreshold` は「悪化（降格）方向の閾値」。改善（昇格）方向は **0.8倍**で別に持つ。例: B→A 昇格は C ≤ 8（10×0.8）で発火、A→B 降格は C > 10 で発火、間の 8〜10 は現状維持。判定には**現クラス**を引数で渡し、上げ/下げで別閾値を使う。

- [ ] **Step 1: 失敗テストを書く（昇格はマージン込み、降格は素閾値、ACH不足で降格）**

`Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs` を新規作成:

```csharp
using Game.CleanRoom.Purity;
using NUnit.Framework;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPuritySimulationTest
    {
        // ACHは全クラス要求を満たす十分大きい値で固定し、濃度のみで判定を見るヘルパ。
        // Use an ACH large enough for all classes so only concentration drives the decision.
        private const double AchAllPass = 1.0;

        [Test]
        public void Decide_FromClassA_StaysA_UntilExceedsThreshold()
        {
            // 現クラスA・C=9.5（8〜10の現状維持帯）→ A維持。
            // Current A, C=9.5 (8..10 hold band) -> stays A.
            Assert.AreEqual(CleanRoomClass.A,
                CleanRoomClassDecider.Decide(CleanRoomClass.A, concentration: 9.5, airChangeRate: AchAllPass));

            // C=11 で閾値10超え → Bへ降格。
            // C=11 exceeds 10 -> demote to B.
            Assert.AreEqual(CleanRoomClass.B,
                CleanRoomClassDecider.Decide(CleanRoomClass.A, concentration: 11.0, airChangeRate: AchAllPass));
        }

        [Test]
        public void Decide_FromClassB_PromotesToA_OnlyBelowMargin()
        {
            // C=9（10×0.8=8 を上回る）→ まだ昇格しない（B維持）。
            // C=9 (above 8) -> not yet promoted, stays B.
            Assert.AreEqual(CleanRoomClass.B,
                CleanRoomClassDecider.Decide(CleanRoomClass.B, concentration: 9.0, airChangeRate: AchAllPass));

            // C=7（8以下）→ Aへ昇格。
            // C=7 (<= 8) -> promote to A.
            Assert.AreEqual(CleanRoomClass.A,
                CleanRoomClassDecider.Decide(CleanRoomClass.B, concentration: 7.0, airChangeRate: AchAllPass));
        }

        [Test]
        public void Decide_AchShortfall_DemotesEvenIfConcentrationClean()
        {
            // C=3.2（A域）だが ACH=0.01 < 0.017（A要求未達）→ Aは成立しない。
            // Concentration is A-grade but ACH below A requirement -> A cannot hold.
            var cls = CleanRoomClassDecider.Decide(CleanRoomClass.A, concentration: 3.2, airChangeRate: 0.01);
            Assert.AreNotEqual(CleanRoomClass.A, cls);
            // ACH=0.01 は B要求0.0083以上・C濃度はB域(<=8昇格境界)なのでBに収まる。
            // ACH=0.01 meets B's 0.0083 and concentration is clean -> settles at B.
            Assert.AreEqual(CleanRoomClass.B, cls);
        }

        [Test]
        public void Decide_VeryDirty_IsOut()
        {
            // C=2000 > 1000 → Out。
            Assert.AreEqual(CleanRoomClass.Out,
                CleanRoomClassDecider.Decide(CleanRoomClass.A, concentration: 2000.0, airChangeRate: AchAllPass));
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Decide_"`
Expected: FAIL（`CleanRoomClass`／`CleanRoomClassDecider` 未定義でコンパイル不可）。新規 asmdef でないので Unity 再起動は不要のはず。

- [ ] **Step 3: クラス列挙＋判定純関数を実装**

`Game.CleanRoom/Purity/CleanRoomClass.cs`:

```csharp
namespace Game.CleanRoom.Purity
{
    // クリーンルームのクラス。清浄なほど上位（A が最良、Out は不成立）。
    // Clean room class; cleaner is higher (A is best, Out means failed).
    public enum CleanRoomClass
    {
        A,
        B,
        C,
        D,
        Out,
    }

    // クラス判定の純関数群。balance-parameters §1 / §1.1 の数値をハードコードする。
    // Pure class-decision helpers; numbers hardcoded from balance-parameters §1 / §1.1.
    public static class CleanRoomClassDecider
    {
        // ヒステリシスの改善側マージン。昇格は閾値×0.8 を下回って初めて発火。
        // Hysteresis margin for improvement; promotion fires only below threshold × 0.8.
        public const double PromoteMargin = 0.8;

        // 降格方向の濃度閾値（個/m³）。A→Out の順で清浄度が下がる。
        // Demotion-side concentration thresholds (per m^3), from A (cleanest) downward.
        private static readonly CleanRoomClass[] Ladder = { CleanRoomClass.A, CleanRoomClass.B, CleanRoomClass.C, CleanRoomClass.D };

        public static double ConcentrationThreshold(CleanRoomClass cls)
        {
            // 各クラスの濃度上限。Out には上限が無い。
            // Concentration ceiling per class; Out has none.
            return cls switch
            {
                CleanRoomClass.A => 10.0,
                CleanRoomClass.B => 50.0,
                CleanRoomClass.C => 200.0,
                CleanRoomClass.D => 1000.0,
                _ => double.PositiveInfinity,
            };
        }

        public static double RequiredAirChangeRate(CleanRoomClass cls)
        {
            // クラスごとの必要換気回数（1/秒）。
            // Required air change rate per class (1/sec).
            return cls switch
            {
                CleanRoomClass.A => 0.017,
                CleanRoomClass.B => 0.0083,
                CleanRoomClass.C => 0.0042,
                CleanRoomClass.D => 0.0014,
                _ => 0.0,
            };
        }

        // 現クラス・濃度C・換気ACH から次クラスを決める。
        // 昇格は閾値×0.8 を下回って初めて、降格は素の閾値を超えたら、二条件を満たす最良クラスへ。
        // Decide next class from current class, concentration C, and ACH.
        // Promotion uses threshold×0.8, demotion uses raw threshold; pick the best class meeting both conditions.
        public static CleanRoomClass Decide(CleanRoomClass current, double concentration, double airChangeRate)
        {
            // 二条件（濃度＋換気）を満たし得る最良クラスを、ヒステリシス込みで選ぶ。
            // Pick the cleanest class that satisfies both conditions, with hysteresis.
            foreach (var candidate in Ladder)
            {
                var rawThreshold = ConcentrationThreshold(candidate);
                // 昇格（現クラスより上位を狙う）はマージンを掛け、それ以外は素の閾値。
                // Tighten the threshold when aiming above the current class (promotion).
                var isImprovement = candidate < current;
                var concentrationLimit = isImprovement ? rawThreshold * PromoteMargin : rawThreshold;

                var concentrationOk = concentration <= concentrationLimit;
                var achOk = airChangeRate >= RequiredAirChangeRate(candidate);
                if (concentrationOk && achOk) return candidate;
            }

            return CleanRoomClass.Out;
        }
    }
}
```

> `Game.CleanRoom/Purity/` ディレクトリが無ければ作る。`Game.CleanRoom.asmdef` はフェーズ1で作成済み（参照に `UnityEngine`/`Game.Block.Interface`/`Game.World.Interface`/`Game.Context`/`Core.Update` を含む）。本ファイルは追加参照不要。

- [ ] **Step 4: 実行して緑を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Decide_"`
Expected: 全 PASS。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomClass.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): クラス列挙と二条件＋ヒステリシスの判定純関数を追加"
```

---

## Task 2: CleanRoomPurityState（N加減・0クランプ・濃度・Cells スナップショット）

部屋ごとの継続純度状態。N の加減（0クランプ）、`Concentration(V)=N/V`、クラス・段階・猶予の保持と単純な状態セッターを実装する。再検出・ロードをまたいで生き残るのでブロックには載せない。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomRoomStatus.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityState.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`

- [ ] **Step 1: 失敗テストを書く（N加減・0クランプ・濃度）**

`CleanRoomPuritySimulationTest.cs` に追加（`using System.Collections.Generic; using UnityEngine;` を先頭に足す）:

```csharp
        [Test]
        public void State_AddRemoveImpurity_ClampsAtZero()
        {
            var cells = new HashSet<Vector3Int> { new Vector3Int(0, 0, 0) };
            var state = new CleanRoomPurityState(cells);

            state.AddImpurity(100.0);
            Assert.AreEqual(100.0, state.ImpurityCount, 1e-9);

            state.RemoveImpurity(30.0);
            Assert.AreEqual(70.0, state.ImpurityCount, 1e-9);

            // 過剰除去は 0 でクランプ（負にならない）。
            // Over-removal clamps at zero (never negative).
            state.RemoveImpurity(1000.0);
            Assert.AreEqual(0.0, state.ImpurityCount, 1e-9);
        }

        [Test]
        public void State_Concentration_IsImpurityOverVolume()
        {
            var cells = new HashSet<Vector3Int> { new Vector3Int(0, 0, 0) };
            var state = new CleanRoomPurityState(cells);
            state.AddImpurity(240.0);

            // C = N / V = 240 / 75 = 3.2。
            Assert.AreEqual(3.2, state.Concentration(75.0), 1e-9);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.State_"`
Expected: FAIL（`CleanRoomPurityState`／`CleanRoomRoomStatus` 未定義）。

- [ ] **Step 3: 段階列挙と状態クラスを実装**

`Game.CleanRoom/Purity/CleanRoomRoomStatus.cs`:

```csharp
namespace Game.CleanRoom.Purity
{
    // 部屋の運用段階。猶予で flicker を吸収する。
    // Operational status of a room; grace absorbs flicker.
    public enum CleanRoomRoomStatus
    {
        Valid,
        Degraded,
        Invalid,
    }
}
```

`Game.CleanRoom/Purity/CleanRoomPurityState.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Game.CleanRoom.Purity
{
    // 部屋ごとの継続純度状態。再検出・ロードをまたいで生き残る（ブロックには載せない）。
    // Continuous purity state per room; survives re-detection and load (not stored on blocks).
    public class CleanRoomPurityState
    {
        public double ImpurityCount { get; private set; }              // N（個）
        public CleanRoomClass CurrentClass { get; private set; }
        public CleanRoomRoomStatus Status { get; private set; }
        public float GraceRemainingSeconds { get; private set; }

        // 同一性照合用の Cells スナップショット。再検出のたびに最新へ差し替える。
        // Cell snapshot for identity matching; replaced with the latest on each re-detection.
        public IReadOnlyCollection<Vector3Int> Cells => _cells;
        private HashSet<Vector3Int> _cells;

        public CleanRoomPurityState(HashSet<Vector3Int> cells)
        {
            _cells = cells;
            CurrentClass = CleanRoomClass.Out;
            Status = CleanRoomRoomStatus.Valid;
        }

        // A_total 由来の不純物を加算。
        // Add impurity from A_total.
        public void AddImpurity(double delta)
        {
            ImpurityCount += delta;
        }

        // n·q·C 由来の除去。0 でクランプ。
        // Remove impurity from n·q·C; clamp at zero.
        public void RemoveImpurity(double removed)
        {
            ImpurityCount -= removed;
            if (ImpurityCount < 0.0) ImpurityCount = 0.0;
        }

        // C = N / V。
        public double Concentration(double volume)
        {
            if (volume <= 0.0) return 0.0;
            return ImpurityCount / volume;
        }

        public void SetImpurityCount(double count)
        {
            ImpurityCount = count < 0.0 ? 0.0 : count;
        }

        public void SetCurrentClass(CleanRoomClass cls)
        {
            CurrentClass = cls;
        }

        public void SetStatus(CleanRoomRoomStatus status)
        {
            Status = status;
        }

        public void SetGraceRemainingSeconds(float seconds)
        {
            GraceRemainingSeconds = seconds < 0f ? 0f : seconds;
        }

        public void SetCells(HashSet<Vector3Int> cells)
        {
            _cells = cells;
        }
    }
}
```

> 単純な値の Set は `SetXxx` メソッドで行う（AGENTS.md「単純な getter/setter プロパティ禁止」）。`get; private set;` の公開読み取りは許容。

- [ ] **Step 4: 実行して緑を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.State_"`
Expected: 全 PASS。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomRoomStatus.cs moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityState.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): 純度継続状態CleanRoomPurityStateを追加"
```

---

## Task 3: 注入インターフェース（A_total per-room ＋ n·q source）

`A_total` と `n·q` をサービスへ注入する境界を定義する。**A_total は per-room クエリ**（部屋ごとに違う値）、清浄機は**代表占有セルを公開**してサービスが `TryGetRoomAt` で部屋へ振り分ける。フェーズ2では中身を持たないスタブ実装でテストし、実数供給はフェーズ3。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPollutionInput.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/ICleanRoomPurificationSource.cs`

このタスクはインターフェース定義のみ（テストは Task 5 の平衡テストで一括検証）。

- [ ] **Step 1: A_total 注入インターフェースを実装**

`Game.CleanRoom/Purity/CleanRoomPollutionInput.cs`:

```csharp
namespace Game.CleanRoom.Purity
{
    // 部屋ごとの A_total（個/秒）を供給する注入境界。フェーズ3が実体を供給する。
    // Injection boundary supplying A_total (per sec) per room; phase 3 provides the implementation.
    public interface CleanRoomPollutionInput
    {
        // 指定部屋の総汚染レート A_total（個/秒）。フェーズ2のスタブは固定値、フェーズ3で実算出。
        // Total pollution rate A_total (per sec) for the given room.
        double GetPollutionPerSecond(CleanRoom room);
    }
}
```

> 命名は `I` 接頭辞無し（コードマップ §2 のキー型名 `CleanRoomPollutionInput` に合わせる）。インターフェースだが契約名はコードマップ準拠。

- [ ] **Step 2: n·q 供給インターフェースを実装**

`Game.CleanRoom/Purity/ICleanRoomPurificationSource.cs`:

```csharp
using UnityEngine;

namespace Game.CleanRoom.Purity
{
    // 清浄機1台が供給する除去能力 q（m³/秒）と、どの部屋に属するかを引くための代表占有セル。
    // One purifier's removal volume q (m^3/sec) and a representative cell to locate its room.
    public interface ICleanRoomPurificationSource
    {
        // この清浄機の代表占有セル。サービスが TryGetRoomAt でこのセルから部屋を引く。
        // Representative occupied cell; the service maps the purifier to a room via TryGetRoomAt.
        Vector3Int RepresentativeCell { get; }

        // 毎秒処理体積 q（m³/秒）。フェーズ3は電力割合・フィルター残で実効値を返す。
        // Removal volume per second q (m^3/sec). Phase 3 returns the effective value.
        double RemovalVolumePerSecond { get; }
    }
}
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。型未検出なら新規ファイル認識のため Unity 再起動。

- [ ] **Step 4: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPollutionInput.cs moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/ICleanRoomPurificationSource.cs
git commit -m "feat(cleanroom): A_total per-room と n·q source の注入境界を追加"
```

---

## Task 4: フェーズ1へ再検出通知を追加（OnRoomsRebuilt）

`CleanRoomPurityService` が「再検出が起きた瞬間」に状態引き継ぎを走らせるための通知を、フェーズ1の `CleanRoomDetectionSystem` に一点追加する。`RebuildAll()` 完了時に新しい部屋集合を流す。**これは純粋な追加でフェーズ1テストを壊さない。**

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs`

- [ ] **Step 1: 失敗テスト（RebuildAll でイベントが新部屋集合を流す）**

`CleanRoomPuritySimulationTest.cs` に追加（`using System; using Core.Update; using Game.CleanRoom; using Game.Context; using Server.Boot; using Tests.Module.TestMod; using Microsoft.Extensions.DependencyInjection;` を必要に応じて先頭へ足す。実 using 名は既存テストに合わせる）:

```csharp
        [Test]
        public void Detection_RebuildAll_FiresOnRoomsRebuilt()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var detection = serviceProvider.GetService<CleanRoomDetectionSystem>();

            IReadOnlyList<CleanRoom> fired = null;
            using var sub = detection.OnRoomsRebuilt.Subscribe(rooms => fired = rooms);

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // 内部 3x3x3
            detection.RebuildAll();

            Assert.NotNull(fired, "OnRoomsRebuilt must fire on RebuildAll");
            Assert.AreEqual(1, fired.Count);
        }
```

> `BuildWallShell` はフェーズ1テスト（`CleanRoomDetectionTest`）の private static ヘルパ。別クラスから使えないため、本テストファイルにも同じヘルパをコピーするか、フェーズ1で `internal static` 公開済みなら参照する。**初手は本ファイルへヘルパをコピーする**（Task 5 でも使う）。`OnRoomsRebuilt.Subscribe` の戻りは UniRx `IDisposable`。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Detection_RebuildAll_FiresOnRoomsRebuilt"`
Expected: FAIL（`OnRoomsRebuilt` 未定義）。

- [ ] **Step 3: CleanRoomDetectionSystem に通知を追加**

`Game.CleanRoom/CleanRoomDetectionSystem.cs` を開き（フェーズ1で確認した実装に合わせて）、UniRx `Subject` を足す。`using UniRx;` を先頭へ。クラスに以下を追加し、`RebuildAll()` の末尾で発火する:

```csharp
        // 再検出完了の通知。純度サービスがこれを購読して状態を新部屋へ引き継ぐ。
        // Notifies that re-detection finished; the purity service carries state onto the new rooms.
        public IObservable<IReadOnlyList<CleanRoom>> OnRoomsRebuilt => _onRoomsRebuilt;
        private readonly Subject<IReadOnlyList<CleanRoom>> _onRoomsRebuilt = new();
```

`RebuildAll()` の本体末尾（`_rooms` を差し替え・`RebuildCount++` の後）に追加:

```csharp
            // 再検出完了を購読者へ通知する。
            // Notify subscribers that re-detection completed.
            _onRoomsRebuilt.OnNext(_rooms);
```

`Destroy()`（購読 dispose 箇所）に追加:

```csharp
            _onRoomsRebuilt.Dispose();
```

> フェーズ1の `RebuildAll()` の実シグネチャ・`_rooms` フィールド名・`Destroy()` の有無は実ファイルで確認して合わせる。`using System;`（`IObservable`）と `using UniRx;`（`Subject`）が無ければ足す。

- [ ] **Step 4: テスト実行 ＋ フェーズ1非回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Detection_RebuildAll_FiresOnRoomsRebuilt"`
Expected: PASS。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomDetectionTest"`
Expected: フェーズ1全 PASS（通知追加が既存を壊していない）。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): 再検出完了通知OnRoomsRebuiltを検出システムに追加"
```

---

## Task 5: CleanRoomPurityService の tick 積分と平衡（worked example で固定）

サービス本体。`GameUpdater.UpdateObservable` を購読し、各部屋について `dN = (A_total − n·q·(N/V)) · SecondsPerTick` を毎tick積分する。`A_total` は `CleanRoomPollutionInput.GetPollutionPerSecond(room)`、`n·q` は room 内 `ICleanRoomPurificationSource` の合算（`TryGetRoomAt` で振り分け）。クラスは毎tick `CleanRoomClassDecider.Decide` で更新する。worked example（V=75, A_total=16, q=5 → C_eq=3.2, クラスA）で平衡を固定する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`

### 数値（balance-parameters §4 worked example）

- 基準部屋: 内寸 5×5×3 = **V=75 m³**（外殻 7×7×5、内部 5×5×3）。
- `A_total = 16.0 個/秒`（テストでは per-room スタブが固定で返す）。
- 清浄機 1台 `q = 5.0 m³/秒` → `n·q = 5`。
- **平衡:** `C_eq = A_total/(n·q) = 16/5 = 3.2 個/m³` → クラスA域（≤10）。`N_eq = C_eq·V = 3.2·75 = 240`。
- **ACH:** `n·q/V = 5/75 ≈ 0.0667 /秒` → A要求 0.017 を満たす。
- **時定数:** `τ = V/(n·q) = 75/5 = 15 秒 = 300 tick`。収束には十分長く回す（`RunFrames(2000)` ≈ 100秒 ≫ 6.6τ）。

> サービス本体は本物の `CleanRoomPollutionInput` / `IReadOnlyList<ICleanRoomPurificationSource>` を DI から受ける。**フェーズ2の DI 既定はゼロ供給スタブ**（後述）。テストはスタブをサービスに直接渡せるよう、サービスは public コンストラクタで両依存を受ける設計にする。

- [ ] **Step 1: 失敗テスト（平衡濃度3.2・クラスA／清浄機0台で発散しOut）**

`CleanRoomPuritySimulationTest.cs` に追加。テスト用スタブをファイル内に定義する:

```csharp
        // テスト用: 指定部屋にだけ固定 A_total を返すスタブ。
        // Test stub: returns a fixed A_total only for the target room.
        private sealed class FixedPollutionStub : CleanRoomPollutionInput
        {
            private readonly double _aTotal;
            public FixedPollutionStub(double aTotal) { _aTotal = aTotal; }
            public double GetPollutionPerSecond(CleanRoom room) => _aTotal;
        }

        // テスト用: 代表セルと q を持つ清浄機スタブ。
        // Test stub: a purifier with a representative cell and q.
        private sealed class PurifierStub : ICleanRoomPurificationSource
        {
            public Vector3Int RepresentativeCell { get; }
            public double RemovalVolumePerSecond { get; }
            public PurifierStub(Vector3Int cell, double q) { RepresentativeCell = cell; RemovalVolumePerSecond = q; }
        }

        [Test]
        public void Service_ReferenceRoom_ConvergesToCeq3p2_ClassA()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var detection = serviceProvider.GetService<CleanRoomDetectionSystem>();

            // 外殻 7x7x5 → 内部 5x5x3 = V75。
            // Shell 7x7x5 -> interior 5x5x3 = V75.
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(6, 6, 4));
            detection.RebuildAll();
            Assert.AreEqual(75, detection.Rooms[0].Volume, "Reference room must be V=75");

            // 室内の代表セル（中心付近）に q=5 の清浄機1台、A_total=16 を注入。
            // One purifier q=5 at an interior cell, A_total=16.
            var insideCell = new Vector3Int(3, 2, 3);
            Assert.True(detection.Rooms[0].Contains(insideCell), "purifier cell must be inside the room");
            var pollution = new FixedPollutionStub(16.0);
            var purifiers = new List<ICleanRoomPurificationSource> { new PurifierStub(insideCell, 5.0) };

            var service = new CleanRoomPurityService(detection, pollution, purifiers);
            // 6.6τ 超（τ=300tick）回して平衡へ。
            // Run well past 6.6τ (τ=300 ticks) to reach equilibrium.
            GameUpdater.RunFrames(2000);

            Assert.True(service.TryGetState(detection.Rooms[0], out var state));
            Assert.AreEqual(3.2, state.Concentration(75.0), 0.05, "C_eq = 16/5 = 3.2");
            Assert.AreEqual(CleanRoomClass.A, state.CurrentClass);

            service.Destroy(); // static GameUpdater 購読を解除（後続テストへの漏れ防止）
        }

        [Test]
        public void Service_NoPurifier_IsOut()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var detection = serviceProvider.GetService<CleanRoomDetectionSystem>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(6, 6, 4));
            detection.RebuildAll();

            // 清浄機 0 台 → n·q=0 → ACH=0。換気要求(最低D=0.0014)すら満たせず即 Out（濃度に依らない）。
            // No purifier -> n·q=0 -> ACH=0; fails even D's requirement, so Out immediately regardless of N.
            var service = new CleanRoomPurityService(detection, new FixedPollutionStub(16.0),
                new List<ICleanRoomPurificationSource>());
            GameUpdater.RunFrames(1);

            Assert.True(service.TryGetState(detection.Rooms[0], out var state));
            Assert.AreEqual(CleanRoomClass.Out, state.CurrentClass, "Zero ACH cannot satisfy any class -> Out");

            service.Destroy();
        }
```

> `Service_NoPurifier_IsOut` は「発散を待つ」テストではない。`n·q=0` なら `ACH=n·q/V=0` で全クラスの換気要求（最低でも D=0.0014）を満たせず、N が小さくても `Decide` は 1tick で Out を返す。長い `RunFrames` は不要（1tick で十分）。除去ゼロでは N は線形に増え続けるので「平衡しない＝清浄機が要る」という設計意図はこの即 Out が体現する。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Service_"`
Expected: FAIL（`CleanRoomPurityService` 未定義）。

- [ ] **Step 3: CleanRoomPurityService を実装（tick積分＋per-room nq集計）**

`Game.CleanRoom/Purity/CleanRoomPurityService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Update;
using UniRx;

namespace Game.CleanRoom.Purity
{
    // 全部屋の継続純度状態を保持し、毎tick dN/dt を積分してクラスを更新する DI singleton。
    // DI singleton holding per-room purity state; integrates dN/dt each tick and updates class.
    public class CleanRoomPurityService
    {
        // 部屋同一性キーは Cells 重なりだが、tick 中は現在の CleanRoom 参照で引く。
        // States keyed by the current CleanRoom reference during ticking.
        private readonly Dictionary<CleanRoom, CleanRoomPurityState> _states = new();
        private readonly CleanRoomDetectionSystem _detection;
        private readonly CleanRoomPollutionInput _pollution;
        private readonly IReadOnlyList<ICleanRoomPurificationSource> _purifiers;
        private readonly List<IDisposable> _subscriptions = new();

        public CleanRoomPurityService(
            CleanRoomDetectionSystem detection,
            CleanRoomPollutionInput pollution,
            IReadOnlyList<ICleanRoomPurificationSource> purifiers)
        {
            _detection = detection;
            _pollution = pollution;
            _purifiers = purifiers;

            // 既存部屋（コンストラクト時点で検出済みなら）を初期登録する。
            // Seed states for already-detected rooms.
            OnRoomsRebuilt(detection.Rooms);

            // tick 購読と再検出購読。
            // Subscribe to tick and re-detection.
            _subscriptions.Add(GameUpdater.UpdateObservable.Subscribe(_ => OnTick()));
            _subscriptions.Add(detection.OnRoomsRebuilt.Subscribe(OnRoomsRebuilt));
        }

        public bool TryGetState(CleanRoom room, out CleanRoomPurityState state)
        {
            return _states.TryGetValue(room, out state);
        }

        // 1tick 分（50ms）の積分をすべての有効部屋に適用する。
        // Integrate one tick (50ms) for every valid room.
        private void OnTick()
        {
            foreach (var room in _detection.Rooms)
            {
                if (!_states.TryGetValue(room, out var state)) continue;

                // V・A_total・n·q を集計し dN を積分。
                // Aggregate V, A_total, n·q and integrate dN.
                var volume = room.Volume;
                var aTotal = _pollution.GetPollutionPerSecond(room);
                var nq = SumPurificationVolume(room);
                var concentration = state.Concentration(volume);

                var dN = (aTotal - nq * concentration) * GameUpdater.SecondsPerTick;
                if (dN >= 0.0) state.AddImpurity(dN);
                else state.RemoveImpurity(-dN);

                // クラスを二条件＋ヒステリシスで更新。
                // Update class via two conditions + hysteresis.
                var ach = volume > 0 ? nq / volume : 0.0;
                var newConcentration = state.Concentration(volume);
                state.SetCurrentClass(CleanRoomClassDecider.Decide(state.CurrentClass, newConcentration, ach));
            }

            // 段階・猶予の更新は Task 6 で OnTick に追加する。
            // Status/grace updates are added to OnTick in Task 6.
        }

        // この部屋に属する清浄機の q を合算する（代表セルが部屋に含まれるものだけ）。
        // Sum q of purifiers whose representative cell lies in this room.
        private double SumPurificationVolume(CleanRoom room)
        {
            var sum = 0.0;
            foreach (var purifier in _purifiers)
            {
                if (room.Contains(purifier.RepresentativeCell))
                    sum += purifier.RemovalVolumePerSecond;
            }
            return sum;
        }

        // 再検出後の状態引き継ぎ。Task 6 で結合/分割/消滅(Degraded)を実装する。
        // Carry state across re-detection; merge/split/vanish(Degraded) implemented in Task 6.
        private void OnRoomsRebuilt(IReadOnlyList<CleanRoom> rooms)
        {
            // フェーズ2のこの時点では、未登録の新部屋に空状態を割り当てる最小実装。
            // Minimal: assign fresh state to any new room not yet tracked.
            var carried = new Dictionary<CleanRoom, CleanRoomPurityState>();
            foreach (var room in rooms)
            {
                if (!carried.ContainsKey(room))
                    carried[room] = new CleanRoomPurityState(new HashSet<UnityEngine.Vector3Int>(room.Cells));
            }
            _states.Clear();
            foreach (var kvp in carried) _states[kvp.Key] = kvp.Value;
        }

        public void Destroy()
        {
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }
    }
}
```

> `GameUpdater.SecondsPerTick`（= 0.05）と `GameUpdater.UpdateObservable`（UniRx `IObservable<Unit>`）は `Core.Update/GameUpdater.cs` で確認済み。`CleanRoom.Volume`/`Contains` はフェーズ1。`OnRoomsRebuilt` の Cells 重なり引き継ぎ（マッチ/結合/分割）は **Task 6 で本実装に差し替える**（このタスクの最小版は「再構築のたびに状態リセット」なので、Task 5 の平衡テストは `RebuildAll()` を1回だけ呼んでから tick する構成にしてある）。

- [ ] **Step 4: DI に登録（ゼロ供給スタブ込み）＋ eager**

`Server.Boot/MoorestechServerDIContainerGenerator.cs` の世界システム登録箇所（`RailGraphSaveLoadService` 登録の近く、`services.AddSingleton<...>` 群）に追加。フェーズ2の DI 既定は「汚染ゼロ・清浄機ゼロ」スタブ:

```csharp
            // フェーズ2: 汚染ゼロのスタブ。フェーズ3で CleanRoomPollutionCalculator に差し替える。
            // Phase 2: zero-pollution stub; replaced by CleanRoomPollutionCalculator in phase 3.
            services.AddSingleton<Game.CleanRoom.Purity.CleanRoomPollutionInput>(_ => new Game.CleanRoom.Purity.ZeroPollutionInput());
            services.AddSingleton<IReadOnlyList<Game.CleanRoom.Purity.ICleanRoomPurificationSource>>(
                _ => new List<Game.CleanRoom.Purity.ICleanRoomPurificationSource>());
            services.AddSingleton<Game.CleanRoom.Purity.CleanRoomPurityService>();
```

eager 実体化箇所（`serviceProvider.GetService<GearNetworkDatastore>();` 群）に追加:

```csharp
            serviceProvider.GetService<Game.CleanRoom.Purity.CleanRoomPurityService>();
```

そして `Game.CleanRoom/Purity/CleanRoomPollutionInput.cs` の隣に最小スタブを追加（同ファイル末尾でも別ファイルでも可。ここでは `ZeroPollutionInput.cs` を新規作成）:

```csharp
namespace Game.CleanRoom.Purity
{
    // フェーズ2 DI 既定。常に 0 を返す。フェーズ3で実算出器に置換。
    // Phase 2 DI default; always returns 0. Replaced by the real calculator in phase 3.
    public class ZeroPollutionInput : CleanRoomPollutionInput
    {
        public double GetPollutionPerSecond(CleanRoom room) => 0.0;
    }
}
```

> `IReadOnlyList<...>` の DI 登録は使用 DI コンテナの記法に合わせる（`System.Collections.Generic` を using）。`Server.Boot` の asmdef に `Game.CleanRoom` 参照が無ければ追加。

- [ ] **Step 5: コンパイル ＋ テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。型未検出なら Unity 再起動。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Service_"`
Expected: PASS（`Service_ReferenceRoom...` は C≈3.2・クラスA。`Service_NoPurifier...` は tick 数を実測で詰めて Out）。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityService.cs moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/ZeroPollutionInput.cs moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): 純度tick積分サービスを追加し基準部屋の平衡C_eq=3.2を固定"
```

---

## Task 6: 同一性引き継ぎ＋Valid/Degraded/Invalid＋猶予（一体の機構）

再検出をまたいだ状態継続と段階遷移は**一つの機構**。リークで部屋が検出から消える＝旧状態を削除せず Degraded にして N を保持し猶予（5.0秒=100tick）を開始する。猶予内に部屋が再出現（セル重なり）すれば N を引き継いで Valid、猶予切れまで未出現なら Invalid。結合は N 合算、分割は濃度按分（N=C·V_new）。`OnTick` は部屋を失った Degraded 状態の猶予を毎tick減らす。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityService.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`

### 仕様（balance-parameters §1.2 / §5 ＋ コードマップ §1.2）

- **猶予 `graceSeconds = 5.0 秒 = 100 tick`**（`GameUpdater.SecondsToTicks(5.0) = 100`）。
- **消滅（部屋が検出から消えた）:** 旧状態を破棄せず Degraded・N保持・猶予開始。tick で猶予減算。
- **再出現（猶予中にセル重なりで再マッチ）:** N を引き継ぎ Valid へ復帰、猶予クリア。
- **猶予切れ:** Invalid（以後 N は保持するが稼働停止＝フェーズ4が参照）。
- **マッチ:** 旧状態 Cells と新部屋 Cells の**最大セル重なり**で1対1対応。
- **結合（複数旧状態が1新部屋へ）:** N を合算。
- **分割（1旧状態が複数新部屋へ）:** 旧濃度 `C = N_old/V_old` を各新部屋へ適用し `N_new = C·V_new`。

- [ ] **Step 1: 失敗テスト（シールブレイク→Degraded(N保持)→再封→Valid／猶予切れ→Invalid／分割按分）**

`CleanRoomPuritySimulationTest.cs` に追加。`A_total=0` のスタブ（純度を凍結して N の引き継ぎだけ見る）を使う:

```csharp
        private sealed class ZeroPollutionStub : CleanRoomPollutionInput
        {
            public double GetPollutionPerSecond(CleanRoom room) => 0.0;
        }

        [Test]
        public void Service_SealBreak_KeepsImpurity_AndGoesDegraded()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var detection = serviceProvider.GetService<CleanRoomDetectionSystem>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            detection.RebuildAll();

            var service = new CleanRoomPurityService(detection, new ZeroPollutionStub(),
                new List<ICleanRoomPurificationSource>());
            Assert.True(service.TryGetState(detection.Rooms[0], out var state));
            state.AddImpurity(150.0); // 既存の汚れを入れておく

            // 壁を1枚壊して密閉を崩す → 再検出で部屋が消える。
            // Break one wall -> room vanishes on re-detection.
            world.RemoveBlock(new Vector3Int(2, 2, 0), Game.World.Interface.DataStore.BlockRemoveReason.Destroy);
            detection.RebuildAll();
            Assert.AreEqual(0, detection.Rooms.Count, "room must vanish");

            // 旧状態は削除されず Degraded・N=150 保持・猶予作動。
            // Old state is kept Degraded with N preserved and grace running.
            Assert.True(service.TryGetDegradedState(out var degraded));
            Assert.AreEqual(CleanRoomRoomStatus.Degraded, degraded.Status);
            Assert.AreEqual(150.0, degraded.ImpurityCount, 1e-6);
            Assert.Greater(degraded.GraceRemainingSeconds, 0f);

            service.Destroy(); // 購読漏れ防止
        }

        [Test]
        public void Service_ResealWithinGrace_RecoversValid_CarriesImpurity()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var detection = serviceProvider.GetService<CleanRoomDetectionSystem>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            detection.RebuildAll();

            var service = new CleanRoomPurityService(detection, new ZeroPollutionStub(),
                new List<ICleanRoomPurificationSource>());
            service.TryGetState(detection.Rooms[0], out var state);
            state.AddImpurity(150.0);

            world.RemoveBlock(new Vector3Int(2, 2, 0), Game.World.Interface.DataStore.BlockRemoveReason.Destroy);
            detection.RebuildAll();
            GameUpdater.RunFrames(50); // 猶予100tick未満

            // 同じ位置に壁を戻す → 再検出で部屋復活。
            // Restore the wall -> room reappears on re-detection.
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(2, 2, 0),
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            detection.RebuildAll();

            Assert.AreEqual(1, detection.Rooms.Count);
            Assert.True(service.TryGetState(detection.Rooms[0], out var recovered));
            Assert.AreEqual(CleanRoomRoomStatus.Valid, recovered.Status);
            Assert.AreEqual(150.0, recovered.ImpurityCount, 1e-6, "N carried across reseal");

            service.Destroy(); // 購読漏れ防止
        }

        [Test]
        public void Service_GraceExpires_GoesInvalid()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var detection = serviceProvider.GetService<CleanRoomDetectionSystem>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            detection.RebuildAll();

            var service = new CleanRoomPurityService(detection, new ZeroPollutionStub(),
                new List<ICleanRoomPurificationSource>());
            service.TryGetState(detection.Rooms[0], out var state);
            state.AddImpurity(150.0);

            world.RemoveBlock(new Vector3Int(2, 2, 0), Game.World.Interface.DataStore.BlockRemoveReason.Destroy);
            detection.RebuildAll();
            GameUpdater.RunFrames(120); // 猶予100tick超

            Assert.True(service.TryGetDegradedState(out var expired));
            Assert.AreEqual(CleanRoomRoomStatus.Invalid, expired.Status);
            Assert.AreEqual(150.0, expired.ImpurityCount, 1e-6, "N retained even when Invalid");

            service.Destroy(); // 購読漏れ防止
        }
```

> `TryGetDegradedState` は「現在どの検出部屋にも紐付かない継続状態」を取り出すテスト用アクセサ（Step 3 で実装）。`BlockRemoveReason`/`BlockDirection`/`BlockCreateParam` の名前空間は既存テスト（`CleanRoomDetectionTest`）に合わせる。**結合/分割の N 保存はワールド形状を組まず、按分純関数 `RedistributeImpurity` の単体テスト（Step 4）で固定する**（advisor 指摘: 形状ベースのテストは差分体積ケースを取りこぼし保存則バグが緑のまま残る）。ここの3テストは消滅→Degraded→Invalid/復活の段階遷移だけを担当する。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Service_(SealBreak|ResealWithinGrace|GraceExpires)"`
Expected: FAIL（`TryGetDegradedState` 未定義・引き継ぎ未実装）。

- [ ] **Step 3: OnRoomsRebuilt をセル重なり引き継ぎへ差し替え＋OnTick に猶予減算を追加**

`CleanRoomPurityService.cs` の `OnRoomsRebuilt` を以下へ置換し、消滅状態の保持リストと猶予定数・アクセサを足す:

```csharp
        // どの検出部屋にも紐付かない継続状態（消滅→Degraded/Invalid 中）。猶予で復活待ち。
        // Continuation states not bound to any detected room (vanished -> Degraded/Invalid), awaiting reseal.
        private readonly List<CleanRoomPurityState> _orphanStates = new();

        // 猶予秒数。balance-parameters §1.2: 5.0秒 = 100tick。
        // Grace seconds. balance-parameters §1.2: 5.0s = 100 ticks.
        public const float GraceSeconds = 5.0f;

        public bool TryGetDegradedState(out CleanRoomPurityState state)
        {
            // テスト/フェーズ4用: 最初の孤立状態を返す。
            // For tests/phase 4: return the first orphan state.
            state = _orphanStates.Count > 0 ? _orphanStates[0] : null;
            return state != null;
        }
```

`OnRoomsRebuilt` 本体:

```csharp
        // 再検出後、旧状態（検出中＋孤立）を新部屋へ最大セル重なりで引き継ぐ。
        // After re-detection, carry old states (tracked + orphan) onto new rooms by max cell overlap.
        private void OnRoomsRebuilt(IReadOnlyList<CleanRoom> rooms)
        {
            // 旧状態をすべて1プールに集める（検出中・孤立の両方）。
            // Gather all old states into one pool (tracked and orphan).
            var oldStates = new List<CleanRoomPurityState>(_states.Values);
            oldStates.AddRange(_orphanStates);
            _states.Clear();
            _orphanStates.Clear();

            var matchedOld = new HashSet<CleanRoomPurityState>();

            // 各新部屋について、重なる旧状態の寄与を合算して N を引き継ぐ（純関数で按分）。
            // For each new room, carry N by summing overlapping old states' contributions (pure apportionment).
            foreach (var room in rooms)
            {
                double carriedN = 0.0;
                var anyOverlap = false;
                foreach (var old in oldStates)
                {
                    var overlap = CountOverlap(old.Cells, room);
                    if (overlap <= 0) continue;
                    anyOverlap = true;
                    matchedOld.Add(old);
                    // 純関数で按分（保存則を満たす単一の式に集約）。
                    // Apportion via the pure function (single conservation-correct formula).
                    carriedN += RedistributeImpurity(old.ImpurityCount, old.Cells.Count, overlap);
                }

                var newState = new CleanRoomPurityState(new HashSet<UnityEngine.Vector3Int>(room.Cells));
                if (anyOverlap) newState.SetImpurityCount(carriedN);
                newState.SetStatus(CleanRoomRoomStatus.Valid);
                newState.SetGraceRemainingSeconds(0f);
                _states[room] = newState;
            }

            // どの新部屋にも対応しなかった旧状態は消滅扱い: Degraded＋猶予開始（既に猶予中なら維持）。
            // Old states matching no new room vanished: go Degraded with grace (keep running grace if already set).
            foreach (var old in oldStates)
            {
                if (matchedOld.Contains(old)) continue;
                if (old.Status == CleanRoomRoomStatus.Valid)
                {
                    old.SetStatus(CleanRoomRoomStatus.Degraded);
                    old.SetGraceRemainingSeconds(GraceSeconds);
                }
                _orphanStates.Add(old);
            }
        }
```

`OnTick` の末尾（クラス更新の後）に孤立状態の猶予減算を追加:

```csharp
            // 孤立状態（部屋を失った Degraded）の猶予を毎tick減らし、切れたら Invalid。
            // Decrement grace for orphan (room-less Degraded) states; on expiry, become Invalid.
            foreach (var orphan in _orphanStates)
            {
                if (orphan.Status != CleanRoomRoomStatus.Degraded) continue;
                var remaining = orphan.GraceRemainingSeconds - (float)GameUpdater.SecondsPerTick;
                orphan.SetGraceRemainingSeconds(remaining);
                if (orphan.GraceRemainingSeconds <= 0f)
                    orphan.SetStatus(CleanRoomRoomStatus.Invalid);
            }
```

按分ヘルパと**保存則を満たす純関数**をクラスに追加:

```csharp
        // 旧状態のセル集合と新部屋の重なりセル数。
        // Number of overlapping cells between an old state's cells and a new room.
        private static int CountOverlap(System.Collections.Generic.IReadOnlyCollection<UnityEngine.Vector3Int> oldCells, CleanRoom room)
        {
            var count = 0;
            foreach (var cell in oldCells)
                if (room.Contains(cell)) count++;
            return count;
        }

        // 旧状態から新部屋への不純物按分（純関数・保存則を満たす）。
        // 旧濃度 C_old=N_old/V_old を、新部屋に入る旧セル数 overlap 個ぶんだけ移す = C_old·overlap。
        // Pure, conservation-correct apportionment: move C_old over the `overlap` cells = C_old·overlap.
        public static double RedistributeImpurity(double oldImpurity, int oldVolume, int overlap)
        {
            if (oldVolume <= 0 || overlap <= 0) return 0.0;
            return oldImpurity * overlap / oldVolume;
        }
```

> **按分の意味（保存則）:** 各新部屋は `N_new = Σ_old ( C_old · overlap )`。`C_old = N_old/V_old`。
> - **1対1（overlap=V_old=V_new）:** `N_new = (N_old/V_old)·V_old = N_old`（V不変ならN不変、V変化なら濃度保存）。
> - **結合（複数旧→1新、各 overlap=V_old）:** `Σ (N_old/V_old)·V_old = Σ N_old` ＝ N 合算。
> - **分割（1旧→複数新、Σ overlap = V_old）:** `Σ (N_old/V_old)·overlap = N_old`（総N保存）かつ各新部屋は `C_old·V_new`（V_new=overlap）。
>
> 旧コードにあった `· room.Volume · overlapFraction` の二重係数は保存則を破る（1対1以外で N が増減する）ため**使わない**。balance-parameters §5「分割時 N=C·V_new／結合時 N合算」と整合。

- [ ] **Step 4: 按分純関数の単体テスト（結合・分割で N 保存）を追加**

`CleanRoomPuritySimulationTest.cs` に、ワールド形状不要の純関数テストを追加する。これが**分割=N=C·V_new／結合=N合算**を直接固定する:

```csharp
        [Test]
        public void Redistribute_Split_ConservesTotalImpurity_AndPreservesConcentration()
        {
            // 旧 {V=10, N=100, C=10} を 2つの新部屋 {V=5,V=5} へ分割。
            // Old {V=10, N=100, C=10} split into two new rooms {V=5, V=5}.
            var n1 = CleanRoomPurityService.RedistributeImpurity(oldImpurity: 100.0, oldVolume: 10, overlap: 5);
            var n2 = CleanRoomPurityService.RedistributeImpurity(oldImpurity: 100.0, oldVolume: 10, overlap: 5);

            // 各新部屋は C_old·V_new = 10·5 = 50。総和は 100 で保存。
            // Each new room gets C_old·V_new = 50; total stays 100.
            Assert.AreEqual(50.0, n1, 1e-9);
            Assert.AreEqual(50.0, n2, 1e-9);
            Assert.AreEqual(100.0, n1 + n2, 1e-9, "split conserves total N");
        }

        [Test]
        public void Redistribute_Merge_SumsImpurity()
        {
            // 2つの旧 {V=5,N=50} が 1つの新部屋 {V=10} へ結合（各 overlap=V_old=5）。
            // Two old {V=5,N=50} merge into one new room {V=10}; each contributes its full overlap.
            var contribA = CleanRoomPurityService.RedistributeImpurity(oldImpurity: 50.0, oldVolume: 5, overlap: 5);
            var contribB = CleanRoomPurityService.RedistributeImpurity(oldImpurity: 50.0, oldVolume: 5, overlap: 5);

            // 合算 = 50 + 50 = 100。
            Assert.AreEqual(100.0, contribA + contribB, 1e-9, "merge sums N");
        }
```

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.(Service_(SealBreak|ResealWithinGrace|GraceExpires)|Redistribute_)"`
Expected: PASS。**`Redistribute_` が落ちたら按分式が保存則を破っている**（`· room.Volume` の混入を疑う）。

- [ ] **Step 6: フェーズ2純度テストの非回帰（平衡テストが壊れていないか）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest"`
Expected: 全 PASS（`Service_ReferenceRoom...` は `RebuildAll()` 1回後の tick なので引き継ぎ差し替えの影響を受けない）。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityService.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): セル重なり引き継ぎとValid/Degraded/Invalid＋猶予を実装"
```

---

## Task 7: 永続化（SaveData ＋ SaveLoadService ＋ 3点改修 ＋ ロード順）

純度状態を `CleanRoomPuritySaveData`（N＋クラス＋段階＋猶予＋全セル署名）で保存し、`CleanRoomSaveLoadService.GetSaveData/Restore` を鉄道 `RailGraphSaveLoadService` の形で実装する。`WorldSaveAllInfoV1`/`AssembleSaveJsonText`/`WorldLoaderFromJson` の3点を改修し、**ロード復元は `LoadBlockDataList` → `RebuildAll()` → `Restore()` の順**で「ブロック生成・部屋検出の後」に置く。フル save/load ラウンドトリップで N が残ることを固定する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/SaveLoad/CleanRoomPuritySaveData.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/SaveLoad/CleanRoomSaveLoadService.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurityPersistenceTest.cs`

### セーブ形式（balance-parameters §6）

| フィールド | 型 | 内容 |
|---|---|---|
| `impurityCount` | double | N |
| `currentClass` | int | クラス列挙 |
| `status` | int | Valid/Degraded/Invalid |
| `graceRemainingSeconds` | float | 猶予残 |
| `cells` | int[][]（x,y,z） | 同一性照合用の全セル署名 |

> V/S は再検出から再導出するため保存しない。

- [ ] **Step 1: 失敗テスト（フル save/load ラウンドトリップで N が残り再マッチされる）**

`Tests/CombinedTest/Core/CleanRoomPurityPersistenceTest.cs` を新規作成。`AssembleSaveJsonText.AssembleSaveJson()` で保存し、新コンテナ＋`WorldLoaderFromJson.Load(json)` で復元、N が残ることを assert する:

```csharp
using System.Collections.Generic;
using Core.Update;
using Game.CleanRoom;
using Game.CleanRoom.Purity;
using Game.Context;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPurityPersistenceTest
    {
        [Test]
        public void SaveLoad_RoundTrip_PreservesImpurity()
        {
            // 1. 保存側コンテナで部屋を作り N を入れて JSON 化。
            // 1. Build a room, set N, serialize JSON on the save-side container.
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var saveWorld = ServerContext.WorldBlockDatastore;
            var saveDetection = saveProvider.GetService<CleanRoomDetectionSystem>();
            var savePurity = saveProvider.GetService<CleanRoomPurityService>();

            BuildWallShell(saveWorld, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            saveDetection.RebuildAll();
            Assert.True(savePurity.TryGetState(saveDetection.Rooms[0], out var state));
            state.AddImpurity(123.0);
            state.SetCurrentClass(CleanRoomClass.B);

            var json = saveProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 2. 新規コンテナで Load → ブロック・部屋・純度を復元。
            // 2. Fresh container; Load restores blocks, rooms, and purity.
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            loadProvider.GetService<IWorldSaveDataLoaderAccessorOrLoaderType>(); // 実型に合わせる
            loadProvider.GetService<WorldLoaderFromJson>().Load(json);

            var loadDetection = loadProvider.GetService<CleanRoomDetectionSystem>();
            var loadPurity = loadProvider.GetService<CleanRoomPurityService>();

            Assert.AreEqual(1, loadDetection.Rooms.Count, "room re-detected after load");
            Assert.True(loadPurity.TryGetState(loadDetection.Rooms[0], out var restored),
                "purity re-matched to the re-detected room by cell overlap");
            Assert.AreEqual(123.0, restored.ImpurityCount, 1e-6, "N survived save/load");
            Assert.AreEqual(CleanRoomClass.B, restored.CurrentClass);
        }

        // フェーズ1の BuildWallShell をこのファイルにもコピーする。
        // Copy phase-1's BuildWallShell helper into this file.
        private static void BuildWallShell(Game.World.Interface.DataStore.IWorldBlockDatastore world,
            Vector3Int min, Vector3Int max)
        {
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var onShell = x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z;
                if (!onShell) continue;
                world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(x, y, z),
                    Game.Block.Interface.BlockDirection.North,
                    System.Array.Empty<Game.Block.Interface.BlockCreateParam>(), out _);
            }
        }
    }
}
```

> `WorldLoaderFromJson` を DI から取得する正確な型・`AssembleSaveJsonText` の取得は既存セーブ系テスト（`SaveLoad` 系の `CombinedTest`）を開いて確認する。`IWorldSaveDataLoaderAccessorOrLoaderType` は仮名: 実際の `IWorldSaveDataLoader` 実装が `WorldLoaderFromJson` ならそれを直接 `GetService<WorldLoaderFromJson>()` で取り `Load(json)` する。**保存側と読込側で同じプロセス内 static（`ServerContext.WorldBlockDatastore`）を共有するため、2コンテナを順に作ると後勝ちになる点に注意** — 既存セーブ系テストの「保存→新コンテナでロード」手順を必ず踏襲する（テストが1コンテナ内で `Load` を呼ぶ形なら、それに合わせる）。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPurityPersistenceTest"`
Expected: FAIL（`CleanRoomPuritySaveData`／`CleanRoomSaveLoadService` 未定義、3点未改修）。

- [ ] **Step 3: 保存レコードを実装**

`Game.CleanRoom/SaveLoad/CleanRoomPuritySaveData.cs`:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.CleanRoom.SaveLoad
{
    // 1部屋1レコードの純度保存データ。V/S は再検出から再導出するため保存しない。
    // One record per room. V/S are re-derived from detection, so not saved.
    public class CleanRoomPuritySaveData
    {
        [JsonProperty("impurityCount")] public double ImpurityCount;
        [JsonProperty("currentClass")] public int CurrentClass;
        [JsonProperty("status")] public int Status;
        [JsonProperty("graceRemainingSeconds")] public float GraceRemainingSeconds;

        // 同一性照合用の全セル署名（x,y,z の配列の配列）。
        // Full-cell signature for identity matching (array of [x,y,z]).
        [JsonProperty("cells")] public List<int[]> Cells;
    }
}
```

- [ ] **Step 4: SaveLoadService を実装（GetSaveData / Restore＝セル重なり照合）**

`Game.CleanRoom/SaveLoad/CleanRoomSaveLoadService.cs`:

```csharp
using System.Collections.Generic;
using Game.CleanRoom.Purity;
using UnityEngine;

namespace Game.CleanRoom.SaveLoad
{
    // 純度状態の保存と復元。鉄道 RailGraphSaveLoadService と同じ GetSaveData/Restore 形。
    // Save/restore purity state; same GetSaveData/Restore shape as RailGraphSaveLoadService.
    public class CleanRoomSaveLoadService
    {
        private readonly CleanRoomPurityService _purityService;
        private readonly CleanRoomDetectionSystem _detection;

        public CleanRoomSaveLoadService(CleanRoomPurityService purityService, CleanRoomDetectionSystem detection)
        {
            _purityService = purityService;
            _detection = detection;
        }

        // 検出中の全部屋の純度状態を保存レコードへ。
        // Build save records from all currently detected rooms' purity states.
        public List<CleanRoomPuritySaveData> GetSaveData()
        {
            var results = new List<CleanRoomPuritySaveData>();
            foreach (var room in _detection.Rooms)
            {
                if (!_purityService.TryGetState(room, out var state)) continue;

                var cells = new List<int[]>();
                foreach (var cell in state.Cells)
                    cells.Add(new[] { cell.x, cell.y, cell.z });

                results.Add(new CleanRoomPuritySaveData
                {
                    ImpurityCount = state.ImpurityCount,
                    CurrentClass = (int)state.CurrentClass,
                    Status = (int)state.Status,
                    GraceRemainingSeconds = state.GraceRemainingSeconds,
                    Cells = cells,
                });
            }
            return results;
        }

        // 保存レコードを再検出済みの部屋へ最大セル重なりでマッチして N をシードする。
        // Re-match save records to re-detected rooms by max cell overlap and seed N.
        public void Restore(IReadOnlyList<CleanRoomPuritySaveData> saveData)
        {
            if (saveData == null) return;

            foreach (var record in saveData)
            {
                if (record == null || record.Cells == null) continue;

                var recordCells = new HashSet<Vector3Int>();
                foreach (var c in record.Cells)
                    if (c != null && c.Length >= 3) recordCells.Add(new Vector3Int(c[0], c[1], c[2]));

                // 最大重なりの検出部屋を探す。
                // Find the detected room with the most overlapping cells.
                CleanRoom best = null;
                var bestOverlap = 0;
                foreach (var room in _detection.Rooms)
                {
                    var overlap = 0;
                    foreach (var cell in recordCells)
                        if (room.Contains(cell)) overlap++;
                    if (overlap > bestOverlap) { bestOverlap = overlap; best = room; }
                }

                if (best == null || bestOverlap == 0) continue;
                _purityService.SeedState(best, record.ImpurityCount,
                    (CleanRoomClass)record.CurrentClass, (CleanRoomRoomStatus)record.Status, record.GraceRemainingSeconds);
            }
        }
    }
}
```

`CleanRoomPurityService` に復元用シードメソッドを追加:

```csharp
        // ロード復元用: 指定部屋の状態へ保存値をシードする。
        // For load restore: seed saved values into the given room's state.
        public void SeedState(CleanRoom room, double impurityCount, CleanRoomClass cls, CleanRoomRoomStatus status, float grace)
        {
            if (!_states.TryGetValue(room, out var state))
            {
                state = new CleanRoomPurityState(new HashSet<UnityEngine.Vector3Int>(room.Cells));
                _states[room] = state;
            }
            state.SetImpurityCount(impurityCount);
            state.SetCurrentClass(cls);
            state.SetStatus(status);
            state.SetGraceRemainingSeconds(grace);
        }
```

> `CleanRoomDetectionSystem.Rooms`/`CleanRoom.Contains`/`CleanRoom.Cells` はフェーズ1。`Game.CleanRoom/SaveLoad/` は新規ディレクトリ。`Game.CleanRoom.asmdef` に Newtonsoft.Json 参照が要れば追加（既存セーブ系 asmdef の参照名に合わせる）。

- [ ] **Step 5: WorldSaveAllInfoV1 を改修（コンストラクタ引数＋プロパティ）**

`Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs` に `using Game.CleanRoom.SaveLoad;` を足し、コンストラクタ最終引数に `List<CleanRoomPuritySaveData> cleanRoomPurity` を追加、本体で代入、プロパティを追加:

```csharp
            List<CleanRoomPuritySaveData> cleanRoomPurity)
        {
            // ... 既存代入 ...
            CleanRoomPurity = cleanRoomPurity ?? new List<CleanRoomPuritySaveData>();
        }

        [JsonProperty("cleanRoomPurity")] public List<CleanRoomPuritySaveData> CleanRoomPurity { get; }
```

> 既存コンストラクタ引数（`playerRidingStates` が最後）の**後ろ**に足す。`PlayerRidingStates` のパターンに完全に合わせる。

- [ ] **Step 6: AssembleSaveJsonText を改修（GetSaveData を引数に）**

`Game.SaveLoad/Json/AssembleSaveJsonText.cs` に `CleanRoomSaveLoadService` フィールド＋コンストラクタ注入を追加し、`AssembleSaveJson()` の `new WorldSaveAllInfoV1(...)` 末尾に `_cleanRoomSaveLoadService.GetSaveData()` を追加:

```csharp
        private readonly CleanRoomSaveLoadService _cleanRoomSaveLoadService;
```
（コンストラクタ最終引数に `CleanRoomSaveLoadService cleanRoomSaveLoadService` を追加し `_cleanRoomSaveLoadService = cleanRoomSaveLoadService;`）

```csharp
                _playerRidingDatastore.GetSaveData(),
                _cleanRoomSaveLoadService.GetSaveData()
            );
```

> `using Game.CleanRoom.SaveLoad;` を足す。`Game.SaveLoad.asmdef` に `Game.CleanRoom` 参照を追加（コンパイルエラーで判明する）。

- [ ] **Step 7: WorldLoaderFromJson を改修（LoadBlockDataList → RebuildAll → Restore）**

`Game.SaveLoad/Json/WorldLoaderFromJson.cs` に `CleanRoomDetectionSystem`／`CleanRoomSaveLoadService` フィールド＋コンストラクタ注入を追加。`Load()` の **`_worldBlockDatastore.LoadBlockDataList(load.World);` の直後**に、部屋を検出してから純度を復元する:

```csharp
            _worldBlockDatastore.LoadBlockDataList(load.World);
            // ブロック生成後に部屋を検出し、その後で純度状態をセル重なりで復元する。
            // After blocks exist, detect rooms, then restore purity by cell overlap.
            _cleanRoomDetectionSystem.RebuildAll();
            _cleanRoomSaveLoadService.Restore(load.CleanRoomPurity);
```

> `using Game.CleanRoom; using Game.CleanRoom.SaveLoad;` を足す。**`RailSegments` 復元は `LoadBlockDataList` の後**にある（既存）。クリーンルーム復元も同じ「ブロック後」の位置に置く。`RebuildAll()` を明示的に呼ぶのは、`LoadBlockDataList` が geometryDirty を立てるだけで再検出は次tick まで走らないため（advisor 指摘）。

- [ ] **Step 8: DI に CleanRoomSaveLoadService を登録＋eager**

`Server.Boot/MoorestechServerDIContainerGenerator.cs` の `RailGraphSaveLoadService` 登録の近くに追加:

```csharp
            services.AddSingleton<Game.CleanRoom.SaveLoad.CleanRoomSaveLoadService>();
```
eager 箇所:
```csharp
            serviceProvider.GetService<Game.CleanRoom.SaveLoad.CleanRoomSaveLoadService>();
```

> `AssembleSaveJsonText`/`WorldLoaderFromJson` のコンストラクタに引数追加したので、DI が解決できるよう `CleanRoomSaveLoadService`／`CleanRoomDetectionSystem`／`CleanRoomPurityService` が同 provider に登録済みであること（Task 5・本タスクで登録済み）を確認。

- [ ] **Step 9: コンパイル ＋ テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。新規 asmdef 参照追加なら Unity 再起動。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPurityPersistenceTest"`
Expected: PASS（N=123・クラスB が再検出部屋へ再マッチ）。

- [ ] **Step 10: フェーズ全体＋セーブ系の非回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: フェーズ1＋2 全 PASS。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(SaveLoad|WorldLoad|Rail).*Test"`
Expected: 既存セーブ/ロード/鉄道テストが非回帰（3点改修が既存を壊していない）。

- [ ] **Step 11: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/SaveLoad/ moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityService.cs moorestech_server/Assets/Scripts/Game.SaveLoad/Json/ moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurityPersistenceTest.cs
git commit -m "feat(cleanroom): 純度状態の永続化（SaveData/SaveLoadService/3点改修）を追加しフェーズ2完了"
```

---

## フェーズ2 完了の定義（Definition of Done）

- `CleanRoomClassDecider.Decide` が二条件（濃度＋ACH）＋ヒステリシス（昇格0.8倍/降格素閾値）で A〜D/Out を返す。balance-parameters §1/§1.1 の数値で固定。
- `CleanRoomPurityState` が N の0クランプ加減・`Concentration(V)=N/V`・クラス/段階/猶予/Cells を保持する。
- `CleanRoomPurityService` が `GameUpdater.UpdateObservable` 購読で `dN=(A_total−n·q·C)·0.05` を積分し、基準部屋（V=75・A_total=16・q=5）で **C_eq=3.2・クラスA** へ収束する。清浄機0台は ACH=0 で全クラス換気要求を満たせず **Out**（即時）。
- フェーズ1 `CleanRoomDetectionSystem` に `OnRoomsRebuilt` 通知を追加し、サービスが再検出をまたいで状態を**セル重なり**で引き継ぐ。按分は保存則を満たす純関数 `RedistributeImpurity(N_old, V_old, overlap)=N_old·overlap/V_old`（1対1→N継続、結合→N合算、分割→各 N=C·V_new で総N保存）で、単体テストで固定する。
- **フェーズ2の DI 既定（汚染ゼロ・清浄機ゼロ）では検出された全部屋が Out になる**（n·q=0 → ACH=0）。これは退行ではなく、清浄機の実体がフェーズ3で入るまでの想定挙動。
- 密閉が崩れた部屋は削除されず **Degraded・N保持・猶予5.0秒**、猶予内再封で **Valid 復帰（N継続）**、猶予切れで **Invalid（N保持）**。
- `CleanRoomPuritySaveData`＋`CleanRoomSaveLoadService` と3点改修（`WorldSaveAllInfoV1`/`AssembleSaveJsonText`/`WorldLoaderFromJson`）で純度が永続化され、ロードは **`LoadBlockDataList`→`RebuildAll()`→`Restore()`** の順でセル重なり再マッチする。フル save/load で N が残る。
- `CleanRoomPurityService`／`CleanRoomSaveLoadService` が DI singleton＋eager で登録される。フェーズ2 DI 既定は汚染ゼロ・清浄機ゼロのスタブ（フェーズ3で実装に差し替え）。
- フェーズ1テスト・既存セーブ/鉄道テストが非回帰。

## フェーズ2で意図的に先送りした事項（後続プラン）

- **汚染源の実数供給**（`A_machine`/`A_hatch`/`A_door`/`a_volume·V`/`a_surface·S`/`a_connector·count`）＝ `CleanRoomPollutionInput` の実体 `CleanRoomPollutionCalculator` → フェーズ3。フェーズ2は `ZeroPollutionInput` スタブ。
- **清浄機ブロック実体**（電力・フィルター仕事量消費・実効 q）＝ `ICleanRoomPurificationSource` の実装 → フェーズ3。
- **製造機統合**（最大グレード天井・down-bin・Invalid停止＋猶予参照） → フェーズ4。`Status==Invalid` を機械ゲートが参照する。
- **クラス閾値のマスタ化**（`cleanRoomClasses.yml`） → 任意。本フェーズは `CleanRoomClass.cs` 定数テーブル。
- **dirty領域の差分更新・按分の丸め最適化** → 後続最適化。本フェーズは `RebuildAll()` 全走査＋セル割合按分。

---

## Self-Review

### 仕様カバレッジ（コードマップ §2 ＋ 設計 §3/§4 ＋ balance-parameters の各項 → タスク対応）

| 仕様項目 | 出典 | タスク |
|---|---|---|
| `CleanRoomClass` 列挙＋クラス判定純関数（二条件 C≤閾値 AND ACH≥要求） | codemap §2 / design §3 二条件 / balance §1 | Task 1 |
| ヒステリシス（上げ閾値×0.8／下げ素閾値） | codemap §2 tick step5 / balance §1.1 | Task 1 |
| `CleanRoomPurityState`（N加減0クランプ・Concentration(V)=N/V・class/status/grace/Cells） | codemap §2 キー型 | Task 2 |
| `CleanRoomRoomStatus { Valid, Degraded, Invalid }` | codemap §2 キー型 | Task 2 |
| `CleanRoomPollutionInput`（A_total 注入、per-room） | codemap §2 File Structure | Task 3（per-room化はadvisor決定。下記注記） |
| `ICleanRoomPurificationSource`（n·q 注入、代表セル） | codemap §2 File Structure | Task 3（代表セルはadvisor決定。下記注記） |
| 再検出イベント `OnRoomsRebuilt` | codemap §1.1（購読前提）／フェーズ1に不在 | Task 4（フェーズ1へ追加。下記注記） |
| tick 積分 `dN=(A_total−n·q·C)·0.05`・平衡 C_eq=A/(nq)・worked example C_eq=3.2/クラスA | codemap §2 tick step4 / design §3 / balance §4 | Task 5 |
| 清浄機0台→ACH=0→Out | balance §4「0台＝清浄不可」 / design §3 二条件 | Task 5 |
| Valid/Degraded/Invalid＋猶予5.0秒（seal break→Degraded／猶予内回復→Valid／猶予切れ→Invalid） | codemap §2 tick step6 / design §8 / balance §1.2 | Task 6 |
| 再検出同一性引き継ぎ（最大セル重なり・結合N合算・分割N=C·V_new、保存則） | codemap §1.2 / balance §5 | Task 6（純関数 `RedistributeImpurity` ＋単体テスト） |
| 永続化 `CleanRoomPuritySaveData`＋`CleanRoomSaveLoadService`（GetSaveData/Restore） | codemap §1.3/§2 / balance §6 | Task 7 |
| 3点改修（WorldSaveAllInfoV1/AssembleSaveJsonText/WorldLoaderFromJson）＋ブロック後復元 | codemap §1.3 | Task 7 |
| ロード順 LoadBlockDataList→RebuildAll→Restore | advisor決定（geometryDirtyは次tick） | Task 7 Step7（下記注記） |
| DI 登録＋eager | codemap §2 DI/参照 | Task 5 Step4 / Task 7 Step8 |

### コードマップ契約名との型整合

`CleanRoomPurityState`・`CleanRoomPurityService`・`CleanRoomClass`・`CleanRoomRoomStatus`・`CleanRoomPollutionInput`・`ICleanRoomPurificationSource`・`CleanRoomPuritySaveData`・`CleanRoomSaveLoadService` の8型名はコードマップ §2 のキー型名と完全一致。メソッド名 `AddImpurity`/`RemoveImpurity`/`Concentration`/`TryGetState`/`GetSaveData`/`Restore` もコードマップ準拠。`ImpurityCount` は `get; private set;` 公開読み取り＋`SetImpurityCount` セッターで「単純 setter 禁止」規約を満たす。

### コードマップに無くadvisorで確定した決定（人間が照合すべき点）

1. **`OnRoomsRebuilt` はフェーズ1に存在しない** → Task 4 でフェーズ1 `CleanRoomDetectionSystem` に追加（純粋追加・既存テスト非回帰）。
2. **A_total と n·q の部屋への対応付け** → `CleanRoomPollutionInput.GetPollutionPerSecond(CleanRoom)`（per-room クエリ）と `ICleanRoomPurificationSource.RepresentativeCell`（サービスが `Contains` で振り分け）。コードマップは「flat list を room-internal でフィルタ」とだけ書くため、対応付けの具体を本プランで確定した。
3. **消滅部屋を削除しない**（Degraded化してN保持・猶予）→ Task 5（carry-over）と Task 6（grace）が一体。`OnRoomsRebuilt` でマッチしない旧状態を `_orphanStates` に退避し `OnTick` で猶予減算。
4. **ロード順 `LoadBlockDataList`→`RebuildAll()`→`Restore()`** → `LoadBlockDataList` は geometryDirty を立てるだけで再検出が次tickになるため、`RebuildAll()` を明示。
5. **按分の保存則** → `RedistributeImpurity(N_old,V_old,overlap)=N_old·overlap/V_old` の単一式に集約（`·V_new·overlapFraction` の二重係数は保存則を破るため不採用）。1対1・結合・分割の3ケースとも N が保存することを純関数の単体テスト（Task 6 Step 4）で固定する。
6. **清浄機0台の挙動** → 「発散して大Nで Out」ではなく「ACH=0 で即 Out」。N は線形に増え続けるが、クラス判定は濃度に依らず換気未達で Out。フェーズ2 DI 既定では全部屋が Out（フェーズ3で清浄機が入るまでの想定挙動、DoD に明記）。

### プレースホルダ走査

全タスクが実 C# コード（テスト＋実装）＋実 `uloop` コマンド＋実 `git` コミットを含む。"TODO"/"後で"/"similar to above" によるコード省略は無い。結合/分割の按分は散文の「最小1ケース」記述を廃し、純関数 `RedistributeImpurity` の具体的な単体テスト（split で N1+N2=N_old、merge で N_new=ΣN）へ置き換えた。仮名が残るのは Task 7 Step1 の `IWorldSaveDataLoaderAccessorOrLoaderType`（DIからローダを取る実型名）と、API確認を促す「確認」注記のみで、いずれも実装直前に既存ファイルで実名へ差し替える指示付き。
