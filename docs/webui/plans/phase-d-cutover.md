# Phase D 実行計画: カットオーバー完了・後始末

親: `../MIGRATION.md` / 進捗: `../TODO.md`
依存: Track A〜C 全完了（i18n とクラフトツリー削除は先行着手可）。
本 Phase の完了 = `MIGRATION.md` の完了定義達成。

## タスク1: クラフトツリー機能の削除（先行可）

- **設計書が既に存在する**: `docs/superpowers/specs/2026-07-18-craft-tree-removal-design.md` が正。
  サーバードメイン（`Game.CraftTree`）・通信・保存・クライアント UI（`InGame/CraftTree/` 一式）・
  Unity アセット・現行資料まで含む完全削除（無効化フラグや互換残骸を残さない）
- 本 Phase では同設計書の実施完了を確認し、`../TODO.md` のチェックを更新するのみ
- 注: 「uGUI コード温存」方針の例外。機能自体の廃止決定（2026-07-18）による削除

## タスク2: i18n — Web 側文字列バインド基盤（旧 INFRA-11・先行可）

- 現状: uGUI は `Client.Localization/TextMeshProLocalize.cs` で文字列バインド。Web 側に等価基盤なし
- 作業:
  1. ローカライズ辞書の配信（`/api/master/items` と同様の HTTP 配信 or Topic）+ 言語切替イベント
  2. Web 側 `t(key)` 相当のフック + 言語切替時の再描画
  3. 既存 feature のハードコード文字列を key 化（漏れ検出に決定論チェックを検討）
- 言語設定 UI 自体はメインメニュー（スコープ外）のため、設定値の受信のみ

## タスク3: 全 uGUI ビューのゲート化監査

- `archive/ui-completeness-reaudit-plan.md` の手順（全 .cs / UI 資産の全件 triage）を再実行し、
  「web モード中に表示され得る uGUI 平面ビュー」が全てゲート化されていることを機械的に確認
- 対象外リスト（ワールド空間 UI・メインメニュー・デバッグ）を明示した監査記録を残す
- 二重表示・ゲート漏れが出たら個別修正

## タスク4: 最終検証

- [ ] PlayMode 全画面遷移スモーク: UIStateEnum 全 state（スコープ外の Debug 除く）を一巡する
      録画付きプレイテスト（`unity-playmode-recorded-playtest`）
- [ ] Ctrl+I トグルの実機目視確認（web⇔uGUI の切替と復帰）
- [ ] 実機 web↔host 連携検証（A1 完了後に可能になった統合検証の最終版）
- [ ] 本番静的配信モード（A3）での起動・一巡確認

## タスク5: （任意・効率化）

- INFRA-4: C#→TS 型自動生成（手書き `payloadTypes.ts` の置換。SourceGenerator 延長）
- GameStateType Topic の一般整備（C4 で最小実装済みのはずのものを正式化）

## 完了後

- `MIGRATION.md` の完了定義チェックを全て満たしたことを確認し、本ドキュメント群をクローズ
- **uGUI コードの物理削除は別判断**: 削除するなら改めて棚卸し計画を立てる
  （Ctrl+I フォールバックを残すかどうかの判断を含む）
