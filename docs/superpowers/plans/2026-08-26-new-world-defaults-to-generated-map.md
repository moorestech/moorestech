# 新規ワールドの既定マップモードを generated にする Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 新規ワールドの既定マップモードを `template` から `generated` へ変え、macOS Releaseビルドで自動生成マップに入り、オープニングスキットが再生され最初のチュートリアルが開始することを実機確認する。

**Architecture:** 変更は `StartServerSettings.MapMode` の既定値1行のみ。「未指定＝自動生成」を唯一の既定とし、速さが要る側（EditModeInPlayingTest）は既に `WorldMapMode.Template` を明示しているのでそのまま。残りは検証タスク（ビルド＋実機起動）で構成する。

**Tech Stack:** Unity 6000.3.8f1 / C# / NUnit (Unity Test Runner) / uloop CLI / Unity batchmode `-executeMethod`

## Requirements

- R1: `StartServerSettings.MapMode` の既定値が `WorldMapMode.Generated` であること。受け入れ基準: 引数なしで `CliConvert.Parse<StartServerSettings>` した結果の `MapMode` が `"generated"`
- R2: 既定値をアサートしている既存テストが新しい既定に追従していること。受け入れ基準: `CliConvertTest.Parse_StartServerSettings_DefaultValues` がpassする
- R3: Templateを明示している既存呼び手の挙動が変わらないこと。受け入れ基準: `EditModeInPlayingTestUtil.LoadMainGame`（Template固定）を使うテスト群がこれまでどおりpassする
- R4: 最新masterのソースでmacOS Releaseビルド（strict同梱・ローカルゲームデータ同梱）が成功すること。受け入れ基準: ビルドログに `Build Result :Succeeded`、`[CefRuntimeBundler] bundled mac helper`、`[GameDataBundler] bundled game data` が出る
- R5: 生成した Release Player を出展モードで起動すると、自動生成マップでゲームが開始すること。受け入れ基準: Player.log に `MapObject near-field instantiation skipped` の例外が無く、`初期化処理中にエラーが発生しました` が出ない
- R6: オープニングスキットが再生されること。受け入れ基準: スキット中の画面キャプチャ（発話UI）と、`Vanilla/Skit/skits/100_start_game` が再生済みとして登録される挙動
- R7: 最初のチュートリアルが開始すること。受け入れ基準: 最初のチャレンジ「小石を3個拾う」のHUDとチュートリアル表示（world pin / UIハイライト）がキャプチャで確認できる
- R8: `Client.Localization/_CompileRequester.cs` のマーカーが現在の `Localization/localization.csv` に追従していること。受け入れ基準: クリーンな状態から `LocalizationKeys.Ui.Tooltip.Place*` のCS0117が出ずにコンパイルが通る

**やらないこと（スコープ境界）:**
- `moorestech_master` の `server_v8/map/map.json` の欠落mapObject guid 4種は修正しない（ユーザー裁定「触らない、イベント向けだから仮対応許容」）
- `MapObjectInstantiationRunner` の skip>0 で例外を投げる方針は変えない
- 生成seedの扱いは変えない（未指定時 `DefaultGeneratedSeed = 196` 固定のまま）
- `Template` モード自体の廃止・`GeneratedWorldPlayModeSettings`（Editor専用ボタン）の整理はしない
- `StandaloneTerrainQa` 入口の不具合（bd moorestech-muyo）は直さない

## 機能死活表（既定変更で死ぬ操作が無いことの確認）

| 現在できること | 変更後 | 根拠 |
|---|---|---|
| 既存 `world_1` でのプレイ継続 | 生きる | `WorldProvisioner.EnsureWorld` はワールド未作成時のみモードを見る。既存Templateワールドはそのままロードされる |
| Editor「Generated world play」ボタン | 生きる | `GeneratedWorldPlayModeSettings` は `world_generated` と `Generated` を明示上書きしており既定に依存しない |
| `--standaloneTerrainQa` 起動 | 生きる | `StandaloneTerrainQaSettings` が `MapMode = Generated` を明示 |
| EditModeInPlayingTest の高速起動 | 生きる | `EditModeInPlayingTestUtil.LoadMainGame` が `WorldMapMode.Template` を明示 |
| 引数なし standalone サーバー起動 | **変わる（意図）** | 新規ワールドが自動生成になる。初回起動が地形生成の分だけ遅くなる（PR #1255 で176秒→30秒） |
| `--mapMode template` の明示指定 | 生きる | CLIオプションは残す。ただし対象ワールドが `server_v8/map/map.json` を使う場合は既知のguid欠落で起動不能のまま |

