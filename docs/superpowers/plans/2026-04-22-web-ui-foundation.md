# Web UI Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unity クライアントを起動すると内部で Kestrel と Vite dev server が自動起動し、ブラウザで `http://localhost:5173/` を開くと React 製画面にローカルプレイヤーのインベントリがリアルタイム表示される基盤を構築する。

**Architecture:** Unity プロセス内で ASP.NET Core 2.3 の Kestrel（`127.0.0.1:5050`）を立て、同じプロセスから Node を spawn して Vite dev server（`127.0.0.1:5173`）を起動する。Vite の `server.proxy` で `/ws` と `/api/*` を Kestrel に転送。WebSocket 上の購読ベースプロトコルで Unity→Web のデータ push を実装。ライフサイクルは pure C# かつ既存の `InitializeScenePipeline` と `GameShutdownEvent`（新規）に同期させる。

**Tech Stack:**
- サーバー: ASP.NET Core 2.3 (netstandard2.0 LTS) / Kestrel (既存 NuGet パッケージ再利用)
- クライアント: React 18 + TypeScript 5 + Vite 5 + Tailwind CSS
- JS ランタイム: Node.js LTS standalone binary
- パッケージマネージャ: pnpm (`node-linker=hoisted`, `@pnpm/exe` standalone)
- 非同期: UniTask (`UniTask.SwitchToMainThread()` でメインスレッド marshalling)

**Spec:** `docs/superpowers/specs/2026-04-22-web-ui-foundation-design.md`

---

## File Structure

### 新規作成（C#）

| ファイル | 責務 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs` | `GameInitializedEvent` と対称のシャットダウンイベント。`BackToMainMenu.Disconnect()` から fire、WebUiHost が購読 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef` | 新規 asmdef。Client.Game と UniRx/UniTask/Microsoft.AspNetCore.* を参照 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiPaths.cs` | `moorestech_web/` への絶対パス解決 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketHub.cs` | WS 接続ごとの購読 topic set 管理、トピックハンドラ登録 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs` | `/ws` と `/api` のルーティング |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/KestrelServer.cs` | Kestrel `IWebHost` の起動/停止ラッパ |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteProcess.cs` | Node spawn、stdout ready 監視、kill |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs` | static facade。StartAsync / Stop / Hub |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs` | static facade。BindTopics() |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/InventoryTopic.cs` | `LocalPlayerInventory.OnItemChange` を購読し `local_player.inventory` トピックに push |

### 新規作成（リポジトリルート）

| パス | 責務 |
|---|---|
| `moorestech_web/.gitignore` | node_modules / node binaries を除外 |
| `moorestech_web/setup.sh` | 開発環境用 Node + pnpm ダウンロードスクリプト（macOS/Linux） |
| `moorestech_web/setup.ps1` | 開発環境用 Node + pnpm ダウンロードスクリプト（Windows） |
| `moorestech_web/webui/package.json` | React/Vite 依存定義 |
| `moorestech_web/webui/.npmrc` | `node-linker=hoisted` |
| `moorestech_web/webui/tsconfig.json` | TS 設定 |
| `moorestech_web/webui/vite.config.ts` | Vite 設定、`server.proxy` で /ws と /api を :5050 に転送 |
| `moorestech_web/webui/tailwind.config.ts` | Tailwind 設定 |
| `moorestech_web/webui/postcss.config.js` | PostCSS (Tailwind) |
| `moorestech_web/webui/index.html` | Vite エントリ |
| `moorestech_web/webui/src/main.tsx` | React root マウント |
| `moorestech_web/webui/src/App.tsx` | ルートコンポーネント |
| `moorestech_web/webui/src/index.css` | Tailwind directives |
| `moorestech_web/webui/src/bridge/webSocketClient.ts` | WS 接続・再接続・envelope 送受信 |
| `moorestech_web/webui/src/bridge/useTopic.ts` | `useTopic<T>(topicName)` React フック |
| `moorestech_web/webui/src/components/InventoryView.tsx` | インベントリ表示コンポーネント |

### 変更

| ファイル | 変更内容 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs` | `Initialize()` 冒頭に `WebUiHost.StartAsync()` と `GameShutdownEvent` 購読を追加。`new ClientContext(...)` 直後に `WebUiGameBinder.BindTopics()` を追加 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs` | `Disconnect()` 末尾に `GameShutdownEvent.FireGameShutdown()` を追加 |

---

## Conventions

- **コメント:** 日本語・英語 2 行セット（`// 日本語 → // English`）を 3〜10 行ごと
- **try-catch 禁止:** nullチェック/条件分岐で対応
- **デフォルト引数禁止**
- **`#region Internal` はメソッド内のローカル関数用途のみ**
- **UniRx** を使用（C# 標準 Action/event は使わない）
- **コード変更後は必ずコンパイル:** `uloop compile --project-path ./moorestech_client`
- **各タスク末で commit**（AGENTS.md の「タスク終了前に必ず全作業をコミット」に従う）

---

## Task 1: moorestech_web ディレクトリ骨格と setup スクリプト作成

**Files:**
- Create: `moorestech_web/.gitignore`
- Create: `moorestech_web/setup.sh`
- Create: `moorestech_web/setup.ps1`
- Create: `moorestech_web/README.md`

- [ ] **Step 1: `moorestech_web/.gitignore` を作成**

```gitignore
# Node.js / pnpm standalone binaries (download via setup.sh / setup.ps1)
node/

# Dependencies
webui/node_modules/

# Vite output
webui/dist/
webui/.vite/
```

- [ ] **Step 2: `moorestech_web/setup.sh` を作成（macOS/Linux 用）**

```bash
#!/usr/bin/env bash
# moorestech_web セットアップ: Node.js LTS と pnpm スタンドアロンバイナリを
# moorestech_web/node/<platform>/ にダウンロードする。
# Setup for moorestech_web: downloads Node.js LTS and pnpm standalone
# binaries into moorestech_web/node/<platform>/.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NODE_VERSION="20.18.1"
PNPM_VERSION="9.15.0"

uname_os="$(uname -s)"
uname_arch="$(uname -m)"

case "$uname_os-$uname_arch" in
  Darwin-arm64) PLATFORM="mac-arm64"; NODE_PLATFORM="darwin-arm64"; PNPM_SUFFIX="macos-arm64"; NODE_EXT="tar.gz";;
  Darwin-x86_64) PLATFORM="mac-x64"; NODE_PLATFORM="darwin-x64"; PNPM_SUFFIX="macos-x64"; NODE_EXT="tar.gz";;
  Linux-x86_64) PLATFORM="linux-x64"; NODE_PLATFORM="linux-x64"; PNPM_SUFFIX="linux-x64"; NODE_EXT="tar.xz";;
  *) echo "Unsupported platform: $uname_os-$uname_arch"; exit 1;;
esac

TARGET_DIR="$SCRIPT_DIR/node/$PLATFORM"
mkdir -p "$TARGET_DIR"

NODE_URL="https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-${NODE_PLATFORM}.${NODE_EXT}"
PNPM_URL="https://github.com/pnpm/pnpm/releases/download/v${PNPM_VERSION}/pnpm-${PNPM_SUFFIX}"

echo "[setup] downloading node from $NODE_URL"
curl -fL "$NODE_URL" -o "/tmp/node.$NODE_EXT"
if [ "$NODE_EXT" = "tar.xz" ]; then
  tar -xJf "/tmp/node.$NODE_EXT" -C "$TARGET_DIR" --strip-components=1
else
  tar -xzf "/tmp/node.$NODE_EXT" -C "$TARGET_DIR" --strip-components=1
fi
rm "/tmp/node.$NODE_EXT"

echo "[setup] downloading pnpm from $PNPM_URL"
curl -fL "$PNPM_URL" -o "$TARGET_DIR/pnpm"
chmod +x "$TARGET_DIR/pnpm"

echo "[setup] done. node: $TARGET_DIR/bin/node, pnpm: $TARGET_DIR/pnpm"
```

