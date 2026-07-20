# web-ui ビルドメニュー + ブループリント Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** uGUIのビルドメニュー（Bキー）とブループリント関連UI（BP選択・コピー時の名前入力・BP削除）をweb-ui（React/CEF）へフル移植する。

**Architecture:** Unity側で`BuildMenuEntryCatalog`を再利用したエントリ合成topic（`build_menu.entries`）をpushし、webの選択アクションはuGUIの選択消費キュー（`BuildMenuView._clickedEntry`）へ合流させる。BP名入力は`BlueprintNameInputView`（Client.Game）が状態権威のまま、`WebUiScreenGate`でuGUI表示のみ抑止し、WebUiHost側アダプタが`WebUiModalService`の入力モーダルへ転送する。詳細は `docs/superpowers/specs/2026-07-10-webui-build-menu-blueprint-design.md`。

**Tech Stack:** Unity C#（UniRx/UniTask/VContainer）、React + TypeScript + Mantine + vitest、C#⇔TS共有wireフィクスチャ（NUnit + vitest）

## Global Constraints

- partial禁止・1ファイル200行以下・try-catch原則禁止（外部境界のみ可）
- イベントはC#標準eventでなくUniRx（`Subject<T>` + `IObservable<T>`）
- 単純なgetter/setterプロパティ禁止。SetはSetHogeメソッド
- デフォルト引数禁止。引数追加時は呼び出し側を全て変更する
- 主要処理に日本語→英語の2行コメント（各1行に収める）
- .metaファイルは手動作成しない（Unityが生成したもののコミットは可）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行する
- web側テスト実行: `cd moorestech_web/webui && pnpm exec vitest run <path>`
- Unity側テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`
- コミットは各タスク末尾で必ず行う

---

### Task 1: Web契約拡張（型・topic・action・validator）

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/protocol.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actions.ts`
- Test: `moorestech_web/webui/src/bridge/contract/validators.test.ts`

**Interfaces:**
- Produces: `BuildMenuEntryType` / `BuildMenuEntryData` / `BuildMenuData` 型、`Topics.buildMenu`（"build_menu.entries"）、`UiStateNames.buildMenu`（"BuildMenu"）、action `"build_menu.select"`（`{entryType, entryKey}`）・`"blueprint.delete"`（`{name}`）、`"ui.modal.respond"`の`text?: string`、`ModalRequest.input?: boolean`。後続の全webタスクとUnity側DTOの文字列契約がこれに依存する。

- [ ] **Step 1: validators.test.ts に失敗するテストを追加**

`validators.test.ts` の末尾に追加:

```ts
describe("validBuildMenu", () => {
  const entry = { entryType: "block", entryKey: "1", label: "鉄の機械", tooltip: "鉄の機械\n鉄インゴット x5", iconUrl: "/api/block-icons/1.png" };
  it("accepts icon and text entries", () => {
    const d = { entries: [entry, { entryType: "blueprint", entryKey: "家", label: "家", tooltip: "家" }] };
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(true);
  });
  it("rejects a non-string entryKey", () => {
    const d = { entries: [{ ...entry, entryKey: 1 }] };
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(false);
  });
  it("rejects a missing entries array", () => {
    expect(validateTopicPayload(Topics.buildMenu, {})).toBe(false);
  });
});

describe("validModal input flag", () => {
  const base = { id: "m1", title: "t", message: "m", buttonText: "OK", variant: "confirm" };
  it("accepts input:true", () => {
    expect(validateTopicPayload(Topics.modal, { modal: { ...base, input: true } })).toBe(true);
  });
  it("rejects a non-bool input", () => {
    expect(validateTopicPayload(Topics.modal, { modal: { ...base, input: "yes" } })).toBe(false);
  });
});
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/bridge/contract/validators.test.ts`
Expected: FAIL（`Topics.buildMenu` が存在しない）

- [ ] **Step 3: payloadTypes.ts に型を追加**

`ResearchTreeData` 定義の後に追加:

```ts
// BM-1 ビルドメニューエントリ。uGUI BuildMenuEntryCatalog の合成結果をそのまま運ぶ
// BM-1 build-menu entries; carries the composed result of the uGUI BuildMenuEntryCatalog
export type BuildMenuEntryType = "block" | "trainCar" | "connectTool" | "blueprintCopy" | "blueprint";
export type BuildMenuEntryData = {
  entryType: BuildMenuEntryType;
  // 種別ごとの安定キー: block=BlockId / trainCar=Guid / connectTool=PlaceMode / blueprint=BP名 / blueprintCopy=""
  // Stable key per type: block=BlockId, trainCar=Guid, connectTool=PlaceMode, blueprint=BP name, blueprintCopy=""
  entryKey: string;
  label: string;
  tooltip: string;
  // アイコン無し（BP・BPコピー）はキー省略されるため optional
  // Icon-less entries (blueprints, copy tool) omit the key, so it is optional
  iconUrl?: string;
};
export type BuildMenuData = { entries: BuildMenuEntryData[] };
```

`ModalRequest` を変更（`variant` の下に `input` を追加）:

```ts
export type ModalRequest = {
  id: string;
  title: string;
  message: string;
  buttonText: string;
  variant: "confirm" | "error";
  // 入力必須モーダル（BP名入力等）。false時はC#側でキー省略されるため optional
  // Input-required modal (e.g. blueprint naming); omitted when false, so it is optional
  input?: boolean;
};
```

- [ ] **Step 4: protocol.ts を拡張**

import に `BuildMenuData` を追加。`Topics` へ:

```ts
  buildMenu: "build_menu.entries",
```

`UiStateNames` へ:

```ts
  buildMenu: "BuildMenu",
```

`TopicPayloads` へ:

```ts
  [Topics.buildMenu]: BuildMenuData;
```

`ActionPayloads` の `"ui.modal.respond"` を変更し、2アクションを追加:

```ts
  // text は input モーダルの確定時のみ付与する
  // text accompanies only the confirm of an input modal
  "ui.modal.respond": { id: string; result: "confirm" | "cancel"; text?: string };
  "build_menu.select": { entryType: BuildMenuEntryType; entryKey: string };
  "blueprint.delete": { name: string };
```

（`BuildMenuEntryType` を type import に追加）

`ACTION_TYPES` 配列へ `"build_menu.select"`, `"blueprint.delete"` を追加（網羅チェックがコンパイルで担保される）。

- [ ] **Step 5: validators.ts を拡張**

`validModal` の modal オブジェクト検査に `(m.input === undefined || isBool(m.input))` を追加（既存の実装に合わせて条件連結する）。

`validResearchTree` の後に追加:

```ts
function validBuildMenuEntry(v: unknown): boolean {
  return isObject(v) && isString(v.entryType) && isString(v.entryKey) && isString(v.label) && isString(v.tooltip) &&
    (v.iconUrl === undefined || isString(v.iconUrl));
}
function validBuildMenu(d: unknown): boolean {
  return isObject(d) && isArrayOf(d.entries, validBuildMenuEntry);
}
```

registry へ:

```ts
  [Topics.buildMenu]: validBuildMenu,
```

- [ ] **Step 6: actions.ts の BENIGN_ERRORS に staleクリック系を追加**

`BENIGN_ERRORS` オブジェクトへ追加（既存エントリの形式に合わせる）:

```ts
  // メニューが先に閉じた/BPが先に消えた stale クリックはトースト不要
  // Stale clicks (menu already closed / BP already deleted) need no error toast
  "build_menu.select": new Set(["invalid_state", "unknown_entry"]),
```

