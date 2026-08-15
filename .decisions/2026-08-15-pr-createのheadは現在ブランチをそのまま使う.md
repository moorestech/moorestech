# pr-createのPR headは現在ブランチをそのまま使う

決定: pr-createスキルのブランチ名縛り（例示の`feature/xxx`が実質prefix固定になっていた）を廃止し、指定がない限りcwdでチェックアウト中のブランチをそのままPR headにする。切り直し・改名はしない。マージ済みPRのブランチでも、新しい差分があれば同じブランチで新規PRを作る。

例外は2つだけ:
- `treeN`（worktree常駐の使い回しブランチ）→ 別ブランチを切る。prefixは自由
- ベースブランチ（master等）上 → head にできないので新ブランチを切る

棄却案:
- `treeN`もそのままheadにする（例外ゼロで最も単純）→ 次タスクで`treeN`を巻き戻した瞬間にPRが壊れるため棄却
- `treeN`上では中断してブランチ名指定を求める → 全自動という前提が崩れるため棄却

理由: 例示リテラルが規範として効き、`chore/sdd-worktree-isolation` が `feature/sdd-worktree-isolation` へ勝手に付け替えられた実害が出た。

リンク: `.agents/skills/pr-create/agent.md` ステップ3
