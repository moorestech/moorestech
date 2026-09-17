# CI shard ハングの failure 化とアイコン撮影の計装 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** CIのshardハングを40分でfailureにして既存の自動再実行に乗せ、同時にアイコン撮影へ進捗ログを入れて次の1件で固着箇所を確定できるようにする。

**Architecture:** 変更は2箇所だけ。(1) `.github/workflows/run_test.yml` のテスト実行stepに `timeout-minutes: 40` を足す。job の `timeout-minutes` による打ち切りは run の結論が `cancelled` になり既存 watchdog（`ci-auto-rerun.yml`・発火条件は `failure` / `timed_out`）を素通りするが、**step の timeout は job を failure にする**ため、判定ロジックを一切増やさずに既存の自動再実行がそのまま発火する。(2) `BlockIconImagePhotographer.TakeIconImages` に、撮影の開始・各対象の段階（setup / render / readback / captured / done の5点）・完了の進捗ログを入れる。固着はメインスレッドごと止まるため、**各段階へ入る直前にログを出しておく以外に箇所を知る方法が無い**。

**Tech Stack:** GitHub Actions (YAML)、Unity 6000.3.8f1、C#（UniTask）、NUnit / Unity Test Framework（`[UnityTest]`）

## Requirements

- R1: `.github/workflows/run_test.yml` の `unity_test_shard` のテスト実行 step が40分で打ち切られること。受け入れ基準: step に `timeout-minutes: 40` があり、job 側の `timeout-minutes: 75` は残っている。
- R2: 打ち切られた shard が `cancelled` ではなく `failure` になること（既存 watchdog の発火条件に合致させるため）。受け入れ基準: Task 1 の実測で job の conclusion が `failure` であることを確認済みであり、そうでなければ Task 1 Step 4 の代替形（`continue-on-error` + 明示的に失敗させる step）を採る。この挙動に依存していることが workflow のコメントに日本語・英語の2行で書かれている。
- R3: `ci-auto-rerun.cjs` / `ci-auto-rerun.yml` を変更しないこと。受け入れ基準: このPRの差分に両ファイルが含まれない。
- R4: アイコン撮影が「何個目の・どの撮影対象の・どの段階で」止まったかがログだけで判別できること。受け入れ基準: 撮影の開始（総数）、各対象の setup 直前・render 直前・readback 直前・captured（破棄直前）・完了、撮影全体の完了（総数と所要秒）がログに出る。
- R5: 計装がテストで固定されていること。受け入れ基準: 撮影1件を実行して期待するログ行が期待順で出ることを検証する `[UnityTest]` がある。
- R6: 計装に環境分岐（batchmode 判定・CI 判定）を入れないこと。受け入れ基準: 追加コードに `Application.isBatchMode` や CI 判定が現れない。
- R7: 撮影の挙動（生成・破棄のライフサイクル、返すTexture）を変えないこと。受け入れ基準: 既存の `BlockIconImagePhotographerLifetimeTest` の2テストが通る。
- やらないこと: 真因そのものの修正（`AsyncGPUReadback` 化・CIで撮影を省く等）。boot パイプライン全体への計装。別スレッドの監視役。`ci-auto-rerun.cjs` の `cancelled` 対応。マージ前の反復CI検証。

## Global Constraints

