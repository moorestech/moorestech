# Web UI 段階移行 Phase 0 + Phase 1 実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WS通信を双方向化（action/result + Newtonsoft.Json + アイコン配信）し、プレイヤーインベントリ＋クラフトUIをWebに移植する。

**Architecture:** 既存のKestrel+WS topic購読基盤（`Client.WebUiHost`）に `action` opとIActionHandlerレジストリを追加。actionハンドラはメインスレッドで既存のクライアント側コントローラ（`LocalPlayerInventoryController` 等）を呼ぶだけの薄い層。状態反映は従来どおりtopic eventで配信する。Web側はReactで `sendAction` ブリッジとインベントリ/クラフトUIを実装する。

**Tech Stack:** Unity (C#, UniTask, Newtonsoft.Json, ASP.NET Kestrel) / React 18 + TypeScript + Tailwind + Vite / uloop CLI（コンパイル・E2E）

**Spec:** `docs/superpowers/specs/2026-06-12-webui-migration-design.md`

---

## 前提知識（実装者向け）

- **作業ブランチ**: `feature/web-ui`（Task 1で作成）。master へのマージは全Phase完了後。
- **必ず最初に `pwd` を実行**し、`/Users/katsumi/moorestech` にいることを確認する（worktree誤爆防止）。
- **C# を変更したら必ず** `uloop compile --project-path ./moorestech_client` を実行する。
- uloop が「Unity is reloading」を返したら45秒待ってリトライ。
- **try-catch は使用禁止**（AGENTS.md）。エラーは条件分岐で処理する。本プランでは「不正JSONを送る攻撃的クライアントは受信ループの例外で接続が切れる」ことを許容する設計（正規クライアントは正しいJSONしか送らない）。
- コメントは「// 日本語 → // English」の2行セット。自明なコメントは書かない。
- Newtonsoft.Json は `moorestech_client/Assets/Plugins/Newtonsoft.Json.dll` に既に存在し、全アセンブリから参照可能（追加設定不要）。
- インベントリのスロット構成: `PlayerInventoryConst.MainInventorySize = 45`（9列×5行）。最終行（インデックス36〜44）がホットバー。Web上では `main`（0〜35）/ `hotbar`（0〜8）/ `grab` の3エリアに分けて表現し、C#側で結合インデックスへ変換する。
- E2E検証の基本手順（Play mode起動、StartLocalボタン押下、ready待ち）は `docs/web-ui-verification.md` の「自動検証」節を参照。`<CLIENT>` = `/Users/katsumi/moorestech/moorestech_client`。

## ファイル構成（最終形）

```
moorestech_client/Assets/Scripts/Client.WebUiHost/
├── Client.WebUiHost.asmdef          [変更] Client.Mod / Game.UnlockState / Server.Event を追加
├── Boot/
│   ├── WebSocketHub.cs              [変更] Newtonsoft化・フラグメント対応・action/resultルーティング
│   ├── WsClientMessage.cs           [新規] 受信メッセージDTO
│   ├── WebUiEndpoints.cs            [変更] /api/icons/ と /api/master/items のルーティング追加
│   └── WebUiHost.cs                 [変更] ClearTopics→ClearBindings、キャッシュクリア呼び出し
├── Common/
│   └── WebUiJson.cs                 [新規] camelCaseシリアライズ設定の一元管理
└── Game/
    ├── WebUiGameBinder.cs           [変更] BindTopics→Bind、actionハンドラ登録追加
    ├── ItemIconEndpoint.cs          [新規] アイコンPNG配信
    ├── ItemMasterEndpoint.cs        [新規] アイテムマスタJSON配信
    ├── Topics/
    │   ├── InventoryTopic.cs        [変更] main/hotbar/grab DTO化＋フレーム末尾publish
    │   └── CraftingRecipesTopic.cs  [新規] アンロック済みレシピ一覧
    └── Actions/
        ├── IActionHandler.cs        [新規] ハンドラIF + ActionResult
        ├── InventoryAreaMapper.cs   [新規] area/slot→ローカル座標変換（純粋関数）
        ├── DebugActions.cs          [新規] debug.echo
        ├── InventoryActions.cs      [新規] move_item / split / collect / sort
        └── CraftActions.cs          [新規] craft.execute

moorestech_client/Assets/Scripts/Client.Tests/
├── Client.Tests.asmdef              [変更] Client.WebUiHost 参照追加
└── WebUi/
    ├── WebUiJsonTest.cs             [新規]
    └── InventoryAreaMapperTest.cs   [新規]

moorestech_web/webui/src/
├── App.tsx                          [変更] 新レイアウト
├── types/
│   ├── inventory.ts                 [新規] 手書きTS型
│   ├── crafting.ts                  [新規]
│   └── itemMaster.ts                [新規]
├── bridge/
│   ├── webSocketClient.ts           [変更] sendAction追加
│   ├── actions.ts                   [新規] dispatchAction（トースト付き）
│   ├── toastBus.ts                  [新規]
│   ├── useItemMaster.ts             [新規]
│   └── useTopic.ts                  [変更なし]
└── components/
    ├── ToastHost.tsx                [新規]
    ├── DebugActionButton.tsx        [新規] debug.echo疎通ボタン
    ├── ItemSlot.tsx                 [新規] アイコン+個数+ツールチップ
    ├── InventoryPanel.tsx           [新規] メイン+ホットバー+grab操作
    ├── CraftPanel.tsx               [新規] レシピ一覧+クラフト実行
    └── InventoryView.tsx            [削除] InventoryPanelで置換

docs/web-ui-verification.md         [変更] action E2Eレシピ追記
```

**スコープ外（明示）:** ドラッグ中の複数スロット均等分配（uGUIのSplitDraggingItem相当）はパイロットでは実装しない。右クリック1個置き・半分取り・ダブルクリック収集・Shift+クリック直接移動は実装する。均等分配はPhase 2のバックログとする。

---

# Phase 0: 通信基盤の双方向化

### Task 1: feature/web-ui ブランチ作成

**Files:** なし（git操作のみ）

- [ ] **Step 1: 現在地とクリーン状態を確認**

```bash
pwd                 # /Users/katsumi/moorestech であること
git status          # クリーンであること（未コミットがあれば先に対処）
git branch --show-current   # master であること
```

- [ ] **Step 2: ブランチ作成**

```bash
git checkout -b feature/web-ui
```

Expected: `Switched to a new branch 'feature/web-ui'`

---

### Task 2: WebUiJson（シリアライズ設定の一元管理）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Common/WebUiJson.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Client.Tests.asmdef`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WebUiJsonTest.cs`

- [ ] **Step 1: Client.Tests.asmdef に Client.WebUiHost 参照を追加**

`Client.Tests.asmdef` の `references` 配列に `"Client.WebUiHost"` を1行追加する（`"Game.Train"` の後ろ）。

- [ ] **Step 2: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/WebUi/WebUiJsonTest.cs` を新規作成:

```csharp
using Client.WebUiHost.Common;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class WebUiJsonTest
    {
        private class SampleDto
        {
            public int ItemId;
            public string DisplayName;
            public string NullField;
        }

        [Test]
        public void SerializeToCamelCaseAndSkipNull()
        {
            var json = WebUiJson.Serialize(new SampleDto { ItemId = 3, DisplayName = "iron" });
            Assert.AreEqual("{\"itemId\":3,\"displayName\":\"iron\"}", json);
        }
    }
}
```

- [ ] **Step 3: コンパイルして失敗を確認**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: `WebUiJson` が存在しないためコンパイルエラー。

- [ ] **Step 4: WebUiJson を実装**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Common/WebUiJson.cs` を新規作成:

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Client.WebUiHost.Common
{
    /// <summary>
    /// Web UI 向け JSON シリアライズ設定の一元管理
    /// Centralized JSON serialization settings for the Web UI
    /// </summary>
    public static class WebUiJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Settings);
        }
    }
}
```

- [ ] **Step 5: コンパイル＆テスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WebUiJsonTest"
```

Expected: コンパイル成功、テスト1件PASS。

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Common/WebUiJson.cs* moorestech_client/Assets/Scripts/Client.Tests/WebUi* moorestech_client/Assets/Scripts/Client.Tests/Client.Tests.asmdef
git commit -m "WebUI: camelCase JSONシリアライズ設定を一元化"
```

※ `.meta` はUnityが自動生成したものを含めてコミットする（以降のタスクも同様）。

---

### Task 3: WebSocketHub の Newtonsoft.Json 化＋フラグメント対応

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WsClientMessage.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketHub.cs`

- [ ] **Step 1: WsClientMessage DTO を作成**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WsClientMessage.cs` を新規作成:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Web クライアントから届く WS メッセージの共通 DTO
    /// Common DTO for WS messages arriving from the web client
    /// </summary>
    public class WsClientMessage
    {
        public string Op;
        public List<string> Topics;
        public string Topic;
        public string Type;
        public string RequestId;
        public JObject Payload;
    }
}
```

※ Json.NET のプロパティマッチは大文字小文字非依存のため、camelCaseの入力（`op` 等）がPascalCaseフィールドに正しくマップされる。

- [ ] **Step 2: WebSocketHub の自作JSONパースを置換**

`WebSocketHub.cs` に以下の変更を加える:

(a) using に追加:

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
```

(b) `Publish` メソッド（54行目付近）の文字列手組みをJObjectに置換:

```csharp
        // 全接続のうち指定トピックを購読している接続に event を配信
        // Broadcast an event payload to all connections subscribed to the topic
        public void Publish(string topic, string dataJson)
        {
            var envelope = BuildEnvelopeJson("event", topic, dataJson);
            foreach (var conn in _connections.Values)
            {
                if (conn.Topics.Contains(topic))
                {
                    conn.EnqueueSend(envelope);
                }
            }
        }
