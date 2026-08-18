# 目標HUDの面と通知の出入りアニメ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Web UI の左上の目標HUD（`CurrentChallengeHud`）にインベントリ同族の半透明ネイビー面＋4辺の境界フェードを敷き、ゲーム通知（`NotificationHost`）に左からのスライド＋フェードの出入りアニメを入れる。

**Architecture:** 面は `shared/ui/GamePanel` に新variant `"hud"`（面と境界フェードだけを持ち、タイトル罫線・下向き三角・右下グリップを持たない）を追加して供給する。目標HUDは外側の `<section>` が位置決め（実viewport左上）を担い、内側の `GamePanel variant="hud"` が面と安全帯paddingを担う。通知の出入りは CSS の2本のアニメーション（入場＝即時、退場＝`生存尺 − 退場尺` の遅延つき）で表現し、生存尺 `NOTIFICATION_DISPLAY_MS` を単一の正としてインラインCSS変数で流し込む。`notificationStore` に退場用の状態は追加しない。

**Tech Stack:** React 18 + TypeScript + CSS Modules（Vite）/ zustand / vitest（node環境・ソーステキスト検査型のデザインテスト）/ Playwright（mock-host）

## Requirements

- 目標HUDに面を敷く。面色はインベントリパネル面と同値の `rgb(10 14 27 / 80%)` で、新しい面色トークンを追加しない ／ 受け入れ: `--hud-panel-face` が既存の面色と同値であること、`--challenge-hud-face` のような別系統の色を作らないこと
- 面は4辺すべてを固定長で境界フェードさせる ／ 受け入れ: `mask-image` が横方向・縦方向の2枚を `mask-composite: intersect` で合成し、フェード幅は `--hud-panel-edge-fade`（= `--panel-edge-fade` = 12px）であること
- 面は `GamePanel` の新variant `"hud"` が供給する。HUD側CSSに独自のパネル面を作らない ／ 受け入れ: `CurrentChallengeHud.module.css` に `background` を持つ面レイヤ（`::before` 等）が存在しないこと
- 面の外形は現行位置（実viewport左上24px）に据え、文字は安全帯paddingで内側へ寄せる ／ 受け入れ: HUD要素の `top`/`left` が24px、`--hud-panel-padding` が20pxで、文字の左端が画面端から44px相当になること
- 面幅は現行の `--challenge-hud-width: 520px` 固定。目標文が短くても面は縮めない ／ 受け入れ: 目標1件でもHUD要素の `width` が520pxであること
- 文字影は削除せず、通知同族の控えめな値へ弱める ／ 受け入れ: `--challenge-hud-text-shadow` が `0.35px 0.35px 0 rgb(0 0 0 / 80%)` であること
- 「見出し → `FadeRule` → 目標一覧」の構成と罫線幅176pxは面付き後も維持する ／ 受け入れ: `aria-hidden="true"` の罫線が1本、幅176pxであること
- 目標HUDにアニメーションは追加しない ／ 受け入れ: HUD要素の `animationName` が `"none"` であること
- 面付きHUDがメニューへ重ならないよう `--menu-upper-safe-area` を 128px → 168px へ広げる ／ 受け入れ: トークンが168px、`buildMenuLayout.spec.ts`（トークン由来の期待値）が緑であること
- 通知（`NotificationHost` のみ）に入場＝左から12pxのスライドイン＋フェードイン、退場＝その逆再生を入れる ／ 受け入れ: 通知行の `animationName` に入場・退場の2本が載り、`animationDelay` が `0s, 6.8s`、`animationFillMode` が `both, forwards` であること
- 通知の生存尺を5秒→7秒にし、出入りは7秒の内側に収める ／ 受け入れ: `NOTIFICATION_DISPLAY_MS === 7000`、単体テストで7秒経過後に配列が空になること
- 退場のために `notificationStore` へ状態（`status`/`exiting`/二段タイマー）を追加しない ／ 受け入れ: `notificationStore.ts` の `GameNotification` 型にフィールドが増えていないこと、`setTimeout` が1本であること
- 生存尺をJSとCSSに二重定義しない ／ 受け入れ: CSSの `--notification-lifetime` が `NotificationHost` から `NOTIFICATION_DISPLAY_MS` 由来のインラインstyleで渡ること、CSSファイルに `7000ms`/`7s` の直書きが無いこと
- 通知の同時表示数に上限を設けない ／ 受け入れ: store に件数上限の判定が無いこと
- デザイン哲学（`webui-design` SKILL）の §1・§2・§6・§8・§8.14 を今回の例外込みに改訂する ／ 受け入れ: 5節すべてに今回の決定が反映され、他節と矛盾しないこと（§2はvariant一覧へhudを追記）

### やらないこと（スコープ境界）

- `ToastHost` の様式化・アニメーション（別issue `moorestech-cgm`）
- 既存通知の積み替え（下へ押される動き）の補間（FLIP相当）
- 同一通知の集約（「×N」表示）・同時表示数の上限
- `prefers-reduced-motion` の分岐（CEF埋め込みでOS設定が届かないため。要求時に再裁定）
- Web UI からの uGUI／正本パリティ語彙・機構の撤去（別issue `moorestech-5lb`。本planは `--menu-upper-safe-area` の168px化のみ先行し、`e2e/parity_targets.py` の目標値は引き直さない）
- 装備HUD・ホットバー等、他の常時表示HUDへの面の展開（`variant="hud"` は今回目標HUDだけが使う）

## Global Constraints

- 対象worktree: `/Users/sakastudio/moorestech-worktrees/webui-hud`、branch `feature/webui-screen-edge-and-list-polish`。**このブランチは別セッションと共有していた**ため、`git switch` / `git checkout <branch>` / `git reset` / `git stash` / `git clean` / `git add -A` / rebase / force push は使用禁止。自分が触ったパスだけを明示 `git add` する
- 色・寸法・z層は必ず `src/app/tokens.css` のトークン経由。機能側CSSへの色・z-index直書きは禁止
- 視覚寸法は固定長トークンが既定。%指定は「画面比例が要件」の場合のみで、理由をコメントに書く
- 表示文字列は必ず `t()` を通す（`no-jsx-visible-literal` lint）
- 主要な処理セクションに日本語・英語の2行セットコメント（`// 日本語` → `// English`）を入れる。各言語1行で折り返さない
- 1ファイル200行以下、1ディレクトリ10ファイル以下
- `partial` 相当の分割・`Func<>`（TS では型としての `Function`）は使わない。`try-catch` は外部境界のみ
- テスト実行コマンド: 単体 `npm run test`（`npx vitest run <path>` で絞る）、e2e `npm run test:e2e`（`npx playwright test --config e2e/playwright.config.ts <spec>` で絞る）。いずれも `moorestech_web/webui` で実行する
- CSS Modules は `@keyframes` 名もハッシュ化する。計算値の `animation-name` を検査するときは完全一致ではなく部分一致（正規表現）で照合する

