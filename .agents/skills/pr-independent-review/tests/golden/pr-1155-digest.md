# PR #1155 機械UI改修: 進捗矢印の共通化・タブ入替/未選択時レシピ表示・電力の充足率化（ADR 0010）

```yaml
pr: 1155
head: ad35195a84d2410bb76585d9a44cff7bacb123d5
verdict: reject
verdict_line: Critical 8 / Warning 30 / Info 20 / suppressed 0 / 新形 0。対象 ad35195a8（base 9631e8e1f）。report-only のため修正の適用は行っていない。
date: 2026-08-18
generated_at: 2026-08-18T02:55:00+09:00
```

## 歯車機械の要求トルク率に上限なしの電力倍率を流すが供給側は1でクランプするため、需要だけ増えて供給・表示・速度が改善しない

```yaml
slug: gear-torque-rate
category: design-decision
severity: critical
must_read: true
summary: 需要だけ1.5倍に膨らみ、供給も速度も増えない。
index_label: 歯車機械に倍率を効かせるか（案A/案Bは排他）
label: 歯車機械の要求トルク率に上限なしの電力倍率を流し込む変更の設計判断カード（実コード抜粋つき）
files: [moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaGearMachineComponent.cs:40]
options:
  - GearConsumptionCalculator.CalcOperatingRate / CalcCurrentPower に requestRate 引数を足し、required と供給の両方へ倍率を反映して要求と供給を一致させる（推奨）
  - SetTorqueRequestRate を Processing ? 1f : idleRate へ戻し、歯車機械では PowerMultiplier を要求側に載せない（表示分母も基礎値へ戻す）
```

```code-card
36|        private void UpdateTorqueRequestRate()
37|        {
-40|            var isProcessing = _vanillaMachineProcessorComponent.CurrentState == ProcessState.Processing;
-41|            _gearEnergyTransformer.SetTorqueRequestRate(isProcessing ? 1f : _idleTorqueRate);
+38|            // 表示の分母・加工速度と同じ導出（EffectiveRequestPowerRate）をそのまま歯車網への要求へ反映する
+39|            // Feed the gear network the same derivation used for the display denominator and processing speed (EffectiveRequestPowerRate)
*+40|            _gearEnergyTransformer.SetTorqueRequestRate(_vanillaMachineProcessorComponent.EffectiveRequestPowerRate);
41|        }
```

**PR側の主張:** 歯車機械の要求トルクにもモジュール倍率を反映し、表示分母・加工速度・歯車網要求を一致させる `[agent前提]`（PR本文「レビュー裁定」／PR内新設 `.decisions/2026-08-17-歯車機械の需要にもモジュール倍率を載せる.md`・免責力なし）。Fable も「意図どおり」と判定した。

**独立レビューの実測:** `MachineModuleEffect.cs:75` の倍率は下限のみで上限クランプがない。一方 `GearConsumptionCalculator.cs:41-52` の `required` は rate を掛けない素の値で `Mathf.Min(currentTorque / required, 1f)` により rate>1 分を必ず捨て、`CalcCurrentPower` は `_torqueRequestRate` を参照しない。結果、`GearNetworkPowerCalculator.cs:44-47` で `demandPower > availablePower` となり **同一歯車網の全消費者が `OverRequirePower` で停止**し、停止しない場合も充足率は恒久的に 67%（赤固定）になる。5系統がこの二択を提示し、Fable の「意図どおり」判定は供給側の導出を読んでいない。

**併用推奨（案C相当）:** `readonly struct TorqueRequestRate` を切り、定義域 [0,∞) の `PowerMultiplier` と `float` 取り違えを型で止める。

## 充足率の表示可否を判別子 currentState でなく requestPower !== 0 の数値センチネルで判定しており、実在マスタで誤表示する

```yaml
slug: power-rate-visibility
category: design-decision
severity: critical
must_read: true
summary: 石窯とボイラーで電力行が丸ごと消える。
index_label: 充足率を隠す判断の正本を state に置くか実効要求値に置くか
label: 充足率の表示可否を requestPower !== 0 の数値センチネルで判定している箇所の設計判断カード（実コード抜粋つき）
files: [moorestech_web/webui/src/features/blockInventory/details/detailLogic.ts:73]
options:
  - state 由来の1枚テーブルへ集約し、MachineStateKeys / MachineStateInsufficientTone も統合する（状態追加が必ずコンパイルエラーになる・推奨）
  - 「0 なら描画しない」を所有者 PowerRateText の early return へ移し computePowerRate の 0→1 を撤去する（MinerSection の既存表示も変わる）
```

```code-card
+71|// 要求電力0（停止中）は充足率が意味を持たないため、表示自体を出さない判断をここで確定する
+72|// A request power of 0 (halted) makes the satisfaction rate meaningless, so the decision to hide it is settled here
*+73|export function isPowerRateMeaningful(requestPower: number): boolean {
*+74|  return requestPower !== 0;
+75|}
```

**PR側の主張:** 実効要求0の機械は充足率を表示せず停止中ラベルのみ出す `[agent前提]`（PR内新設ADR 0010・PR本文R4。免責力なし）。

**独立レビューの実測:** 実マスタ `../moorestech_master/server_v8/.../blocks.json` に **石窯（L94・`requiredPower: 0`）とボイラー（L1937・同）**が実在し、`EffectiveRequestPower = 0 × rate = 0` が常に成立する。よって**正常稼働中でも電力行が消え**、「電力が来ていない」のか「需要が0」なのかユーザーが区別できない。判別子 `currentState` は本PRで `z.enum(["idle","processing","halted"])` に narrowing 済みで**ワイヤ上に既に存在する**のに使っていない。`requestPower === 0` の解釈は現状4通りに分裂（`detailLogic.ts:74`＝表示可否 ／ `detailLogic.ts:8`＝100%充足 ／ `CommonMachineBlockStateDetail.cs:35`＝同上 ／ `MinerSection.tsx:15`＝判定なしで常時表示）。7系統一致。

**決着に要る追加の裁定:** 石窯・ボイラーを「需要なし」と明示表示するか（`L.ui.blockInventory.noDemand` の前例が `NetworkSections.tsx:16` にある）／`MinerSection` の既存表示も揃えて消すか。`CommonMachineBlockStateDetail` に判別共用体を持たせる案はMessagePackワイヤ変更を伴うため非推奨。

## 実効率導出・ラッチ・公開が2 processor へ逐語重複し集約が途中で止まっており、両者の Halted 扱いが既に食い違う