## Global Constraints

- 作業worktree: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/release-20260824`、ブランチ `feature/new-world-defaults-to-generated`（base: `origin/master` = `06a49c1bc`）
- master dataピン: `.moorestech-external-revisions.json` の `moorestech_master` = `274b6d9f`。`moorestech-worktrees/moorestech_master` symlink が `pin-274b6d9f` を指していること。**Unityはこのピンファイルを書き戻すので `git add -A` で巻き込まないこと**
- `.cs` を変更したら必ずコンパイルを実行する（AGENTS.md）
- コミット前に Unity が書き換える `moorestech_client/Assets/Asset/Common/URPSettings/UniversalRP-*.asset` を `git checkout --` で戻す（ビルド副産物であり本変更と無関係）
- `moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs` は**このQAのための一時ファイルでありコミットしない**（デバッグ/QA専用publicをプロダクションに残さない規約）。タスク完了時に削除する
- Releaseビルド成果物の出力先: `/Users/sakastudio/moorestech-builds/release-20260826-macos`（既存があれば上書き。3.3GiB・空き容量10GiB以上を確認してから実行）
- テストは `--filter-type regex` で対象を限定する

---

### Task 1: 既定マップモードを generated にする

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Args/StartServerSettings.cs:18`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/CliConvertTest.cs:889`

**Interfaces:**
- Consumes: `Game.MapGeneration.Transfer.WorldMapMode`（`Template` / `Generated` の文字列定数）
- Produces: `StartServerSettings.MapMode` の既定値が `WorldMapMode.Generated`（文字列 `"generated"`）。`ServerInstanceManager.Start` はこの値を見て `settings.Seed ?? DefaultGeneratedSeed(196)` を解決する

- [ ] **Step 1: 既存テストの期待値を新しい既定へ書き換える（失敗するテストにする）**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Server/CliConvertTest.cs` の
`Parse_StartServerSettings_DefaultValues` を次のように変える:

```csharp
        [Test]
        public void Parse_StartServerSettings_DefaultValues()
        {
            var args = new string[0];
            var result = CliConvert.Parse<StartServerSettings>(args);

            Assert.AreEqual(GameSystemPaths.GetSaveFilePath("world_1"), result.WorldDirectory);

            // 未指定の新規ワールドは自動生成（ADR 0035）。速さが要るテストだけがtemplateを明示する
            // An unspecified new world is generated (ADR 0035); only speed-sensitive tests state template
            Assert.AreEqual("generated", result.MapMode);
            // 未指定は null（0 は有効な seed 値なので既定値には使わない）
            // Unspecified is null (0 is a valid seed, so it is not the default sentinel)
            Assert.IsNull(result.Seed);
            Assert.AreEqual(true, result.AutoSave);
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "CliConvertTest"`
Expected: FAIL — `Parse_StartServerSettings_DefaultValues` が `Expected: "generated" But was: "template"`

- [ ] **Step 3: 既定値を変更する**

`moorestech_server/Assets/Scripts/Server.Boot/Args/StartServerSettings.cs` の該当箇所を次のように変える:

```csharp
        // ワールド新規作成時の生成モード（"template" | "generated"）
        // Provisioning mode for a fresh world ("template" | "generated")
        // 未指定は自動生成。templateはオーサリングマップのコピーで地形を作らないため、明示した呼び手だけが使う
        // Unspecified means generated; template copies the authored map without terrain, so only explicit callers use it
        [Option(isFlag: false, "--mapMode")]
        public string MapMode { get; set; } = WorldMapMode.Generated;
```

