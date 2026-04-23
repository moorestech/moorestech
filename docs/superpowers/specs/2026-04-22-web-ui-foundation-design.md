# moorestech Web UI 基盤 設計仕様

**作成日:** 2026-04-22
**対象タスク:** CEF Web UI 導入計画 の先頭3タスク
- ASP.NET（HTTP/WS サーバー）の導入
- pnpm / TypeScript / React の導入
- Unity ↔ Web の初期化パイプライン構築

**参照:** `docs/cef-webui-plan.md`

---

## 1. スコープと完了条件

### 対象
- Unity クライアントに HTTP + WebSocket サーバー（ASP.NET Core 2.3 / Kestrel）を同居
- TypeScript + React + Vite の Web UI プロジェクトを新設
- Unity プロセスから Node.js (Vite dev server) を spawn し、両サーバーの起動・終了を一貫管理
- WebSocket 上でトピック購読モデルのプロトコルを定義
- デモ対象としてプレイヤーインベントリを WS 配信し、React 側で一覧表示

### 非対象（後続スペックに委譲）
- CEF 統合（本スペック完了時点ではユーザーが Chrome 等で `http://localhost:5173/` を開いて確認する）
- C# → TS の型生成 SourceGenerator
- アイテムアイコン配信
- インベントリ以外の UI 移植
- 本番ビルド時の挙動（`vite build` 成果物の配信、Node バイナリのプラットフォーム別配布など）
- CSRF トークン等の本格ハードニング
- ポート衝突時のフォールバック
- Node orphan プロセスの OS 別対処（Windows Job Object 等）

### 完了条件
Unity クライアントを起動すると、内部で Kestrel と Vite dev server が自動起動し、ブラウザで `http://localhost:5173/` を開くと React 製の画面にローカルプレイヤーのインベントリがリアルタイム表示される。アイテムを取得・消費するとブラウザ側の表示が即時更新される。

---

## 2. リポジトリ構成

```
moorestech/
├── moorestech_client/                          (既存 Unity クライアント)
│   └── Assets/Scripts/Client.WebUiHost/
│       ├── Client.WebUiHost.asmdef
│       ├── WebUiHostBehaviour.cs               (仮コード、feature/webui-host-code ブランチ上に存在。本スペックでは触らない)
│       │
│       ├── Boot/                               ← 初期化系・ゲームデータ非依存
│       │   ├── WebUiHost.cs                    外向き Facade（静的）
│       │   ├── KestrelServer.cs                Kestrel 起動/停止
│       │   ├── ViteProcess.cs                  Node spawn / ready 監視 / kill
│       │   ├── WebSocketHub.cs                 WS 接続管理・トピックレジストリ
│       │   ├── WebUiEndpoints.cs               /ws, /api ルーティング
│       │   └── WebUiPaths.cs                   moorestech_web/ 絶対パス解決
│       │
│       └── Game/                               ← ゲーム系・ClientContext 依存
│           ├── WebUiGameBinder.cs              外向き Facade（静的）
│           └── Topics/
│               └── InventoryTopic.cs           インベントリ → WS 配信
│
│   └── Assets/Scripts/Client.Game/Common/
│       ├── GameInitializedEvent.cs             (既存)
│       └── GameShutdownEvent.cs                ← 新規（本スペックで追加）
│
│   └── Assets/Scripts/Client.Starter/
│       └── InitializeScenePipeline.cs          (既存・hook 追加)
│
│   └── Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/
│       └── BackToMainMenu.cs                   (既存・1行追加)
│
├── moorestech_server/                          (既存、本スペックでは変更なし)
│
├── moorestech_web/                             ← 新規
│   ├── webui/                                  TS/React プロジェクト
│   │   ├── package.json
│   │   ├── pnpm-lock.yaml
│   │   ├── .npmrc                              node-linker=hoisted
│   │   ├── vite.config.ts
│   │   ├── tsconfig.json
│   │   ├── index.html
│   │   ├── src/
│   │   │   ├── main.tsx
│   │   │   ├── App.tsx
│   │   │   ├── bridge/
│   │   │   │   ├── webSocketClient.ts          WS 接続・再接続・envelope 送受信
│   │   │   │   └── useTopic.ts                 React フック
│   │   │   └── components/
│   │   │       └── InventoryView.tsx           インベントリ表示
│   │   └── node_modules/                       (.gitignore)
│   │
│   └── node/                                   Node.js バイナリ同梱
│       ├── mac-arm64/node
│       ├── mac-x64/node
│       ├── win-x64/node.exe
│       └── linux-x64/node
│
└── docs/
    ├── cef-webui-plan.md                       (既存)
    └── superpowers/specs/
        └── 2026-04-22-web-ui-foundation-design.md  (本ドキュメント)
```

