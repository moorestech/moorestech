# レビュー: テスターとオーナーの負担・運用（Fable）

- 対象: `docs/uitk-migration/drafts/plan-v1.md`（2026-10-23 版）
- 実測の元: repo master `e8e611f1c0`（読むだけ）、`~/hermes-agent/data/services/pr-review/state/poller.log`、`~/hermes-agent/data/services/playtest/runs/*/released.json`、`gh pr list`（2026-10-23 時点）
- 指摘 10 件。重要な順。

## 指摘

### 1. 誤り／欠落 — 「Windows 検証機の通し検証」は UI を一切開かない（§2.0 条件 6、§4.9）
- 根拠: `moorestech_client/Assets/Scripts/Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeRunner.cs:67-75`。phase1 ＝ ハンドシェイクの現在チャレンジ数で「チュートリアル開始」＋セーブ書き出し、phase2 ＝ バグ報告の書き出しとアップロード。画面は開かない。Mac の確認は `.agents/skills/moores-playtest-release/SKILL.md` 手順 4-4「Tab でインベントリが開く」で、段階 4 まで Web の画面を見ている。§8.4「段階ごとの短い目視」は staging で反映前か、反映後かが書かれていない。
- 何が起きるか: 段階 1・2・3・5 で、UITK の面（SDF フォントの同梱、`UiAssetCatalog` の起動時ロード、PanelSettings の倍率、Linear の半透明）が配布ビルドで一度も観測されないまま playtest へ反映される。配布ビルドだけで起きる欠落（stripping・未同梱）はテスターが最初に見つける。段階 1 には人手の CEF 停止確認があるが、2・3・5 には無い。
- 最小の直し方: §2.0 条件 6 の Mac 確認項目を「Tab でインベントリ」から「その段のラダー先頭の操作のスクショ（段階 1: Esc でポーズ、2: 採掘ゲージと通知、3: B でビルドメニュー、5: チャレンジ一覧）」に置き換える（agent が cua-driver で行う既存工程）。§8.4 の目視は「staging ビルドで、反映の前」と書く。

### 2. 過剰／順序 — 全 PR に pr-independent-review を通すと、律速はマージでなくオーナーの裁定になる（§3.13 レビューとマージ、§6 #10、D20）
- 根拠: poller.log の実測。レビュー 1〜1.5h（pr-1457 17:59→19:37）、裁定待ち（人）0.5h〜3.5 日（pr-1388 09-20 19:49 → 09-24 02:10、pr-1442 22:13 → 翌 11:50）、apply 0.5〜2.5h で Editor スロットを使う（`services/pr-review/README.md`「Mac miniのEditorメモリ制約から2本」）。直近 40 本のマージ済み PR（#1507〜#1549）に独立レビューのラベルは 0 本、open→merge の中央値 0.7h・最大 31.7h。ラベル基準は `docs/development/macmini/worktree-and-automation.md:42-46`「非常に重いので乱発しない」。
- 何が起きるか: 55〜70 本の PR ごとにオーナーの裁定が挟まり、波の 4〜6 本が全部裁定・apply を抜けるまで integrator の切替 PR が出せない。apply の Editor 2 本が実装の 3 本と同じメモリを取り合う。§3.13「律速は…ユーザーのマージ」は実測と合わない（マージは速い）。
- 最小の直し方: 独立レビューを「切替 PR」と「段階 1 の基盤 PR（共有ファイルを持つもの）」だけに付ける。暗転の単位 PR は moores-code-review までにする。§3.13 と §6 #10 の「律速」を「マージ」から「独立レビューの裁定」に改める。

### 3. 誤り — Editor の本数が計画の中で合っていない（§2.1 規模と並列、§2.3、§3.13 Editor の予算）
- 根拠: §2.1「4〜5 本で並列（Editor 3）」＋「実測は別に Editor を 1〜2 本使う」＝ 4〜5 本。§2.3 も「並列は 4 本（Editor 3）」。pr-apply スロットは apply のたびに Editor を起動する（`.agents/skills/pr-adjudicated-apply/SKILL.md:202`）。配布ビルドは同じ Mac mini で batchmode の Unity を Win/Mac の 2 回回す（`scripts/playtest/release-playtest.sh:129`、約 70 分）。指針は `CLAUDE.local.macmini.md:36`「同時 Editor 数は 2〜3 本を目安」。
- 何が起きるか: 段階 0 の初日から目安を超える。Editor が黙って消える・テストがタイムアウトする（`docs/development/macmini/unity-editor.md`「Editorがログを残さず消えた」）と、エージェントは自分のバグか資源不足かを分けられず、ラダーと録画のゲートが信用できなくなる。
- 最小の直し方: §3.13 の表に「実装に使える Editor ＝ 3 − apply 稼働数 − 配布ビルド中の 1」と書く。S1〜S15 は波の並列を始める前（0-2 を段階 0 の先頭に置く）か macbook-pro で回す。§2.1 の「4〜5 本」を「Editor 要 2 本＋Editor 不要 2 本」に直す。

