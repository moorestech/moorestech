# CI再設計 A: GitHub Actionsワークフロー Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Unity Build を PR トリガーから外して「ビルド検証」ラベル発火＋日次 04:00 JST スケジュールへ移し、空く検知の穴を PF 別コンパイル検査（ubuntu 上・server のみ）で埋め、日次失敗が専用ラベル付き GitHub Issue として起票されるところまでを完成させる。

**Architecture:** すべて `.github/workflows/*.yml` と `.github/scripts/*.cjs`、および `moorestech_server/Assets/Scripts/Editor/` の Editor スクリプト1本で完結する。ゲームコードには一切触れない。PF 別コンパイル検査は「Unity は `-executeMethod` を呼ぶ前に全アセンブリをコンパイルする」という性質を利用し、対象 buildTarget へ切り替えた状態で軽量メソッドを実行できるかどうかでコンパイル可否を判定する（プレイヤービルドは行わない）。

**Tech Stack:** GitHub Actions / game-ci (`unity-builder@v4`, `unity-test-runner@v4`) / `actions/cache@v4` / `actions/github-script@v7` / Unity 6000.3.8f1 / Node.js (CommonJS スクリプト)

## Requirements

設計対話（grill）で確定した要件。ADR: `docs/adr/0028-ci-build-strategy.md`、裁定: `.decisions/2026-08-21-*` および `.decisions/2026-08-22-無人修復の深夜枠は4時開始9時打ち切りとする.md`

- **R1.** `Unity Build` は `pull_request` の全PRでは発火しない。「ビルド検証」ラベルが付いたときだけ発火する。**受け入れ基準**: `build.yml` の `on.pull_request.types` が `[labeled]` のみで、各ジョブが `github.event.label.name == 'ビルド検証'` を条件に持つ。ラベル無しでPRを更新しても Unity Build の run が生成されない。
- **R2.** `Unity Build` は日次 04:00 JST に自動実行される。**受け入れ基準**: `build.yml` の `on.schedule.cron` が `'0 19 * * *'`（UTC 19:00 = JST 翌04:00）である。
- **R3.** 日次ビルドの4ジョブ（Client Win/Mac・Server Win/Mac）は並列に走る。**受け入れ基準**: `build.yml` に `max-parallel: 1` と `needs: server-build` がどちらも存在しない。
- **R4.** `Unity Build` は Library キャッシュを使わない。**受け入れ基準**: `build.yml` に `actions/cache` のステップが存在しない。
- **R5.** 日次ビルドの対象PFは Client Windows/macOS + Server Windows/macOS の4つ。Linux は追加しない。**受け入れ基準**: `build.yml` の matrix に `StandaloneLinux64` の有効な行が無い（コメントアウトのまま）。
- **R6.** Windows ジョブは Docker デーモンの起動を待ってから game-ci を呼ぶ。**受け入れ基準**: `runner.os == 'Windows'` のとき `docker info` の成功をポーリングするステップが unity-builder より前に存在し、既定で最大5分待つ。
- **R7.** PR ごとに、`moorestech_server` の `StandaloneWindows64` / `StandaloneOSX` 向けスクリプトコンパイル可否が検査される。**受け入れ基準**: `platform-compile.yml` が `pull_request` で発火し、2ターゲット分のジョブが `ubuntu-latest` 上で走る。`ServerDirectory.cs` の `#elif UNITY_STANDALONE_OSX` ブロックに構文エラーを入れると StandaloneOSX ジョブだけが落ちる。
- **R8.** PF 別コンパイル検査はプレイヤービルドを行わない。**受け入れ基準**: `platform-compile.yml` の `buildMethod` が `Server.Editor.PlatformCompileCheck.RunFromGithubAction` を指し、`BuildPipeline.BuildPlayer` を呼ばない。
- **R9.** master 上で Library キャッシュが定期的に焼かれ、PR 側から復元できる。**受け入れ基準**: `cache-warm.yml` が `schedule` で走り、`Library_Test_client`・`Library_compile_server_StandaloneWindows64`・`Library_compile_server_StandaloneOSX` の3系統をローリングキーで保存する。`run_test.yml` と `platform-compile.yml` が同じプレフィックスの `restore-keys` を持つ。
- **R10.** キャッシュ総量が10GB枠に収まる。**受け入れ基準**: 保存対象が上記3系統のみ（実測 3.68 + 1.17 + 1.17 ≈ 6.0GB）で、`build.yml` からは保存されない。
- **R11.** `ci-auto-rerun` が日次（`schedule` 起因の run）でも発火する。**受け入れ基準**: `ci-auto-rerun.yml` の `if` が `github.event.workflow_run.event` について `pull_request` と `schedule` の両方を許可する。
- **R12.** 自動再実行後も赤い日次ビルドは、専用ラベル付き Issue として起票される。**受け入れ基準**: Issue に `日次ビルド失敗` ラベルが付き、本文に「前回グリーンの SHA」「以降にマージされた PR 一覧」「失敗ジョブ名とログ抜粋」が含まれる。同じ失敗が続く間は新規起票せず既存 Issue へコメントする。日次が緑に戻ったら Issue を自動クローズする。
- **R13.** CI 衛生の是正。**受け入れ基準**: リポジトリ全体で `actions/cache@v3` の参照が0件。`run_test.yml` の日時取得ステップに `shell: bash` が付いている。`build.yml` の Linux 除外コメントが容量ではなく CEF ネイティブランタイム不在を理由として記述されている。

**やらないこと（スコープ境界）:**

- **poller の Issue 起点拡張は本 plan の対象外。** `~/hermes-agent/data/services/pr-review/poller.py` は git 管理下ですらない別サブシステムで、別 plan（`2026-08-22-ci-build-strategy-poller.md`）が扱う。本 plan は「Issue が正しいラベルと本文で立つ」ところまでを完成とする。
- **`fetch-depth: 0` の廃止はやらない。** game-ci の `versioning: Semantic` が git 履歴からバージョンを生成しており（ログの `Generated version 0.0.13714`）、浅い checkout にすると採番が変わる。`versioning: none` の可否はユーザー裁定待ち（ADR「保留」節・bd 保留タスク）。
- **Linux dedicated server の復活はやらない**（ADR 裁定）。
- **client プロジェクトの PF 別コンパイル検査はやらない**（ADR 裁定。client 側 PF 分岐はテストアセンブリのみ）。
- **修復 PR の自動マージはやらない。**

## Global Constraints

