using System;
using System.Collections.Generic;
using Client.Ui.State.Recipe.Commands;

namespace Client.Tests.Ui.State.Recipe.Screen
{
    /// <summary>
    /// 送ったクラフト要求を記録するだけの偽物
    /// Fake that only records the craft requests sent
    /// </summary>
    public sealed class RecordingCraftRequestSender : ICraftRequestSender
    {
        public List<Guid> Sent { get; } = new();

        public void Send(Guid recipeGuid)
        {
            Sent.Add(recipeGuid);
        }
    }
}
