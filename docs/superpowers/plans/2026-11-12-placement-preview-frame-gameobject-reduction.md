# 設置プレビュー枠の GameObject 削減 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 全設置ブロックに1個ずつ作っている設置プレビュー枠（1個37 GameObject）を、ブロックごとの当たり判定 GameObject 1個と、周囲20mの描画対象にだけ貸し出す使い回しの辺に分け、変更前後のワールド読み込み時間・設置モードに入る瞬間の時間・GameObject 数を測って記録する。

**Architecture:** 設置照準の当たり判定は、ブロック直下の GameObject 1個に今の枠と同じ薄い面の BoxCollider を6個付けた `BlockPlacementHitFaces` にする。どの面に当たったかは `hit.collider` から引く。辺は `BlockPreviewEdgeFramePool`（DI シングルトン）が、面を取り除いた既存プレハブ `BlockPreviewBoundingBox` を必要な数だけ作って貸し出す。ブロック側は `BlockPreviewEdgeFrameLease` を `IPreviewOnlyObject` として既存の `BlockPreviewOnlyObjectsView` に登録し、描画指示で借り、描画しない指示とブロック破棄で返す。

**Tech Stack:** Unity 6000.3.8f1 / C# / VContainer / UniRx / Addressables / NUnit（EditMode・EditModeInPlayingTest）/ uloop

## Requirements

1. 設置照準の当たり判定は、ブロックごとの GameObject 1個（`BlockBoundingBox` レイヤー）に、今の枠と同じ薄い面（厚み 0.14、各面の中心と大きさは今の枠と同一）の BoxCollider（isTrigger）を6個付けた形にする。受け入れ: ブロック配下に `BlockPreviewBoundingBox` が無く、`BlockPlacementHitFaces` が1個あり、BoxCollider が6個ある。
2. 照準がどの面に当たったかは、当たった BoxCollider から今の `PreviewSurfaceType`（ワールド基準の面）を引く。受け入れ: ブロックの外からワールド +X 側の面に撃つと `YZ_X`、上から撃つと `XZ_Y` になる（向き North・East の両方）。占有範囲の内側から +X へ撃つと、そのブロックの `YZ_X` 面に当たる。
3. 当たり判定は、設置モード中は全ブロックで有効、設置モード外は無効にする。半径外のブロックの横にも今どおり置ける（ADR 0083 決定4）。受け入れ: 設置モード中、半径外ブロックの `BlockPlacementHitFaces` が activeInHierarchy で BoxCollider が enabled。録画テストで半径外のブロック上面に設置できる。
3b. 当たり判定のコライダー（`BlockBoundingBox` レイヤー）は、ブロックのコライダー距離カリング（`BlockColliderCullingRegisterService`、半径120m）の対象に入れない。旧枠は生成後のフレームで付いたため対象外だった（推測。根拠: 11/8 の Profiler で枠15,082個が `DelayedActionManager.LateUpdate` の別フレームで生成）。受け入れ: カリングの登録対象（`CreateEntry` が返す対象）を範囲外にしても、`BlockPlacementHitFaces` の BoxCollider は enabled のまま。
4. 辺は全ブロックに持たせない。描画指示（設置モードで周囲20m以内、またはデバッグのホバー `Rendered`）が来たブロックにだけ、プールの枠を貸す。描画しない指示（`Hidden`・`CollidersOnly`・半径外）とブロック破棄（撤去・置き換えの両経路）で返す。受け入れ: 半径内ブロックの位置に有効な枠があり、半径外ブロックの位置には無い。設置モードを出ると有効な枠が0個になる。出入りをくり返しても枠の総数が増え続けない。
5. 枠のプールは数を減らさず、空きが無ければ新しく作る。プールの枠はブロックの子にしない。受け入れ: 枠の親はプールの親 Transform。
6. 見た目（辺の位置・長さ・太さ・マテリアル）は今と同じ。受け入れ: 枠の辺は今の `BlockPreviewBoundingBox.SetBoundingBox` と同じ計算で、ブロックのワールド位置・回転・スケールに合わせる。録画のスクショで今と同じ枠が出る。
7. 変更前（master）と変更後（ブランチ）を、同じ日・同じ windows-desktop・Game ビューを前に出した同じ表示状態で測り、差を報告して `docs/performance/` に記録する。対象は (a) ワールド読み込み時間（開始から定常までの秒数と、1秒超のフレームの内訳）、(b) 設置モードに入る瞬間（入った直後のフレーム時間の最大と合計、3回）、(c) 定常時の Transform 総数と内訳、(d) 定常時ベンチマーク。受け入れ: 記録の README に前後の表があり、ユーザーへの最終報告に前後の数値が入っている。
   出所: ユーザー裁定 2026-11-12 原文「作業が終わったら変更前と変更後でどのぐらいの時間変わるのかをちゃんと計測して報告と記録して」「設置モードに入る瞬間もどのぐらい軽くなるのかも」

やらないこと:
- ブループリントのサムネイル被写体の残留（約3.7万 GameObject）、ベルトコンベア・土台の階層削減（ADR 0099「後続」）
- 他のプレビュー専用部品（`InventoryConnectorView`・`GearConnectorView` など）の作り替え
- 当たり判定を物理エンジンの外で計算する方式（ADR 0099 決定2の棄却案）
- 設置照準の当たり方・見た目・表示ルール（20m・設置アニメ中は描かない・設置モード外は消す）の変更

## Global Constraints

- AGENTS.md の規約をすべて守る（コメントは日本語1行＋英語1行の2行セット、1ファイル200行以下、partial・`Func<>`・デフォルト引数・try-catch 禁止、単純 getter/setter プロパティ禁止で `{ get; private set; }` は可、`#region Internal` はメソッド内ローカル関数だけ、fail-closed の経路は理由をログへ出すか `silent-ok:` コメントで理由を書く）。
- Prefab は Write/Edit で直接編集しない。`uloop execute-dynamic-code` 経由（Unity がシリアライズ）だけで変える。`.meta` は手で作らない（削除するファイルの `.meta` は一緒に消してよい）。
- `.cs` を変えたら必ず `uloop compile --project-path ./moorestech_client` を通す（ErrorCount 0）。
- コミットはプレーンな `git commit`。`--author`・`Co-Authored-By` などの trailer を付けない。
- 計測は windows-desktop（i7-12700／RTX 3060 Ti、`G:\GitHub\moorestech`）で、ベースラインワールド `2026-10-22-world_1` を使う。Game ビューと Editor ウィンドウを前に出して測る（`docs/performance/benchmark/snippets/enter-world.cs` が行う）。変更前と変更後は同じ日に続けて測る。
- 計測で作った作業ワールドは `C:\moorestech-perf\work\current` だけに置く。テスト・録画テストで `Saves/` に作ったワールドは作業の終わりに `list-work-saves.sh` で探して消す。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / コライダー距離カリングの除外が当たり判定の具体型を名指ししていたので、`BlockBoundingBox` レイヤーでの除外へ直した（前例 RuntimeOutlineFactory）。死活表で死ぬ操作は0件
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強2・弱7・第3バケツ0 / ADR 0099 決定4を実装方針（破棄で返す・ClientDIContext で配る）へ更新し、借り口の早期 return に silent-ok を付け、照準の読み替え4か所を1関数へまとめた。残りは挙動に影響しないため手を入れない（判断記録）

---

## File Structure

