# プレイテストの取り込み（Mac mini側）

## 前提

- `~/hermes-agent/data/repos/{moorestech,moorestech_logs}` がある
- `~/hermes-agent/data/services/playtest/env.sh` を `chmod 600` で作る（**値はこのREADMEにもrepoにも書かない**）:
  ```bash
  export PLAYTEST_ADMIN_KEY=...                    # 受け口 Worker の ADMIN_KEY
  export PLAYTEST_DIGEST_DISCORD_CHANNEL_ID=...    # 日次ダイジェストの投稿先
  ```

## supervisor へ登録する

`~/hermes-agent/data/services/always-on/services.json` の `services` 配列へ次を足す（`services.json` はループ毎に再読込されるので supervisor の再起動は不要）:

```json
{
  "name": "playtest-ingest",
  "kind": "periodic",
  "interval_seconds": 300,
  "timeout_seconds": 600,
  "cwd": "~/hermes-agent/data/repos/moorestech",
  "command": ["/bin/bash", "~/hermes-agent/data/repos/moorestech/scripts/playtest/ingest-dispatch.sh"]
}
```

確認（periodic の実行は `supervisor.log` には出ない。サービス個別のログを見る）:

```bash
tail -f ~/hermes-agent/data/services/always-on/logs/playtest-ingest.log        # "run reason=periodic" マーカー
tail -f ~/hermes-agent/data/services/always-on/logs/playtest-ingest-worker.log # "[ingest] ..." 本体
```

## 手動実行

```bash
. ~/hermes-agent/data/services/playtest/env.sh
bash ~/hermes-agent/data/repos/moorestech/scripts/playtest/ingest.sh
```

ロックは `$TMPDIR/moorestech-playtest-ingest.lock`。前回が生きていれば何もせず終わる。

## 成果物

- `moorestech_logs/harness/playtest/{reports,progress}/<steamId>/<id>/`
- **自動修正ランへの投入はここでは起きない**（ADR 0061）。投入は日次ダイジェストを見て人が `scripts/playtest/enqueue-autofix.sh <steamId> <id>` を叩く

## 詰まったとき

- `[ingest] ERROR: READY 本文の files[] が…` → 受け口（plan D）が `complete` で書く要約 JSON に `files[]` が無い。箱は ack されずに残るので、受け口を直せば次の周期で自然に取り込まれる
- `[ingest] ERROR: ack 失敗` → 箱は取り込み済みなので、次の周期で再配信されても再ダウンロードせず ack だけやり直す
