using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;


// ====================== 生文 ======================
// 说明：阿里云百炼的文本生成接口有两种返回结构：
//   1. 旧版 prompt 入参 → output.text
//   2. 新版 messages 入参 → output.choices[0].message.content
// 这里两种都解析，换模型时不会因为响应结构不同而“解析失败”。
[System.Serializable]
public class QwenResponse
{
    public Output output;
}
[System.Serializable]
public class Output
{
    public string text;
    public QwenChoice[] choices;
}
[System.Serializable]
public class QwenChoice
{
    public QwenMessage message;
}
[System.Serializable]
public class QwenMessage
{
    public string role;
    public string content;
}
[System.Serializable]
public class RequestData
{
    public string model;
    public InputData input;
    public object parameters;
}
[System.Serializable]
public class InputData
{
    public QwenChatMessage[] messages;
}
[System.Serializable]
public class QwenChatMessage
{
    public string role;
    public string content;
}

// ====================== 文本（OpenAI 兼容模式） ======================
// 百炼的新模型（qwen3.x 等）不再走原生 text-generation 路径，
// 请求打到那个地址会返回 400 "url error, please check url"。
// 这里统一改走官方推荐的 OpenAI 兼容接口，模型通用、返回结构稳定。
[System.Serializable]
public class CompatibleChatRequest
{
    public string model;
    public QwenChatMessage[] messages;
    public int max_tokens;
}

[System.Serializable]
public class CompatibleChatResponse
{
    public CompatibleChatChoice[] choices;
}

[System.Serializable]
public class CompatibleChatChoice
{
    public QwenChatMessage message;
    public string finish_reason;
}

// ====================== 文本（原生 text-generation 兜底通道） ======================
// 兼容模式在某些网络环境下会超时，而原生接口 + qwen-turbo 是历史验证过可用的组合，
// 所以保留这一条通道作为兜底。
[System.Serializable]
public class LegacyPromptRequest
{
    public string model;
    public LegacyPromptInput input;
    public object parameters;
}

[System.Serializable]
public class LegacyPromptInput
{
    public string prompt;
}


// ====================== 生图 ======================
[System.Serializable]
public class WanxiangRequest
{
    public string model;
    public WanxiangInput input;
    public WanxiangParameters parameters;
}

[System.Serializable]
public class WanxiangInput
{
    public WanxiangMessage[] messages;
}

[System.Serializable]
public class WanxiangMessage
{
    public string role;
    public WanxiangRequestContent[] content; 
}

[System.Serializable]
public class WanxiangRequestContent
{
    public string text;
}

[System.Serializable]
public class WanxiangParameters
{
    public bool prompt_extend;
    public bool watermark;
    public int n;
    public string negative_prompt;
    public string size;
}
[System.Serializable]
public class WanxiangResponse
{
    public WanxiangOutput output;
}

[System.Serializable]
public class WanxiangOutput
{
    public WanxiangChoice[] choices;
}

[System.Serializable]
public class WanxiangChoice
{
    public WanxiangResponseMessage message;
}

[System.Serializable]
public class WanxiangResponseMessage
{
    public string role;
    public WanxiangResponseContent[] content;
}

[System.Serializable]
public class WanxiangResponseContent
{
    public string image; 
    public string type;
}




/// <summary>文本接口通道。</summary>
public enum TextApiMode
{
    /// <summary>原生 text-generation 接口，经典模型（qwen-turbo / qwen-plus）走这里。</summary>
    Native,

    /// <summary>OpenAI 兼容模式，新模型（qwen3.x）走这里。</summary>
    Compatible,

    /// <summary>
    /// DeepSeek 官方 api.deepseek.com。
    /// 使用 DeepSeek 平台的 Key（与百炼的 Key 不通用），也是助手正在用的那条通道。
    /// </summary>
    DeepSeek
}

public class PoemManager : MonoBehaviour
{
    public static PoemManager Instance;

    [Header("文本KEY（留空则读取 Assets/Resources/api_keys.json）")]
    public string textApiKey;

    [Header("图片KEY（留空则读取 Assets/Resources/api_keys.json）")]
    public string imageApiKey;

    public Canvas poemTextCanvas;
    public TMP_Text poemTitleText;
    public TMP_Text poemExplainText;

    public Canvas poemImageCanvas;
    public RawImage poemRawImage;
    public TMP_Text poemImageTitleText;
    public TMP_Text poemImageGenetatingText;

    public Canvas playerUICanvas;
    public Canvas ExpCanvas;
    public Canvas MenuToggle;

    [Header("加载提示")]
    public string textLoadingText = "我正在为你解读这首诗，请稍等...";
    public string imageLoadingText = "正在绘画";

    [Header("文本接口")]
    [Tooltip("文本模型。\n" +
             "qwen-turbo = 经典模型，走原生接口稳定可用（实测在你的网络环境里可通）；\n" +
             "qwen3.8-flash 等新模型需要走「兼容模式」，部分网络环境下会超时。")]
    public string textModel = "qwen-turbo";

    [Tooltip("主通道。\n" +
             "Native = 原生 text-generation 接口（经典模型走这里，实测可用）；\n" +
             "Compatible = OpenAI 兼容模式（新模型走这里，但你的网络可能会超时）。")]
    public TextApiMode textApiMode = TextApiMode.Native;