- ADR 0063（`docs/adr/0063-ci-hang-shard-timeout-and-icon-capture-instrumentation.md`）と `.decisions/2026-09-18-CIのshardハングはstep-timeoutでfailure化し真因は計装で確定させる.md` が裁定の正本。逸脱する実装をしない。
- コメントは日本語・英語の2行セット（`// 日本語` → `// English`）。各言語1行に収める。自明なコメントは書かない。
- 1ファイル200行以下。`partial` 禁止。`Func<>` 禁止。try-catch は外部境界のみ。
- `.cs` を変更したら必ずコンパイルを実行する（`uloop compile --project-path ./moorestech_client`）。
- `.meta` ファイルは手で作らない。レビュー反映（R1）でテストファイルを分割したため新規 `.cs` が2本増えており、その `.meta` は Unity Editor に生成させてコミットする。
- 作業ブランチ: `fix/ci-shard-hang-timeout-and-icon-instrumentation`（worktree: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/ci-shard-hang-timeout-and-icon-instrumentation`）。Unity Editor は同worktreeのものを使う。
- 作業の区切りごとにコミットする。タスク終了前に必ず全変更をコミットする。

---

### Task 1: step timeout が job を failure にすることを実測で確かめる

このplan全体が「step の `timeout-minutes` を超えた step は failure になり、job も failure で終わる（cancelled にならない）」という
**GitHub Actions 側の挙動**に乗っている。公式ドキュメントはこの挙動を明記しておらず、「step の `timeout-minutes` は無視される」と書く
二次情報も存在する。1サンプルの伝聞を規則として採用せず、使い捨ての workflow で実測してから本適用する。

**Files:**
- Create: `.github/workflows/tmp-step-timeout-probe.yml`（このタスクの中で削除する）

**Interfaces:**
- Consumes: なし
- Produces: 実測結果（job の conclusion）。Task 2 が採る YAML の形をこれで決める。

- [ ] **Step 1: 使い捨ての検証 workflow を作る**

`.github/workflows/tmp-step-timeout-probe.yml`:

```yaml
# step の timeout-minutes を超えた job が failure になるか cancelled になるかを実測する使い捨てworkflow。
# A throwaway workflow measuring whether exceeding a step-level timeout-minutes ends the job as failure or cancelled.
# 実測後に削除する（ADR 0063 Task 1）。
# Deleted right after the measurement (ADR 0063 Task 1).
name: Tmp Step Timeout Probe

on:
  workflow_dispatch: {}

jobs:
  probe:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      # 実際のテスト実行stepと同じく uses: のアクションstepで測る（run: とで挙動が違う可能性を潰す）。
      # Measured on a uses: action step like the real test step, ruling out a difference from run: steps.
      - name: Sleep past the step timeout
        uses: actions/github-script@v7
        timeout-minutes: 1
        with:
          script: await new Promise((resolve) => setTimeout(resolve, 150000))

      - name: Report that the probe reached the following step
        if: always()
        run: echo "reached the step after the timed-out step"
```

- [ ] **Step 2: コミットして push する**

```bash
git add .github/workflows/tmp-step-timeout-probe.yml
git commit -m "ci: step timeoutの挙動を実測する使い捨てworkflowを足す

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
git push -u origin fix/ci-shard-hang-timeout-and-icon-instrumentation
```

- [ ] **Step 3: 実行して結果を読む**

```bash
gh workflow run tmp-step-timeout-probe.yml --ref fix/ci-shard-hang-timeout-and-icon-instrumentation
sleep 150
gh run list --workflow tmp-step-timeout-probe.yml --limit 1 --json databaseId,conclusion --jq '.[0]'
```

続けて job と step の結論を読む（`<id>` は上で得た databaseId）:

```bash
gh run view <id> --json conclusion,jobs --jq '{run:.conclusion, jobs:[.jobs[]|{name,conclusion,steps:[.steps[]|{name,conclusion}]}]}'
```

Expected（本planが前提にしている挙動）: run と job の conclusion が `failure`、`Sleep past the step timeout` step の conclusion が `failure`。

- [ ] **Step 4: 結果に応じて Task 2 で採る形を決める**

- run/job が `failure` だった → Task 2 はそのまま（`timeout-minutes: 40` を足すだけ）。
- run/job が `cancelled` だった、または step timeout が効かず10分の job timeout まで走った → Task 2 Step 2 の YAML を次の形に差し替える（`continue-on-error` で打ち切りを job の失敗と切り離し、後続の step が明示的に失敗させる）。この場合も `ci-auto-rerun.cjs` は変更しない:

```yaml
      - name: Run Unity Test - ${{ matrix.shard }}
        id: unity_test
        uses: game-ci/unity-test-runner@0ff419b913a3630032cbe0de48a0099b5a9f0ed9 # v4.3.1
        # 打ち切りをjobのcancelledにせず、後続stepでfailureへ変換する（ADR 0063）。
        # Keep the cutoff from cancelling the job; the following step converts it into a failure (ADR 0063).
        continue-on-error: true
        timeout-minutes: 40
        env:
          # 既存のenv・withブロックはそのまま
      - name: Fail the job when the shard did not pass
        if: steps.unity_test.outcome != 'success'
        shell: bash
        run: |
          echo "Unity test shard outcome: ${{ steps.unity_test.outcome }}" >&2
          exit 1
