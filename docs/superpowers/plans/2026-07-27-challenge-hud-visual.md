---
spec: docs/superpowers/specs/2026-07-27-challenge-hud-visual-design.md
---

# Challenge HUD Visual Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** `challenge.current` の内部キーとMantine既定カードを除去し、チャレンジHUDを面のない見出し・罫線・目標一覧へ置き換える。

**Architecture:** `CurrentChallengeHud` は既存topicの受動的な読み手とし、`App` が画面所有に基づく可視性を決める。既存 `FadeRule` と `tokens.css` の固定長トークンを使い、18状態の分割撮影ハーネスで表示と衝突回避を検証する。操作モード画像で見つかったMantine面は、配置・削除HUDを同じ面なし語彙へ直して検証する。

**Tech Stack:** React 18、TypeScript、CSS Modules、Playwright、Vite mock-host

## Global Constraints

- 常時表示HUDはパネル面・枠・角丸を持たず、世界の上に浮く表現にする。
- 見出し、位置、幅、間隔、文字サイズ、文字影は `tokens.css` の既存または `--challenge-hud-*` 固定長トークンだけを使う。
- 新しい色相、アイコン、ゲージ、箇条書き装飾、光彩、アニメーションを追加しない。
- 目標名は合成boldを使わず、見出しとの文字サイズ差だけで階層を作る。
- 複数currentを切り捨てず受信順ですべて描画し、長文と長語を固定幅内で折り返す。
- topic未受信・0件・blockingスキット中はHUDを描画しない。
- 全画面modalとPlaceBlock/DeleteBarではHUDを抑制し、GameScreen、未知・Debug、通常TrainHUD、backgroundスキットでは維持する。
- 画面操作を阻害しないようHUD全体を `pointer-events: none` にする。
- Playwrightで問題を探し、独立subagentがOKを出すまで修正・再撮影・再評価を反復する。

---

## File Structure

- Modify: `.agents/skills/webui-design/SKILL.md` — チャレンジHUDと `FadeRule` の許可パターンをホワイトリストへ追加する。
- Modify: `.claude/skills/webui-design/SKILL.md` — 実行環境が参照する同内容のデザイン哲学を同期する。
- Modify: `.codex/skills/webui-design/SKILL.md` — Codexが参照する同内容のデザイン哲学を同期する。
- Modify: `moorestech_web/webui/src/app/tokens.css` — HUDの固定長寸法と文字影トークンを定義する。
- Modify: `moorestech_web/webui/src/shared/ui/FadeRule/index.tsx` — 面なしHUDの見出し区切りにも使う責務をコメントへ反映する。
- Modify: `moorestech_web/webui/src/features/challenge/CurrentChallengeHud.tsx` — Mantine表示部品をsemantic DOMへ置換する。
- Create: `moorestech_web/webui/src/features/challenge/CurrentChallengeHud.module.css` — 常駐HUD固有の面なしレイアウトだけを所有する。
- Modify: `moorestech_web/webui/src/features/challenge/style.module.css` — 移動済み `.hud` を削除する。
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/presentationFixtures.ts` — 日本語単一・複数・長文の決定的topic fixtureを提供する。
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts` — fixtureを切り替えるテスト専用scenarioを公開する。
- Modify: `moorestech_web/webui/e2e/tests/challenge.spec.ts` — 表示構造、折返し、空、blocking、入力透過を検証する。
- Create: `moorestech_web/webui/e2e/challenge-hud/` — 全視覚状態のスクリーンショット・計測JSON・manifestを責務別スクリプトで生成する。

### Task 1: デザインホワイトリストと失敗するPlaywright契約