### 4. 欠落 — 「1 周期」の長さと起点が未定義（§3.3 R3、§2.5 切替後の流れ）
- 根拠: 反映はユーザーの指示でだけ行い（SKILL.md 手順 5）、間隔は不定期。`runs/*/released.json`: 10-15 16:35、10-17 10:11、10-21 21:50（2〜4 日間隔）。計画と shipping §1.5 は「毎週」と書く。削除 PR は「playtest で 1 周期通ったら」（R3）、戻しは「切替 PR の 1 行」（§2.5 リスク 1、§6 #7）。
- 何が起きるか: 10-15→10-17 のように 2 日で次の反映が出ると「1 周期」が 2 日で明け、削除 PR で Web 実装が消えた後にテスターの報告（Discord の報告は遅れて来る。10-02 の報告は 6 時間放置の後）が届き、1 行の戻しが効かなくなる。
- 最小の直し方: R3 に「1 周期 ＝ 切替を含むビルドの反映から 7 日以上、かつその間の Discord と受け口の報告を 1 回見た後」と書く。

### 5. 過剰 — 全段階の終わりで「録画シナリオ全本緑」（§2.0 条件 3）
- 根拠: シナリオは 90 本（`.agents/skills/unity-playmode-recorded-playtest/scenarios` の .cs を実測）。一括実行の道具は無く（scripts は `run-scenario.sh` だけ、CI の workflow に参照無し）、1 本に preflight 30 秒＋ready 15〜30 秒＋本体（`references/run-scenario.md:30`）。壊れているのは bd `moorestech-wwxy` の NOTES で 10 本（計画は 8 本）。
- 何が起きるか: 段階末 6 回＋段階 4 の切替前の計 7 回、1 回 3〜5 時間の Editor 占有を 1 本ずつ手で回す。Editor が足りないと部分実行で「緑」と報告される（AGENTS.md「完了通知をすぐ信じない」が前提にする型）。
- 最小の直し方: 全本は段階 1 の終わり（入力層が変わる）・段階 4 の切替前・段階 6 の 3 回に限り、他の段は「移した面に触る 41 本＋ラダー」に絞る。対象外を 10 本に直す。

### 6. 過剰（まとめる）— D14 の grill を 3 回に分けている（§8.2 D14）
- 根拠: D14 の期限欄「段階 1（ポーズ・トースト）、段階 2（列車 HUD）、段階 5（チャレンジ）」。AGENTS.md の grill は HARD GATE で、オーナーのインタビューが要る。
- 何が起きるか: 3 つの段の着手がそれぞれオーナーの時間待ちになる。語彙は ref-webui-design のホワイトリストで共通なので、段ごとに分ける理由が無い。
- 最小の直し方: D14 を段階 0 前の D1〜D9 と同じ 1 回の grill にまとめ、「Mantine 素の 4 画面を同じ語彙で様式化する」を一度に裁定する。

### 7. 順序／欠落 — 段階 0 の IME 確認（S9）が、非 master ビルドの staging 占有とオーナーの RDP に依存し、赤信号で段階 1 を止める（§4.8 S9、§2.1 完了条件、§8.4）
- 根拠: UITK の TextField は PoC ブランチにしか無い（§7.4）。配布ビルドは `release-playtest.sh <SHA>` で 1 本の `playtest-staging` へ上がる（`.decisions/2026-09-24-…playtest-staging…md`）。Windows 検証機の手動確認は Remote Desktop（`scripts/playtest/README.md:186`）。§2.1「S9 が成立しない場合は、段階 1 に入らず裁定に上げる」。
- 何が起きるか: S9 のための PoC の 70 分ビルドが staging を占有し、その週の配布検証と重なる。オーナーの RDP の枠が取れない間、命綱（段階 1）が待つ。
- 最小の直し方: S9 の実機確認はオーナーの週次の反映作業と同じ日に置き、PoC ビルドはその日の反映の後に staging へ上げる。Mac 側は段階 1 のバグ報告本文の確認と兼ねる（§4.9 の 4 回を 3 回にする）。

