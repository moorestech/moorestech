# UITK X-prerun（撤去の前の全録画・wwxy の残り 2 本・全画面の参照の撮り直しと並べ比べ・撤去レーンの見た目の物差し）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 段階 5 の締め（S5-ladder）が `feature/uitk` に入った後、撤去（X-5a〜5e）で全体スイッチの Web 側が使えなくなる前に、(1) 全体スイッチの UITK 側と Web 側で全録画を回して分類し、(2) wwxy の残り 2 本を削除・書き直して bd `moorestech-wwxy` を閉じ、(3) master の worktree で全画面の参照を 1280×720 と 2560×1080 で撮り直し、UITK の全画面（参照 manifest の全 id）と並べ比べて opus の評価役 2 系統の独立評価にかけ、再現した差を「直した／LX-210〜217 に記帳／要裁定 Q-n／持ち主へ bd」のどれかに閉じ、(4) 撤去レーンが見た目の不変を確かめる物差し `prerun-smoke-shots.cs` と撤去前の最後の Web の基準（全録画の Web 側 TSV・参照 `web-<sha>`・全束の突き合わせの記録）を残して `feature/uitk` へ取り込む。

**Architecture:** integrator のレーン（`int`。Mac の Editor 1 本＝このレーンの worktree。Web 側の全録画と 2 本目の Editor が要る再走は Windows の `~/bin/winunity`）。本番のコードは触らず、書くのは「検査する側」（`scenarios/uitk/prerun/` の撮影シナリオ 5 本・wwxy の 2 本）と、並べ比べで再現した差を直す USS（`Client.Ui/**/*.uss`）と台帳の自分の節だけ。全録画は S4-gamma が作った `tools/uitk/list-recordings.sh`・`run-all-recordings.sh` を書き換えずに使い、結果の分類は設計書 [A-177] の 6 分類。UITK の撮影は「既存の段の撮影シナリオ（P）／このレーンが新設する撮影シナリオ（N）／EditMode の `UiRuntimePanelCapture`（E。[A-164]）／動画の 3 フレーム（V）」の 4 種で、参照 manifest の全 id に 1 行ずつ撮り方を割り当て、機械で抜けと重複が 0 であることを確かめる。並べ比べの判定は前例 S3-ladder Task 9・S4-gamma Task 13 の形（評価役 2 系統が互いの結果を見ずに判定 → 別の subagent が画素で再現 → 再現したものだけ処遇）。判定の取りまとめ・USS の修正・LX の記帳（台帳の行と印）はこのレーンが行い、USS で直らない差だけを持ち主へ bd で回して指揮へ返す（設計書 §3.3「並べ比べ」の行と [A-179] の「指揮が判定し、印は I-fix が置く」を X-prerun について上書きする。D-16）。

**Tech Stack:** Unity 6 / UI Toolkit（`Client.Ui`）、プレイテスト DSL（`Client.Playtest`・`.agents/skills/unity-playmode-recorded-playtest`）、`Client.Ui.Editor.Capture`（`UiRuntimePanelCapture`・`GameViewResolution`）、Node と pnpm（master の worktree の `moorestech_web/webui/e2e/uitk-reference/capture.ts`・`tools/uitk/test-name-parity.mjs`）、ffmpeg・ffprobe、Python 3（標準ライブラリだけ。`check-uitk-web-follow.py`・対応表の機械検査）、bd、`~/bin/winunity`。

