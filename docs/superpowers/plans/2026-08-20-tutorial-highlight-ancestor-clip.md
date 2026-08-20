# チュートリアルハイライトの祖先クリップ対応 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** チュートリアルUIハイライトの枠を、アンカー要素の祖先が持つ `overflow` クリップに合わせてマスクし、表示領域の外へ出たノードのハイライトだけが取り残される不具合を直す。

**Architecture:** `resolveTutorialAnchor` がアンカーの祖先を辿って実クリップ矩形を求め、`ResolvedAnchor` に `clip` として載せる。`TutorialOverlay.renderOutline` はハイライトのボックス座標系へ変換した `clip-path: inset()` を当て、交差が空なら要素自体を作らない。walk は feature の語彙を持たず DOM 構造だけで判断するため、`data-tutorial-anchor` を付けた任意の要素へ自動的に効く。

**Tech Stack:** TypeScript / React / Vite / Vitest（environment: `node`）/ Playwright（e2e, mock-host）

## Requirements

- R1: 研究ツリーのノードをパンで TreeView の表示領域外へ出したとき、ハイライト枠がパネル外へ描かれない。受け入れ基準: e2e で「完全に外」の状態にしたとき `[data-kind="outline"]` 要素が DOM に存在しない
- R2: ノードが表示領域の境界をまたいで部分的に見えているとき、ハイライト枠がノードと同じ位置で切られる。受け入れ基準: クリップされた辺について、ハイライトの可視端がアンカーの可視端（`IntersectionObserver.intersectionRect`）と 1.5px 以内で一致する
- R3: クリップ祖先を持たないアンカー（クロスヘア等）のハイライトは従来どおり全体が描かれる。受け入れ基準: 既存 `e2e/tests/system/tutorial.spec.ts` が無改修で通る
- R4: walk は feature の知識を持たず、`data-tutorial-anchor` が付いた任意の要素に効く。受け入れ基準: `ancestorClip.ts` に研究・チャレンジ・レシピ等のドメイン語彙が一切現れない
- R5: `clip` が変化したがアンカー矩形が変化していない場合（コンテナのリサイズ等）にもマスクが更新される。受け入れ基準: `isSameAnchor` が `clip` の4値を比較する
- R6: 「実UI経路 × ブラウザオラクル」の e2e を1本持つ。受け入れ基準: 期待値をハードコードせず `IntersectionObserver.intersectionRect` から導く
- R7: 見た目を確認できる成果物が残る。受け入れ基準: e2e の mp4 録画に加え「内側 / 部分 / 完全に外」の3スクリーンショットが保存される
- R8: クリップされていない辺で `box-shadow` の外側グロー（4px）が消えない。受け入れ基準: 単体テストが無クリップ時の `clip-path` を `inset(-4px -4px -4px -4px)` と検証する

### やらないこと（スコープ境界）

- **`dragGuide`（D&Dガイド矢印）はマスクしない。** 従来どおりパネル外へはみ出したまま残す。理由: 矢印は from→to を translate アニメーションで移動するため、どの時点のどのクリップ矩形を当てるかが未決（ADR 0023 Considered Options）
- **ack セマンティクスは変えない。** 完全にクリップされていても `status: "ready"` / `reason: "mounted"` を ack し続ける。理由: 「1pxだけ見えている状態を表示成功とみなすか」の閾値が未裁定
- **`paddingPx` のリングが端で切られて枠が「コ」の字に欠ける件は直さない。** ユーザー裁定により今回は許容する
- **祖先の `border-radius` に追従しない。** 矩形 `inset()` のみ。現状の webui でクリップ祖先に角丸を持つものは無い
- **実ゲーム（unityプレイ録画テスト）での確認はしない。** mock-host の e2e で完了とする

## Global Constraints

