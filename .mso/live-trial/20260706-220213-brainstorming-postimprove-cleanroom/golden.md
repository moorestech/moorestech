# ゴールデン: ユーザーが「こうであるべき」と提示した参照プラン（別AIとの複数ターン対話の成果物）

（注: 以下のうち「90度回転問題の存在」「ジェネリクスに定義を書かせる方式」「形状定義をblocks.ymlトップレベルに置く指定」は、元対話でユーザー自身が入力・指摘したもの）

## 現状把握: 接続はBlockConnectorComponentの座標一致のみ。歯とシャフト、90度回転した歯車同士も繋がる。IBlockConnector共通化済みが受け皿。

## 前提（自己解決した決定 — 番号付き・拒否権形式）
1. 形状定義はblocks.ymlトップレベルにconnectorShapes（shapeGuid + name）
2. 互換表は無向ペアリスト connectableShapePairs（shape0/shape1）。同一形状同士も自動接続とせず(A,A)を明示登録（暗黙ルールを持たない。オス-オス不可を表現可能に）
3. IBlockConnectorにshapeGuid（optional）。未設定=制約なし → 既存マスタ移行不要
4. 互換表チェックは本体で必ず適用、注入判定は「追加条件」のみ（バイパス事故を構造的に防止。YAGNI）
5. 判定は接続確立時のみ。設置プレビューはスコープ外（将来課題として記載のみ）
6. 90度問題は「回転軸が平行でなければ接続不可」を歯車ドメイン専用判定クラスで実装。同じ「歯」形状同士なので表では表現不可能 — 注入ポイントの仕事。軸はBlockDirectionから導出、データ追加不要。判定のyaml選択可能化はしない（YAGNI）
7. 実マスタへの形状付与はスコープ外（optionalなので既存挙動不変）。テスト用マスタのみ

## 判定ロジック注入 3案比較
- 案A: データのみ（形状ID+互換表） — 90度問題（同一形状・向き依存）が表現できず却下
- 案B（採用）: 第2型パラメータ BlockConnectorComponent<TTarget, TConnectJudge> で判定クラスを型レベル束縛。閉じたジェネリック型が検索キーになり、両側ブロックが同じ判定クラスを使うことが型システムで保証、注入ミスがコンパイルエラーに。コスト: 約20ファイルの型名変更（コンパイルエラー駆動で漏れなく検出）
- 案C: TTargetドメインインターフェースにCanConnect実装 — IBlockInventory全実装に無関係な責務が波及するため非推奨

## 全体像
blocks.ymlトップレベル: connectorShapes / connectableShapePairs（foreignKey → validate-schema観点も設計書に含む）
実行時: MasterHolderに順序正規化HashSet<(Guid,Guid)>、3段判定（位置一致→形状互換表(optional=ワイルドカード)→Judge.CanConnect）、GearConnectJudge=軸平行チェック

## 確認質問（2つだけ・意思決定型）
1. 「軸が平行なら接続可、直交なら不可」はゲームデザイン上正しいか（ベベルギア考慮不要か）
2. 案B確定でよいか → 設計書としてdocs/plans/にまとめる
