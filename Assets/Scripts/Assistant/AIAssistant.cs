using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 右下角 AI 助手「小墨」。
///
/// 特点：
/// · 形象就是一个 Sprite（随便用哪张都行，没配也能用，会显示成纯色方块）；
/// · 动作全部用代码做（上下浮动 / 呼吸 / 招手 / 点头 / 摇头 / 思考），不需要 Animator；
/// · 文字泡带打字机效果；
/// · 有提问次数上限与冷却，避免把 API 额度打爆；
/// · 打开面板时暂停游戏，避免打字时角色乱跑。
///
/// 操作：点右下角形象打开/关闭；在输入框里打字回车发送。
/// </summary>
public class AIAssistant : MonoBehaviour
{
    public static AIAssistant Instance { get; private set; }

    [Header("额度（控制成本）")]
    [Tooltip("一局游戏最多能问几次。用完会提示。")]
    public int maxQuestionsPerSession = 10;

    [Tooltip("两次提问之间的最小间隔（秒）。")]
    public float questionCooldown = 3f;

    [Header("外观")]
    public Vector2 portraitSize = new Vector2(200f, 200f);
    public Vector2 margin = new Vector2(24f, 24f);
    public float panelWidth = 580f;
    public float panelHeight = 230f;

    [Header("动画")]
    public float bobAmplitude = 8f;
    public float bobSpeed = 1.6f;
    public float typingSpeed = 40f; // 每秒显示多少个字

    [Header("按键")]
    public KeyCode toggleKey = KeyCode.Tab;

    // ---- 运行时引用 ----
    private Canvas canvas;
    private RectTransform assistantRoot;
    private RectTransform portraitRect;
    private Image portraitImage;
    private GameObject panelRoot;
    private TMP_Text bubbleText;
    private TMP_Text counterText;
    private TMP_InputField inputField;
    private Button sendButton;

    private AssistantChatClient client;

    private bool panelOpen;
    private bool pausedByMe;
    private bool isBusy;
    private int usedQuestions;
    private float lastAskTime = -999f;

    // 动画状态
    private string currentAction = "idle";
    private float actionTimer;
    private Vector2 portraitBasePos;

    // 打字机
    private string fullText = string.Empty;
    private float shownChars;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        client = GetComponent<AssistantChatClient>();

        if (client == null)
            client = gameObject.AddComponent<AssistantChatClient>();

