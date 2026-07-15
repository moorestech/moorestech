---
name: tart-vm-moorestech-dev
description: Tart macOS VM 内の moorestech worktree で、開発者不在の無人前提で Unity/C# 開発を進めるための運用スキル。Use When — ユーザーが「tart vmモードで起動して」と発言した時に発動する。
---

# Tart VM 専用 moorestech 無人開発スキル

このスキルは **Tart macOS VM の中で、開発者が画面の前にいない状態** で moorestech の Unity/C# 開発を進めるための運用ルール。汎用スキルではない。環境が一致しないなら使わない。

## 0. 発動前チェック（厳格・全一致が必須）

以下を実際にコマンドで確認し、**すべて一致した場合のみ** このスキルの運用ルールを適用する。1つでも外れたら適用を中止し、通常の判断に戻る。

```bash
pwd                              # → /Users/admin/dev-agent/worktrees/moorestech* 配下であること
hostname                         # → Manageds-Virtual-Machine.local
uname -a                         # → Darwin ... VMAPPLE ... arm64 を含む
sysctl -n hw.model               # → VirtualMac2,1
ls /Applications/Unity/Hub/Editor # → 6000.3.8f1 が存在する
```

判定ルール:
- **作業ディレクトリが `/Users/admin/dev-agent` 配下でない** → 発動しない。
- **hostname が `Manageds-Virtual-Machine.local` でない、または `uname` に `VMAPPLE` が無い** → Tart VM ではない。発動しない。
- **対象が moorestech repo/worktree でない**（別 Unity プロジェクト、別リポジトリ） → 発動しない。
- 迷ったら発動しない方を選ぶ。誤発動は通常ローカルの開発フローを壊す。

## 1. 無人前提（最重要）

開発者は基本的に目の前にいない。**Unity Editor の手動操作をユーザーに要求しない。**

- 「Unity でこのボタンを押してください」「Play を押して確認して」「Inspector で値を入れて」は禁止。
- Unity 操作はすべて CLI 経由（`uloop` / `uloop-*` skill）で自分で行う。手動操作が前提のメニュー/プレハブ配線も `uloop execute-dynamic-code` / `uloop execute-menu-item` で実施する（`uloop-execute-dynamic-code`, `uloop-execute-menu-item` skill）。
- どうしても人手でしか進められない箇所に到達したら、勝手に放置せず **final report / 報告に「ここはユーザー操作が必要」と明示** して止める。手動操作をユーザーに丸投げして完了扱いにしない。
- `.meta` ファイルを手で作らない。Prefab/Scene/ScriptableObject を直接テキスト編集しない（AGENTS.md 準拠。変更は `uloop execute-dynamic-code` 経由）。

## 2. クライアント / UI 変更は evidence を残す（必須）

クライアントや UI など **見た目・挙動が伴う実装** は、ユーザーが後から差分を目で確認できる形にする。コードだけ出して「直しました」で終わらせない。

優先順位:
1. **録画**: `unity-playmode-recorded-playtest` skill を使う。第一選択はプレイテストDSL（同 skill 同梱の `scripts/run-scenario.sh` + `PlaytestRunOptions{Record=true}`）の1コマンド一発実行で、preflight〜PlayMode起動〜録画〜result.json回収まで自動化される。DSLが無いブランチのみ手動フロー（PlayMode を CLI 起動して Unity Recorder 制御）。フォーカス不要なので無人で回せる。入力は InputSystem QueueStateEvent で注入する（OS の simulate-keyboard / simulate-mouse は使わない）。
2. **スクリーンショット**: 録画が過剰な小さな UI 変更は `uloop-screenshot` skill で Game View / 該当 EditorWindow を PNG 保存する。
3. ランタイム挙動の不具合調査は `unity-runtime-bug-hunt`、PlayMode 統合テストは `editmode-in-playing-test` skill を併用する。

evidence の出力パス（録画ファイル / PNG）は最終報告に必ず列挙し、何を確認できるかを 1 行で添える。evidence を残せなかった場合は、その理由を報告に明記する。evidence ファイルはローカルパス列挙に加えて、第 7 節のビューワー公開フローで必ず閲覧 URL も添えること。

## 3. 実装フロー（無人で完結させる）

