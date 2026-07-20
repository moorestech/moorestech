# Web UI 対応プレイテスト: DOMクリック基盤 + デバッグ環境ワンライナー 実装計画

作成: 2026-07-19 / 対象ブランチ: web-ui

## 背景・問題

uGUI→Web UI (CEF + React) 移行により、プレイテストDSLのUIクリックが動かなくなった。

**根本原因**: CEFへのマウス転送を担う `CefUnityBrowserSample.HandleMouseInput()`
（`moorestech_client/Library/PackageCache/jp.juha.cefunity@c69a8610e457/Runtime/CefUnityBrowserSample.cs` 909行以降）が
**legacy `UnityEngine.Input` 直読み**（`Input.mousePosition` / `Input.GetMouseButtonDown` 等）のため、
プレイテストの `QueueStateEvent` 注入（InputSystem）がCEFブラウザに一切届かない。
旧uGUIビルドメニューは `ExecuteEvents` 直叩き（`PlaytestUiOps.TryClickBuildMenuSlot`）で回避していたが、React化で消滅した。

**採用方針**: パッケージ改変はしない（Interopに297MBのネイティブバイナリがありvendor化不可。上流PRは別途）。
代わりにプレイテスト専用の入力フォワーダを本体側に置き、
「DOM座標をJSクエリで自動取得 → InputSystem注入マウスを通常のセマンティック経路で動かす → フォワーダが実プレイヤーと同じ `SendMouseClick` ingress でCEFへ届ける」
という既存哲学（DOMへ直接clickコマンドを出さない）の延長で実装する。

## 実装コンポーネント（すべて新規 or 明示ファイルのみ変更）

### A. CEF座標マッパー `Client.Playtest/WebUi/CefScreenMapper.cs`（新規）

Unityスクリーン座標 ⇔ CEFブラウザ座標の双方向変換を一箇所に集約する静的クラス。

- 変換式は `CefUnityBrowserSample.TryGetBrowserCoord()`（同ファイル989-1016行）の再実装＋その逆関数:
  - screen→browser: `RectTransformUtility.ScreenPointToLocalPointInRectangle(rawImageRect, screenPos, cam)` → rect内0..1正規化 → **Y反転**（uvRect(0,1,1,-1)） → ×ブラウザ幅/高さ
  - browser→screen: 上記の逆（正規化→local→`rawImageRect.TransformPoint`→`RectTransformUtility.WorldToScreenPoint(cam, world)`）
  - camは `rawImage.canvas.renderMode == ScreenSpaceOverlay ? null : canvas.worldCamera`
- `CefUnityBrowserSample` インスタンスは `Object.FindFirstObjectByType` で取得しキャッシュ。
  RawImageは同GameObjectの `GetComponentInChildren<RawImage>(true)`、
  ブラウザ実サイズ（`_currentWidth`/`_currentHeight`）とブラウザ本体（`_browser`）は**privateフィールドのためリフレクション**で取得する
  （プレイテスト専用コードなので許容。フィールド名は上記実ファイルをReadして確認すること）。

### B. CEF入力フォワーダ `Client.Playtest/WebUi/CefInputForwarder.cs`（新規）

注入されたInputSystemマウス状態を毎フレームCEFへ転送するMonoBehaviour。

- `PlaytestRunner.Run` 開始時に生成（`CefUnityBrowserSample` が存在するときのみ）、シナリオ終了時に破棄
- `Update()` で `Mouse.current` を読む:
  - 位置が前フレームと変わったら `browser.SendMouseMove(bx, by)`（AのマッパーでUnityスクリーン→ブラウザ座標へ。RawImage外なら送らない）
  - `leftButton.wasPressedThisFrame` → `SendMouseClick(bx, by, Left, mouseUp:false, clickCount:1)`、
    `wasReleasedThisFrame` → 同 `mouseUp:true`。右・中ボタンも同様
  - `scroll.ReadValue()` が非零なら `SendMouseWheel`
