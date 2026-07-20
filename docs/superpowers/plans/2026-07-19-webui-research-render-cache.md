# WebUI Research Render Cache Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 研究ツリーのパン・ズーム中は静的な研究カードと接続線を再評価せず、表示データが変わった場合だけ描画キャッシュを更新する。

**Architecture:** `TreeView` が接続線とノードのReact要素群を `useMemo` で保持し、viewport state更新時はキャンバスtransformだけを再評価する。`ResearchTreePanel` はツリーアクセサとカード描画関数の参照を安定化し、研究データ・所持数・アイテムマスタが変わった場合だけキャッシュを無効化する。i18n欠落警告は辞書世代内でキーごとに一度へ抑える。

**Tech Stack:** React 18、TypeScript、CSS Modules、Vitest、react-test-renderer、Playwright、pnpm

## Global Constraints

- パン・ズーム、研究実行、研究状態表示、所持数によるボタン活性判定の挙動を変更しない。
- `research.tree`、inventory topic、C#ホスト、通信契約、永続化は変更しない。
- viewport stateだけが変わった場合は、`renderNode` を再実行しない。
- `nodes`、所持数Map、アイテムマスタが変わった場合は、研究カードを再評価する。
- 言語変更は既存の各カードのi18n購読から反映する。
- 翻訳辞書が同一の間は同一欠落キーを一度だけ警告し、辞書更新後は再び一度だけ警告する。
- 変更するコードファイルは1ファイル200行以下を維持する。
- TypeScript/TSXの主要処理コメントは日本語・英語の2行セットにする。
- `partial`、デフォルト引数、手動 `.meta` 作成は行わない。

## File Structure

| File | Responsibility |
|---|---|
| `moorestech_web/webui/src/shared/treeView/TreeView.tsx` | viewport入力状態と、静的な接続線・ノード場面キャッシュを所有する |
| `moorestech_web/webui/src/shared/treeView/TreeView.test.ts` | viewport state更新で `renderNode` が再実行されない契約を固定する |
| `moorestech_web/webui/src/features/research/ResearchTreePanel.tsx` | 研究データを共有TreeViewへ渡し、意味のある入力だけでカード描画関数を更新する |
| `moorestech_web/webui/src/shared/i18n/i18nStore.ts` | 辞書世代と欠落翻訳警告の重複抑止を所有する |
| `moorestech_web/webui/src/shared/i18n/useI18n.test.ts` | 欠落警告の辞書世代単位キャッシュを固定する |
| `moorestech_web/webui/package.json` / `pnpm-lock.yaml` | DOM不要のReactレンダー回帰テスト依存を固定する |

## Architecture Placement Audit

| # | Item | Placement | Mechanism | Verdict |
|---|---|---|---|---|
| 1 | 静的ツリー場面キャッシュ | `src/shared/treeView/TreeView.tsx` 共有表示基盤 | React `useMemo` | 研究語彙を持たずChallenge等にも同じ意味で適用されるため共有層に適合 |
| 2 | 研究アクセサ・カード描画関数 | `src/features/research/ResearchTreePanel.tsx` 研究表示層 | モジュール関数＋`useCallback` | 研究データ形状と所持数判定を扱うため具体層に適合 |
| 3 | 翻訳欠落キーキャッシュ | `src/shared/i18n/i18nStore.ts` 翻訳基盤 | 辞書更新時clearする`Set<string>` | 翻訳辞書世代に属する横断的診断状態なのでi18n層に適合 |
| 4 | viewportレンダー回帰 | `src/shared/treeView/TreeView.test.ts` | react-test-renderer | DOMなしVitestという既存テスト方針を維持 |
| 5 | 実ブラウザ回帰 | 既存 `e2e/tests/research.spec.ts` | Playwright | 既存研究パン・ズーム操作の同一経路を再利用 |

データフロー:

`research/inventory topic → ResearchTreePanel（読み手）→ TreeView静的場面キャッシュ → viewport transform → DOM表示`

