# 機械UI修正（矢印統一・タブ入替/自動表示・電力充足率化） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 機械ブロックUI（Web UI）の進捗表示をクラフトと同一の矢印グリフに統一し、タブ順入替と未選択時のレシピタブ初期表示を行い、電力表示を「実効要求電力に対する充足率＋稼働状態ラベル」へ改める。

**Architecture:** 表示はすべて `moorestech_web/webui`（React）。電力のみサーバー変更あり: state生成2箇所で基礎要求値→`EffectiveRequestPower` への差し替え（ワイヤ構造・DTO・クライアントhostは無変更。充足率は既存どおりWeb側で `current/request` 再計算）。稼働状態ラベルは既にワイヤ・DTOに載っている `machine.currentState`（idle/processing/halted）から導出する。

**Tech Stack:** Unity C#（サーバー）/ React + TypeScript + CSS Modules + Mantine / vitest / Playwright（mock-host）

## Requirements

設計セッション（2026-08-17）とADR 0010で確定。各行が受け入れ基準:

- **R1 進捗矢印統一**: 機械UIの加工行（入力→出力間）の進捗が、クラフト画面と同一の矢印グリフゲージ（SVG3層: 溝`--gauge-track`・白充填`--color-content-primary`・輪郭`--craft-arrow-outline`）で表示される。共有部品化し、クラフト側も同一部品を使う。見た目・寸法はクラフト矢印と同一。
- **R2 タブ位置入替**: 機械UIのタブ順が「レシピ選択 / インベントリ」（レシピ選択が先頭）になる。
- **R3 未選択時レシピタブ自動表示**: UIを開いた瞬間 `selectedRecipeGuid` が空GUID/null なら初期タブがレシピ選択、選択済みならインベントリ。開いた後の手動切替は強制しない。レシピ選択直後のインベントリタブ自動復帰・レシピ0件時のタブ非表示は現状維持。
- **R4 電力充足率化**: サーバーが state の `RequestPower` に実効要求電力（Idle=基礎×idlePowerRate、Processing=基礎×モジュール倍率、Halted=0）を詰める。結果、表示の%は「100%未満＝常に電力不足」となり、待機中に20%と表示されない。フッタに稼働状態ラベル（待機中/稼働中/停止中）を併記する。
- **R5 様式先行**: webui-design SKILL.md（ホワイトリスト）を実装前に更新する。実装後に mock-host スクリーンショットで目視QA（§10）を行う。

**やらないこと（スコープ境界）**:
- 採掘機（MinerSection）と流体行（FluidSlotRow）の帯状 `ProgressArrow` は変更しない（`ProgressArrow` 部品は削除せず残す）
- uGUI側（`MachineBlockInventoryView` 等、Phase1描画停止済み）は一切触らない
- `ElectricNetworkInfo`（ネットワーク集約の供給率表示）・発電機・ポンプのUIは変更しない
- サーバーの採掘機は既に実効値送信（`RequestEnergy` が idlePowerRate 適用済み）のため変更しない
- Halted（要求0）の%は既存 `computePowerRate` の「request==0 → 100%」のまま（状態ラベルが文脈を与える）

## Global Constraints

- 作業ブランチ: `feature/machine-ui-refresh`（origin/master 起点。設計成果物 ADR 0010・`.decisions/` 2件・CONTEXT.md 電力用語を含む）
- C#変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（AGENTS.md 絶対規則）
- Web UI はホワイトリスト方式（`.claude/rules/webui-design.md`）。実装前に webui-design SKILL を読み、Task 1 の様式更新を先に済ませる
- JSXに生リテラル禁止（lint `no-jsx-visible-literal`）。表示文字列は `t()` + `Localization/localization.csv`（列: `key,Source,english,japanese`）+ `npm run gen:i18n`
- コメントは日本語→英語の2行セット（AGENTS.md）。`Func<>`禁止・partial禁止・1ファイル200行以下
- webuiコマンドはすべて `moorestech_web/webui/` で実行: `npm run test`（vitest）/ `npm run lint` / `npm run test:e2e`
- コミットは各タスク末で必ず行う（worktree運用・作業消失防止）

## File Structure

