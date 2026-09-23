# debug-workflow スキルは削除し unity-runtime-bug-hunt の Rider 依存を外す

決定: `debug-workflow` スキル（7観点の仮説ファンアウト・ユーザー観察待ちゲート・aggregate-hypotheses.py・evals.json）は統合せず丸ごと削除する。`unity-runtime-bug-hunt` は JetBrains Rider debugger（rider-debugger MCP）を前提にしない。BP・変数観測・スレッド一覧の手順と `references/debugger-gotchas.md` を除去し、`uloop execute-dynamic-code` / `get-logs` と一時ログだけで完結する手順にする。

棄却案:
- debug-workflow を bug-hunt の1段（観点表1枚）として残す → 使っていないスキルを畳んで残しても読まれず、常時ロードだけ増えるため棄却
- Rider を「繋がった時だけ使う補助」に降格して残す → そもそも使っていない。接続失敗（ECONNREFUSED）が毎セッション出る状態を維持する理由がないため棄却

理由: ユーザー裁定 2026-09-23 原文「debug- はもう消して ok、使わない。rider関連ももう使ってないから消してok」。剪定監査 §8（review.moores.tech/docs/skill-prune-audit#s8）の「debug-workflow 統合」「Rider必須前提は緩める」の2項目に対する裁定。

リンク: [[2026-08-03-スキル3重ミラーを廃止しgit管理は1箇所に絞る]]
