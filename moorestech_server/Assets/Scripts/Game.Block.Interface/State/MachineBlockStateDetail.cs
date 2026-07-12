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
        ///    選択中のレシピのGUID（未選択はGuid.Empty）
        /// </summary>
        [Key(2)] public string SelectedRecipeGuid;

        public MachineBlockStateDetail(float processingRate, Guid machineRecipeGuid, Guid selectedRecipeGuid)
        {
            ProcessingRate = processingRate;
            MachineRecipeGuid = machineRecipeGuid.ToString();
            SelectedRecipeGuid = selectedRecipeGuid.ToString();
        }

        public static BlockStateDetail CreateState(float processingRate, Guid machineRecipeGuid, Guid selectedRecipeGuid)
        {
            var stateDetail = new MachineBlockStateDetail(processingRate, machineRecipeGuid, selectedRecipeGuid);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MachineBlockStateDetail() { }
    }
}