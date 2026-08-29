# 出展モードの起動言語は環境変数 MOORESTECH_EVENT_LANGUAGE で指定し gamescom はドイツ語にする

ADR 0030 の出展モードは起動のたびに言語を英語へ強制リセットしており、gamescom（ドイツ）のブースで
ドイツ語起動にする手段が無い。ADR 0040 の言語選択ゲートは未実装で、gamescom までに間に合う保証が無い。

起動スクリプト（`scripts/event/start-gamescom-loop.command`）が既に `MOORESTECH_EVENT_MODE` と
`MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS` を環境変数で注入しているので、同じ経路で起動言語も注入する。

## 決定

- `EventExhibitionSettings` に `MOORESTECH_EVENT_LANGUAGE` を追加する。値は言語コード（`english` / `japanese` / `german`）
- `EventModeAutoStart` は固定の `Localize.DefaultLanguageCode` ではなく `settings.LanguageCode` を `TrySetLanguage` する
- 未設定・未知の値は `english` へ落とし、未知の値のときは `Debug.LogError` を出す（環境変数は外部入力なので検証とフォールバックを許容する）
- C# 側の既定は english のまま。gamescom 用スクリプトだけが `export MOORESTECH_EVENT_LANGUAGE=german` を書く
- ADR 0040 実装後もこの環境変数は「言語選択ゲートが出るまでのロード画面言語」として残す。ゲートで来場者が選んだ言語が以後は勝つ。ADR 0040 の「起動ごとの英語強制リセット」は「環境変数の起動言語へリセット」と読み替える

出所:
- 環境変数で起動言語を指定する最小変更: ユーザー裁定 2026-08-29 原文「いまのコマンドでの起動モードでサクッとビルド無しでドイツ語起動をデフォルトにできる？」「Ok、サクッとやりたい」→ 選択「環境変数 MOORESTECH_EVENT_LANGUAGE を追加する最小変更」
- 未知値は english へ落とす: ユーザー裁定 2026-08-29 選択「english へ落とす」
- 既定の置場は .command の export: ユーザー裁定 2026-08-29 選択「.command で export=german」
- ADR 0040 後もロード画面言語として残す: ユーザー裁定 2026-08-29 選択「ゲート表示前のロード画面言語として残す」
- 未知値で LogError を出すこと: agent前提（既存 `EventModeAutoStart` の TrySetLanguage 失敗時 LogError と同形）

## Considered Options

- 未知値で起動を止める（AutoStart を走らせない／即 Quit）— 無人ブースで復旧不能になるため棄却（ユーザー裁定 2026-08-29）
- C# 側の既定値を german にする — 他イベントで毎回コード変更が要るため棄却（同上）
- ADR 0040 実装時に環境変数を撤去する暫定措置 — ゲート前のロード画面にも言語が要るため棄却（同上）
- `MOORESTECH_EVENT_MODE` を外して PlayerPrefs の言語で起動する（ビルド不要）— ワールド削除・自動開始・無操作終了も無効になるため出展運用として不成立。会話で提示し不採用

## Consequences

- 配布ビルドの再作成が必要（今のビルドは英語固定）
- ビルド対象の master がドイツ語辞書列を持ち、クライアントがそれをロードできることが前提（bd `moorestech-tlza` 参照）
- 起動スクリプトの言語指定ミスは LogError だけで表面化する。ブースでは起動直後の表示言語で目視確認する
