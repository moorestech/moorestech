using System;
using Game.Paths;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>既定ワールドディレクトリをディレクトリごとバックアップ・削除するEditorツール</summary>
/// <summary>Editor tool that backs up and deletes the default world as a whole directory</summary>
public class SaveDataManager : EditorWindow
{
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
        var backupOnlyButton = CreateButton("Backup World", BackupWorld);
        backupOnlyButton.style.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        var backupAndDeleteButton = CreateButton("Backup & Delete World", BackupAndDeleteWorld);
        backupAndDeleteButton.style.backgroundColor = new Color(1.0f, 0.6f, 0.0f);
        var deleteButton = CreateButton("Delete World", DeleteWorld);
        deleteButton.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);

        buttonContainer.Add(openFolderButton);
        buttonContainer.Add(backupOnlyButton);
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
        var infoLabel = new Label($"World Directory:\n{WorldBackupService.WorldDirectory}");
        infoLabel.style.fontSize = 10;
        infoLabel.style.marginTop = 15;
        infoLabel.style.whiteSpace = WhiteSpace.Normal;
        infoLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        root.Add(infoLabel);
    }

    private void DeleteWorld()
    {
        if (!ConfirmWorldExists()) return;

        var result = EditorUtility.DisplayDialog(
            "Confirm Delete",
            $"Delete the whole world directory \"{WorldBackupService.WorldName}\"?\n\n{WorldBackupService.WorldDirectory}",
            "Delete",
            "Cancel");

        if (!result) return;

        WorldBackupService.DeleteWorld();
        EditorUtility.DisplayDialog("Success", $"Deleted world \"{WorldBackupService.WorldName}\".", "OK");
    }

    private void BackupAndDeleteWorld()
    {
        var backupPath = RunBackup(true);
        if (backupPath == null) return;

        WorldBackupService.DeleteWorld();

        EditorUtility.DisplayDialog(
            "Success",
            $"Backed up and deleted world \"{WorldBackupService.WorldName}\".\n\nBackup location:\n{backupPath}",
            "OK");
    }

    private void BackupWorld()
    {
        var backupPath = RunBackup(false);
        if (backupPath == null) return;

        EditorUtility.DisplayDialog(
            "Success",
            $"Backed up world \"{WorldBackupService.WorldName}\".\n\nBackup location:\n{backupPath}",
            "OK");
    }

    // バックアップの共通フロー。中止・失敗時はnullを返し、呼び出し側の後続処理を止める
    // Shared backup flow; returns null on cancel or failure so the caller stops
    private string RunBackup(bool deleteAfterBackup)
    {
        if (!ConfirmWorldExists()) return null;

        var dialog = BackupNameDialog.ShowDialog(WorldBackupService.GetWorldSizeBytes(), deleteAfterBackup);
        if (!dialog.Confirmed) return null;

        var backupFolderName = $"Backup_{dialog.Result}";
        if (WorldBackupService.BackupDestinationExists(backupFolderName))
        {
            EditorUtility.DisplayDialog(
                "Error",
                $"Backup destination already exists.\n\n{WorldBackupService.GetBackupWorldPath(backupFolderName)}",
                "OK");
            return null;
        }

        return WorldBackupService.BackupWorld(backupFolderName);
    }

    private bool ConfirmWorldExists()
    {
        if (WorldBackupService.WorldExists()) return true;

        EditorUtility.DisplayDialog(
            "Info",
            $"World \"{WorldBackupService.WorldName}\" does not exist.\n\n{WorldBackupService.WorldDirectory}",
            "OK");
        return false;
    }

    private void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(GameSystemPaths.SaveFileDirectory);
    }
}
