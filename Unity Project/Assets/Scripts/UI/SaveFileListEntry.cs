using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveFileEntryUIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI detailsText;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button deleteButton;

    public void Bind(SaveFileInfo saveFileInfo, MainMenuUIManager menu)
    {
        ApplyTexts(saveFileInfo);

        if (loadButton != null)
        {
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(() => menu.LoadSelectedSave(saveFileInfo.fileName));
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => menu.DeleteSelectedSave(saveFileInfo.fileName));
        }
    }

    public void BindForPauseMenu(SaveFileInfo saveFileInfo, PauseMenuUIManager menu)
    {
        ApplyTexts(saveFileInfo);

        if (loadButton != null)
        {
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(() => menu.LoadSelectedSave(saveFileInfo.fileName));
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => menu.DeleteSelectedSave(saveFileInfo.fileName));
        }
    }

    private void ApplyTexts(SaveFileInfo saveFileInfo)
    {
        if (titleText != null)
            titleText.text = saveFileInfo.displayName;

        if (detailsText != null)
            detailsText.text = $"{saveFileInfo.locationDisplayName} | {FormatTime(saveFileInfo.savedAtUtc)}";
    }

    private string FormatTime(string utcString)
    {
        if (DateTime.TryParse(utcString, null, DateTimeStyles.RoundtripKind, out DateTime utcTime))
            return utcTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        return "Unknown Time";
    }
}