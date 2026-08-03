# skit辞書の言語集合契約テスト

決定: `SkitLocalizationDynamicLoadContractTest` のハードコード `{english, japanese}` を `LanguageCatalog.Languages` 導出へ変更し、言語追加時のSkit辞書アセット未整備をCIで検出する（AskUserQuestion 2026-08-02・user-simulator予測→ユーザー承認・Fable全般レビュー推奨案B）
棄却案: ローダー側フォールバック（対象言語アセット欠落時に空辞書を返し5段fallbackへ委ねる案A）
理由: 案Aは「アセット丸ごと欠落」まで空文字＝欠落の黙認扱いに広げてしまい、置き忘れが無言で英語表示になる。LanguageCatalogが言語集合の単一の正である前例（LanguageCatalogCodeEmitter.ValidateLanguageSet）にテストで乗る方が整合する
リンク: docs/superpowers/plans/2026-08-02-localization-review-remediation.md Task 6
