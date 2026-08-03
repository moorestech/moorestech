# source疑似ロケールのwire境界

決定: `/api/i18n/{locale}` 単一エンドポイントを維持し `"source"` はエンドポイント内の明示分岐で配信する。C#内部のsnapshot型分離（`Languages`/`SourceTexts`）は実施し、TS側は `SOURCE_LOCALE` 定数化のみ行う（AskUserQuestion 2026-08-02・user-simulator予測→ユーザー承認）
棄却案: `/api/i18n-source` 専用エンドポイント新設（wire上も型分離を貫徹する案）
理由: 「プロトコルは1ドメイン1本・Mode分岐」方針と整合し、webui fetch・mock・テストの追加改修を避ける。型分離の便益（除外規則3箇所の構造的消滅）はC#側だけで得られる
リンク: docs/superpowers/plans/2026-08-02-localization-review-remediation.md Task 13
