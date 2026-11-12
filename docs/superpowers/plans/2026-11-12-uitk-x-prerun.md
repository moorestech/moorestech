# UITK X-prerun（撤去の前の全録画・wwxy の残り 2 本・全画面の参照の撮り直しと並べ比べ・撤去レーンの見た目の物差し）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 段階 5 の締め（S5-ladder）が `feature/uitk` に入った後、撤去（X-5a〜5e）で全体スイッチの Web 側が使えなくなる前に、(1) 全体スイッチの UITK 側と Web 側で全録画を回して分類し、(2) wwxy の残り 2 本を削除・書き直して bd `moorestech-wwxy` を閉じ、(3) master の worktree で全画面の参照を 1280×720 と 2560×1080 で撮り直し、UITK の全画面（参照 manifest の全 id）と並べ比べて opus の評価役 2 系統の独立評価にかけ、再現した差を「直した／LX-210〜217 に記帳／要裁定 Q-n／持ち主へ bd」のどれかに閉じ、(4) 撤去レーンが見た目の不変を確かめる物差し `prerun-smoke-shots.cs` と撤去前の最後の Web の基準（全録画の Web 側 TSV・参照 `web-<sha>`・全束の突き合わせの記録）を残して `feature/uitk` へ取り込む。

**Architecture:** integrator のレーン（`int`。Mac の Editor 1 本＝このレーンの worktree。Web 側の全録画と 2 本目の Editor が要る再走は Windows の `~/bin/winunity`）。本番のコードは触らず、書くのは「検査する側」（`scenarios/uitk/prerun/` の撮影シナリオ 5 本・wwxy の 2 本）と、並べ比べで再現した差を直す USS（`Client.Ui/**/*.uss`）と台帳の自分の節だけ。全録画は S4-gamma が作った `tools/uitk/list-recordings.sh`・`run-all-recordings.sh` を書き換えずに使い、結果の分類は設計書 [A-177] の 6 分類。UITK の撮影は「既存の段の撮影シナリオ（P）／このレーンが新設する撮影シナリオ（N）／EditMode の `UiRuntimePanelCapture`（E。[A-164]）／動画の 3 フレーム（V）」の 4 種で、参照 manifest の全 id に 1 行ずつ撮り方を割り当て、機械で抜けと重複が 0 であることを確かめる。並べ比べの判定は前例 S3-ladder Task 9・S4-gamma Task 13 の形（評価役 2 系統が互いの結果を見ずに判定 → 別の subagent が画素で再現 → 再現したものだけ処遇）。判定の取りまとめ・USS の修正・LX の記帳（台帳の行と印）はこのレーンが行い、USS で直らない差だけを持ち主へ bd で回して指揮へ返す（設計書 §3.3「並べ比べ」の行と [A-179] の「指揮が判定し、印は I-fix が置く」を X-prerun について上書きする。D-16）。

**Tech Stack:** Unity 6 / UI Toolkit（`Client.Ui`）、プレイテスト DSL（`Client.Playtest`・`.agents/skills/unity-playmode-recorded-playtest`）、`Client.Ui.Editor.Capture`（`UiRuntimePanelCapture`・`GameViewResolution`）、Node と pnpm（master の worktree の `moorestech_web/webui/e2e/uitk-reference/capture.ts`・`tools/uitk/test-name-parity.mjs`）、ffmpeg・ffprobe、Python 3（標準ライブラリだけ。`check-uitk-web-follow.py`・対応表の機械検査）、bd、`~/bin/winunity`。

設計の出所: 実行計画 `docs/uitk-migration/execution/execution-plan.md` §1.5 の X-prerun 行（完了条件の原文（c1e1ff2b8b の版）:「全体スイッチの Web 側と UITK 側で全録画を回す（plan §2.6。撤去後は Web 側が使えない）。wwxy の残りを直すか削除して閉じる。master の worktree で全画面の参照を撮り直し（`web-<最新 sha>`）、UITK の全画面と 720p・2560×1080 で並べ比べ、opus の評価役 2 体の独立評価（画像付き。E-24）にかけて差 0 か記帳済み（設計書 §3.3 (c)）。全束のテスト名の突き合わせ差分 0 を撤去前の最後の記録として `disposition.md` に残し（A-251）、撤去レーンの見た目の物差し `scenarios/uitk/prerun/prerun-smoke-shots.cs` を作る」）・§6.1 の X-prerun の割り当て行・§8 E-24・§2.1（DAG `S5L --> PR0[X-prerun] --> X5a`）・§4.3・§5・§6.2、統合計画 `docs/uitk-migration/plan.md` §2.6（「撤去を積み始めたら、全体スイッチの Web 側は使えなくなる。積む前に、全体スイッチの Web 側と UITK 側で全録画を回しておく」）・§4.4（ラダー 6 本）・§4.6・§5.1-4（壊れている 10 本は直すか削除して wwxy を閉じる）、設計書 `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4・[A-164]・[A-177]・[A-179]、`.agents/skills/ref-uitk-design/references/compare-procedure.md`、台帳 `docs/uitk/TODO.md`、`docs/uitk/disposition.md`、裁定 `.decisions/2026-11-06-*.md` の 7 本。実物の全件調査は PLAN-W6 の起草時の `inv/prerun-switch.md`（HEAD `9c83052273`）。

## Requirements

| # | 要件 | 受け入れ基準（観測できる形） |
|---|---|---|
| R-1 | 前提の確認 | Task 1 の照会で、`origin/feature/uitk` に S2-ladder・S3-ladder・S4-gamma・S5-crosscut・S5-ladder のマージコミットがあり、各 `done.md` の 1 行目が「状態: 完了」、ラダー 6 本のシナリオ（`uitk-lifeline`・`uitk-opening-hud`・`uitk-build-bp`・`uitk-production-loop`・`uitk-research-train`・`uitk-challenge-gate`）が実在、`tools/uitk/` か `tools/uitk/recordings/` に `list-recordings.sh`・`run-all-recordings.sh` が実在、S4-gamma の `done.md`「## 全録画の結果」が実在、`Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の表が空、台帳「## 束」の全束が `暗転`、台帳「## 壊れた録画の割り振り」の X-prerun 以外の 8 行の方針の列に結果（`書き直した`・`削除した`・`移植した`）がある（無い行は直すレーンの `done.md` の記録から `X-prerun` の語を含めずに写す。記録も無ければ欠け。D-50）。1 つでも欠ければ `done.md` 1 行目「状態: 停止（前提の欠け: <欠けたもの>）」で止まる |
| R-2 | 全束の突き合わせ差分 0（撤去前の最後の記録・A-251） | 最終 HEAD（Task 16 Step 2 の `origin/feature/uitk` 取り込みの後）で `node tools/uitk/test-name-parity.mjs --bundle all` が exit 0（全束 写す予定 0・未対応 0）。`docs/uitk/disposition.md`「## 突き合わせの記録」に束 1〜5 の 5 行（日付・束・HEAD 10 桁・件数）。件数が Task 13 と違えば 5 行を取り込み後の HEAD で書き直してから push（Task 16 Step 2）。崩れていたら持ち主へ bd を立てて「状態: 停止」 |
| R-3 | wwxy: `tutorial-research-renumber.cs` を削除 | `scenarios/tutorial-research-renumber.cs` が無い（`git ls-files` 0 件）。台帳の行の方針に「削除した（X-prerun）」 |
| R-4 | wwxy: `misc/research-node-state-label.cs` を現行の研究 GUID へ書き直す | 削除済み GUID（837e9697・424be8c1・07d6226c）がファイルに 0 件。ピン先マスタの研究（起草時 e0c913033c: 原始研究1 `858bcb10-b8ba-478e-9bc5-473ca61281a2`・原始研究2 `b47c5e3c-1b58-42c5-a477-d485d2eae747`・原始研究3 `bc5e7786-6759-4271-8095-836703b54490`）で「完了済み／研究可能／研究不可」の 3 状態を作り、GameUi で `Success: true`・ErrorLogs 0・Assert 5/5。Web でも流し、落ちたら原因を分ける: シナリオの誤り（素材・GUID・待ち）なら直して両系で流し直す。Web の画面・CEF の不具合なら直さず、分類と原因を `done.md`「## 全録画の結果」と台帳の行に記録して進む（Web 側の失敗は記録だけ。D-36） |
| R-5 | wwxy を閉じる | 台帳「## 壊れた録画の割り振り」の 10 行すべての方針の列に結果（書き直した・削除した・移植した）が入り、X-prerun の 2 行に GameUi/Web の run を書く。全録画の GameUi 側で 10 行のうち現存する 8 本（起草時: S2-ladder の 4 本・S3-ladder の 3 本・state-label。数は Task 5 Step 1 で 10 行を 1 行ずつ「現存／削除／後継」に分けて数え直す）と、削除した electric-wire の後継 `connect/electric-wire-tool-extend-chain-via-ui.cs`（GameUi）が PASS。bd `moorestech-wwxy` を close（理由に PR URL） |
| R-6 | 全録画の UITK 側（全体スイッチ GameUi） | `list-recordings.sh GameUi` の全本と書き直した state-label から、`run-scenario.sh` で流せない行（EDC の手動投入の `playtest-receiver/developer-report-interrupt.cs`・`developer-report-resume.cs` と、運転役 `.sh` から流す `uitk/stage5/uitk-event-language-gate.cs`）と撮影シナリオ `uitk/prerun/` を外した一覧を `run-all-recordings.sh` で流し、TSV の行数＝一覧の本数。外した手動の行は Task 3 Step 2b の流し方で判定する（interrupt は手動投入、言語ゲートは運転役、resume は `前提つき（判定の対象外）`。D-48）。exit 0 以外の全行を [A-177] の 6 分類（PASS／束 5 未移植／wwxy／前提つき／Web 専用／不具合）に分け、`束 5 未移植` 0・`不具合` 0（途中で見つけたものは持ち主へ回して入ったら流し直して PASS）。exit 0 以外の行は 1 回流し直し、2 回とも落ちた行だけを分類する（2 回目に通った行は `PASS（揺れ: 1 回目 FAIL）`。D-36）。どのシナリオも先回りで分類せず流した結果で分ける。表を作った HEAD の 40 桁を、控えたその場で `done.md`「## 全録画の結果」の頭に `全録画の HEAD（GameUi）:` で書く（X-switch-pr の `<PRERUN_HEAD>`。一覧・TSV は Unity が消さない `moorestech_client/.uloop/outputs/X-prerun/recordings/` に置く。D-43）。表は「シナリオ / UITK の分類 / Web の分類 / 秒 / 原因・注記」で、分類の欄は分類の語だけ（`Web 専用` の行はちょうど `Web 専用`）。`Web 専用` はシナリオ全体が Web に依るものだけで、assert の一部だけが Web に依るものは `前提つき（5a が assert を書き直す）`（Task 3 Step 3。D-29） |
| R-7 | 全録画の Web 側（全体スイッチ Web） | `list-recordings.sh Web` の全本と state-label を流し（手動投入の 2 本は Task 4 Step 2b。interrupt は Mac の Editor で Web の前置きの後に手動投入、resume は `前提つき（判定の対象外）`。D-48）、exit 0 以外の行を S4-gamma の Web 側の TSV（S4-gamma `done.md`「## 全録画の結果」）と比べ、S4-gamma で PASS だった行だけを S4-gamma の HEAD で流し直して `既存`／`退行`／`比較不能（新設）`／`比較不能（変更）` に分ける。exit 0 以外の行は 1 回流し直し、2 回とも落ちた行だけを分類する。Web 側の失敗はどの分類でも直さず停止しない（裁定「UITK 切替まで CEF 由来の不具合は直さない」と、Web が切替で消えること。CEF 由来か否かを問わず撤去前の基準として記録する。A-251・D-36）。exit 0 以外の全行に分類と原因（ログに CEF の描画停止・待ちがあれば `（CEF 由来）`）が付き、`退行` の件数と名前が台帳の行と `done.md` にある |
| R-8 | 5a への申し送り（Web 専用の一覧） | `done.md`「## 5a への申し送り」を 2 つの小節に分ける。「### 消す（シナリオ全体が Web 専用）」に分類が `Web 専用` のシナリオ全部、「### 書き直す（Web に依る assert だけを持つ）」に流した結果で落ちた assert が Web に依るものだけだったシナリオ（起草時の見込み: `playtest-receiver/developer-report-delivery.cs` の webUiHealth の 3 つ。分類は先回りで決めず Task 3 Step 3 の結果で決める。D-29）と `Client.WebUiHost` を直接参照する 8 本（起草時の実測）。どちらも、パス（`scenarios/` からの相対。「## 全録画の結果」の表の 2 列目と同じ形。5a が 2 つを突き合わせる）・落ちた assert・5a でどうするかの案。後者の分類の語に `Web 専用` の文字列を含めない（5a の grep が消す側だけに当たるように）。UITK の分類の欄がちょうど `Web 専用` の行の数と「### 消す」のパスの数が同じ（Task 5 Step 1）。物差しの流し方は 3 つ目の小節「### 物差し（prerun-smoke-shots）」に書く（Task 10 Step 3） |
| R-9 | 参照の撮り直し | origin/master の使い捨て worktree で `capture.ts` を 1280×720 と 2560×1080（W6-4）で撮り（1 回ごとに本体が background で起動して完了の通知で受け、流し直す前は出力先を消す。所要を `done.md` へ）、`moorestech_logs/uitk/reference-captures/web-<sha10>/`・`web-<sha10>-2560x1080/` に置く。両方の `manifest.json` の `viewport` が `1280x720`・`2560x1080`、`entries` の件数が同じ。台帳「## 参照キャプチャ」に撤去前の最後の参照として 1 行（撮影元・置き場・件数） |
| R-10 | 撮影の対応表（W6-5） | `done.md`「## 撮影の対応表」に新しい manifest の全 id が 1 行ずつ（撮り方 P/N/E/V/X・出所・UITK のファイル名・判定）。X は [A-38] で UITK に作らない画面だけで、出所に [A-38] と `disposition.md` の理由の行がある。Task 7 の検査スクリプトが「manifest にあって表に無い 0・表にあって manifest に無い 0・重複 0・撮り方の誤り 0（X の根拠の欠けを含む）」で exit 0 |
| R-11 | UITK の全画面の撮影 | 表の全静止画 id（X を除く。X は撮る相手が無いので R-11 と Task 11 の組の対象外で、`done.md` の表の行が根拠）について `moorestech_client/.uloop/outputs/compare/X-prerun/<id>-1280x720.png` と `<id>-2560x1080.png` が実在（Task 8〜9 の `ls` の数え上げ）。Task 9 Step 2 の逃げ道（撮れない状態を各レーンの PR の並べ比べで代える）を使った id は R-11 未達として数え、`done.md`「要裁定」に Q-n（外した id・代わりの証拠の URL・外した理由）で上げる（0 件なら何もしない。D-23）。E の id は、先に実行時に作れる状態 1 つ（`4-block-chest`）で EditMode と PlayMode の panel の寸法と panel 原点からの閉じるボタンの相対位置の差が 1280×720 で 2px 以内であることを記録してから撮る（[A-164]。panel の画面上の絶対位置は外殻の列の組み方で変わるので比べない。前例 S4-special。D-46）。E の各 id の面（対応表の `面=<testid>`）の矩形が `compare/X-prerun/uitk-panel-rects.tsv`（UITK）と `web-panel-rects-<解像度>.json`（Web。Task 7 Step 4）に両解像度ともある |
| R-12 | 動画 5 件 | 表の V の 5 件について、1280×720 は UITK 側を録画し、Web の webm と同じ相（長さの 10%・50%・90%）の 3 フレームを両方から ffmpeg で抜く。2560×1080 は録画が 1280×720 の出力に固定（`PlaytestRecorder.cs:33`）なので、同じ操作の同じ相で UITK の静止画を撮り、Web の 2560×1080 の webm から同じ相を抜く（D-32）。`compare/X-prerun/video/<id>-{start,mid,end}-{web,uitk}-{1280x720,2560x1080}.png` の 60 枚。各動画は Web の `steps` の `visible(...)` に当たる UITK の要素が出たことを Assert してから相を数える（D-33）。Web の webm の t=0 は ready の手順の後の境界で、脈動の 4 件はもう脈動の途中、通知は境界の 0.5 秒後（`EFFECT_LEAD_IN_MS`）に出るので、両解像度の動画の組で動きの位相の差を数えず、効果の出始め・通知の 0.5 秒の遅れ・実ゲームのワールドピンとチャレンジ 1 の枠とラベルを `states.md` の状態の差に書く（脈動の振幅と周期は `uitk-tutorial-cues.cs` の Assert と UI テストが見る。D-35） |
| R-13 | 独立評価 2 系統と再現 | このレーンが取りまとめ役として（D-16）、並べ比べの組（左 Web・右 UITK の合成画像。E は両側を面の矩形に切り抜き、外殻の外は数えない。D-46）を opus の評価役 A・B に互いの結果を見せずに、1 回 24 組までに分けて判定させ、指摘を別の subagent が画素で再現し、再現したものだけを `done.md`「## 差の表」に残す。再現しなかったものも 1 行で残す |
| R-14 | 差 0 か記帳済み（設計書 §3.3 (c)・§3.4） | 「## 差の表」の全行が「直した（USS・撮り直しの PNG）／状態の差（`states.md` に足して閉じた。根拠の行つき。記録だけで停止しない。D-44）／LX-210〜217（台帳の行と `/* look-exception: LX-n */` を同じコミット。`LookExceptionLedgerTest` PASS。9 件目からは「LX（番号待ち）」で停止。D-24）／要裁定 Q-n（`done.md`「要裁定」）／持ち主へ bd（入って再撮影して閉じた）」のどれか。配置・色の系統・装飾の有無・文字の大きさの差は LX にしない。USS を直した後と、撮影の後に `Client.Ui`・`UI Toolkit` を変える取り込み（持ち主の修正・Task 16 Step 2）をした後は、全 id を撮り直して前の回と比べ、`inf` でない id 全部を並べ比べ直す（D-34・D-45・D-49）。生成物の USS（先頭に `Generated … do not edit by hand` を持つ `Tokens.uss`・`SlotTokens.uss`・`SkitTokens.uss`・`MantineTokens.uss`）は手で直さない。参照の撮影元 master と `feature/uitk` の Web の食い違い（Task 7 Step 3 の表）に当たる差は「feature/uitk 側が正（master の Web 側の残り）」として差の表から外し、表の行に記録する（D-37） |
| R-15 | 撤去レーンの物差し `prerun-smoke-shots.cs`（W6-6 (b)） | `scenarios/uitk/prerun/prerun-smoke-shots.cs`（200 行未満・全体スイッチと Web の型を参照しない）が束ごとに 2 状態・計 10 状態を 1280×720 と 2560×1080 で撮り、最終 HEAD で `Success: true`・PNG 20 枚。`grep -nE "UiSystem|ActiveUiSystem|UiSystemEditorSwitch|WebUiHost|PlaytestDomQuery|Cef" scenarios/uitk/prerun/*.cs` が 0 件（Task 10 Step 2・Task 14 Step 1。D-18）。`done.md` に撮影名の一覧 |
| R-16 | 台帳の記録 | 台帳「## 実測の結論」の末尾に 2 行（全録画の 3 回目の UITK 側・Web 側）。「## 参照キャプチャ」に 1 行（R-9）。「## 壊れた録画の割り振り」の 2 行（R-3・R-4） |
| R-17 | 申し送りの処遇 | 「## 申し送りの処遇」の全行に「やった（Task n・証拠）／回した（bd id・宛先）／閉じた（理由）」が付き、`done.md` に写す |
| R-18 | 最終 HEAD でラダー 6 本と UI テスト全体 | 最終 HEAD（取り込み後）で `uloop compile` ErrorCount 0、`^Client\.Tests\.Ui\.` 全 PASS（1 本 `--timeout-seconds 570`。時間切れなら Global Constraints 4 の割り方）、ラダー 6 本が GameUi で `Success: true`・ErrorLogs 0 |
| R-19 | G1 | `python3 .github/scripts/check-uitk-web-follow.py --base origin/feature/uitk --head HEAD` が「違反なし」、かつ束ごとの「未記帳 n 件」が Task 1 Step 3 の起点と同じ（このレーンは `moorestech_web/` のコミットを足さない。未記帳 0 は切替 PR でだけ求める）。PR の `UITK Web follow gate (G1)` が pass |
| R-20 | 締め | moores-code-review → pr-create（base=`feature/uitk`・報告ページ）→ `origin/feature/uitk` 取り込み → compile・テスト・G1 → `gh pr merge --merge` → Editor を閉じる・bd を閉じる → `done.md`・合図 `cmux wait-for -S uitk-x-prerun` |

### やらないこと
- 全体スイッチ・DSL の Web ロケータ・Web ホストの撤去（X-5a〜5c）。Web 専用シナリオの書き換え・削除（W6-8 で 5a の持ち物。このレーンは一覧を申し送るだけ）
- `scenarios/uitk/prerun/**` と wwxy の 2 本以外のシナリオの編集（全録画で落ちたシナリオも、直すのは持ち主。シナリオの誤りなら bd で 5a か持ち主へ回す）
- 全録画のスクリプト（`list-recordings.sh`・`run-all-recordings.sh`）の書き換え（W6-9）
- `feature/uitk` の Web（`moorestech_web/**`）の変更。参照の撮影表（`referenceShots.ts`・`referenceVideos.ts`）への id の追加（W6-3: 撮影表ごと 5c で消える）。2560×1080 のための `shotRunner.ts:11` の書き換えは master の使い捨て worktree の中だけで、コミットしない（W6-4）
- 本番の C#（View・Presenter・部品）の変更。並べ比べで USS で直らない差は持ち主（I-fix）へ bd で回す
- 撮影の道具（`UiRuntimePanelCapture` 等）への口の追加。足りない口は `done.md`「integrator への依頼」へ
- 設計書・実行計画の本文の改訂（末尾の「## 設計書・実行計画へ反映する行」に下書きを置き、本体が docs PR で入れる）
- Codex（ユーザーの指揮への指示「一旦codexは使わず、claudeセッションで今からこの計画の遂行をして」〈`../moorestech_logs/harness/uitk/conductor.md`「## ユーザー指示（2026-11-06 15:55）」〉。`.decisions/2026-10-16-moores-writing-plansの実装引き継ぎはCodexセッションに固定する.md` と実行計画 §8 E-3 を W6 について上書きする。D-17）。§5 の codex-audit は opus の subagent 2 系統で代える
- worktree の `moores-wt rm`（指揮が行う）

## Global Constraints

1. AGENTS.md 全部（1 ファイル 200 行未満・1 ディレクトリ 10 ファイル・partial と `Func<>` 禁止〈シナリオの `p.Until(() => …)` はドライバの既存の口の呼び出しで、新しく `Func<>` を宣言しない〉・try-catch は外部境界だけ・UniRx・2 行コメント〈日本語 1 行→英語 1 行〉・`#region Internal` はメソッド内のローカル関数だけ・デフォルト引数禁止・.meta を手で作らない・Prefab/シーンを手で編集しない〈uloop execute-dynamic-code 経由はよい〉・fail-closed の経路はログ・コード変更後は必ず compile）
2. 実装はこのホスト（Mac mini）の Claude Code の subagent（opus）で行う。Codex は使わない（D-17）。実行計画 §5 の codex-audit・外部監査は **opus の subagent 2 系統の独立評価**で代える（目的・裁定・設計書・完了条件と成果物の実物だけを渡して別々に判定。設計判断になる指摘は別の subagent に再現させ、再現したものだけ残す）
3. Unity Editor: **Mac mini の Editor は全体で 1 本まで**（このレーンの worktree の 1 本。起動・停止の前に `moores-wt status` で所有を確かめる）。2 本目以降が要るときは Windows（RTX2060）の `~/bin/winunity`（push 済みのブランチを使う。`winunity compile|test|test-all|play <branch> <scenario.cs> [--ui-system GameUi|Web]|edc`）。**Editor の起動・停止・付け替え（Task 1 Step 4・Task 4 Step 3 の経路 M）は本体（制御セッション）が行い、subagent には起動済みの Editor だけを渡す。subagent はそこへ来たら作業を止めて本体へ返す。10 分を超えうる分けられないコマンド（参照の撮影の `capture.ts`。Task 6 Step 2〜3）も本体が Bash の `run_in_background` で 1 回起動し、完了の通知で受ける（ポーリングしない）。** Editor を起動したら毎回、使う前に Global Constraints 5 の前置きを流す（起動済みの Editor にはシェルの環境変数が届かない）。master の参照の撮影は `--no-editor` の worktree で行う
4. 区切り（タスク）ごとにコミットする。subagent はバックグラウンドのポーリングで待たず前景で実行する（1 回の Bash は 600 秒以内に収める。全録画は 3 本ずつ）。`run-scenario.sh` は result.json を 420 秒（`RESULT_TIMEOUT=420`。固定）しか待たないので、`prerun/` のシナリオの `ScenarioTimeoutSeconds` は 360 秒にし、最初の run の所要を `done.md`「## 証拠」に書く（350 秒を超えたシナリオは 2 本に分ける）。UI テストは `^Client\.Tests\.Ui\.` を 1 本 `--timeout-seconds 570` で前景で流し（X-5a の計画の実績で約 2 分）、時間切れなら `^Client\.Tests\.Ui\.[A-L]` と `^Client\.Tests\.Ui\.[M-Z]` の 2 本に割って流し直す。`.superpowers/` はコミットしない
5. 録画: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client <scenario.cs>`。このレーンは X-5a より前なので、PlayMode を止めた状態で `uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); Client.Localization.Localize.Initialize(); return Client.Localization.Localize.TrySetChosenLanguage("japanese");'`（Web は `UiSystem.Web`）を流し、戻り値が `true` であることを見てから入る（`run-all-recordings.sh` は 1 本ごとに系を自分で立てる）。**撮影（Task 8〜10・12・14 の撮り直しを含む）の前は毎回この前置きで表示言語を日本語に固定する**: Web の参照は mock-host の既定の日本語（`e2e/mock-host/topics/topicFixtures.ts:44` `locale: "japanese"`）で、ゲームは `PlayerPrefs` の言語（無ければ english。`Client.Localization/Localize.cs:14,35-37`）で起き、`TrySetChosenLanguage` は `PlayerPrefs` へ保存するので、前に流したシナリオ（P の出所の `uitk/uitk-notification-compare.cs:57` は最後に english にする）の言語が次の撮影へ残る。edit mode では辞書が未初期化なので先に `Localize.Initialize()` を呼ぶ（`IsSelectable` が辞書を引く。`Localize.cs:156-160`）。`prerun/` の 5 本と state-label は本体の冒頭でも `p.Assert(Localize.TrySetChosenLanguage("japanese"), …)` を呼ぶ（前例 `connect/rotary-generator-ui.cs:54`。D-30）。UI テスト（`UiTestHost`）の TearDown はスイッチを Web に戻すので、テストの後の録画の前に必ず立て直す（W0-switch:41）。`run-scenario.sh` が初回 "already playing" で終わったら 1 回だけ投入し直す（bd moorestech-trfka）。録画の前に push する（未 push の HEAD でバグ報告の箱が巨大になり送信トーストが 60 秒を超える。C2-b の申し送り）。受け口は `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` で本番へ送らない（winunity play は Windows の Editor に同じ値を入れて起こす。`winunity.d/interactive.ps1:30`）
6. 録画 mp4・`PlaytestResults/`・TestResults xml・撮影 PNG は worktree の gitignore 下に置き（全録画の一覧・TSV と G1・申し送りの控えは `$O=moorestech_client/.uloop/outputs/X-prerun`〈全録画は `$D=$O/recordings`〉、撮影は `$C=moorestech_client/.uloop/outputs/compare/X-prerun/`。どちらも `.gitignore:102` の `/moorestech_*/.uloop/outputs/` の下で、前例 C2-a・C3-a の `compare/<レーン>/` と同じ置き場）、検証後に消す。**`moorestech_client/Temp/` には何も置かない**: Unity は Editor を正常に閉じるとプロジェクトの `Temp/` を消し（起草時の観測: Editor の居ないメインクローンと worktree に `Temp/` が無く、Editor が動いている worktree だけにある）、このレーンは経路 M（Task 4 Step 3）と停止（Task 3 Step 3・Task 12）の間に Editor を閉じる。あとで読む値（全録画の HEAD・G1 の起点の件数）は控えた時点で `done.md` にも書き、後の手順は `done.md` から読む（D-43）（報告ページへ載せる分は先に載せる）。`../moorestech_logs` に置くのは md と小さな JSON とスクショ 3 枚程度まで（書く前に `../moorestech_logs/README.md` を読む）。参照キャプチャの置き場 `moorestech_logs/uitk/reference-captures/` は既存の運用（台帳から辿る正本）。テストで `Saves/` に作ったワールドは `.agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で探して消す
7. 番号: 設計書 §7.1 の行は **A-250**（撮り方と独立評価）・**A-251**（撤去前の最後の Web の基準）、見た目の例外は **LX-210〜LX-217** だけ。計画・レーンは設計書・実行計画を書かない
8. 共有ファイル（`docs/uitk/TODO.md`・`docs/uitk/disposition.md`・USS・tss）は自分の行・節だけ触る。取り込みの衝突は両方の行を採る。`DerivedTokens.uss`（起草時 195 行）など 190 行を超える USS へ足すときは、先に責務で分けてから足す。分けた先は、元のディレクトリが 10 ファイル以上ならサブディレクトリにする（`Client.Ui/Foundation/Theme/` は起草時 14 ファイル〈USS 9・.cs 4・txt 1〉なので、`DerivedTokens.uss` を分けるなら `Foundation/Theme/Derived/<責務>.uss`。`GameUiTheme.tss` の `@import url("project://database/Assets/Scripts/Client.Ui/Foundation/Theme/Derived/<責務>.uss");` は元の `DerivedTokens.uss` の行の直後に置き、変数の定義の順を保つ）。生成物の USS（1 行目が `/* Generated from … — do not edit by hand. */` の `Tokens.uss`・`SlotTokens.uss`・`SkitTokens.uss`・`MantineTokens.uss`）は手で直さない（D-37）
9. 最終タスク対（実行計画 §4.3）: moores-code-review → pr-create（`base=feature/uitk`）→ 取り込み・compile・テスト・G1 → `gh pr merge --merge`（Squash 禁止）→ Editor を閉じる・bd を閉じる（`moores-wt rm` は指揮）→ `done.md` → `cmux wait-for -S uitk-x-prerun` を 1 回
10. 撮影は `compare-procedure.md` §3「撮影の道具」を名指しで使う（`UiRuntimePanelCapture`〈EditMode〉、`GameViewResolution.Set`／`PlaytestGameViewOps.SetResolution`〈PlayMode〉）。`--capture-mode rendering` は UITK が写らないので使わない
11. 判断台帳: このレーンが Modify/Create するファイル（シナリオ・USS・台帳・処遇表）は `.claude/skills/moores-code-review/reviewers/moores-*.md` の `paths`（`VanillaSchema/`・`Core\.Master`・`BlockTemplate`・`Server\.Protocol`・`Server\.Event`・`Client\.Network`・`DataStore`）に当たらない（起草時に照合）。USS を直すときに当たるものが出たら判断記録に足す
12. `prerun/` のシナリオは全体スイッチ（`UiSystem`・`UiInputExclusivity.ActiveUiSystem`・`UiSystemEditorSwitch`）と Web の型（`Client.WebUiHost`・`PlaytestDomQuery`・`Cef*`）を参照しない。UITK で描かれていることは `GameUiRoot.Instance.GetRootElement()` の要素の照会で確かめる（D-2）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 配置 16 項目は前例・規約表と矛盾なし。別に実物の食い違い 2 件（Pending の数え方・wwxy の現存の内訳）を直した
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱2・第3バケツ0 / 並べ比べの判定と修正の担い手を A-179 からこのレーンへ移すことを D-16 に記録、物差しの型の不参照の grep と研究の照会の exit 1 を足した

## 死活表（Phase 2.5）

このレーンはゲームの操作を消さない（本番の C# を触らない）。消えるのはシナリオ 1 本だけで、ゲームの操作ではない。

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| 全画面の UITK の操作（GameUi） | 生きる | 本番の C# は触らない。USS は並べ比べで再現した差だけを Web へ寄せる。R-18 のラダー 6 本と UI テスト全体で確かめる |
| 全画面の Web の操作（全体スイッチ Web） | 生きる（この PR の後 X-5a で消える） | このレーンは Web を触らない。Web 側が使えるのはこのレーンが最後で、切替後は不要（裁定 2026-11-06 一気切替の日に撤去）。だから全録画の Web 側をここで残す |
| 全体スイッチ（`UiSystemEditorSwitch`） | 生きる（X-5a で消える） | 切替後は不要（裁定 2026-11-06 一気切替の日に撤去）。このレーンは使うだけ |
| 研究ツリーの状態ラベルの録画 `misc/research-node-state-label.cs` | 生きる（直る） | 現行の研究 GUID で 3 状態を作る形へ書き直す（R-4） |
| 研究の番号付けの録画 `tutorial-research-renumber.cs` | 消える（シナリオ。ゲームの操作ではない） | ADR 0038 で研究 1〜3 が消え、見る対象のゲームの挙動が無い（台帳「## 壊れた録画の割り振り」の方針）。研究の番号付けの操作そのものはゲームに無い |
| 撤去レーンの見た目の不変の確かめ | 生まれる | `prerun-smoke-shots.cs`（R-15）。5a〜5e が起点と撤去後で流す |

## ファイルの持ち主

このレーンが書く範囲（W6 のファイルの持ち主の表の X-prerun 行）:
- `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/**`（新設）
- wwxy の残り 2 本: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-research-renumber.cs`（削除）・`.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs`（書き直し）
- `docs/uitk/TODO.md` の「## 参照キャプチャ」（撮影元・置き場・URL・2560×1080 の参照の 4 行を撤去前の最後の参照へ書き換え、前の組を履歴の 1 行へ移す。台帳の規則「撮り直したら、ここの撮影元と置き場を新しい方へ書き換える」）・「## 壊れた録画の割り振り」（X-prerun の 2 行の方針。他の 8 行は、方針の列に結果が無いときだけ直すレーンの `done.md` の記録を `X-prerun` の語を含めずに写す。D-50）・「## 実測の結論」末尾（2 行）・「## 見た目の例外」の LX-210〜217
- `docs/uitk/disposition.md`「## 突き合わせの記録」の撤去前の行（束 1〜5）
- 並べ比べで再現した差を直す USS（`moorestech_client/Assets/Scripts/Client.Ui/**/*.uss`）と、その USS に付ける `look-exception` の印
- 190 行を超える USS を責務で分けたときに足す `@import` の行（`moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の自分の行だけ）

