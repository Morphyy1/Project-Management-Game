using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Информационные панели")]
    public TextMeshProUGUI remainingTimeText;
    public TextMeshProUGUI currentTimeText;
    public TextMeshProUGUI weatherText;
    public TextMeshProUGUI alphaText;
    public TextMeshProUGUI statusText;
    
    [Header("Список источников")]
    public Transform sourceListParent;
    public GameObject sourceListItemPrefab;
    
    [Header("Предупреждения")]
    public GameObject warningPanel;
    public TextMeshProUGUI warningText;
    
    [Header("Кнопки")]
    public Button generateButton;
    public Button validateButton;
    public Button resetButton;
    
    public void UpdateTimeDisplay(float remaining, float current)
    {
        if (remainingTimeText != null)
            remainingTimeText.text = $"Осталось: {remaining:F1} мин";
        
        if (currentTimeText != null)
            currentTimeText.text = $"Прошло: {current:F1} мин";
    }
    
    public void UpdateWeatherDisplay(float coeff)
    {
        if (weatherText != null)
        {
            string weather = "Ясно";
            if (coeff < 0.7f) weather = "Гроза";
            else if (coeff < 0.85f) weather = "Дождь";
            else if (coeff < 0.95f) weather = "Облачно";
            
            weatherText.text = $"Погода: {weather} (×{coeff:F2})";
        }
    }
    
    public void UpdateAlphaDisplay(float alpha)
    {
        if (alphaText != null)
            alphaText.text = $"Коэф. достижимости: α = {alpha:F2}";
    }
    
    public void UpdateStatus(string status)
    {
        if (statusText != null)
            statusText.text = status;
    }
    
    public void ShowWarning(string message)
    {
        if (warningPanel != null)
        {
            warningPanel.SetActive(true);
            if (warningText != null)
                warningText.text = message;
            
            Invoke(nameof(HideWarning), 3f);
        }
    }
    
    void HideWarning()
    {
        if (warningPanel != null)
            warningPanel.SetActive(false);
    }
    
    public void UpdateSourceList(List<SourceInfo> sources)
    {
        if (sourceListParent == null || sourceListItemPrefab == null) return;
        
        // Очистить список
        foreach (Transform child in sourceListParent)
        {
            Destroy(child.gameObject);
        }
        
        // Добавить источники
        foreach (SourceInfo source in sources)
        {
            GameObject item = Instantiate(sourceListItemPrefab, sourceListParent);
            TextMeshProUGUI text = item.GetComponentInChildren<TextMeshProUGUI>(true);
            
            if (text == null)
            {
                Debug.LogWarning("SourceListItemPrefab не содержит TextMeshProUGUI");
                continue;
            }
            
            string status = source.visited ? "✓" : (source.reachable ? "○" : "✗");
            text.text = $"{status} {source.node.name}: u={source.usefulness:F2} d={source.reliability:F2}";
        }
    }
}