using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ChessEngine.Game;
using ChessEngine;
using Clickables;

public class TPSChessInteractor : MonoBehaviour
{
    private enum State { IDLE, CARRYING }

    [Header("Settings")]
    public Vector3 carryPositionOffset = new Vector3(0, 0, 1.2f);
    public float returnDuration = 0.5f;

    [Header("References")]
    public Button selectButton;
    public ChessGameManager gameManager;

    [Header("Events")]
    public System.Action OnPiecePickedUp;
    public System.Action OnPieceDropped;
    public System.Action OnInteractionZoneEntered;

    private Animator animator;
    private State currentState = State.IDLE;
    private VisualChessPiece carriedPiece;
    private Vector3 carriedPieceOriginalPosition;

    // Trigger takibi için
    private VisualChessTableTile currentTriggerTile;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (gameManager == null) gameManager = FindAnyObjectByType<ChessGameManager>();

        if (selectButton != null)
        {
            SetupButton(selectButton);
        }
    }

    public void SetupButton(Button btn)
    {
        if (selectButton != null) selectButton.onClick.RemoveListener(OnSelectButtonClicked);
        
        selectButton = btn;
        
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnSelectButtonClicked);
            selectButton.gameObject.SetActive(true);
            selectButton.interactable = true;
        }
    }

    void Start()
    {
        // Manuel tıklayıcıları kapat
        Clicker[] clickers = FindObjectsByType<Clicker>(FindObjectsSortMode.None);
        foreach (Clicker clicker in clickers) clicker.enabled = false;
    }

    void Update()
    {
        if (currentState == State.CARRYING)
        {
            UpdateCarrying();
        }
        else
        {
            // Buton artık hep aktif, oyuncu her zaman basabilir
            if (selectButton != null)
                selectButton.interactable = true;
        }
    }

    private void UpdateCarrying()
    {
        if (carriedPiece == null) return;
        if (selectButton != null) selectButton.interactable = true;

        Vector3 targetPosition = transform.position + transform.TransformDirection(carryPositionOffset);
        targetPosition.y = carriedPieceOriginalPosition.y; 
        carriedPiece.transform.position = targetPosition;
    }

    // --- Trigger Yönetimi ---
    // Karakterin collider'ına çarpan trigger'ları dinler
    private void OnTriggerEnter(Collider other)
    {
        if (currentState == State.CARRYING) return;

        // Çarptığımız objede veya üstünde VisualChessPiece var mı?
        VisualChessPiece piece = other.GetComponentInParent<VisualChessPiece>();
        if (piece != null)
        {
            SetCurrentTileFromPiece(piece);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        VisualChessPiece piece = other.GetComponentInParent<VisualChessPiece>();
        if (piece != null && currentTriggerTile != null && currentTriggerTile.GetVisualPiece() == piece)
        {
            currentTriggerTile = null;
        }
    }

    private void SetCurrentTileFromPiece(VisualChessPiece piece)
    {
        if (gameManager == null || gameManager.visualTable == null) return;

        // SADECE kendi taşlarımızı seçebilmek için renk kontrolü
        ChessColor myColor = gameManager.ChessInstance.turn;
        if (piece.Piece.Color != myColor) return;

        foreach (var row in gameManager.visualTable.VisualTiles)
        {
            foreach (var tile in row)
            {
                if (tile.GetVisualPiece() == piece)
                {
                    currentTriggerTile = tile;
                    OnInteractionZoneEntered?.Invoke();
                    return;
                }
            }
        }
    }

    private void OnSelectButtonClicked()
    {
        if (currentState == State.IDLE)
        {
            if (currentTriggerTile != null)
            {
                StartCarrying();
            }
            else
            {
                // UYARI VERME ANI
                ShowInteractionWarning();
            }
        }
        else if (currentState == State.CARRYING)
        {
            StopCarrying();
        }
    }

    private void ShowInteractionWarning()
    {
        // Sahnede GameUIManager'ı bul ve mesajı ekrana yazdır
        GameUIManager uiManager = FindAnyObjectByType<GameUIManager>();
        if (uiManager != null)
        {
            uiManager.MessageShow("You must stand behind the piece!");
        }
        else
        {
            Debug.Log("Warning: Stand behind the piece!");
        }
    }

    private void StartCarrying()
    {
        if (currentTriggerTile == null) return;

        carriedPiece = currentTriggerTile.GetVisualPiece();
        if (carriedPiece == null) return;

        gameManager.SelectTile(currentTriggerTile);
        carriedPieceOriginalPosition = carriedPiece.transform.position;
        currentState = State.CARRYING;
        OnPiecePickedUp?.Invoke();
    }

    private void StopCarrying()
    {
        if (carriedPiece == null) return;

        VisualChessTableTile targetTile = FindNearestTile();

        if (targetTile != null)
        {
            bool isValid = (gameManager.Selected.validMoves != null && gameManager.Selected.validMoves.Contains(targetTile.Tile)) ||
                           (gameManager.Selected.validAttacks != null && ChessTableTile.IsTileAttackable(gameManager.Selected.validAttacks, targetTile.Tile));

            if (isValid)
            {
                StartCoroutine(PerformValidMove(targetTile));
                return;
            }
        }
        StartCoroutine(PerformInvalidMove());
    }

    private IEnumerator PerformValidMove(VisualChessTableTile targetTile)
    {
        // 1. Taşımayı hemen durdur ki Update() artık taşı karakterin önünde tutmasın
        VisualChessPiece pieceToMove = carriedPiece;
        currentState = State.IDLE; 

        if (animator != null) animator.SetBool("Push", true);

        // 2. Taşı hedef karenin tam üzerine (snap) yumuşakça bırak
        Vector3 targetPos = targetTile.transform.position;
        targetPos.y = carriedPieceOriginalPosition.y; // Y yüksekliğini koru

        float snapTime = 0.3f;
        float elapsed = 0f;
        Vector3 startPos = pieceToMove.transform.position;

        while (elapsed < snapTime)
        {
            elapsed += Time.deltaTime;
            pieceToMove.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / snapTime);
            yield return null;
        }
        pieceToMove.transform.position = targetPos;

        // 3. Satranç motoruna hamleyi yaptır (Taş artık yerinde)
        gameManager.SelectTile(targetTile);

        yield return new WaitForSeconds(1.0f);
        if (animator != null) animator.SetBool("Push", false);

        ClearCarryingState();
        OnPieceDropped?.Invoke();
    }

    private IEnumerator PerformInvalidMove()
    {
        Vector3 startPos = carriedPiece.transform.position;
        float elapsedTime = 0f;
        while (elapsedTime < returnDuration)
        {
            elapsedTime += Time.deltaTime;
            carriedPiece.transform.position = Vector3.Lerp(startPos, carriedPieceOriginalPosition, elapsedTime / returnDuration);
            yield return null;
        }
        carriedPiece.transform.position = carriedPieceOriginalPosition;
        
        // Deselect in the engine to cancel the move attempt
        gameManager.Deselect();
        
        ClearCarryingState();
    }

    private VisualChessTableTile FindNearestTile()
    {
        if (carriedPiece == null) return null;

        VisualChessTableTile nearest = null;
        float minDist = float.MaxValue;

        // Karakterin değil, TAŞIN pozisyonunu baz alıyoruz
        Vector3 piecePos = carriedPiece.transform.position;

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                VisualChessTableTile tile = gameManager.visualTable.VisualTiles[x][y];
                float d = Vector3.Distance(piecePos, tile.transform.position);
                if (d < minDist) { minDist = d; nearest = tile; }
            }
        }
        return nearest;
    }

    private void ClearCarryingState()
    {
        carriedPiece = null;
        currentState = State.IDLE;
        
        // Reset button interactability based on current trigger state
        if (selectButton != null) 
            selectButton.interactable = (currentTriggerTile != null);
    }
}
