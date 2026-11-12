# UITK X-switch-pr（切替 PR: 追従の記帳・全束の切替・G4・CEF の無いプロジェクトでラダー 6 本と全録画・配布ビルドと検証機・遠隔実行・Mac の Steam・IME の一巡・master への PR）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 撤去 5a〜5e が `feature/uitk` に入った後、`feature/uitk` 上の Web を触った全コミットを台帳へ記帳して全束を `切替済み`・全行を `UITK` にし、G1 と G4 を 0 件にし、CEF パッケージの無い HEAD の Editor でラダー 6 本と全録画を通し、その HEAD の SHA から Windows/Mac の配布ビルドを焼いて CEF のファイルが 0 であること・Windows 検証機の通し検証・遠隔実行の 3 点・Mac の Steam での起動・IME の一巡を確かめ、`feature/uitk` から master への切替 PR を作って G1・G4 を緑にし、ユーザーの目視とマージの準備を整える（マージはしない）。

**Architecture:** integrator のレーン（`int`）。新しく書くコードは無い。このレーンが書くのは台帳 `docs/uitk/TODO.md` の「## 束」「## 行」「## 追従記録」と、切替 PR の本文と、検証の記録（`../moorestech_logs/harness/uitk/X-switch-pr/done.md`・PR の報告ページ）だけ。検証は既存の道具をそのまま使う（G1 `check-uitk-web-follow.py`・X-5e の G4・`run-scenario.sh` と全録画のスクリプト・`release-playtest.sh`・`verify-on-windows.sh`・`remote-exec.sh`・moores-playtest-release の手順 4）。コミットは中継ブランチを作らず `feature/uitk` に直接積み、切替 PR の G1 がその全部を検査する。取り込みの衝突の解決とレビューの自動適用だけはコードに触れうる（どちらも判断記録 D-6・D-11）。

**Tech Stack:** Python（G1・G4）、bash（`scripts/playtest/*`）、Unity 6（Mac の Editor 1 本・batchmode の配布ビルド）、プレイテスト DSL（Editor 専用）、`Client.RemoteExec`（配布ビルドの遠隔実行）、Steam（steamcmd・Steam Desktop）、Windows 検証機（ssh・scheduled task・`SendInput`）、Mac（osascript・cua-driver）、bd、gh。

設計の出所: 実行計画 `docs/uitk-migration/execution/execution-plan.md` §1.5 の X-switch-pr 行（完了条件の原文:「`feature/uitk` 上の Web コミットを `捨てた` で記帳し `check-uitk-web-follow.py --base origin/master --head feature/uitk` 0 件。全束 `切替済み`・全行 `UITK`。G4 緑。Windows/Mac ビルド・Windows 検証機・Mac の Steam・`test-release-playtest-mac.sh`、CEF の無いビルドでラダー 6 本と全録画、IME 一巡。焼くのは `feature/uitk` の HEAD の SHA（`release-playtest.sh` の既定の `origin/master` を使わない）。playtest-staging へのアップロードは裁定の『切替 PR の中の検証』の範囲として行い、playtest への反映（moores-playtest-release の手順 3 以降）はユーザーのマージ後の X-release でだけ行う。pr-create（head=`feature/uitk`、base=master）・pr-independent-review。目視とマージはユーザー。検証中に master の Web 更新が入ったら I-follow で写して再検証」）・§4.3（末尾「X-switch-pr は master への PR で、pr-create まで」）・§5・§7 の U-4、統合計画 `docs/uitk-migration/plan.md` §2.6（切替 PR の作り方）・§4.4（ラダー）・§4.7（G1 の走り方・G4）・§4.9（IME と実機）・§5.1（完了の定義）・§7.4、裁定 `.decisions/2026-11-06-*.md` の 7 本、配布の手順 `.agents/skills/moores-playtest-release/SKILL.md`・`scripts/playtest/README.md`、ADR `docs/adr/0072-remote-exec-in-distributed-builds.md`（完了条件 3 点）と前例 `docs/research/2026-10-26-remote-exec-own-listener-windows.md`、IME の前例 `~/share/2026-10-28-uitk-s9-ime-result/s9-ime-win-report.md`（§2〜§4）と `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-c2b-ime-mac-keys.sh`。

## Requirements

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 前提がそろっている | Task 1 で次を全部確かめて `done.md`「## 起点」に書く: X-5a〜X-5e の `done.md` の 1 行目が「状態: 完了」・各 PR が `feature/uitk` にマージ済み、HEAD に `moorestech_web/`・`moorestech_client/Assets/Scripts/Client.WebUiHost/` が無い、`manifest.json`・`packages-lock.json` に `jp.juha.cefunity` が 0、`UiSystemEditorSwitch` が 0、束 2・4・5 が `暗転` で起点がある、ラダー 6 本と全録画のスクリプト・G4 のスクリプトが実在する。1 つでも欠ければ `done.md` を「状態: 停止（前提の欠け: <何が>）」にして合図して止まる |
| R-2 | master の取り込み | 最新の `origin/master` を `feature/uitk` へマージし、`merge-base(origin/master, HEAD)` が `origin/master` の HEAD と一致する。消したファイルとの衝突は消す側で解く。取り込みで入った master の Web コミットは束ごとに `写した`（UITK 側の写し先）か `捨てた`（理由）で記帳。UITK へ写す必要のある Web の変更があれば、写すのは I-follow（指揮が起こす）で、このレーンは「停止（I-follow 待ち）」にして待つ |
| R-3 | `feature/uitk` 上の Web コミットの記帳 | 全束の「暗転の起点」より後に、その束の Web パスを触った非マージコミット（休止の撤去・5c の `moorestech_web/` 削除・`localizationKeys.ts` の再生成・その他の Web 変更）が「## 追従記録」に束ごとに 1 行ずつある。`localizationKeys.ts` の再生成だけのコミットは前例どおり `写した`（同じコミットの csv と C#）、それ以外は `捨てた（理由）` |
| R-4 | 全束 `切替済み`・全行 `UITK` | 「## 束」の 5 行の状態が `切替済み`（起点は変えない）、「## 行」の全行（起草時 48 行）の所有者が `UITK`。`python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD` が exit 0 で「違反なし」・全束「未記帳 0 件」。push 後に `--head origin/feature/uitk` でも同じ。`python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_web_follow_real_ledger.py' -v` が OK |
| R-5 | G4 緑 | X-5e が作った G4 のスクリプトがローカルで exit 0（許可リストの外で `WebUi`・`Cef`・`webui` のトークンが 0 件）。切替 PR の G4 の job が pass。統合計画 §5.1-2 の「`Client.WebUiHost`・`moorestech_web/`・Web 用の Kestrel が無い」を grep で確かめる |
| R-6 | 独立レビュー（pr-independent-review の代わり） | moores-code-review を base `9f32a03390`（X-audit が見た範囲の終わり）で 1 回回し、自動適用を反映。切替 PR の全体を opus の評価役 2 体に別々に判定させ、設計判断になる指摘は別の subagent に再現させて再現したものだけ残す。ゴール・裁定から外れるものは `done.md`「要裁定」と PR 本文へ |
| R-7 | 凍結する HEAD のテスト | 凍結する HEAD（以後のビルド・録画の対象）で `uloop compile` が ErrorCount 0、`winunity test-all feature/uitk` の summary が全 shard で失敗 0、playtest の契約テスト 5 本（`test-release-playtest-mac.sh` を含む）が PASS |
| R-8 | CEF の無いプロジェクトでラダー 6 本 | 凍結した HEAD の Mac の Editor で `uitk-lifeline`・`uitk-opening-hud`・`uitk-build-bp`・`uitk-production-loop`・`uitk-research-train`・`uitk-challenge-gate` が `Success: true`・ErrorLogs 0（W6-17 の読み） |
| R-9 | CEF の無いプロジェクトで全録画 | 凍結した HEAD で全録画のスクリプトの一覧を全本流し、1 本 1 行の TSV と分類（PASS／前提つき→流し直して PASS／不具合）を `done.md`「## 全録画の結果」に書く。不具合 0（見つけたものは bd で持ち主へ回し、直って再走で PASS）。X-prerun の GameUi 側の結果と比べた新しい失敗は 0 |
| R-10 | Windows/Mac の配布ビルドと playtest-staging | 凍結した HEAD の 40 桁 SHA を `MOORESTECH_BUILD_BRANCH=feature/uitk` で `release-playtest.sh` に渡して焼き、`release_require_windows_artifact`・`release_require_mac_artifact`（5b が反転した CEF 0 の検査を含む）が通り、steamcmd で playtest-staging へ上がり、`validate_staging.py` が `validated-steam-build.json` を書く。`build-info.json` の `commit` が凍結した SHA・`branch` が `feature/uitk`。playtest への反映（`promote_playtest.py`・Steamworks の手動ライブ設定）とパッチノートはしない |
| R-11 | CEF のファイルが 0（統合計画 §5.1-1） | release.log に CEF の検査の失敗が無いことに加え、Steam から入った実物で確かめる: Mac の `moorestech.app` の下に `cef-unity-server.app` も名前に `cef` を含むファイルも 0、Windows 検証機のインストール先の `moorestech_Data/Plugins/x86_64` に名前に `cef` を含むファイルが 0 |
| R-12 | Windows 検証機の通し検証 | `verify-on-windows.sh` の phase1・phase2 がどちらも `success:true`、受け口の READY を確認して ACK（`[verify] passed`）。phase1・phase2 の Player.log に `DllNotFoundException`・`TypeLoadException`・`FileNotFoundException`・`MissingMethodException` が 0（X-5d の申し送り）。phase2 の録画が成立している（ffmpeg の同梱。X-5b の申し送り `FfmpegRuntimeBundler.cs:49-52` の `Plugins/x86_64` 前提） |
| R-13 | 遠隔実行の 3 点（統合計画 §5.1-3・ADR 0072） | Windows 検証機の staging ビルドを Steam 経由で起動し、(1) `--remoteExec` ありで `return 1 + 1;` が `Succeeded`・`2`、(2) Harmony の Postfix の差し込みと解除が `Succeeded`・`20,777777,20`、(3) `--remoteExec` なしで 25100〜25119 に待受が無く `access.json` が無く Player.log に「起動オプションが無いため遠隔実行は無効です」 |
| R-14 | Mac の Steam（moores-playtest-release の手順 4） | Mac mini の Steam（ベータ playtest-staging）で `appmanifest_1958160.acf` の `buildid` が release.log の BuildID と一致・`StateFlags` 4 → `steam://run/1958160` → ワールドに入る → Tab でインベントリ（UITK）が開くスクショ → Cmd+Q。回避操作の要否・Player.log の 3 行を `promotion.md` と `done.md` に書く |
| R-15 | IME の一巡（統合計画 §4.9・W6-17） | Windows 検証機と Mac の配布ビルド（どちらも staging）で、入力欄 4 つ（バグ報告 `bug-report-description`・検索 `build-menu-search`・BP 名 `modal-input`・駅名 `train-station-name-input`）ごとに、遠隔実行で画面を開いてフォーカスし、OS 段の合成キーで `nihongo`→変換→確定し、遠隔実行で値が `日本語` を含み・確定後の ESC で値が残ってフォーカスだけ外れることを読む。各欄の変換中のスクショ 1 枚（下線と候補窓の位置）。合否と証拠を `done.md`「## IME の一巡」の表へ |
| R-16 | 検証中の master の Web 更新 | Task 6〜12 の各タスクの頭で `git fetch origin` し、`git log --oneline HEAD..origin/master -- moorestech_web/` が空でなければ Task 2 の手順で取り込んで記帳し、D-9 の基準で影響する検証をやり直す。最後の検証の後にも同じ確認をして空であることを `done.md` に書く |
| R-17 | 切替 PR | pr-create で head=`feature/uitk`・base=`master` の PR（中継ブランチなし）。本文の先頭行は報告ページの URL の h1、続けて「何を切り替えたか・検証の結果・既知の差・要裁定・ユーザーの目視の手順・マージの方法（マージコミット。Squash しない）」。報告ページにラダー 6 本の録画・Mac の Steam のスクショ・IME のスクショ・検証の要約。PR の `UITK Web follow gate (G1)` と G4 の job が pass。マージはしない |
| R-18 | ユーザーへ渡すもの | 目視の手順（検証ビルドの起動の仕方・見る項目＝統合計画 §4.9 の実機の項目と IME の体感・X-prerun の全画面の並べ比べの報告ページ）を PR 本文と `done.md`「ユーザーの残タスク」に。独立評価の設計判断は `done.md`「要裁定」へ |
| R-19 | LX-218〜219 | 目視・IME で見つけた差のうち直せないもの（設計書 §3.4 の条件に当たるもの）だけ、文面を `done.md`「## 見た目の例外の案」に LX-218・219 で書き、行と印は I-fix が同じ PR で置く（このレーンは USS・C# を持たない）。配置・色の系統・装飾の有無・文字の大きさの差は例外にせず `要裁定` の Q-n へ |
| R-20 | 完了の定義の証拠 | `done.md`「## 完了の定義」に統合計画 §5.1 の 1〜4 の各行の証拠（コマンドの出力・ログの行・スクショ）を書き、5（playtest で 1 周期）は X-release の持ち物と書く。§5.1-4 の「壊れている 10 本」は bd `moorestech-wwxy` が CLOSED であることを `bd show` で確かめる |
| R-21 | 締め | 最終タスク対（moores-code-review → pr-create・G1/G4 緑）→ Editor を閉じる → 録画・撮影・`Saves/` の片付け → `done.md`（1 行目「状態: 完了」）をコミット・push → `cmux wait-for -S uitk-x-switch-pr` を 1 回 |

