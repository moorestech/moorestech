# クリーンルーム フェーズ4（専用機械統合：プッシュ受信 ＋ 最大グレード天井 ＋ down-bin ＋ Invalid停止）実装プラン

- **改訂: 2026-06-12 — codemap v2 整合（プッシュ型・専用機械・Vanilla非改変）＋批判的レビュー反映**
  - プル型ゲート（`ICleanRoomMachineGate` / `ICleanRoomGradeResolver`）を**全廃**し、コードマップ §1.4/§4 の**プッシュ型**（`CleanRoomDatastore` → `ICleanRoomStateReceiver.SetCleanRoomEffect`）へ全面移行。
  - **Vanilla 機械ファイル（`VanillaMachineProcessorComponent` / `VanillaMachineOutputInventory` 等）は一切変更しない（確定）**。専用 blockType `CleanRoomMachine` ＋専用コンポーネント群を新規作成する。
  - `CleanRoomClass` 列挙は廃止（コードマップ v2）。機械はプッシュ済みの `CleanRoomEffect`（`InValidRoom`/`MaxGrade`/`DownBinRate`）を読むだけ。
  - `ICチップ_Lv1..Lv4` と段間合成レシピは **items.json / craftRecipes.json へ決定的 GUID で手書き**（SourceGenerator がマスタデータを生成するという旧前提は誤り）。
  - 抽選パイプラインを設計書 §4 の処理順に固定: **EUV失敗 → 天井 → 基礎分布抽選 → down-bin → [品質シフト挿入点] → 確定**（down-bin が品質シフトより先）。EUV catastrophic 失敗（`machineRecipes` の `outputItems.percent`、現状未実装）は本フェーズが専用機械内で実装する。
  - `MaxGrade=0`（Out 相当）は**抽選せず出力なし**（サイレント Lv1 禁止）。容量予約は**空スロット方式**で down-bin 後の ItemId 不一致による出力消失を塞ぐ。抽選は**出力要素単位**（副産物を差し替えない）。
  - テスト系の修正: 電力は毎tickループ供給（既存 `MachineIOTest` 方式）／down-bin 率は `baseLv≥2` 標本に条件付け／タスク順序は「純関数 → マスタ → 専用機械配線」で前方依存を解消。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - 作業開始時に必ず `pwd` で現在の worktree を確認する（git worktree 頻用のため）。コミットは現在ディレクトリで行い、`cd` で別ツリーへ移動しない。
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` 等で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 blockType／新規 recipe／新規アイテムを認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」「ItemId/BlockId が引けない」で失敗したら uloop で Unity 再起動してから再試行。
> - 「Domain Reload in progress」エラーが出たら45秒待って再試行。
> - blockType／recipe／item スキーマ追加・SourceGenerator トリガの手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル（`.cs`／`.json`／`.yml`）編集時は AGENTS.md の「文字化け防止ワークフロー」を順守（編集前後でエンコーディング確認、`縺`/`繧`/`繝` 連続が出たら破棄して読み直し）。
> - **APIシグネチャ確認の原則:** 本プランのコードは既存コードベースのパターンから書いているが、メソッド名・名前空間・引数順は推定を含む。各 `.cs` を書く前に、本文で「確認」と指示した既存ファイルを開いて実シグネチャに合わせること。コンパイル/テストのチェックポイントが安全網。

**Goal:** 専用機械 `CleanRoomMachine`（露光装置）の出力を、その機械が入っているクリーンルームの純度に連動させる。(a) 部屋が **Invalid／部屋外**なら稼働停止（進捗凍結・壊れない）、Degraded＋猶予中は稼働継続。(b) プッシュされた `MaxGrade` で出力チップ Lv をクランプ。(c) `DownBinRate` で確率的に1段格下げ（down-bin）。(d) EUV catastrophic 失敗（`outputItems.percent`）をパイプライン先頭で実装。グレードは独立 ItemId（`ICチップ_Lv1..Lv4`、決定的 GUID 手書き）で表現し、抽選は決定的乱数（自前 `_processedCycleCount`＋`_blockInstanceId`＋出力要素インデックス＋salt 付き splitmix64）。

**Architecture:**
- **契約（正）はコードマップ v2（`2026-06-06-cleanroom-phases2-5-codemap.md`）とバランス確定書（`2026-06-06-cleanroom-balance-parameters.md`）。** 本プランと食い違いを見つけたら両書を正とし本プランを直す。フェーズ1〜3 の TDD プラン文書が旧名（`CleanRoomDetectionSystem`/`CleanRoomPurityService` 等）のままの場合も、土台の型名はコードマップの `CleanRoomDatastore`/`CleanRoom`/`cleanRoomThresholds.yml` を正とする。
- **プッシュ型（ゲート全廃）:** `CleanRoomDatastore` が毎tick、部屋内ブロックの `ICleanRoomStateReceiver.SetCleanRoomEffect(CleanRoomEffect)` を呼ぶ（電力 `EnergySegment.SupplyEnergy` と同型）。`CleanRoomEffect { InValidRoom, MaxGrade, DownBinRate }`。**未プッシュ時の初期値は `InValidRoom=false`（最悪側）**＝未配線で最高グレードが出る「安全でないフォールバック」を構造的に禁止する。機械は自分の受信値を読むだけで、`Game.Block` から `Game.CleanRoom` へのクロス asmdef 呼び出しは無い。fake の DI 差し替え機構も不要（テストは受信コンポーネントへ直接 `SetCleanRoomEffect` を呼ぶ）。
- **Vanilla 非改変（最重要・確定）:** `VanillaMachineProcessorComponent` / `VanillaMachineOutputInventory` / `VanillaMachineBlockInventoryComponent` / `VanillaMachineSaveComponent` は**読むだけ・変更禁止**（別作業系統＝アップグレード フェーズA/B が同ファイルを触るため）。専用機械のコンポーネントは Vanilla 実装を**参考にコピーして新規作成**する（未変更の `VanillaMachineInputInventory` 等を参照・再利用するのは可）。
- **抽選コアは純関数:** `SemiconductorChipDraw`（static）が「EUV失敗 → 天井クランプ → 基礎分布抽選 → down-bin → [品質シフト挿入点：将来アップグレードB] → Lv確定」を決定的に行う。部屋もDIも不要で単体テスト可能。分布はマスタ読み出し時に **level 昇順へソート**してから渡す（順序依存の排除）。
- **乱数規約:** 専用機械が自前で保存する `_processedCycleCount`＋`_blockInstanceId` から `long` seed を作り、**サブストリームごとの salt**＋**出力要素インデックス**を混合して splitmix64 で [0,1) 化する。アップグレード フェーズA の `DeterministicRoll` とは**実装独立**（規約のみ準拠）なので、フェーズA未マージでも本フェーズはブロックされない。salt 割り当て: level-draw=`0xA5A5…0001`／down-bin=`0xA5A5…0002`／EUV失敗=`0xA5A5…0003`／品質シフト（将来B）=`0xA5A5…0004` 予約。
- **抽選は出力要素単位:** レベル分布（半導体専用マスタ `semiconductorChips.yml`）を持つ出力アイテムだけ差し替え対象。副産物はベース ItemId のまま。`Count` 個は1抽選＝1スタック同一 Lv とする（§7.2「各出力に品質」は出力**要素**単位と解釈。要素ごとに独立シード）。

**Tech Stack:** C# (Unity, moorestech_server), R3/UniRx `IObservable`（`GameUpdater.UpdateObservable`）, NUnit (Server.Tests), Mooresmaster Source Generator（yamlスキーマ→C#モデル生成のみ。**マスタデータは items.json / craftRecipes.json 等へ手書き**）。

---

## 前提（このプランが乗る土台）

| 前提 | 状態 | このプランへの影響 |
|---|---|---|
| **フェーズ1〜2完了（コードマップ基準）** | `Game.CleanRoom` asmdef ＋ `CleanRoomDatastore`（部屋保持・ブロック→部屋マップ・tick購読）＋ `CleanRoom`（Cells/V/S/N/Status/ThresholdIndex）＋ `cleanRoomThresholds.yml`/`CleanRoomThresholdMaster` | Task 5 のプッシュ配線・Task 6 の実部屋統合テストの土台。**未マージでも Task 1〜4（純関数・マスタ・専用機械）は単独で完了できる**（受信コンポーネントへテストが直接 `SetCleanRoomEffect` を呼べばよい） |
| **フェーズ3完了** | エアフィルター・汚染源で実際に閾値行が動く | 実部屋で A/C 行相当を作る統合テスト（Task 6）の前提 |
| **アップグレード フェーズA/B** | **依存しない。** 乱数規約（決定的シード・順序固定 §7.2）に準拠するのみで、`DeterministicRoll`/`MachineModuleEffect` 等のフェーズA成果物は参照しない。フェーズB（`2026-06-06-upgrade-system-phase-b-quality.md`）は Vanilla 機械対象の**別worktree系統**（決着メモ再決着版参照） | フェーズA/B 未マージでも本プランは完結する |

> ⚠ **ワークツリー注意:** 実装着手前に `git log`/ファイル存在で土台（`Game.CleanRoom`）の有無を確認すること。フェーズ1〜2 未マージの場合、Task 5/6 は `Assert.Ignore` 枠で置き、Task 1〜4 のみ完了させる。

---

## File Structure（フェーズ4で作成/変更するファイル）

**マスタ（手書きデータ＋半導体専用スキーマ）**
- Modify: `VanillaSchema/blocks.yml` — blockType `CleanRoomMachine` を追加（param: `requiredPower`・入出力スロット数等。`ElectricMachine` の param を踏襲）。`edit-schema` スキル順守
- Create: `VanillaSchema/semiconductorChips.yml` — 半導体専用マスタ（チップ Lv↔ItemGuid 対応＋レシピ出力要素ごとのレベル分布）。**フェーズBの `levelFamilies.yml` とは別スキーマID＝衝突しない**
- Create: `moorestech_server/Assets/Scripts/Core.Master/SemiconductorChipMaster.cs` — アクセサ（フェーズ2の `CleanRoomThresholdMaster` と同手順で `MasterHolder` に追加）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs` — `SemiconductorChipMaster` 静的プロパティ追加
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ文字列を変更
- Modify: テスト用 mod（`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/`）
  - `items.json` — `ICチップ_Lv1..Lv4` の4アイテム（決定的 GUID **手書き**）
  - `craftRecipes.json` — 段間合成レシピ（Lv1×4→Lv2 等。**手書き**）
  - `machineRecipes.json` — 露光装置レシピ（チップ出力要素に `percent: 0.8`＝EUV 20%失敗）
  - `blocks.json` — `CleanRoomMachine` ブロック（露光装置）
  - `semiconductorChips.json` — Lv対応＋レベル分布
  - `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestItemId.cs` — `ICチップ_Lv1..Lv4` の ItemId アクセサ追加

