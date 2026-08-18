# 研究UI改修（枠色4状態・ステージ全域占有・種類別解放物表示） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Web UIの研究ツリー画面を、(1)ノードの4状態が一目で分かり、(2)ステージ全域を使い、(3)研究の解放物（ブロック等）が種類別に見える形へ改修する。

**Architecture:** サーバー（Game.Research）は変更しない。Unityホスト側のDTO変換（`ResearchNodeDtoFactory`）を拡張して`clearedActions`全種をトピックへ通し、Web側は既存の`hasEnoughItems`によるクライアント側充足再計算をノードカードの視覚状態へ拡張、レイアウトはstage絶対配置の全域占有へ切り替える。設計はADR 0014が正。

**Tech Stack:** Unity C#（Client.WebUiHost）/ React + TypeScript + zod + CSS Modules（moorestech_web/webui）/ vitest / Playwright e2e / uloop

## Requirements

ADR: `docs/adr/0014-research-ui-four-states-fullstage-unlock-sections.md`（実装前に必読）

1. ノードカードは4状態を枠色の系統で見分ける: 未解放=減光45%（現状維持）/ 研究可能・アイテム不足=通常グレー枠 / 研究可能・充足=シアン枠（`--select-cyan`）/ 研究済み=白枠（現状維持）。受け入れ基準: 4状態のノードが並ぶmockシナリオでdata属性とCSSが4通りに分かれ、スクリーンショットで判別できる。
2. アイテム充足はクライアント側でライブ再計算する（`inventory`トピック×`hasEnoughItems`。研究ボタンと同じロジック）。受け入れ基準: サーバーstateが`unresearchableNotEnoughItem`のままでも、所持数が満ちればカードがシアン枠になる（vitestで検証）。
3. 研究パネルはステージ全域（1280×720の上下左右端まで）を占有する。受け入れ基準: e2eで`research-tree`要素の矩形がstage全体と一致する。
4. 持ち物パネルは今まで通りの位置・見た目で研究パネルの上に重畳表示され、アイテム把持操作も維持される。受け入れ基準: e2eで研究画面中に`main-grid`が可視かつ最前面（クリック可能）。
5. チャレンジHUD・キー操作ヒントは全面パネルの上に表示され続ける。受け入れ基準: e2eで`research-key-hints`が可視。
6. `clearedActions`のうち unlockBlock / unlockMachineRecipe / unlockItemRecipeView / unlockConnectTool / unlockTrainCar / giveItem の6種をDTOへ通す。受け入れ基準: C#契約テストとzod契約テストが新フィールド込みで一致する。
7. 詳細ペインは解放物を種類別ラベル付きセクション（`--text-muted`ラベル+アイコン列、connect tool / train car は名前テキスト行）で縦積み表示する。空の種類のセクションは出さない。受け入れ基準: mockノードで「解放: ブロック」等のラベルと中身がe2eで確認できる。
8. 詳細ペインの消費アイテムは不足40%減光+ツールチップで所持/必要数（`CraftRecipeView`の`materialTooltip`様式）。受け入れ基準: e2eまたはvitestでツールチップ文字列に所持数・必要数が入る。
9. webui-designスキル（様式ホワイトリスト）を実装より先に更新する: §8.5の4状態語彙・`--select-cyan`用途追加・§1/§8.14の研究画面例外。
10. やらないこと: サーバー側（Game.Research・プロトコル）の変更 / `ResearchTopic`の再取得タイミング変更 / playSkit等の演出系アクション表示 / unlockCraftRecipe・unlockItemStackLevel等の実データ0件アクション表示 / 接続線の状態色 / チャレンジ画面の変更 / uGUI側の変更（廃止済み）。

## Global Constraints

- 作業ブランチ: `feature/research-ui-refresh`（origin/master起点。`moores-wt new`で作った使い捨てworktreeで作業する）
- AGENTS.md全規約に従う。特に: partial禁止 / `Func<>`禁止 / 1ファイル200行以下 / [SerializeField]なし（今回不使用） / コメントは日本語・英語2行セット / .cs変更後は必ず `uloop compile --project-path ./moorestech_client`
- webui-design SKILL（`.agents/skills/webui-design/SKILL.md`）はホワイトリスト。書かれていない表現は使わない。Task 1の様式更新が全UIタスクの前提
- 色・z層は必ずトークン経由（`moorestech_web/webui/src/app/tokens.css`）。機能側CSSへの直書き禁止
- 表示文字列は必ず`t()`経由。新規キーは`Localization/localization.csv`へ追加し`npm run gen:i18n`で生成（`moorestech_web/webui`で実行）
- webuiのコマンドはすべて `moorestech_web/webui/` で実行: `npm run test`（vitest）/ `npm run lint` / `npx playwright test --config e2e/playwright.config.ts tests/research`
- Unityテストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>" --test-mode EditMode`（既定はPlayModeなのでEditMode明示必須）
- webui e2eは既定ロケールjapaneseで英語literal期待の既存赤10件がある（bd moorestech-2lh.1）。今回触らないspecの赤は既存として切り分け、研究系specは緑を必須とする
- wire契約のフィクスチャ `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/research_tree.json` はC# NUnitとTS vitestの共有正本。DTO変更時は両側テストとフィクスチャを同一コミットで更新する
- zodスキーマは`.strict()`のため、DTO新フィールドはmock-host・全フィクスチャへ同時追加しないと契約テストが落ちる

---

### Task 1: 設計文書の持ち込みとwebui-design様式更新

**Files:**
- Copy+Commit（メインクローン `$HOME/…/moorestech（メインクローン）/` から本worktreeへ。worktree作成時に未追跡ファイルはコピーされないため）:
  - `docs/adr/0014-research-ui-four-states-fullstage-unlock-sections.md`
  - `docs/superpowers/plans/2026-08-18-research-ui-refresh.md`（本ファイル）
  - `.decisions/2026-08-18-研究ノードは枠色の系統で4状態を見分ける.md`
  - `.decisions/2026-08-18-研究画面はステージ全域を占有しHUD重畳を許容する.md`
  - `.decisions/2026-08-18-研究の解放物は種類別ラベル付きセクションで表示する.md`
  - `.decisions/2026-08-18-研究画面全面化でも持ち物パネルは今まで通り重畳表示する.md`
  - `CONTEXT.md`（「### 研究」節の追記分。メインクローン版をそのままコピー）
