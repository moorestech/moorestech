using System;
using System.IO;
using Game.Paths;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class SaveDataManager : EditorWindow
{
    private class BackupNameDialog : EditorWindow
    {
        private TextField descriptionField;
        private Label previewLabel;
        private int fileCount;
        private string dateString;

        public string Result { get; private set; }
        public bool Confirmed { get; private set; }

        public static BackupNameDialog ShowDialog(int fileCount)
        {
            var window = CreateInstance<BackupNameDialog>();
            window.fileCount = fileCount;
            window.dateString = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            window.titleContent = new GUIContent("Backup & Delete");
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

            var message = new Label($"Backup and delete {fileCount} save file(s)?");
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

            var cancelButton = new Button(() => {
                Confirmed = false;
                Close();
            }) { text = "Cancel" };
            cancelButton.style.width = 100;
            cancelButton.style.height = 30;
            cancelButton.style.marginRight = 5;

            var okButton = new Button(() => {
                Confirmed = true;
                var description = descriptionField.value;
                Result = string.IsNullOrEmpty(description) ? dateString : $"{dateString}_{description}";
                Close();
            }) { text = "Backup & Delete" };
            okButton.style.width = 130;
            okButton.style.height = 30;
            okButton.style.backgroundColor = new Color(1.0f, 0.6f, 0.0f);

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
            previewLabel.text = $"Folder name: {folderName}";
        }
    }


    [MenuItem("moorestech/SaveDataManager")]
    private static void ShowWindow()
    {
        var window = GetWindow<SaveDataManager>();
        window.titleContent = new GUIContent("Save Data Manager");
        window.minSize = new Vector2(350, 200);
        window.Show();
    }

    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingTop = 10;
        root.style.paddingBottom = 10;
        root.style.paddingLeft = 10;
        root.style.paddingRight = 10;

        CreateTitle(root);
        CreateButtons(root);
        CreateInfoLabel(root);
    }

    private void CreateTitle(VisualElement root)
    {
        var title = new Label("Save Data Management");
        title.style.fontSize = 16;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 15;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        root.Add(title);
    }

    private void CreateButtons(VisualElement root)
    {
        var buttonContainer = new VisualElement();
        buttonContainer.style.marginBottom = 10;

        var openFolderButton = CreateButton("Open Save Folder", OpenSaveFolder);
        var backupAndDeleteButton = CreateButton("Backup & Delete Save Data", BackupAndDeleteSaveData);
        backupAndDeleteButton.style.backgroundColor = new Color(1.0f, 0.6f, 0.0f);
        var deleteButton = CreateButton("Delete Save Data", DeleteSaveData);
        deleteButton.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);

        buttonContainer.Add(openFolderButton);
        buttonContainer.Add(backupAndDeleteButton);
        buttonContainer.Add(deleteButton);

        root.Add(buttonContainer);
    }

    private Button CreateButton(string text, Action callback)
    {
        var button = new Button(callback) { text = text };
        button.style.height = 30;
        button.style.marginBottom = 5;
        return button;
    }

    private void CreateInfoLabel(VisualElement root)
    {
        var saveDirectory = GameSystemPaths.SaveFileDirectory;
        var infoLabel = new Label($"Save Directory:\n{saveDirectory}");
        infoLabel.style.fontSize = 10;
        infoLabel.style.marginTop = 15;
        infoLabel.style.whiteSpace = WhiteSpace.Normal;
        infoLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        root.Add(infoLabel);
    }

    private void DeleteSaveData()
    {
        var saveDirectory = GameSystemPaths.SaveFileDirectory;

        if (!Directory.Exists(saveDirectory))
        {
            EditorUtility.DisplayDialog("Info", "Save directory does not exist.", "OK");
            return;
        }

        var files = Directory.GetFiles(saveDirectory);
        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog("Info", "No save data found.", "OK");
            return;
        }

        var result = EditorUtility.DisplayDialog(
            "Confirm Delete",
            $"Are you sure you want to delete all save data?\n\n{files.Length} file(s) will be deleted.",
            "Delete",
            "Cancel");

        if (!result) return;

        DeleteAllFiles(saveDirectory);
        EditorUtility.DisplayDialog("Success", $"Deleted {files.Length} file(s).", "OK");
    }

    private void BackupAndDeleteSaveData()
    {
        var saveDirectory = GameSystemPaths.SaveFileDirectory;

        if (!Directory.Exists(saveDirectory))
        {
            EditorUtility.DisplayDialog("Info", "Save directory does not exist.", "OK");
            return;
        }

        var files = Directory.GetFiles(saveDirectory);
        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog("Info", "No save data found.", "OK");
            return;
        }

        var dialog = BackupNameDialog.ShowDialog(files.Length);
        if (!dialog.Confirmed) return;

        var backupFolderName = $"Backup_{dialog.Result}";
        var backupPath = Path.Combine(saveDirectory, backupFolderName);

        Directory.CreateDirectory(backupPath);

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var destPath = Path.Combine(backupPath, fileName);
            File.Copy(file, destPath);
        }

        DeleteAllFiles(saveDirectory);

        EditorUtility.DisplayDialog(
            "Success",
            $"Backed up and deleted {files.Length} file(s).\n\nBackup location:\n{backupFolderName}",
            "OK");
    }

    private void OpenSaveFolder()
    {
        var saveDirectory = GameSystemPaths.SaveFileDirectory;

        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        EditorUtility.RevealInFinder(saveDirectory);
    }

    private void DeleteAllFiles(string directory)
    {
        var files = Directory.GetFiles(directory);
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }
}
