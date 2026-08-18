# GameScreen通知テストは可読性検証を足さず実態に合わせて改名する

裁定日: 2026-08-18 / 出所: ユーザー裁定（moores-code-review C4）

## 決定

`e2e/tests/notification/layering.spec.ts` の「GameScreenでは通知が遮られず読める」テストに可読性検証を追加せず、**このテストは「GameScreen でも通知がマウントされ続ける」ことの確認に留め、テスト名を実態へ合わせて改名する**。

## 理由

現アサーションは `toBeVisible()` と `boundingBox()` のサイズのみで、遮蔽も不透明度も反映しない。テスト名が主張する「読める」を検証していないことが問題であって、検証の不足そのものは受け入れる。
名前を実態へ合わせれば、テストが何を守っているかの誤認は消える。

## 棄却案

- **画素差アサーション（通知あり/なしで通知行の矩形に差があることを assert）** — 第1テストの裏返しで実装可能だが、GameScreen の可読性まで守る必要はないと判断。
- **opacity assert の追加** — 退場アニメ中の緑化は塞げるが「他要素の裏に回っていない」は依然検証されず、中途半端。
- **`elementFromPoint` によるヒットテスト** — 実装不可能につき破棄済み。`.notification` は `pointer-events: none` のままで通知を素通りし、クリック透過の維持は制約（Unityへの入力排他判定を変えないため）。

## リンク

- ADR: `docs/adr/0017-webui-notification-behind-stage-layer.md`
- rundir: `../moorestech_logs/harness/moores-code-review/runs/2026-08-18-1806/`
