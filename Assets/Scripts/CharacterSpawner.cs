using UnityEngine;
using UnityEngine.UI;
using ChessEngine.Game;

public class CharacterSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Karakterin doğacağı nokta. Boş bırakılırsa bu objenin pozisyonu kullanılır.")]
    [SerializeField] Transform spawnPoint;
    [SerializeField] bool spawnOnStart = true;
    
    void Start()
    {
        if (spawnOnStart)
        {
            SpawnSelectedCharacter();
        }
    }

    public void SpawnSelectedCharacter()
    {
        if (SkinManager.Instance == null) return;

        // 1. Get selected character data
        CharacterSkinData selectedChar = SkinManager.Instance.GetCurrentCharacter();
        if (selectedChar == null || selectedChar.characterPrefab == null)
        {
            Debug.LogWarning("CharacterSpawner: No character skin selected or prefab missing!");
            return;
        }

        // 2. Determine spawn position/rotation
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (spawnPoint != null)
        {
            spawnPos = spawnPoint.position;
            spawnRot = spawnPoint.rotation;
        }
        else
        {
            spawnPos = transform.position;
            spawnRot = transform.rotation;
        }

        // 3. Find and remove current player if it exists (for switching at runtime)
        GameObject currentPlayer = GameObject.Find("Character");
        if (currentPlayer != null)
        {
            DestroyImmediate(currentPlayer);
        }

        // 4. Instantiate new character and set its name to "Character"
        GameObject newPlayer = Instantiate(selectedChar.characterPrefab, spawnPos, spawnRot);
        newPlayer.name = "Character";

        // 5. Setup references (Joystick & Camera)
        SetupPlayerReferences(newPlayer);
        
        Debug.Log("CharacterSpawner: Spawned " + selectedChar.skinName + " at " + spawnPos);
    }

    private void SetupPlayerReferences(GameObject playerObj)
    {
        // Dynamic Joystick bul ve ata
        DynamicJoystick joystick = FindAnyObjectByType<DynamicJoystick>();
        PlayerMovement pm = playerObj.GetComponent<PlayerMovement>();
        if (pm != null && joystick != null)
        {
            pm.joystick = joystick;
        }

        // TPS Camera hedefi güncelle
        TPSCamera tpsCam = FindAnyObjectByType<TPSCamera>();
        if (tpsCam != null)
        {
            tpsCam.target = playerObj.transform;
        }

        // TPS Chess Interactor buton bağlantısını yap
        TPSChessInteractor interactor = playerObj.GetComponent<TPSChessInteractor>();
        if (interactor != null)
        {
            // Sahnede ismi "Select" olan butonu bulmaya çalış (Hiyerarşideki ismi)
            GameObject selectBtnObj = GameObject.Find("Select");
            if (selectBtnObj != null)
            {
                interactor.SetupButton(selectBtnObj.GetComponent<Button>());
            }
            else
            {
                // Alternatif: Tag veya tüm butonları tarayarak bulma
                Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
                foreach(var b in allButtons)
                {
                    if(b.name == "Select" || b.gameObject.name == "Select")
                    {
                        interactor.SetupButton(b);
                        break;
                    }
                }
            }
        }
    }
}
