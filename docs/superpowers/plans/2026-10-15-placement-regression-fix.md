# 10/15アプデ設置退行の修正 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** #1499 以降ほぼ置けなくなった歯車チェーンポール・レール橋脚の設置と、MaxStack超の素材を持つ歯車チェーンポールの撤去を、それぞれ直す。

**Architecture:** (1) クライアントの設置プレビューが検出器の無いPrefabへ生成する地形判定箱 `PlacementGroundContactVolume` を最下段セルの上へ持ち上げる。高さ1のブロックには判定箱を作らない（ADR 0080）。さらに4設置系（ベルト・レール橋脚・電柱ゴースト・歯車チェーンポール）の設置Yを、照準1点の floor から通常設置と同じ「占有セルの地形最高点を含むセル」へ揃える（`PlacementGroundCellResolver.TryResolveCellFromGround` を1セル版で使う）。同期問い合わせ `PlacementGroundContactQuery` は変えない。(2) サーバーの接続線返却部品 `ConnectionLineRefundItems.Create` を、既存の `IItemStackFactory.CreateSplitStacks` で MaxStack ごとの複数スタックに分けて返すようにする。この部品を使う6経路（電線・歯車チェーン・レールの撤去と切断）がまとめて直る。

**Tech Stack:** Unity (C#)、NUnit（EditMode／UnityTest）、uloop CLI、MessagePack プロトコル

## Requirements

1. 生成判定箱の y 範囲は `[1+inset, blockSize.y-inset]`（inset = `Physics.defaultContactOffset*2`）。x/z は従来どおり `[inset, size-inset]`。受け入れ: 1x3x1 で生成した箱の `bounds.min.y == 1+inset`、`bounds.max.y == 3-inset`
2. `blockSize.y <= 1` のときは判定箱を作らない。受け入れ: 3x1x3・1x1x1 で `GroundCollisionDetector` が0個、崖の中でも `DetectCurrentGroundOverlap()==false`
3. Prefab に既存の `GroundCollisionDetector` がある場合の挙動は変えない。受け入れ: 既存テスト `ExistingDetectorRemainsTheOnlyContactSource`・`PlacementTerrainContactShapeTest` 全件が無改変で通る
2b. 4設置系の設置Yは、地面ヒットのとき占有セルの地形最高点を含むセル（ADR 0047 と同じ規則）＋高さオフセット。ブロック面ヒット（積み重ね）はレイのセルのまま。占有セルの地表が取れないときは「地面が見つかりません」の理由行を出しプレビュー（ゴースト）を出さない。受け入れ: 3セルで1m上がる斜面で照準セルY=20のとき3x4x3は Y=21、オフセット1で22、ブロック面ヒットは不変、地表なしは `GroundNotFound`。歯車チェーンポールは理由行 `PlaceGroundNotFound` を出す
4. 回帰（設置）: 地表の高さが 0.3／0.7 の平地と、3セルで0.5m上がる斜面（フットプリント内の地表 0.2〜0.7）で、歯車チェーンポール(3x4x3)・コンパクト歯車チェーンポール(1x2x1)・レール橋脚(3x1x3)・ベルト(1x1x1) の判定箱がすべて非接触になる
5. 回帰（崖）: 地表が最下段セルを超えて上部セルへ食い込む崖（3x4x3 は上面2.0、1x2x1 は上面1.5）では接触になる
6. `ConnectionLineRefundItems.Create` は素材ごとに `CreateSplitStacks(itemId, count)` の結果を返す。受け入れ: MaxStack=100 の素材230個 → [100,100,30]、MaxStack=50 の素材120個 → [50,50,20]、ちょうど100個 → [100]、数0と空アイテムは何も返さない。例外は出ない
7. 回帰（撤去）: MaxStack を超える素材記録を持つ歯車チェーンポールを `va:removeBlock` で撤去すると成功し、素材の総数がインベントリに入る（複数スタック）
8. 複数スタックの挿入チェック: 空きが分割後のスタック数より少なければ撤去は `InventoryFull` で拒否され、ブロック・接続記録・インベントリは不変。空きがちょうどスタック数なら成功
9. 回帰（切断）: MaxStack 超の素材記録を持つ歯車チェーンの切断（`va:gearChainConnectionEdit` の Disconnect）が成功し、総数が返る
10. 実ゲーム（unityプレイ録画テスト）: 生成地形の端数のある平地と斜面で、歯車チェーンポール・コンパクト歯車チェーンポール・レール橋脚・ベルトのゴーストが照準位置によらず「地形に埋まっています」にならず設置できる

**やらないこと（スコープ境界）:**
- 旧レールが走れない件（#1429 の列車同期の永久待機説）。プレイヤーのログ待ち
- 4設置系から地形ゲートを撤去する案（ADR 0080 で棄却）
- 通常設置（`CommonBlockPlaceSystem`）とBP貼り付けの挙動変更。ADR 0047 のまま
- `ConstructionCostService.CreateRefundItems` の同形の未分割（下記「本PR外のリファクタ提案」へ）
- Prefab への `GroundCollisionDetector` 追加などアセット変更

## Global Constraints

- 1ファイル200行未満。1ディレクトリ10ファイルまで（`.meta` を除く `.cs` で数える）
- コメントは日本語1行→英語1行の2行セット。各1行に収める
- `.meta` は手で作らない（Unity が生成したものはコミットしてよい）
- try-catch・`Func<>`・partial・デフォルト引数は使わない
- 意図して黙って続行する行には `silent-ok: <続行してよい理由> / <誰がどこで気づくか>` を同じ行か直前の行に書く
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client` を実行する（ErrorCount 0）
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"` で絞って実行する。サーバーテストもクライアントプロジェクトから実行できる
- セーブ形式は変えない（マイグレーション不要）
- 作業場所: worktree `placement-regression-20261015`（環境ローカル規約のworktree置き場）、ブランチ `fix/placement-regression-20261015`

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / Task 2 追加後に再検査。Y決定は既存 TryResolveCellFromGround へ寄せ新機構なし。動作中操作の死活: 積み重ね（面ヒット）・Q/E高さ・ベルトのドラッグ・橋脚の接続モード設置は生存（配置表と前例は「配置と前例」節）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強2・弱2・第3バケツ1 / Task 2 追加後に再実施。強2・弱2は委任方針（細部は推奨既定）と前例でYes側が決まるため前提宣言へ降格して全件planへ反映（判断記録参照）、第3バケツはBP側の同役割を本PR外提案へ追記

---

## 配置と前例

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `BlockPreviewObject.ConfigureTerrainContact` の箱形状変更（既存メソッド） | Client.Game（PreviewController） | 既存の生成判定箱。同期問い合わせはそのまま | 同メソッド自身（#1499） |
| 2 | `ConnectionLineRefundItems.Create` の分割（既存メソッド） | Game.Block（ConnectionLine） | 既存 `IItemStackFactory.CreateSplitStacks` | `Game.Map/VeinHandMiningService.cs:108`・`Game.Map/VanillaStaticMapObject.cs:115` |
| 3 | `GearChainEditTestWorld.SendRemoveBlock`（テスト補助の追加） | Tests（GearChain） | 既存 `Send` と同じ `PacketResponseCreator` 経路 | 同ファイルの `Send` |
| 4 | テスト用フィクスチャのブロックを `ElectricPoleId`(1x3x1) へ変更 | Client.Tests（Preview） | — | `ForUnitTestModBlockId.ElectricPoleId` |
| 5 | `PlaceSystemUtil.ResolveFootprintGroundedRayHitBlockPosition` / `TryGetFootprintGroundedRayHitBlockPosition` / `ResolveFootprintGroundedCell`（既存クラスへ追加） | Client.Game（PlaceSystem/Util） | 既存 `PlacementGroundCellResolver.TryResolveCellFromGround` | 同クラスの `TryGetRayHitBlockPosition`、`Blueprint/Paste/BlueprintPasteOriginResolver.TryResolveFootprintOrigin`（カーソル1点→占有最高点の前例）、`Ground/PlacementGroundFollowStep`（面ヒットは触らない） |
| 6 | `FootprintGroundedCellResult` enum | Client.Game（PlaceSystem/Ground） | 3状態の戻り値 | `ChainPreview/ChainCellBlockReason`（地表なしを理由として返す前例） |
| 7 | `GearChainPoleGhostState` enum（`HasGhost`・`GhostTooFar` の bool 2つを置換） | Client.Game（GearChainPoleConnect/Modes） | 入力構造体の状態フィールド | 同 `GearChainPolePlaceExtendInput` |

データフロー（設置）: 照準レイ →［Task 2: 占有セル最高点でY決定］→ 4設置系 → `PlacementPreviewBlockGameObjectController.SetPreview(…, true)` → `BlockPreviewObject.SetTerrainContactEnabled(true, master.BlockSize)` →［生成判定箱］→ `DetectGroundOverlaps()` → 各設置系が `ApplyGroundOverlapsAndReport` で可否と文言を決める。本planは［Y決定］と［生成判定箱］の形を変える。Y決定は通常設置・BPと同じ `TryResolveCellFromGround` へ寄せるだけで、新しい書き手・分岐は足さない（地表なしの理由行は既存の `GroundNotFound` を流用）。

---

### Task 1: 地形判定箱を最下段セルの上へ持ち上げる（クライアント）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs:95-119`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview/PreviewBlockColliderIsolationTest.cs:91-171`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview/PlacementPreviewPhysicsFixture.cs:44,61`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview/PlacementTerrainContactBaseCellSinkTest.cs`

**Interfaces:**
- Consumes: なし
- Produces: `internal void BlockPreviewObject.ConfigureTerrainContact(Vector3Int blockSize)`（シグネチャ不変・形状のみ変更）。`internal bool DetectCurrentGroundOverlap()`（不変）

- [ ] **Step 1: `ConfigureTerrainContact` を書き換える**

`BlockPreviewObject.cs` の95〜119行を次に置き換える。

```csharp
        internal void ConfigureTerrainContact(Vector3Int blockSize)
        {
            if (_terrainContactConfigured) return;
            _terrainContactConfigured = true;
            CacheContactColliders();
            if (_collisionDetectors.Length != 0) return;

            // 最下段セルはADR 0047の床で沈む許容範囲なので、高さ1は判定箱を持たない
            // The base cell is ADR 0047's allowed floor sink, so height-1 blocks get no volume.
            // silent-ok: 高さ1は地形ゲートを持たない仕様(ADR 0080) / 崖に潜った配置はプレイヤーが見た目で気づく
            if (blockSize.y <= 1) return;

            // 見た目の張り出しを避け、マスター占有の上部セルだけで地形接触を測る
            // Measure terrain only within the upper occupied cells, excluding visual overhang.
            var contact = new GameObject("PlacementGroundContactVolume");
            _ownedTerrainContact = contact;
            contact.layer = LayerConst.BlockLayer;
            contact.transform.SetParent(transform, false);
            var body = contact.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var upperHeight = blockSize.y - 1f;
            var inset = Physics.defaultContactOffset * 2f;
            var box = contact.AddComponent<BoxCollider>();
            box.center = new Vector3(blockSize.x * 0.5f, 1f + upperHeight * 0.5f, blockSize.z * 0.5f);
            box.size = new Vector3(blockSize.x, upperHeight, blockSize.z) - Vector3.one * (inset * 2f);
            box.isTrigger = true;
            contact.AddComponent<GroundCollisionDetector>();
            CacheContactColliders();
        }
```

- [ ] **Step 2: 既存の箱形状テスト2件を新しい形状へ合わせる**

`PreviewBlockColliderIsolationTest.cs` の `MissingDetectorGetsOneInsetContactVolumeWithoutChangingVisualCollider` で、`ConfigureTerrainContact(new Vector3Int(1, 1, 1))` の2行を `ConfigureTerrainContact(new Vector3Int(1, 3, 1))` にし、y の検査2行を次に置き換える。

```csharp
            Assert.AreEqual(1f + inset, box.bounds.min.y, 0.0001f);
            Assert.AreEqual(3f - inset, box.bounds.max.y, 0.0001f);
```

同ファイル末尾の `InsetUsesTwoFacingContactMarginsAtShallowBoundary` を、テスト名ごと次に置き換える（最下段セル上面 y=1 が新しい境界）。

```csharp
        [Test]
        public void InsetUsesTwoFacingContactMarginsAtBaseCellTop()
        {
            var root = new GameObject("Preview");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var preview = root.AddComponent<BlockPreviewObject>();
            preview.SetTerrainContactEnabled(true, new Vector3Int(1, 2, 1));
            var contact = root.GetComponentsInChildren<GroundCollisionDetector>(true).Single().GetComponent<BoxCollider>();
            var inset = Physics.defaultContactOffset * 2f;
            var ground = new GameObject("BaseCellGround");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            ground.layer = LayerConst.GroundLayer;
            ground.AddComponent<GroundGameObject>();
            var groundBox = ground.AddComponent<BoxCollider>();
            groundBox.size = new Vector3(2f, 1f, 2f);
            ground.transform.position = new Vector3(0.5f, 0.5f, 0.5f);
            Physics.SyncTransforms();
            Assert.AreEqual(1f + inset, contact.bounds.min.y, 0.0001f);
            Assert.IsFalse(preview.DetectCurrentGroundOverlap(), "ground filling the base cell must clear the volume");
            ground.transform.position += Vector3.up * inset * 0.5f;
            Physics.SyncTransforms();
            Assert.IsFalse(preview.DetectCurrentGroundOverlap(), "a bump below the inset stays clear");
            ground.transform.position += Vector3.up * inset;
            Physics.SyncTransforms();
            Assert.IsTrue(preview.DetectCurrentGroundOverlap(), "a bump beyond the inset enters the upper cells");
        }
```

`ExistingDetectorRemainsTheOnlyContactSource`（既存検出器あり）は変えない。

- [ ] **Step 3: PlayModeフィクスチャを高さ3のブロックへ切り替える**

`PlacementPreviewProductionWiringTest` は生成判定箱が1個あることを前提にしている。高さ1の `ForUnitTestModBlockId.BlockId` では箱ができなくなるので、`PlacementPreviewPhysicsFixture.cs` の44行目と61行目の `var blockId = ForUnitTestModBlockId.BlockId;` を両方とも次に置き換える。

```csharp
            // 判定箱が生成される高さ2以上の電柱(1x3x1)を使う
            // Use the 1x3x1 electric pole, tall enough to get a contact volume.
            var blockId = ForUnitTestModBlockId.ElectricPoleId;
```

`PlacementPreviewProductionWiringTest.cs` 本体は変えない（地面は `contact.bounds.center` に置くので箱の高さが変わっても成立する）。

- [ ] **Step 4: 回帰テストを新設する**

`PlacementTerrainContactBaseCellSinkTest.cs` を作る。

```csharp
#if UNITY_EDITOR
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaceSystem.Common.Preview
{
    // 床で沈む最下段セルは埋まり判定にせず、上部セルへの食い込みだけを判定する(ADR 0080)
    // Ignore the floor-sunk base cell and flag only intrusion into upper cells (ADR 0080).
    public class PlacementTerrainContactBaseCellSinkTest
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            Assert.AreNotEqual(Physics.defaultPhysicsScene, _scene.GetPhysicsScene());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(_scene);
        }

        // 歯車チェーンポール3x4x3・コンパクト1x2x1・レール橋脚3x1x3・ベルト1x1x1
        // Gear chain pole 3x4x3, compact pole 1x2x1, rail pier 3x1x3, belt 1x1x1.
        [TestCase(3, 4, 3, 0.3f)]
        [TestCase(3, 4, 3, 0.7f)]
        [TestCase(1, 2, 1, 0.3f)]
        [TestCase(1, 2, 1, 0.7f)]
        [TestCase(3, 1, 3, 0.3f)]
        [TestCase(3, 1, 3, 0.7f)]
        [TestCase(1, 1, 1, 0.3f)]
        [TestCase(1, 1, 1, 0.7f)]
        public void FractionalFlatGroundInsideBaseCellIsNotBuried(int x, int y, int z, float surfaceHeight)
        {
            var size = new Vector3Int(x, y, z);
            var preview = CreatePreview(size);
            CreateSlopedGround(size, surfaceHeight, 0f);
            Physics.SyncTransforms();
            Assert.IsFalse(preview.DetectCurrentGroundOverlap());
        }

        [TestCase(3, 4, 3)]
        [TestCase(1, 2, 1)]
        [TestCase(3, 1, 3)]
        [TestCase(1, 1, 1)]
        public void GroundRisingHalfMeterAcrossThreeCellsIsNotBuried(int x, int y, int z)
        {
            // 3セルで0.5m上がる斜面を、フットプリント中央の地表が0.45mになるよう置く
            // Place a 0.5m-per-3-cells slope whose surface is 0.45m at the footprint center.
            var size = new Vector3Int(x, y, z);
            var preview = CreatePreview(size);
            CreateSlopedGround(size, 0.45f, Mathf.Atan2(0.5f, 3f) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
            Assert.IsFalse(preview.DetectCurrentGroundOverlap());
        }

        [TestCase(3, 4, 3, 2.0f)]
        [TestCase(1, 2, 1, 1.5f)]
        public void CliffIntoUpperCellsIsBuried(int x, int y, int z, float cliffTop)
        {
            var size = new Vector3Int(x, y, z);
            var preview = CreatePreview(size);
            CreateCliff(size, cliffTop);
            Physics.SyncTransforms();
            Assert.IsTrue(preview.DetectCurrentGroundOverlap());
        }

        [TestCase(3, 1, 3)]
        [TestCase(1, 1, 1)]
        public void HeightOneBlockHasNoContactVolumeEvenInsideCliff(int x, int y, int z)
        {
            // 高さ1は#1499前と同じく地形ゲートを持たない
            // Height-1 blocks have no terrain gate, matching pre-#1499 behavior.
            var size = new Vector3Int(x, y, z);
            var preview = CreatePreview(size);
            CreateCliff(size, 1.5f);
            Physics.SyncTransforms();
            Assert.IsEmpty(preview.GetComponentsInChildren<GroundCollisionDetector>(true));
            Assert.IsFalse(preview.DetectCurrentGroundOverlap());
        }

        private BlockPreviewObject CreatePreview(Vector3Int size)
        {
            var root = new GameObject("Preview");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var preview = root.AddComponent<BlockPreviewObject>();
            preview.SetTerrainContactEnabled(true, size);
            return preview;
        }

        private void CreateSlopedGround(Vector3Int size, float surfaceHeightAtCenter, float slopeDegrees)
        {
            // 厚い箱をX方向へ傾け、上面がフットプリント中央を指定高さで通るよう置く
            // Tilt a thick box along X so its top passes the footprint center at the given height.
            var rotation = Quaternion.Euler(0f, 0f, slopeDegrees);
            var surfaceCenter = new Vector3(size.x * 0.5f, surfaceHeightAtCenter, size.z * 0.5f);
            var box = CreateGroundBox(new Vector3(size.x + 10f, 10f, size.z + 10f));
            box.transform.SetPositionAndRotation(surfaceCenter - rotation * new Vector3(0f, 5f, 0f), rotation);
        }

        private void CreateCliff(Vector3Int size, float cliffTop)
        {
            // フットプリントの+X端1列だけを、上面cliffTop・下面-5で覆う
            // Cover only the +X edge column of the footprint, from -5 up to cliffTop.
            var box = CreateGroundBox(new Vector3(1f, cliffTop + 5f, size.z + 2f));
            box.transform.position = new Vector3(size.x - 0.5f, (cliffTop - 5f) * 0.5f, size.z * 0.5f);
        }

        private BoxCollider CreateGroundBox(Vector3 boxSize)
        {
            var ground = new GameObject("Ground");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            ground.layer = LayerConst.GroundLayer;
            ground.AddComponent<GroundGameObject>();
            var box = ground.AddComponent<BoxCollider>();
            box.size = boxSize;
            return box;
        }
    }
}
#endif
```

- [ ] **Step 5: コンパイルし、接触判定まわりのテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.PlaceSystem.Common.Preview\.(PlacementTerrainContactBaseCellSinkTest|PreviewBlockColliderIsolationTest|PlacementTerrainContactShapeTest|PlacementGroundContactQueryCapacityTest)"`
Expected: 全件 PASS（新設16ケースを含む）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.PlaceSystem.Common.Preview.PlacementPreviewProductionWiringTest" --timeout-seconds 900`
Expected: 4件 PASS（PlayMode遷移を伴うので、終了後に「Domain Reload in progress」が出たら45秒待って再試行）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.PlaceSystem\.(TrainCostIntegration|Common\.NormalPlacementPreviewStepTest|Common\.PlacementHeightPlaceSystemWiringTest)|GearChainPole|ElectricWire.*Ghost|BeltConveyorPlace"`
Expected: 全件 PASS（4設置系の配線テストが回帰していない）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs \
  moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview/ \
  docs/adr/0080-terrain-contact-gate-ignores-base-cell-sink.md \
  docs/superpowers/plans/2026-10-15-placement-regression-fix.md \
  ".decisions/2026-10-15-地形ゲート付き設置の判定箱は最下段セルを除き撤去返却はMaxStackで分ける.md"
git commit -m "fix(place): 地形判定箱を最下段セルの上へ持ち上げ高さ1では作らない"
```

（新設テストの `.meta` は Unity が生成したものをそのまま含める）

---

### Task 2: 4設置系の設置Yをフットプリント最高点で決める（クライアント）

照準1点の `floor(hit.y)` だと、3x3 のポールは緩い斜面でも上り側の地表が判定箱へ入り、照準位置によって赤くなる。通常設置と同じく、占有セルの地形最高点を含むセルを Y にする（ADR 0047・0080、裁定 `.decisions/2026-10-15-地形ゲート付き4設置系の設置Yもフットプリント最高点で決める.md`）。

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/FootprintGroundedCellResult.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs`（`TryGetRayHitBlockPosition` の直後に3メソッド追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail/TrainRailPlaceService.cs:41`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:128`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleGhostPart.cs:54`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Modes/GearChainPoleGhostState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Modes/GearChainPolePlaceExtendInput.cs:22-28`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Modes/GearChainPoleFrameInputCollector.cs:76-83`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Modes/GearChainPolePlaceExtendMode.cs:29-35`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPoleDecideTestSupport.cs:66`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPolePlaceExtendModeFeedbackTest.cs:145-146,161,176`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/GearChainPoleConnect/GearChainPoleGhostStateFeedbackTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/FootprintGroundedCellResolveTest.cs`

**Interfaces:**
- Consumes: 既存 `bool PlacementGroundCellResolver.TryResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset, out Vector3Int resolvedPosition)`、既存 `enum PlacementHitSurfaceKind { Ground, BlockFace }`（`Common/Run`）、既存 `PlacementFeedback.AddGroundNotFound()` / `PlacementFeedback.GroundNotFoundLine()` / `PlacementFeedback.TooFarLine()`
- Produces:
  - `public enum FootprintGroundedCellResult { NoPlacementSurface, GroundNotFound, Resolved }`（namespace `Client.Game.InGame.BlockSystem.PlaceSystem.Ground`）
  - `public static FootprintGroundedCellResult PlaceSystemUtil.ResolveFootprintGroundedRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)` — 理由行を自分で出せない呼び出し側（歯車チェーンポールの入力収集）用
  - `public static bool PlaceSystemUtil.TryGetFootprintGroundedRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, PlacementFeedback feedback, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)` — 地表なしなら `feedback.AddGroundNotFound()` を積んで false。照準面なしも false（理由行なし）。橋脚・ベルト・電柱ゴーストが使う
  - `internal static FootprintGroundedCellResult PlaceSystemUtil.ResolveFootprintGroundedCell(Vector3Int rayHitCell, PlacementHitSurfaceKind surfaceKind, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset, out Vector3Int pos)`
  - `public enum GearChainPoleGhostState { NoAimSurface, TooFar, GroundNotFound, Shown }`（namespace `Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes`）。`GearChainPolePlaceExtendInput` の `HasGhost`（bool）と `GhostTooFar`（bool）を `public GearChainPoleGhostState GhostState;` 1つに置き換える。構造体の既定値は `NoAimSurface`（先頭メンバー）

- [ ] **Step 1: 結果の型を作る**

`Ground/FootprintGroundedCellResult.cs`:

```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形ゲート付き設置系のカーソルセル解決の結果。照準面なし・地表なし・解決済みを区別する
    ///     Result of the terrain-gated placements' cursor cell resolution; separates no aim surface, no ground and resolved
    /// </summary>
    public enum FootprintGroundedCellResult
    {
        NoPlacementSurface,
        GroundNotFound,
        Resolved,
    }
}
```

- [ ] **Step 2: `PlaceSystemUtil` に1セル版の解決を足す**

`PlaceSystemUtil.cs` の `TryGetRayHitBlockPosition`（30〜34行）の直後に追加する。using に `Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run` と `Client.Game.InGame.BlockSystem.PlaceSystem.Feedback` を足す（`Ground` は既にある）。追加後も200行未満（約185行）。

```csharp
        // 地形ゲート付き設置系の1セル版。地面ヒットのYを占有セルの地形最高点で決め直す（ADR 0047・0080）
        // Single-cell entry for terrain-gated placements; a ground hit's Y is re-decided from the footprint terrain max (ADR 0047, 0080)
        public static FootprintGroundedCellResult ResolveFootprintGroundedRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            if (!TryGetRayHitBlockPosition(mainCamera, heightOffset, currentBlockDirection, holdingBlock, out var rayHitCell, out surface))
            {
                pos = rayHitCell;
                return FootprintGroundedCellResult.NoPlacementSurface;
            }

            var surfaceKind = surface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace;
            return ResolveFootprintGroundedCell(rayHitCell, surfaceKind, currentBlockDirection, holdingBlock.BlockSize, heightOffset, out pos);
        }

        // 理由行を積める呼び出し側向け。地表なしは理由を積んで不可、照準面なしは理由なしで不可
        // For callers holding feedback; no ground pushes its reason, no aim surface fails without a line
        public static bool TryGetFootprintGroundedRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, PlacementFeedback feedback, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            var result = ResolveFootprintGroundedRayHitBlockPosition(mainCamera, heightOffset, currentBlockDirection, holdingBlock, out pos, out surface);
            if (result == FootprintGroundedCellResult.GroundNotFound) feedback.AddGroundNotFound();
            // silent-ok: 照準が地面にもブロックにも当たらないのは操作中の通常状態 / プレビューが出ないことでプレイヤーが気づく
            return result == FootprintGroundedCellResult.Resolved;
        }

        internal static FootprintGroundedCellResult ResolveFootprintGroundedCell(Vector3Int rayHitCell, PlacementHitSurfaceKind surfaceKind, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset, out Vector3Int pos)
        {
            pos = rayHitCell;

            // ブロック面ヒット（積み重ね）は整数グリッド上なので触らない
            // Block-face hits (stacking) sit on the integer grid and stay untouched
            if (surfaceKind == PlacementHitSurfaceKind.BlockFace) return FootprintGroundedCellResult.Resolved;

            // 占有セルの地表が取れなければ置けないので、その理由を呼び出し側へ返す
            // Without ground under the footprint the block cannot be placed, so that reason goes back to the caller
            if (!PlacementGroundCellResolver.TryResolveCellFromGround(rayHitCell, blockDirection, blockSize, heightOffset, out pos)) return FootprintGroundedCellResult.GroundNotFound;
            return FootprintGroundedCellResult.Resolved;
        }
