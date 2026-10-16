# 地形埋まり1/3m許容と既定高さの自動1段上げ Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 既定の設置Yと地形埋まり判定を「底面から1/3mまでの沈みを許容する」1つの規則から出し、ベルト等が地形に埋まらないようにする。

**Architecture:** `PlacementGroundCellResolver.ResolveCellY` を ceil(地形最高点 − 1/3 − 0.001) に変え、`PlacementTerrainSinkQuery.IsSunkAbove` は従来どおり `baseY < ResolveCellY(groundMax, 0)` で同じ規則から出す。新規則では鉱脈パッドの格子誤差（約0.02m）が許容量に吸収されるので、高さ格子1段（quantization step）の受け渡しを全経路から撤去する。

**Tech Stack:** Unity C#（Client.Game）、NUnit（Client.Tests）

## Requirements

1. 埋まり量 = 占有セルの地形最高点 − 底面Y。≤ 1/3m + 0.001m なら許容、超えたら埋まり（受け入れ: `IsSunkAbove(10, 10.334f)` が false、`IsSunkAbove(10, 10.336f)` が true）
2. 既定設置Y = 埋まり量が許容量以下になる最低整数（受け入れ: 地表32.4 → 33、32.2 → 32、32.0 → 32、31.99 → 32）
3. 鉱脈パッド（整数より格子1段＋クリアランスだけ下）は整数セルへ置かれる（受け入れ: 既存 `PlacementGroundTerrainLatticeTest` のパッドケースが通る）
4. Q/E 補正は解決後に加算（受け入れ: 地表20.4・+3 → 24、−2 → 19）
5. 全設置系に効く: `ResolveCellY` を通る通常設置・BP・ベルト途中セル・レール橋脚・電柱・歯車ポール（受け入れ: 呼び出し経路は既存のまま、`ResolveCellY` 以外の丸めが残っていないことを grep で確認）
6. 許容量を超える配置は既存の「地形に埋まっています」で不可（受け入れ: 高さ3ブロックを地表30.3に底30で可・29で不可）
- やらないこと: サーバー・セーブ形式の変更、既存ブロックの移動、プレイテスト版への配布

## Global Constraints

- 許容量は `1f / 3f`、浮動小数余裕は既存 `FloatingPointGroundTolerance = 0.001f`（ADR 0082 決定1）
- コメントは日英2行セット、1ファイル200行未満、デフォルト引数禁止
- コンパイル: `uloop compile --project-path ./moorestech_client`

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 新規型なし。既存の丸め関数の規則変更と未使用になる高さ格子引数の撤去のみ
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 発火なし

---

### Task 1: 1/3m規則と高さ格子の受け渡し撤去

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementTerrainSinkQuery.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/GroundHeightProbe.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/GroundHeightQuantization.cs`（＋Unityが消す.meta）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlacementUnitCellResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolver.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementTerrainSinkQueryTest.cs`, `PlacementGroundCellApplyTest.cs`, `PlacementGroundTerrainLatticeTest.cs`, `GroundHeightProbeTest.cs`, `Calculation/PlaceSystemUtilGroundAndOffsetTest.cs`, `../PlaceSystemUtilCalcPlacePointTest.cs`, `../Shared/PlacementUnitCellResolverTest.cs`, `../Blueprint/Paste/BlueprintPasteOriginResolverTest.cs`、旧規則の期待値を持つ fixture: `../Common/NormalPlacementPreviewStepTest.cs`（地表30.6→30.3）・`PlacementGroundFollowStepTest.cs`（slab中心x.9→x.7）・`../Blueprint/Paste/BlueprintPasteRunBuilderTest.cs`（地面中心5/14→4.7/13.7）・`Entry/FootprintGroundedEntryFixture.cs`（照準20.8→20.2）。期待値は変えず fixture の地表端数を1/3m以内へずらして意図を保つ

**Interfaces:**
- Produces:
  - `internal static int PlacementGroundCellResolver.ResolveCellY(float groundMaxHeight, int heightOffset)`
  - `internal static bool PlacementTerrainSinkQuery.IsSunkAbove(int blockBaseCellY, float groundMaxHeight)`
  - `public static bool GroundHeightProbe.TryGetFootprintMaxGroundHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize, out float maxHeight)`
  - `public static Vector3Int PlaceSystemUtil.CalcPlacePointBySize(Vector3Int rotatedSize, Vector3 hitPoint, int heightOffset, PreviewSurfaceType? surfaceType)`
  - `public static Vector3Int PlacementUnitCellResolver.ResolveCell(Vector3 hitPoint, PreviewSurfaceType? surfaceType, int heightOffset)`
  - `public static Vector3Int BlueprintPasteOriginResolver.ResolveOrigin(Vector3Int footprintSize, Vector3 hitPoint, PreviewSurfaceType? surfaceType, int heightOffset)`

