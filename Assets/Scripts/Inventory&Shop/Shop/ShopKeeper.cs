using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopKeeper : MonoBehaviour
{
    public static ShopKeeper currentShopKeeper;

    public Animator anim;
    public CanvasGroup shopCanvasGroup;
    public ShopManager shopManager;

    [SerializeField] private List<ShopItems> shopItems;
    [SerializeField] private List<ShopItems> shopWeapons;
    [SerializeField] private List<ShopItems> shopArmour;

    [SerializeField] private Camera shopKeeperCam;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -1);


    public static event Action<ShopManager, bool> OnShopStateChanged;

    private bool playerInRange;
    private bool isShopOpen;

    private void OnEnable()
    {
        currentShopKeeper = this;
    }

    void Update()
    {
        if (!playerInRange)
            return;

        if (MobileInputManager.Instance == null)
            return;

        if (MobileInputManager.Instance.GetInteractDown())
        {
            SetShopOpen(!isShopOpen);
        }
    }

    /// <summary>统一开关商店，避免"打开/关闭"两段逻辑各写一遍还漏掉状态。</summary>
    private void SetShopOpen(bool open)
    {
        isShopOpen = open;
        currentShopKeeper = open ? this : null;

        OnShopStateChanged?.Invoke(shopManager, open);

        if (shopCanvasGroup != null)
        {
            shopCanvasGroup.alpha = open ? 1 : 0;
            shopCanvasGroup.blocksRaycasts = open;
            shopCanvasGroup.interactable = open;
        }

        if (shopKeeperCam != null)
        {
            if (open)
            {
                shopKeeperCam.transform.position = transform.position + cameraOffset;
            }
            shopKeeperCam.gameObject.SetActive(open);
        }

        if (open)
        {
            OpenItemShop();
        }
    }

    public void OpenItemShop()
    {
        if (shopManager != null)
            shopManager.PopulateShopItems(shopItems);
    }
    public void OpenWeaponShop()
    {
        if (shopManager != null)
            shopManager.PopulateShopItems(shopWeapons);
    }
    public void OpenArmourShop()
    {
        if (shopManager != null)
            shopManager.PopulateShopItems(shopArmour);
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (anim != null)
                anim.SetBool("playerInRange", true);

            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        if (anim != null)
            anim.SetBool("playerInRange", false);

        playerInRange = false;

        // 走远之后自动关店，避免商店界面跟着玩家一起跑
        if (isShopOpen)
            SetShopOpen(false);
    }

    private void OnDisable()
    {
        if (isShopOpen)
            SetShopOpen(false);
    }
}