- **ラベル名は日本語で `ビルド検証` と `日次ビルド失敗` の2つ。** 既存の `独立レビュー&対応完了` と同じく日本語ラベルを使う。ワークフロー内の比較文字列は逐語一致させる。
- **cron は UTC。** 04:00 JST = `'0 19 * * *'`（前日 19:00 UTC）。03:00 JST = `'0 18 * * *'`。GitHub の scheduled workflow は混雑時に数分〜十数分遅延しうるため、時刻の厳密さに依存する設計にしない。
- **Unity バージョンは `6000.3.8f1` 固定。** 既存ワークフローと同じ値を使う。バージョンを上げない。
- **コメントは日本語・英語の2行セット**（`// 日本語` → `// English`）。YAML では `# 日本語` → `# English`。各言語1行に収める。既存 `build.yml` / `run_test.yml` の記述に合わせる。
- **C# は try-catch 禁止**（外部境界の隔離目的を除く）。`Func<>` 禁止。`partial` 禁止。1ファイル200行以下。エディタ専用コードは `#if UNITY_EDITOR` で囲むか Editor asmdef 配下に置く。
- **`.meta` ファイルは手動作成しない。** Unity が生成したものだけコミットする。
- **`.cs` を変更したら必ずコンパイルを実行する。**
- **キャッシュ総量は10GB以下**（GitHub Actions のリポジトリあたり上限）。実測サイズ: client Library 3.68GB / server Library 1.17GB。

---

### Task 1: CI衛生の是正（cache@v4・shell指定・誤コメント）

独立した機械的修正。他タスクの土台になるので最初に入れる。

**Files:**
- Modify: `.github/workflows/run_test.yml`
- Modify: `.github/workflows/build.yml`
- Modify: `.github/workflows/notion_tickets.yml`

**Interfaces:**
- Consumes: なし
- Produces: なし（後段タスクは変更後のファイルを前提に編集する）

- [x] **Step 1: `actions/cache@v3` と `actions/checkout@v3` の参照箇所を洗い出す**

Run:
```bash
grep -rn "actions/cache@v3\|actions/checkout@v3\|setup-python@v4" .github/workflows/
```
Expected: `run_test.yml` と `build.yml` に `actions/cache@v3` が計3箇所、`notion_tickets.yml` に `actions/checkout@v3` が1箇所。

- [x] **Step 2: バージョンを上げる**

`.github/workflows/run_test.yml` と `.github/workflows/build.yml` の `uses: actions/cache@v3` をすべて `uses: actions/cache@v4` へ置換する。
`.github/workflows/notion_tickets.yml` の `uses: actions/checkout@v3` を `uses: actions/checkout@v4` へ置換する。

```bash
sed -i '' 's#actions/cache@v3#actions/cache@v4#g' .github/workflows/run_test.yml .github/workflows/build.yml
sed -i '' 's#actions/checkout@v3#actions/checkout@v4#g' .github/workflows/notion_tickets.yml
```

- [x] **Step 3: Windows で空になる `CURRENT_DATETIME` を直す**

`.github/workflows/run_test.yml` の日時取得ステップに `shell: bash` を追加する。`windows-latest` の `run:` は既定 pwsh のため、`date +'%Y-%m'` と `>> $GITHUB_ENV` が意図どおり動かず、キャッシュキーが `Library_`（日付なし）になっていた。

変更前:
```yaml
      - name: Set current datetime as env variable
        env:
          TZ: 'Asia/Tokyo'
        run: echo "CURRENT_DATETIME=$(date +'%Y-%m')" >> $GITHUB_ENV
```

変更後:
```yaml
      # windows-latestのrunは既定でpwshのため、bashを明示しないとキーが空になる
      # windows-latest defaults run to pwsh, so bash must be explicit or the key ends up empty
      - name: Set current datetime as env variable
        shell: bash
        env:
          TZ: 'Asia/Tokyo'
        run: echo "CURRENT_DATETIME=$(date +'%Y-%m')" >> $GITHUB_ENV
```

`.github/workflows/build.yml` にも同じステップが2箇所（server-build / client-build）あるので、同様に `shell: bash` を追加する。※ Task 3 で build.yml のキャッシュ自体を削除するため、このステップも Task 3 で消える。ここでは一旦揃えておく。

- [x] **Step 4: Linux除外コメントを実態に合わせて訂正する**

`.github/workflows/build.yml` の client-build matrix にある誤ったコメントを直す。真因は容量ではなく CEF ネイティブランタイム不在（`.decisions/2026-08-02-Linuxビルド入口は意図的失敗として残す.md`）。

変更前:
```yaml
          # Linux はランナーの容量が足りないためクライアントビルドは除外
```

変更後:
```yaml
          # LinuxクライアントはCEFネイティブランタイム不在で必ず失敗するため除外（.decisions/2026-08-02）
          # The Linux client is excluded because the CEF native runtime does not exist for it (.decisions/2026-08-02)
```

- [x] **Step 5: 全ワークフローがYAMLとして妥当か確認する**

Run:
```bash
python3 -c "
import sys, glob, yaml
for f in sorted(glob.glob('.github/workflows/*.yml')):
    yaml.safe_load(open(f))
    print('ok', f)
"
```
Expected: 全8ファイルに `ok` が出る。

- [x] **Step 6: 是正が効いているか確認する**

Run:
```bash
grep -rn "actions/cache@v3\|actions/checkout@v3" .github/workflows/ | wc -l
grep -c "ランナーの容量が足りない" .github/workflows/build.yml || true
```
Expected: 1行目は `0`。2行目は `0`（grep がヒット0で終了コード1を返すため `|| true` を付けている）。

- [x] **Step 7: コミットする**

```bash
git add .github/workflows/run_test.yml .github/workflows/build.yml .github/workflows/notion_tickets.yml
git commit -m "chore(ci): actions/cacheをv4へ上げ、Windowsで空になるキャッシュキーとLinux除外の誤コメントを直す"
```

---

### Task 2: Windows Docker デーモン起動待ちでフレークを根治する

直近の Unity Build 失敗4件は4件とも `failed to connect to the docker API at npipe:////./pipe/docker_engine` で、開始2秒で死んでいた。`game-ci/unity-builder` は Windows のみ Docker コンテナで動くため、デーモンの起動を待つ必要がある。

**Files:**
- Modify: `.github/workflows/build.yml`（server-build / client-build の両ジョブ）

**Interfaces:**
- Consumes: なし
- Produces: なし

- [x] **Step 1: 現在の失敗シグネチャを確認する**

Run:
```bash
gh run view 32398363972 --log-failed 2>&1 | grep -a "docker API"
```
Expected: `failed to connect to the docker API at npipe:////./pipe/docker_engine; ...` が1行出る。（このrunが期限切れで取得できない場合は、`gh run list --workflow="Unity Build" --limit 40 --json databaseId,conclusion --jq '.[]|select(.conclusion=="failure")|.databaseId'` で直近の失敗runを探して同じ grep を当てる）

- [x] **Step 2: 待機ステップを追加する**

`.github/workflows/build.yml` の server-build ジョブで、`Enable long paths (Windows)` ステップの直後に以下を挿入する。