触らないもの: 他のシナリオ全部（5a）・全録画のスクリプト（S4-gamma が作り 5a が直す）・`Client.Starter`・`Client.Input`・`Client.Playtest`・`Client.WebUiHost`・`moorestech_web/**`・本番の C#・テスト（`Client.Tests/**`）・CI・スキル文書・台帳の「## 束」「## 行」「## 追従記録」（X-switch-pr）。

## 消すもの・残すもの・移すものの全件表

| 区分 | 対象 | 消す前に通るもの | 消した後に通るもの |
|---|---|---|---|
| 消す | `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-research-renumber.cs`（起草時 150 行） | `git grep -n "tutorial-research-renumber" -- ':!docs/superpowers/plans' ':!docs/uitk-migration'` で参照の一覧を取る（台帳の行と消すファイル自身の 37 行目〈`PlaytestRunner.Run("tutorial-research-renumber"…`〉の 2 件のはず） | 同じ grep が台帳の行の 1 件だけ。`list-recordings.sh GameUi` に出ない |
| 書き直す | `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs`（起草時 56 行） | — | GameUi と Web の run が `Success: true`（R-4） |
| 新設 | `scenarios/uitk/prerun/` の 5 本（File Structure） | — | 各 1 回 `Success: true` |
| 残す | 他のシナリオ・スクリプト・テスト・CI・文書 | — | — |
| 移す | なし | — | — |
| テスト | 消すテスト 0・残すテスト 全部・移すテスト 0（NUnit を書かない。`/// <web-test>` の追加 0。突き合わせは R-2 の記録だけ） | — | — |

## 申し送りの処遇

| 出所 | 項目 | 処遇 |
|---|---|---|
| W0-tools:19・台帳 | wwxy の残り 2 本（X-prerun） | やる（Task 2） |
| W0-tools:25 | 束 1 の「写す予定」の NUnit 化（albof.21）が X-prerun の前提 | やる（Task 1 で `--bundle all` exit 0 を確かめる。崩れていれば持ち主へ bd・停止） |
| C2-b:71・S2-ladder 依頼 2 | `playtest-receiver/developer-report-delivery.cs` の webUiHealth の assert（GameUi で 3 つとも FAIL・実箱ロックの assert も FAIL し NRE） | 先回りで分類しない。他のシナリオと同じく流した結果で分ける（Task 3 Step 3 の `前提つき` の規則。GameUi で落ちた assert が webUiHealth だけなら `前提つき（5a が assert を書き直す）`、実箱ロックの assert が GameUi だけで落ちるなら `不具合`、Web でも同じく落ちるなら受け口の前提の崩れ）。結果を 5a への申し送りの「書き直す」の小節か持ち主の bd へ（Task 5。D-29） |
| C2-b:75・S2-ladder 依頼 5 | uitk-lifeline の送信トーストが 60 秒を超える（未 push の HEAD で巨大な bundle） | やる（録画の前に push。Global Constraints 5） |
| W0-switch:41 | テストの TearDown がスイッチを Web に戻す | やる（録画の前に毎回スイッチを立て直す。スクリプトがする・ラダーは手で立てる） |
| W0-switch:43・S3-ladder 依頼 | 新しい worktree の Web 側の録画の前に `moorestech_web/node` のコピーと `pnpm install` | やる（Task 1 Step 6） |
| W0-switch:44 | 受け口を本番にしない | やる（Global Constraints 5） |
| S2-ladder:28,42,103 | `tutorial-craft-guidance` の Web の回は木の板の選択とクラフトの長押しを UI で確かめていない | 閉じた（分類に「PASS（Web は UI 未確認・既知）」と書く。Web に testid を足すのは feature/uitk の Web を触るので行わず、5a で Web 側が消える） |
| S2-ladder:48 | 通知とスキットの再走 2 本は GameUi 専用で Web 側を撮っていない | 閉じた（`scenarios/uitk/` は Web の一覧から外れる設計。Web の見た目の正本は参照キャプチャで、R-9〜R-14 が全画面を比べる） |
| S3-ladder 依頼 | 旧 `connect/electric-wire-connect-via-ui.cs` だけが見ていた検証（結線前は別セグメント・セグメント統合・電力範囲ボックス）を後継 extend-chain へ移すか | 回した（bd を `--parent <bd>` で 1 件。シナリオは W6-8 で 5a の持ち物で、移す先の書き換えは 5a か I-fix。D-9） |
| C4-e:59・I-fix3:53・S4-train:64・S4-special:93 | 撮影表への追加（掴み絵 `4-player-inventory-grab`・`3-modal-backdrop-only`・列車族 6・特殊族 `self/*`） | 閉じた（W6-3: 各レーンの PR の並べ比べを証拠にし、撮影表は 5c で消える） |
| S2-ladder 依頼 1 | バグ報告の記述欄の幅 356px→354px（Web・UITK の両方で右枠が切れる） | 閉じた（`feature/uitk` の Web〈`tokens.css:149`〉と UITK〈生成物 `Tokens.uss:59`〉は I-fix2 ad2ff7d2ae で両方 354px に直り、台帳「## 追従記録」に束 1 `写した` で記帳済み。master の Web は 356px のままなので、master で撮る `1-bug-report` の参照だけ右枠が切れる。この差は Task 7 Step 3 の食い違いの表に「feature/uitk 側が正（master の Web 側の残り）」として書いて並べ比べの差から外し、`Tokens.uss` を手で戻さない。D-15・D-37） |
| X-5a（直し役からの依頼） | 「## 5a への申し送り」の「### 消す」「### 書き直す」と「## 全録画の結果」でシナリオのパスを同じ形で書く（5a は小節のパスを `grep -oE '[A-Za-z0-9_./-]+\.(cs|sh)'` で拾い、表の 2 列目と突き合わせる） | やる（Task 5 Step 1。どちらも `scenarios/` からの相対〈例 `misc/cef-webui-showcase.cs`〉に揃え、突き合わせの python で確かめる） |
| X-switch-pr（直し役からの依頼） | UITK 側の全録画を流した HEAD の 40 桁 SHA を `done.md` に書く（X-switch-pr の `<PRERUN_HEAD>`。`比較不能（変更）` と `退行` の分け目） | やる（Task 3 Step 2 で控えたその場で `done.md`「## 全録画の結果」の頭に書いて logs へ push し〈D-43〉、Task 5 Step 1 で確かめる。Task 14 Step 3 で GameUi 側を流し直したらその HEAD に替える） |
| 前のレーンの `done.md`（S2-ladder・S3-ladder・S4-gamma・S5 の各レーン・I-fix） | 「申し送り」「integrator への依頼」「未確認」の節の X-prerun 宛て・撤去の前に済ませる項目 | やる（Task 1 Step 3b で全部読み、この表に 1 行ずつ足して受けるタスクを書く。受け先の無い項目 0） |
| X-5a（直し役からの依頼。独立評価 3 周目の時点） | `playtest-receiver/developer-report-interrupt.cs`・`developer-report-resume.cs` は `PlaytestRunner.Run` を呼ばない EDC の手動投入のスニペットで `run-scenario.sh` では必ず exit 1。両系の全録画でこの 2 行の扱いを分けて書く（5a は X-5a 計画 Task 8 Step 7 の手動投入で判定する） | やる（Task 3 Step 1 で一覧から外し、Task 3 Step 2b・Task 4 Step 2b で interrupt を手動投入〈W0-cef 計画 Task 4 Step 6〉、resume は `前提つき（判定の対象外）`。運転役 `.sh` を持つ `uitk/stage5/uitk-event-language-gate.cs` も同じく外して運転役で流す。X-switch-pr R-9 と同じ語。D-48） |
| 台帳「## 未撮影」16 行 | mock-host で撮れない状態（CEF の合成・実マスタのアイコン・Ctrl+U・ドラッグの最中・IME の変換中など） | 閉じた（参照が無い状態は比べる正本が無い。各画面レーンが実ゲームで撮った組が証拠。IME の変換中は X-switch-pr の IME 一巡） |

## 撮影の対応表（起草時の案。Task 7 で新しい manifest と各レーンの `done.md` に照らして確定する）

撮り方: **P** 既存の段の撮影シナリオ（書き換えない。出力名を写して名前をそろえる）／**N** このレーンが `scenarios/uitk/prerun/` に新設する撮影シナリオ／**E** EditMode の `UiRuntimePanelCapture`（実行時に作れない状態。外殻と偽の Model は「前例」列のレーンの `done.md` の組み方を写す）／**V** 動画（`prerun-videos.cs` で録画し 3 フレーム）／**X** UITK に作らない画面（設計書 [A-38]・実行計画 E-17。比べる相手が無いので撮らず、組も作らない。出所の列に [A-38] と `docs/uitk/disposition.md` の理由の行を書く。D-31）。起草時の manifest（`web-8a585f742a`）は 89 件（静止画 84・動画 5）。

| id | 束 | 撮り方 | 出所（シナリオ／前例） | 出力名（run dir 内） |
|---|---|---|---|---|
| 1-pause-top | 1 | N | `prerun/prerun-shots-pause-train.cs` | `pt-1-pause-top-<w>x<h>` |
| 1-pause-disconnected | 1 | E | 段階 1 screens レーン（`PauseMenuViewTest` の切断の状態）。接続の切断は実行時に作れない | — |
| 1-pause-delivery-pending | 1 | E | 段階 1 screens・C2-b（送信待ちの件数）。outbox の件数は実行時に決められない | — |
| 1-pause-in-skit | 1 | P | `uitk/uitk-skit-compare.cs` | `<prefix>-uitk-pause-in-skit` |
| 1-pause-in-train | 1 | N | `prerun/prerun-shots-pause-train.cs` | `pt-1-pause-in-train-<w>x<h>` |
| 1-bug-report | 1 | N | `prerun/prerun-shots-pause-train.cs` | `pt-1-bug-report-<w>x<h>` |
| 1-settings | 1 | N | `prerun/prerun-shots-pause-train.cs` | `pt-1-settings-<w>x<h>` |
| 1-settings-graphics-recovered | 1 | E | 段階 1 screens（画質の復旧の表示） | — |
| 1-toast | 1 | E | 段階 1 screens（`ToastHostViewTest` の見本） | — |
| 2-game-hud・2-mining・2-placement・2-placement-unavailable・2-placement-connect-tool・2-delete-bar・2-tooltip・2-tooltip-multiline・2-world-pin-onscreen・2-world-pin-offscreen・2-key-control-hint（11 件。表では 1 行ずつ書く） | 2 | P | `uitk/stage2/uitk-stage2-compare.cs`（S2-ladder） | `<w>x<h>-<id>` |
| 2-progress-labeled | 2 | E | S2-hud（ラベル付きの進み。値を実行時に止められない） | — |
| 2-train-hud | 2 | N | `prerun/prerun-shots-pause-train.cs` | `pt-2-train-hud-<w>x<h>` |
| 2-notifications | 2 | P | `uitk/uitk-notification-compare.cs` | `uitk-2-notifications-<w>x<h>` |
| 2-challenge-hud-multiple-long | 2 | E | S2-hud Task 10（長い目標 3 文） | — |
| 2-skit-background・2-skit-text・2-skit-choices・2-skit-transition（4 件） | 2 | P | `uitk/uitk-skit-compare.cs` | `<prefix>-uitk-<id から先頭の 2- を除いた名>` |
| 3-build-menu・3-hotbar-equipment-empty・3-modal（3 件） | 3 | P | `uitk/stage3/uitk-stage3-compare.cs` | `uitk-<id>-<w>x<h>` |
| 4-player-inventory | 4 | P | `uitk/stage4/uitk-stage4-wave-shots.cs`（S4-gamma） | `4-wave-playerInventory-<w>x<h>` |
| 4-research | 4 | P | 同上 | `4-wave-researchTree-<w>x<h>` |
| 4-player-inventory-full | 4 | E | S4-inventory | — |
| 4-item-list-large | 4 | E | S4-recipe | — |
| 4-block-machine-recipe-selection | 4 | E | S4-machine・S4-recipe（大量のレシピ・選択） | — |
| 4-block-train-timetable-tab・4-block-train・4-block-trainTimetable・4-block-trainError・4-block-trainPlatform・4-block-trainFluidPlatform（6 件） | 4 | E | S4-train・S4-alpha（`4-block-trainError`） | — |
| 4-block-chest・4-block-tank・4-block-machine・4-block-gearMachine・4-block-generator・4-block-miner・4-block-gearMiner・4-block-filterSplitter・4-block-pumpNoVein・4-block-gearPump・4-block-gearGenerator・4-block-electricToGear・4-block-electricPole・4-block-spaceElevator（14 件） | 4 | N | `prerun/prerun-shots-blocks-a.cs`（chest〜gearMiner の 7 種）・`prerun/prerun-shots-blocks-b.cs`（filterSplitter〜spaceElevator の 7 種）。平らな地面に置いて開く | `bk-<id>-<w>x<h>` |
| 4-block-generic | 4 | E | S4-alpha | — |
| 4-block-pump | 4 | E | S4-machine（鉱脈のある油井。平らな地面では作れない） | — |
| 4-block-machineShortInput・4-block-machineFilledOutput | 4 | E | S4-machine | — |
| 4-block-cleanRoomMachine・…ManyFilters・…NoFilters・…16Filters・…20Filters・…20FiltersSelecting・4-block-cleanRoomAirFilter2Filters・…8Filters・…14Filters・…20Filters・…30Filters・…30FiltersUnconnected（12 件） | 4 | E | S4-special（清浄室） | — |
| 4-block-spaceElevatorReordered・…Loading・…Insufficient・…Climbing・…OutputBlocked（5 件） | 4 | E | S4-special（宇宙エレベーター） | — |
| 5-challenge-list | 5 | N | `prerun/prerun-shots-b5.cs` | `b5-5-challenge-list-<w>x<h>` |
| 5-event-language-gate | 5 | E | S5-gate（ゲートの L1 の外殻に待ち状態の Model。ゲームの DI より前に 1 度だけ出る全画面で、起動後に出し直す口が無い。D-31） | — |
| 5-tutorial-outline-label | 5 | N | `prerun/prerun-shots-b5.cs` | `b5-5-tutorial-outline-label-<w>x<h>` |
| 5-tutorial-research-node | 5 | N | `prerun/prerun-shots-b5.cs` | `b5-5-tutorial-research-node-<w>x<h>` |
| 5-tutorial-recipe-item | 5 | E | S2-tutorial（レシピのアイテムのアンカー） | — |
| 5-reconnect | 5 | X | [A-38]（再接続オーバーレイは UITK に作らない。Web↔C# の WS が無く状態が起きない）・`docs/uitk/disposition.md` の S5-crosscut の理由の行 | — |
| 5-japanese-inventory | 5 | N | `prerun/prerun-shots-b5.cs` | `b5-5-japanese-inventory-<w>x<h>` |
| 2-notification-enter-aggregate-exit・2-key-control-hint-pulse・2-world-pin-pulse・4-drag-guide・5-tutorial-pulse（5 件） | 2・4・5 | V | `prerun/prerun-videos.cs` | 1280×720: `recording.mp4` と Note の `[video] <id> 1280x720`／2560×1080: `vid-<id>-{start,mid,end}-2560x1080`（D-32） |

S5-ladder が段 5 の撮影シナリオ（`scenarios/uitk/stage5/`）を持っていれば、束 5 の N の行はそちらを P に切り替え、`prerun-shots-b5.cs` は残った id だけを撮る。P の出所のシナリオが無ければ N に切り替えて `prerun/` に足す（10 ファイルまで）。

## File Structure

| ファイル | 種類 | 責務 |
|---|---|---|
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-research-renumber.cs` | Delete | R-3 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs` | Modify（書き直し） | R-4。系に依らない testid で 3 状態を見る |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-smoke-shots.cs` | Create | R-15。撤去レーンの見た目の物差し（10 状態 × 2 解像度） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-pause-train.cs` | Create | 束 1・2 の N（ポーズ・設定・バグ報告・乗車中のポーズ・列車 HUD） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-blocks-a.cs` | Create | 束 4 の N（ブロックを置いて開く 14 種の前半 7 種。1 本で 14 種だと `RESULT_TIMEOUT` 420 秒に近いので 2 本に分ける。D-19） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-blocks-b.cs` | Create | 束 4 の N（後半 7 種。本文は `-a` と同じで配列とシナリオ名だけ違う） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-b5.cs` | Create | 束 5 の N |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-videos.cs` | Create | 動画 5 件の UITK 側（R-12） |
| `moorestech_client/Assets/Scripts/Client.Ui/**/*.uss` | Modify（並べ比べで再現した差だけ） | R-14 |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | Modify（USS を分けたときだけ。`@import` の自分の行） | R-14。分けた USS を読み込ませる |
| `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/Derived/<責務>.uss` | Create（`DerivedTokens.uss` を分けたときだけ） | Global Constraints 8 の置き場 |
| `docs/uitk/TODO.md` | Modify | 自分の 4 節（ファイルの持ち主） |
| `docs/uitk/disposition.md` | Modify | 「## 突き合わせの記録」の束 1〜5 の 5 行 |
| `../moorestech_logs/harness/uitk/X-prerun/done.md` | Create（repo 外） | 報告（撮影の対応表・全録画の結果・差の表・5a への申し送りを含む） |
| `../moorestech_logs/uitk/reference-captures/web-<sha10>/`・`web-<sha10>-2560x1080/` | Create（repo 外） | R-9 |
| `moorestech_client/.uloop/outputs/X-prerun/`（`recordings/`・`g1-before.txt`・`prerun-handoffs.txt`） | repo に置かない（gitignore 下） | 全録画の一覧・TSV・手動投入の結果と、G1・申し送りの控え。Unity が Editor の終了で消す `Temp/` には置かない（D-43） |
| 使い捨て worktree の `moorestech_web/webui/e2e/uitk-reference/prerun-panel-rects.ts` | repo に置かない（worktree ごと畳む） | E の組の切り抜きに使う Web の面の矩形を測る（Task 7 Step 4。D-46） |
| （EditMode の撮影のコード片） | repo に置かない | `moorestech_client/.uloop/outputs/compare/X-prerun/edc/<id>.cs`（gitignore 下）に書いて `uloop execute-dynamic-code --code-file` で流す。中身の要点は `done.md` の対応表の行に書く |

`scenarios/uitk/` の直下は起草時 28 本で 10 を超えているので、新しいファイルは足さず `prerun/` に置く（前例 `stage3/`・S4-gamma D-24 の `stage4/`）。`prerun/` は 6 本（`prerun-videos-2560.cs` に分ければ 7 本。10 本まで）。

## Tasks

### Task 1: 前提の確認・起点の記録・Editor・bd・done.md の骨組み

**Files:**
- Create（repo 外）: `../moorestech_logs/harness/uitk/X-prerun/done.md`

**Interfaces:**
- Consumes: `origin/feature/uitk` の HEAD、S4-gamma の `tools/uitk/{,recordings/}list-recordings.sh <GameUi|Web>`（標準出力に 1 行 1 本のパス・標準エラーに外した名前）と `run-all-recordings.sh <GameUi|Web> <list.txt> <out.tsv> <開始行> <本数>`（TSV `scenario system exit seconds run_dir` を追記）
- Produces: `$REC`（2 本のスクリプトのディレクトリ）、`$BASE`（このレーンの起点 10 桁）、`$S4G`（S4-gamma の Web 側 TSV を作った HEAD 10 桁）、ラダー 6 本のパス、bd `<bd>`、`done.md` の骨組み

- [ ] **Step 1: 作業場所と起点**

Run:
```bash
pwd
git fetch origin
git status --short | head
git log -1 --format='%H %s' HEAD
git merge-base --is-ancestor origin/feature/uitk HEAD && echo "feature/uitk を含む"
```
Expected: worktree（`uitk-lane/x-prerun`）・作業ツリーが空・HEAD が `origin/feature/uitk` を含む。`$BASE=$(git rev-parse --short=10 HEAD)` を `done.md` へ。

- [ ] **Step 2: 前提のレーンが入っているか**

Run:
```bash
git log --oneline --merges origin/feature/uitk | grep -E "uitk-lane/(s2-ladder|s3-ladder|s4-gamma|s5-crosscut|s5-ladder)"
for l in S2-ladder S3-ladder S4-gamma S5-crosscut S5-ladder; do echo "$l: $(head -1 ../moorestech_logs/harness/uitk/$l/done.md 2>/dev/null)"; done
S=.agents/skills/unity-playmode-recorded-playtest/scenarios
for n in uitk-lifeline uitk-opening-hud uitk-build-bp uitk-production-loop uitk-research-train uitk-challenge-gate; do echo "$n: $(find $S -name "$n.cs")"; done
ls tools/uitk/list-recordings.sh tools/uitk/run-all-recordings.sh tools/uitk/recordings/list-recordings.sh tools/uitk/recordings/run-all-recordings.sh 2>/dev/null
grep -n "## 全録画の結果" ../moorestech_logs/harness/uitk/S4-gamma/done.md
grep -n "全録画の 2 回目・Web 側" docs/uitk/TODO.md
```
Expected: マージコミット 5 本・5 つとも「状態: 完了」・ラダー 6 本のパスが 1 つずつ（起草時の予定: `uitk/uitk-lifeline.cs`・`uitk/stage2/uitk-opening-hud.cs`・`uitk/stage3/uitk-build-bp.cs`・`uitk/stage4/uitk-production-loop.cs`・`uitk/stage4/uitk-research-train.cs`・S5-ladder の置き場）・スクリプト 2 本がどちらかの置き場に揃う（その置き場を `$REC` とする）・S4-gamma の「## 全録画の結果」の節と台帳の Web 側の行がある。台帳の行の `<HEAD 10 桁>` を `$S4G` とする。どれかが欠けたら `done.md` 1 行目を「状態: 停止（前提の欠け: <欠けたもの>）」にして push し、`cmux wait-for -S uitk-x-prerun` を 1 回送って止まる（D-10）

- [ ] **Step 3: 台帳と突き合わせの前提**

