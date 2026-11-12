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
| R-2 | master の取り込み | 最新の `origin/master` を `feature/uitk` へマージし、`merge-base(origin/master, HEAD)` が `origin/master` の HEAD と一致する。消したファイルとの衝突（modify/delete）は、`moorestech_web/**`・`Client.WebUiHost/**` に加えて 5a〜5d が消したパスも全部消す側で解く。両側変更は C# を opus の subagent、`localization.csv` を両方の行、prefab・manifest・lock・workflow 等を Task 2 Step 2 の種類ごとの決まりで解き、解けなければ停止して指揮へ返す（D-46）。git は消したディレクトリの中へ master が新しく足したファイルを衝突にせず黙って入れるので、取り込みの直後（マージのコミットの前）に「撤去済みの場所に在るファイル」（Task 2 Step 2 の R-LIST）を列挙して 0 を確かめ、在ればその中身を UITK 側へ写して（I-follow の手順。このレーンが行い、停止しない）記帳し、ファイルは `git rm` で消してマージのコミットに含める。取り込みで入った master の Web コミットは束ごとに記帳する（「判定」の欄は `写した` か `捨てた` の 2 語のどちらかだけ。写し先・理由は 4 列目。`ledger.py:190,193`）。取り込みの後に C-1〜C-9・G4・compile・`^Client\.Tests\.Ui\.` が通る（D-29） |
| R-3 | `feature/uitk` 上の Web コミットの記帳 | 全束の「暗転の起点」より後に、その束の Web パスを触った非マージコミット（休止の撤去・5c の `moorestech_web/` 削除・`localizationKeys.ts` の再生成・その他の Web 変更）が「## 追従記録」に束ごとに 1 行ずつある。`localizationKeys.ts` の再生成だけのコミットは前例どおり判定 `写した`（4 列目に同じコミットの csv と C#）、それ以外は判定 `捨てた`（4 列目に理由。空だと台帳のパースエラー `ledger.py:193`）。判定の欄に括弧書きを付けない（`写した`・`捨てた` の完全一致しか通らない `ledger.py:190`） |
| R-4 | 全束 `切替済み`・全行 `UITK` | 「## 束」の 5 行の状態が `切替済み`（起点は変えない）、「## 行」の全行（起草時 48 行）の所有者が `UITK`。`python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD` が exit 0 で「違反なし」・全束「未記帳 0 件」。push 後に `--head origin/feature/uitk` でも同じ。`python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_web_follow_real_ledger.py' -v` が OK |
| R-5 | G4 緑 | X-5e が作った G4（期待 `python3 .github/scripts/check-uitk-no-web.py --root .`。X-5e 計画 Task 2 の Produces）がローカルで exit 0（許可リストの外で `WebUi`・`webui`・`Cef`・`CEF`・`Kestrel`・`moorestech_web` が 0 件）。切替 PR の `UITK no-Web gate (G4)` の job が pass。統合計画 §5.1-2 の「`Client.WebUiHost`・`moorestech_web/`・Web 用の Kestrel が無い」を、パスの不在と「`.cs`・`.asmdef`・`packages.config` のコメント以外の行に `Kestrel`・`Microsoft.AspNetCore` が 0」で確かめる（コメントに残る語は G4 の許可リストが理由付きで持つ。D-24） |
| R-6 | 独立レビュー（pr-independent-review の代わり） | moores-code-review を `9f32a03390`（X-audit が見た範囲の終わり）より後の feature 側のコミット（`9f32a03390..$PRE_MERGE_HEAD` のうち `--not origin/master` の非マージのコミット・取り込みの後の feature 側の非マージのコミット・master を運ぶ各マージの衝突の解き方〈直接の取り込みは `--remerge-diff` と同じ。I-weekly の PR の形も含む〉。master 側から来たコードは対象にしない。本体と bug-pass の patch を Task 5 Step 1 の `review_patch.py` で作り `--base-ref` を渡さない。D-31・D-50・D-52）で 1 回回し、自動適用を反映。切替 PR の全体を opus の評価役 2 体に別々に判定させ、設計判断になる指摘は別の subagent に再現させて再現したものだけ残す。ゴール・裁定から外れるものは `done.md`「要裁定」と PR 本文へ |
| R-7 | 凍結する HEAD のテスト | 凍結する HEAD（以後のビルド・録画の対象）で `uloop compile` が ErrorCount 0。Windows（winunity）で Task 6 Step 2 の 19 本の regex（W-E5 を最初から 2 本に割るので流すのは 20 本。X-5d D-37。X-5d の `done.md`「## Windows の 19 本の割り方」。`Client.Tests` の全部・`^Tests\.` の全部・そのどちらでもない名前〈namespace の無いテストクラス・パッケージのテスト。W-C6〉を重なりなく覆う。1 本 10 分未満で前景）を流し、各本の total が 0 でなく `timedOut`・`NO RESULT` が 0、失敗の名前が「既知の Windows だけの失敗」の集合に含まれ、それ以外が 0。既知の集合は、X-audit の `done.md` の 14 件（`-nographics` の描画の UiSrgbComposite 10・UiRuntimePanelCapture 2、パス区切りの TutorialAttentionToken 2。I-fix5 R-19）と、前のレーンの最新の Windows の全体の記録（X-5d の `done.md` の撤去後の 19 本、X-5a の `done.md` の `winunity test-all` の起点と最終 HEAD）の失敗の名前の和。集合の名前は全部 Mac の Editor でクラスのフィルタで流し直して失敗 0（Mac でも落ちたら Windows だけの失敗ではないので既知に数えない）。playtest の契約テスト 5 本（`test-release-playtest-mac.sh` を含む）が PASS（D-23・D-28） |
| R-8 | CEF の無いプロジェクトでラダー 6 本 | 凍結した HEAD の Mac の Editor で `uitk-lifeline`・`uitk-opening-hud`・`uitk-build-bp`・`uitk-production-loop`・`uitk-research-train`・`uitk-challenge-gate` が `Success: true`・ErrorLogs 0（W6-17 の読み） |
| R-9 | CEF の無いプロジェクトで全録画 | 凍結した HEAD で全録画のスクリプトの一覧を全本流し、1 本 1 行の TSV（設計書 [A-177] の列 `scenario・system・exit・seconds・run_dir`。スクリプトの TSV は X-5a が `system` を外したので、`done.md` の表で `system` に `GameUi` を入れる）と [A-177] の 6 分類（PASS／束 5 未移植／wwxy／前提つき／Web 専用／不具合）を `done.md`「## 全録画の結果」に書く。exit 0 以外の行は 1 回流し直し、2 回目に通った行は `PASS（揺れ: 1 回目 FAIL）`、2 回とも落ちた行だけを分類と印にかける（X-prerun R-6・D-36 と同じ。D-41）。`束 5 未移植`・`wwxy`・`Web 専用`・`不具合` が 0（見つけたものは bd で持ち主へ回し、直って再走で PASS）。2 回とも落ちた行に X-prerun と同じ比べ方の印（`既存`／`退行`／`比較不能（新設）`／`比較不能（変更）`。基準は X-prerun の UITK 側の結果で、X-prerun の `PASS（揺れ: 1 回目 FAIL）` は PASS と同じに扱う）を付け、`退行` 0（D-8・D-41）。`run-scenario.sh` では流せない行（EDC の手動投入のスニペット `playtest-receiver/developer-report-interrupt.cs`・`developer-report-resume.cs`〈どちらも `PlaytestRunner.Run` を呼ばず run dir を返さないので `run-scenario.sh` は必ず exit 1。ヘッダに流し直しの手順も無い〉と、出展モードの運転役から流す `uitk/stage5/` の 3 本〈ラダー 6 `uitk-challenge-gate.cs`・`uitk-event-language-gate.cs`・`uitk-stage5-compare.cs`。`.cs` を直接流すと出展モードにならない。S5-ladder D-8〉）と撮影シナリオ `uitk/prerun/`（X-prerun R-6 と同じく全録画の外。物差しは W6-6 で流す）は一覧から外して TSV の exit で判定せず、Task 7 Step 1・Step 3b の流し方で判定する: interrupt は X-5a 計画 Task 8 Step 7 の手動投入で `Result` が `pending upload interrupted: <箱名>`、または開発者トークンの無い機材で例外文がちょうど `Not pending before stop` なら `前提つき（手動投入で PASS）`、言語ゲートは運転役 `uitk-event-language-gate.sh`、ラダー 6 と `uitk-stage5-compare.cs` は運転役 `exhibition-mode-run.sh` で `Success: true` なら `前提つき（運転役で PASS）`、resume は `前提つき（判定の対象外）`で PASS にも不具合にも数えない（interrupt の対で、受け口を外した前置きでは本番送信が再開せず、作者の機材にしか無い進捗の箱を読む。X-5a も判定しない）。resume 以外は落ちたら同じ流し方で 1 回流し直し、2 回とも落ちたら `不具合`（D-44・D-55） |
| R-10 | Windows/Mac の配布ビルドと playtest-staging | 凍結した HEAD の 40 桁 SHA を `MOORESTECH_BUILD_BRANCH=feature/uitk` で `release-playtest.sh` に渡して焼き、`release_require_windows_artifact`・`release_require_mac_artifact`（5b が反転した CEF 0 の検査を含む）が通り、steamcmd で playtest-staging へ上がり、`validate_staging.py` が `validated-steam-build.json` を書く。`build-info.json` の `commit` が凍結した SHA・`branch` が `feature/uitk`（どちらも Steam から入った配布物の実物から読む。Task 8 Step 6・Task 10 Step 2）。`promotion.md` の先頭に「反映禁止」の行がある（Task 8 Step 7。D-32）。playtest への反映（`promote_playtest.py`・Steamworks の手動ライブ設定）とパッチノートはしない |
| R-11 | CEF のファイルが 0（統合計画 §5.1-1） | release.log に CEF の検査の失敗が無いことに加え、Steam から入った実物で確かめる: Mac の `moorestech.app` の下に `cef-unity-server.app` も名前に `cef` を含むファイルも 0、Windows 検証機のインストール先の `moorestech_Data/Plugins/x86_64` に名前に `cef` を含むファイルが 0。あわせて両 OS の `StreamingAssets/WebUi` が無い（X-5c の申し送り。Web の配布物の前処理〈`WebUiProductionArtifactBuilder`〉が消えたことの確かめ。R-11 の名前の検査は `cef` だけなので別に見る） |
| R-12 | Windows 検証機の通し検証 | `verify-on-windows.sh` の phase1・phase2 がどちらも `success:true`、受け口の READY を確認して ACK（`[verify] passed`）。phase1・phase2 の Player.log（`verify-on-windows.sh` は `results` しか回収しないので、Task 9 で上書きされる前に検証機の `Player.log`〈phase2〉と `Player-prev.log`〈phase1〉を ssh で取る。D-26）に `DllNotFoundException`・`TypeLoadException`・`FileNotFoundException`・`MissingMethodException` が 0（X-5d の申し送り）。phase2 の録画が成立している（ffmpeg の同梱。X-5b の申し送り `FfmpegRuntimeBundler.cs:49-52` の `Plugins/x86_64` 前提） |
| R-13 | 遠隔実行の 3 点（統合計画 §5.1-3・ADR 0072） | Windows 検証機の staging ビルドを Steam 経由で起動し、(1) `--remoteExec` ありで `return 1 + 1;` が `Succeeded`・`2`、(2) Harmony の Postfix の差し込みと解除が `Succeeded`・`20,777777,20`、(3) `--remoteExec` なしで 25100〜25119 に待受が無く `access.json` が無く Player.log に「起動オプションが無いため遠隔実行は無効です」 |
| R-14 | Mac の Steam（moores-playtest-release の手順 4） | Mac mini の Steam（ベータ playtest-staging）で `appmanifest_1958160.acf` の `buildid` が release.log の BuildID と一致・`StateFlags` 4 → `steam://run/1958160` → ワールドに入る → Tab でインベントリ（UITK）が開くスクショ → Cmd+Q。回避操作の要否・Player.log の 3 行を `promotion.md` と `done.md` に書く。インストールとベータの選択はユーザーにしかできないので Task 1 Step 8b で先に確かめて頼み、Task 10 の待ちが切れたら Mac の確認を `未確認` にしてユーザーの目視へ回す（D-47）。そのときは `done.md` の 1 行目を「状態: 完了（未確認: Mac の Steam）」で締め、目視の手順の Mac の項目を「済むまでマージしない」とし、マージ直前の確認で指揮が目視が済んだかを聞く（D-58） |
| R-15 | IME の一巡（統合計画 §4.9・W6-17） | Windows 検証機と Mac の配布ビルド（どちらも staging）で、入力欄 4 つ（バグ報告 `bug-report-description`・検索 `build-menu-search`・BP 名 `modal-input`・駅名 `train-station-name-input`）ごとに、遠隔実行で画面を開いてフォーカスし、OS 段の合成キーで次を打って遠隔実行で値を読む（Task 11 Step 3）: (a) `nihongo`→変換→変換中の ESC で取り消す（画面は開いたまま・フォーカスは残る・値に `日本語` が無い）(b) `nihongo`→変換→確定（値が `日本語` を含み `composition=[]`）(c) フォーカスを外して戻した後の再入力が値に入る (d) 確定後の ESC: バグ報告・検索・駅名は値が残ってフォーカスだけ外れる、BP 名（`modal-input`）は同じ 1 回でモーダルが取り消される（`ModalView.cs:71-73,110-114`。ModalScreenPresenterTest の仕様）。各欄の変換中のスクショ 1 枚（下線と候補窓の位置）。画面を開けなかった欄は IME の合否と別の行に「開けない（手順の失敗）」と書き、IME の不合格に数えない。合否と証拠を `done.md`「## IME の一巡」の表へ |
| R-16 | 検証中の master の Web 更新 | Task 6〜12 の各タスクの頭で `git fetch origin` し、`git log --oneline HEAD..origin/master -- "${RLIST[@]}"`（Task 2 の撤去済みの場所の一覧）が空でなければ Task 2 の手順で取り込んで記帳し、D-9 の基準で影響する検証をやり直す。最後の検証の後にも同じ確認をして空であることを `done.md` に書く。PR を作ってからユーザーがマージするまでの間は、マージの直前に指揮が Mac が `未確認` なら目視が済んだか（R-14）・PR の衝突の有無（`mergeable` が `CONFLICTING` でない）と同じ確認（`origin/feature/uitk..origin/master`）と、空のコミットでの PR の check の走り直し（G1・G4・Platform Compile Check が pass、Unity Test の失敗が `done.md`「## CI の既知の失敗」の内）を行い、どれかが外れたらこのレーンを Task 2 の前文の「起こし直し」から起こし直す（Task 14 Step 2 の「マージの方法」・Step 3 の 4。D-38・D-48・D-53） |
| R-17 | 切替 PR | pr-create で head=`feature/uitk`・base=`master` の PR（中継ブランチなし）。本文の先頭行は報告ページの URL の h1、続けて「何を切り替えたか・検証の結果と検証の範囲・既知の差・要裁定・ユーザーの目視の手順・マージの方法（マージコミット。Squash しない。マージ直前の指揮の取り込み確認と、空のコミットで G1・G4・Platform Compile Check・Unity Test を走り直させた結果の確認。D-38・D-48）・戻す先（今 playtest に出ている BuildID。Task 12 Step 3）」。報告ページにラダー 6 本の録画・Mac の Steam のスクショ・IME のスクショ・検証の要約。PR の `UITK Web follow gate (G1)` と `UITK no-Web gate (G4)` の job が pass。マージはしない |
| R-18 | ユーザーへ渡すもの | 目視の手順（検証ビルドの起動の仕方・見る項目＝統合計画 §4.9 の実機の項目と IME の体感・出展モードを付けて起動する操作〈Task 9 Step 5 で検証機で通した操作〉と、出展モードは起動時に既定のワールドを消す注意〈`EventModeAutoStartActions.cs:12`〉・X-prerun の全画面の並べ比べの報告ページ）を PR 本文と `done.md`「ユーザーの残タスク」に。独立評価の設計判断は `done.md`「要裁定」へ |
| R-19 | LX-218〜219 | 目視・IME で見つけた差のうち直せないもの（設計書 §3.4 の条件に当たるもの）だけ、文面を `done.md`「## 見た目の例外の案」に LX-218・219 で書き、行と印は I-fix が同じ PR で置く（このレーンは USS・C# を持たない）。配置・色の系統・装飾の有無・文字の大きさの差は例外にせず `要裁定` の Q-n へ |
| R-20 | 完了の定義の証拠 | `done.md`「## 完了の定義」に統合計画 §5.1 の 1〜4 の各行の証拠（コマンドの出力・ログの行・スクショ）を書き、5（playtest で 1 周期）は X-release の持ち物と書く。§5.1-4 の「壊れている 10 本」（台帳「## 壊れた録画の割り振り」の 10 行。X-prerun R-5 が 1 行ずつ現存／削除／後継に分けて片付けた。起草時は 10 行のうち現存 8 本・削除 2）は bd `moorestech-wwxy` が CLOSED であることを `bd show` で確かめ、現存の本と後継が R-9 の全録画で PASS であることを書く |
| R-22 | 前の段からの申し送り | Task 1 Step 11 で X-prerun・X-5a〜X-5e の `done.md` の「申し送り」「integrator への依頼」「未確認」（と同じ役の節）を読み、X-switch-pr 宛て（切替 PR・配布ビルド・検証機・IME 宛てを含む）の項を全部 `done.md`「## 申し送りの受け」へ写し、各項を受けるタスクを 1 行ずつ書く。起草時に計画から見込んだ項（下の「## 申し送りの受け」の表）は全部タスクがあり、表に無い項が出たら同じ形で受けるタスクを決めて足す（受け先の無い項 0） |
| R-23 | server の配布ビルド（X-5d の申し送り） | 凍結した HEAD で `gh workflow run build.yml --ref feature/uitk` を 1 回流し、`Server Build - StandaloneWindows64`・`Server Build - StandaloneOSX` が success、そのログに X-5d の `done.md`「### server（17 名）」の消した名前（Task 1 Step 11 で取り出して行数を確かめた一覧）がエラー・欠落の行に 0 件。同じ run の `Client Build - *` の結論も `done.md` に書く。Linux の DedicatedServer は `build.yml:65-68` で matrix からコメントアウトされていて dispatch でも焼けないので、焼けないことを `done.md`「## 未確認」と PR 本文の「検証の範囲」に書く（D-30） |
| R-21 | 締め | 最終タスク対（moores-code-review → pr-create・G1/G4 緑）→ Editor を閉じる → 録画・撮影・`Saves/` の片付け → `done.md`（1 行目「状態: 完了」）をコミット・push → `cmux wait-for -S uitk-x-switch-pr` を 1 回 |

### やらないこと
- 切替 PR のマージ・`gh pr merge`・`feature/uitk` 向けの PR（このレーンのコミットは `feature/uitk` へ直接積む。D-3）
- playtest への反映（moores-playtest-release の手順 3 のパッチノートと手順 5。`promote_playtest.py`・`promotion.md` の「反映」節の手作業）。X-release の持ち物
- コードの新規作成・画面の修正・シナリオの書き換え（シナリオは X-5a の持ち物〈W6-8〉、USS は X-prerun、画面は各レーン。見つけた不具合は bd で回して止まる）。例外は master の取り込みで入った Web の変更を UITK へ写す I-follow の手順（Task 2 Step 4。指揮の補足で、このレーンが停止せずに行う。D-29）
- 台帳の「## 束」「## 行」「## 追従記録」以外の節（「## 実測の結論」「## 見た目の例外」等）・`docs/uitk/disposition.md`・設計書・実行計画の本文（改訂の下書きは末尾）
- 台帳と G1（`docs/uitk/TODO.md`・`.github/scripts/check-uitk-web-follow.py`・`uitk_web_follow/`・`uitk-web-follow-gate.yml`）の削除（切替 PR の中で消すと G1 が起動しない。X-cleanup が消す）
- 配布ビルドで DSL を動かす仕組み（W6-17）。CI の `ビルド検証` ラベル（client の配布ビルドは手元の `release-playtest.sh` で焼いたものを検証する。server の配布ビルドだけ `build.yml` を `workflow_dispatch` で 1 回流す。D-14・D-30）
- Codex（ユーザー指示「一旦codexは使わず、claudeセッションで今からこの計画の遂行をして」〈`conductor.md`、2026-11-06 15:55〉）。pr-independent-review（スキルは ed461b35de で削除済み）

## Global Constraints

1. AGENTS.md 全部（1 ファイル 200 行未満・1 ディレクトリ 10 ファイル・partial と `Func<>` 禁止・try-catch は外部境界だけ・UniRx・2 行コメント〈日本語 1 行→英語 1 行〉・`#region Internal` はメソッド内のローカル関数だけ・デフォルト引数禁止・.meta を手で作らない・Prefab/シーンを手で編集しない〈uloop execute-dynamic-code 経由はよい〉・fail-closed の経路はログ・コード変更後は必ず compile）
2. 実装はこのホスト（Mac mini）の Claude Code の subagent（opus）で行う。Codex は使わない（ユーザー指示「一旦codexは使わず、claudeセッションで今からこの計画の遂行をして」〈`conductor.md`、2026-11-06 15:55〉）。実行計画 §5 の codex-audit・外部監査は **opus の subagent 2 体の独立評価**で代える（目的・裁定・設計書・完了条件と成果物の実物だけを渡して別々に判定。設計判断になる指摘は別の subagent に再現させ、再現したものだけ残す）
3. Unity Editor: **Mac mini の Editor は全体で 1 本まで**（このレーンの worktree の 1 本。起動・停止の前に `moores-wt status` で所有を確かめる）。2 本目以降が要るときは Windows（RTX2060）の `~/bin/winunity`（push 済みのブランチを使う。`winunity compile|test|play <branch> <scenario.cs>|edc`。`test-all` は前景の 600 秒に収まらないので使わない〈Global Constraints 4〉。X-5a の後なので `--ui-system` は付けない）。subagent は Editor を起動しない。配布ビルドの batchmode の Unity が動く間は Mac の Editor を閉じる（D-13）
4. 区切り（タスク）ごとにコミットする。subagent はバックグラウンドのポーリングで待たず前景で実行する。`.superpowers/` はコミットしない。前景の Bash 1 回の上限は 600 秒なので、10 分を超えうるコマンドは 1 本 10 分未満に分けて前景で流す: テストは `winunity test-all` も `--timeout-seconds 1500` の大きな `uloop run-tests` も使わず、Task 6 Step 2 の 19 本の regex（W-E5 を 2 本に割って流すのは 20 本。否定先読みの補集合で全体を重なりなく覆う。1 本が `timedOut` なら表の割り方で 2 本に割る）、CI は `timeout 580 gh run watch <id> --exit-status` を exit 124 の間だけ繰り返す（Task 8 Step 8）。分けられないものだけ、本体（controller。subagent ではない）が Bash の `run_in_background` で 1 回起動し、完了の通知で受ける（ポーリングしない）: 約 70 分の `release-playtest.sh`（ログの行は Monitor で受ける。moores-playtest-release 手順 1 の形。D-12）・Windows に worktree を作る初回の `winunity compile`・GUI Editor の起動を含む最初の `--editor` の本・1 クラスで 600 秒を超えるテスト（D-28）
5. 録画: `run-scenario.sh`（`.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh`）。X-5a の後なので全体スイッチは無く、スイッチを立てるコマンドは流さない。`run-scenario.sh` は起動済みの Editor へシナリオを投げるだけで、シェルの環境変数は Editor のプロセスに届かない（`uitk-lifeline.cs:31-32` は Editor のプロセスの値が `http://127.0.0.1:9` でなければ「本番へ送るので中止」の例外を投げる）。そこで Editor を起動するたび（Task 1 Step 9・Task 13 の起動し直し・D-9 の流し直しの前）と、各録画・撮影の前に、次の**録画の前置き**を流して戻り値を見る（前例 X-5a 計画 Task の「受け口だけ外す」・X-5d 計画 Global Constraints の「録画の前置き」・X-prerun 計画 Global Constraints 5。X-prerun の `done.md`「## 5a への申し送り」の前置きが違えば、そこから全体スイッチの行を除いたものに替える）:
   ```bash
   uloop control-play-mode --project-path ./moorestech_client --action Stop
   uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Localization.Localize.Initialize(); var ja = Client.Localization.Localize.TrySetChosenLanguage("japanese"); return System.Environment.GetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE") + " ja=" + ja;'
   ```
   Expected: `http://127.0.0.1:9 ja=True`。違えば録画に入らない（前置きの欠けで落ちた録画を不具合として持ち主へ回さない）。`run-scenario.sh` が初回 "already playing" で終わったら 1 回だけ投入し直す（bd moorestech-trfka）。録画の前に push する（未 push の HEAD でバグ報告の箱が巨大になり送信トーストが 60 秒を超える。C2-b の申し送り）。受け口は `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` で本番へ送らない（Editor の録画。配布ビルドの検証機の phase2 は本番の受け口へ送り、`verify-on-windows.sh` が自分で ACK する）
