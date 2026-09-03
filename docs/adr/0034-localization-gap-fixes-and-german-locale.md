# 0034. 生リテラルをローカライズ経路へ載せ、誤訳を正し、ドイツ語ロケールを新設する

日付: 2026-08-25
状態: 採択

## Context

日本語→英語の翻訳漏れ・翻訳ミスを洗い出し、あわせてドイツ語を追加したいという依頼から調査した。

機械判定では **「英訳が空欄・日本語のままコピペ」型の翻訳漏れは0件**だった（`Localization/localization.csv` 173行と `moorestech_master` mod の `localization.csv` 422行、english列への日本語混入0・空欄0・english==japanese 0）。実際の問題は3系統に分かれる。

1. **ローカライズ経路に載っていない生の日本語リテラル**が14箇所ある。ローディング画面の進捗ログ10行（`InitializeScenePipeline` / `ServerConnectionInitializer` / `ModAssetLoader` / `ModAssetIconLoader`）と、メインメニューのサーバー接続エラー4件（`ConnectServer`）。
2. **明確な誤訳・誤字が5件**ある。日本語側の誤字2件（「目を冷まして」「ICチップ基盤」）は英語側が正しく、英語からの逆照合で初めて露見した。
3. 表記スタイルの不統一（英語の大文字化・末尾ピリオド、Liquid/Fluid混在、日本語の「持ち物」/「インベントリ」割れ）が658行全体に散在する。

なお調査は当初 `fix/skit-ground-position-3x3` 上で行い、planning直前に master 基準で取り直したところ2つの前提が崩れた。**電線プレビューの失敗理由・「電線 x{n}」ラベルは master ではすでに `ui.tooltip.placeWire*` としてローカライズ済み**（`ElectricWirePlacementFailureTooltipKey` / `ElectricWireFeedbackLines`）で、**キャラクター名の日英不一致も master では解消済み**（`character.*.name` の japanese 列が「ヨリ／エレノ／クルア」）。両者は本ADRのスコープから外した。

なお `WebUiScreenGate.IsWebUiMode => true` 固定によりuGUIビューは恒久非表示で、そこに残る日本語直書き約40箇所は表示されない。翻訳対象ではなく削除待ちのデッドコードである。

## Decision

### スコープ

- **今回やる**: (a) 生リテラル14箇所のローカライズ化、(b) 明確な誤訳・誤字6件の修正、(c) ドイツ語ロケール新設。
  出所: ユーザー裁定 2026-08-25 選択「生リテラルのローカライズ化（約25箇所）」「明確な誤訳・誤字の修正6件」＋確認質問「含む（Q1は選択漏れ）」（ドイツ語）
- **今回やらない**: 表記スタイルの595行一括統一、恒久非表示uGUIの日本語直書き約40箇所、スキットエディタ辞書（`Skit/i18n/english.json`）の日本語残存2件・欠落71件・キー食い違い1件。いずれも bd issue に積む。
  出所: ユーザー裁定 2026-08-25 選択「どちらも今回は触らず bd に積む」

### 英語の表記規約

- **ラベル・タイトルは Title Case、説明文は sentence case + 末尾ピリオド。** ボタン／タブ／研究名／チャレンジtitle が前者、summary／description が後者。
  出所: ユーザー裁定 2026-08-25 選択「ラベル・タイトルはTitle Case、説明文はsentence case+ピリオド」
- **適用範囲は今回新設・修正する行のみ。** 既存595行の一括揃えはやらない（差分を小さく保つ）。
  出所: ユーザー裁定 2026-08-25 選択「今回新設・修正する行にだけ適用」

### 生リテラルのローカライズ化

- 電線プレビューについては「`ui.wirePreview.*` として別キー群を新設し通知キーとは共有しない」と裁定したが（ユーザー裁定 2026-08-25 選択「別キー群を新設（ui.wirePreview.*）」）、master では同じ判断が `ui.tooltip.placeWire*` としてすでに実装済みだったため、**この裁定に対応する作業は発生しない**。裁定の向き（プレビューと通知で文言を分けて持つ）は master の実装と一致している。
- **ローディング画面の進捗ログは全行ローカライズする（`ui.loading.*`）。** プレイヤーが実際に目にする画面なので他のUIと同じ扱いにする。経過時間はプレースホルダで渡す。
  出所: ユーザー裁定 2026-08-25 選択「全行ローカライズする（ui.loading.*）」／棄却: 開発向け診断表示として英語固定・単一のステータス文へ置換
