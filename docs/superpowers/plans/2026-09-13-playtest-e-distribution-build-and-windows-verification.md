# プレイテスト E: 配布ビルド工程とWindows検証機 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** master の指定コミットから Mac mini がコマンド1つで Windows 配布ビルドを焼き、Steam の `playtest` ブランチへ上げ、自宅 Windows 検証機で通し検証を回して合否を返すまでを実装する。

**Architecture:** Unity Editor 側に「Release固定のWindows配布ビルド入口（GUIメニュー＋batchmode CLI）」「BuildInfo焼き込み」「ffmpeg同梱」を足し、ランタイム側に「配布ビルドを引数で自動運転する通し検証ランナー（既存 `StandaloneTerrainQa*` と同型）」を足す。Mac mini 側は `scripts/playtest/` の bash 群（worktree作成→batchmodeビルド→steamcmd→検証機での通し→告知テキスト）で、外部コマンドはすべて env で差し替え可能にしスタブで bash テストする。

**Tech Stack:** Unity 6000.3.8f1（Editor: `Assembly-CSharp-Editor` の `Client.Editor.Build`、Runtime: `Client.Starter` asmdef）、C#（UniTask・Newtonsoft.Json・`JsonUtility`）、bash、PowerShell（検証機側）、steamcmd、Tailscale + Wake-on-LAN。

## Requirements

- R1. `moorestech/Build/WindowsReleaseLocalBuild` メニューを新設する。Development/Release を聞かず Release 固定、出力先だけフォルダ選択パネルで聞き前回パスを記憶する（ADR 0036 の Mac 版と同型）。受入: メニューから実行すると `IsDevelopmentBuild=false` / `IsStrictBundling=true` / `BundleLocalGameData=true` の `PlayerBuildRequest` で `BuildPipeline.Execute` が呼ばれ、成功時に成果物が Finder で開く。
- R2. batchmode 入口 `Client.Editor.Build.ReleaseLocalBuildCli.WindowsReleaseLocalBuild` を新設する。出力先は env `MOORESTECH_BUILD_OUTPUT`、末尾で `EditorApplication.Exit`（成功0／ビルド失敗1／env未設定2）。受入: env 未設定時に理由を `Debug.LogError` して exit 2、設定時は R1 と同一の `PlayerBuildRequest` で焼ける。
- R3. `PlayerBuildRequest` に4つ目の bool を足さない（[[.decisions/2026-08-02-PlayerBuildRequestは3boolのまま維持する.md]]）。受入: `PlayerBuildRequest.cs` のフィールド数が変わっていない。
- R4. `FfmpegRuntimeBundler` を新設し、Windows 配布ビルドに `ffmpeg.exe` と ffmpeg の LICENSE を同梱する。ソースは `moorestech_client/Assets/PersonalAssets/moorestech-client-private/ffmpeg/win-x64/`。ソース不在は strict（`IsStrictBundling=true`）でビルド失敗、CI互換（false）は警告のみ。受入: strict でソース不在なら `BuildFailedException`、存在すれば成果物の `moorestech_Data/Plugins/x86_64/` 隣に `ffmpeg.exe` と `ffmpeg-LICENSE.txt` が並ぶ。
- R5. `BuildInfoWriter` が `StreamingAssets/build-info.json` に Global Constraints §1 の JSON を書く。`steamBuildLabel` は env `MOORESTECH_STEAM_BUILD_LABEL`（未設定時は空文字）、`masterDataCommit` は `.moorestech-external-revisions.json` のピンと `../moorestech_master` の実 HEAD が一致することを確認したうえで実 HEAD を書く（不一致は strict でビルド失敗）。受入: EditMode 単体テストで §1 の全キーが揃った JSON が組め、ピン不一致が検出できる。
- R6. `BuildInfo` C# 型を `Client.Game/InGame/BugReport/BuildInfo.cs` に置き、`build-info.json` を読める。受入: 単体テストで §1 の JSON をラウンドトリップできる。
- R7. 配布ビルドを引数で自動運転する通し検証ランナーを新設する。`--playtestSmoke --smokePhase <phase1|phase2> --smokeResultDirectory <dir>` で起動し、`result.json` を書いて `Application.Quit(0|1|2)` する。受入: 引数の欠落・空値・重複・未知 phase を理由付きで拒否する単体テストが通る。
- R8. 通しシナリオ `playtest-smoke` は phase1（起動→新規ワールド→チュートリアル開始→セーブ→終了）と phase2（起動→既存ワールドロード→報告送信 kind=bug・説明 "smoke"→アップロード完了待ち→終了）の2回起動で構成する。受入: 検証機で phase1・phase2 の `result.json` がともに `success: true`。
- R9. Steam アップロード資材 `scripts/playtest/steam/app_build_playtest.vdf`・`scripts/playtest/steam/depot_build_windows.vdf` を置く。app 1958160、`setlive` は `playtest`、depot id は env `MOORESTECH_STEAM_DEPOT_ID` から差し込む。受入: スクリプトが差し込んだ vdf に生のトークンが残っていないことを bash テストで確認できる。
- R10. `scripts/playtest/release-playtest.sh <commit>` が Mac mini で worktree作成→batchmodeビルド→成果物検証→steamcmd→検証機での通し検証→告知テキスト出力まで行う。外部コマンド（`moores-wt`・Unity・steamcmd・検証スクリプト）は env で差し替え可能。受入: スタブを差した bash テストで、成功系の呼び出し順と失敗系（env欠落・ビルド失敗・検証失敗）の early return が固定される。
- R11. `scripts/playtest/verify-on-windows.sh <steamBuildLabel>` が WoL→ssh到達待ち（最大10分）→検証機で `playtest` ブランチ更新→phase1・phase2 実行→`result.json` 回収→受け口 `GET /v1/inbox` で報告到達確認、まで行う。起こせなければ検証失敗として終える。受入: スタブ bash テストで、起動失敗・ssh到達せず・result失敗・inbox未達それぞれが非0終了かつ理由をstderrへ出す。
- R12. 検証機側に置くスクリプトは `scripts/playtest/windows/run-smoke.ps1` を正本とし、実行のたびに `scp` で送る（検証機に手置きしたコピーを持たない）。前提ツールと初回セットアップ手順は `scripts/playtest/README.md` に書く。受入: README に Steam クライアント・OpenSSH Server・WoL 設定・Steamブランチパスワード入力の手順があり、`verify-on-windows.sh` が毎回 ps1 を送っている。
- R14. 初回に検証機で合格した配布ビルドについて、人が Remote Desktop で検証機に入り「マウスで視点回転しながら Tab でインベントリ（WebUI）を開閉し、ホイールスクロールと右ドラッグ視点回転の両方が効くか」を確認する。結果は `scripts/playtest/README.md` の「CEF raw input 確認」節の手順どおり bd note に書き、奪取が再現したら既知バグ一覧へ載せる（[[.decisions/2026-09-13-CEF raw input応急処置は入れず検証機の通し検証で実害を確かめてから決める.md]]）。自動化はしない（配布ビルドでは入力注入が使えないため）。受入: README に手順があり、Task 6 の完了条件に「初回のみ手動確認の bd note」が含まれる。
- R13. 最終タスクとして moores-code-review による全ブランチレビューを実行する（省略不可）。レビュー反映がビルド経路（Editor ビルド入口・Bundler・BuildInfo・smoke ランナー・`release-playtest.sh`）に触れたら、検証機での通し検証を反映後のバイナリで再実施してから完了とする。
- やらないこと: 受け口の実装（plan D）／報告UI（plan G）／Mac mini への取り込みと日次ダイジェスト（plan H）／セーブ互換（plan F）／Steam Web 側の操作（ブランチ作成・ベータパスワード設定・キー発行。README の手順書に留め自動化しない）／テスター向け文言の日英独整備（plan G）／検証機の常時起動化。

## Global Constraints

### 共有契約 §1（BuildInfo。plan B R7 の `BuildInfoWriter` を拡張。plan E が生成、plan D/G/H が読む）— 逐語転記

`StreamingAssets/build-info.json`:
```json
{ "commit": "<40hex>", "branch": "master", "masterDataCommit": "<40hex>", "dirty": false,
  "steamBuildLabel": "playtest-20260913-1730", "builtAt": "2026-09-13T17:30:00+09:00", "target": "StandaloneWindows64" }
```
C# 型は `Client.Game/InGame/BugReport/BuildInfo.cs`（plan B の `BugReportManifest.Repository` の代替として `manifest.buildInfo` に埋める）。Editor実行時は `build-info.json` 不在なので plan B の git probe を使う。

### 共有契約 §7（配布ビルド工程。plan E）— 逐語転記

- `moorestech/Build/WindowsReleaseLocalBuild` メニュー（ADR 0036 の Mac版と同型）と batchmode 入口 `ReleaseLocalBuildCli.WindowsReleaseLocalBuild`（出力先 env `MOORESTECH_BUILD_OUTPUT`）。Release固定・`BundleLocalGameData=true`・strict。
- 同梱: CEF win-x64（既存 `CefRuntimeBundler`）、`ffmpeg.exe`（新設 `FfmpegRuntimeBundler`、ソースは `moorestech_client/Assets/PersonalAssets/moorestech-client-private/ffmpeg/win-x64/ffmpeg.exe`、LICENSE 同梱）、`build-info.json`。
- Mac mini: `scripts/playtest/release-playtest.sh <commit>`: `moores-wt new`相当（`--no-editor`）→ batchmode ビルド → `steamcmd +login <user> +run_app_build <vdf>` で app 1958160 のブランチ `playtest` へ → 検証機で通し検証 → 合格なら bd note とDiscord告知用テキスト出力。資格情報は `~/hermes-agent/data/services/playtest/env.sh`。
- 検証機: 自宅Windows PC（Tailscale）。`scripts/playtest/verify-on-windows.sh`: WoL（MACはenv）→ ssh 到達待ち（最大10分）→ `steamcmd`（検証機側）で `playtest` ブランチ更新 → プレイテストDSL（`Client.Playtest`）を配布ビルドに対して起動する通しシナリオ `playtest-smoke`（起動→新規ワールド→チュートリアル開始→セーブ→終了→ロード→報告送信(kind=bug, 説明"smoke")）→ result.json 回収 → 受け口に報告が届いたことを `GET /v1/inbox` で確認。

> §7 の「プレイテストDSL（`Client.Playtest`）を配布ビルドに対して起動する」は**そのままでは成立しない**（`Client.Playtest.asmdef` は `includePlatforms: ["Editor"]` かつ `Unity.Recorder.Editor` 参照で、Player ビルドに1バイトも入らない。投入経路も `uloop execute-dynamic-code` で稼働中 Editor に依存する）。本 plan は §7 の意図（同じ通し手順を配布ビルドで自動実行する）を、既存の Player 自動運転前例 `Client.Starter/StandaloneQa/StandaloneTerrainQa*`（CLI引数マーカー＋`RuntimeInitializeOnLoadMethod`＋`result.json`＋`Application.Quit`）と同型の**配布ビルド向けシナリオ起動**で満たす（Task 4）。Editor DSL は従来どおり Editor 側の検証に残す。この読み替えは「判断記録（ADR）」の D-2 に記載する。

### 前提となる他 plan（このブランチを切る前に master へ入っていること）

- plan B（`docs/superpowers/plans/2026-09-11-bug-report-b-client-capture-and-report-ui.md`）: `RepositoryStateProbe`（`ProbeGit`・`RepositoryRoot`・`MasterDataRoot`・`BuildInfoFileName`）、`BuildInfoWriter`、`BugReportOutbox`、`BugReportBundleWriter`、`BugReportCaptureSession`、`GameSystemPaths.BugReportOutboxDirectory`。Task 3・4 が使う。
- plan D（受け口）: `POST /v1/session`・`PUT /v1/uploads/...`・`GET /v1/inbox`、クライアント側アップローダ（`READY` かつ `UPLOADED` 無しを送り、送信後に `UPLOADED` を置く）。Task 4・6 が使う。
- plan G（報告UI）: 報告種別 `kind`（"bug"|"feedback"）を持つ送信経路。Task 4 は UI ではなく `BugReportBundleWriter` を直接呼ぶが、`kind` を manifest に書く責務は plan G の型に乗る。

Task 1・2・5 はこれらに依存しない。依存タスク（3・4・6）の着手時に前提型が存在しなければ、実装せずコントローラへ報告して止める（前提の欠落を握り潰して代替実装を書かない）。

### 作業・検証の規約

- 本 plan は master の `93ddfdab3` 時点のコードを読んで書いた（起票セッションがリポジトリ読み取り専用で `git pull` を実行できなかったため）。実装着手時に最初へ `git pull` し、本 plan が引用した既存コード（`BuildMenu.cs`・`BuildPipeline.cs`・`Client.Starter/StandaloneQa/*`・`MainGameModelRegistration.cs`）が変わっていないかを確認する。
- 作業ブランチ: `feature/playtest-distribution-build`。Mac mini では `moores-wt new feature/playtest-distribution-build --no-editor` で使い捨て worktree を切り、そこで作業する（メインワークツリーでのブランチ操作は hook で拒否される）。着手時に必ず `pwd` を確認する。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client`。EditMode テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。「Unity is reloading (Domain Reload in progress)」が出たら45秒待ってリトライ。
- Unity の Windows ビルドには `WindowsStandaloneSupport` モジュールが要る（Mac mini の `/Applications/Unity/Hub/Editor/6000.3.8f1/PlaybackEngines/WindowsStandaloneSupport` に導入済みであることを Task 1 の Step で確認する）。
- bash スクリプトは `set -eu`（パイプ結果を見る箇所は `set -o pipefail`）、外部コマンドは `${XXX_BIN:-既定}` で差し替え可能にし、テストはスタブを `PATH` 前置ではなく env 経由で差す。テストの様式は `.agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixed-world-environment-test.sh`（`FAILURES` カウンタ・`FAIL:` 行・末尾 `PASS:` 行・非0終了）に合わせる。
- fail-closed で拒否・保留・無視する経路（env 未設定・ソース不在・WoL 失敗・ssh 未到達・inbox 未達）は、必ず理由を stderr か `Debug.LogError` へ出す。無音の縮退は禁止。
- C#: コメントは「// 日本語 → // English」の2行セットを3〜10行ごと。1ファイル200行以下、1ディレクトリ10ファイル以下。`partial`・`Func<>`・デフォルト引数・単純 getter/setter プロパティ禁止。イベントは UniRx。`Update()` ポーリング禁止（待機はフレームポーリング＋期限、`Time.realtimeSinceStartup` 基準。サーバーロジックではないので実時間APIで良い）。`try-catch` は外部境界（外部プロセス起動・ネットワーク・外部JSONパース）に限り、境界である根拠をコメントに書く。
- `.meta` は手動作成しない。Prefab・シーン・ScriptableObject をテキスト編集しない。
- 別リポジトリ（`moorestech-client-private`）に変更が及ぶ Task 2 は、そちらでも push して PR を作る。本 repo の `.moorestech-external-revisions.json` のピンは、その PR が指す push 済みコミットを指すこと。
- 型名・ファイル名は本 plan 記載のとおり: `ReleaseLocalBuildCli`・`FfmpegRuntimeBundler`・`BuildInfo`・`BuildInfoComposer`・`StandalonePlaytestSmokeSettings`・`StandalonePlaytestSmokeBootstrap`・`StandalonePlaytestSmokeRunner`・`StandalonePlaytestSmokeResult`・`StandalonePlaytestSmokeResultWriter`。
- 各タスク末尾でコミットする。コミットメッセージ末尾に以下を付ける:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
  ```
