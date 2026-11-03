# チップ品質のダイシング決定化と品質モジュール幹の研究ツリー Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ウェハグレードを廃止してダイのレベル配分をダイシング装置の品質モジュールで決める方式へ移し、v8 の半導体以降の研究を「品質モジュールLv1〜5を幹に各モジュール・GPU・スタック拡張が開く」形に組み直す。

**Architecture:** サーバーは成膜のグレード抽選（WaferGrade 役割）を削除し、ダイシングの抽選を「品質最高Tierで選ぶ6段のダイ表＋品質効果合計と部屋クラスによるレア枠シフト」へ置き換える。プレビュー・プロトコル・WebUI のダイシング表示はレベル別の割合を出す形に変え、トランジスタ設置済みウェハを作るレシピは役割なし（NoRole）になる。v8 マスタからは成膜装置を削除し、そのレシピを EUV露光式半導体製造装置へ移す。セーブは V8→V9 の移行でグレード付きウェハを G1 へ寄せ、旧抽選記録と旧継承レベルを捨てる。マスタ（moorestech_master の v8 mod）は品目・レシピ・クリーンルーム表・研究ツリー・nodeGraph を新方式に合わせ、本 repo のピンをそのコミットへ更新する。

**Tech Stack:** Unity C#（moorestech_server / moorestech_client）、Mooresmaster SourceGenerator（VanillaSchema/*.yml）、Newtonsoft.Json（セーブ移行）、MessagePack（プロトコル）、TypeScript/React + zod（moorestech_web/webui）、Python 3（moorestech_master/tools）。

## Requirements

設計 ADR: `docs/adr/0094-chip-level-by-dicing-quality-tier-and-quality-led-research.md`、裁定 `.decisions/2026-11-02-*.md`（11件）。

### チップ抽選（サーバー）
- R1: ウェハグレードを廃止する。成膜は役割なしの普通の工程になり、昇格抽選・`waferGradeDraws`・`WaferGradeRecipeRole`・`WaferGradeOutputRewriter`・`WaferGradePreview`・関連バリデータとプロトコル種別を削除する。受け入れ: `git grep -n -i "wafergrade\|promotionchance\|maxwafergrade\|gradedietables"` が moorestech_server・moorestech_client・moorestech_web・moorestech_master/tools で0件（docs・.decisions・過去plan は除く）。
- R2: EUV露光とダイシングのレシピは `levelInheritance: None` を前提にし、バリデータは入力がレベルファミリーであることを要求しない。受け入れ: グレード無しの test mod と v8 マスタでサーバーが起動し、`CleanRoomMasterTest` が通る。
- R3: ダイシングは、ダイシング装置に挿した品質モジュールの最高 Tier（0〜5）で `qualityTierDieTables[tier]` を選ぶ。表は「Lv1〜LvMax の割合＋失敗ダイの割合」で合計1。受け入れ: Tier t の表で Lv(t+2) 以上が出ない（品質ゲート）ことをユニットテストで固定。
- R4: レア枠シフト s = clamp(品質効果合計 × 部屋クラス倍率 − 最高Tierモジュール1個の効果値, −レア基準値, min(maxRareShift, 失敗枠合計))。s>0 は失敗枠（Lv1〜Lv(t-1) と失敗ダイ）を比例縮小してレア（Lv t+1）へ移し、s<0 はレアから失敗ダイへ移す。Tier0 と Tier=最大レベル（レア無し）は s=0。受け入れ: 「1個・クラスA で表どおり」「3個で失敗枠→レアへ移る」「クラスD 1個でレアが減る」「上限で頭打ち」「合計1を保つ」をユニットテストで固定。
- R5: 部屋クラスは露光失敗率・ダイ損失率（従来どおり）とレア枠シフト（R4 の倍率）に効く。露光の品質モジュールは失敗率の低減だけに効く。受け入れ: 既存の露光テストが通り、ダイシングのテストでクラス差が出る。
- R6: 損失したダイと失敗枠で引かれたダイはどちらも失敗ダイとして出る。ダイの総数は `diesPerWafer` を保つ。受け入れ: 保存則テスト。

### 表示（プレビュー・プロトコル・WebUI）
- R7: ダイシングのプレビューと保存抽選記録は、ダイ損失率・品質Tier・レベル別割合（Lv1〜Lv5）・失敗ダイ割合（どちらもシフト適用後、損失を除いた良品に対する割合）を持つ。プロトコル `va:getCleanRoomYieldInfo` の Dicing 応答と WebUI の歩留まり表示がこれを出す。成膜は「役割なし」の表示になる。受け入れ: パケットテスト・wire fixture テスト・webui テストが新形状で通り、unityプレイ録画テストでダイシング装置の歩留まり欄に品質Tierとレベル別割合が出る。

### セーブ
- R8: `WorldSaveAllInfo.CurrentVersion` を 9 にし、V8→V9 の移行ステップを1本足す。移行は (a) G2〜G5 ウェハ16品目の GUID を同じ工程の G1 GUID へ置き換え、(b) クリーンルーム機械の保存抽選記録のうち `kind:"waferGrade"` と、`qualityTier` を持たない `kind:"dicing"` を null にし、(c) v8 の EUV露光3レシピとダイシングのレシピを指すジョブ・保留の `inheritedLevel` を null にする。受け入れ: 旧形状のセーブ JSON が移行後にロードでき、ウェハが G1 として残る移行テスト。

### マスタ（moorestech_master, server_v8/mods/moorestechAlphaMod_8）
- R9: G2〜G5 のウェハ16品目と4つのウェハ levelFamily を削除し、G1 の4品目は名前から「G1」を外す（GUID は維持）。成膜・EUV・ダイシングのレシピは `None`、`cleanRoom.json` は `waferGradeDraws` を持たず、ダイシングに `qualityTierDieTables`（6段）と `maxRareShift` を持つ。
- R10: v8 のダイ表は次の値にする（Lv1..Lv5, 失敗ダイ）。Tier0: .20/0/0/0/0/.80、Tier1: .70/.10/0/0/0/.20、Tier2: .10/.70/.10/0/0/.10、Tier3: .04/.08/.70/.10/0/.08、Tier4: .02/.04/.06/.70/.10/.08、Tier5: .02/.03/.05/.10/.70/.10。`maxRareShift` は 0.2。
- R11: モジュールスタック上限 `moduleSlotStackLimitLevels` を [3,4,5,6,7,8] にし、スタック拡張 I〜V を level 1〜5 に対応させる。
- R12: 研究ツリーを ADR 0094 の形にする（詳細は Task 5 の表）。本線: 半導体製造 →（GPU設計・金属超精密加工）→ ロケット技術 → 軌道エレベーターの建設 → 反物質爆弾 → 最終研究。核融合炉は金属超精密加工の下で反物質爆弾の前提。品質Lv1〜5 が幹、省エネ・速度・生産性・GPU・スタック拡張の LvN は「品質LvN＋同種Lv(N-1)」が前提。「半導体のアップグレード研究Lv2〜5」と「機械オーバークロック」は名前としては消える（GUID は品質Lv2〜5・速度モジュールLv1 に流用）。
- R13: 品質Lv1 研究は ICチップLv1 200個。品質Lv2 は計算データを要求せず ICチップLv2 50・鋼鉄フレーム 200・コンピュータ 100、前提は品質Lv1 だけ（ロケット技術を前提にしない）。品質Lv3〜5 は計算データ 2,000/20,000/200,000 と ICチップLvN 少数（75/100/100）で、品質Lv3 はロケット技術も前提にする。本線の研究コストは ICチップLv1 までで Lv2 以上を要求しない。
- R18: v8 マスタから成膜装置（ブロック `01a17363-05b5-72d8-8a5d-a0c844ea6e14`）を削除する。そのレシピ「シリコンウェハ×1＋IPA 10 → トランジスタ設置済みウェハ×1（10秒）」（`01a17363-05b5-72d8-8a5d-a0c150aafaa1`）は GUID・中身を保ったまま `blockGuid` を EUV露光式半導体製造装置（`33cd6fe0-4fb2-4acc-a0a1-600c2201bfe0`）へ付け替える。成膜装置を参照する建設レシピ・研究解放・ビルドメニュー・チャレンジ・nodeGraph ノード・ローカライズ・tools の参照も消す。受け入れ: `grep -rn 01a17363-05b5-72d8-8a5d-a0c844ea6e14` が master の `server_v8`・`tools` で0件（`design/` の過去試算は除く）、EUV 装置でトランジスタ設置済みウェハが作れる。既存セーブに置かれた成膜装置は、ロード時の既存のマスタ欠落の刈り込み（`MissingMasterPruner`）で中身ごと消える（ユーザー裁定で許容）。
- R14: ダイ・ICチップ LvN の表示は品質Lv(N-1) の研究で解放（Lv1 は半導体製造）。各研究はその品の LvN 製作上限と表示を解放する。GPU衛星 Lv1 はロケット技術、Lv2〜5 は GPU LvN の研究で解放する。
- R15: nodeGraph（`.mooreseditor/nodeGraph.v1.json`）から削除品目のノードと線を消し、新しい研究ノードを前半と同じ配置文法（解放物は研究の右下・品質の幹は縦・同系統は縦積み）で置く。master-refine 操作A・B（sortPriority・研究 UIPosition の再計算）を回す。
- R16: `tools/research_sync/sync_machine_recipe_unlocks.py --check`、`tools/progression/check.py`、`tools/tier9_balance/run.py` が通る（tier9_balance のグレード前提の検査は新方式へ書き換える）。
- R17: 本 repo の `.moorestech-external-revisions.json` の moorestech_master ピンを、push 済みの master 側 PR のコミットへ更新する。

