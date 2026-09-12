---
name: bug-report-auto-fix
description: |
  バグ報告バンドル（ADR 0057）1件を受け取り、隔離worktreeで決定性検査→再現→修正→合成NUnit→draft PRまでを無人で行う。
  Use When: `/bug-report-auto-fix <run-id>` で起動された時（Mac mini の inbox poller が起動する。人が手で起動してもよい）
hooks:
  # 無人実行の関所。fix-result.json を書くまで終われず、AskUserQuestion は deny
  # Unattended gate: cannot stop until fix-result.json exists; AskUserQuestion is denied
  PreToolUse:
    - matcher: "AskUserQuestion"
      hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py ask"
  Stop:
    - hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py stop bugfix"
---

# bug-report-auto-fix — バグ報告の自動再現・修正（無人実行）

`$RUN = $BUG_REPORT_RUNDIR_BASE/<run-id>`（既定 `~/hermes-agent/data/repos/moorestech_logs/harness/bug-report/runs/<run-id>`）。
`$RUN/run.env` に `WORKTREE`・`MASTER_DIR`・`SERVER_DATA_DIR`・`WORLD_DIR`・`REPORT_COMMIT`・`REPORT_BRANCH`・`LATEST_TICK` と、
**再現環境の欠けを表すフラグ**（`COMMIT_MISSING`・`DIFF_APPLY_FAILED`・`DIFF_ABSENT`・`UNTRACKED_FAILED`・`MASTER_FAILED`・`MASTER_DIFF_APPLY_FAILED`・`MASTER_DIFF_ABSENT`・`MASTER_UNTRACKED_FAILED`）がある。
`$RUN/repo/bundle-status.txt` は運搬側が付けた bundle の3状態（`created` / `not-needed-origin-has-commit` / `failed-*` / `skipped-*`）で、`failed-*` は報告者のローカルコミットが受け側に無いことを意味する。
`SERVER_DATA_DIR` は**記録時にサーバーが実際にマスタを読んだ置き場**（manifest の `serverData` から受け側の worktree 配下へ解決した値）。`MASTER_DIR` とは一致しないことがあり、再生・観察には必ず `SERVER_DATA_DIR` を使う。作業は必ず `$WORKTREE` で行う。
`$RUN`・`$WORKTREE` 等は本ドキュメント上のプレースホルダである。コマンドへ渡すときは `. $RUN/run.env` で読み込むか、実値の絶対パスへ展開して書く。
「バンドル」＝報告1件分の記録一式であり、運搬後は `$RUN` そのものを指す（`manifest.json`・`snapshots/`・`frames/`・`logs/`・`world/`）。

## 同梱スクリプト（`.agents/skills/bug-report-auto-fix/scripts/`）

| パス | 用途 |
| --- | --- |
| `run-edc.sh <project> <snippet.cs> <bundle-dir> [<server-dir>]` | スニペットの `__BUNDLE__`／`__SERVER_DIR__` を置換して `uloop execute-dynamic-code` を実行する |
| `edc/dump-packets.cs` | パケットログを `$RUN/packets.jsonl` へ可読化（Step 1） |
| `edc/replay-check.cs` | 決定性検査。`$RUN/replay-check.json` を書く（Step 2） |
| `scenarios/bug-report-observe.cs` | 報告時のワールドを起動し報告者の位置で30秒観察する（Step 3） |

## HARD GATE

- **終端は `$RUN/fix-result.json` を書いた直後だけ**（`references/fix-result.md`）。書く前に終わる終わり方はバグ
- **3分岐で止まる**: 再現できた→修正まで／再現できない→`not_reproduced`／仕様の曖昧さに当たった→`needs_ruling`（コードを触らず bd に質問を積む）
- **マージしない**。PR は draft
- **時間・利用枠の上限は設けない**（裁定 2026-09-11）。行き詰まりの判定は上の3分岐のみ
- **修正対象は報告されたバグだけ**。ついでの改善はしない（見つけた別問題は bd に積む）

## 無人実行の規律（最初に読む）

このスキルは人が居ない Mac mini 上で poller から起動される。**人へ質問して止まる経路は存在しない**。

- **AskUserQuestion は使わない**（frontmatter の hook が deny する）。判断が要る場面は下の「既定」に従い、
  選んだ解釈と理由を `fix-result.json` の `summary` と PR 本文の「裁定事項」に書いて進める。
  期待挙動そのものが仕様として決まっていない場合だけ `needs_ruling` で止まる
- **待機は同一ターン内でブロッキングして行う**。`uloop run-tests` も観察ランも、結果が返るまでそのターンで待ち切る。
  「あとで結果を確認する」「wakeup をスケジュールした」と述べてターンを閉じることを禁止する — 再開の仕組みは無い