---

## 3. アーキテクチャ

### 3.1 プロセス・ポート構成

```
[ Unity Client プロセス (moorestech_client) ]
  │
  ├── Kestrel (ASP.NET Core 2.3 / netstandard2.0)
  │     └── 127.0.0.1:5050
  │           ├── HTTP:  /api/*     (本スペックでは未使用、枠のみ確保)
  │           └── WS:    /ws        (トピック購読・配信)
  │
  └── Process.Start → Node (同梱) → Vite dev server
                                        └── 127.0.0.1:5173
                                              ├── /        → React アプリ
                                              ├── /src/*   → TSX on-the-fly transform
                                              ├── /api/*   → proxy → 127.0.0.1:5050
                                              └── /ws      → proxy → 127.0.0.1:5050/ws

[ ユーザー ] → Chrome → http://127.0.0.1:5173/
```

### 3.2 採用技術

| 領域 | 選択 | 理由 |
|---|---|---|
| HTTP/WS サーバー | ASP.NET Core 2.3 (Kestrel, netstandard2.0 LTS) | 既存 `WebUiHostBehaviour` で動作確認済み。Unity 6 + NuGetForUnity で利用可能 |
| JS ランタイム同梱 | Node.js LTS バイナリ | `moorestech_web/node/<platform>/`。公式バイナリをそのまま同梱 |
| パッケージマネージャ | pnpm（`node-linker=hoisted`） | 厳格な依存解決。hoisted 指定で symlink / ジャンクションを作らず配布互換性を確保。pnpm 本体は `@pnpm/exe` 相当のスタンドアロンバイナリを `moorestech_web/node/<platform>/pnpm` として同梱する前提（`node` と同じ階層） |
| ビルドツール | Vite（dev server 常時運用） | 本スペック γ 方針に従い、本番相当環境でも `pnpm dev` を走らせる |
| フレームワーク | React 18 + TypeScript 5 | 試作で確認済み |
| スタイル | Tailwind CSS | 試作で確認済み（`cn()` + Tailwind クラス） |
| 状態管理 | React hooks（useState / useEffect） | 本スペックでは追加ライブラリ不要 |
| WS クライアント | ブラウザ標準 `WebSocket` + 薄いラッパ | 追加ライブラリ不要 |

### 3.3 なぜ γ（本番でも `pnpm dev`）を選んだか

CEF Web UI 導入計画の根幹に「mod が HTML/CSS/TS で UI を差し替えられる」という要件がある。配布環境でも Vite dev server を走らせていれば、mod は `.tsx` を投げ込むだけで Vite がその場で変換して配信できる。本スペックでは mod 機能までは実装しないが、将来 mod 対応を素直に載せられる基盤にするため `pnpm dev` 常時運用を採用する。

**リスクの受け入れ:**
- Mod が C# で任意コード実行できる以上、TS 動的実行による追加攻撃面はない（=既存の mod モデルと同じ信頼境界）
- `node_modules` サイズ・Node バイナリ同梱による配布サイズ増は PC ターゲットでは許容
- pnpm の symlink 問題は `node-linker=hoisted` で回避
- orphan プロセス対処は後続スペックに委譲

---

## 4. ライフサイクル（Unity マスター）

Unity を親プロセスとして扱い、Kestrel と Vite のライフタイムを Unity セッションに同期させる。ライフサイクルの発火点は既存のゲームイベント（`GameInitializedEvent`、`GameShutdownEvent`）にあわせ、`Application.quitting` には依存しない。

### 4.1 起動シーケンス

