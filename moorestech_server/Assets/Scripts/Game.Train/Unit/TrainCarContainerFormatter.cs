using System;
using MessagePack;
using MessagePack.Formatters;
using UnityEngine;

namespace Game.Train.Unit
{
    public class TrainCarContainerFormatter : IMessagePackFormatter<ITrainCarContainer>
    {
        public void Serialize(ref MessagePackWriter writer, ITrainCarContainer value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }
            
            var type = value.GetType();
            writer.WriteArrayHeader(2);
            writer.Write(type.AssemblyQualifiedName);
            MessagePackSerializer.Serialize(type, ref writer, value, options);
        }
        
        public ITrainCarContainer Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            
            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength != 2) throw new MessagePackSerializationException($"Invalid array length for ITrainCarContainer: expected 2, got {arrayLength}");
            
            var typeName = reader.ReadString();
            var type = Type.GetType(typeName!, true);
            
            if (!typeof(ITrainCarContainer).IsAssignableFrom(type)) throw new MessagePackSerializationException($"Type {typeName} does not implement ITrainCarContainer");
            
            var obj = MessagePackSerializer.Deserialize(type, ref reader, options);
            return (ITrainCarContainer)obj;
        }
    }
    
    public class TrainCarContainerResolver : IFormatterResolver
    {
        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(ITrainCarContainer))
            {
                return (IMessagePackFormatter<T>)new TrainCarContainerFormatter();
            }
            return null;
        }
    }
}