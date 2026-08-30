# ビルドメニュー1本スクロール化＋カテゴリジャンプサイドバー Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Web UIビルドメニューのカテゴリ切替を廃止し、全カテゴリを「カテゴリ大見出し → サブカテゴリ小見出し＋SlotGrid」の2階層で1本のスクロールに並べ、左サイドバーを「カテゴリ大見出しへのスムーズスクロールジャンプ＋scroll-spyハイライト」にする（ADR 0045）。

**Architecture:** `logic/buildMenuGrouping.ts` を「カテゴリ群（`BuildMenuCategoryGroup[]`）を返す純関数＋検索絞り込み純関数」へ置き換え、scroll-spyの判定は `logic/buildMenuScrollSpy.ts` の純関数に切り出す。DOM都合（視口・見出しoffset・スムーズスクロール・末尾スペーサ高）は `hooks/useBuildMenuCategoryScroll.ts` の1フックへ閉じ込め、`BuildMenuPanel.tsx` はフックが返す `activeCategoryGuid` / `jumpTo` / `headingRef` を配線するだけにする。サイドバーは引き続き `shared/ui/ModeSwitch`（縦）を使い、検索ヒット無しカテゴリを個別に無効化するため `ModeSwitchOption` に汎用の `disabled?: boolean` を足す。

**Tech Stack:** React 18 + TypeScript（moorestech_web/webui）、Mantine `ScrollArea`、vitest + react-test-renderer、Playwright e2e（mock-host）、pnpm。

## Requirements

- R1: 全カテゴリ（エントリのあるものだけ・マスタ定義順）を1本のスクロールに「カテゴリ大見出し → サブカテゴリ小見出し（定義順）＋8列SlotGrid」で並べる。受け入れ: `groupBuildMenuCategories` のユニットテストで「空カテゴリ除外・定義順・空サブカテゴリ除外・配信順維持」が通り、e2eで `build-menu-category-heading-<logistics>` と `build-menu-category-heading-<transport>` が同時にDOMに存在する
- R2: サイドバー項目はカテゴリ単位（エントリのあるカテゴリのみ、`build-menu-category-<guid>` testid維持）。押すとそのカテゴリ大見出しが視口上端に来るようスムーズスクロールする。受け入れ: e2eでtransportを押した後 `expect.poll` で視口 `scrollTop` が見出しの `offsetTop`（±1px）に一致する
- R3: サイドバーのハイライト（`data-selected`）は視口上端にあるカテゴリに追従する（scroll-spy）。手スクロールでも変わる。受け入れ: `activeCategoryAtScroll` のユニットテスト（先頭・中間・境界ちょうど・末尾）と、e2eで `scrollTop` を直接書き換えた後に該当ボタンが `aria-pressed="true"` になる
- R4: スムーズスクロール中はジャンプ先の項目をハイライト固定し、到達後にscroll-spyへ戻す。受け入れ: フック内の「目標到達で固定解除」ロジックを純関数 `isJumpSettled` に切り出しユニットテストする
- R5: 末尾カテゴリへジャンプしても見出しが視口上端に来るよう、リスト末尾にスペーサ（視口高 − 末尾カテゴリ群高、負なら0）を置く。受け入れ: `trailingSpacerHeight` のユニットテスト（不足分・0クランプ）と、e2eで末尾カテゴリ（`tool`）へジャンプ後 `scrollTop` が見出し `offsetTop`（±1px）に一致する
- R6: 検索は同じ2階層リストの絞り込み。ヒットの無いカテゴリ/サブカテゴリは非表示。サイドバーは検索中も有効で、ヒットの無いカテゴリ項目だけ `disabled`。「該当なし」は0件時のみ。検索専用の複合見出し（`カテゴリ / サブ`）は廃止。受け入れ: e2e「鉄」検索でlogistics・transport見出し可視、`build-menu-sidebar` に `data-disabled` が付かず、ヒット無しカテゴリのボタンが `disabled`
- R7: カテゴリ大見出しは本文色（`--text-default`）の大きめラベル＋`FadeRule`、上にカテゴリ間余白。サブカテゴリ小見出しは現行のまま。受け入れ: `style.module.css` に `.categoryHeading`（`color: var(--text-default)`, `font-size: var(--label-face-font-size)`）と `.categoryGroup + .categoryGroup { margin-top: … }` がある
- R8: セッション内保持から `categoryGuid` を削除。`query` / `scrollTop` / `hoveredEntryId` は従来どおり復元。受け入れ: `buildMenuSessionState.test.ts` の期待オブジェクトに `categoryGuid` が無い。e2e「閉じて開き直す」で scrollTop 40 とsticky詳細が復元される
- R9: 既存の8列・スクロールバー予約・チュートリアルアンカーclip逃げ・サイドバー固定高（`buildMenuLayout.spec.ts`）は全て維持。受け入れ: `pnpm test:e2e buildMenuLayout` 緑
- R10: `webui-design` §8.11 をADR 0045の内容へ改定する（縦ModeSwitch＝ジャンプ＋scroll-spy、2階層見出し、検索の扱い、セッション保持からカテゴリ除去）
- やらないこと: Unity `BuildMenuView.cs` の変更、マスタ/プロトコルの変更、`PlaytestUiOps.cs` の変更（カテゴリクリック＝ジャンプとして既に成立する）、localization.csvの追加（カテゴリ名キーは既存）、仮想化などの性能対策

## Global Constraints

- 作業は `moores-wt new feature/build-menu-single-scroll` で切った使い捨てworktreeで行う（CLAUDE.local.md）。PR作成後に `moores-wt rm`。Unity Editorは不要（`--no-editor` 可。.cs変更なしのためコンパイルゲート対象外）
- webuiは `pnpm`。コマンドはすべて `moorestech_web/webui` で実行。単体: `pnpm vitest run <path>`、全体: `pnpm test`、lint: `pnpm lint`、e2e: `pnpm test:e2e <specファイル名の一部>`（e2eはポート5273を他セッションと共有する。失敗specが毎回変わる場合はポート衝突を疑う）
- 1ファイル200行以下・1ディレクトリ10ファイル以下（`features/buildMenu/` 直下は現在7ファイル＋`logic/`＋`sessionState/`。新規フックは `hooks/` サブディレクトリへ）
- コメント規約: 主要セクションに「// 日本語 → // English」の2行セット、各1行。自明なコメントは書かない
- `Func<>`/partial等のC#規約はTS側に該当しないが、「イベント発火にActionを使わない」はTSでは該当なし。状態変化はReact stateとscrollイベント購読で扱い、`setInterval`等のポーリングは禁止
- `ModeSwitch` 等 `shared/ui` にはドメイン語彙（カテゴリ・ビルドメニュー）を持ち込まない
- コミットは通常マージ運用・Squash禁止。コミット末尾に `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` 行
- `.decisions/` 5件（2026-08-30 ビルドメニュー*）とADR 0045は既にmaster相当に存在する前提（本planと同じコミットで入る）

---

### Task 1: グルーピングロジックを「カテゴリ群」型へ置き換える

**Files:**
- Modify: `moorestech_web/webui/src/features/buildMenu/logic/buildMenuGrouping.ts`
- Modify: `moorestech_web/webui/src/features/buildMenu/logic/buildMenuGrouping.test.ts`

**Interfaces:**
- Consumes: `BuildMenuCategory { categoryGuid: string; subCategoryGuids: string[] }`, `BuildMenuEntryData`（`bridge/contract/payloadTypes`）
- Produces:
  - `type BuildMenuSection = { categoryGuid: string; subCategoryGuid: string; entries: BuildMenuDisplayEntry[] }`（既存のまま）
  - `type BuildMenuCategoryGroup = { categoryGuid: string; sections: BuildMenuSection[] }`
  - `groupBuildMenuCategories(categories: BuildMenuCategory[], entries: BuildMenuDisplayEntry[]): BuildMenuCategoryGroup[]`（エントリ0のカテゴリ・サブカテゴリを除外、定義順）
  - `searchBuildMenuEntries(query: string, entries: BuildMenuDisplayEntry[]): BuildMenuDisplayEntry[]`（表示名の大文字小文字無視部分一致。空文字なら全件）
  - `localizeBuildMenuEntries` / `BuildMenuDisplayEntry` は既存のまま
  - 削除: `visibleCategories` / `resolveSelectedCategory` / `sectionsForCategory` / `searchSections`