```yaml
slug: effective-rate-dedup
category: design-decision
severity: critical
must_read: true
summary: 同じ導出が2箇所に写され、既に食い違っている。
index_label: 実効率導出の集約先の選択（ChangeSelectionの再ラッチ判断が従属する）
label: 2つのprocessorへ逐語コピーされた実効率導出とラッチ手続きの集約先を選ぶ設計判断カード（実コード抜粋つき）
files: [moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs:29, moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs:31]
options:
  - MachineProcessContext へ率の網羅switchとラッチ手続きを集約する（推奨・ChangeSelectionの再ラッチ修正と同一編集）
  - 率は IMachineProcessState に float RequestPowerRate を足して実装側へ散らし、状態追加をコンパイルエラーで検知させる（ラッチのみ context へ集約）
```

```code-card
+27|        // 稼働状態に応じてアイドル倍率かモジュール倍率を選ぶ要求電力率。歯車機械の要求トルク率もここから導出する
+28|        // Requested power rate selecting the idle rate or module multiplier by state; the gear machine's requested torque rate also derives from here
*+29|        public float EffectiveRequestPowerRate => CurrentState == ProcessState.Processing ? _context.ProcessingPowerMultiplier : _idlePowerRate;
30|
31|        // 稼働状態に応じてアイドル倍率かモジュール倍率を適用した要求電力
32|        // Requested power applies the idle rate or module multiplier based on the active state
-29|        public float EffectiveRequestPower => _context.RequestPower *
-30|                                              (CurrentState == ProcessState.Processing ? _context.EffectComponent.AggregateCurrent().PowerMultiplier : _idlePowerRate);
+33|        public float EffectiveRequestPower => _context.RequestPower * EffectiveRequestPowerRate;
```

同概念の写し（CleanRoom 側・`Halted` の扱いだけ既に食い違っている）:

```code-card
-29|        // 停止中は要求電力を0にし、稼働中だけ通常機械と同じ倍率を適用する
+29|        // 停止中は0、稼働中は通常機械と同じ倍率
30|        // Halted machines request no power; operating states use the same multipliers as normal machines
-31|        public float EffectiveRequestPower => CurrentState switch
*+31|        public float EffectiveRequestPowerRate => CurrentState switch
32|        {
*33|            ProcessState.Halted => 0f,
-34|            ProcessState.Processing => _context.RequestPower * _context.EffectComponent.AggregateCurrent().PowerMultiplier,
-35|            ProcessState.Idle => _context.RequestPower * _idlePowerRate,
+34|            ProcessState.Processing => _context.ProcessingPowerMultiplier,
+35|            ProcessState.Idle => _idlePowerRate,
36|            _ => throw new ArgumentOutOfRangeException(),
37|        };
38|
+39|        public float EffectiveRequestPower => _context.RequestPower * EffectiveRequestPowerRate;
```

**PR側の主張:** `ProcessingPowerMultiplier` を `MachineProcessContext` へ寄せて「1箇所から取得」した `[agent前提]`（コード内コメント）。集約はそこで止まっている。

**独立レビューの実測:** フィールド宣言・ctor 初期ラッチ・`GetBlockStateDetails` の公開・Update 冒頭3文が**日英2行コメントごと逐語一致**で両クラスに存在し、率だけ Vanilla=三項（`Halted` が無言で `_idlePowerRate` に落ちる）／CleanRoom=網羅switch（`Halted => 0f`）と**既に食い違っている**。両ファイルは 217行 / 222行で200行上限を超過。故障は仮定でなく patch 内で既に1回起きており（CleanRoom の Halted 固着を `:177-178` の手動0埋めで塞いだ）、同型の `ChangeSelection` 2件（[F:change-selection-latch]）では漏れている。4系統一致。

**裁定ポイント:** ラッチの置き場所は `MachineProcessContext` 一択で拮抗しない。選択は「率の分岐を context の網羅switch+throw に置くか、`IMachineProcessState` の実装へ分散させるか」だけ。どちらでも [F:change-selection-latch] の直し方（`ChangeSelection` 直後の再ラッチを1行で呼ぶ）が決まるため、**[F:change-selection-latch] より先に決める必要がある**。

## ProcessingPowerMultiplier を毎回集計のままにするか tick スナップショットにするか

```yaml
slug: multiplier-snapshot
category: design-decision
severity: medium
must_read: false
summary: 同一tick内で別インスタンスの倍率を読む。
label: ProcessingPowerMultiplierを毎回集計するかtickスナップショットにするかの設計判断カード（実コード抜粋つき）
files: [moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/MachineProcessContext.cs:26]
options:
  - GameUpdater.CurrentTick スタンプ付きスナップショットにする。モジュール着脱の反映が最大1tick遅れる代わりに基準一致が構造的に保証される（推奨）
  - 現状維持（毎回 AggregateCurrent。1tickに機械あたり3〜4回のList確保とMasterHolder参照が走る）
```

```code-card
22|        public float SuppliedPower;
23|        public float CurrentPower;
24|
+25|        // 加工中の効果倍率を1箇所から取得し、processorのEffectiveRequestPowerRateと計算式を共有する
*+26|        public float ProcessingPowerMultiplier => EffectComponent.AggregateCurrent().PowerMultiplier;
```

**PR側の主張:** 倍率の単一取得点を作り、加工速度・表示分母・歯車要求で式を共有する `[agent前提]`（コード内コメント）。式の一致という点では改善している。

**独立レビューの実測:** 非キャッシュ getter のため同一tick内で同じモジュール集計を3〜4回作り直す（`AggregateCurrent()` は毎回 List 確保＋`MasterHolder.ItemMaster` 参照。Processing中1台あたり電気/CleanRoom 60→80回/秒、歯車 20→60回/秒）。性能だけの話ではなく、`EnergySegment.SettleTick` が読んだ倍率と `Update()` がラッチする倍率が**別インスタンス**のため、PRの掲げた「基準一致」が tick 内で完全には成立していない。

## 共有部品 ProgressArrowGlyph が --craft-arrow-* というドメイン語彙トークンを参照している

```yaml
slug: arrow-token-name
category: design-decision
severity: medium
must_read: false
summary: 汎用部品がcraftドメインの語彙を参照する。
label: ProgressArrowGlyphが参照する既定寸法トークンの命名に関する設計判断カード（実コード抜粋つき）
files: [moorestech_web/webui/src/shared/ui/ProgressArrowGlyph/style.module.css:4]
options:
  - 現状維持。前例 GamePanel の --craft-grip-* 参照と同形で、追加トークンも増えていない（推奨）
  - tokens.css の3トークンを --progress-arrow-glyph-* へ改名し参照3箇所を差し替える（兄弟 ProgressArrow の中立名と揃う）
```

