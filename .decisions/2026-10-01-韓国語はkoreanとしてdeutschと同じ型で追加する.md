# 韓国語はkoreanとしてドイツ語と同じ型で追加する

決定: 言語コード `korean`・表示名「한국어」・Steam言語 `koreana` を言語一覧CSVへ1行足し、vanilla辞書（303行）とMod辞書へ korean 列を全訳で追加する。スキットエディタ辞書は german と同じく english 複写（locale=ko・name=한국어）で Addressable 登録する。

棄却案: スキットエディタ辞書の全訳（ADR 0034 のドイツ語と同じ理由でプレイヤー非露出）／韓国語フォントの同梱（Hangulはシステムフォントへフォールバックさせ、崩れたら報告ベースで直す）。

理由: 「韓国語を追加して」（/goal、2026-10-01）。方式の細部はユーザー指定が無いため ADR 0034 のドイツ語追加の前例に従った（agent前提）。

リンク: docs/adr/0034-localization-gap-fixes-and-german-locale.md