- `SendMouseMove/Click/Wheel` は `CefUnity.Interop` の公開APIをそのまま使う（`CefUnity.cs` 339-362行）。
  ブラウザ座標のY軸は上原点（Y反転済みの値を渡す）
- プレイテスト中はEditor非フォーカスでOSマウスが動かないため、パッケージ側のlegacy転送との二重送信は実運用上発生しない
  （この前提を日本語/英語コメントで明記する）

### C. DOM矩形クエリ往復（Web側レスポンダ + Unity側クライアント）

DOM要素の画面上矩形をUnityから要求→ページ内JSが応答するWS往復。既存のWebSocketHub/Actionインフラをそのまま使う。

**Unity側 `Client.Playtest/WebUi/PlaytestDomQuery.cs`（新規）**
- 要求: `Client.WebUiHost.Boot.WebUiHost.Hub.Publish("playtest.dom_query", json)` で
  `{ requestId: <Guid文字列>, testid: "..." }` をevent配信（購読側にハンドラ未登録topicでもPublishは可能・snapshot不要）
- 応答受信: `IActionHandler`（`ActionType = "playtest.dom_query_result"`）を実装し
  `Hub.RegisterAction` で登録（プレイテスト開始時）。requestIdキーの pending Dictionary に結果を格納
- 公開API: `static async UniTask<DomQueryResult> Query(string testid, float timeoutSeconds)`
  — Publish→フレームポーリングで応答待ち→タイムアウトでfound=falseを返す（throwしない。呼び出し側のUntilリトライに任せる）
- `DomQueryResult`: found / x, y, width, height（CSS px・ページビューポート基準）/ devicePixelRatio / hitTestPassed
- ブラウザpx換算はCSS px × devicePixelRatio。Aのマッパーでブラウザ→Unityスクリーンへ変換するヘルパもここに置く

**Web側 `moorestech_web/webui/src/bridge/playtest/domQueryResponder.ts`（新規）+ 登録**
- 既存 `webSocketClient` の購読機構に乗せる。`protocol.ts` の `Topics` に `playtestDomQuery: "playtest.dom_query"` を追加し、
  レスポンダ初期化時に subscribe（実装は `subscriptionManager.ts` / `webSocketClient.ts` の既存パターンを読んで合わせる）
- event受信時: `document.querySelector('[data-testid="<testid>"]')` → `getBoundingClientRect()`、
  中心点で `document.elementFromPoint()` を実行し **クリックが本当にその要素（か子孫/祖先）に当たるか** を hitTestPassed として返す
  （モーダル等に覆われた要素への空クリック検出用）
- 応答: `sendAction("playtest.dom_query_result", { requestId, found, x, y, width, height, devicePixelRatio, hitTestPassed })`
- `main.tsx`（または適切な初期化地点）で登録。**本番動作に影響しない受動モジュール**とする（クリック・スクロール等のDOM操作は絶対にしない）

### D. Driver API 追加・改修

**`Client.Playtest/Operations/PlaytestWebUiOps.cs`（新規）** — C/Aを使う操作群:
- `UniTask<bool> TryResolveScreenPoint(string testid, out Vector2 screenPos)` 相当の内部処理
  （クエリ→hitTestPassed確認→矩形が2回連続一致するまで再クエリ（Reactレンダリング待ち）→ブラウザ→スクリーン変換）
- `UniTask ClickWebUi(string testid)` — 上記解決→`SemanticInput.MouseGlideTo(screenPos, 0.3f)`→`SemanticInput.Click()`。
  要素が見つかるまで内部リトライ（タイムアウト定数15秒、超過でTimeoutException）
- `UniTask HoverWebUi(string testid)` — 解決→MouseGlideToのみ
- `UniTask WaitWebUiElement(string testid, float timeoutSeconds)` — found && hitTestPassed になるまでポーリング

**`PlaytestDriver.cs`（変更）** — セマンティック窓口を追加（既存メソッドは削除しない）:
- `await p.ClickWebUi(string testid)` / `await p.HoverWebUi(string testid)` / `await p.UntilWebUiElement(string testid, float timeoutSeconds)`
  — いずれも `_reporter.Act`/`BeginWait` でオーバーレイ・0.5秒インターバルに乗せる
