using System;
using UnityEngine;

public class MicrophoneCapture : MonoBehaviour
{
    [Header("Audio")]
    public int sampleRate = 16000;      // Whisper 推荐 16kHz
    public int recordLengthSec = 10;    // 循环缓冲区长度（秒）

    [Header("Behavior")]
    public bool drainOnStop = true;     // 停止前是否把尾部未读样本读完一次

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

    /// 开始采集（按下按钮时调用）
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
        lastReadPos = Microphone.GetPosition(micDevice); // 从当前写指针开始，避免读到历史
        IsCapturing = true;
        Debug.Log($"[Mic] Start: {micDevice} @{sampleRate}Hz ch={channels}");
    }

    /// 停止采集（松开按钮时调用）
    public void StopCapture()
    {
        if (!IsCapturing) return;

        if (drainOnStop)
        {
            // 读干净尾部（内部忽略 IsCapturing 检查）
            _ = GetNewSamples(ignoreCaptureCheck: true);
        }

        Microphone.End(micDevice);
        IsCapturing = false;
        micClip = null;
        lastReadPos = 0;

        Debug.Log("[Mic] Stopped.");
    }

    /// 获取自上次调用以来的“新样本”（float32 PCM）
    public float[] GetNewSamples(bool ignoreCaptureCheck = false)
    {
        if (micClip == null) return null;
        if (!IsCapturing && !ignoreCaptureCheck) return null;

        int currentPos = Microphone.GetPosition(micDevice);
        if (currentPos == lastReadPos) return null;

        int clipSamples = micClip.samples;     // 单声道样本数
        int sampleCount;                       // 单声道样本数（需读取）
        if (currentPos > lastReadPos)
            sampleCount = currentPos - lastReadPos;
        else
            sampleCount = (clipSamples - lastReadPos) + currentPos; // 环回

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