- [ ] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "CliConvertTest"`
Expected: PASS（全件）

- [ ] **Step 6: 既定値に依存する周辺テストの回帰を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "GeneratedWorldPlayModeSettingsTest|StandaloneTerrainQaSettingsTest|WorldProvisionerTest|TerrainTransferMetaReaderTest|PlaytestBootLifecycleTest|PlaytestWorldBootSessionTest"`
Expected: PASS（全件）。`GeneratedWorldPlayModeSettingsTest.フラグ無効時は起動引数を変更しない` は `new StartServerSettings()` と突き合わせる形なので新しい既定に自動追従する

- [ ] **Step 7: Templateを明示している経路が変わっていないことを確認する（R3）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "LocalPlayEmbeddedServerBootTest"`
Expected: PASS。`EditModeInPlayingTestUtil.LoadMainGame` は `WorldMapMode.Template` を明示しているので、既定値の変更に影響されない。
PlayMode遷移テストなのでドメインリロードが起きる。実行後に uloop が「Unity is reloading」を返したら45秒待ってリトライする（AGENTS.md）

- [ ] **Step 8: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/release-20260824
git checkout -- moorestech_client/Assets/Asset/Common/URPSettings/
git add moorestech_server/Assets/Scripts/Server.Boot/Args/StartServerSettings.cs \
        moorestech_server/Assets/Scripts/Tests/UnitTest/Server/CliConvertTest.cs \
        docs/adr/0035-new-world-defaults-to-generated-map.md \
        .decisions/2026-08-26-新規ワールドは自動生成マップで開始する.md \
        docs/superpowers/plans/2026-08-26-new-world-defaults-to-generated-map.md
git commit -m "feat(server): 新規ワールドの既定マップモードをgeneratedにする (ADR 0035)"
```

---

### Task 2: localization の再compileマーカーをCSVへ追従させる

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`

**Interfaces:**
- Consumes: `Localization/localization.csv`（`Client.Localization/csc.rsp` の `/additionalfile` 経由でSourceGeneratorへ渡る）
- Produces: `LocalizationKeys.Ui.Tooltip.Place*`（`PlaceWireOutOfRange` 等25キー）を含む生成テーブル。`Client.Game` の `ElectricWirePlacementFailureTooltipKey` 等がこれを参照する

**背景（このタスクが要る理由）:** `localization.csv` はUnityのAssets外にあり AssetDatabase が監視しない。master で CSV に25行追加された際に `_CompileRequester.cs` のマーカーが更新されなかったため、Client.Localization アセンブリが再コンパイルされず、古い生成テーブルのまま `Client.Game` がコンパイルされて CS0117 が26件出る。マーカーの内容を変えることが再生成のトリガーになる。

- [ ] **Step 1: 現状のCS0117を再現して確認する**

Run: `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true` の**前**に、まず通常compileでエラーを観測する:
`uloop compile --project-path ./moorestech_client`
Expected: マーカー未更新の環境では `LocalizationKeys.Ui.Tooltip' does not contain a definition for 'PlaceWireOutOfRange'` 等のCS0117。既にマーカー更新済み（このworktreeは更新済み）なら errors 0 で、その場合は Step 2 の値が下記と一致することだけ確認して Step 3 へ進む

- [ ] **Step 2: マーカーをCSVのMD5へ更新する**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/release-20260824
H=$(md5 -q Localization/localization.csv | tr 'a-z' 'A-Z' | sed 's/../&-/g;s/-$//')
echo "$H"   # 期待値: 3B-1D-0D-0E-06-55-AF-16-A5-21-9D-56-5C-C0-54-92
sed -i '' "s/dummyText = \"[^\"]*\"/dummyText = \"$H\"/" \
  moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
```

結果のファイル内容:

```csharp
// SchemaWatcher更新用の再compile印
// Recompile marker updated by SchemaWatcher
public class LocalizationCompileRequester
{
// CSV更新時はこの印もcommit
// Commit this marker with CSV changes
    private const string dummyText = "3B-1D-0D-0E-06-55-AF-16-A5-21-9D-56-5C-C0-54-92";
}
```

- [ ] **Step 3: コンパイルして CS0117 が消えたことを確認する**

Run: `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true`
その後: `uloop compile --project-path ./moorestech_client`
Expected: errors 0（force版はエラー本文を返さないので、判定は2回目の通常compileで行う）

- [ ] **Step 4: ローカライズのテストが通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Localiz"`
Expected: PASS（全件）

