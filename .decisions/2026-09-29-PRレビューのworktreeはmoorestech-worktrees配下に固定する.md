# PRレビューの worktree は moorestech-worktrees 配下に固定する

- 決定: pr-independent-review が作る `pr-<番号>`・`skills-canon-<sha8>`・`skill-<用件>` は `$ORIGIN` の親の `moorestech-worktrees/` に置く。置き場は `canon_setup.py` が決めて `worktree_parent` として返し、エージェントに `--parent` を選ばせない。
- 棄却案: 従来どおり実行エージェントが「worktree親ディレクトリ」を解釈する（実際には repos 直下に `pr-*` が18本・`moorestech-pr-review-baseline-worktrees/` が別に生え、置き場が揺れて掃除から漏れた）。
- 理由: worktree を moores-wt と同じ一箇所へ集め、溜まっているかを一目で見られるようにする。
- リンク: 2026-09-29 セッション（repos 直下の `pr-*` 29本を一括削除した際のユーザー指示「全部 moorestech-worktrees 配下に収まるようにしてほしい」）
