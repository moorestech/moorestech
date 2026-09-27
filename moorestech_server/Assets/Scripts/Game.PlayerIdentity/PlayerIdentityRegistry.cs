using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.PlayerIdentity
{
    // ワールド単位の身元→プレイヤーIDの対応表。連番で払い出し欠番は再利用しない
    // Per-world identity-to-player-id table; ids are sequential and never reused
    public class PlayerIdentityRegistry : IPlayerIdentityRegistry
    {
        private const int FirstPlayerId = 1;

        private readonly Dictionary<string, int> _idByIdentity = new();
        private readonly SortedSet<int> _unclaimedPlayerIds = new();
        private int _nextPlayerId = FirstPlayerId;
        private int? _claimCandidatePlayerId;

        public void InitializeForNewWorld()
        {
            _idByIdentity.Clear();
            _unclaimedPlayerIds.Clear();
            _nextPlayerId = FirstPlayerId;
            _claimCandidatePlayerId = null;
        }

        public bool TryGetPlayerId(string identity, out int playerId)
        {
            return _idByIdentity.TryGetValue(identity, out playerId);
        }

        // 接続確定前に候補を読む。対応表も次のIDも変更しない
        // Read the candidate before binding without changing the table or next id
        public PlayerIdAssignment PreviewAssignment(string identity)
        {
            if (_idByIdentity.TryGetValue(identity, out var knownId)) return new PlayerIdAssignment(knownId, PlayerIdAssignmentKind.Known);
            if (_claimCandidatePlayerId.HasValue) return new PlayerIdAssignment(_claimCandidatePlayerId.Value, PlayerIdAssignmentKind.ClaimedCandidate);

            // 次のIDを保存できない場合は負数へ周回させない
            // Never wrap into negative ids when the next id can no longer be saved
            if (_nextPlayerId == int.MaxValue)
            {
                const string reason = "[PlayerIdentity] プレイヤーIDの採番上限に達したため採番できません";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }
            return new PlayerIdAssignment(_nextPlayerId, PlayerIdAssignmentKind.NewlyAssigned);
        }

        public PlayerIdAssignment Assign(string identity)
        {
            var assignment = PreviewAssignment(identity);
            if (assignment.Kind == PlayerIdAssignmentKind.Known) return assignment;

            // 旧セーブ・再現用の候補は最初の未知の身元にだけ渡す
            // The legacy/repro candidate goes only to the first unknown identity
            if (assignment.Kind == PlayerIdAssignmentKind.ClaimedCandidate)
            {
                _claimCandidatePlayerId = null;
                _unclaimedPlayerIds.Remove(assignment.PlayerId);
                Debug.Log($"[PlayerIdentity] 持ち主未定のプレイヤー{assignment.PlayerId}を身元{identity}へ結びつけました");
            }
            else
            {
                _nextPlayerId++;
                Debug.Log($"[PlayerIdentity] 身元{identity}へ新しいプレイヤーID{assignment.PlayerId}を払い出しました");
            }
            _idByIdentity[identity] = assignment.PlayerId;
            return assignment;
        }

        public PlayersSaveJsonObject GetSaveJsonObject()
        {
            // 比較器が添字で突き合わせるためID昇順で正準化する
            // Canonicalize by ascending id because the comparer matches by index
            var entries = _idByIdentity.Select(pair => new PlayerIdentityEntryJsonObject(pair.Value, pair.Key))
                .Concat(_unclaimedPlayerIds.Select(id => new PlayerIdentityEntryJsonObject(id, null)))
                .OrderBy(entry => entry.PlayerId)
                .ToList();
            return new PlayersSaveJsonObject(_nextPlayerId, _claimCandidatePlayerId, entries);
        }

        public void Load(PlayersSaveJsonObject save)
        {
            // 外部セーブの破損は復元前に検出し、既存の対応表を保持する
            // Detect corrupt external save data before restoring, preserving the current table
            ValidateSave(save);
            InitializeForNewWorld();
            foreach (var entry in save.Entries)
            {
                if (entry.Identity == null) _unclaimedPlayerIds.Add(entry.PlayerId);
                else _idByIdentity[entry.Identity] = entry.PlayerId;
            }
            _nextPlayerId = save.NextPlayerId;
            _claimCandidatePlayerId = save.ClaimCandidatePlayerId;

            // 候補が持ち主未定に居ないのは壊れたセーブ。結びつけずに理由を出す
            // A candidate missing from the unclaimed set means a broken save; drop it and log why
            if (_claimCandidatePlayerId.HasValue && !_unclaimedPlayerIds.Contains(_claimCandidatePlayerId.Value))
            {
                Debug.LogError($"[PlayerIdentity] 結びつけ候補{_claimCandidatePlayerId.Value}が持ち主未定の一覧に無いため候補を破棄します");
                _claimCandidatePlayerId = null;
            }
        }

        private static void ValidateSave(PlayersSaveJsonObject save)
        {
            if (save == null || save.Entries == null) throw InvalidSave("players または entries が欠損しています");
            if (save.NextPlayerId < FirstPlayerId) throw InvalidSave("nextPlayerId は1以上である必要があります");

            // 同一IDや同一身元の重複は別人の状態を上書きするため拒否する
            // Reject duplicate ids or identities because they would overwrite another player's state
            var playerIds = new HashSet<int>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in save.Entries)
            {
                if (entry == null) throw InvalidSave("entries に null が含まれています");
                if (entry.PlayerId < FirstPlayerId || !playerIds.Add(entry.PlayerId))
                {
                    throw InvalidSave($"プレイヤーIDが不正または重複しています: {entry.PlayerId}");
                }
                if (entry.PlayerId >= save.NextPlayerId) throw InvalidSave($"nextPlayerId が既存ID以下です: {entry.PlayerId}");

                // nullだけが持ち主未定で、既知の身元は書式と一意性を検査する
                // Only null denotes an unclaimed player; validate bound identity syntax and uniqueness
                if (entry.Identity == null) continue;
                if (!PlayerIdentityText.IsValid(entry.Identity, out var reason)) throw InvalidSave(reason);
                if (!identities.Add(entry.Identity)) throw InvalidSave($"身元が重複しています: {entry.Identity}");
            }

            #region Internal

            InvalidOperationException InvalidSave(string reason)
            {
                var message = $"[PlayerIdentity] players 節を復元できません: {reason}";
                Debug.LogError(message);
                return new InvalidOperationException(message);
            }

            #endregion
        }
    }
}
