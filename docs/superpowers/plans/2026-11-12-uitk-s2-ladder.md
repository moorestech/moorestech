# UITK S2-ladder（段階 2 の締め: ラダー `uitk-opening-hud`・wwxy の束 2 の 4 本・`challenge.current-hud` の録画・L3・束 2 の暗転と突き合わせ・波の並べ比べの独立評価）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** S2 の 5 レーン（S2-hud #1613・S2-mode-tooltip #1611・S2-tutorial #1627・S2-notification #1649・S2-skit #1653）が `feature/uitk` に入った後、全体スイッチ UITK（GameUi・Web ホスト無し）で「開幕スキットを送る → 目標 HUD の枠 → 歩いて伐採（ゲージと獲得通知）→ 数字キーで選んで設置」をラダー 2 `uitk-opening-hud` として固定し、段階 2 の HUD の結線を L3 で押さえ、wwxy の束 2 の 4 本を今のマスタで直し、束 2 を暗転にして突き合わせ差分 0・G1 未記帳 0・波の並べ比べの独立評価を残して `feature/uitk` へ取り込む。

**Architecture:** integrator のレーン（`int`）。Mac の Editor は使わず、compile・テスト・録画はすべて Windows（RTX2060）の `~/bin/winunity` で push 済みのブランチに対して回す。新しく書くのは主に「検査する側」（L3 テスト 1 本・録画シナリオ 2 本〈ラダーと並べ比べ〉・壊れた録画 3 本の書き直し）で、本番コードは検査で実測した不具合と並べ比べで再現した差だけを直す。L3 は段階 1 の `PauseMenuGameUiTest`（`EditModeInPlayingTest/Ui/`）の型、ラダーは `uitk-skit.cs`・`uitk-hud.cs`・`uitk-tutorial-cues.cs`・`stage3/uitk-build-bp.cs` の手順の写し、並べ比べは S3-ladder Task 9 の 2 本立て（組 1 = EditMode の `UiRuntimePanelCapture`、組 2 = PlayMode は崩れの確認だけ）。

**Tech Stack:** Unity 6 / UI Toolkit（`Client.Ui`・`Client.Ui.State`）、NUnit（EditModeInPlayingTest）、プレイテスト DSL（`Client.Playtest`・`.agents/skills/unity-playmode-recorded-playtest`）、Node（`tools/uitk/test-name-parity.mjs`）、Python（`.github/scripts/check-uitk-web-follow.py`）、bash/PowerShell（`~/bin/winunity`・`~/bin/winunity.d/*.ps1`。git 管理外のマシン固有ツール）、bd。

設計の出所: 実行計画 `docs/uitk-migration/execution/execution-plan.md` §1.4 の S2-ladder 行（完了条件の原文:「ラダー `uitk-opening-hud` を作り、ラダー 1〜2 が UITK 側で緑。wwxy の `tutorial-*` 3 本と `server-notification-smoke` を直す。`challenge.current-hud` のハイライトの録画。L3 1 本（段階 2 の HUD が UITK スイッチで出る・消える）。束 2 の突き合わせ差分 0（他の束のレーンへ「写す予定」で回した件〈S2-hud 計画 D-13: `alwaysOnHudWheel.test.ts` の 2 件〉は除く。除いた件は受け手のレーンの完了条件に入る）、G1 束 2 で 0。codex-audit（画像付き）で波の並べ比べ」）・§4.3・§5・§6.2、統合計画 `docs/uitk-migration/plan.md` §2.0・§2.3（段階 2 の完了条件）・§4.4（ラダー 2 の定義）、設計書 `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4・§4.3（L3 の定義 :379「段ごとに 1 本」）、`.agents/skills/ref-uitk-design/references/compare-procedure.md`、台帳 `docs/uitk/TODO.md`「## 束」「## 追従記録」「## 参照キャプチャ」「## 壊れた録画の割り振り」「## 見た目の例外」。申し送り: `../moorestech_logs/harness/uitk/{S2-hud,S2-mode-tooltip,S2-tutorial,S2-skit,S2-notification,S3-ladder}/done.md`・`conductor.md`。

起草時の実測（調査 subagent 3 本。scratchpad `research/{ladder2,wwxy,l3-parity-compare}.md`）:
- ピン先マスタ（moorestech_master `e0c913033c`）の先頭チャレンジは `fb529cac`「原木を3個入手する」（startedActions に `100_start_game`〈normal〉と `200_star_background`〈background〉、tutorials に mapObjectPin earnItem 原木 `719845cb` と uiHighLight `challenge.current-hud` `a77333f4`）。2 番目は `90a98c1f`「木の板を3枚作る」（uiHighLight `recipe.craft-button`・itemViewHighLight 木の板・keyControl Tab）。小石・石器・装備のチャレンジは無い（ADR 0038）。開始時に石の斧を装備している（items.json `initialEquipmentItems`）
- `100_start_game` に選択肢（selection コマンド）は 0 件。selection を持つスキットは `sample_short` だけで、`uitk-skit-choice-pause-cursor.cs` が受け持つ
- 石の斧の 1 打で原木 2〜8 個（3 個以上の確率 15/16。計算）。最初の伐採でほぼ先頭チャレンジが終わり、枠とピンが消える
- `node tools/uitk/test-name-parity.mjs --bundle 2` = `束 2: Web 115 件 / 写した 111 / 処遇 4 / 写す予定 0 / 未対応 0`（exit 0）。D-13 の 2 件は S3-hotbar-modal・S4-inventory が写し済み
- G1: 束 2 は台帳で `Web` のため数えられていない。起点 `0e81bbe3c3` で暗転にすると未記帳 18 件（束 3 の追従記録の 18 行と同じ sha の集合）。別に束 1 に 5 件・束 3 に 1 件の未記帳がある（全部 `localizationKeys.ts` の再生成）
- `server-notification-smoke.cs` は HEAD で研究をマスタから選び、GameUi で `notification-host` を UITK のロケータで解決している（d89696d271 以降差分なし）
- Web 版 `moorestech_web/webui/src` は参照の撮影元 `8a585f742a` から origin/master `8d1c7fbda9` まで変わっていない → 参照は撮り直さず `web-8a585f742a/`・`web-8a585f742a-2560x1080/` を使う
- `Stage4ScreenSetGameUiTest` は未実装（S4-gamma 未着手）。L3 の実在する前例は `EditModeInPlayingTest/Ui/PauseMenuGameUiTest.cs`・`GameUiSkeletonTest.cs`

## Requirements

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | ラダー 2 `uitk-opening-hud` | `scenarios/uitk/stage2/uitk-opening-hud.cs` が全体スイッチ GameUi・Web ホスト無し（`WebUiHost.Hub == null`）で、開幕 blocking スキットが自動で始まる → 自動送りを切り Enter と窓のクリックで 1 文ずつ送れる（`SceneRevision` が増える）→ `skit-tool-skip` で GameScreen → 目標 HUD `challenge-hud` に `challenge.current-hud` の枠（store に 1 つ・輪の中心が HUD の中心から 3px 未満）とラベルが出る → 選択中の装備が初期の石の斧 → 最寄りの木（ピンの候補をマスタから引く）へ W で歩いて寄る → F 長押しで `progress-bar` → 獲得通知の行（`EarnedNotificationContent`）→ 枠 0 に木のチェストを割り当て Digit1 で PlaceBlock → 設置、を `Success: true`・ErrorLogs 0・Note に系=GameUi。スクショ 5 枚 |
| R-2 | `challenge.current-hud` のハイライトの録画 | R-1 の録画（`Record = true`）の手順 2 で Assert 3 件とスクショ `02-challenge-hud-outline` が残る。録画を PR の報告ページに載せる |
| R-3 | ラダー 1〜2 が UITK で緑（と段階 2 の面に触る録画） | 最終 HEAD の GameUi で `uitk/uitk-lifeline.cs`・`uitk/stage2/uitk-opening-hud.cs`（と既存のラダー 3 `uitk/stage3/uitk-build-bp.cs`）が `Success: true`・ErrorLogs 0。plan §2.0 の 2 の「移した面に触るシナリオ」（Task 9 Step 2b の 8 本）も GameUi で `Success: true`、Web 側は wwxy 4 本の Web の回で合否不変 |
| R-4 | L3 1 本 | `Client.Tests/EditModeInPlayingTest/Ui/Stage2/Stage2HudGameUiTest` が GameUi で、段階 2 の HUD の根要素（表 `Stage2HudSurfaces`）が期待の層に 1 つずつ載る・ゲーム画面で `key-hints`・`challenge-hud`・`crosshair`（一人称の前提を Assert）が見え・PauseMenu で `key-hints` が消え `screen-backdrop` が出る・DeleteBar で `delete-mode-warning`、PlaceBlock で `placement-mode-hud` が出て GameScreen で消える・Ctrl+U で根の `display` が None で全 HUD が不可視・もう一度で戻る・巡る間の Error/Exception ログ 0、を PASS |
| R-5 | wwxy `tutorial-pebble-challenge` | `git mv` で `scenarios/tutorial-opening-log-challenge.cs` に改め、GUID をマスタから引く形（`ChallengeMaster`）に書き直す。先頭チャレンジがカレント・石の斧を装備して始まる・開幕スキットが出て飛ばせる・木ピンが先頭チャレンジの tutorialGuid で出る・目標 HUD の枠が store に出てラベルが見える・最寄りの木を伐採して先頭が完了・2 番目が解放されピンが消える・2 番目の keyControl のヒントが出る・「未破壊のMapObject」の LogError 0、を GameUi と Web で `Success: true` |
| R-6 | wwxy `tutorial-equip-challenge` | 用途が終わった装備の検証を外し、`git mv` で `scenarios/tutorial-craft-guidance.cs` に改め、2 番目のチャレンジ（木の板）のクラフト誘導（keyControl のヒント・Tab でインベントリ・木の板のアイテムの枠 → クリック → `recipe.craft-button` の枠〈GameUi では `TutorialAnchorRegistry` で Unique〉・長押しで 1 枚と残りで 3 枚作って完了・3 番目へ）を GameUi と Web で `Success: true` |
| R-7 | wwxy `tutorial-tree-pin-nearest-search` | GUID をマスタから引き、木をサーバー側で `Destroy()` して（原木を出さずに先頭チャレンジとピンを生かしたまま）、(a) 索引の最寄りと総当たりが一致 (b) ピンが最寄りの木に重なる (c) 壊すと索引とピンが次の木へ移る、を GameUi と Web で `Success: true` |
| R-8 | wwxy `server-notification-smoke` | 系の判定を `UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi` に揃え、GameUi と Web で `Success: true`（S2-notification の報告の追試） |
| R-9 | 台帳の割り振り | 「## 壊れた録画の割り振り」の束 2 の 4 行を結果（改名後の名前・直した／書き直した・GameUi/Web の合否）に書き換える（`X-prerun` の語を書かない） |
| R-10 | 束 2 の暗転と G1 | 台帳「## 束」の束 2 を `暗転`・起点 `0e81bbe3c3`、「## 追従記録」に未記帳の全件を記帳し、`check-uitk-web-follow.py --base origin/master --head HEAD` で「束 2（暗転・起点 0e81bbe3c3）: 未記帳 0 件」。取り込み後も 0 件。束 1・3 の残り（6 件）も同じ処遇で記帳して 0 件。PR の G1 job が pass |
| R-11 | 束 2 の突き合わせ差分 0 | `node tools/uitk/test-name-parity.mjs --bundle 2` が exit 0（`未対応 0`・`写す予定 0`）。`docs/uitk/disposition.md`「## 突き合わせの記録」に 1 行 |
| R-12 | 波の並べ比べの独立評価（設計書 §3.3・[A-179]） | 束 2 の状態（下の Task 8 の表）を、組 1＝参照 `web-8a585f742a`・`-2560x1080` と、EditMode で本番の層に載せた UITK（`UiRuntimePanelCapture`・1280×720 と 2560×1080。撮る前に [A-164] の 2px 照合）、組 2＝同じ実ゲームの状態を GameUi と Web の両方の系で撮った PlayMode の組（`stage2/uitk-stage2-compare.cs`）、組 0＝S2 各レーンの PR 報告ページの Web／UITK の組、を opus の評価役 2 体に別々に判定させ、別の subagent が画像で再現した差だけを残す。差ごとに処理の案（Web へ寄せる直し／例外の文面 LX-190〜199）を `done.md`「差の表」と「要裁定」に書き、判定は指揮へ（このレーンは USS を直さず、台帳の LX の行とコードの印は I-fix が置く） |
| R-13 | winunity の口 | `~/bin/winunity play` に `--ui-system GameUi|Web`（run-scenario.sh の前に EDC で `UiSystemEditorSwitch.Set` と `PlaytestGameViewOps.BringToFront()`、返り値が指定と違えば止める）、`winunity edc <branch> <code.cs>`（GUI Editor で execute-dynamic-code を回して出力と `.uloop/outputs` を回収）、`winunity test-all <branch>`（Client.Tests の 6 分割を順に回して合計を出す）と「batchmode の前に同じ worktree の GUI Editor を閉じる」口を足し（play は受け口の安全装置 `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` を Editor に立てる）、`done.md` に書く |
| R-14 | 申し送りの処遇 | 「## 申し送りの処遇」の表の全行に「やった（Task n・証拠）／回した（行き先）／やらない（理由）」が付き、`done.md` に写す |
| R-15 | 締め | moores-code-review → pr-create（base=feature/uitk、報告ページ）→ origin/feature/uitk 取り込み → compile・テスト → G1 緑 → `gh pr merge --merge` → Windows の GUI Editor を閉じる → `done.md`・合図 `cmux wait-for -S uitk-s2-ladder` |

### やらないこと
- Mac mini の Unity Editor の起動（ユーザー指示: Mac の Editor は全体で 1 本で、I-fix5 が使う）。Mac が要る撮影が残れば `done.md`「未完了」へ
- 画面・部品の新しい機能。設計書・実行計画の本文の改訂（`done.md`「integrator への依頼」に案を書く。台帳の自分の行は書く）
- `tutorial-research-renumber.cs`・`misc/research-node-state-label.cs`（X-prerun の持ち物）
- 段階 2 の各レーンが integrator へ依頼した共有部の直し（字の影の欠片 LX-86・`OpacityAt` の昇格・LX-40 の約物詰め・LX-171 の Medium 化 等）。指揮が I-fix5 へ振り済み（conductor.md 11-12）。並べ比べで観測したら既存 ID で処遇し観測を書き添える
- `scenarios/uitk/` 直下（27 本）・`EditModeInPlayingTest/Ui/` 直下（.cs 11 本）へのファイル追加（10 超のディレクトリには足さず `stage2/`・`Stage2/` に置く。既存の分割は I-fix の範囲）
- Codex（ユーザー指示）。§5 の codex-audit は opus の subagent 2 体で代える

## Global Constraints

- AGENTS.md 全部（1 ファイル 200 行未満・1 ディレクトリ 10 ファイル・partial と `Func<>` の禁止・try-catch は外部境界だけ・UniRx・2 行コメント〈日本語 1 行→英語 1 行〉・`#region Internal` はメソッド内のローカル関数だけ・デフォルト引数禁止・.meta を手で作らない・Prefab/シーンを手で編集しない）。録画シナリオでも `Func<>` を宣言しない（待ちは `p.Until(() => …, 秒, ラベル)`。書き直す 3 本の自前の `PollUntil(Func<…>)` は消す）
- **Unity は Windows の `~/bin/winunity` だけ**（Mac の Editor を起動しない）。winunity は push 済みのブランチを Windows 側が GitHub から取るので、compile・テスト・録画の前に必ず `git push`。結果は `~/.cache/winunity/runs/<runId>/`。missing script・`MapObjectHpBarView.SetHp` の NRE が出たら `../moorestech_logs/harness/uitk/winunity-play-fix.md` の手順 2（CleanBuildCache の再コンパイル）
- コマンドの形（Task 3 の後）: `winunity compile uitk-lane/s2-ladder`／`winunity test uitk-lane/s2-ladder --regex '<正規表現>' [--editor]`（EditModeInPlayingTest は `--editor`）／`winunity play uitk-lane/s2-ladder <scenario.cs> --ui-system GameUi`（Web の回は `--ui-system Web`）／`winunity edc uitk-lane/s2-ladder <code.cs>`／`winunity test-all uitk-lane/s2-ladder`。batchmode の compile・test・test-all は同じ worktree の GUI Editor が開いていると `job.ps1:51` で止まるので、Task 3 で「batchmode の前にその worktree の GUI Editor を閉じる」口を足す（閉じると SessionState の全体スイッチが消えるので、play は必ず `--ui-system` を付ける）
- 録画シナリオ `uitk-*` は冒頭で `p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, …)`。run-scenario.sh が初回 "already playing" で終わったら 1 回だけ投入し直す（bd moorestech-trfka）。Editor が背面だと Keyboard/Mouse が無効になるので、シナリオは前例どおり `IgnoreFocus`・`EnableDevice` を入れる。ラダーは `PlaytestGameViewOps.BringToFront()` を呼ぶ
- 録画 mp4・`PlaytestResults/`・TestResults xml・撮影 PNG は検証後に消す（報告ページへ載せる分は先に載せる）。`.superpowers/` はコミットしない。テストで `Saves/` に作ったワールドは `list-work-saves.sh` で探して消す（Windows 側は winunity の worktree ごと）
- 番号: 設計書の行は A-230〜A-239、見た目の例外は LX-190〜LX-199 だけ（指揮の依頼文と conductor.md 11-12 13:45 の割り当て。実行計画 §6.1 の表への追記は integrator への依頼）
- I-fix5 が直している `Notification.uss`・`Typography.uss`・`TooltipFace.cs`・`LocalizedLabel.cs`・字形の資産は触らない（Task 8 Step 0）
- 共有ファイル（`UiCommandRejectReason.cs`・`localization.csv`・`Registration/GameUi`・`MainGameUiRegistration`・tss・asmdef・台帳・設計書）は自分の行だけ足す。取り込みの衝突は両方の行を採る（I-fix5 が並走中）
- 区切り（タスク）ごとにコミットする。subagent はバックグラウンドのポーリングで待たず前景で実行する（winunity は前景で数分かかる。`timeout` を 600000ms にして前景で回す）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / L3 の可視判定を既存の GameUiElementQuery.IsVisible、開幕スキット待ちを既存の SkipOpeningSkitIfPlaying へ置き換え（本番コードの配置違反 0・死活表の抜け 0）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱0・第3バケツ0 / 7-1: ADR 0029 決定1（装備チャレンジ）が 0038 で消えたのに Accepted のまま → D-13 で integrator へ回す（ADR 本文はこのレーンで書かない）