設計の出所: 実行計画 `docs/uitk-migration/execution/execution-plan.md` §1.5 の X-prerun 行（完了条件の原文:「全体スイッチの Web 側と UITK 側で全録画を回す（plan §2.6。撤去後は Web 側が使えない）。wwxy の残りを直すか削除して閉じる。master の worktree で全画面の参照を撮り直し（`web-<最新 sha>`）、UITK の全画面と 720p・2560×1080 で並べ比べ、codex-audit（画像付き）にかけて差 0 か記帳済み（設計書 §3.3 (c)）」）・§2.1（DAG `S5L --> PR0[X-prerun] --> X5a`）・§4.3・§5・§6.2、統合計画 `docs/uitk-migration/plan.md` §2.6（「撤去を積み始めたら、全体スイッチの Web 側は使えなくなる。積む前に、全体スイッチの Web 側と UITK 側で全録画を回しておく」）・§4.4（ラダー 6 本）・§4.6・§5.1-4（壊れている 10 本は直すか削除して wwxy を閉じる）、設計書 `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4・[A-164]・[A-177]・[A-179]、`.agents/skills/ref-uitk-design/references/compare-procedure.md`、台帳 `docs/uitk/TODO.md`、`docs/uitk/disposition.md`、裁定 `.decisions/2026-11-06-*.md` の 7 本。実物の全件調査は PLAN-W6 の起草時の `inv/prerun-switch.md`（HEAD `9c83052273`）。

## Requirements

| # | 要件 | 受け入れ基準（観測できる形） |
|---|---|---|
| R-1 | 前提の確認 | Task 1 の照会で、`origin/feature/uitk` に S2-ladder・S3-ladder・S4-gamma・S5-crosscut・S5-ladder のマージコミットがあり、各 `done.md` の 1 行目が「状態: 完了」、ラダー 6 本のシナリオ（`uitk-lifeline`・`uitk-opening-hud`・`uitk-build-bp`・`uitk-production-loop`・`uitk-research-train`・`uitk-challenge-gate`）が実在、`tools/uitk/` か `tools/uitk/recordings/` に `list-recordings.sh`・`run-all-recordings.sh` が実在、S4-gamma の `done.md`「## 全録画の結果」が実在、`Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の表が空、台帳「## 束」の全束が `暗転`。1 つでも欠ければ `done.md` 1 行目「状態: 停止（前提の欠け: <欠けたもの>）」で止まる |
| R-2 | 全束の突き合わせ差分 0（撤去前の最後の記録・A-251） | 最終 HEAD で `node tools/uitk/test-name-parity.mjs --bundle all` が exit 0（全束 写す予定 0・未対応 0）。`docs/uitk/disposition.md`「## 突き合わせの記録」に束 1〜5 の 5 行（日付・束・HEAD 10 桁・件数）。崩れていたら持ち主へ bd を立てて「状態: 停止」 |
| R-3 | wwxy: `tutorial-research-renumber.cs` を削除 | `scenarios/tutorial-research-renumber.cs` が無い（`git ls-files` 0 件）。台帳の行の方針に「削除した（X-prerun）」 |
| R-4 | wwxy: `misc/research-node-state-label.cs` を現行の研究 GUID へ書き直す | 削除済み GUID（837e9697・424be8c1・07d6226c）がファイルに 0 件。ピン先マスタの研究（起草時 e0c913033c: 原始研究1 `858bcb10-b8ba-478e-9bc5-473ca61281a2`・原始研究2 `b47c5e3c-1b58-42c5-a477-d485d2eae747`・原始研究3 `bc5e7786-6759-4271-8095-836703b54490`）で「完了済み／研究可能／研究不可」の 3 状態を作り、GameUi と Web の両方で `Success: true`・ErrorLogs 0 |
| R-5 | wwxy を閉じる | 台帳「## 壊れた録画の割り振り」の 10 行すべての方針の列に結果（書き直した・削除した・移植した）が入り、X-prerun の 2 行に GameUi/Web の run を書く。全録画で 10 行のうち現存する 8 本（起草時: S2-ladder の 4 本・S3-ladder の 3 本・state-label。数は Task 5 Step 1 で 10 行を 1 行ずつ「現存／削除／後継」に分けて数え直す）と、削除した electric-wire の後継 `connect/electric-wire-tool-extend-chain-via-ui.cs`（GameUi）が PASS。bd `moorestech-wwxy` を close（理由に PR URL） |
| R-6 | 全録画の UITK 側（全体スイッチ GameUi） | `list-recordings.sh GameUi` の全本と書き直した state-label を `run-all-recordings.sh` で流し、TSV の行数＝一覧の本数。exit 0 以外の全行を [A-177] の 6 分類（PASS／束 5 未移植／wwxy／前提つき／Web 専用／不具合）に分け、`束 5 未移植` 0・`不具合` 0（途中で見つけたものは持ち主へ回して入ったら流し直して PASS）。どのシナリオも先回りで分類せず流した結果で分ける。`Web 専用` はシナリオ全体が Web に依るものだけで、assert の一部だけが Web に依るものは `前提つき（5a が assert を書き直す）`（Task 3 Step 3。D-29） |
| R-7 | 全録画の Web 側（全体スイッチ Web） | `list-recordings.sh Web` の全本と state-label を流し、exit 0 以外の行を S4-gamma の Web 側の TSV（S4-gamma `done.md`「## 全録画の結果」）と比べ、S4-gamma で PASS だった行だけを S4-gamma の HEAD で流し直して `既存`／`退行`／`比較不能（新設）`／`比較不能（変更）` に分ける。`退行` 0 |
| R-8 | 5a への申し送り（Web 専用の一覧） | `done.md`「## 5a への申し送り」を 2 つの小節に分ける。「### 消す（シナリオ全体が Web 専用）」に分類が `Web 専用` のシナリオ全部、「### 書き直す（Web に依る assert だけを持つ）」に流した結果で落ちた assert が Web に依るものだけだったシナリオ（起草時の見込み: `playtest-receiver/developer-report-delivery.cs` の webUiHealth の 3 つ。分類は先回りで決めず Task 3 Step 3 の結果で決める。D-29）と `Client.WebUiHost` を直接参照する 8 本（起草時の実測）。どちらも、パス・落ちた assert・5a でどうするかの案。後者の分類の語に `Web 専用` の文字列を含めない（5a の grep が消す側だけに当たるように） |
| R-9 | 参照の撮り直し | origin/master の使い捨て worktree で `capture.ts` を 1280×720 と 2560×1080（W6-4）で撮り、`moorestech_logs/uitk/reference-captures/web-<sha10>/`・`web-<sha10>-2560x1080/` に置く。両方の `manifest.json` の `viewport` が `1280x720`・`2560x1080`、`entries` の件数が同じ。台帳「## 参照キャプチャ」に撤去前の最後の参照として 1 行（撮影元・置き場・件数） |
| R-10 | 撮影の対応表（W6-5） | `done.md`「## 撮影の対応表」に新しい manifest の全 id が 1 行ずつ（撮り方 P/N/E/V/X・出所・UITK のファイル名・判定）。X は [A-38] で UITK に作らない画面だけで、出所に [A-38] と `disposition.md` の理由の行がある。Task 7 の検査スクリプトが「manifest にあって表に無い 0・表にあって manifest に無い 0・重複 0・撮り方の誤り 0（X の根拠の欠けを含む）」で exit 0 |
| R-11 | UITK の全画面の撮影 | 表の全静止画 id（X を除く。X は撮る相手が無いので R-11 と Task 11 の組の対象外で、`done.md` の表の行が根拠）について `moorestech_client/.uloop/outputs/compare/X-prerun/<id>-1280x720.png` と `<id>-2560x1080.png` が実在（Task 8〜9 の `ls` の数え上げ）。Task 9 Step 2 の逃げ道（撮れない状態を各レーンの PR の並べ比べで代える）を使った id は R-11 未達として数え、`done.md`「要裁定」に Q-n（外した id・代わりの証拠の URL・外した理由）で上げる（0 件なら何もしない。D-23）。E の id は、先に実行時に作れる状態 1 つ（`4-block-chest`）で EditMode と PlayMode の panel の位置・寸法の差が 1280×720 で 2px 以内であることを記録してから撮る（[A-164]） |
| R-12 | 動画 5 件 | 表の V の 5 件について、1280×720 は UITK 側を録画し、Web の webm と同じ相（長さの 10%・50%・90%）の 3 フレームを両方から ffmpeg で抜く。2560×1080 は録画が 1280×720 の出力に固定（`PlaytestRecorder.cs:33`）なので、同じ操作の同じ相で UITK の静止画を撮り、Web の 2560×1080 の webm から同じ相を抜く（D-32）。`compare/X-prerun/video/<id>-{start,mid,end}-{web,uitk}-{1280x720,2560x1080}.png` の 60 枚。各動画は Web の `steps` の `visible(...)` に当たる UITK の要素が出たことを Assert してから相を数える（D-33） |
| R-13 | 独立評価 2 系統と再現 | このレーンが取りまとめ役として（D-16）、並べ比べの組（左 Web・右 UITK の合成画像）を opus の評価役 A・B に互いの結果を見せずに判定させ、指摘を別の subagent が画素で再現し、再現したものだけを `done.md`「## 差の表」に残す。再現しなかったものも 1 行で残す |
| R-14 | 差 0 か記帳済み（設計書 §3.3 (c)・§3.4） | 「## 差の表」の全行が「直した（USS・撮り直しの PNG）／LX-210〜217（台帳の行と `/* look-exception: LX-n */` を同じコミット。`LookExceptionLedgerTest` PASS。9 件目からは「LX（番号待ち）」で停止。D-24）／要裁定 Q-n（`done.md`「要裁定」）／持ち主へ bd（入って再撮影して閉じた）」のどれか。配置・色の系統・装飾の有無・文字の大きさの差は LX にしない |
| R-15 | 撤去レーンの物差し `prerun-smoke-shots.cs`（W6-6 (b)） | `scenarios/uitk/prerun/prerun-smoke-shots.cs`（200 行未満・全体スイッチと Web の型を参照しない）が束ごとに 2 状態・計 10 状態を 1280×720 と 2560×1080 で撮り、最終 HEAD で `Success: true`・PNG 20 枚。`grep -nE "UiSystem|ActiveUiSystem|UiSystemEditorSwitch|WebUiHost|PlaytestDomQuery|Cef" scenarios/uitk/prerun/*.cs` が 0 件（Task 10 Step 2・Task 14 Step 1。D-18）。`done.md` に撮影名の一覧 |
| R-16 | 台帳の記録 | 台帳「## 実測の結論」の末尾に 2 行（全録画の 3 回目の UITK 側・Web 側）。「## 参照キャプチャ」に 1 行（R-9）。「## 壊れた録画の割り振り」の 2 行（R-3・R-4） |
| R-17 | 申し送りの処遇 | 「## 申し送りの処遇」の全行に「やった（Task n・証拠）／回した（bd id・宛先）／閉じた（理由）」が付き、`done.md` に写す |
| R-18 | 最終 HEAD でラダー 6 本と UI テスト全体 | 最終 HEAD（取り込み後）で `uloop compile` ErrorCount 0、`^Client\.Tests\.Ui\.` 全 PASS、ラダー 6 本が GameUi で `Success: true`・ErrorLogs 0 |
| R-19 | G1 | `python3 .github/scripts/check-uitk-web-follow.py --base origin/feature/uitk --head HEAD` が違反 0。PR の `UITK Web follow gate (G1)` が pass |
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
3. Unity Editor: **Mac mini の Editor は全体で 1 本まで**（このレーンの worktree の 1 本。起動・停止の前に `moores-wt status` で所有を確かめる）。2 本目以降が要るときは Windows（RTX2060）の `~/bin/winunity`（push 済みのブランチを使う。`winunity compile|test|test-all|play <branch> <scenario.cs> [--ui-system GameUi|Web]|edc`）。subagent は Editor を起動しない。master の参照の撮影は `--no-editor` の worktree で行う
4. 区切り（タスク）ごとにコミットする。subagent はバックグラウンドのポーリングで待たず前景で実行する（1 回の Bash は 600 秒以内に収める。全録画は 3 本ずつ）。`.superpowers/` はコミットしない
5. 録画: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client <scenario.cs>`。このレーンは X-5a より前なので、PlayMode を止めた状態で `uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); Client.Localization.Localize.Initialize(); return Client.Localization.Localize.TrySetChosenLanguage("japanese");'`（Web は `UiSystem.Web`）を流し、戻り値が `true` であることを見てから入る（`run-all-recordings.sh` は 1 本ごとに系を自分で立てる）。**撮影（Task 8〜10・12・14 の撮り直しを含む）の前は毎回この前置きで表示言語を日本語に固定する**: Web の参照は mock-host の既定の日本語（`e2e/mock-host/topics/topicFixtures.ts:44` `locale: "japanese"`）で、ゲームは `PlayerPrefs` の言語（無ければ english。`Client.Localization/Localize.cs:14,35-37`）で起き、`TrySetChosenLanguage` は `PlayerPrefs` へ保存するので、前に流したシナリオ（P の出所の `uitk/uitk-notification-compare.cs:57` は最後に english にする）の言語が次の撮影へ残る。edit mode では辞書が未初期化なので先に `Localize.Initialize()` を呼ぶ（`IsSelectable` が辞書を引く。`Localize.cs:156-160`）。`prerun/` の 5 本と state-label は本体の冒頭でも `p.Assert(Localize.TrySetChosenLanguage("japanese"), …)` を呼ぶ（前例 `connect/rotary-generator-ui.cs:54`。D-30）。UI テスト（`UiTestHost`）の TearDown はスイッチを Web に戻すので、テストの後の録画の前に必ず立て直す（W0-switch:41）。`run-scenario.sh` が初回 "already playing" で終わったら 1 回だけ投入し直す（bd moorestech-trfka）。録画の前に push する（未 push の HEAD でバグ報告の箱が巨大になり送信トーストが 60 秒を超える。C2-b の申し送り）。受け口は `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` で本番へ送らない（winunity play は Windows の Editor に同じ値を入れて起こす。`winunity.d/interactive.ps1:30`）
6. 録画 mp4・`PlaytestResults/`・TestResults xml・撮影 PNG は worktree の gitignore 下に置き（全録画は `moorestech_client/Temp/uitk-recordings/`、撮影は `moorestech_client/.uloop/outputs/compare/X-prerun/`）、検証後に消す（報告ページへ載せる分は先に載せる）。`../moorestech_logs` に置くのは md と小さな JSON とスクショ 3 枚程度まで（書く前に `../moorestech_logs/README.md` を読む）。参照キャプチャの置き場 `moorestech_logs/uitk/reference-captures/` は既存の運用（台帳から辿る正本）。テストで `Saves/` に作ったワールドは `.agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で探して消す
7. 番号: 設計書 §7.1 の行は **A-250**（撮り方と独立評価）・**A-251**（撤去前の最後の Web の基準）、見た目の例外は **LX-210〜LX-217** だけ。計画・レーンは設計書・実行計画を書かない
8. 共有ファイル（`docs/uitk/TODO.md`・`docs/uitk/disposition.md`・USS・tss）は自分の行・節だけ触る。取り込みの衝突は両方の行を採る。`DerivedTokens.uss`（起草時 195 行）など 190 行を超える USS へ足すときは、先に責務で分けてから足す
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
- `docs/uitk/TODO.md` の「## 参照キャプチャ」（撤去前の最後の参照の 1 行）・「## 壊れた録画の割り振り」（X-prerun の 2 行の方針）・「## 実測の結論」末尾（2 行）・「## 見た目の例外」の LX-210〜217
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
| S2-ladder 依頼 1 | バグ報告の記述欄の幅 356px→354px（Web・UITK の両方で右枠が切れる） | 並べ比べで見る（Task 11）。UITK だけを 354px にすると Web と画素が変わるので、Web と同じに見える限り直さず `done.md`「要裁定」に Q-n で上げる（Web 側を直すのは master の Web の変更で、このレーンの範囲外） |
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
| 4-block-chest・4-block-tank・4-block-machine・4-block-gearMachine・4-block-generator・4-block-miner・4-block-gearMiner・4-block-filterSplitter・4-block-pumpNoVein・4-block-gearPump・4-block-gearGenerator・4-block-electricToGear・4-block-electricPole・4-block-spaceElevator（14 件） | 4 | N | `prerun/prerun-shots-blocks.cs`（平らな地面に置いて開く） | `bk-<id>-<w>x<h>` |
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
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-blocks.cs` | Create | 束 4 の N（ブロックを置いて開く 14 種） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-b5.cs` | Create | 束 5 の N |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-videos.cs` | Create | 動画 5 件の UITK 側（R-12） |
| `moorestech_client/Assets/Scripts/Client.Ui/**/*.uss` | Modify（並べ比べで再現した差だけ） | R-14 |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | Modify（USS を分けたときだけ。`@import` の自分の行） | R-14。分けた USS を読み込ませる |
| `docs/uitk/TODO.md` | Modify | 自分の 4 節（ファイルの持ち主） |
| `docs/uitk/disposition.md` | Modify | 「## 突き合わせの記録」の束 1〜5 の 5 行 |
| `../moorestech_logs/harness/uitk/X-prerun/done.md` | Create（repo 外） | 報告（撮影の対応表・全録画の結果・差の表・5a への申し送りを含む） |
| `../moorestech_logs/uitk/reference-captures/web-<sha10>/`・`web-<sha10>-2560x1080/` | Create（repo 外） | R-9 |
| （EditMode の撮影のコード片） | repo に置かない | `moorestech_client/.uloop/outputs/compare/X-prerun/edc/<id>.cs`（gitignore 下）に書いて `uloop execute-dynamic-code --code-file` で流す。中身の要点は `done.md` の対応表の行に書く |

