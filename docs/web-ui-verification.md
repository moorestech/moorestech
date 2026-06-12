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
// moorestech_web/webui/ws-verify.mjs（ESM解決がスクリプト位置基準のため、必ず webui/ 内に置く）
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
../node/mac-arm64/bin/node ws-verify.mjs
```

期待出力:
- `OPEN`
- `MSG {"op":"snapshot","topic":"local_player.inventory","data":{"mainSlots":[{"itemId":0,"count":0},... 36件],"hotbarSlots":[... 9件],"grab":{"itemId":0,"count":0}}}`

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
op=event topic=local_player.inventory data={"mainSlots":[{"itemId":1,"count":42},...],"hotbarSlots":[...],"grab":{...}}
```

**注意**: `SetMainItem` はクライアントローカルの表示状態のみを書き換え、サーバー側インベントリには反映されない。event 配信の確認にはこれで十分だが、**サーバー往復を含む action（move/collect/sort 等）の E2E には使えない**（後述「10. インベントリ操作のE2E」参照）。

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
rm ws-verify.mjs
```

### 9. action（双方向API）の検証

プロトコル詳細は `docs/superpowers/specs/2026-06-12-webui-migration-design.md` §2 を参照。最小サンプル（Phase 0 の `debug.echo`）:

```js
// action発行 → result受信の最小形
ws.send(JSON.stringify({ op: 'action', type: 'debug.echo', requestId: 'a1', payload: { msg: 'hello' } }));
// 受信: {"op":"result","requestId":"a1","ok":true}
```

登録済み action 一覧（Phase 1 時点）:

| type | payload | 説明 |
| --- | --- | --- |
| `inventory.move_item` | `{from:{area,slot}, to:{area,slot}, count}` | from→to へ count 個移動 |
| `inventory.split` | `{from:{area,slot}}` | スロットの半分を grab へ（右クリック相当） |
| `inventory.collect` | `{target:{area,slot}}` | 同種アイテムを target に集約（ダブルクリック相当） |
| `inventory.sort` | `{}` | メインインベントリ整理 |
| `craft.execute` | `{recipeGuid}` | レシピ GUID 指定でクラフト |
| `debug.echo` | 任意 | 疎通確認用 |

`area` は `"main"`（0〜35）/ `"hotbar"`（0〜8）/ `"grab"`。

エラーコード一覧（`result.ok:false` の `error`）:

| code | 発生条件 |
| --- | --- |
| `invalid_payload` | payload 欠落・形式不正 |
| `invalid_count` | count が 0 以下・整数でない |
| `invalid_slot` | slot 範囲外・area 名不正 |
| `empty_slot` | 移動元/対象スロットが空 |
| `insufficient_count` | 所持数より多い count 指定 |
| `grab_not_empty` | grab 非空時の split |
| `invalid_recipe` | 存在しない/形式不正な recipeGuid |
| `recipe_locked` | 未解放レシピ |
| `unknown_action` | 未登録の action type |

UI 状態の反映は result ではなく後続の `local_player.inventory` event（全スロット再送）で届く。

### 10. インベントリ操作のE2E

**前提**: 手順 6 の `SetMainItem` はクライアントローカルのみの書き換えで、サーバー側インベントリは空のまま。action はサーバー往復を伴うため、**テストアイテムは必ずサーバー側に投入する**こと。クライアント側だけに投入すると、split 後のサーバー echo（`GrabInventoryUpdateEvent`）が grab を空で上書きし、見かけ上アイテムが消失して以降の move/collect が `empty_slot` で失敗する。

サーバー側へのテストアイテム投入（投入後、サーバー event 経由でクライアント・WebUI に自動反映される）:

```bash
uloop execute-dynamic-code --project-path <CLIENT> --code '
var store = Game.Context.ServerContext.GetService<Game.PlayerInventory.Interface.IPlayerInventoryDataStore>();
int playerId = Client.Game.InGame.Context.ClientContext.PlayerConnectionSetting.PlayerId;
var inv = store.GetInventoryData(playerId);
var factory = Game.Context.ServerContext.ItemStackFactory;
inv.MainOpenableInventory.SetItem(0, factory.Create(new Core.Master.ItemId(1), 10));
inv.MainOpenableInventory.SetItem(1, factory.Create(new Core.Master.ItemId(1), 3));
return "server items set";'
```

E2E スクリプト（`moorestech_web/webui/e2e-phase1-verify.mjs`、`pnpm add -D ws` 済み前提）:

```js
import WebSocket from 'ws';
const ws = new WebSocket('ws://127.0.0.1:5173/ws', { headers: { Origin: 'http://localhost:5173' } });
const send = (o) => ws.send(JSON.stringify(o));
ws.on('open', () => {
  console.log('OPEN');
  send({ op: 'subscribe', topics: ['local_player.inventory', 'crafting.recipes'] });
  // 1) split: slot0(10個)の半分をgrabへ
  setTimeout(() => send({ op: 'action', type: 'inventory.split', requestId: 's1', payload: { from: { area: 'main', slot: 0 } } }), 1500);
  // 2) move: grabの5個をmain slot2へ
  setTimeout(() => send({ op: 'action', type: 'inventory.move_item', requestId: 's2', payload: { from: { area: 'grab', slot: 0 }, to: { area: 'main', slot: 2 }, count: 5 } }), 3000);
  // 3) collect: slot2を起点に同種収集
  setTimeout(() => send({ op: 'action', type: 'inventory.collect', requestId: 's3', payload: { target: { area: 'main', slot: 2 } } }), 4500);
  // 4) 異常系: 範囲外スロット
  setTimeout(() => send({ op: 'action', type: 'inventory.move_item', requestId: 's4', payload: { from: { area: 'main', slot: 99 }, to: { area: 'grab', slot: 0 }, count: 1 } }), 6000);
  // 5) 異常系: 空スロットからの移動
  setTimeout(() => send({ op: 'action', type: 'inventory.move_item', requestId: 's5', payload: { from: { area: 'main', slot: 20 }, to: { area: 'grab', slot: 0 }, count: 1 } }), 6500);
  // 6) 異常系: 不正レシピGUID
  setTimeout(() => send({ op: 'action', type: 'craft.execute', requestId: 's6', payload: { recipeGuid: '00000000-0000-0000-0000-000000000000' } }), 7000);
  // 7) sort
  setTimeout(() => send({ op: 'action', type: 'inventory.sort', requestId: 's7', payload: {} }), 7500);
});
ws.on('message', (d) => {
  const s = String(d);
  console.log('MSG', s.length > 500 ? s.slice(0, 500) + `...(${s.length} chars)` : s);
});
setTimeout(() => process.exit(0), 10000);
```

```bash
cd moorestech_web/webui && ../node/mac-arm64/bin/node e2e-phase1-verify.mjs
```

期待値の要約（2026-06-12 実測一致）:

- snapshot×2: inventory（mainSlots 36件・m0=1x10・m1=1x3、hotbarSlots 9件、grab）、crafting.recipes（81件、`{recipeGuid,resultItemId,resultCount,craftTime,requiredItems}`）
- s1 ok → event: m0=1x5, grab={itemId:1,count:5}
- s2 ok → event: m2=1x5, grab 空
- s3 ok → event: m0/m1 が m2 に集約され m2=1x13
- s4 `invalid_slot` / s5 `empty_slot` / s6 `invalid_recipe`
- s7 ok → event: m2 の 13 個が m0 へ移動（ソート反映）
- 各操作で**同一内容の event が 2 回**届く（ローカル楽観更新＋サーバー echo。クライアント実装仕様）

craft.execute 正常系（素材 itemId:1 ×3 → itemId:2 ×1 のレシピ例）:

```js
ws.send(JSON.stringify({ op: 'action', type: 'craft.execute', requestId: 'c1', payload: { recipeGuid: '9c20aa73-1877-4e0e-adcc-9f725c9377da' } }));
// result ok:true → event: 素材3個減、成果物は hotbarSlots[0]（サーバー slot36）に入る
```

### 11. アイコン・マスタ配信の検証

```bash
curl -s http://127.0.0.1:5173/api/master/items | head -c 300; echo
curl -s -o /dev/null -w "%{http_code} " -H "If-None-Match: \"dummy\"" http://127.0.0.1:5173/api/icons/1.png; echo
curl -s -D - -o /dev/null http://127.0.0.1:5173/api/master/items | grep -i cache-control
```

期待:

- `{"items":[{"itemId":1,"name":"小石","maxStack":100},...`
- `200`（ETag 不一致なので本体返却。実 ETag を `If-None-Match` に渡すと `304`）
- `cache-control: no-store`（items）。アイコンは `cache-control: no-cache` + `etag`
- 存在しない itemId（例 `/api/icons/99999.png`）は `404`。ただし Unity Console に Error ログ（`ItemViewData not found`）が出る点に注意

### 12. 既知の環境注意

- **train.json スキーマ不整合（恒久対応待ち）**: `../moorestech_master` の train.json は `ridableSeats` 移行済みだが、本リポジトリの `VanillaSchema/train.yml` は旧 `ridableSeatCount` のため、ローカルゲーム起動が `MooresmasterLoaderException: trainCars[0].ridableSeatCount` で失敗する。回避は train.json（`find /Users/katsumi/moorestech_master/server_v8 -name "train.json"`）の trainCars 各エントリに `"ridableSeatCount": 0` を一時追加 → 検証後 `git -C /Users/katsumi/moorestech_master checkout -- <path>` で復元
- **検証スクリプトは `moorestech_web/webui/` 内に置く**: ESM のモジュール解決がスクリプト位置基準のため、`/tmp` に置くと `ws` を import できない
- **uloop が「not installed」を返す**: `moorestech_client/UserSettings/UnityMcpSettings.json` が `.bak` のみになっていないか確認し、コピー復元する
- **uloop が「Unity is reloading」を返す**: 45 秒待ってリトライ

---

## 検証済みの動作（2026-06-12 時点 / Phase 0・1）

検証したブランチ: `feature/web-ui`

- ✅ topic v2: `local_player.inventory` が `{mainSlots:[36], hotbarSlots:[9], grab}` 形式で snapshot/event 配信
- ✅ topic: `crafting.recipes` snapshot（81 レシピ、`recipeGuid`/`resultItemId`/`requiredItems` 等）
- ✅ action 正常系: `inventory.split` / `inventory.move_item` / `inventory.collect` / `inventory.sort` / `craft.execute`（result ok → event 反映、craft はサーバー往復後に素材減・成果物増）
- ✅ action 異常系: `invalid_payload` / `invalid_count` / `invalid_slot` / `empty_slot` / `insufficient_count` / `grab_not_empty` / `invalid_recipe` / `unknown_action` を実測（`recipe_locked` のみコード確認）
- ✅ HTTP: `/api/master/items`（`cache-control: no-store`）、`/api/icons/{id}.png`（ETag 一致で 304、不一致で 200、未知 ID で 404）
- ✅ ブラウザ実機（headless Chrome）: インベントリグリッド・Craft パネルがアイコン付きで描画
- ⚠️ 各インベントリ操作で同一内容の event が 2 回配信される（ローカル楽観更新＋サーバー echo）
- ⚠️ `SetMainItem` によるクライアントローカル投入では action E2E が成立しない（手順 10 参照）

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
