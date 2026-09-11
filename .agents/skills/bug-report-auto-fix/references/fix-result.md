# fix-result.json

自動修正ラン1件の終端ファイル。`$RUN/fix-result.json`（`$RUN = $BUG_REPORT_RUNDIR_BASE/<run-id>`）へ書く。
**これを書くまでランは終われない**（`unattended-gate.py stop bugfix` が Stop をブロックする）。
poller はこのファイルだけを見てランの結末を判断する。

```json
{"status": "fixed|not_reproduced|needs_ruling|failure", "pr_number": 1234, "base": "master|<branch>", "determinism": "ok|diverged|unchecked", "bd_id": "moorestech-xxxx", "summary": "...", "remaining": "..."}
```

## フィールド

| キー | 必須 | 意味 |
| --- | --- | --- |
| `status` | 常に | 下の4分岐 |
| `pr_number` | `fixed` のみ必須 | draft PR の番号（整数）。他の status では省略または `null` |
| `base` | `fixed` のみ必須 | PR の宛先ブランチ。Step 6 の判定結果（`master` または報告ブランチ名） |
| `determinism` | 常に | Step 2 の結果。`ok` / `diverged` / `unchecked` |
| `bd_id` | 常に | Step 1 で作った追跡 issue の id |
| `summary` | 常に | 何が起きたか・何を判断したか・欠損や環境差の有無を1〜数文で |
| `remaining` | 空でよい | 人がこの後やること／次に試すこと |

`summary` と `remaining` にバンドルの中身（スナップショット本文・ログ本文）を貼らない。パスで指す。

## status の3分岐 + failure

### `fixed`

合成NUnit（または「プレイテスト証拠のみ」）と修正が draft PR にある。`pr_number` 必須。
`summary` には「原因・修正の要点・再現テストの種別（合成NUnit／証拠のみ）・`base` を選んだ理由」を書く。
PR 本文に裁定事項を残した場合はその旨も書く（人が PR を見る動機になる）。

```json
{"status": "fixed", "pr_number": 1355, "base": "master", "determinism": "ok",
 "bd_id": "moorestech-0421", "summary": "搬送ベルトの端でアイテムが消える件。InserterConnector の tick 順序が原因。合成NUnit ItemDisappearOnBeltEndTest を追加し master 基底で修正。", "remaining": "PR #1355 のレビューとマージ"}
```

### `not_reproduced`

Step 3 の観察で症状が出ず、追加シナリオ3本でも出なかった。**コードは触らない**。
`summary` に試したこと（何をどう動かして何が起きなかったか）、`remaining` に次に試す案を書く。

```json
{"status": "not_reproduced", "pr_number": null, "base": null, "determinism": "ok",
 "bd_id": "moorestech-0422", "summary": "観察ラン3本（報告位置で30秒／列車発車／インベントリ開閉）いずれも症状なし。unity.log にも Error 無し。", "remaining": "報告者に直前の操作の詳細を確認。frames/0087.png 以降にUIが写っていないため操作系の記録が要る"}
```

### `needs_ruling`

期待挙動が仕様として決まっておらず、どちらが正しいか裁定が要る。**コードは触らない**（実装を勝手に選ばない）。
`bd create --type=task --priority=1` で裁定 issue を積み、本文に候補と各候補の帰結を書いて、その id を `remaining` に書く。

```json
{"status": "needs_ruling", "pr_number": null, "base": null, "determinism": "ok",
 "bd_id": "moorestech-0423", "summary": "液体レシピの端数が切り捨てか切り上げかが未定義で、報告の症状はどちらの解釈でも仕様通りになりうる。", "remaining": "裁定 issue moorestech-0424（候補A: 切り捨て / 候補B: 切り上げ、帰結を記載）"}
```

### `failure`

環境要因でランが成立しなかった（Editor 起動不能・master data 不整合・`COMMIT_MISSING` で差分が当たらず再現環境を作れない等）。
`summary` に原因を書く。バグそのものの難しさは `failure` ではない（それは `not_reproduced` か `needs_ruling`）。

```json
{"status": "failure", "pr_number": null, "base": null, "determinism": "unchecked",
 "bd_id": "moorestech-0425", "summary": "MASTER_DIR が空。manifest の masterData.commit が moorestech_master に存在せず worktree を作れなかった。", "remaining": "master 側のコミットを push してから再実行"}
```

## `determinism`

- `ok`: Step 2 の `replay-check.json` が `allEqual=true`（是正して true にした場合も含む。是正した事実は `summary` へ）
- `diverged`: 発散を是正しきれずに残したまま進めた。発散した DataStore を `summary` に書く
- `unchecked`: 検査自体を実行できなかった（パケットログ欠損・Editor 不達）。理由を `summary` に書く
