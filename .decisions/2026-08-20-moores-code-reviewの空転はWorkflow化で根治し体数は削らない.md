# moores-code-reviewの空転はWorkflow化で根治し体数は削らない

決定: moores-code-reviewのオーケストレータ（sonnet委譲subagent）をWorkflowスクリプト（`scripts/review_workflow.js`）へ置き換え、系統の起動・再起動・統合・適用を決定論的に回す。レンズ/reviewerの体数は削らない。読み込み累積を減らす「前展開（expanded.md）」方式は、難検出Critical 5件のA/B replay実験（bd moorestech-zfk5・別セッション）で精度低下が無いと確認できるまで既定にしない。壊れている安い系統（Codex外部監査・dead_member ILゲート）は削減より先に復旧する。

棄却案:
- vltk方式（sonnetオーケストレータの待ち方だけを直す） → 空転の正体は「待つだけで1ターン＝全コンテキスト再送」であり、待機がJSのawaitになるWorkflow化の方が項目ごと消える。再開（resumeFromRunId）も付いてくる
- 採用ゼロ系統の削除による体数削減 → 採用ゼロ組は既に全部sonnet（1体 実単価≈$1.2）で、全削除しても−$20〜35/本。採用272件の53%がswarm単独検出で、削ると失う方が大きい
- 前展開方式の即時既定化 → 破棄理由の最頻が「実コード・実データ照合の前提誤り」で、掘ったから当たった成果を前展開が弱めるリスクがある。replayで測ってから

理由: 2026-08-20再計測（`docs/research/2026-08-20-moores-code-review-diet-assessment.md`）。1本$503〜524のうちオーケストレータ待機が$194〜240、reviewer 27体$164〜186。費用構造は起動ベース5%・読み込み累積65%で、効くのは体数でなく「1体のターン数×ctx」とopus既定。headless（claude -p）でWorkflowが動かない懸念は、無人レビューのcmuxフォアグラウンド化（別件）で消えた。

リンク: [[2026-08-16-採用ゼロreviewer削除とレンズ降格]] / [[2026-08-16-investigatorはsonnet降格し採用ゼロレンズは発火厳格化]] / bd moorestech-n9e5 / fxy7 / zfk5 / dkhy / qigy
