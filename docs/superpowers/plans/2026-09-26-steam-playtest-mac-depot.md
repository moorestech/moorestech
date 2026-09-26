# Steam配布パイプラインへのMac（Apple Silicon）版追加 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** `scripts/playtest/release-playtest.sh` 1コマンドで、同一コミットから Windows と Mac（arm64）の配布ビルドを焼き、1つの Steam ビルドに両 depot を入れて `playtest-staging` へ上げる。

**Architecture:** Unity 側は `PlayerBuildRequest` の2つの bool（strict・ゲームデータ同梱）を用途 enum `BuildPurpose` に置き換え、展示会用スクリプトの同梱を `Exhibition` 用途だけに限る。Mac ビルドでは arm64 固定・ffmpeg 同梱・同梱後の ad-hoc 再署名を `BuildPipeline` が行う。シェル側は2回の Unity 呼び出し・OS別の成果物検査・2 depot の VDF 生成を足し、Mac は自動検証せず `promotion.md` に手動確認欄を書く。

**Tech Stack:** Unity 6000.3.8f1（Editor C#, batchmode）、bash 3.2（macOS 標準）、steamcmd、codesign/lipo（Xcode CLT）、NUnit（Client.Tests）。

## Requirements

1. `release-playtest.sh <commit>` が同じ worktree・同じコミットから Windows→Mac の順に焼く。受入: 契約テストで `unity` 呼び出しが2回（`WindowsSteamPlaytestBuild`→`MacOsSteamPlaytestBuild`）記録される。
2. 1回の `steamcmd +run_app_build` で Windows depot と Mac depot を両方上げ、`setlive` は `playtest-staging`。受入: 生成された `app_build_playtest.vdf` が両 depot ID を含み、`depot_build_windows.vdf` / `depot_build_mac.vdf` の contentroot がそれぞれ `build-windows` / `build-mac` を指す。
3. どちらかのビルドまたは成果物検査が失敗したら steamcmd を呼ばない。受入: Windows 失敗・Mac 失敗・Mac 署名検証失敗・Mac アーキテクチャ不一致・展示会用スクリプト混入の各ケースで steamcmd が呼ばれない契約テスト。
4. Mac は自動検証しない。Windows の `verify-on-windows.sh` は従来どおり走る。`promotion.md` に「Mac 版は未検証」と手動確認手順、回避操作の有無と手順の記録欄を書く。受入: 成功時の `promotion.md` に `Mac 版の手動確認` と `回避操作` の語がある。
5. 回避操作が要っても配布は止めず、手順をキーと一緒に案内する旨を `promotion.md` に書く。受入: 同上ファイルに `案内` の文言。
6. Mac 向け ffmpeg（arm64・GPL 静的ビルド）を非公開アセット `ffmpeg/macos-arm64/`（`ffmpeg`・`LICENSE`）に追加し、Mac 成果物の `moorestech.app/Contents/MacOS/ffmpeg` と `Contents/Resources/ffmpeg-LICENSE.txt` へ同梱する。受入: 実ビルドの成果物に両ファイルがあり、`Contents/MacOS/ffmpeg -version` が終了コード0。
7. 実行時の `FfmpegLocator` が Mac Player で同梱 ffmpeg を最優先で見つける。受入: `ResolveBundledPath(dataPath, RuntimePlatform.OSXPlayer)` が `<dataPath>/MacOS/ffmpeg` を返す EditMode テスト。
8. Mac ビルドは arm64 専用。受入: 実ビルドの `lipo -archs moorestech.app/Contents/MacOS/moorestech` が `arm64` のみ。シェルの成果物検査もこれを確認する。
9. 同梱後に `.app` を ad-hoc 再署名する。受入: 実ビルドで `codesign --verify --deep --strict moorestech.app` が0。
10. `PlayerBuildRequest` は `Target`・`OutputDirectory`・`Purpose`・`IsDevelopmentBuild` だけを持つ。strict・ゲームデータ同梱・展示会用スクリプト同梱は `BuildPurposeRules` の1か所から導く。受入: `IsStrictBundling`・`BundleLocalGameData` の参照が repo（`docs/` を除く）に残らない。
11. 展示会用スクリプト `start-gamescom-loop.command` は `Exhibition` 用途のときだけ同梱する（開発メニューの Mac ビルドにも入らなくなる）。受入: Steam 配布の Mac 成果物に無いことをシェルが検査する。
12. env は `MOORESTECH_STEAM_DEPOT_ID_WINDOWS` / `MOORESTECH_STEAM_DEPOT_ID_MAC`（数字のみ）。未設定・非数字はビルド前に exit 2。受入: 契約テスト。
13. 非公開アセットの Mac ffmpeg が無い・LFS ポインタ・LICENSE 欠落ならビルド前に exit 3。受入: 契約テスト。
14. README に Steamworks 側の手作業（macOS depot 作成・macOS 起動オプション・パッケージへの depot 追加・Apple Silicon 要件の明記）と env 名を書く。

**やらないこと:** Mac の自動通し検証・Steam クライアントの Mac mini 導入・公証/Developer ID 署名・Intel/Universal 対応・Steamworks 設定の自動化・Linux。

## Global Constraints

- ADR: `docs/adr/0071-playtest-distribution-adds-apple-silicon-mac-depot.md`。用語は `CONTEXT.md`「ビルド用途」「展示会ビルド」「配布ビルド」。
- すべてのコード（.cs/.sh）は1ファイル200行未満。1ディレクトリの新規コードは10ファイルまで（`Editor/Build/` は本planで .cs 9本）。
- `partial` 禁止、`Func<>` 禁止、デフォルト引数禁止、try-catch 禁止（外部プロセス境界でも既存前例どおり catch しない）。
- コメントは日本語1行→英語1行の2行セット。fail-closed の経路は必ず理由をログ（C# は `Debug.LogError`/`BuildFailedException`、シェルは stderr）。
- `.meta` を手で作らない。ファイル改名は `git mv` で `.cs` と `.cs.meta` を一緒に動かす。新規 `.cs` の `.meta` は Unity のコンパイル時に生成されたものをコミットする。
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client` を実行し ErrorCount 0 を確認する。
- シェルは bash 3.2 で動く書き方（連想配列・`${var,,}` 禁止）。値（depot ID・ユーザー名）をログへ出さない。
- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/steam-playtest-mac-build`（branch `feature/steam-playtest-mac-build`）。最初に `pwd` で確認する。Unity Editor は `uloop launch /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/steam-playtest-mac-build/moorestech_client` で立ち上げる。
- 非公開アセットは worktree 内 `moorestech_client/Assets/PersonalAssets/moorestech-client-private`（remote `moorestech/moorestech-client-private`）。変更は同 repo で push して PR を作り、本 repo の `.moorestech-external-revisions.json` のピンをその push 済みコミットへ更新する（AGENTS.md 関連リポジトリ規約）。
- bd タスク: `moorestech-jql13`。

---

## File Structure

| ファイル | 責務 | 新規/変更 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Editor/Build/BuildPurpose.cs` | 用途 enum と用途→方針の導出 `BuildPurposeRules` | 新規 |
| `moorestech_client/Assets/Scripts/Editor/Build/PlayerBuildRequest.cs` | 2 bool を `Purpose` へ置換 | 変更 |
| `moorestech_client/Assets/Scripts/Editor/Build/BuildPipeline.cs` | 用途から方針を引く・Mac で arm64 固定と再署名 | 変更 |
| `moorestech_client/Assets/Scripts/Editor/Build/BuildMenu.cs` | メニューを開発/展示会/Steam配布に整理 | 変更 |
| `moorestech_client/Assets/Scripts/Editor/Build/SteamPlaytestBuildCli.cs` | 旧 `ReleaseLocalBuildCli`。Windows/Mac の無人入口 | 改名＋変更 |
| `moorestech_client/Assets/Scripts/Editor/Build/ExternalToolRunner.cs` | chmod/codesign の外部プロセス実行を1か所に | 新規 |
| `moorestech_client/Assets/Scripts/Editor/Build/MacPlayerArchitecture.cs` | Mac Player を arm64 に固定 | 新規 |
| `moorestech_client/Assets/Scripts/Editor/Build/MacAppAdHocSigner.cs` | 同梱後の `.app` を ad-hoc 再署名・検証 | 新規 |
| `moorestech_client/Assets/Scripts/Editor/Build/Bundlers/FfmpegRuntimeBundler.cs` | Mac 同梱を追加 | 変更 |
| `moorestech_client/Assets/Scripts/Editor/Build/Bundlers/EventLoopScriptBundler.cs` | chmod を `ExternalToolRunner` へ | 変更 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Recording/FfmpegLocator.cs` | Player 種別ごとの同梱位置 | 変更 |
| `moorestech_client/Assets/Scripts/Client.Tests/BugReport/FfmpegLocatorTest.cs` | 同梱位置のテスト | 変更 |
| `scripts/playtest/release-playtest.sh` | 2回ビルド・lib 呼び出し・promotion | 変更 |
| `scripts/playtest/lib/release-preflight.sh` | env 改名・Mac ffmpeg 検査 | 変更 |
| `scripts/playtest/lib/release-artifact.sh` | OS 別成果物検査（Windows 分は release-playtest.sh から移設） | 新規 |
| `scripts/playtest/lib/release-steam-vdf.sh` | 3つの VDF の生成（release-playtest.sh から移設） | 新規 |
| `scripts/playtest/steam/app_build_playtest.vdf` / `depot_build_windows.vdf` / `depot_build_mac.vdf` | 2 depot 化 | 変更/新規 |
| `scripts/playtest/tests/lib/release-playtest-sandbox.sh` | env 改名・新スタブ呼び出し | 変更 |
| `scripts/playtest/tests/lib/release-playtest-build-stubs.sh` | unity/codesign/lipo スタブ（sandbox から分離） | 新規 |
| `scripts/playtest/tests/test-release-playtest.sh` / `test-release-playtest-mac.sh` | 契約テスト | 変更/新規 |
| `scripts/playtest/README.md` | Steamworks 手作業・env・Mac 手動確認 | 変更 |
| 非公開 repo `ffmpeg/macos-arm64/ffmpeg`・`LICENSE`・`.gitattributes` | Mac ffmpeg 正本 | 新規 |

既存部品の扱い:

| 新規の役割 | 既存部品 | 扱い |
|---|---|---|
| 外部コマンド実行（chmod/codesign） | `EventLoopScriptBundler.MarkExecutable` の Process 起動 | 共通へ切り出し `ExternalToolRunner.Run` を両者から呼ぶ |
| LFS ポインタ判定（C#） | `CefLfsPointer.IsPointerFile` | 呼ぶ |
| LFS ポインタ判定（シェル） | `release_require_private_assets` 内の判定 | 関数化して Windows/Mac 両方から呼ぶ |
| build-info 検査 | `release-playtest.sh` の python 検査 | 移設して target 期待値を引数化し両 OS から呼ぶ |
| VDF トークン置換 | `release-playtest.sh` の sed ループ | 移設して呼ぶ |

## 配置と前例（spec-architecture-review 結果）

