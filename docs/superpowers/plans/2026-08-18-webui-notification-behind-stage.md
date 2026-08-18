# Web UI 通知をstage背面へ沈める Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ゲーム内通知（`NotificationHost`）を最前面Portalから `.stage` の背面レイヤーへ移し、インベントリをはじめとする全画面UI・常駐HUDの裏に沈める。

**Architecture:** 通知は現在 Mantine `Portal`（body直下）に `z-index: var(--z-toast)` = 300 で描かれており、`.viewport` が `position: fixed` でスタッキングコンテキストを作るため **z-index を下げるだけでは裏へ回れない**（body直下の兄弟は `.viewport`（z-index: auto）より必ず前に描かれる）。したがって `NotificationHost` の描画位置を `.viewport` 直下（`.backdrop` と `.stage` の間）へ移す。あわせて App.module.css の生値だった `.backdrop`(0) / `.stage`(1) の層序を `--z-*` トークンへ昇格させ、通知CSSからその層序を参照できるようにし、`zLayerTokens.test.ts` で順序を固定する。

**Tech Stack:** React 18 / TypeScript / CSS Modules / Mantine / Zustand / Vitest（ソーステキスト検査型のデザイン回帰テスト）/ Playwright（e2e, mock-host）

## Requirements

作業ディレクトリはすべて `moorestech_web/webui`。パスは特記なき限りこのディレクトリ相対で記す。

- R1: インベントリ画面を開いている間、通知はインベントリのパネルより**背面**に描かれ、パネルに重なる部分は隠れる。受け入れ基準: `screen === "playerInventory"` で**アイテム入りスロット（完全不透明面）**と通知が重なる領域において、通知の有無でその領域の描画結果が変化しない（e2eのピクセル比較で検証）。パネル面は意図的に半透明なため、空きスロットを含むgrid全体での完全一致は原理的に成立しない（ユーザー裁定 2026-08-18 / `.decisions/2026-08-18-通知背面e2eの比較は不透明マスで行う.md`）。
- R2: 裏に回す対象は特定画面ではなく**全画面UI全般**（インベントリ・ブロックインベントリ・研究・ビルドメニュー・チャレンジ・ポーズ・スキット・モーダル）。受け入れ基準: 画面ごとの条件分岐を一切書かず、`.stage` 全体の背面へ置くことで一律に達成する。
- R3: 通知の見た目（位置・文字サイズ）は現行から変えない。受け入れ基準: `position: fixed` / `top: 50%` / `left: 1rem` を保持し、`--ui-scale` による拡縮に追従させない（`.stage` / `.viewportOverlay` の配下には置かない）。
- R4: 通知は背景ディム（`.backdrop`）より**前**に立ち、画面を開いている間もパネル外へはみ出た部分は読める。受け入れ基準: z層序が `app-backdrop < behind-stage < stage` である。
- R5: 右下の `ToastHost`（ブリッジのエラー・契約違反通知）は `--z-toast` のまま最前面に残す。受け入れ基準: `src/features/toast/style.module.css` を変更しない。
- R6: 層序はDOM順への暗黙依存ではなく `--z-*` トークンで明示し、テストで固定する（ADR 0013 の方針の延長）。受け入れ基準: `src/app/zLayerTokens.test.ts` が `app-backdrop < behind-stage < stage` を検証し、`App.module.css` がトークンを参照している。

**やらないこと（スコープ境界）:**
- `ToastHost` の層・位置の変更（R5）。
- 通知の表示条件・生存尺・アニメーション・文言・ストアの変更。
- 画面が開いている間に通知を非表示にする挙動（「裏に回る」であって「消える」ではない）。
- uGUI 側（`moorestech_client` の Unity UI）への変更。UIはWeb UIのみが対象。
- `--z-toast` / `--z-tooltip` / `--z-modal` / `--z-reconnect` の値変更。

## Global Constraints

