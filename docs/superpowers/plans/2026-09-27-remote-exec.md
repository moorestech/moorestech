# 遠隔実行（Remote Exec） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 起動オプション `-remote-exec` を付けたゲーム（配布ビルド含む）へ、開発者が SSH 越しに C# ソースを送り、ゲーム内でコンパイル・実行して結果を受け取れるようにする。

**Architecture:** クライアントに新アセンブリ `Client.RemoteExec` を置き、Roslyn でソースをコンパイル→`Assembly.Load(bytes)`→実行先（クライアント=Unityメインスレッド／サーバー=内蔵サーバー更新スレッドのtick末尾）で実行する。入口は既存 Web UI サーバー（Kestrel, 127.0.0.1）の `POST /api/remote-exec` 1本で、起動ごとのトークンを要求する。実行記録（台帳）をディスクに残し、プレイ報告・異常終了の箱の manifest に印と台帳を載せ、取り込み側は印付きを自動修正・日次集計から外す。

**Tech Stack:** Unity 6 (Mono, API Compatibility .NET Framework), Roslyn (Microsoft.CodeAnalysis.CSharp 4.x), HarmonyX/Lib.Harmony, NuGetForUnity, ASP.NET Core Kestrel 2.3（既存）, UniTask, Newtonsoft.Json, bash + python3（取り込み側）。

## Requirements

設計の正本は `docs/adr/0072-remote-exec-in-distributed-builds.md`。

- R1: 配布ビルドを含む全ビルドに入る。起動引数に `-remote-exec` が無い起動では、入口を登録せず・トークンを作らず・Roslyn/Harmony を読み込まない。受入: 引数なしで起動したビルドで `POST /api/remote-exec` が 404、`RemoteExec` ディレクトリにトークンファイルが作られない。
- R2: C# はゲーム内の Roslyn でコンパイルする。送る側はソース文字列だけを送る。受入: `return 1 + 1;` を送ると `"2"` が返る。
- R3: 送るコードの形は uloop execute-dynamic-code に揃える（メソッド本体。先頭の `using` 行は取り出して使う。`await` 可。`return` が無くてもよい）。受入: `await UniTask.Delay(10); return "ok";` が `"ok"` を返す／`Debug.Log("x");` だけのコードが成功し戻り値 null。
- R4: 実行先を要求ごとに `client` / `server` で選ぶ。client は Unity メインスレッド、server は内蔵サーバー更新スレッドの tick 末尾。サーバー未起動で server を指定すると理由付き失敗。受入: server 指定で `return System.Threading.Thread.CurrentThread.ManagedThreadId;` がメインスレッドと異なる値を返す（PlayMode テスト）。
- R5: 応答は成否・戻り値（文字列化）・コンパイルエラー一覧・実行時例外・実行中に出た Unity ログを JSON で返す。受入: 構文エラーのコードで `ok:false` と `compileErrors` に行番号付きの診断が入る。
- R6: 入口は既存 Web UI サーバーの `POST /api/remote-exec`。ヘッダ `X-Remote-Exec-Token` が起動ごとのトークンと一致しない要求、`Origin` ヘッダ付きの要求（ブラウザ由来）は 403 で拒否し、拒否理由を `Debug.LogWarning` に出す。受入: トークン誤り・Origin付きで 403 とログ。
- R7: トークンとポートは `GameSystemPaths.GameSystemDirectory/RemoteExec/access.json` に書く（起動ごとに上書き）。受入: 有効起動後にファイルがあり `port` と `token` を持つ。
- R8: Harmony を同梱し、送ったコードから `HarmonyLib` を使える。受入: 送ったコードで既存メソッドに Postfix を当て、呼び出しで効果が観測できる（EditMode テスト）。
- R9: 実行のたびに台帳 `RemoteExec/ledger-<pid>.jsonl` に1行（時刻・実行先・ソース・成否）追記する。受入: 2回実行で2行。
- R10: 有効だったセッションのプレイ報告（bug/feedback）と異常終了（crash）の manifest に `remoteExec`（`enabled:true` と台帳ファイル名）を載せ、台帳を箱の `remote-exec/` に入れる。manifest の `SchemaVersion` を 4 に上げる。受入: 有効セッションの箱の manifest に `remoteExec.enabled == true`、無効セッションでは `remoteExec == null`。
- R11: 取り込み側は `remoteExec.enabled == true` の箱を自動修正ラン投入の対象外にし（`--force` で投入可）、日次ダイジェストのテスター報告件数から除外して別枠件数で出す。受入: scripts/playtest/tests の追加テストが通る。
- R12: 送る側 CLI `scripts/playtest/remote-exec.sh`。ソースファイルまたは stdin、`--target client|server`、`--windows`（`MOORESTECH_VERIFY_*` で SSH）を受け、結果 JSON を出す。受入: ローカル起動のゲームと検証機の両方で動く（Task 8）。
- R13: 完了条件（ADR 0072）: 検証機の配布ビルド相当で (1) `-remote-exec` 付き起動でコードを送り結果が返る (2) Harmony で差し込める (3) オプションなしでは入口が開かない。
- やらないこと: タイトル画面での利用（Web UI サーバーはゲーム開始の初期化で起動する）／実行の打ち切り・タイムアウト（固まったら再起動）／別プロセス・別マシンのサーバー／テスターへの案内／クラッシュ再現検証そのもの（moorestech-rhvub で本機能を使って別途行う）。

## Global Constraints

- AGENTS.md 全規約（1ファイル200行以下、partial禁止、`Func<>`禁止、try-catchは外部境界のみ・根拠コメント必須・握り潰し禁止、2行セットコメント、`#region Internal`はメソッド内ローカル関数のみ、デフォルト引数禁止、単純getter/setterプロパティ禁止、fail-closedはログ必須）。
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`。
- `.meta` は手で作らない。Plugin の platform 設定変更は `uloop execute-dynamic-code` で `PluginImporter` を使う（手でmetaを書かない）。
- 新ディレクトリは10ファイル以下。
- manifest（BugReportManifest）は ADR 0057 の契約。SchemaVersion を上げ、コメントに変更点を追記する。
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoteExec"`。PlayMode は明示指定が要る（uloop v3 の既定は EditMode）。
- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/remote-exec`（ブランチ `feat/remote-exec`）。Editor はこの worktree で `uloop launch` する。

## File Structure

| ファイル | 責務 | 既存部品の扱い |
|---|---|---|
| `moorestech_client/Assets/packages.config` | Roslyn・Harmony を NuGetForUnity で追加 | 前例: Kestrel 一式（同ファイル）に従う |
| `moorestech_client/Packages/manifest.json` | UPM の `org.nuget.microsoft.codeanalysis.csharp` を外す（Editor限定のため製品に入らない。uloop v3 は外部コンパイラを使う） | Task 1 で uloop の動作を確認してから外す |
| `moorestech_client/Assets/Scripts/Client.RemoteExec/Client.RemoteExec.asmdef` | 新アセンブリ | 新規（遠隔実行はどの既存アセンブリの責務でもない） |
| `Client.RemoteExec/RemoteExecLaunchOption.cs` | 起動引数 `-remote-exec` の判定を1度だけ行い保持 | 前例: `Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeSettings.HasMarker` の形 |
| `Client.RemoteExec/Compile/RemoteExecSourceWrapper.cs` | 本体コードを実行用クラスへ包む（using 取り出し） | 新規 |
| `Client.RemoteExec/Compile/RemoteExecCompiler.cs` | Roslyn でコンパイルし Assembly を返す／診断を返す | 新規 |
| `Client.RemoteExec/Compile/RemoteExecReferenceSet.cs` | 参照アセンブリ一覧（読み込み済み＋Managed/ の全DLL、名前で重複排除） | 新規 |
| `Client.RemoteExec/Run/RemoteExecRunner.cs` | 実行先への振り分け・await・結果組み立て | 新規 |
| `Client.RemoteExec/Run/RemoteExecLogCapture.cs` | 実行中の Unity ログ収集 | 新規 |
| `Client.RemoteExec/Run/RemoteExecResult.cs` | 応答 JSON の形 | 新規 |
| `Client.RemoteExec/Access/RemoteExecAccessFile.cs` | トークン生成と access.json 書き出し | 新規 |
| `Client.RemoteExec/Access/RemoteExecLedger.cs` | 台帳の追記と現在セッションの台帳パス | 新規 |
| `Client.RemoteExec/RemoteExecEndpoint.cs` | HTTP 要求の検証→実行→応答 | 前例: `Client.WebUiHost/Game/ItemMasterEndpoint` 等の `Path`+`HandleAsync` 形 |
| `moorestech_server/Assets/Scripts/Server.Boot/Loop/ServerThreadActionQueue.cs` | 任意の処理を内蔵サーバー更新スレッドの tick 末尾で実行する汎用キュー | 前例: 同ディレクトリ `PacketProcessing/TickEndPacketQueue.cs`（ConcurrentQueue をtick末尾で排出） |
| `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` | `GameUpdater.TickEndUpdates.Add(ServerThreadActionQueue.Drain)` を登録 | 既存登録（同ファイル 344行付近）に並べる |
| `Client.WebUiHost/Boot/WebUiEndpoints.cs` | 有効時のみ `/api/remote-exec` を分岐に足す | 既存 if 連鎖に1分岐 |
| `Client.WebUiHost/Boot/WebUiHost.cs` | `KestrelPort` を公開（access.json 用） | 1行追加 |
| `Client.Starter/InitializeScenePipeline.cs` | WebUiHost 起動後に有効化（トークン作成・access.json） | 既存の起動列に1呼び出し |
| `Client.Game/InGame/BugReport/BugReportManifest.cs` | `RemoteExec` 欄と SchemaVersion 4 | 既存契約の拡張 |
| `Client.Game/InGame/BugReport/RemoteExecBundleMark.cs` | manifest への印付けと台帳コピー（bug と crash で共用） | 新規（両 writer から呼ぶ。写しを作らない） |
| `Client.Game/InGame/BugReport/LastSession/Marks/SessionOriginSnapshot.cs` | `remoteExecEnabled` を開始時の出所に追加 | 既存（前例: steamId 等と同じく落ちたセッション自身が書く） |
| `scripts/playtest/enqueue-autofix.sh` / `digest_collect.py` | 印付きの除外 | 既存の kind ガードの隣 |
| `scripts/playtest/remote-exec.sh` | 送る側 CLI | 前例: `verify-on-windows.sh` の SSH 接続部（関数を lib へ切り出して共用） |

依存の向き: `Client.WebUiHost` → `Client.RemoteExec`、`Client.Starter` → `Client.RemoteExec`、`Client.Game` → `Client.RemoteExec`（台帳パスと有効フラグの読み取りのみ）、`Client.RemoteExec` → `Server.Boot`（`ServerThreadActionQueue`）・`Game.Paths`・UniTask・Newtonsoft。`Client.RemoteExec` はゲームの型をコンパイル時に参照しない（送られたコードが実行時に参照する）。

---

### Task 1: Roslyn と Harmony を製品ビルドに入れる（可否ゲート）

**Files:**
- Modify: `moorestech_client/Assets/packages.config`
- Modify: `moorestech_client/Packages/manifest.json`（`org.nuget.microsoft.codeanalysis.csharp` 行を削除）
- Create: `moorestech_client/Assets/Scripts/Client.RemoteExec/Client.RemoteExec.asmdef`
- Create: `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecPackagingProbe.cs`（このタスク内の確認用。Task 2 で削除）

**Interfaces:**
- Produces: アセンブリ `Client.RemoteExec`（references: `UniTask`, `Newtonsoft.Json` は自動参照, `Game.Paths`, `Server.Boot`, `Core.Update`）。`Microsoft.CodeAnalysis`・`Microsoft.CodeAnalysis.CSharp`・`0Harmony` がエディタと StandaloneWindows64/OSX の両方で参照可能な状態。

- [ ] **Step 1: uloop が UPM の Roslyn に依存していないことを確かめる**

Run: `grep -rn "Microsoft.CodeAnalysis" moorestech_client/Library/PackageCache/io.github.hatayama.uloopmcp*/Editor --include='*.asmdef'`
Expected: 出力なし（v3 は外部コンパイラ。`ExternalCompilerPathResolver.cs` 参照）。出力があればこのタスクを止めて報告する。

- [ ] **Step 2: manifest.json から `"org.nuget.microsoft.codeanalysis.csharp": "4.14.0",` の行を削除し、packages.config に追加する**

```xml
  <package id="Lib.Harmony" version="2.3.6" manuallyInstalled="true" />
  <package id="Microsoft.CodeAnalysis.CSharp" version="4.14.0" manuallyInstalled="true" />