```

実測結果（run/job/step の conclusion）を、この plan の `## 判断記録（ADR）` へ1行追記する。

- [ ] **Step 5: 使い捨て workflow を削除してコミットする**

```bash
git rm .github/workflows/tmp-step-timeout-probe.yml
git commit -m "ci: 実測に使った使い捨てworkflowを削除する

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 6: 削除できたことを確認する**

Run: `ls .github/workflows/`

Expected: `tmp-step-timeout-probe.yml` が無い。

---

### Task 2: shard のテスト実行 step を40分で failure にする

**Files:**
- Modify: `.github/workflows/run_test.yml` の shard テスト実行 step。post-check C-1 の是正で、step 名は `Run Unity Test - ${{ matrix.shard }}` のまま据え置き、代わりに前後へ3本（開始時刻の記録・ハング判定・テスト失敗の顕在化）を足す。`ci-auto-rerun.cjs` は `INFRA_KEYWORDS` を `CODE_KEYWORDS` より先に評価するため、テスト実行 step 自体を infra 語へ寄せると通常のテスト失敗まで再実行されてしまう

**Interfaces:**
- Consumes: なし
- Produces: なし（workflow 定義のみ。後段タスクはこれに依存しない）

- [ ] **Step 1: 現在の step 定義を確認する**

Run: `sed -n 170,200p .github/workflows/run_test.yml`

Expected: `- name: Run Unity Test - ${{ matrix.shard }}` の下に `uses: game-ci/unity-test-runner@0ff419b913a3630032cbe0de48a0099b5a9f0ed9 # v4.3.1` があること。

- [ ] **Step 2: 打ち切り時間の正本・テスト step・判定2 step を置く**

`.github/workflows/run_test.yml` の `unity_test_shard` job に、打ち切り時間の正本となる job 単位の `env` を足す（`timeout-minutes: 75` の直後）:

```yaml
    env:
      UNITY_TEST_STEP_TIMEOUT_MINUTES: 40
```

該当 step を次の形にする（`env:` 以降の secrets・`with:` は既存のまま変更しない）。名前の割り当ては `ci-auto-rerun.cjs` の分類に直接効く — INFRA が先に評価されるため、infra 語を含む step が落ちた時点で分類は infra に確定する:

```yaml
      # 打ち切りに達したかを後続stepが根拠付きで判定できるよう、テスト開始時刻をepoch秒で残す。
      # Record the test start time in epoch seconds so a later step can decide on evidence whether the cutoff was reached.
      - name: Record Unity shard start time.
        shell: bash
        run: echo "UNITY_TEST_STARTED_AT=$(date +%s)" >> "$GITHUB_ENV"

      - name: Run Unity Test - ${{ matrix.shard }}
        id: unity_test
        continue-on-error: true
        uses: game-ci/unity-test-runner@0ff419b913a3630032cbe0de48a0099b5a9f0ed9 # v4.3.1
        # 素の ${{ env.X }} は文字列のため無視される（実測 run 35269448343）。fromJSON で数値にする。
        # A bare ${{ env.X }} is a string and gets ignored (measured in run 35269448343); fromJSON makes it a number.
        timeout-minutes: ${{ fromJSON(env.UNITY_TEST_STEP_TIMEOUT_MINUTES) }}
        env:

      # ハングだけをここで失敗させる。名前の 'runner' が INFRA_KEYWORDS に当たり attempt 2 以降も自動再実行される。
      # Fail here only for a hang: the word 'runner' matches INFRA_KEYWORDS so the auto-rerun keeps firing from attempt 2 on.
      - name: Detect Unity shard runner hang
        if: always()
        # 経過が timeout 値に達したときだけ exit 1（詳細は run_test.yml の実装を参照）

      # 通常のテスト失敗をjobの失敗として残す口。名前は CODE_KEYWORDS の 'test' に当たり infra語を含まない。
      # This is where a plain test failure stays a job failure; the name matches CODE_KEYWORDS 'test' and carries no infra word.
      - name: Fail the shard when the Unity test did not pass
        if: always()
```

