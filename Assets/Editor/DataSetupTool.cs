using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 阶段 0：数据准备工具（Editor 专用）。
///
/// 用法：Unity 菜单栏 → Tools → 诗词数据 → 【一键初始化（填 ID + 建索引）】
///
/// 它做三件事：
///   1. 给所有 PoemSO / ItemSO / ActorSO / DialogueSO / QuestSO / SkillSO / LocationSO
///      按「资源文件名」自动填充稳定 ID（已填过的不覆盖）；
///   2. 自动处理重名（追加 _2、_3 后缀）并报告；
///   3. 生成 / 刷新 Assets/Resources/GameDatabase.asset，作为运行时 ID → 资源 的反查清单。
///
/// 这个工具只改 ScriptableObject 资源，不碰任何场景、预制体或代码。
/// </summary>
public static class DataSetupTool
{
    private const string DatabaseFolder = "Assets/Resources";
    private const string DatabasePath = DatabaseFolder + "/GameDatabase.asset";
    private const string UISettingsPath = DatabaseFolder + "/UIStyleSettings.asset";
    private const string IdField = "id";

    // ---------------------------------------------------------------- 菜单入口

    [MenuItem("Tools/诗词数据/一键初始化（填 ID + 建索引）", false, 0)]
    public static void SetupAll()
    {
        int filled = FillAllIds();
        BuildDatabase();
        CreateUIStyleSettings();

        Debug.Log($"[诗词数据] 初始化完成：本次新填 {filled} 个 ID。若有重复或缺失，请查看上方警告。");
    }

