---
name: moores-playtest-release
description: moorestech のプレイテスト配布を通しで行う。最新 origin/master の Windows/Mac ビルド → Steam playtest-staging へアップロード（Steam Guard 対応）→ Windows 検証機の通し検証 → 配信中ビルドとの差分からパッチノート → playtest ブランチへ反映。Use When — 「最新プレイテストビルドして」「プレイテスト版を更新して」「playtest版に反映して」「プレイテストのパッチノート作って」「steamcmd のログインが切れる」と言われた場合。
---

# moores-playtest-release

工程の正本は `scripts/playtest/README.md` と `scripts/playtest/release-playtest.sh`。このスキルは、標準スクリプトが止まる Steam 認証、反映、パッチノートを補う。パッチノートだけを頼まれたら手順 3 だけを行う。スクリプトのパスは本体 clone 直下からの相対で書いている（`.agents/skills/moores-playtest-release/scripts/`）。

## 前提条件

- Mac mini の本体 clone（`~/hermes-agent/data/repos/moorestech`）から実行する。worktree はスクリプトが作る。
- `~/hermes-agent/data/services/playtest/env.sh` に次の変数がある。README に載っている変数と `MOORESTECH_STEAM_OP_HELPER`（1Password ラッパーのパス）、`MOORESTECH_STEAM_OP_ITEM`（Steam 資格の項目 ID）。このファイルは並列セッションと共有しているので、`>` で作り直さず追記する。
- 成果物は `~/hermes-agent/data/services/playtest/runs/<label>/` に置く。label は `playtest-YYYYMMDD-HHMM`。

## 手順

### 1. ビルド（約70分）

先に `MOORESTECH_STEAM_STATE_ROOT`（専用永続ディレクトリ）を env.sh に設定する。認証は共通ランナー `scripts/playtest/steam_auth/run.py` の専用HOMEに保存され、Desktop Steamから分離される。

初回は `python3 .agents/skills/moores-playtest-release/scripts/steam_upload_with_vault.py <認証状態の保存先> --login-only` で認証する。続けて `--check-login` を実行し、Desktop Steam起動後ももう一度成功することを確認する。`--check-login` は保存資格だけを使い、1Passwordを読まない。通常リリースもビルド前に同じ検査を実行し、失敗時はビルドを開始しない。

```bash
cd ~/hermes-agent/data/repos/moorestech && git fetch -q origin
. ~/hermes-agent/data/services/playtest/env.sh
export MOORESTECH_STEAM_BUILD_LABEL=playtest-$(date +%Y%m%d-%H%M)
RUNS=~/hermes-agent/data/services/playtest/runs
scripts/playtest/release-playtest.sh "$(git rev-parse origin/master)" > $RUNS/$MOORESTECH_STEAM_BUILD_LABEL.release.log 2>&1   # Bash の run_in_background で起動
```

- 渡すのは SHA か `origin/master`。ローカルの `master` は渡さない（fetch で進まないので古いコミットを焼く）。
- 待つ間は Monitor でログを見る。拾う語は `ERROR|FAIL|Invalid|credentials|phase|passed|EXIT=` で、成功だけでなく失敗も拾う。
- steamcmd・検証まで通って `promotion.md` が出たら、手順 2 を飛ばして手順 3 へ進む。

### 2. アップロード（steamcmd が `Invalid Password` で止まったとき）

`release-playtest.sh` はビルド前に保存資格を確認する。ビルド中に資格が失効すると、完了後のログインで落ちることがある。ログに `Cached credentials not found` → `ERROR (Invalid Password)` と出た場合がそれで、promotion.md は作られず、worktree も削除済みで終わっている。成果物は run-dir に残っているので、再ビルドはしない。代わりに次を Bash の run_in_background で起動する。

```bash
.agents/skills/moores-playtest-release/scripts/upload_and_verify.sh $RUNS/<label> > $RUNS/<label>.upload-verify.log 2>&1   # run_in_background
```

起動と同時に、Monitor で `tail -F` したログを見張る。拾う語は `steam_guard_required|uploaded|failed|rejected|FAILED|VERIFY_EXIT|LOCK_FAILED|passed`。完了通知だけを待つと、Steam Guard の承認期限に間に合わない。アップロード済みで検証だけをやり直すときは、第 2 引数に `--skip-upload` を付ける。

1. 状態は JSON で 1 行ずつ出る。`steam_guard_required` が出たら、**その場で**ユーザーへ「スマホの Steam アプリで承認して」と伝える。
2. 承認が来ないと、steamcmd は約 1 分で exit 5 になる。承認が取れる状態になったことをユーザーに確かめてから再実行する。
3. 承認の代わりにコードが届いた場合は、`<run-dir>/steam-guard-code.txt` に書けば送信される。
4. `uploaded` のあとは自動で進む。staging の検証（`validated-steam-build.json` を書く）、ingest ロックの取得、Windows の phase1・phase2 と受け口到達の確認、`promotion.md` の書き出しまで行う。