- [ ] **Step 7: テストが通ることを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/bridge`
Expected: PASS（既存テスト含む全件）

- [ ] **Step 8: Commit**

```bash
git add moorestech_web/webui/src/bridge
git commit -m "feat(webui): build_menu.entries契約とモーダルinput拡張を追加"
```

---

### Task 2: 共有wireフィクスチャ + TS側契約テスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/build_menu_snapshot.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/modal_input.json`
- Test: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`

**Interfaces:**
- Consumes: Task 1 の `Topics.buildMenu` / `BuildMenuData` / `ModalData`
- Produces: 正準フィクスチャ2点。Task 4/6 の C# `WireContractTest` が同じファイルに対してDTOシリアライズ一致を検証する。

- [ ] **Step 1: フィクスチャを作成**

`build_menu_snapshot.json`（全エントリ種別を1件ずつ含む正準形）:

```json
{
  "entries": [
    { "entryType": "block", "entryKey": "1", "label": "鉄の機械", "tooltip": "鉄の機械\n鉄インゴット x5", "iconUrl": "/api/block-icons/1.png" },
    { "entryType": "trainCar", "entryKey": "11111111-2222-3333-4444-555555555555", "label": "貨物車", "tooltip": "貨物車", "iconUrl": "/api/train-car-icons/11111111-2222-3333-4444-555555555555.png" },
    { "entryType": "connectTool", "entryKey": "BeltConveyor", "label": "ベルトコンベア", "tooltip": "ベルトコンベア", "iconUrl": "/api/icons/3.png" },
    { "entryType": "blueprintCopy", "entryKey": "", "label": "ブループリントコピー", "tooltip": "ブループリントコピー" },
    { "entryType": "blueprint", "entryKey": "家", "label": "家", "tooltip": "家" }
  ]
}
```

`modal_input.json`:

```json
{
  "modal": {
    "id": "m2",
    "title": "ブループリント名",
    "message": "保存するブループリントの名前を入力してください",
    "buttonText": "保存",
    "variant": "confirm",
    "input": true
  }
}
```

- [ ] **Step 2: wireContract.test.ts にTS側検証を追加**

既存の describe 内に追加（既存ケースの書式に合わせる。型消費のimportに `BuildMenuData` を追加）:

```ts
  it("build_menu_snapshot が受理され型消費できる", () => {
    const d = loadFixture("build_menu_snapshot.json");
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(true);
    const typed = d as BuildMenuData;
    expect(typed.entries[0].entryType).toBe("block");
    expect(typed.entries[3].iconUrl).toBeUndefined();
  });

  it("modal_input が受理され input フラグを型消費できる", () => {
    const d = loadFixture("modal_input.json");
    expect(validateTopicPayload(Topics.modal, d)).toBe(true);
    const typed = d as ModalData;
    expect(typed.modal?.input).toBe(true);
  });
```

- [ ] **Step 3: テストが通ることを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/bridge/contract/wireContract.test.ts`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures moorestech_web/webui/src/bridge/contract/wireContract.test.ts
git commit -m "test(webui): build_menu/modal_input の共有wireフィクスチャを追加"
```

---

### Task 3: 画面ルーティングとレイヤー拡張

**Files:**
- Modify: `moorestech_web/webui/src/shared/uiState/uiScreenRouting.ts`
- Modify: `moorestech_web/webui/src/shared/uiState/activeLayer.ts`
- Test: `moorestech_web/webui/src/shared/uiState/uiScreenRouting.test.ts`
- Test: `moorestech_web/webui/src/shared/uiState/activeLayer.test.ts`（存在する場合は追記、無ければ新規作成）

**Interfaces:**
- Consumes: Task 1 の `UiStateNames.buildMenu`
- Produces: `UiScreen` に `"buildMenu"`、`ActiveLayer` に `"buildMenu"`。Task 12 の App.tsx 配線が `screen === "buildMenu"` を使う。

- [ ] **Step 1: 失敗するテストを追加**

`uiScreenRouting.test.ts` に追加:

```ts
  it("BuildMenu は buildMenu 画面に解決される", () => {
    expect(screenForUiState("BuildMenu")).toBe("buildMenu");
  });
```

`activeLayer` のテスト（既存テストファイルがあれば追記、無ければ `activeLayer.test.ts` を新規作成）:

```ts
import { describe, expect, it } from "vitest";
import { deriveActiveLayer } from "./activeLayer";

describe("deriveActiveLayer buildMenu", () => {
  it("buildMenu 中は game レイヤーにならない", () => {
    expect(deriveActiveLayer({ modalOpen: false, blockInventoryOpen: false, researchOpen: false, buildMenuOpen: true })).toBe("buildMenu");
  });
  it("modal は buildMenu より優先される", () => {
    expect(deriveActiveLayer({ modalOpen: true, blockInventoryOpen: false, researchOpen: false, buildMenuOpen: true })).toBe("modal");
  });
});
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/shared/uiState`
Expected: FAIL

- [ ] **Step 3: uiScreenRouting.ts を拡張**

```ts
export type UiScreen = "none" | "playerInventory" | "subInventory" | "researchTree" | "buildMenu";

export function screenForUiState(state: string | null): UiScreen {
  if (state === UiStateNames.playerInventory) return "playerInventory";
  if (state === UiStateNames.subInventory) return "subInventory";
  if (state === UiStateNames.researchTree) return "researchTree";
  if (state === UiStateNames.buildMenu) return "buildMenu";
  // GameScreen・未対応state・未受信はパネル無し（前方互換: 未知state名も安全側に倒す)
  // GameScreen, unsupported states and pre-snapshot are panel-less (forward-compat: unknown names fail safe)
  return "none";
}
```

- [ ] **Step 4: activeLayer.ts を拡張**

```ts
export type ActiveLayer = "modal" | "blockInventory" | "research" | "buildMenu" | "game";

export function deriveActiveLayer(input: { modalOpen: boolean; blockInventoryOpen: boolean; researchOpen: boolean; buildMenuOpen: boolean }): ActiveLayer {
  if (input.modalOpen) return "modal";
  if (input.blockInventoryOpen) return "blockInventory";
  if (input.researchOpen) return "research";
  if (input.buildMenuOpen) return "buildMenu";
  return "game";
}

export function readActiveLayer(): ActiveLayer {
  const modal = readTopic(Topics.modal);
  const block = readTopic(Topics.blockInventory);
  const uiState = readTopic(Topics.uiState);
  return deriveActiveLayer({
    modalOpen: modal?.modal != null,
    blockInventoryOpen: block?.open === true,
    researchOpen: uiState?.state === UiStateNames.researchTree,
    buildMenuOpen: uiState?.state === UiStateNames.buildMenu,
  });
}
```

注意: `deriveActiveLayer` の引数に `buildMenuOpen` が必須で増えるため、既存の呼び出し箇所（既存テスト含む）を全て `buildMenuOpen: false` 付きに更新する（デフォルト引数は規約で禁止。コンパイルエラー駆動で漏れを検出できる）。

- [ ] **Step 5: テストが通ることを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/shared/uiState`
Expected: PASS

Run: `cd moorestech_web/webui && pnpm exec tsc -b --force`
Expected: エラー0（deriveActiveLayer呼び出しの更新漏れがあればここで検出）

- [ ] **Step 6: Commit**

```bash
git add moorestech_web/webui/src/shared/uiState
git commit -m "feat(webui): BuildMenu画面ルーティングとbuildMenuレイヤーを追加"
```

---

### Task 4: WebUiModalService の入力モーダル対応（Unity）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiModalService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/ModalTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/ModalActions.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs`

**Interfaces:**
- Consumes: Task 2 の `modal_input.json`
- Produces: `WebUiModalService.RequestInputModal(string title, string message, string buttonText) : UniTask<(string result, string text)>`、`Respond(string id, string result, string text) : bool`（3引数に変更）、`CancelPendingRequest() : void`、`ModalRequest.RequiresInput : bool`。Task 8 のブリッジが依存する。

- [ ] **Step 1: WireContractTest に失敗するテストを追加**

`ModalOpenMatchesFixture` の下に追加:

```csharp
        // 入力モーダル: input:true が配信される（BP名入力等）
        // Input modal: input:true is delivered (e.g. blueprint naming)
        [Test]
        public void ModalInputMatchesFixture()
        {
            var dto = new ModalTopicDto
            {
                Modal = new ModalDto { Id = "m2", Title = "ブループリント名", Message = "保存するブループリントの名前を入力してください", ButtonText = "保存", Variant = "confirm", Input = true },
            };
            AssertMatchesFixture(dto, "modal_input.json");
        }
```