```yaml
      # Windowsのunity-builderはDockerコンテナで動くため、デーモンの起動を待ってから呼ぶ
      # unity-builder runs in a Docker container on Windows, so wait for the daemon before invoking it
      - name: Wait for Docker daemon (Windows)
        if: runner.os == 'Windows'
        shell: pwsh
        run: |
          $deadline = 30
          for ($i = 0; $i -lt $deadline; $i++) {
            docker info 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
              Write-Host "docker daemon ready after $($i * 10) seconds"
              exit 0
            }
            Start-Sleep -Seconds 10
          }
          Write-Error "docker daemon did not become ready within $($deadline * 10) seconds"
          exit 1
```

- [x] **Step 3: client-build ジョブにも同じステップを追加する**

client-build ジョブの `Enable long paths (Windows)` の直後にも、Step 2 と同一のステップを挿入する。

- [x] **Step 4: YAML の妥当性と挿入位置を確認する**

Run:
```bash
python3 -c "
import yaml
d = yaml.safe_load(open('.github/workflows/build.yml'))
for job in ('server-build', 'client-build'):
    names = [s.get('name') for s in d['jobs'][job]['steps']]
    assert 'Wait for Docker daemon (Windows)' in names, job
    i = names.index('Wait for Docker daemon (Windows)')
    j = [n for n in names if n and n.startswith(('Server Build -', 'Client Build -'))]
    print(job, 'step index', i, 'before build step:', i < names.index(j[0]))
"
```
Expected: 両ジョブで `before build step: True`。

- [x] **Step 5: コミットする**

```bash
git add .github/workflows/build.yml
git commit -m "fix(ci): WindowsビルドでDockerデーモンの起動を待ってからunity-builderを呼ぶ"
```

---

### Task 3: build.yml をラベル発火＋日次スケジュールへ移し、並列化してキャッシュを外す

**Files:**
- Modify: `.github/workflows/build.yml`

**Interfaces:**
- Consumes: なし
- Produces: ワークフロー名 `Unity Build`（Task 6 の `ci-auto-rerun.yml` と Task 7 の Issue 起票が `workflow_run` でこの名前を参照する。**変更しないこと**）

- [x] **Step 1: トリガーを差し替える**

`.github/workflows/build.yml` 冒頭の `on:` ブロックを次に置き換える。

```yaml
on:
  # 「ビルド検証」ラベルを付けたPRだけビルドする（全PRでは回さない。ADR 0028）
  # Only build PRs that carry the "ビルド検証" label; do not run on every PR (ADR 0028)
  pull_request:
    types: [labeled]

  # 日次フルビルド。UTC 19:00 = JST 翌04:00
  # Daily full build. 19:00 UTC equals 04:00 JST the next day
  schedule:
    - cron: '0 19 * * *'

  # 手動実行デバッグ用
  workflow_dispatch: {}
```

- [x] **Step 2: concurrency をラベル発火に合わせる**

`concurrency` ブロックを次に置き換える。日次（`schedule`）は互いにキャンセルし合わない。

```yaml
# PR更新時に同一PRの古い実行をキャンセルする（groupはworkflow×ref単位）。
# 日次(schedule)と手動(workflow_dispatch)は実行中にキャンセルされない。
# Cancel outdated runs of the same PR on update (the group is per workflow and ref).
# Scheduled and manual runs are never cancelled mid-run.
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}
```

- [x] **Step 3: 両ジョブにラベル条件を付ける**

`server-build` と `client-build` の両方に、`runs-on` の直前へ次を追加する。`pull_request` 以外（schedule / workflow_dispatch）では常に実行する。

```yaml
    # ラベル発火のときは「ビルド検証」ラベルのみ通す。日次・手動は無条件で通す
    # On label events only "ビルド検証" passes; scheduled and manual runs always proceed
    if: github.event_name != 'pull_request' || github.event.label.name == 'ビルド検証'
```

- [x] **Step 4: 直列化を外す**

`server-build` の `strategy` から `max-parallel: 1` の行を削除する。
`client-build` の `strategy` から `max-parallel: 1` の行を削除し、さらに `needs: server-build` の行も削除する。あわせて client-build 冒頭の説明コメントを実態に合わせる。

client-build のヘッダコメントを次に置き換える:
```yaml
  # ===== クライアントビルド =====
  # サーバービルドのartifactは消費しないため依存は無い。4ジョブを並列に走らせる（ADR 0028）
  # The client does not consume the server's artifacts, so there is no dependency; all four jobs run in parallel (ADR 0028)
```

- [x] **Step 5: キャッシュを外す**

`build.yml` から次の3種のステップをすべて削除する（server-build / client-build の両方）:
- `- name: Set current datetime as env variable`（`CURRENT_DATETIME` を作るステップ）
- `- uses: actions/cache@v4`（`moorestech_server/Library` / `moorestech_client/Library` を対象にしたもの）
- それらに付随する「年月日を取得」「キャッシュ keyを月跨ぎで…」の日本語コメント

削除の代わりに、client-build / server-build それぞれのビルドステップ直前へ次のコメントを残す:
```yaml
      # 日次ビルドはキャッシュ枠(10GB)をPR側へ譲るためコールドで回す（ADR 0028）
      # The daily build runs cold so the 10GB cache budget goes to PR-side jobs (ADR 0028)
```

- [x] **Step 6: 変更が要件どおりか機械的に検証する**

Run:
```bash
python3 -c "
import yaml
d = yaml.safe_load(open('.github/workflows/build.yml'))
on = d[True] if True in d else d['on']
assert list(on['pull_request']['types']) == ['labeled'], on['pull_request']
assert on['schedule'] == [{'cron': '0 19 * * *'}], on['schedule']
src = open('.github/workflows/build.yml').read()
assert 'max-parallel' not in src, 'max-parallel still present'
assert 'needs: server-build' not in src, 'needs still present'
assert 'actions/cache' not in src, 'cache still present'
for job in ('server-build', 'client-build'):
    assert 'ビルド検証' in d['jobs'][job]['if'], job
print('all checks passed')
"
```
Expected: `all checks passed`

- [x] **Step 7: Linux行が有効化されていないことを確認する**

Run:
```bash
python3 -c "
import yaml
d = yaml.safe_load(open('.github/workflows/build.yml'))
for job in ('server-build', 'client-build'):
    names = [p['name'] for p in d['jobs'][job]['strategy']['matrix']['platform']]
    assert 'StandaloneLinux64' not in names, (job, names)
    print(job, names)
"
```
Expected: 両ジョブとも `['StandaloneWindows64', 'StandaloneOSX']`

- [x] **Step 8: コミットする**

```bash
git add .github/workflows/build.yml
git commit -m "feat(ci): Unity BuildをPRトリガーからラベル発火+日次04:00へ移し、4ジョブ並列・キャッシュ無しにする"
```

---

