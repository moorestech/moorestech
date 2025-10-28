using System;
using System.IO;
using Game.Paths;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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

        var deleteButton = CreateButton("Delete Save Data", DeleteSaveData);
        var backupAndDeleteButton = CreateButton("Backup & Delete Save Data", BackupAndDeleteSaveData);
        var openFolderButton = CreateButton("Open Save Folder", OpenSaveFolder);

        buttonContainer.Add(deleteButton);
        buttonContainer.Add(backupAndDeleteButton);
        buttonContainer.Add(openFolderButton);

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

        var backupFolderName = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
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