```code-card
+1|/* 既定寸法はクラフト矢印と同値のトークンを直接参照する。呼び出し側は寸法を持たない */
+2|/* Default size references the craft-arrow tokens directly; callers no longer size this themselves */
+3|.arrow {
*+4|  width: var(--craft-arrow-width);
*+5|  height: var(--craft-arrow-height);
+6|}
```

**PR側の主張:** 矢印の既定寸法は部品CSSがトークンを1箇所参照して持つ（**別名トークンを増やさない**） `[agent前提]`（PR内新設 `.decisions/2026-08-17-矢印グリフの既定寸法は部品がトークン参照で持つ.md`。免責力なし）。前例として `shared/ui/GamePanel/style.module.css:97-103` が `--craft-grip-*` を参照している［検証済み］。

**独立レビューの実測:** 兄弟の `ProgressArrow` は中立名 `--progress-arrow-*`（`tokens.css:179,182`）を使っており、`shared/ui` 内で語彙が割れている。`[ADR: AGENTS.md#設計原則]`（汎用基盤にドメイン語彙を持ち込まない）に抵触。PR内新設 `.decisions` は「別名トークン新設」案しか検討しておらず、**改名案は検討されていない**。改名は別名追加ではないので同文書の方針とも矛盾しない。

## ProgressArrow（矩形バー）と ProgressArrowGlyph（SVG矢印）が並立し、同じ機械パネル内で矢印表現が2種混在する

```yaml
slug: arrow-duplication
category: design-decision
severity: medium
must_read: false
summary: 同じ機械パネル内で矢印表現が2種混在する。
label: ProgressArrowとProgressArrowGlyphが同一責務で並立している状態の解消方針を選ぶ設計判断カード（実コード抜粋つき）
files: [moorestech_web/webui/src/shared/ui/index.ts:8]
options:
  - 見た目は変えず ProgressArrow → ProgressArrowBar へクロスファイル改名し、2部品の役割差を名前に出す（推奨）
  - MinerSection と FluidSlotRow も Glyph へ寄せ、旧 ProgressArrow とトークンを削除する（採掘機・流体行の見た目が矩形バー→矢印グリフに変わる）
```

```code-card
5|export { default as BlockSlot } from "./BlockSlot";
6|export { default as FluidSlot } from "./FluidSlot";
7|export { default as FluidSlotRow } from "./FluidSlotRow";
*8|export { default as ProgressArrow } from "./ProgressArrow";
*+9|export { default as ProgressArrowGlyph } from "./ProgressArrowGlyph";
10|export { default as SlotGrid } from "./SlotGrid";
```

**PR側の主張:** 進捗矢印を `shared/ui/ProgressArrowGlyph` へ昇格し、クラフト画面と機械の加工行で同一部品・同一寸法にする `[agent前提]`（PR本文R1・PR内新設ADR。免責力なし）。

**独立レビューの実測:** 移したのは craft / machine の2箇所のみで、`MinerSection.tsx:14` と `FluidSlotRow/index.tsx:23` は旧型（div幅%の矩形バー）のまま。**同じ機械パネル内で矢印表現が2種混在**し、目標「同一部品・同一寸法」が半分しか達成されていない。

## ChangeSelection が状態遷移後に _publishedRequestPower を再ラッチせず、分子分母の基準ズレをレシピ変更経路で再発させる

```yaml
slug: change-selection-latch
category: critical
severity: critical
summary: レシピ切替のたび偽の赤が出る。
label: ChangeSelectionが状態遷移後に_publishedRequestPowerを再ラッチしないCriticalカード（実コード抜粋つき）
files: [moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs:216, moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs:137]
options:
  - ChangeSelection の2箇所で状態書き換え直後に再ラッチする（実効率導出の集約と同一編集にまとめる）
```

```code-card
135|            }
136|
*137|            if (CurrentState == ProcessState.Processing) CurrentState = ProcessState.Idle;
138|            _context.SelectedRecipe = recipe;
*139|            _changeState.OnNext(Unit.Default);
140|            return MachineRecipeSelectionResult.Success;
```

```code-card
+214|            // Halted含む非IdleはIdleへ戻し、次Updateで清浄室条件が再評価される
+215|            // Non-Idle including Halted returns to Idle so the next Update re-evaluates clean-room conditions
*216|            if (CurrentState != ProcessState.Idle) CurrentState = ProcessState.Idle;
217|            _context.SelectedRecipe = recipe;
*218|            _changeState.OnNext(Unit.Default);
```

**PR側の主張:** 表示用要求電力を `CurrentPower` と同位置でラッチし、分子分母の基準ずれによる偽の赤/500%表示を解消する `[agent前提]`（PR本文「レビュー裁定」）。実際のラッチ点は Update 冒頭（`:171` / `:151`）の1箇所のみ。

**独立レビューの実測:** 両メソッドとも `CurrentState` を書き換えた直後に `_changeState.OnNext` を発火するが、`_publishedRequestPower` の再確定がない。`BlockSystem.cs:44` の購読が同期的に `GetBlockStateDetails()` を呼ぶため、`currentState:"idle"` に Processing 基準の高い分母が同梱され、次 Update まで**「待機中なのに加工基準の低い充足率（偽の赤）」**が出る。レシピ変更のたびに毎回起こりうる。3系統一致。

**修正方針:** `CurrentState` 書き換えの直後・`_changeState.OnNext` の**前**に `_publishedRequestPower = EffectiveRequestPower;` を置く。[F:effective-rate-dedup] の集約を採る場合は `_context.LatchTickPower(CurrentState);` の1行に畳み、2ファイルで同じ形にする（**[F:effective-rate-dedup] と同一 surface のため1つの編集にまとめること**）。同型掃引済み・残り0件。

## CleanRoom の EffectiveRequestPowerRate が他ファイル参照0のまま public

```yaml
slug: unused-public-rate
category: critical
severity: critical
summary: 外部参照0のpublicが残っている。
label: CleanRoomMachineProcessorComponentのEffectiveRequestPowerRateが参照0のままpublicになっているCriticalカード（実コード抜粋つき）
files: [moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs:31]
options:
  - private へ落とす（実効率導出の集約を採るなら context へ移動して消滅）
```

