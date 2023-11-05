using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using Newtonsoft.Json;

namespace Game.Quest.Factory.QuestTemplate
{
    public class ItemCraftQuestTemplate : IQuestTemplate
    {
        private readonly ICraftingEvent _craftingEvent;
        private readonly ItemStackFactory _itemStackFactory;

        public ItemCraftQuestTemplate(ItemStackFactory itemStackFactory, ICraftingEvent craftingEvent)
        {
            _itemStackFactory = itemStackFactory;
            _craftingEvent = craftingEvent;
        }

        public IQuest CreateQuest(QuestConfigData questConfig, List<IQuest> preRequestQuests)
        {
            return new ItemCraftQuest(questConfig, _craftingEvent, GetCraftItem(questConfig.QuestParameter),
                preRequestQuests);
        }

        public IQuest LoadQuest(QuestConfigData questConfig, bool isCompleted, bool isRewarded,
            List<IQuest> preRequestQuests)
        {
            return new ItemCraftQuest(questConfig, _craftingEvent, isCompleted, isRewarded,
                GetCraftItem(questConfig.QuestParameter), preRequestQuests);
        }

        private int GetCraftItem(string parameter)
        {
            var param = JsonConvert.DeserializeObject<ItemCraftQuestParameter>(parameter);
            return _itemStackFactory.Create(param.ModId, param.ItemName, 1).Id;
        }
    }


    [JsonObject("ItemCraftQuestParameter")]
    internal class ItemCraftQuestParameter
    {
        [JsonProperty("name")] public string ItemName;

        [JsonProperty("modId")] public string ModId;
    }
}