```

- [ ] **Step 3: レール橋脚・ベルト・電柱ゴーストを切り替える**

3か所とも1行の置き換えで、行数は増えない（`BeltConveyorPlaceSystem.cs` は196行のまま）。

`TrainRailPlaceService.cs:41`（接続モードで地面をクリックして橋脚を立てる経路 `TrainRailConnectSystem` → `ManualUpdate` もここを通る）:

```csharp
            if (!PlaceSystemUtil.TryGetFootprintGroundedRayHitBlockPosition(_mainCamera, HeightOffset, DefaultBlockDirection, holdingBlockMaster, feedback, out var placePoint, out _)) return null;
```

`BeltConveyorPlaceSystem.cs:128`（このファイルは `PlaceSystemUtil` を using static しているので接頭辞なし）:

```csharp
            if (!TryGetFootprintGroundedRayHitBlockPosition(_mainCamera, _dragState.HeightOffset, _currentBlockDirection, holdingBlockMaster, feedback, out var placePoint, out var hitSurface)) { return false; }
```

`ElectricWirePoleGhostPart.cs:54`:

```csharp
            if (!PlaceSystemUtil.TryGetFootprintGroundedRayHitBlockPosition(_mainCamera, 0, selection.CurrentDirection, poleMaster, feedback, out var placePoint, out _)) return false;