```code-card
-29|        // 停止中は要求電力を0にし、稼働中だけ通常機械と同じ倍率を適用する
+29|        // 停止中は0、稼働中は通常機械と同じ倍率
30|        // Halted machines request no power; operating states use the same multipliers as normal machines
-31|        public float EffectiveRequestPower => CurrentState switch
*+31|        public float EffectiveRequestPowerRate => CurrentState switch
32|        {
33|            ProcessState.Halted => 0f,
-34|            ProcessState.Processing => _context.RequestPower * _context.EffectComponent.AggregateCurrent().PowerMultiplier,
-35|            ProcessState.Idle => _context.RequestPower * _idlePowerRate,
+34|            ProcessState.Processing => _context.ProcessingPowerMultiplier,
+35|            ProcessState.Idle => _idlePowerRate,
36|            _ => throw new ArgumentOutOfRangeException(),
37|        };
38|
+39|        public float EffectiveRequestPower => _context.RequestPower * EffectiveRequestPowerRate;
```

**PR側の主張:** Vanilla 側と同じ形で率を切り出した `[agent前提]`（差分実体から観測）。

**独立レビューの実測:** `rg` 実測で CleanRoom 側の参照は**自ファイル `:39` のみ**（プロダクション・テストとも他ファイル0件）。Vanilla 側は `VanillaGearMachineComponent.cs:40` という実消費者を持つので非対称で、CleanRoom には歯車消費者が構造的に存在しない。`[ADR: AGENTS.md#その他の規約]`「デバッグ/テスト専用publicを残さない」の隣接規約に抵触。なお死にメンバーゲート（IL解析）はレビューworktree未コンパイルのため**skipped**で、本件は `rg` 実測で代替検出した。

**修正方針:** `public` → `private`。[F:effective-rate-dedup] の集約を採る場合は context 側へ移って本メンバー自体が消えるため、**[F:effective-rate-dedup] と一括で処理する**。同型掃引済み・残り0件。

## capture-machine-qa.ts が gearMachine で必ずタイムアウト死する（本PRが条件付き非表示にした要素を無条件参照）

```yaml
slug: qa-capture-timeout
category: critical
severity: critical
summary: QAスクリプトが必ず落ちて撮影物が残らない。
label: capture-machine-qa.tsがgearMachineで必ずタイムアウト死するCriticalカード（実コード抜粋つき）
files: [moorestech_web/webui/e2e/capture-machine-qa.ts:136]
options:
  - 「要求0で率が非表示」を manifest に記録する形へ直し、try-catch・旧manifest残置・デフォルト引数の3点も前例どおりに整える（推奨）
```

```code-card
+133|    const stateLabel = page.getByTestId("machine-state-label");
+134|    const powerRate = page.getByTestId("machine-power-rate");
+135|    manifest.gearMachineStateLabelText = await stateLabel.textContent();
*+136|    manifest.gearMachinePowerRateText = await powerRate.textContent();
+137|    const panel = page.getByTestId("block-inventory");
+138|    const [panelBox, stateLabelBox, powerRateBox] = await Promise.all([
+139|      panel.boundingBox(),
+140|      stateLabel.boundingBox(),
*+141|      powerRate.boundingBox(),
+142|    ]);
```

**PR側の主張:** ADR 0010 の矢印グリフ・中心線・フッタ・タブ順を撮影して実測する目視QA `[agent前提]`（ファイル冒頭コメント）。

**独立レビューの実測:** `e2e/mock-host/blockDetailFixtures.ts:50-51` の `blockGearMachine` は `currentPower: 0.0` / `requestPower: 0.0` で、同PRが入れた `isPowerRateMeaningful(machine.requestPower)`（`MachineSection.tsx:36`）が false になるため `machine-power-rate` が **DOM に出ない**。`locator.textContent` が30秒タイムアウト → `void main()` の未処理 rejection でプロセスが落ち、`manifest.json`・`gearmachine-*.png` が一切生成されず、`browser.close()`/`wss.close()`/`server.close()` も走らず Chromium とポート5411が残留する。**ADR 0010 の「実効要求0の機械は充足率を出さない」を検証するはずのQAが、まさにその状態のブロックで死ぬ。**3系統一致。

**修正方針（4点セット）:** ① `:136` を `(await powerRate.count()) === 0 ? null : await powerRate.textContent()` にし、`:138-142` の `Promise.all` から `powerRate.boundingBox()` を外して `:144` 以降のクロップ基準を `stateLabelBox` 単独へ切り替える。② `:159-175` の `try { readFile } catch { sha256: null }` を撤去し前例 `capture-mining-progress.ts:97-101` どおり素の `await readFile` にする `[ADR: AGENTS.md#設計原則]`。③ `:34` の `mkdir` 直後に `rm(join(OUT_DIR, "manifest.json"), { force: true })` を足す（前例 `capture-mining-progress.ts:27`）。④ `:15` の `padding = CROP_PADDING_PX` を必須引数へ `[ADR: AGENTS.md#その他の規約]`。同型掃引済み・残り0件。

## 初期タブが data.machine 未着のマウント時に recipes で確定・固着し、PRの中核仕様が実運用経路で破れる

```yaml
slug: initial-tab-stuck
category: critical
severity: critical
summary: 選択済み機械でもレシピタブに固まる。
label: MachineSectionの初期タブがmachine未着時にrecipesで固着するCriticalカード（実コード抜粋つき）
files: [moorestech_web/webui/src/features/blockInventory/details/MachineSection.tsx:18]
options:
  - SectionStackView でマウント境界を machine 到着へ合わせ、machineInitialTab を非nullable に絞る（推奨）
  - userTab: string | null を持ち、描画時に userTab ?? machineInitialTab(...) で解決する
```

```code-card
16|export default function MachineSection({ data }: { data: BlockInventoryOpen }) {
17|  const machineRecipes = useTopic(Topics.machineRecipes);
-17|  const [tab, setTab] = useState("inventory");
*+18|  const [tab, setTab] = useState<string>(() => machineInitialTab(data.machine?.selectedRecipeGuid));
19|  const { t } = useI18n();
20|  if (!data.machine) return null;
21|  const machine = data.machine;
```

**PR側の主張:** レシピ未選択で開いた場合の初期タブをレシピ選択にし、開いた後の手動切替は尊重する `[agent前提]`（PR本文R2/R3）。

