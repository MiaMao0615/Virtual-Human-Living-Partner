using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public TMP_Text transcriptText;        
    public TMP_InputField chatInputField;  

    public void SetText(string text)
    {
        if (transcriptText != null)
            transcriptText.text = text;
        else
            Debug.Log($"[UI] {text}");
    }

    public void AppendLine(string text)
    {
        if (transcriptText != null)
            transcriptText.text += "\n" + text;
        else
            Debug.Log($"[UI] {text}");
    }

    
    public void UpdateInputField(string text)
    {
        if (chatInputField != null)
            chatInputField.text = text;
        else
            Debug.Log($"[UI Input] {text}");
    }
}