### 8. 欠落 — パッチノートの告知が段階 1 にしか無い（§2.2〜§2.6 出荷）
- 根拠: §2.2「パッチノートに『UI 移行中』と書く」。§2.3・§2.4・§2.5・§2.6 は「出荷: 可」のみ。パッチノートは moores-playtest-release 手順 3 の既存工程。段階 2〜5 は通知が Web の全画面の後ろに隠れ（§2.3 リスク 4）、段階 3 は装備 HUD（Web）とホットバー（UITK）の床ずれ（§2.4 リスク）、段階 4 は退行幅が最大（§2.5 リスク 1）。
- 何が起きるか: テスターが「通知が消えた」「見た目が違う」を不具合として報告し、Discord と受け口が既知差分で埋まる。受け口から無人で動く bug-report-auto-fix が既知差分を拾って修正 PR を出す。
- 最小の直し方: §2.0 条件 6 に「パッチノートに、その段で UITK に移った画面と、1 周期の既知差分（通知の位置など）を書く」の 1 行を足す。

### 9. 欠落 — 戻すときの配布が書かれていない（§2.5 切替後の流れ、§6 #7）
- 根拠: 戻しは「切替 PR の 1 行」。配布は約 70 分のビルド＋検証機＋Mac 確認＋ユーザーの反映指示で、人の指示が「ビルドして」「反映して」の 2 回要る（SKILL.md 手順 1・5）。
- 何が起きるか: 段階 4 の切替で退行が出たとき、1 行戻しても次の反映までテスターは退行の中。オーナーが反映を指示しなければ戻らない。
- 最小の直し方: §6 #7 の潰し方 (3) に「戻しの PR は当日中に通常の配布工程で反映を指示する（Mac 確認は `--skip-mac-check` 可）」と書く。

### 10. 過剰 — 凍結の前提「進行中の Web の PR は完了させてから」は現時点で空（§2.1 0-9、§6 #8）
- 根拠: open PR は #1348（ベルトコンベア張替え、41 日経過）の 1 本だけで Web ではない（`gh pr list` 実測）。10-15 以降の 44 コミットはマージ済み。
- 何が起きるか: 待つ理由が無いのに凍結の開始が遅れる。
- 最小の直し方: 「2026-10-23 時点で進行中の Web の PR は無い。G1 は段階 0 の最初の PR で入れる」に書き換える。

## 確認したこと（指摘ではない）
- `.decisions/2026-09-24-…playtest反映はWebUIで手動実行する.md` の「WebUI」は Steamworks の Web UI で、CEF の Web UI とは別物。現在は `promote_playtest.py`（`SetAppBuildLive`）で API 化済み（SKILL.md 手順 5「Steamworks Web UI での手作業は不要」）。計画 §2.0 条件 6 の「手動で playtest へ反映」は「ユーザーの反映指示」の意味で成り立つ。
- セーブ形式には触れない。言語は C# の PlayerPrefs（`Client.Localization/Localize.cs:174-175`）で両系が共有し、変更は `Localize.OnLanguageChanged` → `Client.WebUiHost/Game/Topics/LocalizationTopic.cs:43` で Web に届く。バグ報告の下書きはポーズ内限定（ADR 0069）で、UITK でも同じ寿命でよい。
- 言語は english／japanese／german／korean の 4（`Localization/localization.csv` ヘッダ）。フォントの JP・EN・KR で足りる。
- 日次ビルド（`.github/workflows/build.yml` cron）が Win/Mac を焼くので、G7（`#if UNITY_EDITOR` の閉じ込め）は日次で捕まる。

## この観点での総評
- テスターの実害を最初に減らす順序（命綱→HUD→束）は正しいが、配布ビルドで UITK の面を見る工程が無く、段階 2・3・5 の退行はテスターが最初に見つける形になっている（指摘 1）。
- オーナーを止める箇所はマージではなく、独立レビューの裁定・grill・IME の RDP の 3 つで、いずれも回数を減らせる（指摘 2・6・7）。
- 並列は Editor の勘定を直し、「1 周期」を日数で定義すれば回る（指摘 3・4・5）。