**独立レビューの実測:** `SectionStackView.tsx:50` は `<MachineSection data={data} />` を**無条件にマウント**し、lazy initializer（マウント時1回のみ）が `if (!data.machine) return null`（`:20`）**より前**に走る。`machineInitialTab` は `isEmptyGuid(undefined) === true` で `"recipes"` を返し、再評価の契機は `key={data.identifier}` のみで同一ブロックでは変化しない。ブロック生成直後（`BlockGameObject` の state 未着）に開くと `BlockDetailDtoBuilder.cs:29` の `common != null && machineState != null` が偽で初回 publish に `machine` が乗らず、**レシピ選択済みでもタブが recipes に確定して閉じるまで是正されない**。e2e `machineRecipe.spec.ts` は `setBlock` を `page.goto` の前に行うためこのレースを1本もカバーしていない。4系統一致。

**修正方針:** マウント境界を machine 到着に合わせる。`SectionStackView.tsx:50` を `{data.machine ? <MachineSection machine={data.machine} data={data} /> : null}` にし、`MachineSection` の引数を `machine: MachineDetailData` 込みへ変えて内部の null 分岐（`:20-21`）を削除。併せて `machineInitialTab(selectedRecipeGuid: string)` を非nullableへ絞り、到達不能状態を固定していた `machineRecipeSelectionLogic.test.ts:46-47` の `null`/`undefined` 2ケースを削除する。`useEffect` による後追い同期は派生stateのアンチパターンかつ「開いた後の手動切替を尊重する」目標と衝突するため採らない。

## 本PRの中核変更5件が mutation で1つも死なず、変更した既存テストは production 式を期待値に置く自己参照アサートへ弱体化している

```yaml
slug: mutation-survives
category: critical
severity: critical
summary: 旧実装へ戻しても全テストが緑になる。
label: 中核変更がmutationで死なずテストが自己参照化しているCriticalカード（実コード抜粋つき）
files: [moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Machine/CleanRoomMachineTest.cs:103, moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineFluidIOTest.cs:316]
options:
  - 自己参照アサートを独立計算の期待値に置換し、死んでいる中核5件に1対1でテストを追加する（推奨）
```

```code-card
96|            for (var i = 0; i < 200 && processor.CurrentState != ProcessState.Processing; i++) TickRoom();
97|            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
+98|            // state公開値は前tick基準でラッチされるため、遷移直後の1tickだけ古い値を挟んでから実効値に揃う
+99|            // The published state latches on the previous tick's basis, so it trails by one tick right after a transition before matching the effective value
*+100|            TickRoom();
+101|            // state要求電力は基礎でなく実効値
+102|            // Lock in that the state carries the effective request power, not the raw base value
*+103|            Assert.AreEqual(machineConsumer.EffectiveRequestPower, GetCommonMachineState(machine).RequestPower, 0.01f);
```

**PR側の主張:** state の `RequestPower` を基礎値で検証していた既存テストは実効値前提へ書き換える `[agent前提]`（PR内新設ADR 0010#影響）。

**独立レビューの実測:** `:103` は期待値を被検証コード `machineConsumer.EffectiveRequestPower` から取る**トートロジー**で、モジュールを1個も装着しないため `PowerMultiplier == 1` となり「基礎値でなく実効値」を数値上区別できない。加えて `:100` の `TickRoom()` が**ラッチ機構が守る当のtickを意図的に読み飛ばしている**。`MachineFluidIOTest.cs:518` もリテラル期待を production 式へ置換した弱体化で、`:316` の `machineParam?.IdlePowerRate ?? 0.2f` は `as` キャストが null に落ちても自己整合値で緑になる。`VanillaGearMachineComponent.cs:40` の歯車率・`detailLogic.ts:73` の `isPowerRateMeaningful`・`BlockInventoryPanel.tsx:63` の `key={data.identifier}` を守るテストは**1本も存在せず、旧実装へ戻しても全テストが緑**。`currentState: "halted"` を通す webui テスト／fixture もコードベース全体に0件。3系統一致。

**修正方針:** ①モジュール装着機械（`MachineModuleSlotTest` の装着ユーティリティ）で期待値を `base × 倍率` の独立計算で書き、`:103` と `MachineFluidIOTest.cs:518` の自己参照を置換。②`:98-100` の読み飛ばしをやめ遷移直後1tickで整合を検証し、電気機械側にも同型テストを新設。③`:315-316` の `?? 100`/`?? 0.2f` を廃し `Assert.IsNotNull(machineParam)` を先に置く。④歯車機械の要求トルクを固定するテストを追加（**期待値が変わるため [F:gear-torque-rate] の裁定後に書く**）。⑤`detailLogic.test.ts` に述語の `it.each` を追加し、gearMachine e2e に `toHaveCount(0)` を1行足す（**[F:power-rate-visibility] の裁定で述語が state 由来へ変わる場合はそれに追随**）。⑥`identifier` 差し替えでタブ再評価を検証。

# 注記

## must-read

なぜ必読か: この3件はゲームプレイの方向・実マスタでの誤表示・他Criticalの直し方を決めるため、裁定なしには修正に着手できない。

## other-rulings

推奨案どおりで良ければ一言で足りる3件。

## suppressed

該当なし（0件）。全観点が `[agent前提]` は免責力を持たないと明記して返しており、contract.md の規定と整合する。

## new-shape

該当なし（0件）。新規性ゲートL1で asmdef 参照追加・新文法ともに 0件。参考扱いの `new_edges` 1件は折りたたみ参考④にある。

## criticals

ここに載る5件は具体名・行・直し方が一意で、判断は要らない。残る Critical 3件（[F:gear-torque-rate] / [F:effective-rate-dedup] / [F:power-rate-visibility]）は上の設計判断カードにある。

# 判断台帳

### ユーザー裁定（免責力あり）

- `[ADR: AGENTS.md#コーディングにおける重要な原則]` 1ファイル200行以下・1ディレクトリ10ファイルまで・`partial`禁止・`Func<>`禁止・try-catch は外部境界のみ
- `[ADR: AGENTS.md#その他の規約]` デフォルト引数禁止（引数追加は呼び出し側を全部変更）・単純 getter/setter 禁止・デバッグ/テスト専用publicを残さない
- `[ADR: AGENTS.md#設計原則]` 汎用基盤にドメイン語彙を持ち込まない・`?? Default` で欠損を吸収しない・状態変化の検知は購読で行う
- `[ADR: AGENTS.md#コメント]` 日本語→英語の2行セット・字数目安 ／ `[ADR: AGENTS.md#時間に関して]` 経過時間は `GameUpdater` のティックのみ
- `[ADR: webui-design §8.13]` 矢印の充填は `--gauge-fill` でなく正本の白を引き継ぐ（既存のユーザー裁定。ただし本PRは当該 SKILL.md 自体を変更しているため、**変更後の規範は `[agent前提]` 扱い**）