- 作業ディレクトリは `moorestech_web/webui`。コマンドはすべてこのディレクトリで実行する。
- コメントは日本語1行→英語1行の2行セット（AGENTS.md）。日本語・英語ともそれぞれ1行に収める。
- Unity固有ファイル（Prefab/シーン/ScriptableObject）は触らない。本planは `.tsx` / `.css` / `.ts` のみを変更する。
- `.cs` ファイルの変更が無いため、Unityコンパイル（`uloop compile`）は不要。
- デザイン判断は `webui-design` スキルのホワイトリストに従う（本planは色・装飾を変更しないため新規の意匠判断は発生しない）。
- 既存テストの流儀に従う: CSSやDOM構造の意図固定は `readFileSync` + `toContain` のソーステキスト検査（前例: `src/app/zLayerTokens.test.ts`, `src/features/notification/notificationAnimationDesign.test.ts`）。実挙動は Playwright e2e（前例: `e2e/tests/notification/animation.spec.ts`）。
- e2e はポート5273を他セッションと共有し衝突で偽の失敗を出すことがある。失敗specが実行のたびに変わる場合はポート衝突を疑い、他セッションのe2eが走っていないか確認してから再実行する。

---

## 配置と前例（spec-architecture-review）

**配置決定インベントリ**

| # | 項目 | 配置先 | 使用する機構 |
|---|---|---|---|
| 1 | z層序トークン `--z-app-backdrop` / `--z-behind-stage` / `--z-stage` | `src/app/tokens.css`（既存 `--z-*` ブロック） | CSSカスタムプロパティ。前例: 同ブロックの `--z-screen`〜`--z-reconnect` |
| 2 | `.backdrop` / `.stage` の z-index のトークン化 | `src/app/App.module.css` | 既存クラスの値差し替えのみ。新規クラス・新規レイヤーは作らない |
| 3 | `NotificationHost` の描画位置 | `src/app/App.tsx` の `.viewport` 直下（`.backdrop` の直後・`.stage` の直前） | JSXの配置変更のみ。ホスト自身は `position: fixed` を保持 |
| 4 | 通知ホストの z-index | `src/features/notification/style.module.css` | `var(--z-toast)` → `var(--z-behind-stage)` |
| 5 | 層序の回帰テスト | `src/app/zLayerTokens.test.ts`（既存に追記） | 既存の `layer()` ヘルパを再利用。新規テストファイルは作らない |
| 6 | 通知の配置の回帰テスト | `src/features/notification/notificationLayering.test.ts`（新規） | ソーステキスト検査。前例: 同ディレクトリの `notificationAnimationDesign.test.ts` |
| 7 | 実挙動のe2e | `e2e/tests/notification/layering.spec.ts`（新規） | Playwright + mock-host。前例: 同ディレクトリの `animation.spec.ts` |

**検査1（層責務）**: 変更はすべて Web UI の表示層（`src/app` = アプリ骨格、`src/features/notification` = 通知機能）に閉じる。`src/bridge`・`src/shared` へは一切追加しない。通知の層序という「アプリ骨格の関心事」はトークンとして `src/app/tokens.css` が持ち、機能側はそれを参照するだけ、という既存の役割分担どおり。

**検査2（前例）**: ADR 0013 が定めた stage族 / viewport族の二分法に対し、本plan は第三の所属「背面viewport族」を ADR 0017 で新規に宣言する。役割同型の既存前例は存在しない（`.backdrop` が唯一 `.stage` より下の要素だが、これは装飾面であり表示要素ではない）ため、**新規パターンとしてADR化済み**（`docs/adr/0017-webui-notification-behind-stage-layer.md`）。層序をトークンで明示する形式は ADR 0013 の「stage内部の前後関係は `--z-*` トークンで明示する（DOM順への暗黙依存をやめる）」に一致させている。

**検査3（イディオム）**: 新規の状態・イベント・購読を導入しない（純粋に描画位置と層序の変更）。UniRx / イベントパケット等の同期機構は関与しない。

**検査4（機構選択）**: 「画面が開いたら通知の z を動的に切り替える」能動介入案と、「常に stage の背面に置く」受動案を比較し、**受動案を採用**した。能動案は (a) Portal兄弟である限り z をどう動かしても `.viewport` より前に描かれるため物理的に成立せず、(b) 成立させるには画面名の一覧を通知機能が知る必要があり、汎用基盤にドメイン語彙を持ち込む違反になる。受動案は画面ごとの条件分岐がゼロで R2 を構造的に満たす。

**データフロー地図**: 本変更は状態の流れに一切参加しない（`bridge → topicStore → NotificationHost` の既存の一方向連鎖は不変で、変わるのは描画先DOMノードと z のみ）。書き手・読み手・交差点のいずれも新設しない。

**機能パリティ死活表（Phase 2.5）**