### Task 4: PF別コンパイル検査（Editorスクリプト＋ワークフロー）

Unity は `-executeMethod` を実行する前に全アセンブリをコンパイルする。したがって「対象 buildTarget へ切り替えた状態で軽量メソッドを実行できるか」がそのままコンパイル可否の判定になる。プレイヤービルドは行わない。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Editor/PlatformCompileCheck.cs`
- Create: `.github/workflows/platform-compile.yml`

**Interfaces:**
- Consumes: なし
- Produces:
  - `Server.Editor.PlatformCompileCheck.RunFromGithubAction()` — `public static void`、引数なし。`-executeMethod` から呼ばれ、必ず `EditorApplication.Exit(0)` か `EditorApplication.Exit(1)` で自プロセスを終了する。Task 4 の `platform-compile.yml` がこの完全修飾名を `buildMethod` として渡す。
  - ワークフロー名 `Platform Compile Check` — Task 6 の `ci-auto-rerun.yml` の監視対象に加える。
  - キャッシュキー接頭辞 `Library_compile_server_<target>-` — Task 5 の `cache-warm.yml` が同じ接頭辞で保存する。

- [x] **Step 1: 既存のEditorスクリプトの流儀を確認する**

Run:
```bash
sed -n '1,20p' moorestech_server/Assets/Scripts/Editor/BuildPipeline.cs
cat moorestech_server/Assets/Scripts/Editor/Server.Editor.asmdef
```
Expected: `BuildPipeline` はグローバル名前空間の `public class`、`UnityEditor` を using している。asmdef 名は `Server.Editor` で `includePlatforms` に `Editor` が入っている。

> **注意**: `BuildPipeline.cs` の CI エントリポイントは名前空間なしのグローバル `BuildPipeline` クラスである（`build.yml` の `buildMethod: BuildPipeline.WindowsBuildFromGithubAction` がその証拠）。新規ファイルは名前空間 `Server.Editor` を付けるため、`buildMethod` は `Server.Editor.PlatformCompileCheck.RunFromGithubAction` になる。

- [x] **Step 2: Editorスクリプトを作成する**

Create `moorestech_server/Assets/Scripts/Editor/PlatformCompileCheck.cs`:

```csharp
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Server.Editor
{
    /// <summary>
    /// プレイヤービルドを行わず、対象プラットフォームのdefineでスクリプトが通るかだけをCIで検査する。
    /// Unityは-executeMethodの実行前に全アセンブリをコンパイルするため、このメソッドに到達できた時点で
    /// コンパイルは成功している。到達後はアセンブリ一覧が空でないことだけ追加で確かめる。
    ///
    /// Verifies in CI that scripts compile under the target platform's defines, without building a player.
    /// Unity compiles every assembly before running -executeMethod, so reaching this method already proves
    /// compilation succeeded; afterwards it only additionally checks that the assembly list is not empty.
    /// </summary>
    public static class PlatformCompileCheck
    {
        public static void RunFromGithubAction()
        {
            // 実際に切り替わったターゲットを記録する（unity-builderの指定と食い違えば検査が無意味になる）
            // Record the target actually in effect; a mismatch with unity-builder's request would void the check
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            Debug.Log($"[PlatformCompileCheck] activeBuildTarget={activeTarget}");

            var defines = PlayerSettings.GetScriptingDefineSymbols(
                UnityEditor.Build.NamedBuildTarget.Standalone);
            Debug.Log($"[PlatformCompileCheck] defines={defines}");

            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            Debug.Log($"[PlatformCompileCheck] player assemblies={assemblies.Length}");

            // アセンブリが1つも無いのはインポートが破綻している状態で、コンパイル成功と区別する必要がある
            // Zero assemblies means the import itself is broken, which must be distinguished from a clean compile
            if (assemblies.Length == 0)
            {
                Debug.LogError("[PlatformCompileCheck] no player assemblies were produced");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[PlatformCompileCheck] " + string.Join(", ", assemblies.Select(a => a.name)));
            EditorApplication.Exit(0);
        }
    }
}
```

- [x] **Step 3: サーバープロジェクトのUnityを立ち上げてコンパイルする**

Run:
```bash
uloop launch ./moorestech_server
uloop compile --project-path ./moorestech_server
```
Expected: `Errors: 0`。`UnityEditor.Build.NamedBuildTarget` が解決できない等のエラーが出た場合は、`PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone)` へ置き換える（Unity 6 では前者が正だが、`using UnityEditor.Build;` の要否が環境で割れる）。

> ドメインリロード中で `uloop` が「Unity is reloading」を返す場合は45秒待ってリトライする。

- [x] **Step 4: `.meta` が生成されたことを確認する**

Run:
```bash
ls moorestech_server/Assets/Scripts/Editor/PlatformCompileCheck.cs.meta
```
Expected: ファイルが存在する（Unity が自動生成したもの。**手動作成は禁止**）。生成されていなければ Unity にフォーカスを当てて再インポートを待つ。

- [x] **Step 5: ワークフローを作成する**

Create `.github/workflows/platform-compile.yml`:

```yaml
# PRごとに、プレイヤービルドをせずPF別のスクリプトコンパイル可否だけを検査する（ADR 0028）。
# Linuxは既存のEditMode Test(ubuntuランナー)が実質カバーしているため対象に含めない。
# Per PR, check only whether scripts compile per platform, without building a player (ADR 0028).
# Linux is omitted because the existing EditMode Test already runs on an ubuntu runner.
name: Platform Compile Check

on:
  pull_request:
    paths-ignore:
      - '**/*.md'

  # 手動実行デバッグ用
  workflow_dispatch: {}

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

jobs:
  compile:
    name: Compile - ${{ matrix.target }}
    runs-on: ubuntu-latest

    strategy:
      fail-fast: false
      matrix:
        target:
          - StandaloneWindows64
          - StandaloneOSX

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          submodules: recursive
          fetch-depth: 0

      # PF固有アセットの再インポートを避けるためターゲットごとにLibraryを分けて復元する
      # Restore a separate Library per target so platform-specific assets are not reimported
      - uses: actions/cache@v4
        with:
          path: moorestech_server/Library
          key: Library_compile_server_${{ matrix.target }}-${{ github.sha }}
          restore-keys: |
            Library_compile_server_${{ matrix.target }}-

      # buildMethodはプレイヤービルドをせず終了するため、実質「そのdefineでコンパイルが通るか」の検査になる
      # The buildMethod exits without building a player, so this effectively checks compilation under those defines
      - name: Compile check - ${{ matrix.target }}
        uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          unityVersion: 6000.3.8f1
          buildMethod: Server.Editor.PlatformCompileCheck.RunFromGithubAction
          targetPlatform: ${{ matrix.target }}
          allowDirtyBuild: true
          projectPath: moorestech_server/
          versioning: None
