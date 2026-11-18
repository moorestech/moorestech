using Client.Ui.State.Modal;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// Web の mock-host の fixture（modalSample・blueprintNameModal）と同じ要求
    /// Requests matching the Web mock-host fixtures (modalSample, blueprintNameModal)
    /// </summary>
    internal static class ModalRequestSamples
    {
        public static ModalRequest Confirm()
        {
            return new ModalRequest { Id = "m1", Title = "確認", Message = "これは確認ダイアログです", ButtonText = "OK", Variant = ModalVariant.Confirm, RequiresInput = false };
        }

        public static ModalRequest BlueprintName()
        {
            return new ModalRequest { Id = "blueprint-name", Title = "ブループリント名", Message = "ブループリントの名前を入力してください", ButtonText = "保存", Variant = ModalVariant.Confirm, RequiresInput = true };
        }

        public static ModalRequest Error()
        {
            return new ModalRequest { Id = "e1", Title = "エラー", Message = "保存できませんでした", ButtonText = "閉じる", Variant = ModalVariant.Error, RequiresInput = false };
        }
    }
}