## 死活表（Phase 2.5）

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| 段階 2 の HUD・通知・ツールチップ・ピン・チュートリアル枠・スキット（GameUi/Web） | 生きる | 本番コードは触らない見込み（L3・録画・台帳を足すだけ）。R-1・R-4 と wwxy の Web の回で確かめる |
| 開幕スキットの送り・スキップ・Ctrl+U | 生きる | 触らない。R-1・R-4 で通す |
| 伐採・採掘ゲージ・獲得通知 | 生きる | 触らない。R-1・R-5 で通す |
| 数字キーで選んで設置 | 生きる | 触らない。R-1 で通す |
| チュートリアルの進行（チャレンジの完了・次の解放・ピンの追従） | 生きる | 触らない。R-5〜R-7 で通す |
| 既存の録画（`uitk-*`・wwxy 以外） | 生きる | 触らない。改名した 2 本を参照する文書だけ書き換える（Task 6 Step 1 の grep） |
| winunity の既存の compile・test・play（`--ui-system` を付けない呼び方） | 生きる | 付けないときは今の挙動のまま（Task 3。既存の呼び方で 1 回流して確かめる） |

## 申し送りの処遇

| 出所 | 項目 | 処遇 |
|---|---|---|
| S3-ladder 要裁定 D-3（指揮が承認） | ラダー 2 を作る | やる（Task 5） |
| S2-hud integrator 4・実行計画 S2-ladder 行 | D-13 の「写す予定」2 件の除外 | やらない（起草時の実測で 2 件とも受け手が写し済み・`写す予定 0`。除外の但し書きは要らない。Task 2 で記録） |
| S2-skit 要裁定（指揮の決定） | 録画で cutscene director を強制有効化する確かめ方 | やらない（決定済み。`uitk-skit.cs` のまま。bd albof.58 を決定の note 付きで close） |
| S2-skit integrator 7・8（指揮が S2-ladder へ） | winunity の GameUi 切替・Game View の前面化・テスト分割 | やる（Task 3） |
| S2-notification integrator | 台帳の server-notification-smoke の行を「直した」に | やる（Task 7） |
| S2-hud integrator 6・bd moorestech-trfka | run-scenario.sh の初回 already playing | やらない（再投入 1 回の運用で回避。winunity の `--ui-system` の EDC で PlayMode 中なら先に止めるので起きにくくなる。観測を done.md に） |
| S2-hud・S2-mode-tooltip・S2-tutorial integrator（日本語の字の影の欠片・LX-86） | 共有の `.key-hint-text` の影 | 回す（I-fix5 の範囲。Task 8 で観測したら LX-86 で処遇し観測を書く） |
| S2-tutorial 要裁定 LX-38 | 明るい地で枠のグローが見えない | 回す（指揮の判断待ち。Task 8 の組 2 で観測を書く） |
| S2-notification 要裁定 LX-40・S3-ladder LX-171 | 文字組み | 回す（指揮が I-fix5 の文字組みの束へ。Task 8 で観測したら既存 ID） |
| S2-hud integrator 1・2・5・7、S2-mode-tooltip 1〜6、S2-tutorial 依頼、S2-skit 1〜6・9、S2-notification 依頼 | 共有部・設計書・カタログの直し | 回す（conductor.md 11-12 で I-fix5 へ振り済み。done.md に「S2-ladder は触っていない」と 1 行） |
| S3-ladder integrator | S4-gamma 計画のラダーのパスの読み替え | 回す（ラダー 2 も `stage2/` に置いたので、S4-gamma 計画の `scenarios/uitk/uitk-opening-hud.cs` を `scenarios/uitk/stage2/uitk-opening-hud.cs` へ。done.md の integrator への依頼） |
| ラダー 2 の定義（plan §4.4）と実物の食い違い | 選択肢が無い・枠は採掘の前 | 回す（plan §4.4 の文言の改めを done.md の integrator への依頼に。D-2・D-3） |

