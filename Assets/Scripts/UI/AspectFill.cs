using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 让图片「铺满父级但保持比例」——超出部分裁掉，不变形。
///
/// 用途：开局封面是 16:9，而手机屏幕可能是 20:9。
/// 直接拉伸会让画面变宽变形；用这个组件改成等比放大后裁切，
/// 观感和视频 App 的「填充屏幕」一致。
///
/// 用法：挂在一个 Image 上，父节点需要有 RectTransform（通常是全屏容器）。
/// </summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class AspectFill : MonoBehaviour
{
    [Tooltip("父节点尺寸变化时是否重新计算（横竖屏切换时需要）。")]
    public bool followParentSize = true;

    private RectTransform rectTransform;
    private Image image;
    private RectTransform parentRect;
    private Vector2 lastParentSize;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        parentRect = transform.parent as RectTransform;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (!followParentSize || parentRect == null)
            return;

        Vector2 size = parentRect.rect.size;

        if (!Mathf.Approximately(size.x, lastParentSize.x) || !Mathf.Approximately(size.y, lastParentSize.y))
            Apply();
    }

    /// <summary>按父级尺寸等比铺满并居中。</summary>
    public void Apply()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (image == null)
            image = GetComponent<Image>();

        if (parentRect == null)
            parentRect = transform.parent as RectTransform;

        if (parentRect == null || image == null || image.sprite == null)
            return;

        Vector2 parentSize = parentRect.rect.size;

        if (parentSize.x <= 0f || parentSize.y <= 0f)
            return;

        lastParentSize = parentSize;

        Rect spriteRect = image.sprite.rect;
        float spriteAspect = spriteRect.width / spriteRect.height;
        float parentAspect = parentSize.x / parentSize.y;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;

        if (spriteAspect > parentAspect)
        {
            // 图更宽 → 高度对齐，左右溢出被裁
            float width = parentSize.y * spriteAspect;
            rectTransform.sizeDelta = new Vector2(width, parentSize.y);
        }
        else
        {
            // 图更高 → 宽度对齐，上下溢出被裁
            float height = parentSize.x / spriteAspect;
            rectTransform.sizeDelta = new Vector2(parentSize.x, height);
        }

        // 裁切需要父级上有 RectMask2D 或 Mask
        if (parentRect.GetComponent<RectMask2D>() == null && parentRect.GetComponent<Mask>() == null)
            parentRect.gameObject.AddComponent<RectMask2D>();
    }
}
