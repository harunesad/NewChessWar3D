using UnityEngine;
using System.Collections.Generic;
using ChessEngine.Game;
using UnityEngine.SceneManagement;
using System.Reflection;
using UnityEngine.Events;
using ChessEngine.Game.Events;

public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }

    public ChessSkinData defaultSkin;
    public List<ChessSkinData> allSkins;

    public CharacterSkinData defaultCharacter;
    public List<CharacterSkinData> allCharacters;

    private ChessSkinData currentSkin;
    private CharacterSkinData currentCharacter;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SubscribeToTable();
    }

    void OnEnable()
    {
        SubscribeToTable();
    }

    public void SubscribeToTable()
    {
        VisualChessTable table = FindAnyObjectByType<VisualChessTable>();
        if (table != null)
        {
            table.InstantiateChessPieceDelegate -= OnInstantiatePiece;
            table.InstantiateChessPieceDelegate += OnInstantiatePiece;
            Debug.Log("Subscribed to VisualChessTable instantiation delegate.");
        }
    }

    private void OnInstantiatePiece(GameObject prefab, Transform parent, bool worldPositionStays, ref GameObject overrideInstance)
    {
        ChessSkinData skin = GetCurrentSkin();
        
        if (skin == null || skin == defaultSkin || skin.skinName == "Default") return;

        ChessPieceType? type = GetPieceType(prefab.name);
        if (type == null) return;
        
        GameObject skinModelPrefab = skin.GetPrefab(type.Value);
        if (skinModelPrefab == null) return;

        // 1. Instantiate the DEFAULT package prefab (Container)
        GameObject instance = Instantiate(prefab, parent, worldPositionStays);
        VisualChessPiece visualScript = instance.GetComponent<VisualChessPiece>();

        if (visualScript != null)
        {
            // 2. Find the exact renderer used by the package
            FieldInfo field = typeof(VisualChessPiece).GetField("m_RendererOverride", BindingFlags.Instance | BindingFlags.NonPublic);
            Renderer oldRend = (field != null) ? (Renderer)field.GetValue(visualScript) : null;

            if (oldRend != null)
            {
                // 3. Disable old model
                oldRend.gameObject.SetActive(false);
                
                // 4. Instantiate custom model and align it
                GameObject newModel = Instantiate(skinModelPrefab, oldRend.transform.parent != null ? oldRend.transform.parent : instance.transform);
                newModel.transform.localPosition = oldRend.transform.localPosition;
                newModel.transform.localRotation = oldRend.transform.localRotation;

                // 5. Update references and Sync MoveUpdate
                Renderer newRend = newModel.GetComponentInChildren<Renderer>();
                if (newRend != null)
                {
                    if (field != null) field.SetValue(visualScript, newRend);

                    PropertyInfo prop = typeof(VisualChessPiece).GetProperty("Renderer", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null) prop.SetValue(visualScript, newRend);
                }

                // SYNC MoveUpdate and materials
                MoveUpdate moveUpdate = newModel.GetComponent<MoveUpdate>();
                if (moveUpdate == null) moveUpdate = newModel.GetComponentInChildren<MoveUpdate>();

                if (moveUpdate != null)
                {
                    // Copy materials from MoveUpdate to VisualChessPiece
                    FieldInfo whiteMatFieldMU = typeof(MoveUpdate).GetField("whiteMat", BindingFlags.Instance | BindingFlags.NonPublic);
                    FieldInfo blackMatFieldMU = typeof(MoveUpdate).GetField("blackMat", BindingFlags.Instance | BindingFlags.NonPublic);
                    
                    Material muWhite = (whiteMatFieldMU != null) ? (Material)whiteMatFieldMU.GetValue(moveUpdate) : null;
                    Material muBlack = (blackMatFieldMU != null) ? (Material)blackMatFieldMU.GetValue(moveUpdate) : null;

                    FieldInfo whiteMatFieldVP = typeof(VisualChessPiece).GetField("m_WhiteMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
                    FieldInfo blackMatFieldVP = typeof(VisualChessPiece).GetField("m_BlackMaterial", BindingFlags.Instance | BindingFlags.NonPublic);

                    if (whiteMatFieldVP != null && muWhite != null) whiteMatFieldVP.SetValue(visualScript, muWhite);
                    if (blackMatFieldVP != null && muBlack != null) blackMatFieldVP.SetValue(visualScript, muBlack);

                    // CLEAR OLD EVENTS AND CONNECT TO NEW MODEL
                    // We only use Destroyed to avoid double counting points.
                    visualScript.Destroyed = new UnityEvent();
                    visualScript.Destroyed.AddListener(moveUpdate.PieceDestroy);

                    // We also reset Captured to clear old persistent listeners, but don't add a new listener there
                    if (visualScript.Captured != null)
                    {
                        visualScript.Captured = new VisualChessPiece.MoveUnityEvent();
                    }
                }
            }
            else
            {
                // Fallback
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
                {
                    r.enabled = false;
                }
                Instantiate(skinModelPrefab, instance.transform);
            }
        }

        overrideInstance = instance;
    }

    private ChessPieceType? GetPieceType(string name)
    {
        if (name.Contains("King")) return ChessPieceType.King;
        if (name.Contains("Queen")) return ChessPieceType.Queen;
        if (name.Contains("Bishop")) return ChessPieceType.Bishop;
        if (name.Contains("Knight")) return ChessPieceType.Knight;
        if (name.Contains("Rook")) return ChessPieceType.Rook;
        if (name.Contains("Pawn")) return ChessPieceType.Pawn;
        return null;
    }

    public void SetSkin(ChessSkinData skin)
    {
        currentSkin = skin;
    }

    public ChessSkinData GetCurrentSkin()
    {
        if (currentSkin == null)
        {
            SaveVariables sv = SaveManager.Load();
            if (sv != null && !string.IsNullOrEmpty(sv.selectedSkin))
            {
                currentSkin = allSkins.Find(s => s.skinName == sv.selectedSkin);
            }
        }
        
        return currentSkin != null ? currentSkin : defaultSkin;
    }

    public void SetCharacter(CharacterSkinData character)
    {
        currentCharacter = character;
    }

    public CharacterSkinData GetCurrentCharacter()
    {
        if (currentCharacter == null)
        {
            SaveVariables sv = SaveManager.Load();
            if (sv != null && !string.IsNullOrEmpty(sv.selectedCharacter))
            {
                currentCharacter = allCharacters.Find(s => s.skinName == sv.selectedCharacter);
            }
        }
        
        return currentCharacter != null ? currentCharacter : defaultCharacter;
    }
}