- **作業場所**: worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/tutorial-highlight-ancestor-clip`、ブランチ `fix/tutorial-highlight-ancestor-clip`。`pnpm install` 済み。webui のみの変更なので Unity Editor は不要
- **コマンドの実行ディレクトリ**: 以下すべて `moorestech_web/webui` 配下で実行する
- **1ファイル200行以下。`partial` 禁止。`Func<>` 禁止。`try-catch` 原則禁止**（AGENTS.md）
- **デフォルト引数は使わない。** 引数追加時は呼び出し側を全て変更する（AGENTS.md）
- **コメントは日本語1行 → 英語1行の2行セット**を約3〜10行ごと。日本語・英語それぞれ必ず1行に収める（AGENTS.md）
- **1ディレクトリ10ファイルまで。** `src/shared/tutorialAnchor/` は現在8ファイルで、本planで9ファイルになる（上限内）
- **`?? Default` フォールバックやスキーマの `optional: true` で欠損を吸収しない**（AGENTS.md 設計原則）。`clip` は `status: "ready"` の必須フィールドにし、型エラーが出る呼び出し側は全て更新する
- **e2e はポート 5273 を単一で共有する。** 他セッションが同時に `pnpm test:e2e` を走らせていると無関係な spec が落ちる。失敗 spec が実行ごとに変わったらまずポート衝突を疑う
- **既知の外部ブロッカー**: `origin/master` の `.moorestech-external-revisions.json` が存在しないコミット `c35f10ab` を指しており、CI の master data checkout が失敗する（bd `moorestech-hvwb`、別担当が対応中）。本planの変更とは無関係で、ローカル検証には影響しない。PR の CI が master pin で落ちた場合はこの issue の解決を待つ
- **設計の正本**: `docs/adr/0023-tutorial-highlight-ancestor-clip-mask.md` と `.decisions/2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md`。実装前に両方読むこと

---

## File Structure

| ファイル | 責務 |
|---|---|
| `src/shared/tutorialAnchor/ancestorClip.ts`（新規） | 祖先を辿った実クリップ矩形の算出と、ボックスを切る `clip-path` 値の生成。DOM構造だけを見る純粋な幾何層 |
| `src/shared/tutorialAnchor/resolveAnchor.ts`（変更） | `status: "ready"` に `clip` を載せる |
| `src/shared/tutorialAnchor/index.ts`（変更） | `clipPathInset` と `ClipRect` を re-export |
| `src/features/tutorial/TutorialOverlay.tsx`（変更） | `renderOutline` で `clip-path` を当て、交差が空なら `null`。`isSameAnchor` で `clip` も比較 |
| `src/features/tutorial/TutorialOverlay.test.ts`（変更） | `ResolvedAnchor` の型追従と、clip-path 適用・非描画の検証 |
| `e2e/mock-host/topics/topicControls.ts`（変更） | 研究ノードを狙う tutorial シナリオを追加 |
| `e2e/tests/system/tutorialHighlightClip.spec.ts`（新規） | 実UI経路 × ブラウザオラクルの唯一のテスト |

## 配置と前例（spec-architecture-review）

**データフロー地図**（本機能は既存パイプラインに相乗りする）:

```
（rAF / MutationObserver / ResizeObserver / capture scroll）
  → TutorialAnchorRegistry.markAllDirty
  → ［resolveTutorialAnchor が ResolvedAnchor を作る］   ← ancestorClip はこの駅の内部ヘルパ
  → TutorialOverlay が購読して setResolved
  → renderOutline が描画
```

`ancestorClip` の立ち位置は **既存の駅（`resolveTutorialAnchor`）の内部ヘルパ**であり、新しい駅・分岐・逆流を作らない。交差点なし。

**検査1（層責務）**: `src/shared/tutorialAnchor/` は anchor 解決の共有基盤。`ancestorClip.ts` は DOM 構造（`overflow` / `position` / border 幅）だけを見て判断し、研究・チャレンジ・レシピといったドメイン語彙を型名にもメソッド名にも持たない。共有層への追加に必要な「ドメイン非依存であること」を満たす。

**検査2（前例）**: 同ディレクトリの `resolveAnchor.ts` が「DOM を見て `ResolvedAnchor` を組み立てる」役割の前例であり、同じ場所・同じ形に置く。e2e の topic シナリオは既存 `tutorialOutline`（`topicControls.ts:84`）が前例。幾何を検証する spec は `e2e/tests/system/worldPin.spec.ts` と `e2e/tests/research/researchViewport.spec.ts` が前例で、後者の `settleBoundingBox` ヘルパをそのまま使う。

**検査3（イディオム）**: 新しい通知機構は導入しない（既存の registry → React state 経路のまま）。`try-catch` 無し、デフォルト引数はテストヘルパのみ、全ファイル200行以下。

**検査4（機構選択）**: 受動的統合案（ブラウザの `IntersectionObserver.intersectionRect` をそのまま使う）と能動介入案（自前 walk）を ADR 0023 の Considered Options で head-to-head 比較済み。IO は非同期配信でパン中に rAF 更新から遅れマスク端がチラつくため、追従精度を理由に walk を採用した。

**機能パリティ死活表**:

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| クロスヘア等クリップ祖先を持たないアンカーのハイライト | 生きる | clip がウィンドウ全体になり `inset(0px 0px 0px 0px)` 相当 |
| 研究 / チャレンジ / レシピ / ビルドメニューのハイライト | 生きる（マスクが付く） | 本タスクの目的 |
| D&Dガイド矢印 | 生きる（従来どおりマスク無し） | `renderDragGuide` 未変更 |
| `tutorial.anchor_ack` の送信 | 生きる | `status` / `reason` を変更しない |
| 完全に隠れたアンカーのハイライト | **消える** | 従来は誤ってパネル外へ描かれていた。意図した修正（R1） |

---

### Task 1: 祖先クリップ矩形の算出と ResolvedAnchor への結線

**Files:**
- Create: `moorestech_web/webui/src/shared/tutorialAnchor/ancestorClip.ts`
- Modify: `moorestech_web/webui/src/shared/tutorialAnchor/resolveAnchor.ts`
- Modify: `moorestech_web/webui/src/shared/tutorialAnchor/index.ts`

**Interfaces:**
- Consumes: なし（このタスクが起点）
- Produces:
  - `export type ClipRect = { left: number; top: number; right: number; bottom: number }`
  - `export function ancestorClipRect(element: HTMLElement): ClipRect`
  - `export function clipPathInset(box: ClipRect, clip: ClipRect, outsetPx: number): string | null` — 完全に切り取られる場合 `null`。`outsetPx` はボックスの外側へ描かれる装飾（`box-shadow` のグロー等）の幅で、クリップされていない辺はこの分だけ負の inset にして装飾を残す
  - `ResolvedAnchor` の ready 変種が `clip: ClipRect` を必須で持つ

- [ ] **Step 1: `ancestorClip.ts` を新規作成する**

`moorestech_web/webui/src/shared/tutorialAnchor/ancestorClip.ts`:

```ts
// アンカーの祖先を辿り、実際にクリップを掛けている要素のpadding boxを交差させる
// Walk the anchor's ancestors, intersecting the padding box of every element that truly clips it
//
// CSSの規則: 祖先Aのoverflowは、Aが子孫Dの包含ブロック連鎖の内側にある場合にのみDをクリップする
// CSS rule: ancestor A's overflow clips descendant D only while A sits inside D's containing-block chain
//
// 脱出則(escape)は現状デッドロジックである。実アンカーは全て .stage(position:relative) 配下にあり
// The escape rules are dead logic today: every real anchor lives under .stage (position: relative)
// absoluteの包含ブロックは常に .stage になるため脱出せず、position:fixed なアンカーも存在しない
// so absolutes never escape, and no anchor is position: fixed
// スクロールコンテナ内へ position:fixed のモーダルを置いた時に初めて効き、誤るとハイライトが丸ごと消える
// They only matter once a fixed modal lands inside a scroller, where a mistake hides the highlight entirely
// 変更する場合は docs/adr/0023-tutorial-highlight-ancestor-clip-mask.md の Consequences を読むこと
// Read the Consequences section of ADR 0023 before changing them

