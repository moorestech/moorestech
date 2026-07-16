# Web UI 動的ポート化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Web UI のポート（Vite/Kestrel）をベース 25173/25050 + 自動インクリメント探索の動的割り当てにし、複数 Unity プロセスの同時起動を可能にする。CEF・CORS・残留プロセス掃除も実ポート追従にする。

**Architecture:** Kestrel がベースから bind 探索で実ポートを確定 → 環境変数で Vite に注入 → Vite は strictPort 無しで自動インクリメントし stdout から実ポートをパース → 確定値を `WebUiPortConfig`（static 保持）へ書き、CORS 検査・CEF ナビゲーション・ready ログが読む。単一の書き手（WebUiHost 起動シーケンス）と複数の読み手という一方向フロー。

**Tech Stack:** Unity 6 / C# (Client.WebUiHost asmdef), ASP.NET Core Kestrel, Vite 5, uloop CLI

**Spec:** `docs/superpowers/specs/2026-07-16-webui-dynamic-ports-design.md`

## Global Constraints

- 1 ファイル 200 行以下。partial 禁止。デフォルト引数禁止
- try-catch は外部境界（OS ネットワーク bind・外部プロセス）のみ。境界根拠コメント必須
- 主要処理に日本語→英語の 2 行セットコメント（各 1 行厳守）
- 単純な public setter プロパティ禁止。Set は `public void SetHoge(値)` メソッド
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`
- Prefab はテキスト編集禁止。`uloop execute-dynamic-code`（Unity Editor 経由）のみ
- エディタ専用コードは `#if UNITY_EDITOR` で囲みファイル末尾
- タスクごとにコミット（worktree 運用のため作業消失防止）

## 配置と前例（spec-architecture-review 記録）

| 項目 | 配置 | 前例・根拠 |
|---|---|---|
| `WebUiPortConfig`（ベース定数+実 Vite ポート） | `Client.WebUiHost/Common/` | 同層の `WebUiPaths.cs`（static 設定値保持）と同形 |
| Kestrel bind 探索 | `KestrelServer.cs`（既存改修） | try-catch は OS ネットワーク境界（AGENTS.md 許容条項） |
| Vite stdout ポートパース | 新規 `ViteOutputParser.cs`（純関数） | 既存 ready 判定（`Local:` 検知）の発展。純関数分離でテスト可能に |
| 残留プロセス掃除 | `ViteProcessKiller.cs`（既存改修） | 既存の lsof ベース pid 特定を汎用化。SessionState は `InitializeScenePipeline.SkipSaveLoadSessionKey` に前例 |
| CEF ナビゲーション | 新規 `Client.WebUiHost/Cef/WebUiCefNavigator.cs` | Client.Game は Client.WebUiHost から参照される側（逆参照不可）のため WebUiHost 側に配置。asmdef に `CefUnity.Runtime` を追加 |
| prefab `_url` 変更 | uloop execute-dynamic-code | プロジェクト規約（Unity シリアライズ経由のみ許容） |

**機能パリティ（死活表）:** CEF 表示=生存（LoadUrl 経由に変化）／Ctrl+I トグル=無関係で生存／外部ブラウザ閲覧=生存（実 URL は ready ログ参照に変化）／`pnpm dev` 単体=生存（25050 フォールバック）／e2e=無関係（独自 PORT+mock-host）。喪失機能なし。

**KestrelPort は WebUiPortConfig に持たせない:** 読み手が ViteProcess への env 注入 1 箇所のみで、`kestrel.ActualPort` を直接引数で渡せば足りる（YAGNI）。共有 static に置くのは複数読み手がいる VitePort だけ。

---

### Task 1: WebUiPortConfig 新設

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Common/WebUiPortConfig.cs`

**Interfaces:**
- Produces: `WebUiPortConfig.KestrelBasePort` (const int 25050), `ViteBasePort` (const int 25173), `PortSearchRange` (const int 20), `VitePort` (static int get), `SetVitePort(int port)` (static void)

- [ ] **Step 1: 実装**

```csharp
namespace Client.WebUiHost.Common
{
    /// <summary>
    /// Web UI のベースポート定数と、起動時に確定した実ポートの保持
    /// Base port constants for the Web UI and the actual ports resolved at startup
    /// </summary>
    public static class WebUiPortConfig
    {
        // ベース値は ephemeral レンジ（Linux 32768〜 / macOS・Win 49152〜）より下の非常用帯から選定
        // Base values sit below every OS ephemeral range (Linux 32768+, macOS/Win 49152+) in an uncommon band
        public const int KestrelBasePort = 25050;
        public const int ViteBasePort = 25173;

        // ベースから何ポートまでインクリメント探索するか
        // How many ports to probe upward from the base
        public const int PortSearchRange = 20;

        // 起動時に確定した Vite の実ポート。0 は未確定（CORS 検査は全拒否になる）
        // Actual Vite port resolved at startup; 0 means unresolved (CORS check rejects everything)
        public static int VitePort => _vitePort;
        private static int _vitePort;

