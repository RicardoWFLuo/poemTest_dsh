using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>助手的一次回复。</summary>
[Serializable]
public class AssistantReply
{
    /// <summary>要说的话。</summary>
    public string say;

    /// <summary>动作名（白名单内）。</summary>
    public string action;

    public bool Success => !string.IsNullOrEmpty(say);
}

/// <summary>
/// 助手对话客户端。
///
/// 关键设计：**让模型输出指令化的 JSON，而不是自由文本**。
/// 这样它只能从我们定义的动作里选，不会输出实现不了的动画，也不会跑飞。
/// 模型不听话（返回纯文本）时会自动降级为「只用文本、不做动作」，保证不崩。
///
/// 走的是百炼 OpenAI 兼容接口 —— 实测该接口在你的网络下可用，
/// 而且能把模型名随时换成别的（deepseek / kimi 等）。
/// </summary>
public class AssistantChatClient : MonoBehaviour
{
    [Header("接口")]
    [Tooltip("默认直连 DeepSeek 官方 API。")]
    public string apiUrl = "https://api.deepseek.com/chat/completions";

    [Tooltip("对话模型。deepseek-chat = 快、不推理；deepseek-reasoner = 先推理，更慢更贵。")]
    public string model = "deepseek-chat";

    [Tooltip("单次回复的最大 token。deepseek-chat 不推理，512 足够。\n" +
             "若换成思考型模型（reasoner），要给 2048 以上，否则正文会是空的。")]
    public int maxTokens = 512;

    public float timeout = 30f;

    [Tooltip("用哪个平台的 Key 鉴权。")]
    public AssistantKeySource keySource = AssistantKeySource.DeepSeek;

    [Tooltip("仅测试用的临时 Key。真实 Key 请填到 Assets/Resources/api_keys.json 的 deepseekKey。")]
    public string customApiKey;

    [Header("上下文")]
    [Tooltip("保留几轮历史对话。越多越贵，建议 2-4。")]
    [Range(0, 6)]
    public int historyPairs = 2;

    [Header("人设")]
    [TextArea(6, 14)]
    public string systemPrompt =
        "你是「红骑士」，一位行走江湖多年、饱读诗书的前辈。\n" +
        "玩家是你的后辈，你以长辈提携后辈的口吻与他说话。\n\n" +
        "必须遵守的规则：\n" +
        "1. 自称「我」，可以称玩家为「小友」。语气温和、稳重、有耐心，不说教、不卖弄。\n" +
        "2. 每次回答不超过 40 个字。\n" +
        "3. 你通晓古诗词：能讲清诗句的意思、典故的来历、诗人的生平。\n" +
        "4. 只聊古诗词、读书学习、以及游戏里的事物。遇到无关或不宜的话题，温和地把话题带回诗词。\n" +
        "5. 绝对不输出任何不适合儿童的内容。\n" +
        "6. 只输出一个 JSON 对象，不要代码块标记，不要任何额外说明文字。\n\n" +
        "JSON 格式：\n" +
        "{\"say\":\"你要说的话\",\"action\":\"动作名\"}\n\n" +
        "action 只能从下面这些里选一个：\n" +
        "idle（不做动作）、wave（招手）、nod（点头）、shake（摇头）、think（思考）";

    /// <summary>可作为动作的合法值。</summary>
    public static readonly string[] ActionWhitelist = { "idle", "wave", "nod", "shake", "think" };

    /// <summary>不合适内容的简易兜底文案。</summary>
    public string fallbackSay = "小友，我们还是回到诗里去吧。";

    /// <summary>简易屏蔽词。真实产品应换成服务端内容审核。</summary>
    private static readonly string[] BlockedWords =
    {
        "暴力", "kill", "fuck", "傻逼", "去死"
    };

    private readonly List<QwenChatMessage> history = new List<QwenChatMessage>();

    [Serializable]
    private class ReplyJson
    {
        public string say;
        public string action;
    }

    [Serializable]
    private class ChatMessageList
    {
        public string model;
        public QwenChatMessage[] messages;
        public int max_tokens;
    }

    [Serializable]
    private class ResponseEnvelope
    {
        public ResponseChoice[] choices;
    }

    [Serializable]
    private class ResponseChoice
    {
        public ResponseMessage message;
    }

