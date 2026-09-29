# PRレビューの worktree は PC共通の worktrees/moorestech 配下に固定する

- 決定: pr-independent-review が作る `pr-<番号>`・`skills-canon-<sha8>`・`skill-<用件>` は PC共通の worktree 置き場 `~/hermes-agent/data/worktrees/moorestech/`（`<clone置き場の親>/worktrees/<project>/`）に置く。置き場は `canon_setup.py` が決めて `worktree_parent` として返し、エージェントに `--parent` を選ばせない。
- 棄却案: 従来どおり実行エージェントが「worktree親ディレクトリ」を解釈する（実際には repos 直下に `pr-*` が18本・`moorestech-pr-review-baseline-worktrees/` が別に生え、置き場が揺れて掃除から漏れた）。
- 理由: ユーザー裁定「PC全体で worktree は `~/hermes-agent/data/worktrees/<project-name>/<worktree-name>` に作る」に従い、moores-wt・applyスロット・masterピンと同じ一箇所へ集め、溜まっているかを一目で見られるようにする。
- リンク: 2026-09-29 セッション（repos 直下の `pr-*` 29本を一括削除した際のユーザー指示「全部 moorestech-worktrees 配下に収まるようにしてほしい」→「PC全体で …/worktrees/<project-name>/<worktree-name> にしたい」）
