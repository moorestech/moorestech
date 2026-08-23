# Unity Test CI Sharding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** PRのUnity EditModeテスト2152件を削らず、実測31分02秒のCI wall timeを、重量クラスの8並列shardと最新Libraryキャッシュで大幅に短縮する。

**Architecture:** `.github/scripts/unity-test-shard-filter.sh` がshard名からassembly・正規表現filter・WebUI要否を一意に解決し、`.github/workflows/run_test.yml` のmatrix jobが8 shardを並列実行する。専用shardに列挙されない既存・将来のテストはClient/Serverそれぞれのremainder shardへ必ず流し、検査漏れを作らない。`.github/workflows/cache-warm.yml` はmaster更新時にclient Libraryを焼き、成功後に旧client cacheだけを削除して10GB枠を維持する。

**Tech Stack:** GitHub Actions / Bash / game-ci unity-test-runner@v4 / actions/cache@v4 / Unity Test Framework 1.6.0

## Requirements

- **R1.** ClientとServerの全EditModeテストを同一ジョブで直列実行しない。**受け入れ基準:** `run_test.yml` が8要素のmatrixを持ち、`Client.Tests` 4 shardと`Server.Tests` 4 shardが`fail-fast: false`で並列実行される。
- **R2.** 2026-08-23のPR #1256 XMLで支配的だったClient EditModeInPlaying群を3 shardへ均等化する。**受け入れ基準:** 専用3 shardの実測test-case duration合計が188.248秒・212.767秒・204.477秒で、残余Clientテストはclient-remainderへ流れる。
- **R3.** 同XMLで約8分だったServer MapGeneration重量fixtureを3 shardへ均等化する。**受け入れ基準:** 専用3 shardの実測test-case duration合計が152.594秒・153.247秒・156.512秒で、未列挙のMapGenerationおよび全Server残余はserver-remainderへ流れる。
- **R4.** テスト範囲を削らない。**受け入れ基準:** 専用filterは正の完全修飾クラス名regex、remainder filterはその完全な和集合の否定としてスクリプト内で自動合成される。現行CIに含まれる`Unity.Addressables.DocExampleCode.Editor.Tests`の1件はserver-remainderへ含め、すべてのshardに`!IgnoreCI` category除外が適用される。
- **R5.** 将来fixtureを追加してもCIから無音で漏れない。**受け入れ基準:** 新しいクラス名は専用正規表現に一致しない限りremainderへ含まれ、専用3群とremainderの間に除外される名前空間丸ごとの穴がない。
- **R6.** shard失敗を既存の必須チェック名で集約する。**受け入れ基準:** 全matrix jobに依存する集約jobの表示名が既存と同じ`EditMode Test (Client + Server)`で、1 shardでも失敗・cancelなら集約jobが失敗する。
- **R7.** shardごとの結果を衝突させない。**受け入れ基準:** GameCI `checkName`とupload artifact名がshard名を含み、8件すべて一意である。
- **R8.** CI固定費を削る。**受け入れ基準:** test checkoutは`fetch-depth: 1`、Server shardはWebUIセットアップをskipし、GameCI結果投稿は1時間App tokenでなくjob寿命の`github.token`を使う。
- **R9.** master更新後のPRが古いLibraryを長期間使わない。**受け入れ基準:** `cache-warm.yml`はUnity関連pathのmaster pushでclient warmだけを起動し、定期・手動時だけ既存server platform cacheもwarmする。
- **R10.** GitHub Actions cache 10GB枠を守る。**受け入れ基準:** client warm成功・保存成功後、現在key以外の`Library_Test_client-` cacheを削除し、server cacheを削除しない。
- **R11.** 実環境の短縮を検証する。**受け入れ基準:** PR #1256の更新後CIが全greenで完走し、変更前31分02秒に対するwall timeと全shard test件数合計を記録する。

**やらないこと:** テストのskip・IgnoreCI追加、ゲーム挙動変更、Unityアセット変更、self-hosted runner移行、platform compile workflowのshard変更は行わない。

## Global Constraints

- Unity versionは既存どおり`6000.3.8f1`、GameCIは`unity-test-runner@v4`を維持する。
- YAMLとBashの主要コメントは日本語・英語2行セットにする。
- `.moorestech-external-revisions.json`のユーザー変更には触れず、commitへ含めない。
- キャッシュ保存者はmasterのcache-warmだけとし、PR shardはrestoreのみ行う。
- `.cs`・Prefab・Scene・ScriptableObject・`.meta`は変更しないためUnityコンパイルとunityプレイ録画テストは不要。検証対象はworkflowの静的構造と実GitHub Actions runである。

---

### Task 1: 決定的で漏れのないshard filter resolver

**Files:**
- Create: `.github/scripts/unity-test-shard-filter.sh`

**Interfaces:**
- Consumes: 位置引数1個のshard名、環境変数`GITHUB_OUTPUT`
- Produces: `assembly_names`、`test_filter`、`needs_webui`のGitHub step output

- [x] **Step 1:** Client/Serverの実測重量クラスを各3群の完全修飾名として定義し、正filterと和集合否定remainder filterを同じ変数から生成する。
- [x] **Step 2:** 未知shard、引数不足、`GITHUB_OUTPUT`未設定を非0終了にし、8 shardすべての出力を一時outputへ解決するshell検査で成功を確認する。
- [x] **Step 3:** 正filter 6本のクラス集合が重複せず、各remainderが同assemblyの正集合だけを否定することを検査する。
- [x] **Step 4:** `git add .github/scripts/unity-test-shard-filter.sh docs/superpowers/plans/2026-08-24-unity-test-ci-sharding.md && git commit -m "perf(ci): Unityテストのshard filterを定義"`でcommitする。