export type ClipRect = { left: number; top: number; right: number; bottom: number };

export function ancestorClipRect(element: HTMLElement): ClipRect {
  let clip: ClipRect = { left: 0, top: 0, right: innerWidth, bottom: innerHeight };
  let escape = getComputedStyle(element).position;
  let node = element.parentElement;
  while (node) {
    const style = getComputedStyle(node);
    const containsFixed = createsFixedContainingBlock(style);
    const containsAbsolute = containsFixed || style.position !== "static";

    // 現在効いている脱出力をこの祖先が捕まえるか
    // Whether this ancestor captures the escape currently in effect
    let clipsHere = true;
    if (escape === "fixed") {
      clipsHere = containsFixed;
      if (containsFixed) escape = "static";
    } else if (escape === "absolute") {
      clipsHere = containsAbsolute;
      if (containsAbsolute) escape = "static";
    }
    if (clipsHere && clipsContent(style)) clip = intersect(clip, paddingBox(node, style));

    // この祖先自身のpositionが、これより上での脱出力になる
    // The ancestor's own position becomes the escape in effect above it
    if (style.position === "fixed") escape = "fixed";
    else if (style.position === "absolute") escape = "absolute";
    node = node.parentElement;
  }
  return clip;
}

// ボックスをclipで切るclip-path値。完全に切り取られる場合はnullで、呼び出し側は描画しない
// The clip-path value cutting box by clip; null when nothing survives so the caller skips rendering
//
// clip-pathの参照ボックスはborder boxなので inset(0px) でも box-shadow の外側グローが切り落とされる
// clip-path resolves against the border box, so even inset(0px) shaves off the box-shadow's outer glow
// クリップの要らない辺は -outsetPx にして、装飾ごと残す
// Sides that need no clipping get -outsetPx so the decoration survives intact
export function clipPathInset(box: ClipRect, clip: ClipRect, outsetPx: number): string | null {
  const top = Math.max(-outsetPx, clip.top - box.top);
  const right = Math.max(-outsetPx, box.right - clip.right);
  const bottom = Math.max(-outsetPx, box.bottom - clip.bottom);
  const left = Math.max(-outsetPx, clip.left - box.left);
  if (top + bottom >= box.bottom - box.top || left + right >= box.right - box.left) return null;
  return `inset(${top}px ${right}px ${bottom}px ${left}px)`;
}

function createsFixedContainingBlock(style: CSSStyleDeclaration) {
  return style.transform !== "none" || style.filter !== "none" || style.perspective !== "none" ||
    style.backdropFilter !== "none" || style.willChange.includes("transform") ||
    style.willChange.includes("filter") || style.willChange.includes("perspective") ||
    style.contain.includes("paint") || style.contain.includes("strict") || style.contain === "content";
}

function clipsContent(style: CSSStyleDeclaration) {
  return style.overflow !== "visible" || style.clipPath !== "none" ||
    style.contain.includes("paint") || style.contain.includes("strict") || style.contain === "content";
}

// overflowのクリップ境界はborder boxではなくpadding box
// The overflow clip edge is the padding box, not the border box
function paddingBox(node: HTMLElement, style: CSSStyleDeclaration): ClipRect {
  const box = node.getBoundingClientRect();
  return {
    left: box.left + (parseFloat(style.borderLeftWidth) || 0),
    top: box.top + (parseFloat(style.borderTopWidth) || 0),
    right: box.right - (parseFloat(style.borderRightWidth) || 0),
    bottom: box.bottom - (parseFloat(style.borderBottomWidth) || 0),
  };
}

function intersect(a: ClipRect, b: ClipRect): ClipRect {
  return { left: Math.max(a.left, b.left), top: Math.max(a.top, b.top),
    right: Math.min(a.right, b.right), bottom: Math.min(a.bottom, b.bottom) };
}
```

- [ ] **Step 2: `resolveAnchor.ts` に `clip` を載せる**

`moorestech_web/webui/src/shared/tutorialAnchor/resolveAnchor.ts` の1行目の前へ import を追加する:

```ts
import { ancestorClipRect, type ClipRect } from "./ancestorClip";
```

`export type ResolvedAnchor =` のブロックを次に置き換える:

```ts
export type ResolvedAnchor =
  | { status: "ready"; reason: "mounted"; rect: DOMRectReadOnly; clip: ClipRect }
  | { status: "not-found"; reason: "missing" | "duplicate-anchor" }
  | { status: "hidden"; reason: Exclude<AnchorReason, "mounted" | "missing" | "duplicate-anchor"> };
