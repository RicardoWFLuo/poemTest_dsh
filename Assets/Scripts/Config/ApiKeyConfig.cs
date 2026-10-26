using System;
using UnityEngine;

/// <summary>
/// API 密钥集中读取工具。
///
/// 背景：历史版本把阿里云百炼（DashScope / 通义千问）和百度语音识别的
/// 真实密钥直接写在了 Assets/Scenes/RPG1.unity 里并提交进了 Git 仓库，
/// 任何人克隆仓库都能拿到可用密钥。现在改为：
///
///   1. 优先读取环境变量（方便 CI / 本地调试）；
///   2. 其次读取 Assets/Resources/api_keys.json（该文件已被 .gitignore 忽略）；
///   3. 都取不到时返回空串，调用方给出友好提示。
///
/// 请在项目根目录的 Assets/Resources/ 下新建 api_keys.json，
/// 内容参照 Assets/Resources/api_keys.example.json。
/// </summary>
public static class ApiKeyConfig
{
    [Serializable]
    private class ApiKeys
    {
        public string qwenTextKey;
        public string qwenImageKey;
        public string baiduApiKey;
        public string baiduSecretKey;
        public string deepseekKey;
    }

    private const string ResourceName = "api_keys";

    private const string EnvQwenText = "POEMTEST_QWEN_TEXT_KEY";
    private const string EnvQwenImage = "POEMTEST_QWEN_IMAGE_KEY";
    private const string EnvBaiduApi = "POEMTEST_BAIDU_API_KEY";
    private const string EnvBaiduSecret = "POEMTEST_BAIDU_SECRET_KEY";
    private const string EnvDeepSeek = "POEMTEST_DEEPSEEK_KEY";

    private static ApiKeys keys;
    private static bool loadAttempted;

    /// <summary>通义千问文本生成（诗词解读）密钥。</summary>
    public static string QwenTextKey => Resolve(EnvQwenText, () => Load().qwenTextKey);

    /// <summary>通义万相文生图（诗词配图）密钥。</summary>
    public static string QwenImageKey => Resolve(EnvQwenImage, () => Load().qwenImageKey);

    /// <summary>百度语音识别 API Key。</summary>
    public static string BaiduApiKey => Resolve(EnvBaiduApi, () => Load().baiduApiKey);

    /// <summary>百度语音识别 Secret Key。</summary>
    public static string BaiduSecretKey => Resolve(EnvBaiduSecret, () => Load().baiduSecretKey);

    /// <summary>
    /// DeepSeek 官方 API 密钥（api.deepseek.com）。
    /// 注意：它和百炼的 Key 不能混用，两个平台的鉴权是独立的。
    /// </summary>
    public static string DeepSeekKey => Resolve(EnvDeepSeek, () => Load().deepseekKey);

    /// <summary>是否至少配置了一组密钥，用于启动时给出提示。</summary>
    public static bool HasAnyKey =>
        !string.IsNullOrEmpty(QwenTextKey) ||
        !string.IsNullOrEmpty(BaiduApiKey) ||
        !string.IsNullOrEmpty(DeepSeekKey);

    private static string Resolve(string envName, Func<string> fromFile)
    {
        string fromEnv = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        string value = fromFile();
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static ApiKeys Load()
    {
        if (loadAttempted)
        {
            return keys;
        }

        loadAttempted = true;

        TextAsset asset = Resources.Load<TextAsset>(ResourceName);
        if (asset == null)
        {
            Debug.LogWarning(
                "[ApiKeyConfig] 未找到 Assets/Resources/api_keys.json，AI 与语音功能将不可用。" +
                "请复制 api_keys.example.json 为 api_keys.json 并填入自己的密钥。");
            keys = new ApiKeys();
            return keys;
        }

        try
        {
            keys = JsonUtility.FromJson<ApiKeys>(asset.text) ?? new ApiKeys();
        }
        catch (Exception e)
        {
            Debug.LogError("[ApiKeyConfig] api_keys.json 解析失败：" + e.Message);
            keys = new ApiKeys();
        }

        return keys;
    }
}
