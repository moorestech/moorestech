決定: リスクの低い作業（仕様記述・壁打ち等）を除き、全タスクでタスク毎に使い捨てworktreeを切る。メインワークツリーでのUnity起動は、worktreeへコピーするためのLibrary生成（日次）のときのみ許可。ルールの明文化はこの環境（Mac mini）固有の運用のため、AGENTS.mdには載せずgit管理外のCLAUDE.local.md（.git/info/excludeで除外）に置く。

棄却案: ①常設スロット方式（Libraryコピー済み常設worktree2〜3本の使い回し） ②メインでのUnity起動の全面禁止 ③AGENTS.mdへの明文化。

理由: 並列セッション数が高く、メインワークツリー共有による競合（Editor占有・無人apply死亡）を隔離性最優先で排除する。運用は環境固有のためリポジトリ共通規約にしない。

リンク: [[2026-08-13-SDDはworktree隔離を必須ゲートにする]] / CLAUDE.local.md（ローカル専用・untracked）