        public static void SetVitePort(int port)
        {
            _vitePort = port;
        }
    }
}
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Common/WebUiPortConfig.cs*
git commit -m "feat(webui): ポート定数と実ポート保持のWebUiPortConfigを新設"
```

（.meta は Unity が生成したものを含めてコミットする。以降のタスクも同様）

---

### Task 2: ViteOutputParser（stdout 実ポートパース・TDD）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteOutputParser.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/ViteOutputParserTest.cs`

**Interfaces:**
- Produces: `ViteOutputParser.TryParseLocalPort(string line, out int port)` (static bool)

- [ ] **Step 1: 失敗するテストを書く**

Vite の実出力は ANSI カラーコードを含む（例: `  ➜  Local:   \x1b[36mhttp://127.0.0.1:\x1b[1m25173\x1b[22m/\x1b[39m`。ポート番号自体が装飾で分断される）ため、ANSI 除去込みでテストする。

```csharp
using Client.WebUiHost.Boot;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class ViteOutputParserTest
    {
        [Test]
        public void プレーンなLocal行からポートを取得できる()
        {
            var ok = ViteOutputParser.TryParseLocalPort("  ➜  Local:   http://127.0.0.1:25173/", out var port);
            Assert.IsTrue(ok);
            Assert.AreEqual(25173, port);
        }

        [Test]
        public void ANSIカラーコード付きのLocal行からポートを取得できる()
        {
            var line = "  ➜  Local:   \x1b[36mhttp://127.0.0.1:\x1b[1m25174\x1b[22m/\x1b[39m";
            var ok = ViteOutputParser.TryParseLocalPort(line, out var port);
            Assert.IsTrue(ok);
            Assert.AreEqual(25174, port);
        }

        [Test]
        public void ポートを含まない行はfalseを返す()
        {
            Assert.IsFalse(ViteOutputParser.TryParseLocalPort("  VITE v5.4.0  ready in 312 ms", out _));
            Assert.IsFalse(ViteOutputParser.TryParseLocalPort("  ➜  Network: use --host to expose", out _));
            Assert.IsFalse(ViteOutputParser.TryParseLocalPort("", out _));
        }
    }
}
```

- [ ] **Step 2: テストが失敗（コンパイルエラー）することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ViteOutputParser` 未定義のコンパイルエラー

- [ ] **Step 3: 実装**

```csharp
using System.Text.RegularExpressions;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Vite dev server の stdout から実ポートをパースする純関数
    /// Pure parser that extracts the actual port from Vite dev server stdout
    /// </summary>
    public static class ViteOutputParser
    {
        // ANSI エスケープ（色・装飾）を除去してから Local 行の URL 末尾ポートを取る
        // Strip ANSI escapes (colors/styles) first, then capture the trailing port of the Local line URL
        private static readonly Regex AnsiEscapeRegex = new(@"\x1b\[[0-9;]*m");
        private static readonly Regex LocalPortRegex = new(@"Local:\s+https?://[^:/\s]+:(\d+)");

        public static bool TryParseLocalPort(string line, out int port)
        {
            port = 0;
            if (string.IsNullOrEmpty(line)) return false;

            var plain = AnsiEscapeRegex.Replace(line, "");
            var match = LocalPortRegex.Match(plain);
            if (!match.Success) return false;

            return int.TryParse(match.Groups[1].Value, out port) && port > 0;
        }
    }
}
```

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ViteOutputParserTest"`
Expected: 3 件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteOutputParser.cs* moorestech_client/Assets/Scripts/Client.Tests/WebUi/ViteOutputParserTest.cs*
git commit -m "feat(webui): Vite stdoutから実ポートをパースするViteOutputParserを追加"
```

---

### Task 3: KestrelServer ポート探索

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/KestrelServer.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/KestrelServerPortScanTest.cs`

**Interfaces:**
- Consumes: `WebUiPortConfig.KestrelBasePort`, `PortSearchRange`（Task 1）
- Produces: `KestrelServer.ActualPort` (int get)。`StartAsync(WebSocketHub hub)` のシグネチャは不変

- [ ] **Step 1: 失敗するテストを書く**

ベースポートを TcpListener で占有した状態で起動し、別ポートへ逃げることを検証する。実ソケットを使う統合テスト。

```csharp
using System.Net;
using System.Net.Sockets;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class KestrelServerPortScanTest
    {
        [Test]
        public void ベースポート占有時は次のポートで起動する()
        {
            // ベースポートをダミーで占有する（他Editorが既に占有している場合もテスト成立）
            // Occupy the base port with a dummy listener (test also holds if another Editor owns it)
            TcpListener blocker = null;
            var basePortWasFree = TryListen(WebUiPortConfig.KestrelBasePort, out blocker);

            var kestrel = new KestrelServer();
            kestrel.StartAsync(new WebSocketHub()).GetAwaiter().GetResult();

            Assert.AreNotEqual(WebUiPortConfig.KestrelBasePort, kestrel.ActualPort);
            Assert.That(kestrel.ActualPort, Is.GreaterThan(WebUiPortConfig.KestrelBasePort));
            Assert.That(kestrel.ActualPort, Is.LessThan(WebUiPortConfig.KestrelBasePort + WebUiPortConfig.PortSearchRange));

            kestrel.StopAsync().GetAwaiter().GetResult();
            if (basePortWasFree) blocker.Stop();
        }

        private static bool TryListen(int port, out TcpListener listener)
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            // ポートが既に他プロセスに占有されている場合は SocketException になる。OS ネットワーク境界の隔離
            // An already-occupied port throws SocketException; isolating the OS network boundary
            try
            {
                listener.Start();
                return true;
            }
            catch (SocketException)
            {
                listener = null;
                return false;
            }
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ActualPort` 未定義のコンパイルエラー