- **簡易ラッパー（1行呼び出し用）**:
  - `await p.ClickBuildMenuBlock(string blockName)` — `PlaytestBlockOps.ResolveBlockId` でBlockIdへ解決し
    testid `build-menu-entry-block-{blockId}` をClickWebUi（testid形式は `BuildMenuSlot.tsx` 24行 + `BuildMenuEntryDtoFactory.GetEntryKey` = BlockIdのint文字列）
  - `await p.CloseWebUiPanel()` — testid `build-menu-close` 等の共通閉じるボタン（実装前に該当testidを`moorestech_web/webui/src/features`でgrepして確認）

**`PlaytestUiOps.OpenBuildMenuAndSelectBlock`（改修）**
- メニューを開くキー操作（B/Tab・リトライ）は現状維持
- スロット選択を `TryClickBuildMenuSlot`（EventSystem直叩き）から **Web UI経路** に置換:
  `WaitWebUiElement("build-menu-panel")` → `ClickWebUi("build-menu-entry-block-{blockId}")` → PlaceBlock遷移待ち（既存ループ流用）
- `CefUnityBrowserSample` が存在しない場合（uGUIモード）は従来のEventSystem直叩きへフォールバックし、既存の挙動を保つ
- これにより既存の `*-via-ui.cs` シナリオは**無改変で**Web UIモードでも通る（互換維持が完了条件の一つ）

### E. デバッグ環境ワンライナー + コンフィグ

**`Client.Playtest/PlaytestEnvironmentConfig.cs`（新規）**
```csharp
public class PlaytestEnvironmentConfig
{
    // 建築を全解放し無料で設置できるようにする（ビルドメニュー全表示 + クライアント/サーバー両方のコスト消費スキップ）
    // Unlock everything and make placement free (full build menu + cost skip on both client and server)
    public bool FreeBlockPlacement = true;

    // y=32上面の平坦足場を生成する（無限落下防止・UI設置レイキャストの前提）
    // Create the flat scaffold with its top at y=32 (prevents infinite falls; required by UI-placement raycasts)
    public bool CreateFlatGround = true;

    // 初期ワープ先。CreateFlatGround時は足場中央上空がデフォルト
    // Initial warp position; defaults to just above the scaffold center when CreateFlatGround is on
    public Vector3 SpawnPosition = new(0f, 33.5f, 0f);
}
```
（単純getter/setterプロパティ禁止規約のため public フィールドで持つ。拡張時はここへフラグを足す）

**`PlaytestDriver.SetupDebugEnvironment(PlaytestEnvironmentConfig config)`（新規メソッド）** — 実処理は `PlaytestSetup` に追加:
1. **最初に全設定値をログへ流す**: `Debug.Log("[Playtest] env config: FreeBlockPlacement=..., CreateFlatGround=..., SpawnPosition=...")`
   ＋ 同内容を `p.Note` でオーバーレイ/Timelineにも残す
2. `DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, config.FreeBlockPlacement)` を**値がfalseでも必ず書く**
   （`../cache/BoolDebugParameters.json` は前回実行から持ち越されるため、明示上書きで残留を防ぐ）。
   サーバー（`PlaceBlockProtocol`）・クライアント（`CommonBlockPlaceSystem`・`BuildMenuEntryCatalog`）は毎回読み直すため再起動不要
3. `config.CreateFlatGround` なら既存 `PlaytestSetup.CreateFlatGround()` を実行
4. `config.SpawnPosition` へ `WarpPlayer`（サーバー同期込み・既存実装流用）＋着地数フレーム待ち

デフォルト引数は使わない（規約）。シナリオは常に `await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());` のように書く。

### F. 実証シナリオ `.claude/skills/unity-playmode-recorded-playtest/scenarios/webui-buildmenu-click.cs`（新規）

