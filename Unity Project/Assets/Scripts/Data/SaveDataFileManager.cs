using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

[Serializable]
public class SaveFileInfo
{
    public string fileName;
    public string displayName;
    public string characterName;
    public string locationDisplayName;
    public string savedAtUtc;
}

[Serializable]
public class LastPlayedSaveData
{
    public string fileName;
}

public class SaveDataFileManager
{
    private const string LastPlayedFileName = "last_played.json";

    private readonly string saveDirectoryPath;

    public SaveDataFileManager(string saveDirectoryPath)
    {
        this.saveDirectoryPath = saveDirectoryPath;
        Directory.CreateDirectory(this.saveDirectoryPath);
    }

    private string GetFullPath(string fileName)
    {
        return Path.Combine(saveDirectoryPath, fileName);
    }

    private string LastPlayedFullPath => GetFullPath(LastPlayedFileName);

    public bool HasAnySaveFiles()
    {
        return GetAllSaveFiles().Count > 0;
    }

    public string CreateSaveFile(CharacterSaveData saveData, bool isAutoSave, string locationDisplayName)
    {
        if (saveData == null)
            return null;

        string prefix = isAutoSave ? "Auto Save" : "Manual Save";
        string safeLocation = string.IsNullOrWhiteSpace(locationDisplayName) ? "Unknown" : locationDisplayName;

        string displayName = $"{prefix} - {safeLocation}";
        string baseFileName = $"{displayName} - {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
        string fileName = MakeUniqueFileName(baseFileName);

        saveData.locationDisplayName = safeLocation;
        saveData.saveDisplayName = displayName;
        saveData.savedAtUtc = DateTime.UtcNow.ToString("o");
        saveData.saveFileName = fileName;

        WriteSaveFile(fileName, saveData);
        return fileName;
    }

    public void WriteSaveFile(string fileName, CharacterSaveData saveData)
    {
        try
        {
            Directory.CreateDirectory(saveDirectoryPath);
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(GetFullPath(fileName), json);
            Debug.Log($"Saved file: {fileName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving file '{fileName}': {ex}");
        }
    }

    public CharacterSaveData LoadSaveFile(string fileName)
    {
        string fullPath = GetFullPath(fileName);

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"Save file not found: {fullPath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            CharacterSaveData saveData = JsonUtility.FromJson<CharacterSaveData>(json);

            if (saveData != null && string.IsNullOrWhiteSpace(saveData.saveFileName))
                saveData.saveFileName = fileName;

            return saveData;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading file '{fileName}': {ex}");
            return null;
        }
    }

    public List<SaveFileInfo> GetAllSaveFiles()
    {
        List<SaveFileInfo> saves = new();

        if (!Directory.Exists(saveDirectoryPath))
            return saves;

        string[] files = Directory.GetFiles(saveDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);

        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileName(files[i]);

            if (string.Equals(fileName, LastPlayedFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                string json = File.ReadAllText(files[i]);
                CharacterSaveData saveData = JsonUtility.FromJson<CharacterSaveData>(json);

                if (saveData == null)
                    continue;

                saves.Add(new SaveFileInfo
                {
                    fileName = fileName,
                    displayName = string.IsNullOrWhiteSpace(saveData.saveDisplayName) ? Path.GetFileNameWithoutExtension(fileName) : saveData.saveDisplayName,
                    characterName = saveData.characterName,
                    locationDisplayName = saveData.locationDisplayName,
                    savedAtUtc = saveData.savedAtUtc
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error reading save file '{fileName}': {ex}");
            }
        }

        saves.Sort((a, b) => CompareSaveTimesDescending(a.savedAtUtc, b.savedAtUtc));
        return saves;
    }

    public void DeleteSaveFile(string fileName)
    {
        string fullPath = GetFullPath(fileName);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        string lastPlayed = GetLastPlayedSaveFile();

        if (!string.IsNullOrWhiteSpace(lastPlayed) && lastPlayed == fileName)
            ClearLastPlayedSaveFile();
    }

    public void SetLastPlayedSaveFile(string fileName)
    {
        try
        {
            LastPlayedSaveData data = new LastPlayedSaveData { fileName = fileName };
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(LastPlayedFullPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error writing last played save file: {ex}");
        }
    }

    public string GetLastPlayedSaveFile()
    {
        if (!File.Exists(LastPlayedFullPath))
            return null;

        try
        {
            string json = File.ReadAllText(LastPlayedFullPath);
            LastPlayedSaveData data = JsonUtility.FromJson<LastPlayedSaveData>(json);

            if (data == null || string.IsNullOrWhiteSpace(data.fileName))
                return null;

            return File.Exists(GetFullPath(data.fileName)) ? data.fileName : null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error reading last played save file: {ex}");
            return null;
        }
    }

    public void ClearLastPlayedSaveFile()
    {
        if (File.Exists(LastPlayedFullPath))
            File.Delete(LastPlayedFullPath);
    }

    public string GetMostRecentSaveFile()
    {
        List<SaveFileInfo> saves = GetAllSaveFiles();
        return saves.Count > 0 ? saves[0].fileName : null;
    }

    private string MakeUniqueFileName(string baseFileName)
    {
        string safeBase = SanitizeFileName(baseFileName);
        string fileName = safeBase + ".json";
        int counter = 1;

        while (File.Exists(GetFullPath(fileName)))
        {
            fileName = $"{safeBase} ({counter}).json";
            counter++;
        }

        return fileName;
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();

        for (int i = 0; i < invalid.Length; i++)
            fileName = fileName.Replace(invalid[i], '-');

        return fileName;
    }

    private static int CompareSaveTimesDescending(string a, string b)
    {
        bool aValid = DateTime.TryParse(a, null, DateTimeStyles.RoundtripKind, out DateTime aTime);
        bool bValid = DateTime.TryParse(b, null, DateTimeStyles.RoundtripKind, out DateTime bTime);

        if (aValid && bValid)
            return bTime.CompareTo(aTime);

        if (aValid)
            return -1;

        if (bValid)
            return 1;

        return 0;
    }
}