using Game.SaveLoad.Json.WorldVersions;

namespace Game.SaveLoad.Writer
{
    // tickスレッドで取り込んだ保存像と書き出し先。JSON化と書き込みは書き出しスレッドが行う
    // A save image captured on the tick thread plus its destination; the writer thread serializes and writes it
    public sealed class SaveWriteJob
    {
        public long Generation { get; }
        public SaveWriteKind Kind { get; }
        public WorldSaveAllInfoV1 Data { get; }
        public string TargetPath { get; }
        public bool KeepBackup { get; }

        public SaveWriteJob(long generation, SaveWriteKind kind, WorldSaveAllInfoV1 data, string targetPath, bool keepBackup)
        {
            Generation = generation;
            Kind = kind;
            Data = data;
            TargetPath = targetPath;
            KeepBackup = keepBackup;
        }
    }
}