```
（アルファベット順の位置に入れる。依存の `Microsoft.CodeAnalysis.Common`・`System.Reflection.Metadata`・`System.Collections.Immutable` の版上げは NuGetForUnity の復元に任せ、復元後の packages.config をそのままコミットする）

- [ ] **Step 3: Editor を起動して復元させ、コンパイルを確かめる**

Run: `uloop launch /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/remote-exec/moorestech_client` → `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0。`Assets/Packages/Microsoft.CodeAnalysis.CSharp.4.14.0/` と `Lib.Harmony.2.3.6/` が生える。重複アセンブリ（Multiple precompiled assemblies with the same name）のエラーが出たら、`Library/PackageCache` 側の残骸が消えたか `uloop get-logs --log-type Error` で確認する。

- [ ] **Step 4: uloop の動的コード実行がまだ動くことを確かめる**

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code "return 1 + 1;"`
Expected: `2`。

- [ ] **Step 5: asmdef と確認用コードを書く**

`Client.RemoteExec.asmdef`:
```json
{
    "name": "Client.RemoteExec",
    "rootNamespace": "Client.RemoteExec",
    "references": ["UniTask", "Game.Paths", "Server.Boot", "Core.Update"],
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

`RemoteExecPackagingProbe.cs`（プレイヤーで Roslyn が emit でき Harmony が読めるかだけを見る。Task 2 で消す）:
```csharp
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEngine;

namespace Client.RemoteExec
{
    public static class RemoteExecPackagingProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Probe()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-remote-exec-probe")) return;

            // 読み込み済みアセンブリだけを参照にして最小コードを emit する
            // Emit a minimal snippet referencing only loaded assemblies
            var refs = System.AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)).Select(a => MetadataReference.CreateFromFile(a.Location));
            var tree = CSharpSyntaxTree.ParseText("public static class P { public static int Run() => 40 + 2; }");
            var compilation = CSharpCompilation.Create("probe", new[] { tree }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var emit = compilation.Emit(stream);
            var harmonyId = new Harmony("probe").Id;
            Debug.Log($"[RemoteExecProbe] emit={emit.Success} diagnostics={string.Join(" | ", emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))} harmony={harmonyId}");
        }
    }
}
```

- [ ] **Step 6: macOS のプレイヤーを Release で焼いて起動し、可否を判定する**

Run（Editor を閉じてから）: `Client.Editor.Build.BuildMenu` と同じ `BuildPipeline.Execute` を `-executeMethod` で呼ぶ入口が無ければ `uloop execute-dynamic-code` で `Client.Editor.Build.BuildPipeline.Execute(new PlayerBuildRequest{ Target = BuildTarget.StandaloneOSX, OutputDirectory = "/tmp/remote-exec-probe", IsDevelopmentBuild = false, IsStrictBundling = false, BundleLocalGameData = false })` を実行する。出来たアプリを `open -a <app> --args -remote-exec-probe` で起動し、`~/Library/Logs/<company>/<product>/Player.log` を `grep RemoteExecProbe`。
Expected: `[RemoteExecProbe] emit=True diagnostics= harmony=probe`。

**可否ゲート:** `emit=False`、または型読み込み例外（`TypeLoadException`・`FileNotFoundException: System.Collections.Immutable` 等）で行が出ない場合は、以降のタスクへ進まずユーザーへ報告する（ADR 0072 の「ゲーム内でコンパイル」を実現できないため裁定のやり直しが要る）。報告には Player.log の該当部分を添える。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/packages.config moorestech_client/Packages/manifest.json moorestech_client/Packages/packages-lock.json moorestech_client/Assets/Packages moorestech_client/Assets/Scripts/Client.RemoteExec
git commit -m "build(remote-exec): Roslyn と Harmony を NuGetForUnity で製品ビルドへ入れる"
```

---

### Task 2: 起動オプションとゲーム内コンパイル

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecPackagingProbe.cs`
- Create: `Client.RemoteExec/RemoteExecLaunchOption.cs`
- Create: `Client.RemoteExec/Compile/RemoteExecSourceWrapper.cs`
- Create: `Client.RemoteExec/Compile/RemoteExecReferenceSet.cs`
- Create: `Client.RemoteExec/Compile/RemoteExecCompiler.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/RemoteExec/RemoteExecCompilerTest.cs`（`Client.Tests.asmdef` の references に `Client.RemoteExec` を追加）

**Interfaces:**
- Produces:
  - `public static class RemoteExecLaunchOption { public const string Marker = "-remote-exec"; public static bool IsEnabled { get; private set; } public static void ResolveFromCommandLine(string[] args); }`
  - `public static class RemoteExecSourceWrapper { public const string EntryTypeName = "RemoteExecSnippet"; public const string EntryMethodName = "Run"; public static string Wrap(string body); }`
  - `public sealed class RemoteExecCompileOutcome { public Assembly Assembly { get; } public IReadOnlyList<string> Errors { get; } public bool Succeeded => Assembly != null; }`
  - `public static class RemoteExecCompiler { public static RemoteExecCompileOutcome Compile(string body); }`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Linq;
using Client.RemoteExec.Compile;
using NUnit.Framework;

namespace Client.Tests.RemoteExec
{
    public class RemoteExecCompilerTest
    {
        [Test]
        public void 本体コードを包んでコンパイルし実行用の型が得られる()
        {
            var outcome = RemoteExecCompiler.Compile("return 1 + 1;");
            Assert.IsTrue(outcome.Succeeded, string.Join("\n", outcome.Errors));
            Assert.IsNotNull(outcome.Assembly.GetType(RemoteExecSourceWrapper.EntryTypeName));
        }

        [Test]
        public void 構文エラーは行番号付きの診断で返る()
        {
            var outcome = RemoteExecCompiler.Compile("return 1 +;");
            Assert.IsFalse(outcome.Succeeded);
            Assert.That(outcome.Errors.Single(), Does.Contain("(1,"));
        }

        [Test]
        public void 先頭のusing行は名前空間の指定として使われる()
        {
            var outcome = RemoteExecCompiler.Compile("using System.Text;\nreturn new StringBuilder(\"a\").ToString();");
            Assert.IsTrue(outcome.Succeeded, string.Join("\n", outcome.Errors));
        }

        [Test]
        public void return無しの本体もコンパイルできる()
        {
            Assert.IsTrue(RemoteExecCompiler.Compile("UnityEngine.Debug.Log(\"x\");").Succeeded);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoteExecCompilerTest"`
Expected: コンパイルエラー（型が無い）。

- [ ] **Step 3: 実装を書く**

`RemoteExecLaunchOption.cs`:
```csharp
using System;
using UnityEngine;

namespace Client.RemoteExec
{
    // 遠隔実行を開けてよい起動かを起動引数で1度だけ決める（ADR 0072）
    // Decides once, from the launch arguments, whether this boot may open remote exec (ADR 0072)
    public static class RemoteExecLaunchOption
    {
        public const string Marker = "-remote-exec";

        public static bool IsEnabled { get; private set; }

        public static void ResolveFromCommandLine(string[] args)
        {
            IsEnabled = Array.IndexOf(args, Marker) >= 0;
            if (IsEnabled) Debug.LogWarning($"[RemoteExec] {Marker} が指定されたため遠隔実行を有効にします");
        }
    }
}
```

`Compile/RemoteExecSourceWrapper.cs`:
```csharp
using System.Linq;
using System.Text;

namespace Client.RemoteExec.Compile
{
    // uloop execute-dynamic-code と同じ書き方（メソッド本体・先頭using・await可）を実行用クラスへ包む
    // Wraps a method body written like uloop execute-dynamic-code (leading usings, await allowed) into a runnable class
    public static class RemoteExecSourceWrapper
    {
        public const string EntryTypeName = "RemoteExecSnippet";
        public const string EntryMethodName = "Run";

        private static readonly string[] DefaultUsings = { "System", "System.Linq", "System.Collections.Generic", "UnityEngine", "Cysharp.Threading.Tasks" };

        public static string Wrap(string body)
        {
            // 先頭の using 行だけを取り出し、残りを本体とする。行番号を保つため本体の行数は変えない
            // Lift only the leading using lines; the body keeps its line count so diagnostics stay aligned
            var lines = body.Replace("\r\n", "\n").Split('\n');
            var usingCount = lines.TakeWhile(l => l.TrimStart().StartsWith("using ") || l.Trim().Length == 0).Count();
            var source = new StringBuilder();
            foreach (var u in DefaultUsings) source.Append($"using {u}; ");
            foreach (var l in lines.Take(usingCount)) source.Append(l.Trim()).Append(' ');
            source.Append($"public static class {EntryTypeName} {{ public static async UniTask<object> {EntryMethodName}() {{\n");

            // using 行は空行として残し、本体の行番号＝送った行番号にする
            // Using lines stay as blank lines so body line numbers equal the sent line numbers
            for (var i = 0; i < lines.Length; i++) source.Append(i < usingCount ? "" : lines[i]).Append('\n');

            // return の無い本体でも全経路が値を返すよう末尾に null を足す（到達不能警告は無害）
            // Append a trailing null return so bodies without return still compile (the unreachable warning is harmless)
            source.Append("#pragma warning disable CS0162, CS1998\nreturn null; } }");
            return source.ToString();
        }
    }
}
```
（注: `#pragma` は `return null;` の前に置くと、その行以降の警告だけを抑える。行番号の整合は Step 1 の診断テストで確認する。ずれた場合は `#line 1` を本体の直前に入れて合わせる）

`Compile/RemoteExecReferenceSet.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Client.RemoteExec.Compile
{
    // 送られたコードが参照できるアセンブリ一覧。読み込み済み全部＋mscorlib と同じ場所の DLL（netstandard 等の未読込ファサード）
    // Assemblies a sent snippet may reference: everything loaded, plus DLLs beside mscorlib (unloaded facades such as netstandard)
    public static class RemoteExecReferenceSet
    {
        public static IReadOnlyList<MetadataReference> Collect()
        {
            // 同名は最初の1つだけ採る。Editor では同名の別版が並ぶことがあり、両方渡すと型が曖昧になる
            // Keep only the first of each name; the Editor may hold several versions and passing both makes types ambiguous
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location)) continue;
                byName.TryAdd(assembly.GetName().Name, assembly.Location);
            }

            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            foreach (var path in Directory.GetFiles(runtimeDirectory, "*.dll"))
            {
                byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);
            }
            foreach (var facade in FacadePaths(runtimeDirectory)) byName.TryAdd(Path.GetFileNameWithoutExtension(facade), facade);

            return byName.Values.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();

            #region Internal

            // Mono の Facades 配下（netstandard.dll 等）。無い環境では空
            // Mono's Facades directory (netstandard.dll etc.); empty where absent
            IEnumerable<string> FacadePaths(string directory)
            {
                var facades = Path.Combine(directory, "Facades");
                return Directory.Exists(facades) ? Directory.GetFiles(facades, "*.dll") : Array.Empty<string>();
            }

            #endregion
        }
    }
}
```

`Compile/RemoteExecCompiler.cs`:
```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Client.RemoteExec.Compile
{
    public sealed class RemoteExecCompileOutcome
    {
        public Assembly Assembly { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Succeeded => Assembly != null;

        public RemoteExecCompileOutcome(Assembly assembly, IReadOnlyList<string> errors)
        {
            Assembly = assembly;
            Errors = errors;
        }
    }

    // 本体コードを包んでコンパイルし、読み込んだアセンブリを返す。読み込んだものは解放できない（ADR 0072 Consequences）
    // Wraps and compiles a body, returning the loaded assembly; loaded assemblies are never unloaded (ADR 0072 Consequences)
    public static class RemoteExecCompiler
    {
        private static int _sequence;

        public static RemoteExecCompileOutcome Compile(string body)
        {
            var tree = CSharpSyntaxTree.ParseText(RemoteExecSourceWrapper.Wrap(body));
            var name = $"RemoteExecSnippet_{Interlocked.Increment(ref _sequence)}";
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);
            var compilation = CSharpCompilation.Create(name, new[] { tree }, RemoteExecReferenceSet.Collect(), options);

            using var stream = new MemoryStream();
            var emit = compilation.Emit(stream);
            if (!emit.Success)
            {
                // 行番号は包みの1行目を差し引き、送った本体の行に合わせて返す
                // Line numbers subtract the wrapper's first line so they match the sent body
                var errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(Describe).ToList();
                return new RemoteExecCompileOutcome(null, errors);
            }
            return new RemoteExecCompileOutcome(Assembly.Load(stream.ToArray()), new List<string>());

            #region Internal

            string Describe(Diagnostic d)
            {
                var span = d.Location.GetLineSpan().StartLinePosition;
                return $"({span.Line},{span.Character + 1}) {d.Id}: {d.GetMessage()}";
            }

            #endregion
        }
    }
}
```
（`span.Line` は0始まり。包みの1行目（using と宣言）が0行目なので、本体1行目は `Line == 1` になり「(1,」で出る）

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoteExecCompilerTest"`
Expected: 4 件 PASS。

- [ ] **Step 5: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.RemoteExec moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(remote-exec): 起動オプションとゲーム内コンパイル"
```

---

### Task 3: 実行先への振り分けと結果の組み立て

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Loop/ServerThreadActionQueue.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（344行付近の `TickEndUpdates.Add` 群に1行）
- Create: `Client.RemoteExec/Run/RemoteExecResult.cs`
- Create: `Client.RemoteExec/Run/RemoteExecLogCapture.cs`
- Create: `Client.RemoteExec/Run/RemoteExecRunner.cs`
- Test: `Client.Tests/RemoteExec/RemoteExecRunnerTest.cs`（EditMode: client 実行）、`Client.Tests/EditModeInPlayingTest/RemoteExec/RemoteExecServerTargetTest.cs`（PlayMode: server 実行。同ディレクトリの既存テストの起動ヘルパーを使う）

**Interfaces:**
- Consumes: `RemoteExecCompiler.Compile(string)`, `RemoteExecSourceWrapper.EntryTypeName/EntryMethodName`
- Produces:
  - `public static class ServerThreadActionQueue { public static bool IsDraining { get; private set; } public static void Enqueue(Action action); public static void Drain(); }`
  - `public enum RemoteExecTarget { Client, Server }`
  - `public sealed class RemoteExecResult { public bool Ok; public string Result; public List<string> CompileErrors = new(); public string Exception; public List<string> Logs = new(); }`
  - `public static class RemoteExecRunner { public static UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target); }`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using System.Collections;

namespace Client.Tests.RemoteExec
{
    public class RemoteExecRunnerTest
    {
        [UnityTest]
        public IEnumerator クライアントで実行し戻り値とログが返る() => UniTask.ToCoroutine(async () =>
        {
            var result = await RemoteExecRunner.RunAsync("UnityEngine.Debug.Log(\"hello\"); await UniTask.Yield(); return 2;", RemoteExecTarget.Client);
            Assert.IsTrue(result.Ok, result.Exception);
            Assert.AreEqual("2", result.Result);
            Assert.That(result.Logs, Has.Some.Contains("hello"));
        });

        [UnityTest]
        public IEnumerator 実行時例外は例外文として返る() => UniTask.ToCoroutine(async () =>
        {
            var result = await RemoteExecRunner.RunAsync("throw new System.InvalidOperationException(\"boom\");", RemoteExecTarget.Client);
            Assert.IsFalse(result.Ok);
            Assert.That(result.Exception, Does.Contain("boom"));
        });

        [UnityTest]
        public IEnumerator コンパイルエラーは実行せず返る() => UniTask.ToCoroutine(async () =>
        {
            var result = await RemoteExecRunner.RunAsync("return 1 +;", RemoteExecTarget.Client);
            Assert.IsFalse(result.Ok);
            Assert.IsNotEmpty(result.CompileErrors);
        });

        [UnityTest]
        public IEnumerator サーバー未起動でサーバー指定は理由付きで失敗する() => UniTask.ToCoroutine(async () =>
        {
            var result = await RemoteExecRunner.RunAsync("return 1;", RemoteExecTarget.Server);
            Assert.IsFalse(result.Ok);
            Assert.That(result.Exception, Does.Contain("サーバー"));
        });
    }
}
```

PlayMode 側（`RemoteExecServerTargetTest`）: 同ディレクトリの既存テスト（`EditModeInPlayingTest/Boot/LocalPlayEmbeddedServerBootTest.cs`）のゲーム起動手順を呼んで内蔵サーバーを起動した後、
```csharp
var main = System.Threading.Thread.CurrentThread.ManagedThreadId;
var result = await RemoteExecRunner.RunAsync("return System.Threading.Thread.CurrentThread.ManagedThreadId;", RemoteExecTarget.Server);
Assert.IsTrue(result.Ok, result.Exception);
Assert.AreNotEqual(main.ToString(), result.Result);
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoteExecRunnerTest"`
Expected: コンパイルエラー。

- [ ] **Step 3: 実装を書く**

`ServerThreadActionQueue.cs`:
```csharp
using System;
using System.Collections.Concurrent;

namespace Server.Boot.Loop
{
    // 他スレッドから預かった処理を、サーバー更新スレッドの tick 末尾でまとめて実行する（前例: TickEndPacketQueue）
    // Runs work handed over from other threads at the server update thread's tick end (precedent: TickEndPacketQueue)
    public static class ServerThreadActionQueue
    {
        private static readonly ConcurrentQueue<Action> Queue = new();

        // tick 末尾の排出が1度でも走ったか。サーバー未起動の判定に使う
        // Whether a tick-end drain has run at least once; used to tell that no server is running
        public static bool IsDraining { get; private set; }

        public static void Enqueue(Action action)
        {
            Queue.Enqueue(action);
        }

        public static void Drain()
        {
            IsDraining = true;

            // 排出開始時点の件数だけ処理し、実行中に積まれた分は次 tick へ回す
            // Process only what was queued at drain start; items added meanwhile wait for the next tick
            var count = Queue.Count;
            for (var i = 0; i < count && Queue.TryDequeue(out var action); i++) action();
        }
    }
}
```
DI 生成（`MoorestechServerDIContainerGenerator.cs`、`WorldMutationTickEndUpdater` の登録直後）:
```csharp
            // 外部スレッドから預かった処理（遠隔実行等）を tick 末尾で実行する
            // Run work handed over from other threads (remote exec etc.) at tick end
            GameUpdater.TickEndUpdates.Add(ServerThreadActionQueue.Drain);
```
（`Drain` 内の `action()` は例外を投げうる。呼び出し元の `ServerGameUpdater` が `catch (Exception) → LogException` しているため tick は継続する。ただし遠隔実行側は例外を自分の結果へ詰めるため、`Enqueue` に渡す処理は例外を外へ出さない形にする（下の Runner 参照））

`Run/RemoteExecResult.cs`:
```csharp
using System.Collections.Generic;

namespace Client.RemoteExec.Run
{
    public enum RemoteExecTarget
    {
        Client,
        Server,
    }

    // 送る側へ返す応答の形。値は JSON 化してそのまま返す
    // The response shape returned to the sender, serialized to JSON as is
    public sealed class RemoteExecResult
    {
        public bool Ok;
        public string Result;
        public List<string> CompileErrors = new();
        public string Exception;
        public List<string> Logs = new();
    }
}
```

`Run/RemoteExecLogCapture.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.RemoteExec.Run
{
    // 実行の間に出た Unity ログを集める。他スレッドのログも混ざる（同時に出たものを区別できないため）
    // Collects Unity logs emitted during a run; logs from other threads mix in since simultaneous ones cannot be told apart
    public sealed class RemoteExecLogCapture : IDisposable
    {
        private readonly List<string> _lines = new();

        public RemoteExecLogCapture()
        {
            Application.logMessageReceivedThreaded += OnLog;
        }

        public List<string> TakeLines()
        {
            lock (_lines) return new List<string>(_lines);
        }

        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            lock (_lines) _lines.Add($"[{type}] {condition}");
        }
    }
}
```

`Run/RemoteExecRunner.cs`:
```csharp
using System;
using System.Reflection;
using Client.RemoteExec.Compile;
using Cysharp.Threading.Tasks;
using Server.Boot.Loop;

namespace Client.RemoteExec.Run
{
    // コンパイル→実行先で実行→結果組み立て。固まったコードは止められない（ADR 0072: 復旧は再起動）
    // Compile, run on the chosen target, build the result; a hung snippet cannot be stopped (ADR 0072: restart to recover)
    public static class RemoteExecRunner
    {
        public static async UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target)
        {
            var result = new RemoteExecResult();
            await UniTask.SwitchToMainThread();

            var outcome = RemoteExecCompiler.Compile(body);
            if (!outcome.Succeeded)
            {
                result.CompileErrors.AddRange(outcome.Errors);
                return result;
            }

            var entry = outcome.Assembly.GetType(RemoteExecSourceWrapper.EntryTypeName).GetMethod(RemoteExecSourceWrapper.EntryMethodName, BindingFlags.Public | BindingFlags.Static);
            using var capture = new RemoteExecLogCapture();

            // 送られたコードは外部入力。何を投げても応答へ詰めて返し、ゲームを落とさない
            // Sent code is external input: whatever it throws goes into the response instead of taking the game down
            try
            {
                var value = target == RemoteExecTarget.Client ? await InvokeAsync(entry) : await RunOnServerThreadAsync(entry);
                result.Ok = true;
                result.Result = value?.ToString();
            }
            catch (Exception e)
            {
                result.Exception = e.GetBaseException().ToString();
            }
            result.Logs.AddRange(capture.TakeLines());
            return result;
        }

        private static async UniTask<object> InvokeAsync(MethodInfo entry)
        {
            return await (UniTask<object>)entry.Invoke(null, null);
        }

        private static async UniTask<object> RunOnServerThreadAsync(MethodInfo entry)
        {
            if (!ServerThreadActionQueue.IsDraining) throw new InvalidOperationException("内蔵サーバーが起動していないため、サーバー側では実行できません");

            // サーバースレッドでは同期部分だけを実行し、await 以降の継続は UniTask の既定（スレッドプール）に任せる
            // Only the synchronous part runs on the server thread; continuations after await follow UniTask's default (thread pool)
            var completion = new UniTaskCompletionSource<object>();
            ServerThreadActionQueue.Enqueue(() =>
            {
                // 送られたコードの例外をサーバー更新ループへ漏らさず応答へ渡す（外部入力の隔離）
                // Hand sent code's exceptions to the response instead of leaking them into the server loop (external input isolation)
                try { ((UniTask<object>)entry.Invoke(null, null)).ContinueWith(v => completion.TrySetResult(v)).Forget(e => completion.TrySetException(e)); }
                catch (Exception e) { completion.TrySetException(e); }
            });
            var value = await completion.Task;
            await UniTask.SwitchToMainThread();
            return value;
        }
    }
}
```
（`entry.Invoke` の例外は `TargetInvocationException` で包まれるため、`GetBaseException()` で中身を出す）

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoteExecRunnerTest"` → PlayMode を明示して `--filter-value "RemoteExecServerTargetTest"`
Expected: すべて PASS。サーバー側のテストスイート（`--filter-value "Server"` の既存分）も回して、`TickEndUpdates` 追加で既存テストが壊れていないことを確かめる（`GameUpdater.ResetUpdate` で Clear されるため、テスト用 DI でも同じ登録経路を通るか確認する）。

- [ ] **Step 5: コミットする**

```bash
git add -A moorestech_server/Assets/Scripts/Server.Boot moorestech_client/Assets/Scripts/Client.RemoteExec moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(remote-exec): 実行先への振り分けと結果の組み立て"
```

---

### Task 4: トークン・台帳・入口と起動時の有効化

**Files:**
- Create: `Client.RemoteExec/Access/RemoteExecAccessFile.cs`
- Create: `Client.RemoteExec/Access/RemoteExecLedger.cs`
- Create: `Client.RemoteExec/RemoteExecEndpoint.cs`
- Create: `Client.RemoteExec/RemoteExecActivation.cs`
（Kestrel 一式の DLL は NuGetForUnity の自動参照プラグイン（`isExplicitlyReferenced: 0`）なので asmdef への追加は不要。`Client.WebUiHost.asmdef` の `"Nuget"` は実在しない参照名で、足さない）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`（references に `Client.RemoteExec`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs`（`/api/ping` の直後に分岐）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`（`KestrelPort` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`（WebUiHost 起動の直後）と `Client.Starter.asmdef`（references に `Client.RemoteExec`）
- Test: `Client.Tests/RemoteExec/RemoteExecEndpointTest.cs`

**Interfaces:**
- Consumes: `RemoteExecRunner.RunAsync`, `RemoteExecResult`, `RemoteExecLaunchOption.IsEnabled`
- Produces:
  - `public static class RemoteExecAccessFile { public const string HeaderName = "X-Remote-Exec-Token"; public static string Token { get; private set; } public static string DirectoryPath { get; } public static void Issue(int port); }`
  - `public static class RemoteExecLedger { public static string CurrentFileName { get; } public static string PathFor(int processId); public static void Append(string target, string body, bool ok); }`
  - `public static class RemoteExecEndpoint { public const string Path = "/api/remote-exec"; public static Task HandleAsync(HttpContext context); }`
  - `public static class RemoteExecActivation { public static void ActivateIfRequested(int kestrelPort); }`
  - `WebUiHost.KestrelPort`（`public static int KestrelPort => _kestrel == null ? 0 : _kestrel.ActualPort;`）

- [ ] **Step 1: 失敗するテストを書く**

`RemoteExecEndpointTest`（前例 `KestrelServerPortScanTest` と同じく Kestrel を実起動しスレッドプールで回す）:
- 有効化前（`RemoteExecLaunchOption` を無効のまま）に `POST /api/remote-exec` → 404
- 有効化後・トークン無し → 403
- 有効化後・`Origin: http://evil.example` 付き → 403
- 有効化後・正しいトークン・本文 `{"code":"return 1 + 1;","target":"client"}` → 200 かつ JSON の `ok == true`・`result == "2"`
- 2回実行後に `RemoteExecLedger.PathFor(現在pid)` の行数が 2

有効化はテスト用の公開口を作らず、`RemoteExecLaunchOption.ResolveFromCommandLine(new[]{"-remote-exec"})` と `RemoteExecActivation.ActivateIfRequested(kestrel.ActualPort)` を順に呼ぶ（プロダクションと同じ経路）。テスト後に `RemoteExecLaunchOption.ResolveFromCommandLine(new string[0])` で戻す。

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoteExecEndpointTest"`
Expected: コンパイルエラー。

- [ ] **Step 3: 実装を書く**

`Access/RemoteExecAccessFile.cs`:
```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using Game.Paths;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.RemoteExec.Access
{
    // 起動ごとのトークンを作り、ポートと一緒にユーザーのデータフォルダへだけ書く（読める人＝そのPCのユーザー権限を持つ人）
    // Issues a per-boot token and writes it with the port only into the user's data folder (readable only with that user's rights)
    public static class RemoteExecAccessFile
    {
        public const string HeaderName = "X-Remote-Exec-Token";

        public static string Token { get; private set; }

        public static string DirectoryPath => Path.Combine(GameSystemPaths.GameSystemDirectory, "RemoteExec");

        public static void Issue(int port)
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            Token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

            // 書けなければ入口は開くが誰も使えない。理由を必ずログへ出す（ディスクIO境界）
            // If unwritable the entry opens but nobody can use it, so the reason is always logged (disk IO boundary)
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var json = new JObject { ["port"] = port, ["token"] = Token, ["processId"] = System.Diagnostics.Process.GetCurrentProcess().Id };
                File.WriteAllText(Path.Combine(DirectoryPath, "access.json"), json.ToString());
                Debug.LogWarning($"[RemoteExec] 入口を開きました port:{port} access:{Path.Combine(DirectoryPath, "access.json")}");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] access.json を書けませんでした（遠隔実行は使えません）: {e.Message}");
            }
        }
    }
}
```

`Access/RemoteExecLedger.cs`:
```csharp
using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using Debug = UnityEngine.Debug;

namespace Client.RemoteExec.Access
{
    // 実行したコードの台帳。プロセスごとに1ファイルで、報告の箱に添える（ADR 0072）
    // The ledger of executed code, one file per process, attached to report boxes (ADR 0072)
    public static class RemoteExecLedger
    {
        private static readonly object WriteLock = new();

        public static string CurrentFileName => FileNameFor(Process.GetCurrentProcess().Id);

        public static string PathFor(int processId)
        {
            return Path.Combine(RemoteExecAccessFile.DirectoryPath, FileNameFor(processId));
        }

        public static void Append(string target, string body, bool ok)
        {
            var line = new JObject { ["at"] = DateTime.UtcNow.ToString("o"), ["target"] = target, ["ok"] = ok, ["code"] = body }.ToString(Newtonsoft.Json.Formatting.None);

            // 台帳が書けなくても実行結果は返す。欠けた事実はログへ残す（ディスクIO境界）
            // The run result is returned even if the ledger fails; the gap is logged (disk IO boundary)
            try
            {
                lock (WriteLock) File.AppendAllText(PathFor(Process.GetCurrentProcess().Id), line + "\n");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] 台帳に書けませんでした: {e.Message}");
            }
        }

        private static string FileNameFor(int processId)
        {
            return $"ledger-{processId}.jsonl";
        }
    }
}
```

`RemoteExecEndpoint.cs`:
```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Client.RemoteExec.Access;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.RemoteExec
{
    // POST /api/remote-exec。トークン一致かつブラウザ由来でない要求だけを実行する
    // POST /api/remote-exec; only requests with the matching token and no browser origin are executed
    public static class RemoteExecEndpoint
    {
        public const string Path = "/api/remote-exec";

        public static async Task HandleAsync(HttpContext context)
        {
            if (!IsAuthorized(out var rejectReason))
            {
                Debug.LogWarning($"[RemoteExec] 要求を拒否しました: {rejectReason}");
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync(rejectReason, CancellationToken.None);
                return;
            }

            // 本文は外部入力。読めない・形が違う場合は 400 と理由を返す（ネットワーク入力のパース境界）
            // The body is external input; unreadable or malformed yields 400 with the reason (network input parse boundary)
            string code;
            RemoteExecTarget target;
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var request = JObject.Parse(await reader.ReadToEndAsync());
                code = request.Value<string>("code") ?? throw new FormatException("code がありません");
                target = request.Value<string>("target") == "server" ? RemoteExecTarget.Server : RemoteExecTarget.Client;
            }
            catch (Exception e) when (e is JsonException || e is FormatException)
            {
                Debug.LogWarning($"[RemoteExec] 要求の本文を読めませんでした: {e.Message}");
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(e.Message, CancellationToken.None);
                return;
            }

            var result = await RemoteExecRunner.RunAsync(code, target);
            RemoteExecLedger.Append(target.ToString(), code, result.Ok);
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonConvert.SerializeObject(result), CancellationToken.None);

            #region Internal

            bool IsAuthorized(out string reason)
            {
                reason = null;
                if (context.Request.Method != "POST") reason = "POST 以外";
                else if (!string.IsNullOrEmpty(context.Request.Headers["Origin"].ToString())) reason = "Origin ヘッダ付き（ブラウザ由来）";
                else if (RemoteExecAccessFile.Token == null) reason = "トークン未発行";
                else if (context.Request.Headers[RemoteExecAccessFile.HeaderName].ToString() != RemoteExecAccessFile.Token) reason = "トークン不一致";
                return reason == null;
            }

            #endregion
        }
    }
}
```

`RemoteExecActivation.cs`:
```csharp
using Client.RemoteExec.Access;

namespace Client.RemoteExec
{
    // Web UI サーバー起動後に1度だけ呼ぶ。無効な起動では何もしない（ADR 0072: 既定では無効）
    // Called once after the Web UI server starts; a disabled boot does nothing (ADR 0072: off by default)
    public static class RemoteExecActivation
    {
        public static void ActivateIfRequested(int kestrelPort)
        {
            if (!RemoteExecLaunchOption.IsEnabled) return;
            if (kestrelPort == 0)
            {
                UnityEngine.Debug.LogError("[RemoteExec] Web UI サーバーが起動していないため遠隔実行を開けません");
                return;
            }
            RemoteExecAccessFile.Issue(kestrelPort);
        }
    }
}
```

`WebUiEndpoints.cs`（`/api/ping` 分岐の直後）:
```csharp
                if (path == Client.RemoteExec.RemoteExecEndpoint.Path && Client.RemoteExec.RemoteExecLaunchOption.IsEnabled)
                {
                    // 起動オプションで有効にした起動だけ遠隔実行を受ける（ADR 0072）
                    // Accept remote exec only on a boot enabled by the launch option (ADR 0072)
                    await Client.RemoteExec.RemoteExecEndpoint.HandleAsync(context);
                    return;
                }
```
（無効時は分岐に入らず末尾の 404 に落ちる。`staticFiles` の分岐は `/api/` を除外しているので先に吸われない）

`WebUiHost.cs`（`WebUiUrl` の下）:
```csharp
        public static int KestrelPort => _kestrel == null ? 0 : _kestrel.ActualPort;
```

`InitializeScenePipeline.cs`（WebUiHost 起動の try/catch の直後）:
```csharp
            // 起動オプションで求められていれば遠隔実行の入口を開く（ADR 0072）
            // Open the remote-exec entry if the launch option asked for it (ADR 0072)
            Client.RemoteExec.RemoteExecLaunchOption.ResolveFromCommandLine(Environment.GetCommandLineArgs());
            Client.RemoteExec.RemoteExecActivation.ActivateIfRequested(Client.WebUiHost.Boot.WebUiHost.KestrelPort);
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests ... --filter-value "RemoteExec"`
Expected: 全 PASS。`wc -l` で `WebUiEndpoints.cs` `WebUiHost.cs` `InitializeScenePipeline.cs` が 200 行以下であることを確認（超えたら責務単位で分割する）。

- [ ] **Step 5: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat(remote-exec): トークン・台帳・入口と起動時の有効化"
```

---

### Task 5: Harmony が送ったコードから使えることの確認

**Files:**
- Test: `Client.Tests/RemoteExec/RemoteExecHarmonyTest.cs`

**Interfaces:**
- Consumes: `RemoteExecRunner.RunAsync`

- [ ] **Step 1: テストを書く**

```csharp
using System.Collections;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec
{
    public class RemoteExecHarmonyTest
    {
        public static int Target() => 1;

        [UnityTest]
        public IEnumerator 送ったコードでHarmonyのPostfixを当てられる() => UniTask.ToCoroutine(async () =>
        {
            const string code = @"using HarmonyLib;
var harmony = new Harmony(""remote-exec-test"");
var original = typeof(Client.Tests.RemoteExec.RemoteExecHarmonyTest).GetMethod(""Target"");
var postfix = typeof(Patches).GetMethod(""Postfix"");
harmony.Patch(original, postfix: new HarmonyMethod(postfix));
var value = Client.Tests.RemoteExec.RemoteExecHarmonyTest.Target();
harmony.UnpatchAll(""remote-exec-test"");
return value;
}
public static class Patches { public static void Postfix(ref int __result) { __result = 42; }";
            var result = await RemoteExecRunner.RunAsync(code, RemoteExecTarget.Client);
            Assert.IsTrue(result.Ok, result.Exception + string.Join("\n", result.CompileErrors));
            Assert.AreEqual("42", result.Result);
        });
    }
}
```
（本体の途中で `}` を閉じて補助クラスを足す書き方は、包みの末尾 `return null; } }` と釣り合う。この書き方が通らなければ、補助クラスを使わず `AccessTools` とラムダで書ける形に変え、その書き方を Task 7 の CLI ヘルプにも書く）

- [ ] **Step 2: 実行して通ることを確認する**

Run: `uloop run-tests ... --filter-value "RemoteExecHarmonyTest"`
Expected: PASS。`Target` がインライン化されて Postfix が効かない場合は、`[MethodImpl(MethodImplOptions.NoInlining)]` を付ける。

- [ ] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/RemoteExec
git commit -m "test(remote-exec): 送ったコードから Harmony で差し込めることを確かめる"
```

---

### Task 6: 報告の箱への印付け

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportManifest.cs`（SchemaVersion 4・`RemoteExec` 欄・`RemoteExecMark` 型）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/RemoteExecBundleMark.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportBundleWriter.cs`（manifest 作成直後に `RemoteExecBundleMark.ApplyForCurrentSession`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/Marks/SessionOriginSnapshot.cs`（`RemoteExecEnabled` の書き読み）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/Marks/CleanExitMarkWriter.cs`（origin 作成時に `RemoteExecLaunchOption.IsEnabled` を渡す）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/CrashBundleWriter.cs`（`RemoteExecBundleMark.ApplyForPreviousSession`）
- Modify: `Client.Game.asmdef`（references に `Client.RemoteExec`）
- Test: `Client.Tests/RemoteExec/RemoteExecBundleMarkTest.cs`

**Interfaces:**
- Consumes: `RemoteExecLedger.PathFor(int)`, `RemoteExecLedger.CurrentFileName`, `RemoteExecLaunchOption.IsEnabled`
- Produces:
  - `public sealed class RemoteExecMark { public bool Enabled; public List<string> LedgerFiles = new(); }`、`BugReportManifest.RemoteExec`（無効なら null）
  - `public static class RemoteExecBundleMark { public const string DirectoryName = "remote-exec"; public static void ApplyForCurrentSession(BugReportManifest manifest, string bundleDirectory); public static void ApplyForPreviousSession(BugReportManifest manifest, string bundleDirectory, bool previousSessionEnabled, IReadOnlyList<int> salvagedProcessIds); }`
  - `SessionOriginSnapshot.RemoteExecEnabled`（`public bool RemoteExecEnabled { get; }`。旧形式の印は false として読む）

- [ ] **Step 1: 失敗するテストを書く**

`RemoteExecBundleMarkTest`（一時ディレクトリで）:
- 無効時 `ApplyForCurrentSession` → `manifest.RemoteExec == null`、`remote-exec/` が作られない
- 有効時・台帳あり → `manifest.RemoteExec.Enabled == true`、`LedgerFiles` に `ledger-<pid>.jsonl`、箱の `remote-exec/` に同名ファイル
- 有効時・台帳なし（まだ1回も実行していない）→ `Enabled == true`、`LedgerFiles` 空、`manifest.Missing` に理由は足さない（未実行は欠損ではない）
- `ApplyForPreviousSession(previousSessionEnabled:true, salvagedProcessIds:[pidA])` → pidA の台帳がコピーされる
- `SessionOriginSnapshot` を `RemoteExecEnabled=true` で `WriteTo`→`ReadFrom` して true が戻る。キーの無い旧形式 JSON は false
- manifest の `SchemaVersion == 4`

有効/無効の切り替えは `RemoteExecLaunchOption.ResolveFromCommandLine` で行う（プロダクションと同じ経路）。

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop run-tests ... --filter-value "RemoteExecBundleMarkTest"`
Expected: コンパイルエラー。

- [ ] **Step 3: 実装を書く**

`BugReportManifest.cs`（SchemaVersion のコメントと値、欄の追加）:
```csharp
        // 4: remoteExec（遠隔実行が有効だったセッションの印と台帳）を追加（ADR 0072）
        // 4: added remoteExec, the mark and ledgers of a session with remote exec enabled (ADR 0072)
        public int SchemaVersion = 4;
```
```csharp
        // 遠隔実行が有効だったセッションの印。無効なら null。取り込み側はこれが付いた箱を自動修正と集計から外す（ADR 0072）
        // Marks a session that had remote exec enabled, null otherwise; the ingest side keeps such boxes out of auto-fix and tallies (ADR 0072)
        public RemoteExecMark RemoteExec;
```
```csharp
    public sealed class RemoteExecMark
    {
        public bool Enabled;
        public List<string> LedgerFiles = new();
    }
```
（ファイルが 200 行を超える場合は `RemoteExecMark` を `RemoteExecBundleMark.cs` 側へ置く）

`RemoteExecBundleMark.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Client.RemoteExec;
using Client.RemoteExec.Access;

namespace Client.Game.InGame.BugReport
{
    // 遠隔実行が有効だったセッションの箱に印と台帳を載せる。bug と crash の両 writer から呼ぶ（ADR 0072）
    // Puts the mark and ledgers into a box from a session with remote exec enabled; called by both the bug and crash writers (ADR 0072)
    public static class RemoteExecBundleMark
    {
        public const string DirectoryName = "remote-exec";

        public static void ApplyForCurrentSession(BugReportManifest manifest, string bundleDirectory)
        {
            if (!RemoteExecLaunchOption.IsEnabled) return;
            Apply(manifest, bundleDirectory, new[] { System.Diagnostics.Process.GetCurrentProcess().Id });
        }

        // 落ちたセッション自身が開始時に書き残した有効フラグで判断する。今回の起動のフラグを使うと取り違える（F12 と同じ理由）
        // Decided by the flag the crashed session wrote at its start; using this boot's flag would misattribute (same reason as F12)
        public static void ApplyForPreviousSession(BugReportManifest manifest, string bundleDirectory, bool previousSessionEnabled, IReadOnlyList<int> salvagedProcessIds)
        {
            if (!previousSessionEnabled) return;
            Apply(manifest, bundleDirectory, salvagedProcessIds);
        }

        private static void Apply(BugReportManifest manifest, string bundleDirectory, IReadOnlyList<int> processIds)
        {
            manifest.RemoteExec = new RemoteExecMark { Enabled = true };
            foreach (var processId in processIds)
            {
                var source = RemoteExecLedger.PathFor(processId);
                if (!File.Exists(source)) continue;

                // 台帳のコピー失敗は印を残したまま欠損として申告する（ディスクIO境界）
                // A failed ledger copy keeps the mark and is declared missing (disk IO boundary)
                try
                {
                    var directory = Path.Combine(bundleDirectory, DirectoryName);
                    Directory.CreateDirectory(directory);
                    File.Copy(source, Path.Combine(directory, Path.GetFileName(source)), true);
                    manifest.RemoteExec.LedgerFiles.Add($"{DirectoryName}/{Path.GetFileName(source)}");
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    manifest.AddMissing(DirectoryName, $"遠隔実行の台帳をコピーできなかった: {e.Message}");
                }
            }
        }
    }
}
```

`BugReportBundleWriter.cs`: `var manifest = BugReportManifest.CreateHeader(...)`（62行）の後、箱ディレクトリが決まった位置（`directory` 変数が確定した直後）で `RemoteExecBundleMark.ApplyForCurrentSession(manifest, directory);` を1行呼ぶ。
`CrashBundleWriter.cs`: `BugReportRepositoryFiles.Write(...)` の直前で `RemoteExecBundleMark.ApplyForPreviousSession(manifest, directory, origin != null && origin.RemoteExecEnabled, artifacts.SalvagedProcessIds);`。

`SessionOriginSnapshot.cs`: 既存の steamId 等と同じ並びで `RemoteExecEnabled` をコンストラクタ引数・`WriteTo` の `["remoteExecEnabled"]`・`ReadFrom` の読み取り（キーが無ければ false）に足し、`WithSnapshotCapture` / `WithSalvageMissing` でも引き継ぐ。公開コンストラクタの呼び出し元（`CleanExitMarkWriter.cs:25`）は `RemoteExecLaunchOption.IsEnabled` を渡す。ただし `CleanExitMarkWriter.InstallAtStartup` は `InitializeScenePipeline` で `ResolveFromCommandLine` より前に呼ばれるため、`ResolveFromCommandLine` の呼び出しを `Playtest.PreviousSessionStartupTasks.BeginCurrentSessionMarks();` より前へ移す（Task 4 で置いた位置を上へ動かし、`ActivateIfRequested` は WebUiHost 起動の直後に残す）。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests ... --filter-value "RemoteExecBundleMarkTest|BugReport|CrashBundle|SessionOrigin"`
Expected: 新規・既存ともに PASS（manifest の既存テストが SchemaVersion 3 を固定していたら 4 に更新する）。

- [ ] **Step 5: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat(remote-exec): 有効だったセッションの報告に印と台帳を載せる"
```

---

### Task 7: 取り込み側の除外と送る側 CLI

**Files:**
- Modify: `scripts/playtest/enqueue-autofix.sh`（kind ガードの直後に remoteExec ガード）
- Modify: `scripts/playtest/digest_collect.py`（`load_reports` で remoteExec 付きを除外して stats に件数）
- Modify: `scripts/playtest/digest_reporter.py`（除外件数を1行出す）
- Test: `scripts/playtest/tests/test-enqueue-autofix.sh`（ケース追加）、`scripts/playtest/tests/test_digest.py`（ケース追加）
- Create: `scripts/playtest/lib/verify-ssh.sh`（`verify-on-windows.sh` の SSH 接続部を関数へ切り出す）
- Modify: `scripts/playtest/verify-on-windows.sh`（切り出した関数を使う）
- Create: `scripts/playtest/remote-exec.sh`
- Create: `scripts/playtest/tests/test-remote-exec.sh`
- Modify: `scripts/playtest/README.md`（使い方の節）

**Interfaces:**
- Consumes: manifest の `remoteExec.enabled`、ゲームの `access.json`（`port`/`token`）、`POST /api/remote-exec`
- Produces: `scripts/playtest/remote-exec.sh [--windows] [--target client|server] [FILE|-]`（stdout に応答 JSON。HTTP 非200は exit 1）

- [ ] **Step 1: 失敗するテストを書く**

- `test-enqueue-autofix.sh`: `{"kind":"bug","remoteExec":{"enabled":true,"ledgerFiles":[]}}` の箱 → exit 3 で「遠隔実行が有効だったセッション」のログ。`--force` 付きなら投入される。`remoteExec: null` の bug は従来どおり投入。
- `test_digest.py`: remoteExec 付きの bug 1件＋通常 bug 1件 → テスター報告の件数 1、除外件数 1。
- `test-remote-exec.sh`: `python3 -m http.server` 相当の小さなスタブ（`tests/lib/` に置く。`X-Remote-Exec-Token` を検証し固定 JSON を返す）と一時 `access.json` を用意し、`MOORESTECH_REMOTE_EXEC_ACCESS=<一時パス> remote-exec.sh - <<< 'return 1;'` が JSON を出す／トークン不一致で exit 1。

- [ ] **Step 2: 実行して失敗を確認する**

Run: `bash scripts/playtest/tests/test-enqueue-autofix.sh; python3 -m pytest scripts/playtest/tests/test_digest.py -q; bash scripts/playtest/tests/test-remote-exec.sh`
Expected: 追加ケースが FAIL。

- [ ] **Step 3: 実装を書く**

`enqueue-autofix.sh`（`[ "${MANIFEST_KIND}" != bug ] && log "--force で ..."` の直後）:
```bash
# 遠隔実行が有効だったセッションの箱は、コードで状態を変えた後の報告なので自動修正の対象外（ADR 0072）
# A box from a session with remote exec enabled reports a state changed by code, so it is not auto-fixed (ADR 0072)
REMOTE_EXEC="$(python3 -c '
import json,sys
mark = json.load(open(sys.argv[1])).get("remoteExec")
print("1" if isinstance(mark, dict) and mark.get("enabled") is True else "0")
' "${BOX}/manifest.json" 2>/dev/null)" || REMOTE_EXEC=0
if [ "${REMOTE_EXEC}" = 1 ] && [ "${FORCE}" != 1 ]; then
  log "遠隔実行が有効だったセッションの箱は自動修正ランの対象外。投入するなら --force: ${ID}"
  exit 3
fi
```

`digest_collect.py` の `load_reports`: manifest を読んだ後、`manifest.get("remoteExec")` が dict で `enabled` が True なら `stats["remoteExec"] = stats.get("remoteExec", 0) + 1` して `continue`。`digest_reporter.py` はその件数が 1 以上なら「遠隔実行ありの報告 N 件（集計から除外）」を1行出す。

`lib/verify-ssh.sh`: `verify-on-windows.sh` にある `MOORESTECH_VERIFY_*` の読み込み・WoL・ssh 呼び出しを `verify_ssh <command...>` 関数として切り出す（中身は移動のみ。挙動を変えない）。`verify-on-windows.sh` は `source` して使う。

`remote-exec.sh`:
```bash
#!/usr/bin/env bash
# 起動オプション -remote-exec 付きのゲームへ C# 本体コードを送り、結果 JSON を出す（ADR 0072）
# Sends a C# method body to a game launched with -remote-exec and prints the result JSON (ADR 0072)
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
WINDOWS=0; TARGET=client; SRC=-
while [ $# -gt 0 ]; do
  case "$1" in
    --windows) WINDOWS=1 ;;
    --target) TARGET="$2"; shift ;;
    -h|--help) echo "usage: remote-exec.sh [--windows] [--target client|server] [FILE|-]"; exit 0 ;;
    *) SRC="$1" ;;
  esac
  shift
done
CODE="$(cat "$SRC")"
BODY="$(python3 -c 'import json,sys; print(json.dumps({"code": sys.argv[1], "target": sys.argv[2]}))' "$CODE" "$TARGET")"

if [ "$WINDOWS" = 1 ]; then
  # 検証機ではトークンを SSH 越しに読み、要求も検証機の中から 127.0.0.1 へ送る（入口は外へ開いていない）
  # On the verification machine the token is read over SSH and the request is sent to 127.0.0.1 from inside it (the entry is not exposed)
  source "$HERE/lib/verify-ssh.sh"
  printf '%s' "$BODY" | verify_ssh powershell -NoProfile -Command '$a = Get-Content "$env:APPDATA\.moorestech\RemoteExec\access.json" | ConvertFrom-Json; $b = [Console]::In.ReadToEnd(); Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$($a.port)/api/remote-exec" -Headers @{"X-Remote-Exec-Token"=$a.token} -ContentType "application/json" -Body $b | ConvertTo-Json -Depth 5'
  exit $?
fi

ACCESS="${MOORESTECH_REMOTE_EXEC_ACCESS:-$HOME/Library/Application Support/moorestech/RemoteExec/access.json}"
PORT="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["port"])' "$ACCESS")"
TOKEN="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["token"])' "$ACCESS")"
curl -sS --fail-with-body -X POST "http://127.0.0.1:${PORT}/api/remote-exec" \
  -H "X-Remote-Exec-Token: ${TOKEN}" -H "Content-Type: application/json" --data-binary "$BODY"
```
（Mac の既定値は `GameSystemPaths.GameSystemDirectory` の macOS 分岐 `/Users/<user>/Library/Application Support/moorestech` に合わせてある。Windows 分岐は `%APPDATA%\.moorestech`）

README に「使い方」節（起動オプションの付け方: Steam の起動オプション欄、または `steam.exe -applaunch <appid> -remote-exec`／送り方の例／タイトル画面では使えない／固まったら再起動／報告に印が付く）を足す。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: Step 2 と同じ3コマンド＋既存の `bash scripts/playtest/tests/test-verify-on-windows.sh`
Expected: 全 PASS（verify-on-windows の既存テストが切り出しで壊れていないこと）。

- [ ] **Step 5: コミットする**

```bash
git add -A scripts/playtest
git commit -m "feat(remote-exec): 取り込み側の除外と送る側 CLI"
```

---

### Task 8: 検証機での実機確認（完了条件 R13）

**Files:**
- Create: `docs/research/2026-09-27-remote-exec-e2e.md`（記録）

- [ ] **Step 1: Windows の Release ビルドを焼き、検証機へ置く**

`scripts/playtest/release-playtest.sh` の Steam アップロード手前までと同じ入口（`ReleaseLocalBuildCli.WindowsReleaseLocalBuild`）でビルドし、`verify-on-windows.sh` と同じ経路で検証機の作業フォルダ（Steam のインストール先とは別）へ転送する。Steam 配布の更新はこのタスクでは行わない（PR マージ後の通常の配布手順で行う）。

- [ ] **Step 2: オプションなしで起動し、入口が開かないことを確かめる（R13-3）**

対話セッションのスケジュールタスク経由（`scripts/playtest/windows/run-smoke.ps1` の `Start-SteamInInteractiveSession` と同じ方式）で起動し、ゲーム開始後に検証機内から `Invoke-WebRequest -Method Post http://127.0.0.1:<Web UI のポート>/api/remote-exec` → 404、`%APPDATA%\.moorestech\RemoteExec\access.json` が今回の起動で作られていない（無い、または更新時刻が起動前）ことを確認。

- [ ] **Step 3: `-remote-exec` 付きで起動し、実行と Harmony を確かめる（R13-1, R13-2）**

```bash
echo 'return QualitySettings.vSyncCount;' | scripts/playtest/remote-exec.sh --windows
echo 'return System.Threading.Thread.CurrentThread.ManagedThreadId;' | scripts/playtest/remote-exec.sh --windows --target server
```
Harmony: Task 5 のテストと同じ形のコードで、ゲーム内の既存 static メソッド（例: `Core.Update.GameUpdater.SecondsToTicks`）に Postfix を当て、呼び出し結果が変わることを返させる。最後に `UnpatchAll` する。

- [ ] **Step 4: 印付き報告の一巡を確かめる（R10, R11）**

有効起動のままポーズメニューからバグ報告を1件送り、取り込み後の `moorestech_logs/harness/playtest/reports/<steamId>/<id>/manifest.json` に `remoteExec.enabled: true` と `remote-exec/ledger-*.jsonl` があり、`enqueue-autofix.sh` が exit 3 で拒否することを確認。確認後、その報告は取り込み先で「検証用」と分かる名前へ移すか削除する（テスター報告の母数を汚さない）。

- [ ] **Step 5: ログの警告語がゼロであることを確かめる**

検証機の Player.log を `[RemoteExec]` 区間で `grep -E "拒否|refus|Exception|Error|書けません|開けません"` し、Step 3〜4 の意図した拒否（Step 2 の 404 は拒否ログを出さない）以外に0件であること。

- [ ] **Step 6: 記録を書いてコミットする**

結果（各ステップの出力抜粋・警告語 grep の結果）を `docs/research/2026-09-27-remote-exec-e2e.md` に書き、`git add docs/research && git commit -m "docs(remote-exec): 検証機での実機確認の記録"`。

---

### Task 9: 全ブランチレビューと後始末（省略不可）

- [ ] **Step 1: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。** 使うスキルは moores-code-review。
- [ ] **Step 2: レビュー指摘の反映が判定経路・条件式・評価時点（入口の許可判定・有効化の順序・印付けの条件）に触れたら、反映後のバイナリで Task 8 を再実施してから完了とする。**
- [ ] **Step 3: 残課題の起票。** plan・Task 8 の記録・進捗台帳に書いた「未検証」「未確認」「残差」を1件ずつ `bd create` し、記録の結論に issue id を列挙する（要約で済ませない）。
- [ ] **Step 4: 実機確認の合否は、期待する肯定行ではなく、Task 8 区間の `[RemoteExec]`・`[WebUiHost]` の警告・拒否語が意図したもの以外ゼロであることで判定したかを記録に明記する。**
- [ ] **Step 5: PR を作成し（マージコミット運用）、worktree を `moores-wt rm remote-exec` で畳む。** PR マージ後、moorestech-rhvub（上限なし FPS の再現検証）へ「配布ビルド更新後に着手可」と note する。

---

## Self-Review 記録

- Requirements coverage: R1→T2/T4/T8、R2→T1/T2、R3→T2/T3、R4→T3、R5→T3、R6→T4、R7→T4、R8→T1/T5、R9→T4、R10→T6、R11→T7、R12→T7/T8、R13→T8。
- 保留・縮退経路: (a) access.json が書けない→入口は開くが使えない（ログ ERROR）。解消は再起動。恒久失敗になる入力: データフォルダが書込不可の環境。検証機では起きない前提で許容（ユーザー操作を失敗させる経路ではない＝開発者の診断手段が使えないだけ）。(b) server 指定でサーバー未起動→即時失敗（`IsDraining` はサーバーの最初の tick 以降 true）。最小構成: ゲーム開始直後・ワールドロード中は false → 理由付き失敗を返し、ロード後に再送すれば通る。(c) トークン不一致→403＋ログ。
- ユーザー操作を恒久に失敗させる経路: 無し（遠隔実行は開発者専用で、プレイヤーの操作経路に割り込まない。無効時は分岐自体に入らない）。

| 状態 | 起きる時機 | 誰が | 見えるもの | 自然解消 |
|---|---|---|---|---|
| サーバー未起動で server 指定 | ワールドロード完了前 | 開発者 | `ok:false`＋理由 | ロード後に再送で解消 |
| access.json 書込失敗 | 起動時 | 環境 | Player.log の ERROR | 再起動（環境を直す） |
| 固まるコード | 実行時 | 開発者 | 応答が返らない | しない（再起動。ADR 0072 で許容） |

- 共有状態の持ち主: 有効フラグ（`RemoteExecLaunchOption`、起動時1回決定・以後不変）、トークン（`RemoteExecAccessFile`、起動時1回）、台帳（ファイル、追記のみ）。書き換え後に最新化が要る保持者は無い。

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | 遠隔実行一式 | 新アセンブリ `Client.RemoteExec` | static（ゲーム寿命・メインメニューへ戻らない設計） | `Client.WebUiHost.Boot.WebUiHost`（static のゲーム寿命サービス） |
| 2 | 起動引数判定 | `Client.RemoteExec` | `Environment.GetCommandLineArgs` を1度 | `StandalonePlaytestSmokeSettings.HasMarker` |
| 3 | HTTP 入口 | 既存 `WebUiEndpoints` の if 連鎖に1分岐＋`Path`/`HandleAsync` の静的クラス | 既存 Kestrel | `Game.ItemMasterEndpoint` 等 |
| 4 | サーバースレッド実行 | `Server.Boot.Loop.ServerThreadActionQueue`（汎用・ドメイン語彙なし） | `GameUpdater.TickEndUpdates` 登録 | `TickEndPacketQueue` / `WorldMutationTickEndUpdater` の登録 |
| 5 | 報告への印 | `Client.Game/InGame/BugReport/RemoteExecBundleMark` | 両 writer から呼ぶ共通関数 | `BugReportRepositoryFiles.Write`（bug/crash 共通の書き込み） |
| 6 | 落ちたセッションの有効フラグ | `SessionOriginSnapshot` に1欄 | 開始時に自分で書き残す | 同クラスの steamId（F12） |
| 7 | 依存追加 | NuGetForUnity（packages.config） | — | Kestrel 一式 |

新規パターンとして注目してほしい点: #4 の `ServerThreadActionQueue` は、`GameUpdater.TickEndUpdates` に「外部スレッドの任意処理」を流す初の汎用口。静的キューにしたのは、クライアント側（別アセンブリ・DI 外）から積むため。

## 判断記録（ADR）

- 設計の裁定は `docs/adr/0072-remote-exec-in-distributed-builds.md`（出所欄はそちらが正。書き換えない）。
- planning 中の判断:
  - Roslyn は UPM の `org.nuget.microsoft.codeanalysis.csharp`（`defineConstraints: UNITY_EDITOR` で製品に入らない）を外し、NuGetForUnity で入れ直す。出所: agent判断（調査: 同パッケージの dll.meta が UNITY_EDITOR 制約、プロジェクトのコードは Roslyn を参照していない、uloop v3 は外部コンパイラを使う）。
  - Task 1 を可否ゲートとし、製品ビルドで emit できなければ止めてユーザーへ戻す。出所: agent判断（ADR 0072 の「ゲーム内でコンパイル」が技術的に成立するかが未検証のため）。
  - サーバー実行は `ServerThreadActionQueue` を `TickEndUpdates` に常時登録する（無効時も空キューの確認だけ走る）。出所: agent判断（サーバー側は起動引数を知らない。空キューの確認は無視できる負荷）。
  - 台帳は `GameSystemDirectory/RemoteExec/ledger-<pid>.jsonl`、異常終了の箱は退避済み pid の台帳を拾う。出所: agent判断（`PreviousSessionArtifacts.SalvagedProcessIds` の前例）。
  - manifest の SchemaVersion を 4 に上げる。出所: agent判断（ADR 0057 の契約変更の作法）。
  - 検証機の実機確認は Steam 配布を更新せず、焼いたビルドを作業フォルダへ置いて行う。出所: agent判断（配布更新は PR マージ後の通常手順に任せる）。