1. AGENTS.md / CLAUDE.md の規約を守る（QA は「バグがある前提」で探す、1 ファイル 200 行以下、partial 禁止、try-catch 基本禁止、`#region Internal` はローカル関数用途のみ 等）。
2. `.cs` を変更したら **必ずコンパイル**: `uloop compile --project-path ./moorestech_client`（`uloop-compile` skill）。
3. テストは regex で対象限定: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`（`uloop-run-tests` skill）。サーバー側テストもこのクライアント project-path で同時に走る。
4. エラー確認: `uloop get-logs --project-path ./moorestech_client --log-type Error`。
5. Domain Reload 中の「Unity is reloading」エラーが出たら 45 秒待って再試行（AGENTS.md 準拠）。
6. マスタデータ/スキーマを触るなら `edit-schema` → `validate-schema` skill を経由する。

## 4. 実装終了後 all-code-review を必ず 1 回実行（必須）

実装が一段落したら、コミット前に **最低 1 回はコードレビューを実行する**。

> **重要**: `all-code-review` は**グローバルのユーザースキルとして実在する**（`~/.claude/skills/all-code-review/`、`~/.agents`・`~/.codex` にもミラー）。repo 内には無いので `grep` を repo 配下だけで打つと 0 件になり「無い」と誤認する（過去に実際に誤認した）。`reviewers/` に1観点1ファイルのレビューア(subagent)を持ち、Codex 外部監査と並列実行する実運用レビュー。
> 注意: 導入済みでも harness の available-skills（system-reminder）は関連度でフィルタされるため、`all-code-review` が自動提示されないことがある。その場合も**存在するので Skill ツールで名前指定して直接起動する**（`find-skills` skill で発見も可）。
> 使い分け: 一括の多エージェント＋外部監査レビューは **`all-code-review`** を第一選択。ビルトイン `/code-review`（`high`〜`max`、クラウド版 `ultra`）は軽量/フォールバック。

> **エビデンス検証の同時発火（必須）**: このコードレビューを起動するのと**同じタイミングで**、`agents/evidence-verification-subagent.md` を読ませたエビデンス検証 subagent を**必ず並列で起動する**（第 2 節で作った録画/スクリーンショット/配信URLと、それについて自分が主張する挙動を渡す）。evidence が1件でもあるなら省略不可。詳細な検証手順はそのドキュメントが内包するのでここには展開しない。狙いは「中身を見ずにエビデンスと言い張る」事故の防止で、検証は必ずこの subagent 側で行う。

手順:
1. 変更を `git diff` / `git status` で確認する。
2. **`all-code-review` skill を起動する**（Skill ツールで名前指定。自動提示されなくても実在する）。`codex` 不在などで使えない時のみビルトイン `/code-review`（`high`〜`max`／`ultra`）にフォールバックする。**同時に上記のエビデンス検証 subagent も並列起動する。**
3. **必ず repo の `code-review-perspectives.md` を読み、記載された各観点を独立した finder 角度として明示的に適用する**（`/code-review` はこのファイルを自動ロードしないため、読まないと弱い指摘に留まる。検証済み）。現行観点: ①ドメイン型優先 ②導出値でなく根本原因でガード ③呼び出し元のコード最小化・判定のサービス集約。合致する変更には該当観点番号を指摘に明記する。
4. 指摘が出たら対応または「対応しない理由」を残し、再度コンパイル/テストで確認する。**エビデンス検証 subagent が Critical を返したら、そのエビデンスを提示・報告する前に撮り直し／作り直しで解消する**（未実証のまま「エビデンスです」と出さない）。
5. レビューと**エビデンス検証**を実行した事実と結果サマリを最終報告に必ず書く。

skill を探す時は repo 配下 grep だけで判断せず、`~/.claude/skills`・`~/.agents/skills` のグローバル領域も確認する（`find-skills` skill を使う）。

## 5. 終了時

- 全作業を **task branch にコミット**する（作業消失防止。AGENTS.md 準拠）。
- `master` への直 push / merge / deploy / release はしない。worktree cleanup もしない。
- 最終報告に「変更ファイル」「実行した検証コマンドと結果」「all-code-review 結果」「evidence パス」「残課題」を含める。

## 6. 終了時の差分公開（difit + Cloudflare Tunnel・必須）

無人前提なので、終了時は **差分を difit で起動し Cloudflare Tunnel で外部公開** し、開発者が手元のブラウザからレビューできる URL を最終報告に載せる。difit の起動・差分範囲指定・常駐管理は **`difit-diff-viewer` skill を使用する**。

手順:
1. ブランチの差分を difit で起動（ブラウザ自動起動はしない・常駐させる）:
   ```bash
   npx difit HEAD master --merge-base --port <PORT> --no-open --background --keep-alive
   ```
   **このコマンドは必ず harness の `run_in_background: true` で起動する。** `--background` は difit の挙動フラグで `npx` ラッパーは戻らないため、フォアグラウンド実行すると difit は立つのにセッションが固まる（過去に約20分スタックした。詳細は `difit-diff-viewer` skill の Gotchas）。起動後は `sleep` せず出力ファイルか `curl localhost:<PORT>` で応答確認。
2. その port を Cloudflare Tunnel で公開（認証不要の quick tunnel）:
   ```bash
   cloudflared tunnel --url http://localhost:<PORT>
   ```
   **これも常駐プロセスなので `run_in_background: true` で起動する。** 出力される `https://<ランダム>.trycloudflare.com` が公開 URL（出力ファイルを読んで取得）。