| 現在使える操作・表示 | 計画後も生きるか | 根拠 |
|---|---|---|
| GameScreen中に通知が左端中央に出る | 生きる | `.viewport` は常時マウントされ、`.stage` の背面でも GameScreen では前面に遮蔽物が無い |
| 通知の入退場アニメーション・7秒生存 | 生きる | `style.module.css` の `animation` と store を変更しない（変更するのは `z-index` の1行のみ） |
| 通知の文字サイズ・位置 | 生きる | `position: fixed` のまま `.stage`（`transform: scale`）の外に置くため拡縮に巻き込まれない |
| 通知のクリック透過（`pointer-events: none`） | 生きる | ホストの `pointer-events: none` を維持。ヒットテストは通知を素通りし `.viewport`（`data-web-ui-transparent`）へ落ちるため、Unityへの入力排他通知（`useWebInputExclusivity`）の判定は変わらない |
| 右下のエラートースト（`ToastHost`） | 生きる | 変更対象外（R5） |
| モーダル・再接続オーバーレイ・チュートリアル・スキット遷移 | 生きる | Portal内に残置。通知だけを Portal から外す |
| インベントリ等を開いた状態で通知の全文を読む | **退化（意図的）** | パネルに重なる部分は隠れる。これがユーザー裁定（ADR 0017）の要求そのものであり、裁定済みのため実装を進めてよい |

---

### Task 1: z層序トークンの新設と固定

`.backdrop` / `.stage` の生値を `--z-*` トークンへ昇格させ、通知が沈む背面層 `--z-behind-stage` をその間に定義する。この時点では通知はまだ移動しない（トークン基盤だけを先に固定する）。

**Files:**
- Modify: `src/app/tokens.css`（`--z-screen` 等が並ぶ `--z-*` ブロック）
- Modify: `src/app/App.module.css`（`.stage` の `z-index: 1`、`.backdrop` の `z-index: 0`）
- Test: `src/app/zLayerTokens.test.ts`（既存へ追記）

**Interfaces:**
- Consumes: なし（本planの最初のタスク）
- Produces: CSSカスタムプロパティ 3つ — `--z-app-backdrop: 0` / `--z-behind-stage: 1` / `--z-stage: 2`。Task 2 は `--z-behind-stage` を参照する。

- [x] **Step 1: 失敗するテストを書く**

`src/app/zLayerTokens.test.ts` の末尾、最後の `it(...)` の直後（`describe` ブロック内）へ以下を追記する。既存の `layer()` ヘルパをそのまま使う。ファイル冒頭の import 群の直後（`const tokens = readFileSync(...)` の次の行）に `appStyles` の読み込みを追加する。

冒頭へ追加する行:

```ts
const appStyles = readFileSync(new URL("./App.module.css", import.meta.url), "utf8");
```

`describe` ブロック内へ追加するテスト:

```ts
  it("通知が沈む背面層はディムより前・stageより後ろに立つ", () => {
    // 背面層がディムより後ろだと暗転に飲まれ、stageより前だと画面UIに被さる
    // Behind the dim it drowns in the darkening; ahead of the stage it covers the screen UI
    expect(layer("app-backdrop")).toBeLessThan(layer("behind-stage"));
    expect(layer("behind-stage")).toBeLessThan(layer("stage"));
  });

  it("stageとディムの層序は生値でなくトークンを参照する", () => {
    // 生値のままだと通知側CSSから層序を参照できず、DOM順への暗黙依存へ戻る
    // Raw values leave the notification CSS unable to reference the order, regressing to implicit DOM-order reliance
    expect(appStyles).toContain("z-index: var(--z-stage)");
    expect(appStyles).toContain("z-index: var(--z-app-backdrop)");
  });
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/app/zLayerTokens.test.ts`
Expected: FAIL。`--z-app-backdrop is missing from tokens.css`（`layer()` が throw）で1件、`z-index: var(--z-stage)` を含まない旨で1件。

- [x] **Step 3: トークンを定義する**

`src/app/tokens.css` の `--z-screen: 20;` の**直前**へ以下を挿入する（既存の `/* 画面レイヤーの層序を一元化し... */` コメントの下）:

```css
  /* stage全体との前後関係。通知はディムより前・stageより後ろの背面層へ沈める（ADR 0017） */
  /* Whole-stage ordering; notifications sink into a layer ahead of the dim and behind the stage (ADR 0017) */
  --z-app-backdrop: 0;
  --z-behind-stage: 1;
  --z-stage: 2;
```

