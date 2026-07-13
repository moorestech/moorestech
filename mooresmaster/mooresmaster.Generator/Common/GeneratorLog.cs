using System;

namespace mooresmaster.Generator.Common;

/// <summary>
///     ジェネレータ内デバッグログの出力ゲート。既定では無効で、一切コンソール出力しない。
///     環境変数 MOORESMASTER_GENERATOR_LOG=1 を明示的に指定した場合のみ出力する。
///     IDE内コンパイル(VBCSCompiler)では標準出力パイプに読み手がおらず、64KBを超える出力はwriteブロックでコンパイルを永久停止させるため、直接のConsole.WriteLineは禁止。
///     Debug log gate for the generator. Disabled by default and writes nothing to the console.
///     Logs only when the MOORESMASTER_GENERATOR_LOG=1 environment variable is explicitly set.
///     Direct Console.WriteLine is forbidden: under IDE compilation (VBCSCompiler) stdout has no reader, so output beyond the 64KB pipe buffer blocks write() and hangs compilation forever.
/// </summary>
public static class GeneratorLog
{
    // デバッグ専用ゲートのため環境変数直読みを許容する / Deliberately read the env var here; this gate exists only for manual debugging
#pragma warning disable RS1035
    private static readonly bool IsEnabled = Environment.GetEnvironmentVariable("MOORESMASTER_GENERATOR_LOG") == "1";
#pragma warning restore RS1035

    public static void WriteLine(object? message)
    {
        if (!IsEnabled) return;

        Console.WriteLine(message);
    }
}
