using UnityEngine;

/// <summary>
/// 让 RectTransform 贴合屏幕安全区（Screen.safeArea）。
///
/// 作用：手机上避开刘海、挖孔、圆角、底部手势条，避免按钮被挡住或点不到。
/// 用法：挂到一个铺满屏幕的 RectTransform 上，它的子对象就会自动落在安全区内。
///
/// 注意：它会覆盖这个 RectTransform 的锚点，所以不要挂在需要自定义锚点的对象上。
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeAreaFitter : MonoBehaviour
{
    [Tooltip("四个方向是否都贴合安全区。一般保持默认全勾。")]
    public bool applyLeft = true;
    public bool applyRight = true;
    public bool applyTop = true;
    public bool applyBottom = true;

    [Tooltip("在安全区基础上再额外留白（参考分辨率像素）。")]
    public Vector2 extraPadding = Vector2.zero;

    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        Apply();
    }

    private void Update()
    {
        // 屏幕尺寸或安全区变化（旋转 / 刘海切换）时重新计算
        if (Screen.safeArea != lastSafeArea ||
            Screen.width != lastScreenSize.x ||
            Screen.height != lastScreenSize.y)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (rectTransform == null)
            return;

        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Vector2 min = lastSafeArea.position;
        Vector2 max = lastSafeArea.position + lastSafeArea.size;

        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        if (!applyLeft) min.x = 0f;
        if (!applyBottom) min.y = 0f;
        if (!applyRight) max.x = 1f;
        if (!applyTop) max.y = 1f;

        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;

        // 用绝对偏移把额外留白算进去（参考分辨率下按 UI 缩放换算更复杂，这里直接用像素偏移）
        rectTransform.offsetMin = new Vector2(extraPadding.x, extraPadding.y);
        rectTransform.offsetMax = new Vector2(-extraPadding.x, -extraPadding.y);
    }
}