| ファイル | 操作 | 責務 |
|---|---|---|
| `.claude/skills/webui-design/SKILL.md` | Modify | 様式更新（§8/§8.6/§8.7/§8.13） |
| `moorestech_server/.../Machine/VanillaMachineProcessorComponent.cs` | Modify | state要求値を実効値へ |
| `moorestech_server/.../CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs` | Modify | 同上 |
| `moorestech_server/.../Tests/CombinedTest/Core/MachineFluidIOTest.cs` | Modify | idle時の期待値を実効値へ |
| `moorestech_web/webui/src/shared/ui/ProgressArrowGlyph/index.tsx` | Create | 矢印グリフゲージ共有部品 |
| `moorestech_web/webui/src/shared/ui/ProgressArrowGlyph/style.module.css` | Create | グリフの3層スタイル |
| `moorestech_web/webui/src/shared/ui/ProgressArrowGlyph/index.test.ts` | Create | 旧CraftProgressArrow.test.tsの移設 |
| `moorestech_web/webui/src/shared/ui/index.ts` | Modify | export追加 |
| `moorestech_web/webui/src/features/recipe/views/CraftProgressArrow.tsx` | Delete | 共有部品へ吸収 |
| `moorestech_web/webui/src/features/recipe/views/CraftProgressArrow.module.css` | Rename→`craftArrow.module.css` | クラフト側の配置寸法のみ残す |
| `moorestech_web/webui/src/features/recipe/views/CraftProgressArrow.test.ts` | Delete | 共有側へ移設 |
| `moorestech_web/webui/src/features/recipe/views/CraftRecipeView.tsx` | Modify | 共有部品の利用へ |
| `moorestech_web/webui/src/app/tokens.css` | Modify | `--machine-arrow-*` トークン追加 |
| `moorestech_web/webui/src/features/blockInventory/details/machine/MachineInventoryBody.tsx` | Modify | 加工行を矢印グリフへ |
| `moorestech_web/webui/src/features/blockInventory/details/machine/machineInventoryBody.module.css` | Modify | 機械矢印の寸法ラッパー |
| `moorestech_web/webui/src/features/blockInventory/details/machine/machineRecipeSelectionLogic.ts` | Modify | `machineInitialTab` 追加 |
| `moorestech_web/webui/src/features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts` | Create/Modify | 初期タブのユニットテスト（既存testファイルがあれば追記） |
| `moorestech_web/webui/src/features/blockInventory/details/MachineSection.tsx` | Modify | タブ順・初期タブ・状態ラベル |
| `moorestech_web/webui/src/features/blockInventory/details/detailLogic.ts` | Modify | `machineStateTranslationKey` 追加 |
| `moorestech_web/webui/src/features/blockInventory/details/detailLogic.test.ts` | Modify | 同マッピングのテスト |
| `Localization/localization.csv` | Modify | 稼働状態ラベル3键 |
| `moorestech_web/webui/e2e/tests/block/machineRecipe.spec.ts` | Modify | タブ順・初期タブ・状態ラベルのe2e |

## Task 1: 様式更新（webui-design SKILL.md）＋設計成果物コミット

**Files:**
- Modify: `.claude/skills/webui-design/SKILL.md`
- （コミットのみ）: `docs/adr/0010-machine-power-display-as-satisfaction-rate.md`, `.decisions/2026-08-17-*.md`（2件）, `CONTEXT.md`, `docs/superpowers/plans/2026-08-17-machine-ui-refresh.md`

**Interfaces:** Produces: 後続タスクが従う様式（§8.7タブ順/初期タブ/フッタ、§8.13共有グリフ）

- [x] **Step 1: SKILL.md を編集する。** 以下の4箇所（文言は既存トーンに合わせて微調整可、意味は変えない）:
  1. §8 の「進捗矢印は `ProgressArrow`（機械・採掘機・流体行の帯状ゲージ）。クラフト画面の矢印グリフ進捗だけは §8.13」→「進捗矢印は `ProgressArrow`（採掘機・流体行の帯状ゲージ）。クラフト画面と機械の加工行は §8.13 の矢印グリフゲージを使う。」
  2. §8.6 GaugeBar の充填逸脱の記述「唯一の前例: クラフト進捗矢印の `--color-content-primary`・§8.13」→「唯一の前例: 矢印グリフゲージの `--color-content-primary`・§8.13」
  3. §8.7 を更新: タブ順は「レシピ選択 / インベントリ」（レシピ選択が先頭）。「デフォルトはインベントリタブ」→「初期タブはレシピ未選択ならレシピ選択、選択済みならインベントリ。開いた後の手動切替は強制しない」。共通フッタの記述に「稼働状態ラベル（待機中/稼働中/停止中。Halted のみ `--text-insufficient`、他は電力率テキストと同トーン）を電力率の隣へ併記する。電力率の%は実効要求電力に対する充足率であり、100%未満は常に電力不足を意味する（ADR 0010）」を追記
  4. §8.13 のスコープ更新: 見出しはそのまま、冒頭に「矢印グリフゲージは共有部品 `ProgressArrowGlyph`（shared/ui）であり、クラフト画面の素材→結果矢印と機械の加工行（入力→出力間）が使う。機械側の寸法はクラフトと同寸（`--machine-arrow-*` は `--craft-arrow-*` を参照）」を追記。「この逸脱はクラフト矢印限りで、他のゲージへ白充填を広げない」→「この逸脱は矢印グリフゲージ限りで、帯状ゲージへ白充填を広げない」