- [ ] **Step 3: `moorestech_web/setup.ps1` を作成（Windows 用）**

```powershell
# moorestech_web セットアップ: Node.js LTS と pnpm スタンドアロンバイナリを
# moorestech_web/node/win-x64/ にダウンロードする。
# Setup for moorestech_web: downloads Node.js LTS and pnpm standalone
# binaries into moorestech_web/node/win-x64/.
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$NodeVersion = "20.18.1"
$PnpmVersion = "9.15.0"
$Platform = "win-x64"
$TargetDir = Join-Path $ScriptDir "node\$Platform"
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

$NodeUrl = "https://nodejs.org/dist/v$NodeVersion/node-v$NodeVersion-win-x64.zip"
$PnpmUrl = "https://github.com/pnpm/pnpm/releases/download/v$PnpmVersion/pnpm-win-x64.exe"

Write-Host "[setup] downloading node from $NodeUrl"
$NodeZip = Join-Path $env:TEMP "node.zip"
Invoke-WebRequest -Uri $NodeUrl -OutFile $NodeZip
Expand-Archive -Path $NodeZip -DestinationPath $TargetDir -Force
# zip 内は node-v20.18.1-win-x64/ 配下なので 1階層下げる
# The zip contains a top-level node-v20.18.1-win-x64/ dir, flatten it
$Inner = Get-ChildItem -Path $TargetDir -Directory | Select-Object -First 1
Move-Item -Path (Join-Path $Inner.FullName "*") -Destination $TargetDir
Remove-Item -Path $Inner.FullName -Recurse -Force
Remove-Item $NodeZip

Write-Host "[setup] downloading pnpm from $PnpmUrl"
Invoke-WebRequest -Uri $PnpmUrl -OutFile (Join-Path $TargetDir "pnpm.exe")

Write-Host "[setup] done. node: $TargetDir\node.exe, pnpm: $TargetDir\pnpm.exe"
```

- [ ] **Step 4: `moorestech_web/README.md` を作成**

```markdown
# moorestech_web

Web UI のフロントエンドプロジェクトと、Unity から spawn する Node.js / pnpm のバイナリを格納する。

## セットアップ

初回のみ、対応プラットフォームのセットアップスクリプトを実行する:

- macOS / Linux: `bash setup.sh`
- Windows (PowerShell): `.\setup.ps1`

これで `moorestech_web/node/<platform>/` に Node.js と pnpm のスタンドアロンバイナリが展開される。バイナリは `.gitignore` されているので commit されない。

## レイアウト

- `webui/` TypeScript + React + Vite プロジェクト
- `node/<platform>/` Node.js と pnpm のスタンドアロンバイナリ（setup スクリプトで配置）

## 開発中の実行

Unity クライアントを起動すると、内部から `node/<platform>/node` が自動 spawn されて Vite dev server が立ち上がる。手動で `pnpm dev` を回す必要はない。開発中に Vite を手動で再起動したい場合は Unity を再起動する。
```

- [ ] **Step 5: Commit**

```bash
git add moorestech_web/.gitignore moorestech_web/setup.sh moorestech_web/setup.ps1 moorestech_web/README.md
chmod +x moorestech_web/setup.sh
git commit -m "moorestech_web ディレクトリ骨格と Node/pnpm setup スクリプト追加"
```

---

## Task 2: TS/React/Vite プロジェクト scaffold

**Files:**
- Create: `moorestech_web/webui/package.json`
- Create: `moorestech_web/webui/.npmrc`
- Create: `moorestech_web/webui/tsconfig.json`
- Create: `moorestech_web/webui/tsconfig.node.json`
- Create: `moorestech_web/webui/vite.config.ts`
- Create: `moorestech_web/webui/tailwind.config.ts`
- Create: `moorestech_web/webui/postcss.config.js`
- Create: `moorestech_web/webui/index.html`
- Create: `moorestech_web/webui/src/main.tsx`
- Create: `moorestech_web/webui/src/App.tsx`
- Create: `moorestech_web/webui/src/index.css`

- [ ] **Step 1: `moorestech_web/webui/.npmrc` を作成**

```
node-linker=hoisted
strict-peer-dependencies=false
```

- [ ] **Step 2: `moorestech_web/webui/package.json` を作成**

```json
{
  "name": "moorestech-webui",
  "private": true,
  "version": "0.0.1",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview"
  },
  "dependencies": {
    "react": "^18.3.1",
    "react-dom": "^18.3.1"
  },
  "devDependencies": {
    "@types/react": "^18.3.12",
    "@types/react-dom": "^18.3.1",
    "@vitejs/plugin-react": "^4.3.4",
    "autoprefixer": "^10.4.20",
    "postcss": "^8.4.49",
    "tailwindcss": "^3.4.16",
    "typescript": "^5.7.2",
    "vite": "^5.4.11"
  }
}
```

- [ ] **Step 3: `moorestech_web/webui/tsconfig.json` を作成**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "Bundler",
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true
  },
  "include": ["src"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
```

- [ ] **Step 4: `moorestech_web/webui/tsconfig.node.json` を作成**

```json
{
  "compilerOptions": {
    "composite": true,
    "skipLibCheck": true,
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "allowSyntheticDefaultImports": true,
    "strict": true
  },
  "include": ["vite.config.ts", "tailwind.config.ts"]
}
```

- [ ] **Step 5: `moorestech_web/webui/vite.config.ts` を作成**

```ts
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Vite dev server の設定
// Vite dev server configuration
export default defineConfig({
  plugins: [react()],
  server: {
    host: "127.0.0.1",
    port: 5173,
    strictPort: true,
    fs: {
      // リポジトリ外や node_modules 階層への /@fs/ アクセスを封じる
      // Prevent /@fs/ access to outside-repo / node_modules paths
      allow: ["./src", "./public", "./index.html"],
      strict: true,
    },
    proxy: {
      // Kestrel への HTTP + WebSocket プロキシ
      // Proxy HTTP and WebSocket to Kestrel
      "/api": {
        target: "http://127.0.0.1:5050",
        changeOrigin: false,
      },
      "/ws": {
        target: "ws://127.0.0.1:5050",
        ws: true,
        changeOrigin: false,
      },
    },
  },
});
```

- [ ] **Step 6: `moorestech_web/webui/tailwind.config.ts` を作成**

```ts
import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {},
  },
  plugins: [],
};

export default config;
```

- [ ] **Step 7: `moorestech_web/webui/postcss.config.js` を作成**

```js
export default {
  plugins: {
    tailwindcss: {},
    autoprefixer: {},
  },
};
```

- [ ] **Step 8: `moorestech_web/webui/index.html` を作成**

```html
<!doctype html>
<html lang="ja">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>moorestech Web UI</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [ ] **Step 9: `moorestech_web/webui/src/index.css` を作成**

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