**Files:**
- Modify: `.agents/skills/webui-design/SKILL.md`
- Modify: `.claude/skills/webui-design/SKILL.md`
- Modify: `.codex/skills/webui-design/SKILL.md`
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/presentationFixtures.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`
- Modify: `moorestech_web/webui/e2e/tests/challenge.spec.ts`

**Interfaces:**
- Consumes: `Topics.challengeCurrent`、`setTopicScenario(page, scenario)`、`setSkitStage(page, stage)`
- Produces: `challengeJapanese`、`challengeMultiple`、`challengeLong`、`challengeMultipleLong` の `TopicScenario`

- [ ] **Step 1: `webui-design` の §8.14 に許可する見た目を追記する**

```markdown
## 8.14 チャレンジHUD

- 常時表示HUD族として、面・枠・角丸を持たず、左上に浮かせる。
- 構成は「`--text-muted` の従属見出し → `FadeRule` → `--text-high-contrast` の目標一覧」だけとする。
- 位置・幅・間隔・文字サイズ・文字影は `--challenge-hud-*` 固定長トークンで管理する。
- 複数目標は受信順で縦積みし、長文・長語を固定幅内で折り返す。
- アイコン、ゲージ、箇条書き装飾、光彩、アニメーションは追加しない。
- `pointer-events: none` を維持し、blockingスキット中は表示しない。
```

- [ ] **Step 2: 単一・複数・長文fixtureとscenarioを追加する**

```ts
export const challengeJapanese = {
  challenges: [{ guid: "ch-jp", title: "石を採掘する", categoryGuid: "cat-1" }],
};
export const challengeMultiple = {
  challenges: [
    { guid: "ch-a", title: "石を採掘する", categoryGuid: "cat-1" },
    { guid: "ch-b", title: "石器をクラフトする", categoryGuid: "cat-1" },
    { guid: "ch-c", title: "木を伐採して拠点へ運ぶ", categoryGuid: "cat-2" },
  ],
};
export const challengeLong = {
  challenges: [{
    guid: "ch-long",
    title: "VeryLongUnbrokenChallengeObjectiveTextThatMustWrapInsideTheHudWithoutOverflowing",
    categoryGuid: "cat-1",
  }],
};
export const challengeMultipleLong = {
  challenges: [
    { guid: "ch-ml-a", title: "地下深くにある非常に長い名前の鉱床を見つけて必要な石を採掘する", categoryGuid: "cat-1" },
    { guid: "ch-ml-b", title: "遠方の森林から建築に必要な木材を伐採して拠点まで運搬する", categoryGuid: "cat-2" },
    { guid: "ch-ml-c", title: "VeryLongUnbrokenSecondaryObjectiveTextThatMustAlsoWrapInsideTheHud", categoryGuid: "cat-3" },
  ],
};
```

`topicControls.ts` の `controls` にfixtureと同名の分岐を追加し、`TopicScenario` の型へ自動的に含める。

```ts
challengeJapanese: () => control(Topics.challengeCurrent, clone(fx.challengeJapanese)),
challengeMultiple: () => control(Topics.challengeCurrent, clone(fx.challengeMultiple)),
challengeLong: () => control(Topics.challengeCurrent, clone(fx.challengeLong)),
challengeMultipleLong: () => control(Topics.challengeCurrent, clone(fx.challengeMultipleLong)),
```

- [ ] **Step 3: 現行実装で失敗するPlaywrightテストを書く**

```ts
test("進行中チャレンジを内部キーやカード面なしで表示する", async ({ page }) => {
  await setTopicScenario(page, "challengeJapanese");
  await page.goto("/");
  const hud = page.getByTestId("challenge-hud");
  await expect(hud).toContainText("現在のチャレンジ");
  await expect(hud).toContainText("石を採掘する");
  await expect(hud).not.toContainText("challenge.current");
  await expect(hud).toHaveCSS("pointer-events", "none");
  await expect(hud).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
});