6. 録画 mp4・`PlaytestResults/`・TestResults xml・撮影 PNG は worktree の gitignore 下に置き、検証後に消す（報告ページへ載せる分は先に載せる）。`../moorestech_logs` に置くのは md と小さな JSON とスクショ 3 枚程度まで（書く前に `../moorestech_logs/README.md` を読む）。テストで `Saves/` に作ったワールドは `.agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で探して消す
7. 番号: 設計書 §7.1 の行は **A-259** だけ、見た目の例外は **LX-218〜LX-219** だけ（W6-18）。計画は設計書・実行計画を書かない（改訂の下書きは末尾の「## 設計書・実行計画へ反映する行」）
8. 共有ファイル（`docs/uitk/TODO.md`・`docs/uitk/disposition.md`・`localization.csv`・`Registration/GameUi`・`MainGameUiRegistration`・tss・asmdef）は自分の行・節だけ触る。取り込みの衝突は両方の行を採る（それ以外の C# でない両側変更〈prefab・manifest・lock・workflow〉は Task 2 Step 2 の決まり。D-46）
9. 撤去の検査（共通前提 11 の読み替え）: このレーンは何も消さない。統合計画 §2.6 と §5.1 で「消えているべきもの」を Task 1 と Task 4 の grep で確かめ、検証のタスクごとに「始める前に通るべきもの・終えた後に通るべきもの」を書く
10. 判断台帳: このレーンの Files は `docs/uitk/TODO.md` と repo 外の記録だけで、`.claude/skills/moores-code-review/reviewers/moores-*.md` の `paths` に当たらない（起草時に grep で確かめた）。取り込みの衝突の解決・レビューの自動適用で `paths` に当たるファイルを変えたら、その判断を判断記録に足す
11. `$SP` はこの計画を実行するセッションの scratchpad。`$WT` はこのレーンの worktree（`~/hermes-agent/data/worktrees/moorestech/<指揮が付けた名前>`）、`$H` は `../moorestech_logs/harness/uitk`、`$RUNS` は `~/hermes-agent/data/services/playtest/runs`、`S` は `.agents/skills/unity-playmode-recorded-playtest/scenarios`。シェルの変数は Bash の呼び出しをまたいで残らないので、実行時に決まる値（`WT`・`H`・`RUNS`・`PRERUN_HEAD`・`SHOT_BASE`・`PRE_MERGE_HEAD`・`REVIEW1_HEAD`・`FROZEN`・`LABEL`）は決めた時点で `done.md` の決まった節（Task 2 の前文の「起こし直し」の表）と `$SP/x-switch-pr/vars.sh` に `NAME=<値>` の 1 行で書き（同じ名前があれば行を置き換える。例外は `PRE_MERGE_HEAD` で、最初の取り込みの前の値を残すため `grep -q '^PRE_MERGE_HEAD=' … || echo … >>` で無いときだけ書く。Task 2 Step 1）、`$WT`・`$FROZEN` などを使う呼び出しの頭で必ず `. "$SP/x-switch-pr/vars.sh" || exit 1` を読む（`$SP` は呼び出しごとに絶対パスで書く）。R-LIST を使う呼び出しは続けて `. "$SP/x-switch-pr/rlist.sh" || exit 1`（Task 2 の前文。空なら exit 1 で止まる）を読む（D-40）。`|| exit 1` を外さない: 無いファイルを `.` で読んでも bash・zsh とも続行し（起草時に実測: bash は rc=1・zsh は rc=127 で次の行へ進む）、`cd "$WT"` が空の引数で今の場所に留まるので、新しいセッション（cmux はメインワークツリーで起こす）では `git merge` 以下がメインクローンで走りうる。`$SP` の作業ファイル（`vars.sh`・`rlist.sh`・`known-win-failures.txt`・`x5d-names-*.txt`・`smoke-psnr-states.txt`）はセッションをまたいで残らないので、別のセッションで続けるときは Task 2 の前文の「起こし直し」で `done.md` から作り直す（D-53）
12. 秘密値（`~/hermes-agent/data/services/playtest/env.sh` の鍵・遠隔実行のトークン）は出力・記録・PR に書かない。`env.sh` は読むだけで書き換えない

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 配置の違反は無し。死活表に `webUiHealth` の行が欠けていたので足し、注目点 3 件（直接 push・worktree から焼く・合成イベントで押す）は D-3・D-2・D-16 に理由がある
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強3・弱2・第3バケツ0 / 強 3 件（D-8 と A-177・D-1 と配布の裁定・D-3 と E-2）は agent 既定で答えて計画と反映する行を直し、弱 2 件（局所関数の部分一致・build-info の欠損）はこのままでよいと決めた（D-21・D-22）

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
| バグ報告の manifest の `webUiHealth`（CEF 停止の記録） | 消える（manifest v8。X-5a の W6-12。S5-crosscut が v7 で足した `gameUiExceptions` の欄は残る） | 切替後は不要（裁定 2026-11-06「CEF 停止の記録は…切替の日に CEF と一緒に消える」）。読み手のダッシュボードは過去の箱を読むので残り、`webUiHealth` の欄の無い v8 の箱は `absent` と出す（`scripts/playtest/dashboard/display_fields.py:68-69`。X-5a が触らないと決めた） |

---

## ファイルの持ち主

| 範囲 | このレーンが書くか |
|---|---|
| 台帳 `docs/uitk/TODO.md` の「## 束」（全束 `切替済み`）・「## 行」（全行 `UITK`）・「## 追従記録」の行（feature/uitk 上の Web を触った全コミットと、取り込んだ master の Web コミット） | 書く（持ち主） |
| 切替 PR の本文・報告ページの manifest（`$SP` に置き repo に入れない） | 書く |
| 検証の記録 `$H/X-switch-pr/done.md`（repo 外） | 書く |
| master の取り込みの衝突の解決と、撤去済みの場所に戻ったファイルの `git rm`（Task 2） | 書く（I-weekly の役をこのレーンが持つ。D-6・D-29） |
| 取り込みで入った master の Web の変更を UITK へ写す I-follow の手順（Task 2 Step 4。写し先の `Client.Ui/**`・`Client.Ui.State/**`・`localization.csv`・L1 テスト・その画面の録画） | 書く（指揮の補足。このレーンが停止せずに行う。写した行は台帳へ `写した` で記帳。D-29） |
| moores-code-review の自動適用（Task 5・Task 13） | 書く（レビューの必須の対。D-11） |
| 台帳の他の節（「## 読み方と運用」「## 参照キャプチャ」「## 未撮影」「## agent 既定」「## 実測の結論」「## 壊れた録画の割り振り」「## 見た目の例外」）・`docs/uitk/disposition.md` | 触らない（X-prerun・X-5c・各レーン） |
| `Client.Ui/**`・`Client.Ui.State/**`・`Client.Starter/**`・シナリオ（`S/**`）・`tools/uitk/**`・`scripts/playtest/**`・`.github/**`・スキル・ADR・`.decisions/` | 触らない（X-prerun・X-5a〜5e。見つけた不具合は bd で回す。`Client.Ui/**`・`Client.Ui.State/**` は上の I-follow の手順の写し先だけ例外） |
| `$RUNS/<LABEL>/promotion.md` の先頭の「反映禁止」の 1 行と「Mac 版の手動確認」の欄（repo 外） | 書く（Task 8 Step 7・Task 10 Step 5。「反映」節は触らない。D-32） |

---

## 消えているべきもの・残っているべきものの確かめ（このレーンは消さない）

撤去は 5a〜5e が済ませている。このレーンは HEAD で次を確かめるだけ（Task 1 Step 3 と Task 4 Step 2）。コマンドは表の中に書かず、下のコードブロックを写して流す（表のセルでは `|` を `\|` と書くしかなく、それを `-E` の下へ写すと字そのままの `|` になって項目が残っていても 0 件の偽の合格になる。また、この Mac の `git grep -E` は `\b` が効かない。どちらも起草時に実測）。

| 区分 | 確かめるもの | コマンド（下のブロックの番号） | 期待 |
|---|---|---|---|
| 5a | 全体スイッチ・Web ロケータ・入力の Web の項・CEF 停止の記録 | C-1 | 0 |
| 5a | manifest v8（付け直されていればその版。X-5a 計画 D-28） | C-2 | 1 以上 |
| 5b | CEF のパッケージ・prefab（ルートの Canvas・CanvasGroup・CanvasRenderer を含む。X-5b R-1・A-254） | C-3 | どれも 0 |
| 5b | 配布の CEF 0 の検査 | C-4 | 「CEF のファイルが 0」の検査の行だけ（helper の存在を要求する行が無い） |
| 5c | Web ホストと Web 資産 | C-5 | `gone` |
| 5c | CI の webui | C-6 | 0 行 |
| 5d | AspNetCore の NuGet | C-7 | 0 |
| 5e | G4 | X-5e の G4（Task 1 Step 4 で写す。期待 `python3 .github/scripts/check-uitk-no-web.py --root .`） | exit 0 |
| 残す | 台帳と G1 | C-8 | 3 つともある |
| 残す | `test-name-parity.mjs` | C-9 | ある（X-cleanup まで） |

```bash
# C-1（起草時の HEAD では 70 件と 1 件。enum は `-P` の \b で数える）
git grep -n -E 'UiSystemEditorSwitch|UiSystemBootSelection|WebUiHostEditorSkip|SwitchedUiElementLocator|WebUiHealthReading|IWebUiHealthProbe' -- moorestech_client/Assets/Scripts | wc -l
git grep -n -P 'enum UiSystem\b' -- moorestech_client/Assets/Scripts | wc -l
# C-2（8 は 5a が webUiHealth を外した版〈7 は S5-crosscut の gameUiExceptions が使った。PLAN-W5 の申し送り〉。切替までに master が manifest の版を上げて取り込まれ、5a〈か取り込みのレーン〉が「webUiHealth を外した版」を次の番号へ付け直していたら、付け直した版の番号に読み替える。X-5a 計画 D-28。番号は X-5a の done.md の「integrator への依頼」と、取り込みのレーンの done.md から写し、D-15 に「8 → <n>」を書く）
git grep -n 'SchemaVersion = 8' -- moorestech_client/Assets/Scripts | wc -l
# C-3（起草時の HEAD では prefab が 3 と 3。2 行目の 3 は Canvas〈`--- !u!223`。ルートの 155 行目〉・CanvasGroup〈`!u!225`〉・CefUnity の CanvasRenderer〈`!u!222`〉）
grep -c 'jp.juha.cefunity' moorestech_client/Packages/manifest.json moorestech_client/Packages/packages-lock.json
grep -c -E '592d2edb173e04ee4a91f67a25b875f1|b266723374a144d27989f433d8f34cea|m_Name: CefUnity' moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab
grep -c -E '^--- !u!(222|223|225) ' moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab
# C-4
grep -n -i 'cef' scripts/playtest/lib/release-artifact.sh
# C-5
test ! -e moorestech_web && test ! -e moorestech_client/Assets/Scripts/Client.WebUiHost && echo gone
# C-6（BRE の \| は「または」。起草時の HEAD では 17・8・5 行）
grep -n -i 'webui\|pnpm' .github/workflows/run_test.yml .github/workflows/build.yml .github/scripts/unity-test-shard-filter.sh
# C-7
grep -c 'Microsoft.AspNetCore' moorestech_client/Assets/packages.config
# C-8・C-9
ls docs/uitk/TODO.md .github/scripts/check-uitk-web-follow.py .github/workflows/uitk-web-follow-gate.yml
ls tools/uitk/test-name-parity.mjs
```

---

## 申し送りの受け（前の段からこのレーン宛ての項）

起草時に X-prerun・X-5a〜X-5e の計画から見込んだ項。実行時は Task 1 Step 11 で各 `done.md` の「申し送り」「integrator への依頼」「未確認」を読み、表に無い項を同じ形で足す（受け先の無い項を残さない。R-22）。

| 出所 | 項 | 受けるタスク |
|---|---|---|
| X-5a R-4・死活表 | 配布ビルドで Client.Ui が登録され UITK で描く | Task 10 Step 4（Tab のインベントリ・Player.log に `WebUiHost`・`Cef` の行 0）・Task 11 |
| X-5b R-15 (1) | Windows の `moorestech_Data/Plugins/x86_64` が CEF 無しでも作られ ffmpeg が同梱される（`FfmpegRuntimeBundler.cs:49-52` の前提） | Task 8 Step 5（phase2 の録画）・Step 6（`ffmpeg=1` 以上・`plugins-dir=True`） |
| X-5b R-15 (1) | `release_require_no_cef_files` が実ビルドで誤検知しない（とくに `icudtl.dat`・`chrome_*_percent.pak`） | Task 8 Step 3 の「CEF の検査で落ちた」の分岐（当たったファイルが CEF の物か Unity の物かを見分ける）・Step 7（検査の行を写す） |
| X-5b R-15 (1) | Linux クライアントの除外の bd の id | Task 14 Step 2 の PR 本文「検証の範囲」 |
| X-5c R-11・R-16 | 削除コミット `D` を暗転中の各束の `捨てた` で記帳 | Task 1 Step 6（SHA を写す）・Task 3 Step 4（`<5c 削除>` の行） |
| X-5c やらないこと | `StreamingAssets/WebUi` が配布物に無い | Task 8 Step 6（Windows）・Task 10 Step 2（Mac） |
| X-5d R-12 (1) | client の配布ビルドで消した DLL 61 名（id 55 件と解析器の DLL 6 名）が足りなくならない | Task 8 Step 5（検証機の Player.log の例外 0 と、61 名が例外の行に 0）・Task 10 Step 3（Mac の Player.log） |
| X-5d R-12 (2)・D-20 | server の配布ビルド（`build.yml` の Server Build。Windows・Mac）が通り、消した 17 名（id 13 件と解析器の 4 名）がログに 0 | Task 8 Step 8（R-23） |
| X-5d R-12 (3) | 名前の一覧は `done.md`「## 消した名前の一覧」の `### client（61 名）`・`### server（17 名）` の下にある。取り出した行数が見出しの数と同じで空でないことを確かめてから使う（空の一覧だと `/usr/bin/grep -F -f` は何にも当たらず 0 件で素通りする） | Task 1 Step 11（取り出しと行数の確かめ） |
| X-5d R-12 (4) | Windows の表は 19 本（W-C6 を足した）。割った本は `done.md`「## Windows の 19 本の割り方」 | Task 6 Step 2 |
| X-5e R-1・R-5 | `docs/webui`・`docs/webui-parity`・`docs/webui-feature-videos-2026-06-14` を `docs/archive/` の下へ移し、`.agents/skills/ref-webui-design`・`.claude/rules/ref-webui-design.md` を消した（`done.md`「## 撤去済みの場所」） | Task 1 Step 11（R-LIST との突き合わせ）・Task 2 の前文（R-LIST） |
| X-prerun R-6 | 全録画の HEAD は `done.md`「## 全録画の結果」の頭の `- 全録画の HEAD（GameUi）: <40 桁>`。UITK の分類に `PASS（揺れ: 1 回目 FAIL）` がある | Task 1 Step 6（`<PRERUN_HEAD>`）・Task 7 Step 3（揺れの扱い。D-41） |
| X-prerun D-37 | 参照の撮影元の master にだけある Web の変更による差（master の未追従）を `done.md`「integrator への依頼」にコミットと撮影 id で書く | Task 1 Step 11（写す候補へ）・Task 2 Step 4（I-follow の写す候補。D-42） |
| X-5a R-15・R-16・Task 8 Step 6・7 | `list-recordings.sh` は全 `.cs` を出し標準エラーに何も出さない。`run-scenario.sh` では流せない 3 本（`developer-report-interrupt.cs`・`developer-report-resume.cs`・`uitk/stage5/uitk-event-language-gate.cs`）は TSV の exit で判定せず、interrupt は手動投入・言語ゲートは運転役 `.sh`・resume は判定の対象外 | Task 7 Step 2（`list-run.txt`・`list-excluded.txt` は空）・Step 3b・Step 5（D-44・D-45） |
| X-prerun の直し役（4 周目）・S5-ladder D-8・R-2 | ラダー 6 `uitk-challenge-gate.cs` と並べ比べ `uitk-stage5-compare.cs` は出展モードの運転役 `exhibition-mode-run.sh` 経由でしか流せない（Mac の Editor。`winunity play` は武装を挟めない）。運転役の後は前置きを流し直す。撮影シナリオ `uitk/prerun/` は全録画の一覧から外す（X-prerun R-6）。物差し `prerun-smoke-shots.cs` は PNG と `smoke-rects.tsv` を出し、矩形は全状態で `diff` 一致・PSNR の `inf` は X-prerun の `done.md`「### 物差し（prerun-smoke-shots）」の「PSNR で比べる状態」だけに求める（X-prerun D-51） | Task 1 Step 4（運転役の usage）・Step 6（物差しの小節の写し）・Step 12（TSV も写す）・Task 6 Step 1b・Task 7 Step 1・Step 2・Step 3b（D-55・D-56） |
| X-5e の bd の処遇 | `moorestech-s2zk.1`（配布ビルド・steamcmd・検証機の通し）をこのレーンの bd へ `bd dep` で結ぶ | Task 1 Step 10 |
| X-5e の bd の処遇・X-prerun の「## 未撮影」 | IME（`moorestech-lc9ty.3` の駅名入力を含む）と変換中の見た目 | Task 11（変換中のスクショ） |

---

## File Structure

| ファイル | 種類 | 責務 |
|---|---|---|
| `docs/uitk/TODO.md` | Modify | 「## 束」の 5 行の状態・「## 行」の所有者の列・「## 追従記録」の末尾への行の追記（R-2〜R-4） |
| `$H/X-switch-pr/done.md` | Create（repo 外。`../moorestech_logs` でコミット・push） | 状態・起点・完了条件ごとの証拠・全録画の結果・IME の一巡・完了の定義・PR URL・未完了・要裁定・ユーザーの残タスク・integrator への依頼 |
| `$SP/x-switch-pr/` | 作業用（コミットしない） | 台帳を書き換える Python・レビューの patch を作る `review_patch.py`（Task 5 Step 1）・CI の結果の xml（Task 14 Step 4）・記帳の下書き・遠隔実行の snippet と PowerShell・IME の鍵送りのスクリプト・評価役への依頼文と報告・報告ページの manifest |
| `moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/` | 作業用（gitignore 下。`.gitignore:102`） | 全録画の一覧と TSV（`Temp/` に置かない: Unity は Editor を正常に閉じると `Temp/` を消し、Task 8 で Editor を閉じた後や停止からの再開で一覧と TSV を失う。X-prerun D-43） |
| `moorestech_client/.uloop/outputs/compare/X-switch-pr/` | 作業用（gitignore 下） | W6-6 (a)(b) の基準（PNG と `smoke-rects.tsv`）と撮り直し（Task 1 Step 12・Task 6 Step 1b・D-9）、I-follow の比較の撮影（Task 2 Step 4） |
| I-follow の写し先（`Client.Ui/**`・`Client.Ui.State/**`・`localization.csv`・L1 テスト） | Modify/Create（写すものがあるときだけ） | master の取り込みで入った Web の変更を UITK へ写す（Task 2 Step 4・D-29） |

---

## Tasks

### Task 1: 前提の確認・起点の記録・Editor の起動・bd・done.md の骨組み

**Files:**
- Create（repo 外）: `$H/X-switch-pr/done.md`

**Interfaces:**
- Consumes: X-5a〜X-5e・X-prerun の `done.md`（`$H/<ID>/done.md`）、X-5e の G4（期待: `python3 .github/scripts/check-uitk-no-web.py --root <repo> [--allowlist <path>] [--report]` が許可リスト外 0 件で exit 0〈1＝許可リスト外に語がある・2＝許可リストの書式誤りか腐った行〉、workflow `.github/workflows/uitk-no-web-gate.yml` の job 名 `UITK no-Web gate (G4)`。X-5e 計画 `docs/superpowers/plans/2026-11-12-uitk-x-5e.md` Task 2 の Produces から写した形で、実物と違えば D-15 に書いて以後のコマンドを読み替える）、全録画のスクリプト（期待: X-5a が系の引数を外した `tools/uitk/list-recordings.sh`〈引数なし。`scenarios` 配下の全 `.cs` を標準出力に 1 行 1 本で出し、台帳を読まず標準エラーには何も出さない。X-5a 計画 Task 8 Step 6 の全文。D-45〉と `tools/uitk/run-all-recordings.sh <list.txt> <out.tsv> <開始行> <本数>`〈TSV の列 `scenario exit seconds run_dir`〉。`tools/uitk/recordings/` の下の場合あり。`usage:` の行から写し、違えば D-15）、`run-scenario.sh` では流せない 3 本（`S/playtest-receiver/developer-report-interrupt.cs`・`S/playtest-receiver/developer-report-resume.cs`・`S/uitk/stage5/uitk-event-language-gate.cs` と運転役 `S/uitk/stage5/uitk-event-language-gate.sh <unity-project-path>`〈S5-gate の `usage:` の行。X-5a は全体スイッチの文だけを消す〉。D-44）、出展モードの運転役 `S/uitk/stage5/exhibition-mode-run.sh`（S5-ladder R-2。起草時の形 `<project> <GameUi|Web> <scenario.cs>`。X-5a が全体スイッチを消すときに系の引数を外すか書き換える〈X-prerun の直し役から X-5a への依頼〉ので `usage` の行から写す）とそれで流す `S/uitk/stage5/uitk-stage5-compare.cs`（D-55）、ラダー 6 本（期待のパス: `S/uitk/uitk-lifeline.cs`・`S/uitk/stage2/uitk-opening-hud.cs`・`S/uitk/stage3/uitk-build-bp.cs`・`S/uitk/stage4/uitk-production-loop.cs`・`S/uitk/stage4/uitk-research-train.cs`・`S/uitk/stage5/uitk-challenge-gate.cs`）
- Produces: `done.md`「## 起点」（凍結前の HEAD・`origin/master` の SHA・前提の確認の結果・G4 と全録画のスクリプトの実際のコマンド・ラダー 6 本の実際のパス・X-prerun の報告ページの URL と GameUi の全録画の TSV の在処・X-prerun の全録画を流した HEAD の 40 桁 SHA〈以後 `<PRERUN_HEAD>`〉・既知の Windows だけの失敗の名前の集合〈R-7〉）、`done.md`「## 申し送りの受け」（R-22）、W6-6 の基準の撮影（Step 12）、bd `<bd>`

- [ ] **Step 1: 場所と枝を確かめる**

```bash
pwd   # $WT であること
mkdir -p "$SP/x-switch-pr"; printf 'WT=%s\nH=%s\n' "$PWD" "$(cd ../moorestech_logs/harness/uitk && pwd)" > "$SP/x-switch-pr/vars.sh"; . "$SP/x-switch-pr/vars.sh" || exit 1   # 以後の呼び出しは頭で読む（Global Constraints 11）
git -C "$WT" fetch -q origin
git -C "$WT" branch --show-current
git -C "$WT" rev-parse HEAD origin/feature/uitk origin/master
git -C "$WT" status --porcelain | wc -l
```
Expected: HEAD と `origin/feature/uitk` が同じ、未コミット 0。枝の名前が `feature/uitk` でなくても、HEAD が `origin/feature/uitk` と同じならよい（以後の push は `git push origin HEAD:feature/uitk`。D-3）。

- [ ] **Step 2: 撤去レーンの完了を確かめる**

```bash
. "$SP/x-switch-pr/vars.sh" || exit 1
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
ls .github/scripts/check-uitk-no-web.py .github/scripts/uitk_no_web/ .github/workflows/uitk-no-web-gate.yml; grep -n -E 'name: UITK no-Web gate|check-uitk-no-web' .github/workflows/*.yml
ls tools/uitk tools/uitk/recordings 2>/dev/null; grep -rn -i 'usage' --include='*recordings*.sh' tools/uitk   # glob を使わない（この Mac の zsh は当たらない glob が 1 つでもあるとコマンドごと実行しない）
for f in uitk/uitk-lifeline.cs uitk/stage2/uitk-opening-hud.cs uitk/stage3/uitk-build-bp.cs uitk/stage4/uitk-production-loop.cs uitk/stage4/uitk-research-train.cs uitk/stage5/uitk-challenge-gate.cs; do ls ".agents/skills/unity-playmode-recorded-playtest/scenarios/$f" || find .agents/skills/unity-playmode-recorded-playtest/scenarios -name "$(basename "$f")"; done
S=.agents/skills/unity-playmode-recorded-playtest/scenarios
ls "$S/playtest-receiver/developer-report-interrupt.cs" "$S/playtest-receiver/developer-report-resume.cs" "$S/uitk/stage5/uitk-event-language-gate.cs" "$S/uitk/stage5/uitk-event-language-gate.sh"
grep -c 'PlaytestRunner.Run' "$S/playtest-receiver/developer-report-interrupt.cs" "$S/playtest-receiver/developer-report-resume.cs"; grep -n 'usage' "$S/uitk/stage5/uitk-event-language-gate.sh"
ls "$S/uitk/stage5/exhibition-mode-run.sh" "$S/uitk/stage5/uitk-stage5-compare.cs"; grep -n 'usage' "$S/uitk/stage5/exhibition-mode-run.sh"; grep -rl 'exhibition-mode-run' "$S" | sort
```
Expected: G4 のスクリプトと workflow の job 名、全録画の 2 本の usage、ラダー 6 本のパスが分かる。Task 7 Step 3b の本と 2 つの運転役（言語ゲートの `.sh` と出展モードの `exhibition-mode-run.sh`）が在り、interrupt・resume の `PlaytestRunner.Run` が 0 件、運転役の usage が分かる（D-44）。`exhibition-mode-run.sh` の usage に系の引数（`<GameUi|Web>`）が残っていれば Task 7 の呼び方で `GameUi` を渡し、外れていれば渡さない。最後の grep で運転役を指す `.cs` が、ラダー 6・言語ゲート・`uitk-stage5-compare.cs` の 3 本のほかにあれば Task 7 Step 2 の `grep -v` と Step 3b に足す（D-55）。G4 が無ければ停止（R-5。X-5e の持ち物）。ラダーが 1 本でも無ければ停止（持ち主: 2 は S2-ladder、4 は S4-gamma、5 は S5-ladder）。実際の値を `done.md`「## 起点」に書き、期待と違えば D-15 に 1 行足す。

- [ ] **Step 5: 台帳の束 2・4・5 を確かめる**

```bash
awk '/^## 束/{f=1;next} /^## /{f=0} f' docs/uitk/TODO.md | grep '^| [1-5] |'
```
Expected: 5 行とも状態 `暗転` で起点がある。`Web` の束が残っていたら停止（起点はその束のレーンが「写した画面の範囲」を知って決めるもので、このレーンが後から置くと記帳の要る範囲を取り違える。D-5）。

- [ ] **Step 6: X-prerun の基準を写す**

`$H/X-prerun/done.md` から、PR の報告ページの URL（全画面の並べ比べ）・全録画の GameUi 側の結果の表・その全録画を流した HEAD の 40 桁 SHA（`<PRERUN_HEAD>`。Task 7 Step 3 の `比較不能（変更）` と `退行` の分け目。`PRERUN_HEAD=$(grep -m1 '全録画の HEAD（GameUi）' "$H/X-prerun/done.md" | grep -oE '[0-9a-f]{40}')` で取り、`vars.sh` に書く。その行が無いときに限り X-prerun の PR の head の SHA を `gh pr view <番号> --json headRefOid` で取り、そう書く）・撤去前の最後の突き合わせの記録（A-251）を `done.md`「## 起点」へ写す。既知の Windows だけの失敗の集合（R-7）を、X-audit の `done.md` の 14 件・X-5a の `done.md` の `winunity test-all` の起点と最終 HEAD の FAIL・X-5d の `done.md` の撤去後の 19 本の FAIL の名前の和で作り、`$SP/x-switch-pr/known-win-failures.txt` と `done.md`「## 起点」に書く。X-5d の名前は `## Windows の 19 本の割り方` の下の `### 撤去後の失敗の名前（N 件）` の text のブロックにある（X-5d D-35。取り出しは X-5d 計画 Task 7 Step 5 の awk と同じ形）:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1
awk -v h="### 撤去後の失敗の名前（" 'index($0,h)==1{f=1;next} /^#/{f=0} f && !/^```/ && NF' "$H/X-5d/done.md" > "$SP/x-switch-pr/x5d-win-after-failed.txt"; wc -l < "$SP/x-switch-pr/x5d-win-after-failed.txt"
grep -E '^### 撤去後の失敗の名前（[0-9]+ 件）$' "$H/X-5d/done.md"
```
Expected: 1 行目の数が見出しの N と同じ（N が 0 なら空でよい）。見出しが無いか数が違えば、既知の集合を作れないので停止（集合が欠けると R-7 の既知の外の失敗を既知と取り違えるか、既知を外の失敗として止まる）。X-5c の `done.md` から 5c の削除コミットの SHA を写し、`git log --diff-filter=D --format=%H -1 -- moorestech_web/webui/package.json` と一致することを確かめる（以後 `<5c 削除>` と書く）。X-prerun の `done.md`「### 物差し（prerun-smoke-shots）」の小節（流し方・出力・TSV の列・写し方・比べ方と、状態の 2 つの一覧「PSNR で比べる状態」「矩形だけで比べる状態」）を `done.md`「## 起点」へ写し、「PSNR で比べる状態」の状態名を `$SP/x-switch-pr/smoke-psnr-states.txt` に 1 行 1 名で書く（2 つの一覧の和が 10 状態であることを確かめる。小節が無ければ Task 6 Step 1b の比べ方を決められないので停止。X-prerun D-51・この計画の D-56）。

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

- [ ] **Step 8b: Mac mini の Steam に moorestech とベータ playtest-staging があるかを確かめる**

```bash
ACF="$HOME/Library/Application Support/Steam/steamapps/appmanifest_1958160.acf"
test -f "$ACF" && grep -n -E '"(buildid|StateFlags|BetaKey)"' "$ACF" || echo "acf-missing"
ls "$HOME/Library/Application Support/Steam/steamapps/common/"
```
Expected: acf があり、`"BetaKey"` の行（`UserConfig` と `MountedConfig` の下）が `playtest-staging`。Task 10 は moores-playtest-release の手順 4 の前提「Mac mini に moorestech がインストール済みで、ベータ playtest-staging を選んである」（`SKILL.md:67`）に頼り、インストールとベータの選択には playtest-staging のパスワードと GUI の操作が要るのでユーザーにしかできない。起草時の Mac mini では acf が無く `steamapps/common/` が空だった（`libraryfolders.vdf` に 1958160 の行だけが残る）。acf が無いか `BetaKey` が違えば、その場で `done.md` の 1 行目を「状態: 作業中（ユーザー待ち: Mac の Steam）」にして「ユーザーの残タスク」に「Mac mini の Steam で moorestech をインストールし、プロパティ → ベータで playtest-staging を選ぶ（パスワードは playtest-staging のもの）」と書き、`../moorestech_logs` でコミット・push し、セッションの途中報告（指揮とユーザーが読む）で同じ文を頼む。止まらずに Task 2〜9 を先に進め、Task 10 の頭でもう一度この Step を流す（約 70 分の配布ビルドと検証機の後の 30 分の待ちで初めて気づくのを避ける。D-47）。

- [ ] **Step 9: Editor を起動する**

`moores-wt status` で Mac の Editor（`-batchMode` を除く）が 0 本であることを確かめ、`uloop launch --project-path ./moorestech_client` → `uloop compile --project-path ./moorestech_client` → Global Constraints 5 の「録画の前置き」。Expected: ErrorCount 0・`http://127.0.0.1:9 ja=True`。他者の Editor が居れば終わるまで待つ（落とさない）。

- [ ] **Step 10: bd と done.md**

```bash
bd create "UITK X-switch-pr: 切替 PR（記帳・全束切替・G4・ラダー6本と全録画・配布ビルドと検証機・遠隔実行・Mac Steam・IME・master への PR）" --parent moorestech-albof -t task
bd update <bd> --claim
bd dep add <bd> moorestech-s2zk.1   # X-5e が「X-switch-pr の bd があれば結ぶ」とした配布ビルドの人手検証の bd（X-5e が閉じていれば結ばずにそう書く）
```
`$H/X-switch-pr/done.md` を作る: 1 行目「状態: 作業中」、節「## 起点」「## 完了条件ごとの証拠」（R-1〜R-21 の行を空で）「## 全録画の結果」「## IME の一巡」「## 完了の定義」「## 見た目の例外の案」「## 申し送りの受け」「## 取り込み」「## 独立レビュー」「## CI の既知の失敗」「## 未確認」「## PR」「## 未完了」「## 要裁定」「## ユーザーの残タスク」「## integrator への依頼」。`../moorestech_logs/README.md` を読んでから `../moorestech_logs` でコミット・push。

- [ ] **Step 11: 前の段の申し送りを全部受ける（R-22）**

```bash
. "$SP/x-switch-pr/vars.sh" || exit 1
for id in X-prerun X-5a X-5b X-5c X-5d X-5e; do echo "### $id"; awk '/^## /{f=($0 ~ /申し送り|integrator への依頼|未確認|未完了/)} f' "$H/$id/done.md"; done > "$SP/x-switch-pr/handoffs.txt"
grep -n -E 'X-switch-pr|切替 ?PR|配布ビルド|検証機|IME|integrator' "$SP/x-switch-pr/handoffs.txt"
```
Expected: 各レーンの節が出る（節の無いレーンは「無し」と書く）。X-switch-pr 宛ての項（切替 PR・配布ビルド・検証機・IME・integrator 宛てでこのレーンの検証の範囲のもの）を全部 `done.md`「## 申し送りの受け」へ「出所・項・受けるタスク」で写す。上の「## 申し送りの受け」の表の項は表のタスクで受け、表に無い項は受けるタスクを決めて表と同じ形で足し、判断記録 D-15 に 1 行ずつ書く。設計書・実行計画の改訂（integrator 宛てで検証でないもの）は「## integrator への依頼」へ回す。受け先の無い項 0 を確かめる。X-prerun の `done.md`「integrator への依頼」の「master の未追従」の行（コミットと撮影 id。X-prerun D-37）は Task 2 Step 4 の写す候補として `$SP/x-switch-pr/master-unfollowed.md` に写す（D-42）。X-5a〜X-5e の `done.md` の削除の全件表と X-5e の `done.md`「## 撤去済みの場所」を Task 2 の前文の R-LIST と突き合わせる（前文の 2 行をここで `$SP/x-switch-pr/rlist.sh` に書き、足りないパスを足して D-15 に書く）。X-5d の `done.md` から消した名前の一覧を取り出す（Task 8 Step 5・8 で使う）:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1
for s in client server; do awk -v h="### ${s}（" 'index($0,h)==1{f=1;next} /^#/{f=0} f && !/^```/ && NF' "$H/X-5d/done.md" > "$SP/x-switch-pr/x5d-names-$s.txt"; echo "$s $(wc -l < "$SP/x-switch-pr/x5d-names-$s.txt")"; done
grep -E '^### (client|server)（[0-9]+ 名）$' "$H/X-5d/done.md"
```
Expected: 各行の数が見出しの数と同じで 0 でない（起草時の見込み client 61・server 17）。違うか 0 なら停止（空の一覧だと `/usr/bin/grep -F -f` は何にも当たらず R-12・R-23 が 0 件で素通りする。X-5d の計画の直しで再現済み）。

- [ ] **Step 12: W6-6 の基準を撮る（D-9 の `Client.Ui/**` の行の比べ直しの起点）**

このレーンの起点（X-5e の後）で W6-6 (a)(b) を撮る。(a) `$SP/x-switch-pr/gallery_capture.cs`（X-5d 計画 Task 2 Step 4 の写し。置き場の名前だけ替える）:
```csharp
var dir = "__WT__/moorestech_client/.uloop/outputs/compare/X-switch-pr/gallery-__LABEL__";
var ground = new UnityEngine.Color32(35, 29, 30, 255);
var widths = new[] { 1280, 2560 };
var heights = new[] { 720, 1080 };
var entries = Client.Ui.Editor.Gallery.UiGalleryEntryCatalog.CreateAll();
var written = 0;
for (var i = 0; i < entries.Count; i++)
{
    var entry = entries[i];
    var safeName = System.Text.RegularExpressions.Regex.Replace(entry.ComponentName, "[^A-Za-z0-9_-]", "_");
    for (var s = 0; s < widths.Length; s++)
    {
        var png = $"{dir}/{i:D3}-{entry.Family}-{safeName}-{widths[s]}x{heights[s]}.png";
        Client.Ui.Editor.Capture.UiRuntimePanelCapture.CaptureToPng(entry.CreateSample(), widths[s], heights[s], ground, Client.Ui.Editor.Capture.UiCaptureLocalization.InitializeDictionary, png);
        written++;
    }
}
return $"entries={entries.Count} png={written}";
```
Run（PlayMode を止めた状態で）:
```bash
sed -e "s#__WT__#$PWD#" -e "s#__LABEL__#base#" "$SP/x-switch-pr/gallery_capture.cs" > "$SP/x-switch-pr/gallery_capture_run.cs"
uloop execute-dynamic-code --project-path ./moorestech_client --code "$(cat "$SP/x-switch-pr/gallery_capture_run.cs")"
```
(b) 「録画の前置き」の後に `run-scenario.sh ./moorestech_client S/uitk/prerun/prerun-smoke-shots.cs` を流し、run dir の PNG 20 枚と `smoke-rects.tsv`（X-prerun D-51。列 `state res name index x y w h`、位置は根からの layout の和で脈動で動かない）を `moorestech_client/.uloop/outputs/compare/X-switch-pr/smoke-base/` へ写す（`mkdir -p moorestech_client/.uloop/outputs/compare/X-switch-pr/smoke-base && cp "<run dir>"/*.png "<run dir>/smoke-rects.tsv" moorestech_client/.uloop/outputs/compare/X-switch-pr/smoke-base/`）。続けて撮った HEAD を `SHOT_BASE=$(git rev-parse HEAD)` で `vars.sh` と `done.md`「## 起点」に書く（Task 6 Step 1b の比べ直しの起点。D-56）。
Expected: `entries=<N> png=<2N>`・`Success: true`・PNG 20 枚・`awk -F'\t' 'NR>1{print $1, $2}' moorestech_client/.uloop/outputs/compare/X-switch-pr/smoke-base/smoke-rects.tsv | sort -u | wc -l` が 20（状態×解像度）。流し方は Step 6 で写した X-prerun の物差しの小節に従い、ギャラリーで X-5d が揺れる組として記録した組（X-5d の `done.md` の W6-6 の記録）は同じ扱いにする。比べ直しは凍結の前が Task 6 Step 1b、凍結の後が D-9 の 5 行目。撮影は Task 14 Step 5 で消す

---

### Task 2: master の取り込み（I-weekly の役）・撤去済みの場所に戻ったファイルの処理・取り込んだ Web コミットの記帳

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 追従記録」の末尾）
- Modify（衝突したときだけ）: 衝突したファイル（D-6）
- Delete（戻ったときだけ）: 撤去済みの場所に master が足したファイル（D-29）
- Modify/Create（写すものがあるときだけ）: I-follow の写し先（`Client.Ui/**`・`Client.Ui.State/**`・`localization.csv`・その画面の L1 テスト。D-29）

**Interfaces:**
- Consumes: `origin/master`、G1 `python3 .github/scripts/check-uitk-web-follow.py --base <rev> --head <rev>`、「## 消えているべきもの」の C-1〜C-9、Task 1 Step 4 の G4 のコマンド
- Produces: `merge-base(origin/master, HEAD) == origin/master`、`PRE_MERGE_HEAD`（取り込みの前の HEAD。Task 5 のレビューの終わり）、取り込んだ master の Web コミットと戻ったファイルの記帳

始める前に通るべきもの: Task 1 の compile 0。終えた後に通るべきもの: R-LIST が 0 行・C-1〜C-9 が表の「期待」のとおり・G4 が exit 0・compile 0・`^Client\.Tests\.Ui\.` と取り込みで衝突したファイルのテスト・G1「違反なし」（2 回目以降は下の読み替え）。

**2 回目以降の取り込み**（R-16・Task 6〜12 の頭・Task 12 Step 1・Task 14 Step 4・D-38 で戻ったとき。台帳はすでに全束 `切替済み`・全行 `UITK`）: Step 1 は `PRE_MERGE_HEAD` を書き足さない（最初の値を残す。Step 1 の `grep -q` の形がそうする）。Step 2・4 は 1 回目と同じ。Step 3 の G1 は「違反なし」を求めず、「違反がすべて `G1-1 切替の記帳` で、その SHA が Step 1 の一覧（今回の取り込みで入った master のコミット）にある」を合格にする（G1 の base の master の台帳は全束 `Web` なので全束が「今回切り替える束」になり、新しく入った Web のコミットは SHA 付きの未記帳として出る。`uitk_web_follow/checks.py` の `switching_bundles`・`check_switch_records`）。Step 1 の一覧に無い SHA や G1-0・G1-2 が出たら停止。Step 5 の下書きは `master-records.md` の末尾へ足す。続く Task 3 は Step 1（`switch_ledger.py` は切替済みの束で「束 1 が暗転ではない」の exit 1 になる。起草時に実測）と Step 2 のコミットを飛ばし、Step 2 の G1 の 1 行（`check-uitk-web-follow.py … 2> "$SP/x-switch-pr/g1-unrecorded.txt"`）だけを流してから Step 3〜6 を行う（Step 3 の件数は今回の取り込みの分だけ）。その後は D-9 の表で再検証する（D-49）。

**起こし直し**（Task 14 Step 2 の 7. のマージ直前の確認・Task 14 Step 3 の 4. で指揮がこのレーンを起こし直したとき、停止から別のセッションで再開するとき。前のセッションの worktree は `moores-wt rm` で畳まれ、`$SP` の作業ファイルは無い。D-53）: Task 2 Step 1 の前に次を流す。
1. worktree は指揮が実行計画 §4.1 の 3.・4. の形で作って起こす（`moores-wt new uitk-lane/x-switch-pr-r<n> --from origin/feature/uitk`〈`<n>` は起こし直しの回数〉→ `cmux-agent-pane -C <その worktree>`。レーンが自分で作ると、セッションの cwd がメインクローンのままで `WT=$PWD` がそこを指しうる）。レーンはその中で Task 1 Step 1 を流す（`vars.sh` の `WT`・`H`。`pwd` が新しい worktree・HEAD が `origin/feature/uitk` と同じ・未コミット 0 でなければ止まる。push は `git push origin HEAD:feature/uitk`）。`done.md` の 1 行目を「状態: 作業中（起こし直し: <理由>）」にする
2. 次の値を `done.md` から写し、`vars.sh` に `NAME=<値>` の 1 行ずつ書く（`PRE_MERGE_HEAD` も最初の値をそのまま書く。書かずに Task 2 Step 1 へ進むと、その `grep -q` が「無い」と見て今の HEAD〈1 回目の取り込みの後〉を最初の値として書き、Task 5・13 のレビューの範囲がずれる）。1 つでも取れなければ `done.md` を「状態: 停止（起こし直しの値の欠け: <名前>）」にして指揮へ返す:

| 名前 | `done.md` の在処 |
|---|---|
| `PRERUN_HEAD`・`SHOT_BASE`・`PRE_MERGE_HEAD`・`FROZEN` | 「## 起点」 |
| `REVIEW1_HEAD` | 「## 独立レビュー」 |
| `LABEL` | R-10 の行（release.log の要所の `label=`）。`RUNS` は Global Constraints 11 の固定のパス |

3. `known-win-failures.txt` を `done.md`「## 起点」の既知の集合から、`smoke-psnr-states.txt` を「## 起点」の X-prerun の物差しの写しから作り、行数を `done.md` の件数と確かめる（違えば Task 1 Step 6 を流し直す）
4. `rlist.sh` を下の 2 行の形で書き、配列の中身は `done.md`「## 取り込み」の最後の R-LIST の 1 行にする（下の既定の 2 行のままだと Task 1 Step 11 で足したパスが落ち、そのパスへ master が足したファイルが黙って残る）。確かめ: `. "$SP/x-switch-pr/rlist.sh" || exit 1; echo "${#RLIST[@]}"` が「## 取り込み」の行のパスの数と同じ
5. Task 1 Step 11 の 2 つ目のブロック（X-5d の消した名前の取り出しと、行数が見出しの数と同じで 0 でないことの確かめ）・Step 7（検証機と winunity が同じ PC か）・Step 8 と 8b（Steam の保存資格・Mac の Steam）・Step 9（Editor の起動と録画の前置き）を流す
6. Task 2 Step 1 から「2 回目以降の取り込み」の流れで進める（`master-records.md` は無ければ作る）

撤去済みの場所の一覧（R-LIST。5a〜5e が消したり `docs/archive/` へ移したりしたもののうち、master が新しいファイルを足しうるディレクトリとファイル。X-5e は `docs/webui`・`docs/webui-parity`・`docs/webui-feature-videos-2026-06-14` を `docs/archive/` の下へ `git mv` し、`.claude/rules/ref-webui-design.md` を消す〈X-5e 計画 R-1・R-5〉。Task 1 Step 11 で 5a〜5e の `done.md` の削除の全件表と X-5e の「## 撤去済みの場所」に突き合わせ、足りなければ足す）。次の 2 行を `$SP/x-switch-pr/rlist.sh` に書く:
```bash
RLIST=(moorestech_web moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Playtest/WebUi moorestech_client/Assets/Scripts/Editor/Cef moorestech_client/Assets/StreamingAssets/WebUi scripts/setup-cef.sh scripts/setup-cef.ps1 .agents/skills/ref-webui-design .claude/rules/ref-webui-design.md docs/webui docs/webui-parity docs/webui-feature-videos-2026-06-14)   # 配列にする（この Mac の既定の zsh は引用符なしの文字列を語に割らず、1 つのパスとして渡して 0 件の偽の合格になる）
[ ${#RLIST[@]} -gt 0 ] || { echo "RLIST が空"; exit 1; }
```
R-LIST を使う**どの**コードブロックも、頭で `. "$SP/x-switch-pr/vars.sh" || exit 1; . "$SP/x-switch-pr/rlist.sh" || exit 1` を読んでから使う（シェルの変数は呼び出しをまたいで残らない。R-16・Task 6〜12 の頭・Task 12 Step 1 も同じ。Global Constraints 11）。読み忘れると zsh では空の配列が引数 0 個に展開され、`git ls-files --` が追跡ファイル全部を出して `restored.txt` に全ファイルが入る（その行を全部 `git rm` するとマージのコミットが全ファイルを消す）。2 行目はそれを止める。引用符なしの文字列 `RLIST='a b'` と `$RLIST` で渡すと、この Mac の zsh では 1 つのパスになり 0 件の偽の合格になる（起草時に実測: 配列 1147 行・文字列 0 行）。確かめ: `. "$SP/x-switch-pr/rlist.sh" || exit 1; git ls-files -- "${RLIST[@]}" | wc -l` が 0（5e の後は `docs/webui` 等は `docs/archive/` にあり、ここには当たらない）

- [ ] **Step 1: 入る Web コミットを列挙する**

```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; . "$SP/x-switch-pr/rlist.sh" || exit 1
cd "$WT" && git fetch -q origin
grep -q '^PRE_MERGE_HEAD=' "$SP/x-switch-pr/vars.sh" || echo "PRE_MERGE_HEAD=$(git rev-parse HEAD)" >> "$SP/x-switch-pr/vars.sh"; . "$SP/x-switch-pr/vars.sh" || exit 1; echo "$PRE_MERGE_HEAD"   # 最初の取り込みの前の値だけを残す（2 回目以降は書かない。`. vars.sh` は後の行が勝つので >> で足すと最初の値が消える）。done.md「## 起点」へ。Task 5 の範囲 (1) の終わり
OLD_BASE=$(git merge-base origin/master HEAD)
git log --no-merges --format='%h %s' "$OLD_BASE"..origin/master -- moorestech_web/ moorestech_client/Assets/Scripts/Client.WebUiHost/
git log --no-merges --diff-filter=A --name-only --format='%h %s' "$OLD_BASE"..origin/master -- "${RLIST[@]}"
git log --no-merges --format='%h' "$OLD_BASE"..origin/master | wc -l
```
Expected: 一覧（起草時 `0e81bbe3c3..origin/master` で `ebe26dc6e8`・その revert `622953fe5f` の 2 件を含む。足したファイルは 0 件）。2 行目に出たコミットは Step 2 で戻るファイルの出所。

- [ ] **Step 2: 取り込む（コミットの前に戻ったファイルを消す）**

```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; . "$SP/x-switch-pr/rlist.sh" || exit 1; cd "$WT"
git merge --no-ff --no-commit origin/master
git status --porcelain | grep -E '^(DD|AU|UD|UA|DU|AA|UU) '   # git の未マージの 7 種（AU・UA は片側だけが同じパスへ足した衝突）
git ls-files -- "${RLIST[@]}" > "$SP/x-switch-pr/restored.txt"; wc -l < "$SP/x-switch-pr/restored.txt"
```
（`--no-commit` で止めるのは、衝突が無いと `--no-ff` がその場でマージのコミットを作り、戻ったファイルが入ったまま次へ進むため。git は消したディレクトリの中へ master が足したファイルを衝突にせず黙って入れ、`git status` の衝突の印〈`DU` 等〉にも出ない。起草時に scratch の repo で再現した）
衝突の解き方: modify/delete（`DU`・`UD`）は、`moorestech_web/**`・`Client.WebUiHost/**` に加えて 5a〜5d が消したパス（`Client.Starter` の Web 配線・`Client.Playtest/WebUi`・`Editor/Cef`・CEF の型を使うテスト・`release-artifact.sh` の helper の検査など、各レーンの `done.md` の削除の全件表に載るもの）も全部消す側で解く（`git rm <path>`。「両方の意図を保つ」で残さない）。master 側の変更の中身は Step 4 で分ける。C# の衝突（`UU`・`AA`・`AU`・`UA`）のうち両方に残るファイルだけを opus の subagent に「両方の意図を保つ」「UITK 側の設計に従う」で解かせ、解いた内容を判断記録 D-6 の下に 1 行ずつ足す。`localization.csv` の衝突は両方の行を採る。C# 以外の両側変更（`UU`・`AA`・`AU`・`UA` のうち `.cs`・`localization.csv` 以外。5b・5c が書き換えた `moorestech_client/Packages/manifest.json`・`packages-lock.json`・`Assets/Asset/UI/Prefab/MainGameUI.prefab`・`.github/workflows/run_test.yml` は master も触る〈`origin/master` の直近 90 日で 4・11・6・20 回。起草時に実測〉）は、I-follow の前例（C2-b 計画の「両方の意図を保って解き、解いたら compile とテスト」）に合わせ、opus の subagent に「両方の意図を残す」「撤去した物〈CEF のパッケージ・CEF の Canvas 一式・Web の job・段・検査〉を戻さない」で解かせる。種類ごとの決まり: (1) prefab・シーン等の Unity のシリアライズ資産はテキストで混ぜない（AGENTS.md）。`git checkout --ours -- <path>` で feature 側を採り、master 側の変更（`git diff "$OLD_BASE" origin/master -- <path>`）を本体が起動済みの Editor へ `uloop execute-dynamic-code` で当て直す（subagent は Editor を起動しない。Global Constraints 3）(2) `manifest.json` は feature 側を採り、master 側で足し・版を上げたパッケージの行だけを足す（`jp.juha.cefunity` は戻さない）。`packages-lock.json` は `git checkout --ours` で feature 側を採り、`uloop compile` の前の更新で Package Manager に解き直させ、変わった lock を `git add` してマージのコミットに含める (3) workflow の YAML・shell・その他のテキストは両方の意図を保ち、撤去した job・段・検査を戻さない (4) 外部 repo のピン `.moorestech-external-revisions.json` は 1 行の SHA なので両方は採れない。衝突した項目（`key` が `moorestech_master` か `moorestech_client_private`。`relativePath` がその repo）ごとに、両側の `commitHash` がその repo で祖先の関係なら（`git -C <その repo> merge-base --is-ancestor <片方> <もう片方>`）新しい方を採る。分岐していればその repo で両方を含むマージのコミットを作って push し、その repo の PR を作り（AGENTS.md「関連リポジトリ」: ピンは別 repo の PR が指す push 済みのコミット）、そのコミットへピンする。マスタデータが両立せずマージを作れなければ下の停止（起草時は分岐点 `0e81bbe3c3` の後に feature 側のピンの変更 0 回・master 側 8 回で、起きにくい）。解いた後に該当の検査を流す: compile・G1・G4・C-1〜C-9（manifest・lock は C-3、workflow は C-6）・関係するテスト（prefab はその画面のテストのクラスのフィルタとその画面を通るラダー、`.github`・`scripts/playtest` は Task 6 Step 3 の契約テスト 5 本）。解けないもの（当て直せない prefab・両立しない意図）は `done.md` を「状態: 停止（取り込みの衝突: <path>）」にし、両側の差と試したことを `done.md`「## 取り込み」に書いて指揮へ返す（終わった撤去レーンへは回さない）。解いた内容は D-6 の下に 1 行ずつ足す（D-46）。master 側の `BugReportManifest.SchemaVersion`（`git show origin/master:moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportManifest.cs | grep -n 'SchemaVersion ='`）が feature 側の版以上なら、X-5a 計画 D-28 と同じ形で「webUiHealth を外した版」を次の番号へ付け直し（直す先は X-5a の `done.md`「integrator への依頼」の一覧）、C-2 の期待をその番号へ読み替えて D-6・D-15 に書く。
戻ったファイル（`restored.txt` の行）: 中身を Step 4 の分け方にかける（どのコミットが足したかは Step 1 の 2 行目）。そのうえで `docs/webui*` の下の文書は X-5e の行き先へ `git mv -- <path> docs/archive/<path から docs/ を外したもの>` で移し（文書は写す先が無く、5e が撤去前の文書を archive に残した形にそろえる）、それ以外は全部 `git rm -q -- <path>` で消し、`git ls-files -- "${RLIST[@]}" | wc -l` が 0 になってから `git commit --no-edit -m "merge: origin/master を feature/uitk へ取り込む（X-switch-pr）"`。消した・移したパスの一覧と、使った R-LIST の 1 行（Task 14 Step 2 のマージ直前の確認で指揮が使う）を `done.md`「## 取り込み」に書く。

- [ ] **Step 3: 取り込みの後の検査**

Run: 「## 消えているべきもの」の C-1〜C-9 と Task 1 Step 4 の G4 → `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 540`（Mac の実績は 600 秒の内。超えたら `^Client\.Tests\.Ui\.(Screens|State)\.` と `^Client\.Tests\.Ui\.(?!Screens\.|State\.)` に割る）
続けて `python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD; echo "exit=$?"`（1 回目は「違反なし」、2 回目以降は前文の読み替え）。
Expected: C-1〜C-9 が表の「期待」のとおり・G4 exit 0・ErrorCount 0・失敗 0（件数を `done.md` へ）。compile が取り込みで落ちたら、`error CS` の行のファイルと型で分ける: 消した型（Web ホスト・CEF・全体スイッチの型）への参照なら、master が残るファイル（`Client.Starter` の登録など）に足した行が衝突せずに自動でマージされて入ったもの（起草時の `0e81bbe3c3..origin/master` でも `MainGameModelRegistration.cs` を触っている）なので、その master のコミットを Step 4 の写すもの（I-follow）として扱い、このレーンが参照を外すか UITK 側へ付け替える。それ以外（master の API の変更に UITK 側が追いつかない等）は Step 2 の C# の衝突と同じく opus の subagent に「両方の意図を保つ」「UITK 側の設計に従う」で直させる。どちらも直した内容を D-6 の下に 1 行ずつ足し、直せなければ Step 2 の停止（D-54）。C-1〜C-9 が取り込みの後に外れたら、原因は取り込みの解き方なので Task 1 Step 3 の「持ち主のレーンへ bd」にせず、このレーンが Step 2 の決まりで解き直す（解けなければ Step 2 の停止）。G4 が取り込みで入った master のファイルで exit 1 になったら（このレーンが I-follow で写したファイルの違反は Step 4 の (2) でこのレーンが直す。D-57）、X-5e は済んでいるので X-5e へは回さず、改名・許可リストの持ち主として I-fix へ bd を `--parent <bd>` で立てて指揮へ返し、`done.md` を「状態: 停止（G4 の違反 n 件の修正待ち）」にする（このレーンは許可リストを書かない）。

- [ ] **Step 4: 入った Web コミットと戻ったファイルを分け、写すものは I-follow の手順で写す**

Step 1 の各コミットを `git show --stat <sha> -- moorestech_web/ moorestech_client/Assets/Scripts/Client.WebUiHost/` と本文で読み、戻ったファイル（`restored.txt`）は `git show origin/master:<path>` で中身を読み、X-prerun が「master の未追従」として申し送ったコミット（Task 1 Step 11 の `master-unfollowed.md`。X-prerun D-37）も同じ候補に入れて（取り込み済みなら Step 1 の一覧にも出る。出ていなくても中身を読む）、次に分ける:
- `捨てた`: UITK に写すものが無い（revert で打ち消し合う組・Web の内部の描画・mock-host・e2e・Web の依存の更新・CEF への手当て〈裁定「CEF 由来の不具合は直さない」〉）
- `写した`（写し先が既にある）: 同じ取り込みで UITK 側に写し先がある（例: `localization.csv` の追加に連動した `localizationKeys.ts` の再生成だけで、キーを使う UITK 側が既にある）
- 写すもの（I-follow の手順）: Web の画面・機能・文言・挙動の変更で、UITK 側に写し先が無い

写すものは停止せず、このレーンが I-follow の手順（実行計画 §1.4 の I-follow-n 行・設計書 §3.3 (b)）で写す。1 件ずつ bd を `--parent <bd>` で立て（Web のコミット・変わった画面・UITK の写し先）、opus の subagent に実装させる（依頼文に「バックグラウンドのポーリングで待たず全部前景で実行する／区切りごとにコミットする」と、ref-uitk-design スキル・AGENTS.md の規約に従うことを書く）。手順: (1) 写し先の UITK の画面・部品へ変更を写し、L1 テスト（`/// <web-test>` の付く Web のテストがあればその写し）を足す (2) `uloop compile` 0・その画面のテストのクラスのフィルタで失敗 0・G4（Task 1 Step 4 のコマンド）が exit 0。写したファイルに G4 の語（`moorestech_web` を含む出所の注記など。写し元は 5c の後の master なので `moorestech_web/webui/src/…` と書きやすい）が入ったら、このレーンが語を直す（出所の注記は X-5e がそろえた `web@<sha10>/…` の形〈X-5e R-11〉に。D-57）。語がどうしても要って許可リストへ足すしかないときだけ、このレーンは許可リストを書かないので I-fix へ bd を立てて停止 (3) その画面の録画（Global Constraints 5 の前置きの後に、その画面を通るシナリオ。無ければラダーのうちその画面を通る本）が `Success: true` (4) master の worktree（`moores-wt new <名前> --from origin/master --no-editor`）でその画面の Web の参照を撮り直し（`web-<sha>`）、UITK の同じ状態を撮って 720p と 2560×1080 の比較 HTML と差の表を作る（W6-4 の 2560×1080 の撮り方。比べ方は `.agents/skills/ref-uitk-design/references/compare-procedure.md`）(5) 差は直す。直せない差は R-19 の LX-218・219 の案へ書く（行と印は I-fix） (6) 台帳の記録は判定 `写した`、4 列目に「I-follow（X-switch-pr Task 2）で <UITK の写し先> <コミット>」（判定の欄に括弧書きを足すと台帳のパースエラー。`ledger.py:190`）。写したコミットは Task 5 のレビューの対象に入る（Task 5 Step 1）。写す量が 1 件で Editor 1 本の 1 日に収まらない（新しい画面の丸ごとの追加など）と分かったら、そのときだけ `done.md` を「状態: 停止（I-follow の規模: <sha>）」にして指揮へ返す（D-29）。

- [ ] **Step 5: 記帳する**

各コミットの持ち主の束を G1 の出力で知る（束はまだ暗転なので、G1 は件数しか出さない。SHA は Task 3 Step 3 でまとめて出るので、ここでは Step 4 の分けを `$SP/x-switch-pr/master-records.md` に `| <sha10> | <判定> | <理由・写し先> |` で下書きし、Task 3 Step 4 で束ごとの行に展開する）。戻ったファイルは、それを足した master のコミットの行の 4 列目に「撤去済みの場所に戻った <path> を git rm（中身は <写し先 or 写すもの無し>）」と書き足す。

- [ ] **Step 6: コミットと push**

```bash
git push origin HEAD:feature/uitk
```
（マージコミットは Step 2 で作った。衝突を解いたファイルと戻ったファイルの削除はマージコミットに入る。I-follow の写しは別のコミット）

---

### Task 3: 台帳の切替（全束 `切替済み`・全行 `UITK`）と Web コミットの記帳 → G1 0 件

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 束」「## 行」「## 追従記録」）

**Interfaces:**
- Consumes: Task 2 の `$SP/x-switch-pr/master-records.md`、`<5c 削除>`、G1
- Produces: 台帳の全束 `切替済み`・全行 `UITK`・未記帳 0

始める前に通るべきもの: Task 2 の G1「違反なし」（2 回目以降は Task 2 の前文の読み替えで、Step 1 と Step 2 のコミットを飛ばす）。終えた後に通るべきもの: G1 が全束「未記帳 0 件」で exit 0・real_ledger テスト OK・G1 の自己テスト OK。

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
grep -o '束 [1-5] の暗転の起点より後のコミット [0-9a-f]*' "$SP/x-switch-pr/g1-unrecorded.txt" | awk '{print $2, $4}' | sort -u > "$SP/x-switch-pr/unrecorded.tsv"
awk '{print $1}' "$SP/x-switch-pr/unrecorded.tsv" | sort | uniq -c
```
（`grep -o` の結果は「束／n／の暗転の起点より後のコミット／sha」の 4 欄なので SHA は `$4`。起草時に `checks.py:64` の文面を流して確かめた）
Expected: `unrecorded.tsv` の各行が「束 sha10」の 2 欄。束ごとの件数（起草時の実測: 束 1〜3 は暗転の後の再生成〈77f336e432・5bf23c60a7・23bf17e0bb・237eb39440・52015d7931 など〉が台帳の「## 追従記録」に記帳済みで、今の G1 は束 1〜3 とも「未記帳 0 件」。数えるのは、その後に Web を触った X-5a〜5e の撤去〈`<5c 削除>` は全束〉・束 4・5 が暗転してからの再生成と、Task 2 の取り込みの分）。

- [ ] **Step 4: 1 件ずつ判定して行を書く**

`unrecorded.tsv` の各 SHA を `git show --stat --format='%h %s' <sha> -- moorestech_web/` で読み、次の規則で「## 追従記録」の表の末尾へ `| <束> | <sha10> | <判定> | <写し先・理由> |` を足す（同じコミットでも束ごとに 1 行。セルに `|` を書かない。判定の欄は `写した` か `捨てた` の 2 語のどちらかだけで、括弧書き・PR 番号・理由は 4 列目に書く〈`ledger.py:190`。`捨てた` は 4 列目が空だとパースエラー `:193`〉）:

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

Run: Task 1 Step 4 で写したコマンド（期待 `python3 .github/scripts/check-uitk-no-web.py --root .; echo "exit=$?"` と `python3 -m unittest discover -s .github/scripts/tests -p 'test_uitk_no_web.py'`）
Expected: 許可リスト外 0 件・exit 0、unittest OK。exit 1（`G4: <path>:<line>: <語>: …` の行）か exit 2（許可リストの書式誤り・腐った行）なら中身を `done.md` に貼る。違反の行がこのレーンの I-follow の写しで書いたファイル（Task 2 Step 4 の写したコミットで触ったファイル）なら、このレーンが語を直して compile・G4 を流し直す（Task 2 Step 4 の (2)。D-57）。それ以外（master の取り込みで入ったファイル・許可リストへ足すしかない語・exit 2）は、改名・許可リストの持ち主へ bd を立てて停止（X-5e は済んでいるので I-fix へ回す。このレーンは許可リストを書かない）。

- [ ] **Step 2: 完了の定義 §5.1-2 の grep**

```bash
test ! -e moorestech_web && test ! -e moorestech_client/Assets/Scripts/Client.WebUiHost && echo "no-webhost"
git grep -n -E 'Kestrel|Microsoft\.AspNetCore' -- '*.cs' '*.asmdef' 'moorestech_client/Assets/packages.config' | grep -v -E '^[^:]+:[0-9]+:[[:space:]]*//' | wc -l
git grep -n -E 'Kestrel|Microsoft\.AspNetCore' -- '*.cs' '*.asmdef' 'moorestech_client/Assets/packages.config' | grep -E '^[^:]+:[0-9]+:[[:space:]]*//'
```
Expected: `no-webhost`・0。3 行目（コメントに残る語）は件数と場所を `done.md` に写すだけで合否に使わない。G4（Step 1）が exit 0 なので、残った行は X-5e の許可リストが理由付きで持つか、すでに消えている（起草時の HEAD では `RemoteExecHttpListener.cs:14-15,45-46`・`EventModeStartGate.cs:22-23` が当たる。X-5c は `RemoteExecHttpListener.cs:46` を 5e の掃除へ申し送った〈X-5c 計画 D-6〉）。パスを `*.cs`・`*.asmdef` に絞るのは `Assets/Packages` の DLL・xml を舐めないため（DLL まで舐めると 5d の前の HEAD で 120 秒を超えた。D-24）。

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
- Consumes: moores-code-review スキル（base `9f32a03390`）、Task 2 の `PRE_MERGE_HEAD` と取り込みのマージコミット、統合計画 §5.1、実行計画 §1.5 の X-switch-pr 行
- Produces: `done.md`「## 独立レビュー」（指摘の数・処遇・要裁定）、凍結の候補の HEAD

始める前に通るべきもの: Task 4 が全部 0。終えた後に通るべきもの: compile 0・自動適用が触れたファイルのテスト（クラスのフィルタ）・G1 0・G4 0。

- [ ] **Step 1: moores-code-review を X-audit の後の全部に回す**

moores-code-review スキルを、feature 側のコミットだけに絞って実行する（X-audit #1656 が `9f32a03390` までの全体を見た。それより後の I-fix・S2〜S5・X-prerun・X-5a〜5e・I-follow の写し・台帳）。Task 2 の取り込みの後の `9f32a03390..HEAD` は、`9f32a03390` が master の祖先でないので master 側の非 UITK の作業（起草時の模擬で 68 コミット・非 UITK の .cs 32 本）を丸ごと含み、自動適用が master の作業者のコードに当たる（D-31）。対象は (1) 範囲 `9f32a03390..$PRE_MERGE_HEAD` の feature 側の変更（I-weekly や各レーンの取り込みで master が `feature/uitk` に入っていれば、その master 側の変更を除く。D-43）、(2) 取り込みの後の feature 側の変更（I-follow の写し・台帳）、(3) master を運ぶマージごとの衝突の解き方（直接の取り込みは `git show --remerge-diff <sha>` と同じで、衝突の解き方と戻ったファイルの `git rm` だけが出る。I-weekly の PR の形はその枝の自分のコミットも出る。D-52）。スキルの Step 1 は `review_diff.py <base>..<head>`（`git diff` に引数を渡すだけ）で patch を作り、Step 7.5 の bug-pass も「Step 1 と同じ base で末尾を最終 HEAD にした PR 全体 diff」を `review_diff.py` で作り直して Critical を自動で当てる（`.agents/skills/moores-code-review/SKILL.md:146`）。base に `9f32a03390` を渡すと bug-pass の patch に master 側の作業が入り、D-31 が避けた自動適用が bug-pass で起きる。また `--remerge-diff` は `git diff` では作れない。そこで本体の patch も bug-pass の patch も次の `$SP/x-switch-pr/review_patch.py` で作る（first-parent の鎖を「master のコミットを運ぶマージ」で区切り、区切りの間は `review_diff.py` と同じ除外の `git diff`、区切りのマージは同じ除外で「1 番目の親と運ばれた master の最新のコミットの自動マージ〈`git merge-tree --write-tree`。衝突の印を含む木〉とマージの結果との差」。最後の区間は末尾の HEAD で終わるので、行番号は HEAD と合う。「master を運ぶ」は 2 番目の親の側に 1 番目の親に無い master のコミットがあることで見分ける。直接の取り込み〈2 番目の親が master の祖先〉ではこの差は `--remerge-diff` と同じ。I-weekly の PR〈2 番目の親が「master を取り込んだ後に台帳を積んだ枝」の先端で、master の祖先ではない。実行計画 §1.6〉や、自分の中で master を取り込んだレーンの PR では、その枝の自分のコミットと衝突の解き方だけが出る。D-52）:
```python
# レビューの patch を feature 側の変更だけで作る（除外は review_diff.py と同じ・master を運ぶマージは自動マージとの差）
# Build the review patch from feature-side changes only (review_diff.py excludes; master-carrying merges diff against an auto-merge)
import subprocess
import sys