**抽選コア（純関数）**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/SemiconductorChipDraw.cs` — EUV失敗→天井→基礎分布→down-bin→[品質シフト挿入点]→確定。**純粋・決定的・マスタ非依存のコア関数＋マスタ依存の ItemId 解決ラッパ**

**プッシュ受信（Game.Block.Interface ／ Game.Block）**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomStateReceiver.cs` — 受信IF＋`CleanRoomEffect` struct（プリミティブのみ。コードマップ §1.4）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomStateReceiverComponent.cs` — 受信値保持（初期値 `InValidRoom=false`）

**専用機械（Game.Block。Vanilla 機械ファイルは非改変）**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomMachineProcessorComponent.cs` — `VanillaMachineProcessorComponent` を参考に新規作成。Idle/Processing＋受信 `InValidRoom` ゲート＋`_processedCycleCount`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomMachineOutputInventory.cs` — `VanillaMachineOutputInventory` を参考に新規作成。空スロット予約＋出力要素単位の抽選差し替え
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomMachineBlockInventoryComponent.cs` — 入出力合成（`VanillaMachineBlockInventoryComponent` 相当）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomMachineElectricComponent.cs` — `IElectricConsumer` 薄ラッパ（`VanillaElectricMachineComponent` 相当）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomMachineSaveComponent.cs` — 状態・残tick・`_processedCycleCount`・インベントリのセーブ
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomMachineTemplate.cs` ＋ `VanillaIBlockTemplates.cs` への登録（Modify）

**プッシュ配線（Game.CleanRoom。フェーズ1〜2マージ後）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/CleanRoomEffectResolver.cs` — `CleanRoom`（Status/ThresholdIndex）→ `CleanRoomEffect` をマスタ閾値で算出する純関数
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs` — tick 末尾で部屋内の `ICleanRoomStateReceiver` へプッシュ（multi-block 占有セル判定含む）

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SemiconductorChipDrawTest.cs` — 純関数テスト（天井・決定性・down-bin条件付き率・EUV率・Out出力なし）
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachineTest.cs` — 専用機械の停止／出力／消失なし／回帰
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachinePushIntegrationTest.cs` — プッシュ配線＋実部屋統合（フェーズ1〜3未マージ時は `Assert.Ignore` 枠）

> 各 `.cs`／`.asmdef` 新規ファイルは Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## Task 1: 抽選コア純関数 SemiconductorChipDraw（マスタ非依存コア）

