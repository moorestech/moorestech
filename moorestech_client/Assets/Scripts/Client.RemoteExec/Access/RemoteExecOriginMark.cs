using System;

namespace Client.RemoteExec.Access
{
    // セッションの遠隔実行の印。この印を持つこと自体が有効を意味し、台帳名は必須
    // A session's remote-execution mark; holding one means enabled, so the ledger name is mandatory
    public sealed class RemoteExecOriginMark
    {
        public string LedgerFileName { get; }
        public bool Attempted { get; }
        public bool LedgerWriteFailed { get; }

        public RemoteExecOriginMark(string ledgerFileName) : this(ledgerFileName, false, false)
        {
        }

        public RemoteExecOriginMark(string ledgerFileName, bool attempted, bool ledgerWriteFailed)
        {
            // 台帳名の無い印は「有効なのに台帳の在処が不明」という読めない状態になる
            // A mark without a ledger name would mean "enabled but nobody knows where the ledger is"
            if (ledgerFileName == null) throw new ArgumentNullException(nameof(ledgerFileName), "遠隔実行の印は台帳ファイル名を必ず持つ");
            LedgerFileName = ledgerFileName;
            Attempted = attempted;
            LedgerWriteFailed = ledgerWriteFailed;
        }
    }
}