- [x] **Step 2: コミットする**

```bash
git add .claude/skills/webui-design/SKILL.md docs/adr/0010-machine-power-display-as-satisfaction-rate.md .decisions CONTEXT.md docs/superpowers/plans/2026-08-17-machine-ui-refresh.md
git commit -m "docs: 機械UI修正の設計成果物とwebui様式更新（ADR 0010）"
```

## Task 2: サーバー — stateのRequestPowerを実効要求電力へ（R4前半）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs:86`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs:75`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineFluidIOTest.cs:317`

**Interfaces:**
- Consumes: 既存 `EffectiveRequestPower`（両コンポーネントに定義済み: `VanillaMachineProcessorComponent.cs:29-30`, `CleanRoomMachineProcessorComponent.cs:31-37`）
- Produces: `CommonMachineBlockStateDetail.RequestPower` の意味＝実効要求電力（電気機械・歯車機械・クリーンルーム機械。歯車機械は `VanillaGearMachineTemplate.cs:57` が同コンポーネントを使うため自動で追従）。Web/クライアントの型・フィールド名は不変

- [x] **Step 1: 既存テストの現状を確認する**

```bash
grep -n "ValidateMachineBlockStateDetails(initialDetails" moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineFluidIOTest.cs
```
Expected: `:317` の1件（idle時に `requiredPower` を期待している行）。

- [x] **Step 2: テストを実効値期待へ先に更新する（TDD: この時点で失敗する状態を作る）**

`MachineFluidIOTest.cs:317` 付近で、テストが使う機械ブロックのマスタparamから `IdlePowerRate` を取り、idle時の期待要求値を実効値にする。`requiredPower` 変数の定義元（同テスト内、マスタparam由来）を確認し:

```csharp
// idle中の要求電力は実効値（基礎×idlePowerRate）になる（ADR 0010）
// While idle the requested power is the effective value (base × idlePowerRate) per ADR 0010
ValidateMachineBlockStateDetails(initialDetails, "idle", 0f, requiredPower * machineParam.IdlePowerRate, 0f);
```

`machineParam` が既にスコープに無い場合は、テスト冒頭のブロック生成箇所から `var machineParam = (ElectricMachineBlockParam)blockMasterElement.BlockParam;` 相当で取得する（実際の型名は同ファイルの既存取得コードに合わせる）。

- [x] **Step 3: 実装 — state生成2箇所を差し替える**

`VanillaMachineProcessorComponent.cs:86`:
```csharp
// 充足率表示のためstateには基礎値でなく実効要求電力を載せる（ADR 0010）
// Publish the effective request power (not the base) so the client rate reads as satisfaction (ADR 0010)
var commonMachineBlock = CommonMachineBlockStateDetail.CreateState(_context.CurrentPower, EffectiveRequestPower, processingRate, CurrentState.ToStr(), _lastState.ToStr());
```

`CleanRoomMachineProcessorComponent.cs:75` も同一の差し替え（`_context.RequestPower` → `EffectiveRequestPower`）。

- [x] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（「Unity is reloading」エラー時は45秒待ってリトライ）

- [x] **Step 5: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineFluidIOTest|IdlePowerRateTest|ChangeBlockEventPacketTest|FuelGearGeneratorTest|MachineModuleSlotTest"`
Expected: 全PASS。失敗した場合、失敗テストが「基礎要求値がstateに載る」前提なら期待値を実効値へ更新する（挙動側は直さない）。

- [x] **Step 6: コミットする**

```bash
git add moorestech_server
git commit -m "feat: 機械stateのRequestPowerを実効要求電力にする（ADR 0010）"
```

## Task 3: 矢印グリフの共有部品化とクラフト側差し替え（R1前半）

**Files:**
- Create: `moorestech_web/webui/src/shared/ui/ProgressArrowGlyph/index.tsx`
- Create: `moorestech_web/webui/src/shared/ui/ProgressArrowGlyph/style.module.css`
- Create: `moorestech_web/webui/src/shared/ui/ProgressArrowGlyph/index.test.ts`
- Modify: `moorestech_web/webui/src/shared/ui/index.ts`
- Modify: `moorestech_web/webui/src/features/recipe/views/CraftRecipeView.tsx:11,79`
- Create: `moorestech_web/webui/src/features/recipe/views/craftArrow.module.css`
- Delete: `moorestech_web/webui/src/features/recipe/views/CraftProgressArrow.tsx`, `CraftProgressArrow.module.css`, `CraftProgressArrow.test.ts`

