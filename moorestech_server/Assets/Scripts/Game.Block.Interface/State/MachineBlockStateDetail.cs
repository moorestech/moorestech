using System;
using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Interface.State
{
    [Serializable]
    [MessagePackObject]
    public class MachineBlockStateDetail
    {
        public const string BlockStateDetailKey = "MachineBlock";
        
        /// <summary>
        ///     アイテムの作成がどれくらい進んでいるかを表す
        /// </summary>
        [Key(0)] public float ProcessingRate;
        
        /// <summary>
        ///    実行中のレシピのGUID
        /// </summary>
        [Key(1)] public string MachineRecipeGuid;

        /// <summary>
        ///    選択レシピGUID(未選択=Empty)
        /// </summary>
        [Key(2)] public string SelectedRecipeGuid;

        /// <summary>
        ///    出力先の空きが足りず次の加工を始められない・完了を払い出せないか
        ///    Whether the machine waits because the output side lacks space to start or pay out a job
        /// </summary>
        [Key(3)] public bool IsOutputSpaceShort;

        public MachineBlockStateDetail(float processingRate, Guid machineRecipeGuid, Guid selectedRecipeGuid, bool isOutputSpaceShort)
        {
            IsOutputSpaceShort = isOutputSpaceShort;
            ProcessingRate = processingRate;
            MachineRecipeGuid = machineRecipeGuid.ToString();
            SelectedRecipeGuid = selectedRecipeGuid.ToString();
        }

        public static BlockStateDetail CreateState(float processingRate, Guid machineRecipeGuid, Guid selectedRecipeGuid, bool isOutputSpaceShort)
        {
            var stateDetail = new MachineBlockStateDetail(processingRate, machineRecipeGuid, selectedRecipeGuid, isOutputSpaceShort);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MachineBlockStateDetail() { }
    }
}