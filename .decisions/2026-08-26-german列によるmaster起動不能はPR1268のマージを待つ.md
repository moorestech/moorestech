# german列によるmaster起動不能はPR #1268のマージを待つ

## 決定
master `8e4b3bea3` が指す master data ピン `200ab3c9` にドイツ語列が入り、
クライアントが `Unsupported localization language: german` で初期化失敗する件は、
**クライアント側のドイツ語対応 PR #1268 のマージを待って解消する**。
Releaseビルドと出展モードの通し録画も、そのマージ後にやり直す。

出所: ユーザー裁定 2026-08-26 選択「PR #1268（ドイツ語対応）のマージを待つ」

## 症状
Release Player 起動時、`ModLocalizationMerger.cs:77` が mod の localization.csv の
未知言語列を見て `LocalizationCsvException` を投げる → `GameShutdownEvent` → メインメニューへ戻る。
ワールドは削除されたまま再生成されない。

- master pin `274b6d9f` の mod csv ヘッダ: `key,Source,english,japanese`
- master pin `200ab3c9` の mod csv ヘッダ: `key,Source,english,japanese,german`
- 本repo master の `Localization/localization.csv` ヘッダ: `key,Source,english,japanese`

moorestech_master#42（german追加）が先にマージされ、moorestech#1268（クライアント対応）が
未マージのまま本repoのピンが `200ab3c9` へ進んだ（`7ca221d56`）ことによるクロスリポジトリの順序破れ。

## 棄却した案
- **ピンを274b6d9fへ戻すPRを作る**: masterの起動不能を即座に直せるが、
  棄却理由 — #1268 が近く入るなら巻き戻しと再前進で履歴が往復するだけ
- **ローカルでピンだけ戻して検証を先に済ませる**: 録画は撮れるが、
  棄却理由 — masterの実態と違う組み合わせを「確認済み」として残すことになる

関連: [[docs/adr/0035-new-world-defaults-to-generated-map.md]] / bd moorestech-amjc（PR #1268）
