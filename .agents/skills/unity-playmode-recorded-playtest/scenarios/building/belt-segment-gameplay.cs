using System.Linq;
using VContainer;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using UnityEngine;
using UnityEngine.InputSystem;

return PlaytestRunner.Run("belt-segment-gameplay", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 900f }, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    p.Assert(p.CurrentUiState == Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen, "通常ゲーム画面");
    p.WarpPlayer(new Vector3(3.5f,33.5f,-1f));
    p.Note("UIで無動力ベルト・坂・曲がりと入出力チェストを設置");
    foreach(var name in new[]{"ベルトコンベア","上りベルトコンベア","下りベルトコンベア","直線歯車ベルトコンベア","木のコンベアチェスト"})
        await p.PrepareBlockForUiPlacement(name,20);
    await p.PlaceBlockViaUi("木のコンベアチェスト",new Vector3Int(4,32,9),BlockDirection.North);
    await p.PlaceBlockViaUi("ベルトコンベア",new Vector3Int(5,32,8),BlockDirection.North);
    await p.DragPlaceViaUi("ベルトコンベア",new Vector3Int(2,32,8),new Vector3Int(4,32,8));
    await p.PlaceBlockViaUi("下りベルトコンベア",new Vector3Int(2,32,7),BlockDirection.North);
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.PressKey(Key.E);
    await p.AimAtPlaceOrigin("ベルトコンベア",new Vector3Int(2,32,6));
    await p.ClickPlace();
    await p.Until(()=>p.GetBlock(new Vector3Int(2,33,6))!=null,15f,"Eで上段セル設置");
    await p.PlaceBlockViaUi("上りベルトコンベア",new Vector3Int(2,32,5),BlockDirection.North);
    await p.PlaceBlockViaUi("直線歯車ベルトコンベア",new Vector3Int(2,32,4),BlockDirection.North);
    await p.PlaceBlockViaUi("木のコンベアチェスト",new Vector3Int(1,32,0),BlockDirection.North);
    await p.ExitToGameScreen();
    var path=new[]{new Vector3Int(1,32,0),new Vector3Int(2,32,4),new Vector3Int(2,32,5),new Vector3Int(2,33,6),new Vector3Int(2,32,7),new Vector3Int(2,32,8),new Vector3Int(3,32,8),new Vector3Int(4,32,8),new Vector3Int(5,32,8),new Vector3Int(4,32,9)};
    for(int i=0;i<path.Length-1;i++)
    {
        var targets=p.GetBlock(path[i]).GetComponent<BlockConnectorComponent<IBlockInventory,DefaultConnectJudge>>().ConnectedTargets;
        p.Assert(targets.ContainsKey(p.GetBlock(path[i+1]).GetComponent<IBlockInventory>()),"実接続 "+path[i]+" -> "+path[i+1]);
    }
    var source=p.GetBlock(path[0]).GetComponent<VanillaChestComponent>();
    var destination=p.GetBlock(path[path.Length-1]).GetComponent<VanillaChestComponent>();
    var gear=p.GetBlock(path[1]).GetComponent<GearBeltConveyorComponent>();
    var item=PlaytestItemOps.ResolveItemId("鉄インゴット");
    source.SetItem(0,item,20);
    p.Note("source chestの通常Updateから投入し、画像付きcubeと搬送を検証");
    await p.Until(()=>source.GetItem(0).Count<20,15f,"実チェストからの搬入");
    p.Assert(gear.CurrentRpm.AsPrimitive()==0,"無動力の歯車ベルト");
    await p.Screenshot("01-main-route-flow");
    await p.Until(()=>destination.InventoryItems.Sum(s=>s.Count)>=1,30f,"坂と曲がりを越えて到着");
    p.WarpPlayer(new Vector3(5.5f,33.5f,4f));
    await p.Screenshot("02-slopes-and-turn");
    await p.Until(()=>destination.InventoryItems.Sum(s=>s.Count)==20,60f,"全20個到着");
    p.Assert(source.InventoryItems.Sum(s=>s.Count)==0,"source chest empty");
    await p.Screenshot("03-main-delivery");
    p.Note("2本の実チェストから合流し、3出口へ分配");
    p.WarpPlayer(new Vector3(13f,33.5f,3f));
    await p.PrepareBlockForUiPlacement("ベルトコンベア分岐器",2);
    await Facing(new Vector3Int(11,32,12),BlockDirection.West);
    await Facing(new Vector3Int(13,32,12),BlockDirection.East);
    await Facing(new Vector3Int(12,32,13),BlockDirection.North);
    await p.PlaceBlockViaUi("ベルトコンベア分岐器",new Vector3Int(12,32,12),BlockDirection.North);
    await p.DragPlaceViaUi("ベルトコンベア",new Vector3Int(12,32,9),new Vector3Int(12,32,11));
    await Facing(new Vector3Int(13,32,10),BlockDirection.West);
    await Facing(new Vector3Int(13,32,9),BlockDirection.North);
    await p.DragPlaceViaUi("ベルトコンベア",new Vector3Int(16,32,9),new Vector3Int(14,32,9));
    await p.PlaceBlockViaUi("木のコンベアチェスト",new Vector3Int(11,32,5),BlockDirection.North);
    await p.PlaceBlockViaUi("木のコンベアチェスト",new Vector3Int(15,32,5),BlockDirection.North);
    await p.ExitToGameScreen();
    Edge(new Vector3Int(11,32,5),new Vector3Int(12,32,9));
    Edge(new Vector3Int(15,32,5),new Vector3Int(16,32,9));
    Edge(new Vector3Int(12,32,9),new Vector3Int(12,32,10));
    Edge(new Vector3Int(13,32,10),new Vector3Int(12,32,10));
    foreach(var pos in new[]{new Vector3Int(11,32,12),new Vector3Int(13,32,12),new Vector3Int(12,32,13)}) Edge(new Vector3Int(12,32,12),pos);
    var left=Chest(new Vector3Int(11,32,5)); var right=Chest(new Vector3Int(15,32,5));
    var copper=PlaytestItemOps.ResolveItemId("銅インゴット");
    left.SetItem(0,item,6); right.SetItem(0,copper,6);
    var world=p.ServerService<BeltWorldDatastore>();
    await p.Until(()=>new[]{new Vector3Int(11,32,12),new Vector3Int(13,32,12),new Vector3Int(12,32,13)}.All(pos=>State(pos).RunningItem!=null),30f,"全3出口に実搬送");
    p.Assert(world.CaptureSnapshot().Simulation.Segments.Any(s=>s.Kind==Game.BeltSegment.BeltSegmentKind.Merge),"合流segment");
    p.Assert(world.CaptureSnapshot().Simulation.Segments.Any(s=>s.Kind==Game.BeltSegment.BeltSegmentKind.Branch),"分岐segment");
    await p.Screenshot("04-merge-branch-two-textures");
    p.Note("4方向の閉路を満たし、実撤去プロトコルで返却と再構築を検証");
    p.WarpPlayer(new Vector3(20.5f,33.5f,4f));
    await Facing(new Vector3Int(20,32,11),BlockDirection.East);
    await Facing(new Vector3Int(21,32,11),BlockDirection.South);
    await Facing(new Vector3Int(21,32,10),BlockDirection.West);
    await Facing(new Vector3Int(20,32,10),BlockDirection.North);
    await Facing(new Vector3Int(20,32,9),BlockDirection.North);
    await p.PlaceBlockViaUi("木のコンベアチェスト",new Vector3Int(19,32,5),BlockDirection.North);
    await p.ExitToGameScreen();
    var loop=new[]{new Vector3Int(20,32,10),new Vector3Int(20,32,11),new Vector3Int(21,32,11),new Vector3Int(21,32,10)};
    for(int i=0;i<4;i++) Edge(loop[i],loop[(i+1)%4]);
    Edge(new Vector3Int(19,32,5),new Vector3Int(20,32,9)); Edge(new Vector3Int(20,32,9),loop[0]);
    Chest(new Vector3Int(19,32,5)).SetItem(0,item,6);
    await p.Until(()=>loop.All(pos=>State(pos).RunningItem!=null)&&State(loop[0]).BufferedItem!=null&&State(new Vector3Int(20,32,9)).RunningItem!=null,30f,"閉路・合流buffer・入口が満杯");
    int inventoryBefore=p.CountItem("鉄インゴット"); ulong generation=world.CaptureSnapshot().Generation;
    var removed=await ClientContext.VanillaApi.Response.BlockRemove(new Vector3Int(20,32,9),default);
    p.Assert(removed!=null&&removed.Success,"実撤去応答");
    await p.Until(()=>world.CaptureSnapshot().Generation>generation,10f,"撤去generation適用");
    p.Assert(p.CountItem("鉄インゴット")==inventoryBefore+1,"撤去セルのrunning itemを1個返却");
    p.Assert(loop.All(pos=>State(pos).RunningItem!=null)&&State(loop[0]).BufferedItem==null,"4個保持・消失junctionのbufferだけ破棄");
    var loopRoute=world.CaptureSnapshot().Routes.Single(route=>route.Cells.Length==4&&route.Cells.All(c=>c.Cell.X>=20));
    p.Assert(loopRoute.Cells[0].Cell.X==20&&loopRoute.Cells[0].Cell.Y==10,"閉路cutは最小座標");
    await p.Screenshot("05-loop-after-refund");
    var resolver=ClientDIContext.DIContainer.DIContainerResolver;
    var controls=resolver.Resolve<System.Collections.Generic.IReadOnlyList<CommandForgeGenerator.Command.ISkitWorldObjectControl>>();
    var renderer=controls.Single(c=>c.GetType().Name=="BeltItemRenderer");
    renderer.SetActive(false); await p.Screenshot("06-skit-belt-items-hidden");
    renderer.SetActive(true); await p.Screenshot("07-skit-belt-items-restored");
    p.WarpPlayer(new Vector3(3.5f,33.5f,-1f));
    source.SetItem(0,item,20);
    await p.Until(()=>source.InventoryItems.Sum(s=>s.Count)<20,10f,"保存時の移動アイテム");
    var save=await resolver.Resolve<Client.Network.API.ServerSaveGenerationWaiter>().SaveAndWaitWrittenAsync(new Client.Network.API.ServerSaveGenerationWaiter.RealtimeBudget(30f));
    p.Assert(save==Client.Network.API.ServerSaveGenerationWaiter.SaveWaitResult.Written,"本番saveの書込完了");
    ValidateClient(renderer, resolver);
    var observation=new {Junctions=new[]{new Vector3Int(12,32,10),new Vector3Int(12,32,12)}.Select(pos=>new {X=pos.x,Y=pos.y,Z=pos.z,State=State(pos)}).ToArray(),Loop=loop.Select(pos=>new {X=pos.x,Y=pos.y,Z=pos.z,State=State(pos)}).ToArray(),Routes=world.CaptureSnapshot().Routes,MainTotal=40,MergeTotal=12};
    System.IO.File.WriteAllText(System.IO.Path.Combine(Client.Playtest.Core.PlaytestPaths.RootDirectory,"worlds/belt-segment-task3-01/belt-validation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(observation,Newtonsoft.Json.Formatting.Indented));
    await p.PressKey(Key.Tab); await p.Screenshot("08-inventory-hud");
    await p.PressKey(Key.Tab);
    await p.Screenshot("09-saved-live-main-route");
    #region Internal
    void ValidateClient(CommandForgeGenerator.Command.ISkitWorldObjectControl renderer,VContainer.IObjectResolver resolver)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var type=typeof(Client.Game.InGame.BeltSegment.BeltWorldRegistration).Assembly.GetType("Client.Game.InGame.BeltSegment.Model.ClientBeltWorld");
        var client=resolver.Resolve(type);
        p.Assert(type.GetProperty("Status",flags).GetValue(client).ToString()=="Running","client accepted stream Running");
        p.Assert((ulong)type.GetProperty("Generation",flags).GetValue(client)==p.ServerService<BeltWorldDatastore>().CaptureSnapshot().Generation,"client最新generation");
        var cpu=(Game.BeltSegment.BeltReplaySnapshot)type.GetMethod("CaptureCpuState",flags).Invoke(client,null);
        var buffers=renderer.GetType().GetField("buffers",flags).GetValue(renderer);
        var counts=(GraphicsBuffer)buffers.GetType().GetField("Counts",flags).GetValue(buffers);
        var values=new uint[counts.count]; counts.GetData(values);
        p.Assert(values.Sum(v=>(long)v)==cpu.Segments.Sum(s=>s.Items.Length),"実GPU draw count = client running count・buffer非表示");
        p.Assert(typeof(Client.Game.InGame.BeltSegment.BeltWorldRegistration).Assembly.GetType("Client.Game.InGame.Entity.Object.BeltConveyorItemEntityObject")==null,"旧belt entity classなし");
    }
    async UniTask Facing(Vector3Int pos,BlockDirection direction)
    {
        await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
        var system=ClientDIContext.DIContainer.DIContainerResolver.Resolve<Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.BeltConveyorPlaceSystem>();
        var field=system.GetType().GetField("_currentBlockDirection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        for(int turns=0;turns<4&&(BlockDirection)field.GetValue(system)!=direction;turns++) await p.PressKey(Key.R);
        await p.AimAtPlaceOrigin("ベルトコンベア",pos); await p.ClickPlace();
        await p.Until(()=>p.GetBlock(pos)!=null,15f,"方向つき設置 "+pos);
        p.Assert(p.GetBlock(pos).BlockPositionInfo.BlockDirection==direction,"水平方向 "+direction);
    }
    void Edge(Vector3Int from,Vector3Int to) => p.Assert(p.GetBlock(from).GetComponent<BlockConnectorComponent<IBlockInventory,DefaultConnectJudge>>().ConnectedTargets.ContainsKey(p.GetBlock(to).GetComponent<IBlockInventory>()),"実接続 "+from+" -> "+to);
    VanillaChestComponent Chest(Vector3Int pos)=>p.GetBlock(pos).GetComponent<VanillaChestComponent>();
    BeltCellSaveState State(Vector3Int pos)=>p.ServerService<BeltWorldDatastore>().CaptureCell(p.GetBlock(pos).GetComponent<SegmentBeltComponent>());
    #endregion
});