    /// <summary>
    /// 创建 UI 样式资源，并自动挑选项目里的中文字体。
    /// 代码生成的界面必须靠它拿到带中文字形的 TMP 字体，否则中文会显示成方块。
    /// </summary>
    [MenuItem("Tools/诗词数据/0. 创建 UI 设置资源（中文字体）", false, 5)]
    public static void CreateUIStyleSettings()
    {
        var existing = AssetDatabase.LoadAssetAtPath<UIStyleSettings>(UISettingsPath);

        if (existing != null)
        {
            bool changed = false;

            if (existing.fontAsset == null)
            {
                existing.fontAsset = FindChineseFont();
                changed = true;
            }

            if (existing.assistantPortrait == null)
            {
                existing.assistantPortrait = FindAssistantPortrait();
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = existing;
            Debug.Log($"[诗词数据] UI 设置已存在：{UISettingsPath}" +
                      (existing.fontAsset != null ? $"（字体：{existing.fontAsset.name}）" : "，但未找到中文字体，请手动指定。"));
            return;
        }

        if (!AssetDatabase.IsValidFolder(DatabaseFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var settings = ScriptableObject.CreateInstance<UIStyleSettings>();
        settings.fontAsset = FindChineseFont();
        settings.assistantPortrait = FindAssistantPortrait();

        AssetDatabase.CreateAsset(settings, UISettingsPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = settings;

        if (settings.fontAsset != null)
            Debug.Log($"[诗词数据] 已创建 UI 设置：{UISettingsPath}（自动选中字体：{settings.fontAsset.name}）");
        else
            Debug.LogWarning($"[诗词数据] 已创建 UI 设置：{UISettingsPath}，但项目里没找到 TMP 字体资源。" +
                             "请把 Font Asset 手动设为 zpix SDF 或 unifont-17.0，否则界面中文会显示成方块。");
    }

    /// <summary>在项目里找最像「中文字体」的 TMP 字体资源。</summary>
    private static TMP_FontAsset FindChineseFont()
    {
        var candidates = new List<TMP_FontAsset>();

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null)
                candidates.Add(font);
        }

        // 先找名字里能看出是中文/像素中文字体的
        foreach (var font in candidates)
        {
            string name = font.name.ToLowerInvariant();

            if (name.Contains("zpix") || name.Contains("unifont") ||
                name.Contains("chinese") || name.Contains("hans") || name.Contains("sc"))
            {
                return font;
            }
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }

    /// <summary>
    /// 给 AI 助手挑一张形象图。
    ///
    /// 注意：项目里的 NPC 图多是「多帧精灵图（Sprite Sheet）」，
    /// 它们的主资源是 Texture2D，Sprite 是子资源 ——
    /// 用 LoadAssetAtPath&lt;Sprite&gt; 直接取会返回 null。
    /// 所以这里既要搜 t:Sprite，也要搜 t:Texture2D 再从里面取子精灵。
    /// </summary>
    private static Sprite FindAssistantPortrait()
    {
        string[] preferredNames = { "Warrior_Red", "Pawn_Red", "Pawn_Purple", "RedPoet", "Poet" };

        // 1) 先按偏好名字找（贴图也要搜，因为精灵图的主资源是 Texture2D）
        foreach (string wanted in preferredNames)
        {
            foreach (string path in FindSpriteCandidatePaths())
            {
                if (path.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Sprite sprite = LoadAnySprite(path);

                if (sprite != null)
                    return sprite;
            }
        }

        // 2) 退一步：项目里任意一张能用的精灵
        foreach (string path in FindSpriteCandidatePaths())
        {
            Sprite sprite = LoadAnySprite(path);

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    /// <summary>列出所有可能包含 Sprite 的资源路径（含多帧精灵图的贴图）。</summary>
    private static System.Collections.Generic.List<string> FindSpriteCandidatePaths()
    {
        var paths = new System.Collections.Generic.List<string>();
        var seen = new System.Collections.Generic.HashSet<string>();

        foreach (string filter in new[] { "t:Sprite", "t:Texture2D" })
        {
            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(path) || !seen.Add(path))
                    continue;

                paths.Add(path);
            }
        }

        paths.Sort(System.StringComparer.Ordinal);
        return paths;
    }

    /// <summary>
    /// 从一条资源路径里取出可用的 Sprite：
    /// 单图直接取；多帧精灵图则取第一个子精灵。
    /// </summary>
    private static Sprite LoadAnySprite(string path)
    {
        var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (direct != null)
            return direct;

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sprite)
                return sprite;
        }

        return null;
    }

    /// <summary>
    /// 单独给 AI 助手挑一张形象图（不用重建整个 UI 设置资源）。
    /// 自动挑失败时，也可以自己在 Inspector 里把任意 Sprite 拖到 Assistant Portrait。
    /// </summary>
    [MenuItem("Tools/诗词数据/6. 给 AI 助手挑一张形象图", false, 23)]
    public static void PickAssistantPortrait()
    {
        var settings = AssetDatabase.LoadAssetAtPath<UIStyleSettings>(UISettingsPath);

        if (settings == null)
        {
            Debug.LogWarning($"[诗词数据] 还没有 {UISettingsPath}。\n" +
                             "请先执行「0. 创建 UI 设置资源（中文字体）」。");
            return;
        }

        Sprite sprite = FindAssistantPortrait();

        if (sprite == null)
        {
            Debug.LogWarning("[诗词数据] 没在项目里找到可用的 Sprite。\n" +
                             "请手动把任意一张图拖到 UIStyleSettings 的 Assistant Portrait 字段。");
            return;
        }

        settings.assistantPortrait = sprite;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Selection.activeObject = settings;

        Debug.Log($"[诗词数据] 已给 AI 助手设置形象：{sprite.name}" +
                  $"（来自 {AssetDatabase.GetAssetPath(sprite)}）");
    }

    /// <summary>
    /// 给 AI 助手载入序列帧，让画像真正动起来。
    ///
    /// Warrior_Red.png 是 6 列 × 8 行、每帧 192×192 的精灵表：
    ///   · 每行左侧 3 帧 = 站立/行走循环
    ///   · 每行右侧 3 帧 = 挥剑攻击（带刀光）
    ///   · 不同行是不同朝向
    /// 这里取第 1 行的前 3 帧作待机循环；朝向由 faceLeft 镜像解决。
    /// </summary>
    [MenuItem("Tools/诗词数据/7. 给 AI 助手载入序列帧（让它动起来）", false, 24)]
    public static void LoadAssistantFrames()
    {
        var settings = AssetDatabase.LoadAssetAtPath<UIStyleSettings>(UISettingsPath);

        if (settings == null)
        {
            Debug.LogWarning($"[诗词数据] 还没有 {UISettingsPath}，请先执行「0. 创建 UI 设置资源」。");
            return;
        }

        if (settings.assistantPortrait == null)
        {
            Debug.LogWarning("[诗词数据] 还没设置助手形象，请先执行「6. 给 AI 助手挑一张形象图」。");
            return;
        }

        string path = AssetDatabase.GetAssetPath(settings.assistantPortrait);
        var all = new System.Collections.Generic.List<Sprite>();

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sprite)
                all.Add(sprite);
        }

        if (all.Count <= 1)
        {
            Debug.LogWarning($"[诗词数据] {path} 只有 1 张精灵（不是精灵表），做不了序列帧动画。");
            return;
        }

        // 按名字末尾编号排序，保证播放顺序正确
        all.Sort((a, b) => ExtractTrailingNumber(a.name).CompareTo(ExtractTrailingNumber(b.name)));

        int count = Mathf.Min(3, all.Count);
        var picked = new Sprite[count];

        for (int i = 0; i < count; i++)
            picked[i] = all[i];

        settings.assistantFrames = picked;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Selection.activeObject = settings;

        string names = string.Join("、", System.Array.ConvertAll(picked, s => s.name));
        Debug.Log($"[诗词数据] 已载入助手序列帧 {count} 帧：{names}（来自 {path}）\n" +
                  "若想换一组动作，直接在 UIStyleSettings 的 Assistant Frames 里替换成别的帧即可。");
    }