- 用途→方針の導出は `Client.Editor.Build`（Assembly-CSharp-Editor）内に置く。ビルド入口の契約はこの名前空間に閉じており（前例: `ReleaseLocalBuildCli.CreateRequest` が契約の単一点だった）、ランタイム層へは出さない。
- arm64 固定・再署名はビルドのオーケストレーション（`BuildPipeline.Execute`）から明示呼び出しする。前例: `CefRuntimeBundler`/`FfmpegRuntimeBundler` を同メソッドから呼ぶ形。購読・コールバック（`IPostprocessBuildWithReport`）は使わない（同梱の後に署名する順序を明示駆動で保証するため）。
- 同梱 ffmpeg の実行時位置は `FfmpegLocator`（Client.Game）が正で、ビルド側 `FfmpegRuntimeBundler` がその定数を参照する（前例: `BundledPluginsRelativeDirectory` の共有）。
- 新規パターン（ユーザー注目点）: `MacPlayerArchitecture` が `#if UNITY_EDITOR_OSX` で `UnityEditor.OSXStandalone.UserBuildSettings` を使う。この型は Mac ホストの Editor にだけ同梱される拡張アセンブリにあるため、Windows CI のコンパイルを壊さないようにホスト条件で囲む。非 Mac ホストで Mac を焼くと理由をログして失敗する。

## 死活表（既存操作への影響）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| メニュー WindowsBuild / MacOsBuild / LinuxBuild（開発） | 生きる。Mac 開発ビルドに展示会用スクリプトが入らなくなる（ADR 0071 裁定どおり） | `LocalDevelopment` 用途 |
| メニュー MacOsReleaseLocalBuild（展示会） | `MacOsExhibitionBuild` に改名して生きる | `Exhibition` 用途 |
| メニュー WindowsReleaseLocalBuild | `WindowsSteamPlaytestBuild` に改名。`MacOsSteamPlaytestBuild` を追加 | `SteamPlaytest` 用途 |
| CI `BuildPipeline.*BuildFromGithubAction` | 生きる。メソッド名不変。Mac CI 成果物は arm64＋再署名（非 strict なので失敗は警告） | `Ci` 用途 |
| `release-playtest.sh` の Windows 配布・検証機検証 | 生きる。env 名が `_WINDOWS` に変わる | Task 6 |

---

### Task 1: Mac 向け ffmpeg を非公開アセットへ追加しピンを更新する

**Files:**
- Create（非公開 repo）: `ffmpeg/macos-arm64/ffmpeg`, `ffmpeg/macos-arm64/LICENSE`
- Modify（非公開 repo）: `.gitattributes`
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Produces: 非公開 repo のパス `ffmpeg/macos-arm64/ffmpeg`（arm64 Mach-O・実行権付き）と `ffmpeg/macos-arm64/LICENSE`。Task 3・Task 6 がこのパスを使う。

- [ ] **Step 1: 非公開 repo にブランチを切る**

```bash
P=/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/steam-playtest-mac-build/moorestech_client/Assets/PersonalAssets/moorestech-client-private
git -C "$P" fetch origin && git -C "$P" switch -c feature/ffmpeg-macos-arm64 origin/main 2>/dev/null || git -C "$P" switch -c feature/ffmpeg-macos-arm64 origin/master
git -C "$P" log --oneline -1
```

- [ ] **Step 2: arm64 の静的 ffmpeg を取得して検査する**

osxexperts.net（https://www.osxexperts.net/）の Apple Silicon 向け ffmpeg 最新版 zip をスクラッチへ取得・展開する。次をすべて満たすことを確かめる（満たさなければ別の配布元を探し、Homebrew の動的リンク版は使わない）:

```bash
F=<展開した ffmpeg のパス>
file "$F"                      # Expected: Mach-O 64-bit executable arm64
otool -L "$F"                  # Expected: /usr/lib/ と /System/Library/ 以外の依存が無い
"$F" -version | head -3        # Expected: 終了コード0、configuration に --enable-gpl
"$F" -version | grep -o -- '--enable-version3'   # 出れば GPLv3、出なければ GPLv2+
shasum -a 256 "$F"             # コミットメッセージへ記録する
```

- [ ] **Step 3: 配置する**

```bash
mkdir -p "$P/ffmpeg/macos-arm64"
cp "$F" "$P/ffmpeg/macos-arm64/ffmpeg" && chmod +x "$P/ffmpeg/macos-arm64/ffmpeg"
# --enable-version3 があれば win-x64 と同じ GPLv3 本文を使う。無ければ https://www.gnu.org/licenses/old-licenses/gpl-2.0.txt を保存する
cp "$P/ffmpeg/win-x64/LICENSE" "$P/ffmpeg/macos-arm64/LICENSE"
echo 'ffmpeg/macos-arm64/ffmpeg filter=lfs diff=lfs merge=lfs -text' >> "$P/.gitattributes"
```

- [ ] **Step 4: Unity に .meta を生成させる**

`uloop launch <worktree>/moorestech_client` で Editor を起動し（起動済みなら `uloop compile --project-path <worktree>/moorestech_client`）、`ffmpeg/macos-arm64/ffmpeg.meta`・`LICENSE.meta`・`macos-arm64.meta` が生成されたことを `ls` で確認する。

- [ ] **Step 5: 非公開 repo でコミット・push・PR**

```bash
git -C "$P" add .gitattributes ffmpeg/macos-arm64 ffmpeg/macos-arm64.meta
git -C "$P" commit -m "feat(ffmpeg): Mac配布ビルド同梱用の ffmpeg macos-arm64 を追加

取得元: <URL> / sha256: <値> / ライセンス: <GPLv3|GPLv2+>

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git -C "$P" lfs ls-files | grep macos-arm64   # Expected: ffmpeg/macos-arm64/ffmpeg が LFS 管理
git -C "$P" push -u origin feature/ffmpeg-macos-arm64
gh pr create -R moorestech/moorestech-client-private --title "Mac配布ビルド同梱用の ffmpeg macos-arm64 を追加" --body "moorestech ADR 0071。Mac（Apple Silicon）配布ビルドのバグ報告録画用。

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

- [ ] **Step 6: 本 repo のピンを push 済みコミットへ更新してコミット**

`.moorestech-external-revisions.json` の `moorestech_client_private.commitHash` を `git -C "$P" rev-parse HEAD` の値へ書き換える。

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/steam-playtest-mac-build
git add .moorestech-external-revisions.json
git commit -m "chore: 非公開アセットのピンを Mac ffmpeg 追加コミットへ更新する

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: ビルド要求を用途 enum へ置き換え、Steam 配布入口を Windows/Mac の2本にする

**Files:**
- Create: `moorestech_client/Assets/Scripts/Editor/Build/BuildPurpose.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/Build/PlayerBuildRequest.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/Build/BuildPipeline.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/Build/BuildMenu.cs`
- Rename: `moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs`(+`.meta`) → `SteamPlaytestBuildCli.cs`(+`.meta`)

**Interfaces:**
- Produces: `enum Client.Editor.Build.BuildPurpose { Ci, LocalDevelopment, Exhibition, SteamPlaytest }`、`static class BuildPurposeRules { bool IsStrictBundling(BuildPurpose); bool BundlesLocalGameData(BuildPurpose); bool BundlesExhibitionLaunchScript(BuildPurpose); }`、`PlayerBuildRequest.Purpose`、batchmode 入口 `Client.Editor.Build.SteamPlaytestBuildCli.WindowsSteamPlaytestBuild()` / `MacOsSteamPlaytestBuild()`（Task 6 が `-executeMethod` で呼ぶ）。

Editor アセンブリ（asmdef 無し＝Assembly-CSharp-Editor）には NUnit テストの前例が無く Client.Tests から参照できないため、このタスクの検証はコンパイルと `uloop execute-dynamic-code` による規則の実行確認で行う。

- [ ] **Step 1: `BuildPurpose.cs` を作る**

```csharp
using System;

namespace Client.Editor.Build
{
    /// <summary>
    /// Playerビルドの目的。strict検査と同梱物はここからだけ導く（ADR 0071）
    /// The purpose of a Player build; strict checks and bundled content derive from it alone (ADR 0071)
    /// </summary>
    public enum BuildPurpose
    {
        Ci,
        LocalDevelopment,
        Exhibition,
        SteamPlaytest,
    }