```

(c) `ReceiveLoop` を、フレーム断片を `EndOfMessage` まで蓄積する実装に置換:

```csharp
        private async Task ReceiveLoop(Connection conn, CancellationToken ct)
        {
            var buffer = new byte[8192];
            var messageBytes = new List<byte>();
            while (conn.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await conn.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await conn.WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
                    return;
                }

                // フレーム断片を EndOfMessage まで蓄積してから1メッセージとして処理する
                // Accumulate frame fragments until EndOfMessage, then process as one message
                for (var i = 0; i < result.Count; i++) messageBytes.Add(buffer[i]);
                if (!result.EndOfMessage) continue;

                var json = Encoding.UTF8.GetString(messageBytes.ToArray());
                messageBytes.Clear();
                await HandleClientMessage(conn, json);
            }
        }
```

(d) `HandleClientMessage` を置換（`subscribe` / `unsubscribe` / `snapshot` のみ。`action` はTask 4で追加）:

```csharp
        private async Task HandleClientMessage(Connection conn, string json)
        {
            var msg = JsonConvert.DeserializeObject<WsClientMessage>(json);
            if (msg?.Op == null) return;

            switch (msg.Op)
            {
                case "subscribe":
                    if (msg.Topics == null) return;
                    foreach (var t in msg.Topics)
                    {
                        conn.Topics.Add(t);
                        await SendSnapshot(conn, t);
                    }
                    break;
                case "unsubscribe":
                    if (msg.Topics == null) return;
                    foreach (var t in msg.Topics) conn.Topics.Remove(t);
                    break;
                case "snapshot":
                    if (msg.Topic == null) return;
                    await SendSnapshot(conn, msg.Topic);
                    break;
            }
        }

        private async Task SendSnapshot(Connection conn, string topic)
        {
            if (!_handlers.TryGetValue(topic, out var handler)) return;
            var snap = await handler.GetSnapshotJsonAsync();
            conn.EnqueueSend(BuildEnvelopeJson("snapshot", topic, snap));
        }

        private static string BuildEnvelopeJson(string op, string topic, string dataJson)
        {
            var env = new JObject
            {
                ["op"] = op,
                ["topic"] = topic,
                ["data"] = JToken.Parse(dataJson),
            };
            return env.ToString(Formatting.None);
        }
```

(e) 不要になった `EscapeJsonString` / `ExtractJsonString` / `ExtractJsonStringArray` の3メソッドを削除する。

- [ ] **Step 3: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: Success（警告なし）。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/
git commit -m "WebUI: WebSocketHubの自作JSONパースをNewtonsoft.Jsonに置換しフラグメント対応"
```

---

### Task 4: action/result op と IActionHandler レジストリ

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/IActionHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketHub.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`

- [ ] **Step 1: IActionHandler と ActionResult を作成**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/IActionHandler.cs` を新規作成:

```csharp
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// Web からの action を処理するハンドラ
    /// Handler for actions sent from the web UI
    /// </summary>
    public interface IActionHandler
    {
        // ドット区切りの action 種別名（例: inventory.move_item）
        // Dot-separated action type name (e.g. inventory.move_item)
        string ActionType { get; }

        // メインスレッドで呼ばれる。payload は null の可能性あり
        // Invoked on the main thread; payload may be null
        UniTask<ActionResult> ExecuteAsync(JObject payload);
    }

    /// <summary>
    /// action の実行結果
    /// Result of an action execution
    /// </summary>
    public readonly struct ActionResult
    {
        public readonly bool Ok;
        public readonly string Error;

        private ActionResult(bool ok, string error)
        {
            Ok = ok;
            Error = error;
        }

        public static ActionResult Success()
        {
            return new ActionResult(true, null);
        }

        public static ActionResult Fail(string error)
        {
            return new ActionResult(false, error);
        }
    }
}
```

- [ ] **Step 2: WebSocketHub に action ルーティングを追加**

`WebSocketHub.cs` に以下の変更を加える:

(a) using に追加:

```csharp
using Client.WebUiHost.Game.Actions;
```

(b) フィールドとレジストリAPIを追加（`_handlers` の直後）:

```csharp
        private readonly ConcurrentDictionary<string, IActionHandler> _actionHandlers = new();

        // action ハンドラ登録（WebUiGameBinder が呼ぶ）
        // Register an action handler (called by WebUiGameBinder)
        public void RegisterAction(IActionHandler handler)
        {
            _actionHandlers[handler.ActionType] = handler;
        }
```

(c) `ClearTopics` を `ClearBindings` にリネームし、actionレジストリもクリアする:

```csharp
        // 登録済みトピック・actionを全て解除。IDisposable なものは dispose する
        // Clear all registered topics and actions; dispose IDisposable handlers
        public void ClearBindings()
        {
            foreach (var kv in _handlers)
            {
                (kv.Value as IDisposable)?.Dispose();
            }
            _handlers.Clear();
            _actionHandlers.Clear();
        }
```

(d) `HandleClientMessage` の switch に `action` caseを追加:

```csharp
                case "action":
                    await HandleActionAsync(conn, msg);
                    break;
```

(e) action実行メソッドを追加（`SendSnapshot` の直後）:

```csharp
        private async Task HandleActionAsync(Connection conn, WsClientMessage msg)
        {
            // requestId が無い action は応答相関できないため黙って捨てる
            // Drop actions without a requestId; the response cannot be correlated
            if (string.IsNullOrEmpty(msg.RequestId)) return;

            if (msg.Type == null || !_actionHandlers.TryGetValue(msg.Type, out var handler))
            {
                conn.EnqueueSend(BuildResultJson(msg.RequestId, false, "unknown_action"));
                return;
            }

            // ハンドラはゲーム状態に触るため必ずメインスレッドで実行する
            // Handlers touch game state, so always run them on the main thread
            await UniTask.SwitchToMainThread();
            var result = await handler.ExecuteAsync(msg.Payload);
            await UniTask.SwitchToTaskPool();

            conn.EnqueueSend(BuildResultJson(msg.RequestId, result.Ok, result.Error));
        }

        private static string BuildResultJson(string requestId, bool ok, string error)
        {
            var env = new JObject
            {
                ["op"] = "result",
                ["requestId"] = requestId,
                ["ok"] = ok,
            };
            if (error != null) env["error"] = error;
            return env.ToString(Formatting.None);
        }
```

※ 受信ループは action 完了を await するため、同一接続の action は到着順に直列実行される（順序保証）。

- [ ] **Step 3: WebUiHost.StopAsync の呼び出しを更新**

`WebUiHost.cs` 77行目の `_hub.ClearTopics();` を `_hub.ClearBindings();` に変更する。

- [ ] **Step 4: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: Success。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/
git commit -m "WebUI: action/result opとIActionHandlerレジストリを実装"
```

---

### Task 5: debug.echo アクションと WebUiGameBinder 拡張

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/DebugActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`（188行目付近の呼び出し）

