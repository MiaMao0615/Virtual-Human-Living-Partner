using UnityEngine;
using TMPro;
using UnityEngine.UI;
using NativeWebSocket;
using System.Collections;
using System.Text;
using System.Linq;

public class MoonshotPCClient : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputField;   // 用户输入
    public TMP_Text answerText;         // 模型回答显示
    public Button sendButton;           // 发送按钮（可选：若不赋值，则只用 Inspector 绑定）

    [Header("Providers")]
    public TimeStateController timeController; // 提供 statusLabel（时间 + 当前ID）
    public TongTongManager tongTongManager;    // 提供当前状态名

    [Header("WebSocket")]
    public string wsUrl;
    private WebSocket ws;

    [Header("Safety")]
    [Tooltip("是否在 Start() 自动给 sendButton 绑定一次 SendActive（避免你忘记在 Inspector 手动绑定）。若你已经在 Inspector 里绑了，就把这个关掉。")]
    public bool autoBindOnStart = false;

    [Tooltip("按钮点击防抖（秒）。同一时间窗口内只允许发送一次。")]
    public float clickCooldown = 0.25f;

    // 接收显示缓冲
    private string lastAnswer = "";
    private string pendingAnswer = null;

    // 防重复 & 防抖
    private static bool _listenerBound = false;   // 跨实例兜底，确保只绑定一次
    private bool _connecting = false;
    private bool _connected = false;
    private float _lastSendTs = -999f;

    private void Start()
    {
        // —— 防重复绑定：只在选中 autoBindOnStart 时尝试绑定一次，并且先 Remove 再 Add，避免重复
        if (autoBindOnStart && sendButton != null)
        {
            sendButton.onClick.RemoveListener(SendActive);
            if (!_listenerBound)
            {
                sendButton.onClick.AddListener(SendActive);
                _listenerBound = true;
                Debug.Log("[Moonshot] Bound SendActive to sendButton (autoBindOnStart).");
            }
        }

        // —— 确保连接协程只跑一次
        if (!_connecting && !_connected)
        {
            StartCoroutine(ConnectWebSocketRoutine());
        }
    }

    private IEnumerator ConnectWebSocketRoutine()
    {
        if (_connecting || _connected) yield break;
        _connecting = true;

        ws = new WebSocket(wsUrl);

        ws.OnOpen += () =>
        {
            _connected = true;
            _connecting = false;
            Debug.Log("[Moonshot] WebSocket connected.");
        };
        ws.OnError += (e) =>
        {
            Debug.LogError("[Moonshot] WebSocket error: " + e);
        };
        ws.OnClose += (code) =>
        {
            _connected = false;
            Debug.Log("[Moonshot] WebSocket closed: " + code);
        };

        ws.OnMessage += (bytes) =>
        {
            string msg = Encoding.UTF8.GetString(bytes);
            pendingAnswer = msg; // 后台线程接收 → 主线程刷新
        };

        yield return ws.Connect();
    }

    /// <summary>
    /// ✅ 绑定到按钮 OnClick 的唯一入口
    /// Inspector: Button → OnClick → 拖入本脚本 → 选择 MoonshotPCClient.SendActive
    /// </summary>
    public void SendActive()
    {
        // —— 防抖：短时间内仅允许一次发送
        if (Time.unscaledTime - _lastSendTs < clickCooldown)
        {
            Debug.Log("[Moonshot] Click ignored due to cooldown.");
            return;
        }
        _lastSendTs = Time.unscaledTime;

        // —— 临时禁用按钮，等 cooldown 后再恢复，避免连点
        if (sendButton != null)
        {
            sendButton.interactable = false;
            StartCoroutine(ReenableButtonAfter(clickCooldown));
        }

        if (ws == null || ws.State != WebSocketState.Open)
        {
            Debug.LogWarning("[Moonshot] WebSocket not connected, skip send.");
            return;
        }

        // 1) 取 statusLabel 文本（例如 "09:30 - study_morning"）
        string statusText = (timeController != null && timeController.statusLabel != null)
            ? timeController.statusLabel.text
            : "";

        // 2) 取当前状态名（例如 "Eating" 或你的自定义状态）
        string stateName = (tongTongManager != null)
            ? tongTongManager.GetCurrentStateName()
            : "";

        // 3) 取用户输入
        string userText = (inputField != null) ? inputField.text.Trim() : "";

        // 4) 组合一条消息（用分隔符清晰区分，避免串在一起难解析）
        string composite = string.Join(" | ",
            new[] { statusText, stateName, userText }.Where(s => !string.IsNullOrEmpty(s)));

        if (string.IsNullOrEmpty(composite))
        {
            Debug.Log("[Moonshot] Nothing to send.");
            return;
        }

        SendMessageToServer(composite);

        // 清空输入框
        if (inputField != null) inputField.text = "";
    }

    private IEnumerator ReenableButtonAfter(float s)
    {
        yield return new WaitForSecondsRealtime(s);
        if (sendButton != null) sendButton.interactable = true;
    }

    private void SendMessageToServer(string message)
    {
        if (ws == null || ws.State != WebSocketState.Open) return;
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        ws.Send(bytes);
        Debug.Log("[Moonshot] >>> " + message);
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (ws != null) ws.DispatchMessageQueue();
#endif
        if (!string.IsNullOrEmpty(pendingAnswer))
        {
            lastAnswer = pendingAnswer;
            if (answerText != null) answerText.text = pendingAnswer;
            pendingAnswer = null;
        }
    }

    private void OnApplicationQuit()
    {
        if (ws != null) StartCoroutine(CloseWebSocketRoutine());
    }

    private IEnumerator CloseWebSocketRoutine()
    {
        yield return ws.Close();
    }
}
