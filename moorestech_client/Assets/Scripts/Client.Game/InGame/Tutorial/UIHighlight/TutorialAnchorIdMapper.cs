using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public static class TutorialAnchorIdMapper
    {
        // 動的アンカーIDのprefix。Web側TutorialAnchorDynamicPrefixesと対応する
        // Dynamic anchor ID prefix; must mirror Web's TutorialAnchorDynamicPrefixes
        public const string ItemAnchorPrefix = "recipe.item-";

        // マスタ側uiObjectIdの動的書式「種別:GUID」のprefix。正本はCore.Master.TutorialUiObjectId
        // Prefixes of the master-side dynamic uiObjectId form "kind:GUID"; sourced from Core.Master.TutorialUiObjectId
        public const string BuildMenuBlockObjectIdPrefix = TutorialUiObjectId.BuildMenuBlockPrefix;
        public const string ResearchNodeObjectIdPrefix = TutorialUiObjectId.ResearchNodePrefix;

        private static readonly IReadOnlyDictionary<string, string> UiAnchors =
            new Dictionary<string, string>
            {
                { "craftButton", "recipe.craft-button" },
                { "challengeHud", "challenge.current-hud" },
                { "hotbar", "hotbar.hud" },
            };

        // 未知のキー・書式不正はfalseを返す
        // Returns false for unknown or malformed keys
        public static bool TryFromUiObjectId(string uiObjectId, out string anchorId)
        {
            // 動的対象はGUIDを小文字化してWeb側の動的anchor生成規則へ揃える
            // Dynamic targets lower-case the GUID to match the web-side dynamic anchor rules
            if (uiObjectId.StartsWith(BuildMenuBlockObjectIdPrefix, StringComparison.Ordinal))
            {
                if (!Guid.TryParse(uiObjectId.Substring(BuildMenuBlockObjectIdPrefix.Length), out var blockGuid))
                {
                    anchorId = null;
                    return false;
                }
                anchorId = $"build-menu.entry-block-{blockGuid.ToString().ToLowerInvariant()}";
                return true;
            }

            if (uiObjectId.StartsWith(ResearchNodeObjectIdPrefix, StringComparison.Ordinal))
            {
                if (!Guid.TryParse(uiObjectId.Substring(ResearchNodeObjectIdPrefix.Length), out var researchGuid))
                {
                    anchorId = null;
                    return false;
                }
                anchorId = $"research.node-{researchGuid.ToString().ToLowerInvariant()}";
                return true;
            }

            return UiAnchors.TryGetValue(uiObjectId, out anchorId);
        }

        public static string FromItemId(int itemId)
        {
            return $"{ItemAnchorPrefix}{itemId}";
        }

        // Web側フィクスチャとの突合テスト用に、静的マッピングの出力アンカーID全件を公開する
        // Exposes every statically mapped anchor ID for the parity test against the Web-side fixture
        public static IReadOnlyCollection<string> AllMappedAnchorIds => UiAnchors.Values.ToArray();

        // Core.Master.TutorialUiObjectId（正本）との突合テスト用に、マッパーが扱う静的キー全件を公開する
        // Exposes every static key handled by the mapper, for the parity test against Core.Master.TutorialUiObjectId (the source of truth)
        public static IReadOnlyCollection<string> KnownStaticKeys => UiAnchors.Keys.ToArray();
    }
}
