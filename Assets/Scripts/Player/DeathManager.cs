using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DeathManager : MonoBehaviour
{
    [Header("死亡UI")]
    public GameObject deathPanel;
    public Image grayOverlay;
    public Button restartButton;
    public Button quitButton;
    public float fadeDuration = 1.5f;

    [Header("玩家出生点")]
    public Vector3 spawnPoint;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (deathPanel != null)
            deathPanel.SetActive(false);
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    public void ShowDeathScreen()
    {
        if (deathPanel != null)
            deathPanel.SetActive(true);

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        if (grayOverlay == null) yield break;

        float elapsed = 0;
        Color color = grayOverlay.color;
        color.a = 0;
        grayOverlay.color = color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (grayOverlay == null || !grayOverlay.gameObject.activeInHierarchy)
                yield break;

            color.a = Mathf.Lerp(0, 0.7f, elapsed / fadeDuration);
            grayOverlay.color = color;
            yield return null;
        }
    }

    private void RestartGame()
    {
        // 停止协程
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        // 隐藏死亡面板
        if (deathPanel != null)
            deathPanel.SetActive(false);

        // 重置颜色
        if (grayOverlay != null)
        {
            Color color = grayOverlay.color;
            color.a = 0;
            grayOverlay.color = color;
        }

        Time.timeScale = 1;
        // 死亡时可能还开着属性/技能面板，这里一并清空暂停请求
        PauseController.ResetAll();

        // 复活玩家
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.SetActive(true);
            player.transform.position = spawnPoint;

            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.Revive();
            }

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.isKinematic = false;
            }
        }

        // 重置血量（Revive 内部也会重置，这里兜底处理玩家对象不存在的情况）
        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.ResetHealth();
        }
    }

    private void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnDestroy()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        if (restartButton != null)
            restartButton.onClick.RemoveListener(RestartGame);
        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);
    }
}