- [ ] **Step 3: YAML の値と step 名の分類を機械で確かめる**

Run（`ci-auto-rerun.cjs` の実際のキーワード配列を読み、9 shard 展開後の名前で突き合わせる。step は `id` で引き名前依存にしない）:
```bash
python3 - <<'EOF'
import re, yaml
cjs = open('.github/scripts/ci-auto-rerun.cjs').read()
arr = lambda n: re.findall(r"'([^']+)'", re.search(r'const %s = \[(.*?)\];' % n, cjs, re.S).group(1))
INFRA, CODE = arr('INFRA_KEYWORDS'), arr('CODE_KEYWORDS')
hits = lambda name, kws: [k for k in kws if k in name.lower()]
job = yaml.safe_load(open('.github/workflows/run_test.yml'))['jobs']['unity_test_shard']
test = [s for s in job['steps'] if s.get('id') == 'unity_test'][0]
print('job timeout:', job['timeout-minutes'])
print('step timeout:', job['env']['UNITY_TEST_STEP_TIMEOUT_MINUTES'], '(via', test['timeout-minutes'], ')')
for s in job['steps']:
    n = s.get('name', '')
    if n.startswith(('Run Unity Test', 'Detect Unity shard runner', 'Fail the shard')):
        print(repr(n), 'infra=', hits(n, INFRA), 'code=', hits(n, CODE))
EOF
```

Expected:
```
job timeout: 75
step timeout: 40 (via ${{ fromJSON(env.UNITY_TEST_STEP_TIMEOUT_MINUTES) }} )
'Run Unity Test - ${{ matrix.shard }}' infra= [] code= ['test']
'Detect Unity shard runner hang' infra= ['runner'] code= []
'Fail the shard when the Unity test did not pass' infra= [] code= ['test']
```

- [ ] **Step 4: watchdog 側を触っていないことを確認する**

Run: `git status --short .github/`

Expected: `M .github/workflows/run_test.yml` の1行だけ。`ci-auto-rerun.yml` と `.github/scripts/ci-auto-rerun.cjs` は出てこない（R3）。

- [ ] **Step 5: コミットする**

```bash
git add .github/workflows/run_test.yml
git commit -m "ci: shardのテスト実行stepを40分で打ち切りハングをfailure化する

jobのtimeout-minutesによる打ち切りはrunの結論がcancelledになり、既存の
ci-auto-rerun（発火条件は failure / timed_out）が発火しない。stepのtimeoutは
jobをfailureにするため、判定ロジックを増やさずに自動再実行が効く。

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: アイコン撮影に進捗ログを入れる

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockIconImagePhotographer.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Block/BlockIconImagePhotographerCaptureLogTest.cs`（段階ログのテスト。新規クラス）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Block/BlockIconCaptureTestEnvironment.cs`（両テストクラスが共有する命名規約・待機・後片付け。新規）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Block/BlockIconImagePhotographerLifetimeTest.cs`（既存の寿命テスト2本を残し、共有部分をヘルパーへ寄せる）

> レビュー反映（2026-09-18・R1）で、段階ログのテストは既存ファイルから切り出して別クラスにした。新規 `.cs` は
> Unity Editor に `.meta` を生成させてコミットする（手で作らない）。

