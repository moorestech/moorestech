using System;
using Cysharp.Threading.Tasks;
using Server.Util.MessagePack;
using UnityEngine;

namespace MainGame.Network.Event
{
    public class ReceiveBlockStateChangeEvent
    {
        public event Action<BlockStateChangeProperties> OnStateChange;

        internal async UniTask  InvokeReceiveBlockStateChange(BlockStateChangeProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnStateChange?.Invoke(properties);
        }     
    }
    
    public class BlockStateChangeProperties
    {
        public string CurrentState { get; }
        public string PreviousState { get;  }
        public byte[] CurrentStateData { get;  }
        public Vector2 Position { get;  }
        
        public BlockStateChangeProperties(string currentState, string previousState, byte[] currentStateData, Vector2MessagePack position)
        
        {
            CurrentState = currentState;
            PreviousState = previousState;
            CurrentStateData = currentStateData;
            Position = new Vector2(position.X,position.Y);
        }

    }
}