```

- [ ] **Step 4: 歯車チェーンポールのゴースト状態を1つの型にする**

`GearChainPoleConnect/Modes/GearChainPoleGhostState.cs`:

```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes
{
    /// <summary>
    ///     ゴーストを出したか、出さなかった理由。既定値は照準面なし
    ///     Whether a ghost is shown, or why not; the default is no aim surface
    /// </summary>
    public enum GearChainPoleGhostState
    {
        NoAimSurface,
        TooFar,
        GroundNotFound,
        Shown,
    }
}
```

`GearChainPolePlaceExtendInput.cs` の22〜28行（`HasGhost`・`GhostTooFar` とそれぞれのコメント2行）を次に置き換える。

```csharp
        // ゴーストを出したか、出さなかった理由（照準面なし・距離超過・地表なし）
        // Whether a ghost is shown, or why not (no aim surface, beyond reach, no footprint ground)
        public GearChainPoleGhostState GhostState;
```

`GearChainPoleFrameInputCollector.cs` の76〜83行（レイ取得・距離判定・`input.HasGhost = true;`）を次に置き換える（using に `Client.Game.InGame.BlockSystem.PlaceSystem.Ground` を足す）。

```csharp
            var ghostCell = PlaceSystemUtil.ResolveFootprintGroundedRayHitBlockPosition(_mainCamera, 0, BlockDirection.North, poleBlockMaster, out var placePos, out _);
            // silent-ok: 照準面なしは既定値NoAimSurfaceのまま返す / ゴーストが出ないことでプレイヤーが気づく
            if (ghostCell == FootprintGroundedCellResult.NoPlacementSurface) return input;
            if (ghostCell == FootprintGroundedCellResult.GroundNotFound)
            {
                input.GhostState = GearChainPoleGhostState.GroundNotFound;
                return input;
            }
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(placePos))
            {
                input.GhostState = GearChainPoleGhostState.TooFar;
                return input;
            }

            input.GhostState = GearChainPoleGhostState.Shown;