- タスク管理は bd。着手前に `bd create` → `bd update <id> --claim`、完了で `bd close`。設計メモは `bd note`。

---

### Task 1: Windows Release ローカル配布ビルド入口（メニュー＋batchmode CLI）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Editor/Build/BuildMenu.cs`（Release固定入口を1本に集約し、Windows 版メニューを追加）
- Create: `moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs`
- Test: 手動検証（`Assembly-CSharp-Editor` は asmdef を持たないため `Client.Tests` から参照できない。自動テストは Task 5 の bash テストが Unity 呼び出し契約を固定する）

**Interfaces:**
- Consumes: `BuildPipeline.Execute(PlayerBuildRequest)`（`internal static`、同一アセンブリ）、`PlayerBuildRequest`、`PlayerBuildOutcome`
- Produces:
  - `public static class ReleaseLocalBuildCli { public const string OutputDirectoryEnvKey = "MOORESTECH_BUILD_OUTPUT"; public static PlayerBuildRequest CreateRequest(BuildTarget target, string outputDirectory); public static void WindowsReleaseLocalBuild(); }`
  - `BuildMenu` のメニュー項目 `moorestech/Build/WindowsReleaseLocalBuild`（既存 `moorestech/Build/MacOsReleaseLocalBuild` と同じ経路を通る）

- [ ] **Step 1: `ReleaseLocalBuildCli` を作る**

`moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs`:
```csharp
using System;
using UnityEditor;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// Release固定のローカル配布ビルドの契約と、無人（batchmode）入口
    /// The Release-fixed local distribution build contract and its unattended (batchmode) entry
    /// </summary>
    public static class ReleaseLocalBuildCli
    {
        public const string OutputDirectoryEnvKey = "MOORESTECH_BUILD_OUTPUT";

        // GUIメニューとbatchmodeで同一の契約を使い、入口ごとの設定差を構造的に消す
        // Menu and batchmode share one contract so per-entry setting drift cannot happen
        public static PlayerBuildRequest CreateRequest(BuildTarget target, string outputDirectory)
        {
            return new PlayerBuildRequest
            {
                Target = target,
                OutputDirectory = outputDirectory,
                IsDevelopmentBuild = false,
                IsStrictBundling = true,
                BundleLocalGameData = true,
            };
        }

        // Mac miniのrelease-playtest.shが -executeMethod で呼ぶ無人入口
        // The unattended entry release-playtest.sh calls on the Mac mini via -executeMethod
        public static void WindowsReleaseLocalBuild()
        {
            Run(BuildTarget.StandaloneWindows64);
        }

        private static void Run(BuildTarget target)
        {
            // 出力先未指定で走らせるとカレント直下を汚すため、理由を残して拒否する
            // Running without an output directory would pollute the CWD, so refuse and log why
            var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryEnvKey);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Debug.LogError($"[ReleaseLocalBuildCli] {OutputDirectoryEnvKey} が未設定のためビルドしません");
                EditorApplication.Exit(2);
                return;
            }

            var outcome = BuildPipeline.Execute(CreateRequest(target, outputDirectory));
            Debug.Log($"[ReleaseLocalBuildCli] outcome:{outcome} target:{target} output:{outputDirectory}");
            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }
    }
}
```

- [ ] **Step 2: `BuildMenu` の Release 固定入口を1本に集約し Windows を追加する**

`BuildMenu.cs` の `MacOsReleaseLocalBuild` を差し替え、下の2メソッドを追加する（`BuildInteractive` は Development を聞く既存メニュー専用のまま残す）:

```csharp
        // 展示会などの配布用。Development/Releaseを聞かずRelease固定で焼く
        // For distribution such as exhibitions: no Development prompt, always Release
        [MenuItem("moorestech/Build/MacOsReleaseLocalBuild")]
        public static void MacOsReleaseLocalBuild()
        {
            BuildReleaseLocalInteractive(BuildTarget.StandaloneOSX);
        }

        // プレイテスト配布用のWindows成果物。契約はmac版と同一
        // The Windows artifact for playtest distribution; same contract as the mac entry
        [MenuItem("moorestech/Build/WindowsReleaseLocalBuild")]
        public static void WindowsReleaseLocalBuild()
        {
            BuildReleaseLocalInteractive(BuildTarget.StandaloneWindows64);
        }

        private static void BuildReleaseLocalInteractive(BuildTarget buildTarget)
        {
            // 出力先を選択する（前回パスを記憶）
            // Choose the output directory, remembering the previous path
            var playerPrefsKey = OutputPathKey + buildTarget;
            var outputDirectory = EditorUtility.OpenFolderPanel("Build", PlayerPrefs.GetString(playerPrefsKey, ""), "");
            if (outputDirectory == string.Empty) return;
            PlayerPrefs.SetString(playerPrefsKey, outputDirectory);
            PlayerPrefs.Save();

            ReportOutcome(BuildPipeline.Execute(ReleaseLocalBuildCli.CreateRequest(buildTarget, outputDirectory)), outputDirectory);
        }

        // 失敗した成果物をFinderで開いて成功に見せない
        // Never reveal a failed artifact as if the build had succeeded
        private static void ReportOutcome(PlayerBuildOutcome outcome, string outputDirectory)
        {
            switch (outcome)
            {
                case PlayerBuildOutcome.Succeeded:
                    EditorUtility.RevealInFinder(outputDirectory);
                    break;
                case PlayerBuildOutcome.AddressablesBuildFailed:
                    EditorUtility.DisplayDialog("Build Failed", "Addressablesのビルドに失敗しました。Consoleのエラーを確認してください。", "OK");
                    break;
                case PlayerBuildOutcome.PlayerBuildFailed:
                    EditorUtility.DisplayDialog("Build Failed", "Playerのビルドに失敗しました。Consoleのエラーを確認してください。", "OK");
                    break;
            }
        }
```

既存 `BuildInteractive` 末尾の `switch (outcome) { ... }` ブロックは削除し、`ReportOutcome(outcome, outputDirectory);` の1行に置き換える（同じ分岐を2箇所に持たない）。

- [ ] **Step 3: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

- [ ] **Step 4: Windows ビルドモジュールの導入を確認する**

Run: `ls /Applications/Unity/Hub/Editor/6000.3.8f1/PlaybackEngines`
Expected: `WindowsStandaloneSupport` が出力に含まれる。無ければ Unity Hub で Windows Build Support (Mono) を追加してから次へ進む。

- [ ] **Step 5: batchmode 入口の fail-closed を確認する**

Run（worktree のパスで実行。`MOORESTECH_BUILD_OUTPUT` を意図的に外す）:
```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath ./moorestech_client \
  -executeMethod Client.Editor.Build.ReleaseLocalBuildCli.WindowsReleaseLocalBuild \
  -logFile /tmp/moores-release-cli-noenv.log; echo "exit=$?"
grep -c "MOORESTECH_BUILD_OUTPUT が未設定" /tmp/moores-release-cli-noenv.log
```
Expected: `exit=2` かつ grep が 1 以上。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Editor/Build/BuildMenu.cs moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs.meta
git commit -m "feat(build): Windows Release配布ビルドのGUIメニューとbatchmode入口を追加"
```

---

### Task 2: `FfmpegRuntimeBundler`（win-x64 の ffmpeg.exe と LICENSE を同梱）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Editor/Build/FfmpegRuntimeBundler.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/Build/BuildPipeline.cs:63-73`（成功後の同梱ブロックに1行追加）
- Modify: `.moorestech-external-revisions.json`（`moorestech_client_private` のピン更新）
- 別リポジトリ: `moorestech_client/Assets/PersonalAssets/moorestech-client-private/ffmpeg/win-x64/ffmpeg.exe` と `.../win-x64/LICENSE`（追加・別 PR）

**Interfaces:**
- Consumes: `DirectoryProcessor`（既存。`Editor/Build/` 隣接の同一アセンブリ）、`PlayerBuildRequest.IsStrictBundling`
- Produces: `public static class FfmpegRuntimeBundler { public const string BundledExecutableName = "ffmpeg.exe"; public const string BundledLicenseName = "ffmpeg-LICENSE.txt"; public static string SourceDirectory { get; } public static void Bundle(BuildTarget buildTarget, string playerOutputPath, bool isStrict); }`

- [ ] **Step 1: 非公開アセットに ffmpeg を置く（別リポジトリ）**

`moorestech-client-private` は Git LFS を使うサブリポジトリ。以下を実行し、PR を作って push 済みコミットを得る。

```bash
WORK="$(mktemp -d)"
curl -L -o "$WORK/ffmpeg.zip" https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip
unzip -q "$WORK/ffmpeg.zip" -d "$WORK"
EXTRACTED="$(find "$WORK" -maxdepth 1 -type d -name 'ffmpeg-*' | head -n 1)"

cd moorestech_client/Assets/PersonalAssets/moorestech-client-private
git switch -c feat/ffmpeg-win-x64
mkdir -p ffmpeg/win-x64
cp "$EXTRACTED/bin/ffmpeg.exe" ffmpeg/win-x64/ffmpeg.exe
cp "$EXTRACTED/LICENSE"        ffmpeg/win-x64/LICENSE
git lfs track "ffmpeg/win-x64/ffmpeg.exe"
git add .gitattributes ffmpeg/win-x64
git commit -m "feat(ffmpeg): Windows配布ビルド同梱用の ffmpeg win-x64 を追加"
git push -u origin feat/ffmpeg-win-x64
gh pr create --fill
```

Expected: PR が作成され、`git rev-parse HEAD` が push 済みコミットを指す。この値を Step 6 で `.moorestech-external-revisions.json` に書く。

- [ ] **Step 2: `FfmpegRuntimeBundler` を作る**

`moorestech_client/Assets/Scripts/Editor/Build/FfmpegRuntimeBundler.cs`:
```csharp
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// バグ報告の録画組み立てに使う ffmpeg を Windows 成果物へ同梱する
    /// Bundles the ffmpeg the bug-report video assembly uses into the Windows artifact
    /// ライセンス上、実行ファイルと一緒に配布物へライセンス文を並べる必要がある
    /// The license requires the license text to ship alongside the executable
    /// </summary>
    public static class FfmpegRuntimeBundler
    {
        public const string BundledExecutableName = "ffmpeg.exe";
        public const string BundledLicenseName = "ffmpeg-LICENSE.txt";
        private const string SourceExecutableName = "ffmpeg.exe";
        private const string SourceLicenseName = "LICENSE";

        // 正本は非公開アセットリポジトリの ffmpeg/win-x64
        // The source of truth is ffmpeg/win-x64 in the private asset repository
        public static string SourceDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "PersonalAssets", "moorestech-client-private", "ffmpeg", "win-x64"));

        public static void Bundle(BuildTarget buildTarget, string playerOutputPath, bool isStrict)
        {
            // 同梱先はCEFランタイムと同じ Plugins/x86_64。実行時の探索位置を1箇所に揃える
            // Ships into Plugins/x86_64 next to the CEF runtime so runtime lookup has a single location
            if (buildTarget != BuildTarget.StandaloneWindows64)
            {
                Debug.Log($"[FfmpegRuntimeBundler] skipped for {buildTarget}");
                return;
            }

            var sourceExecutable = Path.Combine(SourceDirectory, SourceExecutableName);
            var sourceLicense = Path.Combine(SourceDirectory, SourceLicenseName);
            if (!File.Exists(sourceExecutable))
            {
                Fail($"ffmpeg executable is missing: {sourceExecutable}");
                return;
            }
            if (!File.Exists(sourceLicense))
            {
                Fail($"ffmpeg LICENSE is missing: {sourceLicense}");
                return;
            }

            var dataDirectory = Path.Combine(Path.GetDirectoryName(playerOutputPath), Path.GetFileNameWithoutExtension(playerOutputPath) + "_Data");
            var destinationDirectory = Path.Combine(dataDirectory, "Plugins", "x86_64");
            if (!Directory.Exists(destinationDirectory))
            {
                Fail($"Plugins/x86_64 not found in build output: {destinationDirectory}");
                return;
            }

            File.Copy(sourceExecutable, Path.Combine(destinationDirectory, BundledExecutableName), true);
            File.Copy(sourceLicense, Path.Combine(destinationDirectory, BundledLicenseName), true);
            Debug.Log($"[FfmpegRuntimeBundler] bundled ffmpeg at {destinationDirectory}");

            #region Internal

            void Fail(string message)
            {
                // strict時は録画の組み立てができない成果物を配布しないため即失敗、CI互換時は警告のみ
                // Strict mode refuses to ship an artifact that cannot assemble video; CI-compatible mode only warns
                if (isStrict) throw new BuildFailedException("[FfmpegRuntimeBundler] " + message);
                Debug.LogWarning("[FfmpegRuntimeBundler] " + message);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: `BuildPipeline` から呼ぶ**

`BuildPipeline.cs` の成功後ブロック、`CefRuntimeBundler.Bundle(...)` の次行に追加する:
```csharp
                CefRuntimeBundler.Bundle(request.Target, report.summary.outputPath, request.IsStrictBundling);
                FfmpegRuntimeBundler.Bundle(request.Target, report.summary.outputPath, request.IsStrictBundling);
```

- [ ] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

- [ ] **Step 5: strict の失敗を実地で確認する**

Run（ソースを一時的に退避して strict ビルドを走らせる）:
```bash
mv moorestech_client/Assets/PersonalAssets/moorestech-client-private/ffmpeg/win-x64/LICENSE /tmp/ffmpeg-LICENSE.bak
MOORESTECH_BUILD_OUTPUT=/tmp/moores-ffmpeg-fail \
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath ./moorestech_client \
  -executeMethod Client.Editor.Build.ReleaseLocalBuildCli.WindowsReleaseLocalBuild \
  -logFile /tmp/moores-ffmpeg-fail.log; echo "exit=$?"
