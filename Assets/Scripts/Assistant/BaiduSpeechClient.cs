using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 百度短语音识别（REST）。
///
/// 流程：
///   1. 用 API Key / Secret Key 换 access_token（有效期约 30 天，这里缓存起来）
///   2. 把 16k / 16bit / 单声道 的裸 PCM base64 后 POST 到 vop.baidu.com
///   3. 返回 result[0] 就是识别文本
///
/// 为什么选百度：它接受「直接带音频数据的 HTTP POST」，
/// 不需要先把音频传到公网可访问的 OSS —— 对单机游戏来说少一层依赖。
/// </summary>
public class BaiduSpeechClient : MonoBehaviour
{
    [Header("接口")]
    public string tokenUrl = "https://aip.baidubce.com/oauth/2.0/token";
    public string recognizeUrl = "https://vop.baidu.com/server_api";

    [Tooltip("识别模型。1537=普通话(纯中文)，1536=普通话(带标点)，1737=英文。")]
    public int devPid = 1537;

    [Tooltip("单次请求超时（秒）。")]
    public int timeout = 25;

    [Tooltip("超出这个秒数的录音直接拒绝（百度上限 60 秒）。")]
    public int maxSeconds = 55;

    [Tooltip("设备标识，随便一个稳定字符串即可。")]
    public string cuid = "poemtest-unity";

    [Serializable]
    private class RecognizeRequest
    {
        public string format;
        public int rate;
        public int channel;
        public string cuid;
        public string token;
        public string speech;
        public int len;
        public int dev_pid;
    }

    [Serializable]
    private class RecognizeResponse
    {
        public int err_no;
        public string err_msg;
        public string[] result;
    }

    /// <summary>
    /// 识别一段 PCM。onResult 收到识别文本；失败时 onError 收到可展示的中文原因。
    /// 两者一定回调其中之一。
    /// </summary>
    public IEnumerator Recognize(short[] pcm, int sampleRate, Action<string> onResult, Action<string> onError)
    {
        if (pcm == null || pcm.Length == 0)
        {
            onError?.Invoke("没有录到声音。");
            yield break;
        }

        float seconds = (float)pcm.Length / Mathf.Max(1, sampleRate);

        if (seconds > maxSeconds)
        {
            onError?.Invoke($"录音太长了（{seconds:0.#} 秒），请控制在 {maxSeconds} 秒以内。");
            yield break;
        }

        string apiKey = ApiKeyConfig.BaiduApiKey;
        string secretKey = ApiKeyConfig.BaiduSecretKey;

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
        {
            Debug.LogError("[语音识别] 缺少百度 API Key / Secret Key。\n" +
                           "请填到 Assets/Resources/api_keys.json 的 baiduApiKey 与 baiduSecretKey。");
            onError?.Invoke("还没有配置语音识别的密钥。");
            yield break;
        }

        // ① 取 token（与语音合成共用，见 BaiduAuth）
        string authToken = null;
        yield return StartCoroutine(BaiduAuth.EnsureToken(apiKey, secretKey, t => authToken = t));

        if (string.IsNullOrEmpty(authToken))
        {
            onError?.Invoke("获取语音识别凭证失败，请检查密钥。");
            yield break;
        }

        // ② 转 base64
        byte[] raw = new byte[pcm.Length * 2];

        for (int i = 0; i < pcm.Length; i++)
        {
            raw[i * 2] = (byte)(pcm[i] & 0xFF);
            raw[i * 2 + 1] = (byte)((pcm[i] >> 8) & 0xFF);
        }

        var request = new RecognizeRequest
        {
            format = "pcm",
            rate = sampleRate,
            channel = 1,
            cuid = cuid,
            token = authToken,
            speech = Convert.ToBase64String(raw),
            len = raw.Length,
            dev_pid = devPid
        };

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));

        string responseText = null;

        using (UnityWebRequest req = new UnityWebRequest(recognizeUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = timeout;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[语音识别] 请求失败：{req.error}\n{req.downloadHandler.text}");
                onError?.Invoke("语音识别请求失败了，请检查网络。");
                yield break;
            }

            responseText = req.downloadHandler.text;
        }

        // ③ 解析
        RecognizeResponse response = null;

        try
        {
            response = JsonUtility.FromJson<RecognizeResponse>(responseText);
        }
        catch (Exception e)
        {
            Debug.LogError($"[语音识别] 返回内容无法解析：{e.Message}\n{responseText}");
        }

        if (response == null)
        {
            onError?.Invoke("语音识别返回异常。");
            yield break;
        }

        if (response.err_no != 0)
        {
            string friendly = DescribeError(response.err_no);

            Debug.LogWarning($"[语音识别] err_no={response.err_no} err_msg={response.err_msg}");
            onError?.Invoke(friendly);
            yield break;
        }

        if (response.result == null || response.result.Length == 0 || string.IsNullOrEmpty(response.result[0]))
        {
            onError?.Invoke("没听清，能再说一遍吗？");
            yield break;
        }

        onResult?.Invoke(response.result[0].Trim());
    }

    // ---------------------------------------------------------------- 内部

    private static string DescribeError(int errNo)
    {
        switch (errNo)
        {
            case 3301: return "音质太差了，请靠近一点再说。";
            case 3302: return "识别认证失败，请检查百度 Key。";
            case 3303: return "识别服务暂时异常，请稍后再试。";
            case 3304: return "语音识别额度已用完。";
            case 3305: return "今天问得太多了，明天再来吧。";
            case 3307: return "识别服务忙，请稍后再试。";
            case 3308: return "音频格式不对。";
            case 3309: return "音频太长了，请说短一点。";
            case 3310: return "音频太短了，请多说几个字。";
            case 3311: return "识别服务返回异常，请稍后再试。";
            case 3312: return "识别服务暂时不可用。";
            default: return $"语音识别失败（错误码 {errNo}）。";
        }
    }
}
