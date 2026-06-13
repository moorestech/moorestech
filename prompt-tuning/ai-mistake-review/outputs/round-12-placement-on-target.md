# round-12: placement review — caseSSOTPlace-ai-diff

Critical: あり

## 修正方針

- `MachineRecipeMaster.cs:+2行目（_cachedUnlockState フィールド）+ ??= 初期化ロジック`:
  `MachineRecipeMasterUtil` は `Core.Master` 名前空間の純粋な静的ユーティリティ（レシピ検索・確認の提供が責務）。そこに `IGameUnlockStateDataController` という**ゲームの実行時状態**を持ち込み、`ServerContext.GetService<>()` でグローバル取得するのは責務外。
  直し方: `TryGetRecipeElement` の呼び出し側（機械ブロックの実装など）がすでに `IGameUnlockStateDataController` を DI で受け取っているはずなので、アンロック判定を**呼び出し側に戻す**か、`IGameUnlockStateDataController` を `TryGetRecipeElement` の引数として受け取る形にして `_cachedUnlockState` フィールドを削除する。

## 最有力 1 件

`MachineRecipeMaster.cs` — `_cachedUnlockState` フィールドの追加。
`*Util` クラスが実行時サービスをグローバル取得でキャッシュするのは「グローバル取得による隠れ依存」の典型。`ServerContext.GetService<IGameUnlockStateDataController>()` の呼び出しを静的フィールドに閉じ込めることで、テスト・初期化順・生存期間の問題が表面化しにくくなる。アンロック判定ロジックごと、それを担うべきブロック実装クラス（あるいは上位の呼び出し元）へ移し、依存は引数またはコンストラクタ注入で受け取るべき。
