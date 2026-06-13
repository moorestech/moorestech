Critical: なし

このdiffは `TrainHUDScreenState.cs` の既存メソッド内のロジック変更（毎フレーム送信 → `GetKeyDown` 検出時のみ送信）のみ。

- 新たに追加されたクラスメンバ（フィールド/メソッド）はゼロ。
- `isInput` はメソッドスコープのローカル変数であり、配置レビューの対象外（reviewer定義「メソッド内のローカル関数（配置の話ではない）」に準ずる）。
- `ClientContext.VanillaApi` の参照はdiff前から既存。AIが新たにグローバル取得を導入したわけではない。
- `TrainHUDScreenState`（UI状態クラス）がトレイン乗車中の入力を処理してAPIを呼ぶのは既存責務の範囲内。

配置・責務逸脱のCritical該当なし。

最有力1件: なし
