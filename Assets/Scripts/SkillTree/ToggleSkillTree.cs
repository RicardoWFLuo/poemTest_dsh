using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleSkillTree : MonoBehaviour
{
    public CanvasGroup skillCanvas;
    private bool skillOpen = false;

    private void OnDisable()
    {
        // 面板被外部强制隐藏时，确保不会把暂停状态一起带走
        if (skillOpen)
        {
            skillOpen = false;
            PauseController.ReleasePause();
        }
    }

    void Update()
    {
        if (MobileInputManager.Instance == null || skillCanvas == null)
            return;

        if (!MobileInputManager.Instance.GetSkillDown())
            return;

        skillOpen = !skillOpen;

        if (skillOpen)
        {
            PauseController.RequestPause();
            skillCanvas.alpha = 1;
            skillCanvas.blocksRaycasts = true;
            skillCanvas.interactable = true;
        }
        else
        {
            PauseController.ReleasePause();
            skillCanvas.alpha = 0;
            skillCanvas.blocksRaycasts = false;
            skillCanvas.interactable = false;
        }
    }
}
