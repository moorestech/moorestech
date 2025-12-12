using System;
using System.Collections.Generic;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     差分同期された接続情報を保持し、将来的な可視化へ備えるプレースホルダー
    ///     Placeholder component that listens to connection diffs and tracks active edges for future visualization
    /// </summary>
    public sealed class TrainRailObjectManager : MonoBehaviour
    {
        private readonly HashSet<(int fromId, int toId)> _activeConnections = new();
        private RailGraphConnectionNetworkHandler _connectionHandler;
        private bool _isSubscribed;

        private void Update()
        {
            if (_isSubscribed) return;
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            var handler = RailGraphConnectionNetworkHandler.Instance;
            if (handler == null) return;

            _connectionHandler = handler;
            _connectionHandler.ConnectionCreated += OnConnectionCreated;
            _isSubscribed = true;
        }

        private void OnDestroy()
        {
            if (_connectionHandler != null && _isSubscribed)
            {
                _connectionHandler.ConnectionCreated -= OnConnectionCreated;
            }
        }

        private void OnConnectionCreated(RailConnectionCreatedMessagePack message)
        {
            var key = (message.FromNodeId, message.ToNodeId);
            if (_activeConnections.Contains(key))
            {
                return;
            }

            _activeConnections.Add(key);
            // TODO: instantiate or update visual rail objects once coordinates are fully resolved on the client.
        }
    }
}
