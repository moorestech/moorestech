# ダイシング品質モジュールの逓減カーブ Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 精密切断機（ダイシング）の品質モジュールを、1個ごとに必ず良品が伸び、伸び幅が少しずつ小さくなる「逓減カーブ」で効かせる。

**Architecture:** ダイ1個あたりで「モジュール無しの状態」（部屋の損失率・モジュール無しの表）と「十分入れた状態」（損失率×(1−maxLossReduction)・最高 Lv の表）を、近づき度 w = 1 − exp(−品質の効果合計×部屋倍率 ÷ qualityApproachScale) で混ぜる。良品の合計が w に比例するので、1個ごとの伸びが必ず逓減する。レア枠シフト（maxRareShift・最高 Lv の1個分を基準に引く計算）を消す。式は `ChipYieldFormula`、役割ごとの値の受け渡しは `CleanRoomRoleRates` に閉じ、抽選と機械UIの表示はこれまでどおりそこを共有する。

**Tech Stack:** Unity C#（サーバー Game.Block / Core.Master）、マスタスキーマ YAML（VanillaSchema → SourceGenerator）、マスタ JSON（moorestech_master）、NUnit。

## Requirements

1. 品質モジュールの個数を 0→24 と1個ずつ増やすと、ウェハー1枚あたりの良品ダイ（全 Lv の合計）が、すべてのクラス（A〜D）で毎回厳密に増える。受け入れ: `ChipYieldFormulaTest.EveryAddedModuleRaisesGoodDiesTest` が通る。
2. 伸び幅は個数が増えるほど小さくなる（1個増やしたときの増分が、その前の増分以下）。受け入れ: `ChipYieldFormulaTest.GainShrinksWithEveryAddedModuleTest` が通る。
3. v8 のマスタ値で、クラスC・品質Lv1 を9個入れたときの良品が、ウェハー1枚あたり約 66.86%（今の約 66.9% と同じ）。受け入れ: `ChipYieldFormulaTest.V8ClassCNineLv1ModulesMatchesTodayTest` が 0.6686±0.0005 で通る。
4. 表は何 Lv のチップが出るかだけを決める。品質ゲート（最高 Lv が N なら Lv N+2 以上は 0）は、どの重みでも崩れない。受け入れ: `ChipYieldFormulaTest.QualityGateHoldsForEveryWeightTest`。
5. 個数で減った失敗の分は、最高 Lv の表の良品の割合で各 Lv へ配る（レア枠だけへ回さない）。受け入れ: 混合の式が表を生き残りの重みで混ぜること（`BlendedDieRatesMixesStatesPerDieTest`）。
6. 部屋クラスを上げても、良品の合計とレア以上の期待値は減らない（Tier1〜4・1〜24個）。受け入れ: `ChipYieldFormulaTest.ClassImprovementNeverReducesGoodTotalOrUpperLevelsTest`。
7. マスタのダイシング設定に `qualityApproachScale`（正の有限値）を必須で持たせ、`maxRareShift` を消す。受け入れ: 検証器が非有限・0以下を拒否する（`CleanRoomMasterTest.DicingQualityApproachScaleMustBePositiveFiniteTest`）。
8. v8 マスタ（moorestech_master の cleanRoom.json）を ADR 0104 の値にする: `qualityApproachScale` 0.1335、`maxLossReduction` 0.8、Tier1〜5 の表を「十分入れたときの表」へ。受け入れ: 起動時の検証が通り、Requirement 3 の値が出る。
9. 品質モジュールの加工時間の延び（1個5%）、モジュール無しの表（Lv1 20%・失敗80%）、露光（EUV）の失敗率の割引（`ReducedRate`）は変えない。
10. やらないこと: 研究の要求数の変更、クライアント UI の変更、セーブ形式の変更（保存する抽選記録 `DicingPreview` の中身は計算結果だけで形は変わらない）、品質モジュール以外のモジュールの変更。

## Global Constraints

- ADR: `docs/adr/0104-dicing-quality-modules-saturate-by-count.md`（決定と数値の正）。
- 式: 近づき度 `w = 1 − exp(−max(0, qualitySum × classQualityMultiplier) ÷ qualityApproachScale)`。無しの生き残り `s0 = 1 − classDieLossRate`、十分入れた生き残り `s1 = 1 − classDieLossRate × (1 − maxLossReduction)`、生き残り `s = (1 − w)s0 + w s1`。ダイ損失率 `= 1 − s = classDieLossRate × (1 − maxLossReduction × w)`。表の重み `t = w s1 ÷ s`（s が 0 なら t = w）。生き残り1個あたりの振り分け `= (1 − t) × QualityTierDieTables[0] + t × QualityTierDieTables[qualityTier]`。
- v8 の値: `qualityApproachScale` 0.1335、`maxLossReduction` 0.8。表（levelRates [Lv1..Lv5] / failedRate）:
  - Tier0: [0.2, 0, 0, 0, 0] / 0.8（変えない）
  - Tier1: [0.83125, 0.11875, 0, 0, 0] / 0.05
  - Tier2: [0.025, 0.83125, 0.11875, 0, 0] / 0.025
  - Tier3: [0.01, 0.02, 0.83125, 0.11875, 0] / 0.02
  - Tier4: [0.005, 0.01, 0.015, 0.83125, 0.11875] / 0.02
  - Tier5: [0.005, 0.0075, 0.0125, 0.025, 0.925] / 0.025
