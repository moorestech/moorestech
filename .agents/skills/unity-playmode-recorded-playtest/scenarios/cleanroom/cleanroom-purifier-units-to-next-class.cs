// シナリオ: 密閉部屋に満電の清浄機1台を置き、清浄機の画面の見出しが次のクラスとあと何台かを出し、台数とこの清浄機の行があって濃度と換気回数が無いことを見る。続けて見出しの台数だけ清浄機を足すと部屋が本当にそのクラスへ上がり、見出しがそのクラスになることを確かめる（ADR 0105 §5）
// Scenario: one fully powered purifier in a sealed room; its screen headline names the next class and how many more, with count and own-status rows and no concentration or air changes. Adding that many purifiers really raises the room to that class and the headline shows it (ADR 0105 §5)
using System;
using System.Text.RegularExpressions;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
const string WallName = "クリーンルームブロック";
const string PurifierName = "クリーンルーム空気清浄機";
var purifierPos = new Vector3Int(4, 33, 7); // 室内の北の壁際中央。1x2x1 で y=33,34 を占める
// 足す清浄機の置き場（北の壁際で最初の1台の両隣から外へ。発電機1000・清浄機150なので6台まで満電）
// Where extra purifiers go (along the north wall outward from the first; generator 1000 and purifier 150 keep six at full power)
var extraPositions = new[] { new Vector3Int(3, 33, 7), new Vector3Int(5, 33, 7), new Vector3Int(2, 33, 7), new Vector3Int(6, 33, 7), new Vector3Int(1, 33, 7) };
var outsidePosition = new Vector3(4.5f, 33.5f, -4f);
var gasolineGuid = new Guid("019eded2-7454-76da-8943-84c4e7a59179");
var freeCost = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
// 録画中に Editor が背面へ回っても注入キーが GameView へ届くようにする（フィルター消費のシナリオと同じ）
// Route injected input to GameView even when the Editor goes to the background (same as the filter-consumption scenario)
InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
SemanticInput.EnsureDevices();
InputSystem.EnableDevice(Keyboard.current);
InputSystem.EnableDevice(Mouse.current);
return PlaytestRunner.Run("cleanroom-purifier-units-to-next-class", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 15f, "GameScreenに到達");
    p.Assert(Keyboard.current.enabled && Mouse.current.enabled && InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus, $"入力環境: キーボードとマウスが有効 (focus={Application.isFocused})");
    await p.SetupFlatGround();
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    p.WarpPlayer(outsidePosition);
    #region 密閉部屋と清浄機1台 / Sealed room with one purifier
    // 床y=32・天井y=36の全面と側壁y=33〜35の周回で内部7x7x3の部屋を作る（ドア無し・プレイヤーはワープで出入りする）
    // Full floor (y=32), ceiling (y=36) and perimeter walls (y=33-35) make a 7x7x3 interior; no door, the player warps in
    for (var x = 0; x <= 8; x++)
    for (var z = 0; z <= 8; z++)
    {
        await p.PlaceBlockDirect(WallName, new Vector3Int(x, 32, z), BlockDirection.North);
        await p.PlaceBlockDirect(WallName, new Vector3Int(x, 36, z), BlockDirection.North);
        if (x != 0 && x != 8 && z != 0 && z != 8) continue;
        for (var y = 33; y <= 35; y++) await p.PlaceBlockDirect(WallName, new Vector3Int(x, y, z), BlockDirection.North);
    }
    var generatorBlock = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-15, 32, -15), BlockDirection.North);
    var firstPurifier = await PlacePurifier(purifierPos);
    await p.WaitForCleanRoom(firstPurifier, true, 20f, "清浄機1台の密閉部屋にクラスが付いた");
    var startThreshold = await ReadThreshold();
    p.Assert(0 < startThreshold, $"最上位のクラスでなく上げる余地がある(閾値行={startThreshold})");
    #endregion
    #region (1) 清浄機の画面が次のクラスとあと何台かを出す / The purifier screen names the next class and how many more
    await OpenPurifierUi();
    await PlaytestUiElementOps.WaitUiTextContains("clean-room-air-filter-count", "", 10f);
    await PlaytestUiElementOps.WaitUiTextContains("clean-room-queried-air-filter", "%", 10f);
    var headline = (await PlaytestUiElementOps.FindUi("clean-room-headline")).Text;
    var section = (await PlaytestUiElementOps.FindUi("clean-room-section")).Text;
    p.Note($"部屋欄(清浄機1台): {section.Replace("\n", " | ")}");

    // 日本語「クラスC（クラスBまであと2台）」・英語「Class C (2 more to class B)」の両方から読む
    // Read both the Japanese and English forms of the headline
    var units = Regex.Match(headline, @"(\d+)(?:台| more)");
    var next = Regex.Match(headline, @"クラス([^（）\s]+?)まで|to class ([^)\s]+)\)");
    var unitCount = units.Success ? int.Parse(units.Groups[1].Value) : 0;
    var nextClassName = next.Groups[1].Success ? next.Groups[1].Value : next.Groups[2].Value;
    p.Note($"見出しから読んだ値: あと{unitCount}台でクラス{nextClassName}");
    p.Assert(units.Success && next.Success, $"見出しに次のクラスとあと何台かが出る(見出し={headline})");
    p.Assert(0 < unitCount && unitCount <= extraPositions.Length, $"あと{unitCount}台が置き場の数({extraPositions.Length})以内");
    p.Assert((await PlaytestUiElementOps.FindUi("clean-room-air-filter-count")).Text.Contains("1"), "台数の行が1台");
    p.Assert((await PlaytestUiElementOps.FindUi("clean-room-queried-air-filter")).Text.Contains("%"), "この清浄機の電力と残量の行が出る");
    p.Assert(!ContainsConcentration(section) && !section.Contains("換気") && !section.Contains("Air change"), $"部屋欄に濃度と換気回数が無い(本文={section.Replace("\n", " | ")})");
    await p.Screenshot("01-purifier-units-to-next-class");
    await CloseUi();
    #endregion
    #region (2) 見出しの台数だけ足すと次のクラスへ上がる / Adding that many purifiers raises the room to the next class
    // 前の主張が落ちて台数が決まらないときは足さずに終える（失敗の主張は result.json に残っている）
    // When the earlier assertion failed and no count is known, stop here (the failed assertion is already in result.json)
    if (!units.Success || !next.Success || unitCount <= 0 || extraPositions.Length < unitCount) return;
    for (var i = 0; i < unitCount; i++) await PlacePurifier(extraPositions[i]);
    await p.WaitForCleanRoomClass(firstPurifier, startThreshold - 1, 30f, $"清浄機を{unitCount}台足して閾値行{startThreshold - 1}(クラス{nextClassName})になった");
    await OpenPurifierUi();
    await PlaytestUiElementOps.WaitUiTextContains("clean-room-air-filter-count", "", 10f);
    var raised = (await PlaytestUiElementOps.FindUi("clean-room-headline")).Text;
    var raisedCount = (await PlaytestUiElementOps.FindUi("clean-room-air-filter-count")).Text;
    p.Note($"清浄機{1 + unitCount}台: 見出し={raised} 台数={raisedCount}");
    p.Assert(raised.StartsWith($"クラス{nextClassName}") || raised.StartsWith($"Class {nextClassName}"), $"見出しがクラス{nextClassName}になった(見出し={raised})");
    p.Assert(raisedCount.Contains((1 + unitCount).ToString()), $"台数の行が{1 + unitCount}台(行={raisedCount})");
    await p.Screenshot("02-purifier-next-class");
    await CloseUi();
    p.WarpPlayer(outsidePosition);
    #endregion

    #region Internal

    async UniTask<IBlock> PlacePurifier(Vector3Int position)
    {
        // フィルター8個を入れ、発電機を満タンにして室外の発電機から配線する
        // Load eight filters, top up the generator and wire it from the outdoor generator
        var block = await p.PlaceBlockDirect(PurifierName, position, BlockDirection.North);
        Core.Inventory.IOpenableInventory filterInventory = block.GetComponent<IOpenableBlockInventoryComponent>();
        var filterItemId = PlaytestItemOps.ResolveItemId("クリーンルームフィルター");
        var wired = false;
        await p.RunOnServer($"{position} の清浄機にフィルターを入れて配線する", () =>
        {
            filterInventory.SetItem(0, filterItemId, 8);
            generatorBlock.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(200, MasterHolder.FluidMaster.GetFluidId(gasolineGuid)), default);
            wired = ElectricWireSystemUtil.TryConnectBothSides(generatorBlock.GetComponent<IElectricWireConnector>(), block.GetComponent<IElectricWireConnector>(), freeCost);
        });
        p.Assert(wired, $"電線 発電機→{position} の清浄機");
        return block;
    }

    async UniTask<int> ReadThreshold()
    {
        var threshold = -1;
        await p.RunOnServer("部屋の閾値行を読む", () =>
        {
            if (ServerContext.GetService<CleanRoomDatastore>().TryGetCleanRoom(firstPurifier, out var room)) threshold = room.ThresholdIndex;
        });
        return threshold;
    }

    async UniTask OpenPurifierUi()
    {
        // Fの射程は2mなので最初の清浄機の南面の1m手前に立ち、Altのカーソル照準で南面を狙ってFで開く
        // F reaches 2 m, so stand 1 m south of the first purifier and open it with F by aiming at that face via the Alt cursor
        p.WarpPlayer(new Vector3(4.5f, 33.5f, 6f));
        await p.Until(() => 32.6f < p.PlayerPosition.y && p.PlayerPosition.y < 33.1f, 5f, "室内の床に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(4.5f, 33.8f, 7f));
        await p.WaitSeconds(0.5f);
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await PlaytestUiElementOps.WaitUiTextContains("clean-room-section", "", 10f);
    }

    async UniTask CloseUi() { await p.PressKey(Key.Tab); await p.WaitUiState(UIStateEnum.GameScreen, 10f); }
    bool ContainsConcentration(string text) => text.Contains("濃度") || text.Contains("不純物") || text.Contains("oncentration") || text.Contains("ppm");

    #endregion
});