- [ ] **Step 5: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/release-20260824
git checkout -- moorestech_client/Assets/Asset/Common/URPSettings/
git add moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
git commit -m "fix(localization): 再compile印をlocalization.csvの現在値へ追従させる"
```

---

### Task 3: macOS Release ビルドを生成する

**Files:**
- Create（コミットしない一時ファイル）: `moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs`
- Consumes: `moorestech_client/Assets/Scripts/Editor/Build/BuildPipeline.cs` の private static `Execute(PlayerBuildRequest)`

**Interfaces:**
- Consumes: `Client.Editor.Build.PlayerBuildRequest`（`Target` / `OutputDirectory` / `IsDevelopmentBuild` / `IsStrictBundling` / `BundleLocalGameData`）、`Client.Editor.Build.PlayerBuildOutcome`
- Produces: `/Users/sakastudio/moorestech-builds/release-20260826-macos/moorestech.app` と同階層の `game/`（ローカルゲームデータ252ファイル）

**なぜCLI入口が要るか:** `BuildPipeline` のメニュー入口 `MacOsBuild` は `EditorUtility.DisplayDialog` と `OpenFolderPanel` を使う対話式で、無人実行できない。CI入口 `MacOsBuildFromGithubAction` は Development・同梱warn-only・ゲームデータ無しで配布物と別物。よってRelease相当の設定で `Execute` を呼ぶ薄い入口を一時的に置く。

- [ ] **Step 1: 一時ビルド入口を作る**

`moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs`:

```csharp
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// ローカル配布相当（Release・strict同梱・ゲームデータ込み）をbatchmodeから起動するQA専用入口
    /// QA-only entry that runs the local-distribution build (Release, strict bundling, game data) from batchmode
    /// </summary>
    public static class ReleaseLocalBuildCli
    {
        public static void MacOsReleaseLocalBuild()
        {
            // 出力先は環境変数で受け取り、対話ダイアログ無しで確定させる
            // Take the output directory from an env var so nothing needs an interactive dialog
            var outputDirectory = Environment.GetEnvironmentVariable("MOORESTECH_BUILD_OUTPUT");
            if (string.IsNullOrEmpty(outputDirectory))
            {
                Debug.LogError("[ReleaseLocalBuildCli] MOORESTECH_BUILD_OUTPUT is not set");
                EditorApplication.Exit(2);
                return;
            }

            var request = new PlayerBuildRequest
            {
                Target = BuildTarget.StandaloneOSX,
                OutputDirectory = outputDirectory,
                IsDevelopmentBuild = false,
                IsStrictBundling = true,
                BundleLocalGameData = true,
            };

            // メニュー入口と同じオーケストレーションを共有し、QA成果物を配布物と一致させる
            // Share the same orchestration as the menu entry so the QA artifact matches the distributable
            var execute = typeof(BuildPipeline).GetMethod("Execute", BindingFlags.NonPublic | BindingFlags.Static);
            var outcome = (PlayerBuildOutcome)execute.Invoke(null, new object[] { request });
            Debug.Log($"[ReleaseLocalBuildCli] outcome={outcome}");
            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }
    }
}
```

- [ ] **Step 2: 空き容量とEditor占有を確認する**

```bash
df -h / | tail -1                    # Avail が 10Gi 以上あること
pgrep -f "release-20260824/moorestech_client" | head   # Editorが動いていたら終了させる（batchmodeはプロジェクトロックを取れない）
```

- [ ] **Step 3: batchmodeでビルドする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/release-20260824
LOG=/Users/sakastudio/moorestech-builds/release-20260826-macos/build.log
mkdir -p /Users/sakastudio/moorestech-builds/release-20260826-macos
MOORESTECH_BUILD_OUTPUT=/Users/sakastudio/moorestech-builds/release-20260826-macos \
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/moorestech_client" -logFile "$LOG" \
  -executeMethod Client.Editor.Build.ReleaseLocalBuildCli.MacOsReleaseLocalBuild
echo "exit=$?"
```

Expected: exit=0。所要はコンパイル込みで20〜40分程度

- [ ] **Step 4: ビルド成功を検証する**

