using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>バックアップフォルダ名（日時＋任意の説明）を入力させるモーダル</summary>
/// <summary>Modal that asks for the backup folder name (timestamp plus optional description)</summary>
public class BackupNameDialog : EditorWindow
{
    private TextField descriptionField;
    private Label previewLabel;
    private long worldSizeBytes;
    private string dateString;
    private bool deleteAfterBackup;

    public string Result { get; private set; }
    public bool Confirmed { get; private set; }

    public static BackupNameDialog ShowDialog(long worldSizeBytes, bool deleteAfterBackup)
    {
        var window = CreateInstance<BackupNameDialog>();
        window.worldSizeBytes = worldSizeBytes;
        window.dateString = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        window.deleteAfterBackup = deleteAfterBackup;
        window.titleContent = new GUIContent(deleteAfterBackup ? "Backup & Delete" : "Backup");
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

        var actionText = deleteAfterBackup ? "Backup and delete" : "Backup";
        var sizeText = $"{worldSizeBytes / 1024f / 1024f:F1} MB";
        var message = new Label($"{actionText} world \"{WorldBackupService.WorldName}\" ({sizeText})?");
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

        var okButtonText = deleteAfterBackup ? "Backup & Delete" : "Backup";
        var okButton = new Button(() =>
        {
            Confirmed = true;
            var description = descriptionField.value;
            Result = string.IsNullOrEmpty(description) ? dateString : $"{dateString}_{description}";
            Close();
        }) { text = okButtonText };
        okButton.style.width = 130;
        okButton.style.height = 30;
        okButton.style.backgroundColor = deleteAfterBackup
            ? new Color(1.0f, 0.6f, 0.0f)
            : new Color(0.2f, 0.6f, 0.9f);

        buttonContainer.Add(cancelButton);
        buttonContainer.Add(okButton);
        rootVisualElement.Add(buttonContainer);
    }

    private void UpdatePreview()
    {
        var description = descriptionField.value;
        var folderName = string.IsNullOrEmpty(description)
            ? $"Backup_{dateString}"
            : $"Backup_{dateString}_{description}";
        previewLabel.text = $"Folder name: {folderName}/{WorldBackupService.WorldName}";
    }
}
