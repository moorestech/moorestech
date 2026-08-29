# 出展モード起動言語の可否判定は Localize へ集約し env 生値は束ね型にする

- 決定: 言語の可否判定は `LocalizeLanguageApplier.ApplyOrDefault`（`Localize.TrySetLanguage` の公開辞書）の1か所。結果は `LanguageResolution { Unset, Accepted, UnknownFallback }` で返す。`EventExhibitionSettings.Parse` は `EventModeEnvironmentValues`（名前付き struct）を受け、言語は生値を運ぶだけ。適用行は `EventModeAutoStart.ApplyLaunchLanguage` に切り出し実言語でテストする
- 棄却: Parse に既定値だけ注入（分裂が残る）／二段フォールバックのみ（同）／enum を使わず値比較で逆算（正規化で誤検知）／束ね型を 0040 実装時へ先送り／適用行を録画テストのみ・未検証で受け入れ
- 理由: moores-code-review 2026-08-29-1815 の Critical C1〜C3（複数系統一致）。ユーザーは推奨案を採択し束ね型のみ「今回やる」を選択
- リンク: docs/adr/0042-event-mode-launch-language-env.md, ../moorestech_logs/harness/moores-code-review/runs/2026-08-29-1815/design.md
