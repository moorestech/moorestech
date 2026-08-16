// チェーン接続ツール結線モードの検証（UI経路）: 孤立ポール2本を設置し「別ネットワーク」を確認した後、
// 歯車チェーン接続ツールをホットバーで保持してポールA→ポールBの順にクリックし、結線されて同一ネットワークになることを検証する。
// 結線がキーマウ操作（ポールクリック→ChainConnectSend）から発火することの直接の証明。
// Chain connect-tool mode probe (UI route): place two isolated poles, confirm they are in different
// networks, then hold the gear chain connect tool via the hotbar and click pole A then pole B to connect them.
// Directly proves the connection fires from mouse clicks (pole click -> ChainConnectSend), not a direct API call.
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Gear.Common;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("gear-chain-connect-via-ui", options, async p =>
{
    await p.SetupFlatGround();
    // カメラは北(+Z)を向くためプレイヤーを南に置き、ポールは前方(高いZ)へ設置する
    // The camera faces north (+Z), so place the player to the south and the poles ahead (higher Z)
    p.WarpPlayer(new Vector3(5f, 33.5f, -3f));

    // 開幕スキット(Story)を表示中はホットバー入力が効かないためSkipインテントで飛ばしGameScreenへ抜ける
    // The opening skit (Story) blocks hotbar input, so skip it via the intent path and reach GameScreen
    await p.SkipOpeningSkit();

    // ホットバー1=ポール、2=チェーン接続ツール。両方ともホットバー割当前にアンロックが要る（ブロックと接続ツールは別枠）
    // Hotbar 1 = pole, 2 = chain connect tool. Both need unlocking before hotbar assignment (blocks and connect tools use separate unlock buckets)
    p.UnlockBlock("歯車チェーンポール");
    p.Hotbar.UnlockConnectTool("歯車チェーン");
    await p.GiveConstructionCost("歯車チェーンポール", 5);
    await p.GiveItem("鉄のワイヤー", 64);
    await p.Hotbar.AssignHotbar(0, "歯車チェーンポール");
    await p.Hotbar.AssignHotbar(1, "歯車チェーン");

    // 孤立ポール2本: 1本置くごとに同キーで建築モードを抜けて延長起点をリセットし、自動結線させない
    // Two isolated poles: exit build mode with the same key after each to reset the extension source (no auto-chain)
    var c1 = new Vector3Int(2, 32, 2);
    var c2 = new Vector3Int(8, 32, 2);

    async UniTask PlaceIsolatedPole(Vector3Int origin)
    {
        await p.Hotbar.EnterBuildMode(0);
        await p.AimAtPlaceOrigin("歯車チェーンポール", origin);
        await p.ClickPlace();
        await p.Until(() => p.GetBlock(origin) != null, 15f, $"孤立ポール設置 {origin}");
        await p.WaitBlockGameObject(origin);
        await p.Hotbar.ExitBuildMode(0);
    }

    await PlaceIsolatedPole(c1);
    await PlaceIsolatedPole(c2);

    // 結線前の前提を検証: 両ポールは別ネットワーク
    // Pre-connection premise: the two poles belong to different networks
    var gearNetworkDatastore = p.ServerService<GearNetworkDatastore>();
    System.Func<Vector3Int, GearNetworkId?> networkOf = pos =>
    {
        var block = p.GetBlock(pos);
        if (block == null) return null;
        return gearNetworkDatastore.TryGetGearNetwork(block.BlockInstanceId, out var network) ? network.NetworkId : (GearNetworkId?)null;
    };
    p.Assert(networkOf(c1) != null && networkOf(c2) != null && !networkOf(c1).Equals(networkOf(c2)), "結線前は別ネットワーク（孤立設置の確認）");
    await p.Screenshot("01-isolated-poles");

    // ポールの接続エリアコライダー中心を照準点にする（skillの定石: collider.bounds.center）
    // Aim at the pole's connect-area collider center (skill rule of thumb: collider.bounds.center)
    System.Func<Vector3Int, Vector3> poleClickPoint = pos =>
    {
        var blockObject = Client.Game.InGame.Context.ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
        var areaCollider = blockObject.GetComponentInChildren<GearChainPoleConnectAreaCollider>(true);
        return areaCollider.GetComponent<Collider>().bounds.center;
    };

    // 接続ツールを保持し建築モードへ
    // Hold the connect tool to enter build mode
    await p.Hotbar.EnterBuildMode(1);

    // ポールA→ポールBの順にクリックして結線（A=起点選択、B=接続送信）
    // Click pole A then pole B to connect (A selects the source, B sends the connection)
    await p.AimAt(poleClickPoint(c1));
    await p.ClickPlace();
    await p.WaitSeconds(0.3f);
    await p.AimAt(poleClickPoint(c2));
    await p.ClickPlace();

    // 結線反映を条件待機し、同一ネットワークになったことを検証
    // Wait for the connection to land, then verify both poles share one network
    await p.Until(() => networkOf(c1) != null && networkOf(c1).Equals(networkOf(c2)), 15f, "クリック結線で同一ネットワーク化");
    await p.Hotbar.ExitBuildMode(1);
    await p.Screenshot("02-connected");
});
