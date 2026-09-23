using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Client.Game.InGame.BeltSegment.Rendering
{
    internal sealed class BeltDrawBuffers : IDisposable
    {
        internal readonly GraphicsBuffer Cells, Entries, Routes, Kinds, RawPositions, Positions, Counts, Offsets, Cursors, Arguments;
        internal BeltDrawBuffers(BeltDrawLayout layout, int capacity, int[] kinds, Mesh mesh)
        {
            bool initialized = false;
            try
            {
                Cells = Upload(layout.Cells); Entries = Upload(layout.Entries); Routes = Upload(layout.Routes);
                Kinds = Upload(kinds); RawPositions = Create<Vector4>(capacity); Positions = Create<Vector4>(capacity);
                Counts = Create<uint>(kinds.Length); Offsets = Create<uint>(kinds.Length); Cursors = Create<uint>(kinds.Length);
                Arguments = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, Math.Max(1, kinds.Length), GraphicsBuffer.IndirectDrawIndexedArgs.size);
                var args = new GraphicsBuffer.IndirectDrawIndexedArgs[Math.Max(1, kinds.Length)];
                for (int i = 0; i < args.Length; i++) args[i].indexCountPerInstance = mesh.GetIndexCount(0);
                Arguments.SetData(args);
                initialized = true;
            }
            finally { if (!initialized) Dispose(); }
            #region Internal
            static GraphicsBuffer Create<T>(int count) where T : struct => new(GraphicsBuffer.Target.Structured, Math.Max(1, count), Marshal.SizeOf<T>());
            static GraphicsBuffer Upload<T>(T[] data) where T : struct
            {
                var buffer = Create<T>(data.Length);
                bool transferred = false;
                try
                {
                    if (0 < data.Length) buffer.SetData(data);
                    transferred = true;
                    return buffer;
                }
                finally { if (!transferred) buffer.Dispose(); }
            }
            #endregion
        }
        public void Dispose()
        {
            Cells?.Dispose(); Entries?.Dispose(); Routes?.Dispose(); Kinds?.Dispose(); RawPositions?.Dispose();
            Positions?.Dispose(); Counts?.Dispose(); Offsets?.Dispose(); Cursors?.Dispose(); Arguments?.Dispose();
        }
    }
}