`scenarios/uitk/` の直下は起草時 28 本で 10 を超えているので、新しいファイルは足さず `prerun/` に置く（前例 `stage3/`・S4-gamma D-24 の `stage4/`）。`prerun/` は 5 本（10 本まで）。

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
python3 .github/scripts/check-uitk-web-follow.py --base origin/feature/uitk --head HEAD; echo "g1-exit=$?"
```
Expected: 束 1〜5 が `暗転`（起点付き）・Pending の表に持ち主の行が 0（ファイルが無いか、`Owners` の項目 `{ AnchorIds.… }` の数が 0。クラスの説明コメントの「S5-ladder」は数えない。起草時は項目 3）・parity exit 0・G1 exit 0。parity が 1 なら未対応・写す予定の行の持ち主（束の画面レーン。取り込み済みなら I-fix）へ bd を `--parent moorestech-albof` で立て、Step 2 と同じ手順で「状態: 停止（突き合わせの差分 n 件の修正待ち）」で止まる

- [ ] **Step 4: Editor と compile**

Run:
```bash
moores-wt status
uloop compile --project-path ./moorestech_client
```
Expected: Mac の Editor はこのレーンの worktree の 1 本だけ（指揮が `moores-wt new` で起こした）。無ければ他の Editor が 0 本であることを見てから `uloop launch ./moorestech_client`。他者の Editor があれば落とさず終了を待つ。compile は ErrorCount 0

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
Expected: 両方 exit 0・`result.json` `Success: true`・ErrorLogs 0・Assert 4/4。落ちたら Note の `text=` で状態の取り違え（素材の不足で「研究可能」にならない等）かを見て Step 1 の素材を直す。UITK の画面の不具合なら研究の持ち主（I-fix）へ bd を立てて Task 5 の「不具合」と同じく扱う

- [ ] **Step 4: renumber を消し、台帳の 2 行を書く**

Run:
```bash
git grep -n "tutorial-research-renumber" -- ':!docs/superpowers/plans' ':!docs/uitk-migration'
git rm .agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-research-renumber.cs
```
Expected: grep は台帳の行（`docs/uitk/TODO.md`）と消すファイル自身の 37 行目の 2 件だけ（この 2 つ以外にあれば `done.md`「integrator への依頼」に書く。文書の参照はこのレーンは書き換えない）。`git rm` の後に同じ grep を流し直し、台帳の行の 1 件だけになることを見る。
台帳「## 壊れた録画の割り振り」の 2 行の方針の列を次へ書き換える（直すレーンの列 `X-prerun` は残す）:
- `tutorial-research-renumber.cs`: `削除した（X-prerun。ADR 0038 で研究 1〜3 が消え、役目が終わった）`
- `misc/research-node-state-label.cs`: `書き直した（X-prerun。ピン先マスタの原始研究 1〜3。GameUi run <dir 名>・Web run <dir 名> とも緑）`

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
- Produces: `moorestech_client/Temp/uitk-recordings/{list.txt,list-excluded.txt,gameui.tsv}`

- [ ] **Step 1: 一覧を作り、書き直した state-label を足す**

Run:
```bash
D=moorestech_client/Temp/uitk-recordings; mkdir -p $D
$REC/list-recordings.sh GameUi > $D/list.txt 2> $D/list-excluded.txt
echo .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs >> $D/list.txt
sort -u -o $D/list.txt $D/list.txt
wc -l $D/list.txt; cat $D/list-excluded.txt
```
Expected: `list-excluded.txt` に state-label の 1 行だけ（renumber は消えたので出ない。台帳の行の `X-prerun` で外れる。W6-9 でスクリプトは書き換えず一覧へ明示で足す。D-8）。`list.txt` の本数を `done.md` へ

- [ ] **Step 2: 3 本ずつ流す**

Run（前景で。開始行を 3 ずつ進めて最後まで。1 本で 600 秒を超えたシナリオは次から 1 本ずつ）:
```bash
git push
$REC/run-all-recordings.sh GameUi $D/list.txt $D/gameui.tsv 1 3
$REC/run-all-recordings.sh GameUi $D/list.txt $D/gameui.tsv 4 3
```
Expected: 最後に `tail -n +2 $D/gameui.tsv | wc -l` が `list.txt` の本数と同じ

- [ ] **Step 3: 落ちた行を分ける**

exit 0 以外の行ごとに `result.json` と `uloop get-logs --project-path ./moorestech_client --log-type Error` の該当部分を読み、[A-177] の 6 分類に分ける:
- `PASS`（0）。S2-ladder の `tutorial-craft-guidance` は Web の回だけ「PASS（Web は UI 未確認・既知）」
- `束 5 未移植`: 0 のはず。出たら `不具合` として扱う（S5 の完了条件の崩れ）
- `wwxy`: 台帳の 10 行のうち現存する 8 本（起草時: S2-ladder の 4 本〈pebble・equip・tree-pin-nearest-search・server-notification-smoke〉・S3-ladder の 3 本〈blueprint-copy-paste・blueprint-right-click-delete・fps-tps-view-toggle〉・state-label）と、行の外の後継 `connect/electric-wire-tool-extend-chain-via-ui.cs` が落ちたら `不具合`。現存の数は台帳の 10 行を 1 行ずつ `find` で「現存／削除／後継」に分けて数え直してから当てる（固定の数を当てない）
- `前提つき`（`playtest-receiver/`・IME の `.sh`）: そのシナリオのヘッダの手順で 1 回流し直し、落ちた assert ごとに次で振り直す（先回りで分類しない。`playtest-receiver/developer-report-delivery.cs` も同じ。D-29）。全部通る → `PASS`。落ちたのが Web に依る assert（`webUiHealth` の欄を見るもの）だけ → `前提つき（5a が assert を書き直す）`（`Web 専用` の文字列を含めない。5a は v7 の assert へ書き直す側で、消さない）。それ以外の assert が GameUi で落ちる → Task 4 の Web 側の同じシナリオの行と並べ（経路 M では Web 側がまだ無いので、この行は Task 4 Step 2 の後に決める）、Web では通るなら `不具合`（下の手順で持ち主へ）、Web でも同じ assert が落ちるなら受け口の前提の崩れなので `前提つき（両系で落ちる: <assert>）` として 5a への申し送りに書き、bd を `--parent <bd>` で 5a 宛てに立てる（実箱ロックの NRE〈C2-b:71「受け口の差し替えの影響か GameUi が原因かは未切り分け」〉はこの比べで切り分ける）
- `Web 専用`（シナリオ全体が CEF・Web の DOM を見るもの。例 `misc/cef-webui-showcase.cs`）: 落ちた assert を `done.md`「## 5a への申し送り」の「### 消す」へ。assert の一部だけが Web に依るシナリオはここに入れない（上の `前提つき` の規則）
- `不具合`（UITK の画面・操作の誤り）: 1 本ずつ bd を `--parent <bd>` で立て、持ち主（その画面のレーン。取り込み済みなら I-fix）への依頼文（シナリオのパス・落ちた assert・Error の行・run dir の録画）を指揮へ返し、`done.md` 1 行目を「状態: 停止（全録画の不具合 n 件の修正待ち）」にして push・`cmux wait-for -S uitk-x-prerun` を 1 回送り、指揮の再開の連絡を待つ。入ったら `git merge --no-ff origin/feature/uitk` で取り込み、compile のあと `不具合` の行だけを `grep` で作り直した一覧で流し直す（前例 S4-gamma Task 9 Step 3）。シナリオ側の誤り（古い testid・Web の型の参照）なら 5a への申し送りに書いて `Web 専用` か「シナリオの誤り（5a）」に分ける

- [ ] **Step 4: コミットは無し**（Task 5 で記帳）

---

### Task 4: 全録画の Web 側と S4-gamma との比べ（R-7）

**Files:** なし

**Interfaces:**
- Consumes: Task 1 の経路（W か M）・`$S4G`・S4-gamma の `done.md`「## 全録画の結果」の表（シナリオ / UITK の分類 / Web の分類 / 秒）
- Produces: `moorestech_client/Temp/uitk-recordings/{list-web.txt,list-web-excluded.txt,web.tsv,web-base.tsv}`

- [ ] **Step 1: 一覧**

Run:
```bash
$REC/list-recordings.sh Web > $D/list-web.txt 2> $D/list-web-excluded.txt
echo .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/research-node-state-label.cs >> $D/list-web.txt
sort -u -o $D/list-web.txt $D/list-web.txt
wc -l $D/list-web.txt
```
Expected: `scenarios/uitk/` の全本（`prerun/` を含む）が外れる

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
Expected: `web.tsv` の行数が `list-web.txt` の本数と同じ

- [ ] **Step 3: S4-gamma の Web 側と比べる**

exit 0 以外の行を、S4-gamma の表の Web の分類と並べる:
- S4-gamma でも落ちていた → `既存`（S4-gamma の分類を写す）
- S4-gamma に無いシナリオ → `比較不能（新設）`
- S4-gamma にあり、`git diff --quiet $S4G HEAD -- <シナリオ>` が差ありを返す → `比較不能（変更）`（変えたレーンの PR の報告ページで見たことを書く）
- S4-gamma で PASS・中身も同じ → `$S4G` で流し直す:
```bash
git push origin "$S4G":refs/heads/uitk-tmp/prerun-s4g-base
~/bin/winunity play uitk-tmp/prerun-s4g-base <シナリオ> --ui-system Web; echo "exit=$?"
```
Windows で CEF が描けない（経路 M）なら、自分の Editor を閉じ（`moores-wt status` で自分のものと確かめる）、`moores-wt new uitk-tmp/prerun-s4g-base --from "$S4G"`（worktree の名は既定でブランチ名の最後の段 `prerun-s4g-base`）の Editor で、録画の前に Task 1 Step 6 と同じ 2 行を一時の worktree へ向けて流し（`T=~/hermes-agent/data/worktrees/moorestech/prerun-s4g-base; test -d $T/moorestech_web/node || cp -Rc ~/hermes-agent/data/repos/moorestech/moorestech_web/node $T/moorestech_web/node; (cd $T/moorestech_web/webui && pnpm install --frozen-lockfile)`。抜かすと録画が ready の後で止まり、`退行` を `既存` と読み違える。`docs/development/worktree-and-review-pitfalls.md`）、同じシナリオを Web で流し、`moores-wt rm prerun-s4g-base --force --prune-branch`（`rm` は worktree の名かパスで引く。ブランチ名では「worktreeが見つかりません」で止まる）で畳んでから自分の Editor を `uloop launch ./moorestech_client` で起こし直す（D-6）。
落ちる → `既存`、通る → `退行`。`退行` は Task 3 Step 3 の `不具合` と同じ手順で回して待つ。流し終えたら `git push origin --delete uitk-tmp/prerun-s4g-base`

- [ ] **Step 4: コミットは無し**

---

### Task 5: 全録画の記帳と 5a への申し送り（R-5・R-8・R-16）

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 実測の結論」の末尾 2 行）

- [ ] **Step 1: done.md に書く**

`done.md`「## 全録画の結果」に 2 つの TSV を「シナリオ / UITK の分類 / Web の分類 / 秒」の表で写す（run dir は書かない）。これが撤去前の最後の Web の基準（A-251）。`## 5a への申し送り` を「### 消す（シナリオ全体が Web 専用）」と「### 書き直す（Web に依る assert・型の参照だけを持つ）」の 2 つの小節に分け、前者に `Web 専用` の全行、後者に `前提つき（5a が assert を書き直す）`・`前提つき（両系で落ちる: …）`・「シナリオの誤り（5a）」の行と `Client.WebUiHost`／`PlaytestDomQuery`／`IsWebUiAvailable`／`Cef*` を参照するシナリオ（Run: `grep -rlE "WebUiHost|PlaytestDomQuery|IsWebUiAvailable|CefUnity|CefScreenMapper|CefInputForwarder" .agents/skills/unity-playmode-recorded-playtest/scenarios`）を「パス / 理由 / 5a の案」で並べる。「## 全録画の結果」の分類の列で文字列 `Web 専用` を含むのは前者の行だけであることを Run: `sed -n '/^## 全録画の結果/,/^## /p' ../moorestech_logs/harness/uitk/X-prerun/done.md | grep "Web 専用"` で確かめる（5a の Task 1 Step 4 がこの grep で消す一覧を取るため。「### 消す」以外の行〈Web の列に写した S4-gamma の分類を含む〉が当たったら、その行の語を `Web 専用` を含まない語に替える。D-29）。wwxy の 10 行を 1 行ずつ「現存／削除／後継」に分けて数え直し（Run: 台帳の各行のパスを `find .agents/skills/unity-playmode-recorded-playtest/scenarios -path "*<パス>"`。起草時は現存 8・削除 2〈electric-wire・renumber〉・後継 1〈extend-chain〉）、現存の全本と後継の結果を書き、全部 PASS なら bd `moorestech-wwxy` に note（close は Task 16）