| ファイル | 種別 | 責務 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPlacementHitFaces.cs` | Create | ブロック直下に置く設置照準の当たり判定。薄い面6枚の BoxCollider と、collider→`PreviewSurfaceType` の対応を持つ。`IPreviewOnlyObject` |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPreviewBoundingBoxSurface.cs` | Delete | 旧・面1枚ごとのコンポーネント |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPreviewBoundingBox.cs` | Modify | 辺だけを伸ばす。面のフィールドと処理を削る |
| `moorestech_client/Assets/AddressableResources/Block/Util/BlockPreviewBoundingBox.prefab` | Modify（uloop 経由のみ） | 面6系統と root の `PreviewOnlyObject` を取り除き、辺だけの枠にする |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Preview/PlacementFrame/BlockPreviewEdgeFramePool.cs` | Create | 辺の枠の使い回し。貸す・返す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Preview/PlacementFrame/BlockPreviewEdgeFrameLease.cs` | Create | ブロック1個ぶんの枠の借り口。`IPreviewOnlyObject` として表示指示に従って借り・返す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockPreviewBoundingBoxLoader.cs` | Delete | 旧・全ブロックへの枠の非同期生成 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs` | Modify | 当たり判定を同期生成し、借り口を表示に登録する |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs` | Modify | プールを static で配る |
| `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs` | Modify | プールを Singleton 登録 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/Block/BlockColliderCullingRegisterService.cs` | Modify | `BlockBoundingBox` レイヤーのコライダーをカリング対象から外す |
| `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockColliderCullingExcludesHitFacesTest.cs` | Create | 上の確認（EditMode） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs` | Modify | 照準の out を `PreviewSurfaceType?` に変える |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolver.cs` | Modify | 同上の受け取り |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs` | Modify | 同上（変数名だけ） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs` | Modify | 同上（変数名だけ） |
| `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockPlacementHitFacesTest.cs` | Create | 当たり判定の面の対応（EditMode） |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/Entry/FootprintGroundedEntryFixture.cs` | Modify | `CreateFace` を新コンポーネントで作る |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/Entry/FootprintGroundedRayEntryTest.cs` | Modify | 面の比較を `PreviewSurfaceType` で行う |
| `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/PlaceBlockPreviewDisplayTest.cs` | Modify | 新構造で「半径外でも当たり判定は有効」「半径内だけ枠」「出入りで枠が増え続けない」を確かめる |
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/BlockInventory/SpaceElevator/SpaceElevatorConnectActionTest.cs` | Modify | 遅延ロード待ちを当たり判定の存在確認に替える |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs` | Modify | 枠の判定を「プールの枠がブロック位置にあるか」に替える |
| `docs/performance/benchmark/snippets/buildmode-enter.cs`・`buildmode-exit.cs`・`buildmode-frames-collect.cs`・`docs/performance/benchmark/run-buildmode-entry.sh` | Create | 設置モードに入る瞬間の計測 |
| `docs/performance/2026-11-12-placement-preview-frame/README.md` | Create | 前後比較の記録 |

---

### Task 1: 設置照準の当たり判定 `BlockPlacementHitFaces`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPlacementHitFaces.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockPlacementHitFacesTest.cs`

**Interfaces:**
- Consumes: 既存の `PreviewSurfaceTypeResolver.Resolve(BlockDirection blockDirection, Vector3Int localOutwardNormal) : PreviewSurfaceType`、`LayerConst.BlockBoundingBoxLayer`、`IPreviewOnlyObject`
- Produces:
  - `public sealed class BlockPlacementHitFaces : MonoBehaviour, IPreviewOnlyObject`（namespace `Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject`）
  - `public static BlockPlacementHitFaces Create(Transform blockRoot, Vector3Int blockSize, BlockDirection blockDirection)` — `blockRoot` の子に local 原点で作る
  - `public PreviewSurfaceType GetSurfaceType(Collider collider)` — 自分の6面以外の collider を渡すのは設計上起きない（`Dictionary` の KeyNotFound で落ちてよい）

