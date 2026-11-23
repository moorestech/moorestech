using System.Collections.Generic;
using System.Linq;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 発射台・拠点の積荷の選択。選んだ積荷・今の便の積荷・選べる一覧と、見出しと「次の便から」の判定（ADR 0106）
    /// Cargo selection of a launch pad or elevator base: selected cargo, current flight, selectable list, and the headline / next-flight decisions (ADR 0106)
    /// </summary>
    public sealed class OrbitalCargoDetail
    {
        public ItemId? SelectedCargoItemId { get; }
        // 今の便（発射台は積み終えたロケット、拠点は昇降中の便）。無ければ null
        // The current flight (the loaded rocket on a pad, the climbing flight on a base); null when none
        public ItemId? CurrentFlightCargoItemId { get; }
        // 一覧に出す種類（運ぶ種類のうち解放済み。運ぶ順のまま）
        // Kinds listed for selection (carried and unlocked, in carried order)
        public IReadOnlyList<OrbitalCargoChoice> SelectableChoices { get; }
        // 一覧の行に出す 1 便の所要秒（発射台は建造、拠点は昇降）
        // Seconds per flight on list rows (build time on a pad, climb time on a base)
        public double FlightSeconds { get; }
        private readonly IReadOnlyList<OrbitalCargoChoice> _carriedChoices;

        public bool IsSelected => SelectedCargoItemId.HasValue;

        public OrbitalCargoDetail(ItemId? selectedCargoItemId, ItemId? currentFlightCargoItemId, IReadOnlyList<OrbitalCargoChoice> carriedChoices, IReadOnlyCollection<ItemId> unlockedPayloadItemIds, double flightSeconds)
        {
            SelectedCargoItemId = selectedCargoItemId;
            CurrentFlightCargoItemId = currentFlightCargoItemId;
            _carriedChoices = carriedChoices;
            SelectableChoices = carriedChoices.Where(choice => unlockedPayloadItemIds.Contains(choice.PayloadItemId)).ToList();
            FlightSeconds = flightSeconds;
        }

        // 見出しは今の便があればそれ、無ければ選んだ積荷（ADR 0106 の 4「画面は今の便の積荷と成果を出す」）
        // The headline is the current flight when there is one, otherwise the selection (ADR 0106 §4)
        public bool TryGetHeadline(out OrbitalCargoChoice choice)
        {
            return TryGetCarried(CurrentFlightCargoItemId ?? SelectedCargoItemId, out choice);
        }

        // 今の便と選んだ積荷が両方あって違うときだけ「次の便から」を出す
        // Show "from the next flight" only when both the current flight and the selection exist and differ
        public bool TryGetNextFlight(out OrbitalCargoChoice choice)
        {
            choice = null;
            if (!CurrentFlightCargoItemId.HasValue || !IsSelected) return false;
            if (IsSelectedChoice(CurrentFlightCargoItemId.Value)) return false;
            return TryGetCarried(SelectedCargoItemId, out choice);
        }

        public bool TryGetSelected(out OrbitalCargoChoice choice)
        {
            return TryGetCarried(SelectedCargoItemId, out choice);
        }

        public bool IsSelectedChoice(ItemId payloadItemId)
        {
            return SelectedCargoItemId.HasValue && SelectedCargoItemId.Value == payloadItemId;
        }

        private bool TryGetCarried(ItemId? payloadItemId, out OrbitalCargoChoice choice)
        {
            choice = null;
            if (!payloadItemId.HasValue) return false;
            foreach (var carried in _carriedChoices)
            {
                if (carried.PayloadItemId != payloadItemId.Value) continue;
                choice = carried;
                return true;
            }

            // サーバーは運ぶ種類しか選ばせないので、ここへ来るのは状態の食い違い。見出しを出さず開発者へ知らせる
            // The server only accepts carried kinds, so reaching here is a state mismatch; omit the headline and tell the developer
            Debug.LogError($"[OrbitalCargoDetail] ブロックが運ばない積荷が状態に入っています payload={payloadItemId.Value.AsPrimitive()}");
            return false;
        }
    }
}
