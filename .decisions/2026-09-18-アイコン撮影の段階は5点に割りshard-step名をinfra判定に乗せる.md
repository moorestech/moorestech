# アイコン撮影の段階は5点に割り、shard step名をinfra判定に乗せる

2026-09-18 ユーザー裁定（moores-code-review の設計判断 D1・D2・D3 への回答）。

決定: (1) アイコン撮影の段階ログを `setup / render / readback / captured / done` の5点にし、セットアップ段と資源破棄段も窓として埋める。(2) `run_test.yml` のテスト実行 step 名から `ci-auto-rerun.cjs` の CODE_KEYWORDS 該当語（'test'）を外し INFRA_KEYWORDS 該当語を含む名前にして、2回目の失敗も自動再実行に乗せる（`ci-auto-rerun` 自体は無改修）。(3) 段名トークン（`start` / `stage:*` / `completed`）は定数集約も enum 化もせず現状の裸の文字列のままにする。

理由: (1) 3段のままだと、`Instantiate`・Renderer走査・Camera生成からなるセットアップ段と資源破棄段で固着したときに段名が出ず、「何個目のどの対象のどの段階」というこの計装の唯一のゴールが、固着箇所によっては満たされない。窓を全部埋めれば次の1件で必ず箇所が確定する。(2) step timeout は step を failure にするが、step 名 `Run Unity Test - ...` が CODE_KEYWORDS の 'test' に一致するため watchdog は2回目を「コード起因の失敗」と判定して再実行を止める。実測で自動再実行は初回の1回だけであり、ADR 0063 の「人手の rerun 運用は不要」は成り立っていなかった。step 名を変えるだけなら非目標の `ci-auto-rerun` 改修に触れず、check 表示名（checkName 入力）も変わらない。(3) 段名まで production とテストで共有定数にすると、実装とテストが同じバグを共有して検証力が消える。

棄却案:
- 段階を4段（setup だけ追加）にする — 資源破棄段での固着が readback と同じ最終行に畳まれ、誤って AsyncGPUReadback 化という推測修正へ誘導されうる。
- 段階3段のままコメントだけ直す — セットアップ段で固まると対象名すら出ず、計装が空振りするケースが残る。
- ADR と workflow コメントを事実へ訂正するだけ（実装変更なし） — 連続ハング（約9%）が人手 rerun のまま残る。
- `ci-auto-rerun.cjs` に「step 実行時間が step timeout 値に達したら infra」を足す — watchdog 改修は非目標（2026-09-18 裁定）。
- 段名を internal const へ集約 / enum 化 — 実装とテストがバグを共有する、または enum→文字列の変換規則という暗黙ルールが増える。

出所: ユーザー裁定 2026-09-18 選択「5段にして窓を全部埋める（推奨）」「step名を変えてinfra判定に乗せる（推奨）」「現状のまま（推奨・段名トークン）」

リンク:
- ADR: `docs/adr/0063-ci-hang-shard-timeout-and-icon-capture-instrumentation.md`
- 前段の裁定: `.decisions/2026-09-18-CIのshardハングはstep-timeoutでfailure化し真因は計装で確定させる.md`
- レビュー実行記録: `../moorestech_logs/harness/moores-code-review/runs/2026-09-18-0410/`
