# ワークスペース隔離（最初のsubagent派遣前・必須）の手順

SKILL.md ゲート1の実行手順。根拠は `.decisions/2026-08-13-SDDはworktree隔離を必須ゲートにする.md`。

## なぜ必須か

本体ワーキングツリーは他セッション・並行エージェントと共有されており、subagentのコミットが無関係な作業を巻き込む事故が実際に起きている。隔離は「あれば良いもの」ではなく最初の派遣（SDD本体ならタスク1、単一subagentモードならその1体）の前提条件である。

## 例外は2つだけ

1. **既にworktree内にいる** — 新規作成せずそのまま再利用する
2. **人間がこのセッション内で自分の言葉で「本体ワーキングツリーで作業してよい」と指示した** — 進捗台帳に記録して続行する。ここでの「本体」は**ディレクトリ（本体ワーキングツリー）**のことだけを指す。**この例外は隔離の免除であって、実装の担い手の変更ではない** — 本体セッションが自分で実装コードを書いてよいという意味ではないし（規模ゲート未満は単一subagent実装モードが唯一の行き先）、SDD本体（タスクごと派遣）への切替でもない。本体セッションが実装コードを書くのは、人間が「本体セッションが自分で書け」と別途明示した場合に限られ、その場合は**自分のコンテキスト残量が3割を切った時点で作業を止め、残りを単一subagent実装モードへ引き渡し、切替点を台帳に記録する**（実装で本体コンテキストを使い切ると、最終レビュー所見への対応余力が消える）

本体ワーキングツリーで既にfeatureブランチを切って作業中だった場合も例外にはならない。このタスクが所有すると確認できた未コミット変更だけをworktreeへ移してから着手する。所有者を判定できない変更が1件でもあれば移送せず、人間へエスカレーションする。

## 手順

```bash
# 0. 現在地を判定する（2つの値が異なればworktree内 = 作成不要）
# 0. Detect the current location; differing values mean we are already in a worktree
git rev-parse --git-dir; git rev-parse --git-common-dir

# 1. 本体checkoutに触れずorigin/masterを最新化する
# 1. Refresh origin/master without touching the main checkout
git fetch origin master

# 1.5. dirtyなら所有者をパス単位で確認し、このタスク所有の変更だけを退避する
# 1.5. If dirty, verify ownership per path and stash only this task's changes
git status --short
TASK_OWNED_PATHS=(<validated-path> ...)
if test ${#TASK_OWNED_PATHS[@]} -gt 0; then
  git stash push --include-untracked -m "sdd-worktree-move-<task-slug>" -- "${TASK_OWNED_PATHS[@]}"
  SDD_STASH_COMMIT=$(git rev-parse stash@{0})
fi

# 2. 計画名から取ったslugでタスクブランチ付きworktreeを作る
# 2. Create a task-branch worktree named after the plan slug
MAIN=$(git rev-parse --show-toplevel)
git worktree add -b <task-slug> ~/moorestech-worktrees/<task-slug> <base>

# 2.5. 退避した変更を隔離worktreeへ復元し、stashは復旧用に保持する
# 2.5. Restore task changes in the isolated worktree and retain the stash for recovery
if test -n "${SDD_STASH_COMMIT:-}"; then
  git -C ~/moorestech-worktrees/<task-slug> stash apply "$SDD_STASH_COMMIT"
  git -C ~/moorestech-worktrees/<task-slug> status --short
fi

# 3. メイン側Unityを閉じてからLibraryをAPFSクローンで複製する
# 3. Close Unity for the main worktree before cloning its Library via APFS copy-on-write
cp -Rc "$MAIN/moorestech_client/Library" ~/moorestech-worktrees/<task-slug>/moorestech_client/Library
```

## 注意点

- `<base>`は計画が積み上がる土台。通常は最新の`origin/master`、本体の未コミット変更を移す場合と計画が現ブランチの続きなら`HEAD`
- Libraryの複製は計画がUnityに触れるかに関わらず常に行う。数秒の投資で、後からコンパイル・テストが必要になった際の再インポート数十分を確実に回避する
- メイン側Unityが開いている間はLibraryを複製しない。実行中ならUnityを閉じてから手順3を実行する
- 本体がdirtyなら全変更の所有者をパス単位で確認する。このタスク所有と確認できたパスだけを明示して`git stash push --include-untracked -- <paths>`で退避し、所有者不明の変更があれば移送せず人間へエスカレーションする
- worktree側ではstashを`apply`し、復元結果を確認する。復旧手段を失わないようstashは自動dropしない
- 以降の編集・`uloop`各コマンド・テスト・コミットは**すべてworktree側の絶対パス**で行う。`--project-path`もworktree側を指す
- 有料アセット(`PersonalAssets`)は本体にしか存在しない。計画がこれに依存する場合は着手前に人間へエスカレーションする
- サーバーポート11564は固定のため、他worktreeのPlayModeとは同時実行できない。プレイ録画テストを含む計画では1本ずつ動かす
- worktreeは完了後も削除しない（作業消失防止。cleanupは人間の指示があった時のみ）
