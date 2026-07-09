// ブロックスポイトE2E検証: 通常プレイ中と配置モード中のミドルクリックで設置済みブロックをピックする
// 検証項目: GameScreenからPlaceBlock遷移と選択反映、PlaceBlock中の選択切替、West向き保持、地形ミス時の不変、3連ベルトの代表解決
// Block eyedropper E2E: pick placed blocks with middle-click during normal play and placement mode.
// Verifies: GameScreen->PlaceBlock transition with selection, selection swap in PlaceBlock, West direction retention, no-op on bare terrain, and length-3 belt resolution.
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Game.UnlockState;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using System.Linq;
using UnityEngine;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("block-eyedropper-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(0f, 33.5f, 0f));
    await p.WaitSeconds(0.5f);

    // クライアント側DIシングルトンはClientDIContext経由で取得する（MainGameStarter.Containerは
    // ルートスコープでゲームプレイ系登録を持たないため不可。StartGame()内で別ビルドされた
    // resolverがClientDIContext.DIContainer.DIContainerResolverへ保持される）
    // Resolve client-side DI singletons via ClientDIContext (MainGameStarter.Container is the root
    // scope and lacks gameplay registrations; the resolver built inside StartGame() is what's
    // exposed through ClientDIContext.DIContainer.DIContainerResolver)
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var placementSelection = resolver.Resolve<PlacementSelection>();
    var gameUnlockStateData = resolver.Resolve<IGameUnlockStateData>();
    var chestBlockId = PlaytestBlockOps.ResolveBlockId("木のチェスト");
    var beltBlockId = PlaytestBlockOps.ResolveBlockId("ベルトコンベア");

    // 通常プレイ・配置モード共通のミドルクリック入力をSemanticInputで注入する
    // Inject shared middle-click input for both normal play and placement mode via SemanticInput
    async UniTask MiddleClickAsync()
    {
        SemanticInput.MouseButtonDown(2);
        await UniTask.DelayFrame(2);
        SemanticInput.MouseButtonUp(2);
        await UniTask.DelayFrame(2);
    }

    // 指定座標のBlockGameObjectの"ClickCollider"を照準し、BlockレイヤーへのRaycastに当てる
    // GetComponentInChildren<Collider>()は子階層先頭のコライダーを返すため、ベルト等では
    // 装飾用サブメッシュ（例: Cube_2）を拾ってしまい実際の判定面から外れる。
    // "ClickCollider"という名前のコライダーが全ブロック共通の本来の判定対象。
    // Aim at the block's "ClickCollider" so the Block-layer raycast hits it.
    // GetComponentInChildren<Collider>() returns the first child in hierarchy order, which for
    // belts picks up a decorative sub-mesh (e.g. Cube_2) offset from the real hit surface.
    // The collider named "ClickCollider" is the actual intended hit target across all blocks.
    async UniTask PickBlockAtAsync(Vector3Int blockPos)
    {
        var blockGo = await p.WaitBlockGameObject(blockPos);
        var collider = blockGo.GetComponentsInChildren<Collider>().First(c => c.name == "ClickCollider");
        await p.AimAt(collider.bounds.center);
        await MiddleClickAsync();
        await UniTask.DelayFrame(3);
    }

    // カメラの現在向きからXZ平面上の配置座標を決め、シーン依存の初期向きを吸収する
    // Derive XZ placement coordinates from the current camera facing to absorb scene-dependent startup orientation
    var cam = Camera.main;
    var forward = cam.transform.forward;
    forward.y = 0f;
    forward = forward.normalized;
    var right = cam.transform.right;
    right.y = 0f;
    right = right.normalized;
    Vector3Int GroundPoint(float fwd, float rgt) =>
        new Vector3Int(
            Mathf.RoundToInt(cam.transform.position.x + forward.x * fwd + right.x * rgt),
            32,
            Mathf.RoundToInt(cam.transform.position.z + forward.z * fwd + right.z * rgt));

    // 各確認項目の設置点を分離し、地形のみの検証点にはブロックを置かない
    // Keep each check's placement point separated, and leave the bare-terrain point empty
    var posA = GroundPoint(4f, -6f);
    var posC = GroundPoint(4f, -2f);
    var posB = GroundPoint(4f, 2f);
    var posD = GroundPoint(7f, -6f);
    var posE = GroundPoint(4f, 6f);

    // サーバー側アンロック後、クライアント側ミラーへ反映されるまで条件待機する
    // After server-side unlock, wait until the client-side mirror reflects it
    p.UnlockBlock("木のチェスト");
    p.UnlockBlock("ベルトコンベア");
    var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(chestBlockId).BlockGuid;
    var beltGuid = MasterHolder.BlockMaster.GetBlockMaster(beltBlockId).BlockGuid;
    await p.Until(() => gameUnlockStateData.BlockUnlockStateInfos.TryGetValue(chestGuid, out var ci) && ci.IsUnlocked, 10f, "チェストのアンロック同期");
    await p.Until(() => gameUnlockStateData.BlockUnlockStateInfos.TryGetValue(beltGuid, out var bi) && bi.IsUnlocked, 10f, "ベルトのアンロック同期");

    // 確認項目1: GameScreen中にNorth向きチェストをピックし、PlaceBlock遷移と選択反映を検証する
    // Check item 1: pick a North-facing chest during GameScreen, then verify PlaceBlock transition and selection
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "初期状態はGameScreen");
    p.PlaceBlockDirect("木のチェスト", posA, BlockDirection.North);
    await PickBlockAtAsync(posA);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "項目1: ピック成功でPlaceBlockへ遷移");
    p.Assert(placementSelection.SelectionType == PlacementSelectionType.Block, "項目1: 選択種別がBlockになる");
    p.Assert(placementSelection.SelectedBlockId == chestBlockId, "項目1: 選択ブロックがチェストになる");
    await p.Screenshot("01-pick-in-gamescreen");

    // 確認項目3: West向きチェストをピックし、選択向きが元ブロックと一致することを検証する
    // Check item 3: pick a West-facing chest and verify the selected direction matches the source block
    p.PlaceBlockDirect("木のチェスト", posC, BlockDirection.West);
    await PickBlockAtAsync(posC);
    p.Assert(placementSelection.SelectedBlockId == chestBlockId, "項目3: 選択ブロックはチェストのまま");
    p.Assert(placementSelection.SelectedBlockDirection == BlockDirection.West, "項目3: 向きがWestで一致");
    await p.Screenshot("02-pick-west-direction");

    // 確認項目2: PlaceBlock中にベルトをピックし、画面遷移せず選択だけ切り替わることを検証する
    // Check item 2: pick a belt while in PlaceBlock and verify only the selection swaps without a state transition
    p.PlaceBlockDirect("ベルトコンベア", posB, BlockDirection.North);
    await PickBlockAtAsync(posB);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "項目2: PlaceBlockのまま");
    p.Assert(placementSelection.SelectedBlockId == beltBlockId, "項目2: 選択がベルトへ切り替わる");
    await p.Screenshot("03-pick-switch-selection");

    // 確認項目4: 地形のみをミドルクリックし、直前の選択とUI状態が不変であることを検証する
    // Check item 4: middle-click bare terrain and verify the prior selection and UI state remain unchanged
    var beforeBlockId = placementSelection.SelectedBlockId;
    var beforeDirection = placementSelection.SelectedBlockDirection;
    var beforeUiState = p.CurrentUiState;
    await p.AimAt(new Vector3(posD.x + 0.5f, 32f, posD.z + 0.5f));
    await MiddleClickAsync();
    await UniTask.DelayFrame(3);
    p.Assert(placementSelection.SelectedBlockId == beforeBlockId, "項目4: 選択ブロックIDが変化しない");
    p.Assert(placementSelection.SelectedBlockDirection == beforeDirection, "項目4: 選択向きが変化しない");
    p.Assert(p.CurrentUiState == beforeUiState, "項目4: UI状態が変化しない");
    await p.Screenshot("04-pick-empty-terrain-noop");

    // 確認項目5: 3連ベルトの隠しバリアントをピックし、代表ベルトIDへ解決されることを検証する
    // Check item 5: pick the hidden length-3 belt variant and verify it resolves to the representative belt ID
    p.PlaceBlockDirect("ベルトコンベア(3連)", posE, BlockDirection.North);
    await PickBlockAtAsync(posE);
    p.Assert(placementSelection.SelectedBlockId == beltBlockId, "項目5: 隠しバリアントが代表ブロックへ解決される");
    await p.Screenshot("05-pick-hidden-belt-variant");
});
