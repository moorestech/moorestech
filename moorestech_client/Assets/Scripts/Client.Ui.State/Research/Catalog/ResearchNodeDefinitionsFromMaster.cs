using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Item;
using Core.Master;
using Mooresmaster.Model.ResearchModule;
using UnityEngine;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究マスタの全ノードを定義へ写す（Web の橋 ResearchNodeDtoFactory と同じ読み方）
    /// Copies every research master node into a definition (same reading as the Web bridge ResearchNodeDtoFactory)
    /// </summary>
    public static class ResearchNodeDefinitionsFromMaster
    {
        public static IReadOnlyList<ResearchNodeDefinition> Build()
        {
            var masters = MasterHolder.ResearchMaster.GetAllResearches();
            var definitions = new List<ResearchNodeDefinition>(masters.Count);
            foreach (var master in masters) definitions.Add(ToDefinition(master));
            return definitions;

            #region Internal

            ResearchNodeDefinition ToDefinition(ResearchNodeMasterElement master)
            {
                // アイコンと座標はグラフ表示の設定から読む
                // Icon and position come from the graph view settings
                var iconItemId = MasterHolder.ItemMaster.GetItemId(master.GraphViewSettings.IconItem);
                var position = new Vector2(master.GraphViewSettings.UIPosition.x, master.GraphViewSettings.UIPosition.y);
                var previous = new List<Guid>(master.PrevResearchNodeGuids);
                return new ResearchNodeDefinition(master.ResearchNodeGuid, iconItemId, position, previous, ConsumeItemsOf(master), ResearchUnlockSetFromMaster.Build(master));
            }

            // 消費アイテムは上位レベルで代替できる ID 群ごと運ぶ（割当の 2 パスが読む）
            // Consume items carry the higher-level substitutes too (the two-pass allocation reads them)
            IReadOnlyList<ResearchConsumeItem> ConsumeItemsOf(ResearchNodeMasterElement master)
            {
                var items = new List<ResearchConsumeItem>(master.ConsumeItems.Length);
                foreach (var consume in master.ConsumeItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(consume.ItemGuid);
                    items.Add(new ResearchConsumeItem(itemId, consume.ItemCount, LevelFamilySubstituteUtil.GetAcceptedItemIds(itemId)));
                }
                return items;
            }

            #endregion
        }
    }
}