- [ ] **Step 2: 台帳の 2 行**

「## 実測の結論」の末尾に:
```markdown
- 全録画の 3 回目・UITK 側（X-prerun、<HEAD 10 桁>、全体スイッチ GameUi・撤去の前）: <n> 本中 PASS <a>・前提つき（流し直して PASS）<c>・Web 専用 <d>・束 5 未移植 0・不具合 0（途中で見つけた <k> 件は bd <ids> で直して再走 PASS）。結果の表は `harness/uitk/X-prerun/done.md`「## 全録画の結果」
- 全録画の 3 回目・Web 側（X-prerun、<HEAD 10 桁>、`scenarios/uitk/` は除く・撤去の前の最後の Web 側）: <n> 本中 PASS <a>・既存の失敗 <e>（S4-gamma <S4G> でも落ちる: <シナリオ名>）・比較不能（新設）<f>・比較不能（変更）<g>・退行 0
```
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

- [ ] **Step 2: 1280×720**

Run:
```bash
case "$L" in /*) ;; *) echo "L が絶対パスでない: $L"; exit 1;; esac
cd $R/moorestech_web/webui
UITK_REFERENCE_OUT=$L/web-$SHA UITK_REFERENCE_PORT=5791 node --import tsx e2e/uitk-reference/capture.ts
python3 -c 'import json,sys;m=json.load(open(sys.argv[1]));print(m["sourceCommit"],m["viewport"],len(m["entries"]),sum(e["kind"]=="video" for e in m["entries"]))' $L/web-$SHA/manifest.json
```
Expected: `<SHA> 1280x720 <n> 5`（起草時は 89 件・動画 5）。ポートが塞がっていれば別の空きポート

- [ ] **Step 3: 2560×1080（W6-4）**

Run:
```bash
cd $R/moorestech_web/webui
sed -i '' 's/export const referenceViewport = { width: 1280, height: 720 } as const;/export const referenceViewport = { width: 2560, height: 1080 } as const;/' e2e/uitk-reference/shotRunner.ts
grep -n "referenceViewport =" e2e/uitk-reference/shotRunner.ts
pnpm build
UITK_REFERENCE_OUT=$L/web-$SHA-2560x1080 UITK_REFERENCE_PORT=5791 node --import tsx e2e/uitk-reference/capture.ts
python3 -c 'import json,sys;m=json.load(open(sys.argv[1]));print(m["viewport"],len(m["entries"]))' $L/web-$SHA-2560x1080/manifest.json
git -C $R checkout -- moorestech_web/webui/e2e/uitk-reference/shotRunner.ts
```
Expected: grep が `2560, height: 1080`・manifest が `2560x1080 <n>`（Step 2 と同じ件数）・checkout で書き換えが消える（コミットしない）

- [ ] **Step 4: 目で見る・logs へ入れる・worktree を畳む**

`web-$SHA/` の静止画を 5 枚（束ごとに 1 枚）開き、白画面・mock-host の起動失敗の画面でないことを見る。`../moorestech_logs/README.md` を読み、`uitk/reference-captures/web-$SHA/`・`web-$SHA-2560x1080/` を手でコミット・push する（`git log origin/main..main --stat` で押し出すものを確かめる）。畳む前に Run: `ls $L/web-$SHA/manifest.json $L/web-$SHA-2560x1080/manifest.json` が 2 つとも出ることを見る（無ければ畳まず、参照がどこにできたかを `find $R -name manifest.json -newer $R/moorestech_web/webui/package.json` で探して `$L` へ移す）。Run: `moores-wt rm uitk-prerun-ref --force --prune-branch`（`--dir` で付けた worktree の名で引く。ブランチ名 `uitk-tmp/prerun-ref` では `resolve_worktree` が「worktreeが見つかりません」で止まる。未参照の master ピンが報告されたら `--prune-master` を足して流し直す）

- [ ] **Step 5: 台帳に書いてコミット**

「## 参照キャプチャ」の末尾の規則の行の前に 1 行:
```markdown
- 撤去の前の最後の全画面の参照（X-prerun）: 撮影元 moorestech `<SHA>`（origin/master）。1280×720 は `moorestech_logs` の `uitk/reference-captures/web-<SHA>/`（<n> 件。https://github.com/moorestech/moorestech_logs/tree/main/uitk/reference-captures/web-<SHA> ）、2560×1080 は `uitk/reference-captures/web-<SHA>-2560x1080/`（同じ件数。`shotRunner.ts` の `referenceViewport` を使い捨ての worktree の中だけで書き換えて撮った）。切替後はこの組が Web 版の見た目の最後の正本
```
```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 撤去の前の全画面の参照を撮り直して台帳に書く（X-prerun）"
```

---

### Task 7: 撮影の対応表と機械検査（R-10）

**Files:** なし（`done.md`）

**Interfaces:**
- Consumes: Task 6 の `web-<SHA>/manifest.json`、上の「## 撮影の対応表（起草時の案）」、各レーンの `done.md`
- Produces: `done.md`「## 撮影の対応表」（列: `| id | 束 | 撮り方 | 出所 | 出力名 | 判定 |`。id は 1 行に 1 つ。`…` で省略しない）

- [ ] **Step 1: 起草時の案を新しい manifest の id で 1 行ずつに展開する**

新しい manifest の `entries` を順に読み、起草時の案の行（まとめて書いた行は 1 id ずつに割る）から撮り方を写す。新しく増えた id は、同じ `ledgerRow` の既存の行の撮り方を初期値にする。E の行は「前例」列のレーンの `done.md` を `grep -n "<id>\|UiRuntimePanelCapture\|EditMode" ../moorestech_logs/harness/uitk/<レーン>/done.md` で読み、そのレーンが使った外殻と偽の Model の組み方（型名・見本の名前）を「出所」列に写す。見つからなければ「前例なし」と書き、Task 9 Step 2 で組み方を決めて書く。P の出所のシナリオが実在しない行（Run: `ls <パス>`）は N に切り替える。X の行は `grep -n "再接続" docs/uitk/disposition.md` で S5-crosscut の理由の行を確かめて出所に行番号を書く（無ければ X のまま `done.md`「integrator への依頼」に「S5-crosscut の disposition の行の欠け」と書く）。新しく増えた id が [A-38] の画面（辞書エラー等）なら X にする。それ以外を X にしない

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
print(f"manifest {len(ids)} / 表 {len(rows)} / 抜け {missing} / 余り {extra} / 重複 {dups} / 撮り方の誤り {bad}")
sys.exit(1 if missing or extra or dups or bad else 0)
EOF
echo "exit=$?"
```
Expected: `manifest <n> / 表 <n> / 抜け [] / 余り [] / 重複 [] / 撮り方の誤り []`・exit 0

- [ ] **Step 3: 状態の差を書き出す**

参照は mock-host の高密度の fixture で、実ゲームの撮影とデータの状態が違う。P と N の行ごとに状態の差（行数・アイテム・名前・選択・スクロール・世界の地）を `moorestech_client/.uloop/outputs/compare/X-prerun/states.md` に先に書き出す（前例 S4-gamma Task 13 Step 2b）。並べ比べでは状態に依らない観点（配置・寸法・色の系統・装飾の有無・文字の大きさと段組・余白）だけを比べる（D-5）

- [ ] **Step 4: コミットは無し**（`done.md` を logs でコミット・push）

---

### Task 8: UITK の撮影・PlayMode（P と N。R-11）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-pause-train.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-blocks.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-shots-b5.cs`

**Interfaces:**
- Consumes: `PlaytestGameViewOps.SetResolution(CancellationToken, int, int)`（`Client.Playtest.Operations.Ui`）、`p.Screenshot(string)`、`PlaytestOverlayView`（`Client.Playtest.Overlay`。撮る前に消す・前例 `uitk-stage3-compare.cs:108-113`）、`GameUiRoot.Instance.GetRootElement()`（`Client.Ui.Foundation.Root`）、`p.PressInteract()`・`p.PressRide()`（`Client.Playtest.Operations` の拡張）、`p.SetupFlatGround()`（足場 `PlaytestSetup.GroundCenter`=(0,30,0)・`GroundSize`=(50,4,50)。`Client.Playtest/Operations/PlaytestSetup.cs:23-24`）・`p.PlaceBlockDirect(string, Vector3Int, BlockDirection)`・`p.WaitBlockGameObject(Vector3Int)`・`p.RemoveBlock(Vector3Int) : UniTask<bool>`（`PlaytestDriver.cs:83,88,101`）・`p.WarpPlayer(Vector3)`・`p.AimAt(Vector3)`、`TutorialPresentationStateStore.Instance.BeginSession(Guid)`・`AddOutlineHighlight(string, Guid)`・`EndSession(Guid)`（`Client.Game.InGame.Tutorial`）、`AnchorIds.PauseSettings`・`AnchorIds.ResearchNode(string)`・`AnchorIds.ChallengePanel`（`Client.Game.InGame.Tutorial.UIHighlight`）、`MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0].Challenges[0].Tutorials[0].TutorialGuid`（前例 `uitk-tutorial-cues.cs:76`）、乗車の手順 `PrepareAndRideTrain`・`PlacePier`（`uitk/uitk-nested-pause-train.cs:83-114`）、`Localize.TrySetChosenLanguage(string) : bool`（`Client.Localization`。前例 `connect/rotary-generator-ui.cs:54`）
- Produces: `compare/X-prerun/<id>-1280x720.png`・`<id>-2560x1080.png`（P と N の全 id）

