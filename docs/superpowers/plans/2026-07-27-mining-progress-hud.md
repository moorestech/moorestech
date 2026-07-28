---
spec: docs/superpowers/specs/2026-07-27-mining-progress-hud-design.md
---

# 採掘進捗HUD統合 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** `Mining Target` と重複進捗を撤去し、採掘進捗をホットバー全幅・12px上のデザイン準拠ゲージ1本へ統合する。

**Architecture:** 既存の `MapObjectMiningMiningState → ProgressBarView → ProgressTopic` を唯一の値源として維持し、重複する `MiningHudTopic` とReact featureを削除する。Web表示は `ProgressBar → GaugeBar` にして、ホットバーと共有する固定長CSSトークンで相対寸法を決める。

**Tech Stack:** Unity/C#、UniRx、React/TypeScript、CSS Modules、Vitest、Playwright

## Global Constraints

- `Mining Target: ...` と対象名を表示しない。
- 採掘進捗は画面内に1本だけ表示する。
- ゲージの幅と中心を9スロットのホットバー全幅・中心へ一致させる。
- ゲージ下端とホットバー番号タブ上端の間を12px空ける。
- ゲージは既存 `GaugeBar` と `--gauge-track` / `--gauge-fill` / `--gauge-outline-width` を使う。
- HUDはstage内絶対配置、`pointer-events: none`、既存z-indexトークンのみを使う。
- `ui.progress` の `label?: string` 契約を維持し、採掘時はlabelキー省略で文字を描画しない。
- 新しい色、装飾、アニメーションを追加しない。
- C#変更後は `uloop compile --project-path ./moorestech_client` を実行する。

---

## File Structure

**削除する重複経路**

- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/MiningHudTopic.cs`
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/MiningHudTopic.cs.meta`
- `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/mining_hud.json`
- `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/mining_hud.json.meta`
- `moorestech_web/webui/src/features/miningHud/MiningHud.tsx`
- `moorestech_web/webui/src/features/miningHud/index.ts`
- `moorestech_web/webui/src/features/miningHud/style.module.css`

**既存経路へ統合するファイル**

- `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MapObjectMiningController.cs`
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractC2Test.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiGateClassification.cs`
- `moorestech_web/webui/src/app/App.tsx`
- `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`
- `moorestech_web/webui/src/bridge/contract/validators.ts`
- `moorestech_web/webui/src/bridge/contract/validators.test.ts`
- `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`
- `moorestech_web/webui/src/bridge/transport/protocol.ts`
- `moorestech_web/webui/src/features/progress/ProgressBar.tsx`
- `moorestech_web/webui/src/features/progress/style.module.css`
- `moorestech_web/webui/src/features/inventory/HotbarPanel/style.module.css`
- `moorestech_web/webui/src/app/index.css`

**Playwright状態再現と検証**

- `moorestech_web/webui/e2e/mock-host/fixtures.ts`
- `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`
- `moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts`
- `moorestech_web/webui/e2e/support/mockControl.ts`
- `moorestech_web/webui/e2e/tests/progress.spec.ts`
- `moorestech_web/webui/e2e/tests/system/commonHud.spec.ts`
- `moorestech_web/webui/e2e/tests/regression/connection.spec.ts`
- `moorestech_web/webui/e2e/capture-mining-progress.ts`

## データフローと機能死活

`MapObjectMiningMiningState`（書き手）→ `ProgressBarView`（共有状態）→ `ProgressTopic`（読み手）→ `ProgressBar`（読み手）→ `GaugeBar`

| 現行操作 | 計画後 |
|---|---|
| フォーカス・左クリック採掘・中断・完了 | 既存state machineのまま生存 |
| 進捗Show/Hide/SetProgress | `ProgressBarView`のまま生存 |
| ホットバーのキー・ホイール・クリック | `pointer-events: none` により生存 |
| `Mining Target` 対象名 | ユーザー裁定どおり撤去 |
| `ui.progress` の任意ラベル | 契約と描画を維持 |

### Task 1: Playwrightで現状のレイアウト違反をREDにする

**Files:**

- Modify: `moorestech_web/webui/e2e/mock-host/fixtures.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts`
- Modify: `moorestech_web/webui/e2e/support/mockControl.ts`
- Modify: `moorestech_web/webui/e2e/tests/progress.spec.ts`
- Modify: `moorestech_web/webui/e2e/tests/system/commonHud.spec.ts`
- Modify: `moorestech_web/webui/e2e/tests/regression/connection.spec.ts`

**Interfaces:**

- Consumes: `Topics.progress`, `ProgressData { visible, progress, label? }`
- Produces: mock scenarios `progressLabeled`, `mining`, `miningHidden`

- [ ] **Step 1: mockの既定進捗を非表示にし、用途別scenarioを定義する**

```ts
export const progressSample = {
  visible: false,
  progress: 0,
} satisfies ProgressData;

