using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeUIManager : MonoBehaviour
{
    public TMP_Text uiStatusText;
    public void UpdateUI(float timeHour, string state)
    {
        int hour = Mathf.FloorToInt(timeHour);
        int minute = Mathf.FloorToInt((timeHour - hour) * 60);
        uiStatusText.text = $"{hour:00}:{minute:00} - {state}";
    }
}