## File Structure

| ファイル | 種類 | 責務 |
|---|---|---|
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-opening-hud.cs` | Create | R-1・R-2。ラダー 2（前例 S3-ladder D-4 の `stage3/`） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-stage2-compare.cs` | Create | R-12 の組 2（PlayMode。HUD・モード HUD・ツールチップ・チュートリアル。通知とスキットは既存の `uitk-notification-compare.cs`・`uitk-skit-compare.cs` を再走） |
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage2/Stage2HudSurfaces.cs` | Create | R-4。期待の表（名前・層・族・引き方） |
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage2/Stage2HudGameUiTest.cs` | Create | R-4。L3 本体（前例 `EditModeInPlayingTest/Ui/PauseMenuGameUiTest.cs`。Ui 直下は .cs 11 本なので `Stage2/` を新設。S4-gamma の `Stage4/` と同じ形） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-pebble-challenge.cs` → `tutorial-opening-log-challenge.cs` | git mv＋書き直し | R-5 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-equip-challenge.cs` → `tutorial-craft-guidance.cs` | git mv＋書き直し | R-6 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-tree-pin-nearest-search.cs` | Modify（書き直し） | R-7 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs` | Modify（系の判定 1 行） | R-8 |
| `docs/uitk/TODO.md` | Modify | 束 2 の行・「## 追従記録」（束 1・2・3）・「## 参照キャプチャ」（束 2 の最新）・「## 壊れた録画の割り振り」の束 2 の 4 行・「## 見た目の例外」（LX-190〜 を使うときだけ） |
| `docs/uitk/disposition.md` | Modify | 「## 突き合わせの記録」に 1 行 |
| `~/bin/winunity`・`~/bin/winunity.d/interactive.ps1`・`~/bin/winunity.d/job.ps1` | Modify（repo 外・git 管理外） | R-13 |
| `../moorestech_logs/harness/uitk/S2-ladder/done.md` | Create（repo 外） | 報告 |


## Tasks

### Task 1: 前提の確認と記録の準備（済）

- [x] **Step 1**: `pwd` が worktree、土台 `origin/feature/uitk` 9f32a03390・S2 の 5 本と S3-ladder がマージ済み・`uitk-lifeline.cs` あり・`uitk-opening-hud.cs` 無しを確認
- [x] **Step 2**: bd `moorestech-albof.59` を作って claim。調査 3 本（scratchpad `research/`）
- [ ] **Step 3**: `../moorestech_logs/harness/uitk/S2-ladder/done.md` に骨組み（1 行目「状態: 作業中」・bd・土台・計画のパス）を書く（`../moorestech_logs/README.md` を先に読む）

---

### Task 2: 束 2 の暗転・G1・突き合わせの記録（R-10・R-11）

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 束」の束 2 の行・「## 追従記録」の末尾）
- Modify: `docs/uitk/disposition.md`（「## 突き合わせの記録」）

- [ ] **Step 1: 束 2 を暗転にする**

`docs/uitk/TODO.md`「## 束」の束 2 の行を次にする（起点は S2 の 5 レーンの分岐点で取り込み済みの master のうち最も古いもの。`git log --first-parent --merges --grep "uitk-lane/s2-" origin/feature/uitk --format='%H %s'` で 5 本のマージを出し、各マージの第 1 親と `origin/master` の `git merge-base` を取り、最も古いものを使う。起草時の見込みは `0e81bbe3c3`。違えばその値で書き、判断記録 D-5 を書き換える）:
```
| 2 | 常駐表示（入力の無い HUD・通知・ツールチップ・ワールドピン・チュートリアル枠・スキット） | 暗転 | 0e81bbe3c3 | `uitk-opening-hud` |
```

- [ ] **Step 2: 未記帳を出して記帳する**

Run: `git fetch origin master && python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD`
Expected（起草時）: 「束 1: 未記帳 5 件」「束 2: 未記帳 18 件」「束 3: 未記帳 1 件」。各 sha を `git show --stat <sha> -- moorestech_web/webui` で読み、「## 追従記録」の末尾に束 3 の既存の行と同じ処遇で 1 行ずつ足す:
- `src/shared/i18n/generated/localizationKeys.ts` の再生成だけのもの: `| <束> | <sha10> | 写した | Localization/localization.csv への追加に連動した生成物 src/shared/i18n/generated/localizationKeys.ts の再生成だけで、写し先は同じコミットの localization.csv（C# は Localization の鍵で読む） |`
- 旧 R1 の App.tsx 系とその撤去: `| 2 | <sha10> | 捨てた | 旧 R1（App.tsx の暫定の出し分け）とその撤去。W0-switch で外して master と同じ Web に戻した（束 1・3 の同じ sha の行と同じ処遇） |`
- それ以外（UITK 側に写すべき変更）が出たら、写し先を探して `写した` で書く。写し先が無ければ追従漏れなので bd を `--parent moorestech-albof.59` で立て、`done.md`「未完了」に書く（記帳で 0 にしない）

- [ ] **Step 3: 0 件を確かめる**

Run: `python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD; echo "exit=$?"`
Expected: 束 1・2・3 とも「未記帳 0 件」・「違反なし」・exit 0

- [ ] **Step 4: 突き合わせを記録する**

Run: `node tools/uitk/test-name-parity.mjs --bundle 2; echo "exit=$?"`
Expected: `束 2: Web 115 件 / 写した 111 / 処遇 4 / 写す予定 0 / 未対応 0`・exit 0（件数が増えていれば、増えた未対応をこのレーンで写すか持ち主へ回す）。`docs/uitk/disposition.md`「## 突き合わせの記録」に `| 2026-11-12 | 2 | <HEAD 10 桁> | 115 | 111 | 4 | 0 | 0 |`（実測の件数・表の列の並びは既存の行に合わせる）。

- [ ] **Step 5: コミットする**

```bash
git add docs/uitk/TODO.md docs/uitk/disposition.md
git commit -m "docs(uitk): 束 2 を暗転にして追従を記帳し、束 1・3 の残りも記帳して突き合わせを記録する（S2-ladder）"
```

---

### Task 3: winunity の口（R-13。repo 外）

**Files:**
- Modify: `~/bin/winunity`・`~/bin/winunity.d/job.ps1`・`~/bin/winunity.d/interactive.ps1`（git 管理外）

**Interfaces:**
- Produces: `winunity play <branch> <scenario.cs> [--ui-system GameUi|Web]`、`winunity edc <branch> <code.cs>`（結果 `~/.cache/winunity/runs/<runId>/edc.out.txt` と `outputs/`〈Windows の `moorestech_client/.uloop/outputs/` の写し〉）、`winunity test-all <branch>`（結果 `~/.cache/winunity/runs/<runId>/summary-all.txt`）

- [ ] **Step 1: `--ui-system` を足す**

`~/bin/winunity` の引数の解析に `--ui-system) UISYSTEM="$2"; shift ;;` を足し（値は `GameUi` か `Web` だけ。他は `fail`）、`ARGS_JSON` に鍵 `uiSystem` を入れる（付けないときは空文字＝今の挙動）。`interactive.ps1` の `Invoke-WinScenario` の先頭で、`$a.uiSystem` が空でなければ次を行い、出力に指定の値が無ければ `throw "全体スイッチを <値> にできない: <出力>"`（黙って違う系で流さない）:
```powershell
Invoke-Uloop @('control-play-mode','--project-path',$project,'--action','Stop') | Out-Null
$code = "Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.$($a.uiSystem)); Client.Playtest.Operations.Ui.PlaytestGameViewOps.BringToFront(); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();"
$out = Invoke-Uloop @('execute-dynamic-code','--project-path',$project,'--code',$code)
```
（`Invoke-Uloop` の名前・引数の渡し方は interactive.ps1 の既存の uloop 呼び出しに合わせる。`$a` を `Invoke-WinScenario` へ渡す引数が無ければ足す）

- [ ] **Step 1b: batchmode の前に GUI Editor を閉じる**

`job.ps1:51` は同じ project の GUI Editor が開いていると throw する。compile・test（`--editor` 無し）・test-all の前に、その worktree の GUI Editor があれば `editor-stop` と同じ手順で閉じてから回す（閉じたことを出力に 1 行出す）。

- [ ] **Step 2: `edc` を足す**

`winunity edc <branch> <code.cs>`: play と同じく GUI Editor を起こし（`Start-WinUnityEditor`）、コードのファイルを `_runs/<runId>/edc.cs` へ送り、`uloop execute-dynamic-code --project-path <project> --code-file`（撮影の EDC は長く、Windows のコマンド行の長さと引用で壊れるので code-file の経路を優先する。uloop に code-file が無ければ Windows 側でファイルを読んで渡す）を回して出力を `edc.out.txt` へ、`moorestech_client/.uloop/outputs/` を `outputs/` へ写して回収する。

- [ ] **Step 3: `test-all` を足す**

`winunity test-all <branch>`: 次の 6 本を順に回し（1 本目の EditModeInPlayingTest は PlayMode 遷移と入力注入を使うので `--editor` の経路、2〜6 本目は batchmode）、各 `summary.json` の件数・失敗名を `summary-all.txt` に合算する（S2-skit が完走した分割。5・6 の正規表現は起草時の推測なので、6 本の件数の和が `^Client\.Tests\.` の総数と合うかを 1 回見る。合わなければ直す）:
1. `^Client\.Tests\.EditModeInPlayingTest\.`
2. `^Client\.Tests\.Ui\.`
3. `^Client\.Tests\.Map\.`
4. `^Client\.Tests\.([A-D]|Ev|[F-L])`
5. `^Client\.Tests\.(M[^a]|Ma[^p]|[N-T])`
6. `^Client\.Tests\.(U[^i]|[V-Z])`

- [ ] **Step 3b: 受け口の安全装置の環境変数**

`uitk-lifeline.cs:31-32` は環境変数 `MOORESTECH_PLAYTEST_RECEIVER_BASE` が `http://127.0.0.1:9` でないと throw する（バグ報告を本番の受け口へ送らない安全装置）。winunity の play で GUI Editor を起こすとき（`Start-WinUnityEditor`）にこの値を Editor の環境に立てる（既に Editor が起きていて値が無ければ、閉じて起こし直す）。

- [ ] **Step 4: 確かめる**

共有ツールなので（I-fix5 も winunity を使う）、書き換えは `~/bin/winunity-next`・`~/bin/winunity.d-next/` の別名で作って試し、下の確認が通ってから元の名前へ差し替える（差し替えの前に、付けない呼び方が今のまま動くことを別名で確かめる）。このブランチを push して `winunity compile uitk-lane/s2-ladder` → `winunity play uitk-lane/s2-ladder .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-lifeline.cs --ui-system GameUi`。Expected: compile エラー 0・ラダー 1 が `Success: true`（compile で GUI Editor が閉じても GameUi で流れることの確認）。`--ui-system` を付けない play も 1 回流して今の挙動のまま動くことを見る（GUI Editor が開いている間は SessionState が残るので、付けない回は Editor を閉じてから起こした状態で流し、Note の系が Web であることを見る。ラダーは GameUi を主張して落ちるので、`--ui-system` 無しは Web の録画〈`server-notification-smoke.cs`〉で見る）。