## File Structure

| ファイル | 種別 | 責務 |
|---|---|---|
| `moorestech_web/webui/src/app/tokens.css` | 変更 | HUD面トークン3件と通知アニメトークン3件を追記、`--menu-upper-safe-area` と `--challenge-hud-text-shadow` の値を変更 |
| `moorestech_web/webui/src/shared/ui/GamePanel/index.tsx` | 変更 | `variant="hud"` を型とクラスマップへ追加 |
| `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css` | 変更 | 既定面の `::before` から `.hud` を除外し、`.hud` の面＋4辺フェードを追加 |
| `moorestech_web/webui/src/shared/ui/GamePanel/hudVariantDesign.test.ts` | 新規 | hud variant が面と境界フェードだけを持つことを固定する検査 |
| `moorestech_web/webui/src/features/challenge/CurrentChallengeHud.tsx` | 変更 | 内容を `GamePanel variant="hud"` で包み、見出しへ testid を付与 |
| `moorestech_web/webui/src/features/challenge/CurrentChallengeHud.module.css` | 変更 | 位置決めだけを残し、文字影を弱める |
| `moorestech_web/webui/src/features/notification/notificationStore.ts` | 変更 | 生存尺を7000へ変更し `NOTIFICATION_DISPLAY_MS` として公開 |
| `moorestech_web/webui/src/features/notification/notificationStore.test.ts` | 変更 | 7秒での消滅と、退場状態を持たないことを検査 |
| `moorestech_web/webui/src/features/notification/NotificationHost.tsx` | 変更 | 生存尺をインラインCSS変数で渡し、行へ testid を付与 |
| `moorestech_web/webui/src/features/notification/style.module.css` | 変更 | 入場・退場の2本のアニメーションと keyframes を追加 |
| `moorestech_web/webui/src/features/notification/notificationAnimationDesign.test.ts` | 新規 | 生存尺の単一正・fill-mode・尺の直書き禁止を固定する検査 |
| `moorestech_web/webui/e2e/support/challengeHudAssertions.ts` | 変更 | 見出しの取得を `firstElementChild` から testid へ変更 |
| `moorestech_web/webui/e2e/tests/challenge/hudLayout.spec.ts` | 変更 | 面あり前提へ契約を更新（テスト名も改める） |
| `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts` | 変更 | `notificationClear` シナリオを追加 |
| `moorestech_web/webui/e2e/tests/notification/animation.spec.ts` | 新規 | 通知の出入りアニメの契約と、生存尺経過での消滅を検証 |
| `.claude/skills/webui-design/SKILL.md` | 変更 | §1・§6・§8・§8.14 を改訂 |

---

## Task 1: GamePanel に hud variant と面トークンを足す

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Modify: `moorestech_web/webui/src/shared/ui/GamePanel/index.tsx`
- Modify: `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
- Test: `moorestech_web/webui/src/shared/ui/GamePanel/hudVariantDesign.test.ts`

**Interfaces:**
- Consumes: 既存トークン `--panel-edge-fade`（12px、`tokens.css:43`）
- Produces:
  - CSS変数 `--hud-panel-face` / `--hud-panel-edge-fade` / `--hud-panel-padding`
  - `GamePanel` の props `variant?: "default" | "craft" | "skit" | "hud"`（Task 2 が `"hud"` を渡す）
  - CSSクラス `styles.hud`（GamePanel内部のみ。外部から参照しない）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_web/webui/src/shared/ui/GamePanel/hudVariantDesign.test.ts` を新規作成:

```ts
// hud variantが面と境界フェードだけを持ち、罫線・三角・グリップを持たないことを固定する
// Locks the hud variant to a face and boundary fade, without rules, triangles, or a grip
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const component = read("./index.tsx");
const style = read("./style.module.css");
const tokens = read("../../../app/tokens.css");

describe("GamePanel hud variant", () => {
  it("variantの型とクラスマップにhudを持つ", () => {
    expect(component).toContain('variant?: "default" | "craft" | "skit" | "hud"');
    expect(component).toContain("hud: styles.hud");
  });

  it("hudの面色はパネル面と同値でフェード幅は共通トークンを使う", () => {
    expect(tokens).toContain("--hud-panel-face: rgb(10 14 27 / 80%)");
    expect(tokens).toContain("--hud-panel-edge-fade: var(--panel-edge-fade)");
    expect(tokens).toContain("--hud-panel-padding: 20px");
  });

  it("hudの面は4辺を固定長でフェードする", () => {
    const hudFace = style.slice(style.indexOf(".hud::before"));
    expect(hudFace).toContain("background: var(--hud-panel-face)");
    expect(hudFace).toContain("90deg, transparent 0, #000 var(--hud-panel-edge-fade)");
    expect(hudFace).toContain("180deg, transparent 0, #000 var(--hud-panel-edge-fade)");
    expect(hudFace).toContain("mask-composite: intersect");
  });

  it("既定面のフェード合成からhudを除外する", () => {
    expect(style).toContain(".panel:not(.craft):not(.skit):not(.hud)::before");
  });

  it("hudは罫線・三角・グリップの装飾を持たない", () => {
    const hudRules = style.slice(style.indexOf(".hud {"));
    expect(hudRules).not.toContain("decoLine");
    expect(hudRules).not.toContain("bottomDeco");
    expect(hudRules).not.toContain("clip-path");
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/shared/ui/GamePanel/hudVariantDesign.test.ts`
Expected: FAIL（5件すべて。`--hud-panel-face` も `.hud` も未定義）

- [ ] **Step 3: トークンを追加する**

`src/app/tokens.css` の `--menu-content-height: 525px;` の**次の行**（`/* 常駐チャレンジHUDは左上へ固定し… */` コメントの直前）へ挿入:

```css
  /* 常時表示HUDの面。色はパネル面と同値、フェード幅は共通トークン、余白はフェード帯を避ける安全帯 */
  /* Resident-HUD face: panel-equal color, the shared fade width, and padding that clears the fade band */
  --hud-panel-face: rgb(10 14 27 / 80%);
  --hud-panel-edge-fade: var(--panel-edge-fade);
  --hud-panel-padding: 20px;
```

- [ ] **Step 4: GamePanel に variant を追加する**

`src/shared/ui/GamePanel/index.tsx` の変更2箇所。

variant の型宣言（`// skit: 画面下部の全幅会話帯` のコメント群の直後）:

```tsx
  // hud: 常時表示HUD用に面と境界フェードだけを持つ
  // hud: face and boundary fade only, for resident HUDs
  variant?: "default" | "craft" | "skit" | "hud";
```

クラスマップ:

