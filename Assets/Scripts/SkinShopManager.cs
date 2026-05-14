using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinShopManager : MonoBehaviour
{
    [Header("Skins")]
    public List<ChessSkinData> skins;
    
    [Header("UI References")]
    [SerializeField] GameObject skinItemPrefab;
    [SerializeField] Transform container;
    
    private List<GameObject> spawnedItems = new List<GameObject>();
    private JsonSave jsonSave;
    private MenuUIManager menuUIManager;

    void Start()
    {
        jsonSave = FindAnyObjectByType<JsonSave>();
        menuUIManager = FindAnyObjectByType<MenuUIManager>();
        
        RefreshShop();
    }

    public void RefreshShop()
    {
        // Clear existing items
        foreach (var obj in spawnedItems) Destroy(obj);
        spawnedItems.Clear();

        // Ensure save data lists are initialized
        if (jsonSave.sv.purchasedSkins == null) jsonSave.sv.purchasedSkins = new List<string>();
        if (string.IsNullOrEmpty(jsonSave.sv.selectedSkin)) jsonSave.sv.selectedSkin = "Default";

        // Always ensure "Default" is purchased
        if (!jsonSave.sv.purchasedSkins.Contains("Default"))
        {
            jsonSave.sv.purchasedSkins.Add("Default");
        }

        for (int i = 0; i < skins.Count; i++)
        {
            int index = i;
            var skinData = skins[i];
            var itemObj = Instantiate(skinItemPrefab, container);
            spawnedItems.Add(itemObj);

            // Set UI elements (assuming common structure: Image, Button, Text)
            Image iconImage = itemObj.GetComponent<Image>();
            if (iconImage != null) iconImage.sprite = skinData.icon;

            bool isPurchased = jsonSave.sv.purchasedSkins.Contains(skinData.skinName);
            bool isSelected = jsonSave.sv.selectedSkin == skinData.skinName;

            // Handle Buy/Select logic
            Transform buyButtonTransform = itemObj.transform.GetChild(0); // Assuming 1st child is the buy button
            Button itemButton = itemObj.GetComponent<Button>();

            if (isPurchased)
            {
                buyButtonTransform.gameObject.SetActive(false);
                itemButton.onClick.AddListener(() => SelectSkin(index));
            }
            else
            {
                buyButtonTransform.gameObject.SetActive(true);
                TextMeshProUGUI priceText = buyButtonTransform.GetComponentInChildren<TextMeshProUGUI>();
                if (priceText != null) priceText.text = skinData.price.ToString();
                
                Button buyButton = buyButtonTransform.GetComponent<Button>();
                buyButton.onClick.AddListener(() => BuySkin(index));
            }

            // Visual feedback for selection
            if (isSelected)
            {
                itemObj.GetComponent<Image>().color = new Color(1, 1, 1, 1);
            }
            else
            {
                itemObj.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
            }
        }
    }

    void BuySkin(int index)
    {
        var skinData = skins[index];
        if (jsonSave.sv.coin >= skinData.price)
        {
            jsonSave.sv.coin -= skinData.price;
            jsonSave.sv.purchasedSkins.Add(skinData.skinName);
            SaveManager.Save(jsonSave.sv);
            jsonSave.CoinUpdate();
            RefreshShop();
        }
        else
        {
            menuUIManager.MessageShow("Not Enough Coins");
        }
    }

    void SelectSkin(int index)
    {
        var skinData = skins[index];
        jsonSave.sv.selectedSkin = skinData.skinName;
        SaveManager.Save(jsonSave.sv);
        
        // Update SkinManager
        if (SkinManager.Instance != null)
        {
            SkinManager.Instance.SetSkin(skinData);
        }

        RefreshShop();
    }
}
