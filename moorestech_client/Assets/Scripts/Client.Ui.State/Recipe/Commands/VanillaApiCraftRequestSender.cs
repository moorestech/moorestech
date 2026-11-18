using System;
using Client.Game.InGame.Context;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// 本番のクラフト要求（ClientContext の送信専用 API。素材の所持はサーバーが検証する）
    /// Production craft request via the ClientContext send-only API; the server checks owned materials
    /// </summary>
    public sealed class VanillaApiCraftRequestSender : ICraftRequestSender
    {
        public void Send(Guid recipeGuid)
        {
            ClientContext.VanillaApi.SendOnly.Craft(recipeGuid);
        }
    }
}
