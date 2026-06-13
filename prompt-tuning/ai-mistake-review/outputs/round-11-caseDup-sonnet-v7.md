# レビュー結果: round-11-caseDup-sonnet-v7

---

## 指摘一覧

**Critical | FluidMapVeinGameObject.cs:全体 | D | クラス丸ごと複製 — ItemMapVeinGameObject.cs + MapVeinGameObjectService に既に共通化済みのロジックを再インライン化している。MinPosition/MaxPosition 計算・SetBounds/NormalizeBounds・OnDrawGizmosSelected の全コードが MapVeinGameObjectService の既存メソッドと一致する。FluidMapVeinGameObject は ItemMapVeinGameObject と同様に MapVeinGameObjectService を保持し、SetBounds は `bounds = MapVeinGameObjectService.NormalizeBounds(setBounds)` 1行に、OnDrawGizmosSelected は `Service.DrawGizmo(bounds, Color.blue)` 1行に置き換えるだけで済む。**

Warning | FluidMapVeinGameObjectInspector.cs:全体 | D | ItemMapVeinGameObjectInspector は `MapVeinGameObjectEditorService` を使い共通化済みなのに、FluidMapVeinGameObjectInspector は BoxBoundsHandle と Undo.RecordObject を直接再インライン化している。`_editorService.DrawSceneGUI(fluidVein.Service, fluidVein.Bounds, fluidVein.SetBounds, fluidVein, Color.blue)` の1行に置き換える。

Warning | FluidMapVeinDatastore.cs:218 | V | 不要な後方互換コードを追加している。コメント「既存map.jsonとの互換のため」および `if (mapInfoJson.FluidVeins == null) return;` が後方互換ガードそのもの。ItemMapVeinDatastore には同等ガードが無く、本プロジェクトは後方互換不要の規約。null ガード行を削除し、mapInfoJson.FluidVeins は null 非許容として扱う（MapInfoJson の FluidVeins フィールド定義も null 非許容に変更）。

Nit | FluidMapVeinDatastore.cs:222-227 | S | ItemMapVeinDatastore は `ExistItemId` チェック後に `GetItemId` を呼ぶ2ステップ方式だが、FluidMapVeinDatastore は `GetFluidIdOrNull` の1ステップ方式になっている。どちらが正式な流儀かは既存 API 次第だが、同一 Datastore レイヤーで方式が食い違っており、将来の保守で混乱を招く。ItemMapVeinDatastore の方式（ExistXxx → GetXxx）に統一するか、ItemMapVeinDatastore 側も GetFluidIdOrNull 型に揃える。

---

## 最有力の1件

**Critical | FluidMapVeinGameObject.cs:全体 | D**

`FluidMapVeinGameObject` は `ItemMapVeinGameObject` + `MapVeinGameObjectService` の丸ごとコピペ複製であり、かつ既に抽出済みの共通サービスロジックを再インライン化している（規約の巻き戻し）。MinPosition/MaxPosition/SetBounds/OnDrawGizmosSelected の全実装を削除し、`_service` フィールドと `Service` プロパティ経由で共通サービスに委譲するだけでよい。
