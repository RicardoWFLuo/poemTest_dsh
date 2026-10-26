using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "LocationSO")]
public class LocationSO : ScriptableObject, IIdentifiedAsset
{
    /// <summary>
    /// 地点 ID。直接复用已有的 locationID 字段，不再额外加一个 id，
    /// 避免同一个资源出现两个含义重叠的标识。
    /// 由菜单 Tools → 诗词数据 → 填充资源 ID 自动填充；填写后不要再改。
    /// </summary>
    public string locationID;

    public string Id => string.IsNullOrEmpty(locationID) ? null : locationID;

    public string displayName;
}
