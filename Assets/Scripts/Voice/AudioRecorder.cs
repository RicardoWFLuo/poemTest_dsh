using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioRecorder : MonoBehaviour
{
    [Tooltip("录音采样率。百度短语音识别要求 16000Hz。")]
    public int sampleRate = 16000;

    private AudioClip recordedClip;
    private string microphoneDevice;
    private bool isRecording;

    public bool IsRecording => isRecording;
    public string MicrophoneDevice => microphoneDevice;

    void Start()
    {
        // 权限申请（移动端首次录音会弹窗）
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }

        if (Microphone.devices != null && Microphone.devices.Length > 0)//获取麦克风设备
        {
            microphoneDevice = Microphone.devices[0];
        }
        else
        {
            Debug.LogError("未检测到麦克风设备！");
        }
    }

    public void StartRecording(int durationSeconds = 10)//开始录音
    {
        if (string.IsNullOrEmpty(microphoneDevice))
        {
            Debug.LogError("[AudioRecorder] 没有可用的麦克风设备，无法录音。");
            return;
        }

        if (isRecording)
            return;

        //设备名、是否循环、录音时长(秒)、采样率
        recordedClip = Microphone.Start(microphoneDevice, false, durationSeconds, sampleRate);
        isRecording = recordedClip != null;

        if (isRecording)
        {
            Debug.Log("开始录音...");
        }
        else
        {
            Debug.LogError("[AudioRecorder] Microphone.Start 失败。");
        }
    }

    public byte[] StopRecordingAndGetPCM()//停止录音并返回PCM字节数据
    {
        if (!isRecording)
            return null;

        isRecording = false;

        if (recordedClip == null)
            return null;

        // 只取真正录到的采样点，而不是整个缓冲区的长度。
        // 历史版本把整段（含未录制的静音尾巴）都发给了识别接口，浪费带宽且影响识别率。
        int lastPos = Microphone.GetPosition(microphoneDevice);
        Microphone.End(microphoneDevice);

        int channels = Mathf.Max(1, recordedClip.channels);
        int totalSamples = recordedClip.samples * channels;

        if (lastPos <= 0)
            lastPos = recordedClip.samples;

        int usedSamples = Mathf.Clamp(lastPos * channels, 0, totalSamples);

        float[] samples = new float[usedSamples];
        recordedClip.GetData(samples, 0);

        byte[] pcmData = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
            pcmData[i * 2] = (byte)(value & 0xFF);
            pcmData[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        recordedClip = null;

        if (pcmData.Length == 0)
        {
            Debug.LogWarning("[AudioRecorder] 没有录到任何音频数据。");
            return null;
        }

        Debug.Log($"录音结束，共生成 {pcmData.Length} 字节PCM数据。");
        return pcmData;
    }

    private void OnDisable()
    {
        if (isRecording)
        {
            Microphone.End(microphoneDevice);
            isRecording = false;
        }
    }
}