- [ ] **Step 3: KestrelServer を改修**

`KestrelServer.cs` 全体を以下に置き換え:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Client.WebUiHost.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Kestrel IWebHost の起動/停止を包むラッパ。ベースポートから空きを探索して bind する
    /// Wrapper around Kestrel IWebHost lifecycle; probes upward from the base port for a free one
    /// </summary>
    public class KestrelServer
    {
        private IWebHost _webHost;

        // 起動時に確定した実ポート。未起動時は 0
        // Actual port resolved at startup; 0 before start
        public int ActualPort => _actualPort;
        private int _actualPort;

        public async Task StartAsync(WebSocketHub hub)
        {
            // ベースから 1 ずつ上げながら bind を試行し、最初に成功したポートを採用する
            // Probe upward from the base port and adopt the first successful bind
            for (var port = WebUiPortConfig.KestrelBasePort; port < WebUiPortConfig.KestrelBasePort + WebUiPortConfig.PortSearchRange; port++)
            {
                var url = $"http://127.0.0.1:{port}";
                var webHost = new WebHostBuilder()
                    .UseKestrel()
                    .UseUrls(url)
                    .ConfigureServices(services => services.AddRouting())
                    .Configure(app => WebUiEndpoints.Configure(app, hub))
                    .Build();

                // ポート使用中の bind 失敗は IOException で通知される。OS ネットワーク境界の隔離のためここに限り try-catch を使用
                // A bind on an occupied port surfaces as IOException; try-catch here only isolates the OS network boundary
                try
                {
                    await webHost.StartAsync();
                }
                catch (IOException)
                {
                    webHost.Dispose();
                    continue;
                }

                _webHost = webHost;
                _actualPort = port;
                Debug.Log($"[WebUiHost] Kestrel started at {url}");
                return;
            }

            throw new InvalidOperationException(
                $"[WebUiHost] no free port in {WebUiPortConfig.KestrelBasePort}..{WebUiPortConfig.KestrelBasePort + WebUiPortConfig.PortSearchRange - 1}");
        }

        public async Task StopAsync()
        {
            if (_webHost == null) return;

            // 最大 2 秒で graceful shutdown
            // Graceful shutdown capped at 2 seconds
            await _webHost.StopAsync(TimeSpan.FromSeconds(2));
            _webHost.Dispose();
            _webHost = null;
            _actualPort = 0;
            Debug.Log("[WebUiHost] Kestrel stopped");
        }
    }
}
```

注意: 全ポート枯渇の throw は既存の呼び出し側（`InitializeScenePipeline` の WebUI 非必須隔離 try + `WebUiHost.StartAsync` のロールバック finally）に乗る。新規のハンドリング追加は不要。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "KestrelServerPortScanTest"`
Expected: PASS

もし FAIL で例外型が `IOException` 以外（環境の ASP.NET Core バージョンによっては `AddressInUseException` 等）だった場合: ログの実例外型を確認し、catch 節をその型に合わせて修正して再実行する。**catch (Exception) への拡大は禁止**（設定ミス等の実バグを 20 回ループで隠蔽するため）。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/KestrelServer.cs moorestech_client/Assets/Scripts/Client.Tests/WebUi/KestrelServerPortScanTest.cs*
git commit -m "feat(webui): Kestrelをベース25050からのポート探索起動に変更"
```

---

### Task 4: WebUiEndpoints の CORS/WS オリジン検査を動的化

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs:144-148`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WebUiAllowedOriginTest.cs`

**Interfaces:**
- Consumes: `WebUiPortConfig.VitePort` / `SetVitePort`（Task 1）
- Produces: `WebUiEndpoints.IsAllowedOrigin(string origin)` (public static bool に昇格)

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class WebUiAllowedOriginTest
    {
        [Test]
        public void 確定済みViteポートのオリジンのみ許可する()
        {
            WebUiPortConfig.SetVitePort(25174);
            Assert.IsTrue(WebUiEndpoints.IsAllowedOrigin("http://localhost:25174"));
            Assert.IsTrue(WebUiEndpoints.IsAllowedOrigin("http://127.0.0.1:25174"));
            Assert.IsFalse(WebUiEndpoints.IsAllowedOrigin("http://localhost:5173"));
            Assert.IsFalse(WebUiEndpoints.IsAllowedOrigin("http://evil.example.com:25174"));
            Assert.IsFalse(WebUiEndpoints.IsAllowedOrigin(""));
        }

        [Test]
        public void Viteポート未確定時は全拒否する()
        {
            WebUiPortConfig.SetVitePort(0);
            Assert.IsFalse(WebUiEndpoints.IsAllowedOrigin("http://localhost:25173"));
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `IsAllowedOrigin` が private のためコンパイルエラー

- [ ] **Step 3: 実装**

`WebUiEndpoints.cs` の `IsAllowedOrigin` を以下に置換（`using Client.WebUiHost.Common;` を追加）:

```csharp
        // 起動時に確定した Vite 実ポートのオリジンだけを許可する（未確定 0 の間は全拒否）
        // Allow only the origin of the Vite port resolved at startup (reject all while unresolved = 0)
        public static bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin)) return false;

            var vitePort = WebUiPortConfig.VitePort;
            if (vitePort == 0) return false;

            return origin == $"http://localhost:{vitePort}" || origin == $"http://127.0.0.1:{vitePort}";
        }
