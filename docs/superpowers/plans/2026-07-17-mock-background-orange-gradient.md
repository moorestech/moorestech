# Mock Background Orange Gradient Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** モックWeb UIへ明るいオレンジグラデーションを表示し、同じ背景をuGUIで利用可能な単体PNGとして出力する。

**Architecture:** `public/mock-orange-gradient.png`を表示と配布の単一ソースにする。モックホストは`MOCK_DEMO=1`のときだけ静的HTMLへ背景要素を注入し、評価用キャプチャも同じ画像URLを参照するため、本番CEFの透明背景には影響しない。

**Tech Stack:** TypeScript、React、Vite、Node.js HTTP、Playwright、PNG

## Global Constraints

- 本番Web UIの透明背景を維持する。
- Prefab・シーン・ScriptableObjectは直接編集しない。
- `.meta`ファイルは手動作成しない。
- 背景は上部が淡いアプリコット、中央が鮮やかなオレンジ、下部が暖色寄りの濃いオレンジの縦グラデーションとする。

---

### Task 1: 共用背景画像とモック表示

**Files:**
- Create: `moorestech_web/webui/public/mock-orange-gradient.png`
- Modify: `moorestech_web/webui/e2e/mock-host/httpHandler.ts`
- Modify: `moorestech_web/webui/e2e/capture-eval.ts`
- Test: `moorestech_web/webui/e2e/mock-host/tests/httpHandler.test.ts`

**Interfaces:**
- Consumes: `MOCK_DEMO=1`、Viteの`public/`静的アセットコピー
- Produces: `/mock-orange-gradient.png`、モック専用の`#__worldbg`背景要素

- [ ] **Step 1: モックHTMLの背景注入テストを書く**

`MOCK_DEMO=1`で`createMockHttpServer()`を起動して`GET /`を取得し、HTMLに`id="__worldbg"`と`url('/mock-orange-gradient.png')`が含まれることを検証する。`MOCK_DEMO`なしでは両方が含まれないことも別ケースで検証する。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `pnpm vitest run e2e/mock-host/tests/httpHandler.test.ts`

Expected: 背景要素が未実装のためFAILする。

- [ ] **Step 3: 単体PNGを出力する**

上部を淡いアプリコット、中央を明るいオレンジ、下部を暖色寄りの濃いオレンジとした、文字・模様・物体のない滑らかな縦グラデーションPNGを`public/mock-orange-gradient.png`へ出力する。

- [ ] **Step 4: モックホストとキャプチャを同じ画像へ接続する**

`httpHandler.ts`では`MOCK_DEMO=1`の`index.html`応答時だけ、`<body>`直後へ次の要素を注入する。

```html
<div id="__worldbg" style="position:fixed;inset:0;z-index:-1;pointer-events:none;background:url('/mock-orange-gradient.png') center/cover no-repeat"></div>
```

`capture-eval.ts`のCSSグラデーション注入も同じ`url('/mock-orange-gradient.png') center/cover no-repeat`へ置き換える。

- [ ] **Step 5: テストとビルドを通す**

Run: `pnpm vitest run e2e/mock-host/tests/httpHandler.test.ts && pnpm build`

Expected: テストがPASSし、Viteビルドが成功して`dist/mock-orange-gradient.png`が生成される。

- [ ] **Step 6: 実表示をQAする**

`MOCK_DEMO=1 pnpm tsx e2e/mock-host/server.ts`を起動し、`http://127.0.0.1:5273/`をブラウザで開く。Web UI背後が明るいオレンジグラデーションで、PNG単体と同じであること、モック通信とUI操作が動くことを確認する。

- [ ] **Step 7: 変更をコミットする**

```bash
git add moorestech_web/webui/public/mock-orange-gradient.png moorestech_web/webui/e2e/mock-host/httpHandler.ts moorestech_web/webui/e2e/mock-host/tests/httpHandler.test.ts moorestech_web/webui/e2e/capture-eval.ts docs/superpowers/specs/2026-07-17-mock-background-orange-gradient-design.md docs/superpowers/plans/2026-07-17-mock-background-orange-gradient.md
git commit -m "feat(webui): add reusable orange mock background"
```
