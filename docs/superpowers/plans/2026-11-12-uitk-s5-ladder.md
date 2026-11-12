# UITK S5-ladder（段階 5 の締め: ラダー 6 `uitk-challenge-gate`・6 本緑・Web ロケータのログ・アンカーの未移植の表の削除・束 5 の暗転と G1 全束 0・全束の突き合わせ差分 0・台帳の全行の照合・段階 5 の並べ比べ）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 段階 5 の 3 レーン（S5-challenge・S5-gate・S5-crosscut）と S2-ladder・S3-ladder・S4-gamma が `feature/uitk` に入った後、全体スイッチ UITK（GameUi）・Web ホスト無しで「出展モードの言語ゲートを日本語で通る → T でチャレンジ一覧を開いて閉じる」をラダー 6 `uitk-challenge-gate` として固定し、ラダー 6 本（`uitk-lifeline`・`uitk-opening-hud`・`uitk-build-bp`・`uitk-production-loop`・`uitk-research-train`・`uitk-challenge-gate`）を最終 HEAD で緑にする。あわせて、UITK 側で DSL の Web ロケータが一度も使われないことをログで示し、`TutorialAnchorCoveragePending.cs` を消して全アンカーが UITK で印付きであることをテストで固定し、束 5 を暗転にして G1 の未記帳を全束 0、`test-name-parity.mjs --bundle all` の差分を 0（束 1 の写す予定 16 件と束 5 の未対応の残り）にし、台帳の全行の UITK 側の実体を照合し、段階 5 の 2 画面を同じ実ゲームの状態で両系に撮って独立評価にかけ、`feature/uitk` へ取り込む。

**Architecture:** integrator のレーン（`int`。Mac の Editor 1 本）。新しく書くのは「検査する側」だけ（ラダー 1 本・並べ比べの撮影シナリオ 1 本・出展モードの運転役の汎用化・NUnit の写し・処遇の行・台帳の記帳）。本番コード（`Client.Ui`・`Client.Ui.State`・`Client.Starter`・`Client.Playtest` の本体）は書かない。出展モードの起動は S5-gate の起動口 `PlaytestEventLanguageGateBoot`（`Client.Playtest/Boot/`）をそのまま使い、S5-gate の運転役 `uitk-event-language-gate.sh` の中身を、系とシナリオを引数に取る `exhibition-mode-run.sh` へ移して両方から呼ぶ（起動口のファイルは足さない。S5-gate の申し送り）。言語ゲートは DI より前の 1 回きりで、無操作は `EventIdleQuitWatcher` のアプリ終了でゲートへ戻らないので、ラダー 6 は plan §4.4 の語順と逆に「ゲート → 一覧」の順に通る（D-2）。Web ロケータのログは `SwitchedUiElementLocator` の 1 行（`[PlaytestUi] locator reads only the GameUi UI`）と、`WebUiElementLocator` だけが出す `[PlaytestUi] Web …` の行が 0 であることで示す（D-3）。並べ比べは設計書 [A-179] の形で、締めのレーンが同じ実ゲームの状態を両系で撮り、opus の subagent 2 体の独立評価 → 別の subagent の再現 → 指揮の判定に回す（USS は直さず、例外の LX 文面だけ書く。D-9）。

**Tech Stack:** Unity 6 / UI Toolkit（`Client.Ui`・`Client.Ui.State`）、NUnit（`ScreenTestHost` の panel simulate）、プレイテスト DSL（`Client.Playtest`・`.agents/skills/unity-playmode-recorded-playtest`）、bash（出展モードの運転役）、Node（`tools/uitk/test-name-parity.mjs`・`moorestech_web/webui/e2e/uitk-reference/capture.ts`）、Python（`.github/scripts/check-uitk-web-follow.py`）、`~/bin/winunity`（Windows の compile・test-all・play）、bd。

設計の出所: 実行計画 `docs/uitk-migration/execution/execution-plan.md` §1.4 の S5-ladder 行（完了条件の原文は Requirements の上に写す）・§1.5 の X-prerun 行（範囲を重ねない）・§4.3・§5・§6.2、統合計画 `docs/uitk-migration/plan.md` §2.0（共通条件）・§2.6（最後の束の完了条件）・§4.4（ラダー表）、設計書 `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4・[A-179]、台帳 `docs/uitk/TODO.md`、`docs/uitk/disposition.md`、`.agents/skills/ref-uitk-design/references/compare-procedure.md`。前例の計画: `docs/superpowers/plans/2026-11-12-uitk-s3-ladder.md`（締めのレーンの形）・`2026-11-10-uitk-s4-gamma.md`（段 4 の締め）・`origin/uitk-lane/s2-ladder` の `2026-11-12-uitk-s2-ladder.md`（winunity の口・[A-179] の並べ比べ）。依存レーンの計画: `2026-11-12-uitk-s5-challenge.md`・`-s5-gate.md`・`-s5-crosscut.md`。

実行計画 §1.4 の行（原文）:

`| S5-ladder | 段階 5 の締め | 5・W5 末 | S5-* | 要 | int | ラダー `uitk-challenge-gate` を作り 6 本緑。UITK 側で DSL の Web ロケータが一度も要素を返さない（ログ）。`Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`（未移植のアンカーの表。S2-tutorial が作る）が空。台帳の全行が UITK で作られ、G1 全束 0。全束の突き合わせ差分 0 |`

## Requirements

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | ラダー 6 `uitk-challenge-gate` | `scenarios/uitk/stage5/uitk-challenge-gate.cs` を `exhibition-mode-run.sh ./moorestech_client GameUi <それ>` で流し、出展モードで起動 → DI より前の言語ゲート（初期化は未完了）→ `event-language-gate-option-japanese` を押す → その場で外れる → 初期化完了・言語 `japanese` → ゲーム画面 → `WebUiHost.Hub == null` → T で `UIStateEnum.ChallengeList`・`challenge-panel`・解放済みの先頭カテゴリの `challenge-category-<guid>`・進行中の `challenge-node-<guid>` が出る → T で GameScreen、を `Success: true`。初期化の完了の後の Error/Exception のログ 0 件と（起動からの）`[PlaytestUi] Web` のログ 0 件をシナリオ自身が主張し、選ぶ前の言語へ戻せたこと（`TrySetChosenLanguage` が真）を主張してから控えを消す。Note に `系=GameUi` |
| R-2 | 出展モードの運転役の汎用化 | `scenarios/uitk/stage5/exhibition-mode-run.sh <project> <GameUi\|Web> <scenario.cs>` が系の切り替え・武装・`run-scenario.sh`・後始末（Disarm と言語の復元）を行い、引数が 3 つでない・系が 2 値でないときは何もせず終了コード 2。武装の EDC の戻り値が語 `armed` でなければ（PlayMode が残っている等）流さずに終了コード 1。言語の復元は EDC の戻り値の語 `language-restored` のときだけ控えを消し、外れたら `ERROR: … kept` を出して控えを残す（S5-gate の運転役と同じ判定）。S5-gate の `uitk-event-language-gate.sh <project>` は呼び方を変えずにこれへ委ね、S5-gate の録画 `uitk-event-language-gate` が `Success: true` のまま |
| R-3 | ラダー 6 本が UITK で緑（plan §2.0 の 1・§2.6） | 最終 HEAD（Task 12 の取り込み後）の GameUi で 6 本が全部 `Success: true`・ErrorLogs 0。取り込みで `moorestech_client/Assets`・`.agents/skills/unity-playmode-recorded-playtest/` に差分が入ったら 6 本を流し直す。入らなければ流した HEAD の sha と「取り込みの差分にこの 2 か所が無い」（`git diff --stat <流した sha> HEAD -- moorestech_client/Assets .agents/skills/unity-playmode-recorded-playtest` が空）を `done.md` に書く（ラダー 6 の初期化の完了までの NRE は 2026-08-28 裁定で許容し、シナリオは初期化の完了の後だけ数える。S5-gate と同じ扱い）。ラダー 1 の「セーブして終了」の結果は呼び出し側のシェルで `save.json` の更新時刻を見る（`uitk-lifeline.cs` 冒頭の注記） |
| R-4 | UITK 側で DSL の Web ロケータが一度も要素を返さない（ログ） | R-3 の 6 本と R-10 の段階 5 の録画の GameUi の回のそれぞれで、`uloop get-logs --search-text "[PlaytestUi]"` に `[PlaytestUi] locator reads only the GameUi UI` が 1 行以上・`[PlaytestUi] Web` で始まる行が 0。L0 `SwitchedUiElementLocatorTest` が PASS（選んだ系だけを読む構造の固定）。証拠を `done.md` の表に 1 本 1 行 |
| R-5 | `TutorialAnchorCoveragePending.cs` が空 | S5-challenge の後で `Owners` が 0 件であることを確かめてからファイルを消し、`TutorialAnchorCoverageTest` の 1 本目を「全てのアンカーIDはUITKで印が付いている」（未移植の表の分岐を外す）に書き換えて PASS |
| R-6 | 束 5 の暗転と G1 全束 0 | 台帳「## 束」の束 5 が `暗転`・起点 10 桁。`python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD` の出力で束 1〜5 の全部が「未記帳 0 件」・違反なし。取り込み後も同じ。PR の `UITK Web follow gate (G1)` が pass |
| R-7 | 台帳の全行が UITK で作られている | 台帳「## 行」の全行（起草時 49 行）について、UITK 側の実体（`Client.Ui`・`Client.Ui.State`・`Client.Playtest` のディレクトリかファイル）か「作らない（理由: 設計書 A-n・disposition の行）」を `done.md`「台帳の照合」の表に 1 行 1 行で書き、空欄 0。実体のパスは `git ls-files` で実在を確かめる |
| R-8 | 全束の突き合わせ差分 0 | `node tools/uitk/test-name-parity.mjs --bundle all` の終了コード 0。束 1〜5 の全部で「写す予定 0 / 未対応 0」。`docs/uitk/disposition.md`「## 突き合わせの記録」に束ごとの 5 行 |
| R-9 | 束 1 の写す予定 16 件を NUnit へ写す（bd moorestech-albof.21） | Task 3 の NUnit 16 本が PASS し、各テストの直前に元の Web テスト 1 件につき 1 行の `/// <web-test>`。disposition の束 1 の `写す予定` 16 行を消す |
| R-10 | 段階 5 に触る録画（plan §2.0 の 2） | GameUi で `stage5/uitk-challenge-list.cs`・`stage5/uitk-challenge-hud-resident.cs`（S5-challenge）・`stage5/uitk-event-language-gate.sh`（S5-gate）・`misc/walk-while-menus-open.cs`・`misc/ui-state-transition-matrix.cs`・`uitk-tutorial-cues.cs`（段階 5 のチャレンジ一覧と枠に触る既存）が `Success: true`。Web 側は Task 10 Step 4 の列挙で出た Web の録画を `--ui-system Web` で流し、段階 5 の前の合否と比べて退行 0（CEF が出なければ RendererUnavailable を証拠に「未確認」） |
| R-11 | 段階 5 の並べ比べと独立評価（[A-179]） | 撮影シナリオ `stage5/uitk-stage5-compare.cs` を GameUi と Web の両系で流し、`5-event-language-gate`・`5-challenge-list` を 1280×720 と 2560×1080 で撮る。参照（`web-8a585f742a/`・`-2560x1080/` か Task 8 の撮り直し）・組 0（S5-challenge・S5-gate の報告ページ）・組 2（両系）を opus の subagent 2 体が別々に判定し、別の subagent が画素で再現した差だけを `done.md`「差の表」と「要裁定」（指揮の判定待ち）に残す。例外にする差の文面は LX-207〜LX-209 の番号で書く（台帳の行とコードの印は置かない） |
| R-12 | 参照キャプチャの鮮度 | `git log 8a585f742a..origin/master -- <段階 5 の 2 画面の Web パス>` が 0 件なら撮り直さず、台帳「## 参照キャプチャ」に「束 5 の最新の参照（S5-ladder）」の 1 行。1 件以上なら master の worktree で撮り直し `web-<sha>/`・`-2560x1080/` を置く |
| R-13 | Client.Tests 全部 | 最終 HEAD で `winunity test-all <branch>` の落ちた名前が、土台 `origin/feature/uitk` の同じ経路の落ちと一致（このレーン由来の落ち 0）。Mac で `^Client\.Tests\.Ui\.` が全 PASS |
| R-14 | 申し送りと報告 | 「## 申し送りの処遇」の全行を処理し、`../moorestech_logs/harness/uitk/S5-ladder/done.md` に完了条件ごとの証拠・PR URL・未完了・要裁定・integrator への依頼（X-prerun への引き継ぎを含む） |
| R-15 | レビューとマージ | moores-code-review の全ブランチレビュー → PR（base=`feature/uitk`）→ 取り込み → G1 pass → `gh pr merge --merge` |

### やらないこと
- 全録画の Web/UITK 両系の通しと全画面の並べ比べ（X-prerun の持ち物。実行計画 §1.5）。このレーンが流すのはラダー 6 本・段階 5 に触る録画・段階 5 の 2 画面の並べ比べだけ
- 段階 5 の画面・部品・Foundation・State の本番コードの変更（並べ比べで再現した差は指揮の判定へ回し、直すのは I-fix。設計書 [A-179]）。台帳「## 見た目の例外」の行とコードの `/* look-exception */` の印（I-fix が置く）
- 段階 5 の L3（`EditModeInPlayingTest/Ui/Stage5/`）。完了条件に無く、ラダー 6 が実起動で、S5-crosscut の `EditModeInPlayingTest/Ui/Crosscut/GameUiScreenMountTest` が一覧の載り先を持つ（D-6）
- 束の `状態` を `切替済み`・行の `所有者` を `UITK` にする（切替 PR の持ち物。台帳「読み方と運用」）
- Web 側の削除（X-5）。Web のテストを消さない
- 設計書・実行計画の本文の改訂（`done.md`「integrator への依頼」に案を書く。台帳・disposition の自分の行は書く）
- Codex（指揮の指示。§5 の codex-audit は opus の subagent 2 体で代える）

## Global Constraints

- AGENTS.md 全部（1 ファイル 200 行未満・1 ディレクトリ 10 ファイル・partial と `Func<>` の禁止・try-catch は外部境界だけ・イベントは UniRx・2 行コメント〈日本語 1 行→英語 1 行〉・`#region Internal` はメソッド内のローカル関数だけ・デフォルト引数禁止・.meta を手で作らない・Prefab/シーンを手で編集しない・失敗の分岐は理由をログへ）
- Mac mini で同時に動かす Unity Editor は全体で 1 本（このレーンの worktree の 1 本。起動・停止の前に `moores-wt status` で持ち主を確かめる）。subagent は Editor を起動しない。並列が要るとき（test-all・Web 側の録画）は Windows の `~/bin/winunity`（push 済みブランチ。結果は `~/.cache/winunity/runs/<runId>/`）
- 出展モードの録画（ラダー 6・S5-gate の録画・並べ比べ）は Mac の Editor で運転役から流す（`winunity play` は運転役の武装を挟めない。D-8）
- 録画シナリオは PlayMode を止めた状態で、運転役か `uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'` の後に `run-scenario.sh` を呼ぶ。`run-scenario.sh` が初回 "already playing" で終わったら 1 回だけ投入し直す（bd moorestech-trfka）。シナリオは `IgnoreFocus`・`EnableDevice` を入れる（前例どおり）
- ラダー 1 は PlayMode の前に Editor のプロセスへ `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` を立てる（`uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE", "http://127.0.0.1:9"); return System.Environment.GetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE");'`。前例 F1。立っていないとシナリオが送信前に throw する）
- 録画 mp4・`PlaytestResults/`・TestResults xml・撮影 PNG は検証後に消す（報告ページへ載せる分は先に載せる）。テストで作ったワールドは `list-work-saves.sh` で探して消す。`.superpowers/` はコミットしない
- 番号: 設計書の行は A-249、見た目の例外は LX-207〜LX-209 だけ
- 共有ファイル（台帳「## 追従記録」「## 参照キャプチャ」の末尾・`disposition.md` の「## 処遇」「## 突き合わせの記録」の末尾・`TutorialAnchorCoverageTest.cs`）は自分の行だけ足す。取り込みの衝突は両方の行を採る
- 区切り（タスク）ごとにコミットする。subagent はバックグラウンドのポーリングで待たず前景で実行する
- 依存レーンのファイル（`stage5/` の S5-challenge・S5-gate のシナリオ、S5-gate の起動口、各段のラダー）は読むだけ。例外は `uitk-event-language-gate.sh` の中身の移動（Task 5・D-4）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

## 死活表（Phase 2.5）

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| S5-gate の録画 `uitk-event-language-gate.sh <project>` | 生きる | 呼び方を変えず `exhibition-mode-run.sh` へ委ねる。Task 5 Step 3 で流し直して `Success: true` を見る |
| 出展モードの武装の後始末（Disarm・言語の復元） | 生きる | 中身を移すだけ（trap の処理は S5-gate の検査後の形と同じく、復元の成否を EDC の戻り値の語で判定し、戻せたときだけ控えを消す）。ラダー 6 と並べ比べも同じ後始末を通り、シナリオ側も `TrySetChosenLanguage` の真偽を主張してから控えを消す。Task 5 Step 3 で中断時の復元も 1 回確かめる |
| 武装の失敗（PlayMode が残っている） | 生きる（強まる） | `ArmForNextBoot` は PlayMode 中に `ERROR: already playing` を返し EDC の終了コードは 0。運転役は戻り値の語 `armed` を見て、外れたら流さず終了コード 1（武装の無い通常の起動で流れ、ゲート待ちの時間切れが依存レーンの不具合に見える誤りを防ぐ）。Task 5 Step 3 の対照で見る |
| Editor の全体スイッチ（`UiSystemEditorSwitch`。SessionState）の運転役の後の値 | 生きる（流した系のまま残る） | 運転役は後始末で全体スイッチを戻さない（`Web` で流すと、同じ Editor の次の Play も Web で起動する）。S5-gate の運転役は GameUi 固定だったので、この状態は汎用化で新しく生じる。このレーンの録画は毎回 `Set(...)` してから流し（Global Constraints・Task 10）、X-prerun への引き継ぎにも「運転役の後は流した系のまま。次の録画は必ず Set してから」と書く |
| 置き換える既存 NUnit `PauseMenuViewTest.設定画面は言語選択と戻るボタンを出し戻るはtopへのshow_pageを送る` | 生きる | 置き換え後のメソッドは既存の assert（`PauseMenuViewTest.cs:112-128`）を全部含み、走りと画質の節の assert を足すだけ。I-fix5 の後は題の読みを `.PlainText()` にした形（`origin/uitk-lane/i-fix5` の同じメソッド）を土台にする |
| 置き換える既存 NUnit `BugReportFormViewTest.応答待ちが終わってもホスト送信中は種別と本文を保護する` | 生きる（推測。Task 3 Step 1 で確かめる） | 旧テストは「ローカルの送信なしでホストが Submitting」でロックと文言を見ていた。新テストは必ずローカルの送信を通るが、拒否の応答の後は `_sending=false` になりロックの出どころは同じ `capture == Submitting`（`BugReportFormView.cs:154-171`）。Task 3 Step 1 で、旧テストの assert（種別 2 つ・本文・送信の無効と状態行の文言）が新テストに全部含まれることを見る |
| I-fix5 の段ごとの移動の後に古いパスに残った録画 | 回す | Task 0 Step 2 で 2 本出たら新しい段のディレクトリの方を流し、古い方は `done.md`「未完了」に I-fix5 の消し漏れとして書く（持ち物は I-fix5。このレーンは消さない） |
| チュートリアルのアンカーの網羅の検査 | 生きる（強まる） | 未移植の表を消し「全アンカーが UITK で印」を直に主張する。新しいアンカーを足して印を付け忘れると落ちる（今までは表に載せて逃げられた） |
| G1（`check-uitk-web-follow.py`） | 生きる | 束 5 の状態の列だけを `暗転` にし、表の形は変えない。束 5 の Web パスを触る以後の master の取り込みは記帳が要る（X-prerun・取り込みの PR の運用。台帳「読み方と運用」） |
| `test-name-parity.mjs` | 生きる | 処遇の行と `/// <web-test>` を足すだけ。自己テスト `node --test tools/uitk/testNameParity/tests/*.test.mjs` を Task 4 で流す |
| Web 側の録画・Web の画面 | 生きる | Web のコードとテストは触らない。R-10 で Web 側の退行 0 を見る |
| 段階 5 の画面（チャレンジ一覧・言語ゲート） | 生きる | 本番コードを触らない |

## 申し送りの処遇

`grep -rn "S5-ladder\|段階 5 の締め" ../moorestech_logs/harness/uitk/*/done.md ../moorestech_logs/harness/uitk/conductor.md` は起草時 0 件。依存レーンの計画とレーンの記録からこのレーン宛てのものを拾った。Task 0 Step 4 で依存レーンの `done.md` を同じ grep で見直し、増えた行をここへ足す。

