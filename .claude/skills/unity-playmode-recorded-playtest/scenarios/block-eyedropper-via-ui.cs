// ブロックスポイトE2E検証（IPlacementTarget版）: 通常プレイ中と配置モード中のミドルクリックで設置済みブロックをピックする
// 検証項目: GameScreenからPlaceBlock遷移とCurrentTarget反映、PlaceBlock中の持ち替え、West向き保持、地形ミス時の不変、3連ベルトの代表解決
// Block eyedropper E2E (IPlacementTarget edition): pick placed blocks with middle-click during normal play and placement mode.
// Verifies: GameScreen->PlaceBlock transition with CurrentTarget, target swap in PlaceBlock, West direction retention, no-op on bare terrain, and length-3 belt resolution.
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
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
    // ルートスコープでゲームプレイ系登録を持たないため不可）
    // Resolve client-side DI singletons via ClientDIContext (the root scope lacks gameplay registrations)
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var placeController = resolver.Resolve<PlaceSystemStateController>();
    var gameUnlockStateData = resolver.Resolve<IGameUnlockStateData>();
    var chestBlockId = PlaytestBlockOps.ResolveBlockId("木のチェスト");
    var beltBlockId = PlaytestBlockOps.ResolveBlockId("ベルトコンベア");

    // 現在ターゲットをBlockPlacementTargetとして読む（違う型/nullならnull）
    // Read the current target as BlockPlacementTarget (null when absent or another type)
    BlockPlacementTarget CurrentBlockTarget() => placeController.CurrentTarget as BlockPlacementTarget;

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
    // Aim at the block's "ClickCollider" so the Block-layer raycast hits it
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

    // 確認項目1: GameScreen中にNorth向きチェストをピックし、PlaceBlock遷移とCurrentTarget反映を検証する
    // Check item 1: pick a North-facing chest during GameScreen, then verify PlaceBlock transition and CurrentTarget
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "初期状態はGameScreen");
    p.PlaceBlockDirect("木のチェスト", posA, BlockDirection.North);
    await PickBlockAtAsync(posA);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "項目1: ピック成功でPlaceBlockへ遷移");
    p.Assert(CurrentBlockTarget() != null, "項目1: CurrentTargetがBlockPlacementTargetになる");
    p.Assert(CurrentBlockTarget()?.BlockId == chestBlockId, "項目1: 選択ブロックがチェストになる");
    await p.Screenshot("01-pick-in-gamescreen");

    // 確認項目3: West向きチェストをピックし、PickedDirectionが元ブロックと一致することを検証する
    // Check item 3: pick a West-facing chest and verify PickedDirection matches the source block
    p.PlaceBlockDirect("木のチェスト", posC, BlockDirection.West);
    await PickBlockAtAsync(posC);
    p.Assert(CurrentBlockTarget()?.BlockId == chestBlockId, "項目3: 選択ブロックはチェストのまま");
    p.Assert(CurrentBlockTarget()?.PickedDirection == BlockDirection.West, "項目3: 向きがWestで一致");
    await p.Screenshot("02-pick-west-direction");

    // 確認項目2: PlaceBlock中にベルトをピックし、画面遷移せずターゲットだけ持ち替わることを検証する
    // Check item 2: pick a belt while in PlaceBlock and verify only the target swaps without a state transition
    p.PlaceBlockDirect("ベルトコンベア", posB, BlockDirection.North);
    await PickBlockAtAsync(posB);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "項目2: PlaceBlockのまま");
    p.Assert(CurrentBlockTarget()?.BlockId == beltBlockId, "項目2: ターゲットがベルトへ持ち替わる");
    await p.Screenshot("03-pick-switch-selection");

    // 確認項目4: 地形のみをミドルクリックし、直前のターゲットとUI状態が不変であることを検証する
    // Check item 4: middle-click bare terrain and verify the prior target and UI state remain unchanged
    var beforeTarget = placeController.CurrentTarget;
    var beforeUiState = p.CurrentUiState;
    await p.AimAt(new Vector3(posD.x + 0.5f, 32f, posD.z + 0.5f));
    await MiddleClickAsync();
    await UniTask.DelayFrame(3);
    p.Assert(Equals(placeController.CurrentTarget, beforeTarget), "項目4: ターゲットが変化しない");
    p.Assert(p.CurrentUiState == beforeUiState, "項目4: UI状態が変化しない");
    await p.Screenshot("04-pick-empty-terrain-noop");

    // 確認項目5: 3連ベルトの隠しバリアントをピックし、代表ベルトIDへ解決されることを検証する
    // Check item 5: pick the hidden length-3 belt variant and verify it resolves to the representative belt ID
    p.PlaceBlockDirect("ベルトコンベア(3連)", posE, BlockDirection.North);
    await PickBlockAtAsync(posE);
    p.Assert(CurrentBlockTarget()?.BlockId == beltBlockId, "項目5: 隠しバリアントが代表ブロックへ解決される");
    await p.Screenshot("05-pick-hidden-belt-variant");
});
