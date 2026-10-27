using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 百度语音合成（TTS）。
///
/// 优点：**一次请求直接返回音频字节**，不像百炼 CosyVoice 要「提交任务 → 轮询 → 下载音频」。
/// 和语音识别共用同一个 access_token（见 BaiduAuth）。
///
/// 接口要点：
///   · POST https://tsn.baidu.com/text2audio
///   · 参数走 form-urlencoded：tex / tok / cuid / ctp=1 / lan=zh / spd / pit / vol / per / aue
///   · 成功时返回的 Content-Type 是 audio/*；失败时返回 JSON（err_no / err_msg）
///   · 单次文本上限约 60 个汉字，超长需要分段请求
/// </summary>
public class BaiduTtsClient : MonoBehaviour
{
    [Header("接口")]
    public string ttsUrl = "https://tsn.baidu.com/text2audio";

    [Tooltip("设备标识，随便一个稳定字符串即可。")]
    public string cuid = "poemtest-unity";

    [Header("音色与参数")]
    [Tooltip("发音人：0=度小美(女) 1=度小宇(男) 3=度逍遥(男) 4=度丫丫(童声)。")]
    public int per = 3;

    [Tooltip("语速 0-15，默认 5。")]
    public int spd = 5;

    [Tooltip("音调 0-15，默认 5。")]
    public int pit = 5;

    [Tooltip("音量 0-15，默认 5。")]
    public int vol = 5;

    [Tooltip("音频格式：3=mp3 4=pcm16k 5=pcm8k 6=wav。")]
    public int aue = 3;

    [Tooltip("单次请求超时（秒）。")]
    public int timeout = 25;

    [Tooltip("单次最多朗读多少汉字（百度上限约 60，超了会报错）。")]
    public int maxChars = 60;

    [Serializable]
    private class TtsError
    {
        public int err_no;
        public string err_msg;
    }

    /// <summary>
    /// 合成语音。成功回调 AudioClip，失败回调中文原因。两者一定回调其一。
    /// </summary>
    public IEnumerator Synthesize(string text, Action<AudioClip> onClip, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onError?.Invoke("没有要朗读的内容。");
            yield break;
        }

        string spoken = text.Trim();

        if (spoken.Length > maxChars)
            spoken = spoken.Substring(0, maxChars);

        string apiKey = ApiKeyConfig.BaiduApiKey;
        string secretKey = ApiKeyConfig.BaiduSecretKey;

        string token = null;
        yield return StartCoroutine(BaiduAuth.EnsureToken(apiKey, secretKey, t => token = t));

        if (string.IsNullOrEmpty(token))
        {
            onError?.Invoke("语音合成凭证获取失败。");
            yield break;
        }

        // 参数必须 form-urlencoded；tex 里的中文与标点都要转义
        string form = "tex=" + UnityWebRequest.EscapeURL(spoken) +
                      "&tok=" + UnityWebRequest.EscapeURL(token) +
                      "&cuid=" + UnityWebRequest.EscapeURL(cuid) +
                      "&ctp=1&lan=zh" +
                      "&spd=" + spd +
                      "&pit=" + pit +
                      "&vol=" + vol +
                      "&per=" + per +
                      "&aue=" + aue;

        byte[] body = Encoding.UTF8.GetBytes(form);
        AudioClip clip = null;
        string contentType = null;
        string errorJson = null;

        using (UnityWebRequest req = new UnityWebRequest(ttsUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerAudioClip(ttsUrl, AudioType.MPEG);
            req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            req.timeout = timeout;

            yield return req.SendWebRequest();

            contentType = req.GetResponseHeader("Content-Type");

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[语音合成] 请求失败：{req.error}");
                onError?.Invoke("语音合成请求失败了。");
                yield break;
            }

            // 失败时百度会返回 JSON 而不是音频，必须先判断 Content-Type
            if (!string.IsNullOrEmpty(contentType) &&
                contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                byte[] raw = req.downloadHandler != null ? req.downloadHandler.data : null;

                if (raw != null)
                    errorJson = Encoding.UTF8.GetString(raw);

                Debug.LogError($"[语音合成] 返回错误：{errorJson}");
            }
            else
            {
                clip = DownloadHandlerAudioClip.GetContent(req);
            }
        }

        if (clip == null)
        {
            TtsError parsed = null;

            if (!string.IsNullOrEmpty(errorJson))
            {
                try
                {
                    parsed = JsonUtility.FromJson<TtsError>(errorJson);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[语音合成] 错误返回无法解析：{e.Message}");
                }
            }

            onError?.Invoke(parsed != null ? DescribeError(parsed.err_no) : "语音合成失败。");
            yield break;
        }

        onClip?.Invoke(clip);
    }

    private static string DescribeError(int errNo)
    {
        switch (errNo)
        {
            case 500: return "语音合成服务内部错误。";
            case 501: return "语音合成参数有误。";
            case 502: return "语音合成凭证失效。";
            case 503: return "语音合成服务暂时不可用。";
            case 504: return "语音合成超时。";
            default: return $"语音合成失败（错误码 {errNo}）。";
        }
    }
}
