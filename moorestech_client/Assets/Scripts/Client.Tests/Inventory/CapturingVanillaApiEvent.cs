using System;
using System.Collections.Generic;
using Client.Network.API;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Inventory
{
    /// <summary>
    ///     購読されたタグとハンドラを保持し、テストからタグ指定で配信できるイベント口
    ///     Event port that keeps subscribed tags and handlers so a test can dispatch by tag
    /// </summary>
    public class CapturingVanillaApiEvent : IVanillaApiEvent
    {
        private readonly Dictionary<string, Action<byte[]>> _handlers = new();

        public IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction)
        {
            _handlers.Add(tag, responseAction);
            return Disposable.Empty;
        }

        public void Dispatch(string tag, byte[] payload)
        {
            Assert.IsTrue(_handlers.ContainsKey(tag), $"no handler subscribed for {tag}");
            _handlers[tag](payload);
        }
    }
}
