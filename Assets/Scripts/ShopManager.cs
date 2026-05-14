using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    [Header("Items & Skins")]
    public List<Items> items;
    [SerializeField] GameObject item;
    
    [Header("Panels")]
    [SerializeField] GameObject colorPanel;
    [SerializeField] GameObject skinPanel;
    [SerializeField] GameObject characterPanel;

    [Header("Tab Buttons")]
    [SerializeField] Button colorBtn;
    [SerializeField] Button skinBtn;
    [SerializeField] Button characterBtn;

    [SerializeField] List<GameObject> itemObjects;
    [SerializeField] List<Sprite> itemImage;
    
    MenuUIManager menuUIManager;
    JsonSave jsonSave;

    void Start()
    {
        jsonSave = FindAnyObjectByType<JsonSave>();        menuUIManager = FindAnyObjectByType<MenuUIManager>();

        // Tab Butonlarını Ayarla
        colorBtn.onClick.AddListener(ShowColorTab);
        skinBtn.onClick.AddListener(ShowSkinTab);
        characterBtn.onClick.AddListener(ShowCharacterTab);

        // Başlangıçta Renk Panelini Göster
        ShowColorTab();

        for (int i = 0; i < items.Count; i++)
        {
            int j = i;
            var item = Instantiate(this.item, colorPanel.transform.GetChild(0));
            item.GetComponent<Image>().sprite = itemImage[i];
            itemObjects.Add(item);
            if (items[i].purchased)
            {
                item.GetComponent<Button>().onClick.AddListener(delegate { PieceSelect(j); });
                item.transform.GetChild(0).gameObject.SetActive(false);
            }
            else
            {
                item.GetComponentInChildren<TextMeshProUGUI>().text = items[i].price.ToString();
                item.transform.GetChild(0).GetComponent<Button>().onClick.AddListener(delegate { BuyItem(item, j); });
            }
            if (items[i].selected)
            {
                Color itemColor = item.GetComponent<Image>().color;
                item.GetComponent<Image>().color = new Color(itemColor.r, itemColor.g, itemColor.b, 1);
            }
        }
    }

    public void ShowColorTab()
    {
        colorPanel.SetActive(true);
        skinPanel.SetActive(false);
        characterPanel.SetActive(false);
        
        SetButtonAlpha(colorBtn, 1.0f);
        SetButtonAlpha(skinBtn, 0.5f);
        SetButtonAlpha(characterBtn, 0.5f);
    }

    public void ShowSkinTab()
    {
        colorPanel.SetActive(false);
        skinPanel.SetActive(true);
        characterPanel.SetActive(false);

        SetButtonAlpha(colorBtn, 0.5f);
        SetButtonAlpha(skinBtn, 1.0f);
        SetButtonAlpha(characterBtn, 0.5f);
    }

    public void ShowCharacterTab()
    {
        colorPanel.SetActive(false);
        skinPanel.SetActive(false);
        characterPanel.SetActive(true);

        SetButtonAlpha(colorBtn, 0.5f);
        SetButtonAlpha(skinBtn, 0.5f);
        SetButtonAlpha(characterBtn, 1.0f);
    }

    private void SetButtonAlpha(Button btn, float alpha)
    {
        if (btn.GetComponent<Image>() != null)
        {
            Color c = btn.GetComponent<Image>().color;
            btn.GetComponent<Image>().color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    void BuyItem(GameObject item, int i)
    {
        if (jsonSave.sv.coin >= items[i].price)
        {
            jsonSave.sv.coin -= items[i].price;
            items[i].purchased = true;
            jsonSave.sv.items = items;
            SaveManager.Save(jsonSave.sv);
            item.transform.GetChild(0).GetComponent<Button>().gameObject.SetActive(false);
            item.GetComponent<Button>().onClick.AddListener(delegate { PieceSelect(i); });
            jsonSave.CoinUpdate();
        }
        else
        {
            menuUIManager.MessageShow("Not Enough Coins");
        }
    }
    void PieceSelect(int i)
    {
        for (int j = 0; j < itemObjects.Count; j++)
        {
            if (items[j].selected && items[i].white == items[j].white)
            {
                Color itemColor = itemObjects[j].GetComponent<Image>().color;
                itemObjects[j].GetComponent<Image>().color = new Color(itemColor.r, itemColor.g, itemColor.b, .5f);
                items[j].selected = false;
            }
        }
        if (!items[i].selected)
        {
            Color itemColor = itemObjects[i].GetComponent<Image>().color;
            itemObjects[i].GetComponent<Image>().color = new Color(itemColor.r, itemColor.g, itemColor.b, 1);
            items[i].selected = true;
        }
        jsonSave.sv.items = items;
        SaveManager.Save(jsonSave.sv);
    }
}
[System.Serializable]
public class Items
{
    public string name;
    public int price;
    public bool purchased;
    public bool selected;
    public bool white;
}