### agent前提（免責力なし）

- PR本文 R1〜R4 と「レビュー裁定」節に書かれた目標（矢印共通化・タブ入替・充足率化・歯車への倍率反映・ラッチ）
- independent session からの推測・コード内コメントによる自己申告（`ProcessingPowerMultiplier` の「1箇所から取得」等）
- 非目標の申告（uGUI 非改修・e2e 既存赤10件の別issue分離）

### PR内新設ADR（降格済み・要検証）

以下は**本PR自身が新設した判断台帳**であり、独立セッションからは承認の実在を検証できないため**免責力なしへ降格**した。ここに実際のユーザー裁定が含まれるなら教えてほしい（含まれる場合、対応する指摘の格が下がる）。

- `docs/adr/0010-machine-power-display-as-satisfaction-rate.md`（決定1〜3・代替案B却下・影響）
- `docs/adr/0011-recipe-viewer-single-list.md`（本PRに実装を伴わない）
- `.decisions/2026-08-17-*` 16件 — stateのRequestPowerは実効要求電力を送る ／ 歯車機械の需要にもモジュール倍率を載せる ／ 表示用要求電力は供給と同位置でラッチする ／ 要求0の機械は充足率を出さず停止中のみ表示する ／ 電力表示は充足率と稼働状態ラベルに分離する ／ 矢印グリフの既定寸法は部品がトークン参照で持つ ／ 機械レシピの素材は必要数のみで所持数チェックしない ／ 機械レシピエントリはブロックアイコン名前秒数を表示 ／ レシピ単一リストはクラフトレシピを優先表示する ／ クラフトエントリはレシピ行の下に全幅実行ボタン ／ クラフトUI改修はWebUI専任でuGUIは今後見ない ／ アイテム一覧のクラフト可能数0はバッジ非表示 ／ 装飾タブ削除後もアイテム名ヘッダーは残す ／ worktreeはタスク毎使い捨てでメインのUnity起動は日次Library生成のみ ／ 日次Library温めはbatchModeで行いsupervisorで回す ／ ローカルプレイは接続試行フォールバックを廃し必ず内蔵サーバーを起動する
- `docs/superpowers/plans/2026-08-17-machine-ui-refresh.md` ／ `docs/superpowers/plans/2026-08-17-recipe-viewer-single-list.md`（780行の未実装plan）

なお下位3群（レシピビューア系7件・worktree運用/Library温め2件・ローカルプレイ1件・ADR 0011・plan 2本）は**本PRのスコープ外の文書**であり、PR本文に同梱理由の説明がない（Warning に計上）。

# 折りたたみ参考

## ① Critical の修正方針詳細（適用区分つき）

- **[F:gear-torque-rate]**: 設計判断。案A採用時の波及は `GearEnergyTransformerComponent.GetCurrentOperatingRate` / `GetCurrentSuppliedPower`（デフォルト引数禁止のため呼び出し側を明示更新）と gear 系需給テスト群の再検証。案B採用時は歯車機械の表示分母も基礎値へ戻すところまで一貫させる必要がある。同型掃引: `SetTorqueRequestRate` の全6呼び出しを実測し、1を超え得るのは本件のみ・残り0件。
- **[F:power-rate-visibility]**: 設計判断。案A採用時は `MachineStateKeys`・`MachineStateInsufficientTone`・`isPowerRateMeaningful` を1枚のテーブルへ統合し `MachineSection.tsx:36` を `view.showPowerRate && (...)` にする。`requestPower === 0` の4解釈を1つの述語へ寄せること。
- **[F:effective-rate-dedup]**: 自動適用可（集約先が拮抗しない）。`MachineProcessContext` に `IdlePowerRate`・`EffectiveRequestPowerRate(state)`・`EffectiveRequestPower(state)`・`PublishedRequestPower { get; private set; }`・`LatchTickPower(state)`・`PinPowerToZero()` を追加。両processorのフィールド（`_idlePowerRate`・`_publishedRequestPower`）を削除し、Update 冒頭3文を1行へ、CleanRoom `:175-178` を `PinPowerToZero()` へ、`ProcessingMachineProcessState.cs:87` を `_context.EffectiveRequestPower(ProcessState.Processing)` へ。公開APIのシグネチャは維持されるため各Componentr/Template/既存テストは無変更で通る。副次効果として両ファイルが200行以内へ戻る。
- **[F:change-selection-latch]**: 自動適用可。[F:effective-rate-dedup] と同一 surface のため1編集にまとめる。
- **[F:unused-public-rate]**: 自動適用可（単独系統cosmetic相当・照合済み）。[F:effective-rate-dedup] と一括。
- **[F:qa-capture-timeout]**: 自動適用可（1は挙動一意、2〜4は前例踏襲の規約整形）。
- **[F:initial-tab-stuck]**: 自動適用可（3系統が同一の修正形＝マウント境界の narrowing を推しており選択の余地が実質ない）。
- **[F:mutation-survives]**: 自動適用可。ただし修正方針④は [F:gear-torque-rate] の、⑤は [F:power-rate-visibility] の裁定確定後に着手する。

## ② Warning 全件（1件1行・出所系統つき）

integrated.md の Warning 節に列挙されている行は26行（ヘッダ件数の「30」はレンズ横断の重複計上を含むため一致しない）。以下は落とさず全件。