- [ ] **Step 1: テストを書く**

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class BlockPlacementHitFacesTest
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [TestCase(BlockDirection.North)]
        [TestCase(BlockDirection.East)]
        public void OutsideRays_ResolveWorldFaces(BlockDirection direction)
        {
            var bounds = CreateHitFaces(direction);

            // 外から各ワールド面へ撃つと、その面の種類が返る
            // A ray from outside toward each world face returns that face type
            Assert.AreEqual(PreviewSurfaceType.YZ_X, CastFrom(bounds.center + Vector3.right * 20, Vector3.left));
            Assert.AreEqual(PreviewSurfaceType.YZ_Origin, CastFrom(bounds.center + Vector3.left * 20, Vector3.right));
            Assert.AreEqual(PreviewSurfaceType.XZ_Y, CastFrom(bounds.center + Vector3.up * 20, Vector3.down));
            Assert.AreEqual(PreviewSurfaceType.YX_Z, CastFrom(bounds.center + Vector3.forward * 20, Vector3.back));
        }

        [Test]
        public void InsideRay_HitsFarFaceFromInside()
        {
            // 占有範囲の内側から撃っても、奥の面に当たる（今の枠と同じ）
            // A ray starting inside the footprint still hits the far face, as the old frame did
            var bounds = CreateHitFaces(BlockDirection.North);
            Assert.AreEqual(PreviewSurfaceType.YZ_X, CastFrom(bounds.center, Vector3.right));
        }

        [Test]
        public void Create_MakesOneObjectWithSixTriggerFaces()
        {
            CreateHitFaces(BlockDirection.North);
            var hitFaces = _root.GetComponentInChildren<BlockPlacementHitFaces>();
            Assert.AreEqual(1, _root.GetComponentsInChildren<BlockPlacementHitFaces>().Length);
            var colliders = hitFaces.GetComponents<BoxCollider>();
            Assert.AreEqual(6, colliders.Length);
            foreach (var collider in colliders) Assert.IsTrue(collider.isTrigger);
            Assert.AreEqual(Client.Common.LayerConst.BlockBoundingBoxLayer, hitFaces.gameObject.layer);
        }

        private Bounds CreateHitFaces(BlockDirection direction)
        {
            _root = new GameObject("hit-faces-test-block");
            _root.transform.SetPositionAndRotation(new Vector3(1000, 50, 1000), direction.GetRotation());
            var hitFaces = BlockPlacementHitFaces.Create(_root.transform, new Vector3Int(2, 1, 3), direction);
            Physics.SyncTransforms();
            var colliders = hitFaces.GetComponents<BoxCollider>();
            var bounds = colliders[0].bounds;
            foreach (var collider in colliders) bounds.Encapsulate(collider.bounds);
            return bounds;
        }

        private static PreviewSurfaceType CastFrom(Vector3 origin, Vector3 direction)
        {
            Assert.IsTrue(Physics.Raycast(origin, direction, out var hit, 100f, Client.Common.LayerConst.BlockBoundingBoxOnlyLayerMask), $"no hit from {origin} toward {direction}");
            return hit.collider.GetComponent<BlockPlacementHitFaces>().GetSurfaceType(hit.collider);
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

```csharp
using System.Collections.Generic;
using Client.Common;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
{
    /// <summary>
    ///     設置照準だけが当たる薄い面6枚。占有マスの直方体の各面に置く
    ///     Six thin faces hit only by the placement aim, one on each side of the occupied-cell box
    /// </summary>
    public sealed class BlockPlacementHitFaces : MonoBehaviour, IPreviewOnlyObject
    {
        // 旧プレハブの面と同じ厚み。内側から撃っても奥の面に当たる
        // Same thickness as the old prefab faces, so a ray from inside still hits the far face
        private const float FaceThickness = 0.14f;

        private readonly Dictionary<Collider, PreviewSurfaceType> _surfaceTypeByCollider = new();

        public static BlockPlacementHitFaces Create(Transform blockRoot, Vector3Int blockSize, BlockDirection blockDirection)
        {
            var hitFacesObject = new GameObject("BlockPlacementHitFaces") { layer = LayerConst.BlockBoundingBoxLayer };
            hitFacesObject.transform.SetParent(blockRoot, false);
            var hitFaces = hitFacesObject.AddComponent<BlockPlacementHitFaces>();

            // 面は旧プレハブと同じくブロックローカルの占有範囲に沿わせる
            // Faces follow the block-local footprint, matching the old prefab
            var size = (Vector3)blockSize;
            hitFaces.AddFace(new Vector3(size.x / 2, size.y / 2, 0), new Vector3(size.x, size.y, FaceThickness), new Vector3Int(0, 0, -1), blockDirection);
            hitFaces.AddFace(new Vector3(size.x / 2, size.y / 2, size.z), new Vector3(size.x, size.y, FaceThickness), new Vector3Int(0, 0, 1), blockDirection);
            hitFaces.AddFace(new Vector3(0, size.y / 2, size.z / 2), new Vector3(FaceThickness, size.y, size.z), new Vector3Int(-1, 0, 0), blockDirection);
            hitFaces.AddFace(new Vector3(size.x, size.y / 2, size.z / 2), new Vector3(FaceThickness, size.y, size.z), new Vector3Int(1, 0, 0), blockDirection);
            hitFaces.AddFace(new Vector3(size.x / 2, 0, size.z / 2), new Vector3(size.x, FaceThickness, size.z), new Vector3Int(0, -1, 0), blockDirection);
            hitFaces.AddFace(new Vector3(size.x / 2, size.y, size.z / 2), new Vector3(size.x, FaceThickness, size.z), new Vector3Int(0, 1, 0), blockDirection);
            return hitFaces;
        }

        public PreviewSurfaceType GetSurfaceType(Collider collider)
        {
            return _surfaceTypeByCollider[collider];
        }

        public void Initialize(BlockId blockId)
        {
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        // 描画物を持たないので何もしない
        // Holds nothing to render
        public void SetEnableRenderers(bool enable)
        {
        }

        private void AddFace(Vector3 center, Vector3 size, Vector3Int localOutwardNormal, BlockDirection blockDirection)
        {
            // 外向き法線を回してワールド面を決める
            // Rotate the outward normal to get the world face
            var faceCollider = gameObject.AddComponent<BoxCollider>();
            faceCollider.isTrigger = true;
            faceCollider.center = center;
            faceCollider.size = size;
            _surfaceTypeByCollider.Add(faceCollider, PreviewSurfaceTypeResolver.Resolve(blockDirection, localOutwardNormal));
        }
    }
}
```

- [ ] **Step 3: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.BlockPlacementHitFacesTest"`
Expected: ErrorCount 0 / 4件 PASS（TestCase 2件＋2件）。East で `YZ_X` 等が合わない場合は、テストの期待値ではなく `PreviewSurfaceTypeResolver.Resolve` の回転方向と旧プレハブの対応（旧 `BlockPreviewBoundingBox.cs` の `SetPreviewSurfaceType` 6行）を照合して原因を報告する。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPlacementHitFaces.cs moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockPlacementHitFacesTest.cs
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPlacementHitFaces.cs.meta moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockPlacementHitFacesTest.cs.meta
git commit -m "perf(preview): 設置照準の当たり判定を、1個の GameObject に薄い面6枚を付ける BlockPlacementHitFaces として用意する"
```

---

### Task 2: 辺の枠のプールと借り口

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Preview/PlacementFrame/BlockPreviewEdgeFramePool.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Preview/PlacementFrame/BlockPreviewEdgeFrameLease.cs`
- Modify（uloop 経由のみ）: `moorestech_client/Assets/AddressableResources/Block/Util/BlockPreviewBoundingBox.prefab`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPreviewBoundingBox.cs`

**Interfaces:**
- Consumes: `AddressableLoader.LoadDefault<T>(string address) : T`（`Client.Common.Asset`）、`BlockGameObject.BlockMasterElement.BlockSize`、`IPreviewOnlyObject`、UniRx `OnDestroyAsObservable()`
- Produces:
  - `public void BlockPreviewBoundingBox.SetBoundingBox(Vector3Int blockSize)`（旧 `SetBoundingBox(Vector3Int, BlockDirection, BlockGameObject)` を置き換え）
  - `public sealed class BlockPreviewEdgeFramePool`（namespace `Client.Game.InGame.Block.Preview.PlacementFrame`）: `public BlockPreviewBoundingBox Rent(BlockGameObject owner)` / `public void Return(BlockPreviewBoundingBox frame)`
  - `public sealed class BlockPreviewEdgeFrameLease : IPreviewOnlyObject`: `public BlockPreviewEdgeFrameLease(BlockGameObject owner, BlockPreviewEdgeFramePool pool)`

このタスクの単体テストは置かない（プールは Addressables の実プレハブを要するため）。挙動は Task 3 の `PlaceBlockPreviewDisplayTest` と Task 4 の録画テストで確かめる。

- [ ] **Step 0: プレハブから面を取り除く（どのコードも変える前に、全体がコンパイルできる状態で行う）**

`/tmp` ではなく scratchpad に次を書き、`uloop execute-dynamic-code --project-path ./moorestech_client --code-file <path>` で実行する。

```csharp
using UnityEditor;
using UnityEngine;
var path = "Assets/AddressableResources/Block/Util/BlockPreviewBoundingBox.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
foreach (var name in new[] { "XY_origin_Surface", "XY_z_Surface", "YZ_origin_Surface", "YZ_x_Surface", "ZX_origin_Surface", "ZX_y_Surface" })
    Object.DestroyImmediate(root.transform.Find(name).gameObject);
Object.DestroyImmediate(root.GetComponent<Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject.PreviewOnlyObject>());
PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);
var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
return "transforms=" + saved.GetComponentsInChildren<Transform>(true).Length + " colliders=" + saved.GetComponentsInChildren<Collider>(true).Length + " renderers=" + saved.GetComponentsInChildren<Renderer>(true).Length;
```

Expected: `transforms=25 colliders=0 renderers=12`。

- [ ] **Step 1: `BlockPreviewBoundingBox.cs` から面を削る**

`xy_Origin_Surface` 〜 `zx_Y_SurfaceScript` の12個の `[SerializeField]` フィールドと、`SetBoundingBox` 内の「面」に関する行（`*_Surface.transform.localScale/localPosition` の12行と、`*_SurfaceScript.SetPreviewSurfaceType(...)` の6行とその上のコメント2行）を削除し、シグネチャを次に変える。辺（`x_*_Edge`・`y_*_Edge`・`z_*_Edge` の12本）の処理は1文字も変えない。

```csharp
        public void SetBoundingBox(Vector3Int blockSize)
```

ファイル先頭の `using Client.Game.InGame.Block;` と `using Game.Block.Interface;` が不要になれば削る。

- [ ] **Step 2: プールを書く**

```csharp
using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using UnityEngine;

namespace Client.Game.InGame.Block.Preview.PlacementFrame
{
    /// <summary>
    ///     設置モードの枠の辺を使い回す。数は減らさず、空きが無ければ作る
    ///     Reuses the build-mode frame edges; never shrinks and creates one when none is idle
    /// </summary>
    public sealed class BlockPreviewEdgeFramePool
    {
        private const string EdgeFrameAddressablePath = "Vanilla/Block/Util/BlockPreviewBoundingBox";

        private readonly Stack<BlockPreviewBoundingBox> _idleFrames = new();
        private Transform _poolRoot;
        private GameObject _edgeFramePrefab;

