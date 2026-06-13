# レビュー結果 — round-10 / caseV / sonnet-v6

Warning | FluidMapVeinDatastore.cs:21 | V | `mapInfoJson.FluidVeins == null` の早期returnは「後方互換のため」と明示されており、本プロジェクト規約の「後方互換不要」に違反する冗長な防御コード。MapInfoJsonのFluidVeinsをnull許容にせず `List<FluidVeinInfoJson>` のデフォルトを空リストにすれば不要。`[JsonProperty("fluidVeins")] public List<FluidVeinInfoJson> FluidVeins = new();` としてnullチェックを削除する。

Warning | PumpFluidGenerationUtility.cs:21-23 | V | `ServerContext.FluidMapVeinDatastore` への直アクセスはDIを迂回した隠れ依存。既に `GearPumpComponent` / `ElectricPumpProcessorComponent` は `blockPositionInfo` をコンストラクタ引数で受け取っているのだから、`IFluidMapVeinDatastore` も同じくコンストラクタ引数で渡す設計が規約準拠。`GenerateFluids` の引数に `IFluidMapVeinDatastore fluidMapVeinDatastore` を追加し、呼び出し元（両 Pump コンポーネントのコンストラクタ）でDI経由で受け取った値を渡す。

Warning | FluidMapVeinGameObjectInspector.cs:130-131 | V | `EditorGUI.EndChangeCheck()` が `true` のとき `SetBounds` → `Undo.RecordObject` の順になっている。`Undo.RecordObject` は変更を**加える前**に呼ぶのが正しいUnityの作法（変更後に呼んでもアンドゥが効かない）。`Undo.RecordObject(fluidVein, "Change Bounds")` を `SetBounds(bounds)` の前に移動する。

Nit | FluidMapVeinGameObject.cs:68-77 | V | `Update()` 内の `#if UNITY_EDITOR` でエディタ専用コードを呼び出しているが、規約では「エディタ専用コードは `#if UNITY_EDITOR` で囲みファイル末尾に配置」とある。`OnEditorUpdate()` の定義がエディタ専用でないため（`[ExecuteAlways]` でビルドにも含まれる）、`Update()` ごとエディタ専用ブロックに移動するか、ランタイムビルドから除外するよう `#if UNITY_EDITOR` を `private void Update()` 定義全体に被せる。

Nit | PumpFluidVeinTest.cs:683 | T | `new BlockInstanceId(10)` はテスト内でハードコードされたIDで、他テストが同IDのブロックを既に生成していた場合に衝突しうる。テストモジュールの慣例に合わせて `BlockInstanceId.Create()` 等の一意生成方式か、他テストと重複しない固定値の選択根拠をコメントで示す。

Nit | FluidMapVeinDatastore.cs:498-505 | P | `GetOverVeins` が毎 tick 呼ばれるにも関わらず全ベインをフルスキャンしている。現状のデータ量では問題ないが、`MapVeinDatastore` の既存実装と乖離したまま放置されると将来の拡張で困る。現時点では指摘に留め、ベイン数が問題になった段階で空間インデックス化する。

---

**最有力の1件**

Warning | PumpFluidGenerationUtility.cs:21-23 | V | `ServerContext.FluidMapVeinDatastore` への static 直アクセスによる隠れ依存。DI経由で渡された `IFluidMapVeinDatastore` をコンストラクタ引数として両 Pump コンポーネントに持たせ、`GenerateFluids` の引数で渡す形に変更する。
