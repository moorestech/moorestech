# Web UI 基盤 検証手順

moorestech Web UI 基盤（Kestrel + Vite dev server + WebSocket 購読）の動作確認手順と、最後に検証済みの期待挙動を記録する。

関連ドキュメント:
- 計画: `docs/cef-webui-plan.md`
- 設計: `docs/superpowers/specs/2026-04-22-web-ui-foundation-design.md`
- 実装プラン: `docs/superpowers/plans/2026-04-22-web-ui-foundation.md`

---

## 前提

- macOS / Linux / Windows の開発環境
- Unity 6000.3.8f1（moorestech_client の `ProjectVersion.txt` と一致）
- moorestech_master が `../moorestech_master` にクローン済み（`StringDebugParameters.json` で別パス指定も可）

### 初回セットアップ

```bash
# Node.js LTS + pnpm standalone を moorestech_web/node/<platform>/ にダウンロード
bash moorestech_web/setup.sh          # macOS / Linux
# または
.\moorestech_web\setup.ps1             # Windows
```

完了後:
- `moorestech_web/node/mac-arm64/bin/node`（または相当するプラットフォームディレクトリ）
- `moorestech_web/node/mac-arm64/pnpm`

が存在すること。いずれも `.gitignore` されているので commit されない。

---

## 手動検証（通常フロー）

### 1. Unity クライアント起動

```bash
uloop launch /path/to/moorestech_client
```

### 2. MainMenu シーンでローカルゲーム開始

- Unity Editor で Play mode 突入
- MainMenu 画面の「ローカルゲーム開始」ボタンを押下
- `InitializeScenePipeline.Initialize()` が走り、内部で以下が順に起動:
  - ASP.NET Kestrel（`http://127.0.0.1:5050`）
  - Node 経由の Vite dev server（`http://127.0.0.1:5173`）
  - `pnpm install` 未実行なら自動実行（初回のみ数十秒）

### 3. ログで ready 確認

Unity Console で:

```
[WebUiHost] Kestrel started at http://127.0.0.1:5050
[WebUiHost] spawned Vite (pid=XXXXX)
[Vite]   VITE v5.x.x  ready in XXX ms
[Vite]   ➜  Local:   http://127.0.0.1:5173/
[WebUiHost] ready. Open http://localhost:5173/
```

### 4. ブラウザで疎通確認

1. Chrome / Safari で `http://localhost:5173/` を開く
2. React 製の「moorestech Web UI / Main Inventory」画面が表示される
3. インベントリグリッドに各スロットのアイテム ID と数量が表示される
4. ゲーム内でアイテムを取得・消費・移動すると、ブラウザの表示が即時更新される

### 5. Play mode 終了 → クリーンアップ

- Unity の Stop ボタン、または MainMenu に戻るボタンを押す
- Unity Console で:

```
[WebUiHost] Vite process killed
[WebUiHost] Kestrel stopped
[WebUiHost] stopped
```

- ブラウザは WS 切断 → 指数バックオフで再接続試行（次回 Play mode 突入で自動再接続）
- `ps aux | grep vite` で Vite プロセスが残っていないことを確認

---

## 自動検証（uloop CLI + WS クライアント）

Play mode 起動〜終了をスクリプト化する場合のレシピ。CI や再現性確認に有用。

### 前提

- Unity が対象プロジェクトで起動済み（`uloop launch <project-path>`）
- `moorestech_web/setup.sh` 実行済み
- `/Users/katsumi/moorestech_master/server_v8/`（または該当バージョン）の master データが存在
  - パスが異なる場合は `<repo>/cache/StringDebugParameters.json` に `{"DebugServerDirectory":"/abs/path/to/server_vN/"}` を書く

### 1. MainMenu シーンを開いて Play mode 突入

```bash
uloop execute-dynamic-code --project-path <CLIENT> --code \
  'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Game/MainMenu.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);'

uloop control-play-mode --project-path <CLIENT> --action Play
```

Play mode 突入には Domain Reload が走るので **60 秒程度待つ**。応答復帰の polling 例:

```bash
until uloop clear-console --project-path <CLIENT> 2>&1 | grep -q 'Success'; do
  sleep 5
done
```

### 2. StartLocal ボタンを click

```bash
uloop execute-dynamic-code --project-path <CLIENT> --code '
var sl = UnityEngine.Object.FindFirstObjectByType<Client.MainMenu.StartLocal>();
var f = typeof(Client.MainMenu.StartLocal).GetField(
    "startLocalButton",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
((UnityEngine.UI.Button)f.GetValue(sl)).onClick.Invoke();
'
```

### 3. WebUiHost ready を待つ

```bash
for i in $(seq 1 24); do
  sleep 5
  if uloop get-logs --project-path <CLIENT> --log-type Log 2>&1 \
     | grep -q '\[WebUiHost\] ready'; then
    echo "ready at iteration $i"
    break
  fi
done
```

### 4. HTTP 疎通確認

```bash
curl -s http://127.0.0.1:5050/api/ping                        # Kestrel 直叩き
curl -s http://127.0.0.1:5173/api/ping                        # Vite proxy 経由
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5173/  # Vite トップ
```

期待:
- `{"ok":true}` × 2
- `200`

### 5. WebSocket 購読で実データ受信

`ws` パッケージを一時追加（後で削除）:

```bash
cd moorestech_web/webui
../node/mac-arm64/pnpm add -D ws
```

テストスクリプト:

```js
// /tmp/ws-verify.mjs
import WebSocket from 'ws';
const ws = new WebSocket('ws://127.0.0.1:5173/ws', {
  headers: { Origin: 'http://localhost:5173' }
});
ws.on('open', () => {
  console.log('OPEN');
  ws.send(JSON.stringify({ op: 'subscribe', topics: ['local_player.inventory'] }));
});
ws.on('message', (d) => console.log('MSG', String(d)));
setTimeout(() => process.exit(0), 10000);
```

```bash
../node/mac-arm64/bin/node /tmp/ws-verify.mjs
```

期待出力:
- `OPEN`
- `MSG {"op":"snapshot","topic":"local_player.inventory","data":{"mainSlots":[{"itemId":0,"count":0},...],"hotBarSlots":[]}}`

### 6. インベントリ変更 → イベント配信を確認

別ターミナルで WS watch を起動してから:

```bash
uloop execute-dynamic-code --project-path <CLIENT> --code '
var ctrl = Client.Game.InGame.Context.ClientDIContext.DIContainer
    .DIContainerResolver
    .Resolve<Client.Game.InGame.UI.Inventory.Main.LocalPlayerInventoryController>();
var factory = Game.Context.ServerContext.ItemStackFactory;
ctrl.SetMainItem(0, factory.Create(new Core.Master.ItemId(1), 42));
'
```

WS watch 側で即時受信するメッセージ:

```
op=event topic=local_player.inventory data={"mainSlots":[{"itemId":1,"count":42},...],"hotBarSlots":[]}
```

### 7. Play mode 停止 → クリーンアップ確認

```bash
uloop control-play-mode --project-path <CLIENT> --action Stop
sleep 10

# Vite プロセス確認
ps aux | grep -iE "vite|pnpm" | grep -v grep

# ポート確認（Unity が握る Kestrel 以外は空いているはず）
lsof -i :5173           # 空であるべき
lsof -i :5050           # Unity が握っているが、次 Play mode で再 bind 可
```

期待: `ps` の出力が空、`lsof -i :5173` の出力なし。

### 8. 後片付け

```bash
cd moorestech_web/webui
../node/mac-arm64/pnpm remove ws
git checkout -- package.json pnpm-lock.yaml  # 念のため差分を捨てる
rm /tmp/ws-verify.mjs
```

---

## 検証済みの動作（2026-04-22 時点）

検証したブランチ: `web`

- ✅ Play mode 起動 → Kestrel と Vite が自動起動
- ✅ Kestrel `http://127.0.0.1:5050/api/ping` → `{"ok":true}`
- ✅ Vite proxy `http://127.0.0.1:5173/api/ping` → `{"ok":true}`
- ✅ Vite dev server が React アプリを配信
- ✅ WebSocket `ws://127.0.0.1:5173/ws` に接続成立（RFC 6455 自前ハンドシェイク）
- ✅ `local_player.inventory` トピック subscribe → 初期 snapshot 即時返却
- ✅ `LocalPlayerInventoryController.SetMainItem()` → WS event 即時配信（全スロット再送）
- ✅ Play mode 停止 → `BackToMainMenu.Disconnect()` → `GameShutdownEvent` → `WebUiHost.Stop()` → Vite/Kestrel 停止
- ✅ `EditorApplication.playModeStateChanged` の ExitingPlayMode フックで Vite の取りこぼしを安全網として kill
- ✅ Play mode 2 回連続起動でポート衝突なし（Domain Reload 時の Kestrel 停止が機能）

---

## トラブルシューティング

### Node/pnpm が見つからない

`[WebUiHost] Node binary not found at ...` または `pnpm binary not found at ...` が出たら:

```bash
bash moorestech_web/setup.sh      # または setup.ps1
```

### ポート衝突

`[WebUiHost] Kestrel started` の代わりに `address already in use` が出る場合:

```bash
lsof -i :5050 -i :5173
```

他プロセスが使っていれば停止する。Unity を強制終了してプロセスが残っている場合は `kill -9`。

### Vite プロセスが Play mode 終了後も残る

古い実装で観測された問題。現在の実装では:
- `EditorApplication.playModeStateChanged` ExitingPlayMode
- `AssemblyReloadEvents.beforeAssemblyReload`
- `EditorApplication.quitting`

の 3 経路から `CleanupAll()` が呼ばれ `ViteProcess.KillAnyLingering()` で `pkill -f "vite --port 5173"` が最終セーフティネットとして走る。

それでも残る場合は手動で:

```bash
pkill -f "vite --port 5173"
```

### `pnpm install` が失敗

ネットワーク環境を確認。プロキシ配下なら `~/.npmrc` や環境変数 `HTTPS_PROXY` を設定。

### Domain Reload でタイムアウト

uloop が `Unity is reloading` を返す場合は 45 秒待ってリトライ。PlayMode 突入直後や `uloop compile` 直後に発生しやすい。

### サーバーマスターが見つからない

`moorestech_master` のクローンパスが違う場合:

```bash
# <repo>/cache/StringDebugParameters.json に以下を書く
{
  "DebugServerDirectory": "/absolute/path/to/moorestech_master/server_vN/"
}
```

---

## 今後の検証強化候補

- CI ランナーで自動 E2E（Play mode 回しを含む）を回す仕組み
- ブラウザ実機までの E2E（Playwright / Puppeteer で localhost:5173 を開いて DOM を検証）
- Windows / Linux での動作確認（現時点では macOS arm64 のみ検証済み）
- 同時に複数の WS クライアントが繋がったときの配信整合性チェック