- Modify: `.agents/skills/webui-design/SKILL.md`

**Interfaces:**
- Consumes: なし（先頭タスク）
- Produces: 以後の全タスクが参照する様式定義（§8.5の4状態語彙・シアン用途・全域占有例外）

- [ ] **Step 1: メインクローンから設計文書7点をコピーする**

```bash
SRC=<メインクローンの絶対パス>
cp "$SRC/docs/adr/0014-research-ui-four-states-fullstage-unlock-sections.md" docs/adr/
cp "$SRC/docs/superpowers/plans/2026-08-18-research-ui-refresh.md" docs/superpowers/plans/
cp "$SRC"/.decisions/2026-08-18-研究*.md .decisions/
cp "$SRC/CONTEXT.md" CONTEXT.md
```

- [ ] **Step 2: webui-design SKILL.md §8.5 の研究ノードカード節を4状態語彙へ書き換える**

`.agents/skills/webui-design/SKILL.md` の §8.5「研究ノードカード」段落の状態記述を以下へ置換する:

```markdown
- **研究ノードカード**: 「名前1行(ellipsis) + `ItemSlot`アイコン」の縦積みのみ。説明・消費・報酬・ボタンはカードに載せない。
  面は `--research-node-face`、枠は `--research-node-border`（tokens.cssのトークン）。
  状態はdata属性で4値を表す（ADR 0014）:
  `data-locked`（前提未達）=opacity減衰45% / 無印（前提充足・アイテム不足）=通常グレー枠 /
  `data-researchable`（今すぐ研究できる）=`--select-cyan`の枠色 / `data-completed`=`--text-default`の白枠。
  アイテム充足はサーバーstateでなくインベントリトピックからのクライアント側再計算で判定する（ライブ追従）。
  `data-selected` は従来どおり `--text-high-contrast` のoutline。新しい色相・光彩は使わない。
```

- [ ] **Step 3: §5 の `--select-cyan` 用途リストへ研究ノードを追加する**

「用途はスロット選択枠と〜に限る。」の列挙へ「研究ノードカードの実行可能状態の枠色点灯（§8.5・ADR 0014）」を追記する。

- [ ] **Step 4: §1 と §8.14 へ研究画面の例外を明記する**

§1の「全画面UIは作らない」の項へ追記:

```markdown
  - 例外（ADR 0014・ユーザー裁定 2026-08-18）: 研究ツリー画面のみ、半透明GamePanelがステージ全域
    （安全帯含む上下左右端まで）を占有してよい。面は従来どおり半透明で世界は透ける。
    チャレンジHUD・キー操作ヒント・持ち物パネルはこのパネルより上の層（`--z-overlay-panel-chrome`）に重畳する。
```

§8.14チャレンジHUDの「`--menu-upper-safe-area` はその単一HUDが収まる高さを確保する」の直後へ「研究画面はADR 0014の例外として安全帯を覆う全域パネルを敷き、HUDはその上に重畳される」を追記する。

- [ ] **Step 5: 詳細ペインの種類別セクションを §8.5 グラフ内詳細ペインへ追記する**

「内容は名前・説明・消費(`ItemSlot`+insufficient)・報酬/解放(`ItemSlot`)・主要アクションボタン（青グラデ）・閉じるボタン」を以下へ置換:

```markdown
  内容は名前・説明・「必要アイテム」ラベル付き消費（`ItemSlot`+insufficient+所持/必要ツールチップ＝
  CraftRecipeViewのmaterialTooltip様式）・種類別ラベル付き解放セクション（「解放: ブロック」=`BlockSlot`、
  「解放: 機械レシピ」=出力アイテムの`ItemSlot`、「解放: クラフトレシピ」=`ItemSlot`、「報酬アイテム」=個数付き
  `ItemSlot`、「解放: その他」=connect tool/train car名のテキスト行）・主要アクションボタン（青グラデ）・
  閉じるボタン。ラベルは`--text-muted`、空の種類のセクションは出さない（§4の無札並置禁止に従う）。
```

- [ ] **Step 6: コミットする**

```bash
git add docs/adr .decisions CONTEXT.md docs/superpowers/plans .agents/skills/webui-design/SKILL.md
git commit -m "docs: 研究UI改修のADR・裁定・用語とwebui-design様式を更新する"
```

---

### Task 2: DTO拡張（C#: clearedActions全種の抽出）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Research/ResearchTopicDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Research/ResearchNodeDtoFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractResearchTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/research_tree.json`

**Interfaces:**
- Consumes: `Mooresmaster.Model.GameActionModule` の `UnlockBlockGameActionParam.UnlockBlockGuids` / `UnlockMachineRecipeGameActionParam.UnlockMachineRecipeGuids` / `UnlockConnectToolGameActionParam.UnlockConnectToolGuids` / `UnlockTrainCarGameActionParam.UnlockTrainCarGuids`（前例: `moorestech_server/Assets/Scripts/Game.Action/GameActionExecutor.cs:115-190` の同キャスト）、`MasterHolder.BlockMaster.GetBlockId(Guid)`（`Core.Master/BlockMaster.cs:104`）、`MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid)`（`Core.Master/MachineRecipesMaster.cs:36`）と `MachineRecipeMasterElement.OutputItems[].ItemGuid`
- Produces: `ResearchNodeDto` の新フィールド `UnlockBlocks: List<ResearchUnlockBlockDto{BlockId:int, BlockGuid:string}>` / `UnlockMachineRecipeOutputItemIds: List<int>` / `UnlockConnectToolGuids: List<string>` / `UnlockTrainCarGuids: List<string>`（wire上は camelCase: `unlockBlocks[].blockId/.blockGuid`, `unlockMachineRecipeOutputItemIds`, `unlockConnectToolGuids`, `unlockTrainCarGuids`）。Task 3のzodスキーマ・Task 8の表示が消費する

