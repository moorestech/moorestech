# レビュー結果 — round-10-caseDup-sonnet-v6

Critical | FluidMapVeinGameObjectInspector.cs:128-130 | S | `Undo.RecordObject` を `SetBounds()` の後に呼んでいるため Undo が機能しない。`Undo.RecordObject(fluidVein, "Change Bounds")` を `fluidVein.SetBounds(bounds)` より前に移動する。

Warning | FluidMapVeinDatastore.cs:217-218 | V | 「既存map.jsonとの後方互換のためnull許容」と明記したnullチェックを足しているが、本プロジェクトは後方互換不要（AGENTS.md）。`if (mapInfoJson.FluidVeins == null) return;` を削除し、`FluidVeins` を非null必須フィールドにする。

Warning | FluidMapVeinGameObject.cs:49 | V | `public Bounds Bounds => bounds;` は SerializeField をそのまま返す単純getterプロパティで規約違反。publicフィールドに変えるか、アクセス箇所が限定的なら `bounds` を直接渡す形に変更する。

Warning | FluidMapVeinGameObject.cs:74-78 | V | `OnEditorUpdate()` はエディタ専用処理だが `#if UNITY_EDITOR` で囲まれていない（囲んでいるのは呼び出し側の `Update()` 内だけ）。AGENTS.md の「エディタ専用コードは #if UNITY_EDITOR で囲みファイル末尾に配置」に従い、`OnEditorUpdate()` メソッド全体を `#if UNITY_EDITOR` ブロックで囲んでファイル末尾に移動する。

Nit | FluidMapVeinDatastore.cs:235-244 | D | `GetOverVeins` は呼び出しのたびに `new List<IFluidMapVein>()` を生成する。結果が空のケースが多い場合に無駄だが最小修正ではないため Nit 止まり。即修正不要。

---

**最有力の1件**: `FluidMapVeinGameObjectInspector.cs:128-130` — `Undo.RecordObject` を変更後に呼んでいるため Undo が完全に壊れている。`SetBounds()` 呼び出しの直前に `Undo.RecordObject` を移動するだけで修正できる実害バグ。
