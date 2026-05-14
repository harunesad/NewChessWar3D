using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterShopManager : MonoBehaviour
{
    [Header("Characters")]
    public List<CharacterSkinData> characters;
    
    [Header("UI References")]
    [SerializeField] GameObject characterItemPrefab;
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
        if (jsonSave.sv.purchasedCharacters == null) jsonSave.sv.purchasedCharacters = new List<string>();
        if (string.IsNullOrEmpty(jsonSave.sv.selectedCharacter)) jsonSave.sv.selectedCharacter = "Default";

        // Always ensure "Default" is purchased
        if (!jsonSave.sv.purchasedCharacters.Contains("Default"))
        {
            jsonSave.sv.purchasedCharacters.Add("Default");
        }

        for (int i = 0; i < characters.Count; i++)
        {
            int index = i;
            var charData = characters[i];
            var itemObj = Instantiate(characterItemPrefab, container);
            spawnedItems.Add(itemObj);

            // Set UI Icon
            Image iconImage = itemObj.GetComponent<Image>();
            if (iconImage != null) iconImage.sprite = charData.icon;

            bool isPurchased = jsonSave.sv.purchasedCharacters.Contains(charData.skinName);
            bool isSelected = jsonSave.sv.selectedCharacter == charData.skinName;

            // Handle Buy/Select logic
            Transform buyButtonTransform = itemObj.transform.GetChild(0); 
            Button itemButton = itemObj.GetComponent<Button>();

            if (isPurchased)
            {
                buyButtonTransform.gameObject.SetActive(false);
                itemButton.onClick.AddListener(() => SelectCharacter(index));
            }
            else
            {
                buyButtonTransform.gameObject.SetActive(true);
                TextMeshProUGUI priceText = buyButtonTransform.GetComponentInChildren<TextMeshProUGUI>();
                if (priceText != null) priceText.text = charData.price.ToString();
                
                Button buyButton = buyButtonTransform.GetComponent<Button>();
                buyButton.onClick.AddListener(() => BuyCharacter(index));
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

    void BuyCharacter(int index)
    {
        var charData = characters[index];
        if (jsonSave.sv.coin >= charData.price)
        {
            jsonSave.sv.coin -= charData.price;
            jsonSave.sv.purchasedCharacters.Add(charData.skinName);
            SaveManager.Save(jsonSave.sv);
            jsonSave.CoinUpdate();
            RefreshShop();
        }
        else
        {
            menuUIManager.MessageShow("Not Enough Coins");
        }
    }

    void SelectCharacter(int index)
    {
        var charData = characters[index];
        jsonSave.sv.selectedCharacter = charData.skinName;
        SaveManager.Save(jsonSave.sv);
        
        // Update SkinManager
        if (SkinManager.Instance != null)
        {
            SkinManager.Instance.SetCharacter(charData);
        }

        RefreshShop();
    }
}