```

`GearChainPolePlaceExtendMode.cs` の29〜35行（コメント2行と `if (!input.HasGhost) { var noGhostLines = …; return …; }`）を次に置き換える。`return` 行は元のまま。

```csharp
            // 位置なしは非表示。距離外と地表なしだけ理由を出す
            // Shows nothing without a position; reports a reason only when out of range or without ground
            if (input.GhostState != GearChainPoleGhostState.Shown)
            {
                var noGhostLines = input.GhostState switch
                {
                    GearChainPoleGhostState.TooFar => new[] { PlacementFeedback.TooFarLine() },
                    GearChainPoleGhostState.GroundNotFound => new[] { PlacementFeedback.GroundNotFoundLine() },
                    // silent-ok: 照準面なしは理由行を出さない従来挙動 / ゴーストが出ないことでプレイヤーが気づく
                    _ => Array.Empty<TooltipLine>(),
                };
                return GearChainPoleFrameResult.Show(input.SourcePole, GearChainPolePreviewCommand.Hidden, noGhostLines, GearChainPoleExtendPreviewData.Invalid, Array.Empty<ConstructionMaterialShortage>());
            }
```

- [ ] **Step 5: 既存のモード判定テストを新しい型へ合わせ、地表なしのケースを足す**

- `GearChainPoleDecideTestSupport.cs:66` の `HasGhost = true,` → `GhostState = GearChainPoleGhostState.Shown,`
- `GearChainPolePlaceExtendModeFeedbackTest.cs:145-146` の `input.HasGhost = false;` と `input.GhostTooFar = true;` の2行 → `input.GhostState = GearChainPoleGhostState.TooFar;` の1行
- 同 `:161` の `input.HasGhost = false;` → `input.GhostState = GearChainPoleGhostState.NoAimSurface;`
- 同 `:176` の `input.GhostTooFar = true;` → `input.GhostState = GearChainPoleGhostState.TooFar;`

地表なしのケースは同ディレクトリの新規ファイル `GearChainPoleGhostStateFeedbackTest.cs` に置く（既存ファイルは183行のため）。

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    // ゴースト状態ごとの理由行を検証する
    // Verify the reason line for each ghost state
    public class GearChainPoleGhostStateFeedbackTest
    {
        [Test]
        public void GhostGroundNotFoundReportsGroundNotFoundLineTest()
        {
            // 地表が取れずゴーストが無いときは地表なしの行だけ返す
            // With no ghost because the footprint has no ground, only the ground-not-found line is returned
            var input = GearChainPoleDecideInputs.CreateGhostReadyInput(sourcePole: null);
            input.GhostState = GearChainPoleGhostState.GroundNotFound;

            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.IsFalse(result.Preview.GhostVisible);
            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGroundNotFound.Key, result.FeedbackLines[0].Key.Key);
        }
    }
}
```