```bash
L=/Users/sakastudio/moorestech-builds/release-20260826-macos/build.log
grep -c "error CS" "$L"                       # 期待: 0
grep "Build Result :" "$L"                    # 期待: Build Result :Succeeded
grep "bundled mac helper" "$L"                # 期待: [CefRuntimeBundler] bundled mac helper: 252 files
grep "bundled game data" "$L"                 # 期待: [GameDataBundler] bundled game data: 252 files
ls /Users/sakastudio/moorestech-builds/release-20260826-macos/moorestech.app  # 存在すること
```

- [ ] **Step 5: 一時ビルド入口を削除し、副産物を戻す**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/release-20260824
rm -f moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs \
      moorestech_client/Assets/Scripts/Editor/Build/ReleaseLocalBuildCli.cs.meta
git checkout -- moorestech_client/Assets/Asset/Common/URPSettings/
git status -s   # 期待: ピンファイル・一時ファイルの残骸が無い
```

このタスクはコミットを生まない（成果物はリポジトリ外）。

---

### Task 4: Release Player でスキットとチュートリアルを実機確認する

**Files:**
- Create（リポジトリ外・スクラッチ）: 起動＋キャプチャスクリプト
- 参照: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeAutoStart.cs`（`MOORESTECH_EVENT_MODE=1` でワールド削除→英語化→ローカルゲーム自動開始）

**Interfaces:**
- Consumes: Task 3 が生成した `moorestech.app`
- Produces: `player.log` と時系列スクリーンショット群（R5〜R7 の証跡）

**なぜ出展モードを使うか:** macOSの入力注入（osascript / CGEvent）は Accessibility 権限拒否（1002）で使えないため、メインメニューの「Play locally」をクリックできない。`EventModeAutoStart` は入力なしでワールド削除→新規作成→ローカルゲーム開始まで進むので、これを起動経路にする。アイドル終了は `MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS` で延ばす。

- [ ] **Step 1: 起動＋キャプチャスクリプトを用意する**

```bash
cat > /tmp/moorestech-story-qa.sh <<'EOF'
#!/bin/zsh
set -u
APP="$1"; OUT="$2"; DURATION="${3:-480}"
mkdir -p "$OUT/shots" "$OUT/debug-cache"
LOG="$OUT/player.log"; rm -f "$LOG"
MOORESTECH_EVENT_MODE=1 \
MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS=3600 \
MOORESTECH_DEBUG_CACHE_DIR="$OUT/debug-cache" \
"$APP/Contents/MacOS/moorestech" \
  -logFile "$LOG" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 &
PID=$!
echo "player pid=$PID"
i=0; end=$(( $(date +%s) + DURATION ))
while [ $(date +%s) -lt $end ]; do
  kill -0 $PID 2>/dev/null || { echo "player exited early at shot $i"; break; }
  printf -v n "%03d" $i
  screencapture -x -t jpg "$OUT/shots/shot-$n.jpg" 2>/dev/null
  i=$((i+1)); sleep 5
done
echo "shots=$i"
EOF
chmod +x /tmp/moorestech-story-qa.sh
```

`MOORESTECH_DEBUG_CACHE_DIR` を毎回新しい空ディレクトリにするのは、`DebugConst.SkitPlaySettingsKey`（スキット抑止）が過去のQA実行で残っていないことを保証するため。

- [ ] **Step 2: Player ウィンドウが他ウィンドウに隠れない状態にする**

Unity Editor を最小化するか別デスクトップへ退避する。前回の実行では Editor ウィンドウが Player を覆い、スクリーンショットが証跡にならなかった。

- [ ] **Step 3: 実行する**

```bash
/tmp/moorestech-story-qa.sh /Users/sakastudio/moorestech-builds/release-20260826-macos/moorestech.app \
  /tmp/moorestech-story-qa-run 600
```

- [ ] **Step 4: R5（初期化成功）を判定する**

```bash
L=/tmp/moorestech-story-qa-run/player.log
grep -c "初期化処理中にエラーが発生しました" "$L"          # 期待: 0
grep -c "near-field instantiation skipped" "$L"            # 期待: 0
grep -c "MapObject master missing" "$L"                    # 期待: 0（生成マップはマスタ由来のguidしか置かない）
grep "サーバーを終了します" "$L"                            # 期待: 出ない（途中でセッションが畳まれていない）
```

