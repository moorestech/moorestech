# 研究ノードカード状態ラベル Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 研究ツリーの各ノードカードに「完了済み／研究可能／研究不可」の状態ラベル1行を文字で追加する（ADR 0044）。

**Architecture:** 既存の `deriveNodeCardState`（4状態 `{completed, ready, locked}`）の上に3語への写像 `deriveNodeStateLabelKey` を `researchLogic.ts` に置き、`ResearchNodeCard.tsx` がアイコン下に描く。文言は `Localization/localization.csv` の新規キー3件で持ち、webui側生成物 `localizationKeys.ts` は `pnpm gen:i18n` で再生成する。枠色表現には一切触れない。

**Tech Stack:** React + TypeScript（moorestech_web/webui）、vitest + react-test-renderer、pnpm、localization.csv → `scripts/generate-localization-keys.mjs`。

## Requirements

- R1: 各研究ノードカードの `ItemSlot`（アイコン）の直下に状態ラベルを1行表示する。受け入れ: `ResearchNodeCard` の描画結果に `research-node-state-<guid>` testid の要素があり、ラベルキーの翻訳文が出る
- R2: ラベルは3語。`state === "completed"` → `ui.research.stateCompleted`、`deriveNodeCardState().ready` → `ui.research.stateAvailable`、それ以外（`unresearchableNotEnoughItem` / `unresearchableNotEnoughPreNode` / `unresearchableAllReasons`）→ `ui.research.stateUnavailable`。受け入れ: `deriveNodeStateLabelKey` のユニットテストが5 state全部を網羅
- R3: `Localization/localization.csv` にキー3件を追加（Source/english/japanese/german = Completed/Completed/完了済み/Abgeschlossen、Available/Available/研究可能/Verfügbar、Unavailable/Unavailable/研究不可/Nicht verfügbar）。受け入れ: `localizationKeysFreshness.test.ts` を含む `pnpm test` が緑
- R4: 枠色4状態（`data-locked/ready/completed` とCSS）は変更しない。ラベル色は `--text-default` 固定で状態別の色付けをしない。受け入れ: `style.module.css` の `.node[data-*]` 行に差分が無い
- やらないこと: 詳細ペインへの状態行追加、既存キー `ui.research.completed`（ボタン文言「研究済み」）の変更、4語ラベル、シアン文字

## Global Constraints

- 作業は `moores-wt new feature/research-node-state-label` で切った使い捨てworktreeで行う（CLAUDE.local.md）。PR作成後に `moores-wt rm`
- webuiは `pnpm`（`pnpm-lock.yaml`）。コマンドは `moorestech_web/webui` で実行
- コメント規約: 主要セクションに日本語→英語の2行セット、各1行
- C#側の `LocalizationKeys` はSourceGeneratorが `csc.rsp` の additionalfile 経由で自動生成するためコミット対象の生成物は無い。webuiの `src/shared/i18n/generated/localizationKeys.ts` はコミット対象
- `.metaファイル` 手動作成禁止（本planはUnity側ファイルを触らない）
- コミットは通常マージ運用・Squash禁止。コミット末尾に `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` 行

---

### Task 1: localization.csv にキー3件を追加し webui 生成物を再生成する

**Files:**
- Modify: `Localization/localization.csv`（`ui.research.unlockFluidSummary` 行の直後）
- Regenerate: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`
- Test: `moorestech_web/webui/src/shared/i18n/localizationKeysFreshness.test.ts`（既存）

**Interfaces:**
- Produces: `L.ui.research.stateCompleted` / `L.ui.research.stateAvailable` / `L.ui.research.stateUnavailable`（`TranslationKey` 型の定数）

- [ ] **Step 1: CSVに3行追加する**

`Localization/localization.csv` の `ui.research.unlockFluidSummary,...` 行の直後に追記（列順は `key,Source,english,japanese,german`）:

```csv
ui.research.stateCompleted,Completed,Completed,完了済み,Abgeschlossen
ui.research.stateAvailable,Available,Available,研究可能,Verfügbar
ui.research.stateUnavailable,Unavailable,Unavailable,研究不可,Nicht verfügbar
```

- [ ] **Step 2: 鮮度テストが失敗することを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/shared/i18n/localizationKeysFreshness.test.ts`
Expected: FAIL（生成物にキーが無く古い旨）

- [ ] **Step 3: 生成物を再生成する**

Run: `cd moorestech_web/webui && pnpm gen:i18n`
Expected: `localizationKeys.ts` の `research: {...}` に `stateCompleted`, `stateAvailable`, `stateUnavailable` が増える（`git diff --stat` で当該1ファイルのみ変化）

