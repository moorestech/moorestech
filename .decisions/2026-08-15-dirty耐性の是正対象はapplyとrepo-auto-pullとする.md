# dirty耐性の是正対象はapplyとrepo-auto-pullとする

決定: 是正対象は pr-adjudicated-apply の SKILL.md と、always-on の `repo-auto-pull.py`（`git merge --ff-only` が自動生成dirtyで恒久的に失敗しうる）の2箇所とする

棄却案: applyのみ直す／共通リファレンスに「自動生成dirtyの扱い」を横断定義し各スキルから参照する

理由: 実測の結果「dirtyなら中止」ゲートを持つスキルは apply だけだった（pr-independent-review は専用worktreeを毎回resetする別設計、moores-code-review はdirty込みで注記するのみ）。横断ルールを先に作っても参照者が1つでは早すぎる。一方 repo-auto-pull は今回のピン汚染が直撃する経路なので同時に直す

リンク: [[2026-08-15-applyのdirty判定は全面エージェント判断にする]]