いずれかが期待と違う場合は、そこで止めてログの該当箇所をユーザーへ報告する（勝手にコード修正へ進まない）。

- [ ] **Step 5: R6（スキット再生）を判定する**

`/tmp/moorestech-story-qa-run/shots/` を古い順に確認し、スキットの発話UI（キャラクター＋台詞ボックス）が写っているコマを1枚特定する。ファイル名を記録する。
ログ側の補助証跡: `grep -n "SkitCharacter\|100_start_game" "$L"`

- [ ] **Step 6: R7（チュートリアル開始）を判定する**

同じくスクリーンショットから、最初のチャレンジ「小石を3個拾う」（英語ロケールなので `Pick up 3 pebbles` 相当）のHUDと、チュートリアル表示（world pin「左クリックで拾う」＝ `Left click to pick up` 相当、UIハイライト「左上で現在の目標を確認する」）が出ているコマを特定する。ファイル名を記録する。

期待挙動の出所: `moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json` の先頭チャレンジ
`bd5262ed-fbd4-51e0-a75d-2944f366e10a`（`startedActions` に `playSkit: Vanilla/Skit/skits/100_start_game`、`tutorials` に `mapObjectPin` と `uiHighLight`）

- [ ] **Step 7: 証跡をまとめてbdへ記録する**

```bash
bd note moorestech-vq12 "<R4〜R7の判定結果と、スキット/チュートリアルが写ったスクリーンショットのファイル名>"
```

- [ ] **Step 8: Player を終了する**

```bash
pkill -f "moorestech-builds/release-20260826-macos/moorestech.app"
```

このタスクはコミットを生まない（証跡はリポジトリ外＋bd）。

---

### Task 5: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`feature/new-world-defaults-to-generated` の `origin/master` からの差分全体を対象に moores-code-review スキルを実行する。
機械的修正は適用し、設計判断は末尾でユーザーへ提示する。

- [ ] **Step 2: 指摘対応後にコンパイルとテストを再実行する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "CliConvertTest|GeneratedWorldPlayModeSettingsTest|WorldProvisionerTest"`
Expected: errors 0 / PASS

- [ ] **Step 3: コミットしてpushし、PRを作る**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/release-20260824
git status -s    # ピンファイル・URPSettings・一時ファイルが混ざっていないこと
git push -u origin feature/new-world-defaults-to-generated
```
pr-create スキルでPRを作成する。

---

## 判断記録（ADR）

- 設計ADR: [docs/adr/0035-new-world-defaults-to-generated-map.md](../../adr/0035-new-world-defaults-to-generated-map.md)
- 裁定の蒸留: [.decisions/2026-08-26-新規ワールドは自動生成マップで開始する.md](../../../.decisions/2026-08-26-新規ワールドは自動生成マップで開始する.md)

planning中に新たに生じた判断:

- **一時ビルド入口 `ReleaseLocalBuildCli.cs` をコミットしない。** 出所: agent前提（AGENTS.md「デバッグ/テスト専用publicをプロダクションに残さない」）。メニュー入口が対話式・CI入口がDevelopment固定で、Release相当の無人ビルド手段が既存に無いため一時的に置く。恒久的なローカル配布ビルドCLIが要るなら別タスクで設計する
- **検証の起動経路に出展モード（`MOORESTECH_EVENT_MODE=1`）を使う。** 出所: agent前提（macOSのAccessibility権限拒否1002で入力注入が不可、`EventModeAutoStart` が入力なしで新規ワールド開始まで到達する唯一の経路）
- **`localization.csv` の再compile印更新を本ブランチに含める。** 出所: agent前提（本変更のビルドが通らないため不可分。masterの衛生バグでありスコープ外にすると検証自体が実行できない）
- **`MainGame.unity` に環境ルート3種（`DebugEnvironmentObjectRoot` / `PureNatureEnvironmentObjectRoot` / `OtherEnvironmentObjectRoot`）が存在しないため、既定をGeneratedにしてもオーサリング地形との二重表示は起きない。** 出所: agent前提（実測。scene YAML に3つのscript guidがいずれも0件。bd moorestech-muyo と一致）
