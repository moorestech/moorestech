# ADR 0063: CIのshardハングはstep timeoutでfailure化して既存の自動再実行に乗せ、真因はアイコン撮影の計装で特定する

- 状態: 採用
- 日付: 2026-09-18
- 文脈: タスク台帳 moorestech-7gsc（2026-09-08起票・ブランチ非依存で再発中のCI flake）
- 裁定者: ユーザー（2026-09-18 の設計インタビュー）

## 文脈

`.github/workflows/run_test.yml` の `unity_test_shard`（9 shard）のうち、client 系 shard が起動boot中に
Unity Editor のメインスレッドごと固着し、job の `timeout-minutes: 75` で打ち切られる事象が再発している。

実測した事実（2026-09-18 時点）:

- 停止点は常に同じ。`[InitializeScenePipeline] parallel mod asset load completed`（`ModAssetLoader.cs:63`）の直後、
  `ServerCommunicator.cs:51` の `Debug.Log("サーバーに接続しました")` の手前で全ログが止まる。例外は出ない。
- サーバー側は同じログに「接続確立」を出している（accept 済み）。接続待ちには3秒の `.Timeout()` が掛かっており、
  PlayerLoop が回っていれば無限待ちにならない。よってメインスレッドの固着と判断できる。
- 常に shard 内の最初のテストで起きる（ハングしたログには当該行が1回しか出ない。正常shardでは6回出る）。
- ブランチ非依存。スキルファイルしか変えていないPRでも再発する。直近40 runのうち12 runで発生（約30%）。
- 正常時の所要は client-play 系が9〜11分、最長は server-remainder の28分。
- **既存の自動再実行 watchdog（`ci-auto-rerun.yml` / `ci-auto-rerun.cjs`）はこの事象では発火しない。**
  job が `timeout-minutes` で打ち切られると run の結論は `cancelled` になるが、watchdog の発火条件は
  `failure` と `timed_out` だけである。直近の該当14件はすべて `skipped` だった。

停止点の直後に実行されるのは `ModAssetIconLoader` → `BlockIconImagePhotographer` のアイコン撮影で、
ブロック1個ごとに `Camera.Render()` と `Texture2D.ReadPixels()`（同期GPU読み戻し）を回す。
GPUの無いLinuxランナーでの固着候補として最有力だが、**撮影内にログが1行も無いため現時点では推定に留まる**。

## 裁定

### 1. CIの赤は workflow 側で止める（step timeout で failure 化する）

`unity_test_shard` のテスト実行 step に `timeout-minutes: 40` を付ける。
job の `timeout-minutes` による打ち切りは `cancelled` になり既存 watchdog を素通りするが、
step の timeout は job を `failure` にするため、**判定ロジックを増やさずに既存の自動再実行がそのまま発火する**。

- 値は全 shard 一律40分。実測最長28分に対し約40%の余裕を持たせる。
- 「step timeout は job を failure にする」は公式ドキュメントに明記が無いため、実装時に使い捨ての workflow で実測してから
  本適用する。実測で `cancelled` になる、または step timeout が効かない場合は、`continue-on-error: true` と
  「outcome が success でなければ失敗させる step」の組で同じ結果（job を failure にする）を作る。
- watchdog 側（`ci-auto-rerun.cjs`）は変更しない。`cancelled` を拾う改修は、人の手動キャンセルとの区別が
  新たに必要になるため採らない。
- job の `timeout-minutes: 75` は残す（step timeout が効かなかった場合の最後の網）。

出所: ユーザー裁定 2026-09-18 原文「じゃあこのPRとは独立してCIを直して」→ 選択「両方やる（1本のPRで）」
および 選択「stepにtimeoutを付けてfailureにする」「全shard一律40分」

#### 棄却した案

- **watchdog 側で `cancelled` も拾う**: PR更新による自動キャンセルは既存の鮮度チェックで弾けるが、
  人の手動キャンセルと timeout 由来の区別を新たに実装する必要がある。step timeout なら区別自体が不要になる。
- **両方入れる（step timeout + watchdog の cancelled 対応）**: 手動キャンセルを再実行するリスクを抱える。
- **shard 別に実測ベースの値を置く**: テスト追加のたびに値の保守が要る。一律40分で足りる。
- **一律30分**: server-remainder の28分に対し余裕が2分しかなく、遅い日に本物のテストを誤って打ち切る。

### 2. 真因は推定のまま直さない。アイコン撮影に計装を入れて次の1件で確定させる

`BlockIconImagePhotographer` の撮影に進捗ログを入れ、次にハングしたときに
「何個目の・どの撮影対象の・どの段階で」止まったかがログから読めるようにする。

- 推測でプロダクションコードを書き換えない。CIの赤は §1 で既に止まっているため、確定を待つ余裕がある。
- 計装の範囲はアイコン撮影の中だけに限る。boot パイプライン全体への進捗ログ追加や、
  別スレッドの監視役の常駐は採らない。

出所: ユーザー裁定 2026-09-18 選択「計装を入れて箇所を確定させる」「アイコン撮影の中だけ」

#### 棄却した案

- **同期読み戻しを `AsyncGPUReadback` へ置き換える**: 有力候補ではあるが、固着が `Camera.Render()` 側なら空振りする。
  確定前の推測修正になる。
- **CIでは撮影を省く**: 確実に止まるが、環境分岐をプロダクションコードに持ち込み、CIが実のboot経路を通らなくなる
  （AGENTS.md「デバッグ/テスト専用をプロダクションに残さない」と衝突）。
- **計装と `AsyncGPUReadback` の両方**: どちらが効いたか分かるという利点より、推測修正を混ぜる害が勝る。
- **別スレッドの監視役を置いて固着時に状態を吐く**: テスト環境専用の仕掛けを常駐させることになる。

### 3. 効果の確認は通常のCIで行う

このPR自身のCIで「正常runが40分を超えず通る」ことだけ確かめてマージする。
ハング時の連鎖（step timeout → failure → 自動再実行）は、実際の次の1件で観測する。

- ハングは約30%の間欠事象で、マージ前に引き当てるには CI を何度も回す必要があり、消費と待ち時間が大きい。
- 目的は人手の rerun を消すことであり、外れたとしても損失は現状（人手 rerun）に戻るだけで小さい。

出所: ユーザー裁定 2026-09-18 選択「通常のCIで確認してマージ」

#### 棄却した案

- **マージ前に反復CI検証する**: 自動復旧の発火を確実に見られるが、Actions の消費と待ち時間が大きい。
- **意図的にハングを作って確かめる**: 一度で連鎖を確認できるが、検証用コミットの投入と撤去という工程が増える。

## 帰結

- ハングしても最大40分で failure になり、既存 watchdog が失敗ジョブだけを自動で再実行する（`run_attempt` 3回で打ち止め）。
  人手の rerun 運用は不要になる。
- 真因が消えるわけではない。moorestech-7gsc は開いたまま残し、次のハングの計装ログで箇所を確定させてから修正する。
- 撮影が真犯人でなかった場合、計装ログは「撮影は完走していた」という否定の証拠になり、次の探索先が絞れる。
