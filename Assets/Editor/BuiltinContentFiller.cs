using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 阶段 3：内置内容回填工具（Editor 专用）。
///
/// 用法：Unity 菜单栏 → Tools → 诗词数据 → 4. 回填内置内容
///
/// 它会扫描 Assets/_PoemContentSource/ 目录，按「资源 ID」匹配文件：
///   · Poem1.txt  → 写入对应 PoemSO 的 builtinExplanation（内置释义）
///   · Poem1.png  → 写入对应 PoemSO 的 builtinIllustration（内置插画，自动设为 Sprite 类型）
///
/// 这个目录里的文件只作为「内容来源」，不会被丢进游戏包体
/// （Unity 只打包被引用的资源，而这些 txt/png 只是被工具读取）。
/// </summary>
public static class BuiltinContentFiller
{
    private const string ContentFolder = "Assets/_PoemContentSource";

    [MenuItem("Tools/诗词数据/4. 回填内置内容（_PoemContentSource）", false, 13)]
    public static void FillBuiltinContent()
    {
        if (!AssetDatabase.IsValidFolder(ContentFolder))
        {
            CreateContentFolder();
            Debug.LogWarning($"[内置内容] 已创建目录 {ContentFolder}，但里面还没有任何文件。\n" +
                             "请把生成好的 Poem1.txt / Poem1.png … Poem10.txt / Poem10.png 放进去，然后重新执行本菜单。");
            return;
        }

        List<PoemSO> poems = CollectPoems();
        if (poems.Count == 0)
        {
            Debug.LogError("[内置内容] 项目里没有找到任何 PoemSO。");
            return;
        }

        int textFilled = 0;
        int imageFilled = 0;
        var missing = new List<string>();

        foreach (var poem in poems)
        {
            if (poem == null)
                continue;

            string id = string.IsNullOrEmpty(poem.id) ? poem.name : poem.id;

            bool gotText = TryFillText(poem, id);
            bool gotImage = TryFillImage(poem, id);

            if (gotText)
                textFilled++;
            if (gotImage)
                imageFilled++;

            if (!gotText && !gotImage)
                missing.Add(id);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[内置内容] 回填完成：释义 {textFilled} 篇，插画 {imageFilled} 张（共 {poems.Count} 首诗）。");

        if (missing.Count > 0)
        {
            Debug.LogWarning($"[内置内容] 以下 {missing.Count} 首诗没有任何内置内容文件：" +
                             string.Join("、", missing) +
                             $"\n请把 {missing[0]}.txt / {missing[0]}.png 这样的文件放进 {ContentFolder}。");
        }
    }

    [MenuItem("Tools/诗词数据/创建内置内容目录", false, 21)]
    public static void CreateContentFolder()
    {
        if (AssetDatabase.IsValidFolder(ContentFolder))
        {
            EditorUtility.RevealInFinder(ContentFolder);
            return;
        }

        AssetDatabase.CreateFolder("Assets", "_PoemContentSource");
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(ContentFolder);
        Debug.Log($"[内置内容] 已创建目录：{ContentFolder}");
    }

    /// <summary>
    /// 清空诗词内容的本地缓存。清掉之后下次打开面板会重新调用 AI（会再次产生费用）。
    /// 平时不需要清；只有想「重新生成某一首」时才用。
    /// </summary>
    [MenuItem("Tools/诗词数据/清空诗词内容缓存（会重新调用 AI）", false, 22)]
    public static void ClearPoemCache()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "清空诗词内容缓存",
            "这会删除本地已生成的释义与插画缓存。\n" +
            "下次打开诗碑面板时会重新调用 AI（生图会产生费用）。\n\n确定要清空吗？",
            "确定清空", "取消");

        if (!confirmed)
            return;

        PoemContentCache.ClearAll();
        Debug.Log($"[内置内容] 已清空诗词内容缓存（目录：{PoemContentCache.CacheDirectory}）。");
    }

    // ---------------------------------------------------------------- 具体回填

    private static bool TryFillText(PoemSO poem, string id)
    {
        string path = Path.Combine(ContentFolder, id + ".txt").Replace('\\', '/');

        if (!File.Exists(path))
            return false;

        try
        {
            string text = File.ReadAllText(path, Encoding.UTF8);

            // 去掉 BOM 和首尾空白
            text = text.TrimStart('\uFEFF').Trim();

            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning($"[内置内容] {path} 是空文件，已跳过。");
                return false;
            }

            poem.builtinExplanation = text;
            EditorUtility.SetDirty(poem);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[内置内容] 读取 {path} 失败：{e.Message}");
            return false;
        }
    }

    private static bool TryFillImage(PoemSO poem, string id)
    {
        string path = Path.Combine(ContentFolder, id + ".png").Replace('\\', '/');

        if (!File.Exists(path))
            return false;

        // 先确保导入类型是 Sprite，否则 LoadAssetAtPath<Sprite> 会返回 null
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
        {
            Debug.LogError($"[内置内容] {path} 无法作为 Sprite 加载，请检查导入设置。");
            return false;
        }

        poem.builtinIllustration = sprite;
        EditorUtility.SetDirty(poem);
        return true;
    }

    // ---------------------------------------------------------------- 工具

    private static List<PoemSO> CollectPoems()
    {
        var poems = new List<PoemSO>();

        foreach (string guid in AssetDatabase.FindAssets("t:PoemSO", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            var poem = AssetDatabase.LoadAssetAtPath<PoemSO>(path);
            if (poem != null)
                poems.Add(poem);
        }

        poems.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return poems;
    }
}
