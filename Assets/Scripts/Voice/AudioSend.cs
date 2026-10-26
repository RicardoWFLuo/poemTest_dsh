using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 百度智能云语音识别（ASR）封装。
/// 密钥优先取 Inspector 里填的值，留空则回退到 Assets/Resources/api_keys.json / 环境变量。
/// </summary>
public class AudioSend : MonoBehaviour
{
    [System.Serializable]
    private class TokenResponse
    {
        public string access_token;
        public string error;
        public string error_description;
    }

    [System.Serializable]
    private class ASRResponse
    {
        public int err_no;
        public string err_msg;
        public string[] result;
    }

    /// <summary>百度 ASR 请求体。用 JsonUtility 序列化可避免手工拼字符串导致的转义问题。</summary>
    [System.Serializable]
    private class ASRRequest
    {
        public string format;
        public int rate;
        public int channel;
        public string cuid;
        public string token;
        public int dev_pid;
        public string speech;
        public int len;
    }

    [Header("API KEY（留空则读取 Assets/Resources/api_keys.json）")]
    public string apiKey;

    [Header("SECRET KEY（留空则读取 Assets/Resources/api_keys.json）")]
    public string secretKey;

    private string accessToken;
    private const string TokenUrl = "https://aip.baidubce.com/oauth/2.0/token";
    private const string ASRUrl = "https://vop.baidu.com/server_api";
    private const int TimeoutSeconds = 30;

    private string ResolvedApiKey =>
        string.IsNullOrWhiteSpace(apiKey) ? ApiKeyConfig.BaiduApiKey : apiKey.Trim();

    private string ResolvedSecretKey =>
        string.IsNullOrWhiteSpace(secretKey) ? ApiKeyConfig.BaiduSecretKey : secretKey.Trim();

    IEnumerator Start()
    {
        yield return StartCoroutine(GetAccessToken());
    }

    IEnumerator GetAccessToken()
    {
        accessToken = null;

        string key = ResolvedApiKey;
        string secret = ResolvedSecretKey;

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(secret))
        {
            Debug.LogError("[AudioSend] 未配置百度语音识别密钥，请在 Assets/Resources/api_keys.json 中填写，或在 Inspector 中赋值。");
            yield break;
        }

        string url = $"{TokenUrl}?grant_type=client_credentials&client_id={UnityWebRequest.EscapeURL(key)}&client_secret={UnityWebRequest.EscapeURL(secret)}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = TimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"获取Token失败: {request.error}");
                yield break;
            }

            TokenResponse response = null;
            try
            {
                response = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogError("[AudioSend] Token 响应解析失败：" + e.Message);
            }

            if (response != null && !string.IsNullOrEmpty(response.access_token))
            {
                accessToken = response.access_token;
                Debug.Log("Access Token获取成功！");
            }
            else
            {
                Debug.LogError($"获取Token失败: {response?.error} {response?.error_description}");
            }
        }
    }

    // 调用语音识别API
    public IEnumerator RecognizeSpeech(byte[] audioData, System.Action<string> callback)
    {
        if (audioData == null || audioData.Length == 0)
        {
            callback?.Invoke(null);
            yield break;
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogWarning("Access Token无效，尝试重新获取...");
            yield return StartCoroutine(GetAccessToken());
            if (string.IsNullOrEmpty(accessToken))
            {
                callback?.Invoke(null);
                yield break;
            }
        }

        // 1. 将音频数据转换为 Base64 字符串
        string speechBase64 = Convert.ToBase64String(audioData);

        // 2. 构建 JSON 请求体
        ASRRequest asrRequest = new ASRRequest
        {
            format = "pcm",
            rate = 16000,
            channel = 1,
            cuid = GetCUID(),
            token = accessToken,
            dev_pid = 1537,
            speech = speechBase64,
            len = audioData.Length
        };

        string jsonData = JsonUtility.ToJson(asrRequest);

        // 3. 发送请求
        using (UnityWebRequest request = new UnityWebRequest(ASRUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = TimeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"网络请求失败: {request.error}");
                callback?.Invoke(null);
                yield break;
            }

            string jsonResponse = request.downloadHandler.text;
            Debug.Log($"API响应: {jsonResponse}");

            ASRResponse result = null;
            try
            {
                result = JsonUtility.FromJson<ASRResponse>(jsonResponse);
            }
            catch (Exception e)
            {
                Debug.LogError("[AudioSend] 识别响应解析失败：" + e.Message);
            }

            // 注意：百度接口在识别失败时会返回 result: null 或空数组，
            // 历史版本直接取 result[0] 会抛 NullReferenceException / IndexOutOfRange。
            if (result != null && result.err_no == 0 &&
                result.result != null && result.result.Length > 0 &&
                !string.IsNullOrEmpty(result.result[0]))
            {
                string recognizedText = result.result[0];
                Debug.Log($"识别成功: {recognizedText}");
                callback?.Invoke(recognizedText);
            }
            else
            {
                Debug.LogError($"识别失败，错误码: {result?.err_no}, 错误信息: {result?.err_msg}");
                callback?.Invoke(null);
            }
        }
    }

    private string GetCUID()
    {
        string deviceId = SystemInfo.deviceUniqueIdentifier;

        if (string.IsNullOrEmpty(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
        {
            deviceId = PlayerPrefs.GetString("VoiceCUID", "");
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = System.Guid.NewGuid().ToString();
                PlayerPrefs.SetString("VoiceCUID", deviceId);
                PlayerPrefs.Save();
            }
        }

        // 百度要求 cuid 长度在 60 字节以内（按 UTF-8 字节数计算）
        if (System.Text.Encoding.UTF8.GetByteCount(deviceId) > 60)
        {
            deviceId = deviceId.Substring(0, 60);
        }

        return deviceId;
    }
}