**Interfaces:**
- Consumes: なし
- Produces: `BlockIconImagePhotographer.CaptureLogPrefix`（`internal const string` = `"[BlockIconCapture]"`。テストは `InternalsVisibleTo("Client.Tests")` 経由で参照する）。テストとログ検索の双方がこの1箇所を参照する。ログ行の形は次の7種（D1案A・5段）:
  - `[BlockIconCapture] start count:{総数}`
  - `[BlockIconCapture] {i}/{総数} {debugName} stage:setup`
  - `[BlockIconCapture] {i}/{総数} {debugName} stage:render`
  - `[BlockIconCapture] {i}/{総数} {debugName} stage:readback`
  - `[BlockIconCapture] {i}/{総数} {debugName} stage:captured`
  - `[BlockIconCapture] {i}/{総数} {debugName} stage:done elapsed:{ms}ms`
  - `[BlockIconCapture] completed count:{総数} elapsed:{秒}s`
  - `{i}` は1始まり。固着すると最後に出た行が固着箇所を名指しする。撮影1件は `stage:setup`〜`stage:done` の5行になる。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Block/BlockIconImagePhotographerLifetimeTest.cs` の
`TakeIconImages_撮影Cameraを一台ずつ生成する` の**後ろ**に、次のテストを追加する:

```csharp
        [UnityTest]
        public IEnumerator TakeIconImages_撮影の段階をログに残す()
        {
            var photographerObject = new GameObject($"{TestObjectPrefix}LogPhotographer");
            var photographer = photographerObject.AddComponent<BlockIconImagePhotographer>();
            var cameraPrefabObject = new GameObject($"{TestObjectPrefix}LogCamera");
            var cameraPrefab = cameraPrefabObject.AddComponent<Camera>();
            var targetPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetPrefab.name = $"{TestObjectPrefix}LogTarget";
            const string captureDebugName = "log-test";

            var cameraField = typeof(BlockIconImagePhotographer).GetField("cameraPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            cameraField.SetValue(photographer, cameraPrefab);

            // 固着時はメインスレッドごと止まるため、各段階へ入る直前のログだけが箇所の手掛かりになる
            // A freeze stops the main thread itself, so only the log emitted before each stage can locate it
            var captureLogs = new List<string>();
            void CollectLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Log && condition.StartsWith(BlockIconImagePhotographer.CaptureLogPrefix)) captureLogs.Add(condition);
            }

            Application.logMessageReceived += CollectLog;
            var captureTask = photographer.TakeIconImages(new List<(GameObject prefab, string debugName)>
            {
                (targetPrefab, captureDebugName),
            });
            yield return WaitForCompletion(captureTask);
            var textures = captureTask.GetAwaiter().GetResult();
            Application.logMessageReceived -= CollectLog;
            foreach (var texture in textures) Object.DestroyImmediate(texture);

            Assert.That(captureLogs.Count, Is.EqualTo(5), string.Join(" | ", captureLogs));
            Assert.That(captureLogs[0], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} start count:1"));
            Assert.That(captureLogs[1], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} 1/1 {captureDebugName} stage:render"));
            Assert.That(captureLogs[2], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} 1/1 {captureDebugName} stage:readback"));
            Assert.That(captureLogs[3], Does.StartWith($"{BlockIconImagePhotographer.CaptureLogPrefix} 1/1 {captureDebugName} stage:done elapsed:"));
            Assert.That(captureLogs[4], Does.StartWith($"{BlockIconImagePhotographer.CaptureLogPrefix} completed count:1 elapsed:"));
        }
```

このテストは既存ファイル冒頭の `using` に依存する。`System.Collections`・`System.Collections.Generic`・`System.Reflection`・`Client.Game.InGame.Block`・`Cysharp.Threading.Tasks`・`NUnit.Framework`・`UnityEngine`・`UnityEngine.TestTools` は既に書かれているため、**using の追加は不要**。

- [ ] **Step 2: テストを実行して失敗することを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockIconImagePhotographer" --timeout-seconds 600`

Expected: `TakeIconImages_撮影の段階をログに残す` がコンパイルエラー（`CaptureLogPrefix` が存在しない）で失敗する。既存2テストの結果は問わない。

- [ ] **Step 3: 撮影本体に計装を実装する**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockIconImagePhotographer.cs` を次のとおり変更する。

クラス先頭のフィールド宣言の直前に定数を足す:

```csharp
        // 固着時に箇所を名指しするための目印。ログ検索とテストが同じ1箇所を参照する（ADR 0063）
        // The marker that names a freeze site; log searches and tests share this single source (ADR 0063)
        internal const string CaptureLogPrefix = "[BlockIconCapture]";