sys.path.insert(0, ".agents/skills/moores-code-review/scripts")
from review_diff import exclude_pathspecs

start, end, out = sys.argv[1:4]
excludes = ["--", ".", *exclude_pathspecs()]


def git(*args, ok=(0,)):
    done = subprocess.run(["git", *args], capture_output=True, text=True)
    if done.returncode not in ok:
        sys.exit(f"git {' '.join(args)} failed: {done.stderr.strip()}")
    return done.stdout


def carried_master(sha):
    # 2 番目の親の側が first-parent に無い master のコミットを運ぶなら、その最新の master のコミットを返す
    # If the second-parent side brings master commits missing from the first parent, return the newest one
    parents = git("rev-list", "--parents", "-n", "1", sha).split()[1:]
    if len(parents) < 2:
        return None
    base = git("merge-base", parents[1], "origin/master", ok=(0, 1)).strip()
    if not base:
        return None
    inside = subprocess.run(["git", "merge-base", "--is-ancestor", base, parents[0]]).returncode == 0
    return None if inside else base


parts = []
segment_start = start
merges = 0
for sha in git("rev-list", "--first-parent", "--reverse", f"{start}..{end}").split():
    carried = carried_master(sha)
    if carried is None:
        continue
    # 1 番目の親と運ばれた master の自動マージ（衝突の印を含む木）と、マージの結果との差＝feature 側の変更と衝突の解き方
    # Diff the auto-merge of first parent and carried master (conflict markers kept) against the merge: side changes plus resolutions
    tree = git("merge-tree", "--write-tree", "--no-messages", f"{sha}^1", carried, ok=(0, 1)).split()[0]
    parts.append(git("diff", f"{segment_start}..{sha}^1", *excludes))
    parts.append(git("diff", tree, sha, *excludes))
    segment_start = sha
    merges += 1
parts.append(git("diff", f"{segment_start}..{end}", *excludes))
open(out, "w", encoding="utf-8").write("".join(parts))
print(f"intake_merges={merges} bytes={sum(len(x) for x in parts)}")
```
Run（`$WT` で）:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT"
grep -q '^REVIEW1_HEAD=' "$SP/x-switch-pr/vars.sh" || echo "REVIEW1_HEAD=$(git rev-parse HEAD)" >> "$SP/x-switch-pr/vars.sh"; . "$SP/x-switch-pr/vars.sh" || exit 1; echo "$REVIEW1_HEAD"   # done.md「## 独立レビュー」へ。Task 13 の範囲の始まり
python3 "$SP/x-switch-pr/review_patch.py" 9f32a03390 HEAD "<$RUNDIR の実値>/patch.diff"
n=0; for m in $(git rev-list --first-parent --merges 9f32a03390..HEAD); do [ -n "$(comm -12 <(git rev-list "$m^1..$m^2" | sort) <(git rev-list origin/master | sort) | head -1)" ] && n=$((n+1)); done; echo "master-carrying=$n"   # スクリプトと別の数え方で同じ数になること
```
Expected: `intake_merges=<n>` と `master-carrying=<n>` が同じ数（違えば patch を使わず `done.md` に両方の出力を書いて停止。master の作業者のコードがレビューと自動適用に入るため）。起草時の模擬（`origin/feature/uitk` に `origin/master` を取り込んだコミットを作って流した）では `9f32a03390..HEAD` の `git diff` が 219 ファイルのところ、patch は 10 ファイルで、改める前の `--remerge-diff` の版とバイト単位で同じだった。I-weekly-1 のマージ `5e168777b0`（#1603）で `1c0836907a..5e168777b0` を流すと、改める前の版は `intake_merges=0` で 43 ファイル（master の車両撤去などの `.cs` を含む）、今の版は `intake_merges=1` で 26 ファイル（I-weekly の枝の台帳・文書のコミット 2 件と、枝の中の取り込み `d03e06663b` の衝突の解き方〈`--remerge-diff` の 29 ファイルの内〉だけ）・2 つの数え方とも 1。取り込みの無い今の `origin/feature/uitk` では `review_diff.py 9f32a03390..origin/feature/uitk` の出力とバイト単位で同じ（どれも起草時に scratch の clone で実測。D-52）。スキルの Step 1 の「作業範囲を特定」はこの patch を PATCH_PATH にし、`build_workflow_args.py` には `--base-ref` を渡さない（未指定なら final.diff は patch.diff と HEAD からの未コミット分の連結になる〈`scripts/review_workflow/40_apply.js:22`〉。`9f32a03390` を渡すと final.diff が `git diff 9f32a03390 -- <触ったファイル>` になり、同じファイルへの master 側の変更が入る）。Step 7.5 の bug-pass の `$BPDIR/patch.diff` も同じスクリプトで `python3 "$SP/x-switch-pr/review_patch.py" 9f32a03390 HEAD "$BPDIR/patch.diff"`（末尾は裁定反映の後の HEAD。未コミット分があればスキルの規定どおり `review_diff.py --cached` と引数なしを後ろへ連結）で作り、bug-pass の `build_workflow_args.py` にも `--base-ref` を渡さない（D-50）。master 側から来たコードへの指摘は自動適用せず、bd で master の持ち主へ回して `done.md`「## 独立レビュー」に書く。記録は `../moorestech_logs/harness/` へ（スキルの規定）。設計判断は agent 既定で推奨案を採り、判断記録に「agent 既定（moores-code-review Dn）」と書く。ゴール・裁定から外れるものだけ `done.md`「要裁定」。自動適用・反映がソースの判定経路・条件式に触れたら、そのファイルを持つ画面のラダーを Task 7 で必ず流す（Task 7 は凍結の後なので、ここで直ったものも含めて全部を流す）。

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
- Consumes: Task 5 の HEAD、Task 1 Step 12 の W6-6 の基準と `SHOT_BASE`・`smoke-psnr-states.txt`（Step 1b）、X-5d 計画 Task 2 Step 1 の 19 本の表と割り方・集計の python（と X-5d の `done.md`「## Windows の 19 本の割り方」の割った本）、Task 1 Step 6 の `known-win-failures.txt`
- Produces: **凍結した SHA**（`FROZEN=$(git rev-parse HEAD)`。以後のビルド・録画・検証の対象）を `done.md`「## 起点」へ

始める前に通るべきもの: R-16 の確認（`. "$SP/x-switch-pr/vars.sh" || exit 1; . "$SP/x-switch-pr/rlist.sh" || exit 1; cd "$WT" && git fetch -q origin && git log --oneline HEAD..origin/master -- "${RLIST[@]}"` が空。空でなければ Task 2 へ戻る。Task 7〜12 の頭の「R-16 の確認」も同じ 1 行）・HEAD が push 済み（`git rev-parse HEAD` と `origin/feature/uitk` が同じ）。終えた後に通るべきもの: 下の Step 1〜3（Step 1b の比べ直しを含む）。

- [ ] **Step 1: compile**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0。

- [ ] **Step 1b: 見た目の比べ直し（凍結の前。D-33・D-56）**