- [ ] **Step 2: コンパイルエラーで失敗することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー（`ModalDto.Input` が存在しない）

- [ ] **Step 3: WebUiModalService.cs を入力対応に書き換え**

保留ソースをタプル型に変更し、以下のpublic APIにする（クラスコメント・Instance・OnPendingChanged・_nextId等は現状維持）:

```csharp
        private string _pendingId;
        private UniTaskCompletionSource<(string result, string text)> _pendingSource;
        private int _nextId;

        // モーダルを Web に出して結果文字列（"confirm" | "cancel"）を待つ
        // Show a modal on the web and await the result string ("confirm" | "cancel")
        public async UniTask<string> RequestModal(string title, string message, string buttonText, ModalVariant variant)
        {
            var (result, _) = await RequestCore(title, message, buttonText, variant, false);
            return result;
        }

        // 入力付きモーダルを出して (結果, 入力テキスト) を待つ（キャンセル時 text は null）
        // Show an input modal and await (result, entered text); text is null on cancel
        public UniTask<(string result, string text)> RequestInputModal(string title, string message, string buttonText)
        {
            return RequestCore(title, message, buttonText, ModalVariant.Confirm, true);
        }

        private UniTask<(string result, string text)> RequestCore(string title, string message, string buttonText, ModalVariant variant, bool requiresInput)
        {
            // 既存の保留要求があれば cancel 扱いで解決し、最新要求のみを保持する
            // Resolve any existing pending request as cancel so only the latest request is kept
            _pendingSource?.TrySetResult(("cancel", null));

            _nextId++;
            _pendingId = _nextId.ToString();
            _pendingSource = new UniTaskCompletionSource<(string result, string text)>();

            Pending = new ModalRequest
            {
                Id = _pendingId,
                Title = title,
                Message = message,
                ButtonText = buttonText,
                Variant = variant,
                RequiresInput = requiresInput,
            };
            _onPendingChanged.OnNext(Unit.Default);

            return _pendingSource.Task;
        }

        // 保留中の要求だけを cancel 解決する（Instance は維持。ビュー側クローズ等の要求単位キャンセル用）
        // Cancel-resolve only the pending request, keeping Instance (per-request cancel, e.g. view closed)
        public void CancelPendingRequest()
        {
            if (_pendingSource == null && Pending == null) return;
            var source = _pendingSource;
            _pendingSource = null;
            _pendingId = null;
            Pending = null;
            _onPendingChanged.OnNext(Unit.Default);
            source?.TrySetResult(("cancel", null));
        }

        // バインド解除時に保留要求を cancel で解決し解決口を閉じる（await リーク防止）
        // On unbind, cancel-resolve the pending request and close the resolution point (prevents leaked awaits)
        public void CancelPending()
        {
            CancelPendingRequest();

            // 自分が現行 Instance なら破棄扱いにして解決口を閉じる
            // If this is the current Instance, treat it as disposed and close the resolution point
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        // Web からの応答。id 不一致は古い応答なので無視する
        // Reply from the web; ignore id mismatches as stale responses
        public bool Respond(string id, string result, string text)
        {
            if (_pendingSource == null || id == null || id != _pendingId) return false;

            var source = _pendingSource;
            _pendingSource = null;
            _pendingId = null;
            Pending = null;
            _onPendingChanged.OnNext(Unit.Default);

            source.TrySetResult((result, text));
            return true;
        }
```

`ModalRequest` クラスへ `public bool RequiresInput;` を追加。

- [ ] **Step 4: ModalTopic.cs に Input 配信を追加**

`ModalDto` へ `public bool? Input;` を追加し、`BuildJson` の `ModalDto` 生成に1行追加:

```csharp
                        // 入力モーダルのみ true を配信し、通常モーダルはキー省略する（既存フィクスチャ互換）
                        // Deliver true only for input modals; plain modals omit the key (keeps existing fixtures)
                        Input = pending.RequiresInput ? true : (bool?)null,
```

- [ ] **Step 5: ModalActions.cs に text 受領を追加**

`ExecuteAsync` の `result` 検証の後に追加し、`Respond` 呼び出しを3引数に変更:

```csharp
            // 入力モーダルの text は任意（無ければ null）
            // The input modal's text is optional (null when absent)
            var text = payload["text"] is JValue { Type: JTokenType.String } textValue ? (string)textValue : null;

            // id 不一致・保留なしは古い応答なので no_pending_modal で返す
            // id mismatch or no pending request is a stale reply; report no_pending_modal
            if (!_service.Respond((string)idValue, result, text)) return UniTask.FromResult(ActionResult.Fail("no_pending_modal"));
```

- [ ] **Step 6: コンパイルとテストが通ることを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractTest"`
Expected: 全件PASS（`ModalInputMatchesFixture` 含む。`modal_open.json` は `Input=null` キー省略で従来どおり一致）

- [ ] **Step 7: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(webui-host): WebUiModalServiceに入力モーダル対応を追加"
```

---

### Task 5: Client.Game側の受け口整備（BPライブラリ通知・選択投入・名前入力公開）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/ClientBlueprintLibrary.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Blueprint/BlueprintNameInputView.cs`

**Interfaces:**
- Produces:
  - `ClientBlueprintLibrary.OnChanged : IObservable<Unit>`（キャッシュ更新成功時に発火）
  - `BuildMenuView.SetSelectedEntry(BuildMenuEntry entry) : void`（webからの選択を既存消費キューへ投入）
  - `BlueprintNameInputView.IsOpen : bool` / `OnOpenChanged : IObservable<bool>` / `SetConfirmFromWeb(string name) : void` / `SetCancelFromWeb() : void`
  - Task 6/7/8 が全てこれらに依存する。

- [ ] **Step 1: ClientBlueprintLibrary に OnChanged を追加**

`using UniRx;` を追加し、フィールドと公開プロパティを追加:

```csharp
        // キャッシュが最新全件に置き換わったら発火する（BuildMenuTopic の再配信トリガ）
        // Fires when the cache is replaced with a fresh full list (republish trigger for BuildMenuTopic)
        public IObservable<Unit> OnChanged => _onChanged;
        private readonly Subject<Unit> _onChanged = new();
```

`ApplyResponse` の `_blueprints.AddRange(response.Blueprints);` の後に:

```csharp
            _onChanged.OnNext(Unit.Default);
```

- [ ] **Step 2: BuildMenuView に選択投入とwebモード表示抑止を追加**

`using Client.Game.InGame.UI.UIState;` を追加。`SetActive` を以下へ書き換え（Internal region 含め既存ロジックは維持）:

```csharp
        public void SetActive(bool active)
        {
            // webモード中は置換済みビューとしてuGUI表示を抑止する（PlayerInventoryViewControllerと同型）
            // In web mode this is a replaced view, so suppress the uGUI visual (same as PlayerInventoryViewController)
            var visible = active && !WebUiScreenGate.IsWebUiMode;
            gameObject.SetActive(visible);
            if (!active) return;

            // 前回セッションの未消費クリックを破棄
            // Discard an unconsumed click from the previous session before showing
            _clickedEntry = null;

            // 非表示時はuGUIスロット構築とBP更新をスキップする（web側の更新はBuildMenuTopicが担う）
            // Skip uGUI slot building and the BP refresh while hidden (BuildMenuTopic handles the web-side refresh)
            if (!visible) return;

            // キャッシュで即表示後、BP更新時に再構築
            // Show the cached list immediately, then rebuild after the BP library refresh only if needed
            RebuildEntryList();
            RefreshBlueprintsAndRebuild().Forget();

            #region Internal
            // （既存の RefreshBlueprintsAndRebuild をそのまま維持）
            #endregion
        }

        // webからの選択をuGUIクリックと同じ消費キューへ投入する
        // Feed a web selection into the same consume queue as uGUI clicks
        public void SetSelectedEntry(BuildMenuEntry entry)
        {
            _clickedEntry = entry;
        }
```

