// 無料設置デバッグONで電柱をUI経路設置し、既設電柱へ電線が自動接続され素材が減らないことを確認する（ADR 0056）
// With free-placement debug on, UI-place a pole and confirm it auto-connects to an existing pole without consuming materials (ADR 0056)
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("free-placement-wire-autoconnect", options, async p =>
{
    // 既定で FreeBlockPlacement=true（無料設置ON）
    // FreeBlockPlacement defaults to true (free placement on)
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();
    p.WarpPlayer(new Vector3(4f, 33.5f, -8f));

    // 既設電柱をサーバー直で置く。connectToolは未解放のまま・電線は所持0のまま
    // Place an existing pole directly; connectTool stays locked and no wire item is held
    var existing = new Vector3Int(0, 32, 2);
    p.PlaceBlockDirect("電柱", existing, BlockDirection.North);
    await p.WaitBlockGameObject(existing);
    p.Note("既設電柱の隣へ電柱をUI設置する");

    var placed = new Vector3Int(4, 32, 2);
    await p.PlaceBlockViaUi("電柱", placed, BlockDirection.North);
    await p.WaitBlockGameObject(placed);

    // 新電柱と既設電柱が相互接続されている
    // The new pole and the existing pole are connected both ways
    var placedConnector = p.GetBlock(placed).GetComponent<IElectricWireConnector>();
    var existingConnector = p.GetBlock(existing).GetComponent<IElectricWireConnector>();
    p.Assert(placedConnector.ContainsWireConnection(existingConnector.BlockInstanceId), "新電柱→既設電柱の接続");
    p.Assert(existingConnector.ContainsWireConnection(placedConnector.BlockInstanceId), "既設電柱→新電柱の接続");
    p.Assert(placedConnector.WireConnections.Count == 1, "新電柱の接続数は1");

    // 電線描画の出現を待って絵に残す
    // Wait for the wire view and capture it
    await p.WaitSeconds(1.0f);
    await p.Screenshot("01-wired");
});