- AGENTS.md の規約: 2行コメント（日本語→英語）、`#region Internal` はメソッド内ローカル関数のみ、try-catch 禁止、デフォルト引数禁止、1ファイル200行以下、partial 禁止、`Func<>` 禁止。
- マスタ JSON は既存の書式（2スペースインデント、ファイル末尾に改行なし）を保つ。
- 別リポジトリ（moorestech_master）の変更は push して PR を作り、本 repo の `.moorestech-external-revisions.json` のピンをその PR の push 済みコミットへ向ける。マージは通常のマージコミット（squash 禁止）。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / テスト2ファイルの200行超を別クラスへ分割。注目点のうち古いコメント2か所をFilesへ追加、式の組み立てを DicingRates へ寄せた
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱2・第3バケツ0 / 弱1（表の大小の検証）はユーザー裁定で検証を足す、弱2（生き残り率の二重導出）は未反映（下の『引き継ぎ時点で未反映の計画修正』）

---

## File Structure

| ファイル | 変更 | 責務 |
|---|---|---|
| `VanillaSchema/cleanRoom.yml` | Modify | dicings から `maxRareShift` を消し `qualityApproachScale` を足す。表の説明を「十分入れたときの表」へ |
| `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomDicingValidator.cs` | Modify | `MaxRareShift` の検査を `QualityApproachScale`（正の有限値）の検査へ置き換える |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/Yield/ChipYieldFormula.cs` | Modify | レア枠シフトの式を消し、近づき度・表の混合・損失の割引の3関数にする。`ReducedRate`（EUV 用）は残す |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/OutputRewrite/CleanRoomRoleRates.cs` | Modify | `Dicing` を新しい式でつなぐ |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs` | Modify | 使われなくなる `HighestQualityModuleEffectValue` を消す |
| `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoom.json` | Modify | `maxRareShift` → `qualityApproachScale: 1.0` |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/OutputRewrite/DicingOutputRewriter.cs` | Modify（コメントのみ） | 「レア枠シフト後」の説明を新しい式に合わせる |
| `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/CleanRoomClassYield.cs` | Modify（コメントのみ） | QualityMultiplier の説明を「品質の効きの倍率」に合わせる |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomYield/ChipYieldFormulaTest.cs` | Modify（全面書き換え） | 式の単体（近づき度・損失・混合・品質ゲート） |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomYield/ChipYieldSaturationCurveTest.cs` | Create | v8 の値での曲線の性質（v8 の基準値・1個ごとに増える・逓減・クラス単調） |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomYield/CleanRoomRoleRatesTest.cs` | Modify | テスト mod の値での期待値を新しい式へ |
| `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomMasterTest.cs` | Modify | dicings の [0,1] 検査のキーを `maxLossReduction` へ |
| `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomDicingMasterValidationTest.cs` | Create | `qualityApproachScale` の検査（既存ファイルが200行を超えないよう分ける） |
| `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Machine/Upgrade/MachineModuleQualityTierTest.cs` | Modify | `AggregateReportsHighestTierEffectValueTest` を消す |
| `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/cleanRoom.json` | Modify（別 repo） | v8 の値 |
| `.moorestech-external-revisions.json` | Modify | master ピン更新 |

配置と前例: 式は既存どおり `ChipYieldFormula`（純粋関数の静的クラス）に置き、損失率と混ぜた表を1回で返す `DicingRates` までを式側に持たせる（受け渡し役とテストが同じ組み立てを書き写さないため）。マスタ値と式の接続は既存どおり `CleanRoomRoleRates` の1か所に置く（抽選 `DicingOutputRewriter` と機械UI `GetCleanRoomYieldInfoProtocol` が共有する既存の作り）。新しい型・層・機構は足さない。

---

### Task 1: サーバーの式・スキーマ・検証器・テストを逓減カーブにする

**Files:**
- Modify: `VanillaSchema/cleanRoom.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomDicingValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/Yield/ChipYieldFormula.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/OutputRewrite/CleanRoomRoleRates.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/OutputRewrite/DicingOutputRewriter.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/CleanRoomClassYield.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoom.json`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomYield/ChipYieldFormulaTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomYield/ChipYieldSaturationCurveTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomDicingMasterValidationTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomYield/CleanRoomRoleRatesTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomMasterTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Machine/Upgrade/MachineModuleQualityTierTest.cs`

**Interfaces:**
- Consumes: 生成クラス `DicingsElement`（SourceGenerator）。スキーマ変更後は `QualityApproachScale`（float）を持ち、`MaxRareShift` を持たない。
- Produces:
  - `public static double ChipYieldFormula.ApproachWeight(double qualityEffectSum, double classQualityMultiplier, double qualityApproachScale)`
  - `public static double[] ChipYieldFormula.BlendedDieRates(IReadOnlyList<float> noModuleLevelRates, float noModuleFailedRate, IReadOnlyList<float> tierLevelRates, float tierFailedRate, double noModuleSurvival, double fullSurvival, double approachWeight, out double blendedFailedRate)`
  - `public static double ChipYieldFormula.ApproachedLossRate(double classBaseRate, double approachWeight, double maxReduction)`
  - `public static double[] ChipYieldFormula.DicingRates(IReadOnlyList<float> noModuleLevelRates, float noModuleFailedRate, IReadOnlyList<float> tierLevelRates, float tierFailedRate, double classDieLossRate, double maxLossReduction, double approachWeight, out double dieLossRate, out double failedRate)`（損失率と生き残り1個あたりの振り分けを1回で返す。`CleanRoomRoleRates.Dicing` とテストの両方が使う）
  - `public static double ChipYieldFormula.ReducedRate(double classBaseRate, double qualityEffectSum, double maxReduction)`（既存のまま。EUV 用）
  - `ChipYieldFormula.ShiftedDieRates` と `MachineModuleEffect.HighestQualityModuleEffectValue` は消える。

- [ ] **Step 1: スキーマを書き換える**

