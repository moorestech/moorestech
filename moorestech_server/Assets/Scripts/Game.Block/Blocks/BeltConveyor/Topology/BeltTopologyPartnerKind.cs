namespace Game.Block.Blocks.BeltConveyor.Topology
{
    // 接続相手がベルトのマスか、ベルト以外のブロック（機械）か
    // Whether the connection partner is a belt cell or a non-belt block (machine)
    public enum BeltTopologyPartnerKind
    {
        Belt,
        Machine
    }
}