- [ ] **Step 1: 契約テストとフィクスチャを先に新フィールドへ拡張する（失敗するテスト）**

`WireContractResearchTest.cs` の2ノードへ新フィールドを追加する。ノード1は空リスト、ノード2は全種1件以上:

```csharp
// ノード1（completed側）へ追加:
UnlockBlocks = new List<ResearchUnlockBlockDto>(),
UnlockMachineRecipeOutputItemIds = new List<int>(),
UnlockConnectToolGuids = new List<string>(),
UnlockTrainCarGuids = new List<string>(),
// ノード2（前提未達側）へ追加:
UnlockBlocks = new List<ResearchUnlockBlockDto> { new() { BlockId = 7, BlockGuid = "44444444-4444-4444-8444-444444444444" } },
UnlockMachineRecipeOutputItemIds = new List<int> { 9 },
UnlockConnectToolGuids = new List<string> { "55555555-5555-4555-8555-555555555555" },
UnlockTrainCarGuids = new List<string> { "66666666-6666-4666-8666-666666666666" },
```

`research_tree.json` の両ノードへ同内容を追加する（camelCase）:

```json
      "unlockBlocks": [],
      "unlockMachineRecipeOutputItemIds": [],
      "unlockConnectToolGuids": [],
      "unlockTrainCarGuids": []
```

（ノード2は `"unlockBlocks": [ { "blockId": 7, "blockGuid": "44444444-4444-4444-8444-444444444444" } ]`, `"unlockMachineRecipeOutputItemIds": [ 9 ]`, `"unlockConnectToolGuids": [ "55555555-5555-4555-8555-555555555555" ]`, `"unlockTrainCarGuids": [ "66666666-6666-4666-8666-666666666666" ]`）

- [ ] **Step 2: DTOへ新フィールドと `ResearchUnlockBlockDto` を追加する**

`ResearchTopicDtos.cs` の `ResearchNodeDto` 末尾へ:

```csharp
        public List<ResearchUnlockBlockDto> UnlockBlocks;
        public List<int> UnlockMachineRecipeOutputItemIds;
        public List<string> UnlockConnectToolGuids;
        public List<string> UnlockTrainCarGuids;
```

新クラス（同ファイル内・既存DTO群の並びに追加）:

```csharp
    /// <summary>
    /// unlockBlock解放の表示用DTO。IconはBlockId、名前はGuid導出キーで引く
    /// Display DTO for unlockBlock; icon via BlockId, name via the Guid-derived key
    /// </summary>
    public class ResearchUnlockBlockDto
    {
        public int BlockId;
        public string BlockGuid;
    }
```

- [ ] **Step 3: ResearchNodeDtoFactory で全種を抽出する**

`Create()` の初期化へ4リストの `new` を追加し、`AppendActionItems` の `else if` 連鎖へ4分岐を追加する:

```csharp
                else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockBlock)
                {
                    var unlock = (UnlockBlockGameActionParam)action.GameActionParam;
                    foreach (var blockGuid in unlock.UnlockBlockGuids)
                        dto.UnlockBlocks.Add(new ResearchUnlockBlockDto { BlockId = MasterHolder.BlockMaster.GetBlockId(blockGuid).AsPrimitive(), BlockGuid = blockGuid.ToString() });
                }
                else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockMachineRecipe)
                {
                    // 機械レシピは出力アイテムのアイコンで代表させる（§8.7の代表出力アイテム前例）
                    // Represent machine recipes by their output item icons (per the §8.7 precedent)
                    var unlock = (UnlockMachineRecipeGameActionParam)action.GameActionParam;
                    foreach (var recipeGuid in unlock.UnlockMachineRecipeGuids)
                    foreach (var output in MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid).OutputItems)
                        dto.UnlockMachineRecipeOutputItemIds.Add(MasterHolder.ItemMaster.GetItemId(output.ItemGuid).AsPrimitive());
                }
                else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockConnectTool)
                {
                    var unlock = (UnlockConnectToolGameActionParam)action.GameActionParam;
                    foreach (var toolGuid in unlock.UnlockConnectToolGuids) dto.UnlockConnectToolGuids.Add(toolGuid.ToString());
                }
                else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockTrainCar)
                {
                    var unlock = (UnlockTrainCarGameActionParam)action.GameActionParam;
                    foreach (var carGuid in unlock.UnlockTrainCarGuids) dto.UnlockTrainCarGuids.Add(carGuid.ToString());
                }
```

注: `BlockId.AsPrimitive()` が無い場合（UnitGenerator設定差）はコンパイルエラーで判明する。その際は `GetBlockId(blockGuid)` の戻り値型定義（`Core.Master/BlockMaster.cs`）を確認し、`(int)` 変換等の既存前例（`GameActionExecutor.cs`のBlockId利用箇所）に合わせる。

- [ ] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [ ] **Step 5: 契約テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractResearchTest" --test-mode EditMode`
Expected: PASS（fixture mismatchが出たらStep 1のJSONとDTOの食い違いを直す）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Research moorestech_client/Assets/Scripts/Client.Tests/WebUi
git commit -m "feat: 研究DTOへclearedActions全種の解放物を通す"
```

---

### Task 3: Web契約側（zodスキーマとフィクスチャ）

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/research.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/researchFixtures.ts`

**Interfaces:**
- Consumes: Task 2のwireフィールド名（`unlockBlocks[].blockId/.blockGuid`, `unlockMachineRecipeOutputItemIds`, `unlockConnectToolGuids`, `unlockTrainCarGuids`）
- Produces: `ResearchNodeData` 型（`payloadTypes.ts:126` の `z.infer` 経由で自動拡張）に上記4フィールドが乗る。Task 5-8が消費する