- [ ] **Step 1: ポーズと列車の撮影シナリオ**

`prerun/prerun-shots-pause-train.cs`（200 行未満）。冒頭の using は `uitk-nested-pause-train.cs:3-28` を写し（28 行目の `using Client.Playtest.Input;` が `SemanticInput` を解く）、`Client.Localization`・`Client.Playtest.Operations.Ui`・`Client.Playtest.Overlay`・`Client.Ui.Foundation.Root`・`UnityEngine.UIElements` を足す。本体:
```csharp
// 撤去の前の並べ比べ（X-prerun）: ポーズ・設定・バグ報告と、乗車中の列車 HUD・入れ子のポーズを 1280×720 と 2560×1080 で撮る（録画しない）
// Pre-removal comparison (X-prerun): shoot pause, settings, bug report, the riding train HUD and the nested pause at 1280x720 and 2560x1080 (no recording)
var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
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

`prerun/prerun-shots-blocks.cs`（200 行未満）:
```csharp
// 撤去の前の並べ比べ（X-prerun）: 平らな地面にブロックを置いて開き、ブロック UI を 1280×720 と 2560×1080 で撮る（録画しない）
// Pre-removal comparison (X-prerun): place blocks on flat ground, open them and shoot the block UI at 1280x720 and 2560x1080 (no recording)
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
    ("4-block-gearMiner", "原始的な採掘機"), ("4-block-filterSplitter", "フィルター分岐器"), ("4-block-pumpNoVein", "油井"),
    ("4-block-gearPump", "歯車ポンプ"), ("4-block-gearGenerator", "燃料式風車"), ("4-block-electricToGear", "回転生成機"),
    ("4-block-electricPole", "電柱"), ("4-block-spaceElevator", "宇宙エレベーター拠点"),
};
var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
return PlaytestRunner.Run("prerun-shots-blocks", options, async p =>
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
        p.Note($"{id} {width}x{height} panel={GameUiRoot.Instance.GetRootElement().Q("block-inventory-panel").worldBound}");
        await p.Screenshot($"bk-{id}-{width}x{height}");
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    #endregion
});
```
置く位置 (-6,32,-6) は、ピン先マスタ e0c913033c の最大の寸法（液体タンク 9×7×9・ガソリンエンジン発電機 7×5×13・油井 3×8×9・宇宙エレベーター拠点 9×4×9）でも足場（`PlaytestSetup.GroundCenter`=(0,30,0)・`GroundSize`=(50,4,50)。x・z は -25〜25）の内側に収まり、手前 1.5 マスに立つ位置も足場の上にある。足場の外へ置くとワープ先の下に足場が無く開く手順が時間切れになる（D-19）。足場の上で開けなかったブロック（UI を持たない・置けない）だけを、Step 4 の run の落ちた行を見て表の撮り方を E に替え、配列から外す（`done.md` に理由）。座標・照準の誤りで落ちた行は E に替えず、座標を直して流し直す。`p.Note` の panel の矩形は Task 9 Step 1 の EditMode との一致の確かめに使う。

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
        var session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.PauseSettings, tutorialGuid);
        await p.Until(() => root.Q("tutorial-highlight-label") != null, 10f, "枠のラベル");
        await Shoot("5-tutorial-outline-label", w, h);
        TutorialPresentationStateStore.Instance.EndSession(session);
        await p.ExitToGameScreen();

        // 研究ツリーの原始研究1 のノードに強調枠
        // Outline the primitive research 1 node in the research tree
        await p.PressKey(Key.R);
        await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
        session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.ResearchNode("858bcb10-b8ba-478e-9bc5-473ca61281a2"), tutorialGuid);
        await p.Until(() => root.Query(className: "tutorial-outline__ring").First() != null, 10f, "研究ノードの枠");
        await Shoot("5-tutorial-research-node", w, h);
        TutorialPresentationStateStore.Instance.EndSession(session);
        await p.ExitToGameScreen();
```
ループの前に `var root = GameUiRoot.Instance.GetRootElement();` と `var tutorialGuid = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0].Challenges[0].Tutorials[0].TutorialGuid;`（前例 `uitk-tutorial-cues.cs:76`）を置く。出展モードの言語ゲート（`5-event-language-gate`）はこのシナリオで撮らない。ゲートはゲームの DI より前に `MainGameInitializationFinalizer` から 1 度だけ出る全画面で（`Client.Ui.State/EventMode/EventLanguageGate.cs:18`）、起動後のゲームで出し直す公開の口が無く、口を足せば本番にテスト専用の口が残るので、E で撮る（Task 9 Step 2・D-31）。S5-ladder が `stage5/` に同じ状態を撮るシナリオを持っていれば、その id はこのファイルから外して P にする（Task 7 の表を直す）

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
戻り値 `true`（日本語に固定できた）を見る。同じ前置き（1 本ごとに毎回。P の `uitk/uitk-notification-compare.cs` は最後に english を保存するので、流した後の撮影は前置きで日本語に戻す）で `prerun-shots-blocks.cs`・`prerun-shots-b5.cs` と、表の P の出所のシナリオ（`uitk/stage2/uitk-stage2-compare.cs`・`uitk/uitk-notification-compare.cs`・`uitk/uitk-skit-compare.cs`・`uitk/stage3/uitk-stage3-compare.cs`・`uitk/stage4/uitk-stage4-wave-shots.cs`・S5-ladder の段 5 の撮影）を流す。
各 run dir の PNG を、表の「出力名」列から `$C/<id>-<w>x<h>.png` へ `cp` する（skit-compare の `<prefix>` が `720p` だけの状態は 1280×720 の行だけを埋め、2560×1080 が無い id は表の撮り方を N に替えて `prerun-shots-*` に足す）。
Expected: 全 run が exit 0・ErrorLogs 0。Run: `for id in <表の P と N の id>; do for r in 1280x720 2560x1080; do test -s $C/$id-$r.png || echo "無い: $id $r"; done; done` が何も出さない

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
- Produces: E の全 id の `$C/<id>-<w>x<h>.png`、`$C/video/<id>-{start,mid,end}-{web,uitk}-{1280x720,2560x1080}.png`

- [ ] **Step 1: EditMode と PlayMode の一致を先に確かめる（[A-164]）**

`4-block-chest` を E の手順でも 1280×720 と 2560×1080 の両方で撮り、Task 8 の PlayMode の `bk-4-block-chest-1280x720`・`bk-4-block-chest-2560x1080` と比べる（E の id は 2560×1080 でも撮るので、舞台の倍率が合っているかをその解像度でも見る）。PlayMode を止め、`$C/edc/4-block-chest.cs` に S4-alpha の `done.md` の組み方（本番の `BlockInventoryView` を `StageColumnsLayout` の Viewer 列に載せ、木のチェストの本文と偽の詳細）で外殻を組み、`block-inventory-panel` の `worldBound` を返す式を書き、先頭で `Client.Localization.Localize.Initialize(); Client.Localization.Localize.TrySetChosenLanguage("japanese");` を呼んで（`InitializeDictionary` の `Localize.Initialize()` は `PlayerPrefs` の言語を読むので、保存された日本語で辞書が立つ。D-30）、`UiRuntimePanelCapture.CaptureToPng(<外殻>, <w>, <h>, new Color32(<参照の地の画素>, 255), UiCaptureLocalization.InitializeDictionary, "<絶対パス>/$C/edc-4-block-chest-<w>x<h>.png")` を 1280×720 と 2560×1080 の 2 回呼ぶ:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code-file $C/edc/4-block-chest.cs
```
PlayMode 側の panel の矩形は、`prerun-shots-blocks.cs` が撮影の直前に打つ `p.Note`（`<id> <w>x<h> panel=<worldBound>`。Task 8 Step 2 のコードに最初から入っている）の `4-block-chest 1280x720` と `4-block-chest 2560x1080` の行から取る（`worldBound` は panel の論理座標なので、2560×1080 は舞台の倍率を掛けた画面の px に直して比べる）。
Expected: panel の x・y・幅・高さの差が 1280×720 で 2px 以内、2560×1080 で倍率を掛けた 2px 以内（どちらも `done.md` に値）。超えたら舞台の論理寸法（PlayMode の root の大きさ）を EditMode の外殻へそろえて撮り直し、それでも超えるなら E の行の比べ方を「状態に依らない観点だけ・位置は PlayMode の組と照らす」と表に書く（`done.md`）

- [ ] **Step 2: E の全 id を撮る**

表の E の行ごとに `$C/edc/<id>.cs` を書き（出所のレーンの組み方を写す。地の色は新しい参照の該当画面の外側の 1 画素）、先頭で Step 1 と同じく日本語に固定してから、1280×720 と 2560×1080 の 2 回 `CaptureToPng` を呼ぶ。辞書を足すテストの見本を使うときは `UiCaptureLocalization.KeepCurrentDictionary` にし（このときも日本語に固定してから辞書を足す）、撮った後に辞書を戻す（前例 S4-research。言語は日本語のままにする）。`5-event-language-gate` は S5-gate のゲートの L1 の土台（`Client.Tests/Ui/Screens/<ゲート>/Support/`。起草時の HEAD に無いので Task 7 Step 1 で S5-gate の `done.md` と実物から型名を写す）の外殻に、mock-host の `eventLanguageGateWaiting` の topic と同じ待ち状態の Model を渡して撮る（実行時に出し直す口が無い。D-31）。前例の無い id は、その画面の L1 の土台（`Client.Tests/Ui/Screens/<画面>/Support/`）の外殻に、Web の fixture（`moorestech_web/webui/e2e/mock-host/` の該当 fixture）と同じ値の ViewModel を渡す形で組み、`done.md` の表の出所に型名を書く。
Expected: Run: `for id in <表の E の id>; do for r in 1280x720 2560x1080; do test -s $C/$id-$r.png || echo "無い: $id $r"; done; done` が何も出さない。撮れない状態（外殻が実行時の DI を要する等）は `done.md`「integrator への依頼」に道具の口の不足として書き、その id の比べ方を「各レーンの PR の並べ比べを証拠にする」（W6-3 と同じ扱い）に替えて表に書く。この逃げ道を使った id は R-11 未達なので、`done.md`「要裁定」に Q-n（外した id・代わりの証拠の報告ページの URL・外した理由・推奨案）でまとめて上げてから先へ進む（実行計画 §1.5 の完了条件「UITK の全画面と並べ比べ」を省く判断はユーザーのもの。0 件なら何もしない。D-23）

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
        session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        start = Mark("5-tutorial-pulse", w, h);
        TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.GameCrosshair, tutorialGuid);
        await p.Until(() => IsShown("tutorial-highlight-label"), 5f, "枠のラベルが出た");
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
Expected: 5 件 × 3 相 × 2 系 × 2 解像度＝60 枚。録画の時刻と Timeline の秒がずれる（録画の始まりが RunStart と違う）ときは、最初の `[video]` の操作の直後に出る要素が最初に写るフレームを探して差を測り、`T` に足す（`done.md` に差の秒）。2560×1080 の組は静止画の撮影に数フレームかかり相が少しずれうるので、評価役には「2560×1080 の動画の組は動きの位相の差を数えず、位置・寸法・色・装飾だけを見る」と渡す（Task 11 Step 2）

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

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
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
        var session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        TutorialPresentationStateStore.Instance.AddOutlineHighlight(AnchorIds.PauseSettings, tutorialGuid);
        await p.Until(() => root.Q("tutorial-highlight-label") != null, 10f, "枠のラベル");
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
Expected: 2 回とも exit 0・PNG 20 枚。PSNR が `inf` でない状態（世界の動き・カーソル・時刻で揺れる）は、その状態の撮影の前の待ちを延ばすか、揺れる部分（世界の空・水面）を `done.md` に書いて 5a〜5e の比べ方を「PSNR ではなく UITK の要素の矩形の差 0」に替える旨を「5a への申し送り」に書く（D-12）

- [ ] **Step 3: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-smoke-shots.cs
git commit -m "test(uitk): 撤去レーンの見た目の物差し prerun-smoke-shots を足す（X-prerun）"
```
`done.md`「## 5a への申し送り」に、流し方（Task 8 Step 4 の前置き〈日本語の固定を含む〉。5a の後は全体スイッチの行を消した前置き）・出力名の形 `smoke-<状態>-<w>x<h>.png`（run dir 内・1 回の run で 20 枚）・状態名 10 個・揺れた状態の扱いを書く。ヘッダの 2 言語のコメントにも同じ 4 点（出力名と置き場・状態名・前置き・比べ方）があることを Run: `head -14 .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/prerun/prerun-smoke-shots.cs` で見る（X-5a Task 1 Step 4 がヘッダの「撮影の解像度と出力先の説明」を前提に読む）

