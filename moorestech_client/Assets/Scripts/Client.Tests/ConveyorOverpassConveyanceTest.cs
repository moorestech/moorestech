using System;
using System.IO;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Gear.Common;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;

namespace Client.Tests
{
    // ライブMod(実 上り/下りベルトコンベア)で立体交差プロファイルが実際にアイテムを搬送できるかをEditModeで検証する
    // EditMode verification that the overpass profile actually conveys an item with the live mod's real up/down belts.
    public class ConveyorOverpassConveyanceTest
    {
        [Test]
        public void OverpassActuallyConveysItemAcross()
        {
            // ライブMod(moorestechAlphaMod_8)を読み込んでサーバーDIを起動する
            // Boot the server DI loading the live mod (moorestechAlphaMod_8).
            var liveModDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", "moorestech_master", "server_v8"));
            Assert.IsTrue(Directory.Exists(liveModDir), $"live mod not found: {liveModDir}");
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(liveModDir));

            // 歯車ベルトコンベア上り/下りを使った正しい立体交差プロファイルを設置する
            // Place the correct overpass profile using Gear Up/Down belts (the only belts with actual Y-shifting connectors).
            // 上り歯車: output dir [0,1,1] → East rotation → (1,1,0) → target (p+1, q+1, r) (goes East AND up)
            // 下り歯車: input dir [0,1,-1] → East rotation → (-1,1,0) → accepts from (p-1, q+1, r) (from above-and-behind)
            // Layout (all BlockDirection.East): (0,0,0)Straight→(1,0,0)GearUp→(2,1,0)Straight→(3,0,0)GearDown→(4,0,0)Straight
            var b0 = PlaceBelt("ベルトコンベア", new Vector3Int(0, 0, 0));
            var b1 = PlaceBelt("上り歯車ベルトコンベア", new Vector3Int(1, 0, 0));
            var b2 = PlaceBelt("ベルトコンベア", new Vector3Int(2, 1, 0));
            var b3 = PlaceBelt("下り歯車ベルトコンベア", new Vector3Int(3, 0, 0));
            var b4 = PlaceBelt("ベルトコンベア", new Vector3Int(4, 0, 0));

            // 歯車ベルトコンポーネントを取得してギアエネルギーを供給する
            // Get gear belt components to supply gear energy each tick.
            var g1 = GetGearBeltComponent(new Vector3Int(1, 0, 0));
            var g3 = GetGearBeltComponent(new Vector3Int(3, 0, 0));

            // 先頭ベルトにアイテムを1個挿入する
            // Insert one item into the first belt.
            var itemId = MasterHolder.ItemMaster.GetItemAllIds().First();
            var item = ServerContext.ItemStackFactory.Create(itemId, 1);
            b0.InsertItem(item, InsertItemContext.Empty);

            // 十分にtickを回してアイテムを流す（毎tick歯車ベルトにエネルギーを供給する）
            // Tick enough to let the item travel (supply gear energy every tick).
            const float supplyRpm = 10f;
            const float supplyTorque = 10f;
            for (var i = 0; i < 600; i++)
            {
                g1.SupplyPower(new RPM(supplyRpm), new Torque(supplyTorque), true);
                g3.SupplyPower(new RPM(supplyRpm), new Torque(supplyTorque), true);
                GameUpdater.UpdateOneTick();
            }

            // 各ベルト上のアイテム数を集計する（立体交差を渡れたなら終端ベルトに乗る）
            // Count items on each belt (if it crossed the overpass the item ends up on the far belt).
            var c0 = Count(b0);
            var c1 = Count(b1);
            var c2 = Count(b2);
            var c3 = Count(b3);
            var c4 = Count(b4);
            var diag = $"items per belt: x0={c0} x1(GearUp)={c1} x2(cross@y1)={c2} x3(GearDown)={c3} x4={c4}";
            Debug.Log(diag);

            // 終端ベルト(x=4)に到達していれば立体交差を搬送できている
            // Reaching the far belt (x=4) proves the overpass conveys.
            Assert.Greater(c4, 0, $"アイテムが立体交差を渡れなかった / item did not cross the overpass. {diag}");

            #region Internal

            VanillaBeltConveyorComponent PlaceBelt(string blockName, Vector3Int pos)
            {
                // 名前からBlockIdを引いてサーバーに設置し、ベルトコンポーネントを返す
                // Look up the BlockId by name, place it on the server, and return the belt component.
                var blockId = FindBlockIdByName(blockName);
                var placed = ServerContext.WorldBlockDatastore.TryAddBlock(blockId, pos, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var block);
                Assert.IsTrue(placed, $"設置失敗 / failed to place {blockName} at {pos}");
                return block.GetComponent<VanillaBeltConveyorComponent>();
            }

            GearBeltConveyorComponent GetGearBeltComponent(Vector3Int pos)
            {
                // ワールドからブロックを取得してGearBeltConveyorComponentを返す
                // Retrieve the block from the world and return its GearBeltConveyorComponent.
                ServerContext.WorldBlockDatastore.TryGetBlock<GearBeltConveyorComponent>(pos, out var gearBelt);
                Assert.IsNotNull(gearBelt, $"GearBeltConveyorComponent not found at {pos}");
                return gearBelt;
            }

            BlockId FindBlockIdByName(string blockName)
            {
                foreach (var id in MasterHolder.BlockMaster.GetBlockAllIds())
                {
                    if (MasterHolder.BlockMaster.GetBlockMaster(id).Name == blockName) return id;
                }
                throw new ArgumentException($"block not found: {blockName}");
            }

            int Count(VanillaBeltConveyorComponent belt)
            {
                return belt.BeltConveyorItems.Count(x => x != null);
            }

            #endregion
        }
    }
}