test("複数目標を受信順で表示し長文をHUD幅内へ折り返す", async ({ page }) => {
  await setTopicScenario(page, "challengeMultiple");
  await page.goto("/");
  await expect(page.getByTestId("challenge-objective")).toHaveText([
    "石を採掘する",
    "石器をクラフトする",
    "木を伐採して拠点へ運ぶ",
  ]);
  await setTopicScenario(page, "challengeLong");
  const objective = page.getByTestId("challenge-objective");
  await expect(objective).toHaveCount(1);
  await expect(objective).toContainText("VeryLongUnbrokenChallengeObjectiveText");
  const layout = await objective.evaluate((element) => {
    const style = getComputedStyle(element);
    return {
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
      clientHeight: element.clientHeight,
      lineHeight: Number.parseFloat(style.lineHeight),
    };
  });
  expect(layout.scrollWidth).toBeLessThanOrEqual(layout.clientWidth);
  expect(layout.clientHeight).toBeGreaterThan(layout.lineHeight);
});

test("blockingスキット中だけ進行中チャレンジを隠す", async ({ page }) => {
  await setTopicScenario(page, "challengeJapanese");
  await setSkitStage(page, "none");
  await page.goto("/");
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
  await setSkitStage(page, "text");
  await expect(page.getByTestId("challenge-hud")).toBeHidden();
  await setSkitStage(page, "none");
  await expect(page.getByTestId("challenge-hud")).toBeVisible();
});
```

- [ ] **Step 4: 対象E2Eを実行し、現行UIで失敗することを確認する**

Run: `cd moorestech_web/webui && pnpm test:e2e -- challenge.spec.ts`

Expected: `現在のチャレンジ`、透明面、複数scenarioのいずれかでFAIL。

- [ ] **Step 5: テストとデザイン哲学をコミットする**

```bash
git add .agents/skills/webui-design/SKILL.md .claude/skills/webui-design/SKILL.md .codex/skills/webui-design/SKILL.md moorestech_web/webui/e2e
git commit -m "チャレンジHUDの表示契約を追加"
```

### Task 2: 面なしチャレンジHUDの実装

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Modify: `moorestech_web/webui/src/shared/ui/FadeRule/index.tsx`
- Modify: `moorestech_web/webui/src/features/challenge/CurrentChallengeHud.tsx`
- Create: `moorestech_web/webui/src/features/challenge/CurrentChallengeHud.module.css`
- Modify: `moorestech_web/webui/src/features/challenge/style.module.css`
- Test: `moorestech_web/webui/e2e/tests/challenge.spec.ts`

**Interfaces:**
- Consumes: `FadeRule()`、`useTopic(Topics.challengeCurrent)`、`useTopicSelector(Topics.skitPresentation, selector)`
- Produces: `CurrentChallengeHud()` の `data-testid="challenge-hud"` と各 `data-testid="challenge-objective"`

- [ ] **Step 1: `tokens.css` に固定長トークンを追加する**

```css
--challenge-hud-left: 24px;
--challenge-hud-top: 24px;
--challenge-hud-width: 288px;
--challenge-hud-label-font-size: 12px;
--challenge-hud-objective-font-size: 17px;
--challenge-hud-label-rule-gap: 5px;
--challenge-hud-rule-list-gap: 8px;
--challenge-hud-objective-gap: 5px;
--challenge-hud-letter-spacing: 1px;
--challenge-hud-objective-line-height: 25px;
--challenge-hud-text-shadow: 0 1px 2px rgb(0 0 0 / 85%);
```

- [ ] **Step 2: HUDをsemantic DOMと共有 `FadeRule` へ置換する**

```tsx
import { FadeRule } from "@/shared/ui";
import styles from "./CurrentChallengeHud.module.css";

const label = t("現在のチャレンジ");
return (
  <section className={styles.hud} aria-label={label} data-testid="challenge-hud"
    {...tutorialAnchor(TutorialAnchorIds.challengeCurrentHud)}>
    <div className={styles.label}>{label}</div>
    <FadeRule />
    <div className={styles.objectives}>
      {current.challenges.map((challenge) => (
        <div key={challenge.guid} className={styles.objective} data-testid="challenge-objective">
          {challenge.title}
        </div>
      ))}
    </div>
  </section>
);
```

- [ ] **Step 3: 専用CSSで面なし・固定幅・折返しを実装する**

```css
.hud {
  position: fixed;
  top: var(--challenge-hud-top);
  left: var(--challenge-hud-left);
  z-index: var(--z-overlay-panel);
  width: var(--challenge-hud-width);
  color: var(--text-high-contrast);
  pointer-events: none;
  text-shadow: var(--challenge-hud-text-shadow);
}