- [ ] **Step 1: wireContract.test.ts の research 節へ新フィールドの型消費アサーションを足す（失敗するテスト）**

`describe("research_tree fixture")` 内へ:

```ts
  it("解放物4種のフィールドを受理し型消費できる", () => {
    const data = loadFixture("research_tree.json") as ResearchTreeData;
    expect(validateTopicPayload(Topics.researchTree, data)).toBe(true);
    const node = data.nodes[1];
    expect(node.unlockBlocks[0]).toEqual({ blockId: 7, blockGuid: "44444444-4444-4444-8444-444444444444" });
    expect(node.unlockMachineRecipeOutputItemIds).toEqual([9]);
    expect(node.unlockConnectToolGuids.length + node.unlockTrainCarGuids.length).toBe(2);
  });
```

Run: `npm run test -- wireContract`
Expected: FAIL（zodがstrictで未知キー拒否 → validateTopicPayload false）

- [ ] **Step 2: zodスキーマへ4フィールドを追加する**

`research.ts` の `ResearchNodeDataSchema` へ（`unlockItemIds` の次）:

```ts
  unlockBlocks: z.array(z.object({ blockId: z.number(), blockGuid: GuidSchema }).strict()),
  unlockMachineRecipeOutputItemIds: z.array(z.number()),
  unlockConnectToolGuids: z.array(GuidSchema),
  unlockTrainCarGuids: z.array(GuidSchema),
```

- [ ] **Step 3: mock-host の researchFixtures.ts を新契約へ更新し、4状態検証用の第4ノードを足す**

3ノードへ空の4フィールドを追加したうえで、ノード3（researchable）へ解放物を持たせ、第4ノード（アイテム不足）を追加する:

```ts
// ノード3(researchableNodeGuid)の変更: 解放物セクションのe2e検証用
      unlockItemIds: [],
      unlockBlocks: [{ blockId: 1, blockGuid: "44444444-4444-4444-8444-444444444444" }],
      unlockMachineRecipeOutputItemIds: [2],
      unlockConnectToolGuids: ["55555555-5555-4555-8555-555555555555"],
      unlockTrainCarGuids: ["66666666-6666-4666-8666-666666666666"],
```

```ts
// 第4ノード: 前提充足・アイテム不足（mockインベントリはitemId1を計15個しか持たない）
// Fourth node: prerequisites met but items lacking (the mock inventory owns only 15 of itemId 1)
export const itemLackingNodeGuid = "77777777-7777-4777-8777-777777777777";
    {
      guid: itemLackingNodeGuid,
      state: "unresearchableNotEnoughItem",
      iconItemId: 2,
      position: { x: 600.0, y: -240.0 },
      prevGuids: ["11111111-1111-4111-8111-111111111111"],
      consumeItems: [{ itemId: 1, count: 999 }],
      rewardItems: [],
      unlockItemIds: [],
      unlockBlocks: [],
      unlockMachineRecipeOutputItemIds: [],
      unlockConnectToolGuids: [],
      unlockTrainCarGuids: [],
    },
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `npm run test -- wireContract` → PASS
Run: `npm run test` → 既存vitest全緑（researchFixturesを参照するmock-host testsも含む）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/bridge moorestech_web/webui/e2e/mock-host/researchFixtures.ts
git commit -m "feat: 研究解放物4種のwire契約をWeb側スキーマとmockへ反映する"
```

---

### Task 4: i18nキー追加（セクションラベル6種）

**Files:**
- Modify: `Localization/localization.csv`（`ui.research.title` 行の直後・127-136行の研究ブロック内）
- Regenerate: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（手編集禁止・生成のみ）

**Interfaces:**
- Consumes: なし
- Produces: `L.ui.research.consumeItemsLabel / unlockBlocksLabel / unlockMachineRecipesLabel / unlockCraftRecipesLabel / rewardItemsLabel / unlockOthersLabel`。Task 8が消費する

- [ ] **Step 1: CSVへ6行追加する（列順は key,Source,english,japanese）**

```csv
ui.research.consumeItemsLabel,Required items,Required items,必要アイテム
ui.research.unlockBlocksLabel,Unlocks: Blocks,Unlocks: Blocks,解放: ブロック
ui.research.unlockMachineRecipesLabel,Unlocks: Machine recipes,Unlocks: Machine recipes,解放: 機械レシピ
ui.research.unlockCraftRecipesLabel,Unlocks: Crafting recipes,Unlocks: Crafting recipes,解放: クラフトレシピ
ui.research.rewardItemsLabel,Reward items,Reward items,報酬アイテム
ui.research.unlockOthersLabel,Unlocks: Others,Unlocks: Others,解放: その他
```

- [ ] **Step 2: 生成を実行し、鮮度テストを通す**

Run: `npm run gen:i18n`（`moorestech_web/webui/`）
Run: `npm run test -- localizationKeysFreshness`
Expected: PASS（生成物に6キーが入る）

- [ ] **Step 3: コミットする**

```bash
git add Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated
git commit -m "feat: 研究詳細ペインの種類別ラベル6キーを追加する"
```

---

### Task 5: researchLogic の4状態導出（クライアント充足のライブ再計算）

**Files:**
- Modify: `moorestech_web/webui/src/features/research/researchLogic.ts:51-61`
- Test: `moorestech_web/webui/src/features/research/researchLogic.test.ts`

**Interfaces:**
- Consumes: `hasEnoughItems`（`src/shared/ownedCounts.ts`）、Task 3の `ResearchNodeData`
- Produces: `deriveNodeCardState(node: ResearchNodeData, owned: Map<number, number>): NodeCardState`（`NodeCardState = { completed: boolean; ready: boolean; locked: boolean }`）。Task 6が消費する。旧シグネチャ `deriveNodeCardState(state)` と `researchable` フィールドは廃止

