# カーソルツールチップ初回表示位置の修正 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** カーソルツールチップが、初回表示（新規ワールドで開幕スキットをスキップした直後など）でも `pointer` 基準の正しい位置に出るようにする。

**Architecture:** `CursorTooltip` の位置計算 `useLayoutEffect` が要素を `useRef` で読んでいるため、Mantine `Portal` の1描画遅れのマウントで空振りし、再実行されない。要素を callback ref（`useState` の setter）で受け、要素自体を effect の依存に入れて、要素が付いた時点で必ず計算が走るようにする。テストの `Portal` モックを本物と同じ遅延マウントにし、vitest と Playwright e2e の両方で回帰を固定する。

**Tech Stack:** React 18 / Mantine（`@mantine/core` の `Portal`・`Paper`）/ vitest 2 + react-test-renderer / Playwright（mock-host）

## Requirements

- R1: ツールチップが非表示の間に `pointer` が (640,360) に更新され、その後 `pointermove` が来ないまま表示に切り替わったとき、表示位置は `clampTooltipPosition(640, 360, 実寸幅, 実寸高, innerWidth, innerHeight)` の結果になる。初期値 (12,12) のまま残らない。受入: Task 1 の vitest と Task 2 の e2e が通る。
- R2: 既存の再計算（`pointer`・`data`・解決後テキスト・`locale` の変化で再計算）は維持する。受入: 既存の vitest「recalculates position when locale changes the resolved text」が通る。
- R3: 単体テストの `Portal` モックは、本物の `Portal`（初回描画で `null`、layout effect 後に子を出す）と同じ遅延マウントにする。受入: 修正前のコードに Task 1 のテストを当てると失敗する（Step 2 で確認）。
- R4: 実ゲームで、セーブを空にして「Play locally」→ 開幕スキットをスキップボタンでスキップ → mapObject を照準したとき、ツールチップがクロスヘア（画面中央）の右下近傍に出る。受入: Task 3 の実OS入力確認。
- やらないこと: Unity 側・wire 契約・ADR 0020 のカーソルワープの変更。プレイテスト基盤の不具合（`CefScreenMapper.TryBrowserToScreen` の上下逆転、注入マウスが実経路を通らない問題）は別 bead（moorestech-7179 の子）で扱う。

## Global Constraints

- 1ファイル200行未満（AGENTS.md）。`CursorTooltip.test.ts` は現在124行、`commonHud.spec.ts` は102行。
- コメントは日本語1行＋英語1行の2行セット（AGENTS.md「コメント」）。
- webui の依存配備は `pnpm install`（npm ci・node_modules の cp は不可）。
- e2e の mock-host は固定ポート5273を全セッションで共有する。待機と実行は1コマンドに繋げる: `until ! lsof -ti :5273 >/dev/null 2>&1; do sleep 5; done && pnpm test:e2e ...`
- 設計の正: `docs/adr/0074-cursor-tooltip-recomputes-position-when-element-attaches.md`

## File Structure

| ファイル | 変更 | 責務 |
|---|---|---|
| `moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx` | Modify | 要素を callback ref で受け、要素の付与を位置計算のきっかけに加える |
| `moorestech_web/webui/src/shared/tooltip/CursorTooltip.test.ts` | Modify | `Portal` モックを遅延マウントにし、初回表示の回帰テストを足す |
| `moorestech_web/webui/e2e/tests/system/commonHud.spec.ts` | Modify | 実 `Portal` での初回表示位置を e2e で固定する |

既存部品: 位置のクランプは既存の `clampTooltipPosition`（`src/shared/tooltip/tooltipPosition.ts`）を呼ぶ（変更しない）。e2e の topic 切替は既存の `setTopicScenario`／`setUiState`（`e2e/support/mockControl.ts`）を呼ぶ。新規ファイルは無い。

---

### Task 1: 要素付与で位置を計算する（vitest で固定）

**Files:**
- Modify: `moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx`
- Test: `moorestech_web/webui/src/shared/tooltip/CursorTooltip.test.ts`

**Interfaces:**
- Consumes: なし
- Produces: `CursorTooltip` の公開シグネチャは不変（`export function CursorTooltip()`）。`data-testid="cursor-tooltip"` の要素の `style` は `{ left, top }` のまま。

- [x] **Step 1: テストの `Portal` モックを遅延マウントにし、clamp モックを引数依存にする**

`CursorTooltip.test.ts` の `vi.hoisted` と `vi.mock("@mantine/core", ...)` を次に置き換える。