.label {
  margin-bottom: var(--challenge-hud-label-rule-gap);
  color: var(--text-muted);
  font-size: var(--challenge-hud-label-font-size);
  letter-spacing: var(--challenge-hud-letter-spacing);
}

.objectives {
  display: flex;
  flex-direction: column;
  gap: var(--challenge-hud-objective-gap);
  margin-top: var(--challenge-hud-rule-list-gap);
}

.objective {
  overflow-wrap: anywhere;
  font-size: var(--challenge-hud-objective-font-size);
  line-height: var(--challenge-hud-objective-line-height);
}
```

- [ ] **Step 4: 対象E2Eを実行して通過を確認する**

Run: `cd moorestech_web/webui && pnpm test:e2e -- challenge.spec.ts`

Expected: PASS。単一、複数、長文、空、blockingがすべて通る。

- [ ] **Step 5: lint・unit test・buildを実行する**

Run: `cd moorestech_web/webui && pnpm lint && pnpm test && pnpm build`

Expected: すべてexit 0。

- [ ] **Step 6: 実装をコミットする**

```bash
git add moorestech_web/webui/src
git commit -m "チャレンジHUDを面なし表示へ変更"
```

### Task 3: Playwright目視QAとsubagent合格ループ

**Files:**
- Inspect: `moorestech_web/webui/e2e/mock-host/httpHandler.ts`
- Inspect: `moorestech_web/webui/e2e/capture-eval.ts`
- Create: `moorestech_web/webui/e2e/challenge-hud/capture.ts`
- Create: `moorestech_web/webui/e2e/challenge-hud/cases.ts`
- Create: `moorestech_web/webui/e2e/challenge-hud/pageCapture.ts`
- Create: `moorestech_web/webui/e2e/challenge-hud/serverLifecycle.ts`
- Inspect: `/tmp/challenge-hud-visual-qa/*.png`
- Modify if findings exist: `moorestech_web/webui/src/features/challenge/CurrentChallengeHud.module.css`
- Modify if findings exist: `moorestech_web/webui/src/app/tokens.css`
- Modify if findings exist: `moorestech_web/webui/e2e/tests/challenge.spec.ts`
- Modify: `moorestech_web/webui/src/features/modeHud/PlacementModeHud.tsx` — 配置情報を面なしのHTML構造へ置換する。
- Modify: `moorestech_web/webui/src/features/modeHud/DeleteModeHud.tsx` — 削除案内と警告を面なしのHTML構造へ置換する。
- Modify: `moorestech_web/webui/src/features/modeHud/style.module.css` — Mantine面を除去し操作HUDトークンだけで構成する。
- Create: `moorestech_web/webui/src/features/modeHud/modeHudDesign.test.ts` — デザインホワイトリストを固定する。
- Create: `moorestech_web/webui/e2e/tests/modeHud/operation-mode-hud.spec.ts` — 実ブラウザで面なし契約を検証する。

**Interfaces:**
- Consumes: mock-host HTTP controls、Playwright Chromium、1280×720 viewport
- Produces: 単一・複数・長文・複数長文・空・背景スキット・blocking・明暗背景・パネル画面のスクリーンショット一式と独立subagentのOK判定

- [ ] **Step 1: 18状態の分割撮影ハーネスを書く**

実装責務:

- `cases.ts`: 18ケース、期待目標、1280×720 viewport、正確なPNG一覧を定義する。
- `pageCapture.ts`: 共通mock制御helperを使い、本文・画面・操作モード・スキットを完全一致で待って撮影・計測する。
- `serverLifecycle.ts`: HTTP listen成功後のWSS構築を支え、既知成果物だけを削除し、全資源の終了を独立試行して最初の失敗を返す。
- `capture.ts`: 18 PNG、`metrics.json`、正確な画像一覧とviewportを持つ`manifest.json`を生成する。

起動失敗は処理済みの非0終了に変換し、browser・WSS・HTTPは正常系と失敗系の双方で閉じる。`CHALLENGE_CAPTURE_PORT`占有時は既存サーバーを壊さず終了する。

- [ ] **Step 2: build後に全18状態を撮影する**

Run: `cd moorestech_web/webui && pnpm build && node --import tsx e2e/challenge-hud/capture.ts`

Expected: `/tmp/challenge-hud-visual-qa/` に18枚のPNG、`metrics.json`、正確な成果物一覧を持つ `manifest.json` を生成し、プロセスがexit 0で終了する。

- [ ] **Step 3: 主担当が全画像を拡大確認して不具合を列挙する**

確認項目:

```text
内部キー露出 / カード面 / 枠 / 角丸 / 左上余白 / 罫線幅
見出しと目標の階層 / 明暗背景上の可読性 / 長文折返し
複数行間隔 / 他HUD・パネルとの重なり / blocking中の残留 / 入力透過
```

- [ ] **Step 4: 独立subagentへspec・全画像・計測JSONを渡して判定させる**

Expected output contract:

```text
VERDICT: OK
```

または、各findingについて画像名、座標、違反したspec項目、修正案を返す。

- [ ] **Step 5: findingが1件でもあれば修正し、Task 2のE2E・全状態撮影・subagent判定を最初から再実行する**

Expected: 独立subagentが `VERDICT: OK` を返すまでループを終了しない。

- [ ] **Step 6: 最終差分と検証結果をコミットする**

```bash
git add moorestech_web/webui
git commit -m "チャレンジHUDの目視QA指摘を修正"
```

変更がなければ空コミットは作らない。

### Task 4: 最終ブランチレビュー

**Files:**
- Inspect: `git diff origin/master...HEAD`
- Inspect: `docs/superpowers/specs/2026-07-27-challenge-hud-visual-design.md`
- Inspect: `docs/superpowers/plans/2026-07-27-challenge-hud-visual.md`

- [ ] **Step 1: 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

Run: moores-code-reviewの全レンズを `origin/master...HEAD` に対して実行する。

Expected: Critical/Warningを修正し、再レビューで問題なし。

- [ ] **Step 2: 最終検証を再実行する**

Run: `cd moorestech_web/webui && pnpm lint && pnpm test && pnpm test:e2e -- challenge.spec.ts && pnpm build`

Expected: すべてexit 0。

- [ ] **Step 3: 作業ツリーがクリーンで全変更がコミット済みか確認する**

Run: `git status --short --branch`

Expected: 未コミット変更なし。

## 判断記録（ADR）

- 対応spec: `docs/superpowers/specs/2026-07-27-challenge-hud-visual-design.md` の「判断記録（ADR）」を正とする。
- ユーザー裁定（発言「playwrgihtチェックでsubagentがok出るまで」2026-07-27）: Playwright全状態の画像と計測値を独立subagentが確認し、OKが出るまで修正ループを継続する。
- agent前提（既存データフロー・拒否権つき）: `challenge.current topic → CurrentChallengeHud` の読み取り経路を維持し、通信・状態所有を変更しない。
- agent前提（テスト分離・拒否権つき）: 視覚QA用の複数・長文状態はmock-host fixtureで決定的に再現し、本番topic契約へテスト都合の項目を追加しない。
- agent前提（画面所有・目視QA結果・拒否権つき）: 全画面modalとPlaceBlock/DeleteBarは画面固有UIが表示を所有して常駐HUDを抑制し、GameScreen、未知・Debug、通常TrainHUD、backgroundスキットではHUDを維持する。