- `VanillaMachineProcessorComponent.cs:29`［rev-core-cs 系］: 三項が `Halted` を無言で `_idlePowerRate` に落とす（CleanRoom は `Halted => 0f`）。`_stateHandlers`(:81-85) に Halted ハンドラが無く `State:2` のセーブ復元で `KeyNotFoundException` になる一方、要求電力だけ idle 需要を返す。
- `VanillaMachineProcessorComponent.cs:171` / `CleanRoomMachineProcessorComponent.cs:151`［inv 系］: ラッチは状態遷移の前、同じ payload の `CurrentState.ToStr()` は遷移後。1スナップショットが2つの状態基準の混成になる。`CleanRoomMachineTest.cs:98-100` がこのズレを仕様として固定している。
- `VanillaElectricMachineComponent.cs:31` / `CleanRoomMachineComponent.cs:21`［lens-domain-boundary］: 電力網はライブの `EffectiveRequestPower` を読み state はラッチ済み値を出す。「要求電力」の真実源が2つに分かれ1tickずれ得る。
- `Blocks/Miner/VanillaMinerProcessorComponent.cs:258,30`［rev-core-cs-result-state-propagation］: 3人目の生産者である採掘機は `RequestEnergy` を読み出し時に再導出し未ラッチ。遷移tickで `PowerRate = 5.0`（500%）や 20% の偽赤が残る。
- `MinerSection.tsx:15`［rev-core-ts_tsx 系］: 採掘機フッタが今回の様式から取り残され `PowerRateText` 無条件表示のまま。風力掘削機（`requiredPower: 0` 実測）が「電力 100% (0/0)」をラベル無しで出す。
- `UI/Inventory/Block/MachineBlockInventoryView.cs:150-155`［rev-core-cs-callsite-tracer］: uGUI の分母が黙って変化し、Halted（要求0）で `PowerRate == 1.0` となり完全停止中を「エネルギー 100.00% 0.00/0.00」と表示（PR前は 0.00% の赤）。非推奨サーフェスのため Warning。
- `Game.Block.Interface/State/CommonMachineBlockStateDetail.cs:30,36`［lens-type-driven-structure］: `RequestPower` の意味が破壊的に変わったのにフィールド名も XML doc も未更新。同名・異義が3つ並ぶ（ワイヤ側 `EffectiveRequestPower` / サーバ側 `BaseRequestPower` への改名で解消）。
- `MachineProcessContext.cs:26`［lens-domain-boundary］: `ProcessingPowerMultiplier` が名前に反して状態非依存。「Processing のときの値」という文脈が呼び出し側の三項にしか無く Idle 経路での誤参照を誘う（`ModulePowerMultiplier` 等へ）。
- `MachineProcessContext.cs:25-27`［rev-core-any-efficiency］: 非キャッシュ getter で同一tick内に3〜4回集計。`SettleTick` と `Update()` で別インスタンスを見るため「基準一致」が tick 内で成立していない（→ [F:multiplier-snapshot]）。
- `bridge/contract/schemas/inventory.ts:23,30`［rev-core-ts_tsx-single-source-of-truth］: enum 化により4つ目の `ProcessState` 追加で `topicStore.ts:65-71` の safeParse が `block_inventory` を丸ごと破棄しパネルが stale で固まる（故障モードが全損）。C#との同値性契約テストが必要。
- `MachineSection.tsx:18,60-63,66,79` ＋ `machineRecipeSelectionLogic.ts:42`［rev-core-ts_tsx-type-driven-structure］: タブID union を `useState<string>` で広げリテラルが4箇所に散る。`ModeSwitch` のジェネリック化で `FilterSplitterInventory.tsx:38` / `TrainPlatformSection.tsx:33` の同型2件も同時に消える。
- `MachineSection.tsx:24,78` / `BlockInventoryPanel.tsx:38`［rev-core-ts_tsx-result-state-propagation］: `machineRecipes?.recipes ?? []` が「トピック未着」を「0件」へ畳む既存形。本PRがその帰結を拡大し、未着中はタブ列が描画されず `isLargeMachinePanel` も false になりパネル寸法が跳ねる。
- `shared/ui/index.ts:8-9`［rev-core-ts_tsx-centralization-duplication］: `ProgressArrow` と `ProgressArrowGlyph` の並立（→ [F:arrow-duplication]）。
- `shared/ui/ProgressArrowGlyph/style.module.css:4,5,32`［lens-precedent-alignment］: craft ドメイン語彙のトークン参照（→ [F:arrow-token-name]）。
- `shared/ui/ProgressArrowGlyph/style.module.css:20-22`［Fable］: 充填色の根拠コメントが「クラフト完了状態」のまま。共有部品になったのに複製元のクラフト限定説明が残る。
- `shared/ui/` 直下［決定論・lens-precedent-alignment］: エントリ22件で「1ディレクトリ10ファイルまで」を大幅超過（本PRで `ProgressArrowGlyph/` を追加しさらに悪化）。`shared/ui/gauge/`・`shared/ui/slot/` 等の役割別分割が前例どおりの形。
- `MachineInventoryBody.tsx:40`（＋ `MinerSection.tsx:14` / `FluidSlotRow/index.tsx:23`）［rev-core-ts_tsx-default-resolution-ownership］: 同じ optional な `progress` に「未指定→0／未指定→0／未指定→非表示」の3規則が散在。所有者が `clamp01(value ?? 0)` で1回だけ解決するのが最小修正。
- `detailLogic.ts:53,65`［rev-core-ts_tsx-centralization-duplication］: `MachineStateKeys` と `MachineStateInsufficientTone` が同じキー集合の並行テーブル2枚（[F:power-rate-visibility] 案A に併合）。
- `detailLogic.ts:61-70`［rev-core-ts_tsx-speculative-abstraction］: 真偽値テーブルは同ファイルの「union→翻訳キー」規約の外側の新形で、受益者は1式のみ・テストも無い。[F:power-rate-visibility] 案A で統合するか `insufficient={machine.currentState === "halted"}` へ落とす。
- `capture-machine-qa.ts:166` 付近［rev-core-ts_tsx-ai-recurring-mistakes］: 撮影ファイル名の配列が撮影側とマニフェスト側で二重管理。片方だけ追加すると `sha256: null` で静かに落ちる。
- `capture-machine-qa.ts`［同上］: `void main()` のため失敗時のサーバ後始末・非0終了が無い（[F:qa-capture-timeout] の故障と複合）。
- `docs/adr/0011` / `plans/2026-08-17-recipe-viewer-single-list.md`（780行の未実装plan）/ レシピビューア系 `.decisions/` 7件 / worktree運用・Library温め `.decisions/` 2件［lens-precedent-alignment］: 機械UI改修PRに設計文書のみが同梱。出所は `[agent前提]` のみで免責力なし。
- `.agents/skills/webui-design/SKILL.md` §8.13［post-check 系］: 残存行「構造はインラインSVGの3層（`CraftProgressArrow`）」が本PRで削除された旧コンポーネント名を指したまま。
- `MachineFluidIOTest.cs:518`［rev-core-any-test-mutation-effectiveness］: ライブ再導出値とラッチ値の突き合わせのため、idle→processing 遷移tickを観測すると ±1f では通らない。逆に一度も入らないなら死んだ検証。`CleanRoomMachineTest.cs:99-103` は「1tick余分に回す」で回避しており非対称。
- `CleanRoomMachineProcessorComponent.cs:177`［rev-core-cs-caller-orchestration-minimization］: `_context.CurrentPower = 0f` が「CurrentPower は Update 冒頭のラッチ以外で書き換えない」という単一書き込み点の不変条件を破る。現状実害なし（[F:effective-rate-dedup] の `PinPowerToZero()` で1箇所へ戻る）。
- 決定論 confirmed（file-too-long・努力目標のため Critical に数えず）: `CleanRoomMachineProcessorComponent.cs` 222行 / `VanillaMachineProcessorComponent.cs` 217行（上限200）。[F:effective-rate-dedup] の集約で両方とも上限内へ戻る。

