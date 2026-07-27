---
spec: docs/superpowers/specs/2026-07-27-remove-tutorial-screen-dimming-design.md
---

# Tutorial Screen Dimming Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** チュートリアル対象の黄色いDOM輪郭を維持しつつ、対象外画面を暗くする `spotlight` 機能を表現・描画できない状態にする。

**Architecture:** Unity producerは用途別 `AddOutlineHighlight(anchorId, message)` だけを呼び、store内部でwire種別 `outline` を設定する。Web契約は `outline` と `callout` だけを受理し、TutorialOverlayは巨大shadowを持たない輪郭だけを描画する。

**Tech Stack:** C# / Unity / NUnit / UniRx、TypeScript / React / Zod / Vitest / Playwright / CSS Modules

## Global Constraints

- 対象DOMの位置を示す黄色い輪郭、DOM追従、callout、アンカー解決通知は維持する。
- Web契約へ旧 `spotlight` が届いた場合は、暗黙に `outline` へ変換せず契約違反として拒否する。
- DOMアンカーの購読、位置とpaddingの反映、Unityへのanchor ack、pointer inputの扱いは変更しない。
- C#ファイル変更後は `uloop compile --project-path ./moorestech_client` を必ず実行する。
- 既存の未コミット変更を変更・ステージしない。

---

### Task 1: Web契約からspotlightを削除する

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/presentation.test.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/presentation.ts`

**Interfaces:**
- Consumes: `TutorialHighlightSchema.safeParse(input)`
- Produces: `kind: "outline" | "callout"` のみを受理する `TutorialHighlightSchema`

- [ ] **Step 1: 既存dirty fileを記録する**

Run: `git status --short && git hash-object moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

Expected: `_CompileRequester.cs` は未stage、内容hashは `c316e4df4f260c3c53192f54a27b057efdb3572a`。今回の対象へstageしない。

- [ ] **Step 2: 失敗する契約テストを書く**

`presentation.test.ts` のimportへ `TutorialHighlightSchema` を追加し、受理する2種と拒否する旧種別を追加する。

```ts
import {
  GameStateDataSchema, SkitPresentationDataSchema,
  TutorialHighlightSchema, TutorialPresentationDataSchema,
} from "./presentation";

  it.each(["outline", "callout"] as const)("accepts the %s tutorial highlight kind", (kind) => {
    expect(TutorialHighlightSchema.safeParse({
      highlightId: "highlight-1", anchorId: "game.crosshair", kind,
      message: "", paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(true);
  });

  it("rejects the removed spotlight tutorial highlight kind", () => {
    expect(TutorialHighlightSchema.safeParse({
      highlightId: "highlight-1", anchorId: "game.crosshair", kind: "spotlight",
      message: "", paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(false);
  });
```

- [ ] **Step 3: テストを実行して失敗を確認する**

Run: `pnpm test src/bridge/contract/schemas/presentation.test.ts`

Workdir: `moorestech_web/webui`

Expected: `rejects the removed spotlight...` が `expected true to be false` でFAILする。

- [ ] **Step 4: Web契約からspotlightを削除する**

```ts
export const TutorialHighlightSchema = z.object({
  highlightId: z.string(), anchorId: z.string(), kind: z.enum(["outline", "callout"]),
  messageKey: z.string().optional(), message: z.string(), paddingPx: z.number().nonnegative(),
  blocksPointerInput: z.boolean(),
});
```

- [ ] **Step 5: 契約テストを実行して通ることを確認する**

Run: `pnpm test src/bridge/contract/schemas/presentation.test.ts`

Workdir: `moorestech_web/webui`

Expected: 対象テストがすべてPASSする。

- [ ] **Step 6: Task 1をコミットする**

```bash
git add moorestech_web/webui/src/bridge/contract/schemas/presentation.test.ts \
  moorestech_web/webui/src/bridge/contract/schemas/presentation.ts
git commit -m "チュートリアルspotlightをWeb契約から削除"
```

### Task 2: Unity producerをoutline専用APIへ移行する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/TutorialPresentationStateStoreTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/TutorialPresentationStateStore.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/UIHighlightTutorialManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/ItemViewHighLightTutorialManager.cs`

**Interfaces:**
- Consumes: `TutorialPresentationStateStore.AddOutlineHighlight(string anchorId, string message)`
- Produces: `TutorialHighlightData.Kind == "outline"` のpresentation

- [ ] **Step 1: 失敗するstate storeテストを書く**

既存 `AddHighlightPublishesAnchorAndKind` を次へ置換する。

