# 初期チュートリアル提示強化（枠線ラベル・keyControl復活・アンカー語彙拡張・石の斧モデル） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ADR 0022 のとおり、枠線ハイライトに文言ラベルを描き、keyControl をキーキャップ付きHUDヒントとしてWebで復活させ、アンカー語彙にインベントリ所持スロット/装備スロットを足し、石の斧の手持ちモデルをAddressable登録する。本体は3PR（A/B/C）、マスタは3本マージ後に1PR。

**Architecture:** 提示は既存の `TutorialPresentationStateStore`（Unity）→ `tutorial.presentation` topic → `TutorialOverlay`（Web）の一本道を拡張する。outline要素に `labelTutorialGuid` を足し、新kind `keyControl` を足す。文言はいずれも `challengeTutorial.<tutorialGuid>.text` をWebが解決する（WorldPin前例）。uiState一致判定・anchor解決・skit中非表示はすべてWeb側。アンカー語彙は `anchorIds.ts` 単一ソースにprefix/静的IDを足し、`data-tutorial-anchor` を空白区切りトークン列にして1要素複数アンカーを許す。

**Tech Stack:** Unity C#（VContainer, UniRx, NUnit）、React + TypeScript（Mantine, vitest, Playwright）、Mooresmaster SourceGenerator（VanillaSchema YAML）、Unity Addressables。

## Requirements

- R1 枠線ハイライト（uiHighLight / itemViewHighLight）に文言ラベルを描く。文言はマスタ `highLightText` 由来（`challengeTutorial.<tutorialGuid>.text` をWeb解決）。`highLightText` が空なら枠線のみ。受け入れ: 石器を作るで `recipe.item-<石器id>` の枠線脇に「①石器を選択」等が出る（マスタ追記後）。
- R2 keyControl をWebで表示。kind `keyControl {elementId, tutorialGuid, keyName, uiState}`。画面下中央（ホットバーの上）に `[Tab] インベントリを開く` 形式（`<kbd>` ＋文言）。`ui_state.current` の `state` が `uiState` と一致する間だけ表示。blockingスキット中は非表示。同時複数は縦積み。
- R3 schema `challenges.yml` の keyControl に `keyName: string`（必須・default "Tab"）を追加し、`uiState` enum を `GameScreen, PlayerInventory, SubInventory, PauseMenu, DeleteBar, Story, PlaceBlock, ChallengeList, ResearchTree, TrainHUDScreen, BuildMenu` にする。`optional` や `?? Default` で吸収しない。
- R4 アンカー語彙に `inventory.item-<itemGuid>`（メインインベントリで該当アイテムを持つ先頭スロット。guid→itemIdはWeb側でitem masterから解決）、`equipment.slot-<index>`、`equipment.selected-slot` を追加。フィクスチャ `tutorial_anchor_ids.json` と Unity/Web の双方テストが一致。
- R5 同一要素に複数アンカーを付けられる（`data-tutorial-anchor` 空白区切り・`~=` 解決）。既存の単一指定は無変更で動く。
- R6 石の斧の手持ちモデル: `AddressableResources/Item/StoneAxe.prefab`（手持ちオフセット焼き込み）を `Vanilla/Item/StoneAxe` で Vanilla Asset Group に登録。PlayModeスクリーンショットで見た目確認。
- R7 マスタ追記（3本マージ後・moorestech_master PR）: 小石HUDラベル文言「左上で現在の目標を確認する」、石器を作るの「①石器を選択」「②クラフトボタンを長押し」＋keyControl(Tab/GameScreen)、木を伐採の keyControl(Tab/GameScreen)＋`uiDragGuide{inventory.item-<石器guid> → equipment.selected-slot}`、木の板の keyControl(Tab/GameScreen)、原始研究1の keyControl(R/GameScreen, R/PlayerInventory)、石の斧 `handGrabModel=Vanilla/Item/StoneAxe`、mod_3 の keyControl に keyName、localization.csv 追記、本体 `.moorestech-external-revisions.json` のピン更新。
- やらないこと: 新チャレンジ・新taskCompletionType（equipItem）は作らない。uGUI(TMP)のkeyControl描画は復活させない（`KeyControlDescription` の既存 `SetText` 利用は触らない）。anchorIdの検証・変換はUnity側に追加しない（2026-08-19裁定）。

## Global Constraints

- AGENTS.md 規約: partial禁止・1ファイル200行以下・1ディレクトリ10ファイル・UniRx・`Func<>`禁止・try-catch原則禁止・デフォルト引数禁止・2行セットコメント（日本語→英語）・`#region Internal` はメソッド内ローカル関数のみ・.cs変更後は必ず `uloop compile`。
- Web: 表示文字列は `t()` 経由（JSX生リテラルはlintで落ちる）。色・z-index・寸法は `src/app/tokens.css` のトークン。`webui-design` §7（`<kbd>`様式）・§8.17（tutorial overlay内にz層を増やさない）に従う。
- 作業場所: メインワークツリーでの作業禁止。各PRは `moores-wt new <branch>` で使い捨てworktreeを切る（Library コピー・Editor 起動込み）。終了後 `moores-wt rm`。
- テスト実行: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<regex>"`。Web: `cd moorestech_web/webui && npx vitest run <path>`、`npm run lint`、`npx tsc -b`。
- マスタ側の変更はすべて `../moorestech_master` の別PR（最後）に回す。本体PRのテストは TestMod（`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest`）のJSONで完結させる。
- コミットは小さく頻繁に。各コミット末尾に `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`。

---

# PR-A: 枠線ラベル＋keyControl復活（ブランチ `feature/tutorial-label-and-keycontrol`）

## File Structure（PR-A）

- Modify `VanillaSchema/challenges.yml` … keyControl に `keyName`、uiState enum 更新
- Modify `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` … SourceGenerator 再生成トリガ
- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/TutorialPresentationData.cs` … `TutorialOutlineElementData.LabelTutorialGuid`、新 `TutorialKeyControlElementData`
- Modify `.../Tutorial/Presentation/TutorialPresentationStateStore.cs` … `AddOutlineHighlight(anchorId, labelTutorialGuid)`、`AddKeyControlHint(tutorialGuid, keyName, uiState)`
- Modify `.../Tutorial/UIHighlight/UIHighlightTutorialManager.cs`、`ItemViewHighLightTutorialManager.cs` … ラベルguidを渡す
- Modify `.../Tutorial/KeyControlTutorialManager.cs` … store発行のみの薄いManagerへ書き換え（TMP/UIStateControl依存を外す）
- Modify `.../Tutorial/TutorialManager.cs` … `ClearPresentation` 呼び出しとフィールド削除
- Modify `moorestech_client/Assets/Scripts/Client.Tests/WebUi/TutorialPresentationStateStoreTest.cs` … 新API のテスト
- Create `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/KeyControlTutorialManagerTest.cs`
- Modify `moorestech_web/webui/src/bridge/contract/schemas/presentation.ts`（＋`presentation.test.ts`）… `labelTutorialGuid` / `TutorialKeyControlSchema`
- Modify `moorestech_web/webui/src/features/tutorial/TutorialOverlay.tsx`（＋`.test.ts`、`style.module.css`）… ラベル描画
- Create `moorestech_web/webui/src/features/tutorial/KeyControlHintHud.tsx`（＋`KeyControlHintHud.test.ts`、`keyControlHint.module.css`）
- Modify `moorestech_web/webui/src/features/tutorial/index.ts`、`src/app/App.tsx`、`src/app/tokens.css`
- Modify `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`、`e2e/tests/system/tutorial.spec.ts`

### Task A1: schema — keyControl に keyName を追加し uiState enum を実体に揃える

**Files:**
- Modify: `VanillaSchema/challenges.yml:171-187`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

**Interfaces:**
- Produces: 生成型 `Mooresmaster.Model.ChallengesModule.KeyControlTutorialParam` に `string KeyName` プロパティが追加される（`UiState`・`ControlText` は既存）。

- [x] **Step 1: スキーマを編集する**

`VanillaSchema/challenges.yml` の keyControl case を次に置き換える（`uiState` の enum は `Client.Game/InGame/UI/UIState/UIStateEnum.cs` から `Debug` を除いた全値）:

```yaml
              - when: keyControl
                type: object
                properties:
                - key: uiState
                  type: enum
                  options:
                  - GameScreen
                  - PlayerInventory
                  - SubInventory
                  - PauseMenu
                  - DeleteBar
                  - Story
                  - PlaceBlock
                  - ChallengeList
                  - ResearchTree
                  - TrainHUDScreen
                  - BuildMenu
                  default: GameScreen
                - key: keyName
                  type: string
                  default: Tab
                - key: controlText
                  type: string
                  default: control text
```

- [x] **Step 2: SourceGenerator を再トリガする**

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` 定数の値を別の文字列（例: 現在値の末尾に `-keyName` を付けたもの）へ変更する。

- [x] **Step 3: 旧enum値・データ残存を確認する**

Run: `grep -rn '"BlockInventory"' --include='*.json' moorestech_server moorestech_client ../moorestech_master | grep -v worktrees`
Expected: 0件（keyControl データは TestMod / EditModeInPlayingTestMod に存在せず、v8 origin/master にも0件。mod_3 の1件はマスタPR側で keyName を足す）。

- [x] **Step 4: コンパイルして生成物を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0。`grep -rn "KeyName" Library/` ではなく、次のテスト（Task A2）で `KeyControlTutorialParam.KeyName` を参照してコンパイルが通ることで確認する。

- [x] **Step 5: コミットする**