    /// <summary>取名字末尾的编号，用于给精灵表帧排序（如 Warrior_Red_12 → 12）。</summary>
    private static int ExtractTrailingNumber(string name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;

        int end = name.Length - 1;

        while (end >= 0 && !char.IsDigit(name[end]))
            end--;

        if (end < 0)
            return 0;

        int start = end;

        while (start >= 0 && char.IsDigit(name[start]))
            start--;

        int value;

        return int.TryParse(name.Substring(start + 1, end - start), out value) ? value : 0;
    }

    /// <summary>
    /// 把菜单界面（标题 / 主菜单 / 存档列表 / 菜单按钮）生成成一个预制体，
    /// 之后你就能在 Unity 里直接可视化编辑它。
    ///
    /// 生成后运行时会优先使用这个预制体（Resources/GameMenuCanvas），
    /// 而构建代码只会「复用」其中的节点、不会覆盖你的排版。
    /// </summary>
    [MenuItem("Tools/诗词数据/8. 生成菜单界面预制体（可在 Unity 中编辑）", false, 25)]
    public static void CreateMenuPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        const string path = "Assets/Resources/GameMenuCanvas.prefab";

        bool overwrite = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;

        if (overwrite)
        {
            bool ok = EditorUtility.DisplayDialog(
                "重新生成菜单预制体",
                "已存在 " + path + "\n\n" +
                "继续会覆盖它，你在 Unity 里对该预制体做的修改会丢失。\n" +
                "确定要继续吗？",
                "覆盖", "取消");

            if (!ok)
                return;
        }

        GameObject built = GameMenuUI.BuildForPrefab();