```csharp
        // outline専用APIはwire種別をstore内部で固定する
        // The outline-specific API fixes the wire kind inside the store
        [Test]
        public void AddOutlineHighlightPublishesAnchorAndOutlineKind()
        {
            var store = new TutorialPresentationStateStore();
            var challengeId = Guid.NewGuid();
            store.BeginSession(challengeId);

            store.AddOutlineHighlight("recipe.craft-button", "Hold to craft");

            var current = store.GetCurrent();
            Assert.AreEqual(challengeId.ToString(), current.ChallengeId);
            Assert.AreEqual("recipe.craft-button", current.Highlights[0].AnchorId);
            Assert.AreEqual("outline", current.Highlights[0].Kind);
        }
```

同ファイル内の他2テストも `store.AddOutlineHighlight(anchorId, message)` へ更新する。

- [ ] **Step 2: 対象Unityテストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TutorialPresentationStateStoreTest"`

Workdir: repository root

Expected: `AddOutlineHighlight` が未定義のコンパイルエラーでFAILする。domain reload中なら45秒待って同じコマンドを再実行する。

- [ ] **Step 3: state storeをoutline専用APIへ変更する**

`AddHighlight` を次のsignatureへ変更し、任意kind引数を削除する。

```csharp
        // outline用途だけを公開し、廃止済みkindの再流入を防ぐ
        // Expose only the outline use case to prevent removed kinds from returning
        public ITutorialView AddOutlineHighlight(string anchorId, string message)
        {
            var highlight = new TutorialHighlightData
            {
                HighlightId = Guid.NewGuid().ToString(),
                AnchorId = anchorId,
                Kind = "outline",
                Message = message,
                PaddingPx = 8,
                BlocksPointerInput = false,
            };
            var highlights = new List<TutorialHighlightData>(_current.Highlights) { highlight };
            SetHighlights(highlights.ToArray());
            return new TutorialPresentationView(this, _current.TutorialSessionId, highlight.HighlightId);
        }
```

- [ ] **Step 4: 2つのproducerを新APIへ移行する**

両managerのreturnを次の形へ変更する。

```csharp
return TutorialPresentationStateStore.Instance.AddOutlineHighlight(anchorId, highlightParam.HighLightText);
```

- [ ] **Step 5: 対象Unityテストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TutorialPresentationStateStoreTest"`

Expected: 3テストすべてPASSする。domain reload中なら45秒待って再実行する。

- [ ] **Step 6: Unityコンパイルを実行する**

Run: `uloop compile --project-path ./moorestech_client`

Expected: compilation errorsが0件。domain reload中なら45秒待って再実行する。

- [ ] **Step 7: Task 2をコミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/WebUi/TutorialPresentationStateStoreTest.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/TutorialPresentationStateStore.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/UIHighlightTutorialManager.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/ItemViewHighLightTutorialManager.cs
git commit -m "チュートリアルハイライトをoutline専用APIへ移行"
```

### Task 3: 暗転CSSを削除しブラウザ回帰テストで観測する

**Files:**
- Create: `moorestech_web/webui/e2e/tests/tutorial.spec.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`
- Modify: `moorestech_web/webui/src/features/tutorial/style.module.css`

**Interfaces:**
- Consumes: `setTopicScenario(page, "tutorialOutline" | "tutorialEmpty")`
- Produces: `outline` presentation eventと、巨大な黒shadowを持たないDOM輪郭

- [ ] **Step 1: 失敗するPlaywright回帰テストを書く**

```ts
import { expect, test } from "@playwright/test";
import { setTopicScenario } from "../support/mockControl";

test.afterEach(async ({ page }) => {
  await setTopicScenario(page, "tutorialEmpty");
});

test("tutorial outline highlights the target without dimming the rest of the screen", async ({ page }) => {
  await page.goto("/");
  await setTopicScenario(page, "tutorialOutline");

  const highlight = page.getByTestId("tutorial-overlay").locator("[data-kind='outline']");
  await expect(highlight).toBeVisible();
  await expect(highlight).toHaveCSS("border-top-color", "rgb(255, 221, 87)");
  expect(await highlight.evaluate((element) => getComputedStyle(element).boxShadow)).not.toContain("9999px");
});
```

- [ ] **Step 2: E2Eテストを実行して失敗を確認する**

Run: `pnpm test:e2e -- e2e/tests/tutorial.spec.ts`

Workdir: `moorestech_web/webui`

Expected: `tutorialOutline` / `tutorialEmpty` がまだ `TopicScenario` に存在しないため、E2EのTypeScript検査が引数型エラーでFAILする。

- [ ] **Step 3: 既存topic controlへtutorial scenarioを追加する**

`controls` へ既存の `control(...)` と `state.topicOverrides` を利用する2 scenarioを追加する。

```ts
  // チュートリアル輪郭の表示・消去を実topic経路で駆動する
  // Drive tutorial outline visibility through the real topic path
  tutorialOutline: () => control(Topics.tutorialPresentation, {
    tutorialSessionId: "tutorial-session-1", revision: 1, challengeId: "tutorial-challenge-1",
    highlights: [{
      highlightId: "tutorial-highlight-1",
      anchorId: "game.crosshair",
      kind: "outline" as const,
      message: "", paddingPx: 8, blocksPointerInput: false,
    }],
  }),
  tutorialEmpty: () => control(Topics.tutorialPresentation, {
    tutorialSessionId: "", revision: 0, challengeId: "", highlights: [],
  }),