```tsx
const VARIANT_CLASS_NAMES = { default: "", craft: styles.craft, skit: styles.skit, hud: styles.hud };
```

- [ ] **Step 5: hud の面をCSSへ追加する**

`src/shared/ui/GamePanel/style.module.css` の既定面セレクタを1箇所書き換える:

```css
.panel:not(.craft):not(.skit):not(.hud)::before {
```

そのうえでファイル末尾（`.bottomDeco span { … }` の後）へ追加:

```css
/* 常時表示HUDの面。フェード帯へ内容が載らないよう全辺に安全帯paddingを取る */
/* Resident-HUD face; padding on every side keeps content off the fade band */
.hud {
  padding: var(--hud-panel-padding);
}

/* 面は4辺を同尺でフェードさせ、世界背景へ溶かす。罫線・三角・グリップは持たない */
/* The face fades on all four edges into the world; no rules, triangles, or grip */
.hud::before {
  position: absolute;
  inset: 0;
  z-index: 0;
  background: var(--hud-panel-face);
  -webkit-mask-image: linear-gradient(90deg, transparent 0, #000 var(--hud-panel-edge-fade), #000 calc(100% - var(--hud-panel-edge-fade)), transparent 100%), linear-gradient(180deg, transparent 0, #000 var(--hud-panel-edge-fade), #000 calc(100% - var(--hud-panel-edge-fade)), transparent 100%);
  -webkit-mask-composite: source-in;
  mask-image: linear-gradient(90deg, transparent 0, #000 var(--hud-panel-edge-fade), #000 calc(100% - var(--hud-panel-edge-fade)), transparent 100%), linear-gradient(180deg, transparent 0, #000 var(--hud-panel-edge-fade), #000 calc(100% - var(--hud-panel-edge-fade)), transparent 100%);
  mask-composite: intersect;
  content: "";
  pointer-events: none;
}
```

内容を面より前面へ出す既存ルール `.panel:not(.craft):not(.skit) > *:not(.bottomDeco)` は `.hud` にもそのまま適用されるため、新しい重なり順ルールは追加しない。

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx vitest run src/shared/ui/GamePanel/hudVariantDesign.test.ts`
Expected: PASS（5件）

- [ ] **Step 7: 既存の単体テストとlintが緑であることを確認する**

Run: `cd moorestech_web/webui && npm run test && npm run lint`
Expected: PASS（既存テストの失敗0件、lintエラー0件）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_web/webui/src/app/tokens.css moorestech_web/webui/src/shared/ui/GamePanel/index.tsx moorestech_web/webui/src/shared/ui/GamePanel/style.module.css moorestech_web/webui/src/shared/ui/GamePanel/hudVariantDesign.test.ts
git commit -m "feat(webui): GamePanelへ常時表示HUD用のhud variantを追加する"
```

---

## Task 2: 目標HUDを面付きにしメニュー上端の安全帯を広げる

**Files:**
- Modify: `moorestech_web/webui/src/features/challenge/CurrentChallengeHud.tsx`
- Modify: `moorestech_web/webui/src/features/challenge/CurrentChallengeHud.module.css`
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Modify: `moorestech_web/webui/e2e/support/challengeHudAssertions.ts`
- Test: `moorestech_web/webui/e2e/tests/challenge/hudLayout.spec.ts`

**Interfaces:**
- Consumes: Task 1 の `GamePanel variant="hud"`、`--hud-panel-padding`
- Produces: DOM契約 — 外側 `<section data-testid="challenge-hud">`（位置決め）→ 内側 `GamePanel`（面）→ `<div data-testid="challenge-hud-label">`（見出し）／`aria-hidden="true"` の罫線1本／`<div data-testid="challenge-objective">`（目標行）

- [ ] **Step 1: 失敗するe2eテストを書く**

`e2e/tests/challenge/hudLayout.spec.ts` の1本目のテストを次の内容へ差し替える（テスト名も変える）:

```ts
test("進行中チャレンジを内部キーを出さずインベントリ同族の面付きで表示する", async ({ page }) => {
  await setTopicScenario(page, "challengeJapanese");
  await setUiState(page, "GameScreen");
  await page.goto("/");
  const hud = page.getByTestId("challenge-hud");
  await expect(hud).toContainText("現在のチャレンジ");
  await expect(hud).toContainText("石を採掘する");
  await expect(hud).not.toContainText("challenge.current");
  await expect(hud).toHaveCSS("pointer-events", "none");

  // 左上固定の寸法と短い罫線を検証する
  // Verify top-left fixed dimensions and the shortened rule
  await expect(hud).toHaveCSS("top", "24px");
  await expect(hud).toHaveCSS("left", "24px");
  await expect(hud).toHaveCSS("width", "520px");
  await expect(hud).toHaveCSS("text-shadow", "rgba(0, 0, 0, 0.8) 0.35px 0.35px 0px");
  const rule = hud.locator('[aria-hidden="true"]');
  await expect(rule).toHaveCount(1);
  await expect(rule).toHaveCSS("width", "176px");

  // 面はGamePanelのhud variantが供給し、4辺フェードと安全帯paddingを持つ
  // The face comes from GamePanel's hud variant with a four-edge fade and safe-area padding
  const face = hud.locator('[data-variant="hud"]');
  await expect(face).toHaveCount(1);
  await expect(face).toHaveCSS("padding", "20px");
  const faceLayer = await face.evaluate((element) => {
    const before = getComputedStyle(element, "::before");
    return { background: before.backgroundColor, maskImage: before.maskImage || before.webkitMaskImage };
  });
  expect(faceLayer.background).toBe("rgba(10, 14, 27, 0.8)");
  // 横方向・縦方向の2枚が載ることで4辺フェードが成立する
  // Both the horizontal and vertical gradients must be present for a four-edge fade
  expect(faceLayer.maskImage).toContain("90deg");
  expect(faceLayer.maskImage).toContain("180deg");

  // HUD自身はアニメーションも角丸も枠も持たない
  // The HUD itself keeps no animation, radius, or border
  const visualContract = await hud.evaluate((element) => {
    const hudStyle = getComputedStyle(element);
    const labelStyle = getComputedStyle(element.querySelector('[data-testid="challenge-hud-label"]')!);
    const objectiveStyle = getComputedStyle(element.querySelector('[data-testid="challenge-objective"]')!);
    return {
      animationName: hudStyle.animationName,
      borderRadius: hudStyle.borderRadius,
      borderWidth: hudStyle.borderWidth,
      fontWeight: objectiveStyle.fontWeight,
      labelLetterSpacing: labelStyle.letterSpacing,
      objectiveLineHeight: objectiveStyle.lineHeight,
    };
  });
  expect(visualContract).toEqual({
    animationName: "none",
    borderRadius: "0px",
    borderWidth: "0px",
    fontWeight: "400",
    labelLetterSpacing: "1px",
    objectiveLineHeight: "20px",
  });
});

test("面付きHUDは目標3件でもメニュー上端の安全帯に収まる", async ({ page }) => {
  await setTopicScenario(page, "challengeMultiple");
  await setUiState(page, "PlayerInventory");
  await page.goto("/");

  const hud = page.getByTestId("challenge-hud");
  const hudBox = await hud.boundingBox();
  expect(hudBox).not.toBeNull();
  const safeArea = await page.evaluate(() =>
    Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--menu-upper-safe-area")));
  expect(safeArea).toBe(168);
  expect(hudBox!.y + hudBox!.height).toBeLessThanOrEqual(safeArea);
});
```