body {
  margin: 0;
  font-family: system-ui, -apple-system, BlinkMacSystemFont, sans-serif;
  background-color: #111;
  color: #eee;
}
```

- [ ] **Step 10: `moorestech_web/webui/src/main.tsx` を作成**

```tsx
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import "./index.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
```

- [ ] **Step 11: `moorestech_web/webui/src/App.tsx` を作成（仮実装）**

```tsx
export default function App() {
  // インベントリ表示は Task 9 で差し替え
  // Inventory rendering will be wired in Task 9
  return (
    <div className="p-4">
      <h1 className="text-2xl font-bold">moorestech Web UI</h1>
      <p className="text-sm text-gray-400 mt-2">bootstrapping...</p>
    </div>
  );
}
```

- [ ] **Step 12: setup スクリプトを実行してバイナリ配置**

```bash
bash moorestech_web/setup.sh
```

Expected: `moorestech_web/node/mac-arm64/bin/node` (macOS ARM) と `moorestech_web/node/mac-arm64/pnpm` が配置される。

- [ ] **Step 13: pnpm install でローカル動作確認**

```bash
cd moorestech_web/webui
../node/mac-arm64/pnpm install
```

Expected: `node_modules/` がフラット配置（symlink なし）で生成される。

- [ ] **Step 14: Vite dev server を手動起動して動作確認**

```bash
cd moorestech_web/webui
../node/mac-arm64/pnpm dev
```

Expected: `Local: http://127.0.0.1:5173/` が表示され、ブラウザで開くと "moorestech Web UI / bootstrapping..." が表示される。Ctrl+C で停止。

- [ ] **Step 15: Commit**

```bash
git add moorestech_web/webui/
git commit -m "webui プロジェクト scaffold: React + Vite + Tailwind + TypeScript"
```

---

## Task 3: GameShutdownEvent 新規作成と BackToMainMenu から fire

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs`

- [ ] **Step 1: `GameShutdownEvent.cs` を作成（`GameInitializedEvent.cs` と対称）**

```csharp
using System;
using UniRx;

namespace Client.Game.Common
{
    /// <summary>
    /// ゲームの終了パイプラインイベント
    /// Game shutdown pipeline event
    /// </summary>
    public static class GameShutdownEvent
    {
        private static readonly Subject<Unit> _onGameShutdown = new();

        // ゲーム終了時に発火するイベント
        // Event fired when game shutdown begins
        public static IObservable<Unit> OnGameShutdown => _onGameShutdown;

        public static void FireGameShutdown()
        {
            _onGameShutdown.OnNext(Unit.Default);
        }
    }
}
```

- [ ] **Step 2: `BackToMainMenu.Disconnect()` 末尾に fire を追加**

```csharp
using System.Threading;
using Client.Common;
using Client.Game.Common;          // ← 追加
using Client.Game.InGame.Context;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    //ゲームが終了したときかメインメニューに戻るときはサーバーを終了させます
    public class BackToMainMenu : MonoBehaviour
    {
        [SerializeField] private Button backToMainMenuButton;
        
        private void Start()
        {
            backToMainMenuButton.onClick.AddListener(Back);
        }
        
        private void OnDestroy()
        {
            Disconnect();
        }
        
        private void OnApplicationQuit()
        {
            Disconnect();
        }
        
        private void Back()
        {
            Disconnect();
            SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
        }
        
        private void Disconnect()
        {
            ClientContext.VanillaApi.SendOnly.Save();
            Thread.Sleep(50);
            ClientContext.VanillaApi.Disconnect();
            // Web UI 等、ゲーム終了に同期したい購読者へ通知
            // Notify subscribers tied to game shutdown (e.g. Web UI)
            GameShutdownEvent.FireGameShutdown();
        }
    }
}
```

- [ ] **Step 3: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs
git commit -m "GameShutdownEvent を追加し BackToMainMenu から fire"
```

注: `.meta` ファイルは Unity が起動時に自動生成するので手動作成しない。Unity 起動後にまとめて追加 commit する。

---

