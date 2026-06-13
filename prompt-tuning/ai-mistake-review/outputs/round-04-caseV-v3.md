# caseV レビュー結果 (prompt-v3)

Warning | PumpFluidGenerationUtility.cs:305 | S/M | `GetFluidId(gen.FluidGuid)` をVeinチェックのためループのGenerateTime>0直後で呼ぶが、マスタに存在しないFluidGuidだと`GetFluidId`が例外/不正値を返し得る（datastore側は`GetFluidIdOrNull`で存在検証しているのにこちらは生Get）。→ ここも `GetFluidIdOrNull` を使い、nullなら`continue`にする。

Warning | FluidMapVeinDatastore.cs:480-482 | V | `mapInfoJson.FluidVeins == null` の「既存map.jsonとの互換のため」early returnは後方互換コードで本プロジェクトでは不要。→ null許容コメントとnullガードを削除し、map.json側に空配列出力を保証（GetFluidVeinInfoは常にListを返すので問題なし）。

Warning | PumpFluidVeinTest.cs:683-686 | T | `PlacePoweredPump`で生成した`EnergySegment`をローカル変数に置くだけで、どこにも登録・tick駆動されない。ElectricPumpへ実際に電力が供給されずpowerRate=0のままになり、`PumpOnMatchingFluidVein_GeneratesFluid`が常に失敗する（生成テストの前提が成立しない）。→ segmentをワールドのエネルギー系へ登録するか、テストユーティリティの既存電力供給パターンに合わせる。

Warning | PumpFluidVeinTest.cs:619-623 | T | テストのVein座標(10,0,0)/(20,0,0)とWaterFluidGuid"...1234-...001"がForUnitTestModのmap.json/マスタに実在する前提だが、この差分にはそのmap.json更新が含まれていない。→ ForUnitTestModのmap.jsonにfluidVeinsエントリを追加するコミットが別途必要（無ければ全ケース失敗）。

Nit | GearPumpComponent.cs:227 / ElectricPumpProcessorComponent.cs:258 | P | `_blockPositionInfo.OriginalPos`をUpdate(毎tick)で渡し、Utility内で毎tick`GetOverVeins`を線形走査する。ポンプ位置は不変なので生成時に1回Vein判定すれば足りる。→ 影響軽微だが、コンストラクタでVein有無/対象FluidIdを確定キャッシュする方が素直（最小修正としては現状維持可）。

Nit | FluidMapVeinDatastore.cs:498-507 | P | `GetOverVeins`が毎呼び出しで`new List`を確保し全Vein線形走査。Vein数が少なければ許容範囲。→ 現状維持可。

Nit | MapInfoJson.cs:383 | S | `VeinFluidGuid => Guid.Parse(VeinFluidGuidStr)`はVeinFluidGuidStrがnull/不正文字列だと例外。マップ生成出力なので通常問題ないが外部JSON由来。→ datastore側で既にOrNull検証しているため実害は低い。

---

**最有力1件**: PumpFluidVeinTest.cs:683-686（T） — `EnergySegment`を作るだけで未登録・未tickのため電力が供給されず、メインの正常系テスト`PumpOnMatchingFluidVein_GeneratesFluid`が構造的に通らない。テストが実装の駆動経路と噛み合っておらず、人間が最初に直す箇所。
