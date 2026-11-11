using System;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// 検証済みのクラフト要求をサーバーへ送る口
    /// Port sending an already-validated craft request to the server
    /// </summary>
    public interface ICraftRequestSender
    {
        void Send(Guid recipeGuid);
    }
}
