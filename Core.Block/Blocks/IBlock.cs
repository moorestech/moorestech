using System;
using MessagePack;

namespace Core.Block.Blocks
{
    public interface IBlock
    {
        public int EntityId { get;}
        public int BlockId { get;}
        public ulong BlockHash { get;}
        public string GetSaveState();
        
        /// <summary>
        /// ブロックで何らかのステートが変化したときに呼び出されます
        /// 例えば、動いている機械が止まったなど
        /// クライアント側で稼働アニメーションや稼働音を実行するときに使用します
        /// </summary>
        public event Action<ChangedBlockState> OnBlockStateChange;
    }

    /// <summary>
    /// 変化したステートを通知するクラスです
    /// <see cref="CurrentState"/>や<see cref="PreviousState"/>がStringなのは、ブロックの種類によって表現したいステートが異なり、
    /// それらをパケットで取り扱う必要があるからです
    /// TODO シリアライズ可能なクラスにした方がいいかも？
    /// </summary>
    public class ChangedBlockState
    {
        public readonly string CurrentState;
        public readonly string PreviousState;
        
        /// <summary>
        /// ブロックのステートのデータ
        /// 各ブロックのよって必要なデータは違うため、このクラスを継承して派生させる
        /// </summary>
        public readonly ChangeBlockStateData CurrentStateDetailInfo;
        
        public ChangedBlockState(string currentState, string previousState, ChangeBlockStateData currentStateDetailInfo = null)
        {
            CurrentState = currentState;
            PreviousState = previousState;
            CurrentStateDetailInfo = currentStateDetailInfo;
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public abstract class ChangeBlockStateData{}
}