**Interfaces:**
- Produces: `ProgressArrowGlyph({ value: number, testId: string })` — 親要素の width/height を埋めるSVG矢印ゲージ。`@/shared/ui` からexport。Task 4 が `testId="machine-progress-arrow"` で使う

- [x] **Step 1: 共有部品を作る。** 旧 `CraftProgressArrow.tsx` の中身を移設し、(a) `testId` propを追加、(b) クラス名から craft 語彙を除去、(c) ドメイン注釈をwebui-design §8.13参照へ:

`src/shared/ui/ProgressArrowGlyph/index.tsx`（旧ファイルとの差分のみ示す。ロジック・定数・clipId一意化はそのまま移す）:
```tsx
import styles from "./style.module.css";
// （ARROW_PATH等の定数は旧CraftProgressArrow.tsxからそのまま移設）

// 矢印グリフ自体が進捗ゲージ（webui-design §8.13）。配置側が親要素で寸法を決める
// The arrow glyph itself is the progress gauge (webui-design §8.13); the caller sizes it via the parent element
export default function ProgressArrowGlyph({ value, testId }: { value: number; testId: string }) {
  // …clamp01・useId・clipIdは旧実装のまま（clipId接頭辞は "progress-arrow-fill-" へ変更）…
  return (
    <div className={styles.arrow} data-testid={testId} role="progressbar" aria-valuemin={0} aria-valuemax={1} aria-valuenow={filled}>
      <svg className={styles.glyph} viewBox={`0 0 ${VIEWBOX_WIDTH} ${VIEWBOX_HEIGHT}`} aria-hidden="true">
        {/* defs/clipPath/3層pathは旧実装のまま。クラス名だけ track/fill/outline へ */}
        <path className={styles.track} d={ARROW_PATH} />
        <path className={styles.fill} d={ARROW_PATH} clipPath={`url(#${clipId})`} />
        <path className={styles.outline} d={ARROW_PATH} />
      </svg>
    </div>
  );
}
```

`style.module.css`（旧 `CraftProgressArrow.module.css` から `.craftArrow` の寸法指定を除いて移設。コメントの§参照はwebui-design §8.13へ）:
```css
/* 親要素の寸法を埋める。配置側（クラフト・機械）がラッパーで幅・高さを決める */
/* Fills the parent element; each placement (craft/machine) sizes it via a wrapper */
.arrow { width: 100%; height: 100%; }
.glyph {
  display: block;
  width: 100%;
  height: 100%;
  overflow: visible;
  filter: drop-shadow(0 1px 1px rgb(0 0 0 / 45%));
}
.track { fill: var(--gauge-track); }
.fill { fill: var(--color-content-primary); }
.outline { fill: none; stroke: var(--craft-arrow-outline); stroke-width: 2px; }
```
（`.track`/`.fill`/`.outline` の既存の日英2行コメント（溝トーン・白充填の裁定根拠・輪郭の役割）も旧cssから移設する）

- [x] **Step 2: テストを移設する。** 旧 `CraftProgressArrow.test.ts` を `src/shared/ui/ProgressArrowGlyph/index.test.ts` へ移し、import先とdescribe名を `ProgressArrowGlyph` に、`createElement(ProgressArrowGlyph, { value, testId: "arrow" })` にし、クラス名アサーションを `craftArrowTrack`→`track` / `craftArrowFill`→`fill` / `craftArrowOutline`→`outline` へ更新。旧3ファイル（tsx/css/test）を削除。

- [x] **Step 3: export追加。** `src/shared/ui/index.ts` に `export { default as ProgressArrowGlyph } from "./ProgressArrowGlyph";` を既存exportの並び順（アルファベット順なら従う）で追加。

- [x] **Step 4: クラフト側を差し替える。** `craftArrow.module.css` を新規作成（配置寸法のみ。旧cssの `.craftArrow` の解説コメントを移設）:
```css
.craftArrow {
  width: var(--craft-arrow-width);
  height: var(--craft-arrow-height);
  margin-top: var(--craft-arrow-offset-y);
}
```
`CraftRecipeView.tsx`: import を `import { ProgressArrowGlyph } from "@/shared/ui";` と `import craftArrowStyles from "./craftArrow.module.css";` にし、79行目を:
```tsx
<div className={craftArrowStyles.craftArrow}>
  <ProgressArrowGlyph value={isHolding ? progress : 0} testId="craft-progress-arrow" />