```

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WebUiAllowedOriginTest"`
Expected: 2 件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs moorestech_client/Assets/Scripts/Client.Tests/WebUi/WebUiAllowedOriginTest.cs*
git commit -m "feat(webui): CORS/WSオリジン検査を実Viteポートとの突合に変更"
```

---

### Task 5: ViteProcessKiller 改修（SessionState 追跡 kill + 自 worktree 孤児掃除）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteProcessKiller.cs`

**Interfaces:**
- Consumes: `WebUiPortConfig.ViteBasePort` / `PortSearchRange`（Task 1）
- Produces（すべて `#if UNITY_EDITOR`）:
  - `ViteProcessKiller.RecordSpawned(int pid, int port)` (static void) — SessionState へ記録
  - `ViteProcessKiller.KillAnyLingering()` (static void) — 記録済み pid をポート照合のうえ kill（既存シグネチャ維持）
  - `ViteProcessKiller.KillOrphansOfThisWorkspace(string webuiRoot)` (static void) — ポート範囲内で cwd 一致の LISTEN プロセスを kill
- `KillProcessTree(int pid)` / `RunDetached(...)` は不変

- [ ] **Step 1: 実装**

`#if UNITY_EDITOR` ブロック（`KillAnyLingering` と `FindPidOnPort5173`）を以下に置き換える。`using Client.WebUiHost.Common;` を追加:

```csharp
#if UNITY_EDITOR
        // SessionState キー。ドメインリロードを跨いで自インスタンスの Vite (pid, port) を追跡する
        // SessionState keys tracking this instance's Vite (pid, port) across domain reloads
        private const string SessionKeyVitePid = "WebUiHost.VitePid";
        private const string SessionKeyVitePort = "WebUiHost.VitePort";

        public static void RecordSpawned(int pid, int port)
        {
            UnityEditor.SessionState.SetInt(SessionKeyVitePid, pid);
            UnityEditor.SessionState.SetInt(SessionKeyVitePort, port);
        }

        // 自インスタンスが記録した Vite を掃除する。pid 再利用誤爆を防ぐため「記録ポートを当該 pid が今も LISTEN しているか」を照合する
        // Sweep the Vite this instance recorded; verify the pid still listens on the recorded port to avoid pid-reuse misfire
        public static void KillAnyLingering()
        {
            var pid = UnityEditor.SessionState.GetInt(SessionKeyVitePid, 0);
            var port = UnityEditor.SessionState.GetInt(SessionKeyVitePort, 0);
            if (pid == 0 || port == 0) return;

            if (FindPidOnPort(port) == pid)
            {
                KillPid(pid);
            }
            UnityEditor.SessionState.EraseInt(SessionKeyVitePid);
            UnityEditor.SessionState.EraseInt(SessionKeyVitePort);
        }

        // クラッシュした過去セッションの孤児 Vite を掃除する。cwd が自 worktree の webuiRoot に一致するものだけを対象にし、他 worktree の Vite には触れない
        // Sweep orphaned Vite processes from crashed sessions; only those whose cwd matches this worktree's webuiRoot, never touching other worktrees
        public static void KillOrphansOfThisWorkspace(string webuiRoot)
        {
            var normalizedRoot = Path.GetFullPath(webuiRoot).TrimEnd(Path.DirectorySeparatorChar);
            for (var port = WebUiPortConfig.ViteBasePort; port < WebUiPortConfig.ViteBasePort + WebUiPortConfig.PortSearchRange; port++)
            {
                var pid = FindPidOnPort(port);
                if (pid == 0) continue;
                if (GetProcessCwd(pid) != normalizedRoot) continue;

                Debug.Log($"[WebUiHost] killing orphaned vite (pid={pid}, port={port})");
                KillPid(pid);
            }
        }

        private static void KillPid(int pid)
        {
#if UNITY_EDITOR_WIN
            RunDetached(@"C:\Windows\System32\taskkill.exe", $"/F /PID {pid}");
#else
            RunDetached("/bin/kill", $"-9 {pid}");
#endif
        }

        // 指定ポートを listen している pid を返す。見つからなければ 0
        // Return the pid listening on the given port; 0 if not found
        private static int FindPidOnPort(int port)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            var output = RunAndCapture(LsofPath(), $"-ti :{port} -sTCP:LISTEN");
            var firstLine = output.Trim().Split('\n')[0].Trim();
            return int.TryParse(firstLine, out var pid) ? pid : 0;
#else
            // Windows: TODO netstat ベースの pid 特定（現状は未対応）
            // Windows: TODO netstat-based pid lookup (not yet implemented)
            return 0;
#endif
        }

        // 指定 pid のカレントディレクトリを返す。取得できなければ null
        // Return the cwd of the given pid; null when unavailable
        private static string GetProcessCwd(int pid)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            // lsof -Fn の出力から cwd 行（n で始まる行）を取り出す
            // Extract the cwd line (prefixed with 'n') from lsof -Fn output
            var output = RunAndCapture(LsofPath(), $"-a -p {pid} -d cwd -Fn");
            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("n", StringComparison.Ordinal))
                    return line.Substring(1).Trim().TrimEnd(Path.DirectorySeparatorChar);
            }
            return null;
#else
            return null;
#endif
        }

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        private static string LsofPath()
        {
            return File.Exists("/usr/sbin/lsof") ? "/usr/sbin/lsof"
                 : File.Exists("/usr/bin/lsof") ? "/usr/bin/lsof"
                 : null;
        }

        // 外部コマンドを実行して stdout を回収する。コマンド欠如時は空文字
        // Run an external command and capture stdout; empty string when the command is missing
        private static string RunAndCapture(string fileName, string arguments)
        {
            if (fileName == null || !File.Exists(fileName)) return "";
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var p = Process.Start(psi);
            if (p == null) return "";
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1500);
            p.Dispose();
            return output;
        }
#endif
#endif
```