- [ ] **Step 2: e2eを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx playwright test --config e2e/playwright.config.ts e2e/tests/challenge/hudLayout.spec.ts`
Expected: FAIL（`[data-variant="hud"]` が0件、`text-shadow` が旧値、`--menu-upper-safe-area` が128）

- [ ] **Step 3: HUDを GamePanel variant="hud" で包む**

`src/features/challenge/CurrentChallengeHud.tsx` の `return` を差し替える（import に `GamePanel` を追加。`FadeRule` と同じ `@/shared/ui` から取る）:

```tsx
import { FadeRule, GamePanel } from "@/shared/ui";
```

```tsx
  // 面はGamePanelのhud variantが供給し、この階層は位置決めと情報階層だけを持つ
  // GamePanel's hud variant supplies the face; this level only positions and orders the content
  const label = t(L.ui.challenge.currentTitle);
  return (
    <section
      className={styles.hud}
      aria-label={label}
      data-testid="challenge-hud"
      {...tutorialAnchor(TutorialAnchorIds.challengeCurrentHud)}
    >
      <GamePanel variant="hud">
        <div className={styles.label} data-testid="challenge-hud-label">{label}</div>
        <div className={styles.rule}>
          <FadeRule />
        </div>
        <div className={styles.objectives}>
          {current.challenges.map((challenge) => (
            <div key={challenge.guid} className={styles.objective} data-testid="challenge-objective">
              {t(challengeTitleKey(challenge.guid))}
            </div>
          ))}
        </div>
      </GamePanel>
    </section>
  );
```

`@/shared/ui` が `GamePanel` を export していない場合は `src/shared/ui/index.ts` へ `export { default as GamePanel } from "./GamePanel";` を追加する（既存exportの並びに従う）。

- [ ] **Step 4: HUDのCSSから面の役割を外す**

`src/features/challenge/CurrentChallengeHud.module.css` の先頭コメントと `.hud` を差し替える（`.rule` / `.label` / `.objectives` / `.objective` は変更しない）:

```css
/* 世界上へ面付きで重ねる常駐HUD。位置決めだけを持ち、面はGamePanelのhud variantが供給する */
/* Resident HUD overlaid on the world with a face; this file only positions it, GamePanel supplies the face */
.hud {
  position: absolute;
  top: var(--challenge-hud-top);
  left: var(--challenge-hud-left);
  z-index: var(--z-overlay-panel);
  width: var(--challenge-hud-width);
  color: var(--text-high-contrast);
  pointer-events: none;
  text-shadow: var(--challenge-hud-text-shadow);
}
```

- [ ] **Step 5: トークン2件の値を変える**

`src/app/tokens.css` の2行を書き換える:

```css
  --menu-upper-safe-area: 168px;
```

```css
  --challenge-hud-text-shadow: 0.35px 0.35px 0 rgb(0 0 0 / 80%);
```

`--menu-upper-safe-area` のコメントも実態へ合わせる:

```css
  /* メニュー内容を面付き常駐HUDの下からホットバー手前までに収める */
  /* Keep menu content between the faced resident HUD and the hotbar */
```

- [ ] **Step 6: e2eヘルパの見出し取得を testid へ変える**

`e2e/support/challengeHudAssertions.ts` の `hud.evaluate` 内1行を差し替える:

```ts
    const label = element.querySelector<HTMLElement>('[data-testid="challenge-hud-label"]')!;
```

- [ ] **Step 7: HUD側CSSに面が残っていないことを確認する**

Run: `cd /Users/sakastudio/moorestech-worktrees/webui-hud && grep -n "background\|::before\|mask" moorestech_web/webui/src/features/challenge/CurrentChallengeHud.module.css`
Expected: 出力なし（面の供給元は `GamePanel` だけであり、HUD側CSSは位置決めと文字組しか持たない）

- [ ] **Step 8: e2eを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx playwright test --config e2e/playwright.config.ts e2e/tests/challenge/hudLayout.spec.ts e2e/tests/challenge.spec.ts e2e/tests/train.spec.ts e2e/tests/regression/buildMenuLayout.spec.ts`
Expected: PASS（`challengeHudAssertions` を使う3specと、トークン由来の期待値で組まれたbuildMenu specを含めて緑）

- [ ] **Step 9: コミットする**

```bash
git add moorestech_web/webui/src/features/challenge moorestech_web/webui/src/app/tokens.css moorestech_web/webui/src/shared/ui/index.ts moorestech_web/webui/e2e/support/challengeHudAssertions.ts moorestech_web/webui/e2e/tests/challenge/hudLayout.spec.ts
git commit -m "feat(webui): 目標HUDへ面を敷きメニュー上端の安全帯を168pxへ広げる"
```

---

## Task 3: 通知の出入りアニメを入れる

**Files:**
- Modify: `moorestech_web/webui/src/features/notification/notificationStore.ts`
- Modify: `moorestech_web/webui/src/features/notification/notificationStore.test.ts`
- Modify: `moorestech_web/webui/src/features/notification/NotificationHost.tsx`
- Modify: `moorestech_web/webui/src/features/notification/style.module.css`
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Test: `moorestech_web/webui/src/features/notification/notificationAnimationDesign.test.ts`

**Interfaces:**
- Consumes: 既存 `useNotificationStore`（`notifications` / `addNotification`）
- Produces:
  - `export const NOTIFICATION_DISPLAY_MS = 7000`（`notificationStore.ts`）
  - CSS変数 `--notification-enter-duration` / `--notification-exit-duration` / `--notification-shift`
  - インラインCSS変数 `--notification-lifetime`（`.host` へ付与し子へ継承させる）
  - DOM契約 `<div data-testid="notification-row" data-category="…">`

- [ ] **Step 1: 失敗する単体テストを書く**

`src/features/notification/notificationStore.test.ts` の1本目を差し替え、末尾へ1本足す:

```ts
  it("追加され7秒後に消える", () => {
    useNotificationStore.getState().addNotification({
      category: "achievement",
      messageId: "achievement.researchCompleted",
      messageParams: ["Iron"],
      itemId: null,
    });
    expect(useNotificationStore.getState().notifications).toHaveLength(1);
    vi.advanceTimersByTime(NOTIFICATION_DISPLAY_MS - 1);
    expect(useNotificationStore.getState().notifications).toHaveLength(1);
    vi.advanceTimersByTime(1);
    expect(useNotificationStore.getState().notifications).toHaveLength(0);
  });

  it("退場用の状態を持たない", () => {
    useNotificationStore.getState().addNotification({
      category: "achievement",
      messageId: "achievement.unlockedItem",
      messageParams: [],
      itemId: null,
    });
    expect(Object.keys(useNotificationStore.getState().notifications[0]).sort())
      .toEqual(["category", "id", "itemId", "messageId", "messageParams"]);
  });
```

import 行も差し替える:

```ts
import { NOTIFICATION_DISPLAY_MS, useNotificationStore } from "./notificationStore";
```

`src/features/notification/notificationAnimationDesign.test.ts` を新規作成:

```ts
// 通知の出入りアニメが生存尺を単一の正から引き、CSSへ尺を直書きしないことを固定する
// Locks the notification enter/exit animation to a single lifetime source with no duration hardcoded in CSS
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const host = read("./NotificationHost.tsx");
const store = read("./notificationStore.ts");
const style = read("./style.module.css");
const tokens = read("../../app/tokens.css");

describe("notification enter/exit animation", () => {
  it("生存尺はstoreが唯一の正で、CSSへ尺を直書きしない", () => {
    expect(store).toContain("export const NOTIFICATION_DISPLAY_MS = 7000");
    expect(host).toContain('"--notification-lifetime": `${NOTIFICATION_DISPLAY_MS}ms`');
    expect(style).not.toMatch(/7000ms|7s\b/);
  });

  it("入場と退場の2本を持ち、退場は生存尺から逆算した遅延で始まる", () => {
    expect(style).toContain("animation:");
    expect(style).toContain("notificationEnter var(--notification-enter-duration)");
    expect(style).toContain("calc(var(--notification-lifetime) - var(--notification-exit-duration))");
  });

  it("退場のfill-modeはforwardsで入場を巻き戻さない", () => {
    // bothにすると遅延中に退場のfrom状態が前方適用され、入場アニメが消える
    // Using both would back-fill the exit's from state during the delay and erase the enter animation
    expect(style).toMatch(/notificationExit[^;]*forwards/);
    expect(style).not.toMatch(/notificationExit[^;]*\bboth\b/);
  });

  it("移動量と尺はトークンで管理する", () => {
    expect(tokens).toContain("--notification-enter-duration: 160ms");
    expect(tokens).toContain("--notification-exit-duration: 200ms");
    expect(tokens).toContain("--notification-shift: 12px");
  });

  it("同時表示数の上限を持たない", () => {
    expect(store).not.toMatch(/slice\(|MAX_|limit/);
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
```

- [ ] **Step 2: 単体テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/notification`
Expected: FAIL（`NOTIFICATION_DISPLAY_MS` が未export、CSSにアニメーション未定義）

- [ ] **Step 3: storeの生存尺を7秒にして公開する**

`src/features/notification/notificationStore.ts` の該当2箇所を差し替える:

```ts
// 生存尺は7秒。出入りアニメの尺はこの内側に含める（CSSへはHostが変数で渡す）
// The lifetime is 7s and contains the enter/exit animation; the host passes it to CSS as a variable
export const NOTIFICATION_DISPLAY_MS = 7000;
```

```ts
    // 生存尺の経過で削除する。退場アニメはこの尺から逆算してCSS側が描く
    // Remove it when the lifetime elapses; CSS derives the exit animation from that same lifetime
    setTimeout(() => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })), NOTIFICATION_DISPLAY_MS);
```

- [ ] **Step 4: Hostが生存尺をCSSへ渡す**

`src/features/notification/NotificationHost.tsx` の import と `return` を差し替える:

```tsx
import type { CSSProperties } from "react";
import { NOTIFICATION_DISPLAY_MS, useNotificationStore } from "./notificationStore";
```

```tsx
  // 生存尺はstoreの定数が正。CSS変数として流し込み、退場の遅延をCSS側で逆算させる
  // The store constant is the single lifetime source; CSS receives it and derives the exit delay
  const lifetimeStyle = { "--notification-lifetime": `${NOTIFICATION_DISPLAY_MS}ms` } as CSSProperties;

  return (
    <div className={styles.host} style={lifetimeStyle} data-testid="notification-host">
      {notifications.map((n) => (
        // categoryはdata属性で表し、色分けはCSSトークンに委ねる
        // Category goes into a data attribute; token-based CSS handles the coloring
        <div key={n.id} className={styles.notification} data-testid="notification-row" data-category={n.category}>
          {n.itemId != null && <ItemIcon itemId={n.itemId} className={styles.icon} />}
          {t(
            resolveNotificationKey(n.messageId),
            buildInterpolationValues(n.messageId, resolveNotificationParams(n.messageId, n.messageParams, t)),
          )}
        </div>
      ))}
    </div>
  );
```

- [ ] **Step 5: アニメーショントークンを追加する**

`src/app/tokens.css` の `--notification-max-width: 20vw;` の直後へ挿入:

```css
  /* 通知の出入り。左から固定長で滑り込み、退場は生存尺から逆算した遅延で逆再生する */
  /* Notification enter/exit: slides in from the left by a fixed length; the exit mirrors it after a lifetime-derived delay */
  --notification-enter-duration: 160ms;
  --notification-exit-duration: 200ms;
  --notification-shift: 12px;
```

- [ ] **Step 6: 出入りアニメをCSSへ書く**

`src/features/notification/style.module.css` の `.notification` の宣言末尾（`text-shadow` の次の行）へ追加:

```css
  /* 入場は即時、退場は生存尺の終端に合わせる。退場をbothにすると遅延中に前方適用され入場が消える */
  /* The enter runs immediately and the exit lands at the end of the lifetime; both-fill would back-fill the exit and erase the enter */
  animation:
    notificationEnter var(--notification-enter-duration) ease-out both,
    notificationExit var(--notification-exit-duration) ease-in calc(var(--notification-lifetime) - var(--notification-exit-duration)) forwards;
```

ファイル末尾へ keyframes を追加:

```css
@keyframes notificationEnter {
  from {
    opacity: 0;
    transform: translateX(calc(-1 * var(--notification-shift)));
  }

  to {
    opacity: 1;
    transform: translateX(0);
  }
}

@keyframes notificationExit {
  from {
    opacity: 1;
    transform: translateX(0);
  }

  to {
    opacity: 0;
    transform: translateX(calc(-1 * var(--notification-shift)));
  }
}
```

- [ ] **Step 7: 単体テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/notification`
Expected: PASS（`notificationStore` 3件・`notificationMessages` 既存件数・`notificationAnimationDesign` 5件）

