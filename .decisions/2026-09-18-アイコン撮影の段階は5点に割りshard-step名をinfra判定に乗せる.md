# アイコン撮影の段階は5点に割り、ハング判定は専用stepに分けてinfra判定へ乗せる

2026-09-18 ユーザー裁定（moores-code-review の設計判断 D1・D2・D3 と、その後の post-check Critical C-1 への回答）。

決定: (1) アイコン撮影の段階ログを `setup / render / readback / captured / done` の5点にし、セットアップ段と資源破棄段も窓として埋める。(2) `run_test.yml` の shard を3 stepに分ける — テスト実行 step は名前 `Run Unity Test - ${{ matrix.shard }}` のまま `continue-on-error: true` にし、直後に `ci-auto-rerun.cjs` の INFRA_KEYWORDS に当たる名前の判定 step（`Detect Unity shard runner hang`）を置いて**40分の打ち切りに達したときだけ失敗**させ、さらに CODE_KEYWORDS 側の名前の step（`Fail the shard when the Unity test did not pass`）で通常のテスト失敗を job の失敗として残す（`ci-auto-rerun` 自体は無改修）。(3) 段名トークン（`start` / `stage:*` / `completed`）は定数集約も enum 化もせず現状の裸の文字列のままにする。

理由: (1) 3段のままだと、`Instantiate`・Renderer走査・Camera生成からなるセットアップ段と資源破棄段で固着したときに段名が出ず、「何個目のどの対象のどの段階」というこの計装の唯一のゴールが、固着箇所によっては満たされない。窓を全部埋めれば次の1件で必ず箇所が確定する。(2) step timeout は step を failure にするが `timed_out` にはしないため、watchdog は step 名でしか拾えない。ところが `ci-auto-rerun.cjs` は `hasInfraStep` を `hasCodeStep` より先に評価するので、テスト実行 step そのものを `Unity shard runner - ...` へ改名すると、ハングだけでなく通常のテスト失敗まで infra 判定になり、確定的に落ちる PR すべてが毎回1回余分に再実行される（shard 1本あたり最大40分の空費と、赤の確定の遅れ）。判定用の step を別に立てれば「ハング＝infra 名の step が失敗」「通常のテスト失敗＝code 名の step だけが失敗」が両立し、CODE_KEYWORDS の存在意義（明確なコード失敗は再実行しない）を壊さない。打ち切り判定は推測せず、開始時刻を `$GITHUB_ENV` へ記録して経過秒を閾値と比べる。閾値の正本は job の `env.UNITY_TEST_STEP_TIMEOUT_MINUTES` 1箇所で、`timeout-minutes` と判定 step の両方がそこを読む（`timeout-minutes` 側は `fromJSON()` 経由。素の `${{ env.X }}` は文字列のため GitHub に無言で無視される — 実測 run 35269448343）。(3) 段名まで production とテストで共有定数にすると、実装とテストが同じバグを共有して検証力が消える。

棄却案:
- 段階を4段（setup だけ追加）にする — 資源破棄段での固着が readback と同じ最終行に畳まれ、誤って AsyncGPUReadback 化という推測修正へ誘導されうる。
- 段階3段のままコメントだけ直す — セットアップ段で固まると対象名すら出ず、計装が空振りするケースが残る。
- テスト実行 step 自体を `Unity shard runner - <shard>` へ改名する（当初の D2案A） — ハングは拾えるが、infra が先に評価されるため通常のテスト失敗まで再実行され、Unity Test レーンで「明確なコード失敗はスキップ」が到達不能になる（post-check C-1）。
- step 名の変更を撤回し、ADR と workflow コメントを事実へ訂正するだけにする（実装変更なし） — 連続ハングが人手 rerun のまま残り、当初の目的が果たせない。
- 副作用を許容して現状維持＋ADR への明記 — 赤 PR すべてが毎回余分な再実行を消費し、赤の確定が最大40分遅れる。
- `ci-auto-rerun.cjs` に「step 実行時間が step timeout 値に達したら infra」を足す — watchdog 改修は非目標（2026-09-18 裁定）。
- 段名を internal const へ集約 / enum 化 — 実装とテストがバグを共有する、または enum→文字列の変換規則という暗黙ルールが増える。

出所: ユーザー裁定 2026-09-18 選択「5段にして窓を全部埋める（推奨）」「判定用の step を別に立てる」「現状のまま（推奨・段名トークン）」

リンク:
- ADR: `docs/adr/0063-ci-hang-shard-timeout-and-icon-capture-instrumentation.md`
- 前段の裁定: `.decisions/2026-09-18-CIのshardハングはstep-timeoutでfailure化し真因は計装で確定させる.md`
- レビュー実行記録: `../moorestech_logs/harness/moores-code-review/runs/2026-09-18-0410/`
- 判定 step の実測: GitHub Actions run 35270032962（使い捨て workflow TMP Hang Detector Probe。打ち切り→判定 step failure / 通常失敗→判定 step success）
