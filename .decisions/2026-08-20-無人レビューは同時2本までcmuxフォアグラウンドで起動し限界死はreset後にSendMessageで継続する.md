# 無人レビューは同時2本までcmuxフォアグラウンドで起動し限界死はreset後にSendMessageで継続する

決定:
1. pr-review poller のレビュー同時起動は **2本まで**（applyのスロット方式と同型。3本目以降は見送り・次tickで再評価）
2. レビュー・apply の claude 起動は `claude -p`（headless・detach）をやめ、**cmux に専用ワークスペースを作りフォアグラウンド対話モードで起動**する（`cmux new-workspace --cwd … --command 'env PR_REVIEW_UNATTENDED=1 HOME=… claude --session-id … --dangerously-skip-permissions "<プロンプト>"'`）。プロンプト冒頭で「無人運用である（質問で止まるな・人は見ていない前提）」を伝える。cmux が応答しない（`cmux ping` 失敗）ときは起動を見送り Discord 通知する（-p フォールバックは持たない）
3. session limit で死んだら CLI 出力の「resets HH:MM」を読んで **reset 時刻＋数分まで待ち**、同じペインへ「RUNDIR の agents/*.md を点検し、保持しているオーケストレータIDへ SendMessage で未完了分だけ継続せよ（再派遣は失敗体のみ）」と送って**同一セッションを継続**する。時刻が読めない時だけ1800秒フォールバック
4. weekly limit で死んだら**失敗ラベルへ遷移して人を呼ぶ**（無人では回復しない。アカウント切替は人の操作）
5. 完了（findings.json / apply 完了）したワークスペースは自動 close、失敗ラベルへ遷移したものは残す
6. 直近5h消費を見た起動ゲートは**今は入れない**

棄却案:
- 同時起動1本（ピークは最も下がるが昼間の処理が遅すぎる — ユーザー裁定 2026-08-20「1だとあまりに遅すぎて昼間の時間帯で困る」）
- 固定1800秒バックオフのままの再開（空起動・通知の荒れ・別死因との混同）
- 新オーケストレータを再派遣して agents/*.md だけ再利用（実装は単純だがオーケストレータの統合途中文脈と未完了reviewerをゼロから）
- 限界死の再開を現状 RESUME_PROMPT（agents/*.md を捨てるな）だけに任せる
- 消費ゲート（閾値が決められない・誤見送りで遅延だけ増える）／時間帯ゲート
- レビューだけ cmux・apply は -p のまま／cmux 不在時に -p へフォールバック（検知ロジックが二系統になる）
- 週次上限も reset まで待つ（数日単位の無人待機になる）
- ペインを常に残す／常に自動 close

理由: 5日間の実測（docs/research/2026-08-20-token-burn-reassessment.md）で、限界死は「人のSDDと無人レビューが5h枠を共有し合算で溢れた時に走っていたものが死ぬ」構造で、死ぬ側の6割が無人レビュー、死んだら毎回ゼロからやり直し（PR1176は r1〜r4）。サブエージェントは親 `-r` 後も SendMessage で文脈ごと継続できることを実機確認（同 §8）。対話モードならターン終了でプロセスが死なず（PR1193 が resume で繕った自壊が構造的に消える）、限界死からの再開も「ペインへ文字を送る」だけになり、人が介入・観察できる。

スコープ外（並行セッションが担当）: moores-code-review オーケストレータの待機規律（Monitor 無出力 until）・poll-guard 拡張・SKILL.md の監督コスト記述訂正。

リンク: [[2026-08-14-独立レビュー無人化はsupervisor素pollerを起点にする]]、[[2026-08-20-無人レビューの自壊対策はresumeとabort申告で入れる]]、docs/research/2026-08-20-token-burn-reassessment.md、docs/adr/0023-unattended-review-runs-in-cmux-foreground.md