```
InitializeScenePipeline.Initialize()
  │
  ├─ [新規] WebUiHost.StartAsync()
  │    ├─ KestrelServer.Start()        → 127.0.0.1:5050 bind、/ws WebSocket accept ready
  │    └─ ViteProcess.Start()
  │         ├─ platform 判定 → moorestech_web/node/<platform>/node を選択
  │         ├─ cwd = moorestech_web/webui
  │         ├─ node_modules 不在なら pnpm install を先に実行
  │         ├─ spawn: node <vite-cli> --port 5173 --strictPort
  │         └─ stdout を監視し "ready in" を検出するまで await
  │
  ├─ [新規] GameShutdownEvent.OnGameShutdown.Subscribe(_ => WebUiHost.Stop())
  │
  ├─ （既存）Addressables.InitializeAsync
  ├─ （既存）WhenAll(VanillaApi, ModAssets, SceneLoad)
  ├─ （既存）new ClientContext(...)
  │
  ├─ [新規] WebUiGameBinder.BindTopics()
  │    └─ InventoryTopic.Register(WebUiHost.Hub)
  │         └─ LocalPlayerInventoryController の変更通知を購読
  │
  └─ （既存）シーン遷移 → MainGameStarter.StartGame → GameInitializedEvent.FireGameInitialized()
```

**ポイント:**
- `WebUiHost.StartAsync()` は `Initialize()` の最序盤に置く。ローディング中でもブラウザで `http://localhost:5173/` が開ける
- `WebUiGameBinder.BindTopics()` は `ClientContext` 生成後に呼ぶ。トピックハンドラが `ClientContext` 経由のゲーム状態にアクセスできる必要があるため
- 新規処理は MonoBehaviour を導入しない。pure C# で `InitializeScenePipeline` から呼び出す

### 4.2 終了シーケンス

ゲーム終了の単一窓口は既存の `BackToMainMenu.Disconnect()`（ボタン / `OnDestroy` / `OnApplicationQuit` の 3 経路すべてが通る）。

**新規イベント `GameShutdownEvent`**（`GameInitializedEvent` と対称）:
```csharp
namespace Client.Game.Common
{
    public static class GameShutdownEvent
    {
        private static readonly Subject<Unit> _onGameShutdown = new();
        public static IObservable<Unit> OnGameShutdown => _onGameShutdown;
        public static void FireGameShutdown()
        {
            _onGameShutdown.OnNext(Unit.Default);
        }
    }
}
```

**発火点:** `BackToMainMenu.Disconnect()` の末尾に `GameShutdownEvent.FireGameShutdown()` を追加。

**購読側の動作（`WebUiHost.Stop()`）:**
```
WebUiHost.Stop()
  ├─ WebSocketHub.CloseAll()        全接続に close フレーム送信
  ├─ KestrelServer.StopAsync(2s)    graceful shutdown、最大 2 秒待機
  └─ ViteProcess.Kill()             Node プロセスに SIGTERM（失敗時は強制終了）
```

### 4.3 失敗時の挙動（本スペック範囲）

- Node バイナリが存在しない / 起動失敗 → `Debug.LogError` を出し、Unity 本体の初期化は続行する（Web UI は死ぬがゲームは動く）
- Kestrel の bind 失敗（ポート競合等）→ 同上。後続スペックでフォールバック採番を実装する
- `pnpm install` 失敗 → 同上
- Vite の ready 出力が一定時間来ない（タイムアウト 30 秒）→ 同上

いずれもゲーム本体の起動は止めない。エラーはログに出すだけ。

---

## 5. Client.WebUiHost の責務分割

`Client.WebUiHost` は 2 つのサブ領域に分割する。両者とも pure C#（MonoBehaviour 不使用）。

### 5.1 Boot 層（`Client.WebUiHost/Boot/`）

ゲームデータに依存しない。Kestrel・Vite・WebSocket インフラだけを扱う。

