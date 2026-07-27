---
spec: docs/superpowers/specs/2026-07-27-world-pin-arrow-visual-design.md
---

# 画面外ワールドピン矢印の視認性改善 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 画面外のチュートリアル対象を示すシェブロンを、Playwrightで寸法・形状・画面内収まりを確認できる56pxの軸付き矢印へ変更する。

**Architecture:** Unityから受信する方向ベクトルと既存のクランプ計算は維持し、Web UIのSVGとCSSだけで見た目を変更する。画面端余白はCSS変数を唯一の値源として40pxへ広げ、斜め回転時にも矢印全体をviewport内へ収める。

**Tech Stack:** React 18、TypeScript、CSS Modules、inline SVG、Playwright

## Global Constraints

- SVGは太い軸と三角形の先端を持つ右向きの塗りつぶし矢印とする。
- 表示サイズは `56px × 56px` とする。
- 明るい塗り、暗い輪郭、ドロップシャドウを組み合わせる。
- `--world-pin-edge-margin` は `40px` とし、TypeScript側に数値フォールバックを重複させない。
- Unityの射影・方向計算、wire contract、画面内ワールドピンは変更しない。
- 実装後はPlaywrightの寸法検証とスクリーンショット目視の両方で視認性を確認する。

---

## File Structure

- `moorestech_web/webui/e2e/tests/system/worldPin.spec.ts`
  - 軸付きSVGパス、56px表示、40pxクランプ、斜め回転時のviewport内収まりを検証する。
- `moorestech_web/webui/src/features/tutorial/WorldPinOverlay.tsx`
  - 画面外矢印のSVGパスを変更し、余白をCSS変数だけから読み取る。
- `moorestech_web/webui/src/features/tutorial/worldPin.module.css`
  - 矢印の寸法、塗り、輪郭、影を定義する。
- `moorestech_web/webui/src/app/index.css`
  - 画面端余白の正本を40pxへ変更する。
- `moorestech_web/webui/e2e/capture-world-pin-arrow.ts`
  - mock hostを単体起動し、方向と背景密度を変えた矢印をPNGへ撮影する。
- `.claude/skills/webui-design/SKILL.md`
  - §8.8の許可表現を、承認済みの軸付き塗りつぶし矢印・輪郭・最小限の影へ更新する。

配置検査結果: 既存の表示コンポーネント・CSS Module・ルートデザイントークン・同機能のE2Eをそのまま変更するため、層責務と前例に一致する。新規の状態、通信、イベント、型、制御フローは追加しない。

| # | 項目 | 配置先 | 使用する機構 |
|---|---|---|---|
| 1 | 画面外矢印SVG | `features/tutorial/WorldPinOverlay.tsx` | 既存React表示コンポーネント内のinline SVG |
| 2 | 矢印の寸法・配色・影 | `features/tutorial/worldPin.module.css` | 既存CSS Module |
| 3 | 画面端余白 | `app/index.css` | 既存ルートCSSカスタムプロパティ |
| 4 | 視認性契約 | `e2e/tests/system/worldPin.spec.ts` | 既存Playwrightシステムテスト |
| 5 | 視覚QA画像 | `e2e/capture-world-pin-arrow.ts` | 既存capture harnessと同型のPlaywright単体撮影スクリプト |
| 6 | HUDデザイン規約 | `.claude/skills/webui-design/SKILL.md` | 既存§8.8ホワイトリストの改訂 |

データフロー: Unity射影 → `tutorial.world_pins` → `WorldPinOverlay`（読み手）→ SVG表示。既存経路へ分岐や逆流を追加しない。

### Task 1: Playwrightで矢印の視認性契約を固定する

**Files:**
- Modify: `moorestech_web/webui/e2e/tests/system/worldPin.spec.ts`

**Interfaces:**
- Consumes: `GET /__worldpin?on=0&dx=<number>&dy=<number>&text=<string>`、`data-testid="world-pin-arrow-map-object-pin"`
- Produces: 56px寸法、軸付きSVGパス、40px余白、斜め時viewport内収まりの回帰テスト

- [ ] **Step 1: 右向き矢印の形状・寸法・40px余白を要求する失敗テストへ更新する**

