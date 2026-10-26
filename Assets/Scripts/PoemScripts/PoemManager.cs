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

    [Header("请求参数")]
    [Tooltip("文本模型。当前使用 qwen3.8-flash（免费额度 100 万 Token，到期 2026/11/25）。" +
             "换模型只需改这里，代码已兼容 output.text 与 output.choices 两种返回结构。")]
    public string textModel = "qwen3.8-flash";

    [Tooltip("文生图模型。必须是「图像生成」类模型（wan 系列 / qwen-image 系列），" +
             "不能用「视觉模型」（如 qwen-mt-image，那类模型读图并输出文本，不会返回图片）。" +
             "若留空或不可用，会自动回退到 PoemSO 里的内置插画。")]
    public string imageModel = "wan2.7-image-pro";

    public float requestTimeout = 60f;

    public bool isTextGenerating;
    private const string textApiUrl = "https://dashscope.aliyuncs.com/api/v1/services/aigc/text-generation/generation";
    public bool isImageGenerating;
    private const string imageApiUrl = "https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation";

    private Texture2D generatedTexture;
    private bool warnedMissingKey;

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

    public void OpenCanvasAndExplain(PoemSO poemSO)
    {
        if (poemSO == null || isTextGenerating)
            return;

        poemTitleText.text = poemSO.poemTitle;
        poemExplainText.text = textLoadingText;
        poemTextCanvas.gameObject.SetActive(true);

        StartCoroutine(SendAIRequest(poemSO));
    }

    public void OpenImageCanvasAndGenerate(PoemSO poemSO)
    {
        if (poemSO == null || isImageGenerating)
            return;

        poemImageTitleText.text = poemSO.poemTitle;
        poemImageGenetatingText.text = imageLoadingText;
        ClearGeneratedTexture();
        poemImageCanvas.gameObject.SetActive(true);

        StartCoroutine(SendImageAIRequest(poemSO));
    }


    IEnumerator SendAIRequest(PoemSO poemSO)
    {
        isTextGenerating = true;

        string apiKey = ResolveTextKey();
        if (WarnIfMissingKey(apiKey, "文本生成 API Key"))
        {
            poemExplainText.text = "尚未配置 AI 密钥，无法解读。";
            isTextGenerating = false;
            yield break;
        }

        string prompt = $@"
            请用客观、简洁的现代汉语，解释这首古诗，要求如下：
            1.  字数控制在200-300字之间，不啰嗦、不冗余
            2.  必须使用正确的标点符号（逗号、句号、顿号），语句通顺
            3.  不要口语化、哄小孩的语气，直接客观解释诗句的含义和全诗主旨
            4.  不要分标题、不要列表、不要多余互动，用连贯的段落呈现
            5.  不要重复全诗原文，只做解释
            古诗：《{poemSO.poemTitle}》
            作者：{poemSO.poet}
            原文：{poemSO.poemContent}
            {poemSO.promptText}";

        RequestData requestData = new RequestData();
        requestData.model = textModel;
        requestData.input = new InputData();
        requestData.input.messages = new QwenChatMessage[]
        {
            new QwenChatMessage { role = "user", content = prompt }
        };
        requestData.parameters = new { };

        string json = JsonUtility.ToJson(requestData);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(textApiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.timeout = Mathf.CeilToInt(requestTimeout);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                QwenResponse response = null;
                try
                {
                    response = JsonUtility.FromJson<QwenResponse>(req.downloadHandler.text);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[PoemManager] 文本响应解析失败：" + e.Message);
                }

                if (response != null && response.output != null && !string.IsNullOrEmpty(response.output.text))
                {
                    poemExplainText.text = response.output.text;
                }
                else if (response != null && response.output != null &&
                         response.output.choices != null && response.output.choices.Length > 0 &&
                         response.output.choices[0].message != null &&
                         !string.IsNullOrEmpty(response.output.choices[0].message.content))
                {
                    // 新版 messages 入参的返回结构
                    poemExplainText.text = response.output.choices[0].message.content;
                }
                else
                {
                    poemExplainText.text = "抱歉，我的大脑宕机了。";
                    Debug.LogError("[PoemManager] 文本响应结构无法识别，原始返回：\n" + req.downloadHandler.text);
                }
            }
            else
            {
                poemExplainText.text = $"请求失败：{req.error}\n请检查API Key或网络。";
                Debug.LogError($"[PoemManager] 请求失败（模型 {textModel}）：{req.error}\n{req.downloadHandler.text}");
            }
        }

        isTextGenerating = false;

    }

    IEnumerator SendImageAIRequest(PoemSO poemSO)
    {
        isImageGenerating = true;

        string apiKey = ResolveImageKey();
        if (WarnIfMissingKey(apiKey, "文生图 API Key"))
        {
            poemImageGenetatingText.text = "尚未配置 AI 密钥，无法绘画。";
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
                    yield return StartCoroutine(DownloadImage(url));
                }
                else
                {
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
                poemImageGenetatingText.text = "生成失败";
            }
        }

        isImageGenerating = false;
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

    IEnumerator DownloadImage(string url)
    {
        if (string.IsNullOrEmpty(url))
            yield break;

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
                poemImageGenetatingText.text = "描绘完成";
            }
            else
            {
                Debug.LogError("图片下载失败：" + req.error);
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
        if (isTextGenerating)
        {
            StopAllCoroutines();
            isTextGenerating = false;
        }

        if (poemTextCanvas != null)
            poemTextCanvas.gameObject.SetActive(false);
    }

    public void CloseImageCanvas()
    {
        if (isImageGenerating)
        {
            StopAllCoroutines();
            isImageGenerating = false;
        }

        if (poemImageCanvas != null)
            poemImageCanvas.gameObject.SetActive(false);

        ClearGeneratedTexture();
    }

    public void CloseMainCanvas()
    {
        if (playerUICanvas != null) playerUICanvas.gameObject.SetActive(false);
        if (ExpCanvas != null) ExpCanvas.gameObject.SetActive(false);
        if (MenuToggle != null) MenuToggle.gameObject.SetActive(false);
    }

    public void OpenMainCanvas()
    {
        if (playerUICanvas != null) playerUICanvas.gameObject.SetActive(true);
        if (ExpCanvas != null) ExpCanvas.gameObject.SetActive(true);
        if (MenuToggle != null) MenuToggle.gameObject.SetActive(true);
    }
}