progressLabeled: () => control(Topics.progress, { visible: true, progress: 0.4, label: "Crafting" }),
mining: (params: URLSearchParams) => control(Topics.progress, {
  visible: true,
  progress: Number(params.get("progress") ?? "0.65"),
}),
miningHidden: () => control(Topics.progress, { visible: false, progress: 0 }),
```

- [ ] **Step 2: Playwrightへ採掘HUDの数値要件を書く**

`progress.spec.ts` の採掘ケースで次をassertする。

```ts
await setTopicScenario(page, "mining");
const gauge = page.locator('[data-tutorial-anchor="mining.hud"]');
const hotbar = page.getByTestId("hotbar-grid");
const firstNumberTab = hotbar.locator("> div").first().locator("span");
await expect(page.getByText(/Mining Target/i)).toHaveCount(0);
await expect(page.getByText("Iron Ore", { exact: true })).toHaveCount(0);
await expect(page.getByRole("progressbar")).toHaveCount(1);
await expect(gauge).toHaveCSS("pointer-events", "none");

const gaugeBox = await gauge.boundingBox();
const hotbarBox = await hotbar.boundingBox();
const numberTabBox = await firstNumberTab.boundingBox();
expect(gaugeBox).not.toBeNull();
expect(hotbarBox).not.toBeNull();
expect(numberTabBox).not.toBeNull();
expect(Math.abs(gaugeBox!.width - hotbarBox!.width)).toBeLessThanOrEqual(0.5);
expect(Math.abs((gaugeBox!.x + gaugeBox!.width / 2) - (hotbarBox!.x + hotbarBox!.width / 2))).toBeLessThanOrEqual(0.5);
expect(numberTabBox!.y - (gaugeBox!.y + gaugeBox!.height)).toBeCloseTo(12, 1);
```

computed styleではゲージのtrack/fillが `--gauge-track` / `--gauge-fill` の解決色と一致し、Mantineクラスと緑色を持たないことをassertする。

- [ ] **Step 3: 再接続テストをlabel文字列からprogress値へ移行する**

`mockControl.ts` のrevision helperは `progress: number` をqueryへ渡し、`connection.spec.ts` は `aria-valuenow` の `0.1 → 0.65` と旧revision `0.2` の拒否を検証する。snapshot delay対象は `"ui.progress"` に変更する。

- [ ] **Step 4: REDを確認する**

Run:

```bash
cd moorestech_web/webui
pnpm test:e2e -- progress.spec.ts system/commonHud.spec.ts regression/connection.spec.ts
```

Expected: geometryまたはstyle assertionがFAILし、現行の幅・重なり・Mantine緑ゲージを検出する。mockや型のエラーではなく、意図したUI差分で失敗することを確認する。

- [ ] **Step 5: テスト先行変更をコミットする**

```bash
git add moorestech_web/webui/e2e
git commit -m "採掘進捗HUDの回帰テストを追加する"
```

### Task 2: 重複するMiningHud契約と表示を削除する

**Files:**

- Delete: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/MiningHudTopic.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/MiningHudTopic.cs.meta`
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/mining_hud.json`
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/mining_hud.json.meta`
- Delete: `moorestech_web/webui/src/features/miningHud/MiningHud.tsx`
- Delete: `moorestech_web/webui/src/features/miningHud/index.ts`
- Delete: `moorestech_web/webui/src/features/miningHud/style.module.css`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MapObjectMiningController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractC2Test.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiGateClassification.cs`
- Modify: `moorestech_web/webui/src/app/App.tsx`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.test.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/protocol.ts`