```
SetupDebugEnvironment（FreeBlockPlacement=true）
→ Bでビルドメニュー → p.UntilWebUiElement("build-menu-panel")
→ p.ClickBuildMenuBlock("ベルトコンベア") → PlaceBlock遷移assert
→ AimAtPlaceOrigin + ClickPlace で1個設置 → GetBlockでassert → スクショ
```
既存 `scenarios/belt-line-via-ui.cs` と `cef-webui-showcase.cs` を参考に、`p.Note` ナレーションを入れる。

## 制約・規約（違反するとレビューで差し戻す）

- リポジトリの `AGENTS.md` に全面的に従う。特に:
  - 1ファイル200行以内 / partial絶対禁止 / try-catch原則禁止（外部境界のみ・根拠コメント必須。WS/JSON parseはWeb境界として可）
  - 日本語→英語の2行セットコメントを主要処理に挿入 / 単純getter/setterプロパティ禁止 / デフォルト引数禁止
  - イベントはActionでなくUniRx（今回の新規コードでは基本ポーリング/UniTaskで足りる想定）
- `Library/PackageCache` 配下（CefUnityパッケージ）は**読み取り専用**。1文字も変更しない
- `Client.Playtest` の asmdef に `CefUnity.Runtime` / `CefUnity.Interop` / `Client.WebUiHost` への参照を追加してよい
- Prefab・シーン等Unity YAMLファイルは編集禁止。.metaファイルは手動作成禁止
  （新規.csの.metaはUnity起動時に自動生成されるので放置でよい）
- **並行作業中につき以下のファイルには絶対に触らない**（git dirtyのまま作業する）:
  `moorestech_web/webui/src/features/recipe/**`、`moorestech_web/webui/e2e/tests/recipe.spec.ts`、`.mso/**`
- git commit / git stash / git checkout 等のgit状態変更コマンドは実行しない

## 完了条件

1. Web側: `moorestech_web/webui` で `pnpm typecheck`（scripts名は package.json を確認）と既存vitestが通る
   （ネットワーク遮断環境のため `pnpm install` は不可。node_modules はインストール済み）
2. Unity側: C#がコンパイル可能な状態（Unityコンパイルはこちらで `uloop compile` 実行して検証するので、
   静的に破綻がないことをファイル間で突き合わせて確認すること。**使用する既存API・フィールド名は必ず実ファイルをReadして実在確認**）
3. 変更ファイル一覧と、各ファイルの役割・行数を最後に報告する

## 参照ファイル（実装前に必読）

| 内容 | パス |
|---|---|
| Driver本体・既存API | `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs` |
| UI操作・EventSystem直叩き現行実装 | `moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestUiOps.cs` |
| 足場・ワープ既存実装 | `moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestSetup.cs` |
| 入力注入 | `moorestech_client/Assets/Scripts/Client.Playtest/Input/SemanticInput.cs` |
| ランナー（フォワーダ生成地点） | `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestRunner.cs` |
| CEF転送の現行実装・座標変換式 | `moorestech_client/Library/PackageCache/jp.juha.cefunity@c69a8610e457/Runtime/CefUnityBrowserSample.cs` |
| CEF公開API（SendMouseClick等） | `moorestech_client/Library/PackageCache/jp.juha.cefunity@c69a8610e457/Interop/CefUnity.cs` |
| WSハブ・Publish/RegisterAction | `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketHub.cs` |
| actionハンドラ実例 | `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BuildMenuActions.cs` |
| entryType/entryKey形式 | `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs` |
| デバッグパラメータ | `moorestech_server/Assets/Scripts/Common.Debug/DebugParameter.cs` |
| Web側WSクライアント | `moorestech_web/webui/src/bridge/transport/webSocketClient.ts` / `protocol.ts` / `subscriptionManager.ts` |
| ビルドメニューtestid | `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.tsx` / `BuildMenuPanel.tsx` |

## スコープ外（別途こちらで実施）

- cef-unity上流へのHybridInput化PR
- プレイテストスキル（SKILL.md / references/ / テンプレート）のドキュメント更新 — 実機検証後に反映
- 実機でのシナリオ実行検証（`uloop` はこちらで実行する）