このレーンが `Client.Ui` の系統に触れうる経路（Task 2 の取り込みの衝突の解決・I-follow の写し・取り込みの後の compile の直し、Task 5 の moores-code-review の自動適用、Task 7 Step 4 で取り込む I-fix）は、どれも最初の凍結（Step 4）より前に起き、凍結の後の HEAD の変化を受ける D-9 の表には入らない。そこで凍結の前に（PlayMode を止めた状態で）、Task 1 Step 12 の基準から HEAD までに触れたかを見て、触れていれば撮り直して比べる:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT"
git diff --name-only "$SHOT_BASE" HEAD -- moorestech_client/Assets/Scripts/Client.Ui moorestech_client/Assets/Scripts/Client.Ui.State moorestech_client/Assets/Scripts/Client.Ui.Editor "moorestech_client/Assets/UI Toolkit" | tee "$SP/x-switch-pr/visual-changed.txt" | wc -l
```
（USS は `Client.Ui`・ギャラリーの見本の USS は `Client.Ui.Editor`・テーマの tss は `Assets/UI Toolkit` にある。起草時に `git ls-tree` で確かめた）
0 なら `done.md`「## 起点」の W6-6 の行に「凍結の前の比べ直し: 触れたファイル 0（`SHOT_BASE`..`<HEAD 10 桁>`）」と書いて Step 2 へ。0 でなければ:

(a) ギャラリー:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT"
rm -rf moorestech_client/.uloop/outputs/compare/X-switch-pr/gallery-after
sed -e "s#__WT__#$PWD#" -e "s#__LABEL__#after#" "$SP/x-switch-pr/gallery_capture.cs" > "$SP/x-switch-pr/gallery_capture_run.cs"
uloop execute-dynamic-code --project-path ./moorestech_client --code "$(cat "$SP/x-switch-pr/gallery_capture_run.cs")"
B=moorestech_client/.uloop/outputs/compare/X-switch-pr/gallery-base; A=moorestech_client/.uloop/outputs/compare/X-switch-pr/gallery-after
diff <(ls "$B") <(ls "$A") && echo "組が同じ"
ls "$B" | while read f; do r=$(ffmpeg -hide_banner -nostats -i "$B/$f" -i "$A/$f" -lavfi psnr -f null - 2>&1 | grep -o 'average:[^ ]*'); [ "$r" = "average:inf" ] || echo "DIFF $f $r"; done; echo compared
```
(b) スモーク: 「録画の前置き」の後に Task 1 Step 12 の (b) と同じに流し、PNG と `smoke-rects.tsv` を `moorestech_client/.uloop/outputs/compare/X-switch-pr/smoke-after/` へ写して（先に `rm -rf` で前の回の分を消す）:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT"
B=moorestech_client/.uloop/outputs/compare/X-switch-pr/smoke-base; A=moorestech_client/.uloop/outputs/compare/X-switch-pr/smoke-after
diff "$B/smoke-rects.tsv" "$A/smoke-rects.tsv" > "$SP/x-switch-pr/smoke-rects.diff"; echo "rects-exit=$?"
diff <(ls "$B") <(ls "$A") && echo "組が同じ"
grep -v '^$' "$SP/x-switch-pr/smoke-psnr-states.txt" | while read st; do for res in 1280x720 2560x1080; do f="smoke-$st-$res.png"; r=$(ffmpeg -hide_banner -nostats -i "$B/$f" -i "$A/$f" -lavfi psnr -f null - 2>&1 | grep -o 'average:[^ ]*'); [ "$r" = "average:inf" ] || echo "DIFF $f $r"; done; done; echo compared
```
Expected: (a) `組が同じ`・`compared`・`DIFF` 0 行（I-follow で意図して変えた部品の組は、Task 2 Step 4 の I-follow の比較 HTML を証拠にして表に書き、`DIFF` に数えない。X-5d が揺れる組として記録した組は X-5d と同じ扱い）。(b) `rects-exit=0`（全 10 状態・両解像度の矩形が一致。X-prerun D-51）・`組が同じ`・「PSNR で比べる状態」の PNG で `DIFF` 0 行・`compared`。「矩形だけで比べる状態」の PSNR は記録だけ。外れた組は「状態名か部品・解像度・矩形の差の行か PSNR・差の場所（`ffmpeg -i a -i b -filter_complex blend=all_mode=difference` の画像を目で見た所見）・原因」を `done.md` に表で書き、`visual-changed.txt` のどの変更が原因かを決める。このレーンの変更（衝突の解き方・自動適用・取り込み後の直し）が原因なら直して撮り直す。I-follow の写しの意図した変化なら I-follow の比較 HTML を証拠に書く。I-fix の取り込みが原因なら持ち主へ bd を立てて停止。比べた HEAD を `done.md` に書く（凍結の後に `Client.Ui` の系統が変わったら D-9 の 5 行目で同じ 2 つを流し直す）

- [ ] **Step 2: テスト全体（Windows で 1 本 10 分未満に分けて前景で 1 本ずつ・既知の失敗は Mac で流し直す）**

`winunity test-all` は使わない（`~/bin/winunity` は ssh で Windows のジョブの終わりまで同期に待ち〈`:87-90`〉、6 本を順に流すので通しで約 40 分〈`$H/S2-ladder/done.md:11`〉。前景の 600 秒で切れると `summary-all.txt` が回収できない。D-28）。X-5d 計画 Task 2 Step 1 の 19 本の表（否定先読みの補集合で `Client.Tests` の全部・`^Tests\.` の全部・そのどちらでもない名前〈namespace の無い `SchemaWatcherPersistenceTest`〈`Client.Tests/SchemaWatcher/SchemaWatcherPersistenceTest.cs:6`〉とパッケージのテスト。W-C6〉を重なりなく覆う。X-5d が CI #1656 の xml の全 8,667 名で各名前がちょうど 1 本に当たることを確かめた。X-5d D-26）をそのまま使う。X-5d の `done.md`「## Windows の 19 本の割り方」に割った本（`W-E4a` 等）があればその割り方で流す。W-E5 は X-5d と同じく最初から W-E5a・W-E5b の 2 本に割って流す（`…\.Ui\.` の 1 本は実績 232 秒で `--timeout 240` との余白が 8 秒しかない。X-5d D-37）ので、割らなければ流すのは 20 本。

1. 初回の準備（**本体**。割れない）: このブランチで Windows に worktree が無ければ、本体が Bash の `run_in_background` で `~/bin/winunity compile feature/uitk --name xsw` を 1 回起動し、完了の通知で受ける（準備だけで最大 494 秒の実績）。続けて表の最初の `--editor` の本 W-E1 も、GUI Editor の起動を含むので本体が `run_in_background` で 1 回起動し通知で受ける
2. 残りの本を表の順に前景で 1 本ずつ（`--editor` の 9 本〈W-E5a・W-E5b を含む〉を先に続けて）: `~/bin/winunity test feature/uitk --regex '<regex>' [--editor] --name xsw --timeout <秒> 2>&1 | tee "$SP/x-switch-pr/win-<#>.txt"`（`--editor` の本は `--timeout 240`、batchmode の本は `--timeout 480`。Bash の timeout は 600000）

| # | `--regex` | `--editor` |
|---|---|---|
| W-E1 | `^Client\.Tests\.EditModeInPlayingTest\.BugReport\.BugReportBundleWriteTest` | 付ける |
| W-E2 | `^Client\.Tests\.EditModeInPlayingTest\.(BugReport\.(?!BugReportBundleWriteTest)\|Playtest\.)` | 付ける |
| W-E3 | `^Client\.Tests\.EditModeInPlayingTest\.(Belt\|Block(?!Inventory\.)\|RemoteExec\.)` | 付ける |
| W-E4 | `^Client\.Tests\.EditModeInPlayingTest\.BlockInventory\.` | 付ける |
| W-E5a | `^Client\.Tests\.EditModeInPlayingTest\.Ui\.GameUiInputTest` | 付ける |
| W-E5b | `^Client\.Tests\.EditModeInPlayingTest\.Ui\.(?!GameUiInputTest)` | 付ける |
| W-E6 | `^Client\.Tests\.EditModeInPlayingTest\.(Util\|MachineUpgrade\|MapObjects)\.` | 付ける |
| W-E7 | `^Client\.Tests\.EditModeInPlayingTest\.(?!(BugReport\|Playtest\|RemoteExec\|BlockInventory\|Ui\|Util\|MachineUpgrade\|MapObjects)\.\|Belt\|Block)[A-L]` | 付ける |
| W-E8 | W-E7 の末尾を `[M-Z]` にしたもの | 付ける |
| W-C1 | `^Client\.Tests\.Ui\.` | — |
| W-C2 | `^Client\.Tests\.Map\.` | — |
| W-C3 | `^Client\.Tests\.BugReport\.` | — |
| W-C4 | `^Client\.Tests\.(?!EditModeInPlayingTest\.\|BugReport\.)[A-L]` | — |
| W-C5 | `^Client\.Tests\.(?!Map\.\|Ui\.)[M-Z]` | — |
| W-C6 | `^(?!Client\.Tests\.\|Tests\.)` | — |
| W-S1 | `^Tests\.UnitTest\.Game\.MapGeneration\.(Tiling\|Facade\|Provisioning\|Placement)\.` | — |
| W-S2 | `^Tests\.UnitTest\.Game\.MapGeneration\.(Visual\|Surface)\.` | — |
| W-S3 | `^Tests\.UnitTest\.Game\.MapGeneration\.(?!(Tiling\|Facade\|Provisioning\|Placement\|Visual\|Surface)\.)` | — |
| W-S4 | `^Tests\.UnitTest\.(?!Game\.MapGeneration\.)` | — |
| W-S5 | `^Tests\.(?!UnitTest\.)` | — |

（表の `\|` は Markdown の表のための書き方で、コマンドでは `|`。割り方は X-5d 計画 Task 2 Step 1 の「割り方」と同じ: 1 本が `timedOut=True` か件数無しで終わったら、その本だけを次の名前の区切りの頭文字で 2 本に割って流し直し、元の本の `.txt` は `.timedout` へ改名して集計から外す。前景の 1 本が 10 分で切れて `[mac] results:` が出なかったら同じ本を流し直さず〈Windows でジョブが続いている〉、X-5d 計画の同じ段の `ssh winbuild … _runs` の手順で run を探して結果を取る。1 クラスで上限を超えるものは `--class <名前> --timeout 1500` にして本体が `run_in_background` で 1 回起動し完了の通知で受ける）

3. 集める（X-5d 計画 Task 2 Step 1 の 3. の集計の python をそのまま、出力先を `$SP/x-switch-pr/win-all.txt` に替えて流す）。Expected: `TOTAL` の行が流した本の数（割らなければ 20）・各本の total が 0 でない・`NO RESULT` と `timedOut=True` が 0 行。失敗の名前（`$SP/x-switch-pr/win-failed.txt`）が Task 1 Step 6 の `known-win-failures.txt` に全部含まれ、それ以外が 0（件数と既知の件数を `done.md` へ。既知が直っていて 0 ならそれも書く）。既知の外の失敗があれば持ち主へ bd を立てて停止（D-23）
4. 既知の名前を Mac で流し直す: `known-win-failures.txt` のクラスごとに `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value '<クラスの完全名>' --timeout-seconds 540`。Expected: 全部失敗 0（Mac でも落ちたものは Windows だけの失敗ではないので既知に数えず、持ち主へ bd を立てて停止）

winunity が使えないとき（検証機と同じ PC で使用中など）は、同じ 19 本を Mac の Editor で `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value '<regex>' --timeout-seconds 540` で 1 本ずつ前景で流す（`--editor` の本も同じ。1 本が 540 秒で切れたら同じ割り方で割る）。

- [ ] **Step 3: 配布の契約テスト**

```bash
for t in test-release-playtest-mac.sh test-release-playtest.sh test-release-playtest-origin.sh test-verify-on-windows.sh test-remote-exec-offline.sh; do bash "scripts/playtest/tests/$t" || echo "FAIL $t"; done
```
Expected: 5 本とも exit 0（最後の行は 4 本が `PASS: …`、`test-remote-exec-offline.sh` だけ `OK`。起草時に実測）、`FAIL` 0（`test-release-playtest-mac.sh` は 5b が反転した CEF 0 のケースを含む）。

- [ ] **Step 4: 凍結を記録する**: `FROZEN=$(git rev-parse HEAD)` を `vars.sh`（`FROZEN=` の行を置き換える。Global Constraints 11）と `done.md`「## 起点」に書き（Task 2 の前文の「起こし直し」が読む）、`../moorestech_logs` でコミット・push。以後のタスクで HEAD が変わったら（取り込み・レビューの適用）、D-9 の表で再検証の範囲を決め、`FROZEN` を書き直す

---

### Task 7: CEF の無いプロジェクトでラダー 6 本と全録画（W6-17）

**Files:** なし（`moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/` は gitignore 下で、Editor を閉じても消えない。`Temp/` に置かない。D-51）

**Interfaces:**
- Consumes: Task 1 Step 4 で写した全録画の 2 本のコマンドとラダー 6 本のパス、X-prerun の GameUi 側の全録画の結果（Task 1 Step 6）
- Produces: ラダー 6 本の `recording.mp4`（報告ページ用。Task 14 まで残す）、`done.md`「## 全録画の結果」

始める前に通るべきもの: HEAD が `FROZEN`・Task 1 の表の 5b 行（manifest・lock に `jp.juha.cefunity` が 0）・R-16 の確認・compile 0。終えた後に通るべきもの: ラダー 6 本 `Success: true`（ラダー 6 は運転役から）・全録画の不具合 0・Step 3b の interrupt・言語ゲート・`uitk-stage5-compare.cs` が PASS（D-44・D-55）。

- [ ] **Step 1: ラダー 6 本**

先に Global Constraints 5 の「録画の前置き」を流し、`http://127.0.0.1:9 ja=True` を見る（新しく起動した Editor では受け口の値が立っておらず、`uitk-lifeline.cs:31-32` が「本番へ送るので中止」で必ず落ちる。D-9 で流し直すときも同じ）。
```bash
R=.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh
for s in <Task 1 Step 4 のラダー 1〜5 の 5 本のパス>; do "$R" ./moorestech_client "$s"; echo "exit=$? $s"; done
```
ラダー 6（`uitk/stage5/uitk-challenge-gate.cs`）は出展モードで起動するシナリオで、`.cs` を `run-scenario.sh` で直接流すと出展モードにならない。出展モードの運転役 `exhibition-mode-run.sh`（S5-ladder R-2・D-8。武装の EDC で次の起動 1 回だけを出展モードにしてから `run-scenario.sh` を流し、終わり方によらず武装を解いて言語を戻す）から、前置きの後に Mac の Editor で流す（`winunity play` は武装を挟めない）。呼び方は Task 1 Step 4 で写した usage の形（起草時は `<project> <GameUi|Web> <scenario.cs>`。X-5a が全体スイッチを消すときに系の引数を外していれば外した形。D-55）:
```bash
S=.agents/skills/unity-playmode-recorded-playtest/scenarios
bash "$S/uitk/stage5/exhibition-mode-run.sh" ./moorestech_client <系の引数が残っていれば GameUi> "$S/uitk/stage5/uitk-challenge-gate.cs"; echo "exit=$?"
```
運転役の後は、次の録画の前に前置きを流し直す（運転役は後始末で武装を解き言語を戻すので、日本語の固定と受け口の値を前置きの戻り値で確かめ直す。Step 3b の 2. と X-prerun Task 3 Step 2b と同じ）。（1 回の呼び出しが 600 秒に収まらなければ 2 本ずつに分け、各呼び出しの前に前置きを流し直す）。ラダー 1 が `MOORESTECH_PLAYTEST_RECEIVER_BASE が起動前に…` の例外で落ちたら前置きの欠けなので、不具合として回さず前置きを流してやり直す
Expected: 6 本とも exit 0・`result.json` の `Success: true`・ErrorLogs 0。各 run dir の `recording.mp4` を `$SP/x-switch-pr/report/` へ次の名前で写す（Task 14 Step 1 の manifest と同じ名前。段階 4 は 2 本あるので通し番号にしない）: `uitk-lifeline` → `ladder-1.mp4`・`uitk-opening-hud` → `ladder-2.mp4`・`uitk-build-bp` → `ladder-3.mp4`・`uitk-production-loop` → `ladder-4a.mp4`・`uitk-research-train` → `ladder-4b.mp4`・`uitk-challenge-gate` → `ladder-5.mp4`。写した後に `ls "$SP/x-switch-pr/report/"ladder-*.mp4 | wc -l` が 6。

- [ ] **Step 2: 全録画の一覧を作る**

Run（Task 1 Step 4 で写した実際のコマンド。期待の形）:
```bash
mkdir -p moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings
tools/uitk/list-recordings.sh > moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/list.txt 2> moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/list-excluded.txt
grep -v -E '/playtest-receiver/developer-report-(interrupt|resume)\.cs$|/uitk/stage5/(uitk-challenge-gate|uitk-event-language-gate|uitk-stage5-compare)\.cs$|/scenarios/uitk/prerun/' moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/list.txt > moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/list-run.txt
wc -l moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/list.txt moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/list-run.txt moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/list-excluded.txt
```
Expected: 一覧の本数が X-5c の `done.md` の全録画の本数と同じ（違えば差を書く）。`list-run.txt` は一覧から、Step 1 で運転役から流したラダー 6・Step 3b の 4 本（interrupt・resume・言語ゲート・`uitk-stage5-compare.cs`）・撮影シナリオ `uitk/prerun/` の本（起草時の X-prerun 計画で 6 本〈物差し `prerun-smoke-shots.cs` と撮影 5 本〉）を引いた本数（`run-scenario.sh` では流せない・出展モードの運転役が要る本は `run-all-recordings.sh` に渡さない。D-44・D-55。撮影シナリオは X-prerun と同じく全録画の一覧から外す〈X-prerun R-6〉: `prerun-shots-blocks-a/b` は 1 本で `RESULT_TIMEOUT` 420 秒に近く〈X-prerun D-19〉、3 本ずつの束に入ると 1 回の前景の呼び出しが 600 秒を超えて途中で切れ、X-prerun の表に無いので落ちれば `比較不能（新設）` になる。物差し `prerun-smoke-shots.cs` は Task 1 Step 12・Task 6 Step 1b で流す）。Task 1 Step 4 の `grep -l exhibition-mode-run` で他の出展モードの `.cs` が見つかっていれば、`grep -v` の式と Step 3b に足す。`list-excluded.txt` は空（X-5a の版は台帳を読まず標準エラーに何も出さない。空でなければスクリプトが期待の形と違うので、中身を `done.md` に写して D-15 に書く。D-45）。

- [ ] **Step 3: 全本を流す**

Run（前景で。1 回の呼び出しが 600 秒に収まるよう、Task 1 Step 6 で写した X-prerun の全録画の表の秒の列を見て束の本数を決める〈既定 3 本。続く 3 本の秒の和が 540 を超えるなら減らす〉。1 本で 600 秒を超えたものは 1 本ずつ）:
```bash
tools/uitk/run-all-recordings.sh moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/list-run.txt moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/all.tsv 1 3
tools/uitk/run-all-recordings.sh moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/list-run.txt moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/all.tsv 4 3
```
（開始行を束の本数ずつ進めて最後まで。一覧の後半を winunity で流してよい: 先に `printf 'scenario\texit\tseconds\trun_dir\n' > moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/all-win.tsv` で見出しだけの表を作り〈Windows で 1 本も流さなくても下の awk が読めるように〉、`~/bin/winunity play feature/uitk <シナリオ>` を 1 本ずつ、結果を同じ列で `all-win.tsv` に足す。`winunity play` は `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` を立てて GUI Editor を起こし、値の無い Editor は起こし直す〈`~/bin/winunity.d/interactive.ps1:30-31,50,68-81`〉ので、受け口を見る本も Windows で流してよい。ただし表示言語を日本語に固定しない〈前置きの `TrySetChosenLanguage` が無い〉ので、Windows で落ちた本は分類の前に Mac で流し直し、Mac で通れば言語の差として `all-win.tsv` のその行を消し、Mac の結果を `all.tsv` に足す。検証機と同じ PC なら Task 8〜11 と重ねない）
続けて [A-177] の列にそろえた TSV を作る（スクリプトの TSV は X-5a が `system` の列を外した `scenario exit seconds run_dir`。撤去の後は系が 1 つなので `GameUi` を入れる）:
```bash
awk -F'\t' 'BEGIN{OFS="\t"; print "scenario","system","exit","seconds","run_dir"} $1=="scenario"{next} {print $1,"GameUi",$2,$3,$4}' moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/all.tsv moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/all-win.tsv > moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/all-a177.tsv
```
Expected: TSV の行数＝`list-run.txt` の本数（ラダー 6・Step 3b の 4 本・撮影シナリオは TSV に入らない）。先に exit 0 以外の行だけを 1 回流し直す（揺れで落ちた行を不具合と読まない。X-prerun R-6・D-36 と同じ。D-41）: `awk -F'\t' 'FNR>1 && $2!=0 {print $1}' moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/all.tsv moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/all-win.tsv > moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/rerun.txt`（Windows で流した行も読む） → `tools/uitk/run-all-recordings.sh moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/rerun.txt moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/all-rerun.tsv <開始行> <束の本数>` を束ごとに前景で（`all.tsv` は X-5a の後の列 `scenario exit seconds run_dir` なので exit は `$2`）。2 回目に通った行は分類を `PASS（揺れ: 1 回目 FAIL）` にする。2 回とも落ちた行ごとに `result.json` と `uloop get-logs --project-path ./moorestech_client --log-type Error` を読み、[A-177] の 6 分類に分ける（名前は X-prerun Task 3 と同じ。`前提つき` はヘッダの手順の流し直しを 2 回目に数える）:
- `PASS`
- `束 5 未移植`: 段階 4 だけの分類で、ここでは 0 のはず。出たら `不具合` として扱う
- `wwxy`: 台帳「## 壊れた録画の割り振り」の 10 行のうち現存の本と後継 `connect/electric-wire-tool-extend-chain-via-ui.cs`。X-prerun で直して wwxy を閉じたので、落ちたら `不具合`
- `前提つき`（`playtest-receiver/*` のうちヘッダに前提の手順を書いたもの〈例 `unsent-report-export.cs` の偽の箱〉・IME の `.sh` など。シナリオのヘッダの手順で 1 回流し直し、PASS／不具合に振り直す。ヘッダの手順を持たない `developer-report-interrupt.cs`・`developer-report-resume.cs` と運転役の要る言語ゲートは Step 2 で外したので Step 3b で扱う。`uitk-c2b-ime-mac.cs` は `bash S/uitk/uitk-c2b-ime-mac-keys.sh ./moorestech_client S/uitk/uitk-c2b-ime-mac.cs`）
- `Web 専用`（CEF・Web の DOM・`webUiHealth` を見るもの）: X-5a が全部書き直すか消した（W6-8）ので 0 のはず。出たら `不具合`（持ち主は X-5a を引き継ぐ I-fix）
- `不具合`（それ以外）
あわせて 2 回とも落ちた行に、X-prerun の UITK 側の結果（Task 1 Step 6 で写した表。X-prerun の `PASS（揺れ: 1 回目 FAIL）` は PASS と同じに扱う）と比べた印を付ける（X-prerun の Web 側の比べ方 Task 4 Step 3 と同じ名前）: X-prerun でも落ちていた → `既存`、X-prerun に無い → `比較不能（新設）`、`git diff --quiet "<PRERUN_HEAD>" "$FROZEN" -- <シナリオ>`（`<PRERUN_HEAD>` は Task 1 Step 6 で写した X-prerun の全録画の HEAD）が差ありを返す（X-5a の書き直し・削除の後継）→ `比較不能（変更）`、X-prerun で PASS・中身も同じ → `退行`。印は分類の置き換えではなく、どれも `不具合` の扱い（Step 4）は同じ（D-8）

- [ ] **Step 3b: `run-scenario.sh` では流せない 4 本（D-44・D-55）**

Step 2 で一覧から外した 4 本（撮影シナリオ `uitk/prerun/` とラダー 6 を除く）を、それぞれの流し方で判定する（`run-scenario.sh` は run dir を返さないシナリオを必ず exit 1 にし〈`run-scenario.sh:140-145`〉、出展モードのシナリオの `.cs` を直接流すと出展モードにならないので、TSV の exit では判定しない）。ラダー 6 の行は Step 1 の運転役の結果を写す（`Success: true` なら `前提つき（運転役で PASS）`）。interrupt・言語ゲート・`uitk-stage5-compare.cs` は落ちたら同じ流し方で 1 回流し直し（D-41 と同じ）、2 回とも落ちたら Step 3 と同じ決め方で印を付けて `不具合` にする。

1. `developer-report-interrupt.cs`（EDC の手動投入。X-5a 計画 Task 8 Step 7 の写しで、その元は W0-cef 計画 `docs/superpowers/plans/2026-11-06-uitk-w0-cef.md` Task 4 Step 6）。Global Constraints 5 の前置きの後に:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT"
SKILL=.agents/skills/unity-playmode-recorded-playtest
mkdir -p "$SP/x-switch-pr/interrupt"
COMMIT=$(git show HEAD:.moorestech-external-revisions.json | python3 -c "import json,sys; d=json.load(sys.stdin); print(next(r['commitHash'] for r in d['repositories'] if r.get('key')=='moorestech_master'))")
MASTER_WT=$(git -C ../moorestech_master worktree list --porcelain | awk -v c="$COMMIT" '/^worktree /{p=substr($0,10)} /^HEAD /{if($2==c){print p; exit}}')
echo "master: $MASTER_WT/server_v8"   # 空なら run-scenario.sh の resolve_master_dir と同じく停止して互換コミットの master の worktree を用意する
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'return Game.Paths.GameSystemPaths.BugReportOutboxDirectory;'
```
`Result` の絶対パスを `OUTBOX=<値>`、`echo` の master を `MASTER_WT=<値>` で `vars.sh` に書き（Global Constraints 11）、次の呼び出しで:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT"
ls "$OUTBOX" | sort > "$SP/x-switch-pr/interrupt/outbox-before.txt"
uloop execute-dynamic-code --project-path ./moorestech_client --code "using Client.Playtest; return PlaytestBoot.PrepareAndEnterPlayMode(\"$MASTER_WT/server_v8\", true);"
```
`Result` の絶対パスを `SESSION_DIR=<値>` で `vars.sh` に書き、次の呼び出しで:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT"
for i in $(seq 1 150); do [ -f "$SESSION_DIR/ready.marker" ] && break; sleep 2; done; ls "$SESSION_DIR/ready.marker"
uloop execute-dynamic-code --project-path ./moorestech_client --code-file .agents/skills/unity-playmode-recorded-playtest/scenarios/playtest-receiver/developer-report-interrupt.cs
ls "$OUTBOX" | sort | comm -13 "$SP/x-switch-pr/interrupt/outbox-before.txt" -
```
Expected（開発者トークンの有無で 2 通り。どちらも X-5a が足した v8 の 3 つの検査〈`schemaVersion` が 8・`webUiHealth` が無い・`gameUiExceptions` の `status` が `unknown`〉は通った後）: トークンあり → `Result` が `pending upload interrupted: <箱名>` で PlayMode が止まる。トークンなし → 例外文がちょうど `Not pending before stop`。どちらも `前提つき（手動投入で PASS）`。例外文が `schemaVersion is not 8`・`webUiHealth is still …`・`gameUiExceptions is not unknown …`・`READY failed` か、スニペットのコンパイルの失敗なら不合格。どちらの場合も、最後の `comm -13` で出た箱（この投入が作った 1 件）を `rm -rf "$OUTBOX/<箱名>"` で消し（トークンありでも送信の途中で止めた箱が残る。残すと受け口の値の無い Editor の次の再生で本番へ送られうる〈推測〉）、`rm -f .superpowers/sdd/task10-interrupted-id.txt`（トークンありのときスニペットが書く）・`uloop control-play-mode --project-path ./moorestech_client --action Stop`。結果（`Result` か例外文・箱名・トークンの有無）を Step 5 の表の行に書く。

2. `uitk-event-language-gate`（運転役。`ArmForNextBoot()` で次の起動 1 回だけを出展モードにしてから `run-scenario.sh` を流し、終わり方によらず武装を解いて言語を戻す）。前置きの後に（引数は Task 1 Step 4 で写した usage の形）:
```bash
.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-event-language-gate.sh ./moorestech_client; echo "exit=$?"
```
Expected: exit 0・`result.json` が `Success: true` → `前提つき（運転役で PASS）`。秒と run dir は `run-scenario.sh` の出力から写す。次の録画の前に前置きを流し直す

2b. `uitk/stage5/uitk-stage5-compare.cs`（S5-ladder の並べ比べの撮影シナリオ。出展モードで起動し、言語ゲートを両解像度で撮る。S5-ladder Task 9）: 前置きの後に Step 1 のラダー 6 と同じ運転役の呼び方（Task 1 Step 4 で写した usage の形）で流す:
```bash
S=.agents/skills/unity-playmode-recorded-playtest/scenarios
bash "$S/uitk/stage5/exhibition-mode-run.sh" ./moorestech_client <系の引数が残っていれば GameUi> "$S/uitk/stage5/uitk-stage5-compare.cs"; echo "exit=$?"
```
Expected: exit 0・`result.json` が `Success: true` → `前提つき（運転役で PASS）`（X-prerun Task 3 Step 2b と同じ語）。次の録画の前に前置きを流し直す

3. `developer-report-resume.cs`: 流さず `前提つき（判定の対象外）` と書く。interrupt の対で、次の再生で本番送信が再開して `UPLOADED` が付くことを見るシナリオで、受け口を `http://127.0.0.1:9` へ外した前置きでは再開せず、作者の機材にしか無い進捗の箱 `20261014_151419_783aca75` を読む（`developer-report-resume.cs:15-16`）ので、どの機材でも通らない。X-5a も判定しない（X-5a 計画 R-16・Task 8 Step 7）

- [ ] **Step 4: 不具合を回す**

`不具合` は 1 本ずつ bd を `--parent <bd>` で立て、Step 3 の印（`既存`／`退行`／`比較不能（新設）`／`比較不能（変更）`）を書き、持ち主（シナリオは X-5a の持ち物を引き継ぐ I-fix、画面はその画面の I-fix）への依頼文（シナリオのパス・落ちた assert・Error の行・run dir）を指揮へ返す。`done.md` の 1 行目を「状態: 停止（全録画の不具合 n 件の修正待ち）」にして Task 14 Step 5 の合図を送り止まる。直ったら `git merge --no-ff origin/feature/uitk`（I-fix が feature/uitk へ入れたもの）で取り込み、Task 6 からやり直す（`FROZEN` が変わるため。D-9）。

- [ ] **Step 5: 記録**

`done.md`「## 全録画の結果」に `all-a177.tsv` を「シナリオ / system / exit / 秒 / 分類 / X-prerun と比べた印」の表で写す（run dir は書かない。X-prerun の表と同じ扱い）。Step 1 のラダー 6 と Step 3b の 4 本も同じ表に 1 行ずつ足す（system は `GameUi`、exit の欄は interrupt が `Result` か例外文・ラダー 6・言語ゲート・`uitk-stage5-compare.cs` が運転役の exit・resume が `-`、分類は Step 3b の語。撮影シナリオ `uitk/prerun/` は表に入れず、本数と「X-prerun と同じく全録画の外。物差しは Task 1 Step 12・Task 6 Step 1b」の 1 行を書く。D-44・D-55）。要約 1 行「<n> 本中 PASS <a>（うち揺れ〈2 回目に PASS〉<s>）・前提つき（流し直して PASS）<c>（うち手動投入 1 と運転役 3〈ラダー 6・言語ゲート・並べ比べ〉）・判定の対象外 1（resume）・束 5 未移植 0・wwxy 0・Web 専用 0・不具合 0（途中で見つけた <k> 件は bd <ids> で直して再走 PASS）。X-prerun の UITK 側と比べた退行 0」。テストのワールドを `bash .agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で探して消す。ラダー以外の run dir（mp4・PNG）を消す。

---

### Task 8: 配布ビルド・CEF 0 の検査・playtest-staging・Windows 検証機の通し検証

**Files:** なし（成果物は `$RUNS/<label>/`）

**Interfaces:**
- Consumes: `FROZEN`、`$WT/scripts/playtest/release-playtest.sh`（5b が CEF 0 の検査へ反転した `lib/release-artifact.sh` を含む、凍結した HEAD の版。D-2）、moores-playtest-release の手順 1・2
- Produces: `LABEL`（`playtest-YYYYMMDD-HHMM`）、`$RUNS/$LABEL/`（`validated-steam-build.json`・`verify/`・`promotion.md`）、release.log の BuildID、検証機の `$SP/x-switch-pr/win-Player.log`・`win-Player-prev.log`（Step 5）

始める前に通るべきもの: HEAD＝`FROZEN`＝`origin/feature/uitk`・Task 6 Step 3 の契約テスト 5 本 PASS・Task 1 Step 8 の保存資格 exit 0・R-16 の確認。終えた後に通るべきもの: R-10〜R-12・R-23。

- [ ] **Step 1: Mac の Editor を閉じる**

`moores-wt status` で自分の Editor（`$WT`）だと確かめて `uloop` で閉じる（配布ビルドの batchmode の Unity と同時に 2 本を動かさない。D-13）。

- [ ] **Step 2: 焼く（本体が起動する。subagent に渡さない）**

```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT" && git fetch -q origin
. ~/hermes-agent/data/services/playtest/env.sh
export MOORESTECH_BUILD_BRANCH=feature/uitk
export MOORESTECH_STEAM_BUILD_LABEL=playtest-$(date +%Y%m%d-%H%M)
LABEL=$MOORESTECH_STEAM_BUILD_LABEL; RUNS=~/hermes-agent/data/services/playtest/runs
printf 'LABEL=%s\nRUNS=%s\n' "$LABEL" "$RUNS" >> "$SP/x-switch-pr/vars.sh"   # Step 4 以降の呼び出しが読む（Global Constraints 11）
scripts/playtest/release-playtest.sh "$FROZEN" > "$RUNS/$MOORESTECH_STEAM_BUILD_LABEL.release.log" 2>&1   # Bash の run_in_background で起動
```
起動と同時に Monitor で `tail -F "$RUNS/$MOORESTECH_STEAM_BUILD_LABEL.release.log"` を見張り、`ERROR|FAIL|Invalid|credentials|steam_guard|phase|passed|EXIT=` の行を受ける（成功だけでなく失敗も拾う）。`$WT` の `scripts/playtest` から流す理由は D-2（メインクローンの master の版は Mac 成果物に CEF helper を要求して落ちる。`scripts/playtest/lib/release-artifact.sh:54-64`〈`helper_executable` が無いと exit 4〉）。
Expected: release.log に `[release-playtest] label=<LABEL> commit=<FROZEN> branch=feature/uitk`・Windows と Mac の成果物の検査の失敗なし・steamcmd の成功・`[verify] passed`・`[release-playtest] promotion: …/promotion.md`。

- [ ] **Step 3: 止まったとき**

- `Cached credentials not found` → `ERROR (Invalid Password)`: moores-playtest-release の手順 2。メインクローンの `~/hermes-agent/data/repos/moorestech/.agents/skills/moores-playtest-release/scripts/upload_and_verify.sh $RUNS/<label>` を run_in_background で起動し、Monitor で `steam_guard_required|uploaded|failed|rejected|FAILED|VERIFY_EXIT|LOCK_FAILED|passed` を受ける（このスクリプトは中身に CEF を見ないのでメインクローンの版でよい。取り込みロックの置き場がメインクローン基準のため、worktree の版を使わない）。`steam_guard_required` が出たら**その場で**ユーザーへ「スマホの Steam アプリで承認して」と伝える
- 成果物の検査で落ちた（`ERROR: 成果物に … がありません`・CEF の検査）: Steam には何も上がっていない。メッセージを `done.md` に貼り、持ち主（CEF 0 の検査は X-5b、成果物の構成はその持ち主）へ bd を立てて停止。`ERROR: 成果物に CEF のファイルが残っています（<pattern>）: <path>` のときは、当たったファイルが CEF の物か Unity・他の物か（とくに `icudtl.dat`・`chrome_*_percent.pak`。X-5b の申し送り）を `<path>` の置き場と `strings`・`ls -l` で見分けて bd に書く（Unity の物なら誤検知で、名前を列から外す理由を X-5b の持ち物を引き継ぐ I-fix へ渡す）
- `verify-on-windows.sh` で落ちた: `$RUNS/<label>/verify/` の `result.json`・Player.log を読み、UI の原因なら bd を立てて停止。検証機の環境（WoL・Steam の更新待ち）なら `upload_and_verify.sh $RUNS/<label> --skip-upload` で検証だけやり直す
- master data のピンのずれ（Gotchas）: ピンに合わせて Mac だけやり直す（moores-playtest-release の Gotchas どおり）

- [ ] **Step 4: staging の一致を確かめる**

Run: `. "$SP/x-switch-pr/vars.sh" || exit 1; python3 "$WT/.agents/skills/moores-playtest-release/scripts/validate_staging.py" "$RUNS/$LABEL"`
Expected: `validated-steam-build.json` が書かれる（release-playtest.sh が自分で流さないため。手順 1 の末尾）。BuildID を `done.md` に書く。

- [ ] **Step 5: 検証機の結果を読む（R-12）**