- [x] **Step 1: 失敗するテストを書く**

`buildMenuGrouping.test.ts` の `describe("visibleCategories")` / `describe("resolveSelectedCategory")` / `describe("sectionsForCategory")` / `describe("searchSections")` ブロックを削除し、importを差し替えて以下を追加する（`localizeBuildMenuEntries` 系の既存describeは残す）:

```ts
import {
  groupBuildMenuCategories,
  localizeBuildMenuEntries,
  searchBuildMenuEntries,
} from "./buildMenuGrouping";

describe("groupBuildMenuCategories", () => {
  it("エントリの無いカテゴリを除外し定義順のカテゴリ群を返す", () => {
    const groups = groupBuildMenuCategories(categories, entries);
    expect(groups.map((g) => g.categoryGuid)).toEqual([miningCategoryGuid, logisticsCategoryGuid]);
  });
  it("各カテゴリ内はサブカテゴリ定義順で空サブカテゴリを除外する", () => {
    const logistics = groupBuildMenuCategories(categories, entries)[1];
    expect(logistics.sections.map((s) => s.subCategoryGuid)).toEqual([chestSubCategoryGuid, conveyorSubCategoryGuid]);
    expect(logistics.sections[0].entries.map((e) => e.displayLabel)).toEqual(["木のチェスト"]);
    expect(logistics.sections[0].categoryGuid).toBe(logisticsCategoryGuid);
  });
  it("エントリが空なら空配列", () => {
    expect(groupBuildMenuCategories(categories, [])).toEqual([]);
  });
});

describe("searchBuildMenuEntries", () => {
  it("表示名の部分一致で大文字小文字を無視して絞り込む", () => {
    expect(searchBuildMenuEntries("鉄", entries).map((e) => e.displayLabel)).toEqual(["鉄の採掘機"]);
    expect(searchBuildMenuEntries("ベルト", entries).map((e) => e.displayLabel)).toEqual(["ベルトコンベア"]);
  });
  it("空文字は全件を返す", () => {
    expect(searchBuildMenuEntries("", entries)).toHaveLength(3);
  });
  it("不一致は空配列", () => {
    expect(searchBuildMenuEntries("存在しない", entries)).toEqual([]);
  });
  it("絞り込み結果をグルーピングするとヒットの無いカテゴリが消える", () => {
    const groups = groupBuildMenuCategories(categories, searchBuildMenuEntries("鉄", entries));
    expect(groups.map((g) => g.categoryGuid)).toEqual([miningCategoryGuid]);
  });
});
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `pnpm vitest run src/features/buildMenu/logic/buildMenuGrouping.test.ts`
Expected: FAIL（`groupBuildMenuCategories is not a function` 等）

- [x] **Step 3: 実装を書く**

`buildMenuGrouping.ts` を以下の全文で置き換える:

```ts
import type { BuildMenuCategory, BuildMenuEntryData } from "../../../bridge/contract/payloadTypes";
import type { TranslationKey } from "../../../shared/i18n";
import { localizeSelectableTargetName, placementTargetOf } from "../../../shared/placementTarget";

export type BuildMenuSection = {
  categoryGuid: string;
  subCategoryGuid: string;
  entries: BuildMenuDisplayEntry[];
};

// 1本スクロールの単位。カテゴリ大見出し1つとその下のサブカテゴリ群
// Unit of the single scroll: one category heading and its sub-category sections
export type BuildMenuCategoryGroup = {
  categoryGuid: string;
  sections: BuildMenuSection[];
};

export type BuildMenuDisplayEntry = BuildMenuEntryData & { displayLabel: string };

// 各エントリを共有の表示名解決へ回す
// Route every entry through the shared display-name resolution
export function localizeBuildMenuEntries(
  entries: BuildMenuEntryData[],
  translate: (key: TranslationKey) => string,
): BuildMenuDisplayEntry[] {
  return entries.map((entry) => ({
    ...entry,
    displayLabel: localizeSelectableTargetName(placementTargetOf(entry), translate),
  }));
}

// カテゴリ定義順→サブカテゴリ定義順で群化し、空の群は落とす。エントリ並びは配信順（sortPriority昇順）を維持
// Group by category then sub-category definition order, dropping empty groups; entry order stays as delivered
export function groupBuildMenuCategories(
  categories: BuildMenuCategory[],
  entries: BuildMenuDisplayEntry[],
): BuildMenuCategoryGroup[] {
  return categories
    .map((category) => ({
      categoryGuid: category.categoryGuid,
      sections: category.subCategoryGuids
        .map((subCategoryGuid) => ({
          categoryGuid: category.categoryGuid,
          subCategoryGuid,
          entries: entries.filter((entry) =>
            entry.categoryGuid === category.categoryGuid && entry.subCategoryGuid === subCategoryGuid),
        }))
        .filter((section) => section.entries.length > 0),
    }))
    .filter((group) => group.sections.length > 0);
}

// 表示名の部分一致検索（大文字小文字無視）。空文字は全件
// Case-insensitive substring search on display labels; empty query returns everything
export function searchBuildMenuEntries(query: string, entries: BuildMenuDisplayEntry[]): BuildMenuDisplayEntry[] {
  if (query === "") return entries;
  const lowered = query.toLowerCase();
  return entries.filter((entry) => entry.displayLabel.toLowerCase().includes(lowered));
}
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `pnpm vitest run src/features/buildMenu/logic/buildMenuGrouping.test.ts`
Expected: PASS（この時点で `BuildMenuPanel.tsx` は型エラーだが、vitestは単体ファイルなので影響なし）

- [x] **Step 5: コミットする**

```bash
git add src/features/buildMenu/logic/buildMenuGrouping.ts src/features/buildMenu/logic/buildMenuGrouping.test.ts
git commit -m "refactor(webui): build menu grouping returns category groups for single scroll"
```

---

### Task 2: scroll-spy／ジャンプ到達／末尾スペーサの純関数

**Files:**
- Create: `moorestech_web/webui/src/features/buildMenu/logic/buildMenuScrollSpy.ts`
- Create: `moorestech_web/webui/src/features/buildMenu/logic/buildMenuScrollSpy.test.ts`

**Interfaces:**
- Produces:
  - `type CategoryHeadingOffset = { categoryGuid: string; top: number }`（視口内容座標での見出し上端）
  - `activeCategoryAtScroll(offsets: CategoryHeadingOffset[], scrollTop: number): string | null`
  - `isJumpSettled(scrollTop: number, targetTop: number): boolean`
  - `trailingSpacerHeight(viewportHeight: number, lastGroupHeight: number): number`
  - `export const scrollSettleTolerancePx = 1`

- [x] **Step 1: 失敗するテストを書く**

```ts
import { describe, expect, it } from "vitest";
import { activeCategoryAtScroll, isJumpSettled, trailingSpacerHeight } from "./buildMenuScrollSpy";

const offsets = [
  { categoryGuid: "a", top: 0 },
  { categoryGuid: "b", top: 300 },
  { categoryGuid: "c", top: 720 },
];

describe("activeCategoryAtScroll", () => {
  it("先頭より上は先頭カテゴリ", () => {
    expect(activeCategoryAtScroll(offsets, 0)).toBe("a");
  });
  it("見出しの間は直前の見出しのカテゴリ", () => {
    expect(activeCategoryAtScroll(offsets, 298)).toBe("a");
    expect(activeCategoryAtScroll(offsets, 500)).toBe("b");
  });
  it("見出し上端ちょうど（±1px）はその見出しのカテゴリ", () => {
    expect(activeCategoryAtScroll(offsets, 300)).toBe("b");
    expect(activeCategoryAtScroll(offsets, 719)).toBe("c");
  });
  it("末尾を越えても末尾カテゴリ", () => {
    expect(activeCategoryAtScroll(offsets, 5000)).toBe("c");
  });
  it("見出しが無ければnull", () => {
    expect(activeCategoryAtScroll([], 10)).toBeNull();
  });
});

describe("isJumpSettled", () => {
  it("目標±1px以内で到達", () => {
    expect(isJumpSettled(299.4, 300)).toBe(true);
    expect(isJumpSettled(301, 300)).toBe(true);
  });
  it("それ以上離れていれば未到達", () => {
    expect(isJumpSettled(297, 300)).toBe(false);
  });
});

describe("trailingSpacerHeight", () => {
  it("末尾群が視口より短ければ差分を返す", () => {
    expect(trailingSpacerHeight(600, 220)).toBe(380);
  });
  it("末尾群が視口以上なら0", () => {
    expect(trailingSpacerHeight(600, 600)).toBe(0);
    expect(trailingSpacerHeight(600, 900)).toBe(0);
  });
});
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `pnpm vitest run src/features/buildMenu/logic/buildMenuScrollSpy.test.ts`
Expected: FAIL（モジュール未存在）

- [x] **Step 3: 実装を書く**

```ts
// scroll-spy・ジャンプ到達・末尾スペーサの判定。DOMを持たずフックから呼ばれる
// Scroll-spy, jump-settled, and trailing-spacer math; DOM-free, called from the hook