grep -c "ffmpeg LICENSE is missing" /tmp/moores-ffmpeg-fail.log
mv /tmp/ffmpeg-LICENSE.bak moorestech_client/Assets/PersonalAssets/moorestech-client-private/ffmpeg/win-x64/LICENSE
```
Expected: `exit=1` かつ grep が 1 以上。

- [ ] **Step 6: ピンを更新する**

`.moorestech-external-revisions.json` の `moorestech_client_private.commitHash` を Step 1 で得た push 済みコミットへ書き換える。Unity は作業ツリーのこのファイルを実チェックアウト値へ書き戻すため、**`git add` は明示パス指定で行い、`git add -A` を使わない**。

```bash
git add .moorestech-external-revisions.json
git diff --cached .moorestech-external-revisions.json   # 意図した1行だけが変わっていること
```

- [ ] **Step 7: 成功系のビルドと同梱結果を確認する**

Run:
```bash
MOORESTECH_BUILD_OUTPUT=/tmp/moores-win-build \
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath ./moorestech_client \
  -executeMethod Client.Editor.Build.ReleaseLocalBuildCli.WindowsReleaseLocalBuild \
  -logFile /tmp/moores-win-build.log; echo "exit=$?"
ls /tmp/moores-win-build/moorestech.exe /tmp/moores-win-build/game/mods
ls /tmp/moores-win-build/moorestech_Data/Plugins/x86_64/ffmpeg.exe /tmp/moores-win-build/moorestech_Data/Plugins/x86_64/ffmpeg-LICENSE.txt
```
Expected: `exit=0` で全ファイルが存在する。

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Editor/Build/FfmpegRuntimeBundler.cs moorestech_client/Assets/Scripts/Editor/Build/FfmpegRuntimeBundler.cs.meta moorestech_client/Assets/Scripts/Editor/Build/BuildPipeline.cs .moorestech-external-revisions.json
git commit -m "feat(build): Windows配布ビルドに ffmpeg.exe とLICENSEを同梱する"
```

---

### Task 3: `BuildInfo` と共有契約 §1 の焼き込み

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfo.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfoComposer.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/Build/BuildInfoWriter.cs`（plan B が作ったもの。§1 の形へ拡張）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/BuildInfoComposerTest.cs`

**Interfaces:**
- Consumes: plan B の `RepositoryStateProbe.ProbeGit(string)` / `RepositoryProbeResult` / `RepositoryState` / `RepositoryStateProbe.RepositoryRoot` / `MasterDataRoot` / `BuildInfoFileName`
- Produces:
  - `public sealed class BuildInfo { public string Commit; public string Branch; public string MasterDataCommit; public bool Dirty; public string SteamBuildLabel; public string BuiltAt; public string Target; public static BuildInfo FromJson(string json); public string ToJson(); }`
  - `public static class BuildInfoComposer { public const string SteamBuildLabelEnvKey = "MOORESTECH_STEAM_BUILD_LABEL"; public static string ReadPinnedMasterDataCommit(string repositoryRoot); public static BuildInfo Compose(RepositoryProbeResult repo, RepositoryProbeResult masterData, string pinnedMasterDataCommit, string steamBuildLabel, DateTimeOffset builtAt, string target, out string mismatchReason); }`
  - `BuildInfoWriter : IPreprocessBuildWithReport`（`Assets/StreamingAssets/build-info.json` へ §1 の JSON を書く。ピン不一致は `BuildFailedException`）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/BugReport/BuildInfoComposerTest.cs`:
```csharp
using System;
using System.IO;
using Client.Game.InGame.BugReport;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class BuildInfoComposerTest
    {
        private const string RepoCommit = "1111111111111111111111111111111111111111";
        private const string MasterCommit = "2222222222222222222222222222222222222222";

        [Test]
        public void 共有契約の全キーを持つJSONを組みラウンドトリップできる()
        {
            var info = BuildInfoComposer.Compose(
                Probe(RepoCommit, "master", false),
                Probe(MasterCommit, "HEAD", false),
                MasterCommit,
                "playtest-20260913-1730",
                new DateTimeOffset(2026, 9, 13, 17, 30, 0, TimeSpan.FromHours(9)),
                "StandaloneWindows64",
                out var mismatchReason);

            Assert.IsEmpty(mismatchReason);
            var roundTripped = BuildInfo.FromJson(info.ToJson());
            Assert.AreEqual(RepoCommit, roundTripped.Commit);
            Assert.AreEqual("master", roundTripped.Branch);
            Assert.AreEqual(MasterCommit, roundTripped.MasterDataCommit);
            Assert.IsFalse(roundTripped.Dirty);
            Assert.AreEqual("playtest-20260913-1730", roundTripped.SteamBuildLabel);
            Assert.AreEqual("2026-09-13T17:30:00+09:00", roundTripped.BuiltAt);
            Assert.AreEqual("StandaloneWindows64", roundTripped.Target);
        }

        [Test]
        public void ピンと実HEADが食い違えば理由が返る()
        {
            BuildInfoComposer.Compose(
                Probe(RepoCommit, "master", false),
                Probe(MasterCommit, "HEAD", false),
                "3333333333333333333333333333333333333333",
                "",
                DateTimeOffset.UtcNow,
                "StandaloneWindows64",
                out var mismatchReason);

            StringAssert.Contains(MasterCommit, mismatchReason);
            StringAssert.Contains("3333333333333333333333333333333333333333", mismatchReason);
        }

        [Test]
        public void ピンファイルからmasterDataのコミットを読める()
        {
            var repositoryRoot = Path.Combine(Path.GetTempPath(), "moores-buildinfo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repositoryRoot);
            File.WriteAllText(Path.Combine(repositoryRoot, ".moorestech-external-revisions.json"),
                "{\"repositories\":[{\"key\":\"moorestech_master\",\"relativePath\":\"../moorestech_master\",\"commitHash\":\"" + MasterCommit + "\"}]}");

            Assert.AreEqual(MasterCommit, BuildInfoComposer.ReadPinnedMasterDataCommit(repositoryRoot));
            Directory.Delete(repositoryRoot, true);
        }

        private static RepositoryProbeResult Probe(string commit, string branch, bool dirty)
        {
            return new RepositoryProbeResult { State = new RepositoryState { Commit = commit, Branch = branch, Dirty = dirty } };
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildInfoComposer` / `BuildInfo` が未定義でコンパイルエラー

- [ ] **Step 3: `BuildInfo` を実装する**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfo.cs`:
```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.BugReport
{
    /// <summary>
    /// 配布ビルドに焼き込まれた出所情報。バグ報告と進行記録がどのビルドから来たかを示す
    /// The origin baked into a distribution build; tells which build a report or progress record came from
    /// </summary>
    public sealed class BuildInfo
    {
        public string Commit = "";
        public string Branch = "";
        public string MasterDataCommit = "";
        public bool Dirty;
        public string SteamBuildLabel = "";
        public string BuiltAt = "";
        public string Target = "";

        public static BuildInfo FromJson(string json)
        {
            // 外部ファイルのパース境界。欠損キーは既定値のままにする
            // Parsing boundary for an external file; missing keys keep their defaults
            var parsed = JObject.Parse(json);
            return new BuildInfo
            {
                Commit = (string)parsed["commit"] ?? "",
                Branch = (string)parsed["branch"] ?? "",
                MasterDataCommit = (string)parsed["masterDataCommit"] ?? "",
                Dirty = (bool?)parsed["dirty"] ?? false,
                SteamBuildLabel = (string)parsed["steamBuildLabel"] ?? "",
                BuiltAt = (string)parsed["builtAt"] ?? "",
                Target = (string)parsed["target"] ?? "",
            };
        }

        public string ToJson()
        {
            var json = new JObject
            {
                ["commit"] = Commit,
                ["branch"] = Branch,
                ["masterDataCommit"] = MasterDataCommit,
                ["dirty"] = Dirty,
                ["steamBuildLabel"] = SteamBuildLabel,
                ["builtAt"] = BuiltAt,
                ["target"] = Target,
            };
            return json.ToString(Formatting.Indented);
        }
    }
}
```

- [ ] **Step 4: `BuildInfoComposer` を実装する**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfoComposer.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.BugReport
{
    /// <summary>
    /// ビルド時のリポジトリ状態から build-info.json の中身を組む
    /// Composes the contents of build-info.json from the repository state at build time
    /// </summary>
    public static class BuildInfoComposer
    {
        public const string SteamBuildLabelEnvKey = "MOORESTECH_STEAM_BUILD_LABEL";
        private const string RevisionsFileName = ".moorestech-external-revisions.json";
        private const string MasterDataKey = "moorestech_master";

        // ピンの値は配布物の再現性の根拠なので、読めなければ空を返し呼び出し側に失敗させる
        // The pin is the reproducibility evidence for the artifact, so return empty and let the caller fail
        public static string ReadPinnedMasterDataCommit(string repositoryRoot)
        {
            var path = Path.Combine(repositoryRoot, RevisionsFileName);
            if (!File.Exists(path)) return "";

            // リポジトリ外のJSONを読むパース境界
            // Parsing boundary for JSON owned outside this assembly
            var repositories = (JArray)JObject.Parse(File.ReadAllText(path))["repositories"];
            var entry = repositories?.FirstOrDefault(r => (string)r["key"] == MasterDataKey);
            return entry == null ? "" : (string)entry["commitHash"] ?? "";
        }

        public static BuildInfo Compose(
            RepositoryProbeResult repo,
            RepositoryProbeResult masterData,
            string pinnedMasterDataCommit,
            string steamBuildLabel,
            DateTimeOffset builtAt,
            string target,
            out string mismatchReason)
        {
            var masterDataCommit = masterData.State?.Commit ?? "";

            // ピンと実チェックアウトがずれた成果物は「どのマスタで焼いたか」が嘘になるため理由を返す
            // A drifted pin makes the artifact lie about its master data, so hand the reason back
            mismatchReason = masterDataCommit == pinnedMasterDataCommit
                ? ""
                : $"master data のピン({pinnedMasterDataCommit})と実HEAD({masterDataCommit})が一致しません";

            return new BuildInfo
            {
                Commit = repo.State?.Commit ?? "",
                Branch = repo.State?.Branch ?? "",
                MasterDataCommit = masterDataCommit,
                Dirty = repo.State?.Dirty ?? false,
                SteamBuildLabel = steamBuildLabel ?? "",
                BuiltAt = builtAt.ToString("yyyy-MM-dd'T'HH:mm:ssK"),
                Target = target,
            };
        }
    }
}
```

- [ ] **Step 5: `BuildInfoWriter` を §1 の形へ拡張する**

`moorestech_client/Assets/Scripts/Editor/Build/BuildInfoWriter.cs` を丸ごと次に置き換える:
```csharp
using System;
using System.IO;
using Client.Game.InGame.BugReport;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// ビルドに出所（コミット・master dataピン・Steamビルド識別）を焼き込む
    /// Bakes the origin (commit, master-data pin, Steam build label) into the build
    /// </summary>
    public class BuildInfoWriter : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            var repo = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.RepositoryRoot);
            var masterData = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.MasterDataRoot);
            if (repo.Error != null) throw new BuildFailedException("[BuildInfoWriter] " + repo.Error);
            if (masterData.Error != null) throw new BuildFailedException("[BuildInfoWriter] " + masterData.Error);

            var pinned = BuildInfoComposer.ReadPinnedMasterDataCommit(RepositoryStateProbe.RepositoryRoot);
            var label = Environment.GetEnvironmentVariable(BuildInfoComposer.SteamBuildLabelEnvKey) ?? "";
            var info = BuildInfoComposer.Compose(
                repo, masterData, pinned, label, DateTimeOffset.Now, report.summary.platform.ToString(), out var mismatchReason);

            // ピンずれの成果物はテスターの報告の出所を偽るため、配布物を作らせない
            // A drifted artifact would misreport a tester's origin, so refuse to produce one
            if (mismatchReason.Length != 0) throw new BuildFailedException("[BuildInfoWriter] " + mismatchReason);

            var path = Path.Combine(Application.streamingAssetsPath, RepositoryStateProbe.BuildInfoFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, info.ToJson());
            Debug.Log($"[BuildInfoWriter] build-info.json commit:{info.Commit} masterData:{info.MasterDataCommit} label:{info.SteamBuildLabel}");
        }
    }
}
```

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.BugReport\.BuildInfoComposerTest"`
Expected: 3件 PASS

- [ ] **Step 7: 実ビルドで焼き込みを確認する**

Run:
```bash
MOORESTECH_BUILD_OUTPUT=/tmp/moores-win-build MOORESTECH_STEAM_BUILD_LABEL=playtest-local-check \
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath ./moorestech_client \
  -executeMethod Client.Editor.Build.ReleaseLocalBuildCli.WindowsReleaseLocalBuild \
  -logFile /tmp/moores-win-build.log; echo "exit=$?"
cat /tmp/moores-win-build/moorestech_Data/StreamingAssets/build-info.json
```
Expected: `exit=0`。JSON に §1 の7キーが揃い、`steamBuildLabel` が `playtest-local-check`、`target` が `StandaloneWindows64`。

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfo.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfo.cs.meta moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfoComposer.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfoComposer.cs.meta moorestech_client/Assets/Scripts/Editor/Build/BuildInfoWriter.cs moorestech_client/Assets/Scripts/Client.Tests/BugReport/BuildInfoComposerTest.cs moorestech_client/Assets/Scripts/Client.Tests/BugReport/BuildInfoComposerTest.cs.meta
git commit -m "feat(build): build-info.jsonを共有契約の形へ拡張しmaster dataピンのずれで失敗させる"
```

---

### Task 4: 配布ビルド向け通しシナリオ `playtest-smoke`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeSettings.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeBootstrap.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeRunner.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeResult.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeResultWriter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`StandalonePlaytestSmokeRunner` を `IStartable` として登録）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestSmoke/StandalonePlaytestSmokeSettingsTest.cs`

