# ローカライズ Plan 2 レビュー記録 (2026-07-30)

## 対象

- base: `a22105493b1529b33464f86c1b687ee1041ccc77` / reviewed head: `1ce2b05d719095babdafac8ebd26eda7a7bb0551`
- 差分: 258 files changed, 6603 insertions(+), 1096 deletions(-)
- ブランチ: `feature/localization-foundation`
- context要約 — ゴール: mod CSV・Skit Addressable JSON・Master原文をUnity/Webで同じ導出キーへ合成する / 非目標: 言語切替UI、stable key未定のtrainCar・connectTool / 許容トレードオフ: Web辞書はrevision付き3 HTTP応答 / 制約: Skit専用辞書を既存置き場から動的ロード、C# 200行・日英コメント・`Func<>`禁止

## 系統別判定

| 系統 | Critical | 要旨 |
|---|---:|---|
| 決定論チェック | 0（反証1） | 比較演算子2件を修正。外部JSON parse境界の`try-catch` 1件はAGENTS明示許可かつ境界根拠コメントあり |
| master-data-defense / type-driven-structure | 2 | CharacterGuid空値・重複、Placement payloadのraw block名を検出 |
| precedent-alignment / redundant-member-duplication | 2 | mod列挙順の非決定性、BuildMenuの重複索引を検出 |
| lifecycle / async品質レビュー | 4 | Skit Prepare中Disposeの無限反復、二重load、失敗revision再試行、Addressable null/解放漏れを検出 |
| Web契約・Fable全般 | 3 | locale同一の辞書再合成が再取得されない、HTTP異世代混在、semantic GUIDが任意文字列を受理する問題を検出 |
| Codex外部監査 | High 3 / Medium 5 | Web辞書世代、mod順、Placement原文、Skit key重複、Addressable handle、Prepare race、CharacterGuid、Web GUIDを指摘 |
| comment-convention-guard | 0（初回50） | 機械的短縮50件を適用し、根拠コメント47件は例外として保全 |
| comment-rationale-guard | 0（初回1） | 録画用一時worldが既存セーブ読込を防ぐ根拠を復元後、再検査Criticalなし |

## 適用した修正

- CharacterGuidの空値・重複検証と実masterテストを追加（master-data-defense）→ `1ce2b05d7`
- mod辞書の列挙をModId ordinal順へ固定（precedent-alignment / Codex）→ `1ce2b05d7`
- Placementを`block` / `blueprintCopy` / `raw`のstrict discriminated unionへ変更（type-driven-structure）→ `1ce2b05d7`
- Web辞書とrevisionを同一immutable snapshotでatomic publishし、topic・HTTP・Providerをrevision必須化（Codex / Fable）→ `1ce2b05d7`
- Skit key builderをCommandForge field正本へ集約し、Addressable handleをparse後に解放（Codex）→ `1ce2b05d7`
- Skit PrepareのDispose・購読・失敗revision・並行実行raceをTDD修正し、辞書合成責務を分離（async品質レビュー）→ `1ce2b05d7`
- inventory / recipe / research / challengeのsemantic GUIDを共通UUID schemaへ統一（Codex）→ `1ce2b05d7`
- 単一呼出helperを`#region Internal`ローカル関数へ移動し、全C#ファイルを200行以下へ整理（core convention）→ `1ce2b05d7`
- コメント50件を機械的短縮し、一時worldの既存セーブ隔離根拠を復元（post-check）→ `1ce2b05d7`

## 設計判断（AskUserQuestion裁定）

- Q: Skit文言の正本をどこに置くか / 裁定: ユーザー発言「skitは専用のスキット文章翻訳csvの置き場があった気がしてて、そこから動的に参照するようにできない？」を受け、実在する `Assets/AddressableResources/Skit/i18n/{language}.json` を正本として対象言語・英語を動的ロード / 適用: Plan 2 Task 8一式
- Q: 前回説明項目4を採用するか / 裁定: ユーザー回答「4はok」 / 適用: Plan 2のWeb側解決方針を維持

## 破棄した指摘

- trainCar・connectToolも今回stable key化すべき — Plan 2 Task 7が正本未定義としてraw維持を明記し、train masterには表示名正本がないため破棄
- Localizationテストディレクトリ10ファイルは上限違反 — 規約は「10ファイルまで」であり、11件目は追加していないため破棄
- blueprint copyの空entryKeyはhidden sentinel — discriminated unionの`blueprintCopy` variantで型付けされた一様選択identityであり、raw文字列分岐ではないため破棄
- optional tooltipの空文字を禁止すべき — 表示不在を表す正規値で、翻訳候補の空文字欠落規則とは別契約のため破棄
- Skit JSON parseの`try-catch`は禁止 — 外部入力JSONの構文エラー隔離というAGENTSの明示例外で、address付き例外へ変換する境界コメントもあるため破棄

## 事後結果（マージ後追記可）

- 未マージ

## メタ

- セッションID: Codex外部監査 `30106` / subagents: `plan2_task8_impl`, `plan2_task8_quality_review`, `plan2_task8_spec_review`, `plan2_comment_convention_guard`, `plan2_comment_rationale_guard`
- スキップ系統: なし
- 録画QA: `PlaytestResults/20260730_204854/localization-task8-priority`、42 assertions、13.77秒、mp4 5.5MB、スクリーンショット3枚を目視
- 最終検証: Unity compile 0 errors、関連C# 138/138＋再修正後7/7、Web Vitest 486/486、Playwright 119/119、TypeScript build・E2E typecheck・ESLint成功

