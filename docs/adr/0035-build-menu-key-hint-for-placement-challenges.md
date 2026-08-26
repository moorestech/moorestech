# 0035: 設置チャレンジにビルドメニュー(B)のkeyControlチュートリアルを足す

- Status: Accepted
- Date: 2026-08-26

## Context

チュートリアル進行で初めてブロックを設置するのは「風力掘削機を設置する」チャレンジ
(`a6497c0b-82eb-5280-82c7-d339bc32de14`)。その summary は
「Bでビルドメニューを開き、風力掘削機をホットバーへドラッグして粘土鉱脈の上に設置しよう」だが、
`tutorials` は `veinPin`（粘土鉱脈へピン）と `uiDragGuide`（ビルドメニューのエントリ→ホットバー）の2件だけで
**`keyControl` が無い**。結果、中央下のキーヒントHUDに `[B]` が一度も出ず、
ビルドメニューを開く手段だけが誘導から抜けている。後続の「石窯を設置する」
(`603e84c0-10b1-501f-a03d-598584d34d58`) も同じ形（summary は B に言及、tutorials は uiDragGuide のみ）。

調査で確定した事実:

- 機構は既存で足りる。`keyControl { uiState, keyName, controlText }` はスキーマ・クライアント
  (`KeyControlTutorialManager`)・Web側HUDまで実装済みで、前例は「原始研究1」(`R`×2件) と
  「石器/石の斧を装備する」(`Tab`)。**C#・スキーマの変更は不要で、master データのみで完結する。**
- `B` でビルドメニューへ遷移できるのは `GameScreenState` と `DeleteObjectState`(=`DeleteBar`) のみ。
  `PlayerInventoryState` は Tab/ESC/R しか受けない。
- ADR 0032 により左下HUDには常時「B ビルドメニュー」が出ている。チュートリアルの keyControl は
  中央下の別枠(`KeyControlHintHud`)なので二重表示になるが、これは Tab・R でも既にそうなっている既定の姿。
- `challenges.json` は `moorestech_master/tools/tutorial_v3_port/generate_challenges.py` が生成する。
  再生成して現行コミットと突き合わせた結果、差分は末尾改行1行のみで、スクリプトが正本として生きていることを実測確認した。
- `tutorialGuid` は `uuid5(NS, "tutorial-v8-slot:<challenge key>#<slot index>")`、
  すなわち **配列内のスロット位置から導出される**。既存要素の前に挿入すると後続の GUID が全て変わり、
  `moorestech_master/.../localization/localization.csv` の
  `challengeTutorial.<guid>.text` 行が孤児になる。

## Decision

- 「風力掘削機を設置する」「石窯を設置する」の両チャレンジへ
  `keyControl { uiState: GameScreen, keyName: "B", controlText: "ビルドメニューを開く" }` を追加する。
  出所: ユーザー裁定 2026-08-26 原文「風力採掘機の設置チュートリアルで、Bキーのチュートリアルを出したい」
  → 選択「GameScreenのみ」「ビルドメニューを開く」「石窯にも同じBヒントを足す」
  (`.decisions/2026-08-26-設置チャレンジのBキーヒントはGameScreenのみで石窯にも付ける.md`)
- **uiState は GameScreen 単独**。ビルドメニューを開いた瞬間に [B] が消え、入れ替わりで既存の
  uiDragGuide が出る。破壊モード(DeleteBar)・ビルドメニュー(BuildMenu)向けのエントリは作らない。
  出所: 同上（棄却案は `.decisions/` に記載）
- **既存 tutorials の末尾へ追加する**（風力掘削機は slot 2、石窯は slot 1）。先頭・中間への挿入は
  スロット由来 GUID をずらして既存の localization 行を孤児にするため採らない。
  出所: agent前提（`tutorial_guid_for` の導出規則。表示は3種とも別ウィジェットで配列順に依存しない）
- 変更は `generate_challenges.py` の定義表へ `key('GameScreen', 'B', 'ビルドメニューを開く')` を足し、
  スクリプトを再実行して `challenges.json` を再生成する形で入れる。JSON を直接手編集しない。
  出所: agent前提（生成物の正本はスクリプトであることを再生成一致で実測確認済み）
- 新規 `tutorialGuid` 2件ぶんの `challengeTutorial.<guid>.text` 行を
  `moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv` へ追加する。
  english は `Open the build menu`。
  出所: agent前提（english 起案は ADR 0029 と同じ扱い）

## Considered Options

- **uiState を GameScreen + DeleteBar にする** — B は DeleteObjectState からもビルドメニューを開けるので
  操作としては正しいが、この時点の進行で破壊モードに入る動線が無く、エントリが増えるだけ。
  棄却（ユーザー裁定 2026-08-26）
- **uiState を GameScreen + BuildMenu にする** — 開いた先でも [B]（閉じる）を出し続ける案。
  ドラッグガイドと同時に中央下へ出て「閉じろ」と誤読されうる。棄却（同上）
- **文言「ビルドメニューから風力掘削機を選ぶ」／「ビルドメニューを開いて風力掘削機を出す」** —
  直後にドラッグガイドが同じ内容を示すため冗長。棄却（同上）
- **風力掘削機だけに足し石窯は現状維持** — 石窯側の summary/tutorials 不一致が残る。棄却（同上）

## Consequences

- 変更は `moorestech_master` 側のみ（`generate_challenges.py` / `challenges.json` / `localization.csv`）。
  本 repo は `.moorestech-external-revisions.json` のピンを、master 側 PR の push 済みコミットへ更新する。
- `challenges.json` の再生成で末尾改行1行の差分が同時に入る（現行ファイルは MooresEditor 保存由来で
  末尾改行が無い）。生成スクリプトの出力を正とする。
- 実装時、`localization.csv` の挿入位置は当初想定した uiDragGuide の翻訳行の直後ではなくなっていた。
  上流（master data `274b6d9f` 系列）で uiDragGuide の無用な翻訳行が削除済みだったため、
  風力掘削機は自チャレンジの veinPin 行の直後、石窯は直前チャレンジ（原始研究4）の行の直後へ入れた。
  `tutorialGuid` 自体は新旧どちらのベースでも同一であることを実測確認している。
- 進行中の独語ロケール作業（ADR 0034）が master の localization.csv へ german 列を足すため、
  本変更で追加する2行にも後から german 値が要る。列追加側とマージ順に注意する。
- 検証は unity プレイ録画テストで「チャレンジ開始時に中央下へ [B] ビルドメニューを開く が出る →
  B を押すと消えてドラッグガイドへ入れ替わる」を確認する。