- [ ] **Step 5: 記録**: repo の変更は無い。`done.md` に足した口と使い方を書く（`~/bin` は git 管理外。台帳 `~/brain/machines/` の windows-pc の winunity の行に 1 行足す）

---

### Task 4: L3 `Stage2HudGameUiTest`（R-4）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage2/Stage2HudSurfaces.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage2/Stage2HudGameUiTest.cs`

**Interfaces:**
- Consumes: `UiSystemPlayModeTestBase.SelectUiSystemBeforePlayMode(UiSystem)`、`EditModeInPlayingTestUtil.EnterPlayModeUtil()`・`LoadMainGame()`、`EditorBackgroundInputScope.Open()`、`GameUiRoot.Instance.GetRootElement()`・`GetLayer(UiLayer, UiFamily)`、`GameUiElementQuery.IsVisible(VisualElement)`（無ければ `Find(root, testid, mapper).Visible`）、`UIStateControl`（`CurrentState`・遷移の要求は `GameUiSkeletonTest` の `TransitionTo` と同じ書き方）、`WholeUiVisibilityModel.IsShown`、`CrosshairVisibility.IsVisible()`、`SemanticInput.KeyDown/KeyUp/TapKey`

- [ ] **Step 1: 表を書く**

`Stage2HudSurfaces.cs`（`internal static class Stage2HudSurfaces`）: 段階 2 の HUD の根要素を 1 行 1 件で持つ。各行は `readonly struct Stage2HudSurface`（`public readonly string Name; public readonly bool ByClass; public readonly UiLayer Layer; public readonly UiFamily Family;` と ctor。プロパティは使わない）、`public static readonly Stage2HudSurface[] All`。起草時の調査の行（名前・層は実物で確かめ、違えば実物に合わせる）:

| Name | ByClass | Layer | Family |
|---|---|---|---|
| `crosshair` | false | ViewportStage | Stage |
| `key-hints` | false | Chrome | Viewport |
| `progress-bar` | false | Chrome | Viewport |
| `challenge-hud` | false | Chrome | Viewport |
| `train-riding-hud` | true | Screen | Stage |
| `screen-backdrop` | false | Backdrop | Viewport |
| `placement-mode-hud` | false | Chrome | Viewport |
| `delete-mode-warning` | false | Chrome | Viewport |
| `delete-box-height-hud` | false | Chrome | Viewport |
| `cursor-tooltip` | false | Tooltip | Viewport |
| `tutorial-overlay` | false | Tutorial | Viewport |
| `key-control-hint-hud` | false | OverlayPanel | Viewport |
| `world-pin-overlay` | false | WorldPin | Viewport |
| `notification-host` | false | BehindStage | Viewport |
| `blocking-skit` | false | （スキットの層。`Registration/GameUi` の Skit の登録で確かめる） | （同） |

- [ ] **Step 2: テストを書く**

`Stage2HudGameUiTest : UiSystemPlayModeTestBase`、`[Category("CiShardClientPlay1")]`。テスト名 `UITK側で段階2のHUDが各層に1つずつ載り画面遷移とCtrlUで出て消える`。本体（PauseMenuGameUiTest の型。200 行未満）:

```csharp
[UnityTest]
public IEnumerator UITK側で段階2のHUDが各層に1つずつ載り画面遷移とCtrlUで出て消える()
{
    SelectUiSystemBeforePlayMode(UiSystem.GameUi);
    EnterPlayModeUtil();

    // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
    // Always call yield return new EnterPlayMode directly under the [UnityTest] function
    yield return new EnterPlayMode(expectDomainReload: true);
    LogAssert.ignoreFailingMessages = true;
    yield return TestBody().ToCoroutine();
    yield return new ExitPlayMode();
    SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

    #region Internal

    async UniTask TestBody()
    {
        var errors = new List<string>();
        await LoadMainGame();
        await UniTask.DelayFrame(5);
        var resolver = ClientDIContext.DIContainer.DIContainerResolver;
        var uiStateControl = resolver.Resolve<UIStateControl>();
        var wholeUi = resolver.Resolve<WholeUiVisibilityModel>();
        var crosshair = resolver.Resolve<CrosshairVisibility>();
        var root = GameUiRoot.Instance.GetRootElement();
        using var backgroundInput = EditorBackgroundInputScope.Open();

        // Web ホスト無しで GameUi だけが動く
        // Only GameUi runs with no web host
        Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");
        Assert.IsNull(Client.WebUiHost.Boot.WebUiHost.Hub, "UITK 側で Web ホストが起動している");

        // 開幕の blocking スキットが走っていれば抜けるまで待つ（走ると key-hints が隠れる）
        // Wait out an opening blocking skit if one runs (it hides key-hints)
        if (resolver.Resolve<BlockingSkitPresence>().IsBlocking.Value) await PlaytestSkitOps.SkipOpeningSkitIfPlaying(30f);
        Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "開幕スキットを飛ばした後にゲーム画面でない");

        // 巡る間のエラーだけを集める（起動の後に購読し、必ず外す。例外は握らない）
        // Collect errors only while cycling (subscribe after boot and always unsubscribe; nothing is swallowed)
        Application.logMessageReceived += Collect;
        try
        {
            await Cycle();
        }
        finally
        {
            Application.logMessageReceived -= Collect;
        }
        Assert.IsEmpty(errors, "巡る間に Error/Exception のログが出た: " + string.Join(" / ", errors));

        async UniTask Cycle()
        {

        // 各 HUD が期待の層に 1 つずつ載る
        // Each HUD is mounted once under its expected layer
        foreach (var surface in Stage2HudSurfaces.All) AssertMountedOnce(root, surface);

        // ゲーム画面で出るもの・出ないもの
        // What shows and what does not on the game screen
        Assert.IsTrue(Visible(root, "key-hints"), "ゲーム画面で key-hints が見えない");
        Assert.IsTrue(crosshair.IsVisible(), "前提: ゲーム画面の開始で一人称（偽なら一人称へ切り替える手順を足す）");
        Assert.IsTrue(Visible(root, "crosshair"), "一人称なのに crosshair が見えない");
        Assert.IsTrue(Visible(root, "challenge-hud"), "ゲーム画面で目標 HUD が見えない（テスト用マスタのチャレンジで出るかは実物で確かめ、出ないならこの行を外して D-7 に書く）");
        Assert.IsFalse(Visible(root, "screen-backdrop"), "ゲーム画面で幕が見える");
        Assert.IsFalse(Visible(root, "delete-mode-warning"), "ゲーム画面で削除の帯が見える");

        // UI 状態の遷移で出し分けが変わる
        // Transitions switch what is shown
        await TransitionTo(uiStateControl, UIStateEnum.PauseMenu);
        Assert.IsFalse(Visible(root, "key-hints"), "ポーズ中に key-hints が見える");
        Assert.IsTrue(Visible(root, "screen-backdrop"), "ポーズ中に幕が出ない");
        await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
        await TransitionTo(uiStateControl, UIStateEnum.DeleteBar);
        Assert.IsTrue(Visible(root, "delete-mode-warning"), "削除モードで帯が出ない");
        Assert.IsTrue(Visible(root, "delete-box-height-hud"), "削除モードで削除箱の高さ HUD が出ない（出る条件が範囲選択中だけなら、この行を外して D-7 に書く）");
        await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
        Assert.IsFalse(Visible(root, "delete-mode-warning"), "ゲーム画面へ戻っても帯が残る");
        await TransitionTo(uiStateControl, UIStateEnum.PlaceBlock);
        Assert.IsTrue(Visible(root, "placement-mode-hud"), "設置モードで配置 HUD が出ない");
        await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
        Assert.IsFalse(Visible(root, "placement-mode-hud"), "ゲーム画面へ戻っても配置 HUD が残る");

        // Ctrl+U で根ごと消え、もう一度で戻る
        // Ctrl+U hides the whole root and a second press restores it
        await ToggleCtrlU();
        Assert.IsFalse(wholeUi.IsShown.Value, "Ctrl+U で全体非表示にならない");
        Assert.AreEqual(DisplayStyle.None, root.resolvedStyle.display, "Ctrl+U で根が隠れない");
        foreach (var surface in Stage2HudSurfaces.All) Assert.IsFalse(Visible(root, surface.Name), $"{surface.Name} が Ctrl+U 中も見える");
        await ToggleCtrlU();
        Assert.IsTrue(wholeUi.IsShown.Value, "2 回目の Ctrl+U で戻らない");
        Assert.IsTrue(Visible(root, "key-hints"), "Ctrl+U の往復の後に key-hints が見えない");
        }

        void Collect(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) errors.Add(condition);
        }
    }

    #endregion
}
```

クラス直下の private static に、`AssertMountedOnce(VisualElement root, Stage2HudSurface s)`（`ByClass` なら `root.Query(className: s.Name)`、そうでなければ `root.Query(name: s.Name)` の件数が 1、かつ `GameUiRoot.Instance.GetLayer(s.Layer, s.Family).Contains(element)` が真。失敗文に名前と件数）、`Visible(VisualElement root, string name)`（名前かクラスで引き、どれかが既存の `GameUiElementQuery.IsVisible(VisualElement)`〈`Client.Playtest/UiElements/GameUi/GameUiElementQuery.cs:93`。internal だが `Client.Playtest/AssemblyInfo.cs` の InternalsVisibleTo("Client.Tests") で呼べる〉で見える。判定を作り直さない）、`TransitionTo(UIStateControl, UIStateEnum)`（`GameUiSkeletonTest` の同名の手順を写す。遷移の後 `DelayFrame(3)` して `CurrentState` を Assert）、`ToggleCtrlU()`（`SemanticInput.KeyDown(Key.LeftCtrl)` → `await SemanticInput.TapKey(Key.U)` → `KeyUp(Key.LeftCtrl)` → `DelayFrame(3)`）、開幕スキットは既存の `PlaytestSkitOps.SkipOpeningSkitIfPlaying(float timeoutSeconds)`（`Client.Playtest/Operations/Ui/PlaytestSkitOps.cs:42`。再生されなければ期限まで待って Debug.Log して戻り、例外は出さない）を呼んで飛ばし（作り直さない）、直後に `uiStateControl.CurrentState == UIStateEnum.GameScreen` を Assert する（飛ばせなければここで落ちる）。待ち秒数は 30 秒。L3 の専用マスタ（`EditModeInPlayingTest` の ServerData の challenges.json に startedActions が無い）では開幕スキットは走らないので、`BlockingSkitPresence.IsBlocking` が偽なら呼ばない（`BlockingSkitPresence` の型と読み口は実物で確かめる。DI に無ければ `SkitPresentationStateStore.Instance.GetCurrent()` の Mode で判定）。DeleteBar・PlaceBlock へ直接遷移できない（前提条件で拒まれる。PlaceBlock は選択中のブロックが要るかもしれない）と実測したら、ホットバーへ木のチェストを割り当てて数字キーで入る等の本番の経路に替え、それでも入れなければその行だけを外して判断記録 D-7 に理由と実測を書く（Ctrl+U と PauseMenu は必須のまま）。