- 判断に迷ったら **ADR 0057 → `.decisions/` の該当裁定 → 既存の前例コード** の順に根拠を探し、進める
- session limit に当たったら何もしなくてよい（poller が reset 後に継続指示を送る）

### 迷ったときの既定（質問の代わり）

| 状況 | 既定の振る舞い |
| --- | --- |
| `COMMIT_MISSING=1` / `DIFF_APPLY_FAILED=1` / `DIFF_ABSENT=1` / `UNTRACKED_FAILED=1` | 止まらず進む。再現環境が報告時と違う旨を `summary` と PR 本文に必ず書く |
| `MASTER_FAILED=1` / `MASTER_DIFF_APPLY_FAILED=1` / `MASTER_DIFF_ABSENT=1` / `MASTER_UNTRACKED_FAILED=1` | 止まらず進む。**マスタデータが報告時と違う**（レシピ・ブロック定義が別物でありうる）ため、`not_reproduced` の判定はこれを踏まえ、`summary` と PR 本文に必ず書く |
| `$RUN/repo/bundle-status.txt` に `failed-*` がある | 報告者のローカルコミットが受け側に無い。`COMMIT_MISSING=1` と同じ扱いで `summary` と PR 本文に書く |
| バンドルの欠損（動画・スナップショット・パケットログ） | 残った資料で進める。欠損項目を `summary` に書く |
| Step 3 の観察で症状が出ない | 追加シナリオを最大3本試し、それでも出なければ `not_reproduced` |
| `$WORLD_DIR/save.json` が無い（スナップショット欠損の箱） | Step 3 を飛ばし、ログ・パケット・スクショだけで Step 4 へ。飛ばした理由を `summary` に書く |
| `SERVER_DATA_DIR` が空（manifest に `serverData` が無い/解決できない箱） | Step 2 を飛ばし、理由を `summary` に書いて Step 3 へ。`MASTER_DIR` で代用しない（別マスタでの再生は偽の結果になる） |
| `replay-check` が `ERROR: 渡されたサーバーデータが記録時のものと違います` を返す | 渡すディレクトリを間違えている。`SERVER_DATA_DIR` を渡し直す（それでも解決しない箱は Step 2 を飛ばして理由を `summary` に書く） |
| 修正案が複数あって優劣が付かない | 前例に最も近い案を選び、他案を PR 本文の「裁定事項」に列挙して進む |
| 期待挙動が仕様として存在しない | コードを触らず `needs_ruling` |
| Editor が起動しない・master data が壊れている | `failure`（環境要因）。原因を `summary` に書く |

## Step 1: 読む

1. `$RUN/manifest.json`（説明文・`snapshotTicks`・`missing`・`clientState`・`repository`）と `$RUN/run.env` のフラグ・`$RUN/repo/bundle-status.txt` を読む。上の既定表のフラグが1つでも立っていたら、再現環境が報告時と違うことを summary に必ず書く
2. `$RUN/logs/unity.log` の Error/Exception 行、`$RUN/frames/`（2fps の連番。Read で数枚見る）、`$RUN/screenshot.png`
3. パケットログを可読化: `bash .agents/skills/bug-report-auto-fix/scripts/run-edc.sh $WORKTREE/moorestech_client .agents/skills/bug-report-auto-fix/scripts/edc/dump-packets.cs $RUN`（Editor が未起動なら先に `uloop launch $WORKTREE/moorestech_client`）。`$RUN/packets.jsonl` の末尾（報告直前の操作）を読む
4. `bd create "bug-report <run-id>: <説明文の要約>" --type=bug --priority=2 --description="<manifest要約と $RUN パス>"` で追跡 issue を作り、続けて `bd update` の `--claim` で着手する（claim は素のコマンド単体で打つ。パイプ・リダイレクト・複数コマンドの混在は hook に拒否される）

## Step 2: 決定性検査（必須・最初に）

`SERVER_DATA_DIR` が空なら決定性検査は成立しない（記録時のマスタが特定できない）。その場合は飛ばし、理由を `summary` に書いて Step 3 へ。

`bash .agents/skills/bug-report-auto-fix/scripts/run-edc.sh $WORKTREE/moorestech_client .agents/skills/bug-report-auto-fix/scripts/edc/replay-check.cs $RUN $SERVER_DATA_DIR`
→ `$RUN/replay-check.json`。`allEqual=false` なら **発散した DataStore の是正を先に行う**（ADR 0057）。差分パスが指す箇所の非決定性（列挙順・未シード乱数・未保存の過渡状態）を直し、再検査で `allEqual=true` にしてから Step 3 へ。是正はバグ修正と同じ PR に含め、summary に書く。

是正しきれない場合も止まらない。`determinism: "diverged"` として残し、発散した DataStore を `summary` に書いたうえで Step 3 へ進む（再現結果の信頼度が落ちることを PR 本文にも書く）。

## Step 3: 観察（スナップショットからのプレイテスト）