```

> `versioning: None` を指定しているのは、この検査でバージョン採番が不要であり、`Semantic` のまま走らせると git 履歴に依存する処理が増えるため。**`build.yml` 側の `versioning` は触らない**（保留事項）。

- [x] **Step 6: YAML の妥当性と要件を検証する**

Run:
```bash
python3 -c "
import yaml
d = yaml.safe_load(open('.github/workflows/platform-compile.yml'))
assert d['name'] == 'Platform Compile Check'
j = d['jobs']['compile']
assert j['runs-on'] == 'ubuntu-latest'
assert j['strategy']['matrix']['target'] == ['StandaloneWindows64', 'StandaloneOSX']
step = [s for s in j['steps'] if s.get('uses', '').startswith('game-ci/unity-builder')][0]
assert step['with']['buildMethod'] == 'Server.Editor.PlatformCompileCheck.RunFromGithubAction'
assert step['with']['projectPath'] == 'moorestech_server/'
print('ok')
"
```
Expected: `ok`

- [x] **Step 7: 検査が実際にPF固有の破壊を捕まえることを確認する（手動スパイク）**

`moorestech_server/Assets/Scripts/Server.Boot/ServerDirectory.cs` の `#elif UNITY_STANDALONE_OSX` ブロック内に、一時的に構文エラー（例: `var broken = ;`）を入れてコミットし、PR 上で `Platform Compile Check` を走らせる。

Expected: `Compile - StandaloneOSX` が失敗し、`Compile - StandaloneWindows64` は成功する。確認後、**必ず構文エラーを戻してコミットし直す**。

> このスパイクは実際に GitHub 上で走らせないと検証できない。ローカルでは代替できないため省略しないこと。スパイク用のコミットはブランチ履歴に残してよいが、修正コミットを必ず後続に積む。

- [x] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Editor/PlatformCompileCheck.cs \
        moorestech_server/Assets/Scripts/Editor/PlatformCompileCheck.cs.meta \
        .github/workflows/platform-compile.yml
git commit -m "feat(ci): PF別コンパイル検査を新設し、ubuntu上でserverのWin/Mac defineを毎PR検査する"
```

---

### Task 5: masterでLibraryキャッシュを焼き、PR側の復元キーを揃える

Actions のキャッシュスコープは「自分の ref とベースブランチ」なので、master 上にキャッシュが無い限り PR は永遠にコールドで走る。トリガーが `pull_request` だけだった現状がまさにそれで、ログに `Cache not found for input keys: Server_Library_` が出ていた。

**Files:**
- Create: `.github/workflows/cache-warm.yml`
- Modify: `.github/workflows/run_test.yml`

**Interfaces:**
- Consumes: `Server.Editor.PlatformCompileCheck.RunFromGithubAction`（Task 4 で作成）
- Produces: キャッシュキー3系統
  - `Library_Test_client-<sha>`（`moorestech_client/Library`・約3.68GB）
  - `Library_compile_server_StandaloneWindows64-<sha>`（`moorestech_server/Library`・約1.17GB）
  - `Library_compile_server_StandaloneOSX-<sha>`（同・約1.17GB）

- [x] **Step 1: 現在のキャッシュ状況を記録する**

Run:
```bash
gh api repos/:owner/:repo/actions/cache/usage
gh api "repos/:owner/:repo/actions/caches?per_page=100" --jq '.actions_caches[]|[.ref,.key,(.size_in_bytes/1073741824*100|floor/100)]|@tsv'
```
Expected: `active_caches_size_in_bytes` が 10GB 前後で、`ref` が全て `refs/pull/*`（master のキャッシュが存在しない）。この出力を後の比較用に控える。

- [x] **Step 2: 既存のPR側キャッシュを一掃する**

古いキー（`Library_`, `Library_Test_`, `Server_Library_`）は新方式と混在すると10GB枠を圧迫するため削除する。

Run:
```bash
gh api "repos/:owner/:repo/actions/caches?per_page=100" --jq '.actions_caches[].id' \
  | while read id; do gh api -X DELETE "repos/:owner/:repo/actions/caches/$id"; done
gh api repos/:owner/:repo/actions/cache/usage
```
Expected: 最後の `active_caches_size_in_bytes` が 0 に近い値。

- [x] **Step 3: キャッシュ焼きワークフローを作成する**

Create `.github/workflows/cache-warm.yml`:

```yaml
# master上でLibraryキャッシュを焼く。Actionsのキャッシュスコープは「自分のref＋ベースブランチ」なので、
# masterに焼かない限りPR側は永遠にコールドで走る（ADR 0028）。日次ビルドの1時間前に回す。
# Warm the Library caches on master. Actions cache scope is "own ref plus base branch", so PRs stay
# cold forever unless master has them (ADR 0028). Runs one hour before the daily build.
name: Cache Warm

on:
  # UTC 18:00 = JST 翌03:00（日次ビルドの1時間前）
  # 18:00 UTC equals 03:00 JST the next day, one hour before the daily build
  schedule:
    - cron: '0 18 * * *'

  # 手動実行デバッグ用
  workflow_dispatch: {}

concurrency:
  group: ${{ github.workflow }}
  cancel-in-progress: false

jobs:
  # EditMode Testが使うclient Libraryを焼く
  # Warm the client Library used by the EditMode Test
  warm-client-test:
    name: Warm client Library (EditMode Test)
    runs-on: ubuntu-latest
    steps:
      - name: Check out my unity project.
        uses: actions/checkout@v4
        with:
          submodules: recursive
          fetch-depth: 0
          path: moorestech

      - name: Read pinned master repository revision.
        id: master_revision
        shell: bash
        working-directory: moorestech
        run: |
          commit_hash=$(jq -er '.repositories[] | select(.key == "moorestech_master") | .commitHash' .moorestech-external-revisions.json)
          echo "commit_hash=$commit_hash" >> "$GITHUB_OUTPUT"

      - name: Generate github token
        id: generate_token
        uses: tibdex/github-app-token@v1
        with:
          app_id: ${{ secrets.APP_ID }}
          private_key: ${{ secrets.PRIVATE_KEY }}

      - name: Check out pinned master repository.
        uses: actions/checkout@v4
        with:
          repository: moorestech/moorestech_master
          token: ${{ steps.generate_token.outputs.token }}
          ref: ${{ steps.master_revision.outputs.commit_hash }}
          path: moorestech_master

      - name: Setup Web UI toolchain
        shell: bash
        working-directory: moorestech/moorestech_web
        env:
          PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD: '1'
        run: |
          bash setup.sh
          shopt -s nullglob
          candidates=(node/*/pnpm)
          pnpm_binary="$PWD/${candidates[0]}"
          if [ ! -f "$pnpm_binary" ]; then
            echo "pnpm not found after setup: $pnpm_binary" >&2
            exit 1
          fi
          "$pnpm_binary" --dir webui install --frozen-lockfile

      - uses: actions/cache@v4
        with:
          path: moorestech/moorestech_client/Library
          key: Library_Test_client-${{ github.sha }}
          restore-keys: |
            Library_Test_client-

      # テストを1本も実行せずインポートだけ済ませる（Libraryを作るのが目的）
      # Import only, without running any test, since the goal is to produce the Library
      - name: Warm Library
        uses: game-ci/unity-test-runner@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath: moorestech/moorestech_client/
          githubToken: ${{ steps.generate_token.outputs.token }}
          unityVersion: 6000.3.8f1
          testMode: editmode
          customParameters: '-testFilter __cache_warm_matches_nothing__'

  # PF別コンパイル検査が使うserver Libraryをターゲットごとに焼く
  # Warm the server Library per target for the platform compile check
  warm-server-compile:
    name: Warm server Library - ${{ matrix.target }}
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        target:
          - StandaloneWindows64
          - StandaloneOSX
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          submodules: recursive
          fetch-depth: 0

      - uses: actions/cache@v4
        with:
          path: moorestech_server/Library
          key: Library_compile_server_${{ matrix.target }}-${{ github.sha }}
          restore-keys: |
            Library_compile_server_${{ matrix.target }}-

      - name: Warm Library - ${{ matrix.target }}
        uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          unityVersion: 6000.3.8f1
          buildMethod: Server.Editor.PlatformCompileCheck.RunFromGithubAction
          targetPlatform: ${{ matrix.target }}
          allowDirtyBuild: true
          projectPath: moorestech_server/
          versioning: None