## Task 4: Client.WebUiHost asmdef 作成

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`

- [ ] **Step 1: `Client.WebUiHost.asmdef` を作成**

既存の `Client.Game.asmdef` をリファレンスとして参考にしつつ、以下の内容で作成。

```json
{
    "name": "Client.WebUiHost",
    "rootNamespace": "Client.WebUiHost",
    "references": [
        "GUID:0e2b9c8b00d0446c8b0e87d96a3e5d7c",
        "Client.Common",
        "Client.Game",
        "Client.Network",
        "Core.Item.Interface",
        "Core.Master",
        "Game.Context",
        "Game.PlayerInventory.Interface",
        "UniRx",
        "UniTask"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

注: Microsoft.AspNetCore.* DLL は既に `Assets/Packages/` 配下にあり NuGetForUnity 経由でグローバルに参照可能。`overrideReferences: false` と `autoReferenced: true` で取り込まれる。

- [ ] **Step 2: `Client.Game.asmdef` の `name` と実 `GUID` を確認して `references` を必要に応じて修正**

```bash
cat moorestech_client/Assets/Scripts/Client.Game/Client.Game.asmdef | head -20
```

上記で `name` が `Client.Game` であれば references には "Client.Game" をそのまま記載すれば OK。GUID 参照が必要な場合は `.asmdef` 隣接の `.meta` 内 `guid` を使うが、可読性のため name 参照を優先。

- [ ] **Step 3: ディレクトリだけ先に作る（空ファイルを 1 つ置く必要はない）**

```bash
mkdir -p moorestech_client/Assets/Scripts/Client.WebUiHost/Boot
mkdir -p moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics
```

- [ ] **Step 4: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし（中身のない asmdef だが問題なし）。

- [ ] **Step 5: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/
git commit -m "Client.WebUiHost asmdef と Boot/Game ディレクトリ追加"
```

---

## Task 5: WebUiPaths（絶対パス解決）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiPaths.cs`

- [ ] **Step 1: `WebUiPaths.cs` を作成**

```csharp
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// moorestech_web/ 配下の絶対パスを解決するユーティリティ
    /// Resolves absolute paths under moorestech_web/
    /// </summary>
    public static class WebUiPaths
    {
        // エディタ実行時: moorestech_client/Assets の2階層上 = リポジトリルート
        // At editor time: two levels up from moorestech_client/Assets == repo root
        // Application.dataPath は moorestech_client/Assets を指すので、
        // その親（moorestech_client）の親がリポジトリルート。
        // Application.dataPath points at moorestech_client/Assets,
        // so its parent's parent is the repo root.
        public static string RepoRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static string WebRoot =>
            Path.Combine(RepoRoot, "moorestech_web");

        public static string WebuiRoot =>
            Path.Combine(WebRoot, "webui");

        public static string NodeBinary
        {
            get
            {
                var platform = GetPlatformDir();
                // Windows は node.exe、それ以外は bin/node
                // Windows: node.exe, others: bin/node
                var rel = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "node.exe"
                    : Path.Combine("bin", "node");
                return Path.Combine(WebRoot, "node", platform, rel);
            }
        }

        public static string PnpmBinary
        {
            get
            {
                var platform = GetPlatformDir();
                var file = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pnpm.exe" : "pnpm";
                return Path.Combine(WebRoot, "node", platform, file);
            }
        }

        // プラットフォーム別ディレクトリ名（setup.sh / setup.ps1 と一致）
        // Per-platform directory name (matches setup.sh / setup.ps1)
        public static string GetPlatformDir()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "mac-arm64" : "mac-x64";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win-x64";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux-x64";
            }
            return "unknown";
        }
    }
}
```

- [ ] **Step 2: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiPaths.cs
git commit -m "WebUiPaths: moorestech_web/ 配下の絶対パス解決ユーティリティ"
```

---

## Task 6: WebSocketHub（接続・購読管理）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketHub.cs`

`WebSocketHub` は WS 接続ごとの購読 topic set を保持し、トピックハンドラを名前で登録する。ハンドラは `ITopicHandler` インターフェースに準拠。

- [ ] **Step 1: `WebSocketHub.cs` を作成**

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// トピックハンドラのインターフェース
    /// Interface for topic handlers
    /// </summary>
    public interface ITopicHandler
    {
        // 新規購読者に現在値を snapshot として返す
        // Return current value as snapshot for a new subscriber
        UniTask<string> GetSnapshotJsonAsync();
    }

    /// <summary>
    /// WS 接続の集約・購読管理・トピック配信
    /// Aggregates WS connections, manages subscriptions, dispatches topic events
    /// </summary>
    public class WebSocketHub
    {
        private readonly ConcurrentDictionary<Guid, Connection> _connections = new();
        private readonly ConcurrentDictionary<string, ITopicHandler> _handlers = new();

        // トピックハンドラ登録（InventoryTopic などが呼ぶ）
        // Register a topic handler (called by InventoryTopic etc.)
        public void RegisterTopic(string topic, ITopicHandler handler)
        {
            _handlers[topic] = handler;
        }

        // 全接続のうち指定トピックを購読している接続に event を配信
        // Broadcast an event payload to all connections subscribed to the topic
        public void Publish(string topic, string dataJson)
        {
            var envelope = $"{{\"op\":\"event\",\"topic\":\"{EscapeJsonString(topic)}\",\"data\":{dataJson}}}";
            foreach (var conn in _connections.Values)
            {
                if (conn.Topics.Contains(topic))
                {
                    conn.EnqueueSend(envelope);
                }
            }
        }

        // 新規接続を受け入れ、メッセージループを開始
        // Accept a new connection and start its message loop
        public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            var conn = new Connection(id, webSocket);
            _connections[id] = conn;
            
            // 送信ループと受信ループを同時実行
            // Run send loop and receive loop concurrently
            var sendTask = SendLoop(conn, ct);
            var receiveTask = ReceiveLoop(conn, ct);
            await Task.WhenAny(sendTask, receiveTask);
            
            _connections.TryRemove(id, out _);
        }

        #region Internal

        private async Task ReceiveLoop(Connection conn, CancellationToken ct)
        {
            var buffer = new byte[8192];
            while (conn.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await conn.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await conn.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    return;
                }
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await HandleClientMessage(conn, json);
            }
        }

        private async Task HandleClientMessage(Connection conn, string json)
        {
            // 超軽量 JSON パース: op / topics / topic を小さく取り出すだけ
            // Minimal JSON parse: extract only op / topics / topic
            var op = ExtractJsonString(json, "\"op\"");
            if (op == "subscribe")
            {
                var topics = ExtractJsonStringArray(json, "\"topics\"");
                foreach (var t in topics)
                {
                    conn.Topics.Add(t);
                    if (_handlers.TryGetValue(t, out var handler))
                    {
                        var snap = await handler.GetSnapshotJsonAsync();
                        var env = $"{{\"op\":\"snapshot\",\"topic\":\"{EscapeJsonString(t)}\",\"data\":{snap}}}";
                        conn.EnqueueSend(env);
                    }
                }
            }
            else if (op == "unsubscribe")
            {
                var topics = ExtractJsonStringArray(json, "\"topics\"");
                foreach (var t in topics) conn.Topics.Remove(t);
            }
            else if (op == "snapshot")
            {
                var topic = ExtractJsonString(json, "\"topic\"");
                if (_handlers.TryGetValue(topic, out var handler))
                {
                    var snap = await handler.GetSnapshotJsonAsync();
                    var env = $"{{\"op\":\"snapshot\",\"topic\":\"{EscapeJsonString(topic)}\",\"data\":{snap}}}";
                    conn.EnqueueSend(env);
                }
            }
        }

        private async Task SendLoop(Connection conn, CancellationToken ct)
        {
            while (conn.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var msg = await conn.DequeueSendAsync(ct);
                if (msg == null) continue;
                var bytes = Encoding.UTF8.GetBytes(msg);
                await conn.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
            }
        }

        public async Task CloseAllAsync()
        {
            foreach (var conn in _connections.Values)
            {
                if (conn.WebSocket.State == WebSocketState.Open)
                {
                    await conn.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "server stopping", CancellationToken.None);
                }
            }
            _connections.Clear();
        }

        private static string EscapeJsonString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // 最小 JSON ヘルパ: "key":"value" パターンから value を返す
        // Minimal JSON helper: extract string value after a "key": marker
        private static string ExtractJsonString(string json, string key)
        {
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return "";
            var colon = json.IndexOf(':', idx);
            if (colon < 0) return "";
            var q1 = json.IndexOf('"', colon);
            if (q1 < 0) return "";
            var q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        // 最小 JSON ヘルパ: "key":[ "a", "b" ] から文字列配列を返す
        // Minimal JSON helper: extract string array after a "key": marker
        private static List<string> ExtractJsonStringArray(string json, string key)
        {
            var result = new List<string>();
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return result;
            var lb = json.IndexOf('[', idx);
            if (lb < 0) return result;
            var rb = json.IndexOf(']', lb);
            if (rb < 0) return result;
            var inside = json.Substring(lb + 1, rb - lb - 1);
            var pos = 0;
            while (pos < inside.Length)
            {
                var q1 = inside.IndexOf('"', pos);
                if (q1 < 0) break;
                var q2 = inside.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                result.Add(inside.Substring(q1 + 1, q2 - q1 - 1));
                pos = q2 + 1;
            }
            return result;
        }

        #endregion

        /// <summary>
        /// 1 本の WS 接続の状態
        /// State of a single WS connection
        /// </summary>
        private sealed class Connection
        {
            public Guid Id { get; }
            public WebSocket WebSocket { get; }
            public HashSet<string> Topics { get; } = new();
            private readonly Channel<string> _sendChannel = Channel.CreateUnbounded<string>();

            public Connection(Guid id, WebSocket webSocket)
            {
                Id = id;
                WebSocket = webSocket;
            }

            public void EnqueueSend(string msg) => _sendChannel.Writer.TryWrite(msg);

            public async Task<string> DequeueSendAsync(CancellationToken ct)
            {
                return await _sendChannel.Reader.ReadAsync(ct);
            }
        }
    }
}
```

注: `System.Threading.Channels.Channel` は netstandard2.1 で利用可能。Unity 6 は netstandard2.1 対応（`apiCompatibilityLevel: 6` は .NET Standard 2.1）。利用できない場合は `BlockingCollection<string>` に置換する。

- [ ] **Step 2: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。`Channel` が解決できない場合は `System.Threading.Channels` NuGet パッケージを追加するか、`BlockingCollection<string>` で書き換える。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketHub.cs
git commit -m "WebSocketHub: 接続管理とトピック購読の中核"
```

---

## Task 7: WebUiEndpoints（ルーティング）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs`

- [ ] **Step 1: `WebUiEndpoints.cs` を作成**

