# debug-workflow スキルは削除し unity-runtime-bug-hunt の Rider 依存を外す

決定: `debug-workflow` スキル（7観点の仮説ファンアウト・ユーザー観察待ちゲート・aggregate-hypotheses.py・evals.json）は統合せず丸ごと削除する。`unity-runtime-bug-hunt` は JetBrains Rider debugger（rider-debugger MCP）を前提にしない。BP・変数観測・スレッド一覧の手順と `references/debugger-gotchas.md` を除去し、`uloop execute-dynamic-code` / `get-logs` と一時ログだけで完結する手順にする。

棄却案:
- debug-workflow を bug-hunt の1段（観点表1枚）として残す → 使っていないスキルを畳んで残しても読まれず、常時ロードだけ増えるため棄却
- Rider を「繋がった時だけ使う補助」に降格して残す → そもそも使っていない。接続失敗（ECONNREFUSED）が毎セッション出る状態を維持する理由がないため棄却

理由: ユーザー裁定 2026-09-23 原文「debug- はもう消して ok、使わない。rider関連ももう使ってないから消してok」。剪定監査 §8（review.moores.tech/docs/skill-prune-audit#s8）の「debug-workflow 統合」「Rider必須前提は緩める」の2項目に対する裁定。

リンク: [[2026-08-03-スキル3重ミラーを廃止しgit管理は1箇所に絞る]]

## 追補: unity-runtime-bug-hunt 自体も削除する

決定: Rider を抜いた bug-hunt は「get-logs → execute-dynamic-code で状態ダンプ → 一時ログ → 除去」の手順の言語化だけで、uloop-get-logs / uloop-execute-dynamic-code と二重になるため削除する。moorestech 固有の API 早見表（`references/project-api-cheatsheet.md`）と「見えないは probe で確定する」規則だけを `unity-playmode-recorded-playtest/references/runtime-state-probe.md` へ移す。bug-report-auto-fix Step 4 は起動先を失うので手順3行を直書きする。

棄却案:
- Rider を外した bug-hunt を残す → 参照元が bug-report-auto-fix 1箇所（当日付け替えたばかり）で、手順の固有価値が無いため棄却

理由: ユーザー裁定 2026-09-24 原文「ハントってもう要らなさそう？」→「じゃあそれで」（削除提案を承認）。