```ts
const testState = vi.hoisted(() => ({
  locale: "english",
  data: {
    visible: true,
    lines: [{ textKey: "ui.mainMenu.playLocally", textParams: [] as string[] }],
  } as { visible: boolean; lines: { textKey: string; textParams: string[] }[] },
  // 渡された pointer に応じた値を返し、どの pointer で計算したかを描画結果から読めるようにする
  // Return a pointer-dependent value so the rendered style reveals which pointer the calculation used
  clamp: vi.fn((x: number, y: number) => ({ x: x + 12, y: y + 12 })),
}));

// 本物の Portal と同じく初回描画では子を出さず、layout effect の後に出す
// Like the real Portal, render nothing on the first pass and the children only after a layout effect
vi.mock("@mantine/core", async () => {
  const { useLayoutEffect, useState } = await import("react");
  return {
    Paper: forwardRef((props: Record<string, unknown>, ref) => createElement("div", { ...props, ref })),
    Portal: ({ children }: { children: unknown }) => {
      const [mounted, setMounted] = useState(false);
      useLayoutEffect(() => setMounted(true), []);
      return mounted ? children : null;
    },
  };
});
```

- [x] **Step 2: 初回表示の回帰テストを書き、修正前に失敗することを確認する**

`describe("CursorTooltip", ...)` の末尾（既存の locale テストの後）に追加する。

