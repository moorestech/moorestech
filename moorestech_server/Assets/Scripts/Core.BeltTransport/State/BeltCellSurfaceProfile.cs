namespace Core.BeltTransport
{
    public readonly struct BeltCellSurfaceProfile
    {
        // CPUの座標とは独立した、セル原点からの表示面高さ。
        // Presentation surface heights relative to the cell origin, independent of CPU coordinates.
        public readonly float InputHeight, OutputHeight;
        public BeltCellSurfaceProfile(float inputHeight, float outputHeight)
        { InputHeight = inputHeight; OutputHeight = outputHeight; }
    }
}
