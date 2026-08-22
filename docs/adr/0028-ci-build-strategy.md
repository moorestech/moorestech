# PRビルドを日次へ移し、PF別コンパイル検査と無人修復パイプラインで置き換える

## Context

`Unity Build`（`.github/workflows/build.yml`）は `pull_request` で発火し、PRごとに2〜4時間かかっている。
実測（run 32444438082・2h24m）の内訳は Server Win 23m / Server Mac 14m / Client Win 51m / Client Mac 57m で、
作業時間の合計145分が `max-parallel: 1` と `needs: server-build` によって完全に直列化されている。
Client は Server のartifactを一切ダウンロードしておらず、この依存に実体は無い。

キャッシュは一度も効いていない。ログに `Cache not found for input keys: Server_Library_` が出る。
`build.yml` / `run_test.yml` のトリガーが `pull_request` と `workflow_dispatch` だけであるため、
Actionsのキャッシュスコープ（自分のPR refとベースブランチのみ参照可）上、PRが書いたキャッシュは他のPRから参照できず、
masterのキャッシュは存在しない。加えて `active_caches_size_in_bytes` は 10.45GB で上限10GBを超え、常時eviction中。

Windowsジョブの失敗率は12〜14%だが、直近の失敗4件は4件とも
`failed to connect to the docker API at npipe:////./pipe/docker_engine` で、実体のあるビルド失敗はゼロ。
`game-ci/unity-builder` はWindowsのみDockerコンテナ（`unityci/editor:windows-6000.3.8f1-windows-il2cpp-3`）で動くため、
runnerのDockerデーモン起動前に叩いて開始2秒で落ちている。macOSはネイティブ実行のため失敗0件。
失敗のたびに `ci-auto-rerun` が2時間コースをもう1周している。

macOSランナーの待ちも重く、run 32470221390 は起動まで1h46m空転した。

masterへのマージは1日20〜29件（8/18: 29, 8/19: 19, 8/20: 28）。

PF条件コンパイルの実測: `UNITY_STANDALONE_(WIN|OSX|LINUX)` を含む111ファイルのうち108は
`PersonalAssets/moorestech-client-private` 内のSteamworks.NETで、CIはこのrepoをcheckoutしない（[[2026-08-19-有料アセット依存テストはIgnoreCIでCIから外す]]）。
自前コードは3ファイルのみで、`Client.Tests/.../OsInputSpoof.cs` と `Tests/Watchdog/OsDefaultOpener.cs` は
テストアセンブリでありプレイヤービルドに含まれない。製品コードのPF分岐は `Server.Boot/ServerDirectory.cs` の1本だけ。
またEditMode Testはubuntuランナー上で動くため、Linux defineでのコンパイルは既に毎PR通っている。

Linuxは、クライアントがCEFネイティブランタイム不在で原理的にビルド不可（[[2026-08-02-Linuxビルド入口は意図的失敗として残す]]）、
サーバー（dedicated）は 2026-04-16 の `f04d083c0` でmatrix行がコメントアウトされたまま4ヶ月放置で、
容量が真因である証跡は一次資料に存在しない。`build.yml:141` の「ランナーの容量が足りないため」というコメントは実態と食い違う。

## Decision

ビルドの目的を「プレイヤービルド固有の破壊検知」に限定し、PRからビルドを外して日次へ移す。
空く検知の穴のうち、コンパイル面はPF別コンパイル検査で、時間面は無人修復パイプラインで埋める。

### PR CI（毎PR）

- `Unity Build` は `pull_request` から外し、**「ビルド検証」ラベル付与時のみ**発火させる（`pull_request: types: [labeled]`）
- **PF別コンパイル検査を新設**する。ubuntu-latest のUnityコンテナを `-buildTarget` 切り替えで起動し、
  `moorestech_server` について `StandaloneWindows64` / `StandaloneOSX` のスクリプトコンパイル可否だけを検査する。
  プレイヤービルドは行わない。`StandaloneLinux64` は既存のEditMode Testが実質カバー済みのため追加しない
