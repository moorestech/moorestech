// ベルト張替え設置検証（ティア差し替え・実マスタ4ティア）: 分岐器手持ちの通常設置が化けないことを確かめ、
// 木の歯車ライン（直線3・上り1・直線1・分岐器1）へ搬送品を載せ、鉄ティアへ張替え→Ctrl+Z→満杯インベントリ拒否まで通す
// Belt replace (tier swap) scenario on the real 4-tier master: first prove a held splitter places itself,
// then build a wood gear line (3 straight, 1 up, 1 straight, 1 splitter) with transit items and drive
// replace to the iron tier, Ctrl+Z undo, and a full-inventory rejection
//
// 進行率（RemainingRate）はアサートしない。歯車ベルトは設置直後が停止中のため進捗は保存できず、
// 保証は「張替えで搬送品をロストしないこと」までである（CONTROLLER-RULINGS §11 / 2026-09-11 ユーザー裁定）
// Progress rate is deliberately NOT asserted: gear belts are stopped right after placement so progress cannot be
// preserved; the guarantee stops at "replace never loses transit items" (controller ruling §11)
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Inventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-replace-tier-via-ui", options, async p =>
{
    await p.SkipOpeningSkit();
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(3.5f, 33.5f, -1f));

    // 木・鉄の両ティアを解放しコストを付与する。張替えは新ブロックのコストを、Undoは旧ブロックのコストを消費する
    // Unlock and fund both tiers: the replace pays the new block's cost and the undo pays the old block's cost
    // 分岐器は1セット1個しか置けないため、Step 1の3個ぶんと張替え2回ぶんを別々に見込む
    // A splitter places only one block per cost set, so budget Step 1's three plus both replace runs
    await p.PrepareBlockForUiPlacement("鉄の歯車ベルトコンベア", 24);
    await p.PrepareBlockForUiPlacement("鉄の上り歯車ベルトコンベア", 6);
    await p.PrepareBlockForUiPlacement("鉄の歯車ベルトコンベア分岐機", 6);
    await p.PrepareBlockForUiPlacement("直線歯車ベルトコンベア", 12);
    await p.PrepareBlockForUiPlacement("上り歯車ベルトコンベア", 3);
    await p.PrepareBlockForUiPlacement("歯車コンベア分岐機", 3);

    var woodStraightId = PlaytestBlockOps.ResolveBlockId("直線歯車ベルトコンベア");
    var woodUpId = PlaytestBlockOps.ResolveBlockId("上り歯車ベルトコンベア");
    var woodSplitterId = PlaytestBlockOps.ResolveBlockId("歯車コンベア分岐機");
    var ironStraightId = PlaytestBlockOps.ResolveBlockId("鉄の歯車ベルトコンベア");
    var ironUpId = PlaytestBlockOps.ResolveBlockId("鉄の上り歯車ベルトコンベア");
    var ironSplitterId = PlaytestBlockOps.ResolveBlockId("鉄の歯車ベルトコンベア分岐機");

    // Step 1: 分岐器手持ちの通常設置（Task 4 で閉じた取り違えの実機確認）
    // Step 1: normal placement while holding a splitter (real-run check of the mix-up closed in Task 4)
    p.Note("Step 1: 分岐器を手持ちにして空き地へドラッグ設置し、直線に化けず坂も挿入されないことを見る");
    var splitterCells = new[] { new Vector3Int(5, 32, 2), new Vector3Int(5, 32, 3), new Vector3Int(5, 32, 4) };
    await p.DragPlaceViaUi("鉄の歯車ベルトコンベア分岐機", splitterCells[0], splitterCells[2]);
    await p.Until(() => AllBlocksAre(splitterCells, ironSplitterId), 15f, "Step1: 分岐器手持ちの通常設置が3セルとも分岐器BlockIdになり直線に化けない");
    p.Assert(NoBlockAboveOrBelow(splitterCells), "Step1: 分岐器設置で坂が自動挿入されない（3セルとも上下段が空）");
    await p.ExitToGameScreen();
    await p.WaitBlockGameObject(splitterCells[2]);
    await p.Screenshot("01-splitter-direct-place");

    // Step 2: 張替え対象の木のラインをサーバー直設置で用意する（検証対象は張替え操作なので準備は直設置）
    // Step 2: prepare the wood line by direct placement (the operation under test is the replace, not this setup)
    p.Note("Step 2: 木の歯車ライン（直線3・上り・直線・分岐器）をサーバー直設置する");
    p.PlaceBlockDirect("直線歯車ベルトコンベア", new Vector3Int(2, 32, 2), BlockDirection.North);
    p.PlaceBlockDirect("直線歯車ベルトコンベア", new Vector3Int(2, 32, 3), BlockDirection.North);
    p.PlaceBlockDirect("直線歯車ベルトコンベア", new Vector3Int(2, 32, 4), BlockDirection.North);
    p.PlaceBlockDirect("上り歯車ベルトコンベア", new Vector3Int(2, 32, 5), BlockDirection.North);
    p.PlaceBlockDirect("直線歯車ベルトコンベア", new Vector3Int(2, 33, 6), BlockDirection.North);
    p.PlaceBlockDirect("歯車コンベア分岐機", new Vector3Int(2, 33, 7), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 33, 7));

    var lineCells = new[]
    {
        new Vector3Int(2, 32, 2), new Vector3Int(2, 32, 3), new Vector3Int(2, 32, 4),
        new Vector3Int(2, 32, 5), new Vector3Int(2, 33, 6), new Vector3Int(2, 33, 7),
    };
    var woodLineIds = new[] { woodStraightId, woodStraightId, woodStraightId, woodUpId, woodStraightId, woodSplitterId };
    var ironLineIds = new[] { ironStraightId, ironStraightId, ironStraightId, ironUpId, ironStraightId, ironSplitterId };

    p.Note("搬送品2個を先頭ベルトと上りベルトへ投入する（歯車動力が無いのでライン上に留まる）");
    var itemId = PlaytestItemOps.ResolveItemId("鉄インゴット");
    InsertTransitItem(new Vector3Int(2, 32, 2));
    await p.WaitSeconds(0.5f);
    InsertTransitItem(new Vector3Int(2, 32, 5));
    await p.WaitSeconds(0.5f);
    await p.Until(() => CountItemsOnLine() == 2, 30f, "Step2: 搬送品2個がライン上にある");
    await p.Screenshot("02-wood-line");

    // Step 3: 手持ちを鉄の直線にして既設ラインへホバーし、張替えプレビュー（黄）を絵に残す
    // Step 3: hold the iron straight, hover the existing line and capture the yellow replace preview
    p.Note("Step 3: 鉄の歯車ベルトを選び既設ラインへホバーし、張替えプレビュー色（黄）を確認する");
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -1f));
    await p.OpenBuildMenuAndSelectBlock("鉄の歯車ベルトコンベア");

    // 起点は既設ベルトの天面（1段浮いたセル）を狙う。終点は浅い視線で1セル手前に着弾するため1つ先のセルを狙う
    // The origin aims at the belt's top face (the cell floated one step up); the end aims one cell further because the shallow view lands one cell short
    var fromAim = PlaytestUiOps.PlaceAimPoint("鉄の歯車ベルトコンベア", new Vector3Int(2, 33, 2), BlockDirection.North);
    var toAim = PlaytestUiOps.PlaceAimPoint("鉄の歯車ベルトコンベア", new Vector3Int(2, 34, 8), BlockDirection.North);
    await p.AimAt(fromAim);
    await p.Until(PreviewShowsReplaceColor, 10f, "Step3: 既設ベルトへのホバーでプレビューが張替え色（MaterialConst.ReplaceColor）になる");
    await p.Screenshot("03-replace-preview-yellow");

    // Step 4: 既設起点から終点まで張替えドラッグする
    // Step 4: drag the replace from the existing origin to the end of the line
    p.Note("Step 4: 既設ベルトの天面を起点に終点までドラッグし、ライン全体を鉄ティアへ張替える");
    await PlaytestUiOps.DragPlace(fromAim, toAim);
    await p.Until(() => LineMatches(ironLineIds), 15f, "Step4: 張替えで6セル全てが鉄ティアの同ロール・北向きになる");
    p.Assert(CountItemsOnLine() == 2, "Step4: 張替え後も搬送品2個がライン上に残る（ロストなし）");
    await p.WaitBlockGameObject(new Vector3Int(2, 33, 7));
    await p.Screenshot("04-iron-line");

    // Step 5: Ctrl+Zで逆張替えして木ティアへ戻す（クライアント単体テストはPlaceInfo組み立てまでしか見ていない）
    // Step 5: Ctrl+Z reverse-replaces back to the wood tier (client unit tests only cover the PlaceInfo build)
    p.Note("Step 5: Ctrl+Zで張替えをUndoし、元のBlockIdへ戻ることを見る");
    await p.WaitSeconds(1f);
    await PressCtrlZ();
    await p.Until(() => LineMatches(woodLineIds), 15f, "Step5: Undoで6セル全てが元の木ティアの同ロール・北向きへ戻る");
    p.Assert(CountItemsOnLine() == 2, "Step5: Undo後も搬送品2個がライン上に残る（ロストなし）");
    await p.WaitBlockGameObject(new Vector3Int(2, 33, 7));
    await p.Screenshot("05-undo-back-to-wood");

    // Step 6: 空きスロットを埋めて張替える。搬送品を抱えたセルは受け皿検査(HasRoomForReturnedItems)に落ちて既設のまま残る
    // Step 6: fill every empty slot and replace again; a cell holding transit items fails HasRoomForReturnedItems and stays as-is
    p.Note("Step 6: インベントリの空きスロットを全て埋めてから張替え、受け皿検査の挙動を見る");
    var filledSlots = FillEmptyInventorySlots("小石");
    p.Note($"空きスロットを{filledSlots}枠埋めた（満杯状態）");
    p.Assert(filledSlots > 0, "Step6: 空きスロットを実際に埋めて満杯状態を作れている");

    // 搬送品はライン上を流れるので、拒否されるセルはドラッグ直前の分布でしか確定しない
    // Transit items drift along the line, so only the distribution taken right before the drag pins down which cells must refuse
    var itemCountsBeforeReplace = SnapshotItemCounts();
    p.Assert(0 < CountHoldingCells(itemCountsBeforeReplace) && CountHoldingCells(itemCountsBeforeReplace) < lineCells.Length,
        "Step6: 搬送品を抱えたセルと抱えないセルが両方あり、セル単位の判定を観測できる");
    await PlaytestUiOps.DragPlace(fromAim, toAim);
    await p.Until(() => MatchesFullInventoryOutcome(itemCountsBeforeReplace), 15f, "Step6: 搬送品を抱えないセルだけが鉄ティアへ張替わり、抱えたセルは木ティアのまま残る");
    p.Note("満杯時の張替え結果: " + DescribeLine());

    // セルごとに肯定形で残す。まとめて拒否・1セルも送信されない退行はここで赤くなる
    // Record each cell positively; a batch-wide refusal or a drag that sent nothing turns these red
    for (var i = 0; i < lineCells.Length; i++)
    {
        var holdsTransitItem = 0 < itemCountsBeforeReplace[i];
        var expectedBlockId = holdsTransitItem ? woodLineIds[i] : ironLineIds[i];
        var block = p.GetBlock(lineCells[i]);
        p.Assert(block != null && block.BlockId == expectedBlockId,
            $"Step6: z{lineCells[i].z}は{(holdsTransitItem ? "搬送品を抱えるため木ティアのまま残る" : "鉄ティアへ張替わる")}");
    }
    p.Assert(CountItemsOnLine() == 2, "Step6: 満杯インベントリでの張替え試行でも搬送品がロストしない");
    await p.Screenshot("06-inventory-full-replace");

    #region Internal

    // 指定セル群が全て同じBlockIdか
    // Whether every given cell holds the same BlockId
    bool AllBlocksAre(Vector3Int[] cells, BlockId expectedBlockId)
    {
        foreach (var cell in cells)
        {
            var block = p.GetBlock(cell);
            if (block == null || block.BlockId != expectedBlockId) return false;
        }
        return true;
    }

    // 指定セル群の上下段が全て空か（坂の自動挿入が起きていないこと）
    // Whether every cell above and below the given cells is empty (no slope was auto-inserted)
    bool NoBlockAboveOrBelow(Vector3Int[] cells)
    {
        foreach (var cell in cells)
        {
            if (p.GetBlock(cell + Vector3Int.up) != null) return false;
            if (p.GetBlock(cell + Vector3Int.down) != null) return false;
        }
        return true;
    }

    // ホバー中のプレビューが張替え色で塗られているか。色は置換マテリアルのプロパティに載る
    // Whether the hovered preview is painted with the replace color, which lands on the replacement material's property
    bool PreviewShowsReplaceColor()
    {
        var previewController = UnityEngine.Object.FindFirstObjectByType<PlacementPreviewBlockGameObjectController>(FindObjectsInactive.Include);
        if (previewController == null || !previewController.IsActive) return false;
        if (!previewController.TryGetPreviewBlock(0, out var previewBlock)) return false;

        foreach (var previewRenderer in previewBlock.GetComponentsInChildren<Renderer>())
        {
            foreach (var material in previewRenderer.sharedMaterials)
            {
                if (material == null || !material.HasProperty(MaterialConst.PreviewColorPropertyName)) continue;
                if (material.GetColor(MaterialConst.PreviewColorPropertyName) == MaterialConst.ReplaceColor) return true;
            }
        }
        return false;
    }

    // ライン6セルが期待BlockId列と一致し、向きが全て北のままか
    // Whether the six line cells match the expected BlockId column and all keep the north direction
    bool LineMatches(BlockId[] expectedBlockIds)
    {
        for (var i = 0; i < lineCells.Length; i++)
        {
            var block = p.GetBlock(lineCells[i]);
            if (block == null || block.BlockId != expectedBlockIds[i]) return false;
            if (block.BlockPositionInfo.BlockDirection != BlockDirection.North) return false;
        }
        return true;
    }

    // ライン上の搬送品総数。進行率は見ずロストの有無だけを数える
    // Total transit items on the line; counts only losses, never the progress rate
    int CountItemsOnLine()
    {
        var total = 0;
        foreach (var cell in lineCells) total += CountItemsOnCell(cell);
        return total;
    }

    int CountItemsOnCell(Vector3Int cell)
    {
        var block = p.GetBlock(cell);
        if (block == null) return 0;

        var count = 0;
        foreach (var item in block.GetComponent<VanillaBeltConveyorComponent>().BeltConveyorItems)
        {
            if (item != null) count++;
        }
        return count;
    }

    // ドラッグ直前の搬送品分布。どのセルが受け皿検査に落ちるかはこの時点で決まる
    // The transit item distribution right before a drag; it decides which cells fail the receptacle check
    int[] SnapshotItemCounts()
    {
        var itemCounts = new int[lineCells.Length];
        for (var i = 0; i < lineCells.Length; i++) itemCounts[i] = CountItemsOnCell(lineCells[i]);
        return itemCounts;
    }

    int CountHoldingCells(int[] itemCounts)
    {
        var holdingCells = 0;
        foreach (var itemCount in itemCounts)
        {
            if (0 < itemCount) holdingCells++;
        }
        return holdingCells;
    }

    // 満杯時の期待形: 搬送品を抱えたセルは木のまま、抱えないセルは鉄へ変わる
    // The expected shape under a full inventory: cells holding transit items stay wood and the rest turn iron
    bool MatchesFullInventoryOutcome(int[] itemCountsBefore)
    {
        for (var i = 0; i < lineCells.Length; i++)
        {
            var block = p.GetBlock(lineCells[i]);
            if (block == null) return false;
            if (block.BlockId != (0 < itemCountsBefore[i] ? woodLineIds[i] : ironLineIds[i])) return false;
        }
        return true;
    }

    // セル毎のブロック名と搬送品数を1行に畳む（拒否されたセルを録画とTimelineから特定するため）
    // Folds each cell's block name and item count into one line so a refused cell is identifiable from the recording and timeline
    string DescribeLine()
    {
        var descriptions = new List<string>();
        foreach (var cell in lineCells)
        {
            var block = p.GetBlock(cell);
            descriptions.Add($"z{cell.z}={(block == null ? "null" : MasterHolder.BlockMaster.GetBlockMaster(block.BlockId).Name)}(items:{CountItemsOnCell(cell)})");
        }
        return string.Join(" / ", descriptions);
    }

    void InsertTransitItem(Vector3Int cell)
    {
        var belt = p.GetBlock(cell).GetComponent<VanillaBeltConveyorComponent>();
        belt.InsertItem(ServerContext.ItemStackFactory.Create(itemId, 1), InsertItemContext.Empty);
    }

    // 空きスロットへ1個ずつ別アイテムを置いて満杯にする。異種アイテムなので返却品はどのスロットにも入らない
    // Fill every empty slot with one unrelated item; refunds cannot merge into any slot because the item differs
    int FillEmptyInventorySlots(string fillerItemName)
    {
        var fillerItemId = PlaytestItemOps.ResolveItemId(fillerItemName);
        var playerInventory = GetPlayerInventory();
        var filled = 0;
        for (var slot = 0; slot < playerInventory.GetSlotSize(); slot++)
        {
            if (playerInventory.GetItem(slot).Count != 0) continue;
            playerInventory.SetItem(slot, fillerItemId, 1);
            filled++;
        }
        return filled;
    }

    IOpenableInventory GetPlayerInventory()
    {
        var playerId = Client.Game.InGame.Context.ClientContext.PlayerConnectionSetting.PlayerId;
        return ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
    }

    // Ctrl+Z注入。HybridInputはLeftCtrl(GetKey保持)+Z(GetKeyDown)を見る
    // Inject Ctrl+Z; HybridInput checks LeftCtrl held (GetKey) plus Z (GetKeyDown)
    async UniTask PressCtrlZ()
    {
        p.Note("Ctrl+Z注入: LeftCtrl押下→Z押下→Z解放→LeftCtrl解放");
        SemanticInput.KeyDown(Key.LeftCtrl);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyDown(Key.Z);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyUp(Key.Z);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyUp(Key.LeftCtrl);
        await UniTask.DelayFrame(3);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