- [ ] **Step 3: BlueprintNameInputView に開閉公開とweb応答口を追加**

以下のメンバーを追加し、`Open`/`Close` を書き換え:

```csharp
        // 開閉状態（webブリッジの購読用。ビューが状態権威）
        // Open/close state (subscribed by the web bridge; this view owns the state)
        public bool IsOpen { get; private set; }
        public IObservable<bool> OnOpenChanged => _onOpenChanged;
        private readonly Subject<bool> _onOpenChanged = new();

        public void Open()
        {
            nameInputField.text = "";
            IsOpen = true;

            // webモード中は置換済みビューとしてuGUI表示を抑止する（状態と通知は維持）
            // In web mode suppress the uGUI visual as a replaced view (state and notifications stay live)
            var visible = !WebUiScreenGate.IsWebUiMode;
            gameObject.SetActive(visible);
            if (visible) nameInputField.ActivateInputField();

            _onOpenChanged.OnNext(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (!IsOpen) return;
            IsOpen = false;
            _onOpenChanged.OnNext(false);
        }

        // webモーダルからの確定（uGUIボタンと同一の空白検証・Trim・通知経路）
        // Confirm from the web modal (same whitespace validation, trim, and notification path as the uGUI button)
        public void SetConfirmFromWeb(string name)
        {
            if (!IsOpen) return;
            if (string.IsNullOrWhiteSpace(name)) return;
            _onConfirm.OnNext(name.Trim());
            Close();
        }

        // webモーダルからのキャンセル
        // Cancel from the web modal
        public void SetCancelFromWeb()
        {
            if (!IsOpen) return;
            _onCancel.OnNext(Unit.Default);
            Close();
        }
```

`using Client.Game.InGame.UI.UIState;` を追加。

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 5: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game
git commit -m "feat(client): BP周りにwebブリッジ受け口を追加（OnChanged/SetSelectedEntry/名前入力公開）"
```

---

### Task 6: BuildMenuTopic とDTOファクトリ（Unity）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuDtos.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs`

**Interfaces:**
- Consumes: Task 5 の `ClientBlueprintLibrary.OnChanged`、既存 `BuildMenuEntryCatalog.CreateEntries(IGameUnlockStateData, ClientBlueprintLibrary)`、`UIStateControl.OnStateChanged`
- Produces: `BuildMenuTopic`（TopicName `"build_menu.entries"`、ctor `(WebSocketHub, UIStateControl, IGameUnlockStateData, ClientBlueprintLibrary)`）、`BuildMenuEntryDtoFactory.CreateDtos(...) : List<BuildMenuEntryDto>` と `GetEntryTypeName(PlacementSelectionType) : string` / `GetEntryKey(BuildMenuEntry) : string`（Task 7 のselect照合が再利用）

- [ ] **Step 1: WireContractTest に失敗するテストを追加**

```csharp
        // ビルドメニュー: 全エントリ種別とアイコンURL省略の正準形
        // Build menu: the canonical form covering every entry type and icon-url omission
        [Test]
        public void BuildMenuMatchesFixture()
        {
            var dto = new BuildMenuTopicDto
            {
                Entries = new List<BuildMenuEntryDto>
                {
                    new() { EntryType = "block", EntryKey = "1", Label = "鉄の機械", Tooltip = "鉄の機械\n鉄インゴット x5", IconUrl = "/api/block-icons/1.png" },
                    new() { EntryType = "trainCar", EntryKey = "11111111-2222-3333-4444-555555555555", Label = "貨物車", Tooltip = "貨物車", IconUrl = "/api/train-car-icons/11111111-2222-3333-4444-555555555555.png" },
                    new() { EntryType = "connectTool", EntryKey = "BeltConveyor", Label = "ベルトコンベア", Tooltip = "ベルトコンベア", IconUrl = "/api/icons/3.png" },
                    new() { EntryType = "blueprintCopy", EntryKey = "", Label = "ブループリントコピー", Tooltip = "ブループリントコピー" },
                    new() { EntryType = "blueprint", EntryKey = "家", Label = "家", Tooltip = "家" },
                },
            };
            AssertMatchesFixture(dto, "build_menu_snapshot.json");
        }
```

（`using Client.WebUiHost.Game.Topics.BuildMenu;` と `using System.Collections.Generic;` を必要に応じ追加）

- [ ] **Step 2: コンパイルエラーで失敗することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー（`BuildMenuTopicDto` 未定義）

- [ ] **Step 3: BuildMenuDtos.cs を作成**

```csharp
using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// build_menu.entries の配信 DTO
    /// Payload DTOs for build_menu.entries
    /// </summary>
    public class BuildMenuTopicDto
    {
        public List<BuildMenuEntryDto> Entries;
    }

    public class BuildMenuEntryDto
    {
        public string EntryType;
        public string EntryKey;
        public string Label;
        public string Tooltip;
        // アイコン無し（BP・BPコピー）は null でキー省略される
        // Null (thus key-omitted) for icon-less entries: blueprints and the copy tool
        public string IconUrl;
    }
}
```