**先に前提を確かめる。** `LATEST_TICK` が空、または `$WORLD_DIR/save.json` が無い箱では観察は成立しない。
`run-scenario.sh` はこの状態を検査せず PlayMode を起動し、**300秒待って `NG: game not ready` を出したうえ終了コード 0 を返し、
Editor を PlayMode に置き去りにする**（2026-09-12 リハーサルで実測）。該当する箱は Step 3 を飛ばし、
「観察できない理由（世界データ欠損）」を `summary` に書いて Step 4 へ進む。飛ばしたこと自体も必ずログに残す。

```bash
[ -s "$WORLD_DIR/save.json" ] || echo "観察を飛ばす: $WORLD_DIR/save.json が無い（スナップショット欠損の箱）"
```

観察を実行する場合:

```bash
sed "s|__BUNDLE__|$RUN|g" .agents/skills/bug-report-auto-fix/scripts/scenarios/bug-report-observe.cs > $RUN/observe.cs
uloop control-play-mode --project-path $WORKTREE/moorestech_client --action stop
PLAYTEST_WORLD_DIRECTORY=$WORLD_DIR PLAYTEST_MAP_MODE=template PLAYTEST_SEED=0 \
  .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh $WORKTREE/moorestech_client $RUN/observe.cs ${SERVER_DATA_DIR:-$MASTER_DIR}
```
サーバーデータは記録時と同じ `SERVER_DATA_DIR` を渡す。空で `MASTER_DIR` に落ちた場合は、記録時と別のマスタで観察している旨を `summary` に書く。
（`world.json` の `mapMode` が `generated` なら `PLAYTEST_MAP_MODE=generated PLAYTEST_SEED=<world.jsonのseed>`）
`run-scenario.sh` は失敗しても終了コード 0 を返すので、**成否は `result.json` の有無と `Success` で判定する**（終了コードでは判定しない）。
`NG: game not ready within 300s` が出たら Editor が PlayMode のまま残っているので、
`uloop control-play-mode --project-path $WORKTREE/moorestech_client --action Stop` で必ず戻す。
結果ディレクトリを `$RUN/observe/` へコピーする。説明文の症状が録画・スクショ・`ErrorLogs` に現れるかを判定する。現れなければ、説明文の操作を DSL で再現する追加シナリオを最大3本まで書いて試す（`references/write-scenario.md`）。

3本試しても症状が出なければ Step 9 へ飛び、`not_reproduced` で終える（試したこと全部を `summary`、次に試す案を `remaining` に書く）。

## Step 4: 原因特定

`debug-workflow` スキルを起動する。症状＝説明文＋観察結果、既知の試行＝Step 3、尊重すべき制約＝AGENTS.md。ログ仕込みは `$WORKTREE` 内で行い、Step 3 のシナリオで観察する。

原因が「期待挙動そのものが決まっていない」に行き着いたら、そこで実装へ進まず Step 9 の `needs_ruling` へ抜ける。

## Step 5: 修正と合成NUnit

1. 修正は `$WORKTREE`（ブランチ `bugfix/<run-id>`）で行う
2. **合成NUnit が再現テストの正、バンドル依存のプレイテストは証拠扱い**（裁定 2026-09-11）。`creating-server-tests` に従い、
   バンドルのワールドファイル・スナップショットに一切依存しない最小の再現テストを書く。
   **修正前に落ち、修正後に通る**ことを `uloop run-tests --test-mode EditMode` で両方とも実測し、出力を報告に残す。
   最小化できない場合はプレイテストシナリオと録画を `$RUN/observe/` に残し、PR 本文に「プレイテスト証拠のみ」と明記する
3. `uloop compile` を通す

**テストを緩めて緑にすることは禁止**。落ちる合成NUnit を通すのは修正であって、期待値の書き換え・Assert の削除・
`Ignore` 属性・許容誤差の拡大ではない。既存テストが赤くなったら、それは修正が壊した回帰であり直す対象である。

**バンドルの中身（スナップショット JSON・動画・フレーム・ログ本文）をテストのフィクスチャとしてコードrepoへコミットしない**
（裁定 2026-09-11。バンドルは moorestech_logs 側に置いたまま参照する）。

## Step 6: 修正先の判定（裁定 2026-09-11）

裁定は「**再現は必ず報告時のコミット＋未コミット差分で行い、同じ再現テストが最新 master でも落ちれば master 基底、
master で落ちなければ報告ブランチ基底（PR は報告ブランチ宛て）**」である。合成NUnit を作ったら、その1コミットを
`origin/master` 基底でも実行して判定する:

```bash
git -C $WORKTREE fetch origin master
git -C $WORKTREE branch bugfix/<run-id>-master origin/master
git -C $WORKTREE checkout bugfix/<run-id>-master
git -C $WORKTREE cherry-pick <合成NUnitのコミット>
```
`uloop compile` の後にテストを実行する（`--test-mode EditMode`）。

