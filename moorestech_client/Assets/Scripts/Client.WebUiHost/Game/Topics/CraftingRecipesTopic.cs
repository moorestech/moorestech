using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Ui.State.Recipe.Catalog;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Server.Event.EventReceive;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// crafting.recipes トピック: アンロック済みクラフトレシピ一覧。アンロックイベントで再配信
    /// crafting.recipes topic: unlocked craft recipes; republished on unlock events
    /// </summary>
    public class CraftingRecipesTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "crafting.recipes";

        private readonly WebSocketHub _hub;
        private readonly IGameUnlockStateData _unlockStateData;
        private readonly IDisposable _subscription;

        public CraftingRecipesTopic(WebSocketHub hub, IGameUnlockStateData unlockStateData)
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
            // 解放済みの判定と並びは State の Catalog に任せ、ここでは DTO へ写すだけ
            // The State catalog decides which recipes are unlocked and their order; this only maps them to DTOs
            var dto = new CraftRecipesDto { Recipes = new List<CraftRecipeDto>() };
            foreach (var recipe in CraftRecipeCatalog.BuildUnlocked(_unlockStateData))
            {
                var requiredItems = new List<RequiredItemDto>();
                foreach (var requiredItem in recipe.RequiredItems)
                {
                    requiredItems.Add(new RequiredItemDto { ItemId = requiredItem.ItemId.AsPrimitive(), Count = requiredItem.Count });
                }

                dto.Recipes.Add(new CraftRecipeDto
                {
                    RecipeGuid = recipe.RecipeGuid.ToString(),
                    ResultItemId = recipe.ResultItemId.AsPrimitive(),
                    ResultCount = recipe.ResultCount,
                    CraftTime = recipe.CraftTime,
                    RequiredItems = requiredItems,
                });
            }
            return WebUiJson.Serialize(dto);
        }
    }

    /// <summary>
    /// crafting.recipes の配信 DTO
    /// Payload DTO for crafting.recipes
    /// </summary>
    public class CraftRecipesDto
    {
        public List<CraftRecipeDto> Recipes;
    }

    public class CraftRecipeDto
    {
        public string RecipeGuid;
        public int ResultItemId;
        public int ResultCount;
        public double CraftTime;
        public List<RequiredItemDto> RequiredItems;
    }

    public class RequiredItemDto
    {
        public int ItemId;
        public int Count;
    }
}
