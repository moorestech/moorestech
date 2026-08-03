# PR #1095 独立レビュー差し戻し実装 レビュー記録 (2026-07-30)

<!-- 1レビュー実行=1ファイル。命名: YYYY-MM-DD-<topic>.md（再レビューは -r2 付き新ファイル＋相互リンク1行）。
     記録は不変。マージ後に判明した事実のみ「事後結果」へ追記可。設計根拠: docs/superpowers/specs/2026-07-23-review-records-design.md -->

## 対象
- base: `c2cafee6d7c4803a975159acb5650b1b8fabc313` / reviewed head: dirty 104ファイル（`1096 insertions / 706 deletions`）、適用コミット `8aeba8327`
- ブランチ: `feature/placement-guid-equipment-mining` / PR: `#1095`
- context要約 — ゴール: `/tmp/pr-review-1095-fixlist.md` のA〜Dを実装し、装備選択・採掘・設置対象・Blueprint・マスター境界を裁定どおり正す / 非目標: サーバー側Blueprint統合 / 許容トレードオフ: 後方互換・性能最適化・将来拡張性は対象外 / 制約: AGENTS.md、裁定D1〜D12、マスターデータ4段階、サーバー権威同期

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | 比較・region・schema optional・イベント同期の新規違反なし。行数3件と同一ディレクトリ22ファイルは既存構造由来のeffort warning |
| moores設計レンズ | 0（最終） | イベントパケット分割、ローカル関数化、テスト補強後はCriticalなし。D6のClient→Game.Map依存とBlueprint表示名は裁定済みとしてsuppressed |
| 汎用reviewer | 0（最終） | editor境界、非正値採掘検証、アンロック集合テストの不足を検出し適用 |
| Codex外部監査 | 0（最終） | Web楽観更新が古いechoでpending解除される欠陥、送信失敗・再接続時のpending残留を検出し適用 |
| Fable全般 | 0（最終） | Web pendingのサーバー確認識別不足を独立検出。確認revisionとFIFOで解消 |
| コメントpost-check | 0（最終） | rationale 2件を復元し、規約ガードの機械的短縮26件を適用 |

## 適用した修正
- `tools` デッドスキーマ・生成利用・バリデータ・全JSONを撤去し、外部マスターを `261e06165c6c846410b47c544a376b286931af69` へ更新（裁定A） → 適用コミット `8aeba8327`
- サーバーBlueprint catalog DIを撤去し、ロード時Guid補完を廃止（裁定A/D1） → 適用コミット `8aeba8327`
- PlacementTarget catalog/factory/unlockをKind網羅・Guid一意・単一構築経路へ整理（B/C） → 適用コミット `8aeba8327`
- 採掘クールダウンをplayerId単体キー化し、装備解決・入力値検証・結果enum・回帰テストを追加（D2/D3/D5〜D12） → 適用コミット `8aeba8327`
- 装備選択をサーバー権威の無条件echoへ戻し、slot更新とselected-index更新のイベントを分離（D4） → 適用コミット `8aeba8327`
- `SelectionConfirmationRevision` をwireへ追加し、Web pendingを確認revision差分のFIFOで消費、送信失敗・切断・再接続時に破棄（Codex/Fable） → 適用コミット `8aeba8327`
- C#・wire・Web UIの回帰テストを追加し、コメント規約とファイル配置を是正（reviewer/post-check） → 適用コミット `8aeba8327`

## 設計判断（ユーザー裁定）
- Q: Blueprint統合の責務 / 裁定: **D1=クライアント専用へ正直化し、サーバー側統合は別PR** / 適用: サーバーDI・依存を撤去
- Q: 採掘クールダウンのキー / 裁定: **D2=playerId単体** / 適用: 対象切替でクールダウンを迂回できない構造へ変更
- Q: 装備選択同期 / 裁定: **D4=サーバー権威、無条件echo、Web抑止撤去、pending保持** / 適用: 確認revisionを追加してサーバー応答だけを確定根拠にした
- Q: 残る設計判断 / 裁定: **D3・D5〜D12は修正指示書の推奨案どおり** / 適用: `8aeba8327`
- Q: 以前の免責3件 / 裁定: **tools残置・サーバーDI維持・Guid補完をすべて否認** / 適用: 3件とも撤去
- Q: 「同値抑止だけ外す」7/29裁定 / 裁定: **覚えなし。今回のD4裁定を正とする** / 適用: 楽観表示は維持しつつ確認とpendingをサーバー応答へ結び直した

## 破棄した指摘
- `.moorestech-external-revisions.json` が旧HEAD `064c2f` 以降の無関係コミットを含むとの指摘 — 作業開始時点で参照はユーザー変更済みの `98a0e9a`。本タスクの外部コミット `261e061` はその直接の子であり、本タスク差分はtools撤去だけ
- `Client.Game` から `Game.Map` への依存 — D6で明示裁定済みの依存
- Blueprintの `MasterDisplayName` にユーザー名が入るとの指摘 — 今回のBlueprint表示契約として裁定済み

## 事後結果（マージ後追記可）

## メタ
- セッションID: 現Codex実装セッション / スキップ系統: なし / 備考: Unity対象テスト129件、Web対象テスト69件、Web build、最終Unity compileを実施。コメント短縮後の最終compileは `Success=true / ErrorCount=0 / WarningCount=0`
- 備考: EditModeInPlayingの最新ランナー実行は意図したdomain reloadでCLI接続が切れたため、同じ同期契約をC#単体・wire・Web FIFOテストで検証。以前の実行ではクライアント選択とサーバー選択の収束を確認済み