| 出所 | 項目 | 処遇 |
|---|---|---|
| S5-challenge 計画 D-17・Task（未移植の表） | `TutorialAnchorCoveragePending.cs` の削除と空の確認 | やる（Task 2） |
| S5-challenge 計画 やらないこと | ラダー `uitk-challenge-gate`・束 5 の `状態` の切り替え | やる（Task 6・Task 1） |
| S5-challenge 計画 やらないこと | L3 は段の締めの持ち物 | やらない（D-6。S5-crosscut の `GameUiScreenMountTest` とラダー 6 が持つ。理由を `done.md` に書き、S5-crosscut D-14 の「無い段」の報告への答えにする） |
| S5-gate 計画 所有の表 | ラダー 6 で出展モードを使うなら起動口を流用しファイルを足さない | やる（Task 5。起動口は読むだけ、運転役の中身を `exhibition-mode-run.sh` へ移す。D-4） |
| S5-crosscut 計画 やらないこと（52 行） | 束 5 の暗転・突き合わせの締め・並べ比べの締め | やる（Task 1・Task 4・Task 9） |
| S5-crosscut 計画 Task（並べ比べ Step 3） | S5-crosscut 以前からの差は S5-ladder の締めへ回す | やる（Task 9 の評価に S5-crosscut の `done.md`「差の表」の回し分を入れる。段階 5 の 2 画面以外の差は X-prerun へ回す） |
| S5-crosscut 計画 D-14 | 段ごとの L3 の数え上げ（S5-ladder の段 5） | 回す（D-6 の結論を `done.md`「integrator への依頼」に書く） |
| S3-ladder 判断記録 D-3 | ラダー 2 の欠けは後段で必ず検出する | やる（Task 0 Step 2 でラダー 6 本のパスの実在を見る。欠ければ「状態: 停止（理由）」） |
| S3-ladder・S2-ladder の依頼 | S4-gamma 計画のラダー 2・3 のパスの読み替え（`stage2/`・`stage3/`） | やる（Task 0 Step 2 で実物のパスを引き、このレーンはその実物を流す。I-fix5 の 10n で `scenarios/uitk/` 直下が段ごとに移っていればその後のパス） |
| bd moorestech-albof.21 | 束 1 の写す予定（16 件）を NUnit へ | やる（Task 3。閉じるのは Task 12） |
| S2-ladder done（winunity の口） | `winunity play --ui-system`・`edc`・`test-all` | やる（Task 10 で使う。口が無ければ `done.md`「未完了」に書き、Mac の Editor で順に流す） |
| S3-ladder 依頼 | Web の録画には `moorestech_web/node` の複製と `pnpm install` が要る | やる（Task 9・Task 10 の Web の回の前提に書く） |
| 実行計画 §1.5 X-prerun | 全録画の両系の通し・全画面の並べ比べ・wwxy の残り | 回す（`done.md`「integrator への依頼」に、出展モードの 3 本〈ラダー 6・S5-gate の録画・並べ比べ〉は `exhibition-mode-run.sh` 経由でしか流せないことを書く） |

## File Structure

`SK` = `.agents/skills/unity-playmode-recorded-playtest`、`CT` = `moorestech_client/Assets/Scripts/Client.Tests`。

| ファイル | 種類 | 責務 |
|---|---|---|
| `SK/scenarios/uitk/stage5/exhibition-mode-run.sh` | Create | R-2。出展モードの運転役（系とシナリオを引数に取る） |
| `SK/scenarios/uitk/stage5/uitk-event-language-gate.sh` | Modify | R-2。中身を `exhibition-mode-run.sh` へ委ねる 1 行に（呼び方は不変。持ち主 S5-gate は先にマージ済み） |
| `SK/scenarios/uitk/stage5/uitk-challenge-gate.cs` | Create | R-1。ラダー 6 |
| `SK/scenarios/uitk/stage5/uitk-stage5-compare.cs` | Create | R-11。並べ比べの組 2（両系。録画しない） |
| `CT/Ui/Anchor/TutorialAnchorCoveragePending.cs` | Delete | R-5 |
| `CT/Ui/Anchor/TutorialAnchorCoverageTest.cs` | Modify | R-5。1 本目の書き換え |
| Task 3 の NUnit（`CT/Ui/Screens/PauseMenu/`・`Settings/`・`BugReport/` の既存クラスへの追記と新しいクラス） | Create/Modify | R-9。置き場は Task 3 の表 |
| 束 5 の写しの印（既存の NUnit へ `/// <web-test>` の行を足すだけ） | Modify | R-8。対象は Task 4 の表 |
| `docs/uitk/TODO.md` | Modify | 束 5 の行（暗転・起点）・「## 追従記録」の束 5 の行（と束 1〜4 の残り）・「## 参照キャプチャ」の 1 行 |
| `docs/uitk/disposition.md` | Modify | 「## 処遇」の束 1 の写す予定 16 行の削除と束 5 の行・「## 突き合わせの記録」の 5 行 |
| `../moorestech_logs/harness/uitk/S5-ladder/done.md` | Create（repo 外） | R-14 |
| `../moorestech_logs/uitk/reference-captures/web-<sha>/`・`web-<sha>-2560x1080/` | Create（repo 外。R-12 で撮り直すときだけ） | R-12 |

`stage5/` は起草時の見込みで S5-challenge 2 本・S5-gate 2 本・S5-crosscut 0 本＋このレーン 3 本の 7 本（10 以下）。`CT/Ui/Anchor/` は 5→4 本。

## Tasks

### Task 0: 前提の確認・Editor の起動・記録の準備

**Files:** なし（`done.md` の骨組みだけ）

- [ ] **Step 1: 場所と依存のマージ**

```bash
pwd
git fetch origin
for lane in s5-challenge s5-gate s5-crosscut s2-ladder s3-ladder s4-gamma i-fix5 i-fix6; do echo "$lane $(git log origin/feature/uitk --merges --oneline --grep "uitk-lane/$lane" | head -1)"; done
```
Expected: 8 行すべてにマージコミットが出る。S5-challenge・S5-gate・S5-crosscut・S2-ladder・S4-gamma のどれかが無ければ、無いレーンを `done.md`「状態: 停止（<レーン> 未マージ）」に書いて `cmux wait-for -S uitk-s5-ladder` を送り止める（ラダー 2・4・6 の前提と段階 5 の画面が無い）。I-fix5・I-fix6 が無いだけなら続け、次の読み替えをして `done.md`「証拠」に 1 行ずつ書く（どちらも並走で、このレーンより後に入るなら、入った後の取り込みで下の形へ戻す）:
- I-fix5 が未マージ: Task 0 Step 2 のパスは実物に合わせる（段ごとの移動の前）。`Client.Tests/Ui/TestHost/LabelPlainText.cs` と `LocalizedLabel.SourceText` が無いので、Task 3 の `.PlainText()` を `.text` に、`using Client.Tests.Ui.TestHost;` を消し、Task 6 のローカル関数 `PlainText` を `label.text` から印を外すだけにする（I-fix5 の前は行送りの印も付かない）。`PauseMenu/` は 6→8 本（I-fix5 の後は `PauseMenuSubPageFlexTest.cs` が入って 7→9 本）
- I-fix6 が未マージ: Task 3 の 8〜12 の前提の file:line（`BugReportFormView.cs:90`・`BugReportRecoveryControlsView.cs:49-51`）は素の Label の形のまま。テストはどちらでも同じに書ける（言語を切り替えてから `Apply`、読みは `.PlainText()`）。I-fix6 が後から入って `BugReportRecoveryControlsViewTest.cs` が衝突したら両方の行を採るレーンのブランチを `origin/feature/uitk` から切り直していなければ `git merge origin/feature/uitk`

- [ ] **Step 2: ラダー 6 本と段階 5 の録画のパス**

```bash
for n in uitk-lifeline uitk-opening-hud uitk-build-bp uitk-production-loop uitk-research-train uitk-challenge-list uitk-challenge-hud-resident uitk-event-language-gate uitk-tutorial-cues walk-while-menus-open ui-state-transition-matrix; do echo "$n: $(git ls-files ".agents/skills/unity-playmode-recorded-playtest/scenarios/*/$n.cs" ".agents/skills/unity-playmode-recorded-playtest/scenarios/*/*/$n.cs" | tr '\n' ' ')"; done
git ls-files ".agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/"
```
Expected: 11 行すべてに 1 本ずつパスが出る（2 本出たら古い方の削除漏れを `done.md`「未完了」に書き、新しい段のディレクトリの方を使う）。`stage5/` に `uitk-event-language-gate.sh` がある。出たパスを `done.md`「証拠」の表に写し、以後の Task はこのパスで流す（下の Task に書いたパスは起草時の見込み）。ラダーが欠けたら Step 1 と同じく停止

- [ ] **Step 3: 依存の API の実物照合**

```bash
grep -n "public static string ArmForNextBoot\|public static string Disarm" moorestech_client/Assets/Scripts/Client.Playtest/Boot/PlaytestEventLanguageGateBoot.cs
grep -rn "class GameInitializedEvent" moorestech_client/Assets/Scripts | head -2
grep -rn "class WebUiHost\b\|public static .* Hub" moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/*.cs | head -3
grep -n "public .*Categories" -r moorestech_client/Assets/Scripts/Client.Ui.State/Challenge | head -3
grep -n "IsUnlocked\|CurrentChallenges" moorestech_client/Assets/Scripts/Client.Network/API/Responses.cs | head -4
git grep -n "public void Dispose" -- moorestech_client/Assets/Scripts/Client.Ui/Screens/PauseMenu/PauseMenuScreenPresenter.cs
ls moorestech_client/Assets/Scripts/Client.Tests/Ui/TestHost/LabelPlainText.cs; grep -n "SourceText" moorestech_client/Assets/Scripts/Client.Ui/Foundation/Text/LocalizedLabel.cs | head -2
git grep -n "ChallengeListView" -- 'moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Crosscut/GameUiScreenMountTest.cs'
grep -rn "\"challenge-panel\"\|challenge-category-\|challenge-node-" moorestech_client/Assets/Scripts/Client.Ui/Screens/Challenge | head -5
grep -rn "event-language-gate-option-" moorestech_client/Assets/Scripts/Client.Ui | head -2
grep -c "{ AnchorIds" moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
```
Expected: 起動口の 2 つの public static、`GameInitializedEvent` の名前空間（Task 6 の `using Client.Game.Common;` と違えば直す）、`WebUiHost.Hub`（`Client.WebUiHost.Boot`）、`ChallengeProgressState.Categories`（`Client.Ui.State/Challenge`）と、その要素 `ChallengeCategoryResponse` の `IsUnlocked`・`CurrentChallenges`（`Client.Network/API/Responses.cs:71-77`。Task 6 のシナリオが使う。名前が違えば S5-challenge の `uitk-challenge-list.cs` の書き方へ合わせる）、`PauseMenuScreenPresenter` の `Dispose` が 0 件（S5-crosscut T13 の後。残っていれば Task 3 のテスト 2・3 の TearDown で呼ぶ）、`LabelPlainText.cs` と `SourceText` の有無（I-fix5。無ければ Step 1 の読み替え）、`GameUiScreenMountTest` に `ChallengeListView` が載っているか（無ければ D-6 の「一覧の載り先は GameUiScreenMountTest」が成り立たないので、`done.md`「integrator への依頼」に「S5-crosscut の L3 の型の一覧に `ChallengeListView` を足す」を書き、D-6 の理由を「一覧の載りはラダー 6 の実起動が見る」に読み替えて `done.md` に書く）、testid 3 種、ゲートのボタンの名前、未移植の表の行 0（1 以上なら S5-challenge の消し忘れ。消すのはこのレーン Task 2 で、残っていた行と持ち主を `done.md` に書く）

- [ ] **Step 4: 申し送りの見直し**

```bash
grep -rn "S5-ladder\|段階 5 の締め" ../moorestech_logs/harness/uitk/*/done.md ../moorestech_logs/harness/uitk/conductor.md
```
Expected: 出た行を「## 申し送りの処遇」に足し（処遇は やる／回す／やらない と理由）、計画をコミットする

- [ ] **Step 5: bd と Editor と記録**

```bash
bd create "UITK S5-ladder: 段階5の締め（ラダー uitk-challenge-gate・6本緑・Webロケータのログ・アンカー未移植の表の削除・束5暗転とG1全束0・全束の突き合わせ0・台帳の照合・段階5の並べ比べ）" --parent moorestech-albof -t task
bd update <出た ID> --claim
moores-wt status
```
`moores-wt status` で Mac に他の Editor が無いことを見てから、このレーンの worktree の Editor を `uloop launch` で起こし（起動済みなら使う）、`uloop compile --project-path ./moorestech_client` で `ErrorCount: 0`。他の Editor があれば終わるのを待つ（落とさない・`--force` を使わない）。`../moorestech_logs/README.md` を読んでから `../moorestech_logs/harness/uitk/S5-ladder/done.md` を作り、1 行目「状態: 作業中」・見出し「証拠」「台帳の照合」「差の表」「未完了」「要裁定」「integrator への依頼」を置く

---

### Task 1: 束 5 の暗転と G1 全束 0（R-6）

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 束」の束 5 の行・「## 追従記録」の末尾）

**Interfaces:**
- Consumes: `.github/scripts/check-uitk-web-follow.py`、`../moorestech_logs/harness/uitk/conductor.md`（指揮が書いた起点があればそれ）
- Produces: 束 5 の `暗転`・起点。G1 の出力の全束「未記帳 0 件」

- [ ] **Step 1: 起点を決める**

```bash
grep -n "束 5.*起点\|暗転の起点" ../moorestech_logs/harness/uitk/conductor.md | tail -3
for b in s5-challenge s5-gate s5-crosscut; do m=$(git log origin/feature/uitk --merges --format=%H --grep "uitk-lane/$b" | head -1); echo "$b $(git merge-base ${m}^2 origin/master | cut -c1-10)"; done
```
Expected: 指揮の記録に束 5 の起点があればそれを使う。無ければ 3 行の master 側の点のうち最も古いもの（S5 の 3 レーンが分かれた時点で取り込み済みの master。台帳「束のレーンを始めるとき」の規則。前例 S2-ladder D-5・S3-ladder D-2）。起草時の見込みは `0e81bbe3c3`。どちらを使ったかと 3 行を `done.md`「証拠」に書き、台帳の束 5 の行を `| 5 | 最後の束と撤去（チャレンジ一覧・言語ゲート・チュートリアル枠・Web の横断層） | 暗転 | <起点> | \`uitk-challenge-gate\` |` にする（中身と物差しの列はそのまま）

- [ ] **Step 2: G1 を回して記帳する**

