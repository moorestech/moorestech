# レビュー結果 — round-11 caseV sonnet-v7

## 指摘一覧

Warning | FluidMapVeinDatastore.cs:8 | V | コミットメッセージ・コメントで「互換のためnull許容」と明記。本プロジェクトは後方互換不要の規約があり、`if (mapInfoJson.FluidVeins == null) return;` は不要な後方互換ガード。削除し、FluidVeinsを非null前提（List初期化済み）に揃えるだけ。

Warning | FluidMapVeinDatastore.cs:19-23 | V | `MasterHolder.FluidMaster.GetFluidIdOrNull()` → nullチェック分岐。MasterHolderは設計上存在が保証されるコアコンポーネントであり、「マスタに存在しないGUID」は設定ミスなので null guard より Assert/例外で止める方が規約に沿う。ただし `GetFluidIdOrNull` の戻り値がnullでありうる（外部データ由来のGUID）ため、FluidIdの存在検証自体は正当。問題はnullチェック後に `continue` で黙ってスキップする点で、Debug.LogError で止まらない。最小修正: LogError → `throw` または `Debug.LogWarning` に留まらずワールドロード失敗として扱う（プロジェクト方針次第だが、現状は黙殺）。→ 軽微なため Nit 扱い。

Nit | FluidMapVeinDatastore.cs:19 | V | `GetFluidIdOrNull` は既存の `MapVeinDatastore` がどう書いているかに依存するが、diff 内の `MapVeinDatastore` 側は変更なし。`FluidMapVeinDatastore` だけ `OrNull` 系APIを使い、既存の `MapVeinDatastore` が例外スローAPIを使っているなら統一性が崩れている。→ 既存の書き方に合わせる。

Critical | PumpFluidVeinTest.cs:32-35 | D | `FluidMapVeinDatastore` と新規クラス群は `FluidXxx` ↔ `MapVeinDatastore`（`ItemXxx` に相当）の対関係にある。diff 内の `FluidMapVeinDatastore.GetOverVeins` と `MapVeinDatastore.GetOverVeins`（想定）はメソッド名・処理骨格がほぼ同一とみられる。**必須確認**: diff に `MapVeinDatastore` の変更が無いため、現在見える範囲では丸ごとコピペかどうか断言不可。ただしコミット文「Minerと同じ仕様で」と `GetOverVeins` という同名メソッドが登場していることから、既存 `MapVeinDatastore` との実装重複が強く疑われる。→ 既存 `MapVeinDatastore` を確認し、共通基底/汎用ヘルパーへ抽出すべきかを判断。

Warning | PumpFluidVeinTest.cs:619-621 | T | テストは `WaterVeinPos = (10,0,0)`、`SteamVeinPos = (20,0,0)`、`NoVeinPos = (30,0,0)` を固定値として使っているが、これらが `ForUnitTestMod` の `map.json` に実際に定義されているかを diff は示していない。diff 内に `map.json` の変更がない。Vein定義が無ければ `PumpOnMatchingFluidVein_GeneratesFluid` は常にPassせず（FluidVeins=null → 全スキップ）、`PumpOutsideFluidVein_GeneratesNothing` はたまたまPassする偽陽性になる。→ map.jsonに対応するFluidVeinエントリを追加する必要がある（diff漏れ）。

Warning | FluidMapVeinGameObjectInspector.cs:130 | V | `Undo.RecordObject` を `SetBounds` の後に呼んでいる。正しい Unity Undo の手順は「変更を記録してから変更を加える」（`RecordObject` → `SetBounds` の順）であり、逆順では Undo が機能しない。→ `Undo.RecordObject(fluidVein, "Change Bounds");` を `fluidVein.SetBounds(bounds);` の前に移動。

Nit | FluidMapVeinGameObject.cs:36-44 | V | `MinPosition`、`MaxPosition` がプロパティ（getter）として公開されている。規約「単純なgetter/setterプロパティ使用禁止」に抵触する可能性がある。ただし演算付きなので「単純なgetter」かどうかはグレー。→ 既存の `MapVeinGameObject` の書き方に合わせるか、必要なら `GetMinPosition()` メソッドへ変更。

Nit | FluidMapVeinGameObject.cs:69-73 | V | `Update()` 内で `#if UNITY_EDITOR` を使ってエディタ専用処理を呼んでいるが、規約「エディタ専用コードは `#if UNITY_EDITOR` で囲みファイル末尾に配置」に照らすと、`OnEditorUpdate()` メソッド自体とその呼び出しをファイル末尾の `#if UNITY_EDITOR` ブロックに移すべき。→ `private void Update()` 全体と `OnEditorUpdate()` をファイル末尾の `#if UNITY_EDITOR` 内に移動。

Nit | MapInfoJson.cs:383 | V | `FluidVeinInfoJson.VeinFluidGuid` が `[JsonIgnore]` の computed プロパティ（getter only）。`MapVeinInfoJson` に同様のパターンがあれば統一されているが、新規追加で `Guid.Parse` を毎回呼ぶのは無駄（同一インスタンス内で複数回呼ばれる可能性）。最小修正: バッキングフィールドにキャッシュするか、呼び出し側で一度だけ Parse する。→ 軽微。

---

## 最有力の1件

**Warning | PumpFluidVeinTest.cs:619-621 | T | map.json に FluidVein エントリを追加する diff が漏れている**

テストが依存する `WaterVeinPos`・`SteamVeinPos`・`NoVeinPos` に対応する FluidVein 定義が `ForUnitTestMod/map.json` に追加されていないと、3つのテストのうち2つは「Veinなし→常に生成されない」として誤ったPassまたは常時Failになる。テスト自体の論理は正しいが、前提データが diff に存在しない。→ `ForUnitTestMod` の `map.json` に `fluidVeins` エントリを追加する。