- メインメニューのサーバー接続エラー4件は `ui.mainMenu.*` へ（agent前提: 既存 `ui.mainMenu.serverIpPlaceholder` 等と同じ名前空間）。

### 誤訳・誤字の修正

| 対象 | 修正 |
|---|---|
| `skit.100_start_game.33` | 日本語「いい加減目を**冷ま**してください」→「目を**覚ま**して」 |
| `item.019e3b03-….name` | 「ICチップ基**盤**」→「基**板**」 |
| `skit.100_start_game.17` | 英語 "**broken** promises and jokes" → 原文にない "broken" を削除 |
| 回転生成機（電力→回転） | **英語のみ改名。** `Rotation Generator` → Electric Motor 系へ。日本語は据え置き。回転発電機（回転→電力, `Rotary Generator`）との取り違えを断つ |
| スマート分岐器 | **ブロック名に寄せる。** 研究名を日英とも「フィルター分岐器 / Filter Splitter」へ |

出所: ユーザー裁定 2026-08-25 選択「英語のみ改名（回転生成機→ Electric Motor 系）」「ブロック名に寄せる（研究名→フィルター分岐器）」
キャラクター名については「日本語はカタカナ、英語はラテン」と裁定したが（ユーザー裁定 2026-08-25 選択「日本語はカタカナ、英語はラテン」）、master ではすでにその形になっており作業は発生しない。
棄却（Generator衝突）: 日英両方改名・回転発電機側を Gear Dynamo 等へ改名・今回は触らない
棄却（分岐器）: 研究名に寄せる（ブロック→スマート分岐器）・今回は触らない

Electric Motor 系の具体名は `Electric Gear Motor` とする（agent前提: 既存アイテム「モーター / Motor」と衝突せず、電力→歯車動力という機能が読める）。

### ドイツ語ロケール

- **`Localization/localization_settings.csv` に1行、両CSVに `german` 列を追加する。**（対象は vanilla 233行 + mod 425行 = 658訳文） 言語リストはCSV駆動でハードコードが無く（`LocalizationLanguageContract` は english 列の存在のみ必須化）、コード変更は原則不要（agent前提: 調査結果）。`steam_api_lang_code` は既存の `en`/`ja` に倣い `de`、`display_name` は `Deutsch`（agent前提: 既存2行の前例）。
- **658行すべて私（Claude）が英語列を原文として全訳し、`codex-audit` でドイツ語ネイティブ視点のレビューを1周かけてから確定する。** スキット本文47行も訳す。
  出所: ユーザー裁定 2026-08-25 選択「私が全訳し、Codex外部監査で相互チェック」／棄却: 暫定品質としてそのまま出す・UI文言のみ訳しスキット本文は空欄でenへfallback・列と設定行だけ用意し訳文は入れない
- **監査と割れた箇所は私が裁定し、判断がつかないものだけ列挙して提示する。** 明らかな誤りは取り込み、好みの問題は私の案を残す。残った争点はPR本文に書く。
  出所: ユーザー裁定 2026-08-25 選択「私が裁定し、判断がつかないものだけ列挙して提示」／棄却: 監査の指摘は全部取り込む・割れた箇所は全件ユーザー裁定へ
- **スキット本文の人称は Sie（敬称）+ Prinzessin。** AIは仕える側なので、日本語の敬体・英語の丁寧さをそのまま写し取る。
  出所: ユーザー裁定 2026-08-25 選択「Sie（敬称）+ Prinzessin」／棄却: du + Prinzessin・UIはduでスキット本文はSie
- **`dictionaryIndependentText.ts`（辞書ロード失敗時のフォールバック7件）は英日併記のまま残す。** 辞書が読めない非常時の文言で、言語を増やすと際限なく長くなるため、ここは意図的にドイツ語を持たない。
  出所: ユーザー裁定 2026-08-25 選択「英日併記のまま残す」／棄却: 英語のみに絞る・英日独の3言語併記