```

関数末尾の `return { status: "ready", reason: "mounted", rect };` を次に置き換える:

```ts
  return { status: "ready", reason: "mounted", rect, clip: ancestorClipRect(element) };
```

- [ ] **Step 3: `index.ts` から re-export する**

`moorestech_web/webui/src/shared/tutorialAnchor/index.ts` の `export type { ResolvedAnchor, AnchorReason } from "./resolveAnchor";` の直後に追加:

```ts
export { ancestorClipRect, clipPathInset } from "./ancestorClip";
export type { ClipRect } from "./ancestorClip";
```

- [ ] **Step 4: 既存テストと型検査を実行する**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/shared/tutorialAnchor`
Expected: PASS（既存テストが全て通る）

補足: `resolveAnchor.test.ts` は `document` と `getComputedStyle` を `vi.stubGlobal` で丸ごと差し替えており、`FakeElement` は `parentElement` を持たない。`ancestorClipRect` は `getComputedStyle(element).position` が `undefined`、`element.parentElement` が `undefined` になるので while ループへ入らず `{ left: 0, top: 0, right: 1280, bottom: 720 }` を返す。ready のアサーションは `toMatchObject` なので余分な `clip` があっても通る。

Run: `cd moorestech_web/webui && pnpm exec tsc -b`
Expected: `src/features/tutorial/TutorialOverlay.test.ts` で `clip` 欠落の型エラーが出る（Task 2 で解消する）。それ以外のエラーが出た場合は該当箇所を修正する

- [ ] **Step 5: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/tutorial-highlight-ancestor-clip
git add moorestech_web/webui/src/shared/tutorialAnchor/ancestorClip.ts \
        moorestech_web/webui/src/shared/tutorialAnchor/resolveAnchor.ts \
        moorestech_web/webui/src/shared/tutorialAnchor/index.ts
git commit -m "feat(webui): アンカーの祖先クリップ矩形をResolvedAnchorへ載せる"
```

---

### Task 2: ハイライトへ clip-path を当て、完全クリップ時は描画しない

**Files:**
- Modify: `moorestech_web/webui/src/features/tutorial/TutorialOverlay.tsx`
- Test: `moorestech_web/webui/src/features/tutorial/TutorialOverlay.test.ts`

**Interfaces:**
- Consumes: Task 1 の `clipPathInset(box: ClipRect, clip: ClipRect): string | null` と `ResolvedAnchor.clip`
- Produces: `[data-kind="outline"]` 要素が `style.clipPath` を持つ。完全クリップ時は要素そのものが存在しない

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_web/webui/src/features/tutorial/TutorialOverlay.test.ts` の `ready` ヘルパを次に置き換える（既存の呼び出し `ready(10)` / `ready(100)` はそのまま動く）:

```ts
const FULL_CLIP = { left: -100, top: -100, right: 1280, bottom: 820 };
const ready = (left: number, clip = FULL_CLIP): ResolvedAnchor => ({
  status: "ready", reason: "mounted",
  rect: { left, top: 0, width: 10, height: 10 } as DOMRectReadOnly,
  clip,
});
```

ファイル末尾（最後の `});` の後）に新しい describe を追加する:

```ts
describe("TutorialOverlay outline clipping", () => {
  afterEach(() => {
    mockState.presentation = null;
    mockState.listeners.clear();
  });

  it("祖先クリップの外へ出た辺はclip-pathで切られる", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "research.node-a")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });

    // rectは left:100 top:0 の 10x10、paddingPx:0 なのでboxは 100..110 / 0..10。右辺だけがclipに掛かる
    // The rect is 10x10 at left:100 top:0 with paddingPx:0 so the box spans 100..110 / 0..10; only the right side clips
    pushAnchor("research.node-a", ready(100, { left: -100, top: -100, right: 104, bottom: 820 }));

    const outlines = renderer.root.findAllByProps({ "data-kind": "outline" });
    expect(outlines.length).toBe(1);
    expect(outlines[0].props.style.clipPath).toBe("inset(-4px 6px -4px -4px)");
  });

  it("完全にクリップされた枠は要素ごと描かない", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "research.node-a")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });

    pushAnchor("research.node-a", ready(100, { left: -100, top: -100, right: 50, bottom: 820 }));

    expect(renderer.root.findAllByProps({ "data-kind": "outline" }).length).toBe(0);
  });

  // clipだけが変わった場合（コンテナのリサイズ等）に再描画されないとマスクが古いまま残る
  // A clip-only change (container resize etc.) must still re-render, or the mask goes stale
  it("rectが同値でもclipが変われば再描画する", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [outline("highlight-1", "research.node-a")] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });

    // クリップが遠い＝どの辺も切らない。グロー4pxを残すため全辺 -4px になる
    // A far-away clip cuts no side, so every side is -4px to preserve the 4px glow
    pushAnchor("research.node-a", ready(100, { left: -100, top: -100, right: 1280, bottom: 820 }));
    expect(renderer.root.findAllByProps({ "data-kind": "outline" })[0].props.style.clipPath)
      .toBe("inset(-4px -4px -4px -4px)");

    pushAnchor("research.node-a", ready(100, { left: -100, top: -100, right: 104, bottom: 820 }));
    expect(renderer.root.findAllByProps({ "data-kind": "outline" })[0].props.style.clipPath)
      .toBe("inset(-4px 6px -4px -4px)");
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/features/tutorial/TutorialOverlay.test.ts`
Expected: FAIL — 3件とも `style.clipPath` が `undefined`、および完全クリップのケースで要素が1件描かれている