```csharp
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Kestrel のルーティング設定。/api と /ws を提供
    /// Kestrel routing: provides /api and /ws
    /// </summary>
    public static class WebUiEndpoints
    {
        public static void Configure(IApplicationBuilder app, WebSocketHub hub)
        {
            // WebSocket を有効化
            // Enable WebSocket middleware
            app.UseWebSockets();

            app.Run(async context =>
            {
                var path = context.Request.Path.Value ?? "";

                if (path == "/ws")
                {
                    // Origin ヘッダを検査
                    // Validate Origin header
                    if (!IsAllowedOrigin(context.Request.Headers["Origin"]))
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("forbidden origin", CancellationToken.None);
                        return;
                    }
                    if (!context.WebSockets.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        return;
                    }
                    var ws = await context.WebSockets.AcceptWebSocketAsync();
                    await hub.HandleConnectionAsync(ws, context.RequestAborted);
                    return;
                }

                if (path == "/api/ping")
                {
                    // ヘルスチェック兼疎通確認用エンドポイント
                    // Health / connectivity check
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync("{\"ok\":true}", CancellationToken.None);
                    return;
                }

                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("not found", CancellationToken.None);
            });
        }

        private static bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin)) return false;
            return origin == "http://localhost:5173" || origin == "http://127.0.0.1:5173";
        }
    }
}
```

- [ ] **Step 2: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs
git commit -m "WebUiEndpoints: /api/ping と /ws のルーティング"
```

---

## Task 8: KestrelServer（起動/停止ラッパ）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/KestrelServer.cs`

- [ ] **Step 1: `KestrelServer.cs` を作成**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Kestrel IWebHost の起動/停止を包むラッパ
    /// Wrapper around Kestrel IWebHost lifecycle
    /// </summary>
    public class KestrelServer
    {
        private const int Port = 5050;
        private IWebHost _webHost;

        public async Task StartAsync(WebSocketHub hub)
        {
            var url = $"http://127.0.0.1:{Port}";

            _webHost = new WebHostBuilder()
                .UseKestrel()
                .UseUrls(url)
                .ConfigureServices(services => services.AddRouting())
                .Configure(app => WebUiEndpoints.Configure(app, hub))
                .Build();

            await _webHost.StartAsync();
            Debug.Log($"[WebUiHost] Kestrel started at {url}");
        }

        public async Task StopAsync()
        {
            if (_webHost == null) return;
            // 最大 2 秒で graceful shutdown
            // Graceful shutdown capped at 2 seconds
            await _webHost.StopAsync(TimeSpan.FromSeconds(2));
            _webHost.Dispose();
            _webHost = null;
            Debug.Log("[WebUiHost] Kestrel stopped");
        }
    }
}
```

- [ ] **Step 2: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/KestrelServer.cs
git commit -m "KestrelServer: IWebHost 起動/停止ラッパ"
```

---

## Task 9: ViteProcess（Node spawn / ready 検出 / kill）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteProcess.cs`

- [ ] **Step 1: `ViteProcess.cs` を作成**

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Node を spawn して Vite dev server を起動し、終了時に kill する
    /// Spawn Node to run Vite dev server; kill it on shutdown
    /// </summary>
    public class ViteProcess
    {
        private Process _process;

        public async UniTask StartAsync()
        {
            var nodePath = WebUiPaths.NodeBinary;
            var pnpmPath = WebUiPaths.PnpmBinary;
            var webuiRoot = WebUiPaths.WebuiRoot;

            // Node / pnpm バイナリ / webui ディレクトリの存在確認
            // Verify node / pnpm binaries and webui dir are present
            if (!File.Exists(nodePath))
            {
                Debug.LogError($"[WebUiHost] Node binary not found at {nodePath}. Run moorestech_web/setup.sh (or setup.ps1) first.");
                return;
            }
            if (!File.Exists(pnpmPath))
            {
                Debug.LogError($"[WebUiHost] pnpm binary not found at {pnpmPath}. Run moorestech_web/setup.sh (or setup.ps1) first.");
                return;
            }
            if (!Directory.Exists(webuiRoot))
            {
                Debug.LogError($"[WebUiHost] webui dir not found at {webuiRoot}.");
                return;
            }

            // node_modules が無ければ pnpm install を先に走らせる
            // Run pnpm install first if node_modules is missing
            if (!Directory.Exists(Path.Combine(webuiRoot, "node_modules")))
            {
                await RunPnpmInstallAsync(nodePath, pnpmPath, webuiRoot);
            }

            // Vite dev server を spawn
            // Spawn Vite dev server
            _process = SpawnViteDev(nodePath, pnpmPath, webuiRoot);

            // stdout に "ready in" が出るまで待機（最大 30 秒）
            // Wait for "ready in" marker in stdout (cap 30 seconds)
            await WaitForReadyAsync(30);
        }

        public void Kill()
        {
            if (_process == null) return;
            if (_process.HasExited) { _process = null; return; }
            _process.Kill();
            _process.WaitForExit(2000);
            _process.Dispose();
            _process = null;
            Debug.Log("[WebUiHost] Vite process killed");
        }

        #region Internal

        private readonly ManualResetEventSlim _readySignal = new(false);

        private async UniTask RunPnpmInstallAsync(string nodePath, string pnpmPath, string cwd)
        {
            Debug.Log("[WebUiHost] running pnpm install...");
            var psi = new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = $"\"{pnpmPath}\" install",
                WorkingDirectory = cwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null)
            {
                Debug.LogError("[WebUiHost] pnpm install: failed to spawn process");
                return;
            }
            await UniTask.RunOnThreadPool(() => p.WaitForExit());
            if (p.ExitCode != 0)
            {
                Debug.LogError($"[WebUiHost] pnpm install exited with code {p.ExitCode}\n{p.StandardError.ReadToEnd()}");
            }
            else
            {
                Debug.Log("[WebUiHost] pnpm install complete");
            }
        }

        private Process SpawnViteDev(string nodePath, string pnpmPath, string cwd)
        {
            // node <pnpm> exec vite --port 5173 --strictPort --host 127.0.0.1
            // pnpm exec を通すと webui/node_modules 内の vite を使ってくれる
            // Using pnpm exec routes to the vite installed in webui/node_modules
            var psi = new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = $"\"{pnpmPath}\" exec vite --port 5173 --strictPort --host 127.0.0.1",
                WorkingDirectory = cwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += OnViteStdout;
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning($"[Vite] {e.Data}"); };
            p.Exited += (_, _) => Debug.Log("[WebUiHost] Vite process exited");
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            Debug.Log($"[WebUiHost] spawned Vite (pid={p.Id})");
            return p;
        }

        private void OnViteStdout(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            Debug.Log($"[Vite] {e.Data}");
            if (e.Data.Contains("ready in") || e.Data.Contains("Local:"))
            {
                _readySignal.Set();
            }
        }

        private async UniTask WaitForReadyAsync(int timeoutSec)
        {
            var start = DateTime.UtcNow;
            while (!_readySignal.IsSet)
            {
                if ((DateTime.UtcNow - start).TotalSeconds > timeoutSec)
                {
                    Debug.LogError($"[WebUiHost] Vite did not become ready within {timeoutSec}s");
                    return;
                }
                await UniTask.Delay(100);
            }
        }

        #endregion
    }
}
```

- [ ] **Step 2: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteProcess.cs
git commit -m "ViteProcess: Node spawn と Vite ready 検出"
```

---

## Task 10: WebUiHost facade（起動/停止の単一入口）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`

