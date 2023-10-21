using System.Collections.Generic;

namespace Core.EnergySystem
{
    public static class EnergySegmentExtension
    {
        public static TSegment Merge<TSegment>(List<TSegment> segments) where TSegment : EnergySegment, new()
        {
            var newSegment = new TSegment();
            //1
            foreach (var electric in segments)
            {
                
                foreach (var generator in electric.Generators) newSegment.AddGenerator(generator.Value);

                
                foreach (var consumer in electric.Consumers) newSegment.AddEnergyConsumer(consumer.Value);

                
                foreach (var transformer in electric.EnergyTransformers) newSegment.AddEnergyTransformer(transformer.Value);
            }

            return newSegment;
        }
    }
}