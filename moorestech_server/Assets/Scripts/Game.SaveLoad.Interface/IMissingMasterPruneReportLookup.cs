namespace Game.SaveLoad.Interface
{
    /// <summary>除去件数の読み取り面。書き込みはDI注入したstoreだけが持つ</summary>
    /// <summary>Read face for the prune counts; only the DI-injected store can write them</summary>
    public interface IMissingMasterPruneReportLookup
    {
        MissingMasterPruneReport Report { get; }
    }
}
