using System.Collections.Generic;

namespace Core.EnergySystem
{
    public static class EnergySegmentExtension
    {
        public static TSegment Merge<TSegment>(List<TSegment> segments) where TSegment : EnergySegment,new ()
        {
            var newSegment = new TSegment();
            //受け取った電気セグメントを1つずつ結合していく
            foreach (var electric in segments)
            {
                //エネルギー供給者の結合
                foreach (var generator in electric.Generators)
                {
                    newSegment.AddGenerator(generator.Value);
                }

                //エネルギー消費者の結合
                foreach (var consumer in electric.Consumers)
                {
                    newSegment.AddEnergyConsumer(consumer.Value);
                }

                //エネルギー輸送の結合
                foreach (var transformer in electric.EnergyTransformers)
                {
                    newSegment.AddEnergyTransformer(transformer.Value);
                }
            }

            return newSegment;
        }
    }
}