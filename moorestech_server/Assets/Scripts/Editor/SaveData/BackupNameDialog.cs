using System;
using Game.Paths;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ダイアログの表示モード。削除の実行有無ではなく表示切り替えを表す
// Dialog display mode; represents the display switch, not whether delete actually runs
internal enum WorldBackupDialogMode
{
    BackupOnly,
    BackupAndDelete
}

/// <summary>バックアップフォルダ名（日時＋任意の説明）を入力させるモーダル</summary>
/// <summary>Modal that asks for the backup folder name (timestamp plus optional description)</summary>
public class BackupNameDialog : EditorWindow
{
    private TextField descriptionField;
    private Label previewLabel;
    private long worldSizeBytes;
    private string dateString;
    private WorldBackupDialogMode mode;

    internal string BackupFolderName { get; private set; }
    internal bool Confirmed { get; private set; }

    internal static BackupNameDialog ShowDialog(long worldSizeBytes, WorldBackupDialogMode mode)
    {
        var window = CreateInstance<BackupNameDialog>();
        window.worldSizeBytes = worldSizeBytes;
        window.dateString = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        window.mode = mode;
        window.titleContent = new GUIContent(mode == WorldBackupDialogMode.BackupAndDelete ? "Backup & Delete" : "Backup");
        window.minSize = new Vector2(450, 220);
        window.maxSize = new Vector2(450, 220);
        window.ShowModalUtility();
        return window;
    }

    private void CreateGUI()
    {
        rootVisualElement.style.paddingTop = 15;
        rootVisualElement.style.paddingBottom = 15;
        rootVisualElement.style.paddingLeft = 15;
        rootVisualElement.style.paddingRight = 15;

        var actionText = mode == WorldBackupDialogMode.BackupAndDelete ? "Backup and delete" : "Backup";
        var sizeText = $"{worldSizeBytes / 1024f / 1024f:F1} MB";
        var message = new Label($"{actionText} world \"{GameSystemPaths.DefaultWorldName}\" ({sizeText})?");
        message.style.marginBottom = 15;
        message.style.whiteSpace = WhiteSpace.Normal;
        rootVisualElement.Add(message);

        var dateLabel = new Label($"Date: {dateString}");
        dateLabel.style.marginBottom = 10;
        dateLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        rootVisualElement.Add(dateLabel);

        descriptionField = new TextField("Description (Optional):");
        descriptionField.style.marginBottom = 10;
        descriptionField.RegisterValueChangedCallback(evt => UpdatePreview());
        rootVisualElement.Add(descriptionField);

        previewLabel = new Label();
        previewLabel.style.marginBottom = 15;
        previewLabel.style.fontSize = 10;
        previewLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        rootVisualElement.Add(previewLabel);
        UpdatePreview();

        var buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = FlexDirection.Row;
        buttonContainer.style.justifyContent = Justify.FlexEnd;
        buttonContainer.style.marginTop = 10;

        var cancelButton = new Button(() =>
        {
            Confirmed = false;
            Close();
        }) { text = "Cancel" };
        cancelButton.style.width = 100;
        cancelButton.style.height = 30;
        cancelButton.style.marginRight = 5;

        var okButtonText = mode == WorldBackupDialogMode.BackupAndDelete ? "Backup & Delete" : "Backup";
        var okButton = new Button(() =>
        {
            var folderName = WorldBackupService.BuildBackupFolderName(dateString, descriptionField.value);
            if (folderName == null)
            {
                previewLabel.text = "Invalid description: avoid / \\ and \"..\"";
                return;
            }

            Confirmed = true;
            BackupFolderName = folderName;
            Close();
        }) { text = okButtonText };
        okButton.style.width = 130;
        okButton.style.height = 30;
        okButton.style.backgroundColor = mode == WorldBackupDialogMode.BackupAndDelete
            ? new Color(1.0f, 0.6f, 0.0f)
            : new Color(0.2f, 0.6f, 0.9f);

        buttonContainer.Add(cancelButton);
        buttonContainer.Add(okButton);
        rootVisualElement.Add(buttonContainer);
    }

    private void UpdatePreview()
    {
        var folderName = WorldBackupService.BuildBackupFolderName(dateString, descriptionField.value);
        previewLabel.text = folderName == null
            ? "Invalid description: avoid / \\ and \"..\""
            : $"Folder name: {folderName}/{GameSystemPaths.DefaultWorldName}";
    }
}
