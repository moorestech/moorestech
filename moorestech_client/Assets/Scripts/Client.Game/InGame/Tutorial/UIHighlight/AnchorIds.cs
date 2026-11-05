using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    // チュートリアルアンカーIDのC#側の唯一の正本。共存中はWebのanchorIds.tsと突き合わせる
    // C# single source of truth for tutorial anchor IDs; cross-checked against Web's anchorIds.ts while both coexist
    public static class AnchorIds
    {
        public const string PlacementHud = "placement.hud";
        public const string DeleteHud = "delete.hud";
        public const string GameCrosshair = "game.crosshair";
        public const string TrainHudStatus = "train-hud.status";
        public const string PauseMenu = "pause.menu";
        public const string PauseSave = "pause.save";
        public const string PauseBack = "pause.back";
        public const string PauseSettings = "pause.settings";
        public const string PauseBugReport = "pause.bug-report";
        public const string PauseBackToTop = "pause.back-to-top";
        public const string InventoryCloseButton = "inventory.close-button";
        public const string ChallengeCurrentHud = "challenge.current-hud";
        public const string RecipeCraftButton = "recipe.craft-button";
        public const string MiningHud = "mining.hud";
        public const string ChallengePanel = "challenge.panel";
        public const string ChallengeCategories = "challenge.categories";
        public const string HotbarHud = "hotbar.hud";
        public const string EquipmentHud = "equipment.hud";
        public const string EquipmentSelectedSlot = "equipment.selected-slot";

        public static readonly IReadOnlyList<string> StaticIds = new[]
        {
            PlacementHud, DeleteHud, GameCrosshair, TrainHudStatus, PauseMenu, PauseSave, PauseBack,
            PauseSettings, PauseBugReport, PauseBackToTop, InventoryCloseButton, ChallengeCurrentHud,
            RecipeCraftButton, MiningHud, ChallengePanel, ChallengeCategories, HotbarHud, EquipmentHud,
            EquipmentSelectedSlot,
        };

        // マスタデータはこの書式のanchorIdを直書きする
        // Master data writes anchor IDs in these forms directly
        public const string ResearchNodePrefix = "research.node-";
        public const string RecipeItemPrefix = "recipe.item-";
        public const string BuildMenuEntryPrefix = "build-menu.entry-";
        public const string ChallengeNodePrefix = "challenge.node-";
        public const string InventoryItemPrefix = "inventory.item-";
        public const string EquipmentSlotPrefix = "equipment.slot-";

        public static readonly IReadOnlyList<string> DynamicPrefixes = new[]
        {
            ResearchNodePrefix, RecipeItemPrefix, BuildMenuEntryPrefix,
            ChallengeNodePrefix, InventoryItemPrefix, EquipmentSlotPrefix,
        };

        public static string ResearchNode(string guid) => Validate($"{ResearchNodePrefix}{guid}".ToLowerInvariant());

        public static string RecipeItem(int itemId) => Validate($"{RecipeItemPrefix}{itemId}");

        public static string BuildMenuEntry(string kind, string id) => Validate($"{BuildMenuEntryPrefix}{kind}-{id}".ToLowerInvariant());

        public static string ChallengeNode(string guid) => Validate($"{ChallengeNodePrefix}{guid}".ToLowerInvariant());

        public static string InventoryItem(string itemGuid) => Validate($"{InventoryItemPrefix}{itemGuid}".ToLowerInvariant());

        public static string EquipmentSlot(int index) => Validate($"{EquipmentSlotPrefix}{index}");

        // 空白入りIDはクラス名トークンとして解決しない。Webの実機と同じく警告してそのまま返す（開発者はこのログで気づく）
        // A whitespace-bearing ID never resolves as a class token; like Web's shipped behavior, log and return it as is (developers notice via this log)
        private static string Validate(string anchorId)
        {
            foreach (var character in anchorId)
            {
                if (!char.IsWhiteSpace(character)) continue;
                Debug.LogError($"[AnchorIds] anchor id must not contain whitespace: \"{anchorId}\"");
                break;
            }
            return anchorId;
        }
    }
}