- [ ] **Step 4: BuildMenuEntryDtoFactory.cs を作成**

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Core.Master;
using Game.UnlockState;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// BuildMenuEntryCatalog の合成結果を web 配信用 DTO へ変換する
    /// Converts the BuildMenuEntryCatalog composition into web-delivery DTOs
    /// </summary>
    public static class BuildMenuEntryDtoFactory
    {
        public static List<BuildMenuEntryDto> CreateDtos(IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary)
        {
            var dtos = new List<BuildMenuEntryDto>();
            foreach (var entry in BuildMenuEntryCatalog.CreateEntries(unlockState, blueprintLibrary))
            {
                dtos.Add(new BuildMenuEntryDto
                {
                    EntryType = GetEntryTypeName(entry.EntryType),
                    EntryKey = GetEntryKey(entry),
                    // ラベルはツールチップ1行目（ブロック名等）を使う
                    // The label is the tooltip's first line (block name etc.)
                    Label = entry.ToolTipText.Split('\n')[0],
                    Tooltip = entry.ToolTipText,
                    IconUrl = CreateIconUrl(entry),
                });
            }
            return dtos;
        }

        // web契約の entryType 文字列（select アクションの照合と共有する）
        // The web-contract entryType string (shared with the select action's matching)
        public static string GetEntryTypeName(PlacementSelectionType type)
        {
            return type switch
            {
                PlacementSelectionType.Block => "block",
                PlacementSelectionType.TrainCar => "trainCar",
                PlacementSelectionType.ConnectTool => "connectTool",
                PlacementSelectionType.BlueprintCopy => "blueprintCopy",
                PlacementSelectionType.Blueprint => "blueprint",
                _ => type.ToString(),
            };
        }

        // 種別ごとの安定キー（配列indexは再配信でずれるため使わない）
        // Stable key per type (array indices shift across republishes, so they are never used)
        public static string GetEntryKey(BuildMenuEntry entry)
        {
            return entry.EntryType switch
            {
                PlacementSelectionType.Block => entry.BlockId.AsPrimitive().ToString(),
                PlacementSelectionType.TrainCar => entry.TrainCarGuid.ToString(),
                PlacementSelectionType.ConnectTool => entry.ConnectPlaceMode,
                PlacementSelectionType.Blueprint => entry.BlueprintName,
                _ => string.Empty,
            };
        }

        private static string CreateIconUrl(BuildMenuEntry entry)
        {
            switch (entry.EntryType)
            {
                case PlacementSelectionType.Block:
                    return $"{BlockIconEndpoint.PathPrefix}{entry.BlockId.AsPrimitive()}{BlockIconEndpoint.PathSuffix}";
                case PlacementSelectionType.TrainCar:
                    return $"{TrainCarIconEndpoint.PathPrefix}{entry.TrainCarGuid}{TrainCarIconEndpoint.PathSuffix}";
                case PlacementSelectionType.ConnectTool:
                {
                    // 接続ツールはマスタの IconItemGuid からアイテムアイコンを引く
                    // Connect tools resolve their icon from the master's IconItemGuid
                    var tool = MasterHolder.PlaceSystemMaster.PlaceSystem.Data.First(t => t.PlaceMode == entry.ConnectPlaceMode);
                    var itemId = MasterHolder.ItemMaster.GetItemId(tool.IconItemGuid.Value);
                    return $"{ItemIconEndpoint.PathPrefix}{itemId.AsPrimitive()}{ItemIconEndpoint.PathSuffix}";
                }
                default:
                    return null;
            }
        }
    }
}
```

注意: `TrainCarIconEndpoint` は Task 9 で作成するが、コンパイルを通すため本タスクでは先に Task 9 の Step 3 のファイルを作成してもよい（実装順の都合で Task 9 を先行実行しても依存は壊れない）。単独で進める場合は一時的に文字列リテラル `"/api/train-car-icons/"` を使わず、**Task 9 を先に実施すること**。

- [ ] **Step 5: BuildMenuTopic.cs を作成**

```csharp
using System;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using UniRx;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// build_menu.entries トピック: ビルドメニューのエントリ一覧を push
    /// build_menu.entries topic: pushes the build-menu entry list
    /// </summary>
    public class BuildMenuTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "build_menu.entries";

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private readonly IGameUnlockStateData _unlockState;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly IDisposable _librarySubscription;
        private bool _publishScheduled;
        private bool _disposed;

        public BuildMenuTopic(WebSocketHub hub, UIStateControl uiStateControl, IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _unlockState = unlockState;
            _blueprintLibrary = blueprintLibrary;

            // BuildMenu入場で再配信、BPライブラリ更新でも再配信する
            // Republish on BuildMenu entry and on blueprint-library updates
            _uiStateControl.OnStateChanged += OnStateChanged;
            _librarySubscription = _blueprintLibrary.OnChanged.Subscribe(_ => SchedulePublish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _uiStateControl.OnStateChanged -= OnStateChanged;
            _librarySubscription.Dispose();
        }

        private void OnStateChanged(UIStateEnum state)
        {
            if (state != UIStateEnum.BuildMenu) return;

            // uGUIビュー非表示時のBP更新はここが担う（更新完了は OnChanged 経由で再配信される）
            // While the uGUI view is hidden, this refresh path keeps blueprints fresh (completion republishes via OnChanged)
            _blueprintLibrary.Refresh(CancellationToken.None).Forget();
            SchedulePublish();
        }

        // INFRA-7 デバウンス規約: 同フレーム多発でもフレーム末の最終状態だけ配信する
        // INFRA-7 debounce rule: publish only the frame-end final state even on same-frame bursts
        private void SchedulePublish()
        {
            if (_publishScheduled) return;
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();

            #region Internal

            async UniTaskVoid PublishAtEndOfFrame()
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                _publishScheduled = false;
                if (_disposed) return;
                _hub.Publish(TopicName, BuildJson());
            }

            #endregion
        }

        private string BuildJson()
        {
            var dto = new BuildMenuTopicDto { Entries = BuildMenuEntryDtoFactory.CreateDtos(_unlockState, _blueprintLibrary) };
            return WebUiJson.Serialize(dto);
        }
    }
}
```

- [ ] **Step 6: コンパイルとテストが通ることを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（Task 9 を先行実施していること）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractTest"`
Expected: 全件PASS

- [ ] **Step 7: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(webui-host): build_menu.entriesトピックとDTOファクトリを追加"
```

---

### Task 7: build_menu.select / blueprint.delete アクション（Unity）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BuildMenuActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/error_codes.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs`（エラーコード正準セット）

**Interfaces:**
- Consumes: Task 5 の `BuildMenuView.SetSelectedEntry`、Task 6 の `BuildMenuEntryDtoFactory.GetEntryTypeName/GetEntryKey`、既存 `UIStateControl.CurrentState`
- Produces: `BuildMenuSelectActionHandler`（ctor `(UIStateControl, IGameUnlockStateData, ClientBlueprintLibrary, BuildMenuView)`）、`BlueprintDeleteActionHandler`（ctor `(ClientBlueprintLibrary)`）。Task 10 が登録する。エラーコード `"unknown_entry"` を新設。

- [ ] **Step 1: error_codes.json と WireContractTest の正準セットに "unknown_entry" を追加**

`error_codes.json` の `"codes"` 配列末尾に `"unknown_entry"` を追加。`WireContractTest.cs` の `expected` HashSet にも `"unknown_entry",` を追加。

- [ ] **Step 2: テストが通ることを確認（両側同時更新のため通る）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractTest"`
Expected: PASS

Run: `cd moorestech_web/webui && pnpm exec vitest run src/bridge/contract/wireContract.test.ts`
Expected: PASS

- [ ] **Step 3: BuildMenuActions.cs を作成**

```csharp
using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// build_menu.select: web の選択を uGUI の選択消費キューへ投入する
    /// build_menu.select: feeds a web selection into the uGUI consume queue
    /// </summary>
    public class BuildMenuSelectActionHandler : IActionHandler
    {
        public string ActionType => "build_menu.select";

        private readonly UIStateControl _uiStateControl;
        private readonly IGameUnlockStateData _unlockState;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly BuildMenuView _buildMenuView;

        public BuildMenuSelectActionHandler(UIStateControl uiStateControl, IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary, BuildMenuView buildMenuView)
        {
            _uiStateControl = uiStateControl;
            _unlockState = unlockState;
            _blueprintLibrary = blueprintLibrary;
            _buildMenuView = buildMenuView;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["entryType"] is not JValue { Type: JTokenType.String } typeValue) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["entryKey"] is not JValue { Type: JTokenType.String } keyValue) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // BuildMenu以外でのstale到達（Unity側が先に閉じたレース）は拒否する
            // Reject stale arrivals outside BuildMenu (the Unity side closed the menu first)
            if (_uiStateControl.CurrentState != UIStateEnum.BuildMenu) return UniTask.FromResult(ActionResult.Fail("invalid_state"));

            // 現在のカタログと種別+キーで照合し、削除済みBP等へのstaleクリックを弾く
            // Match against the current catalog by type+key, rejecting stale clicks (e.g. deleted blueprints)
            var entryType = (string)typeValue;
            var entryKey = (string)keyValue;
            var entries = BuildMenuEntryCatalog.CreateEntries(_unlockState, _blueprintLibrary);
            var matched = entries.Where(e => BuildMenuEntryDtoFactory.GetEntryTypeName(e.EntryType) == entryType && BuildMenuEntryDtoFactory.GetEntryKey(e) == entryKey).ToList();
            if (matched.Count == 0) return UniTask.FromResult(ActionResult.Fail("unknown_entry"));

            _buildMenuView.SetSelectedEntry(matched[0]);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// blueprint.delete: BPをサーバーから削除する（成功時は OnChanged 経由で一覧が再配信される）
    /// blueprint.delete: deletes a blueprint on the server (success republishes the list via OnChanged)
    /// </summary>
    public class BlueprintDeleteActionHandler : IActionHandler
    {
        public string ActionType => "blueprint.delete";

        private readonly ClientBlueprintLibrary _blueprintLibrary;

        public BlueprintDeleteActionHandler(ClientBlueprintLibrary blueprintLibrary)
        {
            _blueprintLibrary = blueprintLibrary;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["name"] is not JValue { Type: JTokenType.String } nameValue) return ActionResult.Fail("invalid_payload");

            await _blueprintLibrary.DeleteBlueprint((string)nameValue, CancellationToken.None);
            return ActionResult.Success();
        }
    }
}
```

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 5: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(webui-host): build_menu.select/blueprint.deleteアクションを追加"
```

---

### Task 8: BlueprintNameInputWebBridge（Unity）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/BlueprintNameInputWebBridge.cs`

