using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem
{
    public class BlockSlopeDeformationType
    {
        private static readonly List<string> deformationTypes = new()
        {
            "BeltConveyor",
        };
        
        
        /// <summary>
        ///     傾斜によって角度や大きさが変わるブロックかどうかを取得するフラグ
        ///     ベルトコンベアなど変形するものはtrue、それ以外の通常のブロックはfalse
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsDeformation(string type)
        {
            return deformationTypes.Contains(type);
        }
    }
}