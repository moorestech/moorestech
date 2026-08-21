決定: pr-independent-review の測定器（レンズ・reviewer・スクリプト・統合ルール）は、`origin/master` に固定した専用worktree `skills-canon` から読む。`$CANON` は「このSKILL.mdが置かれているtree」ではなくこの固定worktreeを指す
棄却案: ①従来どおり起動元tree（多くはメインworktree）のHEADを `$CANON` とする ②レビューworktree(pr-review)から読む
理由: ①は物差しが「メインworktreeがたまたま乗っているブランチ」になり、実際にレビュー実行中へ別セッションのブランチ切り替えが入った。台帳の `canonical:` は測定器の版を記録する欄なのに、版が実行中に動く前提では記録が意味を失う。`origin/master` 固定なら再現可能。②はレビュー対象のPRが自分を裁くレンズを書ける自己弱体化経路
補足: SKILL.md本体はharnessが起動元treeから読み込むためskillには選べず、固定できるのは参照ファイルのみ。よって起動元と固定worktreeのSKILL.mdを冒頭でdiffし、差分があればfail-closedで停止するガードを併設する。続行時は `canonical:` に skew と両SHAを残す
リンク: 出所=ユーザー裁定 2026-08-05（AskUserQuestionではなく「origin/master に固定（推奨）これにして」）。関連=[[2026-08-05-独立レビューセッションはCANONの作業ツリーに書かず専用worktreeで完結する]]
