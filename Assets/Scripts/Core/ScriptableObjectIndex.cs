using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时「ID → 资源」查表。
///
/// 存档只写 ID 字符串（例如 "Bone"），读档时通过这里换回 ItemSO 资源。
/// 表在第一次访问时从 Resources/GameDatabase 构建，之后常驻。
/// </summary>
public static class ScriptableObjectIndex
{
    private const string DatabaseResourcePath = "GameDatabase";

    private static Dictionary<string, ScriptableObject> byKey;
    private static bool initialized;

    /// <summary>索引是否已就绪（GameDatabase 存在且至少加载过）。</summary>
    public static bool IsReady => initialized && byKey != null && byKey.Count > 0;

    /// <summary>按类型 + ID 取资源。</summary>
    public static bool TryGet<T>(string id, out T asset) where T : ScriptableObject
    {
        asset = null;

        if (string.IsNullOrEmpty(id))
            return false;

        EnsureInitialized();

        if (byKey == null)
            return false;

        if (!byKey.TryGetValue(MakeKey(typeof(T), id), out var found) || found == null)
            return false;

        asset = found as T;
        return asset != null;
    }

    /// <summary>取某个资源的存档 ID；没有实现 IIdentifiedAsset 或 id 为空时返回 null。</summary>
    public static string GetId(ScriptableObject asset)
    {
        if (asset is IIdentifiedAsset identified && !string.IsNullOrEmpty(identified.Id))
            return identified.Id;

        return null;
    }

    /// <summary>强制重建索引（资源热更或测试时使用）。</summary>
    public static void Rebuild()
    {
        initialized = false;
        byKey = null;
        EnsureInitialized();
    }

    private static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        byKey = new Dictionary<string, ScriptableObject>();

        var database = Resources.Load<GameDatabase>(DatabaseResourcePath);
        if (database == null)
        {
            Debug.LogError(
                $"[ScriptableObjectIndex] 找不到 Resources/{DatabaseResourcePath}.asset。" +
                "请在 Unity 中执行菜单：Tools → 诗词数据 → 一键初始化（填 ID + 建索引）。");
            return;
        }

        foreach (var asset in database.EnumerateAll())
        {
            if (asset is not IIdentifiedAsset identified)
                continue;

            string id = identified.Id;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError($"[ScriptableObjectIndex] 资源「{asset.name}」没有填写 ID，已被跳过。" +
                               "请执行菜单：Tools → 诗词数据 → 填充资源 ID。");
                continue;
            }

            string key = MakeKey(asset.GetType(), id);
            if (byKey.ContainsKey(key))
            {
                Debug.LogError($"[ScriptableObjectIndex] 出现重复 ID「{id}」（类型 {asset.GetType().Name}），" +
                               $"后者「{asset.name}」被忽略。请执行菜单检查重复 ID。");
                continue;
            }

            byKey[key] = asset;
        }
    }

    private static string MakeKey(System.Type type, string id) => type.Name + "|" + id;
}