export type CategoryHeadingOffset = { categoryGuid: string; top: number };

// スムーズスクロールの停止位置は小数で揺れるため±1pxを同値とみなす
// Smooth scrolling settles on fractional positions, so treat ±1px as equal
export const scrollSettleTolerancePx = 1;

// 視口上端（許容内）以上にある最後の見出しが現在地。先頭より上なら先頭
// The last heading at or above the viewport top (within tolerance) is current; above the first means the first
export function activeCategoryAtScroll(offsets: CategoryHeadingOffset[], scrollTop: number): string | null {
  if (offsets.length === 0) return null;
  let active = offsets[0].categoryGuid;
  for (const offset of offsets) {
    if (offset.top - scrollSettleTolerancePx <= scrollTop) active = offset.categoryGuid;
  }
  return active;
}

export function isJumpSettled(scrollTop: number, targetTop: number): boolean {
  return Math.abs(scrollTop - targetTop) <= scrollSettleTolerancePx;
}

// 末尾カテゴリの見出しを視口上端まで持ち上げられるよう不足分を埋める
// Fill the shortfall so the last category heading can still reach the viewport top
export function trailingSpacerHeight(viewportHeight: number, lastGroupHeight: number): number {
  return Math.max(0, viewportHeight - lastGroupHeight);
}
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `pnpm vitest run src/features/buildMenu/logic/buildMenuScrollSpy.test.ts`
Expected: PASS

- [x] **Step 5: コミットする**

```bash
git add src/features/buildMenu/logic/buildMenuScrollSpy.ts src/features/buildMenu/logic/buildMenuScrollSpy.test.ts
git commit -m "feat(webui): add build menu scroll-spy math"
```

---

### Task 3: ModeSwitch に選択肢単位の disabled を追加する

**Files:**
- Modify: `moorestech_web/webui/src/shared/ui/ModeSwitch/index.tsx`
- Modify: `moorestech_web/webui/src/shared/ui/ModeSwitch/style.module.css`
- Modify: `moorestech_web/webui/src/shared/ui/ModeSwitch/index.test.ts`

**Interfaces:**
- Produces: `ModeSwitchOption.disabled?: boolean`（そのボタンだけ `disabled` 属性＋`data-option-disabled="true"`。root の `disabled` は従来どおり全体減衰）

- [x] **Step 1: 失敗するテストを書く**

`index.test.ts` の `describe("ModeSwitch", …)` 内に追加:

```ts
  it("option単位のdisabledはそのボタンだけを無効化しrootにdata-disabledを付けない", () => {
    const onChange = vi.fn();
    const renderer = create(createElement(ModeSwitch, {
      value: "a",
      options: [
        { value: "a", label: createElement("span", null, "a") },
        { value: "b", label: createElement("span", null, "b"), disabled: true },
      ],
      onChange,
      testId: "mode-switch",
    }));
    const root = renderer.root.findByProps({ "data-testid": "mode-switch" });
    const buttons = renderer.root.findAllByType("button");

    expect(root.props["data-disabled"]).toBeUndefined();
    expect(buttons[0].props.disabled).toBeFalsy();
    expect(buttons[1].props.disabled).toBe(true);
    expect(buttons[1].props["data-option-disabled"]).toBe("true");
  });
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `pnpm vitest run src/shared/ui/ModeSwitch/index.test.ts`
Expected: FAIL（`buttons[1].props.disabled` が undefined）

- [x] **Step 3: 実装を書く**

`index.tsx`:

```tsx
export type ModeSwitchOption = {
  value: string;
  label: ReactNode;
  testId?: string;
  // この選択肢だけ押せなくする。rootのdisabledは全体減衰で別物
  // Disables only this option; the root-level disabled is the whole-switch fade
  disabled?: boolean;
};
```

`options.map` 内の `<button>` を:

```tsx
        const optionDisabled = disabled || option.disabled === true;
        return (
          <button
            className={styles.option}
            data-selected={selected ? "true" : undefined}
            data-option-disabled={option.disabled ? "true" : undefined}
            data-testid={option.testId}
            aria-pressed={selected}
            key={option.value}
            type="button"
            disabled={optionDisabled}
            onClick={() => onChange(option.value)}
          >
```

`style.module.css` 末尾に追加:

```css
/* 選択肢単位の無効化はroot無効化と同じ減衰で示し、他の選択肢は生かす */
/* Per-option disabling uses the same fade as root disabling while the other options stay live */
.option[data-option-disabled] {
  color: var(--text-muted);
  cursor: default;
  pointer-events: none;
}
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `pnpm vitest run src/shared/ui/ModeSwitch/index.test.ts`
Expected: PASS（既存3件＋新規1件）

- [x] **Step 5: コミットする**

```bash
git add src/shared/ui/ModeSwitch/
git commit -m "feat(webui): ModeSwitch supports per-option disabled"
```

---

### Task 4: セッション内保持から categoryGuid を外す

**Files:**
- Modify: `moorestech_web/webui/src/features/buildMenu/sessionState/buildMenuSessionState.ts`
- Modify: `moorestech_web/webui/src/features/buildMenu/sessionState/buildMenuSessionState.test.ts`

**Interfaces:**
- Produces: `BuildMenuSessionState = { query: string; scrollTop: number; hoveredEntryId: string | null }`。`loadBuildMenuSessionState` / `updateBuildMenuSessionState` のシグネチャは不変

- [x] **Step 1: テストを書き換える**

`buildMenuSessionState.test.ts` を以下の全文にする:

```ts
import { beforeEach, describe, expect, it, vi } from "vitest";

// 各テストでstoredを初期化
// Reset stored per test via resetModules
beforeEach(() => {
  vi.resetModules();
});

describe("buildMenuSessionState", () => {
  it("初期状態は空検索・先頭スクロール・ホバー無し", async () => {
    const { loadBuildMenuSessionState } = await import("./buildMenuSessionState");
    expect(loadBuildMenuSessionState()).toEqual({ query: "", scrollTop: 0, hoveredEntryId: null });
  });

  it("部分更新が累積し、他フィールドは保たれる", async () => {
    const { loadBuildMenuSessionState, updateBuildMenuSessionState } = await import("./buildMenuSessionState");
    updateBuildMenuSessionState({ query: "鉄" });
    updateBuildMenuSessionState({ scrollTop: 120 });
    expect(loadBuildMenuSessionState()).toEqual({ query: "鉄", scrollTop: 120, hoveredEntryId: null });
  });

  // 前テストの更新が残らないことを確認
  // Confirms updates don't carry across tests
  it("モジュール再読込で前テストの更新が持ち越されない", async () => {
    const { loadBuildMenuSessionState } = await import("./buildMenuSessionState");
    expect(loadBuildMenuSessionState().query).toBe("");
  });

  it("ホバー中エントリを保持し、null で解除できる", async () => {
    const { loadBuildMenuSessionState, updateBuildMenuSessionState } = await import("./buildMenuSessionState");
    updateBuildMenuSessionState({ hoveredEntryId: "entry-1" });
    expect(loadBuildMenuSessionState().hoveredEntryId).toBe("entry-1");
    updateBuildMenuSessionState({ hoveredEntryId: null });
    expect(loadBuildMenuSessionState().hoveredEntryId).toBeNull();
  });
});
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `pnpm vitest run src/features/buildMenu/sessionState/buildMenuSessionState.test.ts`
Expected: FAIL（`toEqual` に `categoryGuid: null` が余分）

- [x] **Step 3: 実装を書き換える**

`buildMenuSessionState.ts` の型と初期値から `categoryGuid` を削除:

```ts
// ビルドメニューのセッション内状態。現在地はscrollTopから再現できるためカテゴリは持たない
// In-session build menu state; the current category is derivable from scrollTop, so it is not stored
type BuildMenuSessionState = {
  query: string;
  scrollTop: number;
  hoveredEntryId: string | null;
};

const initialState: BuildMenuSessionState = {
  query: "",
  scrollTop: 0,
  hoveredEntryId: null,
};
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `pnpm vitest run src/features/buildMenu/sessionState/buildMenuSessionState.test.ts`
Expected: PASS

- [x] **Step 5: コミットする**

```bash
git add src/features/buildMenu/sessionState/
git commit -m "refactor(webui): drop categoryGuid from build menu session state"
```

---

### Task 5: カテゴリスクロールフック（scroll-spy・スムーズジャンプ・末尾スペーサ）

**Files:**
- Create: `moorestech_web/webui/src/features/buildMenu/hooks/useBuildMenuCategoryScroll.ts`

**Interfaces:**
- Consumes: Task 2 の `activeCategoryAtScroll` / `isJumpSettled` / `trailingSpacerHeight` / `CategoryHeadingOffset`
- Produces:
  ```ts
  type BuildMenuCategoryScroll = {
    activeCategoryGuid: string | null;          // ハイライト対象（ジャンプ中は固定値）
    spacerHeight: number;                       // 末尾スペーサpx
    attachViewport: (viewport: HTMLDivElement | null) => void;  // ScrollArea viewportRef へ渡す
    attachHeading: (categoryGuid: string, element: HTMLElement | null) => void;  // 各大見出しの ref callback
    attachLastGroup: (element: HTMLElement | null) => void;     // 末尾カテゴリ群の ref callback
    jumpTo: (categoryGuid: string) => void;     // スムーズスクロール開始
    handleScroll: (scrollTop: number) => void;  // ScrollArea onScrollPositionChange の y を渡す
  };
  export function useBuildMenuCategoryScroll(visibleCategoryGuids: string[]): BuildMenuCategoryScroll;
  ```
  `visibleCategoryGuids` は表示中の群の順序付きGUID列（絞り込み後）。変わるたびにoffsetを取り直す

- [x] **Step 1: 実装を書く**

```ts
import { useCallback, useLayoutEffect, useRef, useState } from "react";
import {
  activeCategoryAtScroll,
  isJumpSettled,
  trailingSpacerHeight,
  type CategoryHeadingOffset,
} from "../logic/buildMenuScrollSpy";

type BuildMenuCategoryScroll = {
  activeCategoryGuid: string | null;
  spacerHeight: number;
  attachViewport: (viewport: HTMLDivElement | null) => void;
  attachHeading: (categoryGuid: string, element: HTMLElement | null) => void;
  attachLastGroup: (element: HTMLElement | null) => void;
  jumpTo: (categoryGuid: string) => void;
  handleScroll: (scrollTop: number) => void;
};

// DOM都合（視口・見出し位置・スムーズスクロール・末尾スペーサ）をここへ閉じ込め、判定は純関数へ委ねる
// Keeps DOM concerns (viewport, heading offsets, smooth scroll, trailing spacer) here and defers the math to pure functions
export function useBuildMenuCategoryScroll(visibleCategoryGuids: string[]): BuildMenuCategoryScroll {
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const headingsRef = useRef(new Map<string, HTMLElement>());
  const lastGroupRef = useRef<HTMLElement | null>(null);
  // ジャンプ中はハイライトを目標に固定し、到達でscroll-spyへ戻す
  // While jumping, pin the highlight to the target and release it to scroll-spy on arrival
  const jumpTargetRef = useRef<{ categoryGuid: string; top: number } | null>(null);
  const [activeCategoryGuid, setActiveCategoryGuid] = useState<string | null>(null);
  const [spacerHeight, setSpacerHeight] = useState(0);

  // 見出しの上端は視口内容座標（offsetTop）で読む。viewportがoffsetParentになるようCSSで position:relative を与える
  // Heading tops are read in viewport content coordinates (offsetTop); CSS makes the viewport the offsetParent
  const headingOffsets = (): CategoryHeadingOffset[] =>
    visibleCategoryGuids
      .map((categoryGuid) => {
        const element = headingsRef.current.get(categoryGuid);
        return element ? { categoryGuid, top: element.offsetTop } : null;
      })
      .filter((offset): offset is CategoryHeadingOffset => offset !== null);

  const spy = useCallback((scrollTop: number) => {
    const target = jumpTargetRef.current;
    if (target !== null) {
      if (!isJumpSettled(scrollTop, target.top)) return;
      jumpTargetRef.current = null;
    }
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), scrollTop));
  // headingOffsets は ref と props だけを読む
  // headingOffsets reads only refs and props
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visibleCategoryGuids]);

  // 表示群が変わったら末尾スペーサと現在地を取り直す
  // Recompute the trailing spacer and current category whenever the visible groups change
  useLayoutEffect(() => {
    const viewport = viewportRef.current;
    if (viewport === null) return;
    const lastGroupHeight = lastGroupRef.current?.offsetHeight ?? 0;
    setSpacerHeight(trailingSpacerHeight(viewport.clientHeight, lastGroupHeight));
    jumpTargetRef.current = null;
    setActiveCategoryGuid(activeCategoryAtScroll(headingOffsets(), viewport.scrollTop));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visibleCategoryGuids]);

  const attachViewport = useCallback((viewport: HTMLDivElement | null) => {
    viewportRef.current = viewport;
  }, []);
  const attachHeading = useCallback((categoryGuid: string, element: HTMLElement | null) => {
    if (element === null) headingsRef.current.delete(categoryGuid);
    else headingsRef.current.set(categoryGuid, element);
  }, []);
  const attachLastGroup = useCallback((element: HTMLElement | null) => {
    lastGroupRef.current = element;
  }, []);

  const jumpTo = useCallback((categoryGuid: string) => {
    const viewport = viewportRef.current;
    const heading = headingsRef.current.get(categoryGuid);
    if (viewport === null || heading === undefined) return;
    // 末尾スペーサ込みでも到達できない場合は最大スクロール位置を目標にして到達判定を成立させる
    // If even the spacer cannot reach the top, target the max scroll so the settle check can succeed
    const top = Math.min(heading.offsetTop, viewport.scrollHeight - viewport.clientHeight);
    jumpTargetRef.current = { categoryGuid, top };
    setActiveCategoryGuid(categoryGuid);
    if (isJumpSettled(viewport.scrollTop, top)) {
      jumpTargetRef.current = null;
      return;
    }
    viewport.scrollTo({ top, behavior: "smooth" });
  }, []);

  return { activeCategoryGuid, spacerHeight, attachViewport, attachHeading, attachLastGroup, jumpTo, handleScroll: spy };
}
```

- [x] **Step 2: 型チェックとlintを通す**

Run: `pnpm exec tsc --noEmit -p tsconfig.json && pnpm lint`
Expected: このファイルに関するエラーなし（`BuildMenuPanel.tsx` の Task 1 由来の型エラーは Task 6 で解消するため、ここではファイル名で切り分けて確認する）

- [x] **Step 3: コミットする**

```bash
git add src/features/buildMenu/hooks/useBuildMenuCategoryScroll.ts
git commit -m "feat(webui): add build menu category scroll hook (scroll-spy + smooth jump)"
```

---

### Task 6: 1本スクロールのリスト部品・サイドバー・パネル配線・CSS

**Files:**
- Create: `moorestech_web/webui/src/features/buildMenu/BuildMenuCategoryList.tsx`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuCategoryGrid.tsx`（`compositeHeading` 削除）
- Modify: `moorestech_web/webui/src/features/buildMenu/CategorySidebar.tsx`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuPanel.tsx`
- Modify: `moorestech_web/webui/src/features/buildMenu/style.module.css`
- Create: `moorestech_web/webui/src/features/buildMenu/BuildMenuCategoryList.test.ts`

**Interfaces:**
- Consumes: Task 1 `BuildMenuCategoryGroup` / `groupBuildMenuCategories` / `searchBuildMenuEntries`、Task 3 `ModeSwitchOption.disabled`、Task 4 のセッション状態、Task 5 `useBuildMenuCategoryScroll`
- Produces:
  - `BuildMenuCategoryList` props: `{ groups: BuildMenuCategoryGroup[]; spacerHeight: number; attachHeading; attachLastGroup; onSelect; onDelete; onEntryHovered }`。各群は `<section data-testid="build-menu-category-<guid>-group">`、大見出しは `<h2 data-testid="build-menu-category-heading-<guid>">`、末尾に `<div data-testid="build-menu-trailing-spacer" style={{height}}>`
  - `BuildMenuCategoryGrid` props: `{ sections; onSelect; onDelete; onEntryHovered }`（`compositeHeading` 削除。見出しはサブカテゴリ名のみ）
  - `CategorySidebar` props: `{ categories: { categoryGuid: string; disabled: boolean }[]; selected: string; onSelect }`（`disabled` 全体フラグは削除）

- [x] **Step 1: リスト部品の失敗するテストを書く**

`BuildMenuCategoryList.test.ts`:

```ts
import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BuildMenuCategoryGroup } from "./logic/buildMenuGrouping";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@/shared/ui", () => ({
  FadeRule: () => createElement("mock-fade-rule"),
  SlotGrid: ({ children, ...props }: { children?: unknown }) => createElement("mock-slot-grid", props, children as never),
}));
vi.mock("./BuildMenuSlot", () => ({
  BuildMenuSlot: (props: { entry: { id: string } }) => createElement("mock-slot", { "data-id": props.entry.id }),
}));

import { BuildMenuCategoryList } from "./BuildMenuCategoryList";

const entry = (id: string, categoryGuid: string, subCategoryGuid: string) => ({
  kind: "block" as const, id, categoryGuid, subCategoryGuid, requiredItems: [], paymentWaived: false, displayLabel: id,
});
const groups: BuildMenuCategoryGroup[] = [
  { categoryGuid: "cat-a", sections: [{ categoryGuid: "cat-a", subCategoryGuid: "sub-1", entries: [entry("e1", "cat-a", "sub-1")] }] },
  { categoryGuid: "cat-b", sections: [{ categoryGuid: "cat-b", subCategoryGuid: "sub-2", entries: [entry("e2", "cat-b", "sub-2")] }] },
];

describe("BuildMenuCategoryList", () => {
  it("カテゴリ群ごとに大見出しを置き、末尾群と各見出しをrefへ登録し、末尾スペーサ高を反映する", () => {
    const attachHeading = vi.fn();
    const attachLastGroup = vi.fn();
    const renderer = create(createElement(BuildMenuCategoryList, {
      groups,
      spacerHeight: 123,
      attachHeading,
      attachLastGroup,
      onSelect: () => undefined,
      onDelete: () => undefined,
      onEntryHovered: () => undefined,
    }));
    const headings = renderer.root.findAllByType("h2");
    expect(headings.map((h) => h.props["data-testid"])).toEqual([
      "build-menu-category-heading-cat-a",
      "build-menu-category-heading-cat-b",
    ]);
    const spacer = renderer.root.findByProps({ "data-testid": "build-menu-trailing-spacer" });
    expect(spacer.props.style).toEqual({ height: 123 });
    // ref callback はマウント時にelement付きで呼ばれる（react-test-renderer では null）
    // Ref callbacks fire on mount (react-test-renderer passes null)
    expect(attachHeading).toHaveBeenCalledWith("cat-a", null);
    expect(attachHeading).toHaveBeenCalledWith("cat-b", null);
    expect(attachLastGroup).toHaveBeenCalledTimes(1);
  });
});
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `pnpm vitest run src/features/buildMenu/BuildMenuCategoryList.test.ts`
Expected: FAIL（モジュール未存在）

- [x] **Step 3: BuildMenuCategoryGrid から compositeHeading を外す**

`BuildMenuCategoryGrid.tsx` を以下の全文にする:

```tsx
import { FadeRule, SlotGrid } from "@/shared/ui";
import { buildMenuSubCategoryNameKey, useI18n } from "@/shared/i18n";
import type { BuildMenuDisplayEntry, BuildMenuSection } from "./logic/buildMenuGrouping";
import { BuildMenuSlot } from "./BuildMenuSlot";
import styles from "./style.module.css";

type Props = {
  sections: BuildMenuSection[];
  onSelect: (entry: BuildMenuDisplayEntry) => void;
  onDelete: (entry: BuildMenuDisplayEntry) => void;
  // 入場のみ見て離脱は捨てる
  // Only entry matters; drop the leave boolean here
  onEntryHovered: (entry: BuildMenuDisplayEntry) => void;
};

// サブカテゴリ小見出し+SlotGridでエントリを列挙する
// Lists entries as sub-category headings plus a SlotGrid
export function BuildMenuCategoryGrid({ sections, onSelect, onDelete, onEntryHovered }: Props) {
  const { t } = useI18n();
  return (
    <>
      {sections.map((section) => (
        <section
          key={`${section.categoryGuid}/${section.subCategoryGuid}`}
          className={styles.section}
          data-testid={`build-menu-section-${section.categoryGuid}-${section.subCategoryGuid}`}
        >
          <h3 className={styles.sectionHeading}>{t(buildMenuSubCategoryNameKey(section.subCategoryGuid))}</h3>
          <FadeRule />
          <SlotGrid cols={8} testId={`build-menu-grid-${section.categoryGuid}-${section.subCategoryGuid}`}>
            {section.entries.map((entry) => (
              <BuildMenuSlot
                key={entry.id}
                entry={entry}
                onLeftClick={() => onSelect(entry)}
                onRightClick={entry.kind === "blueprint" ? () => onDelete(entry) : undefined}
                onHoverChange={(hovering) => { if (hovering) onEntryHovered(entry); }}
              />
            ))}
          </SlotGrid>
        </section>
      ))}
    </>
  );
}
```

- [x] **Step 4: BuildMenuCategoryList を作る**

```tsx
import { FadeRule } from "@/shared/ui";
import { buildMenuCategoryNameKey, useI18n } from "@/shared/i18n";
import type { BuildMenuCategoryGroup, BuildMenuDisplayEntry } from "./logic/buildMenuGrouping";
import { BuildMenuCategoryGrid } from "./BuildMenuCategoryGrid";
import styles from "./style.module.css";

type Props = {
  groups: BuildMenuCategoryGroup[];
  spacerHeight: number;
  attachHeading: (categoryGuid: string, element: HTMLElement | null) => void;
  attachLastGroup: (element: HTMLElement | null) => void;
  onSelect: (entry: BuildMenuDisplayEntry) => void;
  onDelete: (entry: BuildMenuDisplayEntry) => void;
  onEntryHovered: (entry: BuildMenuDisplayEntry) => void;
};

// 全カテゴリを「大見出し → サブカテゴリ群」で1本に並べる（ADR 0045）
// Lays every category out as "heading → sub-category sections" in one scroll (ADR 0045)
export function BuildMenuCategoryList({ groups, spacerHeight, attachHeading, attachLastGroup, onSelect, onDelete, onEntryHovered }: Props) {
  const { t } = useI18n();
  const lastIndex = groups.length - 1;
  return (
    <div className={styles.gridArea} data-testid="build-menu-sections">
      {groups.map((group, index) => (
        <section
          key={group.categoryGuid}
          className={styles.categoryGroup}
          data-testid={`build-menu-category-${group.categoryGuid}-group`}
          ref={index === lastIndex ? attachLastGroup : undefined}
        >
          <h2
            className={styles.categoryHeading}
            data-testid={`build-menu-category-heading-${group.categoryGuid}`}
            ref={(element) => attachHeading(group.categoryGuid, element)}
          >
            {t(buildMenuCategoryNameKey(group.categoryGuid))}
          </h2>
          <FadeRule />
          <BuildMenuCategoryGrid
            sections={group.sections}
            onSelect={onSelect}
            onDelete={onDelete}
            onEntryHovered={onEntryHovered}
          />
        </section>
      ))}
      {/* 末尾カテゴリの見出しを視口上端へ持ち上げるための余白 */}
      {/* Trailing room so the last category heading can reach the viewport top */}
      <div data-testid="build-menu-trailing-spacer" style={{ height: spacerHeight }} />
    </div>
  );
}
```

- [x] **Step 5: テストを実行して通ることを確認する**

Run: `pnpm vitest run src/features/buildMenu/BuildMenuCategoryList.test.ts`
Expected: PASS

- [x] **Step 6: CategorySidebar をジャンプ用に書き換える**

`CategorySidebar.tsx` 全文:

```tsx
import { ModeSwitch } from "@/shared/ui";
import { buildMenuCategoryNameKey, useI18n } from "@/shared/i18n";

export type CategorySidebarItem = {
  categoryGuid: string;
  // 検索でヒットが無いカテゴリは押せない
  // Categories with no search hit cannot be pressed
  disabled: boolean;
};

type Props = {
  categories: CategorySidebarItem[];
  // scroll-spyの現在地（ジャンプ中は目標）
  // Scroll-spy current category (the target while jumping)
  selected: string;
  onSelect: (categoryGuid: string) => void;
};

// §8.6の縦ModeSwitchをカテゴリ見出しへのジャンプサイドバーとして転用する（ADR 0045）
// Reuses the §8.6 vertical ModeSwitch as the jump-to-category-heading sidebar (ADR 0045)
export function CategorySidebar({ categories, selected, onSelect }: Props) {
  const { t } = useI18n();
  return (
    <ModeSwitch
      value={selected}
      options={categories.map((category) => ({
        value: category.categoryGuid,
        label: t(buildMenuCategoryNameKey(category.categoryGuid)),
        testId: `build-menu-category-${category.categoryGuid}`,
        disabled: category.disabled,
      }))}
      onChange={onSelect}
      orientation="vertical"
      testId="build-menu-sidebar"
    />
  );
}
```

- [x] **Step 7: BuildMenuPanel を配線し直す**

`BuildMenuPanel.tsx` 全文:

```tsx
import { useLayoutEffect, useRef, useState } from "react";
import { ScrollArea } from "@mantine/core";
import { useTopic, dispatchAction, Topics, UiStateNames } from "@/bridge";
import { GamePanel, IconButton } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import {
  groupBuildMenuCategories,
  localizeBuildMenuEntries,
  searchBuildMenuEntries,
  type BuildMenuDisplayEntry,
} from "./logic/buildMenuGrouping";
import { useBuildMenuCategoryScroll } from "./hooks/useBuildMenuCategoryScroll";
import { BuildMenuCategoryList } from "./BuildMenuCategoryList";
import { BuildMenuDetailSidebar } from "./BuildMenuDetailSidebar";
import { BuildMenuSearchInput } from "./BuildMenuSearchInput";
import { CategorySidebar } from "./CategorySidebar";
import { loadBuildMenuSessionState, updateBuildMenuSessionState } from "./sessionState/buildMenuSessionState";
import styles from "./style.module.css";

// BuildMenuViewのweb版・3カラム(§8.11)。中央は全カテゴリ1本スクロール（ADR 0045）
// Web version of BuildMenuView; 3 columns (§8.11). The middle is one scroll over every category (ADR 0045)
export function BuildMenuPanel() {
  const { t } = useI18n();
  const data = useTopic(Topics.buildMenu);
  // ストアから初期値を復元
  // Restore initial values from the session store
  const [stored] = useState(() => loadBuildMenuSessionState());
  const [query, setQuery] = useState(stored.query);
  const [hoveredId, setHoveredId] = useState<string | null>(stored.hoveredEntryId);

  // 表示名を一度解決し全表示へ共有。サイドバーは絞り込み前、リストは絞り込み後の群を見る
  // Resolve display names once; the sidebar sees unfiltered groups, the list sees filtered ones
  const displayEntries = data ? localizeBuildMenuEntries(data.entries, t) : [];
  const allGroups = data ? groupBuildMenuCategories(data.categories, displayEntries) : [];
  const shownGroups = data ? groupBuildMenuCategories(data.categories, searchBuildMenuEntries(query, displayEntries)) : [];
  const shownGuids = shownGroups.map((group) => group.categoryGuid);
  const scroll = useBuildMenuCategoryScroll(shownGuids);

  // 視口アタッチ時に1回復元
  // Restore once via the viewport attach callback
  const scrollRestoredRef = useRef(false);
  const scrollViewportRef = useRef<HTMLDivElement | null>(null);
  const attachScrollViewport = (viewport: HTMLDivElement | null) => {
    scroll.attachViewport(viewport);
    if (viewport === null) return;
    // 保存先は常に最新視口
    // Save target always tracks the latest viewport
    scrollViewportRef.current = viewport;
    if (scrollRestoredRef.current) return;
    scrollRestoredRef.current = true;
    viewport.scrollTop = loadBuildMenuSessionState().scrollTop;
    // クランプ後の実効値へ揃え直す
    // Realign the store with the clamped effective value
    updateBuildMenuSessionState({ scrollTop: viewport.scrollTop });
  };
  // scrollイベントは次フレームまで合体されアンマウントに間に合わないため、DOM除去前の実効値を確定保存する
  // Scroll events coalesce until the next frame and miss the unmount, so persist the effective value before DOM removal
  useLayoutEffect(() => () => {
    if (scrollViewportRef.current === null) return;
    updateBuildMenuSessionState({ scrollTop: scrollViewportRef.current.scrollTop });
  }, []);
  if (!data) return null;

  const searching = query !== "";
  const sidebarItems = allGroups.map((group) => ({
    categoryGuid: group.categoryGuid,
    disabled: !shownGuids.includes(group.categoryGuid),
  }));

  // sticky:離脱で消さず引き直す
  // Sticky: never clear; re-resolve on rebroadcast
  const detailEntry = hoveredId
    ? displayEntries.find((entry) => entry.id === hoveredId) ?? null
    : null;
  const hover = (entry: BuildMenuDisplayEntry) => {
    setHoveredId(entry.id);
    updateBuildMenuSessionState({ hoveredEntryId: entry.id });
  };
  const changeQuery = (next: string) => {
    setQuery(next);
    updateBuildMenuSessionState({ query: next });
  };
  const onScroll = (y: number) => {
    updateBuildMenuSessionState({ scrollTop: y });
    scroll.handleScroll(y);
  };

  const select = (entry: BuildMenuDisplayEntry) =>
    void dispatchAction("build_menu.select", { id: entry.id });
  // BPのGuidを設置対象と削除対象の共通identityとして使う
  // Use the blueprint GUID as the shared identity for placement and deletion
  const remove = (entry: BuildMenuDisplayEntry) =>
    void dispatchAction("blueprint.delete", { id: entry.id });
  // 閉じるはGameScreen遷移要求
  // Close requests a GameScreen transition
  const close = () => void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });

  return (
    <div className={styles.panelBand}>
      <div className={styles.panel} data-testid="build-menu-panel">
        {/* GamePanelは内容高で伸びるため、blockInventory前例と同じくバンド高へ明示的に縛る（§8.11の--menu-content-height） */}
        {/* GamePanel grows with its content, so pin it to the band height like the blockInventory precedent (§8.11 --menu-content-height) */}
        <GamePanel title={t(L.ui.buildMenu.title)} variant="default" style={{ height: "100%", boxSizing: "border-box" }}>
          <IconButton onClick={close} ariaLabel={t(L.ui.common.close)} className={styles.close} testId="build-menu-close" />
          <div className={styles.columns} data-testid="build-menu-columns">
            <div className={styles.sidebar}>
              <CategorySidebar
                categories={sidebarItems}
                selected={scroll.activeCategoryGuid ?? ""}
                onSelect={scroll.jumpTo}
              />
            </div>
            <div className={styles.main}>
              <BuildMenuSearchInput value={query} onChange={changeQuery} />
              <ScrollArea
                className={styles.scroll}
                type="auto"
                viewportRef={attachScrollViewport}
                onScrollPositionChange={({ y }) => onScroll(y)}
              >
                {shownGroups.length === 0 && searching ? (
                  <span className={styles.noHit}>{t(L.ui.buildMenu.noResults)}</span>
                ) : (
                  <BuildMenuCategoryList
                    groups={shownGroups}
                    spacerHeight={scroll.spacerHeight}
                    attachHeading={scroll.attachHeading}
                    attachLastGroup={scroll.attachLastGroup}
                    onSelect={select}
                    onDelete={remove}
                    onEntryHovered={hover}
                  />
                )}
              </ScrollArea>
            </div>
            <BuildMenuDetailSidebar entry={detailEntry} />
          </div>
        </GamePanel>
      </div>
    </div>
  );
}
```

注: フックは `if (!data) return null` より前に無条件で呼ぶ（Hooksの呼び出し順序規則）。`data` が無い間は空配列を渡す。

- [x] **Step 8: CSSを追加する**

`style.module.css` の `.sectionHeading { font-size: 12px; … }` ブロックの直後に追加:

```css
/* カテゴリ大見出しは本文色・大きめで、muted小見出しと階層差を出す（ADR 0045） */
/* Category headings use the body color at a larger size to read above the muted sub-headings (ADR 0045) */
.categoryHeading {
  color: var(--text-default);
  font-size: var(--label-face-font-size);
  font-weight: 400;
  margin: 0;
}

