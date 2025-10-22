using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PushToTalk : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public MicrophoneCapture mic;
    public float pollInterval = 0.05f;

    private bool isHolding = false;
    private Coroutine loop;

    public void OnPointerDown(PointerEventData _)
    {
        if (mic == null || isHolding) return;
        isHolding = true;
        mic.StartCapture();
        loop = StartCoroutine(PollLoop());
    }

    public void OnPointerUp(PointerEventData _)
    {
        if (mic == null || !isHolding) return;
        isHolding = false;

        if (loop != null) { StopCoroutine(loop); loop = null; }

        var tail = mic.GetNewSamples();
        if (tail != null && tail.Length > 0)
        {
            var bytes = MicrophoneCapture.FloatArrayToByteArray(tail);
            // TODO: Send the final bytes to 3000 port (WebSocket binary)
        }

        mic.StopCapture();
    }

    private IEnumerator PollLoop()
    {
        var wait = new WaitForSeconds(pollInterval);
        while (isHolding)
        {
            var buf = mic.GetNewSamples();
            if (buf != null && buf.Length > 0)
            {
                var bytes = MicrophoneCapture.FloatArrayToByteArray(buf);
                // TODO: Send bytes through WebSocket.Send(binary) to 3000 port
            }
            yield return wait;
        }
    }
}
