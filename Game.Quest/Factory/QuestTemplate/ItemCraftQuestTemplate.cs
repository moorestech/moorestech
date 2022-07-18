using Core.Item;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using Newtonsoft.Json;

namespace Game.Quest.Factory.QuestTemplate
{
    public class ItemCraftQuestTemplate : IQuestTemplate
    {
        private readonly ItemStackFactory _itemStackFactory;
        private readonly ICraftingEvent _craftingEvent;

        public ItemCraftQuestTemplate(ItemStackFactory itemStackFactory, ICraftingEvent craftingEvent)
        {
            _itemStackFactory = itemStackFactory;
            _craftingEvent = craftingEvent;
        }

        public IQuest CreateQuest(QuestConfigData questConfig)
        {
            return new ItemCraftQuest(questConfig,_craftingEvent,GetCraftItem(questConfig.QuestParameter));
        }

        public IQuest LoadQuest(QuestConfigData questConfig, bool isCompleted, bool isRewarded)
        {
            return new ItemCraftQuest(questConfig,_craftingEvent,isCompleted,isRewarded,GetCraftItem(questConfig.QuestParameter));
        }

        private int GetCraftItem(string parameter)
        {
            var param = JsonConvert.DeserializeObject<ItemCraftQuestParameter>(parameter);
            return _itemStackFactory.Create(param.ModId, param.ItemName,1).Id;
        }
    }
    

    [JsonObject("ItemCraftQuestParameter")]
    internal class ItemCraftQuestParameter
    {
        [JsonProperty("modId")]
        public string ModId;
        [JsonProperty("name")]
        public string ItemName;
    }
}