```

- [ ] **Step 4: spotlight暗転CSSを削除する**

`style.module.css` から次のrule全体を削除する。

```css
.highlight[data-kind="spotlight"] {
  box-shadow: 0 0 0 9999px rgb(0 0 0 / 58%);
}
```

- [ ] **Step 5: E2Eテストを実行して通ることを確認する**

Run: `pnpm test:e2e -- e2e/tests/tutorial.spec.ts`

Workdir: `moorestech_web/webui`

Expected: 1テストがPASSする。

- [ ] **Step 6: Web全体のテスト・build・残存参照検索を実行する**

Run: `pnpm test && pnpm build && ! rg -n 'spotlight|9999px' src --glob '!**/*.test.ts'`

Workdir: `moorestech_web/webui`

Expected: Vitest全件PASS、build成功、`spotlight` と `9999px` の残存参照0件。

- [ ] **Step 7: Task 3をコミットする**

```bash
git add moorestech_web/webui/e2e/tests/tutorial.spec.ts \
  moorestech_web/webui/e2e/mock-host/topics/topicControls.ts \
  moorestech_web/webui/src/features/tutorial/style.module.css
git commit -m "チュートリアル画面暗転を撤去"
```

### Task 4: 全体QAとブランチレビューを行う

**Files:**
- Verify: `docs/superpowers/specs/2026-07-27-remove-tutorial-screen-dimming-design.md`
- Verify: Task 1〜3の全変更ファイル

**Interfaces:**
- Consumes: Task 1〜3のコミット済み差分
- Produces: コンパイル・対象テスト・Web全体テスト・レビュー結果

- [ ] **Step 1: 変更要件と残存参照を確認する**

Run: `rg -n 'spotlight|9999px|AddHighlight\(' moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial moorestech_client/Assets/Scripts/Client.Tests/WebUi moorestech_web/webui/src moorestech_web/webui/e2e --glob '!**/*.test.ts' --glob '!**/*.spec.ts'`

Expected: 該当なし。

- [ ] **Step 2: Unityの対象テストとコンパイルを再実行する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TutorialPresentationStateStoreTest"`

Run: `uloop compile --project-path ./moorestech_client`

Expected: 対象テスト全件PASS、compilation errors 0件。

- [ ] **Step 3: Webの全テスト・対象E2E・buildを再実行する**

Run: `pnpm test && pnpm test:e2e -- e2e/tests/tutorial.spec.ts && pnpm build`

Workdir: `moorestech_web/webui`

Expected: 全コマンド成功。

- [ ] **Step 4: 必ずmoores-code-reviewスキルで全ブランチレビューを実行する**

`.agents/skills/moores-code-review/SKILL.md` を読み、現在branchのbase差分と未コミット差分を対象に全レンズを実行する。指摘を修正した場合はStep 1〜3を再実行する。

- [ ] **Step 5: 最終状態をコミットする**

レビュー修正があれば対象ファイルだけをstageしてコミットする。`git status --short` と `git log -5 --oneline` で、今回の全成果物がコミット済みかつ既存のユーザー変更が未stageのまま保持されていることを確認する。

Run: `test "$(git hash-object moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs)" = "c316e4df4f260c3c53192f54a27b057efdb3572a"`

Expected: exit 0。既存dirty fileの内容が開始時から変わっていない。

## 判断記録（ADR）

- 対応spec: [チュートリアル画面暗転の撤去設計](../specs/2026-07-27-remove-tutorial-screen-dimming-design.md#判断記録adr)
- シミュレーター予測→ユーザー承認（2026-07-27）: 任意kind引数を `AddOutlineHighlight` へ置換し、廃止種別とtypoをUnity側で表現不能にする。
- agent前提（拒否権つき）: Playwrightは既存 `topicControls` / `topicOverrides` にtutorial scenarioを追加し、製品契約と同じsnapshot・event経路で利用者観測を検証する。
