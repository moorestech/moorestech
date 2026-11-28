# Orca 使い方メモ

moorestech の並列セッションは Orca で開き、Orca の中でやり取りする。CLI は `/Applications/Orca.app/Contents/Resources/bin/orca`（以下 `orca`）で、PATH の `orca` に頼らない。最初に `orca status` で ready を確かめる。構文の正は同梱の `orca skills get orca-cli`・`orca skills get orchestration`。

## 階層と ID

```
bot > workspace（worktree / folder） > エージェント（terminal 型 / chat 型）
```

- エージェントの ID は `orca_session_id:<uuid>`、ターミナルの handle は `term_<uuid>`（どちらも `orca agent list --json`）
- handle は Orca や端末の再起動で変わる。控えた handle を使い回さず、操作のたびに取り直す
- 自分の ID は起動時の前置き（`Your Orca session ID is: …`）で確かめる。自分に送らない

## 新しいセッションを開く

新しいセッションは必ず bot の配下に置く。moorestech の作業は `--bot moorestech`。worktree は先に `moores-wt new` で切り、その worktree を `-C` に渡す（本体を渡すとメインワークツリーで作業してしまう）。

```bash
~/bin/orca-agent-pane -C <worktree> -a claude -m opus --bot moorestech --prompt-file <依頼文>
~/bin/orca-agent-pane -C <worktree> -a codex -m "$(~/bin/codex-model sol)" --bot moorestech --prompt-file <依頼文>
```

- 終了コード 0 で依頼文が届き、確認画面でも止まっていない。6 は起動したが依頼文が処理されていない（Codex の「Hooks need review」等）。stdout の JSON の `screen` を見て対処し、再実行しない
- Orca 内の coordinator が worker として渡すなら `orca orchestration worker-start --spec "…" --worktree <selector> --agent <agent>`
- 起動後は `orca worktree ps` で `under bot moorestech` と、最初の依頼が `last asked` に出たことを確かめる

## 状況確認

```bash
orca worktree ps                        # workspace ごとの bot・エージェントの状態・最後の依頼と発話・handle
orca worktree ps --agent-wait --json    # 質問待ち・作業中を JSON で
orca agent list --json                  # エージェント・チャット・ターミナルと orca_session_id
```

`waiting for an answer` が質問待ち、`working` が作業中。`ps` の preview は端末出力の断片なので、中身は次の `agent read` で読む。

## 別セッションを読む

```bash
orca agent read <orca_session_id> --limit 40              # 会話。止まっていても読める
orca terminal read --terminal <handle> --screen --json    # 今描かれている画面（選択画面・確認画面）
```

読むだけなので安全。`terminal read` は `--screen` 無しだと出力の積み重ねで、TUI は文字が重なって読めない。

## 別セッションに送る

- Orca の中から: `orca orchestration send --to <宛先> --subject "…" --body "…"`。宛先は `bot:<名前>`・`dispatch:<id>`・`run:<id>`。worker の質問には `orca orchestration reply --id <msg_id> --body "…"`
- Orca の外（Hermes・cron・ssh）からは `orchestration send` が `no_active_sender_terminal` で拒否される。新しい仕事は新しいセッションで渡し、bot には `orca automations create --bot moorestech --once --by <ISO 時刻> --prompt "…"` で頼む
- 入力待ちの terminal 型に一言だけ: `orca terminal send --terminal <handle> --text "…" --enter --wait-submit 10`。作業中の相手には打たない（入力に割り込むため）
- 送った後は `agent read` で、依頼が届いて作業が始まったかを確かめる。送った応答だけで届いたとしない

## 終わるのを待つ

```bash
orca terminal wait --terminal <handle> --for tui-idle --timeout-ms 600000   # 入力待ちに戻るまで
```

完了は表示でなく、コミット・報告ファイル・PR で確かめる。
