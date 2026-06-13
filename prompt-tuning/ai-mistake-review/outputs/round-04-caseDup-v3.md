# レビュー結果: caseDup (prompt-v3)

Warning | FluidMapVeinGameObject.cs:36-44 | S | `MinPosition`/`MaxPosition` の式が誤り。Min は本来 `center - size/2`、Max は `center + size/2` なのに、両方とも `transform.position ± size/2 + bounds.center` という同じ符号構成で、Min/Max の差が center 項で打ち消されず、かつ `transform.position` と `bounds.center` を二重に加算している。AItemVeinGameObjectの既存実装に合わせ、Minは `RoundToInt(transform.position + center - size/2)`、Maxは `RoundToInt(transform.position + center + size/2)` に直す（符号と加算項を既存Vein実装と一致させる）。

Warning | FluidMapVeinDatastore.cs:216-218 | V | 「既存map.jsonとの互換のためnull許容」という後方互換コメント＋`FluidVeins == null` 早期return。本プロジェクトは後方互換不要。コメントを削除し、`FluidVeins` は非null前提（MapInfoJsonのデシリアライズで空リスト初期化）にして早期returnを除去する。

Warning | FluidMapVeinDatastore.cs:234-243 | P | `GetOverVeins` は毎呼び出し（=ポンプ毎tick想定）で全Veinを線形走査し、ヒット毎に新規Listをalloc。Minerと同じ呼び出し頻度なら毎tickのGC圧になる。設置時に1回引いてキャッシュする（ポンプ側で初期化時に解決して保持）か、少なくとも該当無し時は共有空リストを返すよう直す。

Nit | FluidMapVeinGameObject.cs:46-50 | V | `VeinFluidGuid`/`Bounds` の単純getterプロパティ新設。規約上は単純getter/setter禁止だが、既存VeinGameObjectが同型なら踏襲可。新規導入なら `Bounds` は既存パターン準拠、`VeinFluidGuid` は `Guid.Parse` を含むので許容範囲。owner踏襲なら据え置き。

Nit | FluidMapVeinDatastore.cs:222-226 | M | `GetFluidIdOrNull` で存在検証しスキップ＋LogError は妥当（M観点を満たす）。ただしマスタ不一致を「エラーログ＋スキップ」止まりにしている点は、起動時にまとめて検出したい場合は集約報告が望ましい。最小修正としては現状維持で可。

注: 提供diffはコミットメッセージ記載の `PumpFluidGenerationUtility`・`MapInfoJson.FluidVeins`追加・`PumpFluidVeinTest`・ServerContext/DI登録・MapExportAndSetting の変更を含んでいない（line247で打ち切れている）。Pump側のVeinDatastore参照タイミング（ServerContext直取得=V観点のDI回避疑い）やテスト前提ズレ(T)は本diffだけでは判定不可。

## 最有力の1件
Warning | FluidMapVeinGameObject.cs:36-44 | S — Min/Max座標式の符号・加算項の取り違え。これがズレるとVein範囲が実際の設置位置とずれ、ポンプが液体を生成しない/誤位置で生成する実害バグになる。既存ItemVeinGameObjectの式と一致させるのが人間の最小修正。
