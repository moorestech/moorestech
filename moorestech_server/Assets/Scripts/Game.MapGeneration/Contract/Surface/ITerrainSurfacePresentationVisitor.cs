namespace Game.MapGeneration.Surface
{
    public interface ITerrainSurfacePresentationVisitor
    {
        void VisitLegacy();
        void VisitGrounded(SurfaceEnvelope envelope);
    }
}
