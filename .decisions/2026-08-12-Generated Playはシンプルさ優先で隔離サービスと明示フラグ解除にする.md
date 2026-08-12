# Generated Playはシンプルさ優先で隔離サービスと明示フラグ解除にする

決定:
1. デバッグ環境は `DebugParametersCacheDirectory` の一時cache隔離へ置換する（退避/復元の自作をやめる）。[[2026-08-12-Generated Playのデバッグ環境は再生中だけ切替え復元する]] を差し替える
2. 起動モードの排他は各入口で相手フラグを明示解除する（1〜3行）
3. `"DebugEnvironmentTypeKey"` のSSOT集約は今回やらず別PRへ送る

棄却案:
- 現行の退避/復元を維持 — クラッシュでRuntimeが残置し手動復旧が要る。かつコード量は隔離案より多い
- 起動種別を単一SessionState値（enum）へ畳む — 既存のNoSave Play・DSLまで書き換える必要があり、使い捨て予定のエディタ拡張には過剰
- キー文字列をDebugConstへ集約し6箇所差し替え — ランタイム側の既存ファイルに波及し、今回の機能と無関係

理由: ユーザー指示「複雑にしなくてシンプルなエディタ拡張でいい。最終的にすべて自動生成に移行すれば要らなくなる」。隔離サービス案は退避キー2本・復元メソッド・復元テスト2本が不要になり、正味でコードが減る。前回裁定の根拠として示した「既存DSLも常時残置する」は誤りで、DSLはセッション専用cacheへ隔離し自分のoverrideだけ解除していた（PlaytestBootLifecycle.cs:131-133,147-148）。

リンク: docs/adr/0009-generated-world-editor-play-button.md