- **`WebUiHost`（static facade）**: 起動・停止の入口。`StartAsync()`、`Stop()`、および `Hub`（`WebSocketHub` インスタンス）プロパティを公開
- **`KestrelServer`**: Kestrel の `IWebHost` 起動・停止ラッパ。ルーティング設定は `WebUiEndpoints` に委譲
- **`ViteProcess`**: Node プロセスの spawn、stdout の ready 検出、kill を担当。プラットフォーム別バイナリパスの選別も含む
- **`WebSocketHub`**: WebSocket 接続ごとの購読 topic set を保持。トピックハンドラを名前で登録 / 配信
- **`WebUiEndpoints`**: `/ws` と `/api` のルーティング定義。`UseWebSockets()` の有効化と `HttpContext.WebSockets.AcceptWebSocketAsync()` による接続受付
- **`WebUiPaths`**: `Application.dataPath` から `moorestech_web/` への絶対パスを解決するユーティリティ

### 5.2 Game 層（`Client.WebUiHost/Game/`）

`ClientContext` 生成後に初めて使える。ゲーム状態を読み取って WS に流す責務を持つ。

- **`WebUiGameBinder`（static facade）**: `BindTopics()` を公開。内部で各トピックを `WebUiHost.Hub` に登録
- **`Topics/InventoryTopic`**: `LocalPlayerInventoryController` の変更通知を購読し、`WebSocketHub` 経由で `"local_player.inventory"` トピックの購読者に `snapshot` / `event` メッセージを配信

---

## 6. WebSocket プロトコル

### 6.1 エンドポイント

- URL: `ws://localhost:5173/ws`（Vite proxy 経由で Kestrel `ws://localhost:5050/ws` に転送）
- オリジン検査: `Origin` ヘッダが `http://localhost:5173` または `http://127.0.0.1:5173` 以外の場合は WebSocket upgrade を拒否（HTTP 403）
- サブプロトコル: 指定なし

### 6.2 メッセージ envelope

すべて UTF-8 JSON テキストフレーム。

**Web → Unity（クライアント発信）:**
```ts
type ClientMsg =
  | { op: "subscribe";   topics: string[] }
  | { op: "unsubscribe"; topics: string[] }
  | { op: "snapshot";    topic: string }
```

**Unity → Web（サーバー発信）:**
```ts
type ServerMsg =
  | { op: "snapshot"; topic: string; data: unknown }
  | { op: "event";    topic: string; data: unknown }
  | { op: "error";    message: string }
```

### 6.3 購読セマンティクス

- クライアントが `subscribe` を送ると、サーバーは即座に該当トピックの現在値を `snapshot` として返す
- 以降、そのトピックに変化が起きるたびにサーバーは `event` を push する
- `unsubscribe` で停止。接続が切れたら自動 unsubscribe
- `snapshot` op（request 型）は「現在値を1回だけ再取得」したい場合の明示要求

### 6.4 再接続（クライアント側）

- WebSocket が closed になったら指数バックオフで再接続（100ms → 200ms → 400ms → ... 上限 5s）
- 再接続成功後、直前の購読トピックを自動で再 subscribe
- 再接続後の最初の `snapshot` で React 側の状態は上書きされる

---

## 7. デモ対象トピック: インベントリ

### 7.1 トピック名

`"local_player.inventory"`

### 7.2 Payload 型

```ts
type InventoryData = {
  mainSlots:   Array<{ itemId: string; count: number }>
  hotBarSlots: Array<{ itemId: string; count: number }>
}
```

- `itemId` は Guid 文字列（空スロットは空 Guid `"00000000-0000-0000-0000-000000000000"` と `count: 0`）
- `mainSlots` / `hotBarSlots` の長さはインベントリ仕様に準ずる

### 7.3 配信ポリシー

- `subscribe` 到着時 → `InventoryTopic` が現在のインベントリ全体を `snapshot` で返す
- `LocalPlayerInventoryController` の変更通知に反応して `event` で全量 push（差分最適化は本スペック対象外）

### 7.4 React 側

```tsx
// components/InventoryView.tsx（概念コード）
const inventory = useTopic<InventoryData>("local_player.inventory");
if (!inventory) return <div>loading...</div>;
return (
  <div className="grid grid-cols-9 gap-1">
    {inventory.mainSlots.map((s, i) => (
      <div key={i} className="border p-2 text-xs">
        {s.count > 0 ? `${s.itemId.slice(0, 8)}... × ${s.count}` : ""}
      </div>
    ))}
  </div>
);
```

