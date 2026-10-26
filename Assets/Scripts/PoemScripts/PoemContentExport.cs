using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 把游戏内 AI 生成的内容导出到项目的 _PoemContentSource 目录。
///
/// 用途：你不需要手动另存文件 —— 在编辑器里把十首诗各点一遍，
/// 生成的释义和插画就会自动写到：
///     Assets/_PoemContentSource/Poem1.txt
///     Assets/_PoemContentSource/Poem1.png
/// 之后再执行菜单 Tools → 诗词数据 → 4. 回填内置内容 即可。
///
/// 只在编辑器里有效：打包后的游戏没有 Assets 目录可写，
/// 因此整个类的内容都用 #if UNITY_EDITOR 包起来，不会进入 Player 构建。
/// </summary>
public static class PoemContentExport
{
    public const string FolderName = "_PoemContentSource";

#if UNITY_EDITOR

    /// <summary>内容目录的绝对路径（编辑器下 Application.dataPath 就是项目的 Assets 目录）。</summary>
    public static string FolderPath => Path.Combine(Application.dataPath, FolderName);

    /// <summary>Unity 项目内的相对路径，可直接用于 AssetDatabase。</summary>
    public static string ProjectRelativeFolder => "Assets/" + FolderName;

    public static string TextPath(string id) => Path.Combine(FolderPath, id + ".txt");

    public static string ImagePath(string id) => Path.Combine(FolderPath, id + ".png");

    /// <summary>把释义写入 {id}.txt。已存在则覆盖。</summary>
    public static bool SaveExplanation(string id, string text)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(text))
            return false;

        try
        {
            Directory.CreateDirectory(FolderPath);

            string path = TextPath(id);

            // 不带 BOM 的 UTF-8，避免回填时把 BOM 读进正文
            File.WriteAllText(path, text.Trim(), new UTF8Encoding(false));

            Debug.Log($"[内置内容导出] 释义已写入 {ProjectRelativeFolder}/{id}.txt");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[内置内容导出] 写入释义失败（{id}）：{e.Message}");
            return false;
        }
    }

    /// <summary>把生成出来的贴图存成 {id}.png。已存在则覆盖。</summary>
    public static bool SaveImage(string id, Texture2D texture)
    {
        if (string.IsNullOrEmpty(id) || texture == null)
            return false;

        try
        {
            Directory.CreateDirectory(FolderPath);

            byte[] png = texture.EncodeToPNG();

            if (png == null || png.Length == 0)
            {
                Debug.LogError($"[内置内容导出] 贴图编码为 PNG 失败（{id}），" +
                               "可能是该贴图不可读（未勾选 Read/Write）。");
                return false;
            }

            File.WriteAllBytes(ImagePath(id), png);

            Debug.Log($"[内置内容导出] 插画已写入 {ProjectRelativeFolder}/{id}.png");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[内置内容导出] 写入插画失败（{id}）：{e.Message}");
            return false;
        }
    }

#else

    // 非编辑器环境：直接返回 false，调用方无需关心平台差异
    public static bool SaveExplanation(string id, string text) => false;

    public static bool SaveImage(string id, Texture2D texture) => false;

#endif
}