- [x] **Step 4: App.module.css を生値からトークンへ差し替える**

`src/app/App.module.css` の `.stage` 内 `z-index: 1;` を次へ置換する:

```css
  z-index: var(--z-stage);
```

同ファイルの `.backdrop` 内 `z-index: 0;` を次へ置換する:

```css
  z-index: var(--z-app-backdrop);
```

- [x] **Step 5: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/app/zLayerTokens.test.ts`
Expected: PASS（既存2件＋追加2件の計4件）

- [x] **Step 6: 全ユニットテストとlintを実行する**

Run: `cd moorestech_web/webui && pnpm test && pnpm lint`
Expected: 既存テストの失敗ゼロ、lintエラーゼロ

- [x] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/app/tokens.css moorestech_web/webui/src/app/App.module.css moorestech_web/webui/src/app/zLayerTokens.test.ts
git commit -m "refactor(webui): stageとディムの層序を--z-*トークンへ昇格し背面層を定義する"
```

---

### Task 2: 通知をPortalから外しstage背面へ移す

`NotificationHost` を `Portal` から `.viewport` 直下（`.backdrop` の直後・`.stage` の直前）へ移し、ホストの z-index を `--z-behind-stage` にする。

**Files:**
- Modify: `src/app/App.tsx:84-85`（`.backdrop` と `.stage` の間へ挿入）、`src/app/App.tsx:126-131`（`<Portal>` 内から `<NotificationHost />` を削除）
- Modify: `src/features/notification/style.module.css`（`.host` の `z-index`）
- Test: `src/features/notification/notificationLayering.test.ts`（新規）

**Interfaces:**
- Consumes: Task 1 が定義した `--z-behind-stage`
- Produces: なし（`NotificationHost` の export・props は不変。`src/features/notification/index.ts` は変更しない）

- [x] **Step 1: 失敗するテストを書く**

`src/features/notification/notificationLayering.test.ts` を新規作成する:

```ts
// 通知がstage背面へ置かれ続けることを固定する（ADR 0017）
// Locks the notification into the layer behind the stage (ADR 0017)
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const style = readFileSync(new URL("./style.module.css", import.meta.url), "utf8");
const app = readFileSync(new URL("../../app/App.tsx", import.meta.url), "utf8");

describe("notification layering", () => {
  it("通知ホストは背面層のトークンを使い、最前面のトースト層を使わない", () => {
    expect(style).toContain("z-index: var(--z-behind-stage)");
    expect(style).not.toContain("var(--z-toast)");
  });

  it("通知はPortalの外、stageより前のDOM位置に描かれる", () => {
    // Portal内はbody直下の兄弟になり、zをどう下げても.viewportより前に描かれる
    // Inside the portal it becomes a body-level sibling and paints ahead of .viewport at any z
    const hostIndex = app.indexOf("<NotificationHost />");
    const portalIndex = app.indexOf("<Portal>");
    const stageIndex = app.indexOf("className={styles.stage}");
    expect(hostIndex).toBeGreaterThan(-1);
    expect(hostIndex).toBeLessThan(stageIndex);
    expect(hostIndex).toBeLessThan(portalIndex);
  });

  it("通知は実画面へ固定され、stage拡縮に追従しない", () => {
    expect(style).toContain("position: fixed");
    expect(style).toContain("top: 50%");
    expect(style).toContain("left: 1rem");
  });
});
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/features/notification/notificationLayering.test.ts`
Expected: FAIL。1件目は `z-index: var(--z-behind-stage)` を含まない、2件目は `hostIndex`（Portal内なので後方）が `stageIndex` より大きい。

- [x] **Step 3: 通知ホストのz-indexを背面層へ変える**

`src/features/notification/style.module.css` の `.host` 内 `z-index: var(--z-toast);` を次へ置換する:

```css
  z-index: var(--z-behind-stage);
```

- [x] **Step 4: App.tsx の描画位置を移す**

`src/app/App.tsx` の `<Portal>` ブロックから `<NotificationHost />` の行を**削除**する。変更後:

```tsx
      <Portal>
        <ToastHost />
        <SkitTransition />
        <TutorialOverlay />
        <WorldPinOverlay />
      </Portal>
```