    [Serializable]
    private class ResponseMessage
    {
        public string role;
        public string content;

        // DeepSeek / 思考型模型会先输出推理过程，正文可能为空
        public string reasoning_content;
    }

    private void Awake()
    {
        // 从 UIStyleSettings 读取配置（那个资源可以在 Inspector 里改，运行时创建的对象不行）
        UIStyleSettings settings = UIStyle.Settings;

        if (settings == null)
            return;

        if (!string.IsNullOrEmpty(settings.assistantApiUrl))
            apiUrl = settings.assistantApiUrl;

        if (!string.IsNullOrEmpty(settings.assistantModel))
            model = settings.assistantModel;

        if (settings.assistantMaxTokens > 0)
            maxTokens = settings.assistantMaxTokens;

        keySource = settings.assistantKeySource;
        customApiKey = settings.assistantApiKeyOverride;
    }

    /// <summary>按 keySource 取对应的密钥。取不到返回 null。</summary>
    private string ResolveApiKey()
    {
        switch (keySource)
        {
            case AssistantKeySource.BailianQwen:
                return ApiKeyConfig.QwenTextKey;

            case AssistantKeySource.Custom:
                return string.IsNullOrWhiteSpace(customApiKey) ? null : customApiKey.Trim();

            default:
                return ApiKeyConfig.DeepSeekKey;
        }
    }

    /// <summary>缺少密钥时给出的、能直接照做的提示。</summary>
    private string MissingKeyHint()
    {
        switch (keySource)
        {
            case AssistantKeySource.BailianQwen:
                return "请在 Assets/Resources/api_keys.json 的 qwenTextKey 里填百炼的 Key。";

            case AssistantKeySource.Custom:
                return "请在 UIStyleSettings 的 Assistant Api Key Override 里填 Key（仅测试用）。";

            default:
                return "请在 Assets/Resources/api_keys.json 的 deepseekKey 里填 DeepSeek 官方平台的 Key" +
                       "（也可以在系统环境变量里设 POEMTEST_DEEPSEEK_KEY）。";
        }
    }

    // ---------------------------------------------------------------- 对外接口

