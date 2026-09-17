# CIのshardハングはstep timeoutでfailure化し、真因は計装で確定させる

2026-09-18 ユーザー裁定（CI flake moorestech-7gsc の設計インタビュー）。

決定: `run_test.yml` のテスト実行 step に `timeout-minutes: 40` を付ける（全shard一律）。あわせて `BlockIconImagePhotographer` のアイコン撮影に進捗ログを入れ、真因は次のハング1件で箇所を確定させてから直す。

理由: job の `timeout-minutes` による打ち切りは run の結論が `cancelled` になり、既存の自動再実行 watchdog（発火条件は `failure` / `timed_out`）を素通りする。step の timeout は `failure` になるため、判定ロジックを増やさずに既存の再実行がそのまま乗り、手動キャンセルとの区別も不要になる。40分は実測最長28分（server-remainder）に対する約40%の余裕。真因はメインスレッドの固着で、固着がネイティブ呼出し内ならC#側のタイムアウトは効かず、撮影内にログが1行も無い現状では推測修正になるため、まず計装で確定させる。

棄却案:
- watchdog 側で `cancelled` も再実行対象にする — 人の手動キャンセルとの区別が新たに要る。step timeout なら区別自体が不要。
- step timeout と watchdog 改修の両方を入れる — 手動キャンセルを再実行するリスクを抱える。
- shard 別に実測ベースの timeout 値を置く — テスト追加のたびに値の保守が要る。
- 一律30分 — server-remainder の実測28分に対し余裕が2分しかなく、遅い日に本物のテストを誤って打ち切る。
- 同期読み戻しを `AsyncGPUReadback` へ置き換える — 固着が `Camera.Render()` 側なら空振りする確定前の推測修正。
- CIでは撮影を省く — 環境分岐をプロダクションコードに持ち込み、CIが実のboot経路を通らなくなる。
- 計装と `AsyncGPUReadback` の両方 — どちらが効いたか分かる利点より、推測修正を混ぜる害が勝る。
- boot パイプライン全体への計装 / 別スレッドの監視役の常駐 — 変更範囲が広がる / テスト専用の仕掛けを常駐させる。
- マージ前に反復CI検証してハングを引き当てる / 意図的にハングを作って連鎖を確認する — Actions の消費と待ち時間が大きい / 検証用コミットの投入と撤去の工程が増える。効果確認は通常のCI（正常runが40分以内に通ること）で足りる。

出所: ユーザー裁定 2026-09-18 原文「じゃあこのPRとは独立してCIを直して」→ 選択「両方やる（1本のPRで）」「stepにtimeoutを付けてfailureにする」「全shard一律40分」「計装を入れて箇所を確定させる」「アイコン撮影の中だけ」

リンク:
- ADR: `docs/adr/0063-ci-hang-shard-timeout-and-icon-capture-instrumentation.md`
- タスク台帳: moorestech-7gsc