### やらないこと
- 切替 PR のマージ・`gh pr merge`・`feature/uitk` 向けの PR（このレーンのコミットは `feature/uitk` へ直接積む。D-3）
- playtest への反映（moores-playtest-release の手順 3 のパッチノートと手順 5。`promote_playtest.py`・`promotion.md` の「反映」節の手作業）。X-release の持ち物
- コードの新規作成・画面の修正・シナリオの書き換え（シナリオは X-5a の持ち物〈W6-8〉、USS は X-prerun、画面は各レーン。見つけた不具合は bd で回して止まる）
- 台帳の「## 束」「## 行」「## 追従記録」以外の節（「## 実測の結論」「## 見た目の例外」等）・`docs/uitk/disposition.md`・設計書・実行計画の本文（改訂の下書きは末尾）
- 台帳と G1（`docs/uitk/TODO.md`・`.github/scripts/check-uitk-web-follow.py`・`uitk_web_follow/`・`uitk-web-follow-gate.yml`）の削除（切替 PR の中で消すと G1 が起動しない。X-cleanup が消す）
- 配布ビルドで DSL を動かす仕組み（W6-17）。CI の `ビルド検証` ラベル（配布ビルドは手元の `release-playtest.sh` で焼いたものを検証する。D-14）
- Codex（ユーザー指示「一旦 Codex セッションは使わない」）。pr-independent-review（スキルは ed461b35de で削除済み）

## Global Constraints