- [ ] **Step 3: push してコンパイルとテスト**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage2/
git commit -m "test(uitk): 段階 2 の HUD が UITK スイッチで各層に載り、画面遷移と Ctrl+U で出て消える L3 を足す（S2-ladder）"
git push -u origin uitk-lane/s2-ladder
winunity compile uitk-lane/s2-ladder
winunity test uitk-lane/s2-ladder --regex '^Client\.Tests\.EditModeInPlayingTest\.Ui\.' --editor
```
Expected: compile エラー 0・`Stage2HudGameUiTest` PASS・同じディレクトリの既存の L3（PauseMenuGameUiTest 等）も PASS。.meta は Windows の Editor が作るので、作られた .meta を Windows の worktree から回収してコミットする（`ssh winbuild` で `C:\dev\worktrees\moorestech\<name>\moorestech_client\Assets\Scripts\Client.Tests\EditModeInPlayingTest\Ui\Stage2\*.meta` と `Stage2.meta` を scp で取り、`git add` → `git commit -m "chore(uitk): Unity が生成した .meta を足す（S2-ladder L3）"`。手で .meta を作らない）

---

### Task 5: ラダー 2 `uitk-opening-hud`（R-1・R-2）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-opening-hud.cs`

**Interfaces:**
- Consumes: DSL `p.Until`・`p.UntilUiElement`・`p.ClickUi`・`p.PressKey`・`p.WaitUiState`・`p.SetupDebugEnvironment`・`p.WarpPlayer`・`p.AimAt`・`p.AimAtPlaceOrigin`・`p.ClickPlace`・`p.WaitBlockGameObject`・`p.Hotbar.AssignHotbar`・`p.Screenshot`・`p.Note`・`p.Assert`、`SemanticInput.KeyDown/KeyUp`、`SkitPresentationStateStore.Instance`、`TutorialPresentationStateStore.Instance`、`AnchorIds.ChallengeCurrentHud`、`MasterHolder.ChallengeMaster`（`ChallengeCategoryMasterElements`・`GetCategoryInitialChallenges(Guid)`・`ResolvePinTargets(MapObjectPinTutorialParam)`）、`MapObjectGameObjectDatastore.SearchNearestMapObject(HashSet<Guid>, Vector3)`、`InteractTargetSelector.Scan().Primary`、`GameUiRoot.Instance.GetLayer(UiLayer.BehindStage, UiFamily.Viewport)`、`NotificationRowView.Row.Content is EarnedNotificationContent`

- [ ] **Step 0: 置き場の確認**: `git fetch origin feature/uitk` で I-fix5（`scenarios/uitk/` を段ごとのサブディレクトリへ移す。I-fix5 計画 10n）が入ったかを見る。入っていて `stage2/` に段階 2 の録画が移っていれば、取り込んでからその中に置く（`stage2/` が 10 本を超えるなら I-fix5 の分け方に合わせてさらに分ける）。以降のタスクのシナリオのパスは、その時点で `git ls-files` で引き直す

- [ ] **Step 1: シナリオを書く**

下書き（scratchpad `research/uitk-opening-hud.draft.cs`。168 行・未コンパイル）を土台に、次を変えて `stage2/uitk-opening-hud.cs` に置く（200 行未満）:
- 木の GUID の直書き（5 個）をやめ、先頭チャレンジの mapObjectPin からマスタで引く:
```csharp
var category = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.Single(c => c.InitialUnlocked);
var first = MasterHolder.ChallengeMaster.GetCategoryInitialChallenges(category.CategoryGuid).Single();
var pinTutorial = first.Tutorials.Single(t => t.TutorialType == TutorialsElement.TutorialTypeConst.mapObjectPin);
var treeGuids = MasterHolder.ChallengeMaster.ResolvePinTargets((MapObjectPinTutorialParam)pinTutorial.TutorialParam);
```
- 初期装備の照会は `MasterHolder.ItemMaster.Items.InitialEquipmentItems[0].ItemGuid` の `ItemId` と比べる（名前「石の斧」の直書きをやめる）
- 冒頭のコメントに「100_start_game に選択肢は無い。選択肢は `uitk-skit-choice-pause-cursor.cs`（sample_short）が受け持つ（D-2）」「枠は先頭チャレンジの tutorial で、伐採で原木 3 個に届くと消えるので採掘の前に見る（D-3）」を 2 行コメントで書く
- `IsUiFound` の `GetAwaiter().GetResult()` をやめ、`await p.Until(async 版)` が無ければ `await PlaytestUiElementOps.FindUi("progress-bar")` を `p.Until` の外で回す前例（`uitk-hud.cs:113-116`）の書き方に合わせる（`Func<>` を宣言しない）

流れ: 系の主張・入力準備・Game View を表へ → SetupDebugEnvironment → 1. 開幕スキット（blocking 開始・文が出て送り待ち・`blocking-skit`・自動送りを切る・01 スクショ・Enter で 1 文・窓のクリックで 1 文・`skit-tool-skip` → GameScreen）→ 2. 目標 HUD の枠（`challenge-hud` が見える・store に `challenge.current-hud` の枠が 1 つ・輪の中心のずれ < 3px・ラベルが見える・02 スクショ）→ 3. 石の斧を選んでいる・最寄りの木の 5m 手前へ・W で 1.6m 以内へ歩く・本番の選定器が木を選ぶ・F 長押しで `progress-bar`（03）・獲得通知の行（04）→ 4. 足場へ戻り枠 0 に木のチェスト・Digit1 で PlaceBlock・設置（05）→ Esc で GameScreen。

- [ ] **Step 2: push して GameUi で流す**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-opening-hud.cs
git commit -m "test(uitk): ラダー 2 uitk-opening-hud を足す（開幕スキット・目標 HUD の枠・歩いて伐採・獲得通知・数字キーで設置。S2-ladder）"
git push
winunity play uitk-lane/s2-ladder .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-opening-hud.cs --ui-system GameUi
```
Expected: `Success: true`・ErrorLogs 0・Note に `全体スイッチ 系=GameUi`・スクショ 5 枚・録画 mp4。落ちたらスクショと `scenario.log`・`gui-editor.log` から原因を確かめて直す（歩く区間が届かなければ W の向きを `Camera.main.transform.forward` から木への向きに `AimAt` で合わせ直す・木の手前に障害があれば別の木を候補の中から選ぶ）。製品の不具合が原因なら直さず bd（`--parent moorestech-albof.59`）に証拠付きで立て、`done.md`「未完了」に書く

- [ ] **Step 3: 録画を控える**: `~/.cache/winunity/runs/<runId>/play/` の `recording.mp4` と 02 スクショを報告ページ用に scratchpad へ写す（Task 11 で載せる）。`done.md` に runId・Assert 数を書く

---

### Task 6: wwxy の tutorial 3 本（R-5・R-6・R-7）

**Files:**
- git mv: `scenarios/tutorial-pebble-challenge.cs` → `scenarios/tutorial-opening-log-challenge.cs`（書き直し）
- git mv: `scenarios/tutorial-equip-challenge.cs` → `scenarios/tutorial-craft-guidance.cs`（書き直し）
- Modify: `scenarios/tutorial-tree-pin-nearest-search.cs`（書き直し）
（`scenarios/` = `.agents/skills/unity-playmode-recorded-playtest/scenarios/`）

**Interfaces:**
- Consumes: Task 5 と同じマスタの引き方、`p.WaitForChallenge(Guid, bool completed, float, string)`・`p.ReadChallenge`（`PlaytestChallengeSnapshots.cs:15`）・`p.SkipOpeningSkit()`（`PlaytestSkitOps.cs:17`）・`p.RunOnServer(string, Action)`・`p.GiveItemDirect`・`p.CountItem`・`SendOnly.AttackMapObject`・`SendOnly.Craft`、`WorldPinStateStore.Instance.GetCurrent().Pins`（`PinId`・`TutorialGuid`）、`TutorialPresentationStateStore`（`TutorialOutlineElementData.AnchorId`・`TutorialKeyControlElementData`）、`PlaytestUiElementOps.FindUi`（両系の切替ロケータ）、`TutorialAnchorRegistry.Resolve`（GameUi だけ。前例 `uitk/uitk-research-tree.cs:66-67`）、`ServerContext.MapObjectDatastore.Get(id).Destroy()`（前例 `Client.Tests/EditModeInPlayingTest/MapObjects/MapObjectNearestSearchTest.cs`）

- [ ] **Step 1: 改名と参照の洗い出し**

```bash
cd .agents/skills/unity-playmode-recorded-playtest/scenarios
git mv tutorial-pebble-challenge.cs tutorial-opening-log-challenge.cs
git mv tutorial-equip-challenge.cs tutorial-craft-guidance.cs
cd -
rg -l "tutorial-pebble-challenge|tutorial-equip-challenge" --glob '!docs/superpowers/plans/**' --glob '!docs/uitk-migration/execution/research/**' --glob '!.decisions/**' --glob '!docs/adr/**'
```
出た文書のうち、スキルの references と台帳 `docs/uitk/TODO.md` だけ名前を新しい名前に書き換える（過去の計画書・研究文書・ADR・裁定は書き換えない）。`PlaytestRunner.Run` の第 1 引数もファイル名に合わせる。

- [ ] **Step 2: 3 本を書き直す**

共通（3 本とも）: ⚠ 見出しと先頭の 2 行の `using` の跡を外す。GUID の直書きをやめて Task 5 の形でマスタから引く（`first`・`second = GetNextChallenges(first.ChallengeGuid).Single()`・`pinTutorialGuid = pinTutorial.TutorialGuid.ToString("D")`・`treeCandidates = ResolvePinTargets(...)`・`firstTask = (InInventoryItemTaskParam)first.TaskParam`・`initialTool = Items.InitialEquipmentItems[0].ItemGuid`）。`Single` はマスタの形が変わったら最初の行で止めるため（意図）。自前の `PollUntil(Func<…>)` を消して `p.Until` に揃える。`SetupDebugEnvironment` は呼ばない（自然なマップを残す。元の方針）。系は `p.Note($"系={UiInputExclusivity.ActiveUiSystem}")`。前置きに `IgnoreFocus`・`EnableDevice`。UITK だけの追加確認は `if (UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi)` で囲む。

`tutorial-opening-log-challenge.cs`（R-5）の Assert の列:
1. `WaitForChallenge(first, false)`「先頭チャレンジ（原木）がカレント」
2. `RunOnServer` で選択中の装備が `initialTool` の ItemId「石の斧を選んで装備した状態で始まる（ADR 0038）」
3. `FindUi("blocking-skit")` が見つかる → 01 → `SkipOpeningSkit()` → 見つからなくなる
4. `WorldPinStateStore` に `PinId=="map-object-pin" && TutorialGuid==pinTutorialGuid` → `FindUi("world-pin-overlay")` → 02
5. `TutorialPresentationStateStore` の Elements に `TutorialOutlineElementData{AnchorId==AnchorIds.ChallengeCurrentHud}` → `FindUi("tutorial-highlight-label")` → 03
6. 最寄りの木（`SearchNearestMapObject(treeCandidates, p.PlayerPosition)` が非 null になるまで待つ）を `SendOnly.AttackMapObject` で最大 6 回・間隔 1.1 秒、`CountItem(原木の名前) >= firstTask.ItemCount` で止める → `WaitForChallenge(first, true)` → 04
7. `WaitForChallenge(second, false)`・ピンが store から消える
8. `second.Tutorials` の keyControl の `TutorialKeyControlElementData`（TutorialGuid・KeyName）→ `FindUi("key-control-hint")` → 05
9. 「未破壊のMapObject」の LogError 0（旧 equip の購読を移す）

`tutorial-craft-guidance.cs`（R-6）の Assert の列:
1. `SkipOpeningSkit()`・`GiveItemDirect(原木の名前, firstTask.ItemCount + 3)` → `WaitForChallenge(first, true)` → `WaitForChallenge(second, false)`
2. keyControl の element（TutorialGuid・KeyName が `second` のマスタどおり）→ `FindUi("key-control-hint")` → 01
3. `PressKey(Tab)` → `WaitUiState(PlayerInventory)` → Elements に itemViewHighLight の枠（`recipe.item-<木の板の ItemId>`）→ GameUi のときだけ `TutorialAnchorRegistry.Resolve(root, "recipe.item-<木の板の ItemId>")` が Unique → 02。`recipe.craft-button` のアンカーはアイテムを選んだ後にしか付かない（`Client.Ui.State/Recipe/Screen/RecipeViewModelFactory.cs:28-34`・`Client.Ui/Screens/Recipe/Parts/CraftRecipeEntryView.cs:92-95`。マスタの誘導も「①木の板をクリック → ②クラフトボタンを長押し」）
4. 木の板のアイテムを `ClickUi`（両系の testid は実物の Recipe 画面のアイテムの name・Web の data-testid から取る）→ Elements に `recipe.craft-button` の枠 → GameUi のときだけ `Resolve(root, "recipe.craft-button")` が Unique → 03。クラフトボタンを長押しして 1 枚作る（UITK・Web の長押しの前例は `uitk/uitk-recipe-craft.cs`）→ 残り 2 枚は `CraftRecipeMaster.GetResultItemCraftRecipes(木の板の ItemId)[0].CraftRecipeGuid` を `SendOnly.Craft` で → `WaitForChallenge(second, true)` → 3 番目（`GetNextChallenges(second).Single()`）がカレント
5. `PressKey(Tab)` → `WaitUiState(GameScreen)` → 3 番目の keyControl の element → 04
（研究の説明文の確認は外す。研究の詳細欄はノードを選ばないと出ず、`uitk/uitk-research-tree.cs` が両系で見ている。D-9）

`tutorial-tree-pin-nearest-search.cs`（R-7）の Assert の列:
1. `WaitForChallenge(first, false)` → `SkipOpeningSkit()` → ピンが `pinTutorialGuid` で出る → 01
2. (a) 索引 `SearchNearestMapObject(treeCandidates, 位置)` と総当たり（`MapObjectGuid` が候補にあり `IsAvailable` のものの最小距離）が同じ木・同じ距離
3. (b) ピンの transform が索引の木に重なる（元の判定の距離をそのまま）
4. `RunOnServer("最寄りの木を採掘無しで壊す", () => ServerContext.MapObjectDatastore.Get(id).Destroy())` → クライアントで `!IsAvailable` → `ReadChallenge(first.ChallengeGuid, false)` が真かつ `ReadChallenge(first.ChallengeGuid, true)` が偽（原木を得ていないので先頭はまだカレント）→ 02
5. (c) 索引が壊した木以外を返し総当たりと一致・ピンがその木へ移る → 03

- [ ] **Step 3: push して両系で流す**

```bash
git add -A .agents/skills/unity-playmode-recorded-playtest/ docs/
git commit -m "test(uitk): wwxy の tutorial 3 本を今のマスタから引く形へ書き直し、2 本を中身に合わせて改名する（S2-ladder）"
git push
for s in tutorial-opening-log-challenge tutorial-craft-guidance tutorial-tree-pin-nearest-search; do
  for sys in GameUi Web; do winunity play uitk-lane/s2-ladder .agents/skills/unity-playmode-recorded-playtest/scenarios/$s.cs --ui-system $sys; done