```bash
python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD
```
Expected: 束 1〜5 の「（暗転・起点 …）: 未記帳 k 件」。k が 1 以上の束ごとに、出たコミットを `git show --stat <sha> -- moorestech_web/webui` で読み、「## 追従記録」の末尾へ `| <束> | <sha 10 桁> | 写した | <UITK 側の写し先> |` か `| <束> | <sha 10 桁> | 捨てた | <理由> |` で足す。処遇の型（前例: 台帳の束 2 の記帳）:
- 生成物 `src/shared/i18n/generated/localizationKeys.ts` の再生成だけ → `写した`「生成物の再生成だけ（Localization/localization.csv への追加に連動）。写し先は同じコミットの localization.csv と C#」
- 旧 R1 の休止の撤去・W0-switch → `捨てた`（既存の束 1・2 の同じコミットの理由をそのまま写す）
- Web の検証基盤（`e2e/`・mock-host・撮影）だけ → `捨てた`「Web の検証基盤の変更で、UITK の検証は Client.Playtest と NUnit が持つ」
- 束 5 の画面（challenge・eventLanguageGate・tutorial の枠・横断層）の Web の変更 → UITK 側に同じ変更があれば `写した`（ファイル）。無ければ記帳せず、bd を `--parent <Task 0 の ID>` で立てて `done.md`「未完了」に書く（追従の実装は画面のレーンへ回す。記帳で 0 にしない）
全束 0 になるまで繰り返す。写しが無くて記帳できない行が残ったら R-6 を満たせない（PR の G1 は未記帳では赤にならない〈plan §4.7〉ので、マージを止めるのはこの停止）: `done.md` の 1 行目を「状態: 停止（G1 の束 n に写しの無い Web の変更 k 件）」にし、`cmux wait-for -S uitk-s5-ladder` を送って待つ。画面のレーンか I-fix の写しが入ったら取り込んで記帳し直す。違反（切替済みの束への変更）は 0 のはず。出たら停止して指揮へ

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 束 5 を暗転にして全束の追従を記帳する（S5-ladder）"
```

---

### Task 2: アンカーの未移植の表を消す（R-5）

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`（と Unity が作った `.meta`）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoverageTest.cs`

**Interfaces:**
- Consumes: `AnchorIds.StaticIds`・`AnchorIds` の `*Prefix` 定数（`Client.Game.InGame.Tutorial.UIHighlight`）、`TutorialAnchor.Mark(`（`Client.Ui` のファイルの文字列で拾う）
- Produces: `TutorialAnchorCoverageTest.全てのアンカーIDはUITKで印が付いている()`

- [ ] **Step 1: 空を確かめる**

```bash
grep -rn "TutorialAnchorCoveragePending" moorestech_client/Assets/Scripts
grep -c "{ AnchorIds" moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
```
Expected: 参照は `TutorialAnchorCoverageTest.cs` の 2 か所と定義だけ。行 0。1 以上なら、その行のアンカーに UITK の印があるかを `grep -rn "AnchorIds.<名前>" moorestech_client/Assets/Scripts/Client.Ui` で見て、印があれば消し忘れとして消す（持ち主を `done.md` へ）。印が無ければ停止して指揮へ（画面のレーンの未完了で、このレーンが印を付けるのは範囲外）

- [ ] **Step 2: テストを書き換える（落ちる形の確認を先に）**

1 本目のテストメソッド（`全てのアンカーIDは印付けか未移植の表のどちらか一方にだけ載る`）を次に置き換える（`#region Internal` の 2 つのローカル関数は今のまま残す）。クラスの `<summary>` の 1 行目も「アンカー id ごとに UITK の印が付いていることと、ポーズのアンカーが解けることを確かめる（L1・plan §4.5）」／「Verifies every anchor id is marked in UITK and that pause anchors resolve (L1, plan §4.5)」へ直す:

```csharp
        [Test]
        public void 全てのアンカーIDはUITKで印が付いている()
        {
            // 印の無いアンカーは UITK で枠が永久に出ない。未移植の表は段階 5 で空になり消した
            // An unmarked anchor never shows its outline in UITK; the pending table emptied in stage 5 and was removed
            var marked = MarkedAnchorIds();
            var unmarked = AllAnchorIds().Where(anchorId => !marked.Contains(anchorId)).ToList();
            CollectionAssert.IsEmpty(unmarked, "UITK で TutorialAnchor.Mark の無いアンカー");

            #region Internal

            IEnumerable<string> AllAnchorIds()
            {
                // 固定 id と、接頭辞で作る id の接頭辞（AnchorIds の *Prefix 定数）
                // Static ids plus the prefixes of generated ids (AnchorIds *Prefix constants)
                var prefixes = typeof(AnchorIds).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral && field.Name.EndsWith("Prefix")).Select(field => (string)field.GetRawConstantValue());
                return AnchorIds.StaticIds.Concat(prefixes);
            }

            HashSet<string> MarkedAnchorIds()
            {
                // TutorialAnchor.Mark を呼ぶ Client.Ui のファイルが参照する AnchorIds の名を拾い、id に写す（工場メソッドは名前＋Prefix の定数）
                // Collect AnchorIds names referenced by Client.Ui files that call TutorialAnchor.Mark and map them to ids (factory methods map to Name+Prefix)
                var ids = new HashSet<string>();
                var root = Path.Combine(Application.dataPath, "Scripts", "Client.Ui");
                foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    var source = File.ReadAllText(file);
                    if (!source.Contains("TutorialAnchor.Mark(")) continue;
                    foreach (Match match in Regex.Matches(source, @"AnchorIds\.(\w+)"))
                    {
                        var field = typeof(AnchorIds).GetField(match.Groups[1].Value) ?? typeof(AnchorIds).GetField(match.Groups[1].Value + "Prefix");
                        if (field != null && field.IsLiteral) ids.Add((string)field.GetRawConstantValue());
                    }
                }
                return ids;
            }

            #endregion
        }
```
`TutorialAnchorCoveragePending.cs` をまだ消さずに書き換えだけで流して、新しいテストが PASS することを見る（印が全部あるので PASS）。対照として、`var marked = MarkedAnchorIds();` の次に `marked.Remove(AnchorIds.ChallengePanel);` の 1 行を一時に足して流し、FAIL（メッセージに `challenge-panel` が並ぶ）を見てからその行を消す（コミットしない）:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TutorialAnchorCoverageTest"
```
Expected: 一時の行がある回は 1 本目が FAIL、消した回は 2 本とも PASS

- [ ] **Step 3: 表を消してコンパイルとテスト**

```bash
git rm moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs.meta
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Anchor\."
```
Expected: `ErrorCount: 0`、`Client.Tests.Ui.Anchor` の全テスト PASS（件数を `done.md` へ）。`grep -rn "TutorialAnchorCoveragePending" moorestech_client docs/uitk` が 0 件（設計書・実行計画の言及は integrator への依頼に書く）

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/
git commit -m "test(uitk): アンカーの未移植の表を消し、全アンカーが UITK で印付きであることを直に確かめる（S5-ladder）"
```

---

### Task 3: 束 1 の写す予定 16 件を NUnit へ写す（R-9・bd moorestech-albof.21）

**Files:**（`CT` = `moorestech_client/Assets/Scripts/Client.Tests`。.meta は書かない）
- Create: `CT/Ui/Screens/PauseMenu/PauseMenuScreenPresenterTest.cs`・`CT/Ui/Screens/PauseMenu/PauseMenuFeedbackReportLabelsTest.cs`（PauseMenu 6→8 本。I-fix5 の `PauseMenuSubPageFlexTest.cs` が先に入っていれば 7→9 本。どちらも 10 本以下）
- Modify: `CT/Ui/Screens/PauseMenu/PauseMenuViewTest.cs`（180→約 188 行。I-fix5 の後は 181→約 189 行）
- Create: `CT/Ui/Screens/Settings/GraphicsQualityItemCommandsTest.cs`（Settings 4→5 本）
- Modify: `CT/Ui/Screens/Settings/SettingsViewTest.cs`（173→約 187 行）
- Create: `CT/Ui/Screens/BugReport/BugReportDeliveryStatusViewTest.cs`（BugReport 6→7 本）
- Modify: `CT/Ui/Screens/BugReport/BugReportFormViewTest.cs`（75→約 124）・`BugReportFormSendViewTest.cs`（130→約 172）・`BugReportRecoveryControlsViewTest.cs`（66→約 118）
- Modify: `docs/uitk/disposition.md`（束 1 の `写す予定` 16 行を消す）

**Interfaces:**
- Consumes: 既存の土台 `ScreenTestHost`・`BugReportFormScreenTestHost`・`RecordingPauseMenuCommands`・`RecordingSettingsCommands`・`FakePauseMenuModel`・`FakeSettingsModel`・`UiRoutingStateTestFactory`・`GameUiRootTestFactory`・`PauseMenuModelTestFactory`（internal・同じ `Client.Tests` の asmdef）、本番の辞書 `Localize.Initialize()`・`Localize.TrySetChosenLanguage(code)`・`LanguageCatalog.Languages`（前例 `CT/Ui/State/BugReportFormTextTest.cs:18-29`・`CT/Localization/Skit/SkitLocalizationRuntimeContentTest.cs:22-29`）。各テストの「前提にした既存 API」に file:line
- Produces: NUnit のテストメソッド 16 本（`[TestCase]` で展開して約 57 件）。新しい本番の API は無い

テストの一覧（Web の 1 行 = NUnit 1 メソッド。期待は起草時にコードを読んだ推測で、Step 1 で実測する）:

| # | Web | テストメソッド | 期待 |
|---|---|---|---|
| 1 | feedbackReportLabels.spec | PauseMenuFeedbackReportLabelsTest.設定で選んだ言語の本番辞書で感想の送信ボタンと完了通知を出す ×4 | PASS（推測） |
| 2 | pauseMenu.spec | PauseMenuScreenPresenterTest.ポーズのUI状態へ遷移すると画面が出てセーブとセーブして終了のコマンドを出す | PASS（推測） |
| 3 | PauseMenuPanel.test | PauseMenuScreenPresenterTest.ポーズを閉じて開き直すと書きかけは空になる | PASS（推測） |
| 4 | PauseMenuPanel.test | PauseMenuViewTest.設定画面は言語選択と戻るボタンを出し戻るはtopへのshow_pageを送る（既存に追記） | PASS（推測） |
| 5 | GraphicsQualitySettings.test | GraphicsQualityItemCommandsTest.モデルが変わらない間の連続操作と…今の項目を送る ×9 | **FAIL の見込み**（Step 2 で判定） |
| 6 | GraphicsQualitySettings.test | GraphicsQualityItemCommandsTest.選択肢を押すとtestidに対応する項目だけを差し替えた値を送る ×7 | PASS（推測） |
| 7 | SprintModeSelect.test | SettingsViewTest.走りはholdの初期表示からtoggleを選択できる | PASS（推測） |
| 8 | deliveryStatus.test | BugReportDeliveryStatusViewTest.配送の行と送れない理由の状態行を同じ画面に同時に出す ×6 | PASS（推測） |
| 9 | deliveryStatus.test | BugReportDeliveryStatusViewTest.各言語の本番辞書で配送不能件数を出し旧い復旧案内を出さない ×言語数 | PASS（推測） |
| 10 | deliveryStatus.test | BugReportDeliveryStatusViewTest.各言語の本番辞書で件数不明を完了や0件にしない ×言語数 | PASS（推測） |
| 11 | recoveryControls.test | BugReportRecoveryControlsViewTest.各言語の本番辞書で救出ボタン2つと書き出し中の文言が未翻訳キーにならない ×言語数 | PASS（推測） |
| 12 | recoveryControls.test | BugReportRecoveryControlsViewTest.各言語の本番辞書で書き出しの完了と失敗の文言が差し込み値を保ち未翻訳キーにならない ×言語数 | PASS（推測） |
| 13 | hostSubmissionLock.test | BugReportFormViewTest.応答待ちが終わってもホスト送信中は種別と本文を保護しreadyで解く（既存を置き換え） | PASS（推測） |
| 14 | productionSendingLabels.test | BugReportFormViewTest.ホスト送信中の状態行は本番辞書の各言語で種別の文言を出す ×8 | PASS（推測） |
| 15 | reportKindLabels.test | BugReportFormSendViewTest.感想でも欠損があれば送信成功時に欠損のエラーのトーストを出す | PASS（推測） |
| 16 | reportKindLabels.test | BugReportFormSendViewTest.感想の送信中は表示とロックを保ち完了は送信開始時の種別で伝える | PASS（推測） |


「本番辞書」は `Localize.Initialize()` 後の `Localize.TrySetChosenLanguage(code)` で引く（Vanilla 辞書は `Localization/localization.csv` から生成される `VanillaLocalizationTable`。`Client.Localization/Dictionary/VanillaLocalizationDictionaryFactory.cs:9-37`）。文言をそのまま比べるテスト（1・14）は Web と同じく言語ごとの期待値を `[TestCase]` に書き、「未翻訳キーにならない」型（9〜12）は `LanguageCatalog.Languages` で回す。配送の行・書き出しの行は `Apply` の時に解いた文字列を入れるので（`BugReportFormView.cs:90`・`BugReportRecoveryControlsView.cs:51`）、8〜12 は言語を切り替えてから `Apply` する。テスト 4 の Web の `graphics-quality-settings` は Web テストの mock だけが付けた testid で（`PauseMenuPanel.test.ts:37`）、UITK では型 `GraphicsQualitySettingsView` と `graphics-preset` で見る。

#### 1. `e2e/tests/system/feedbackReportLabels.spec.ts`「感想送信のボタンと完了通知: ${label.locale}」

- 置き場: **Create** `Client.Tests/Ui/Screens/PauseMenu/PauseMenuFeedbackReportLabelsTest.cs`
- 埋める穴: 4 言語の本番辞書で、感想の送信ボタンと完了トーストの文言を見る。設定画面の言語選択から通す（Web と同じ順）。言語の切り替えは本物の `SettingsCommands` に通して Localize を実際に切り替える
- 期待: PASS（推測）

```csharp
using System.Linq;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.Commands;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using Client.Ui.State.Toast;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// 設定で言語を選んでから感想を送り、送信ボタンと完了のトーストがその言語の本番辞書の文言になることを確かめる（L1。Web の feedbackReportLabels.spec.ts）
    /// Verifies that after choosing a language in settings, the impressions send button and completion toast use that language's production text (L1; Web feedbackReportLabels.spec.ts)
    /// </summary>
    public class PauseMenuFeedbackReportLabelsTest : ScreenTestHost
    {
        private FakePauseMenuModel _model;
        private RecordingPauseMenuCommands _commands;
        private PauseMenuView _view;

        [SetUp]
        public void MountPauseMenu()
        {
            // 言語の選択は本物の SettingsCommands に通し、Localize の言語を実際に切り替える
            // Language choices go through the real SettingsCommands so Localize actually switches
            _model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.Top));
            _commands = new RecordingPauseMenuCommands();
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            _view = Mount(new PauseMenuView(_model.Current, settings.Current, _commands, new SettingsCommands(), Toasts, Toaster, new BugReportDraft()));
            _view.SetShown(true);
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/system/feedbackReportLabels.spec.ts">感想送信のボタンと完了通知: ${label.locale}</web-test>
        [TestCase("japanese", "感想を送信", "感想をoutboxに書き出しました", "バグ報告を送信")]
        [TestCase("english", "Send impressions", "Impressions saved to outbox", "Send bug report")]
        [TestCase("german", "Eindrücke senden", "Eindrücke im Postausgang gespeichert", "Fehlerbericht senden")]
        [TestCase("korean", "감상 보내기", "감상이 보낼 편지함에 저장되었습니다", "버그 신고 보내기")]
        public void 設定で選んだ言語の本番辞書で感想の送信ボタンと完了通知を出す(string locale, string send, string sent, string bug)
        {
            // 設定で言語を選ぶとロケール印がその言語になる
            // Choosing a language in settings switches the locale marker to it
            ShowPage(PauseMenuPage.Settings);
            simulate.Click(Find<Button>(_view, $"language-select-option-{locale}"));
            Assert.IsNotNull(_view.Q($"pause-menu-locale-{locale}"), "言語の切り替えが画面に届かない");

            // トップへ戻ってバグ報告を開くと送信ボタンはバグの文言
            // Back to the top and into the bug report, the send button reads the bug label
            simulate.Click(Find<Button>(_view, "pause-menu-back"));
            ShowPage(PauseMenuPage.Top);
            simulate.Click(Find<Button>(_view, "pause-menu-open-bug-report"));
            ShowPage(PauseMenuPage.BugReport);
            Assert.AreEqual(bug, SendLabel());

            // 感想へ切り替えて送ると、ボタンと完了のトーストが感想の文言になる
            // Switching to impressions and sending uses the impressions labels on the button and the toast
            simulate.Click(Find<Button>(_view, "bug-report-kind-feedback"));
            Assert.AreEqual(send, SendLabel());
            Find<TextField>(_view, "bug-report-description").value = "feedback label QA";
            simulate.Click(Find<FormButton>(_view, "bug-report-send"));
            _commands.CompleteSubmit(UiCommandAccepted.Instance);
            Assert.AreEqual(sent, ShownToasts.Single().Text);
            Assert.AreEqual(GameUiToastVariant.Info, ShownToasts.Single().Variant);

            // 送った中身は本文と感想の種別
            // The submitted payload carries the description and the impressions kind
            var submit = _commands.Issued.Last();
            Assert.AreEqual(PauseMenuCommandKind.SubmitBugReport, submit.Kind);
            Assert.AreEqual("feedback label QA", submit.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, submit.ReportKind);

            // 開き直すと種別はバグへ、本文は空へ戻る
            // Reopening returns the kind to bug and the description to empty
            ShowPage(PauseMenuPage.Top);
            ShowPage(PauseMenuPage.BugReport);
            Assert.AreEqual(bug, SendLabel());
            Assert.AreEqual("", Find<TextField>(_view, "bug-report-description").value);
        }

        private string SendLabel()
        {
            return Find<FormButton>(_view, "bug-report-send").Q<Label>().PlainText();
        }

        // ページはホストが決めるので、押した後に偽モデルへ入れて描き直させる。スクロールバーの出し入れで 2 フレーム要る
        // The host decides the page, so push it into the fake model after the press; scroller toggling needs two frames
        private void ShowPage(PauseMenuPage page)
        {
            _model.Set(FakePauseMenuModel.Page(page));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
```

前提にした既存 API:
- `ScreenTestHost` の Mount・Find・Toasts・Toaster・ShownToasts、英語で始める SetUp: `Client.Tests/Ui/Screens/ScreenTestHost.cs:25-27,34-35,56-68`
- `FakePauseMenuModel.Page`: `Client.Tests/Ui/Screens/PauseMenu/FakePauseMenuModel.cs:22`、`FakeModel<T>.Set`・`.Current`（`PauseMenuViewTest.cs:33,37,48` の使い方）
- `RecordingPauseMenuCommands.CompleteSubmit`・`PauseMenuCommandRecord`: `RecordingPauseMenuCommands.cs:22-36,60-65`
- `PauseMenuView` の ctor・SetShown・ロケール印: `Client.Ui/Screens/PauseMenu/PauseMenuView.cs:36,74,155`
- `SettingsCommands.SetLanguage` が `Localize.TrySetChosenLanguage` を呼ぶ: `Client.Ui.State/Settings/SettingsCommands.cs:31-35`（前例 `Client.Tests/Ui/State/SettingsCommandsTest.cs:41`）
- 送信ボタン文言の読み方 `Q<Label>().text`: `BugReportFormSendViewTest.cs:102`。I-fix5 の後は `LocalizedLabel.text` が行送りの印 `<line-height=Npx>` を前に付け、行末の約物を詰める改行も入るので、`Client.Tests.Ui.TestHost.LabelPlainText.PlainText()`（`origin/uitk-lane/i-fix5` の `Client.Tests/Ui/TestHost/LabelPlainText.cs:15-23`。`SourceText` から印を外す。素の Label は `text` から印を外す）で読む（I-fix5 が同じメソッドの題の読みをこの形へ変えた: `PauseMenuViewTest.cs:117`）
- ページを 2 フレームで落ち着かせる: `PauseMenuViewTest.cs:171-178`
- 期待の文言: `Localization/localization.csv:345-347`（Web の期待値と同じ）


#### 2. `e2e/tests/system/pauseMenu.spec.ts`「PauseMenu遷移で表示しセーブとセーブして終了actionを送る」
#### 3. `src/features/pauseMenu/PauseMenuPanel.test.ts`「パネルを外して付け直すと書きかけは空になる」

- 置き場: **Create** `Client.Tests/Ui/Screens/PauseMenu/PauseMenuScreenPresenterTest.cs`（2 と 3 を同じクラスに入れる）
- 埋める穴: 2 は UI 状態の遷移 → Presenter が出す → 2 つのコマンド、を一続きで見る。3 はポーズを開くたびに `PauseMenuScreenPresenter` が書きかけを Reset することを見る（Web の「外して付け直す」は UITK では「閉じて開き直す」に当たる。View は起動時に 1 回だけ載り、外れないため）
- 期待: どちらも PASS（推測）

```csharp
using System.Linq;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.State;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの UI 状態への遷移で画面が出て、開くたびに書きかけが空へ戻ることを本物の Presenter と UiRoutingState で確かめる（L1。前例 InventoryScreenVisibilityTest・RecipeSelectionCloseResetTest）
    /// Verifies with the real presenter and UiRoutingState that entering the pause UI state shows the screen and every open clears the draft (L1; precedents InventoryScreenVisibilityTest and RecipeSelectionCloseResetTest)
    /// </summary>
    public class PauseMenuScreenPresenterTest : ScreenTestHost
    {
        private GameObject _host;
        private UIStateControl _control;
        private PauseMenuStateService _pauseMenuState;
        private RecordingPauseMenuCommands _commands;
        private BugReportDraft _draft;
        private PauseMenuScreenPresenter _presenter;

        [SetUp]
        public void MountPresenter()
        {
            // ゲーム画面とポーズの間を行き来できるルータと、本物のページ状態の上の PauseMenuModel で組む
            // Build over a router moving between the game screen and the pause, and a PauseMenuModel on a real page state
            _host = new GameObject("pause-menu-presenter");
            var routing = UiRoutingStateTestFactory.CreateSwitchable(_host, UIStateEnum.GameScreen, new[] { UIStateEnum.GameScreen, UIStateEnum.PauseMenu }, out _control);
            _pauseMenuState = new PauseMenuStateService();
            _commands = new RecordingPauseMenuCommands();
            _draft = new BugReportDraft();
            var root = GameUiRootTestFactory.Create(_host, rootVisualElement);
            _presenter = new PauseMenuScreenPresenter(root, routing, PauseMenuModelTestFactory.Create(_pauseMenuState), new SettingsModel(), _commands, new RecordingSettingsCommands(), Toasts, Toaster, _draft);
            _presenter.Initialize();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DestroyHost()
        {
            // Presenter はゲーム寿命で Dispose を持たない（S5-crosscut T13）。購読する源・ルータ・層はテストごとに作り直すので宿主を消すだけ
            // The presenter is game-lifetime with no Dispose (S5-crosscut T13); its sources, router and layers are rebuilt per test, so only the host goes
            Object.DestroyImmediate(_host);
        }

        /// <web-test file="e2e/tests/system/pauseMenu.spec.ts">PauseMenu遷移で表示しセーブとセーブして終了actionを送る</web-test>
        [Test]
        public void ポーズのUI状態へ遷移すると画面が出てセーブとセーブして終了のコマンドを出す()
        {
            var view = rootVisualElement.Q<PauseMenuView>();
            Assert.IsNotNull(view, "Presenter が View を層へ載せていない");
            Assert.IsFalse(IsShown(view), "ゲーム画面でポーズが出ている");

            // ポーズへ遷移すると画面が出る
            // Entering the pause shows the screen
            OpenPause();
            Assert.IsTrue(IsShown(view), "ポーズへ遷移しても画面が出ない");
            Assert.IsNotNull(view.Q("pause-menu"));

            // セーブとセーブして終了を順に押すと、それぞれのコマンドが 1 回ずつ出る
            // Pressing save then save-and-quit issues each command once
            simulate.Click(Find<Button>(view, "pause-menu-save"));
            simulate.Click(Find<Button>(view, "pause-menu-save-and-quit"));
            CollectionAssert.AreEqual(new[] { PauseMenuCommandKind.Save, PauseMenuCommandKind.SaveAndQuit }, _commands.Issued.Select(record => record.Kind));
        }

        /// <web-test file="src/features/pauseMenu/PauseMenuPanel.test.ts">パネルを外して付け直すと書きかけは空になる</web-test>
        [Test]
        public void ポーズを閉じて開き直すと書きかけは空になる()
        {
            // 1 回目のポーズでバグ報告に感想の書きかけを残す
            // Leave an impressions draft on the bug report during the first pause
            OpenPause();
            ShowPage(PauseMenuPage.BugReport);
            var view = rootVisualElement.Q<PauseMenuView>();
            simulate.Click(Find<Button>(view, "bug-report-kind-feedback"));
            Find<TextField>(view, "bug-report-description").value = "ベルトが止まる";
            Assert.AreEqual("ベルトが止まる", _draft.Description);

            // 閉じてから開き直し、バグ報告へ進むと本文も種別も既定へ戻っている
            // Close, reopen and go to the bug report: the description and kind are back to defaults
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.GameScreen);
            Assert.IsFalse(IsShown(view), "閉じても画面が残る");
            OpenPause();
            ShowPage(PauseMenuPage.BugReport);
            Assert.AreEqual("", Find<TextField>(view, "bug-report-description").value);
            Assert.AreEqual("", _draft.Description);
            Assert.AreEqual(PlaytestReportKind.Bug, _draft.Kind);
            Assert.AreEqual("Send bug report", Find<FormButton>(view, "bug-report-send").Q<Label>().PlainText());
        }

        // 本番の PauseMenuState.OnEnter と同じく、遷移したら開いたことを知らせる
        // Announce the open after the transition, as the production PauseMenuState.OnEnter does
        private void OpenPause()
        {
            UiRoutingStateTestFactory.Transit(_control, UIStateEnum.PauseMenu);
            _pauseMenuState.OnEnter();
            simulate.FrameUpdate();
        }

        // ページの切り替えは本物のページ状態へ入れ、PauseMenuModel 経由で View に届ける
        // Page changes go into the real page state and reach the view through PauseMenuModel
        private void ShowPage(PauseMenuPage page)
        {
            _pauseMenuState.ShowPage(page);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
```

前提にした既存 API:
- `PauseMenuScreenPresenter` の ctor・Initialize（View を `UiLayer.PauseMenu` の層へ載せ、`OnPauseMenuOpened` で `_draft.Reset()`、ルータで SetShown）: `Client.Ui/Screens/PauseMenu/PauseMenuScreenPresenter.cs:33,46-68`。起草時の `Dispose`（:70-75）は S5-crosscut T13（DC-32 A）がゲーム寿命の型から外すので、テストは呼ばない。購読の源（`PauseMenuModel`・`UiRoutingState`）はどちらもテストごとに作る物で、静的な源を購読しないので漏れない。Task 0 Step 3 の grep で `PauseMenuScreenPresenter` に `Dispose` が残っていたら（T13 が D-16 で残した型）、TearDown の先頭で `_presenter.Dispose();` を呼ぶ形に戻す
- `UiRoutingStateTestFactory.CreateSwitchable`・`Transit`: `Client.Tests/Ui/Screens/Inventory/Support/UiRoutingStateTestFactory.cs:26,53`（前例 `Client.Tests/Ui/Screens/Recipe/Input/RecipeSelectionCloseResetTest.cs:30,49`）
- `GameUiRootTestFactory.Create`: `Client.Tests/Ui/Screens/Inventory/Support/GameUiRootTestFactory.cs:15`（前例 `Client.Tests/Ui/Screens/Skit/SkitScreenPresenterTest.cs:103`）
- `PauseMenuModelTestFactory.Create`（internal・同じ Tests.asmdef）: `Client.Tests/Ui/State/PauseMenuModelTestFactory.cs:15-20`
- `PauseMenuStateService.ShowPage`・`OnEnter`（ページを Top にしてから開いた通知）: `Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuStateService.cs:20,42-49`。本番は `PauseMenuState.cs:22` が OnEnter を呼ぶ。テストの IdleUiState は呼ばないので手で呼ぶ
- `new SettingsModel()` の前例: `Client.Tests/Ui/State/SettingsModelTest.cs:34`
- ポーズの画面だけ PauseMenu 列が真: `Client.Ui.State/Routing/UiScreenTraits.cs:55,61`。PauseMenu → UiScreen.PauseMenu: `Client.Ui.State/Routing/UiScreenRouting.cs:24`
- `BugReportDraft.Reset` が種別もバグへ戻す: `Client.Ui.State/PauseMenu/BugReportDraft.cs:30-35`


#### 4. `src/features/pauseMenu/PauseMenuPanel.test.ts`「設定画面は言語選択と戻るボタンを出し、戻るはtopへのshow_pageを送る」

- 置き場: **Modify** `Client.Tests/Ui/Screens/PauseMenu/PauseMenuViewTest.cs`。既存メソッド `設定画面は言語選択と戻るボタンを出し戻るはtopへのshow_pageを送る`（起草時 112-128 行。I-fix5 の後は 113-129 行で、題の読みが `.PlainText()`）を、メソッド名で探して下で置き換える（web-test の 1 行と、走りと画質の節の assert を足す。題の読みは I-fix5 の後の `.PlainText()` のまま。メソッド全体を `.text` の形で上書きして I-fix5 の変更を戻さない）
- using の追加: `using Client.Ui.Screens.Settings;`（`using Client.Tests.Ui.TestHost;` は I-fix5 が足す。I-fix5 が未マージなら足さず、下の `.PlainText()` を `.text` に読み替える）
- 埋める穴: Web が見る `sprint-mode-select` と `graphics-quality-settings` が設定画面に出ること。`graphics-quality-settings` は Web テストのモックが付けた testid で、本番の Web にも UITK にも無いので、UITK では型 `GraphicsQualitySettingsView` と、その先頭の択一 `graphics-preset` で見る
- 期待: PASS（推測）

```csharp
        /// <web-test file="src/features/pauseMenu/PauseMenuPanel.test.ts">設定画面は言語選択と戻るボタンを出し、戻るはtopへのshow_pageを送る</web-test>
        [Test]
        public void 設定画面は言語選択と戻るボタンを出し戻るはtopへのshow_pageを送る()
        {
            ShowPage(PauseMenuPage.Settings);
            Assert.AreEqual("Settings", _view.Q<Label>(className: "pause-menu__title").PlainText());
            Assert.IsNotNull(_view.Q("language-select"));
            Assert.IsNull(_view.Q("pause-menu-save"));
            Assert.IsInstanceOf<GameScrollView>(Find<VisualElement>(_view, "pause-menu-body"));

            // 言語に続いて走りと画質の節が 1 つずつ並ぶ
            // The sprint and graphics sections follow the language, one each
            Assert.AreEqual(1, _view.Query<SprintModeSelectView>().ToList().Count);
            Assert.IsNotNull(_view.Q("sprint-mode-select"));
            Assert.AreEqual(1, _view.Query<GraphicsQualitySettingsView>().ToList().Count);
            Assert.IsNotNull(_view.Q("graphics-preset"));

            simulate.Click(Find<Button>(_view, "pause-menu-back"));
            Assert.AreEqual(PauseMenuCommandKind.ShowPage, _commands.Issued.Last().Kind);
            Assert.AreEqual(PauseMenuPage.Top, _commands.Issued.Last().Page);
            Assert.IsTrue(Find<Button>(_view, "pause-menu-back").ClassListContains(AnchorIds.PauseBackToTop));
            Assert.AreEqual(720f - 80f, Frame().worldBound.height, 0.5f);
            Assert.AreEqual(420f, Frame().worldBound.width, 0.5f);
            Assert.AreEqual(640f, Frame().worldBound.center.x, 0.5f);
        }
```

前提にした既存 API:
- 設定ページが言語・走り・画質の 3 節を並べる: `Client.Ui/Screens/PauseMenu/PauseMenuSettingsPage.cs:20-29`
- 走りの択一の testid `sprint-mode-select`: `Client.Ui/Screens/Settings/SprintModeSelectView.cs:33`。画質のプリセットの択一 `graphics-preset`: `Client.Ui/Screens/Settings/GraphicsQualitySettingsView.cs:36`
- 既存のメソッド本体と ShowPage・Frame: `PauseMenuViewTest.cs:112-128,166-178`


#### 5. `src/features/settings/GraphicsQualitySettings.test.ts`「topicを固定したまま連続操作とpreset直後に%sを送る」
#### 6. `src/features/settings/GraphicsQualitySettings.test.ts`「%sの操作値を送る」

- 置き場: **Create** `Client.Tests/Ui/Screens/Settings/GraphicsQualityItemCommandsTest.cs`（SettingsViewTest に足すと 200 行を超えるため別クラス）
- 埋める穴: 6 は testid と送る項目の対応（`GraphicsQualityItemRows` の表）を View で押して見る。Web の 7 件（描画倍率・地形・オンオフ 5 項目）を全部写す。期待値は表を通さず、testid から独立に組む。5 はモデルが変わらない間の連続操作とプリセット直後の操作で、前の操作を巻き戻さないことを見る
- 末尾の行（ocean）が 720px のパネルの外へ出ても押せるよう、ScrollView に入れて `ScrollTo` してから押す（前例 `PauseMenuSettingsLayoutTest.cs:73-76`）
- 期待: 6 は PASS（推測）。**5 は今の本番コードでは FAIL の見込み**（コードを読んだ推測で、実行していない）。`GraphicsQualityRowSwitch` は `_current` を `Apply` でしか更新しないため（`GraphicsQualityRow.cs:58,69,72-76`）、モデルが変わらないまま 2 回目を押すと影の値を Medium に戻して送る。プリセットを押しても各行の `_current` は変わらないので（`GraphicsQualitySettingsView.cs:37`）、プリセット直後の操作もプリセットを巻き戻す。ただし `ExpectedAfterPress` の testid が `graphics-shadows-off` のときは、前半の assert は通る。扱いは「足りない API・確かめられなかった前提」1

```csharp
using System;
using System.Linq;
using Client.Game.InGame.GraphicsQuality;
using Client.Ui.Screens.Settings;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Settings
{
    /// <summary>
    /// 画質の各選択肢の testid が送る項目と値に対応し、モデルが変わらない間の連続操作でも前の操作を巻き戻さないことを確かめる（L1。Web の GraphicsQualitySettings.test.ts）
    /// Verifies each graphics option testid maps to the item and value it sends, and back-to-back presses on a stale model never undo the previous one (L1; Web GraphicsQualitySettings.test.ts)
    /// </summary>
    public class GraphicsQualityItemCommandsTest : ScreenTestHost
    {
        private static readonly GraphicsQualitySettings Medium = GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium);
        private static readonly GraphicsQualitySettings Low = GraphicsQualityPresets.Get(GraphicsQualityPreset.Low);

        private RecordingSettingsCommands _commands;
        private ScrollView _scroll;
        private GraphicsQualitySettingsView _view;

        [SetUp]
        public void MountGraphicsSettings()
        {
            // 末尾の行が 720px の外へ出ても押せるよう、スクロールに入れて載せる
            // Mount inside a scroller so the last rows stay pressable beyond the 720px panel
            _commands = new RecordingSettingsCommands();
            _view = new GraphicsQualitySettingsView(FakeSettingsModel.Default(), _commands);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.height = 720f;
            _scroll.Add(_view);
            Mount(_scroll);
        }

        /// <web-test file="src/features/settings/GraphicsQualitySettings.test.ts">%sの操作値を送る</web-test>
        [TestCase("graphics-render-scale-75")]
        [TestCase("graphics-terrain-coarse")]
        [TestCase("graphics-ssao-off")]
        [TestCase("graphics-postProcessing-off")]
        [TestCase("graphics-effects-off")]
        [TestCase("graphics-mapObjects-off")]
        [TestCase("graphics-ocean-off")]
        public void 選択肢を押すとtestidに対応する項目だけを差し替えた値を送る(string optionTestid)
        {
            Press(optionTestid);
            var sent = _commands.Issued.Single();
            Assert.AreEqual(SettingsCommandKind.SetGraphicsQuality, sent.Kind);
            Assert.AreEqual(ExpectedAfterPress(Medium, optionTestid), sent.Graphics, $"{optionTestid} が別の項目か別の値を送った");
        }

        /// <web-test file="src/features/settings/GraphicsQualitySettings.test.ts">topicを固定したまま連続操作とpreset直後に%sを送る</web-test>
        [TestCase("graphics-draw-distance-1000")]
        [TestCase("graphics-render-scale-75")]
        [TestCase("graphics-terrain-coarse")]
        [TestCase("graphics-shadows-off")]
        [TestCase("graphics-ssao-off")]
        [TestCase("graphics-postProcessing-off")]
        [TestCase("graphics-effects-off")]
        [TestCase("graphics-mapObjects-off")]
        [TestCase("graphics-ocean-off")]
        public void モデルが変わらない間の連続操作とプリセット直後の操作は前の操作を巻き戻さず今の項目を送る(string optionTestid)
        {
            // 影オフの直後に、モデルを更新しないまま次の項目を押す
            // Press the next item right after shadows-off without updating the model
            Press("graphics-shadows-off");
            Press(optionTestid);
            var afterShadowsOff = Medium.WithShadowsEnabled(false);
            Assert.AreEqual(afterShadowsOff, _commands.Issued[0].Graphics);
            Assert.AreEqual(ExpectedAfterPress(afterShadowsOff, optionTestid), _commands.Issued[1].Graphics, "2 回目の操作が影オフを巻き戻した");

            // 低プリセットの直後に同じ項目を押すと、低の値に今の項目だけを差し替えて送る
            // Pressing the same item right after the low preset sends the low values with only that item replaced
            Press("graphics-preset-low");
            Press(optionTestid);
            Assert.AreEqual(SettingsCommandKind.ApplyGraphicsPreset, _commands.Issued[2].Kind);
            Assert.AreEqual(GraphicsQualityPreset.Low, _commands.Issued[2].Preset);
            Assert.AreEqual(ExpectedAfterPress(Low, optionTestid), _commands.Issued[3].Graphics, "プリセット直後の操作がプリセットを巻き戻した");
        }

        // 選択肢をスクロールで見える位置へ出してから押す
        // Scroll the option into view before pressing it
        private void Press(string optionTestid)
        {
            var option = Find<Button>(_view, optionTestid);
            _scroll.ScrollTo(option);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            simulate.Click(option);
        }

        // GraphicsQualityItemRows を通さず、testid から期待の値を独立に組む
        // Build the expected value from the testid independently of GraphicsQualityItemRows
        private static GraphicsQualitySettings ExpectedAfterPress(GraphicsQualitySettings settings, string optionTestid)
        {
            return optionTestid switch
            {
                "graphics-draw-distance-1000" => settings.WithDrawDistance(GraphicsDrawDistance.Meters1000),
                "graphics-render-scale-75" => settings.WithRenderScale(GraphicsRenderScale.Percent75),
                "graphics-terrain-coarse" => settings.WithTerrainDetail(GraphicsTerrainDetail.Coarse),
                "graphics-shadows-off" => settings.WithShadowsEnabled(false),
                "graphics-ssao-off" => settings.WithSsaoEnabled(false),
                "graphics-postProcessing-off" => settings.WithPostProcessingEnabled(false),
                "graphics-effects-off" => settings.WithEffectsEnabled(false),
                "graphics-mapObjects-off" => settings.WithMapObjectsVisible(false),
                "graphics-ocean-off" => settings.WithOceanVisible(false),
                _ => throw new ArgumentOutOfRangeException(nameof(optionTestid), optionTestid, null),
            };
        }
    }
}
```

前提にした既存 API:
- `GraphicsQualitySettingsView` の ctor（プリセットは `ApplyGraphicsPreset`、項目は表の行から作る）・Apply: `Client.Ui/Screens/Settings/GraphicsQualitySettingsView.cs:25-51,53-66`
- 項目の表と testid の語: `Client.Ui/Screens/Settings/GraphicsQualityRows/GraphicsQualityItemRows.cs:12-23`。オンオフの選択肢 `graphics-{語}-on/off`: `GraphicsQualityToggleRow.cs:30-34`。数値の選択肢 `graphics-draw-distance-{m}`・`graphics-render-scale-{%}`・`graphics-terrain-fine/coarse`: `GraphicsQualityValueRows.cs:31,56,66-67`
- 押下は `_current` の 1 項目を差し替えて `SetGraphicsQuality` へ渡し、`_current` は Apply でだけ変わる: `GraphicsQualityRows/GraphicsQualityRow.cs:58,69,72-76`
- `GraphicsQualitySettings.With*` と値の等価（`Equals(object)` を上書き）: `Client.Game/InGame/GraphicsQuality/GraphicsQualitySettings.cs:9,38-80,90`
- プリセットの値（Medium と Low）: `Client.Game/InGame/GraphicsQuality/GraphicsQualityPresets.cs:16-17`
- `RecordingSettingsCommands` の記録（Kind・Preset・Graphics）: `Client.Tests/Ui/Screens/Settings/RecordingSettingsCommands.cs:19-35,45-46`。`FakeSettingsModel.Default`（Medium）: `FakeSettingsModel.cs:20-23`
- 選んである選択肢をもう一度押しても送る: `ModeSwitch.cs:118`（前例 `SettingsViewTest.cs:146-147`）
- ScrollTo してから押す: `PauseMenuSettingsLayoutTest.cs:73-76`


#### 7. `src/features/settings/SprintModeSelect.test.ts`「holdの初期表示からtoggleを選択できる」

- 置き場: **Modify** `Client.Tests/Ui/Screens/Settings/SettingsViewTest.cs`。`走りの復旧記録を表示し解除のtopicで消す`（81 行の `}`）の直後に足す。using の追加は無し
- 埋める穴: hold で開いた画面から toggle を押すと `SetSprintInputMode(Toggle)` が出る。`SprintInputMode` の既定値と取り違えないよう、コマンドの種類も見る
- 期待: PASS（推測）

```csharp
        /// <web-test file="src/features/settings/SprintModeSelect.test.ts">holdの初期表示からtoggleを選択できる</web-test>
        [Test]
        public void 走りはholdの初期表示からtoggleを選択できる()
        {
            // hold で開くと hold だけが選ばれている
            // Opening on hold selects only hold
            var view = Mount(new SprintModeSelectView(Settings(GraphicsQualityPresetChoice.Medium, false, SprintInputMode.Hold, false), _commands));
            Assert.IsTrue(IsSelected(view, "sprint-mode-option-hold"));
            Assert.IsFalse(IsSelected(view, "sprint-mode-option-toggle"));

            // toggle を押すと走りの設定コマンドが toggle で 1 回出る
            // Pressing toggle issues the sprint setting command once with toggle
            simulate.Click(Find<Button>(view, "sprint-mode-option-toggle"));
            Assert.AreEqual(SettingsCommandKind.SetSprintInputMode, _commands.Issued.Single().Kind);
            Assert.AreEqual(SprintInputMode.Toggle, _commands.Issued.Single().SprintMode);
        }
```

前提にした既存 API:
- `SprintModeSelectView` の ctor・Apply・選択肢の testid: `Client.Ui/Screens/Settings/SprintModeSelectView.cs:21-43`
- `SettingsViewTest` の private `Settings(...)`・`_commands`: `SettingsViewTest.cs:21-27,167-171`
- `IsSelected`: `ScreenTestHost.cs:78-81`


#### 8. `src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts`「送信状況 %j と送信可否を同時に表示する」
#### 9. 同「%s辞書で配送不能件数と復旧案内を出す」
#### 10. 同「%s辞書で件数不明を完了や0件にしない」

- 置き場: **Create** `Client.Tests/Ui/Screens/BugReport/BugReportDeliveryStatusViewTest.cs`
- 埋める穴: 8 は配送の行と状態の行（送れない理由）を同じ画面に同時に出すこと。文言は BugReportFormTextTest にあるが View のテストが無い。9・10 は全言語の本番辞書（UITK は日本語だけ）
- 配送の行は起草時は素の Label で、`Apply` の時に解決した文字列が入る（`BugReportFormView.cs:90`）。I-fix6 の後は `LocalizedLabel` で `SetText(DescribeDelivery(...))`（`DescribeDelivery` が `UiText` を返す。`origin/uitk-lane/i-fix6` の `BugReportFormView.cs:71,90`）。Web は辞書を後から差し替えて描き直すが、UITK はどちらの形でも言語を切り替えてから `Apply` すれば同じ文になる。読みは `.PlainText()`（I-fix5 の行送りの印を外す）
- 期待: どれも PASS（推測）

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.TestHost;
using Client.Ui.State.PauseMenu;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 配送の行が状態の行と同じ画面に並び、各言語の本番辞書で件数と理由を保つことを確かめる（L1。Web の BugReportFormTests/deliveryStatus.test.ts）
    /// Verifies the delivery line sits beside the status line and keeps counts and reasons in every language's production dictionary (L1; Web BugReportFormTests/deliveryStatus.test.ts)
    /// </summary>
    public class BugReportDeliveryStatusViewTest : BugReportFormScreenTestHost
    {
        private const string UntranslatedKeyPrefix = "ui.bugReport.";

        // 期待は Web と同じく辞書から独立に書いた日本語
        // Expectations are Japanese written independently of the dictionary, as on the Web
        private static IEnumerable<TestCaseData> KnownDeliveries()
        {
            yield return new TestCaseData(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenMissing, Counts(3, 0)), "未送信: 開発者トークンが未設定です（この機材に3件保存）").SetName("配送行と状態行_未送信_トークン未設定");
            yield return new TestCaseData(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, Counts(3, 0)), "未送信: 開発者トークンを読めません（この機材に3件保存）").SetName("配送行と状態行_未送信_トークン読めない");
            yield return new TestCaseData(new PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason.DistributionUnavailable, Counts(3, 0)), "未送信: build-infoとSteamの送信条件を確認してください（この機材に3件保存）").SetName("配送行と状態行_未送信_配布条件");
            yield return new TestCaseData(new PlaytestDeliveryPending(Counts(3, 0)), "送信待ち 3件").SetName("配送行と状態行_送信待ち");
            yield return new TestCaseData(new PlaytestDeliveryRefused(Counts(3, 0)), "受け口に拒否されました。トークン失効の可能性があります（送信待ち3件）").SetName("配送行と状態行_拒否");
            yield return new TestCaseData(PlaytestDeliveryAllSent.Instance, "すべての報告を送信しました").SetName("配送行と状態行_全件送信済み");
        }

        private static IEnumerable<string> LanguageCodes()
        {
            return LanguageCatalog.Languages.Select(language => language.Code);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts">送信状況 %j と送信可否を同時に表示する</web-test>
        [TestCaseSource(nameof(KnownDeliveries))]
        public void 配送の行と送れない理由の状態行を同じ画面に同時に出す(PlaytestDeliveryStatus delivery, string expected)
        {
            // 確保セッションが無い状態で配送状況を流す
            // Push the delivery status while there is no capture session
            Assert.IsTrue(Localize.TrySetChosenLanguage("japanese"), "日本語へ切り替えられる");
            Type("broken belt");
            View.Apply(new PauseMenuViewModel(PauseMenuPage.BugReport, false, FakePauseMenuModel.Capture(BugReportCaptureStatus.NoSession), delivery, PlaytestExportIdle.Instance));

            // 配送の行と、送れない理由の状態行 1 本が両方出て、送信は押せない
            // Both the delivery line and one status line with the blocking reason show, and send is disabled
            var deliveryLine = Find<Label>(View, "bug-report-delivery");
            Assert.IsTrue(IsShown(deliveryLine));
            Assert.AreEqual(expected, deliveryLine.PlainText());
            var statusLines = View.Query<Label>("bug-report-status").ToList();
            Assert.AreEqual(1, statusLines.Count);
            Assert.AreEqual("記録を確保するためポーズメニューを開き直してください", statusLines[0].PlainText());
            Assert.IsFalse(SendButton().enabledSelf);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts">%s辞書で配送不能件数と復旧案内を出す</web-test>
        [TestCaseSource(nameof(LanguageCodes))]
        public void 各言語の本番辞書で配送不能件数を出し旧い復旧案内を出さない(string languageCode)
        {
            Assert.IsTrue(Localize.TrySetChosenLanguage(languageCode), $"言語 {languageCode} へ切り替えられる");
            ApplyDelivery(new PlaytestDeliveryPending(Counts(0, 2)));

            // 失敗件数が数字で残り、未翻訳キーと旧い再送復旧の案内が出ない
            // The failed count stays as a number, with no untranslated key and no old resend-recovery guidance
            var text = Find<Label>(View, "bug-report-delivery").PlainText();
            StringAssert.Contains("2", text);
            StringAssert.DoesNotContain(UntranslatedKeyPrefix, text);
            StringAssert.DoesNotMatch("再送復旧|recover|Wiederherstellung|재전송 복구", text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts">%s辞書で件数不明を完了や0件にしない</web-test>
        [TestCaseSource(nameof(LanguageCodes))]
        public void 各言語の本番辞書で件数不明を完了や0件にしない(string languageCode)
        {
            Assert.IsTrue(Localize.TrySetChosenLanguage(languageCode), $"言語 {languageCode} へ切り替えられる");
            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxInventoryUnavailable("access denied")));

            // 読めなかった理由が残り、0 件や未翻訳キーにならない
            // The unreadable reason stays, never turning into zero counts or an untranslated key
            var text = Find<Label>(View, "bug-report-delivery").PlainText();
            StringAssert.Contains("access denied", text);
            StringAssert.DoesNotContain("0", text);
            StringAssert.DoesNotContain(UntranslatedKeyPrefix, text);
        }

        private void ApplyDelivery(PlaytestDeliveryStatus delivery)
        {
            View.Apply(new PauseMenuViewModel(PauseMenuPage.BugReport, false, FakePauseMenuModel.Capture(BugReportCaptureStatus.Ready), delivery, PlaytestExportIdle.Instance));
        }

        private static PlaytestOutboxDeliveryCounts Counts(int pending, int failed)
        {
            return new PlaytestOutboxDeliveryCounts(pending, failed);
        }
    }
}
```

前提にした既存 API:
- `BugReportFormScreenTestHost`（View・Type・SendButton、英語で載せる）: `Client.Tests/Ui/Screens/BugReport/BugReportFormScreenTestHost.cs:20-46`
- `PauseMenuViewModel` の ctor: `Client.Ui.State/PauseMenu/PauseMenuViewModel.cs:20`
- `BugReportFormView.Apply`（配送の行は解決済みの文字列）・状態の行は 1 本: `Client.Ui/Screens/BugReport/BugReportFormView.cs:87-93,163-178`
- `BugReportFormText.DescribeDelivery`・`DescribeStatus`（NoSession の文）: `Client.Ui.State/PauseMenu/BugReportFormText.cs:22-34,42-51`
- 配送の型と ctor: `Client.PlaytestReceiver/Delivery/PlaytestDeliveryStatus.cs`（Pending・LocalOnly・Refused・AllSent）、`PlaytestOutboxDeliveryCounts.cs`（Counts・Unavailable）、`PlaytestDeliveryRoute.cs`（PlaytestLocalOnlyReason）
- 欠けたキーは `[!key]` で返る（`ui.bugReport.` を含むので DoesNotContain で捕まる）: `Client.Localization/LocalizationTextResolver.cs:35-39`
- TestCaseSource と SetName の前例: `Client.Tests/Ui/State/BlockInventory/CleanRoom/CleanRoomLogicTest.cs:47-56`。LanguageCatalog で回す前例: `Client.Tests/Localization/Skit/SkitLocalizationRuntimeContentTest.cs:22-29`
- 期待の日本語: `Localization/localization.csv:352,419-425,433`（BugReportFormTextTest.cs:101-106 の期待と同じ）


#### 11. `src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts`「%s辞書でボタン2つと書き出し中の文言が未翻訳キーにならない」
#### 12. 同「%s辞書で完了・欠落・開けなかった案内・失敗の文言が未翻訳キーにならない」

- 置き場: **Modify** `Client.Tests/Ui/Screens/BugReport/BugReportRecoveryControlsViewTest.cs`。`救出ボタンは配送状況で出し分け書き出し中は押せない` の閉じ括弧（起草時 53 行）の直後に下の 3 メンバ（LanguageCodes と 2 テスト）を足す。行番号でなくメソッド名で位置を探す（I-fix6 が同じファイルの 37 行目を `DescribeDelivery(...).Resolve()` へ変えている。取り込みの衝突は両方を採る）
- using の追加: `using System.Collections.Generic;`・`using Client.Localization;`・`using Client.PlaytestReceiver.Recovery;`（既にある）・`using Client.Tests.Ui.TestHost;`・`using Client.Ui.Components.Controls;`・`using Mooresmaster.Localization.Generated;`
- 埋める穴: 全言語。汎用の LocalizationDictionaryCoverageTest は空訳だけを見て、差し込み値が残るかは見ない
- 書き出しの行は起草時は素の Label で `Apply` の時に解決する（`BugReportRecoveryControlsView.cs:49-51`）。I-fix6 の後は `LocalizedLabel` で `SetText(UiText)` し、言語の変更で解き直す（`origin/uitk-lane/i-fix6` の同じファイル。`HasExportLine` で行の有無を決める）。どちらでも言語を切り替えてから `Apply` すれば同じ文が出るので、テストはそのまま通る見込み。読みは `.PlainText()`
- 期待: どちらも PASS（推測）

```csharp
        private static IEnumerable<string> LanguageCodes()
        {
            return LanguageCatalog.Languages.Select(language => language.Code);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">%s辞書でボタン2つと書き出し中の文言が未翻訳キーにならない</web-test>
        [TestCaseSource(nameof(LanguageCodes))]
        public void 各言語の本番辞書で救出ボタン2つと書き出し中の文言が未翻訳キーにならない(string languageCode)
        {
            // 失敗ありの書き出し中で、再送・書き出し・書き出し状況の 3 つを出す
            // Exporting with failures shows all three: resend, export and the export status
            Assert.IsTrue(Localize.TrySetChosenLanguage(languageCode), $"言語 {languageCode} へ切り替えられる");
            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(1, 16)), PlaytestExportRunning.Instance);
            var texts = new[]
            {
                Find<FormButton>(View, "bug-report-resend-failed").Q<Label>().PlainText(),
                Find<FormButton>(View, "bug-report-export-unsent").Q<Label>().PlainText(),
                Find<Label>(View, "bug-report-export-status").PlainText(),
            };

            // どれも空でなく未翻訳キーでもない
            // None is empty or an untranslated key
            foreach (var text in texts)
            {
                Assert.IsNotEmpty(text, languageCode);
                StringAssert.DoesNotContain("ui.bugReport.", text, languageCode);
            }
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts">%s辞書で完了・欠落・開けなかった案内・失敗の文言が未翻訳キーにならない</web-test>
        [TestCaseSource(nameof(LanguageCodes))]
        public void 各言語の本番辞書で書き出しの完了と失敗の文言が差し込み値を保ち未翻訳キーにならない(string languageCode)
        {
            // 完了・欠落・開けなかった案内を 1 行にした文に、差し込み値が全部残る
            // The done, missing and not-revealed sentence keeps every inserted value
            Assert.IsTrue(Localize.TrySetChosenLanguage(languageCode), $"言語 {languageCode} へ切り替えられる");
            var missing = new[] { new PlaytestExportMissing("reports/x/video.mp4", "IOException") };
            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(1, 16)), new PlaytestExportDone("/exports/unsent-reports.zip", 17, missing, false));
            var done = Find<Label>(View, "bug-report-export-status").PlainText();
            StringAssert.Contains("/exports/unsent-reports.zip", done);
            StringAssert.Contains("17", done);
            StringAssert.Contains("reports/x/video.mp4", done);
            StringAssert.Contains("IOException", done);
            StringAssert.DoesNotContain("ui.bugReport.", done);

            // 失敗の文に理由が残る
            // The failure sentence keeps its reason
            ApplyDelivery(new PlaytestDeliveryPending(new PlaytestOutboxDeliveryCounts(1, 16)), new PlaytestExportFailed("desktop folder unavailable"));
            var failed = Find<Label>(View, "bug-report-export-status").PlainText();
            StringAssert.Contains("desktop folder unavailable", failed);
            StringAssert.DoesNotContain("ui.bugReport.", failed);
        }
