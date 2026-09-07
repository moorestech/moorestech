using System;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    /// <summary>
    ///     セーブ要求の送信口。応答は要求番号のみで待ち合わせ先が無いため失敗のログだけ観測する
    ///     Entry point for save requests; the response carries only the generation, so only failures are logged
    /// </summary>
    public class GameSaveRequester
    {
        public void Save()
        {
            ClientContext.VanillaApi.Response.Save(default).Forget(LogSaveFailure);
        }

        private static void LogSaveFailure(Exception exception)
        {
            Debug.LogError($"セーブ要求に失敗しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
        }
    }
}