抽選パイプライン「EUV失敗 → 天井クランプ → 基礎分布抽選 → down-bin → [品質シフト挿入点] → Lv確定」を、分布リストと数値だけを入力に取る**完全に純粋な static 関数**として先に実装する。マスタもDIも部屋も不要なので、前方依存ゼロで最初に固められる（タスク順序の前方依存解消）。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/SemiconductorChipDraw.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SemiconductorChipDrawTest.cs`

- [ ] **Step 1: 失敗するテストを書く（天井・決定性・条件付き down-bin 率・EUV率・Out）**

```csharp
using System.Collections.Generic;
using Game.Block.Blocks.CleanRoom;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class SemiconductorChipDrawTest
    {
        // テスト用基礎分布（level 昇順）: Lv1:0.70 / Lv2:0.20 / Lv3:0.08 / Lv4:0.02
        // Base distribution for tests (ascending level order)
        private static readonly IReadOnlyList<(int level, double weight)> Dist = new (int, double)[]
        {
            (1, 0.70), (2, 0.20), (3, 0.08), (4, 0.02),
        };

        // MaxGrade=2 ではどの seed でも Lv2 を超えない（天井クランプ）
        // With MaxGrade=2, no seed ever yields above Lv2 (ceiling clamp)
        [Test]
        public void CeilingNeverExceededTest()
        {
            for (long seed = 0; seed < 1000; seed++)
            {
                var ok = SemiconductorChipDraw.TryDrawLevel(Dist, maxGrade: 2, downBinRate: 0.15, euvSuccessPercent: 1.0, seed, outputIndex: 0, out var lv);
                Assert.IsTrue(ok);
                Assert.LessOrEqual(lv, 2, $"seed {seed} exceeded ceiling");
                Assert.GreaterOrEqual(lv, 1);
            }
        }

        // 同一 seed・同一引数は常に同一結果（決定性）
        // Same seed and args always yield the same result (determinism)
        [Test]
        public void DeterministicForSameSeedTest()
        {
            var okA = SemiconductorChipDraw.TryDrawLevel(Dist, 3, 0.05, 1.0, 12345, 0, out var a);
            var okB = SemiconductorChipDraw.TryDrawLevel(Dist, 3, 0.05, 1.0, 12345, 0, out var b);
            Assert.AreEqual(okA, okB);
            Assert.AreEqual(a, b);
        }

        // 出力要素インデックスが違えば同一 seed でも独立に抽選される（相関排除）
        // Different output indices decorrelate even under the same cycle seed
        [Test]
        public void OutputIndexDecorrelatesTest()
        {
            var diff = 0;
            for (long seed = 0; seed < 1000; seed++)
            {
                SemiconductorChipDraw.TryDrawLevel(Dist, 4, 0.0, 1.0, seed, 0, out var lv0);
                SemiconductorChipDraw.TryDrawLevel(Dist, 4, 0.0, 1.0, seed, 1, out var lv1);
                if (lv0 != lv1) diff++;
            }
            Assert.Greater(diff, 0, "outputs are fully correlated across indices");
        }

        // down-bin 率は「格下げ可能な標本（baseLv≥2）」に条件付けて検証する。
        // Lv1 は下げ先が無く格下げを観測できないため、全標本に対する率は約 5%×P(baseLv≥2) に縮む。
        // Down-bin rate must be measured conditional on demotable samples (baseLv >= 2);
        // Lv1 cannot demote, so the unconditional rate shrinks to ~5% * P(baseLv>=2).
        [Test]
        public void DownBinConditionalRateApproximatelyFivePercentTest()
        {
            int eligible = 0, demoted = 0;
            for (long seed = 0; seed < 20000; seed++)
            {
                var baseLv = SemiconductorChipDraw.DrawBaseLevelForTest(Dist, maxGrade: 3, seed, outputIndex: 0);
                if (baseLv < 2) continue;
                eligible++;
                SemiconductorChipDraw.TryDrawLevel(Dist, 3, 0.05, 1.0, seed, 0, out var finalLv);
                if (finalLv < baseLv) demoted++;
            }
            Assert.Greater(eligible, 1000, "sample too small");
            var rate = demoted / (double)eligible;
            Assert.That(rate, Is.EqualTo(0.05).Within(0.015)); // 5% ± 1.5%（条件付き）
        }

        // MaxGrade=0（Out 相当）は抽選せず出力なし（false）。サイレントに Lv1 を出してはならない
        // MaxGrade=0 (Out) yields no output; silent Lv1 emission is forbidden
        [Test]
        public void MaxGradeZeroYieldsNoOutputTest()
        {
            for (long seed = 0; seed < 200; seed++)
                Assert.IsFalse(SemiconductorChipDraw.TryDrawLevel(Dist, maxGrade: 0, downBinRate: 0.0, euvSuccessPercent: 1.0, seed, 0, out _));
        }

        // EUV 成功率 0.8 → 約 20% が出力なし（catastrophic 失敗）。level 抽選とは独立（別 salt）
        // euvSuccessPercent 0.8 -> ~20% no-output, independent of the level draw (separate salt)
        [Test]
        public void EuvFailRateApproximatelyTwentyPercentTest()
        {
            int fail = 0; const int total = 20000;
            for (long seed = 0; seed < total; seed++)
                if (!SemiconductorChipDraw.TryDrawLevel(Dist, 4, 0.0, 0.8, seed, 0, out _)) fail++;
            Assert.That(fail / (double)total, Is.EqualTo(0.20).Within(0.01));
        }

        // 天井で切り落とした確率質量は比例配分で再正規化される（合計1の保存）
        // Truncated mass above the ceiling is renormalized proportionally (total probability preserved)
        [Test]
        public void TruncatedDistributionRenormalizedTest()
        {
            // dist {1:0.5, 2:0.3, 3:0.2}, ceiling=2 → P(2) = 0.3/0.8 = 0.375 ± 帯
            var dist = new (int, double)[] { (1, 0.5), (2, 0.3), (3, 0.2) };
            int lv2 = 0; const int total = 20000;
            for (long seed = 0; seed < total; seed++)
            {
                var baseLv = SemiconductorChipDraw.DrawBaseLevelForTest(dist, maxGrade: 2, seed, 0);
                if (baseLv == 2) lv2++;
            }
            Assert.That(lv2 / (double)total, Is.EqualTo(0.375).Within(0.015));
        }

        // down-bin が発火しても下げ幅は1段だけ（2段以上下げない）
        // Down-bin demotes exactly one level when it fires
        [Test]
        public void DownBinDemotesExactlyOneLevelTest()
        {
            for (long seed = 0; seed < 5000; seed++)
            {
                var baseLv = SemiconductorChipDraw.DrawBaseLevelForTest(Dist, 4, seed, 0);
                SemiconductorChipDraw.TryDrawLevel(Dist, 4, 0.35, 1.0, seed, 0, out var finalLv);
                Assert.That(baseLv - finalLv, Is.InRange(0, 1));
            }
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SemiconductorChipDrawTest"`
Expected: コンパイルエラー or FAIL（`SemiconductorChipDraw` 未実装）。

- [ ] **Step 3: SemiconductorChipDraw を実装（順序固定・salt＋出力要素インデックス付き決定的乱数）**

`Game.Block/Blocks/CleanRoom/SemiconductorChipDraw.cs`:

```csharp
using System.Collections.Generic;

namespace Game.Block.Blocks.CleanRoom
{
    // 半導体チップのレベル抽選コア。EUV失敗→天井→基礎分布→down-bin→[品質シフト挿入点]→確定。
    // 設計書§4 の処理順（down-bin が品質シフトより先）を固定する。純粋・決定的・マスタ非依存。
    // Semiconductor level-draw core: EUV fail -> ceiling -> base draw -> down-bin -> [quality-shift slot] -> confirm.
    // Pure, deterministic, master-independent; pins the spec §4 order (down-bin before quality shift).
    public static class SemiconductorChipDraw
    {
        // RNG サブストリーム salt（相関回避）。0x…0004 は将来アップグレードB の品質シフト用に予約。
        // RNG sub-stream salts; 0x…0004 is reserved for the future upgrade-B quality shift.
        private const ulong SaltLevelDraw = 0xA5A5_0000_0000_0001UL;
        private const ulong SaltDownBin = 0xA5A5_0000_0000_0002UL;
        private const ulong SaltEuvFail = 0xA5A5_0000_0000_0003UL;

        // 抽選本体。dist は level 昇順前提（マスタ読み出し側でソート済み）。false=出力なし（EUV失敗 or MaxGrade=0）。
        // Main draw. dist must be sorted ascending by level. Returns false when no output (EUV fail / MaxGrade=0).
        public static bool TryDrawLevel(
            IReadOnlyList<(int level, double weight)> dist,
            int maxGrade, double downBinRate, double euvSuccessPercent,
            long deterministicSeed, int outputIndex, out int level)
        {
            level = 0;

            // 1. EUV catastrophic 失敗（出力なし）。レベル抽選とは別 salt で独立
            // 1. EUV catastrophic failure (no output), independent salt from the level draw
            if (Roll(deterministicSeed, SaltEuvFail, outputIndex) >= euvSuccessPercent) return false;

            // 2. 天井。MaxGrade=0（Out）は抽選せず出力なし（サイレント Lv1 禁止）
            // 2. Ceiling. MaxGrade=0 (Out) -> no draw, no output (no silent Lv1)
            if (maxGrade <= 0) return false;

            // 3. 基礎分布抽選（天井超え分を切り落として比例再正規化）
            // 3. Base draw, truncating above-ceiling mass and renormalizing proportionally
            var baseLv = DrawBaseLevel(dist, maxGrade, deterministicSeed, outputIndex);
            if (baseLv <= 0) return false; // 分布が天井以下に質量を持たない（マスタ不備）→出力なし

            // 4. down-bin：DownBinRate で1段格下げ（Lv1 は下げ先無しで据え置き）
            // 4. Down-bin: demote one level at DownBinRate (Lv1 has no lower target)
            var finalLv = baseLv;
            if (finalLv > 1 && Roll(deterministicSeed, SaltDownBin, outputIndex) < downBinRate) finalLv -= 1;

            // 5. [品質シフト挿入点] 将来アップグレードB がここで上位寄せを差す（salt 0x…0004 予約）。本フェーズは中立。
            //    設計書§4 の処理順どおり down-bin の後・確定の前に置く。
            // 5. [Quality-shift slot] Upgrade-B inserts the upward shift here (salt 0x…0004 reserved); neutral now.

            level = finalLv;
            return true;
        }

        #region Test helpers
        // テストが down-bin 前後を比較するための可視ヘルパ
        // Visible helper so tests can compare pre/post down-bin
        public static int DrawBaseLevelForTest(IReadOnlyList<(int level, double weight)> dist, int maxGrade, long seed, int outputIndex)
        {
            return DrawBaseLevel(dist, maxGrade, seed, outputIndex);
        }
        #endregion

        // 基礎分布から天井以下の Lv を1つ抽選。天井以下に質量が無ければ 0（=出力なし）を返す
        // Draw one Lv (<= ceiling); returns 0 (no output) when no mass remains under the ceiling
        private static int DrawBaseLevel(IReadOnlyList<(int level, double weight)> dist, int maxGrade, long seed, int outputIndex)
        {
            double totalWeight = 0;
            foreach (var (level, weight) in dist)
                if (level <= maxGrade) totalWeight += weight;
            if (totalWeight <= 0) return 0;

            var roll = Roll(seed, SaltLevelDraw, outputIndex) * totalWeight;
            double acc = 0;
            var chosen = 0;
            foreach (var (level, weight) in dist)
            {
                if (level > maxGrade) continue;
                acc += weight;
                chosen = level; // 浮動小数の端で roll≥totalWeight の場合は最後の（=最大の）天井内 Lv
                if (roll < acc) break;
            }
            return chosen;
        }

        // salt＋出力要素インデックス付き splitmix64：seed から [0,1) を決定的に返す
        // Salted splitmix64 with output-index mixing: deterministic [0,1) from the cycle seed
        private static double Roll(long seed, ulong salt, int outputIndex)
        {
            var x = (ulong)seed * 0x9E3779B97F4A7C15UL + salt + (ulong)(outputIndex + 1) * 0xBF58476D1CE4E5B9UL;
            x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27; x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / (1UL << 53));
        }
    }
}
```

> `chosen = level` のフォールバックは dist の **level 昇順**を前提にする（降順だと浮動小数の端ケースで最低 Lv に化ける）。マスタ読み出し側（Task 2 の `SemiconductorChipMaster`）が必ずソートして返すこと。

- [ ] **Step 4: コンパイル＋テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SemiconductorChipDrawTest"`
Expected: 全 PASS。条件付き down-bin 率テストが帯から外れたら salt/seed 経路を見直す。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/SemiconductorChipDraw.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SemiconductorChipDrawTest.cs
git commit -m "feat(cleanroom): 半導体レベル抽選コア（EUV失敗→天井→分布→down-bin）を純関数で実装"
```

---

## Task 2: マスタ手書き定義（チップ4種＋合成レシピ＋半導体専用分布マスタ＋専用blockType）

`ICチップ_Lv1..Lv4` と段間合成レシピを **items.json / craftRecipes.json に決定的 GUID で手書き**する（SourceGenerator はスキーマ→C#モデル生成のみで、マスタ**データ**は生成しない）。レベル分布は半導体専用マスタ `semiconductorChips.yml`（出力要素単位）に置き、フェーズBの `levelFamilies.yml` と衝突させない。`edit-schema` スキルの手順に従うこと。

**Files:**
- Modify: `VanillaSchema/blocks.yml`（blockType `CleanRoomMachine`）
- Create: `VanillaSchema/semiconductorChips.yml`
- Create: `Core.Master/SemiconductorChipMaster.cs` ／ Modify: `Core.Master/MasterHolder.cs` ／ Modify: `Core.Master/_CompileRequester.cs`
- Modify: テスト mod の `items.json` / `craftRecipes.json` / `machineRecipes.json` / `blocks.json` / `semiconductorChips.json`
- Modify: `Tests.Module/TestMod/ForUnitTestItemId.cs`

- [ ] **Step 1: 既存スキーマ構造を確認**

`VanillaSchema/blocks.yml` の `ElectricMachine` blockType の param（`requiredPower`・スロット数）と、`machineRecipes.yml` の `outputItems`（`itemGuid`/`count`/**`percent`**（既存・0〜1・default 1。**コードでは未実装**＝本フェーズの専用機械が初めて実装する））を確認する。

- [ ] **Step 2: `semiconductorChips.yml` スキーマを追加**

半導体専用マスタ。チップの Lv↔ItemGuid 対応と、レシピ**出力要素単位**のレベル分布を持つ:

```yaml
id: semiconductorChips
type: object
properties:
- key: chipLevels
  type: array
  items:
    type: object
    properties:
    - key: level
      type: integer
    - key: itemGuid
      type: uuid
- key: outputDistributions
  type: array
  items:
    type: object
    properties:
    - key: machineRecipeGuid
      type: uuid
    - key: outputItemGuid
      type: uuid
    - key: levelWeights
      type: array
      items:
        type: object
        properties:
        - key: level
          type: integer
        - key: weight
          type: number
```

> **置き場の根拠（決着メモ再決着版）:** フェーズB は `levelFamilies.yml` を Vanilla 機械向けに作る別系統。半導体は本スキーマで自立し、将来 `levelFamilies.yml` が汎用化されたら `chipLevels` をその参照へ差し替えられるよう、Lv↔ItemId 対応の読み出しは `SemiconductorChipMaster` の1箇所に隔離する。

- [ ] **Step 3: blocks.yml に `CleanRoomMachine` blockType を追加**

`ElectricMachine` の param 構成（`requiredPower`、入出力スロット数等）を踏襲して `CleanRoomMachine` を追加。`edit-schema` スキルに従い、`_CompileRequester.cs` の `dummyText` を変更して SourceGenerator をトリガ:

```csharp
private const string dummyText = "regenerate-cleanroom-phase4";
```

- [ ] **Step 4: `SemiconductorChipMaster` アクセサを実装**

`Core.Master/SemiconductorChipMaster.cs`（フェーズ2 の `CleanRoomThresholdMaster` と同じ流儀で `MasterHolder` へ登録）:

- `ItemId GetChipItemId(int level)` — `chipLevels` から解決（ロード時に1回だけ構築）。
- `int GetChipLevel(ItemId itemId)` — 逆引き（チップでなければ -1）。
- `bool TryGetDistribution(Guid machineRecipeGuid, Guid outputItemGuid, out IReadOnlyList<(int level, double weight)> dist)` — **level 昇順にソートして返す**（Task 1 の前提）。
- 出力要素が分布を持つか＝「この出力はレベル付き半導体か」の判定はこの `TryGetDistribution` に一本化する。

- [ ] **Step 5: テスト mod にデータ追加（全て手書き・決定的 GUID）**

- `items.json`: `ICチップ_Lv1..Lv4`。GUID は固定値で手書き（例: `3a000000-0000-0000-0000-000000000001` 〜 `…0004`）。
- `craftRecipes.json`: 段間合成レシピ（`Lv1×4 → Lv2×1` 等の3本）。
- `machineRecipes.json`: 露光装置レシピ。チップ出力要素に `"percent": 0.8`（EUV 20%失敗）。**副産物（非チップ出力・`percent: 1`）も1つ入れておく**（出力要素単位差し替えのテスト用）。
- `blocks.json`: `CleanRoomMachine` の露光装置ブロック。
- `semiconductorChips.json`: `chipLevels`（Lv1..Lv4↔GUID）＋露光レシピのチップ出力要素に `levelWeights: [{level:1, weight:0.70}, {level:2, weight:0.20}, {level:3, weight:0.08}, {level:4, weight:0.02}]`。
- `ForUnitTestItemId.cs` にアクセサ追加（既存パターン）:

```csharp
        public static ItemId IcChipLv1 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000001"));
        public static ItemId IcChipLv2 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000002"));
        public static ItemId IcChipLv3 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000003"));
        public static ItemId IcChipLv4 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000004"));
```

> GUID は手書きの固定値なので「毎回変わってセーブ破損」は構造的に起きない（アップグレード設計書 §4.3 の決定的 GUID 要件を手書きで満たす）。AGENTS.md 文字化け防止ワークフロー順守。

- [ ] **Step 6: SemiconductorChipDraw に ItemId 解決ラッパを追加**

Task 1 の純関数の上に、マスタ依存の薄いラッパを足す（同ファイル）:

```csharp
        // マスタ依存ラッパ：出力要素の分布を引き、抽選結果を ItemId で返す。分布が無い出力要素は対象外（false）。
        // Master-backed wrapper: resolve the per-output distribution and return the drawn ItemId.
        public static bool TryResolveOutputItemId(
            Guid machineRecipeGuid, Guid outputItemGuid,
            int maxGrade, double downBinRate, double euvSuccessPercent,
            long deterministicSeed, int outputIndex, out ItemId itemId)
```

内部は `MasterHolder.SemiconductorChipMaster.TryGetDistribution(...)` → `TryDrawLevel(...)` → `GetChipItemId(level)`。分布を持たない出力要素では抽選せず false 相当の「対象外」を返し、呼び出し側（Task 4）はベース ItemId をそのまま使う（＝副産物は差し替えない）。「対象外」と「EUV失敗/Out で出力なし」を呼び出し側が区別できるシグネチャにすること（例: `enum DrawResult { NotLeveled, NoOutput, Drawn }` を返す）。

- [ ] **Step 7: 再生成・コンパイル確認**

Run: Unity 再起動 → `uloop compile --project-path ./moorestech_client`
Expected: 成功。`Mooresmaster.Model.SemiconductorChipsModule` が生成され、テスト mod のロードで `ForUnitTestItemId.IcChipLv1` 等が引ける。

- [ ] **Step 8: Commit**

```bash
git add VanillaSchema/ moorestech_server/Assets/Scripts/Core.Master/ moorestech_server/Assets/Scripts/Tests.Module/TestMod/ moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/SemiconductorChipDraw.cs
git commit -m "feat(cleanroom): ICチップ Lv1-4・段間合成・半導体分布マスタ・CleanRoomMachine blockType を手書き定義"
```

---

## Task 3: ICleanRoomStateReceiver ＋ CleanRoomEffect ＋ 受信コンポーネント

プッシュ受信の口を `Game.Block.Interface` に、受信値の保持を `Game.Block` に作る。**初期値（未プッシュ時）は `InValidRoom=false`（最悪側）**。テストはこの受信コンポーネントへ直接 `SetCleanRoomEffect` を呼べばよいため、**fake の DI 差し替え機構は不要**。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomStateReceiver.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomStateReceiverComponent.cs`

- [ ] **Step 1: `ICleanRoomStateReceiver` ＋ `CleanRoomEffect` を作成（コードマップ §1.4 そのまま）**

`Game.Block.Interface/Component/ICleanRoomStateReceiver.cs`:

```csharp
namespace Game.Block.Interface.Component
{
    // 部屋の効果を受け取るブロック側コンポーネント。CleanRoomDatastore が毎tickセットする。
    // Block-side receiver; CleanRoomDatastore pushes the room effect each tick.
    public interface ICleanRoomStateReceiver : IBlockComponent
    {
        void SetCleanRoomEffect(CleanRoomEffect effect);
    }

    // プッシュされる最小ペイロード（算出済みの効果値。プリミティブのみ＝Game.Block はクリーンルーム型を知らない）
    // Minimal pushed payload (already-resolved primitive effect values)
    public readonly struct CleanRoomEffect
    {
        public readonly bool InValidRoom;   // 有効な部屋内にあるか（Invalid/部屋外なら false）
        public readonly int MaxGrade;       // 最大チップグレード天井（0 = Out 相当・出力なし）
        public readonly double DownBinRate; // 汚れ由来の格下げ率

        public CleanRoomEffect(bool inValidRoom, int maxGrade, double downBinRate)
        {
            InValidRoom = inValidRoom;
            MaxGrade = maxGrade;
            DownBinRate = downBinRate;
        }
    }
}
```

- [ ] **Step 2: 受信コンポーネントを作成（初期値＝最悪側）**

`Game.Block/Blocks/CleanRoom/CleanRoomStateReceiverComponent.cs`:

```csharp
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // 受信した CleanRoomEffect を保持する。未プッシュ時の初期値は InValidRoom=false（最悪側）。
    // 未配線・未登録の機械が最高グレードで稼働する「安全でないフォールバック」を構造的に禁止する。
    // Holds the last pushed CleanRoomEffect; defaults to InValidRoom=false (worst case)
    // so an unwired machine can never run at the best grade by accident.
    public class CleanRoomStateReceiverComponent : ICleanRoomStateReceiver
    {
        public CleanRoomEffect CurrentEffect { get; private set; } = new(false, 0, 0.0);

        public void SetCleanRoomEffect(CleanRoomEffect effect)
        {
            CurrentEffect = effect;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 4: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomStateReceiver.cs moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomStateReceiverComponent.cs
git commit -m "feat(cleanroom): ICleanRoomStateReceiver/CleanRoomEffect と受信コンポーネント（初期値=最悪側）を追加"
```

---

## Task 4: 専用機械 CleanRoomMachine（プロセッサ＋出力インベントリ＋テンプレート。Vanilla 非改変）

`VanillaMachine*` を**参考にコピーして**専用コンポーネント群を新規作成する。Vanilla ファイルは1行も変更しない。停止ゲートは受信値 `InValidRoom`、出力は「EUV失敗→天井→分布→down-bin」差し替え＋空スロット予約。

**Files:**
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomMachineProcessorComponent.cs`
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomMachineOutputInventory.cs`
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomMachineBlockInventoryComponent.cs`
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomMachineElectricComponent.cs`
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomMachineSaveComponent.cs`
- Create: `Game.Block/Factory/BlockTemplate/VanillaCleanRoomMachineTemplate.cs` ／ Modify: `Game.Block/Factory/VanillaIBlockTemplates.cs`（登録のみ）
- Test: `Tests/CombinedTest/Core/CleanRoomMachineTest.cs`

- [ ] **Step 1: 失敗するテストを書く（Invalid 停止／Valid 稼働／天井クランプ／消失なし）**

電力供給は**毎tickループ**（既存 `MachineIOTest` と同じ流儀。1回だけの給電は次 tick で `_currentPower=0` にリセットされ、レシピが完了しない）:

```csharp
using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomMachineTest
    {
        // 未プッシュ（初期値 InValidRoom=false）の専用機械は、電力があっても処理を開始しない
        // An unpushed (default InValidRoom=false) machine never starts even with power
        [Test]
        public void DefaultEffectHaltsMachineTest()
        {
            var (block, proc, _) = PlaceExposureMachineWithInputs();

            RunTicksWithPower(block, 10);

            // 初期値は最悪側なので Idle のまま（開始しない）
            // Default effect is worst-case, so it stays Idle
            Assert.AreEqual(ProcessState.Idle, proc.CurrentState);
        }

        // Valid＋MaxGrade=4 をプッシュすると稼働し、チップ Lv は 1..4 の範囲（EUV失敗時は出力なしも正）
        // With Valid + MaxGrade=4 pushed, the machine runs and emits chips in Lv1..4 (EUV fail = no chip is OK)
        [Test]
        public void ValidEffectRunsAndOutputsChipTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs();
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));

            RunUntilIdle(block, proc);

            foreach (var lv in CollectOutputChipLevels(block)) Assert.That(lv, Is.InRange(1, 4));
        }

        // MaxGrade=2 では複数サイクル回しても Lv2 を超えない（天井クランプ）
        // With MaxGrade=2, no cycle ever emits above Lv2
        [Test]
        public void OutputCeilingClampTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs(cycles: 20);
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 2, 0.15));

            RunCycles(block, proc, 20);

            foreach (var lv in CollectOutputChipLevels(block))
                Assert.LessOrEqual(lv, 2);
        }

        // MaxGrade=0（Out 相当）は稼働するが出力を生成しない（不良扱い。サイレント Lv1 禁止）
        // MaxGrade=0 (Out) keeps running but emits nothing (no silent Lv1)
        [Test]
        public void MaxGradeZeroRunsButEmitsNothingTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs();
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 0, 0.0));

            RunUntilIdle(block, proc);

            // 入力は消費される（稼働は継続）が、チップは1つも出ない
            // Inputs are consumed (machine keeps operating) but no chip is emitted
            Assert.IsEmpty(CollectOutputChipLevels(block));
        }

        // 処理中に Invalid 化すると進捗が凍結し、Valid に戻ると再開する（壊れない）
        // Turning Invalid mid-process freezes progress; restoring Valid resumes (nothing breaks)
        [Test]
        public void MidProcessInvalidFreezesAndResumesTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs();
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));

            RunTicksWithPower(block, 3);
            Assert.AreEqual(ProcessState.Processing, proc.CurrentState);

            receiver.SetCleanRoomEffect(new CleanRoomEffect(false, 0, 0.0));
            var frozen = proc.RemainingTicks;
            RunTicksWithPower(block, 10);
            Assert.AreEqual(frozen, proc.RemainingTicks); // 凍結
            Assert.AreEqual(ProcessState.Processing, proc.CurrentState); // 壊れない・Idle に落ちない

            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));
            RunUntilIdle(block, proc); // 再開して完走
        }

        // 入力消費数 ＝ チップ出力数 ＋ EUV失敗数（決定的 seed で正確に一致）。サイレント消失ゼロ
        // Inputs consumed == chips emitted + EUV failures, exactly (deterministic). No silent loss.
        [Test]
        public void NoSilentOutputLossTest()
        {
            const int cycles = 30;
            var (block, proc, receiver) = PlaceExposureMachineWithInputs(cycles: cycles);
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 3, 0.35)); // down-bin 多発条件

            RunCycles(block, proc, cycles);

            // 専用機械側が数えた EUV 失敗数（テスト可視カウンタ）＋出力チップ数 ＝ サイクル数
            // Failures counted by the machine + emitted chips must equal completed cycles
            var emitted = CollectOutputChipLevels(block).Count;
            Assert.AreEqual(cycles, emitted + proc.EuvFailCountForTest);
        }

        // 副産物（分布を持たない出力要素）はベース ItemId のまま（チップに化けない）
        // By-products (outputs without a distribution) keep their base ItemId
        [Test]
        public void ByProductNotReplacedTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs();
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));

            RunUntilIdle(block, proc);

            // 露光レシピの副産物出力（テスト mod で定義）がそのままの ItemId で存在する
            // The recipe's by-product output remains with its own ItemId
            AssertByProductPresent(block);
        }

        #region Internal

        // 露光装置を設置し、cycles 回分の入力を投入する
        // Place the exposure machine and load inputs for the given cycles
        (IBlock block, CleanRoomMachineProcessorComponent proc, CleanRoomStateReceiverComponent receiver)
            PlaceExposureMachineWithInputs(int cycles = 1)
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = FindExposureRecipe();
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var inventory = block.GetComponent<CleanRoomMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count * cycles));

            return (block, block.GetComponent<CleanRoomMachineProcessorComponent>(),
                (CleanRoomStateReceiverComponent)block.GetComponent<ICleanRoomStateReceiver>());
        }

        // 毎tick給電しながら指定 tick 進める（既存 MachineIOTest 流儀。1回給電では完走しない）
        // Advance ticks while supplying power EVERY tick (MachineIOTest style)
        void RunTicksWithPower(IBlock block, int ticks)
        {
            var electric = block.GetComponent<CleanRoomMachineElectricComponent>();
            for (var i = 0; i < ticks; i++)
            {
                electric.SupplyEnergy(new ElectricPower(10000));
                GameUpdater.UpdateOneTick();
            }
        }

        void RunUntilIdle(IBlock block, CleanRoomMachineProcessorComponent proc) { /* RunTicksWithPower を Idle まで・上限tick付きで回す */ }
        void RunCycles(IBlock block, CleanRoomMachineProcessorComponent proc, int cycles) { /* 全入力消費まで毎tick給電 */ }
        System.Collections.Generic.List<int> CollectOutputChipLevels(IBlock block) { /* 出力スロットを GetChipLevel で走査 */ return null; }
        void AssertByProductPresent(IBlock block) { /* 副産物 ItemId の存在確認 */ }
        Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement FindExposureRecipe()
        {
            // semiconductorChips マスタに分布を持つレシピ＝露光レシピを引く
            // The exposure recipe is the one with a distribution entry in the semiconductorChips master
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            foreach (var o in r.OutputItems)
                if (MasterHolder.SemiconductorChipMaster.TryGetDistribution(r.MachineRecipeGuid, o.ItemGuid, out _)) return r;
            throw new Exception("exposure recipe not found");
        }

        #endregion
    }
}
```

> テストヘルパの中身（出力スロット走査の到達経路等）は実装時に `CleanRoomMachineBlockInventoryComponent` の公開面に合わせて具体化する（骨組みのまま残さない。Self-Review で走査）。`ProcessState` は Vanilla の enum（`Game.Block.Blocks.Machine`）を**参照のみ**で再利用してよい（ファイル変更ではない）。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomMachineTest"`
Expected: コンパイルエラー or FAIL（専用機械コンポーネント未実装）。