同ファイルの `.backdrop` の行と `.stage` の行の間へ `<NotificationHost />` を挿入する。変更後:

```tsx
      {modalScreen && <div className={styles.backdrop} data-testid="screen-backdrop" />}
      {/* 通知はstage背面へ置き、全画面UIと常駐HUDの裏へ沈める（ADR 0017） */}
      {/* Notifications sit behind the stage so every screen and always-on HUD covers them (ADR 0017) */}
      <NotificationHost />
      <div ref={stageRef} className={styles.stage} data-web-ui-transparent>
```

import 行（`import { NotificationHost } from "@/features/notification";`）はそのまま残す。

- [x] **Step 5: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/features/notification/notificationLayering.test.ts`
Expected: PASS（3件）

- [x] **Step 6: 全ユニットテスト・型検査・lintを実行する**

Run: `cd moorestech_web/webui && pnpm test && pnpm build && pnpm lint`
Expected: すべて成功。`pnpm build` の `tsc -b` で未使用importなどの型エラーが出ないこと。

- [x] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/app/App.tsx moorestech_web/webui/src/features/notification/style.module.css moorestech_web/webui/src/features/notification/notificationLayering.test.ts
git commit -m "fix(webui): 通知をstage背面へ移し全画面UIの裏へ沈める"
```

---

### Task 3: 実挙動をe2eで固定する

インベントリ画面を開いた状態で、通知が実際にパネルの背面に描かれている（＝パネルの見た目を1ピクセルも変えない）ことと、GameScreenでは変わらず見えることをブラウザ上で検証する。

**Files:**
- Test: `e2e/tests/notification/layering.spec.ts`（新規）

**Interfaces:**
- Consumes: Task 2 で移設済みの `NotificationHost`。既存のmock-host操作 `setUiState(page, "PlayerInventory" | "GameScreen")` と `setTopicScenario(page, "notificationAchievement" | "notificationClear")`（`e2e/support/mockControl` から export）。既存 testid: `notification-row`（通知1行）、`notification-host`（通知ホスト）、`main-grid`（インベントリのメイングリッド）。
- Produces: なし

- [x] **Step 1: 失敗するテストを書く**

`e2e/tests/notification/layering.spec.ts` を新規作成する:

```ts
import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  // 他specへ漏らさず空へ戻す
  // Reset to empty so it doesn't leak to other specs
  await setTopicScenario(page, "notificationClear");
  await setUiState(page, "PlayerInventory");
});

test("インベントリを開いている間、通知はパネルの描画を一切変えない", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");

  const grid = page.getByTestId("main-grid");
  await expect(grid).toBeVisible();
  const before = await grid.screenshot();

  await setTopicScenario(page, "notificationAchievement");
  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();

  // 重なっていなければこの検証は無意味になるため、まず重なりを確定させる
  // Without an actual overlap the check is vacuous, so pin the intersection first
  const gridBox = await grid.boundingBox();
  const rowBox = await row.boundingBox();
  expect(gridBox).not.toBeNull();
  expect(rowBox).not.toBeNull();
  const overlapWidth = Math.min(gridBox!.x + gridBox!.width, rowBox!.x + rowBox!.width) - Math.max(gridBox!.x, rowBox!.x);
  const overlapHeight = Math.min(gridBox!.y + gridBox!.height, rowBox!.y + rowBox!.height) - Math.max(gridBox!.y, rowBox!.y);
  expect(overlapWidth).toBeGreaterThan(0);
  expect(overlapHeight).toBeGreaterThan(0);

  // 背面にいるならパネルの画素は通知の有無で変わらない
  // If it truly sits behind, the panel's pixels are identical with and without the notification
  const after = await grid.screenshot();
  expect(after.equals(before)).toBe(true);
});

test("GameScreenでは通知が遮られず読める", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "notificationAchievement");

  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();
  const box = await row.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.width).toBeGreaterThan(0);
  expect(box!.height).toBeGreaterThan(0);
});

test("通知ホストはstageより後ろの層に立つ", async ({ page }) => {
  await setUiState(page, "PlayerInventory");
  await page.goto("/");
  await setTopicScenario(page, "notificationAchievement");
  await expect(page.getByTestId("notification-host")).toBeVisible();

  // 算出済みのz値で層序を確認する（トークン差し替えの取りこぼしを拾う）
  // Compare the computed z values so a missed token swap is caught
  const layers = await page.evaluate(() => {
    const host = document.querySelector('[data-testid="notification-host"]') as HTMLElement;
    const stage = document.querySelector('[data-testid="app-stage"]') as HTMLElement;
    return {
      sameParent: host.parentElement === stage.parentElement,
      hostZ: Number.parseInt(getComputedStyle(host).zIndex, 10),
      stageZ: Number.parseInt(getComputedStyle(stage).zIndex, 10),
    };
  });
  expect(layers.sameParent).toBe(true);
  expect(layers.hostZ).toBeLessThan(layers.stageZ);
});
```

