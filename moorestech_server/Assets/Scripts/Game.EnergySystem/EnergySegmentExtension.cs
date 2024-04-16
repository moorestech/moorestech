using System.Collections.Generic;
using Core.EnergySystem;

namespace Game.EnergySystem
{
    public static class EnergySegmentExtension
    {
        public static TSegment Merge<TSegment>(List<TSegment> segments) where TSegment : EnergySegment, new()
        {
            var newSegment = new TSegment();
            //受け取った電気セグメントを1つずつ結合していく
            foreach (var electric in segments)
            {
                //エネルギー供給者の結合
                foreach (KeyValuePair<int, IElectricGenerator> generator in electric.Generators) newSegment.AddGenerator(generator.Value);

                //エネルギー消費者の結合
                foreach (KeyValuePair<int, IElectricConsumer> consumer in electric.Consumers) newSegment.AddEnergyConsumer(consumer.Value);

                //エネルギー輸送の結合
                foreach (KeyValuePair<int, IElectricTransformer> transformer in electric.EnergyTransformers)
                    newSegment.AddEnergyTransformer(transformer.Value);
            }

            return newSegment;
        }
    }
}