### スキットエディタ辞書 `german.json`（ドイツ語追加の必須随伴物）

`SkitLocalizationDynamicLoadContractTest.AddressableSettingsContainOnlySupportedSkitDictionaryAddresses` が `LanguageCatalog.Languages`（= `localization_settings.csv` 由来）と `Vanilla/Skit/i18n/*` の Addressable エントリの**完全一致**を要求するため、ドイツ語を追加すると `Skit/i18n/german.json` の新規作成と Addressable 登録が強制される。「スキットエディタ辞書は今回触らない」というスコープ裁定はこの新規作成には及ばない（既存 `english.json` の穴埋め＝`moorestech-9ls3` とは別物）。

- **`english.json` を複写し、`locale` を `de`・`name` を `Deutsch` にするだけとする。** 翻訳文（`translations`）は英語のままにする。スキットエディタのコマンド名・説明文はプレイヤー非露出であり、独語化しなくても実害がない。
  出所: ユーザー裁定 2026-08-25 選択「english.json を複写し locale/name だけ独語にする」／棄却: 152件もドイツ語へ全訳する・この機会に english.json の穴埋め（moorestech-9ls3）も一緒にやる
- Addressable への登録は `AddressableAssetGroup` の `.asset`（Unity固有YAML）編集にあたるため、手編集せず `uloop execute-dynamic-code` 経由で行う（agent前提: AGENTS.md「Unity固有ファイルの直接編集禁止・uloop execute-dynamic-code は正規ルート」）。

### 検証とPR

- **ドイツ語によるUIレイアウト崩れの事前検証はしない。** 複合語で文字列が伸びてボタン・タブが崩れる可能性は認識した上で、まず入れて出し、崩れたら報告ベースで後日直す。
  出所: ユーザー裁定 2026-08-25 選択「検証しない（崩れたら後日直す）」／棄却: プレイ録画テストで主要画面を独語で通す・文字数上限を機械チェックして長い訳は短縮する
- **PRは本repo1本 + `moorestech_master` 1本の計2本。** それぞれに生リテラル・誤訳修正・ドイツ語をすべて含める（行ったり来たりを避ける）。
  出所: ユーザー裁定 2026-08-25 選択「本repo1本 + master1本（内容は全部含む）」／棄却: 修正系とドイツ語で分けて計4本
- `.cs` を触るのでコンパイル必須。ドイツ語追加で更新が必要になる既存テストは以下（agent前提: master 上で実測）。
  - `Client.Tests/Localization/Resolution/LocalizeTest.cs:104` — `CollectionAssert.AreEqual(new[] { "english", "japanese" }, ...)` が言語集合をリテラル固定している
  - `Client.Tests/Localization/Skit/SkitLocalizationDynamicLoadContractTest.cs` — Addressable エントリと `LanguageCatalog.Languages` の完全一致（`german.json` 追加で解消）
  - `moorestech_web/webui` の `src/shared/i18n/generated/localizationKeys.ts` — 新規キー追加後に `pnpm gen:i18n` で再生成しないと `localizationKeysFreshness.test.ts` が落ちる
  - `SkitLocalizationDictionaryCompletenessTest` は `[TestCase]` で english/japanese を明示列挙しており、german を足しても自動では失敗しない（`german.json` 用の baseline 追加は不要）

## Considered Options

主要な棄却案は各決定の「棄却」行に併記した。

## Consequences

- 電線まわりの文言が `ui.notification.electricWireExtend*`（通知）と `ui.wirePreview.*`（プレビュー）の2系統になる。同じ失敗理由に2つの文言が並ぶため、片方だけ直す取りこぼしが起こりうる。同じ失敗理由enumから両方が引かれる構造にして、追加時に両方が視界に入るようにする。
- ドイツ語列が入った時点で、以降 `ui.*` キーを新設するたびに3言語ぶんの記入が必要になる。
- 表記スタイルの不統一は今回の修正行にだけ規約が効くため、既存行との混在が一時的に残る。