</div>
```
（注意: 旧実装は高さを `.craftArrowGlyph`（svg側）に持っていた。ラッパーに移しても `svg height:100%` で同寸になることをStep 6の目視で確認する）

- [x] **Step 5: vitest・lintを実行する**

Run: `cd moorestech_web/webui && npm run test && npm run lint`
Expected: 全PASS（特に `ProgressArrowGlyph/index.test.ts` と既存 `craftLogic` 系）

- [x] **Step 6: クラフト画面の見た目確認（e2e既存分）**

Run: `cd moorestech_web/webui && npm run test:e2e`
Expected: 全PASS（クラフト矢印のtestid `craft-progress-arrow` は不変のため既存specが通る）

- [x] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src
git commit -m "refactor: クラフト進捗矢印をProgressArrowGlyphとしてshared/uiへ昇格する"
```

## Task 4: 機械加工行を矢印グリフへ差し替え（R1後半）

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css:188` 付近
- Modify: `moorestech_web/webui/src/features/blockInventory/details/machine/MachineInventoryBody.tsx:3,40`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/machine/machineInventoryBody.module.css`

**Interfaces:**
- Consumes: `ProgressArrowGlyph`（Task 3）
- Produces: testid `machine-progress-arrow`（e2e用）。機械セクションから `progress-arrow`（帯バー）は消える

- [x] **Step 1: トークン追加。** `tokens.css` の `--craft-arrow-*` 定義（:188-192）の直後に:
```css
/* 機械加工行の矢印はクラフト矢印と同寸（同一見た目のユーザー裁定・ADR 0010セッション） */
/* The machine process-row arrow matches the craft arrow size (user ruling: identical look) */
--machine-arrow-width: var(--craft-arrow-width);
--machine-arrow-height: var(--craft-arrow-height);
```

- [x] **Step 2: ラッパークラス追加。** `machineInventoryBody.module.css` に:
```css
/* 加工行中央の矢印グリフの寸法箱。offsetはクラフト固有のため持たない */
/* Size box for the process-row arrow glyph; the craft-only vertical offset is not carried over */
.machineArrow {
  width: var(--machine-arrow-width);
  height: var(--machine-arrow-height);
}
```

- [x] **Step 3: 差し替え。** `MachineInventoryBody.tsx`:
  - import: `ProgressArrow` を `ProgressArrowGlyph` に変更（`FluidSlotRow` 等は維持）
  - 40行目: `<ProgressArrow value={data.progress ?? 0} />` →
```tsx
<div className={styles.machineArrow}>
  <ProgressArrowGlyph value={data.progress ?? 0} testId="machine-progress-arrow" />
</div>
```

- [x] **Step 4: e2eへ表示アサーションを足す。** `e2e/tests/block/machineRecipe.spec.ts` の最初のテスト（インベントリタブ表示確認部、:20付近）に:
```ts
await expect(page.getByTestId("machine-progress-arrow")).toBeVisible();
```

- [x] **Step 5: テストを実行する**

Run: `cd moorestech_web/webui && npm run test && npm run lint && npm run test:e2e`
Expected: 全PASS。`MinerSection`・流体行の `progress-arrow` はそのまま（`fluidSlot.spec.ts` がPASSすること）

- [x] **Step 6: コミットする**

```bash
git add moorestech_web/webui
git commit -m "feat: 機械の加工進捗をクラフトと同一の矢印グリフにする"
```

## Task 5: タブ順入替と未選択時レシピタブ初期表示（R2・R3）

**Files:**
- Modify: `moorestech_web/webui/src/features/blockInventory/details/machine/machineRecipeSelectionLogic.ts`
- Test: `moorestech_web/webui/src/features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts`（無ければ新規作成。既存なら追記）
- Modify: `moorestech_web/webui/src/features/blockInventory/details/MachineSection.tsx:16,52-55`
- Modify: `moorestech_web/webui/e2e/tests/block/machineRecipe.spec.ts`

**Interfaces:**
- Produces: `machineInitialTab(selectedRecipeGuid: string | null | undefined): "inventory" | "recipes"`

- [x] **Step 1: 失敗するユニットテストを書く**（`machineRecipeSelectionLogic.test.ts`。既存の同名テストファイルが無い場合は近隣テストのimport様式に合わせて新規作成）:
```ts
import { describe, expect, it } from "vitest";
import { machineInitialTab } from "./machineRecipeSelectionLogic";

describe("machineInitialTab", () => {
  it.each([
    { guid: null, tab: "recipes" },
    { guid: undefined, tab: "recipes" },
    { guid: "00000000-0000-0000-0000-000000000000", tab: "recipes" },
    { guid: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb", tab: "inventory" },
  ])("selectedRecipeGuid=$guid → $tab", ({ guid, tab }) => {
    expect(machineInitialTab(guid)).toBe(tab);
  });
});
```