- [ ] **Step 4: 鮮度テストが通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/shared/i18n/localizationKeysFreshness.test.ts`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git commit -m "feat(research): 研究ノード状態ラベルのローカライズキー3件を追加

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: 3語ラベルキーへの写像関数を researchLogic.ts に追加する

**Files:**
- Modify: `moorestech_web/webui/src/features/research/researchLogic.ts`（`deriveNodeCardState` の直後）
- Test: `moorestech_web/webui/src/features/research/researchLogic.test.ts`

**Interfaces:**
- Consumes: Task 1 の `L.ui.research.state*`、既存 `deriveNodeCardState(node): NodeCardState`
- Produces: `export function deriveNodeStateLabelKey(node: ResearchNodeData): TranslationKey`

- [ ] **Step 1: 失敗するテストを書く**

`researchLogic.test.ts` の import に `deriveNodeStateLabelKey` を追加し、ファイル末尾に追記:

```ts
describe("deriveNodeStateLabelKey", () => {
  it("completedは完了済みラベル", () => {
    expect(deriveNodeStateLabelKey(node("a", 0, 0, { state: "completed" }))).toBe(L.ui.research.stateCompleted);
  });
  it("researchableは研究可能ラベル", () => {
    expect(deriveNodeStateLabelKey(node("a", 0, 0, { state: "researchable" }))).toBe(L.ui.research.stateAvailable);
  });
  it("不可3状態はすべて研究不可ラベルへ畳む", () => {
    for (const state of ["unresearchableNotEnoughItem", "unresearchableNotEnoughPreNode", "unresearchableAllReasons"] as const) {
      expect(deriveNodeStateLabelKey(node("a", 0, 0, { state }))).toBe(L.ui.research.stateUnavailable);
    }
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/features/research/researchLogic.test.ts`
Expected: FAIL（`deriveNodeStateLabelKey` is not a function / export無し）

- [ ] **Step 3: 実装する**

`researchLogic.ts` の `deriveNodeCardState` 定義の直後に追加:

```ts
// カードの状態ラベル。ADR 0044: 不可の理由（不足/前提未達）は詳細ペインが担うのでカードでは3語へ畳む
// Card state label. ADR 0044: the reason for "unavailable" lives in the detail pane, so the card collapses to 3 words
export function deriveNodeStateLabelKey(node: ResearchNodeData): TranslationKey {
  const cardState = deriveNodeCardState(node);
  if (cardState.completed) return L.ui.research.stateCompleted;
  if (cardState.ready) return L.ui.research.stateAvailable;
  return L.ui.research.stateUnavailable;
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/features/research/researchLogic.test.ts`
Expected: PASS（既存テスト含め全件）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/features/research/researchLogic.ts moorestech_web/webui/src/features/research/researchLogic.test.ts
git commit -m "feat(research): ノード状態を3語ラベルキーへ写像するderiveNodeStateLabelKeyを追加

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: ResearchNodeCard にラベル行を描画する

**Files:**
- Modify: `moorestech_web/webui/src/features/research/ResearchNodeCard.tsx`
- Modify: `moorestech_web/webui/src/features/research/style.module.css`（`.nodeName` の直後に `.nodeState` を追加。`.node[data-*]` 行は不変）
- Create: `moorestech_web/webui/src/features/research/ResearchNodeCard.test.ts`

**Interfaces:**
- Consumes: Task 2 の `deriveNodeStateLabelKey(node)`
- Produces: `data-testid="research-node-state-<guid>"` の `<span>`

- [ ] **Step 1: 失敗するテストを書く**

`ResearchNodeCard.test.ts` を新規作成（`ResearchDetailPane.test.ts` と同じスタブ流儀）:

```ts
import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { ResearchNodeData } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
// ItemSlotはMantineProvider依存のためスタブ
// ItemSlot depends on MantineProvider, so stub it
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
}));

import ResearchNodeCard from "./ResearchNodeCard";

const guid = "86000000-0000-4000-8000-000000000002";
const node = (state: ResearchNodeData["state"]): ResearchNodeData => ({
  guid, state, iconItemId: 1,
  position: { x: 0, y: 0 }, prevGuids: [], consumeItems: [], rewardItems: [], unlockItemRecipeViewItemIds: [],
  unlockBlocks: [], unlockMachineRecipes: [], unlockConnectToolGuids: [], unlockTrainCarGuids: [],
});

function renderStateText(state: ResearchNodeData["state"]): string {
  const renderer = create(createElement(ResearchNodeCard, { node: node(state), left: 0, top: 0, selected: false }));
  return renderer.root.findByProps({ "data-testid": `research-node-state-${guid}` }).props.children;
}

describe("ResearchNodeCard", () => {
  it("状態ラベルを完了済み/研究可能/研究不可の3語で描く", () => {
    expect(renderStateText("completed")).toBe("ui.research.stateCompleted");
    expect(renderStateText("researchable")).toBe("ui.research.stateAvailable");
    expect(renderStateText("unresearchableNotEnoughItem")).toBe("ui.research.stateUnavailable");
    expect(renderStateText("unresearchableNotEnoughPreNode")).toBe("ui.research.stateUnavailable");
  });
  it("枠色用のdata属性は従来どおり付く", () => {
    const renderer = create(createElement(ResearchNodeCard, { node: node("researchable"), left: 0, top: 0, selected: false }));
    const card = renderer.root.findByProps({ "data-testid": `research-node-${guid}` });
    expect(card.props["data-ready"]).toBe(true);
    expect(card.props["data-completed"]).toBeUndefined();
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm test -- src/features/research/ResearchNodeCard.test.ts`
Expected: FAIL（`research-node-state-...` の要素が見つからない）

- [ ] **Step 3: カードにラベル行を追加する**

`ResearchNodeCard.tsx` の import を `import { deriveNodeCardState, deriveNodeStateLabelKey } from "./researchLogic";` にし、JSX を以下へ:

```tsx
      <span className={styles.nodeName}>{t(researchNameKey(node.guid))}</span>
      <ItemSlot itemId={node.iconItemId} />
      {/* 状態ラベル。枠色4状態を補助する3語表示（ADR 0044） */}
      {/* State label; 3-word text that supplements the 4-state border color (ADR 0044) */}
      <span className={styles.nodeState} data-testid={`research-node-state-${node.guid}`}>
        {t(deriveNodeStateLabelKey(node))}
      </span>
```

`style.module.css` の `.nodeName {...}` ブロック直後に追加:

```css
/* 状態ラベルは本文色固定。状態別の色付けはしない（ADR 0044） */
/* State label stays in the default text color; no per-state coloring (ADR 0044) */
.nodeState {
  font-size: 12px;
  color: var(--text-default);
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm test`
Expected: PASS（全件）

- [ ] **Step 5: 型・lintを通す**

Run: `cd moorestech_web/webui && pnpm tsc -b && pnpm lint`
Expected: エラー0

- [ ] **Step 6: コミットする**

```bash
git add moorestech_web/webui/src/features/research/ResearchNodeCard.tsx moorestech_web/webui/src/features/research/style.module.css moorestech_web/webui/src/features/research/ResearchNodeCard.test.ts
git commit -m "feat(research): ノードカードに状態ラベル（完了済み/研究可能/研究不可）を表示

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Unity側コンパイル確認と実表示確認

**Files:**
- 変更なし（確認のみ）

- [ ] **Step 1: C#側のLocalizationKeys再生成を含めてコンパイルする**

localization.csvはAssetDatabase外のため force-recompile が必須（memory: localization-csv-needs-force-recompile）:

Run: `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true` の後 `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 2: 研究画面の実表示でカードの重なりを確認する**

`unity-playmode-recorded-playtest` スキルで PlayMode 起動し研究画面（Rキー）を開いてスクリーンショットを取る。各カードのアイコン下にラベルが出ること、隣接ノードとカードが重ならないことを目視確認する。重なりが出た場合はplanを止めてユーザーへ報告する（座標はマスタ由来のため独断で調整しない）。

- [ ] **Step 3: bd note に確認結果を残す**

Run: `bd note moorestech-8sbd "実表示確認: <結果1行>"`

---

### Task 5: 全ブランチレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の機械的修正は適用し、設計判断はユーザーへ。

- [ ] **Step 2: pr-create スキルでPRを作成し、`moores-wt rm` でworktreeを畳む**

- [ ] **Step 3: `bd close moorestech-8sbd --reason="PR #<番号>"`**

---

## 配置と前例

| 項目 | 配置 | 前例 |
|---|---|---|
| 3語写像 `deriveNodeStateLabelKey` | `features/research/researchLogic.ts`（表示ロジック層） | 同ファイルの `deriveResearchButton` が state→tooltipKey を写像している |
| ラベル描画 | `ResearchNodeCard.tsx` | `features/challenge/ChallengeNodeCard.tsx` の `ChallengeStateKeys` + `<Text size="xs">` |
| 文言キー | `Localization/localization.csv` `ui.research.state*` | `ui.challenge.stateLocked/stateCurrent/stateCompleted` |
| データフロー | サーバー `node.state` → `deriveNodeCardState` → ラベル（読み手のみ。書き手・交差点を足さない） | — |

## 判断記録（ADR）

- 設計裁定: `docs/adr/0044-research-node-card-state-text-label.md`、`.decisions/2026-08-30-研究ノードカードに状態を3語の文字ラベルで明示する.md`
- ラベルは `ready` 判定を `deriveNodeCardState` 経由で取る（`deriveResearchButton` を直接呼ばない）。出所: agent前提（カードの状態正本は `deriveNodeCardState` に一本化されている前例）
- ラベル用 `<span>` は Mantine `Text` でなく素の `span` + CSS module。出所: agent前提（`ResearchNodeCard.tsx` は `nodeName` を素の `span` で描いており、同ファイル内の流儀に合わせる。Challengeカードは `Paper/Text` 流儀だが役割同型なのはラベルの存在であって描画部品ではない）
- フォントサイズ12px。出所: agent前提（`nodeName` 14px より一段小さく、副次情報として読ませる。裁定対象外の値）
- 実表示で重なりが出た場合の調整は裁定事項として止める。出所: agent前提（座標はマスタ由来・ADR 0044 Consequences）
