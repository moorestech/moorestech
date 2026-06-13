## レビュー結果 — caseV (ポンプ液体供給のFluidMapVein連動)

Warning | PumpFluidVeinTest.cs:683-686 | T | `PlacePoweredPump` が `EnergySegment` をローカル変数として作るだけでワールドの電力系へ接続もtickもしておらず、segmentはreturn後に破棄される。`SupplyPower` が呼ばれず powerRate=0 のままで「水が溜まる」テスト(PumpOnMatchingFluidVein)は本来Greenにならない可能性が高い。人間の修正: segmentを毎tick `segment.Update()` 相当で回す既存の電力供給ヘルパ（他のElectricPumpテストの電力供給パターン）に差し替える。

Warning | FluidMapVeinDatastore.cs:480-482 | V | 「既存map.jsonとの互換のためnull許容」という後方互換コメント付きの `if (mapInfoJson.FluidVeins == null) return;` を新設している。本プロジェクトは後方互換不要。人間の修正: コメントと早期returnを削除し、FluidVeinsは常に存在する前提（必要ならexportで空リストを必ず出力）にする。

Nit | MapInfoJson.cs:372 | V | コミットメッセージで `fluidVeins` を「null許容で後方互換」と明記している点も同様に後方互換前提の設計。人間の修正: 互換目的の設計意図を外し、空配列前提に統一。

Warning | MoorestechServerDIContainerGenerator.cs:519-540, 569-584 | D | 鉄道・Entity関連の既存登録行が `-`/`+` で大量に差し替わっているが中身は同一でインデント/行末空白だけの変更。本筋(FluidMapVein登録1行)と無関係な空白チャーンでdiffを汚しレビュー/コンフリクトリスクを増やす。人間の修正: 該当行の空白変更を元に戻し、追加した `AddSingleton<IFluidMapVeinDatastore, FluidMapVeinDatastore>()` の1行だけを残す。

Nit | FluidMapVeinDatastore.cs:498-507 | D | `GetOverVeins` の包含判定が毎回 `new List` を確保し全Veinを線形走査。ポンプ毎tick呼び出しで件数が増えると無駄だが、Veinが少数なら許容範囲。人間の修正(任意): 既存 `MapVeinDatastore` の同等メソッド実装に合わせる（独自実装の重複を避け同パターンへ寄せる）。

Nit | FluidMapVeinGameObjectInspector.cs:128-131 | S | `Undo.RecordObject` を `SetBounds` の後に呼んでおり、変更前状態がUndoに記録されずUndoが効かない。人間の修正: `Undo.RecordObject(fluidVein, "Change Bounds");` を `SetBounds` 呼び出しの前に移動。

---

最有力の1件: **PumpFluidVeinTest.cs の電力供給が成立しておらず、肯定系テスト(PumpOnMatchingFluidVein_GeneratesFluid)がそもそも電力0でVeinロジック以前に破綻している恐れ（T）**。Vein一致でも水が溜まらず、テストが意味をなさない/誤って通る危険があるため最優先で電力供給ヘルパへ差し替える。