```ts
const viewport = page.viewportSize()!;
const margin = 40;
const box = (await arrow.boundingBox())!;
expect(box.width).toBeCloseTo(56, 0);
expect(box.height).toBeCloseTo(56, 0);
expect(Math.abs(box.x + box.width / 2 - (viewport.width - margin))).toBeLessThanOrEqual(1.5);
await expect(arrow.locator("path")).toHaveAttribute("d", "M2 8 H13 V3 L22 12 L13 21 V16 H2 Z");
expect(await arrow.locator("path").evaluate((path) => getComputedStyle(path).fill)).not.toBe("none");
```

- [ ] **Step 2: 左上斜めのテストを40px余白へ更新し、回転後の境界ボックスがviewport内に収まることを追加する**

```ts
const margin = 40;
const scale = (viewport.height / 2 - margin) / 0.9;
const box = (await arrow.boundingBox())!;
expect(box.x).toBeGreaterThanOrEqual(0);
expect(box.y).toBeGreaterThanOrEqual(0);
expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
expect(box.y + box.height).toBeLessThanOrEqual(viewport.height);
```

- [ ] **Step 3: 対象E2Eを実行し、旧28pxシェブロンとの差で失敗することを確認する**

Run:

```bash
cd moorestech_web/webui
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/system/worldPin.spec.ts
```

Expected: 右向き矢印の幅が28pxであること、SVGパスまたは40px余白が一致しないことによりFAIL。

- [ ] **Step 4: テスト変更をコミットする**

```bash
git add moorestech_web/webui/e2e/tests/system/worldPin.spec.ts
git commit -m "画面外ワールドピン矢印の視認性契約を追加"
```

### Task 2: 56pxの軸付き塗りつぶし矢印を実装する

**Files:**
- Modify: `.claude/skills/webui-design/SKILL.md`
- Modify: `moorestech_web/webui/src/features/tutorial/WorldPinOverlay.tsx`
- Modify: `moorestech_web/webui/src/features/tutorial/worldPin.module.css`
- Modify: `moorestech_web/webui/src/app/index.css`
- Test: `moorestech_web/webui/e2e/tests/system/worldPin.spec.ts`

**Interfaces:**
- Consumes: `WorldPin.directionX`、`WorldPin.directionY`、CSS変数 `--world-pin-edge-margin`
- Produces: `EdgeArrow` の軸付きSVG、56pxの高コントラスト表示、40pxの画面端クランプ

- [ ] **Step 1: webui-design §8.8を承認済みの矢印表現へ更新する**

```markdown
- **画面外矢印**: 方向ベクトルを画面端（マージン `--world-pin-edge-margin` の固定長）へクランプした位置に、方向へ回転したインラインSVGの軸付き塗りつぶし矢印を置く。`--text-high-contrast` の塗りと `--world-pin-face` の輪郭を使い、世界背景から分離する最小限の影を許可する。テキストラベルは付けない（uGUI版HudArrowと同じ責務分担）。
- 新しい色相・光彩・アニメーションは追加しない。z層は `--z-world-pin` トークンのみで制御する。
```

- [ ] **Step 2: SVGパスを軸付き矢印へ置き換える**

```tsx
<svg viewBox="0 0 24 24" aria-hidden="true">
  <path d="M2 8 H13 V3 L22 12 L13 21 V16 H2 Z" />
</svg>
```

- [ ] **Step 3: TypeScript側の余白フォールバックを撤去してCSS変数だけを読む**

```ts
let cachedEdgeMargin: number | null = null;

function readEdgeMargin(): number {
  if (cachedEdgeMargin !== null) return cachedEdgeMargin;
  const raw = getComputedStyle(document.documentElement).getPropertyValue("--world-pin-edge-margin");
  cachedEdgeMargin = Number.parseFloat(raw);
  return cachedEdgeMargin;
}
```

- [ ] **Step 4: CSSで56pxの塗りつぶし矢印と高コントラスト輪郭・影を定義する**

```css
.arrow {
  position: fixed;
  width: 56px;
  height: 56px;
}

.arrow svg {
  width: 100%;
  height: 100%;
  fill: var(--text-high-contrast);
  stroke: var(--world-pin-face);
  stroke-width: 1.5;
  stroke-linejoin: round;
  paint-order: stroke fill;
  filter: drop-shadow(0 2px 3px rgb(0 0 0 / 65%));
}
```