- [ ] **Step 1: 失敗するテストを書く**

`researchLogic.test.ts` へ追加:

```ts
describe("deriveNodeCardState", () => {
  const owned = new Map([[1, 5]]);
  it("完了ノードはcompletedのみ立つ", () => {
    expect(deriveNodeCardState(node("a", 0, 0, { state: "completed" }), owned))
      .toEqual({ completed: true, ready: false, locked: false });
  });
  it("前提未達はlocked", () => {
    expect(deriveNodeCardState(node("a", 0, 0, { state: "unresearchableNotEnoughPreNode" }), owned))
      .toEqual({ completed: false, ready: false, locked: true });
  });
  it("前提充足でも所持不足ならready無しの通常表示", () => {
    const n = node("a", 0, 0, { state: "unresearchableNotEnoughItem", consumeItems: [{ itemId: 1, count: 6 }] });
    expect(deriveNodeCardState(n, owned)).toEqual({ completed: false, ready: false, locked: false });
  });
  it("サーバーstateがアイテム不足でも所持が満ちればready（ライブ再計算）", () => {
    const n = node("a", 0, 0, { state: "unresearchableNotEnoughItem", consumeItems: [{ itemId: 1, count: 5 }] });
    expect(deriveNodeCardState(n, owned)).toEqual({ completed: false, ready: true, locked: false });
  });
});
```

Run: `npm run test -- researchLogic`
Expected: FAIL（シグネチャ不一致）

- [ ] **Step 2: 実装を書き換える**

```ts
// カードのdata属性用の4状態導出。充足はインベントリからのライブ再計算（ADR 0014）
// Derive the card's 4-state data attributes; sufficiency is recomputed live from the inventory (ADR 0014)
export type NodeCardState = { completed: boolean; ready: boolean; locked: boolean };

export function deriveNodeCardState(node: ResearchNodeData, owned: Map<number, number>): NodeCardState {
  const completed = node.state === "completed";
  const preNodeMet = isPreNodeMet(node.state);
  return {
    completed,
    ready: !completed && preNodeMet && hasEnoughItems(node.consumeItems, owned),
    locked: !completed && !preNodeMet,
  };
}
```

- [ ] **Step 3: テストを実行して通ることを確認する**

Run: `npm run test -- researchLogic`
Expected: PASS（既存のderiveNodeCardState旧テストがあれば新仕様へ書き換え。この時点で`ResearchNodeCard.tsx`が型エラーになるのは次タスクで解消するため、vitest対象外なら許容。`npm run lint`はTask 6完了後に通す）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_web/webui/src/features/research/researchLogic.ts moorestech_web/webui/src/features/research/researchLogic.test.ts
git commit -m "feat: 研究ノード状態を所持数ライブ再計算込みの4状態へ導出する"
```

---

### Task 6: ノードカードの4状態表示（シアン枠）

**Files:**
- Modify: `moorestech_web/webui/src/features/research/ResearchNodeCard.tsx`
- Modify: `moorestech_web/webui/src/features/research/ResearchTreePanel.tsx:35-38`
- Modify: `moorestech_web/webui/src/features/research/style.module.css:39-41`
- Test: `moorestech_web/webui/e2e/tests/research/research.spec.ts`

**Interfaces:**
- Consumes: Task 5の `deriveNodeCardState(node, owned)`、Task 3の `itemLackingNodeGuid`（mock-host）
- Produces: ノードカードのdata属性契約: `data-completed` / `data-researchable`（=ready） / `data-locked` / 無印（アイテム不足）。CSS: researchable=`--select-cyan`枠

- [ ] **Step 1: ResearchNodeCard へ owned を渡し、ready を data-researchable に割り当てる**

```tsx
type Props = {
  node: ResearchNodeData;
  owned: Map<number, number>;
  left: number;
  top: number;
  selected: boolean;
  onSelect: (guid: string) => void;
};