- [ ] **Step 1: `WebUiHost.cs` を作成**

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Web UI ホストの起動/停止の静的 facade。
    /// Web UI host static facade for start/stop and Hub access.
    /// </summary>
    public static class WebUiHost
    {
        private static KestrelServer _kestrel;
        private static ViteProcess _vite;
        private static WebSocketHub _hub;

        public static WebSocketHub Hub => _hub;

        public static async UniTask StartAsync()
        {
            // 二重起動防止
            // Prevent double-start
            if (_kestrel != null) return;

            _hub = new WebSocketHub();

            _kestrel = new KestrelServer();
            await _kestrel.StartAsync(_hub);

            _vite = new ViteProcess();
            await _vite.StartAsync();

            Debug.Log("[WebUiHost] ready. Open http://localhost:5173/");
        }

        public static void Stop()
        {
            if (_kestrel == null) return;

            // 先に WS を閉じてから HTTP を止め、最後に Vite を kill
            // Close WS first, stop HTTP, then kill Vite
            _hub?.CloseAllAsync().GetAwaiter().GetResult();
            _kestrel.StopAsync().GetAwaiter().GetResult();
            _vite?.Kill();

            _hub = null;
            _kestrel = null;
            _vite = null;
        }
    }
}
```

- [ ] **Step 2: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs
git commit -m "WebUiHost facade: Kestrel と Vite の統合起動/停止"
```

---

## Task 11: InventoryTopic（LocalPlayerInventory → WS）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/InventoryTopic.cs`

`InventoryTopic` は `LocalPlayerInventoryController` 経由で `ILocalPlayerInventory` を取得し、`OnItemChange` を購読して `local_player.inventory` トピックに push する。

- [ ] **Step 1: `InventoryTopic.cs` を作成**

```csharp
using System.Text;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Boot;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// local_player.inventory トピック: スロット変更のたびに全量を push
    /// local_player.inventory topic: pushes the full inventory on every slot change
    /// </summary>
    public class InventoryTopic : ITopicHandler
    {
        public const string TopicName = "local_player.inventory";

        private readonly WebSocketHub _hub;
        private readonly LocalPlayerInventoryController _controller;

        public InventoryTopic(WebSocketHub hub, LocalPlayerInventoryController controller)
        {
            _hub = hub;
            _controller = controller;

            // スロット変更通知を購読して全量を配信
            // Subscribe to slot-change notifications and broadcast the full inventory
            _controller.LocalPlayerInventory.OnItemChange.Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson(_controller.LocalPlayerInventory));
        }

        #region Internal

        private void Publish()
        {
            var json = BuildJson(_controller.LocalPlayerInventory);
            _hub.Publish(TopicName, json);
        }

        private static string BuildJson(ILocalPlayerInventory inv)
        {
            // ホットバーは既存定数がある場合はそちらに従うが、本スペックでは
            // 単純化のため全スロットを mainSlots として出力する。
            // Hotbar splitting will be refined later; for this spec we dump
            // every slot into mainSlots to keep things simple.
            var sb = new StringBuilder();
            sb.Append("{\"mainSlots\":[");
            for (var i = 0; i < inv.Count; i++)
            {
                var stack = inv[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"itemId\":");
                sb.Append(stack.Id.AsPrimitive());
                sb.Append(",\"count\":");
                sb.Append(stack.Count);
                sb.Append('}');
            }
            sb.Append("],\"hotBarSlots\":[]}");
            return sb.ToString();
        }

        #endregion
    }
}
```

注: `ItemId.AsPrimitive()` は Mooresmaster 生成の int プリミティブ返却メソッド。

- [ ] **Step 2: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/InventoryTopic.cs
git commit -m "InventoryTopic: ローカルプレイヤーインベントリの WS 配信"
```

---

## Task 12: WebUiGameBinder facade

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`

- [ ] **Step 1: `WebUiGameBinder.cs` を作成**

```csharp
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// ゲーム系トピックを WebUiHost.Hub にバインドする facade。
    /// ClientContext / ClientDIContext の準備が終わった後に 1 回呼ぶ。
    /// Facade that binds game-side topics to WebUiHost.Hub.
    /// Must be called once, after ClientContext / ClientDIContext are ready.
    /// </summary>
    public static class WebUiGameBinder
    {
        public static void BindTopics()
        {
            var hub = WebUiHost.Hub;
            if (hub == null) return;

            // DI からインベントリコントローラを取得
            // Resolve the inventory controller from DI
            var controller = ClientDIContext.DIContainer
                .DIContainerResolver
                .Resolve<LocalPlayerInventoryController>();

            var inventoryTopic = new InventoryTopic(hub, controller);
            hub.RegisterTopic(InventoryTopic.TopicName, inventoryTopic);
        }
    }
}
```

- [ ] **Step 2: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs
git commit -m "WebUiGameBinder: ゲーム系トピックを Hub にバインド"
```

---

## Task 13: InitializeScenePipeline 統合

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`

- [ ] **Step 1: `InitializeScenePipeline.cs` の using 追加と Initialize 先頭への hook 挿入、ClientContext 生成後への bind 呼び出し追加**

変更箇所 1: ファイル先頭の using にこの 3 行を追加。

```csharp
using Client.Game.Common;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game;
```

変更箇所 2: `Initialize()` の最序盤（`var args = CliConvert.Parse...` の前）に以下を挿入。

```csharp
            // ---- Web UI サーバーの起動（最序盤）----
            // ---- Web UI server bootstrap (earliest phase) ----
            await WebUiHost.StartAsync();
            GameShutdownEvent.OnGameShutdown.Subscribe(_ => WebUiHost.Stop());
```

変更箇所 3: `new ClientContext(...)` の**直後、MainGameScene 遷移の前**に以下を挿入。

位置: 既存の `new ClientContext(...)` 行と `SceneManager.sceneLoaded += MainGameSceneLoaded;` 行の間。

```csharp
            // Web UI ゲーム系トピックを Hub にバインド
            // Bind game-side Web UI topics to the hub
            WebUiGameBinder.BindTopics();
```

注意: `new ClientContext(...)` は `ClientDIContext` 生成前に走るが、`WebUiGameBinder.BindTopics()` は `ClientDIContext` の `DIContainer` を参照する。`ClientDIContext` は `MainGameSceneLoaded` 内で作られる。よって `BindTopics()` は `MainGameSceneLoaded` 側に入れる必要がある。

**差し戻し修正:** 変更箇所 3 は `MainGameSceneLoaded` 内（既存の `new ClientDIContext(...)` 直後、`GameInitializedEvent.FireGameInitialized();` の前）に入れる。

```csharp
            void MainGameSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= MainGameSceneLoaded;
                var starter = FindObjectOfType<MainGameStarter>();
                var resolver = starter.StartGame(handshakeResponse);
                new ClientDIContext(new DIContainer(resolver));

                // Web UI ゲーム系トピックを Hub にバインド
                // Bind game-side Web UI topics to the hub
                WebUiGameBinder.BindTopics();

                // ゲーム初期化完了イベントを発火
                // Fire game initialization complete event
                GameInitializedEvent.FireGameInitialized();
            }
```

- [ ] **Step 2: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラーなし。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs
git commit -m "InitializeScenePipeline に WebUiHost 起動と Topic bind を統合"
```

---

## Task 14: Unity を起動して Kestrel + Vite の統合を手動検証

**Files:** なし（動作確認のみ）

- [ ] **Step 1: Unity エディタを起動**

```bash
uloop launch --project-path ./moorestech_client
```

- [ ] **Step 2: Unity エディタ内でメインメニュー → ローカルゲーム開始**

InitializeScenePipeline が走るシーンで動かす。

- [ ] **Step 3: ログ確認**

```bash
uloop get-logs --project-path ./moorestech_client --log-type Log | grep WebUiHost
```

Expected:
```
[WebUiHost] Kestrel started at http://127.0.0.1:5050
[WebUiHost] spawned Vite (pid=XXXX)
[Vite] ...Local: http://127.0.0.1:5173/
[WebUiHost] ready. Open http://localhost:5173/
```

- [ ] **Step 4: ブラウザで `http://localhost:5173/` を開く**

