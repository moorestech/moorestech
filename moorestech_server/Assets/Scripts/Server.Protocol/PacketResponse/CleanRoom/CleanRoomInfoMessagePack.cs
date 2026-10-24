using System;
using System.Collections.Generic;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public enum CleanRoomInfoResponseKind
    {
        Enclosed,
        NotEnclosed,
        BlockNotFound,
    }

    public enum CleanRoomLeakResponseKind
    {
        Openings,
        NoEnclosure,
        TooLarge,
        DetectionPending,
    }

    public enum CleanRoomClassResponseKind
    {
        Classified,
        Out,
    }

    /// <summary>
    ///     密閉できていない理由。組み立ては種別ごとの static factory、読み出しは TryGetView に閉じる
    ///     Why the structure is not sealed; built only through per-kind static factories and read only through TryGetView
    /// </summary>
    [MessagePackObject]
    public class CleanRoomLeakMessagePack
    {
        [Key(0)] public CleanRoomLeakResponseKind Kind { get; set; }

        // OpeningCells（表示上限まで）と TotalOpeningCount は Openings、MaxRoomVolume は TooLarge のときだけ書かれる。読むのは TryGetView だけ
        // OpeningCells (capped for display) and TotalOpeningCount are written only for Openings, MaxRoomVolume only for TooLarge; only TryGetView reads them
        [Key(1)] public List<Vector3IntMessagePack> OpeningCells { get; set; }
        [Key(2)] public int TotalOpeningCount { get; set; }
        [Key(3)] public int MaxRoomVolume { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CleanRoomLeakMessagePack() { }

        private CleanRoomLeakMessagePack(CleanRoomLeakResponseKind kind)
        {
            Kind = kind;
        }

        public static CleanRoomLeakMessagePack CreateOpenings(List<Vector3IntMessagePack> openingCells, int totalOpeningCount)
        {
            return new CleanRoomLeakMessagePack(CleanRoomLeakResponseKind.Openings) { OpeningCells = openingCells, TotalOpeningCount = totalOpeningCount };
        }

        public static CleanRoomLeakMessagePack CreateNoEnclosure()
        {
            return new CleanRoomLeakMessagePack(CleanRoomLeakResponseKind.NoEnclosure);
        }

        public static CleanRoomLeakMessagePack CreateTooLarge(int maxRoomVolume)
        {
            return new CleanRoomLeakMessagePack(CleanRoomLeakResponseKind.TooLarge) { MaxRoomVolume = maxRoomVolume };
        }

        public static CleanRoomLeakMessagePack CreateDetectionPending()
        {
            return new CleanRoomLeakMessagePack(CleanRoomLeakResponseKind.DetectionPending);
        }

        // 判別子の読み分けはここだけ。wire から読んだ値なので、Openings のセル欠損は警告して false
        // The only place that branches on the discriminator; values come from the wire, so missing Openings cells warn and return false
        public bool TryGetView(out CleanRoomLeakView view)
        {
            switch (Kind)
            {
                case CleanRoomLeakResponseKind.Openings:
                    if (OpeningCells == null || OpeningCells.Exists(cell => cell == null))
                    {
                        Debug.LogWarning($"[GetCleanRoomInfo] Openings leak has no cells (total {TotalOpeningCount}); treating the response as unreadable");
                        view = default;
                        return false;
                    }

                    view = new CleanRoomOpeningsLeakView(OpeningCells, TotalOpeningCount);
                    return true;
                case CleanRoomLeakResponseKind.NoEnclosure:
                    view = CleanRoomNoEnclosureLeakView.Instance;
                    return true;
                case CleanRoomLeakResponseKind.TooLarge:
                    view = new CleanRoomTooLargeLeakView(MaxRoomVolume);
                    return true;
                case CleanRoomLeakResponseKind.DetectionPending:
                    view = CleanRoomDetectionPendingLeakView.Instance;
                    return true;
                default:
                    Debug.LogWarning($"Unreadable response kind: {Kind}");
                        view = default;
                        return false;
            }
        }
    }
}
