# reconcileモード（人間レビューとの突き合わせ・改善発火）

本文は SKILL.md の記法（プレースホルダ `$RUNDIR` 等は実値へ展開して書く）に従う。

`/pr-independent-review reconcile <番号>` で単独起動、またはStep 0.5から強制実行される。**ここは改善機構の発火装置であり、改善の
手法・検証・回帰コーパスは moores-code-review 側（`references/skill-improvement.md`・`eval/`）が単一の正。手順・fixture・検証規則を複製しない。**

`$RUNDIR` は自分で決めず、records の `pr-<番号>.md`（最新の `-rN`）の `- rundir:` 行が指すディレクトリを使う。その行が無い古い記録
（2026-08-08以前）は中間生成物が無い前提で、人間コメントとrecordsのテキストだけで突き合わせる。

1. **入力は人間のGitHubコメントのみ**（人間に台帳記入・ラベル付け・分類を求めない）:

       gh api repos/moorestech/moorestech/pulls/<番号>/comments --paginate \
         --jq '.[] | {path, line, body, html_url, commit_id}' > <$RUNDIRの実値>/reconcile-comments.json

   **`commit_id` は必ず一緒に取る** — 改善時のフォレンジック・リプレイのピン先はこの `commit_id` で、自動レビュー当時のheadではない。
   レビューbody（`gh api .../pulls/<番号>/reviews --paginate`）と通常コメント（`gh pr view <番号> --comments`）も読む。
   全部0件なら「人間レビュー未実施」として `reconcile` 列は空欄のまま終了
2. **突き合わせ**: records の裁定・suppressed・Warning（折りたたみ参考含む）と各コメントを照合し、caught / missed / 対象外
   （質問・運用連絡・雑談）に分類する。**迷ったらmissedに倒す**。**verdictが一致していてもreconcileを省かない**
3. **内訳をrecordsへ追記**（`references/record-format.md` の「突き合わせ内訳」）。missedの各行に分類タグとコメントURLを付ける:
   - `[レンズ盲点]` `[reviewer盲点]` `[決定論較正]` — ハーネス既存観点の欠落・較正ミス
   - `[L1語彙]` `[配管]` — 本スキル固有部品（novelty gate・patch生成・context再構成・digest）の欠陥
   - `[規範初出]` — AGENTS.md・レンズ・reviewerのどこにも成文化されていない規範を人間が初めて示したもの。ハーネスの欠陥ではなく
     成文化の入力であり、人間にしか出せない類として分計する（この割合の推移が自動マージ移行可否の実測境界）
4. **ルーティング（改善の実施は全部あちらの規則で）**:
   - `[レンズ盲点]` `[reviewer盲点]` `[決定論較正]` → `<$CANONの実値>/.claude/skills/moores-code-review/references/skill-improvement.md` の手順
     （フォレンジック・リプレイ診断 → 対策先決定 → 実例追記 → 4段階検証（発火・由来サニティ・ブラインド陽陰・実diffバックテスト）→
     `eval/fixtures.tsv`・`eval/expected-findings.md` へ追記）。完了しない改修は改善と認めない。診断をrecordsのテキスト照合で代用しない
   - `[規範初出]` → まずAGENTS.mdまたは決定論チェックへ成文化し、同じ4段階検証に通す
   - `[L1語彙]` `[配管]` → 本スキルの `scripts/` を修正し `tests/test_novelty_gate.py` に赤→緑のケースを追加する
5. **改善キューへ起票**: `records/improvement-queue.md` に1行/件。`closed` にできるのは**手順4の検証完了根拠を `closed根拠` 列に書けた時だけ**:
   - レンズ/reviewer/決定論較正/規範成文化 → 4段階検証の完了記録。特に段階4の「見逃しsurface×検出元マトリクス＋過検知数」が必須。
     合成fixture緑（段階3まで）だけではclosedにしない
   - `[L1語彙]` `[配管]` → 赤→緑を実証したテストの緑
   観点ファイルへの追記だけではclosedにしない（作文はclosedの根拠にならない）
6. **前向きログ**: `$LOGS/harness/moores-code-review/eval-log.md` に1行追記（PR番号・人間指摘数・分類内訳・ハーネス事前検出数・却下数・recordsへの相対リンク）
7. **台帳更新**: `reconcile` 列に実施日。`あなたの実判断`・`一致` 列が空欄なら観測可能な事実（差し戻しコメント・approve・マージ状態）から記入する