    [Tooltip("兼容模式地址。")]
    public string compatibleTextApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";

    [Tooltip("原生 text-generation 地址。")]
    public string nativeTextApiUrl = "https://dashscope.aliyuncs.com/api/v1/services/aigc/text-generation/generation";

    [Tooltip("DeepSeek 官方接口地址（Text Api Mode 选 DeepSeek 时使用，用的是 deepseekKey）。")]
    public string deepSeekTextApiUrl = "https://api.deepseek.com/chat/completions";

    [Tooltip("文本回复的最大 token 数。释义约 200 字，1024 足够，也顺便控制成本。")]
    public int textMaxTokens = 1024;

    [Tooltip("释义正文的最大字数。超出会被截断到最近的句号处，避免撑破卷轴。建议 180-260。")]
    public int maxExplanationChars = 220;

    [Tooltip("请求超时（秒）。原生接口通常 3-15 秒返回，设 20-30 即可，超时能更快察觉。")]
    public float requestTimeout = 30f;

    [Header("兜底与限流")]
    [Tooltip("主通道失败后，是否换另一条通道再试一次。\n" +
             "注意：兼容模式超时会把 requestTimeout 等满，明显拖慢体验，建议保持关闭。")]
    public bool enableTextFallback = false;

    [Tooltip("兜底通道使用的模型。")]
    public string fallbackTextModel = "qwen3.8-flash";

    [Tooltip("「已知可用」的模型。\n" +
             "当配置的模型被接口拒绝（报 url error）时，会自动用它再试一次，\n" +
             "避免因为填错模型名导致整个功能完全不可用。")]
    public string knownGoodTextModel = "qwen-turbo";

    [Tooltip("请求失败后的冷却时间（秒）。避免连续按 F 反复请求。")]
    public float failureCooldownSeconds = 8f;

    [Tooltip("被限流（HTTP 429）后的冷却时间（秒）。限流说明接口是通的，只是调用太快。")]
    public float rateLimitCooldownSeconds = 20f;

    [Header("模型连通性诊断")]
    [Tooltip("进游戏后按这个键，会依次测试候选模型 + 接口组合，把结果打到 Console。")]
    public KeyCode diagnosticsKey = KeyCode.F11;

    [Tooltip("诊断用的候选模型。建议填你控制台里「有免费额度」的那些。")]
    public string[] diagnosticModels = new string[]
    {
        "deepseek-v4.1-flash-1017",
        "qwen3.8-flash",
        "qwen3.8-max-0902",
        "kimi-k3",
        "qwen-turbo"
    };

    [Header("生图")]
    [Tooltip("文生图模型。必须是「图像生成」类模型（wan 系列 / qwen-image 系列），" +
             "不能用「视觉模型」（如 qwen-mt-image，那类模型读图并输出文本，不会返回图片）。" +
             "若留空或不可用，会自动回退到内置插画。")]
    public string imageModel = "wan2.7-image-pro";

    [Tooltip("生图接口地址（原生多模态生成接口，wan 系列使用）。")]
    public string imageApiUrl = "https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation";

    [Header("内置内容导出（仅编辑器生效）")]
    [Tooltip("AI 生成成功后，把结果自动写入 Assets/_PoemContentSource/。\n" +
             "这样你在游戏里点一遍，内容就落盘了，再执行一次「回填内置内容」即可。")]
    public bool exportGeneratedContent = true;

    [Header("内容来源标识")]
    [Tooltip("释义的来源角标。留空则运行时自动在释义画布左上角生成一个，字体沿用释义文本的字体。")]
    public TMP_Text explainSourceText;

    [Tooltip("配图的来源角标。留空则运行时自动在配图画布左上角生成一个。")]
    public TMP_Text imageSourceText;

    [Tooltip("每次打开面板都重新调用 AI。\n" +
             "默认关闭：有缓存或内置内容时直接复用，不重复花钱。\n" +
             "想重新生成某一首时，用菜单「清空诗词内容缓存」更方便。")]
    public bool regenerateOnEveryOpen = false;

    [Header("角标文案（也是 AI 生成内容的显式标识）")]
    public string builtinSourceLabel = "内置内容";
    public string aiSourceLabel = "AI 实时生成";
    public string cachedSourceLabel = "AI 生成 · 已缓存";
    public Color builtinBadgeColor = new Color(0.75f, 0.80f, 0.90f, 1f);
    public Color aiBadgeColor = new Color(0.95f, 0.80f, 0.40f, 1f);
    public Color cachedBadgeColor = new Color(0.70f, 0.85f, 0.70f, 1f);

    public bool isTextGenerating;
    public bool isImageGenerating;

    private Texture2D generatedTexture;
    private bool warnedMissingKey;

    private bool explainRequestSucceeded;
    private bool explainRateLimited;
    private bool explainUrlError;
    private float lastTextFailureTime;
    private float currentTextCooldown;

    private Coroutine textRoutine;
    private Coroutine imageRoutine;