- [ ] **Step 3: 専用プロセッサを実装（Vanilla をコピーして開始・受信ゲート追加）**

`VanillaMachineProcessorComponent.cs` を**コピー**して `CleanRoomMachineProcessorComponent.cs` を作り、以下を変える（Vanilla 側は無変更）:

- コンストラクタで `CleanRoomStateReceiverComponent` と `BlockInstanceId` を受け取る。
- `Idle()` の開始条件に受信ゲートを AND:

```csharp
        private void Idle()
        {
            var isGetRecipe = _inputInventory.TryGetRecipeElement(out var recipe);

            // 受信値が InValidRoom=false（部屋外/Invalid/未プッシュ）なら開始しない
            // Do not start when the pushed effect says InValidRoom=false (outside / Invalid / not yet pushed)
            var roomAllowsStart = _receiver.CurrentEffect.InValidRoom;

            var isStartProcess = CurrentState == ProcessState.Idle && isGetRecipe && roomAllowsStart &&
                   _inputInventory.IsAllowedToStartProcess() &&
                   _outputInventory.IsAllowedToOutputItem(recipe);

            if (isStartProcess)
            {
                CurrentState = ProcessState.Processing;
                _processingRecipe = recipe;
                _processingRecipeTicks = GameUpdater.SecondsToTicks(_processingRecipe.Time);
                _inputInventory.ReduceInputSlot(_processingRecipe);
                RemainingTicks = _processingRecipeTicks;
            }
        }
```