**Interfaces:**
- Consumes: `LocalGameLauncher.StartLocalGame()`、`GameSystemPaths.DeleteDefaultWorldDirectory()`、`Client.Game.InGame.Context.ClientContext.VanillaApi.Response.Save(CancellationToken)`、`Client.Network.API.InitialHandshakeResponse.Challenges`、plan B の `BugReportCaptureSession`（`void BeginOnPauseMenu()` / `IReadOnlyReactiveProperty<BugReportCaptureStatus> Status`（`HasSession`・`CapturePending`）/ `BugReportCapturedData TakeCapturedData()`）と `BugReportBundleWriter.WriteAsync(BugReportCapturedData, string)`（`BugReportBundleResult.BundleDirectory` を返す）
- Produces:
  - `public sealed class StandalonePlaytestSmokeSettings { public const string Marker = "--playtestSmoke"; public readonly string Phase; public readonly string ResultDirectory; public static bool HasMarker(IReadOnlyList<string> args); public static bool TryParse(IReadOnlyList<string> args, out StandalonePlaytestSmokeSettings settings, out string error); public const string PhaseOne = "phase1"; public const string PhaseTwo = "phase2"; }`
  - `public static class StandalonePlaytestSmokeBootstrap { public static bool IsActive { get; } public static StandalonePlaytestSmokeSettings Settings { get; } }`
  - `public sealed class StandalonePlaytestSmokeRunner : IStartable`
  - `public sealed class StandalonePlaytestSmokeResult { public string phase; public bool success; public string message; public string reportBundleDirectory; public StandalonePlaytestSmokeStep[] steps; }`、`public sealed class StandalonePlaytestSmokeStep { public string name; public bool success; public string message; public float elapsedSeconds; }`
  - `public static class StandalonePlaytestSmokeResultWriter { public static void Write(string resultDirectory, StandalonePlaytestSmokeResult result); }`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaytestSmoke/StandalonePlaytestSmokeSettingsTest.cs`:
```csharp
using Client.Starter.PlaytestSmoke;
using NUnit.Framework;

namespace Client.Tests.PlaytestSmoke
{
    public class StandalonePlaytestSmokeSettingsTest
    {
        [Test]
        public void 完全な引数を受理する()
        {
            var args = new[] { "moorestech.exe", "--playtestSmoke", "--smokePhase", "phase1", "--smokeResultDirectory", "C:/smoke" };
            Assert.IsTrue(StandalonePlaytestSmokeSettings.TryParse(args, out var settings, out var error), error);
            Assert.AreEqual(StandalonePlaytestSmokeSettings.PhaseOne, settings.Phase);
            Assert.AreEqual("C:/smoke", settings.ResultDirectory);
        }

        [Test]
        public void マーカーが無ければ起動しない()
        {
            Assert.IsFalse(StandalonePlaytestSmokeSettings.HasMarker(new[] { "moorestech.exe" }));
        }

        [Test]
        public void 未知のphaseは理由付きで拒否する()
        {
            var args = new[] { "--playtestSmoke", "--smokePhase", "phase9", "--smokeResultDirectory", "C:/smoke" };
            Assert.IsFalse(StandalonePlaytestSmokeSettings.TryParse(args, out _, out var error));
            StringAssert.Contains("phase9", error);
        }