```

- [x] **Step 4: run_test.yml のキャッシュキーを新方式へ揃える**

`.github/workflows/run_test.yml` の日時取得ステップ（Task 1 で `shell: bash` を足したもの）とキャッシュステップを、次に置き換える。

削除するもの:
```yaml
      # 年月日を取得　キャッシュのkeyに利用
      - name: Set current datetime as env variable
        shell: bash
        env:
          TZ: 'Asia/Tokyo'
        run: echo "CURRENT_DATETIME=$(date +'%Y-%m')" >> $GITHUB_ENV

      # キャッシュ keyを月跨ぎで更新することで定期的にキャッシュ自体の更新を行う
      - uses: actions/cache@v4
        with:
          path: moorestech/moorestech_client/Library
          key: "Library_Test_${{ env.CURRENT_DATETIME }}"
```

追加するもの:
```yaml
      # masterのcache-warmが焼いたキャッシュをrestore-keysの前方一致で拾う（ADR 0028）
      # Pick up whatever master's cache-warm produced via the restore-keys prefix (ADR 0028)
      - uses: actions/cache@v4
        with:
          path: moorestech/moorestech_client/Library
          key: Library_Test_client-${{ github.sha }}
          restore-keys: |
            Library_Test_client-
```

- [x] **Step 5: キー接頭辞が3ファイルで一致していることを検証する**

Run:
```bash
grep -h "Library_Test_client-\|Library_compile_server_" \
  .github/workflows/cache-warm.yml \
  .github/workflows/run_test.yml \
  .github/workflows/platform-compile.yml | sed 's/^ *//' | sort | uniq -c
```
Expected: `Library_Test_client-` が `cache-warm.yml` と `run_test.yml` の双方に（key と restore-keys で計4行）、`Library_compile_server_${{ matrix.target }}-` が `cache-warm.yml` と `platform-compile.yml` の双方に（計4行）現れる。接頭辞の綴りが3ファイルで完全一致していること。

- [ ] **Step 6: 手動でキャッシュ焼きを実行して結果を確認する** — **マージ前は実行不能（2026-08-22 実測）**。GitHubは既定ブランチに存在しないワークフローを `workflow_dispatch` で発火できず、`gh workflow run "Cache Warm" --ref feature/ci-build-strategy` も API の dispatches 直叩きも 404 を返す。masterマージ後に実施する（bdへ起票済み）

Run:
```bash
gh workflow run "Cache Warm" --ref feature/ci-build-strategy
```

> `schedule` で走るワークフローは既定ブランチの内容が使われるため、**本番の日次焼きは master へマージするまで動かない**。ここでは `workflow_dispatch` でブランチ指定して動作確認する。

完了後に:
```bash
gh api repos/:owner/:repo/actions/cache/usage
gh api "repos/:owner/:repo/actions/caches?per_page=100" --jq '.actions_caches[]|[.ref,.key,(.size_in_bytes/1073741824*100|floor/100)]|@tsv'
```
Expected: 3件のキャッシュが `Library_Test_client-*` と `Library_compile_server_*-*` のキーで保存され、合計が **6.5GB 以下**（10GB枠に収まる）。超えている場合は Task 5 を止めてユーザーへ報告する（枠設計の前提が崩れているため）。

- [x] **Step 7: コミットする**

```bash
git add .github/workflows/cache-warm.yml .github/workflows/run_test.yml
git commit -m "feat(ci): masterでLibraryキャッシュを焼くワークフローを新設し、PR側をローリングキー+restore-keysへ移行する"
```

---

### Task 6: ci-auto-rerun を日次にも効かせ、失敗を専用ラベルIssueにする

`ci-auto-rerun.yml` は `github.event.workflow_run.event == 'pull_request'` に絞られているため、日次（`schedule`）へ移した瞬間に自動再実行が効かなくなる。再実行しても赤い失敗だけを Issue にする。

**Files:**
- Modify: `.github/workflows/ci-auto-rerun.yml`
- Create: `.github/workflows/daily-build-issue.yml`
- Create: `.github/scripts/daily-build-issue.cjs`

**Interfaces:**
- Consumes: ワークフロー名 `Unity Build`（Task 3）、`Platform Compile Check`（Task 4）
- Produces:
  - GitHub ラベル `日次ビルド失敗` を持つ Issue。本文には `<!-- daily-build-issue -->` マーカー、`前回グリーン: <sha>`、`容疑者PR:` 見出しの箇条書き、`失敗ジョブ:` 見出しとログ抜粋を含む。**別 plan（poller 側）はこのマーカーとラベルで Issue を識別する。**
  - `.github/scripts/daily-build-issue.cjs` は `module.exports = async ({ github, context, core }) => {...}` を default export する（`ci-auto-rerun.cjs` と同形式）。

- [x] **Step 1: 既存の再実行スクリプトの契約を読む**

Run:
```bash
sed -n '1,40p' .github/scripts/ci-auto-rerun.cjs
grep -n "module.exports" .github/scripts/ci-auto-rerun.cjs
```
Expected: `module.exports = async ({ github, context, core }) => {...}` の形式。この形式に合わせて新スクリプトを書く。

- [x] **Step 2: ci-auto-rerun の発火条件を広げる**

`.github/workflows/ci-auto-rerun.yml` の `if` を次に置き換える。

変更前:
```yaml
    if: >-
      github.event.workflow_run.event == 'pull_request' &&
      (github.event.workflow_run.conclusion == 'failure' ||
       github.event.workflow_run.conclusion == 'timed_out')
