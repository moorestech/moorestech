using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.SaveLoad.Json
{
    /// <summary>ロードしたセーブが「マイグレーションで補填した項目」を持ち越す。ロードで保持しセーブで書き戻す</summary>
    /// <summary>Carries the fields a migration backfilled in the loaded save; kept on load and written back on save</summary>
    public class SaveBackfilledFieldsRecord
    {
        // 読み捨てると次のautosaveで痕跡が消え、捏造値と実値を二度と区別できなくなる
        // Dropping it on load would let the next autosave erase the trace, so fabricated and real values could never be told apart again
        public IReadOnlyList<string> Fields { get; private set; } = Array.Empty<string>();

        public void SetFields(IReadOnlyList<string> fields)
        {
            Fields = fields.ToArray();
        }
    }
}
