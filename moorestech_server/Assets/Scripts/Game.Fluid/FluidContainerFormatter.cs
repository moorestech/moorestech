using Core.Master;
using MessagePack;
using MessagePack.Formatters;

namespace Game.Fluid
{
    public class FluidContainerFormatter : IMessagePackFormatter<FluidContainer>
    {
        public void Serialize(ref MessagePackWriter writer, FluidContainer value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(3);
            writer.Write(value.Capacity);
            writer.Write(value.Amount);
            options.Resolver.GetFormatterWithVerify<FluidId>().Serialize(ref writer, value.FluidId, options);
        }

        public FluidContainer Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength != 3) throw new MessagePackSerializationException($"Invalid array length for FluidContainer: expected 3, got {arrayLength}");

            var capacity = reader.ReadDouble();
            var amount = reader.ReadDouble();
            var fluidId = options.Resolver.GetFormatterWithVerify<FluidId>().Deserialize(ref reader, options);

            var container = new FluidContainer(capacity);
            container.Amount = amount;
            container.FluidId = fluidId;
            return container;
        }
    }

    public class FluidContainerResolver : IFormatterResolver
    {
        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(FluidContainer))
            {
                return (IMessagePackFormatter<T>)(object)new FluidContainerFormatter();
            }
            return null;
        }
    }
}