done
```
Expected: 6 回とも `Success: true`・ErrorLogs 0。Web の回で CEF が出ない環境（Editor ログに RendererUnavailable）なら、その証拠を添えて「Web は未確認」と `done.md` に書く。落ちたら原因を確かめて直し、製品の不具合なら bd へ（Task 5 Step 2 と同じ）

---

### Task 7: server-notification-smoke と台帳の割り振り（R-8・R-9）

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs`（系の判定）
- Modify: `docs/uitk/TODO.md`（「## 壊れた録画の割り振り」の束 2 の 4 行）

- [ ] **Step 1: 系の判定を揃える**

`server-notification-smoke.cs` の `WebUiHost.Hub == null` による GameUi の分岐を `UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi` にする（Web ホストの起動に失敗した Web の回が GameUi の分岐へ入らないため）。`Hub == null` は GameUi の回で「Web ホスト無し」を Assert する 1 行として残す。

- [ ] **Step 2: 両系で流す**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs
git commit -m "test(uitk): server-notification-smoke の系の判定を全体スイッチに揃える（S2-ladder）"
git push
for sys in GameUi Web; do winunity play uitk-lane/s2-ladder .agents/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs --ui-system $sys; done
```
Expected: 両方 `Success: true`・ErrorLogs 0

- [ ] **Step 3: 台帳の 4 行を書き換える**

I-fix5（計画 10m）も server-notification-smoke の行を「直した（#1649）」へ書き換える。取り込みで衝突したら両方の情報を 1 行にまとめる。「## 壊れた録画の割り振り」の束 2 の 4 行の「シナリオ」「方針」の列を結果にする（`X-prerun` の語を書かない。S4-gamma の一覧作りが行全体を grep するため）。例:
```
| `tutorial-opening-log-challenge.cs`（旧 `tutorial-pebble-challenge.cs`） | 待つチャレンジ・チュートリアルの GUID がピン先マスタに無い | 2 | S2-ladder | 書き直した（S2-ladder。先頭チャレンジ〈原木〉の連鎖をマスタから引く。GameUi・Web とも緑） |
| `tutorial-craft-guidance.cs`（旧 `tutorial-equip-challenge.cs`） | 同上（装備のチャレンジは ADR 0038 で削除） | 2 | S2-ladder | 書き直した（S2-ladder。2 番目〈木の板〉のクラフト誘導へ用途替え。GameUi・Web とも緑） |
| `tutorial-tree-pin-nearest-search.cs` | 同上 | 2 | S2-ladder | 書き直した（S2-ladder。木をサーバーで壊してピンの追従を見る。GameUi・Web とも緑） |
| `server-notification-smoke.cs` | 削除済みの研究 GUID を送る | 2 | S2-ladder | 直した（S2-notification #1649。S2-ladder で系の判定を揃え GameUi・Web とも緑） |
```
（「GameUi・Web とも緑」は実測の合否に合わせる）

- [ ] **Step 4: コミットする**

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 壊れた録画の割り振りの束 2 の 4 行を結果に書き換える（S2-ladder）"
```

---

### Task 8: 波の並べ比べと独立評価（R-12）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-stage2-compare.cs`
- Modify: `docs/uitk/TODO.md`「## 参照キャプチャ」（束 2 の最新の 1 行だけ）
- 束 2 の USS・台帳「## 見た目の例外」は触らない（[A-179]: 直すか例外かは指揮が判定し、行と印は I-fix が置く。D-14）

**Interfaces:**
- Consumes: `Client.Ui.Editor.Capture.UiRuntimePanelCapture.CaptureToPng(VisualElement, int, int, Color32, UiCaptureLocalization, string)`（compare-procedure.md §3）、既存の見本（`ModeHudViewModelSamples`・`CursorTooltipViewModelSamples`〈`Client.Tests/Ui/Screens/ModeHud/`・`CursorTooltip/`〉・`NotificationHostViewTestFixture`・`KeyHintHudViewModel.Create`・`CurrentChallengeHudViewModel.Create`・`ProgressHudView.SetShown/SetProgress`・`TrainRidingHudView`）、`PlaytestGameViewOps.SetResolution(CancellationToken, int, int)`、Task 3 の `winunity edc`

撮る状態（参照 id はすべて `web-8a585f742a/` と `-2560x1080/` にある）。組 1 の見本の中身（文言・件数・値）は Web の fixture（`moorestech_web/webui/e2e/uitk-reference/referenceShots.ts` とそこから辿る mock-host の fixture）の値を写して組む（例: 2-game-hud の題「Second Step」・キーヒント「Tab インベントリ／R リサーチツリー」）。既存の見本の値が違えば、見本を使わず VM を直に組む。2-placement-unavailable・2-placement-connect-tool は `ModeHudPlacement` の ctor 引数で、通知は `NotificationHostView` と通知モデルを直に組む（`NotificationHostViewTestFixture` は NUnit の土台で EDC から呼べない）:

| 参照 id | 組 1（EditMode）の見本 | 組 2（PlayMode） |
|---|---|---|
| 2-game-hud | `KeyHintHudViewModel.Create`・`CurrentChallengeHudViewModel.Create` を直に組む | `uitk-stage2-compare.cs` |
| 2-challenge-hud-multiple-long | `CurrentChallengeHudViewModel.Create`（長文 3 件） | 同 |
| 2-mining | `ProgressHudView.SetShown(true)`・`SetProgress(x)` | 同（採掘中） |
| 2-train-hud | `TrainRidingHudView` に分岐 3・選択 2 | 撮らない（乗車は分岐 1 しか出ない） |
| 2-placement・2-placement-unavailable・2-placement-connect-tool | `ModeHudViewModelSamples` | 同（設置モード） |
| 2-delete-bar | `ModeHudViewModelSamples.Delete(height)` | 同（DeleteBar） |
| 2-tooltip・2-tooltip-multiline | `CursorTooltipViewModelSamples` | 同 |
| 2-notifications | `NotificationHostViewTestFixture` | `uitk/uitk-notification-compare.cs` を再走 |
| 2-key-control-hint・2-world-pin-onscreen・2-world-pin-offscreen | `KeyControlHintPresenter.Mount(VisualElement, IReadOnlyReactiveProperty<…>)`・`WorldPinPresenter.Mount`（public static）に `Client.Tests/Ui/Screens/Tutorial/Cues/FakeTutorialCueModels.cs` の値 | `uitk-stage2-compare.cs`（`uitk-tutorial-cues.cs` の 01〜03 の手順） |
| 2-skit-text・2-skit-choices・2-skit-transition・2-skit-background | `Client.Tests/Ui/Screens/Skit/SkitTestData.cs` の値で会話窓を組む（撮れなければ理由を書き組 2 で判定。D-11） | `uitk/uitk-skit-compare.cs` を再走 |

（2-progress-labeled は処遇「捨てる」なので撮らない。動画 3 本は並べない〈S2-tutorial・S2-notification が動きを確かめ済み〉）