- [ ] **Step 8: 全単体テストとlintを確認してコミットする**

Run: `cd moorestech_web/webui && npm run test && npm run lint`
Expected: PASS

```bash
git add moorestech_web/webui/src/features/notification moorestech_web/webui/src/app/tokens.css
git commit -m "feat(webui): 通知の出入りアニメを追加し生存尺を7秒へ延ばす"
```

---

## Task 4: 通知アニメのe2e契約を張る

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`
- Test: `moorestech_web/webui/e2e/tests/notification/animation.spec.ts`

**Interfaces:**
- Consumes: Task 3 の `data-testid="notification-row"`、トークン値（160ms / 200ms / 12px）、生存尺7000ms
- Produces: `TopicScenario` に `notificationClear` が加わる（他specの通知汚染を消すため）

- [ ] **Step 1: 失敗するe2eテストを書く**

`e2e/tests/notification/animation.spec.ts` を新規作成:

```ts
import { test, expect } from "@playwright/test";
import { setTopicScenario, setUiState } from "../../support/mockControl";

test.afterEach(async ({ page }) => {
  // 通知トピックは値が残るため、他specへ漏らさないよう空へ戻す
  // The notification topic is sticky, so reset it to empty and keep other specs clean
  await setTopicScenario(page, "notificationClear");
});

test("通知は左からのスライドとフェードで入場し生存尺の終端で退場する", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  await setTopicScenario(page, "notificationAchievement");

  const row = page.getByTestId("notification-row").first();
  await expect(row).toBeVisible();

  // 入場・退場の2本が載り、退場だけが生存尺から逆算した遅延を持つ
  // Two animations are attached and only the exit carries the lifetime-derived delay
  const animation = await row.evaluate((element) => {
    const style = getComputedStyle(element);
    return {
      name: style.animationName,
      duration: style.animationDuration,
      delay: style.animationDelay,
      fillMode: style.animationFillMode,
      timingFunction: style.animationTimingFunction,
    };
  });
  expect(animation.name).toMatch(/notificationEnter/);
  expect(animation.name).toMatch(/notificationExit/);
  expect(animation.duration).toBe("0.16s, 0.2s");
  expect(animation.delay).toBe("0s, 6.8s");
  expect(animation.fillMode).toBe("both, forwards");
  expect(animation.timingFunction).toBe("ease-out, ease-in");

  // 入場完了後は不透明・移動量ゼロへ落ち着く
  // After the enter finishes it settles at full opacity with no offset
  await expect.poll(async () => row.evaluate((element) => getComputedStyle(element).opacity)).toBe("1");
  const settled = await row.evaluate((element) => getComputedStyle(element).transform);
  expect(settled === "none" || settled === "matrix(1, 0, 0, 1, 0, 0)").toBe(true);

  // 生存尺の経過でDOMから消える（退場アニメの終端と一致する）
  // It leaves the DOM when the lifetime elapses, matching the end of the exit animation
  await expect(page.getByTestId("notification-row")).toHaveCount(0, { timeout: 9000 });
});
```

- [ ] **Step 2: e2eを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx playwright test --config e2e/playwright.config.ts e2e/tests/notification/animation.spec.ts`
Expected: FAIL（`notificationClear` が `TopicScenario` に無く型エラー、または mock control が 400 を返す）

- [ ] **Step 3: notificationClear シナリオを足す**

`e2e/mock-host/topics/topicControls.ts` の `notificationDenied` の次の行へ追加:

```ts
  // 通知トピックは値が残るため、specの後片付け用に空値へ戻す口を用意する
  // The notification topic is sticky, so expose an empty reset for spec teardown
  notificationClear: () => control(Topics.notification, {}),
```

- [ ] **Step 4: e2eを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx playwright test --config e2e/playwright.config.ts e2e/tests/notification/animation.spec.ts`
Expected: PASS（1件）

- [ ] **Step 5: e2e全体が緑であることを確認する**

Run: `cd moorestech_web/webui && npm run test:e2e`
Expected: PASS（既存specの失敗0件。落ちた場合は本planの変更（面の追加・安全帯168px・通知の尺）に由来するか切り分け、契約側を更新する）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_web/webui/e2e/mock-host/topics/topicControls.ts moorestech_web/webui/e2e/tests/notification
git commit -m "test(webui): 通知の出入りアニメのe2e契約を張る"
```

---

## Task 5: デザイン哲学（webui-design SKILL）を改訂する

**Files:**
- Modify: `.claude/skills/webui-design/SKILL.md`（`.agents/skills/webui-design/SKILL.md` がgit正本でありシンボリックリンク経由で同一実体）

**Interfaces:**
- Consumes: Task 1〜4 で確定した実装（トークン名・variant名・尺）
- Produces: ホワイトリスト本文（後続のレビュー・実装が参照する唯一の様式定義）

- [ ] **Step 1: §1 の常時表示HUD条項を書き換える**

現行の最終箇条:

> - 常時表示HUD（ホットバー・クロスヘア・キーヒント等）は例外的にパネル外だが、これも「浮いている」表現であること。面で塗らない。

を次へ差し替える:

```markdown
- 常時表示HUD（ホットバー・クロスヘア・キーヒント等）は例外的にパネル外で、原則として「浮いている」表現とし面で塗らない。
  - **唯一の例外は目標HUD（チャレンジHUD・§8.14）**。面が必要な場合も独自CSSで面を作らず、`GamePanel variant="hud"` から供給する（面色 `--hud-panel-face`・4辺フェード `--hud-panel-edge-fade`・安全帯 `--hud-panel-padding`）。他のHUDへ面を広げるのは都度裁定。
```

- [ ] **Step 2: §2 の GamePanel variant 一覧へ hud を足す**

`variant="craft"` の箇条の直後へ追加:

```markdown
  - `variant="hud"`: 面と4辺の境界フェードだけを持つ常時表示HUD用バリアント。タイトル罫線・下向き三角・右下グリップ・正本合わせの実測オフセットを持たない。余白は `--hud-panel-padding`（全辺、フェード幅を超える安全帯）。
```

- [ ] **Step 3: §6 の装飾アニメーション条項を書き換える**

現行:

> - 装飾アニメーションは基本入れない。トランジションを入れる場合もe2eが同期検証できること（モーダルは duration 0）。

を次へ差し替える:

```markdown
- 装飾アニメーションは基本入れない。トランジションを入れる場合もe2eが同期検証できること（モーダルは duration 0）。
  - **例外は通知の出入り（§8）だけ**。入場＝左から `--notification-shift` のスライド＋フェード、退場＝その逆再生で、色相・形・光彩は動かさない。
  - アニメーションを足す場合、テスト時に尺をゼロへ落とす抜け道は作らない（実挙動と乖離するため）。計算値の `animation-name` はCSS Modulesがハッシュ化するので、e2eでは部分一致で照合する。
```