3本目が参照する `data-testid="app-stage"` はまだ存在しないため、この時点では必ず失敗する。Step 3 で App.tsx へ付与する。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test:e2e -- e2e/tests/notification/layering.spec.ts`
Expected: 3本目が FAIL（`app-stage` の要素が見つからず `stage` が null になる）。1本目・2本目は Task 2 完了済みなので PASS する。

- [x] **Step 3: stageにtestidを付ける**

`src/app/App.tsx` の stage の div へ testid を追加する:

```tsx
      <div ref={stageRef} className={styles.stage} data-testid="app-stage" data-web-ui-transparent>
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test:e2e -- e2e/tests/notification/layering.spec.ts`
Expected: PASS（3件）。失敗する場合、他セッションのe2eによるポート5273の衝突を疑い、単独で再実行する。

- [x] **Step 5: 通知まわりの既存e2eが壊れていないことを確認する**

Run: `cd moorestech_web/webui && pnpm test:e2e -- e2e/tests/notification e2e/tests/inventory`
Expected: すべて PASS

- [x] **Step 6: ユニットテストとlintを再実行する**

Run: `cd moorestech_web/webui && pnpm test && pnpm lint`
Expected: すべて成功

- [x] **Step 7: コミットする**

```bash
git add moorestech_web/webui/e2e/tests/notification/layering.spec.ts moorestech_web/webui/src/app/App.tsx
git commit -m "test(webui): 通知がstage背面に沈むことをe2eで固定する"
```

---

### Task 4: 全ブランチレビュー（省略不可）

- [x] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、master からの全差分をレビュー対象にする。指摘のうち機械的修正は適用し、設計判断は AskUserQuestion でユーザーへ諮る。

このタスクはゴール文言や「変更が小さいから」を理由に省略できない。

- [x] **Step 2: レビュー指摘の反映をコミットする**

```bash
git add -A
git commit -m "fix(webui): コードレビュー指摘を反映する"
```

（指摘がゼロだった場合はコミット不要。その旨を報告する）

---

## 判断記録（ADR）

- `docs/adr/0017-webui-notification-behind-stage-layer.md` — 通知をstage背面レイヤーへ置き全画面UIの裏に回す。stage族/viewport族に対する第三の所属「背面viewport族」の宣言を含む。
  出所: ユーザー裁定 2026-08-18
- `docs/adr/0013-webui-stage-family-vs-viewport-family.md` — stage族/viewport族の二分法と、層序をトークンで明示する方針。本planはその延長。
- `.decisions/2026-08-18-通知は全画面UIの裏へ沈める.md` — 棄却案（z-indexだけ下げる／viewportOverlayへ移設／Toastも下げる）の記録。

planning中に生じた判断:

- **z値の割り当てを `app-backdrop: 0` / `behind-stage: 1` / `stage: 2` とし、`.stage` を 1 から 2 へ繰り上げる**。同値のタイをDOM順で解決する形（backdrop と通知をともに 0 にする）を避け、ADR 0013 の「DOM順への暗黙依存をやめる」方針に揃えるため。
  出所: agent前提（ADR 0013 の方針の適用）
- **`.stage` へ `data-testid="app-stage"` を追加する**。e2e から算出z値を取得する際のセレクタを安定させるため。既存の testid 付与（`screen-backdrop`・`notification-host` 等）と同形式。
  出所: agent前提（既存 testid 命名の踏襲）
- **e2e はピクセル比較（パネル領域の screenshot 一致）で「背面にいる」ことを検証する**。通知ホストは `pointer-events: none` のため `elementsFromPoint` によるヒットテスト検証が使えず、実際の描画結果を見る方法が唯一の実挙動検証になる。重なりが存在することを bounding box で先に確定させ、検証が空振りにならないようにする。
  出所: agent前提