- `Processing()` の進捗を Invalid 時に凍結:

```csharp
        private void Processing()
        {
            // 室が無効化されたら進捗を凍結する（Idle に落とさない・入力も出力も壊さない）。
            // 凍結中も _usedPower=true を立てる＝供給された電力は消費される（意図的な仕様）。
            // 「電力0と同じ」は進捗凍結のみを指し、稼働要求（電力消費）は継続する点に注意。
            // Freeze progress while the room is invalid (stay Processing; nothing breaks).
            // _usedPower stays true: supplied power IS consumed while frozen (intentional).
            if (!_receiver.CurrentEffect.InValidRoom)
            {
                _usedPower = true;
                return;
            }

            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, RequestPower);
            if (subTicks >= RemainingTicks)
            {
                RemainingTicks = 0;
                CurrentState = ProcessState.Idle;

                // サイクル完了：抽選 seed 用カウンタを前進させてから出力を確定する
                // Cycle complete: advance the deterministic cycle counter, then emit outputs
                _processedCycleCount++;
                _outputInventory.InsertOutputSlot(_processingRecipe, BuildCycleSeed());
            }
            else
            {
                RemainingTicks -= subTicks;
            }

            _usedPower = true;
        }

        // 決定的サイクル seed（自前カウンタ＋blockInstanceId。フェーズA非依存・規約のみ準拠）
        // Deterministic per-cycle seed from our own counter + blockInstanceId (phase-A independent)
        private long BuildCycleSeed()
        {
            return ((long)_blockInstanceId.AsPrimitive() << 20) ^ (long)_processedCycleCount;
        }
```

