# planレンズ第一弾の採掘根拠（2026-07-23）

セッションログ（~/.claude/projects/-Users-katsumi-moorestech/、`docs/superpowers/plans` を含む77セッション）から
ユーザーの生発言のみを抽出し、plan提示後の修正・実行中〜後の計画起因手戻りをカタログ化。
既存採録（spec-architecture-review の5実例・データフロー地図・機能パリティ死活表）と重複する類型は除外した残りを `lenses/` 化した。
採掘で判明した頻度トップ2（波及棚卸し・前提裏取り）は、現状の観点別レビューでも捕まえられずユーザー本人の指摘で発覚する比率が最も高い。

| 実例（セッション） | 内容 | レンズ |
|---|---|---|
| 11d2381a | ブッシュ青銅ドロップ（移行ミス）前提で計画→「その前提で作り直して」全面作り直し | premise-verification |
| d6e90a24 | map静的ベイク計画→「seed変えたら別マップが遊べるようにしたい」が中核価値 | premise-verification |
| ae75c64b | 消滅GUIDを在庫と誤認し空化計画→実は接続コスト記録・第2クラッシュ地点（Codex監査で発覚） | premise-verification |
| 0595be6c | 「既存buildMenu様式を踏襲」→実はレガシー実装でGamePanel化がスコープ落ち | premise-verification |
| d6e90a24 | DI直接構築427箇所が未計測のまま計画→名前付きファクトリ2種へ全面改訂 | blast-radius-enumeration |
| e927d479 | CleanRoom機械（同Idleステートの第2プロセッサ）見落とし→共通Util化で後追い対応 | blast-radius-enumeration |
| ae75c64b | 「アンロック対象のマスタ再登録が必要では？」「shape追加のマスタ設定は？」「コネクタ分割は計画に入ってるの？」 | blast-radius-enumeration |
| d6e90a24 | プロビジョニング中断の残骸・エディタ再生・CliConvertTestの破損が計画に無く後追い追加 | blast-radius-enumeration |
| 795bacde | モックe2e全依存→本番staticが一度も動いていなかった（実機検証で2バグ発見） | verification-coverage |
| 089479aa | train同期の境界（空snapshot・所有権照合・走行時間assert）欠落が後付けレビューで発覚 | verification-coverage |
| 68697493 | `_isUndoing` 早期解放で連続Undo誤判定（CodeRabbit指摘） | verification-coverage |

SSOT統括不在（WorldDataDirectory新設）・スコープ確定系はspec側レンズ（brainstorming/lenses/）と
spec-architecture-review が担当。plan側は「specレビュー済みの設計を前提に、計画固有の欠陥（波及・前提・検証）」に絞る。