```bash
git add VanillaSchema/challenges.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat(schema): keyControl に keyName を追加し uiState enum を UIStateEnum に揃える

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task A2: Unity presentation data/store — outline のラベルguid と keyControl 要素

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/TutorialPresentationData.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/TutorialPresentationStateStore.cs:47-70`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/UIHighlightTutorialManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/ItemViewHighLightTutorialManager.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/TutorialPresentationStateStoreTest.cs`

**Interfaces:**
- Produces:
  - `TutorialOutlineElementData.LabelTutorialGuid : string`（null＝ラベル無し。`WebUiJson` は `NullValueHandling.Ignore` なので JSON からキーが消える）
  - `TutorialKeyControlElementData { const KindName="keyControl"; string TutorialGuid; string KeyName; string UiState; }`
  - `ITutorialView TutorialPresentationStateStore.AddOutlineHighlight(string anchorId, string labelTutorialGuid)`
  - `ITutorialView TutorialPresentationStateStore.AddKeyControlHint(string tutorialGuid, string keyName, string uiState)`

- [x] **Step 1: 失敗するテストを書く**

`TutorialPresentationStateStoreTest.cs` に追加:

```csharp
        // 文言付きの枠線はラベル用tutorialGuidを載せ、文言無しはnullで枠線のみを表す
        // Outlines with text carry the label tutorialGuid; outlines without text carry null for outline-only
        [Test]
        public void AddOutlineHighlightCarriesLabelTutorialGuid()
        {
            var store = new TutorialPresentationStateStore();
            store.BeginSession(Guid.NewGuid());

            store.AddOutlineHighlight("recipe.craft-button", "11111111-1111-4111-8111-111111111111");
            store.AddOutlineHighlight("hotbar.hud", null);

            var elements = store.GetCurrent().Sessions.Single().Elements.Cast<TutorialOutlineElementData>().ToArray();
            Assert.AreEqual("11111111-1111-4111-8111-111111111111", elements[0].LabelTutorialGuid);
            Assert.IsNull(elements[1].LabelTutorialGuid);
        }

        // keyControlはkeyName/uiState/tutorialGuidを持つ独立kindとして公開する
        // keyControl is published as its own kind carrying keyName, uiState and tutorialGuid
        [Test]
        public void AddKeyControlHintPublishesKeyControlKind()
        {
            var store = new TutorialPresentationStateStore();
            var challengeId = Guid.NewGuid();
            store.BeginSession(challengeId);

            var view = store.AddKeyControlHint("22222222-2222-4222-8222-222222222222", "Tab", "GameScreen");

            var element = (TutorialKeyControlElementData)store.GetCurrent().Sessions.Single().Elements.Single();
            Assert.AreEqual(TutorialKeyControlElementData.KindName, element.Kind);
            Assert.AreEqual("22222222-2222-4222-8222-222222222222", element.TutorialGuid);
            Assert.AreEqual("Tab", element.KeyName);
            Assert.AreEqual("GameScreen", element.UiState);

            view.CompleteTutorial();
            Assert.IsEmpty(store.GetCurrent().Sessions.Single().Elements);
        }
```

既存テストの `store.AddOutlineHighlight("recipe.craft-button")` 呼び出しは全て `store.AddOutlineHighlight("recipe.craft-button", null)` に書き換える（デフォルト引数禁止のため第2引数必須）。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `AddOutlineHighlight` の引数不一致・`TutorialKeyControlElementData` 未定義でコンパイルエラー。

- [x] **Step 3: データ型を追加する**

`TutorialPresentationData.cs` の `TutorialOutlineElementData` に追加し、新クラスを末尾に足す:

```csharp
    public class TutorialOutlineElementData : TutorialOverlayElementData
    {
        public const string KindName = "outline";

        public TutorialOutlineElementData()
        {
            Kind = KindName;
        }

        public string AnchorId;
        public int PaddingPx;
        public bool BlocksPointerInput;
        // 枠線脇ラベルの文言キー元。nullなら枠線のみ（JSONではキーごと省略される）
        // Source GUID of the side label text; null means outline only (the key is omitted from JSON)
        public string LabelTutorialGuid;
    }

    // キー操作ヒント。uiState一致・skit中非表示の判定はWeb側が行う
    // Key-control hint; the web side decides uiState matching and hides it during skits
    public class TutorialKeyControlElementData : TutorialOverlayElementData
    {
        public const string KindName = "keyControl";

        public TutorialKeyControlElementData()
        {
            Kind = KindName;
        }

        public string TutorialGuid;
        public string KeyName;
        public string UiState;
    }
```

- [x] **Step 4: store の API を拡張する**

`TutorialPresentationStateStore.cs` の `AddOutlineHighlight` を差し替え、`AddKeyControlHint` を `AddDragGuide` の下に追加:

```csharp
        // outline用途だけを公開し、廃止済みkindの再流入を防ぐ。labelTutorialGuidはnullでラベル無し
        // Expose only the outline use case to prevent removed kinds from returning; null labelTutorialGuid means no label
        public ITutorialView AddOutlineHighlight(string anchorId, string labelTutorialGuid)
        {
            return AddElement(new TutorialOutlineElementData
            {
                ElementId = Guid.NewGuid().ToString(),
                AnchorId = anchorId,
                PaddingPx = 8,
                BlocksPointerInput = false,
                LabelTutorialGuid = labelTutorialGuid,
            });
        }

        // キー操作ヒント。表示可否（uiState一致）はWeb側が判定するので常に載せる
        // Key-control hint; always published since the web side decides visibility by uiState
        public ITutorialView AddKeyControlHint(string tutorialGuid, string keyName, string uiState)
        {
            return AddElement(new TutorialKeyControlElementData
            {
                ElementId = Guid.NewGuid().ToString(),
                TutorialGuid = tutorialGuid,
                KeyName = keyName,
                UiState = uiState,
            });
        }
```

- [x] **Step 5: 呼び出し側（枠線2種）でラベルguidを渡す**

`UIHighlightTutorialManager.cs`:

```csharp
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var highlightParam = (UiHighLightTutorialParam)tutorial.TutorialParam;

            // マスタのanchorIdを無変換でWebオーバーレイへ渡す。文言があるときだけラベル用guidを添える
            // Pass the master anchorId verbatim; attach the label GUID only when the master has text
            var labelTutorialGuid = string.IsNullOrEmpty(highlightParam.HighLightText) ? null : tutorial.TutorialGuid.ToString();
            return TutorialPresentationStateStore.Instance.AddOutlineHighlight(highlightParam.HighLightAnchorId, labelTutorialGuid);
        }
```

`ItemViewHighLightTutorialManager.cs`:

```csharp
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var highlightParam = (ItemViewHighLightTutorialParam)tutorial.TutorialParam;

            // アイテムハイライトもWebオーバーレイのDOMハイライトのみで表示する
            // Item highlighting is rendered exclusively via the web overlay's DOM highlight
            var itemId = MasterHolder.ItemMaster.GetItemId(highlightParam.HighLightItemGuid).AsPrimitive();
            var anchorId = TutorialAnchorIdMapper.FromItemId(itemId);
            var labelTutorialGuid = string.IsNullOrEmpty(highlightParam.HighLightText) ? null : tutorial.TutorialGuid.ToString();
            return TutorialPresentationStateStore.Instance.AddOutlineHighlight(anchorId, labelTutorialGuid);
        }
```

他に `AddOutlineHighlight(` を呼ぶ箇所が無いか確認: `grep -rn "AddOutlineHighlight(" moorestech_client/Assets/Scripts`（テストと上記2件以外に無いこと）。

- [x] **Step 6: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → errors 0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TutorialPresentationStateStoreTest"`
Expected: 全PASS（新規2件含む）。

- [x] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial moorestech_client/Assets/Scripts/Client.Tests/WebUi/TutorialPresentationStateStoreTest.cs
git commit -m "feat(tutorial): 枠線にラベル用tutorialGuidを載せ、keyControl kindをpresentation storeへ追加

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task A3: KeyControlTutorialManager を store 発行のみに書き換える

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/KeyControlTutorialManager.cs`（全面置換）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/TutorialManager.cs:19,30,71-79`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/KeyControlTutorialManagerTest.cs`

**Interfaces:**
- Consumes: `TutorialPresentationStateStore.AddKeyControlHint(string,string,string)`（Task A2）、生成型 `KeyControlTutorialParam.KeyName/UiState`（Task A1）
- Produces: `KeyControlTutorialManager : MonoBehaviour, ITutorialViewManager`（`ITutorialView` 実装と `ClearPresentation()` は削除）

- [x] **Step 1: 失敗するテストを書く**

`KeyControlTutorialManagerTest.cs`（`UiDragGuideTutorialManagerTest.cs` と同型。TestMod の challenges.json 先頭tutorialを keyControl に差し替えて読み込む）:

```csharp
using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.Tutorial;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial
{
    public class KeyControlTutorialManagerTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000001");

        private ChallengeMaster _originalChallengeMaster;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _originalChallengeMaster = MasterHolder.ChallengeMaster;
            SetChallengeMaster(CreateKeyControlChallengeMaster());
            _root = new GameObject("KeyControlTutorialManagerTest");

            #region Internal

            ChallengeMaster CreateKeyControlChallengeMaster()
            {
                var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                    "mods", "forUnitTest", "master", "challenges.json");
                var json = JObject.Parse(File.ReadAllText(path));
                var tutorials = (JArray)json["data"][0]["challenges"][0]["tutorials"];
                var tutorial = (JObject)tutorials[0].DeepClone();
                tutorials.Clear();
                tutorials.Add(tutorial);
                tutorial["tutorialType"] = "keyControl";
                tutorial["tutorialParam"] = new JObject
                {
                    ["uiState"] = "PlayerInventory",
                    ["keyName"] = "R",
                    ["controlText"] = "研究画面を開く",
                };
                var master = new ChallengeMaster(json);
                master.Initialize();
                return master;
            }

            #endregion
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            SetChallengeMaster(_originalChallengeMaster);
        }

        // keyName/uiState/tutorialGuidがそのままkeyControl要素として公開され、完了で撤去される
        // keyName/uiState/tutorialGuid are published verbatim as a keyControl element and removed on completion
        [Test]
        public void ApplyTutorialはkeyControl要素を公開し完了で撤去する()
        {
            var manager = _root.AddComponent<KeyControlTutorialManager>();
            var tutorial = MasterHolder.ChallengeMaster.GetChallenge(ChallengeGuid).Tutorials[0];
            var countBefore = KeyControls().Length;

            var view = manager.ApplyTutorial(tutorial);

            var hints = KeyControls();
            Assert.AreEqual(countBefore + 1, hints.Length);
            var hint = hints[hints.Length - 1];
            Assert.AreEqual(tutorial.TutorialGuid.ToString(), hint.TutorialGuid);
            Assert.AreEqual("R", hint.KeyName);
            Assert.AreEqual("PlayerInventory", hint.UiState);

            view.CompleteTutorial();
            Assert.AreEqual(countBefore, KeyControls().Length);

            #region Internal

            TutorialKeyControlElementData[] KeyControls()
            {
                return TutorialPresentationStateStore.Instance.GetCurrent().Sessions
                    .SelectMany(session => session.Elements)
                    .OfType<TutorialKeyControlElementData>().ToArray();
            }

            #endregion
        }

        private static void SetChallengeMaster(ChallengeMaster challengeMaster)
        {
            typeof(MasterHolder).GetProperty(nameof(MasterHolder.ChallengeMaster))
                .GetSetMethod(true).Invoke(null, new object[] { challengeMaster });
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 既存 `KeyControlTutorialManager.ApplyTutorial` は `this` を返す旧実装なので、テスト自体はコンパイルは通る。次に Step 4 の実行で失敗（`KeyControls()` が増えない）を確認する。
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "KeyControlTutorialManagerTest"`
Expected: FAIL（要素が公開されない）。

- [x] **Step 3: KeyControlTutorialManager を書き換える**

全面置換:

```csharp
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    // キー操作ヒントはWebの下中央HUDが描く。uiState一致の判定もWeb側なので、ここはマスタ値をそのまま公開するだけ
    // Key-control hints are drawn by the web's bottom-center HUD; uiState matching is web-side too, so this only publishes master values
    public class KeyControlTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (KeyControlTutorialParam)tutorial.TutorialParam;
            return TutorialPresentationStateStore.Instance.AddKeyControlHint(
                tutorial.TutorialGuid.ToString(), param.KeyName, param.UiState);
        }
    }
}
```

`TutorialManager.cs`: フィールド `_keyControlTutorialManager` とその代入（19行・30行）、`CompleteChallenge` 内の `_keyControlTutorialManager.ClearPresentation();`（77行）を削除する。ctor 引数 `keyControlTutorialManager` は残し、`_tutorialViewManagers.Add(TutorialsElement.TutorialTypeConst.keyControl, keyControlTutorialManager);` だけにする。`CompleteChallenge` の該当部は次になる:

```csharp
            if (WebUiScreenGate.IsWebUiMode)
            {
                var presentationStore = TutorialPresentationStateStore.Instance;
                if (presentationStore.HasSession(challengeId)) presentationStore.EndSession(challengeId);
            }