        public BlockPreviewBoundingBox Rent(BlockGameObject owner)
        {
            var frame = _idleFrames.Count > 0 ? _idleFrames.Pop() : CreateFrame();

            // ブロックの姿勢と占有範囲に合わせる。ブロックは動かないので貸す時だけでよい
            // Match the block pose and footprint; blocks never move, so only on rent
            var ownerTransform = owner.transform;
            frame.transform.SetPositionAndRotation(ownerTransform.position, ownerTransform.rotation);
            frame.transform.localScale = ownerTransform.lossyScale;
            frame.SetBoundingBox(owner.BlockMasterElement.BlockSize);
            frame.gameObject.SetActive(true);
            return frame;

            #region Internal

            BlockPreviewBoundingBox CreateFrame()
            {
                // 初回の貸し出しでプレハブを1度だけ読み、親を作る
                // Load the prefab once and create the parent on the first rent
                if (_poolRoot == null)
                {
                    _poolRoot = new GameObject("BlockPreviewEdgeFramePool").transform;
                    _edgeFramePrefab = AddressableLoader.LoadDefault<GameObject>(EdgeFrameAddressablePath);
                }
                return Object.Instantiate(_edgeFramePrefab, _poolRoot).GetComponent<BlockPreviewBoundingBox>();
            }

            #endregion
        }

        public void Return(BlockPreviewBoundingBox frame)
        {
            // silent-ok: シーン破棄でプールの枠が先に壊れた時だけ起きる / 終了処理中で以後の貸し出しは無い
            // Only happens when scene teardown destroyed the frame first; no rent follows during shutdown
            if (frame == null) return;
            frame.gameObject.SetActive(false);
            _idleFrames.Push(frame);
        }
    }
}
```

- [ ] **Step 3: 借り口を書く**

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Core.Master;
using UniRx;
using UniRx.Triggers;

namespace Client.Game.InGame.Block.Preview.PlacementFrame
{
    /// <summary>
    ///     ブロック1個ぶんの枠の借り口。描画指示で借り、描画しない指示と破棄で返す
    ///     A block's frame lease; rents on a render instruction and returns on a non-render instruction or destruction
    /// </summary>
    public sealed class BlockPreviewEdgeFrameLease : IPreviewOnlyObject
    {
        private readonly BlockGameObject _owner;
        private readonly BlockPreviewEdgeFramePool _pool;
        private BlockPreviewBoundingBox _rentedFrame;

        public BlockPreviewEdgeFrameLease(BlockGameObject owner, BlockPreviewEdgeFramePool pool)
        {
            _owner = owner;
            _pool = pool;
            // 撤去と置き換えのどちらの破棄でも返す
            // Return on destruction by either removal or replacement
            owner.OnDestroyAsObservable().Subscribe(_ => Release());
        }

        public void Initialize(BlockId blockId)
        {
        }

        // 有効化だけでは借りない。描画の指示が来た時だけ借りる
        // Activation alone never rents; only a render instruction does
        public void SetActive(bool active)
        {
            if (!active) Release();
        }

        public void SetEnableRenderers(bool enable)
        {
            if (enable) Rent();
            else Release();
        }

        private void Rent()
        {
            // silent-ok: 描画指示は半径の再評価ごとに重ねて届く / 借りている枠をそのまま使う
            // Render instructions repeat on every radius re-evaluation; keep the frame already rented
            if (_rentedFrame != null) return;
            _rentedFrame = _pool.Rent(_owner);
        }

        private void Release()
        {
            // silent-ok: 描画しない指示は借りていない時にも届く（設置モード外の全ブロック等） / 返すものが無い
            // Non-render instructions also arrive with nothing rented (e.g. every block outside build mode)
            if (_rentedFrame == null) return;
            _pool.Return(_rentedFrame);
            _rentedFrame = null;
        }
    }
}
```

- [ ] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: この時点では旧 `BlockPreviewBoundingBoxLoader.cs` が旧シグネチャの `SetBoundingBox(master.BlockSize, posInfo.BlockDirection, owner)` を呼ぶのでコンパイルエラーになる。Task 2 と Task 3 は同じコミットにまとめるため、ここではコミットせず Task 3 へ進む（エラーは Task 3 Step 1〜2 で旧ローダーを消し `BlockGameObject` を組み替えると解消する）。

---

### Task 3: ブロックへの組み込み・照準の切り替え・旧枠の撤去

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockPreviewBoundingBoxLoader.cs`（＋`.meta`）
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPreviewBoundingBoxSurface.cs`（＋`.meta`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/Block/BlockColliderCullingRegisterService.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockColliderCullingExcludesHitFacesTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteOriginResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/Entry/FootprintGroundedEntryFixture.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/Entry/FootprintGroundedRayEntryTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/PlaceBlockPreviewDisplayTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/BlockInventory/SpaceElevator/SpaceElevatorConnectActionTest.cs`

**Interfaces:**
- Consumes: Task 1 の `BlockPlacementHitFaces.Create` / `GetSurfaceType`、Task 2 の `BlockPreviewEdgeFramePool` / `BlockPreviewEdgeFrameLease` / `SetBoundingBox(Vector3Int)`
- Produces:
  - `public static BlockPreviewEdgeFramePool ClientDIContext.BlockPreviewEdgeFramePool { get; private set; }`
  - `PlaceSystemUtil` の照準系の out 引数を `out PreviewSurfaceType? surfaceType` に変更（`TryGetRayHitBlockPosition` / `ResolveFootprintGroundedRayHitBlockPosition` / `TryGetFootprintGroundedRayHitBlockPosition` / `TryGetRayHitPlacePointBySize`（2つ）/ `TryGetRayHitPosition` / `TryRaycastPlacementSurface`）。`null` が地面、値ありがブロックの面

- [ ] **Step 1: 旧ファイルを消す**

```bash
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockPreviewBoundingBoxLoader.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockPreviewBoundingBoxLoader.cs.meta
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPreviewBoundingBoxSurface.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPreviewBoundingBoxSurface.cs.meta
```

`PreviewSurfaceType` enum は `BlockPreviewBoundingBoxSurface.cs` に同居しているので、消す前に同じ namespace の新ファイル `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/PreviewSurfaceType.cs` へ、コメントごとそのまま移す。

```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
{
    public enum PreviewSurfaceType
    {
        YX_Origin, // YX平面の手前側 Front side of YX plane
        YX_Z, // YX平面のZ方向の奥側 Back side of the YX plane in the Z direction
        
        YZ_Origin, // YZ平面の手前側 Front side of YZ plane
        YZ_X, // YZ平面のX方向の奥側 Back side of the YZ plane in the X direction
        
        XZ_Origin, // XZ平面の下側 Bottom side of XZ plane
        XZ_Y, // XZ平面のY方向の上側 Top side of XZ plane
    }
}
```

- [ ] **Step 2: `BlockGameObject.Initialize` を組み替える**

`using Client.Game.InGame.Block.Preview.PlacementFrame;` と `using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;` を足し、`Initialize` の「プレビュー限定オブジェクトをオフに」から `#endregion` までを次に置き換える（`RequestInitialBlockState` はそのまま残す）。

```csharp
            // 設置照準の当たり判定を作る。表示切り替えの対象に入るよう、プレビュー限定の収集より先に置く
            // Create the placement-aim hit faces before collecting preview-only objects so the display switch includes them
            BlockPlacementHitFaces.Create(transform, blockMasterElement.BlockSize, posInfo.BlockDirection);
            // プレビュー限定オブジェクトをオフに
            // Turn off preview-only object
            _previewOnlyObjectsView = new BlockPreviewOnlyObjectsView(BlockPreviewOnlyObjects.Initialize(gameObject, BlockId, false));
            // 枠の辺は描画指示が来た時だけプールから借りる
            // Frame edges are rented from the pool only on a render instruction
            _previewOnlyObjectsView.Add(new BlockPreviewEdgeFrameLease(this, ClientDIContext.BlockPreviewEdgeFramePool));

            // 初期状態を要求。返答はtick末尾の束で届く
            // Ask the server for the initial block state; the reply arrives via BlockStateEventHandler in the tick-end bundle
            RequestInitialBlockState();
            #region Internal

            void RequestInitialBlockState()
            {
                ClientContext.VanillaApi.SendOnly.RequestBlockState(BlockPosInfo.OriginalPos);
            }
            #endregion
```

