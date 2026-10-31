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
        ///    開始前・完了後の出力空き不足
        ///    Output-space shortage before starting or after completing a job
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