`VanillaSchema/cleanRoom.yml` の dicings で、`maxRareShift` の3行（コメント2行＋key）と表のコメントを次に置き換える（edit-schema スキルの手順に従い、SourceGenerator を回す）:

```yaml
        - key: maxLossReduction
          type: number
        # 品質の効果合計×部屋倍率がこの値のとき、表への近づき度が約63%になる尺度
        # Scale at which the quality sum times the class multiplier brings the approach weight to about 63%
        - key: qualityApproachScale
          type: number
        # 品質最高Tier 0〜最大レベルの順に並ぶダイ表。Tier0はモジュール無し、Tier t≥1 は品質モジュールを十分入れたときの表（Lv t が既定・Lv t+1 がレア・それ未満と失敗ダイが失敗枠）
        # Die tables ordered by highest quality tier 0..max level; tier 0 is without modules, tier t>=1 is the table with enough quality modules (Lv t default, Lv t+1 rare, lower levels and the failed die as the failure slot)
        - key: qualityTierDieTables
```

- [ ] **Step 2: テスト mod の値を書き換える**

`Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoom.json` の dicings[0] の `"maxRareShift": 0.2,` を `"qualityApproachScale": 1.0,` に置き換える（ほかは変えない）。

- [ ] **Step 3: 検証器を書き換える**

`CleanRoomDicingValidator.cs` の `MaxRareShift` の行を次に置き換える:

```csharp
                if (float.IsNaN(dicing.QualityApproachScale) || float.IsInfinity(dicing.QualityApproachScale) || !(0f < dicing.QualityApproachScale)) logs += $"[CleanRoomMaster] {RoleName} MachineRecipeGuid:{dicing.MachineRecipeGuid} QualityApproachScale:{dicing.QualityApproachScale} must be finite and positive\n";
```

- [ ] **Step 4: `ChipYieldFormula.cs` を全面的に書き換える**

```csharp
using System;
using System.Collections.Generic;

namespace Game.Block.Blocks.CleanRoom.Machine.Yield
{
    /// <summary>
    ///     チップ歩留まりの確率式。抽選と機械UIの表示が CleanRoomRoleRates を通して同じ式を使う
    ///     Probability formulas for chip yield; the draws and the machine UI use them through CleanRoomRoleRates
    /// </summary>
    public static class ChipYieldFormula
    {
        // 品質の効き（効果合計×部屋倍率）から、十分入れたときの表への近づき度 0〜1 を出す。1個ごとに必ず増え、増え方は逓減する
        // Approach weight 0..1 toward the full-module table from the quality sum times the class multiplier; it always rises per module with diminishing gains
        public static double ApproachWeight(double qualityEffectSum, double classQualityMultiplier, double qualityApproachScale)
        {
            var effectiveQuality = Math.Max(0d, qualityEffectSum * classQualityMultiplier);
            return 1d - Math.Exp(-effectiveQuality / qualityApproachScale);
        }

        // ダイ1個あたりで「無し」と「十分入れた」状態を近づき度で混ぜ、生き残り1個あたりのレベル別割合と失敗ダイ割合にする
        // Mix the no-module and full-module states per die by the approach weight, returning per-survivor level rates and the failed-die rate
        public static double[] BlendedDieRates(IReadOnlyList<float> noModuleLevelRates, float noModuleFailedRate, IReadOnlyList<float> tierLevelRates, float tierFailedRate, double noModuleSurvival, double fullSurvival, double approachWeight, out double blendedFailedRate)
        {
            // 生き残りの内訳で表の重みを決める。良品の合計が近づき度に比例し、1個ごとの伸びが逓減する
            // Weight the tables by where survivors come from, so the good total is linear in the weight and gains diminish per module
            var survivors = (1d - approachWeight) * noModuleSurvival + approachWeight * fullSurvival;
            // silent-ok: 全ダイが損失のとき生き残りの割合は抽選に使われない / 損失率1で全数が失敗ダイになり機械UIにもそう出る
            var tableWeight = survivors <= 0d ? approachWeight : approachWeight * fullSurvival / survivors;

            var levelCount = tierLevelRates.Count;
            var blended = new double[levelCount];
            for (var i = 0; i < levelCount; i++) blended[i] = noModuleLevelRates[i] * (1d - tableWeight) + tierLevelRates[i] * tableWeight;
            blendedFailedRate = noModuleFailedRate * (1d - tableWeight) + tierFailedRate * tableWeight;
            return blended;
        }

        // 部屋の損失率を近づき度で割り引く。近づき度1で maxReduction だけ減る
        // Discount the class loss rate by the approach weight; a weight of one removes maxReduction of it
        public static double ApproachedLossRate(double classBaseRate, double approachWeight, double maxReduction)
        {
            return classBaseRate * (1d - maxReduction * approachWeight);
        }

        // ダイシングの損失率と、生き残り1個あたりのレベル別割合・失敗ダイ割合を1回で出す
        // Dicing loss rate plus per-survivor level rates and failed-die rate in one call
        public static double[] DicingRates(IReadOnlyList<float> noModuleLevelRates, float noModuleFailedRate, IReadOnlyList<float> tierLevelRates, float tierFailedRate, double classDieLossRate, double maxLossReduction, double approachWeight, out double dieLossRate, out double failedRate)
        {
            dieLossRate = ApproachedLossRate(classDieLossRate, approachWeight, maxLossReduction);
            var noModuleSurvival = 1d - classDieLossRate;
            var fullSurvival = 1d - ApproachedLossRate(classDieLossRate, 1d, maxLossReduction);
            return BlendedDieRates(noModuleLevelRates, noModuleFailedRate, tierLevelRates, tierFailedRate, noModuleSurvival, fullSurvival, approachWeight, out failedRate);
        }

        // クラス基準率を品質合計で割り引く。割引の上限は maxReduction（EUV露光の層失敗率に使う）
        // Discount a class base rate by the quality sum, capped at maxReduction (used for the EUV layer fail rate)
        public static double ReducedRate(double classBaseRate, double qualityEffectSum, double maxReduction)
        {
            return classBaseRate * (1d - Math.Clamp(qualityEffectSum, 0d, maxReduction));
        }
    }
}
```