- [ ] **Step 5: ルートデザイントークンの画面端余白を40pxへ変更する**

```css
--world-pin-edge-margin: 40px;
```

- [ ] **Step 6: 対象E2Eを実行して全件成功を確認する**

Run:

```bash
cd moorestech_web/webui
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/system/worldPin.spec.ts
```

Expected: 5 tests passed。

- [ ] **Step 7: buildとlintを実行する**

Run:

```bash
cd moorestech_web/webui
pnpm build
pnpm lint
```

Expected: 両コマンドともexit code 0。

- [ ] **Step 8: 実装をコミットする**

```bash
git add .claude/skills/webui-design/SKILL.md moorestech_web/webui/src/features/tutorial/WorldPinOverlay.tsx moorestech_web/webui/src/features/tutorial/worldPin.module.css moorestech_web/webui/src/app/index.css
git commit -m "画面外ワールドピンを大きな軸付き矢印に変更"
```

### Task 3: Playwrightスクリーンショットでチュートリアル誘導としての大きさをQAする

**Files:**
- Create: `moorestech_web/webui/e2e/capture-world-pin-arrow.ts`
- Modify if tuning is required: `moorestech_web/webui/src/features/tutorial/WorldPinOverlay.tsx`
- Modify if tuning is required: `moorestech_web/webui/src/features/tutorial/worldPin.module.css`
- Modify if tuning is required: `moorestech_web/webui/src/app/index.css`
- Test if tuning is required: `moorestech_web/webui/e2e/tests/system/worldPin.spec.ts`

**Interfaces:**
- Consumes: Playwright mock hostの右・右上・左上・下方向の画面外ワールドピン
- Produces: 寸法・方向・背景コントラスト・画面端欠けが確認できるスクリーンショット

- [ ] **Step 1: mock hostを単体起動して3背景・3方向をPNG撮影するスクリプトを作る**

```ts
import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { chromium } from "@playwright/test";
import { WebSocketServer } from "ws";

const PORT = Number(process.env.CAPTURE_PORT ?? 5402);
const OUT_DIR = process.env.CAPTURE_OUT_DIR ?? "/tmp/world-pin-arrow-qa";

const cases = [
  {
    name: "world-pin-arrow-right-light.png",
    direction: "dx=1&dy=0",
    background: "repeating-linear-gradient(45deg,#f5f0df 0 12px,#b6c6aa 12px 24px)",
  },
  {
    name: "world-pin-arrow-left-up-dark.png",
    direction: "dx=-0.4&dy=-0.9",
    background: "radial-gradient(circle at 30% 40%,#3e4f38 0 8%,transparent 9%),repeating-linear-gradient(135deg,#101722 0 10px,#293020 10px 20px)",
  },
  {
    name: "world-pin-arrow-right-down-game.png",
    direction: "dx=0.8&dy=0.6",
    background: "url('/mock-orange-gradient.png') center/cover no-repeat",
  },
] as const;

async function main() {
  process.env.MOCK_DEMO = "1";
  const { createMockHttpServer } = await import("./mock-host/httpHandler");
  const { attachWsHandlers } = await import("./mock-host/wsHandler");
  const server = createMockHttpServer();
  const wss = new WebSocketServer({ server, path: "/ws" });
  attachWsHandlers(wss);
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
  await mkdir(OUT_DIR, { recursive: true });

  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  await page.goto(`http://127.0.0.1:${PORT}/`);
  await page.getByTestId("hotbar-grid").waitFor();

  for (const capture of cases) {
    await page.request.get(`http://127.0.0.1:${PORT}/__worldpin?on=0&${capture.direction}&text=TutorialTarget`);
    await page.getByTestId("world-pin-arrow-map-object-pin").waitFor();
    await page.locator("#__worldbg").evaluate((element, background) => {
      (element as HTMLElement).style.background = background;
    }, capture.background);
    await page.waitForTimeout(100);
    await page.screenshot({ path: join(OUT_DIR, capture.name) });
  }

  await browser.close();
  wss.close();
  await new Promise<void>((resolve) => server.close(() => resolve()));
}