### Task 2: Unity Testを8並列matrixへ変更

**Files:**
- Modify: `.github/workflows/run_test.yml`

**Interfaces:**
- Consumes: Task 1のresolver outputs
- Produces: 8個の`Unity Test - <shard>` check、8個の`Test results - <shard>` artifact、集約check`EditMode Test (Client + Server)`

- [x] **Step 1:** 既存test jobの共通setupをmatrix jobへ移し、8 shardを`fail-fast: false`で並列実行する。
- [x] **Step 2:** resolverの`assembly_names`と`test_filter`を`-assemblyNames`・`-testFilter`へ渡し、全shardに`-testCategory "!IgnoreCI"`を渡す。
- [x] **Step 3:** Client shardだけWebUI toolchainを準備し、全shardでshallow checkout・job token・一意check/artifact名を使う。
- [x] **Step 4:** `if: always()`の集約jobでmatrix resultがsuccessのときだけ成功させ、既存check表示名を維持する。
- [x] **Step 5:** Ruby YAML parseと構造assertionでmatrix 8件、一意名、resolver利用、`fetch-depth: 0`消滅を確認する。
- [x] **Step 6:** `git add .github/workflows/run_test.yml && git commit -m "perf(ci): Unity EditModeテストを8並列化"`でcommitする。

### Task 3: master pushで最新client Libraryをwarm

**Files:**
- Modify: `.github/workflows/cache-warm.yml`

**Interfaces:**
- Consumes: masterへの`moorestech_client/**`・`moorestech_server/**`・関連workflow変更push
- Produces: 最新`Library_Test_client-<master SHA>` cache、定期・手動時の既存server platform cache

- [x] **Step 1:** Unity関連pathのmaster push triggerを追加し、push時はclient warmだけ実行する。
- [x] **Step 2:** client warm/save成功後にGitHub APIで同prefixの旧client cache IDだけを削除し、現在keyとserver prefixを保持する。
- [x] **Step 3:** Ruby YAML parseと構造assertionでpush paths、server job条件、cleanup success条件、`actions: write` permissionを確認する。
- [x] **Step 4:** `git add .github/workflows/cache-warm.yml && git commit -m "perf(ci): master更新時にUnity test cacheをwarm"`でcommitする。

### Task 4: 実CI検証と全ブランチレビュー

**Files:**
- Modify: `docs/superpowers/plans/2026-08-24-unity-test-ci-sharding.md`（チェックボックスと実測記録だけ）

**Interfaces:**
- Consumes: GitHub Actions run artifacts
- Produces: 全shard件数合計、wall time、変更前比較

- [ ] **Step 1:** shell syntax、全workflow YAML parse、resolver 8 shard、workflow構造assertion、git diff whitespace checkを実行する。
- [ ] **Step 2:** branchをpushし、PR #1256の全8 shardと集約checkの完走を待つ。失敗時はartifact/logから前方修正する。
- [ ] **Step 3:** shard artifact XMLの`total`合計が変更前2152件で、test-case `fullname`の多重集合が変更前と一致することを確認する（現行結果には同一fullnameが2組あるため単純集合比較は不可）。
- [ ] **Step 4:** test job開始から集約完了までのwall timeを記録し、変更前31分02秒から短縮したことを確認する。
- [ ] **Step 5:** 必ずmoores-code-reviewスキルで全ブランチレビューを実行し、確定指摘を修正・再検証する。
- [ ] **Step 6:** セッション終了可能状態にする。既存PR #1256を更新するためpr-createスキルで差分・PR状態を確認し、master conflictがあれば通常mergeで解消、全作業commit・push済みかつPRがmerge可能な状態で終える。

## 配置と前例

| # | 項目 | 配置先 | 機構・前例照合 |
|---|---|---|---|
| 1 | shard filter resolver | `.github/scripts/` | 既存`ci-auto-rerun.cjs`等と同じCI補助スクリプト層。ゲームassemblyへCI語彙を持ち込まない。 |
| 2 | parallel matrix | `.github/workflows/run_test.yml` | 既存`platform-compile.yml`のmatrix + `fail-fast: false`前例に合わせる。 |
| 3 | cache lifecycle | `.github/workflows/cache-warm.yml` | ADR 0028のmaster-only writerを維持し、PR workflowはrestore-onlyのままにする。 |

データフロー: `PR更新 → shard resolver → 8 matrix jobs → 一意XML/artifact → 集約check → merge可否`。resolverはworkflowの設定値を書くだけで、テストランナーやゲームコードへ逆流しない。

## 判断記録（ADR）

- **agent前提（拒否権つき）:** 2分割は固定費込み約20分なので、短さ最優先というユーザー要求に合わせ8 shardを選ぶ。Actions消費量よりwall timeを優先する。
- **agent前提（拒否権つき）:** namespace丸ごとの否定は将来テストを漏らすため禁止し、remainderは専用クラス集合の否定を同じ変数から合成する。
- **agent前提（拒否権つき）:** master pushでplatform compile cacheまで毎回焼くと10GB枠と実行量を圧迫するため、push時はclient test cacheだけ、schedule/manualでは従来3系統を維持する。
- **ユーザー裁定（2026-08-24）:** grill不要、計測を先に行い、短ければ短いほど良い。PR #1256実測後にCI側施策も実施する。
- unityプレイ録画テストはゲームランタイム挙動を変更しないため対象外。実GitHub Actionsでの全件同一性とwall timeが正本の検証となる。
- user-simulator reviewは指定モデルFableをこの実行環境で選べないため、Requirements coverage・配置表・将来fixtureのremainder包含をメインagentが自己レビューした。要裁定分岐はなく、短さ優先のユーザー裁定内で実装できる。