新しい書き込み経路、交差点、状態複製、通信経路は追加しない。既存viewport stateを正として受動的に描画結果を再利用する。

操作死活表:

| Operation | Plan後 | Evidence |
|---|---|---|
| ホイールズーム | 維持 | viewport stateとtransform式を変更しない |
| 背景左ドラッグパン | 維持 | pointer handlerを変更せず、子要素の生成だけをメモ化する |
| 右ドラッグ無視 | 維持 | `event.button !== 0` 判定を変更しない |
| ノード上ドラッグ無視 | 維持 | `nodeTargetSelector` 判定を変更しない |
| 研究実行 | 維持 | `ResearchNodeCard` とActionを変更しない |
| 所持数変化による活性更新 | 維持 | `owned` 変更で `renderNode` と場面キャッシュを更新する |
| 研究完了表示 | 維持 | `nodes` 変更で場面キャッシュを更新する |
| 言語切替 | 維持 | 各カードの既存 `useI18n` 購読を変更しない |

---

### Task 1: TreeView Static Scene Cache

**Files:**
- Modify: `moorestech_web/webui/package.json`
- Modify: `pnpm-lock.yaml`
- Create: `moorestech_web/webui/src/shared/treeView/TreeView.test.ts`
- Modify: `moorestech_web/webui/src/shared/treeView/TreeView.tsx`
- Modify: `moorestech_web/webui/src/features/research/ResearchTreePanel.tsx`

**Interfaces:**
- Consumes: `TreeView<T>` existing props and viewport handlers
- Produces: viewport-only state changes reuse the same rendered scene; research callbacks change only with semantic data

- [ ] **Step 1: Install the renderer test dependency**

Run:

```bash
cd moorestech_web/webui
pnpm add -D react-test-renderer@18.3.1 @types/react-test-renderer@18.3.0
```

Expected: `package.json` and `pnpm-lock.yaml` include both exact development dependencies.

- [ ] **Step 2: Write the failing TreeView render-cache test**

Create `TreeView.test.ts`:

```ts
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import TreeView from "./TreeView";

describe("TreeView render cache", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("does not rebuild nodes when only the viewport moves", () => {
    vi.stubGlobal("Element", class TestElement {});
    const nodes = [{ id: "node-a", x: 10, y: 20, prevIds: [] as string[] }];
    let renderedNodeCount = 0;
    const getId = (node: (typeof nodes)[number]) => node.id;
    const getPosition = (node: (typeof nodes)[number]) => ({ x: node.x, y: node.y });
    const getPrevIds = (node: (typeof nodes)[number]) => node.prevIds;
    const renderNode = () => {
      renderedNodeCount++;
      return createElement("span", null, "node");
    };
    const renderer = create(createElement(TreeView, {
      nodes, getId, getPosition, getPrevIds, renderNode,
      nodeTargetSelector: "[data-node]", testIdPrefix: "test",
    }));
    const viewport = renderer.root.findByProps({ "data-testid": "test-viewport" });
    expect(renderedNodeCount).toBe(1);

    act(() => viewport.props.onPointerDown({
      isPrimary: true, button: 0, target: null, pointerId: 1, clientX: 0, clientY: 0,
      currentTarget: { setPointerCapture: () => undefined },
    }));
    act(() => viewport.props.onPointerMove({
      pointerId: 1, clientX: 10, clientY: 5,
      currentTarget: {
        offsetWidth: 100,
        getBoundingClientRect: () => ({ width: 100 }),
      },
    }));

    expect(renderedNodeCount).toBe(1);
  });
});
```

- [ ] **Step 3: Run the test and verify RED**

Run:

```bash
cd moorestech_web/webui
pnpm vitest run src/shared/treeView/TreeView.test.ts
```

Expected: FAIL because `renderedNodeCount` is greater than `1` after viewport state updates.

- [ ] **Step 4: Memoize the static scene in TreeView**

In `TreeView.tsx`, add a memoized React fragment after `byId`:

```tsx
  // ノードと接続線は意味のある入力が変わるまで同じReact要素を再利用する
  // Reuse node and connection React elements until a semantic input changes
  const renderedScene = useMemo(() => (
    <>
      {nodes.flatMap((node) => getPrevIds(node).map((prevId) => {
        const prev = byId.get(prevId);
        if (!prev) return null;
        const line = lineBetween(toTreeCanvasPoint(getPosition(node), bounds), toTreeCanvasPoint(getPosition(prev), bounds));
        return <div key={`${getId(node)}-${prevId}`} className={styles.line}
          style={{ left: line.x, top: line.y, width: line.length, transform: `rotate(${line.angleDeg}deg)` }} />;
      }))}
      {nodes.map((node) => <div key={getId(node)}>{renderNode(node, toTreeCanvasPoint(getPosition(node), bounds))}</div>)}
    </>
  ), [bounds, byId, getId, getPosition, getPrevIds, nodes, renderNode]);
```

Replace the existing two inline `nodes.flatMap` / `nodes.map` blocks inside the canvas with:

```tsx
        {renderedScene}
```

- [ ] **Step 5: Stabilize ResearchTreePanel callbacks**

In `ResearchTreePanel.tsx`, import `useCallback` and `TreePoint`, then add module-level accessors:

```ts
const getResearchNodeId = (node: ResearchNodeData) => node.guid;
const getResearchNodePosition = (node: ResearchNodeData) => node.position;
const getPreviousResearchNodeIds = (node: ResearchNodeData) => node.prevGuids;
```

Replace inline functions with callbacks whose invalidation matches visible data:

```tsx
  const resolveName = useCallback((itemId: number) => itemMaster?.get(itemId)?.name, [itemMaster]);
  const renderResearchNode = useCallback((node: ResearchNodeData, point: TreePoint) => (
    <ResearchNodeCard
      node={node}
      left={point.x}
      top={point.y}
      owned={owned}
      resolveName={resolveName}
    />
  ), [owned, resolveName]);
```

Pass the stable references:

```tsx
      <TreeView nodes={nodes} getId={getResearchNodeId} getPosition={getResearchNodePosition}
        getPrevIds={getPreviousResearchNodeIds} nodeTargetSelector="[data-research-node]" testIdPrefix="research"
        renderNode={renderResearchNode} />
```

- [ ] **Step 6: Run the test and verify GREEN**

Run:

```bash
cd moorestech_web/webui
pnpm vitest run src/shared/treeView/TreeView.test.ts
```

Expected: `1 passed` and `renderedNodeCount` remains `1`.

- [ ] **Step 7: Run focused tree and research unit tests**

Run:

```bash
cd moorestech_web/webui
pnpm vitest run src/shared/treeView src/features/research
```

Expected: all selected test files pass with zero failures.

- [ ] **Step 8: Commit the render cache**

```bash
git add moorestech_web/webui/package.json pnpm-lock.yaml \
  moorestech_web/webui/src/shared/treeView/TreeView.test.ts \
  moorestech_web/webui/src/shared/treeView/TreeView.tsx \
  moorestech_web/webui/src/features/research/ResearchTreePanel.tsx
git commit -m "perf(webui): 研究ツリーの静的描画をキャッシュ"
```

---

### Task 2: Missing Translation Warning Cache

**Files:**
- Modify: `moorestech_web/webui/src/shared/i18n/useI18n.test.ts`
- Modify: `moorestech_web/webui/src/shared/i18n/i18nStore.ts`

**Interfaces:**
- Consumes: `setDictionaries(locale, dictionary, fallbackDictionary)` and `createTranslator(snapshot)`
- Produces: one missing-key warning per key per dictionary generation

- [ ] **Step 1: Write the failing warning-cache test**

Extend the import and add this test to `useI18n.test.ts`:

```ts
import { createTranslator, setDictionaries } from "./i18nStore";

it("warns once per missing key until dictionaries change", () => {
  const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
  const current = { locale: "japanese", dictionary: {}, fallbackDictionary: {} };
  setDictionaries(current.locale, current.dictionary, current.fallbackDictionary);
  const first = createTranslator(current);
  const second = createTranslator(current);

  first("missing.key");
  second("missing.key");
  expect(warn).toHaveBeenCalledTimes(1);

  setDictionaries("english", {}, {});
  createTranslator({ locale: "english", dictionary: {}, fallbackDictionary: {} })("missing.key");
  expect(warn).toHaveBeenCalledTimes(2);
  warn.mockRestore();
});
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
cd moorestech_web/webui
pnpm vitest run src/shared/i18n/useI18n.test.ts
```

Expected: FAIL because the same key warns twice before the dictionary update.

- [ ] **Step 3: Add the dictionary-generation warning cache**

In `i18nStore.ts`, add the cache beside `listeners`:

```ts
const listeners = new Set<() => void>();
const warnedMissingTranslationKeys = new Set<string>();
```

Clear it when dictionaries change:

```ts
  snapshot = { locale, dictionary, fallbackDictionary };
  warnedMissingTranslationKeys.clear();
  listeners.forEach((listener) => listener());
```

Replace the unconditional warning with:

```ts
    if (template === undefined && !warnedMissingTranslationKeys.has(key)) {
      warnedMissingTranslationKeys.add(key);
      console.warn(`[i18n] Missing translation key: ${key}`);
    }
```

- [ ] **Step 4: Run the test and verify GREEN**

Run:

```bash
cd moorestech_web/webui
pnpm vitest run src/shared/i18n/useI18n.test.ts
```

Expected: all translation tests pass with zero failures.

- [ ] **Step 5: Commit the warning cache**

```bash
git add moorestech_web/webui/src/shared/i18n/i18nStore.ts \
  moorestech_web/webui/src/shared/i18n/useI18n.test.ts
git commit -m "perf(webui): 翻訳欠落警告を辞書世代単位で抑止"
```

---

### Task 3: Browser Regression and Full Verification

**Files:**
- Verify: `moorestech_web/webui/e2e/tests/research.spec.ts`
- Verify: all modified files and current runtime at `http://127.0.0.1:25173/`

**Interfaces:**
- Consumes: Task 1 render cache and Task 2 warning cache
- Produces: automated and actual-game evidence that interactions remain correct and warning bursts disappear

- [ ] **Step 1: Run the research Playwright spec**

Run:

```bash
cd moorestech_web/webui
pnpm playwright test --config e2e/playwright.config.ts e2e/tests/research.spec.ts
```

Expected: all research tests pass, including zoom, pan, right-drag rejection, node-drag rejection, and research completion.

- [ ] **Step 2: Run the complete Vitest suite**

Run:

```bash
cd moorestech_web/webui
pnpm test
```

Expected: all test files pass with zero failed tests.

- [ ] **Step 3: Run lint**

Run:

```bash
cd moorestech_web/webui
pnpm lint
```

Expected: exit code `0` with zero ESLint errors.

- [ ] **Step 4: Run TypeScript and production build**

Run:

```bash
cd moorestech_web/webui
pnpm build
```

Expected: `tsc -b` and `vite build` both exit with code `0`.

- [ ] **Step 5: Inspect the actual game UI**

At `http://127.0.0.1:25173/`:

1. Confirm `[data-testid="research-tree"]` and 47 `[data-research-node]` elements are present.
2. Read warning logs, record the latest timestamp, and clear the comparison window logically by retaining that timestamp.
3. Perform one wheel zoom and one empty-background pan.
4. Confirm the research canvas transform changes.
5. Confirm no new burst of repeated `[i18n] Missing translation key` messages appears after the recorded timestamp.
6. Confirm a visible research card remains rendered with its item images and research button.

- [ ] **Step 6: Run moores-code-review**

Review the final diff with the `moores-code-review` skill, address every actionable finding, then rerun Steps 1-4 if code changes.

- [ ] **Step 7: Verify repository state and commit any review fixes**

Run:

```bash
git diff --check
git status --short
git log -n 3 --oneline
```

Expected: no uncommitted implementation changes remain, whitespace checks pass, and the design plus implementation commits are present.