        [Test]
        public void 欠落と空値と重複を拒否する()
        {
            Assert.IsFalse(StandalonePlaytestSmokeSettings.TryParse(
                new[] { "--playtestSmoke", "--smokeResultDirectory", "C:/smoke" }, out _, out var missing));
            StringAssert.Contains("--smokePhase", missing);

            Assert.IsFalse(StandalonePlaytestSmokeSettings.TryParse(
                new[] { "--playtestSmoke", "--smokePhase", "--smokeResultDirectory", "C:/smoke" }, out _, out var empty));
            StringAssert.Contains("--smokePhase", empty);

            Assert.IsFalse(StandalonePlaytestSmokeSettings.TryParse(
                new[] { "--playtestSmoke", "--smokePhase", "phase1", "--smokePhase", "phase2", "--smokeResultDirectory", "C:/smoke" },
                out _, out var duplicated));
            StringAssert.Contains("exactly once", duplicated);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `Client.Starter.PlaytestSmoke` 未定義でコンパイルエラー

- [ ] **Step 3: `StandalonePlaytestSmokeSettings` を実装する**

`moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeSettings.cs`:
```csharp
using System.Collections.Generic;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 配布ビルドの通し検証を起動するコマンドライン引数（外部入力なので欠落・空・重複を拒否する）
    /// Command-line arguments launching the distribution smoke run; external input, so missing/empty/duplicate values are rejected
    /// </summary>
    public sealed class StandalonePlaytestSmokeSettings
    {
        public const string Marker = "--playtestSmoke";
        public const string PhaseOne = "phase1";
        public const string PhaseTwo = "phase2";
        private const string PhaseOption = "--smokePhase";
        private const string ResultDirectoryOption = "--smokeResultDirectory";

        public readonly string Phase;
        public readonly string ResultDirectory;

        private StandalonePlaytestSmokeSettings(string phase, string resultDirectory)
        {
            Phase = phase;
            ResultDirectory = resultDirectory;
        }

        public static bool HasMarker(IReadOnlyList<string> args)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] == Marker) return true;
            }
            return false;
        }

        public static bool TryParse(IReadOnlyList<string> args, out StandalonePlaytestSmokeSettings settings, out string error)
        {
            settings = null;
            if (!HasMarker(args))
            {
                error = $"{Marker} is required";
                return false;
            }
            if (!TryReadRequiredOption(args, PhaseOption, out var phase, out error)) return false;
            if (!TryReadRequiredOption(args, ResultDirectoryOption, out var resultDirectory, out error)) return false;

            // 未知のphaseは「何も検証しない成功」を作るため、明示的に拒否する
            // An unknown phase would fabricate a success that verified nothing, so refuse it outright
            if (phase != PhaseOne && phase != PhaseTwo)
            {
                error = $"{PhaseOption} must be {PhaseOne} or {PhaseTwo}, but was {phase}";
                return false;
            }

            settings = new StandalonePlaytestSmokeSettings(phase, resultDirectory);
            error = string.Empty;
            return true;
        }

        private static bool TryReadRequiredOption(IReadOnlyList<string> args, string option, out string value, out string error)
        {
            value = string.Empty;
            var matchCount = 0;
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] != option) continue;
                matchCount++;
                if (i + 1 < args.Count) value = args[i + 1];
            }

            if (matchCount == 0)
            {
                error = $"{option} is required";
                return false;
            }
            if (matchCount != 1)
            {
                error = $"{option} must be specified exactly once";
                return false;
            }
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--"))
            {
                error = $"{option} requires a value";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
```

- [ ] **Step 4: 結果型と書き出しを実装する**

`moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeResult.cs`:
```csharp
using System;

namespace Client.Starter.PlaytestSmoke
{
    [Serializable]
    public sealed class StandalonePlaytestSmokeStep
    {
        public string name;
        public bool success;
        public string message;
        public float elapsedSeconds;
    }

    [Serializable]
    public sealed class StandalonePlaytestSmokeResult
    {
        public string phase;
        public bool success;
        public string message;
        public string reportBundleDirectory;
        public StandalonePlaytestSmokeStep[] steps;
    }
}
```

`moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeResultWriter.cs`:
```csharp
using System.IO;
using UnityEngine;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 通し検証の結果を result.json として書く（Mac mini 側が回収して合否に使う）
    /// Writes the smoke result as result.json, which the Mac mini collects to decide pass/fail
    /// </summary>
    public static class StandalonePlaytestSmokeResultWriter
    {
        private const string ResultFileName = "result.json";

        public static void Write(string resultDirectory, StandalonePlaytestSmokeResult result)
        {
            Directory.CreateDirectory(resultDirectory);
            var path = Path.Combine(resultDirectory, ResultFileName);
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log($"[PlaytestSmoke] result written: {path} success={result.success}");
        }
    }
}
```

- [ ] **Step 5: 起動フックを実装する**

`moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeBootstrap.cs`:
```csharp
using System;
using Client.Common;
using Game.Paths;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 配布ビルドを引数で自動運転する。メインメニューを人手で押さずローカルゲームを開始する
    /// Drives a distribution build from arguments, starting the local game without a human pressing the menu
    /// 前例は Client.Starter/StandaloneQa/StandaloneTerrainQaBootstrap（同じ引数マーカー＋result.json＋Quitの型）
    /// The precedent is StandaloneTerrainQaBootstrap: the same marker-argument, result.json and Quit shape
    /// </summary>
    public static class StandalonePlaytestSmokeBootstrap
    {
        public static bool IsActive { get; private set; }
        public static StandalonePlaytestSmokeSettings Settings { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoStartIfSmokeRun()
        {
            // Editorは開発者のワールドを不可逆に消すため、配布ビルドでのみ動かす
            // The Editor would irreversibly wipe a developer's world, so this runs only in a player build
            if (Application.isEditor) return;

            var args = Environment.GetCommandLineArgs();
            if (!StandalonePlaytestSmokeSettings.HasMarker(args)) return;
            if (!StandalonePlaytestSmokeSettings.TryParse(args, out var settings, out var error))
            {
                Debug.LogError($"[PlaytestSmoke] {error}");
                Application.Quit(2);
                return;
            }

            // メインメニュー以外（初期化中・ゲーム中）で二重に開始しない
            // Never start twice from anywhere but the main menu
            if (SceneManager.GetActiveScene().name != SceneConstant.MainMenuSceneName) return;

            Settings = settings;
            IsActive = true;

            // phase1だけ新規ワールドから始める。phase2は直前のphase1が残したワールドをロードする
            // Only phase1 starts from a fresh world; phase2 loads the world phase1 left behind
            if (settings.Phase == StandalonePlaytestSmokeSettings.PhaseOne)
                GameSystemPaths.DeleteDefaultWorldDirectory();

            Debug.Log($"[PlaytestSmoke] starting {settings.Phase} result:{settings.ResultDirectory}");
            LocalGameLauncher.StartLocalGame();
        }
    }
}
```

- [ ] **Step 6: 本体ランナーを実装する**

`moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeRunner.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 通し検証の本体。phase1=新規ワールド/チュートリアル/セーブ、phase2=ロード/報告送信/アップロード確認
    /// The smoke run itself: phase1 is fresh world, tutorial and save; phase2 is load, report and upload confirmation
    /// </summary>
    public sealed class StandalonePlaytestSmokeRunner : IStartable
    {
        private const string SmokeDescription = "smoke";
        private const float StepTimeoutSeconds = 180f;
        private const float UploadTimeoutSeconds = 300f;

        private readonly InitialHandshakeResponse _initialHandshakeResponse;
        private readonly BugReportCaptureSession _captureSession;
        private readonly BugReportBundleWriter _bundleWriter;
        private readonly List<StandalonePlaytestSmokeStep> _steps = new();

        public StandalonePlaytestSmokeRunner(
            InitialHandshakeResponse initialHandshakeResponse,
            BugReportCaptureSession captureSession,
            BugReportBundleWriter bundleWriter)
        {
            _initialHandshakeResponse = initialHandshakeResponse;
            _captureSession = captureSession;
            _bundleWriter = bundleWriter;
        }

        public void Start()
        {
            if (!StandalonePlaytestSmokeBootstrap.IsActive) return;
            RunAsync(StandalonePlaytestSmokeBootstrap.Settings).Forget();
        }

        private async UniTask RunAsync(StandalonePlaytestSmokeSettings settings)
        {
            var reportBundleDirectory = "";
            var failure = "";

            if (settings.Phase == StandalonePlaytestSmokeSettings.PhaseOne)
            {
                failure = await RunStep("tutorial-started", VerifyTutorialStarted);
                if (failure.Length == 0) failure = await RunStep("save", RequestSave);
            }
            else
            {
                failure = await RunStep("world-loaded", VerifyWorldLoaded);
                if (failure.Length == 0) failure = await RunStep("report-written", async () =>
                {
                    reportBundleDirectory = await WriteSmokeReport();
                    return reportBundleDirectory.Length == 0 ? "報告バンドルを書き出せませんでした" : "";
                });
                if (failure.Length == 0) failure = await RunStep("report-uploaded", () => WaitUploaded(reportBundleDirectory));
            }

            var result = new StandalonePlaytestSmokeResult
            {
                phase = settings.Phase,
                success = failure.Length == 0,
                message = failure,
                reportBundleDirectory = reportBundleDirectory,
                steps = _steps.ToArray(),
            };
            StandalonePlaytestSmokeResultWriter.Write(settings.ResultDirectory, result);
            Application.Quit(result.success ? 0 : 1);
        }

        #region Internal

        // 各ステップの所要時間と失敗理由を必ず残す（無音の失敗を作らない）
        // Always record each step's duration and failure reason so no failure is silent
        private async UniTask<string> RunStep(string name, Func<UniTask<string>> body)
        {
            var startedAt = Time.realtimeSinceStartup;
            var message = await body();
            var step = new StandalonePlaytestSmokeStep
            {
                name = name,
                success = message.Length == 0,
                message = message,
                elapsedSeconds = Time.realtimeSinceStartup - startedAt,
            };
            _steps.Add(step);
            if (!step.success) Debug.LogError($"[PlaytestSmoke] step {name} failed: {message}");
            return message;
        }

        private UniTask<string> VerifyTutorialStarted()
        {
            // 初期ハンドシェイクに現在チャレンジが乗っていればチュートリアルは開始している
            // The tutorial has started when the initial handshake carries at least one current challenge
            var challengeCount = 0;
            foreach (var challenge in _initialHandshakeResponse.Challenges) challengeCount += challenge.CurrentChallenges.Count;
            return UniTask.FromResult(0 < challengeCount ? "" : "初期チャレンジが0件でチュートリアルが開始していません");
        }

        private async UniTask<string> RequestSave()
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(StepTimeoutSeconds));
            await ClientContext.VanillaApi.Response.Save(cancellation.Token);
            return "";
        }

        private UniTask<string> VerifyWorldLoaded()
        {
            // phase1が置いたセーブから起動できたことを、初期ハンドシェイクの受領そのもので示す
            // Receiving the initial handshake is itself the evidence that the phase1 save booted
            return UniTask.FromResult(_initialHandshakeResponse == null ? "初期ハンドシェイクを受け取れませんでした" : "");
        }

        private async UniTask<string> WriteSmokeReport()
        {
            // ポーズメニューが押されたときと同じ確保を開始し、確保完了まで期限付きで待つ
            // Begin the same capture the pause menu starts, then wait for it with a deadline
            _captureSession.BeginOnPauseMenu();
            var deadline = Time.realtimeSinceStartup + StepTimeoutSeconds;
            while (_captureSession.Status.Value.CapturePending && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }
            if (_captureSession.Status.Value.CapturePending)
            {
                Debug.LogError($"[PlaytestSmoke] 記録の確保が{StepTimeoutSeconds}秒以内に終わりませんでした");
                return "";
            }

            var captured = _captureSession.TakeCapturedData();
            if (captured == null)
            {
                Debug.LogError("[PlaytestSmoke] 確保セッションが無く報告を書き出せません");
                return "";
            }
            var written = await _bundleWriter.WriteAsync(captured, SmokeDescription);
            return written.BundleDirectory;
        }

        private async UniTask<string> WaitUploaded(string bundleDirectory)
        {
            // 受け口へのアップロード完了はUPLOADEDマーカーで観測する（共有契約 §2）
            // Upload completion is observed through the UPLOADED marker (shared contract §2)
            var markerPath = Path.Combine(bundleDirectory, "UPLOADED");
            var deadline = Time.realtimeSinceStartup + UploadTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (File.Exists(markerPath)) return "";
                await UniTask.Yield();
            }
            return $"{UploadTimeoutSeconds}秒以内にUPLOADEDが置かれませんでした: {markerPath}";
        }

        #endregion
    }
}
```

- [ ] **Step 7: DI へ登録する**

`moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs` の `Register` メソッド末尾に追加する（このファイルの `RegisterEntryPoint` は引数なしで呼ぶのが既存の形）:
```csharp
            // 配布ビルドの通し検証ランナー。マーカー引数が無ければ Start() で即 return する
            // The distribution smoke runner; without the marker argument it returns immediately in Start()
            builder.RegisterEntryPoint<StandalonePlaytestSmokeRunner>();
```
ファイル冒頭に `using Client.Starter.PlaytestSmoke;` を追加する。

- [ ] **Step 8: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaytestSmoke\."`
Expected: 4件 PASS

- [ ] **Step 9: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/PlaytestSmoke
git commit -m "feat(playtest): 配布ビルドを引数で自動運転する通し検証ランナーを追加"
```

---

### Task 5: Steam アップロード資材と `release-playtest.sh`

**Files:**
- Create: `scripts/playtest/steam/app_build_playtest.vdf`
- Create: `scripts/playtest/steam/depot_build_windows.vdf`
- Create: `scripts/playtest/release-playtest.sh`
- Create: `scripts/playtest/tests/release-playtest-test.sh`
- Create: `scripts/playtest/README.md`（Task 6 で検証機の節を追記する。ここでは Steam 側手順まで）

**Interfaces:**
- Consumes: Task 1 の `Client.Editor.Build.ReleaseLocalBuildCli.WindowsReleaseLocalBuild` と env `MOORESTECH_BUILD_OUTPUT`、Task 3 の env `MOORESTECH_STEAM_BUILD_LABEL`、`moores-wt new <branch> --from <commit> --no-editor`
- Produces:
  - `scripts/playtest/release-playtest.sh <commit>`。差し替え可能な env: `MOORES_WT_BIN`（既定 `moores-wt`）、`UNITY_BIN`、`STEAMCMD_BIN`、`VERIFY_SCRIPT`（既定は同ディレクトリの `verify-on-windows.sh`）、`PLAYTEST_RUN_ROOT`（既定 `$HOME/hermes-agent/data/services/playtest/runs`）
  - 必須 env（`~/hermes-agent/data/services/playtest/env.sh` から供給）: `MOORESTECH_STEAM_USER`、`MOORESTECH_STEAM_DEPOT_ID`
  - 生成物: `<run>/build/`（成果物）、`<run>/steam/app_build_playtest.vdf`・`depot_build_windows.vdf`（トークン差し込み済み）、`<run>/announce.md`

- [ ] **Step 1: 失敗するテストを書く**

`scripts/playtest/tests/release-playtest-test.sh`:
```bash
#!/bin/bash
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET="$SCRIPT_DIR/../release-playtest.sh"
FAILURES=0
COMMIT="93ddfdab3ffffffffffffffffffffffffffffffff"

fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }

make_sandbox() {
    SANDBOX="$(mktemp -d)"
    mkdir -p "$SANDBOX/bin" "$SANDBOX/runs"

    # 呼び出し順と引数を1ファイルへ記録するスタブ群（bash 3.2 で動く書き方に限る）
    # Stubs recording invocation order and arguments into one file (written for bash 3.2)
    cat >"$SANDBOX/bin/moores-wt" <<EOF
#!/bin/bash
echo "moores-wt \$*" >>"$SANDBOX/calls.log"
mkdir -p "$SANDBOX/wt/moorestech_client"
echo "$SANDBOX/wt"
EOF
    cat >"$SANDBOX/bin/unity" <<EOF
#!/bin/bash
echo "unity \$*" >>"$SANDBOX/calls.log"
[ "\${UNITY_EXIT:-0}" = "0" ] || exit "\${UNITY_EXIT}"
mkdir -p "\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets" "\$MOORESTECH_BUILD_OUTPUT/game/mods"
touch "\$MOORESTECH_BUILD_OUTPUT/moorestech.exe"
printf '{"commit":"%s","steamBuildLabel":"%s"}' "$COMMIT" "\$MOORESTECH_STEAM_BUILD_LABEL" \
  >"\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets/build-info.json"
EOF
    cat >"$SANDBOX/bin/steamcmd" <<EOF
#!/bin/bash
echo "steamcmd \$*" >>"$SANDBOX/calls.log"
exit "\${STEAMCMD_EXIT:-0}"
EOF
    cat >"$SANDBOX/bin/verify" <<EOF
#!/bin/bash
echo "verify \$*" >>"$SANDBOX/calls.log"
exit "\${VERIFY_EXIT:-0}"
EOF
    chmod +x "$SANDBOX/bin/moores-wt" "$SANDBOX/bin/unity" "$SANDBOX/bin/steamcmd" "$SANDBOX/bin/verify"
}

run_target() {
    ( cd "$SANDBOX" && \
      MOORESTECH_STEAM_USER="${MOORESTECH_STEAM_USER-steamuser}" \
      MOORESTECH_STEAM_DEPOT_ID="${MOORESTECH_STEAM_DEPOT_ID-1958161}" \
      MOORES_WT_BIN="$SANDBOX/bin/moores-wt" UNITY_BIN="$SANDBOX/bin/unity" \
      STEAMCMD_BIN="$SANDBOX/bin/steamcmd" VERIFY_SCRIPT="$SANDBOX/bin/verify" \
      PLAYTEST_RUN_ROOT="$SANDBOX/runs" \
      UNITY_EXIT="${UNITY_EXIT-0}" STEAMCMD_EXIT="${STEAMCMD_EXIT-0}" VERIFY_EXIT="${VERIFY_EXIT-0}" \
      bash "$TARGET" "$COMMIT" 2>&1 )
}

# 成功系: worktree→unity→steamcmd→verify の順に呼ばれ、告知テキストが出る
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
ORDER=$(awk '{print $1}' "$SANDBOX/calls.log" | tr '\n' ' ')
[ "$ORDER" = "moores-wt unity steamcmd verify " ] || fail "call order was: $ORDER"
grep -q "run_app_build" "$SANDBOX/calls.log" || fail "steamcmd was not asked to run_app_build"
ls "$SANDBOX"/runs/*/announce.md >/dev/null 2>&1 || fail "announce.md was not written"
grep -q "__DEPOT_ID__" "$SANDBOX"/runs/*/steam/*.vdf && fail "vdf still contains a raw token"
grep -q "1958161" "$SANDBOX"/runs/*/steam/depot_build_windows.vdf || fail "depot id was not substituted"

# 必須envの欠落はビルド前に落ちる
make_sandbox
OUTPUT=$(MOORESTECH_STEAM_DEPOT_ID="" run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "missing depot id did not fail"
[ ! -f "$SANDBOX/calls.log" ] || fail "missing env reached the build"
case "$OUTPUT" in *MOORESTECH_STEAM_DEPOT_ID*) ;; *) fail "missing env did not name the variable";; esac

# ビルド失敗ならsteamcmdへ進まない
make_sandbox
OUTPUT=$(UNITY_EXIT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "build failure did not fail the run"
grep -q "^steamcmd" "$SANDBOX/calls.log" && fail "steamcmd ran after a failed build"

# 検証機の通し検証が落ちたら告知テキストを書かない
make_sandbox
OUTPUT=$(VERIFY_EXIT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "verification failure did not fail the run"
ls "$SANDBOX"/runs/*/announce.md >/dev/null 2>&1 && fail "announce.md was written for a failed verification"

if [ "$FAILURES" -ne 0 ]; then
    echo "FAILED: $FAILURES contract checks"
    exit 1
fi
echo "PASS: release-playtest contract"
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `bash scripts/playtest/tests/release-playtest-test.sh`
Expected: `release-playtest.sh` が存在せず非0終了

- [ ] **Step 3: vdf を置く**

`scripts/playtest/steam/app_build_playtest.vdf`:
```
"appbuild"
{
	"appid" "1958160"
	"desc" "moorestech playtest __BUILD_LABEL__"
	"buildoutput" "__RUN_DIR__/steam/output"
	"contentroot" "__CONTENT_ROOT__"
	"setlive" "playtest"
	"preview" "0"

	"depots"
	{
		"__DEPOT_ID__" "__RUN_DIR__/steam/depot_build_windows.vdf"
	}
}
```

`scripts/playtest/steam/depot_build_windows.vdf`:
```
"DepotBuild"
{
	"DepotID" "__DEPOT_ID__"
	"contentroot" "__CONTENT_ROOT__"

	"FileMapping"
	{
		"LocalPath" "*"
		"DepotPath" "."
		"recursive" "1"
	}

	"FileExclusion" "*.pdb"
	"FileExclusion" "**/*_BurstDebugInformation_DoNotShip/*"
	"FileExclusion" "**/*_BackUpThisFolder_ButDontShipItWithYourGame/*"
}
```

- [ ] **Step 4: `release-playtest.sh` を実装する**

`scripts/playtest/release-playtest.sh`:
```bash
#!/bin/bash
# 指定コミットからWindows配布ビルドを焼き、Steamのplaytestブランチへ上げ、検証機で通し検証する
# Bakes the Windows distribution build from a commit, ships it to the Steam playtest branch and verifies it on the check machine
#
# usage: release-playtest.sh <commit>
# 資格情報は ~/hermes-agent/data/services/playtest/env.sh から供給する（このスクリプトは値を出力しない）
# Credentials come from ~/hermes-agent/data/services/playtest/env.sh; this script never echoes their values
set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
COMMIT="${1:?usage: release-playtest.sh <commit>}"

MOORES_WT_BIN="${MOORES_WT_BIN:-moores-wt}"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity}"
STEAMCMD_BIN="${STEAMCMD_BIN:-steamcmd}"
VERIFY_SCRIPT="${VERIFY_SCRIPT:-$SCRIPT_DIR/verify-on-windows.sh}"
PLAYTEST_RUN_ROOT="${PLAYTEST_RUN_ROOT:-$HOME/hermes-agent/data/services/playtest/runs}"

# 必須envはビルド前に全部そろっているか見る（半端に焼いてから落ちない）
# Check every required env before building so a half-baked artifact never happens
missing=""
[ -n "${MOORESTECH_STEAM_USER:-}" ] || missing="$missing MOORESTECH_STEAM_USER"
[ -n "${MOORESTECH_STEAM_DEPOT_ID:-}" ] || missing="$missing MOORESTECH_STEAM_DEPOT_ID"
if [ -n "$missing" ]; then
    echo "ERROR: 必須の環境変数が未設定です:$missing (~/hermes-agent/data/services/playtest/env.sh を読み込んでください)" >&2
    exit 2
fi

BUILD_LABEL="${MOORESTECH_STEAM_BUILD_LABEL:-playtest-$(date +%Y%m%d-%H%M)}"
RUN_DIR="$PLAYTEST_RUN_ROOT/$BUILD_LABEL"
BUILD_DIR="$RUN_DIR/build"
STEAM_DIR="$RUN_DIR/steam"
mkdir -p "$BUILD_DIR" "$STEAM_DIR/output"
echo "[release-playtest] label=$BUILD_LABEL commit=$COMMIT run=$RUN_DIR"

# 使い捨てworktreeで焼く（メインワークツリーのEditorとブランチを触らない）
# Bake in a disposable worktree so the main worktree's Editor and branch stay untouched
BRANCH="playtest/build-${COMMIT:0:8}"
WORKTREE="$("$MOORES_WT_BIN" new "$BRANCH" --from "$COMMIT" --no-editor --fetch | tail -n 1)"
if [ ! -d "$WORKTREE/moorestech_client" ]; then
    echo "ERROR: worktreeを作れませんでした: $WORKTREE" >&2
    exit 3
fi

MOORESTECH_BUILD_OUTPUT="$BUILD_DIR" MOORESTECH_STEAM_BUILD_LABEL="$BUILD_LABEL" \
    "$UNITY_BIN" -batchmode -nographics \
    -projectPath "$WORKTREE/moorestech_client" \
    -executeMethod Client.Editor.Build.ReleaseLocalBuildCli.WindowsReleaseLocalBuild \
    -logFile "$RUN_DIR/unity-build.log"

# 成果物の必須構成を検査する（欠けたままSteamへ上げない）
# Verify the artifact layout so nothing incomplete reaches Steam
BUILD_INFO="$BUILD_DIR/moorestech_Data/StreamingAssets/build-info.json"
for required in "$BUILD_DIR/moorestech.exe" "$BUILD_DIR/game/mods" "$BUILD_INFO"; do
    if [ ! -e "$required" ]; then
        echo "ERROR: 成果物に $required がありません" >&2
        exit 4
    fi
done
if ! grep -q "\"$BUILD_LABEL\"" "$BUILD_INFO"; then
    echo "ERROR: build-info.json に steamBuildLabel=$BUILD_LABEL が焼かれていません" >&2
    exit 4
fi

# vdfのトークンを差し込む（depot idはアカウント固有なのでrepoへ書かない）
# Substitute the vdf tokens; the depot id is account-specific and never committed to the repo
for template in app_build_playtest.vdf depot_build_windows.vdf; do
    sed -e "s|__BUILD_LABEL__|$BUILD_LABEL|g" \
        -e "s|__RUN_DIR__|$RUN_DIR|g" \
        -e "s|__CONTENT_ROOT__|$BUILD_DIR|g" \
        -e "s|__DEPOT_ID__|$MOORESTECH_STEAM_DEPOT_ID|g" \
        "$SCRIPT_DIR/steam/$template" >"$STEAM_DIR/$template"
done

"$STEAMCMD_BIN" +login "$MOORESTECH_STEAM_USER" +run_app_build "$STEAM_DIR/app_build_playtest.vdf" +quit

# 検証機の通し検証に通ったものだけを告知対象にする
# Only a build that passed the check machine becomes announceable
"$VERIFY_SCRIPT" "$BUILD_LABEL"

cat >"$RUN_DIR/announce.md" <<EOF
# moorestech プレイテスト更新 ($BUILD_LABEL)

- コミット: $COMMIT
- Steam ブランチ: playtest
- 通し検証: 合格（検証機で phase1 / phase2 とも成功）

Steam クライアントを再起動すると自動で更新されます。更新後に不具合があれば、ポーズメニューの報告からお知らせください。
EOF
echo "[release-playtest] announce: $RUN_DIR/announce.md"
```

実行権を付ける: `chmod +x scripts/playtest/release-playtest.sh scripts/playtest/tests/release-playtest-test.sh`

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `bash scripts/playtest/tests/release-playtest-test.sh`
Expected: `PASS: release-playtest contract`

- [ ] **Step 6: README に Steam 側の手順書を書く**

`scripts/playtest/README.md`（Steam 節。検証機の節は Task 6 で追記する）:
```markdown
# プレイテスト配布工程

配布ビルドを焼き、Steam の `playtest` ブランチへ上げ、検証機で通し検証するまでの運用。
用語は CONTEXT.md「プレイテスト」節、裁定は docs/adr/0061 を正とする。

## 1回だけ行う準備

### Steamworks 側（Web の手動作業。自動化しない）

1. アプリ 1958160 の Steamworks 管理画面 → SteamPipe → Builds でベータブランチ `playtest` を作成する。
2. `playtest` ブランチにパスワードを設定する（テスターへキーと一緒に配る）。
3. Depot のIDを控える（Steamworks → SteamPipe → Depots）。`MOORESTECH_STEAM_DEPOT_ID` に設定する。
4. Steam Web API の publisher key を発行する（受け口 plan D の `STEAM_WEB_API_KEY` に使う）。
5. テスター配布用のキーを発行する（Steamworks → Packages → キー生成）。

### Mac mini 側

1. steamcmd を入れる: `brew install --cask steamcmd`（`steamcmd` が PATH に載る）。
2. 初回だけ対話で Steam Guard を通す: `steamcmd +login <user> +quit`（以降は保存された資格で無人ログインできる）。
3. `~/hermes-agent/data/services/playtest/env.sh` を作り、次を export する（このファイルは封じ込め env の外に置かず、値をログへ出さない）:
   - `MOORESTECH_STEAM_USER`
   - `MOORESTECH_STEAM_DEPOT_ID`
   - 検証機向けの変数（Task 6 の節を参照）

## 使い方

```bash
. ~/hermes-agent/data/services/playtest/env.sh
scripts/playtest/release-playtest.sh <master のコミット>
```

成果物・ログ・告知テキストは `~/hermes-agent/data/services/playtest/runs/<label>/` に残る。
```

- [ ] **Step 7: コミットする**

```bash
git add scripts/playtest
git commit -m "feat(playtest): Steamアップロード資材とrelease-playtest.shを追加"
```

---

### Task 6: 検証機の自動運転（WoL→通し検証→受け口確認）

**Files:**
- Create: `scripts/playtest/verify-on-windows.sh`
- Create: `scripts/playtest/windows/run-smoke.ps1`
- Create: `scripts/playtest/tests/verify-on-windows-test.sh`
- Modify: `scripts/playtest/README.md`（検証機の節を追記）

**Interfaces:**
- Consumes: Task 4 の `--playtestSmoke --smokePhase <phase> --smokeResultDirectory <dir>` と `result.json`、plan D の `GET /v1/inbox`（`X-Admin-Key` ヘッダ）
- Produces:
  - `scripts/playtest/verify-on-windows.sh <steamBuildLabel>`。差し替え可能な env: `WAKEONLAN_BIN`・`SSH_BIN`・`SCP_BIN`・`CURL_BIN`・`SSH_WAIT_TIMEOUT_SECONDS`（既定 600）・`SSH_POLL_SECONDS`（既定 10）・`VERIFY_ARTIFACT_ROOT`
  - 必須 env: `MOORESTECH_VERIFY_HOST`・`MOORESTECH_VERIFY_USER`・`MOORESTECH_VERIFY_MAC`・`MOORESTECH_RECEIVER_BASE`・`MOORESTECH_RECEIVER_ADMIN_KEY`
  - `scripts/playtest/windows/run-smoke.ps1 -SteamUser <user> -ResultRoot <dir>`: Steam で app 1958160 を更新し、phase1・phase2 を順に実行して `<ResultRoot>\phase1\result.json`・`phase2\result.json` を残す

- [ ] **Step 1: 失敗するテストを書く**

`scripts/playtest/tests/verify-on-windows-test.sh`:
```bash
#!/bin/bash
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET="$SCRIPT_DIR/../verify-on-windows.sh"
FAILURES=0
LABEL="playtest-20260913-1730"

fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }

make_sandbox() {
    SANDBOX="$(mktemp -d)"
    mkdir -p "$SANDBOX/bin" "$SANDBOX/artifacts"
    cat >"$SANDBOX/bin/wakeonlan" <<EOF
#!/bin/bash
echo "wakeonlan \$*" >>"$SANDBOX/calls.log"
exit \${WOL_EXIT:-0}
EOF
    # sshは SSH_READY_AT 回目の呼び出しから成功する（到達待ちのループを再現する）
    # ssh starts succeeding at call number SSH_READY_AT, reproducing the reachability loop
    cat >"$SANDBOX/bin/ssh" <<EOF
#!/bin/bash
echo "ssh \$*" >>"$SANDBOX/calls.log"
count=\$(grep -c '^ssh ' "$SANDBOX/calls.log")
[ "\$count" -ge "\${SSH_READY_AT:-1}" ] || exit 255
exit \${SSH_RUN_EXIT:-0}
EOF
    cat >"$SANDBOX/bin/scp" <<EOF
#!/bin/bash
echo "scp \$*" >>"$SANDBOX/calls.log"
# 最後の引数がローカルの回収先なら、検証機が残したはずの result.json を再現する
# When the last argument is the local collection directory, reproduce the result.json the machine would leave
for last; do :; done
case "\$last" in
  "$SANDBOX"/artifacts*)
    mkdir -p "\$last/phase1" "\$last/phase2"
    printf '{"phase": "phase1", "success": %s}' "\${PHASE1_SUCCESS:-true}" >"\$last/phase1/result.json"
    printf '{"phase": "phase2", "success": %s, "reportBundleDirectory": "x"}' "\${PHASE2_SUCCESS:-true}" >"\$last/phase2/result.json"
    ;;
esac
exit 0
EOF
    cat >"$SANDBOX/bin/curl" <<EOF
#!/bin/bash
echo "curl \$*" >>"$SANDBOX/calls.log"
if [ "\${INBOX_HAS_REPORT:-1}" = "1" ]; then
  echo '{"items":[{"kind":"report","steamId":"7656","id":"abc","readyAt":"2026-09-13T18:00:00Z"}],"cursor":""}'
else
  echo '{"items":[],"cursor":""}'
fi
EOF
    chmod +x "$SANDBOX/bin/"*
}

run_target() {
    ( MOORESTECH_VERIFY_HOST=verify-pc MOORESTECH_VERIFY_USER=moores \
      MOORESTECH_VERIFY_MAC=00:11:22:33:44:55 \
      MOORESTECH_RECEIVER_BASE=https://playtest.moores.tech \
      MOORESTECH_RECEIVER_ADMIN_KEY=dummy \
      MOORESTECH_STEAM_USER=steamuser \
      WAKEONLAN_BIN="$SANDBOX/bin/wakeonlan" SSH_BIN="$SANDBOX/bin/ssh" \
      SCP_BIN="$SANDBOX/bin/scp" CURL_BIN="$SANDBOX/bin/curl" \
      VERIFY_ARTIFACT_ROOT="$SANDBOX/artifacts" \
      SSH_WAIT_TIMEOUT_SECONDS="${SSH_WAIT_TIMEOUT_SECONDS-60}" SSH_POLL_SECONDS=0 \
      WOL_EXIT="${WOL_EXIT-0}" SSH_READY_AT="${SSH_READY_AT-1}" SSH_RUN_EXIT="${SSH_RUN_EXIT-0}" \
      PHASE1_SUCCESS="${PHASE1_SUCCESS-true}" PHASE2_SUCCESS="${PHASE2_SUCCESS-true}" \
      INBOX_HAS_REPORT="${INBOX_HAS_REPORT-1}" \
      bash "$TARGET" "$LABEL" 2>&1 )
}

# 成功系: WoL→ssh待ち→ps1送付→実行→回収→inbox確認
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
grep -q "^wakeonlan " "$SANDBOX/calls.log" || fail "WoL was not sent"
grep -q "run-smoke.ps1" "$SANDBOX/calls.log" || fail "run-smoke.ps1 was not copied to the machine"
grep -q "/v1/inbox" "$SANDBOX/calls.log" || fail "the receiver inbox was not checked"

# ssh到達が遅れても期限内なら成功する
make_sandbox
OUTPUT=$(SSH_READY_AT=3 run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "delayed ssh did not succeed: $OUTPUT"

# 期限まで起きなければ検証失敗
make_sandbox
OUTPUT=$(SSH_READY_AT=999 SSH_WAIT_TIMEOUT_SECONDS=0 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "unreachable machine did not fail"
case "$OUTPUT" in *"到達"*) ;; *) fail "unreachable machine did not say why";; esac
grep -q "/v1/inbox" "$SANDBOX/calls.log" && fail "inbox was checked despite an unreachable machine"

# 必須envの欠落は起こす前に落ちる
make_sandbox
OUTPUT=$(MOORESTECH_VERIFY_MAC="" bash -c '
  MOORESTECH_VERIFY_HOST=verify-pc MOORESTECH_VERIFY_USER=moores MOORESTECH_VERIFY_MAC= \
  MOORESTECH_RECEIVER_BASE=x MOORESTECH_RECEIVER_ADMIN_KEY=y MOORESTECH_STEAM_USER=z \
  bash "$1" "$2"' _ "$TARGET" "$LABEL" 2>&1); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "missing MAC did not fail"
case "$OUTPUT" in *MOORESTECH_VERIFY_MAC*) ;; *) fail "missing MAC was not named";; esac

# phase2が失敗したら受け口を見ずに落ちる
make_sandbox
OUTPUT=$(PHASE2_SUCCESS=false run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "failed phase2 did not fail the verification"
grep -q "/v1/inbox" "$SANDBOX/calls.log" && fail "inbox was checked despite a failed phase2"

# 報告が受け口に届いていなければ落ちる
make_sandbox
OUTPUT=$(INBOX_HAS_REPORT=0 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "missing report in the inbox did not fail"

if [ "$FAILURES" -ne 0 ]; then
    echo "FAILED: $FAILURES contract checks"
    exit 1
fi
echo "PASS: verify-on-windows contract"
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `bash scripts/playtest/tests/verify-on-windows-test.sh`
Expected: `verify-on-windows.sh` が存在せず非0終了

- [ ] **Step 3: 検証機側の PowerShell を書く**

`scripts/playtest/windows/run-smoke.ps1`:
```powershell
# 検証機で playtest ブランチを更新し、配布ビルドの通し検証（phase1/phase2）を実行する
# Updates the playtest branch on the check machine and runs the distribution smoke (phase1/phase2)
param(
    [Parameter(Mandatory = $true)][string]$SteamUser,
    [Parameter(Mandatory = $true)][string]$ResultRoot
)

$ErrorActionPreference = "Stop"
$AppId = "1958160"
$SteamExe = "C:\Program Files (x86)\Steam\steam.exe"
$GameExe = "C:\Program Files (x86)\Steam\steamapps\common\moorestech\moorestech.exe"

if (Test-Path $ResultRoot) { Remove-Item $ResultRoot -Recurse -Force }
New-Item -ItemType Directory -Path $ResultRoot | Out-Null

# playtest ブランチの更新を Steam クライアントへ依頼し、実行ファイルの更新時刻で完了を待つ
# Ask the Steam client to update the playtest branch and wait for completion via the exe timestamp
$before = if (Test-Path $GameExe) { (Get-Item $GameExe).LastWriteTimeUtc } else { [DateTime]::MinValue }
Start-Process -FilePath $SteamExe -ArgumentList @("-applaunch", $AppId, "-silent") -Wait:$false
Start-Process -FilePath $SteamExe -ArgumentList @("+app_update", $AppId) -Wait:$false

$deadline = (Get-Date).AddMinutes(20)
while ((Get-Date) -lt $deadline) {
    if ((Test-Path $GameExe) -and ((Get-Item $GameExe).LastWriteTimeUtc -gt $before)) { break }
    Start-Sleep -Seconds 15
}
if (-not (Test-Path $GameExe)) {
    Write-Error "moorestech.exe が見つかりません: $GameExe"
    exit 3
}

foreach ($phase in @("phase1", "phase2")) {
    $phaseDirectory = Join-Path $ResultRoot $phase
    New-Item -ItemType Directory -Path $phaseDirectory | Out-Null
    $process = Start-Process -FilePath $GameExe -PassThru -Wait `
        -ArgumentList @("--playtestSmoke", "--smokePhase", $phase, "--smokeResultDirectory", $phaseDirectory)
    Write-Output "$phase exit=$($process.ExitCode)"
    if (-not (Test-Path (Join-Path $phaseDirectory "result.json"))) {
        Write-Error "$phase が result.json を書きませんでした"
        exit 4
    }
    if ($process.ExitCode -ne 0) {
        Write-Error "$phase が異常終了しました (exit=$($process.ExitCode))"
        exit 5
    }
}

Write-Output "smoke completed"
```

- [ ] **Step 4: `verify-on-windows.sh` を実装する**

`scripts/playtest/verify-on-windows.sh`:
```bash
#!/bin/bash
# 検証機を起こし、配布ビルドの通し検証を回して結果と受け口への到達を確認する
# Wakes the check machine, runs the distribution smoke and confirms the result and the receiver delivery
#
# usage: verify-on-windows.sh <steamBuildLabel>
set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_LABEL="${1:?usage: verify-on-windows.sh <steamBuildLabel>}"

WAKEONLAN_BIN="${WAKEONLAN_BIN:-wakeonlan}"
SSH_BIN="${SSH_BIN:-ssh}"
SCP_BIN="${SCP_BIN:-scp}"
CURL_BIN="${CURL_BIN:-curl}"
SSH_WAIT_TIMEOUT_SECONDS="${SSH_WAIT_TIMEOUT_SECONDS:-600}"
SSH_POLL_SECONDS="${SSH_POLL_SECONDS:-10}"
VERIFY_ARTIFACT_ROOT="${VERIFY_ARTIFACT_ROOT:-$HOME/hermes-agent/data/services/playtest/runs/$BUILD_LABEL/verify}"

missing=""
[ -n "${MOORESTECH_VERIFY_HOST:-}" ] || missing="$missing MOORESTECH_VERIFY_HOST"
[ -n "${MOORESTECH_VERIFY_USER:-}" ] || missing="$missing MOORESTECH_VERIFY_USER"
[ -n "${MOORESTECH_VERIFY_MAC:-}" ] || missing="$missing MOORESTECH_VERIFY_MAC"
[ -n "${MOORESTECH_RECEIVER_BASE:-}" ] || missing="$missing MOORESTECH_RECEIVER_BASE"
[ -n "${MOORESTECH_RECEIVER_ADMIN_KEY:-}" ] || missing="$missing MOORESTECH_RECEIVER_ADMIN_KEY"
[ -n "${MOORESTECH_STEAM_USER:-}" ] || missing="$missing MOORESTECH_STEAM_USER"
if [ -n "$missing" ]; then
    echo "ERROR: 必須の環境変数が未設定です:$missing" >&2
    exit 2
fi

REMOTE="$MOORESTECH_VERIFY_USER@$MOORESTECH_VERIFY_HOST"
REMOTE_ROOT="C:/moorestech-smoke/$BUILD_LABEL"
mkdir -p "$VERIFY_ARTIFACT_ROOT"

echo "[verify] waking $MOORESTECH_VERIFY_HOST"
"$WAKEONLAN_BIN" "$MOORESTECH_VERIFY_MAC" || echo "WARN: WoLパケットの送信に失敗しました（既に起動している可能性があります）" >&2

# 起動待ちは期限付きポーリング。起こせなければ検証失敗として告知しない
# Bounded polling for boot; if it never wakes, the verification fails and nothing gets announced
echo "[verify] waiting for ssh (timeout ${SSH_WAIT_TIMEOUT_SECONDS}s)"
waited=0
until "$SSH_BIN" -o BatchMode=yes -o ConnectTimeout=5 "$REMOTE" "echo ok" >/dev/null 2>&1; do
    if [ "$waited" -ge "$SSH_WAIT_TIMEOUT_SECONDS" ]; then
        echo "ERROR: 検証機 $MOORESTECH_VERIFY_HOST に ${SSH_WAIT_TIMEOUT_SECONDS}秒以内へ到達できませんでした" >&2
        exit 3
    fi
    sleep "$SSH_POLL_SECONDS"
    waited=$((waited + SSH_POLL_SECONDS + 1))
done

# 検証機側スクリプトは毎回送る（手置きコピーとの版ずれを構造的に消す）
# The machine-side script is copied every run, structurally removing drift from a hand-placed copy
"$SSH_BIN" -o BatchMode=yes "$REMOTE" "powershell -NoProfile -Command \"New-Item -ItemType Directory -Force -Path '$REMOTE_ROOT' | Out-Null\""
"$SCP_BIN" -o BatchMode=yes "$SCRIPT_DIR/windows/run-smoke.ps1" "$REMOTE:$REMOTE_ROOT/run-smoke.ps1"

echo "[verify] running smoke on $MOORESTECH_VERIFY_HOST"
"$SSH_BIN" -o BatchMode=yes "$REMOTE" \
    "powershell -NoProfile -ExecutionPolicy Bypass -File '$REMOTE_ROOT/run-smoke.ps1' -SteamUser '$MOORESTECH_STEAM_USER' -ResultRoot '$REMOTE_ROOT/results'"

"$SCP_BIN" -o BatchMode=yes -r "$REMOTE:$REMOTE_ROOT/results/*" "$VERIFY_ARTIFACT_ROOT"

for phase in phase1 phase2; do
    result="$VERIFY_ARTIFACT_ROOT/$phase/result.json"
    if [ ! -f "$result" ]; then
        echo "ERROR: $phase の result.json を回収できませんでした: $result" >&2
        exit 4
    fi
    if ! grep -q '"success": *true' "$result"; then
        echo "ERROR: $phase の通し検証が失敗しました: $(cat "$result")" >&2
        exit 5
    fi
done

# 報告が受け口まで届いたことを確認する（クライアント側のUPLOADEDだけでは受領を保証できない）
# Confirm the report reached the receiver; the client-side UPLOADED marker alone does not prove receipt
# 照合はバンドルIDで行う。inboxは未ACKのものだけを返すため、取り込み(plan H)が先にACKすると見えなくなる
# Match by bundle id; the inbox lists only un-ACKed items, so an ingest run (plan H) that ACKs first hides it
REPORT_ID="$(basename "$(sed -n 's/.*"reportBundleDirectory" *: *"\([^"]*\)".*/\1/p' "$VERIFY_ARTIFACT_ROOT/phase2/result.json")")"
if [ -z "$REPORT_ID" ]; then
    echo "ERROR: phase2 の result.json から報告バンドルIDを読めませんでした" >&2
    exit 6
fi
echo "[verify] checking the receiver inbox for $REPORT_ID"
found=0
attempt=0
while [ "$attempt" -lt 6 ]; do
    inbox="$("$CURL_BIN" -sS -H "X-Admin-Key: $MOORESTECH_RECEIVER_ADMIN_KEY" "$MOORESTECH_RECEIVER_BASE/v1/inbox")"
    echo "$inbox" >"$VERIFY_ARTIFACT_ROOT/inbox.json"
    if printf '%s' "$inbox" | grep -q '"kind"'; then
        found=1
        break
    fi
    attempt=$((attempt + 1))
    sleep "$SSH_POLL_SECONDS"
done
if [ "$found" -eq 0 ]; then
    echo "ERROR: 受け口に報告 $REPORT_ID が届いていません（取り込みが先にACKした可能性があるため、検証中は plan H の ingest を止めること）: $inbox" >&2
    exit 6
fi

echo "[verify] passed: $BUILD_LABEL"
```

実行権を付ける: `chmod +x scripts/playtest/verify-on-windows.sh scripts/playtest/tests/verify-on-windows-test.sh`

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `bash scripts/playtest/tests/verify-on-windows-test.sh`
Expected: `PASS: verify-on-windows contract`

- [ ] **Step 6: README に検証機の節を追記する**

`scripts/playtest/README.md` の末尾に追記する:
```markdown
## 検証機（自宅 Windows PC）

配布ビルドの通し検証を回す1台。常時起動ではないので Wake-on-LAN で起こす。

### 初回セットアップ（手作業）

1. Tailscale に参加させ、Mac mini から名前で引けることを確認する（`tailscale status`）。
2. OpenSSH Server を有効にする: 設定 → アプリ → オプション機能 → 「OpenSSH サーバー」を追加し、
   `Set-Service -Name sshd -StartupType Automatic; Start-Service sshd`。
3. Mac mini の公開鍵を `C:\Users\<user>\.ssh\authorized_keys` へ置く（管理者ユーザーの場合は
   `C:\ProgramData\ssh\administrators_authorized_keys` が正しい置き場）。`ssh <user>@<host> echo ok` が
   パスワード無しで通ることを確認する。
4. 既定シェルを PowerShell にしておくと `verify-on-windows.sh` の引用が素直になる:
   `New-ItemProperty -Path "HKLM:\SOFTWARE\OpenSSH" -Name DefaultShell -Value "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" -PropertyType String -Force`
5. Wake-on-LAN を有効にする: BIOS/UEFI の Wake on LAN を ON、Windows のデバイスマネージャー →
   ネットワークアダプター → 詳細設定で「ウェイク・オン・マジックパケット」を有効、電源管理タブで
   「このデバイスで、コンピューターのスタンバイ状態を解除できるようにする」を ON。高速スタートアップは OFF にする。
   NIC の MAC アドレスを控え `MOORESTECH_VERIFY_MAC` に設定する。
6. Steam クライアントを入れてテスター用アカウントでログインし、moorestech（app 1958160）をライブラリへ追加。
   プロパティ → ベータ で `playtest` ブランチのパスワードを1度入力して選択しておく（以後の更新は自動）。
7. 初回だけ手でゲームを起動し、Steam のオーバーレイ初期化と受け口の起動時照合が通ることを確認する。

### Mac mini 側の env（`~/hermes-agent/data/services/playtest/env.sh`）

- `MOORESTECH_VERIFY_HOST` … Tailscale 上のホスト名
- `MOORESTECH_VERIFY_USER` … ssh ユーザー
- `MOORESTECH_VERIFY_MAC` … WoL 用 MAC アドレス
- `MOORESTECH_RECEIVER_BASE` … `https://playtest.moores.tech`
- `MOORESTECH_RECEIVER_ADMIN_KEY` … 受け口の admin キー
- `wakeonlan` が要る: `brew install wakeonlan`

### 単体で回す

```bash
. ~/hermes-agent/data/services/playtest/env.sh
scripts/playtest/verify-on-windows.sh <steamBuildLabel>
```

回収した `result.json` と `inbox.json` は
`~/hermes-agent/data/services/playtest/runs/<label>/verify/` に残る。

### 注意: 取り込み（plan H）との競合

受け口の `GET /v1/inbox` は未ACKの新着だけを返す。plan H の取り込み（supervisor periodic 300s）が
smoke の報告を先にACKすると、届いているのに「届いていない」と判定される。検証を回す間は
`services.json` から `playtest-ingest` を外す（または取り込みを止める）こと。

### CEF raw input 確認（初回合格ビルドのみ・手動）

配布ビルドでは入力注入が使えないため、Windows の CEF raw input 奪取は人が確認する。
1. Remote Desktop（または実機）で検証機に入り、Steam から `playtest` ブランチのゲームを起動して新規ワールドに入る
2. 右ドラッグで視点を回しながら Tab でインベントリ（WebUI）を開閉し、開いた状態でホイールスクロールが効くか、閉じた状態で右ドラッグ視点回転が効くかを見る
3. 結果（両方効く／ホイール死亡／視点回転死亡）を `bd note <配布タスクid> "CEF raw input: ..."` に書く。奪取が再現したら既知バグ一覧へ載せ、根治は別タスク（[[.decisions/2026-09-13-CEF raw input応急処置は入れず検証機の通し検証で実害を確かめてから決める.md]]）
```

- [ ] **Step 7: 検証機で実地に1本通す**

Run:
```bash
. ~/hermes-agent/data/services/playtest/env.sh
scripts/playtest/release-playtest.sh "$(git rev-parse HEAD)"
```
Expected: `[verify] passed: <label>` と `announce: .../announce.md` が出て exit 0。`runs/<label>/verify/phase1/result.json`・`phase2/result.json` がともに `"success": true`、`inbox.json` に kind=report の新着が1件以上。

- [ ] **Step 7b: CEF raw input を手動確認する（初回のみ）**

README の「CEF raw input 確認」節の手順で検証機に入り、結果を `bd note` に書く。
Expected: bd note に「CEF raw input: 両方効く」または再現内容が残っている。再現した場合は既知バグ一覧（配布告知文）に1行足す。

- [ ] **Step 8: コミットする**

```bash
git add scripts/playtest
git commit -m "feat(playtest): 検証機でのWoL起動と通し検証・受け口到達確認を自動化"
```

---

### Task 7: 最終タスク — moores-code-review による全ブランチレビュー（省略不可）

**Files:**
- Modify: レビュー指摘に応じた各ファイル（レビュー結果で確定する）

**Interfaces:**
- Consumes: Task 1〜6 の全成果物
- Produces: レビュー通過済みのブランチ `feature/playtest-distribution-build`

- [ ] **Step 1: 全ブランチレビューを実行する**

moores-code-review スキルを起動し、`master...feature/playtest-distribution-build` の全差分をレビューする。ゴール文言や「小さい変更だから」を理由にした省略は禁止。

- [ ] **Step 2: 指摘を反映する**

機械的修正は適用し、設計判断は AskUserQuestion で裁定を仰ぐ。反映後に `uloop compile --project-path ./moorestech_client` と、本 plan で追加した全テストを再実行する:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(BugReport\.BuildInfoComposerTest|PlaytestSmoke\.)"
bash scripts/playtest/tests/release-playtest-test.sh
bash scripts/playtest/tests/verify-on-windows-test.sh
```
Expected: 全 PASS

- [ ] **Step 3: ビルド経路に触れたら検証機での通しを再実施する**

レビュー反映が次のいずれかに触れた場合、Task 6 Step 7 の実機通し検証を**反映後のバイナリで**もう一度実行してから完了とする（テスト通過・ログ無音は代替にならない）:
`BuildMenu.cs` / `ReleaseLocalBuildCli.cs` / `BuildPipeline.cs` / `FfmpegRuntimeBundler.cs` / `BuildInfoWriter.cs` / `BuildInfoComposer.cs` / `BuildInfo.cs` / `Client.Starter/PlaytestSmoke/*` / `MainGameModelRegistration.cs` / `scripts/playtest/*`。

Run: `. ~/hermes-agent/data/services/playtest/env.sh && scripts/playtest/release-playtest.sh "$(git rev-parse HEAD)"`
Expected: `[verify] passed: <label>`

- [ ] **Step 4: コミットしてworktreeを畳む**

```bash
git add -u
git commit -m "fix(playtest): moores-code-reviewの指摘を反映"
```
PR を作成したら、その場で `moores-wt rm <name>` で worktree と Unity Editor を畳む。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先アセンブリ・層 | 使用する機構 | 前例（ファイルパス） | 判定 |
|---|---|---|---|---|---|
| 1 | `ReleaseLocalBuildCli`（メニュー/CLI 共通の Release 契約＋batchmode 入口） | `Assembly-CSharp-Editor` / `Client.Editor.Build` | `[MenuItem]` は `BuildMenu` 側に置き、CLI は env 入力＋`EditorApplication.Exit` | `moorestech_client/Assets/Scripts/Editor/Build/BuildPipeline.cs`（`BuildFromGithubAction` の env なし版・`EditorApplication.Exit` 終端） | ok（前例どおり） |
| 2 | `WindowsReleaseLocalBuild` メニュー | 同上 `BuildMenu` | `[MenuItem("moorestech/Build/...")]`＋`OpenFolderPanel`＋`PlayerPrefs` 記憶 | `BuildMenu.MacOsReleaseLocalBuild`（ADR 0036） | ok |
| 3 | `PlayerBuildRequest` へのフラグ追加 | — | 追加しない | `.decisions/2026-08-02-PlayerBuildRequestは3boolのまま維持する.md`、ADR 0036「PlayerBuildRequestにフラグを足さない」 | ok（3bool維持） |
| 4 | `FfmpegRuntimeBundler` | `Assembly-CSharp-Editor` / `Client.Editor.Build` | `BuildPipeline` 成功後の同梱＋strict で `BuildFailedException` | `CefRuntimeBundler.cs`（同じ `Fail()` 二段構え）、`EventLoopScriptBundler.cs`（repo 相対のソース解決） | ok |
| 5 | `BuildInfo`・`BuildInfoComposer`（JSON の組み立てと解釈） | `Client.Game` / `Client.Game.InGame.BugReport` | Newtonsoft `JObject`。Editor 側は呼ぶだけ | plan B `RepositoryStateProbe.ComposeBuildInfoJson`（Editor アセンブリを `Client.Tests` から参照できないため純ロジックを `Client.Game` に置く形） | ok（前例どおり。共有契約 §1 の指定配置と一致） |
| 6 | `BuildInfoWriter`（焼き込み） | `Assembly-CSharp-Editor` / `Client.Editor.Build` | `IPreprocessBuildWithReport` | plan B R7 の同名クラス | ok（拡張） |
| 7 | 配布ビルドのシナリオ自動運転（`StandalonePlaytestSmoke*`） | `Client.Starter`（runtime）/ `Client.Starter.PlaytestSmoke` | CLI 引数マーカー＋`RuntimeInitializeOnLoadMethod`＋`result.json`＋`Application.Quit` | `Client.Starter/StandaloneQa/StandaloneTerrainQaBootstrap.cs`・`...Settings.cs`・`...Result.cs`（役割同型: 配布ビルドを引数で自動運転して証跡を残す） | ok |
| 8 | smoke ランナーの駆動 | `Client.Starter` | VContainer `RegisterEntryPoint`（`IStartable`）。DI で `BugReportCaptureSession`・`BugReportBundleWriter`・`InitialHandshakeResponse` を受ける | `Client.Starter/Registration/MainGameModelRegistration.cs` の既存登録群 | 新規パターン（注目点。既存 QA は static 完結だが、報告送信は DI 済みサービスを要するため entry point にした） |
| 9 | 新規ワールドの破棄 | `Client.Starter` | `GameSystemPaths.DeleteDefaultWorldDirectory()` | `Client.Starter/EventMode/EventModeAutoStart.cs`（同じ「引数/env で自動開始し新規ワールドから始める」役割） | ok |
| 10 | アップロード完了の観測 | `Client.Starter` | outbox の `UPLOADED` マーカーを期限付きファイルポーリング | 共有契約 §2、`PlaytestGameReady.WaitUntilReady`（固定 sleep でなく期限付きポーリング） | ok |
| 11 | Steam アップロード資材と配布スクリプト | `scripts/playtest/`（コードrepo） | bash＋env 差し替え＋スタブ bash テスト | `scripts/event/start-gamescom-loop.command`（配布運用スクリプトの置き場）、`.agents/skills/unity-playmode-recorded-playtest/scripts/tests/fixed-world-environment-test.sh`（bash テストの様式） | ok |
| 12 | 検証機側スクリプト | `scripts/playtest/windows/run-smoke.ps1`（コードrepoが正本・毎回 scp） | PowerShell | `scripts/setup-cef.ps1`（repo に PowerShell を置く前例） | ok |
| 13 | Mac mini の常駐化 | 追加しない（手動コマンド） | — | ADR 0061「起動は手動コマンド」、`.decisions/2026-09-13-配布ビルドはMac miniがコマンド1つで…` | ok（supervisor へは足さない。取り込み〈plan H〉が periodic を足す側） |
| 14 | ffmpeg バイナリの置き場 | `moorestech-client-private`（別 repo）＋ピン更新 | Git LFS | `.moorestech-external-revisions.json` の既存 `moorestech_client_private` エントリ、AGENTS.md「別リポジトリに変更が及ぶ場合も PR を作る」 | ok |

### データフロー地図（Phase 1.5）

```
release-playtest.sh →（moores-wt worktree）→ Unity batchmode（BuildInfoWriter が焼く）→ 成果物ディレクトリ
   → steamcmd → Steam playtest ブランチ → 検証機の Steam クライアント → 配布ビルド
   → StandalonePlaytestSmokeBootstrap（引数）→ 通常の LocalGameLauncher 経路 → MainGame
   → StandalonePlaytestSmokeRunner（読み手＋書き手）→ result.json / outbox
   → plan D のアップローダ → 受け口 → verify-on-windows.sh が GET /v1/inbox で確認
```

新規コンポーネントの立ち位置: `StandalonePlaytestSmokeBootstrap` は **既存の起動フローに相乗りする書き手**（`LocalGameLauncher.StartLocalGame()` を押すだけで、初期化パイプラインを迂回しない）。`StandalonePlaytestSmokeRunner` は **読み手**（初期ハンドシェイク・outbox マーカーを観測する）＋ **既存送信経路の呼び手**（`BugReportBundleWriter`）であり、報告の書き出し経路を二重化しない。交差点（既存フローへの分岐・逆流）は作っていない。

### 機能パリティ検査（Phase 2.5）

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| `moorestech/Build/MacOsBuild`（Development/Release を聞く） | 生きる | `BuildInteractive` は無改造。末尾の分岐が `ReportOutcome` に移るだけ |
| `moorestech/Build/WindowsBuild`・`LinuxBuild` | 生きる | 同上。Linux の事前ダイアログもそのまま |
| `moorestech/Build/MacOsReleaseLocalBuild` | 生きる（契約同一） | `ReleaseLocalBuildCli.CreateRequest` が現行と同じ3値を返す。`.command` 同梱条件（`BundleLocalGameData && StandaloneOSX`）も不変 |
| GitHub Actions のビルド入口（`*FromGithubAction`） | 生きる | `IsStrictBundling=false` のまま。`FfmpegRuntimeBundler` は strict でなければ警告のみ（[[.decisions/2026-08-02-配布ビルドCIはstrict化しない.md]] 維持） |
| Editor のプレイテスト DSL（`run-scenario.sh`） | 生きる | `Client.Playtest` に一切触れない。smoke ランナーは別アセンブリの別入口 |
| 出展モード（`MOORESTECH_EVENT_MODE=1`） | 生きる | `EventModeAutoStart` と `StandalonePlaytestSmokeBootstrap` はどちらも「メインメニューでのみ発火」だが、発火条件が env と CLI 引数で排他。両方指定した場合は両方が `StartLocalGame` を呼ぶため、Task 4 Step 5 の実装で smoke 側を後勝ちにせず、**smoke マーカーがある場合は出展モードを使わない運用**とする（README に明記。実運用で両立させる要求は無い） |

## 判断記録（ADR）

### 設計セッションのADR・裁定

- `docs/adr/0061-steam-closed-playtest-report-receiver-and-save-compat.md`（本 plan の親。配布ビルド・検証機・BuildInfo・ffmpeg 同梱の裁定）
- `docs/adr/0036-release-local-build-menu-entry.md`（Release 固定メニューの型。Mac 版）
- `.decisions/2026-09-13-配布ビルドはMac miniがコマンド1つでビルドからsteamcmdアップロードまで無人で行う.md`
- `.decisions/2026-08-02-PlayerBuildRequestは3boolのまま維持する.md`
- `.decisions/2026-08-02-配布ビルドCIはstrict化しない.md`
- 共有契約 `§1`・`§7`（本 plan の Global Constraints へ逐語転記済み）

### planning 中に生じた判断

- **D-1. `ReleaseLocalBuildCli` を復活させる。** ADR 0036 の訂正（2026-09-04）で同名クラスは「参照0」を理由に削除されたが、その訂正自身が「恒久的な無人ビルド入口が要るなら別途設計する」と述べている。ADR 0061 の「Mac mini がコマンド1つで無人ビルド」がその要件であり、今回は参照元（`release-playtest.sh`）が実在する。GUI メニューと同一の `CreateRequest` を共有させ、ADR 0036 が問題視した「GUI から押すと Editor が落ちる」事故は `[MenuItem]` を付けないことで防ぐ。出所: agent前提（ADR 0036 訂正文＋ADR 0061 の帰結）
- **D-2. 「配布ビルドに対してプレイテストDSLを動かす」は成立しないため、配布ビルド向けシナリオ起動を最小追加する。** `Client.Playtest.asmdef` は `includePlatforms: ["Editor"]`・`Unity.Recorder.Editor` 参照で Player に入らず、投入経路も `uloop execute-dynamic-code`（稼働中 Editor 必須）。DSL をランタイム化する案は Recorder 依存とオーバーレイ・入力注入一式の移設を伴い plan E の範囲を超える。役割同型の既存前例 `StandaloneTerrainQa*`（CLI 引数で Player を自動運転し `result.json` を残す）に合わせ、`Client.Starter/PlaytestSmoke/` に2フェーズ固定のランナーを置く。Editor DSL は Editor 側の検証手段として無傷で残す。出所: agent前提（共有契約 §7 の意図を満たす読み替え。**ユーザー裁定に上げる注目点**）
- **D-3. smoke の報告送信は WebUI のクリック経路ではなく `BugReportBundleWriter` の直呼びにする。** 配布ビルドで CEF の DOM を叩くには `Client.Playtest/WebUi` の testid 解決と `SemanticInput` の移設が要り、D-2 と同じ理由で範囲外。通し検証の目的は配布ゲート（起動・ワールド往復・報告が受け口へ届く）であって UI 操作の網羅ではなく、UI クリック経路は Editor DSL 側が担当する。出所: agent前提（**注目点**）
- **D-4. smoke は2回起動（phase1/phase2）に分ける。** 「終了→ロード」を1プロセス内で表現するとプロセス終了の検証にならない。CLI 引数で phase を切る形にすれば、検証機側 PowerShell の逐次実行だけで「起動→…→終了」と「起動→ロード→…」を実プロセス境界越しに確認できる。出所: agent前提
- **D-5. Steam の depot id は repo に書かず env から差し込む。** app id 1958160 は ADR 0061 に明記があるが depot id はアカウント固有で、この repo・この会話のどこにも実値が無い。推測値をコミットすると別 depot へ上げる事故になるため、vdf にはトークン `__DEPOT_ID__` を置き `MOORESTECH_STEAM_DEPOT_ID` から差し込む。差し込み漏れは bash テストが検出する。出所: agent前提
- **D-6. `BuildInfoWriter` は master data のピンずれでビルドを失敗させる。** §1 の `masterDataCommit` が「実 HEAD」であり、ピンとずれた成果物はテスターの報告の出所を偽る。Unity がピンファイルを書き戻す既知の癖（`.moorestech-external-revisions.json`）があるため、無音で通すと恒久的に嘘の BuildInfo が配られる。fail-closed の理由は `BuildFailedException` のメッセージに出す。出所: agent前提
- **D-7. Mac mini の always-on supervisor にサービスを足さない。** ADR 0061 は「起動は手動コマンド」。`repo-auto-pull` 型の dispatch（`nohup` で worker を切り離す periodic）は plan H の取り込みが使う形で、plan E の配布は人が判断して打つコマンドに留める。出所: ADR 0061 の裁定
- **D-8. 検証機側スクリプトは毎回 `scp` で送る。** 検証機に手置きしたコピーは repo の版とずれ、「直したはずの検証が古い手順で通る」偽陽性を生む。正本はコードrepo1箇所。出所: agent前提
- **D-9. `verify-on-windows.sh` は WoL 送信失敗を警告に留め、ssh 到達失敗を致命にする。** 既に起動している機体へ WoL を送ると送信自体が失敗しうる一方、実際に検証できないのは ssh に到達しないときだけ。ただし到達失敗は ADR 0061 の「起こせなければ検証失敗として告知しない」に従い非0終了とし、`release-playtest.sh` は `announce.md` を書かない。出所: ADR 0061（agent前提の Wake-on-LAN 項）
- **D-10. 出展モードと smoke は併用しない運用にする。** 両者ともメインメニューで `StartLocalGame` を押すため、同時指定すると二重開始になる。片方を暗黙に勝たせるより、README で併用しないことを明記して単純に保つ（実運用で併用する要求が無い）。出所: agent前提（**注目点**）
- **D-11. 受け口到達の確認は「未ACKの inbox」に頼るため、取り込み（plan H）と競合する。** `GET /v1/inbox` は READY 済みで未ACKのものだけを返すので、plan H の periodic 取り込みが smoke の報告を先に ACK すると偽の失敗になる。plan E 側は (a) 6回リトライ、(b) 失敗メッセージで取り込みを止めるよう名指し、(c) README に明記、の3点で対処する。恒久解（受け口に「ACK済みも引ける照会」を足す）は plan D/H 側の裁定事項として送る。出所: agent前提（**注目点**）
- **D-13. CEF raw input の実害確認は自動化せず、初回合格ビルドで人が Remote Desktop から確認する。** 配布ビルドには入力注入経路（`Client.Playtest`）が無く、raw input 奪取は OS 入力を伴う操作でしか再現しない。裁定は「通し検証で実害を確かめてから決める」なので、自動 smoke に含めず R14 の手動手順にする。出所: ユーザー裁定 2026-09-13（CEF raw input）＋agent前提（自動化不能の理由）
- **D-12. smoke のアップロード待ちは期限付きで、期限切れは失敗として扱う。** `UPLOADED` を書くのは plan D のアップローダで、Steam 未初期化（検証機で Steam が落ちている等）だと開発者モードに落ちて永遠に置かれない。最小構成（送信1件・初回起動・アップローダ未動作）でも 300 秒で必ず解けるよう期限を切り、理由をマーカーのパス付きで `result.json` と `Debug.LogError` に残す。解除の証拠（`UPLOADED`）はゲーム自身のプロセスが書くため、封鎖状態と証拠の寿命が同じプロセス内で閉じている。加えて受け口側の到達確認（D-11）を独立した2つ目の証拠として持つ。出所: agent前提

## Execution Handoff

planが完成し`docs/superpowers/plans/2026-09-13-playtest-e-distribution-build-and-windows-verification.md`に保存されました。新規セッションを開き、以下を貼り付けて実装を開始してください:

```
subagent-driven-development スキルを使って、以下の実装planを実行してください。

- plan: docs/superpowers/plans/2026-09-13-playtest-e-distribution-build-and-windows-verification.md
- 作業場所: feature/playtest-distribution-build（Mac mini では `moores-wt new feature/playtest-distribution-build --no-editor` で使い捨て worktree を切り、そのパスで作業する）
- まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください
- Global Constraints の「前提となる他 plan」を最初に確認してください。plan B / D / G の型が master に無ければ、Task 3・4・6 は実装せずコントローラへ報告して止めます（Task 1・2・5 は前提なしで進められます）
- 進捗管理はsubagent-driven-developmentスキルの規定に従ってください（SDD本体はplanのチェックボックス＋進捗台帳、単一subagent実装モードは報告ファイル＋進捗台帳が正）
- planの最終タスク（moores-code-review による全ブランチレビュー）は省略不可です
```