void main();
```

- [ ] **Step 2: Playwrightで対象E2EとPNG撮影スクリプトを実行する**

Run:

```bash
cd moorestech_web/webui
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/system/worldPin.spec.ts
pnpm build
CAPTURE_OUT_DIR=/tmp/world-pin-arrow-qa pnpm exec tsx e2e/capture-world-pin-arrow.ts
ls -l /tmp/world-pin-arrow-qa/world-pin-arrow-*.png
```

Expected: 対象テスト5件が成功し、1280×720のPNGが3枚生成される。

- [ ] **Step 3: 3枚の画像を開き、次の合格基準をすべて満たすか確認する**

```text
- 「＞」ではなく、太い軸と三角形の先端を一目で識別できる。
- 1280×720のHUDでチュートリアル誘導として見落としにくい。
- ゲーム背景に対して明るい面・暗い輪郭・影が分離して見える。
- 右方向と斜め方向のどちらも画面端で欠けない。
- 矢印先端が対象方向を正しく向く。
```

- [ ] **Step 4: 不合格項目があればSVG形状・寸法・余白・影を調整し、E2Eとスクリーンショット確認を繰り返す**

```text
寸法不足なら4px単位で拡大し、画面端余白を ceil(size / sqrt(2))px へ同時に更新する。
圧迫感が強ければ4px単位で縮小するが、元寸法の2倍である56pxを下限とする。
形状だけを変更する場合はSVGパス期待値を同時に更新する。
背景から分離しなければ輪郭幅を0.25単位、影の不透明度を5%単位で調整する。
合格基準を全件満たすまでTask 3 Step 2へ戻る。
```

- [ ] **Step 5: 撮影スクリプトとQA調整をコミットする**

```bash
git add moorestech_web/webui/e2e/capture-world-pin-arrow.ts moorestech_web/webui/src/features/tutorial/WorldPinOverlay.tsx moorestech_web/webui/src/features/tutorial/worldPin.module.css moorestech_web/webui/src/app/index.css moorestech_web/webui/e2e/tests/system/worldPin.spec.ts
git commit -m "画面外ワールドピン矢印のPlaywright視覚QAを追加"
```

### Task 4: 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行する

**Files:**
- Review: `docs/superpowers/specs/2026-07-27-world-pin-arrow-visual-design.md`
- Review: `docs/superpowers/plans/2026-07-27-world-pin-arrow-visual.md`
- Review: `.claude/skills/webui-design/SKILL.md`
- Review: `moorestech_web/webui/e2e/tests/system/worldPin.spec.ts`
- Review: `moorestech_web/webui/e2e/capture-world-pin-arrow.ts`
- Review: `moorestech_web/webui/src/features/tutorial/WorldPinOverlay.tsx`
- Review: `moorestech_web/webui/src/features/tutorial/worldPin.module.css`
- Review: `moorestech_web/webui/src/app/index.css`

**Interfaces:**
- Consumes: ブランチの全コミットとベースとの差分
- Produces: moores-code-reviewの全レンズ結果と、修正後の再検証結果

- [ ] **Step 1: moores-code-reviewスキルを読み、全ブランチレビューを実行する**

Run: スキルが指定するコマンドを、`feature/world-pin-arrow-visual` のベース `0a7251226` との差分全体に対して実行する。

Expected: Critical、Warning、機械チェック違反をすべて確認できる。

- [ ] **Step 2: 指摘を修正して関連検証を再実行する**

Run:

```bash
cd moorestech_web/webui
pnpm build
pnpm lint
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/system/worldPin.spec.ts
```

Expected: build・lint成功、5 tests passed。

- [ ] **Step 3: レビュー修正と記録をコミットする**

```bash
git add -A
git commit -m "画面外ワールドピン矢印の最終レビューを反映"
```

## 判断記録（ADR）

- 対応specの判断記録: `docs/superpowers/specs/2026-07-27-world-pin-arrow-visual-design.md#判断記録adr`
- **agent前提（拒否権つき）**: 寸法とviewport内収まりを自動E2Eで固定し、チュートリアルとしての視認性はPlaywrightスクリーンショットの目視基準で補完する。理由はピクセル寸法だけでは形状認知と背景コントラストを証明できないため。
- **agent前提（拒否権つき）**: 画像QAで不十分なら、最初の56px案へ固執せず寸法・形状・影・余白を一体で再調整する。理由はユーザーの成功条件が「適切な大きさを確認できるまで」であるため。
