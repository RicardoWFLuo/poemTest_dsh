using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopButtonToggles : MonoBehaviour
{
    public ShopManager shopManager;


    public void OpenItemShop()
    {
        if(ShopKeeper.currentShopKeeper != null)
        {
            ShopKeeper.currentShopKeeper.OpenItemShop();
        }
    }
    public void OpenWeaponsShop()
    {
        Debug.Log("按钮被点击了");
        if (ShopKeeper.currentShopKeeper != null)
        {
            ShopKeeper.currentShopKeeper.OpenWeaponShop();
            Debug.Log("成功调用OpenWeapon");
        }
        else
        {
            Debug.Log("ShopKeeper.currentShopKeeper = null");
        }
    }
    public void OpenArmourShop()
    {
        if(ShopKeeper.currentShopKeeper != null)
        {
            ShopKeeper.currentShopKeeper.OpenArmourShop();
        }
    }
}