- [ ] **Step 3: `renderOutline` を実装する**

`moorestech_web/webui/src/features/tutorial/TutorialOverlay.tsx` の `@/shared/tutorialAnchor` からの import 行を次に置き換える:

```ts
import { TutorialAnchorRegistry, clipPathInset, type ClipRect, type ResolvedAnchor } from "@/shared/tutorialAnchor";
```

`renderOutline` 関数の直前へ定数を追加する:

```ts
// style.module.css の .highlight が持つ box-shadow の広がり幅。clip-pathで削らないため外側へ逃がす
// The spread of .highlight's box-shadow in style.module.css; the clip is pushed out so it is not shaved off
const HIGHLIGHT_GLOW_PX = 4;
```

`renderOutline` 関数全体を次に置き換える:

```tsx
function renderOutline(key: string, element: TutorialOutlineElement, value: ResolvedAnchor | undefined) {
  if (!value || value.status !== "ready") return null;
  const padding = element.paddingPx;
  const box = {
    left: value.rect.left - padding, top: value.rect.top - padding,
    right: value.rect.left + value.rect.width + padding,
    bottom: value.rect.top + value.rect.height + padding,
  };
  // 祖先のoverflowで完全に隠れている間は要素ごと出さず、DOMと見た目を一致させる
  // While ancestor overflow hides it entirely, omit the element so the DOM matches what is painted
  const clipPath = clipPathInset(box, value.clip, HIGHLIGHT_GLOW_PX);
  if (clipPath === null) return null;
  return <div key={key} className={styles.highlight} data-kind={element.kind}
    style={{ left: box.left, top: box.top, width: box.right - box.left, height: box.bottom - box.top, clipPath }} />;
}
```

- [ ] **Step 4: `isSameAnchor` で clip も比較する**

同ファイルの `isSameAnchor` 関数全体を次に置き換える。矩形が動かずコンテナだけリサイズされた場合に再描画されず、マスクが古いまま残るのを防ぐ。

```ts
// 矩形とクリップは参照ではなく値で比較する。同値の再解決で再描画させないため
// Compare the rect and the clip by value, not by reference, so a same-valued re-resolve skips the re-render
function isSameAnchor(previous: ResolvedAnchor | undefined, value: ResolvedAnchor) {
  if (!previous || previous.status !== value.status || previous.reason !== value.reason) return false;
  if (previous.status !== "ready" || value.status !== "ready") return true;
  return previous.rect.left === value.rect.left && previous.rect.top === value.rect.top &&
    previous.rect.width === value.rect.width && previous.rect.height === value.rect.height &&
    isSameClip(previous.clip, value.clip);
}

function isSameClip(previous: ClipRect, value: ClipRect) {
  return previous.left === value.left && previous.top === value.top &&
    previous.right === value.right && previous.bottom === value.bottom;
}
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/features/tutorial/TutorialOverlay.test.ts`
Expected: PASS（新規3件を含め全件）

Run: `cd moorestech_web/webui && pnpm exec vitest run`
Expected: PASS（全ユニットテスト）

Run: `cd moorestech_web/webui && pnpm exec tsc -b && pnpm lint`
Expected: エラー0件・警告0件

- [ ] **Step 6: ファイル行数を確認する**

Run: `cd moorestech_web/webui && wc -l src/features/tutorial/TutorialOverlay.tsx src/shared/tutorialAnchor/ancestorClip.ts`
Expected: どちらも200行以下

- [ ] **Step 7: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/tutorial-highlight-ancestor-clip
git add moorestech_web/webui/src/features/tutorial/TutorialOverlay.tsx \
        moorestech_web/webui/src/features/tutorial/TutorialOverlay.test.ts
git commit -m "fix(webui): チュートリアルハイライトを祖先のoverflowクリップに合わせてマスクする"
```

---

### Task 3: 実UI経路 × ブラウザオラクルの e2e

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`
- Create: `moorestech_web/webui/e2e/tests/system/tutorialHighlightClip.spec.ts`

**Interfaces:**
- Consumes: Task 2 の `[data-kind="outline"]` 要素と `style.clipPath`
- Produces: `TopicScenario` に `"tutorialResearchNode"` が加わる（`setTopicScenario(page, "tutorialResearchNode")` で使える）

- [ ] **Step 1: 失敗する e2e spec を書く**

`moorestech_web/webui/e2e/tests/system/tutorialHighlightClip.spec.ts` を新規作成する:

```ts
import { expect, test, type Page } from "@playwright/test";
import { resetResearch, setTopicScenario, setUiState } from "../../support/mockControl";
import { settleBoundingBox } from "../../support/panSettle";
import { researchableNodeGuid } from "../../mock-host/researchFixtures";

const RESEARCH_NODE = `research-node-${researchableNodeGuid}`;
// mock-host の tutorialResearchNode シナリオの paddingPx と同じ値
// Same value as paddingPx in the mock host's tutorialResearchNode scenario
const PADDING_PX = 8;
// TutorialOverlay の HIGHLIGHT_GLOW_PX と同じ値。切れていない辺は枠がここまで外へ出る
// Same value as HIGHLIGHT_GLOW_PX in TutorialOverlay; intact sides extend this far outward
const GLOW_PX = 4;
const TOLERANCE_PX = 1.5;

type Rect = { left: number; top: number; right: number; bottom: number };

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "tutorialEmpty");
  await resetResearch(page);
  await setUiState(page, "PlayerInventory");
});

// アンカーの可視矩形はブラウザ自身に計算させる。祖先クリップ規則の正本はブラウザである
// Let the browser compute the anchor's visible rect; the browser is the authority on ancestor clipping
async function anchorRects(page: Page, testId: string) {
  return page.evaluate(async (id) => {
    const element = document.querySelector(`[data-testid="${id}"]`);
    if (!element) return null;
    const full = element.getBoundingClientRect();
    const entry = await new Promise<IntersectionObserverEntry>((resolve) => {
      const observer = new IntersectionObserver((entries) => { observer.disconnect(); resolve(entries[0]); }, { threshold: 0 });
      observer.observe(element);
    });
    const visible = entry.intersectionRect;
    return {
      full: { left: full.left, top: full.top, right: full.right, bottom: full.bottom },
      visible: visible.width <= 0.01 || visible.height <= 0.01
        ? null
        : { left: visible.left, top: visible.top, right: visible.right, bottom: visible.bottom },
    };
  }, testId);
}

// ハイライトの実可視領域を boundingBox と computed clip-path から復元する
// Reconstruct the highlight's actually visible region from its bounding box and computed clip-path
async function highlightVisibleRect(page: Page) {
  return page.evaluate(() => {
    const element = document.querySelector('[data-testid="tutorial-overlay"] [data-kind="outline"]');
    if (!element) return null;
    const box = element.getBoundingClientRect();
    // 計算値はCSS短縮形へ畳まれる（inset(-4px) / inset(10px 6px) 等）ので1〜4値を展開する
    // The computed value collapses to CSS shorthand (inset(-4px), inset(10px 6px)...), so expand 1..4 values
    const matched = /^inset\(([^)]*)\)$/.exec(getComputedStyle(element).clipPath);
    if (!matched) return { left: box.left, top: box.top, right: box.right, bottom: box.bottom };
    const parts = matched[1].trim().split(/\s+/).map((value) => Number(value.replace("px", "")));
    const [top, right = top, bottom = top, left = right] = parts;
    return {
      left: box.left + left, top: box.top + top,
      right: box.right - right, bottom: box.bottom - bottom,
    };
  });
}

// クリップされた辺ではハイライトの可視端がアンカーの可視端に一致し、切れていない辺ではpadding+glow分だけ外側になる
// On clipped sides the highlight's visible edge matches the anchor's; on intact sides it sits padding+glow outside
function expectMaskedLikeAnchor(highlight: Rect, anchor: { full: Rect; visible: Rect }) {
  const sides = [
    { key: "left" as const, sign: -1 }, { key: "top" as const, sign: -1 },
    { key: "right" as const, sign: 1 }, { key: "bottom" as const, sign: 1 },
  ];
  for (const side of sides) {
    const clipped = Math.abs(anchor.visible[side.key] - anchor.full[side.key]) > TOLERANCE_PX;
    const expected = clipped
      ? anchor.visible[side.key]
      : anchor.full[side.key] + side.sign * (PADDING_PX + GLOW_PX);
    expect(Math.abs(highlight[side.key] - expected), `${side.key} (clipped=${clipped})`).toBeLessThanOrEqual(TOLERANCE_PX);
  }
}

// 空背景を掴んでキャンバスをパンする。ノード上から始めるとクリック扱いになる
// Pan the canvas by grabbing the empty background; starting on a node would count as a click
async function dragViewport(page: Page, viewportBox: { x: number; y: number; width: number; height: number }, dx: number, dy: number) {
  const start = { x: viewportBox.x + viewportBox.width - 40, y: viewportBox.y + viewportBox.height - 40 };
  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(start.x + dx, start.y + dy, { steps: 10 });
  await page.mouse.up();
}

test("研究ノードのハイライトが祖先のoverflowクリップに合わせてマスクされる", async ({ page }, testInfo) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await setTopicScenario(page, "tutorialResearchNode");

  const node = page.getByTestId(RESEARCH_NODE);
  await expect(node).toBeVisible();
  const highlight = page.locator('[data-testid="tutorial-overlay"] [data-kind="outline"]');
  await expect(highlight).toBeVisible();

  // 1. ノードがビューポート中央にある状態: 枠は全周が描かれる
  // 1. The node sits centered in the viewport: the frame is drawn on all four sides
  const inside = await anchorRects(page, RESEARCH_NODE);
  expect(inside?.visible).not.toBeNull();
  expectMaskedLikeAnchor((await highlightVisibleRect(page))!, { full: inside!.full, visible: inside!.visible! });
  await page.screenshot({ path: testInfo.outputPath("clip-1-inside.png") });

  // 2. ノードがビューポート端をまたぐまでパンする: 枠がノードと同じ位置で切られる
  // 2. Pan until the node straddles the viewport edge: the frame is cut where the node is
  const viewportBox = (await page.getByTestId("research-viewport").boundingBox())!;
  await dragViewport(page, viewportBox, viewportBox.width / 2 - 40, 0);
  await settleBoundingBox(page, node);
  const partial = await anchorRects(page, RESEARCH_NODE);
  expect(partial?.visible).not.toBeNull();
  expect(partial!.visible!.right).toBeLessThan(partial!.full.right - TOLERANCE_PX);
  expectMaskedLikeAnchor((await highlightVisibleRect(page))!, { full: partial!.full, visible: partial!.visible! });
  await page.screenshot({ path: testInfo.outputPath("clip-2-partial.png") });

  // 3. ノードを完全に押し出す: 枠は要素ごと消える
  // 3. Push the node fully out: the frame disappears element and all
  await dragViewport(page, viewportBox, viewportBox.width, 0);
  await page.waitForFunction(() =>
    document.querySelector('[data-testid="tutorial-overlay"] [data-kind="outline"]') === null);
  const outside = await anchorRects(page, RESEARCH_NODE);
  expect(outside!.visible).toBeNull();
  await page.screenshot({ path: testInfo.outputPath("clip-3-outside.png") });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm exec tsc -p e2e/tsconfig.json --noEmit`
