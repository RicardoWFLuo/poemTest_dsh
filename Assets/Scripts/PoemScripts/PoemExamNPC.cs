using UnityEngine;
using NPinyin;

/// <summary>
/// 诗词背诵考官：玩家对着 NPC 说出诗的下一句，用拼音比对判定是否通过。
/// </summary>
public class PoemExamNPC : MonoBehaviour
{
    [Header("诗词数据")]
    public PoemSO examPoem;

    [Header("UI引用")]
    public GameObject examCanvas;
    public AudioCommunicationUI audioUI;

    [Header("奖励设置")]
    public ItemSO rewardItem;
    public int rewardQuantity = 1;

    [Header("设置")]
    [Range(0.1f, 1f)]
    [Tooltip("拼音相似度阈值。完全包含目标拼音，或相似度达到该比例即算通过。")]
    public float passThreshold = 0.7f;

    private bool playerInRange = false;
    private bool hasPassed = false;
    private string firstLine;
    private string secondLine;

    private static System.Collections.Generic.Dictionary<PoemSO, bool> passedExams = new();


    void Start()
    {
        if (examCanvas != null)
            examCanvas.SetActive(false);

        ParsePoemLines();

        if (examPoem != null && passedExams.ContainsKey(examPoem))
        {
            hasPassed = passedExams[examPoem];
        }
    }

    void ParsePoemLines()
    {
        firstLine = null;
        secondLine = null;

        if (examPoem == null || string.IsNullOrEmpty(examPoem.poemContent))
            return;

        string[] lines = examPoem.poemContent.Replace("\r\n", "\n").Split(new char[] { '\n' },
            System.StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length >= 1)
            firstLine = lines[0].Trim();

        if (lines.Length >= 2)
            secondLine = lines[1].Trim();
    }

    void Update()
    {
        if (!playerInRange)
            return;

        if (MobileInputManager.Instance == null || MobileInputManager.Instance.GetInteractDown() == false)
            return;

        bool isCanvasActive = examCanvas != null && examCanvas.activeSelf;

        if (isCanvasActive)
        {
            CloseExam();
        }
        else
        {
            OpenExam();
        }
    }

    void OpenExam()
    {
        if (examCanvas != null)
            examCanvas.SetActive(true);

        if (audioUI != null)
            audioUI.SetCurrentExamNPC(this);
        else
            Debug.LogWarning("[PoemExamNPC] 没有绑定 AudioCommunicationUI，考试界面无法交互。");

        ShowExamContent();
    }

    public void ShowExamContent()
    {
        if (audioUI == null || audioUI.receiveText == null)
            return;

        if (hasPassed)
        {
            audioUI.receiveText.text = "你已完成此诗的考试！";
            return;
        }

        if (string.IsNullOrEmpty(secondLine))
        {
            audioUI.receiveText.text = "这首诗还没有配置完整的诗句。";
            return;
        }

        audioUI.receiveText.text = "上句: " + firstLine + "\n请说出下一句";
    }

    void CloseExam()
    {
        if (examCanvas != null)
            examCanvas.SetActive(false);

        if (audioUI != null)
            audioUI.ClearCurrentExamNPC(this);
    }

    public void OnVoiceRecognized(string recognizedText)
    {
        if (audioUI == null || audioUI.receiveText == null)
            return;

        bool isCanvasActive = examCanvas != null && examCanvas.activeSelf;
        if (!isCanvasActive || hasPassed)
            return;

        if (string.IsNullOrEmpty(recognizedText))
        {
            audioUI.receiveText.text = "上句: " + firstLine + "\n未识别到语音，再试试";
            return;
        }

        bool passed = CompareByPinyin(secondLine, recognizedText);

        if (passed)
        {
            hasPassed = true;

            if (examPoem != null)
                passedExams[examPoem] = true;

            audioUI.receiveText.text = "上句: " + firstLine + "\n你说: " + recognizedText + "\n\n正确！通过了！";
            GiveReward();
        }
        else
        {
            audioUI.receiveText.text = "上句: " + firstLine + "\n你说: " + recognizedText + "\n\n不对，再试试";
        }
    }

    /// <summary>
    /// 拼音比对。passThreshold 现在真正参与判定：
    /// 识别结果包含目标拼音（全对）或拼音相似度达到阈值都算通过，
    /// 这样个别字被识别错时不会直接判负。
    /// </summary>
    bool CompareByPinyin(string target, string recognized)
    {
        string cleanTarget = CleanChinese(target);

        if (string.IsNullOrEmpty(cleanTarget))
            return false;

        Pinyin pinyin = new Pinyin();
        pinyin.Option = new PinyinOption()
        {
            Style = PinyinStyle.Normal,
            EnableInterval = false
        };

        string targetPinyin = pinyin.ConvertToPinyin(cleanTarget);
        string cleanRecognized = CleanChinese(recognized);

        if (string.IsNullOrEmpty(cleanRecognized))
            return false;

        string recognizedPinyin = pinyin.ConvertToPinyin(cleanRecognized);

        if (string.IsNullOrEmpty(targetPinyin) || string.IsNullOrEmpty(recognizedPinyin))
            return false;

        if (recognizedPinyin.Contains(targetPinyin))
            return true;

        float similarity = Similarity(targetPinyin, recognizedPinyin);
        Debug.Log($"[PoemExam] 目标拼音={targetPinyin} 识别拼音={recognizedPinyin} 相似度={similarity:F2} 阈值={passThreshold:F2}");

        return similarity >= passThreshold;
    }

    /// <summary>归一化编辑距离相似度（1 = 完全相同，0 = 完全不同）。</summary>
    private static float Similarity(string a, string b)
    {
        int distance = LevenshteinDistance(a, b);
        int maxLength = Mathf.Max(a.Length, b.Length);

        if (maxLength == 0)
            return 1f;

        return 1f - (float)distance / maxLength;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Mathf.Min(
                    Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }

    string CleanChinese(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            if (c >= 0x4e00 && c <= 0x9fff)
                sb.Append(c);
        }
        return sb.ToString();
    }

    void GiveReward()
    {
        if (rewardItem == null || rewardQuantity <= 0)
            return;

        if (InventoryManager.Instance == null)
            return;

        InventoryManager.Instance.AddItem(rewardItem, rewardQuantity);
        Debug.Log($"获得奖励: {rewardItem.itemName} x{rewardQuantity}");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            if (examCanvas != null && examCanvas.activeSelf)
                CloseExam();
        }
    }

    void OnDisable()
    {
        playerInRange = false;
    }
}
