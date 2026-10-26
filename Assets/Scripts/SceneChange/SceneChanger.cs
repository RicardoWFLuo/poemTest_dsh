using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    public string sceneToLoad;
    public Animator fadeAnim;
    public float fadeTime = .5f;
    public Vector2 newPlayerPosition;
    private Transform player;

    private bool isTransitioning;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isTransitioning)
            return;

        if (!collision.gameObject.CompareTag("Player"))
            return;

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogError($"[SceneChanger] {name} 没有配置目标场景名。");
            return;
        }

        isTransitioning = true;

        player = collision.transform;

        if (fadeAnim != null)
            fadeAnim.Play("FadeToWhite");

        StartCoroutine(DelayFade());
    }

    IEnumerator DelayFade()
    {
        yield return new WaitForSeconds(fadeTime);

        if (player != null)
            player.position = newPlayerPosition;

        SceneManager.LoadScene(sceneToLoad);
    }
}