**Interfaces:**

- Removes: `MiningHudTopic`, `MiningHudDto`, `Topics.miningHud`, `MiningHudDataSchema`, `MiningHudData`
- Preserves: `ProgressTopic`, `Topics.progress`, `ProgressData`

- [ ] **Step 1: 契約削除を要求するVitestを先に書く**

```ts
it("does not expose the removed duplicate mining HUD topic", () => {
  expect(Object.values(Topics)).not.toContain("ui.mining_hud");
});
```

- [ ] **Step 2: VitestのREDを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/bridge/contract/validators.test.ts`

Expected: FAIL because `Topics` still contains `ui.mining_hud`.

- [ ] **Step 3: UnityとWebの重複経路を削除する**

`MapObjectMiningController` から `GetFocusTargetName`、`IsMining`、`GetMiningProgress` を削除する。`WebUiGameBinder` の `MiningHudTopic` 登録、C# wire fixture test、gate分類の専用記述、TS topic/schema/validator/type、Appのimport/render、React featureとfixtureを一括削除する。

- [ ] **Step 4: 契約・ビルドをGREENにする**

Run:

```bash
cd moorestech_web/webui
pnpm test -- src/bridge/contract/validators.test.ts src/bridge/contract/wireContract.test.ts
pnpm build
```

Expected: PASS。`MiningHud` や削除済みfixtureの型・importエラーがない。

- [ ] **Step 5: 残存参照を確認する**

Run:

```bash
rg -n 'MiningHud|ui\.mining_hud|Mining Target|GetFocusTargetName|GetMiningProgress' \
  moorestech_client/Assets/Scripts moorestech_web/webui/src moorestech_web/webui/e2e
```

Expected: 0 matches。

- [ ] **Step 6: 重複経路の削除をコミットする**

```bash
git add -A moorestech_client/Assets/Scripts moorestech_web/webui/src
git commit -m "重複する採掘HUD経路を削除する"
```

### Task 3: ProgressBarをホットバー準拠のGaugeBarへ変更する

**Files:**

- Modify: `moorestech_web/webui/src/features/progress/ProgressBar.tsx`
- Modify: `moorestech_web/webui/src/features/progress/style.module.css`
- Modify: `moorestech_web/webui/src/features/inventory/HotbarPanel/style.module.css`
- Modify: `moorestech_web/webui/src/app/index.css`
- Create: `moorestech_web/webui/e2e/capture-mining-progress.ts`

**Interfaces:**

- Consumes: `GaugeBar({ value, testId })`, `TutorialAnchorIds.miningHud`
- Produces: `data-tutorial-anchor="mining.hud"` on the sole progress wrapper

- [ ] **Step 1: ホットバー寸法を共有トークンへ移す**

`index.css` に固定長トークンを置き、既存の `.hotbarFrame` と `.cell` を同じ値へ差し替える。

```css
--hotbar-slot-size: 3.23rem;
--hotbar-slot-gap: 5px;
--hotbar-key-tab-height: 16px;
--mining-progress-hotbar-gap: 12px;
--hotbar-width: calc(var(--hotbar-slot-size) * 9 + var(--hotbar-slot-gap) * 8);
```

- [ ] **Step 2: ProgressBarをGaugeBarへ置換する**

```tsx
import { Text } from "@mantine/core";
import { GaugeBar } from "@/shared/ui";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";

