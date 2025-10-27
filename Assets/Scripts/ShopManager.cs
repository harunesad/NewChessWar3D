using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public List<Items> items;
    [SerializeField] GameObject item;
    [SerializeField] Button skinBtn;
    [SerializeField] List<GameObject> itemObjects;
    [SerializeField] List<Sprite> itemImage;
    MenuUIManager menuUIManager;
    JsonSave jsonSave;
    void Start()
    {
        jsonSave = FindAnyObjectByType<JsonSave>();
        menuUIManager = FindAnyObjectByType<MenuUIManager>();
        skinBtn.onClick.AddListener(delegate { menuUIManager.MessageShow("Coming Soon"); });

        for (int i = 0; i < items.Count; i++)
        {
            int j = i;
            var item = Instantiate(this.item, transform);
            item.GetComponent<Image>().sprite = itemImage[i];
            itemObjects.Add(item);
            if (items[i].purchased)
            {
                item.GetComponent<Button>().onClick.AddListener(delegate { PieceSelect(j); });
                item.transform.GetChild(0).gameObject.SetActive(false);
                item.transform.GetChild(1).gameObject.SetActive(false);
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
            menuUIManager.MessageShow("Enough Coin");
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