```

`TakeIconImages` を次の形にする（`GetIcon` のシグネチャに段階ログ用の引数を足す）:

```csharp
        public async UniTask<List<Texture2D>> TakeIconImages(List<(GameObject prefab, string debugName)> targets)
        {
            var result = new List<Texture2D>();

            // 固着はメインスレッドごと止まるため、各段階へ入る直前に出したログだけが手掛かりになる
            // A freeze stops the main thread itself, so only logs emitted before each stage remain as evidence
            Debug.Log($"{CaptureLogPrefix} start count:{targets.Count}");
            var captureStartedAt = Time.realtimeSinceStartup;

            // 撮影資源を一件ずつ破棄し、対象数に依存する瞬間メモリ増加を防ぐ
            // Release capture resources one subject at a time to bound peak memory regardless of subject count
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                var progress = $"{index + 1}/{targets.Count} {target.debugName}";
                var instance = Instantiate(target.prefab, transform);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                result.Add(await GetIcon(instance, target.debugName, progress));
                if (Application.isPlaying)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            Debug.Log($"{CaptureLogPrefix} completed count:{targets.Count} elapsed:{Time.realtimeSinceStartup - captureStartedAt:F1}s");
            return result;

            #region Internal

            async UniTask<Texture2D> GetIcon(GameObject captureTarget, string captureDebugName, string captureProgress)
            {
```

> **反映後の実装はこの雛形と異なる（2026-09-18 レビュー反映）。** 実装の正本は
> `BlockIconImagePhotographer.cs` 本体。差分は3点: (a) D1案A で `stage:setup`（`Instantiate` の直前・所要計測の起点）と
> `stage:captured`（資源破棄の直前）を足して5段にした、(b) C6 で `progress` 文字列の組み立てを `GetIcon` 内へ畳み、
> 引数を `(prefab, debugName, index, count)` にした（`Instantiate` も `GetIcon` 内へ移り、`stage:setup` の直後に置かれる）、
> (c) C5 で `CaptureLogPrefix` を `internal` にした。

`GetIcon` の中身は、次の3箇所だけを足す（他の行は変更しない）:

`await UniTask.Yield(PlayerLoopTiming.Update);`（カメラ設定後）の**次**、`var renderTexture = new RenderTexture(...)` の**前**に、所要計測の開始と render 段階のログを置く:

```csharp
                // GPUへ渡す直前。ここで最後のログが止まっていればRender側の固着
                // Right before handing work to the GPU; a log stopping here means the freeze is on the Render side
                var iconStartedAt = Time.realtimeSinceStartup;
                Debug.Log($"{CaptureLogPrefix} {captureProgress} stage:render");
```

`blockImageCamera.targetTexture = null;` の**次**、`var texture = new Texture2D(...)` の**前**に、readback 段階のログを置く:

```csharp
                // 同期読み戻しの直前。ここで止まっていればReadPixels側の固着
                // Right before the synchronous readback; a log stopping here means the freeze is on the ReadPixels side
                Debug.Log($"{CaptureLogPrefix} {captureProgress} stage:readback");
```

`return texture;` の**直前**（破棄の分岐の後）に、完了ログを置く:

```csharp
                Debug.Log($"{CaptureLogPrefix} {captureProgress} stage:done elapsed:{(Time.realtimeSinceStartup - iconStartedAt) * 1000f:F0}ms");
                return texture;
```

- [ ] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`

Expected: ErrorCount 0。

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockIconImagePhotographer" --timeout-seconds 600`

Expected: 3テストすべて PASS（新規の `TakeIconImages_撮影の段階をログに残す` と、既存の `TakeIconImages_撮影用Cameraを残さない`・`TakeIconImages_撮影Cameraを一台ずつ生成する`。R7）。

- [ ] **Step 6: ファイル長と禁止事項を確認する**

Run: `wc -l moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockIconImagePhotographer.cs moorestech_client/Assets/Scripts/Client.Tests/Block/BlockIcon*.cs && grep -n "isBatchMode\|Environment.GetEnvironmentVariable\|Stopwatch" moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockIconImagePhotographer.cs`

Expected: 全ファイルの行数が200未満。grep は1件もヒットしない（R6。実時間の計測は `Time.realtimeSinceStartup` で行い `Stopwatch` を使わない）。

段階が5点そろっていることは、実装側を直接 grep して確認する:

Run: `grep -n "stage:" moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockIconImagePhotographer.cs`

Expected: `stage:setup` / `stage:render` / `stage:readback` / `stage:captured` / `stage:done` が上から順に1件ずつ出る（D1案A）。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockIconImagePhotographer.cs moorestech_client/Assets/Scripts/Client.Tests/Block/
git commit -m "feat(client): アイコン撮影に段階ログを入れ固着箇所を特定できるようにする

撮影の開始・各対象のsetup/render/readback/captured/done・完了をログに残す。
固着はメインスレッドごと止まるため、各段階へ入る直前のログだけが箇所の手掛かりになる。

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: ADR と裁定記録をコミットする

**Files:**
- Modify: なし（Task 1 以前に worktree へ配置済みの2ファイルを追跡下に入れる）
  - `docs/adr/0063-ci-hang-shard-timeout-and-icon-capture-instrumentation.md`
  - `.decisions/2026-09-18-CIのshardハングはstep-timeoutでfailure化し真因は計装で確定させる.md`
  - `docs/superpowers/plans/2026-09-18-ci-shard-hang-timeout-and-icon-capture-instrumentation.md`（本plan）

**Interfaces:**
- Consumes: なし
- Produces: なし

- [ ] **Step 1: 3ファイルが未追跡で存在することを確認する**

Run: `git status --short docs .decisions`

Expected: 上記3ファイルが `??` で並ぶ。

- [ ] **Step 2: コミットする**

```bash
git add docs/adr/0063-ci-hang-shard-timeout-and-icon-capture-instrumentation.md .decisions/ docs/superpowers/plans/2026-09-18-ci-shard-hang-timeout-and-icon-capture-instrumentation.md
git commit -m "docs: ADR 0063 CIのshardハングのfailure化とアイコン撮影の計装

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5（必須・省略不可）: moores-code-review で全ブランチレビューを実行する

**Files:**
- Modify: レビュー指摘に応じて Task 1〜4 の対象ファイル

**Interfaces:**
- Consumes: Task 1〜4 のコミット
- Produces: レビュー済みのブランチ

- [ ] **Step 1: 全ブランチレビューを実行する**

`moores-code-review` スキルを起動し、`origin/master..HEAD` の全差分をレビューする。ゴール文言や「差分が小さいから」を理由に省略しない。

- [ ] **Step 2: 指摘を反映し、反映後に検証をやり直す**

`.cs` を触った場合は `uloop compile --project-path ./moorestech_client` と Task 3 Step 5 のテストを**反映後のコードで再実行**する。`run_test.yml` を触った場合は Task 2 Step 3 の YAML 検査を再実行する。

- [ ] **Step 3: 残課題を1件ずつ起票する**

plan・レビューで出た「未検証」「未確認」「残差」を、issue tracker（Beads）へ1件ずつ起票する。少なくとも次の2件は必ず起票または既存issueへの追記を行う:
- 真因（アイコン撮影の固着）の特定と修正 → 既存 moorestech-7gsc に、本PRで計装を入れたこと・次のハングで読むログ行の形（`[BlockIconCapture]` で grep し、最後に出た `stage:setup` / `stage:render` / `stage:readback` / `stage:captured` / `stage:done` のどれかで固着区間を読む）を追記する。
- step timeout → failure → 自動再実行の連鎖が実際に発火したかの観測（次にハングが起きたとき）。

結論には issue 番号を列挙する。「残差は◯◯のみ」という要約で代えない。

- [ ] **Step 4: 全変更をコミットして push し、PR を作成する**

PR 本文には、既存 watchdog がこのflakeでは発火していなかった事実（直近14件が `skipped`）と、ADR 0063 へのリンクを含める。

- [ ] **Step 5: CI の結果を確認する**

このPR自身のCIで、正常runが40分を超えず通ることを確認する（ADR 0063 §3。ハング時の連鎖は実際の次の1件で観測する）。

---

## 判断記録（ADR）

- 設計の正本: `docs/adr/0063-ci-hang-shard-timeout-and-icon-capture-instrumentation.md`
- 裁定の蒸留: `.decisions/2026-09-18-CIのshardハングはstep-timeoutでfailure化し真因は計装で確定させる.md`

planning 中に生じた判断:

- **ログ行の形を `[BlockIconCapture] {i}/{N} {name} stage:{段階}` に固定した。** 出所: agent前提。固着時にログの最後の1行だけで「何個目・どの対象・どの段階」が読めることが計装の目的であり、grep しやすい固定接頭辞を `internal const` で1箇所に置いてテストと共有する（前例: `BlockIconImagePhotographer` 内の `CaptureRenderTexturePrefix` を既存テストが参照している形）。
- **段階を render / readback / done の3点にした。** 出所: agent前提。次の修正候補が `Camera.Render()` 側か同期読み戻し側かで分かれるため、この2つを分離できる粒度が要る。撮影対象は約200件のため、boot 1回あたり約600行のログが増える。この量はEditorログ・バグ報告バンドルに乗るが、接頭辞で絞れるため許容する。
  - **改訂（2026-09-18・レビュー D1案A・ユーザー裁定）: setup / render / readback / captured / done の5点にした。** 3点では「Instantiate〜Camera生成」と「破棄」の2区間が計装の空白として残り、そこで固着すると `stage:render` や `stage:readback` の行に畳まれて的外れな修正先（例: `AsyncGPUReadback` 化）へ誘導されうる。`stage:setup` が撮影1件の起点となり `stage:done` の `elapsed` はその1件の実所要と一致する。ログは1件5行（boot 1回あたり約1000行）。
- **経過時間を `Time.realtimeSinceStartup` で測る。** 出所: agent前提。AGENTS.md の「実時間APIを使わない」はサーバーのゲームロジックの経過時間測定に対する規約であり、ここはクライアントの診断ログ。`Stopwatch` を使わないという同規約の趣旨には沿う。
- **テストのログ収集を `Application.logMessageReceived` で行う（`LogAssert.Expect` を使わない）。** 出所: agent前提。順序と件数まで固定したいため、収集してから並びを検証する形にした。
- **改訂（2026-09-18・post-check C-1 の是正）: shard を「テスト実行 step（`continue-on-error`）＋ infra 名の判定 step ＋ code 名の失敗 step」の3本に分けた。** `ci-auto-rerun.cjs` は INFRA を CODE より先に評価するため、テスト実行 step 自体を `Unity shard runner - <shard>` へ改名すると通常のテスト失敗まで infra 判定になり、赤 PR が毎回1回余分に再実行される。使い捨て workflow `tmp-hang-detector-probe.yml` の実測（run 35270032962）で、打ち切り時は判定 step が `failure`（elapsed=73s）、通常の失敗時は判定 step が `success`（elapsed=0s）で code 名の step だけが `failure` になることを確認した。
- **step の `timeout-minutes` へ `${{ env.X }}` を素で書くと無視される。** 実測 run 35269448343 で、素の env 参照では 1 分指定の step が `sleep 300` を最後まで走らせた（literal `1` と `fromJSON(env.X)` はどちらも約73秒で打ち切られた）。値は文字列として渡るため、`fromJSON()` で数値化して渡す。
- **`foreach` を添字 `for` に変えた。** 出所: agent前提。進捗の `{i}/{N}` を出すために添字が要る。挙動は変わらない。
- **step timeout の挙動を実測してから本適用する（Task 1）。** 出所: agent前提。公式ドキュメントは step timeout 超過時の結論を明記しておらず、「step の `timeout-minutes` は無視される」と書く二次情報もある。planの全体がこの外部挙動に乗るため、伝聞のまま所与にしない（writing-plans Self-Review §6）。実測結果はこの行の下へ追記する。
  - 実測結果: run `failure` / job `probe` `failure` / step `Sleep past the step timeout` `failure`（run id 35262375455・2026-09-17T19:01Z・`actions/github-script@v7` に `timeout-minutes: 1`）。後続の `if: always()` step は `success` で走った。よって Task 2 は `timeout-minutes: 40` を足すだけの形を採る（`continue-on-error` の代替形は不要）。
  - なお `workflow_dispatch` はデフォルトブランチに存在しない workflow を起動できず404になるため、検証workflowは自ブランチへの push 起動に変えて実測した（plan の Step 3 のコマンドからの逸脱はこの点のみ）。
