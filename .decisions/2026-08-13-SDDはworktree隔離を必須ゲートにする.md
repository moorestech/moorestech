# SDDはworktree隔離を必須ゲートにする

決定: subagent-driven-developmentは、タスク1のimplementer派遣前に専用worktreeを作ることを必須ゲートとする。例外は「既にworktree内にいる（再利用）」「人間がこのセッション内で自分の言葉で本体実装を指示した」の2つのみで、本体で既にfeatureブランチを切っていても例外にならない。worktree作成直後にUnity Libraryを`cp -Rc`で常に複製する。手順はSDDスキル内に持ち、外部スキルへ委譲しない

棄却案: デフォルト推奨に留めコントローラー判断で省略可とする（「既にfeatureブランチだから隔離済み」という自己判断を許すと本体ディレクトリ共有によるブランチ汚染が再発する）／Library複製は計画がUnity作業を含む時だけにする（判定を誤ると数十分の再インポート、`cp -Rc`は16GBでも数秒）／`superpowers:using-git-worktrees`へ委譲する（当該スキルはローカルに存在せず宙吊り参照だった）

理由: 本体ワーキングツリーは他セッション・並行エージェントと共有されており、subagentのコミットが無関係な作業を巻き込む事故が実際に起きている。共有されているのはブランチではなくディレクトリである

リンク: `.agents/skills/subagent-driven-development/SKILL.md`「ワークスペース隔離（タスク1派遣前・必須）」、`~/.agents/skills/subagent-driven-development/SKILL.md`（プロジェクト非依存版）、前例は`~/.agents/skills/macmini-moorestech-dev/SKILL.md` 1章