```

変更後:
```yaml
    # 日次(schedule)へ移したUnity Buildもフレーク再実行の対象にする（ADR 0028）
    # The daily (scheduled) Unity Build is also eligible for flaky reruns (ADR 0028)
    if: >-
      (github.event.workflow_run.event == 'pull_request' ||
       github.event.workflow_run.event == 'schedule') &&
      (github.event.workflow_run.conclusion == 'failure' ||
       github.event.workflow_run.conclusion == 'timed_out')
```

あわせて `on.workflow_run.workflows` のリストに `- "Platform Compile Check"` を追加する。

- [x] **Step 3: 日次失敗をIssue化するスクリプトを作成する**

Create `.github/scripts/daily-build-issue.cjs`:

```javascript
// 日次(schedule)のUnity Buildが自動再実行後も赤いとき、専用ラベル付きIssueを起票・更新する。
// 同じ失敗が続く間は新規起票せず既存Issueへコメントし、緑に戻ったら自動クローズする。
// Files or updates a labelled issue when the daily (scheduled) Unity Build stays red after auto-rerun.
// While the same failure persists it comments on the existing issue instead of opening a new one,
// and it closes the issue automatically once the daily build goes green again.

const LABEL = '日次ビルド失敗';
const MARKER = '<!-- daily-build-issue -->';

module.exports = async ({ github, context, core }) => {
  const run = context.payload.workflow_run;
  const { owner, repo } = context.repo;

  const existing = await findExistingIssue();

  if (run.conclusion === 'success') {
    if (existing) {
      await github.rest.issues.createComment({
        owner, repo, issue_number: existing.number,
        body: `日次ビルドが緑に戻ったため自動クローズします。\nrun: ${run.html_url}`,
      });
      await github.rest.issues.update({
        owner, repo, issue_number: existing.number, state: 'closed',
      });
      core.info(`closed issue #${existing.number}`);
    }
    return;
  }

  const lastGreenSha = await findLastGreenSha();
  const suspects = await listMergedPullRequests(lastGreenSha, run.head_sha);
  const failedJobs = await listFailedJobs();
  const body = renderBody({ run, lastGreenSha, suspects, failedJobs });

  if (existing) {
    await github.rest.issues.createComment({ owner, repo, issue_number: existing.number, body });
    core.info(`commented on issue #${existing.number}`);
    return;
  }

  const created = await github.rest.issues.create({
    owner, repo,
    title: `日次ビルド失敗: ${run.head_sha.slice(0, 8)}`,
    labels: [LABEL],
    body,
  });
  core.info(`created issue #${created.data.number}`);

  async function findExistingIssue() {
    const issues = await github.rest.issues.listForRepo({
      owner, repo, state: 'open', labels: LABEL, per_page: 20,
    });
    return issues.data.find((i) => (i.body || '').includes(MARKER)) || null;
  }

  async function findLastGreenSha() {
    const runs = await github.rest.actions.listWorkflowRuns({
      owner, repo, workflow_id: run.workflow_id,
      event: 'schedule', status: 'success', per_page: 1,
    });
    return runs.data.workflow_runs.length > 0 ? runs.data.workflow_runs[0].head_sha : null;
  }

  async function listMergedPullRequests(baseSha, headSha) {
    if (!baseSha) return [];
    const compare = await github.rest.repos.compareCommits({
      owner, repo, base: baseSha, head: headSha,
    });
    const seen = new Map();
    for (const commit of compare.data.commits) {
      const matched = /^Merge pull request #(\d+)|\(#(\d+)\)$/m.exec(commit.commit.message);
      if (!matched) continue;
      const number = matched[1] || matched[2];
      if (!seen.has(number)) {
        seen.set(number, commit.commit.message.split('\n')[0]);
      }
    }
    return [...seen.entries()].map(([number, title]) => ({ number, title }));
  }

  async function listFailedJobs() {
    const jobs = await github.rest.actions.listJobsForWorkflowRun({
      owner, repo, run_id: run.id, per_page: 50,
    });
    return jobs.data.jobs
      .filter((j) => j.conclusion === 'failure')
      .map((j) => ({ name: j.name, url: j.html_url }));
  }

  function renderBody({ run, lastGreenSha, suspects, failedJobs }) {
    const lines = [MARKER, ''];
    lines.push(`日次ビルドが自動再実行後も失敗しました。`);
    lines.push(`- run: ${run.html_url}`);
    lines.push(`- head: \`${run.head_sha}\``);
    lines.push(`- 前回グリーン: ${lastGreenSha ? `\`${lastGreenSha}\`` : '（成功記録なし）'}`);
    lines.push('');
    lines.push('## 失敗ジョブ:');
    for (const job of failedJobs) {
      lines.push(`- [${job.name}](${job.url})`);
    }
    lines.push('');
    lines.push('## 容疑者PR:');
    if (suspects.length === 0) {
      lines.push('- （前回グリーンからの差分を特定できませんでした）');
    }
    for (const pr of suspects) {
      lines.push(`- #${pr.number} ${pr.title}`);
    }
    lines.push('');
    lines.push('前方修正で対応します。bisectは行いません（ADR 0028）。');
    return lines.join('\n');
  }
};
```

- [x] **Step 4: Issue起票ワークフローを作成する**

Create `.github/workflows/daily-build-issue.yml`:

```yaml
# 日次(schedule)のUnity Buildの結末を受けて、専用ラベルIssueを起票・更新・クローズする（ADR 0028）。
# ci-auto-rerunによる再実行のあとの最終的な結末だけを見るため、workflow_runの完了を待って動く。
# Reacts to the outcome of the daily (scheduled) Unity Build to file, update, or close a labelled issue (ADR 0028).
# It runs on workflow_run completion so it only observes the final outcome after ci-auto-rerun has retried.
name: Daily Build Issue

on:
  workflow_run:
    workflows:
      - "Unity Build"
    types:
      - completed

permissions:
  issues: write
  actions: read
  contents: read

jobs:
  file-issue:
    runs-on: ubuntu-latest
    # 日次(schedule)起因のrunだけを対象にする。ラベル発火や手動実行では起票しない
    # Only scheduled runs are eligible; label-triggered and manual runs never file an issue
    if: github.event.workflow_run.event == 'schedule'
    steps:
      - name: Checkout issue script
        uses: actions/checkout@v4
        with:
          filter: blob:none
          sparse-checkout: .github/scripts

      - name: File or update the daily build issue
        uses: actions/github-script@v7
        with:
          script: |
            const fileDailyBuildIssue = require('./.github/scripts/daily-build-issue.cjs');
            await fileDailyBuildIssue({ github, context, core });