    /// <summary>
    /// 用途ごとの同梱・検査方針の単一の導出点
    /// The single place deriving bundling and check policy from a purpose
    /// </summary>
    public static class BuildPurposeRules
    {
        // 人へ配る成果物だけは同梱・出所の問題でビルドを落とす
        // Only artifacts handed to people fail the build on bundling or origin problems
        public static bool IsStrictBundling(BuildPurpose purpose)
        {
            switch (purpose)
            {
                case BuildPurpose.Ci:
                case BuildPurpose.LocalDevelopment:
                    return false;
                case BuildPurpose.Exhibition:
                case BuildPurpose.SteamPlaytest:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }

        // CIはmaster data無しで焼くため同梱しない
        // CI builds without master data, so it never bundles game data
        public static bool BundlesLocalGameData(BuildPurpose purpose)
        {
            switch (purpose)
            {
                case BuildPurpose.Ci:
                    return false;
                case BuildPurpose.LocalDevelopment:
                case BuildPurpose.Exhibition:
                case BuildPurpose.SteamPlaytest:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }

        // 展示会ブースの再起動ループは展示会ビルドにだけ入れる
        // The booth restart loop ships with exhibition builds only
        public static bool BundlesExhibitionLaunchScript(BuildPurpose purpose)
        {
            switch (purpose)
            {
                case BuildPurpose.Exhibition:
                    return true;
                case BuildPurpose.Ci:
                case BuildPurpose.LocalDevelopment:
                case BuildPurpose.SteamPlaytest:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }
    }
}
```

- [ ] **Step 2: `PlayerBuildRequest.cs` のクラス部分を置き換える（`PlayerBuildOutcome` enum はそのまま）**

```csharp
    /// <summary>
    /// Playerビルド1回分の入力（入口ごとの違いは用途で表す）
    /// Input for one Player build; per-entry differences are expressed by the purpose
    /// </summary>
    public class PlayerBuildRequest
    {
        public BuildTarget Target;

        // 成果物を配置するディレクトリ（この直下に実行ファイルとgame/が並ぶ）
        // Directory receiving the artifact (player executable and game/ sit directly under it)
        public string OutputDirectory;

        public BuildPurpose Purpose;

        // 開発メニューで人が選ぶ。展示会・Steam配布はfalse、CIはtrueを入口が渡す
        // Chosen by a person in the dev menu; exhibition/Steam entries pass false and CI passes true
        public bool IsDevelopmentBuild;
    }
```

- [ ] **Step 3: `BuildPipeline.cs` を用途から方針を引く形にする**

`Execute` の先頭（`Debug.Log("Build Start Time ...")` の直後）に追加:

```csharp
            // 同梱・検査の方針は用途からだけ導く
            // Bundling and check policy derive from the purpose alone
            var isStrictBundling = BuildPurposeRules.IsStrictBundling(request.Purpose);
```

`BuildInfoWriter.Write(request.IsStrictBundling, request.Target);` を `BuildInfoWriter.Write(isStrictBundling, request.Target);` に、成功時ブロックを次に置き換える:

```csharp
            if (report.summary.result == BuildResult.Succeeded)
            {
                CefRuntimeBundler.Bundle(request.Target, report.summary.outputPath, isStrictBundling);
                FfmpegRuntimeBundler.Bundle(request.Target, report.summary.outputPath, isStrictBundling);
                if (BuildPurposeRules.BundlesLocalGameData(request.Purpose))
                {
                    GameDataBundler.Bundle(request.OutputDirectory, isStrictBundling);
                    WorldSnapshotBundler.Bundle(request.OutputDirectory, isStrictBundling);
                }

                // 展示会の起動ループは展示会ビルドにだけ入れる（Steam配布のMac版へ混ぜない）
                // The exhibition loop ships only with exhibition builds, never with the Steam Mac artifact
                if (BuildPurposeRules.BundlesExhibitionLaunchScript(request.Purpose))
                    EventLoopScriptBundler.Bundle(request.OutputDirectory, isStrictBundling);
            }
```

`BuildFromGithubAction` の要求生成を次にする:

```csharp
            var outcome = Execute(new PlayerBuildRequest
            {
                Target = buildTarget,
                OutputDirectory = "Output_" + buildTarget,
                Purpose = BuildPurpose.Ci,
                IsDevelopmentBuild = true,
            });
```

- [ ] **Step 4: `ReleaseLocalBuildCli` を改名する**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/steam-playtest-mac-build/moorestech_client/Assets/Scripts/Editor/Build
git mv ReleaseLocalBuildCli.cs SteamPlaytestBuildCli.cs
git mv ReleaseLocalBuildCli.cs.meta SteamPlaytestBuildCli.cs.meta
```

`SteamPlaytestBuildCli.cs` の全文:

```csharp
using System;
using UnityEditor;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// Steamプレイテスト配布ビルドの無人（batchmode）入口。release-playtest.sh が -executeMethod で呼ぶ
    /// Unattended (batchmode) entries for the Steam playtest build, called by release-playtest.sh via -executeMethod
    /// </summary>
    public static class SteamPlaytestBuildCli
    {
        private const string OutputDirectoryEnvKey = "MOORESTECH_BUILD_OUTPUT";

        public static void WindowsSteamPlaytestBuild()
        {
            BuildFromEnvironment(BuildTarget.StandaloneWindows64);
        }

        public static void MacOsSteamPlaytestBuild()
        {
            BuildFromEnvironment(BuildTarget.StandaloneOSX);
        }

        private static void BuildFromEnvironment(BuildTarget target)
        {
            // 出力先未指定で走らせるとカレント直下を汚すため、理由を残して拒否する
            // Running without an output directory would pollute the CWD, so refuse and log why
            var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryEnvKey);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Debug.LogError($"[SteamPlaytestBuildCli] {OutputDirectoryEnvKey} が未設定のためビルドしません");
                EditorApplication.Exit(2);
                return;
            }

            var outcome = BuildPipeline.Execute(new PlayerBuildRequest
            {
                Target = target,
                OutputDirectory = outputDirectory,
                Purpose = BuildPurpose.SteamPlaytest,
                IsDevelopmentBuild = false,
            });
            Debug.Log($"[SteamPlaytestBuildCli] outcome:{outcome} target:{target} output:{outputDirectory}");
            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }
    }
}
```

- [ ] **Step 5: `BuildMenu.cs` のメニューを整理する**

`MacOsReleaseLocalBuild` と `WindowsReleaseLocalBuild` の2メソッドを次の3つに置き換える:

```csharp
        // 展示会ブース用。再起動ループを同梱しRelease固定で焼く
        // For the exhibition booth: bundles the restart loop and always builds Release
        [MenuItem("moorestech/Build/MacOsExhibitionBuild")]
        public static void MacOsExhibitionBuild()
        {
            BuildDistributionInteractive(BuildTarget.StandaloneOSX, BuildPurpose.Exhibition);
        }

        // Steamプレイテスト配布の成果物を手元で焼く。無人入口 SteamPlaytestBuildCli と同じ用途
        // Builds the Steam playtest artifact by hand; same purpose as the unattended SteamPlaytestBuildCli
        [MenuItem("moorestech/Build/WindowsSteamPlaytestBuild")]
        public static void WindowsSteamPlaytestBuild()
        {
            BuildDistributionInteractive(BuildTarget.StandaloneWindows64, BuildPurpose.SteamPlaytest);
        }

        [MenuItem("moorestech/Build/MacOsSteamPlaytestBuild")]
        public static void MacOsSteamPlaytestBuild()
        {
            BuildDistributionInteractive(BuildTarget.StandaloneOSX, BuildPurpose.SteamPlaytest);
        }
```

`BuildInteractive` 内の要求生成を次にする（コメントも差し替え）:

```csharp
            // 開発用: 同梱・出所の問題は警告で続行する。strictは人へ配る用途に限る
            // Development use: bundling/origin problems warn and continue; strict is reserved for purposes handed to people
            var outcome = BuildPipeline.Execute(new PlayerBuildRequest
            {
                Target = buildTarget,
                OutputDirectory = outputDirectory,
                Purpose = BuildPurpose.LocalDevelopment,
                IsDevelopmentBuild = isDevelopmentBuild,
            });
```

`BuildReleaseLocalInteractive` を次に置き換える:

```csharp
        private static void BuildDistributionInteractive(BuildTarget buildTarget, BuildPurpose purpose)
        {
            var outputDirectory = SelectOutputDirectory(buildTarget);
            if (outputDirectory == null) return;
            ReportOutcome(BuildPipeline.Execute(new PlayerBuildRequest
            {
                Target = buildTarget,
                OutputDirectory = outputDirectory,
                Purpose = purpose,
                IsDevelopmentBuild = false,
            }), outputDirectory);
        }
```

- [ ] **Step 6: 旧名の残りが無いことを確認する**

Run: `git grep -n -e IsStrictBundling -e BundleLocalGameData -e ReleaseLocalBuildCli -e ReleaseLocalBuild -- moorestech_client scripts .github`
Expected: `scripts/playtest/release-playtest.sh` の `ReleaseLocalBuildCli.WindowsReleaseLocalBuild` 1件だけ（Task 6 で直す）。`.cs` のヒットは0件。

- [ ] **Step 7: コンパイルと規則の実行確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

Run: `uloop execute-dynamic-code --project-path ./moorestech_client` に次を渡す:
```csharp
var r = new System.Text.StringBuilder();
foreach (Client.Editor.Build.BuildPurpose p in System.Enum.GetValues(typeof(Client.Editor.Build.BuildPurpose)))
    r.AppendLine($"{p} strict={Client.Editor.Build.BuildPurposeRules.IsStrictBundling(p)} game={Client.Editor.Build.BuildPurposeRules.BundlesLocalGameData(p)} loop={Client.Editor.Build.BuildPurposeRules.BundlesExhibitionLaunchScript(p)}");
return r.ToString();
```
Expected:
```
Ci strict=False game=False loop=False
LocalDevelopment strict=False game=True loop=False
Exhibition strict=True game=True loop=True
SteamPlaytest strict=True game=True loop=False
```

- [ ] **Step 8: コミット**

```bash
git add moorestech_client/Assets/Scripts/Editor/Build
git commit -m "refactor(build): ビルド要求を用途enumで表し展示会ビルドとSteam配布ビルドを分ける (ADR 0071)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Mac Player 用の同梱 ffmpeg を実行時に見つけ、ビルドで同梱する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Recording/FfmpegLocator.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/FfmpegLocatorTest.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/Build/ExternalToolRunner.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/Build/Bundlers/EventLoopScriptBundler.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/Build/Bundlers/FfmpegRuntimeBundler.cs`

**Interfaces:**
- Consumes: Task 1 の非公開アセット `ffmpeg/macos-arm64/ffmpeg`・`LICENSE`。
- Produces: `FfmpegLocator.BundledMacExecutableRelativePath`（`"MacOS/ffmpeg"`、`.app/Contents` からの相対）、`FfmpegLocator.ResolveBundledPath(string dataPath, RuntimePlatform platform) : string`、`ExternalToolRunner.Run(string fileName, string arguments) : int`（Task 4 が使う）。Mac 成果物の `moorestech.app/Contents/MacOS/ffmpeg` と `Contents/Resources/ffmpeg-LICENSE.txt`（Task 4 の署名と Task 6 のシェル検査が使う）。

- [ ] **Step 1: 失敗するテストを書く**

`FfmpegLocatorTest.cs` のクラス末尾に追加:

```csharp
        // Mac Playerは.app/Contents/MacOS/ffmpegを同梱位置として探す
        // A Mac player looks for the bundled copy at .app/Contents/MacOS/ffmpeg
        [Test]
        public void MacPlayerの同梱位置はContents配下のMacOS()
        {
            var dataPath = Path.Combine("moorestech.app", "Contents");
            var path = FfmpegLocator.ResolveBundledPath(dataPath, UnityEngine.RuntimePlatform.OSXPlayer);
            Assert.AreEqual(Path.Combine(dataPath, "MacOS", "ffmpeg"), path);
        }

        [Test]
        public void WindowsPlayerの同梱位置はPluginsのx86_64()
        {
            var dataPath = "moorestech_Data";
            var path = FfmpegLocator.ResolveBundledPath(dataPath, UnityEngine.RuntimePlatform.WindowsPlayer);
            Assert.AreEqual(Path.Combine(dataPath, "Plugins", "x86_64", "ffmpeg.exe"), path);
        }

        // Editorには同梱物が無いので同梱位置を持たない
        // The Editor has no bundled copy, so it has no bundled location
        [Test]
        public void Editorには同梱位置が無い()
        {
            Assert.AreEqual(string.Empty, FfmpegLocator.ResolveBundledPath("Assets", UnityEngine.RuntimePlatform.OSXEditor));
        }
```

- [ ] **Step 2: 失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.BugReport.FfmpegLocatorTest"`
Expected: コンパイルエラー（`ResolveBundledPath` が無い）

- [ ] **Step 3: `FfmpegLocator` を実装する**

定数群と `Find` を次に置き換える（`ResolveInitialAvailability`・`FindIn` はそのまま）:

```csharp
        public const string MissingFfmpegReason = "ffmpeg が見つかりません（配布物同梱 Windows: moorestech_Data/Plugins/x86_64/ffmpeg.exe・Mac: moorestech.app/Contents/MacOS/ffmpeg・MOORESTECH_FFMPEG・PATH・既知の場所 /opt/homebrew/bin, /usr/local/bin のいずれにも無い）";
        // 同梱ffmpeg実行ファイル名
        // The bundled ffmpeg executable names
        public const string BundledWindowsExecutableName = "ffmpeg.exe";
        public const string BundledMacExecutableName = "ffmpeg";
        // 同梱先の Player データフォルダ（<exe>_Data）からの相対ディレクトリ。ビルド側の同梱先と実行時の探索先で共有する
        // The bundled directory relative to the player data folder (<exe>_Data), shared by the build bundler and the runtime lookup
        public static readonly string BundledPluginsRelativeDirectory = Path.Combine("Plugins", "x86_64");
        // Mac Playerの Application.dataPath（.app/Contents）からの相対パス。署名対象のコード置き場 MacOS に置く
        // Relative to a Mac player's Application.dataPath (.app/Contents); placed in MacOS, where signed code lives
        public static readonly string BundledMacExecutableRelativePath = Path.Combine("MacOS", BundledMacExecutableName);

        private static readonly string[] KnownPaths = { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg" };

        // 同梱→環境変数→PATH→既知の場所の順に探す。無ければ null（呼び出し側が縮退を記録する）
        // Search the bundled copy, then env var, then PATH, then known locations; null if absent (the caller records the degradation)
        public static string Find()
        {
            var bundledPath = ResolveBundledPath(Application.dataPath, Application.platform);
            var pathExecutableName = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer
                ? BundledWindowsExecutableName
                : BundledMacExecutableName;
            return FindIn(bundledPath, global::System.Environment.GetEnvironmentVariable("MOORESTECH_FFMPEG"), global::System.Environment.GetEnvironmentVariable("PATH"), pathExecutableName, KnownPaths);
        }

        // 実行中のPlayer種別ごとの同梱位置。同梱物の無いEditor等は空文字（FindInが飛ばす）
        // The bundled location per running player kind; empty for the Editor and others without a bundle (FindIn skips it)
        public static string ResolveBundledPath(string dataPath, RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer:
                    return Path.Combine(dataPath, BundledPluginsRelativeDirectory, BundledWindowsExecutableName);
                case RuntimePlatform.OSXPlayer:
                    return Path.Combine(dataPath, BundledMacExecutableRelativePath);
                default:
                    return string.Empty;
            }
        }
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.BugReport.FfmpegLocatorTest"`
Expected: 全件 PASS（`ffmpegのある環境では実在するパスを返す` は環境により Ignore 可）

- [ ] **Step 5: `ExternalToolRunner.cs` を作る**

```csharp
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Client.Editor.Build
{
    /// <summary>
    /// ビルド後処理で使うOSコマンド（chmod・codesign）を実行し終了コードを返す
    /// Runs OS commands used after the build (chmod, codesign) and returns the exit code
    /// </summary>
    internal static class ExternalToolRunner
    {
        // 外部プロセス境界: 権限付与と署名は.NET Standard 2.1にAPIが無いためOSのコマンドへ委譲する
        // External process boundary: .NET Standard 2.1 has no permission or signing API, so delegate to OS commands
        public static int Run(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments) { UseShellExecute = false, RedirectStandardError = true };
            var process = Process.Start(startInfo);
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // 失敗理由は呼び出し側の判断より先にそのまま残す
            // Keep the raw failure reason before the caller decides what to do
            if (process.ExitCode != 0) Debug.LogWarning($"[ExternalToolRunner] {fileName} {arguments} exited {process.ExitCode}: {standardError}");
            return process.ExitCode;
        }
    }
}
```

- [ ] **Step 6: `EventLoopScriptBundler.MarkExecutable` を `ExternalToolRunner` 呼び出しにする**

`MarkExecutable` の本体を次に置き換え、不要になった `using System.Diagnostics;` と `using Debug = UnityEngine.Debug;` の別名を外す（`Debug` は `UnityEngine` の using で解決する）:

```csharp
        private static void MarkExecutable(string filePath, bool isStrict)
        {
            if (ExternalToolRunner.Run("/bin/chmod", $"+x \"{filePath}\"") == 0) return;

            if (isStrict) throw new BuildFailedException($"[EventLoopScriptBundler] chmod failed: {filePath}");
            Debug.LogWarning($"[EventLoopScriptBundler] chmod failed: {filePath}");
        }
```

- [ ] **Step 7: `FfmpegRuntimeBundler` を Mac 対応にする**

クラス全体を次にする（usings は `System.IO`・`Client.Game.InGame.BugReport.Recording`・`Client.Editor`・`UnityEditor`・`UnityEditor.Build`・`UnityEngine`）:

```csharp
    /// <summary>
    /// バグ報告の録画組み立てに使う ffmpeg を Windows/Mac 成果物へ同梱する
    /// Bundles the ffmpeg the bug-report video assembly uses into the Windows and Mac artifacts
    /// ライセンス上、実行ファイルと一緒に配布物へライセンス文を並べる必要がある
    /// The license requires the license text to ship alongside the executable
    /// </summary>
    public static class FfmpegRuntimeBundler
    {
        private const string SourceLicenseName = "LICENSE";
        private const string BundledLicenseName = "ffmpeg-LICENSE.txt";

        // 正本は非公開アセットリポジトリの ffmpeg/<os-arch>
        // The source of truth is ffmpeg/<os-arch> in the private asset repository
        private static string SourceDirectory(string platformDirectoryName) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "PersonalAssets", "moorestech-client-private", "ffmpeg", platformDirectoryName));

        public static void Bundle(BuildTarget buildTarget, string playerOutputPath, bool isStrict)
        {
            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows64:
                    BundleWindows();
                    break;
                case BuildTarget.StandaloneOSX:
                    BundleMacOs();
                    break;
                default:
                    Debug.Log($"[FfmpegRuntimeBundler] skipped for {buildTarget}");
                    break;
            }

            #region Internal

            void BundleWindows()
            {
                // 同梱先はCEFランタイムと同じ Plugins/x86_64。実行時の探索位置を1箇所に揃える
                // Ships into Plugins/x86_64 next to the CEF runtime so runtime lookup has a single location
                var sourceDirectory = SourceDirectory("win-x64");
                var destinationDirectory = WindowsPlayerPluginsDirectory.Resolve(playerOutputPath);
                if (!Directory.Exists(destinationDirectory))
                {
                    Fail($"Plugins/x86_64 not found in build output: {destinationDirectory}");
                    return;
                }
                CopyExecutableAndLicense(sourceDirectory, FfmpegLocator.BundledWindowsExecutableName,
                    Path.Combine(destinationDirectory, FfmpegLocator.BundledWindowsExecutableName), Path.Combine(destinationDirectory, BundledLicenseName));
            }

            void BundleMacOs()
            {
                // 実行ファイルは署名対象のContents/MacOS、ライセンス文は非コードなのでContents/Resourcesへ置く
                // The executable goes into signed Contents/MacOS; the license is not code, so it goes into Contents/Resources
                var contentsDirectory = Path.Combine(playerOutputPath, "Contents");
                var executableDestination = Path.Combine(contentsDirectory, FfmpegLocator.BundledMacExecutableRelativePath);
                if (!Directory.Exists(Path.GetDirectoryName(executableDestination)))
                {
                    Fail($"Contents/MacOS not found in build output: {contentsDirectory}");
                    return;
                }
                if (!CopyExecutableAndLicense(SourceDirectory("macos-arm64"), FfmpegLocator.BundledMacExecutableName,
                        executableDestination, Path.Combine(contentsDirectory, "Resources", BundledLicenseName))) return;

                // コピーで実行権が落ちうるため付け直す
                // The copy may drop the executable bit, so restore it
                if (ExternalToolRunner.Run("/bin/chmod", $"+x \"{executableDestination}\"") != 0) Fail($"chmod failed: {executableDestination}");
            }

            bool CopyExecutableAndLicense(string sourceDirectory, string executableName, string executableDestination, string licenseDestination)
            {
                var sourceExecutable = Path.Combine(sourceDirectory, executableName);
                var sourceLicense = Path.Combine(sourceDirectory, SourceLicenseName);

                // 実体の検証（LFS未解決の殻でないこと）。CEF前例と同じ判定点を使う
                // Verify the executable is real, not an unresolved LFS husk, using the same check as the CEF precedent
                if (!File.Exists(sourceExecutable) || CefLfsPointer.IsPointerFile(sourceExecutable))
                {
                    Fail($"ffmpeg executable is missing or an LFS pointer: {sourceExecutable}");
                    return false;
                }
                if (!File.Exists(sourceLicense))
                {
                    Fail($"ffmpeg LICENSE is missing: {sourceLicense}");
                    return false;
                }

                File.Copy(sourceExecutable, executableDestination, true);
                File.Copy(sourceLicense, licenseDestination, true);
                Debug.Log($"[FfmpegRuntimeBundler] bundled ffmpeg at {executableDestination}");
                return true;
            }

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
```

- [ ] **Step 8: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → Expected: ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BugReport"` → Expected: 失敗0

- [ ] **Step 9: コミット**

```bash
git add moorestech_client/Assets/Scripts
git commit -m "feat(build): Mac配布ビルドへffmpegを同梱し実行時に同梱位置から見つける (ADR 0071)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Mac ビルドを arm64 に固定し、同梱後に ad-hoc 再署名する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Editor/Build/MacPlayerArchitecture.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/Build/MacAppAdHocSigner.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/Build/BuildPipeline.cs`

**Interfaces:**
- Consumes: `ExternalToolRunner.Run`（Task 3）、`FfmpegLocator.BundledMacExecutableRelativePath`（Task 3）。
- Produces: `MacPlayerArchitecture.TryPinAppleSilicon() : bool`、`MacAppAdHocSigner.Sign(string appPath, bool isStrict) : void`。Mac 成果物が `codesign --verify --deep --strict` を通り、主実行ファイルが arm64 のみ。

- [ ] **Step 1: `MacPlayerArchitecture.cs` を作る**

```csharp
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// Mac Playerをarm64専用に固定する（CEFのMacランタイムがosx-arm64しか無いため。ADR 0071）
    /// Pins the Mac player to arm64 only, since CEF's Mac runtime exists only for osx-arm64 (ADR 0071)
    /// </summary>
    internal static class MacPlayerArchitecture
    {
        public static bool TryPinAppleSilicon()
        {
#if UNITY_EDITOR_OSX
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
            Debug.Log("[MacPlayerArchitecture] macOS player architecture pinned to ARM64");
            return true;
#else
            // この設定の型はMacホストのEditorにだけある拡張アセンブリに属するため、他ホストからのMacビルドは理由を残して止める
            // The setting's type lives in an extension assembly shipped only with Mac-host Editors, so a Mac build from another host stops with a reason
            Debug.LogError("[MacPlayerArchitecture] macOS向けビルドはmacOSホストのEditorでしかarm64へ固定できないため中止します");
            return false;
#endif
        }
    }
}
```

- [ ] **Step 2: `MacAppAdHocSigner.cs` を作る**

```csharp
using System.IO;
using Client.Game.InGame.BugReport.Recording;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// 同梱を終えた.appをad-hoc署名し直す。ビルド後に入れたCEF helperとffmpegでUnityの署名の封印が崩れるため（ADR 0071）
    /// Re-signs the bundled .app ad-hoc, because the CEF helper and ffmpeg added after the build break Unity's signature seal (ADR 0071)
    /// </summary>
    internal static class MacAppAdHocSigner
    {
        private const string CodesignPath = "/usr/bin/codesign";

        public static void Sign(string appPath, bool isStrict)
        {
            // 入れ子のコードを先に署名してから.app全体を封印する
            // Sign nested code first, then seal the whole .app
            var bundledFfmpeg = Path.Combine(appPath, "Contents", FfmpegLocator.BundledMacExecutableRelativePath);
            if (File.Exists(bundledFfmpeg) && ExternalToolRunner.Run(CodesignPath, $"--force -s - \"{bundledFfmpeg}\"") != 0)
            {
                Fail($"codesign failed for bundled ffmpeg: {bundledFfmpeg}");
                return;
            }
            if (ExternalToolRunner.Run(CodesignPath, $"--force --deep -s - \"{appPath}\"") != 0)
            {
                Fail($"codesign failed for app: {appPath}");
                return;
            }

            // 署名が実際に通るかを配布前に確かめる
            // Confirm before distribution that the signature actually verifies
            if (ExternalToolRunner.Run(CodesignPath, $"--verify --deep --strict \"{appPath}\"") != 0)
            {
                Fail($"codesign verification failed: {appPath}");
                return;
            }
            Debug.Log($"[MacAppAdHocSigner] ad-hoc signed and verified: {appPath}");

            #region Internal

            void Fail(string message)
            {
                // strict時は起動保証の無い成果物を配らない。CI互換時は警告のみ
                // Strict mode never ships an artifact without a launch guarantee; CI-compatible mode only warns
                if (isStrict) throw new BuildFailedException("[MacAppAdHocSigner] " + message);
                Debug.LogWarning("[MacAppAdHocSigner] " + message);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: `BuildPipeline.Execute` から呼ぶ**

ターゲット切替の `if (... SwitchActiveBuildTarget ...) { ... }` ブロックの直後に追加:

```csharp
            // CEFのMacランタイムがarm64のみのため、Macは焼く前にarm64へ固定する
            // CEF's Mac runtime is arm64 only, so pin the Mac player to arm64 before building
            if (request.Target == BuildTarget.StandaloneOSX && !MacPlayerArchitecture.TryPinAppleSilicon())
            {
                return PlayerBuildOutcome.PlayerBuildFailed;
            }
```

成功時ブロックの末尾（`EventLoopScriptBundler` の条件文の後、ブロックの閉じ括弧の前）に追加:

```csharp
                // 同梱で崩れた署名を最後にまとめて張り直す
                // Re-seal the signature broken by bundling, as the very last step
                if (request.Target == BuildTarget.StandaloneOSX)
                    MacAppAdHocSigner.Sign(report.summary.outputPath, isStrictBundling);
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0。`UnityEditor.OSXStandalone` が解決できないエラーが出た場合は、`UnityEditor.EditorUserBuildSettings.SetPlatformSettings(UnityEditor.BuildPipeline.GetBuildTargetName(UnityEditor.BuildTarget.StandaloneOSX), "Architecture", ...)` の値を `uloop execute-dynamic-code` で `GetPlatformSettings` を読んで確かめてから、その値で書く形へ切り替え、その経緯を plan の判断記録へ追記する。

- [ ] **Step 5: 行数と1ディレクトリの本数を確認する**

Run: `wc -l moorestech_client/Assets/Scripts/Editor/Build/*.cs moorestech_client/Assets/Scripts/Editor/Build/Bundlers/*.cs; ls moorestech_client/Assets/Scripts/Editor/Build/*.cs | wc -l`
Expected: すべて200行未満、`Editor/Build/*.cs` は9本。

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Editor/Build
git commit -m "feat(build): Macビルドをarm64に固定し同梱後にad-hoc再署名する (ADR 0071)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: 契約テストのスタブを Mac 対応にし、Mac 版の失敗系テストを書く（赤）

**Files:**
- Create: `scripts/playtest/tests/lib/release-playtest-build-stubs.sh`
- Modify: `scripts/playtest/tests/lib/release-playtest-sandbox.sh`
- Modify: `scripts/playtest/tests/test-release-playtest.sh`
- Create: `scripts/playtest/tests/test-release-playtest-mac.sh`

**Interfaces:**
- Produces: sandbox の env ノブ `MOORESTECH_STEAM_DEPOT_ID_WINDOWS`(既定1958161)・`MOORESTECH_STEAM_DEPOT_ID_MAC`(既定1958162)・`UNITY_MAC_EXIT`・`CODESIGN_EXIT`・`LIPO_ARCHS`(既定`arm64`)・`MAC_LEAKS_EVENT_SCRIPT`・`FFMPEG_MAC_STATE`(`real`/`missing`/`lfs`)と、スクリプトへ渡す `CODESIGN_BIN`・`LIPO_BIN`。Task 6 の実装はこれらを満たす。

- [ ] **Step 1: `release-playtest-build-stubs.sh` を作る**

```bash
#!/usr/bin/env bash
# release-playtest.sh の契約テスト用に、ビルドと Mac 成果物検査のコマンド（unity/codesign/lipo）のスタブを書く
# 引数の sandbox に bin/ を作り、呼び出しを calls.log へ記録する（bash 3.2 で動く書き方に限る）
# Writes stubs for the build and Mac-artifact-check commands (unity/codesign/lipo) used by the release-playtest.sh contract tests
# Creates bin/ under the given sandbox and records invocations into calls.log (written for bash 3.2)

write_build_stubs() {
    local sandbox="$1" commit="$2"
    # executeMethod で OS を見分け、それぞれの成果物レイアウトと build-info.json を作る
    # Tell the OS apart by executeMethod and lay out each artifact with its build-info.json
    cat >"$sandbox/bin/unity" <<EOF
#!/bin/bash
echo "unity \$* branch=\$MOORESTECH_BUILD_BRANCH masterRoot=\$MOORESTECH_MASTER_DATA_ROOT" >>"$sandbox/calls.log"
out="\$MOORESTECH_BUILD_OUTPUT"
case "\$*" in
  *MacOsSteamPlaytestBuild*)
    [ "\${UNITY_MAC_EXIT:-0}" = "0" ] || exit "\${UNITY_MAC_EXIT}"
    app="\$out/moorestech.app"
    mkdir -p "\$app/Contents/MacOS" "\$app/Contents/Resources/Data/StreamingAssets" "\$out/game/mods"
    touch "\$app/Contents/MacOS/moorestech" "\$app/Contents/MacOS/ffmpeg" "\$app/Contents/Resources/ffmpeg-LICENSE.txt"
    [ "\${MAC_LEAKS_EVENT_SCRIPT:-0}" = "0" ] || touch "\$out/start-gamescom-loop.command"
    info="\$app/Contents/Resources/Data/StreamingAssets/build-info.json"
    target=StandaloneOSX
    ;;
  *)
    [ "\${UNITY_EXIT:-0}" = "0" ] || exit "\${UNITY_EXIT}"
    mkdir -p "\$out/moorestech_Data/StreamingAssets" "\$out/game/mods"
    touch "\$out/moorestech.exe"
    info="\$out/moorestech_Data/StreamingAssets/build-info.json"
    target=StandaloneWindows64
    ;;
esac
printf '{"commit":"%s","branch":"%s","steamBuildLabel":"%s","target":"%s"}' \\
  "\${BUILD_INFO_COMMIT:-$commit}" "\${BUILD_INFO_BRANCH:-\$MOORESTECH_BUILD_BRANCH}" "\$MOORESTECH_STEAM_BUILD_LABEL" "\$target" >"\$info"
EOF
    cat >"$sandbox/bin/codesign" <<EOF
#!/bin/bash
echo "codesign \$*" >>"$sandbox/calls.log"
exit "\${CODESIGN_EXIT:-0}"
EOF
    cat >"$sandbox/bin/lipo" <<EOF
#!/bin/bash
echo "lipo \$*" >>"$sandbox/calls.log"
echo "\${LIPO_ARCHS:-arm64}"
EOF
    chmod +x "$sandbox/bin/unity" "$sandbox/bin/codesign" "$sandbox/bin/lipo"
}
```

- [ ] **Step 2: sandbox を改修する**

`release-playtest-sandbox.sh` で:
1. 先頭の定数定義の後に `. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/release-playtest-build-stubs.sh"` を追加する。
2. `make_sandbox` 内の `cat >"$SANDBOX/bin/unity" <<EOF ... EOF` ブロックを削除し、`chmod +x "$SANDBOX/bin/"*` の直前に `write_build_stubs "$SANDBOX" "$COMMIT"` を置く。
3. moores-wt スタブの ffmpeg 生成部の直後（`echo "$SANDBOX/wt"` の前）に Mac ffmpeg を追加する:
```bash
mac_ffmpeg_dir="$SANDBOX/wt/$PRIVATE_REL/ffmpeg/macos-arm64"
mkdir -p "\$mac_ffmpeg_dir"
touch "\$mac_ffmpeg_dir/LICENSE"
case "\${FFMPEG_MAC_STATE:-real}" in
  real) head -c 4096 /dev/zero >"\$mac_ffmpeg_dir/ffmpeg" ;;
  lfs) printf 'version https://git-lfs.github.com/spec/v1\noid sha256:00\nsize 1\n' >"\$mac_ffmpeg_dir/ffmpeg" ;;
esac
```
4. `run_target` の env 行 `MOORESTECH_STEAM_DEPOT_ID="${MOORESTECH_STEAM_DEPOT_ID-1958161}" \` を次に置き換える:
```bash
      MOORESTECH_STEAM_DEPOT_ID_WINDOWS="${MOORESTECH_STEAM_DEPOT_ID_WINDOWS-1958161}" \
      MOORESTECH_STEAM_DEPOT_ID_MAC="${MOORESTECH_STEAM_DEPOT_ID_MAC-1958162}" \
      CODESIGN_BIN="$SANDBOX/bin/codesign" LIPO_BIN="$SANDBOX/bin/lipo" \
      UNITY_MAC_EXIT="${UNITY_MAC_EXIT-0}" CODESIGN_EXIT="${CODESIGN_EXIT-0}" LIPO_ARCHS="${LIPO_ARCHS-arm64}" \
      MAC_LEAKS_EVENT_SCRIPT="${MAC_LEAKS_EVENT_SCRIPT-0}" FFMPEG_MAC_STATE="${FFMPEG_MAC_STATE-real}" \
```
5. `wc -l` で sandbox が200行未満であることを確認する。

- [ ] **Step 3: `test-release-playtest.sh` を2 depot 前提へ更新する**

- 成功系の順序期待を `"git git git moores-wt git git git git git git git git git unity unity codesign lipo steamcmd verify moores-wt "` にする。
- `grep -q "1958161" .../depot_build_windows.vdf` の行の後に追加:
```bash
grep -q "1958162" "$SANDBOX"/runs/*/steam/depot_build_mac.vdf || fail "mac depot id was not substituted"
grep -q '"1958161"' "$SANDBOX"/runs/*/steam/app_build_playtest.vdf || fail "app build did not list the windows depot"
grep -q '"1958162"' "$SANDBOX"/runs/*/steam/app_build_playtest.vdf || fail "app build did not list the mac depot"
grep -q "/build-windows" "$SANDBOX"/runs/*/steam/depot_build_windows.vdf || fail "windows depot contentroot is not build-windows"
grep -q "/build-mac" "$SANDBOX"/runs/*/steam/depot_build_mac.vdf || fail "mac depot contentroot is not build-mac"
grep -q 'Mac 版の手動確認' "$SANDBOX"/runs/*/promotion.md || fail "promotion.md lacks the Mac manual check"
grep -q '回避操作' "$SANDBOX"/runs/*/promotion.md || fail "promotion.md lacks the workaround record"
grep -q '案内' "$SANDBOX"/runs/*/promotion.md || fail "promotion.md does not say workarounds are guided, not blocking"
```
- 「必須envの欠落」ケースの `MOORESTECH_STEAM_DEPOT_ID=""` と期待語を `MOORESTECH_STEAM_DEPOT_ID_WINDOWS` に、非数字ケースの `MOORESTECH_STEAM_DEPOT_ID="1958161|x"` を `MOORESTECH_STEAM_DEPOT_ID_WINDOWS="1958161|x"` にする。
- 自前 unity スタブを書く「steamBuildLabel キー」ケースは、Windows 呼び出しでキー欠落の build-info を書く現行内容のまま残す（Mac 呼び出しより前に Windows 検査で落ちる想定ではなく、両ビルド後の検査で落ちる。どちらでも steamcmd に届かないことだけを見る）。

- [ ] **Step 4: `test-release-playtest-mac.sh` を作る**

```bash
#!/usr/bin/env bash
# release-playtest.sh の Mac 版に関する契約。どの失敗でも steamcmd へ進まず、理由を出して止まる
# Contract for the Mac side of release-playtest.sh: any failure stops before steamcmd with a reason
# fail()で集計してFAILURES件数を末尾判定する方式のため、set -eは使わない
# Aggregated via fail() and judged by FAILURES at the end, so set -e is not used
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/release-playtest-sandbox.sh
. "$SCRIPT_DIR/lib/release-playtest-sandbox.sh"

# 両OSが同じworktreeからWindows→Macの順に焼かれ、出力先がOS別に分かれる
# Both OSes are baked from one worktree, Windows then Mac, into per-OS output dirs
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
UNITY_LINES=$(grep "^unity" "$SANDBOX/calls.log")
echo "$UNITY_LINES" | sed -n 1p | grep -q "WindowsSteamPlaytestBuild" || fail "first unity call was not the Windows build"
echo "$UNITY_LINES" | sed -n 2p | grep -q "MacOsSteamPlaytestBuild" || fail "second unity call was not the Mac build"
[ "$(grep -c "^steamcmd" "$SANDBOX/calls.log")" -eq 1 ] || fail "steamcmd was not called exactly once"
ls -d "$SANDBOX"/runs/*/build-windows "$SANDBOX"/runs/*/build-mac >/dev/null 2>&1 || fail "per-OS build dirs were not created"

# Mac 側のどの失敗でも steamcmd へ進まない（ケース名 環境変数代入 の組）
# No Mac-side failure reaches steamcmd (pairs of case name and env assignment)
for case_spec in "mac-build UNITY_MAC_EXIT=1" "codesign CODESIGN_EXIT=1" "universal LIPO_ARCHS=x86_64_arm64" \
    "intel LIPO_ARCHS=x86_64" "event-script MAC_LEAKS_EVENT_SCRIPT=1"; do
    name="${case_spec%% *}"
    assignment="${case_spec#* }"
    make_sandbox
    OUTPUT=$(eval "$assignment" run_target); STATUS=$?
    [ "$STATUS" -ne 0 ] || fail "$name did not fail the run"
    grep -q "^steamcmd" "$SANDBOX/calls.log" && fail "steamcmd ran despite $name"
    grep -q "^moores-wt .*rm" "$SANDBOX/calls.log" || fail "worktree was not torn down after $name"
done

# Mac の depot id の欠落・非数字はビルド前に exit 2
# A missing or non-numeric Mac depot id exits 2 before building
for value in "" "12a"; do
    make_sandbox
    OUTPUT=$(MOORESTECH_STEAM_DEPOT_ID_MAC="$value" run_target); STATUS=$?
    [ "$STATUS" -eq 2 ] || fail "mac depot id '$value' did not exit 2 (got $STATUS)"
    [ ! -f "$SANDBOX/calls.log" ] || fail "mac depot id '$value' reached git/build"
    case "$OUTPUT" in *MOORESTECH_STEAM_DEPOT_ID_MAC*) ;; *) fail "mac depot id '$value' was not named";; esac
done

# 非公開アセットの Mac ffmpeg が無い・LFS の殻ならビルド前に exit 3
# A missing or LFS-husk Mac ffmpeg in the private assets exits 3 before building
for state in missing lfs; do
    make_sandbox
    OUTPUT=$(FFMPEG_MAC_STATE="$state" run_target); STATUS=$?
    [ "$STATUS" -eq 3 ] || fail "mac ffmpeg '$state' did not exit 3 (got $STATUS)"
    grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite mac ffmpeg '$state'"
    case "$OUTPUT" in *macos-arm64*) ;; *) fail "mac ffmpeg '$state' failure did not name macos-arm64";; esac
