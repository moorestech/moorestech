using System.Collections.Generic;

namespace Core.EnergySystem
{
    public static class EnergySegmentExtension
    {
        public static EnergySegment Merge(List<EnergySegment> segments)
        {
            var electricSegment = new EnergySegment();
            //受け取った電気セグメントを1つずつ結合していく
            foreach (var electric in segments)
            {
                //エネルギー供給者の結合
                foreach (var generator in electric.Generators)
                {
                    electricSegment.AddGenerator(generator.Value);
                }

                //エネルギー消費者の結合
                foreach (var consumer in electric.Consumers)
                {
                    electricSegment.AddEnergyConsumer(consumer.Value);
                }

                //エネルギー輸送の結合
                foreach (var transformer in electric.EnergyTransformers)
                {
                    electricSegment.AddEnergyTransformer(transformer.Value);
                }
            }

            return electricSegment;
        }

        public static EnergySegment CreateMergedSegment(this EnergySegment self, EnergySegment target)
        {
            return Merge(new List<EnergySegment> {self, target});
        }
    }
}