- [ ] **Step 0: I-fix5 との重なりを見る**

並走中の I-fix5 は `scenarios/uitk/` 直下を段ごとのサブディレクトリへ移し（I-fix5 計画 10n）、`UiRuntimePanelCapture` に幕と地の口を足し（10j。シグネチャが変わる）、`Screens/Notification/Notification.uss`・`Foundation/Theme/Typography.uss`・`Components/Text/TooltipFace/TooltipFace.cs`・`Foundation/Text/LocalizedLabel.cs`・字形の資産を変え、台帳から LX-10・LX-40・LX-171 の行を消している。`git fetch origin feature/uitk` で I-fix5 のマージを確かめ、済みなら先に取り込んでから、取り込んだ後のパス・シグネチャで書いて撮る（字形と行送りが変わるため）。済んでいなければ差の表の見出しに「I-fix5 前の撮影」と書く。このタスクでは上の 4 ファイルと字形を触らない（文字の差の観測は I-fix5 の ID か `done.md` へ）。台帳に無い LX の ID を引かない

- [ ] **Step 1: 組 1 を撮る**

上の表の組 1 の見本を、EDC の中で `UiLayerStack.Build(content)` を組み、各 View を本番の Presenter と同じ層・族（Task 4 の `Stage2HudSurfaces` の表）へ載せてから（裸で撮ると舞台族の倍率と枠の中の配置が違い、位置を比べられない。前例 S3-buildmenu D-61）`UiRuntimePanelCapture.CaptureToPng` で 1280×720 と 2560×1080 に撮る。地の色は HUD ごとにその HUD のすぐ外の参照の画素を読み（2-game-hud は上と下で地が違う）、HUD 単位で撮る。撮る前に [A-164] の照合: 実行時に作れる 1 状態（2-mining）を組 1 と組 2 で撮り、HUD の矩形が 2px 以内で一致することを確かめる（合わなければ舞台の論理寸法を組 2 の実測にそろえて撮り直す）。そのコードを scratchpad に書き（撮影のための新しいコードを repo に足さない。compare-procedure.md §3 の例の形・地の色は各参照 PNG の該当画面の外側の 1 色）、`winunity edc uitk-lane/s2-ladder <code.cs>` で回して `outputs/compare/S2-ladder/` を回収する。ファイル名は参照の撮影 id と同じ。`winunity edc` で撮れない見本（Editor のパネルの都合で落ちる等）は、理由を `done.md`「未完了」に書き組 2 で判定する

- [ ] **Step 2: 組 2 を撮る**

`uitk-stage2-compare.cs`（200 行未満）: 系を GameUi で主張 → `BringToFront`・操作オーバーレイを切る（`uitk-skit-compare.cs` の前例）→ `SetResolution(1280, 720)` → 開幕スキットを飛ばす → ゲーム画面（2-game-hud）→ 木へ寄り F で採掘中（2-mining）→ 枠 0 に木のチェスト・Digit1 で設置モード（2-placement）→ 設置できない所へ照準（2-placement-unavailable）→ 枠 1 に電柱を割り当て電線ツールの状態（2-placement-connect-tool。前例 `uitk-mode-hud-and-tooltip.cs` の 02）→ Esc・削除モード（2-delete-bar）→ ツールチップの出る所へカーソル（2-tooltip。`uitk-mode-hud-and-tooltip.cs` の 04 の手順）→ ピン・キー操作ヒント（2-world-pin-onscreen・2-key-control-hint。`uitk-tutorial-cues.cs` の 01〜03）→ 同じ状態を 2560×1080 で。シナリオは両系で同じ手順が通るように書く（UITK だけの照会は `if (ActiveUiSystem == GameUi)` で囲む）。push して `winunity play ... --ui-system GameUi` と `--ui-system Web`（Web で CEF が出なければ Editor ログの RendererUnavailable を証拠に「Web 側の組 2 は未撮影」と書き、組 1 と組 0 で判定する）。続けて `uitk/uitk-notification-compare.cs`・`uitk/uitk-skit-compare.cs` を `--ui-system GameUi` で再走

- [ ] **Step 3: 評価役 2 体に別々に判定させる（本体が派遣する）**

参照・組 1・組 2（GameUi と Web）と組 0（S2 の 5 レーンの報告ページ `https://review.moores.tech/pr/{1611,1613,1627,1649,1653}/report/` の Web／UITK の組。読めなければ各 `harness/uitk/S2-*/done.md` の差の表）を並べた簡易 HTML を scratchpad に作り、opus の subagent を 2 体、同じ依頼文で別々に起こす（互いの結果を見せない）。渡すもの: ゴールの原文「WebUIと同じ見た目で、適切な設計、コンポーネント化されたUITKベースのUIを開発すること」、画像のローカルパスと撮影元 sha、`docs/uitk/TODO.md`「## 見た目の例外」、設計書 §3.3・§3.4、compare-procedure.md §5。依頼:「色・寸法・配置・装飾の有無・文字の大きさと段組の差は参照と組 1 で判定する（組 1 が無い状態は組 2 の GameUi と Web で判定し、世界の地の違いは差に数えない）。組 2 は同じ実ゲームの状態の GameUi と Web の差と、崩れ（はみ出し・重なり・欠け）・同時表示の重なりを見る。記帳済みの LX に当たる差は除く。アイテムの並び・名前・地の違いは差に数えない。既存の LX に当たる差はその ID を書く。各差に座標か画素の値を添える」。報告は scratchpad のファイルへ、返答は 3 行。

- [ ] **Step 4: 再現して処理の案を書く**

2 体の指摘を合わせ、別の subagent（opus）に PNG の画素・寸法を測らせて再現できたものだけ残す（[A-179]）。残った差ごとに処理の案を書く: Web へ寄せる直し（差のあるファイル・USS の行・値）か、例外の文面（LX-190〜199 の番号で「画面・部品／差／同じにしない理由／推奨案」。§3.4 の入れてよい条件を満たすものだけ）。このレーンは USS を直さず、台帳の LX の行とコードの印を置かない（指揮が判定し、I-fix が同じ PR で置く）。差の一覧を `done.md`「要裁定」に「指揮の判定を待つ並べ比べの差 n 件（差の表の LX 案）」として 1 行で書く。I-fix5 の範囲の差（字の影の欠片・行末の約物・和文の題の太さ・明るい地のグロー等）は、その時点の台帳にある ID で処遇し（消えた ID は引かない）、観測を `done.md` に書く（直さない）。全行を `done.md`「差の表」に「観点・参照・UITK・判定・処理の案」で書く（S3-ladder の差の表の書式）。台帳「## 参照キャプチャ」に「束 2 の最新の参照（S2-ladder）: 撮影元 8a585f742a（Web の src は origin/master 8d1c7fbda9 まで不変）」の 1 行を足す

- [ ] **Step 5: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-stage2-compare.cs docs/uitk/TODO.md
git commit -m "test(uitk): 束 2 の並べ比べを両系で撮るシナリオを足し、束 2 の最新の参照を台帳に書く（S2-ladder）"
git push
```
撮影の PNG は報告ページへ載せた後に消す。

---

### Task 9: 最終の HEAD でラダー 1〜3・段階 2 の録画・Client.Tests を通す（R-3）

- [ ] **Step 1: テスト**: push して `winunity compile uitk-lane/s2-ladder` → `winunity test-all uitk-lane/s2-ladder`（サーバーのテストは含めない。このレーンはサーバーを触らない。R-3 は「Client.Tests 全部」）。Expected: 落ちた名前を S2-skit の実測（`../moorestech_logs/harness/sdd/uitk-lane-s2-skit/workspace/merge-report.md` の落ち名の一覧。5323 件中 65 件が Windows 環境由来・既知）と突き合わせ、一覧に無い落ち 0（このレーン由来 0）。件数・突き合わせた数を `done.md` へ。一覧に無い落ちが出たら、`origin/feature/uitk` の同じ名前を同じ経路で流して土台でも落ちるかを見て、土台でも落ちるなら環境由来として書き、落ちなければこのレーンで直す。L3 の `--editor` の回は全 PASS

- [ ] **Step 2: ラダー**: `--ui-system GameUi` で `uitk/uitk-lifeline.cs`・`uitk/stage2/uitk-opening-hud.cs`・`uitk/stage3/uitk-build-bp.cs` を流す。Expected: 全部 `Success: true`・ErrorLogs 0（lifeline の受け口の安全装置は Task 3 Step 3b で winunity が立てる）

- [ ] **Step 2b: 段階 2 の面に触る録画（plan §2.0 の 2）**: `--ui-system GameUi` で `uitk/uitk-hud.cs`・`uitk/uitk-skit.cs`・`uitk/uitk-skit-choice-pause-cursor.cs`・`uitk/uitk-nested-pause-skit.cs`・`uitk/uitk-tutorial-cues.cs`・`uitk/uitk-mode-hud-and-tooltip.cs`（Task 8 で流した `uitk-notification-compare.cs`・`uitk-skit-compare.cs` はその結果を使う）。GameUi の回は `[PlaytestUi] locator reads only the GameUi UI` のログ行が出ていることも見る（UITK のロケータで解決した証拠。`PauseMenuGameUiTest.cs:116` と同じ文言）。Web 側は、`rg -l "blocking-skit|notification-host|world-pin|key-control-hint|progress-bar|challenge-hud|tutorial-highlight" .agents/skills/unity-playmode-recorded-playtest/scenarios --glob '!uitk/**'` で段階 2 の面に触る Web の録画を列挙し（wwxy の 4 本を含む）、`--ui-system Web` で流して、土台 `9f32a03390` の前の合否（各レーンの done.md と S2-skit の merge-report。記録が無ければ土台のコミットで同じ録画を 1 回流す）と比べて退行 0。Expected: 全部 `Success: true`（Web は CEF が出ない環境なら証拠付きで未確認）。結果を `done.md` の表に 1 本 1 行で

- [ ] **Step 2c: 段階 2 の完了条件の残り（plan §2.3）の証拠**: ADR 0024 の判定役の UITK 版 `Client.Tests/Ui/Screens/Tutorial/Overlay/TutorialHighlightAncestorClipTest`・二重解決のエラー `TutorialAnchorTrackerTest` が Step 1 で PASS したことを `done.md` に名前付きで書く

- [ ] **Step 3: コミットは無し**（記録のみ）

---

### Task 10: moores-code-review で全ブランチレビュー（必須）

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。base は `origin/feature/uitk`。Codex 系統はユーザー指示で実施しない（記録に明記）。設計判断は agent 既定で推奨案を採り、判断記録に「agent 既定（moores-code-review Dn）」と書く（ゴール・裁定から外れるものだけ `done.md`「要裁定」）。自動適用・反映の後は、Task 4 の L3・Task 5〜7 の録画・Task 9 Step 2 と Step 2b の録画のうち、反映が触れた経路のものを反映後のバイナリで流し直してから完了とする（実行計画 §4.3 の 1）。

---

### Task 11: feature/uitk への PR・取り込み・マージ（必須）

セッション終了可能状態にすること: pr-createスキルでPRを作成し、コンフリクトがあれば解消・コンパイル確認のうえpushする。このレーンは実行計画 §4.3 の形（base=feature/uitk、agent がマージ）。

- [ ] **Step 1: PR**: pr-create で `base=feature/uitk`。報告ページ `https://review.moores.tech/pr/<番号>/report/` にラダー 2 の録画（`challenge.current-hud` の枠を含む）・並べ比べ（参照・組 1・組 2）を、載せ先の生成器の書式（`~/hermes-agent/data/services/pr-review/publish/README.md` の manifest の group／caption）で載せ、本文の先頭行をその URL の h1 にする。公開スクリプトが失敗したら報告 URL 無しで完了にしない
- [ ] **Step 2: 取り込み**: `git merge origin/feature/uitk`。I-fix5 がこの間にマージされていて Task 8 の撮影の後なら、文字の観点だけ撮り直して差の表を更新する。共有区画の衝突は両方の行を採る。push → `winunity compile`・`winunity test --regex '^Client\.Tests\.(Ui|UIState|Input)\.'`・G1 ローカル（束 1〜3 未記帳 0）・判定経路に触れた衝突ならラダーの再走
- [ ] **Step 3: G1 とマージ**: PR の `UITK Web follow gate (G1)` が pass を見て `gh pr merge <番号> --merge`（Squash 禁止）
- [ ] **Step 4: 片付け**: `winunity editor-stop <name>` で Windows の GUI Editor を閉じる。録画・PlaytestResults・TestResults・撮影 PNG を消す。bd albof.59 を close（albof.58 も決定の note 付きで close）。worktree の `moores-wt rm` は指揮が行う（依頼文の指示。D-8）
- [ ] **Step 5: 報告と合図**: `done.md` の 1 行目を「状態: 完了」（止まったら「状態: 停止（理由）」）にし、完了条件ごとの証拠・PR URL・未完了・要裁定・integrator への依頼（申し送りの処遇の「回す」行・下の「設計書・実行計画へ反映する行」・winunity の口）を書いて `../moorestech_logs` でコミット・push。最後に `cmux wait-for -S uitk-s2-ladder` を 1 回