- [ ] **Step 6: カーソルセル解決の単体テストを書く**

`Client.Tests/PlaceSystem/Ground/FootprintGroundedCellResolveTest.cs`。既存 `PlacementGroundCellApplyTest` と同じく既定物理シーンへ Ground レイヤーの箱を置く（他テストと重ならない座標を使う）。

```csharp
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground
{
    // 地形ゲート付き4設置系のカーソルセルが照準点でなく占有セルの地形最高点でYを決めることを検証する
    // Verify the terrain-gated placements' cursor cell takes Y from the footprint terrain max, not the aim point
    public class FootprintGroundedCellResolveTest
    {
        private static readonly Vector3Int PoleSize = new(3, 4, 3);
        private readonly List<GameObject> _slabs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var slab in _slabs) Object.DestroyImmediate(slab);
            _slabs.Clear();
        }

        [Test]
        public void GroundHitTakesFootprintMaxInsteadOfAimPoint()
        {
            // X方向に3セルで1m上がる斜面。占有範囲x=500〜503の地表は約20.3〜21.3
            // A slope rising 1m over 3 cells along X; ground over x=500..503 spans about 20.3..21.3
            CreateSlope(new Vector3(501.5f, 20.8f, 600.5f), 1f / 3f);
            var result = PlaceSystemUtil.ResolveFootprintGroundedCell(new Vector3Int(500, 20, 599), PlacementHitSurfaceKind.Ground, BlockDirection.North, PoleSize, 0, out var resolved);

            Assert.AreEqual(FootprintGroundedCellResult.Resolved, result);
            Assert.AreEqual(new Vector3Int(500, 21, 599), resolved);
        }

        [Test]
        public void HeightOffsetIsAddedAfterFootprintResolution()
        {
            CreateSlope(new Vector3(701.5f, 20.8f, 800.5f), 1f / 3f);
            var result = PlaceSystemUtil.ResolveFootprintGroundedCell(new Vector3Int(700, 21, 799), PlacementHitSurfaceKind.Ground, BlockDirection.North, PoleSize, 1, out var resolved);

            Assert.AreEqual(FootprintGroundedCellResult.Resolved, result);
            Assert.AreEqual(22, resolved.y);
        }

        [Test]
        public void BlockFaceHitKeepsTheRayCell()
        {
            // 積み重ねはレイのセルのまま。下に地面があっても動かさない
            // Stacking keeps the ray cell even with ground below
            CreateSlope(new Vector3(901.5f, 20.8f, 1000.5f), 1f / 3f);
            var rayCell = new Vector3Int(900, 25, 999);
            var result = PlaceSystemUtil.ResolveFootprintGroundedCell(rayCell, PlacementHitSurfaceKind.BlockFace, BlockDirection.North, PoleSize, 0, out var resolved);

            Assert.AreEqual(FootprintGroundedCellResult.Resolved, result);
            Assert.AreEqual(rayCell, resolved);
        }

        [Test]
        public void MissingFootprintGroundReportsGroundNotFound()
        {
            var result = PlaceSystemUtil.ResolveFootprintGroundedCell(new Vector3Int(4000, 7, 4000), PlacementHitSurfaceKind.Ground, BlockDirection.North, PoleSize, 0, out _);
            Assert.AreEqual(FootprintGroundedCellResult.GroundNotFound, result);
        }

        private void CreateSlope(Vector3 surfaceCenter, float rise)
        {
            // 厚さ2の箱をZ軸回りに傾け、上面がsurfaceCenterを通りX方向へ1mあたりriseで上がるよう置く
            // Tilt a 2-thick box about Z so its top passes surfaceCenter and rises by `rise` per meter along X
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan(rise) * Mathf.Rad2Deg);
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.layer = LayerConst.GroundLayer;
            slab.transform.localScale = new Vector3(20f, 2f, 20f);
            slab.transform.SetPositionAndRotation(surfaceCenter - rotation * Vector3.up, rotation);
            Physics.SyncTransforms();
            _slabs.Add(slab);
        }
    }
}
```

