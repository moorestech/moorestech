using System.Collections.Generic;

namespace Core.Electric
{
    public class ElectricMergeService
    {
        public ElectricSegment Merge(List<ElectricSegment> electricSegments)
        {
            var electricSegment = new ElectricSegment();
            //受け取った電気セグメントを1つずつ結合していく
            foreach (var electric in electricSegments)
            {
                //発電機の結合
                foreach (var generator in electric.GetGenerators())
                {
                    electricSegment.AddGenerator(generator.Value);
                }

                //電気ブロックの結合
                foreach (var electricBlock in electric.GetElectrics())
                {
                    electricSegment.AddBlockElectric(electricBlock.Value);
                }

                //電柱の結合
                foreach (var electricPole in electric.GetElectricPoles())
                {
                    electricSegment.AddElectricPole(electricPole.Value);
                }
            }

            return electricSegment;
        }
    }
}