    /// <summary>被「诗词面板」临时隐藏的 HUD 对象，关闭时按这份清单恢复。</summary>
    private readonly List<GameObject> hiddenHudRoots = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (poemTextCanvas != null)
            poemTextCanvas.gameObject.SetActive(false);
        if (poemImageCanvas != null)
            poemImageCanvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(diagnosticsKey))
        {
            StartCoroutine(RunDiagnostics());
        }
    }

    // ====================== 模型连通性诊断 ======================

    private const string ProbePrompt = "只回复两个字：可用";

    /// <summary>
    /// 依次测试「兼容模式」下的每个候选模型，再测一次「原生接口 + knownGoodTextModel」。
    /// 一次按键就能确定哪个组合真正可用，不用反复猜。
    /// 结果含义：
    ///   ✅ 可用            —— 直接把这个模型配上
    ///   ⚠️ 429             —— 接口可达，但该模型当前没有可用配额（免费额度用尽或没开通）
    ///   ❌ 超时            —— 该接口在该模型上不可达
    ///   ❌ url error(400)  —— 该模型不在这条接口上
    /// </summary>
    public IEnumerator RunDiagnostics()
    {
        string apiKey = ResolveTextKey();

        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[诊断] 没有可用的文本 API Key，无法诊断。");
            yield break;
        }

        Debug.Log("=========== 文本模型连通性诊断开始 ===========");
        Debug.Log($"兼容模式地址：{compatibleTextApiUrl}");
        Debug.Log($"原生接口地址：{nativeTextApiUrl}");

        if (diagnosticModels != null)
        {
            foreach (string model in diagnosticModels)
            {
                if (string.IsNullOrEmpty(model))
                    continue;

                yield return StartCoroutine(ProbeCompatible(model, apiKey));
                yield return new WaitForSecondsRealtime(1.5f); // 稍微间隔，避免自己把自己限流
            }
        }

        yield return StartCoroutine(ProbeNative(knownGoodTextModel, apiKey));

        Debug.Log("=========== 诊断结束，请把以上日志整段发给开发者 ===========");
    }

    private IEnumerator ProbeCompatible(string model, string apiKey)
    {
        var body = new CompatibleChatRequest
        {
            model = model,
            messages = new[] { new QwenChatMessage { role = "user", content = ProbePrompt } },
            max_tokens = 16
        };

        yield return StartCoroutine(
            Probe(compatibleTextApiUrl, JsonUtility.ToJson(body), apiKey, "兼容模式 / " + model));
    }

    private IEnumerator ProbeNative(string model, string apiKey)
    {
        var body = new LegacyPromptRequest
        {
            model = model,
            input = new LegacyPromptInput { prompt = ProbePrompt },
            parameters = new { }
        };

        yield return StartCoroutine(
            Probe(nativeTextApiUrl, JsonUtility.ToJson(body), apiKey, "原生接口 / " + model));
    }

    private IEnumerator Probe(string url, string json, string apiKey, string label)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.timeout = 15;

            yield return req.SendWebRequest();

            string responseBody = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;

            if (req.result == UnityWebRequest.Result.Success)
            {
                string text = ExtractExplanation(responseBody);

                if (string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning($"[诊断] {label} → ⚠️ 接口成功但内容为空" +
                                     "（可能是思考型模型把 token 配额都用在了推理上，或返回结构与预期不同）");
                }
                else
                {
                    Debug.Log($"[诊断] {label} → ✅ 可用（返回：{text.Trim()}）");
                }
            }
            else if (req.responseCode == 429)
            {
                Debug.LogWarning($"[诊断] {label} → ⚠️ 429 被拒（接口可达，但该模型当前没有可用配额）\n{responseBody}");
            }
            else if (!string.IsNullOrEmpty(req.error) && req.error.Contains("timeout"))
            {
                Debug.LogError($"[诊断] {label} → ❌ 超时（15 秒无响应）");
            }
            else
            {
                Debug.LogError($"[诊断] {label} → ❌ HTTP {req.responseCode} {req.error}\n{responseBody}");
            }
        }
    }

    /// <summary>优先使用 Inspector 里填的值，留空则回退到 api_keys.json / 环境变量。</summary>
    private string ResolveTextKey()
    {
        return string.IsNullOrWhiteSpace(textApiKey) ? ApiKeyConfig.QwenTextKey : textApiKey.Trim();
    }

    private string ResolveImageKey()
    {
        return string.IsNullOrWhiteSpace(imageApiKey) ? ApiKeyConfig.QwenImageKey : imageApiKey.Trim();
    }

    private bool WarnIfMissingKey(string key, string keyName)
    {
        if (!string.IsNullOrEmpty(key))
            return false;

        if (!warnedMissingKey)
        {
            warnedMissingKey = true;
            Debug.LogError($"[PoemManager] 未配置 {keyName}。请在 Assets/Resources/api_keys.json 中填写密钥，或在 Inspector 中直接赋值。");
        }
        return true;
    }

    /// <summary>释义面板是否正在显示。</summary>
    public bool IsTextCanvasOpen => poemTextCanvas != null && poemTextCanvas.gameObject.activeSelf;

    /// <summary>配图面板是否正在显示。</summary>
    public bool IsImageCanvasOpen => poemImageCanvas != null && poemImageCanvas.gameObject.activeSelf;

    /// <summary>任意诗词面板是否正在显示。</summary>
    public bool IsAnyPoemCanvasOpen => IsTextCanvasOpen || IsImageCanvasOpen;

    public void OpenCanvasAndExplain(PoemSO poemSO)
    {
        if (poemSO == null || isTextGenerating)
            return;

        poemTitleText.text = poemSO.poemTitle;
        poemTextCanvas.gameObject.SetActive(true);

        if (!regenerateOnEveryOpen)
        {
            // ① 之前已经生成过 → 直接复用，不再花钱调接口
            if (TryShowCachedExplanation(poemSO))
                return;

            // ② 有内置内容 → 直接复用
            if (TryShowBuiltinExplanation(poemSO))
                return;
        }

        // ③ 两者都没有，才真正请求 AI
        poemExplainText.text = textLoadingText;
        textRoutine = StartCoroutine(SendAIRequest(poemSO));
    }

    public void OpenImageCanvasAndGenerate(PoemSO poemSO)
    {
        if (poemSO == null || isImageGenerating)
            return;

        poemImageTitleText.text = poemSO.poemTitle;
        poemImageCanvas.gameObject.SetActive(true);

        ClearGeneratedTexture();

        if (!regenerateOnEveryOpen)
        {
            // ① 之前已经生成过 → 直接复用（生图是花钱的，绝不能每次重画）
            if (TryShowCachedIllustration(poemSO))
                return;

            // ② 有内置插画 → 直接复用
            if (TryShowBuiltinIllustration(poemSO))
                return;
        }

        // ③ 两者都没有，才真正请求 AI
        poemImageGenetatingText.text = imageLoadingText;
        imageRoutine = StartCoroutine(SendImageAIRequest(poemSO));
    }


    /// <summary>
    /// 释义请求总入口。
    ///
    /// 通道由 textApiMode 决定；主通道失败后只有开启 enableTextFallback 才换另一条。
    ///
    /// 为什么要能手动指定主通道：实测在部分网络环境下 compatible-mode 会被挡住
    /// （只会超时，拿不到任何响应体），而原生 text-generation 接口是正常的
    /// （能返回 429 这类业务错误）。所以默认走原生 + qwen-turbo。
    ///
    /// 同时带失败冷却：失败后一段时间内不再发起请求，避免连续按 F 把额度打成 429。
    /// </summary>
    IEnumerator SendAIRequest(PoemSO poemSO)
    {
        if (lastTextFailureTime > 0f)
        {
            float elapsed = Time.unscaledTime - lastTextFailureTime;

            if (elapsed < currentTextCooldown)
            {
                int wait = Mathf.CeilToInt(currentTextCooldown - elapsed);
                poemExplainText.text = $"请求太频繁了，请 {wait} 秒后再试。";
                yield break;
            }
        }

        isTextGenerating = true;
        explainRequestSucceeded = false;
        explainRateLimited = false;
        explainUrlError = false;

        // 按通道决定用哪个平台的 Key：DeepSeek 官方与百炼的 Key 不通用，混用只会 401。
        string apiKey;
        string keyHint;

        if (textApiMode == TextApiMode.DeepSeek)
        {
            apiKey = ApiKeyConfig.DeepSeekKey;
            keyHint = "DeepSeek 官方 Key（api_keys.json 的 deepseekKey）";
        }
        else
        {
            apiKey = ResolveTextKey();
            keyHint = "百炼 Key（api_keys.json 的 qwenTextKey）";
        }

        if (WarnIfMissingKey(apiKey, "文本生成 API Key"))
        {
            if (!TryShowBuiltinExplanation(poemSO))
                poemExplainText.text = $"尚未配置 {keyHint}，且这首诗还没有配置内置释义。";

            isTextGenerating = false;
            textRoutine = null;
            yield break;
        }

        string prompt = BuildExplainPrompt(poemSO);

        if (textApiMode == TextApiMode.DeepSeek)
        {
            // DeepSeek 官方：请求体与 OpenAI 兼容格式一致
            yield return StartCoroutine(
                RequestExplanationCompatible(poemSO, prompt, apiKey, textModel, deepSeekTextApiUrl));
        }
        else if (textApiMode == TextApiMode.Native)
        {
            yield return StartCoroutine(RequestExplanationNative(poemSO, prompt, apiKey, textModel));

            if (!explainRequestSucceeded && enableTextFallback)
            {
                Debug.LogWarning("[PoemManager] 原生接口未成功，改用兼容模式再试一次……");
                yield return StartCoroutine(
                    RequestExplanationCompatible(poemSO, prompt, apiKey, fallbackTextModel, compatibleTextApiUrl));
            }
        }
        else
        {
            yield return StartCoroutine(
                RequestExplanationCompatible(poemSO, prompt, apiKey, textModel, compatibleTextApiUrl));

            if (!explainRequestSucceeded && enableTextFallback)
            {
                Debug.LogWarning("[PoemManager] 兼容模式未成功，改用原生接口再试一次……");
                yield return StartCoroutine(RequestExplanationNative(poemSO, prompt, apiKey, fallbackTextModel));
            }
        }

        // 模型被接口拒绝（url error）时，用「已知可用」的模型再试一次。
        // 这类错误返回很快（400），不会让玩家干等，属于低成本自愈。
        // DeepSeek 通道不走这条路径（它的接口地址不同）。
        if (!explainRequestSucceeded && explainUrlError && textApiMode != TextApiMode.DeepSeek &&
            !string.IsNullOrEmpty(knownGoodTextModel) && knownGoodTextModel != textModel)
        {
            Debug.LogWarning($"[PoemManager] 模型「{textModel}」不被该接口接受，改用「{knownGoodTextModel}」再试一次……");
            yield return StartCoroutine(RequestExplanationNative(poemSO, prompt, apiKey, knownGoodTextModel));
        }

        if (explainRequestSucceeded)
        {
            lastTextFailureTime = 0f;
        }
        else
        {
            lastTextFailureTime = Time.unscaledTime;
            currentTextCooldown = explainRateLimited ? rateLimitCooldownSeconds : failureCooldownSeconds;

            if (!TryShowBuiltinExplanation(poemSO))
            {
                poemExplainText.text = explainRateLimited
                    ? $"请求太频繁（被限流），请 {Mathf.CeilToInt(currentTextCooldown)} 秒后再试。"
                    : "文本生成失败，详情见 Console。";
            }
        }

        isTextGenerating = false;
        textRoutine = null;
    }

    private string BuildExplainPrompt(PoemSO poemSO)
    {
        return $@"
            请用客观、简洁的现代汉语，解释这首古诗，要求如下：
            1.  全文严格控制在 150-220 字之间，绝不超过 220 字，不啰嗦、不冗余
            2.  必须使用正确的标点符号（逗号、句号、顿号），语句通顺
            3.  不要口语化、哄小孩的语气，直接客观解释诗句的含义和全诗主旨
            4.  不要分标题、不要列表、不要多余互动，用连贯的段落呈现
            5.  不要重复全诗原文，只做解释
            古诗：《{poemSO.poemTitle}》
            作者：{poemSO.poet}
            原文：{poemSO.poemContent}
            {poemSO.promptText}";
    }

    /// <summary>通道：OpenAI 兼容格式（百炼兼容模式 / DeepSeek 官方共用同一套请求体）。</summary>
    private IEnumerator RequestExplanationCompatible(PoemSO poemSO, string prompt, string apiKey, string model, string url)
    {
        CompatibleChatRequest requestData = new CompatibleChatRequest();
        requestData.model = model;
        requestData.messages = new QwenChatMessage[]
        {
            new QwenChatMessage { role = "user", content = prompt }
        };
        requestData.max_tokens = Mathf.Max(64, textMaxTokens);

        string json = JsonUtility.ToJson(requestData);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.timeout = Mathf.CeilToInt(requestTimeout);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                ApplyExplanationResult(poemSO, req.downloadHandler.text);
            }
            else
            {
                ReportTextRequestFailure("兼容模式", model, url, req);
            }
        }
    }

    /// <summary>通道：原生 text-generation 接口。</summary>
    private IEnumerator RequestExplanationNative(PoemSO poemSO, string prompt, string apiKey, string model)
    {
        var requestData = new LegacyPromptRequest();
        requestData.model = model;
        requestData.input = new LegacyPromptInput { prompt = prompt };
        requestData.parameters = new { };

        string json = JsonUtility.ToJson(requestData);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(nativeTextApiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.timeout = Mathf.CeilToInt(requestTimeout);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                ApplyExplanationResult(poemSO, req.downloadHandler.text);
            }
            else
            {
                ReportTextRequestFailure("原生接口", model, nativeTextApiUrl, req);
            }
        }
    }

    /// <summary>统一的失败诊断：把「限流」和「真失败」区分开，日志要能直接定位问题。</summary>
    private void ReportTextRequestFailure(string channel, string model, string url, UnityWebRequest req)
    {
        string body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;

        if (IsRateLimited(req.responseCode, body))
        {
            explainRateLimited = true;

            Debug.LogError(
                $"[PoemManager] {channel}被限流（HTTP {req.responseCode}，模型 {model}）。\n返回：{body}\n" +
                "重要：返回 429 说明接口和参数都没问题，只是调用太快。等十几秒再试即可。");
            return;
        }

        Debug.LogError($"[PoemManager] {channel}请求失败（模型 {model}，地址 {url}）：{req.error}\n返回：{body}");

        // 标记「模型不被该接口接受」，调用方会用已知可用的模型重试一次
        if (!string.IsNullOrEmpty(body) && body.Contains("url error"))
            explainUrlError = true;
    }

    private static bool IsRateLimited(long responseCode, string body)
    {
        if (responseCode == 429)
            return true;

        return !string.IsNullOrEmpty(body) && body.Contains("Throttling");
    }

    /// <summary>把拿到的返回解析、限长、显示、缓存、导出（两条通道共用）。</summary>
    private void ApplyExplanationResult(PoemSO poemSO, string responseJson)
    {
        string explanation = ExtractExplanation(responseJson);

        if (string.IsNullOrEmpty(explanation))
        {
            Debug.LogError("[PoemManager] 文本响应结构无法识别，原始返回：\n" + responseJson);
            return;
        }

        explanation = LimitExplanationLength(explanation);

        poemExplainText.text = explanation;
        SetContentSource(true, ContentSourceKind.AiGenerated);

        // 落盘缓存，下次直接复用，不再重复调用（省钱）
        PoemContentCache.SaveExplanation(poemSO.Id, explanation);

        if (exportGeneratedContent)
            PoemContentExport.SaveExplanation(poemSO.Id, explanation);

        explainRequestSucceeded = true;
    }

    IEnumerator SendImageAIRequest(PoemSO poemSO)
    {
        isImageGenerating = true;

        string apiKey = ResolveImageKey();
        if (WarnIfMissingKey(apiKey, "文生图 API Key"))
        {
            if (!TryShowBuiltinIllustration(poemSO))
                poemImageGenetatingText.text = "尚未配置 AI 密钥，且这首诗还没有配置内置插画。";

            isImageGenerating = false;
            yield break;
        }

        WanxiangRequest requestData = new WanxiangRequest();
        requestData.model = imageModel;
        requestData.input = new WanxiangInput();
        requestData.input.messages = new WanxiangMessage[1];
        requestData.input.messages[0] = new WanxiangMessage();
        requestData.input.messages[0].role = "user";
        requestData.input.messages[0].content = new WanxiangRequestContent[1];
        requestData.input.messages[0].content[0] = new WanxiangRequestContent();
        requestData.input.messages[0].content[0].text = "古诗《" + poemSO.poemTitle + "》场景插画，" + poemSO.promptImage + "，中国古风，高清";

        requestData.parameters = new WanxiangParameters();
        requestData.parameters.size = "1024*576";
        requestData.parameters.n = 1;
        requestData.parameters.watermark = false;
        requestData.parameters.prompt_extend = true;
        requestData.parameters.negative_prompt = "low quality,ugly";

        string json = JsonUtility.ToJson(requestData);

        byte[] postData = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(imageApiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(postData);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.timeout = Mathf.CeilToInt(requestTimeout);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                WanxiangResponse resp = null;
                try
                {
                    resp = JsonUtility.FromJson<WanxiangResponse>(req.downloadHandler.text);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[PoemManager] 生图响应解析失败：" + e.Message);
                }

                string url = ExtractImageUrl(resp);
                if (!string.IsNullOrEmpty(url))
                {
                    yield return StartCoroutine(DownloadImage(url, poemSO));
                }
                else
                {
                    if (!TryShowBuiltinIllustration(poemSO))
                        poemImageGenetatingText.text = "响应解析失败";
                    Debug.LogError(
                        $"[PoemManager] 生图响应里没有图片 URL（模型：{imageModel}）。\n" +
                        "常见原因：\n" +
                        "  1. 该模型不是文生图模型（「视觉模型」只读图并返回文字，不会返回图片）；\n" +
                        "  2. 模型 code 拼写有误或已下线；\n" +
                        "  3. 额度用尽或被限流。\n" +
                        "请在百炼控制台确认 imageModel 属于「图像生成」类（wan 系列 / qwen-image 系列）。\n" +
                        "原始返回：\n" + req.downloadHandler.text);
                }
            }
            else
            {
                Debug.LogError($"[PoemManager] 生图请求失败（模型 {imageModel}）：{req.error}\n{req.downloadHandler.text}");

                if (!TryShowBuiltinIllustration(poemSO))
                    poemImageGenetatingText.text = "生成失败";
            }
        }

        isImageGenerating = false;
        imageRoutine = null;
    }

    /// <summary>
    /// 逐层判空取出图片 URL。历史版本只在最外层判空，
    /// 且当 content[0] 是文本（模型拒答）时会把 null 传给下载协程。
    /// </summary>
    private string ExtractImageUrl(WanxiangResponse resp)
    {
        if (resp?.output?.choices == null || resp.output.choices.Length == 0)
            return null;

        var message = resp.output.choices[0].message;
        if (message?.content == null || message.content.Length == 0)
            return null;

        foreach (var content in message.content)
        {
            if (content != null && !string.IsNullOrEmpty(content.image))
                return content.image;
        }

        return null;
    }

    IEnumerator DownloadImage(string url, PoemSO poemSO)
    {
        if (string.IsNullOrEmpty(url))
        {
            if (!TryShowBuiltinIllustration(poemSO))
                poemImageGenetatingText.text = "图片地址为空";
            yield break;
        }

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            req.timeout = Mathf.CeilToInt(requestTimeout);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                ClearGeneratedTexture();
                generatedTexture = tex;
                poemRawImage.texture = tex;
                poemRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                poemImageGenetatingText.text = "描绘完成";
                SetContentSource(false, ContentSourceKind.AiGenerated);

                // 落盘缓存，下次直接复用，不再重复调用（生图是真的花钱）
                PoemContentCache.SaveIllustration(poemSO.Id, tex);

                if (exportGeneratedContent)
                    PoemContentExport.SaveImage(poemSO.Id, tex);
            }
            else
            {
                Debug.LogError("图片下载失败：" + req.error);

                if (!TryShowBuiltinIllustration(poemSO))
                    poemImageGenetatingText.text = "图片下载失败";
            }
        }
    }

    /// <summary>释放上一张生成贴图，避免反复生图造成显存泄漏。</summary>
    private void ClearGeneratedTexture()
    {
        if (poemRawImage != null)
            poemRawImage.texture = null;

        if (generatedTexture != null)
        {
            Destroy(generatedTexture);
            generatedTexture = null;
        }
    }

    public void CloseTextCanvas()
    {
        // 只停「释义」这一条协程。
        // 历史上用的是 StopAllCoroutines()，会把正在进行的生图请求一起杀掉。
        if (textRoutine != null)
        {
            StopCoroutine(textRoutine);
            textRoutine = null;
        }

        isTextGenerating = false;

        if (poemTextCanvas != null)
            poemTextCanvas.gameObject.SetActive(false);
    }

    public void CloseImageCanvas()
    {
        if (imageRoutine != null)
        {
            StopCoroutine(imageRoutine);
            imageRoutine = null;
        }

        isImageGenerating = false;

        if (poemImageCanvas != null)
            poemImageCanvas.gameObject.SetActive(false);

        ClearGeneratedTexture();
    }

    /// <summary>
    /// 打开诗词面板时隐藏游戏 HUD（血量、等级、经验条、菜单按钮等）。
    ///
    /// 血量与等级文本未必挂在 playerUICanvas / ExpCanvas 上，
    /// 所以这里额外按「这段文字属于哪个 Canvas」反查一遍，
    /// 保证不管它们挂在哪一层都能被藏起来。
    /// </summary>
    public void CloseMainCanvas()
    {
        hiddenHudRoots.Clear();

        HideHud(playerUICanvas);
        HideHud(ExpCanvas);
        HideHud(MenuToggle);

        if (StatsManager.Instance != null && StatsManager.Instance.healthText != null)
            HideHud(StatsManager.Instance.healthText.GetComponentInParent<Canvas>());

        ExpManager exp = FindObjectOfType<ExpManager>();
        if (exp != null && exp.currentLevelText != null)
            HideHud(exp.currentLevelText.GetComponentInParent<Canvas>());
    }

    /// <summary>关闭诗词面板后恢复 HUD（只恢复自己藏过的那些）。</summary>
    public void OpenMainCanvas()
    {
        foreach (var root in hiddenHudRoots)
        {
            if (root != null)
                root.SetActive(true);
        }

        hiddenHudRoots.Clear();

        // 兜底：三个已知对象确保恢复
        if (playerUICanvas != null) playerUICanvas.gameObject.SetActive(true);
        if (ExpCanvas != null) ExpCanvas.gameObject.SetActive(true);
        if (MenuToggle != null) MenuToggle.gameObject.SetActive(true);
    }

    private void HideHud(Canvas canvas)
    {
        if (canvas != null)
            HideHudObject(canvas.gameObject);
    }

    private void HideHudObject(GameObject target)
    {
        if (target == null)
            return;

        // 绝不隐藏诗词自己的画布
        if (poemTextCanvas != null && target == poemTextCanvas.gameObject)
            return;

        if (poemImageCanvas != null && target == poemImageCanvas.gameObject)
            return;

        if (!target.activeSelf || hiddenHudRoots.Contains(target))
            return;

        target.SetActive(false);
        hiddenHudRoots.Add(target);
    }

    // ====================== 内置内容与来源标识 ======================

    /// <summary>内容来源。用于在界面上区分「预置内容」与「AI 实时生成」。</summary>
    public enum ContentSourceKind
    {
        None,
        Builtin,
        AiGenerated,
        /// <summary>AI 生成过、已缓存，本次直接复用（没有再次调用接口）。</summary>
        Cached
    }

    /// <summary>
    /// 从返回 JSON 里取出释义文本。同时兼容两种结构：
    ///  · OpenAI 兼容模式：choices[0].message.content
    ///  · 原生模式：output.text / output.choices[0].message.content
    /// 这样以后无论换哪个接口地址，解析都不会挂。
    /// </summary>
    private string ExtractExplanation(string responseJson)
    {
        if (string.IsNullOrEmpty(responseJson))
            return null;

        // 1) OpenAI 兼容模式
        try
        {
            var compatible = JsonUtility.FromJson<CompatibleChatResponse>(responseJson);

            if (compatible?.choices != null)
            {
                foreach (var choice in compatible.choices)
                {
                    if (choice?.message != null && !string.IsNullOrEmpty(choice.message.content))
                        return choice.message.content.Trim();
                }
            }
        }
        catch
        {
            // 结构不匹配就继续尝试下一种
        }

        // 2) 原生 text-generation 模式
        try
        {
            var native = JsonUtility.FromJson<QwenResponse>(responseJson);

            if (native?.output != null)
            {
                if (!string.IsNullOrEmpty(native.output.text))
                    return native.output.text.Trim();

                if (native.output.choices != null)
                {
                    foreach (var choice in native.output.choices)
                    {
                        if (choice?.message != null && !string.IsNullOrEmpty(choice.message.content))
                            return choice.message.content.Trim();
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>
    /// 限制释义字数，超出时截断到最近的句号处，避免文字撑破卷轴。
    /// 提示词里已经要求 150-220 字，但模型不一定守规矩，这里是硬性兜底。
    /// </summary>
    private string LimitExplanationLength(string text)
    {
        if (string.IsNullOrEmpty(text) || maxExplanationChars <= 0)
            return text;

        text = text.Replace("\r\n", "\n").Trim();

        if (text.Length <= maxExplanationChars)
            return text;

        int searchEnd = Mathf.Min(maxExplanationChars, text.Length - 1);
        int cut = text.LastIndexOf('。', searchEnd);

        // 找到了合适的句号 → 断在句子结尾，读起来完整
        if (cut >= maxExplanationChars / 2)
            return text.Substring(0, cut + 1).Trim();

        // 找不到合适句号 → 硬截断并加省略号
        return text.Substring(0, Mathf.Min(maxExplanationChars, text.Length)).TrimEnd() + "……";
    }

    /// <summary>
    /// 显示之前生成并缓存的释义（运行时缓存目录，编辑器里也会看项目内容目录）。
    /// 命中就不再调用 AI，避免重复花钱。
    /// </summary>
    private bool TryShowCachedExplanation(PoemSO poemSO)
    {
        if (poemSO == null)
            return false;

        if (!PoemContentCache.TryGetExplanation(poemSO.Id, out string text) || string.IsNullOrEmpty(text))
            return false;

        poemExplainText.text = LimitExplanationLength(text);
        SetContentSource(true, ContentSourceKind.Cached);
        return true;
    }

    /// <summary>显示之前生成并缓存的插画。命中就不再调用 AI。</summary>
    private bool TryShowCachedIllustration(PoemSO poemSO)
    {
        if (poemSO == null)
            return false;

        if (!PoemContentCache.TryGetIllustration(poemSO.Id, out Texture2D texture) || texture == null)
            return false;

        ClearGeneratedTexture();

        if (poemRawImage != null)
        {
            poemRawImage.texture = texture;
            poemRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        poemImageGenetatingText.text = cachedSourceLabel;
        SetContentSource(false, ContentSourceKind.Cached);
        return true;
    }

    /// <summary>显示内置释义。没有配置时返回 false，并隐藏来源角标。</summary>
    private bool TryShowBuiltinExplanation(PoemSO poemSO)
    {
        if (poemSO == null || string.IsNullOrEmpty(poemSO.builtinExplanation))
        {
            SetContentSource(true, ContentSourceKind.None);
            return false;
        }

        poemExplainText.text = poemSO.builtinExplanation;
        SetContentSource(true, ContentSourceKind.Builtin);
        return true;
    }

    /// <summary>显示内置插画。没有配置时返回 false，并隐藏来源角标。</summary>
    private bool TryShowBuiltinIllustration(PoemSO poemSO)
    {
        if (poemSO == null || poemSO.builtinIllustration == null)
        {
            SetContentSource(false, ContentSourceKind.None);
            return false;
        }

        ClearGeneratedTexture();

        if (poemRawImage != null)
        {
            poemRawImage.texture = poemSO.builtinIllustration.texture;
            poemRawImage.uvRect = GetSpriteUvRect(poemSO.builtinIllustration);
        }

        poemImageGenetatingText.text = builtinSourceLabel;
        SetContentSource(false, ContentSourceKind.Builtin);
        return true;
    }

    /// <summary>
    /// 把 Sprite 在图集里的区域换算成 RawImage 的 uvRect。
    /// 直接用 sprite.texture 而不换算的话，图集里的小图会显示成整张图集。
    /// </summary>
    private static Rect GetSpriteUvRect(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return new Rect(0f, 0f, 1f, 1f);

        float textureWidth = sprite.texture.width;
        float textureHeight = sprite.texture.height;

        if (textureWidth <= 0f || textureHeight <= 0f)
            return new Rect(0f, 0f, 1f, 1f);

        Rect rect = sprite.textureRect;

        return new Rect(
            rect.x / textureWidth,
            rect.y / textureHeight,
            rect.width / textureWidth,
            rect.height / textureHeight);
    }

    /// <summary>设置来源角标（同时也是「AI 生成内容」的显式标识）。</summary>
    private void SetContentSource(bool forText, ContentSourceKind kind)
    {
        TMP_Text label = forText ? explainSourceText : imageSourceText;

        if (label == null)
        {
            Canvas canvas = forText ? poemTextCanvas : poemImageCanvas;
            TMP_Text styleSource = forText ? poemExplainText : poemImageTitleText;

            label = CreateBadge(canvas, forText ? "ExplainSourceBadge" : "ImageSourceBadge", styleSource);

            if (forText)
                explainSourceText = label;
            else
                imageSourceText = label;
        }

        if (label == null)
            return;

        switch (kind)
        {
            case ContentSourceKind.Builtin:
                label.text = builtinSourceLabel;
                label.color = builtinBadgeColor;
                label.gameObject.SetActive(true);
                break;

            case ContentSourceKind.AiGenerated:
                label.text = aiSourceLabel;
                label.color = aiBadgeColor;
                label.gameObject.SetActive(true);
                break;

            case ContentSourceKind.Cached:
                label.text = cachedSourceLabel;
                label.color = cachedBadgeColor;
                label.gameObject.SetActive(true);
                break;

            default:
                label.gameObject.SetActive(false);
                break;
        }
    }

    /// <summary>
    /// 在画布左上角自动生成一个来源角标。
    /// 字体直接沿用同画布里已有的文本组件，保证中文能正常显示（不依赖 UI 设置资源）。
    /// </summary>
    private TMP_Text CreateBadge(Canvas canvas, string objectName, TMP_Text styleSource)
    {
        if (canvas == null)
            return null;

        var parent = canvas.transform as RectTransform;
        if (parent == null)
            return null;

        RectTransform rect = UIFactory.NewRect(objectName, parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(520f, 56f);
        rect.anchoredPosition = new Vector2(28f, -28f);

        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (styleSource != null && styleSource.font != null)
            text.font = styleSource.font;
        else
            text.font = UIStyle.Font;

        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
        text.text = "";

        rect.gameObject.SetActive(false);
        return text;
    }
}
