// Step 6: ロード後の重要データ(列車インベントリ)を実値ダンプし、サイレントなデータ欠損を検査する。
// 例: コンテナのスロット数 > master.InventorySlots だとロード時に切り詰められる -> ここで件数が減って見える。
// 実行: uloop execute-dynamic-code --project-path ./moorestech_client --code "$(cat references/verify_loaded.cs)"
using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Game.SaveLoad.Interface;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Core.Master;

var options = new MoorestechServerDIContainerOptions(ServerDirectory.GetDirectory());
var (packet, sp) = new MoorestechServerDIContainerGenerator().Create(options);
sp.GetService<IWorldSaveDataLoader>().LoadOrInitialize();

var sb = new StringBuilder();
var trains = sp.GetService<ITrainUnitLookupDatastore>().GetRegisteredTrains();
sb.Append("trains=" + trains.Count + "\n");
foreach (var t in trains)
    for (int ci = 0; ci < t.Cars.Count; ci++)
    {
        var car = t.Cars[ci];
        if (car.Container is ItemTrainCarContainer item)
        {
            var items = item.InventoryItems;
            var ne = items.Where(s => s.Id != ItemMaster.EmptyItemId)
                          .GroupBy(s => s.Id.AsPrimitive())
                          .Select(g => "id" + g.Key + "x" + g.Sum(s => s.Count) + "(" + g.Count() + "slots)");
            sb.Append($"  car{ci}: slots={items.Count} total={items.Sum(s => s.Count)} | {string.Join(",", ne)}\n");
        }
        else sb.Append($"  car{ci}: {(car.Container == null ? "null" : car.Container.GetType().Name)}\n");
    }
return sb.ToString();