---

### Task 11: 並べ比べと独立評価 2 系統（R-13）

**Files:** なし（`done.md`・gitignore 下）

- [ ] **Step 1: 組を作る**

Run（静止画 id × 2 解像度。Web は新しい参照）:
```bash
mkdir -p $C/pairs
for id in <表の静止画の id（X を除く）>; do
  ffmpeg -v error -y -i $L/web-$SHA/$id.png -i $C/$id-1280x720.png -filter_complex "[0][1]hstack=inputs=2" $C/pairs/$id-1280x720.png
  ffmpeg -v error -y -i $L/web-$SHA-2560x1080/$id.png -i $C/$id-2560x1080.png -filter_complex "[0][1]hstack=inputs=2" $C/pairs/$id-2560x1080.png
done
for id in <表の V の id>; do for r in 1280x720 2560x1080; do for n in start mid end; do ffmpeg -v error -y -i $C/video/$id-$n-web-$r.png -i $C/video/$id-$n-uitk-$r.png -filter_complex "[0][1]hstack=inputs=2" $C/pairs/$id-$n-$r.png; done; done; done
ls $C/pairs | wc -l
```
Expected: 静止画 id 数（X を除く）× 2 ＋ 動画 5 × 3 × 2 の枚数。高さが違って hstack が失敗した組は、UITK 側の撮影の解像度の誤りなので撮り直す

- [ ] **Step 2: 評価役 A・B を束ごとに派遣する（本体が派遣し、結果をこのレーンが取りまとめる。D-16）**

opus の subagent を、評価役 A と B の 2 系統で、束ごと（束 4 は 2 つに分けて 1 回 24 組まで）に派遣する（D-3）。A と B は互いの結果を見ない。依頼文に渡すもの: ゴールの原文「WebUIと同じ見た目で、適切な設計、コンポーネント化されたUITKベースのUIを開発すること」、裁定 `.decisions/2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`・`…段階2以降の画面もWeb版と同じ見た目にする.md`、設計書 §3.3・§3.4、`compare-procedure.md` §5・§6、台帳「## 見た目の例外」の全行、`$C/states.md` のその束の行、組の画像のローカルパス（左が Web・右が UITK）。依頼:「各組の左右の差を、位置・寸法・色の系統・装飾（罫線・三角・影・ベベル・グラデ）の有無・文字の大きさと段組・欠けている要素・重なり・はみ出しで挙げる。states.md に書いた状態の差（データ・名前・行数・世界の地）は差に数えない。字形の差（Chrome と TextCore）は数えない。2560×1080 の動画の組（`<id>-<相>-2560x1080`）は UITK 側が同じ相の静止画で、相が数フレームずれうるので動きの位相の差は数えず、位置・寸法・色・装飾だけを見る。既存の LX に当たる差はその ID を書く。各差に座標（左上原点・px）と測った値を添え、重さ（一目で分かるか）を書く。合否の目安は 1280×720 で 2px。報告はファイル `<scratchpad>/x-prerun-eval-<A|B>-<束>.md` へ、返答は 3 行」。subagent はバックグラウンドのポーリングで待たず前景で実行し、Editor を起動しない

- [ ] **Step 3: 再現させる**

A・B の指摘を合わせて重ね（同じ組・同じ位置の差は 1 件）、指摘ごとに fresh-context の subagent に組の画像と指摘文だけを渡し「この差は画像で確かめられるか。px で測って」と頼む。画素は `ffmpeg -v error -i <png> -vf "crop=<w>:<h>:<x>:<y>" -f rawvideo -pix_fmt rgb24 - | python3 -c 'import sys;b=sys.stdin.buffer.read();print([tuple(b[i:i+3]) for i in range(0,len(b),3)][:16])'` のように生データで読む。再現したものだけ `done.md`「## 差の表」に「id / 解像度 / 差 / 測った値 / A・B のどちらが挙げたか / 処遇（Task 12 で埋める）」で残し、再現しなかったものは「再現せず」と 1 行で残す

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

- (a) USS の値（つまみの上書き漏れ・トークンの取り違え・半透明の補正漏れ・余白）で Web に寄せられる → このレーンが直す。Web の CSS（`feature/uitk` の `moorestech_web/webui/src/**`。読むだけ）と突き合わせて値を写す。直す場所は (1) 差のある画面の USS（`Client.Ui/Screens/<画面>/**`。つまみの上書き）を先に選び、(2) 画面の USS で直せず Web の CSS でも部品・トークン側の値が違うときだけ部品の USS（`Client.Ui/Components/**`）かトークン（`Client.Ui/Foundation/Theme/*.uss`）を直す（共有の USS は使い手の全画面を動かすので。どちらでも Step 2 の全 id の撮り直しで他の画面への波及を見る。D-34）
- (b) USS で直らず、C#・部品の作り直しが要り、差が一目で分かる（配置・色の系統・装飾の有無・文字の大きさ・欠け・重なり・はみ出し）→ 持ち主（その画面・部品のレーン。取り込み済みなら I-fix）へ bd を `--parent <bd>` で立て、依頼文（id・組の画像・測った値・Web の CSS の行）を指揮へ返し、`done.md` 1 行目を「状態: 停止（並べ比べの差 n 件の修正待ち）」にして push・合図を 1 回送って待つ。入ったら取り込み、該当の id だけ Task 8・9 の手順で撮り直して Task 11 Step 1・3 を回す
- (c) 同じにするための対応が大きい（部品の作り直し・数百行の描画・性能の劣化）かブラウザの癖の再現で、かつ一目で分からない → LX-210〜217 に記帳する
- (d) Web に寄せることがゴール・裁定と食い違う（Web 側が明らかに誤り・Web と同じにすると読めない等。例: 申し送りのバグ報告の記述欄の幅）→ `done.md`「要裁定」に Q-n（差・撮影・推奨案・帰結）で上げ、今の UITK のまま進める
- 既存の LX に当たる差 → その ID で処遇し、観測を `done.md` に書く

- [ ] **Step 2: USS を直す**

直す前に `wc -l <対象の USS>` を見て、190 行を超えていれば責務で分けたファイルを先に作ってから足す（tss の読み込みに 1 行足す。tss は自分の行だけ）。直したら:
Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Conventions|Theme|Screens\.<直した画面>)\." --timeout-seconds 1500`
直した USS が `Foundation/`・`Components/` の下にあるか、2 つ以上の画面から読まれる（Run: 直した USS のファイル名を `grep -rn "<ファイル名>" "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" moorestech_client/Assets/Scripts/Client.Ui --include=*.uss --include=*.tss --include=*.cs` で読み手を数える）ときは、テストのフィルタを `^Client\.Tests\.Ui\.` 全体にする。
Expected: ErrorCount 0・全 PASS。続けて、**直した id だけでなく全 id を撮り直す**（D-34）:
1. 最初の USS の修正の前に、それまでの UITK の PNG を `$C/before-uss/` へ写しておく（Run: `mkdir -p $C/before-uss && cp $C/*.png $C/before-uss/ && cp -R $C/video $C/before-uss/`。1 回だけ）
2. 直した後、Task 8 Step 4（P と N）・Task 9 Step 2（E）・Task 9 Step 3〜4（V）を同じ前置き（日本語の固定を含む）で全部流し直す
3. 静止画 id × 2 解像度と動画の 60 枚について、Run: `for f in $C/before-uss/*.png $C/before-uss/video/*.png; do n=${f#$C/before-uss/}; v=$(ffmpeg -v error -i "$f" -i "$C/$n" -lavfi psnr -f null - 2>&1 | grep -o 'average:[^ ]*'); [ "$v" = "average:inf" ] || echo "変わった: $n $v"; done > $C/uss-changed.txt; wc -l $C/uss-changed.txt` で直す前と比べ、変わった id を並べる。動画の相とカーソル・世界の揺れで変わるもの（Task 10 Step 2 の揺れと同じ種類）は同じ id を 2 回撮って揺れか USS の影響かを分けてから数える
4. 変わった id 全部（直した id と、波及した他の id）について Task 11 Step 1 の組を作り直し、Task 11 Step 2〜3（評価役 A・B と再現）を回し直す。直した id は差が 2px 以内か画素で測る。波及した id で新しい差が出たら、その差を「## 差の表」に足して Step 1 から処遇する（波及を直す USS を選び直す）
5. `done.md` の行に「直した・撮り直しの値・変わった id の一覧（`uss-changed.txt` の件数）」を書く。報告ページ（Task 16 Step 1）には最終 HEAD で撮った組を載せる
変わった id が 0 件（直した id 自身も変わらない）なら、USS が読み込まれていない（tss の `@import` の漏れ・セレクタの誤り）ので直し直す

- [ ] **Step 3: 例外を記帳する**

