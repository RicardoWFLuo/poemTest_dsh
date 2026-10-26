using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跟随场景切换，把 Cinemachine 的 2D 边界重新指向新场景里 Tag 为 Confiner 的多边形碰撞体。
/// </summary>
public class ConfinerFinder : MonoBehaviour
{
    private CinemachineConfiner2D confiner;

    private void Awake()
    {
        confiner = GetComponent<CinemachineConfiner2D>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshConfiner();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshConfiner();
    }

    private void RefreshConfiner()
    {
        if (confiner == null)
            return;

        // 历史版本直接链式调用 GetComponent，场景里没有 Confiner 时会空引用崩溃
        GameObject confinerObject = GameObject.FindWithTag("Confiner");
        if (confinerObject == null)
        {
            Debug.LogWarning("[ConfinerFinder] 当前场景里没有 Tag 为 Confiner 的对象，跳过相机边界设置。");
            return;
        }

        if (confinerObject.TryGetComponent(out PolygonCollider2D polygon))
        {
            confiner.m_BoundingShape2D = polygon;
            confiner.InvalidateCache();
        }
        else
        {
            Debug.LogWarning("[ConfinerFinder] Confiner 对象上没有 PolygonCollider2D。");
        }
    }
}