**Interfaces:**
- Consumes: Task 4 の `WebUiModalService.RequestInputModal`/`CancelPendingRequest`、Task 5 の `BlueprintNameInputView.OnOpenChanged`/`SetConfirmFromWeb`/`SetCancelFromWeb`、既存 `WebUiScreenGate.IsWebUiMode`
- Produces: `BlueprintNameInputWebBridge(BlueprintNameInputView, WebUiModalService)`。Task 10 が生成する。

- [ ] **Step 1: BlueprintNameInputWebBridge.cs を作成**

```csharp
using System;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// BP名入力ビューの開閉を購読し、webモード時は入力モーダルへ転送して応答をビューに書き戻すブリッジ。
    /// 状態権威はビュー側（Client.Game）のまま。uGUIモード時は何もしない。
    /// Bridges the blueprint-name view to the web input modal in web mode, writing the reply back to the view.
    /// The view (Client.Game) stays the state authority; in uGUI mode this does nothing.
    /// </summary>
    public class BlueprintNameInputWebBridge : IDisposable
    {
        private readonly BlueprintNameInputView _view;
        private readonly WebUiModalService _modalService;
        private readonly IDisposable _subscription;

        public BlueprintNameInputWebBridge(BlueprintNameInputView view, WebUiModalService modalService)
        {
            _view = view;
            _modalService = modalService;
            _subscription = _view.OnOpenChanged.Subscribe(OnOpenChanged);
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void OnOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                // uGUIモードはuGUIダイアログが表示されるため転送しない
                // In uGUI mode the uGUI dialog is visible, so nothing is forwarded
                if (!WebUiScreenGate.IsWebUiMode) return;
                RequestAndRespond().Forget();
                return;
            }

            // ビュー側クローズ（確定/キャンセル/Disable）でwebモーダルも畳む（解決済みならno-op）
            // View-side close (confirm/cancel/Disable) also dismisses the web modal (no-op when already resolved)
            _modalService.CancelPendingRequest();

            #region Internal

            async UniTaskVoid RequestAndRespond()
            {
                var (result, text) = await _modalService.RequestInputModal("ブループリント名", "保存するブループリントの名前を入力してください", "保存");

                // 確定は空白のみを弾いてビューへ書き戻す（web側でも確定無効化済みの二重防御）
                // Confirm rejects whitespace-only before writing back (double guard; the web disables confirm too)
                if (result == "confirm" && !string.IsNullOrWhiteSpace(text)) _view.SetConfirmFromWeb(text);
                else _view.SetCancelFromWeb();
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost
git commit -m "feat(webui-host): BP名入力のwebモーダルブリッジを追加"
```

---

### Task 9: TrainCarIconEndpoint とルート追加（Unity）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/TrainCarIconEndpoint.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs`

**Interfaces:**
- Consumes: 既存 `ClientContext.TrainCarImageContainer.GetTrainCarView(Guid)`
- Produces: `GET /api/train-car-icons/{guid}.png`。`TrainCarIconEndpoint.PathPrefix`/`PathSuffix` 定数（Task 6 のファクトリが参照）

- [ ] **Step 1: TrainCarIconEndpoint.cs を作成（BlockIconEndpoint と同型）**

```csharp
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UnityEngine;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// 車両Guidの画像をPNG配信する
    /// Serves train-car images (keyed by Guid) as PNG
    /// </summary>
    public static class TrainCarIconEndpoint
    {
        public const string PathPrefix = "/api/train-car-icons/";
        public const string PathSuffix = ".png";

        private static readonly ConcurrentDictionary<Guid, CachedIcon> _pngCache = new();

        // PNG とその ETag（内容ハッシュ）をペアで保持する
        // Holds a PNG together with its content-hash ETag
        private readonly struct CachedIcon
        {
            public readonly byte[] Png;
            public readonly string ETag;

            public CachedIcon(byte[] png, string etag)
            {
                Png = png;
                ETag = etag;
            }
        }

        public static void ClearCache()
        {
            _pngCache.Clear();
        }

        public static async Task HandleAsync(HttpContext context, string path)
        {
            var guidText = path.Substring(PathPrefix.Length, path.Length - PathPrefix.Length - PathSuffix.Length);
            if (!Guid.TryParse(guidText, out var trainCarGuid))
            {
                context.Response.StatusCode = 404;
                return;
            }

            // ゲーム起動完了前は TrainCarImageContainer が未生成のため 503
            // TrainCarImageContainer is not yet created before game startup; return 503
            if (ClientContext.TrainCarImageContainer == null)
            {
                context.Response.StatusCode = 503;
                return;
            }

            if (!_pngCache.TryGetValue(trainCarGuid, out var cached))
            {
                var png = await EncodePngOnMainThread();
                if (png == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                var etag = "\"" + Convert.ToBase64String(MD5.Create().ComputeHash(png)) + "\"";
                cached = new CachedIcon(png, etag);
                _pngCache[trainCarGuid] = cached;
            }

            // アイコン内容の変化に ETag 再検証で追随する
            // Follow icon-content changes via ETag revalidation
            context.Response.Headers["ETag"] = cached.ETag;
            context.Response.Headers["Cache-Control"] = "no-cache";

            if (context.Request.Headers["If-None-Match"].ToString() == cached.ETag)
            {
                context.Response.StatusCode = 304;
                return;
            }

            context.Response.ContentType = "image/png";
            await context.Response.Body.WriteAsync(cached.Png, 0, cached.Png.Length);

            #region Internal

            async UniTask<byte[]> EncodePngOnMainThread()
            {
                // EncodeToPNG は Unity API のためメインスレッドで実行する
                // EncodeToPNG is a Unity API and must run on the main thread
                await UniTask.SwitchToMainThread();
                var view = ClientContext.TrainCarImageContainer.GetTrainCarView(trainCarGuid);
                var png = view?.ItemTexture is Texture2D texture ? texture.EncodeToPNG() : null;
                await UniTask.SwitchToTaskPool();
                return png;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: WebUiEndpoints.cs にルートを追加**

`BlockIconEndpoint` のルートブロックの直後に追加:

```csharp
                if (path.StartsWith(Game.TrainCarIconEndpoint.PathPrefix, StringComparison.Ordinal) && path.EndsWith(Game.TrainCarIconEndpoint.PathSuffix, StringComparison.Ordinal))
                {
                    // 車両アイコンの PNG 配信
                    // Serve train-car icon PNGs
                    await Game.TrainCarIconEndpoint.HandleAsync(context, path);
                    return;
                }
```

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost
git commit -m "feat(webui-host): 車両アイコン配信エンドポイントを追加"
```

---

### Task 10: WebUiGameBinder 配線（Unity）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`

**Interfaces:**
- Consumes: Task 6 の `BuildMenuTopic`、Task 7 の両アクション、Task 8 のブリッジ。`BuildMenuView`/`BlueprintNameInputView`/`ClientBlueprintLibrary` はVContainer登録済み（`MainGameStarter.cs:274-280` の `RegisterComponent` 群と `ClientBlueprintLibrary` のDI登録）

- [ ] **Step 1: Bind() に登録を追加**

using に `Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint` / `Client.Game.InGame.UI.Blueprint` / `Client.Game.InGame.UI.BuildMenu` / `Client.WebUiHost.Game.Topics.BuildMenu` を追加。研究トピック登録の後（actionハンドラ登録の前）に追加:

```csharp
            // ビルドメニュートピックを登録（BP名入力ブリッジも同時に張る）
            // Register the build-menu topic (also wires the blueprint-name input bridge)
            var blueprintLibrary = resolver.Resolve<ClientBlueprintLibrary>();
            var buildMenuView = resolver.Resolve<BuildMenuView>();
            var blueprintNameInputView = resolver.Resolve<BlueprintNameInputView>();
            var buildMenuTopic = new BuildMenuTopic(hub, uiStateControl, unlockStateData, blueprintLibrary);
            hub.RegisterTopic(BuildMenuTopic.TopicName, buildMenuTopic);
            var blueprintNameInputBridge = new BlueprintNameInputWebBridge(blueprintNameInputView, modalService);
```

action ハンドラ登録群の末尾に追加:

```csharp
            hub.RegisterAction(new BuildMenuSelectActionHandler(uiStateControl, unlockStateData, blueprintLibrary, buildMenuView));
            hub.RegisterAction(new BlueprintDeleteActionHandler(blueprintLibrary));
```

注意: `unlockStateData` の resolve はこの位置より上（クラフトレシピトピック登録時）で行われているため再取得不要。`blueprintNameInputBridge` はhub寿命と同じくアプリセッション中生存でよい（既存topicも同様にDispose配線なし）。未使用変数警告が出る場合は `_ = blueprintNameInputBridge;` とせず、変数宣言を `new BlueprintNameInputWebBridge(...)` の式文にしてよい（Disposeは既存topic同様プロセス終了任せ）。

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost
git commit -m "feat(webui-host): BuildMenuトピック・アクション・BP名入力ブリッジを配線"
```

---

### Task 11: ModalHost の入力バリアント（Web）

**Files:**
- Modify: `moorestech_web/webui/src/features/modal/ModalHost.tsx`
- Modify: `moorestech_web/webui/src/features/modal/modalLogic.ts`
- Test: `moorestech_web/webui/src/features/modal/modalLogic.test.ts`

**Interfaces:**
- Consumes: Task 1 の `ModalRequest.input?` と `"ui.modal.respond"` の `text?`
- Produces: input付きモーダルの描画（TextInput + 空白のみ確定不可）。`respondPayload(id, result, text?)` / `canConfirm(input, text)`

- [ ] **Step 1: modalLogic.test.ts に失敗するテストを追加**

```ts
describe("respondPayload with text", () => {
  it("text 付き confirm を組み立てる", () => {
    expect(respondPayload("m2", "confirm", "家")).toEqual({ id: "m2", result: "confirm", text: "家" });
  });
  it("text 省略時は text キーを含めない", () => {
    expect(respondPayload("m1", "cancel")).toEqual({ id: "m1", result: "cancel" });
  });
});

describe("canConfirm", () => {
  it("非inputモーダルは常に確定可", () => {
    expect(canConfirm(undefined, "")).toBe(true);
  });
  it("inputモーダルは空白のみを確定不可にする", () => {
    expect(canConfirm(true, "   ")).toBe(false);
    expect(canConfirm(true, " 家 ")).toBe(true);
  });
});
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/features/modal`
Expected: FAIL（`canConfirm` 未定義）

- [ ] **Step 3: modalLogic.ts を拡張**

```ts
// modal 応答の action payload を組み立てる純関数。text は input モーダルの確定時のみ付与する。
// Pure builder for the modal-response payload; text accompanies only an input modal's confirm.
export function respondPayload(
  id: string,
  result: "confirm" | "cancel",
  text?: string,
): ActionPayloads["ui.modal.respond"] {
  return text === undefined ? { id, result } : { id, result, text };
}

// 入力必須モーダルは空白のみを確定不可にする（uGUI BlueprintNameInputView と同一検証）
// Input-required modals reject whitespace-only text (same validation as uGUI BlueprintNameInputView)
export function canConfirm(input: boolean | undefined, text: string): boolean {
  if (!input) return true;
  return text.trim().length > 0;
}
```

- [ ] **Step 4: ModalHost.tsx を入力対応に書き換え**

```tsx
import { useState } from "react";
import { Button, Modal, Text, TextInput } from "@mantine/core";
import { useTopic, dispatchAction, Topics } from "@/bridge";
import type { ModalRequest } from "@/bridge/contract/payloadTypes";
import { respondPayload, buttonColor, canConfirm } from "./modalLogic";

// uGUI OneButtonModal の web 版。ui.modal トピックを購読し、要求があれば中央モーダルを描く。
// Web version of uGUI OneButtonModal; subscribes ui.modal and renders a centered modal on request.
export function ModalHost() {
  const data = useTopic(Topics.modal);

  // スナップショット未着、または表示対象が無ければ何も描かない。
  // Render nothing before the first snapshot or when there is no modal to show.
  if (!data || !data.modal) return null;

  // id を key にして要求ごとに入力状態をリセットする。
  // Keying by id resets the input state per request.
  return <ModalBody key={data.modal.id} modal={data.modal} />;
}

function ModalBody({ modal }: { modal: ModalRequest }) {
  const { id, title, message, buttonText, variant, input } = modal;
  const [text, setText] = useState("");

  // confirm/cancel を host へ送る。input モーダルの confirm は Trim 済み text を同送する。
  // Send confirm/cancel to the host; an input modal's confirm carries the trimmed text.
  const confirm = () => dispatchAction("ui.modal.respond", respondPayload(id, "confirm", input ? text.trim() : undefined));
  const cancel = () => dispatchAction("ui.modal.respond", respondPayload(id, "cancel"));

  // e2e が同期検証できるようトランジションは無効化する。
  // Disable transitions so e2e can assert synchronously.
  return (
    <Modal.Root opened onClose={() => void cancel()} centered transitionProps={{ duration: 0 }}>
      <Modal.Overlay data-testid="modal-backdrop" backgroundOpacity={0.6} />
      <Modal.Content data-testid="modal" w={320}>
        <Modal.Header>
          <Modal.Title fz="h4" fw={700}>{title}</Modal.Title>
        </Modal.Header>
        <Modal.Body p="lg">
          <Text size="sm" c="dimmed" mb="lg">{message}</Text>
          {input ? (
            <TextInput data-testid="modal-input" value={text} onChange={(e) => setText(e.currentTarget.value)} mb="lg" autoFocus />
          ) : null}
          <Button data-testid="modal-button" fullWidth color={buttonColor(variant)} disabled={!canConfirm(input, text)} onClick={() => void confirm()}>
            {buttonText}
          </Button>
        </Modal.Body>
      </Modal.Content>
    </Modal.Root>
  );
}
```

- [ ] **Step 5: テストが通ることを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/features/modal`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add moorestech_web/webui/src/features/modal
git commit -m "feat(webui): モーダルに入力バリアントを追加（BP名入力用）"
```

---

### Task 12: features/buildMenu と App 配線（Web）

**Files:**
- Create: `moorestech_web/webui/src/features/buildMenu/BuildMenuPanel.tsx`
- Create: `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.tsx`
- Create: `moorestech_web/webui/src/features/buildMenu/buildMenuLogic.ts`
- Create: `moorestech_web/webui/src/features/buildMenu/style.module.css`
- Create: `moorestech_web/webui/src/features/buildMenu/index.ts`
- Test: `moorestech_web/webui/src/features/buildMenu/buildMenuLogic.test.ts`
- Modify: `moorestech_web/webui/src/app/App.tsx`

**Interfaces:**
- Consumes: Task 1 の契約、Task 3 の `screen === "buildMenu"`、既存 `SlotGrid`・`useTopic`・`dispatchAction`・`UiStateNames`
- Produces: `BuildMenuPanel`（buildMenu画面のルート）

- [ ] **Step 1: buildMenuLogic.test.ts に失敗するテストを追加**

```ts
import { describe, expect, it } from "vitest";
import { selectPayload, deletePayload } from "./buildMenuLogic";

describe("buildMenuLogic", () => {
  it("selectPayload はエントリの種別とキーを写す", () => {
    const entry = { entryType: "blueprint" as const, entryKey: "家", label: "家", tooltip: "家" };
    expect(selectPayload(entry)).toEqual({ entryType: "blueprint", entryKey: "家" });
  });
  it("deletePayload はBP名を写す", () => {
    expect(deletePayload("家")).toEqual({ name: "家" });
  });
});
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/features/buildMenu`
Expected: FAIL（モジュール未作成）

- [ ] **Step 3: buildMenuLogic.ts を作成**

```ts
import type { BuildMenuEntryData } from "@/bridge/contract/payloadTypes";
import type { ActionPayloads } from "@/bridge";

// 選択アクションの payload を組み立てる純関数
// Pure builder for the select-action payload
export function selectPayload(entry: BuildMenuEntryData): ActionPayloads["build_menu.select"] {
  return { entryType: entry.entryType, entryKey: entry.entryKey };
}