- [ ] **Step 1: EchoActionHandler を作成**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/DebugActions.cs` を新規作成:

```csharp
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 疎通確認用のダミー action
    /// Dummy action for connectivity verification
    /// </summary>
    public class EchoActionHandler : IActionHandler
    {
        public string ActionType => "debug.echo";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            Debug.Log($"[WebUiHost] debug.echo: {payload?.ToString(Formatting.None)}");
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

- [ ] **Step 2: WebUiGameBinder を Bind にリネームし action 登録を追加**

`WebUiGameBinder.cs` の `BindTopics` メソッドを以下に置換（using に `Client.WebUiHost.Game.Actions` を追加）:

```csharp
        public static void Bind()
        {
            var hub = Boot.WebUiHost.Hub;
            if (hub == null) return;

            // DI からインベントリコントローラを取得
            // Resolve the inventory controller from DI
            var controller = ClientDIContext.DIContainer
                .DIContainerResolver
                .Resolve<LocalPlayerInventoryController>();

            // インベントリトピックを生成して Hub に登録
            // Create inventory topic and register it with the Hub
            var inventoryTopic = new InventoryTopic(hub, controller);
            hub.RegisterTopic(InventoryTopic.TopicName, inventoryTopic);

            // action ハンドラ登録
            // Register action handlers
            hub.RegisterAction(new EchoActionHandler());
        }
```

- [ ] **Step 3: 呼び出し側を更新**

```bash
grep -n "WebUiGameBinder.BindTopics" moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs
```

該当行（188行目付近）の `WebUiGameBinder.BindTopics();` を `WebUiGameBinder.Bind();` に変更する。

- [ ] **Step 4: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: Success。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/ moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs
git commit -m "WebUI: debug.echoアクションを追加しBinderでaction登録を開始"
```

---

### Task 6: アイコン配信エンドポイント

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/ItemIconEndpoint.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`

- [ ] **Step 1: asmdef に Client.Mod 参照を追加**

`Client.WebUiHost.asmdef` の `references` に `"Client.Mod"` を追加する（`ItemViewData` が `Client.Mod.Texture` 名前空間にあるため）。

- [ ] **Step 2: ItemIconEndpoint を作成**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/ItemIconEndpoint.cs` を新規作成:

```csharp
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UnityEngine;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// GET /api/icons/{itemId}.png でアイテムアイコンを PNG 配信する
    /// Serves item icons as PNG at GET /api/icons/{itemId}.png
    /// </summary>
    public static class ItemIconEndpoint
    {
        public const string PathPrefix = "/api/icons/";
        public const string PathSuffix = ".png";

        private static readonly ConcurrentDictionary<int, byte[]> _pngCache = new();

        public static void ClearCache()
        {
            _pngCache.Clear();
        }

        public static async Task HandleAsync(HttpContext context, string path)
        {
            // パスから itemId を取り出す。不正なら 404
            // Extract itemId from the path; 404 if malformed
            var idText = path.Substring(PathPrefix.Length, path.Length - PathPrefix.Length - PathSuffix.Length);
            if (!int.TryParse(idText, out var itemIdValue))
            {
                context.Response.StatusCode = 404;
                return;
            }

            // ゲーム起動完了前は ItemImageContainer が未生成のため 503
            // ItemImageContainer is not yet created before game startup; return 503
            if (ClientContext.ItemImageContainer == null)
            {
                context.Response.StatusCode = 503;
                return;
            }

            if (!_pngCache.TryGetValue(itemIdValue, out var png))
            {
                png = await EncodePngOnMainThread(itemIdValue);
                if (png == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                _pngCache[itemIdValue] = png;
            }

            context.Response.ContentType = "image/png";
            context.Response.Headers["Cache-Control"] = "public, max-age=86400";
            await context.Response.Body.WriteAsync(png, 0, png.Length);
        }

        private static async UniTask<byte[]> EncodePngOnMainThread(int itemIdValue)
        {
            // EncodeToPNG は Unity API のためメインスレッドで実行する
            // EncodeToPNG is a Unity API and must run on the main thread
            await UniTask.SwitchToMainThread();
            var view = ClientContext.ItemImageContainer.GetItemView(new ItemId(itemIdValue));
            var png = view?.ItemTexture is Texture2D texture ? texture.EncodeToPNG() : null;
            await UniTask.SwitchToTaskPool();
            return png;
        }
    }
}
```

※ 空アイテム（itemId=0）や未知IDは `GetItemView` がnullを返すため404になる。未知IDでは `ItemViewData not found` のLogErrorが出るが、正規UIはtopicに含まれるIDしか要求しないため実害なし。

- [ ] **Step 3: WebUiEndpoints にルーティングを追加**

`WebUiEndpoints.cs` の `/api/ping` ブロックの直後（86行目の404処理の前）に追加:

```csharp
                if (path.StartsWith(Game.ItemIconEndpoint.PathPrefix, StringComparison.Ordinal) && path.EndsWith(Game.ItemIconEndpoint.PathSuffix, StringComparison.Ordinal))
                {
                    // アイテムアイコンの PNG 配信
                    // Serve item icon PNGs
                    await Game.ItemIconEndpoint.HandleAsync(context, path);
                    return;
                }
```

- [ ] **Step 4: WebUiHost.StopAsync でキャッシュをクリア**

`WebUiHost.cs` の `StopAsync` 内、`_hub.ClearBindings();` の直前に追加:

```csharp
            // セーブデータ/Mod 切り替えに備えてアイコンキャッシュを破棄する
            // Drop the icon cache in case the save data / mod set changes
            Game.ItemIconEndpoint.ClearCache();
```

- [ ] **Step 5: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: Success。

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/
git commit -m "WebUI: アイテムアイコンのPNG配信エンドポイントを追加"
```

---

### Task 7: React側 sendAction + トースト

**Files:**
- Modify: `moorestech_web/webui/src/bridge/webSocketClient.ts`
- Create: `moorestech_web/webui/src/bridge/toastBus.ts`
- Create: `moorestech_web/webui/src/bridge/actions.ts`
- Create: `moorestech_web/webui/src/components/ToastHost.tsx`
- Create: `moorestech_web/webui/src/components/DebugActionButton.tsx`
- Modify: `moorestech_web/webui/src/App.tsx`

- [ ] **Step 1: webSocketClient.ts に action/result を追加**

`webSocketClient.ts` を以下のとおり変更する。

(a) 型定義を更新:

```ts
export type ActionResult = { ok: boolean; error?: string };

type ServerMsg =
  | { op: "snapshot"; topic: string; data: unknown }
  | { op: "event"; topic: string; data: unknown }
  | { op: "result"; requestId: string; ok: boolean; error?: string };

type ClientMsg =
  | { op: "subscribe"; topics: string[] }
  | { op: "unsubscribe"; topics: string[] }
  | { op: "snapshot"; topics: string[] }
  | { op: "action"; type: string; requestId: string; payload: unknown };
```

(b) `WebSocketClient` クラスにフィールドとメソッドを追加:

```ts
  private nextRequestId = 1;
  private readonly pendingActions = new Map<
    string,
    { resolve: (r: ActionResult) => void; reject: (e: Error) => void; timer: number }
  >();

  // action を発行し result を Promise で返す。タイムアウト・切断時は reject
  // Send an action and resolve with its result; reject on timeout or disconnect
  sendAction(type: string, payload: unknown): Promise<ActionResult> {
    return new Promise((resolve, reject) => {
      if (this.ws?.readyState !== WebSocket.OPEN) {
        reject(new Error("disconnected"));
        return;
      }
      const requestId = `a${this.nextRequestId++}`;
      const timer = window.setTimeout(() => {
        this.pendingActions.delete(requestId);
        reject(new Error("timeout"));
      }, 5000);
      this.pendingActions.set(requestId, { resolve, reject, timer });
      this.ws.send(JSON.stringify({ op: "action", type, requestId, payload }));
    });
  }
```

(c) `ws.onmessage` を result 対応に変更:

```ts
    ws.onmessage = (ev) => {
      // バイナリ等の文字列以外のフレームは捨てる
      // Drop non-text frames
      if (typeof ev.data !== "string") return;
      const msg = JSON.parse(ev.data) as Partial<ServerMsg>;
      if (msg.op === "result") {
        if (typeof msg.requestId !== "string") return;
        const pending = this.pendingActions.get(msg.requestId);
        if (!pending) return;
        this.pendingActions.delete(msg.requestId);
        window.clearTimeout(pending.timer);
        pending.resolve({ ok: msg.ok === true, error: msg.error });
        return;
      }
      if (msg.op !== "snapshot" && msg.op !== "event") return;
      if (typeof msg.topic !== "string") return;
      const set = this.listeners.get(msg.topic);
      if (set) set.forEach((l) => l(msg.data));
    };
```

(d) `ws.onclose` の先頭で保留中actionを全てrejectする:

```ts
    ws.onclose = () => {
      // 切断時は保留中の action を全て reject する
      // Reject all pending actions on disconnect
      this.pendingActions.forEach((p) => {
        window.clearTimeout(p.timer);
        p.reject(new Error("disconnected"));
      });
      this.pendingActions.clear();
      this.ws = null;
      // 指数バックオフで再接続（上限 5 秒）
      // Exponential backoff reconnect (capped at 5s)
      const delay = Math.min(this.reconnectDelayMs, 5000);
      this.reconnectDelayMs = Math.min(this.reconnectDelayMs * 2, 5000);
      setTimeout(() => this.openSocket(), delay);
    };
```

(e) ファイル末尾にエクスポートを追加:

```ts
export function sendAction(type: string, payload: unknown): Promise<ActionResult> {
  return client.sendAction(type, payload);
}
```

- [ ] **Step 2: toastBus.ts を作成**

```ts
// トースト通知の極小 pub-sub。React 外（bridge層）からも emit できるようにする
// Minimal pub-sub for toast notifications, emittable from outside React (bridge layer)

type ToastListener = (message: string) => void;

const listeners = new Set<ToastListener>();

export function subscribeToast(listener: ToastListener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function showToast(message: string) {
  listeners.forEach((l) => l(message));
}
```

- [ ] **Step 3: actions.ts を作成**

```ts
import { sendAction } from "./webSocketClient";
import { showToast } from "./toastBus";

// action を発行し、失敗時はトースト表示して false を返す UI 向けラッパ
// UI-facing wrapper: dispatch an action, toast on failure, return success flag
export async function dispatchAction(type: string, payload: unknown): Promise<boolean> {
  try {
    const result = await sendAction(type, payload);
    if (!result.ok) {
      showToast(`${type} failed: ${result.error ?? "unknown"}`);
      return false;
    }
    return true;
  } catch (e) {
    showToast(`${type} error: ${e instanceof Error ? e.message : String(e)}`);
    return false;
  }
}
```

- [ ] **Step 4: ToastHost.tsx を作成**

```tsx
import { useEffect, useState } from "react";
import { subscribeToast } from "../bridge/toastBus";

type Toast = { id: number; message: string };

// 画面右下にトーストを表示するホスト。3秒で自動消滅
// Toast host pinned to the bottom-right; each toast auto-dismisses after 3s
export default function ToastHost() {
  const [toasts, setToasts] = useState<Toast[]>([]);

  useEffect(() => {
    let nextId = 1;
    return subscribeToast((message) => {
      const id = nextId++;
      setToasts((prev) => [...prev, { id, message }]);
      setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 3000);
    });
  }, []);

  return (
    <div className="fixed bottom-4 right-4 space-y-2 z-50">
      {toasts.map((t) => (
        <div key={t.id} className="bg-red-800 text-white text-sm rounded px-3 py-2 shadow">
          {t.message}
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 5: DebugActionButton.tsx を作成**

```tsx
import { dispatchAction } from "../bridge/actions";
import { showToast } from "../bridge/toastBus";

// debug.echo を発行して双方向APIの疎通を確認する開発用ボタン
// Dev button that sends debug.echo to verify the bidirectional API
export default function DebugActionButton() {
  const onClick = async () => {
    const ok = await dispatchAction("debug.echo", { hello: "world" });
    if (ok) showToast("debug.echo ok");
  };

  return (
    <button onClick={onClick} className="bg-gray-700 hover:bg-gray-600 text-sm rounded px-3 py-1">
      Ping Action
    </button>
  );
}
```

- [ ] **Step 6: App.tsx に組み込み**

```tsx
import InventoryView from "./components/InventoryView";
import ToastHost from "./components/ToastHost";
import DebugActionButton from "./components/DebugActionButton";

export default function App() {
  return (
    <div className="p-4 space-y-4">
      <h1 className="text-2xl font-bold">moorestech Web UI</h1>
      <DebugActionButton />
      <InventoryView />
      <ToastHost />
    </div>
  );
}
```

- [ ] **Step 7: TypeScript ビルド確認**

```bash
cd moorestech_web/webui && ../node/mac-arm64/pnpm exec tsc --noEmit && cd ../..
```

Expected: エラーなし。

- [ ] **Step 8: コミット**

```bash
git add moorestech_web/webui/src/
git commit -m "WebUI: React側にsendActionブリッジとトースト表示を追加"
```

---

### Task 8: Phase 0 E2E 検証

**Files:** なし（検証のみ。失敗したら該当タスクに戻って修正）

- [ ] **Step 1: Play mode 起動と ready 待ち**

`docs/web-ui-verification.md` の「自動検証」節の手順1〜3を実行（MainMenuシーンを開く → Play → StartLocalボタン押下 → `[WebUiHost] ready` をログで確認）。

- [ ] **Step 2: ws パッケージを一時追加し、action検証スクリプトを実行**

```bash
cd moorestech_web/webui && ../node/mac-arm64/pnpm add -D ws && cd ../..
```

`/tmp/ws-action-verify.mjs` を作成:

```js
import WebSocket from 'ws';
const ws = new WebSocket('ws://127.0.0.1:5173/ws', {
  headers: { Origin: 'http://localhost:5173' }
});
ws.on('open', () => {
  console.log('OPEN');
  ws.send(JSON.stringify({ op: 'action', type: 'debug.echo', requestId: 'a1', payload: { hello: 'world' } }));
  ws.send(JSON.stringify({ op: 'action', type: 'no.such.action', requestId: 'a2', payload: null }));
});
ws.on('message', (d) => console.log('MSG', String(d)));
setTimeout(() => process.exit(0), 8000);
```

```bash
cd moorestech_web/webui && ../node/mac-arm64/bin/node /tmp/ws-action-verify.mjs && cd ../..
```

Expected:
- `MSG {"op":"result","requestId":"a1","ok":true}`
- `MSG {"op":"result","requestId":"a2","ok":false,"error":"unknown_action"}`

Unityログにも `[WebUiHost] debug.echo: {"hello":"world"}` が出ること:

```bash
uloop get-logs --project-path ./moorestech_client --log-type Log | grep "debug.echo"
```

- [ ] **Step 3: アイコンエンドポイントを確認**

```bash
curl -s -o /tmp/icon.png -w "%{http_code} %{content_type}\n" http://127.0.0.1:5173/api/icons/1.png
file /tmp/icon.png
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5173/api/icons/0.png        # 空アイテム → 404
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5173/api/icons/xyz.png      # 不正ID → 404
```

Expected: 1行目 `200 image/png`、`file` が `PNG image data`、以降 `404` `404`。

- [ ] **Step 4: ブラウザで Ping Action ボタンを確認**

`http://localhost:5173/` を開き、「Ping Action」クリック → 「debug.echo ok」トーストが表示されること（ユーザーに目視依頼するか、スクリーンショットで確認）。

- [ ] **Step 5: 後片付けと既存機能の回帰確認**

Play mode を停止し、クリーンアップログ（`[WebUiHost] stopped`）を確認。wsパッケージを戻す:

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
cd moorestech_web/webui && ../node/mac-arm64/pnpm remove ws && git checkout -- package.json pnpm-lock.yaml && cd ../..
rm /tmp/ws-action-verify.mjs /tmp/icon.png
```

- [ ] **Step 6: Phase 0 完了をコミット（検証中に修正があれば一緒に）**

```bash
git status   # 差分が残っていないこと（修正があればコミット）
```

---

# Phase 1: プレイヤーインベントリ移植

### Task 9: InventoryAreaMapper（area/slot変換の純粋関数）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/InventoryAreaMapper.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/InventoryAreaMapperTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/WebUi/InventoryAreaMapperTest.cs` を新規作成:

```csharp
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Game.Actions;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class InventoryAreaMapperTest
    {
        [TestCase("main", 0, LocalMoveInventoryType.MainOrSub, 0)]
        [TestCase("main", 35, LocalMoveInventoryType.MainOrSub, 35)]
        [TestCase("hotbar", 0, LocalMoveInventoryType.MainOrSub, 36)]
        [TestCase("hotbar", 8, LocalMoveInventoryType.MainOrSub, 44)]
        [TestCase("grab", 0, LocalMoveInventoryType.Grab, 0)]
        public void ValidAreaSlotMapsToLocalSlot(string area, int slot, LocalMoveInventoryType expectedType, int expectedSlot)
        {
            var ok = InventoryAreaMapper.TryGetLocalSlot(area, slot, out var type, out var localSlot);
            Assert.IsTrue(ok);
            Assert.AreEqual(expectedType, type);
            Assert.AreEqual(expectedSlot, localSlot);
        }

        [TestCase("main", -1)]
        [TestCase("main", 36)]
        [TestCase("hotbar", -1)]
        [TestCase("hotbar", 9)]
        [TestCase("sub", 0)]
        [TestCase(null, 0)]
        public void InvalidAreaSlotReturnsFalse(string area, int slot)
        {
            var ok = InventoryAreaMapper.TryGetLocalSlot(area, slot, out _, out _);
            Assert.IsFalse(ok);
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: `InventoryAreaMapper` 未定義でコンパイルエラー。

- [ ] **Step 3: InventoryAreaMapper を実装**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/InventoryAreaMapper.cs` を新規作成:

```csharp
using Client.Game.InGame.UI.Inventory.Main;
using Game.PlayerInventory.Interface;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// Web の area/slot 表現をローカルインベントリ座標へ変換する
    /// Maps web-side area/slot pairs to local inventory coordinates
    /// </summary>
    public static class InventoryAreaMapper
    {
        // メイン部 = ホットバー行を除いた 36 スロット
        // Main section = 36 slots excluding the hotbar row
        public static readonly int MainAreaSize = PlayerInventoryConst.MainInventorySize - PlayerInventoryConst.MainInventoryColumns;

        public static bool TryGetLocalSlot(string area, int slot, out LocalMoveInventoryType type, out int localSlot)
        {
            switch (area)
            {
                case "main" when 0 <= slot && slot < MainAreaSize:
                    type = LocalMoveInventoryType.MainOrSub;
                    localSlot = slot;
                    return true;
                case "hotbar" when 0 <= slot && slot < PlayerInventoryConst.MainInventoryColumns:
                    type = LocalMoveInventoryType.MainOrSub;
                    localSlot = MainAreaSize + slot;
                    return true;
                case "grab":
                    type = LocalMoveInventoryType.Grab;
                    localSlot = 0;
                    return true;
                default:
                    type = LocalMoveInventoryType.MainOrSub;
                    localSlot = -1;
                    return false;
            }
        }

        // payload 中の {"area":"main","slot":3} 形式の JToken を変換する
        // Parse a {"area":"main","slot":3}-shaped JToken from a payload
        public static bool TryParseSlotRef(JToken token, out LocalMoveInventoryType type, out int localSlot)
        {
            type = LocalMoveInventoryType.MainOrSub;
            localSlot = -1;
            if (token is not JObject obj) return false;
            var area = obj.Value<string>("area");
            var slot = obj.Value<int?>("slot") ?? 0;
            return TryGetLocalSlot(area, slot, out type, out localSlot);
        }
    }
}
```

- [ ] **Step 4: コンパイル＆テスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "InventoryAreaMapperTest"
```

Expected: 全ケースPASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/ moorestech_client/Assets/Scripts/Client.Tests/WebUi/
git commit -m "WebUI: area/slot表現のローカル座標変換を追加"
```

---

### Task 10: InventoryTopic v2（main/hotbar/grab DTO化）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/InventoryTopic.cs`（全面書き換え）
- Modify: `moorestech_web/webui/src/components/InventoryView.tsx`（暫定追従。Task 15で置換）

**設計上の要点:** `LocalPlayerInventoryController.MoveItem` はスロット代入のたびに `OnItemChange` を発火するが、その時点では `GrabInventory` の更新が完了していない中間状態がある（例: grab→slot移動でスロット代入→grab減算の順）。そのため**即時publishせず、フレーム末尾にまとめて1回publish**する。これによりcollect等の連続移動も1イベントに合体する。

- [ ] **Step 1: InventoryTopic.cs を全面書き換え**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// local_player.inventory トピック: main/hotbar/grab の全量を push
    /// local_player.inventory topic: pushes the full main/hotbar/grab state
    /// </summary>
    public class InventoryTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "local_player.inventory";

        private readonly WebSocketHub _hub;
        private readonly LocalPlayerInventoryController _controller;
        private readonly IDisposable _subscription;
        private bool _publishScheduled;

        public InventoryTopic(WebSocketHub hub, LocalPlayerInventoryController controller)
        {
            _hub = hub;
            _controller = controller;

            // スロット変更通知を購読し、Dispose 時に解除できるよう保持
            // Subscribe to slot-change notifications; retain the disposable so Dispose can unhook
            _subscription = _controller.LocalPlayerInventory.OnItemChange.Subscribe(_ => SchedulePublish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        // MoveItem 途中の中間状態（grab 未更新等）を配信しないようフレーム末尾でまとめて publish する
        // Defer publishing to end of frame so mid-MoveItem intermediate states never go out
        private void SchedulePublish()
        {
            if (_publishScheduled) return;
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();
        }

        private async UniTaskVoid PublishAtEndOfFrame()
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            _publishScheduled = false;
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            var inv = _controller.LocalPlayerInventory;
            var dto = new PlayerInventoryDto
            {
                MainSlots = new List<SlotDto>(InventoryAreaMapper.MainAreaSize),
                HotbarSlots = new List<SlotDto>(PlayerInventoryConst.MainInventoryColumns),
                Grab = ToDto(_controller.GrabInventory),
            };
            for (var i = 0; i < InventoryAreaMapper.MainAreaSize; i++) dto.MainSlots.Add(ToDto(inv[i]));
            for (var i = InventoryAreaMapper.MainAreaSize; i < PlayerInventoryConst.MainInventorySize; i++) dto.HotbarSlots.Add(ToDto(inv[i]));
            return WebUiJson.Serialize(dto);
        }

        private static SlotDto ToDto(IItemStack stack)
        {
            return new SlotDto { ItemId = stack.Id.AsPrimitive(), Count = stack.Count };
        }
    }

    /// <summary>
    /// local_player.inventory の配信 DTO
    /// Payload DTO for local_player.inventory
    /// </summary>
    public class PlayerInventoryDto
    {
        public List<SlotDto> MainSlots;
        public List<SlotDto> HotbarSlots;
        public SlotDto Grab;
    }

    public class SlotDto
    {
        public int ItemId;
        public int Count;
    }
}
```

- [ ] **Step 2: 既存 InventoryView.tsx を暫定追従**

新形式（`mainSlots`/`hotbarSlots`/`grab`）でクラッシュしないよう、型とmap対象だけ差し替える（Task 15で全面置換するため最小修正）:

```tsx
import { useTopic } from "../bridge/useTopic";

