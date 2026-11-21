using Core.Inventory;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Blocks.OrbitalCargo
{
    /// <summary>
    ///     積荷の選択をブループリントでコピーする設定（発射台・拠点で共用。ADR 0106 5）
    ///     Blueprint settings copying the cargo selection, shared by launch pad and base (ADR 0106 5)
    /// </summary>
    public class OrbitalCargoBlueprintSettingsComponent : IBlockBlueprintSettings
    {
        public const string SettingsKey = "OrbitalCargoSelection";
        public string BlueprintSettingsKey => SettingsKey;

        private readonly IOrbitalCargoSelectorComponent _selector;

        public OrbitalCargoBlueprintSettingsComponent(IOrbitalCargoSelectorComponent selector)
        {
            _selector = selector;
        }

        public string GetBlueprintSettingsJson()
        {
            return JsonConvert.SerializeObject(new SettingsJsonObject
            {
                SelectedCargoItemGuid = _selector.Selection.ToItemGuidTextOrNull(),
            });
        }

        public void ApplyBlueprintSettingsJson(string json)
        {
            var settings = JsonConvert.DeserializeObject<SettingsJsonObject>(json);
            // silent-ok: 未選択でコピーした台は未選択のまま置く / プレイヤーは台の「積荷を選んでいない」表示で気づく
            if (settings?.SelectedCargoItemGuid == null) return;
            if (!OrbitalCargoItemGuid.TryParseExistingItemGuid(settings.SelectedCargoItemGuid, out var payloadItemId))
            {
                Debug.LogWarning($"ブループリントの積荷 {settings.SelectedCargoItemGuid} が今のマスタに無いため、未選択のまま設置します");
                return;
            }

            // 未解放・この台が運ばない種類は SelectCargo が理由をログに出して拒否する
            // SelectCargo logs the reason and rejects locked kinds and kinds this site cannot carry
            // 設置直後で入口は空のため返却は起きない。返却先は空きの無いダミーでよい
            // The inlet is empty immediately after placement, so nothing is refunded; a zero-slot dummy suffices
            var noRefund = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 0);
            _selector.SelectCargo(payloadItemId, noRefund);
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }

        private class SettingsJsonObject
        {
            [JsonProperty("selectedCargoItemGuid")] public string SelectedCargoItemGuid;
        }
    }
}
