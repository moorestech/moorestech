# post-checksは削除せず発火ゲート化する

決定: post-checks 2本（comment-rationale-guard・comment-convention-guard）は削除せず、発火条件を付けて空振り回だけを消す。rationale-guardは最終diffにコメント削除行がある時だけ、convention-guardは `checks-final.json` の `comment_length` 候補が1件以上ある時だけ起動する。スキップは最終報告に1行明記。

棄却案: 2本とも削除する（ユーザー当初案）→ 採用率調査で convention-guard 適用20回（全系統2位）・rationale-guard 適用8回・破棄ほぼゼロと実績トップ層だったため、削除でなくゲート化を選択。

理由: 2026-08-16再監査の採用率実測。コスト（全体の約8%）の削減は無条件起動の廃止で足りる。

リンク: [[2026-08-16-investigatorはsonnet降格し採用ゼロレンズは発火厳格化]]
