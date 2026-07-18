using System.Collections.Generic;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public static class TutorialAnchorIdMapper
    {
        private static readonly IReadOnlyDictionary<string, string> UiAnchors =
            new Dictionary<string, string>
            {
                { "craftButton", "recipe.craft-button" },
            };

        public static string FromUiObjectId(string uiObjectId)
        {
            return UiAnchors[uiObjectId];
        }

        public static string FromItemId(int itemId)
        {
            return $"recipe.item-{itemId}";
        }
    }
}
