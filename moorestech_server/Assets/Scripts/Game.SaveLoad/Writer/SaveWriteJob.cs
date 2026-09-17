using Game.SaveLoad.Json.WorldVersions;

namespace Game.SaveLoad.Writer
{
    // tickスレッドで取り込んだ保存像と書き出し先。JSON化と書き込みは書き出しスレッドが行う
    // A save image captured on the tick thread plus its destination; the writer thread serializes and writes it
    public sealed class SaveWriteJob
    {
        public long Generation { get; }
        public SaveWriteKind Kind { get; }
        public WorldSaveAllInfo Data { get; }
        public string TargetPath { get; }
        public bool KeepBackup { get; }

        private SaveWriteJob(long generation, SaveWriteKind kind, WorldSaveAllInfo data, string targetPath, bool keepBackup)
        {
            Generation = generation;
            Kind = kind;
            Data = data;
            TargetPath = targetPath;
            KeepBackup = keepBackup;
        }

        // プレイヤーのセーブは要求番号で完了と突き合わせ、置換前の版を .bak に残す
        // A player save matches its completion by generation and keeps the pre-swap file as .bak
        public static SaveWriteJob ForPlayerSave(long generation, WorldSaveAllInfo data, string targetPath)
        {
            return new SaveWriteJob(generation, SaveWriteKind.PlayerSave, data, targetPath, true);
        }

        // スナップショットは世代ごとに別ファイルなので要求番号も控えも持たない
        // A snapshot writes its own file per generation, so it carries neither a generation nor a backup
        public static SaveWriteJob ForSnapshot(WorldSaveAllInfo data, string targetPath)
        {
            return new SaveWriteJob(0, SaveWriteKind.Snapshot, data, targetPath, false);
        }
    }
}