done

finish_contract "release-playtest mac contract"
```

- [ ] **Step 5: 赤を確認する**

Run: `bash scripts/playtest/tests/test-release-playtest-mac.sh; bash scripts/playtest/tests/test-release-playtest.sh`
Expected: 両方 `FAILED: N contract checks`（実装前なので Mac 系が落ちる）

- [ ] **Step 6: コミット**

```bash
chmod +x scripts/playtest/tests/test-release-playtest-mac.sh
git add scripts/playtest/tests
git commit -m "test(playtest): 配布パイプラインのMac版契約テストを追加する（赤） (ADR 0071)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: release-playtest.sh を両 OS・2 depot 対応にする（緑）

**Files:**
- Modify: `scripts/playtest/release-playtest.sh`
- Modify: `scripts/playtest/lib/release-preflight.sh`
- Create: `scripts/playtest/lib/release-artifact.sh`
- Create: `scripts/playtest/lib/release-steam-vdf.sh`
- Modify: `scripts/playtest/steam/app_build_playtest.vdf`, `scripts/playtest/steam/depot_build_windows.vdf`
- Create: `scripts/playtest/steam/depot_build_mac.vdf`

**Interfaces:**
- Consumes: Task 2 の `SteamPlaytestBuildCli.WindowsSteamPlaytestBuild` / `MacOsSteamPlaytestBuild`、Task 5 のスタブ契約（`CODESIGN_BIN`・`LIPO_BIN`）。
- Produces: `release_require_windows_artifact <build_dir>`、`release_require_mac_artifact <build_dir>`、`release_render_steam_vdfs <run_dir> <steam_dir>`。