- [ ] **Step 1: 規則を実装する**

```csharp
// PlacementGroundCellResolver
// 地形への沈みとして許容する底面からの深さ（ADR 0082）
// The depth below the bottom that a terrain sink may reach (ADR 0082)
private const float AllowedTerrainSinkMeters = 1f / 3f;
private const float FloatingPointGroundTolerance = 0.001f;

// 沈みが許容量以下になる最も低いセル。整数直下の鉱脈パッドもこの許容量に収まり整数セルになる
// The lowest cell whose sink stays within the allowance; a vein pad just under an integer also fits and lands on it
internal static int ResolveCellY(float groundMaxHeight, int heightOffset)
{
    return Mathf.CeilToInt(groundMaxHeight - AllowedTerrainSinkMeters - FloatingPointGroundTolerance) + heightOffset;
}

// PlacementTerrainSinkQuery
internal static bool IsSunkAbove(int blockBaseCellY, float groundMaxHeight)
{
    return blockBaseCellY < PlacementGroundCellResolver.ResolveCellY(groundMaxHeight, 0);
}
```

`QuantizedPadUndershoot`・`GroundHeightQuantization`・`TryGetFootprintMaxGroundHeight` の step out・`CalcPlacePointBySize`/`ResolveCell`/`ResolveOrigin` の step 引数と `PlaceSystemUtil.TryGetRayHitPlacePointBySize` の step 算出を削除する。

- [ ] **Step 2: テストを新規則へ更新する**（期待値は Requirements の受け入れ値。`PlacementTerrainSinkQueryTest` の地表は30.6→30.3、ケースは (10,10.3,false)(10,10.334,false)(10,10.336,true)(10,10.6,true)(9,10.3,true)(11,10.6,false)。`許容誤差は整数直下だけを引き上げる` は「沈み1/3mまでは同じセル」へ改名し 32.0/32.2/32.333→32、32.34/32.4→33、31.99→32。格子テストは 10−1.5step も 10 になる。オフセットテストは地表20.4→+3=24・−2=19。負の高さ −3.4 は −4→ ceil(−3.734)=−3 になるので −3 へ）

- [ ] **Step 3: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem"`
Expected: ErrorCount 0 / 全PASS。加えて `grep -rn "FloorToInt(.*ground\|QuantizationStep" moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem` が0件

- [ ] **Step 4: コミット**

### Task 2: moores-code-review（必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可））

### Task 3: PR作成（pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする。全作業がコミット・push済みでPRがマージ可能な状態を確認して終える）

## 判断記録（ADR）

- 範囲削除・BPコピーの箱を旧規則ブロック向けに広げる bug-pass 自動修正は採らない（別タスク bd に積む）。出所: ユーザー裁定 2026-10-16 原文「このまま進めて良い」（推奨「戻して別タスクに積む」に対して）。`.decisions/2026-10-16-範囲削除とBPコピーの箱は旧規則ブロック向けに広げない.md`

- user-simulator review: 適用推奨0・Warning1（旧floor規則を焼き込んだ fixture 3本が Files から脱落）→ Files・Step 2 に追記し、実装で fixture 側を直した。出所: シミュレーター予測（ユーザー未提示の機械的修正）

- 設計: docs/adr/0082-terrain-sink-tolerance-one-third-meter-and-auto-raise.md、`.decisions/2026-10-16-地形埋まりは底面から3分の1mまで許容し既定高さを自動で1段上げる.md`
- 高さ格子1段の受け渡しを全経路から撤去する。出所: agent前提（新規則ではパッド誤差が許容量に吸収され step を読む箇所が無くなる。未使用引数を残さない＝設計原則「変更の波及を恐れない」）
- タスクは1本にまとめる。出所: agent前提（規則変更と引数撤去は同時にしかコンパイルが通らない）
- 実装はこのセッションで行う。出所: ユーザー裁定 2026-10-16 原文「moores-writing-plans、実装・検証・PR まで、このセッションで完遂してください。」
- unityプレイ録画テストは行わず、実TerrainData を使う EditMode テスト（`PlacementGroundTerrainLatticeTest`）と既存PlaceSystemテスト群で検証する。出所: agent前提（変更は純粋な丸め関数で、全設置系が同じ関数を通る。見た目の確認は1/3m規則の数値で決まる）
