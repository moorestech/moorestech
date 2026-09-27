using System.Diagnostics;
using System.IO;
using Client.RemoteExec.Access;

namespace Client.Tests.RemoteExec
{
    // 実ユーザーデータは退避して復元し、テスト間の台帳混入を防ぐ
    // Preserve real user files and prevent ledger entries leaking between tests
    internal sealed class RemoteExecTestFiles
    {
        private readonly string _accessPath = Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json");
        private readonly string _ledgerPath = RemoteExecLedger.PathFor(Process.GetCurrentProcess().Id);
        private readonly byte[] _access;
        private readonly byte[] _ledger;
        private readonly bool _directoryExisted;

        internal RemoteExecTestFiles()
        {
            _directoryExisted = Directory.Exists(RemoteExecAccessFile.DirectoryPath);
            _access = File.Exists(_accessPath) ? File.ReadAllBytes(_accessPath) : null;
            _ledger = File.Exists(_ledgerPath) ? File.ReadAllBytes(_ledgerPath) : null;
            if (File.Exists(_accessPath)) File.Delete(_accessPath);
            if (File.Exists(_ledgerPath)) File.Delete(_ledgerPath);
        }

        internal int CountLedgerLines()
        {
            return File.Exists(_ledgerPath) ? File.ReadAllLines(_ledgerPath).Length : 0;
        }

        internal void Restore()
        {
            RestoreFile(_accessPath, _access);
            RestoreFile(_ledgerPath, _ledger);
            if (!_directoryExisted && Directory.Exists(RemoteExecAccessFile.DirectoryPath) &&
                Directory.GetFileSystemEntries(RemoteExecAccessFile.DirectoryPath).Length == 0)
                Directory.Delete(RemoteExecAccessFile.DirectoryPath);
        }

        private static void RestoreFile(string path, byte[] original)
        {
            if (original == null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else File.WriteAllBytes(path, original);
        }
    }
}