        if (built == null)
        {
            Debug.LogError("[菜单] 生成失败：构建代码返回了空对象。");
            return;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(built, path);
        Object.DestroyImmediate(built);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log($"[菜单] 已生成菜单界面预制体：{path}\n" +
                      "现在可以直接在 Unity 里编辑它的布局、文字与图片；\n" +
                      "运行时会自动使用这个预制体（找不到才退回代码生成）。");
        }
        else
        {
            Debug.LogError($"[菜单] 保存预制体失败：{path}");
        }
    }

    /// <summary>
    /// 把项目里现成的横幅/按钮贴图配到 UIStyleSettings 上，省得手动拖。
    /// 配完执行一次「8. 生成菜单界面预制体」，预制体里就会带上这些图。
    /// </summary>
    [MenuItem("Tools/诗词数据/9. 配置菜单界面贴图（横幅 + 按钮）", false, 26)]
    public static void ConfigureMenuSprites()
    {
        var settings = AssetDatabase.LoadAssetAtPath<UIStyleSettings>(UISettingsPath);

        if (settings == null)
        {
            Debug.LogWarning($"[诗词数据] 还没有 {UISettingsPath}。\n" +
                             "请先执行「0. 创建 UI 设置资源（中文字体）」。");
            return;
        }

        string bannerVertical = "Assets/Sprites/UI/Banners/Banner_Vertical.png";
        string bannerHorizontal = "Assets/Sprites/UI/Banners/Banner_Horizontal.png";
        string buttonBlue = "Assets/Sprites/UI/Buttons/Button_Blue_3Slides.png";
        string coverPath = "Assets/Resources/tt.png";

        Sprite titleBanner = AssetDatabase.LoadAssetAtPath<Sprite>(bannerVertical);
        Sprite slotBanner = AssetDatabase.LoadAssetAtPath<Sprite>(bannerHorizontal);
        Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(buttonBlue);
        Sprite cover = AssetDatabase.LoadAssetAtPath<Sprite>(coverPath);

        // 封面图 2560 宽，默认最大尺寸 2048 会被压缩；这里保证不被缩
        if (cover != null && AssetImporter.GetAtPath(coverPath) is TextureImporter importer)
        {
            if (importer.maxTextureSize < 2560)
            {
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
        }

        if (cover != null)
            settings.menuCover = cover;

        if (titleBanner != null)
            settings.menuTitleBanner = titleBanner;

        if (slotBanner != null)
            settings.menuSlotBanner = slotBanner;

        // 游戏菜单界面也用横版横幅
        if (slotBanner != null)
            settings.menuScreenBanner = slotBanner;

        if (buttonSprite != null)
        {
            settings.menuButtonSprite = buttonSprite;

            // 另外两个界面默认沿用同一张，你也可以在 Inspector 里换成别的
            if (settings.menuButtonSpriteAlt == null)
                settings.menuButtonSpriteAlt = buttonSprite;
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);

        Debug.Log("[诗词数据] 菜单贴图已配置：\n" +
                  $"  标题横幅   {(titleBanner != null ? "Banner_Vertical" : "（未找到，保持纯色）")}\n" +
                  $"  存档横幅   {(slotBanner != null ? "Banner_Horizontal" : "（未找到，保持纯色）")}\n" +
                  $"  按钮底图   {(buttonSprite != null ? "Button_Blue_3Slides" : "（未找到，保持纯色）")}\n" +
                  "接着执行「8. 生成菜单界面预制体」让贴图进入预制体。");
    }

    [MenuItem("Tools/诗词数据/1. 填充资源 ID", false, 10)]
    public static void FillIdsMenu()
    {
        int filled = FillAllIds();
        Debug.Log($"[诗词数据] 填充资源 ID 完成：本次新填 {filled} 个。");
    }

    [MenuItem("Tools/诗词数据/2. 构建资源索引（GameDatabase）", false, 11)]
    public static void BuildDatabaseMenu()
    {
        BuildDatabase();
    }

    [MenuItem("Tools/诗词数据/3. 检查重复 / 缺失 ID", false, 12)]
    public static void CheckIdsMenu()
    {
        CheckDuplicates();
    }

    // ---------------------------------------------------------------- 填 ID

    /// <summary>给所有受支持的资源类型填充空 ID，返回本次填充数量。</summary>
    private static int FillAllIds()
    {
        int filled = 0;

        filled += FillIdsForType<PoemSO>(IdField);
        filled += FillIdsForType<ItemSO>(IdField);
        filled += FillIdsForType<ActorSO>(IdField);
        filled += FillIdsForType<DialogueSO>(IdField);
        filled += FillIdsForType<QuestSO>(IdField);
        filled += FillIdsForType<SkillSO>(IdField);

        // LocationSO 复用已有的 locationID 字段，不新增 id
        filled += FillIdsForType<LocationSO>("locationID");

        AssetDatabase.SaveAssets();

        DeduplicateAll();
        return filled;
    }

    /// <summary>对某一类型：把空的 id 字段按资源文件名填上。</summary>
    private static int FillIdsForType<T>(string fieldName) where T : ScriptableObject
    {
        int filled = 0;

        foreach (string path in FindAssetPaths<T>())
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                continue;

            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[诗词数据] 资源「{path}」（{typeof(T).Name}）上找不到字段「{fieldName}」。");
                continue;
            }

            if (!string.IsNullOrEmpty(property.stringValue))
                continue; // 已有 ID，绝不覆盖

            property.stringValue = MakeIdFromPath(path);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            filled++;
        }

        return filled;
    }

    /// <summary>把路径转成 ID：取文件名，去掉非法字符。</summary>
    private static string MakeIdFromPath(string assetPath)
    {
        string name = Path.GetFileNameWithoutExtension(assetPath);

        foreach (char illegal in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(illegal, '_');
        }

        return name.Replace(' ', '_').Trim();
    }

    // ---------------------------------------------------------------- 去重

    /// <summary>扫描所有类型，把重复 ID 追加 _2、_3 后缀。</summary>
    private static void DeduplicateAll()
    {
        int fixedCount = 0;

        fixedCount += DeduplicateType<PoemSO>(IdField);
        fixedCount += DeduplicateType<ItemSO>(IdField);
        fixedCount += DeduplicateType<ActorSO>(IdField);
        fixedCount += DeduplicateType<DialogueSO>(IdField);
        fixedCount += DeduplicateType<QuestSO>(IdField);
        fixedCount += DeduplicateType<SkillSO>(IdField);
        fixedCount += DeduplicateType<LocationSO>("locationID");

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.LogWarning($"[诗词数据] 检测到重复 ID，已自动重命名 {fixedCount} 个。请检查这些资源是否符合预期。");
        }
    }

    private static int DeduplicateType<T>(string fieldName) where T : ScriptableObject
    {
        var used = new HashSet<string>();
        int fixedCount = 0;

        foreach (string path in FindAssetPaths<T>())
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                continue;

            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
                continue;

            string id = property.stringValue;
            if (string.IsNullOrEmpty(id))
                continue;

            if (used.Add(id))
                continue;

            // 该 ID 已被占用 → 追加后缀直到唯一
            string candidate = id + "_2";
            int suffix = 2;
            while (!used.Add(candidate))
            {
                suffix++;
                candidate = id + "_" + suffix;
            }

            property.stringValue = candidate;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            Debug.LogWarning($"[诗词数据] 「{path}」的 ID「{id}」重复，已改为「{candidate}」。");
            fixedCount++;
        }

        return fixedCount;
    }

    // ---------------------------------------------------------------- 检查

    [MenuItem("Tools/诗词数据/导出 ID 一览表到 Console", false, 20)]
    public static void CheckDuplicates()
    {
        int total = 0, missing = 0;
        var duplicates = new List<string>();

        total += ReportType<PoemSO>(IdField, duplicates, ref missing);
        total += ReportType<ItemSO>(IdField, duplicates, ref missing);
        total += ReportType<ActorSO>(IdField, duplicates, ref missing);
        total += ReportType<DialogueSO>(IdField, duplicates, ref missing);
        total += ReportType<QuestSO>(IdField, duplicates, ref missing);
        total += ReportType<SkillSO>(IdField, duplicates, ref missing);
        total += ReportType<LocationSO>("locationID", duplicates, ref missing);

        if (duplicates.Count == 0 && missing == 0)
        {
            Debug.Log($"[诗词数据] 检查通过：共 {total} 个资源，全部有唯一 ID。");
        }
        else
        {
            if (missing > 0)
                Debug.LogError($"[诗词数据] 有 {missing} 个资源没有 ID，请重新执行「填充资源 ID」。");

            foreach (string message in duplicates)
                Debug.LogError("[诗词数据] " + message);
        }
    }

    private static int ReportType<T>(string fieldName, List<string> duplicates, ref int missing)
        where T : ScriptableObject
    {
        var seen = new Dictionary<string, string>();
        int count = 0;

        foreach (string path in FindAssetPaths<T>())
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                continue;

            count++;

            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(fieldName);
            string id = property != null ? property.stringValue : null;

            if (string.IsNullOrEmpty(id))
            {
                missing++;
                Debug.LogWarning($"[诗词数据] 缺少 ID：{path}");
                continue;
            }

            if (seen.TryGetValue(id, out string firstPath))
            {
                duplicates.Add($"重复 ID「{id}」：{firstPath} 与 {path}");
            }
            else
            {
                seen[id] = path;
            }
        }

        return count;
    }

    // ---------------------------------------------------------------- 建索引

    private static void BuildDatabase()
    {
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);

        if (database == null)
        {
            if (!AssetDatabase.IsValidFolder(DatabaseFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            database = ScriptableObject.CreateInstance<GameDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            Debug.Log($"[诗词数据] 已创建索引资源：{DatabasePath}");
        }

        database.poems = Collect<PoemSO>();
        database.items = Collect<ItemSO>();
        database.actors = Collect<ActorSO>();
        database.dialogues = Collect<DialogueSO>();
        database.quests = Collect<QuestSO>();
        database.locations = Collect<LocationSO>();
        database.skills = Collect<SkillSO>();

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[诗词数据] 索引已刷新：" +
            $"诗词 {database.poems.Count}、物品 {database.items.Count}、角色 {database.actors.Count}、" +
            $"对话 {database.dialogues.Count}、任务 {database.quests.Count}、" +
            $"地点 {database.locations.Count}、技能 {database.skills.Count}。");
    }

    // ---------------------------------------------------------------- 工具方法

    private static List<string> FindAssetPaths<T>() where T : ScriptableObject
    {
        var paths = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);
        }

        paths.Sort(System.StringComparer.Ordinal);
        return paths;
    }

    private static List<T> Collect<T>() where T : ScriptableObject
    {
        var result = new List<T>();

        foreach (string path in FindAssetPaths<T>())
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                result.Add(asset);
        }

        return result;
    }
}
