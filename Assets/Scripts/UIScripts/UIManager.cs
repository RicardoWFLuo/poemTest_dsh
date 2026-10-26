using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup menuBar;
    private bool isMenuActive;

    [SerializeField] private CanvasGroup statsMenu;
    [SerializeField] private CanvasGroup skillMenu;
    [SerializeField] private CanvasGroup questMenu;

    [SerializeField] private Image menuToggleImage;
    [SerializeField] private Sprite openSprite;
    [SerializeField] private Sprite closeSprite;

    /// <summary>只显示目标子面板（其它子面板关闭）。</summary>
    public void ToggleMenu(CanvasGroup target)
    {
        SetMenuState(statsMenu, target != null && target == statsMenu);
        SetMenuState(skillMenu, target != null && target == skillMenu);
        SetMenuState(questMenu, target != null && target == questMenu);
    }

    public void ToggleMainMenu()
    {
        isMenuActive = !isMenuActive;
        SetMenuState(menuBar, isMenuActive);

        if (menuToggleImage != null)
            menuToggleImage.sprite = isMenuActive ? closeSprite : openSprite;

        SetMenuState(statsMenu, false);
        SetMenuState(skillMenu, false);
        SetMenuState(questMenu, false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void SetMenuState(CanvasGroup group, bool isActive)
    {
        if (group == null)
            return;

        group.alpha = isActive ? 1 : 0;
        group.interactable = isActive;
        group.blocksRaycasts = isActive;
    }
}