/* 大見出し→FadeRule→サブカテゴリ群の縦積み。群同士はカテゴリ間余白で切る */
/* Stacks heading, FadeRule, and sub-category sections; groups are separated by the inter-category gap */
.categoryGroup {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.categoryGroup + .categoryGroup {
  margin-top: var(--build-menu-category-gap);
}
```

`.scroll :global(.mantine-ScrollArea-viewport)` ブロックに1行追加（見出しの `offsetTop` を視口基準にするため）:

```css
.scroll :global(.mantine-ScrollArea-viewport) {
  box-sizing: border-box;
  padding: var(--tutorial-anchor-clip-inset);
  /* 見出しoffsetTopの基準を視口にする（scroll-spy） */
  /* Make the viewport the offsetParent for heading offsetTop (scroll-spy) */
  position: relative;
}
```

`app/tokens.css` の `--build-menu-category-height: 2.0625rem;` 行の直後に追加:

```css
  /* 1本スクロール内のカテゴリ群同士の縦間隔（ADR 0045） */
  /* Vertical gap between category groups inside the single scroll (ADR 0045) */
  --build-menu-category-gap: 20px;
```

- [x] **Step 9: 型・lint・全単体テストを通す**

Run: `pnpm exec tsc --noEmit -p tsconfig.json && pnpm lint && pnpm test`
Expected: すべて成功。`pnpm test` で `buildMenuGrouping` / `buildMenuScrollSpy` / `BuildMenuCategoryList` / `buildMenuSessionState` / `ModeSwitch` / `BuildMenuSlot` / `BuildMenuDetailSidebar` が緑

- [x] **Step 10: コミットする**

```bash
git add src/features/buildMenu/ src/app/tokens.css
git commit -m "feat(webui): build menu becomes one scroll with category-jump sidebar (ADR 0045)"
```

---

### Task 7: e2e回帰テスト・キャプチャスクリプト・webui-design §8.11 の更新

**Files:**
- Modify: `moorestech_web/webui/e2e/tests/regression/buildMenu.spec.ts`
- Modify: `moorestech_web/webui/e2e/capture-buildmenu.ts`
- Modify: `.agents/skills/webui-design/SKILL.md`（§8.11）
- 変更なしで緑を確認: `moorestech_web/webui/e2e/tests/regression/buildMenuLayout.spec.ts`

**Interfaces:**
- Consumes: Task 6 の testid（`build-menu-category-heading-<guid>`、`build-menu-category-<guid>-group`、`build-menu-trailing-spacer`、`build-menu-category-<guid>` ボタン、`build-menu-sidebar`）

- [x] **Step 1: buildMenu.spec.ts のテストを書き換える**

(a) `test("カテゴリ切替でセクションが入れ替わる", …)` を丸ごと以下に置き換える:

```ts
// 全カテゴリが同時にDOMへ載り、サイドバーは見出しへのジャンプになる（ADR 0045）
// Every category is in the DOM at once; the sidebar jumps to headings (ADR 0045)
test("全カテゴリが1本スクロールに並び、サイドバー押下で見出しへジャンプしハイライトが追従する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await expect(page.getByTestId(`build-menu-category-heading-${buildMenuCategoryIds.logistics}`)).toBeVisible();
  await expect(page.getByTestId(`build-menu-category-heading-${buildMenuCategoryIds.transport}`)).toBeAttached();
  await expect(page.getByTestId(`build-menu-entry-block-${buildMenuEntryIds.rail}`)).toBeAttached();

  await page.getByTestId(`build-menu-category-${buildMenuCategoryIds.transport}`).click();
  const viewport = page.getByTestId("build-menu-panel").locator(".mantine-ScrollArea-viewport");
  const headingTop = await page
    .getByTestId(`build-menu-category-heading-${buildMenuCategoryIds.transport}`)
    .evaluate((el: HTMLElement) => el.offsetTop);
  // スムーズスクロールの停止を待つ
  // Wait for smooth scrolling to settle
  await expect.poll(() => viewport.evaluate((el, top) => Math.abs(el.scrollTop - top) <= 1, headingTop)).toBe(true);
  await expect(page.getByTestId(`build-menu-category-${buildMenuCategoryIds.transport}`)).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByTestId(`build-menu-category-${buildMenuCategoryIds.logistics}`)).toHaveAttribute("aria-pressed", "false");

  // 手スクロールでもハイライトが追従する
  // Highlight follows manual scrolling too
  await viewport.evaluate((el) => { el.scrollTop = 0; });
  await expect(page.getByTestId(`build-menu-category-${buildMenuCategoryIds.logistics}`)).toHaveAttribute("aria-pressed", "true");
});