## ③ Info 全件（圧縮列挙）

integrated.md の Info 節に列挙されている行は17行（ヘッダ件数の「20」は重複計上を含むため一致しない）。

- `MachineProcessStateSchema` の3値はサーバ `VanillaMachineBlockStateConst.cs:5-7` と完全一致。`ToCamelCase` はこの3値では恒等。採掘機は `dto.Machine` に流入しないため enum 化は現時点で安全。
- `key={data.identifier}` の値は `BlockInventoryTopic.cs:139` の `BlockPosition.ToString()` で publish 毎に不変。毎フレーム再マウント事故は起きない。
- 歯車機械の idle 挙動はビット同値（旧 `idleTorqueRate` の実引数と新経路の `_idlePowerRate` が同値）。
- `ProcessingPowerMultiplier` への一本化で加工速度・表示分母・歯車要求の「式」は同一になった（値の再計算タイミングは別＝[F:multiplier-snapshot]）。方向としては改善。
- `_publishedRequestPower` は new/load とも単一の実体ctor末尾で初期化され、load 経路の未初期化は無い。
- `CraftProgressArrow.{tsx,module.css,test.ts}` の不変条件（3層構造・clip矩形 x=2/width=117/height=78・`useId` のコロン除去・トークン参照）は `ProgressArrowGlyph/` で再確立済み。残存参照0件。
- `ProgressArrowGlyph/index.test.ts` は本PRで最も mutation 耐性が高く、他テストの水準の基準にできる。
- ローカライズ3キーは CSV・`localizationKeys.ts`・`VanillaLocalizationKeys`・`_CompileRequester.cs` の4点で揃っている。
- `.cs` 差分に `async`/`await`/`UniTask`/`CancellationToken` の追加0件。実時間APIの導入も0件で `[ADR: AGENTS.md#時間に関して]` と整合。
- `#region Internal` の誤用なし。`partial`・`Func<>`・デフォルト引数の新設は `.cs` 側に0件。
- `VanillaSchema/*.yml` の変更0件・`optional` 文字列0件のため master-data-defense の対象は構造的に発生しない。
- `machineRecipeSelectionLogic.ts:25` の代表1件への縮約は本patchが触っていない既存行。
- `MachineInventoryBody.tsx` 内の `output` シャドウは変更範囲外の既存コード。
- `isPowerRateMeaningful` のゲートにより `computePowerRate` の `0 → 1` 分岐は機械経路では到達不能（死に分岐化）。`MinerSection` からは今も到達する。
- post-check `comment-rationale-guard`: Critical 0件。削除コメント28行を全数追跡し根拠の逐語保存を確認。
- 決定論 `comment_length` 候補27件は `[ADR: AGENTS.md#コメント]` の目安であり、convention-guard の自己裁定領域。Critical/Warning に計上しない。
- 新規性ゲートL1: 新規エッジ1件・asmdef 参照追加0・新文法0。

## ④ 参考扱いの new_edges 1件

- `CleanRoomMachineTest.cs` → `Game.Block.Interface.State`（`generic_origin=false` / `dir_is_new: false`）。テストが `CommonMachineBlockStateDetail` をデシリアライズするための参照で、入国審査は不要。

## ⑤ 系統別の生所見要約（縮退・事故の申告を含む）

- **決定論チェック**: confirmed 2（file-too-long ×2＝努力目標）／候補27（全て comment_length）。
- **死にメンバーゲート（IL解析）**: **skipped＝縮退**。`moorestech_client/Library/ScriptAssemblies` 不在（レビューworktreeはUnity未コンパイル）。新設 public の死活は `rg` 実測で代替し [F:unused-public-rate] を検出。
- **ts 死コードゲート（knip）**: **skipped＝縮退**。knip 未インストール（webui で `pnpm install` が必要）。TS の未参照シンボルは参照表で部分的に代替。
- **moores設計レンズ ×7**: 7起動7回収。domain-boundary が [F:gear-torque-rate]、type-driven-structure が [F:effective-rate-dedup]・[F:power-rate-visibility] へ併合。default-resolution-ownership / precedent-alignment / redundant-member-duplication / master-data-defense / hardcoded-content-enumeration は Critical なし。
- **汎用reviewer ×25**: 25起動25回収・欠員なし。Critical あり10本 / なし15本。
- **investigator ×6**: 6起動6回収だが**二重起動事故により5本のレポートが2回目の実行で上書きされ、1回目の詳細（行番号・根拠）が失われている**。`ORCHESTRATOR-NOTE-superseded-first-run.md` の逐語控えを1系統として扱い全件を実コードで裏取りした結果、[F:gear-torque-rate]・[F:change-selection-latch]・[F:power-rate-visibility] へ併合、**4件を事実誤りとして棄却**（`machineStateType` 必須追加による parse 全損／CleanRoom Halted の非0 publish／倍率の分子分母二重適用／`?? 0` による電力行消失。いずれも該当行を引用して否定）。5件目は [F:power-rate-visibility] と同一指摘のため重複排除で併合。
- **Fable 全般レビュー**: Critical なし／Warning 2。`[検証済み]` 9項目を spot-check し、「歯車の rate>1 は裁定どおり」という判定のみ [F:gear-torque-rate] と真っ向から矛盾。実コード検証の結果 **Fable の Info 判定は採らない**（供給側 `GearConsumptionCalculator` の導出を読んでいない）。
- **post-checks**: `comment-rationale-guard` は起動・回収とも1で Critical なし。`comment-convention-guard` は `select_post_checks.py` が発火条件未達と判定しスキップ（縮退ではなく設計どおり）。
- **Codex 外部監査 ×3（俯瞰・バグ狩り・設計整合）**: **0起動・全欠員＝縮退**。`which codex` が失敗し CLI 不在のため3本ともスキップされ、`codex-audit.out.md` / `codex-bughunt.out.md` / `codex-design.out.md` は生成されていない。**別モデルによる独立第三者視点がこの run には一切入っていない。**