Expected: FAIL — `"tutorialResearchNode"` が `TopicScenario` に存在しないという型エラー

- [ ] **Step 3: mock-host にシナリオを追加する**

`moorestech_web/webui/e2e/mock-host/topics/topicControls.ts` の `import * as fx from "../fixtures";` の直前へ追加:

```ts
import { researchNodeAnchorId } from "../../../src/shared/tutorialAnchor/anchorIds";
```

`controls` テーブル内の `tutorialEmpty:` の行の直前へ、次のシナリオを追加する:

```ts
  // 研究ノードは TreeView の overflow:hidden 内にあり、パンでクリップ境界をまたげる
  // The research node lives inside TreeView's overflow:hidden, so panning moves it across the clip edge
  tutorialResearchNode: () => control(Topics.tutorialPresentation, {
    revision: 1,
    sessions: [{
      tutorialSessionId: "tutorial-session-research", challengeId: "tutorial-challenge-research",
      elements: [{
        kind: "outline" as const,
        elementId: "tutorial-highlight-research",
        anchorId: researchNodeAnchorId(fx.researchableNodeGuid),
        paddingPx: 8, blocksPointerInput: false,
      }],
    }],
  }),
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm exec tsc -p e2e/tsconfig.json --noEmit`
Expected: エラー0件

Run: `cd moorestech_web/webui && pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/system/tutorialHighlightClip.spec.ts`
Expected: PASS（1件）

失敗した場合、まず他セッションが同じポート 5273 で e2e を走らせていないか確認する（Global Constraints 参照）。

- [ ] **Step 5: 3枚のスクリーンショットを目視で確認する**

Run: `cd moorestech_web/webui && ls test-results/*/clip-*.png`
Expected: `clip-1-inside.png` / `clip-2-partial.png` / `clip-3-outside.png` の3枚が存在する

3枚を実際に開き、次を目で確認したうえでユーザーへ提示する:
- `clip-1-inside`: 黄色い枠がノードの四辺を囲んでいる
- `clip-2-partial`: ノードが TreeView の端で切れている位置で、枠も同じ位置で切れている（枠だけがパネル外へ伸びていない）
- `clip-3-outside`: パネル外に黄色い枠が一切残っていない

- [ ] **Step 6: e2e 全体が壊れていないことを確認する**

Run: `cd moorestech_web/webui && pnpm test:e2e`
Expected: 全 spec PASS。特に既存の `e2e/tests/system/tutorial.spec.ts`（クリップ祖先を持たないクロスヘアのハイライト・R3）と `e2e/tests/research/researchViewport.spec.ts` が無改修で通ること

- [ ] **Step 7: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/tutorial-highlight-ancestor-clip
git add moorestech_web/webui/e2e/mock-host/topics/topicControls.ts \
        moorestech_web/webui/e2e/tests/system/tutorialHighlightClip.spec.ts
