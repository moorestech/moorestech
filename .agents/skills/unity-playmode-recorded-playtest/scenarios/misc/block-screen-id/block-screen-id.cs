// マスタの画面IDへの移行前後で、Fキーから開く画面と貨物の転送モード操作を確認する
// Compare the screens opened with F and the cargo transfer control before and after the screen ID migration.
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Control;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using TransferMode = Game.Block.Blocks.TrainRail.TrainPlatformTransferComponent.TransferMode;

return PlaytestRunner.Run("block-screen-id", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 240f }, async p =>
{
    const int GroundY = 32;
    const int BlockZ = -20;
    const string LoadButton = "train-platform-mode-load";
    const string UnloadButton = "train-platform-mode-unload";
    const string SelectedClass = "mode-switch__option--selected";

    // 足場上に対象を一つずつ置き、岩で照準を遮られないようにする
    // Place each target on the scaffold and keep rocks from occluding the aim.
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "ゲーム画面とカメラが有効");
    PlaytestSetup.SetMapObjectsVisible(false);
    p.Note($"入力の疎通を確認する (focus={Application.isFocused}, keyboard={Keyboard.current.enabled}, mouse={Mouse.current.enabled})");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.UntilUiElement("inventory-sort", 10f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    await CheckScreen("木のチェスト", -22, 1, 0.4f, "chest-grid", "01-木のチェスト");
    await CheckScreen("石窯", -17, 3, 0.4f, "machine-section", "02-石窯");
    await CheckScreen("ベルトコンベア", -11, 1, 0.1f, "belt-throughput-section", "03-ベルトコンベア");
    await CheckScreen("直線歯車ベルトコンベア", -7, 1, 0.1f, "belt-throughput-section", "04-直線歯車ベルトコンベア");
    await CheckScreen("フィルター分岐器", -3, 1, 0.4f, "filter-splitter", "05-フィルター分岐器");
    await CheckScreen("電柱", 1, 1, 0.4f, "electric-pole-view", "06-電柱");

    // 切替ボタンを押し、選択表示とサーバーから届く状態の両方を確認する
    // Click the mode button and verify both its selection and the state received from the server.
    var platform = await InteractWith("貨物プラットフォーム", 6, 7, 0.4f);
    await p.Until(() => Mode() == TransferMode.LoadToTrain && IsSelected(LoadButton), 10f, "積み込みの表示と状態が揃う");
    await CheckBody("貨物プラットフォーム", "train-platform-view", "07-貨物プラットフォーム");
    p.Assert(!IsSelected(UnloadButton), "初期状態では荷降ろしは未選択");
    await p.ClickUi(UnloadButton);
    await p.Until(() => Mode() == TransferMode.UnloadToPlatform && IsSelected(UnloadButton) && !IsSelected(LoadButton), 10f, "荷降ろしの表示と状態が揃う");
    p.Assert(p.CurrentUiState == UIStateEnum.SubInventory && IsVisible("train-platform-view"), "荷降ろしを選んだ後も貨物プラットフォームの画面を表示");
    await p.Screenshot("08-貨物プラットフォーム-荷降ろし");
    await CloseScreen();

    // 照準を当てた土台にFを押してもゲーム画面のままになる
    // F leaves the game screen in place when the foundation is under the aim.
    await InteractWith("基本土台", 17, 5, 0.4f);
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "基本土台に照準を合わせてFを押しても画面は開かない");
    await p.Screenshot("09-基本土台");

    #region Internal

    async UniTask CheckScreen(string name, int x, int width, float aimHeight, string testId, string screenshot)
    {
        await InteractWith(name, x, width, aimHeight);
        await CheckBody(name, testId, screenshot);
        await CloseScreen();
    }

    async UniTask<BlockGameObject> InteractWith(string name, int x, int width, float aimHeight)
    {
        p.Note($"{name}を置き、照準を合わせてFを押す");
        var position = new Vector3Int(x, GroundY, BlockZ);
        await p.PlaceBlockDirect(name, position, BlockDirection.North);
        var block = await p.WaitBlockGameObject(position);
        await p.Until(() => !block.IsPlayingPlaceAnimation && block.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger && c.bounds.size != Vector3.zero), 10f, $"{name}の当たり判定が有効");

        // 設置面の手前に着地し、実際の照準レイが2m以内で対象に当たるまで待つ
        // Land in front of the block and wait for the live aim ray to hit it within two meters.
        var centerX = x + width / 2f;
        p.WarpPlayer(new Vector3(centerX, GroundY + 0.5f, BlockZ - 0.8f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - GroundY) < 0.1f, 8f, "足場に着地");
        SemanticInput.KeyDown(Key.LeftAlt);
        try
        {
            await p.AimAt(new Vector3(centerX, GroundY + aimHeight, BlockZ + 0.35f));
            await p.Until(() => HitsBlock(block), 5f, $"{name}に2m以内で照準が当たる");
            p.Assert(HitsBlock(block), $"{name}を2m以内で狙っている");
            p.Assert(!UiPointerHitTest.IsPointerOverAnyUi(), $"{name}を狙うポインタがUIで遮られていない");
            await p.PressInteract();
            await p.WaitSeconds(0.5f);
        }
        finally
        {
            SemanticInput.KeyUp(Key.LeftAlt);
        }
        return block;
    }

    bool HitsBlock(BlockGameObject block)
    {
        // 選定と同じ距離だけレイを伸ばし、到達距離はプレイヤーから測る
        // Use the selection ray length and measure reach from the player.
        var playerPosition = p.PlayerPosition;
        var rayDistance = Vector3.Distance(Camera.main.transform.position, playerPosition) + InteractOverlap.InteractDistance;
        var layerMask = LayerConst.BlockOnlyLayerMask | LayerConst.MapObjectOnlyLayerMask;
        return BlockClickDetectUtil.TryGetFrontmostSolidHit(layerMask, rayDistance, out var hit)
            && hit.collider.transform.IsChildOf(block.transform)
            && Vector3.Distance(playerPosition, hit.point) <= InteractOverlap.InteractDistance;
    }

    async UniTask CheckBody(string name, string testId, string screenshot)
    {
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.Until(() => IsVisible(testId), 10f, $"{name}の本文 {testId} が表示される");
        p.Assert(IsVisible(testId), $"{name}の本文 {testId} を表示");
        await p.Screenshot(screenshot);
    }

    async UniTask CloseScreen()
    {
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    // コンテナはクリックを受けないので、描画領域と祖先の表示状態を確認する
    // Containers need not accept clicks, so check their layout and the display state of their ancestors.
    bool IsVisible(string testId)
    {
        var element = Find(testId);
        if (element == null || element.worldBound.width <= 0f || element.worldBound.height <= 0f) return false;
        for (var parent = element; parent != null; parent = parent.parent)
        {
            if (!parent.visible || parent.resolvedStyle.display == DisplayStyle.None) return false;
        }
        return true;
    }

    VisualElement Find(string testId) => GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testId);
    bool IsSelected(string testId) => Find(testId)?.ClassListContains(SelectedClass) == true;
    TransferMode? Mode() => platform.GetStateDetail<TrainPlatformTransferStateDetail>(TrainPlatformTransferStateDetail.BlockStateDetailKey)?.Mode;

    #endregion
});