- 既存の `Web UI Test` / `EditMode Test` / `Check invalid characters` / `Mooresmaster Test` は維持する

**PF別コンパイル検査の検出範囲（2026-08-22 実測）**

PR #1220 で実際に構文エラーを注入して測った結果、この検査はEditorとしてのコンパイルであるため次の非対称性がある。

- 検出できる: `#if UNITY_STANDALONE_OSX` のように、先行する `#if UNITY_EDITOR` に隠れていないPF分岐。
  `OsDefaultOpener.cs` のOSXブランチへ構文エラーを入れると `Compile - StandaloneOSX` だけが `error CS1525` で落ち、
  `Compile - StandaloneWindows64` は成功した
- 検出できない: `#if UNITY_EDITOR` → `#elif UNITY_STANDALONE_OSX` の形で、Editor分岐に先取りされたブランチ。
  Editor実行時は常に先頭の `UNITY_EDITOR` が真になるため、後続ブランチはコンパイル対象にならない。
  `ServerDirectory.cs` のOSXブランチへ構文エラーを入れても両ジョブとも成功した
- したがって「Editor分岐に隠れたPF分岐」の担保は、依然として日次のプレイヤービルドが担う

### 日次（04:00 JST・打ち切り09:00 JST）

- Client Win/Mac + Server Win/Mac の4ジョブを**並列**・キャッシュ無しで実行する。Linux は戻さない
- 失敗は `ci-auto-rerun` で再実行し、**再実行後も赤いときだけ**専用ラベル付きのGitHub Issueを起票する。
  起票時に「前回グリーンのSHA」「以降にマージされたPR一覧」「失敗ジョブとエラーログ抜粋」を本文へ機械的に埋め込む
- 既存のPRラベル駆動ステートマシン（`~/hermes-agent/data/services/pr-review/poller.py`）をIssue起点に拡張し、
  cmux上で `claude` を起動して調査→**前方修正**→PR作成まで無人で進める。git bisectによる犯人特定は行わない
- 修復エージェントは自分のPRに「ビルド検証」ラベルを付けて緑を確認してから人のレビューへ渡す。**自動マージはしない**
- 09:00時点で未完了なら、「どこまで調べたか・何を直しかけたか・残作業」をIssueへコメントしてからワークスペースを閉じる

### キャッシュ（10GB枠）

- EditMode Test用 client-Linux（3.68GB）と PF別コンパイル用 server-Win/Mac（1.17GB×2）の計約6GBのみキャッシュする
- **masterでキャッシュを焼く**ジョブを設ける。これが無い限りPR側は永遠にコールドのままになる
- 日次フルビルドはキャッシュを使わない

出所: ユーザー裁定 2026-08-21 選択「プレイヤービルド固有の破壊検知だけ」[[2026-08-21-PRビルドの目的はプレイヤービルド固有の破壊検知に限定する]]／
選択「日次のみ」[[2026-08-21-プレイヤービルドの検知粒度は日次のみとする]]／
原文「issueをたて、専用ラベルを付け、いまのPRのPRレビューや裁定と同じ仕組みでエージェントを発火し、修正してPRを作る。この一連の作業を深夜におこなう」
→ 選択「ビルドも修復も深夜」[[2026-08-21-日次ビルド失敗は専用ラベルIssueから無人修復パイプラインを深夜に回す]]／
選択「専用ラベルで発火」[[2026-08-21-ブランチ単位のビルド検証は専用ラベルで発火する]]／
選択「またLinuxは外しておく」[[2026-08-21-日次ビルドの対象PFにLinuxは戻さない]]／
原文「各PRで各PFのビルド前のコンパイルチェックだけやりたいけどそれは出来る？」→ 選択「入れる（ubuntuでWin/Macの2ターゲット）」
[[2026-08-21-PR CIにPF別コンパイルチェックを入れる]]／選択「serverプロジェクトのみ」[[2026-08-21-PF別コンパイルチェックはserverプロジェクトのみを対象にする]]／
選択「PR側に全振り、日次はコールド」[[2026-08-21-キャッシュ枠はPR側に全振りし日次ビルドはコールドで回す]]／
選択「根治し、再実行でも赤いときだけ起票」[[2026-08-21-インフラ起因の失敗はフレークを根治し再実行後も赤いときだけ起票する]]／
選択「前方修正のみ、材料はIssueに埋め込む」[[2026-08-21-修復エージェントは前方修正のみで犯人特定はしない]]／
ユーザー裁定 2026-08-22 自由記述「4時開始、9時うちきり」→ 選択「途中経過をIssueに残して停止」
[[2026-08-22-無人修復の深夜枠は4時開始9時打ち切りとする]]

