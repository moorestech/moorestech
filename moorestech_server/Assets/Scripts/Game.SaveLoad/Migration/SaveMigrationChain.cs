using System;
using System.Collections.Generic;
using System.Linq;
using Game.SaveLoad.Json.WorldVersions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration
{
    /// <summary>セーブJSONを目標版まで順に変換する。欠番・重複は構築時に落として恒久封鎖を作らない</summary>
    /// <summary>Converts a save JSON up to the target version; duplicates and gaps fail at construction so no version is permanently unloadable</summary>
    public sealed class SaveMigrationChain
    {
        private const string WorldVersionKey = "worldVersion";

        // 読み取り不能時にBlocked結果へ載せる版の代表値。分岐制御には使わずログ用途に限る
        // The placeholder version carried on an unreadable-version Blocked result; used only for logging, never for branching
        private const int UnreadableWorldVersion = 0;

        private readonly List<ISaveMigrationStep> _steps;
        private readonly int _currentVersion;

        // 本番の入口。目標版はセーブ形式の現在版だけで、呼び出し側に綴らせない
        // The production entry point; the target is always the save format's current version, never spelled by the caller
        public static SaveMigrationChain ForCurrentVersion(IReadOnlyList<ISaveMigrationStep> steps)
        {
            return new SaveMigrationChain(steps, WorldSaveAllInfoV1.CurrentVersion);
        }

        // テストが任意の目標版を渡す注入口。固定にすると現在版が小さい間は不変条件（重複禁止・欠番禁止・昇順適用）をテストできない
        // The injection point letting tests pass any target; hard-coding it would leave the invariants untestable while the current version is small
        public SaveMigrationChain(IReadOnlyList<ISaveMigrationStep> steps, int currentVersion)
        {
            _steps = steps.OrderBy(step => step.FromVersion).ToList();
            _currentVersion = currentVersion;

            // 1..currentVersion-1 を欠番なく1本ずつ覆っていることを起動時に確かめる
            // Verify at boot that 1..currentVersion-1 is covered exactly once with no gaps
            var expected = Enumerable.Range(1, currentVersion - 1).ToArray();
            var actual = _steps.Select(step => step.FromVersion).ToArray();
            if (!expected.SequenceEqual(actual))
            {
                var expectedText = string.Join(",", expected);
                var actualText = string.Join(",", actual);
                throw new ArgumentException(
                    $"マイグレーションステップのFromVersionが不正です。期待={{{expectedText}}} 実際={{{actualText}}}（目標版={currentVersion}）");
            }
        }

        public SaveMigrationResult Migrate(JObject save)
        {
            if (!TryReadWorldVersion(out var fromVersion, out var unreadableReason))
                return SaveMigrationResult.Blocked(UnreadableWorldVersion, unreadableReason);

            if (_currentVersion < fromVersion)
                return SaveMigrationResult.Blocked(fromVersion,
                    $"セーブの版{fromVersion}はこのビルドが知る現在版{_currentVersion}より新しいため、ロードせずに中断します。ゲームを更新してください。");

            if (fromVersion < 1)
                return SaveMigrationResult.Blocked(fromVersion,
                    $"セーブの版{fromVersion}は不正です（1以上である必要があります）。ロードせずに中断します。");

            if (fromVersion == _currentVersion)
                return SaveMigrationResult.Completed(fromVersion, false, save);

            // 版に対応するステップだけを昇順に適用し、1手ごとにworldVersionを進める
            // Apply only the steps at or above the save's version in order, advancing worldVersion after each hop
            var migrated = save;
            foreach (var step in _steps.Where(step => fromVersion <= step.FromVersion))
            {
                // 変換できなかった手が出たら版を刻まずに中断する。刻むと未変換のセーブが新版の顔でLoadへ渡る
                // A hop that could not convert stops the chain without stamping the version; stamping would pass an unconverted save to Load
                var stepResult = step.Migrate(migrated);
                if (!stepResult.IsConverted)
                {
                    var reason = $"セーブをV{step.FromVersion}からV{step.FromVersion + 1}へ変換できませんでした: {stepResult.FailureReason}";
                    Debug.LogError(reason);
                    return SaveMigrationResult.Blocked(fromVersion, reason);
                }

                migrated = stepResult.Save;
                migrated[WorldVersionKey] = step.FromVersion + 1;
                Debug.Log($"セーブをV{step.FromVersion}からV{step.FromVersion + 1}へ変換しました。");
            }

            return SaveMigrationResult.Completed(fromVersion, true, migrated);

            #region Internal

            // worldVersionが無いセーブは版1。将来版と取り違えて拒否すると原本を触れなくなる
            // A save without worldVersion is version 1; mistaking it for a future one would lock the original away
            // 整数として読めないworldVersionは「版0」へ潰さず専用理由を返す。潰すとプレイヤーに存在しない版番号を見せてしまう
            // A worldVersion unreadable as an integer is not collapsed into "version 0"; a dedicated reason avoids naming a version the save never had
            bool TryReadWorldVersion(out int version, out string reason)
            {
                reason = null;

                var token = save[WorldVersionKey];
                if (token == null)
                {
                    Debug.Log($"セーブに{WorldVersionKey}がないため版1として扱います。");
                    version = 1;
                    return true;
                }

                if (TryReadVersionInt32(token, out version)) return true;

                reason = $"セーブの{WorldVersionKey}が整数として読めません。ロードせずに中断します。 value={token.ToString(Formatting.None)} type={token.Type}";
                Debug.LogError(reason);
                return false;
            }

            // 巨大整数はJson.NETがlongやBigIntegerで持つ。int範囲に収まるものだけを版として受け取る
            // Json.NET holds a huge integer as long or BigInteger; only values fitting in int are accepted as a version
            bool TryReadVersionInt32(JToken versionToken, out int parsedVersion)
            {
                parsedVersion = UnreadableWorldVersion;
                if (versionToken.Type != JTokenType.Integer) return false;

                var raw = (versionToken as JValue)?.Value;
                if (raw is long longVersion)
                {
                    if (longVersion < int.MinValue || int.MaxValue < longVersion) return false;
                    parsedVersion = (int)longVersion;
                    return true;
                }

                if (raw is ulong ulongVersion)
                {
                    if (int.MaxValue < ulongVersion) return false;
                    parsedVersion = (int)ulongVersion;
                    return true;
                }

                // longにも収まらない綴りはBigInteger等で届く。版として意味のある値にはなりえない
                // A spelling too large even for long arrives as BigInteger or similar and can never be a meaningful version
                return false;
            }

            #endregion
        }
    }
}