- [ ] **Step 5: `CleanRoomRoleRates.Dicing` を書き換える**

`CleanRoomRoleRates.cs` の `Dicing` メソッドとその直前のコメント2行を次に置き換える（`LayerFailRate` は変えない）:

```csharp
        // ダイシングの率を1回だけ計算する。近づき度で損失を割り引き、モジュール無しの表から品質最高Tier（ダイの最大レベルで頭打ち）の表へ寄せる
        // Compute dicing rates once; the approach weight discounts loss and moves the no-module table toward the highest quality tier's table (capped at the die's top level)
        public static DicingPreview Dicing(DicingRecipeRole role, CleanRoomClassYield classYield, MachineModuleEffect moduleEffect)
        {
            var approachWeight = ChipYieldFormula.ApproachWeight(moduleEffect.QualityShift, classYield.QualityMultiplier, role.Dicing.QualityApproachScale);
            var qualityTier = Math.Min(moduleEffect.HighestQualityModuleTier, role.DieLevelCount);
            var noModuleTable = role.Dicing.QualityTierDieTables[0];
            var tierTable = role.Dicing.QualityTierDieTables[qualityTier];
            var levelRates = ChipYieldFormula.DicingRates(noModuleTable.LevelRates, noModuleTable.FailedRate, tierTable.LevelRates, tierTable.FailedRate, classYield.DieLossRate, role.Dicing.MaxLossReduction, approachWeight, out var lossRate, out var failedRate);
            return new DicingPreview(lossRate, qualityTier, levelRates, failedRate);
        }
```

- [ ] **Step 6: `MachineModuleEffect` から `HighestQualityModuleEffectValue` を消す**

フィールドとそのコメント2行、コンストラクタの引数 `float highestQualityModuleEffectValue` と代入、`Aggregate` の `highestQualityEffectValue` 変数を消す。`return` は `new MachineModuleEffect(processingTimeMultiplier, powerMultiplier, extraOutputChance, qualityShift, highestQualityTier)` にする。ローカル関数 `UpdateHighestQuality` とその `#region Internal` を消し、呼び出し行を次にする:

```csharp
                        if (0 < count) highestQualityTier = Math.Max(highestQualityTier, module.Tier);
```

`MachineModuleQualityTierTest.cs` の `AggregateReportsHighestTierEffectValueTest`（`[Test]` からメソッド末尾まで）を消す。

古い説明を直す（コードは変えない）:
- `DicingOutputRewriter.cs` のクラス直前2行を次にする:

```csharp
    // ダイシング: 1枚を diesPerWafer 個に切り、損失を失敗ダイへ、残りを品質の近づき度で混ぜた表でレベル別か失敗ダイへ振り分ける
    // Dicing: cut one wafer into diesPerWafer dies, send losses to failed dies and split survivors by the table blended by the quality approach weight
```

- `CleanRoomClassYield.cs` の summary 2行を次にする:

```csharp
    ///     部屋クラスの歩留まり値（ダイシングの品質の効きの倍率・EUVの層失敗率・ダイシングの損失率）。稼働できる部屋の効果だけが持つ
    ///     Per-class yield values (dicing quality effect multiplier, EUV layer fail rate, dicing loss rate); only an operable room effect carries them
```

- [ ] **Step 7: `ChipYieldFormulaTest.cs` を全面的に書き換え、`ChipYieldSaturationCurveTest.cs` を作る**

式の単体は `ChipYieldFormulaTest`、v8 の値での曲線の性質は `ChipYieldSaturationCurveTest` に分ける（1ファイル200行以下）。

`ChipYieldFormulaTest.cs`:

```csharp
using System.Linq;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class ChipYieldFormulaTest
    {
        // モジュール無しの表とTier1の十分入れたときの表（Lv1..Lv5）と失敗ダイ
        // The no-module table and the tier-1 full-module table (Lv1..Lv5) with the failed die
        private static readonly float[] NoModuleLevels = { 0.2f, 0f, 0f, 0f, 0f };
        private const float NoModuleFailed = 0.8f;
        private static readonly float[] Tier1Levels = { 0.83125f, 0.11875f, 0f, 0f, 0f };
        private const float Tier1Failed = 0.05f;

        [Test]
        public void ReducedRateClampsQualityReductionTest()
        {
            Assert.AreEqual(0.07, ChipYieldFormula.ReducedRate(0.10, 0.3, 0.8), 1e-9);
            Assert.AreEqual(0.02, ChipYieldFormula.ReducedRate(0.10, 2.0, 0.8), 1e-9);
            Assert.AreEqual(0.10, ChipYieldFormula.ReducedRate(0.10, 0.0, 0.8), 1e-9);
        }

        [Test]
        public void ApproachWeightIsZeroWithoutQualityAndKnownAtNineModulesTest()
        {
            Assert.AreEqual(0.0, ChipYieldFormula.ApproachWeight(0, 0.5, 0.1335), 1e-12);
            Assert.AreEqual(0.0, ChipYieldFormula.ApproachWeight(-1, 0.5, 0.1335), 1e-12);

            // クラスC・Lv1を9個: 1−exp(−0.36×0.5÷0.1335)
            // Class C with nine Lv1: 1 - exp(-0.36 x 0.5 / 0.1335)
            Assert.AreEqual(0.740322, ChipYieldFormula.ApproachWeight(0.36, 0.5, 0.1335), 1e-6);
        }

        [Test]
        public void ApproachedLossRateScalesByWeightTest()
        {
            Assert.AreEqual(0.45, ChipYieldFormula.ApproachedLossRate(0.45, 0, 0.8), 1e-12);
            Assert.AreEqual(0.27, ChipYieldFormula.ApproachedLossRate(0.45, 0.5, 0.8), 1e-12);
            Assert.AreEqual(0.09, ChipYieldFormula.ApproachedLossRate(0.45, 1, 0.8), 1e-12);
        }

        [Test]
        public void BlendedDieRatesMixesStatesPerDieTest()
        {
            // 重み0はモジュール無しの表、重み1は最高Tierの表
            // Weight 0 is the no-module table and weight 1 the tier table
            AssertRates(Blend(0.55, 0.91, 0), 0.2, 0, 0, 0, 0, 0.8);
            AssertRates(Blend(0.55, 0.91, 1), 0.83125, 0.11875, 0, 0, 0, 0.05);

            // 生き残りが同じなら中間は表の真ん中
            // With equal survival the midpoint is the middle of the tables
            AssertRates(Blend(1, 1, 0.5), 0.515625, 0.059375, 0, 0, 0, 0.425);

            // 十分入れた側の生き残りが多いほど、生き残りは最高Tierの表へ寄る（表の重み 0.5×0.91÷0.73）
            // More full-module survivors pull survivors toward the tier table (table weight 0.5 x 0.91 / 0.73)
            AssertRates(Blend(0.55, 0.91, 0.5), 0.593450, 0.074015, 0, 0, 0, 0.332534);

            // 全ダイが損失のときも合計1を保つ
            // The sum stays one even when every die is lost
            Assert.AreEqual(1.0, Blend(0, 0, 0.5).Sum(), 1e-9);
        }

        [Test]
        public void DicingRatesReturnsLossAndPerSurvivorRatesTest()
        {
            // クラスC(損失0.45)・重み0.5: 損失0.27、生き残りの内訳は Blend(0.55, 0.91, 0.5) と同じ
            // Class C (loss 0.45) at weight 0.5: loss 0.27, survivors split the same as Blend(0.55, 0.91, 0.5)
            var rates = ChipYieldFormula.DicingRates(NoModuleLevels, NoModuleFailed, Tier1Levels, Tier1Failed, 0.45, 0.8, 0.5, out var dieLossRate, out var failedRate);
            Assert.AreEqual(0.27, dieLossRate, 1e-12);
            AssertRates(rates.Append(failedRate).ToArray(), 0.593450, 0.074015, 0, 0, 0, 0.332534);
        }

        [Test]
        public void QualityGateHoldsForEveryWeightTest()
        {
            // どの重みでも Tier t で Lv t+2 以上は0、合計は1（Tier2の表で確かめる）
            // At any weight, tier t keeps Lv t+2 and above at zero and the sum at one (checked on the tier-2 table)
            var tier2Levels = new[] { 0.025f, 0.83125f, 0.11875f, 0f, 0f };
            foreach (var weight in new[] { 0.0, 0.1, 0.5, 0.9, 1.0 })
            {
                var rates = ChipYieldFormula.BlendedDieRates(NoModuleLevels, NoModuleFailed, tier2Levels, 0.025f, 0.55, 0.91, weight, out var failedRate);
                Assert.AreEqual(0.0, rates[3], 1e-12, $"weight {weight} Lv4");
                Assert.AreEqual(0.0, rates[4], 1e-12, $"weight {weight} Lv5");
                Assert.AreEqual(1.0, rates.Sum() + failedRate, 1e-6, $"weight {weight} sum");
            }
        }

        #region TestHelper

        // Tier1の表で混ぜた結果を [Lv1..Lv5, 失敗ダイ] に並べる
        // Lay the tier-1 blend out as [Lv1..Lv5, failed die]
        private static double[] Blend(double noModuleSurvival, double fullSurvival, double weight)
        {
            var rates = ChipYieldFormula.BlendedDieRates(NoModuleLevels, NoModuleFailed, Tier1Levels, Tier1Failed, noModuleSurvival, fullSurvival, weight, out var failedRate);
            return rates.Append(failedRate).ToArray();
        }

        private static void AssertRates(double[] actual, params double[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], actual[i], 1e-6, $"index {i}");
        }

        #endregion
    }
}
```

`ChipYieldSaturationCurveTest.cs`（新規）:

```csharp
using System.Linq;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class ChipYieldSaturationCurveTest
    {
        // v8 のクラスA→D（品質倍率・ダイ損失率）
        // v8 classes A to D (quality multiplier, die loss rate)
        private static readonly (double qualityMultiplier, double dieLossRate)[] ClassRows =
        {
            (1.0, 0.15),
            (0.75, 0.3),
            (0.5, 0.45),
            (0.25, 0.6),
        };

        // v8 の品質Tier別ダイ表（Lv1..Lv5）と失敗ダイ。Tier0はモジュール無し、Tier1以上は十分入れたときの表
        // v8 per-quality-tier die tables (Lv1..Lv5) and the failed die; tier 0 is without modules, tier 1+ is the full-module table
        private static readonly (float[] levels, float failed)[] TierTables =
        {
            (new[] { 0.2f, 0f, 0f, 0f, 0f }, 0.8f),
            (new[] { 0.83125f, 0.11875f, 0f, 0f, 0f }, 0.05f),
            (new[] { 0.025f, 0.83125f, 0.11875f, 0f, 0f }, 0.025f),
            (new[] { 0.01f, 0.02f, 0.83125f, 0.11875f, 0f }, 0.02f),
            (new[] { 0.005f, 0.01f, 0.015f, 0.83125f, 0.11875f }, 0.02f),
            (new[] { 0.005f, 0.0075f, 0.0125f, 0.025f, 0.925f }, 0.025f),
        };

        // v8 の品質モジュールLv1〜5の効果値（添字=Tier）
        // v8 quality module effect values for Lv1-5 (index = tier)
        private static readonly double[] EffectValueByTier = { 0, 0.04, 0.06, 0.08, 0.10, 0.12 };

        private const double QualityApproachScale = 0.1335;
        private const double MaxLossReduction = 0.8;
        private const int MaxModuleCount = 24;

        [Test]
        public void V8ClassCNineLv1ModulesMatchesTodayTest()
        {
            // クラスC・Lv1を9個で、ウェハー1枚あたりの良品が今と同じ約66.86%
            // Class C with nine Lv1 keeps good dies per wafer at about 66.86%, the same as today
            var dies = GoodDieShares(ClassRows[2], 1, 9);
            Assert.AreEqual(0.6686, dies.Sum(), 5e-4);
            Assert.AreEqual(0.5886, dies[0], 5e-4);
            Assert.AreEqual(0.0800, dies[1], 5e-4);
        }

        [Test]
        public void EveryAddedModuleRaisesGoodDiesTest()
        {
            // どのクラス・Tierでも、0→24個の1個ごとに良品の合計が厳密に増える
            // For every class and tier, each added module from 0 to 24 strictly raises the good total
            for (var tier = 1; tier < TierTables.Length; tier++)
            {
                foreach (var classRow in ClassRows)
                {
                    for (var count = 1; count <= MaxModuleCount; count++)
                    {
                        var before = count == 1 ? GoodDieShares(classRow, 0, 0).Sum() : GoodDieShares(classRow, tier, count - 1).Sum();
                        Assert.Greater(GoodDieShares(classRow, tier, count).Sum(), before, $"tier {tier} class {classRow} count {count}");
                    }
                }
            }
        }

        [Test]
        public void GainShrinksWithEveryAddedModuleTest()
        {
            // 1個増やしたときの増分は、その前の増分以下（2個目以降）
            // The gain from one more module never exceeds the previous gain (from the second module on)
            for (var tier = 1; tier < TierTables.Length; tier++)
            {
                foreach (var classRow in ClassRows)
                {
                    for (var count = 2; count < MaxModuleCount; count++)
                    {
                        var previousGain = GoodDieShares(classRow, tier, count).Sum() - GoodDieShares(classRow, tier, count - 1).Sum();
                        var nextGain = GoodDieShares(classRow, tier, count + 1).Sum() - GoodDieShares(classRow, tier, count).Sum();
                        Assert.LessOrEqual(nextGain, previousGain + 1e-12, $"tier {tier} class {classRow} count {count}");
                    }
                }
            }
        }

        [Test]
        public void ClassImprovementNeverReducesGoodTotalOrUpperLevelsTest()
        {
            // クラスを上げると、良品の合計とレア以上の期待値が減らない（Tier1〜4・1〜24個の総当たり）
            // A better class never lowers the good total or the rare-and-above share (tiers 1-4 x 1-24 modules)
            for (var tier = 1; tier <= 4; tier++)
            {
                for (var count = 1; count <= MaxModuleCount; count++)
                {
                    for (var better = 0; better < ClassRows.Length - 1; better++)
                    {
                        var betterDies = GoodDieShares(ClassRows[better], tier, count);
                        var worseDies = GoodDieShares(ClassRows[better + 1], tier, count);
                        var label = $"tier {tier} count {count} rows {better}/{better + 1}";
                        Assert.GreaterOrEqual(betterDies.Sum() + 1e-9, worseDies.Sum(), $"total {label}");
                        Assert.GreaterOrEqual(betterDies.Skip(tier).Sum() + 1e-9, worseDies.Skip(tier).Sum(), $"rare+ {label}");
                    }
                }
            }
        }

        // 本番の式（近づき度→損失と表の混合）を通し、ダイシング投入ウェハー1枚あたりのレベル別良品ダイの割合を出す
        // Run the production formulas (approach weight, then loss and table blend) for the good-die share per diced wafer by level
        private static double[] GoodDieShares((double qualityMultiplier, double dieLossRate) classRow, int tier, int moduleCount)
        {
            var weight = ChipYieldFormula.ApproachWeight(EffectValueByTier[tier] * moduleCount, classRow.qualityMultiplier, QualityApproachScale);
            var rates = ChipYieldFormula.DicingRates(TierTables[0].levels, TierTables[0].failed, TierTables[tier].levels, TierTables[tier].failed, classRow.dieLossRate, MaxLossReduction, weight, out var dieLossRate, out _);
            return rates.Select(rate => rate * (1d - dieLossRate)).ToArray();
        }
    }
}
```

- [ ] **Step 8: `CleanRoomRoleRatesTest.cs` の2つのテストを書き換える**

テスト mod の値（`qualityApproachScale` 1.0、TestQualityModule は Tier1・効果1.0、TestQualityFractionalModule は Tier2・効果0.4）での期待値に置き換える。`DicingRatesOf` と `AssertRates` は変えない。

