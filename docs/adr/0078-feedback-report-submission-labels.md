# 0078: 感想報告の送信文言を報告種別に合わせる

日付: 2026-10-11
状態: Accepted

## Context

感想選択後も送信ボタン・完了通知がバグ表記になる。
原因: 実測（証拠: `/tmp/moorestech-a-baseline.txt` の BASELINE_KIND feedback / BUTTON バグ報告を送信 / BASELINE_TOAST）。既存React testSupportで本番BugReportFormを描画しdispatchActionと辞書をスタブした観測。実機CEFは別途録画する。CSVの実訳では固定キーui.bugReport.sentがバグ報告を明示する。

## Decision

- 出所: ユーザー裁定 2026-10-11 原文「感想選択時は感想用文言、バグ選択時は従来バグ文言へ合わせ全言語を揃える」。感想専用送信・成功キーをCSVに足し、種別で選ぶ。
- 出所: ユーザー裁定 2026-10-11 原文「送信成功・失敗・送信中の既存状態や送信データを壊さない」。種別を送信開始時に保存しpayloadと完了通知に同じ保存値を使う。missing・拒否・成功後reset・画面遷移は現行を保つ。
- 出所: agent前提（既存ui.bugReport.sendingがバグを明示する同型箇所の掃引）。感想の送信中表示にも専用キーを足す。日本語・英語・独語・韓国語を同時に揃える。
- 出所: agent前提（既存BugReportFormが入力と送信UIを所有し、Localization/localization.csvが全翻訳の正本）。ローカル関数で3文言の選択を一箇所化。新しい共通サービスは作らない。生成TSキーはgen:i18nで生成する。

## Consequences

通信action・payload形・欠損通知は従来と同じ。成功後は既存の裁定どおり入力を空にしバグ既定へ戻る。辞書の追加だけでC#側変更は不要。

参照: .decisions/2026-09-13-感想もポーズメニューの報告UIで受け種別バグと感想を選ばせる.md、.decisions/2026-09-24-バグ報告画面の送信成功後はポーズのトップへ戻る.md