- フィールド追加: `private uint _processedCycleCount;`（**セーブ対象**。Step 5 の SaveComponent で永続化）、`private readonly BlockInstanceId _blockInstanceId;`、`private readonly CleanRoomStateReceiverComponent _receiver;`。
- テスト可視カウンタ `public int EuvFailCountForTest`（出力インベントリから失敗通知を受けて加算。`NoSilentOutputLossTest` 用）。
- フェーズ3 の `A_machine`（稼働中機械の汚染）集計用に `CurrentState` を public プロパティで公開したまま維持する（Vanilla 同様）。

> seed はサイクル単位で1つ。出力**要素**ごとの独立性は `SemiconductorChipDraw` 側の `outputIndex` 混合で確保する（Task 1）。

- [ ] **Step 4: 専用出力インベントリを実装（空スロット予約＋出力要素単位差し替え）**

`VanillaMachineOutputInventory.cs` を**コピー**して `CleanRoomMachineOutputInventory.cs` を作り、以下を変える:

`IsAllowedToOutputItem` — レベル付き出力（分布を持つ出力要素）は**空スロット方式**で予約:

```csharp
        public bool IsAllowedToOutputItem(MachineRecipeMasterElement machineRecipe)
        {
            // レベル付き出力は down-bin/効果変動で完了時 ItemId が変わり得るため、
            // 「天井Lvのスタックが入るか」ではなく「空スロットがレベル付き出力の数だけあるか」で予約する。
            // ItemId 不一致によるサイレント消失（部分的に埋まった天井Lvスロットで予約が通る穴）を塞ぐ。
            // Leveled outputs reserve EMPTY slots (one per leveled output element), because the final
            // ItemId can change via down-bin / effect drift. This closes the silent-loss hole.
            var leveledCount = 0;
            foreach (var itemOutput in machineRecipe.OutputItems)
            {
                if (MasterHolder.SemiconductorChipMaster.TryGetDistribution(machineRecipe.MachineRecipeGuid, itemOutput.ItemGuid, out _))
                {
                    leveledCount++;
                    continue;
                }

                // 非レベル出力は Vanilla と同じ判定（既存スタックへの追記も可）
                // Non-leveled outputs use the vanilla check (may append to an existing stack)
                var outputItemId = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                var outputItemStack = ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count);
                var isAllowed = OutputSlot.Aggregate(false, (current, slot) => slot.IsAllowedToAdd(outputItemStack) || current);
                if (!isAllowed) return false;
            }

            // 空スロット数 ≥ レベル付き出力要素数 を要求（MaxGrade に依存させない：処理中の効果変動でも消失しない）
            // Require empty slots >= leveled output count, independent of the current MaxGrade
            if (leveledCount > 0)
            {
                var emptySlots = OutputSlot.Count(slot => slot.Id == ItemMaster.EmptyItemId);
                if (emptySlots < leveledCount) return false;
            }

            // ... 液体チェックは Vanilla のコピーのまま ...
            return true;
        }
```

`InsertOutputSlot(MachineRecipeMasterElement machineRecipe, long cycleSeed)` — **出力要素単位**で抽選（シグネチャに seed を取る。デフォルト引数禁止＝呼び出し側のプロセッサ（Step 3）を合わせて変更済み）:

```csharp
        public void InsertOutputSlot(MachineRecipeMasterElement machineRecipe, long cycleSeed)
        {
            for (var outputIndex = 0; outputIndex < machineRecipe.OutputItems.Length; outputIndex++)
            {
                var itemOutput = machineRecipe.OutputItems[outputIndex];
                var effect = _receiver.CurrentEffect;

                // 分布を持つ出力要素だけ抽選で差し替える。EUV失敗/Out(MaxGrade=0) は出力なし。
                // Only distribution-carrying outputs are drawn; EUV fail / Out emits nothing.
                ItemId outputItemId;
                var isLeveled = MasterHolder.SemiconductorChipMaster.TryGetDistribution(
                    machineRecipe.MachineRecipeGuid, itemOutput.ItemGuid, out _);
                if (isLeveled)
                {
                    var percent = itemOutput.Percent; // EUV catastrophic 失敗率の補数（生成プロパティ名は Module で確認）
                    if (!SemiconductorChipDraw.TryResolveOutputItemId(
                            machineRecipe.MachineRecipeGuid, itemOutput.ItemGuid,
                            effect.MaxGrade, effect.DownBinRate, percent,
                            cycleSeed, outputIndex, out outputItemId))
                    {
                        _onNoOutputForTest?.Invoke(); // EUV失敗/Out のテスト可視カウンタ（消失なしアサート用）
                        continue;
                    }
                }
                else
                {
                    // 副産物（分布なし）はベース ItemId のまま
                    // By-products keep their base ItemId
                    outputItemId = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                }

                // 同 ItemId スロット→空スロットの順に格納（予約済みの空スロットが受け皿になる）
                // Insert into a matching-Id slot first, then an empty one (the reserved empty slot)
                InsertToSlot(ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count));
            }

            // ... 液体出力は Vanilla のコピーのまま ...
        }
```