type SlotData = { itemId: number; count: number };
type InventoryData = {
  mainSlots: SlotData[];
  hotbarSlots: SlotData[];
  grab: SlotData;
};

// ローカルプレイヤーのインベントリを WS 購読して表示
// Subscribe to the local player's inventory over WS and render it
export default function InventoryView() {
  const inventory = useTopic<InventoryData>("local_player.inventory");

  if (!inventory) {
    return <div className="text-sm text-gray-400">connecting...</div>;
  }

  const slots = [...inventory.mainSlots, ...inventory.hotbarSlots];

  return (
    <div>
      <h2 className="text-lg font-semibold mb-2">Inventory</h2>
      <div className="grid grid-cols-9 gap-1">
        {slots.map((s, i) => (
          <div
            key={i}
            className="border border-gray-700 rounded p-2 min-h-[48px] text-xs flex flex-col justify-between bg-gray-900"
          >
            <div className="text-gray-400">#{i}</div>
            {s.count > 0 ? (
              <div>
                <div className="text-white">id:{s.itemId}</div>
                <div className="text-green-400">×{s.count}</div>
              </div>
            ) : null}
          </div>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: コンパイルと型チェック**

```bash
uloop compile --project-path ./moorestech_client
cd moorestech_web/webui && ../node/mac-arm64/pnpm exec tsc --noEmit && cd ../..
```

Expected: 両方Success。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/ moorestech_web/webui/src/components/InventoryView.tsx
git commit -m "WebUI: インベントリトピックをmain/hotbar/grab構成にDTO化しフレーム末尾publishに変更"
```

---

### Task 11: inventory.* アクション実装

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/InventoryActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`

- [ ] **Step 1: InventoryActions.cs を作成**

4ハンドラを1ファイルにまとめる（いずれも `LocalPlayerInventoryController` を呼ぶ薄い層）:

```csharp
using System.Linq;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// inventory.move_item: from→to へ count 個移動する
    /// inventory.move_item: move count items from→to
    /// </summary>
    public class MoveItemActionHandler : IActionHandler
    {
        public string ActionType => "inventory.move_item";

        private readonly LocalPlayerInventoryController _controller;

        public MoveItemActionHandler(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            var count = payload.Value<int?>("count") ?? 0;
            if (count <= 0) return UniTask.FromResult(ActionResult.Fail("invalid_count"));

            if (!InventoryAreaMapper.TryParseSlotRef(payload["from"], out var fromType, out var fromSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));
            if (!InventoryAreaMapper.TryParseSlotRef(payload["to"], out var toType, out var toSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            _controller.MoveItem(fromType, fromSlot, toType, toSlot, count);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// inventory.split: スロットの半分を grab に取る（uGUI の右クリック相当）
    /// inventory.split: grab half of a slot's stack (uGUI right-click equivalent)
    /// </summary>
    public class SplitGrabActionHandler : IActionHandler
    {
        public string ActionType => "inventory.split";

        private readonly LocalPlayerInventoryController _controller;

        public SplitGrabActionHandler(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!InventoryAreaMapper.TryParseSlotRef(payload?["from"], out var fromType, out var fromSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));
            if (fromType != LocalMoveInventoryType.MainOrSub) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));
            if (_controller.GrabInventory.Id != ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("grab_not_empty"));

            var item = _controller.LocalPlayerInventory[fromSlot];
            if (item.Id == ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("empty_slot"));

            // 1個以下なら半分は0なので何もしない（成功扱い）
            // A stack of 1 has no half; treat as a successful no-op
            var half = item.Count / 2;
            if (half > 0) _controller.MoveItem(LocalMoveInventoryType.MainOrSub, fromSlot, LocalMoveInventoryType.Grab, 0, half);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// inventory.collect: 同種アイテムを target に集める（uGUI のダブルクリック相当）
    /// inventory.collect: gather same-type items into target (uGUI double-click equivalent)
    /// </summary>
    public class CollectActionHandler : IActionHandler
    {
        public string ActionType => "inventory.collect";

        private readonly LocalPlayerInventoryController _controller;

        public CollectActionHandler(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!InventoryAreaMapper.TryParseSlotRef(payload?["target"], out var targetType, out var targetSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            var inventory = _controller.LocalPlayerInventory;
            var isGrabTarget = targetType == LocalMoveInventoryType.Grab;
            var collectTarget = isGrabTarget ? _controller.GrabInventory : inventory[targetSlot];
            if (collectTarget.Id == ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("empty_slot"));

            // 同種アイテムを所持数の少ない順に集める（uGUI のダブルクリックと同じ並び）
            // Gather same-type stacks smallest-first, matching the uGUI double-click order
            var sourceSlots = inventory
                .Select((item, index) => (item, index))
                .Where(x => x.item.Id == collectTarget.Id)
                .Where(x => isGrabTarget || x.index != targetSlot)
                .OrderBy(x => x.item.Count)
                .Select(x => x.index)
                .ToList();

            foreach (var index in sourceSlots)
            {
                var added = collectTarget.AddItem(inventory[index]);
                var moveCount = inventory[index].Count - added.RemainderItemStack.Count;
                if (moveCount <= 0) continue;
                _controller.MoveItem(LocalMoveInventoryType.MainOrSub, index, targetType, targetSlot, moveCount);
                collectTarget = added.ProcessResultItemStack;

                // 余りが出たら集積先が満杯なので終了
                // A remainder means the target stack is full; stop here
                if (added.RemainderItemStack.Count != 0) break;
            }
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// inventory.sort: インベントリ整理（uGUI の整理ボタン相当）
    /// inventory.sort: tidy the inventory (uGUI sort-button equivalent)
    /// </summary>
    public class SortInventoryActionHandler : IActionHandler
    {
        public string ActionType => "inventory.sort";

        private readonly LocalPlayerInventoryController _controller;

        public SortInventoryActionHandler(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _controller.SortInventory();
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

- [ ] **Step 2: WebUiGameBinder に登録を追加**

`Bind()` の `hub.RegisterAction(new EchoActionHandler());` の後に追加:

```csharp
            hub.RegisterAction(new MoveItemActionHandler(controller));
            hub.RegisterAction(new SplitGrabActionHandler(controller));
            hub.RegisterAction(new CollectActionHandler(controller));
            hub.RegisterAction(new SortInventoryActionHandler(controller));
```

- [ ] **Step 3: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: Success。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/
git commit -m "WebUI: inventory.move_item/split/collect/sortアクションを実装"
```

---

### Task 12: craft.execute アクションと crafting.recipes トピック

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/CraftActions.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/CraftingRecipesTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`

- [ ] **Step 1: asmdef に参照を追加**

`Client.WebUiHost.asmdef` の `references` に以下2件を追加:
- `"Game.UnlockState"`（`IGameUnlockStateData`）
- `"Server.Event"`（`UnlockedEventPacket.EventTag`）

- [ ] **Step 2: CraftActions.cs を作成**

```csharp
using System;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// craft.execute: 指定レシピのワンクリッククラフトをサーバーへ送信する
    /// craft.execute: send a one-click craft request for the given recipe
    /// </summary>
    public class CraftExecuteActionHandler : IActionHandler
    {
        public string ActionType => "craft.execute";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var guidText = payload?.Value<string>("recipeGuid");
            if (!Guid.TryParse(guidText, out var recipeGuid)) return UniTask.FromResult(ActionResult.Fail("invalid_recipe"));

            // 素材所持チェックはサーバー側で行われるためここでは送信のみ
            // Material checks happen server-side; just send the request here
            ClientContext.VanillaApi.SendOnly.Craft(recipeGuid);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

- [ ] **Step 3: CraftingRecipesTopic.cs を作成**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Server.Event.EventReceive;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// crafting.recipes トピック: アンロック済みクラフトレシピ一覧。アンロックイベントで再配信
    /// crafting.recipes topic: unlocked craft recipes; republished on unlock events
    /// </summary>
    public class CraftingRecipesTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "crafting.recipes";

        private readonly WebSocketHub _hub;
        private readonly IGameUnlockStateData _unlockStateData;
        private readonly IDisposable _subscription;

        public CraftingRecipesTopic(WebSocketHub hub, IGameUnlockStateData unlockStateData)
        {
            _hub = hub;
            _unlockStateData = unlockStateData;

            // ClientGameUnlockStateData より後に購読登録されるため、ここでは更新後の状態を読める
            // Subscribed after ClientGameUnlockStateData, so the updated unlock state is visible here
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private string BuildJson()
        {
            var dto = new CraftRecipesDto { Recipes = new List<CraftRecipeDto>() };
            var unlockInfos = _unlockStateData.CraftRecipeUnlockStateInfos;
            foreach (var recipe in MasterHolder.CraftRecipeMaster.GetAllCraftRecipes())
            {
                if (!unlockInfos[recipe.CraftRecipeGuid].IsUnlocked) continue;

                var requiredItems = new List<RequiredItemDto>();
                foreach (var requiredItem in recipe.RequiredItems)
                {
                    requiredItems.Add(new RequiredItemDto
                    {
                        ItemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid).AsPrimitive(),
                        Count = requiredItem.Count,
                    });
                }

                dto.Recipes.Add(new CraftRecipeDto
                {
                    RecipeGuid = recipe.CraftRecipeGuid.ToString(),
                    ResultItemId = MasterHolder.ItemMaster.GetItemId(recipe.CraftResultItemGuid).AsPrimitive(),
                    ResultCount = recipe.CraftResultCount,
                    CraftTime = recipe.CraftTime,
                    RequiredItems = requiredItems,
                });
            }
            return WebUiJson.Serialize(dto);
        }
    }

    /// <summary>
    /// crafting.recipes の配信 DTO
    /// Payload DTO for crafting.recipes
    /// </summary>
    public class CraftRecipesDto
    {
        public List<CraftRecipeDto> Recipes;
    }

    public class CraftRecipeDto
    {
        public string RecipeGuid;
        public int ResultItemId;
        public int ResultCount;
        public double CraftTime;
        public List<RequiredItemDto> RequiredItems;
    }

    public class RequiredItemDto
    {
        public int ItemId;
        public int Count;
    }
}
```

※ `recipe.CraftTime` の型が `double` 以外（float/int）でコンパイルエラーになる場合は DTO 側の型をマスタ定義（Mooresmaster生成コード）に合わせる。`RequiredItems` の `Count` も同様。

- [ ] **Step 4: WebUiGameBinder に登録を追加**

`Bind()` 内、actionハンドラ登録ブロックの前に追加（using に `Game.UnlockState` を追加）:

```csharp
            // クラフトレシピトピックを登録（アンロック状態は DI から取得）
            // Register the craft-recipes topic (unlock state comes from DI)
            var unlockStateData = ClientDIContext.DIContainer
                .DIContainerResolver
                .Resolve<IGameUnlockStateData>();
            var craftingRecipesTopic = new CraftingRecipesTopic(hub, unlockStateData);
            hub.RegisterTopic(CraftingRecipesTopic.TopicName, craftingRecipesTopic);
```

actionハンドラ登録ブロックには追加:

```csharp
            hub.RegisterAction(new CraftExecuteActionHandler());
```

- [ ] **Step 5: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: Success。型不一致が出たらStep 3の※に従い修正。

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/
git commit -m "WebUI: craft.executeアクションとcrafting.recipesトピックを追加"
```

---

### Task 13: /api/master/items エンドポイント

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/ItemMasterEndpoint.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`

- [ ] **Step 1: ItemMasterEndpoint を作成**

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Client.WebUiHost.Common;
using Core.Master;
using Microsoft.AspNetCore.Http;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// GET /api/master/items でアイテムマスタ（名前・スタック上限）を配信する
    /// Serves item master data (name, max stack) at GET /api/master/items
    /// </summary>
    public static class ItemMasterEndpoint
    {
        public const string Path = "/api/master/items";

        private static string _cachedJson;

        public static void ClearCache()
        {
            _cachedJson = null;
        }

        public static async Task HandleAsync(HttpContext context)
        {
            // マスタロード完了前のリクエストは 503 を返す
            // Requests arriving before master data is loaded get a 503
            if (MasterHolder.ItemMaster == null)
            {
                context.Response.StatusCode = 503;
                return;
            }

            _cachedJson ??= BuildJson();
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(_cachedJson, CancellationToken.None);
        }

        private static string BuildJson()
        {
            var dto = new ItemMasterListDto { Items = new List<ItemMasterDto>() };
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var master = MasterHolder.ItemMaster.GetItemMaster(itemId);
                dto.Items.Add(new ItemMasterDto
                {
                    ItemId = itemId.AsPrimitive(),
                    Name = master.Name,
                    MaxStack = master.MaxStack,
                });
            }
            return WebUiJson.Serialize(dto);
        }
    }

    /// <summary>
    /// /api/master/items の配信 DTO
    /// Payload DTO for /api/master/items
    /// </summary>
    public class ItemMasterListDto
    {
        public List<ItemMasterDto> Items;
    }

    public class ItemMasterDto
    {
        public int ItemId;
        public string Name;
        public int MaxStack;
    }
}
```

- [ ] **Step 2: WebUiEndpoints にルーティングを追加**

アイコンルーティングの直後に追加:

```csharp
                if (path == Game.ItemMasterEndpoint.Path)
                {
                    // アイテムマスタの JSON 配信
                    // Serve item master JSON
                    await Game.ItemMasterEndpoint.HandleAsync(context);
                    return;
                }
```

- [ ] **Step 3: WebUiHost.StopAsync にキャッシュクリアを追加**

`Game.ItemIconEndpoint.ClearCache();` の直後に追加:

```csharp
            Game.ItemMasterEndpoint.ClearCache();
```

- [ ] **Step 4: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: Success。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/
git commit -m "WebUI: アイテムマスタ配信エンドポイントを追加"
```

---

### Task 14: Web側 型定義・useItemMaster・ItemSlot

**Files:**
- Create: `moorestech_web/webui/src/types/inventory.ts`
- Create: `moorestech_web/webui/src/types/crafting.ts`
- Create: `moorestech_web/webui/src/types/itemMaster.ts`
- Create: `moorestech_web/webui/src/bridge/useItemMaster.ts`
- Create: `moorestech_web/webui/src/components/ItemSlot.tsx`

- [ ] **Step 1: 型定義3ファイルを作成**

`types/inventory.ts`:

```ts
// local_player.inventory トピックの手書き型（SourceGenerator 導入後に自動生成へ置換予定）
// Handwritten types for local_player.inventory (to be replaced by generated types later)

export type SlotData = { itemId: number; count: number };

export type PlayerInventoryData = {
  mainSlots: SlotData[];
  hotbarSlots: SlotData[];
  grab: SlotData;
};

export type InventoryArea = "main" | "hotbar" | "grab";

export type SlotRef = { area: InventoryArea; slot: number };
```

`types/crafting.ts`:

```ts
// crafting.recipes トピックの手書き型
// Handwritten types for crafting.recipes

export type RequiredItem = { itemId: number; count: number };

export type CraftRecipe = {
  recipeGuid: string;
  resultItemId: number;
  resultCount: number;
  craftTime: number;
  requiredItems: RequiredItem[];
};

export type CraftRecipesData = { recipes: CraftRecipe[] };
```

`types/itemMaster.ts`:

```ts
// /api/master/items の手書き型
// Handwritten types for /api/master/items

export type ItemMasterEntry = { itemId: number; name: string; maxStack: number };

export type ItemMasterData = { items: ItemMasterEntry[] };
```

- [ ] **Step 2: useItemMaster.ts を作成**

```ts
import { useEffect, useState } from "react";
import type { ItemMasterData, ItemMasterEntry } from "../types/itemMaster";

let cached: Map<number, ItemMasterEntry> | null = null;

// アイテムマスタを一度だけ fetch して itemId → entry の Map を返す
// Fetch the item master once and return an itemId → entry map
export function useItemMaster(): Map<number, ItemMasterEntry> | null {
  const [master, setMaster] = useState(cached);

  useEffect(() => {
    if (cached) return;
    let cancelled = false;
    fetch("/api/master/items")
      .then((r) => r.json())
      .then((data: ItemMasterData) => {
        cached = new Map(data.items.map((i) => [i.itemId, i]));
        if (!cancelled) setMaster(cached);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return master;
}
```

- [ ] **Step 3: ItemSlot.tsx を作成**

```tsx
import type { MouseEvent } from "react";

type Props = {
  itemId: number;
  count: number;
  name?: string;
  selected?: boolean;
  onLeftDown?: (shiftKey: boolean) => void;
  onRightDown?: () => void;
  onDoubleClick?: () => void;
};

// アイコン・個数・ホバーツールチップ付きの汎用アイテムスロット
// Generic item slot with icon, count, and a hover tooltip
export default function ItemSlot({ itemId, count, name, selected, onLeftDown, onRightDown, onDoubleClick }: Props) {
  const onMouseDown = (e: MouseEvent) => {
    e.preventDefault();
    if (e.button === 0) onLeftDown?.(e.shiftKey);
    if (e.button === 2) onRightDown?.();
  };

  const hasItem = itemId > 0 && count > 0;

  return (
    <div
      className={`group relative w-12 h-12 border rounded bg-gray-900 select-none ${
        selected ? "border-yellow-400" : "border-gray-700"
      }`}
      onMouseDown={onMouseDown}
      onDoubleClick={onDoubleClick}
      onContextMenu={(e) => e.preventDefault()}
    >
      {hasItem ? (
        <>
          <img src={`/api/icons/${itemId}.png`} alt={name ?? `item ${itemId}`} className="w-full h-full object-contain p-0.5" draggable={false} />
          <span className="absolute bottom-0 right-0.5 text-xs text-green-300 font-bold drop-shadow">{count}</span>
          {name ? (
            <span className="pointer-events-none absolute bottom-full left-1/2 -translate-x-1/2 mb-1 hidden group-hover:block whitespace-nowrap bg-black/90 text-white text-xs rounded px-2 py-1 z-20">
              {name}
            </span>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
```

- [ ] **Step 4: 型チェック**

```bash
cd moorestech_web/webui && ../node/mac-arm64/pnpm exec tsc --noEmit && cd ../..
```

Expected: エラーなし。

- [ ] **Step 5: コミット**

```bash
git add moorestech_web/webui/src/
git commit -m "WebUI: 手書きTS型・アイテムマスタフック・汎用ItemSlotを追加"
```

---

### Task 15: InventoryPanel（インベントリ操作UI）

**Files:**
- Create: `moorestech_web/webui/src/components/InventoryPanel.tsx`
- Delete: `moorestech_web/webui/src/components/InventoryView.tsx`
- Modify: `moorestech_web/webui/src/App.tsx`

**操作仕様（uGUIのPlayerInventoryViewControllerと同等）:**

| 操作 | grab空 | grab保持中 |
|---|---|---|
| 左クリック | スロット全量→grab（Shift時は直接移動） | grab全量→スロット |
| 右クリック | スロット半分→grab（`inventory.split`） | grab→スロットへ1個 |
| ダブルクリック | 同種をスロットへ収集 | 同種をgrabへ収集 |

Shift+左クリック（直接移動）はWeb側で移動先を決定する: 反対エリア（main↔hotbar）の「同種でmaxStack未満」のスロットを先に、なければ空スロットを探し、`inventory.move_item` を発行する。

- [ ] **Step 1: InventoryPanel.tsx を作成**

```tsx
import { useEffect, useState } from "react";
import { useTopic } from "../bridge/useTopic";
import { useItemMaster } from "../bridge/useItemMaster";
import { dispatchAction } from "../bridge/actions";
import type { InventoryArea, PlayerInventoryData, SlotData, SlotRef } from "../types/inventory";
import ItemSlot from "./ItemSlot";

const GRAB: SlotRef = { area: "grab", slot: 0 };

// プレイヤーインベントリ（メイン4行+ホットバー1行+grab）の表示と操作
// Player inventory view & interactions: 4 main rows, 1 hotbar row, and the grab stack
export default function InventoryPanel() {
  const inventory = useTopic<PlayerInventoryData>("local_player.inventory");
  const itemMaster = useItemMaster();
  const [mousePos, setMousePos] = useState({ x: 0, y: 0 });

  useEffect(() => {
    const onMove = (e: globalThis.MouseEvent) => setMousePos({ x: e.clientX, y: e.clientY });
    window.addEventListener("mousemove", onMove);
    return () => window.removeEventListener("mousemove", onMove);
  }, []);

  if (!inventory) {
    return <div className="text-sm text-gray-400">connecting...</div>;
  }

  const grabHeld = inventory.grab.count > 0;

  const onLeftDown = (ref: SlotRef, slot: SlotData, shiftKey: boolean) => {
    if (grabHeld) {
      void dispatchAction("inventory.move_item", { from: GRAB, to: ref, count: inventory.grab.count });
      return;
    }
    if (slot.count === 0) return;
    if (shiftKey) {
      directMove(ref, slot);
      return;
    }
    void dispatchAction("inventory.move_item", { from: ref, to: GRAB, count: slot.count });
  };

  const onRightDown = (ref: SlotRef, slot: SlotData) => {
    if (grabHeld) {
      void dispatchAction("inventory.move_item", { from: GRAB, to: ref, count: 1 });
      return;
    }
    if (slot.count === 0) return;
    void dispatchAction("inventory.split", { from: ref });
  };

  const onDoubleClick = (ref: SlotRef, slot: SlotData) => {
    if (!grabHeld && slot.count === 0) return;
    void dispatchAction("inventory.collect", { target: grabHeld ? GRAB : ref });
  };

  // Shift+クリック: 反対エリアの同種スタック→空スロットの順で移動先を探す
  // Shift-click: prefer a same-item stack in the opposite area, then an empty slot
  const directMove = (from: SlotRef, slot: SlotData) => {
    const targetArea: InventoryArea = from.area === "hotbar" ? "main" : "hotbar";
    const targetSlots = targetArea === "main" ? inventory.mainSlots : inventory.hotbarSlots;
    const maxStack = itemMaster?.get(slot.itemId)?.maxStack ?? Infinity;

    const stackable = targetSlots.findIndex((s) => s.itemId === slot.itemId && s.count < maxStack);
    const empty = targetSlots.findIndex((s) => s.count === 0);
    const target = stackable >= 0 ? stackable : empty;
    if (target < 0) return;
    void dispatchAction("inventory.move_item", { from, to: { area: targetArea, slot: target }, count: slot.count });
  };

  const renderSlot = (area: InventoryArea, index: number, slot: SlotData) => {
    const ref: SlotRef = { area, slot: index };
    return (
      <ItemSlot
        key={`${area}-${index}`}
        itemId={slot.itemId}
        count={slot.count}
        name={itemMaster?.get(slot.itemId)?.name}
        onLeftDown={(shiftKey) => onLeftDown(ref, slot, shiftKey)}
        onRightDown={() => onRightDown(ref, slot)}
        onDoubleClick={() => onDoubleClick(ref, slot)}
      />
    );
  };

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <h2 className="text-lg font-semibold">Inventory</h2>
        <button
          onClick={() => void dispatchAction("inventory.sort", {})}
          className="bg-gray-700 hover:bg-gray-600 text-sm rounded px-3 py-1"
        >
          Sort
        </button>
      </div>
      <div className="grid grid-cols-9 gap-1 w-fit">
        {inventory.mainSlots.map((s, i) => renderSlot("main", i, s))}
      </div>
      <div className="grid grid-cols-9 gap-1 w-fit pt-1 border-t border-gray-600">
        {inventory.hotbarSlots.map((s, i) => renderSlot("hotbar", i, s))}
      </div>
      {grabHeld ? (
        <div
          className="pointer-events-none fixed z-40 w-12 h-12"
          style={{ left: mousePos.x - 24, top: mousePos.y - 24 }}
        >
          <ItemSlot itemId={inventory.grab.itemId} count={inventory.grab.count} />
        </div>
      ) : null}
    </div>
  );
}
```

- [ ] **Step 2: InventoryView.tsx を削除し App.tsx を更新**

```bash
rm moorestech_web/webui/src/components/InventoryView.tsx
```

`App.tsx`:

```tsx
import InventoryPanel from "./components/InventoryPanel";
import ToastHost from "./components/ToastHost";
import DebugActionButton from "./components/DebugActionButton";

export default function App() {
  return (
    <div className="p-4 space-y-4">
      <h1 className="text-2xl font-bold">moorestech Web UI</h1>
      <DebugActionButton />
      <InventoryPanel />
      <ToastHost />
    </div>
  );
}
```

- [ ] **Step 3: 型チェック**

```bash
cd moorestech_web/webui && ../node/mac-arm64/pnpm exec tsc --noEmit && cd ../..
```

Expected: エラーなし。

- [ ] **Step 4: コミット**

```bash
git add moorestech_web/webui/src/
git commit -m "WebUI: インベントリ操作パネルを実装しInventoryViewを置換"
```

---

### Task 16: CraftPanel（クラフトUI）

**Files:**
- Create: `moorestech_web/webui/src/components/CraftPanel.tsx`
- Modify: `moorestech_web/webui/src/App.tsx`

- [ ] **Step 1: CraftPanel.tsx を作成**

```tsx
import { useState } from "react";
import { useTopic } from "../bridge/useTopic";
import { useItemMaster } from "../bridge/useItemMaster";
import { dispatchAction } from "../bridge/actions";
import type { CraftRecipe, CraftRecipesData } from "../types/crafting";
import type { PlayerInventoryData } from "../types/inventory";
import ItemSlot from "./ItemSlot";

// アンロック済みレシピの一覧表示とクラフト実行
// Lists unlocked recipes and executes crafts
export default function CraftPanel() {
  const recipes = useTopic<CraftRecipesData>("crafting.recipes");
  const inventory = useTopic<PlayerInventoryData>("local_player.inventory");
  const itemMaster = useItemMaster();
  const [selectedGuid, setSelectedGuid] = useState<string | null>(null);

  if (!recipes || !inventory) {
    return <div className="text-sm text-gray-400">connecting...</div>;
  }

  // インベントリ全域（main+hotbar+grab）の所持数を集計する
  // Tally item counts across main, hotbar, and grab
  const counts = new Map<number, number>();
  const addCount = (itemId: number, count: number) => {
    if (count > 0) counts.set(itemId, (counts.get(itemId) ?? 0) + count);
  };
  inventory.mainSlots.forEach((s) => addCount(s.itemId, s.count));
  inventory.hotbarSlots.forEach((s) => addCount(s.itemId, s.count));
  addCount(inventory.grab.itemId, inventory.grab.count);

  const isCraftable = (recipe: CraftRecipe) =>
    recipe.requiredItems.every((r) => (counts.get(r.itemId) ?? 0) >= r.count);

  const selected = recipes.recipes.find((r) => r.recipeGuid === selectedGuid) ?? null;

  const onCraft = () => {
    if (!selected || !isCraftable(selected)) return;
    void dispatchAction("craft.execute", { recipeGuid: selected.recipeGuid });
  };

  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold">Craft</h2>
      <div className="grid grid-cols-9 gap-1 w-fit">
        {recipes.recipes.map((r) => (
          <div key={r.recipeGuid} className={isCraftable(r) ? "" : "opacity-40"}>
            <ItemSlot
              itemId={r.resultItemId}
              count={r.resultCount}
              name={itemMaster?.get(r.resultItemId)?.name}
              selected={r.recipeGuid === selectedGuid}
              onLeftDown={() => setSelectedGuid(r.recipeGuid)}
            />
          </div>
        ))}
      </div>
      {selected ? (
        <div className="space-y-2">
          <div className="text-sm text-gray-300">
            {itemMaster?.get(selected.resultItemId)?.name ?? `item ${selected.resultItemId}`} ×{selected.resultCount}
          </div>
          <div className="flex gap-1 items-center">
            {selected.requiredItems.map((r, i) => (
              <div key={i} className={(counts.get(r.itemId) ?? 0) >= r.count ? "" : "opacity-40"}>
                <ItemSlot itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} />
              </div>
            ))}
            <button
              onClick={onCraft}
              disabled={!isCraftable(selected)}
              className="ml-3 bg-blue-700 hover:bg-blue-600 disabled:bg-gray-700 disabled:text-gray-500 text-sm rounded px-4 py-2"
            >
              Craft
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
```

- [ ] **Step 2: App.tsx に組み込み**

```tsx
import InventoryPanel from "./components/InventoryPanel";
import CraftPanel from "./components/CraftPanel";
import ToastHost from "./components/ToastHost";
import DebugActionButton from "./components/DebugActionButton";

export default function App() {
  return (
    <div className="p-4 space-y-6">
      <h1 className="text-2xl font-bold">moorestech Web UI</h1>
      <DebugActionButton />
      <div className="flex gap-10 flex-wrap">
        <InventoryPanel />
        <CraftPanel />
      </div>
      <ToastHost />
    </div>
  );
}
```

- [ ] **Step 3: 型チェック**

```bash
cd moorestech_web/webui && ../node/mac-arm64/pnpm exec tsc --noEmit && cd ../..
```

Expected: エラーなし。

- [ ] **Step 4: コミット**

```bash
git add moorestech_web/webui/src/
git commit -m "WebUI: クラフトパネルを実装"
```

---

### Task 17: Phase 1 E2E 検証と検証ドキュメント更新

**Files:**
- Modify: `docs/web-ui-verification.md`

- [ ] **Step 1: Play mode 起動（web-ui-verification.md の自動検証 手順1〜3）**

- [ ] **Step 2: テストアイテムを投入**

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var ctrl = Client.Game.InGame.Context.ClientDIContext.DIContainer
    .DIContainerResolver
    .Resolve<Client.Game.InGame.UI.Inventory.Main.LocalPlayerInventoryController>();
var factory = Game.Context.ServerContext.ItemStackFactory;
ctrl.SetMainItem(0, factory.Create(new Core.Master.ItemId(1), 10));
ctrl.SetMainItem(1, factory.Create(new Core.Master.ItemId(1), 3));
'
```

- [ ] **Step 3: ws パッケージを一時追加し、action E2E スクリプトを実行**

```bash
cd moorestech_web/webui && ../node/mac-arm64/pnpm add -D ws && cd ../..
```

`/tmp/ws-phase1-verify.mjs` を作成:

```js
import WebSocket from 'ws';
const ws = new WebSocket('ws://127.0.0.1:5173/ws', {
  headers: { Origin: 'http://localhost:5173' }
});
const send = (o) => ws.send(JSON.stringify(o));
ws.on('open', () => {
  console.log('OPEN');
  send({ op: 'subscribe', topics: ['local_player.inventory', 'crafting.recipes'] });
  // slot0(10個) の半分を grab へ → main slot2 へ全量配置 → slot2 を起点に同種収集
  setTimeout(() => send({ op: 'action', type: 'inventory.split', requestId: 's1', payload: { from: { area: 'main', slot: 0 } } }), 1000);
  setTimeout(() => send({ op: 'action', type: 'inventory.move_item', requestId: 's2', payload: { from: { area: 'grab', slot: 0 }, to: { area: 'main', slot: 2 }, count: 5 } }), 2000);
  setTimeout(() => send({ op: 'action', type: 'inventory.collect', requestId: 's3', payload: { target: { area: 'main', slot: 2 } } }), 3000);
  // 範囲外スロットはエラーになること
  setTimeout(() => send({ op: 'action', type: 'inventory.move_item', requestId: 's4', payload: { from: { area: 'main', slot: 99 }, to: { area: 'grab', slot: 0 }, count: 1 } }), 4000);
});
ws.on('message', (d) => console.log('MSG', String(d).slice(0, 400)));
setTimeout(() => process.exit(0), 8000);
```

```bash
cd moorestech_web/webui && ../node/mac-arm64/bin/node /tmp/ws-phase1-verify.mjs && cd ../..
```

Expected（要点）:
- 初回 `snapshot` に `mainSlots`（36要素・slot0=count10・slot1=count3）、`hotbarSlots`（9要素）、`grab` が含まれる
- `crafting.recipes` の `snapshot` に `recipes` 配列（アンロック済みレシピ）が含まれる
- `result s1 ok:true` → 続く `event` で `mainSlots[0].count=5`、`grab.count=5`
- `result s2 ok:true` → `event` で `mainSlots[2].count=5`、`grab.count=0`
- `result s3 ok:true` → `event` で slot0/slot1 の同種アイテムが slot2 に集約される
- `result s4 ok:false error:"invalid_slot"`

- [ ] **Step 4: craft.execute を検証**

`crafting.recipes` のsnapshotから素材が揃えられるレシピを1つ選び（または `SetMainItem` で素材を投入し）、`recipeGuid` を使って:

```js
// /tmp/ws-craft-verify.mjs （recipeGuid は手順3の snapshot から転記）
import WebSocket from 'ws';
const ws = new WebSocket('ws://127.0.0.1:5173/ws', { headers: { Origin: 'http://localhost:5173' } });
ws.on('open', () => {
  ws.send(JSON.stringify({ op: 'subscribe', topics: ['local_player.inventory'] }));
  ws.send(JSON.stringify({ op: 'action', type: 'craft.execute', requestId: 'c1', payload: { recipeGuid: '<GUID>' } }));
});
ws.on('message', (d) => console.log('MSG', String(d).slice(0, 300)));
setTimeout(() => process.exit(0), 8000);
```

Expected: `result c1 ok:true` の後、サーバー経由のインベントリ更新eventで素材が減り成果物が増える。

- [ ] **Step 5: ブラウザで実操作確認**

`http://localhost:5173/` を開き以下を確認（ユーザーに目視依頼可）:
- アイコン付きでインベントリが表示される
- クリックで取る/置く、右クリック半分/1個置き、ダブルクリック収集、Shift+クリック直接移動、Sortボタン
- クラフトパネルでレシピ選択→Craftボタンでクラフト成立
- 不正操作（範囲外等）はトーストが出る

- [ ] **Step 6: 後片付け**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
cd moorestech_web/webui && ../node/mac-arm64/pnpm remove ws && git checkout -- package.json pnpm-lock.yaml && cd ../..
rm -f /tmp/ws-phase1-verify.mjs /tmp/ws-craft-verify.mjs
```

- [ ] **Step 7: web-ui-verification.md に E2E レシピを追記**

「自動検証」節の末尾（手順8の後）に以下を追記する:

```markdown
### 9. action（双方向API）の検証

Phase 0 以降は WS 上で `action` op が使える。疎通確認:

​```js
// /tmp/ws-action-verify.mjs
import WebSocket from 'ws';
const ws = new WebSocket('ws://127.0.0.1:5173/ws', { headers: { Origin: 'http://localhost:5173' } });
ws.on('open', () => {
  ws.send(JSON.stringify({ op: 'action', type: 'debug.echo', requestId: 'a1', payload: { hello: 'world' } }));
});
ws.on('message', (d) => console.log('MSG', String(d)));
setTimeout(() => process.exit(0), 8000);
​```

期待: `{"op":"result","requestId":"a1","ok":true}`。

インベントリ操作系は `inventory.move_item` / `inventory.split` / `inventory.collect` / `inventory.sort` / `craft.execute` が登録済み。
payload 形式は `docs/superpowers/specs/2026-06-12-webui-migration-design.md` §2 を参照。

### 10. アイコン・マスタ配信の検証

​```bash
curl -s -o /tmp/icon.png -w "%{http_code} %{content_type}\n" http://127.0.0.1:5173/api/icons/1.png   # 200 image/png
curl -s http://127.0.0.1:5173/api/master/items | head -c 200                                          # {"items":[{"itemId":...
​```
```

（コードフェンスはドキュメント内ではゼロ幅文字なしの通常の ``` を使うこと）

- [ ] **Step 8: コミット**

```bash
git add docs/web-ui-verification.md
git commit -m "WebUI: 双方向API・アイコン配信のE2E検証手順を追記"
```

---

### Task 18: CefUnity スモークテスト（探索タスク）

**Files:** なし（検証と記録のみ）

**目的:** 「ゲーム内テクスチャにWeb UIが表示されるか」の早期確認。**失敗してもPhase 1の完了は妨げない**（未知をPhase 3に持ち越さないための偵察）。結果は成否にかかわらず記録する。

- [ ] **Step 1: Play mode + Web UI ready 状態にする**（Task 17 Step 1と同じ）

- [ ] **Step 2: シーン内の CefUnity オブジェクトを特定**

MainGameUI.prefab 内に無効化済みの「CefUnity」GameObjectがある（URL=`https://google.com`、RawImage接続済み）。実行中シーンから探す:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
{
    if (go.name != "CefUnity" || !go.scene.IsValid()) continue;
    UnityEngine.Debug.Log($"[smoke] found CefUnity active={go.activeSelf} path={GetPath(go.transform)}");
    foreach (var comp in go.GetComponents<UnityEngine.Component>())
        UnityEngine.Debug.Log($"[smoke] component: {comp.GetType().FullName}");
}
string GetPath(UnityEngine.Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
'
```

- [ ] **Step 3: URL を localhost に書き換えて有効化**

Step 2のログでブラウザコンポーネントの型名とフィールドを把握した上で、reflectionでURLフィールド（`url` 等）を `http://localhost:5173/` に書き換え、`SetActive(true)` する:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
{
    if (go.name != "CefUnity" || !go.scene.IsValid()) continue;
    foreach (var comp in go.GetComponents<UnityEngine.Component>())
    {
        var f = comp.GetType().GetField("url", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (f == null) continue;
        f.SetValue(comp, "http://localhost:5173/");
        UnityEngine.Debug.Log($"[smoke] url set on {comp.GetType().Name}");
    }
    go.SetActive(true);
    UnityEngine.Debug.Log("[smoke] CefUnity activated");
}
'
```

※ フィールド名が `url` でない場合はStep 2の出力に合わせて調整する。

- [ ] **Step 4: 結果確認**

```bash
uloop get-logs --project-path ./moorestech_client --log-type Error    # CEF関連エラーの有無
```

GameViewのスクリーンショットでWeb UI（インベントリグリッド）がゲーム内に描画されているかを確認する（`UnityEngine.ScreenCapture.CaptureScreenshot` をdynamic-codeで実行し画像を確認）。

- [ ] **Step 5: 結果を記録**

`docs/cef-webui-plan.md` の「5. 未解決・リスク」節の直前に1段落（スモークテスト実施日・成否・観察事項：表示可否、入力、色味、性能感）を追記する。

- [ ] **Step 6: 後片付けとコミット**

Play modeを停止（シーン上の変更は揮発するためprefabは未変更のまま）。

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
git add docs/cef-webui-plan.md
git commit -m "WebUI: CefUnityスモークテスト結果を記録"
```

---

### Task 19: 仕上げ（master逆マージと最終確認）

**Files:** なし

- [ ] **Step 1: master を逆マージ**

```bash
git fetch origin
git merge origin/master    # コンフリクトがあれば解消（uGUI側は原則そのまま受け入れる）
uloop compile --project-path ./moorestech_client
```

- [ ] **Step 2: テスト全体の回帰確認**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WebUi"
```

Expected: WebUiJsonTest / InventoryAreaMapperTest が全てPASS。

- [ ] **Step 3: 未コミット差分がないことを確認**

```bash
git status   # クリーンであること
git log --oneline master..HEAD   # Phase 0/1 のコミットが揃っていること
```

- [ ] **Step 4: ユーザーへ報告**

Phase 0/1の完了内容・CEFスモーク結果・スコープ外（ドラッグ均等分配）を報告し、Phase 2（GameScreen HUD移植）への着手判断を仰ぐ。以降、週次目安で `git merge origin/master` を継続する。
