using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoemImageGN : MonoBehaviour
{
    public PoemSO thisPoem;
    private bool playerInRange = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    private void Update()
    {
        if (!playerInRange)
            return;

        if (PoemManager.Instance == null || MobileInputManager.Instance == null)
            return;

        if (thisPoem == null)
            return;

        if (!MobileInputManager.Instance.GetInteractDown())
            return;

        bool isCanvasActive = PoemManager.Instance.poemImageCanvas != null &&
                              PoemManager.Instance.poemImageCanvas.gameObject.activeSelf;
        bool isGenerating = PoemManager.Instance.isImageGenerating;

        if (isGenerating)
            return;

        if (isCanvasActive)
        {
            PoemManager.Instance.CloseImageCanvas();
            PoemManager.Instance.OpenMainCanvas();
        }
        else
        {
            PoemManager.Instance.OpenImageCanvasAndGenerate(thisPoem);
            PoemManager.Instance.CloseMainCanvas();
        }
    }

    private void OnDisable()
    {
        playerInRange = false;
    }
}