ファイル冒頭の using に `using Client.WebUiHost.Common;` を追加。クラスコメントの「5173」への言及も汎用表現に更新する。

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0（この時点で `RecordSpawned`/`KillOrphansOfThisWorkspace` は未参照の警告なしを確認）

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteProcessKiller.cs
git commit -m "feat(webui): 残留Vite掃除をSessionState追跡+cwd照合の自worktree限定に変更"
```

---

### Task 6: ViteProcess 動的化 + PnpmInstaller 分割

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/PnpmInstaller.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteProcess.cs`

**Interfaces:**
- Consumes: `ViteOutputParser.TryParseLocalPort`（Task 2）、`ViteProcessKiller.RecordSpawned` / `KillOrphansOfThisWorkspace`（Task 5）、`WebUiPortConfig.ViteBasePort`（Task 1）
- Produces:
  - `ViteProcess.StartAsync(int kestrelPort)` (UniTask&lt;bool&gt;) — **シグネチャ変更**（引数なし → int。デフォルト引数禁止のため呼び出し側 `WebUiHost` は Task 7 で更新）
  - `ViteProcess.ActualPort` (int get) — ready 後に確定
  - `PnpmInstaller.RunIfNeeded(string nodePath, string pnpmPath, string webuiRoot)` (static UniTask)

- [ ] **Step 1: PnpmInstaller を新設**

既存 `ViteProcess.cs` の `RunPnpmInstall` ローカル関数を移設（200 行制限対策 + 責務分離）:

```csharp
using System;
using System.Diagnostics;
using System.IO;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// node_modules が無い場合に pnpm install を実行する
    /// Runs pnpm install when node_modules is missing
    /// </summary>
    public static class PnpmInstaller
    {
        public static async UniTask RunIfNeeded(string nodePath, string pnpmPath, string webuiRoot)
        {
            if (Directory.Exists(Path.Combine(webuiRoot, "node_modules"))) return;

            Debug.Log("[WebUiHost] running pnpm install...");
            // pnpm はネイティブバイナリなので直接 FileName に指定し、node bin を PATH に追加
            // pnpm is a native binary; set it as FileName and prepend node bin dir to PATH
            var psi = new ProcessStartInfo
            {
                FileName = pnpmPath,
                Arguments = "install",
                WorkingDirectory = webuiRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var nodeBinDir = Path.GetDirectoryName(nodePath);
            psi.Environment["PATH"] = $"{nodeBinDir}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
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
    }
}
```

- [ ] **Step 2: ViteProcess を改修**

変更点の全量:

1. `using Client.WebUiHost.Common;` を追加
2. フィールドに実ポートを追加:

```csharp
        // stdout の Local 行から確定した実ポート。ready 前は 0
        // Actual port parsed from the stdout Local line; 0 before ready
        public int ActualPort => _actualPort;
        private volatile int _actualPort;
```

3. `StartAsync` のシグネチャと冒頭を変更:

```csharp
        public async UniTask<bool> StartAsync(int kestrelPort)
        {
            var nodePath = WebUiPaths.NodeBinary;
            var pnpmPath = WebUiPaths.PnpmBinary;
            var webuiRoot = WebUiPaths.WebuiRoot;

            // node/pnpm/webuiRoot が欠けていれば起動不可として false を返す（呼び出し元がホスト無効化に使う）
            // Return false when node/pnpm/webuiRoot is missing, marking startup unavailable (caller disables the host)
            if (!IsEnvironmentReady()) return false;

#if UNITY_EDITOR
            // クラッシュした過去セッションの孤児 Vite を spawn 前に掃除する（自 worktree 分のみ）
            // Sweep orphaned Vite processes from crashed sessions before spawning (this worktree only)
            ViteProcessKiller.KillOrphansOfThisWorkspace(webuiRoot);
#endif

            // node_modules が無ければ pnpm install を先に走らせる
            // Run pnpm install first if node_modules is missing
            await PnpmInstaller.RunIfNeeded(nodePath, pnpmPath, webuiRoot);

            _readySignal = new ManualResetEventSlim(false);
            _process = SpawnViteDev();

            // stdout の Local 行（実ポート確定）が出るまで待機（最大 30 秒）。時間内に来なければ false
            // Wait for the stdout Local line (actual port resolved), capped at 30 seconds; false when it does not arrive
            var ready = await WaitForReady(30);

#if UNITY_EDITOR
            // 確定した (pid, port) を SessionState へ記録し、クリーンアップ時の照合 kill に使う
            // Record the resolved (pid, port) in SessionState for verified kill during cleanup
            if (ready) ViteProcessKiller.RecordSpawned(_process.Id, _actualPort);
#endif
            return ready;
```

（`RunPnpmInstall` ローカル関数は削除。`#region Internal` 内は `IsEnvironmentReady` / `SpawnViteDev` / `OnViteStdout` / `WaitForReady` の 4 つになる）

4. `SpawnViteDev` の引数と env を変更:

```csharp
                var psi = new ProcessStartInfo
                {
                    FileName = pnpmPath,
                    // strictPort を付けない: 占有時は Vite が自動で次のポートへインクリメントする
                    // No strictPort: Vite auto-increments to the next port when the base is occupied
                    Arguments = $"exec vite --port {WebUiPortConfig.ViteBasePort} --host 127.0.0.1",
                    WorkingDirectory = webuiRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                var nodeBinDir = Path.GetDirectoryName(nodePath);
                psi.Environment["PATH"] = $"{nodeBinDir}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
                // Kestrel 実ポートを vite.config.ts の proxy target へ注入する
                // Inject the actual Kestrel port into the vite.config.ts proxy target
                psi.Environment["MOORESTECH_BACKEND_PORT"] = kestrelPort.ToString();
```

5. `OnViteStdout` の ready 判定をポートパース成功に変更（「ready in」だけではポート不明のため）:

```csharp
            void OnViteStdout(object sender, DataReceivedEventArgs e)
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Debug.Log($"[Vite] {e.Data}");

                // Local 行から実ポートを確定できた時点で ready とする
                // Ready once the actual port is resolved from the Local line
                if (ViteOutputParser.TryParseLocalPort(e.Data, out var port))
                {
                    _actualPort = port;
                    _readySignal.Set();
                }
            }
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `WebUiHost.cs:61` の `vite.StartAsync()` が引数不足でコンパイルエラー（**期待どおり**。呼び出し側は Task 7 で直すため、このタスクではコミットせず Task 7 とまとめる選択も可…ではなく、**ここではコミットしない**。Task 7 完了後にまとめてコミットする）

**注記:** コンパイルが通らない中間状態を挟むため、Task 6 と Task 7 は連続で実施し、コミットは Task 7 末尾で行う。

---

### Task 7: WebUiHost 配線 + vite.config.ts

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`
- Modify: `moorestech_web/webui/vite.config.ts`

**Interfaces:**
- Consumes: `KestrelServer.ActualPort`（Task 3）、`ViteProcess.StartAsync(int)` / `ActualPort`（Task 6）、`WebUiPortConfig.SetVitePort`（Task 1）
- Produces: `WebUiHost.ViteUrl` (static string get、未起動時 null) — Task 8 の `WebUiCefNavigator` が読む

- [ ] **Step 1: WebUiHost.cs を改修**

1. `using Client.WebUiHost.Common;` を追加
2. フィールドとプロパティを追加:

```csharp
        // 起動完了後に確定する Vite の URL。未起動・停止後は null
        // Vite URL resolved after startup; null while not running
        public static string ViteUrl => _viteUrl;
        private static string _viteUrl;
```

3. `StartAsync` 内の Vite 起動部（61 行目付近）を変更:

```csharp
                // Vite が起動できない（node 欠如・ready 未達）と無 UI になるため、失敗扱いでロールバックする
                // A Vite failure (missing node / not ready) leaves the UI blank, so roll back as a failure
                if (!await vite.StartAsync(kestrel.ActualPort)) return false;

                // 確定した実ポートを公開し、CORS 検査と CEF ナビゲーションを有効化する
                // Publish the resolved port, enabling the CORS check and CEF navigation
                WebUiPortConfig.SetVitePort(vite.ActualPort);
                _viteUrl = $"http://127.0.0.1:{vite.ActualPort}/";
```

