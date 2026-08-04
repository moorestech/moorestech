# 0005: 名前空間ローカライズキーの正準化とバニラCSVのコード側埋め込み

日付: 2026-07-29
状態: 採択

## 背景

ローカライズ基盤は配信パイプ（`/api/i18n` + `localization.current` トピック）だけが完成し、中身が分裂していた。

- Web UI は lint（`no-jsx-visible-literal`）で `t()` ラップを完全強制済みだが、430キーが**日本語原文をそのままキー**にしており、辞書（54キー・英語表記）には3キーしかヒットしない。`?? key` フォールバックで日本語が表示されるだけで、言語切替しても画面は変わらない。
- `config/localization.csv` は v4 時代から凍結された遺物（md5 が v4〜v8 で完全一致）。キーの約44個は現マスタと繋がらない旧英語アイテム名の化石。
- CSV は別リポジトリ（moorestech_master）にあり、コード参照キーと実体のドリフトがピン留めによって恒常化する構造だった。

## 決定

1. **`ui.inventory.title` 形式の dot 区切り名前空間キーを正準キー空間とする。** Web の日本語原文キー430個は一括置換で全面移行し、日本語原文は japanese 列の初期値として辞書へ移す。
2. **バニラCSVは moorestech リポジトリ内へ移動する**（スキーマではないため VanillaSchema/ とは別の専用ディレクトリ。案: リポジトリ直下 `Localization/`）。`config/localization.csv` の実行時読み込みは廃止する。
3. **既存の単一 `MooresmasterSourceGenerator` が `LocalizationSourceEmitter` を呼び、キー定数と辞書本体の両方を C# に埋め込む。** 独立した第2 generator は共通CSV DLLのanalyzer依存を全assemblyで解決できずコンパイルを壊すため採用しない。csc.rsp に additionalfile と共通DLL analyzer参照を追加し、SchemaWatcher の監視対象へ新ディレクトリを加える。
4. **webui は同一CSVから TS 定数を生成する。** `t()` への生文字列リテラルは lint で禁止し、キー切れを C#=コンパイルエラー / TS=lint・型エラーとして両側でビルド時に検出する。
5. **バニラキーの欠落は CI/テストで機械検出してエラー化する。** 実行時の `Get` / `GetLegacy` は対象言語→english→source→`[!key]` で解決し、全段に欠けた場合は目立つ表示で露出させる。
6. **CSVパーサー・行モデル・例外は runtime 参照可能な小さな共通DLLへ置く。** SourceGenerator と Unity runtime は同じ実装を参照し、parserをコピーしない。共通DLLは generator と同じビルドで client/server の両方へデプロイし、同一テスト群で検証する。
7. **言語セットの唯一の定義は辞書CSVヘッダとする。** `localization_settings.csv` は表示名とSteam言語コードだけを持ち、ヘッダとの集合不一致はコンパイルエラーにする。
8. **空文字の翻訳は「値」ではなく欠落として扱う。** runtime合成・解決では空文字を登録/返却せず次のfallback段へ進む。CSV parserは欠落検査のため空field自体は保持し、Source列を含むliteral `\n` は実改行へ正規化する。

## 却下した選択肢

- **日本語原文キーの維持（gettext方式）** — 原文の微修正が全言語のキー切れを起こし、SourceGenerator の型付きキーと二重体系になるため却下。出所: ユーザー裁定 2026-07-29（AskUserQuestion「正準キー空間」で全面移行を選択）
- **段階移行（新規のみ名前空間キー）** — 二重体系が恒久化するリスクが高く却下。出所: 同上
- **CSVを moorestech_master に残し実行時ロード** — UI文言はコンテンツデータではなくゲームコードの一部であり、マスタピンとの組み合わせでキー不一致が構造的に発生するため却下。出所: ユーザー裁定 2026-07-29「埋め込むが場所はスキーマ以外の場所にしたい（スキーマじゃないので）」
- **Webは文字列キーのままテスト担保のみ** — 型の守りが弱く却下。出所: ユーザー裁定 2026-07-29（AskUserQuestion「Web型付け」）

## 帰結

- コードと文言が同一コミットで動き、ドリフトが構造的に消える。
- mooresmaster generator 変更時は `mooresmaster/build.sh` で client/server 両方の DLL を再ビルド・コミットする運用が必要。
- 共通CSVライブラリ変更時も `mooresmaster/build.sh` で parser DLL と generator DLL を client/server の両方へ再ビルド・コミットする。build.shから全meta生成/上書きを除去し、追跡済みgenerator metaは保持、新規共通DLL metaはUnity自身にruntime pluginとして生成・設定させる。
- 既存の `Localize.cs` の CSV パース・`TextMeshProLocalize` の try-catch（規約違反）は基盤改修で置き換えられ自然消滅する。