- [ ] **Step 1: VDF を2 depot 化する**

`steam/app_build_playtest.vdf`:
```
"appbuild"
{
	"appid" "1958160"
	"desc" "moorestech playtest staging __BUILD_LABEL__"
	"buildoutput" "__RUN_DIR__/steam/output"
	"contentroot" "__RUN_DIR__"
	"setlive" "playtest-staging"
	"preview" "0"

	"depots"
	{
		"__DEPOT_ID_WINDOWS__" "__RUN_DIR__/steam/depot_build_windows.vdf"
		"__DEPOT_ID_MAC__" "__RUN_DIR__/steam/depot_build_mac.vdf"
	}
}
```
`steam/depot_build_windows.vdf` の `__DEPOT_ID__` を `__DEPOT_ID_WINDOWS__`、`__CONTENT_ROOT__` を `__CONTENT_ROOT_WINDOWS__` に置換する。
`steam/depot_build_mac.vdf`:
```
"DepotBuild"
{
	"DepotID" "__DEPOT_ID_MAC__"
	"contentroot" "__CONTENT_ROOT_MAC__"

	"FileMapping"
	{
		"LocalPath" "*"
		"DepotPath" "."
		"recursive" "1"
	}

	"FileExclusion" "**/*_BurstDebugInformation_DoNotShip/*"
	"FileExclusion" "**/*_BackUpThisFolder_ButDontShipItWithYourGame/*"
	"FileExclusion" "**/*.dSYM/*"
}
```