export default function ResearchNodeCard({ node, owned, left, top, selected, onSelect }: Props) {
  const cardState = deriveNodeCardState(node, owned);
  // ...（data-researchable={cardState.ready || undefined} へ変更、他は現状維持）
```

`ResearchTreePanel.tsx` の `renderResearchNode` へ `owned={owned}` を追加し、useCallback依存配列へ `owned` を足す。

- [ ] **Step 2: CSSへシアン枠を追加する**

`style.module.css` の状態ルール群（39-41行）へ:

```css
/* 研究可能（充足）はシアン枠で点灯する。ADR 0014の4状態語彙 */
/* Ready-to-research lights up with a cyan border; the 4-state vocabulary of ADR 0014 */
.node[data-researchable] { border-color: var(--select-cyan); }
```

`.node[data-completed]`（白枠）より前に置き、completed優先を維持する（deriveNodeCardStateはcompletedとreadyを排他にするため実際は同時に立たない）。

- [ ] **Step 3: e2eで4状態のdata属性を検証する**

`research.spec.ts` へ追加:

```ts
import { itemLackingNodeGuid } from "../../mock-host/researchFixtures";

test("ノードカードが4状態のdata属性で描き分けられる", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const completed = page.getByTestId("research-node-11111111-1111-4111-8111-111111111111");
  const locked = page.getByTestId("research-node-22222222-2222-4222-8222-222222222222");
  const ready = page.getByTestId(`research-node-${researchableNodeGuid}`);
  const lacking = page.getByTestId(`research-node-${itemLackingNodeGuid}`);
  await expect(completed).toHaveAttribute("data-completed", "");
  await expect(locked).toHaveAttribute("data-locked", "");
  await expect(ready).toHaveAttribute("data-researchable", "");
  await expect(lacking).not.toHaveAttribute("data-researchable", "");
  await expect(lacking).not.toHaveAttribute("data-locked", "");
});
```

Run: `npx playwright test --config e2e/playwright.config.ts tests/research`
Expected: PASS（他の研究spec含む）

- [ ] **Step 4: lint・vitest全体を通してコミットする**

```bash
npm run lint && npm run test
git add moorestech_web/webui/src/features/research moorestech_web/webui/e2e/tests/research
git commit -m "feat: 研究ノードカードを枠色4状態で描き分ける"
```

---

### Task 7: ステージ全域レイアウトと重畳層

**Files:**
- Modify: `moorestech_web/webui/src/features/research/style.module.css:7-13`
- Modify: `moorestech_web/webui/src/app/tokens.css:66付近`（--z-overlay-panel の隣）
- Modify: `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx:26付近`（GamePanelのstyle）
- Modify: `moorestech_web/webui/src/features/research/ResearchScreenChrome.module.css:19`
- Modify: `moorestech_web/webui/src/app/App.module.css:52-59`（`.viewportOverlay`）
- Test: `moorestech_web/webui/e2e/tests/research/research.spec.ts`

**Interfaces:**
- Consumes: なし（CSSのみ）
- Produces: トークン `--z-overlay-panel-chrome: 31`（全域パネルの上に出す持ち物パネル・キーヒント用）

- [ ] **Step 1: e2eでレイアウトの失敗するテストを書く**

```ts
test("研究パネルはステージ全域を占有し持ち物とキーヒントが上に重なる", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  const tree = page.getByTestId("research-tree");
  const stageBox = await page.locator(".stage, [class*='stage']").first().boundingBox();
  const treeBox = await tree.boundingBox();
  // stage全域一致（一様スケール後の実px。誤差1px許容）
  // Full-stage match in post-scale pixels with 1px tolerance
  expect(Math.abs(treeBox!.x - stageBox!.x)).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.y - stageBox!.y)).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.width - stageBox!.width)).toBeLessThan(1.5);
  expect(Math.abs(treeBox!.height - stageBox!.height)).toBeLessThan(1.5);
  // 持ち物パネルとキーヒントは可視のまま（重畳・裁定2026-08-18）
  // Inventory panel and key hints stay visible on top (adjudicated 2026-08-18)
  await expect(page.getByTestId("main-grid")).toBeVisible();
  await expect(page.getByTestId("research-key-hints")).toBeVisible();
  // 持ち物グリッドがクリックを受ける（最前面確認。trialは重なり判定のみ行う）
  // The inventory grid receives clicks (front-most check; trial only verifies hit-testing)
  await page.getByTestId("main-grid").locator(":scope > *").first().click({ trial: true });
});
```

注: `.stage` のセレクタはCSS Modulesでハッシュ化されるため、`page.locator('[class*="stage"]')` が拾えない場合は `page.getByTestId("research-tree")` の親要素チェーンから `data-web-ui-transparent` を持つstage要素を特定する（`App.tsx:86` 参照）。
Run: 上記テスト → Expected: FAIL（現状は128px下・持ち物列を除く矩形）

- [ ] **Step 2: researchArea をstage絶対配置の全域へ切り替える**

`style.module.css` の `.researchArea` を置換:

```css
/* 研究エリア: ステージ全域を占有する（ADR 0014・裁定2026-08-18）。持ち物・HUD・キーヒントは上層に重畳 */
/* Research area occupies the full stage (ADR 0014); inventory, HUD and key hints overlay above it */
.researchArea {
  position: absolute;
  inset: 0;
  z-index: var(--z-overlay-panel);
  min-width: 0;
}
```

（`grid-column` / `grid-row` / `height: var(--menu-content-height)` は削除。冒頭の旧コメント2組も新実態へ書き換える）

- [ ] **Step 3: 重畳層トークンを追加し、持ち物パネルとキーヒントへ適用する**

`tokens.css` の `--z-overlay-panel: 30;` の直後へ:

```css
  /* 全域パネル（研究）の上へ重畳する常設面: 持ち物パネル・画面キーヒント */
  /* Chrome overlaid above full-stage panels: the inventory panel and screen key hints */
  --z-overlay-panel-chrome: 31;
```

`InventoryPanel/index.tsx` のGamePanel styleへ `position: "relative", zIndex: "var(--z-overlay-panel-chrome)"` を追加。
`ResearchScreenChrome.module.css` の `.keyHints` の `z-index: 10;` を `z-index: var(--z-overlay-panel-chrome);` へ変更（ハードコード解消）。
`App.module.css` の `.viewportOverlay` へ `z-index: var(--z-overlay-panel-chrome);` を追加する（チャレンジHUD等の画面端HUDはこのオーバーレイの子。z指定なしだとz30の全域研究パネルに隠れる。オーバーレイは`pointer-events: none`なので入力への影響は無く、研究画面と共存しないスキット・配置HUDの層関係は実質不変）。

- [ ] **Step 4: e2eを実行して通ることを確認する**

Run: `npx playwright test --config e2e/playwright.config.ts tests/research`
Expected: PASS（`researchViewport.spec.ts` のパン・ズーム系も緑を確認。矩形前提のspecがあれば新レイアウトへ期待値を更新する）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/features/research moorestech_web/webui/src/app/tokens.css moorestech_web/webui/src/features/inventory moorestech_web/webui/e2e/tests/research
git commit -m "feat: 研究パネルをステージ全域占有にし持ち物とヒントを上層へ重畳する"
```

---

### Task 8: 詳細ペインの種類別解放セクションと所持/必要ツールチップ

**Files:**
- Create: `moorestech_web/webui/src/features/research/UnlockSections.tsx`
- Modify: `moorestech_web/webui/src/features/research/ResearchDetailPane.tsx`
- Modify: `moorestech_web/webui/src/features/research/style.module.css`（セクションラベル・テキスト行のスタイル追加）
- Test: `moorestech_web/webui/e2e/tests/research/research.spec.ts`