### やらないこと
- 品質以外のモジュール効果値・速度/省エネ/生産性の効き方は変えない。
- 本線研究（GPU設計・精密加工・核融合・ロケット・軌道エレベーター・反物質爆弾）の素材個数は前提の付け替え以外変えない。
- 「上位チップは下位の代わりに使える」「製作上限を超えるチップは製品に入れず取っておく」既存裁定の挙動は変えない。
- v7 以前の mod は触らない。

## Global Constraints

- 全コードファイル 200 行未満、1 ディレクトリ 10 ファイルまで（AGENTS.md）。
- コメントは日本語1行＋英語1行のセット。`#region Internal` はメソッド内ローカル関数に限る。partial・`Func<>`・デフォルト引数・try-catch（外部境界以外）禁止。
- Mooresmaster.Model.* は自動生成。スキーマ変更は VanillaSchema/*.yml を編集し edit-schema スキルに従う。後方互換の `optional`・既定値補完はしない。
- .meta を手で作らない。Prefab・シーンをテキスト編集しない。
- C# を変えたら `uloop compile --project-path ./moorestech_client` を通す。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。
- master 側の作業 worktree: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/chip-quality-research-redesign`（branch `feat/chip-quality-research-redesign`、origin/master 5c5ccfe から）。本 repo の worktree: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/chip-quality-research-redesign`（branch `feat/chip-quality-research-redesign`）。
- Editor は自分の worktree のものだけを使う（`moores-wt status` で確認）。本 worktree は `--no-editor` で作ってあるので、Unity 作業前に `uloop launch /Users/sakastudio/hermes-agent/data/worktrees/moorestech/chip-quality-research-redesign/moorestech_client` で起動する。Task 1 で `qualityTierDieTables` が必須キーになった時点から Task 5 でピンを更新するまで、v8 マスタを読む経路（Editor の通しプレイ・`PlaytestBootLifecycleTest` 等）は `MooresmasterLoaderException` でロードに失敗する。Task 1〜4 ではその経路を実行せず、test mod（`Tests.Module/TestMod/ForUnitTest`）を使うテストだけを回す。
- master の JSON は `json.dumps(ensure_ascii=False, indent=2)` で書き、末尾改行の有無は元ファイルに合わせる。新しい GUID は既存と同じ UUIDv7 形式で採番する（`python3 -c "import uuid;print(uuid.uuid7())"`。無ければ同等の実装）。
- セーブ形式の変更は AGENTS.md「唯一の例外: セーブ形式」と moorestech-save-migration スキルに従う。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反3件・修正3件 / 移行のセーブキー定数化・二段階化・テスト配置と連鎖テスト配列の追加
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱2・第3バケツ0 / シフト基準の ADR 文言ずれ（ADR を plan に合わせて修正）・失敗枠ループの重複（統合）

---

## 配置と前例

| # | 項目 | 配置先 | 機構・前例 |
|---|---|---|---|
| 1 | `qualityTierDieTables` / `maxRareShift` | `VanillaSchema/cleanRoom.yml` の `dicings[]` | 既存 `gradeDieTables` の置き換え（同じ場所・同じ配列形） |
| 2 | ダイ表の検証 | `Core.Master/Validator/CleanRoom/CleanRoomYieldTableRules.cs` の `ValidateDieTables` を書き換え | 既存の同メソッド |
| 3 | レア枠シフトの式 | `Game.Block/Blocks/CleanRoom/Machine/Yield/ChipYieldFormula.cs` に `ShiftedDieRates` | 既存 `ReducedRate`・`PromotionChance` と同じ純関数の置き場 |
| 4 | Tier・シフトの入力の組み立て | `OutputRewrite/CleanRoomRoleRates.cs` に `DicingRates` | 既存 `DieLossRate` と同じ「役割×クラス×モジュール→率」の口 |
| 5 | 最高Tierモジュール1個の効果値 | `Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs` に `HighestQualityModuleEffectValue` | 既存 `HighestQualityModuleTier` と同じ集計ループで求める |
| 6 | 抽選 | `Yield/ChipYieldDraw.cs` の `DrawDicing` を `(diesPerWafer, dieLossRate, IReadOnlyList<double> survivorRates)` に | 既存の同メソッド |
| 7 | プレビュー・記録 | `YieldPreview/CleanRoomYieldPreviewResult.cs` の `DicingPreview` | 既存の同クラス |
| 8 | 移行 | `Game.SaveLoad/Migration/Steps/SaveMigrationStepV8ToV9.cs` と `Steps/V8ToV9/` | 前例 `SaveMigrationStepV7ToV8.cs`・`Steps/V7ToV8/ZeroCostTier9ResearchResetTable.cs`（GUID 直書き表）・`MachineInheritedLevelFiller.cs` |

---

### Task 1: スキーマとマスタ検証をグレード無し・Tier別ダイ表へ移す

**Files:**
- Modify: `VanillaSchema/cleanRoom.yml`（`waferGradeDraws` 削除、`dicings[].gradeDieTables` を `qualityTierDieTables` に置換、`maxRareShift` 追加）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/CleanRoomMaster.cs`
- Delete: `moorestech_server/Assets/Scripts/Core.Master/CleanRoomRoles/WaferGradeRecipeRole.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/CleanRoomRoles/DicingRecipeRole.cs`
- Delete: `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomWaferGradeDrawValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomMasterUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomDicingValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomLayerExposureValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomRoleRecipeChecks.cs`（`ValidateGradeInheritingRole` を削除し、`RequireNoInheritance(roleName, recipe)` を足す）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/CleanRoom/CleanRoomYieldTableRules.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoom.json`・`items.json`・`machineRecipes.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestItemId.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomMasterTest.cs`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomYield/CleanRoomYieldTableRulesTest.cs`

**Interfaces:**
- Produces: 生成型 `DicingsElement.QualityTierDieTables`（要素 `QualityTierDieTablesElement { float[] LevelRates; float FailedRate; }`）と `DicingsElement.MaxRareShift`（float）。`CleanRoomYieldTableRules.ValidateDieTables(string roleLabel, IReadOnlyList<(IReadOnlyList<float> levelRates, float failedRate)> tierTables, int dieLevelCount)` → `string`（空文字＝合格）。`CleanRoomRoleRecipeChecks.RequireNoInheritance(string roleName, MachineRecipeMasterElement recipe)` → `string`。
- Consumes: なし。

- [ ] **Step 1: スキーマを書き換える**

`VanillaSchema/cleanRoom.yml` から `waferGradeDraws` のキー定義（L53-71）を削除し、`dicings[]` の `gradeDieTables` を次に置き換え、`maxLossReduction` の直後に `maxRareShift` を足す（コメントは既存の書式で日英1行ずつ）。

```yaml
        # 品質モジュール最高Tierでレア枠へ移せる割合の上限
        # Cap on the share the quality sum can move into the rare slot
        - key: maxRareShift
          type: number
        # 品質最高Tier 0〜最大レベルの順に並ぶダイ表。Tier t は Lv t が既定・Lv t+1 がレア・それ未満と失敗ダイが失敗枠
        # Die tables ordered by highest quality tier 0..max level; tier t has Lv t default, Lv t+1 rare, lower levels and the failed die as the failure slot
        - key: qualityTierDieTables
          type: array
          items:
            type: object
            properties:
            - key: levelRates
              type: array
              items:
                type: number
            - key: failedRate
              type: number
```

SourceGenerator の再生成は edit-schema スキルの手順に従う。

- [ ] **Step 2: 役割と検証からグレードを消す**

- `CleanRoomMaster.Initialize` の `_roleByRecipeGuid` 構築から `WaferGradeDraws` を外す。`WaferGradeRecipeRole.cs` と `CleanRoomWaferGradeDrawValidator.cs` を削除し、`CleanRoomMasterUtil.Validate` からその呼び出しを外す。
- `CleanRoomRoleRecipeChecks.ValidateGradeInheritingRole` を削除し、次を足す。

```csharp
// 清浄室の抽選工程はレベル継承を使わない（グレードを廃止したため）
// Clean-room draw processes never use level inheritance since wafer grades were removed
internal static string RequireNoInheritance(string roleName, MachineRecipeMasterElement recipe)
{
    if (recipe.LevelInheritance == MachineRecipeMasterElement.LevelInheritanceConst.None) return "";
    return $"[CleanRoomMaster] {roleName} MachineRecipeGuid:{recipe.MachineRecipeGuid} levelInheritance must be None\n";
}
```

（`LevelInheritanceConst` の実際の生成名は `MachineRecipesModule` の生成コードで確かめて合わせる。）
- `CleanRoomLayerExposureValidator`: `ValidateGradeInheritingRole` と「入力グレード数＝層グレード数」の検査を `RequireNoInheritance` に置き換える。層ウェハ・失敗ウェハが出力にあることの検査は残す。
- `CleanRoomDicingValidator`: L28-29 を `RequireNoInheritance` に置き換え、`QualityTierDieTables.Length == DieLevelCount + 1` を要求し、`MaxRareShift` が有限で [0,1] であることを要求し、`ValidateDieTables` に `(LevelRates, FailedRate)` の列を渡す。出力配置（Lv1..LvN, 失敗ダイ、各 Count=diesPerWafer）とスタック検査は残す。
- `DicingRecipeRole` は `Dicing`・`DieBaseItemId`・`DieLevelCount` のまま（`DieLevelCount` はダイ family 長から取る既存実装を維持）。

- [ ] **Step 3: ダイ表の規則を書き換える**

`CleanRoomYieldTableRules.ValidateDieTables` を次の規則にする（違反ごとに1行のメッセージを積んで返す）。
1. 表の数 = `dieLevelCount + 1`。
2. 各表の `levelRates.Count == dieLevelCount`。
3. 全要素と `failedRate` が有限かつ 0 以上。
4. `sum(levelRates) + failedRate` が 1（許容 1e-4）。
5. 品質ゲート: Tier t の表で、レベル番号が t+2 以上（0始まり添字で `index >= t + 1`）の割合が 0。
6. Tier t (1≤t) の既定 Lv t（添字 t-1）は 0 より大きい。

旧規則「上位の裾が縮まない」は削除する（グレード間の比較だったため）。`ValidateClassRows` は変えない。

- [ ] **Step 4: test mod をグレード無しにする**

- `items.json`: `TestGradedWaferFamily`・`TestLayeredWaferFamily` の levelFamily を削除し、G2・G3 の品目を削除、G1 品目は残す（名前から G1 を外してよい）。
- `machineRecipes.json`: test の成膜（`ddcacf5c…`）・露光（`06549afe…`）・ダイシング（`aadd680b…`）を `"levelInheritance": "None"` にし、入力は G1 品目の GUID。
- `cleanRoom.json`: `waferGradeDraws` を削除。ダイシングに `"maxRareShift": 0.2` と、ダイ family 長 3 に合わせた4段の表を入れる。

```json
"qualityTierDieTables": [
  { "levelRates": [0.2, 0.0, 0.0], "failedRate": 0.8 },
  { "levelRates": [0.7, 0.1, 0.0], "failedRate": 0.2 },
  { "levelRates": [0.1, 0.7, 0.1], "failedRate": 0.1 },
  { "levelRates": [0.05, 0.1, 0.75], "failedRate": 0.1 }
]
```

- `ForUnitTestItemId.cs`: 消した品目の定数を削除し、参照箇所をコンパイルエラーで洗い出して G1 定数へ寄せる（テストの書き換えは各タスクで行う。ここではコンパイルを通すための置換のみ）。

- [ ] **Step 5: テストを書き換える**

- `CleanRoomMasterTest`: `TryGetRecipeRoleResolvesWaferGradeTest`・`WaferGradeRoleRejectsInheritanceRecipeTest` を削除。代わりに `DepositionRecipeHasNoRoleTest`（成膜レシピで `TryGetRecipeRole` が false）と `DicingRoleRejectsInheritanceRecipeTest`（ダイシングを `Inherit` にした master で検証ログに `levelInheritance must be None` が出る。既存 `WaferGradeRoleRejectsInheritanceRecipeTest` の master 差し替え手法を流用）を足す。`RoleChanceRequiresFiniteUnitIntervalTest` は `MaxRareShift` に対象を変える。
- `CleanRoomYieldTableRulesTest`: `TableAboveItsGradeIsRejectedTest` を `TierTableAboveQualityGateIsRejectedTest`（Tier1 の表で Lv3>0 を拒否）へ、`ShrinkingUpperTailIsRejectedTest` を削除、`TableNotSummingToOneIsRejectedTest` は failedRate 込みの合計で判定、`ContractDieTablesAreValidTest` は R10 の v8 表（6段×5レベル）が合格することを確かめる。`MissingDefaultLevelIsRejectedTest`（Tier2 で Lv2=0 を拒否）と `WrongTableCountIsRejectedTest` を足す。

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomMasterTest|CleanRoomYieldTableRulesTest"`
Expected: ErrorCount 0 / 全 PASS（抽選側の旧テストのコンパイルエラーは Task 2 で直すため、この Step では該当テストファイルに `WaferGrade` 参照が残るならこのタスク内で削除して構わない。削除したテスト名は Task 2 の置き換え対象として報告に書く）

- [ ] **Step 7: コミットする**

```bash
git add VanillaSchema/cleanRoom.yml moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests.Module moorestech_server/Assets/Scripts/Tests
git commit -m "クリーンルームのマスタからウェハグレードを外し、品質Tier別のダイ表を検証する"
```

### Task 2: ダイシングの抽選を品質Tier＋レア枠シフトへ置き換え、成膜の抽選を消す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/Yield/ChipYieldFormula.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/Yield/ChipYieldDraw.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/OutputRewrite/CleanRoomRoleRates.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/OutputRewrite/DicingOutputRewriter.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/OutputRewrite/WaferGradeOutputRewriter.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/YieldPreview/CleanRoomYieldPreview.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/YieldPreview/CleanRoomYieldPreviewResult.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/CleanRoomYieldDrawDecorator.cs`（`inheritedLevel` をダイシングへ渡さない）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomYield/ChipYieldFormulaTest.cs`、`.../ChipYieldDrawTest.cs`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Machine/CleanRoomDicingTest.cs`、`.../CleanRoomLayerExposureTest.cs`、`.../CleanRoomMachineFluidTest.cs`、`.../Save/CleanRoomJobSaveRoundTripTest.cs`、Delete: `.../CleanRoomWaferGradeDrawTest.cs`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Upgrade/MachineModuleQualityTierTest.cs`

**Interfaces:**
- Consumes: Task 1 の `DicingsElement.QualityTierDieTables`・`MaxRareShift`。
- Produces:
  - `MachineModuleEffect.HighestQualityModuleEffectValue`（`readonly float`。最高 Tier の品質モジュール1個の `EffectValue`。品質モジュールが無ければ 0）。
  - `ChipYieldFormula.ShiftedDieRates(IReadOnlyList<float> levelRates, float failedRate, int tier, double qualityEffectSum, double classQualityMultiplier, double referenceEffectValue, double maxRareShift)` → `double[]`（長さ = levelRates.Count + 1、末尾が失敗ダイ。合計1）。
  - `ChipYieldDraw.DrawDicing(int diesPerWafer, double dieLossRate, IReadOnlyList<double> survivorRates, CleanRoomDrawRandom random)` → `DicingDrawResult`（`survivorRates` の末尾は失敗ダイ。損失ダイと失敗枠ダイを `FailedDieCount` に合算）。
  - `CleanRoomRoleRates.DicingRates(DicingRecipeRole role, CleanRoomClassYield classYield, MachineModuleEffect moduleEffect, out int qualityTier)` → `double[]`。
  - `DicingPreview(double dieLossRate, int qualityTier, IReadOnlyList<double> levelRates, double failedRate)`。保存キー `kind:"dicing"`, `dieLossRate`, `qualityTier`, `levelRates`（配列）, `failedRate`。

- [ ] **Step 1: 式のテストを書く（ChipYieldFormulaTest）**

旧 `MaxWaferGradeIsHighestTierPlusOneTest`・`PromotionChanceIsClampedProductTest`・`QualityGateNeverYieldsAboveTierPlusOneTest` を削除し、次を足す。

```csharp
private static readonly float[] Tier2Levels = { 0.1f, 0.7f, 0.1f, 0f, 0f };

[Test]
public void OneModuleInClassAKeepsTableTest()
{
    // 1個・クラスAではシフト0で表のまま
    // One module in class A means zero shift, so the table stays
    var rates = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 0.06, 1.0, 0.06, 0.2);
    Assert.AreEqual(new[] { 0.1, 0.7, 0.1, 0.0, 0.0, 0.1 }, rates.Select(r => Math.Round(r, 6)).ToArray());
}

[Test]
public void MoreModulesMoveFailureToRareTest()
{
    // 3個でシフト0.12。失敗枠0.2が0.08へ比例縮小し、レアが0.22になる
    // Three modules shift 0.12: the 0.2 failure slot shrinks proportionally to 0.08 and the rare becomes 0.22
    var rates = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 0.18, 1.0, 0.06, 0.2);
    Assert.AreEqual(0.04, rates[0], 1e-9);
    Assert.AreEqual(0.7, rates[1], 1e-9);
    Assert.AreEqual(0.22, rates[2], 1e-9);
    Assert.AreEqual(0.04, rates[5], 1e-9);
}

[Test]
public void LowClassMovesRareToFailedDieTest()
{
    // クラスD(0.25)1個はシフト−0.045。レアが減り失敗ダイが増える
    // One module in class D (0.25) shifts −0.045, lowering the rare and raising the failed die
    var rates = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 0.06, 0.25, 0.06, 0.2);
    Assert.AreEqual(0.055, rates[2], 1e-9);
    Assert.AreEqual(0.145, rates[5], 1e-9);
}

[Test]
public void ShiftIsCappedAndSumStaysOneTest()
{
    // 上限0.2で頭打ち、どの入力でも合計1
    // The shift saturates at 0.2 and every input keeps the sum at one
    var rates = ChipYieldFormula.ShiftedDieRates(Tier2Levels, 0.1f, 2, 5.0, 1.0, 0.06, 0.2);
    Assert.AreEqual(0.2, rates[2] - 0.1, 1e-9);
    Assert.AreEqual(1.0, rates.Sum(), 1e-9);
}

[Test]
public void TierZeroAndTopTierNeverShiftTest()
{
    // Tier0（モジュール無し）と最大Tier（レア無し）は表のまま
    // Tier 0 (no module) and the top tier (no rare) keep the table
    var none = ChipYieldFormula.ShiftedDieRates(new[] { 0.2f, 0f, 0f, 0f, 0f }, 0.8f, 0, 0, 1.0, 0, 0.2);
    Assert.AreEqual(0.2, none[0], 1e-9);
    var top = ChipYieldFormula.ShiftedDieRates(new[] { 0.02f, 0.03f, 0.05f, 0.1f, 0.7f }, 0.1f, 5, 0.6, 1.0, 0.12, 0.2);
    Assert.AreEqual(0.7, top[4], 1e-9);
    Assert.AreEqual(0.1, top[5], 1e-9);
}
```

`WorkedExampleExpectedDiesTest` と `ClassImprovementNeverReducesGoodTotalOrUpperLevelsTest` は新しい式で書き直す（後者は「クラスを上げると、良品の合計とレア以上の割合が減らない」を Tier1〜4・1〜5個で総当たり）。

- [ ] **Step 2: 式を実装する（ChipYieldFormula）**

`MaxWaferGrade`・`PromotionChance` を削除し、次を足す（`ReducedRate` は残す）。

```csharp
// Tier tの表に品質シフトを掛けた、良品ダイ1個あたりの [Lv1..LvMax, 失敗ダイ] の割合
// Per-survivor rates [Lv1..LvMax, failed die] after applying the quality shift to tier t's table
public static double[] ShiftedDieRates(IReadOnlyList<float> levelRates, float failedRate, int tier, double qualityEffectSum, double classQualityMultiplier, double referenceEffectValue, double maxRareShift)
{
    var levelCount = levelRates.Count;
    var rates = new double[levelCount + 1];
    for (var i = 0; i < levelCount; i++) rates[i] = levelRates[i];
    rates[levelCount] = failedRate;

    // レア枠が無いTier（0と最大）はシフトしない
    // Tiers without a shiftable rare slot (0 and the top) do not shift
    if (tier <= 0 || levelCount <= tier) return rates;
    var rareIndex = tier;

    // 失敗枠は Lv1〜Lv(t-1) と失敗ダイ
    // The failure slot is Lv1..Lv(t-1) plus the failed die
    var failureIndices = new int[tier];
    for (var i = 0; i < tier - 1; i++) failureIndices[i] = i;
    failureIndices[tier - 1] = levelCount;
    var failureTotal = 0.0;
    foreach (var index in failureIndices) failureTotal += rates[index];
    var shift = Math.Clamp(qualityEffectSum * classQualityMultiplier - referenceEffectValue, -rates[rareIndex], Math.Min(maxRareShift, failureTotal));

    // 正は失敗枠を比例縮小してレアへ、負はレアから失敗ダイへ
    // Positive shrinks the failure slot proportionally into the rare; negative moves rare into the failed die
    if (0 < shift)
    {
        var keep = (failureTotal - shift) / failureTotal;
        foreach (var index in failureIndices) rates[index] *= keep;
    }
    else rates[levelCount] -= shift;
    rates[rareIndex] += shift;
    return rates;
}
```

- [ ] **Step 3: 最高Tierの効果値を集計する（MachineModuleEffect）**

`Aggregate` の品質分岐で、`module.Tier` が現在の最高より大きいとき（同値なら最大の EffectValue）に `highestQualityEffectValue = module.EffectValue` を更新し、コンストラクタ引数と `public readonly float HighestQualityModuleEffectValue` を足す。既存の呼び出し・`new MachineModuleEffect(...)` を全て更新する（デフォルト引数は使わない）。`MachineModuleQualityTierTest` に `AggregateReportsHighestTierEffectValueTest`（Tier1×3 と Tier2×1 で 0.4 = test mod `TestQualityFractionalModule` の値）を足す。

- [ ] **Step 4: 抽選を書き換える（ChipYieldDraw）**

`DrawWaferGrade` を削除し、`DrawDicing` を次にする。

```csharp
// ダイごとに損失を判定し、残りを [Lv1..LvMax, 失敗ダイ] の割合で振り分ける。損失と失敗枠はどちらも失敗ダイ
// Roll loss per die, then assign survivors by [Lv1..LvMax, failed die]; losses and failure-slot picks both become failed dies
public static DicingDrawResult DrawDicing(int diesPerWafer, double dieLossRate, IReadOnlyList<double> survivorRates, CleanRoomDrawRandom random)
{
    var levelCount = survivorRates.Count - 1;
    var goodCounts = new int[levelCount];
    var failedCount = 0;
    for (var die = 0; die < diesPerWafer; die++)
    {
        if (random.NextDouble() < dieLossRate) { failedCount++; continue; }
        var picked = PickIndex(random.NextDouble());
        if (picked == levelCount) failedCount++;
        else goodCounts[picked]++;
    }
    return new DicingDrawResult(goodCounts, failedCount);

    #region Internal

    int PickIndex(double roll)
    {
        var cumulative = 0d;
        for (var i = 0; i < survivorRates.Count - 1; i++)
        {
            cumulative += survivorRates[i];
            if (roll < cumulative) return i;
        }
        return survivorRates.Count - 1;
    }

    #endregion
}
```

`ChipYieldDrawTest`: `WaferGradeStaysWithinCapTest`・`WaferGradeFrequencyMatchesClosedFormTest`・`GradeOneTableYieldsOnlyLevelOneTest` を削除。`DicingConservesDieCountTest`・`DicingMeanMatchesExpectationTest` を新シグネチャで書き直し（期待値 = 300×(1−loss)×rate、失敗ダイ = 300×loss + 300×(1−loss)×failedRate）、`QualityGateNeverDrawsAboveRareTest`（Tier1 の割合で Lv3 以上が常に0）を足す。

- [ ] **Step 5: 率の口と書き換え器を差し替える**

`CleanRoomRoleRates`: `MaxWaferGrade`・`PromotionChance` を削除し、次を足す。

```csharp
// ダイシングの良品1個あたりの割合。Tierはダイシング装置の品質最高Tier
// Per-survivor dicing rates; the tier is the dicing machine's highest quality tier
internal static double[] DicingRates(DicingRecipeRole role, CleanRoomClassYield classYield, MachineModuleEffect moduleEffect, out int qualityTier)
{
    qualityTier = Math.Min(moduleEffect.HighestQualityModuleTier, role.DieLevelCount);
    var table = role.Dicing.QualityTierDieTables[qualityTier];
    return ChipYieldFormula.ShiftedDieRates(table.LevelRates, table.FailedRate, qualityTier, moduleEffect.QualityShift, classYield.QualityMultiplier, moduleEffect.HighestQualityModuleEffectValue, role.Dicing.MaxRareShift);
}
```

`DicingOutputRewriter.Rewrite(DicingRecipeRole role, CleanRoomYieldRollContext context)`（`gradeLevel` 引数を削除）: `DieLossRate` と `DicingRates` を求めて `DrawDicing` に渡し、出力の組み立ては既存ループのまま。`WaferGradeOutputRewriter.cs` を削除。

- [ ] **Step 6: プレビューと記録を差し替える**

- `CleanRoomYieldPreview.Evaluate`: `WaferGradeRecipeRole` の case を削除。`DicingRecipeRole` の case は `inheritedLevel` を見ずに `DicingOutputRewriter.Rewrite(dicing, roll.Value)` を呼び、`new DicingPreview(lossRate, tier, rates.Take(levelCount).ToArray(), rates[levelCount])` を返す。`inheritedLevel` 引数は他の case が使わなくなれば `Evaluate` と `CleanRoomYieldDrawDecorator.Decorate` の呼び出し経路から外す（`Decorate` のシグネチャは機械共通の decorator interface に合わせる必要があるなら残し、ダイシングへ渡さないだけにする）。
- `CleanRoomYieldPreviewResult`: `WaferGradePreview` を削除。`DicingPreview` を `(double DieLossRate, int QualityTier, IReadOnlyList<double> LevelRates, double FailedRate)` にし、`CaptureDrawRecord` は `new JObject { ["kind"]="dicing", ["dieLossRate"]=…, ["qualityTier"]=…, ["levelRates"]=new JArray(LevelRates), ["failedRate"]=… }`。`RestoreDrawRecord` の `"waferGrade"` 分岐を削除し、`"dicing"` は4キーすべてを `Required` で読む（`levelRates` は `drawRecord["levelRates"].ToObject<double[]>()`、欠けていれば同じ例外）。

- [ ] **Step 7: 統合テストを書き換える**

- `CleanRoomWaferGradeDrawTest.cs` を削除。そこにあった `SaveLoadPreservesCycleCounterTest` は `CleanRoomDicingTest` へ移す。
- `CleanRoomDicingTest`: `GradeThreeWaferSplitsAcrossThreeLevelsTest`・`GradeOneWaferYieldsOnlyLevelOneTest` を、`NoQualityModuleYieldsOnlyLevelOneOrFailedTest`（モジュール無しで Lv2 以上が0）・`TierOneModuleYieldsLevelOneMostlyAndLevelTwoRarelyTest`（`TestQualityModule` 1個で Lv1 > Lv2 > 0、Lv3 = 0）・`TierTwoModuleFailureSlotContainsLevelOneTest`（`TestQualityFractionalModule` で Lv1 > 0 かつ Lv2 が最多）へ置き換える。`FullOutputSlotHoldsJobWithoutLossTest`・`ProductivityExtraSetSharesTheDrawAndFitsTest` は G1 入力で動くよう直す。
- `CleanRoomLayerExposureTest.ExposureKeepsInputGradeOrFailsTest` を `ExposureYieldsNextLayerOrFailsTest` にする（グレードの検査を外す）。`CleanRoomMachineFluidTest`・`CleanRoomMachineOutputSpaceShortReasonTest`・`CleanRoomMachineOutputBlockedPowerNoticeTest` の `TestGradedWaferG1` 参照は Task 1 で残した G1 定数名に合わせる。
- `CleanRoomJobSaveRoundTripTest`: `DrawRecordPreservesWireShapeAndRatesThroughJson` を新しい dicing 記録の4キーで、`DrawRecordRejectsUnknownKindsMissingRatesAndNonDrawStates` に「`kind:"waferGrade"` を拒否」「`qualityTier` 欠けの dicing を拒否」を足す。`SaveReloadPreservesJobTimeAndConsumedGradeOrWarnsForLegacy` はダイシングが継承を持たなくなった前提へ直す（継承の検査はパッケージング装置など Inherit レシピ側で行う）。

- [ ] **Step 8: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom|ChipYield|MachineModuleQualityTier|ModuleSlotStackLimit"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 9: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Tests
git commit -m "ダイシングの抽選を品質最高Tierの表とレア枠シフトへ置き換え、成膜のグレード抽選を消す"
```

### Task 3: 歩留まりのプロトコル・クライアント DTO・WebUI をダイシングのレベル別割合へ移す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CleanRoom/CleanRoomYieldMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CleanRoom/GetCleanRoomYieldInfoProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CleanRoom/CleanRoomYieldResponseReader.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CleanRoom/CleanRoomYieldView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/CleanRoom/CleanRoomYieldDetailDtoBuilder.cs`・`CleanRoomYieldDetailDtos.cs`・`CleanRoomYieldPendingDrawDto.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/cleanRoom/index.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/cleanRoom/cleanRoomYieldLogic.ts`・`CleanRoomYieldSection.tsx`
- Modify: `Localization/localization.csv`（キー追加・削除）と生成物 `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（既存の生成手順で再生成）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/CleanRoom/GetCleanRoomYieldInfoProtocolTest.cs`・`CleanRoomPendingDrawYieldInfoTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/WireContract/WireContractCleanRoomYieldTest.cs` と `WireFixtures/block_inventory_clean_room_yield_*.json`、`moorestech_client/Assets/Scripts/Client.Tests/BlockInfoPolling/BlockInfoPollerTest.cs`、`moorestech_web/webui` の `cleanRoomYieldLogic.test.ts`・`CleanRoomYieldSection.test.ts`・`bridge/contract/schemas/__tests__/inventory.test.ts`

**Interfaces:**
- Consumes: Task 2 の `DicingPreview(DieLossRate, QualityTier, LevelRates, FailedRate)`。
- Produces: `DicingYieldMessagePack { [Key(0)] double DieLossRate; [Key(1)] int QualityTier; [Key(2)] double[] LevelRates; [Key(3)] double FailedRate; }`。`CleanRoomYieldResponseKind` から `WaferGrade` を削除。応答の Key3（WaferGrade）と対応する Pending 欄を削除し、残りのキー番号は詰めて振り直す（後方互換は考えない。サーバーとクライアントは同じビルドで出る）。WebUI の dicing 役割 DTO に `qualityTier`（int）・`levelRates`（number[]）・`failedRate`（number）を足す。

- [ ] **Step 1: サーバーのプロトコルを書き換える**

`WaferGradeYieldMessagePack`・`CreateWaferGrade`・`CleanRoomYieldWaferGradeView`・reader の wafer 分岐を削除。`DicingYieldMessagePack` を上の形にし、`CreateDicing` と preview→MessagePack の写しに新フィールドを通す。成膜を選んだ機械は役割表に無いので `NoRole` で返る（既存経路）。

- [ ] **Step 2: パケットテストを書き換える**

`GetCleanRoomYieldInfoProtocolTest.DepositionShowsMaxGradeAndPromotionChanceTest` を `DepositionReportsNoRoleTest` に置き換え、`DicingShowsDieLossRateTest` を `DicingShowsQualityTierAndLevelRatesTest`（test mod の `TestQualityModule` 1個・クラスA で `QualityTier==1`、`LevelRates` が表 `[0.7,0.1,0]`、`FailedRate==0.2`、`DieLossRate` はクラス A の値）へ。`CleanRoomPendingDrawYieldInfoTest` は保留抽選の dicing が新フィールドを持つことを確かめる。

- [ ] **Step 3: クライアント DTO と wire fixture を書き換える**

`CleanRoomYieldDetailDtos`・`CleanRoomYieldPendingDrawDto` から `MaxGrade`・`PromotionChance`・`CreateWaferGrade` を削除し、dicing に `QualityTier`・`LevelRates`・`FailedRate` を足す（`{ get; private set; }` 形）。`CleanRoomYieldDetailDtoBuilder` の switch から wafer 分岐を削除。fixture `block_inventory_clean_room_yield_deposition.json`・`_deposition_pending.json` を削除し、`WireContractCleanRoomYieldTest` の対応テストを削除、`_dicing.json` を新形状で作り直す（既存の fixture 更新手順に従う）。`BlockInfoPollerTest.InvalidYieldWireResponseDropsAndRecoversTest` の `CreateWaferGrade(3,0.5,null)` を `CreateDicing` の不正値に差し替える。

- [ ] **Step 4: WebUI を書き換える**

- zod スキーマ: `waferGradeRates` を削除し、`dicingRates` を `{ dieLossRate: z.number(), qualityTier: z.number().int().min(0), levelRates: z.array(z.number()), failedRate: z.number() }` にする。
- `cleanRoomYieldLogic.ts`: 役割 union から `waferGrade` を削除。dicing の行を「品質Tier（`clean-room-yield-quality-tier`）」「ダイ損失率（`clean-room-yield-die-loss-rate`、既存）」「Lv1〜Lv5 の割合（`clean-room-yield-level-rate-<n>`、0 の行は出さない）」「失敗ダイの割合（`clean-room-yield-failed-rate`）」にする。
- `CleanRoomYieldSection.tsx`: 上の行を既存の行コンポーネントで描く（ref-webui-design スキルのホワイトリストに従う）。
- `localization.csv`: `ui.blockInventory.cleanRoomYieldMaxGrade`・`cleanRoomYieldPromotionChance` を削除し、`cleanRoomYieldQualityTier`（ja「品質モジュールTier」/en「Quality module tier」）・`cleanRoomYieldLevelRate`（ja「Lv{0}の割合」/en「Lv{0} share」）・`cleanRoomYieldFailedRate`（ja「失敗ダイの割合」/en「Failed die share」）を足し、生成物を再生成する。
- webui テスト: 「成膜は上限グレードと昇格率の2行を出す」を削除し、「ダイシングは品質Tier・損失率・0でないレベル割合・失敗割合を出す」を足す。`inventory.test.ts` の waferGrade ケースを dicing の新形状へ。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomYield|CleanRoomPendingDraw|WireContractCleanRoomYield|BlockInfoPoller"` → `cd moorestech_web/webui && npm test -- cleanRoom inventory`（webui の既存テストコマンドに合わせる）
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts moorestech_web/webui Localization
git commit -m "歩留まり表示から成膜のグレードを外し、ダイシングに品質Tierとレベル別割合を出す"
```

### Task 4: セーブ V8→V9 移行（グレード付きウェハの G1 化・旧抽選記録と旧継承レベルの破棄）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`（`CurrentVersion = 9`）
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV8ToV9.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V8ToV9/IV8ToV9MigrationPart.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V8ToV9/WaferGradeRemovalTable.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V8ToV9/WaferGradeItemGuidRewriter.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V8ToV9/CleanRoomLegacyDrawStateCleaner.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs`（`new SaveMigrationChain(...)` に V8ToV9 を追加）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs`（L37 付近のテスト用移行チェーン配列に V8ToV9 を追加）
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs`（L137 付近の配列に V8ToV9 を追加）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV8ToV9Test.cs`（前例 `SaveMigrationStepV7ToV8Test.cs` と同じ置き場）

**Interfaces:**
- Consumes: なし（JSON だけを扱う）。
- Produces: `SaveMigrationStepV8ToV9 : ISaveMigrationStep { int FromVersion => 8; SaveMigrationStepResult Migrate(JObject save); }`。`IV8ToV9MigrationPart { bool TryPrepare(JObject save, out string failureReason); string ApplyAndSummarize(); }`（前例 `Steps/V7ToV8/IV7ToV8MigrationPart.cs` と同じ2段構成。実際のメンバー名は前例に合わせる）。

- [ ] **Step 1: 移行表を書く（WaferGradeRemovalTable）**

前例 `Steps/V7ToV8/ZeroCostTier9ResearchResetTable.cs` と同じ形で、v8 マスタの GUID を直書きする。値は v8 `items.json` の `levelFamilies`（トランジスタ設置済みウェハ・1層・2層・完成パターンウェハ）の `levelItemGuids` から取る。

- `RemovedToBaseWaferGuids`: G2〜G5 の16 GUID → 同じ family の G1（`baseItemGuid`）。例: `01a17356-2aff-701f-9a1c-173dd8d81157`（トランジスタ設置済みウェハG2）→ `019e3c14-b270-716f-84e5-c0b06a97592b`。
- `DroppedInheritanceRecipeGuids`: v8 の EUV露光3レシピ `01a17363-05b5-72d8-8a5d-a0c245c7ebb6`・`01a17363-05b5-72d8-8a5d-a0c31f51aa60`・`01a17363-05b5-72d8-8a5d-a0c41f603b85` とダイシング `01a17363-05b5-72d8-8a5d-a0c5f18cd451`。

GUID はクラス冒頭のコメントに「v8 マスタの値。マスタ側で GUID を変えないこと」と日英で書く。

- [ ] **Step 2: 書き換え部品を実装する**

- `WaferGradeItemGuidRewriter`: セーブ JSON 全体を再帰で辿り、`JTokenType.String` の値が `RemovedToBaseWaferGuids` のキーに一致したら G1 GUID に置き換える（stack 形以外の `consumedInputItemGuids` 等の文字列配列も同じく置き換わる）。置き換えた個数を返す。
- 両部品とも `IV8ToV9MigrationPart` を実装し、`TryPrepare` で書き換え対象を集めて形を検査するだけにし、書き換えは `ApplyAndSummarize` で行う（全部品の `TryPrepare` が通ってから Apply する。原本を半分だけ書き換えた状態を作らない）。
- `CleanRoomLegacyDrawStateCleaner`: ブロックの componentStates のうちクリーンルーム機械プロセッサの保存キーの値 JSON に対し（保存キーは現行の型を参照せず、前例 `Steps/V7ToV8/V7ToV8SaveJson.cs` の `CleanRoomMachineSaveKey` と同じく V8ToV9 側の文字列定数で持つ。Game.SaveLoad は Game.Block を参照しない）、
  - `pendingRealization.drawRecord` が `kind:"waferGrade"`、または `kind:"dicing"` で `qualityTier` が無いとき `null` にする。
  - `recipeGuid` が `DroppedInheritanceRecipeGuids` にあるときトップレベルの `inheritedLevel` を `null`、`pendingRealization.recipeGuid` が同表にあるとき `pendingRealization.inheritedLevel` を `null` にする。
  - 値が文字列化された JSON で保存されているなら、前例 `Steps/V7ToV8/MachineInheritedLevelFiller.cs` の読み書き方法に合わせる。
- 形が読めない（想定キーの型違い）ときは例外ではなく `SaveMigrationStepResult.Failed(理由)` を返す。`pendingRealization`・`drawRecord`・`inheritedLevel` が無い・null なのは正常形（待機中の機械）なので、前例 `V7ToV8SaveJson.IsAbsentOrNull` と同じ判定で読み飛ばす。

- [ ] **Step 3: ステップを組んで登録する**

`SaveMigrationStepV8ToV9.Migrate` は前例 `SaveMigrationStepV7ToV8.cs` と同じく、全部品の `TryPrepare` を先に呼んでどれかが失敗したら `SaveMigrationStepResult.Failed` を返し、全部通ったら Rewriter → Cleaner の順に `ApplyAndSummarize` を呼び、`SaveMigrationStepResult` の成功を返す（前例 `SaveMigrationStepV7ToV8.cs` の構成と戻り値に合わせる）。`CurrentVersion` を 9 にし、`SaveAndEventServiceRegistration` の配列へ追加する。

- [ ] **Step 4: 移行テストを書く**

前例の V7ToV8 テストと同じ置き場・同じ雛形で、次の旧形状セーブ JSON を手で組んで検証する。
1. 機械の出力スロットに `トランジスタ設置済みウェハG3` のスタック（count 5）→ 移行後は G1 GUID・count 5。
2. クリーンルーム機械の `pendingRealization.drawRecord = {"kind":"waferGrade","maxGrade":2,"promotionChance":0.3}` → `null`。`{"kind":"dicing","dieLossRate":0.15}` → `null`。`{"kind":"layerExposure","layerFailRate":0.1}` はそのまま。
3. `recipeGuid` = ダイシング・`inheritedLevel` = 2 → `null`。`recipeGuid` = パッケージング装置のレシピ・`inheritedLevel` = 2 → 2 のまま。
4. 2回適用しても結果が変わらない（冪等）。
5. 移行後のセーブを test mod でロードして例外が出ない（既存 V7ToV8 テストにロード確認があればそれに倣う。無ければ 1〜4 で足りる）。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigration|SaveLoad"`
Expected: ErrorCount 0 / 全 PASS（`SaveMigrationChain` の欠番検査が起動時に通る）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Tests
git commit -m "セーブV9: グレード付きウェハをG1へ寄せ、旧抽選記録と旧継承レベルを捨てる"
```

### Task 5: v8 マスタを新方式へ（品目・レシピ・クリーンルーム・研究ツリー・nodeGraph）とピン更新

作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/chip-quality-research-redesign`、対象 `server_v8/mods/moorestechAlphaMod_8/`。

**Files:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/items.json`、`machineRecipes.json`、`cleanRoom.json`、`research.json`、`blocks.json`（成膜装置 `01a17363-05b5-72d8-8a5d-a0c844ea6e14` を削除・削除品目への参照があれば除去）、`craftRecipes.json`・`buildMenu.json`・`challenges.json`（成膜装置・削除品目への参照があれば除去）、`server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`（成膜装置の行）
- Modify: `tools/tier9_balance/sections/s1/item_checks.py`・`tools/tier9_balance/sections/s2/compute_timeline.py`・`tools/tier9_balance/tests/test_s1_hatch_capacity.py`（成膜装置を前提にした台数・ハッチ容量の検査を EUV に寄せる）
- Modify: `server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json`
- Modify: `tools/tier9_balance/sections/s1/cleanroom_checks.py`、`tools/tier9_balance/tests/test_s1_tables.py`（グレード前提の検査を新方式へ）
- Modify: `tools/progression/model/machine_sources.py`（L39 の `waferGradeDraws` 読み・L142/L144 の `gradeDieTables` 読み＝`_wafer_grades`・`_emit_dies`）、`tools/progression/tests/fixture_tier9.py`（L47/L50）、`tools/progression/tests/test_model_contracts.py`（L86）
- Modify（本 repo）: `.moorestech-external-revisions.json`
- Test: `tools/research_sync/sync_machine_recipe_unlocks.py --check`、`tools/progression/check.py`、`tools/tier9_balance/run.py`、本 repo の `uloop run-tests ... "MasterValidation|CleanRoomMasterTest"`（v8 マスタを読むテストの名前は既存に合わせる）

**Interfaces:**
- Consumes: Task 1 のスキーマ（`qualityTierDieTables`・`maxRareShift`、`waferGradeDraws` 無し）。
- Produces: master 側の push 済みコミット（ピンの値）。

- [ ] **Step 1: 品目・レシピ・クリーンルームを書き換える**

スクリプトを scratchpad に書いて一括で適用する（手編集しない）。
- `items.json`: ウェハ4 family（`トランジスタ設置済みウェハ`・`1層パターンウェハ`・`2層パターンウェハ`・`完成パターンウェハ`）の `levelFamilies` 要素を削除し、その G2〜G5 の16品目を `data` から削除。G1 の4品目は名前の末尾「G1」を外す。`moduleSlotStackLimitLevels` を `[{"stackLimit":3},{"stackLimit":4},{"stackLimit":5},{"stackLimit":6},{"stackLimit":7},{"stackLimit":8}]` にする。
- `machineRecipes.json`: 成膜・EUV3本・ダイシングを `"levelInheritance": "None"` にする（GUID は Task 4 の表と同じ）。
- `blocks.json`・`machineRecipes.json` ほか: R18 のとおり成膜装置を削除し、トランジスタ設置済みウェハのレシピ（`01a17363-05b5-72d8-8a5d-a0c150aafaa1`）の `blockGuid` を EUV 装置（`33cd6fe0-4fb2-4acc-a0a1-600c2201bfe0`）へ付け替える。成膜装置のブロックGUIDとそのアイテムを参照する全 JSON の箇所（研究の `unlockItemRecipeView`・`unlockMachineRecipe` 等、建設レシピ、ビルドメニュー、チャレンジ）を消し、`grep` で0件を確かめる。nodeGraph の成膜装置ノードも Step 4 で消す。
- `cleanRoom.json`: `waferGradeDraws` を削除し、ダイシングの `gradeDieTables` を R10 の6段（`levelRates` 5要素＋`failedRate`）に置き換え、`"maxRareShift": 0.2` を足す。
- 全 JSON で削除した16 GUID を参照する箇所が残っていないことを `grep` で確かめる（研究の `unlockItemRecipeView` は Step 2 で消える）。

- [ ] **Step 2: 研究ツリーを組み直す**

スクリプトで `research.json` を書き換える。名前は表示名、GUID は既存を流用するものだけ明記し、新規は UUIDv7 で採番する。説明文（`researchNodeDescription`）は解放内容ベースの1行（裁定 `2026-08-22-研究説明文は全ノードに解放内容ベースの1行を書く`）。`graphViewSettings.IconItem` は解放する代表品目。

| 研究 | GUID | 前提 | 消費 | 解放 |
|---|---|---|---|---|
| 半導体製造 | 既存 | 変えない | 変えない | 既存から G2〜G5 ウェハ・ダイLv2〜5・ICチップLv2〜5 の表示を除く（ダイLv1・ICチップLv1 は残す） |
| 品質モジュールLv1 | 既存「品質モジュール」`01a167f3-bd6b-70c9-be6e-52e41907b21f` | 半導体製造 | ICチップLv1 200・鋼鉄フレーム 150・コンピュータ 60 | 既存（品質モジュールLv1 レシピ・表示）＋ダイLv2・ICチップLv2 の表示 |
| 品質モジュールLv2〜5 | 既存「半導体のアップグレード研究Lv2〜5」`01a173a4-cfe2-769c-9b35-eabb655f9a8f`・`…eabc81931e86`・`…eabdf0cceabb`・`…eabe85cfb575` を順に流用 | 品質Lv(N-1)（品質Lv3 だけロケット技術も） | Lv2: ICチップLv2 50・鋼鉄フレーム 200・コンピュータ 100（計算データ無し）／Lv3〜5: 計算データ 2,000/20,000/200,000 ＋ ICチップLvN 75/100/100 | 品質モジュールの製作上限 N（`unlockLevelInheritanceCraftLevel` target=品質モジュールLv1, level=N）・品質モジュールLvN の表示・ダイ/ICチップ Lv(N+1) の表示（N=5 は無し） |
| 速度モジュールLv1 | 既存「機械オーバークロック」`019e3ae2-5005-7439-b242-9b7b3191a2c3` を流用 | 品質Lv1 | 既存のまま | 既存（速度モジュールLv1） |
| 省エネモジュールLv1 | 既存「省エネモジュール」 | 品質Lv1 | 既存のまま | 既存 |
| 生産性モジュールLv1 | 既存「生産性モジュール」 | 品質Lv1（スタック拡張I を外す） | 既存のまま | 既存 |
| 省エネ/速度/生産性モジュールLv2〜5 | 新規 12 | 品質LvN ＋ 同種Lv(N-1) | ICチップLvN 1000（生産性は 1500）・鋼鉄フレーム 200×N・コンピュータ 100×N | 同種の製作上限 N・同種LvN の表示 |
| GPU設計 | 既存 | 半導体製造（品質を外す） | 既存のまま | 既存（GPULv1） |
| GPU Lv2〜5 | 新規 4 | 品質LvN ＋ GPU Lv(N-1)（Lv2 は GPU設計） | ICチップLvN 1000・鋼鉄フレーム 200×N・コンピュータ 100×N | GPU の製作上限 N・GPULvN と GPU衛星LvN の表示・GPU衛星LvN の機械レシピ（今アップグレード研究LvN が持つ `unlockMachineRecipe` をここへ移す） |
| モジュールスタック拡張I | 既存 | 品質Lv1 | 既存のまま | level 1 |
| モジュールスタック拡張II | 既存 | 品質Lv2 ＋ 拡張I | 既存のまま | level 2 |
| モジュールスタック拡張III〜V | 新規 3 | 品質LvN ＋ 拡張(N-1) | ICチップLvN 1500・鋼鉄フレーム 200×N・制御モータ 100×N | level N |
| 金属超精密加工 | 既存 | 半導体製造 | 変えない | 変えない |
| ロケット技術 | 既存 | GPU設計 ＋ 金属超精密加工 | 変えない | 既存から GPU衛星Lv2〜5 の表示を除く |
| 軌道エレベーターの建設 | 既存 | ロケット技術 | 変えない | 変えない |
| 核融合炉・反物質爆弾・最終研究 | 既存 | 変えない | 変えない | 変えない |

品質Lv2〜5（旧アップグレード研究）が持っていた「全モジュールとGPUの製作上限・表示」は上の表どおり各研究へ分配し、品質研究には品質モジュールの分だけを残す。`UIPosition` は Step 4 の操作B で再計算する。

- [ ] **Step 3: 検査スクリプトを通す**

Run（master worktree で）: `python3 tools/research_sync/sync_machine_recipe_unlocks.py --check` → `python3 tools/progression/check.py` → `python3 tools/tier9_balance/run.py`
Expected: すべて exit 0。progression の `_wafer_grades` は削除し、`_emit_dies` は「Tier t の表で割合が正のレベルを、ダイシングに Tier t の品質モジュールを挿せる時点で到達可能とする」（Tier0 は常時、Tier t≥1 は品質モジュールLv t が到達可能になった後）到達性に置き換え、fixture とテストを新キーへ直す。tier9_balance の `cleanroom_checks.py`・`test_s1_tables.py` がグレードや `gradeDieTables` を読んで落ちる場合は、新方式の検査（R10 の表が R3 の規則を満たす・Tier1〜4 の1個クラスA期待値が既定70/レア10/失敗20）へ書き換えてから再実行する。sync スクリプトが差分を出したら `--check` なしで適用し、差分が研究の付け替えと矛盾しないことを確かめる。

- [ ] **Step 4: nodeGraph を組み直し、操作A・Bを回す**

- 削除した16品目のノードと、それらに繋がる線（edge）を消す。G1 の4ノードのラベルは名前変更に追従させる（ノードは `masterGuid` で照合）。
- 新しい研究ノード（品質Lv2〜5 は流用 GUID なので既存ノードの名前と位置を更新、新規 19 ノードを追加）を前半の配置文法で置く: 品質Lv1〜5 は縦の幹（下へ 260〜300 刻み）、各品質LvN の右に同じ行で省エネ・速度・生産性・GPU・スタック拡張の LvN を 280〜360 刻みで並べ、その研究が解放する品目（モジュールLvN・GPULvN・GPU衛星LvN）を研究の右下（右へ 100〜1300、下へ 120〜560）に置く。研究と解放物の間には線を引かない。ノード同士の重なり0・線がノードを横切る箇所0を目視とスクリプトで確かめる。
- master-refine スキルの操作A（`recalc_sort_priority.py`）と操作B（`recalc_research_positions.py`）を v8 に対して実行する（手順は `.agents/skills/master-refine/SKILL.md`）。

- [ ] **Step 5: master 側をコミット・push し、PR を作る**

```bash
git -C /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/chip-quality-research-redesign add -A
git -C /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/chip-quality-research-redesign commit -m "v8: ウェハグレードを廃止し、ダイシングの品質Tier表と品質モジュール幹の研究ツリーへ組み直す"
git -C /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/chip-quality-research-redesign push -u origin feat/chip-quality-research-redesign
```

PR は pr-create スキル（master repo 向け）で作る。本文は本 repo の PR と相互リンクする。

- [ ] **Step 6: 本 repo のピンを更新して検証する**

`.moorestech-external-revisions.json` の `moorestech_master.commitHash` を Step 5 の push 済みコミットにし、ピン worktree の更新手順（`docs/development/worktree-and-review-pitfalls.md` の「master ピン」節）に従ってマスタを差し替えてから、v8 マスタを読むテストを走らせる。

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MasterValidation|Mooresmaster|CleanRoom"`
Expected: ErrorCount 0 / 全 PASS（`MooresmasterLoaderException` が出ない）

- [ ] **Step 7: 本 repo をコミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "moorestech_master のピンをチップ品質・研究ツリー組み直しのコミットへ更新"
```

### Task 6: unityプレイ録画テストのシナリオを新方式へ直して実行する

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-1a-chip-line.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-3-yield-ui.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-5-compute-research.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-2-modules-gpu.cs`（研究 GUID 一覧が変わる場合）

**Interfaces:**
- Consumes: Task 2〜5 の挙動とマスタ。
- Produces: 録画と result.json（`../moorestech_logs` の規約どおり、録画は worktree の gitignore 下）。

- [ ] **Step 1: シナリオを書き換える**

- `tier9-1a`: 出力の選択名を「トランジスタ設置済みウェハ」「1層パターンウェハ」（G1 無し）に。「完成パターンウェハG2」をダイシングして Lv2 を待つ部分は、ダイシング装置に品質モジュールLv1 を挿して完成パターンウェハをダイシングし、Lv1 ダイ ≥10 と Lv2 ダイ ≥1 を待つ形に変える（Lv2 は レア 10% のため待機上限を延ばす）。パッケージングで ICチップLv1 と Lv2 ができることは残す。
- `tier9-1a`・`tier9-3` 共通: 成膜装置の設置を EUV装置の設置に置き換え、トランジスタ設置済みウェハは EUV装置で作る（EUV 装置はトランジスタ設置と露光3層の両方を選べる）。
- `tier9-3`: EUV装置でトランジスタ設置済みウェハのレシピを選んだときの歩留まり欄が「役割なし」表示になること、ダイシング装置で `clean-room-yield-quality-tier`・`clean-room-yield-level-rate-1`・`clean-room-yield-failed-rate`・`clean-room-yield-die-loss-rate` が出ることを確かめる。`max-grade`・`promotion-chance` の検査を削除。
- `tier9-5`: `Single(r => CraftLevel(r)?.Level == 2)` を「品質モジュールLv2（GUID は旧アップグレード研究Lv2 の流用値）を名指しし、その研究が品質モジュールの製作上限2を解放する」形に変える。GPULv1 を target とする検査は GPU Lv2 研究を名指しする形に移す。
- `tier9-2`: 完了させる研究 GUID 一覧に品質モジュールLv1 などの名前変更があっても GUID は同じなので、一覧の差分だけ直す。

- [ ] **Step 2: 実行する**

unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh` で tier9-1a・tier9-2・tier9-3・tier9-5 を実行する（Editor は自分の worktree のもの）。
Expected: 全シナリオ result.json が成功。録画でダイシング装置の歩留まり欄に品質Tierとレベル別割合が出ている。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/tier9
git commit -m "Tier9 録画シナリオをウェハグレード廃止と研究の組み直しに合わせる"
```

### Task 7: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。Codex ホストでは `references/codex-host.md` の runner で実行する。レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、反映後のバイナリで Task 6 の録画テストを再実施してから完了とする。

### Task 8: PR 作成とセッション終了可能化

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。本 repo の PR と moorestech_master の PR を相互リンクし、本 repo のピンが master 側 PR の push 済みコミットを指していることを確かめる。PR 本文の成果物の載せ先は環境ローカル規約（CLAUDE.local.md「PR の成果物の載せ先」）に従う。

---

## 判断記録（ADR）

- 設計: `docs/adr/0094-chip-level-by-dicing-quality-tier-and-quality-led-research.md`、裁定 `.decisions/2026-11-02-*.md`（11件）。
- planning 中の判断（出所: agent前提）:
  - ダイ表はスキーマ上「Tier ごとの Lv1〜LvMax 割合＋失敗ダイ割合」の明示表にし、規則（既定・レア・失敗枠）は表の値とバリデータの品質ゲート検査で守る。式で表を生成する案より、マスタで数値を直接調整できる方を取った。
  - レア枠シフトの基準は「最高Tierモジュール1個の効果値」を引いた量にした。裁定「1個・クラスA で既定70・レア10・失敗20」を表の値そのものにするためで、クラスが下がると負のシフトでレアが減る（裁定「クラスはレアの割合も上げる」の向きと一致）。
  - 最高Tierモジュール1個の効果値を `MachineModuleEffect` に足す。同じ集計ループで求まる値で、クリーンルーム側でモジュール一覧を辿り直さないため。
  - トランジスタ設置済みウェハのレシピは役割なしになる（失敗判定が無く、効く先が無い）。テスト mod の成膜用ブロック・レシピ（`ForUnitTestMachineRecipeId` の成膜）は、役割なしレシピの検査用としてそのまま残す。
  - 既存研究の GUID を流用する（品質モジュール→品質Lv1、アップグレード研究Lv2〜5→品質Lv2〜5、機械オーバークロック→速度モジュールLv1）。研究ノードの GUID を新規採番しない分、マスタ差分が小さい。
  - 削除するウェハは移行で G1 へ寄せる（刈り込みで消すと工程途中の在庫を失うため）。EUV・ダイシングの旧継承レベルと旧抽選記録は移行で捨てる（無いと `RestoreDrawRecord` が例外で落ちる）。
  - プロトコルの MessagePack キーは WaferGrade 削除後に詰めて振り直す（後方互換は考慮不要。サーバーとクライアントは同じビルド）。
  - 品質Lv2〜5 研究の ICチップ数（50/75/100/100）、各モジュール Lv2〜5 研究の素材数、Tier5 と失敗枠の中の配分、`maxRareShift` 0.2 は試算による仮置き（裁定は「少数」「多め」「上限あり」まで）。
  - 品質Lv3〜5 の計算データ量は、既存裁定の 10 倍刻みの値（Lv3 2,000・Lv4 20,000・Lv5 200,000）をそのまま使う。品質Lv2 の素材（ICチップLv2 50・鋼鉄フレーム 200・コンピュータ 100）は試算による仮置き。
- planning 中のユーザー裁定（2026-11-02）:
  - 成膜装置を削除し、トランジスタ設置済みウェハのレシピを EUV露光式半導体製造装置へ移す（「挿せなくする」裁定を置き換え）。原文「じゃあ成膜装置いらなくない？」→ 提示「成膜装置を削除し、レシピをEUVに移す」に「はい」。[[2026-11-02-成膜装置を削除しトランジスタ設置済みウェハはEUV装置で作る]]
  - 旧アップグレード研究を終えたワールド・スタック上限を超えて挿してあるワールドへの手当てはしない。原文「2 ←そのユーザーは存在しないので問題として上がらない。 3←これも同様に問題として上がらない」。[[2026-11-02-旧研究完了とスタック上限超過のワールドは手当てしない]]
  - 品質Lv2 は計算データを要求せずロケット技術も前提にしない。計算データは品質Lv3 から、品質Lv3 の前提にロケット技術を付ける。原文「4 ←つけない。計算データを要求するのはレベル3から。レベル3の前提にロケットはつける」。[[2026-11-02-計算データは品質Lv3からで品質Lv3の前提にロケット技術を付ける]]
- unityプレイ録画テスト: ダイシングの歩留まり欄（UI）と抽選結果（ゲームプレイ）に触れるため Task 6 で実施する。
- マスタ層の改修判断（出所: ADR 0094 の裁定を受けた agent前提）: `VanillaSchema/cleanRoom.yml` は `gradeDieTables` を `qualityTierDieTables`＋`maxRareShift` へ置き換え（optional・既定値補完なしで全 JSON 一括更新）。`moorestech_server/Assets/Scripts/Core.Master/CleanRoomMaster.cs`・`Core.Master/CleanRoomRoles/DicingRecipeRole.cs` は役割表から成膜を外すだけで役割の形は既存のまま。`Core.Master/Validator/CleanRoom/CleanRoomMasterUtil.cs`・`CleanRoomDicingValidator.cs`・`CleanRoomLayerExposureValidator.cs`・`CleanRoomRoleRecipeChecks.cs`・`CleanRoomYieldTableRules.cs` は継承前提の検査を「継承なし」の検査へ、グレード別表の規則を Tier 別表の品質ゲート規則へ置き換える（マスタ不正は起動時の検証で止める既存の防御を保つ）。
- プロトコル層の改修判断（出所: agent前提）: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CleanRoom/CleanRoomYieldMessagePack.cs`・`GetCleanRoomYieldInfoProtocol.cs`・`CleanRoomYieldResponseReader.cs`・`CleanRoomYieldView.cs` は既存の要求応答型 `va:getCleanRoomYieldInfo` の形を保ったまま WaferGrade 種別を削除し Dicing の中身を増やす。新しいプロトコルやイベントは作らない（歩留まりは機械 UI を開いた間のポーリングで読む既存方式のため）。