```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; V="$RUNS/$LABEL/verify"; ls -R "$V" | head -40
grep -h '"success"' $(find "$V" -name result.json)
grep -c -E 'DllNotFoundException|TypeLoadException|FileNotFoundException|MissingMethodException' $(find "$V" -name 'Player*.log')
```
`verify-on-windows.sh` は検証機の `results` だけを scp で回収し（`verify-on-windows.sh:81`）、`run-smoke.ps1` も Player.log を写さないので、`find` の Player.log は通常 0 本。Task 9 の起動で上書きされる前に、ssh で検証機の Player.log を 2 本取る（phase2 の起動で phase1 の分は `Player-prev.log` へ回る。Unity の既定の動き。検証機では未実測の推測）:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; . ~/hermes-agent/data/services/playtest/env.sh; . "$WT/scripts/playtest/lib/verify-ssh.sh"
for f in Player.log Player-prev.log; do verify_ssh_remote "powershell -NoProfile -Command \"Get-Content -Raw \$env:USERPROFILE/AppData/LocalLow/sakastudio/moorestech/$f\"" > "$SP/x-switch-pr/win-$f"; echo "$f lines=$(wc -l < "$SP/x-switch-pr/win-$f")"; done
grep -c -E 'DllNotFoundException|TypeLoadException|FileNotFoundException|MissingMethodException' "$SP/x-switch-pr/win-Player.log" "$SP/x-switch-pr/win-Player-prev.log"
grep -h -E 'smokePhase|phase1|phase2' "$SP/x-switch-pr/win-Player.log" "$SP/x-switch-pr/win-Player-prev.log" | head -4
test -s "$SP/x-switch-pr/x5d-names-client.txt" || { echo "x5d-names-client.txt が無いか空（Task 1 Step 11 か起こし直しの 5. で作る）"; exit 1; }
grep -h -E 'DllNotFoundException|TypeLoadException|FileNotFoundException|MissingMethodException|Could not load' "$SP/x-switch-pr/win-Player.log" "$SP/x-switch-pr/win-Player-prev.log" | grep -c -F -f <(sed 's/\.dll$//' "$SP/x-switch-pr/x5d-names-client.txt")   # X-5d が消した client の 61 名（Task 1 Step 11 で行数を確かめた一覧）
```
Expected: phase1・phase2 とも `"success": true`、2 本とも行数が 0 でなく、例外の数が全部 0、X-5d の client の 61 名が例外・読み込み失敗の行に 0（X-5d R-12 (1)）。4 行目でどちらの log がどの phase かを確かめる（`Player-prev.log` が phase1 でなければ、取れた分だけで判定し `done.md` に「phase1 の Player.log は取れなかった」と書く）。phase2 の `result.json` に録画（ffmpeg）とバグ報告の送信が成功した項目がある（項目名は `StandalonePlaytestSmokeRunner` の書くもの。X-5b の申し送りの `Plugins/x86_64` の ffmpeg が動いた証拠）。（D-26）

- [ ] **Step 6: Windows のインストール先の CEF 0（R-11）**

検証機で（`$WT/scripts/playtest/lib/verify-ssh.sh` の `verify_ssh_remote` を使う。`remote-exec.sh:36-41` の `-EncodedCommand` の形で送る）:
```powershell
$steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath).SteamPath -replace "/", "\"
$game = Join-Path $steam "steamapps\common\moorestech"
$info = Get-Content (Join-Path $game "moorestech_Data\StreamingAssets\build-info.json") -Raw | ConvertFrom-Json
"build=" + $info.commit
"branch=" + $info.branch
"cef-files=" + @(Get-ChildItem -Recurse -File (Join-Path $game "moorestech_Data\Plugins\x86_64") | Where-Object { $_.Name -match "cef" }).Count
"ffmpeg=" + @(Get-ChildItem -Recurse -File $game -Filter "ffmpeg*.exe").Count
"plugins-dir=" + (Test-Path (Join-Path $game "moorestech_Data\Plugins\x86_64"))
"webui-dir=" + (Test-Path (Join-Path $game "moorestech_Data\StreamingAssets\WebUi"))
```
（ライブラリを既定以外に置いていれば `steamapps\libraryfolders.vdf` から探す）
Expected: `build=<FROZEN>`・`branch=feature/uitk`（配布物に焼かれた値。release.log の `branch=` はスクリプトへ渡した環境変数の写しで、焼き込み側〈`BuildInfoComposer.cs:52`〉の読み落としは見えない）・`cef-files=0`・`ffmpeg=1` 以上・`plugins-dir=True`（X-5b の申し送り: CEF 無しでも `Plugins/x86_64` が作られ ffmpeg が入る。`FfmpegRuntimeBundler.cs:49-52`）・`webui-dir=False`（X-5c の申し送り）。`build=` が空・別の SHA・`branch=` が `feature/uitk` でない・PowerShell のエラーはどれも不合格（欠損の分岐は置かず、この照合で気づく。D-22）。

- [ ] **Step 7: 記録**: `done.md` の冒頭（1 行目の状態の直後）に「指揮の依頼文はメインクローンの `scripts/playtest` から焼くと書いていたが、凍結した HEAD の worktree の `scripts/playtest` から焼いた。master の版は Mac 成果物に CEF helper を要求して必ず落ちるため（D-2）」の 1 行を書く。release.log の要所（label・commit・branch・検査・BuildID・`[verify] passed`）と Step 4〜6 の出力を `done.md` の R-10〜R-12 へ。`promotion.md` の「反映」節は実行しない（X-release）と `done.md` に書く。`$RUNS/$LABEL/promotion.md` の先頭に「反映禁止: このビルドは切替 PR（feature/uitk <FROZEN 10 桁>）のマージ前の検証ビルド。playtest への反映は、ユーザーのマージの後に X-release がユーザーの指示で行い、そのときにこの行を外す（裁定「切替前のビルドはどこにも配らない」）」の 1 行を足す（`release-playtest.sh` の `promotion.md` は commit だけで branch を書かず、`promote_playtest.py` は `promotion.md` を読まないので、この行は機械を止めない。別のセッションが「playtest に反映して」と頼まれて最新の run を選ぶ前に、`promotion.md` を開いた人とエージェントへ知らせる印。D-32）。Mac の Editor は Task 9〜11 で要らないので閉じたままにする

- [ ] **Step 8: server の配布ビルド（X-5d の申し送り。R-23）**

client の配布ビルドは `release-playtest.sh` が焼くが、server（`moorestech_server/` の単体ビルド）を焼く工程は配布の道具に無い（`release-playtest.sh`・`lib/*.sh` に `moorestech_server` の参照 0）。PR ごとの `Platform Compile Check` は server のスクリプトのコンパイルだけを見るので、プレイヤービルドで初めて出る不足（ビルド時の参照の解決・プラグインの .meta の設定など。推測）は、master へ入った後の日次 cron（`build.yml:11-12`）まで出ない。切替 PR の中で 1 回だけ dispatch で焼く（裁定「ビルドは切替 PR の中でだけ回す」の範囲。D-30）:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT"
gh workflow run build.yml --ref feature/uitk
RUN=$(gh run list --workflow build.yml --branch feature/uitk --event workflow_dispatch --limit 1 --json databaseId,headSha --jq '.[0] | "\(.databaseId) \(.headSha)"'); echo "$RUN"   # headSha が $FROZEN と同じこと（違えばまだ一覧に出ていないので流し直す）
timeout 580 gh run watch "${RUN%% *}" --exit-status; echo "exit=$?"   # 124 の間は同じ行を前景で繰り返す（間に他の記録の作業をする。sleep で待たない）
gh run view "${RUN%% *}" --json jobs --jq '.jobs[] | "\(.name) \(.conclusion)"'
test -s "$SP/x-switch-pr/x5d-names-server.txt" || { echo "x5d-names-server.txt が無いか空（Task 1 Step 11 か起こし直しの 5. で作る）"; exit 1; }
gh run view "${RUN%% *}" --log 2>/dev/null | grep -i -F -f <(sed 's/\.dll$//' "$SP/x-switch-pr/x5d-names-server.txt") | grep -i -E 'error|not found|missing|could not' | wc -l
```
Expected: `Server Build - StandaloneWindows64 success`・`Server Build - StandaloneOSX success`・最後の行が 0（X-5d が消した server の 17 名〈id 13 件と解析器の 4 名〉が、ビルドのエラー・欠落の行に 0）。`Client Build - *` の結論も `done.md` の R-23 に書く（落ちたら原因を読み、配布物の client は `release-playtest.sh` 側で通っているので、CI だけの原因〈runner の環境〉か切替の原因かを書いて持ち主へ bd）。Server Build が落ちたら `error CS` と欠落の行を `done.md` に貼り、NuGet の持ち主の X-5d を引き継ぐ I-fix へ bd を立てて停止。Linux の DedicatedServer は matrix でコメントアウト（`build.yml:65-68`）なので焼けない。そのことを `done.md`「## 未確認」と PR 本文の「検証の範囲」に書く（この工程で `build.yml` は変えない。`.github/**` はこのレーンの外）。この run は配布物にしない（Steam へ上げない）

---

### Task 9: 遠隔実行の 3 点（Windows 検証機の staging ビルド。統合計画 §5.1-3）

**Files:** なし（`$SP/x-switch-pr/remote-exec/` に snippet と PowerShell）

**Interfaces:**
- Consumes: `$WT/scripts/playtest/remote-exec.sh --windows`（`MOORESTECH_VERIFY_HOST`・`MOORESTECH_VERIFY_USER`・ssh）、Task 8 で検証機に入った staging ビルド、前例の snippet（`$RUNS/remote-exec-listener-20261026-172117/snippets/{hold,harmony,quit}.cs`・`probe.ps1`・`launch.ps1`）
- Produces: `done.md` の R-13 の証拠、出展モードを付けて起動する操作（Step 5。R-18 の目視の手順へ）

始める前に通るべきもの: R-16 の確認（Task 6 の頭と同じ 1 行が空。空でなければ Task 2 へ戻る）・Task 8 の `[verify] passed`・検証機で moorestech が動いていない。終えた後に通るべきもの: 3 点とも合格・検証機で moorestech のプロセス 0。

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
送り方: `. "$WT/scripts/playtest/lib/verify-ssh.sh"` の後、`remote-exec.sh:36-41` と同じく `-EncodedCommand` に UTF-16LE の base64 で包んで `verify_ssh_remote` に渡す（引数は先頭の `param` に合わせて `& { <本文> } -CaseName on -RemoteExec` の形で包む）。

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

- [ ] **Step 5: 出展モードを付けて起動する操作を確かめる（オーナーの目視の手順に書くため）**

出展モードは環境変数 `MOORESTECH_EVENT_MODE=1` だけで有効になり（`EventExhibitionSettings.cs:12,48`）、起動引数の口は無い。Steam の起動オプションでは環境変数を渡せない（推測）。検証機で次の順に試し、最初に通った操作を `done.md`「## ユーザーの残タスク」の手順の元にする（D-25）。出展モードは起動時に既定のワールドを消す（`EventModeAutoStartActions.cs:12` の `GameSystemPaths.DeleteDefaultWorldDirectory()`）。消える場所は環境変数 `MOORESTECH_DEFAULT_WORLD_DIRECTORY` で替えられる（`moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs:26,49-54`）ので、どの手でも `MOORESTECH_DEFAULT_WORLD_DIRECTORY=C:/moorestech-smoke/x-switch-pr/event-world` を一緒に渡す。Mac mini では試さない。
1. 直接起動: `launch.ps1` の `$action` を `cmd.exe /c "set MOORESTECH_EVENT_MODE=1&& set MOORESTECH_DEFAULT_WORLD_DIRECTORY=C:/moorestech-smoke/x-switch-pr/event-world&& start "" "<インストール先>\moorestech.exe" --remoteExec"` に替えた `launch-event.ps1` で起動する（インストール先は Task 8 Step 6 の `$game`）
2. 1 が `IsEnabled=False` か起動しないとき: ユーザーの環境変数を足して Steam を起動し直す（`MOORESTECH_EVENT_MODE`＝`1` と `MOORESTECH_DEFAULT_WORLD_DIRECTORY`＝`C:/moorestech-smoke/x-switch-pr/event-world` を `[Environment]::SetEnvironmentVariable(<名前>,<値>,"User")` で足す → `run-smoke.ps1:98,102` と同じく `-shutdown`・`-silent` で Steam を起動し直す → `launch.ps1 -CaseName event -RemoteExec`）。終わったら必ず 2 つとも `[Environment]::SetEnvironmentVariable(<名前>,$null,"User")` で消して Steam を起動し直す（`finally` で）
確かめ方: `probe.ps1 -WaitSeconds 240` の後、`event.cs`（`return Client.Starter.EventMode.EventExhibitionSettings.FromEnvironment().IsEnabled + " " + Game.Paths.GameSystemPaths.DefaultWorldDirectory;`）を `remote-exec.sh --windows` で送る → `"result":"True C:/moorestech-smoke/x-switch-pr/event-world"`（検証機の `Saves/world_1` は消えていない）。続けて `quit.cs` で閉じる。
Expected: どちらかの操作で `True`。両方 `False` なら、オーナーの手順の出展モードの項に「起動の手が確かめられていない」と書き、`done.md`「## 未完了」に残す（止まらない。出展モードの言語ゲート自体はラダー 5 が Editor で通している）。

- [ ] **Step 6: 記録と片付け**: 3 点と Step 5 の結果を `done.md` の R-13・R-18 へ（ポートは書いてよい・トークンは書かない）。検証機の `C:/moorestech-smoke/x-switch-pr/` を消す（ssh で `Remove-Item -Recurse -Force`。自分が作った場所だけ）

---

### Task 10: Mac の Steam での起動（moores-playtest-release の手順 4）

**Files:** なし

**Interfaces:**
- Consumes: Task 8 の BuildID、cua-driver スキルの `references/games.md`
- Produces: `done.md` の R-14・R-11（Mac）の証拠、`$SP/x-switch-pr/report/mac-inventory.png`、`promotion.md` の Mac の欄

始める前に通るべきもの: R-16 の確認（Task 6 の頭と同じ 1 行が空。空でなければ Task 2 へ戻る）・Task 8 の staging への上げ・Task 1 Step 8b をもう一度流して acf があり `BetaKey` が `playtest-staging`（まだ無ければ、下の「待ちが切れたとき」と同じ扱いにして Step 1 の待ちに入らない）。Mac の Editor が閉じている（前面を取り合わない）。終えた後に通るべきもの: R-14（または「待ちが切れたとき」の記録）。

- [ ] **Step 1: 更新を待つ**

`open -g -a Steam` → `~/Library/Application Support/Steam/steamapps/appmanifest_1958160.acf` の `buildid` が Task 8 の BuildID・`StateFlags` が `4` になるまで、Monitor の until ループ（条件: `grep -q "\"buildid\"\s*\"<BuildID>\"" <acf> && grep -q '"StateFlags"\s*"4"' <acf>`・上限 30 分）で待つ（moores-playtest-release の手順 4-1）。

待ちが切れたとき（上限の 30 分で条件がそろわない）: acf の有無・`buildid`・`StateFlags`・`BetaKey` を `done.md` に写す。acf が無いか `BetaKey` が `playtest-staging` でなければ前提の欠け、そろっているのに `buildid` が古ければ Steam の更新の遅れとして 1 回だけ `open -g -a Steam` から 30 分待ち直す。どちらでも越えられなければ、Mac の確認（R-14・R-11 の Mac・R-15 の Mac の 4 行）を `未確認（Mac の Steam: <前提の欠け|更新されない>）` として `done.md` の各行と「## 未確認」と PR 本文の「検証の範囲」に書き、ユーザーの目視の手順（Task 14 Step 3）に「Mac の Steam で起動し Tab でインベントリが UITK で出ること・IME の 4 欄」を「完了条件の代わり。済むまでマージしない」として足し、`done.md` の 1 行目の締めを「状態: 完了（未確認: Mac の Steam）」にし（Task 14 Step 5 の 3.）、manifest から Mac の 2 枚を外して（Task 14 Step 1）、Task 10 の残りと Task 11 の Mac の分を飛ばして先へ進む（停止しない。Windows の分は通常どおり。D-47）。

- [ ] **Step 2: Mac の CEF 0（R-11）**

```bash
APP="$HOME/Library/Application Support/Steam/steamapps/common/moorestech/moorestech.app"
find "$APP" -iname '*cef*' | wc -l
test -e "$APP/Contents/Resources/Data/StreamingAssets/WebUi" && echo webui-present || echo webui-absent
python3 -c 'import json,sys;d=json.load(open(sys.argv[1]));print(d["commit"], d["branch"])' "$APP/Contents/Resources/Data/StreamingAssets/build-info.json"
```
Expected: 0・`webui-absent`（X-5c の申し送り）・`<FROZEN> feature/uitk`（R-10 の `branch`）。

- [ ] **Step 3: 起動してワールドに入る**

`open "steam://run/1958160"`。cua-driver の games.md の手順（`screencapture` で撮り、`cliclick` の長押しクリックと `osascript` のキー入力。前面化が要る）で、同意画面「了解」→ 前回異常終了の報告「送らない」→「ローカルでプレイ」。`~/Library/Logs/sakastudio/moorestech/Player.log` の `[PlaytestTitleGates] step`・`[InitializeScenePipeline] mod icon capture completed`・`[TerrainRuntimeBuilder] Terrain built` を確かめ、`test -s "$SP/x-switch-pr/x5d-names-client.txt" && grep -E 'DllNotFoundException|TypeLoadException|FileNotFoundException|MissingMethodException|Could not load' ~/Library/Logs/sakastudio/moorestech/Player.log | grep -c -F -f <(sed 's/\.dll$//' "$SP/x-switch-pr/x5d-names-client.txt")` が 0（`test -s` が偽で何も出なければ、空の一覧の 0 件の素通りを避けるため Task 1 Step 11 の取り出しを流し直してから数える）（X-5d の申し送り。client の 61 名）。Gatekeeper などの回避操作が要ったかを記録する。

- [ ] **Step 4: Tab でインベントリ**

Tab → スクショ（`$SP/x-switch-pr/report/mac-inventory.png`）。UITK のインベントリが出ていることを目で見る（CEF の Web の描画ではない＝Player.log に `[WebUiHost]`・`Cef` の行が 0: `grep -c -E 'WebUiHost|Cef' ~/Library/Logs/sakastudio/moorestech/Player.log`）。Cmd+Q で終える。
Expected: スクショにインベントリ・grep が 0。

- [ ] **Step 5: 記録**: `promotion.md` の「Mac 版の手動確認」のチェック欄を埋め、BuildID・セーブ変換の行・スクショで見たことを追記する（moores-playtest-release の手順 4-5。「反映」節は触らない）。Task 8 Step 7 の先頭の「反映禁止」の行は残す（外すのは X-release）。`done.md` の R-14 へ

---

### Task 11: IME の一巡（Windows 検証機と Mac の配布ビルド。統合計画 §4.9）

**Files:** なし（`$SP/x-switch-pr/ime/` に snippet と鍵送りのスクリプト）

**Interfaces:**
- Consumes: Task 8・10 で入った staging ビルド、`remote-exec.sh`（Mac はローカル・Windows は `--windows`）、`Client.Ui.Foundation.Root.GameUiRoot.Instance.GetRootElement()`（`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Root/GameUiRoot.cs:41`）、入力欄の testid（`GameTextField` が内側の `UnityEngine.UIElements.TextField` の name に付ける。`GameTextField.cs:51`）: `bug-report-description`（`BugReportFormView.cs:60`・複数行）・`build-menu-search`（`BuildMenuPanelSkeleton.cs:43`）・`modal-input`（`ModalView.cs:67`）・`train-station-name-input`（`TrainStationNameRow.cs:29`）、BP 名の入口 `Client.Game.InGame.UI.Blueprint.BlueprintNameInputState.Open()`（`ClientDIContext.DIContainer.DIContainerResolver` から引く）、駅の設置はサーバーの `Game.Context.ServerContext.WorldBlockDatastore.TryAddBlock`（`Client.Playtest/Operations/PlaytestBlockOps.cs:51-61` の写し。駅は名前 `蒸気機関車駅` で `MasterHolder.BlockMaster.GetBlockAllIds()` から引く）、駅を開く前例 `S/uitk/uitk-train-inventory.cs:171-185` の `OpenStation` と、それを写すクライアントの API（`Client.Game.InGame.Player.PlayerSystemContainer.Instance.PlayerObjectController` の `Position`・`SetPlayerPosition(Vector3)`〈`IPlayerObjectController`〉、`ClientContext.VanillaApi.SendOnly.SendPlayerPosition(Vector3)`〈`PlaytestSetup.WarpPlayer` の写し〉、`ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(Vector3Int)`、`UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(Vector2)`〈推測〉、照準の元を読む `Client.Game.InGame.Control.ViewMode.AimPointProvider.GetEffectiveAimSource()`〈`AimPointProvider.cs:37-44`。三人称で左 Alt を押している間だけ `Cursor`、それ以外は `ScreenCenter`〉）、S9 の Windows の鍵送りの方法（`~/share/2026-10-28-uitk-s9-ime-result/s9-ime-win-report.md` §2〜§4）、Mac の鍵送りの前例 `S/uitk/uitk-c2b-ime-mac-keys.sh`
- Produces: `done.md`「## IME の一巡」の 8 行（2 OS × 4 欄。開けなかった欄は「開けない」の印つき）、変換中のスクショ 8 枚（報告ページには各 OS 1 枚）

始める前に通るべきもの: R-16 の確認（Task 6 の頭と同じ 1 行が空。空でなければ Task 2 へ戻る）・Task 9（Windows）・Task 10（Mac）が済み、ゲームが動いていない（Task 10 が「待ちが切れたとき」で Mac を `未確認` にしたら、Mac の 4 行は流さず `未確認` のまま。D-47）。終えた後に通るべきもの: 8 行とも合格か、不合格の行に bd。

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
- `open-bug-report.cs`（OS の Esc でポーズを開いた後に送る。`Button` は `NavigationSubmitEvent` で `clicked` を呼ぶ〈推測。最初の 1 回で確かめ、開かなければ `GameUiScreenMapper`（`Client.Playtest/UiElements/GameUi/GameUiScreenMapper.cs:19-26` の `PanelToScreen` と `internal static Map`。`internal` なので遠隔実行の snippet からは呼べず、式を snippet に写す）で画面座標を出して OS のクリック〈Mac は `cliclick`、Windows は `SendInput` のマウス〉に替え、D-16 に書く〉）:
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
- 駅（`蒸気機関車駅`）は 7×1×20 のブロック（moorestech_master の `blocks.json` の `blockSize`）で、カメラの前に原点を合わせて置くだけではプレイヤーに重なるか照準が外れて F が何も開かない。前例 `S/uitk/uitk-train-inventory.cs:171-185` の `OpenStation`（駅の外の南側へワープ → コライダーへ照準 → LeftAlt を押したまま Interact〈F〉。LeftAlt の長押しはカーソルを自由にする〈`UiStateCameraPolicyService.cs:95,106`・`GameScreenState.cs:142`〉）を、DSL の代わりに遠隔実行と OS の鍵で写す。前例の順は「ワープ → 10 フレーム → 左 Alt を押す → 2 フレーム → 照準 → F → 左 Alt を離す」で、**照準より先に左 Alt を押す**。左 Alt の押下は `UiStateCameraPolicyService.cs:95-101` で `ApplyZonePolicy(CursorCenterWarp.ToScreenCenter)` を呼び、カーソルを画面中央へ戻す（`PlayerCameraInteractionApplier.cs:21`）ので、照準の後に押すと置いたカーソルが中央へ戻り、三人称の照準が中央の光線になって駅に当たらない（D-39）。snippet は 4 つ:
  - `player-pos.cs`（クライアント）: `var q = Client.Game.InGame.Player.PlayerSystemContainer.Instance.PlayerObjectController.Position; return $"{UnityEngine.Mathf.FloorToInt(q.x)},{UnityEngine.Mathf.FloorToInt(q.y)},{UnityEngine.Mathf.FloorToInt(q.z)}";` → `<px>,<py>,<pz>`
  - `place-station.cs`（`--target server`。原点はプレイヤーの足もとから北へ 8 マス〈`<px>`, `<py>`, `<pz>+8`〉。前例は平らな地面の `(10,32,4)`・South で、配布ビルドのワールドは平らでないので足もとの高さに合わせる〈推測〉）:
```csharp
using Core.Master;
using Game.Block.Interface;
using Game.Context;
var stationId = default(BlockId);
var found = false;
foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
{
    if (MasterHolder.BlockMaster.GetBlockMaster(blockId).Name != "蒸気機関車駅") continue;
    stationId = blockId;
    found = true;
}
if (!found) return "missing:蒸気機関車駅";
var placed = ServerContext.WorldBlockDatastore.TryAddBlock(stationId, new UnityEngine.Vector3Int(<x>, <y>, <z>), BlockDirection.South, System.Array.Empty<BlockCreateParam>(), out var block);
return $"placed={placed}";
```
  （`ServerContext` は `Game.Context`〈`moorestech_server/Assets/Scripts/Game.Context/ServerContext.cs:9-11`〉。駅は名前で引く〈`PlaytestBlockOps.ResolveBlockId`〈`PlaytestBlockOps.cs:23-30`〉と同じ引き方。名前はシナリオ `uitk/uitk-train-inventory.cs:57` の `"蒸気機関車駅"`〉。`--target server` はサーバーの更新スレッドの tick 末尾で走るので、`PlaytestBlockOps.PlaceBlockDirect`〈`:51-61`〉の `PlaytestServerThreadAction` と同じ位置で書く〈ADR 0072:42〉）
  - `aim-station.cs`（クライアント。`<x>,<y>,<z>` は置いた原点。前例の `OpenStation` の写し: コライダーの外接箱を取り、南の外へワープし、照準の点へ OS のカーソルを置く）:
```csharp
using System.Linq;
using UnityEngine;
var go = Client.Game.InGame.Context.ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(new Vector3Int(<x>, <y>, <z>));
var colliders = go.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.bounds.size != Vector3.zero).ToList();
if (colliders.Count == 0) return "missing:station-collider";
var bounds = colliders[0].bounds; foreach (var c in colliders) bounds.Encapsulate(c.bounds);
var feet = new Vector3(bounds.center.x, bounds.min.y + 1.5f, bounds.min.z - 1f);
Client.Game.InGame.Player.PlayerSystemContainer.Instance.PlayerObjectController.SetPlayerPosition(feet);
Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.SendPlayerPosition(feet);
var screen = Camera.main.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.min.y + 0.4f, bounds.min.z + 0.1f));
if (screen.z <= 0f || screen.x < 0f || Screen.width < screen.x || screen.y < 0f || Screen.height < screen.y) return $"offscreen:{screen}";
UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(new Vector2(screen.x, screen.y));
return $"aimed={screen.x:F0},{screen.y:F0}";
```
  （前例の 33.5 と 32.4 は駅の原点 y=32 からの +1.5・+0.4 なので、外接箱の下端からの差に置き換えた。ワープの後にカメラが落ち着くまで 1 秒置いてから `aim-station.cs` をもう 1 回流し、2 回目の照準で F を押す〈前例の `DelayFrame(10)`・`AimAt` の静定待ちの代わり〉。`Mouse.current.WarpCursorPosition` が遠隔実行の snippet から使えるか・OS のカーソルを動かすかは推測。D-16）
  - `aim-source.cs`（クライアント）: `return Client.Game.InGame.Control.ViewMode.AimPointProvider.GetEffectiveAimSource().ToString();` → 左 Alt を押している間の三人称なら `Cursor`。`ScreenCenter` なら Alt が効いていない（一人称では Alt を受け付けない〈`UiStateCameraPolicyService.cs:91-93`〉か、Alt の KEYDOWN が届いていない）ので F を押さない

- [ ] **Step 2: 鍵送りを用意する**

- Mac（`$SP/x-switch-pr/ime/mac-keys.sh`）: `uitk-c2b-ime-mac-keys.sh` の形で、ゲームの pid を前面にしてから 1 鍵ずつ `osascript -e 'tell application "System Events" to key code <n>'`（0.25 秒おき）。修飾の長押しは `key down option`／`key up option`。毎回前面が moorestech か確かめ、違えば送らずに止める。終わり方によらず英数（102）へ戻し、`key up option` を送る（`trap`）。鍵: かな 104・英数 102・n 45・i 34・h 4・o 31・g 5・x 7・F 3・V 9・Space 49・Return 36・Esc 53・B 11・Tab 48・左 Option（Alt）58。左 Option の押下と解放は遠隔実行の呼び出しの前後に分けて送れるよう、`mac-keys.sh alt-down`・`mac-keys.sh alt-up` を別の呼び出しにする（`key down option` は解放を送るまで押されたまま。`trap` の `key up option` は `alt-up` と同じ）
- Windows（`$SP/x-switch-pr/ime/win-keys.ps1`）: S9 の報告 §2〜§4 の形。対話セッションの scheduled task から PowerShell で `SendInput` を呼び、仮想キーコード＋スキャンコードで送る（`KEYEVENTF_UNICODE` は使わない）。IME のオンは `VK_IME_ON`（0x16）、オフは `VK_IME_OFF`（0x1A）。鍵（VK/スキャン）: N 0x4E/0x31・I 0x49/0x17・H 0x48/0x23・O 0x4F/0x18・G 0x47/0x22・X 0x58/0x2D・Space 0x20/0x39・Return 0x0D/0x1C・Esc 0x1B/0x01・B 0x42/0x30・F 0x46/0x21・V 0x56/0x2F・左 Alt（VK_LMENU）0xA4/0x38（長押しは KEYDOWN と KEYUP を別の呼び出し〈`win-keys.ps1 -AltDown`・`-AltUp`〉で送り、その間に遠隔実行を挟む）。送る前に前面の窓が moorestech でなければ送らない。`finally` で IME をオフに戻し、左 Alt の KEYUP を送る

- [ ] **Step 3: 1 欄の手順（4 欄 × 2 OS で同じ。ただし 8 の Expected は欄で分ける）**

`blur.cs`（`FIELD` を欄ごとに置き換える）: `var f = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetRootElement().Q<UnityEngine.UIElements.TextField>("FIELD"); if (f == null) return "missing:FIELD"; f.Blur(); return "blurred";`

1. ゲームを起動してワールドに入る（Mac は Task 10 Step 3 と同じ。Windows は Task 9 の `launch.ps1 -CaseName ime -RemoteExec` と `hold.cs`。Mac は `open "steam://run/1958160//--remoteExec/"` で起動し、Player.log に `[RemoteExec] 待受を開きました` が出たことを確かめる。出なければ Steam の起動オプションに `--remoteExec` を入れる手を cua-driver で行い、終わったら外す）
2. 画面を開く: バグ報告は Esc → `open-bug-report.cs`、検索は B、BP 名は `open-bp-name.cs`、駅名は `player-pos.cs` → `place-station.cs`（`placed=True`）→ 1 秒 → 左 Alt を押下（離さない。Mac `mac-keys.sh alt-down`・Windows `win-keys.ps1 -AltDown`）→ 0.2 秒（前例の 2 フレーム）→ `aim-station.cs`（ワープと照準。`aimed=…`）→ 1 秒置いてもう 1 回 `aim-station.cs` → `aim-source.cs` が `Cursor` → F → 左 Alt を離す（`alt-up`・`-AltUp`）。`aim-source.cs` が `ScreenCenter` なら F を押さずに左 Alt を離し、V で三人称へ切り替えてから左 Alt の押下からやり直す（一人称では Alt が効かない。これは下の「1 回だけやり直す」に数えない）。照準より先に Alt を押すのは前例 `OpenStation` の順で、Alt の押下がカーソルを画面中央へ戻すため（D-39）。開いたかは `focus.cs` が `missing:` を返さないことで見る。`missing:`・`offscreen:`・`placed=False`・Alt を押しても `ScreenCenter` のときは「開けない（手順の失敗）」として、その欄の行を IME の合否と別に書き（どの段で止まったか・返り値）、置き方・照準を直して 1 回だけやり直す。それでも開けなければその欄は IME を `未確認（開けない）` にし、ユーザーの目視の項目（Task 14 Step 3 の IME の行に駅名・BP 名は既にある）で見てもらう旨を書く。`GameTextField` の持ち主へは回さない
3. `focus.cs` → `focused=True`
4. 変換中の取消: IME オン（Mac 104・Windows VK_IME_ON）→ n i h o n g o → Space → スクショ（変換中。下線と候補窓の位置）→ Esc → `read.cs`。`composition=[]` でなければ、もう 1 回 Esc → `read.cs`（変換中の ESC は IME が受け、UITK にもゲームにも届かない〈S9 の報告〉。`composition=[]` になった後は Esc を送らない＝余計な Esc は確定後の ESC になり欄が外れる）。Expected: 画面は開いたまま（`missing` を返さない。BP 名のモーダルも閉じない〈`ModalView.cs:110-114` の「変換中のESCでは取り消さない」〉）・`focused=True`・`value` に `日本語` が無い
5. 確定: n i h o n g o → Space → Return → `read.cs`。Mac で `composition=[]` でないときだけ（候補一覧からの 1 回目が確定にならないことがある〈台帳 R-15 の記録〉）、もう 1 回 Return → `read.cs`。`composition=[]` のときに 2 回目の Return を送らない（単行のモーダルでは BP 名の確定になって閉じる）
6. `read.cs` → `value` に `日本語` を含む・`composition=[]`
7. フォーカスの往復: `blur.cs` → `read.cs` で `focused=False` → `focus.cs` で `focused=True` → IME オフ（Mac 102・Windows VK_IME_OFF）→ x → `read.cs` → `value` に `日本語` を含み `x` で終わる・`focused=True`（S9 の「フォーカス往復の後の再入力」）
8. 確定後の ESC: Esc → `read.cs`。Expected は欄で分ける:
   - バグ報告・検索・駅名: `value` は 7 と同じ・`focused=False`。画面は開いたまま（`missing` を返さない）
   - BP 名（`modal-input`）: 同じ 1 回でモーダルが取り消される（`read.cs` が `missing:modal-input`。`ModalView.cs:71-73,110-114`。Web の Mantine と同じ 1 回で、ModalScreenPresenterTest が見る仕様）。値の確かめは 6・7 で済んでいる
9. 画面を閉じる（Esc。BP 名は 8 で閉じている）
Expected: 3〜8 のとおり。8 行それぞれ「OS / 欄 / 開けたか / 変換中の取消 / 確定後の値 / 往復の後の値 / ESC 後の値とフォーカス（BP 名はモーダルが閉じたか） / 候補窓の位置（スクショから: キャレットの直下か） / 合否」を `done.md`「## IME の一巡」へ。

- [ ] **Step 4: 不合格の処遇**

「開けない（手順の失敗）」と `未確認（開けない）` は不合格に数えない（Step 3-2）。不合格（3〜8 の Expected 外れ）は 1 行ずつ bd を `--parent <bd>` で立て（OS・欄・手順の番号・値・スクショ）、`GameTextField` の持ち主へ回す依頼を指揮へ返して `done.md` を「状態: 停止（IME の不合格 n 件の修正待ち）」にする。直らない見た目の差（候補窓の位置の数 px など、設計書 §3.4 に当たるもの）は R-19 の LX-218・219 の案に書き、止まらない。

- [ ] **Step 5: 片付け**: 両 OS でゲームを閉じ、IME をオフに戻したことを確かめる。Windows の `C:/moorestech-smoke/x-switch-pr/` を消す。スクショは各 OS 1 枚（変換中の下線と候補窓が見える欄を選ぶ）を `$SP/x-switch-pr/report/ime-win.png`・`$SP/x-switch-pr/report/ime-mac.png` の名前で写し（Task 14 Step 1 の manifest と同じ名前。どの欄の撮影かを `done.md`「## IME の一巡」に書き、manifest の caption の `<欄>` に写す）、他は消す。撮影が 1 枚も無い OS（全欄が開けなかった・Mac が `未確認`）はその名前のファイルを作らない

---

### Task 12: 検証の締め（最後の master の確認と完了の定義の証拠）

**Files:** なし（記録だけ）

**Interfaces:**
- Consumes: Task 1〜11 の記録
- Produces: `done.md`「## 完了の定義」・R-16 の最後の確認

- [ ] **Step 1: 最後の master の確認（R-16）**

Run: `. "$SP/x-switch-pr/vars.sh" || exit 1; . "$SP/x-switch-pr/rlist.sh" || exit 1; cd "$WT" && git fetch -q origin && git log --oneline "$FROZEN"..origin/master -- "${RLIST[@]}" && git log --oneline "$FROZEN"..origin/master | wc -l`（`"${RLIST[@]}"` は Task 2 の撤去済みの場所の一覧。`moorestech_web/` だけでなく `Client.WebUiHost/` など撤去済みの場所への master の変更も Web の更新として拾う）
Expected: Web のコミットが空。空でなければ Task 2 へ戻り、D-9 の表で再検証する。Web 以外の master のコミットの数は PR 本文の「検証の範囲」に「検証ビルド `<FROZEN 10 桁>` の後に master へ入った Web 以外のコミット n 件は検証ビルドに入っていない」と書く（D-10）。

- [ ] **Step 2: 完了の定義の表**

`done.md`「## 完了の定義」に統合計画 §5.1 の 1〜5 を 1 行ずつ: 1＝Task 1 Step 3 の 5b 行の出力（C-3 の 3 行目〈prefab の Canvas・CanvasGroup・CanvasRenderer 0〉を含む）＋Task 8 Step 6・Task 10 Step 2 の CEF 0、2＝Task 4 Step 1・2、3＝Task 9、4＝Task 7（ラダー 6 本・全録画）＋`bd show moorestech-wwxy` の CLOSED、5＝「X-release の持ち物（ユーザーのマージの後に playtest へ反映して 7 日以上と報告の確認 1 回）」。

- [ ] **Step 3: 戻す先を記録する（統合計画 §7.4・U-5）**

裁定の帰結は「切替の後に不具合が出たら、切替前のビルドへ戻すしかない」。マージの後に戻す判断が要ったとき PR から辿れるよう、今 playtest に出ているビルドを記録する:
```bash
. "$SP/x-switch-pr/vars.sh" || exit 1; python3 "$WT/.agents/skills/moores-playtest-release/scripts/find_live_playtest.py" "$RUNS"
```
Expected: 配信中の playtest の BuildID・run・コミット（moores-playtest-release 手順 3-1 の比較元と同じもの）。`done.md` の「## integrator への依頼」（X-release 宛て）と PR 本文の「マージの方法」の後に「戻す先: playtest の BuildID <id>（run <label>・commit <sha10>）。戻す手順は moores-playtest-release で Steamworks の playtest のライブをこの BuildID に戻す」と 1 行書く。取れなければ（スクリプトのエラー・該当なし）その出力を貼り、`done.md`「## 未完了」に残す（止まらない）

- [ ] **Step 4: 記録**: `../moorestech_logs` でコミット・push

---

### Task 13: moores-code-review で全ブランチレビュー（必須）

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。対象は Task 5 Step 1 と同じ絞り方で、Task 5 の後の feature 側の変更と master を運ぶマージの衝突の解き方（直接の取り込みは `git show --remerge-diff` と同じ。D-52）。patch は本体も bug-pass も `. "$SP/x-switch-pr/vars.sh" || exit 1; cd "$WT"; python3 "$SP/x-switch-pr/review_patch.py" "$REVIEW1_HEAD" HEAD <出力先>` で作り、`--base-ref` は渡さない（範囲の始まり `REVIEW1_HEAD` は Task 5 Step 1 が `vars.sh` と `done.md`「## 独立レビュー」に書いた値。`vars.sh` が消えていたら `done.md` から写す）（X-audit が `9f32a03390` まで、Task 5 がその後から凍結の前までを見たので、残りは取り込み・台帳・レビューの適用だけ。master 側から来たコードは対象にしない。D-11・D-31）。差分が台帳だけでも省略しない。自動適用・反映が `.cs` に触れたら、Mac の Editor は Task 8 から閉じたままなので、`moores-wt status` で自分の worktree の Editor が無いこと（他者の Editor を落とさない）を確かめて `uloop launch --project-path ./moorestech_client` → `uloop compile --project-path ./moorestech_client`（ErrorCount 0）→ Global Constraints 5 の「録画の前置き」を流してから、テストと録画をやり直す（AGENTS.md の compile の門）。設計判断は agent 既定で推奨案を採り判断記録へ、ゴール・裁定から外れるものだけ `done.md`「要裁定」。自動適用・反映がソースの判定経路・条件式に触れたら、`FROZEN` が変わるので D-9 の表に従ってラダー 6 本と影響する録画を反映後のバイナリで流し直し、配布物に入るコードに触れたら Task 8〜11 をやり直してから完了とする。

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
    "server の配布ビルド（build.yml の workflow_dispatch）: Server Build Windows・Mac success・X-5d が消した server の 17 名のエラー 0（Linux の DedicatedServer は matrix で無効のため焼いていない）",
    "Unity コンパイル: エラー 0 / Client.Tests とサーバーの Tests の全体（Windows で 19 本の regex を 20 本に割って流した）: 既知の Windows だけの失敗 <k> 件の外は失敗 0・既知の名前は Mac で流し直して失敗 0",
    "全録画: <n> 本中 PASS <a>（うち揺れ〈2 回目に PASS〉<s>）・前提つき（流し直して PASS）<c>（うち手動投入の interrupt 1 と運転役のラダー 6・言語ゲート・並べ比べ 3）・判定の対象外 1（resume）・不具合 0（撮影シナリオ uitk/prerun は X-prerun と同じく一覧の外）",
    "配布ビルド <LABEL>（BuildID <id>・commit <FROZEN 10 桁>・branch feature/uitk）: Windows/Mac の成果物の検査（CEF 0 を含む）合格・Windows 検証機 phase1/phase2 合格",
    "CEF のファイル: Mac の moorestech.app 0・Windows の Plugins/x86_64 0",
    "遠隔実行 3 点: 有効時 2・Harmony 20,777777,20・無効時に待受なし",
    "IME の一巡: Windows・Mac × バグ報告・検索・BP 名・駅名 <合格数>/8"
  ],
  "notes": ["全画面の並べ比べ（撤去前の最後の Web と UITK）: <X-prerun の報告ページの URL>", "要裁定: <件数と 1 行ずつ>"]
}
```
（`file` は `$SP/x-switch-pr/report/` からの相対。数値は実測で埋める）。`publish_pr_report.py` の `validate` は `media` のファイルが無いか 0 byte だと manifest の不正で exit 1 になる（`publish_pr_report.py:62-80`）ので、無いファイルの行を残さない: Mac の確認が `未確認`（Task 10 の「待ちが切れたとき」）なら `mac-inventory.png` と `ime-mac.png` の行を消し、verification の「CEF のファイル」の Mac の部分を `Mac の moorestech.app 未確認（Mac の Steam: <理由>）` に、IME の行の分母を流した OS の分（Windows だけなら `/4`）と「Mac 未確認」に書き換える。撮影が 1 枚も無い OS の IME の行（`ime-win.png`・`ime-mac.png`）も消す。書いた後に `for f in $(python3 -c 'import json,sys;[print(m["file"]) for m in json.load(open(sys.argv[1]))["media"]]' "$SP/x-switch-pr/report/manifest.json"); do test -s "$SP/x-switch-pr/report/$f" || echo "missing $f"; done` が何も出さないこと

- [ ] **Step 2: PR を作る**

pr-create スキルで head=`feature/uitk`・base=`master`（中継ブランチを作らない。cwd の枝が `feature/uitk` でなければ head を明示する）。題: 「UITK 一気切替: 全画面を UITK にし CEF と Web を撤去する（切替 PR）」。PR を作った後に `python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <番号> "$SP/x-switch-pr/report/manifest.json"` で報告ページを公開し（失敗したら報告 URL 無しで完了にしない）、本文を次の順で書く（先頭行はその URL の h1。tailnet の URL は書かない）:
1. `# https://review.moores.tech/pr/<番号>/report/`
2. 何を切り替えたか（撤去 5a〜5e の PR へのリンク・台帳の切替。このレーンの記帳・取り込みのコミットは PR を通さず `feature/uitk` へ直接積み、この PR の G1 で検査した旨の 1 行。D-3）
3. 検証の結果（manifest の verification と同じ。凍結した SHA と BuildID）・検証の範囲（D-10 の 1 行・server の配布ビルドは CI の dispatch で Windows・Mac だけ焼き、Linux の DedicatedServer は matrix でコメントアウトのため焼いていない〈R-23〉・Linux クライアントの除外の bd の id〈X-5b の申し送り〉・IME で開けなかった欄があればそれ）
4. 既知の差（台帳の「## 見た目の例外」の件数と LX-218・219 の案。目視で見てほしいもの）
5. 要裁定（独立評価で残った設計判断。無ければ「無し」）
6. ユーザーの目視の手順（下の Step 3 の文）
7. マージの方法: 「マージコミットでマージする（Squash and merge を使わない）。マージの後に X-release（playtest への反映）と X-cleanup（台帳と G1 の撤去 PR）を指揮が起こす。`delete-merged-branch.yml` により `feature/uitk` はマージ時に消える（D-17）」。続けて「マージの直前の確認（指揮が行う）: ユーザーから『マージする前の確認をして』と頼まれたら、指揮は (0) `done.md` の 1 行目が「（未確認: Mac の Steam）」を含むなら、ユーザーに「目視の手順の Mac の項目（Mac の Steam で起動・Tab のインベントリ・IME の 4 欄）は済みましたか」と 1 問聞き、済んだという返事が無ければ『Mac の目視が済んでいない。マージしない』と返して終わる（起こし直さない。Mac の Steam は完了条件で、`未確認` の間はユーザーの目視が代わり。目視もマージもユーザーのまま）。続けて `gh pr view <番号> --json mergeable -q .mergeable` が `MERGEABLE` であることを確かめる（`UNKNOWN` は GitHub が計算中なので間を置いて数回取り直す。`CONFLICTING` なら衝突がある。5a〜5d が消した個別のファイルを master が直すと modify/delete の衝突になり (1) の R-LIST には出ず、衝突のある PR では `pull_request` の workflow が走らないので (2) の run も現れない）(1) `git fetch -q origin && git log --oneline origin/feature/uitk..origin/master -- <done.md「## 取り込み」の R-LIST>` を流す (2) 空なら PR の G1・G4 をその時点の master で走り直させる（`feature/uitk` に空のコミットを足して push する: `git commit --allow-empty -m "chore(uitk): 切替 PR の G1・G4 をマージ直前の master で走り直す"` → `git push origin HEAD:feature/uitk`。G1・G4 の workflow は `pull_request` の既定の種類だけで base の更新では走らず〈`uitk-web-follow-gate.yml:5`〉、`gh run rerun` は前の merge ref のまま走る〈推測〉ので使わない）→ 同じ push で走った G1・G4・Platform Compile Check・Unity Test の run が全部終わるまで `timeout 580 gh run watch <run の id> --exit-status` を exit 124 の間だけ繰り返して待つ（Unity Test は shard で数十分かかる。G1・G4 だけが先に終わったところで返さない）→ 次が全部そろったらユーザーへ『マージしてよい』と返す: G1・G4 が pass、Platform Compile Check の `Compile - StandaloneWindows64`・`Compile - StandaloneOSX` が pass（server のプロジェクトだけをコンパイルする。`platform-compile.yml` の `projectPath: moorestech_server/`）、Unity Test（client と server の EditMode。UITK 側が master の API の変更でコンパイルできなくなったのを拾えるのはここだけ）が全 shard pass か、`gh run view <id> --log-failed | grep -c 'error CS'` が 0 で、落ちた shard の失敗の名前（Task 14 Step 4 の取り出し方）が `done.md`「## CI の既知の失敗」に全部含まれる (3) (0) が `CONFLICTING` なら『衝突あり。マージしない』、(1) が空でなければ『Web の変更あり。マージしない』、(2) がそろわなければ『CI が落ちた。マージしない』と返し、X-switch-pr のレーンを Task 2 の前文の「起こし直し」から起こし直す（worktree と `vars.sh`・`rlist.sh`・既知の集合・X-5d の名前の一覧を `done.md` から作り直してから、取り込み・記帳・D-9 の範囲の再検証。CI の落ちは原因を `done.md` に書いてから。D-53）。master の保護に必須の check が無い（`required_status_checks` が空）ので、緑のまま古い結果でマージできてしまうのと、凍結の後に master へ入った Web 以外の API の変更で merge ref のコンパイルが壊れたままマージできてしまうのを、この確認で止める（D-38・D-48）」。続けて Task 12 Step 3 の「戻す先」の 1 行

- [ ] **Step 3: 目視の手順の文（PR 本文と done.md「ユーザーの残タスク」に同じ文）**

読者: このゲームのオーナー。説明なしで使ってよい語: Steam のベータ、playtest-staging、IME、Ctrl+U、出展モード、環境変数。出展モードの起動の操作は Task 9 Step 5 で通った方を書く（下は 1 が通ったときの文。2 が通ったら 2 の手順に置き換える）。
```markdown
1. Steam のライブラリで moorestech → プロパティ → ベータで `playtest-staging` を選ぶ（パスワードは playtest-staging のもの）。ビルド ID が <BuildID> になるまで待って起動する
2. 見る項目（統合計画 §4.9）:
   - UI の大きさ: ウィンドウと全画面、1920×1080 と他の解像度で、文字・枠が崩れないか
   - Steam オーバーレイ（Shift+Tab）を開いて閉じた後も、操作とフォーカスが戻るか
   - 言語 4 種（設定で切り替え）: 数値の書式と、訳の無い文言の出方
   - 左 Ctrl+U（UI を隠す。左の Ctrl だけが効く）・カットシーン
   - 出展モード: 出展モードは**起動時に既定のワールド（`Saves/world_1`）を消す**ので、消してよい場所をワールドの置き場に指定して起動する。Windows は Steam を起動したまま、コマンドプロンプトで `set MOORESTECH_EVENT_MODE=1&& set MOORESTECH_DEFAULT_WORLD_DIRECTORY=%TEMP%\moorestech-event-world&& start "" "<Steam のライブラリ>\steamapps\common\moorestech\moorestech.exe"` を実行すると言語ゲートが出る（Steam の起動オプションでは環境変数を渡せない）。見終わったらゲームを閉じる
   - 日本語 IME を実キーボードで: 駅名・BP 名・ビルドメニューの検索・バグ報告に日本語を打ち、遅れ・候補窓のちらつき・全画面と他の解像度での候補窓の位置
   - （Task 10 で Mac の確認が `未確認` になったときだけ載せる）**完了条件の代わり。済むまでマージしない**: Mac の Steam で同じベータを入れて起動し、Tab でインベントリが出ることと、上の IME の 4 欄
3. 全画面を撤去前の Web と並べた比較: <X-prerun の報告ページの URL>
4. 気になったものは PR にコメントする。UI が原因で進めない不具合が無ければ、マージの直前に指揮へ「切替 PR をマージする前の確認をして」と頼み、「マージしてよい」（Mac が `未確認` ならその目視が済んでいる・衝突なし・master に Web の変更なし・G1/G4 と Platform Compile Check が緑・Unity Test の失敗が既知の内）の返事を受けてからマージコミットでマージする。返事が「衝突あり」「Web の変更あり」「CI が落ちた」のどれかなら、取り込みと再検証が済んで改めて「マージしてよい」が来るまでマージしない
```

- [ ] **Step 4: G1・G4 を見る**

Run: `gh pr checks <番号> --watch=false` を、job が終わったことを確かめるまで前景で数回（間は他の記録の作業をする。`sleep` で待たない）。
Expected: `UITK Web follow gate (G1)` と `UITK no-Web gate (G4)`（Task 1 Step 4 で写した job 名）が `pass`。他の check（Unity Test・Platform Compile Check 等）の結果も `done.md` に書く。Unity Test が落ちたら、失敗の名前と落ちた shard を取り出す（マージ直前の確認で指揮も同じ形で使う）:
```bash
RUN=$(gh run list --workflow run_test.yml --branch feature/uitk --event pull_request -L 1 --json databaseId -q '.[0].databaseId')
rm -rf "$SP/x-switch-pr/ci-xml"; gh run download "$RUN" -p 'Test results - *' -D "$SP/x-switch-pr/ci-xml"
python3 -c 'import pathlib,sys,xml.etree.ElementTree as E; [print(c.get("fullname")) for p in pathlib.Path(sys.argv[1]).rglob("*.xml") for c in E.parse(p).iter("test-case") if c.get("result")=="Failed"]' "$SP/x-switch-pr/ci-xml" | sort -u
gh run view "$RUN" --json jobs -q '.jobs[] | select(.conclusion=="failure") | .name'; gh run view "$RUN" --log-failed | grep -c 'error CS'
```
失敗の名前が Task 1 Step 6 の `known-win-failures.txt` か、前のレーンの `done.md` が CI だけの既知として書いたもの（X-5d の `client-play-1` の crash 等）に全部含まれ、`error CS` が 0 なら、その名前と xml の無い落ちた shard の名前を `done.md`「## CI の既知の失敗」に書く（マージ直前の確認の基準。指揮は `$SP` を読めないので `done.md` に置く）。外れた名前があれば原因を直す（台帳ならこのレーン、他は持ち主へ bd で停止）。G1・G4・Platform Compile Check が落ちたときも同じ。`CONFLICTING` なら pr-create の手順で master を取り込み（Task 2 の「2 回目以降の取り込み」の流れで Step 1〜6 と Task 3 Step 2〜6。`--no-commit` で止め、撤去済みの場所に戻ったファイルを列挙して消し、写すものは I-follow の手順で写して記帳する）、D-9 で再検証してから push。

- [ ] **Step 5: 片付け・報告・合図**

1. `moores-wt status` で自分の Editor だと確かめて閉じる（Task 8 から閉じたままなら確かめるだけ）。録画・`PlaytestResults/`・TestResults・撮影 PNG・`moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/`・`moorestech_client/.uloop/outputs/compare/X-switch-pr/` を消す（報告ページへ載せた後）。I-follow の参照を撮るために作った master の worktree があれば `moores-wt rm <名前>` で畳む。テストのワールドを `list-work-saves.sh` で探して消す
2. bd は閉じない。`bd update <bd> --notes "切替 PR #<番号> 作成。G1・G4 緑。ユーザーの目視とマージ待ち。マージ後に X-release が閉じる"`（D-18）
3. `done.md` の 1 行目を「状態: 完了」（Task 10 の「待ちが切れたとき」で Mac の確認を `未確認` にしたら「状態: 完了（未確認: Mac の Steam）」。止まったら「状態: 停止（理由）」）にし、完了条件ごとの証拠・PR URL・未完了・要裁定・ユーザーの残タスク（目視・マージ・要裁定の判断・Steam Guard の承認が要ったならその記録）・integrator への依頼（Step 2 の 7. の「マージの直前の確認」の手順〈指揮宛て。R-LIST の 1 行と「## CI の既知の失敗」を添え、PR の `mergeable` が `CONFLICTING` でないこと・空のコミットで走り直した Platform Compile Check の pass と Unity Test の失敗が既知の内であることも合格の条件だと書き、外れたときはこの計画の Task 2 の前文の「起こし直し」から起こし直すと書く。D-38・D-48・D-53〉・下の「## 設計書・実行計画へ反映する行」・LX-218・219 の案があれば I-fix で行と印を置く依頼・X-release への申し送り〈LABEL・BuildID・run dir・`promotion.md` の Mac の欄は「済み」か「未確認（Mac の Steam: <理由>。Task 10 Step 5 を飛ばしたので欄は空。反映の前に Mac を確かめるか、`--skip-mac-check` にユーザーの指示を引く）」のどちらか・先頭の「反映禁止」の行は反映の前に外す・戻す先の BuildID〈Task 12 Step 3〉〉・X-cleanup への申し送り）を書いて `../moorestech_logs` でコミット・push
4. 最後に `cmux wait-for -S uitk-x-switch-pr` を 1 回（途中で止まる場合も同じ合図を送る）。worktree の `moores-wt rm` は指揮が行う

## Self-Review

- Requirements の各行のタスク: R-1→T1、R-2→T2、R-3・R-4→T3、R-5→T4、R-6→T5、R-7→T6、R-8・R-9→T7、R-10〜R-12→T8、R-13→T9、R-14→T10（R-11 の Mac も）、R-15→T11、R-16→T6〜T12 の頭と T12 Step 1・T14 Step 2 の 7.（マージ直前の指揮の確認）、R-17→T14、R-18→T9 Step 5（出展モードの起動の手）・T14 Step 3、R-19→T11 Step 4・T14 Step 5、R-20→T12 Step 2、R-21→T13・T14、R-22→T1 Step 11（受けるタスクは「## 申し送りの受け」の表）、R-23→T8 Step 8
- プレースホルダ: `<FROZEN>`・`<LABEL>`・`<BuildID>`・`<番号>`・`<5c 削除>` は前のタスクの Produces で決まる実行時の値。G4 と全録画のスクリプトの名前は期待の形を書き、Task 1 Step 4 で実物から写して違えば D-15 に書く（まだ無い型の扱い）。`place-station.cs`・`aim-station.cs` は実物（`Game.Context.ServerContext`・名前で引く `ResolveBlockId` の形・前例 `OpenStation`）から写して埋めた。残る `<x>`・`<y>`・`<z>` は `player-pos.cs` の実行時の値から決める原点
- 保留の経路: 前提の欠け（T1）・I-follow の規模が 1 日に収まらないとき（T2。それ以外の写すものは停止せず写す。D-29）・取り込みの後の G4 の違反（T2 Step 3）・全録画の不具合（T7）・IME の不合格（T11。開けない欄は不合格に数えない）・G4 の違反（T4）・server の配布ビルドの失敗（T8 Step 8）・取り込みの衝突が解けないとき（T2 Step 2。D-46）はどれも「停止（理由）」と合図で指揮に届き、解ける条件（持ち主のマージ）と再開の位置（T2 か T6）を書いた。最小構成: master に Web のコミットが 0 件のとき T2 Step 4・5 は空で進む。未記帳が 0 件のとき（起こらない。5c の削除が必ず全束に当たる）でも T3 Step 2 の exit 0 で進む
- 凍結と再検証: HEAD が変わる経路（取り込み・レビューの適用・I-fix の取り込み）を D-9 の 1 つの表に集め、T6・T7・T8・T13・T14 から同じ表を引く
- 「台帳と G1 は消さない」: Files は台帳の 3 節だけで、`check-uitk-web-follow.py`・`uitk_web_follow/`・`uitk-web-follow-gate.yml` を触らない
- 検査の反映: 撤去の確かめのコマンドを表の外のコードブロックへ移した（表の `\|` が `-E` の下で偽の合格になるため）。G4 の名前は X-5e 計画の Produces（`check-uitk-no-web.py`・`UITK no-Web gate (G4)`）にそろえ、R-5・Task 1・Task 4・消えているべきものの表・manifest で同じ名前にした。全録画の列と分類は R-9・Task 7・D-8・反映する行の A-177 の改めで同じにした。テストの判定（既知の Windows の失敗の扱い）は R-7・Task 6・manifest・D-23 で同じにした
- 独立評価 1 周目の反映（D-28〜D-37）: 10 分を超えるコマンドは Global Constraints 4・T6 Step 2・T8 Step 8 で同じ割り方（19 本の regex・`timeout 580 gh run watch` の繰り返し・割れないものだけ本体の `run_in_background`）にした。受け口の環境変数は Global Constraints 5 の前置き 1 つにし、T1 Step 9・T1 Step 12・T7 Step 1・T13・D-9 の表の末尾から同じ前置きを引く。master の取り込みは T2 の手順 1 つ（`--no-commit`・R-LIST・I-follow の手順）にし、R-2・R-16・T6・T12 Step 1・T14 Step 4 から同じ手順と同じ `"${RLIST[@]}"` を引く。I-follow の写しでコードに触れる例外は「やらないこと」・ファイルの持ち主・D-29 で同じ書き方にした。レビューの絞り方は T5 Step 1・T13・D-11・D-31 で同じ。IME の欄ごとの Expected は R-15・T11 Step 3・D-34 で同じ。申し送りは R-22・「## 申し送りの受け」・T1 Step 11・D-35 で同じ表を引く
- 独立評価 2 周目の反映（D-38〜D-43）: マージ直前の確認は R-16・R-17・T14 Step 2 の 7.・Step 3 の 4.・Step 5 の integrator への依頼・D-9 の空のコミットの行で同じ手順にした。19 本・61 名・17 名は R-7・R-23・Global Constraints 4・申し送りの表・T1 Step 6/11・T6・T8 Step 5/8・T10 Step 3・manifest で同じ数にした。実行時の値と R-LIST は Global Constraints 11 の `vars.sh`・`rlist.sh` を R-LIST を使う全ブロック（T2 Step 1/2・T6〜T12 の頭・T12 Step 1）と `$LABEL`・`$FROZEN` を使うブロック（T8 Step 2/4/5/8）で読む。駅の開き方（Alt を照準より先）は T11 の Interfaces・Step 1・Step 2・Step 3-2・D-39 で同じ。揺れの扱いは R-9・T7 Step 3/5・manifest・D-41 で同じ
- X-5a からの依頼の反映（D-44・D-45）: `run-scenario.sh` では流せない 3 本の扱いは R-9・T1 の Interfaces と Step 4・T7 の始める前/終えた後・Step 2（`list-run.txt`）・Step 3 の `前提つき`・Step 3b・Step 5・申し送りの表・manifest で同じにした。`list-recordings.sh` の標準エラーは空の前提で T1 の Interfaces・T7 Step 2・申し送りの表をそろえた
- 独立評価 3 周目の反映（D-46〜D-51）: 取り込みの衝突の決まり（C# 以外の両側変更）は R-2・Global Constraints 8・T2 Step 2/3・D-6 の下の記録・D-46 で同じ。2 回目以降の取り込みは T2 の前文・T2 Step 1（`PRE_MERGE_HEAD` を残す）・T3 の始める前・D-9 の取り込みの 2 行・T14 Step 4 の `CONFLICTING`・D-49 で同じ流れを引く。マージ直前の確認の合格の条件（G1・G4・Platform Compile Check・Unity Test が既知の内・`error CS` 0）は R-16・R-17・T14 Step 2 の 7.・Step 3 の 4.・Step 4（「## CI の既知の失敗」を作る）・Step 5 の integrator への依頼・D-48 で同じ。Mac の Steam の前提は R-14・T1 Step 8b・T10 の始める前と Step 1・T11 の始める前・T14 Step 3・D-47 で同じ。レビューの patch は R-6・T5 Step 1・T13・D-50 で同じ `review_patch.py` と `REVIEW1_HEAD`（Global Constraints 11）。保留の経路に「取り込みの衝突が解けない（T2 Step 2）」を足し、Mac の Steam の待ちは停止せず `未確認` へ回す
- 独立評価 4 周目の反映（D-52〜D-58）: 起こし直しは GC 11・T2 の前文・R-16・T14 Step 2 の 7.・Step 5 の integrator への依頼・D-53 で同じ手順を引き、`vars.sh`・`rlist.sh` を読む全部の行に `|| exit 1`、X-5d の名前の一覧を使う T8 Step 5・Step 8・T10 Step 3 に `test -s`。見た目の比べ直しは T1 Step 6（物差しの小節）・Step 12（TSV と `SHOT_BASE`）・T6 Step 1b・D-9 の 5 行目・D-33・D-56・File Structure で同じ比べ方（矩形の `diff` は全状態・`inf` は「PSNR で比べる状態」だけ）。出展モードの 3 本と撮影シナリオの外し方は R-9・T1 の Interfaces と Step 4・T7 Step 1/2/3b/5・申し送りの表・D-55 で同じ。Mac が `未確認` の締めは R-14・R-16・T10 Step 1・T11 Step 5・T14 Step 1/2/3/5・D-58 で同じ。マージ直前の確認の (0)（Mac の目視・`mergeable`）は R-16・T14 Step 2 の 7.・Step 3 の 4.・Step 5 の依頼で同じ。G4 の I-follow の違反の持ち主は T2 Step 3/4・T4 Step 1・D-57 で同じ。レビューの patch は R-6・T5 Step 1・T13・D-52 で同じ `review_patch.py`
- unityプレイ録画テスト: 含む（T7 のラダー 6 本と全録画。このレーンはランタイムのコードを変えないが、切替 PR の完了条件そのもの。I-follow の写しがあれば T2 Step 4 でその画面の録画も流す）

## 判断記録（ADR）

従う裁定: `.decisions/2026-11-06-*.md` の 7 本（とくに「一気切替の日に CEF と Web を同時に撤去」「切替前のビルドはどこにも配らない。切替 PR の中でだけ Windows/Mac ビルドと検証機の通し検証を回す」「最終検証中に master へ Web の更新が入ったら取り込んで追従し影響箇所を再検証」「master への切替 PR のマージはユーザー」）。

W6 の agent 既定（指揮の割り振りの写し）:
- W6-1: Codex を使わない。§5 の外部監査・codex-audit は opus 2 体の独立評価。pr-independent-review はスキルが削除済み（ed461b35de、2026-10-30）→ Task 5。根拠はユーザーの指揮への指示（`../moorestech_logs/harness/uitk/conductor.md`「ユーザー指示（2026-11-06 15:55）」の原文「一旦codexは使わず、claudeセッションで今からこの計画の遂行をして」）で、これが `.decisions/2026-10-16-moores-writing-plansの実装引き継ぎはCodexセッションに固定する.md` と実行計画 §8 E-3（integrator レーンも Codex セッションで回す）を W6 について上書きする
- W6-2: Mac Editor 1 本・それ以上は winunity（ユーザー指示 2026-11-11「やはりunity2つは安定しないので1つに絞ってほしい」。`../moorestech_logs/harness/uitk/conductor.md`「2026-11-11 21:30 Mac Editor を 1 本に」）
- W6-8: シナリオの書き換え・削除は全部 X-5a が持つ。このレーンはシナリオを書かない（全録画の不具合は bd で回す）
- W6-9: 全録画のスクリプトは X-5a が系の引数を外したものを使い、書き換えない
- W6-12: バグ報告の manifest は v8（`webUiHealth` を外した。7 は S5-crosscut の `gameUiExceptions` が使い、その欄は残る。PLAN-W5 の申し送り）。セーブ形式ではない
- W6-13: `release-artifact.sh` の CEF helper の検査は 5b が「CEF のファイルが 0」へ反転済み。このレーンはビルドで流すだけ
- W6-14: 撤去後の突き合わせは `--web-rev <5c の削除の親>` で回す（`--web-rev` 無しは全束 0 件で exit 2）
- W6-17: 「CEF の存在しないビルドでラダー 6 本と全録画」は、DSL が Editor 専用（`Client.Playtest.asmdef` の includePlatforms が Editor）なので、切替 PR の HEAD（CEF パッケージの無いプロジェクト）の Editor で流す。配布ビルドでは Windows 検証機の phase1/phase2・遠隔実行の 3 点・`release-artifact.sh` の CEF 0 の検査・Mac の Steam の起動確認・IME の一巡（OS 段の合成キー＋遠隔実行で画面を開きフォーカスし値を読む。入力欄はバグ報告・検索・BP 名・駅名）を行う。ビルドで DSL を動かす仕組みは作らない
- W6-18: このレーンは A-259（検証の読み）と LX-218〜219（目視と IME で見つかった差の予約）

この計画の判断（agent 既定）:
- D-1（Phase 2.6 検査7: 配布の裁定は「オーナー向け staging にも配らない」と書くのに、playtest-staging へ上げてオーナーに staging で目視させてよいか → このままでよい（staging へ上げ、オーナーの目視も staging のビルドで行う）。出所 agent 既定〈指揮の答え〉）: 裁定 `.decisions/2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md` の決定は「切替前のビルドはどこにも配らない（テスターにもオーナー向け staging にも）。確認は Unity Editor の中だけ。切替 PR は配布物から CEF を消すので、その PR の中でだけ Windows/Mac ビルドと検証機の通し検証を回す」、帰結は「切替前に配布環境で UITK に触れるのは切替 PR の検証だけになる」、棄却案は「段階の区切りでオーナーが staging ビルドを目視する」。実行計画 §1.5 の X-switch-pr 行は「playtest-staging へのアップロードは裁定の『切替 PR の中の検証』の範囲として行い、playtest への反映（moores-playtest-release の手順 3 以降）はユーザーのマージ後の X-release でだけ行う」と明記し、§7 の U-4 は「検証ビルドで 1 回だけ目視」、統合計画 §2.0（`docs/uitk-migration/plan.md:101`）も「オーナーの目視は 1 回、切替 PR の検証ビルドで、マージの前に行う」と書く。裁定が禁じたのは切替 PR の外（段階の区切り・テスター）への配布で、帰結の文が切替 PR の検証を配布環境で行うことを認めている。検証機の通し検証（`steam.exe -applaunch`。`run-smoke.ps1:129`）と Mac の Steam の確認は staging に上がったビルドでしか回らない。staging の配信先はオーナーと検証機だけ: staging は `playtest` と別のパスワードのブランチで、テスターへ配るのは `playtest` のパスワードだけ（`scripts/playtest/README.md`「Steamworks 側」2）、検証機はテスター用アカウントで staging を選んでおく決まり（同 6）。よって裁定の文は staging への上げとオーナーの 1 回の目視を禁じていると読めず、要裁定にしない。playtest への反映は X-release
- D-2（agent 既定）: `release-playtest.sh` はメインクローン（master）ではなく、凍結した HEAD の worktree（`$WT`）の `scripts/playtest` から流す。master の `scripts/playtest/lib/release-artifact.sh:54-64` は Mac 成果物に `cef-unity-server.app` が無いと exit 4 で落とすので、CEF の無いビルドは master の版では検査を通らない。worktree はメインクローンと object を共有するので、README の「別 clone から流すと object が無い」には当たらない（`moores-wt` もメインクローン固定で worktree を作る〈`~/bin/moores-wt:25`〉）。アップロードのやり直し（`upload_and_verify.sh`）だけはメインクローンの版を使う（取り込みロックの置き場がメインクローン基準・中身に CEF を見ない）。棄却: 依頼文のとおりメインクローンから流す（Mac の検査で必ず落ちる）、master の検査を一時的に外す（master を触る・配布物の検査を弱める）
- D-3（agent 既定）: このレーンのコミットは中継ブランチも `feature/uitk` 向けの PR も作らず、`git push origin HEAD:feature/uitk`（早送りだけ。force しない）で積む。切替 PR の head を `feature/uitk` にする裁定（統合計画 §2.6「中継ブランチ禁止」）と「焼くのは feature/uitk の HEAD の SHA」を両立する形で、G1 は切替 PR がこのレーンのコミットを全部検査する（E-2 の「feature/uitk 向けの PR で G1 を走らせる」の目的は切替 PR の G1 が満たす）。W6 は直列で、このレーンの間に指揮は I-weekly を回さない（取り込みはこのレーンの Task 2 が持つ）。
  Phase 2.6 検査7（実行計画 §8 E-2「レーンは pr-create で base=feature/uitk の PR を開き…レーン自身がマージコミットで取り込む」を覆して直接 push してよいか → このままでよい。E-2 の例外として残し、実行計画に注記を足す〈末尾の反映する行〉。出所 agent 既定〈指揮の答え〉）: 切替 PR は head が `feature/uitk` そのものなので、記帳・取り込みのコミットは `feature/uitk` に積むしかない（中継ブランチは統合計画 §2.6 が禁じる）。E-2 が「ローカルマージで push」を棄却した理由は「G1 が切替 PR まで走らない」だが、このレーンでは切替 PR 自身で G1 が走るので当たらない。裁定 `.decisions/2026-11-06-CEF停止の記録はUITK長命ブランチから先にmasterへ切り出す.md` の「段階 2〜5 の各レーンは、moores-code-review を通したうえで agent が長命ブランチ feature/uitk へマージする」は段階 2〜5 のレーンの決まりで、このレーンは字面でも外れない。PR を通らない間は CI の Unity テストが回らないので、自動適用・取り込みがコードに触れたら push の前に compile と該当のテストを手元で流す（Task 2 Step 3・Task 5 Step 4・Task 6）。新しいやり方なので PR 本文の「何を切り替えたか」に 1 行書く
- D-4（agent 既定）: `localizationKeys.ts` の再生成だけのコミットは `写した`（前例: 台帳の束 1・3 の既存の記録）、それ以外の feature/uitk 上の Web コミットは `捨てた（理由）`。実行計画の完了条件の「捨てた で記帳」は「写すものが無いコミットは捨てた」と読む
- D-5（agent 既定）: 束 2・4・5 の起点が台帳に無ければ停止する。起点はその束のレーンが「写した画面の範囲」を知って決めるもので、このレーンが後から置くと、それより前の master の Web 変更が記帳の対象から外れる
- D-6（agent 既定）: 最新の master をビルドの前に取り込む（I-weekly の役をこのレーンが持つ）。マージされる結果に近いものを検証するため。衝突は消したファイル側で解き、UITK へ写す変更はこのレーンが停止せずに I-follow の手順で写す（D-29 で改めた。規模が Editor 1 本の 1 日に収まらないときだけ停止して指揮へ返す）。C# の衝突の解き方は opus の subagent に任せ、解いた内容をこの下に 1 行ずつ足す
- D-7（agent 既定）: 台帳の書き換えは「## 束」の状態と「## 行」の所有者の列だけを機械的に行い、起点・他の列・他の節は変えない（`ledger.py:171-176` の規則「全束が切替済みのときだけ UITK」をそのまま満たす）
- D-8（Phase 2.6 検査7: 設計書 [A-177] は切替 PR を名指しして TSV の `system` 列・6 分類・「Web 側の失敗は段階の前のコミットで流し直して既存と退行に分ける」を決めているのに、計画は 3 分類・`system` 列なしにしていた。どちらが正か → A-177 に合わせる。出所 agent 既定〈指揮の答え〉）: 列は A-177 の `scenario・system・exit・seconds・run_dir`（X-5a が外した `system` は `GameUi` で埋める。Task 7 Step 3）、分類は A-177 の 6 つ（PASS／束 5 未移植／wwxy／前提つき／Web 専用／不具合）。撤去の後は Web 側が流せないので、A-177 の「Web 側の失敗を段階の前のコミットで流し直す」は「X-prerun の UITK 側の結果（撤去前の最後の全録画）と比べる」に読み替え、比べた印の名前は X-prerun 計画 Task 4 Step 3 の `既存`／`退行`／`比較不能（新設）`／`比較不能（変更）` にそろえる。印は分類を広げるので、A-177 の改め案を末尾の「設計書・実行計画へ反映する行」に置く（最後の Web 側は X-prerun。A-251）
- D-9（agent 既定）: `FROZEN` が変わったときの再検証の範囲:

| HEAD を変えたもの | やり直すもの |
|---|---|
| 台帳・`.md`・`.github/scripts` の G1/G4 だけ・G1/G4 を走り直すための空のコミット（ビルドの入力に入らない） | Task 3 Step 5・Task 4。ビルドと録画はやり直さない（PR 本文に「検証ビルドと HEAD の差は台帳・文書だけ」と書く） |
| master の取り込みで `moorestech_web/`・`Client.WebUiHost/` の削除の解決だけ（かつ `git diff --name-only "$FROZEN" HEAD` が台帳・`.md`・R-LIST の削除だけ。`git merge origin/master` は Web 以外の master のコミットも必ず連れてくるので、それが client・server・`Packages`・`scripts/playtest`・シナリオに触れていれば、この行でなく変わったパスで下の行を引く。D-54） | Task 2 の「2 回目以降の取り込み」の流れ（記帳は Task 2 Step 5 の下書きと Task 3 Step 3〜5）・Task 4。ビルドと録画はやり直さない（PR 本文に「検証ビルドと HEAD の差は取り込みの削除の解決だけ」と書く） |
| master の取り込みで撤去済みの場所に戻ったファイルの `git rm` だけ（写すもの無し。上の行と同じ `git diff --name-only "$FROZEN" HEAD` の条件つき） | 上の行と同じ（戻ったファイルは消えてマージの結果に残らない。Task 2 Step 2 の R-LIST が 0 行であることを `done.md` に書く） |
| `moorestech_client/**`・`moorestech_server/**`・`Packages/**`・`scripts/playtest/**` に触れた取り込み・レビューの適用・I-fix・I-follow の写し | Task 6〜11 の全部（配布物が変わるため）と、server に触れたら Task 8 Step 8 |
| 上の行のうち `Client.Ui` の系統（`Client.Ui/**`・`Client.Ui.State/**`・`Client.Ui.Editor/**`・`Assets/UI Toolkit/**` の USS・tss・C#）に触れたもの | 加えて Task 6 Step 1b（ギャラリーとスモークの撮り直し・矩形の `diff`・「PSNR で比べる状態」の `inf`）を、`git diff --name-only "$SHOT_BASE" HEAD` の対象のパスで流し直す。Expected は Task 6 Step 1b と同じ（撤去 5a〜5e がそろえた見た目の不変の確かめを、このレーンの変更にも掛ける。D-33・D-56） |
| シナリオ（`S/**`）・`tools/uitk/**` だけ | Task 7 の該当シナリオとラダー 6 本 |

どの行でも、Editor を起動し直したら・録画を流し直す前に、Global Constraints 5 の「録画の前置き」を流す（受け口の値は Editor のプロセスにしか残らない）。

- D-10（agent 既定）: 検証中に master へ入った Web 以外のコミットは、衝突しない限り取り込まない。裁定が再検証を求めるのは Web の更新で、Web 以外の変更まで取り込むたびに 70 分のビルドからやり直すと切替が延び続ける。取り込まなかった件数を PR 本文の「検証の範囲」に書く
- D-11（agent 既定）: レビューは 2 回に分ける。Task 5 で X-audit の後の feature 側の全部（`9f32a03390` より後。master 側から来たコードは除く。D-31）を凍結の前に見て、ビルドと検証が直した後のコードに当たるようにする。最終タスク対の Task 13 は Task 5 の後の差分（取り込み・台帳）だけを見る。自動適用がコードに触れたら D-9 の表でやり直す。持ち主のあるファイルの機械的でない修正は、このレーンが直さず bd で I-fix へ回す
- D-12（agent 既定）: 約 70 分の `release-playtest.sh` は、moores-playtest-release 手順 1 のとおり本体が Bash の `run_in_background` で起動し、Monitor でログの行を受ける（前景の上限 600 秒に収まらず、スクリプトは途中で分けられない。Steam Guard の承認は約 1 分で切れるので、ログの行をすぐ受ける必要がある）。subagent には渡さない。ほかの待ちは前景（検証機の上の待ちは PowerShell の中で回す・Steam の更新待ちは Monitor の until ループ・CI は `gh pr checks` を前景で見る）
- D-13（agent 既定）: 配布ビルドの batchmode の Unity が動く間は Mac の Editor を閉じる（Mac の Editor 1 本の指示と同じ理由で、Unity を 2 本動かさない）。検証機と winunity が同じ Windows PC なら、検証機を使う Task 8〜11 の間は winunity を使わない（同じ対話セッションで Steam のゲームと Editor が前面を取り合う）
- D-14（agent 既定。独立評価 1 周目で改めた → D-30）: CI の `ビルド検証` ラベルは付けない。完了条件の client のビルドは「焼くのは feature/uitk の HEAD の SHA」の配布ビルドで、それは手元の `release-playtest.sh` が焼いて Steam と検証機で確かめる。server の配布ビルドだけは配布の道具に工程が無いので、`build.yml` を `workflow_dispatch` で 1 回流す（Task 8 Step 8）。CI のビルドは配布物にしない
- D-15（agent 既定・実行時に足す）: Task 1 Step 4 で写した G4・全録画のスクリプト・ラダーの実際のパスが期待と違えば、ここに「期待 → 実物」を 1 行ずつ足し、以後のコマンドを読み替える
- D-16（agent 既定・推測を含む）: IME の一巡で画面を開くのは、プレイヤーの経路（Esc・B・F）を OS の鍵で通せる所はそれを使い、ボタンとモーダルは遠隔実行で開く（DSL が配布ビルドで動かないため。W6-17）。`NavigationSubmitEvent` で `Button.clicked` が呼ばれる・`BlueprintNameInputState.Open()` だけでモーダルが出る、はどちらも推測で、最初の 1 回で確かめる。外れたら代わりの手（OS のクリック／BP 名だけ Editor の合成キーと目視へ）を実行時にここへ書く。駅は前例 `OpenStation` を遠隔実行（ワープ・`Mouse.current.WarpCursorPosition` の照準）と OS の鍵（左 Alt の長押し＋F）で写す（独立評価 1 周目。D-34）。開けなかった欄は「開けない（手順の失敗）」として IME の合否と分け、`GameTextField` の持ち主へ回さない
- D-17（agent 既定）: `.github/workflows/delete-merged-branch.yml` はマージされた PR の head のブランチを消すので、切替 PR のマージで `feature/uitk` が消える。X-release・X-cleanup は master で行い、切替後に `feature/uitk` を使う工程は無いので止めない。PR 本文に書いてユーザーに知らせる
- D-18（agent 既定）: このレーンの bd は PR を作った時点では閉じない（完了条件の残り＝ユーザーの目視とマージが残る）。X-release が閉じる
- D-19（agent 既定）: 目視と IME で見つけた直せない差の LX-218・219 は、文面だけこのレーンが `done.md` に書き、台帳の行と USS・C# の印は I-fix が同じ PR で置く（`LookExceptionLedgerTest` が行と印の一致を見るので、行だけを先に置けない。このレーンは USS・C# を持たない。前例は S4-gamma 計画 `docs/superpowers/plans/2026-11-10-uitk-s4-gamma.md` の LX-141〜145 の段取り〈起草時は計画の中だけで、台帳にはまだ無い〉）
- D-20（agent 既定）: moores-playtest-release の手順 4（Mac の Steam）はこのレーンが staging のビルドで行い、`promotion.md` の Mac の欄を埋める。X-release の「Mac の確認項目をラダー 6 本の先頭操作のスクショへ」は反映の前の別の確認として X-release が持つ（実行計画の 2 行の重なりの読み）
- D-21（Phase 2.6 5-B: `hold.cs`・`harmony.cs` が局所関数を `method.Name.Contains("g__Hold|")` の部分一致で引くのを、列挙で閉じるべきか → このままでよい。出所 agent 既定）: 前例 `$RUNS/remote-exec-listener-20261026-172117/snippets/` の写しで、コミットしない検証用の snippet。C# のコンパイラが局所関数に付ける名前（`<…>g__Hold|n_m`）を引く口はほかに無く、`Single` は 0 件・2 件以上で例外を投げ、遠隔実行の応答の `outcome` が `RuntimeException` になって Task 9 の Expected と食い違うので気づける
- D-22（Phase 2.6 5-I: `build-info.json` の `commit` を空・欠損の分岐なしで表示へ出すのを直すか → このままでよい。Expected に「空・別の SHA・エラーは不合格」と書いた。出所 agent 既定）: 値は表示して `<FROZEN>` と目で照合するだけで、欠損は照合の不一致として必ず落ちる（Task 8 Step 6・Task 10 Step 2）。分岐を足しても気づく人と場所は同じ
- D-23（設計検査〈実物〉: Windows の `^Client\.Tests\.Ui\.` に既知の失敗 14 件があり、R-7 の「全 shard で失敗 0」が必ず落ちる → 既知の名前の集合の外を 0 にする判定へ改め、既知の 3 クラスは Mac の Editor で失敗 0 を確かめる。サーバーの `Tests.*` は `winunity test-all` に入らないので Mac で流す。出所 agent 既定。流し方と既知の集合の出所は D-28 で改めた）: 14 件は `-nographics` の描画とパス区切りの Windows だけの失敗で（X-audit `done.md` の「コンパイル・テスト」の行。I-fix5 R-19 の既知）、切替の可否と関係しない。既知を許すと新しい失敗が紛れるので、許すのは名前の集合だけにし、件数と顔ぶれを `done.md` に書く
- D-24（設計検査〈実物〉: Task 4 Step 2 の `git grep -E 'Kestrel|Microsoft\.AspNetCore'` は撤去計画の外に残るコメント〈`RemoteExecHttpListener.cs:14-15,45-46`・`EventModeStartGate.cs:22-23`〉に当たって 0 にならず、DLL まで舐めて 120 秒を超える → `.cs`・`.asmdef`・`packages.config` のコメント以外の行だけを 0 の判定にし、コメントに残る語は G4〈`Kestrel` を検出する語に持つ〉の許可リストに任せる。出所 agent 既定）: 統合計画 §5.1-2 の「Web 用の Kestrel が無い」はコードとして無いことで、コメントの語の掃除は X-5e の持ち物（X-5c 計画 D-6 が 5e の掃除へ申し送った）
- D-25（シミュレーター予測〈agent 前提・拒否権つき〉: オーナーの目視の手順の「`MOORESTECH_EVENT_MODE=1` を付けて起動」は、Steam から起動するビルドに環境変数を付ける具体の操作が無く、出展モードが既定のワールドを消すことも書いていない → Task 9 Step 5 で検証機で起動の手を確かめ、通った操作とワールドの置き場の指定〈`MOORESTECH_DEFAULT_WORLD_DIRECTORY`〉を手順に書く。UI を隠すのは左 Ctrl+U だけ〈`UIRoot.cs:18`〉と書く）: 出展モードの有効化は環境変数だけ（`EventExhibitionSettings.cs:12,48`）。起動時に `GameSystemPaths.DeleteDefaultWorldDirectory()`（`EventModeAutoStartActions.cs:12`）が走るので、置き場を替えずに起動させるとオーナーのワールドが消える
- D-26（シミュレーター予測〈agent 前提・拒否権つき〉: `verify-on-windows.sh` は Player.log を回収せず、phase1 の分は `Player-prev.log` に回り、Task 9 の起動で両方が上書きされる → Task 8 Step 5 で Task 9 の前に 2 本を ssh で取る）: 回収は `results` の scp だけ（`verify-on-windows.sh:81`）。`Player-prev.log` に phase1 が入るのは Unity の既定の動きの推測で、取れた log の中身で phase を確かめる
- D-27（シミュレーター予測〈agent 前提・拒否権つき〉: 依頼文はメインクローンの `scripts/playtest` から焼くと書くのに計画は worktree の版から焼く → D-2 のまま。依頼文から外れたことを `done.md` の冒頭に 1 行書く〈Task 8 Step 7〉）: ユーザー規約「同じものが使えず別のものを使ったら、何を何に替えたかと理由を報告の最初に書く」
- D-28（独立評価 1 周目: `winunity test-all`〈通しで約 40 分。`~/bin/winunity:87-90` は ssh で同期に待つ〉と `--timeout-seconds 1500` の `uloop run-tests` は前景の 600 秒で切れ、`summary-all.txt` が回収できず R-7 が判定できない。既知の失敗の集合が X-audit の `^Client\.Tests\.Ui\.` の 14 件だけで、Ui 以外の本の Windows だけの既知の失敗が「既知の外」になる → 1 本 10 分未満の 18 本の regex〈X-5d 計画 Task 2 Step 1 の表と割り方。否定先読みの補集合で `Client.Tests` と `^Tests\.` を重なりなく覆う〉を前景で 1 本ずつ流し、割れないもの〈初回の `winunity compile`・最初の `--editor` の本・1 クラスで上限を超えるもの〉だけを本体が `run_in_background` で 1 回起動し完了の通知で受ける。既知の集合は X-audit の 14 件・X-5a の `test-all` の起点と最終 HEAD・X-5d の撤去後の 18 本の失敗の名前の和にし、名前は全部 Mac で流し直して失敗 0 を確かめる。出所 agent 既定〈指揮の全レーン共通の補足〉）: 前例 X-5d が同じ 18 本を起点と撤去後に流すので、同じ表を使えば既知の集合と比べ方がそろう（X-5d が W-C6 を足して 19 本にしたので D-38 で 19 本に改めた）
- D-29（独立評価 1 周目〈2 体とも重〉: master の取り込みで、消したディレクトリの中へ master が足したファイルは衝突にならず黙って入り、`git status` の衝突の印にも出ない〈別の subagent が scratch の repo で再現: 衝突なし・status 空・grep 0 行。modify/delete は `DU`、新規は `A `〉。`--no-ff` は衝突が無いとその場でコミットし、戻ったファイルは Task 4 まで、push の後まで気づかれない。`Client.WebUiHost/` へ .cs が戻れば asmdef の無い C# になる。さらに 5a〜5d が消した C# の modify/delete を「両方の意図を保つ」で解くと消したファイルが戻りうる → 取り込みを `--no-commit` で止め、撤去済みの場所の一覧〈R-LIST〉に在るファイルを列挙して 0 を確かめ、在れば中身を UITK 側へ写して〈I-follow の手順〉記帳し、ファイルは `git rm` でマージのコミットに含める。5a〜5d が消したパスの modify/delete も消す側で解く。取り込みの後に C-1〜C-9 と G4 を流す。出所 指揮の補足〈「取り込みの直後に『撤去済みの場所に在るファイル』を列挙して 0 を確かめ、在れば中身を UITK 側へ写して〈I-follow の手順〉記帳し、ファイルは消す手順にする〈停止にしない〉」〉）: 起草時の今の master では分岐点〈11/06〉の後の追加は 0 件だが、9〜11 月に `moorestech_web/` へ 85 件・`Client.WebUiHost/` へ 49 件の追加があった〈再現役の実測〉。指揮の補足は戻ったファイルについて停止にしないと決めたので、同じ「写すものがある master の Web 変更」である modify/delete の中身と、取り込んだ Web コミットの写すものも同じ手順にそろえ、このレーンが停止せずに I-follow の手順で写す（前の版は「停止（I-follow 待ち）」だった。agent 既定）。写す規模が Editor 1 本の 1 日に収まらないときだけ停止して指揮へ返す。棄却: 戻ったファイルを消すだけで写さない（master の Web の変更が UITK に入らず、裁定「最終検証中に master へ Web の更新が入ったら取り込んで追従」から外れる）／I-follow を待って停止（戻ったファイルを消す手順が無く、やり直しても同じ所で止まり続ける）
- D-30（独立評価 1 周目と X-5d の依頼〈X-5d R-12 (2)・D-20〉: server の配布ビルドを焼く工程がどこにも無い → 切替 PR の HEAD で `gh workflow run build.yml --ref feature/uitk` を 1 回流し、`Server Build` の Windows・Mac の success と、X-5d が消した 13 件の名前のエラー 0 を R-23 の証拠にする〈名前は D-38 で 17 名に改めた〉。出所 agent 既定〈指揮の補足〉）: 別の subagent の再現では「一部」: server のスクリプトのコンパイル不足は PR ごとの `Platform Compile Check`〈`platform-compile.yml`。プレイヤービルドはしない〉が切替 PR で拾うので、cron まで出ないのはプレイヤービルドで初めて出る不足〈ビルド時の参照の解決・プラグインの .meta 等。推測〉に限られる。それでも裁定「ビルドは切替 PR の中でだけ回す」の下で server を焼ける場所はここだけなので足す。指揮の依頼文は「DedicatedServer を含む」と書くが、Linux の DedicatedServer は `build.yml:65-68` で matrix からコメントアウトされていて dispatch でも焼けない。このレーンは `.github/**` を持たないので matrix は変えず、焼けないことを `done.md`「## 未確認」と PR 本文に書く。同じ run の `Client Build` は配布物にしない
- D-31（独立評価 1 周目〈軽〉: Task 5 の対象 `9f32a03390..HEAD` は、Task 2 の取り込みの後なので master 側の非 UITK の作業を丸ごと含み、自動適用が master の作業者のコードに当たる → 対象を feature 側に絞る: `9f32a03390..$PRE_MERGE_HEAD`・取り込みの後の feature 側の非マージのコミット・取り込みのマージコミットの `git show --remerge-diff`〈衝突の解き方と戻ったファイルの `git rm` だけが出る〉。Task 13 も同じ絞り方。出所 agent 既定）: 別の subagent が再現した（`9f32a03390` は master の祖先でなく、模擬の取り込みで差分が 10 から 219 ファイルに増え、非 UITK の .cs 32 本と master 側の moores-code-review の変更が入る）。棄却: 取り込みの前に Task 5 を回す（衝突の解き方と I-follow の写しがレビューを通らない）
- D-32（独立評価 1 周目〈軽〉: `promotion.md` の Mac の欄を埋めると反映の前提がそろって見え、別のセッションがマージ前の切替ビルドを playtest へ出しうる → `promotion.md` の先頭に「反映禁止（切替 PR 未マージ。反映と行の削除は X-release）」の 1 行を足し、Task 10 Step 5 の後も残す。出所 agent 既定）: 別の subagent の再現では「一部」: `promote_playtest.py` は `promotion.md` を読まず `--mac-checked` は申告だけなので、欄を埋めても前提はそろわず、この行は機械を止めない。危険は「playtest に反映して」と頼まれた別のセッションが最新の run を選ぶ経路で、`release-playtest.sh` の `promotion.md` は branch を書かないので、この 1 行が人とエージェントへの唯一の手がかりになる。X-release が同じ run を反映に使うので「X-release が外す」と添える。機械で止める改修（`promote_playtest.py` が release.log の `branch=` を見て master 以外を拒む）はこのレーンの持ち物の外（`.agents/skills`）なので「## 本PR外のリファクタ提案」に置く
- D-33（独立評価 1 周目〈2 体とも軽〉: 撤去 5a〜5e はどれも W6-6 で見た目の不変を証拠にしたのに、このレーンだけが `Client.Ui/**` に触れうる経路〈取り込みの衝突の解決・I-follow の写し・レビューの自動適用〉の後に見た目の比べ直しを持たない → Task 1 Step 12 でこのレーンの起点の W6-6 (a)(b) を撮り、凍結の前は Task 6 Step 1b、凍結の後は D-9 の `Client.Ui` の系統の行で撮り直して比べる〈独立評価 4 周目で改めた: 3 つの経路はどれも最初の凍結の前に起きるのに、撮り直しを D-9 にしか繋いでいなかった。D-56〉。出所 agent 既定）: ラダーは USS の値やトークンの書き換えでは落ちないので、比べ直しが無いとユーザーの目視の前に Web との差が入っても気づけない。I-follow の写しは意図して見た目を変えるので、写した画面の組は I-follow の比較 HTML〈Task 2 Step 4〉を証拠にし、それ以外の組の不変をここで見る
- D-34（独立評価 1 周目: IME の一巡の手順が実物と合わない 3 点 → 直す。出所 agent 既定）: (1) BP 名の `modal-input` は入力欄の ESC でフォーカスが外れるのを見て同じ 1 回でモーダルを取り消す〈`ModalView.cs:71-73,110-114`〉ので、「ESC の後も画面は開いたまま」は仕様どおりでも必ず外れる → 欄ごとに Expected を分け、BP 名は「ESC でモーダルが閉じる」を合格にする。Mac の「もう 1 回 Return」は `composition=[]` でないときだけにする（確定済みに 2 回目を送ると BP 名の確定で閉じる）(2) 駅は 7×1×20 で、カメラの前に置くだけでは F で開かない。Mac の鍵の一覧に F が無かった → 前例 `OpenStation`〈`uitk-train-inventory.cs:171-185`〉を遠隔実行と OS の鍵で写し、F〈key code 3〉と左 Option〈58〉・Windows の左 Alt を足す。開けなかった欄は「開けない（手順の失敗）」として IME の合否と分け、`GameTextField` の持ち主へ回さない (3) S9〈`s9-ime-win-report.md:11`〉と統合計画 §2.2 の段階 1 の完了条件〈`plan.md:217`〉・§4.9 の S9〈`plan.md:692`〉が見た「変換中の取消」と「フォーカスの往復の後の再入力」が一巡に無かった → Step 3 の 4 と 7 に 1 手ずつ足す
- D-35（独立評価 1 周目: 前の段からこのレーンへの申し送りを拾う手順が無い〈Task 1 は `done.md` の 1 行目しか読まない〉。X-5d の server の配布ビルド・X-5c の `StreamingAssets/WebUi`・X-5b の誤検知と `Plugins/x86_64`・X-5a の manifest の版の付け直し〈X-5a 計画 D-28〉が落ちていた → Task 1 Step 11 で各 `done.md` の「申し送り」「integrator への依頼」「未確認」を読んで全部を受けるタスクに割り当て、起草時に見込んだ項を「## 申し送りの受け」の表に置く〈R-22〉。出所 agent 既定〈指揮の補足〉）: 受け先の無い項 0 を Task 1 で確かめる。X-5a 計画 D-28 の版の付け直しは C-2 の読み替えと Task 2 Step 2 の取り込みの手順に入れた
- D-36（独立評価 1 周目〈軽〉: R-18・Task 14 Step 3 の目視の前に、切替の後に戻す先〈今 playtest に出ているビルド〉が記録されず、PR から辿れない → Task 12 Step 3 で `find_live_playtest.py` を流し、BuildID・run・コミットを `done.md` と PR 本文に書く。出所 agent 既定）: 裁定の帰結「切替の後に不具合が出たら、切替前のビルドへ戻すしかない」の戻す先を、マージの前に固定しておく
- D-37（独立評価 1 周目〈軽〉: Task 7 Step 3 の `<X-prerun の HEAD>` を決める手順が無い → Task 1 Step 6 で X-prerun の全録画を流した HEAD の 40 桁 SHA を `<PRERUN_HEAD>` として写す。出所 agent 既定）
- D-38（独立評価 2 周目〈重〉: PR を作ってからユーザーがマージするまで〈目視で数日かかりうる〉に master が撤去済みの場所〈`Client.WebUiHost/`・`moorestech_web/` 等〉へ新しいファイルを足すと、衝突にならず PR は MERGEABLE のまま、G1・G4 は base の更新では走り直さず〈`uitk-web-follow-gate.yml:5` の `pull_request: {}`〉、master の保護に必須の check も無いので、緑のまま古い結果でマージでき、asmdef の無い .cs が master に戻るか Web の変更が UITK に写らないまま切り替わる。裁定「最終検証中に master へ Web の更新が入ったら取り込んで追従し、影響箇所を再検証してから切り替える」の「切り替える前」はユーザーのマージの直前まで → PR 本文の「マージの方法」と目視の手順の 4.・`done.md`「ユーザーの残タスク」「integrator への依頼」に、マージの直前に指揮が `origin/feature/uitk..origin/master -- <R-LIST>` を確かめ、空なら空のコミットで G1・G4 を走り直させて緑を見てから「マージしてよい」と返し、空でなければこのレーンを Task 2 から起こし直す手順を書く。出所 agent 既定〈指揮の補足: 評価の直し方どおり〉）: 別の opus の subagent が再現した（scratch の repo で消したディレクトリへの新規ファイルが衝突なしにマージされて残る・`branches/master/protection` と ruleset に必須の check が無い・計画に PR 作成後の確認が無い。master のコンパイルが壊れる段は推測。`check/x-switch-pr-repro2.md`）。マージはユーザーが行うので、計画にマージの手順を足さず、確認を指揮の持ち物にした。棄却: G1・G4 を必須の check にする（master の保護の設定はこのレーンの外。ユーザーの判断）／PR を作った後もこのレーンが常駐して見張る（目視の日数だけセッションが残る）
- D-39（独立評価 2 周目〈重〉: 駅名の欄を開く順が「照準 → 左 Alt → F」で、左 Alt の押下が `ApplyZonePolicy(CursorCenterWarp.ToScreenCenter)`〈`UiStateCameraPolicyService.cs:95-101`・`PlayerCameraInteractionApplier.cs:21`〉でカーソルを画面中央へ戻すので、置いた照準が消えて F で駅が開かず、両 OS で「開けない」に流れて IME の駅名が一度も自動で確かめられない → 前例 `OpenStation`〈`uitk-train-inventory.cs:171-185`〉の順「左 Alt を押す → 2 フレーム → 照準 → F → 左 Alt を離す」に合わせ、鍵送りで Alt の KEYDOWN と KEYUP を遠隔実行の前後の別の呼び出しに分ける。F の前に `AimPointProvider.GetEffectiveAimSource()`〈`AimPointProvider.cs:37-44`〉が `Cursor` かを見て、`ScreenCenter` なら V で三人称へ戻してからやり直す（一人称は Alt を受け付けない `:91-93`）。出所 agent 既定〈指揮の補足: 前例の順に合わせる〉）: 手順の誤りが「開けない欄は不合格に数えない」に隠れるのを防ぐ
- D-40（独立評価 2 周目〈2 体とも軽〉: `$LABEL`・`$FROZEN`・`$PRE_MERGE_HEAD`・`$RUNS` と R-LIST を Bash の呼び出しをまたいで使うのに残す手が無く〈Task 8 Step 2 は `LABEL` を代入していなかった〉、R-LIST を使うブロックに読む行が無い。zsh では空の配列が引数 0 個になり、`git ls-files --` が全ファイルを出して `restored.txt` に入る → Global Constraints 11 に `vars.sh` を置き、R-LIST を使う全ブロックと `$LABEL`・`$FROZEN` を使うブロックの頭で `vars.sh`・`rlist.sh` を読む。`rlist.sh` は空なら exit 1。出所 agent 既定）
- D-41（X-prerun の直し役からの依頼 3: X-prerun の UITK の分類に `PASS（揺れ: 1 回目 FAIL）` が入る → このレーンの全録画も exit 0 以外の行を 1 回流し直し、2 回とも落ちた行だけを分類と印にかける。X-prerun の揺れの行は PASS と同じに扱う。出所 agent 既定）: 比べる基準とこちらで揺れの扱いが違うと、揺れで 1 回落ちた行が `退行` に数えられる
- D-42（X-prerun の直し役からの依頼 1・2: `<PRERUN_HEAD>` は X-prerun の `done.md`「## 全録画の結果」の頭の `全録画の HEAD（GameUi）:` の行から取り、`gh pr view` はその行が無いときだけにする。X-prerun が「master の未追従」として申し送るコミットと撮影 id〈X-prerun D-37〉を Task 2 Step 4 の写す候補に入れる → Task 1 Step 6・Step 11・Task 2 Step 4・申し送りの表を直した。出所 agent 既定）
- D-43（独立評価 2 周目〈軽〉: Task 5 の範囲 (1) `9f32a03390..$PRE_MERGE_HEAD` は、I-weekly や各レーンの取り込みで master が `feature/uitk` に入っていれば master 側の非マージのコミットも含み、D-31 が避けた「自動適用が master の作業者のコードに当たる」が残る → (1) を `git rev-list --no-merges 9f32a03390..$PRE_MERGE_HEAD --not origin/master` にし、その範囲の取り込みのマージには `--remerge-diff` を添える。出所 agent 既定）: 起草時の `origin/feature/uitk` では範囲の 3 コミットがどちらでも同じ（取り込みのマージは 0）で、差は今後の取り込みで出る
- D-44（X-5a の直し役からの依頼 1: X-5a の `list-recordings.sh` は scenarios 配下の全 `.cs` を出すので、`run-scenario.sh` では流せない 3 本が全録画に入る。`playtest-receiver/developer-report-interrupt.cs`・`developer-report-resume.cs` は `PlaytestRunner.Run` を呼ばず run dir でない文字列を返す EDC の手動投入のスニペットで、`run-scenario.sh:140-145` が必ず exit 1 にし〈run dir も `result.json` も出ない〉、ヘッダに流し直しの手順も無いので、Task 7 Step 3 の「`前提つき` はヘッダの手順で流し直す」に当てはまらない。`uitk/stage5/uitk-event-language-gate.cs` は運転役 `.sh` の武装〈`PlaytestEventLanguageGateBoot.ArmForNextBoot()`〉が無いと出展モードにならず、ゲートを待って落ちる → 3 本を Task 7 Step 2 で一覧から外して `list-run.txt` にし、Step 3b で interrupt は X-5a 計画 Task 8 Step 7 の手動投入、言語ゲートは運転役で判定し、resume は判定の対象外にする。R-9 に 3 本の扱いを書いた。出所 agent 既定〈X-5a の依頼。2 本の実物〈`origin/feature/uitk`〉・`run-scenario.sh`・X-5a 計画 Task 8 Step 7・S5-gate 計画の運転役で確かめた〉）: X-5a R-16 のように一覧に残して exit を読み捨てる形にしないのは、interrupt を `run-scenario.sh` で流すと本物の報告の箱を実 outbox に作ったまま後始末されず〈トークンなしは例外で止まり、トークンありは送信の途中で PlayMode を止める〉、揺れの流し直しでもう 1 箱増えるため。resume を対象外にするのは、受け口を外した前置きでは本番送信が再開せず、作者の機材にしか無い進捗の箱〈`developer-report-resume.cs:15-16`〉を読むので、どの機材でも通らないため（X-5a も判定しない）。棄却: resume のために受け口を本番へ戻す（Editor の録画から本番へ送らない Global Constraints 5 に反する）
- D-45（X-5a の直し役からの依頼 2: Task 1 の Interfaces と Task 7 Step 2 は `list-recordings.sh` が標準エラーに外した名前と理由を出す〈S4-gamma 計画 `docs/superpowers/plans/2026-11-10-uitk-s4-gamma.md` の系の引数つきの版〉と読んでいたが、X-5a 計画 Task 8 Step 6 の全文は引数なしで `find "$root" -name '*.cs' | sort` を出すだけで、台帳を読まず標準エラーに何も出さない → `list-excluded.txt` は空を期待にし、外した名前の記録はこのレーンの `list.txt` と `list-run.txt` の差〈D-44 の 3 本〉だけにした。空でなければスクリプトが期待の形と違うので、中身を `done.md` に写して D-15 に書く。出所 agent 既定〈X-5a の依頼。X-5a 計画 Task 8 Step 6 と S4-gamma 計画の版で確かめた〉）
- D-46（独立評価 3 周目〈重〉: master の取り込みの衝突の解き方が片側削除・C#・`localization.csv`〈と asmdef・tss〉にしか無く、5b・5c が書き換え master も触る `manifest.json`・`packages-lock.json`・`MainGameUI.prefab`・`run_test.yml` が両側変更になると、prefab は AGENTS.md のテキスト編集の禁止に当たり、manifest・lock を「両方の行を採る」で解くと `jp.juha.cefunity` が戻って C-3 が外れ、持ち先も無い → Task 2 Step 2 に C# 以外の両側変更の決まりを足す: I-follow の前例〈C2-b 計画「両方の意図を保って解き、解いたら compile とテスト」〉に合わせて opus の subagent に「両方の意図を残す・撤去した物を戻さない」で解かせ、prefab・シーンは feature 側を採って master 側の差を Editor へ uloop で当て直し、manifest は feature 側に master 側の足し・上げだけを足し、lock は feature 側を採って Package Manager に解き直させ、workflow・shell は撤去した job・段・検査を戻さない。解いた後に compile・G1・G4・C-1〜C-9・関係するテストを流し、解けないものは停止して `done.md` へ書き指揮へ返す。取り込みの後に C-x が外れたら終わった撤去レーンへ回さず、このレーンが解き直す。出所 agent 既定〈指揮の補足: 前例の I-follow の写し方に合わせる〉）: 別の opus の subagent が再現した（5b・5c の計画がその 4 ファイルを変える行・`origin/master` の直近 90 日の変更 20・11・6・4 回・scratch の repo で manifest と lock の隣の行を変えると `UU` になり、両方の行を採ると JSON として正しいまま cefunity が戻り C-3 が 1。merge-base〈11-06〉の後に master はこの 4 ファイルを触っていない。「終わったレーンへ回して止まり続ける」は推測で、言えるのは取り込みの後の C-x の外れに持ち先が書かれていないまで。`check/x-switch-pr-repro3.md`）。棄却: master 側を採って撤去をやり直す（5b の uloop の撤去を Editor で再現することになり、撤去の検査も 5b の持ち物をこのレーンでやり直す）
- D-47（独立評価 3 周目〈重〉: Task 10 は moores-playtest-release の手順 4 の前提「Mac mini に moorestech がインストール済みでベータ playtest-staging を選んである」〈`SKILL.md:67`〉に頼るのに Task 1 が確かめず、今の Mac mini は `appmanifest_1958160.acf` が無く `steamapps/common/` が空〈起草時に実測〉。約 70 分の配布ビルドと検証機の後の 30 分の待ちで初めて気づき、切れたときの分岐も無い → Task 1 Step 8b で acf と `BetaKey` を確かめ、無ければその場で `done.md` の下書きと途中報告でユーザーにインストールとベータの選択を頼み、Task 2〜9 を先に進めて Task 10 の頭で確かめ直す。待ちが切れたら acf・`BetaKey`・`buildid` を記録し、前提の欠けか更新されないままなら Mac の確認〈R-14・R-11 の Mac・R-15 の Mac の 4 行〉を `未確認` にしてユーザーの目視の手順へ回し、停止しない。出所 agent 既定〈指揮の補足〉）: インストールとベータの選択はパスワードと GUI の操作が要り、このレーンが代われない。止めずに未確認で回すのは、Windows の検証と PR の準備を Mac の待ちに縛らないため
- D-48（独立評価 3 周目〈2 体〉: マージ直前の確認が G1・G4 の pass だけを見るので、ユーザーの目視の間に master へ入った Web 以外の API の変更〈R-LIST に当たらず G1・G4 も緑〉で merge ref の UITK 側がコンパイルできなくても『マージしてよい』が出て、必須の check も無いのでそのままマージできる → 空のコミットで走り直した Platform Compile Check の pass と、Unity Test の失敗が既知の集合〈Task 14 Step 4 で作る `done.md`「## CI の既知の失敗」〉に収まり `error CS` が 0 であることを合格の条件に足し、run が全部終わるまで `timeout 580 gh run watch` で待ってから返す。R-16・R-17・Task 14 Step 2 の 7.・Step 3 の 4.・Step 5 の integrator への依頼をそろえた。出所 agent 既定〈指揮の補足〉）: 別の opus の subagent が再現した（手順は G1・G4 しか見ない・master の `required_status_checks` が空で ruleset は deletion・copilot_code_review・non_fast_forward だけ・UITK 側は master にも在る `UIStateEnum.SubInventory`・`BlockInfoPoller` 等に依存し、合成した例で衝突なしにマージされて古い名前の参照が残った。merge ref が実際にコンパイルエラーになる段は推測）。再現役の訂正を採った: Platform Compile Check は `projectPath: moorestech_server/` で server だけをコンパイルするので、UITK 側の破れを拾うのは Unity Test だけ。そのため Unity Test を「既知の内」で見て `error CS` 0 を足した。評価の案の「`-- moorestech_client moorestech_server` も見て compile と `^Client\.Tests\.Ui\.` を流す」は、同じ merge ref で走る Unity Test が覆うので足さない
- D-49（独立評価 3 周目〈軽〉: 取り込みをやり直す 2 回目以降の経路が 1 回目の書き方のままで通らない〈`switch_ledger.py` は切替済みの束で exit 1・全束が切替済みの後の取り込みでは G1 が新しい master のコミットを G1-1 の違反として出すので「違反なし」を満たせない・D-9 の表がやり直しを「Task 3 Step 5・Task 4」にしていた〉→ Task 2 の前文に「2 回目以降の取り込み」の流れを書き、G1 は違反が全部 G1-1 で SHA が今回の取り込みのコミットだけを合格にし、Task 3 は Step 1 と Step 2 のコミットを飛ばして G1 の 1 行から Step 3〜6 を行う。D-9 の取り込みの 2 行を「Task 2 の 2 回目以降の流れ〈記帳は Task 2 Step 5 と Task 3 Step 3〜5〉・Task 4」に直し、Task 14 Step 4 の `CONFLICTING` も同じ流れを引く。`PRE_MERGE_HEAD` は無いときだけ書く形にした（`. vars.sh` は後の行が勝つ）。出所 agent 既定〈指揮の補足: 束がすでに切替済みのときの扱いを含めて通す〉）
- D-50（独立評価 3 周目〈軽〉: レビューを feature 側に絞る指示は moores-code-review の本体の patch にしか効かず、Step 7.5 の bug-pass は「Step 1 と同じ base で末尾を最終 HEAD にした PR 全体 diff」を `review_diff.py` で作り直して Critical を自動で当てる〈`SKILL.md:146`〉。base に `9f32a03390` を渡すと bug-pass に master 側の作業が入る。`--remerge-diff` は `git diff` を包むだけの `review_diff.py` では作れない。Task 13 の範囲の始まりの HEAD も残らない → Task 5 Step 1 に `review_patch.py`〈first-parent の鎖を取り込みのマージで区切り、区間は `review_diff.py` と同じ除外の `git diff`、取り込みは同じ除外の `--remerge-diff`〉を置き、本体と bug-pass の patch を両方これで作り、`--base-ref` は渡さない〈未指定なら final.diff は patch.diff と未コミット分の連結。`40_apply.js:22`〉。`REVIEW1_HEAD` を `vars.sh` と `done.md` に書き、Task 13 はそこから同じスクリプトで作る。出所 agent 既定）: 起草時の模擬（`origin/feature/uitk` に `origin/master` を取り込んだコミットを scratch の clone で作った）で `9f32a03390..HEAD` の diff 219 ファイルに対し patch は 10 ファイル、取り込みの無い今の範囲では `review_diff.py` の出力とバイト単位で同じだった。棄却: `--base-ref` に `PRE_MERGE_HEAD` を渡す（final.diff に取り込みの master の差が入る）
- D-51（X-prerun の直し役からの依頼: 全録画の一覧と TSV を `moorestech_client/Temp/uitk-recordings/` に置くと、Unity は Editor を正常に閉じると `Temp/` を消すので〈X-prerun D-43 の観測〉、Task 8 で Editor を閉じた後や停止からの再開で一覧と TSV を失う → 置き場を `moorestech_client/.uloop/outputs/X-switch-pr/uitk-recordings/`〈`.gitignore:102` で無視。`git check-ignore` で確かめた〉へ移した。出所 agent 既定〈X-prerun の依頼〉）
- D-52（独立評価 4 周目〈軽〉: `review_patch.py` の `is_intake` は「2 番目の親が `origin/master` の祖先」だけで取り込みを見分けるので、I-weekly の PR〈2 番目の親は master を取り込んだ後に台帳を積んだ枝の先端で、master の祖先ではない。実行計画 §1.6〉を取り込みと見なさず、区間の `git diff` に master の非 UITK のコードが丸ごと入り、本体と bug-pass の自動適用が master の作業者のコードに当たる → 「2 番目の親の側に 1 番目の親に無い master のコミットがある」マージを区切りにし、その差は「1 番目の親と運ばれた master の最新のコミットの自動マージ〈`git merge-tree --write-tree`。衝突の印を含む木〉とマージの結果」の `git diff` で作る。Task 5 Step 1 の Expected に、別の数え方〈`comm -12` で 2 番目の側と `origin/master` の共通を数える〉と同じ数でなければ patch を使わず停止を足した。出所 agent 既定）: 実測（scratch の clone）: I-weekly-1 の `5e168777b0`〈#1603〉で改める前の版は `intake_merges=0`・43 ファイル〈master の車両撤去の `.cs` などを含む〉、今の版は `intake_merges=1`・26 ファイル〈枝の台帳・文書のコミット 2 件と、枝の中の取り込み `d03e06663b` の衝突の解き方だけ。自動マージは衝突あり〈`merge-tree` の exit 1〉で、その形でも動いた〉。直接の取り込みの模擬は改める前の `--remerge-diff` の版とバイト単位で同じ、取り込みの無い今の範囲は `review_diff.py` とバイト単位で同じ。棄却: 評価の案の「側の非マージのコミットを `git show` で足し、マージ自身は `--remerge-diff`」（枝の中の取り込みの衝突の解き方が抜け、コミットごとの差は HEAD の行番号と合わない）。あわせて「## 未確認」の「レーンのブランチが自分の中で master を取り込んだ形は見分けない」も同じ判定で解けた
- D-53（独立評価 4 周目〈重〉: マージ直前の確認から「Task 2 から起こし直す」とき、前のセッションの worktree は畳まれ、`$SP` の `vars.sh`・`rlist.sh`・既知の集合・X-5d の名前の一覧が無い。無いファイルを `.` で読んでも bash・zsh とも続行し〈実測〉、`cd ""` は今の場所に留まり、`grep -F -f <空>` は 0 件で素通りする → Task 2 の前文に「起こし直し」〈指揮が §4.1 の形で worktree を作って起こす → Task 1 Step 1 → 値を `done.md` の決まった節から写す〈`PRE_MERGE_HEAD` は Task 2 Step 1 の前に〉→ 既知の集合・物差しの状態の一覧・`rlist.sh`〈「## 取り込み」の最後の R-LIST の 1 行〉を作り直す → Task 1 Step 11 の 2 つ目のブロック・Step 7・8・8b・9 → Task 2〉を置き、R-16・Task 14 Step 2 の 7.・Step 5 の integrator への依頼から指す。`vars.sh`・`rlist.sh` を読む全部の行を `|| exit 1` にし、X-5d の名前の一覧を使う 3 か所の前に `test -s` を置いた。出所 agent 既定）: 別の opus の subagent が再現した（`.` の続行・`cd ""`・hook が merge を止めない・空の一覧の 0 件・値はほぼ `done.md` にある。メインクローンで merge が走るのは指揮が §4.1 を踏まずに起こしたときだけで推測の段。`check/x-switch-pr-repro4.md`）。再現役の訂正を採った: worktree をレーンが自分で作る形は §4.1〈指揮が作って `-C` で起こす〉と食い違うので、指揮が作る形にした。`PRE_MERGE_HEAD` を `done.md` から先に写すのは、写さずに Task 2 Step 1 へ進むと `grep -q` が今の HEAD を最初の値として書くため〈再現役の追加の筋〉。`LABEL` は R-10 の行の `label=` から取る
- D-54（独立評価 4 周目〈軽 3 件〉: 取り込みの後の扱いの抜け → (1) 取り込みの後の compile エラーは、消した型への参照〈master が残るファイルに足した登録の行が衝突せずに入る。`0e81bbe3c3..origin/master` でも `MainGameModelRegistration.cs` を触っている〉なら I-follow としてこのレーンが外すか付け替え、それ以外は C# の衝突と同じく opus の subagent が直し、D-6 に書く (2) 外部 repo のピン `.moorestech-external-revisions.json` の衝突は、祖先の関係なら新しい方、分岐していればその repo で両方を含むマージを作って push・PR してピンする〈AGENTS.md「関連リポジトリ」。`moorestech_master`・`moorestech_client_private` の 2 項目〉(3) D-9 の取り込みの 2・3 行目は、`git merge origin/master` が Web 以外の master のコミットも連れてくるので、`git diff --name-only "$FROZEN" HEAD` が台帳・文書・R-LIST の削除だけのときに限り、それ以外は変わったパスで下の行を引く。出所 agent 既定）: 取り込みの手順の中の直しで、このレーンが持つ I-weekly の役の内（他のレーンの分担を変えない）なので再現役は立てていない
- D-55（X-prerun の直し役からの依頼〈4 周目〉と独立評価 4 周目〈軽〉: ラダー 6 `uitk-challenge-gate.cs` と並べ比べ `uitk-stage5-compare.cs` は出展モードで、`.cs` を `run-scenario.sh` で直接流すと出展モードにならない〈S5-ladder D-8・R-2〉。撮影シナリオ `uitk/prerun/` は X-prerun が全録画から外した〈X-prerun R-6〉のに、この計画は外しておらず、3 本ずつの束で 1 回の前景の呼び出しが 600 秒を超えうる → ラダー 6 は Task 7 Step 1 で運転役 `exhibition-mode-run.sh` から、`uitk-stage5-compare.cs` は Step 3b で同じ運転役から流し、どちらも `list-run.txt` から外す。運転役の後は前置きを流し直す。`uitk/prerun/` は一覧から外し、物差しは W6-6〈Task 1 Step 12・Task 6 Step 1b〉で流す。束の本数は X-prerun の秒の列から決める。運転役の呼び方〈系の引数の有無〉は X-5a が全体スイッチを消すときに変えうるので、Task 1 Step 4 で usage から写す。出所 agent 既定）: X-prerun 計画 :474-475・:499 と S5-ladder 計画 R-2・:1551・:1760 の形に合わせた
- D-56（独立評価 4 周目〈重〉と X-prerun の直し役からの依頼〈4 周目〉: D-33 が挙げる `Client.Ui` に触れうる 3 経路〈取り込みの衝突の解決・I-follow の写し・レビューの自動適用〉はどれも最初の凍結の前に起きるのに、撮り直しを凍結の後の D-9 にしか繋いでいなかった。物差しは X-prerun D-51 で PNG と `smoke-rects.tsv` の 2 つになり、矩形は全状態で `diff` 一致、PSNR の `inf` は「PSNR で比べる状態」だけに求める形になった → Task 1 Step 12 で TSV も写して `SHOT_BASE` を書き、Task 6 に Step 1b〈`git diff --name-only "$SHOT_BASE" HEAD` が `Client.Ui`・`Client.Ui.State`・`Client.Ui.Editor`・`Assets/UI Toolkit` に当たれば撮り直し、ギャラリーは全組 `inf`・スモークは矩形の `diff` exit 0 と「PSNR で比べる状態」の `inf`〉を足し、D-9 の 5 行目は同じ Step を流し直す形にした。Task 1 Step 6 で X-prerun の「### 物差し（prerun-smoke-shots）」を写し、状態の一覧を `smoke-psnr-states.txt` に書く。出所 agent 既定）: 撮り直しの起点と比べ方を同じレーンの中で直しただけで、設計書・裁定・分担を変えないので再現役は立てていない。tss と見本の USS の置き場は `git ls-tree` で確かめた
- D-57（独立評価 4 周目〈軽〉: I-follow の写しで G4 の語を書くと、Task 4 Step 1 の「取り込み・I-follow の違反は I-fix へ回して停止」で、自分の写したファイルの 1 行を直すだけで済むのにレーンが止まる → I-follow の手順 (2) に G4 を足し、このレーンが写したファイルの違反はこのレーンが直す。I-fix へ回すのは master から入ったファイル・許可リストへ足すしかない語・exit 2 だけ。Task 2 Step 3・Task 4 Step 1 をそろえた。出所 agent 既定）: I-fix との分担を変えるので別の opus の subagent に再現させた（一部再現: I-follow の手順に G4 が無く、Task 4 Step 1 は I-follow の違反も I-fix へ回す。今の HEAD の `Client.Ui*` の 48 ファイルに `// web: moorestech_web/...` の出所の注記があり、写す人がその形を書く筋はある〈推測〉。「最初に気づくのは Task 4」は言い過ぎで、Task 2 の終えた後の門に G4 がある。`check/x-switch-pr-repro4.md`）。再現役の指摘どおり、X-5e 計画の「## 申し送りの処遇」と D-39 の I-fix の下書きも同じ分け方にそろえる必要があるので、X-5e への依頼にした（このレーンは他の計画を書かない）
- D-58（独立評価 4 周目〈軽 2 件〉: Mac の Steam が `未確認` でも `done.md` の 1 行目が「状態: 完了」になり、マージ直前の確認も Mac を見ないので、ユーザーが Mac の目視を飛ばしても「マージしてよい」が出る。報告ページの manifest は Mac の 2 枚を無条件に載せ、`publish_pr_report.py` の `validate` が無いファイルで exit 1 になる → 1 行目を「状態: 完了（未確認: Mac の Steam）」、目視の手順の Mac の項目を「完了条件の代わり。済むまでマージしない」、マージ直前の確認の (0) で指揮がユーザーに Mac の目視が済んだかを 1 問聞く、manifest は Mac が `未確認` なら Mac の 2 枚の行を消して verification の Mac の部分と IME の分母を書き換える、Task 11 Step 5 で IME の撮影を manifest と同じ名前で写す、X-release への申し送りの `promotion.md` の Mac の欄を「済み／未確認」で分ける。出所 agent 既定）: 別の opus の subagent が再現した（一部再現: 完了条件に Mac の Steam があり、未確認でも「状態: 完了」・マージ直前の確認に Mac が無い。X-release への申し送りが「Mac の欄は済み」と固定で、未確認のとき `promotion.md` の欄は空のまま〈追加の筋〉。「黙って抜ける」でなく「見落としうる」程度。`check/x-switch-pr-repro4.md`）。指揮は目視を代わらずユーザーに尋ねるだけなので、裁定「目視とマージはユーザー」と矛盾しない
- PLAN-W5 の申し送り（指揮の中継。`check/w5-handoff.md`）: バグ報告の manifest は、S5-crosscut が v7 で `gameUiExceptions` を足したので、X-5a が `webUiHealth` を外す版は v8 になった（X-5a 計画 R-9・P-20・:1397-1399 をコミット `bfbdec60e6` の実物で確かめた）→ 死活表・確かめの表の 5a 行・C-2（`SchemaVersion = 8`）・Task 7 Step 3b の interrupt の検査（3 つ。不合格文に `gameUiExceptions is not unknown …`）・W6-12 を v8 にそろえた。`gameUiExceptions` の欄は残る。出所 agent 既定
- 独立評価 2 周目のその他（軽。手順・数の直しで設計を変えない。出所 agent 既定）: X-5d の Windows の表は W-C6 を足した 19 本・名前の一覧は client 61 名・server 17 名（X-5d D-26・D-28。取り出しは X-5d の依頼の awk で、行数が見出しと同じで空でないことを確かめてから使う）／R-LIST に X-5e が移した `docs/webui-parity`・`docs/webui-feature-videos-2026-06-14` と消した `.claude/rules/ref-webui-design.md` を足し、外すパスの指定 `docs/webui/archive/` を消した（戻った文書は `docs/archive/` へ `git mv`）／Task 9〜11 の頭に R-16 の確認／`winunity play` は受け口の値を立てて Editor を起こす（`interactive.ps1:30-31,68-81`）ので、言語の差だけを Mac で流し直す形にした／衝突の列挙を未マージの 7 種に／Task 1 Step 4 の glob をやめた／C-3 に prefab の `--- !u!222`・`223`・`225` を足した（X-5b R-1。§5.1-1 の「Canvas が無い」の証拠）／D-6 の古い句を D-29 に合わせた
- 独立評価 4 周目のその他（軽。手順・数・文面の直しで設計を変えない。出所 agent 既定）: マージ直前の確認に (0) `gh pr view --json mergeable` を足した（5a〜5d が消した個別のファイルを master が直すと modify/delete の衝突になり R-LIST には出ず、衝突のある PR では `pull_request` の workflow が走らないので待つ run が現れない。`CONFLICTING` なら「衝突あり。マージしない」で起こし直し、`UNKNOWN` は取り直す）／R-10 の `branch` を配布物の実物で見る（Task 8 Step 6 の PowerShell に `branch=`、Task 10 Step 2 の python で `commit` と `branch`。焼き込みは `BuildInfoComposer.cs:52`）／Windows で流した全録画の行が表から落ちないよう、`all-a177.tsv` と `rerun.txt` の awk に `all-win.tsv` も渡し、`all-win.tsv` は見出しだけで先に作る／Task 6 Step 3 の Expected を「exit 0（最後の行は 4 本が `PASS: …`、`test-remote-exec-offline.sh` は `OK`）」に（実物の末尾 `echo OK` を読んだ）／Task 3 Step 3 の見込みを「束 1〜3 は記帳済み〈台帳の「## 追従記録」に 5 件とも束ごとにある。今の G1 は束 1〜3 とも未記帳 0 件〉。数えるのは撤去・束 4・5 の暗転の後・取り込みの分」に（G1 と台帳を実測）
- 独立評価 4 周目で退けた指摘: 無し（評価 1 の重 2・軽 9 と評価 2 の軽 4 を、実物と再現で確かめて直した。評価 2 の I-weekly の直し方は、枝の中の取り込みの衝突の解き方も拾う自動マージとの差に替えた〈D-52〉。評価 1 の起こし直しの直し方は、worktree を指揮が作る形に替えた〈D-53。再現役の訂正〉）
- 独立評価 3 周目のその他（軽。手順・数の直しで設計を変えない。出所 agent 既定）: Windows の表の W-E5 を X-5d と同じく最初から W-E5a・W-E5b に割った（流すのは 20 本。X-5d D-37）／ラダーの mp4 の写し先の名前を manifest の `ladder-1`〜`ladder-5`（段階 4 は `4a`・`4b`）にそろえた／Task 12 Step 3 の頭で `vars.sh` を読む／X-5d の依頼 2 点: Task 1 Step 11 の awk を `${s}（` にした（`$s（` は /bin/bash 3.2 で壊れる。X-5d D-38）、Task 1 Step 6 の X-5d の失敗の名前の読み先を `## Windows の 19 本の割り方` の下の `### 撤去後の失敗の名前（N 件）` の text のブロックにし、X-5d 計画 Task 7 Step 5 と同じ awk で取り出して行数を見出しの N と確かめる
- 独立評価 3 周目で退けた指摘: 無し（2 体の重 3・軽 8〈重複 2 組〉と X-5d の依頼 2 件・X-prerun の依頼 1 件を、実物と再現で確かめて直した。評価 2 の B の直し方のうち「client・server の差分も見て compile と `^Client\.Tests\.Ui\.` を流す」だけは、同じ merge ref で走る Unity Test が覆うので足さなかった〈D-48〉）
- 独立評価 2 周目で退けた指摘: 無し（2 体の 9 件〈重複 1 組〉と X-5d・X-prerun からの依頼 6 件を全部、実物で確かめて直した）
- 独立評価 1 周目で退けた指摘: 無し（全部を実物で確かめて直した。再現役が「一部」とした D-30・D-32 は、成り立つ部分〈server のプレイヤービルドの不足・人とエージェントへの印〉に合わせて直し方を絞った）
- 要裁定（done.md へ上げる）: 起草時点で 0 件（Phase 2.6 の強 3 件・弱 2 件と検査の指摘は、どれもゴール・裁定から外れない読みで agent 既定に決めた）。実行時に独立評価・moores-code-review・目視の準備で出たゴール・裁定から外れる判断を `done.md`「要裁定」に足す
- unityプレイ録画テスト: 含む（Task 7）

## 未確認

- G4 のスクリプトの名前・引数・workflow の job 名（X-5e が作る。Task 1 Step 4 で写す）
- 全録画のスクリプトの X-5a の後の引数（Task 1 Step 4）
- 言語ゲートの運転役 `uitk-event-language-gate.sh` の引数の形と、X-5a が書き換えた後の `developer-report-interrupt.cs` の検査の文面（S5-gate・X-5a が作る・書き換える。Task 1 Step 4 で写す。D-44）
- `test_uitk_web_follow_real_ledger.py` が `moorestech_web/webui/src/` の無い HEAD で通るか（5c.md の推測。Task 3 Step 5 で実測）
- release.log に出る 5b の CEF 0 の検査の行の文面（5b が書く。Task 8 Step 2 で読んで `done.md` に写す）
- phase2 の `result.json` で録画（ffmpeg）の成否を示す項目名（Task 8 Step 5 で読む）
- Mac の Steam から `--remoteExec` を渡す形（`steam://run/1958160//--remoteExec/` で効くか。Task 11 Step 3-1）
- `NavigationSubmitEvent` で `Button.clicked` が呼ばれるか・`BlueprintNameInputState.Open()` だけで BP 名のモーダルが出るか（D-16）
- 検証機と winunity が同じ PC か（Task 1 Step 7）
- 出展モードを付けて起動する操作（直接起動で環境変数が効くか、ユーザーの環境変数と Steam の起動し直しが要るか）と、出展モードの起動で遠隔実行の待受が開くか（Task 9 Step 5。D-25）
- 検証機の `Player-prev.log` に phase1 の分が入っているか（Task 8 Step 5。D-26）
- Windows だけの既知の失敗 14 件（I-fix5 R-19）が切替までに直るか（Task 6 Step 2 で実数を書く。D-23）
- X-5d の 19 本の regex の割り方が、5a〜5e の後のテストの並びでもそのまま重なりなく覆うか（Task 6 Step 2。X-5d の `done.md`「## Windows の 19 本の割り方」に割った本があればその形）
- 遠隔実行と OS の鍵で左 Alt を押したまま照準したとき `GetEffectiveAimSource()` が `Cursor` になるか・Mac の `key down option` が別の osascript の呼び出しをまたいで押されたままか（Task 11 Step 3-2。D-39）
- `gh run rerun` が前の merge ref のまま走るか（推測。Task 14 Step 2 の 7. は空のコミットで走り直させるので結果に依らない）
- 撤去済みの場所の一覧（R-LIST）が 5a〜5e の削除の全件と合っているか・`docs/webui` の行き先（Task 1 Step 11 で 5a〜5e の `done.md` と突き合わせる）
- 駅を開く手（遠隔実行のワープと `Mouse.current.WarpCursorPosition` の照準、OS の左 Alt の長押し＋F）が配布ビルドで効くか・足もとから北へ 8 マスの原点が配布ビルドのワールドで地形に埋まらないか（Task 11 Step 3-2。D-16・D-34）
- server の配布ビルドの所要と、Linux の DedicatedServer が焼けないことの扱い（Task 8 Step 8。D-30）
- Unity Test の成果物 `Test results - *` の xml が NUnit 3 の `test-case`（`fullname`・`result="Failed"`）の形か（推測。Task 14 Step 4 の最初の取り出しで、落ちた shard の数と名前の有無を突き合わせて確かめる。形が違えば取り出しの 1 行を直して `done.md` と integrator への依頼に写す。D-48）
- `packages-lock.json` を `git checkout --ours` で採った後、`uloop compile` の前の更新で Package Manager が master 側で足したパッケージを lock に解き直すか（推測。Task 2 Step 2 で衝突したときに `git diff` で確かめる。D-46）
- `review_patch.py` の自動マージ（`git merge-tree --write-tree`）が、W6 の間に入る I-weekly・取り込みの実際の形で衝突の印の多い木になったとき、patch がレビューに読める大きさか（I-weekly-1 の模擬では 26 ファイル。Task 5 Step 1 の 2 つの数え方が合わなければ停止。D-52）
- `exhibition-mode-run.sh` の X-5a の後の呼び方（系の引数を外すか。X-prerun の直し役から X-5a への依頼の結果。Task 1 Step 4 で usage から写す。D-55）
- X-prerun の `done.md`「### 物差し（prerun-smoke-shots）」の小節の書式（2 つの状態の一覧の書き方。Task 1 Step 6 で写す。D-56）
- Mac mini の Steam に moorestech とベータ playtest-staging が入るか（起草時は acf 無し。Task 1 Step 8b でユーザーに頼む。D-47）

## 本PR外のリファクタ提案

- `promote_playtest.py`（`.agents/skills/moores-playtest-release/scripts/`）が release.log の `branch=` を読み、master 以外のビルドの反映を拒む（D-32 の印を機械の止めにする。今は `promotion.md` を読まず `--mac-checked` は申告だけ）。`release-playtest.sh` の `promotion.md` に branch を書く。持ち主は moores-playtest-release のスキル（X-release か X-cleanup）
- `build.yml` の Linux の DedicatedServer の matrix の再開（D-30。統合計画 §5.2 の Linux の判断と一緒に）

## 設計書・実行計画へ反映する行

- 設計書 §7.1:
  `| A-259 | 切替 PR の検証の読み: 「CEF の存在しないビルドでラダー 6 本と全録画」は切替 PR の HEAD（CEF パッケージの無いプロジェクト）の Editor で流す（DSL は Editor 専用）。配布ビルドでは Windows 検証機の phase1/phase2・遠隔実行の 3 点・release-artifact.sh の CEF 0・Steam から入った実物の CEF 0・Mac の Steam の起動・IME の一巡（遠隔実行で画面を開きフォーカスし値を読み、OS 段の合成キーで打つ）を行う。release-playtest.sh は凍結した HEAD の worktree の scripts/playtest から MOORESTECH_BUILD_BRANCH=feature/uitk で流す（master の版は CEF helper を要求する）。レーンのコミットは feature/uitk へ直接積み、切替 PR の G1 が検査する | 配布ビルドで DSL を動かす仕組みを作る（撤去の直前に新しい配布物の口を足し、配布物にテスト専用の経路が入る）／メインクローンの master の release-playtest.sh で焼く（Mac の CEF helper の検査で必ず落ちる）／レーン用の中継ブランチから PR を出す（統合計画 §2.6 の中継ブランチ禁止） |`
- 設計書 §7.1 の [A-177] の改め（本文の末尾に足す）: 「切替 PR（撤去の後）は Web 側が流せないので、`system` は `GameUi` で埋め、Web 側の流し直しの代わりに exit 0 以外の行を X-prerun の UITK 側の結果と比べて `既存`／`退行`／`比較不能（新設）`／`比較不能（変更）` の印を付ける（印の名前は X-prerun の Web 側の比べ方と同じ）。`束 5 未移植`・`wwxy`・`Web 専用` は 0 のはずで、出たら不具合として扱う（X-switch-pr D-8）」
- 実行計画 §8 の E-2 の行に注記: 「例外: X-switch-pr は head が `feature/uitk` そのものの切替 PR なので、記帳・取り込みのコミットは `feature/uitk` へ早送りで直接 push する。E-2 の棄却理由（G1 が切替 PR まで走らない）は、切替 PR 自身で G1 が走るので当たらない（X-switch-pr D-3）」
- 実行計画 §6.1 の表に足す行: `| X-switch-pr（`P/2026-11-12-uitk-x-switch-pr.md`。PLAN-W6） | A-259 | LX-218〜219（目視と IME で見つかった直せない差の予約。行と印は I-fix が置く） |`
- 実行計画 §1.5 の X-switch-pr 行: 「pr-independent-review」→「moores-code-review（`9f32a03390` より後の feature 側のコミット。master 側から来たコードは除く。D-31）と独立評価（opus 2 体。W6-1）」。§5 の末尾「切替 PR は pr-independent-review を付ける」と §7 の U-4「独立レビューの裁定」を同じ読みに改める
- 実行計画 §1.5 の X-switch-pr 行の完了条件に足す: 「master の取り込みでは撤去済みの場所に戻ったファイルを列挙して消し、中身は I-follow の手順でこのレーンが写す（停止しない）。server の配布ビルドは `build.yml` の `workflow_dispatch` で 1 回（Linux の DedicatedServer は matrix で無効）。今 playtest に出ている BuildID を戻す先として PR に書く（X-switch-pr D-29・D-30・D-36）」
- 実行計画 §1.5 の X-release 行に足す: 「反映の前に、X-switch-pr が `promotion.md` の先頭に書いた『反映禁止』の行を外す（D-32）」
- 実行計画 §1.5 の X-release 行: 「moores-playtest-release」の後に「（手順 4 の Mac の Steam は X-switch-pr が staging のビルドで済ませている。X-release は手順 3・5 と、反映前の Mac の追加の確認）」を足す
- 統合計画 §2.6「切替 PR の作り方」の「ラダー 6 本と全録画を CEF の存在しないビルドで流す」に「（DSL は Editor 専用なので、CEF パッケージの無い HEAD の Editor で流す。A-259）」を足す。§5.1-4 の観測のしかたも同じ
- moores-playtest-release（スキル本文）への申し送り（X-5e か X-cleanup が持つ。このレーンは書かない）: 「`feature/uitk` の SHA を焼くときは `MOORESTECH_BUILD_BRANCH=feature/uitk` を付け、その SHA の worktree の `scripts/playtest` から流す」は切替 PR の後は不要になるので、スキルには書かない（記録は A-259 だけ）
