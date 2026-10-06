namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    /// <summary>
    ///     見た目を焼くときに配置台帳を差し出す口。台帳を既に持つ側と、要求されて初めて作る側を同じ形で扱う
    ///     台帳の生成はワールド全体のpass-1に相当する重い処理なので、要否の判断は焼き手（キャッシュの当否を知る側）に委ねる
    ///     台帳と同じpass-1が決めた表示高さの手順(陸分類を含む)も一緒に返し、表示側で分類を作り直さない
    ///     The port handing a placement ledger to the visual bake, treating "already holds one" and "builds one on demand" alike
    ///     Building a ledger costs a whole-world pass-1, so whether it is needed is decided by the baker, which alone knows if the cache hit
    ///     The display-height policy settled by the same pass-1 (land classification included) comes along, so the display side never rebuilds it
    /// </summary>
    public interface IPlacementLedgerSource
    {
        GenerationRun Resolve();
    }
}
