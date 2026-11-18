// シナリオ: チェスト→ベルト→空気清浄機にフィルター5個を流し、スロットが3で止まり残り2個が搬入元側に残ることを確かめる
// Scenario: feed five filters chest -> belt -> purifier and check the slot stops at three with two left upstream
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using UnityEngine;
return PlaytestRunner.Run("cleanroom-filter-auto-insert-cap", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 15f, "GameScreenに到達");
    await p.SetupFlatGround();
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);

    // 北向きにチェスト→ベルト→清浄機
    // Chest, belt and purifier northbound
    var chestBlock = await p.PlaceBlockDirect("チェスト", new Vector3Int(3, 33, 2), BlockDirection.North);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(3, 33, 3), BlockDirection.North);
    var filterBlock = await p.PlaceBlockDirect("クリーンルーム空気清浄機", new Vector3Int(3, 33, 4), BlockDirection.North);
    var filterItemId = PlaytestItemOps.ResolveItemId("クリーンルームフィルター");
    IOpenableInventory chestInventory = chestBlock.GetComponent<IOpenableBlockInventoryComponent>();
    IOpenableInventory filterInventory = filterBlock.GetComponent<IOpenableBlockInventoryComponent>();
    await p.RunOnServer("チェストにフィルター5個を入れる", () => chestInventory.SetItem(0, filterItemId, 5));

    // スロットが3になり、その後も増えない
    // The slot reaches three and stays there
    await p.Until(() => filterInventory.GetItem(0).Count == 3, 20f, "清浄機のスロットが3個になった");
    await p.WaitSeconds(8f);
    p.Assert(filterInventory.GetItem(0).Count == 3, $"8秒後もスロットは3個のまま(実際={filterInventory.GetItem(0).Count})");
    p.Assert(chestInventory.InventoryItems.Sum(i => i.Id == filterItemId ? i.Count : 0) <= 2, "チェストに残るのは2個以下（残りはベルト上で待つ）");
    await p.Screenshot("01-filter-capped-at-three");
});