```

`KeyControlDescription.Instance.SetOverrideText/ClearOverrideText` の呼び出し元が他に無いことを確認する（`grep -rn "OverrideText" moorestech_client/Assets/Scripts`）。無ければ `KeyControlDescription.cs` の `SetOverrideText`/`ClearOverrideText`/`_overrideText` を削除し `RefreshText` を `_defaultText` のみにする（デバッグ/未使用publicを残さない規約）。

- [x] **Step 4: prefab の旧SerializeField参照を確認する**

`KeyControlTutorialManager` から `keyControlUIObject` / `keyControlTutorialText` が消える。シーン/プレハブ側の参照はUnityが無視するが、Console に「missing serialized field」系の警告が出ないことを `uloop get-logs --project-path ./moorestech_client --log-type Warning` で確認する。出る場合は `uloop execute-dynamic-code` で該当コンポーネントの SerializedObject を更新して保存する（テキスト編集禁止）。

- [x] **Step 5: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → errors 0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "KeyControlTutorialManagerTest|TutorialPresentationStateStoreTest|UiDragGuideTutorialManagerTest|VeinPinTutorialTest"`
Expected: 全PASS。

- [x] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial moorestech_client/Assets/Scripts/Client.Game/InGame/UI/KeyControl/KeyControlDescription.cs moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/KeyControlTutorialManagerTest.cs*
git commit -m "feat(tutorial): keyControlをpresentation store発行へ書き換え、uGUI依存とClearPresentationを撤去

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task A4: Web wire schema — outline.labelTutorialGuid と keyControl kind

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/presentation.ts:4-19`
- Test: `moorestech_web/webui/src/bridge/contract/schemas/presentation.test.ts`

**Interfaces:**
- Produces: `TutorialHighlightSchema` に `labelTutorialGuid: z.string().uuid().optional()`；新 `TutorialKeyControlSchema { kind:"keyControl", elementId, tutorialGuid(uuid), keyName, uiState }`；`TutorialOverlayElementSchema` の union に追加。型 `TutorialPresentationData` は既存の `z.infer` 経由で自動更新。

- [ ] **Step 1: 失敗するテストを書く**

`presentation.test.ts` に追加:

```ts
  it("accepts an outline with and without a label tutorial guid", () => {
    const base = { kind: "outline", elementId: "h1", anchorId: "recipe.craft-button", paddingPx: 8, blocksPointerInput: false };
    expect(TutorialHighlightSchema.safeParse(base).success).toBe(true);
    expect(TutorialHighlightSchema.safeParse({ ...base, labelTutorialGuid: "11111111-1111-4111-8111-111111111111" }).success).toBe(true);
    expect(TutorialHighlightSchema.safeParse({ ...base, labelTutorialGuid: "" }).success).toBe(false);
  });

  it("accepts a keyControl hint and rejects unknown keys", () => {
    const hint = { kind: "keyControl", elementId: "k1", tutorialGuid: "22222222-2222-4222-8222-222222222222", keyName: "Tab", uiState: "GameScreen" };
    expect(TutorialOverlayElementSchema.safeParse(hint).success).toBe(true);
    expect(TutorialOverlayElementSchema.safeParse({ ...hint, text: "x" }).success).toBe(false);
  });
```

`import` に `TutorialHighlightSchema, TutorialOverlayElementSchema` を足す。

- [ ] **Step 2: 実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/bridge/contract/schemas/presentation.test.ts`
Expected: FAIL（`labelTutorialGuid` は strict で拒否、`keyControl` は union 外）。

- [ ] **Step 3: スキーマを実装する**

```ts
// 枠線は矩形だけ。文言を持つなら labelTutorialGuid で辞書キーを示し、Web側が t() で解決して脇に描く
// Highlights carry only the outline; when text exists, labelTutorialGuid names the dictionary key the web resolves with t()
export const TutorialHighlightSchema = z.object({
  kind: z.literal("outline"), elementId: z.string(), anchorId: z.string(),
  paddingPx: z.number().nonnegative(), blocksPointerInput: z.boolean(),
  labelTutorialGuid: z.string().uuid().optional(),
}).strict();
// D&D説明の矢印ガイド。from/to両anchorが解決している間だけ描く
// Drag guide arrows for D&D instruction; drawn only while both anchors resolve
export const TutorialDragGuideSchema = z.object({
  kind: z.literal("dragGuide"), elementId: z.string(),
  fromAnchorId: z.string(), toAnchorId: z.string(),
}).strict();
// キー操作ヒント。uiStateが ui_state.current と一致する間だけ下中央HUDに描く
// Key-control hint; drawn in the bottom-center HUD only while uiState matches ui_state.current
export const TutorialKeyControlSchema = z.object({
  kind: z.literal("keyControl"), elementId: z.string(),
  tutorialGuid: z.string().uuid(), keyName: z.string(), uiState: z.string(),
}).strict();
// overlay要素はkind判別unionの単一列。種別追加は配列を増やさずunionへ足す
// Overlay elements form one kind-discriminated union list; new kinds extend the union, not the arrays
export const TutorialOverlayElementSchema = z.discriminatedUnion("kind", [
  TutorialHighlightSchema, TutorialDragGuideSchema, TutorialKeyControlSchema,
]);
```

- [ ] **Step 4: テスト・型チェック**

Run: `npx vitest run src/bridge/contract/schemas/presentation.test.ts && npx tsc -b`
Expected: PASS。`tsc` で `TutorialOverlay.tsx` の `element.kind !== "outline"` 分岐が keyControl に `fromAnchorId` 無しで型エラーになる → Task A5 で直すため、このステップでは `TutorialOverlay.tsx:37-41` を一時的に `if (element.kind === "dragGuide") { ...; continue; } if (element.kind !== "outline") continue;` に直してから進める。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/bridge/contract/schemas/presentation.ts moorestech_web/webui/src/bridge/contract/schemas/presentation.test.ts moorestech_web/webui/src/features/tutorial/TutorialOverlay.tsx
git commit -m "feat(webui): tutorial presentation に labelTutorialGuid と keyControl kind を追加

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task A5: Web — 枠線脇のラベル描画

**Files:**
- Modify: `moorestech_web/webui/src/features/tutorial/TutorialOverlay.tsx:76-92`
- Modify: `moorestech_web/webui/src/features/tutorial/style.module.css`
- Modify: `moorestech_web/webui/src/app/tokens.css`（`--tutorial-*` の近く・186行付近）
- Test: `moorestech_web/webui/src/features/tutorial/TutorialOverlay.test.ts`

**Interfaces:**
- Consumes: `challengeTutorialTextKey(guid)`（`@/shared/i18n`）、`useI18n().t`
- Produces: DOM `div[data-testid="tutorial-highlight-label"]`（枠線 div の兄弟、`.highlightLabel`）

- [ ] **Step 1: 失敗するテストを書く**

`TutorialOverlay.test.ts` の `vi.mock("@/bridge"...)` の下に i18n モックを追加し、テストを足す:

```ts
vi.mock("@/shared/i18n", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/i18n")>();
  return { ...actual, useI18n: () => ({ t: (key: string) => `T:${key}` }) };
});
```

