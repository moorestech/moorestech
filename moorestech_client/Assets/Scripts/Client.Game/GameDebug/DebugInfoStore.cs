using System;
using Client.Game.InGame.Block;
using UniRx;

namespace Client.Game.GameDebug
{
    public class DebugInfoStore
    {
        public static bool EnableBlockDebugMode { get; set; }
        
        public static IObservable<BlockGameObject> OnClickBlock => _onClickBlock;
        private static readonly Subject<BlockGameObject> _onClickBlock = new();
        
        public static void InvokeClickBlock(BlockGameObject block)
        {
            _onClickBlock.OnNext(block);
        }
    }
}