Expected: "moorestech Web UI / bootstrapping..." が表示される。

- [ ] **Step 5: `http://localhost:5173/api/ping` を叩いて疎通確認**

```bash
curl -s http://localhost:5173/api/ping
```

Expected: `{"ok":true}`

- [ ] **Step 6: ゲームをメインメニューに戻す → Vite プロセスが kill されていることを ps で確認**

```bash
# macOS / Linux
ps aux | grep -i vite | grep -v grep
```

Expected: Vite プロセスが存在しない。

- [ ] **Step 7: エラーが出ていない場合は **Task 14 完了** とマーク。出た場合はログを debug して Task 9 / Task 10 に戻る**

この検証で通らない代表パターン:
- Node バイナリが見つからない → setup.sh 再実行
- `pnpm install` が失敗 → ネットワーク確認、pnpm/node バージョン確認
- Vite が 5173 にバインドできない → 他プロセスとの衝突、`lsof -i :5173`
- `/api/ping` が Vite 経由で届かない → `vite.config.ts` の proxy 設定確認
- ブラウザでコンソールエラー → React scaffold の記述確認

---

## Task 15: WebSocket クライアント基盤（webSocketClient.ts + useTopic.ts）

**Files:**
- Create: `moorestech_web/webui/src/bridge/webSocketClient.ts`
- Create: `moorestech_web/webui/src/bridge/useTopic.ts`

- [ ] **Step 1: `src/bridge/webSocketClient.ts` を作成**

```ts
// Unity 側 Web UI ホストと通信する WebSocket クライアント。
// 購読モデル: subscribe / unsubscribe / snapshot の 3 種類を送り、
// snapshot / event / error の 3 種類を受ける。
// WebSocket client that talks to the Unity-side Web UI host.
// Subscribe-model protocol: sends subscribe / unsubscribe / snapshot,
// receives snapshot / event / error.

export type ServerMsg =
  | { op: "snapshot"; topic: string; data: unknown }
  | { op: "event"; topic: string; data: unknown }
  | { op: "error"; message: string };

type Listener = (data: unknown) => void;

class WebSocketClient {
  private ws: WebSocket | null = null;
  private readonly url: string;
  private readonly listeners = new Map<string, Set<Listener>>();
  private readonly pendingSubscribes = new Set<string>();
  private reconnectDelayMs = 100;
  private closedByUs = false;

  constructor(url: string) {
    this.url = url;
  }

  connect() {
    this.closedByUs = false;
    this.openSocket();
  }

  close() {
    this.closedByUs = true;
    this.ws?.close();
    this.ws = null;
  }

  subscribe(topic: string, listener: Listener): () => void {
    let set = this.listeners.get(topic);
    if (!set) {
      set = new Set();
      this.listeners.set(topic, set);
    }
    set.add(listener);

    this.sendSubscribe(topic);

    return () => {
      set!.delete(listener);
      if (set!.size === 0) {
        this.listeners.delete(topic);
        this.sendRaw({ op: "unsubscribe", topics: [topic] });
      }
    };
  }

  private sendSubscribe(topic: string) {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.sendRaw({ op: "subscribe", topics: [topic] });
    } else {
      this.pendingSubscribes.add(topic);
    }
  }

  private sendRaw(obj: unknown) {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(obj));
    }
  }

  private openSocket() {
    const ws = new WebSocket(this.url);
    this.ws = ws;

    ws.onopen = () => {
      this.reconnectDelayMs = 100;
      // 保留中の subscribe と、再接続時は listener が登録済みのトピックを全て再購読
      // On (re)connect, flush pending subscribes and re-subscribe known topics
      const toSub = new Set<string>();
      this.pendingSubscribes.forEach((t) => toSub.add(t));
      this.listeners.forEach((_, t) => toSub.add(t));
      this.pendingSubscribes.clear();
      if (toSub.size > 0) {
        this.sendRaw({ op: "subscribe", topics: Array.from(toSub) });
      }
    };

    ws.onmessage = (ev) => {
      const msg = JSON.parse(String(ev.data)) as ServerMsg;
      if (msg.op === "snapshot" || msg.op === "event") {
        const set = this.listeners.get(msg.topic);
        if (set) set.forEach((l) => l(msg.data));
      } else if (msg.op === "error") {
        console.error("[ws] server error:", msg.message);
      }
    };

    ws.onerror = () => {
      // onerror 後は onclose が続くので特に何もしない
      // onerror is followed by onclose, no action needed here
    };

    ws.onclose = () => {
      this.ws = null;
      if (this.closedByUs) return;
      // 指数バックオフで再接続（上限 5 秒）
      // Exponential backoff reconnect (capped at 5s)
      const delay = Math.min(this.reconnectDelayMs, 5000);
      this.reconnectDelayMs = Math.min(this.reconnectDelayMs * 2, 5000);
      setTimeout(() => this.openSocket(), delay);
    };
  }
}

// モジュール内シングルトンで接続を保持
// Keep the connection as a module-level singleton
const client = new WebSocketClient(`ws://${location.host}/ws`);
client.connect();

export function subscribeTopic<T>(topic: string, listener: (data: T) => void) {
  return client.subscribe(topic, (d) => listener(d as T));
}
```

- [ ] **Step 2: `src/bridge/useTopic.ts` を作成**

```ts
import { useEffect, useState } from "react";
import { subscribeTopic } from "./webSocketClient";

// 指定トピックを購読して最新の値を返す React フック
// React hook that subscribes to a topic and returns the latest value
export function useTopic<T>(topic: string): T | null {
  const [value, setValue] = useState<T | null>(null);
  useEffect(() => {
    const unsub = subscribeTopic<T>(topic, (data) => setValue(data));
    return unsub;
  }, [topic]);
  return value;
}
```

- [ ] **Step 3: TS チェック**

```bash
cd moorestech_web/webui
../node/mac-arm64/pnpm exec tsc --noEmit
```

Expected: エラーなし。

- [ ] **Step 4: Commit**

```bash
git add moorestech_web/webui/src/bridge/
git commit -m "webSocketClient と useTopic: 購読モデル WS クライアント"
```

---

## Task 16: InventoryView（実データ表示）

**Files:**
- Create: `moorestech_web/webui/src/components/InventoryView.tsx`
- Modify: `moorestech_web/webui/src/App.tsx`

- [ ] **Step 1: `src/components/InventoryView.tsx` を作成**

```tsx
import { useTopic } from "../bridge/useTopic";

type InventoryData = {
  mainSlots: Array<{ itemId: number; count: number }>;
  hotBarSlots: Array<{ itemId: number; count: number }>;
};