`AddBoundingBox` ローカル関数と、`using Cysharp.Threading.Tasks;` のうち不要になったもの（`PlayPlaceAnimation` などが `UniTask` を使うので通常は残る）を整理する。

- [ ] **Step 3: DI に登録して配る**

`MainGameModelRegistration.cs` の `builder.RegisterEntryPoint<BlockPreviewRadiusRegisterService>();` の直後に足す。

```csharp
            builder.Register<BlockPreviewEdgeFramePool>(Lifetime.Singleton);
```

（`using Client.Game.InGame.Block.Preview.PlacementFrame;` を足す）

`ClientDIContext.cs` にプロパティと解決を足す。

```csharp
        public static BlockPreviewEdgeFramePool BlockPreviewEdgeFramePool { get; private set; }
```

```csharp
            BlockPreviewEdgeFramePool = diContainer.DIContainerResolver.Resolve<BlockPreviewEdgeFramePool>();
```

- [ ] **Step 3b: 当たり判定をコライダー距離カリングから外す**

`BlockColliderCullingRegisterService.CreateEntry` の最初の行を次に替える（`using System.Linq;` と `using Client.Common;` を足す）。除外はレイヤーで行う（前例: `Client.Game/InGame/Interact/Outline/RuntimeOutlineFactory.cs:123` が `LayerConst.BlockBoundingBoxLayer` を除外している）。

```csharp
            // 設置照準専用レイヤーの当たり判定は距離に関係なく有効のまま残す（ADR 0083 決定4）
            // Colliders on the placement-aim-only layer stay enabled regardless of distance (ADR 0083 decision 4)
            var colliders = block.GetComponentsInChildren<Collider>(true).Where(collider => collider.gameObject.layer != LayerConst.BlockBoundingBoxLayer).ToArray();
```

テスト `BlockColliderCullingExcludesHitFacesTest.cs`:

```csharp
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.ColliderStreaming;
using Client.Game.InGame.ColliderStreaming.Block;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class BlockColliderCullingExcludesHitFacesTest
    {
        [Test]
        public void OutOfRange_KeepsHitFacesEnabled_AndDisablesBlockColliders()
        {
            var storeObject = new GameObject("culling-test-store");
            var blockObject = new GameObject("culling-test-block");
            var block = blockObject.AddComponent<BlockGameObject>();
            var bodyCollider = blockObject.AddComponent<BoxCollider>();
            var hitFaces = BlockPlacementHitFaces.Create(blockObject.transform, Vector3Int.one, BlockDirection.North);
            var service = new BlockColliderCullingRegisterService(storeObject.AddComponent<BlockGameObjectDataStore>(), new ColliderDistanceCullingManager(null));

            // 範囲外にすると本体のコライダーだけが切れる
            // Going out of range disables only the block body collider
            service.CreateEntry(block).Target.SetWithinRange(false);
            Assert.IsFalse(bodyCollider.enabled);
            foreach (var faceCollider in hitFaces.GetComponents<BoxCollider>()) Assert.IsTrue(faceCollider.enabled);

            Object.DestroyImmediate(blockObject);
            Object.DestroyImmediate(storeObject);
        }
    }
}
```

`BlockDistanceCullingEntry.Target` は internal だが、`Client.Game/AssemblyInfo.cs` の `InternalsVisibleTo("Client.Tests")` でテストから読める。`ColliderDistanceCullingManager(PlayerSystemContainer)` は Tick しなければ null で作れる。

- [ ] **Step 4: 照準の out を `PreviewSurfaceType?` に変える**

`PlaceSystemUtil.cs` で `out BlockPreviewBoundingBoxSurface surface` をすべて `out PreviewSurfaceType? surfaceType` に、本文中の `surface` を `surfaceType` に替える。`surfaceType == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace` はそのまま成り立つ。`TryGetRayHitPlacePointBySize`（3引数 out 版）の `var surfaceType = surface == null ? (PreviewSurfaceType?)null : surface.PreviewSurfaceType;` の行は消し、`CalcPlacePointBySize(rotatedSize, hit.point, heightOffset, surfaceType)` に直接渡す。`TryRaycastPlacementSurface` は次に置き換える。

```csharp
        internal static bool TryRaycastPlacementSurface(Camera mainCamera, out RaycastHit hit, out PreviewSurfaceType? surfaceType)
        {
            surfaceType = null;
            var ray = mainCamera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());

            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out hit, float.PositiveInfinity, LayerConst.Without_Player_MapObject_Block_LayerMask)) return false;
            // 地面か、ブロックの設置用当たり判定に当たった時だけ設置面とする
            // Only the ground or a block's placement hit faces count as a placement surface
            if (hit.transform.TryGetComponent<GroundGameObject>(out _)) return true;
            if (!hit.transform.TryGetComponent<BlockPlacementHitFaces>(out var hitFaces)) return false;
            surfaceType = hitFaces.GetSurfaceType(hit.collider);
            return true;
        }
```

「null なら地面、値があればブロックの面」の読み替えは、`PlaceSystemUtil` に関数1つとして置き、4か所（`PlaceSystemUtil.ResolveFootprintGroundedRayHitBlockPosition`・`BlueprintPasteOriginResolver`・`CommonBlockPlaceSystem`・`BeltConveyorPlaceSystem`）はこれを呼ぶ。

```csharp
        // 照準の結果を、地面かブロックの面かの区別へ読み替える
        // Map an aim result to whether it hit the ground or a block face
        public static PlacementHitSurfaceKind ToHitSurfaceKind(PreviewSurfaceType? surfaceType)
        {
            return surfaceType == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace;
        }
```

呼び出し側:
- `PlaceSystemUtil.ResolveFootprintGroundedRayHitBlockPosition`: `var surfaceKind = surface == null ? ... ;` → `var surfaceKind = ToHitSurfaceKind(surfaceType);`
- `BlueprintPasteOriginResolver.TryResolveCursorOrigin`: `out var surface` → `out var surfaceType`、`surfaceKind = surface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace;` → `surfaceKind = PlaceSystemUtil.ToHitSurfaceKind(surfaceType);`、`AlignSideFace(anchor, hitPoint, surface == null ? null : surface.PreviewSurfaceType, heightOffset)` → `AlignSideFace(anchor, hitPoint, surfaceType, heightOffset)`
- `CommonBlockPlaceSystem.cs:134-138`: `out var hitSurface` → `out var hitSurfaceType`、`_dragState.ResolveSurfaceKind(hitSurface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace)` → `_dragState.ResolveSurfaceKind(ToHitSurfaceKind(hitSurfaceType))`（同ファイルは `using static ...PlaceSystemUtil` で `TryGetRayHitBlockPosition` を直接呼んでいるので同じ形で呼ぶ。`using static` が無ければ `PlaceSystemUtil.` を付ける）
- `BeltConveyorPlaceSystem.cs:124-130`: `out var hitSurface` → `out var hitSurfaceType`、`hitSurface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace` → `ToHitSurfaceKind(hitSurfaceType)`（`using static` の扱いは同上）

`PlaceSystemUtil.cs` は変更後も200行以下に収まることを確認する（現在の行数は Task 開始時に `wc -l` で確かめ、超えるなら照準まわりの関数を `PlaceSystemRaycastUtil.cs`（同じフォルダの既存ファイル）へ移す）。
- `out _` で捨てている呼び出し（`PlacementUnitCellResolver`・`GearChainPoleFrameInputCollector`・`TrainRailConnectSystem`）は変更不要

- [ ] **Step 5: テストを新構造に合わせる**

`FootprintGroundedEntryFixture.CreateFace` を置き換える（上面の中心 (501.5, 25, 600.5)、10×10 の上面）。

