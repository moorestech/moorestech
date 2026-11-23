// グラフェンケーブルを選んだ、材料充足・積荷空の発射台で、所持ケーブルのShift+クリックがケーブル枠へ一括移動し、受け付けない枠への通常配置は拒否後に持物・積荷表示がサーバーと一致することを確かめる
// On a pad with graphene cable selected, full materials and empty cargo, Shift-click moves held cables into the cable slot, and a plain drop on a refusing slot leaves the local view equal to the server after the rejection
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

// 物理機器の無入力イベントが注入したShiftを上書きしないよう、仮想機器だけで入力する（終了時に戻す）
// Drive input through virtual devices only so idle physical events never override the injected Shift (restored at the end)
InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
foreach (var device in InputSystem.devices.ToArray())
    if (device is Keyboard || device is Mouse) InputSystem.DisableDevice(device);
var virtualKeyboard = InputSystem.AddDevice<Keyboard>("CargoShiftKeyboard");
var virtualMouse = InputSystem.AddDevice<Mouse>("CargoShiftMouse");
SemanticInput.ReleaseAllKeys();

var padPos = new Vector3Int(0, 32, 0);
return PlaytestRunner.Run("tier9-4b-rocket-cargo-shift-reject", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 180f }, async p =>
{
    // 仮想機器とキー解放の反映を待つ
    // Let the virtual devices and the key release settle in the input system before any operation
    await UniTask.DelayFrame(3);
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen に戻った");
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);

    // 発射台に材料だけをマスタ量積み、ケーブル1000本と鋼鉄板10枚を持たせる
    // Load only the master material counts onto the pad and hand the player 1000 cables and 10 steel plates
    var pad = await p.PlaceBlockDirect("ロケット発射台", padPos, BlockDirection.North);
    var param = (RocketLaunchPadBlockParam)pad.BlockMasterElement.BlockParam;
    var padInventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
    // 未選択の発射台は何も受け取らないので、先にグラフェンケーブルを解放して積荷に選ぶ（ADR 0106 の 2。主題は Shift なので選択はサーバーで直接）
    // An unselected pad accepts nothing, so unlock graphene cable and select it first (ADR 0106 §2; the subject is Shift, so select on the server directly)
    var cableId = PlaytestItemOps.ResolveItemId("グラフェンケーブル");
    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
    var selection = OrbitalCargoSelectionResult.Locked;
    await p.RunOnServer("グラフェンケーブルを解放し、発射台の積荷に選ぶ", () =>
    {
        ServerContext.GetService<IGameUnlockStateDataController>().UnlockItem(cableId);
        var refund = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
        selection = pad.GetComponent<IOrbitalCargoSelectorComponent>().SelectCargo(cableId, refund);
    });
    p.Assert(selection == OrbitalCargoSelectionResult.Success, $"グラフェンケーブルを選んだ result={selection}");
    await p.RunOnServer("材料をマスタ量だけ積む", () => { foreach (var m in param.LaunchMaterials) padInventory.InsertItem(MasterHolder.ItemMaster.GetItemId(m.ItemGuid), m.Count); });
    var plateId = PlaytestItemOps.ResolveItemId("鋼鉄板");
    await p.GiveItemDirect("グラフェンケーブル", 1000);
    await p.GiveItemDirect("鋼鉄板", 10);
    var local = ClientDIContext.DIContainer.DIContainerResolver.Resolve<LocalPlayerInventoryController>();
    var padObject = await p.WaitBlockGameObject(padPos);
    await p.Until(() => !padObject.IsPlayingPlaceAnimation, 30f, "発射台の設置演出が終わった");
    var cableSlot = -1;
    var plateSlot = -1;
    await p.Until(() => (cableSlot = FindLocal(cableId)) >= 0 && (plateSlot = FindLocal(plateId)) >= 0, 10f, "ケーブルと鋼鉄板が持物に届いた");
    var cableTargets = new List<int>();
    await p.RunOnServer("ケーブルを受け付ける枠を読む", () => { for (var i = 0; i < padInventory.GetSlotSize(); i++) if (padInventory.IsAllowedToPlace(i, ServerContext.ItemStackFactory.Create(cableId, 1000))) cableTargets.Add(i); });
    p.Assert(cableTargets.Count == 1, $"ケーブル枠はただ1つ: [{string.Join(",", cableTargets)}]");
    await OpenPad();
    await p.Screenshot("01-before-shift");

    // Shift+クリックはケーブル枠へ一括で入り、表示もサーバーと一致する
    // Shift-click lands the whole stack in the cable slot and the view matches the server
    p.Note("所持ケーブル1000本をShift+クリックで発射台へ送る");
    await p.HoverUi("main-slot-" + cableSlot);
    SemanticInput.KeyDown(Key.LeftShift);
    await UniTask.DelayFrame(3);
    p.Assert(Keyboard.current.shiftKey.isPressed, "Shiftの押下が入力系に届いた");
    await SemanticInput.Click();
    SemanticInput.KeyUp(Key.LeftShift);
    await p.WaitSeconds(1.5f);
    var afterShift = await ReadServer();
    p.Note($"Shift後 server main={afterShift.Main[cableSlot]} pad={Dump(afterShift.Pad)} / local main={local.LocalPlayerInventory[cableSlot]} pad={Dump(LocalPad())}");
    p.Assert(afterShift.Pad[cableTargets[0]].Id == cableId && afterShift.Pad[cableTargets[0]].Count == 1000 && afterShift.Main[cableSlot].Count == 0, "ケーブル1000本がケーブル枠へ一括移動した");
    AssertLocalMatches(afterShift, "Shift後");
    await p.Screenshot("02-after-shift");

    // 受け付けない枠へ鋼鉄板を通常配置すると拒否され、表示はサーバーの在庫へ戻る
    // A plain drop of steel plates on a refusing slot is rejected and the view returns to the server stock
    // 選んだ後の画面は資材と選んだ積荷の枠だけを出すので、筐体素材の入った資材の1枠目を使う（選んでいない積荷の枠は画面に無い）
    // The selected screen shows only materials and the selected payload slot, so use the first material slot holding hull material (unselected payload slots are not on screen)
    var refusingSlot = 0;
    var heldBefore = afterShift.Pad[refusingSlot];
    p.Note($"鋼鉄板を掴み、受け付けない資材枠slot{refusingSlot}（{heldBefore.Id}x{heldBefore.Count}）へ置く");
    await p.ClickUi("main-slot-" + plateSlot);
    await p.WaitSeconds(0.5f);
    await p.ClickUi("rocket-launch-pad-material-slots-slot-" + refusingSlot);
    await p.WaitSeconds(1.5f);
    var afterReject = await ReadServer();
    p.Note($"拒否後 server grab={afterReject.Grab} pad={Dump(afterReject.Pad)} / local grab={local.GrabInventory} pad={Dump(LocalPad())}");
    p.Assert(afterReject.Pad[refusingSlot].Id == heldBefore.Id && afterReject.Pad[refusingSlot].Count == heldBefore.Count, "サーバーは受け付けない枠を書き換えていない");
    p.Assert(local.GrabInventory.Id == afterReject.Grab.Id && local.GrabInventory.Count == afterReject.Grab.Count, $"拒否後の掴み表示がサーバーと一致: local={local.GrabInventory} server={afterReject.Grab}");
    AssertLocalMatches(afterReject, "拒否後");
    await p.Screenshot("03-after-reject");

    foreach (var device in InputSystem.devices.ToArray()) if (device is Keyboard || device is Mouse) InputSystem.EnableDevice(device);
    InputSystem.RemoveDevice(virtualKeyboard);
    InputSystem.RemoveDevice(virtualMouse);

    #region Internal

    int FindLocal(ItemId itemId)
    {
        for (var i = 0; i < local.LocalPlayerInventory.MainSlotCount; i++) if (local.LocalPlayerInventory[i].Id == itemId) return i;
        return -1;
    }

    IItemStack[] LocalPad() => Enumerable.Range(0, padInventory.GetSlotSize()).Select(i => local.LocalPlayerInventory[local.LocalPlayerInventory.MainSlotCount + i]).ToArray();

    string Dump(IEnumerable<IItemStack> stacks) => string.Join(",", stacks.Select((s, i) => $"{i}:{s.Id}x{s.Count}").Where(e => !e.EndsWith("x0")));

    // 持物・積荷の表示がサーバーの実在庫と1枠ずつ一致すること
    // Every player and pad slot shown locally equals the server's actual stock
    void AssertLocalMatches((IItemStack[] Main, IItemStack Grab, IItemStack[] Pad) server, string when)
    {
        var localPad = LocalPad();
        var mainDiff = Enumerable.Range(0, server.Main.Length).Where(i => !Same(local.LocalPlayerInventory[i], server.Main[i])).ToList();
        var padDiff = Enumerable.Range(0, server.Pad.Length).Where(i => !Same(localPad[i], server.Pad[i])).ToList();
        p.Assert(mainDiff.Count == 0, $"{when}: 持物表示がサーバーと一致 差分slot=[{string.Join(",", mainDiff)}]");
        p.Assert(padDiff.Count == 0, $"{when}: 積荷表示がサーバーと一致 差分slot=[{string.Join(",", padDiff)}]");
    }

    bool Same(IItemStack a, IItemStack b) => a.Count == b.Count && (a.Count == 0 || a.Id == b.Id);

    async UniTask<(IItemStack[] Main, IItemStack Grab, IItemStack[] Pad)> ReadServer()
    {
        (IItemStack[] Main, IItemStack Grab, IItemStack[] Pad) snapshot = default;
        await p.RunOnServer("サーバーの持物・掴み・発射台在庫を読む", () =>
        {
            var data = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            snapshot = (data.MainOpenableInventory.InventoryItems.ToArray(), data.GrabInventory.GetItem(0), padInventory.InventoryItems.ToArray());
        });
        return snapshot;
    }

    async UniTask OpenPad()
    {
        // tier9-4 と同じく南面1m手前から Alt カーソルで南面を狙って開く
        // Same as tier9-4: from 1 m south, aim the Alt cursor at the south face and open
        p.WarpPlayer(new Vector3(padPos.x + 5f, 32.5f, padPos.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(padPos.x + 5f, 33f, padPos.z));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilUiElement("block-inventory-close", 10f);
    }

    #endregion
});
