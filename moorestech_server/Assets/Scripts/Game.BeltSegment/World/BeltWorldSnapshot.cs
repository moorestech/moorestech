namespace Game.BeltSegment
{
    public sealed class BeltWorldSnapshot
    {
        public readonly BeltStreamPosition Position;
        public readonly ulong Generation;
        public readonly BeltReplaySnapshot Simulation;
        public readonly BeltRoute[] Routes;
        public BeltWorldSnapshot(BeltStreamPosition position, ulong generation, BeltReplaySnapshot simulation, BeltRoute[] routes)
        {
            Position = position; Generation = generation; Simulation = simulation;
            Routes = new BeltRoute[routes.Length];
            for (int i = 0; i < routes.Length; i++) Routes[i] = new BeltRoute(routes[i].Cells, routes[i].EntryCells);
        }
    }
}
