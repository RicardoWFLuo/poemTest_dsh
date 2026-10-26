using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 存档系统总控。
///
/// 特点：
///  · 不需要往任何场景里挂脚本 —— 通过 RuntimeInitializeOnLoadMethod 自动创建常驻对象；
///  · 不改各游戏系统的内部逻辑 —— 只读取它们的公开字段，需要写入的地方调用它们新增的恢复方法；
///  · 三个存档位，JSON 明文，位置：Application.persistentDataPath/saves/slotN.json
///  · 带格式版本号，以后加字段可以迁移。
///
/// 调试：进游戏后按 F5 存档、F9 读档（可在 Inspector 关掉）。
/// </summary>
[DisallowMultipleComponent]
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    /// <summary>可用存档位数量。</summary>
    public const int SlotCount = 3;

    [Header("存档设置")]
    [Tooltip("默认使用的存档位，1 ~ 3")]
    [Range(1, SlotCount)]
    public int currentSlot = 1;

    [Tooltip("切换场景时自动存档（启动后的第一次场景加载不会触发）。\n" +
             "验证阶段建议先关掉：自动存档会掩盖「读档是否真的生效」，等验证完再打开。")]
    public bool autoSaveOnSceneChange = false;

    [Tooltip("退出游戏 / 切到后台时自动存档。\n" +
             "验证阶段建议先关掉，用 F5 手动存档更方便定位问题。")]
    public bool autoSaveOnQuit = false;

    [Header("调试快捷键（编辑器 / PC）")]
    public bool enableDebugHotkeys = true;
    public KeyCode saveKey = KeyCode.F5;
    public KeyCode loadKey = KeyCode.F9;

    private bool isRestoring;
    private int sceneLoadCount;

    private ExpManager cachedExp;
    private SkillTreeManager cachedSkillTree;

    /// <summary>是否正在读档（读档期间应跳过自动存档）。</summary>
    public bool IsRestoring => isRestoring;

    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");
    private static string SlotPath(int slot) => Path.Combine(SaveDirectory, "slot" + slot + ".json");

    // ---------------------------------------------------------------- 生命周期

    /// <summary>
    /// 自动创建常驻实例。这样就不需要手动把 SaveManager 拖到场景里，
    /// 也不会因为漏挂而出现「存档按钮点了没反应」。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var host = new GameObject("[SaveManager]");
        DontDestroyOnLoad(host);
        host.AddComponent<SaveManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        ApplySettingsOverrides();
    }

    /// <summary>把 Resources/UIStyleSettings 里的行为开关套用到本组件（不存在则保留 Inspector 默认值）。</summary>
    private void ApplySettingsOverrides()
    {
        UIStyleSettings settings = UIStyle.Settings;
        if (settings == null)
            return;

        autoSaveOnSceneChange = settings.autoSaveOnSceneChange;
        autoSaveOnQuit = settings.autoSaveOnQuit;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneLoadCount++;

        // 每次进入场景都重新套用「已消耗对话」，否则场景重载会把 NPC 的对话列表还原
        RemoveConsumedDialoguesInScene();

        // 启动后的第一次加载不自动存档，避免一进游戏就把已有存档覆盖掉
        if (!isRestoring && autoSaveOnSceneChange && sceneLoadCount > 1)
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        if (autoSaveOnQuit && !isRestoring)
            Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && autoSaveOnQuit && !isRestoring)
            Save();
    }

    private void Update()
    {
        if (!enableDebugHotkeys)
            return;

        if (Input.GetKeyDown(saveKey))
        {
            Save();
            Debug.Log($"[存档] 调试快存 → 存档位 {currentSlot}");
        }

        if (Input.GetKeyDown(loadKey))
        {
            Load();
            Debug.Log($"[存档] 调试快读 ← 存档位 {currentSlot}");
        }
    }

    // ---------------------------------------------------------------- 对外接口

    /// <summary>存档。slot 传 0 或省略表示使用 currentSlot。</summary>
    public bool Save(int slot = 0)
    {
        if (slot <= 0)
            slot = currentSlot;

        // 记住这次用的存档位，F5/F9 这些快捷键就会跟着走
        currentSlot = slot;

        try
        {
            SaveData data = Capture();

            Directory.CreateDirectory(SaveDirectory);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SlotPath(slot), json, System.Text.Encoding.UTF8);

            Debug.Log($"[存档] 已保存到存档位 {slot}（{data.sceneName}，等级 {data.level}，金币 {data.gold}）");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[存档] 保存失败：{e.Message}");
            return false;
        }
    }

    /// <summary>读档。slot 传 0 或省略表示使用 currentSlot。</summary>
    public bool Load(int slot = 0)
    {
        if (slot <= 0)
            slot = currentSlot;

        string path = SlotPath(slot);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[存档] 存档位 {slot} 没有存档文件：{path}");
            return false;
        }

        SaveData data;

        try
        {
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            data = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[存档] 读取失败（文件可能损坏）：{e.Message}");
            return false;
        }

        if (data == null)
        {
            Debug.LogError("[存档] 存档内容为空或格式不正确。");
            return false;
        }

        if (data.version > SaveData.CurrentVersion)
        {
            Debug.LogError($"[存档] 存档版本 {data.version} 高于当前支持的 {SaveData.CurrentVersion}，" +
                           "这个文件是更新版本的游戏生成的，无法读取。");
            return false;
        }

        currentSlot = slot;
        StartCoroutine(RestoreRoutine(data));
        return true;
    }

    public bool HasSave(int slot)
    {
        return slot >= 1 && slot <= SlotCount && File.Exists(SlotPath(slot));
    }

    public bool DeleteSave(int slot)
    {
        string path = SlotPath(slot);

        if (!File.Exists(path))
            return false;

        try
        {
            File.Delete(path);
            Debug.Log($"[存档] 已删除存档位 {slot}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[存档] 删除失败：{e.Message}");
            return false;
        }
    }

    /// <summary>取某个存档位的概要信息（给未来的存档界面用）。</summary>
    public SaveSlotInfo GetSlotInfo(int slot)
    {
        var info = new SaveSlotInfo { slot = slot };

        string path = SlotPath(slot);
        if (!File.Exists(path))
            return info;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path, System.Text.Encoding.UTF8));
            if (data == null)
                return info;

            info.exists = true;
            info.savedAtUtc = data.savedAtUtc;
            info.sceneName = data.sceneName;
            info.level = data.level;
            info.gold = data.gold;
        }
        catch
        {
            // 读不出来就当不存在，避免存档界面整个崩掉
        }

        return info;
    }

    /// <summary>开启新游戏：清空运行时世界状态，但不删任何存档文件。</summary>
    public void ResetRuntimeState()
    {
        WorldState.Clear();
        RemoveConsumedDialoguesInScene();
        Debug.Log("[存档] 已重置运行时世界状态（新游戏）");
    }

    /// <summary>存档目录的完整路径，方便用户在文件管理器里找到。</summary>
    public string GetSaveDirectory() => SaveDirectory;

    // ---------------------------------------------------------------- 采集

    private SaveData Capture()
    {
        var data = new SaveData();
        data.savedAtUtc = DateTime.UtcNow.ToString("o");
        data.sceneName = SceneManager.GetActiveScene().name;

        // 玩家位置
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 position = player.transform.position;
            data.playerX = position.x;
            data.playerY = position.y;
            data.playerZ = position.z;
        }

        CaptureInventory(data);
        CaptureStats(data);
        CaptureProgress(data);
        CaptureSkills(data);
        CaptureQuests(data);
        CaptureWorldHistory(data);

        data.passedPoemIds.AddRange(PoemExamNPC.GetPassedPoemIds());
        data.consumedDialogueIds.AddRange(WorldState.ConsumedDialogueIds);

        return data;
    }

    private void CaptureInventory(SaveData data)
    {
        var inventory = InventoryManager.Instance;
        if (inventory == null)
            return;

        data.gold = inventory.gold;

        if (inventory.itemSlots == null)
            return;

        for (int i = 0; i < inventory.itemSlots.Length; i++)
        {
            var slot = inventory.itemSlots[i];
            if (slot == null || slot.itemSO == null || slot.quantity <= 0)
                continue;

            string itemId = ScriptableObjectIndex.GetId(slot.itemSO);
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning($"[存档] 物品「{slot.itemSO.name}」没有 ID，本次未被保存。");
                continue;
            }

            data.items.Add(new ItemEntry
            {
                slotIndex = i,
                itemId = itemId,
                quantity = slot.quantity
            });
        }
    }

    private void CaptureStats(SaveData data)
    {
        var stats = StatsManager.Instance;
        if (stats == null)
            return;

        data.currentHealth = stats.currentHealth;
        data.maxHealth = stats.maxHealth;
        data.damage = stats.damage;
        data.speed = stats.speed;
    }

    private void CaptureProgress(SaveData data)
    {
        ExpManager exp = FindExpManager();
        if (exp == null)
            return;

        data.level = exp.level;
        data.currentExp = exp.currentExp;
        data.expToLevel = exp.expToLevel;
    }

    private void CaptureSkills(SaveData data)
    {
        SkillTreeManager tree = FindSkillTreeManager();
        if (tree == null)
            return;

        data.skillPoints = tree.availablePoints;

        if (tree.skillSlots == null)
            return;

        for (int i = 0; i < tree.skillSlots.Length; i++)
        {
            var slot = tree.skillSlots[i];
            if (slot == null)
                continue;

            data.skills.Add(new SkillEntry
            {
                slotIndex = i,
                currentLevel = slot.currentLevel,
                unlocked = slot.isUnclocked
            });
        }
    }

    private void CaptureQuests(SaveData data)
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.QuestManager == null)
            return;

        foreach (var quest in gameManager.QuestManager.GetActiveQuests())
        {
            string id = ScriptableObjectIndex.GetId(quest);
            if (!string.IsNullOrEmpty(id))
                data.acceptedQuestIds.Add(id);
        }

        foreach (var quest in gameManager.QuestManager.GetCompletedQuests())
        {
            string id = ScriptableObjectIndex.GetId(quest);
            if (!string.IsNullOrEmpty(id))
                data.completedQuestIds.Add(id);
        }
    }

    private void CaptureWorldHistory(SaveData data)
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        if (gameManager.DialogueHistoryTracker != null)
        {
            foreach (var actor in gameManager.DialogueHistoryTracker.GetSpokenActors())
            {
                string id = ScriptableObjectIndex.GetId(actor);
                if (!string.IsNullOrEmpty(id))
                    data.spokenActorIds.Add(id);
            }
        }

        if (gameManager.LocationHistoryTracker != null)
        {
            foreach (var location in gameManager.LocationHistoryTracker.GetVisitedLocations())
            {
                string id = ScriptableObjectIndex.GetId(location);
                if (!string.IsNullOrEmpty(id))
                    data.visitedLocationIds.Add(id);
            }
        }
    }

    // ---------------------------------------------------------------- 恢复

    private IEnumerator RestoreRoutine(SaveData data)
    {
        isRestoring = true;

        // 如果存档来自另一个场景，先把场景切过去
        string currentScene = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(data.sceneName) && data.sceneName != currentScene)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(data.sceneName);

            if (operation == null)
            {
                Debug.LogError($"[存档] 无法加载场景「{data.sceneName}」，请确认它已加入 Build Settings。");
                isRestoring = false;
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            yield return null; // 等一帧，让场景里的对象 Awake/Start 完成
        }

        Apply(data);

        isRestoring = false;
        Debug.Log($"[存档] 已读取存档位 {currentSlot}（{data.sceneName}，等级 {data.level}，金币 {data.gold}）");
    }

    private void Apply(SaveData data)
    {
        // 1) 世界状态（先套用，后面 NPC 过滤要用到）
        WorldState.SetConsumed(data.consumedDialogueIds);

        ApplyInventory(data);
        ApplyStats(data);
        ApplyProgress(data);
        ApplySkills(data);
        ApplyQuests(data);
        ApplyWorldHistory(data);
        ApplyPassedPoems(data);
        ApplyPlayerPosition(data);

        // 2) 场景里的 NPC 按最新的「已消耗对话」重新过滤
        RemoveConsumedDialoguesInScene();
    }

    private void ApplyInventory(SaveData data)
    {
        var inventory = InventoryManager.Instance;
        if (inventory == null)
            return;

        inventory.gold = data.gold;
        inventory.UpdateGoldUI();

        if (inventory.itemSlots == null)
            return;

        // 先清空所有格子
        foreach (var slot in inventory.itemSlots)
        {
            if (slot == null)
                continue;

            slot.itemSO = null;
            slot.quantity = 0;
            slot.UpdateUI();
        }

        // 再按存档逐格还原（直接写格子，而不是走 AddItem，保持原有排布）
        foreach (var entry in data.items)
        {
            if (entry == null || entry.quantity <= 0)
                continue;

            if (entry.slotIndex < 0 || entry.slotIndex >= inventory.itemSlots.Length)
            {
                Debug.LogWarning($"[存档] 物品 {entry.itemId} 的格子下标 {entry.slotIndex} 越界，已跳过。");
                continue;
            }

            if (!ScriptableObjectIndex.TryGet(entry.itemId, out ItemSO item))
            {
                Debug.LogWarning($"[存档] 找不到物品「{entry.itemId}」，已跳过。");
                continue;
            }

            var slot = inventory.itemSlots[entry.slotIndex];
            if (slot == null)
                continue;

            slot.itemSO = item;
            slot.quantity = entry.quantity;
            slot.UpdateUI();
        }
    }

    private void ApplyStats(SaveData data)
    {
        var stats = StatsManager.Instance;
        if (stats == null)
            return;

        stats.maxHealth = Mathf.Max(1, data.maxHealth);
        stats.currentHealth = Mathf.Clamp(data.currentHealth, 0, stats.maxHealth);
        stats.damage = data.damage;
        stats.speed = data.speed;
        stats.RefreshHealthText();

        if (stats.statsUI != null)
            stats.statsUI.UpdateAllStats();
    }

    private void ApplyProgress(SaveData data)
    {
        ExpManager exp = FindExpManager();
        if (exp == null)
            return;

        exp.level = data.level;
        exp.currentExp = data.currentExp;
        exp.expToLevel = Mathf.Max(1, data.expToLevel);
        exp.UpdateUI();
    }

    private void ApplySkills(SaveData data)
    {
        SkillTreeManager tree = FindSkillTreeManager();
        if (tree == null)
            return;

        tree.availablePoints = data.skillPoints;

        if (tree.pointsText != null)
            tree.pointsText.text = "技能点:" + tree.availablePoints;

        if (tree.skillSlots == null)
            return;

        foreach (var entry in data.skills)
        {
            if (entry == null)
                continue;

            if (entry.slotIndex < 0 || entry.slotIndex >= tree.skillSlots.Length)
                continue;

            var slot = tree.skillSlots[entry.slotIndex];
            if (slot == null)
                continue;

            slot.RestoreState(entry.currentLevel, entry.unlocked);
        }
    }

    private void ApplyQuests(SaveData data)
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.QuestManager == null)
            return;

        var accepted = new List<QuestSO>();
        foreach (string id in data.acceptedQuestIds)
        {
            if (ScriptableObjectIndex.TryGet(id, out QuestSO quest))
                accepted.Add(quest);
            else
                Debug.LogWarning($"[存档] 找不到任务「{id}」。");
        }

        var completed = new List<QuestSO>();
        foreach (string id in data.completedQuestIds)
        {
            if (ScriptableObjectIndex.TryGet(id, out QuestSO quest))
                completed.Add(quest);
            else
                Debug.LogWarning($"[存档] 找不到任务「{id}」。");
        }

        gameManager.QuestManager.RestoreState(accepted, completed);
    }

    private void ApplyWorldHistory(SaveData data)
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        if (gameManager.DialogueHistoryTracker != null)
        {
            var actors = new List<ActorSO>();
            foreach (string id in data.spokenActorIds)
            {
                if (ScriptableObjectIndex.TryGet(id, out ActorSO actor))
                    actors.Add(actor);
            }

            gameManager.DialogueHistoryTracker.Restore(actors);
        }

        if (gameManager.LocationHistoryTracker != null)
        {
            var locations = new List<LocationSO>();
            foreach (string id in data.visitedLocationIds)
            {
                if (ScriptableObjectIndex.TryGet(id, out LocationSO location))
                    locations.Add(location);
            }

            gameManager.LocationHistoryTracker.Restore(locations);
        }
    }

    private void ApplyPassedPoems(SaveData data)
    {
        PoemExamNPC.RestorePassedPoems(data.passedPoemIds);

        // 考官实例在 Start 时已经读过一次通过状态，这里让它们重新同步
        foreach (var exam in FindObjectsOfType<PoemExamNPC>(true))
        {
            if (exam != null)
                exam.RefreshPassedState();
        }
    }

    private void ApplyPlayerPosition(SaveData data)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("[存档] 场景里找不到 Tag 为 Player 的对象，玩家位置未恢复。");
            return;
        }

        player.SetActive(true);
        player.transform.position = new Vector3(data.playerX, data.playerY, data.playerZ);

        var body = player.GetComponent<Rigidbody2D>();
        if (body != null)
            body.velocity = Vector2.zero;
    }

    // ---------------------------------------------------------------- 工具

    private void RemoveConsumedDialoguesInScene()
    {
        foreach (var npc in FindObjectsOfType<NPCTalk>(true))
        {
            if (npc != null)
                npc.RemoveConsumedDialogues();
        }

        foreach (var poet in FindObjectsOfType<PoemTalk>(true))
        {
            if (poet != null)
                poet.RemoveConsumedDialogues();
        }
    }

    private ExpManager FindExpManager()
    {
        if (cachedExp == null)
            cachedExp = FindObjectOfType<ExpManager>();

        return cachedExp;
    }

    private SkillTreeManager FindSkillTreeManager()
    {
        if (cachedSkillTree == null)
            cachedSkillTree = FindObjectOfType<SkillTreeManager>();

        return cachedSkillTree;
    }
}

/// <summary>存档位概要，供存档界面显示。</summary>
public struct SaveSlotInfo
{
    public int slot;
    public bool exists;
    public string savedAtUtc;
    public string sceneName;
    public int level;
    public int gold;
}