```csharp
        protected PreviewSurfaceType CreateFace()
        {
            // 上面の中心が旧テストの面と同じ位置に来るよう、10マス角のブロックとして作る
            // Build it as a 10-cell block so the top face center matches the old test face
            var block = CreateObject("BlockFace");
            block.transform.position = new Vector3(496.5f, 15f, 595.5f);
            BlockPlacementHitFaces.Create(block.transform, new Vector3Int(10, 10, 10), Game.Block.Interface.BlockDirection.North);
            Physics.SyncTransforms();
            return PreviewSurfaceType.XZ_Y;
        }
```

`FootprintGroundedRayEntryTest.RayEntryPreservesBlockFaceCellAndOffset` の `out var surface` を `out var surfaceType` に、`Assert.AreSame(face, surface);` を `Assert.AreEqual(face, surfaceType);` に替える。同ファイルの `Assert.IsNull(surface);` は `Assert.IsNull(surfaceType);` に替える。

`PlaceBlockPreviewDisplayTest` の `WaitForBoundingBox` 呼び出し4行とローカル関数を消し、`AssertPreview` と最後の `Assert.IsFalse(...BlockPreviewBoundingBox...)` を次に替える。最後の `display.OnExit();` の直後に、枠の総数が増え続けないことも確かめる。

```csharp
            void AssertPreview(BlockGameObject block, bool rendered)
            {
                // 旧枠はブロック配下に無い
                // No old frame lives under the block
                Assert.IsNull(block.GetComponentInChildren<BlockPreviewBoundingBox>(true));

                // 描画抑止が隣接設置の面コライダーを失わせない
                // Rendering suppression must preserve face colliders used for adjacent placement
                var hitFaces = block.GetComponentInChildren<BlockPlacementHitFaces>(true);
                Assert.IsNotNull(hitFaces);
                Assert.IsTrue(hitFaces.gameObject.activeInHierarchy, "当たり判定は半径外でも有効");
                var colliders = hitFaces.GetComponents<BoxCollider>();
                Assert.AreEqual(6, colliders.Length);
                foreach (var collider in colliders) Assert.IsTrue(collider.enabled);

                // 半径内のブロック位置にだけ、プールの枠が有効で置かれる
                // Only blocks within the radius get an active pooled frame at their position
                Assert.AreEqual(rendered, ActiveFrameAt(block) != null, block.name);
            }

            BlockPreviewBoundingBox ActiveFrameAt(BlockGameObject block)
            {
                foreach (var frame in Object.FindObjectsByType<BlockPreviewBoundingBox>(FindObjectsSortMode.None))
                    if (frame.transform.position == block.transform.position) return frame;
                return null;
            }
```

```csharp
                display.OnExit();
                // 出ると有効な枠は0個。出入りをくり返しても総数は増え続けない
                // Leaving leaves no active frame, and repeated entry does not keep growing the total
                Assert.AreEqual(0, Object.FindObjectsByType<BlockPreviewBoundingBox>(FindObjectsSortMode.None).Length);
                var totalAfterFirstCycle = Object.FindObjectsByType<BlockPreviewBoundingBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                display.OnEnter();
                display.OnExit();
                display.OnEnter();
                display.OnExit();
                Assert.AreEqual(totalAfterFirstCycle, Object.FindObjectsByType<BlockPreviewBoundingBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                Object.Destroy(storeObject);
```

`SpaceElevatorConnectActionTest.cs:56-58` の遅延ロード待ち（`for` ループと `Assert.IsNotNull(view.GetComponentInChildren<BlockPreviewBoundingBox>(true))`）を次に替え、コメントも合わせる。

```csharp
                // 設置照準の当たり判定も検査対象へ含める
                // Include the placement-aim hit faces in the inspected hierarchy
                Assert.IsNotNull(view.GetComponentInChildren<BlockPlacementHitFaces>(true));
```

- [ ] **Step 6: 残った参照を確かめる**

Run: `grep -rn "BlockPreviewBoundingBoxSurface\|BlockPreviewBoundingBoxLoader" moorestech_client/Assets/Scripts .agents/skills`
Expected: `build-preview-radius.cs` 以外に該当なし（シナリオは Task 4 で直す）。

- [ ] **Step 7: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockPlacementHitFacesTest|BlockColliderCullingExcludesHitFacesTest|FootprintGrounded|BlockPreviewOnlyObjectsViewTest|PlaceBlockPreviewDisplayTest|SpaceElevatorConnectActionTest" --timeout-seconds 1500`
Expected: ErrorCount 0 / すべて PASS。PlayMode 遷移後に「Unity is reloading」が出たら45秒待って再実行する。

- [ ] **Step 8: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests moorestech_client/Assets/AddressableResources/Block/Util/BlockPreviewBoundingBox.prefab
git commit -m "perf(preview): 設置プレビュー枠を、ブロックごとの当たり判定1個と、周囲20mにだけ貸す使い回しの辺に分ける（ADR 0099）"
```

---

### Task 4: unityプレイ録画テスト（`build-preview-radius`）

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs`

- [ ] **Step 1: シナリオの枠判定を新構造に合わせる**

`#region Internal` の `BoxAt` と `IsBoxRendered` を次に替え、`await p.Until(() => BoxAt(probe) != null, ...)` と `await p.Until(() => BoxAt(farTarget + Vector3Int.up) != null, ...)` の2か所を `HitFacesAt(...) != null` の待ちに替える（「非同期ロード後に」のコメントも「当たり判定の生成後に」へ直す）。`using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;` は既にある。

```csharp
    BlockPlacementHitFaces HitFacesAt(Vector3Int pos) => ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos).GetComponentInChildren<BlockPlacementHitFaces>(true);

    // プールの枠がそのブロックの位置で有効なら描画中
    // A block is outlined when an active pooled frame sits at its position
    bool IsBoxRendered(Vector3Int pos)
    {
        var block = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
        foreach (var frame in UnityEngine.Object.FindObjectsByType<BlockPreviewBoundingBox>(FindObjectsSortMode.None))
            if (frame.transform.position == block.transform.position) return frame.GetComponentInChildren<Renderer>(true).enabled;
        return false;
    }
```

- [ ] **Step 2: 録画テストを実行する**

