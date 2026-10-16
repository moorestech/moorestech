決定: moores-writing-plans の Execution Handoff は、個人スキル writing-plans と同じく新規 Codex セッション（cxd・gpt-6.1-sol）で moores-subagent-driven-development を起動し、SDD は runtime-codex.md、moores-code-review は codex-host.md の runner で回す。開始プロンプト末尾の「codex移譲モードで」と `--model opus` 指定は消す。
棄却案: 既定は Codex にしつつ、ユーザー指定時だけ Claude（opus・codex移譲モード）で起動する分岐を残す。
理由: ユーザー裁定 2026-10-16。原文「home側のskillと同じように、こちらもcodexでタスクを依頼するようにしたい」→ 選択「置き換える」。帰結: 実装セッションでは Claude 側の Stop hook（ledger-gate 等）が効かず、ゲートはスキル本文の必須タスクが担う。
リンク: .agents/skills/moores-writing-plans/SKILL.md / ~/.agents/skills/writing-plans/SKILL.md