```ts
  it("positions from the latest pointer when the tooltip first appears without further pointer moves", () => {
    let pointerMove: ((event: { clientX: number; clientY: number }) => void) | undefined;
    vi.stubGlobal("window", {
      addEventListener: vi.fn((type: string, listener: (event: { clientX: number; clientY: number }) => void) => {
        if (type === "pointermove") pointerMove = listener;
      }),
      removeEventListener: vi.fn(),
      innerWidth: 1280,
      innerHeight: 720,
    });
    testState.data = { visible: false, lines: [] };
    // pointermove リスナーを登録する useEffect を流すため、生成を act で包む
    // Wrap creation in act so the useEffect that registers the pointermove listener is flushed
    let renderer!: ReturnType<typeof create>;
    act(() => {
      renderer = create(createElement(CursorTooltip), {
        createNodeMock: () => ({ getBoundingClientRect: () => ({ width: 120, height: 40 }) }),
      });
    });

    // 非表示中にロック前ワープの中央座標だけが届き、その後は pointermove が来ない状況を再現する
    // Reproduce only the pre-lock centered warp arriving while hidden, with no pointermove afterwards
    act(() => pointerMove?.({ clientX: 640, clientY: 360 }));
    act(() => {
      testState.data = { visible: true, lines: [{ textKey: "ui.mainMenu.playLocally", textParams: [] }] };
      renderer.update(createElement(CursorTooltip));
    });

    expect(testState.clamp).toHaveBeenLastCalledWith(640, 360, 120, 40, 1280, 720);
    expect(renderer.root.findByProps({ "data-testid": "cursor-tooltip" }).props.style).toEqual({ left: 652, top: 372 });
  });
```

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/tooltip/CursorTooltip.test.ts`
Expected: 新テストだけが FAIL（`toHaveBeenLastCalledWith` が一致しない）。既存の6件は PASS（2026-10-01 に plan 作成時の事前検証で実測: 1 failed | 6 passed）。

- [x] **Step 3: callback ref で要素を受け、要素を依存に入れる**

`CursorTooltip.tsx` を次のとおり変更する。import から `useRef` を外す。

```tsx
import { useEffect, useLayoutEffect, useState } from "react";
```

```tsx
export function CursorTooltip() {
  const data = useTopic(Topics.tooltip);
  const { locale, t } = useI18n();
  // Portal は子を1描画遅れて出すため、要素の付与そのものを位置計算のきっかけにする（ADR 0074）
  // Portal mounts children one render late, so the element attaching itself triggers the position calculation (ADR 0074)
  const [element, setElement] = useState<HTMLDivElement | null>(null);
  const [pointer, setPointer] = useState({ x: 0, y: 0 });
  const [position, setPosition] = useState({ x: 12, y: 12 });
```

```tsx
  useLayoutEffect(() => {
    if (!element) return;
    // offsetWidthはtransform前の実装寸法なので、--ui-scale拡縮後の実寸を返すrectで画面端を判定する
    // offsetWidth is the pre-transform layout size, so the rect's post-scale dimensions decide the screen-edge clamp
    const rect = element.getBoundingClientRect();
    setPosition(clampTooltipPosition(pointer.x, pointer.y, rect.width, rect.height, window.innerWidth, window.innerHeight));
  }, [element, pointer, data, text, locale]);
```

```tsx
      <Paper ref={setElement} className={styles.tooltip} data-testid="cursor-tooltip" style={{ left: position.x, top: position.y }}>
```

- [x] **Step 4: テストが通ることを確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/tooltip/ && pnpm exec tsc --noEmit -p .`
Expected: `CursorTooltip.test.ts` と `tooltipPosition.test.ts` の全7件が PASS（事前検証で実測: 7 passed）。型エラー無し。

- [x] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx moorestech_web/webui/src/shared/tooltip/CursorTooltip.test.ts
git commit -m "fix(webui): カーソルツールチップの位置を要素付与時にも計算し初回表示の左上固定を直す"
```

### Task 2: 実 Portal での初回表示位置を e2e で固定する

**Files:**
- Test: `moorestech_web/webui/e2e/tests/system/commonHud.spec.ts`

**Interfaces:**
- Consumes: Task 1 の `CursorTooltip`（`data-testid="cursor-tooltip"`）、既存の `setTopicScenario(page, "tooltip" | "tooltipHidden")`・`setUiState(page, "GameScreen")`
- Produces: なし

- [ ] **Step 1: e2e テストを書く**

`commonHud.spec.ts` の「ツールチップは複数行を順序どおり縦積みで表示する」テストの後に追加する。

```ts
test("ツールチップは非表示中に届いたpointer位置へ初回表示から出る", async ({ page }) => {
  await setUiState(page, "GameScreen");
  await page.goto("/");
  // 非表示中にロック前ワープの中央座標だけが届き、表示後はpointermoveが来ない実プレイの状況を再現する
  // Reproduce real play: only the pre-lock centered warp arrives while hidden, and no pointermove follows the show
  await page.mouse.move(640, 360);
  await setTopicScenario(page, "tooltip");

  const tooltip = page.getByTestId("cursor-tooltip");
  await expect(tooltip).toBeVisible();
  const box = await tooltip.boundingBox();
  expect(box?.x).toBeGreaterThan(600);
  expect(box?.y).toBeGreaterThan(330);
});
```

- [ ] **Step 2: 修正前のコードで失敗することを確認する**

Task 1 のコミット済みの修正を作業ツリー上だけ一時的に戻し（`git checkout HEAD~1 -- moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx`。HEAD は Task 1 のコミット）、e2e を実行する。

Run: `cd moorestech_web/webui && until ! lsof -ti :5273 >/dev/null 2>&1; do sleep 5; done && pnpm test:e2e -- tests/system/commonHud.spec.ts`
Expected: 新テストが FAIL（`box.x` が 12 付近）。確認後 `git checkout HEAD -- moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx` で修正へ戻し、`git status` で差分が無いことを確かめる。

- [ ] **Step 3: 修正後のコードで通ることを確認する**

Run: `cd moorestech_web/webui && until ! lsof -ti :5273 >/dev/null 2>&1; do sleep 5; done && pnpm test:e2e -- tests/system/commonHud.spec.ts`
Expected: `commonHud.spec.ts` の全テストが PASS。失敗するspecが実行ごとに変わる場合は、5273番ポートを共有する他セッションの影響を疑い、単独で3回再実行して切り分ける。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_web/webui/e2e/tests/system/commonHud.spec.ts
git commit -m "test(webui): 実Portalでのツールチップ初回表示位置をe2eで固定する"
```

### Task 3: 実OS入力で初回表示位置を確認する

自動プレイテストの注入マウスは実プレイの経路（legacy `Input.mousePosition`）を通らず、この不具合を再現できない（ADR 0074）。そのため、実OS入力（cliclick・osascript）で確認する。

**Files:** なし（確認のみ。セーブは退避して戻す）

**Interfaces:**
- Consumes: Task 1 の修正を含む webui（Editor の WebUiHost が配信するもの）
- Produces: なし

- [ ] **Step 1: 前提を整える**

worktree の `moorestech_web/node` が無ければメインからコピーする（`cp -Rc <メイン>/moorestech_web/node <worktree>/moorestech_web/node`）。`moorestech_web/webui` で `pnpm install --frozen-lockfile` を実行する。`moores-wt status` で自分の Editor を確認する。

- [ ] **Step 2: セーブを退避する（削除しない）**

```bash
SV="$HOME/Library/Application Support/moorestech/Saves"
mv "$SV/world_1" "$SV/world_1.bak-tooltip-verify"
```

- [ ] **Step 3: メインメニューから実入力で再現手順を踏む**

`uloop execute-dynamic-code` で `EditorSceneManager.playModeStartScene` に `Assets/Scenes/Game/MainMenu.unity` を設定し、`EditorApplication.EnterPlaymode()` で起動する。以降の入力は、`osascript` で自分の Editor（pid は `moores-wt status` で確認）を前面にしてから行う。

1. `cliclick` でポップアップ（同意・クラッシュ報告）を閉じ、「Play locally」を押す。
2. スキット開始を待ち、マウスを Game ビュー上で動かしてから、右上のスキップボタンを押す。
3. EDC でプレイヤーを最寄りの小石（`c74efe49-52f3-403b-9c9a-b39eb1c85fce`）の正面へワープさせ、照準させる。

クリックは `cliclick m:X,Y w:500 dd:X,Y w:300 du:X,Y` の形で押下時間を持たせる。前面が他セッションの Editor に奪われていないか、`osascript -e 'tell application "System Events" to get unix id of first process whose frontmost is true'` で毎回確かめる。

- [ ] **Step 4: 表示位置を確かめる**

`screencapture` で Game ビューを撮り、ツールチップがクロスヘア（画面中央）の右下近傍に出ていること、左上に出ていないことを目視で確認する。あわせて EDC で `PlaytestDomQuery.Query("cursor-tooltip", 3f)` を呼び、CSS 位置が (652, 372) 付近（画面 1280x720 のとき）であることを確認する。

- [ ] **Step 5: 後片付け**

PlayMode を停止し、`playModeStartScene` を `null` に戻す。テストで作られた `world_1` を `world_1.tooltip-verify-generated` へ退避し、`world_1.bak-tooltip-verify` を `world_1` へ戻す。

### Task 4: 全ブランチレビューと PR 作成

必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。

- [ ] **Step 1: moores-code-review でブランチ全体をレビューする**
- [ ] **Step 2: 指摘を反映する。** 反映が `CursorTooltip.tsx` の位置計算に触れた場合は、Task 1 Step 4・Task 2 Step 3・Task 3 を反映後のコードで再実施する。e2e の合否は、期待テストの PASS に加え、実行ログに失敗・リトライ・ポート衝突（`already used`）が無いことで判定する。
- [ ] **Step 3: 残課題を起票する。** 未検証・未確認の項目があれば1件ずつ bd に起票し、PR 本文に bead ID を列挙する。プレイテスト基盤の2件は起票済み（moorestech-7179 の子）なので、PR 本文で参照する。
- [ ] **Step 4: 全作業をコミットし、push して pr-create スキルで PR を作る。** PR 本文に bead moorestech-7179 と ADR 0074 を記載する。
- [ ] **Step 5: PR 作成後に `moores-wt rm tooltip-topleft-after-skip` で worktree と Editor を畳む。**

---

## 進捗メモ

Task 1 は plan 作成時の事前検証で実装・実測・コミットまで済んでいる（修正なし: 1 failed | 6 passed、修正あり: 7 passed）。実装セッションは Task 2 から始める。

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 判定 |
|---|---|---|---|---|
| 1 | `CursorTooltip` の要素参照を callback ref（`useState` setter）へ | `webui/src/shared/tooltip`（既存） | React state を ref に渡す標準形 | ok。同コンポーネント内の変更で、層の移動は無い |
| 2 | テストの `Portal` 遅延マウントモック | `CursorTooltip.test.ts`（既存） | vi.mock | ok。既存モックの置き換え |
| 3 | e2e テスト追加 | `e2e/tests/system/commonHud.spec.ts`（既存） | 既存 `setTopicScenario`・`setUiState` | ok。同ファイルの既存 tooltip テストと同じ形 |

データフロー: OS カーソル →（CefUnity）→ DOM `pointermove` → `pointer` state → **位置計算（ここだけ変更）** → `position` state → 描画。新しい経路・書き手は足さない。機能パリティ: ツールチップの表示内容・クランプ・書式・`ui.visibility` 退避は不変。

## 判断記録（ADR）

- 設計の正: `docs/adr/0074-cursor-tooltip-recomputes-position-when-element-attaches.md`（出所欄はADR参照。修正方針・テストモック・プレイテスト基盤の別起票はユーザー裁定 2026-10-01「ok」）。
- `.decisions/2026-10-01-カーソルツールチップは要素付与時に表示位置を計算する.md`
- ADR 0020「実装後の実測」節に、ADR 0074 への訂正注記を追記した（決定は維持）。出所: agent判断。
- e2e を vitest に加えて足す: 出所: agent判断（vitest の `Portal` はモックであり、本物の Mantine `Portal` の遅延マウントを検証できるのは e2e だけ）。
- 実ゲームでの最終確認を自動プレイテストでなく実OS入力で行う: 出所: agent判断（注入マウスが実経路を通らないことを 2026-10-01 に実測。ユーザー発言「変な回避ルートじゃなく真のカーソル移動をしたら再現しないの」）。
- 実装を単一の `useState` callback ref にする（`ResizeObserver` 等は使わない）: 出所: agent判断（要求は「要素付与で計算を走らせる」ことだけで、サイズ変化は既存の `text`・`locale` 依存で足りる）。