        BuildUI();
        SetPanelOpen(false);
        SetBubble("你好，我是小墨。想聊聊哪首诗？");
    }

    /// <summary>自动创建常驻实例，不需要手动挂到场景。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var host = new GameObject("[AIAssistant]");
        DontDestroyOnLoad(host);
        host.AddComponent<AIAssistant>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetPanelOpen(!panelOpen);

        UpdateTyping();
        UpdateAnimation();
    }

    // ---------------------------------------------------------------- UI 搭建

    private void BuildUI()
    {
        UIFactory.EnsureEventSystem();

        canvas = UIFactory.CreateCanvas("[AssistantCanvas]", 400);

        // 整层放进安全区，避免手机上被刘海挡住
        RectTransform safeRoot = UIFactory.CreateFullScreen("SafeRoot", canvas.transform);
        safeRoot.gameObject.AddComponent<SafeAreaFitter>();

        assistantRoot = UIFactory.NewRect("Assistant", safeRoot);
        assistantRoot.anchorMin = new Vector2(1f, 0f);
        assistantRoot.anchorMax = new Vector2(1f, 0f);
        assistantRoot.pivot = new Vector2(1f, 0f);
        assistantRoot.sizeDelta = new Vector2(panelWidth, portraitSize.y + panelHeight + 40f);
        assistantRoot.anchoredPosition = new Vector2(-margin.x, margin.y);

        BuildPortrait();
        BuildCounter();
        BuildPanel();
    }

    private void BuildPortrait()
    {
        portraitRect = UIFactory.NewRect("Portrait", assistantRoot);
        portraitRect.anchorMin = new Vector2(1f, 0f);
        portraitRect.anchorMax = new Vector2(1f, 0f);
        portraitRect.pivot = new Vector2(1f, 0f);
        portraitRect.sizeDelta = portraitSize;
        portraitRect.anchoredPosition = Vector2.zero;
        portraitBasePos = Vector2.zero;

        portraitImage = portraitRect.gameObject.AddComponent<Image>();
        portraitImage.raycastTarget = true;

        Sprite portrait = UIStyle.Settings != null ? UIStyle.Settings.assistantPortrait : null;

        if (portrait != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.color = Color.white;
            portraitImage.preserveAspect = true;
        }
        else
        {
            // 没配形象也不影响使用，退化成一块圆角色块
            portraitImage.sprite = UIFactory.WhiteSprite;
            portraitImage.color = new Color(0.85f, 0.72f, 0.42f, 0.92f);
        }

        var button = portraitRect.gameObject.AddComponent<Button>();
        button.targetGraphic = portraitImage;
        button.onClick.AddListener(() => SetPanelOpen(!panelOpen));
    }

    private void BuildCounter()
    {
        counterText = UIFactory.CreateText(
            assistantRoot, string.Empty, 22f, UIStyle.AccentColor,
            TextAlignmentOptions.Center,
            new Vector2(portraitSize.x, 30f),
            new Vector2(-portraitSize.x / 2f, portraitSize.y + 6f));

        RefreshCounter();
    }

    private void BuildPanel()
    {
        panelRoot = UIFactory.NewRect("Panel", assistantRoot).gameObject;

        var rect = (RectTransform)panelRoot.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(panelWidth, panelHeight);
        rect.anchoredPosition = new Vector2(0f, portraitSize.y + 42f);

        // 泡泡底板
        var bubbleBg = UIFactory.NewRect("BubbleBg", rect);
        bubbleBg.anchorMin = new Vector2(0f, 0f);
        bubbleBg.anchorMax = new Vector2(1f, 1f);
        bubbleBg.offsetMin = new Vector2(0f, 64f);
        bubbleBg.offsetMax = new Vector2(0f, 0f);

        var bubbleImage = bubbleBg.gameObject.AddComponent<Image>();
        bubbleImage.sprite = UIFactory.WhiteSprite;
        bubbleImage.color = new Color(0.08f, 0.07f, 0.11f, 0.94f);

        // 泡泡文字
        var textRect = UIFactory.NewRect("BubbleText", bubbleBg);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 12f);
        textRect.offsetMax = new Vector2(-16f, -12f);

        bubbleText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        bubbleText.font = UIStyle.Font;
        bubbleText.fontSize = 28f;
        bubbleText.color = UIStyle.TextColor;
        bubbleText.alignment = TextAlignmentOptions.TopLeft;
        bubbleText.enableWordWrapping = true;
        bubbleText.raycastTarget = false;

        BuildInputRow(rect);
    }

    private void BuildInputRow(RectTransform panel)
    {
        const float rowHeight = 56f;
        const float sendWidth = 92f;

        // 输入框
        RectTransform inputRect = UIFactory.NewRect("Input", panel);
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0f, 0f);
        inputRect.offsetMin = new Vector2(0f, 0f);
        inputRect.offsetMax = new Vector2(-(sendWidth + 8f), rowHeight);

        var inputBg = inputRect.gameObject.AddComponent<Image>();
        inputBg.sprite = UIFactory.WhiteSprite;
        inputBg.color = new Color(1f, 1f, 1f, 0.10f);

        // 文本区域（必须有一个 viewport，TMP_InputField 才知道裁剪范围）
        RectTransform viewport = UIFactory.NewRect("TextArea", inputRect);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(14f, 6f);
        viewport.offsetMax = new Vector2(-14f, -6f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform textRect = UIFactory.NewRect("Text", viewport);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var inputText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        inputText.font = UIStyle.Font;
        inputText.fontSize = 26f;
        inputText.color = UIStyle.TextColor;
        inputText.alignment = TextAlignmentOptions.Left;
        inputText.enableWordWrapping = false;
        inputText.raycastTarget = false;

        RectTransform placeholderRect = UIFactory.NewRect("Placeholder", viewport);
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;

        var placeholderText = placeholderRect.gameObject.AddComponent<TextMeshProUGUI>();
        placeholderText.font = UIStyle.Font;
        placeholderText.fontSize = 24f;
        placeholderText.color = new Color(UIStyle.TextColor.r, UIStyle.TextColor.g, UIStyle.TextColor.b, 0.45f);
        placeholderText.alignment = TextAlignmentOptions.Left;
        placeholderText.text = "问我关于这首诗的问题…";
        placeholderText.raycastTarget = false;

        inputField = inputRect.gameObject.AddComponent<TMP_InputField>();
        inputField.textViewport = viewport;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = 100;
        inputField.onSubmit.AddListener(OnSubmitText);

        // 发送按钮
        RectTransform sendRect = UIFactory.NewRect("Send", panel);
        sendRect.anchorMin = new Vector2(1f, 0f);
        sendRect.anchorMax = new Vector2(1f, 0f);
        sendRect.pivot = new Vector2(1f, 0f);
        sendRect.sizeDelta = new Vector2(sendWidth, rowHeight);
        sendRect.anchoredPosition = Vector2.zero;

        var sendBg = sendRect.gameObject.AddComponent<Image>();
        sendBg.sprite = UIFactory.WhiteSprite;
        sendBg.color = Color.white;

        sendButton = sendRect.gameObject.AddComponent<Button>();
        sendButton.targetGraphic = sendBg;

        var colors = sendButton.colors;
        colors.normalColor = UIStyle.ButtonColor;
        colors.highlightedColor = UIStyle.ButtonHighlightColor;
        colors.pressedColor = UIStyle.AccentColor;
        colors.colorMultiplier = 1f;
        sendButton.colors = colors;

        sendButton.onClick.AddListener(OnSendClicked);

        RectTransform sendLabelRect = UIFactory.CreateFullScreen("Label", sendRect);
        var sendLabel = sendLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
        sendLabel.font = UIStyle.Font;
        sendLabel.text = "发送";
        sendLabel.fontSize = 26f;
        sendLabel.color = UIStyle.TextColor;
        sendLabel.alignment = TextAlignmentOptions.Center;
        sendLabel.raycastTarget = false;
    }

    // ---------------------------------------------------------------- 交互

    private void OnSubmitText(string _)
    {
        OnSendClicked();
    }

    private void OnSendClicked()
    {
        if (inputField == null)
            return;

        string question = inputField.text;

        if (string.IsNullOrWhiteSpace(question))
            return;

        AskQuestion(question);
        inputField.text = string.Empty;

        // 保持输入焦点，方便连续提问
        if (inputField != null)
            StartCoroutine(RefocusNextFrame());
    }

    private IEnumerator RefocusNextFrame()
    {
        yield return null;

        if (panelOpen && inputField != null)
            inputField.ActivateInputField();
    }

    /// <summary>对外入口：也可以通过别处触发提问。</summary>
    public void AskQuestion(string question)
    {
        if (isBusy)
            return;

        if (usedQuestions >= maxQuestionsPerSession)
        {
            SetBubble($"今天已经问了 {maxQuestionsPerSession} 次啦，我们歇一歇再聊吧。", "shake");
            return;
        }

        float sinceLast = Time.unscaledTime - lastAskTime;

        if (sinceLast < questionCooldown)
        {
            SetBubble($"别急，{(questionCooldown - sinceLast):0.#} 秒后再问～", "shake");
            return;
        }

        StartCoroutine(AskRoutine(question));
    }

    private IEnumerator AskRoutine(string question)
    {
        isBusy = true;
        lastAskTime = Time.unscaledTime;
        usedQuestions++;
        RefreshCounter();

        SetBubble("…", "think");

        if (sendButton != null)
            sendButton.interactable = false;

        AssistantReply reply = null;

        yield return StartCoroutine(client.Ask(question, r => reply = r));

        if (sendButton != null)
            sendButton.interactable = true;

        isBusy = false;

        if (reply == null || !reply.Success)
        {
            SetBubble("我没听清，能再说一遍吗？", "think");
            yield break;
        }

        SetBubble(reply.say, reply.action);
    }

    private void SetPanelOpen(bool open)
    {
        panelOpen = open;

        if (panelRoot != null)
            panelRoot.SetActive(open);

        if (open)
        {
            if (!pausedByMe)
            {
                PauseController.RequestPause();
                pausedByMe = true;
            }

            if (inputField != null)
                inputField.ActivateInputField();
        }
        else
        {
            if (pausedByMe)
            {
                PauseController.ReleasePause();
                pausedByMe = false;
            }
        }
    }

    private void RefreshCounter()
    {
        if (counterText == null)
            return;

        int remain = Mathf.Max(0, maxQuestionsPerSession - usedQuestions);
        counterText.text = $"可问 {remain} 次";
    }

    // ---------------------------------------------------------------- 文字与动画

    private void SetBubble(string text, string action = "idle")
    {
        fullText = text ?? string.Empty;
        shownChars = 0f;

        if (bubbleText != null)
            bubbleText.text = string.Empty;

        PlayAction(action);
    }

    private void UpdateTyping()
    {
        if (bubbleText == null || string.IsNullOrEmpty(fullText))
            return;

        if (shownChars >= fullText.Length)
            return;

        shownChars += typingSpeed * Time.unscaledDeltaTime;

        int count = Mathf.Clamp(Mathf.FloorToInt(shownChars), 0, fullText.Length);
        bubbleText.text = fullText.Substring(0, count);
    }

    private void PlayAction(string action)
    {
        currentAction = string.IsNullOrEmpty(action) ? "idle" : action;

        actionTimer = currentAction switch
        {
            "wave" => 1.2f,
            "nod" => 0.6f,
            "shake" => 0.6f,
            _ => 0f
        };
    }

    private void UpdateAnimation()
    {
        if (portraitRect == null)
            return;

        float t = Time.unscaledTime;

        // 基础：上下浮动 + 呼吸
        float bob = Mathf.Sin(t * bobSpeed) * bobAmplitude;
        float breathe = 1f + Mathf.Sin(t * bobSpeed * 1.7f) * 0.015f;

        Vector2 offset = new Vector2(0f, bob);
        float scale = breathe;
        float angle = 0f;

        if (actionTimer > 0f)
        {
            actionTimer -= Time.unscaledDeltaTime;

            switch (currentAction)
            {
                case "wave":
                    angle = Mathf.Sin(t * 14f) * 8f;
                    break;

                case "nod":
                    offset.y += Mathf.Sin(t * 18f) * 10f;
                    break;

                case "shake":
                    offset.x += Mathf.Sin(t * 22f) * 14f;
                    break;

                case "think":
                    angle = Mathf.Sin(t * 2f) * 5f;
                    scale = 1f + Mathf.Sin(t * 3f) * 0.03f;
                    break;
            }

            if (actionTimer <= 0f)
                currentAction = "idle";
        }

        portraitRect.anchoredPosition = portraitBasePos + offset;
        portraitRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        portraitRect.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>新游戏时重置额度与对话历史。</summary>
    public void ResetSession()
    {
        usedQuestions = 0;
        lastAskTime = -999f;
        RefreshCounter();

        if (client != null)
            client.ClearHistory();

        SetBubble("你好，我是小墨。想聊聊哪首诗？");
    }
}