return (
  <div
    data-testid="progress-bar"
    className={styles.wrapper}
    {...tutorialAnchor(TutorialAnchorIds.miningHud)}
  >
    {data.label != null && <Text className={styles.label}>{data.label}</Text>}
    <GaugeBar value={data.progress} testId="progress-gauge" />
  </div>
);
```

`Progress.Section`、`color="green"`、Mantine Progress importを削除する。

- [ ] **Step 3: stage内絶対配置と幅・空隙をCSSで定義する**

```css
.wrapper {
  pointer-events: none;
  position: absolute;
  right: 0;
  bottom: calc(2px + var(--hotbar-slot-size) + var(--hotbar-key-tab-height) + var(--mining-progress-hotbar-gap));
  left: 0;
  width: var(--hotbar-width);
  margin-inline: auto;
  z-index: var(--z-screen);
}
```

ラベルは `overflow: hidden`、`text-overflow: ellipsis`、`white-space: nowrap`、`color: var(--text-muted)` とし、ゲージ幅へ影響させない。

- [ ] **Step 4: PlaywrightをGREENにする**

Run:

```bash
cd moorestech_web/webui
pnpm test:e2e -- progress.spec.ts system/commonHud.spec.ts regression/connection.spec.ts
```

Expected: PASS。採掘時のprogressbarは1本、対象名なし、中心・幅差0.5px以内、空隙12px、既存ゲージ色。

- [ ] **Step 5: lint・build・全Vitestを実行する**

Run:

```bash
cd moorestech_web/webui
pnpm lint
pnpm test
pnpm build
```

Expected: all PASS with no errors。

- [ ] **Step 6: Playwrightで全画面とクロップを撮影・目視する**

`capture-mining-progress.ts` は1280×720でGameScreen、`mining` scenarioを設定し、次を出力する。

- `/tmp/mining-progress-hud/full.png`
- `/tmp/mining-progress-hud/hotbar-crop.png`
- `/tmp/mining-progress-hud/manifest.json`（寸法、12px gap、SHA-256）

画像を開き、対象名、重複、重なり、幅、配色、左右中心を確認する。問題があればCSSとテストを修正し、撮影をやり直す。

- [ ] **Step 7: Unityテスト・コンパイル・Errorログを確認する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractC2Test|WebUiGate"
uloop compile --project-path ./moorestech_client
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: tests/compile PASS、今回変更由来のError 0件。Domain Reloadの場合は45秒後に再実行する。

- [ ] **Step 8: UI統合をコミットする**

```bash
git add moorestech_web/webui moorestech_client/Assets/Scripts
git commit -m "採掘進捗をホットバー準拠HUDへ統合する"
```

### Task 4: 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行する

**Files:**

- Review: `57af8af1e..HEAD` と作業ツリーの全差分

- [ ] **Step 1: moores-code-reviewを完全実行する**

決定論チェック、設計レンズ、汎用reviewer、外部監査、Fable全般、最終diff post-checksを省略せず実行する。確定指摘は修正し、変更に応じてPlaywright、Web build、Unity compileを再実行する。

- [ ] **Step 2: 完了監査を行う**

次の証拠を全て現行HEADから再確認する。

- `Mining Target` / `MiningHud` / `ui.mining_hud` の実装参照0件
- Playwright関連3 spec PASS
- 全Vitest、lint、build PASS
- 画像2枚とmanifestが同じ最終ソースから生成済み
- Unity対象test・compile PASS、Errorログに変更由来エラーなし
- `git status --short` がclean

- [ ] **Step 3: レビュー修正をコミットする**

```bash
git add -A
git commit -m "採掘進捗HUDの最終レビュー指摘を反映する"
```

変更が無い場合は空コミットを作らず、既存コミットとclean statusを完了証拠にする。

## 判断記録（ADR）

- Spec ADR: `docs/superpowers/specs/2026-07-27-mining-progress-hud-design.md#判断記録adr`
- agent前提（タスク分割）: RED確認、重複契約削除、視覚実装、最終レビューを独立した検証境界として分ける。
- agent前提（テスト状態同一性）: mockの既定進捗を非表示にし、ラベル付き汎用進捗とラベルなし採掘進捗を明示scenarioで分離する。
- agent前提（シミュレーター予測を適用）: 採掘mockは `label: null` を送らず、本番serializerと同じくlabelキーを省略して既存 `label?: string` 契約を維持する。
- agent前提（実行方法）: ユーザーが「Playwrightで問題ないと言えるまで」の継続実行を指示しているため、同一セッションでInline Executionする。