```ts
describe("TutorialOverlay outline labels", () => {
  afterEach(() => { mockState.presentation = null; mockState.listeners.clear(); });

  // labelTutorialGuid付きの枠線だけが文言ラベルを持ち、文言は challengeTutorial.<guid>.text で解決する
  // Only outlines with labelTutorialGuid get a text label, resolved through challengeTutorial.<guid>.text
  it("labelTutorialGuid がある枠線だけラベルを描く", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [
        { ...outline("h1", "recipe.craft-button"), labelTutorialGuid: "11111111-1111-4111-8111-111111111111" },
        outline("h2", "hotbar.hud"),
      ] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("recipe.craft-button", ready(10));
    pushAnchor("hotbar.hud", ready(100));

    const labels = renderer.root.findAllByProps({ "data-testid": "tutorial-highlight-label" });
    expect(labels.length).toBe(1);
    expect(labels[0].children).toEqual(["T:challengeTutorial.11111111-1111-4111-8111-111111111111.text"]);
    // ラベルは枠線の下辺外側（top = rect.bottom + padding）に置く
    // The label sits just below the outline (top = rect.bottom + padding)
    expect(labels[0].props.style.top).toBe(10);
    expect(labels[0].props.style.left).toBe(10);
  });

  it("anchor未解決の枠線はラベルも描かない", () => {
    mockState.presentation = presentation(1, [
      { tutorialSessionId: "s1", challengeId: "c1", elements: [
        { ...outline("h1", "recipe.craft-button"), labelTutorialGuid: "11111111-1111-4111-8111-111111111111" },
      ] },
    ]);
    let renderer!: ReturnType<typeof create>;
    act(() => { renderer = create(createElement(TutorialOverlay)); });
    pushAnchor("recipe.craft-button", hidden);
    expect(renderer.root.findAllByProps({ "data-testid": "tutorial-highlight-label" }).length).toBe(0);
  });
});
```

（`ready()` の rect は `top:0,height:10`、`outline()` の `paddingPx:0` なので `top` 期待値は `0 + 10 + 0 = 10`。）

- [ ] **Step 2: 実行して失敗を確認する**

Run: `npx vitest run src/features/tutorial/TutorialOverlay.test.ts`
Expected: FAIL（ラベル要素が無い）。

- [ ] **Step 3: 描画を実装する**

`TutorialOverlay.tsx`:
- import に `import { challengeTutorialTextKey, useI18n } from "@/shared/i18n";` を追加。
- `TutorialOverlay` 内で `const { t } = useI18n();` を取り、レンダー部を:

```tsx
  if (!presentation) return null;
  return <div className={styles.overlay} data-testid="tutorial-overlay">
    {presentation.sessions.flatMap((session) => session.elements.map((element) => {
      const key = `${session.tutorialSessionId}:${element.elementId}`;
      if (element.kind === "outline") return renderOutline(key, element, resolved[element.anchorId], t);
      if (element.kind === "dragGuide") return renderDragGuide(key, resolved[element.fromAnchorId], resolved[element.toAnchorId]);
      // keyControlはanchorを持たず、下中央HUD(KeyControlHintHud)が描く
      // keyControl has no anchor; the bottom-center HUD (KeyControlHintHud) renders it
      return null;
    }))}
  </div>;
```

- 購読集約ループ（36-46行）は `if (element.kind === "dragGuide") { anchorIds.add(from/to); continue; } if (element.kind !== "outline") continue;` の形にする（Task A4 Step 4 で暫定修正済みなら整える）。
- `renderOutline` を置換:

```tsx
type Translate = ReturnType<typeof useI18n>["t"];

function renderOutline(key: string, element: TutorialOutlineElement, value: ResolvedAnchor | undefined, t: Translate) {
  if (!value || value.status !== "ready") return null;
  const padding = element.paddingPx;
  const left = value.rect.left - padding;
  const outline = <div key={key} className={styles.highlight} data-kind={element.kind}
    style={{ left, top: value.rect.top - padding,
      width: value.rect.width + padding * 2, height: value.rect.height + padding * 2 }} />;
  if (!element.labelTutorialGuid) return outline;
  // ラベルは枠線の下辺外側に左揃えで置き、文言はtutorialGuid導出キーで辞書解決する
  // The label sits left-aligned just below the outline; its text resolves via the tutorialGuid-derived key
  const label = <div key={`${key}:label`} className={styles.highlightLabel} data-testid="tutorial-highlight-label"
    style={{ left, top: value.rect.top + value.rect.height + padding }}>
    {t(challengeTutorialTextKey(element.labelTutorialGuid))}
  </div>;
  return [outline, label];
}
```

`flatMap` は配列戻りを平坦化するので `[outline, label]` で2要素になる。

- `style.module.css` に追加:

```css
/* 枠線脇のラベル。ワールドピンのラベルと同じ面・文字色で、枠線の下辺外側に付く */
/* Side label of an outline; same face and text color as the world-pin label, attached just below the outline */
.highlightLabel {
  position: fixed;
  margin-top: var(--tutorial-highlight-label-gap);
  padding: 4px 10px;
  color: var(--text-high-contrast);
  background: var(--world-pin-face);
  white-space: nowrap;
  font-size: var(--tutorial-highlight-label-font-size);
  pointer-events: none;
}
```

- `tokens.css` の `--tutorial-drag-guide-size` の近くに追加:

```css
  --tutorial-highlight-label-gap: 4px;
  --tutorial-highlight-label-font-size: 14px;
```

`.agents/skills/webui-design/SKILL.md` の「## 8.17 チュートリアルのドラッグガイド矢印」の末尾に次の箇条書きを追加する:

```markdown
- **枠線ハイライトの文言ラベル**: `tutorial.presentation` の outline に `labelTutorialGuid` があるとき、`TutorialOverlay` が枠線の下辺外側・左揃えに `t(challengeTutorial.<guid>.text)` のラベルを描く（ユーザー裁定 2026-08-20）。面は `--world-pin-face`、文字は `--text-high-contrast`、間隔・文字サイズは `--tutorial-highlight-label-*` 固定長トークン。枠線が非表示ならラベルも出さない。吹き出し矢印・光彩・アニメーションは付けない。
```

- [ ] **Step 4: テスト・lint・型**

Run: `npx vitest run src/features/tutorial && npm run lint && npx tsc -b`
Expected: 全PASS / エラー0。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/features/tutorial moorestech_web/webui/src/app/tokens.css .agents/skills/webui-design/SKILL.md
git commit -m "feat(webui): 枠線ハイライトの脇に challengeTutorial 文言ラベルを描く

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task A6: Web — 下中央の KeyControlHintHud

**Files:**
- Create: `moorestech_web/webui/src/features/tutorial/KeyControlHintHud.tsx`
- Create: `moorestech_web/webui/src/features/tutorial/keyControlHint.module.css`
- Create: `moorestech_web/webui/src/features/tutorial/KeyControlHintHud.test.ts`
- Modify: `moorestech_web/webui/src/features/tutorial/index.ts`（export 追加）
- Modify: `moorestech_web/webui/src/app/App.tsx:119`（`<ProgressBar />` の直後に `<KeyControlHintHud />`）
- Modify: `moorestech_web/webui/src/app/tokens.css`

**Interfaces:**
- Consumes: `Topics.tutorialPresentation`、`Topics.uiState`（`state: string`）、`useBlockingSkitActive()`、`challengeTutorialTextKey`、`useI18n`
- Produces: `export function KeyControlHintHud()`；DOM `div[data-testid="key-control-hint-hud"] > div[data-testid="key-control-hint"]`（`<kbd>{keyName}</kbd><span>{text}</span>`）

- [ ] **Step 1: 失敗するテストを書く**

`KeyControlHintHud.test.ts`:

```ts
// キー操作ヒントは uiState 一致かつスキット非blocking のときだけ描く
// Key-control hints render only while uiState matches and no blocking skit is active
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { TutorialPresentationData } from "@/bridge";

const host = vi.hoisted(() => ({
  presentation: null as TutorialPresentationData | null,
  uiState: null as { state: string } | null,
  blockingSkit: false,
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    useTopic: (topic: string) => {
      if (topic === actual.Topics.tutorialPresentation) return host.presentation;
      if (topic === actual.Topics.uiState) return host.uiState;
      return null;
    },
  };
});
vi.mock("@/shared/uiState", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/uiState")>();
  return { ...actual, useBlockingSkitActive: () => host.blockingSkit };
});
vi.mock("@/shared/i18n", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/i18n")>();
  return { ...actual, useI18n: () => ({ t: (key: string) => `T:${key}` }) };
});

import { KeyControlHintHud } from "./KeyControlHintHud";

const keyControl = (elementId: string, keyName: string, uiState: string) => ({
  kind: "keyControl" as const, elementId, tutorialGuid: "22222222-2222-4222-8222-222222222222", keyName, uiState,
});

function render() {
  let renderer!: ReturnType<typeof create>;
  act(() => { renderer = create(createElement(KeyControlHintHud)); });
  return renderer;
}

describe("KeyControlHintHud", () => {
  afterEach(() => { host.presentation = null; host.uiState = null; host.blockingSkit = false; });

  it("uiStateが一致するヒントだけを描く", () => {
    host.uiState = { state: "GameScreen" };
    host.presentation = { revision: 1, sessions: [{ tutorialSessionId: "s1", challengeId: "c1", elements: [
      keyControl("k1", "Tab", "GameScreen"), keyControl("k2", "R", "PlayerInventory"),
    ] }] };
    const renderer = render();
    const hints = renderer.root.findAllByProps({ "data-testid": "key-control-hint" });
    expect(hints.length).toBe(1);
    expect(renderer.root.findByType("kbd").children).toEqual(["Tab"]);
    expect(renderer.root.findByType("span").children).toEqual(["T:challengeTutorial.22222222-2222-4222-8222-222222222222.text"]);
  });

  it("一致するヒントが無ければHUD自体を描かない", () => {
    host.uiState = { state: "ResearchTree" };
    host.presentation = { revision: 1, sessions: [{ tutorialSessionId: "s1", challengeId: "c1", elements: [keyControl("k1", "Tab", "GameScreen")] }] };
    const renderer = render();
    expect(renderer.root.findAllByProps({ "data-testid": "key-control-hint-hud" }).length).toBe(0);
  });

  it("blockingスキット中は描かない", () => {
    host.uiState = { state: "GameScreen" };
    host.blockingSkit = true;
    host.presentation = { revision: 1, sessions: [{ tutorialSessionId: "s1", challengeId: "c1", elements: [keyControl("k1", "Tab", "GameScreen")] }] };
    const renderer = render();
    expect(renderer.root.findAllByProps({ "data-testid": "key-control-hint-hud" }).length).toBe(0);
  });
});
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `npx vitest run src/features/tutorial/KeyControlHintHud.test.ts`
Expected: FAIL（モジュール未作成）。

- [ ] **Step 3: コンポーネントを実装する**

`KeyControlHintHud.tsx`:

```tsx
import { Topics, useTopic } from "@/bridge";
import { challengeTutorialTextKey, useI18n } from "@/shared/i18n";
import { useBlockingSkitActive } from "@/shared/uiState";
import styles from "./keyControlHint.module.css";