## Considered Options

- **日次のみ＋無人修復（採択）**: PRの回転速度を最優先し、検知の遅れは無人修復ループで吸収する
- **マージごと＋日次フルの2層（棄却）**: 犯人が1PRに確定しbisect不要になるが、日20〜29マージ分のrunと、古いrunをキャンセルする前提の複雑さを負う
- **マージごとのみ（棄却）**: 層は1つで済むが「全PFが実際に通った日」が保証されない日が出る
- **PR単位を維持し対象をWindows 1PFへ削る（棄却）**: 即時フィードバックは最強だが、PRごとの待ち時間が残り続ける
- **PF別コンパイルを入れない（棄却）**: 実入りは製品コード1ファイル分だが、日次の間のPF固有コンパイルエラーを24時間見逃す
- **PF別コンパイルでclientも対象にする（棄却）**: client側のPF分岐はテストアセンブリのみで、キャッシュ枠10GBに収まらない
- **Linux dedicated serverを復活させる（棄却）**: 「全PFが通る」が初めて事実になるが、直らない場合に赤い日次が常態化し無人修復を毎晩空振りさせる
- **Mac miniにself-hosted runnerを立てる（棄却）**: キャッシュ枠から解放されるが、CIが自宅サーバー常駐に依存し、既存のUnity Editor群・cmuxワークスペースと資源を争う
- **外部S3互換ストレージをキャッシュバックエンドにする（棄却）**: 容量制限は外れるが転送量と鍵管理が増え、CIが外部サービス依存になる
- **自動bisectで犯人を特定する（棄却）**: 犯人PRが一意に定まるが、コールドビルド約1時間×log2(29)≈5回で深夜枠を使い切る

## Consequences

- PRのフィードバックからプレイヤービルドの検証が消える。IL2CPP・シェーダ・アセット処理・CEF同梱の破壊は、
  「ビルド検証」ラベルを明示的に付けない限り、最短でも翌04:00までmasterに載ったまま残る
- 日次が赤くなったとき、容疑者は20〜29PRになる。前方修正が効かない種類の破壊では人の介入が要る
- 修復エージェントが使う検証ビルドもキャッシュ無しのため、1回あたり1時間弱かかる。
  5時間の深夜予算は平常時（3〜4時間）に収まるが、ランナー待ちが重なった最悪ケース（約6時間）は打ち切りに当たる
- `ci-auto-rerun` の発火条件が `github.event.workflow_run.event == 'pull_request'` に絞られているため、
  日次（scheduleイベント）に広げない限り自動再実行が効かなくなる

## 保留

`fetch-depth: 0` の廃止（`.git` が4.2GB、checkoutに2〜5分×5ジョブ）は、`game-ci` の `versioning: Semantic` が
git履歴からバージョンを生成している（ログの `Generated version 0.0.13714`）ため単独では実施できない。
`versioning` を `none` に落としてよいかの裁定とセットで別途扱う。