## Self-Review

- Requirements の各行のタスク: R-1・R-2→T5、R-3→T9、R-4→T4、R-5〜R-7→T6、R-8・R-9→T7、R-10・R-11→T2（と T11 Step 2 の再確認）、R-12→T8、R-13→T3、R-14→各タスクと T11 Step 5、R-15→T10・T11
- 保留の経路: Web の回で CEF が出ないときは RendererUnavailable の証拠付きで「未確認」（無音にならない）。製品の不具合は直さず bd と done.md「未完了」。`winunity edc` で撮れない見本は done.md「未完了」と組 2 での判定。`--ui-system` は指定の系にならなければ throw（黙って違う系で流さない）。G1 の追従漏れは記帳で 0 にせず bd
- 最小構成: L3 の開幕スキット待ちは既存の `SkipOpeningSkitIfPlaying(30f)`（期限で戻る）の直後の GameScreen の Assert で落ちる（永遠に待たず無音にもならない）。ラダーの `Single` はカテゴリ 1・先頭 1 の今のマスタで成り立ち、形が変われば最初の行で止まる（意図）。
- 型の一致: `Stage2HudSurfaces.All`・`Stage2HudSurface(Name, ByClass, Layer, Family)` は T4 の中だけで使う。マスタの引き方は T5・T6 で同じ名前（`first`・`second`・`pinTutorial`・`treeCandidates`）

## 判断記録（ADR）

従う裁定: `.decisions/2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md`・`2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md`・`2026-11-06-UITK段階2以降の画面もWeb版と同じ見た目にする.md`・`2026-11-06-UITK切替までCEF由来の不具合は直さない.md`・`2026-11-06-UITK一気切替の日にCEFとWebを同時に撤去する.md`・`2026-11-06-CEF停止の記録はUITK長命ブランチから先にmasterへ切り出す.md`・`2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`。依頼文の指示: Codex を使わない・Mac の Editor を起動せず winunity だけ・計画から実装まで同じセッション・問いは agent 既定・worktree の片付けは指揮。

- D-1（agent 既定）: §5 の独立評価は opus の subagent 2 体（Codex の代わり。前例 S3-ladder D-1）。計画は設計検査 2 体（違反 2・強 1）・独立評価 2 体（A: High 3・Medium 7・Low 8、B: High 3・Medium 6・Low 9）・user-simulator（Fable。適用推奨 2・Warning 4・要裁定 1）に通し、指摘を全部反映した。問いは agent 既定へ畳み、ゴール・裁定から外れるものだけ `done.md`「要裁定」
- D-2（agent 既定・user-simulator の予測 (a) と同じ。完了条件の定義の変更なので done.md「要裁定」にも 1 行載せる〈前例 S3-ladder D-3〉）: `100_start_game` に選択肢が無い（selection 0 件）ので、ラダー 2 は「開幕スキットを Enter と窓のクリックで送ってからスキップで抜ける」とし、選択肢は `uitk-skit-choice-pause-cursor.cs`（sample_short）が受け持つ。棄却: 開幕の後に sample_short を起動して選ぶ（背景スキットの終わりを待って時間が延び、既存シナリオと中身が重なる）。plan §4.4 の文言の改めを integrator へ依頼
- D-3（agent 既定）: 目標 HUD の枠は先頭チャレンジの tutorial で、石の斧の 1 打で原木 3 個に届く確率が 15/16 のため伐採で消える。ラダー 2 は枠を採掘の前に見る（plan §4.4 の並びと順番が違う。依頼に含める）
- D-4（agent 既定）: ラダー 2 は `scenarios/uitk/stage2/`、L3 は `EditModeInPlayingTest/Ui/Stage2/`（`scenarios/uitk/` 直下 27 本・`EditModeInPlayingTest/Ui/` 直下 .cs 11 本が 10 超。前例 S3-ladder D-4 の `stage3/`・S4-gamma の `Stage4/`）。S4-gamma 計画のラダー 2 のパスの読み替えを依頼
- D-5（agent 既定）: 束 2 の暗転の起点は S2 の 5 レーンの分岐点で取り込み済みの master のうち最も古いもの（起草時の見込み `0e81bbe3c3`。Task 2 Step 1 で実物を確かめる）。束 1・3 の残りの未記帳 6 件も同じ処遇で記帳する（同じ種類〈生成物の再生成〉で、取り込みの PR が 0 件を見る運用のため）
- D-6（agent 既定）: L3 の「消える」は Ctrl+U の全体非表示（S2-skit が入れた本物の根の隠し）と UI 状態の遷移（PauseMenu・DeleteBar）の両方を必須にする。blocking スキットで隠れる経路は store を直に動かすので L1 と重なり入れない
- D-7（agent 既定・条件付き）: DeleteBar へ `RequestTransition` で直接入れないと実測したら、DeleteBar の行だけを外し理由と実測をここへ書く
- D-8（agent 既定）: §4.3 の 5（`moores-wt rm`）は依頼文の「worktree の片付けは指揮が行う」に従い、このレーンは Windows の GUI Editor を閉じるまで
- D-9（agent 既定）: `tutorial-equip-challenge` は装備のチャレンジが ADR 0038 で消えて役目が終わった。残る意図（キー操作ヒントとクラフトの誘導）は 2 番目のチャレンジ（木の板）へ移ったので、そこへ用途を替えて `tutorial-craft-guidance.cs` に改名する。研究の説明文の確認は外す（詳細欄はノード選択の後にしか出ず、`uitk-research-tree.cs` が両系で見ている）。棄却: 削除（木の板の itemViewHighLight と `recipe.craft-button` の枠の実走がどこにも残らない）
- D-10（agent 既定）: `tutorial-pebble-challenge` は先頭チャレンジの連鎖へ置き換え `tutorial-opening-log-challenge.cs` に改名。`tutorial-tree-pin-nearest-search` は木をサーバーで `Destroy()` してピンを生かしたまま追従を見る（伐採すると 1 打で先頭が終わりピンが消えて (c) が観測できない）
- D-11（agent 既定・独立評価 A の M2・M3）: 並べ比べの参照は撮り直さない（Web の src は撮影元 `8a585f742a` から origin/master `8d1c7fbda9` まで不変）。組 1 は全状態を本番の層に載せて撮る（キー操作ヒント・ピンは Presenter の `Mount` と L1 の Fake、スキットは `SkitTestData`）。組 1 で撮れない状態だけ理由を書き組 2 で判定し、道具の拡張を integrator へ依頼する（compare-procedure §3）
- D-12（agent 既定）: winunity の口（`--ui-system`・`edc`・`test-all`）はマシン固有ツール側に足す（run-scenario.sh 側の環境変数案は repo のスキル同梱スクリプトを変え、Mac の運用にも波及するため棄却）
- D-13（agent 既定・Phase 2.6 の 7-1 への裁定）: ADR 0029 決定1・決定4 は ADR 0038 決定2 で役目を終えたが 0029 に置き換えの表記が無い。ADR の本文はこのレーンでは書かず、0029 に「決定1・4 は 0038 で廃止」の注記を足す案を integrator へ依頼する（R-6 の用途替えは 0038 に従う）
- D-14（agent 既定・独立評価 B の H2）: 波の並べ比べは設計書 [A-179] に従い、両系で撮る組を足し、差は処理の案だけを書いて指揮の判定へ回す（このレーンは USS を直さず、台帳の LX の行と印を置かない）。前例 S3-ladder は差を自分で記帳したが（S3-ladder D-13）、設計書の方が上位で、並走中の I-fix5 と束 2 の USS で衝突するのも避けられる
- D-15（agent 既定・独立評価 A・B の M4・M1）: I-fix5 が触っているファイル（Notification.uss・Typography.uss・TooltipFace.cs・LocalizedLabel.cs・字形の資産・`scenarios/uitk/` の配置・`UiRuntimePanelCapture` のシグネチャ・台帳の割り振りの行）は、取り込みの状態を見てから書く（Task 5 Step 0・Task 7 Step 3・Task 8 Step 0）
- unityプレイ録画テスト: 含む（Task 5・6・7・8・9）

## 設計書・実行計画へ反映する行（integrator への依頼の案。このレーンは書かない）

- A-230: ラダー 2 `uitk-opening-hud` の置き場 `scenarios/uitk/stage2/` と中身（D-2・D-3 の 2 点で plan §4.4 と違う）。plan §4.4 の段階 2 の行を「オープニングのスキットを送ってスキップする → 目標 HUD を指すチュートリアル枠が出る → 歩いて伐採する（ゲージと獲得通知）→ 数字キーで選んで設置する」に改める。S4-gamma 計画の `scenarios/uitk/uitk-opening-hud.cs` を `scenarios/uitk/stage2/uitk-opening-hud.cs` へ読み替える
- A-231: 段階 2 の L3 `EditModeInPlayingTest/Ui/Stage2/Stage2HudGameUiTest`（載り・遷移・Ctrl+U）
- A-232: wwxy の改名 2 本（`tutorial-opening-log-challenge.cs`・`tutorial-craft-guidance.cs`）
- 実行計画 §1.4 の S2-ladder 行: D-13 の除外の但し書きは不要になった（受け手が写し済み）・「codex-audit」を「独立評価（opus 2 体）」に読み替えた記録