- [ ] **Step 4: §8 の NotificationHost 条項へ出入りアニメを追記する**

`Mantine `Notification` コンポーネントは使わない。` の直後へ追加:

```markdown
- **NotificationHostの出入りは唯一の装飾アニメーション例外**（§6）。入場は `--notification-enter-duration`（160ms・ease-out）で左から `--notification-shift`（12px）のスライドイン＋フェードイン、退場は `--notification-exit-duration`（200ms・ease-in）でその逆再生。生存尺は store の `NOTIFICATION_DISPLAY_MS`（7000ms）が単一の正で、`NotificationHost` がインラインCSS変数 `--notification-lifetime` として渡し、CSSは退場遅延を `calc(生存尺 − 退場尺)` で逆算する。**退場のためにstoreへ状態（`exiting` 等）を持たせない。** 退場の `animation-fill-mode` は `forwards`（`both` にすると遅延中に前方適用されて入場が消える）。積み替えの移動は補間せず、同時表示数の上限も設けない。
```

- [ ] **Step 5: §8.14 のチャレンジHUD条項を書き換える**

現行の1・2箇条目:

> - 常時表示HUD族として、面・枠・角丸を持たず、`.viewportOverlay` の左上安全帯に浮かせる。

を次へ差し替える:

```markdown
- 常時表示HUD族の中で唯一**面を持つ**（§1の例外）。面は `GamePanel variant="hud"` が供給し、枠・角丸は持たない。位置決め（`.viewportOverlay` 左上・`--challenge-hud-*`）はHUD側CSSが持ち、面表現はHUD側に書かない。
- 面の外形は実viewport左上24pxに据え、文字は `--hud-panel-padding` の安全帯で内側へ寄せる（画面端から約44px）。面幅は `--challenge-hud-width`（520px）固定で、目標文が短くても縮めない。
```

そして `アイコン、ゲージ、箇条書き装飾、光彩、アニメーションは追加しない。` の後へ追加:

```markdown
- 文字影 `--challenge-hud-text-shadow` は面付き後も残し、通知同族の控えめな値（0.35px級）にする。可読性の主担当は面で、影は世界が透ける面上の補助。
- メニュー上端の安全帯 `--menu-upper-safe-area`（168px）は、目標3件までの面付きHUDが収まる高さとして決めている。HUDの寸法・目標行数の上限を変えるときはこのトークンを一緒に見直す。
```

- [ ] **Step 6: 矛盾が残っていないか検索して確認する**

Run: `cd /Users/sakastudio/moorestech-worktrees/webui-hud && grep -n "面で塗らない\|装飾アニメーション\|面・枠・角丸" .claude/skills/webui-design/SKILL.md`
Expected: §1・§6・§8.14 の該当行がすべて例外つきの新文言になっており、「面で塗らない」「アニメーションは追加しない」が例外の記述なしで残っていないこと

- [ ] **Step 7: コミットする**

```bash
git add .claude/skills/webui-design/SKILL.md
git commit -m "docs(webui-design): 目標HUDの面と通知の出入りアニメを様式へ追記する"
```

---

## Task 6: 目視QA（mockホストのスクリーンショット）

**Files:**
- 変更なし（撮影と確認のみ。必要な修正が出た場合は該当タスクのファイルへ戻す）

**Interfaces:**
- Consumes: Task 1〜4 の実装
- Produces: 目視確認の結果（4辺の見え方・重なり・中央対称の判定）

- [ ] **Step 1: 専用ハーネスで目標HUDの22ケースを撮影する**

目標HUD専用の視覚QAハーネスが既にある（`e2e/challenge-hud/capture.ts` + `cases.ts`。単一/複数/長文/明暗背景/各メニュー/スキット/操作モードの22ケースを固定順で撮り、`metrics.json` と `manifest.json` も出す）。新しい撮影スクリプトは作らずこれを使う。

Run: `cd moorestech_web/webui && CHALLENGE_CAPTURE_OUT=/tmp/challenge-hud-face-qa npx tsx e2e/challenge-hud/capture.ts`
Expected: `/tmp/challenge-hud-face-qa/` に `01-single-world.png` 〜 `22-pause-menu-long.png` と `metrics.json` / `manifest.json` が出力される

- [ ] **Step 2: 4辺の見え方を拡大クロップで確認する**

チェック項目（`webui-design` §10 の1〜4）:
- `01`/`02`/`03`: 文字がフェード帯に載って「はみ出て」見えないか。逆に内容の直後で面が途切れて「切れて」見えないか（4辺すべて。特に右端と下端）
- `02-single-bright` / `03-single-dark`: 明背景・暗背景の両方で文字が読めるか（弱めた文字影で足りているか）
- `07-inventory` / `14-multiple-long-inventory-visible` / `19`〜`22`: 面の下端がメニューパネルの面へかかっていないか。**目標が折り返す長文ケース（`05`/`06`/`14`/`19`〜`22`）は面の高さが168pxを超えてメニューへかかる**。これは面の無い現行でも同様に起きている既存の性質であり、本planでは回帰扱いにしない（単行3件までが安全帯の設計根拠）。面が2枚重なった見え方が許容範囲かだけを目視で判断する
- `01`: 罫線（176px）が面の内側で正しく短く見えるか
- `09-empty`: 目標0件でHUD（面ごと）が消えているか
- `11-blocking-hidden`: blockingスキット中にHUDが面ごと消えているか

- [ ] **Step 3: 通知の出入りを録画で確認する**

Run: `cd moorestech_web/webui && npx playwright test --config e2e/playwright.config.ts e2e/tests/notification/animation.spec.ts --headed`
Expected: 左から滑り込んで現れ、約6.8秒後に左へ抜けながら消えることを目視で確認できる

- [ ] **Step 4: 破綻があれば該当タスクのファイルを直し、無ければ次へ進む**

修正した場合はそのタスクのテストを再実行して緑を確認し、`git add <直したパス>` して `fix(webui): …` でコミットする。

---

## Task 7: 全ブランチのコードレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、`feature/webui-screen-edge-and-list-polish` の変更全体（本planの全コミット）をレビューする。ゴール文言による省略は不可。

- [ ] **Step 2: 指摘を反映する**

機械的修正は適用し、設計判断を要する指摘はユーザーへ諮る。反映後に `npm run test` / `npm run lint` / `npm run test:e2e` を再実行して緑を確認する。

- [ ] **Step 3: コミットする**

```bash
git add <レビューで直したパス>
git commit -m "fix(webui): コードレビュー指摘を反映する"
```

---

## 配置と前例

