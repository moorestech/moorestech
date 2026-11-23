using System;
using Client.Ui.State.Modal;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// Web の mock-host の fixture（modalSample・blueprintNameModal）と同じ要求と、項目名と値の行を載せた要求
    /// Requests matching the Web mock-host fixtures (modalSample, blueprintNameModal) plus one with label-and-values rows
    /// </summary>
    internal static class ModalRequestSamples
    {
        public static ModalRequest Confirm()
        {
            return new ModalRequest { Id = "m1", Title = "確認", Message = "これは確認ダイアログです", ButtonText = "OK", Variant = ModalVariant.Confirm, RequiresInput = false, Rows = Array.Empty<ModalTableRow>() };
        }

        public static ModalRequest BlueprintName()
        {
            return new ModalRequest { Id = "blueprint-name", Title = "ブループリント名", Message = "ブループリントの名前を入力してください", ButtonText = "保存", Variant = ModalVariant.Confirm, RequiresInput = true, Rows = Array.Empty<ModalTableRow>() };
        }

        public static ModalRequest Error()
        {
            return new ModalRequest { Id = "e1", Title = "エラー", Message = "保存できませんでした", ButtonText = "閉じる", Variant = ModalVariant.Error, RequiresInput = false, Rows = Array.Empty<ModalTableRow>() };
        }

        // ADR 0105 §4 の例の形（今の行は本文、改善ごとに項目名と差分）
        // The ADR 0105 §4 example shape (current values in the message, one label and deltas per improvement)
        public static ModalRequest Table()
        {
            var rows = new[] { new ModalTableRow("部屋クラスを B に", "Lv1 +9.2% / 失敗 −13.1%"), new ModalTableRow("品質モジュールLv1 を追加", "Lv1 +1.2% / 失敗 −2.7%") };
            return new ModalRequest { Id = "t1", Title = "失敗率の改善方法", Message = "今: Lv1 42.3% / 失敗 50.5%", ButtonText = "閉じる", Variant = ModalVariant.Confirm, RequiresInput = false, Rows = rows };
        }
    }
}
