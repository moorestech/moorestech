using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;
using Server.Event.EventReceive;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// crafting.machine_recipes トピック: アンロック済み機械レシピ一覧。アンロックイベントで再配信
    /// crafting.machine_recipes topic: unlocked machine recipes; republished on unlock events
    /// </summary>
    public class MachineRecipesTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "crafting.machine_recipes";

        private readonly WebSocketHub _hub;
        private readonly IGameUnlockStateData _unlockStateData;
        private readonly IDisposable _subscription;

        public MachineRecipesTopic(WebSocketHub hub, IGameUnlockStateData unlockStateData)
        {
            _hub = hub;
            _unlockStateData = unlockStateData;

            // ClientGameUnlockStateData より後に購読登録されるため、ここでは更新後の状態を読める
            // Subscribed after ClientGameUnlockStateData, so the updated unlock state is visible here
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private string BuildJson()
        {
            var dto = new MachineRecipesDto { Recipes = new List<MachineRecipeDto>() };
            var unlockInfos = _unlockStateData.MachineRecipeUnlockStateInfos;
            foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                // dict に無いレシピはロック扱いに倒して例外を避ける
                // Treat recipes missing from the dict as locked to avoid exceptions
                if (!unlockInfos.TryGetValue(recipe.MachineRecipeGuid, out var unlockInfo)) continue;
                if (!unlockInfo.IsUnlocked) continue;

                var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
                var blockName = MasterHolder.BlockMaster.GetBlockMaster(blockId).Name;

                dto.Recipes.Add(new MachineRecipeDto
                {
                    RecipeGuid = recipe.MachineRecipeGuid.ToString(),
                    BlockGuid = recipe.BlockGuid.ToString(),
                    BlockId = blockId.AsPrimitive(),
                    BlockName = blockName,
                    Time = recipe.Time,
                    InputItems = BuildInputItems(recipe),
                    OutputItems = BuildOutputItems(recipe),
                });
            }
            return WebUiJson.Serialize(dto);

            #region Internal

            List<RecipeItemDto> BuildInputItems(MachineRecipeMasterElement element)
            {
                var items = new List<RecipeItemDto>();
                foreach (var inputItem in element.InputItems)
                {
                    items.Add(new RecipeItemDto
                    {
                        ItemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid).AsPrimitive(),
                        Count = inputItem.Count,
                    });
                }
                return items;
            }

            List<RecipeItemDto> BuildOutputItems(MachineRecipeMasterElement element)
            {
                var items = new List<RecipeItemDto>();
                foreach (var outputItem in element.OutputItems)
                {
                    items.Add(new RecipeItemDto
                    {
                        ItemId = MasterHolder.ItemMaster.GetItemId(outputItem.ItemGuid).AsPrimitive(),
                        Count = outputItem.Count,
                    });
                }
                return items;
            }

            #endregion
        }
    }

    /// <summary>
    /// crafting.machine_recipes の配信 DTO
    /// Payload DTO for crafting.machine_recipes
    /// </summary>
    public class MachineRecipesDto
    {
        public List<MachineRecipeDto> Recipes;
    }

    public class MachineRecipeDto
    {
        public string RecipeGuid;
        public string BlockGuid;
        public int BlockId;
        public string BlockName;
        public double Time;
        public List<RecipeItemDto> InputItems;
        public List<RecipeItemDto> OutputItems;
    }

    public class RecipeItemDto
    {
        public int ItemId;
        public int Count;
    }
}
