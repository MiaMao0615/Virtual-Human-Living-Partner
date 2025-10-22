using System;
using UnityEngine;

public class MicrophoneCapture : MonoBehaviour
{
    [Header("Audio")]
    public int sampleRate = 16000;      // Whisper �Ƽ� 16kHz
    public int recordLengthSec = 10;    // ѭ�����������ȣ��룩

    [Header("Behavior")]
    public bool drainOnStop = true;     // ֹͣǰ�Ƿ��β��δ����������һ��

    private AudioClip micClip;
    private string micDevice;
    private int lastReadPos = 0;
    private int channels = 1;

    public bool IsCapturing { get; private set; }

    void Awake()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone found.");
            enabled = false;
            return;
        }
        micDevice = Microphone.devices[0];
    }

    void OnDisable()
    {
        if (IsCapturing) StopCapture();
    }

    /// ��ʼ�ɼ������°�ťʱ���ã�
    public void StartCapture()
    {
        if (IsCapturing) return;

        micClip = Microphone.Start(micDevice, true, recordLengthSec, sampleRate);
        if (micClip == null)
        {
            Debug.LogError("Microphone.Start failed.");
            return;
        }
        channels = micClip.channels;
        lastReadPos = Microphone.GetPosition(micDevice); // �ӵ�ǰдָ�뿪ʼ�����������ʷ
        IsCapturing = true;
        Debug.Log($"[Mic] Start: {micDevice} @{sampleRate}Hz ch={channels}");
    }

    /// ֹͣ�ɼ����ɿ���ťʱ���ã�
    public void StopCapture()
    {
        if (!IsCapturing) return;

        if (drainOnStop)
        {
            // ���ɾ�β�����ڲ����� IsCapturing ��飩
            _ = GetNewSamples(ignoreCaptureCheck: true);
        }

        Microphone.End(micDevice);
        IsCapturing = false;
        micClip = null;
        lastReadPos = 0;

        Debug.Log("[Mic] Stopped.");
    }

    /// ��ȡ���ϴε��������ġ�����������float32 PCM��
    public float[] GetNewSamples(bool ignoreCaptureCheck = false)
    {
        if (micClip == null) return null;
        if (!IsCapturing && !ignoreCaptureCheck) return null;

        int currentPos = Microphone.GetPosition(micDevice);
        if (currentPos == lastReadPos) return null;

        int clipSamples = micClip.samples;     // ������������
        int sampleCount;                       // �����������������ȡ��
        if (currentPos > lastReadPos)
            sampleCount = currentPos - lastReadPos;
        else
            sampleCount = (clipSamples - lastReadPos) + currentPos; // ����

        int totalToRead = sampleCount * channels;
        float[] buffer = new float[totalToRead];

        int samplesToEnd = clipSamples - lastReadPos;
        if (sampleCount <= samplesToEnd)
        {
            micClip.GetData(buffer, lastReadPos);
        }
        else
        {
            var part1 = new float[samplesToEnd * channels];
            micClip.GetData(part1, lastReadPos);
            var part2 = new float[(sampleCount - samplesToEnd) * channels];
            micClip.GetData(part2, 0);
            Array.Copy(part1, 0, buffer, 0, part1.Length);
            Array.Copy(part2, 0, buffer, part1.Length, part2.Length);
        }

        lastReadPos = currentPos;
        return buffer;
    }

    public static byte[] FloatArrayToByteArray(float[] floatArray)
    {
        if (floatArray == null || floatArray.Length == 0) return null;
        byte[] byteArray = new byte[floatArray.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }
}