Run:
```bash
awk '/^## 束/{f=1;next} /^## /{f=0} f' docs/uitk/TODO.md
grep -c '{ AnchorIds\.' moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs 2>/dev/null; echo "pending-file-exit=$?"
node tools/uitk/test-name-parity.mjs --bundle all; echo "parity-exit=$?"
O=moorestech_client/.uloop/outputs/X-prerun; mkdir -p $O
python3 .github/scripts/check-uitk-web-follow.py --base origin/feature/uitk --head HEAD > $O/g1-before.txt; echo "g1-exit=$?"; cat $O/g1-before.txt
awk '/^## 壊れた録画の割り振り/{f=1;next} /^## /{f=0} f' docs/uitk/TODO.md | grep '^| `' | grep -v 'X-prerun' | awk -F'|' '{print $2 "|" $6}'
```
Expected: 束 1〜5 が `暗転`（起点付き）・Pending の表に持ち主の行が 0（ファイルが無いか、`Owners` の項目 `{ AnchorIds.… }` の数が 0。クラスの説明コメントの「S5-ladder」は数えない。起草時は項目 3）・parity exit 0・G1 exit 0 で「違反なし」の行（束ごとの「未記帳 n 件」の行を `done.md`「## 前提」に写す。Task 16 Step 2 は `done.md` のこの行と比べる。未記帳 0 は切替 PR でだけ求める）。wwxy の X-prerun 以外の 8 行の方針の列が、どれも結果の語（`書き直した`・`削除した`・`移植した`）で始まる（起草時は S2-ladder の 4 行〈pebble・equip・tree-pin・server-notification-smoke〉が方針の文のままで、S2-ladder の締めで埋まる前提）。結果の無い行があれば、その行の直すレーンの `done.md` でそのシナリオの直しの記録（コミット・run）を探し、記録があれば Task 2 Step 4 の台帳のコミットでその行の方針の列を `書き直した（<レーン> <コミット 10 桁>。<レーン>/done.md から写した）` に書き換える（**行に `X-prerun` の語を書かない**: S4-gamma の `list-recordings.sh` は行のどこかに `X-prerun` を含む行を一覧から外すので、書くとその本が全録画から黙って抜ける）。記録が無ければ Step 2 と同じ手順で「状態: 停止（前提の欠け: wwxy の <シナリオ> の直しの記録）」で止まる（D-50）。parity が 1 なら未対応・写す予定の行の持ち主（束の画面レーン。取り込み済みなら I-fix）へ bd を `--parent moorestech-albof` で立て、Step 2 と同じ手順で「状態: 停止（突き合わせの差分 n 件の修正待ち）」で止まる

- [ ] **Step 3b: 前のレーンの申し送りを受ける**

Run:
```bash
H=../moorestech_logs/harness/uitk
for f in $(grep -l "X-prerun\|撤去の前\|integrator" $H/*/done.md); do echo "### $f"; awk '/^## /{f=($0 ~ /申し送り|integrator への依頼|未確認|未完了/)} f' "$f"; done > moorestech_client/.uloop/outputs/X-prerun/prerun-handoffs.txt
grep -n "X-prerun\|integrator\|撤去の前\|全録画\|参照キャプチャ\|並べ比べ" moorestech_client/.uloop/outputs/X-prerun/prerun-handoffs.txt
```
Expected: 当たった項目（1 行目だけでなく「申し送り」「integrator への依頼」「未確認」の節）を 1 つずつ読み、X-prerun 宛て・撤去の前に済ませる項目を「## 申し送りの処遇」の表と同じ形で `done.md`「## 申し送りの処遇」に足し、受けるタスク（Task n の Step）か「回した（bd・宛先）」「閉じた（理由）」を書く。受け先の無い項目 0。計画の表に無い項目で作業が要るものは、近いタスクの Step に足して判断記録へ `D-n（実行時）` で書く

- [ ] **Step 4: Editor と compile（本体が行う）**

Run:
```bash
moores-wt status
uloop compile --project-path ./moorestech_client
```
Expected: Mac の Editor はこのレーンの worktree の 1 本だけ（指揮が `moores-wt new` で起こした）。無ければ本体が、他の Editor が 0 本であることを見てから `uloop launch ./moorestech_client` で起こし、Global Constraints 5 の前置きを流して戻り値 `true` を見る（Editor を起こすたびに同じ。subagent は起こさない）。他者の Editor があれば落とさず終了を待つ。compile は ErrorCount 0

- [ ] **Step 5: Windows の CEF の描画を試す（Web 側を winunity で並走させるか決める）**

Run（ブランチを push 済みにしてから）:
```bash
git push -u origin HEAD
~/bin/winunity play "$(git rev-parse --abbrev-ref HEAD)" .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cef-webui-showcase.cs --ui-system Web; echo "exit=$?"
```
Expected: exit 0 なら Web 側の全録画を Windows で回す（Task 4 の経路 W）。exit 1 で `play/result.json` か `log-excerpt.txt` に CEF の描画の失敗（RendererUnavailable 等）があれば、Web 側は Mac で GameUi の後に流す（経路 M）。どちらにしたかと run dir を `done.md`「## 前提」に書く（D-1）

- [ ] **Step 6: Web 側の録画の準備（経路 M のときだけ）**

Run:
```bash
test -d moorestech_web/node || cp -Rc ~/hermes-agent/data/repos/moorestech/moorestech_web/node moorestech_web/node
(cd moorestech_web/webui && pnpm install --frozen-lockfile)
```
Expected: `moorestech_web/node/` がある・install が 0 で終わる（`docs/development/worktree-and-review-pitfalls.md`「新しい worktree で録画シナリオが ready 後に止まる」）

- [ ] **Step 7: bd と done.md の骨組み**

Run:
```bash
bd create "UITK X-prerun: 撤去の前の全録画・wwxy の残り・全画面の並べ比べ" --parent moorestech-albof -t task
bd update <bd> --claim
mkdir -p ../moorestech_logs/harness/uitk/X-prerun
```
`done.md` を次の骨組みで作る:
```markdown
状態: 進行中
# X-prerun
- bd: <bd>
- 起点: origin/feature/uitk <BASE>
## 前提
## 撮影の対応表
## 全録画の結果
## 5a への申し送り
## 差の表
## 申し送りの処遇
## 要裁定
## integrator への依頼
## 未確認
## 証拠
```

- [ ] **Step 8: コミットは無し**（記録は repo 外。`../moorestech_logs` でコミット・push）

---

### Task 2: wwxy の残り 2 本（R-3・R-4・R-5 の台帳）

**Files:**
- Delete: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-research-renumber.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs`
- Modify: `docs/uitk/TODO.md`（「## 壊れた録画の割り振り」の X-prerun の 2 行）

**Interfaces:**
- Consumes: `PlaytestRunner.Run(string, PlaytestRunOptions, Func<PlaytestDriver, UniTask>)`（既存の口。シナリオは渡すだけ）、`p.SetupDebugEnvironment`・`p.SkipOpeningSkitIfPlaying`・`p.ExitToGameScreen`・`p.GiveItemDirect(string, int)`・`p.PressKey(Key)`・`p.Until`・`p.Screenshot(string)`・`p.CurrentUiState`、`ClientContext.VanillaApi.SendOnly.CompleteResearch(Guid)`（`Client.Network/API/VanillaApiSendOnly.cs:104`）、`PlaytestUiElementOps.FindUi(string)` → `UiElementSnapshot`（`IsFound`・`Text`）、UITK の testid `research-node-state-<guid>`（`Client.Ui/Screens/Research/Parts/ResearchNodeCard.cs:38`）と Web の同じ testid、文言 `ui.research.completed`＝完了済み・`ui.research.stateAvailable`＝研究可能・`ui.research.stateUnavailable`＝研究不可（`Localization/localization.csv:193,210,211`）
- Produces: なし

- [ ] **Step 1: ピン先マスタの研究を確かめる**

Run:
```bash
PIN=$(python3 -c 'import json;print([r["commitHash"] for r in json.load(open(".moorestech-external-revisions.json"))["repositories"] if r["key"]=="moorestech_master"][0])')
git -C ../moorestech_master show "$PIN":server_v8/mods/moorestechAlphaMod_8/master/research.json | python3 -c '
import json,sys
rows={r["researchNodeGuid"]:r for r in json.load(sys.stdin)["data"]}
missing=[]
for g in ["858bcb10-b8ba-478e-9bc5-473ca61281a2","b47c5e3c-1b58-42c5-a477-d485d2eae747","bc5e7786-6759-4271-8095-836703b54490"]:
    if g not in rows:
        missing.append(g); continue
    r=rows[g]; print(g, r["researchNodeName"], r["prevResearchNodeGuids"], r["consumeItems"])
if missing:
    print("ピン先マスタに無い研究:", missing); sys.exit(1)'
echo "research-exit=$?"
```
Expected（起草時 e0c913033c）: `research-exit=0` で 原始研究1（前提なし・木の板 3・木の棒 3・砕いた石材 2）、原始研究2（前提 原始研究1・レンガ 30）、原始研究3（前提 原始研究2）。exit 1（GUID がピン先マスタに無い）か、ピンが動いて名前・前提・素材が違えば、「前提なしの研究 → それを前提にする研究 → さらにそれを前提にする研究」の 3 つを同じ照会で選び直し、Step 2 の GUID と素材を書き換える（`done.md` に書く。D-7）

- [ ] **Step 2: state-label を書き直す**

`misc/research-node-state-label.cs` を次の中身にする（冒頭の「⚠ 現行masterでは動かない」の 2 行を消す）:
```csharp
// 研究ノードカードの状態ラベル（完了済み/研究可能/研究不可）の実表示を撮影する。系に依らない testid なので全体スイッチの両側で流せる
// Capture the research node card state label (Completed/Available/Unavailable) live; the testids are system-independent so it runs on either side
using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;

// ピン先マスタ e0c913033c の研究: 原始研究1（前提なし）→ 原始研究2（前提 1）→ 原始研究3（前提 2）
// Research in the pinned master e0c913033c: primitive 1 (no prerequisite) -> 2 (needs 1) -> 3 (needs 2)
var completed = new Guid("858bcb10-b8ba-478e-9bc5-473ca61281a2");
var available = new Guid("b47c5e3c-1b58-42c5-a477-d485d2eae747");
var unavailable = new Guid("bc5e7786-6759-4271-8095-836703b54490");
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("research-node-state-label", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // ラベルの文言を日本語で見るので、前に流したシナリオの言語に依らず日本語へ固定する
    // The labels are asserted in Japanese, so pin the language regardless of what earlier scenarios left
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "表示言語を日本語に固定");

    // 原始研究1 の素材を渡して完了させ、原始研究2 の素材を渡して研究可能にする（原始研究3 は前提未達で研究不可）
    // Give primitive 1's materials and complete it, then give primitive 2's so it is researchable (primitive 3 stays blocked)
    p.Note("原始研究1を完了させ、原始研究2の素材を渡して3状態を作る");
    await p.GiveItemDirect("木の板", 3);
    await p.GiveItemDirect("木の棒", 3);
    await p.GiveItemDirect("砕いた石材", 2);
    await p.WaitSeconds(1f);
    ClientContext.VanillaApi.SendOnly.CompleteResearch(completed);
    await p.GiveItemDirect("レンガ", 30);
    await p.WaitSeconds(2f);

    // R で研究ツリーを開く
    // Open the research tree with R
    await p.PressKey(Key.R);
    await p.Until(() => p.CurrentUiState == UIStateEnum.ResearchTree, 10f, "研究ツリーへ遷移");

    // 3 状態が描かれるまで待ってから撮る。出ていなければ落ちるので画像が証跡になる
    // Wait for all three labels before capturing, so a missing state fails instead of yielding a hollow screenshot
    await AssertStateLabel(completed, "完了済み");
    await AssertStateLabel(available, "研究可能");
    await AssertStateLabel(unavailable, "研究不可");
    await p.Screenshot("01_research_tree_state_labels");
    p.Assert(p.CurrentUiState == UIStateEnum.ResearchTree, "研究ツリー状態のまま");

    #region Internal

    async UniTask AssertStateLabel(Guid researchGuid, string expectedLabel)
    {
        var deadline = UnityEngine.Time.realtimeSinceStartup + 10f;
        var label = await PlaytestUiElementOps.FindUi($"research-node-state-{researchGuid}");
        while ((!label.IsFound || label.Text != expectedLabel) && UnityEngine.Time.realtimeSinceStartup < deadline)
        {
            await p.WaitSeconds(0.5f);
            label = await PlaytestUiElementOps.FindUi($"research-node-state-{researchGuid}");
        }
        p.Note($"{researchGuid} found={label.IsFound} text={label.Text}");
        p.Assert(label.IsFound && label.Text == expectedLabel, $"{researchGuid} の状態ラベルが「{expectedLabel}」");
    }

    #endregion
});
```
`PlaytestEnvironmentConfig` の名前空間・`GiveItemDirect` の名前は Step 3 の compile で実物に合わせる（元のシナリオと uitk-tutorial-cues の `using Client.Playtest;` で解決する）。

- [ ] **Step 3: 両系で流す**

Run（push してから。経路 M）:
```bash
git push
S=.agents/skills/unity-playmode-recorded-playtest
for sys in GameUi Web; do
  uloop control-play-mode --project-path ./moorestech_client --action Stop
  uloop execute-dynamic-code --project-path ./moorestech_client --code "System.Environment.SetEnvironmentVariable(\"MOORESTECH_PLAYTEST_RECEIVER_BASE\",\"http://127.0.0.1:9\"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.$sys); Client.Localization.Localize.Initialize(); return Client.Localization.Localize.TrySetChosenLanguage(\"japanese\");"
  $S/scripts/run-scenario.sh ./moorestech_client $S/scenarios/misc/research-node-state-label.cs; echo "$sys exit=$?"
done
```
経路 W なら Web の回は `~/bin/winunity play <branch> .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs --ui-system Web`。
Expected: 両方 exit 0・`result.json` `Success: true`・ErrorLogs 0・Assert 5/5（日本語の固定・状態ラベル 3・研究ツリーのまま）。落ちたら Note の `text=` で状態の取り違え（素材の不足で「研究可能」にならない等）かを見て Step 1 の素材を直す（シナリオの誤りは両系とも直して流し直す）。GameUi の画面の不具合なら研究の持ち主（I-fix）へ bd を立てて Task 3 Step 3 の `不具合` と同じく扱う（停止）。GameUi で通り Web だけが 2 回とも落ち、原因がシナリオでなく Web の画面・CEF なら直さず、落ちた assert と原因を `done.md`「## 全録画の結果」の state-label の行に書いて進む（Web 側の失敗は記録だけ。D-36）

- [ ] **Step 4: renumber を消し、台帳の 2 行を書く**

Run:
```bash
git grep -n "tutorial-research-renumber" -- ':!docs/superpowers/plans' ':!docs/uitk-migration'
git rm .agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-research-renumber.cs
```
Expected: grep は台帳の行（`docs/uitk/TODO.md`）と消すファイル自身の 37 行目の 2 件だけ（この 2 つ以外にあれば `done.md`「integrator への依頼」に書く。文書の参照はこのレーンは書き換えない）。`git rm` の後に同じ grep を流し直し、台帳の行の 1 件だけになることを見る。
台帳「## 壊れた録画の割り振り」の 2 行の方針の列を次へ書き換える（直すレーンの列 `X-prerun` は残す）:
- `tutorial-research-renumber.cs`: `削除した（X-prerun。ADR 0038 で研究 1〜3 が消え、役目が終わった）`
- `misc/research-node-state-label.cs`: `書き直した（X-prerun。ピン先マスタの原始研究 1〜3。GameUi run <dir 名> 緑・Web run <dir 名> <緑／落ちた assert と原因〈Web 側は記録だけ〉>）`
- Task 1 Step 3 で結果の無かった他の行（あれば）: `書き直した（<レーン> <コミット 10 桁>。<レーン>/done.md から写した）`。`X-prerun` の語を含めない（D-50）

- [ ] **Step 5: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs docs/uitk/TODO.md
git commit -m "test(uitk): wwxy の残り 2 本を片付ける（renumber を削除・研究の状態ラベルを現行の研究 GUID へ書き直す）（X-prerun）"
```
bd `moorestech-wwxy` に note「10 行すべて片付いた（Task 6 の全録画で確かめてから close）」。

---

### Task 3: 全録画の UITK 側（R-6）

**Files:** なし（結果は gitignore 下と `done.md`）

**Interfaces:**
- Consumes: `$REC/list-recordings.sh`・`$REC/run-all-recordings.sh`（Task 1）
- Produces: `$D`（`moorestech_client/.uloop/outputs/X-prerun/recordings/`）の `{list.txt,list-excluded.txt,list-manual.txt,gameui.tsv,rerun.txt,gameui-rerun.tsv}`、`done.md`「## 全録画の結果」の頭の `- 全録画の HEAD（GameUi）: <40 桁>`（X-switch-pr の `<PRERUN_HEAD>`。控えた時点で書く）

- [ ] **Step 1: 一覧を作り、書き直した state-label を足し、`run-scenario.sh` で流せない行と撮影シナリオを外す**

Run:
```bash
D=moorestech_client/.uloop/outputs/X-prerun/recordings; mkdir -p $D
$REC/list-recordings.sh GameUi > $D/list.txt 2> $D/list-excluded.txt
echo .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs >> $D/list.txt
sort -u -o $D/list.txt $D/list.txt
grep -E 'playtest-receiver/developer-report-(interrupt|resume)\.cs$|uitk/stage5/uitk-event-language-gate\.cs$' $D/list.txt > $D/list-manual.txt
grep -vE 'playtest-receiver/developer-report-(interrupt|resume)\.cs$|uitk/stage5/uitk-event-language-gate\.cs$|/scenarios/uitk/prerun/' $D/list.txt > $D/list.tmp && mv $D/list.tmp $D/list.txt
wc -l $D/list.txt $D/list-manual.txt; cat $D/list-excluded.txt
```
Expected: `list-excluded.txt` に state-label の 1 行だけ（renumber は消えたので出ない。台帳の行の `X-prerun` で外れる。W6-9 でスクリプトは書き換えず一覧へ明示で足す。D-8）。`list-manual.txt` に `run-scenario.sh` では流せない行（起草時の HEAD では `developer-report-interrupt.cs`・`developer-report-resume.cs` の 2 本。`uitk-event-language-gate.cs` は S5-gate の後にできる）。この行は Step 3b で流す（D-48）。`scenarios/uitk/prerun/` は撮影シナリオで全録画の表の外（Task 3 の時点ではまだ無いが、Task 14 Step 3 で同じ手順を流すときに外れるよう、ここで外す。独立評価 3 周目の軽）。`list.txt` と `list-manual.txt` の本数を `done.md` へ

- [ ] **Step 2: 3 本ずつ流す**

Run（前景で。開始行を 3 ずつ進めて最後まで。1 本で 600 秒を超えたシナリオは次から 1 本ずつ）:
```bash
git push
$REC/run-all-recordings.sh GameUi $D/list.txt $D/gameui.tsv 1 3
$REC/run-all-recordings.sh GameUi $D/list.txt $D/gameui.tsv 4 3
```
Expected: 最後に `tail -n +2 $D/gameui.tsv | wc -l` が `list.txt` の本数と同じ。流した HEAD（Run: `git rev-parse HEAD`。40 桁。X-switch-pr の `<PRERUN_HEAD>`）を**控えたその場で** `done.md`「## 全録画の結果」の見出しの直下に `- 全録画の HEAD（GameUi）: <40 桁>` と書き、`../moorestech_logs` でコミット・push する（`$D` が消えても、worktree が `moores-wt rm` で消えた後も残る形にする。以後この HEAD は Run: `PH=$(grep -m1 '全録画の HEAD（GameUi）' ../moorestech_logs/harness/uitk/X-prerun/done.md | grep -oE '[0-9a-f]{40}')` で読む。D-43）

- [ ] **Step 2b: `run-scenario.sh` で流せない行（`list-manual.txt`）を流す（D-48）**

- `playtest-receiver/developer-report-interrupt.cs`（`PlaytestRunner.Run` を呼ばない EDC の手動投入のスニペット。`run-scenario.sh` では run dir も result.json も出ず必ず exit 1）: W0-cef 計画 `docs/superpowers/plans/2026-11-06-uitk-w0-cef.md` Task 4 Step 6 の手動投入（boot を `PlaytestBoot.PrepareAndEnterPlayMode` で行い、`ready.marker` の後にスニペットを投入）で、GameUi の前置きの後に 1 回流す。`Result` が `pending upload interrupted: <箱名>`、または開発者トークンの無い機材で例外文が `Not pending before stop` なら `前提つき（手動投入で PASS）`。manifest の webUiHealth の検査（CEF の生の値 3 つ）の例外で落ちたら `前提つき（5a が assert を書き直す）`（5a は X-5a 計画 Task 8 Step 7 で v7 へ書き直して手動投入で判定する）。それ以外の例外は 1 回流し直し、2 回とも落ちたら Step 3 の `前提つき` の規則で Web 側の同じ行（Task 4 Step 2b）と並べて分ける
- `playtest-receiver/developer-report-resume.cs`（interrupt の対。再生停止前に保存した箱を次の再生で照合し、作者の機材の進捗の箱を読む）: 流さず `前提つき（判定の対象外）`。PASS にも不具合にも数えない（受け口を外した前置きでは本番送信が再開しない。X-5a・X-switch-pr も判定しない）
- `uitk/stage5/uitk-event-language-gate.cs`（あれば。`.cs` を直接流すと出展モードにならない）: 同じディレクトリの運転役 `uitk-event-language-gate.sh` をそのヘッダの手順で流し、`Success: true` なら `前提つき（運転役で PASS）`、2 回とも落ちたら `不具合`
結果を `$D/gameui-manual.tsv`（`scenario system exit seconds run_dir` の形。exit の欄は interrupt が `Result` か例外文・言語ゲートが運転役の exit・resume が `-`）に書く

- [ ] **Step 3: 落ちた行を分ける**

先に exit 0 以外の行だけを 1 回流し直す（揺れで落ちた行を不具合と読まない。D-36）。Run: `awk -F'\t' 'NR>1 && $3!=0 {print $1}' $D/gameui.tsv > $D/rerun.txt; wc -l $D/rerun.txt` → `$REC/run-all-recordings.sh GameUi $D/rerun.txt $D/gameui-rerun.tsv <開始行> 3` を 3 本ずつ前景で。2 回目に通った行は分類を `PASS（揺れ: 1 回目 FAIL）` にする。2 回とも落ちた行ごとに `result.json` と `uloop get-logs --project-path ./moorestech_client --log-type Error` の該当部分を読み、[A-177] の 6 分類に分ける（`前提つき` の行はヘッダの手順の流し直しを 2 回目に数える）:
- `PASS`（0）。S2-ladder の `tutorial-craft-guidance` は Web の回だけ「PASS（Web は UI 未確認・既知）」
- `束 5 未移植`: 0 のはず。出たら `不具合` として扱う（S5 の完了条件の崩れ）
- `wwxy`: 台帳の 10 行のうち現存する 8 本（起草時: S2-ladder の 4 本〈pebble・equip・tree-pin-nearest-search・server-notification-smoke〉・S3-ladder の 3 本〈blueprint-copy-paste・blueprint-right-click-delete・fps-tps-view-toggle〉・state-label）と、行の外の後継 `connect/electric-wire-tool-extend-chain-via-ui.cs` が落ちたら `不具合`。現存の数は台帳の 10 行を 1 行ずつ `find` で「現存／削除／後継」に分けて数え直してから当てる（固定の数を当てない）
- `前提つき`（`playtest-receiver/` のうち `PlaytestRunner.Run` で流れる行・IME の `.sh`。手動投入の 2 本と運転役の言語ゲートは Step 2b）: そのシナリオのヘッダの手順で 1 回流し直し、落ちた assert ごとに次で振り直す（先回りで分類しない。`playtest-receiver/developer-report-delivery.cs` も同じ。D-29）。全部通る → `PASS`。落ちたのが Web に依る assert（`webUiHealth` の欄を見るもの）だけ → `前提つき（5a が assert を書き直す）`（`Web 専用` の文字列を含めない。5a は v7 の assert へ書き直す側で、消さない）。それ以外の assert が GameUi で落ちる → Task 4 の Web 側の同じシナリオの行と並べ（経路 M では Web 側がまだ無いので、この行は Task 4 Step 2 の後に決める）、Web では通るなら `不具合`（下の手順で持ち主へ）、Web でも同じ assert が落ちるなら受け口の前提の崩れなので `前提つき（両系で落ちる: <assert>）` として 5a への申し送りに書き、bd を `--parent <bd>` で 5a 宛てに立てる（実箱ロックの NRE〈C2-b:71「受け口の差し替えの影響か GameUi が原因かは未切り分け」〉はこの比べで切り分ける）
- `Web 専用`（シナリオ全体が CEF・Web の DOM を見るもの。例 `misc/cef-webui-showcase.cs`）: 落ちた assert を `done.md`「## 5a への申し送り」の「### 消す」へ。assert の一部だけが Web に依るシナリオはここに入れない（上の `前提つき` の規則）
- `不具合`（UITK の画面・操作の誤り）: 1 本ずつ bd を `--parent <bd>` で立て、持ち主（その画面のレーン。取り込み済みなら I-fix）への依頼文（シナリオのパス・落ちた assert・Error の行・run dir の録画）を指揮へ返し、`done.md` 1 行目を「状態: 停止（全録画の不具合 n 件の修正待ち）」にして push・`cmux wait-for -S uitk-x-prerun` を 1 回送り、指揮の再開の連絡を待つ。入ったら `git merge --no-ff origin/feature/uitk` で取り込み、compile のあと `不具合` の行だけを `grep` で作り直した一覧で流し直す（前例 S4-gamma Task 9 Step 3。停止の間に Editor を閉じても `$D` は残る。D-43）。撮影（Task 8〜9）の後の取り込み（Task 14 Step 3 の停止からの再開）で `Client.Ui`・`UI Toolkit` が変わっていれば、Task 12 Step 2 の撮り直しの手順も回す（D-45）。シナリオ側の誤り（古い testid・Web の型の参照）なら 5a への申し送りに書いて `Web 専用` か「シナリオの誤り（5a）」に分ける

- [ ] **Step 4: コミットは無し**（Task 5 で記帳）

---

### Task 4: 全録画の Web 側と S4-gamma との比べ（R-7）

**Files:** なし

**Interfaces:**
- Consumes: Task 1 の経路（W か M）・`$S4G`・S4-gamma の `done.md`「## 全録画の結果」の表（シナリオ / UITK の分類 / Web の分類 / 秒）
- Produces: `$D` の `{list-web.txt,list-web-excluded.txt,web.tsv,rerun-web.txt,web-rerun.tsv,web-manual.tsv}`（分類は記録だけ。停止しない）、`done.md`「## 全録画の結果」の頭の `- 全録画の HEAD（Web）: <40 桁>`

- [ ] **Step 1: 一覧**

Run:
```bash
D=moorestech_client/.uloop/outputs/X-prerun/recordings
$REC/list-recordings.sh Web > $D/list-web.txt 2> $D/list-web-excluded.txt
echo .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs >> $D/list-web.txt
sort -u -o $D/list-web.txt $D/list-web.txt
grep -vE 'playtest-receiver/developer-report-(interrupt|resume)\.cs$' $D/list-web.txt > $D/list-web.tmp && mv $D/list-web.tmp $D/list-web.txt
wc -l $D/list-web.txt
```
Expected: `scenarios/uitk/` の全本（`prerun/` を含む）が外れる。手動投入の 2 本は Step 2b で流す（D-48）

- [ ] **Step 2: 流す**

経路 M: Task 3 Step 2 と同じ小分けで `$REC/run-all-recordings.sh Web $D/list-web.txt $D/web.tsv <開始行> 3`。
経路 W（Task 3 と並走してよい。Mac の Editor は使わない）:
```bash
B=$(git rev-parse --abbrev-ref HEAD)
[ -f $D/web.tsv ] || printf "scenario\tsystem\texit\tseconds\trun_dir\n" > $D/web.tsv
tail -n +<開始行> $D/list-web.txt | head -n 3 | while IFS= read -r s; do
  began=$(date +%s); out=$(~/bin/winunity play "$B" "$s" --ui-system Web 2>&1 </dev/null); code=$?
  dir=$(printf '%s\n' "$out" | grep -o '\[mac\] results: .*' | sed 's/^\[mac\] results: //')
  printf "%s\tWeb\t%s\t%s\t%s\n" "$s" "$code" "$(( $(date +%s) - began ))" "${dir:--}" >> $D/web.tsv
done
```
（winunity は 1 本ごとに Windows の Editor に受け口と系を立てる。1 回の呼び出しが 600 秒を超えるなら 1 本ずつ。`~/bin/winunity` の中の `ssh` は標準入力を閉じないので、`</dev/null` が無いとパイプの残りの行を ssh が読み切って 1 本しか流れない。D-21）
Expected: `web.tsv` の行数が `list-web.txt` の本数と同じ。続けて exit 0 以外の行だけの一覧（Run: `awk -F'\t' 'NR>1 && $3!=0 {print $1}' $D/web.tsv > $D/rerun-web.txt`）を同じ経路でもう 1 回流し（出力 `$D/web-rerun.tsv`）、2 回目に通った行は `PASS（揺れ: 1 回目 FAIL）`、2 回とも落ちた行だけを Step 3 へ回す（D-36）。流した HEAD の 40 桁を控えたその場で `done.md`「## 全録画の結果」の頭に `- 全録画の HEAD（Web）: <40 桁>` と書いて logs でコミット・push する（D-43）

- [ ] **Step 2b: 手動投入の 2 本（Web。D-48）**

`developer-report-interrupt.cs` は経路に依らず Mac の Editor で、Web の前置き（Global Constraints 5 の `UiSystem.Web`）の後に Task 3 Step 2b と同じ手動投入で 1 回流す（Windows の Editor へ EDC のスニペットを投入する手順は持たない）。判定の語は Task 3 Step 2b と同じで、Web 側も失敗は記録だけ（D-36）。`developer-report-resume.cs` は流さず `前提つき（判定の対象外）`。結果を `$D/web-manual.tsv` へ。UITK 側で `前提つき（5a が assert を書き直す）` 以外の例外で 2 回とも落ちた interrupt は、ここで Web も同じ例外なら `前提つき（両系で落ちる: <例外文>）`、Web で通るなら `不具合`（Task 3 Step 3 の規則）

- [ ] **Step 3: S4-gamma の Web 側と比べる（記録だけ。停止しない）**

Web 側の失敗は直さず、どの分類でも停止しない（裁定「UITK 切替まで CEF 由来の不具合は直さない」と、Web が切替で消えること。CEF 由来か否かを問わず撤去前の基準として記録する。A-251・D-36）。停止の対象は Task 3 の UITK 側の `不具合` だけ。2 回とも落ちた行を、S4-gamma の表の Web の分類と並べる:
- S4-gamma でも落ちていた → `既存`（S4-gamma の分類を写す）
- S4-gamma に無いシナリオ → `比較不能（新設）`
- S4-gamma にあり、`git diff --quiet $S4G HEAD -- <シナリオ>` が差ありを返す → `比較不能（変更）`（変えたレーンの PR の報告ページで見たことを書く）
- S4-gamma で PASS・中身も同じ → `$S4G` で 2 回まで流し直す（1 回でも通れば `退行`、2 回とも落ちれば `既存`）:
```bash
git push origin "$S4G":refs/heads/uitk-tmp/prerun-s4g-base
~/bin/winunity play uitk-tmp/prerun-s4g-base <シナリオ> --ui-system Web </dev/null; echo "exit=$?"
```
Windows で CEF が描けない（経路 M）なら、**この流し直しは本体（制御セッション）が行う**（Editor の付け替えを伴う。Global Constraints 3。subagent はここで本体へ返す）。本体は自分の Editor を閉じ（`moores-wt status` で自分のものと確かめる）、`moores-wt new uitk-tmp/prerun-s4g-base --from "$S4G"`（worktree の名は既定でブランチ名の最後の段 `prerun-s4g-base`）の Editor で、録画の前に Task 1 Step 6 と同じ 2 行を一時の worktree へ向けて流し（`T=~/hermes-agent/data/worktrees/moorestech/prerun-s4g-base; test -d $T/moorestech_web/node || cp -Rc ~/hermes-agent/data/repos/moorestech/moorestech_web/node $T/moorestech_web/node; (cd $T/moorestech_web/webui && pnpm install --frozen-lockfile)`。抜かすと録画が ready の後で止まり、`退行` を `既存` と読み違える。`docs/development/worktree-and-review-pitfalls.md`）、前置きも一時の worktree へ向けて流す:
```bash
uloop execute-dynamic-code --project-path $T/moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.Web); Client.Localization.Localize.Initialize(); return Client.Localization.Localize.TrySetChosenLanguage("japanese");'
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh $T/moorestech_client $T/<シナリオのパス>; echo "exit=$?"
```
戻り値 `true` を見てから流す。流し終えたら `moores-wt rm prerun-s4g-base --force --prune-branch`（`rm` は worktree の名かパスで引く。ブランチ名では「worktreeが見つかりません」で止まる）で畳み、自分の Editor を `uloop launch ./moorestech_client` で起こし直して前置き（Global Constraints 5）を流す（D-6）。
どの行も、ログ（`result.json` の `ErrorLogs`・`log-excerpt.txt`・`uloop get-logs`）に CEF の描画停止・待ち（`RendererUnavailable`・CEF の待ちの時間切れ）があれば分類の後に `（CEF 由来）` を付け、落ちた assert と原因の 1 行を書く。`退行` は件数と名前を Task 5 の台帳の行と `done.md` に書き、`done.md`「## 未確認」に「Web 側の退行 n 件（記録だけ。CEF 由来でないものは裁定により master の Web の不具合として扱う範囲で、このレーンは直さない）」を書く。bd も停止もしない。流し終えたら `git push origin --delete uitk-tmp/prerun-s4g-base`

- [ ] **Step 4: コミットは無し**

---

### Task 5: 全録画の記帳と 5a への申し送り（R-5・R-8・R-16）

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 実測の結論」の末尾 2 行）

- [ ] **Step 1: done.md に書く**

`done.md`「## 全録画の結果」の見出しの直下の `- 全録画の HEAD（GameUi）: <40 桁>`（Task 3 Step 2 で書いた行。Task 3 の途中で不具合の修正を取り込んで流し直した行があれば、その行の秒の列の後に `再走 <HEAD 10 桁>`。Task 14 Step 3 で流し直したら、その HEAD に替える。X-switch-pr の `<PRERUN_HEAD>`）と `- 全録画の HEAD（Web）: <40 桁>`（Task 4 Step 2 で書いた行）があることを確かめ、2 つの TSV（と流し直しの TSV・手動投入の `gameui-manual.tsv`・`web-manual.tsv`）を「シナリオ / UITK の分類 / Web の分類 / 秒 / 原因・注記」の表で写す（run dir は書かない）。UITK の分類と Web の分類の欄は分類の語だけを書く（`PASS`・`PASS（揺れ: 1 回目 FAIL）`・`束 5 未移植`・`wwxy`・`前提つき（…）`・`Web 専用`・`不具合`・`シナリオの誤り（5a）`、Web は加えて `既存`・`退行`・`比較不能（新設）`・`比較不能（変更）` と `（CEF 由来）` の印）。とくに `Web 専用` の行の UITK の分類の欄はちょうど `Web 専用` にし、注記・落ちた assert・原因は「原因・注記」の列へ書く（X-5a Task 1 Step 4b は `awk -F'|' '$3 ~ /^ *Web 専用 *$/'` で欄が `Web 専用` ちょうどの行を拾う）。シナリオの列は `scenarios/` からの相対（例 `misc/cef-webui-showcase.cs`。TSV の `.agents/skills/unity-playmode-recorded-playtest/scenarios/` を `sed 's#^\.agents/skills/unity-playmode-recorded-playtest/scenarios/##'` で外す）にし、「## 5a への申し送り」の 2 つの小節のパスも同じ形で書く（5a は小節のパスを `grep -oE '[A-Za-z0-9_./-]+\.(cs|sh)'` で拾って表の 2 列目と突き合わせる）。これが撤去前の最後の Web の基準（A-251）。`## 5a への申し送り` を「### 消す（シナリオ全体が Web 専用）」と「### 書き直す（Web に依る assert・型の参照だけを持つ）」の 2 つの小節に分け、前者に `Web 専用` の全行、後者に `前提つき（5a が assert を書き直す）`・`前提つき（両系で落ちる: …）`・「シナリオの誤り（5a）」の行と `Client.WebUiHost`／`PlaytestDomQuery`／`IsWebUiAvailable`／`Cef*` を参照するシナリオ（Run: `grep -rlE "WebUiHost|PlaytestDomQuery|IsWebUiAvailable|CefUnity|CefScreenMapper|CefInputForwarder" .agents/skills/unity-playmode-recorded-playtest/scenarios | sed 's#^\.agents/skills/unity-playmode-recorded-playtest/scenarios/##'`）を「パス / 理由 / 5a の案」で並べる。並べた後、小節の全パスが表の 2 列目にあることを Run: `python3 -c 'import re,sys;t=open(sys.argv[1],encoding="utf-8").read();h=t.split("## 5a への申し送り",1)[1].split("\n## ",1)[0].split("\n### 物差し",1)[0];r=t.split("## 全録画の結果",1)[1].split("\n## ",1)[0];rows={l.split("|")[1].strip() for l in r.splitlines() if l.startswith("| ")};miss=[x for x in re.findall(r"[A-Za-z0-9_./-]+\.(?:cs|sh)",h) if x not in rows and not x.startswith("uitk/prerun/")];print("表に無いパス",miss);sys.exit(1 if miss else 0)' ../moorestech_logs/harness/uitk/X-prerun/done.md` で確かめる（exit 0。物差し `uitk/prerun/` は表の外。Task 10 Step 3 が足す「### 物差し」の小節は見ない）。「## 全録画の結果」の分類の列で文字列 `Web 専用` を含むのは前者の行だけであることを Run: `sed -n '/^## 全録画の結果/,/^## /p' ../moorestech_logs/harness/uitk/X-prerun/done.md | grep "Web 専用"` で確かめる（5a の Task 1 Step 4 がこの grep で消す一覧を取るため。「### 消す」以外の行〈Web の列に写した S4-gamma の分類を含む〉が当たったら、その行の語を `Web 専用` を含まない語に替える。D-29）。続けて、UITK の分類の欄がちょうど `Web 専用` の行の数と「### 消す」のパスの数が同じことを Run: `X=../moorestech_logs/harness/uitk/X-prerun/done.md; a=$(sed -n '/^## 全録画の結果/,/^## /p' $X | awk -F'|' '$3 ~ /^ *Web 専用 *$/' | wc -l); b=$(awk '/^### 消す/{f=1;next} /^##/{f=0} f' $X | grep -oE '[A-Za-z0-9_./-]+\.(cs|sh)' | sort -u | wc -l); echo "表 $a / 消す $b"; [ "$a" -eq "$b" ]; echo "exit=$?"` で見る（exit 0。X-5a Task 1 Step 4b の 2 つの抜き方と同じ式。違えば欄の注記を「原因・注記」の列へ移すか、「### 消す」の漏れを足す）。wwxy の 10 行を 1 行ずつ「現存／削除／後継」に分けて数え直し（Run: 台帳の各行のパスを `find .agents/skills/unity-playmode-recorded-playtest/scenarios -path "*<パス>"`。起草時は現存 8・削除 2〈electric-wire・renumber〉・後継 1〈extend-chain〉）、現存の全本と後継の結果を書き、全部 PASS なら bd `moorestech-wwxy` に note（close は Task 16）

- [ ] **Step 2: 台帳の 2 行**

「## 実測の結論」の末尾に:
```markdown
- 全録画の 3 回目・UITK 側（X-prerun、<HEAD 10 桁>、全体スイッチ GameUi・撤去の前）: <n> 本中 PASS <a>・前提つき（流し直して PASS）<c>・Web 専用 <d>・束 5 未移植 0・不具合 0（途中で見つけた <k> 件は bd <ids> で直して再走 PASS）。結果の表は `harness/uitk/X-prerun/done.md`「## 全録画の結果」
- 全録画の 3 回目・Web 側（X-prerun、<HEAD 10 桁>、`scenarios/uitk/` は除く・撤去の前の最後の Web 側）: <n> 本中 PASS <a>・既存の失敗 <e>（S4-gamma <S4G> でも落ちる: <シナリオ名>）・比較不能（新設）<f>・比較不能（変更）<g>・退行 <h>（<シナリオ名と CEF 由来か>。Web 側の失敗は記録だけで直さない。裁定 2026-11-06「UITK 切替まで CEF 由来の不具合は直さない」）・揺れ（2 回目に PASS）<i>
```
UITK 側の行の `<HEAD 10 桁>` は `done.md` の `全録画の HEAD（GameUi）` の先頭 10 桁（Task 14 Step 3 で流し直したら、その行をこの形で 1 行足す）。
`Saves/` に作ったワールドを `.agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で探して消す

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 撤去の前の全録画（UITK 側・Web 側）を台帳に記録する（X-prerun）"
```

---

### Task 6: 全画面の参照の撮り直し（R-9。Editor 不要・Task 3〜4 と並走してよい）

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 参照キャプチャ」）
- Create（repo 外）: `../moorestech_logs/uitk/reference-captures/web-<sha10>/`・`web-<sha10>-2560x1080/`

- [ ] **Step 1: master の使い捨て worktree**

Run:
```bash
L=$(cd ../moorestech_logs && pwd -P)/uitk/reference-captures; echo $L
moores-wt new uitk-tmp/prerun-ref --dir uitk-prerun-ref --from origin/master --no-editor --fetch
R=~/hermes-agent/data/worktrees/moorestech/uitk-prerun-ref
test -d $R/moorestech_web/node || cp -Rc ~/hermes-agent/data/repos/moorestech/moorestech_web/node $R/moorestech_web/node
cd $R/moorestech_web/webui && pnpm install --frozen-lockfile && pnpm build
SHA=$(git -C $R rev-parse --short=10 HEAD); echo $SHA
```
Expected: `L` が `moorestech_logs` の実体の絶対パス（レーンの worktree の中で決める。`capture.ts:19-22` は `UITK_REFERENCE_OUT` を cwd 相対で `mkdir` するので、相対のまま `cd` すると参照が使い捨て worktree の中にできて Step 4 の `moores-wt rm` で消える。D-22）・`pnpm build` が 0。`L`・`R`・`SHA` は Task 7・9・11 でも使うので `done.md`「## 前提」に値を書く。`moores-wt` が非ログインシェルで失敗したら `docs/development/macmini/worktree-and-automation.md` の手順で流し直す

- [ ] **Step 2: 1280×720（`capture.ts` は本体が background で 1 回起動する）**

`capture.ts` は 1 回の呼び出しで静止画 84 件（撮るたびに mock-host を起こし直す）と動画 5 件を撮り、出力先を分ける口も id で絞る口も無い（`capture.ts:16-63`）ので、1 回の Bash の 600 秒（Global Constraints 4）を超えうる（所要は未実測）。分けられないコマンドなので、**本体（制御セッション）が Bash の `run_in_background` で 1 回起動し、完了の通知で受ける**（ポーリングしない。subagent はここで本体へ返す）。`capture.ts:22` は出力先が空でないと `output directory is not empty` で止まるので、流し直す前は必ず出力先を消す。
Run（本体・background）:
```bash
case "$L" in /*) ;; *) echo "L が絶対パスでない: $L"; exit 1;; esac
cd $R/moorestech_web/webui
rm -rf "$L/web-$SHA"
began=$(date +%s); UITK_REFERENCE_OUT=$L/web-$SHA UITK_REFERENCE_PORT=5791 node --import tsx e2e/uitk-reference/capture.ts; echo "capture-exit=$? seconds=$(( $(date +%s) - began ))"
```
完了の通知を受けたら Run: `python3 -c 'import json,sys;m=json.load(open(sys.argv[1]));print(m["sourceCommit"],m["viewport"],len(m["entries"]),sum(e["kind"]=="video" for e in m["entries"]))' $L/web-$SHA/manifest.json`
Expected: `capture-exit=0`・`<SHA> 1280x720 <n> 5`（起草時は 89 件・動画 5）。所要の秒を `done.md`「## 証拠」に書く。ポートが塞がっていれば別の空きポートで、出力先を消してから流し直す

- [ ] **Step 3: 2560×1080（W6-4。`capture.ts` は Step 2 と同じく本体が background で）**

Run（前景）:
```bash
cd $R/moorestech_web/webui
sed -i '' 's/export const referenceViewport = { width: 1280, height: 720 } as const;/export const referenceViewport = { width: 2560, height: 1080 } as const;/' e2e/uitk-reference/shotRunner.ts
grep -n "referenceViewport =" e2e/uitk-reference/shotRunner.ts
pnpm build
```
Run（本体・background）:
```bash
cd $R/moorestech_web/webui
rm -rf "$L/web-$SHA-2560x1080"
began=$(date +%s); UITK_REFERENCE_OUT=$L/web-$SHA-2560x1080 UITK_REFERENCE_PORT=5791 node --import tsx e2e/uitk-reference/capture.ts; echo "capture-exit=$? seconds=$(( $(date +%s) - began ))"
```
完了の通知を受けたら Run:
```bash
python3 -c 'import json,sys;m=json.load(open(sys.argv[1]));print(m["viewport"],len(m["entries"]))' $L/web-$SHA-2560x1080/manifest.json
git -C $R checkout -- moorestech_web/webui/e2e/uitk-reference/shotRunner.ts
```
Expected: grep が `2560, height: 1080`・`capture-exit=0`・manifest が `2560x1080 <n>`（Step 2 と同じ件数）・checkout で書き換えが消える（コミットしない）。所要の秒を `done.md`「## 証拠」に書く

- [ ] **Step 4: 目で見る・logs へ入れる（worktree は Task 7 Step 4 まで残す）**

`web-$SHA/` の静止画を 5 枚（束ごとに 1 枚）開き、白画面・mock-host の起動失敗の画面でないことを見る。`../moorestech_logs/README.md` を読み、`uitk/reference-captures/web-$SHA/`・`web-$SHA-2560x1080/` を手でコミット・push する（`git log origin/main..main --stat` で押し出すものを確かめる）。Run: `ls $L/web-$SHA/manifest.json $L/web-$SHA-2560x1080/manifest.json` が 2 つとも出ることを見る（無ければ、参照がどこにできたかを `find $R -name manifest.json -newer $R/moorestech_web/webui/package.json` で探して `$L` へ移す）。使い捨て worktree は E の面の矩形を測る（Task 7 Step 4）まで畳まない（D-46）

- [ ] **Step 5: 台帳に書いてコミット**

「## 参照キャプチャ」は台帳の規則（節の末尾の行「撮り直したら、ここの撮影元と置き場を新しい方へ書き換える」）に従い、行を足すのではなく上の 4 行を書き換える（足すだけだと「撮影元」の行に古い版が残り、撤去の後に台帳を読む人が古い組を引く）。起草時の `docs/uitk/TODO.md:135-139` の「撮影元」「置き場」「URL」「2560×1080 の参照」の 4 行を次の 3 行に替え、「目視検査」の行は前の組の行の中へ移す（レーンの自撮り・束 3 の最新・1920×1080 の行と末尾の規則の行は残す）:
```markdown
- 撮影元: moorestech `<SHA>`（origin/master。撤去の前の最後の全画面の参照。X-prerun）。撮り方: `moorestech_web/webui/e2e/uitk-reference/capture.ts`（撮影表は同じディレクトリの `referenceShots.ts`・`referenceVideos.ts`）。切替後はこの組が Web 版の見た目の最後の正本
- 置き場: `moorestech_logs` の `uitk/reference-captures/web-<SHA>/`（<n> 件。一覧は `index.md`、機械向けは `manifest.json`。https://github.com/moorestech/moorestech_logs/tree/main/uitk/reference-captures/web-<SHA> ）、2560×1080 は `uitk/reference-captures/web-<SHA>-2560x1080/`（同じ件数。`shotRunner.ts` の `referenceViewport` を使い捨ての worktree の中だけで書き換えて撮った）。参照と `feature/uitk` の Web の食い違いは `harness/uitk/X-prerun/done.md`「## 参照と feature/uitk の Web の食い違い」
- 前の組（履歴）: 1280×720 は撮影元 `a9d5123537`・`uitk/reference-captures/web-a9d5123537/`（目視検査は同じ置き場の `visual-review.md` と `qa/`。全64静止画・全5動画）、2560×1080 は撮影元 `0e81bbe3c3`・`uitk/reference-captures/web-0e81bbe3c3-2560x1080/`
```
書き換えた後に Run: `awk '/^## 参照キャプチャ/{f=1;next} /^## /{f=0} f' docs/uitk/TODO.md | grep -n "^- 撮影元:"` で、「撮影元:」で始まる行が `<SHA>` の 1 行だけ（`a9d5123537` は「前の組（履歴）」の行の中だけ）であることを見る。
```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 撤去の前の全画面の参照を撮り直して台帳に書く（X-prerun）"
```

---

### Task 7: 撮影の対応表と機械検査（R-10）

**Files:** なし（`done.md`）

**Interfaces:**
- Consumes: Task 6 の `web-<SHA>/manifest.json`、上の「## 撮影の対応表（起草時の案）」、各レーンの `done.md`
- Produces: `done.md`「## 撮影の対応表」（列: `| id | 束 | 撮り方 | 出所 | 出力名 | 判定 |`。id は 1 行に 1 つ。`…` で省略しない。E の行の出所に `面=<testid>`）、`$C/states.md`、`$C/e-panels.json`・`$C/web-panel-rects-{1280x720,2560x1080}.json`（Web の面の矩形。Playwright の `boundingBox` の `x`・`y`・`width`・`height`）

- [ ] **Step 1: 起草時の案を新しい manifest の id で 1 行ずつに展開する**

新しい manifest の `entries` を順に読み、起草時の案の行（まとめて書いた行は 1 id ずつに割る）から撮り方を写す。新しく増えた id は、同じ `ledgerRow` の既存の行の撮り方を初期値にする。E の行は「前例」列のレーンの `done.md` を `grep -n "<id>\|UiRuntimePanelCapture\|EditMode" ../moorestech_logs/harness/uitk/<レーン>/done.md` で読み、そのレーンが使った外殻と偽の Model の組み方（型名・見本の名前）を「出所」列に写す。見つからなければ「前例なし」と書き、Task 9 Step 2 で組み方を決めて書く。E の行の「出所」列には、比べる面（外殻の一番外の要素）の testid を `面=<testid>` で必ず書く（Web と UITK で同じ名前の要素。例 ブロックは `面=block-inventory-panel`。Web は `referenceShots.ts` のその id の `steps` の testid と Web の DOM、UITK は外殻の要素の `name` で確かめる。Task 7 Step 4 と Task 11 Step 1 がこの面の矩形で両側を切り抜く。D-46）。P の出所のシナリオが実在しない行（Run: `ls <パス>`）は N に切り替える。X の行は `grep -n "再接続" docs/uitk/disposition.md` で S5-crosscut の理由の行を確かめて出所に行番号を書く（無ければ X のまま `done.md`「integrator への依頼」に「S5-crosscut の disposition の行の欠け」と書く）。新しく増えた id が [A-38] の画面（辞書エラー等）なら X にする。それ以外を X にしない

- [ ] **Step 2: 機械で確かめる**

Run:
```bash
python3 - "$L/web-$SHA/manifest.json" ../moorestech_logs/harness/uitk/X-prerun/done.md <<'EOF'
import json, re, sys
ids = [e["id"] for e in json.load(open(sys.argv[1]))["entries"]]
text = open(sys.argv[2], encoding="utf-8").read()
section = text.split("## 撮影の対応表", 1)[1].split("\n## ", 1)[0]
rows = [l.split("|")[1].strip() for l in section.splitlines() if l.startswith("| ") and not l.startswith("| id ") and not l.startswith("|---")]
dups = sorted({r for r in rows if rows.count(r) > 1})
missing = [i for i in ids if i not in rows]
extra = [r for r in rows if r not in ids]
kinds = [l.split("|")[3].strip() for l in section.splitlines() if l.startswith("| ") and not l.startswith("| id ") and not l.startswith("|---")]
bad = [k for k in kinds if k not in ("P", "N", "E", "V", "X")]
xrows = [l for l in section.splitlines() if l.startswith("| ") and l.split("|")[3].strip() == "X"]
bad += [l.split("|")[1].strip() + "(X の根拠なし)" for l in xrows if "A-38" not in l or "disposition" not in l]
erows = [l for l in section.splitlines() if l.startswith("| ") and l.split("|")[3].strip() == "E"]
bad += [l.split("|")[1].strip() + "(E の面なし)" for l in erows if not re.search(r"面=[A-Za-z0-9_-]+", l.split("|")[4])]
print(f"manifest {len(ids)} / 表 {len(rows)} / 抜け {missing} / 余り {extra} / 重複 {dups} / 撮り方の誤り {bad}")
sys.exit(1 if missing or extra or dups or bad else 0)
EOF
echo "exit=$?"
```
Expected: `manifest <n> / 表 <n> / 抜け [] / 余り [] / 重複 [] / 撮り方の誤り []`・exit 0

- [ ] **Step 3: 状態の差と、参照と feature/uitk の Web の食い違いを書き出す**

参照は mock-host の高密度の fixture で、実ゲームの撮影とデータの状態が違う。P・N・E・V の行ごとに状態の差を `moorestech_client/.uloop/outputs/compare/X-prerun/states.md` に先に書き出す（前例 S4-gamma Task 13 Step 2b）。並べ比べでは状態に依らない観点（配置・寸法・色の系統・装飾の有無・文字の大きさと段組・余白）だけを比べる（D-5）。
- P と N: 行数・アイテム・名前・選択・スクロール・世界の地。実ゲームではチャレンジ 1 のワールドピンが常に出ている（画面内ならマーカー、画面外なら画面端の矢印。`uitk/uitk-tutorial-cues.cs:47` が待つ）が、mock の静止画には無い。ゲーム画面が写る id には「ワールドピンのマーカー・矢印は状態の差」と書く
- P・N・V（ゲーム画面の HUD が写る全 id。少なくとも P の `2-game-hud` 系、N の `2-train-hud`、V の `2-key-control-hint-pulse`・`2-world-pin-pulse`・`5-tutorial-pulse`、N の `5-tutorial-outline-label`・`5-tutorial-research-node`）: 実ゲームではチャレンジ 1 のチュートリアル `uiHighLight` の枠とラベルが常に出ている（anchor `challenge.current-hud`、ラベル「左上で現在の目標を確認する」。ピン先マスタ e0c913033c の `challenges.json` の 1 件目のチャレンジの `tutorials[1]`。前例 `uitk/uitk-tutorial-cues.cs:123-124` のコメントと S2-tutorial `done.md` の「チャレンジ 1 の本物の枠…スクショ 04 に枠が 2 つ写っていた」）。mock の参照には無い（`2-game-hud` の controls は `crosshairVisible`・`challengeActive`・`GameScreen` だけ。`referenceShots.ts:81`）。これらの id に「チャレンジ 1 の `challenge.current-hud` の枠とラベル（実ゲームだけ）は状態の差」と名指しで書く。ピンが動いて 1 件目のチャレンジの `uiHighLight` が変わっていれば、照会の結果の anchor とラベルで書く（`PIN` は Task 2 Step 1 の式。Run: `git -C ../moorestech_master show "$PIN":server_v8/mods/moorestechAlphaMod_8/master/challenges.json | python3 -c 'import json,sys;c=json.load(sys.stdin)["data"][0]["challenges"][0];print([t["tutorialParam"] for t in c["tutorials"] if t["tutorialType"]=="uiHighLight"])'`。D-44）
- E: 外殻に渡した偽の Model の値（前例のレーンの見本の名前）と mock の fixture の値の差。加えて E の行の頭に次を書く: 「E は外殻（画面の面）だけを単色の地に描く（`UiRuntimePanelCapture.Render` は単色の地に content を 1 つ載せるだけ）。比べる範囲は面の矩形（対応表の `面=`）に両側とも切り抜き、位置は面の原点からの相対で見る（前例 S4-special `done.md`「EditMode の器の一致…panel 原点からの相対で比べて」）。外殻の外（常時 HUD・チャレンジ HUD・キー案内・持ち物などの他の列・ホットバー・世界の地）は E では写らないので数えない（前例 S3-ladder「絵・地・別の束の HUD｜判定外」・S4-research「撮影の口が画面だけを載せるため…捨てる」・S3-buildmenu「ビルドメニューの面だけを比べた」）。面の画面上の絶対位置も数えない（外殻の列の組み方で変わる。S4-alpha `done.md`「UITK 本番は Inv 列が空で x=56」）」（D-46）
- V（5 件。D-35）: Web の webm の t=0 は「ready の手順 → 緑の印 400ms（`MARKER_HOLD_MS`）」の後の境界で（`videoCapture.ts:19-26`）、`2-key-control-hint-pulse`・`2-world-pin-pulse`・`4-drag-guide`・`5-tutorial-pulse` は controls の段で要素が出ているので t=0 で脈動の途中、UITK は `Mark` の後に出し始めるので効果の出始め。`2-notification-enter-aggregate-exit` は Web が境界の 0.5 秒後（`EFFECT_LEAD_IN_MS`）に最初の通知、UITK は境界で通知（全相が 0.5 秒ずれる）。`2-world-pin-pulse` は Web が `/__worldpin?on=0&dx=1&dy=0.3`（右寄りの矢印）、UITK は振り向いた結果の向き（矢印の位置が違う）。どの動画も実ゲームのチャレンジ 1 のワールドピンが写りうる。脈動の振幅と周期（1→1.08・DragGuide の不透明の区間）は `uitk/uitk-tutorial-cues.cs` の Assert（Task 3 の全録画）と `^Client\.Tests\.Ui\.Screens\.Tutorial\.` の UI テスト（`TutorialAttentionPulseTest` ほか）が見るので、並べ比べでは動きの位相を数えない

続けて、参照の撮影元（`$SHA`。撮影した origin/master）と `feature/uitk` の Web の食い違いを洗い出す（D-37）。Run:
```bash
git diff --stat "$SHA" HEAD -- moorestech_web/webui/src
git log --no-merges --format='%h %s' "$SHA"..HEAD -- moorestech_web/webui/src
git log --no-merges --format='%h %s' HEAD.."$SHA" -- moorestech_web/webui/src
awk '/^## 追従記録/{f=1;next} /^## /{f=0} f' docs/uitk/TODO.md | grep -E '\| (写した|捨てた) \|'
```
Expected（起草時の模擬。`9c83052273` と `c2fc93f7f2`）: 差のあるファイルは `src/app/tokens.css`・`features/pauseMenu/style.module.css`（I-fix2 ad2ff7d2ae。台帳の追従記録に束 1 `写した`）と生成物 `src/shared/i18n/generated/localizationKeys.ts`（見た目に効かない）。master にだけある Web のコミット（`HEAD.."$SHA"`）は起草時 3 本で、Revert で打ち消し合って正味 0。
差のあるファイルごとに、効く撮影 id（tokens.css なら変わったトークンを使う画面。例 `--bug-report-textarea-width` は `1-bug-report`）を `states.md` の末尾の「## 参照と feature/uitk の Web の食い違い」と `done.md` の同じ名前の節に「ファイル / コミット / 効く id / 区分」で書く。区分は 2 つ:
- `feature/uitk` 側のコミット（`"$SHA"..HEAD`）による差 → 「feature/uitk 側が正（master の Web 側の残り）」。並べ比べの差から外し、UITK を参照へ寄せない（`Tokens.uss` など生成物を手で戻さない）。起草時は `1-bug-report` の記述欄の幅（参照 356px で右枠が切れる・UITK 354px）
- master にだけあるコミット（`HEAD.."$SHA"`）による差 → 「master の未追従（I-follow で写す）」。このレーンは UITK を寄せず、`done.md`「integrator への依頼」に「X-switch-pr Task 2 Step 4 の I-follow で写す」とコミットと id を書く（台帳「## 追従記録」の記帳は取り込みの I-follow が行う）

- [ ] **Step 4: Web の面の矩形を測り、使い捨て worktree を畳む（E の組の切り抜き。D-46）**

参照の manifest は矩形を持たない（`entries` は `id`・`bundle`・`ledgerRow`・`file`・`kind` だけ）ので、E の id ごとに参照と同じ手順（`openShot`）で開いた Web の面の矩形を、Task 6 の使い捨て worktree の中に置く一時のスクリプトで測る（コミットしない。W6-4 の `shotRunner.ts` の一時の書き換えと同じ扱い）。Run:
```bash
CA=$(mkdir -p moorestech_client/.uloop/outputs/compare/X-prerun && cd moorestech_client/.uloop/outputs/compare/X-prerun && pwd -P)
python3 - ../moorestech_logs/harness/uitk/X-prerun/done.md > $CA/e-panels.json <<'EOF'
import json, re, sys
t = open(sys.argv[1], encoding="utf-8").read().split("## 撮影の対応表", 1)[1].split("\n## ", 1)[0]
rows = [l for l in t.splitlines() if l.startswith("| ") and l.split("|")[3].strip() == "E"]
print(json.dumps({l.split("|")[1].strip(): re.search(r"面=([A-Za-z0-9_-]+)", l.split("|")[4]).group(1) for l in rows}))
EOF
cat > $R/moorestech_web/webui/e2e/uitk-reference/prerun-panel-rects.ts <<'EOF'
// X-prerun の E の組の切り抜き用に、参照と同じ手順で開いた面の矩形を測る（使い捨て worktree の中だけ。コミットしない）
// Measure panel rects for X-prerun's E pairs after opening each shot like the reference (disposable worktree only; never committed)
import { readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { chromium } from "@playwright/test";
import { referenceShots } from "./referenceShots";
import { openShot, startMockHost, stopMockHost } from "./shotRunner";

const webuiRoot = fileURLToPath(new URL("../..", import.meta.url));
const port = Number(process.env.UITK_REFERENCE_PORT);
const [width, height] = (process.env.PRERUN_VIEWPORT ?? "").split("x").map(Number);

async function main(): Promise<void> {
  const targets: Record<string, string> = JSON.parse(await readFile(process.env.PRERUN_PANEL_TESTIDS ?? "", "utf8"));
  const rects: Record<string, unknown> = {};
  const browser = await chromium.launch();
  for (const shot of referenceShots.filter((s) => s.id in targets)) {
    const host = await startMockHost(webuiRoot, port);
    const context = await browser.newContext({ viewport: { width, height } });
    const page = await context.newPage();
    await openShot(page, `http://127.0.0.1:${port}`, shot);
    rects[shot.id] = await page.getByTestId(targets[shot.id]).first().boundingBox();
    await context.close();
    await stopMockHost(host);
  }
  await browser.close();
  await writeFile(process.env.PRERUN_PANEL_RECTS_OUT ?? "", JSON.stringify(rects, null, 2));
}

void main().catch((error: unknown) => {
  console.error(error);
  process.exitCode = 1;
});
EOF
cd $R/moorestech_web/webui
for vp in 1280x720 2560x1080; do UITK_REFERENCE_PORT=5791 PRERUN_VIEWPORT=$vp PRERUN_PANEL_TESTIDS=$CA/e-panels.json PRERUN_PANEL_RECTS_OUT=$CA/web-panel-rects-$vp.json node --import tsx e2e/uitk-reference/prerun-panel-rects.ts; echo "$vp exit=$?"; done
python3 - $CA <<'EOF'
import json, sys
want = set(json.load(open(f"{sys.argv[1]}/e-panels.json")))
for vp in ("1280x720", "2560x1080"):
    got = json.load(open(f"{sys.argv[1]}/web-panel-rects-{vp}.json"))
    print(vp, "抜け", sorted(want - set(got)), "矩形なし", sorted(k for k, v in got.items() if v is None))
EOF
```
Expected: 2 回とも `exit=0`、どちらの解像度も `抜け []`・`矩形なし []`。抜けは `referenceShots.ts` に無い id（E の id は全部参照にある）、矩形なしは testid の誤りなので、対応表の `面=` を直して流し直す。所要が 600 秒を超えるなら Task 6 Step 2 と同じく本体が background で流す。測り終えたら Run: `git -C $R status --short`（`prerun-panel-rects.ts` の 1 本だけが未追跡）→ `moores-wt rm uitk-prerun-ref --force --prune-branch`（`--dir` で付けた worktree の名で引く。ブランチ名 `uitk-tmp/prerun-ref` では `resolve_worktree` が「worktreeが見つかりません」で止まる。未参照の master ピンが報告されたら `--prune-master` を足して流し直す）

- [ ] **Step 5: コミットは無し**（`done.md` を logs でコミット・push）

---

### Task 8: UITK の撮影・PlayMode（P と N。R-11）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-pause-train.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-blocks-a.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-blocks-b.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-b5.cs`

**Interfaces:**
- Consumes: `PlaytestGameViewOps.SetResolution(CancellationToken, int, int)`（`Client.Playtest.Operations.Ui`）、`p.Screenshot(string)`、`PlaytestOverlayView`（`Client.Playtest.Overlay`。撮る前に消す・前例 `uitk-stage3-compare.cs:108-113`）、`GameUiRoot.Instance.GetRootElement()`（`Client.Ui.Foundation.Root`）、`p.PressInteract()`・`p.PressRide()`（`Client.Playtest.Operations` の拡張）、`p.SetupFlatGround()`（足場 `PlaytestSetup.GroundCenter`=(0,30,0)・`GroundSize`=(50,4,50)。`Client.Playtest/Operations/PlaytestSetup.cs:23-24`）・`p.PlaceBlockDirect(string, Vector3Int, BlockDirection)`・`p.WaitBlockGameObject(Vector3Int)`・`p.RemoveBlock(Vector3Int) : UniTask<bool>`（`PlaytestDriver.cs:83,88,101`）・`p.WarpPlayer(Vector3)`・`p.AimAt(Vector3)`、`TutorialPresentationStateStore.Instance.BeginSession(Guid)`・`AddOutlineHighlight(string, Guid)`・`EndSession(Guid)`（`Client.Game.InGame.Tutorial`）、`AnchorIds.PauseSettings`・`AnchorIds.ResearchNode(string)`・`AnchorIds.ChallengePanel`（`Client.Game.InGame.Tutorial.UIHighlight`）、`MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0].Challenges[0].Tutorials[0].TutorialGuid`（前例 `uitk-tutorial-cues.cs:76`）、乗車の手順 `PrepareAndRideTrain`・`PlacePier`（`uitk/uitk-nested-pause-train.cs:83-114`）、`Localize.TrySetChosenLanguage(string) : bool`（`Client.Localization`。前例 `connect/rotary-generator-ui.cs:54`）
- Produces: `compare/X-prerun/<id>-1280x720.png`・`<id>-2560x1080.png`（P と N の全 id）

- [ ] **Step 1: ポーズと列車の撮影シナリオ**

`prerun/prerun-shots-pause-train.cs`（200 行未満）。冒頭の using は `uitk-nested-pause-train.cs:3-28` を写し（28 行目の `using Client.Playtest.Input;` が `SemanticInput` を解く）、`Client.Localization`・`Client.Playtest.Operations.Ui`・`Client.Playtest.Overlay`・`Client.Ui.Foundation.Root`・`UnityEngine.UIElements` を足す。本体:
```csharp
// 撤去の前の並べ比べ（X-prerun）: ポーズ・設定・バグ報告と、乗車中の列車 HUD・入れ子のポーズを 1280×720 と 2560×1080 で撮る（録画しない）
// Pre-removal comparison (X-prerun): shoot pause, settings, bug report, the riding train HUD and the nested pause at 1280x720 and 2560x1080 (no recording)
var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 360f };
return PlaytestRunner.Run("prerun-shots-pause-train", options, async p =>
{
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Assert(GameUiRoot.Instance.GetRootElement().Q("hotbar-slot-1") != null, "UITK のホットバーが描かれている");
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "撮影の表示言語を日本語に固定（Web の参照と同じ）");
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;

    // ポーズの 3 画面を 2 解像度で撮る
    // Shoot the three pause screens at two resolutions
    foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
    {
        await SetResolution(w, h);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
        await p.UntilUiElement("pause-menu-open-settings", 10f);
        await Shoot("1-pause-top", w, h);
        await p.ClickUi("pause-menu-open-settings");
        await p.UntilUiElement("language-select", 10f);
        await Shoot("1-settings", w, h);
        await p.ClickUi("pause-menu-back");
        await p.UntilUiElement("pause-menu-open-bug-report", 10f);
        await p.ClickUi("pause-menu-open-bug-report");
        await p.WaitSeconds(1f);
        await Shoot("1-bug-report", w, h);
        await p.ExitToGameScreen();
    }

    // 乗車して列車 HUD と入れ子のポーズを撮る
    // Ride and shoot the train HUD and the nested pause
    await SetResolution(1280, 720);
    await PrepareAndRideTrain();
    var trainHud = resolver.Resolve<UIStateDictionary>().GetState(UIStateEnum.TrainHUDScreen) as TrainHUDScreenState;
    await p.Until(() => p.CurrentUiState == UIStateEnum.TrainHUDScreen && trainHud.IsRiding, 15f, "列車HUDで乗車済み");
    await p.UntilUiElement("train-riding-hud", 10f);
    foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
    {
        await SetResolution(w, h);
        await Shoot("2-train-hud", w, h);
        await p.PressKey(Key.Escape);
        await p.Until(() => trainHud.SubState == NestedPauseSubStateEnum.PauseMenuScreen, 15f, "入れ子ポーズ");
        await p.UntilUiElement("pause-menu", 10f);
        await Shoot("1-pause-in-train", w, h);
        await p.PressKey(Key.Escape);
        await p.Until(() => trainHud.SubState == NestedPauseSubStateEnum.GameScreen, 15f, "入れ子ポーズを閉じた");
    }
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    async UniTask SetResolution(int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await p.WaitSeconds(1.5f);
        p.Assert(Screen.width == width && Screen.height == height, $"Screen が {width}x{height}（実際 {Screen.width}x{Screen.height}）");
    }

    // 操作オーバーレイを消し、カーソルをパネルの外へ置いてから撮る
    // Hide the operation overlay and park the cursor off the panels before shooting
    async UniTask Shoot(string id, int width, int height)
    {
        foreach (var overlay in UnityEngine.Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"pt-{id}-{width}x{height}");
    }

    // PrepareAndRideTrain と PlacePier は uitk-nested-pause-train.cs:83-114 をそのまま写す

    #endregion
});
```
`PrepareAndRideTrain`・`PlacePier` の 2 つのローカル関数は `uitk/uitk-nested-pause-train.cs:83-114` の本文を変えずに `#region Internal` の中へ写す（最後のコメント行は写したら消す）。写した本文の `Object.FindObjectsByType`（写し元 99 行）は裸の `Object` なので、このファイルに `using System;` を足さない（足すと `System.Object` とあいまいになり CS0104）。`Guid` を使う `prerun-shots-b5.cs`・`prerun-videos.cs`・`prerun-smoke-shots.cs` は `using System;` を持つので、`Object` は必ず `UnityEngine.Object` と書く（D-20）。

- [ ] **Step 2: ブロックの撮影シナリオ**

`prerun/prerun-shots-blocks-a.cs`（200 行未満。前半 7 種）:
```csharp
// 撤去の前の並べ比べ（X-prerun）: 平らな地面にブロックを置いて開き、ブロック UI を 1280×720 と 2560×1080 で撮る（録画しない。前半 7 種、後半は prerun-shots-blocks-b.cs）
// Pre-removal comparison (X-prerun): place blocks on flat ground, open them and shoot the block UI at 1280x720 and 2560x1080 (no recording; first 7 kinds, the rest in prerun-shots-blocks-b.cs)
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// 撮影 id とピン先マスタのブロック名（Task 7 の対応表の N の行と同じ並び）
// Shot ids and block names in the pinned master (same order as the N rows of the Task 7 table)
var blocks = new (string id, string blockName)[]
{
    ("4-block-chest", "木のチェスト"), ("4-block-tank", "液体タンク"), ("4-block-machine", "石窯"),
    ("4-block-gearMachine", "原始的な粉砕機"), ("4-block-generator", "ガソリンエンジン発電機"), ("4-block-miner", "電気採掘機"),
    ("4-block-gearMiner", "原始的な採掘機"),
};
var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 360f };
return PlaytestRunner.Run("prerun-shots-blocks-a", options, async p =>
{
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await p.SetupFlatGround();
    p.Assert(GameUiRoot.Instance.GetRootElement().Q("hotbar-slot-1") != null, "UITK のホットバーが描かれている");
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "撮影の表示言語を日本語に固定（Web の参照と同じ）");

    // 足場（中心 (0,30,0)・50×50・上面 y=32）の内側の 1 か所へ 1 個ずつ置き、2 解像度を撮って消してから次へ
    // Place one block at a time at a fixed spot inside the 50x50 ground (top y=32), shoot both resolutions, then remove it
    var position = new Vector3Int(-6, 32, -6);
    foreach (var (id, blockName) in blocks)
    {
        await p.PlaceBlockDirect(blockName, position, BlockDirection.North);
        var placed = await p.WaitBlockGameObject(position);
        var bounds = placed.GetComponentsInChildren<Collider>(true)[0].bounds;
        foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) }) await OpenAndShoot(id, bounds, w, h);
        p.Assert(await p.RemoveBlock(position), $"{blockName} を撤去した");
        await p.WaitSeconds(1f);
    }
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    // 解像度を合わせ、手前に立って照準し、開いて panel の矩形を控えて撮って閉じる
    // Set the resolution, stand in front, aim, open, note the panel rect, shoot and close
    async UniTask OpenAndShoot(string id, Bounds target, int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await p.WaitSeconds(1.5f);
        p.Assert(Screen.width == width && Screen.height == height, $"Screen が {width}x{height}");
        p.WarpPlayer(new Vector3(target.center.x, target.min.y + 1.5f, target.min.z - 1.5f));
        await p.WaitSeconds(0.5f);
        await p.AimAt(target.center);
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.UntilUiElement("block-inventory-panel", 10f);
        foreach (var overlay in UnityEngine.Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        await p.WaitSeconds(1f);
        var uiRoot = GameUiRoot.Instance.GetRootElement();
        p.Note($"{id} {width}x{height} root={uiRoot.layout.width} panel={uiRoot.Q("block-inventory-panel").worldBound} close={uiRoot.Q("block-inventory-close").worldBound}");
        await p.Screenshot($"bk-{id}-{width}x{height}");
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    #endregion
});
```
`prerun/prerun-shots-blocks-b.cs` は同じ本文で、1 行目のコメントの「前半 7 種、後半は prerun-shots-blocks-b.cs」を「後半 7 種、前半は prerun-shots-blocks-a.cs」に、配列を後半 7 種（`("4-block-filterSplitter", "フィルター分岐器"), ("4-block-pumpNoVein", "油井"), ("4-block-gearPump", "歯車ポンプ"), ("4-block-gearGenerator", "燃料式風車"), ("4-block-electricToGear", "回転生成機"), ("4-block-electricPole", "電柱"), ("4-block-spaceElevator", "宇宙エレベーター拠点")`）に、`PlaytestRunner.Run` の名前を `prerun-shots-blocks-b` にする。2 本に分けるのは、1 本 14 種だと Act ごとの 0.5 秒の間（`PlaytestReporter.ActionIntervalSeconds`）を含めて 350 秒前後かかり（推測）、`run-scenario.sh` が result.json を待つ 420 秒（`RESULT_TIMEOUT`）に近いため（D-19）。最初の run の所要を `done.md`「## 証拠」に書き、350 秒を超えたらさらに分ける。
置く位置 (-6,32,-6) は、ピン先マスタ e0c913033c の最大の寸法（液体タンク 9×7×9・ガソリンエンジン発電機 7×5×13・油井 3×8×9・宇宙エレベーター拠点 9×4×9）でも足場（`PlaytestSetup.GroundCenter`=(0,30,0)・`GroundSize`=(50,4,50)。x・z は -25〜25）の内側に収まり、手前 1.5 マスに立つ位置も足場の上にある。足場の外へ置くとワープ先の下に足場が無く開く手順が時間切れになる（D-19）。足場の上で開けなかったブロック（UI を持たない・置けない）だけを、Step 4 の run の落ちた行を見て表の撮り方を E に替え、配列から外す（`done.md` に理由）。座標・照準の誤りで落ちた行は E に替えず、座標を直して流し直す。`p.Note` の root の幅・panel と閉じるボタンの矩形は Task 9 Step 1 の EditMode との一致の確かめに使う（寸法と、panel 原点からの閉じるボタンの相対位置で比べる。D-46）。

- [ ] **Step 3: 束 5 の撮影シナリオ**

`prerun/prerun-shots-b5.cs`（200 行未満。Step 1 と同じ前置き〈日本語の固定の `p.Assert(Localize.TrySetChosenLanguage("japanese"), …)` を含む〉・`SetResolution`・`Shoot` を持ち、出力名の接頭辞は `b5-`）。本体の 2 解像度のループの中身:
```csharp
        // チャレンジ一覧（T）
        // Challenge list (T)
        await p.PressKey(Key.T);
        await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
        await p.Until(() => root.Query(className: AnchorIds.ChallengePanel).First() != null, 10f, "チャレンジ一覧のパネル");
        await Shoot("5-challenge-list", w, h);
        await p.ExitToGameScreen();

        // 持ち物を日本語のまま開く（i18n の参照）
        // Open the inventory in Japanese (the i18n reference)
        p.Assert(root.Query(className: "game-ui-locale--cjk").First() != null || root.ClassListContains("game-ui-locale--cjk"), "和文のロケール");
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
        await p.UntilUiElement("inventory-panel", 10f);
        await Shoot("5-japanese-inventory", w, h);
        await p.ExitToGameScreen();

        // ポーズの設定ボタンに強調枠とラベル
        // Outline the pause settings button with its label
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
        // チャレンジ 1 の本物の枠とラベルが常に木にあるので、足す前の数を控えて数が増えるまで待つ
        // Challenge 1's real outline and label are always in the tree, so wait until the count grows past the one noted before adding
        var labelsBefore = root.Query(name: "tutorial-highlight-label").ToList().Count;
        var session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.PauseSettings, tutorialGuid);
        await p.Until(() => root.Query(name: "tutorial-highlight-label").ToList().Count > labelsBefore, 10f, "足した枠のラベル");
        await Shoot("5-tutorial-outline-label", w, h);
        TutorialPresentationStateStore.Instance.EndSession(session);
        await p.ExitToGameScreen();

        // 研究ツリーの原始研究1 のノードに強調枠
        // Outline the primitive research 1 node in the research tree
        await p.PressKey(Key.R);
        await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
        var ringsBefore = root.Query(className: "tutorial-outline__ring").ToList().Count;
        session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.ResearchNode("858bcb10-b8ba-478e-9bc5-473ca61281a2"), tutorialGuid);
        await p.Until(() => root.Query(className: "tutorial-outline__ring").ToList().Count > ringsBefore, 10f, "研究ノードの枠");
        await Shoot("5-tutorial-research-node", w, h);
        TutorialPresentationStateStore.Instance.EndSession(session);
        await p.ExitToGameScreen();
```
ループの前に `var root = GameUiRoot.Instance.GetRootElement();` と `var tutorialGuid = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0].Challenges[0].Tutorials[0].TutorialGuid;`（前例 `uitk-tutorial-cues.cs:76`）を置く。枠とラベルの待ちは「足す前の数より増えた」で見る（`root.Q("tutorial-highlight-label") != null` や `First() != null` はチャレンジ 1 の本物の枠〈`challenge.current-hud`〉で最初から真になり、足した枠が出る前に撮ってしまう。S2-tutorial が同じ取り違えで FAIL した前例。D-47）。出展モードの言語ゲート（`5-event-language-gate`）はこのシナリオで撮らない。ゲートはゲームの DI より前に `MainGameInitializationFinalizer` から 1 度だけ出る全画面で（`Client.Ui.State/EventMode/EventLanguageGate.cs:18`）、起動後のゲームで出し直す公開の口が無く、口を足せば本番にテスト専用の口が残るので、E で撮る（Task 9 Step 2・D-31）。S5-ladder が `stage5/` に同じ状態を撮るシナリオを持っていれば、その id はこのファイルから外して P にする（Task 7 の表を直す）

- [ ] **Step 4: P と N を流して名前をそろえる**

Run（GameUi。1 本ずつ前景で）:
```bash
git push
S=.agents/skills/unity-playmode-recorded-playtest
C=moorestech_client/.uloop/outputs/compare/X-prerun; mkdir -p $C
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); Client.Localization.Localize.Initialize(); return Client.Localization.Localize.TrySetChosenLanguage("japanese");'
$S/scripts/run-scenario.sh ./moorestech_client $S/scenarios/uitk/prerun/prerun-shots-pause-train.cs; echo "exit=$?"
```
戻り値 `true`（日本語に固定できた）を見る。同じ前置き（1 本ごとに毎回。P の `uitk/uitk-notification-compare.cs` は最後に english を保存するので、流した後の撮影は前置きで日本語に戻す）で `prerun-shots-blocks-a.cs`・`prerun-shots-blocks-b.cs`・`prerun-shots-b5.cs` と、表の P の出所のシナリオ（`uitk/stage2/uitk-stage2-compare.cs`・`uitk/uitk-notification-compare.cs`・`uitk/uitk-skit-compare.cs`・`uitk/stage3/uitk-stage3-compare.cs`・`uitk/stage4/uitk-stage4-wave-shots.cs`・S5-ladder の段 5 の撮影）を流す。
各 run dir の PNG を、表の「出力名」列から `$C/<id>-<w>x<h>.png` へ `cp` する（skit-compare の `<prefix>` が `720p` だけの状態は 1280×720 の行だけを埋め、2560×1080 が無い id は表の撮り方を N に替えて `prerun-shots-*` に足す）。
Expected: 全 run が exit 0・ErrorLogs 0。Run: `for id in <表の P と N の id>; do for r in 1280x720 2560x1080; do test -s $C/$id-$r.png || echo "無い: $id $r"; done; done` が何も出さない。撮った HEAD（Run: `git rev-parse HEAD`）を `done.md`「## 撮影の対応表」の見出しの直下に `- 撮影の HEAD（UITK）: <40 桁>` で書く（撮り直したら替える。Task 12 Step 2・Task 16 Step 2 が「撮影の HEAD から今の HEAD までに `Client.Ui`・`UI Toolkit` が変わったか」をこの行で見る。D-45）

- [ ] **Step 5: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/
git commit -m "test(uitk): 撤去の前の並べ比べの撮影シナリオを足す（ポーズと列車・ブロック・束 5）（X-prerun）"
```
`done.md`「## 証拠」に run dir 名と PNG の数を書く

---

### Task 9: UITK の撮影・EditMode（E）と動画（V）（R-11・R-12）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-videos.cs`

**Interfaces:**
- Consumes: `Client.Ui.Editor.Capture.UiRuntimePanelCapture.CaptureToPng(VisualElement content, int width, int height, Color ground, UiCaptureLocalization localization, string pngPath) : string`・`Render(VisualElement, int, int, Color, UiCaptureLocalization) : Texture2D`（`Client.Ui.Editor/Capture/UiRuntimePanelCapture.cs:33,125`）、`UiCaptureLocalization.InitializeDictionary`／`KeepCurrentDictionary`、Task 7 の表の E の行の「出所」（各レーンの外殻・偽の Model の組み方）、`ClientDIContext.ClientLocalNotificationSource.Notify(…)`・`NotificationMessagePack.CreateItemEarned(ItemId, int)`・`CreateAchievementWithItem(string, Dictionary<string,string>, ItemId)`（前例 `uitk/uitk-notification-compare.cs:33-38,61`）、`TutorialPresentationStateStore.Instance.AddKeyControlHint(string, string, string)`・`AddDragGuide(string, string)`・`AddOutlineHighlight(string, Guid)`、ワールドピンの `WorldPinStateStore.Instance.GetCurrent().Pins` と振り向き `TurnUntilPin`・`Turn`（`uitk/uitk-tutorial-cues.cs:46-70,182-197`）、Web の動画の手順 `moorestech_web/webui/e2e/uitk-reference/referenceVideos.ts:11-17`
- Produces: E の全 id の `$C/<id>-<w>x<h>.png` と面の矩形 `$C/uitk-panel-rects.tsv`（`id 解像度 x y 幅 高さ`。px）、`$C/video/<id>-{start,mid,end}-{web,uitk}-{1280x720,2560x1080}.png`

- [ ] **Step 1: EditMode と PlayMode の一致を先に確かめる（[A-164]）**

`4-block-chest` を E の手順でも 1280×720 と 2560×1080 の両方で撮り、Task 8 の PlayMode の `bk-4-block-chest-1280x720`・`bk-4-block-chest-2560x1080` と比べる（E の id は 2560×1080 でも撮るので、舞台の倍率が合っているかをその解像度でも見る）。PlayMode を止め、`$C/edc/4-block-chest.cs` に S4-alpha の `done.md` の組み方（本番の `BlockInventoryView` を `StageColumnsLayout` の Viewer 列に載せ、木のチェストの本文と偽の詳細）で外殻を組み、舞台の root の `layout.width` と `block-inventory-panel`・`block-inventory-close` の `worldBound` を返す式を書き、先頭で `Client.Localization.Localize.Initialize(); Client.Localization.Localize.TrySetChosenLanguage("japanese");` を呼んで（`InitializeDictionary` の `Localize.Initialize()` は `PlayerPrefs` の言語を読むので、保存された日本語で辞書が立つ。D-30）、`UiRuntimePanelCapture.CaptureToPng(<外殻>, <w>, <h>, new Color32(<参照の地の画素>, 255), UiCaptureLocalization.InitializeDictionary, "<絶対パス>/$C/edc-4-block-chest-<w>x<h>.png")` を 1280×720 と 2560×1080 の 2 回呼ぶ:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code-file $C/edc/4-block-chest.cs
```
PlayMode 側の矩形は、`prerun-shots-blocks-a.cs` が撮影の直前に打つ `p.Note`（`<id> <w>x<h> root=<幅> panel=<worldBound> close=<worldBound>`。Task 8 Step 2 のコードに最初から入っている）の `4-block-chest 1280x720` と `4-block-chest 2560x1080` の行から取る（`worldBound` は論理座標なので、画面の px = 論理 ×〈PNG の幅 ÷ root の幅〉に直して比べる）。比べるのは panel の幅・高さと、panel 原点からの閉じるボタンの相対位置（x−panel.x・y−panel.y）で、panel の画面上の絶対位置は比べない（EditMode の外殻は Viewer 列だけで持ち物の列が空なので panel が左へ寄る。S4-alpha `done.md`「UITK 本番は Inv 列が空で x=56」。前例 S4-special は panel 原点からの相対で 0.0px・0.7px。D-46）。
Expected: panel の幅・高さと閉じるボタンの相対位置の差が 1280×720 で 2px 以内、2560×1080 で倍率を掛けた 2px 以内（どちらも `done.md` に値。panel の絶対位置の差も参考に書く）。超えたら舞台の論理寸法（PlayMode の root の大きさ）を EditMode の外殻へそろえて撮り直す。それでも超えるなら E の撮影へ進まない（E の id は実行時に作れない状態なので PlayMode の組が無く、位置の差を照らす相手が無い。撮り進めると評価役の挙げる位置の差が道具の差か画面の差か分けられない）。E の全 id を Task 9 Step 2 の逃げ道と同じ扱い（R-11 未達。`done.md`「要裁定」に Q-n〈外した id・一致の測った値・代わりの証拠の報告ページの URL・推奨案〉）にし、`done.md`「integrator への依頼」に「`UiRuntimePanelCapture` の舞台の寸法を PlayMode にそろえる口の不足（測った差）」を書いて Step 3 へ進む（D-23・D-42）

- [ ] **Step 2: E の全 id を撮る**

表の E の行ごとに `$C/edc/<id>.cs` を書き（出所のレーンの組み方を写す。地の色は新しい参照の該当画面の外側の 1 画素）、先頭で Step 1 と同じく日本語に固定してから、1280×720 と 2560×1080 の 2 回 `CaptureToPng` を呼ぶ。各回の戻り値に、対応表の `面=<testid>` の要素の矩形を画面の px（論理 ×〈PNG の幅 ÷ 舞台の root の `layout.width`〉）で返させ、`$C/uitk-panel-rects.tsv` に `<id>\t<1280x720|2560x1080>\t<x>\t<y>\t<幅>\t<高さ>` の 1 行で足す（Task 11 Step 1 が切り抜きに使う。撮り直すときはこのファイルを空にしてから足し直す。D-46）。辞書を足すテストの見本を使うときは `UiCaptureLocalization.KeepCurrentDictionary` にし（このときも日本語に固定してから辞書を足す）、撮った後に辞書を戻す（前例 S4-research。言語は日本語のままにする）。`5-event-language-gate` は S5-gate のゲートの L1 の土台（`Client.Tests/Ui/Screens/<ゲート>/Support/`。起草時の HEAD に無いので Task 7 Step 1 で S5-gate の `done.md` と実物から型名を写す）の外殻に、mock-host の `eventLanguageGateWaiting` の topic と同じ待ち状態の Model を渡して撮る（実行時に出し直す口が無い。D-31）。前例の無い id は、その画面の L1 の土台（`Client.Tests/Ui/Screens/<画面>/Support/`）の外殻に、Web の fixture（`moorestech_web/webui/e2e/mock-host/` の該当 fixture）と同じ値の ViewModel を渡す形で組み、`done.md` の表の出所に型名を書く。
Expected: Run: `for id in <表の E の id>; do for r in 1280x720 2560x1080; do test -s $C/$id-$r.png || echo "無い: $id $r"; awk -F'\t' -v i=$id -v r=$r '$1==i && $2==r {f=1} END {exit !f}' $C/uitk-panel-rects.tsv || echo "矩形が無い: $id $r"; done; done` が何も出さない。撮れない状態（外殻が実行時の DI を要する等）は `done.md`「integrator への依頼」に道具の口の不足として書き、その id の比べ方を「各レーンの PR の並べ比べを証拠にする」（W6-3 と同じ扱い）に替えて表に書く。この逃げ道を使った id は R-11 未達なので、`done.md`「要裁定」に Q-n（外した id・代わりの証拠の報告ページの URL・外した理由・推奨案）でまとめて上げてから先へ進む（実行計画 §1.5 の完了条件「UITK の全画面と並べ比べ」を省く判断はユーザーのもの。0 件なら何もしない。D-23）

- [ ] **Step 3: 動画の UITK 側のシナリオ**

`prerun/prerun-videos.cs`（200 行未満。`Record = true`。冒頭は Task 8 Step 1 と同じ前置き〈日本語の固定を含む〉・受け口の確かめ〈`uitk-tutorial-cues.cs:31-32` の throw〉・`SetResolution`・オーバーレイを消す処理）。録画は 1280×720 の出力に固定（`Client.Playtest/Recording/PlaytestRecorder.cs:33` `OutputWidth = 1280, OutputHeight = 720`）なので、1280×720 は録画から 3 フレームを抜き、2560×1080 は同じ操作をもう 1 周して Web の `durationMs`（`referenceVideos.ts:11-17`。通知 `NOTIFICATION_DISPLAY_MS`〈7000〉+5000＝12000・ヒント 3000・ピン 3000・DragGuide 4000・枠 3000）の 10%・50%・90% の時刻に静止画を撮る（DSL に録画の解像度の口を足さない。D-32）。各動画は Web の `steps` の `visible(...)` と同じ要素が出たことを Assert してから相を数える（要素が出ないまま撮って見せかけの「欠け」を作らない。D-33）:
```csharp
    // 1280×720 は録画から、2560×1080 は同じ相の静止画で比べる
    // Compare 1280x720 via the recording and 2560x1080 via stills at the same phases
    foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
    {
        await SetResolution(w, h);
        await PlayAll(w, h);
    }
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    async UniTask PlayAll(int w, int h)
    {
        // 2-notification-enter-aggregate-exit: 獲得 5 → 1.5 秒 → 同じアイテム 3（集約で 8）→ 1 秒 → 研究完了。12 秒
        // 2-notification-enter-aggregate-exit: earn 5 -> 1.5s -> 3 more (aggregated to 8) -> 1s -> research completed; 12s
        var id = "2-notification-enter-aggregate-exit";
        var start = Mark(id, w, h);
        Notify(NotificationMessagePack.CreateItemEarned(new ItemId(2), 5));
        await p.Until(() => IsShown("notification-row"), 5f, "通知の行が出た");
        await ShootAt(id, "start", start, 1.2f, w, h);
        await WaitUntil(start, 1.5f);
        Notify(NotificationMessagePack.CreateItemEarned(new ItemId(2), 3));
        await WaitUntil(start, 2.5f);
        Notify(NotificationMessagePack.CreateAchievementWithItem("achievement.researchCompleted", new Dictionary<string, string> { ["research"] = research.ResearchNodeGuid.ToString() }, new ItemId(1)));
        await ShootAt(id, "mid", start, 6f, w, h);
        await ShootAt(id, "end", start, 10.8f, w, h);
        await WaitUntil(start, 12.5f);

        // 2-key-control-hint-pulse: ゲーム画面で Tab のキー操作ヒント。3 秒
        // 2-key-control-hint-pulse: the Tab key-control hint on the game screen; 3s
        var session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        start = Mark("2-key-control-hint-pulse", w, h);
        TutorialPresentationStateStore.Instance.AddKeyControlHint(tutorialGuid.ToString(), "Tab", "GameScreen");
        await p.Until(() => IsShown("key-control-hint"), 5f, "キー操作ヒントが出た");
        await Phases("2-key-control-hint-pulse", start, 3f, w, h);
        TutorialPresentationStateStore.Instance.EndSession(session);

        // 2-world-pin-pulse: チャレンジ 1 のピンを画面外にして端の矢印。3 秒
        // 2-world-pin-pulse: turn the challenge 1 pin off screen and keep the edge arrow; 3s
        await p.Until(() => WorldPinStateStore.Instance.GetCurrent().Pins.Length > 0, 20f, "ワールドピン");
        await TurnUntilPin(false);
        await Turn(2);
        await p.Until(() => IsShown("world-pin__arrow"), 10f, "画面端の矢印が出た");
        start = Mark("2-world-pin-pulse", w, h);
        await Phases("2-world-pin-pulse", start, 3f, w, h);

        // 4-drag-guide: 持ち物のレシピ一覧のセル → ホットバーの DragGuide（mock-host の tutorialDragGuide と同じ組）。4 秒
        // 4-drag-guide: DragGuide from a recipe list cell in the inventory to the hotbar (same pair as mock-host tutorialDragGuide); 4s
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
        await p.Until(() => root.Query(className: dragFrom).First() != null, 10f, "DragGuide の始点のセルがある");
        session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        start = Mark("4-drag-guide", w, h);
        TutorialPresentationStateStore.Instance.AddDragGuide(dragFrom, AnchorIds.HotbarHud);
        await p.Until(() => IsShown("tutorial-drag-guide"), 5f, "DragGuide が出た");
        await Phases("4-drag-guide", start, 4f, w, h);
        TutorialPresentationStateStore.Instance.EndSession(session);
        await p.ExitToGameScreen();

        // 5-tutorial-pulse: 照準（game.crosshair）に強調枠とラベル（mock-host の tutorialOutlineWithLabel と同じ）。3 秒
        // 5-tutorial-pulse: outline the crosshair with its label (same as mock-host tutorialOutlineWithLabel); 3s
        var labelsBefore = root.Query(name: "tutorial-highlight-label").ToList().Count;
        session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        start = Mark("5-tutorial-pulse", w, h);
        TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.GameCrosshair, tutorialGuid);
        await p.Until(() => root.Query(name: "tutorial-highlight-label").ToList().Count > labelsBefore, 5f, "足した枠のラベルが出た（チャレンジ 1 の本物の枠の分を除く）");
        await Phases("5-tutorial-pulse", start, 3f, w, h);
        TutorialPresentationStateStore.Instance.EndSession(session);
    }

    // 相の時刻: 1280×720 は録画から抜くので待つだけ、2560×1080 は静止画を撮る
    // Phase times: at 1280x720 just wait (frames come from the recording); at 2560x1080 shoot a still
    float Mark(string id, int w, int h)
    {
        p.Note($"[video] {id} {w}x{h}");
        return Time.realtimeSinceStartup;
    }

    async UniTask Phases(string id, float start, float seconds, int w, int h)
    {
        await ShootAt(id, "start", start, seconds * 0.1f, w, h);
        await ShootAt(id, "mid", start, seconds * 0.5f, w, h);
        await ShootAt(id, "end", start, seconds * 0.9f, w, h);
        await WaitUntil(start, seconds + 0.5f);
    }

    async UniTask ShootAt(string id, string phase, float start, float seconds, int w, int h)
    {
        await WaitUntil(start, seconds);
        if (w == 2560) await p.Screenshot($"vid-{id}-{phase}-{w}x{h}");
    }

    async UniTask WaitUntil(float start, float seconds)
    {
        var rest = start + seconds - Time.realtimeSinceStartup;
        if (rest > 0f) await p.WaitSeconds(rest);
    }
```
本体の冒頭（ループの前）に `var root = GameUiRoot.Instance.GetRootElement();`・`var tutorialGuid = …`（Task 8 Step 3 と同じ）・`var research = MasterHolder.ResearchMaster.GetAllResearches().First();`・`void Notify(NotificationMessagePack m) => ClientDIContext.ClientLocalNotificationSource.Notify(m);` に当たる呼び出し（ローカル関数として `#region Internal` に置く）・`var dragFrom = AnchorIds.RecipeItem(<実ゲームの持ち物のレシピ一覧に並ぶアイテムの ItemId>);` を置く。`dragFrom` のアイテムは mock-host の `fx.TUTORIAL_RECIPE_ITEM_ID` を写さず、ピン先マスタで持ち物のレシピ一覧に最初から並ぶもの（木の板などのクラフトの結果）を選ぶ（mock の id は実ゲームの持ち物に無いことがあり、ガイドが描かれないまま撮れてしまう。上の `Until` で始点のセルがあることを確かめる）。`IsShown`・`IsShownElement`（`uitk-tutorial-cues.cs:150-166`）と `TurnUntilPin`・`Turn`（同 182-197）を `#region Internal` へ写す（`ScaleRange` は写さない）。写した結果が 200 行に届くなら、2560×1080 の周を `prerun/prerun-videos-2560.cs`（`Record = false`）に分ける（`prerun/` は 6 本で 10 本まで。File Structure の行を足す）。何を使ったか（`dragFrom` のアイテム名）を `done.md` に書く。Run: Task 8 Step 4 と同じ前置きで `run-scenario.sh ./moorestech_client $S/scenarios/uitk/prerun/prerun-videos.cs`。Expected: exit 0・Assert の要素待ちが全部通る・`recording.mp4` と、`result.json` の Timeline に `[video] <id> 1280x720` と `[video] <id> 2560x1080` が 5 行ずつ・run dir に `vid-<id>-{start,mid,end}-2560x1080.png` が 15 枚

- [ ] **Step 4: 3 フレームを抜く**

Run（id ごとに。`T` は Timeline の `[video] <id> 1280x720` の行の秒、`dur` は 1280×720 の webm の長さ）:
```bash
mkdir -p $C/video
W=$L/web-$SHA/<id>.webm; dur=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$W")
W2=$L/web-$SHA-2560x1080/<id>.webm; dur2=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$W2")
for ph in start:0.1 mid:0.5 end:0.9; do n=${ph%%:*}; f=${ph##*:}
  ffmpeg -v error -y -ss "$(python3 -c "print($dur*$f)")" -i "$W" -frames:v 1 $C/video/<id>-$n-web-1280x720.png
  ffmpeg -v error -y -ss "$(python3 -c "print($T+$dur*$f)")" -i <run dir>/recording.mp4 -frames:v 1 $C/video/<id>-$n-uitk-1280x720.png
  ffmpeg -v error -y -ss "$(python3 -c "print($dur2*$f)")" -i "$W2" -frames:v 1 $C/video/<id>-$n-web-2560x1080.png
  cp <run dir>/vid-<id>-$n-2560x1080.png $C/video/<id>-$n-uitk-2560x1080.png
done
```
Expected: 5 件 × 3 相 × 2 系 × 2 解像度＝60 枚。録画の時刻と Timeline の秒がずれる（録画の始まりが RunStart と違う）ときは、最初の `[video]` の操作の直後に出る要素が最初に写るフレームを探して差を測り、`T` に足す（`done.md` に差の秒）。Web の webm の t=0 は ready の後の境界で、脈動の 4 件は t=0 で脈動の途中、通知は 0.5 秒後に出る（Task 7 Step 3 の V の行）。UITK は `Mark` の後に出し始め、2560×1080 は静止画の撮影に数フレームかかる。だから両解像度の動画の組で相がそろわず、評価役には「動画の組（1280×720 と 2560×1080 の両方）は動きの位相の差〈脈動の大きさ・透明度・通知の出入りの途中〉を数えず、位置・寸法・色・装飾だけを見る」と渡す（Task 11 Step 2。脈動の振幅と周期は `uitk-tutorial-cues.cs` と UI テストが見る。D-35）

- [ ] **Step 5: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-videos.cs
git commit -m "test(uitk): 撤去の前の並べ比べの動画の撮影シナリオを足す（X-prerun）"
```

---

### Task 10: 撤去レーンの物差し `prerun-smoke-shots.cs`（R-15）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-smoke-shots.cs`

**Interfaces:**
- Consumes: Task 8 の Interfaces と同じ口（全体スイッチと Web の型は使わない）
- Produces: `prerun-smoke-shots.cs`。run dir に `smoke-<状態名>-<w>x<h>.png` の 20 枚（状態名は下の 10 個）。5a〜5e が起点と撤去後で流し、同じ名前の PNG を `ffmpeg -i a.png -i b.png -lavfi psnr -f null -` で比べる（W6-6 (b)）

- [ ] **Step 1: 書く**

```csharp
// 撤去レーン（X-5a〜5e）の見た目の不変の物差し: 束ごとに 2 状態・計 10 状態を 1280×720 と 2560×1080 で撮る（X-prerun）
// 全体スイッチと Web の型を参照しないので 5a〜5e のどの HEAD でもそのまま流せる。Editor の受け口を http://127.0.0.1:9 にしてから流す
// 1 回の run で両解像度を撮る。出力は run dir の smoke-<状態>-<幅>x<高さ>.png の 20 枚。表示言語は日本語に固定して撮る（Web の参照と同じ）
// 状態: 1-pause-top・1-settings・2-game-hud・2-placement・3-build-menu・3-build-menu-search・4-player-inventory・4-research・5-challenge-list・5-tutorial-outline-label
// 前置き: 撤去の前は uloop execute-dynamic-code で受け口・全体スイッチ GameUi・日本語を立て、全体スイッチの撤去の後はスイッチの行を除いた同じ前置き
// 比べ方: 起点と撤去後の同じ名前の PNG を ffmpeg -lavfi psnr で比べる（揺れる状態の扱いは harness/uitk/X-prerun/done.md の 5a への申し送り）
// Visual-invariance yardstick for the removal lanes (X-5a..5e): 2 states per bundle, 10 in all, shot at 1280x720 and 2560x1080 (X-prerun)
// It references neither the global switch nor Web types, so it runs unchanged on any 5a..5e HEAD; set the Editor receiver to http://127.0.0.1:9 first
// One run shoots both resolutions; output is 20 PNGs named smoke-<state>-<width>x<height>.png in the run dir, shot with the language pinned to Japanese (same as the Web reference)
// States: 1-pause-top, 1-settings, 2-game-hud, 2-placement, 3-build-menu, 3-build-menu-search, 4-player-inventory, 4-research, 5-challenge-list, 5-tutorial-outline-label
// Preamble: before removal set the receiver, the global switch to GameUi and Japanese via uloop execute-dynamic-code; after the switch is removed use the same preamble without the switch line
// Comparison: diff same-named PNGs from the baseline and post-removal runs with ffmpeg -lavfi psnr (see the 5a handoff in harness/uitk/X-prerun/done.md for unstable states)
using System;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 360f };
return PlaytestRunner.Run("prerun-smoke-shots", options, async p =>
{
    // 本番の受け口へ送らない・背景 Editor でも入力が届く（uitk-tutorial-cues と同じ前置き）
    // Never send to production and let injected input reach a background Editor (same preamble as uitk-tutorial-cues)
    if (Environment.GetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE") != "http://127.0.0.1:9")
        throw new InvalidOperationException("MOORESTECH_PLAYTEST_RECEIVER_BASE が http://127.0.0.1:9 でない（本番へ送るので中止）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // UITK が描いていることを要素で確かめ、ホットバーの枠 0 に木のチェストを置く
    // Confirm UITK draws by an element and put the wooden chest in hotbar slot 0
    var root = GameUiRoot.Instance.GetRootElement();
    p.Assert(root.Q("hotbar-slot-1") != null, "UITK のホットバーが描かれている");
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "撮影の表示言語を日本語に固定（起点と撤去後で言語を揃える）");
    var tutorialGuid = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0].Challenges[0].Tutorials[0].TutorialGuid;
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    await p.WaitSeconds(6f);

    foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, w, h);
        await p.WaitSeconds(1.5f);
        p.Assert(Screen.width == w && Screen.height == h, $"Screen が {w}x{h}（実際 {Screen.width}x{Screen.height}）");
        await ShootAll($"{w}x{h}");
    }
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    async UniTask ShootAll(string res)
    {
        // 束 1: ポーズのトップと設定
        // Bundle 1: pause top and settings
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 10f);
        await p.UntilUiElement("pause-menu-open-settings", 10f);
        await Shoot("1-pause-top", res);
        await p.ClickUi("pause-menu-open-settings");
        await p.UntilUiElement("language-select", 10f);
        await Shoot("1-settings", res);
        await p.ClickUi("pause-menu-back");
        await p.UntilUiElement("pause-menu-open-settings", 10f);

        // 束 5: ポーズの設定ボタンに強調枠とラベル
        // Bundle 5: outline the pause settings button with its label
        // チャレンジ 1 の本物の枠のラベルが常にあるので、足す前の数より増えるまで待つ
        // Challenge 1's real label is always present, so wait until the count exceeds the one before adding
        var labelsBefore = root.Query(name: "tutorial-highlight-label").ToList().Count;
        var session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.PauseSettings, tutorialGuid);
        await p.Until(() => root.Query(name: "tutorial-highlight-label").ToList().Count > labelsBefore, 10f, "足した枠のラベル");
        await Shoot("5-tutorial-outline-label", res);
        TutorialPresentationStateStore.Instance.EndSession(session);
        await p.ExitToGameScreen();

        // 束 2: ゲーム画面の HUD と設置モード
        // Bundle 2: game-screen HUD and placement mode
        await Shoot("2-game-hud", res);
        await p.PressKey(Key.Digit1);
        await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
        await p.UntilUiElement("placement-mode-hud", 10f);
        await Shoot("2-placement", res);
        await p.ExitToGameScreen();

        // 束 3: ビルドメニューと検索語を入れたビルドメニュー
        // Bundle 3: the build menu and the build menu with a search term
        await p.PressKey(Key.B);
        await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
        await Shoot("3-build-menu", res);
        await p.SetText("build-menu-search", "石窯");
        await p.WaitSeconds(0.5f);
        await Shoot("3-build-menu-search", res);
        await p.SetText("build-menu-search", "");
        await p.ExitToGameScreen();

        // 束 4: 持ち物と研究ツリー
        // Bundle 4: inventory and research tree
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
        await p.UntilUiElement("inventory-panel", 10f);
        await Shoot("4-player-inventory", res);
        await p.ExitToGameScreen();
        await p.PressKey(Key.R);
        await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
        await p.UntilUiElement("research-tree", 10f);
        await Shoot("4-research", res);
        await p.ExitToGameScreen();

        // 束 5: チャレンジ一覧
        // Bundle 5: challenge list
        await p.PressKey(Key.T);
        await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
        await p.Until(() => root.Query(className: AnchorIds.ChallengePanel).First() != null, 10f, "チャレンジ一覧のパネル");
        await Shoot("5-challenge-list", res);
        await p.ExitToGameScreen();
    }

    // 操作オーバーレイを消し、カーソルをパネルの外へ置いてから撮る
    // Hide the operation overlay and park the cursor off the panels before shooting
    async UniTask Shoot(string state, string res)
    {
        foreach (var overlay in UnityEngine.Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"smoke-{state}-{res}");
    }

    #endregion
});
```
アニメーションの途中を撮らないよう、各 `Shoot` の前の待ちが画面の入場の tween（最長は設計書のモーションのトークン）より長いことを Step 2 の 2 回の run で確かめる。

- [ ] **Step 2: 2 回流して決定的かを見る**

先に Run: `grep -nE "UiSystem|ActiveUiSystem|UiSystemEditorSwitch|WebUiHost|PlaytestDomQuery|Cef" .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/*.cs; echo "guard-exit=$?"`。Expected: 何も出ず `guard-exit=1`（grep の 0 件。D-2 の「5a〜5e の全 HEAD でそのままコンパイルできる」の前提を機械で確かめる。当たったら参照を `GameUiRoot` の要素の照会へ書き換えてから流す。D-18）。
Run（Task 8 Step 4 と同じ前置きで GameUi）: `run-scenario.sh ./moorestech_client $S/scenarios/uitk/prerun/prerun-smoke-shots.cs` を 2 回。2 つの run dir の同じ名前の PNG を `ffmpeg -v error -i <run1>/smoke-<state>-<res>.png -i <run2>/smoke-<state>-<res>.png -lavfi psnr -f null - 2>&1 | tail -1` で比べる。
Expected: 2 回とも exit 0・PNG 20 枚。PSNR が `inf` でない状態（世界の動き・カーソル・時刻で揺れる）は、その状態の撮影の前の待ちを延ばすか、揺れる部分（世界の空・水面）を `done.md` に書いて 5a〜5e の比べ方を「PSNR ではなく UITK の要素の矩形の差 0」に替える旨を「5a への申し送り」の「### 物差し（prerun-smoke-shots）」の小節（Step 3）に書く（D-12）

- [ ] **Step 3: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-smoke-shots.cs
git commit -m "test(uitk): 撤去レーンの見た目の物差し prerun-smoke-shots を足す（X-prerun）"
```
`done.md`「## 5a への申し送り」の「### 書き直す」の小節の後に小節「### 物差し（prerun-smoke-shots）」を立て、その中に、流し方（Task 8 Step 4 の前置き〈日本語の固定を含む〉。5a の後は全体スイッチの行を消した前置き）・出力名の形 `smoke-<状態>-<w>x<h>.png`（run dir 内・1 回の run で 20 枚）・状態名 10 個・揺れた状態の扱い（Step 2 と D-12）を書く（見出しが無いと、X-5a Task 1 Step 4b の `awk '/^### 書き直す/{f=1;next} /^##/{f=0} f'` が次の `##` までを拾い、流し方の文の `prerun-smoke-shots.cs`・`run-scenario.sh` が書き直すシナリオの一覧に混ざる。`### 物差し` の行は `/^##/` に当たるのでそこで止まる。独立評価 3 周目の軽）。ヘッダの 2 言語のコメントにも同じ 4 点（出力名と置き場・状態名・前置き・比べ方）があることを Run: `head -14 .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-smoke-shots.cs` で見る（X-5a Task 1 Step 4 がヘッダの「撮影の解像度と出力先の説明」を前提に読む）

---

### Task 11: 並べ比べと独立評価 2 系統（R-13）

**Files:** なし（`done.md`・gitignore 下）

- [ ] **Step 1: 組を作る**

Run（静止画 id × 2 解像度。Web は新しい参照。P と N は画面全体、E は面の矩形に両側を切り抜く）:
```bash
mkdir -p $C/pairs
for id in <表の P と N の id>; do
  ffmpeg -v error -y -i $L/web-$SHA/$id.png -i $C/$id-1280x720.png -filter_complex "[0][1]hstack=inputs=2" $C/pairs/$id-1280x720.png
  ffmpeg -v error -y -i $L/web-$SHA-2560x1080/$id.png -i $C/$id-2560x1080.png -filter_complex "[0][1]hstack=inputs=2" $C/pairs/$id-2560x1080.png
done
python3 - "$C" "$L/web-$SHA" <<'EOF'
import json, subprocess, sys
C, W = sys.argv[1], sys.argv[2]
uitk = {}
for line in open(f"{C}/uitk-panel-rects.tsv", encoding="utf-8"):
    i, r, x, y, w, h = line.rstrip("\n").split("\t")
    uitk[(i, r)] = [round(float(v)) for v in (x, y, w, h)]
for r, web_dir in (("1280x720", W), ("2560x1080", W + "-2560x1080")):
    for i, b in json.load(open(f"{C}/web-panel-rects-{r}.json")).items():
        wx, wy, ww, wh = (round(b[k]) for k in ("x", "y", "width", "height"))
        ux, uy, uw, uh = uitk[(i, r)]
        H = max(wh, uh)
        f = f"[0]crop={ww}:{wh}:{wx}:{wy},pad={ww}:{H}:0:0:color=magenta[a];[1]crop={uw}:{uh}:{ux}:{uy},pad={uw}:{H}:0:0:color=magenta[b];[a][b]hstack=inputs=2"
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", f"{web_dir}/{i}.png", "-i", f"{C}/{i}-{r}.png", "-filter_complex", f, f"{C}/pairs/{i}-{r}.png"], check=True)
        print(i, r, f"web {ww}x{wh} uitk {uw}x{uh}")
EOF
for id in <表の V の id>; do for r in 1280x720 2560x1080; do for n in start mid end; do ffmpeg -v error -y -i $C/video/$id-$n-web-$r.png -i $C/video/$id-$n-uitk-$r.png -filter_complex "[0][1]hstack=inputs=2" $C/pairs/$id-$n-$r.png; done; done; done
ls $C/pairs | wc -l
```
Expected: 静止画 id 数（X を除く）× 2 ＋ 動画 5 × 3 × 2 の枚数。E の組は左右とも面の矩形だけで、面の原点が左上にそろう（位置は面の原点からの相対で読める）。高さの違う面は下をマゼンタで埋める（マゼンタの帯の高さが面の高さの差。python の出力の寸法を `done.md` に写す）。P・N・V の組で高さが違って hstack が失敗した組は、UITK 側の撮影の解像度の誤りなので撮り直す（D-46）

- [ ] **Step 2: 評価役 A・B を束ごとに派遣する（本体が派遣し、結果をこのレーンが取りまとめる。D-16）**

opus の subagent を、評価役 A と B の 2 系統で、束ごとに、どの束も 1 回 24 組までに分けて派遣する（D-3・D-42。新しい manifest の件数で数え直す。起草時の `web-8a585f742a` では束 1 が 18 組で 1 回、束 2 が静止画 19×2＋動画 3×6＝56 組で 3 回、束 3 が 6 組で 1 回、束 4 が 46×2＋6＝98 組で 5 回、束 5 が X を除く 6×2＋6＝18 組で 1 回）。A と B は互いの結果を見ない。依頼文に渡すもの: ゴールの原文「WebUIと同じ見た目で、適切な設計、コンポーネント化されたUITKベースのUIを開発すること」、裁定 `.decisions/2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`・`…段階2以降の画面もWeb版と同じ見た目にする.md`、設計書 §3.3・§3.4、`compare-procedure.md` §5・§6、台帳「## 見た目の例外」の全行、`$C/states.md` のその束の行と「## 参照と feature/uitk の Web の食い違い」の節、組の画像のローカルパス（左が Web・右が UITK）。依頼:「各組の左右の差を、位置・寸法・色の系統・装飾（罫線・三角・影・ベベル・グラデ）の有無・文字の大きさと段組・欠けている要素・重なり・はみ出しで挙げる。states.md に書いた状態の差（データ・名前・行数・世界の地・実ゲームのワールドピン・実ゲームのチャレンジ 1 の `challenge.current-hud` の枠とラベル「左上で現在の目標を確認する」）と、「参照と feature/uitk の Web の食い違い」に「feature/uitk 側が正」「master の未追従」と書いた差は差に数えない（当たると思ったらその行を書く）。字形の差（Chrome と TextCore）は数えない。EditMode の組（states.md の E の行の id）は両側とも画面の面の矩形に切り抜いてあり、位置は面の原点（左上）からの相対で見る。外殻の外（常時 HUD・チャレンジ HUD・キー案内・持ち物などの他の列・ホットバー・世界の地）は E では写らないので数えず、面の画面上の絶対位置も数えない。下のマゼンタの帯は切り抜きの埋めで、帯の高さ（面の高さの差）だけを寸法の差として読む。動画の組（`<id>-<相>-<解像度>`。1280×720 と 2560×1080 の両方）は Web と UITK で効果の出始めと通知の出る時刻が違い（states.md の V の行）、2560×1080 は UITK 側が静止画なので、動きの位相の差（脈動の大きさ・透明度・通知の出入りの途中）は数えず、位置・寸法・色・装飾だけを見る。既存の LX に当たる差はその ID を書く。各差に座標（左上原点・px）と測った値を添え、重さ（一目で分かるか）を書く。合否の目安は 1280×720 で 2px。報告はファイル `<scratchpad>/x-prerun-eval-<A|B>-<束>.md` へ、返答は 3 行」。subagent はバックグラウンドのポーリングで待たず前景で実行し、Editor を起動しない

- [ ] **Step 3: 再現させる**

A・B の指摘を合わせて重ね（同じ組・同じ位置の差は 1 件）、指摘ごとに fresh-context の subagent に組の画像と指摘文だけを渡し「この差は画像で確かめられるか。px で測って」と頼む。画素は `ffmpeg -v error -i <png> -vf "crop=<w>:<h>:<x>:<y>" -f rawvideo -pix_fmt rgb24 - | python3 -c 'import sys;b=sys.stdin.buffer.read();print([tuple(b[i:i+3]) for i in range(0,len(b),3)][:16])'` のように生データで読む。再現したものだけ `done.md`「## 差の表」に「id / 解像度 / 差 / 測った値 / A・B のどちらが挙げたか / 処遇（Task 12 で埋める）」で残し、再現しなかったものは「再現せず」と 1 行で残す。再現した差のうち、states.md に無かった状態の差（mock の fixture と実ゲームのデータの差）と読めるものも差の表に残し、Task 12 Step 1 の (f) で閉じる（再現の段ではふるい落とさない）

- [ ] **Step 4: コミットは無し**（`done.md` を logs でコミット・push）

---

### Task 12: 差の処遇と撮り直し（R-14）

**Files:**
- Modify（差を直すとき）: `moorestech_client/Assets/Scripts/Client.Ui/**/*.uss`（差のある画面・部品の USS）
- Modify（例外にするとき）: `docs/uitk/TODO.md`「## 見た目の例外」（LX-210〜217）と、印を付ける USS
- Modify（190 行を超える USS を分けたときだけ）: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（分けたファイルの `@import` の 1 行。自分の行だけ）

**Interfaces:**
- Consumes: 「## 差の表」、`LookExceptionLedgerTest`（`Client.Tests/Ui/Conventions/LookExceptionLedgerTest.cs`）

- [ ] **Step 1: 行ごとに処遇を決める（D-4・D-16）**

処遇を決めるのはこのレーン（指揮の判定を待たない。設計書 §3.3・[A-179] の「指揮が判定」を X-prerun について上書き。D-16）。(b) だけが指揮へ返る。

- (a) USS の値（つまみの上書き漏れ・トークンの取り違え・半透明の補正漏れ・余白）で Web に寄せられる → このレーンが直す。値は参照の撮影元（master）の CSS ではなく `feature/uitk` の Web の CSS（`moorestech_web/webui/src/**`。読むだけ）と突き合わせて写す（Task 7 Step 3 の食い違いの表に当たる差は (a) にしない）。直す場所は (1) 差のある画面の USS（`Client.Ui/Screens/<画面>/**`。つまみの上書き）を先に選び、(2) 画面の USS で直せず Web の CSS でも部品・トークン側の値が違うときだけ部品の USS（`Client.Ui/Components/**`）か手書きのトークン（`Client.Ui/Foundation/Theme/` の `DerivedTokens.uss`・`SkitDerivedTokens.uss`・`Theme.uss`・`Typography.uss`・`Layout.uss`）を直す。生成物（`Tokens.uss`・`SlotTokens.uss`・`SkitTokens.uss`・`MantineTokens.uss`。1 行目が `Generated … do not edit by hand`）は手で直さない。生成物の値が `feature/uitk` の Web の CSS と違うときだけ、生成元の 1 行目に書かれたコマンド（`node tools/uitk/convert-web-tokens.mjs` 等）で作り直し、`git diff --stat` で変わったファイルを確かめて作り直した出力をそのままコミットする（`WebTokenParityTest` は名前しか照合しないので、手直しは緑のまま入ってしまう。D-37）（共有の USS は使い手の全画面を動かすので。どちらでも Step 2 の全 id の撮り直しで他の画面への波及を見る。D-34）
- (b) USS で直らず、C#・部品の作り直しが要り、差が一目で分かる（配置・色の系統・装飾の有無・文字の大きさ・欠け・重なり・はみ出し）→ 持ち主（その画面・部品のレーン。取り込み済みなら I-fix）へ bd を `--parent <bd>` で立て、依頼文（id・組の画像・測った値・Web の CSS の行）を指揮へ返し、`done.md` 1 行目を「状態: 停止（並べ比べの差 n 件の修正待ち）」にして push・合図を 1 回送って待つ。入ったら取り込み、compile のあと **該当の id だけでなく全 id を** Step 2 の 1〜5 の手順で撮り直し、変わった id 全部の組を作り直して評価役 A・B と再現を回し直す（持ち主の修正は共有の部品〈`Components/**`・`GamePanel`・`ProgressBar` 等〉に入ることが多く、USS の修正と同じく使い手の全画面を動かす。X-prerun は Web と比べられる最後の回なので、波及を後のレーンが見つけられない。D-34・D-45）。全録画の GameUi 側の流し直しは Task 14 Step 3
- (c) 同じにするための対応が大きい（部品の作り直し・数百行の描画・性能の劣化）かブラウザの癖の再現で、かつ一目で分からない → LX-210〜217 に記帳する
- (d) Web に寄せることがゴール・裁定と食い違う（Web 側が明らかに誤り・Web と同じにすると読めない等）→ `done.md`「要裁定」に Q-n（差・撮影・推奨案・帰結）で上げ、今の UITK のまま進める
- (e) Task 7 Step 3 の食い違いの表に当たる差 → 寄せない。「feature/uitk 側が正（master の Web 側の残り）」（起草時は `1-bug-report` の記述欄の幅。参照 356px・UITK 354px）は差の表の処遇を「feature/uitk 側が正（<コミット>）」にして閉じる。「master の未追従」は処遇を「I-follow へ（X-switch-pr Task 2 Step 4）」にし、`done.md`「integrator への依頼」に書く（D-37）
- (f) 状態の差（mock の fixture と実ゲームのデータの差で、Task 7 Step 3 の `states.md` に無かったもの。例 実ゲームだけにあるチュートリアルの枠・ワールドピン・アイテム）→ 寄せない・停止しない（記録だけ）。`states.md` のその id の行に足し、差の表の処遇を「状態の差（<根拠の行>）」にして閉じる。根拠の行はマスタ（ピン先の json のファイルと項目）か撮影シナリオ・mock の fixture の行で、差の表に書く。根拠の行を示せない差は状態の差にしない（(a)〜(e) で処遇する。D-44）
- 既存の LX に当たる差 → その ID で処遇し、観測を `done.md` に書く

- [ ] **Step 2: USS を直す（と、持ち主の修正を取り込んだ後の撮り直し）**

この Step の 1〜5（全 id の撮り直し）は、このレーンが USS を直したときに加えて、撮影（Task 8〜9）の後に `Client.Ui`・`UI Toolkit` を変える取り込みをしたとき（Step 1 (b)・Step 3 の LX 番号待ちからの再開・Task 14 Step 3 の `不具合` からの再開・Task 16 Step 2）にも回す。取り込みの後に回すかは Run: `UH=$(grep -m1 '撮影の HEAD（UITK）' ../moorestech_logs/harness/uitk/X-prerun/done.md | grep -oE '[0-9a-f]{40}'); git diff --stat "$UH" HEAD -- 'moorestech_client/Assets/Scripts/Client.Ui' 'moorestech_client/Assets/UI Toolkit'` が空でないかで決める（空なら回さない。D-45）。撮り直したら `done.md` の `撮影の HEAD（UITK）` を今の HEAD に替える。

直す前に `wc -l <対象の USS>` を見て、190 行を超えていれば責務で分けたファイルを先に作ってから足す（置き場と tss の `@import` の位置は Global Constraints 8。tss は自分の行だけ）。直したら:
Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Conventions|Theme|Screens\.<直した画面>)\." --timeout-seconds 570`
直した USS が `Foundation/`・`Components/` の下にあるか、2 つ以上の画面から読まれる（Run: 直した USS のファイル名を `grep -rn "<ファイル名>" "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" moorestech_client/Assets/Scripts/Client.Ui --include=*.uss --include=*.tss --include=*.cs` で読み手を数える）ときは、テストのフィルタを `^Client\.Tests\.Ui\.` 全体にする。
Expected: ErrorCount 0・全 PASS。続けて、**直した id だけでなく全 id を撮り直す**（D-34）:
1. USS を直す前・取り込む前に毎回、その時点の UITK の PNG を `$C/before/` へ写し直す（Run: `rm -rf $C/before && mkdir -p $C/before && cp $C/*.png $C/before/ && cp -R $C/video $C/before/`。前の回の `before/` は消す。比べるのはその回の修正・取り込みの前後）
2. 直した後・取り込んだ後、Task 8 Step 4（P と N）・Task 9 Step 2（E。`uitk-panel-rects.tsv` も作り直す）・Task 9 Step 3〜4（V）を同じ前置き（日本語の固定を含む）で全部流し直す
3. 静止画 id × 2 解像度と動画の 60 枚について、Run: `for f in $C/before/*.png $C/before/video/*.png; do n=${f#$C/before/}; v=$(ffmpeg -v error -i "$f" -i "$C/$n" -lavfi psnr -f null - 2>&1 | grep -o 'average:[^ ]*'); [ "$v" = "average:inf" ] || echo "変わった: $n $v"; done > $C/uss-changed.txt; wc -l $C/uss-changed.txt` で前の回と比べ、`inf` でない id を並べる。**`inf` でない id は、揺れ（世界の空・水面・ワールドピン・動画の相・カーソル）か修正の波及かを分けず、全部「変わった」に数える**（P・N・V は世界が写るので前後の PSNR がほぼ `inf` にならず、2 回撮っても揺れと波及を分ける決まった線が引けない。分けようとすると波及を揺れとして落とす。D-49）
4. 変わった id 全部（直した id・波及した他の id・揺れる id）について Task 11 Step 1 の組を作り直し、Task 11 Step 2〜3（評価役 A・B と再現）を回し直す。直した id は差が 2px 以内か画素で測る。新しい差が出たら、その差を「## 差の表」に足して Step 1 から処遇する（波及を直す USS を選び直す）
5. `done.md` の行に「直した・取り込んだもの・撮り直しの値・変わった id の一覧（`uss-changed.txt` の件数）・撮影の HEAD（UITK）」を書く。報告ページ（Task 16 Step 1）には最後の撮影の HEAD で撮った組を載せる
USS を直した回で、直した id の直した箇所の画素が前の回と同じ（切り抜いて `-lavfi psnr` が `inf`）なら、USS が読み込まれていない（tss の `@import` の漏れ・セレクタの誤り）ので直し直す

- [ ] **Step 3: 例外を記帳する**

台帳「## 見た目の例外」の末尾に 1 行（列: ID／画面・部品／差／同じにしない理由／推奨案／撮影〈報告ページの URL。PR 作成後に Task 16 で埋める〉／出所〈agent 既定（X-prerun D-4）〉）と、差のある USS の行に `/* look-exception: LX-n */` を同じコミットで入れる。C# の行が要る差は印を置けない（本番の C# を触らない）ので (b) として持ち主へ回す。使える番号は LX-210〜217 の 8 本だけ。(c) が 9 件以上になったら、残りの行の処遇を「LX（番号待ち）」にして `done.md`「integrator への依頼」に番号の追加を頼み、(b) と同じく 1 行目を「状態: 停止（LX の番号待ち n 件）」にして push・合図を 1 回送って待つ（計画・レーンは実行計画 §6.1 の割り当てを書かない。D-24）。番号が足されて再開するとき `origin/feature/uitk` を取り込んだら、Step 2 の冒頭の判定で撮り直しを回す（D-45）。
Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.Conventions.LookExceptionLedgerTest"`
Expected: PASS

- [ ] **Step 4: コミット**

```bash
git add <直した USS> docs/uitk/TODO.md   # USS を分けたときは "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" も
git commit -m "fix(uitk): 撤去の前の並べ比べで再現した差を Web へ寄せる・例外を記帳する（X-prerun）"
```
「## 差の表」の全行に処遇が入ったことを `grep -c "| *$" ` 等で空の処遇の列が 0 であることで確かめる

---

### Task 13: 撤去前の最後の突き合わせの記録（R-2）

**Files:**
- Modify: `docs/uitk/disposition.md`

- [ ] **Step 1: 流す**

Run: `node tools/uitk/test-name-parity.mjs --bundle all; echo "exit=$?"`
Expected: 束 1〜5 とも `写す予定 0 / 未対応 0`・exit 0（崩れていたら Task 1 Step 3 と同じく持ち主へ回して停止）

- [ ] **Step 2: 記録してコミット**

「## 突き合わせの記録」に束ごとに 1 行ずつ 5 行（`| <日付> | <束> | <HEAD 10 桁> | <Web> | <写した> | <処遇> | 0 | 0 |`。件数は実測）。
```bash
git add docs/uitk/disposition.md
git commit -m "docs(uitk): 撤去の前の最後の全束の突き合わせ（差分 0）を記録する（X-prerun）"
```

---

### Task 14: 最終 HEAD でラダー 6 本と UI テスト全体（R-18）

- [ ] **Step 1: テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 570`（時間切れなら `^Client\.Tests\.Ui\.[A-L]`・`^Client\.Tests\.Ui\.[M-Z]` の 2 本。Global Constraints 4）
Expected: ErrorCount 0・全 PASS（件数を `done.md` へ）。ドメインリロード中のエラーは 45 秒待って再試行。あわせて Task 10 Step 2 の `prerun/*.cs` の grep を最終 HEAD で流し直し、0 件（`guard-exit=1`）であること（R-15。D-18）

- [ ] **Step 2: ラダー 6 本と物差し**

Run（テストの TearDown が系を Web に戻したので、立て直してから。1 本ずつ前景で）:
```bash
git push
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); Client.Localization.Localize.Initialize(); return Client.Localization.Localize.TrySetChosenLanguage("japanese");'
$S/scripts/run-scenario.sh ./moorestech_client <ラダー 1 のパス>; echo "exit=$?"
```
同じ前置きでラダー 2〜6（Task 1 Step 2 のパス）と `prerun/prerun-smoke-shots.cs`。
Expected: 7 本とも exit 0・`Success: true`・ErrorLogs 0。`done.md`「## 証拠」に run dir 名と Assert の数。mp4 はラダー 6 本を報告ページ用に残し、他は消す

- [ ] **Step 3: 全録画の GameUi 側を最終 HEAD で流し直す（USS を直したとき・不具合の修正を取り込んだとき）**

Task 3 の後にこのレーンが USS を直した（Task 12・Task 15 の自動適用）か、Task 3 Step 3・Task 12 (b) で持ち主の修正を取り込んだときは、全録画の GameUi 側を今の HEAD で流し直す（Run: `PH=$(grep -m1 '全録画の HEAD（GameUi）' ../moorestech_logs/harness/uitk/X-prerun/done.md | grep -oE '[0-9a-f]{40}'); git diff --stat "$PH" HEAD -- moorestech_client/Assets .agents/skills/unity-playmode-recorded-playtest/scenarios ':!.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun'` が空なら流さない。`prerun/` は Task 3 の後に足した撮影シナリオで全録画の表の外。`PH` が空なら `done.md` の行の欠けなので Task 3 Step 2 の書き方で直してから流す。D-43）。共有の部品・トークンの USS は要素の寸法と重なりを変え、testid のクリックと可視判定（`GameUiElementQuery` の `worldBound` と `panel.Pick`）に効く。流し直さないと、X-5a（R-16・Task 9）と X-switch-pr（R-9）がこのレーンの USS の修正で落ちた行を自分の退行として追う（D-38）。
Task 3 Step 1〜3 を同じ手順（一覧から手動投入の行と `uitk/prerun/` を外す・3 本ずつ・落ちた行の 1 回の流し直し・Step 2b の手動投入・分類・`不具合` なら停止）で流し、`$D/gameui-final.tsv` に書き、流した HEAD をその場で `done.md` の `全録画の HEAD（GameUi）` の行へ書き替えて logs でコミット・push する（D-43）。`不具合` で停止して持ち主の修正を取り込んだら、Task 12 Step 2 の冒頭の判定で撮り直しも回す（D-45）。`done.md`「## 全録画の結果」の UITK の分類の列をこの結果へ差し替え（表の行の集合は Task 3 と同じ。Task 5 Step 1 の python と `Web 専用` の数の確かめを流し直す）、Task 3 の結果から分類の変わった行を「### Task 3 からの変化」の小節に残す。台帳「## 実測の結論」に Task 5 Step 2 の UITK 側の行の形で 1 行足す（Task 16 Step 1 の後にコミット）。

- [ ] **Step 4: コミットは無し**（記録のみ。台帳の行を足したときは `git add docs/uitk/TODO.md && git commit -m "docs(uitk): 最終 HEAD の全録画の UITK 側を台帳に記録する（X-prerun）"`）

---

### Task 15: moores-code-review で全ブランチレビュー（必須）

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。base は `origin/feature/uitk`。設計判断は agent 既定で推奨案を採り、判断記録に「agent 既定（moores-code-review Dn）」と書く（ゴール・裁定から外れるものだけ `done.md`「要裁定」）。Workflow の自動適用の後は対象テスト（Task 12 Step 2・Task 14 Step 1 のフィルタ）を流し直す（`docs/development/worktree-and-review-pitfalls.md`）。自動適用・反映がシナリオの判定（Assert・待ち）や USS に触れたら、Task 14 Step 2 の 7 本と、触れた撮影シナリオを反映後のバイナリで流し直し、USS なら Task 12 Step 2 の 1〜5（全 id の撮り直し・直す前との PSNR・変わった id の組の作り直しと評価役・再現）と Task 14 Step 3（全録画の GameUi 側の流し直し）を回してから完了とする（実行計画 §4.3 の 1。D-34・D-38）。

---

### Task 16: feature/uitk への PR・取り込み・マージ・片付け・報告（必須）

セッション終了可能状態にすること: pr-createスキルでPRを作成し、コンフリクトがあれば解消・コンパイル確認のうえpushする。このレーンは実行計画 §4.3 の形（base=feature/uitk、agent がマージ）。

- [ ] **Step 1: PR**: pr-create で `base=feature/uitk`。報告ページ `https://review.moores.tech/pr/<番号>/report/` に、`~/hermes-agent/data/services/pr-review/publish/README.md` の manifest の形で、並べ比べ（`$C/pairs/` を束ごとの `group`。caption `Web <id> / UITK <id> <解像度>`。全部の組を最後の撮影の HEAD（`done.md` の `撮影の HEAD（UITK）`）で撮った版にする〈USS を直した後・持ち主の修正を取り込んだ後は Task 12 Step 2 で全 id を撮り直してあるので、前の回の組を載せない。Step 2 の取り込みで撮り直したら載せ直す〉。X の id は組を作らず `verification` に [A-38] の根拠の 1 行）・動画の 3 フレームの組・ラダー 6 本の `recording.mp4`・`prerun-smoke-shots` の 1280×720 の 10 枚を載せ、`verification` に全録画の 2 行（台帳の文面）・突き合わせ・テストの件数を書いて `python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <番号> <manifest.json>` で公開し、本文の先頭行をその URL の h1 にする。公開に失敗したら報告 URL 無しで完了にしない。LX を足したら台帳の撮影の列に報告ページの URL を入れてコミット・push
- [ ] **Step 2: 取り込み**: `git merge origin/feature/uitk`。台帳・USS の衝突は両方の行を採る。`uloop compile`・Task 14 Step 1 のテスト・G1 ローカル（`python3 .github/scripts/check-uitk-web-follow.py --base origin/feature/uitk --head HEAD` が「違反なし」、かつ束ごとの「未記帳 n 件」が Task 1 Step 3 で `done.md`「## 前提」に写した件数と同じ。取り込みで feature/uitk 側に Web のコミットが増えていれば、同じコマンドを `--head origin/feature/uitk` で流した件数と同じ。このレーンは `moorestech_web/` のコミットを足さない）・`node tools/uitk/test-name-parity.mjs --bundle all; echo "parity-exit=$?"`（exit 0。束ごとの件数が Task 13 の 5 行と違えば、`disposition.md`「## 突き合わせの記録」の自分の 5 行を取り込み後の HEAD と件数で書き直してコミットしてから push。差分が出たら Task 1 Step 3 と同じく持ち主へ回して停止。R-2）。取り込みがシナリオ・USS の判定に触れた衝突ならラダーと物差しの再走。取り込みで `全録画の HEAD（GameUi）` から最終 HEAD までに USS・シナリオが変わったら（Run: `PH=$(grep -m1 '全録画の HEAD（GameUi）' ../moorestech_logs/harness/uitk/X-prerun/done.md | grep -oE '[0-9a-f]{40}'); git diff --stat "$PH" HEAD -- 'moorestech_client/Assets/Scripts/Client.Ui' 'moorestech_client/Assets/UI Toolkit' .agents/skills/unity-playmode-recorded-playtest/scenarios ':!.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun'`）、そのファイルの一覧を `done.md`「## 全録画の結果」の HEAD の行の下に「全録画の HEAD から最終 HEAD までに変わった USS・シナリオ（他レーンの取り込み）」として書く（5a・X-switch-pr が退行の切り分けに読む）。続けて Task 12 Step 2 の冒頭の判定（`撮影の HEAD（UITK）` から今の HEAD までの `Client.Ui`・`UI Toolkit` の差）が空でなければ、Task 12 Step 2 の 1〜5（全 id の撮り直し・前の回との PSNR・変わった id の組の作り直しと評価役・再現・差の処遇）を回し、Step 1 の公開のコマンドで報告ページを載せ直す（他レーンの部品の変更も使い手の全画面を動かし、X-prerun が Web と比べられる最後の回なので。D-45）。push
- [ ] **Step 3: G1 とマージ**: PR の `UITK Web follow gate (G1)` が pass を見て `gh pr merge <番号> --merge`（Squash 禁止）
- [ ] **Step 4: 片付け**: `moores-wt status` で自分の Editor だと確かめて Editor を閉じる。全録画と控えの `moorestech_client/.uloop/outputs/X-prerun/`・`PlaytestResults/`・TestResults・`$C`（報告ページに載せた後）を消す。テストで作ったワールドを `list-work-saves.sh` で探して消す。winunity の一時ブランチ `uitk-tmp/prerun-s4g-base` が残っていれば `git push origin --delete` で消す。bd `moorestech-wwxy` を close（理由に PR URL と「10 行すべて片付いた」）・`<bd>` を close。worktree の `moores-wt rm` は指揮が行う
- [ ] **Step 5: 報告と合図**: `done.md` の 1 行目を「状態: 完了」（止まったら「状態: 停止（理由）」）にし、完了条件（R-1〜R-20）ごとの証拠・PR URL・未完了・要裁定・integrator への依頼（道具の口の不足・申し送りの「回した」行・Task 7 Step 3 の「master の未追従」の差〈X-switch-pr の I-follow へ〉・Web 側の `退行` の一覧〈記録だけ。D-36〉・A-250/A-251 の下書きは本計画の末尾にある旨）・5a への申し送り（Web 専用の一覧・物差しの使い方）を書いて `../moorestech_logs` でコミット・push。最後に `cmux wait-for -S uitk-x-prerun` を 1 回

## Self-Review

- Requirements の各行のタスク: R-1→T1（wwxy の 8 行は T1 Step 3・T2 Step 4）、R-2→T1 Step 3・T13・T16 Step 2、R-3・R-4→T2、R-5→T2・T5・T16 Step 4、R-6→T3（手動の行は T3 Step 2b）・T5 Step 1・T14 Step 3、R-7→T4（手動の行は T4 Step 2b）・T5、R-8→T5・T10 Step 3、R-9→T6、R-10→T7、R-11→T7 Step 4・T8・T9、R-12→T9、R-13→T11、R-14→T12・T16 Step 2、R-15→T10、R-16→T2・T5・T6、R-17→T1 Step 3b・各タスクと T16、R-18→T14、R-19→T1 Step 3・T16 Step 2〜3、R-20→T15・T16
- 保留の経路（停止して待つ）: 前提の欠け（T1）・突き合わせの差分（T1・T13・T16 Step 2）・全録画の UITK 側の不具合（T3・T14 Step 3）・USS で直らない差（T12 (b)）・LX の番号の枠切れ（T12 Step 3）。Web 側の失敗はどの分類でも停止せず記録だけ（T2 Step 3・T4 Step 3。D-36）。どれも `done.md` 1 行目・bd・合図 1 回で指揮に届き、解消の条件は「持ち主の修正が `feature/uitk` に入る」（枠切れは「番号が足される」）。撮れない E の id（T9 Step 2）は停止せず `done.md`「要裁定」の Q-n で届く。状態の差（T12 (f)）は停止せず `states.md` と差の表に記録だけ。停止の間に Editor を閉じても全録画の TSV・控えは `.uloop/outputs/X-prerun/` に、全録画の HEAD と G1 の起点は `done.md` に残る（D-43）。入った後にこのレーンが該当分だけ流し直す経路を各タスクに書いた。最小構成: 不具合 0 件なら停止に入らない。全 id が P のときも E の一致の確かめ（T9 Step 1）は `4-block-chest` が N なので必ず走る
- 対照: 物差しは 2 回流して決定的かを見る（T10 Step 2）。EditMode は先に実行時と、panel の寸法と panel 原点からの相対位置で 2px 以内の一致を 1280×720 と 2560×1080 の両方で見る（T9 Step 1。絶対位置は比べない。D-46）。全録画は両系とも exit 0 以外の行を 1 回流し直し、2 回とも落ちた行だけを分類する（T3 Step 3・T4 Step 2。D-36）。Web 側の失敗は S4-gamma の HEAD で 2 回まで流し直して既存と退行を分け、記録だけにする（T4 Step 3）。USS を直したか修正を取り込んだら、全録画の GameUi 側を最終 HEAD で流し直し、表と `全録画の HEAD（GameUi）`〈40 桁〉を差し替える（T14 Step 3。D-38）。USS を直したら・撮影の後に `Client.Ui`・`UI Toolkit` を変える取り込みをしたら全 id を撮り直し、前の回と PSNR で比べて `inf` でない id を全部（揺れと分けずに）並べ比べ直す（T12 Step 2・T15・T16 Step 2。D-34・D-45・D-49）。GameUi だけで落ちた assert は Web 側の同じ行と並べて不具合と前提の崩れを分ける（T3 Step 3。D-29）
- 撮影の条件: 撮影の前は毎回、前置きと各シナリオの冒頭で表示言語を日本語に固定する（Global Constraints 5・T8〜T10・T9 の EditMode。Web の参照は日本語。D-30）。透かしは入れない（D-39）。足した枠とラベルの待ちは足す前の数より増えたことで見る（チャレンジ 1 の本物の枠で最初から真にならない。T8 Step 3・T9 Step 3・T10。D-47）。`prerun/` のシナリオは `ScenarioTimeoutSeconds = 360f`（`run-scenario.sh` の 420 秒より短く。ブロックは 7 種ずつ 2 本。D-40）
- 並べ比べの基準: 動画の組は両解像度で位相を数えず、効果の出始め・通知の 0.5 秒・実ゲームのワールドピンを `states.md` の状態の差に書く（T7 Step 3・T9 Step 4・T11 Step 2。D-35）。実ゲームだけにあるチャレンジ 1 の枠とラベルは状態の差に名指しで書き、`states.md` に無かった状態の差は (f) で閉じる（T7 Step 3・T12 (f)。D-44）。E は両側を面の矩形に切り抜き、位置は面の原点からの相対で、外殻の外は数えない（T7 Step 3〜4・T11 Step 1〜2。D-46）。参照（master）と `feature/uitk` の Web の食い違いを先に洗い出し、「feature/uitk 側が正」「master の未追従」の差は寄せない。生成物の USS は手で直さない（T7 Step 3・T12 (a)(e)。D-37）
- 担い手: Editor の起動・停止・付け替えは本体、subagent は起動済みの Editor だけを使う（Global Constraints 3・T1 Step 4・T4 Step 3。D-41）。評価役は 1 回 24 組まで（T11 Step 2）。10 分を超えうる `capture.ts` は本体が background で 1 回起動し、流し直す前に出力先を消す（T6 Step 2〜3）
- 撮り方の区分: P/N/E/V に X（[A-38] で UITK に作らない画面。撮らない）を足し、機械検査は X の根拠（A-38 と disposition の行）の欠けも誤りに数える（T7 Step 2。D-31）。言語ゲートは起動後に出し直せないので E（T9 Step 2）
- 他レーンとの境界: 5a は「## 全録画の結果」の `Web 専用` を grep して消す。この計画は `Web 専用` の文字列をシナリオ全体が Web 専用の行だけに使い（T5 Step 1 で grep して確かめる）、assert の書き直しは別の小節で申し送る（D-29）。表と小節のパスは `scenarios/` からの相対にそろえ、python で突き合わせる（T5 Step 1）。X-switch-pr の `<PRERUN_HEAD>` は `done.md`「## 全録画の結果」の `全録画の HEAD（GameUi）` の 40 桁（T5 Step 1・T14 Step 3）。前のレーンの `done.md` の申し送りは T1 Step 3b で全部受ける。UITK の分類の欄はちょうど `Web 専用` で、その数と「### 消す」の数を X-5a と同じ式で突き合わせる（T5 Step 1）。物差しは「### 物差し」の小節に書き、X-5a の「### 書き直す」の抜き出しに混ざらない（T10 Step 3）。手動投入の 2 本と運転役の言語ゲートは一覧から外して X-switch-pr R-9 と同じ語で判定する（T3 Step 2b・T4 Step 2b。D-48）。全録画の一覧は Task 3 と Task 14 Step 3 で同じ集合（`uitk/prerun/` を外す）
- 型の一致: `UiRuntimePanelCapture.CaptureToPng(VisualElement, int, int, Color, UiCaptureLocalization, string)`・`PlaytestGameViewOps.SetResolution(CancellationToken, int, int)`・`TutorialPresentationStateStore.Instance.AddOutlineHighlight(string, Guid)`・`AnchorIds.PauseSettings`／`ResearchNode(string)`／`ChallengePanel`・`GameUiRoot.Instance.GetRootElement()`・testid `research-node-state-<guid>`・`block-inventory-panel`・`block-inventory-close`・`placement-mode-hud`・`inventory-panel`・`research-tree`・`pause-menu-open-settings`・`language-select`・`build-menu-search`・`p.RemoveBlock(Vector3Int) : UniTask<bool>`・`PlaytestSetup.GroundCenter`／`GroundSize` は起草時の HEAD `9c83052273` の実物と一致。S5 のレーンが足すもの（チャレンジ一覧の中身・言語ゲート）は T8 Step 3 で実物から写す

## 判断記録（ADR）

従う裁定: `.decisions/2026-11-06-*`（UITK 6 本と CEF 停止の 7 本。特に「一気切替の日に CEF と Web を同時に撤去する」＝撤去の後は Web 側が無いので、ここが Web と比べられる最後の回）。上書きする裁定: `.decisions/2026-10-16-moores-writing-plansの実装引き継ぎはCodexセッションに固定する.md` と実行計画 §8 E-3（D-17）。依頼文の指示: Codex を使わない・Mac Editor 1 本・問いは agent 既定・worktree の片付けは指揮。判断台帳: このレーンの Modify/Create は `moores-*.md` の `paths` に当たらない（Global Constraints 11）。unityプレイ録画テスト: 含む（Task 2・3・4・8・9・10・14）。

W6 の agent 既定の写し:
- **W6-1** Codex を使わない。§5 の外部監査・codex-audit は opus の評価役 2 系統の独立評価。pr-independent-review はスキルが削除済み（ed461b35de、2026-10-30）。根拠と上書きする裁定は D-17
- **W6-2** Mac Editor 1 本・それ以上は winunity（ユーザー指示 2026-11-11「やはりunity2つは安定しないので1つに絞ってほしい」。`../moorestech_logs/harness/uitk/conductor.md`「## 2026-11-11 21:30 Mac Editor を 1 本に」）
- **W6-3** 「全画面」は参照 manifest の全 id（起草時 89 件・静止画 84・動画 5）。撮影表に無くレーンが自撮りした場面（掴み絵 `4-player-inventory-grab`・`3-modal-backdrop-only`・列車族 6・特殊族 `self/*`）は各レーンの PR の並べ比べを証拠にし、撮影表への追加の依頼は「撮影表ごと 5c で消える」で閉じる
- **W6-4** Web 参照の 2560×1080 は master の worktree で `shotRunner.ts:11` の `referenceViewport` を一時的に書き換え、`pnpm build` の後に撮り、コミットしない（前例 S3-ladder）
- **W6-5** UITK 側の撮影は id → 撮り方の対応表を作り、全 id が 1 行ずつあることを機械で確かめる。PlayMode は既存の段の撮影シナリオを流し、足りない id は `scenarios/uitk/prerun/` に新設（200 行未満・10 ファイルまで）。実行時に作れない状態は EditMode の `UiRuntimePanelCapture`（実行時に作れる状態 1 つで 2px 以内の一致を先に確かめる。[A-164]）。動画 5 件は UITK 側を録画し、Web の webm と同じ時刻の 3 フレーム（始・中・終）を ffmpeg で抜いて比べる
- **W6-6** 撤去レーン（X-5a〜5e）の見た目の不変の確かめ: (a) 部品ギャラリーの全 entry を `UiRuntimePanelCapture.CaptureToPng` で撮り PSNR `inf`、(b) X-prerun が作る `prerun-smoke-shots.cs`（束ごとに 2 状態・計 10 状態）を両解像度で撮る。X-prerun が作るのは (b) の物差し
- **W6-8** シナリオの撤去に伴う書き換え・削除は全部 5a が持つ。X-prerun は `prerun/**` と wwxy の 2 本だけを書き、Web 専用の一覧を申し送る
- **W6-9** 全録画のスクリプトは X-prerun が使うだけで書き換えない。5a が系の引数・`UiSystemEditorSwitch`・wwxy の除外を外す
- **W6-18** 番号: A-250 X-prerun の撮り方と独立評価／A-251 撤去前の最後の Web の基準。LX-210〜217 は X-prerun（LX-218〜219 は X-switch-pr）

計画の判断（agent 既定）:
- **D-1** Web 側の全録画は、Windows の Editor で CEF が描けるか（`misc/cef-webui-showcase.cs` を `--ui-system Web` で 1 本）を先に試し、描ければ winunity で GameUi と並走、描けなければ Mac で GameUi の後に流す（依頼文の指示。Mac の Editor を 2 本にしない）
- **D-2** `prerun/` のシナリオは全体スイッチと Web の型を参照しない。UITK で描かれていることは `GameUiRoot` の要素で確かめる（物差しは 5a〜5e の全 HEAD でそのままコンパイルできる必要があり、他の撮影シナリオも 5a の書き直しの数を増やさないため。前例のシナリオの `ActiveUiSystem == GameUi` の Assert は 5a で消える形）
- **D-3** 評価役 2 系統（A・B）は束ごとに派遣を分ける（全 id × 2 解像度の画像は 1 体のコンテキストに収まらない）。渡すのは左 Web・右 UITK を横に並べた合成画像（同じ状態の 2 枚を別々に渡すと位置の取り違えが起きる）。A と B は互いの結果を見ず、指摘は別の subagent が画素で再現したものだけ残す（前例 S3-ladder D-1・S4-gamma Task 13 Step 5）
- **D-4** 差の処遇は (a) USS で直す（このレーン）／(b) USS で直らず一目で分かる差は持ち主へ bd して停止／(c) 対応が大きいかブラウザの癖で一目で分からない差は LX-210〜217／(d) Web に寄せるのがゴール・裁定と食い違う差は Q-n。本番の C# に印や修正が要るものは (b)（このレーンは本番の C# を触らない。ファイルの持ち主の表）
- **D-5** 参照（mock-host の fixture）と実ゲームの撮影の状態の差は先に `states.md` に書き、状態に依らない観点（配置・寸法・色の系統・装飾・文字の大きさと段組・余白）だけを比べる（前例 S4-gamma Task 13 Step 2b）。N のブロックは mock の fixture と中身が違うので、比べるのは器（枠・題・区画の並び・余白・色）だけ
- **D-6** Web 側の新しい失敗の流し直し（S4-gamma の HEAD）は、その SHA を指す一時ブランチ `uitk-tmp/prerun-s4g-base` を push して winunity で流す（Mac の Editor を 2 本にしない）。Windows で CEF が描けなければ自分の Editor を閉じてから Mac の使い捨て worktree で流し、畳んでから戻す
- **D-7** state-label の書き直しはピン先マスタ e0c913033c の原始研究 1〜3（`858bcb10`・`b47c5e3c`・`bc5e7786`）で、素材は原始研究1 に木の板 3・木の棒 3・砕いた石材 2、原始研究2 にレンガ 30（`research.json`・`items.json` で確かめた）。`CompleteResearch` はサーバーが素材と前提を検証して拒否を通知する（`OperationDeniedNotificationTest`）ので、元のシナリオと違い原始研究1 の素材も渡す。ピンが動いたら同じ照会で選び直す
- **D-8** 書き直した state-label は、台帳の行の直すレーンの列が `X-prerun` のまま残るので `list-recordings.sh` が外す。スクリプトは書き換えず（W6-9）、全録画の一覧へ明示で足す
- **D-9** 旧 `connect/electric-wire-connect-via-ui.cs` だけが見ていた検証を後継へ移すかは、シナリオの持ち主が 5a（W6-8）なので、このレーンは bd を立てて回す
- **D-10** 停止するときは `done.md` 1 行目・bd・`cmux wait-for -S uitk-x-prerun` を 1 回送り、指揮の再開の連絡を待つ（指揮が止まったレーンに気づく経路。最後の合図と同じ名前）
- **D-11** UITK の撮影は P（既存の段の撮影シナリオを書き換えずに流し、出力名を対応表で `<id>-<w>x<h>.png` にそろえる）・N・E・V の 4 種。既存のシナリオの出力名を直さない（シナリオは 5a の持ち物）
- **D-12** 物差しは 2 回流して PSNR を見る。世界の動きで揺れる状態は待ちを延ばし、それでも揺れれば 5a〜5e の比べ方を要素の矩形の差 0 に替える旨を申し送る（W6-6 の「差があれば表にして原因を書く」の前提を固める）
- **D-13** 物差しの 10 状態は、束 1: `1-pause-top`・`1-settings`、束 2: `2-game-hud`・`2-placement`、束 3: `3-build-menu`・`3-build-menu-search`、束 4: `4-player-inventory`・`4-research`、束 5: `5-challenge-list`・`5-tutorial-outline-label`（キーと testid で決定的に作れ、撤去で触る入力排他・起動配線・ロケータに近い画面を選んだ。出展モードの言語ゲートは起動の前段を要するので外した。並べ比べでは E で撮る。D-31）
- **D-14** §4.3 の 5（`moores-wt rm`）は依頼文の「worktree の片付けは指揮が行う」に従い、このレーンは Editor を閉じるまで
- **D-15**（独立評価 2 周目で改めた。D-37）申し送りのバグ報告の記述欄の幅は、`feature/uitk` の Web（`tokens.css:149`）と UITK（生成物 `Tokens.uss:59`）が I-fix2 ad2ff7d2ae で両方 354px に直り（台帳「## 追従記録」束 1 `写した`）、master の Web だけが 356px で右枠が切れる。参照（master）だけが切れるので、Task 7 Step 3 の食い違いの表に「feature/uitk 側が正（master の Web 側の残り）」と書いて並べ比べの差から外し、UITK を寄せない・Q-n にも上げない（起草時の「Web と UITK が同じ見た目」は実物と合わなかった）

検査の反映（Phase 2.6・user-simulator・設計検査の注目点）:
- **D-16**（Phase 2.6 検査7・1行目 [強]: 設計書 §3.3「並べ比べ」の行と [A-179] は「codex-audit → 別の subagent が再現 → 指揮が判定 → 台帳の行と印は I-fix が置く」。計画はどちらを正にするか → X-prerun（integrator レーン）が判定の取りまとめと USS の修正・LX の記帳〈台帳の行と `/* look-exception */` の印を同じコミット〉を行う。判定は opus 2 体の独立評価、再現は別の subagent。USS で直せない差〈部品の作り直しが要るもの〉は持ち主の部品の範囲として bd を立てて指揮へ返し、停止して待つ。理由: W6 は撤去の直前で、段の締めの I-fix レーンはもう無く、直列のクリティカルパスに別レーンを挟むと撤去が止まる。A-179 が I-fix に印を置かせた理由〈`LookExceptionLedgerTest` が同じ HEAD で台帳と印を照合する〉は、このレーンが同じコミットで両方を入れるので満たす。A-250 の下書きにこの担い手の移りを書いた。出所 agent 既定〈指揮の補足〉）
- **D-17**（設計検査 注目点5: 「Codex を使わない」が実行計画 §3 の「integrator レーンは Codex セッションで回す」と逆向き → 根拠はユーザーの指揮への指示で、`../moorestech_logs/harness/uitk/conductor.md`「## ユーザー指示（2026-11-06 15:55）」の原文「一旦codexは使わず、claudeセッションで今からこの計画の遂行をして」と次の行「実行計画 §4.1 の Codex 指定をこの指示で上書き」、および「## 2026-11-11 21:30 Mac Editor を 1 本に」の「やはりunity2つは安定しないので1つに絞ってほしい」。これが `.decisions/2026-10-16-moores-writing-plansの実装引き継ぎはCodexセッションに固定する.md` と実行計画 §8 E-3〈integrator レーンも Codex セッションで回す〉を W6 について上書きする。出所 ユーザー指示〈指揮の記録〉を agent が引用）
- **D-18**（Phase 2.6 5-E・1行目 [弱]: 「全体スイッチと Web の型を参照しない」〈D-2〉を確かめるガードが無い。型で強制するかテストに載せるか → `prerun/*.cs` に `UiSystem|ActiveUiSystem|UiSystemEditorSwitch|WebUiHost|PlaytestDomQuery|Cef` が 0 件であることを Task 10 Step 2 と Task 14 Step 1 の grep で確かめ、R-15 の受け入れ基準に入れる。シナリオは asmdef の外で型の参照を禁じる仕組みが無いので grep にした。出所 agent 既定）
- **D-19**（シミュレーター 適用推奨1・設計検査 注目点7・シミュレーター Warning 3: ブロック 14 種を x=8〜216 に並べると 50×50 の足場の外に出て開けず、逃げ道が座標の誤りを「開けないブロック」と読み替える。`ScenarioTimeoutSeconds = 900f` は 1 回の Bash の 600 秒を超える。panel の矩形の `p.Note` を Task 8 の済んだコミットへ後から足す文になっていた → 足場の内側の (-6,32,-6) へ 1 個ずつ置いて 2 解像度を撮り `p.RemoveBlock` で消して次へ進む形に書き直し、逃げ道を「足場の上で開けなかったものだけ E」へ絞り、上限を 600 秒にし、`p.Note` を Task 8 Step 2 のコードに最初から入れた〈上限の値と根拠は独立評価 2 周目で D-40 に改めた〉。出所 シミュレーター予測（agent 前提・拒否権つき）と設計検査）
- **D-20**（設計検査 注目点6: `Guid` を使うシナリオに `using System;` を足すと裸の `Object` が CS0104 であいまい → 計画のコードの `Object.FindObjectsByType` を全部 `UnityEngine.Object` にし、写し元の本文を変えずに写す `prerun-shots-pause-train.cs` には `using System;` を足さないと書いた。出所 agent 既定）
- **D-21**（シミュレーター 適用推奨2: 経路 W の `while read` のループの中で `~/bin/winunity` の ssh が標準入力を読み切り 1 本しか流れない〈`~/bin/winunity` 64・80・87 行の ssh に `-n` も `</dev/null` も無い〉→ ループの中の呼び出しに `</dev/null` を付けた。出所 シミュレーター予測（agent 前提・拒否権つき））
- **D-22**（シミュレーター 適用推奨3: `L` が相対のまま `cd $R/moorestech_web/webui` するので、`capture.ts:19-22` が参照を使い捨て worktree の中に作り `moores-wt rm` で消える。Step 3 に `cd` が無い → `L` をレーンの worktree で `pwd -P` の絶対パスに決め、Step 2・3 の先頭に `cd`、畳む前に 2 つの manifest の実在を見る手順を足した。出所 シミュレーター予測（agent 前提・拒否権つき））
- **D-23**（シミュレーター Warning 1: Task 9 Step 2 の逃げ道〈撮れない E の id を各レーンの PR の並べ比べで代える〉と R-11「全静止画 id の PNG が実在」が食い違う → 逃げ道を使った id は R-11 未達として数え、`done.md`「要裁定」に Q-n で上げる。実行計画 §1.5 の「UITK の全画面と並べ比べ」を省くのはユーザーの判断なので、実行時に起きたら上げる。起草の段階では件数が 0 か分からないので計画は止めない。出所 シミュレーター予測（agent 前提・拒否権つき））
- **D-24**（シミュレーター Warning 2: 例外の番号は LX-210〜217 の 8 本だけで、9 件目の扱いが無い → 9 件目からは「LX（番号待ち）」にして `done.md`「integrator への依頼」で番号の追加を頼み停止する。計画・レーンは実行計画 §6.1 を書かない〈Global Constraints 7〉。出所 シミュレーター予測（agent 前提・拒否権つき））
- **D-25**（Phase 2.6 5-C・5行目 [弱]: 研究の照会が GUID の欠けを `None None None` で出して exit 0 → 欠けを明示の分岐にし、無ければ一覧を出して exit 1。出所 agent 既定）
- **D-26**（設計検査 注目点1: state-label の `AssertStateLabel` は自前の `realtimeSinceStartup` の期限付きループで、前例は `p.Until(() => FindUi(…).GetAwaiter().GetResult()…)` → このままでよい。`p.Until` は時間切れで投げて観測した文言が残らず、Task 2 Step 3 の「Note の `text=` で状態の取り違えを見る」が使えなくなる。時間の規約はサーバーのゲームロジックだけが対象。出所 agent 既定）
- **D-27**（設計検査 注目点3・4: A-179 が棄却した「mock-host の参照だけと比べる」を X-prerun が採る理由と、A-177 の 6 分類を広げた分類〈`比較不能（新設）`・`比較不能（変更）`・「シナリオの誤り（5a）」〉が設計書に無い → A-250 の棄却案に「実ゲームで Web と UITK を全画面撮る」を足して理由を書き、A-251 に広げた分類を書いた。出所 agent 既定）
- **D-28**（設計検査 実物2・シミュレーター Warning 4: 「現存する 8 本（S2/S3-ladder の 6 本と state-label・後継 extend-chain）」の内訳が台帳と合わない〈台帳では S2-ladder 4・S3-ladder 3・state-label で 8、extend-chain は行の外の後継〉→ 括弧を実物の内訳に直し、extend-chain を別に書き、Task 5 Step 1 で 10 行を 1 行ずつ「現存／削除／後継」に分けて数え直す形にした。出所 設計検査とシミュレーター予測（agent 前提・拒否権つき））

独立評価 1 周目の反映:
- **D-29**（独立評価 1 周目: `developer-report-delivery.cs` を先回りで「Web 専用（assert）」と決めると、5a の `grep "Web 専用"` → `git rm` に拾われて、5a が v7 へ書き直すはずのシナリオが消える。実箱ロックの NRE は C2-b:71 で原因が未切り分けで、Web 専用と決める根拠が無い → 先回りで分類せず、他のシナリオと同じく流した結果で分ける。`前提つき` はヘッダの手順で流し直し、落ちた assert が webUiHealth だけなら `前提つき（5a が assert を書き直す）`、他の assert が GameUi だけで落ちれば `不具合`、Web でも落ちれば `前提つき（両系で落ちる）`。`Web 専用` の文字列はシナリオ全体が Web 専用の行だけに使い、5a への申し送りを「消す」「書き直す」の 2 小節に分ける。5a の grep との突き合わせは X-5a へ依頼する。出所 agent 既定〈指揮の補足「先回りで Web 専用に分けず、流した結果で分類する」〉）
- **D-30**（独立評価 1 周目: Web の参照は mock-host の既定で日本語〈`topicFixtures.ts:44`〉、ゲームは `PlayerPrefs` の言語〈無ければ english。`Localize.cs:14,35-37`〉で、P の `uitk-notification-compare.cs:57` は最後に english を保存する。言語が撮影ごとに揺れ、b5 の「和文のロケール」の Assert と state-label の文言の Assert が落ち、日本語の Web と英語の UITK を比べ、5a〜5e の物差しの PSNR に言語の差が混ざる → 撮影の前置き〈Global Constraints 5〉で毎回 `Localize.Initialize()` → `TrySetChosenLanguage("japanese")` を流して `true` を見る。`prerun/` の 5 本と state-label は冒頭でも `p.Assert(Localize.TrySetChosenLanguage("japanese"), …)`〈前例 `connect/rotary-generator-ui.cs:54`〉。E は `CaptureToPng` の前に日本語を保存する〈`InitializeDictionary` の `Localize.Initialize()` が `PlayerPrefs` を読む〉。物差しのヘッダに「日本語で撮る」を書いた。出所 agent 既定〈指揮の補足「表示言語は撮影の前に毎回日本語へ固定する」〉）
- **D-31**（独立評価 1 周目・別 subagent が再現: (1) `5-reconnect` は [A-38]・実行計画 E-17 で UITK に作らない画面なのに E に割り当てていて、外殻が無いので Task 9 Step 2 の逃げ道から Q-n でユーザーに上がり、agent 既定で決まっている「比べる相手が無い」を裁定に回してしまう。機械検査に作らない画面の区分が無い。(2) `5-event-language-gate` はゲームの DI より前に `MainGameInitializationFinalizer` から 1 度だけ出る全画面〈`EventLanguageGate.cs:18`。待ち状態へ戻す口が無い〉で、起動後のゲームで出し直す公開の口が無く、b5 の 2 解像度ループの中では出せない。D-13 とも食い違う → 撮り方に X〈UITK に作らない画面。撮らず、出所に [A-38] と disposition の行〉を足して機械検査で通し〈根拠の欠けは誤り〉、R-11 と Task 11 の組から外す。言語ゲートは E にし、S5-gate のゲートの L1 の土台の外殻に待ち状態の Model を渡して撮る。b5 からゲートの区画を消した。出展モードで起動し直す道は `EventModeAutoStartActions` が既定のワールドを消すので採らない。出所 agent 既定〈独立評価の指摘を別 subagent が実物で再現〉）
- **D-32**（独立評価 1 周目: 完了条件は「720p・2560×1080 で並べ比べ」なのに動画 5 件は 1280×720 だけを比べていた。録画の出力は 1280×720 に固定〈`PlaytestRecorder.cs:33`〉 → 2560×1080 は同じ操作をもう 1 周し、Web の `durationMs` の 10%・50%・90% の時刻に UITK の静止画を撮り、Web の 2560×1080 の webm〈`videoCapture.ts:14` は `referenceViewport` で録る〉から同じ相を抜いて比べる。相は数フレームずれうるので、2560×1080 の動画の組では評価役に動きの位相を数えさせない〈独立評価 2 周目で両解像度へ広げた。D-35〉。DSL に録画の解像度の口は足さない〈撮影の道具に口を足さない。やらないこと〉。出所 agent 既定）
- **D-33**（独立評価 1 周目: Web の動画は `visible(...)` で要素が出たのを確かめてから撮るのに、UITK 側は Note を打って待つだけで、mock-host の DragGuide の始点のアイテムが実ゲームの持ち物に無ければガイドが描かれないまま撮れる → 各動画で Web の `steps` と同じ要素〈`notification-row`・`key-control-hint`・`world-pin__arrow`・`tutorial-drag-guide`・`tutorial-highlight-label`〉が出たことを `p.Until` で確かめてから相を数え、DragGuide の始点は実ゲームの持ち物のレシピ一覧にあるアイテムにして始点のセルの存在も確かめる。出所 agent 既定）
- **D-34**（独立評価 1 周目〈評価役 2 体とも〉: 差を Web へ寄せる USS は部品の USS〈例 `Components/Controls/FormButton/FormButton.uss` は BugReportForm・ModalHost・ChallengePanel・EventLanguageGateBody が使う〉やトークンであることが多いのに、撮り直すのが直した id だけで、他の画面への波及が「差 0」のまま PR に入り、X-prerun が Web と比べられる最後の回なので後のレーンが見つけられない → 最初の修正の前に UITK の PNG を `before-uss/` へ写し〈独立評価 3 周目で回ごとの `before/` に改め、持ち主の修正の取り込みにも広げた。D-45・D-49〉、直すたびに全 id〈P/N/E/V〉を撮り直して PSNR で直す前と比べ、変わった id 全部の組を作り直して評価役 A・B と再現を回し直す。直す場所は画面の USS を先に選び、部品・トークンは画面で直せないときだけ。報告ページは最終 HEAD の組だけを載せる。Task 15 の自動適用が USS に触れたときも同じ手順。出所 agent 既定〈指揮の補足「USS を直したら全 id を撮り直して PSNR で変わった id を並べ比べ直す」〉）
- 軽の指摘の直し（判断を伴わない）: `moores-wt rm` の引数を worktree の名に（`uitk-prerun-ref`・`prerun-s4g-base`。`resolve_worktree` はパスか `WT_ROOT/<名>` しか引かない）・経路 M の一時 worktree の録画の前に `moorestech_web/node` のコピーと `pnpm install`・写し元の行の範囲（using 3-28、関数 83-114）・`GameUiTheme.tss` をファイルの持ち主・File Structure・Task 12 の Files と `git add` へ・renumber の grep の期待値（台帳の行と消すファイル自身の 2 件）・物差しのヘッダに出力名と置き場・状態名・前置き・比べ方・Task 9 Step 1 の [A-164] の一致を 2560×1080 でも

独立評価 2 周目の反映:
- **D-35**（独立評価 2 周目〈評価役 2 体とも〉: Web の webm の t=0 は ready の手順と緑の印 400ms の後の境界で〈`videoCapture.ts:19-26`〉、脈動の 4 件は t=0 で脈動の途中、通知は境界の 0.5 秒後〈`EFFECT_LEAD_IN_MS`〉に出る。UITK は `Mark` の後に出し始めるので、1280×720 の組も相がそろわず、位相の差を数えないと渡していたのは 2560×1080 だけだった。実ゲームのチャレンジ 1 のワールドピンも mock に無い。脈動の大きさの差〈1.08 倍・幅 200px で最大 16px〉や矢印の位置が再現できてしまい、USS の誤った修正・LX・停止へ流れる → 両解像度の動画の組で位相の差を数えないと評価役へ渡し、効果の出始め・通知の 0.5 秒の遅れ・`2-world-pin-pulse` の矢印の向き・常時のワールドピンを `states.md` の V の行〈と P・N の行〉に状態の差として書く。`Mark` の打ち方は変えない。脈動の振幅と周期は `uitk/uitk-tutorial-cues.cs` の Assert〈矢印・キー操作ヒントの 1→1.08・DragGuide の不透明の区間〉と `TutorialAttentionPulseTest` ほかの UI テストが見るので、並べ比べから外しても見落とさない。出所 agent 既定〈指揮の補足「1280×720 の組にも位相の差を数えないを広げ、効果の出始めと通知の 0.5 秒の遅れ・常時のワールドピンを states.md の状態の差に書く」〉）
- **D-36**（独立評価 2 周目〈評価役 2 体とも〉: Web 側の `退行` を UITK の `不具合` と同じく持ち主へ回して停止していたが、裁定「UITK 切替まで CEF 由来の不具合は直さない」で CEF 由来の退行は直す人がおらず停止が解けない。分類も 1 回ずつの実行で決まり、揺れで無い不具合を作る → Web 側の失敗は CEF 由来か否かを問わず直さず記録だけにする〈Web は切替で消えるので撤去前の基準として残す。A-251〉。停止の対象は UITK 側の失敗だけ。両系とも exit 0 以外の行を 1 回流し直し、2 回とも落ちた行だけを分類し、2 回目に通った行は `PASS（揺れ: 1 回目 FAIL）`。Web の `退行` の判定の `$S4G` も 2 回まで流す。CEF の描画停止・待ちがログにあれば `（CEF 由来）` を付ける。state-label の Web の回も、シナリオの誤りでなければ記録だけ。CEF 由来でない Web の不具合は裁定どおり master の Web の不具合の範囲で、このレーンは直さず `done.md`「## 未確認」に書く。棄却: CEF 由来の退行だけ停止しない〈指揮が「CEF 由来か否かを問わず」と決めた〉。出所 agent 既定〈指揮の補足〉）
- **D-37**（独立評価 2 周目・別 subagent が一部再現〈`check/x-prerun-repro2.md` 指摘 2〉: 参照の撮影元 origin/master は origin/feature/uitk の祖先でなく、`--bug-report-textarea-width` は master 356px・feature/uitk の Web と UITK 354px〈I-fix2 ad2ff7d2ae〉。D-15 と申し送りの行の前提が実物と合わず、Task 12 (a) は画面の USS で 356px へ上書きする経路と生成物 `Tokens.uss` に触れる余地を残し、`WebTokenParityTest` は名前しか見ないので止まらない。master にだけある Web の変更〈起草時 3 本・Revert で正味 0〉の行き先も無かった → Task 7 Step 3 で参照の撮影元と HEAD の Web の差を `git diff --stat`・`git log` の両向き・台帳「## 追従記録」で洗い出し、`feature/uitk` 側のコミットによる差は「feature/uitk 側が正（master の Web 側の残り）」、master にだけあるコミットによる差は「master の未追従」として `states.md` と `done.md` に書き、どちらも並べ比べの差から外して寄せない〈後者は X-switch-pr Task 2 Step 4 の I-follow へ〉。Task 12 (a) の値は `feature/uitk` の Web の CSS から写し、生成物の USS は手で直さず、生成元と違うときだけ生成のコマンドで作り直す。Tokens.uss を手で戻さない。出所 agent 既定〈指揮の補足「feature/uitk 側が正（master の Web 側の残り）として並べ比べの差から外して記録する。Tokens.uss を手で戻さない」と再現役の推奨〉）
- **D-38**（独立評価 2 周目・別 subagent が再現〈指摘 3〉と X-switch-pr の依頼: 全録画は Task 3〈USS の修正の前〉にしか流さず、Task 14 はラダー 6 本と物差しだけ。X-5a〈R-16・Task 9〉は HEAD を見ずに分類だけで退行を決め、X-switch-pr〈`<PRERUN_HEAD>`〉は `done.md` に HEAD が無いと PR の head〈USS 修正後〉を拾う。共有の部品・トークンの USS は `GameUiElementQuery` の `worldBound`・`panel.Pick` を通じて testid のクリックと可視判定に効く → `done.md`「## 全録画の結果」の頭に `全録画の HEAD（GameUi）`〈40 桁〉を書き、Task 3 の後に USS を直したか修正を取り込んだら Task 14 Step 3 で全録画の GameUi 側を最終 HEAD で流し直して表と HEAD を差し替える。Task 16 Step 2 の取り込みで変わった USS・シナリオは一覧を書く〈取り込みの後にもう 1 周は流さない。費用が全録画 1 周で、変わったものは一覧で読み手に分かる〉。出所 agent 既定〈再現役の推奨・X-switch-pr の依頼〉）
- **D-39**（独立評価 2 周目・別 subagent が一部再現〈指摘 1〉: 設計書 §3.3 の「UITK の画面」の行と `compare-procedure.md` §3 は「フレーム番号の透かしを入れる」だが、計画の `Shoot` に透かしが無く理由も無い → 入れない。理由: (1) `PlaytestScreenshot.Capture` は同じ名前のファイルを先に消し、そのファイルが現れるまで待って返す〈`PlaytestScreenshot.cs:16-30`〉。出力名は id・解像度・相で run 内で一意なので古いフレームを拾う経路が無い (2) 透かしは Web の参照に無い差を UITK 側だけに足し、Task 10 Step 2・Task 12 Step 2 の PSNR `inf` の比べ方を全 id で壊す〈推論〉 (3) 前例の `uitk-stage3-compare.cs`・`uitk-notification-compare.cs` も入れていない。`compare-procedure.md` の行は X-5e が書き換える〈他レーンへの依頼〉。A-250 の下書きに書いた。出所 agent 既定）
- **D-40**（独立評価 2 周目: `ScenarioTimeoutSeconds = 600f` は `run-scenario.sh` の `RESULT_TIMEOUT=420`〈固定〉より長く、14 種のブロックは Act ごとの 0.5 秒の間を含めて 350 秒前後〈推測〉で 420 秒に近い。超えると run が exit 1 になり、落ちた行を「足場の上で開けなかったブロック」と読み違えて E へ替える → `prerun/` の 3 本の上限を 360 秒にし、ブロックを 7 種ずつ `prerun-shots-blocks-a.cs`・`-b.cs` の 2 本に分け〈`prerun/` は 6 本〉、最初の run の所要を `done.md` に書いて 350 秒を超えたらさらに分ける。本文が同じ 2 本は、シナリオが互いのコードを呼べない〈EDC で 1 本ずつ実行〉ので前例どおり写す。出所 agent 既定）
- **D-41**（独立評価 2 周目〈評価役 2 体とも〉: Global Constraints 3「subagent は Editor を起動しない」と、Task 1 Step 4 の `uloop launch`・Task 4 Step 3 の経路 M〈Editor を閉じて一時 worktree の Editor で流して戻す〉が食い違い、subagent は止まるか規約を破る。経路 M の前置きの `--project-path` も一時 worktree へ向けていなかった → Editor の起動・停止・付け替えは本体〈制御セッション〉が行い、subagent はそこへ来たら本体へ返す。経路 M の前置きは `--project-path $T/moorestech_client` で流し、Editor を起こすたびに前置きを流す〈起動済みの Editor にシェルの環境変数は届かない〉。出所 agent 既定）
- **D-42**（独立評価 2 周目〈評価役 2 体とも〉: Task 9 Step 1 の超えたときの逃げ道「位置は PlayMode の組と照らす」は、E の id に PlayMode の組が無いので使えない。評価役の 1 回の組数は「束 4 は 2 つに分けて 1 回 24 組まで」で、束 4〈98 組〉も束 2〈56 組〉も上限を破る → EditMode と PlayMode が 2px を超えたまま一致しなければ E の撮影へ進まず、E の全 id を D-23 の逃げ道〈R-11 未達・Q-n〉にして道具の口の不足を「integrator への依頼」へ。評価役はどの束も 1 回 24 組までに分ける〈上限だけを正にし、回数は新しい manifest で数える〉。出所 agent 既定）
- 軽の指摘の直し（判断を伴わない）: 設計の出所の完了条件の引用を実行計画 c1e1ff2b8b の §1.5 の文に替え、§6.1 と §1.5 の改め案を「反映済み」にした・state-label の期待値を Assert 5/5 に・Task 2 Step 3 の「Task 5 の不具合」を「Task 3 Step 3 の `不具合`」に・台帳「## 参照キャプチャ」は行を足さず撮影元・置き場・URL・2560×1080 の 4 行を書き換えて前の組を履歴の 1 行へ（台帳の規則）・190 行を超える USS を分けた先は 10 ファイル超のディレクトリならサブディレクトリ（`Foundation/Theme/Derived/`。tss の `@import` は元の行の直後）・最終 HEAD（取り込み後）でも `--bundle all` を流して件数が違えば `disposition.md` の 5 行を書き直す・G1 は「違反なし、かつ束ごとの未記帳の件数が起点と同じ」・UI テストは 1 本 570 秒で、時間切れなら `[A-L]`／`[M-Z]` に割る・Task 1 Step 3b で前のレーンの `done.md` の「申し送り」「integrator への依頼」「未確認」を全部読んで受けるタスクを割り当てる・「## 全録画の結果」と「## 5a への申し送り」のパスを `scenarios/` からの相対にそろえて python で突き合わせる（X-5a の依頼）

独立評価 3 周目の反映:
- **D-43**（独立評価 3 周目〈評価役 1・重〉: 全録画の一覧・TSV・`gameui-head.txt` と G1 の起点 `g1-before.txt` を `moorestech_client/Temp/` に置いていたが、Unity は Editor を正常に閉じるとプロジェクトの `Temp/` を消す〈起草時の観測: Editor の居ないメインクローンと worktree に `Temp/` が無く、Editor が動いている worktree だけにある〉。このレーンは経路 M〈Task 4 Step 3〉と停止〈Task 3 Step 3・Task 12〉の間に Editor を閉じるので、再開後の `不具合` の行の一覧・Task 14 Step 3 と Task 16 Step 2 の `$(cat $D/gameui-head.txt)` が空になる。Task 1 Step 3 は Editor の有無を見る前に `Temp/` へ書く → 置き場を `.gitignore:102` の `/moorestech_*/.uloop/outputs/` の下の `moorestech_client/.uloop/outputs/X-prerun/`〈全録画は `recordings/`〉へ移す〈前例 C2-a・C3-a の `compare/<レーン>/`。`PlaytestResults/` も `Temp/` の外〉。全録画の HEAD〈GameUi・Web〉は控えたその場で `done.md`「## 全録画の結果」の頭に書いて logs へ push し、後の手順は `done.md` から `grep -m1 … | grep -oE '[0-9a-f]{40}'` で読む〈X-switch-pr Task 1 Step 6 も done.md を読むので、worktree が `moores-wt rm` で消えた後も残る〉。G1 の起点は `done.md`「## 前提」の件数と比べる。出所 agent 既定〈指揮の補足「Temp に置かない。Unity が消さず git に入らない場所へ移し、全録画の HEAD は控えた時点で done.md の下書きにも書く」〉）
- **D-44**（独立評価 3 周目〈評価役 1・重〉: 実ゲームではチャレンジ 1 のチュートリアル `uiHighLight`〈anchor `challenge.current-hud`・ラベル「左上で現在の目標を確認する」。ピン先マスタ e0c913033c の `challenges.json` の 1 件目の `tutorials[1]` を照会で確かめた〉の枠とラベルが常に出ている〈`uitk-tutorial-cues.cs:123-124`・S2-tutorial `done.md`〉が、mock の参照には無い〈`referenceShots.ts:81` の `2-game-hud` の controls〉。`states.md` は Task 7〈撮る前〉に書くのに枠とラベルが無く、Task 12 の処遇にも状態の差の行き先が無いので、評価役が挙げて再現すると (b) で停止するか LX の 8 枠を使う → Task 7 Step 3 の P・N・V の行に名指しで足し〈ピンが動けば照会し直す〉、評価役への依頼文の「数えない」にも足し、Task 12 Step 1 に (f)「状態の差 → `states.md` に足して閉じる。根拠の行〈マスタ・シナリオ・fixture〉を差の表に書く。記録だけで停止しない」を新設し R-14 に入れた。根拠の行を示せない差は (f) にしない。撮影の前に `EndSession` で本物の枠を畳む案は採らない〈実ゲームの常時の状態を撮る方が撤去後の物差しとも合い、畳む口の副作用を確かめる手間が増える〉。出所 agent 既定〈指揮の補足「チャレンジ 1 の常時の枠とラベルを states.md に名指しで足し、処遇に状態の差の行き先〈記録だけ・停止しない〉を足す」〉）
- **D-45**（独立評価 3 周目〈評価役 1・重〉: Task 12 (b) で持ち主の修正を取り込んだ後、該当の id だけを撮り直していた。持ち主の修正は共有の部品〈`Components/**`・`GamePanel`・`ProgressBar` 等〉に入ることが多く、D-34 が USS の修正で全 id を撮り直す理由〈使い手の全画面を動かす・X-prerun が Web と比べられる最後の回〉と同じことが起きる。Task 16 Step 1 の「全部の組を最終 HEAD で撮った版」も成り立たなかった → 撮影〈Task 8〜9〉の後に `Client.Ui`・`UI Toolkit` を変える取り込み〈Task 12 (b)・LX 番号待ちからの再開・Task 14 Step 3 の `不具合` からの再開・Task 16 Step 2 の取り込み〉をしたら、Task 12 Step 2 の 1〜5 を回す。回すかは `done.md` の `撮影の HEAD（UITK）` から今の HEAD までの `git diff --stat -- Client.Ui 'UI Toolkit'` で決め、撮り直したらその行を替える。Task 16 Step 2 で撮り直したら報告ページを載せ直す。棄却: 変わったファイルの使い手の id だけを撮り直す〈USS・C# の使い手を漏れなく数える決まった方法が無く、共有の部品は全画面に効く〉。出所 agent 既定〈指揮の補足「持ち主の部品の修正を取り込んだ後は、USS 修正と同じ理由〈D-34〉で全 id を撮り直す」〉）
- **D-46**（独立評価 3 周目〈評価役 2・重〉: E の外殻は画面の面だけを単色の地に描く〈`UiRuntimePanelCapture.Render`〉のに、Task 9 Step 1 は panel の絶対位置を PlayMode と比べ、Task 11 は画面全体の組を作っていた。外殻が Viewer 列だけだと持ち物の列が空で panel が左へ寄り〈S4-alpha `done.md`「UITK 本番は Inv 列が空で x=56」〉、Web の参照には HUD・持ち物・ホットバー・世界が写るので、評価役が「欠け」を挙げて再現し (b) で解けない停止に入る → 前例 S4-special〈panel 原点からの相対〉に合わせる: Task 9 Step 1 は panel の寸法と panel 原点からの閉じるボタンの相対位置で比べ、絶対位置は比べない。E の行に比べる面 `面=<testid>` を必須にし〈機械検査〉、Web の面の矩形は参照と同じ `openShot` で開いて使い捨て worktree の中の一時のスクリプトで測り〈manifest は矩形を持たない〉、UITK の面の矩形は EDC が px で返す。Task 11 Step 1 は E の組を両側とも面の矩形に切り抜き〈高さの差はマゼンタで埋める〉、`states.md` の E の行と評価役への依頼文に「外殻の外〈常時 HUD・他の列・ホットバー・世界の地〉と面の絶対位置は数えない」を足した〈前例 S3-ladder・S4-research・S3-buildmenu の「数えない」規則〉。棄却: 外殻を PlayMode と同じ 3 列・HUD の層で組む〈前例 S4-research の形だが、E の 38 件それぞれに持ち物と HUD の偽の Model を足す必要があり、撮影の道具の口も足りない〉。出所 agent 既定〈指揮の補足「前例 S4-special の形〈パネルの矩形に切り抜き、位置はパネル原点からの相対〉に合わせ、外殻の外は数えないを states.md と評価役への依頼文に足す」〉）
- **D-47**（独立評価 3 周目〈評価役 1・軽〉: 足した枠が出たことの確かめ〈`root.Q("tutorial-highlight-label") != null`・`Query(className: "tutorial-outline__ring").First() != null`・動画の `IsShown("tutorial-highlight-label")`〉が、チャレンジ 1 の本物の枠とラベル〈D-44〉で最初から真になり、足した枠が出る前に撮る〈S2-tutorial が同じ取り違えで FAIL した前例〉→ 足す前の数〈`Query(name: …)`・`Query(className: …)` の `ToList().Count`〉を控えて数が増えるまで待つ形にした〈b5・物差し・動画の 3 本〉。前例のアンカーに一番近い枠を選ぶ形より短く、ラベルにも同じ形で使える。出所 agent 既定）
- **D-48**（X-5a の直し役からの依頼〈`check/x-5a-evalfix2.md`〉: `playtest-receiver/developer-report-interrupt.cs`・`developer-report-resume.cs` は `PlaytestRunner.Run` を呼ばない EDC の手動投入のスニペットで〈`grep -c PlaytestRunner.Run` が 0。resume はハードコードの進捗の箱と `.superpowers/sdd` の控えを読む〉、`run-scenario.sh` では必ず exit 1 になり、Task 3 Step 3 の「ヘッダの手順で流し直し」の手順もヘッダに無い → 全録画の一覧から外し、interrupt は W0-cef 計画 Task 4 Step 6 の手動投入で両系 1 回ずつ流して `前提つき（手動投入で PASS）`／webUiHealth の検査で落ちれば `前提つき（5a が assert を書き直す）`、resume は流さず `前提つき（判定の対象外）`。運転役 `.sh` から流す `uitk/stage5/uitk-event-language-gate.cs`〈S5-gate の後にできる〉も一覧から外して運転役で流す。語は X-switch-pr R-9 と同じにした〈X-5a R-16・X-switch-pr R-9 もこの 3 行を TSV の exit で判定しない〉。Web 側の interrupt は経路に依らず Mac の Editor で流す〈Windows の Editor へスニペットを投入する手順を持たない〉。出所 agent 既定〈他レーンの依頼〉）
- **D-49**（独立評価 3 周目〈評価役 2・軽〉: USS を直した後の PSNR の比べで、P・N・V は世界〈空・水面・ワールドピン〉が写るので前後がほぼ `inf` にならず、「同じ id を 2 回撮って揺れか USS の影響かを分ける」の分け方が決まっていない。波及が揺れとして外れ、D-34 が防ぎたかったことが残る → `inf` でない id は揺れか波及かを分けず全部「変わった」に数え、組の作り直しと評価役・再現の対象にする。比べる相手は回ごとの `before/`〈その回の修正・取り込みの直前〉にした。費用は P・N・V の組を回ごとに評価し直す分だけ増える。棄却: P・N を UITK の panel の矩形で切り抜いて比べる〈HUD の id には panel が無く、切り抜きの外の波及を落とす〉。出所 agent 既定）
- **D-50**（独立評価 3 周目〈評価役 2・軽〉: 台帳「## 壊れた録画の割り振り」の S2-ladder の 4 行〈pebble・equip・tree-pin・server-notification-smoke〉は今の HEAD で方針の列が方針の文のままで、R-5「10 行すべての方針の列に結果」を満たす手順も、満たさないときの扱いも無かった。X-prerun がこの行へ `X-prerun` の語を書くと、S4-gamma の `list-recordings.sh`〈行のどこかに `X-prerun` を含む行を外す〉がその本を全録画から黙って外す → R-1・Task 1 Step 3 で 8 行に結果があるかを見て、無ければ直すレーンの `done.md` の記録から `X-prerun` の語を含めずに写し〈台帳のこの節は W6 の持ち主の表で X-prerun の範囲〉、記録も無ければ前提の欠けで止まる。出所 agent 既定）
- 軽の指摘の直し（判断を伴わない。独立評価 3 周目）: 物差しの流し方を「## 5a への申し送り」の小節「### 物差し（prerun-smoke-shots）」に書く（X-5a Task 1 Step 4b の `awk '/^### 書き直す/{f=1;next} /^##/{f=0} f'` に流し方の `.cs`・`.sh` が混ざらない。Task 5 Step 1 の python もこの小節を見ない）・全録画の表を「シナリオ / UITK の分類 / Web の分類 / 秒 / 原因・注記」にし、分類の欄は分類の語だけ（`Web 専用` の行はちょうど `Web 専用`）、その数と「### 消す」の数を X-5a と同じ式で突き合わせる・全録画の一覧から `uitk/prerun/` を外し（Task 3 Step 1）、Task 14 Step 3 の流し直しも同じ集合にする・`capture.ts` は 10 分を超えうる分けられないコマンドなので本体が background で 1 回起動して完了の通知で受け、流し直す前に出力先を消し（`capture.ts:22` は空でない出力先で止まる）、所要を `done.md` に書く

## 未確認

- ラダー 4〜6・`tools/uitk/` の 2 本・段 4〜5 の撮影シナリオ・S5-gate の UITK の言語ゲートとその L1 の土台（`Client.Tests/Ui/Screens/<ゲート>/Support/`）は起草時の HEAD に無い（S4-gamma・S5-gate・S5-ladder の後にできる）。パスと API は Task 1・Task 7 Step 1・Task 9 Step 2 で実物から写す。土台が無く外殻を組めなければ `5-event-language-gate` は Task 9 Step 2 の逃げ道（Q-n）に入る
- `docs/uitk/disposition.md` の再接続オーバーレイの理由の行は起草時に無い（S5-crosscut が書く。Task 7 Step 1 で確かめる）
- `prerun-videos.cs` の `dragFrom` に使うアイテム（実ゲームの持ち物のレシピ一覧に最初から並ぶもの）は Task 9 Step 3 で選ぶ
- 新しい参照の manifest の id の数（起草時 89）。master で増えていれば Task 7 で表を足す
- E の各 id の外殻の組み方は、前例のレーンの `done.md` の記述から写す（repo に撮影のコードが無い）。前例の無い id は Task 9 Step 2 で決める
- `prerun-shots-blocks-a.cs`・`-b.cs` の 14 種のうち、平らな地面で開けないもの（油井・宇宙エレベーター拠点・電柱の UI の有無）は Task 8 の run で分かる。置く向き North で大きなブロックが原点のどちら側へ伸びるかは確かめていない（(-6,32,-6) はどちらへ 13 マス伸びても足場の内側）
- 動画の録画の始まりと Timeline の秒のずれ（Task 9 Step 4 で測る）
- Windows の Editor で CEF が描けるか（Task 1 Step 5。winunity の Library の不調〈`winunity-play-fix.md`〉が再発したら経路 M）
- `prerun/` の各シナリオの所要（起草時の見積もりは推測。ブロック 7 種で 200 秒前後）。最初の run で測る（D-40）
- 参照の撮影元と `feature/uitk` の Web の食い違いの件数（起草時の模擬は `1-bug-report` の幅の 1 件と生成物の `localizationKeys.ts`）。Task 7 Step 3 で数え直す（D-37）
- Web の面の矩形を測る一時のスクリプト（Task 7 Step 4）は起草時に流していない。`chromium`・`openShot`・`startMockHost` の使い方は `capture.ts` の写しで、E の各 id の面の testid（`面=`）は Task 7 Step 1 で決める（D-46）
- `capture.ts` の 1 回の所要（静止画 84・動画 5）。10 分を超えるかは未実測で、本体が background で流して測る
- interrupt の手動投入の GameUi の結果（manifest v6 の webUiHealth の検査で落ちて `前提つき（5a が assert を書き直す）` になる見込み。推測。D-48）と、Unity が `Temp/` を消すのが Editor の正常終了のときだけか（起草時はメインクローンと worktree の有無だけを見た。置き場を移したので結果に依らない。D-43）

## 本PR外のリファクタ提案

- 既存の段の撮影シナリオは出力名の付け方がばらばら（`uitk-<id>-<w>x<h>`・`<w>x<h>-<id>`・`<prefix>-uitk-<名>`・`4-wave-<状態>-<w>x<h>`）。撮影 id と解像度から名前を作る口が DSL に無いので、切替後に撮影を続けるなら `PlaytestScreenshot` に `<id>-<w>x<h>` の口を足す案（持ち主: DSL。X-cleanup 以後）。その口に「操作オーバーレイを消し、カーソルをパネルの外へ置いてから撮る」手順（既存の `uitk/stage3/uitk-stage3-compare.cs` の `ShootAsync`・`MoveCursorOffPanelsAsync` と、この計画の `prerun/` の 4 本の `Shoot`）も入れられる（Phase 2.6 の参考。表の対象外のテストコード）
- `UiRuntimePanelCapture` に「画面の外殻と偽の Model を組む見本」を画面ごとに持たせる口が無く、E の撮影は毎回レーンの `done.md` の記述から組み直している（前例 S3-buildmenu D-61・S4-alpha の依頼）。画面ごとの `IUiGalleryEntry` に Web の fixture 相当の見本を足す案（持ち主: 各画面・I-fix）

## 設計書・実行計画へ反映する行

### 設計書 §7.1（A-250・A-251）

```markdown
| A-250 | 撤去の前の全画面の並べ比べ（X-prerun）は、master の worktree で撮り直した参照 manifest の全 id に「既存の段の撮影シナリオ／`scenarios/uitk/prerun/` の新設シナリオ／EditMode の `UiRuntimePanelCapture`（実行時に作れない状態。[A-164] の一致を先に確かめる）／動画は録画して Web の webm と同じ相の 3 フレーム（2560×1080 は録画が 1280×720 固定なので同じ相の静止画）／UITK に作らない画面（[A-38]）は撮らず根拠の行を残す」のどれか 1 つを割り当てた対応表を作り、抜けと重複が 0 であることを機械で確かめてから撮る。撮影は毎回表示言語を日本語に固定する（参照が日本語）。判定は左 Web・右 UITK の合成画像を opus の評価役 2 系統に束ごとに別々に渡し、別の subagent が画素で再現した差だけを「USS で直す／持ち主へ回す／LX／Q-n」に閉じる。USS を直したら、また撮影の後に持ち主の部品の修正など `Client.Ui`・`UI Toolkit` を変える取り込みをしたら、全 id を撮り直し、前の回と PSNR で比べて `inf` でない id を（世界の揺れと分けずに）全部並べ比べ直し（共有の部品・トークンの波及を見落とさない）、全録画の GameUi 側も最終 HEAD で流し直す（後のレーンが退行の基準に使う）。EditMode の撮影は外殻（画面の面）だけを写すので、組は両側を面の矩形に切り抜き、位置は面の原点からの相対で見て、外殻の外（常時 HUD・他の列・世界の地）は数えない（[A-164] の一致の確かめも面の寸法と面の原点からの相対位置で見る）。実ゲームだけにある状態（チャレンジ 1 の常時の枠とラベル・ワールドピン）は状態の差として撮る前に書き、撮った後に見つかった状態の差も根拠の行を付けて記録だけで閉じる（停止しない）。動画の組は両解像度で動きの位相の差を数えず、効果の出始め・通知の出る時刻・実ゲームのワールドピンを状態の差として書く（脈動の振幅と周期は録画の Assert と UI テストが見る）。参照の撮影元（master）と `feature/uitk` の Web の食い違いを先に洗い出し、`feature/uitk` 側の修正による差（feature/uitk 側が正）と master にだけある変更による差（I-follow へ）は UITK を寄せない。生成物の USS は手で直さない。フレーム番号の透かしは入れない（出力名が run 内で一意で、`PlaytestScreenshot` がファイルの出現を待つので古いフレームを拾わない。透かしは Web の参照に無い差を足し PSNR の比べ方を壊す）。§3.3 の「UITK の画面」の行と `compare-procedure.md` §3 の透かしの文を改める。判定の取りまとめ・USS の修正・LX の記帳（台帳の行と印を同じコミット）は X-prerun が行い、USS で直らない差だけを持ち主へ bd で回して指揮へ返す。§3.3「並べ比べ」の行と [A-179] の「指揮が判定し、台帳の行と印は I-fix が置く」を X-prerun について改める（段の締めの I-fix レーンが W6 には無く、撤去の直列の道に別レーンを挟まない）。撤去レーンの見た目の不変は X-prerun が作る `prerun-smoke-shots.cs`（束ごとに 2 状態・全体スイッチと Web の型を参照しない）で測る（X-prerun D-2・D-3・D-11・D-16・D-30〜D-35・D-37〜D-39・D-44〜D-46・D-49） | 撮影表の 84 件だけを PlayMode で撮る: 実行時に作れない状態が比べられない。評価役 1 体に全部を渡す: コンテキストに収まらず後半を見ない。Codex の codex-audit: ユーザーの指示（2026-11-06「一旦codexは使わず」）で使わない。[A-179] のとおり指揮が判定し I-fix が印を置く: W6 に I-fix レーンが無く、撤去の直列の道が止まる。[A-179] のとおり実ゲームで Web と UITK の両方を全画面撮る: 段の締め（S2〜S5-ladder）が同時表示の重なりを実ゲームの組で見終えており、残るのは全 id を覆う比べで、Web 側を全画面撮るには CEF の Editor がもう 1 本要る（Mac の Editor は 1 本）。フレーム番号の透かしを入れる（§3.3 のとおり）: 古いフレームを拾う経路が無く、PSNR の比べ方を壊す。参照（master）の見た目へ UITK を寄せる: `feature/uitk` で直した Web の不具合（例 バグ報告の記述欄の幅）を UITK に戻す。EditMode の組を画面全体で比べる: 外殻の外（HUD・他の列・世界）が欠けとして挙がり、持ち主に直しようのない停止になる。修正を取り込んだ後に該当の id だけ撮り直す: 共有の部品の波及が差 0 のまま残る |
| A-251 | 撤去の前の最後の Web の基準は、全録画の Web 側の結果（`harness/uitk/X-prerun/done.md`「## 全録画の結果」と台帳「## 実測の結論」の行）・参照 `web-<sha>`／`web-<sha>-2560x1080`（台帳「## 参照キャプチャ」）・全束の突き合わせの記録（`disposition.md` の束 1〜5 の行）の 3 つとし、切替後に Web の挙動・見た目・テストを確かめるときはここだけを引く。Web 側の失敗の分類は [A-177] の 6 分類に `既存`・`退行`・`比較不能（新設）`（前の全録画に無いシナリオ）・`比較不能（変更）`（前の全録画の HEAD から中身が変わったシナリオ）と、UITK 側の「シナリオの誤り（5a）」（古い testid・Web の型の参照）を足して使う。両系とも exit 0 以外の行は 1 回流し直し、2 回とも落ちた行だけを分類する（2 回目に通った行は `PASS（揺れ）`）。Web 側の失敗は CEF 由来か否かを問わず直さず、停止もせず、分類・原因（CEF 由来なら印）を記録だけする（裁定「UITK 切替まで CEF 由来の不具合は直さない」・Web は切替で消える）。UITK 側の全録画を流した HEAD を 40 桁で、控えた時点で報告（`done.md`）に残し、後のレーンはそれを退行の基準の HEAD にする。`run-scenario.sh` で流せない EDC の手動投入のスニペットと運転役 `.sh` から流すシナリオは一覧から外し、手動投入・運転役で判定する（X-prerun D-6・D-27・D-36・D-38・D-43・D-48） | 撤去後に Web を復元して比べる: 裁定（切替後に Web へ戻すデバッグ切替を残さない）と合わない。基準を残さない: 切替後の不具合で Web の挙動を辿れない。Web 側の退行で停止して持ち主に直させる: CEF 由来は裁定で直す人がおらず、撤去の直列の道が止まる。1 回の実行で分類する: 揺れで無い不具合・退行を作る |
```

### 実行計画 §6.1 の割り当て行・§1.5 の X-prerun 行

反映済み（実行計画 c1e1ff2b8b。§1.5 の X-prerun 行は「opus の評価役 2 体の独立評価（画像付き。E-24）」と「全束のテスト名の突き合わせ差分 0 … `prerun-smoke-shots.cs` を作る」を含み、§6.1 の割り当て行と §8 E-24 も入っている）。本体が docs PR で二重に入れない
