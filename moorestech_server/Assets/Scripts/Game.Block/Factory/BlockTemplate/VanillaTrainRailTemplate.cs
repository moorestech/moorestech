using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEditor.Overlays;
using Core.Item.Interface;
using Newtonsoft.Json;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Gear.Common;
using Game.Train.RailGraph;
using Game.Block.Blocks.Miner;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainRailTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            RailComponent[] railComponents = new RailComponent[1];
            for (int i = 0; i < railComponents.Length; i++)
            {
                RailComponentID railComponentID = new RailComponentID(blockPositionInfo.OriginalPos, i, true);
                var railcomponent = new RailComponent(blockPositionInfo, railComponentID);
                railComponents[i] = railcomponent;
            }

            var railSaver = new RailSaverComponent(railComponents);
            var components = new List<IBlockComponent>
            {
                railSaver,
                railComponents[0]
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            //componentStatesに入っているrailcomponentの数は1つ

            //必要な情報はrailComponentごとに
            //自分のRailComponentID
            //自分のFrontNodeがつながる先のRailNodeに紐づいているRailComponentIDのリスト "_"区切り
            //自分のBackNodeがつながる先のRailNodeに紐づいているRailComponentIDのリスト "_"区切り
            //上3つを":"区切り
            //railComponentの間は";"区切り
            
            var str = componentStates["RailSaverComponent"];
            //strを;で区切って文字列配列にする。使うのは最初の1つだけ
            var railComponents_str = str.Split(';')[0];
            //railComponents_strを:で区切って文字列配列にする
            var railComponent_str = railComponents_str.Split(':');
            var s0 = railComponent_str[0];//自分のRailComponentID
            var s1 = railComponent_str[1];//FrontNodeがつながる先のRailNodeに紐づいているRailComponentIDのリスト "_"区切り
            var s2 = railComponent_str[2];//BackNodeがつながる先のRailNodeに紐づいているRailComponentIDのリスト "_"区切り
            //s1分解
            var s1_list = s1.Split('_');
            //s2分解
            var s2_list = s2.Split('_');

      

            var railComponentID = RailComponentID.Load(s0);
            var railcomponent = new RailComponent(blockPositionInfo, railComponentID);
            //接続情報を復元
            //FrontNode s1_list 最後の要素は空文字列なので無視
            for (int i = 0; i < s1_list.Length - 1; i++)
            {
                var f_node_railComponentID = RailComponentID.Load(s1_list[i]);
                //f_node_railComponentIDを分解 
                var (position, id, isFront) = f_node_railComponentID.GetInfo();
                //World座標からBlockを取得、なければskip
                var block = ServerContext.WorldBlockDatastore.GetBlock(position);
                if (block == null) continue;
                //あればRailSaverComponentを取得
                if (!block.TryGetComponent<RailSaverComponent>(out var railSavercomponent)) continue;
                var nextRailComponent = railSavercomponent.railComponents[id];
                railcomponent.ConnectRailComponent(nextRailComponent, true, isFront);
            }
            //BackNode s2_list 最後の要素は空文字列なので無視
            for (int i = 0; i < s2_list.Length - 1; i++)
            {
                var b_node_railComponentID = RailComponentID.Load(s2_list[i]);
                //b_node_railComponentIDを分解 
                var (position, id, isFront) = b_node_railComponentID.GetInfo();
                //World座標からBlockを取得、なければskip
                var block = ServerContext.WorldBlockDatastore.GetBlock(position);
                if (block == null) continue;
                //あればRailSaverComponentを取得
                if (!block.TryGetComponent<RailSaverComponent>(out var railSavercomponent)) continue;
                var nextRailComponent = railSavercomponent.railComponents[id];
                railcomponent.ConnectRailComponent(nextRailComponent, false, isFront);
            }


            RailComponent[] railComponents = new RailComponent[1];
            railComponents[0] = railcomponent;
            var railSaver = new RailSaverComponent(railComponents);
            var components = new List<IBlockComponent>
            {
                railSaver,
                railcomponent
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