3. 公開 URL を最終報告に必ず記載する。difit / cloudflared プロセスは開発者が見終わるまで常駐させたままにする（勝手に kill しない）。

> **VM 内 DNS は `*.trycloudflare.com` を解決できない（誤検知に注意）。** トンネル起動直後に VM 内から `curl https://<host>.trycloudflare.com` すると `000`／exit 6（Couldn't resolve host）になるが、これは VM のリゾルバの問題であってトンネルは生きている（cloudflared ログに `Registered tunnel connection ... location=...` が出ていれば OK）。**外部ブラウザ（ユーザー側）は公開 DNS で解決でき到達する。** VM 内から到達確認したいときは公開 DNS で名前解決してから叩く:
> ```bash
> IP=$(nslookup <host>.trycloudflare.com 1.1.1.1 | awk '/^Address: /{print $2; exit}')
> curl -s -o /dev/null -w '%{http_code}\n' --resolve <host>.trycloudflare.com:443:$IP https://<host>.trycloudflare.com/
> ```
> 内部 `000` を見てトンネルが壊れたと誤判断しないこと。ローカル difit（`curl localhost:<PORT>` が 200）とエッジ登録ログの2点で生存確認する。

前提ツール（この VM では導入済み。未導入環境では先に入れる）:
- `difit` は `npx difit`（グローバル導入不要）。
- `cloudflared` は `brew install cloudflared`。

> difit の詳細手順（差分範囲指定・バックグラウンド常駐・ポート確認・Gotchas）は `difit-diff-viewer` skill に集約。本節はそれに Cloudflare Tunnel 公開を重ねた終了時の運用ルール。

## 7. 動画・画像 evidence の公開（evidence ビューワー + Cloudflare Tunnel・必須）

無人前提なので、第 2 節で動画・スクリーンショットを evidence として作成したら、**常に evidence ビューワーで配信し Cloudflare Tunnel で外部公開** し、開発者が手元のブラウザから閲覧できる URL を最終報告のファイルパス列挙と並べて必ず載せる。ローカルパスを書くだけで終わらせない。

ビューワーは本スキル自身のディレクトリ（`.claude/skills/tart-vm-moorestech-dev/evidence-viewer/`）に同梱されている。`--dir` 配下を再帰走査し、動画 `.webm` / `.mp4` は Range 対応ストリーミング、画像はギャラリー表示する（詳細はそのディレクトリの README）。

手順:
1. 初回のみ evidence-viewer ディレクトリでセットアップ（`pnpm install && pnpm build`）→ ビューワーを起動して evidence ディレクトリを配信 → その port を Cloudflare Tunnel で公開:
   ```bash
   # 初回のみ（evidence-viewer ディレクトリで実行）
   cd .claude/skills/tart-vm-moorestech-dev/evidence-viewer && pnpm install && pnpm build

   # 配信（evidence-viewer ディレクトリで実行。--dir は evidence の絶対パス）
   node server.mjs --dir <worktreeの絶対パス>/docs/evidence/<date>-<task>/ --port <PORT>

   # 公開（認証不要の quick tunnel）
   cloudflared tunnel --url http://localhost:<PORT>
   ```
   出力される `https://<ランダム>.trycloudflare.com` が公開 URL。
2. 公開 URL を最終報告に必ず記載する。`--port` は第 6 節の difit と同時起動する場合にポートが衝突しない値を選ぶ。
3. server.mjs / cloudflared プロセスは開発者が見終わるまで常駐させたままにする（第 6 節の difit と同様に勝手に kill しない）。
4. **URL の直後には必ず半角スペースを1つ入れる**（第 6 節の difit URL 含む報告中の全 URL 共通）。Discord relay 等のリンク自動検出で隣接する句読点・括弧が URL に巻き込まれてリンクが壊れるため。