> `Percent` の生成プロパティ名・optional の扱い（default 1）は `Mooresmaster.Model.MachineRecipesModule` の生成コードを開いて確認すること。**Vanilla 機械は今後も percent を無視する**（実装するのは専用機械だけ。Vanilla の挙動は変更しない）。

- [ ] **Step 5: 合成コンポーネント・テンプレート・登録**

- `CleanRoomMachineBlockInventoryComponent` — `VanillaMachineBlockInventoryComponent` をコピーし、出力側の型を `CleanRoomMachineOutputInventory` に差し替え（入力側は未変更の `VanillaMachineInputInventory` を**そのまま再利用**）。
- `CleanRoomMachineElectricComponent` — `VanillaElectricMachineComponent` をコピーし、参照先を専用プロセッサに差し替えた薄い `IElectricConsumer`。
- `CleanRoomMachineSaveComponent` — `VanillaMachineSaveComponent` をコピーし、`_processedCycleCount` を**追加で**永続化（抽選の決定性がセーブをまたいで保たれる）。
- `VanillaCleanRoomMachineTemplate` — `VanillaMachineTemplate` をコピーし、blockType `CleanRoomMachine` 用に上記コンポーネント＋ `CleanRoomStateReceiverComponent` を合成。`VanillaIBlockTemplates.cs` に登録（この Modify は機械ファイルではなく登録ポイントなので可）。

- [ ] **Step 6: コンパイル＋テスト**

Run: Unity 再起動 → `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomMachineTest"`
Expected: 全 PASS。特に `NoSilentOutputLossTest`（入力消費数＝出力数＋EUV失敗数）と `MaxGradeZeroRunsButEmitsNothingTest`。

- [ ] **Step 7: 既存機械テストの回帰確認（Vanilla 非改変の検証）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineIOTest|GearMachineIoTest|MachineFluidIOTest"`
Expected: 全 PASS。さらに `git diff --stat` で **`Game.Block/Blocks/Machine/` 配下に変更が1行も無い**ことを確認する（このプランの最重要不変条件）。

- [ ] **Step 8: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/ moorestech_server/Assets/Scripts/Game.Block/Factory/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachineTest.cs
git commit -m "feat(cleanroom): 専用機械 CleanRoomMachine（受信ゲート停止・天井/down-bin抽選・空スロット予約）を Vanilla 非改変で実装"
```

---

## Task 5: プッシュ配線（CleanRoomEffectResolver ＋ CleanRoomDatastore からのプッシュ）

`Game.CleanRoom` 側で、部屋の状態（Status/ThresholdIndex）→ `CleanRoomEffect` の算出と、毎tickの受信コンポーネントへのプッシュを実装する。**フェーズ1〜2（`CleanRoomDatastore`）マージ後に実施**。未マージ環境では本 Task の統合テストを `Assert.Ignore` 枠で置く。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/CleanRoomEffectResolver.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachinePushIntegrationTest.cs`

- [ ] **Step 1: CleanRoomEffectResolver（純関数）を実装**

```csharp
using Core.Master;
using Game.Block.Interface.Component;

namespace Game.CleanRoom.Machine
{
    // 部屋の状態（Status＋現在の閾値行）から、機械へプッシュする効果値を算出する純関数。
    // ヒステリシス（閾値行の維持）はデータストアの tick が ThresholdIndex を更新する側で担う。
    // Pure mapping from room state (Status + threshold row) to the pushed effect.
    public static class CleanRoomEffectResolver
    {
        public static CleanRoomEffect Resolve(CleanRoom room)
        {
            // Invalid は稼働不可。Valid/Degraded（猶予中）は稼働可（設計書§8）
            // Invalid -> cannot operate; Valid/Degraded (grace) -> can operate (spec §8)
            var inValidRoom = room.Status != CleanRoomRoomStatus.Invalid;

            // 閾値行（cleanRoomThresholds マスタ）から MaxGrade / DownBinRate を引く。
            // Out 行（C > 1000）は MaxGrade=0（=出力なし。バランス確定書§1）
            // Resolve MaxGrade / DownBinRate from the thresholds master; the Out row maps to MaxGrade=0
            var row = MasterHolder.CleanRoomThresholdMaster.GetRow(room.ThresholdIndex);
            return new CleanRoomEffect(inValidRoom, row.MaxGrade, row.DownBinRate);
        }
    }
}
```

> `CleanRoomThresholdMaster` の行アクセサ名はフェーズ2実装を開いて合わせる。バランス確定書 §1 の対応（A→Lv4/0%・B→Lv3/5%・C→Lv2/15%・D→Lv1/35%・Out→MaxGrade=0）は**マスタ JSON 側の数値**であり、コードにハードコードしない。

- [ ] **Step 2: CleanRoomDatastore にプッシュループを追加**

`CleanRoomDatastore.Update()` の純度更新の**後**に:

- 部屋内の内部ブロック（設置時に登録済みの「ブロック→部屋」マップ。コードマップ §1.4）のうち `ICleanRoomStateReceiver` を持つものへ `SetCleanRoomEffect(CleanRoomEffectResolver.Resolve(room))` を呼ぶ。
- **multi-block 占有判定はデータストア側の責務**: 機械の `BlockPositionInfo.MinPos..MaxPos` の全占有セルが同一の部屋の **`Cells`** に含まれるか（V ではなく Cells。占有セルは V から除外されるが Cells には含まれる。コードマップ §4／バランス確定書 §5）。含まれない（部屋外・またがり）受信ブロックには `new CleanRoomEffect(false, 0, 0)` をプッシュする。**機械側に部屋探索ループは書かない。**
- 部屋が消滅・無効化された場合も、登録済み受信ブロックへ最悪側をプッシュして取り残し（古い効果値の残留）を防ぐ。

- [ ] **Step 3: 配線テスト**

`CleanRoomMachinePushIntegrationTest.cs`（フェーズ1〜2未マージ環境では枠のみ）:

```csharp
        // 密閉室内の専用機械に Datastore が効果をプッシュし、機械が稼働する
        // The datastore pushes the effect to a machine inside a sealed room and it operates
        [Test]
        public void DatastorePushesEffectToMachineTest()
        {
            Assert.Ignore("enable after cleanroom phases 1-2 merged");
        }

        // 室境界をまたぐ multi-block 機械には InValidRoom=false がプッシュされ停止する
        // A straddling multi-block machine receives InValidRoom=false and halts
        [Test]
        public void StraddlingMachineReceivesWorstEffectTest()
        {
            Assert.Ignore("enable after cleanroom phases 1-2 merged");
        }
```

- [ ] **Step 4: コンパイル＋テスト＋Commit**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachinePushIntegrationTest.cs
git commit -m "feat(cleanroom): CleanRoomEffectResolver と Datastore からの効果プッシュ配線を実装"
```

---

## Task 6: 実部屋の統合テスト（フェーズ1〜3マージ後に有効化）

実際の密閉室で閾値行を成立させ、出力 Lv・Invalid 停止・またがり停止を end-to-end で検証する。**フェーズ1〜3 がマージされて初めて成立**するため、未マージ環境では `Assert.Ignore` で枠だけ置く。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachinePushIntegrationTest.cs`

- [ ] **Step 1: 統合テストを書く（4ケース・毎tick給電）**

```csharp
        // 基準部屋（V=75・エアフィルター1台・平衡 C=3.2＝A行）→ 露光装置の出力天井は Lv4
        // Worked-example room (V=75, one air filter, C_eq=3.2 = row A) -> ceiling Lv4
        [Test]
        public void RealRoomRowAOutputsUpToLv4Test()
        {
            // 1. 壁で 5×5×3 の密閉室＋露光装置＋エアフィルター＋接続点2（バランス確定書§4）
            // 2. 毎tick給電しつつ平衡まで tick → ThresholdIndex が A 行
            // 3. レシピ完了まで毎tick給電 → 出力チップ Lv ≤ 4・決定的 seed どおり
            Assert.Ignore("enable after phases 1-3 merged");
        }

        // C 行相当の部屋 → 出力は Lv2 以下（天井クランプ）
        // A row-C room -> outputs are Lv2 or below
        [Test]
        public void RealRoomRowCNeverExceedsLv2Test()
        {
            Assert.Ignore("enable after phases 1-3 merged");
        }

        // 壁破壊→猶予 5 秒（100 tick）超過で Invalid → 機械凍結。猶予内の再封なら稼働継続（両側を検証）
        // Broken wall past the 5s grace -> Invalid -> freeze; resealed within grace -> keeps running
        [Test]
        public void RealRoomInvalidAfterGraceHaltsMachineTest()
        {
            Assert.Ignore("enable after phases 1-3 merged");
        }

        // 室境界をまたぐ multi-block 機械 → InValidRoom=false がプッシュされ停止
        // Straddling multi-block machine -> worst effect pushed -> halts
        [Test]
        public void RealRoomStraddlingMachineHaltsTest()
        {
            Assert.Ignore("enable after phases 1-3 merged");
        }
```