- [x] **Step 2: 実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts`
Expected: FAIL（machineInitialTab is not exported）

- [x] **Step 3: 実装する。** `machineRecipeSelectionLogic.ts` 末尾に:
```ts
// 未選択で開いた機械はレシピ選択タブから始める（ADR 0010セッションのユーザー裁定）
// A machine opened with no recipe selected starts on the recipe tab (user ruling in the ADR 0010 session)
export function machineInitialTab(selectedRecipeGuid: string | null | undefined): "inventory" | "recipes" {
  return isEmptyGuid(selectedRecipeGuid) ? "recipes" : "inventory";
}
```

- [x] **Step 4: テストを実行して通ることを確認する**（Step 2と同コマンド）Expected: PASS

- [x] **Step 5: MachineSectionへ配線する。**
  - `:16` を `const [tab, setTab] = useState<string>(() => machineInitialTab(data.machine?.selectedRecipeGuid));` に変更し、importへ `machineInitialTab` を追加（初期値はマウント時のみ評価。開いた後の手動切替を強制しない要件をこれで満たす）
  - `:52-55` の options 配列を「レシピ選択が先頭」に入替:
```tsx
options={[
  { value: "recipes", label: t(L.ui.blockInventory.recipeSelectionTab), testId: "machine-tab-recipes" },
  { value: "inventory", label: t(L.ui.blockInventory.inventoryTab), testId: "machine-tab-inventory" },
]}
```

- [x] **Step 6: e2eを更新・追加する。** `machineRecipe.spec.ts`:
  - 最初のテスト（fixture `machine` は選択済みGUID）はそのまま「デフォルト＝インベントリタブ」のアサーションが通ることを確認。コメントを「選択済み機械のデフォルトはインベントリタブ」に更新
  - タブ順のアサーションを最初のテストへ追加:
```ts
// タブ順はレシピ選択が先頭（ADR 0010セッションの裁定）
// The recipe tab comes first (ruling from the ADR 0010 session)
const tabButtons = page.getByTestId("machine-tab-switch").locator("button");
await expect(tabButtons.first()).toHaveAttribute("data-testid", "machine-tab-recipes");
```
  - 新規テストを追加（fixture `gearMachine` は `selectedRecipeGuid` が空GUID・レシピ行が存在することを前提。行が無ければ `blockDetailFixtures.ts` / mock-hostのmachineRecipesフィクスチャを確認し、gearMachine用レシピが無い場合は `blockMachine` を複製した未選択fixture `machineUnselected` を `blockDetailFixtures.ts` と `httpHandler.ts:24` 付近のマップへ追加して使う）:
```ts
test("レシピ未選択の機械はレシピ選択タブで開く", async ({ page }) => {
  await setBlock(page, "gearMachine");
  await page.goto("/");
  await expect(page.getByTestId("machine-tab-recipes")).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();
});
```

- [x] **Step 7: テストを実行する**

Run: `cd moorestech_web/webui && npm run test && npm run lint && npm run test:e2e`
Expected: 全PASS（既存の「選択後にインベントリタブへ自動復帰」「レシピ0件はタブ非表示」specが無修正で通ること＝現状維持要件の担保）

- [x] **Step 8: コミットする**

```bash
git add moorestech_web/webui
git commit -m "feat: 機械タブをレシピ選択先頭にし未選択時はレシピタブで開く"
```

## Task 6: 電力フッタの稼働状態ラベル（R4後半）

**Files:**
- Modify: `Localization/localization.csv`（`ui.blockInventory.*` の既存ブロック末尾）
- Modify: `moorestech_web/webui/src/features/blockInventory/details/detailLogic.ts`
- Test: `moorestech_web/webui/src/features/blockInventory/details/detailLogic.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/MachineSection.tsx:26-32`
- Modify: `moorestech_web/webui/e2e/tests/block/machineRecipe.spec.ts`

**Interfaces:**
- Consumes: `machine.currentState`（既存DTO。"idle" | "processing" | "halted"）、`LackHighlightText`（既存。props: `insufficient/size/testId/children`）
- Produces: `machineStateTranslationKey(currentState: string): TranslationKey | null`、testid `machine-state-label`

- [x] **Step 1: ローカライズキーを追加する。** `Localization/localization.csv` の `ui.blockInventory.powerRateSummary`（:35）付近に3行追加:
```csv
ui.blockInventory.machineStateIdle,Standby,Standby,待機中
ui.blockInventory.machineStateProcessing,Operating,Operating,稼働中
ui.blockInventory.machineStateHalted,Halted,Halted,停止中
```
Run: `cd moorestech_web/webui && npm run gen:i18n`
Expected: `L.ui.blockInventory.machineStateIdle` 等の型が生成される

- [x] **Step 2: 失敗するテストを書く。** `detailLogic.test.ts` に追記:
```ts
describe("machineStateTranslationKey", () => {
  it.each([
    { state: "idle", key: L.ui.blockInventory.machineStateIdle },
    { state: "processing", key: L.ui.blockInventory.machineStateProcessing },
    { state: "halted", key: L.ui.blockInventory.machineStateHalted },
  ])("$state を対応キーへ写像する", ({ state, key }) => {
    expect(machineStateTranslationKey(state)).toBe(key);
  });

  it("未知の状態はnull（ラベル非表示）", () => {
    expect(machineStateTranslationKey("unknown")).toBeNull();
  });
});
```
（既存テストのimport様式に合わせ `machineStateTranslationKey`・`L` を追加import）

Run: `cd moorestech_web/webui && npx vitest run src/features/blockInventory/details/detailLogic.test.ts`
Expected: FAIL

- [x] **Step 3: 実装する。** `detailLogic.ts` に追加（既存 `GearStopReasonKeys` の様式に合わせる）:
```ts
// 機械の稼働状態→表示ラベル。%が待機を意味しなくなった分、状態はラベルで示す（ADR 0010）
// Machine state → label key; the rate no longer encodes standby, so the state gets its own label (ADR 0010)
export function machineStateTranslationKey(currentState: string): TranslationKey | null {
  return MachineStateKeys[currentState] ?? null;
}