```

前提にした既存 API:
- 既存の private `ApplyDelivery(delivery, export)`: `BugReportRecoveryControlsViewTest.cs:55-58`
- `BugReportRecoveryControlsView`（ボタンは FormButton、文言は LocalizedLabel、書き出し状況は素の Label）: `Client.Ui/Screens/BugReport/BugReportRecoveryControlsView.cs:28-36,39-52,54-59`。FormButton の中の LocalizedLabel: `Client.Ui/Components/Controls/FormButton/FormButton.cs`（`_label = new LocalizedLabel`）、`LocalizedLabel : Label`: `Client.Ui/Foundation/Text/LocalizedLabel.cs:21`
- `BugReportFormText.DescribeExport`（完了・欠落・開けなかった案内を ` / ` でつなぐ）: `Client.Ui.State/PauseMenu/BugReportFormText.cs:53-62,143-153`
- 書き出しの型と ctor: `Client.PlaytestReceiver/Recovery/PlaytestExportStatus.cs`（Done・Missing・Failed・Running）


#### 13. `src/features/pauseMenu/BugReportFormTests/hostSubmissionLock.test.ts`「応答待ちが終了してもホスト送信中は感想の種別と本文を保護する」
#### 14. `src/features/pauseMenu/BugReportFormTests/productionSendingLabels.test.ts`「${locale}: ${kind}のホスト送信表示は本番CSVを使う」

- 置き場: **Modify** `Client.Tests/Ui/Screens/BugReport/BugReportFormViewTest.cs`
  - 13: 既存メソッド `応答待ちが終わってもホスト送信中は種別と本文を保護する`（63-73 行）を下の `応答待ちが終わってもホスト送信中は種別と本文を保護しreadyで解く` で**置き換える**（既存はホストの Submitting を Apply するだけで、送信を通らない）
  - 14: クラスの末尾（置き換えたメソッドの後）に足す
- using の追加: `using Client.Game.InGame.BugReport.Playtest;`・`using Client.Localization;`・`using Client.Tests.Ui.Screens.PauseMenu;`・`using Client.Tests.Ui.TestHost;`・`using Client.Ui.State.Commands;`・`using Client.Ui.State.Toast;`
- 13 で埋める穴: 送信 → ホスト送信中 → ローカルの応答が終わる、の後もロックが残り、成功のトーストを出さず、ready で解けること。Web はローカルの応答を失敗（`resolveSubmit(false)`）で終えるので、UITK も拒否で終える
- 14 で埋める穴: 本番辞書の文言そのもの（UITK はキーの選び分けだけを見ていた）
- 期待: どちらも PASS（推測）

```csharp
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/hostSubmissionLock.test.ts">応答待ちが終了してもホスト送信中は感想の種別と本文を保護する</web-test>
        [Test]
        public void 応答待ちが終わってもホスト送信中は種別と本文を保護しreadyで解く()
        {
            // 感想を送り、ローカルの応答より先にホストが送信中になる
            // Send impressions; the host turns to submitting before the local reply returns
            simulate.Click(Find<Button>(View, "bug-report-kind-feedback"));
            Type("感想");
            simulate.Click(SendButton());
            View.Apply(Model(BugReportCaptureStatus.Submitting));

            // ローカルの応答が失敗で終わっても、ホスト送信中のロックと文言は残る
            // Even after the local reply ends in failure, the host-submitting lock and label remain
            Commands.CompleteSubmit(new UiCommandRejected(UiCommandRejectReason.CaptureRefused, ""));
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-bug").enabledSelf);
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-feedback").enabledSelf);
            Assert.IsFalse(Description().enabledSelf);
            Assert.IsFalse(SendButton().enabledSelf);
            var statusLines = View.Query<Label>("bug-report-status").ToList();
            Assert.AreEqual(1, statusLines.Count);
            Assert.AreEqual("Writing the impressions…", statusLines[0].PlainText());

            // 送った中身は感想で、成功のトーストは出ない
            // The payload was impressions and no success toast shows
            var submit = Commands.Issued.Single();
            Assert.AreEqual(PauseMenuCommandKind.SubmitBugReport, submit.Kind);
            Assert.AreEqual("感想", submit.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, submit.ReportKind);
            Assert.IsFalse(ShownToasts.Any(toast => toast.Variant == GameUiToastVariant.Info), "ホスト送信中に成功のトーストを出した");

            // ホストの送信が終われば書きかけを編集できる
            // Editing resumes once the host finishes writing
            View.Apply(Model(BugReportCaptureStatus.Ready));
            Assert.IsTrue(Find<Button>(View, "bug-report-kind-bug").enabledSelf);
            Assert.IsTrue(Description().enabledSelf);
            Assert.IsTrue(SendButton().enabledSelf);
            Assert.AreEqual("感想", Draft.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, Draft.Kind);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/productionSendingLabels.test.ts">${locale}: ${kind}のホスト送信表示は本番CSVを使う</web-test>
        [TestCase("english", PlaytestReportKind.Feedback, "Writing the impressions…")]
        [TestCase("english", PlaytestReportKind.Bug, "Writing the bug report…")]
        [TestCase("japanese", PlaytestReportKind.Feedback, "感想を書き出しています…")]
        [TestCase("japanese", PlaytestReportKind.Bug, "バグ報告を書き出しています…")]
        [TestCase("german", PlaytestReportKind.Feedback, "Eindrücke werden geschrieben…")]
        [TestCase("german", PlaytestReportKind.Bug, "Fehlerbericht wird geschrieben…")]
        [TestCase("korean", PlaytestReportKind.Feedback, "감상을 작성하는 중…")]
        [TestCase("korean", PlaytestReportKind.Bug, "버그 신고를 작성하는 중…")]
        public void ホスト送信中の状態行は本番辞書の各言語で種別の文言を出す(string locale, PlaytestReportKind kind, string expected)
        {
            // 辞書を切り替えてからホスト送信中へ移る（Web と同じく辞書の差し替え後の状態遷移を見る）
            // Switch the dictionary, then move to host submitting (the transition after a dictionary swap, as on the Web)
            Draft.SetKind(kind);
            Assert.IsTrue(Localize.TrySetChosenLanguage(locale), $"言語 {locale} へ切り替えられる");
            View.Apply(Model(BugReportCaptureStatus.Submitting));

            var statusLines = View.Query<Label>("bug-report-status").ToList();
            Assert.AreEqual(1, statusLines.Count);
            Assert.AreEqual(expected, statusLines[0].PlainText());
        }
```

前提にした既存 API:
- 送信の流れ（押した時点で `_sending`、応答後に `_sending=false`、拒否なら toaster へ渡して Refresh して戻る）: `Client.Ui/Screens/BugReport/BugReportFormView.cs:107-134`
- ロックは `_sending || capture == Submitting`、状態の行は `DescribeStatus(_capture, _sending, _draft.Kind)`: `BugReportFormView.cs:154-171`、`BugReportFormText.cs:22-34,91-94`
- 拒否は `UiCommandResultToaster.Report` がエラーのトーストにする（成功のトーストは画面側）: `Client.Ui.State/Toast/UiCommandResultToaster.cs`（`Report`）
- `BugReportFormScreenTestHost.Model`・`Description`・`SendButton`: `BugReportFormScreenTestHost.cs:34-46`
- 期待の文言: `Localization/localization.csv:348,353`（Web の期待値と同じ）


#### 15. `src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts`「感想でも欠損時は従来の通知を出す」
#### 16. 同「感想送信中の表示とロックを保ち、送信開始時の種別で完了を伝える」

- 置き場: **Modify** `Client.Tests/Ui/Screens/BugReport/BugReportFormSendViewTest.cs`。クラスの末尾（`送信が拒否されると成功通知を出さず下書きを残す` の後）に 2 つ足す。using の追加は `using Client.Tests.Ui.TestHost;` だけ（`Localize`・`LocalizationKeys`・`GameUiToastVariant`・`PlaytestReportKind`・`UiCommandAccepted` は既にある）
- 15 で埋める穴: 欠損のエラーのトーストは種別バグでしか見ていなかった
- 16 で埋める穴: 送信中に下書きの種別が変わっても、完了は送信開始時の種別で伝えること
- 期待: どちらも PASS（推測）

```csharp
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts">感想でも欠損時は従来の通知を出す</web-test>
        [Test]
        public void 感想でも欠損があれば送信成功時に欠損のエラーのトーストを出す()
        {
            // 欠損ありで感想を送る。状態行が増えて送信ボタンがずれるので 1 フレーム進めてから押す
            // Send impressions with a gap; the extra status line shifts the send button, so advance a frame first
            View.Apply(Model(BugReportCaptureStatus.Ready, "video"));
            simulate.Click(Find<Button>(View, "bug-report-kind-feedback"));
            Type("感想");
            simulate.FrameUpdate();
            simulate.Click(SendButton());
            Commands.CompleteSubmit(UiCommandAccepted.Instance);

            // 感想の完了ではなく欠損のエラーを 1 件出す
            // Show one gap error instead of the impressions completion
            Assert.AreEqual(PlaytestReportKind.Feedback, Commands.Issued.Single().ReportKind);
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts.Single().Variant);
            Assert.AreEqual("Missing: video", ShownToasts.Single().Text);
            Assert.AreNotEqual(Localize.Get(LocalizationKeys.Ui.FeedbackReport.Sent), ShownToasts.Single().Text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts">感想送信中の表示とロックを保ち、送信開始時の種別で完了を伝える</web-test>
        [Test]
        public void 感想の送信中は表示とロックを保ち完了は送信開始時の種別で伝える()
        {
            // 感想を送ると、応答待ちの間は感想の書き出し中の表示とロックが出る
            // Sending impressions shows the impressions writing label and the lock while awaiting the reply
            simulate.Click(Find<Button>(View, "bug-report-kind-feedback"));
            Type("感想");
            simulate.Click(SendButton());
            Assert.AreEqual("Writing the impressions…", Status().PlainText());
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-feedback").enabledSelf);
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-bug").enabledSelf);

            // 応答待ちの間に下書きの種別がバグへ変わっても、完了は感想として伝える
            // Even if the draft kind turns to bug while awaiting, completion is reported as impressions
            Draft.SetKind(PlaytestReportKind.Bug);
            Commands.CompleteSubmit(UiCommandAccepted.Instance);
            var submit = Commands.Issued.Single();
            Assert.AreEqual("感想", submit.Description);
            Assert.AreEqual(PlaytestReportKind.Feedback, submit.ReportKind);
            Assert.AreEqual(GameUiToastVariant.Info, ShownToasts.Single().Variant);
            Assert.AreEqual("Impressions saved to outbox", ShownToasts.Single().Text);
        }
```

前提にした既存 API:
- 押した時点の確保状態と種別で完了を伝える（`captureAtClick`・`kind`）: `Client.Ui/Screens/BugReport/BugReportFormView.cs:117-138`
- `BugReportDraft.SetKind` は世代を進めない（完了後の Reset は走る）: `Client.Ui.State/PauseMenu/BugReportDraft.cs:16,23-26,30-35`
- 欠損の文（英語 `Missing: {items}`）: `BugReportFormText.cs:36-40`、`Localization/localization.csv:351`
- 欠損ありで押す前に 1 フレーム進める前例: `BugReportFormSendViewTest.cs:81-84`
- 感想の完了・書き出し中の英語: `Localization/localization.csv:347-348`

- [ ] **Step 1: 書いて流す**

上の 16 本を書き（テスト 5 は Step 2 の判定の後）、
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.(PauseMenu|Settings|BugReport)\."
```
Expected: `ErrorCount: 0`、テスト 5 以外の 15 本（展開後の全件）が PASS、既存のテストも PASS。置き換える既存の 2 メソッド（テスト 4・13）は、旧メソッドの assert が新メソッドに全部残っていることを `git diff` で見る（死活表の 2 行）。言語の切り替えは `Assert.IsTrue(Localize.TrySetChosenLanguage(..))` で真を主張する（偽のまま前の言語で読んで通ることを防ぐ）。新しいテストは 1 回だけ期待値を壊して FAIL を見てから戻す（例: テスト 1 の期待の文字列を 1 字変える）。PASS しないものは、テストの前提（クリック・スクロール・遷移の同期）の誤りか本番の不具合かを切り分け、テストの誤りなら直し、本番の不具合なら bd（`--parent <Task 0 の ID>`）と `done.md`「未完了」へ書いてそのテストの処遇を Step 2 と同じ判定にかける（このレーンは本番を直さない）

- [ ] **Step 2: テスト 5（画質の連続操作）の判定**

起草時の読みでは、UITK の `GraphicsQualityRowSwitch` は `_current` を `Apply` でしか更新しない（`Client.Ui/Screens/Settings/GraphicsQualityRows/GraphicsQualityRow.cs:58,69,72-76`）。プリセットも行の `_current` を変えない（`GraphicsQualitySettingsView.cs:37`）。モデルが変わらないまま 2 回押すと 1 回目を巻き戻すので、上のテスト 5 は Fake のモデルでは落ちる見込み。本番では `GraphicsQualityPreference.SetSettings` → `SettingsModel.Rebuild` → `PauseMenuView.OnSettingsChanged` → `Apply` が同じ呼び出しの中で回る（`SettingsModel.cs:23,32-35`・`PauseMenuView.cs:94,104-107`）見込みで、そうならモデルが追いつかない間の連続操作は UITK では起きない。
1. テスト 5 を書いて流し、FAIL を確かめる（PASS なら写して終わり）
2. FAIL なら上の 2 か所を読み、送信から `Apply` までが同期（UniTask・次フレーム・Subject の遅延購読が挟まらない）かを確かめる。同期なら、テスト 5 のメソッドを消し、disposition の束 1 のこの行を `捨てる`「UITK は画質の全項目を送り、送った同じ呼び出しの中で SettingsModel から View の Apply まで回るので、モデルが追いつかない間の連続操作が起きない（SettingsModel.cs:23,32-35・PauseMenuView.cs:94,104-107）」に書き換える（D-14）
3. 同期でなければ本番の不具合（連続操作で前の操作が巻き戻る故障シナリオがある）。bd を立て、`done.md`「要裁定」に「画質の連続操作の巻き戻り（本番の修正が要る。設定のレーンが無いので I-fix へ回すか）」を書き、この行は `写す予定`（bd の ID）のまま残す。R-8 の束 1 は 0 にならないので、このときはマージの前に指揮の判定を待つ

- [ ] **Step 3: disposition を直して突き合わせ**

`docs/uitk/disposition.md`「## 処遇」から束 1 の `写す予定` の行（Step 2 の結果に応じて 15 か 16 行）を消す。
```bash
node tools/uitk/test-name-parity.mjs --bundle 1; echo "exit=$?"
```
Expected: 「束 1: … / 写す予定 0 / 未対応 0」・exit=0（Step 2 の 3 の分岐なら写す予定 1）。テスト 13 は「成功（Info）のトーストを出さない」だけを見る（拒否のエラーのトーストは Web も出す: `dispatchAction` が失敗をトーストにする〈`moorestech_web/webui/src/bridge/transport/actions.ts:79`・`src/features/pauseMenu/BugReportForm.tsx:50`〉。UITK は `UiCommandResultToaster.Report` が同じくエラーのトーストにするので差ではなく、差の表にも X-prerun にも回さない）

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/PauseMenu/ moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Settings/ moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BugReport/ docs/uitk/disposition.md
git commit -m "test(uitk): 束 1 の写す予定の Web テストを NUnit へ写す（S5-ladder・albof.21）"
```


---

### Task 4: 束 5 の突き合わせを 0 にする（R-8）

**Files:**
- Modify: `docs/uitk/disposition.md`（「## 処遇」に束 5 の行）
- Modify: 下の表の「写す」の既存 NUnit（`/// <web-test>` の行を足すだけ。メソッドの本体は変えない）

**Interfaces:**
- Consumes: `node tools/uitk/test-name-parity.mjs --bundle 5 --emit-missing`（未対応を `| 5 | \`<file>\` | <name> | 要判断 | 要判断 | — |` の形で出す）
- Produces: 束 5「写す予定 0 / 未対応 0」

起草時の実測（`origin/feature/uitk` 9c83052273）は束 5「Web 528 / 写した 157 / 処遇 6 / 写す予定 0 / 未対応 365」。S5-challenge が 5 件（`e2e/tests/challenge.spec.ts` 3・`src/features/challenge/challengeLogic.test.ts` 2）、S5-gate が 9 件（`src/features/eventLanguageGate/EventLanguageGate.test.ts` 8・`useLanguageSelectionAnswer.test.ts` 1）、S5-crosscut が 22 件（写す 16・捨てる 6）を持ち、残る約 329 件に持ち主が無い。このレーンはその残りを全部処遇する。処遇の規則（disposition.md「## 書式」）: UITK の NUnit が同じことを主張していれば `写す`（既存のテストメソッドの直前に `/// <web-test>` の行を足す。1 つの NUnit が複数の Web テストを写してよい）、UITK に同じものが無ければ `捨てる`（理由）、UITK に挙動があるのに NUnit が無ければこのタスクで NUnit を書く（`写す予定` を残さない。差分に数えられるため。D-7）。

- [ ] **Step 1: 依存レーンの後の残りを出す**

```bash
node tools/uitk/test-name-parity.mjs --bundle 5 --emit-missing > moorestech_client/.uloop/outputs/S5-ladder-parity-b5.txt; echo "exit=$?"
grep -c '^| 5 |' moorestech_client/.uloop/outputs/S5-ladder-parity-b5.txt
grep '^| 5 |' moorestech_client/.uloop/outputs/S5-ladder-parity-b5.txt | awk -F'`' '{print $2}' | sort | uniq -c | sort -rn
```
Expected: exit=1。件数は約 329。`challenge.spec`・`challengeLogic`・`eventLanguageGate/`・`uiState.spec`・`inputLayer.spec`・`textSelection.spec`・`i18n.spec`・`connection.spec`・`allScreensI18n` の行が出たら依存レーンの写し漏れ（持ち主と件数を `done.md`「未完了」に書き、このレーンで同じ規則で処遇する）

- [ ] **Step 2: ファイルごと捨てる行（`*`）を書く**

次のファイルは UITK に同じものが無いので、ファイルごと 1 行（テスト名 `*`）で `捨てる`。`disposition.md`「## 処遇」の末尾へ足す（PR の列は Task 12 で番号を入れる。それまで `—`）:

| Web のファイル（`moorestech_web/webui/` から） | 起草時の件数 | 理由（「証跡のパスか理由」の列にそのまま書く） |
|---|---|---|
| `src/bridge/` 配下の 29 ファイル（`contract/`・`transport/`・`store/`・`playtest/` の `*.test.ts`。一覧は Step 1 の出力の `src/bridge/` の行） | 203 | Web と C# の間の WS/HTTP の契約・検証・購読・ストア・DOM 照会の応答。UITK は同じプロセスで `Client.Ui.State` のモデルを直に読み、通信層を持たない（設計書 A-13）。UITK 側の DSL の照会は `Client.Tests/Ui/Dsl/` が持つ。切替で bridge ごと消える |
| `e2e/mock-host/tests/httpHandler.test.ts` | 4 | Web の検証基盤（mock-host の HTTP 応答）。UITK は mock-host を使わない |
| `e2e/mock-host/tests/spaceElevatorConnect.test.ts` | 3 | mock-host の模擬サーバーの挙動。UITK は本物のサーバーにつなぎ、mock-host を使わない |
| `e2e/tests/reference/captureReadiness.spec.ts` | 6 | Web の参照キャプチャの撮影の準備の検査。UITK の撮影は `UiRuntimePanelCapture`・`PlaytestGameViewOps`（compare-procedure.md §3） |
| `e2e/uitk-reference/captureLifecycle.test.ts` | 2 | 同上（Web の撮影ツールの後始末と manifest） |
| `src/shared/i18n/localizationKeysFreshness.test.ts` | 13 | TS の生成物 `localizationKeys.ts` の鮮度と CSV の構文解析。C# 側は SourceGenerator が毎ビルド CSV から生成し古くならない |
| `src/shared/i18n/provider/dictionaryRetry.test.ts` | 6 | 辞書の HTTP 取得の再試行。UITK は辞書を同じプロセスで同期に読み、取得が無い（設計書 A-38） |
| `src/shared/i18n/provider/dictionaryGeneration.test.ts` | 1 | 同上（取得の世代の入れ替え） |
| `src/shared/i18n/noJsxVisibleLiteral.test.ts` | 5 | JSX の見える文字の lint。UITK の同じ規則は UXML 側の `Client.Tests/Ui/Text/UxmlLiteralTextTest` が持ち、JSX の構文は無い |
| `src/app/App.architecture.test.ts` | 2 | React の App の構成の制約（辞書エラーのリロード・画面固有のクロームを持たない）。UITK は辞書エラーを作らず（設計書 A-38）、層の向きは `Client.Tests/Ui/Conventions/UiLayerDependencyDirectionTest` が持つ |

- [ ] **Step 3: 1 件ずつ処遇するファイル**

次のファイルは 1 件ずつ見る。各 Web テストの本文（`moorestech_web/webui/<file>`）と候補の NUnit を読み、同じことを主張していれば候補のメソッドの直前に `/// <web-test file="<file>"><Web のテスト名そのまま></web-test>` を足す。主張していなければ、UITK に挙動があるなら同じディレクトリの既存クラスへ NUnit を足し（200 行を超えるなら新しいクラス）、UITK に無ければ「## 処遇」に 1 件 1 行で `捨てる`（理由）を書く:

| Web のファイル | 起草時の件数 | 候補の NUnit（`CT/` から） | 捨てる理由の型 |
|---|---|---|---|
| `src/shared/uiState/uiScreenRouting.test.ts` | 15 | `Ui/Routing/UiScreenRoutingTest`（同名の 8 本と、`%s の … 可否は %s` の 6 本は `全画面の性質がWebの表と一致する`）・`Ui/Routing/UiScreenTraitsStageLeftPaddingTest` | 無い見込み |
| `src/shared/uiState/activeLayer.test.ts` | 17 | `Ui/State/Routing/UiActiveLayerTest`（modal・blockInventory・research・game・buildMenu の層）、`Foundation/Input` の規則のテスト（`grep -rln "GameUiInputRules\|WheelPassthroughMark\|IsTextInputFocused" CT/Ui` で探す） | DOM 固有（`input type`・`contenteditable` の判定・React ハンドラへの伝播）は「DOM の要素の種類の判定。UITK の文字入力は `TextField` のフォーカスだけで決まる」 |
| `src/app/zLayerTokens.test.ts` | 6 | `Ui/Layers/UiLayerOrderTest`: 層序の 5 本（背面層・常駐 HUD とツールチップ・ポーズ・モーダルとトースト・全画面ゲートと再接続）は `CompositeStackingKeyIncreasesInEnumOrder`（Web の `--z-*` の値を UiLayer の並びで隣どうし全部比べ、Web の各対の大小を含む）、`stageとディムの層序は生値でなくトークンを参照する` は `EveryLayerHasTokenMapping` | 無い見込み |
| `src/app/iconTextOutlineDesign.test.ts` | 3 | 前例: 同じファイルの 3 件は既に `Ui/Components/Slots/Fluid/FluidSlotLookTest`・`FluidAmountSlotTest` が写している。残りは `Components/Slots/OutlinedCountLabel` の見た目のテスト（`grep -rln "OutlinedCountLabel" CT/Ui`）と `Ui/Theme/UssTokenValueTest` | CSS の擬似要素の構造は「CSS の共有クラスの数の規則で、UITK は `OutlinedCountLabel` 1 つが縁を持つ」 |
| `src/shared/i18n/useI18n.test.ts` | 14 | `Localization/Resolution/LocalizationTextResolverTest`（現在 → 英語 → 原文 → 目印の順と空訳）・`Formatting/LocalizationTextInterpolatorTest`（名前付きの差し込み・未知の差し込みを残す）・`LocalizeTest`（`GetLegacyReturnsMissingMarkerForUnknownRawKey`） | 辞書の世代・初回の取得失敗・未初期化の状態は「React の辞書の取得状態。UITK は起動時に同期で辞書を持ち、取得の状態が無い（A-38）」 |
| `src/shared/i18n/itemName/useItemName.test.ts` | 8 | `Ui/Content/GameContentPresentationTest`・`LocalizeContentTest`（Guid から対象言語の名・英語への落とし） | 「マスタ未着」「resolver の参照の維持」は「UITK にマスタ未受信の状態が無い（A-13）・React の再描画の最適化」 |
| `src/shared/i18n/contentKeys.test.ts` | 5 | `Localization/Resolution/ContentLocalizationKeysTest`（`<namespace>.<小文字 D 形式 guid>.<field>`） | TS の barrel の再輸出・型の和は「TS のモジュール構成」 |
| `src/shared/i18n/diagnostics/translationWarnings.test.ts` | 3 | `LocalizeTest`・`LocalizationTextResolverTest` の警告のテスト（`grep -rn "LogAssert\|Warning" CT/Localization`） | 再描画ごとの重複抑止は「React の再描画で同じ警告が繰り返される問題で、UITK は解決を描画のたびに呼ばない」 |
| `src/shared/tutorialAnchor/anchorIds.test.ts` | 4 | `Ui/Anchor/AnchorIdsWebParityTest`（`StaticIdsMatchWeb`・`DynamicPrefixesMatchWeb`・`GeneratorsMatchWebFormats`） | 空白の拒否が無ければ NUnit を足す（`AnchorIds` の工場に空白が入らないことは UITK にもある規則） |
| `src/shared/tutorialAnchor/resolveAnchor.test.ts` | 4 | `Ui/Anchor/TutorialAnchorRegistryTest`（Unique・Duplicate・Missing・複数 id）。面積 0 は `TutorialAnchorResolution` の種類を見て、あれば NUnit を足す | 無い見込み |
| `src/shared/tutorialAnchor/tutorialAnchor.test.ts` | 3 | `TutorialAnchorRegistryTest.ElementWithTwoIdsResolvesUniqueByEither`（空白区切りの複数 id） | DOM の属性と CSS セレクタの組み立ては「DOM の属性の書式。UITK はクラスで印を付ける」 |
| `src/shared/ui/IconButton/index.test.ts` | 2 | `Ui/Components/Controls/IconButtonTest`（既定の × の図形・押下） | aria-label・残余 props は「DOM の属性の受け渡し」 |
| その他 Step 1 の出力に残ったファイル | — | 同じ手順で候補を `grep -rln` で探す | 理由を書く |

- [ ] **Step 4: 0 を見る**

```bash
node tools/uitk/test-name-parity.mjs --bundle 5; echo "exit=$?"
node --test tools/uitk/testNameParity/tests/*.test.mjs
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.(Ui|Localization)\."
```
Expected: 「束 5: … / 写す予定 0 / 未対応 0」・exit=0、自己テスト PASS、`ErrorCount: 0`、テスト全 PASS（印を足しただけのテストは本体が変わらないので今まで通り。新しく書いた NUnit は FAIL→PASS を確かめてから入れる）。`写す`・`捨てる`・新しく書いた NUnit の件数を `done.md` へ

- [ ] **Step 5: コミット**

```bash
git add docs/uitk/disposition.md moorestech_client/Assets/Scripts/Client.Tests/
git commit -m "test(uitk): 束 5 の Web テストの残りを処遇し突き合わせを 0 にする（S5-ladder）"
```

---

### Task 5: 出展モードの運転役を汎用にする（R-2）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/exhibition-mode-run.sh`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-event-language-gate.sh`

**Interfaces:**
- Consumes: `Client.Playtest.Boot.PlaytestEventLanguageGateBoot.ArmForNextBoot()`・`.Disarm()`（S5-gate。`public static string`）、`Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem)`、`Client.Localization.Localize.TrySetChosenLanguage(string)`、`SK/scripts/run-scenario.sh <project> <scenario.cs>`（終了コード 0/1/2）
- Produces: `exhibition-mode-run.sh <project> <GameUi|Web> <scenario.cs>`（終了コードは `run-scenario.sh` のもの。引数の誤りは 2、武装の戻り値が `armed` でなければ 1）。シナリオは選ぶ前の言語を `<project>/PlaytestResults/scratch/event-language-gate/language-before.txt` に書き、戻したら消す（S5-gate の取り決めのまま）

- [ ] **Step 1: 実物の運転役を読む**

`git show origin/feature/uitk:.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-event-language-gate.sh` を読み、下の `exhibition-mode-run.sh` と後始末・武装の判定を突き合わせる。下のコード片は S5-gate 計画（Task 7 Step 3）の検査後の形（言語の復元は EDC の戻り値の語 `language-restored`／`language-restore-failed` で判定し、戻せたときだけ控えを消す）に、武装の判定（戻り値の語 `armed`）を足したもの。実物がこれより進んだ形なら実物を写し、実物が終了コードで判定する古い形なら下の形を採る（後退させない）。どちらを採ったかを `done.md`「証拠」に 1 行

- [ ] **Step 2: 汎用の運転役を書く**

`exhibition-mode-run.sh`（`chmod +x`）:
```bash
#!/bin/bash
# 出展モードで起動するシナリオの運転役: 全体スイッチを指定の系にし、次の起動 1 回だけを出展モードにしてから run-scenario.sh を前景で流す
# 出展モードの環境変数は Editor のプロセスに要るので、Unity の中の起動口（PlaytestEventLanguageGateBoot）が Play の間だけ立てる
# シナリオは選ぶ前の言語を PlaytestResults/scratch/event-language-gate/language-before.txt に書き、戻したら消す（残っていればここで戻す）
# Driver for scenarios that boot in exhibition mode: switch to the given UI system, arm exhibition mode for the next boot only, then run run-scenario.sh in the foreground
# The exhibition env vars must live in the Editor process, so the in-Unity boot entry (PlaytestEventLanguageGateBoot) sets them only during Play
# The scenario writes the pre-choice language to PlaytestResults/scratch/event-language-gate/language-before.txt and deletes it once restored (restored here if left)
#
# usage: exhibition-mode-run.sh <unity-project-path> <GameUi|Web> <scenario.cs>
set -u
if [ "$#" -ne 3 ]; then echo "usage: exhibition-mode-run.sh <unity-project-path> <GameUi|Web> <scenario.cs>"; exit 2; fi
PROJECT="$1"
SYSTEM="$2"
SCENARIO="$3"
case "$SYSTEM" in
  GameUi|Web) ;;
  *) echo "NG: UI system must be GameUi or Web (got $SYSTEM)"; exit 2 ;;
esac
[ -f "$SCENARIO" ] || { echo "NG: scenario not found: $SCENARIO"; exit 2; }
HERE="$(cd "$(dirname "$0")" && pwd)"
SKILL_DIR="$(cd "$HERE/../../.." && pwd)"
SCRATCH="$PROJECT/PlaytestResults/scratch/event-language-gate"
mkdir -p "$SCRATCH"

# 終わり方によらず武装を解き、シナリオが言語を戻せずに落ちていたら選ぶ前の言語へ戻す
# However this ends, disarm; if the scenario died before restoring the language, put the pre-choice language back
on_exit() {
  uloop execute-dynamic-code --project-path "$PROJECT" --code 'return Client.Playtest.Boot.PlaytestEventLanguageGateBoot.Disarm();' >/dev/null \
    || echo "ERROR: disarm failed; run Client.Playtest.Boot.PlaytestEventLanguageGateBoot.Disarm() via execute-dynamic-code"
  [ -f "$SCRATCH/language-before.txt" ] || return 0
  local code
  code=$(cat "$SCRATCH/language-before.txt")
  echo "WARN: the scenario left the language changed; restoring $code"
  # 戻せたかは EDC の戻り値の語で見る（終了コードは EDC が走ったかだけを表す）。戻せたときだけ控えを消す
  # Judge the restore by the word EDC returns (the exit code only says EDC ran); delete the note only on success
  local restored
  restored=$(uloop execute-dynamic-code --project-path "$PROJECT" --code "return Client.Localization.Localize.TrySetChosenLanguage(\"$code\") ? \"language-restored\" : \"language-restore-failed\";")
  if echo "$restored" | grep -q "language-restored"; then
    rm -f "$SCRATCH/language-before.txt"
  else
    echo "ERROR: language restore to $code failed; language-before.txt kept ($SCRATCH): $restored"
  fi
}
trap on_exit EXIT

# 武装できたかも戻り値の語で見る（PlayMode が残っていると "ERROR: already playing" を返し、終了コードは 0 のまま）
# Judge arming by the returned word too (a lingering PlayMode returns "ERROR: already playing" with exit code 0)
armed=$(uloop execute-dynamic-code --project-path "$PROJECT" --code "Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.$SYSTEM); return Client.Playtest.Boot.PlaytestEventLanguageGateBoot.ArmForNextBoot();")
if echo "$armed" | grep -q "ERROR" || ! echo "$armed" | grep -q "armed"; then
  echo "NG: could not switch to $SYSTEM or arm exhibition mode (stop PlayMode first): $armed"
  exit 1
fi
bash "$SKILL_DIR/scripts/run-scenario.sh" "$PROJECT" "$SCENARIO"
```

`uitk-event-language-gate.sh` を次の全文に置き換える（呼び方 `uitk-event-language-gate.sh <project>` は不変）:
```bash
#!/bin/bash
# uitk-event-language-gate.cs の運転役: GameUi で出展モードの運転役（exhibition-mode-run.sh）へ委ねる
# Driver for uitk-event-language-gate.cs: delegate to the exhibition-mode driver (exhibition-mode-run.sh) on GameUi
#
# usage: uitk-event-language-gate.sh <unity-project-path>
set -u
if [ "$#" -ne 1 ]; then echo "usage: uitk-event-language-gate.sh <unity-project-path>"; exit 2; fi
HERE="$(cd "$(dirname "$0")" && pwd)"
exec bash "$HERE/exhibition-mode-run.sh" "$1" GameUi "$HERE/uitk-event-language-gate.cs"
```

- [ ] **Step 3: 確かめる**

```bash
bash .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/exhibition-mode-run.sh ./moorestech_client; echo "exit=$?"
bash .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/exhibition-mode-run.sh ./moorestech_client Classic x.cs; echo "exit=$?"
.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-event-language-gate.sh ./moorestech_client; echo "exit=$?"
```
加えて、PlayMode を手で起こした状態（`uloop control-play-mode --project-path ./moorestech_client --action play`）で `exhibition-mode-run.sh ./moorestech_client GameUi <uitk-event-language-gate.cs のパス>` を流し、`exit=1` と `NG: could not switch … ERROR: already playing` の行を見てから PlayMode を止める（武装できないまま通常の起動で流れない対照）。
Expected: 1 本目と 2 本目は `exit=2`（Unity に何も送らない）。3 本目は `exit=0`・`result.json` が `Success: true`・Note の `系=GameUi`（S5-gate の録画が呼び方を変えずに通る）。続けて `uloop execute-dynamic-code --project-path ./moorestech_client --code 'return Client.Localization.Localize.GetCurrentLanguageCode();'` が流す前の言語のまま、`ls moorestech_client/PlaytestResults/scratch/event-language-gate/` に `language-before.txt` が無い

- [ ] **Step 4: コミット**

```bash
chmod +x .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/exhibition-mode-run.sh
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/exhibition-mode-run.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-event-language-gate.sh
git commit -m "test(uitk): 出展モードの運転役を系とシナリオを引数に取る形にし、言語ゲートの録画はそれへ委ねる（S5-ladder）"
```

---

### Task 6: ラダー 6 `uitk-challenge-gate`（R-1）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-challenge-gate.cs`

**Interfaces:**
- Consumes: Task 5 の運転役、`GameInitializedEvent.OnGameInitialized`（UniRx）、`GameUiRoot.Instance.GetRootElement()`、`WebUiHost.Hub`、`ChallengeProgressState`（`Categories`・`IsUnlocked`・`Category.CategoryGuid`・`Category.Challenges`・`CurrentChallenges`・`ChallengeGuid`。S5-challenge の `uitk-challenge-list.cs` と同じ引き方）、`Localize.GetCurrentLanguageCode()`・`TrySetChosenLanguage(string)`、DSL（`UntilUiElement`・`ClickUi`・`Until`・`SetupDebugEnvironment`・`SkipOpeningSkitIfPlaying`・`ExitToGameScreen`・`PressKey`・`WaitUiState`・`Screenshot`）
- Produces: `result.json`・mp4・スクショ 3 枚（`01-event-language-gate`・`02-challenge-list-japanese`・`03-after-close`）

- [ ] **Step 1: シナリオを書く（200 行未満）**

```csharp
// ラダー 6 uitk-challenge-gate: Web ホスト無しの UITK で、出展モードの言語ゲートを日本語で通り、チャレンジ一覧を T で開いて閉じる
// Ladder 6 uitk-challenge-gate: on UITK with no web host, pass the exhibition language gate in Japanese, then open and close the challenge list with T
// 実行は stage5/exhibition-mode-run.sh <project> GameUi <この .cs> から（全体スイッチと出展モードの武装を済ませてから run-scenario.sh を流す）
// Run via stage5/exhibition-mode-run.sh <project> GameUi <this .cs> (it sets the switch and arms exhibition mode before run-scenario.sh)
// ゲートは DI より前の 1 回きりで無操作はアプリ終了になりゲートへ戻らないので、plan §4.4 の語順と逆にゲートを先に通る
// The gate shows once before DI and idling quits the app instead of returning to it, so this passes the gate first, reversing plan §4.4's wording
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Challenge;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

return PlaytestRunner.Run("uitk-challenge-gate", new PlaytestRunOptions { Record = true }, async p =>
{
    // 1. 系の主張と入力の準備（背景 Editor でも注入入力が届くよう焦点無視）
    // 1. Assert the UI system and prepare input (ignore focus so injected input reaches a background Editor)
    p.Note($"系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが GameUi");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    var initialized = false;
    var initializedSubscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => initialized = true);

    // Web ロケータのログは最初から、エラーは初期化の完了から数える（DI の前の毎フレームの NRE は 2026-08-28 裁定で許容）
    // Web-locator logs count from the start and errors from initialization completion (per-frame NREs before DI are allowed by the 2026-08-28 ruling)
    var errorsAfterInitialization = new List<string>();
    var webLocatorLogs = new List<string>();
    Application.logMessageReceived += CountLog;
    try
    {
        // 2. DI より前の言語ゲート。選ぶまで初期化は止まっている
        // 2. The pre-DI language gate; initialization holds until a choice
        await p.UntilUiElement("event-language-gate", 60f);
        var root = GameUiRoot.Instance.GetRootElement();
        p.Assert(!initialized, "言語を選ぶまで初期化が完了しない");
        await p.Screenshot("01-event-language-gate");

        // 選ぶ前の言語を控え、途中で落ちても運転役が戻せるようにする
        // Note the pre-choice language so the driver can restore it even if this dies midway
        var languageBefore = Localize.GetCurrentLanguageCode();
        var languageBeforePath = LanguageBeforePath();
        Directory.CreateDirectory(Path.GetDirectoryName(languageBeforePath));
        File.WriteAllText(languageBeforePath, languageBefore);

        // 3. 日本語を押すとその場で外れ、初期化が完了する
        // 3. Pressing Japanese removes the gate at once and initialization completes
        await p.ClickUi("event-language-gate-option-japanese");
        await p.Until(() => root.Q("event-language-gate") == null, 5f, "押したその場でゲートが外れる");
        await p.Until(() => initialized, 180f, "選択の後に初期化が完了する");
        p.Assert(Localize.GetCurrentLanguageCode() == "japanese", "日本語で始まる");

        // 4. ゲーム画面まで進み、Web ホストが起きていないことを見る
        // 4. Reach the game screen and check that no web host started
        await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
        await p.SkipOpeningSkitIfPlaying();
        await p.ExitToGameScreen();
        p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（hostless）");

        // 5. 解放済みの先頭カテゴリと最初の進行中を源から引く（uitk-challenge-list と同じ引き方）
        // 5. Take the first unlocked category and its first current challenge from the source (same as uitk-challenge-list)
        var progress = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ChallengeProgressState>();
        var category = progress.Categories.First(c => c.IsUnlocked);
        var current = category.Category.Challenges.First(c => category.CurrentChallenges.Contains(c));
        p.Note($"カテゴリ {category.Category.CategoryGuid}・進行中 {current.ChallengeGuid}");

        // 6. T で一覧を開き、枠・先頭カテゴリのボタン・進行中のカードが UITK のロケータで解ける
        // 6. Open the list with T; the frame, first category button and current card resolve through the UITK locator
        await p.PressKey(Key.T);
        await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
        await p.UntilUiElement("challenge-panel", 5f);
        await p.UntilUiElement($"challenge-category-{category.Category.CategoryGuid}", 5f);
        await p.UntilUiElement($"challenge-node-{current.ChallengeGuid}", 5f);
        var cardTexts = root.Q($"challenge-node-{current.ChallengeGuid}").Query<Label>().ToList().Select(PlainText).Where(text => !string.IsNullOrEmpty(text)).ToList();
        p.Note($"進行中のカードの文字 {string.Join(" / ", cardTexts)}");
        p.Assert(cardTexts.Count > 0, "進行中のカードに文字が出る");
        await p.WaitSeconds(1f);
        await p.Screenshot("02-challenge-list-japanese");

        // 7. T で閉じてゲーム画面へ戻る
        // 7. Close with T and return to the game screen
        await p.PressKey(Key.T);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        await p.Screenshot("03-after-close");

        // 言語を戻せたときだけ控えを消す。戻せなければ控えを残し、運転役がもう一度戻して結果を出す
        // Delete the note only when the language came back; otherwise keep it so the driver retries and reports
        var languageRestored = Localize.TrySetChosenLanguage(languageBefore);
        p.Assert(languageRestored, $"選ぶ前の言語 {languageBefore} へ戻せる");
        if (languageRestored) File.Delete(languageBeforePath);
    }
    finally
    {
        // 途中の時間切れで抜けても購読を残さない（前例 uitk-machine-ui.cs）
        // Never leave the subscriptions behind, even when a timeout exits midway (precedent uitk-machine-ui.cs)
        Application.logMessageReceived -= CountLog;
        initializedSubscription.Dispose();
    }

    // 8. 初期化の後のエラーと Web ロケータのログが 0 件（完了条件「UITK 側で Web ロケータが一度も要素を返さない」の補助の証拠。主の証拠は locator の行と Hub == null）
    // 8. Zero errors after initialization and zero Web-locator logs (supporting evidence for the exit criterion; the main evidence is the locator line and Hub == null)
    p.Note($"初期化の後のエラー {errorsAfterInitialization.Count} 件・Web ロケータのログ {webLocatorLogs.Count} 件");
    p.Assert(errorsAfterInitialization.Count == 0, $"初期化の後の Error/Exception が 0 件（最初: {errorsAfterInitialization.FirstOrDefault()}）");
    p.Assert(webLocatorLogs.Count == 0, $"[PlaytestUi] Web のログが 0 件（最初: {webLocatorLogs.FirstOrDefault()}）");

    #region Internal

    void CountLog(string condition, string stackTrace, LogType type)
    {
        if (initialized && (type == LogType.Error || type == LogType.Exception)) errorsAfterInitialization.Add(condition);
        if (condition.StartsWith("[PlaytestUi] Web")) webLocatorLogs.Add(condition);
    }

    string LanguageBeforePath()
    {
        return Path.Combine(Application.dataPath, "..", "PlaytestResults", "scratch", "event-language-gate", "language-before.txt");
    }

    // LocalizedLabel の表示文は行送りの印を前に付ける（I-fix5）ので、原文から印を外して読む
    // LocalizedLabel's display text carries a line-height tag (I-fix5), so read the source with tags dropped
    string PlainText(Label label)
    {
        var text = label is LocalizedLabel { SourceText: not null } localized ? localized.SourceText : label.text;
        return Regex.Replace(text ?? string.Empty, "<[^>]+>", string.Empty);
    }

    #endregion
});
```

`LocalizedLabel.SourceText` は I-fix5 が足す（`origin/uitk-lane/i-fix5` の `Client.Ui/Foundation/Text/LocalizedLabel.cs`）。Task 0 Step 1 で I-fix5 が未マージなら、ローカル関数 `PlainText` は `label.text` から印を外すだけにする（I-fix5 の前は行送りの印も付かない）。

- [ ] **Step 2: 流す**

PlayMode を止めた状態で:
```bash
uloop clear-console --project-path ./moorestech_client
.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/exhibition-mode-run.sh ./moorestech_client GameUi .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-challenge-gate.cs; echo "exit=$?"
uloop get-logs --project-path ./moorestech_client --log-type All --search-text "[PlaytestUi]" --max-count 200
```
Expected: `exit=0`、`result.json` が `Success: true`、Note に `系=GameUi`・「初期化の後のエラー 0 件・Web ロケータのログ 0 件」、mp4 とスクショ 3 枚。get-logs に `[PlaytestUi] locator reads only the GameUi UI` が 1 行以上・`[PlaytestUi] Web` が 0 行。落ちたら、まず運転役の出力を見る: `NG: could not switch … or arm` が出ていれば運転役の側（PlayMode が残っていた等。`uloop control-play-mode --action stop` の後に流し直す）で、依存レーンへは回さない。`ERROR: language restore` が出ていれば控えが残っているので、出た言語へ戻してから次を流す。運転役が通っていて落ちたら: ゲートが出ない → S5-gate の Task 8 Step 1 の切り分け（`[EventLanguageGate]`・`[UiSrgbPanelTarget]` のログ）。T で一覧が出ない → `uitk-challenge-list.cs` を GameUi で流して同じく落ちるかを見て、落ちるなら S5-challenge の不具合として bd（`--parent <Task 0 の ID>`）と `done.md`「未完了」へ（このレーンで画面を直さない）。初期化の後のエラーが出たら、1 行目を `done.md` に書き、出所のレーンへ bd で回す（シナリオの Assert は緩めない）。**どの分岐でも、緑にならないまま先へ進めない**: R-1 が満たせないので、bd を立てて `done.md` の 1 行目を「状態: 停止（ラダー 6 が赤: <理由>）」にし、`cmux wait-for -S uitk-s5-ladder` を送って待つ。出所のレーンか I-fix の直しが `feature/uitk` に入ったら取り込んで流し直す。未完了のままマージしない

- [ ] **Step 3: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-challenge-gate.cs
git commit -m "test(uitk): ラダー 6 uitk-challenge-gate（出展モードの言語ゲート → チャレンジ一覧）を足す（S5-ladder）"
```

---

### Task 7: 台帳の全行の照合（R-7）

**Files:** なし（`done.md`「台帳の照合」だけ。台帳の表の列は変えない〈G1 の読み手を守る〉）

- [ ] **Step 1: 行を並べて実体を引く**

```bash
sed -n '/^## 行/,/^## 追従記録/p' docs/uitk/TODO.md | grep '^| ' | tail -n +2 | awk -F' \\| ' '{print $1" | "$3}' | sed 's/^| //'
```
Expected: 行名と束の一覧（起草時 49 行）。各行について UITK の実体を `git ls-files` で確かめ、`done.md`「台帳の照合」に `| 行 | 束 | UITK の実体（ディレクトリかファイル） | 確かめたコマンド |` か `| 行 | 束 | 作らない | 理由（設計書 A-n・disposition の行・台帳の agent 既定） |` で書く。起草時の対応の見込み（実物で確かめてから書く）:

| 台帳の行 | 見込みの実体（`moorestech_client/Assets/Scripts/` から） |
|---|---|
| pauseMenu・settings・toast・commonHud・keyHint・modeHud・progress・trainHud・notification・skit・CursorTooltip・buildMenu・hotbar・modal・inventory・recipe・blockInventory・research | `Client.Ui/Screens/<PauseMenu・Settings・Toast・CommonHud・KeyHints・ModeHud・Progress・TrainHud・Notification・Skit・CursorTooltip・BuildMenu・Hotbar・Modal・Inventory・Recipe・BlockInventory・Research>/` と `Client.Ui.State/` の同名 |
| blockInventory 特殊族: ロケット・宇宙エレベーター | `Client.Ui/Screens/BlockInventory/Special/` |
| challenge: CurrentChallengeHud・challenge: ChallengePanel | `Client.Ui/Screens/Challenge/CurrentHud/`・`Client.Ui/Screens/Challenge/`（S5-challenge）と `Client.Ui.State/Challenge/` |
| eventLanguageGate | `Client.Ui/Screens/EventMode/`・`Client.Ui.State/EventMode/`（S5-gate） |
| tutorial: KeyControlHintHud・WorldPinOverlay・DragGuide・TutorialOverlay | `Client.Ui/Screens/Tutorial/<KeyControlHint・WorldPin・DragGuide・Overlay>/` |
| tutorialAnchor | `Client.Ui/Foundation/Anchor/` |
| treeView | `Client.Ui/Components/NodeGraph/NodeGraphView/` |
| shared/ui | `Client.Ui/Components/` |
| placementTarget・itemMove・ownedCounts | `Client.Ui.State/PlacementTarget/`・`Client.Ui.State/ItemMove/`・所持数の読み手（`grep -rln "OwnedCount" moorestech_client/Assets/Scripts/Client.Ui.State`） |
| pointerGesture | `Client.Ui/Foundation/Input/Gesture/`・`Foundation/DragDrop/` |
| i18n | `Client.Ui/Foundation/Text/`・`Client.Localization/` |
| uiState | `Client.Ui.State/Routing/` |
| uiScale | `Client.Ui/Foundation/Stage/ScaleReading/` |
| オーバーレイ: 8 行（背景スキット・Ctrl+U・カットシーン中の退避・背面の通知層・カーソル追従・常駐 HUD の可否・マウント点と背景ディム・再接続/辞書エラー/エラー境界） | `Client.Ui.State/SkitPresence/`・`Client.Ui.State/WholeUiVisibility/`・`Client.Ui/Screens/WholeUiVisibility/`・`Client.Ui/Foundation/Layers/`（BehindStage）・`Client.Ui/Foundation/Tooltip/`・`Client.Ui/Screens/Shell/`。再接続と辞書エラーは「作らない（A-13・A-38）」、エラー境界は S5-crosscut の例外の受け皿 |
| bridge（通信・契約・ストア） | 作らない（A-13。UITK は同じプロセスのモデルを直に読む。disposition の束 5 の bridge の行） |
| アプリ殻（起動・トークン・層） | `Client.Ui/Foundation/Root/GameUiRoot.cs`・`Foundation/Theme/`・`Foundation/Layers/` |
| Web の検証基盤（mock-host・Playwright・撮影） | `Client.Playtest/`（DSL）・`Client.Ui/Editor/Capture/`（撮影）。mock-host は作らない |

空欄が残る行（UITK の実体が見つからず「作らない」の根拠も無い）が出たら、その行を `done.md`「要裁定」に書く（完了条件「台帳の全行が UITK で作られ」を満たさない。指揮の判定を待ち、マージは進めない）

- [ ] **Step 2: コミットは無し**（`done.md` は Task 12 でまとめてコミット）

---

### Task 8: 参照キャプチャの鮮度（R-12）

**Files:**
- Modify: `docs/uitk/TODO.md`「## 参照キャプチャ」（末尾に 1 行）

- [ ] **Step 1: Web の変化を見る**

```bash
ls ../moorestech_logs/uitk/reference-captures/ | sort | tail -4
git log --oneline 8a585f742a..origin/master -- moorestech_web/webui/src/features/challenge moorestech_web/webui/src/features/eventLanguageGate moorestech_web/webui/src/shared/ui/treeView moorestech_web/webui/src/shared/ui moorestech_web/webui/src/app moorestech_web/webui/e2e/mock-host/topics moorestech_web/webui/e2e/uitk-reference | wc -l
```
Expected: 最新は `web-8a585f742a/`・`web-8a585f742a-2560x1080/`（`5-challenge-list.png`・`5-event-language-gate.png` がある）。件数 0 なら撮り直さない。1 以上なら compare-procedure.md §2 の手順で master の worktree（`moores-wt new` ではなく `git -C <master の作業コピー>`。手順は同 §2）から `capture.ts` を 1280×720 と 2560×1080 で流し、`web-<origin/master の 10 桁>/`・`-2560x1080/` を置く（Web の録画には `moorestech_web/node` の複製と `pnpm install` が要る。S3-ladder の依頼）

- [ ] **Step 2: 台帳に 1 行**

「## 参照キャプチャ」の末尾に `- 束 5 の最新の参照（S5-ladder）: 撮影元 <8a585f742a か撮り直しの sha>（段階 5 の 2 画面の Web の src は origin/master <10 桁> まで<不変／変化あり・撮り直し>）` を足してコミット:
```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 束 5 の最新の参照を台帳に書く（S5-ladder）"
```

---

### Task 9: 段階 5 の並べ比べと独立評価（R-11）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-stage5-compare.cs`
- 段階 5 の USS・台帳「## 見た目の例外」は触らない（[A-179]: 直すか例外かは指揮が判定し、行と印は I-fix が置く。D-9）

**Interfaces:**
- Consumes: Task 5 の運転役、`PlaytestGameViewOps.SetResolution(CancellationToken, int, int)`、`PlaytestUiElementOps.FindUi(string)`（`IsFound`）、`PlaytestOverlayView`（`Client.Playtest.Overlay`）、参照 `5-event-language-gate`・`5-challenge-list`（Task 8）、組 0 = S5-challenge・S5-gate の報告ページ `https://review.moores.tech/pr/<番号>/report/`（番号は各 `harness/uitk/<レーン>/done.md` の PR URL）
- Produces: `uitk-5-event-language-gate-{1280x720,2560x1080}`・`uitk-5-challenge-list-{…}`・`web-live-5-…`（4 枚 × 2 系）、`done.md`「差の表」

撮る状態: 参照の fixture（`moorestech_web/webui/e2e/uitk-reference/referenceShots.ts:114-115` の `5-challenge-list`〈topic `challengeActive`・`ChallengeList`〉と `5-event-language-gate`〈topic `eventLanguageGateWaiting`〉）は英語なので、組 2 はゲートで `english` を選ぶ（ラダー 6 は日本語。D-10）。カードの中身・カテゴリの数・地の違いは差に数えない。

- [ ] **Step 1: 撮影シナリオを書く（200 行未満）**

```csharp
// 段階 5 の並べ比べ（組 2）: 出展モードで起動し、言語ゲートとチャレンジ一覧を同じ実ゲームの状態で 1280×720 と 2560×1080 に撮る。全体スイッチの両系で同じ手順が通る（S5-ladder）
// Stage 5 comparison (set 2): boot in exhibition mode and shoot the language gate and challenge list in the same real game state at 1280x720 and 2560x1080; the same steps pass on both UI systems (S5-ladder)
// 実行は stage5/exhibition-mode-run.sh <project> <GameUi|Web> <この .cs> から。参照の fixture が英語なのでゲートでは英語を選ぶ
// Run via stage5/exhibition-mode-run.sh <project> <GameUi|Web> <this .cs>; the reference fixtures are English, so English is chosen at the gate
using System.IO;
using Client.Game.Common;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

// 撮るのは静止画だけなので録画しない（操作オーバーレイは撮る前に消す）
// Only stills are needed, so no recording (the operation overlay is hidden before each shot)
var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 400f };
return PlaytestRunner.Run("uitk-stage5-compare", options, async p =>
{
    // 1. 系を記録し、撮影名の頭を系で分ける。入力の準備
    // 1. Record the system and prefix shot names by it; prepare input
    var system = UiInputExclusivity.ActiveUiSystem;
    var shotPrefix = system == UiSystem.GameUi ? "uitk" : "web-live";
    p.Note($"系={system}");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    var initialized = false;
    var initializedSubscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => initialized = true);

    // 2. DI より前のゲートを 2 解像度で撮る
    // 2. Shoot the pre-DI gate at both resolutions
    await p.UntilUiElement("event-language-gate", 60f);
    await MoveCursorOffPanelsAsync();
    await ShootBothResolutionsAsync("5-event-language-gate");

    // 選ぶ前の言語を控え、英語を選んで初期化を待つ
    // Note the pre-choice language, choose English and wait for initialization
    var languageBefore = Localize.GetCurrentLanguageCode();
    var languageBeforePath = Path.Combine(Application.dataPath, "..", "PlaytestResults", "scratch", "event-language-gate", "language-before.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(languageBeforePath));
    File.WriteAllText(languageBeforePath, languageBefore);
    await p.ClickUi("event-language-gate-option-english");
    await p.Until(() => initialized, 180f, "選択の後に初期化が完了する");
    await p.WaitSeconds(1f);
    p.Assert(!(await PlaytestUiElementOps.FindUi("event-language-gate")).IsFound, "選択の後はゲートが無い");

    // 3. ゲーム画面から T でチャレンジ一覧を開き、中央寄せの動きが止まってから撮る
    // 3. Open the challenge list with T from the game screen and shoot once centering settles
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
    await p.UntilUiElement("challenge-panel", 5f);
    await p.WaitSeconds(1f);
    await MoveCursorOffPanelsAsync();
    await ShootBothResolutionsAsync("5-challenge-list");

    // 4. 閉じて言語を戻す。戻せたときだけ控えを消し、戻せなければ控えを残して運転役に戻させる
    // 4. Close and restore the language; delete the note only on success, else keep it for the driver to retry
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    var languageRestored = Localize.TrySetChosenLanguage(languageBefore);
    p.Assert(languageRestored, $"選ぶ前の言語 {languageBefore} へ戻せる");
    if (languageRestored) File.Delete(languageBeforePath);
    initializedSubscription.Dispose();

    #region Internal

    async UniTask ShootBothResolutionsAsync(string shotId)
    {
        // 参照と同じ 2 つの解像度で撮り、1280×720 へ戻す
        // Shoot at the reference's two resolutions and return to 1280x720
        await ShootAsync(shotId, 1280, 720);
        await ShootAsync(shotId, 2560, 1080);
        await SetResolutionAsync(1280, 720);
    }

    async UniTask ShootAsync(string shotId, int width, int height)
    {
        // 操作オーバーレイを消してから撮る（参照に無いものを写さない）
        // Hide the operation overlay before shooting (keep things the reference lacks out of the shot)
        await SetResolutionAsync(width, height);
        foreach (var overlay in Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        await p.WaitSeconds(0.3f);
        await p.Screenshot($"{shotPrefix}-{shotId}-{width}x{height}");
    }

    async UniTask SetResolutionAsync(int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await p.WaitSeconds(1.5f);
        p.Assert(Screen.width == width && Screen.height == height, $"Screen が {width}x{height} (実際 {Screen.width}x{Screen.height})");
    }

    async UniTask MoveCursorOffPanelsAsync()
    {
        // カーソルを画面の左端の中ほど（どのボタンにも掛からない）へ置き、ホバーの無い状態にする
        // Put the cursor mid-left of the screen (over no button) so nothing is hovered
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
```
名前空間は起草時の実物: `PlaytestEnvironmentConfig` は `Client.Playtest`（`Client.Playtest/PlaytestEnvironmentConfig.cs:3`）、`PlaytestUiElementOps` は `Client.Playtest.Operations`（`Operations/PlaytestUiElementOps.cs:7`。`FindUi(string)` は :20）、`PlaytestGameViewOps` は `Client.Playtest.Operations.Ui`（`Operations/Ui/PlaytestGameViewOps.cs:10`）、`PlaytestOverlayView` は `Client.Playtest.Overlay`、`GameInitializedEvent` は `Client.Game.Common`（`Client.Game/Common/GameInitializedEvent.cs:4`）。`p.Until(Func<bool>, float, string)` は `PlaytestDriver.cs:177`（シナリオ側のラムダで、本番コードに `Func<>` を足すものではない）。

- [ ] **Step 2: 両系で撮る**

```bash
.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/exhibition-mode-run.sh ./moorestech_client GameUi .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-stage5-compare.cs; echo "exit=$?"
.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/exhibition-mode-run.sh ./moorestech_client Web .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-stage5-compare.cs; echo "exit=$?"
```
Expected: 両方 `exit=0`・Note の系がそれぞれ。各 run dir のスクショ 4 枚を `moorestech_client/.uloop/outputs/compare/S5-ladder/` へ写す。Web の回で CEF が出なければ（`uloop get-logs --log-type All --search-text "RendererUnavailable"` が出る）、その行を証拠に「Web 側の組 2 は未撮影」と `done.md` に書き、参照と組 0 と組 2 の GameUi で判定する

- [ ] **Step 3: 評価役 2 体に別々に判定させる（本体が派遣する）**

参照 4 枚（`web-<sha>/5-event-language-gate.png`・`5-challenge-list.png` と `-2560x1080` の同名）・組 0（S5-challenge・S5-gate の報告ページの Web／UITK の組。読めなければ各 `harness/uitk/<レーン>/done.md` の差の表）・組 2（GameUi と Web の 8 枚）を並べた簡易 HTML を scratchpad に作り、opus の subagent を 2 体、同じ依頼文で別々に起こす（互いの結果を見せない。subagent は Editor を起動しない）。渡すもの: ゴールの原文「WebUIと同じ見た目で、適切な設計、コンポーネント化されたUITKベースのUIを開発すること」、画像のローカルパスと撮影元 sha、`docs/uitk/TODO.md`「## 見た目の例外」、設計書 §3.3・§3.4、compare-procedure.md §5、S5-crosscut の `done.md`「差の表」の S5-ladder へ回した行。依頼:「色・寸法・配置・装飾の有無・文字の大きさと太さと段組の差は参照と組 2 の GameUi で判定する（世界の地・カードの中身・カテゴリの数の違いは差に数えない）。組 2 の GameUi と Web は同じ実ゲームの状態の差と、崩れ（はみ出し・重なり・欠け）・同時に出る HUD と一覧の重なりを見る。記帳済みの LX に当たる差はその ID を書いて除く。各差に座標か画素の値を添える」。報告は scratchpad のファイルへ、返答は 3 行

- [ ] **Step 4: 再現して処理の案を書く**

2 体の指摘を合わせ、別の subagent（opus）に PNG の画素・寸法を測らせて再現できたものだけ残す（[A-179]）。残った差ごとに処理の案を書く: Web へ寄せる直し（差のあるファイル・USS の行・値）か、例外の文面（LX-207〜LX-209 の番号で「画面・部品／差／同じにしない理由／推奨案」。§3.4 の入れてよい条件を満たすものだけ）。このレーンは USS を直さず、台帳の LX の行とコードの印を置かない。全行を `done.md`「差の表」に「観点・参照・UITK・判定・処理の案」で書き、`done.md`「要裁定」に「指揮の判定を待つ並べ比べの差 n 件（差の表の LX 案）」の 1 行（0 件なら書かない）。段階 5 の 2 画面以外の差（同時に出る HUD 等）は X-prerun へ回す行として「integrator への依頼」へ

- [ ] **Step 5: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/uitk-stage5-compare.cs
git commit -m "test(uitk): 段階 5 の 2 画面を両系の同じ状態で撮る並べ比べのシナリオを足す（S5-ladder）"
git push
```
撮影の PNG は報告ページへ載せた後に消す。

---

### Task 10: 最終の HEAD でラダー 6 本・段階 5 の録画・Client.Tests を通す（R-3・R-4・R-10・R-13）

Task 11 の自動適用の後と Task 12 Step 2 の取り込みの後にも、触れた経路の分を流し直す。

- [ ] **Step 1: テスト**

```bash
git push
winunity compile <レーンのブランチ>
winunity test-all <レーンのブランチ>
winunity test-all feature/uitk
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 1500
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SwitchedUiElementLocatorTest"
```
Expected: `winunity compile` が成功。test-all の落ちた名前の集合が、`feature/uitk` の test-all の落ちた名前の集合に含まれる（このレーン由来の落ち 0。差があれば、その名前を `winunity test <branch> --regex '<名前>'` で両方のブランチで流し直し、土台でも落ちるなら環境由来、落ちなければこのレーンで直す）。件数と突き合わせを `done.md` へ。Mac の 2 本は全 PASS。`winunity test-all` が無ければ（S2-ladder の口が未マージ）Mac で `^Client\.Tests\.` を 3 つに分けて流す（`Ui\.`・`(UIState|Input|Localization|Anchor)\.`・それ以外）

- [ ] **Step 2: ラダー 6 本と Web ロケータのログ**

ラダー 1 の前に受け口の差し替え（Global Constraints の EDC）を立てる。1〜5 は 1 本ごとに次を回し（`<ladder>` は Task 0 Step 2 のパス）、6 は Task 6 Step 2 の 3 行を回す:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'
uloop clear-console --project-path ./moorestech_client
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client <ladder>; echo "exit=$?"
uloop get-logs --project-path ./moorestech_client --log-type All --search-text "[PlaytestUi]" --max-count 200
uloop get-logs --project-path ./moorestech_client --log-type Error --max-count 50
```
Expected: 6 本とも `exit=0`・`Success: true`・ErrorLogs 0（ラダー 6 はシナリオの主張。初期化の完了までの NRE は除く）。各回の get-logs に `[PlaytestUi] locator reads only the GameUi UI` が 1 行以上・`[PlaytestUi] Web` が 0 行・Error 0。ラダー 1 の後に、`bash .agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で出た作業セーブの `save.json` を `stat -f "%Sm" <save.json>` で読み、更新時刻が流し始めた時刻より後。結果を `done.md`「証拠」に 1 本 1 行（パス・流した HEAD の sha・exit・ErrorLogs・locator 行の数・Web 行の数）。1 本でも赤なら R-3・R-4 を満たさない: 緑にならない録画がある間は、bd を立てて `done.md` の 1 行目を「状態: 停止（<録画名> が赤: <理由>）」にし、`cmux wait-for -S uitk-s5-ladder` を送って待つ。出所のレーンか I-fix の直しが `feature/uitk` に入ったら取り込んで該当分を流し直す。未完了のままマージしない

- [ ] **Step 3: 段階 5 に触る録画（GameUi）**

同じ手順で `stage5/uitk-challenge-list.cs`・`stage5/uitk-challenge-hud-resident.cs`・`misc/walk-while-menus-open.cs`・`misc/ui-state-transition-matrix.cs`・`uitk-tutorial-cues.cs`（Task 0 Step 2 のパス）を、`stage5/uitk-event-language-gate.sh ./moorestech_client` を流す。Expected: 全部 `Success: true`・locator の行の条件は Step 2 と同じ。`misc/` の 2 本が段階 5 の前から GameUi で落ちていたら（S4-gamma の全録画の表 `../moorestech_logs/harness/uitk/S4-gamma/done.md` で引く）、同じ落ちなら「既知」と書き、新しい落ちならこのレーンの原因かを切り分ける（このレーンは本番を触らないので、出所のレーンへ bd）。「既知」以外の赤は R-10 を満たさない: 緑にならない録画がある間は、bd を立てて `done.md` の 1 行目を「状態: 停止（<録画名> が赤: <理由>）」にし、`cmux wait-for -S uitk-s5-ladder` を送って待つ。出所のレーンか I-fix の直しが `feature/uitk` に入ったら取り込んで該当分を流し直す。未完了のままマージしない

- [ ] **Step 4: Web 側の退行**

```bash
rg -l "challenge-panel|challenge-node|challenge-category|event-language-gate|ChallengeList" .agents/skills/unity-playmode-recorded-playtest/scenarios --glob '!uitk/**'
```
Expected: 出た Web の録画（起草時の見込み: `misc/walk-while-menus-open.cs`・`misc/ui-state-transition-matrix.cs` ほか）を `winunity play <branch> <scenario.cs> --ui-system Web` で流し（Web の録画は `moorestech_web/node` の複製と `pnpm install` が前提）、S4-gamma の全録画の Web の回の合否と比べて退行 0。CEF が出なければ RendererUnavailable を証拠に「未確認」。`winunity play --ui-system` が無ければ Mac で `UiSystem.Web` を立てて流す。結果を `done.md` の表に 1 本 1 行

- [ ] **Step 5: 片付け**

`bash .agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で出たこのレーンのワールドを消す。コミットは無し（記録のみ）

---

### Task 11: moores-code-review で全ブランチレビュー（必須）

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。base は `origin/feature/uitk`。設計判断は agent 既定で推奨案を採り、判断記録に「agent 既定（moores-code-review Dn）」と書く（ゴール・裁定から外れるものだけ `done.md`「要裁定」）。自動適用・反映の後は、Task 6・Task 9 の録画と Task 10 Step 2・Step 3 の録画のうち、反映が触れた経路のものを反映後のバイナリで流し直してから完了とする（実行計画 §4.3 の 1）。

---

### Task 12: feature/uitk への PR・取り込み・マージ（必須）

セッション終了可能状態にすること: pr-createスキルでPRを作成し、コンフリクトがあれば解消・コンパイル確認のうえpushする。このレーンは実行計画 §4.3 の形（base=feature/uitk、agent がマージ）。

- [ ] **Step 1: PR**: pr-create で `base=feature/uitk`。報告ページ `https://review.moores.tech/pr/<番号>/report/` にラダー 6 の録画・並べ比べ（参照・組 0・組 2 の両系）・台帳の照合の表を、載せ先の生成器の書式（`~/hermes-agent/data/services/pr-review/publish/README.md` の manifest の group／caption）で載せ、本文の先頭行をその URL の h1 にする。公開スクリプトが失敗したら報告 URL 無しで完了にしない。disposition の PR の列の `—` を番号に置き換えてコミット・push
- [ ] **Step 2: 取り込み**: `git merge origin/feature/uitk`。共有区画の衝突は両方の行を採る。push → `uloop compile`・`uloop run-tests ... "^Client\.Tests\.Ui\."`・`node tools/uitk/test-name-parity.mjs --bundle all`（exit 0）・G1 ローカル（全束 未記帳 0。取り込みで新しい Web のコミットが入ったら Task 1 Step 2 の型で記帳）。`git diff --stat <Task 10 で流した sha> HEAD -- moorestech_client/Assets .agents/skills/unity-playmode-recorded-playtest` が空でなければ（衝突の有無によらず）Task 10 Step 2・3 を流し直し、新しい sha で `done.md`「証拠」を書き直す（R-3）。空なら流した sha と空の結果を `done.md` に書く
- [ ] **Step 3: G1 とマージ**: PR の `UITK Web follow gate (G1)` が pass を見て `gh pr merge <番号> --merge`（Squash 禁止）
- [ ] **Step 4: 片付け**: `moores-wt status` で自分の Editor だと確かめて Editor を閉じる。`winunity editor-stop <name>`（使ったなら）。録画・PlaytestResults・TestResults・撮影 PNG を消す。bd moorestech-albof.21 を「Task 3 で 16 件を写した」の reason で close、Task 0 の bd を close。worktree の `moores-wt rm` は指揮が行う（依頼文の指示。D-11）
- [ ] **Step 5: 報告と合図**: `done.md` の 1 行目を「状態: 完了」（止まったら「状態: 停止（理由）」）にし、完了条件ごとの証拠（R-1〜R-13 の表）・PR URL・台帳の照合・差の表・未完了・要裁定・integrator への依頼（申し送りの処遇の「回す」行・下の「設計書・実行計画へ反映する行」・X-prerun への引き継ぎ）を書いて `../moorestech_logs` でコミット・push。最後に `cmux wait-for -S uitk-s5-ladder` を 1 回

## Self-Review

- Requirements の各行のタスク: R-1→T6、R-2→T5、R-3・R-4・R-10・R-13→T10（R-4 はラダー 6 のシナリオ自身も主張）、R-5→T2、R-6→T1（と T12 Step 2 の再確認）、R-7→T7、R-8→T4（と T3）、R-9→T3、R-11→T9、R-12→T8、R-14→各タスクと T12 Step 5、R-15→T11・T12
- 保留の経路: 依存レーンの未マージ・ラダーの欠けは「状態: 停止（理由）」と合図で止まる（黙って 5 本で締めない）。ラダー 6 本・段階 5 の録画のどれかが赤（R-1・R-3・R-4・R-10。出所が依存レーンでも）、G1 に記帳できない Web の変更が残る（R-6）、束 1 の写す予定が残る（D-14 の 3）も同じく「状態: 停止（理由）」で合図して待ち、取り込みの後に該当分を流し直す（Task 12 の G1 は未記帳を赤にしないので、マージを止めるのはこの停止だけ）。Web の回で CEF が出ないときは RendererUnavailable の証拠付きで「未確認」。画面の不具合は直さず bd と `done.md`「未完了」。G1 の追従漏れは記帳で 0 にせず bd。台帳の照合で空欄が残れば「要裁定」でマージしない。運転役の引数の誤りは終了コード 2 で Unity に何も送らない
- 最小構成: ラダー 6 は日本語の言語・一覧の開閉・ログの 0 件だけを主張し、中央寄せ・アンカー・パンは `uitk-challenge-list.cs`（S5-challenge）に任せる。並べ比べのシナリオは撮るだけで主張は解像度とゲートの消えだけ
- 対照: Task 2 は新しいテストを 1 回だけ落ちる形（印の集合から 1 つ除く）で FAIL させてから戻す。Task 5 は引数の誤り 2 通りで `exit=2` を見てから正しい呼び方を流す。Task 4 の新しい NUnit は FAIL→PASS を見てから入れる
- 型の一致: `ChallengeProgressState` の引き方（`Categories.First(c => c.IsUnlocked)`・`Category.Challenges`・`CurrentChallenges`・`ChallengeGuid`・`Category.CategoryGuid`）は S5-challenge 計画の `uitk-challenge-list.cs` と同じ。`PlaytestEventLanguageGateBoot.ArmForNextBoot()`・`Disarm()`・`language-before.txt` のパスは S5-gate 計画 Task 7 と同じ。Task 0 Step 3 でマージ後の実物と照合する
- 番号: A-249・LX-207〜LX-209 のほかを使っていない
- Task 3 のコードは下書きの段でコンパイル・実行していない（Editor を起動しない起草のため）。Step 1 で全件を流し、前提（クリック・スクロール・遷移の同期・`TestCaseData.SetName` の日本語）が外れたらテスト側を直す。テスト 5 だけは本番の挙動で落ちる見込みがあり、Step 2 の判定と D-14 に分けた

## 判断記録（ADR）

従う裁定: `.decisions/2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md`・`2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md`・`2026-11-06-UITK段階2以降の画面もWeb版と同じ見た目にする.md`・`2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`・`2026-11-06-UITK切替までCEF由来の不具合は直さない.md`・`2026-11-06-UITK一気切替の日にCEFとWebを同時に撤去する.md`・`2026-11-06-CEF停止の記録はUITK長命ブランチから先にmasterへ切り出す.md`・`2026-08-28-出展モードの言語選択はロード完了後の全画面ゲートにする.md`・`2026-08-28-言語ゲートの無人復帰穴は別タスクへ送りNREは許容する.md`。依頼文の指示: Codex を使わない・Mac Editor 1 本と winunity・問いは agent 既定・worktree の片付けは指揮。

- D-1（agent 既定）: §5 の独立評価は opus の subagent 2 体（Codex の代わり。前例 S3-ladder D-1・S2-ladder D-1）
- D-2（agent 既定・設計書 A-249 の本文。plan §4.4 の文言の改めを integrator へ依頼）: ラダー 6 は「出展モードの言語ゲートを通る → チャレンジ一覧を開く」の順にする。plan §4.4 の語順（一覧 → ゲート）は通せない: ゲートは DI より前に 1 回だけ出て（2026-08-28 裁定・S5-gate の A-242）、出展モードの無操作は `EventIdleQuitWatcher` のアプリ終了でゲートへ戻らない。棄却: 一覧を通常の起動で見てから出展モードで再起動する 2 本立て（1 本のシナリオで PlayMode を 2 回起こせず、ラダーが 7 本になる）
- D-3（agent 既定）: 「UITK 側で Web ロケータが一度も要素を返さない（ログ）」は、(1) `SwitchedUiElementLocator` が ctor で出す `[PlaytestUi] locator reads only the GameUi UI`（選んだ系だけに委ねる。L0 `SwitchedUiElementLocatorTest` が構造を固定）、(2) `WebUiElementLocator` だけが出す `[PlaytestUi] Web …` の行が 0、(3) `WebUiHost.Hub == null`（Web ロケータは Hub が無いと Unavailable しか返さない）の 3 つで示す。ラダー 6 は (2) をシナリオの中で `Application.logMessageReceived` で数えて主張し、他の録画は `uloop get-logs` で見る。棄却: Web ロケータに「返した」ログを足す（本番の DSL を変えるレーンになり、X-5a で消える物に手を入れる）
- D-4（agent 既定）: 出展モードの運転役は S5-gate の `uitk-event-language-gate.sh` の中身を `exhibition-mode-run.sh <project> <系> <scenario.cs>` へ移し、元の名前は委ねる 1 行にする（呼び方は不変）。ラダー 6・並べ比べ・S5-gate の録画の 3 本が同じ後始末を通る。S5-gate の「起動口を流用しファイルを足さない」は起動口（C#）についての申し送りで、運転役の bash の重複を避けるのはこれと両立する。棄却: ラダー 6 用に同じ 40 行の運転役を複製する（後始末の直しが 2 か所に割れる）。S5-gate の運転役の第 2 引数を任意にする（引数の有無で挙動が変わる。デフォルト引数禁止の趣旨）
- D-5（agent 既定）: `TutorialAnchorCoveragePending.cs` は空を確かめてから消し、`TutorialAnchorCoverageTest` の 1 本目を「全アンカーが UITK で印」へ書き換える。完了条件「空」は表が 0 行であることで満たし、消すことで以後の画面のアンカーの印の付け忘れを表へ逃がせなくする（切替後に Web の印が消えると表の意味も無くなる）。棄却: 空の表を残す（使い手の無い規則が残る）
- D-6（agent 既定）: 段階 5 の L3 は作らない。完了条件と plan §2.0 に L3 は無く、一覧の載り先は S5-crosscut の `GameUiScreenMountTest`、ゲートは DI より前の実起動でしか出ずラダー 6 と S5-gate の録画が見る。S5-crosscut D-14 の「段ごとの L3 の数え上げ」にはこの理由で答える。棄却: `EditModeInPlayingTest/Ui/Stage5/` を作る（一覧の載りは GameUiScreenMountTest と重なり、ゲートは EditModeInPlayingTest の起動経路で出せない）
- D-7（agent 既定）: 束 5 の未対応の残り（約 329 件。持ち主のレーン無し）はこのレーンが全部処遇する。`写す予定` は差分に数えられるので残さず、UITK に挙動があるのに NUnit が無いものはこのレーンで書く。bridge・mock-host・Web の撮影・TS の生成物・辞書の HTTP 取得はファイルごと `捨てる`（理由は A-13・A-38 と UITK の対応物）。棄却: 持ち主の画面のレーンへ戻す（段階 5 の後に画面のレーンは無く、X-prerun の前提「全束の突き合わせ差分 0」が満たせない）
- D-8（agent 既定）: 出展モードの 3 本は Mac の Editor で運転役から流し、`winunity play` では流さない（`winunity play` は run-scenario.sh の前に系の切替しか挟めず、出展モードの武装〈SessionState〉を同じ Editor のセッションへ入れる口が無い）。X-prerun の全録画の一覧にはこの 3 本を運転役経由と書いて渡す
- D-9（agent 既定・前例 S2-ladder D-14）: 段階 5 の並べ比べは設計書 [A-179] に従い、両系で撮る組を足し、差は処理の案と LX-207〜LX-209 の文面だけを書いて指揮の判定へ回す（USS を直さず、台帳の LX の行と印を置かない）
- D-10（agent 既定）: 並べ比べの組 2 はゲートで英語を選ぶ（参照の fixture の文言が英語。日本語で撮ると字形と幅の差が全部差に出る）。ラダー 6 は日本語（plan §4.4 のラダー 1 と同じく、多言語で通ることを命綱で見る）
- D-11（agent 既定）: §4.3 の 5（`moores-wt rm`）は依頼文の「worktree の片付けは指揮が行う」に従い、このレーンは Editor を閉じるまで
- D-12（agent 既定）: 束 5 の暗転の起点は S5 の 3 レーンの分岐点で取り込み済みの master のうち最も古いもの（起草時の見込み `0e81bbe3c3`。台帳「束のレーンを始めるとき」の規則を、締めのレーンが束を代表して当てる。前例 S2-ladder D-5）。束 5 の画面の Web の変更で UITK 側に写しが無いものは記帳で 0 にせず bd で画面のレーンへ回す
- D-14（agent 既定・条件付き）: 束 1 の「topicを固定したまま連続操作とpreset直後に%sを送る」は、UITK の画質の行が `_current` を `Apply` でしか更新しないため Fake のモデルでは前の操作を巻き戻す。本番で送信から `Apply` までが同じ呼び出しの中で回ると実物で確かめたら、モデルが追いつかない状態が起きないので `捨てる`（理由に file:line）。回らなければ本番の不具合として bd と「要裁定」に上げ、このレーンでは直さない（本番コードを書かないレーン）。棄却: View が送った値を持つように本番を変える（設定のレーンの範囲で、故障シナリオが立たないなら要らない変更）
- D-13（agent 既定）: 「台帳の全行が UITK で作られ」は台帳の列を足さず（G1 のパーサを守る）、`done.md` の照合の表で行ごとに実体のパスか「作らない」の根拠を示す。空欄が残れば完了条件を満たさないので「要裁定」に書きマージしない
- unityプレイ録画テスト: 含む（Task 5・6・9・10）

## 設計書・実行計画へ反映する行（integrator への依頼の案。このレーンは書かない）

- A-249: ラダー 6 `uitk-challenge-gate` の置き場 `scenarios/uitk/stage5/` と中身（ゲート → 一覧の順。D-2）、出展モードの運転役 `stage5/exhibition-mode-run.sh <project> <系> <scenario.cs>`（D-4・D-8）、Web ロケータのログの示し方（D-3）、`TutorialAnchorCoveragePending` の削除（D-5）
- plan §4.4 のラダー 5 の行を「出展モードの言語ゲートを通る → チャレンジ一覧を開く」に改める（D-2）
- 実行計画 §1.4 の S5-ladder 行の「`TutorialAnchorCoveragePending.cs` … が空」に「→ 空を確かめて消し、`TutorialAnchorCoverageTest` が全アンカーの印を直に見る」を足す（D-5）。「codex-audit」を「独立評価（opus 2 体）」に読み替えた記録
- 実行計画 §1.5 の X-prerun 行に「出展モードの 3 本は `exhibition-mode-run.sh` 経由・Mac の Editor で」を足す（D-8）
