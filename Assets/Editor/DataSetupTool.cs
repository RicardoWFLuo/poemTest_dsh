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

    /// <summary>找一张现成的精灵图给 AI 助手当形象（随便哪张都行）。</summary>
    private static Sprite FindAssistantPortrait()
    {
        string[] preferredNames = { "Pawn_Red", "Pawn_Purple", "RedPoet", "Poet" };

        foreach (string wanted in preferredNames)
        {
            foreach (string guid in AssetDatabase.FindAssets(wanted + " t:Sprite", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(path))
                    continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (sprite != null)
                    return sprite;
            }
        }

        // 退一步：项目里任意一张精灵图
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite != null)
                return sprite;
        }

        return null;
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
