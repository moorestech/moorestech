using System;
using Cysharp.Threading.Tasks;
using Server.Util.MessagePack;
using UnityEngine;

namespace MainGame.Network.Event
{
    public class ReceiveBlockStateChangeEvent
    {
        public event Action<BlockStateChangeProperties> OnStateChange;

        public async UniTask InvokeReceiveBlockStateChange(BlockStateChangeProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnStateChange?.Invoke(properties);
        }
    }

    public class BlockStateChangeProperties
    {
        public BlockStateChangeProperties(string currentState, string previousState, string currentStateData, Vector2MessagePack position)

        {
            CurrentState = currentState;
            PreviousState = previousState;
            CurrentStateData = currentStateData;
            Position = new Vector2Int((int)position.X, (int)position.Y);
        }

        public string CurrentState { get; }
        public string PreviousState { get; }
        public string CurrentStateData { get; }
        public Vector2Int Position { get; }
    }
}