// BP削除アクションの payload を組み立てる純関数
// Pure builder for the blueprint-delete payload
export function deletePayload(name: string): ActionPayloads["blueprint.delete"] {
  return { name };
}
```

- [ ] **Step 4: style.module.css を作成**

```css
.panel {
  position: fixed;
  inset: 8% 12%;
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  background: rgba(30, 30, 30, 0.92);
  border-radius: 8px;
  z-index: 100;
}

.scroll {
  overflow-y: auto;
}

.slot {
  width: 64px;
  height: 64px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid rgba(255, 255, 255, 0.15);
  border-radius: 4px;
  cursor: pointer;
  overflow: hidden;
}

.slot:hover {
  background: rgba(255, 255, 255, 0.15);
}

.icon {
  max-width: 100%;
  max-height: 100%;
}

.label {
  font-size: 10px;
  color: #eee;
  text-align: center;
  word-break: break-all;
  padding: 2px;
}

.tooltip {
  white-space: pre-line;
}
```

- [ ] **Step 5: BuildMenuSlot.tsx を作成**

```tsx
import type { MouseEvent } from "react";
import { Tooltip } from "@mantine/core";
import type { BuildMenuEntryData } from "@/bridge/contract/payloadTypes";
import styles from "./style.module.css";

type Props = {
  entry: BuildMenuEntryData;
  onLeftClick: () => void;
  // BPエントリのみ右クリック削除を受け付ける
  // Only blueprint entries accept right-click deletion
  onRightClick?: () => void;
};

// アイコン有無で画像/テキストを出し分けるビルドメニュー1スロット
// One build-menu slot, rendering an image or a text label depending on icon presence
export default function BuildMenuSlot({ entry, onLeftClick, onRightClick }: Props) {
  const onMouseDown = (e: MouseEvent) => {
    e.preventDefault();
    if (e.button === 0) onLeftClick();
    if (e.button === 2) onRightClick?.();
  };

  return (
    <Tooltip label={<span className={styles.tooltip}>{entry.tooltip}</span>}>
      <div
        className={styles.slot}
        data-testid={`build-menu-entry-${entry.entryType}-${entry.entryKey}`}
        onMouseDown={onMouseDown}
        onContextMenu={(e) => e.preventDefault()}
      >
        {entry.iconUrl ? (
          <img src={entry.iconUrl} alt={entry.label} className={styles.icon} draggable={false} />
        ) : (
          <span className={styles.label}>{entry.label}</span>
        )}
      </div>
    </Tooltip>
  );
}
```

- [ ] **Step 6: BuildMenuPanel.tsx を作成**

```tsx
import { CloseButton, Group, Title } from "@mantine/core";
import { useTopic, dispatchAction, Topics, UiStateNames } from "@/bridge";
import SlotGrid from "@/shared/ui/SlotGrid";
import BuildMenuSlot from "./BuildMenuSlot";
import { selectPayload, deletePayload } from "./buildMenuLogic";
import styles from "./style.module.css";

// uGUI BuildMenuView の web 版。エントリ選択は build_menu.select で Unity の消費キューへ届く。
// Web version of uGUI BuildMenuView; selections reach Unity's consume queue via build_menu.select.
export function BuildMenuPanel() {
  const data = useTopic(Topics.buildMenu);

  // 閉じるは既存許可済みの GameScreen 遷移要求（BlockInventoryPanel と同型）。B/ESC は Unity 側が処理する。
  // Close requests the already-allowed GameScreen transition (same as BlockInventoryPanel); B/ESC are handled by Unity.
  const close = () => void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });

  if (!data) return null;

  return (
    <div className={styles.panel} data-testid="build-menu-panel">
      <Group justify="space-between">
        <Title order={2} size="h4">ビルドメニュー</Title>
        <CloseButton data-testid="build-menu-close" onClick={close} />
      </Group>
      <div className={styles.scroll}>
        <SlotGrid cols={10} testId="build-menu-grid">
          {data.entries.map((entry) => (
            <BuildMenuSlot
              key={`${entry.entryType}:${entry.entryKey}`}
              entry={entry}
              onLeftClick={() => void dispatchAction("build_menu.select", selectPayload(entry))}
              onRightClick={
                entry.entryType === "blueprint"
                  ? () => void dispatchAction("blueprint.delete", deletePayload(entry.entryKey))
                  : undefined
              }
            />
          ))}
        </SlotGrid>
      </div>
    </div>
  );
}
```

- [ ] **Step 7: index.ts を作成し App.tsx に配線**

`index.ts`:

```ts
export { BuildMenuPanel } from "./BuildMenuPanel";
```

`App.tsx`: import に `import { BuildMenuPanel } from "@/features/buildMenu";` を追加し、`{screen === "researchTree" && <ResearchTreePanel />}` の下に:

```tsx
      {screen === "buildMenu" && <BuildMenuPanel />}
```

- [ ] **Step 8: テストと型チェックが通ることを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run`
Expected: 全件PASS

Run: `cd moorestech_web/webui && pnpm exec tsc -b --force`
Expected: エラー0

- [ ] **Step 9: Commit**

```bash
git add moorestech_web/webui/src/features/buildMenu moorestech_web/webui/src/app/App.tsx
git commit -m "feat(webui): ビルドメニューパネルを追加（選択・BP右クリック削除・閉じる）"
```

---

### Task 13: 統合検証

**Files:** なし（検証のみ。問題があれば該当タスクのファイルを修正）

- [ ] **Step 1: 全コンパイル・全テスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractTest"`
Expected: 全件PASS

Run: `cd moorestech_web/webui && pnpm exec vitest run`
Expected: 全件PASS

- [ ] **Step 2: 機能パリティ死活表の確認（コードレビューで確認）**

| 操作 | 計画後 | 根拠 |
|---|---|---|
| uGUIモード: Bキーでビルドメニュー表示・選択・BP削除・BP名入力 | 生存 | `BuildMenuView.SetActive` は非webモードで従来経路、`BlueprintNameInputView.Open` も同様 |
| uGUIモード: モーダル（OneButtonModal系） | 生存 | uGUI ModalManagerは無変更 |
| webモード: 既存モーダル（confirm/error） | 生存 | `ModalDto.Input` はnull省略で既存契約不変（`modal_open.json` 無変更で確認済み） |
| webモード: BP名入力中のESC/ステート離脱 | 生存 | `BlueprintCopySystem.Disable` → `Close()` → ブリッジが `CancelPendingRequest` |
| webモード: ホットバー1-9キーがBuildMenu中に発火しない | 生存 | Task 3 の buildMenu レイヤー追加 |

- [ ] **Step 3: 手動確認（Unity PlayMode + webモード）**

1. Unity起動 → ゲーム開始 → Ctrl+IでwebモードON（既定ON）
2. Bキー → webにビルドメニューパネルが出る（uGUIメニューは非表示）
3. ブロックエントリをクリック → PlaceBlockに遷移し設置できる
4. B → メニュー → 「ブループリントコピー」→ ドラッグ範囲選択 → 離すとweb入力モーダル → 空白では保存不可 → 名前入力し保存 → B → メニューにBPが並ぶ
5. BPエントリ左クリック → ペーストプレビュー表示・設置できる
6. BPエントリ右クリック → 即削除され一覧から消える
7. Ctrl+IでuGUIモード → 同フローがuGUIで従来どおり動く

- [ ] **Step 4: Commit（修正があれば）**

```bash
git add -A
git commit -m "fix(webui): ビルドメニュー/BP統合検証の修正"
```

---

## 実行順の注意

- Task 9（TrainCarIconEndpoint）は Task 6 のファクトリが定数参照するため、**Task 6 より先に実行してよい**（推奨順: 1→2→3→4→5→9→6→7→8→10→11→12→13）
- Task 1〜3（web契約系）と Task 4〜10（Unity側）は独立して進められるが、フィクスチャ（Task 2）は両側のテストが参照する