`unity-playmode-recorded-playtest` スキルの手順（`scripts/run-scenario.sh`）で `scenarios/building/build-preview-radius.cs` を実行する。
Expected: result.json の全 Assert が成功（足元は枠あり・約42m先は枠なし・移動で切り替わる・半径外のチェスト上面へ照準して設置できる・新規の半径外ブロックは枠なし）。スクショ `01-build-mode-dense`・`02-after-move`・`03-place-on-far-block` で、枠の見た目が変更前と同じ（辺の太さ・位置）であることを目視で確かめ、`avgFrameMs` の値を控える。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs
git commit -m "test(playtest): 設置プレビュー半径の録画シナリオを、使い回しの枠と当たり判定の構造に合わせる"
```

---

### Task 5: 変更前後の計測と記録

**Files:**
- Create: `docs/performance/benchmark/snippets/buildmode-enter.cs`
- Create: `docs/performance/benchmark/snippets/buildmode-exit.cs`
- Create: `docs/performance/benchmark/snippets/buildmode-frames-collect.cs`
- Create: `docs/performance/benchmark/run-buildmode-entry.sh`
- Create: `docs/performance/2026-11-12-placement-preview-frame/README.md`（日付は計測日に合わせてよい）
- Modify: `docs/performance/README.md`、`docs/performance/benchmark/README.md`（結果一覧に行を足す）

- [ ] **Step 1: 設置モードに入る瞬間の計測スニペットを書く**

`buildmode-enter.cs`（Profiler を始めてから設置モードへ入る）:

```csharp
// Profiler を開始してから、キー入力を介さず本番の遷移要求で設置モードへ入る
// Start the profiler, then enter build mode through the production transition request without key input
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using UnityEditorInternal;
using UnityEngine;
ProfilerDriver.ClearAllFrames();
ProfilerDriver.profileEditor = false;
ProfilerDriver.deepProfiling = false;
ProfilerDriver.enabled = true;
var blockId = MasterHolder.BlockMaster.GetBlockAllIds()[0];
var target = new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid, null);
ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlaceSystemStateController>().SetTarget(target, PlacementOrigin.NonHotbar);
Object.FindFirstObjectByType<UIStateControl>().RequestTransition(UIStateEnum.PlaceBlock);
return "entered request frame=" + Time.frameCount;
```

`buildmode-exit.cs`:

```csharp
// 通常画面へ戻す
// Return to the normal game screen
using Client.Game.InGame.UI.UIState;
using UnityEngine;
Object.FindFirstObjectByType<UIStateControl>().RequestTransition(UIStateEnum.GameScreen);
return "exit requested frame=" + Time.frameCount;
```

`buildmode-frames-collect.cs`（Profiler を止め、記録中のフレーム時間の最大・合計と、最大フレームの上位階層を返す）:

```csharp
// 設置モード遷移の前後に記録したフレームの最大・合計と、最大フレームの上位を返す
// Return max/total frame time around the build-mode transition and the top items of the slowest frame
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;
ProfilerDriver.enabled = false;
var sb = new StringBuilder();
float max = 0, total = 0; int maxFrame = -1, frames = 0;
for (int f = ProfilerDriver.firstFrameIndex; f <= ProfilerDriver.lastFrameIndex; f++)
{
    using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
    {
        if (!v.valid) continue;
        frames++; total += v.frameTimeMs;
        if (v.frameTimeMs > max) { max = v.frameTimeMs; maxFrame = f; }
    }
}
sb.Append("{\"frames\":" + frames + ",\"maxFrameMs\":" + max.ToString("F1") + ",\"totalMs\":" + total.ToString("F1") + ",\"top\":\"");
using (var v = ProfilerDriver.GetHierarchyFrameDataView(maxFrame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
{
    var agg = new Dictionary<string, float>();
    System.Action<int> walk = null;
    walk = id => { var ch = new List<int>(); v.GetItemChildren(id, ch); foreach (var c in ch) { var n = v.GetItemName(c); agg[n] = (agg.TryGetValue(n, out var s) ? s : 0) + v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime); walk(c); } };
    walk(v.GetRootItemID());
    foreach (var kv in agg.OrderByDescending(k => k.Value).Take(15)) sb.Append(kv.Key.Replace("\"", "'") + "=" + kv.Value.ToString("F1") + "ms; ");
}
sb.Append("\"}");
ProfilerDriver.ClearAllFrames();
return sb.ToString();
```

- [ ] **Step 1b: 設置モードに入る瞬間の計測スクリプトを書く**

`docs/performance/benchmark/run-buildmode-entry.sh`（`chmod +x` する）:

```bash
#!/usr/bin/env bash
# windows-desktop でベースラインワールドを読み込み、定常後に Transform 内訳と設置モードに入る瞬間（3回）を測る
# usage: run-buildmode-entry.sh <label>  結果は results/<日時>_<label>_buildmode.txt
set -euo pipefail
LABEL="${1:?usage: run-buildmode-entry.sh <label>}"
HOST="${BENCH_HOST:-windows-desktop}"
PROJECT='G:\GitHub\moorestech\moorestech_client'
SN='C:\moorestech-perf\snippets'
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SSH_OPTS=(-o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=3)
remote() { ssh "${SSH_OPTS[@]}" "$HOST" "$@"; }
run() {
  remote "uloop execute-dynamic-code --code-file $SN\\$1 --project-path $PROJECT" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
d=json.loads(t[s:]) if s>=0 else {}
print(d.get("Result","") if d.get("Success") else "FAIL "+t[:800])'
}
OUT="$SCRIPT_DIR/results/$(date +%Y-%m-%dT%H%M)_${LABEL}_buildmode.txt"
remote "New-Item -ItemType Directory -Force -Path $SN | Out-Null"
scp -q "${SSH_OPTS[@]}" "$SCRIPT_DIR"/snippets/*.cs "$SCRIPT_DIR"/../2026-10-22-gameobject-count/go-breakdown.cs "$HOST:C:/moorestech-perf/snippets/"
echo "commit=$(remote "git -C G:\\GitHub\\moorestech rev-parse HEAD" | tr -d '\r')" > "$OUT"
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
remote "\$b='C:\\moorestech-perf\\baselines\\2026-10-22-world_1'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
remote "uloop control-play-mode --action Play --project-path $PROJECT" >/dev/null
for _ in $(seq 1 40); do r="$(run enter-world.cs)"; case "$r" in started*) break ;; esac; sleep 3; done
case "$r" in started*) ;; *) echo "ERROR: ローカル開始できませんでした: $r" >&2; run cleanup.cs; exit 3 ;; esac
# 定常（Renderer 数の変化が5秒で0.1%以内）を待ってから20秒置く
# Wait for the steady state (renderer count within 0.1% over 5 s), then settle 20 s
prev=-1; stable=0
for _ in $(seq 1 120); do
  sleep 5
  cur="$(run scene-stats.cs | python3 -c 'import json,sys
try: print(json.load(sys.stdin)["renderers"])
except Exception: print(-1)')"
  if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then stable=1; break; fi
  prev="$cur"
done
[ "$stable" = 1 ] || { echo "ERROR: 定常状態になりませんでした" >&2; run cleanup.cs; exit 3; }
sleep 20
echo "== go-breakdown" >> "$OUT"; run go-breakdown.cs >> "$OUT"
remote "Get-Content C:\\moorestech-perf\\go-breakdown.txt -ErrorAction SilentlyContinue" >> "$OUT" || true
for i in 1 2 3; do
  echo "== buildmode $i" >> "$OUT"
  run buildmode-enter.cs >> "$OUT"; sleep 3
  run buildmode-frames-collect.cs >> "$OUT"
  run buildmode-exit.cs >> "$OUT"; sleep 3
done
run cleanup.cs >/dev/null
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
echo "[buildmode] 保存: $OUT"
```

`go-breakdown.cs` は内訳を `C:\moorestech-perf\go-breakdown.txt` に書き出して `ok` を返すので、`Get-Content` で結果ファイルへ取り込む。

- [ ] **Step 2: windows-desktop を変更前（master）にして測る**

1. windows-desktop の `G:\GitHub\moorestech` を `origin/master` の detached HEAD にし、`moorestech_master` と `moorestech-client-private` を `.moorestech-external-revisions.json` のピンへ合わせる。Editor が起動していなければ `Start-ScheduledTask LaunchUnityBench` で起動し、`uloop compile --project-path G:\GitHub\moorestech\moorestech_client` が ErrorCount 0 になるまで待つ（初回はインポートで数分かかる）。
2. ワールド読み込み: `docs/performance/2026-11-08-world-load-remeasure/load-measure.sh timeline <出力先>/before-timeline` と `... subtree <出力先>/before-subtree` を実行する（各1回の読み込み）。
3. 定常時ベンチマーク: `docs/performance/benchmark/run-benchmark.sh before-placement-frame` を実行する（スニペットは Mac 側の最新が転送されるので、Step 1 の新スニペットも windows へ届く）。
4. 設置モードに入る瞬間と GameObject 数: Step 1b の `docs/performance/benchmark/run-buildmode-entry.sh before-placement-frame` を実行する（1回のワールド読み込みで、Transform 内訳1回と、設置モードの出入り3回を測る）。

- [ ] **Step 3: 同じ日に、windows-desktop を変更後（このブランチ）にして同じ手順で測る**

ブランチを push し、windows-desktop で `git fetch` → ブランチの HEAD を detached で checkout → compile ErrorCount 0 を確認して、Step 2 の 2〜4 を同じ順で行う（ラベルは `after-placement-frame`）。ブロック直下に `BlockPreviewBoundingBox` が無いことを `go-breakdown.cs` の結果で確認する。

- [ ] **Step 4: 記録を書く**

`docs/performance/2026-11-12-placement-preview-frame/README.md` に、`docs/performance/2026-11-08-world-load-remeasure/README.md` と同じ構成（結論・計測環境・計測方法・結果・推測の区別）で書く。結論には次の前後比較の表を必ず入れる。

| 指標 | 変更前 | 変更後 | 差 |
|---|---:|---:|---:|
| ワールド読み込み（開始→定常、秒） | | | |
| 最も重いフレーム（ms）と中身 | | | |
| 枠の生成にあたるフレーム（ms） | | | |
| 設置モードに入る瞬間の最大フレーム（ms）1回目 | | | |
| 同 2回目 | | | |
| 同 3回目 | | | |
| 設置モードに入る瞬間の合計（ms）1回目・2回目・3回目 | | | |
| Transform 総数（うち activeInHierarchy） | | | |
| 定常時 fps・PlayerLoop ms | | | |

表の注記として次の2点を書く。(1) 設置モードに入る1回目は、変更後はプールが空なのでプレハブの同期ロードと周囲20mぶんの枠の生成が入り、2回目以降は貸し出しだけになる。1回目と2回目以降を分けて読む。(2) 「枠の生成にあたるフレーム」は変更後には対応するフレームが無い（当たり判定はブロック生成のフレーム内で作る）。変更後の欄は「該当なし」とし、代わりにブロック生成のフレームの前後差を書く。

生データ（timeline・subtree・buildmode の出力）を同じフォルダに置く。`docs/performance/README.md` の表と、`docs/performance/benchmark/README.md` の結果一覧に行を足す。

- [ ] **Step 5: コミットする**

```bash
git add docs/performance
git commit -m "docs(perf): 設置プレビュー枠の GameObject 削減の前後で、読み込み時間・設置モードに入る瞬間・GameObject 数を測って記録する"
```

---

### Task 6: moores-code-review で全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が照準の判定経路・条件式・その評価時点に触れたら、Task 4 の録画テストを反映後のバイナリで再実施してから完了とする。

### Task 7: PR 作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR の説明には Task 5 の前後比較の表を貼る。最終報告で、ユーザーへ前後の数値（読み込み時間・設置モードに入る瞬間・Transform 総数）を報告する。

---

## 判断記録（ADR）

- 設計の裁定: `docs/adr/0099-placement-bounding-box-single-collider-and-pooled-edges.md`（範囲・当たり判定の形・辺のプール・プールの形）と `.decisions/2026-11-09-*.md` 3件
- 計測の裁定: 作業後に変更前と変更後を測って報告・記録する。対象は読み込み時間と設置モードに入る瞬間を含む。出所: ユーザー裁定 2026-11-12 原文「作業が終わったら変更前と変更後でどのぐらいの時間変わるのかをちゃんと計測して報告と記録して」「設置モードに入る瞬間もどのぐらい軽くなるのかも」
- 借りた枠の返却は、ブロックの破棄（`OnDestroyAsObservable`）で返す（ADR 0099 決定4を計画時に更新済み）。`BlockGameObjectDataStore.PlaceBlock` の置き換え経路が `DestroyBlock` を通らず `Destroy(oldBlock.gameObject)` するため、`DestroyBlock` だけでは置き換え時に枠が返らない。出所: agent前提（`BlockGameObjectDataStore.cs` の置き換え経路）
- プールはプレハブを初回の貸し出しで `AddressableLoader.LoadDefault`（同期）で1度だけ読む。最初に設置モードへ入るときに小さなヒッチが1回出うる。起動時に読む案より単純で、2回目以降は影響しない。出所: agent前提
- プールの配り方は `ClientDIContext` の static プロパティ（`BlockGameObject` が `ClientContext.VanillaApi` を引くのと同じ形）。`BlockGameObject.Initialize` の引数で渡す案は、`BlockGameObjectPrefabContainer.CreateBlock` から `BlockGameObjectDataStore` まで引数を通す変更が広がるため採らない。出所: agent前提（前例 `ClientDIContext.BlockGameObjectDataStore`）
- 照準の結果の「null なら地面」の読み替え（4か所に重複）は、どのみち書き換える行なので `PlaceSystemUtil.ToHitSurfaceKind` 1つにまとめる。新しい型は作らない。出所: agent前提（AGENTS.md「同種の条件分岐は一箇所へ揃える」。ユーザーに選択肢として出したが、現状説明の無い内部構造の質問だったため回答されず、ゲーム挙動に影響しない内部の書き方としてこちらで決めて宣言した 2026-11-12）
- 設計検査で挙がった細かい4件（照準失敗の理由を区別しない・当たり判定へ想定外のコライダーを渡す前提の専用テスト・枠を返す入口が3つ・プールの親の破棄の所有者）は手を入れない。いずれも今と同じ形か AGENTS.md の既知の制約で考慮不要で、挙動に影響しない。出所: agent前提（同上の経緯で宣言 2026-11-12）
- 貸し出す枠は、今のプレハブから面を除いた形（GameObject 25個: root 1・辺の親12・辺12）のまま使う。辺の親を畳んで13個にはしない。貸し出すのは周囲20mのぶんだけで、数を減らす効果は小さい。また、辺の伸ばし方（`SetBoundingBox` の親 scale の計算）を変えずに済む。出所: agent前提（user-simulator review の Warning を受けて 2026-11-12 に明記）
- 照準の out 引数は `BlockPreviewBoundingBoxSurface` から `PreviewSurfaceType?` に変える。本番コードで `BlockPreviewBoundingBoxSurface.BlockGameObject` を読む箇所は無く、使われているのは `PreviewSurfaceType` だけだったため。出所: agent前提（grep で確認）
- 当たり判定のコライダーは、ブロックのコライダー距離カリング（120m）の登録から外す。除外条件は `BlockBoundingBox` レイヤーとする。比較: (a) `IPreviewOnlyObject` 配下を一律に外す案（`BlockGameObjectColliderSetup.IsActiveAndNotPreview` と同じ役割判定）は、`InventoryConnectorView`・`GearConnectorView` などほかのプレビュー専用部品のコライダーまでカリング対象から外れ、今の挙動が変わるので採らない。(b) `BlockPlacementHitFaces` を型で名指しする案は、汎用のカリング層に設置照準の具体型を持ち込むので採らない。`BlockBoundingBox` レイヤーを使うのは旧枠のプレハブと新しい当たり判定だけで（grep で確認）、`RuntimeOutlineFactory.cs:123` に同じレイヤー除外の前例がある。出所: agent前提（設計検査 Phase 2 の指摘を受けて修正）
  - 外す理由: 新しい当たり判定はブロック生成と同時に作るので、外さないと120m より遠いブロックへの照準が当たらなくなり、ADR 0083 決定4（半径外のブロックの横にも置ける）が崩れる。旧枠は生成後のフレームで付いたため登録に入っていなかった（推測。根拠: 11/8 の Profiler frame 688）。出所: agent前提（今の挙動の維持）
- unityプレイ録画テスト: ランタイム挙動（設置モードの枠の表示・照準）に触れるので、既存の `build-preview-radius` シナリオを新構造に合わせて実行する（Task 4）。
- 聞き漏れチェック（moores-grill-with-docs §1.7）は、codex が無い環境のため Claude のサブエージェントで代替した（ユーザー指示「claudeでできないの」）。残った問い1件（当たり判定の形）はユーザー裁定済み。
