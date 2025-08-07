using System;
using Game.Paths;
using Spectre.Console.Cli;

namespace Server.Boot.Args
{
    public class StartServerSettings : CommandSettings
    {
        [CommandOption("-s|--saveFilePath <PATH>")] public string SaveFilePath { get; set; } = MoorestechServerDIContainerOptions.DefaultSaveJsonFilePath;

        [CommandOption("-c|--autoSave <BOOLEAN>")] public bool AutoSave { get; set; } = true;

        /// <summary>
        /// string[] args を Spectre.Console.Cli で解析し、
        /// 生成された StartServerSettings を返す。
        /// </summary>
        public static StartServerSettings Parse(string[] args)
        {
            // 解析結果をここに格納
            StartServerSettings? parsed = null;

            // ① 設定を横取りするインターセプタ
            var interceptor = new CaptureInterceptor<StartServerSettings>(s => parsed = s);

            // ② デフォルトコマンドとしてダミーを登録
            var app = new CommandApp<ParseOnlyCommand>();
            app.Configure(cfg => cfg.SetInterceptor(interceptor));   // ← Interceptor を差し込む :contentReference[oaicite:0]{index=0}

            // ③ 実行（＝パース）させる。ExitCode は不要なので無視
            app.Run(args);   // Interceptor の中で parsed に値が入る :contentReference[oaicite:1]{index=1}

            return parsed ?? throw new InvalidOperationException("Argument parsing failed.");
        }


        /// <summary>Execute しないダミーコマンド。</summary>
        private sealed class ParseOnlyCommand : Command<StartServerSettings>
        {
            public override int Execute(CommandContext _, StartServerSettings __) => 0;
        }

        /// <summary>ICommandInterceptor 実装。Settings をコールバックで渡すだけ。</summary>
        private sealed class CaptureInterceptor<T>
            : ICommandInterceptor where T : CommandSettings
        {
            private readonly Action<T> _callback;
            public CaptureInterceptor(Action<T> cb) => _callback = cb;

            public void Intercept(CommandContext _, CommandSettings settings) => _callback((T)settings);

            public void InterceptResult(CommandContext _, CommandSettings __, ref int ___) { }
        }
    }
}
