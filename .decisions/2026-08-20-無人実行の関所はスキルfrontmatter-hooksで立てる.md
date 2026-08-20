# 無人実行の関所はスキルのfrontmatter hooksで立て、Stopは上限2回でブロックする

2026-08-20。「今回変更した両skillについて、AIがストップしようとしたりask user questionつかおうとしたら
hooksで『それダメだよ』って指示を入れたらいいのでは」というユーザー提案から出た裁定3件。
対象は `.agents/skills/pr-independent-review/` `.agents/skills/pr-adjudicated-apply/` と
`~/hermes-agent/data/services/pr-review/poller.py`（git管理外）。

## 決定1: Stopをhookでブロックし、上限2回でフェイルオープンする

成果物（review: `session-done.marker` / apply: `apply-result.json` / 共通: `abort.json`）が無いまま
ターンを終えようとしたら block し、正しい終わり方を再注入する。同一セッション最大2回まで、3回目は素通し。
カウンタは自前の状態ファイルで持つ（`user-simulator/scripts/shadow-gate.sh` と同形）。

- 棄却: 成果物が出るまで無制限にblockする案（本当に続行不能なセッションがblockと停止を延々繰り返しquotaを焼き切る）
- 棄却: blockせずDiscord通知だけ出す案（暴走リスクはゼロだが、20分空転とRESUME予算1発消費という症状が残り
  「人は見ていない」という前提と噛み合わない）
- 理由: cmuxフォアグラウンド化で「ターン終了＝プロセス死」が消え、途中で止まっても誰も気付かなくなった。
  pollerは transcript が `IDLE_SECONDS=1200` 止まって初めて自壊と判定し、唯一の `MAX_REVIEW_RESUME=1` を消費する

## 決定2: hookはrepo横断のsettings.jsonではなくスキルのfrontmatter hooksに置く

`pr-independent-review/SKILL.md` と `pr-adjudicated-apply/SKILL.md` の frontmatter に
`PreToolUse(AskUserQuestion)` と `Stop` を書く。そのスキルが発動している間だけ関所が立つ。

- 棄却: ユーザーレベル `~/.claude/settings.json` に登録する案（このマシン限定になりgitレビューも履歴も残らない）
- 棄却: repo横断の `.claude/settings.json` + `.dev-hooks/` に置く案（開発者の通常セッションまで巻き込む）
- 棄却: 上記2つへの重複登録案（1回のStopで二重発火し、冪等性とカウンタ共有を自前で保証する必要が出る）
- 出所: ユーザー裁定 2026-08-20 原文「skillにskillが発動したときだけつけれるhooksがある」
- 理由: 前例が `moores-grill-with-docs` の shadow-gate（同じStop関所の形）。スキル発動＝無人起動の文脈そのものなので
  `PR_REVIEW_UNATTENDED` のenv判定も不要になる

## 決定3: apply起動前にスロットを `origin/master` へ戻す

`reset_apply_slot_to_master(slot)` を apply の新規起動とretry起動の直前に呼ぶ。fetchが失敗したら
force checkoutへ進まずログだけ残して起動を続ける。

- 棄却: apply側はhookを諦めreview側だけ導入する案（「無人applyはターン終了で死ぬ」という記録済みの失敗モードが未対応で残る）
- 棄却: hook本体だけrepo外に置きSKILL.mdから絶対パスで呼ぶ案（frontmatterの登録自体が古い版だと欠けるため無意味）
- 理由: applyは `cwd=slot`（`~/moorestech-worktrees/pr-apply`）で起動し、スロットのディスク状態は前回ジョブが
  残した別PRのheadのまま。frontmatter hooksは起動時点でディスクにある版が読まれるため、関所が黙って登録されない

## リンク

- [[2026-08-20-無人レビューの終了処理と時刻不明表現とlimit判定の裁定]]（決定2の `session-done.marker` の出所）
- ADR: `docs/adr/0023-unattended-review-runs-in-cmux-foreground.md`