test("末尾カテゴリへジャンプしても見出しが視口上端に来る", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  const lastButton = page.getByTestId("build-menu-sidebar").locator("button").last();
  const lastGuid = (await lastButton.getAttribute("data-testid"))!.replace("build-menu-category-", "");
  await lastButton.click();
  const viewport = page.getByTestId("build-menu-panel").locator(".mantine-ScrollArea-viewport");
  const headingTop = await page.getByTestId(`build-menu-category-heading-${lastGuid}`).evaluate((el: HTMLElement) => el.offsetTop);
  await expect.poll(() => viewport.evaluate((el, top) => Math.abs(el.scrollTop - top) <= 1, headingTop)).toBe(true);
  await expect(lastButton).toHaveAttribute("aria-pressed", "true");
});
```

(b) `test("横断検索は複合見出しで区切りサイドバーを無効化する", …)` を以下に置き換える:

```ts
test("検索は同じリストを絞り込み、ヒットの無いカテゴリだけサイドバーで無効になる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.goto("/");

  await page.getByTestId("build-menu-search").fill("鉄");
  await expect(page.getByTestId(`build-menu-category-heading-${buildMenuCategoryIds.logistics}`)).toBeVisible();
  await expect(page.getByTestId(
    `build-menu-section-${buildMenuCategoryIds.transport}-${buildMenuSubCategoryIds.rail}`,
  )).toBeAttached();
  // 複合見出しは廃止。サブカテゴリ見出しにカテゴリ名を含めない
  // Composite headings are gone; sub-category headings never carry the category name
  await expect(page.getByTestId(
    `build-menu-section-${buildMenuCategoryIds.logistics}-${buildMenuSubCategoryIds.chest}`,
  ).locator("h3")).not.toContainText("/");
  await expect(page.getByTestId("build-menu-sidebar")).not.toHaveAttribute("data-disabled", "true");
  await expect(page.getByTestId(`build-menu-category-${buildMenuCategoryIds.transport}`)).toBeEnabled();
  await expect(page.getByTestId(`build-menu-category-${buildMenuCategoryIds.blueprint}`)).toBeDisabled();

  await page.getByTestId("build-menu-search").fill("");
  await expect(page.getByTestId(`build-menu-category-${buildMenuCategoryIds.blueprint}`)).toBeEnabled();
});
```

(c) `test("閉じて開き直すとタブ・検索・スクロール・詳細stickyが復元される", …)` のタイトルを `"閉じて開き直すと検索・スクロール・詳細stickyが復元される"` にし、先頭の `await page.getByTestId(\`build-menu-category-${buildMenuCategoryIds.transport}\`).click();` 行と「タブ+sticky+スクロール構築」コメント2行を削除する（railは常時DOMにあるためホバー可能）。末尾の `toBeVisible()` は `toBeAttached()` に変える。

(d) `test("検索文字列も閉じて開き直すと復元される", …)` の最終行 `await expect(page.getByTestId("build-menu-sidebar")).toHaveAttribute("data-disabled", "true");` を `await expect(page.getByTestId(\`build-menu-category-${buildMenuCategoryIds.blueprint}\`)).toBeDisabled();` に変える。

(e) `test("エントリ選択とBP右クリック削除のアクション契約", …)` の `await page.getByTestId(\`build-menu-category-${buildMenuCategoryIds.blueprint}\`).click();` 行は残す（ジャンプ後にクリックする形で成立）。

- [x] **Step 2: capture-buildmenu.ts のコメントと手順を合わせる**

- 「1. 既定表示（先頭カテゴリ選択・カーソル退避）」→「1. 既定表示（先頭カテゴリが視口上端・カーソル退避）」/ 英語 `(first category at viewport top, cursor parked off-screen)`
- 「2.検索中(複合見出し)」→「2.検索中(絞り込み)」/ `2. Searching (filtered)`
- 「4.8列グリッドが埋まるカテゴリ」の後に、click後 `await page.waitForTimeout(600);`（スムーズスクロール完了待ち）を `waitFor()` の直後へ追加

- [x] **Step 3: e2eを実行する**

Run: `pnpm test:e2e buildMenu`
Expected: `buildMenu.spec.ts` と `buildMenuLayout.spec.ts` がすべてPASS。失敗した場合は失敗specとメッセージを記録し、ポート5273衝突でないことを確認してから修正する

- [x] **Step 4: webui-design §8.11 を改定する**

`.agents/skills/webui-design/SKILL.md` の §8.11 で以下を書き換える:

- 「**3カラム構成**: 1枚のGamePanel内で「カテゴリ | 検索+グリッド | 詳細サイドバー」。」→「**3カラム構成**: 1枚のGamePanel内で「カテゴリジャンプ | 検索+全カテゴリ1本スクロール | 詳細サイドバー」（ADR 0045）。」
- 「**縦ModeSwitchサイドバー**: カテゴリ切替は §8.6 の縦向き ModeSwitch を左サイドバーとして使う。」→「**縦ModeSwitchサイドバー（ジャンプ＋scroll-spy）**: 左サイドバーは §8.6 の縦向き ModeSwitch。押すとそのカテゴリ大見出しが視口上端に来るようスムーズスクロールし、ハイライト（`data-selected`）は視口上端にあるカテゴリへ追従する（ジャンプ中は目標に固定）。タブ（表示切替）ではない。」
- 「**検索**: §8.9 の検索入力を中央カラム上部に置く。」→「**検索**: §8.9 の検索入力を中央カラム上部に置く。検索は同じ1本スクロールの絞り込みで、ヒットの無いカテゴリ/サブカテゴリは非表示、サイドバーはヒットの無いカテゴリ項目だけ `ModeSwitchOption.disabled` で無効化する。複合見出しは使わない。」
- 「**サブカテゴリ見出し**: …」の前に追加: 「**カテゴリ大見出し**: 各カテゴリ群の先頭に `--text-default`・`--label-face-font-size` のラベル + `FadeRule`。群同士は `--build-menu-category-gap` で区切る。リスト末尾には末尾カテゴリの見出しが視口上端まで上がれるよう「視口高−末尾群高」のスペーサを置く。」
- 「**セッション内状態保持**: 選択カテゴリ・検索文字列・スクロール位置・詳細sticky表示は」→「**セッション内状態保持**: 検索文字列・スクロール位置・詳細sticky表示は」（選択カテゴリを削除）
- §8.6 ModeSwitch の項に1行追加: 「  - **`ModeSwitchOption.disabled?: boolean`**: 選択肢単位の無効化（`data-option-disabled`）。rootの `disabled` と同じ減衰で、他の選択肢は生かす。判断は利用側が持つ。」

- [x] **Step 5: コミットする**

```bash
git add e2e/tests/regression/buildMenu.spec.ts e2e/capture-buildmenu.ts ../../.agents/skills/webui-design/SKILL.md
git commit -m "test(webui): build menu e2e for single scroll + category jump; update webui-design §8.11"
```

---

### Task 8: 全ブランチレビュー（必須・省略不可）

**Files:**
- 対象: ブランチ `feature/build-menu-single-scroll` の全差分

- [ ] **Step 1: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

`moores-code-review` スキルを起動し、masterとの全差分をレビューする。機械的修正は適用し、設計判断はAskUserQuestionで仰ぐ。

- [ ] **Step 2: 修正があればテストを再実行してコミットする**

Run: `pnpm lint && pnpm test && pnpm test:e2e buildMenu`
Expected: すべてPASS

- [ ] **Step 3: PRを作成し、bd を閉じ、worktreeを畳む**

`pr-create` スキルでPRを作成（本文にADR 0045と `.decisions/2026-08-30-ビルドメニュー*` 5件を列挙）。`bd close moorestech-5ria --reason="PR #<番号>"`。その後 `moores-wt rm feature/build-menu-single-scroll`。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `groupBuildMenuCategories` / `searchBuildMenuEntries` | `features/buildMenu/logic/`（純関数） | 配列変換 | 既存 `sectionsForCategory` / `searchSections`（同ファイル） |
| 2 | `activeCategoryAtScroll` 等 | `features/buildMenu/logic/buildMenuScrollSpy.ts`（純関数） | 数値判定 | `features/recipe/panels/useDragScroll.ts` の `nextScrollTop`（DOM無しの純関数をフックから呼ぶ形） |
| 3 | `useBuildMenuCategoryScroll` | `features/buildMenu/hooks/` | React ref + scrollイベント購読（`ScrollArea.onScrollPositionChange`） | `BuildMenuPanel` の既存 `viewportRef`/`onScrollPositionChange` 配線、`features/recipe/panels/useDragScroll.ts` |
| 4 | `ModeSwitchOption.disabled` | `shared/ui/ModeSwitch` | data属性＋CSS減衰 | 同部品の root `disabled`（§8.6） |
| 5 | `BuildMenuCategoryList` | `features/buildMenu/` | 既存 `BuildMenuCategoryGrid` を内包 | 同ディレクトリの `BuildMenuCategoryGrid` |
| 6 | `--build-menu-category-gap` | `app/tokens.css` | 固定長トークン | `--build-menu-*` 既存トークン群 |
| 7 | scroll-spy の駆動 | `ScrollArea` の scroll イベント購読 | 購読（ポーリング無し） | AGENTS.md「状態変化の検知は購読で」 |

- 検査4（機構選択）: スムーズスクロール中のハイライトちらつき対策として「scroll-spyを止める」のではなく、「目標到達までハイライトを固定し、scroll-spyは動かしたまま到達判定だけ挟む」受動的統合を採る。IntersectionObserverによる代替は視口上端基準の判定を素直に表現できないため不採用（`anchorRegistry.ts` のIOは可視ダーティ通知用途で役割が違う）。
- 機能死活表: エントリ左クリック選択（生存・DOM常駐）、BP右クリック削除（生存）、検索（生存・絞り込みへ）、閉じる（生存）、詳細sticky（生存）、閉じて開き直し復元（生存・カテゴリ以外）、ホットバーへのドラッグ（`useHotbarDragSource`・生存）、チュートリアルアンカー `build-menu.entry-*`（生存・全エントリ常駐で寧ろ堅牢化）、プレイテストDSLのカテゴリクリック（生存・ジャンプ後にエントリクリック）。死ぬ操作: 無し。

## 判断記録（ADR）

- 設計裁定: `docs/adr/0045-build-menu-single-scroll-with-category-jump-sidebar.md`、`.decisions/2026-08-30-ビルドメニュー*`（5件）
- planning中の判断（すべて agent前提）:
  - scroll-spyの現在地判定は「視口上端以上にある最後の見出し」・許容±1px。IntersectionObserverは使わない（上記）
  - スムーズスクロール中の固定解除は「scrollTopが目標±1px」到達で行い、`scrollend` イベントは使わない（Unity内蔵WebViewでの対応が未確認のため。scrollイベントは確実に届く）
  - 末尾スペーサはCSSの `min-height:100%` ではなくフックで実測（視口高−末尾群高）して `style.height` に流す。ScrollArea viewport内のパーセント高は信頼できないため
  - 見出し `offsetTop` を視口基準にするため viewport に `position: relative` を与える。既存の `padding`（チュートリアル逃げ）はそのまま
  - カテゴリ間余白は新トークン `--build-menu-category-gap: 20px`。大見出しのfont-sizeは既存 `--label-face-font-size`（14px）を流用し新トークンを作らない
  - サイドバーの全体 `disabled` は廃止し、選択肢単位の `ModeSwitchOption.disabled` を汎用機能として `shared/ui` に追加（ドメイン語彙なし）
  - e2eでスムーズスクロールの完了は `expect.poll` で `scrollTop` を待つ（固定 `waitForTimeout` は capture スクリプトのみ）
