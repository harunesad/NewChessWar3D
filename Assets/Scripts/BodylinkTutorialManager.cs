using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using ChessEngine.Game;
using DG.Tweening;

public class BodylinkTutorialManager : MonoBehaviour
{
    public enum TutorialState { None, MoveAvatar, GoToPawnAndOpenHands, GrabPiece, MoveWhileHolding, DropPiece, Finish }
    
    [Header("Settings")]
    public TutorialState currentState = TutorialState.None;
    public float movementThreshold = 0.5f; // Daha kolay algılansın
    public float targetDistanceThreshold = 2.5f; // Daha uzaktan algılansın

    [Header("UI References")]
    private GameObject tutorialCanvas;
    private TextMeshProUGUI tutorialText;

    [Header("Visual References")]
    [SerializeField] private GameObject openHandVisual;
    [SerializeField] private GameObject closedHandVisual;

    private ChessGameManager gameManager;
    private BodylinkHumanoidAvatar avatar;
    private BodylinkTwoHandInteractor interactor;
    
    private Vector3 startPos;
    private VisualChessPiece targetPawn;
    private ChessEngine.TileIndex startTileIndex; // Piyonun başladığı mantıksal kare
    private float dropTimer = 0f;

    public static bool IsTutorialActive = false;

    void Start()
    {
        // Taşların yüklenmesi için 1 saniye bekle
        if (IsTutorialActive) StartCoroutine(DelayedStart());
    }

    IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(1f);
        StartTutorial();
    }

    public void StartTutorial()
    {
        IsTutorialActive = true;
        currentState = TutorialState.MoveAvatar;
        
        gameManager = FindAnyObjectByType<ChessGameManager>();
        avatar = FindAnyObjectByType<BodylinkHumanoidAvatar>();
        interactor = FindAnyObjectByType<BodylinkTwoHandInteractor>();
        
        startPos = avatar.transform.position;

        CreateTutorialUI();
        PrepareBoard();
        UpdateHandVisuals(false, false);
        
        Debug.Log("Tutorial Started!");
    }

    private void CreateTutorialUI()
    {
        // Create Canvas
        tutorialCanvas = new GameObject("TutorialCanvas");
        Canvas c = tutorialCanvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        tutorialCanvas.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // Create Text
        GameObject textObj = new GameObject("TutorialText");
        textObj.transform.SetParent(tutorialCanvas.transform);
        
        tutorialText = textObj.AddComponent<TextMeshProUGUI>();
        tutorialText.alignment = TextAlignmentOptions.Center;
        tutorialText.fontSize = 45;
        tutorialText.color = Color.yellow; // Dikkat çekmesi için sarı
        tutorialText.fontStyle = FontStyles.Bold;
        tutorialText.raycastTarget = false;
        
        // Outline for readability
        tutorialText.outlineWidth = 0.25f;
        tutorialText.outlineColor = Color.black;
        
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.7f);
        rt.anchorMax = new Vector2(1, 0.9f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 0);
    }

    private void PrepareBoard()
    {
        VisualChessPiece[] allPieces = FindObjectsByType<VisualChessPiece>(FindObjectsSortMode.None);
        bool targetFound = false;

        foreach (VisualChessPiece p in allPieces)
        {
            // Paket kodundaki hatayı engellemek için:
            var rotateScript = p.GetComponent<RotateChessPieceByColor>();
            if (p.Piece == null) {
                if (rotateScript != null) rotateScript.enabled = false;
                HidePiece(p);
                continue; 
            } else {
                if (rotateScript != null) rotateScript.enabled = true;
            }

            // Parça türü ve rengi FEN'den çok kesin olarak belirle
            string fen = p.Piece.GetFENIdentifier().ToLower();
            bool isPawn = fen == "p";
            bool isKing = fen == "k";
            bool isWhite = p.Piece.Color.ToString().ToLower() == "white";

            // Siyah taşlara dokunma, oldukları gibi kalsınlar
            if (!isWhite) continue;

            // Beyaz şaha dokunma
            if (isKing) continue;

            // Eğer beyaz piyonsa, sadece İLK bulduğunu hedef seç ve bırak
            if (isPawn)
            {
                if (!targetFound)
                {
                    targetPawn = p;
                    targetFound = true;
                    Debug.Log($"<color=yellow>[Tutorial] UNIQUE Target Pawn Found: {p.name} at {p.transform.position}</color>");
                }
                else
                {
                    HidePiece(p); // Fazla beyaz piyonları sil
                }
            }
            else
            {
                // Beyazın piyon ve şah dışındaki tüm taşlarını (Vezir, Kale, At, Fil) sil
                HidePiece(p);
            }
        }
    }

    private void HidePiece(VisualChessPiece p)
    {
        if (p.Piece != null && gameManager.ChessInstance != null)
        {
            // Satranç motorunun hafızasından bu taşı tamamen SİL
            gameManager.ChessInstance.Table.DestroyPiece(p.Piece);
        }

        // Görsel objeyi de yok et
        Destroy(p.gameObject);
    }

    void Update()
    {
        if (!IsTutorialActive || avatar == null || interactor == null) return;

        bool showOpen = false;
        bool showClosed = false;

        switch (currentState)
        {
            case TutorialState.MoveAvatar:
                tutorialText.text = "MOVE LEFT, RIGHT, FORWARD AND BACKWARD TO CALIBRATE";
                bool movedX = Mathf.Abs(avatar.transform.position.x - startPos.x) > movementThreshold;
                bool movedZ = Mathf.Abs(avatar.transform.position.z - startPos.z) > movementThreshold;
                if (movedX && movedZ) currentState = TutorialState.GoToPawnAndOpenHands;
                break;

            case TutorialState.GoToPawnAndOpenHands:
                if (targetPawn == null) {
                    tutorialText.text = "WAITING FOR CHESS PIECES...";
                    PrepareBoard();
                    break;
                }

                // Eğer piyonu çoktan tuttuysan direkt harekete geç
                if (gameManager.Selected.visualPiece == targetPawn) {
                    startTileIndex = targetPawn.Piece.TileIndex;
                    currentState = TutorialState.MoveWhileHolding;
                    break;
                }

                // 2D Mesafe kontrolü (Kuş bakışı mesafe - En güveniliri)
                float distToPawn = Vector2.Distance(
                    new Vector2(avatar.transform.position.x, avatar.transform.position.z),
                    new Vector2(targetPawn.transform.position.x, targetPawn.transform.position.z)
                );

                bool isClose = (distToPawn < targetDistanceThreshold); // Unity Inspector'dan ayarlanabilir
                bool handsOpen = (interactor.GetHandDistance() > interactor.releaseThreshold * 0.7f);

                if (!isClose) {
                    tutorialText.text = "GO TO THE WHITE PAWN";
                } else {
                    if (!handsOpen) {
                        tutorialText.text = "OPEN YOUR HANDS WIDE";
                        showOpen = true;
                    } else {
                        tutorialText.text = "CLOSE YOUR HANDS TO GRAB THE PIECE";
                        showClosed = true;
                        // Eğer hem yakındaysa hem de ellerini açmışsa artık tutma aşamasına geçebiliriz
                        currentState = TutorialState.GrabPiece;
                    }
                }
                break;

            case TutorialState.GrabPiece:
                // Burada da eğer piyonun yanından uzaklaşırsa başa döndürelim
                float distToPawnGrab = Vector2.Distance(
                    new Vector2(avatar.transform.position.x, avatar.transform.position.z),
                    new Vector2(targetPawn.transform.position.x, targetPawn.transform.position.z)
                );

                if (distToPawnGrab >= targetDistanceThreshold && gameManager.Selected.visualPiece == null) {
                    currentState = TutorialState.GoToPawnAndOpenHands;
                    break;
                }

                tutorialText.text = "CLOSE YOUR HANDS TO GRAB THE PIECE";
                showClosed = true;
                if (gameManager.Selected.visualPiece == targetPawn) {
                    startTileIndex = targetPawn.Piece.TileIndex;
                    currentState = TutorialState.MoveWhileHolding;
                }
                break;

            case TutorialState.MoveWhileHolding:
                tutorialText.text = "MOVE TO A DIFFERENT HIGHLIGHTED SQUARE";
                showClosed = true;

                // Check if avatar is hovering over a valid destination tile
                bool isHoveringValidTile = false;
                Ray ray = new Ray(avatar.transform.position + Vector3.up * 1f, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, 5f))
                {
                    VisualChessTableTile tile = hit.collider.GetComponent<VisualChessTableTile>();
                    if (tile != null)
                    {
                        var sel = gameManager.Selected;
                        if (sel.validMoves != null)
                        {
                            foreach (var m in sel.validMoves)
                            {
                                if (gameManager.visualTable.GetVisualTile(m) == tile) { isHoveringValidTile = true; break; }
                            }
                        }
                        if (!isHoveringValidTile && sel.validAttacks != null)
                        {
                            foreach (var m in sel.validAttacks)
                            {
                                if (gameManager.visualTable.GetVisualTile(m.attackTile) == tile) { isHoveringValidTile = true; break; }
                            }
                        }
                    }
                }

                if (isHoveringValidTile) {
                    currentState = TutorialState.DropPiece;
                }

                // If they drop the piece prematurely, send them back to GrabPiece
                if (gameManager.Selected.visualPiece == null) {
                    currentState = TutorialState.GrabPiece;
                }
                break;

            case TutorialState.DropPiece:
                tutorialText.text = "DROP THE PIECE ON A NEW SQUARE";
                showOpen = true;

                // Verify they are still hovering over a valid tile
                bool isStillHoveringValid = false;
                Ray dropRay = new Ray(avatar.transform.position + Vector3.up * 1f, Vector3.down);
                if (Physics.Raycast(dropRay, out RaycastHit dropHit, 5f))
                {
                    VisualChessTableTile tile = dropHit.collider.GetComponent<VisualChessTableTile>();
                    if (tile != null)
                    {
                        var sel = gameManager.Selected;
                        if (sel.validMoves != null)
                        {
                            foreach (var m in sel.validMoves)
                            {
                                if (gameManager.visualTable.GetVisualTile(m) == tile) { isStillHoveringValid = true; break; }
                            }
                        }
                        if (!isStillHoveringValid && sel.validAttacks != null)
                        {
                            foreach (var m in sel.validAttacks)
                            {
                                if (gameManager.visualTable.GetVisualTile(m.attackTile) == tile) { isStillHoveringValid = true; break; }
                            }
                        }
                    }
                }

                // If they hover away while still holding, prompt them to move again
                if (!isStillHoveringValid && gameManager.Selected.visualPiece != null) {
                    currentState = TutorialState.MoveWhileHolding;
                }

                // Once they drop the piece (selected visual piece becomes null)
                if (gameManager.Selected.visualPiece == null) {
                    // Check if it landed on a new tile
                    if (!targetPawn.Piece.TileIndex.Equals(startTileIndex)) {
                        currentState = TutorialState.Finish;
                    } else {
                        // Returned to original tile or cancelled, retry grab
                        currentState = TutorialState.GrabPiece;
                    }
                }
                break;

            case TutorialState.Finish:
                tutorialText.text = "TUTORIAL COMPLETE! ENJOY YOUR ADVENTURE.";
                
                // Başarıyı kaydet
                if (PlayerPrefs.GetInt("TutorialDone") == 0) {
                    PlayerPrefs.SetInt("TutorialDone", 1);
                    PlayerPrefs.Save();
                }

                // 3 saniye bekle sonra ana menüye dön
                dropTimer += Time.deltaTime;
                if (dropTimer > 3.0f) {
                    IsTutorialActive = false;
                    SceneManager.LoadScene(0);
                }
                break;
        }

        UpdateHandVisuals(showOpen, showClosed);
    }

    private void UpdateHandVisuals(bool showOpen, bool showClosed)
    {
        if (openHandVisual != null)
        {
            bool wasActive = openHandVisual.activeSelf;
            openHandVisual.SetActive(showOpen);
            if (showOpen && !wasActive)
            {
                openHandVisual.transform.localScale = Vector3.one;
                openHandVisual.transform.DOKill();
                openHandVisual.transform.DOScale(1.15f, 0.4f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
            else if (!showOpen && wasActive)
            {
                openHandVisual.transform.DOKill();
            }
        }

        if (closedHandVisual != null)
        {
            bool wasActive = closedHandVisual.activeSelf;
            closedHandVisual.SetActive(showClosed);
            if (showClosed && !wasActive)
            {
                closedHandVisual.transform.localScale = Vector3.one;
                closedHandVisual.transform.DOKill();
                closedHandVisual.transform.DOScale(1.15f, 0.4f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
            else if (!showClosed && wasActive)
            {
                closedHandVisual.transform.DOKill();
            }
        }
    }

    private void OnDestroy()
    {
        if (openHandVisual != null) openHandVisual.transform.DOKill();
        if (closedHandVisual != null) closedHandVisual.transform.DOKill();
    }
}