4. ready ログ（88 行目）を実 URL に変更:

```csharp
            Debug.Log($"[WebUiHost] ready. Open {_viteUrl}");
```

5. `StopAsync` の冒頭（フィールド退避の直後）に確定値のリセットを追加:

```csharp
            // 実ポート公開を取り下げる（停止中の CORS 全拒否・CEF ナビゲーション抑止）
            // Withdraw the published port (rejects CORS and suppresses CEF navigation while stopped)
            _viteUrl = null;
            WebUiPortConfig.SetVitePort(0);
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0

- [ ] **Step 3: vite.config.ts を改修**

`server` セクションを以下に置換:

```ts
// Unity から注入される実ポート。単体 `pnpm dev` 時はベース値へフォールバックする
// Actual ports injected by Unity; standalone `pnpm dev` falls back to the base values
const vitePort = Number(process.env.MOORESTECH_VITE_PORT ?? 25173);
const backendPort = Number(process.env.MOORESTECH_BACKEND_PORT ?? 25050);
```

（`defineConfig` の前に配置）

```ts
  server: {
    host: "127.0.0.1",
    port: vitePort,
    fs: {
      // Vite は allow リストに node_modules を含むエントリを拒否するため、
      // プロジェクトルート自体を許可してデフォルト挙動(リポジトリ外は元々アクセス不可)に委ねる
      // Vite rejects allow entries containing node_modules, so allow the project root itself
      // and rely on the default behavior (access outside the repo is already blocked)
      allow: ["."],
      strict: true,
    },
    proxy: {
      // Kestrel への HTTP + WebSocket プロキシ
      // Proxy HTTP and WebSocket to Kestrel
      "/api": {
        target: `http://127.0.0.1:${backendPort}`,
        changeOrigin: false,
      },
      "/ws": {
        target: `ws://127.0.0.1:${backendPort}`,
        ws: true,
        changeOrigin: false,
      },
    },
  },
```

（`strictPort: true` の行は削除。ポート衝突時は Vite が自動インクリメントする）

- [ ] **Step 4: Task 6 + 7 をまとめてコミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteProcess.cs moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/PnpmInstaller.cs* moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs moorestech_web/webui/vite.config.ts
git commit -m "feat(webui): Vite/Kestrelの実ポートを動的確定しenv注入・URL公開する配線に変更"
```

---

### Task 8: WebUiCefNavigator + asmdef 参照 + prefab 編集

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Cef/WebUiCefNavigator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`（references に `CefUnity.Runtime` を追加）
- Modify（uloop 経由）: `moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab`

**Interfaces:**
- Consumes: `WebUiHost.ViteUrl`（Task 7）、`CefUnityBrowserSample.LoadUrl(string)`（UPM パッケージ jp.juha.cefunity）

- [ ] **Step 1: asmdef に参照追加**

`Client.WebUiHost.asmdef` の `references` 配列に `"CefUnity.Runtime"` を追加（JSON テキスト編集で可。asmdef は Unity YAML ではない）。

- [ ] **Step 2: WebUiCefNavigator を実装**

```csharp
using CefUnity.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Cef
{
    /// <summary>
    /// WebUiHost が確定した実 Vite URL へ CEF ブラウザを遷移させる。prefab の _url は about:blank 固定
    /// Navigates the CEF browser to the actual Vite URL resolved by WebUiHost; the prefab _url stays about:blank
    /// </summary>
    public class WebUiCefNavigator : MonoBehaviour
    {
        private void Start()
        {
            NavigateWhenReady().Forget();
        }

        private async UniTaskVoid NavigateWhenReady()
        {
            // 同フレームの CefUnityBrowserSample.Start でのブラウザ生成完了を待つため 1 フレーム遅らせる
            // Delay one frame so CefUnityBrowserSample.Start (same frame) finishes creating the browser
            await UniTask.Yield();

            // WebUiHost は MainGame シーンロード前（InitializeScenePipeline 序盤）に起動済み。null はホスト起動失敗
            // WebUiHost boots before the MainGame scene loads (early InitializeScenePipeline); null means host startup failed
            var url = Boot.WebUiHost.ViteUrl;
            if (string.IsNullOrEmpty(url)) return;

            GetComponent<CefUnityBrowserSample>().LoadUrl(url);
        }
    }
}
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0

- [ ] **Step 4: prefab を uloop で編集**

`uloop execute-dynamic-code --project-path ./moorestech_client` で以下を実行:

```csharp
var path = "Assets/Asset/UI/Prefab/MainGameUI.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
var browser = root.GetComponentInChildren<CefUnity.Runtime.CefUnityBrowserSample>(true);
if (browser == null) { UnityEditor.PrefabUtility.UnloadPrefabContents(root); return "CefUnityBrowserSample not found"; }

// _url を about:blank へ変更（実 URL は WebUiCefNavigator が実行時に注入する）
var so = new UnityEditor.SerializedObject(browser);
so.FindProperty("_url").stringValue = "about:blank";
so.ApplyModifiedPropertiesWithoutUndo();

// 同 GameObject に WebUiCefNavigator を追加（冪等）
if (browser.GetComponent<Client.WebUiHost.Cef.WebUiCefNavigator>() == null)
    browser.gameObject.AddComponent<Client.WebUiHost.Cef.WebUiCefNavigator>();

UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
return "done";
```

- [ ] **Step 5: prefab の変更を確認**

Run: `grep -n "_url\|WebUiCefNavigator" moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab | head` （読み取りのみ）
Expected: `_url: about:blank` と WebUiCefNavigator の MonoBehaviour 参照（スクリプト GUID 行）が存在

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Cef* moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef "moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab"
git commit -m "feat(webui): CEFを実行時確定のVite URLへ遷移させるWebUiCefNavigatorを追加"
```

---

### Task 9: 統合検証（ポート衝突・孤児掃除・実プレイ）

**Files:** なし（検証のみ。問題発見時は該当タスクのファイルを修正）

- [ ] **Step 1: 全テスト + コンパイルの最終確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ViteOutputParserTest|KestrelServerPortScanTest|WebUiAllowedOriginTest"`
Expected: 全 PASS

- [ ] **Step 2: ポート占有下での PlayMode 起動検証**

1. ベースポート 2 つをダミーで占有（scratchpad で実行・cwd は webuiRoot 以外）:

```bash
python3 -c "
import socket, time
a = socket.socket(); a.bind(('127.0.0.1', 25173)); a.listen()
b = socket.socket(); b.bind(('127.0.0.1', 25050)); b.listen()
time.sleep(600)
" &
echo $! > /tmp/dummy_port_holder.pid
```

2. PlayMode 起動（`uloop-control-play-mode` スキル参照）し、ログを確認:

Run: `uloop get-logs --project-path ./moorestech_client --search-text "WebUiHost"`
Expected:
- `Kestrel started at http://127.0.0.1:25051`（25050 を回避）
- `[Vite] ... Local: http://127.0.0.1:25174/`（25173 を回避）
- `ready. Open http://127.0.0.1:25174/`
- `forbidden origin` が **出ていない**（WS 接続成功 = CORS 動的化が機能）
- ダミープロセスが kill されて **いない**（cwd 照合が守られている）: `kill -0 $(cat /tmp/dummy_port_holder.pid)` が成功すること

3. `uloop-screenshot` で Game View をキャプチャし、CEF の Web UI が表示されていること（about:blank のままでないこと）を目視確認

4. PlayMode 停止 → 再度 `lsof -ti :25174` で Vite が掃除されたことを確認

5. ダミーを解放: `kill $(cat /tmp/dummy_port_holder.pid)`

- [ ] **Step 3: 通常起動（占有なし）の回帰検証**

PlayMode 起動 → ログで `Kestrel started at http://127.0.0.1:25050` / `Local: http://127.0.0.1:25173/` （ベース値がそのまま使われる）と CEF 表示を確認。

- [ ] **Step 4: 孤児掃除の検証**

1. webuiRoot を cwd にした擬似孤児を 25173 で起動:

```bash
cd moorestech_web/webui && python3 -m http.server 25173 --bind 127.0.0.1 &
echo $! > /tmp/fake_orphan.pid
```

2. PlayMode 起動 → ログに `killing orphaned vite (pid=..., port=25173)` が出て、`kill -0 $(cat /tmp/fake_orphan.pid)` が失敗（掃除済み）することを確認
3. Vite がベース 25173 で起動できていることを確認

- [ ] **Step 5: 検証で見つかった問題の修正をコミットし、最終状態を全コミット**

```bash
git status  # 未コミットの残骸が無いことを確認
```

---

## Self-Review 結果

- **Spec coverage:** ベースポート変更=Task 1/6/7、Kestrel 探索=Task 3、Vite 自動インクリメント+パース=Task 2/6、env 注入=Task 6/7、CORS 動的化=Task 4、掃除の SessionState/cwd 照合化=Task 5、CEF 動的 URL=Task 8、検証=Task 9。スペックの「実装時に実出力で確認」項目（Vite の Local 行形式・Kestrel の bind 例外型）は Task 9 Step 2 / Task 3 Step 4 に明記。
- **Spec からの逸脱:** なし（`MOORESTECH_VITE_PORT` フォールバックも spec どおり維持）。
- **Type consistency:** `ViteProcess.StartAsync(int kestrelPort)`（Task 6 定義 → Task 7 で使用）、`ActualPort`（Task 3/6 定義 → Task 7 で使用）、`WebUiHost.ViteUrl`（Task 7 定義 → Task 8 で使用）、`IsAllowedOrigin` public 化（Task 4 定義 → テストで使用）を突合済み。
- **中間非コンパイル状態:** Task 6→7 のみ（シグネチャ変更のため不可避）。コミットを Task 7 末尾に集約して対処。
