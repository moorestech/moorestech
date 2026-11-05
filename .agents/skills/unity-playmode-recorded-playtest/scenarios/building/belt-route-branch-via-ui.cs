// 既存ベルトからの分岐（ADR 0097）の通し検証。本線を2クリックで置き、Ctrl+クリックで途中のベルトを分岐器へ置き換えて
// 横へ経路を出し、荷物が本線と分岐の両方へ届くこと、横から荷物を受けるベルトは分岐にできないことを確かめる
// End-to-end check of branching from an existing belt (ADR 0097): place a main line in two clicks, Ctrl+click a belt on it to
// replace it with a splitter and route sideways, then confirm items reach both ends and a side-fed belt refuses to branch
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-route-branch-via-ui", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { SpawnPosition = new Vector3(6f, 33.5f, -3f) });
    await p.SkipOpeningSkit();
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    // 足場は地形の山の中にあり、そのままでは設置物が録画に写らない
    // The scaffold sits inside a terrain mound, so placed blocks would not show on the recording
    p.HideMapObjects();
    p.SetOverlayPanels(false, false);

    // ---------------------------------------------------------------- 1. 本線
    p.Note("フェーズ1: 東向きの本線(2,4)→(10,4)を2クリックで置く");
    await p.RoutePlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 4), BlockDirection.East, new Vector3Int(10, 32, 4), BlockDirection.East);
    p.Assert(Enumerable.Range(2, 9).All(x => p.GetBlock(new Vector3Int(x, 32, 4)) != null), "本線9セルが置かれる");

    // ---------------------------------------------------------------- 2. 分岐
    p.Note("フェーズ2: Ctrl+クリックで(6,4)のベルトを分岐器に置き換え、右（南）へ(6,1)まで分岐する");
    await p.BranchPlaceViaUi("ベルトコンベア", new Vector3Int(6, 32, 4), true, new Vector3Int(6, 32, 1), BlockDirection.South);
    await p.Until(() => NameAt(new Vector3Int(6, 32, 4)) == "ベルトコンベア分岐器", 15f, "(6,4)が分岐器へ置き換わる");
    p.Assert(Enumerable.Range(1, 3).All(z => p.GetBlock(new Vector3Int(6, 32, z)) != null && ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(6, 32, z)) == BlockDirection.South), "分岐の3セルが南向きに置かれる");
    // ベルトの見た目は非同期に読み込まれるので、録画に写るまで終点の実体を待つ
    // Belt visuals load asynchronously, so wait for the end cell's object before it is filmed
    await p.WaitBlockGameObject(new Vector3Int(6, 32, 1));
    await p.ExitToGameScreen();
    await p.Screenshot("01-branch");

    // ---------------------------------------------------------------- 3. 両方へ流れる
    p.Note("フェーズ3: 本線の手前から荷物を流すと、本線の先と分岐の先の両方へ届く");
    var source = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(1, 32, 4), BlockDirection.East);
    var mainSink = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(11, 32, 4), BlockDirection.East);
    var branchSink = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(6, 32, 0), BlockDirection.South);
    var itemId = PlaytestItemOps.ResolveItemId("鉄インゴット");
    await p.RunOnServer("本線手前のチェストへ鉄インゴット6個", () => source.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ServerContext.ItemStackFactory.Create(itemId, 6)));
    await p.Until(() => 1 <= CountOf(mainSink, itemId) && 1 <= CountOf(branchSink, itemId), 60f, "本線の先と分岐の先の両方へ荷物が届く");
    await p.Screenshot("02-flow-both");

    // ---------------------------------------------------------------- 4. 分岐できないベルト
    p.Note("フェーズ4: 横から荷物を受けているベルトはCtrl+クリックしても分岐にできない（赤と理由）");
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(8, 32, 5), BlockDirection.South);
    await p.WaitBlockGameObject(new Vector3Int(8, 32, 5));
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    SemanticInput.KeyDown(Key.LeftCtrl);
    await p.AimAt(new Vector3(8.5f, 32.5f, 4.5f));
    await UniTask.DelayFrame(5);
    p.Note($"[Ctrl+照準中] tooltip = {Snapshot()}");
    p.Assert(HasKey("ui.tooltip.placeBeltBranchSideInputOccupied"), "横から受けているベルトは分岐不可の理由行が出る");
    await p.Screenshot("03-branch-refused");
    SemanticInput.KeyUp(Key.LeftCtrl);
    await UniTask.DelayFrame(2);
    await p.ExitToGameScreen();

    #region Internal

    string NameAt(Vector3Int position)
    {
        var block = p.GetBlock(position);
        return block == null ? "" : block.BlockMasterElement.Name;
    }

    int CountOf(IBlock chest, ItemId targetItemId)
    {
        var inventory = chest.GetComponent<IOpenableBlockInventoryComponent>();
        var count = 0;
        for (var i = 0; i < inventory.GetSlotSize(); i++)
            if (inventory.GetItem(i).Id == targetItemId) count += inventory.GetItem(i).Count;
        return count;
    }

    IMouseCursorTooltip Tooltip()
    {
        return ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>();
    }

    string Snapshot()
    {
        var presentation = Tooltip().GetPresentation();
        if (!presentation.Visible) return "(hidden)";
        return string.Join(" / ", presentation.Lines.Select(line => line.Key.Key));
    }

    bool HasKey(string key)
    {
        var presentation = Tooltip().GetPresentation();
        return presentation.Visible && presentation.Lines.Any(line => line.Key.Key == key);
    }

    #endregion
});