台帳「## 見た目の例外」の末尾に 1 行（列: ID／画面・部品／差／同じにしない理由／推奨案／撮影〈報告ページの URL。PR 作成後に Task 16 で埋める〉／出所〈agent 既定（X-prerun D-4）〉）と、差のある USS の行に `/* look-exception: LX-n */` を同じコミットで入れる。C# の行が要る差は印を置けない（本番の C# を触らない）ので (b) として持ち主へ回す。使える番号は LX-210〜217 の 8 本だけ。(c) が 9 件以上になったら、残りの行の処遇を「LX（番号待ち）」にして `done.md`「integrator への依頼」に番号の追加を頼み、(b) と同じく 1 行目を「状態: 停止（LX の番号待ち n 件）」にして push・合図を 1 回送って待つ（計画・レーンは実行計画 §6.1 の割り当てを書かない。D-24）。
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

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 1500`
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

- [ ] **Step 3: コミットは無し**（記録のみ）

---

### Task 15: moores-code-review で全ブランチレビュー（必須）

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。base は `origin/feature/uitk`。設計判断は agent 既定で推奨案を採り、判断記録に「agent 既定（moores-code-review Dn）」と書く（ゴール・裁定から外れるものだけ `done.md`「要裁定」）。Workflow の自動適用の後は対象テスト（Task 12 Step 2・Task 14 Step 1 のフィルタ）を流し直す（`docs/development/worktree-and-review-pitfalls.md`）。自動適用・反映がシナリオの判定（Assert・待ち）や USS に触れたら、Task 14 Step 2 の 7 本と、触れた撮影シナリオを反映後のバイナリで流し直し、USS なら Task 12 Step 2 の 1〜5（全 id の撮り直し・直す前との PSNR・変わった id の組の作り直しと評価役・再現）を回してから完了とする（実行計画 §4.3 の 1。D-34）。

---

### Task 16: feature/uitk への PR・取り込み・マージ・片付け・報告（必須）

セッション終了可能状態にすること: pr-createスキルでPRを作成し、コンフリクトがあれば解消・コンパイル確認のうえpushする。このレーンは実行計画 §4.3 の形（base=feature/uitk、agent がマージ）。

- [ ] **Step 1: PR**: pr-create で `base=feature/uitk`。報告ページ `https://review.moores.tech/pr/<番号>/report/` に、`~/hermes-agent/data/services/pr-review/publish/README.md` の manifest の形で、並べ比べ（`$C/pairs/` を束ごとの `group`。caption `Web <id> / UITK <id> <解像度>`。全部の組を最終 HEAD で撮った版にする〈USS を直した後は Task 12 Step 2 で全 id を撮り直してあるので、直す前の組を載せない〉。X の id は組を作らず `verification` に [A-38] の根拠の 1 行）・動画の 3 フレームの組・ラダー 6 本の `recording.mp4`・`prerun-smoke-shots` の 1280×720 の 10 枚を載せ、`verification` に全録画の 2 行（台帳の文面）・突き合わせ・テストの件数を書いて `python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <番号> <manifest.json>` で公開し、本文の先頭行をその URL の h1 にする。公開に失敗したら報告 URL 無しで完了にしない。LX を足したら台帳の撮影の列に報告ページの URL を入れてコミット・push
- [ ] **Step 2: 取り込み**: `git merge origin/feature/uitk`。台帳・USS の衝突は両方の行を採る。`uloop compile`・Task 14 Step 1 のテスト・`python3 .github/scripts/check-uitk-web-follow.py --base origin/feature/uitk --head HEAD`（違反 0）。取り込みがシナリオ・USS の判定に触れた衝突ならラダーと物差しの再走。push
- [ ] **Step 3: G1 とマージ**: PR の `UITK Web follow gate (G1)` が pass を見て `gh pr merge <番号> --merge`（Squash 禁止）
- [ ] **Step 4: 片付け**: `moores-wt status` で自分の Editor だと確かめて Editor を閉じる。全録画の `moorestech_client/Temp/uitk-recordings/`・`PlaytestResults/`・TestResults・`$C`（報告ページに載せた後）を消す。テストで作ったワールドを `list-work-saves.sh` で探して消す。winunity の一時ブランチ `uitk-tmp/prerun-s4g-base` が残っていれば `git push origin --delete` で消す。bd `moorestech-wwxy` を close（理由に PR URL と「10 行すべて片付いた」）・`<bd>` を close。worktree の `moores-wt rm` は指揮が行う
- [ ] **Step 5: 報告と合図**: `done.md` の 1 行目を「状態: 完了」（止まったら「状態: 停止（理由）」）にし、完了条件（R-1〜R-20）ごとの証拠・PR URL・未完了・要裁定・integrator への依頼（道具の口の不足・申し送りの「回した」行・A-250/A-251 と §6.1 の行の下書きは本計画の末尾にある旨）・5a への申し送り（Web 専用の一覧・物差しの使い方）を書いて `../moorestech_logs` でコミット・push。最後に `cmux wait-for -S uitk-x-prerun` を 1 回

## Self-Review

- Requirements の各行のタスク: R-1→T1、R-2→T1 Step 3・T13、R-3・R-4→T2、R-5→T2・T5・T16 Step 4、R-6→T3、R-7→T4、R-8→T5、R-9→T6、R-10→T7、R-11→T8・T9、R-12→T9、R-13→T11、R-14→T12、R-15→T10、R-16→T2・T5・T6、R-17→各タスクと T16、R-18→T14、R-19→T1 Step 3・T16 Step 2〜3、R-20→T15・T16
- 保留の経路（停止して待つ）: 前提の欠け（T1）・突き合わせの差分（T1・T13）・全録画の不具合と退行（T3・T4）・USS で直らない差（T12 (b)）・LX の番号の枠切れ（T12 Step 3）。どれも `done.md` 1 行目・bd・合図 1 回で指揮に届き、解消の条件は「持ち主の修正が `feature/uitk` に入る」（枠切れは「番号が足される」）。撮れない E の id（T9 Step 2）は停止せず `done.md`「要裁定」の Q-n で届く。入った後にこのレーンが該当分だけ流し直す経路を各タスクに書いた。最小構成: 不具合 0 件なら停止に入らない。全 id が P のときも E の一致の確かめ（T9 Step 1）は `4-block-chest` が N なので必ず走る
- 対照: 物差しは 2 回流して決定的かを見る（T10 Step 2）。EditMode は先に実行時と 2px 以内の一致を 1280×720 と 2560×1080 の両方で見る（T9 Step 1）。Web 側の失敗は S4-gamma の HEAD で流し直して既存と退行を分ける（T4 Step 3）。USS を直したら全 id を撮り直し、直す前と PSNR で比べて変わった id を並べ比べ直す（T12 Step 2・T15。D-34）。GameUi だけで落ちた assert は Web 側の同じ行と並べて不具合と前提の崩れを分ける（T3 Step 3。D-29）
- 撮影の条件: 撮影の前は毎回、前置きと各シナリオの冒頭で表示言語を日本語に固定する（Global Constraints 5・T8〜T10・T9 の EditMode。Web の参照は日本語。D-30）
- 撮り方の区分: P/N/E/V に X（[A-38] で UITK に作らない画面。撮らない）を足し、機械検査は X の根拠（A-38 と disposition の行）の欠けも誤りに数える（T7 Step 2。D-31）。言語ゲートは起動後に出し直せないので E（T9 Step 2）
- 他レーンとの境界: 5a は「## 全録画の結果」の `Web 専用` を grep して消す。この計画は `Web 専用` の文字列をシナリオ全体が Web 専用の行だけに使い（T5 Step 1 で grep して確かめる）、assert の書き直しは別の小節で申し送る（D-29）
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
- **D-15** 申し送りのバグ報告の記述欄の幅（356→354px）は Web と UITK が同じ見た目（どちらも右枠が切れる）なので、UITK だけを直さず Q-n に上げる（UITK だけ直すと Web と画素が変わり「Web と同じ見た目」から外れる。Web 側を直すのは master の Web の変更）