// ローカルプレイヤーのインベントリを WS 購読して表示
// Subscribe to the local player's inventory over WS and render it
export default function InventoryView() {
  const inventory = useTopic<InventoryData>("local_player.inventory");

  if (!inventory) {
    return <div className="text-sm text-gray-400">connecting...</div>;
  }

  return (
    <div>
      <h2 className="text-lg font-semibold mb-2">Main Inventory</h2>
      <div className="grid grid-cols-9 gap-1">
        {inventory.mainSlots.map((s, i) => (
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

- [ ] **Step 2: `src/App.tsx` を更新**

```tsx
import InventoryView from "./components/InventoryView";

export default function App() {
  return (
    <div className="p-4 space-y-4">
      <h1 className="text-2xl font-bold">moorestech Web UI</h1>
      <InventoryView />
    </div>
  );
}
```

- [ ] **Step 3: TS チェック**

```bash
cd moorestech_web/webui
../node/mac-arm64/pnpm exec tsc --noEmit
```

Expected: エラーなし。

- [ ] **Step 4: Commit**

```bash
git add moorestech_web/webui/src/
git commit -m "InventoryView: local_player.inventory を購読して表示"
```

---

## Task 17: エンドツーエンド動作確認

**Files:** なし（動作確認のみ）

- [ ] **Step 1: Unity エディタ再起動（最新コードを反映）**

```bash
uloop launch --project-path ./moorestech_client
```

- [ ] **Step 2: ローカルゲームを開始（InitializeScenePipeline が走るフロー）**

- [ ] **Step 3: ブラウザで `http://localhost:5173/` を開く**

Expected: インベントリスロットのグリッドが表示される。全スロット空（`count: 0`）の場合はスロット番号 `#0` `#1` ... のみが見える。

- [ ] **Step 4: Chrome DevTools の Network タブで WS 接続を確認**

Expected:
- `ws://localhost:5173/ws` が 101 Switching Protocols
- フレーム: 送信 `{"op":"subscribe","topics":["local_player.inventory"]}`、受信 `{"op":"snapshot","topic":"local_player.inventory","data":{"mainSlots":[...],"hotBarSlots":[]}}`

- [ ] **Step 5: ゲーム内でアイテムを取得または消費**

Expected: ブラウザ側の表示が即時更新される。DevTools に受信 `{"op":"event","topic":"local_player.inventory","data":{...}}` が追記される。

- [ ] **Step 6: ゲームをメインメニューに戻す**

Expected:
- Unity ログ: `[WebUiHost] Kestrel stopped`、`[WebUiHost] Vite process killed`
- ブラウザ側: WS が切断され、再接続を試行するがサーバー停止中は失敗し続ける
- `ps` コマンド: Vite プロセス残存なし

- [ ] **Step 7: 再度ローカルゲーム開始**

Expected: ブラウザ側の WS が自動再接続し、インベントリが snapshot で再同期される。

- [ ] **Step 8: すべて通った場合、完了コミットを作る**

```bash
git commit --allow-empty -m "Web UI 基盤 3 タスクのエンドツーエンド動作確認完了"
```

---

## Task 18: Unity 生成 .meta ファイルを取り込み

Unity を起動すると、新規作成した .cs / .asmdef ファイルに対応する .meta が自動生成される。この .meta を commit しないと他環境で asmdef 認識されない。

**Files:** 自動生成された `.meta` 群

- [ ] **Step 1: Unity を一度起動して .meta を自動生成させる**

Task 14 / 17 で既に起動済みなら OK。

- [ ] **Step 2: git status で .meta が出ていることを確認**

```bash
git status --short | grep "\.meta$"
```

Expected: 新規追加された `.cs` / asmdef ごとに `.meta` が ?? で表示される。

- [ ] **Step 3: .meta を追加 commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/
git add moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs.meta
git commit -m "Unity 自動生成の .meta を取り込み"
```

---

## Self-Review チェック

### Spec coverage

| Spec セクション | カバーするタスク |
|---|---|
| 1. スコープと完了条件 | Task 17 が完了条件を検証 |
| 2. リポジトリ構成 | Task 1 (moorestech_web scaffold) + Task 4 (Client.WebUiHost asmdef) + Task 2 (webui) |
| 3. アーキテクチャ（プロセス・ポート） | Task 8 (Kestrel 5050) + Task 9 (Vite 5173) + Task 2 (vite.config proxy) |
| 4.1 起動シーケンス | Task 10 (WebUiHost facade) + Task 13 (InitializeScenePipeline 統合) |
| 4.2 終了シーケンス | Task 3 (GameShutdownEvent + BackToMainMenu fire) + Task 13 (subscribe → Stop) |
| 4.3 失敗時挙動 | Task 9 (ViteProcess のバイナリ不在時 LogError) |
| 5.1 Boot 層 | Task 5/6/7/8/9/10 |
| 5.2 Game 層 | Task 11/12 |
| 6. WebSocket プロトコル | Task 6 (WebSocketHub envelope) + Task 15 (クライアント側) |
| 7. インベントリトピック | Task 11 (InventoryTopic) + Task 16 (InventoryView) |
| 8. Main Thread Dispatch | （本プランではシンプル化のため、`OnItemChange` は Unity main thread から発火されると想定。必要になれば ViteProcess/Kestrel ハンドラ内で `UniTask.SwitchToMainThread()` を足す。本スペック範囲での追加なし） |
| 9. セキュリティ最低線 | Task 2 (Vite fs.allow + 127.0.0.1) + Task 7 (WS Origin 検査) + Task 8 (Kestrel 127.0.0.1) |
| 10. 後続スペック | 本プラン範囲外、スコープアウト記述済み |
| 11. 変更対象ファイル | Task 3 (BackToMainMenu + GameShutdownEvent) + Task 13 (InitializeScenePipeline) + 各新規 |

### Placeholder scan

- 「TBD」「TODO」「fill in later」なし
- 「add appropriate error handling」なし（nullチェック・条件分岐で具体化）
- 「similar to Task N」なし（各タスクのコードを完全記述）
- 「write tests for the above」なし

### Type consistency

- `ItemId.AsPrimitive()` は int を返す前提（server 側のテストコードで `new ItemId(1)` が通るため）
- WS envelope は C# 側（`WebSocketHub`）と TS 側（`webSocketClient.ts`）で `op` / `topic` / `data` キー命名が揃っている
- トピック名 `"local_player.inventory"` は C# 側 `InventoryTopic.TopicName` と TS 側 `InventoryView` の `useTopic` 引数で完全一致
- `WebUiHost.Hub` は `WebSocketHub` 型で統一

### 注意点（実装者向け）

1. `System.Threading.Channels.Channel` が netstandard2.1 で見つからない場合は `BlockingCollection<string>` で書き換える（Task 6）。
2. `Client.WebUiHost.asmdef` の `references` に実際のアセンブリ名が一致しているか、Unity でコンパイル後に要確認（Task 4）。
3. `ItemId.AsPrimitive()` がもし int 以外（long / Guid 等）を返していた場合、`InventoryTopic.BuildJson()` の数値直書きを文字列化に変更する必要あり（Task 11）。
4. Vite の proxy 設定の `ws: true` は WebSocket を透過させる必須オプション。外すと `/ws` が 404 になる（Task 2）。
5. `InitializeScenePipeline` の `WebUiHost.StartAsync()` は Addressables 初期化より前に置くため、Addressables 側の挙動を阻害しないか初回検証で観察する（Task 13, 14）。

---

## 実行ハンドオフ

**Plan complete and saved to `docs/superpowers/plans/2026-04-22-web-ui-foundation.md`.**

2 つの実行モードから選んでください:

**1. Subagent-Driven（推奨）** — タスクごとに fresh subagent を dispatch、タスク間でレビュー、高速イテレーション

**2. Inline Execution** — このセッション内で executing-plans を使い、バッチ実行 + チェックポイントでレビュー

どちらで進めますか?