- master でも落ちる → 修正の土台は `bugfix/<run-id>-master`（master 基底）。修正コミットをそこへ cherry-pick / 適用し、PR は `master` 宛て。`base="master"`
- master では落ちない → 土台は `bugfix/<run-id>`。PR は `REPORT_BRANCH` 宛て（`COMMIT_MISSING=1` なら master 宛てにし summary に書く）。`base="<REPORT_BRANCH>"`
- cherry-pick がコンフリクトする → master 側でテストを成立させられないということなので、`git cherry-pick --abort` して
  報告ブランチ基底を選ぶ。判定不能だった事実を `summary` に書く

判定が済んだら `git checkout` で選んだ土台のブランチに戻り、以降の修正・レビュー・push をそこで行う。

## Step 7: 修正後の証拠

Step 3 のシナリオを修正後のバイナリで再実行し `$RUN/observe-after/` に残す。症状が消えたことを録画・スクショで確認する（レビュー後に修正が変わったら再実施）。

## Step 8: レビューと PR

1. `moores-code-review` を実行し、機械的指摘を反映する。設計判断が要る指摘は PR 本文の「裁定事項」に列挙する（AskUserQuestion は使えない）
2. `git push -u origin <ブランチ>` → `pr-create` スキルで PR を作る（本文: 説明文の引用・原因・修正・合成NUnit・`$RUN` のパス・欠損項目・決定性是正の有無・裁定事項）。**動画やスナップショットは添付しない**（裁定: 公開PRにはパスと説明だけ）
3. `gh pr ready --undo <番号>` で draft にする
4. `bd note <id> "PR #<番号> ..."`

**公開 PR に載せてよいのは「説明とパス」だけ**（裁定 2026-09-11）。バンドルは private の moorestech_logs 側にあり、
本人とエージェントだけが閲覧する。PR 本文・コミットメッセージ・テストコードに次を持ち込まない:

- 動画・フレーム・スクリーンショットの添付（修正後のプレイ録画も同じ扱い）
- スナップショット JSON・パケットログ本文の貼り付け
- 報告者のローカルパス・ワールドの中身

代わりに `moorestech_logs/harness/bug-report/runs/<run-id>/` のパスと、再現手順の説明を書く。

## Step 9: 結果を書いて終える

`$RUN/fix-result.json` を `references/fix-result.md` の契約で書く。`status` は `fixed` / `not_reproduced` / `needs_ruling` / `failure`。`needs_ruling` の場合は `bd create` で裁定事項を積み（`--type=task --priority=1`、本文に候補と帰結）、その id を `remaining` に書く。書いた直後に終了する。

## Unity のつまずき（無人で詰まりやすい順）

- **`uloop` が `UNITY_NOT_REACHABLE` を返す第一原因はコンパイルエラー**。Editor は生きているのに CLI サーバーが立たない状態で、
  繋がるまで待つ戦略は永久に終わらない。**唯一の観測点は Editor.log** なので、まず
  `tail -n 200 ~/Library/Logs/Unity/Editor.log`（この worktree の Editor のもの）でエラーを読み、原因の `.cs` を直す。
  直前に自分が書いた `.cs` が原因であることが最も多い。人の操作を待つ必要は無い
- **新規のサーバー側 `.cs` を足したら Unity 再起動が要る**: `uloop launch $WORKTREE/moorestech_client --restart`。
  file: パッケージ参照のため Refresh では検出されない（既存ファイルの編集だけなら再起動は不要）
- 「Unity is reloading (Domain Reload in progress)」は 45 秒待って再試行する
- `--test-mode` は**必ず明示する**。EditMode のつもりで PlayMode に入ると Editor が固着し、以後の uloop が全て詰まる。
  固着したら `uloop control-play-mode --project-path $WORKTREE/moorestech_client --action stop` で解除する
- `TestResults.xml` はマシン共通パスで他 worktree と競合する。**uloop が返した結果を正とする**

## 禁止事項

- **AskUserQuestion の使用禁止**（hook が deny する。判断は既定表に従い `summary` へ書いて進める）
- **テストを緩めて緑にすること禁止**（期待値の書き換え・Assert 削除・`Ignore`・許容誤差の拡大）
- **マージ禁止・非 draft の PR 禁止**（PR は必ず `gh pr ready --undo` で draft にする）
- **報告されたバグ以外の修正禁止**（Step 2 の決定性是正だけが例外。他は bd へ積む）
- **バンドルの中身を公開 repo へ持ち込むこと禁止**（PR 添付・テストフィクスチャ・コミットのいずれも）
- **`$WORKTREE` の外での編集・コミット禁止**。`git add` は必ずパス指定（`git add -A` / `git add .` は禁止）
