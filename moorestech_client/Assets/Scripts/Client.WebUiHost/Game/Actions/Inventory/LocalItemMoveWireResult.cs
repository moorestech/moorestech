using System;
using Client.Game.InGame.UI.Inventory.Main;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// コントローラの移動の結果を Web の inventory.move_item の結果へ写す（何もしない移動は従来どおり成功。Web ホストの写しはここ 1 か所）
    /// Maps the controller's move result to the Web inventory.move_item result (no-effect stays a success; the Web host's single mapping)
    /// </summary>
    public static class LocalItemMoveWireResult
    {
        public static ActionResult ToActionResult(LocalItemMoveResult result)
        {
            return result switch
            {
                LocalItemMoveResult.Moved => ActionResult.Success(),
                LocalItemMoveResult.NoEffect => ActionResult.Success(),
                LocalItemMoveResult.SourceEmpty => ActionResult.Fail("empty_slot"),
                LocalItemMoveResult.SourceCountShort => ActionResult.Fail("insufficient_count"),
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
            };
        }
    }
}