- [ ] **Step 2: フェーズ1〜3 がマージ済みなら `Assert.Ignore` を外して実装**

各ケースを実部屋構築で具体化（バランス確定書 §4 の worked example で閾値行を固定。電力は**毎tickループ供給**）。Degraded 猶予は 5.0 秒＝100 tick（バランス確定書 §1.2）。猶予**内**は稼働継続・猶予**超**で停止、の両側をアサートする。

- [ ] **Step 3: テスト＋Commit**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: 全 PASS（または フェーズ1〜3 未マージ時は Ignore ＋実装済みテスト PASS）。

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachinePushIntegrationTest.cs
git commit -m "test(cleanroom): 実部屋の出力天井・猶予つき Invalid 停止・またがり停止の統合テスト"
```

---

## Self-Review（実装完了前チェック）

### コードマップ §4 カバレッジ（v2 契約）
- [ ] (a) Invalid/部屋外 停止 → Task 3＋4（受信初期値=最悪側／Idle 開始ゲート／Processing 凍結／Degraded＋猶予中は稼働継続）。「止まるが壊れない」＝進捗凍結のみ（凍結中も電力は消費。意図をコメントで明記済み）。
- [ ] (b) 最大グレード天井 → Task 1 `DrawBaseLevel`（天井超え分の切落し＋比例再正規化）＋受信 `MaxGrade`。
- [ ] (c) down-bin → Task 1 `TryDrawLevel`（受信 `DownBinRate`・1段格下げ・Lv1 据え置き）。
- [ ] プッシュ受信（ゲート全廃）→ Task 3 `ICleanRoomStateReceiver`/`CleanRoomEffect`＋Task 5 Datastore プッシュ。`Game.Block` は `Game.CleanRoom` を参照しない（受信IFは `Game.Block.Interface`・プリミティブのみ）。
- [ ] **Vanilla 機械ファイル非改変** → Task 4 Step 7 で `git diff --stat` により `Game.Block/Blocks/Machine/` 無変更を機械的に確認。
- [ ] multi-block 占有＝全占有セルが同一部屋の `Cells`（V ではない）に含まれるか、を**データストア側**で判定（機械側に部屋探索ループ無し） → Task 5 Step 2。
- [ ] EUV catastrophic 失敗（`outputItems.percent`）の実装担当はフェーズ4＝本プラン Task 1/4（宙吊りにしない）。

### 設計書 §4/§8 整合
- [ ] §4 処理順: **EUV失敗 → 天井内で生成 → down-bin → [品質モジュール上位寄せ挿入点] → 確定**（down-bin が品質シフトより**先**。Task 1 のパイプラインとコメントで固定）。
- [ ] §4 クラス効果＝バランス確定書 §1（A→Lv4/0%, B→Lv3/5%, C→Lv2/15%, D→Lv1/35%, **Out→MaxGrade=0＝出力なし**）。マスタ JSON 側で持ち、コードにハードコードしない（Task 5 resolver）。
- [ ] §8 機械関係（Valid 通常／Degraded＋猶予は稼働継続／Invalid 停止）＝ `CleanRoomEffectResolver` が Invalid のみ `InValidRoom=false`（Task 5）＋猶予内/猶予超の両側テスト（Task 6）。

### 決着メモ（再決着版）引き渡し事項カバレッジ
- [ ] 1. レベルファミリー＝半導体限定・**手書き決定的 GUID**（items.json/craftRecipes.json） → Task 2。
- [ ] 2. 分布スキーマ＝半導体専用 `semiconductorChips.yml`（出力要素単位）。フェーズBの `levelFamilies.yml` と別スキーマID＝衝突しない。Lv↔ItemId 対応は `SemiconductorChipMaster` の1箇所に隔離（将来統合点） → Task 2。
- [ ] 3. 合成順序固定（EUV→天井→分布→down-bin→[品質シフト挿入点]→確定） → Task 1。
- [ ] 4. 乱数規約＝自前 `_processedCycleCount`＋`_blockInstanceId`＋出力要素インデックス＋salt 付き splitmix64。フェーズA 非依存（規約準拠のみ） → Task 1/4。
- [ ] 5. 品質シフト挿入点（salt `0xA5A5…0004` 予約・down-bin の**後**）を温存 → Task 1 コメント位置。

### バグ再発防止チェック（2026-06-12 レビュー反映）
- [ ] 出力消失なし: 空スロット予約（MaxGrade 非依存）＋「入力消費数＝出力数＋EUV失敗数」の決定的アサート（`NoSilentOutputLossTest`）。
- [ ] Out（MaxGrade=0）でサイレント Lv1 が出ない（`MaxGradeZeroYieldsNoOutputTest` / `MaxGradeZeroRunsButEmitsNothingTest`）。
- [ ] down-bin 率テストは `baseLv≥2` に**条件付け**（無条件だと約 5%×P(baseLv≥2) に縮み、5%帯のアサートは数学的に必ず失敗する）。
- [ ] 電力は**毎tickループ供給**（1回給電では `_currentPower` が次 tick で 0 リセットされ完走しない）。
- [ ] 副産物の非差し替え（`ByProductNotReplacedTest`）＋抽選は出力要素単位＋出力要素インデックスで seed 相関排除（`OutputIndexDecorrelatesTest`）。
- [ ] 分布は `SemiconductorChipMaster` が **level 昇順ソート**して返す（抽選コアの順序依存排除）。
- [ ] タスク順序: 純関数（Task 1）→ マスタ（Task 2）→ 受信（Task 3）→ 機械（Task 4）→ 配線（Task 5）で前方依存なし。Task 1〜4 はフェーズ1〜3 未マージでも完了可能。

### プレースホルダ走査
- [ ] `TODO`/`FIXME`/未実装スタブが残っていないか全変更ファイルを grep。
- [ ] テストヘルパ（`RunUntilIdle`/`CollectOutputChipLevels` 等）が実装されているか（プラン中の骨組みのまま残さない）。
- [ ] `Percent` 生成プロパティ名・optional 扱いを `Mooresmaster.Model.MachineRecipesModule` で確認済みか。
- [ ] `_processedCycleCount` が SaveComponent で永続化され、セーブ→ロード後も抽選が決定的か。

---

## 変更ファイル総覧（フェーズ4）

| 区分 | ファイル | 種別 |
|---|---|---|
| マスタ | `VanillaSchema/blocks.yml`（`CleanRoomMachine`） | 改 |
| マスタ | `VanillaSchema/semiconductorChips.yml` | 新規 |
| マスタ | `Core.Master/SemiconductorChipMaster.cs` ／ `MasterHolder.cs` ／ `_CompileRequester.cs` | 新規/改 |
| マスタ | テスト mod `items.json`/`craftRecipes.json`/`machineRecipes.json`/`blocks.json`/`semiconductorChips.json`／`ForUnitTestItemId.cs` | 改 |
| 抽選コア | `Game.Block/Blocks/CleanRoom/SemiconductorChipDraw.cs` | 新規 |
| プッシュ | `Game.Block.Interface/Component/ICleanRoomStateReceiver.cs`（＋`CleanRoomEffect`） | 新規 |
| プッシュ | `Game.Block/Blocks/CleanRoom/CleanRoomStateReceiverComponent.cs` | 新規 |
| 専用機械 | `Game.Block/Blocks/CleanRoom/CleanRoomMachineProcessorComponent.cs` / `CleanRoomMachineOutputInventory.cs` / `CleanRoomMachineBlockInventoryComponent.cs` / `CleanRoomMachineElectricComponent.cs` / `CleanRoomMachineSaveComponent.cs` | 新規 |
| 専用機械 | `Game.Block/Factory/BlockTemplate/VanillaCleanRoomMachineTemplate.cs` ＋ `VanillaIBlockTemplates.cs`（登録） | 新規/改 |
| 配線 | `Game.CleanRoom/Machine/CleanRoomEffectResolver.cs` ／ `Game.CleanRoom/CleanRoomDatastore.cs`（プッシュ追加） | 新規/改 |
| テスト | `Tests/UnitTest/Game/SemiconductorChipDrawTest.cs` ／ `Tests/CombinedTest/Core/CleanRoomMachineTest.cs` ／ `CleanRoomMachinePushIntegrationTest.cs` | 新規 |

> **非改変（不変条件）:** `Game.Block/Blocks/Machine/` 配下（`VanillaMachineProcessorComponent` / `VanillaMachineOutputInventory` / `VanillaMachineBlockInventoryComponent` / `VanillaMachineSaveComponent` / `VanillaElectricMachineComponent` 等）は**一切変更しない**。Task 4 Step 7 で機械的に検証する。
