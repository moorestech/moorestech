# レビュー結果 — round-12-ssot-on-target

Critical: あり

## 修正方針

- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs`: `static _cachedUnlockState` フィールドを削除し、`ServerContext.GetService<IGameUnlockStateDataController>()` を毎回直接呼ぶ1本化に戻す（DIコンテナが権威ソース）。

## 最有力1件

**`MachineRecipeMaster.cs` — `_cachedUnlockState` が DI の第2の出所**

```csharp
// 追加されたコード（問題箇所）
private static IGameUnlockStateDataController _cachedUnlockState;
...
_cachedUnlockState ??= ServerContext.IsInitialized ? ServerContext.GetService<IGameUnlockStateDataController>() : null;
```

`IGameUnlockStateDataController` の権威ある出所は DI コンテナ（`ServerContext.GetService<>()`）の1本だけ。テストや再起動で DI コンテナが再構築されても `static` キャッシュは古いインスタンスを保持し続けるため、`MachineRecipeMasterUtil.TryGetRecipeElement` が stale なコントローラーを参照し続ける desync が発生する。`??=` による初回キャッシュは invalidation 機構を持たない。

最小修正: `_cachedUnlockState` フィールドを削除し、元の `ServerContext.GetService<IGameUnlockStateDataController>()` の直接呼び出しに戻す。ホットパス最適化が必要なら、DIコンテナ再構築イベントに連動した無効化機構を持たせることが前提となるが、それは最小修正でないため現状は直接参照が正解。

---

**その他の変更（Critical なし）**

- `RecipeViewerView.cs` の `UnlockedMachineRecipes()` を1回計算して複数ビューへ渡す変更: 各ビューが個別に計算していた重複を解消する方向であり SSOT 改善。同一データを1カ所で計算して渡すローカル変数キャッシュは過検知ガードの「1箇所でしか書き込まれず読み取り専用に配るキャッシュ」に該当。
- `ItemRecipeViewerDataContainer.cs` の `TryGetValue` → `infos[key]` 変更: `UnlockedCraftRecipes()` と判定ロジックを統一する変更。SSOT的には無関係。
- `MachineRecipeView.cs` / `RecipeTabView.cs` のシグネチャ変更と `using` 追加: 構造的リファクタリング。SSOT違反なし。
- `UnlockedEventPacket.cs` のパラメータ名変更 (`unlockedChallengeCategoryGuid` → `guid`): 命名の汎用化のみ。SSOT違反なし。
