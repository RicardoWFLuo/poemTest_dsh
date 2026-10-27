using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 百度 AI 平台的 access_token 获取与缓存。
///
/// 语音识别（vop.baidu.com）和语音合成（tsn.baidu.com）用的是**同一个 token**，
/// 所以抽出来共用，避免各自重复请求。
/// token 有效期约 30 天，这里缓存并提前 5 分钟过期。
/// </summary>
public static class BaiduAuth
{
    private const string TokenUrl = "https://aip.baidubce.com/oauth/2.0/token";

    private static string token;
    private static float expireAt;

    [Serializable]
    private class TokenResponse
    {
        public string access_token;
        public int expires_in;
        public string error;
        public string error_description;
    }

    private static bool HasValidToken =>
        !string.IsNullOrEmpty(token) && Time.realtimeSinceStartup < expireAt;

    /// <summary>
    /// 取一个可用的 token。onDone 里的参数为 null 表示失败（失败原因已打日志）。
    /// 必须通过 StartCoroutine 驱动。
    /// </summary>
    public static IEnumerator EnsureToken(string apiKey, string secretKey, Action<string> onDone)
    {
        if (HasValidToken)
        {
            onDone?.Invoke(token);
            yield break;
        }

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
        {
            Debug.LogError("[百度] 缺少 API Key / Secret Key。\n" +
                           "请填到 Assets/Resources/api_keys.json 的 baiduApiKey 与 baiduSecretKey。");
            onDone?.Invoke(null);
            yield break;
        }

        string url = $"{TokenUrl}?grant_type=client_credentials" +
                     $"&client_id={UnityWebRequest.EscapeURL(apiKey)}" +
                     $"&client_secret={UnityWebRequest.EscapeURL(secretKey)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 25;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[百度] 获取 token 失败：{req.error}\n{req.downloadHandler.text}");
                onDone?.Invoke(null);
                yield break;
            }

            TokenResponse parsed = null;

            try
            {
                parsed = JsonUtility.FromJson<TokenResponse>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[百度] token 返回无法解析：{e.Message}");
            }

            if (parsed == null || string.IsNullOrEmpty(parsed.access_token))
            {
                string reason = parsed != null ? parsed.error_description : req.downloadHandler.text;
                Debug.LogError($"[百度] 没有拿到 access_token：{reason}");
                onDone?.Invoke(null);
                yield break;
            }

            token = parsed.access_token;

            int lifetime = parsed.expires_in > 0 ? parsed.expires_in : 2592000;
            expireAt = Time.realtimeSinceStartup + Mathf.Max(60f, lifetime - 300f);

            onDone?.Invoke(token);
        }
    }

    /// <summary>每次进入 Play 清掉缓存（关闭域重载时静态字段会跨会话保留）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        token = null;
        expireAt = 0f;
    }
}
