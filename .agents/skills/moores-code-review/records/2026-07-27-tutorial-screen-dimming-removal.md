# チュートリアル画面暗転の撤去 レビュー記録 (2026-07-27)

## 対象
- base: `3bf15f5d6` / reviewed head: `4adf5ebfa` → 修正適用後 `5116e690f`
- ブランチ: `feature/remove-tutorial-screen-dimming` / PR: なし
- context要約
  - ゴール: チュートリアル対象の黄色いDOM輪郭を維持し、対象外画面を暗くする`spotlight`契約とCSSを撤去する
  - 非目標: callout、DOM追従、通常のmodal/backdrop、pointer input、anchor ackの変更
  - 許容トレードオフ: なし
  - 制約: Unity producer・Web契約・CSSを一括更新し、旧暗転の再導入を自動テストで検出する

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 1→0 | 新規E2Eにより`e2e/tests`直下が13ファイルになった問題を検出。`system`へ移動後、最終confirmed 0 |
| domain-boundary / precedent-alignment | なし | 用途別store APIと既存topic control経路に一致 |
| 汎用reviewer群 | 2→0 | ファイル配置超過と、旧暗転CSS復元mutationが生存する回帰テスト不足を検出 |
| Codex外部監査 | Critical 1 / Medium 2 / Low 1 | 配置とCSS mutationは他系統と一致。現行設計文書の古い`spotlight`記述も検出 |
| Fable全般 | なし | Warningとして`9999px`否定だけでは同等暗転を見逃す点を検出 |
| comment-rationale-guard | なし | 削除コメントはWHAT説明のみで、根拠喪失なし |
| comment-convention-guard | 2→0 | テスト名・scenario名と重複する日英コメント2組を機械的削除 |
| 修正後独立再レビュー | なし | Critical 0 / Warning 0。E2E 1 passed、mutation RED記録、path同期を確認 |

## 適用した修正
- E2Eを`e2e/tests/system/tutorial.spec.ts`へ移し、黄色4px輪郭・透明overlay・旧`spotlight` selector復元を検出するprobeへ強化（決定論 / reviewer / Codex / Fable）→ `ebb167ace`
- `docs/webui/design/tutorial-web-redesign.md`の契約例とPhase T1を`outline | callout`へ同期（Codex Low）→ `ebb167ace`
- テスト名・scenario名と重複する日英コメント2組を削除（comment-convention-guard）→ `7bf3b28cf`

## 設計判断（AskUserQuestion裁定）
- なし

## 破棄した指摘
- Codex Medium「`TutorialHighlightData.Kind`をenum/value type化し、mutable DTO公開も閉じる」— 今回の設計はstoreのpublic producer APIから任意kind引数を除去する契約であり、wire DTOのシリアライズ型と既存snapshot構造の変更は別スコープ。production生成経路は`AddOutlineHighlight`に閉じ、Web runtime schemaも`spotlight`を拒否するため不採用。

## 事後結果（マージ後追記可）
- （未記入）

## メタ
- セッションID: Codex workspace session（ID非公開）
- スキップ系統: なし。利用可能モデル制約によりOpus/Fable/Sonnet指定は`gpt-5.6-sol`で代替し、reviewer群は3グループで並列実行
- suppressed: 0件
- 最終検証: Unity 3/3、compile Error 0 / Warning 0、新規Error log 0。Web unit 379/379、E2E 1/1、build成功、production旧参照0件
