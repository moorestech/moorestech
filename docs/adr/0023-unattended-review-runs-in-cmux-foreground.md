# 0023. 無人レビュー/applyは cmux フォアグラウンド対話セッションで走らせる

日付: 2026-08-20 / 状態: 採択

## 文脈

pr-review poller は `claude -p`（headless・detach）でレビューと apply を起動してきた。headless はターンが終わるとプロセスが消えるため、(a) findings.json を書く前にターンが終わる「自壊」（PR1193 で resume により回収）、(b) session limit で死ぬと親もサブエージェントも消え、再開は新セッションでゼロから（PR1176 は r1〜r4、5日で限界死 $2.4k・18%）、(c) 人が途中で介入・観察できない、という問題があった。実機確認により、サブエージェントは親 `-r` 後に SendMessage で文脈ごと継続できる（docs/research/2026-08-20-token-burn-reassessment.md §8）。

## 決定

poller は cmux CLI（`workspace create --cwd --command` / `send` / `capture-pane` / `workspace close`）で PR ごとに専用ワークスペースを作り、**対話モードの claude をフォアグラウンドで起動**する。プロンプト冒頭で無人運用であることを明示する。完了・死亡・限界の検知は transcript jsonl（session-id 固定）と findings.json / apply 結果ファイル、必要なら `capture-pane` で行う。session limit は reset 時刻まで待ってから同じペインへ継続指示を送る。cmux が応答しなければ起動を見送り通知する（headless へのフォールバックは持たない）。同時レビューは2本まで。

出所: ユーザー裁定 2026-08-20「cmuxで新しいペインを作ってそこでフォアグラウンドで実行する。これにより、何か合った時にすぐに介入できる。ただし、AIには無人運用であることを伝える」「同時起動は2にしたい」。

## 検討した選択肢

- headless `-p` のまま resume を強化する（PR1193 の延長）— 自壊・限界死のたびに `-r` 再入が要り、サブエージェント継続の手順も親任せになる。棄却
- cmux と `-p` の併用（cmux 不在時フォールバック）— 検知ロジックが二系統になる。棄却
- 同時起動1本 — 昼間の処理が遅すぎる。棄却（ユーザー裁定）

## 帰結

- 自壊（ターン終了死）は構造的に消える。限界死は「一時停止」になり、再開コストは未完了エージェントのコンテキスト再送のみ
- poller は cmux アプリの稼働に依存する（launchd 配下から GUI セッションの cmux ソケットへ）。cmux 停止時は無人パイプラインが止まり通知される
- review.log（stdout）が無くなり、検知は transcript ベースへ移る。`rate_limited()` 等の判定関数は transcript の最終 assistant text を読む形へ置き換える
- 人が介入できる反面、介入した内容は無人フローの想定外になりうる。介入は「止める・続きを指示する」に限る運用

実装: docs/superpowers/plans/2026-08-20-pr-review-poller-cmux-foreground.md

## 追記 2026-08-20: 無人実行の関所（スキルfrontmatter hooks）

対話モード化の副作用として「ターン終了＝プロセス死」という暗黙の終了契約が消えた。途中で止まっても
プロセスは生き続けるため誰も気付かず、poller は transcript が `IDLE_SECONDS=1200` 止まって初めて自壊と判定し、
唯一の `MAX_REVIEW_RESUME=1` を消費する。実質20分の空転と予算1発の消費が、無言で起きる。

そこで両スキルの frontmatter hooks に関所を置いた（`pr-independent-review/scripts/unattended-gate.py`）。
repo横断の `.claude/settings.json` ではなく frontmatter に置くのは、**そのスキルが発動している間だけ**関所を立て、
開発者の通常セッションを巻き込まないため（前例: `moores-grill-with-docs` の shadow-gate）。

- `Stop` … 起動プロンプトに `【無人起動】` があり、かつ成果物（review: `session-done.marker` /
  apply: `apply-result.json` / 共通: `abort.json`）が無ければブロックし、正しい終わり方を再注入する。
  同一セッション2回でフェイルオープンする（無制限blockは、続行不能なセッションがquotaを焼き切るため棄却）
- `PreToolUse(AskUserQuestion)` … 無人実行中はdenyし、判断の行き先（裁定カード / apply-result.json の summary /
  abort.json）を再注入する。起動引数の `--disallowedTools` と二重化するのは、hookはサブエージェント内でも
  発火することが実測済みなのに対し、`--disallowedTools` のサブエージェント継承が未検証のため
- 全失敗経路は exit 0・無出力（fail open）。関所の実装バグでレビューが止まる方が、たまに素通しするより高くつく

apply は `cwd=slot`（`~/moorestech-worktrees/pr-apply`）で起動し、スロットのディスク状態は前回ジョブが残した
別PRのheadのままである。frontmatter hooks は起動時点でディスクにある版が読まれるため、そのままでは関所が
黙って登録されない。よって poller は apply 起動の直前にスロットを `origin/master` へ戻す
（`reset_apply_slot_to_master`）。review 側は `cwd=CLONE_DIR`（master固定のメインクローン）なので元から問題ない。

裁定の蒸留: `.decisions/2026-08-20-無人実行の関所はスキルfrontmatter-hooksで立てる.md`