**Interfaces:**
- Consumes: Task 3の `ResearchNodeData` 新4フィールド、Task 4の `L.ui.research.*Label` 6キー、`BlockSlot`（`src/shared/ui`、props: `blockId:number, name?:string`）、`blockNameKey / connectToolNameKey / trainCarNameKey`（`src/shared/i18n/generated/contentKeys.ts`）、`useItemNameResolver`（`import { useItemNameResolver } from "@/shared/i18n"`。前例: `CraftRecipeView.tsx:12`）、`L.ui.recipe.materialTooltip` / `L.ui.common.itemFallback`
- Produces: `UnlockSections`（props: `{ node: ResearchNodeData }`）。詳細ペイン内の表示専用コンポーネント

- [ ] **Step 1: UnlockSections.tsx を新規作成する**

```tsx
import type { ResearchNodeData } from "@/bridge";
import { ItemSlot, BlockSlot } from "@/shared/ui";
import {
  L,
  blockNameKey,
  connectToolNameKey,
  trainCarNameKey,
  useI18n,
} from "@/shared/i18n";
import styles from "./style.module.css";

type Props = { node: ResearchNodeData };

// 解放物の種類別ラベル付きセクション（ADR 0014）。空の種類は出さない
// Labeled unlock sections per kind (ADR 0014); empty kinds render nothing
export default function UnlockSections({ node }: Props) {
  const { t } = useI18n();
  const otherNames = [
    ...node.unlockConnectToolGuids.map((guid) => t(connectToolNameKey(guid))),
    ...node.unlockTrainCarGuids.map((guid) => t(trainCarNameKey(guid))),
  ];
  return (
    <>
      {node.unlockBlocks.length > 0 && (
        <div data-testid="research-unlock-blocks">
          <span className={styles.sectionLabel}>{t(L.ui.research.unlockBlocksLabel)}</span>
          <div className={styles.detailSlots}>
            {node.unlockBlocks.map((b, i) => (
              <BlockSlot key={`ub-${b.blockId}-${i}`} blockId={b.blockId} name={t(blockNameKey(b.blockGuid))} />
            ))}
          </div>
        </div>
      )}
      {node.unlockMachineRecipeOutputItemIds.length > 0 && (
        <div data-testid="research-unlock-machine-recipes">
          <span className={styles.sectionLabel}>{t(L.ui.research.unlockMachineRecipesLabel)}</span>
          <div className={styles.detailSlots}>
            {node.unlockMachineRecipeOutputItemIds.map((id, i) => <ItemSlot key={`um-${id}-${i}`} itemId={id} />)}
          </div>
        </div>
      )}
      {node.unlockItemIds.length > 0 && (
        <div data-testid="research-unlock-craft-recipes">
          <span className={styles.sectionLabel}>{t(L.ui.research.unlockCraftRecipesLabel)}</span>
          <div className={styles.detailSlots}>
            {node.unlockItemIds.map((id, i) => <ItemSlot key={`uc-${id}-${i}`} itemId={id} />)}
          </div>
        </div>
      )}
      {node.rewardItems.length > 0 && (
        <div data-testid="research-reward-items">
          <span className={styles.sectionLabel}>{t(L.ui.research.rewardItemsLabel)}</span>
          <div className={styles.detailSlots}>
            {node.rewardItems.map((r, i) => <ItemSlot key={`rw-${r.itemId}-${i}`} itemId={r.itemId} count={r.count} />)}
          </div>
        </div>
      )}
      {otherNames.length > 0 && (
        <div data-testid="research-unlock-others">
          <span className={styles.sectionLabel}>{t(L.ui.research.unlockOthersLabel)}</span>
          {otherNames.map((name, i) => <p key={`ot-${i}`} className={styles.unlockOtherName}>{name}</p>)}
        </div>
      )}
    </>
  );
}
```

- [ ] **Step 2: ResearchDetailPane を書き換える（消費ラベル+ツールチップ、混載行の廃止）**

消費アイテムブロック（35-42行）を置換:

```tsx
          {node.consumeItems.length > 0 && (
            <div data-testid="research-consume-items">
              <span className={styles.sectionLabel}>{t(L.ui.research.consumeItemsLabel)}</span>
              <div className={styles.detailSlots}>
                {node.consumeItems.map((c, i) => (
                  <ItemSlot key={`consume-${c.itemId}-${i}`} itemId={c.itemId} count={c.count}
                    insufficient={!isItemSufficient(node, c.itemId, c.count, owned) && node.state !== "completed"}
                    tooltip={<span style={{ whiteSpace: "pre-line" }}>{t(L.ui.recipe.materialTooltip, {
                      itemName: resolveItemName(c.itemId) ?? t(L.ui.common.itemFallback, { itemId: c.itemId }),
                      ownedCount: owned.get(c.itemId) ?? 0,
                      requiredCount: c.count,
                    })}</span>}
                  />
                ))}
              </div>
            </div>
          )}
```

旧43-52行（rewardItems+unlockItemIdsの無札混載行）を `<UnlockSections node={node} />` へ置換。コンポーネント先頭で `const resolveItemName = useItemNameResolver();` を追加し、importへ `useItemNameResolver` を足す（`from "@/shared/i18n"`）。

- [ ] **Step 3: スタイルを追加する**

`style.module.css` 末尾へ:

```css
/* 種類別セクションの従属ラベルとその他解放物の名前行 */
/* Muted per-kind section labels and name rows for other unlocks */
.sectionLabel {
  display: block;
  margin-bottom: 4px;
  font-size: 12px;
  color: var(--text-muted);
}
.unlockOtherName {
  margin: 0;
  font-size: 13px;
  color: var(--text-default);
}
```

- [ ] **Step 4: e2eでセクション表示を検証する**

`research.spec.ts` へ追加:

```ts
test("詳細ペインに解放物が種類別ラベル付きで並ぶ", async ({ page }) => {
  await setUiState(page, "ResearchTree");
  await page.goto("/");
  await page.getByTestId(`research-node-${researchableNodeGuid}`).click();
  const pane = page.getByTestId("research-detail-pane");
  await expect(pane.getByTestId("research-consume-items")).toBeVisible();
  await expect(pane.getByTestId("research-unlock-blocks")).toBeVisible();
  await expect(pane.getByTestId("research-unlock-machine-recipes")).toBeVisible();
  await expect(pane.getByTestId("research-reward-items")).toBeVisible();
  await expect(pane.getByTestId("research-unlock-others")).toBeVisible();
  // 空種類のセクションは出ない（ノード3はunlockItemIdsが空）
  // Empty kinds render nothing (node 3 has no unlockItemIds)
  await expect(pane.getByTestId("research-unlock-craft-recipes")).toHaveCount(0);
});
```

注: mock-hostのローカライズ辞書（`e2e/mock-host/localization/`）に `block.*.name` 等の導出キーが無い場合、名前テキスト行は原文フォールバック表示になる。テストはラベルのtestId可視のみを検証し文言比較はしない（既定ロケール問題・bd moorestech-2lh.1の轍を踏まない）。
Run: `npx playwright test --config e2e/playwright.config.ts tests/research`
Expected: PASS（既存「研究報酬itemの個数」specはセクション化後のDOMでも`getByText("2")`が通ることを確認、壊れたら期待値をセクション構造へ更新）

- [ ] **Step 5: lint・200行制約を確認しコミットする**

Run: `npm run lint && npm run test`（`ResearchDetailPane.tsx`と`UnlockSections.tsx`が各200行以下であること）

```bash
git add moorestech_web/webui/src/features/research moorestech_web/webui/e2e/tests/research
git commit -m "feat: 研究詳細ペインを種類別解放セクションと所持数ツールチップにする"
```

---

### Task 9: 目視QAとUnity側総仕上げ

**Files:**
- Create（撮影スクリプト。既存 `moorestech_web/webui/e2e/capture-eval.ts` の様式を踏襲）: `moorestech_web/webui/e2e/capture-research-qa.ts`
- 検証のみ: Unityコンパイル・関連EditModeテスト全体

**Interfaces:**
- Consumes: Task 2-8の全成果
- Produces: QAスクリーンショット（`moorestech_web/webui/e2e/` 配下の出力先は capture-eval.ts と同じ場所）

- [ ] **Step 1: capture-eval.ts を読み、研究画面QA撮影スクリプトを同様式で作る**

mock-hostを起動し `ResearchTree` へ遷移、(a)全景（4状態ノードが写る位置）、(b)研究可能ノード選択時の詳細ペイン、(c)アイテム不足ノード選択時、の3枚を撮影する。

- [ ] **Step 2: webui-design §10のチェック項目で目視確認する**

1. 端: パネル面がstage四辺まで届き、内容がフェード帯にはみ出て見えないか（4辺の拡大クロップ）
2. 重なり: 持ち物パネル・チャレンジHUD・キーヒントが研究パネルの上に正しく重畳しているか
3. 区別: 4状態の枠色が判別できるか / 詳細ペインのセクションに無札並置が残っていないか
4. 1280×720に加え2432×786等の横長viewportでも撮影し、全域パネルの左右端を確認

問題があれば該当タスクの実装へ戻って修正し、修正コミットを積む。

- [ ] **Step 3: Unity側の総仕上げ検証**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract" --test-mode EditMode
```

Expected: errors 0 / 全PASS

- [ ] **Step 4: 撮影スクリプトをコミットする**

```bash
git add moorestech_web/webui/e2e/capture-research-qa.ts
git commit -m "test: 研究UI目視QAの撮影スクリプトを追加する"
```

---

### Task 10: 全ブランチレビュー（必須・省略不可）

- [ ] **必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。** moorestechのプロジェクト固有レビュースキル **moores-code-review** を使い、`origin/master..HEAD` の全変更を対象に1パス実行する。指摘の機械的修正は適用してコミットし、設計判断はAskUserQuestionでまとめて仰ぐ。
- [ ] レビュー完了後、未コミットの変更が無いことを確認する（`git status --short` が空）。

---

## 判断記録（ADR）

設計セッションのADR: `docs/adr/0014-research-ui-four-states-fullstage-unlock-sections.md`（4裁定+agent前提4点の出所つき本文）。関連裁定: `.decisions/2026-08-18-研究*.md` 4件。

planning中に新たに生じた判断:

- **DTOの解放物表現**: unlockBlockは `{blockId, blockGuid}` のペア（アイコンはid・名前はGuid導出キー`blockNameKey`）、機械レシピは出力アイテムidへ畳む（レシピguidは送らない）、connect tool / train car はGuidのみ送りWeb側で名前導出キー解決。出所: agent前提（既存 `contentKeys.ts` の導出キー基盤と§8.7代表出力アイテム前例。表示に必要な最小データのみ通す）
- **持ち物パネル・キーヒント・画面端HUDの重畳はzトークン新設で解く**: `--z-overlay-panel-chrome: 31` を `tokens.css` へ追加し、InventoryPanel・ResearchScreenChrome・`.viewportOverlay`（チャレンジHUD等の親）へ適用。DOM順依存の暗黙スタッキングは使わない。キーヒントの既存 `z-index: 10` ハードコードも同トークンへ是正。出所: agent前提（webui-design「z層は--z-*トークンのみ」規約。viewportOverlayはz未指定だとz30の全域パネルに隠れるため）
- **`deriveNodeCardState`は破壊的にシグネチャ変更**: 旧`(state)`版を残さず全呼び出し側（1箇所）を更新。出所: agent前提（後方互換不要の設計原則）
- **`findInitialFocusNode`は現状維持**（サーバーstateベース。ライブ再計算は初期フォーカスへ波及させない）。出所: agent前提（要件外・YAGNI）
- **e2eの文言比較はしない**: セクション検証はtestIdの可視のみ。出所: agent前提（既定ロケールjapaneseで英語literal期待が恒常失敗する既知問題 bd moorestech-2lh.1 の再発防止）
- **mock-hostへ第4ノード（アイテム不足）を追加**して4状態をe2e検証可能にする。出所: agent前提（テスト充足性）
