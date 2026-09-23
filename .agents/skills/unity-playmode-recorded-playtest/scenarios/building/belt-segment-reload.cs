using System.Linq;
using System.IO;
using Client.Playtest;
using Client.Game.InGame.Context;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface.Extension;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

return PlaytestRunner.Run("belt-segment-reload",new PlaytestRunOptions {Record=true,ScenarioTimeoutSeconds=180f},async p=>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    p.Note("同一固定worldを再起動。再投入前の停止閉路・分岐状態と総数を検証");
    var saved=JObject.Parse(File.ReadAllText(Path.Combine(Client.Playtest.Core.PlaytestPaths.RootDirectory,"worlds/belt-segment-task3-01/belt-validation.json")));
    var disk=JObject.Parse(File.ReadAllText(Path.Combine(Client.Playtest.Core.PlaytestPaths.RootDirectory,"worlds/belt-segment-task3-01/save.json")));
    var blocks=(JArray)disk["world"];
    var mainTotal=blocks.Where(b=>(int)b["X"]<10).Sum(b=>
    {
        var state=b["state"];
        var chest=state["Game.Block.Blocks.Chest.VanillaChestComponent"] as JArray;
        var belt=state["Game.Block.Blocks.BeltConveyor.SegmentBeltSaveComponent"];
        return (chest?.Sum(i=>(int)i["count"])??0)+(belt?["RunningItem"]?.Type==JTokenType.Object?1:0)+(belt?["BufferedItem"]?.Type==JTokenType.Object?1:0);
    });
    p.Note("比較元: 実save.json tick="+disk["currentTick"]+", main総数="+mainTotal);
    var world=p.ServerService<BeltWorldDatastore>();
    foreach(var entry in saved["Loop"].Concat(saved["Junctions"]))
    {
        var pos=new Vector3Int((int)entry["X"],(int)entry["Y"],(int)entry["Z"]);
        var actual=world.CaptureCell(p.GetBlock(pos).GetComponent<SegmentBeltComponent>());
        var expected=blocks.Single(b=>(int)b["X"]==pos.x&&(int)b["Y"]==pos.y&&(int)b["Z"]==pos.z)["state"]["Game.Block.Blocks.BeltConveyor.SegmentBeltSaveComponent"];
        p.Assert(JToken.DeepEquals(expected,JToken.Parse(JsonConvert.SerializeObject(actual))),"停止cellのGUID・progress・entry・RR復元 "+pos);
    }
    p.Assert(JToken.DeepEquals(saved["Routes"],JToken.FromObject(world.CaptureSnapshot().Routes)),"全route・entry・loop cut復元");
    var snapshot=world.CaptureSnapshot();
    var source=p.GetBlock(new Vector3Int(1,32,0)).GetComponent<VanillaChestComponent>();
    var destination=p.GetBlock(new Vector3Int(4,32,9)).GetComponent<VanillaChestComponent>();
    var running=snapshot.Simulation.Segments.Select((segment,index)=>new {segment,route=snapshot.Routes[index]});
    var mainRunning=running.Where(x=>x.route.Cells.All(c=>c.Cell.X<10)).Sum(x=>x.segment.Items.Length+(x.segment.BufferedItem.HasValue?1:0));
    p.Assert(source.InventoryItems.Sum(s=>s.Count)+destination.InventoryItems.Sum(s=>s.Count)+mainRunning==mainTotal,"再起動main全"+mainTotal+"個保存");
    var left=p.GetBlock(new Vector3Int(11,32,5)).GetComponent<VanillaChestComponent>();
    var right=p.GetBlock(new Vector3Int(15,32,5)).GetComponent<VanillaChestComponent>();
    var mergeRunning=running.Where(x=>x.route.Cells.All(c=>c.Cell.X>=11&&c.Cell.X<19)).Sum(x=>x.segment.Items.Length+(x.segment.BufferedItem.HasValue?1:0));
    p.Assert(left.InventoryItems.Sum(s=>s.Count)+right.InventoryItems.Sum(s=>s.Count)+mergeRunning==(int)saved["MergeTotal"],"2種合流の全12個保存");
    // 通常のLook入力で横から観察し、機械による遮蔽を避ける。
    // Inspect from the side with ordinary Look input so machines do not occlude the routes.
    InputSystem.QueueDeltaStateEvent(Mouse.current.delta,new Vector2(-600f,0f));
    await p.Until(()=>Mathf.Abs(Mathf.DeltaAngle(Camera.main.transform.eulerAngles.y,270f))<1f,10f,"通常カメラを西向きへ");
    p.WarpPlayer(new Vector3(23f,33.5f,8.5f)); await p.AimAt(new Vector3(20.5f,32.5f,10.5f));
    await p.Screenshot("01-restored-full-loop");
    var controls=ClientDIContext.DIContainer.DIContainerResolver.Resolve<System.Collections.Generic.IReadOnlyList<CommandForgeGenerator.Command.ISkitWorldObjectControl>>();
    var renderer=controls.Single(c=>c.GetType().Name=="BeltItemRenderer");
    renderer.SetActive(false); await p.Screenshot("01a-skit-hidden");
    renderer.SetActive(true); await p.Screenshot("01b-skit-restored");
    p.WarpPlayer(new Vector3(16f,33.5f,11f)); await p.AimAt(new Vector3(12.5f,32.5f,11f));
    await p.Screenshot("02-restored-merge-branch");
    p.WarpPlayer(new Vector3(5.5f,33.5f,6f)); await p.AimAt(new Vector3(2.5f,32.5f,6f));
    await p.Until(()=>destination.InventoryItems.Sum(s=>s.Count)==mainTotal,60f,"再投入なしで残りが到着");
    p.Note("復元総数確認後に実チェストへ20個を追加し、再起動後の搬送を横から記録");
    source.SetItem(0,Client.Playtest.Operations.PlaytestItemOps.ResolveItemId("鉄インゴット"),20);
    await p.Until(()=>destination.InventoryItems.Sum(s=>s.Count)>mainTotal,30f,"再起動後もチェスト間配送");
    await p.Screenshot("03-restored-slopes-flow");
    await p.Until(()=>destination.InventoryItems.Sum(s=>s.Count)==mainTotal+20,60f,"追加20個が全数到着");
    await p.Screenshot("04-restored-delivery");
    var save=await ClientDIContext.DIContainer.DIContainerResolver.Resolve<Client.Network.API.ServerSaveGenerationWaiter>().SaveAndWaitWrittenAsync(new Client.Network.API.ServerSaveGenerationWaiter.RealtimeBudget(30f));
    p.Assert(save==Client.Network.API.ServerSaveGenerationWaiter.SaveWaitResult.Written,"再起動後も本番save成功");
});
