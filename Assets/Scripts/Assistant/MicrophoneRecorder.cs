using System.Collections;
using UnityEngine;

/// <summary>一次录音的结果。</summary>
public class RecordingResult
{
    /// <summary>16 位单声道 PCM（已重采样到目标采样率）。</summary>
    public short[] pcm;

    public int sampleRate;

    public bool IsUsable => pcm != null && pcm.Length > 0;

    public float DurationSeconds => sampleRate > 0 ? (float)pcm.Length / sampleRate : 0f;
}

/// <summary>
/// 用 Unity 的 Microphone 录音，并整理成百度短语音识别要的格式：
/// 单声道 / 16 位 / 16kHz 的裸 PCM。
///
/// 注意几点：
/// · 设备不一定支持 16000Hz，所以要读 GetDeviceCaps 并做重采样；
/// · 录音开头结尾的静音要裁掉，既省流量也能提高识别率；
/// · MobileInputManager 的按键不会帮你处理权限，这里用
///   Application.RequestUserAuthorization 主动要一次麦克风授权。
/// </summary>
public class MicrophoneRecorder : MonoBehaviour
{
    [Header("录音参数")]
    [Tooltip("目标采样率。百度短语音识别推荐 16000。")]
    public int targetSampleRate = 16000;

    [Tooltip("最长录音秒数。百度短语音识别上限 60 秒，提问场景 30 秒足够。")]
    public int maxSeconds = 30;

    [Tooltip("低于这个音量视为静音，用于裁掉首尾空白。")]
    public float silenceThreshold = 0.01f;

    [Tooltip("裁掉静音后至少保留多少毫秒，太短的录音直接判为无效。")]
    public int minValidMilliseconds = 300;

    private AudioClip clip;
    private string deviceName;
    private bool recording;

    public bool IsRecording => recording;

    public bool HasDevice => Microphone.devices != null && Microphone.devices.Length > 0;

    public bool HasPermission => Application.HasUserAuthorization(UserAuthorization.Microphone);

    // ---------------------------------------------------------------- 对外接口

    /// <summary>请求麦克风权限（移动端首次会弹系统授权框）。</summary>
    public IEnumerator RequestPermission()
    {
        if (HasPermission)
            yield break;

        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);

        if (!HasPermission)
            Debug.LogWarning("[录音] 用户拒绝了麦克风权限。");
    }

    /// <summary>开始录音。返回 false 时 error 里是可展示给玩家的原因。</summary>
    public bool StartRecording(out string error)
    {
        error = null;

        if (recording)
            return true;

        if (!HasDevice)
        {
            error = "没有检测到麦克风。";
            return false;
        }

        deviceName = null; // 用系统默认设备

        int minFreq = 0;
        int maxFreq = 0;
        Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);

        int frequency = targetSampleRate;

        // 有的设备只支持 44100/48000，这时先按设备能力录，再重采样到 16000
        if (maxFreq > 0 && frequency > maxFreq)
            frequency = maxFreq;

        if (minFreq > 0 && frequency < minFreq)
            frequency = minFreq;

        clip = Microphone.Start(deviceName, false, maxSeconds, frequency);

        if (clip == null)
        {
            error = "麦克风启动失败。";
            return false;
        }

        recording = true;
        return true;
    }

    /// <summary>停止录音并返回处理好的 PCM。返回 null 表示没有有效声音。</summary>
    public RecordingResult StopRecording()
    {
        if (!recording)
            return null;

        recording = false;

        int position = Microphone.GetPosition(deviceName);
        Microphone.End(deviceName);

        if (clip == null || position <= 0)
        {
            clip = null;
            return null;
        }

        int channels = clip.channels;
        int totalSamples = clip.samples;

        float[] buffer = new float[totalSamples * channels];
        clip.GetData(buffer, 0);

        int sourceRate = clip.frequency;
        clip = null;

        // 只取已录到的部分，并混成单声道
        int monoLength = Mathf.Clamp(position, 0, totalSamples);
        float[] mono = new float[monoLength];

        for (int i = 0; i < monoLength; i++)
        {
            float sum = 0f;

            for (int c = 0; c < channels; c++)
                sum += buffer[i * channels + c];

            mono[i] = sum / channels;
        }

        mono = TrimSilence(mono, out bool hadVoice);

        if (!hadVoice || mono.Length < sourceRate * minValidMilliseconds / 1000f)
        {
            Debug.LogWarning("[录音] 没有检测到有效声音。");
            return null;
        }

        short[] pcm = ResampleToPcm16(mono, sourceRate, targetSampleRate);

        return new RecordingResult { pcm = pcm, sampleRate = targetSampleRate };
    }

    /// <summary>放弃当前录音（不做识别）。</summary>
    public void Cancel()
    {
        if (!recording)
            return;

        recording = false;
        Microphone.End(deviceName);
        clip = null;
    }

    // ---------------------------------------------------------------- 处理

    /// <summary>裁掉首尾静音。</summary>
    private float[] TrimSilence(float[] samples, out bool hadVoice)
    {
        hadVoice = false;

        if (samples == null || samples.Length == 0)
            return samples;

        int start = 0;
        int end = samples.Length - 1;

        while (start < samples.Length && Mathf.Abs(samples[start]) < silenceThreshold)
            start++;

        while (end > start && Mathf.Abs(samples[end]) < silenceThreshold)
            end--;

        if (start >= end)
        {
            hadVoice = false;
            return new float[0];
        }

        hadVoice = true;

        // 前后各留一点余量，避免把字头切掉
        int padding = Mathf.RoundToInt(samples.Length * 0.02f);
        start = Mathf.Max(0, start - padding);
        end = Mathf.Min(samples.Length - 1, end + padding);

        int length = end - start + 1;
        float[] trimmed = new float[length];
        System.Array.Copy(samples, start, trimmed, 0, length);

        return trimmed;
    }

    /// <summary>线性插值重采样，并转成 16 位 PCM。</summary>
    private static short[] ResampleToPcm16(float[] source, int sourceRate, int targetRate)
    {
        if (sourceRate == targetRate)
        {
            short[] direct = new short[source.Length];

            for (int i = 0; i < source.Length; i++)
                direct[i] = ToInt16(source[i]);

            return direct;
        }

        int targetLength = Mathf.RoundToInt(source.Length * (float)targetRate / sourceRate);

        if (targetLength <= 0)
            return new short[0];

        short[] output = new short[targetLength];
        float step = (float)source.Length / targetLength;

        for (int i = 0; i < targetLength; i++)
        {
            float pos = i * step;
            int i0 = (int)pos;
            int i1 = Mathf.Min(i0 + 1, source.Length - 1);
            float t = pos - i0;

            output[i] = ToInt16(Mathf.Lerp(source[i0], source[i1], t));
        }

        return output;
    }

    private static short ToInt16(float value)
    {
        int v = Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 32767f);
        return (short)Mathf.Clamp(v, short.MinValue, short.MaxValue);
    }
}