検査の反映（Phase 2.6・user-simulator・設計検査の注目点）:
- **D-16**（Phase 2.6 検査7・1行目 [強]: 設計書 §3.3「並べ比べ」の行と [A-179] は「codex-audit → 別の subagent が再現 → 指揮が判定 → 台帳の行と印は I-fix が置く」。計画はどちらを正にするか → X-prerun（integrator レーン）が判定の取りまとめと USS の修正・LX の記帳〈台帳の行と `/* look-exception */` の印を同じコミット〉を行う。判定は opus 2 体の独立評価、再現は別の subagent。USS で直せない差〈部品の作り直しが要るもの〉は持ち主の部品の範囲として bd を立てて指揮へ返し、停止して待つ。理由: W6 は撤去の直前で、段の締めの I-fix レーンはもう無く、直列のクリティカルパスに別レーンを挟むと撤去が止まる。A-179 が I-fix に印を置かせた理由〈`LookExceptionLedgerTest` が同じ HEAD で台帳と印を照合する〉は、このレーンが同じコミットで両方を入れるので満たす。A-250 の下書きにこの担い手の移りを書いた。出所 agent 既定〈指揮の補足〉）
- **D-17**（設計検査 注目点5: 「Codex を使わない」が実行計画 §3 の「integrator レーンは Codex セッションで回す」と逆向き → 根拠はユーザーの指揮への指示で、`../moorestech_logs/harness/uitk/conductor.md`「## ユーザー指示（2026-11-06 15:55）」の原文「一旦codexは使わず、claudeセッションで今からこの計画の遂行をして」と次の行「実行計画 §4.1 の Codex 指定をこの指示で上書き」、および「## 2026-11-11 21:30 Mac Editor を 1 本に」の「やはりunity2つは安定しないので1つに絞ってほしい」。これが `.decisions/2026-10-16-moores-writing-plansの実装引き継ぎはCodexセッションに固定する.md` と実行計画 §8 E-3〈integrator レーンも Codex セッションで回す〉を W6 について上書きする。出所 ユーザー指示〈指揮の記録〉を agent が引用）
- **D-18**（Phase 2.6 5-E・1行目 [弱]: 「全体スイッチと Web の型を参照しない」〈D-2〉を確かめるガードが無い。型で強制するかテストに載せるか → `prerun/*.cs` に `UiSystem|ActiveUiSystem|UiSystemEditorSwitch|WebUiHost|PlaytestDomQuery|Cef` が 0 件であることを Task 10 Step 2 と Task 14 Step 1 の grep で確かめ、R-15 の受け入れ基準に入れる。シナリオは asmdef の外で型の参照を禁じる仕組みが無いので grep にした。出所 agent 既定）
- **D-19**（シミュレーター 適用推奨1・設計検査 注目点7・シミュレーター Warning 3: ブロック 14 種を x=8〜216 に並べると 50×50 の足場の外に出て開けず、逃げ道が座標の誤りを「開けないブロック」と読み替える。`ScenarioTimeoutSeconds = 900f` は 1 回の Bash の 600 秒を超える。panel の矩形の `p.Note` を Task 8 の済んだコミットへ後から足す文になっていた → 足場の内側の (-6,32,-6) へ 1 個ずつ置いて 2 解像度を撮り `p.RemoveBlock` で消して次へ進む形に書き直し、逃げ道を「足場の上で開けなかったものだけ E」へ絞り、上限を 600 秒にし、`p.Note` を Task 8 Step 2 のコードに最初から入れた。出所 シミュレーター予測（agent 前提・拒否権つき）と設計検査）
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
- **D-32**（独立評価 1 周目: 完了条件は「720p・2560×1080 で並べ比べ」なのに動画 5 件は 1280×720 だけを比べていた。録画の出力は 1280×720 に固定〈`PlaytestRecorder.cs:33`〉 → 2560×1080 は同じ操作をもう 1 周し、Web の `durationMs` の 10%・50%・90% の時刻に UITK の静止画を撮り、Web の 2560×1080 の webm〈`videoCapture.ts:14` は `referenceViewport` で録る〉から同じ相を抜いて比べる。相は数フレームずれうるので、2560×1080 の動画の組では評価役に動きの位相を数えさせない。DSL に録画の解像度の口は足さない〈撮影の道具に口を足さない。やらないこと〉。出所 agent 既定）
- **D-33**（独立評価 1 周目: Web の動画は `visible(...)` で要素が出たのを確かめてから撮るのに、UITK 側は Note を打って待つだけで、mock-host の DragGuide の始点のアイテムが実ゲームの持ち物に無ければガイドが描かれないまま撮れる → 各動画で Web の `steps` と同じ要素〈`notification-row`・`key-control-hint`・`world-pin__arrow`・`tutorial-drag-guide`・`tutorial-highlight-label`〉が出たことを `p.Until` で確かめてから相を数え、DragGuide の始点は実ゲームの持ち物のレシピ一覧にあるアイテムにして始点のセルの存在も確かめる。出所 agent 既定）
- **D-34**（独立評価 1 周目〈評価役 2 体とも〉: 差を Web へ寄せる USS は部品の USS〈例 `Components/Controls/FormButton/FormButton.uss` は BugReportForm・ModalHost・ChallengePanel・EventLanguageGateBody が使う〉やトークンであることが多いのに、撮り直すのが直した id だけで、他の画面への波及が「差 0」のまま PR に入り、X-prerun が Web と比べられる最後の回なので後のレーンが見つけられない → 最初の修正の前に UITK の PNG を `before-uss/` へ写し、直すたびに全 id〈P/N/E/V〉を撮り直して PSNR で直す前と比べ、変わった id 全部の組を作り直して評価役 A・B と再現を回し直す。直す場所は画面の USS を先に選び、部品・トークンは画面で直せないときだけ。報告ページは最終 HEAD の組だけを載せる。Task 15 の自動適用が USS に触れたときも同じ手順。出所 agent 既定〈指揮の補足「USS を直したら全 id を撮り直して PSNR で変わった id を並べ比べ直す」〉）
- 軽の指摘の直し（判断を伴わない）: `moores-wt rm` の引数を worktree の名に（`uitk-prerun-ref`・`prerun-s4g-base`。`resolve_worktree` はパスか `WT_ROOT/<名>` しか引かない）・経路 M の一時 worktree の録画の前に `moorestech_web/node` のコピーと `pnpm install`・写し元の行の範囲（using 3-28、関数 83-114）・`GameUiTheme.tss` をファイルの持ち主・File Structure・Task 12 の Files と `git add` へ・renumber の grep の期待値（台帳の行と消すファイル自身の 2 件）・物差しのヘッダに出力名と置き場・状態名・前置き・比べ方・Task 9 Step 1 の [A-164] の一致を 2560×1080 でも

## 未確認

- ラダー 4〜6・`tools/uitk/` の 2 本・段 4〜5 の撮影シナリオ・S5-gate の UITK の言語ゲートとその L1 の土台（`Client.Tests/Ui/Screens/<ゲート>/Support/`）は起草時の HEAD に無い（S4-gamma・S5-gate・S5-ladder の後にできる）。パスと API は Task 1・Task 7 Step 1・Task 9 Step 2 で実物から写す。土台が無く外殻を組めなければ `5-event-language-gate` は Task 9 Step 2 の逃げ道（Q-n）に入る
- `docs/uitk/disposition.md` の再接続オーバーレイの理由の行は起草時に無い（S5-crosscut が書く。Task 7 Step 1 で確かめる）
- `prerun-videos.cs` の `dragFrom` に使うアイテム（実ゲームの持ち物のレシピ一覧に最初から並ぶもの）は Task 9 Step 3 で選ぶ
- 新しい参照の manifest の id の数（起草時 89）。master で増えていれば Task 7 で表を足す
- E の各 id の外殻の組み方は、前例のレーンの `done.md` の記述から写す（repo に撮影のコードが無い）。前例の無い id は Task 9 Step 2 で決める
- `prerun-shots-blocks.cs` の 14 種のうち、平らな地面で開けないもの（油井・宇宙エレベーター拠点・電柱の UI の有無）は Task 8 の run で分かる。置く向き North で大きなブロックが原点のどちら側へ伸びるかは確かめていない（(-6,32,-6) はどちらへ 13 マス伸びても足場の内側）
- 動画の録画の始まりと Timeline の秒のずれ（Task 9 Step 4 で測る）
- Windows の Editor で CEF が描けるか（Task 1 Step 5。winunity の Library の不調〈`winunity-play-fix.md`〉が再発したら経路 M）

## 本PR外のリファクタ提案

- 既存の段の撮影シナリオは出力名の付け方がばらばら（`uitk-<id>-<w>x<h>`・`<w>x<h>-<id>`・`<prefix>-uitk-<名>`・`4-wave-<状態>-<w>x<h>`）。撮影 id と解像度から名前を作る口が DSL に無いので、切替後に撮影を続けるなら `PlaytestScreenshot` に `<id>-<w>x<h>` の口を足す案（持ち主: DSL。X-cleanup 以後）。その口に「操作オーバーレイを消し、カーソルをパネルの外へ置いてから撮る」手順（既存の `uitk/stage3/uitk-stage3-compare.cs` の `ShootAsync`・`MoveCursorOffPanelsAsync` と、この計画の `prerun/` の 4 本の `Shoot`）も入れられる（Phase 2.6 の参考。表の対象外のテストコード）
- `UiRuntimePanelCapture` に「画面の外殻と偽の Model を組む見本」を画面ごとに持たせる口が無く、E の撮影は毎回レーンの `done.md` の記述から組み直している（前例 S3-buildmenu D-61・S4-alpha の依頼）。画面ごとの `IUiGalleryEntry` に Web の fixture 相当の見本を足す案（持ち主: 各画面・I-fix）

## 設計書・実行計画へ反映する行

### 設計書 §7.1（A-250・A-251）

```markdown
| A-250 | 撤去の前の全画面の並べ比べ（X-prerun）は、master の worktree で撮り直した参照 manifest の全 id に「既存の段の撮影シナリオ／`scenarios/uitk/prerun/` の新設シナリオ／EditMode の `UiRuntimePanelCapture`（実行時に作れない状態。[A-164] の一致を先に確かめる）／動画は録画して Web の webm と同じ相の 3 フレーム（2560×1080 は録画が 1280×720 固定なので同じ相の静止画）／UITK に作らない画面（[A-38]）は撮らず根拠の行を残す」のどれか 1 つを割り当てた対応表を作り、抜けと重複が 0 であることを機械で確かめてから撮る。撮影は毎回表示言語を日本語に固定する（参照が日本語）。判定は左 Web・右 UITK の合成画像を opus の評価役 2 系統に束ごとに別々に渡し、別の subagent が画素で再現した差だけを「USS で直す／持ち主へ回す／LX／Q-n」に閉じる。USS を直したら全 id を撮り直し、直す前と PSNR で比べて変わった id を並べ比べ直す（共有の部品・トークンの波及を見落とさない）。判定の取りまとめ・USS の修正・LX の記帳（台帳の行と印を同じコミット）は X-prerun が行い、USS で直らない差だけを持ち主へ bd で回して指揮へ返す。§3.3「並べ比べ」の行と [A-179] の「指揮が判定し、台帳の行と印は I-fix が置く」を X-prerun について改める（段の締めの I-fix レーンが W6 には無く、撤去の直列の道に別レーンを挟まない）。撤去レーンの見た目の不変は X-prerun が作る `prerun-smoke-shots.cs`（束ごとに 2 状態・全体スイッチと Web の型を参照しない）で測る（X-prerun D-2・D-3・D-11・D-16・D-30〜D-34） | 撮影表の 84 件だけを PlayMode で撮る: 実行時に作れない状態が比べられない。評価役 1 体に全部を渡す: コンテキストに収まらず後半を見ない。Codex の codex-audit: ユーザーの指示（2026-11-06「一旦codexは使わず」）で使わない。[A-179] のとおり指揮が判定し I-fix が印を置く: W6 に I-fix レーンが無く、撤去の直列の道が止まる。[A-179] のとおり実ゲームで Web と UITK の両方を全画面撮る: 段の締め（S2〜S5-ladder）が同時表示の重なりを実ゲームの組で見終えており、残るのは全 id を覆う比べで、Web 側を全画面撮るには CEF の Editor がもう 1 本要る（Mac の Editor は 1 本） |
| A-251 | 撤去の前の最後の Web の基準は、全録画の Web 側の結果（`harness/uitk/X-prerun/done.md`「## 全録画の結果」と台帳「## 実測の結論」の行）・参照 `web-<sha>`／`web-<sha>-2560x1080`（台帳「## 参照キャプチャ」）・全束の突き合わせの記録（`disposition.md` の束 1〜5 の行）の 3 つとし、切替後に Web の挙動・見た目・テストを確かめるときはここだけを引く。Web 側の失敗の分類は [A-177] の 6 分類に `既存`・`退行`・`比較不能（新設）`（前の全録画に無いシナリオ）・`比較不能（変更）`（前の全録画の HEAD から中身が変わったシナリオ）と、UITK 側の「シナリオの誤り（5a）」（古い testid・Web の型の参照）を足して使う（X-prerun D-6・D-27） | 撤去後に Web を復元して比べる: 裁定（切替後に Web へ戻すデバッグ切替を残さない）と合わない。基準を残さない: 切替後の不具合で Web の挙動を辿れない |
```

### 実行計画 §6.1 の割り当て行

```markdown
| X-prerun（`P/2026-11-12-uitk-x-prerun.md`。PLAN-W6） | A-250・A-251 | LX-210〜217（撤去の前の全画面の並べ比べで再現した差のうち例外にするもの） |
```

### 実行計画 §1.5 の X-prerun 行の改め案

- 「codex-audit（画像付き）にかけて」を「opus の評価役 2 系統の独立評価（画像付き。再現した差だけを処遇。Codex は使わない）にかけて」へ
- 完了条件の末尾に「全束の突き合わせ差分 0 を撤去前の最後の記録として `disposition.md` に残す（A-251）。撤去レーンの見た目の物差し `scenarios/uitk/prerun/prerun-smoke-shots.cs` を作る（W6-6 (b)）」を足す