- [ ] **Step 2: `lib/release-steam-vdf.sh` を作る**

```bash
#!/usr/bin/env bash
# release-playtest.sh の Steam VDF 生成。テンプレートのトークンを実値へ置き換えて run の steam/ へ書く
# depot id はアカウント固有なので repo へ書かない（入口で数字のみを検証済み）
# Renders the Steam VDFs for release-playtest.sh by replacing template tokens and writing them under the run's steam/
# Depot ids are account-specific and never committed (validated as digits at entry)

release_render_steam_vdfs() {
    local run_dir="$1" steam_dir="$2" template
    # sedの区切り文字はパスに現れないASCII制御文字を使い、パスに'|'を含む環境でも壊れないようにする
    # The sed delimiter is a control char that paths never contain, so a '|' in a path cannot break it
    local d=$'\x01'
    for template in app_build_playtest.vdf depot_build_windows.vdf depot_build_mac.vdf; do
        sed -e "s${d}__BUILD_LABEL__${d}${BUILD_LABEL}${d}g" \
            -e "s${d}__RUN_DIR__${d}${run_dir}${d}g" \
            -e "s${d}__CONTENT_ROOT_WINDOWS__${d}${run_dir}/build-windows${d}g" \
            -e "s${d}__CONTENT_ROOT_MAC__${d}${run_dir}/build-mac${d}g" \
            -e "s${d}__DEPOT_ID_WINDOWS__${d}${MOORESTECH_STEAM_DEPOT_ID_WINDOWS}${d}g" \
            -e "s${d}__DEPOT_ID_MAC__${d}${MOORESTECH_STEAM_DEPOT_ID_MAC}${d}g" \
            "$SCRIPT_DIR/steam/$template" >"$steam_dir/$template"
    done
}
```

- [ ] **Step 3: `lib/release-artifact.sh` を作る**

```bash
#!/usr/bin/env bash
# release-playtest.sh の成果物検査。欠けた・取り違えた・署名やCPUの合わない成果物を Steam へ上げない
# 失敗は理由を stderr へ出して exit 4 する（source 元の set -e 下で呼ぶ前提）
# Artifact checks for release-playtest.sh, so an incomplete, mixed-up, badly signed or wrong-CPU artifact never reaches Steam
# Failures print the reason to stderr and exit 4 (meant to be called under the sourcing script's set -e)

release_require_paths() {
    local path
    for path in "$@"; do
        if [ ! -e "$path" ]; then
            echo "ERROR: 成果物に $path がありません" >&2
            exit 4
        fi
    done
}

# steamBuildLabel・commit・branch・target の値を検査する。grep の一致だけでは stale worktree や取り違えに気づけない
# Check the steamBuildLabel, commit, branch and target values; a plain grep cannot catch a stale worktree or a mix-up
release_require_build_info() {
    local build_info="$1" expected_target="$2"
    if ! BUILD_LABEL="$BUILD_LABEL" COMMIT="$COMMIT_FULL" BRANCH="$BUILD_BRANCH" TARGET="$expected_target" python3 -c '
import json, os, sys
info = json.load(open(sys.argv[1]))
for key, env in (("steamBuildLabel", "BUILD_LABEL"), ("commit", "COMMIT"), ("branch", "BRANCH"), ("target", "TARGET")):
    if info.get(key) != os.environ[env]:
        print(f"{key} mismatch: got {info.get(key)!r} want {os.environ[env]!r}", file=sys.stderr)
        sys.exit(1)
' "$build_info"; then
        echo "ERROR: build-info.json の内容が指定コミット/ラベル/ターゲットと一致しません（共有契約 Global Constraints §1 のキー名。stale worktreeや取り違えた成果物の可能性）: ${build_info}" >&2
        exit 4
    fi
}

release_require_windows_artifact() {
    local build_dir="$1"
    release_require_paths "$build_dir/moorestech.exe" "$build_dir/game/mods" "$build_dir/moorestech_Data/StreamingAssets/build-info.json"
    release_require_build_info "$build_dir/moorestech_Data/StreamingAssets/build-info.json" StandaloneWindows64
}

release_require_mac_artifact() {
    local build_dir="$1" app="$1/moorestech.app" archs
    release_require_paths "$app/Contents/MacOS/moorestech" "$app/Contents/MacOS/ffmpeg" "$app/Contents/Resources/ffmpeg-LICENSE.txt" \
        "$build_dir/game/mods" "$app/Contents/Resources/Data/StreamingAssets/build-info.json"
    # 展示会用の再起動ループがSteam配布に混ざっていたら用途の取り違え（ADR 0071）
    # The exhibition restart loop inside the Steam artifact means the purpose was mixed up (ADR 0071)
    if [ -e "$build_dir/start-gamescom-loop.command" ]; then
        echo "ERROR: Steam配布のMac成果物に展示会用スクリプトが入っています: ${build_dir}/start-gamescom-loop.command" >&2
        exit 4
    fi
    if ! "$CODESIGN_BIN" --verify --deep --strict "$app"; then
        echo "ERROR: Mac成果物の署名検証に失敗しました: ${app}" >&2
        exit 4
    fi
    # CEFのMacランタイムがarm64のみのため、主実行ファイルはarm64だけでなければならない
    # CEF's Mac runtime is arm64 only, so the main executable must be arm64 and nothing else
    archs="$("$LIPO_BIN" -archs "$app/Contents/MacOS/moorestech")"
    if [ "$archs" != "arm64" ]; then
        echo "ERROR: Mac成果物のアーキテクチャが arm64 のみではありません（${archs}）: ${app}" >&2
        exit 4
    fi
    release_require_build_info "$app/Contents/Resources/Data/StreamingAssets/build-info.json" StandaloneOSX
}
```

