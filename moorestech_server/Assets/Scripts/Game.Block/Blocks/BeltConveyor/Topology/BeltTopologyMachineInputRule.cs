namespace Game.Block.Blocks.BeltConveyor.Topology
{
    // ベルトのマスが機械から受け取る入力を絞る規則。外される状況は通常プレイで起きるのでログは出さない
    // Rule that narrows a belt cell's inputs from machines; exclusion happens in normal play, so it never logs
    // ベルト入力が1本でもあれば機械入力をすべて外し、ベルト入力が無く機械入力が複数なら座標X→Y→Zが最小の1本だけ残す
    // Any belt input removes every machine input; with no belt input and several machine inputs, only the smallest X-Y-Z partner stays
    // ベルトから機械への出力は対象外。外された機械の出力はマス一覧のどこにも現れない
    // Belt-to-machine outputs are never restricted; a removed machine's output appears nowhere in the cell list
    internal static class BeltTopologyMachineInputRule
    {
        // 並び替え済みの入力配列を受け取り、何も外さないときは同じ配列をそのまま返す
        // Takes the sorted input array and returns the very same array when nothing is removed
        internal static BeltTopologyConnection[] Apply(BeltTopologyConnection[] inputs)
        {
            var beltInputCount = 0;
            var machineInputCount = 0;
            var preferredMachineIndex = -1;
            for (var i = 0; i < inputs.Length; i++)
            {
                if (inputs[i].PartnerKind == BeltTopologyPartnerKind.Belt)
                {
                    beltInputCount++;
                    continue;
                }
                machineInputCount++;
                if (preferredMachineIndex < 0 || BeltTopologyGeometry.ComparePosition(inputs[i].PartnerCell, inputs[preferredMachineIndex].PartnerCell) < 0)
                    preferredMachineIndex = i;
            }
            if (machineInputCount == 0 || beltInputCount == 0 && machineInputCount == 1) return inputs;
            if (beltInputCount == 0) return new[] { inputs[preferredMachineIndex] };

            // ベルト入力だけを元の並び順のまま残す
            // Keep only the belt inputs in their original order
            var beltInputs = new BeltTopologyConnection[beltInputCount];
            var filled = 0;
            for (var i = 0; i < inputs.Length; i++)
                if (inputs[i].PartnerKind == BeltTopologyPartnerKind.Belt) beltInputs[filled++] = inputs[i];
            return beltInputs;
        }
    }
}
