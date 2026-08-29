// 右短押しでの解除検証: 建築モード→右短押しで抜ける / 右ドラッグでは抜けない / インベントリ→パネル外右短押しで閉じる
// Right short press cancel probe: build mode exits on a short press, stays on a drag, inventory closes on a short press outside the panel
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("right-short-press-cancel", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // 開幕スキットはこのプレイテスト起動では再生されず(mode=none)Skipインテントが拒否されるため、GameScreen到達だけを待つ
    // The opening skit never starts on this playtest boot (mode=none) and rejects the skip intent, so wait only for GameScreen
    await p.WaitUiState(UIStateEnum.GameScreen, 30f);

    p.Note("建築モードへ入る");
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    await p.Hotbar.EnterBuildMode(0);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 5f);
    // 画面中央（パネル外）に照準してから操作する
    // Aim at the screen center (outside any panel) before the presses
    SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
    await UniTask.DelayFrame(3);

    p.Note("右ドラッグでは建築モードに留まる");
    await p.RightDrag(new Vector2(60f, 0f));
    await UniTask.DelayFrame(5);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "右ドラッグ後もPlaceBlock");
    await p.Screenshot("01-after-right-drag");

    p.Note("右短押しで建築モードを抜ける");
    await p.RightShortClick();
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "右短押しでGameScreen");
    await p.Screenshot("02-after-right-short-press");

    p.Note("インベントリをパネル外の右短押しで閉じる");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 5f);
    // 画面左上端はインベントリパネルの外
    // The top-left corner is outside the inventory panel
    SemanticInput.MouseMoveTo(new Vector2(8f, Screen.height - 8f));
    await UniTask.DelayFrame(3);
    await p.RightShortClick();
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "パネル外右短押しでインベントリが閉じる");
    await p.Screenshot("03-inventory-closed");

    p.Note("パネル上の右クリックは従来どおり効き、UIは閉じない");
    // グリッド中央のスロットに必ずアイテムが載るよう、全アイテムを2個ずつ入れて主インベントリを埋める
    // Fill the main inventory with two of every item so the grid's center slot certainly holds a stack
    foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
    {
        p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(itemId).Name, 2);
    }
    var localPlayerInventory = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ILocalPlayerInventory>();
    await p.Until(() => Enumerable.Range(0, localPlayerInventory.MainSlotCount).All(i => localPlayerInventory[i].Count != 0), 15f, "主インベントリが全スロット埋まる");

    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 5f);

    // スロットグリッド中央（＝パネル上）へカーソルを寄せてから右短押しする
    // Move the cursor onto the center of the slot grid (i.e. over the panel) before the short press
    await p.HoverWebUi("main-grid");
    await p.RightShortClick();
    await UniTask.DelayFrame(5);
    p.Assert(p.CurrentUiState == UIStateEnum.PlayerInventory, "パネル上の右短押しではインベントリが閉じない");

    // 半分取りがサーバーのgrabインベントリへ反映されたかで、右クリックが従来どおり効いていることを確認する
    // Confirm the right click still works by checking the split landed in the server-side grab inventory
    var playerInventoryDataStore = p.ServerService<IPlayerInventoryDataStore>();
    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
    var grabbed = false;
    var grabDeadline = Time.realtimeSinceStartup + 5f;
    while (Time.realtimeSinceStartup <= grabDeadline)
    {
        if (playerInventoryDataStore.GetInventoryData(playerId).GrabInventory.GetItem(0).Count != 0)
        {
            grabbed = true;
            break;
        }
        await UniTask.Yield();
    }
    p.Assert(grabbed, "パネル上の右クリックでアイテムがカーソルへ乗る（半分取り）");
    await p.Screenshot("04-right-click-on-slot");
});
