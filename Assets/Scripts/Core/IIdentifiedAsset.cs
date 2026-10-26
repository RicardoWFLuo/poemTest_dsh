using UnityEngine;

/// <summary>
/// 可被存档系统引用的资源都需要一个稳定的字符串 ID。
///
/// 为什么不能直接用对象引用：Unity 的 ScriptableObject 引用无法写进 JSON 存档。
/// 为什么不直接用资源文件名：文件可以被重命名，改名后老存档就失效。
/// 所以用「填一次就不动」的 id 字段 —— 改名不影响存档，改 id 才会。
/// </summary>
public interface IIdentifiedAsset
{
    /// <summary>该资源的稳定 ID（由 Editor 工具按文件名自动填充）。</summary>
    string Id { get; }
}