const MachineStateKeys: Record<string, TranslationKey> = {
  idle: L.ui.blockInventory.machineStateIdle,
  processing: L.ui.blockInventory.machineStateProcessing,
  halted: L.ui.blockInventory.machineStateHalted,
};
```

- [x] **Step 4: テストを実行して通ることを確認する**（Step 2と同コマンド）Expected: PASS

- [x] **Step 5: フッタへ配線する。** `MachineSection.tsx` の `powerRate` 定義（:26-32）を:
```tsx
// 稼働状態ラベル＋充足率をタブ外の共通フッタに常時表示する（ADR 0010）
// The state label and satisfaction rate stay visible on both tabs as the shared footer (ADR 0010)
const stateKey = machineStateTranslationKey(machine.currentState);
const powerRate = (
  <Group justify="center" gap="xs">
    {stateKey && (
      <LackHighlightText insufficient={machine.currentState === "halted"} size="sm" testId="machine-state-label">
        {t(stateKey)}
      </LackHighlightText>
    )}
    <PowerRateText currentPower={machine.currentPower} requestPower={machine.requestPower} testId="machine-power-rate" />
  </Group>
);
```
importへ `machineStateTranslationKey`（`./detailLogic`）と `LackHighlightText`（`./LackHighlightText`）を追加。

- [x] **Step 6: e2eへ足す。** `machineRecipe.spec.ts` の電力率アサーション（:27付近）の隣に:
```ts
// 稼働状態ラベルが電力率の隣に出る（fixtureはprocessing）
// The machine state label sits next to the power rate (fixture is processing)
await expect(page.getByTestId("machine-state-label")).toBeVisible();
```

- [x] **Step 7: テストを実行する**

Run: `cd moorestech_web/webui && npm run test && npm run lint && npm run test:e2e`
Expected: 全PASS

- [x] **Step 8: コミットする**

```bash
git add Localization moorestech_web/webui
git commit -m "feat: 機械フッタに稼働状態ラベルを併記する（ADR 0010）"
```

## Task 7: 目視QA（webui-design §10・必須）

**Files:**
- 参照: `moorestech_web/webui/e2e/capture-eval.ts`（撮影様式の正本）

- [x] **Step 1: mock-hostで機械パネルを撮影する。** `capture-eval.ts` の様式でmock-hostを起動し、`/__block` で `machine`（選択済み・インベントリタブ）と `gearMachine`（未選択・レシピタブ初期表示）を再現して撮影。クラフト画面（インベントリの長押しクラフト中でなくてよい。value=0の矢印形状確認）も1枚撮る。
- [x] **Step 2: チェックする（§10の4項目）**
  1. 機械の矢印グリフがクラフトと同寸・同見た目か（拡大クロップで比較）
  2. 加工行の矢印がパネル中心線上にあるか（入出力スロット数が非対称のfixtureで確認）
  3. フッタの「状態ラベル＋電力率」が中央揃えで読めるか・フェード帯に載っていないか
  4. タブ順が「レシピ選択 / インベントリ」で表示されるか
  問題があれば該当タスクのCSSを修正して再撮影（矢印の縦位置ズレは `.machineArrow` に margin 調整を足すのではなく、加工行の `1fr auto 1fr` グリッドの揃えを確認する）。
- [x] **Step 3: スクリーンショット確認結果を1行でコミットメッセージに含めてコミットする**（修正が無ければ空コミット不要、bd noteへ記録のみ）

## Task 8: 全ブランチレビュー（必須・省略不可）

- [x] **Step 1: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。** moores-code-review スキルを起動し、`feature/machine-ui-refresh` の全差分（origin/master比）をレビューする。指摘は本plan・ADR 0010・`.decisions/` と突合し、裁定済み事項への再指摘は却下理由を記録、未裁定の実指摘は修正してコミットする。
- [x] **Step 2: 最終確認**: `uloop compile` と `npm run test && npm run lint && npm run test:e2e` が全て緑であることを確認し、未コミット差分ゼロで終了する。
  - 結果（2026-08-18再検証）: uloop compile 0エラー / C# 31/31 / vitest 602/602 / lint clean / e2e 144 passed・10 failed。
  - e2e失敗10件は本branch由来ではない既存赤。mock-hostの既定locale が japanese（`topicFixtures.ts:36`）なのに英語literalを期待するspec群（hotbar/modeHud/recipe/connection/skit/commonHud/train）で、失敗specは全てorigin/masterとbyte一致・単独実行でも同様に失敗する。機械・クラフト矢印のspecは緑。→ moorestech-2lh.1 へ分離。

## 判断記録（ADR）

設計セッションのADR: [docs/adr/0010-machine-power-display-as-satisfaction-rate.md](../../adr/0010-machine-power-display-as-satisfaction-rate.md)

- 電力%は実効要求に対する充足率＋状態ラベル併記 — 出所: ユーザー裁定 2026-08-17（[.decisions/2026-08-17-電力表示は充足率と稼働状態ラベルに分離する.md]）
- stateのRequestPowerを実効値へ（案A、クライアント再計算維持） — 出所: ユーザー裁定 2026-08-17（[.decisions/2026-08-17-stateのRequestPowerは実効要求電力を送る.md]）
- 矢印はCraftProgressArrowの完全共有（同見た目・同寸） — 出所: ユーザー裁定 2026-08-17（Q2への ok）
- タブ順入替・未選択時レシピタブ初期表示・手動切替尊重・選択後復帰維持 — 出所: ユーザー裁定 2026-08-17（Q3への ok）
- planning中の追加判断（すべて agent前提）:
  - 共有部品名は `ProgressArrowGlyph`（shared/uiにドメイン語彙を置かない規約。craft語彙をクラス名からも除去）
  - 帯状 `ProgressArrow` は採掘機・流体行で存続（削除しない）。設計対話時の「置き換えて削除」は機械加工行に限る読み替え（ProgressArrowはMinerSection/FluidSlotRowが使用中のため）
  - 機械矢印の寸法はクラフトと同値のトークン参照（`--machine-arrow-*: var(--craft-arrow-*)`）
  - 状態ラベルは `machine.currentState`（既存ワイヤ値）から導出、ホスト・DTO無変更。Haltedのみ `--text-insufficient`（LackHighlightText流用）、未知状態は非表示
  - 採掘機はサーバー側が既に実効値送信（`VanillaMinerProcessorComponent.cs:30`）のため無変更。Halted=要求0の%は既存の需要なし=100%挙動を維持
  - 初期タブはマウント時のみ評価（`useState` 初期化関数）で「手動切替を強制しない」を実現
  - 様式更新（webui-design SKILL.md）を実装より先に行う（ホワイトリスト運用規則）
- Task 8レビューで上記agent前提が覆った分（.decisions/が正）:
  - 矢印寸法の別名トークン `--machine-arrow-*` は廃止し部品CSSが `--craft-arrow-*` を1箇所で参照 — [.decisions/2026-08-17-矢印グリフの既定寸法は部品がトークン参照で持つ.md]
  - Halted（要求0）は%を出さず停止中ラベルのみ表示（100%維持を撤回） — [.decisions/2026-08-17-要求0の機械は充足率を出さず停止中のみ表示する.md]
  - 表示用要求電力はCurrentPowerと同位置でラッチ、歯車機械の要求トルクにもモジュール倍率を反映 — [.decisions/2026-08-17-表示用要求電力は供給と同位置でラッチする.md] / [.decisions/2026-08-17-歯車機械の需要にもモジュール倍率を載せる.md]

## 機能パリティ（死活表）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| タブ手動切替 | 生存 | ModeSwitch機構は不変・初期値のみ変更 |
| レシピ選択→インベントリ自動復帰 | 生存 | `onSelected` 配線は不変（Task 5 Step 7で既存specが担保） |
| レシピ0件機械の小型パネル | 生存 | `rows.length === 0` 分岐は不変 |
| 採掘機・流体行の帯状進捗 | 生存 | `ProgressArrow` 存続・未変更 |
| クラフト長押し矢印 | 生存 | 同一部品へ移設・testid不変・既存spec担保 |
| 電力%表示 | 意味変更（意図） | ADR 0010。待機中100%表示は仕様変更そのもの |
| ブロックの稼働アニメーション | 生存 | クライアントの `PowerRate` 消費者は退役uGUIのみ（検索済み） |