git commit -m "test(webui): ハイライトのマスクを実UI経路とIntersectionObserverで検証する"
```

---

### Task 4: 全ブランチレビュー（省略不可）

**Files:**
- Modify: なし（レビュー指摘に応じて変更が生じる）

**Interfaces:**
- Consumes: Task 1〜3 の全コミット
- Produces: レビュー指摘への対応コミット

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

`moores-code-review` スキルを起動し、`fix/tutorial-highlight-ancestor-clip` の master からの全差分をレビューする。ゴール文言（「単純な変更だから」「テストが通っているから」等）による省略は不可。

- [ ] **Step 2: 指摘へ対応してコミットする**

機械的修正は適用し、設計判断が必要な指摘は `AskUserQuestion` でユーザーへ諮る。ADR 0023 の Consequences に「今回は許容する」と明記済みの3件（`dragGuide` 未対応・ack セマンティクス据え置き・「コ」の字欠け）は裁定済みであり、再指摘されても実装し直さない。

- [ ] **Step 3: 台帳を更新して派生タスクを積む**

タスク台帳 `moorestech-2j6n` を close する。理由には「outlineの祖先クリップマスクを実装。dragGuide・ackセマンティクス・コの字欠けはADR 0023で今回スコープ外と裁定」と書く。

続けて、スコープ外とした3件を新規 issue として積む（いずれも type=bug / priority=2、本 issue から派生した旨を残す）:

1. 「チュートリアルのD&Dガイド矢印が祖先のoverflowでクリップされない」— `renderDragGuide` は `renderOutline` と同じ body 直下 fixed portal にあり同じ問題を持つ。矢印は from→to を translate アニメーションで移動するため、どの時点のどのクリップ矩形を当てるかが未決（from側固定・to側固定・両者の和・アニメーション追従の4案）。ADR 0023 Considered Options 参照
2. 「チュートリアルハイライトのackが完全クリップ時もreadyのまま」— 祖先クリップで完全に隠れていても `status: "ready"` を ack するため Unity 側は表示できていると認識する。`hidden` / 新 reason `clipped` を返す案は、1pxだけ見えている状態を表示成功とみなすかの閾値が未裁定のため保留。ADR 0023 Consequences 参照
3. 「ハイライトのpaddingリングが端に密着したアンカーで枠が「コ」の字に欠ける」— `paddingPx` はアンカーの外側へ広がるため、クリップ端に密着したアンカーではノードが100%見えていてもリングの一辺だけ切られる。レシピ一覧（`.mantine-ScrollArea-viewport` に上パディング無し）とビルドメニュー（`.scroll` にパディング無し）の最上段で実発生する。手当て案はリングを内側へ逃がす / アンカー内側に描く。ADR 0023 Consequences 参照

---

## 判断記録（ADR）

設計セッションの正本: `docs/adr/0023-tutorial-highlight-ancestor-clip-mask.md` / `.decisions/2026-08-20-チュートリアルハイライトのマスク方式とテスト方針.md`

planning 中に新たに生じた判断:

- **`isSameAnchor` で `clip` の4値も比較する。** ノードが動かずコンテナだけリサイズされた場合、`rect` は同値だが `clip` が変わる。現行の実装は `rect` の4値しか見ないため、この経路で再描画されずマスクが古いまま残る。plan 執筆中に発見した。
  出所: agent前提（`TutorialOverlay.tsx` の `isSameAnchor` を読んで導出）
- **`clipPathInset` は完全に切り取られる場合 `null` を返し、真偽判定を呼び出し側へ出さない。** 「交差が空か」の判定を overlay 側に書くと同種の条件分岐が2箇所へ散る。
  出所: agent前提（AGENTS.md「同種の条件分岐は文脈が集まっている側の一箇所へ揃える」の適用）
- **ハイライトのボックスは `rect.right` / `rect.bottom` ではなく `rect.left + rect.width` から求める。** 既存 `TutorialOverlay.test.ts` のフェイク矩形は `{left, top, width, height}` しか持たず、`right` を読むと `NaN` になる。
  出所: agent前提（`TutorialOverlay.test.ts:46-49` を読んで導出）
- **`TutorialOverlay.test.ts` に clip-path の検証を3件追加する。** 型追従のためどのみち同ファイルを触る必要があり、「`clip` を計算したのに `clip-path` へ渡し忘れる」結線ミスを最も安いレベルで守れる。ユーザー裁定「テストは実UI経路×ブラウザオラクル1本」は、棄却された合成15形状オラクルテストとの二択に対する裁定であり、既存ユニットテストの型追従とその延長は別事項として扱う。不要と判断されれば削れる。
  出所: agent前提（裁定の射程を狭く解釈したうえでの追加）
- **テストヘルパ `ready()` にのみデフォルト引数を使う。** AGENTS.md はデフォルト引数を原則禁止するが、既存6箇所の呼び出しを機械的に書き換えるよりテストの意図が明確になる。プロダクションコードには使わない。
  出所: agent前提（規約の趣旨は呼び出し側の暗黙依存を防ぐことで、テストヘルパは射程外と解釈）
- **`clip-path` の下限は `0` ではなく `-HIGHLIGHT_GLOW_PX`（4px）にする。** `clip-path` の参照ボックスは border box なので `inset(0px)` でも `.highlight` の `box-shadow: 0 0 0 4px` による外側グローが切り落とされる。headless Chromium で実測し、無クリップ相当の `inset(0px)` を当てた枠だけグローが消えることを確認した。クリップの要らない辺を `-4px` にすると装飾が残る。
  出所: agent前提（plan 執筆中に実測して発見。R8 として要件化した）
- **グロー幅 4px は CSS（`style.module.css` の `box-shadow`）と TS（`HIGHLIGHT_GLOW_PX`）で二重に持つ。** `clip-path` は要素を描く前に決める必要があり、`getComputedStyle` で CSS から読む経路が無い。両ファイルへ相互参照コメントを置いて対処する。
  出所: agent前提（`box-shadow` の値を実行時に取得する経路が無いため）
- **e2e の `clip-path` パーサは CSS 短縮形を展開する。** Chrome の計算値は `inset(0px 0px 0px 0px)` → `inset(0px)`、`inset(10px 6px 10px 6px)` → `inset(10px 6px)` と畳まれる（実測）。4値固定の正規表現では無クリップ時に一致せず、誤った矩形で検証してしまう。
  出所: agent前提（plan 執筆中に実測して発見）
- **master pin の破損（台帳 `moorestech-hvwb`）は本planでは直さない。** 別担当が対応中の P0 であり、webui のみの本変更とは無関係。
  出所: agent前提（台帳の担当者を確認して回避）
