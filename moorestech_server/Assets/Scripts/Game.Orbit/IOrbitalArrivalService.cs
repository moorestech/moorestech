using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Game.Orbit
{
    // 打ち上げ口が軌道到着を伝える窓口。成果の量や積荷の種類による分岐はここだけが持つ
    // The single entry launch sites use to report orbital arrival; only this owns output amounts and per-payload branching
    public interface IOrbitalArrivalService
    {
        IObservable<OrbitalArrival> OnOrbitalArrival { get; }

        bool IsOrbitalPayload(ItemId itemId);

        // 到着させずに成果だけを返す。打ち上げ口は出力口に入りきるかをこれで確かめてから打ち上げる
        // Returns the outputs without arriving; launch sites check output space with it before launching
        IReadOnlyList<IItemStack> PreviewArrivalOutputs(ItemId payloadItemId, int payloadCount);

        // 成果を新しいスタックの並びで返し、到着を通知する。表に無い積荷・1便の整数倍でない個数は呼び出し側のバグとして例外
        // Returns the outputs as fresh stacks and publishes the arrival; unknown payloads or non-multiple counts are caller bugs and throw
        IReadOnlyList<IItemStack> Arrive(ItemId payloadItemId, int payloadCount);
    }
}
