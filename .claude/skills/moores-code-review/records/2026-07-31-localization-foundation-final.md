# ローカライズ基盤 最終レビュー記録 (2026-07-31)

## 対象

- base: `055a4d85b` / reviewed head: `d227708a4`
- 外部master: `c783a13b361da414cf97b56641d23a1b3c0047d8`
- 差分: 16 files changed, 238 insertions(+), 41 deletions(-)
- ブランチ: `feature/localization-foundation`
- context要約 — ゴール: バニラ辞書・mod辞書・Skit専用辞書・言語切替UIを最終QAし、実行時QA文言と入力境界の欠陥を除去する / 非目標: 正準source未定のtrainCar・connectToolの辞書化 / 許容トレードオフ: user blueprintとtrainCar・connectToolは原文Labelを維持 / 制約: Skit専用Addressable辞書の動的参照、C# 200行・日英コメント・`Func<>`禁止

## 系統別判定

| 系統 | Critical | 要旨 |
|---|---:|---|
| 決定論チェック | 0（初回コメント長候補5） | コメントを短縮後、confirmed・candidateとも0 |
| 独立コードレビュー | 0（初回Important 1 / Minor 1） | `lang_name`空白受理とgit子プロセス無期限待機を修正し、再レビュー0件 |
| 独立spec/planレビュー | 0（初回Minor 1） | Japanese Skit fixtureの説明をcommand 1/2へ訂正し、再レビュー0件 |
| 最終レビュー前バグ狩り | 0（初回Important 4） | 実行時QA文言、設定値空白、Skitテスト無期限待機、Label全廃文書矛盾を修正 |

## 適用した修正

- Addressable英日Skit辞書とmod CSVのQA識別文言を自然文へ置換し、pin先本番内容の回帰テストを追加（最終レビュー前バグ狩り）→ main `2a7aa890f` / master `c783a13`
- `display_name`・`steam_api_lang_code`の空白値を入力境界で拒否（最終レビュー前バグ狩り）→ `2a7aa890f`
- Skit resolverテストの全`WaitUntil`へ2秒timeoutを追加（最終レビュー前バグ狩り）→ `2a7aa890f`
- 安定Guid対象と原文Label維持対象をADR・spec・planで明文化（最終レビュー前バグ狩り）→ `2a7aa890f`
- `lang_name`も空白値を拒否し、空文字・空白のTheoryを追加（独立コードレビュー）→ `d227708a4`
- 本番辞書検査のgit stdout/stderrを並行回収し、プロセス5秒・stream 2秒のtimeoutを追加（独立コードレビュー）→ `d227708a4`
- 5段fallback fixtureのplan説明をJapanese command 1/2非空へ訂正（独立spec/planレビュー）→ `d227708a4`

## 設計判断（AskUserQuestion裁定）

- Q: Skit文言をどこから参照するか / 裁定: ユーザー発言「専用のスキット文章翻訳csvの置き場があった気がしてて、そこから動的に参照するようにできない？」を受け、既存 `Assets/AddressableResources/Skit/i18n/{language}.json` を動的ロード / 適用: 対象言語Skit→英語Skitをmod辞書の間へ重ねる5段fallback
- Q: stable keyを持たない表示名の扱い / 裁定: ユーザー回答「4はok」 / 適用: user blueprintは原文Label、正準source未定のtrainCar・connectToolは暫定Labelを維持し、ホスト側翻訳は行わない

## 破棄した指摘

- `LanguageCatalog.Languages`配列の可変性 — 計画で生成配列APIを明示し、既存Master生成APIの前例にも一致するため今回の欠陥としては不採用
- Skit辞書composerのコピーコスト — 起動時と言語切替時だけの小規模辞書合成で、実測上の問題がなく正しさを優先するため不採用

## 事後結果（マージ後追記可）

- 未マージ

## メタ

- セッションID: subagents `task6_fix_settings_validation`, `task6_fix_skit_sentinels`, `task6_fix_skit_test_timeouts`, `task6_final_code_review`, `task6_final_spec_review`
- スキップ系統: なし
- 録画QA: `PlaytestResults/20260731_002403/localization-skit-natural-content-verified`、16/16、ErrorLogs 0、8.24秒、mp4 2.4MB、自然文言スクリーンショット目視済み
- 既存UI録画QA: `PlaytestResults/20260731_002053/localization-language-switch-and-legacy` と `20260731_002158/localization-language-switch-and-legacy` は各35/35・ErrorLogs 0、`20260731_002403/localization-language-restore` は13/13・ErrorLogs 0
- 最終検証: 決定論チェック0件、mooresmaster 271/271、Unity関連115/115、pin先本番辞書3/3、Unity compile 0 errors / 0 warnings、Web Vitest 486/486、Playwright 120/120、TypeScript build・ESLint成功
