using System;
using System.Collections.Generic;
using System.Linq;
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

        // worldVersionが整数として読めないときに返す版。1未満なので拒否経路へそのまま落ちる
        // The version returned when worldVersion is not readable as an integer; being below 1 it falls straight into the rejection path
        private const int UnreadableWorldVersion = 0;

        private readonly List<ISaveMigrationStep> _steps;
        private readonly int _currentVersion;

        // 目標版を固定にすると、現在版が小さい間は不変条件（重複禁止・欠番禁止・昇順適用）をテストできない
        // Hard-coding the target version would leave the invariants untestable while the current version is small
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

        // worldVersionが無いセーブは版1。将来版と取り違えて拒否すると原本を触れなくなる
        // A save without worldVersion is version 1; mistaking it for a future one would lock the original away
        public static int ReadWorldVersion(JObject save)
        {
            var token = save[WorldVersionKey];
            if (token == null)
            {
                Debug.Log($"セーブに{WorldVersionKey}がないため版1として扱います。");
                return 1;
            }

            // 整数でないworldVersionをそのまま読むと生の型例外になり、理由がどこにも残らない
            // Reading a non-integer worldVersion raw would throw a bare cast exception with the reason logged nowhere
            if (token.Type != JTokenType.Integer)
            {
                Debug.LogError($"セーブの{WorldVersionKey}が整数として読めません。ロードせずに中断します。 value={token.ToString(Formatting.None)} type={token.Type}");
                return UnreadableWorldVersion;
            }

            return token.Value<int>();
        }

        public SaveMigrationResult Migrate(JObject save)
        {
            var fromVersion = ReadWorldVersion(save);

            if (fromVersion > _currentVersion)
                return SaveMigrationResult.Blocked(fromVersion,
                    $"セーブの版{fromVersion}はこのビルドが知る現在版{_currentVersion}より新しいため、ロードせずに中断します。ゲームを更新してください。");

            if (fromVersion < 1)
                return SaveMigrationResult.Blocked(fromVersion,
                    $"セーブの版{fromVersion}は不正です（1以上である必要があります）。ロードせずに中断します。");

            if (fromVersion == _currentVersion)
                return SaveMigrationResult.Completed(fromVersion, fromVersion, false, save);

            // 版に対応するステップだけを昇順に適用し、1手ごとにworldVersionを進める
            // Apply only the steps at or above the save's version in order, advancing worldVersion after each hop
            var migrated = save;
            foreach (var step in _steps.Where(step => step.FromVersion >= fromVersion))
            {
                migrated = step.Migrate(migrated);
                migrated[WorldVersionKey] = step.FromVersion + 1;
                Debug.Log($"セーブをV{step.FromVersion}からV{step.FromVersion + 1}へ変換しました。");
            }

            return SaveMigrationResult.Completed(fromVersion, _currentVersion, true, migrated);
        }
    }
}