// keyControl要素のうち現在のUI状態に一致するものだけを下中央HUDへ縦積みする
// Stack only the keyControl elements matching the current UI state in the bottom-center HUD
export function KeyControlHintHud() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const uiState = useTopic(Topics.uiState);
  const blockingSkitActive = useBlockingSkitActive();
  const { t } = useI18n();
  if (blockingSkitActive || !presentation || !uiState) return null;

  const hints = presentation.sessions.flatMap((session) => session.elements.flatMap((element) =>
    element.kind === "keyControl" && element.uiState === uiState.state
      ? [{ key: `${session.tutorialSessionId}:${element.elementId}`, keyName: element.keyName, tutorialGuid: element.tutorialGuid }]
      : []));
  if (hints.length === 0) return null;

  return (
    <div className={styles.hud} data-testid="key-control-hint-hud">
      {hints.map((hint) => (
        <div key={hint.key} className={styles.hint} data-testid="key-control-hint">
          <kbd>{hint.keyName}</kbd>
          <span>{t(challengeTutorialTextKey(hint.tutorialGuid))}</span>
        </div>
      ))}
    </div>
  );
}
```

`keyControlHint.module.css`（ProgressBar と同じ「ホットバーの床」基準・§8.16のHUD族 z層）:

```css
/* ホットバー直上（採掘ゲージのさらに上）へ中央揃えで積む常時表示HUD族。面は持たず pointer-events:none */
/* Always-on HUD family stacked centered right above the hotbar (above the mining gauge); faceless, pointer-events:none */
.hud {
  position: absolute;
  right: 0;
  bottom: calc(var(--hotbar-bottom) + var(--hotbar-slot-size) + var(--hotbar-number-tab-overhang) + var(--tutorial-key-hint-hotbar-gap));
  left: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--tutorial-key-hint-gap);
  pointer-events: none;
  z-index: var(--z-stage-overlay-panel);
}

/* InventoryScreenChrome の keyHints と同じ文字様式（kbd + 文言） */
/* Same text style as InventoryScreenChrome's keyHints (kbd + text) */
.hint {
  display: flex;
  align-items: center;
  gap: var(--tutorial-key-hint-kbd-gap);
  font-size: var(--tutorial-key-hint-font-size);
  line-height: 1.2;
  letter-spacing: 0.055em;
  font-weight: 500;
  color: var(--text-high-contrast);
  text-shadow: 0.35px 0.35px 0 rgb(0 0 0 / 80%);
}

.hint kbd {
  font: inherit;
  color: var(--text-high-contrast);
}
```

`tokens.css` に追加（`--tutorial-highlight-label-*` の隣）:

```css
  --tutorial-key-hint-hotbar-gap: 72px;
  --tutorial-key-hint-gap: 6px;
  --tutorial-key-hint-kbd-gap: 10px;
  --tutorial-key-hint-font-size: 25px;
```

`features/tutorial/index.ts` に `export { KeyControlHintHud } from "./KeyControlHintHud";` を追加。`App.tsx` の import を `import { KeyControlHintHud, TutorialOverlay, WorldPinOverlay } from "@/features/tutorial";` にし、`<ProgressBar />` の直後（119行）に `<KeyControlHintHud />` を置く（viewport族・ホットバー床基準のため同じ `viewportOverlay` 内）。

`webui-design` SKILL.md（`.agents/skills/webui-design/SKILL.md`）の「## 8.17 チュートリアルのドラッグガイド矢印」の直後に次の節をそのまま追加する（§9「このドキュメントに書かれていないパターンの使用」禁止のため）:

```markdown
## 8.18 キー操作ヒントHUD（チュートリアルの keyControl）

- `tutorial.presentation` の kind `keyControl`（tutorialGuid / keyName / uiState）を `KeyControlHintHud` が描く。表示は `ui_state.current` の `state` が `uiState` と一致する間だけで、blockingスキット中は出さない（ユーザー裁定 2026-08-20）。
- 配置は常時表示HUD族の `.viewportOverlay` 内・画面下中央で、ホットバーの床（`--hotbar-bottom` + スロット高 + 番号タブ）から `--tutorial-key-hint-hotbar-gap` だけ上に置き、採掘ゲージと重ねない。複数は `--tutorial-key-hint-gap` で縦積み。
- 様式は §7 のキー操作ヒント（`<kbd>{keyName}</kbd>` + `t(challengeTutorial.<guid>.text)`）。文字サイズ・間隔は `--tutorial-key-hint-*` 固定長トークン。面・枠・光彩・アニメーションは持たず `pointer-events: none`。
```

- [ ] **Step 4: テスト・lint・型**

Run: `npx vitest run src/features/tutorial && npm run lint && npx tsc -b`
Expected: 全PASS。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/features/tutorial moorestech_web/webui/src/app .agents/skills/webui-design/SKILL.md
git commit -m "feat(webui): keyControl ヒントをキーキャップ付き下中央HUDとして描く

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task A7: e2e フィクスチャ（mock-host）とスペック

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts:84-96`
- Modify: `moorestech_web/webui/e2e/tests/system/tutorial.spec.ts`

- [ ] **Step 1: mock-host にシナリオを追加する**

`topicControls.ts` の `tutorialOutline` の次に追加:

```ts
  tutorialOutlineWithLabel: () => control(Topics.tutorialPresentation, {
    revision: 2,
    sessions: [{
      tutorialSessionId: "tutorial-session-1", challengeId: "tutorial-challenge-1",
      elements: [{
        kind: "outline" as const, elementId: "tutorial-highlight-2", anchorId: "game.crosshair",
        paddingPx: 8, blocksPointerInput: false, labelTutorialGuid: "11111111-1111-4111-8111-111111111111",
      }],
    }],
  }),
  tutorialKeyControl: () => control(Topics.tutorialPresentation, {
    revision: 3,
    sessions: [{
      tutorialSessionId: "tutorial-session-1", challengeId: "tutorial-challenge-1",
      elements: [{
        kind: "keyControl" as const, elementId: "tutorial-key-1",
        tutorialGuid: "11111111-1111-4111-8111-111111111111", keyName: "Tab", uiState: "GameScreen",
      }],
    }],
  }),
```

- [ ] **Step 2: スペックを追加する**

`tutorial.spec.ts` 末尾:

```ts
test("outline with a label renders the label text beside the outline", async ({ page }) => {
  await page.goto("/");
  await setTopicScenario(page, "tutorialOutlineWithLabel");
  const label = page.getByTestId("tutorial-highlight-label");
  await expect(label).toBeVisible();
  await expect(label).not.toHaveText("");
});

test("keyControl hint renders a kbd and text above the hotbar while uiState matches", async ({ page }) => {
  await page.goto("/");
  await setTopicScenario(page, "tutorialKeyControl");
  const hint = page.getByTestId("key-control-hint");
  await expect(hint).toBeVisible();
  await expect(hint.locator("kbd")).toHaveText("Tab");
});
```

mock-host の既定 `ui_state.current` が `GameScreen` でない場合は、既存の ui state シナリオ（`topicControls.ts` 内の `uiState*`）で `GameScreen` にしてから検証する。

- [ ] **Step 3: 実行**

Run: `cd moorestech_web/webui && npm run test:e2e -- e2e/tests/system/tutorial.spec.ts`
Expected: PASS（ポート5273衝突で偽失敗する場合は別セッションのe2e終了を待つ）。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_web/webui/e2e
git commit -m "test(webui-e2e): 枠線ラベルと keyControl ヒントの表示スペックを追加

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task A8: PR-A 全ブランチレビュー（必須）

- [ ] **Step 1:** `uloop compile` errors 0、`uloop run-tests ... --filter-value "Tutorial|Localization"` 全PASS、`npx vitest run` 全PASS、`npm run lint`、`npx tsc -b` を再確認する。
- [ ] **Step 2:** 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（moores-code-review・自動実行・ゴール文言による省略不可）。指摘の機械的修正を適用し、設計判断は AskUserQuestion で仰ぐ。
- [ ] **Step 3:** pr-create スキルで PR を作る（タイトル例: `feat(tutorial): 枠線ラベル描画と keyControl のWeb復活（keyName/uiState enum）`）。

---

# PR-B: アンカー語彙拡張（ブランチ `feature/tutorial-anchor-inventory-equipment`）

## File Structure（PR-B）

- Modify `moorestech_web/webui/src/shared/tutorialAnchor/tutorialAnchor.ts`（複数ID）＋ `tutorialAnchor.test.ts`
- Modify `moorestech_web/webui/src/shared/tutorialAnchor/resolveAnchor.ts`、`anchorRegistry.ts`（`~=` セレクタ）＋ `resolveAnchor.test.ts`
- Modify `moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts`、`index.ts`
- Modify `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json`
- Create `moorestech_web/webui/src/features/inventory/inventoryItemAnchors.ts`（＋`.test.ts`）
- Modify `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx`
- Modify `moorestech_web/webui/src/features/inventory/EquipmentPanel/index.tsx`（＋`index.test.ts`）

### Task B1: `data-tutorial-anchor` を空白区切りトークン列にする

**Files:**
- Modify: `moorestech_web/webui/src/shared/tutorialAnchor/tutorialAnchor.ts`
- Modify: `moorestech_web/webui/src/shared/tutorialAnchor/resolveAnchor.ts:9-11`
- Modify: `moorestech_web/webui/src/shared/tutorialAnchor/anchorRegistry.ts:47-48`
- Test: `tutorialAnchor.test.ts`、`resolveAnchor.test.ts`

**Interfaces:**
- Produces: `tutorialAnchor(...anchorIds: TutorialAnchorId[]): TutorialAnchorAttributes`（0個は禁止・1個は従来どおり、複数は空白結合）；`tutorialAnchorSelector(anchorId: string): string`（`[data-tutorial-anchor~="…"]` を返す。resolveAnchor / registry 共通）

- [ ] **Step 1: 失敗するテストを書く**

`tutorialAnchor.test.ts`:

```ts
  it("joins multiple anchor ids with a single space", () => {
    expect(tutorialAnchor("equipment.slot-0", "equipment.selected-slot")).toEqual({
      "data-tutorial-anchor": "equipment.slot-0 equipment.selected-slot",
    });
  });
```