- [ ] **Step 4: `lib/release-preflight.sh` を直す**

`release_require_env` のループを次にし、depot id 検査を2つにする:
```bash
    for name in MOORESTECH_STEAM_USER MOORESTECH_STEAM_DEPOT_ID_WINDOWS MOORESTECH_STEAM_DEPOT_ID_MAC MOORESTECH_VERIFY_HOST \
        MOORESTECH_VERIFY_USER MOORESTECH_VERIFY_MAC PLAYTEST_ADMIN_KEY; do
```
```bash
    # depot id は VDF へそのまま埋め込むため数字だけを許す
    # Depot ids are embedded verbatim into the VDF, so only digits are allowed
    for name in MOORESTECH_STEAM_DEPOT_ID_WINDOWS MOORESTECH_STEAM_DEPOT_ID_MAC; do
        if ! printf '%s' "${!name}" | grep -Eq '^[0-9]+$' || [ "$(printf '%s' "${!name}" | wc -l)" -ne 0 ]; then
            echo "ERROR: ${name} は数字のみを許可します" >&2
            exit 2
        fi
    done
```
`release_require_private_assets` の ffmpeg 検査部（`ffmpeg_dir=...` 以降）を関数呼び出し2回へ置き換え、関数を足す:
```bash
    release_require_ffmpeg_source "$private_root/ffmpeg/win-x64" ffmpeg.exe
    release_require_ffmpeg_source "$private_root/ffmpeg/macos-arm64" ffmpeg
}

# ffmpeg の実体（LFS の殻でない）と LICENSE が揃うことを確かめる
# LFS 未解決の殻は CefLfsPointer と同じく「1024バイト以下で先頭が version https://git-lfs」で判定する
# Confirm the real ffmpeg (not an LFS husk) and its LICENSE are present
# An unresolved LFS husk is detected like CefLfsPointer: at most 1024 bytes and starting with "version https://git-lfs"
release_require_ffmpeg_source() {
    local ffmpeg_dir="$1" executable="$2"
    if [ ! -f "$ffmpeg_dir/$executable" ] || { [ "$(wc -c <"$ffmpeg_dir/$executable")" -le 1024 ] &&
        [ "$(head -c 23 "$ffmpeg_dir/$executable")" = "version https://git-lfs" ]; }; then
        echo "ERROR: ffmpeg の実体が無いか LFS ポインタのままです: ${ffmpeg_dir}/${executable}（git lfs pull を確認してください）" >&2
        exit 3
    fi
    if [ ! -f "$ffmpeg_dir/LICENSE" ]; then
        echo "ERROR: ffmpeg の LICENSE がありません: ${ffmpeg_dir}/LICENSE" >&2
        exit 3
    fi
}
```
関数冒頭のコメント「非公開アセット（ffmpeg の正本）…」は「Windows/Mac の ffmpeg 実体と LICENSE」に書き換え、`local` 宣言から `ffmpeg_dir` を外す。

- [ ] **Step 5: `release-playtest.sh` を直す**

1. 冒頭コメントの「Windows配布ビルド」を「Windows と Mac（Apple Silicon）の配布ビルド」にし、ツール変数に追加:
```bash
CODESIGN_BIN="${CODESIGN_BIN:-/usr/bin/codesign}"
LIPO_BIN="${LIPO_BIN:-/usr/bin/lipo}"
```
2. `. "$SCRIPT_DIR/lib/release-preflight.sh"` の直後に `. "$SCRIPT_DIR/lib/release-artifact.sh"` と `. "$SCRIPT_DIR/lib/release-steam-vdf.sh"` を追加する（shellcheck source コメント付き）。
3. `BUILD_DIR="$RUN_DIR/build"` を削除し、`mkdir -p "$BUILD_DIR" "$STEAM_DIR/output"` を `mkdir -p "$RUN_DIR/build-windows" "$RUN_DIR/build-mac" "$STEAM_DIR/output"` にする。
4. Unity 呼び出し1回（`MOORESTECH_BUILD_OUTPUT=... -logFile "$RUN_DIR/unity-build.log"`）から build-info 検査の python ブロックの終わりまでを、次に置き換える:
```bash
# 同じworktreeからWindows→Macの順に焼く。どちらかが落ちたら何も上げない（ADR 0071）
# Bake Windows then Mac from the same worktree; if either fails nothing is uploaded (ADR 0071)
release_build_player() {
    local method="$1" output="$2" log="$3"
    MOORESTECH_BUILD_OUTPUT="$output" MOORESTECH_STEAM_BUILD_LABEL="$BUILD_LABEL" MOORESTECH_BUILD_BRANCH="$BUILD_BRANCH" \
        MOORESTECH_MASTER_DATA_ROOT="$MOORESTECH_MASTER_DATA_ROOT" \
        "$UNITY_BIN" -batchmode -nographics \
        -projectPath "$WORKTREE/moorestech_client" \
        -executeMethod "Client.Editor.Build.SteamPlaytestBuildCli.$method" \
        -logFile "$log"
}
release_build_player WindowsSteamPlaytestBuild "$RUN_DIR/build-windows" "$RUN_DIR/unity-build-windows.log"
release_build_player MacOsSteamPlaytestBuild "$RUN_DIR/build-mac" "$RUN_DIR/unity-build-mac.log"

# 両OSの成果物の必須構成・出所・Macの署名とCPUを検査する（欠けたままSteamへ上げない）
# Verify both artifacts' layout and origin plus the Mac signature and CPU, so nothing incomplete reaches Steam
release_require_windows_artifact "$RUN_DIR/build-windows"
release_require_mac_artifact "$RUN_DIR/build-mac"
```
5. VDF の sed ループ（`SED_DELIM=...` から `done` まで、直前のコメント含む）を `release_render_steam_vdfs "$RUN_DIR" "$STEAM_DIR"` に置き換える。
6. `promotion.md` の heredoc を次にする:
```bash
cat >"$RUN_DIR/promotion.md" <<EOF
# moorestech プレイテスト反映手順 ($BUILD_LABEL)

- コミット: $COMMIT_FULL
- Steam アップロード先: playtest-staging（Windows depot と Mac depot を同じビルドに格納）
- 通し検証: Windows は検証機で phase1 / phase2 とも合格。Mac 版は自動検証していない（ADR 0071）

## Mac 版の手動確認（playtest 反映前に必須）

1. Apple Silicon の Mac で Steam のベータ \`playtest-staging\` を選び、更新後に Steam から起動する
2. タイトルから新規ワールドへ入り、Tab でインベントリが開くことを確認する
3. 起動に回避操作（セキュリティ設定の変更・コマンド実行）が要ったか: [ ] 要らなかった / [ ] 要った（手順: ）
   要った場合も配布は止めず、その手順をキーと一緒にテスターへ案内する

## 反映

Steamworks → アプリ 1958160 → SteamPipe → ビルドで、検証済みビルドを \`playtest\` ブランチに手動でライブ設定してください。設定後に対象のビルド ID を確認し、テスターへ告知してください。
EOF
```
7. `wc -l scripts/playtest/release-playtest.sh scripts/playtest/lib/*.sh` がすべて200行未満であることを確認する。

- [ ] **Step 6: 契約テストが緑になることを確認する**

Run:
```bash
bash scripts/playtest/tests/test-release-playtest.sh
bash scripts/playtest/tests/test-release-playtest-mac.sh
bash scripts/playtest/tests/test-release-playtest-origin.sh
bash scripts/playtest/tests/test-verify-on-windows.sh
```
Expected: 4本とも `PASS: ... contract`

- [ ] **Step 7: 旧名の残りを確認する**

Run: `git grep -n -e 'MOORESTECH_STEAM_DEPOT_ID\b' -e 'MOORESTECH_STEAM_DEPOT_ID"' -e '__DEPOT_ID__' -e '__CONTENT_ROOT__' -e ReleaseLocalBuild -- scripts moorestech_client .github`
Expected: 0件

- [ ] **Step 8: コミット**

```bash
git add scripts/playtest
git commit -m "feat(playtest): 配布パイプラインでWindowsとMacを焼き1つのSteamビルドに両depotを入れる (ADR 0071)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: README に Steamworks 手作業・env・Mac 手動確認を書く

**Files:**
- Modify: `scripts/playtest/README.md`

- [ ] **Step 1: README を更新する**

- 「配布工程」冒頭の説明を「Windows と Mac（Apple Silicon）の配布ビルドを焼き、1つの Steam ビルドに両 depot を入れて `playtest-staging` へ上げる。Windows は検証機で通し検証し、Mac は `promotion.md` の手順で人が確認する」に変える。
- 「Steamworks 側」の手順3を次に置き換える:
  3. Windows の Depot ID を控え `MOORESTECH_STEAM_DEPOT_ID_WINDOWS` に設定する（Steamworks → SteamPipe → Depots）。
  3b. macOS 用 depot を作成する（Depots → 新規 depot、OS 指定 macOS）。ID を `MOORESTECH_STEAM_DEPOT_ID_MAC` に設定する。
  3c. インストール設定 → 一般 → 起動オプションに macOS 用を追加する（実行ファイル `moorestech.app`、OS: macOS）。
  3d. テスター用キーのパッケージ（Packages）に macOS depot を追加する。入れないと Mac のテスターへ配信されない。
  3e. ストア/depot のシステム要件に「Apple Silicon（M1 以降）専用。Intel Mac 非対応」と明記する（CEF の Mac ランタイムが arm64 のみのため）。
- 「Mac mini 側」の env 一覧の `MOORESTECH_STEAM_DEPOT_ID` を `MOORESTECH_STEAM_DEPOT_ID_WINDOWS` と `MOORESTECH_STEAM_DEPOT_ID_MAC` の2行にする。
- 「使い方」の必須 env 列挙も同様に改め、次を追記する:
  - Windows→Mac の順に同じ worktree から焼き、成果物は `runs/<label>/build-windows/`・`build-mac/`、ログは `unity-build-windows.log`・`unity-build-mac.log`。どちらかのビルド・成果物検査が失敗したら Steam には何も上げない。
  - Mac 成果物は `moorestech.app` の ad-hoc 署名検証（`codesign --verify --deep --strict`）と主実行ファイルが arm64 のみであること、展示会用スクリプトが入っていないことも検査する。
  - Mac は自動検証しない。`promotion.md` の「Mac 版の手動確認」を済ませてから `playtest` へ反映する。回避操作が要った場合も止めず、その手順をキーと一緒に案内する（ADR 0071）。
- 非公開アセットの検査の文を「`ffmpeg/win-x64/ffmpeg.exe` と `ffmpeg/macos-arm64/ffmpeg`（LFS ポインタでない実体）とそれぞれの `LICENSE`」に改める。
- 「テスト」節に `bash scripts/playtest/tests/test-release-playtest-mac.sh   # PASS: release-playtest mac contract と出れば合格` を追加する。