- **面の供給元を `GamePanel` へ集約**: `webui-design` §2「GamePanel の外で独自CSSのパネル面を作るのは禁止。新しい見た目は variant を追加してから使う」に従う。前例は `variant="skit"`（`style.module.css` の `.skit` / `.skit::before`）で、面＋片端フェード＋`> *` の重なり順という同形の構造を持つ。
- **HUD側は位置決めだけを持つ**: 前例は `HotbarPanel` / `EquipmentPanel`（HUD自身の固定長トークンで位置と寸法だけを決める）。§8.16 の「機能側の固定配置・独自z-index・パネル面は禁止、面表現は GamePanel が一元供給」と同じ責務分割。
- **寸法トークンの局所上書き**: `--hud-panel-padding` / `--hud-panel-edge-fade` は共有側に既定値を置き、必要なら利用側で上書きする形。前例は `--icon-button-size`（§8.6）・`--mode-switch-option-height`（§8.11）。
- **生存尺の単一正 + インラインCSS変数**: JS定数をCSSへ流す前例は `TreeView` のビューポート系（`shared/treeView`）と skit の `--skit-*` 群の使い方に倣い、CSSへ数値を二重定義しない方針を維持する。
- **デザインテストの形**: ソーステキストを `readFileSync` で読んで様式を固定する前例は `src/features/blockInventory/blockInventoryDesign.test.ts`。vitest は node 環境で `*.test.ts` のみを拾う（`vitest.config.ts`）ため、React 描画に依存しない検査に留める。
- **新規パターン（レビュー注目点）**: 常時表示HUDが面を持つのは初。§1 の原則を例外つきに改訂して許可する形を取っており、他HUDへの横展開は都度裁定とする。

## 機能死活表（変更の巻き添え検査）

| 操作・表示 | plan後も生きるか | 根拠 |
|---|---|---|
| 目標HUDの表示（全画面共通・受信順の縦積み・長文折返し） | 生きる | DOM構造は面の階層が1枚増えるだけ。`.objectives` / `.objective` のCSSは変更しない |
| blockingスキット中の目標HUD非表示 | 生きる | `useBlockingSkitActive()` による早期 return を変更しない |
| チュートリアルの目標HUDアンカー（`challengeCurrentHud`） | 生きる | `tutorialAnchor` は外側 `<section>` に付いたまま。吹き出しの基準矩形は面の外形と一致する |
| 目標HUDのメニュー中表示（インベントリ・研究・建築・ポーズ） | 生きる | 画面状態を参照しない単一レイアウトを維持。安全帯を168pxへ広げて単行3件までの重なりを避ける（**折り返す長文目標では面がメニューへかかる。面の無い現行でも同じで、回帰ではない**） |
| インベントリ・クラフト・レシピ・研究・建築メニューの表示 | 生きる（**位置が40px下がる**） | `--menu-upper-safe-area` の変更による意図的な移動（ユーザー裁定）。`buildMenuLayout.spec.ts` はトークン由来の期待値なので緑のまま |
| uGUI正本パリティ（`parity_targets.py` の bbox・色ピック） | **意図的に破棄** | uGUI移植終了の裁定により追従しない。撤去作業は別issue `moorestech-5lb` |
| 通知の表示（achievement / operationDenied の色分け・`ItemIcon`・折返し・最大幅20vw） | 生きる | `.notification` の既存宣言は変更せず、`animation` を追記するだけ |
| 通知の自動消滅 | 生きる（5秒→**7秒**） | `NOTIFICATION_DISPLAY_MS` の変更による意図的な延長（ユーザー裁定） |
| `ToastHost` の表示（右下・3秒） | 生きる（無変更） | 本planの対象外 |
| `notification.events` のseq重複防止 | 生きる | `lastSeq` の判定を変更しない |

## 判断記録（ADR）

設計セッションのADR:
- `docs/adr/0015-webui-hud-face-variant.md` — 常時表示HUDへ面を許可し `GamePanel` の hud variant で供給する
- `docs/adr/0016-webui-notification-enter-exit-animation.md` — 通知の出入りにアニメーションを許可し生存尺から逆算して駆動する

ユーザー裁定（`.decisions/`、いずれも2026-08-17）:
- `目標HUDの面色は既存の半透明ネイビーを流用する`
- `HUDの面はGamePanelのhud-variantで供給する`
- `目標HUDの面は固定520px幅で敷く`
- `目標HUDの面は現位置に置き文字を内側へ寄せる`
- `面付き目標HUDの文字影は通知同族へ弱める`
- `通知の出入りアニメはNotificationHostだけに入れる`
- `通知の出入りは左からのスライドとフェードにする`
- `通知の退場は生存尺から逆算し退場状態を持たせない`
- `通知の生存尺は7秒へ延ばし出入りを内側に含める`
- `通知の同時表示数に上限は設けない`
- `面付きHUDに合わせメニュー上端の安全帯を168pxへ広げる`
- `uGUI移植を終了しWeb-UIは独自調整へ移る`
- `目標HUD背景と通知アニメは進行中のwebuiブランチへ相乗りする`

planning中に生じた判断:
- **タスク分割**: e2e契約の更新を独立タスクにせず、面付き化（Task 2）と通知アニメ（Task 3→4）の成果物へ畳んだ。契約だけを直したコミットは単体でレビューの意味を持たないため。出所: agent前提
- **`--menu-upper-safe-area` の168px化を Task 2 に含める**: 面の高さが決まってから安全帯を決めないと値が宙に浮くため、同一タスクの成果物とした。出所: agent前提
- **退場の `animation-fill-mode` は `forwards`**: `both` だと遅延中に退場の `from` が前方適用され入場アニメが消える。CSSの仕様に起因する落とし穴なので、デザインテストで固定した。出所: agent前提
- **安全帯168pxの設計根拠は「単行3件まで」**: 折り返す長文目標（`challengeLong` / `challengeMultipleLong`）では面の高さが168pxを超えメニューへかかる。面の無い現行でも同様に超えているため回帰とせず、目視QAで許容範囲だけを判断する。恒久対策（行数上限・省略表示）が必要になった時点で別裁定。出所: agent前提
- **目視QAは既存の専用ハーネスを使う**: `e2e/challenge-hud/capture.ts` の22ケースが単一/複数/長文/明暗背景/各メニュー/スキット/操作モードを既に網羅しているため、新規スクリプトを作らない。出所: agent前提
- **`notificationClear` シナリオの新設**: 通知トピックは値が残るため、specの後片付け口が無いと他specへ通知が漏れる。出所: agent前提
- **`prefers-reduced-motion` は実装しない**: CEF埋め込みで媒体側のOS設定が届かないため。要求が出た時点で再裁定する。出所: agent前提
- **`ToastHost` の様式化は別issue `moorestech-cgm`、uGUI語彙の撤去は別issue `moorestech-5lb`**: いずれも本planのスコープ外。出所: agent前提（uGUI撤去そのものはユーザー裁定、分離の判断がagent前提）