1. AGENTS.md 全部（1 ファイル 200 行未満・1 ディレクトリ 10 ファイル・partial と `Func<>` 禁止・try-catch は外部境界だけ・UniRx・2 行コメント〈日本語 1 行→英語 1 行〉・`#region Internal` はメソッド内のローカル関数だけ・デフォルト引数禁止・.meta を手で作らない・Prefab/シーンを手で編集しない〈uloop execute-dynamic-code 経由はよい〉・fail-closed の経路はログ・コード変更後は必ず compile）
2. 実装はこのホスト（Mac mini）の Claude Code の subagent（opus）で行う。Codex は使わない（指揮の指示「一旦 Codex セッションは使わない」）。実行計画 §5 の codex-audit・外部監査は **opus の subagent 2 体の独立評価**で代える（目的・裁定・設計書・完了条件と成果物の実物だけを渡して別々に判定。設計判断になる指摘は別の subagent に再現させ、再現したものだけ残す）
3. Unity Editor: **Mac mini の Editor は全体で 1 本まで**（このレーンの worktree の 1 本。起動・停止の前に `moores-wt status` で所有を確かめる）。2 本目以降が要るときは Windows（RTX2060）の `~/bin/winunity`（push 済みのブランチを使う。`winunity compile|test|test-all|play <branch> <scenario.cs>|edc`。X-5a の後なので `--ui-system` は付けない）。subagent は Editor を起動しない。配布ビルドの batchmode の Unity が動く間は Mac の Editor を閉じる（D-13）
4. 区切り（タスク）ごとにコミットする。subagent はバックグラウンドのポーリングで待たず前景で実行する。`.superpowers/` はコミットしない。唯一の例外は約 70 分の `release-playtest.sh` で、本体（controller）が Bash の `run_in_background` で起動し Monitor でログの行を受ける（moores-playtest-release 手順 1 の形。D-12）
5. 録画: `run-scenario.sh`（`.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh`）。X-5a の後なので全体スイッチは無く、録画の前に何も流さない。`run-scenario.sh` が初回 "already playing" で終わったら 1 回だけ投入し直す（bd moorestech-trfka）。録画の前に push する（未 push の HEAD でバグ報告の箱が巨大になり送信トーストが 60 秒を超える。C2-b の申し送り）。受け口は `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` で本番へ送らない（Editor の録画。配布ビルドの検証機の phase2 は本番の受け口へ送り、`verify-on-windows.sh` が自分で ACK する）
6. 録画 mp4・`PlaytestResults/`・TestResults xml・撮影 PNG は worktree の gitignore 下に置き、検証後に消す（報告ページへ載せる分は先に載せる）。`../moorestech_logs` に置くのは md と小さな JSON とスクショ 3 枚程度まで（書く前に `../moorestech_logs/README.md` を読む）。テストで `Saves/` に作ったワールドは `.agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で探して消す
7. 番号: 設計書 §7.1 の行は **A-259** だけ、見た目の例外は **LX-218〜LX-219** だけ（W6-18）。計画は設計書・実行計画を書かない（改訂の下書きは末尾の「## 設計書・実行計画へ反映する行」）
8. 共有ファイル（`docs/uitk/TODO.md`・`docs/uitk/disposition.md`・`localization.csv`・`Registration/GameUi`・`MainGameUiRegistration`・tss・asmdef）は自分の行・節だけ触る。取り込みの衝突は両方の行を採る
9. 撤去の検査（共通前提 11 の読み替え）: このレーンは何も消さない。統合計画 §2.6 と §5.1 で「消えているべきもの」を Task 1 と Task 4 の grep で確かめ、検証のタスクごとに「始める前に通るべきもの・終えた後に通るべきもの」を書く
10. 判断台帳: このレーンの Files は `docs/uitk/TODO.md` と repo 外の記録だけで、`.claude/skills/moores-code-review/reviewers/moores-*.md` の `paths` に当たらない（起草時に grep で確かめた）。取り込みの衝突の解決・レビューの自動適用で `paths` に当たるファイルを変えたら、その判断を判断記録に足す
11. `$SP` はこの計画を実行するセッションの scratchpad。`$WT` はこのレーンの worktree（`~/hermes-agent/data/worktrees/moorestech/<指揮が付けた名前>`）、`$H` は `../moorestech_logs/harness/uitk`、`$RUNS` は `~/hermes-agent/data/services/playtest/runs`、`S` は `.agents/skills/unity-playmode-recorded-playtest/scenarios`
12. 秘密値（`~/hermes-agent/data/services/playtest/env.sh` の鍵・遠隔実行のトークン）は出力・記録・PR に書かない。`env.sh` は読むだけで書き換えない

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## 死活表（Phase 2.5）

このレーンは何も消さない。切替 PR（5a〜5e とこのレーン）が master に入ったときに消える操作と残る操作を並べる。「消える」はすべて **切替後は不要（裁定 2026-11-06「UITK一気切替の日にCEFとWebを同時に撤去する」・「切替後に Web へ戻すデバッグ切替は残さない」）**。

| 操作 | 切替後 | 根拠 |
|---|---|---|
| ゲームの全画面（ポーズ・設定・バグ報告・トースト・HUD・通知・ツールチップ・ワールドピン・チュートリアル枠・スキット・ビルドメニュー・ホットバー・モーダル・インベントリ・装備・掴み・レシピ・ブロック UI 全族・研究・チャレンジ・言語ゲート） | 生きる（UITK が描く） | R-8・R-9 の録画、R-14・R-15 の配布ビルドで確かめる |
| Esc でセーブ・バグ報告（命綱） | 生きる | ラダー 1・R-12 の phase2 |
| 出展モード（言語ゲート・自動開始） | 生きる（UITK のゲート。X-5a の W6-11） | ラダー 5。配布ビルドではユーザーの目視の項目（§4.9） |
| 日本語 IME の入力（4 欄） | 生きる | R-15 |
| 遠隔実行（`--remoteExec`） | 生きる（専用の HttpListener。ADR 0072 改訂） | R-13 |
| Windows 検証機の通し検証・playtest-staging への配布の工程 | 生きる（CEF helper の検査は 5b で「CEF 0」へ反転） | R-10〜R-12 |
| Web UI（CEF・Web ホスト・`moorestech_web`）の描画と操作 | 消える | 切替後は不要（裁定 2026-11-06 一気切替の日に撤去） |
| Editor の全体スイッチ（`moorestech/UI System/*` のメニュー・`UiSystemEditorSwitch`） | 消える | 同上（Web へ戻すデバッグ切替は残さない） |
| DSL の Web ロケータ・`--ui-system Web` での録画・Web 側の全録画 | 消える | 同上。撤去前の最後の Web 側の結果は X-prerun（A-251） |
| Web の参照キャプチャの撮影（`capture.ts`） | 消える（master の worktree では X-release までは撮れる） | 同上。最後の参照は X-prerun の `web-<sha>` |
| `test-name-parity.mjs` の作業ツリー読み | 消える（`--web-rev <5c の削除の親>` で読む） | 同上（W6-14） |
| CI の `webui-test` ジョブ・pnpm の step | 消える | 同上（5c） |
| 追従ゲート G1・台帳 | 生きる（切替 PR の中では消さない） | 統合計画 §4.7。X-cleanup が消す |

---

## ファイルの持ち主

| 範囲 | このレーンが書くか |
|---|---|
| 台帳 `docs/uitk/TODO.md` の「## 束」（全束 `切替済み`）・「## 行」（全行 `UITK`）・「## 追従記録」の行（feature/uitk 上の Web を触った全コミットと、取り込んだ master の Web コミット） | 書く（持ち主） |
| 切替 PR の本文・報告ページの manifest（`$SP` に置き repo に入れない） | 書く |
| 検証の記録 `$H/X-switch-pr/done.md`（repo 外） | 書く |
| master の取り込みの衝突の解決（Task 2） | 書く（I-weekly の役をこのレーンが持つ。D-6） |
| moores-code-review の自動適用（Task 5・Task 13） | 書く（レビューの必須の対。D-11） |
| 台帳の他の節（「## 読み方と運用」「## 参照キャプチャ」「## 未撮影」「## agent 既定」「## 実測の結論」「## 壊れた録画の割り振り」「## 見た目の例外」）・`docs/uitk/disposition.md` | 触らない（X-prerun・X-5c・各レーン） |
| `Client.Ui/**`・`Client.Ui.State/**`・`Client.Starter/**`・シナリオ（`S/**`）・`tools/uitk/**`・`scripts/playtest/**`・`.github/**`・スキル・ADR・`.decisions/` | 触らない（X-prerun・X-5a〜5e。見つけた不具合は bd で回す） |

---

## 消えているべきもの・残っているべきものの確かめ（このレーンは消さない）

撤去は 5a〜5e が済ませている。このレーンは HEAD で次を確かめるだけ（Task 1 Step 3 と Task 4 Step 2 のコマンド）。

| 区分 | 確かめるもの | コマンド | 期待 |
|---|---|---|---|
| 5a | 全体スイッチ・Web ロケータ・入力の Web の項・CEF 停止の記録 | `git grep -n -E 'UiSystemEditorSwitch\|UiSystemBootSelection\|WebUiHostEditorSkip\|SwitchedUiElementLocator\|enum UiSystem\b\|WebUiHealthReading\|IWebUiHealthProbe' -- moorestech_client/Assets/Scripts \| wc -l` | 0 |
| 5a | manifest v7 | `git grep -n 'SchemaVersion = 7' -- moorestech_client/Assets/Scripts \| wc -l` | 1 以上 |
| 5b | CEF のパッケージ・prefab | `grep -c 'jp.juha.cefunity' moorestech_client/Packages/manifest.json moorestech_client/Packages/packages-lock.json`・`grep -c -E '592d2edb173e04ee4a91f67a25b875f1\|b266723374a144d27989f433d8f34cea\|m_Name: CefUnity' moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab` | どれも 0 |
| 5b | 配布の CEF 0 の検査 | `grep -n -i 'cef' scripts/playtest/lib/release-artifact.sh` | 「CEF のファイルが 0」の検査の行だけ（helper の存在を要求する行が無い） |
| 5c | Web ホストと Web 資産 | `test ! -e moorestech_web && test ! -e moorestech_client/Assets/Scripts/Client.WebUiHost && echo gone` | `gone` |
| 5c | CI の webui | `grep -n -i 'webui\|pnpm' .github/workflows/run_test.yml .github/workflows/build.yml .github/scripts/unity-test-shard-filter.sh` | 0 行 |
| 5d | AspNetCore の NuGet | `grep -c 'Microsoft.AspNetCore' moorestech_client/Assets/packages.config` | 0 |
| 5e | G4 | X-5e の G4 のスクリプト（Task 1 Step 4 で写す） | exit 0 |
| 残す | 台帳と G1 | `ls docs/uitk/TODO.md .github/scripts/check-uitk-web-follow.py .github/workflows/uitk-web-follow-gate.yml` | 3 つともある |
| 残す | `test-name-parity.mjs` | `ls tools/uitk/test-name-parity.mjs` | ある（X-cleanup まで） |

---

## File Structure

| ファイル | 種類 | 責務 |
|---|---|---|
| `docs/uitk/TODO.md` | Modify | 「## 束」の 5 行の状態・「## 行」の所有者の列・「## 追従記録」の末尾への行の追記（R-2〜R-4） |
| `$H/X-switch-pr/done.md` | Create（repo 外。`../moorestech_logs` でコミット・push） | 状態・起点・完了条件ごとの証拠・全録画の結果・IME の一巡・完了の定義・PR URL・未完了・要裁定・ユーザーの残タスク・integrator への依頼 |
| `$SP/x-switch-pr/` | 作業用（コミットしない） | 台帳を書き換える Python・記帳の下書き・遠隔実行の snippet と PowerShell・IME の鍵送りのスクリプト・評価役への依頼文と報告・報告ページの manifest |
| `moorestech_client/Temp/uitk-recordings/` | 作業用（gitignore 下） | 全録画の一覧と TSV |

---

## Tasks

### Task 1: 前提の確認・起点の記録・Editor の起動・bd・done.md の骨組み

**Files:**
- Create（repo 外）: `$H/X-switch-pr/done.md`

**Interfaces:**
- Consumes: X-5a〜X-5e・X-prerun の `done.md`（`$H/<ID>/done.md`）、X-5e の G4（期待: `python3 .github/scripts/check-uitk-g4.py --root .` が違反 0 で exit 0。X-5e 計画の Produces と実物から写し、違えば D-15 に書いて以後のコマンドを読み替える）、全録画のスクリプト（期待: X-5a が系の引数を外した `tools/uitk/list-recordings.sh`〈引数なし。標準出力に 1 行 1 本・標準エラーに外した名前と理由〉と `tools/uitk/run-all-recordings.sh <list.txt> <out.tsv> <開始行> <本数>`〈TSV の列 `scenario exit seconds run_dir`〉。`tools/uitk/recordings/` の下の場合あり。`usage:` の行から写し、違えば D-15）、ラダー 6 本（期待のパス: `S/uitk/uitk-lifeline.cs`・`S/uitk/stage2/uitk-opening-hud.cs`・`S/uitk/stage3/uitk-build-bp.cs`・`S/uitk/stage4/uitk-production-loop.cs`・`S/uitk/stage4/uitk-research-train.cs`・`S/uitk/stage5/uitk-challenge-gate.cs`）
- Produces: `done.md`「## 起点」（凍結前の HEAD・`origin/master` の SHA・前提の確認の結果・G4 と全録画のスクリプトの実際のコマンド・ラダー 6 本の実際のパス・X-prerun の報告ページの URL と GameUi の全録画の TSV の在処）、bd `<bd>`

- [ ] **Step 1: 場所と枝を確かめる**

```bash
pwd   # $WT であること
git -C "$WT" fetch -q origin
git -C "$WT" branch --show-current
git -C "$WT" rev-parse HEAD origin/feature/uitk origin/master
git -C "$WT" status --porcelain | wc -l
```
Expected: HEAD と `origin/feature/uitk` が同じ、未コミット 0。枝の名前が `feature/uitk` でなくても、HEAD が `origin/feature/uitk` と同じならよい（以後の push は `git push origin HEAD:feature/uitk`。D-3）。

- [ ] **Step 2: 撤去レーンの完了を確かめる**

```bash
for id in X-prerun X-5a X-5b X-5c X-5d X-5e; do printf '%s: ' "$id"; head -1 "$H/$id/done.md"; done
git -C "$WT" log --first-parent --merges --oneline origin/feature/uitk | grep -E 'uitk-lane/x-(prerun|5a|5b|5c|5d|5e)'
bd show moorestech-wwxy | head -3
```
Expected: 6 本とも「状態: 完了」、6 本のマージコミットがある、wwxy が CLOSED（§5.1-4）。欠けがあれば `done.md` の 1 行目を「状態: 停止（前提の欠け: <ID>）」にして Task 14 Step 5 の合図だけ送って止まる。

- [ ] **Step 3: 消えているべきものを確かめる**

上の「## 消えているべきもの・残っているべきものの確かめ」の表のコマンドを `$WT` で全部流し、出力を `done.md`「## 起点」に貼る。Expected: 表の「期待」のとおり。外れたら持ち主のレーン（表の区分）へ bd を立てて停止。

- [ ] **Step 4: 道具とラダーの実物を写す**

```bash
cd "$WT"
ls .github/scripts/ | grep -i g4; grep -n -i 'G4' .github/workflows/*.yml | head
grep -rn 'usage:' tools/uitk/*recordings*.sh tools/uitk/recordings/*.sh 2>/dev/null
for f in uitk/uitk-lifeline.cs uitk/stage2/uitk-opening-hud.cs uitk/stage3/uitk-build-bp.cs uitk/stage4/uitk-production-loop.cs uitk/stage4/uitk-research-train.cs uitk/stage5/uitk-challenge-gate.cs; do ls ".agents/skills/unity-playmode-recorded-playtest/scenarios/$f" || find .agents/skills/unity-playmode-recorded-playtest/scenarios -name "$(basename "$f")"; done
```
Expected: G4 のスクリプトと workflow の job 名、全録画の 2 本の usage、ラダー 6 本のパスが分かる。G4 が無ければ停止（R-5。X-5e の持ち物）。ラダーが 1 本でも無ければ停止（持ち主: 2 は S2-ladder、4 は S4-gamma、5 は S5-ladder）。実際の値を `done.md`「## 起点」に書き、期待と違えば D-15 に 1 行足す。

- [ ] **Step 5: 台帳の束 2・4・5 を確かめる**

```bash
awk '/^## 束/{f=1;next} /^## /{f=0} f' docs/uitk/TODO.md | grep '^| [1-5] |'
```
Expected: 5 行とも状態 `暗転` で起点がある。`Web` の束が残っていたら停止（起点はその束のレーンが「写した画面の範囲」を知って決めるもので、このレーンが後から置くと記帳の要る範囲を取り違える。D-5）。

- [ ] **Step 6: X-prerun の基準を写す**

`$H/X-prerun/done.md` から、PR の報告ページの URL（全画面の並べ比べ）・全録画の GameUi 側の結果の表・撤去前の最後の突き合わせの記録（A-251）を `done.md`「## 起点」へ写す。X-5c の `done.md` から 5c の削除コミットの SHA を写し、`git log --diff-filter=D --format=%H -1 -- moorestech_web/webui/package.json` と一致することを確かめる（以後 `<5c 削除>` と書く）。

- [ ] **Step 7: Windows の 2 つの役を確かめる**

```bash
. ~/hermes-agent/data/services/playtest/env.sh
echo "verify=$MOORESTECH_VERIFY_HOST"; grep -n -E 'HOST=|ssh ' ~/bin/winunity | head -5
```
Expected: 検証機と winunity の Windows が同じ PC か分かる。同じなら、検証機を使う Task 8〜11 の間は winunity を使わない（D-13）。値は `done.md` に書かない（ホスト名だけ「同じ／別」と書く）。

- [ ] **Step 8: Steam の保存資格を先に確かめる**

```bash
. ~/hermes-agent/data/services/playtest/env.sh
python3 "$WT/scripts/playtest/steam_auth/check_login.py" "$MOORESTECH_STEAM_USER"; echo "exit=$?"
```
Expected: exit 0。失敗したら、その場でユーザーに「スマホの Steam アプリで承認できる状態か」を尋ね、moores-playtest-release の手順 1 の `steam_upload_with_vault.py <認証状態の保存先> --login-only` → `--check-login` をユーザーの承認と同時に行う。ユーザーが不在なら `done.md`「ユーザーの残タスク」に書き、Task 2〜7 を先に進める（Task 8 の前にもう一度確かめる）。

- [ ] **Step 9: Editor を起動する**

`moores-wt status` で Mac の Editor（`-batchMode` を除く）が 0 本であることを確かめ、`uloop launch --project-path ./moorestech_client` → `uloop compile --project-path ./moorestech_client`。Expected: ErrorCount 0。他者の Editor が居れば終わるまで待つ（落とさない）。

- [ ] **Step 10: bd と done.md**

```bash
bd create "UITK X-switch-pr: 切替 PR（記帳・全束切替・G4・ラダー6本と全録画・配布ビルドと検証機・遠隔実行・Mac Steam・IME・master への PR）" --parent moorestech-albof -t task
bd update <bd> --claim
```
`$H/X-switch-pr/done.md` を作る: 1 行目「状態: 作業中」、節「## 起点」「## 完了条件ごとの証拠」（R-1〜R-21 の行を空で）「## 全録画の結果」「## IME の一巡」「## 完了の定義」「## 見た目の例外の案」「## PR」「## 未完了」「## 要裁定」「## ユーザーの残タスク」「## integrator への依頼」。`../moorestech_logs/README.md` を読んでから `../moorestech_logs` でコミット・push。

---

### Task 2: master の取り込み（I-weekly の役）と取り込んだ Web コミットの記帳

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 追従記録」の末尾）
- Modify（衝突したときだけ）: 衝突したファイル（D-6）

**Interfaces:**
- Consumes: `origin/master`、G1 `python3 .github/scripts/check-uitk-web-follow.py --base <rev> --head <rev>`
- Produces: `merge-base(origin/master, HEAD) == origin/master`、取り込んだ master の Web コミットの記帳

始める前に通るべきもの: Task 1 の compile 0。終えた後に通るべきもの: compile 0・`^Client\.Tests\.Ui\.` と取り込みで衝突したファイルのテスト・G1「違反なし」。

- [ ] **Step 1: 入る Web コミットを列挙する**

```bash
cd "$WT" && git fetch -q origin
OLD_BASE=$(git merge-base origin/master HEAD)
git log --no-merges --format='%h %s' "$OLD_BASE"..origin/master -- moorestech_web/
git log --no-merges --format='%h' "$OLD_BASE"..origin/master | wc -l
```
Expected: 一覧（起草時 `0e81bbe3c3..origin/master` で `ebe26dc6e8`・その revert `622953fe5f` の 2 件を含む）。

- [ ] **Step 2: 取り込む**

```bash
git merge --no-ff origin/master -m "merge: origin/master を feature/uitk へ取り込む（X-switch-pr）"
git status --porcelain | grep -E '^(DU|UD|UU|AA|DD) '
```
衝突の解き方: `moorestech_web/**` と `Client.WebUiHost/**` の modify/delete は消す側で解く（`git rm <path>`）。中身が UITK に写す要る変更（新しい画面・機能・文言・挙動）なら、写すのは I-follow の持ち物なので Step 4 で止まる。C# の衝突は opus の subagent に「両方の意図を保つ」「UITK 側の設計に従う」で解かせ、解いた内容を判断記録 D-6 の下に 1 行ずつ足す。`localization.csv` の衝突は両方の行を採る。

- [ ] **Step 3: 取り込みの後の compile とテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 1500`
Expected: ErrorCount 0・失敗 0（件数を `done.md` へ）。

- [ ] **Step 4: 入った Web コミットを分ける**

Step 1 の各コミットを `git show --stat <sha> -- moorestech_web/` と本文で読み、次に分ける:
- `捨てた`: UITK に写すものが無い（revert で打ち消し合う組・Web の内部の描画・mock-host・e2e・Web の依存の更新・CEF への手当て〈裁定「CEF 由来の不具合は直さない」〉）
- `写した`: 同じ取り込みで UITK 側に写し先がある（例: `localization.csv` の追加に連動した `localizationKeys.ts` の再生成だけで、キーを使う UITK 側が既にある）
- I-follow が要る: Web の画面・機能・文言・挙動の変更で、UITK 側に写し先が無い

I-follow が要るものが 1 件でもあれば、bd を `--parent <bd>` で 1 件ずつ立て（シナリオ: Web のコミット・変わった画面・UITK の写し先の候補）、`done.md` の 1 行目を「状態: 停止（I-follow 待ち: <sha の並び>）」にして Task 14 Step 5 の合図を送り止まる。I-follow がマージされたら Task 2 からやり直す（同じ SHA は `写した（I-follow <PR>）` で記帳）。

- [ ] **Step 5: 記帳する**

各コミットの持ち主の束を G1 の出力で知る（束はまだ暗転なので、G1 は件数しか出さない。SHA は Task 3 Step 3 でまとめて出るので、ここでは Step 4 の分けを `$SP/x-switch-pr/master-records.md` に `| <sha10> | <判定> | <理由・写し先> |` で下書きし、Task 3 Step 4 で束ごとの行に展開する）。

- [ ] **Step 6: コミットと push**

```bash
git push origin HEAD:feature/uitk
```
（マージコミットは Step 2 で作った。衝突を解いたファイルはマージコミットに入る）

---

### Task 3: 台帳の切替（全束 `切替済み`・全行 `UITK`）と Web コミットの記帳 → G1 0 件

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 束」「## 行」「## 追従記録」）

**Interfaces:**
- Consumes: Task 2 の `$SP/x-switch-pr/master-records.md`、`<5c 削除>`、G1
- Produces: 台帳の全束 `切替済み`・全行 `UITK`・未記帳 0

始める前に通るべきもの: Task 2 の G1「違反なし」。終えた後に通るべきもの: G1 が全束「未記帳 0 件」で exit 0・real_ledger テスト OK・G1 の自己テスト OK。

- [ ] **Step 1: 書き換えのスクリプトを作る（コミットしない）**

`$SP/x-switch-pr/switch_ledger.py`:
```python
# 台帳の「## 束」を全部 切替済み に、「## 行」の所有者を全部 UITK にする（起点・他の列・他の節は変えない）
# Switch every bundle to 切替済み and every row owner to UITK; starts, other columns and sections stay as they are
import pathlib
import re
import sys

path = pathlib.Path(sys.argv[1])
lines = path.read_text(encoding="utf-8").split("\n")
section = None
bundles = rows = 0
for i, line in enumerate(lines):
    if line.startswith("## "):
        section = line[3:].strip()
        continue
    if not line.startswith("| ") or line.startswith("|---"):
        continue
    cells = line.split("|")
    if section == "束" and re.fullmatch(r" [1-5] ", cells[1]):
        if cells[3].strip() != "暗転":
            sys.exit(f"束 {cells[1].strip()} が暗転ではない: {cells[3].strip()}")
        cells[3] = " 切替済み "
        bundles += 1
    elif section == "行" and cells[1].strip() != "行":
        if cells[4].strip() != "Web":
            sys.exit(f"行「{cells[1].strip()}」の所有者が Web ではない: {cells[4].strip()}")
        cells[4] = " UITK "
        rows += 1
    else:
        continue
    lines[i] = "|".join(cells)
if bundles != 5:
    sys.exit(f"束が 5 行ではない: {bundles}")
path.write_text("\n".join(lines), encoding="utf-8")
print(f"bundles={bundles} rows={rows}")
```
Run: `python3 "$SP/x-switch-pr/switch_ledger.py" docs/uitk/TODO.md && git diff --stat docs/uitk/TODO.md`
Expected: `bundles=5 rows=48`（行数は取り込みで増えていれば実数）。差分は「## 束」の 5 セルと「## 行」の所有者の列だけ（`git diff -U0 docs/uitk/TODO.md | grep '^[-+]|' | wc -l` が `2×(5+行数)`）。

- [ ] **Step 2: 台帳を読めることを確かめる**

Run: `python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD 2> "$SP/x-switch-pr/g1-unrecorded.txt"; echo "exit=$?"` は作業ツリーでなく HEAD を読むので、先に `git commit -m "docs(uitk): 切替 PR で全束を切替済み・全行を UITK にする（X-switch-pr）" -- docs/uitk/TODO.md` してから流す（push はまだしない）。
Expected: 台帳を読めないエラー（「台帳を読めない」）が 0。exit 1 で、標準エラーに「G1-1 切替の記帳: 束 n の暗転の起点より後のコミット <sha10> …」が並ぶ（全束が「今回切り替える束」になったため、未記帳がすべて SHA 付きで出る）。

- [ ] **Step 3: 未記帳を束ごとに数える**

```bash
grep -o '束 [1-5] の暗転の起点より後のコミット [0-9a-f]*' "$SP/x-switch-pr/g1-unrecorded.txt" | awk '{print $2, $5}' | sort -u > "$SP/x-switch-pr/unrecorded.tsv"
awk '{print $1}' "$SP/x-switch-pr/unrecorded.tsv" | sort | uniq -c
```
Expected: 束ごとの件数（起草時の見込み: 束 1 に 5 件〈77f336e432・5bf23c60a7・23bf17e0bb・237eb39440・52015d7931〉・束 3 に 1 件〈77f336e432〉に加え、S2〜S5・I-fix の再生成、W6 の撤去〈`<5c 削除>` は全束〉、Task 2 の master の分）。

- [ ] **Step 4: 1 件ずつ判定して行を書く**

`unrecorded.tsv` の各 SHA を `git show --stat --format='%h %s' <sha> -- moorestech_web/` で読み、次の規則で「## 追従記録」の表の末尾へ `| <束> | <sha10> | <判定> | <写し先・理由> |` を足す（同じコミットでも束ごとに 1 行。セルに `|` を書かない）:

| 種類 | 判定 | 写し先・理由の書き方 |
|---|---|---|
| `src/shared/i18n/generated/localizationKeys.ts` の再生成だけ（同じコミットに `Localization/localization.csv` と C# がある） | 写した | 前例どおり「生成物 src/shared/i18n/generated/localizationKeys.ts の再生成だけ（Localization/localization.csv への追加に連動）。束 n の行は i18n（全束）の持ち物として記帳する。写し先は同じコミットの localization.csv と C# <型名>」 |
| `<5c 削除>`（`moorestech_web/` を丸ごと消した） | 捨てた | 「X-5c の撤去（裁定 2026-11-06 一気切替の日に CEF と Web を同時に撤去する）。moorestech_web/ を丸ごと消した。束 n の画面は UITK が描く」 |
| X-5a〜5e の撤去の一部として Web を触ったその他のコミット | 捨てた | 「X-5<区分> の撤去の一部（<1 行の中身>）。切替で Web ごと消える」 |
| Task 2 の master の Web コミット | `master-records.md` の判定 | 下書きをそのまま写す |
| 上のどれにも当たらない feature/uitk 上の Web 変更 | 中身を読んで決める | 写すものがあれば写し先の UITK のファイルとコミット、無ければ理由。決められなければ `done.md`「要裁定」に足して `捨てた（要裁定 <n>。<1 行>）` にしない＝停止して指揮へ返す |

- [ ] **Step 5: G1 を 0 にする**

```bash
git commit -m "docs(uitk): feature/uitk 上と取り込んだ master の Web コミットを各束の追従記録へ記帳する（X-switch-pr）" -- docs/uitk/TODO.md
python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD; echo "exit=$?"
python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_web_follow_real_ledger.py' -v
for t in .github/scripts/tests/test_uitk_web_follow_*.py; do n=$(basename "$t"); [ "$n" = test_uitk_web_follow_real_ledger.py ] && continue; python3 -m unittest discover -s .github/scripts/tests -p "$n"; done
```
Expected: G1 が `G1: 束 1（切替済み・起点 979725bcd1）: 未記帳 0 件` … 束 5 まで 5 行と `G1: 違反なし`・exit 0。real_ledger の unittest が OK（`moorestech_web/webui/src/` が空で空集合の比較になる。5c.md の推測を実測で確かめる）。自己テストが全部 OK。

- [ ] **Step 6: push して origin でも確かめる**

```bash
git push origin HEAD:feature/uitk
git fetch -q origin && python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head origin/feature/uitk; echo "exit=$?"
```
Expected: exit 0・「違反なし」（実行計画の完了条件の形 `--head feature/uitk`）。出力を `done.md` の R-1・R-3・R-4 の行へ貼る。

---

### Task 4: G4 と決定論ゲートの確認

**Files:** なし（記録だけ）

**Interfaces:**
- Consumes: Task 1 Step 4 で写した G4 のコマンド、`node tools/uitk/test-name-parity.mjs`、`<5c 削除>`
- Produces: `done.md` の R-5 の証拠

始める前に通るべきもの: Task 3 の G1 0。終えた後に通るべきもの: このタスクの 4 つが全部 0／OK。

- [ ] **Step 1: G4**

Run: Task 1 Step 4 で写したコマンド（期待 `python3 .github/scripts/check-uitk-g4.py --root .; echo "exit=$?"`）
Expected: 違反 0・exit 0。違反があれば中身を `done.md` に貼り、改名・許可リストの持ち主の X-5e へ bd を立てて停止（このレーンは許可リストを書かない）。

- [ ] **Step 2: 完了の定義 §5.1-2 の grep**

```bash
test ! -e moorestech_web && test ! -e moorestech_client/Assets/Scripts/Client.WebUiHost && echo "no-webhost"
git grep -n -E 'Kestrel|Microsoft\.AspNetCore' -- moorestech_client moorestech_server ':!*.md' | wc -l
```
Expected: `no-webhost`・0。

- [ ] **Step 3: テスト名の突き合わせ（撤去後の形）**

Run: `node tools/uitk/test-name-parity.mjs --bundle all --web-rev "<5c 削除>^"; echo "exit=$?"`
Expected: 全束で未対応 0・exit 0（X-5c の `done.md` の撤去後の記録と同じ件数）。`--web-rev` 無しで流すと全束 0 件で exit 2 になるのは仕様（W6-14）。

- [ ] **Step 4: 自己テスト**

Run: `node --test tools/uitk/testNameParity/tests/*.test.mjs && bash .agents/skills/unity-playmode-recorded-playtest/scripts/tests/edc-client-test.sh && bash .agents/skills/unity-playmode-recorded-playtest/scripts/tests/run-scenario-compile-error-test.sh`
Expected: 全部 pass（G1 の workflow の自己テストの job と同じもの）。

- [ ] **Step 5: 記録**: 4 つの出力を `done.md` の R-5 の行へ。コミットは無し（`../moorestech_logs` の done.md だけコミット・push）

---

### Task 5: 独立レビュー（pr-independent-review の代わり。W6-1）

**Files:**
- Modify（自動適用のときだけ）: moores-code-review が直したファイル（D-11）

**Interfaces:**
- Consumes: moores-code-review スキル（base `9f32a03390`）、統合計画 §5.1、実行計画 §1.5 の X-switch-pr 行
- Produces: `done.md`「## 独立レビュー」（指摘の数・処遇・要裁定）、凍結の候補の HEAD

始める前に通るべきもの: Task 4 が全部 0。終えた後に通るべきもの: compile 0・自動適用が触れたファイルのテスト（クラスのフィルタ）・G1 0・G4 0。

- [ ] **Step 1: moores-code-review を X-audit の後の全部に回す**

moores-code-review スキルを、対象 `9f32a03390..HEAD`（X-audit #1656 が `9f32a03390` までの全体を見た。それより後の I-fix・S2〜S5・X-prerun・X-5a〜5e・取り込み・台帳）で実行する。記録は `../moorestech_logs/harness/` へ（スキルの規定）。設計判断は agent 既定で推奨案を採り、判断記録に「agent 既定（moores-code-review Dn）」と書く。ゴール・裁定から外れるものだけ `done.md`「要裁定」。自動適用・反映がソースの判定経路・条件式に触れたら、そのファイルを持つ画面のラダーを Task 7 で必ず流す（Task 7 は凍結の後なので、ここで直ったものも含めて全部を流す）。

- [ ] **Step 2: 評価役 2 体に別々に判定させる（本体が派遣する）**

opus の subagent を 2 体、同じ依頼文で別々に起こす（互いの結果を見せない。前景で実行。報告は `$SP/x-switch-pr/eval-<1|2>.md`、返答は 3 行）。渡すもの:
- ゴールの原文「WebUIと同じ見た目で、適切な設計、コンポーネント化されたUITKベースのUIを開発すること」
- 裁定 `.decisions/2026-11-06-*.md` の 7 本のパス
- 完了の定義: 統合計画 `docs/uitk-migration/plan.md` §5.1・§2.6「切替 PR の作り方」・§4.7「G1 の走り方」、実行計画 §1.5 の X-switch-pr 行
- 成果物の実物: `git diff --stat origin/master...HEAD`（`$SP/x-switch-pr/pr-stat.txt`）、`git log --first-parent --oneline origin/master..HEAD`、台帳 `docs/uitk/TODO.md`、Task 3・Task 4 の出力、repo を読んでよい（`$WT` の読み取りだけ。書き込み禁止）
- 依頼: 「(a) 切替後に Web/CEF が残る経路（起動・配布物・入力・DSL・CI・文書）が無いか、(b) 裁定（一気切替・Web へ戻す切替を残さない・台帳と G1 は切替 PR で消さない・切替前は配らない）から外れたものが無いか、(c) 統合計画 §5.1 の 1〜4 を満たす証拠がこの計画の残りの検証で取れるか、(d) master へ入れたときに壊れるもの（取り込みの衝突の解き方・削除漏れの参照・asmdef・packages）。各指摘に故障のシナリオ（具体的な入力・状態）とファイル:行を付ける。推測は推測と書く」

- [ ] **Step 3: 再現して処遇を決める**

2 体の指摘を合わせて重複を除き、設計判断になるものは別の opus の subagent（経緯を渡さない）に「このファイル:行でこの故障が起きるか」を再現させ、再現したものだけ残す。残ったものを: 機械的に直せる → 持ち主のファイルならこのレーンは直さず bd で持ち主（I-fix）へ回して停止し、入ったら取り込む（D-11）。台帳・PR 本文の不備 → このレーンが直す。ゴール・裁定から外れる判断 → `done.md`「要裁定」と PR 本文の「要裁定」節。全行を `done.md`「## 独立レビュー」に「指摘・再現・処遇」で書く。

- [ ] **Step 4: コミットと push**

自動適用があれば moores-code-review のスキルがコミットする。`uloop compile`・自動適用のファイルのテスト・G1・G4 を流してから `git push origin HEAD:feature/uitk`。

---

### Task 6: HEAD の凍結とテスト

**Files:** なし（記録だけ）

**Interfaces:**
- Consumes: Task 5 の HEAD
- Produces: **凍結した SHA**（`FROZEN=$(git rev-parse HEAD)`。以後のビルド・録画・検証の対象）を `done.md`「## 起点」へ

始める前に通るべきもの: R-16 の確認（`git fetch -q origin && git log --oneline HEAD..origin/master -- moorestech_web/` が空。空でなければ Task 2 へ戻る）・HEAD が push 済み（`git rev-parse HEAD` と `origin/feature/uitk` が同じ）。終えた後に通るべきもの: 下の 3 つ。

- [ ] **Step 1: compile**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0。

- [ ] **Step 2: テスト全体（Windows）**

Run: `~/bin/winunity test-all feature/uitk --timeout 3000`
Expected: summary-all.txt の全 shard で失敗 0（件数を `done.md` へ）。winunity が使えないとき（検証機と同じ PC で使用中など）は Mac で `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\." --timeout-seconds 1500` と `"^Tests\."`（サーバー）を順に流す（`Client.Tests` は 1500 秒に収まらなければ `^Client\.Tests\.(Ui|UIState|Input)\.` と残りに分ける）。

- [ ] **Step 3: 配布の契約テスト**

```bash
for t in test-release-playtest-mac.sh test-release-playtest.sh test-release-playtest-origin.sh test-verify-on-windows.sh test-remote-exec-offline.sh; do bash "scripts/playtest/tests/$t" || echo "FAIL $t"; done
```
Expected: 5 本とも `PASS: …`、`FAIL` 0（`test-release-playtest-mac.sh` は 5b が反転した CEF 0 のケースを含む）。

- [ ] **Step 4: 凍結を記録する**: `FROZEN=$(git rev-parse HEAD)` を `done.md` に書き、`../moorestech_logs` でコミット・push。以後のタスクで HEAD が変わったら（取り込み・レビューの適用）、D-9 の表で再検証の範囲を決め、`FROZEN` を書き直す

---

### Task 7: CEF の無いプロジェクトでラダー 6 本と全録画（W6-17）

**Files:** なし（`moorestech_client/Temp/uitk-recordings/` は gitignore 下）

**Interfaces:**
- Consumes: Task 1 Step 4 で写した全録画の 2 本のコマンドとラダー 6 本のパス、X-prerun の GameUi 側の全録画の結果（Task 1 Step 6）
- Produces: ラダー 6 本の `recording.mp4`（報告ページ用。Task 14 まで残す）、`done.md`「## 全録画の結果」

始める前に通るべきもの: HEAD が `FROZEN`・Task 1 の表の 5b 行（manifest・lock に `jp.juha.cefunity` が 0）・R-16 の確認・compile 0。終えた後に通るべきもの: ラダー 6 本 `Success: true`・全録画の不具合 0。

- [ ] **Step 1: ラダー 6 本**

```bash
R=.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh
for s in <Task 1 Step 4 のラダー 6 本のパス>; do "$R" ./moorestech_client "$s"; echo "exit=$? $s"; done
```
（1 回の呼び出しが 600 秒に収まらなければ 2 本ずつに分ける）
Expected: 6 本とも exit 0・`result.json` の `Success: true`・ErrorLogs 0。各 run dir の `recording.mp4` を `$SP/x-switch-pr/report/ladder-<n>.mp4` へ写す（Task 14 の報告ページ用）。

- [ ] **Step 2: 全録画の一覧を作る**

Run（Task 1 Step 4 で写した実際のコマンド。期待の形）:
```bash
mkdir -p moorestech_client/Temp/uitk-recordings
tools/uitk/list-recordings.sh > moorestech_client/Temp/uitk-recordings/list.txt 2> moorestech_client/Temp/uitk-recordings/list-excluded.txt
wc -l moorestech_client/Temp/uitk-recordings/list.txt moorestech_client/Temp/uitk-recordings/list-excluded.txt
```
Expected: 一覧の本数が X-5c の `done.md` の全録画の本数と同じ（違えば差を書く）。`list-excluded.txt` が空か、外した理由が全部「削除済み」。

- [ ] **Step 3: 全本を流す**

Run（前景で。1 回の呼び出しが 600 秒に収まるよう 3 本ずつ。1 本で 600 秒を超えたものは 1 本ずつ）:
```bash
tools/uitk/run-all-recordings.sh moorestech_client/Temp/uitk-recordings/list.txt moorestech_client/Temp/uitk-recordings/all.tsv 1 3
tools/uitk/run-all-recordings.sh moorestech_client/Temp/uitk-recordings/list.txt moorestech_client/Temp/uitk-recordings/all.tsv 4 3
```
（開始行を 3 ずつ進めて最後まで。一覧の後半を winunity で流してよい: `~/bin/winunity play feature/uitk <シナリオ>` を 1 本ずつ、結果を同じ列で `all-win.tsv` に書く。検証機と同じ PC なら Task 8〜11 と重ねない）
Expected: TSV の行数＝一覧の本数。exit 0 以外の行ごとに `result.json` と `uloop get-logs --project-path ./moorestech_client --log-type Error` を読み、次に分ける:
- `PASS`
- `前提つき`（`playtest-receiver/*`・IME の `.sh` など。シナリオのヘッダの手順で 1 回流し直し、PASS／不具合に振り直す。`uitk-c2b-ime-mac.cs` は `bash S/uitk/uitk-c2b-ime-mac-keys.sh ./moorestech_client S/uitk/uitk-c2b-ime-mac.cs`）
- `不具合`（それ以外）

- [ ] **Step 4: 不具合を回す**

`不具合` は 1 本ずつ bd を `--parent <bd>` で立て、X-prerun の GameUi 側の結果で同じシナリオが PASS だったか（＝撤去の後の退行か）を書き、持ち主（シナリオは X-5a の持ち物を引き継ぐ I-fix、画面はその画面の I-fix）への依頼文（シナリオのパス・落ちた assert・Error の行・run dir）を指揮へ返す。`done.md` の 1 行目を「状態: 停止（全録画の不具合 n 件の修正待ち）」にして Task 14 Step 5 の合図を送り止まる。直ったら `git merge --no-ff origin/feature/uitk`（I-fix が feature/uitk へ入れたもの）で取り込み、Task 6 からやり直す（`FROZEN` が変わるため。D-9）。

- [ ] **Step 5: 記録**

`done.md`「## 全録画の結果」に TSV を「シナリオ / 分類 / 秒 / X-prerun の GameUi 側」の表で写す（run dir は書かない）。要約 1 行「<n> 本中 PASS <a>・前提つき（流し直して PASS）<c>・不具合 0。X-prerun の GameUi 側と比べた新しい失敗 0」。テストのワールドを `bash .agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で探して消す。ラダー以外の run dir（mp4・PNG）を消す。

---

### Task 8: 配布ビルド・CEF 0 の検査・playtest-staging・Windows 検証機の通し検証

**Files:** なし（成果物は `$RUNS/<label>/`）

**Interfaces:**
- Consumes: `FROZEN`、`$WT/scripts/playtest/release-playtest.sh`（5b が CEF 0 の検査へ反転した `lib/release-artifact.sh` を含む、凍結した HEAD の版。D-2）、moores-playtest-release の手順 1・2
- Produces: `LABEL`（`playtest-YYYYMMDD-HHMM`）、`$RUNS/$LABEL/`（`validated-steam-build.json`・`verify/`・`promotion.md`）、release.log の BuildID

始める前に通るべきもの: HEAD＝`FROZEN`＝`origin/feature/uitk`・Task 6 Step 3 の契約テスト 5 本 PASS・Task 1 Step 8 の保存資格 exit 0・R-16 の確認。終えた後に通るべきもの: R-10〜R-12。

- [ ] **Step 1: Mac の Editor を閉じる**

`moores-wt status` で自分の Editor（`$WT`）だと確かめて `uloop` で閉じる（配布ビルドの batchmode の Unity と同時に 2 本を動かさない。D-13）。

- [ ] **Step 2: 焼く（本体が起動する。subagent に渡さない）**

```bash
cd "$WT" && git fetch -q origin
. ~/hermes-agent/data/services/playtest/env.sh
export MOORESTECH_BUILD_BRANCH=feature/uitk
export MOORESTECH_STEAM_BUILD_LABEL=playtest-$(date +%Y%m%d-%H%M)
RUNS=~/hermes-agent/data/services/playtest/runs
scripts/playtest/release-playtest.sh "$FROZEN" > "$RUNS/$MOORESTECH_STEAM_BUILD_LABEL.release.log" 2>&1   # Bash の run_in_background で起動
```
起動と同時に Monitor で `tail -F "$RUNS/$MOORESTECH_STEAM_BUILD_LABEL.release.log"` を見張り、`ERROR|FAIL|Invalid|credentials|steam_guard|phase|passed|EXIT=` の行を受ける（成功だけでなく失敗も拾う）。`$WT` の `scripts/playtest` から流す理由は D-2（メインクローンの master の版は Mac 成果物に CEF helper を要求して落ちる。`scripts/playtest/lib/release-artifact.sh:54-62`）。
Expected: release.log に `[release-playtest] label=<LABEL> commit=<FROZEN> branch=feature/uitk`・Windows と Mac の成果物の検査の失敗なし・steamcmd の成功・`[verify] passed`・`[release-playtest] promotion: …/promotion.md`。

- [ ] **Step 3: 止まったとき**

- `Cached credentials not found` → `ERROR (Invalid Password)`: moores-playtest-release の手順 2。メインクローンの `~/hermes-agent/data/repos/moorestech/.agents/skills/moores-playtest-release/scripts/upload_and_verify.sh $RUNS/<label>` を run_in_background で起動し、Monitor で `steam_guard_required|uploaded|failed|rejected|FAILED|VERIFY_EXIT|LOCK_FAILED|passed` を受ける（このスクリプトは中身に CEF を見ないのでメインクローンの版でよい。取り込みロックの置き場がメインクローン基準のため、worktree の版を使わない）。`steam_guard_required` が出たら**その場で**ユーザーへ「スマホの Steam アプリで承認して」と伝える
- 成果物の検査で落ちた（`ERROR: 成果物に … がありません`・CEF の検査）: Steam には何も上がっていない。メッセージを `done.md` に貼り、持ち主（CEF 0 の検査は X-5b、成果物の構成はその持ち主）へ bd を立てて停止
- `verify-on-windows.sh` で落ちた: `$RUNS/<label>/verify/` の `result.json`・Player.log を読み、UI の原因なら bd を立てて停止。検証機の環境（WoL・Steam の更新待ち）なら `upload_and_verify.sh $RUNS/<label> --skip-upload` で検証だけやり直す
- master data のピンのずれ（Gotchas）: ピンに合わせて Mac だけやり直す（moores-playtest-release の Gotchas どおり）

- [ ] **Step 4: staging の一致を確かめる**

Run: `python3 "$WT/.agents/skills/moores-playtest-release/scripts/validate_staging.py" "$RUNS/$LABEL"`
Expected: `validated-steam-build.json` が書かれる（release-playtest.sh が自分で流さないため。手順 1 の末尾）。BuildID を `done.md` に書く。

- [ ] **Step 5: 検証機の結果を読む（R-12）**

```bash
V="$RUNS/$LABEL/verify"; ls -R "$V" | head -40
grep -h '"success"' $(find "$V" -name result.json)
grep -c -E 'DllNotFoundException|TypeLoadException|FileNotFoundException|MissingMethodException' $(find "$V" -name 'Player*.log')
```
Expected: phase1・phase2 とも `"success": true`、例外の数が全部 0。phase2 の `result.json` に録画（ffmpeg）とバグ報告の送信が成功した項目がある（項目名は `StandalonePlaytestSmokeRunner` の書くもの。X-5b の申し送りの `Plugins/x86_64` の ffmpeg が動いた証拠）。Player.log が `verify/` に無ければ、ssh で検証機の `%USERPROFILE%\AppData\LocalLow\sakastudio\moorestech\Player.log` を `$SP/x-switch-pr/win-player.log` へ取る。

- [ ] **Step 6: Windows のインストール先の CEF 0（R-11）**

検証機で（`$WT/scripts/playtest/lib/verify-ssh.sh` の `verify_ssh_remote` を使う。`remote-exec.sh:36-39` の `-EncodedCommand` の形で送る）:
```powershell
$steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath).SteamPath -replace "/", "\"
$game = Join-Path $steam "steamapps\common\moorestech"
"build=" + (Get-Content (Join-Path $game "moorestech_Data\StreamingAssets\build-info.json") -Raw | ConvertFrom-Json).commit
"cef-files=" + @(Get-ChildItem -Recurse -File (Join-Path $game "moorestech_Data\Plugins\x86_64") | Where-Object { $_.Name -match "cef" }).Count
"ffmpeg=" + @(Get-ChildItem -Recurse -File $game -Filter "ffmpeg*.exe").Count
```
（ライブラリを既定以外に置いていれば `steamapps\libraryfolders.vdf` から探す）
Expected: `build=<FROZEN>`・`cef-files=0`・`ffmpeg=1` 以上。

- [ ] **Step 7: 記録**: release.log の要所（label・commit・branch・検査・BuildID・`[verify] passed`）と Step 4〜6 の出力を `done.md` の R-10〜R-12 へ。`promotion.md` の「反映」節は実行しない（X-release）と `done.md` に書く。Mac の Editor は Task 9〜11 で要らないので閉じたままにする

---

### Task 9: 遠隔実行の 3 点（Windows 検証機の staging ビルド。統合計画 §5.1-3）

**Files:** なし（`$SP/x-switch-pr/remote-exec/` に snippet と PowerShell）

**Interfaces:**
- Consumes: `$WT/scripts/playtest/remote-exec.sh --windows`（`MOORESTECH_VERIFY_HOST`・`MOORESTECH_VERIFY_USER`・ssh）、Task 8 で検証機に入った staging ビルド、前例の snippet（`$RUNS/remote-exec-listener-20261026-172117/snippets/{hold,harmony,quit}.cs`・`probe.ps1`・`launch.ps1`）
- Produces: `done.md` の R-13 の証拠

始める前に通るべきもの: Task 8 の `[verify] passed`・検証機で moorestech が動いていない。終えた後に通るべきもの: 3 点とも合格・検証機で moorestech のプロセス 0。

- [ ] **Step 1: snippet を用意する（コミットしない）**

前例の `hold.cs`・`harmony.cs`（コメントの水増しは要らない）・`quit.cs` を `$SP/x-switch-pr/remote-exec/` へ写し、`basic.cs` を足す:
```csharp
return 1 + 1;
```
`hold.cs` は phase1 の自動の流れを止めて、要求を送る間ゲームを開けておくため（前例 r2 と同じ）。中身（前例の写し）:
```csharp
using HarmonyLib;
using System.Reflection;
using Cysharp.Threading.Tasks;
var original = typeof(Client.Starter.PlaytestSmoke.StandalonePlaytestSmokeRunner).GetMethod("RunAsync", BindingFlags.NonPublic | BindingFlags.Instance);
var prefix = typeof(RemoteExecSnippet).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(method => method.Name.Contains("g__Hold|"));
new Harmony("x-switch-pr-smoke-hold").Patch(original, prefix: new HarmonyMethod(prefix));
return "smoke-held";

static bool Hold(ref UniTask __result) { __result = UniTask.CompletedTask; return false; }
```
`harmony.cs`（前例の写し）:
```csharp
using HarmonyLib;
using System.Reflection;
var original = typeof(Core.Update.GameUpdater).GetMethod("SecondsToTicks");
var postfix = typeof(RemoteExecSnippet).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(method => method.Name.Contains("g__Postfix|"));
var harmony = new Harmony("x-switch-pr-remote-exec-check");
var before = Core.Update.GameUpdater.SecondsToTicks(1);
var during = 0u;
try { harmony.Patch(original, postfix: new HarmonyMethod(postfix)); during = (uint)Core.Update.GameUpdater.SecondsToTicks(1); }
finally { harmony.UnpatchAll("x-switch-pr-remote-exec-check"); }
var after = Core.Update.GameUpdater.SecondsToTicks(1);
return $"{before},{during},{after}";

static void Postfix(ref uint __result) { __result = 777777; }
```
`quit.cs`（前例の写し）:
```csharp
using Cysharp.Threading.Tasks;
QuitSoon().Forget();
return "quit-scheduled";
static async UniTask QuitSoon() { await UniTask.Delay(1000); UnityEngine.Application.Quit(0); }
```

- [ ] **Step 2: 検証機で Steam 経由で起動する PowerShell を用意する**

`$SP/x-switch-pr/remote-exec/launch.ps1`（`run-smoke.ps1:78-90` の `Start-SteamInInteractiveSession` と同じ形。テスターと同じ起動経路を通す）:
```powershell
param([string]$CaseName, [switch]$RemoteExec)
$ErrorActionPreference = "Stop"
if (@(Get-Process -Name moorestech -ErrorAction SilentlyContinue).Count -gt 0) { throw "moorestech が既に動いている。何も変えずに止める" }
$steamExe = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamExe).SteamExe -replace "/", "\"
$root = "C:/moorestech-smoke/x-switch-pr/$CaseName"
New-Item -ItemType Directory -Force -Path $root | Out-Null
$gameArgs = "--playtestSmoke --smokePhase phase1 --smokeResultDirectory $root"
if ($RemoteExec) { $gameArgs = "--remoteExec $gameArgs" }
$taskName = "x-switch-pr-launch-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
$action = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c start `"`" `"$steamExe`" -applaunch 1958160 $gameArgs"
$principal = New-ScheduledTaskPrincipal -UserId (whoami) -LogonType Interactive
Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Force | Out-Null
Start-ScheduledTask -TaskName $taskName
Start-Sleep -Seconds 5
$result = (Get-ScheduledTaskInfo -TaskName $taskName).LastTaskResult
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
"launch-result=$result"
```
`$SP/x-switch-pr/remote-exec/probe.ps1`（前例 `probe.ps1` を Steam の配置に合わせた形。検証機の上で待つので Mac 側で sleep しない）:
```powershell
param([int]$WaitSeconds)
$ErrorActionPreference = "Stop"
$access = "$env:APPDATA/.moorestech/RemoteExec/access.json"
$deadline = (Get-Date).AddSeconds($WaitSeconds)
while ((Get-Date) -lt $deadline -and -not (Get-Process -Name moorestech -ErrorAction SilentlyContinue)) { Start-Sleep -Seconds 2 }
while ((Get-Date) -lt $deadline -and -not (Test-Path $access)) { Start-Sleep -Seconds 2 }
$p = Get-Process -Name moorestech -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p) { "pid=$($p.Id)"; Get-NetTCPConnection -State Listen -OwningProcess $p.Id -ErrorAction SilentlyContinue | ForEach-Object { "listen=$($_.LocalAddress):$($_.LocalPort)" } } else { "pid=none" }
if (Test-Path $access) { $a = Get-Content -Raw $access | ConvertFrom-Json; "access.json port=$($a.port) processId=$($a.processId) token=[MASKED]" } else { "access.json=none" }
$log = "$env:USERPROFILE/AppData/LocalLow/sakastudio/moorestech/Player.log"
if (Test-Path $log) { Select-String -Path $log -Pattern "\[RemoteExec\]" | ForEach-Object { $_.Line } }
```
送り方: `. "$WT/scripts/playtest/lib/verify-ssh.sh"` の後、`remote-exec.sh:36-39` と同じく `-EncodedCommand` に UTF-16LE の base64 で包んで `verify_ssh_remote` に渡す（引数は先頭の `param` に合わせて `& { <本文> } -CaseName on -RemoteExec` の形で包む）。

- [ ] **Step 3: (1)(2) オプションあり**

1. `launch.ps1 -CaseName on -RemoteExec` → `launch-result=0`
2. `probe.ps1 -WaitSeconds 240` → `listen=127.0.0.1:251xx`（25100〜25119 のどれか）・`access.json port=<同じ> processId=<pid と同じ>`・`[RemoteExec] 待受を開きました`
3. Mac から: `bash "$WT/scripts/playtest/remote-exec.sh" --windows "$SP/x-switch-pr/remote-exec/hold.cs"` → `"result":"smoke-held"`（phase1 が先に終わって取れなければ、前例 r1 と同じく起動直後から probe と hold を続けて流す形でやり直す）
4. `remote-exec.sh --windows basic.cs` → `"outcome":"Succeeded"`・`"result":"2"`・exit 0
5. `remote-exec.sh --windows harmony.cs` → `"outcome":"Succeeded"`・`"result":"20,777777,20"`・exit 0
6. `remote-exec.sh --windows quit.cs` → `quit-scheduled`。`probe.ps1 -WaitSeconds 10` で `pid=none`・`access.json=none`
Expected: 上の全部。応答の JSON を `$SP/x-switch-pr/remote-exec/on-*.json` に残す（トークンは含まれない）。

- [ ] **Step 4: (3) オプションなし**

1. `launch.ps1 -CaseName off`
2. `probe.ps1 -WaitSeconds 120` → `listen=` に 25100〜25119 が無い・`access.json=none`・`[RemoteExec] 起動オプションが無いため遠隔実行は無効です`
3. phase1 が自分で終わる（`C:/moorestech-smoke/x-switch-pr/off/result.json` の `success:true`）のを `probe.ps1 -WaitSeconds 300` の `pid=none` で確かめる
Expected: 上の全部。

- [ ] **Step 5: 記録と片付け**: 3 点の結果を `done.md` の R-13 へ（ポートは書いてよい・トークンは書かない）。検証機の `C:/moorestech-smoke/x-switch-pr/` を消す（ssh で `Remove-Item -Recurse -Force`。自分が作った場所だけ）

---

### Task 10: Mac の Steam での起動（moores-playtest-release の手順 4）

**Files:** なし

**Interfaces:**
- Consumes: Task 8 の BuildID、cua-driver スキルの `references/games.md`
- Produces: `done.md` の R-14・R-11（Mac）の証拠、`$SP/x-switch-pr/report/mac-inventory.png`、`promotion.md` の Mac の欄

始める前に通るべきもの: Task 8 の staging への上げ。Mac の Editor が閉じている（前面を取り合わない）。終えた後に通るべきもの: R-14。

- [ ] **Step 1: 更新を待つ**

`open -g -a Steam` → `~/Library/Application Support/Steam/steamapps/appmanifest_1958160.acf` の `buildid` が Task 8 の BuildID・`StateFlags` が `4` になるまで、Monitor の until ループ（条件: `grep -q "\"buildid\"\s*\"<BuildID>\"" <acf> && grep -q '"StateFlags"\s*"4"' <acf>`・上限 30 分）で待つ（moores-playtest-release の手順 4-1）。

- [ ] **Step 2: Mac の CEF 0（R-11）**

```bash
APP="$HOME/Library/Application Support/Steam/steamapps/common/moorestech/moorestech.app"
find "$APP" -iname '*cef*' | wc -l
python3 -c 'import json,sys;print(json.load(open(sys.argv[1]))["commit"])' "$APP/Contents/Resources/Data/StreamingAssets/build-info.json"
```
Expected: 0・`<FROZEN>`。

- [ ] **Step 3: 起動してワールドに入る**

`open "steam://run/1958160"`。cua-driver の games.md の手順（`screencapture` で撮り、`cliclick` の長押しクリックと `osascript` のキー入力。前面化が要る）で、同意画面「了解」→ 前回異常終了の報告「送らない」→「ローカルでプレイ」。`~/Library/Logs/sakastudio/moorestech/Player.log` の `[PlaytestTitleGates] step`・`[InitializeScenePipeline] mod icon capture completed`・`[TerrainRuntimeBuilder] Terrain built` を確かめる。Gatekeeper などの回避操作が要ったかを記録する。

- [ ] **Step 4: Tab でインベントリ**

Tab → スクショ（`$SP/x-switch-pr/report/mac-inventory.png`）。UITK のインベントリが出ていることを目で見る（CEF の Web の描画ではない＝Player.log に `[WebUiHost]`・`Cef` の行が 0: `grep -c -E 'WebUiHost|Cef' ~/Library/Logs/sakastudio/moorestech/Player.log`）。Cmd+Q で終える。
Expected: スクショにインベントリ・grep が 0。

- [ ] **Step 5: 記録**: `promotion.md` の「Mac 版の手動確認」のチェック欄を埋め、BuildID・セーブ変換の行・スクショで見たことを追記する（moores-playtest-release の手順 4-5。「反映」節は触らない）。`done.md` の R-14 へ

---

### Task 11: IME の一巡（Windows 検証機と Mac の配布ビルド。統合計画 §4.9）

**Files:** なし（`$SP/x-switch-pr/ime/` に snippet と鍵送りのスクリプト）

**Interfaces:**
- Consumes: Task 8・10 で入った staging ビルド、`remote-exec.sh`（Mac はローカル・Windows は `--windows`）、`Client.Ui.Foundation.Root.GameUiRoot.Instance.GetRootElement()`（`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Root/GameUiRoot.cs:41`）、入力欄の testid（`GameTextField` が内側の `UnityEngine.UIElements.TextField` の name に付ける。`GameTextField.cs:51`）: `bug-report-description`（`BugReportFormView.cs:60`・複数行）・`build-menu-search`（`BuildMenuPanelSkeleton.cs:43`）・`modal-input`（`ModalView.cs:67`）・`train-station-name-input`（`TrainStationNameRow.cs:29`）、BP 名の入口 `Client.Game.InGame.UI.Blueprint.BlueprintNameInputState.Open()`（`ClientDIContext.DIContainer.DIContainerResolver` から引く）、駅の設置はサーバーの `ServerContext.WorldBlockDatastore.TryAddBlock`（`Client.Playtest/Operations/PlaytestBlockOps.cs:51-61` の写し）、S9 の Windows の鍵送りの方法（`~/share/2026-10-28-uitk-s9-ime-result/s9-ime-win-report.md` §2〜§4）、Mac の鍵送りの前例 `S/uitk/uitk-c2b-ime-mac-keys.sh`
- Produces: `done.md`「## IME の一巡」の 8 行（2 OS × 4 欄）、変換中のスクショ 8 枚（報告ページには各 OS 1 枚）

始める前に通るべきもの: Task 9（Windows）・Task 10（Mac）が済み、ゲームが動いていない。終えた後に通るべきもの: 8 行とも合格か、不合格の行に bd。

- [ ] **Step 1: 遠隔実行の snippet を用意する（コミットしない）**

`$SP/x-switch-pr/ime/focus.cs`（`FIELD` を欄ごとに置き換えて 4 つ作る）:
```csharp
using UnityEngine.UIElements;
var root = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetRootElement();
var field = root.Q<TextField>("FIELD");
if (field == null) return "missing:FIELD";
field.Focus();
var focused = field.panel.focusController.focusedElement as VisualElement;
return $"focused={(focused == field || (focused != null && field.Contains(focused)))} ime={UnityEngine.Input.imeCompositionMode}";
```
`$SP/x-switch-pr/ime/read.cs`（同じく 4 つ）:
```csharp
using UnityEngine.UIElements;
var root = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetRootElement();
var field = root.Q<TextField>("FIELD");
if (field == null) return "missing:FIELD";
var focused = field.panel.focusController.focusedElement as VisualElement;
return $"value=[{field.value}] focused={(focused == field || (focused != null && field.Contains(focused)))} ime={UnityEngine.Input.imeCompositionMode} composition=[{UnityEngine.Input.compositionString}]";
```
画面を開く snippet:
- `open-bug-report.cs`（OS の Esc でポーズを開いた後に送る。`Button` は `NavigationSubmitEvent` で `clicked` を呼ぶ〈推測。最初の 1 回で確かめ、開かなければ `GameUiScreenMapper.Map`（`Client.Playtest/UiElements/GameUi/GameUiScreenMapper.cs:21-25`）の式で画面座標を出して OS のクリック〈Mac は `cliclick`、Windows は `SendInput` のマウス〉に替え、D-16 に書く〉）:
```csharp
using UnityEngine.UIElements;
var root = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetRootElement();
var button = root.Q<Button>("pause-menu-open-bug-report");
if (button == null) return "missing:pause-menu-open-bug-report";
using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); }
return "submitted";
```
- 検索は OS の B キーで開く（snippet なし）
- `open-bp-name.cs`:
```csharp
var state = Client.Game.InGame.Context.ClientDIContext.DIContainer.DIContainerResolver.Resolve<Client.Game.InGame.UI.Blueprint.BlueprintNameInputState>();
state.Open();
return "opened";
```
（モーダルが出ない〈`read.cs` が `missing:modal-input`〉なら、UIState が BP コピーの状態を要する読み（推測）。そのときは BP 名の欄だけ配布ビルドでの一巡を `未確認` にし、Editor の `S/uitk/stage3/uitk-build-bp.cs` の BP 名を Mac の合成キーで打つ形を `done.md` に書き、ユーザーの目視の項目に足す。D-16）
- `place-station.cs`（`--target server`。プレイヤーの前に駅を置く。座標は `camera-front.cs`〈クライアント〉で `Vector3Int.FloorToInt(Camera.main.transform.position + Camera.main.transform.forward * 4f)` を返させ、その値を書き込む）:
```csharp
var blockId = Core.Master.MasterHolder.BlockMaster.GetBlockId(<蒸気機関車駅の BlockGuid。PlaytestBlockOps.ResolveBlockId と同じ引き方でピン先マスタから写す>);
var placed = Server.Boot.ServerContext.WorldBlockDatastore.TryAddBlock(blockId, new UnityEngine.Vector3Int(<x>, <y>, <z>), Game.Block.Interface.BlockDirection.South, System.Array.Empty<Game.Block.Interface.BlockCreateParam>(), out var block);
return $"placed={placed}";
```
（名前空間は `PlaytestBlockOps.cs` の using から写す。置けたら OS の F キーで開く＝プレイヤーの経路）

- [ ] **Step 2: 鍵送りを用意する**

- Mac（`$SP/x-switch-pr/ime/mac-keys.sh`）: `uitk-c2b-ime-mac-keys.sh` の形で、ゲームの pid を前面にしてから 1 鍵ずつ `osascript -e 'tell application "System Events" to key code <n>'`（0.25 秒おき）。毎回前面が moorestech か確かめ、違えば送らずに止める。終わり方によらず英数（102）へ戻す（`trap`）。鍵: かな 104・英数 102・n 45・i 34・h 4・o 31・g 5・Space 49・Return 36・Esc 53・B 11・Tab 48
- Windows（`$SP/x-switch-pr/ime/win-keys.ps1`）: S9 の報告 §2〜§4 の形。対話セッションの scheduled task から PowerShell で `SendInput` を呼び、仮想キーコード＋スキャンコードで送る（`KEYEVENTF_UNICODE` は使わない）。IME のオンは `VK_IME_ON`（0x16）、オフは `VK_IME_OFF`（0x1A）。鍵（VK/スキャン）: N 0x4E/0x31・I 0x49/0x17・H 0x48/0x23・O 0x4F/0x18・G 0x47/0x22・Space 0x20/0x39・Return 0x0D/0x1C・Esc 0x1B/0x01・B 0x42/0x30・F 0x46/0x21。送る前に前面の窓が moorestech でなければ送らない。`finally` で IME をオフに戻す

- [ ] **Step 3: 1 欄の手順（4 欄 × 2 OS で同じ）**

1. ゲームを起動してワールドに入る（Mac は Task 10 Step 3 と同じ。Windows は Task 9 の `launch.ps1 -CaseName ime -RemoteExec` と `hold.cs`。Mac は `open "steam://run/1958160//--remoteExec/"` で起動し、Player.log に `[RemoteExec] 待受を開きました` が出たことを確かめる。出なければ Steam の起動オプションに `--remoteExec` を入れる手を cua-driver で行い、終わったら外す）
2. 画面を開く: バグ報告は Esc → `open-bug-report.cs`、検索は B、BP 名は `open-bp-name.cs`、駅名は `camera-front.cs` → `place-station.cs` → F
3. `focus.cs` → `focused=True`
4. 鍵: IME オン（Mac 104・Windows VK_IME_ON）→ n i h o n g o → Space → スクショ（変換中。下線と候補窓の位置）→ Return（Mac は候補一覧からの 1 回目が確定にならないことがある〈台帳 R-15 の記録〉。そのときはもう 1 回）
5. `read.cs` → `value` に `日本語` を含む・`composition=[]`
6. 鍵: Esc → `read.cs` → `value` は 5 と同じ・`focused=False`。画面は開いたまま（バグ報告・ビルドメニュー・モーダルは閉じていない。`read.cs` が `missing` を返さない）
7. IME オフ（Mac 102・Windows VK_IME_OFF）。画面を閉じる（Esc。モーダルは Esc で取消）
Expected: 3・5・6 のとおり。8 行それぞれ「OS / 欄 / 変換中の文字 / 確定後の値 / ESC 後の値とフォーカス / 候補窓の位置（スクショから: キャレットの直下か） / 合否」を `done.md`「## IME の一巡」へ。

- [ ] **Step 4: 不合格の処遇**

不合格は 1 行ずつ bd を `--parent <bd>` で立て（OS・欄・手順の番号・値・スクショ）、`GameTextField` の持ち主へ回す依頼を指揮へ返して `done.md` を「状態: 停止（IME の不合格 n 件の修正待ち）」にする。直らない見た目の差（候補窓の位置の数 px など、設計書 §3.4 に当たるもの）は R-19 の LX-218・219 の案に書き、止まらない。

- [ ] **Step 5: 片付け**: 両 OS でゲームを閉じ、IME をオフに戻したことを確かめる。Windows の `C:/moorestech-smoke/x-switch-pr/` を消す。スクショは各 OS 1 枚を `$SP/x-switch-pr/report/` に残し、他は消す

---

### Task 12: 検証の締め（最後の master の確認と完了の定義の証拠）

**Files:** なし（記録だけ）

**Interfaces:**
- Consumes: Task 1〜11 の記録
- Produces: `done.md`「## 完了の定義」・R-16 の最後の確認

- [ ] **Step 1: 最後の master の確認（R-16）**

Run: `git fetch -q origin && git log --oneline "$FROZEN"..origin/master -- moorestech_web/ && git log --oneline "$FROZEN"..origin/master | wc -l`
Expected: Web のコミットが空。空でなければ Task 2 へ戻り、D-9 の表で再検証する。Web 以外の master のコミットの数は PR 本文の「検証の範囲」に「検証ビルド `<FROZEN 10 桁>` の後に master へ入った Web 以外のコミット n 件は検証ビルドに入っていない」と書く（D-10）。

- [ ] **Step 2: 完了の定義の表**

`done.md`「## 完了の定義」に統合計画 §5.1 の 1〜5 を 1 行ずつ: 1＝Task 1 Step 3 の 5b 行の出力＋Task 8 Step 6・Task 10 Step 2 の CEF 0、2＝Task 4 Step 1・2、3＝Task 9、4＝Task 7（ラダー 6 本・全録画）＋`bd show moorestech-wwxy` の CLOSED、5＝「X-release の持ち物（ユーザーのマージの後に playtest へ反映して 7 日以上と報告の確認 1 回）」。

- [ ] **Step 3: 記録**: `../moorestech_logs` でコミット・push

---

### Task 13: moores-code-review で全ブランチレビュー（必須）

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。対象は `<Task 5 Step 1 で見た HEAD>..HEAD`（X-audit が `9f32a03390` まで、Task 5 がその後から凍結の前までを見たので、残りは取り込み・台帳・レビューの適用だけ。D-11）。差分が台帳だけでも省略しない。設計判断は agent 既定で推奨案を採り判断記録へ、ゴール・裁定から外れるものだけ `done.md`「要裁定」。自動適用・反映がソースの判定経路・条件式に触れたら、`FROZEN` が変わるので D-9 の表に従ってラダー 6 本と影響する録画を反映後のバイナリで流し直し、配布物に入るコードに触れたら Task 8〜11 をやり直してから完了とする。

---

### Task 14: 切替 PR を作り G1・G4 を緑にする（必須。マージはしない）

セッション終了可能状態にすること: pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する（解消は Task 2 の手順。Web の取り込みが入れば記帳して D-9 で再検証）。この PR はユーザーがマージするので、`gh pr merge` はしない。

- [ ] **Step 1: 報告ページの manifest を作る**

`$SP/x-switch-pr/report/manifest.json`（書式は `~/hermes-agent/data/services/pr-review/publish/README.md`）:
```json
{
  "head": "<FROZEN 先頭 10 桁>",
  "summary": [
    "全画面を UITK に切り替え、CEF・Web ホスト・moorestech_web・全体スイッチを撤去する（撤去 5a〜5e と台帳の切替）",
    "台帳の全束を切替済み・全行を UITK にし、feature/uitk 上の Web を触った全コミットを追従記録へ記帳した"
  ],
  "media": [
    {"file": "ladder-1.mp4", "caption": "ラダー 1 uitk-lifeline（CEF の無いプロジェクトの Editor）", "group": "ラダー 6 本"},
    {"file": "ladder-2.mp4", "caption": "ラダー 2 uitk-opening-hud", "group": "ラダー 6 本"},
    {"file": "ladder-3.mp4", "caption": "ラダー 3 uitk-build-bp", "group": "ラダー 6 本"},
    {"file": "ladder-4a.mp4", "caption": "ラダー 4 uitk-production-loop", "group": "ラダー 6 本"},
    {"file": "ladder-4b.mp4", "caption": "ラダー 4 uitk-research-train", "group": "ラダー 6 本"},
    {"file": "ladder-5.mp4", "caption": "ラダー 5 uitk-challenge-gate", "group": "ラダー 6 本"},
    {"file": "mac-inventory.png", "caption": "Mac の Steam（playtest-staging）で Tab のインベントリ", "group": "配布ビルド"},
    {"file": "ime-win.png", "caption": "Windows の配布ビルドで変換中（<欄>）", "group": "IME の一巡"},
    {"file": "ime-mac.png", "caption": "Mac の配布ビルドで変換中（<欄>）", "group": "IME の一巡"}
  ],
  "verification": [
    "G1: 全束 未記帳 0 件・違反なし（--base origin/master --head origin/feature/uitk）",
    "G4: 違反 0",
    "Unity コンパイル: エラー 0 / Client.Tests 全体: 失敗 0（winunity test-all）",
    "全録画: <n> 本中 PASS <a>・前提つき（流し直して PASS）<c>・不具合 0",
    "配布ビルド <LABEL>（BuildID <id>・commit <FROZEN 10 桁>・branch feature/uitk）: Windows/Mac の成果物の検査（CEF 0 を含む）合格・Windows 検証機 phase1/phase2 合格",
    "CEF のファイル: Mac の moorestech.app 0・Windows の Plugins/x86_64 0",
    "遠隔実行 3 点: 有効時 2・Harmony 20,777777,20・無効時に待受なし",
    "IME の一巡: Windows・Mac × バグ報告・検索・BP 名・駅名 <合格数>/8"
  ],
  "notes": ["全画面の並べ比べ（撤去前の最後の Web と UITK）: <X-prerun の報告ページの URL>", "要裁定: <件数と 1 行ずつ>"]
}
```
（`file` は `$SP/x-switch-pr/report/` からの相対。数値は実測で埋める）

- [ ] **Step 2: PR を作る**

pr-create スキルで head=`feature/uitk`・base=`master`（中継ブランチを作らない。cwd の枝が `feature/uitk` でなければ head を明示する）。題: 「UITK 一気切替: 全画面を UITK にし CEF と Web を撤去する（切替 PR）」。PR を作った後に `python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <番号> "$SP/x-switch-pr/report/manifest.json"` で報告ページを公開し（失敗したら報告 URL 無しで完了にしない）、本文を次の順で書く（先頭行はその URL の h1。tailnet の URL は書かない）:
1. `# https://review.moores.tech/pr/<番号>/report/`
2. 何を切り替えたか（撤去 5a〜5e の PR へのリンク・台帳の切替）
3. 検証の結果（manifest の verification と同じ。凍結した SHA と BuildID）・検証の範囲（D-10 の 1 行）
4. 既知の差（台帳の「## 見た目の例外」の件数と LX-218・219 の案。目視で見てほしいもの）
5. 要裁定（独立評価で残った設計判断。無ければ「無し」）
6. ユーザーの目視の手順（下の Step 3 の文）
7. マージの方法: 「マージコミットでマージする（Squash and merge を使わない）。マージの後に X-release（playtest への反映）と X-cleanup（台帳と G1 の撤去 PR）を指揮が起こす。`delete-merged-branch.yml` により `feature/uitk` はマージ時に消える（D-17）」

- [ ] **Step 3: 目視の手順の文（PR 本文と done.md「ユーザーの残タスク」に同じ文）**

読者: このゲームのオーナー。説明なしで使ってよい語: Steam のベータ、playtest-staging、IME、Ctrl+U、出展モード。
```markdown
1. Steam のライブラリで moorestech → プロパティ → ベータで `playtest-staging` を選ぶ（パスワードは playtest-staging のもの）。ビルド ID が <BuildID> になるまで待って起動する
2. 見る項目（統合計画 §4.9）:
   - UI の大きさ: ウィンドウと全画面、1920×1080 と他の解像度で、文字・枠が崩れないか
   - Steam オーバーレイ（Shift+Tab）を開いて閉じた後も、操作とフォーカスが戻るか
   - 言語 4 種（設定で切り替え）: 数値の書式と、訳の無い文言の出方
   - Ctrl+U（UI を隠す）・カットシーン・出展モード（環境変数 `MOORESTECH_EVENT_MODE=1` を付けて起動すると言語ゲートが出る）
   - 日本語 IME を実キーボードで: 駅名・BP 名・ビルドメニューの検索・バグ報告に日本語を打ち、遅れ・候補窓のちらつき・全画面と他の解像度での候補窓の位置
3. 全画面を撤去前の Web と並べた比較: <X-prerun の報告ページの URL>
4. 気になったものは PR にコメントする。UI が原因で進めない不具合が無ければ、マージコミットでマージする
```

- [ ] **Step 4: G1・G4 を見る**

Run: `gh pr checks <番号> --watch=false` を、job が終わったことを確かめるまで前景で数回（間は他の記録の作業をする。`sleep` で待たない）。
Expected: `UITK Web follow gate (G1)` と G4 の job が `pass`。他の必須の check（Unity Test・Platform Compile Check 等）の結果も `done.md` に書く。落ちたら原因を直す（台帳ならこのレーン、他は持ち主へ bd で停止）。`CONFLICTING` なら pr-create の手順で master を取り込み（Task 2）、D-9 で再検証してから push。

- [ ] **Step 5: 片付け・報告・合図**

1. `moores-wt status` で自分の Editor だと確かめて閉じる（Task 8 から閉じたままなら確かめるだけ）。録画・`PlaytestResults/`・TestResults・撮影 PNG・`moorestech_client/Temp/uitk-recordings/` を消す（報告ページへ載せた後）。テストのワールドを `list-work-saves.sh` で探して消す
2. bd は閉じない。`bd update <bd> --notes "切替 PR #<番号> 作成。G1・G4 緑。ユーザーの目視とマージ待ち。マージ後に X-release が閉じる"`（D-18）
3. `done.md` の 1 行目を「状態: 完了」（止まったら「状態: 停止（理由）」）にし、完了条件ごとの証拠・PR URL・未完了・要裁定・ユーザーの残タスク（目視・マージ・要裁定の判断・Steam Guard の承認が要ったならその記録）・integrator への依頼（下の「## 設計書・実行計画へ反映する行」・LX-218・219 の案があれば I-fix で行と印を置く依頼・X-release への申し送り〈LABEL・BuildID・run dir・`promotion.md` の Mac の欄は済み〉・X-cleanup への申し送り）を書いて `../moorestech_logs` でコミット・push
4. 最後に `cmux wait-for -S uitk-x-switch-pr` を 1 回（途中で止まる場合も同じ合図を送る）。worktree の `moores-wt rm` は指揮が行う

## Self-Review

- Requirements の各行のタスク: R-1→T1、R-2→T2、R-3・R-4→T3、R-5→T4、R-6→T5、R-7→T6、R-8・R-9→T7、R-10〜R-12→T8、R-13→T9、R-14→T10（R-11 の Mac も）、R-15→T11、R-16→T6〜T12 の頭と T12 Step 1、R-17・R-18→T14、R-19→T11 Step 4・T14 Step 5、R-20→T12 Step 2、R-21→T13・T14
- プレースホルダ: `<FROZEN>`・`<LABEL>`・`<BuildID>`・`<番号>`・`<5c 削除>` は前のタスクの Produces で決まる実行時の値。G4 と全録画のスクリプトの名前は期待の形を書き、Task 1 Step 4 で実物から写して違えば D-15 に書く（まだ無い型の扱い）。`place-station.cs` の BlockGuid と名前空間は実物（`PlaytestBlockOps.cs`）から写す指示にした
- 保留の経路: 前提の欠け（T1）・I-follow 待ち（T2）・全録画の不具合（T7）・IME の不合格（T11）・G4 の違反（T4）はどれも「停止（理由）」と合図で指揮に届き、解ける条件（持ち主のマージ）と再開の位置（T2 か T6）を書いた。最小構成: master に Web のコミットが 0 件のとき T2 Step 4・5 は空で進む。未記帳が 0 件のとき（起こらない。5c の削除が必ず全束に当たる）でも T3 Step 2 の exit 0 で進む
- 凍結と再検証: HEAD が変わる経路（取り込み・レビューの適用・I-fix の取り込み）を D-9 の 1 つの表に集め、T6・T7・T8・T13・T14 から同じ表を引く
- 「台帳と G1 は消さない」: Files は台帳の 3 節だけで、`check-uitk-web-follow.py`・`uitk_web_follow/`・`uitk-web-follow-gate.yml` を触らない
- unityプレイ録画テスト: 含む（T7 のラダー 6 本と全録画。このレーンはランタイムのコードを変えないが、切替 PR の完了条件そのもの）

## 判断記録（ADR）

従う裁定: `.decisions/2026-11-06-*.md` の 7 本（とくに「一気切替の日に CEF と Web を同時に撤去」「切替前のビルドはどこにも配らない。切替 PR の中でだけ Windows/Mac ビルドと検証機の通し検証を回す」「最終検証中に master へ Web の更新が入ったら取り込んで追従し影響箇所を再検証」「master への切替 PR のマージはユーザー」）。

W6 の agent 既定（指揮の割り振りの写し）:
- W6-1: Codex を使わない。§5 の外部監査・codex-audit は opus 2 体の独立評価。pr-independent-review はスキルが削除済み（ed461b35de、2026-10-30）→ Task 5
- W6-2: Mac Editor 1 本・それ以上は winunity（ユーザー指示 2026-11-11「unity2つは安定しないので1つに絞って」）
- W6-8: シナリオの書き換え・削除は全部 X-5a が持つ。このレーンはシナリオを書かない（全録画の不具合は bd で回す）
- W6-9: 全録画のスクリプトは X-5a が系の引数を外したものを使い、書き換えない
- W6-12: バグ報告の manifest は v7（`webUiHealth` を外した）。セーブ形式ではない
- W6-13: `release-artifact.sh` の CEF helper の検査は 5b が「CEF のファイルが 0」へ反転済み。このレーンはビルドで流すだけ
- W6-14: 撤去後の突き合わせは `--web-rev <5c の削除の親>` で回す（`--web-rev` 無しは全束 0 件で exit 2）
- W6-17: 「CEF の存在しないビルドでラダー 6 本と全録画」は、DSL が Editor 専用（`Client.Playtest.asmdef` の includePlatforms が Editor）なので、切替 PR の HEAD（CEF パッケージの無いプロジェクト）の Editor で流す。配布ビルドでは Windows 検証機の phase1/phase2・遠隔実行の 3 点・`release-artifact.sh` の CEF 0 の検査・Mac の Steam の起動確認・IME の一巡（OS 段の合成キー＋遠隔実行で画面を開きフォーカスし値を読む。入力欄はバグ報告・検索・BP 名・駅名）を行う。ビルドで DSL を動かす仕組みは作らない
- W6-18: このレーンは A-259（検証の読み）と LX-218〜219（目視と IME で見つかった差の予約）

この計画の判断（agent 既定）:
- D-1（agent 既定）: playtest-staging への上げは裁定「切替 PR の中でだけ Windows/Mac ビルドと検証機の通し検証を回す」の範囲として行う。検証機の通し検証（`steam.exe -applaunch`）と Mac の Steam の確認は staging に上がったビルドでしか回らず、staging はテスターに配らないパスワード付きのブランチ（`scripts/playtest/README.md`「Steamworks 側」2）。playtest への反映は X-release
- D-2（agent 既定）: `release-playtest.sh` はメインクローン（master）ではなく、凍結した HEAD の worktree（`$WT`）の `scripts/playtest` から流す。master の `scripts/playtest/lib/release-artifact.sh:54-62` は Mac 成果物に `cef-unity-server.app` が無いと exit 4 で落とすので、CEF の無いビルドは master の版では検査を通らない。worktree はメインクローンと object を共有するので、README の「別 clone から流すと object が無い」には当たらない（`moores-wt` もメインクローン固定で worktree を作る〈`~/bin/moores-wt:25`〉）。アップロードのやり直し（`upload_and_verify.sh`）だけはメインクローンの版を使う（取り込みロックの置き場がメインクローン基準・中身に CEF を見ない）。棄却: 依頼文のとおりメインクローンから流す（Mac の検査で必ず落ちる）、master の検査を一時的に外す（master を触る・配布物の検査を弱める）
- D-3（agent 既定）: このレーンのコミットは中継ブランチも `feature/uitk` 向けの PR も作らず、`git push origin HEAD:feature/uitk`（早送りだけ。force しない）で積む。切替 PR の head を `feature/uitk` にする裁定（統合計画 §2.6「中継ブランチ禁止」）と「焼くのは feature/uitk の HEAD の SHA」を両立する形で、G1 は切替 PR がこのレーンのコミットを全部検査する（E-2 の「feature/uitk 向けの PR で G1 を走らせる」の目的は切替 PR の G1 が満たす）。W6 は直列で、このレーンの間に指揮は I-weekly を回さない（取り込みはこのレーンの Task 2 が持つ）
- D-4（agent 既定）: `localizationKeys.ts` の再生成だけのコミットは `写した`（前例: 台帳の束 1・3 の既存の記録）、それ以外の feature/uitk 上の Web コミットは `捨てた（理由）`。実行計画の完了条件の「捨てた で記帳」は「写すものが無いコミットは捨てた」と読む
- D-5（agent 既定）: 束 2・4・5 の起点が台帳に無ければ停止する。起点はその束のレーンが「写した画面の範囲」を知って決めるもので、このレーンが後から置くと、それより前の master の Web 変更が記帳の対象から外れる
- D-6（agent 既定）: 最新の master をビルドの前に取り込む（I-weekly の役をこのレーンが持つ）。マージされる結果に近いものを検証するため。衝突は消したファイル側で解き、UITK へ写す変更は I-follow（指揮が起こす）を待つ。C# の衝突の解き方は opus の subagent に任せ、解いた内容をこの下に 1 行ずつ足す
- D-7（agent 既定）: 台帳の書き換えは「## 束」の状態と「## 行」の所有者の列だけを機械的に行い、起点・他の列・他の節は変えない（`ledger.py:171-176` の規則「全束が切替済みのときだけ UITK」をそのまま満たす）
- D-8（agent 既定）: 全録画の「退行」の基準は X-prerun の GameUi 側の結果（撤去前の最後の全録画）。Web 側は撤去で流せないので比べない（最後の Web 側は X-prerun。A-251）
- D-9（agent 既定）: `FROZEN` が変わったときの再検証の範囲:

| HEAD を変えたもの | やり直すもの |
|---|---|
| 台帳・`.md`・`.github/scripts` の G1/G4 だけ（ビルドの入力に入らない） | Task 3 Step 5・Task 4。ビルドと録画はやり直さない（PR 本文に「検証ビルドと HEAD の差は台帳・文書だけ」と書く） |
| master の取り込みで `moorestech_web/`・`Client.WebUiHost/` の削除の解決だけ | 同上 |
| `moorestech_client/**`・`moorestech_server/**`・`Packages/**`・`scripts/playtest/**` に触れた取り込み・レビューの適用・I-fix | Task 6〜11 の全部（配布物が変わるため） |
| シナリオ（`S/**`）・`tools/uitk/**` だけ | Task 7 の該当シナリオとラダー 6 本 |

- D-10（agent 既定）: 検証中に master へ入った Web 以外のコミットは、衝突しない限り取り込まない。裁定が再検証を求めるのは Web の更新で、Web 以外の変更まで取り込むたびに 70 分のビルドからやり直すと切替が延び続ける。取り込まなかった件数を PR 本文の「検証の範囲」に書く
- D-11（agent 既定）: レビューは 2 回に分ける。Task 5 で X-audit の後の全部（`9f32a03390..HEAD`）を凍結の前に見て、ビルドと検証が直した後のコードに当たるようにする。最終タスク対の Task 13 は Task 5 の後の差分（取り込み・台帳）だけを見る。自動適用がコードに触れたら D-9 の表でやり直す。持ち主のあるファイルの機械的でない修正は、このレーンが直さず bd で I-fix へ回す
- D-12（agent 既定）: 約 70 分の `release-playtest.sh` は、moores-playtest-release 手順 1 のとおり本体が Bash の `run_in_background` で起動し、Monitor でログの行を受ける（前景の上限 600 秒に収まらず、スクリプトは途中で分けられない。Steam Guard の承認は約 1 分で切れるので、ログの行をすぐ受ける必要がある）。subagent には渡さない。ほかの待ちは前景（検証機の上の待ちは PowerShell の中で回す・Steam の更新待ちは Monitor の until ループ・CI は `gh pr checks` を前景で見る）
- D-13（agent 既定）: 配布ビルドの batchmode の Unity が動く間は Mac の Editor を閉じる（Mac の Editor 1 本の指示と同じ理由で、Unity を 2 本動かさない）。検証機と winunity が同じ Windows PC なら、検証機を使う Task 8〜11 の間は winunity を使わない（同じ対話セッションで Steam のゲームと Editor が前面を取り合う）
- D-14（agent 既定）: CI の `ビルド検証` ラベルは付けない。完了条件のビルドは「焼くのは feature/uitk の HEAD の SHA」の配布ビルドで、それは手元の `release-playtest.sh` が焼いて Steam と検証機で確かめる。CI のビルドは配布物にならず、Linux クライアントの除外（5b）と重なる
- D-15（agent 既定・実行時に足す）: Task 1 Step 4 で写した G4・全録画のスクリプト・ラダーの実際のパスが期待と違えば、ここに「期待 → 実物」を 1 行ずつ足し、以後のコマンドを読み替える
- D-16（agent 既定・推測を含む）: IME の一巡で画面を開くのは、プレイヤーの経路（Esc・B・F）を OS の鍵で通せる所はそれを使い、ボタンとモーダルは遠隔実行で開く（DSL が配布ビルドで動かないため。W6-17）。`NavigationSubmitEvent` で `Button.clicked` が呼ばれる・`BlueprintNameInputState.Open()` だけでモーダルが出る、はどちらも推測で、最初の 1 回で確かめる。外れたら代わりの手（OS のクリック／BP 名だけ Editor の合成キーと目視へ）を実行時にここへ書く
- D-17（agent 既定）: `.github/workflows/delete-merged-branch.yml` はマージされた PR の head のブランチを消すので、切替 PR のマージで `feature/uitk` が消える。X-release・X-cleanup は master で行い、切替後に `feature/uitk` を使う工程は無いので止めない。PR 本文に書いてユーザーに知らせる
- D-18（agent 既定）: このレーンの bd は PR を作った時点では閉じない（完了条件の残り＝ユーザーの目視とマージが残る）。X-release が閉じる
- D-19（agent 既定）: 目視と IME で見つけた直せない差の LX-218・219 は、文面だけこのレーンが `done.md` に書き、台帳の行と USS・C# の印は I-fix が同じ PR で置く（`LookExceptionLedgerTest` が行と印の一致を見るので、行だけを先に置けない。このレーンは USS・C# を持たない。前例 S4-gamma の LX-141〜145）
- D-20（agent 既定）: moores-playtest-release の手順 4（Mac の Steam）はこのレーンが staging のビルドで行い、`promotion.md` の Mac の欄を埋める。X-release の「Mac の確認項目をラダー 6 本の先頭操作のスクショへ」は反映の前の別の確認として X-release が持つ（実行計画の 2 行の重なりの読み）
- 要裁定（done.md へ上げる）: 起草時点で 0 件。実行時に独立評価・moores-code-review・目視の準備で出たゴール・裁定から外れる判断を `done.md`「要裁定」に足す
- unityプレイ録画テスト: 含む（Task 7）

## 未確認

- G4 のスクリプトの名前・引数・workflow の job 名（X-5e が作る。Task 1 Step 4 で写す）
- 全録画のスクリプトの X-5a の後の引数（Task 1 Step 4）
- `test_uitk_web_follow_real_ledger.py` が `moorestech_web/webui/src/` の無い HEAD で通るか（5c.md の推測。Task 3 Step 5 で実測）
- release.log に出る 5b の CEF 0 の検査の行の文面（5b が書く。Task 8 Step 2 で読んで `done.md` に写す）
- phase2 の `result.json` で録画（ffmpeg）の成否を示す項目名（Task 8 Step 5 で読む）
- Mac の Steam から `--remoteExec` を渡す形（`steam://run/1958160//--remoteExec/` で効くか。Task 11 Step 3-1）
- `NavigationSubmitEvent` で `Button.clicked` が呼ばれるか・`BlueprintNameInputState.Open()` だけで BP 名のモーダルが出るか（D-16）
- 検証機と winunity が同じ PC か（Task 1 Step 7）

## 本PR外のリファクタ提案

- なし（このレーンはコードを書かない）

## 設計書・実行計画へ反映する行

- 設計書 §7.1:
  `| A-259 | 切替 PR の検証の読み: 「CEF の存在しないビルドでラダー 6 本と全録画」は切替 PR の HEAD（CEF パッケージの無いプロジェクト）の Editor で流す（DSL は Editor 専用）。配布ビルドでは Windows 検証機の phase1/phase2・遠隔実行の 3 点・release-artifact.sh の CEF 0・Steam から入った実物の CEF 0・Mac の Steam の起動・IME の一巡（遠隔実行で画面を開きフォーカスし値を読み、OS 段の合成キーで打つ）を行う。release-playtest.sh は凍結した HEAD の worktree の scripts/playtest から MOORESTECH_BUILD_BRANCH=feature/uitk で流す（master の版は CEF helper を要求する）。レーンのコミットは feature/uitk へ直接積み、切替 PR の G1 が検査する | 配布ビルドで DSL を動かす仕組みを作る（撤去の直前に新しい配布物の口を足し、配布物にテスト専用の経路が入る）／メインクローンの master の release-playtest.sh で焼く（Mac の CEF helper の検査で必ず落ちる）／レーン用の中継ブランチから PR を出す（統合計画 §2.6 の中継ブランチ禁止） |`
- 実行計画 §6.1 の表に足す行: `| X-switch-pr（`P/2026-11-12-uitk-x-switch-pr.md`。PLAN-W6） | A-259 | LX-218〜219（目視と IME で見つかった直せない差の予約。行と印は I-fix が置く） |`
- 実行計画 §1.5 の X-switch-pr 行: 「pr-independent-review」→「moores-code-review（`9f32a03390..HEAD`）と独立評価（opus 2 体。W6-1）」。§5 の末尾「切替 PR は pr-independent-review を付ける」と §7 の U-4「独立レビューの裁定」を同じ読みに改める
- 実行計画 §1.5 の X-release 行: 「moores-playtest-release」の後に「（手順 4 の Mac の Steam は X-switch-pr が staging のビルドで済ませている。X-release は手順 3・5 と、反映前の Mac の追加の確認）」を足す
- 統合計画 §2.6「切替 PR の作り方」の「ラダー 6 本と全録画を CEF の存在しないビルドで流す」に「（DSL は Editor 専用なので、CEF パッケージの無い HEAD の Editor で流す。A-259）」を足す。§5.1-4 の観測のしかたも同じ
- moores-playtest-release（スキル本文）への申し送り（X-5e か X-cleanup が持つ。このレーンは書かない）: 「`feature/uitk` の SHA を焼くときは `MOORESTECH_BUILD_BRANCH=feature/uitk` を付け、その SHA の worktree の `scripts/playtest` から流す」は切替 PR の後は不要になるので、スキルには書かない（記録は A-259 だけ）
