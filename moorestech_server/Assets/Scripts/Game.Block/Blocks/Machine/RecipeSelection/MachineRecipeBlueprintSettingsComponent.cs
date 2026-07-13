using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     レシピ選択をBPコピーする設定(共用)
    ///     Blueprint settings component copying the machine recipe selection (shared by vanilla and clean-room)
    /// </summary>
    public class MachineRecipeBlueprintSettingsComponent : IBlockBlueprintSettings
    {
        public const string SettingsKey = "MachineRecipeSelection";
        public string BlueprintSettingsKey => SettingsKey;

        private readonly IMachineRecipeSelectorComponent _selector;

        public MachineRecipeBlueprintSettingsComponent(IMachineRecipeSelectorComponent selector)
        {
            _selector = selector;
        }

        public string GetBlueprintSettingsJson()
        {
            var guid = _selector.SelectedRecipeGuid;
            return JsonConvert.SerializeObject(new SettingsJsonObject
            {
                SelectedRecipeGuid = guid == Guid.Empty ? null : guid.ToString(),
            });
        }

        public void ApplyBlueprintSettingsJson(string json)
        {
            var settings = JsonConvert.DeserializeObject<SettingsJsonObject>(json);
            if (settings?.SelectedRecipeGuid == null) return;
            if (!Guid.TryParse(settings.SelectedRecipeGuid, out var guid)) return;

            // 別ブロックのレシピ・未アンロックはSetSelectedRecipe内の検証で弾かれ、未選択のまま設置される
            // Recipes of another block or locked ones are rejected by SetSelectedRecipe and the block stays unselected
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(guid);
            if (recipe == null) return;

            // 適用は設置直後（必ずIdle・ジョブ無し）のため返却は発生しない。溢れ先はダミーで良い
            // Applied right after creation (always idle, no job), so no refund occurs; a dummy overflow suffices
            var dummyOverflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 0);
            _selector.SetSelectedRecipe(recipe, dummyOverflow);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }

        private class SettingsJsonObject
        {
            [JsonProperty("selectedRecipeGuid")] public string SelectedRecipeGuid;
        }
    }
}