`resolveAnchor.test.ts` の `beforeEach` の `document` スタブを、セレクタ文字列を記録する形にし、テストを追加:

```ts
  let lastSelector = "";
  // beforeEach 内: vi.stubGlobal("document", { querySelectorAll: (selector: string) => { lastSelector = selector; return matches; } });

  it("matches anchors as whitespace-separated tokens", () => {
    resolveTutorialAnchor("equipment.selected-slot");
    expect(lastSelector).toBe('[data-tutorial-anchor~="equipment.selected-slot"]');
  });
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `npx vitest run src/shared/tutorialAnchor`
Expected: FAIL（2件）。

- [ ] **Step 3: 実装する**

`tutorialAnchor.ts`:

```ts
import type { DynamicTutorialAnchorId, StaticTutorialAnchorId } from "./anchorIds";

export type TutorialAnchorId = StaticTutorialAnchorId | DynamicTutorialAnchorId;
export type AnchorId = TutorialAnchorId;

export type TutorialAnchorAttributes = Readonly<{
  "data-tutorial-anchor": string;
}>;

// 1要素が複数のアンカー名を名乗れるよう空白区切りトークン列にする（アンカーIDに空白は含まれない）
// One element may declare several anchor names as a whitespace-separated token list (anchor IDs never contain spaces)
export function tutorialAnchor(first: TutorialAnchorId, ...rest: TutorialAnchorId[]): TutorialAnchorAttributes {
  return { "data-tutorial-anchor": [first, ...rest].join(" ") };
}

// トークン一致セレクタ。resolveAnchor と registry が同じ書式で問い合わせる
// Token-match selector shared by resolveAnchor and the registry
export function tutorialAnchorSelector(anchorId: string): string {
  const escaped = globalThis.CSS?.escape ? globalThis.CSS.escape(anchorId) : anchorId.replaceAll('"', '\\"');
  return `[data-tutorial-anchor~="${escaped}"]`;
}
```

`resolveAnchor.ts` の 9-11 行を `const matches = document.querySelectorAll<HTMLElement>(tutorialAnchorSelector(anchorId));` にし、`import { tutorialAnchorSelector } from "./tutorialAnchor";` を追加。`anchorRegistry.ts` 47-48 行も `tutorialAnchorSelector(anchorId)` に置換。`index.ts` に `tutorialAnchorSelector` を export 追加。

- [ ] **Step 4: テスト**

Run: `npx vitest run src/shared/tutorialAnchor src/features/tutorial && npx tsc -b && npm run lint`
Expected: PASS。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/shared/tutorialAnchor
git commit -m "feat(webui): tutorial anchor を空白区切りトークン列にし ~= で解決する

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task B2: 語彙追加（anchorIds.ts とフィクスチャ）

**Files:**
- Modify: `moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts`
- Modify: `moorestech_web/webui/src/shared/tutorialAnchor/index.ts`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json`

**Interfaces:**
- Produces: `TutorialAnchorIds.equipmentSelectedSlot = "equipment.selected-slot"`；`TutorialAnchorDynamicPrefixes.inventoryItem = "inventory.item-"`、`.equipmentSlot = "equipment.slot-"`；`inventoryItemAnchorId(itemGuid: string)`（小文字化）、`equipmentSlotAnchorId(index: number)`。

- [ ] **Step 1: フィクスチャを更新する（テストが先に失敗する）**

`tutorial_anchor_ids.json` の `staticIds` 末尾に `"equipment.selected-slot"`、`dynamicPrefixes` に `"inventoryItem": "inventory.item-"`, `"equipmentSlot": "equipment.slot-"` を追加。
Run: `npx vitest run src/shared/tutorialAnchor/anchorIds.test.ts` → FAIL（Web側未追加）。

- [ ] **Step 2: anchorIds.ts を更新する**

```ts
  hotbarHud: "hotbar.hud",
  equipmentSelectedSlot: "equipment.selected-slot",
} as const;
```

```ts
export const TutorialAnchorDynamicPrefixes = {
  researchNode: "research.node-",
  recipeItem: "recipe.item-",
  buildMenuEntry: "build-menu.entry-",
  challengeNode: "challenge.node-",
  inventoryItem: "inventory.item-",
  equipmentSlot: "equipment.slot-",
} as const;

// メインインベントリで該当アイテムを持つ先頭スロット。guidはマスタ直書き値と一致させるため小文字化する
// First main-inventory slot holding the item; the guid is lowercased to match master-written values
export function inventoryItemAnchorId(itemGuid: string): DynamicTutorialAnchorId {
  return `${TutorialAnchorDynamicPrefixes.inventoryItem}${itemGuid}`.toLowerCase() as DynamicTutorialAnchorId;
}

export function equipmentSlotAnchorId(index: number): DynamicTutorialAnchorId {
  return `${TutorialAnchorDynamicPrefixes.equipmentSlot}${index}` as DynamicTutorialAnchorId;
}
```

`index.ts` の export に `inventoryItemAnchorId, equipmentSlotAnchorId` を追加。

- [ ] **Step 3: テスト**

Run: `npx vitest run src/shared/tutorialAnchor`（PASS）。Unity側: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TutorialAnchorContractTest"`（PASS。`AllModAnchorIdsResolveToWebVocabulary` は sibling master が古いと Ignore になる）。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_web/webui/src/shared/tutorialAnchor moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json
git commit -m "feat(tutorial): アンカー語彙に inventory.item-/equipment.slot-/equipment.selected-slot を追加

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task B3: InventoryPanel — 所持アイテム先頭スロットのアンカー

**Files:**
- Create: `moorestech_web/webui/src/features/inventory/inventoryItemAnchors.ts`
- Create: `moorestech_web/webui/src/features/inventory/inventoryItemAnchors.test.ts`
- Modify: `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx:41-53`

**Interfaces:**
- Produces: `firstSlotIndexByItemId(slots: ReadonlyArray<{ itemId: number }>): Map<number, number>`（itemId>0 の各アイテムについて先頭のスロットindex）

- [ ] **Step 1: 失敗するテストを書く**

```ts
import { describe, expect, it } from "vitest";
import { firstSlotIndexByItemId } from "./inventoryItemAnchors";

describe("firstSlotIndexByItemId", () => {
  // 同じアイテムが複数スロットにあっても先頭だけを採り、空スロット(0)は無視する
  // Only the first slot per item is taken even when it appears in several; empty slots (0) are ignored
  it("maps each item to its first slot and skips empty slots", () => {
    const slots = [{ itemId: 0 }, { itemId: 7 }, { itemId: 3 }, { itemId: 7 }];
    expect([...firstSlotIndexByItemId(slots)]).toEqual([[7, 1], [3, 2]]);
  });
});
```

- [ ] **Step 2: 実行して失敗を確認する** — `npx vitest run src/features/inventory/inventoryItemAnchors.test.ts` → FAIL。

- [ ] **Step 3: 実装する**

`inventoryItemAnchors.ts`:

```ts
// アイテムごとに先頭スロットだけをアンカー担当にする。同名アンカーの重複はresolverが不一致扱いにするため
// Only the first slot per item carries the anchor; duplicate anchor names would be rejected by the resolver
export function firstSlotIndexByItemId(slots: ReadonlyArray<{ itemId: number }>): Map<number, number> {
  const result = new Map<number, number>();
  slots.forEach((slot, index) => {
    if (slot.itemId <= 0 || result.has(slot.itemId)) return;
    result.set(slot.itemId, index);
  });
  return result;
}
```

`InventoryPanel/index.tsx`: import に `import { useItemMaster } from "@/bridge";`、`import { inventoryItemAnchorId, tutorialAnchor } from "@/shared/tutorialAnchor";`、`import { firstSlotIndexByItemId } from "../inventoryItemAnchors";` を追加。コンポーネント内（`if (!inventory)` の前、フック順を守る）に `const itemMaster = useItemMaster();` を置き、`inventory` 取得後に `const firstSlots = firstSlotIndexByItemId(inventory.mainSlots);`。グリッドの各スロットを次のようにラップする（ItemListPanel と同じ「div ラッパーにアンカー」前例）:

```tsx
        {inventory.mainSlots.map((slot, i) => {
          const ref: SlotRef = { area: "main", slot: i };
          const itemGuid = firstSlots.get(slot.itemId) === i ? itemMaster?.get(slot.itemId)?.itemGuid : undefined;
          return (
            <div key={`main-${i}`} {...(itemGuid ? tutorialAnchor(inventoryItemAnchorId(itemGuid)) : {})}>
              <ItemSlot
                itemId={slot.itemId}
                count={slot.count}
                onLeftDown={(shiftKey) => slotActions.onLeftDown(ref, shiftKey)}
                onRightDown={() => slotActions.onRightDown(ref)}
                onRightEnter={() => slotActions.onRightEnter(ref)}
                onLeftEnter={() => slotActions.onLeftEnter(ref)}
                onDoubleClick={() => slotActions.onDoubleClick(ref)}
              />
            </div>
          );
        })}
```

`SlotGrid` の子が `div > ItemSlot` になっても寸法が崩れないことを確認する（`ItemListPanel` が同構造で運用中）。崩れる場合は `display: contents` を使わず（zero-area で hidden 扱いになる）、ラッパー div に `className={styles.slotCell}`（`display:block`）を足す。

- [ ] **Step 4: テスト・lint・型** — `npx vitest run src/features/inventory && npx tsc -b && npm run lint` → PASS。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/features/inventory
git commit -m "feat(webui): メインインベントリの所持アイテム先頭スロットへ inventory.item-<guid> アンカーを付ける

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task B4: EquipmentPanel — 装備スロット／選択中スロットのアンカー

**Files:**
- Modify: `moorestech_web/webui/src/features/inventory/EquipmentPanel/index.tsx:91-108`
- Test: `moorestech_web/webui/src/features/inventory/EquipmentPanel/index.test.ts`

- [ ] **Step 1: 失敗するテストを書く**

`index.test.ts` の既存 describe（`beforeEach` が `host.inventory` に `mainSlots/grab/equipment/selectedEquipment/equipmentSelectionConfirmationRevision` を入れる）に追加。`slot(itemId,count)` ヘルパは既存:

```ts
  // 各装備枠は equipment.slot-<i> を名乗り、選択中の枠だけ equipment.selected-slot も併せて名乗る
  // Every equipment slot declares equipment.slot-<i>; only the selected one also declares equipment.selected-slot
  it("declares slot anchors and marks the selected slot", () => {
    host.inventory = {
      mainSlots: [slot(0, 0)], grab: slot(0, 0),
      equipment: [slot(0, 0), slot(0, 0)], selectedEquipment: 1, equipmentSelectionConfirmationRevision: 0,
    };
    const renderer = create(createElement(EquipmentPanel));
    const anchors = renderer.root.findAll((node) => typeof node.props["data-tutorial-anchor"] === "string")
      .map((node) => node.props["data-tutorial-anchor"]);
    expect(anchors).toEqual(["equipment.slot-0", "equipment.slot-1 equipment.selected-slot"]);
  });
```

- [ ] **Step 2: 実行して失敗を確認する** — `npx vitest run src/features/inventory/EquipmentPanel` → FAIL。

- [ ] **Step 3: 実装する**

import に `import { equipmentSlotAnchorId, tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";` を追加し、map 内を:

```tsx
      {inventory.equipment.map((slot, i) => {
        const ref: SlotRef = { area: "equipment", slot: i };
        const selected = i === inventory.selectedEquipment;
        const anchor = selected
          ? tutorialAnchor(equipmentSlotAnchorId(i), TutorialAnchorIds.equipmentSelectedSlot)
          : tutorialAnchor(equipmentSlotAnchorId(i));
        return (
          <div key={`equipment-${i}`} {...anchor}>
            <ItemSlot
              itemId={slot.itemId}
              count={slot.count}
              selected={selected}
              onLeftDown={grabInteractive ? (shiftKey) => slotActions.onLeftDown(ref, shiftKey) : undefined}
              onRightDown={grabInteractive ? () => slotActions.onRightDown(ref) : undefined}
              onRightEnter={grabInteractive ? () => slotActions.onRightEnter(ref) : undefined}
              onLeftEnter={grabInteractive ? () => slotActions.onLeftEnter(ref) : undefined}
              onDoubleClick={grabInteractive ? () => slotActions.onDoubleClick(ref) : undefined}
            />
          </div>
        );
      })}
```

`.equipmentArea` は `display:flex; flex-direction:column` なのでラッパー div は自然に縦並びになる（`--slot-size` は継承）。

- [ ] **Step 4: テスト・lint・型** — `npx vitest run src/features/inventory && npx tsc -b && npm run lint` → PASS。e2e のスクリーンショット比較spec（inventory/hotbar 系）がある場合は `npm run test:e2e -- e2e/tests/system/inventory*.spec.ts` 相当で DOM 変更による退行が無いことを確認する。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/features/inventory/EquipmentPanel
git commit -m "feat(webui): 装備スロットへ equipment.slot-<i> と選択中の equipment.selected-slot アンカーを付ける

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task B5: PR-B 全ブランチレビュー（必須）

- [ ] **Step 1:** `npx vitest run`、`npm run lint`、`npx tsc -b`、`uloop run-tests ... "TutorialAnchorContractTest"` を再確認。
- [ ] **Step 2:** 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（moores-code-review・自動実行・ゴール文言による省略不可）。
- [ ] **Step 3:** pr-create で PR 作成（`feat(webui): チュートリアルアンカー語彙にインベントリ所持スロットと装備スロットを追加`）。

---

# PR-C: 石の斧の手持ちモデル Addressable 登録（ブランチ `feature/stone-axe-hand-model`）

## File Structure（PR-C）

- Create（Unity Editor経由）: `moorestech_client/Assets/AddressableResources/Item/StoneAxe.prefab`（＋`.meta`）
- Modify（Unity Editor経由）: `moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset`

### Task C1: ラッパープレハブ生成と Addressable 登録（Editor 経由・テキスト編集禁止）

**Files:**
- Create: `moorestech_client/Assets/AddressableResources/Item/StoneAxe.prefab`
- Modify: `moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset`

**Interfaces:**
- Produces: Addressable address `Vanilla/Item/StoneAxe`（GameObject）。マスタ `石の斧.addressablePaths.handGrabModel` から参照する。

- [ ] **Step 1: 元プレハブの構造を確認する**

`Assets/Dependencies/Sketchfab/StoneAxe/StoneAxe.prefab`（guid `ba793c97a36087e48872b232c94bce98`）のルート名 `StoneAxe`、子 `Cylinder`（scale 0.0629）・`Circle`（scale 0.0185）。`StoneTool.prefab` は Sketchfab FBX をネストし `m_LocalScale 0.0025 / position (-0.534, 0.083, -0.137) / yaw 90°` を焼き込んでいる。

- [ ] **Step 2: uloop execute-dynamic-code でラッパーを生成し登録する**

worktree の Editor に対して次のC#を実行する（`uloop-execute-dynamic-code` スキル参照）。初期値は StoneTool と同じ姿勢から始める:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

var sourcePath = "Assets/Dependencies/Sketchfab/StoneAxe/StoneAxe.prefab";
var outputPath = "Assets/AddressableResources/Item/StoneAxe.prefab";
var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
var root = new GameObject("StoneAxe");
var child = (GameObject)PrefabUtility.InstantiatePrefab(source);
child.transform.SetParent(root.transform, false);
child.transform.localScale = Vector3.one * 0.0025f;
child.transform.localPosition = new Vector3(-0.534f, 0.083f, -0.137f);
child.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
PrefabUtility.SaveAsPrefabAsset(root, outputPath);
Object.DestroyImmediate(root);

var settings = AddressableAssetSettingsDefaultObject.Settings;
var group = settings.FindGroup("Vanilla Asset Group");
var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(outputPath), group, false, false);
entry.address = "Vanilla/Item/StoneAxe";
settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true, true);
AssetDatabase.SaveAssets();
Debug.Log("StoneAxe registered: " + entry.address);
```

Run 後: `grep -n "Vanilla/Item/StoneAxe" "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset"` で1件、`ls moorestech_client/Assets/AddressableResources/Item/StoneAxe.prefab*` で prefab と meta が存在。

- [ ] **Step 3: PlayMode で見た目を確認して姿勢を調整する**

masterピンworktree（`moorestech-master-worktrees/pin-*`）の `items.json` ではなく、検証用に `../moorestech_master` 側で PR #22 のブランチ `feature/tutorial-master-tweaks-20260820` を使い、石の斧の `addressablePaths.handGrabModel` を一時的に `Vanilla/Item/StoneAxe` にしてPlayMode起動（unity-playmode-recorded-playtest のDSLで 石の斧を所持→装備スロットへ移動→装備選択→`uloop screenshot` Game View）。モデルの向き・大きさが石器と同程度になるまで Step 2 の scale/position/rotation を変えて再保存する（`PrefabUtility.SaveAsPrefabAsset` を再実行。Addressable登録は再実行不要）。最終値を commit message に記す。一時変更したマスタは `git -C ../moorestech_master checkout -- server_v8/...items.json` で戻す（値の恒久反映はマスタPRで行う）。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/AddressableResources/Item/StoneAxe.prefab moorestech_client/Assets/AddressableResources/Item/StoneAxe.prefab.meta "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset"
git commit -m "feat(asset): 石の斧の手持ちモデル StoneAxe.prefab を Vanilla/Item/StoneAxe で Addressable 登録（scale/pos/rot=<最終値>）

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

### Task C2: PR-C 全ブランチレビュー（必須）

- [ ] **Step 1:** `uloop compile` errors 0。
- [ ] **Step 2:** 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（moores-code-review・自動実行・ゴール文言による省略不可）。
- [ ] **Step 3:** pr-create で PR 作成（`feat(asset): 石の斧の手持ちモデルを Addressable 登録`）。

---

# マスタPR（PR-A/B/C マージ後・`../moorestech_master` ブランチ `feature/tutorial-keycontrol-labels-equip-guide`、origin/master 起点で #22 を含む）

### Task M1: generator と challenges.json の追記

**Files:**
- Modify: `tools/tutorial_v3_port/generate_challenges.py`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/challenges.json`（再生成）

- [ ] **Step 1: ヘルパを追加する**

```python
def key(state, key_name, text): return ('keyControl', {'uiState': state, 'keyName': key_name, 'controlText': text})
# 装備誘導: メインインベントリの所持スロットから選択中の装備枠へ
# Equip guide: from the main-inventory slot holding the item to the selected equipment slot
def equip_drag(item_name, text): return ('uiDragGuide', {
    'fromAnchorId': f'inventory.item-{items[item_name]}', 'toAnchorId': 'equipment.selected-slot'})
```

- [ ] **Step 2: CHALLENGES を更新する（keyは不変・tutorials配列のみ変更）**

```python
    ('小石を3個拾う', '小石を3個拾う', '地面の小石を左クリックで3個拾おう', 'item', '小石', 3,
     [pin('小石', '左クリックで拾う'), ui('challenge.current-hud', '左上で現在の目標を確認する')], '小石'),
    ('石器を作る', '石器を作る', '小石3個からインベントリで石器をクラフトしよう', 'craft', '石器', None,
     [ui('recipe.craft-button', '②クラフトボタンを長押し'), iv('石器', '①石器を選択'), key('GameScreen', 'Tab', 'インベントリを開く')], '石器'),
    ('木を伐採して原木を入手する', '木を伐採して原木を入手する', '石器を装備して木を伐採し、原木を3個集めよう', 'item', '原木', 3,
     [pin('木', '石器で木を伐採'), key('GameScreen', 'Tab', 'インベントリを開いて石器を装備'), equip_drag('石器', '装備スロットへドラッグ')], '原木'),
    ('木の板を5枚作る', '木の板を5枚作る', '原木から木の板を5枚クラフトしよう', 'item', '木の板', 5,
     [iv('木の板', '原木から木の板を作る'), key('GameScreen', 'Tab', 'インベントリを開く')], '木の板'),
    ('原始研究1を完了する', '原始研究1を完了する', 'Rキーで研究画面を開き、木の板5枚と木の棒5本で原始研究1を完了しよう', 'research', '原始研究1', None,
     [research_node_ui('原始研究1', '原始研究1を完了する'), key('GameScreen', 'R', '研究画面を開く'), key('PlayerInventory', 'R', '研究画面を開く')], '木の板'),
```