- [ ] **Step 2: コミット**

```bash
git add scripts/playtest/README.md
git commit -m "docs(playtest): Mac depotのSteamworks手作業とMac版の手動確認を書く (ADR 0071)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: 実機 e2e — 本物の Unity で両 OS を焼き、Mac 成果物を検証する

steamcmd と検証機はスタブにし、それ以外（worktree 作成・ピン照合・Unity batchmode 2回・成果物検査・VDF 生成）は本物で通す。Steamworks の mac depot はまだ無いので、Steam への実アップロードはこの plan の範囲外（Task 9 で起票する）。

**Files:** なし（検証のみ。結果は bd note に残す）

- [ ] **Step 1: ブランチを push する（スクリプトは要求コミットが `origin/<配布元 ref>` に含まれることを要求するため）**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/steam-playtest-mac-build
git push -u origin feature/steam-playtest-mac-build
```

- [ ] **Step 2: スタブ付きで本物のパイプラインを走らせる**

作業中 worktree の Unity Editor は閉じなくてよい（スクリプトは別の使い捨て worktree で焼く）。ただし同時 Editor 数を抑えるため、Task 1〜4 で起動した Editor は `uloop launch -q` で先に閉じる。

```bash
S=<scratchpad>/e2e && mkdir -p "$S"
printf '#!/bin/bash\necho "steamcmd $*" >> %s/steamcmd.log\n' "$S" > "$S/steamcmd" && chmod +x "$S/steamcmd"
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/steam-playtest-mac-build
MOORESTECH_STEAM_USER=e2e-dummy MOORESTECH_STEAM_DEPOT_ID_WINDOWS=1 MOORESTECH_STEAM_DEPOT_ID_MAC=2 \
MOORESTECH_VERIFY_HOST=e2e MOORESTECH_VERIFY_USER=e2e MOORESTECH_VERIFY_MAC=00:00:00:00:00:00 PLAYTEST_ADMIN_KEY=e2e \
MOORESTECH_BUILD_BRANCH=feature/steam-playtest-mac-build MOORESTECH_STEAM_BUILD_LABEL=e2e-mac-$(date +%Y%m%d-%H%M) \
STEAMCMD_BIN="$S/steamcmd" VERIFY_SCRIPT=/usr/bin/true PLAYTEST_RUN_ROOT="$S/runs" \
scripts/playtest/release-playtest.sh origin/feature/steam-playtest-mac-build 2>&1 | tee "$S/release.log"
```
Expected: 終了コード0、`$S/steamcmd.log` に `+run_app_build` が1行。所要は Unity ビルド2本分（数十分）なので `timeout` を十分に取るか `run_in_background` で回し、完了通知まで待つ（ポーリングしない）。

- [ ] **Step 3: Mac 成果物を実物で確かめる**

```bash
R=$(ls -d "$S"/runs/e2e-mac-*); APP="$R/build-mac/moorestech.app"
lipo -archs "$APP/Contents/MacOS/moorestech"                 # Expected: arm64
codesign --verify --deep --strict --verbose=2 "$APP"; echo $?  # Expected: 0
"$APP/Contents/MacOS/ffmpeg" -version | head -1; echo $?       # Expected: ffmpeg version ... と 0
ls "$APP/Contents/Resources/ffmpeg-LICENSE.txt" "$R/build-mac/game/mods" >/dev/null && echo ok
ls "$R/build-mac/start-gamescom-loop.command" 2>/dev/null && echo "NG: event script leaked"
find "$APP" -name "cef-unity-server.app" -maxdepth 6            # Expected: 1件（CEF helper 同梱）
grep -c '"DepotID"' "$R/steam/depot_build_mac.vdf"            # Expected: 1
```

- [ ] **Step 4: Unity ログの警告・拒否語がゼロであることを確かめる**

```bash
grep -n -i -e "BuildFailedException" -e "\[FfmpegRuntimeBundler\].*missing" -e "\[MacAppAdHocSigner\]" -e "\[MacPlayerArchitecture\]" -e "codesign failed" -e "LFS pointer" -e "skipped for" -e "not found in build output" "$R"/unity-build-*.log
```
Expected: `[MacPlayerArchitecture] macOS player architecture pinned to ARM64` と `[MacAppAdHocSigner] ad-hoc signed and verified` の肯定行だけで、`failed`・`missing`・`skipped`・`not found` を含む行が0件。0件でなければ原因を直し、Step 2 からやり直す。

- [ ] **Step 5: Mac 版を直接起動して、録画用 ffmpeg とCEF が実行時に見つかることを確かめる（Steam 外なので Steam 初期化の失敗行は想定内）**

```bash
"$APP/Contents/MacOS/moorestech" & PID=$!; sleep 90; kill $PID
L=~/Library/Logs/sakastudio/moorestech/Player.log
grep -n -e "ffmpeg が見つかりません" -e "録画リングを開始しません" -e "cef-unity-server" -e "Exception" "$L" | head -40
```
Expected: `ffmpeg が見つかりません`・`録画リングを開始しません` が0件。`Exception` の行は1件ずつ読み、Steam 初期化由来（Steam 外起動）以外があれば原因を調べて bd に起票する。

- [ ] **Step 6: 結果を記録する**

`bd note moorestech-jql13 "e2e(Task8): ..."` に、ラベル・lipo/codesign/ffmpeg の結果・ログの警告語の件数・Player.log で見た Exception の内訳を書く。記録は Write ツールで一時ファイルに書いてから `bd note moorestech-jql13 --file <path>` で渡す（heredoc に bd の語を含めると hook が拒否するため）。

---

### Task 9: 全ブランチレビューと後始末（省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

- [ ] **Step 2: レビュー指摘の反映がソース（判定経路・条件式・その評価時点）に触れたら、Task 8 の Step 2〜5 を反映後のコミットで再実施する**（テスト通過・ログ無音は代替にならない）。あわせて Task 6 Step 6 の契約テスト4本を再実行する。

- [ ] **Step 3: 残課題を1件ずつ起票する（起票されるまで残課題と呼ばない）**

最低限、次を `bd create --deps=discovered-from:moorestech-jql13` で起票し、記録の結論には issue ID を列挙する:
1. Steamworks 側の手作業（macOS depot 作成・macOS 起動オプション・パッケージへの depot 追加・Apple Silicon 要件の明記）と、Mac mini の `~/hermes-agent/data/services/playtest/env.sh` への `MOORESTECH_STEAM_DEPOT_ID_MAC` 追加（人が行う）
2. PR マージ後に env.sh の `MOORESTECH_STEAM_DEPOT_ID` を `MOORESTECH_STEAM_DEPOT_ID_WINDOWS` へ改名する（マージ前に改名すると master のスクリプトが止まるため。改名は値を出力しない `sed -i '' 's/MOORESTECH_STEAM_DEPOT_ID=/MOORESTECH_STEAM_DEPOT_ID_WINDOWS=/; s/export MOORESTECH_STEAM_DEPOT_ID$/export MOORESTECH_STEAM_DEPOT_ID_WINDOWS/'` で行い、実行前に `grep -c MOORESTECH_STEAM_DEPOT_ID env.sh` で対象行数だけ確認する）
3. 初回の実配布で Mac 版の手動確認を行い、回避操作の要否を記録する
4. Task 8 で見つかった未解決事項（あれば全件）

- [ ] **Step 4: e2e 区間の合否を警告語で判定したことを確認する**

Task 8 Step 4・5 の grep が、肯定語でなく `failed`/`missing`/`skipped`/`not found`/`見つかりません`/`開始しません`/`Exception` で引いたものであること、下流（成果物の実物・実行時の ffmpeg 探索）まで一巡したことを bd note に明記する。

- [ ] **Step 5: PR を作る**

本 repo と非公開 repo（Task 1）の両方に PR があり、本 repo のピンが非公開 repo の PR が指す push 済みコミットと一致することを確認してから、pr-create スキルで本 repo の PR を作る。PR 本文に「マージ前に Steamworks の mac depot と env が要る（Task 9 Step 3 の起票 ID）」と書く。PR 作成後に `moores-wt rm steam-playtest-mac-build` で worktree と Editor を畳む（未 push が無いことを確認してから）。

## 判断記録（ADR）

設計裁定の正本: `docs/adr/0071-playtest-distribution-adds-apple-silicon-mac-depot.md`（ユーザー裁定9件の出所はそこに逐語で記録）と `.decisions/2026-09-25-*`・`2026-09-26-*` の5件。

planning 中に新たに生じた判断（すべて agent判断）:

| 判断 | 出所 |
|---|---|
| arm64 固定は `BuildPipeline` で全 Mac ビルド（CI・開発・展示会含む）に適用する。CEF が arm64 のみなので用途によらず Universal は壊れた成果物になるため | agent判断（ADR 0071 の arm64 専用裁定の適用範囲） |
| ad-hoc 再署名も全 Mac ビルドに適用し、strict 用途だけ失敗でビルドを落とす | agent判断（既存 Bundler の strict/警告の二分の前例） |
| 再署名は Unity 側（`BuildPipeline` の最後）で行い、シェルは `codesign --verify` と `lipo` で検査するだけ | agent判断（展示会ビルドも同じ成果物品質になる・同梱と署名の順序を1か所で明示駆動） |
| Mac の ffmpeg は `Contents/MacOS/ffmpeg`、ライセンス文は `Contents/Resources/ffmpeg-LICENSE.txt` | agent判断（非コードファイルを `Contents/MacOS` に置くと codesign が封印を拒むため） |
| `UnityEditor.OSXStandalone.UserBuildSettings` を `#if UNITY_EDITOR_OSX` で囲む | agent判断（型が Mac ホスト Editor の拡張アセンブリにしか無く、Windows CI のコンパイルを守るため） |
| `ExternalToolRunner` を切り出し chmod/codesign を1か所から実行する | agent判断（既存部品の写しを避ける） |
| env の改名は PR マージ後に行い、この plan では起票だけする | agent判断（マージ前の改名は master のスクリプトを止める） |
| Mac ffmpeg の取得元は osxexperts.net の arm64 静的ビルド（`otool -L` でシステム以外の依存が無いこと・`--enable-gpl` を確認） | agent判断（Homebrew 版は動的リンクで配布に使えない） |
| 実機 e2e では steamcmd と検証機をスタブにし、Steam への実アップロードは Steamworks 側設定の後（起票） | agent判断（mac depot がまだ存在しない） |
| Editor アセンブリにテスト前例が無いため `BuildPurposeRules` は `uloop execute-dynamic-code` の実行確認で検証する | agent判断 |
