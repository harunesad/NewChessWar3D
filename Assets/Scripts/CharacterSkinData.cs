using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterSkin", menuName = "Chess/Character Skin")]
public class CharacterSkinData : ScriptableObject
{
    public string skinName;
    public int price;
    public GameObject characterPrefab;
    public Sprite icon;
}
