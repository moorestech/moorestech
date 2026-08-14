決定: checked-inのprojectスキル（.agents/skills）をskillOverridesで無効化する場合、gitignoreされる.claude/settings.local.jsonではなく共有の.claude/settings.jsonに書く。
棄却案: settings.local.jsonへの記載（/doctorの保守的デフォルト）。worktree新設時に引き継がれず、無効化の意図が共有されない。
理由: 対象スキル自体がgit共有物なので無効化判断も共有されるべき。worktree頻用運用ではlocal設定の欠落が挙動差を生む。
リンク: userスキル（~/.claude/skills）の無効化は従来どおりuser/localスコープでよい。