### 3. パッチノート

1. 比較元は、いまテスターに配信中のビルド。`.agents/skills/moores-playtest-release/scripts/find_live_playtest.py` で、配信中のビルド ID とその run・コミットを得る。
2. `git log --merges <比較元>..<今回>` で PR を列挙する。対象を絞るときは `--first-parent` を使わない（比較元がブランチ上のコミットだと範囲がずれる）。`gh pr view` で中身を読む。
3. 次の PR は載せない。比較元にすでに入っている PR、文書・スキル・テストだけの PR。`.moorestech-external-revisions.json` の master ピンの差分も確かめる（マスタ変更はレシピ・数値の変更として載せる）。
4. `<run-dir>/patch-notes.md` を書く。プレイヤーへの影響が大きい順に「1. **題**」と本文を並べ、プレイヤーの言葉で書く（クラス名・PR 番号は書かない）。形は過去の run の `patch-notes.md` を手本にする。
5. HTML にして共有する。

```bash
python3 .agents/skills/moores-playtest-release/scripts/render_patch_notes.py <run-dir>/patch-notes.md "<状態文>" > <run-dir>/patch-notes.html
share-put -n <日付>-moorestech-playtest-patch-notes <run-dir>/patch-notes.html
```

状態文は、反映前なら「staging へアップロード済み」、反映後なら「playtest へ配信済み（Build ID N）」と書く。反映したら HTML を作り直して同じ名前で share-put し直す。

### 4. playtest への反映（ユーザーが反映を指示したときだけ）

```bash
python3 .agents/skills/moores-playtest-release/scripts/promote_playtest.py <run-dir> --mac-checked
python3 .agents/skills/moores-playtest-release/scripts/promote_playtest.py <run-dir> --skip-mac-check "<ユーザー指示の引用>"
```

- Mac 版の手動確認がまだなのにユーザーが反映を指示したときだけ、後者を使う。その場合は「Mac は未確認」と報告する。
- スクリプトは次の 2 点を確かめてから `SetAppBuildLive` を呼び、読み戻しで一致を確かめて `released.json` を書く。ログに `[verify] passed` があること。コミットは release.log の `commit=` から読む。`released.json` は次回のパッチノートで比較元になる。
- Steamworks Web UI での手作業は不要。

## Gotchas

- SteamCMDの通常配布と認証ヘルパーは共通の隔離ランナーを使う。実HOMEやDesktop設定へのsymlinkを指定しない。同じstate rootで並行実行すると理由を示して停止する。Desktop起動後に失敗したら、保存先と実行経路を照合し、実測せず「毎回承認が必要」と説明しない。
- Steam Guard の承認待ちは約 1 分しかない。バックグラウンドで黙って待つと承認が間に合わず、2 回失敗する。プロンプトを検知したらすぐ伝える。
- 検証機の通し検証中は、検証用の報告を本番の取り込みに拾わせないこと。README は `services.json` から playtest-ingest を外す方法を書いているが、このスキルではその代わりに、`upload_and_verify.sh` で `ingest.sh` と同じロックを握る。`ingest.sh` は受け口に触れる前にロックを取れなければ終わるので、これで防げる。共有設定の書き換えも supervisor の再起動も要らない。
- `release-playtest.sh` は終了時に `moores-wt rm` を呼ぶが、未登録の残骸ディレクトリ（`worktrees/moorestech/build-<sha8>`）が残ることがある。残っていても報告だけにする。
- 同梱する master data の元は、pin と一致する detached worktree（`moorestech_master/pin-*`）。長時間ビルドの途中で同梱元の HEAD が進むと、Mac ビルドが BuildInfoWriter の strict 検査で落ちる。そのときは pin に合わせて Mac ビルドだけやり直す。

## Available scripts

- `scripts/upload_and_verify.sh <run-dir> [--skip-upload]` — アップロード、staging 検証、ingest ロック、Windows 検証、promotion.md の書き出しを順に行う
- `scripts/steam_upload_with_vault.py <run-dir> [--login-only|--check-login]` — 1Password の資格で steamcmd にログインして VDF を上げる（Steam Guard 対応）
- `scripts/validate_staging.py <run-dir>` — staging の Win/Mac depot が今回アップロードしたビルドと一致するか確かめる
- `scripts/find_live_playtest.py [runs-dir]` — 配信中の playtest ビルドと、その run・コミットを返す
- `scripts/promote_playtest.py <run-dir> --mac-checked|--skip-mac-check "<引用>"` — playtest へ反映し、読み戻しで確認する
- `scripts/render_patch_notes.py <md> <状態文>` — パッチノートの md を共有用 HTML にする