- [ ] **Step 7: コンパイルし、関連テストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.PlaceSystem\.(Ground\.|GearChainPoleConnect\.|TrainRail|TrainCostIntegration|BeltConveyor|ElectricWire)"`
Expected: 全件 PASS（新設5ケースを含む）。失敗したテストが照準1点の旧Yを前提にしていたら、期待値を「占有セルの地形最高点を含むセル」へ直し、その理由をコミットメッセージに書く

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ \
  moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ \
  ".decisions/2026-10-15-地形ゲート付き4設置系の設置Yもフットプリント最高点で決める.md"
git commit -m "fix(place): 地形ゲート付き4設置系の設置Yをフットプリント最高点で決める"
```

---

### Task 3: 接続線の素材返却を MaxStack ごとに分ける（サーバー）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ConnectionLine/ConnectionLineRefundItems.cs:14-26`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/GearChainEditTestWorld.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/ConnectionLineRefundItemsTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/GearChainRefundAboveMaxStackTest.cs`

**Interfaces:**
- Consumes: 既存 `List<IItemStack> IItemStackFactory.CreateSplitStacks(ItemId id, int totalCount)`（空アイテム・1未満は空リストを返す）
- Produces: `public static List<IItemStack> ConnectionLineRefundItems.Create(IReadOnlyList<ConnectToolMaterialCost> materials)`（シグネチャ不変。各スタックの Count が MaxStack 以下になる）。テスト補助 `internal RemoveBlockProtocol.RemoveBlockResponseMessagePack GearChainEditTestWorld.SendRemoveBlock(Vector3Int position)`

- [ ] **Step 1: `ConnectionLineRefundItems.Create` を分割版にする**

14〜26行の `Create` を次に置き換える。

```csharp
        public static List<IItemStack> Create(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            var result = new List<IItemStack>();
            if (materials == null) return result;
            foreach (var material in materials)
            {
                // 長い線は素材数がMaxStackを超えるので、スタック上限ごとに分けて返す
                // Long lines can exceed MaxStack, so split each material into max-size stacks
                // silent-ok: 数0と空アイテムは返却物が無いだけ / CreateSplitStacksが空リストを返し総数テストで固定
                result.AddRange(ServerContext.ItemStackFactory.CreateSplitStacks(material.ItemId, material.Count));
            }
            return result;
        }
```

using はそのまま（`Core.Master` は `ConnectToolMaterialCost` に使う。`ItemMaster.EmptyItemId` を使わなくなるが名前空間は残る）。

- [ ] **Step 2: 返却部品の単体テストを書く**

`ConnectionLineRefundItemsTest.cs` を作る。テストmodの Test4(`…1234-000000000004`) は MaxStack 100（レベル1）、Test2(`…1234-000000000002`) は 50。

```csharp
using System;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.ConnectionLine;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.Chain
{
    // 接続線の返却がMaxStackで分割され総数を保つことを検証する
    // Verify connection-line refunds split by MaxStack and preserve the total count
    public class ConnectionLineRefundItemsTest
    {
        private static readonly Guid Stack100ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");
        private static readonly Guid Stack50ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TestCase(230, new[] { 100, 100, 30 })]
        [TestCase(100, new[] { 100 })]
        [TestCase(99, new[] { 99 })]
        public void MaterialAboveMaxStackSplitsIntoMaxSizedStacks(int count, int[] expectedCounts)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(Stack100ItemGuid);
            Assert.AreEqual(100, ItemStackLevelDataStore.Instance.GetMaxStack(itemId));
            var stacks = ConnectionLineRefundItems.Create(new[] { new ConnectToolMaterialCost(itemId, count) });

            Assert.IsTrue(stacks.All(stack => stack.Id == itemId));
            CollectionAssert.AreEqual(expectedCounts, stacks.Select(stack => stack.Count).ToArray());
        }

        [Test]
        public void EachMaterialSplitsByItsOwnMaxStackAndSkipsEmptyEntries()
        {
            var stack100 = MasterHolder.ItemMaster.GetItemId(Stack100ItemGuid);
            var stack50 = MasterHolder.ItemMaster.GetItemId(Stack50ItemGuid);
            var materials = new[]
            {
                new ConnectToolMaterialCost(stack100, 230),
                new ConnectToolMaterialCost(stack50, 120),
                new ConnectToolMaterialCost(stack50, 0),
                new ConnectToolMaterialCost(ItemMaster.EmptyItemId, 5),
            };
            var stacks = ConnectionLineRefundItems.Create(materials);

            // 素材ごとに自身のMaxStackで分割され、総数が保存される
            // Each material splits by its own MaxStack and keeps its total
            CollectionAssert.AreEqual(new[] { 100, 100, 30 }, stacks.Where(s => s.Id == stack100).Select(s => s.Count).ToArray());
            CollectionAssert.AreEqual(new[] { 50, 50, 20 }, stacks.Where(s => s.Id == stack50).Select(s => s.Count).ToArray());
            Assert.AreEqual(6, stacks.Count);
        }
    }
}
```

- [ ] **Step 3: テストワールドに撤去送信を足す**

`GearChainEditTestWorld.cs` の `Send` メソッドの直後に追加する（using に `static Server.Protocol.PacketResponse.RemoveBlockProtocol` を足す）。

```csharp
        internal RemoveBlockResponseMessagePack SendRemoveBlock(Vector3Int position)
        {
            // 撤去も本物のプロトコル経路で要求者へ紐付けて送る
            // Send removal through the real protocol bound to the requester as well
            var payload = MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(position));
            var bytes = _packet.GetPacketResponse(payload, BoundPacketContext.Bind(_playerId)).Single();
            return MessagePackSerializer.Deserialize<RemoveBlockResponseMessagePack>(bytes.ToArray());
        }
