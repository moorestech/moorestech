// 電線接続E2E検証(UI経路): ビルドメニューの接続ツール「電線接続」を選択し2本の電柱をクリック結線する
// 検証項目: 結線前は独立セグメント、接続ツール選択でPlaceBlock遷移、起点→接続クリックでセグメント統合、電柱ブロック選択中の電力範囲表示
// Electric wire connect E2E (UI route): select the "電線接続" connect tool from the build menu and click-connect two poles.
// Verifies: isolated segments before, PlaceBlock transition via tool slot, origin->target click merges segments, and energized-range boxes while a pole block is selected.
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.Electric;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.EnergySystem;
using Game.UnlockState;
using Mooresmaster.Model.PlaceSystemModule;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("electric-wire-connect-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(5f, 33.5f, 6f));
    await p.WaitSeconds(0.5f);

    // 電線アイテムはツールの動作前提（クライアント在庫を自動選択するため同期込みで付与）
    // Holding wire items is a tool precondition (auto-selected from the client inventory, so grant with sync)
    await p.GiveItemToHotbar(0, "電線", 64);

    // 2本の電柱を直設置（距離6 ≤ maxWireLength 12）
    // Place two poles directly (distance 6 <= maxWireLength 12)
    var poleAPos = new Vector3Int(2, 32, 2);
    var poleBPos = new Vector3Int(8, 32, 2);
    p.PlaceBlockDirect("電柱", poleAPos, BlockDirection.North);
    p.PlaceBlockDirect("電柱", poleBPos, BlockDirection.North);
    await p.WaitBlockGameObject(poleAPos);
    await p.WaitBlockGameObject(poleBPos);

    // 結線前: 2本は独立セグメント（直置きでは自動結線されない）
    // Before connecting: the two poles are isolated segments (direct placement never auto-connects)
    var wireDatastore = p.ServerService<IElectricWireNetworkDatastore>();
    BlockInstanceId InstanceId(Vector3Int pos) => p.GetBlock(pos).BlockInstanceId;
    bool SameSegment() => wireDatastore.TryGetEnergySegment(InstanceId(poleAPos), out var segment) && segment.EnergyTransformers.ContainsKey(InstanceId(poleBPos));
    p.Assert(wireDatastore.SegmentCount >= 2, $"結線前はセグメント2以上 実際:{wireDatastore.SegmentCount}");
    p.Assert(!SameSegment(), "結線前は2本が別セグメント");
    await p.Screenshot("01-isolated-poles");

    // ビルドメニューから接続ツール「電線接続」をアイコン一致で選択する
    // Select the "電線接続" connect tool slot from the build menu by icon identity
    var wireTool = MasterHolder.PlaceSystemMaster.PlaceSystem.Data.First(e => e.PlaceMode == PlaceSystemMasterElement.PlaceModeConst.ElectricWireConnect);
    var wireToolIcon = ClientContext.ItemImageContainer.GetItemView(wireTool.IconItemGuid.Value);
    await OpenBuildMenuAndClickIconSlot(wireToolIcon, "02-menu-wire-tool");

    // 起点電柱→接続先電柱の順にClickColliderへ照準しクリック結線する
    // Aim at each pole's ClickCollider and click: first sets the origin, second sends the connect request
    await AimAtBlockAsync(poleAPos);
    await p.ClickPlace();
    await p.WaitSeconds(0.3f);
    await AimAtBlockAsync(poleBPos);
    await p.ClickPlace();

    // サーバー側でセグメントが統合されるまで待つ
    // Wait until the server merges the two segments into one
    await p.Until(SameSegment, 15f, "クリック結線で2本が同一セグメントになる");
    await p.Screenshot("03-connected");

    // 電力範囲表示: 電柱ブロックをメニュー選択中のみEnergizedRangeObjectが既存電柱位置に出る
    // Energized range: EnergizedRangeObject boxes appear at existing poles only while a pole block is selected
    await p.ExitToGameScreen();
    p.UnlockBlock("電柱");
    var unlockStateData = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IGameUnlockStateData>();
    var poleGuid = MasterHolder.BlockMaster.GetBlockMaster(PlaytestBlockOps.ResolveBlockId("電柱")).BlockGuid;
    await p.Until(() => unlockStateData.BlockUnlockStateInfos.TryGetValue(poleGuid, out var info) && info.IsUnlocked, 10f, "電柱アンロックのクライアント同期");
    await p.OpenBuildMenuAndSelectBlock("電柱");
    await p.Until(() => UnityEngine.Object.FindObjectsByType<EnergizedRangeObject>(FindObjectsSortMode.None).Length >= 2, 10f, "電柱選択中に電力範囲ボックスが2つ以上表示される");
    await p.Screenshot("04-energized-range");
    await p.ExitToGameScreen();

    #region Internal

    // 指定座標のBlockGameObjectの"ClickCollider"中心へ照準する（装飾サブメッシュ回避）
    // Aim at the block's "ClickCollider" center (avoids decorative sub-meshes)
    async UniTask AimAtBlockAsync(Vector3Int blockPos)
    {
        var blockGo = await p.WaitBlockGameObject(blockPos);
        var collider = blockGo.GetComponentsInChildren<Collider>(true).First(c => c.name == "ClickCollider");
        await p.AimAt(collider.bounds.center);
        await UniTask.DelayFrame(3);
    }

    // アイコン付きスロットをItemViewData参照一致で特定しクリックする（BP再構築レースはリトライで吸収）
    // Find an icon slot by ItemViewData reference identity and click it (retry absorbs the async rebuild race)
    async UniTask OpenBuildMenuAndClickIconSlot(Client.Mod.Texture.ItemViewData icon, string screenshotName)
    {
        for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
        {
            var openKey = p.CurrentUiState == UIStateEnum.PlaceBlock ? UnityEngine.InputSystem.Key.Tab : UnityEngine.InputSystem.Key.B;
            await p.PressKey(openKey);
            var openDeadline = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < openDeadline && p.CurrentUiState != UIStateEnum.BuildMenu) await UniTask.DelayFrame(5);
        }
        p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, "ビルドメニューが開く (電線接続)");

        var screenshotTaken = false;
        var clickDeadline = Time.realtimeSinceStartup + 20f;
        while (p.CurrentUiState != UIStateEnum.PlaceBlock && Time.realtimeSinceStartup < clickDeadline)
        {
            var slot = FindIconSlot(icon);
            if (slot != null && !screenshotTaken && screenshotName != null)
            {
                await p.Screenshot(screenshotName);
                screenshotTaken = true;
                slot = FindIconSlot(icon);
            }
            if (slot != null) ClickUi(slot.GetComponentInChildren<CommonSlotView>(true).gameObject);
            await UniTask.DelayFrame(10);
        }
        p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "電線接続スロット選択でPlaceBlockへ遷移");
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.6f));
    }

    ItemSlotView FindIconSlot(Client.Mod.Texture.ItemViewData icon)
    {
        var buildMenuView = UnityEngine.Object.FindFirstObjectByType<BuildMenuView>(FindObjectsInactive.Include);
        if (buildMenuView == null || !buildMenuView.gameObject.activeInHierarchy) return null;
        return buildMenuView.GetComponentsInChildren<ItemSlotView>(true).FirstOrDefault(s => ReferenceEquals(s.ItemViewData, icon));
    }

    void ClickUi(GameObject target)
    {
        // EventSystem直叩き（OSカーソル非依存）
        // Direct EventSystem execution (OS-cursor independent)
        var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerClickHandler);
    }

    #endregion
});
