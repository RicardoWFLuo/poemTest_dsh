using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 诗词内容的本地缓存。
///
/// 为什么需要它：AI 生图是**真扣费**的（万相没有免费额度），
/// 如果每次打开面板都重新请求，就会反复花钱。
/// 所以生成成功的结果要落盘，下次直接读，不再调接口。
///
/// 查找顺序（先找到先用）：
///   1. 运行时缓存目录         persistentDataPath/poem_cache/Poem1.txt|png
///   2. 项目内容目录（仅编辑器） Assets/_PoemContentSource/Poem1.txt|png
///   3. PoemSO 上的内置内容字段（由「回填内置内容」写入）
///   4. 以上都没有，才真正去调用 AI
/// </summary>
public static class PoemContentCache
{
    private const string FolderName = "poem_cache";

    private static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

    /// <summary>运行时缓存目录。</summary>
    public static string CacheDirectory => Path.Combine(Application.persistentDataPath, FolderName);

    // ---------------------------------------------------------------- 读取

    public static bool TryGetExplanation(string id, out string text)
    {
        text = null;

        if (string.IsNullOrEmpty(id))
            return false;

        foreach (string path in CandidatePaths(id, ".txt"))
        {
            if (!File.Exists(path))
                continue;

            try
            {
                string content = File.ReadAllText(path, Encoding.UTF8);

                // 去掉可能存在的 BOM
                content = content.TrimStart('\uFEFF').Trim();

                if (!string.IsNullOrEmpty(content))
                {
                    text = content;
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[诗词缓存] 读取 {path} 失败：{e.Message}");
            }
        }

        return false;
    }

    public static bool TryGetIllustration(string id, out Texture2D texture)
    {
        texture = null;

        if (string.IsNullOrEmpty(id))
            return false;

        // 内存里已经有了就直接用，避免每次打开都重新解码
        if (textureCache.TryGetValue(id, out Texture2D cached) && cached != null)
        {
            texture = cached;
            return true;
        }

        foreach (string path in CandidatePaths(id, ".png"))
        {
            if (!File.Exists(path))
                continue;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (!tex.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(tex);
                    continue;
                }

                tex.name = "PoemCache_" + id;
                textureCache[id] = tex;
                texture = tex;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[诗词缓存] 读取 {path} 失败：{e.Message}");
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- 写入

    public static bool SaveExplanation(string id, string text)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(text))
            return false;

        try
        {
            Directory.CreateDirectory(CacheDirectory);

            // 不带 BOM 的 UTF-8
            File.WriteAllText(Path.Combine(CacheDirectory, id + ".txt"),
                text.Trim(), new UTF8Encoding(false));

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[诗词缓存] 写入释义缓存失败（{id}）：{e.Message}");
            return false;
        }
    }

    public static bool SaveIllustration(string id, Texture2D source)
    {
        if (string.IsNullOrEmpty(id) || source == null)
            return false;

        try
        {
            Directory.CreateDirectory(CacheDirectory);

            byte[] png = source.EncodeToPNG();

            if (png == null || png.Length == 0)
                return false;

            File.WriteAllBytes(Path.Combine(CacheDirectory, id + ".png"), png);

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[诗词缓存] 写入插画缓存失败（{id}）：{e.Message}");
            return false;
        }
    }

    // ---------------------------------------------------------------- 维护

    /// <summary>判断某首诗是否已经有缓存（用于避免重复调用）。</summary>
    public static bool HasAnyContent(string id)
    {
        return TryGetExplanation(id, out _) || HasIllustrationFile(id);
    }

    public static bool HasIllustrationFile(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        foreach (string path in CandidatePaths(id, ".png"))
        {
            if (File.Exists(path))
                return true;
        }

        return false;
    }

    /// <summary>清空所有缓存（含运行时目录与内存里的贴图）。</summary>
    public static void ClearAll()
    {
        foreach (var pair in textureCache)
        {
            if (pair.Value != null)
                UnityEngine.Object.Destroy(pair.Value);
        }

        textureCache.Clear();

        try
        {
            if (Directory.Exists(CacheDirectory))
                Directory.Delete(CacheDirectory, true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[诗词缓存] 删除缓存目录失败：{e.Message}");
        }
    }

    /// <summary>删除某一首诗的缓存。</summary>
    public static void ClearOne(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (textureCache.TryGetValue(id, out Texture2D tex))
        {
            if (tex != null)
                UnityEngine.Object.Destroy(tex);

            textureCache.Remove(id);
        }

        foreach (string path in CandidatePaths(id, ".txt"))
        {
            TryDelete(path);
        }

        foreach (string path in CandidatePaths(id, ".png"))
        {
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 删不掉就算了，下次仍会被读出来，但不影响使用
        }
    }

    // ---------------------------------------------------------------- 路径

    private static IEnumerable<string> CandidatePaths(string id, string extension)
    {
        // 1) 运行时缓存
        yield return Path.Combine(CacheDirectory, id + extension);

#if UNITY_EDITOR
        // 2) 项目内的内容目录（编辑器里直接可用，不必等回填）
        yield return Path.Combine(Application.dataPath, PoemContentExport.FolderName, id + extension);
#endif
    }
}