```

- [x] **Step 5: スクリプトが構文として妥当か確認する**

Run:
```bash
node --check .github/scripts/daily-build-issue.cjs && echo "syntax ok"
node -e "const m = require('./.github/scripts/daily-build-issue.cjs'); console.log(typeof m === 'function' ? 'export ok' : 'export NG')"
```
Expected: `syntax ok` と `export ok`

- [x] **Step 6: ラベルをリポジトリに作成する**

Run:
```bash
gh label create "ビルド検証" --description "付けるとUnity Buildが走る" --color "1D76DB" || true
gh label create "日次ビルド失敗" --description "日次フルビルドが再実行後も失敗した" --color "B60205" || true
gh label list --limit 100 | grep -E "ビルド検証|日次ビルド失敗"
```
Expected: 2つのラベルが一覧に出る（既存なら `|| true` で握りつぶされる）。

- [x] **Step 7: YAML の妥当性と発火条件を検証する**

Run:
```bash
python3 -c "
import yaml
a = yaml.safe_load(open('.github/workflows/ci-auto-rerun.yml'))
assert 'schedule' in a['jobs']['auto-rerun']['if']
assert 'Platform Compile Check' in a[True]['workflow_run']['workflows']
d = yaml.safe_load(open('.github/workflows/daily-build-issue.yml'))
assert d[True]['workflow_run']['workflows'] == ['Unity Build']
assert d['jobs']['file-issue']['if'] == \"github.event.workflow_run.event == 'schedule'\"
assert d['permissions']['issues'] == 'write'
print('ok')
"
```
Expected: `ok`

- [x] **Step 8: コミットする**

```bash
git add .github/workflows/ci-auto-rerun.yml .github/workflows/daily-build-issue.yml .github/scripts/daily-build-issue.cjs
git commit -m "feat(ci): 日次ビルドをフレーク再実行の対象に加え、再実行後も赤い失敗を専用ラベルIssueにする"
```

---

### Task 7: 全ブランチレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、`master...feature/ci-build-strategy` の差分全体をレビューする。

**このタスクは自動実行であり、ゴール文言（「全部できた」等）による省略はできない。** plan から省略してもゲートは免除されない。

- [ ] **Step 2: 指摘のうち機械的修正を適用し、設計判断は AskUserQuestion でユーザーへ諮る**

- [ ] **Step 3: 修正をコミットする**

```bash
git add -A
git commit -m "fix(ci): レビュー指摘を反映"
```

---

## 判断記録（ADR）

設計セッションのADR: `docs/adr/0028-ci-build-strategy.md`
裁定の原本: `.decisions/2026-08-21-*`（10件）、`.decisions/2026-08-22-無人修復の深夜枠は4時開始9時打ち切りとする.md`

planning中に新たに生じた判断:

- **A→Bのplan分割**（タスク分割）: 本plan（GitHub Actions）と poller 側を別planにした。`~/hermes-agent/data/services/pr-review/` は git 管理下ですらなく、デリバリ手段もレビュー経路も異なるサブシステムであるため。本planは「Issue が正しいラベルと本文で立つ」までを完成とする。*出所: agent前提（writing-plans の Scope Check）*
- **PF別コンパイル検査の機構**: `game-ci/unity-builder` の `buildMethod` に、プレイヤービルドをせず自分で `EditorApplication.Exit` する軽量メソッドを渡す形を採る。Unity は `-executeMethod` 実行前に全アセンブリをコンパイルするため、メソッドに到達できること自体がコンパイル成功の証明になる。*出所: agent前提（既存 `build.yml` が同じ `unity-builder` + `buildMethod` 経路を使っている前例に合わせた）*
- **キャッシュ焼きは日次ビルドの1時間前（03:00 JST）**: PR が日中に使うキャッシュを、その日の日次ビルドより先に更新しておくため。*出所: agent前提*
- **`platform-compile.yml` と `cache-warm.yml` は `versioning: None`**: これらはバージョン採番を必要とせず、git 履歴依存を増やさないため。**`build.yml` の `versioning` は変更しない**（`Semantic` のまま。保留事項に含まれる）。*出所: agent前提*
- **ラベル名は `ビルド検証` / `日次ビルド失敗`**: 既存の `独立レビュー&対応完了` に倣い日本語で統一した。*出所: agent前提*
- **Issue の識別子は `<!-- daily-build-issue -->` HTMLコメント**: poller 側（別plan）がラベルとこのマーカーで対象を判定できるようにする。*出所: agent前提*
- **既存キャッシュの一掃を Task 5 に含める**: 旧キー（`Library_`, `Server_Library_`, `Library_Test_`）が新方式と混在すると10GB枠を即座に食い潰すため。*出所: agent前提*

## レイヤリング制約（配置と前例）

- **ワークフローはすべて `.github/workflows/` 直下**。既存8ファイルと同じ階層で、サブディレクトリは作らない（GitHub Actions がサブディレクトリを読まないため）。
- **判定ロジックは `.github/scripts/*.cjs` に置き、ワークフローからは `require` するだけ**。前例: `ci-auto-rerun.yml` が `.github/scripts/ci-auto-rerun.cjs` を `actions/github-script@v7` から `require` している。`daily-build-issue.cjs` は同じ `module.exports = async ({ github, context, core })` シグネチャに合わせた。
- **Editor 専用の C# は `moorestech_server/Assets/Scripts/Editor/`（`Server.Editor.asmdef` 配下）**。前例: `BuildPipeline.cs`（CI エントリポイント）、`CliTestRunner.cs` が同じ場所にある。`Server.Editor.asmdef` は `includePlatforms: [Editor]` なのでプレイヤービルドには含まれない。
- **CI エントリポイントは `public static void` の引数なしメソッド**。前例: `BuildPipeline.WindowsBuildFromGithubAction()`。

**新規パターン（レビュー注目点）:**

1. **`buildMethod` をプレイヤービルド以外に流用する**のは本リポジトリ初。既存の `buildMethod` は全て `BuildPipeline.BuildPlayer` を呼ぶ。`unity-builder` の既定スクリプトがビルド成果物の存在を前提にしていないか、Task 4 Step 7 の実走スパイクで必ず確認すること。
2. **`cache-warm.yml` の client 側で `unity-test-runner` を「1本もマッチしないフィルタ」で回す**のも初。テスト実行ではなくインポートが目的。`customParameters: '-testFilter __cache_warm_matches_nothing__'` が「0件でも成功終了する」かは実走で確認が要る。失敗扱いになる場合は `continue-on-error: true` を足すか、`unity-builder` + `PlatformCompileCheck` 相当のメソッドを client 側にも作る方式へ切り替える（その場合は client プロジェクトにも Editor スクリプトが増えるため、ユーザーへ諮ること）。