```csharp
        [Test]
        public void ClassMultiplierBelowOneSlowsTheApproachTest()
        {
            // クラスB(倍率0.75・損失0)×Tier1モジュール1個: 近づき度 1−exp(−0.75)=0.527633 でモジュール無しの表からTier1の表へ寄る
            // Class B (multiplier 0.75, no loss) with one tier-1 module: weight 1-exp(-0.75)=0.527633 moves the no-module table toward tier 1
            var rates = DicingRatesOf(new CleanRoomClassYield(0.75f, 0f, 0f), TestQualityModuleItemGuid, 1, out var qualityTier);
            Assert.AreEqual(1, qualityTier);
            AssertRates(rates, 0.463817, 0.052763, 0.0, 0.483420);
        }

        [Test]
        public void HighestTierTablePicksLevelsAndEffectSumSetsTheWeightTest()
        {
            // クラスA×Tier2の小数モジュール2個: 合計0.8で近づき度0.550671、表はTier2
            // Class A with two tier-2 fractional modules: sum 0.8 gives weight 0.550671 toward the tier-2 table
            var rates = DicingRatesOf(new CleanRoomClassYield(1f, 0f, 0f), TestQualityFractionalModuleItemGuid, 2, out var qualityTier);
            Assert.AreEqual(2, qualityTier);
            AssertRates(rates, 0.144933, 0.385470, 0.055067, 0.414530);
        }
```

- [ ] **Step 9: `CleanRoomMasterTest.cs` を直す**

`RoleChanceRequiresFiniteUnitIntervalTest` のキーの行を次にする:

```csharp
            var key = role == "layerExposures" ? "maxFailReduction" : "maxLossReduction";
```

次のテストを新しいファイル `Tests/CombinedTest/Core/CleanRoom/CleanRoomDicingMasterValidationTest.cs` に置く（既存の `CleanRoomMasterTest.cs` が200行を超えないよう分ける）:

```csharp
using System.IO;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomDicingMasterValidationTest
    {
        [Test]
        public void DicingQualityApproachScaleMustBePositiveFiniteTest(
            [Values(float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -1f, 0.1335f)] float value)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var json = JObject.Parse(File.ReadAllText(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "cleanRoom.json")));

            // 近づき度の尺度は正の有限値だけを通す
            // The approach scale accepts only positive finite values
            json["data"][0]["dicings"][0]["qualityApproachScale"] = value;
            var valid = new CleanRoomMaster(json).Validate(out var logs);
            Assert.AreEqual(value == 0.1335f, valid, logs);
            if (!valid) StringAssert.Contains("QualityApproachScale", logs);
        }
    }
}
```

`CleanRoomMaster` の名前空間は既存の `CleanRoomMasterTest.cs` の using（`Core.Master`）に合わせる。

- [ ] **Step 10: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ChipYieldFormulaTest|ChipYieldSaturationCurveTest|CleanRoomRoleRatesTest|CleanRoomMasterTest|CleanRoomDicingMasterValidationTest|MachineModuleQualityTierTest|CleanRoom|Dicing"`
Expected: 全 PASS

- [ ] **Step 11: コミットする**

```bash
git add VanillaSchema/cleanRoom.yml moorestech_server/Assets/Scripts docs/adr/0104-dicing-quality-modules-saturate-by-count.md docs/superpowers/plans/2026-11-20-dicing-quality-saturation.md CONTEXT.md .decisions
git commit -m "feat(cleanroom): ダイシングの品質モジュールを個数で逓減しながら効かせる"
```

### Task 2: v8 マスタの値を入れ、ピンを更新する

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/cleanRoom.json`（別 repo）
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: Task 1 のスキーマ（dicings に `qualityApproachScale` が必須、`maxRareShift` が無い）。
- Produces: v8 マスタのコミット（PR 済み）と、それを指すピン。

- [ ] **Step 1: moorestech_master の作業ブランチを用意する**

```bash
git -C ../moorestech_master fetch origin
git -C ../moorestech_master worktree add ../moorestech_master-wt-dicing -b feature/dicing-quality-saturation origin/master
```

- [ ] **Step 2: cleanRoom.json を書き換える**

書式（2スペースインデント・ensure_ascii なし・末尾改行なし）を保って次を当てる。実行前に、原文を同じ設定で読み書きして完全一致することを確かめる。

```bash
python3 - <<'PY'
import json
p='../moorestech_master-wt-dicing/server_v8/mods/moorestechAlphaMod_8/master/cleanRoom.json'
raw=open(p,encoding='utf-8').read()
d=json.loads(raw)
assert json.dumps(d,ensure_ascii=False,indent=2)==raw, 'round-trip mismatch'
dic=d['data'][0]['dicings'][0]
new={}
for k,v in dic.items():
    if k=='maxRareShift':
        new['qualityApproachScale']=0.1335
        continue
    new[k]=v
new['maxLossReduction']=0.8
new['qualityTierDieTables']=[
 {"levelRates":[0.2,0,0,0,0],"failedRate":0.8},
 {"levelRates":[0.83125,0.11875,0,0,0],"failedRate":0.05},
 {"levelRates":[0.025,0.83125,0.11875,0,0],"failedRate":0.025},
 {"levelRates":[0.01,0.02,0.83125,0.11875,0],"failedRate":0.02},
 {"levelRates":[0.005,0.01,0.015,0.83125,0.11875],"failedRate":0.02},
 {"levelRates":[0.005,0.0075,0.0125,0.025,0.925],"failedRate":0.025},
]
d['data'][0]['dicings'][0]=new
open(p,'w',encoding='utf-8').write(json.dumps(d,ensure_ascii=False,indent=2))
PY
git -C ../moorestech_master-wt-dicing diff --stat
```

Expected: cleanRoom.json だけが変わる。round-trip が一致しない場合は止まって、indent・数値表記を原文に合わせてから当て直す。

- [ ] **Step 3: マスタをコミット・push・PR 作成する**

```bash
git -C ../moorestech_master-wt-dicing add server_v8/mods/moorestechAlphaMod_8/master/cleanRoom.json
git -C ../moorestech_master-wt-dicing commit -m "balance(cleanroom): ダイシングの品質モジュールを個数で逓減しながら効かせる値にする"
git -C ../moorestech_master-wt-dicing push -u origin feature/dicing-quality-saturation
gh pr create -R moorestech/moorestech_master --head feature/dicing-quality-saturation --base master --title "balance(cleanroom): ダイシングの品質モジュールを逓減カーブにする" --body "moorestech 側 ADR 0104 の値（qualityApproachScale 0.1335・maxLossReduction 0.8・十分入れたときの表）。moorestech 側の PR と同時にマージする。"
```

- [ ] **Step 4: ピンを更新する**

`.moorestech-external-revisions.json` の `moorestech_master` の `commitHash` を Step 3 で push したコミットのハッシュ（`git -C ../moorestech_master-wt-dicing rev-parse HEAD`）にする。`../moorestech_master` を同じコミットにして、v8 マスタで起動時の検証が通ることを確かめる（クライアントの起動検証テスト、または `uloop run-tests --filter-type regex --filter-value "MasterValidation|V8"` のうちこの repo にあるもの）。

- [ ] **Step 5: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore(pin): ダイシングの逓減カーブの値を入れたマスタへピンを更新"
```