アイテムアイコン画像は後続スペックに委譲。本スペックでは Guid 文字列先頭 8 文字と個数のみ表示。

---

## 8. Main Thread Dispatch

Kestrel のリクエストハンドラは ThreadPool で動作する。Unity API（`ClientContext`、`LocalPlayerInventoryController` 等）は main thread からのアクセスを前提とするため、スレッド境界の marshalling が必要。

**採用手段:** UniTask の `UniTask.SwitchToMainThread()`。既存プロジェクトで広く使われており、独自の Dispatcher を追加しない。

**典型パターン:**
```csharp
// Kestrel ハンドラ内（ThreadPool で実行中）
await UniTask.SwitchToMainThread();
var inventory = ClientContext.LocalPlayerInventory;  // Unity API を main thread で呼ぶ
await UniTask.SwitchToThreadPool();                  // WS 送信は ThreadPool に戻す
await webSocket.SendAsync(payload, ...);
```

---

## 9. セキュリティ最低線

本スペック範囲での必須対応。

- **Bind アドレス:** Kestrel・Vite ともに `127.0.0.1` 厳守（`0.0.0.0` 禁止）
- **Vite `server.fs.allow`:** `['./src', './public']` に制限し、リポジトリ外や `node_modules` の流出を防ぐ
- **WS Origin 検査:** `Origin` ヘッダが `http://localhost:5173` または `http://127.0.0.1:5173` のみ許可
- **CSRF トークン等の本格的な保険は本スペック対象外**（γ 方針で受け入れ、mod 配布時に再検討）

---

## 10. 後続スペックに委譲する項目

本スペック完了後、以下を順次別スペックで扱う。

| 項目 | 内容 |
|---|---|
| CEF 統合 | moorestech_client に CEF を同梱し、ゲーム内で Web UI をテクスチャ合成 |
| C# → TS 型生成 SourceGenerator | 既存の Mooresmaster SourceGenerator と同系統で、API シグネチャとデータ型を TS へ |
| アイテムアイコン配信 | Kestrel の `/api/assets/icons/{itemId}` で PNG を返す経路 |
| 本番ビルド挙動 | `vite build` 成果物の静的配信、Unity Build Post-Processor での同梱、Node バイナリのプラットフォーム別選別、`pnpm install` の事前実行による初回起動短縮 |
| ポート衝突対処 | 固定 + フォールバック、または OS 採番（port 0）+ `runtime.json` による動的配布 |
| Node orphan 対処 | Windows Job Object、macOS の親 PID 監視、Linux `PR_SET_PDEATHSIG` |
| CSRF トークン | localhost バインドに対する追加の保険 |
| HMR / エラーオーバーレイの本番無効化 | 配布時の情報露出を抑える設定 |
| UI 移植（インベントリ以降） | 既存 uGUI の React 置き換え |
| 双方向 API 策定 | CEF の JS↔Native ブリッジに載せる RPC/イベント体系 |

---

## 11. 変更対象の既存ファイル（サマリ）

- `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`
  - `Initialize()` の最序盤に `WebUiHost.StartAsync()` と `GameShutdownEvent` 購読を追加
  - `new ClientContext(...)` 直後に `WebUiGameBinder.BindTopics()` を追加
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs`
  - `Disconnect()` 末尾に `GameShutdownEvent.FireGameShutdown()` を追加
- `moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs`
  - 新規作成
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/*.cs`
  - 新規作成（6 ファイル、`WebUiHost` facade ほか）
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/*.cs`
  - 新規作成（`WebUiGameBinder` と `InventoryTopic`）
- `moorestech_client/Assets/Scripts/Client.WebUiHost/WebUiHostBehaviour.cs`
  - **触らない**（仮コード、将来削除）
  - 現在 `feature/webui-host-code` ブランチにのみ存在。本スペックの実装ブランチで統合戦略（rebase / cherry-pick / 既存ブランチを捨てて再実装）を決める
- `moorestech_web/`（新規ディレクトリ、リポジトリルート直下）
  - `webui/` サブディレクトリに TS/React/Vite 一式
  - `node/` サブディレクトリに Node.js プラットフォーム別バイナリ