```

- [ ] **Step 4: 撤去・切断の回帰テストを書く**

`GearChainRefundAboveMaxStackTest.cs` を作る。チェーン素材 Test4 は MaxStack 100、230個は [100,100,30] の3スタックになる。ポール自体の建設コストは Test3 なので、チェーン素材の数には混ざらない。

```csharp
using System;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using static Server.Protocol.PacketResponse.GearChainConnectionEditProtocol;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;
using static Tests.CombinedTest.Server.PacketTest.GearChain.GearChainEditTestWorld;

namespace Tests.CombinedTest.Server.PacketTest.GearChain
{
    // MaxStackを超える素材記録の撤去・切断が例外なく総数を返すことを検証する
    // Verify removal and disconnect of above-MaxStack records refund the full total without throwing
    public class GearChainRefundAboveMaxStackTest
    {
        private const int Remainder = 30;
        private static readonly Guid PoleCostItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        [Test]
        public void RemovingPoleRefundsAboveMaxStackChainAsSplitStacks()
        {
            var world = new GearChainEditTestWorld(true);
            var total = ConnectLongChain(world);
            var response = world.SendRemoveBlock(PosA);

            Assert.IsTrue(response.Success, response.FailureReason.ToString());
            Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(PosA));
            Assert.AreEqual(total, world.CountItem(world.ChainItemId));
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(world.ChainItemId);
            CollectionAssert.AreEqual(new[] { maxStack, maxStack, Remainder }, ChainStackCounts(world));
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        public void RemovalInsertionCheckCountsEverySplitStack(int freeSlots, bool expectSuccess)
        {
            var world = new GearChainEditTestWorld(true);
            var total = ConnectLongChain(world);
            // 分割後3スタックに対し空き枠の数だけを変える
            // Vary only the number of free slots against the three split stacks
            world.FillInventory();
            for (var slot = 0; slot < freeSlots; slot++) world.Inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
            // ポール本体の建設コスト返却(Test3)は既存の端数スタックへ合流させ、空き枠をチェーン専用にする
            // Let the pole's own cost refund (Test3) merge into a partial stack so free slots count only chain stacks
            var poleCostItemId = MasterHolder.ItemMaster.GetItemId(PoleCostItemGuid);
            world.Inventory.SetItem(freeSlots, ServerContext.ItemStackFactory.Create(poleCostItemId, 1));
            var before = world.Inventory.InventoryItems.Select(stack => (stack.Id, stack.Count)).ToArray();
            var response = world.SendRemoveBlock(PosA);

            Assert.AreEqual(expectSuccess, response.Success);
            if (expectSuccess)
            {
                Assert.AreEqual(total, world.CountItem(world.ChainItemId));
                return;
            }
            Assert.AreEqual(RemoveBlockFailureReason.InventoryFull, response.FailureReason);
            Assert.IsNotNull(ServerContext.WorldBlockDatastore.GetBlock(PosA));
            Assert.IsTrue(Pole(PosA).ContainsChainConnection(Pole(PosB).BlockInstanceId));
            CollectionAssert.AreEqual(before, world.Inventory.InventoryItems.Select(stack => (stack.Id, stack.Count)).ToArray());
        }

        [Test]
        public void DisconnectRefundsAboveMaxStackChainAsSplitStacks()
        {
            var world = new GearChainEditTestWorld(true);
            var total = ConnectLongChain(world);
            var response = world.Send(GearChainConnectionEditRequest.CreateDisconnectRequest(PosA, PosB));

            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.AreEqual(total, world.CountItem(world.ChainItemId));
            Assert.AreEqual(3, ChainStackCounts(world).Length);
        }

        private static int ConnectLongChain(GearChainEditTestWorld world)
        {
            // 長い線を想定し、MaxStackの2倍＋端数の支払い記録を両端へ置く
            // Record a long-line payment of twice MaxStack plus a remainder on both ends
            var total = ItemStackLevelDataStore.Instance.GetMaxStack(world.ChainItemId) * 2 + Remainder;
            var record = new ConnectionLineRecord(ChainToolGuid, new[] { new ConnectToolMaterialCost(world.ChainItemId, total) });
            Assert.IsTrue(Pole(PosA).TryAddChainConnection(Pole(PosB).BlockInstanceId, record));
            Assert.IsTrue(Pole(PosB).TryAddChainConnection(Pole(PosA).BlockInstanceId, record));
            return total;
        }

        private static int[] ChainStackCounts(GearChainEditTestWorld world)
        {
            return world.Inventory.InventoryItems.Where(stack => stack.Id == world.ChainItemId)
                .Select(stack => stack.Count).OrderByDescending(count => count).ToArray();
        }
    }
}
```

注: `ConnectionLineRecord` は `Game.Block.Interface.Component`、`ConnectToolMaterialCost` は `Core.Master`。撤去ではチェーン素材に加えてポール本体の建設コスト（テストmodの GearChainPole は Test3×2、MaxStack 300）も返る場合があるため、境界テストでは Test3 の端数スタックを1枠置いてそちらへ合流させ、空き枠の数がチェーンの3スタックだけで決まるようにしている。

- [ ] **Step 5: コンパイルし、返却まわりのテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConnectionLineRefundItemsTest|GearChainRefundAboveMaxStackTest|GearChainDisconnectProtocolTest|ElectricWireRemovalTest|ElectricWireDisconnect|RemoveRailBlockRefundTest|RemoveRailRefundFailureTest|RailConnectionEdit"`
Expected: 全件 PASS（新設8ケースと、同部品を使う電線・レールの既存テスト）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/ConnectionLine/ConnectionLineRefundItems.cs \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Game/Chain/ \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GearChain/
git commit -m "fix(refund): 接続線の素材返却をMaxStackごとの複数スタックに分ける"
```

---

### Task 4: unityプレイ録画テストで実地形の設置を確かめる

**Files:**
- Create: `unity-playmode-recorded-playtest` スキルが指定する場所のシナリオ（スキルの規定に従う。本repoのソースは変えない）

**Interfaces:**
- Consumes: Task 1 の判定箱と Task 2 のY決定（実ゲームの4設置系経由）
- Produces: 録画と判定ログ（PR本文に添付する）

- [ ] **Step 1: シナリオを用意して実行する**

`unity-playmode-recorded-playtest` スキルの手順に従い、生成ワールドで次を通しで録画する。

1. 地表の高さが整数でない平地（`hitY - floor(hitY)` が 0.1〜0.9 の点。ログかデバッグ表示で確かめる）を照準し、歯車チェーンポール・コンパクト歯車チェーンポール・レール橋脚・ベルトをそれぞれ持って、ゴーストが通常色で「地形に埋まっています」が出ないことを確かめ、実際に設置する
2. 崖の斜面にポール上部が1セルを超えて刺さる位置で歯車チェーンポールを持ち、ゴーストが不可色になり「地形に埋まっています」が出ることを確かめる
3. 電線接続ツールで電柱ゴーストを端数のある平地に出し、不可にならないことを確かめる
4. 1m/3セル程度の斜面で歯車チェーンポールを持ち、照準を斜面の上り側・下り側へ動かしても同じセル高さで通常色のままであることを確かめる

Expected: 1・3・4 は設置可、2 は設置不可。`uloop get-logs --project-path ./moorestech_client --log-type Error` が空

- [ ] **Step 2: 結果を記録してコミットする**

録画・判定ログの置き場はスキルの規定に従う（コードrepoへ記録ファイルをコミットしない）。シナリオファイルを repo に追加した場合だけコミットする。

```bash
git add <スキルが作ったシナリオファイル>
git commit -m "test(playtest): 端数のある地形でポール・橋脚・ベルトが置けることを録画で確かめる"
```

---

### Task 5: 全ブランチレビュー

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）
- [ ] レビュー指摘の反映が判定経路・条件式・その評価時点に触れたら、Task 4 のunityプレイ録画テストを反映後のバイナリで再実施してから完了とする

### Task 6: PR作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-create スキルでPRを作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する（解消の実作業は pr-create 経由で opus subagent に委譲される）。全作業がコミット・push 済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。全実装タスク完了＝完了ではない。PR未作成のまま終わるのはplan未完了である
- [ ] PR本文に「本PR外のリファクタ提案」節を置き、下記の第3バケツを転記する

---

## 本PR外のリファクタ提案

- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolver.cs:30-40` の `TryResolveFootprintOrigin` は、Task 2 の `PlaceSystemUtil.ResolveFootprintGroundedCell`（向き `North` 固定版）と同じ判定（面ヒットは触らず、地面ヒットは占有最高点）を持ち、同ファイル `TryResolveCursorOrigin`（16-27行。25行目の `surface == null ? Ground : BlockFace`）も `ResolveFootprintGroundedRayHitBlockPosition` と同じ変換を持つ。BP側を新関数の呼び出しへ寄せられる（同役割＝カーソルセルのY決定）

- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/ConstructionCostService.cs:36-49` の `CreateRefundItems` も `ItemStackFactory.Create(itemId, count)` を MaxStack で分けずに呼ぶ。建設コストの定義数が MaxStack を超えるブロック・列車を撤去すると同じ例外になる。`ConnectionLineRefundItems` と同じく `CreateSplitStacks` へ寄せられる（同役割＝返却スタック生成）
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeRefundUtil.cs:22-29` の `CreateRefundStacks` も同形（`ItemStackFactory.Create(itemId, input.Count)` を分割しない）。レシピ入力数が MaxStack を超えると同じ例外になる（同役割＝返却スタック生成）

## 判断記録（ADR）

- 設計ADR: `docs/adr/0080-terrain-contact-gate-ignores-base-cell-sink.md`（判定箱の最下段セル除外・高さ1は箱なし）。関連: `docs/adr/0047-placement-default-height-floor-of-terrain-max.md`
- ユーザー裁定: `.decisions/2026-10-15-地形ゲート付き設置の判定箱は最下段セルを除き撤去返却はMaxStackで分ける.md`（①判定箱の下端を1セル持ち上げる ②素材をMaxStackごとに分けて返す。原文「ok、これの修正計画を立てて。」）
- ユーザー裁定: `.decisions/2026-10-15-地形ゲート付き4設置系の設置Yもフットプリント最高点で決める.md`（シミュレーターreviewが「4設置系のYは照準1点floorで、裁定①だけでは斜面で照準位置により赤くなる」を検出→AskUserQuestion で選択「Yを最高点に揃えて同PRで直す」。棄却: planのまま後続issue、ゲート撤去）
- スコープ外の裁定: 旧レールが走れない件（#1429 列車同期の永久待機説）はプレイヤーのログ待ち。地形ゲート撤去案は採らない（同上の裁定）
- planning判断: 分割は既存 `IItemStackFactory.CreateSplitStacks` を使い、新しい分割関数を作らない（前例 `VeinHandMiningService.cs:108` ほか）
- planning判断: 修正点を共通部品 `ConnectionLineRefundItems` 1か所に置き、6経路（電線撤去・電線切断・歯車チェーン撤去・歯車チェーン切断・レール撤去返却・レール切断）をまとめて直す。経路ごとの分割はしない
- planning判断: 撤去の挿入チェック `RemoveBlockProtocol.TryInsertRefundItems` は変えない。`InsertionCheck` はインベントリのコピーへ全スタックを順に挿入して残りの有無を見る（`Core.Inventory/InventoryInsertItem.cs:26-39`）ので、複数スタックでも正しく働く。プレイヤーのメインインベントリは `AllowMultipleStacksPerItemOnInsert = true`（既定）。Task 3 のテストで固定する
- planning判断: PlayModeフィクスチャのブロックを `ElectricPoleId`(1x3x1) に変える。高さ1のテストブロックでは判定箱ができず、生成判定箱の配線テストが成り立たなくなるため
- planning判断: unityプレイ録画テストを含める（Task 4）。地形の端数・斜面の分布はユニットテストの箱地面では再現しきれず、4設置系の実配線と文言表示も通しで確かめる必要があるため
- planning判断: Task 2 のカーソルセル解決は3状態 enum `FootprintGroundedCellResult` で返す。照準面なし（従来どおり無言で非表示）と占有セルの地表なし（理由行を出す）を bool に畳まないため。理由行を積める3系統（橋脚・ベルト・電柱ゴースト）には、地表なしの理由行を内包する bool 版 `TryGetFootprintGroundedRayHitBlockPosition` を用意し、同じ3段の分岐を呼び出し側へ3回書かない（Phase 2.6 検査6 弱→前提宣言）
- planning判断: 地表なしは理由行（ツールチップ）で知らせ、毎フレームの開発者ログは出さない。前例 `PlacementGroundFollowStep`（通常設置の GroundNotFound も理由行のみ）に揃える
- planning判断: 歯車チェーンポールの `HasGhost`・`GhostTooFar`（bool 2つ）を enum `GearChainPoleGhostState { NoAimSurface, TooFar, GroundNotFound, Shown }` 1つに置き換える。bool と理由 enum を並べると「ゴースト有り＋距離超過」の無意味な状態が作れるため（Phase 2.6 検査5-B 強→前提宣言）
- planning判断: Task 2 で 3x3 ポールも Y が占有最高点を含むセルになるので、地表が平面に近い限り判定箱（最下段セルより上）へ地表は入らない。判定箱が効くのは、セル内の4隅サンプルの間で地表が盛り上がる崖・尾根だけになる（シミュレーターreviewの選択肢の説明どおり、ユーザーはこれを承知で選択）
- planning判断: 照準面なしで黙って非表示にする既存挙動の行（`TryGetFootprintGroundedRayHitBlockPosition` の戻り・歯車チェーンポールの早期 return と switch 既定枝）に `silent-ok:` を書く（Phase 2.6 検査7 弱→前提宣言）
- planning判断: ベルトの200行対策（`PushBeltReason` の移設と `cursorIndex<0` を公開APIへ出す案）は、bool 版の導入で置き換えが1行になり不要になったので採らない（Phase 2.6 検査5-B 強は対象ごと消滅）
- planning判断: `ConstructionCostService.CreateRefundItems` の同形の未分割は本PRに入れず、第3バケツとして人間に渡す（報告された退行の経路ではないため）