### Task 3: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

### Task 4: セッション終了可能状態にする

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR 本文には moorestech_master 側の PR を先にマージすることを書く。

---

## 引き継ぎ時点で未反映の計画修正（受け取ったセッションが、実装の前にこの plan へ反映する）

1. **生き残り率の導出を1本にする（Phase 2.6 弱2）。** 今の plan では `DicingRates` が損失率を `ApproachedLossRate` で出し、`BlendedDieRates` が中で生き残りを出し直している。`DicingRates` の中で生き残り `s = (1 − w)s0 + w s1` を1回だけ出し、損失率 `= 1 − s`、表の重み `t = (s ≤ 0 ? w : w s1 ÷ s)` を求め、`BlendedDieRates` は表の重み `t` だけを受け取る線形の混合（`BlendedDieRates(noModuleLevelRates, noModuleFailedRate, tierLevelRates, tierFailedRate, double tableWeight, out double blendedFailedRate)`）に変える。テストもそれに合わせる（生き残りで重みが変わるケースは `DicingRatesReturnsLossAndPerSurvivorRatesTest` 側へ。全ダイが損失のケースは `DicingRates(..., classDieLossRate 1, maxLossReduction 0, ...)` で損失1・合計1を確かめる）。
2. **ダイ表の大小を起動時に検証する（ユーザー裁定 2026-11-20「じゃあ入れといて」、`.decisions/2026-11-20-ダイ表の十分入れた表は良品割合がモジュール無しより大きいことを起動時に検証する.md`）。** Tier1〜5 の表の良品の割合（`1 − failedRate`、すなわち levelRates の合計）が Tier0 の表より大きくなければ、起動時のマスタ検証でログを出して止める。置き場は既存の表の規則 `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomYieldTableRules.cs`（`ValidateDieTables`）を第一候補にし、呼び出し元をまず grep で確かめる。テストは既存の `Tests/UnitTest/Game/CleanRoomYield/CleanRoomYieldTableRulesTest.cs` に、Tier1 の表を良品10%（failedRate 0.9）にすると止まるケースと、今の v8 の値が通るケースを足す（ファイルが200行を超えるなら別クラスへ）。File Structure・Task 1 の Files・Requirements にも行を足す。
3. 1・2 を反映したら、変えた範囲に spec-architecture-review（Phase 1〜2.5 と Phase 2.6）を fresh-context で回し直し、その後 moores-writing-plans の反証検査（plan-review-lenses 6本、opus subagent 1体）を回してから実装に入る。

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0104-dicing-quality-modules-saturate-by-count.md`。裁定: `.decisions/2026-11-20-ダイシングの品質モジュールは個数で逓減しながら効かせる.md`、`.decisions/2026-11-20-品質モジュールで減った失敗分は表の良品の割合どおり各Lvへ配る.md`、`.decisions/2026-11-20-ダイシングの逓減カーブは24個までよく伸ばしクラスCだけ今に合わせ加工時間5パーセントは据え置く.md`。
- タスク分割: スキーマのキーを消すと式・検証器・テスト mod・テストが同時にコンパイル・ロード不能になるため、サーバー側は1タスクにまとめる（agent前提）。
- unityプレイ録画テストは入れない: 変えるのはサーバーの率の計算式だけで、抽選の経路・機械UIへの送信・クライアント表示・入力・カメラ・エンティティ表示は変えない。機械UIは同じ `CleanRoomRoleRates` の値を既存プロトコルで表示する（agent前提）。
- `MachineModuleEffect.HighestQualityModuleEffectValue` は唯一の使い手（レア枠シフトの基準）が消えるので消す（agent前提。YAGNI）。
- テスト mod の `qualityApproachScale` は 1.0（テスト用モジュールの効果値 1.0/0.4 で期待値を手計算しやすくするため。agent前提）。
- `VanillaSchema/cleanRoom.yml`: dicings の `maxRareShift` を消し `qualityApproachScale` を必須キーで足す。optional・既定値は付けず、テスト mod と v8 マスタの JSON を同じ変更で一括更新する（AGENTS.md「変更の波及を恐れない」。agent前提）。
- `CleanRoomDicingValidator.cs`: `qualityApproachScale` は正の有限値だけを通す。0 は近づき度の式で割り算になるため起動時の検証で止める（AGENTS.md「必須の値は最も早い段階で止める」。agent前提）。
- ダイ表の大小の起動時検証を足す: ユーザー裁定 2026-11-20 原文「じゃあ入れといて」（`.decisions/2026-11-20-ダイ表の十分入れた表は良品割合がモジュール無しより大きいことを起動時に検証する.md`）。