    /// <summary>
    /// 向助手提问。onDone 一定会被调用（成功或失败都会），调用方不用处理异常。
    /// </summary>
    public IEnumerator Ask(string question, Action<AssistantReply> onDone)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            onDone?.Invoke(new AssistantReply { say = "你想问点什么呀？", action = "idle" });
            yield break;
        }

        string apiKey = ResolveApiKey();

        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError($"[助手] 没有找到可用的 API Key（当前使用的是 {keySource}）。{MissingKeyHint()}");

            onDone?.Invoke(new AssistantReply
            {
                say = "我还没有钥匙，暂时不能聊天。",
                action = "shake"
            });
            yield break;
        }

        string cleanedQuestion = question.Trim();

        // 超长提问截断，既省 token 也防滥用
        if (cleanedQuestion.Length > 200)
            cleanedQuestion = cleanedQuestion.Substring(0, 200);

        string body = BuildRequestJson(cleanedQuestion);
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(body);

        string raw = null;

        using (UnityWebRequest req = new UnityWebRequest(apiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(payload);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.timeout = Mathf.CeilToInt(timeout);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                raw = req.downloadHandler.text;
            }
            else if (req.responseCode == 429)
            {
                Debug.LogWarning($"[助手] 被限流：{req.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"[助手] 请求失败（模型 {model}）：{req.error}\n{req.downloadHandler.text}");
            }
        }

        if (string.IsNullOrEmpty(raw))
        {
            onDone?.Invoke(new AssistantReply
            {
                say = "我这会儿有点忙，等会儿再聊好不好？",
                action = "think"
            });
            yield break;
        }

        string content = ExtractContent(raw);

        if (string.IsNullOrEmpty(content))
        {
            onDone?.Invoke(new AssistantReply
            {
                say = "我没听清，能再说一遍吗？",
                action = "think"
            });
            yield break;
        }

        AssistantReply reply = ParseReply(content);

        // 记录历史（只留最近几轮，控制成本）
        RememberTurn(cleanedQuestion, reply.say);

        // 内容兜底
        if (ContainsBlocked(reply.say))
        {
            reply.say = fallbackSay;
            reply.action = "shake";
        }

        onDone?.Invoke(reply);
    }

    /// <summary>清空对话历史（新游戏时调用）。</summary>
    public void ClearHistory()
    {
        history.Clear();
    }

    // ---------------------------------------------------------------- 内部

    private string BuildRequestJson(string question)
    {
        var messages = new List<QwenChatMessage>
        {
            new QwenChatMessage { role = "system", content = systemPrompt }
        };

        // 只带最近 historyPairs 轮
        int maxHistoryMessages = Mathf.Max(0, historyPairs) * 2;
        int start = Mathf.Max(0, history.Count - maxHistoryMessages);

        for (int i = start; i < history.Count; i++)
            messages.Add(history[i]);

        messages.Add(new QwenChatMessage { role = "user", content = question });

        var request = new ChatMessageList
        {
            model = model,
            messages = messages.ToArray(),
            max_tokens = Mathf.Max(32, maxTokens)
        };

        return JsonUtility.ToJson(request);
    }

    private void RememberTurn(string question, string answer)
    {
        if (historyPairs <= 0)
            return;

        history.Add(new QwenChatMessage { role = "user", content = question });
        history.Add(new QwenChatMessage { role = "assistant", content = answer });

        int maxMessages = historyPairs * 2;

        while (history.Count > maxMessages)
            history.RemoveAt(0);
    }

    private string ExtractContent(string responseJson)
    {
        try
        {
            var envelope = JsonUtility.FromJson<ResponseEnvelope>(responseJson);

            if (envelope?.choices != null)
            {
                foreach (var choice in envelope.choices)
                {
                    if (choice?.message == null)
                        continue;

                    if (!string.IsNullOrEmpty(choice.message.content))
                        return choice.message.content;

                    // DeepSeek 这类模型会先推理再回答；
                    // 如果 token 预算被推理吃光，正文就会是空的。
                    if (!string.IsNullOrEmpty(choice.message.reasoning_content))
                    {
                        Debug.LogWarning(
                            "[助手] 模型只返回了推理内容，正文为空。\n" +
                            $"当前 maxTokens = {maxTokens}，可能被推理过程耗尽了。\n" +
                            "请在 UIStyleSettings 里把 Assistant Max Tokens 调大到 2048，" +
                            "或把模型换成非思考型的（如 qwen3.8-flash）。");
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
    /// 解析模型回复。优先按 JSON 约定解析；
    /// 模型不听话（返回纯文本）时降级成「整段当台词、不做动作」。
    /// </summary>
    private static AssistantReply ParseReply(string content)
    {
        string json = StripCodeFence(content);

        int braceStart = json.IndexOf('{');
        int braceEnd = json.LastIndexOf('}');

        if (braceStart >= 0 && braceEnd > braceStart)
        {
            string candidate = json.Substring(braceStart, braceEnd - braceStart + 1);

            try
            {
                var parsed = JsonUtility.FromJson<ReplyJson>(candidate);

                if (parsed != null && !string.IsNullOrEmpty(parsed.say))
                {
                    return new AssistantReply
                    {
                        say = parsed.say.Trim(),
                        action = NormalizeAction(parsed.action)
                    };
                }
            }
            catch
            {
                // 落到下面的降级分支
            }
        }

        return new AssistantReply
        {
            say = json.Trim(),
            action = "idle"
        };
    }

    private static string StripCodeFence(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = text.Trim();

        if (!text.StartsWith("```"))
            return text;

        int firstNewline = text.IndexOf('\n');

        if (firstNewline < 0)
            return text;

        text = text.Substring(firstNewline + 1);
        int fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);

        if (fenceEnd >= 0)
            text = text.Substring(0, fenceEnd);

        return text.Trim();
    }

    private static string NormalizeAction(string action)
    {
        if (string.IsNullOrEmpty(action))
            return "idle";

        action = action.Trim().ToLowerInvariant();

        foreach (string allowed in ActionWhitelist)
        {
            if (action == allowed)
                return action;
        }

        return "idle";
    }

    private static bool ContainsBlocked(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        string lower = text.ToLowerInvariant();

        foreach (string word in BlockedWords)
        {
            if (lower.Contains(word))
                return true;
        }

        return false;
    }
}