注意: tutorialGuid は `(key, slot)` 導出なので、既存枠（slot 0/1）の文言変更でも GUID は不変、追加枠は新GUID。「石器を作る」の slot0（uiHighLight）と slot1（itemViewHighLight）の順序は維持する。summary を変えた「木を伐採…」は `challenge.<guid>.summary` の CSV 行も更新する。

- [ ] **Step 3: 再生成して差分を確認する**

Run: `python3 tools/tutorial_v3_port/generate_challenges.py && git diff --stat`
Expected: 変更は challenges.json のみ。diff に `highLightUIObjectId` 等の旧語彙が無い・既存GUIDが消えていない（`git diff | grep '^-.*Guid'` が空）。

### Task M2: mod_3 の keyControl に keyName を追加

- [ ] `server/mods/moorestechAlphaMod_3/master/challenges.json` の keyControl 1件（tutorialGuid `cebf17e3-…`）の `tutorialParam` に `"keyName": "Tab"` を追加（`uiState` は `GameScreen` のまま）。`grep -rn '"keyControl"' -A4 server*/mods/*/master/challenges.json | grep -c keyName` が keyControl 件数と一致することを確認。

### Task M3: items.json（石の斧）

- [ ] 石の斧（`4c5fefbd-60a4-42ea-b70a-38a83b96e25e`）の `addressablePaths.handGrabModel` を `"Vanilla/Item/StoneAxe"` にする（PR #22 で nested 形に統一済み）。

### Task M4: localization.csv

- [ ] 変更した/追加した tutorialGuid すべてについて `challengeTutorial.<guid>.text` 行を更新/追加（Source・japanese=日本語文言、english=自然な英語。例: `左上で現在の目標を確認する` → `Check your current objective in the top-left`、`①石器を選択` → `1. Select the Stone Tool`、`②クラフトボタンを長押し` → `2. Hold the Craft Button`、`インベントリを開く` → `Open the inventory`、`インベントリを開いて石器を装備` → `Open the inventory and equip the Stone Tool`、`装備スロットへドラッグ` は uiDragGuide で文言フィールドが無いため行を作らない、`研究画面を開く` → `Open the research screen`）。「木を伐採…」の summary 行（`challenge.fb529cac-….summary`）の3列も更新。整合確認: `python3` で CSV を読み、challenges.json の全 challengeGuid/tutorialGuid（uiDragGuide を除く）に行があり、孤児行が無いことを確認する。

### Task M5: 検証・コミット・PR・本体ピン更新

- [ ] `python3 -m json.tool` 両JSON OK、ゼロ幅文字無し。本体側 worktree（PR-A/B/C マージ後の master）で `../moorestech_master` をこのブランチにして `uloop run-tests ... "TutorialAnchorContractTest|MasterSourceTextCollectorTest"` PASS（`AllModAnchorIdsResolveToWebVocabulary` が Ignore でなく実行されること）。
- [ ] unityプレイ録画テスト（playtest DSL）で: ①開始直後に左上HUD枠線＋ラベル ②石器を作るで `[Tab] インベントリを開く` が下中央に出て、Tabで消える ③インベントリで石器の枠線＋「①石器を選択」、選択後クラフトボタン枠線＋「②…」 ④木を伐採で Tab後、石器スロット→選択中装備枠へ矢印ループ、装備後に石の斧/石器の手持ちモデル ⑤原始研究1で GameScreen/PlayerInventory 双方に `[R] 研究画面を開く`。
- [ ] コミット・push・`gh pr create --repo moorestech/moorestech_master`。
- [ ] 本体で小PR: `.moorestech-external-revisions.json` の `moorestech_master.commitHash` をマスタPRのマージコミットへ更新（CI の master data checkout が通ることを確認）。

---

## 配置と前例（spec-architecture-review）

| 項目 | 配置 | 前例 |
|---|---|---|
| `TutorialOutlineElementData.LabelTutorialGuid` / `TutorialKeyControlElementData` | `Client.Game/InGame/Tutorial/Presentation`（提示データ） | 同ファイルの `TutorialDragGuideElementData`（kind判別の単一列に種別を足す） |
| `TutorialPresentationStateStore.AddKeyControlHint` | 同store（outline/dragGuide と同じ `AddElement` 経路） | `AddDragGuide` |
| `KeyControlTutorialManager`（store発行のみ） | `Client.Game/InGame/Tutorial`（`ITutorialViewManager`） | `UiDragGuideTutorialManager` / `UIHighlightTutorialManager`（マスタ値を無変換で store へ） |
| uiState一致判定 | Web `KeyControlHintHud`（`ui_state.current` 購読） | `activeLayer.ts` / `uiScreenRouting.ts` が同topicで画面を導出。anchor解決可否もWeb側判断 |
| 文言解決 | Web `t(challengeTutorialTextKey(guid))` | `WorldPinOverlay.tsx:33` |
| keyControl HUD の位置 | `.viewportOverlay` 内・ホットバー床基準 | `features/progress/ProgressBar` の `bottom: calc(--hotbar-bottom + …)` |
| `<kbd>` 様式 | `keyControlHint.module.css` | `InventoryScreenChrome.module.css .keyHints` |
| アンカー語彙追加 | `shared/tutorialAnchor/anchorIds.ts` 単一ソース＋Unityフィクスチャ | `recipeItemAnchorId` / `buildMenuEntryAnchorId`、`tutorial_anchor_ids.json` |
| 所持スロット/装備スロットの anchor 付与 | 各パネルの描画側（`InventoryPanel` / `EquipmentPanel`）が `tutorialAnchor()` を div ラッパーに付与 | `ItemListPanel.tsx:74`（div ラッパー＋`recipeItemAnchorId`）、`ResearchNodeCard.tsx:32` |
| 複数アンカー | 属性トークン列＋`~=` | 新規パターン（ADR 0022 §4 agent前提）。レビュー注目点 |
| schema `keyName` 必須＋enum更新 | `VanillaSchema/challenges.yml` ＋ `_CompileRequester` | edit-schema スキル（optional禁止・全JSON更新。keyControl データは v8 に無く、mod_3 はマスタPRで更新） |
| StoneAxe ラッパーprefab＋Addressable登録 | `AddressableResources/Item/` ＋ `Vanilla Asset Group` | `StoneTool.prefab`、`Editor/MapObjectWrapperGenerator/WrapperAddressableRegistrar.cs`（`CreateOrMoveEntry`） |

データフロー: マスタ → `TutorialManager.ApplyTutorial` → 各 ViewManager（書き手）→ `TutorialPresentationStateStore`［共有状態］→ `tutorial.presentation` topic → Web（読み手: `TutorialOverlay` / `KeyControlHintHud`）。新規コンポーネントはすべて既存の書き手/読み手の位置に入り、交差点（bool戻り・第2経路）は無い。

死活表: 既存の uGUI keyControl 描画はWebモードで元々非表示（死なない）。`KeyControlDescription.SetText` の既存呼び出しは維持。ホットバー/装備/インベントリのクリック・ホイール・D&D操作は DOM ラッパー追加のみで変化なし（B3/B4 のテストで担保）。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0022-tutorial-label-keycontrol-anchor-vocabulary.md`、裁定ファイル: `.decisions/2026-08-20-枠線ハイライトに文言ラベルを描く.md`、`.decisions/2026-08-20-keyControlはキーキャップ付きHUDヒントとしてWebで復活させる.md`、`.decisions/2026-08-20-石器の装備誘導は木を伐採チャレンジのtutorialsに付ける.md`、`.decisions/2026-08-20-アンカー語彙にインベントリ所持スロットと装備スロットを足す.md`、`.decisions/2026-08-20-石の斧の手持ちモデルはStoneToolと同方式でAddressable登録する.md`。調査: `docs/research/2026-08-20-tutorial-master-rewrite-feasibility.md`。
- keyControl の uiState 一致判定を Web 側にする（ADR 0022 §2 に追記済み）。出所: agent前提（`TutorialPresentationStateStore.AddElement` が最後に BeginSession した challenge の session へ付ける構造のため、Unity 側で後から足し引きすると別 challenge の session に紛れ込む。outline/dragGuide の表示可否も Web 側判断という前例と一致）。
- outline のラベル有無は Unity が `HighLightText` の空判定で決め `labelTutorialGuid` を null/省略にする。出所: agent前提（Web の `t()` は欠落キーを `[!key]` プレースホルダで露出するため、Web 側で「文言無し」を判別できない。文言の正本を持つ Unity が決める）。
- 1要素複数アンカーは `data-tutorial-anchor` の空白区切り＋`~=` セレクタ。出所: agent前提（ラッパー要素を増やすと `display:contents` が zero-area で hidden 判定になる／実要素のネスト増はレイアウトに影響する。トークン列は属性セレクタの標準機能で、既存の単一指定を壊さない）。
- `inventory.item-<guid>` は Web 側で「アイテムごとの先頭スロット」に付ける（要求されたアンカーを知らずに全アイテムへ付与）。出所: agent前提（Web は presentation から要求アンカーを逆引きしないという既存の分離を保つ。重複アンカーは resolver が不一致扱いにするため先頭1枠のみ）。
- keyControl HUD の配置は ProgressBar と同じ「ホットバーの床」基準で、採掘ゲージの上に `--tutorial-key-hint-hotbar-gap` で積む。出所: ユーザー裁定 2026-08-20「画面下中央（ホットバーの上）」＋agent前提（ゲージと重ねない）。
- `KeyControlDescription.SetOverrideText/ClearOverrideText` は唯一の呼び出し元が消えるため削除する（AGENTS.md「デバッグ/テスト専用publicをプロダクションに残さない」）。出所: agent前提。
- PR 分割: 本体3PR（A: ラベル＋keyControl＋schema / B: アンカー / C: 斧アセット）＋マスタ1PR。出所: ユーザー裁定 2026-08-20「本体3PR（a/b/c）＋マスタ1PR」。
- メインワークツリーの master はローカル未push 2コミット（docs）と origin/master が分岐している（2026-08-20 時点）。本plan はチュートリアル関連パスに差分が無いことを確認した上で origin/master 基準で書いた。各PRは `moores-wt new <branch> --from origin/master` で切る。出所: agent前提（事実確認済み）。
