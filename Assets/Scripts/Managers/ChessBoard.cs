using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessBoard : MonoBehaviour
{
    // Satranç tahtasýný temsil eden 8x8'lik bir dizi
    public static ChessPiece[,] board = new ChessPiece[8, 8];
    public static GameObject[,] boardObject = new GameObject[8, 8];
    [SerializeField]
    float squareSize = 1.0f;
    [SerializeField]
    GameObject pawnPrefabWhite, pawnPrefabBlack, rookPrefabWhite, rookPrefabBlack, knightPrefabWhite, knightPrefabBlack, 
        bishopPrefabWhite, bishopPrefabBlack, kingPrefabWhite, kingPrefabBlack;
    public GameObject queenPrefabWhite, queenPrefabBlack;
    public GameObject piecesParent, squareParent, squarePrefab;

    void Start()
    {
        // Tahtayý baþlatmak için bir örnek fonksiyonu çaðýrabilirsiniz
        InitializeBoard();
        DrawBoardGame();
    }
    void Update()
    {
        //DrawBoard();
    }
    private void OnDrawGizmos()
    {
        //DrawBoard();
        DrawnPieces();
    }
    // Tahtayý baþlatan fonksiyon
    public void InitializeBoard()
    {
        // Tahtanýn baþlangýç durumunu ayarlamak için bir örnek
        for (int col = 0; col < 8; col++)
        {
            board[1, col] = CreateChessPiece(new Vector3(col, 0.2f, 1), ChessPiece.Player.White, pawnPrefabWhite, col, 1);
            board[6, col] = CreateChessPiece(new Vector3(col, 0.2f, 6), ChessPiece.Player.Black, pawnPrefabBlack, col, 6);
        }
        SpawnTwoPieces(0, 7, rookPrefabWhite, rookPrefabBlack);
        SpawnTwoPieces(1, 6, knightPrefabWhite, knightPrefabBlack);
        SpawnTwoPieces(2, 5, bishopPrefabWhite, bishopPrefabBlack);
        SpawnQueen();
        SpawnKing();
    }
    void SpawnTwoPieces(int firstCol, int secondCol, GameObject whitePiece, GameObject blackPiece)
    {
        board[0, secondCol] = CreateChessPiece(new Vector3(secondCol, 0.2f, 0), ChessPiece.Player.White, whitePiece, secondCol, 0);
        board[0, firstCol] = CreateChessPiece(new Vector3(firstCol, 0.2f, 0), ChessPiece.Player.White, whitePiece, firstCol, 0);
        board[7, secondCol] = CreateChessPiece(new Vector3(secondCol, 0.2f, 7), ChessPiece.Player.Black, blackPiece, secondCol, 7);
        board[7, firstCol] = CreateChessPiece(new Vector3(firstCol, 0.2f, 7), ChessPiece.Player.Black, blackPiece, firstCol, 7);
    }
    void SpawnQueen()
    {
        board[0, 4] = CreateChessPiece(new Vector3(4, 0.2f, 0), ChessPiece.Player.White, queenPrefabWhite, 4, 0);
        board[7, 4] = CreateChessPiece(new Vector3(4, 0.2f, 7), ChessPiece.Player.Black, queenPrefabBlack, 4, 7);
    }
    void SpawnKing()
    {
        board[0, 3] = CreateChessPiece(new Vector3(3, 0.2f, 0), ChessPiece.Player.White, kingPrefabWhite, 3, 0);
        board[7, 3] = CreateChessPiece(new Vector3(3, 0.2f, 7), ChessPiece.Player.Black, kingPrefabBlack, 3, 7);
    }
    public ChessPiece CreateChessPiece(Vector3 position, ChessPiece.Player pieceType, GameObject piece, int x, int z)
    {
        GameObject pieceObject = Instantiate(piece, position, Quaternion.identity,piecesParent.transform);

        ChessPiece pieceComponent = pieceObject.GetComponent<ChessPiece>();
        pieceComponent.position = new Vector2Int((int)position.x, (int)position.z);
        pieceComponent.x = x;
        pieceComponent.z = z;

        for (int i = 0; i < ServerControl.Instance.chessPlayers.Count; i++)
        {
            ChessControl chessControl = ServerControl.Instance.chessPlayers[i].GetComponent<ChessControl>();
            if (chessControl.player == ChessControl.Player.White && pieceType == ChessPiece.Player.White)
            {
                chessControl.myChessPieces.Add(pieceComponent);
                pieceComponent.chessControl = chessControl;
                ChessControl enemyChessControl = i == 0 ?
                    ServerControl.Instance.chessPlayers[i + 1].GetComponent<ChessControl>() : ServerControl.Instance.chessPlayers[i - 1].GetComponent<ChessControl>();
                pieceComponent.enemyChessControl = enemyChessControl;
                //if (pieceComponent.type == ChessPiece.PieceType.King)
                //{
                //    chessControl.king = pieceComponent;
                //}
            }
            else if (chessControl.player == ChessControl.Player.Black && pieceType == ChessPiece.Player.Black)
            {
                chessControl.myChessPieces.Add(pieceComponent);
                pieceComponent.chessControl = chessControl;
                ChessControl enemyChessControl = i == 0 ?
                    ServerControl.Instance.chessPlayers[i + 1].GetComponent<ChessControl>() : ServerControl.Instance.chessPlayers[i - 1].GetComponent<ChessControl>();
                pieceComponent.enemyChessControl = enemyChessControl;
                //if (pieceComponent.type == ChessPiece.PieceType.King)
                //{
                //    chessControl.king = pieceComponent;
                //}
            }
        }
        return pieceComponent;
    }
    public List<ChessMove> GenerateLegalMoves()
    {
        List<ChessMove> legalMoves = new List<ChessMove>();
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = board[x, y];
                if (piece != null && piece.player == ChessPiece.Player.White)
                {
                    // Beyaz taþýn geçerli hamlelerini al
                    piece.Highlited(false);
                    List<Vector2Int> whiteMoves = GetValidMoves(piece);
                    foreach (Vector2Int move in whiteMoves)
                    {
                        ChessMove chessMove = new ChessMove();
                        chessMove.startX = x;
                        chessMove.startY = y;
                        chessMove.endX = move.x;
                        chessMove.endY = move.y;
                        legalMoves.Add(chessMove);
                    }
                }
                else if (piece != null && piece.player == ChessPiece.Player.Black)
                {
                    // Siyah taþýn geçerli hamlelerini al
                    piece.Highlited(false);
                    List<Vector2Int> blackMoves = GetValidMoves(piece);
                    foreach (Vector2Int move in blackMoves)
                    {
                        ChessMove chessMove = new ChessMove();
                        chessMove.startX = x;
                        chessMove.startY = y;
                        chessMove.endX = move.x;
                        chessMove.endY = move.y;
                        legalMoves.Add(chessMove);
                    }
                }
            }
        }

        return legalMoves;
    }
    private List<Vector2Int> GetValidMoves(ChessPiece piece)
    {
        // Taþa özgü geçerli hamleleri döndürmek için gerekli kodu ekle
        List<Vector2Int> validMoves = new List<Vector2Int>();

        switch (piece.type)
        {
            case ChessPiece.PieceType.Pawn:
                validMoves.AddRange(piece.getValidMoves);
                break;
            case ChessPiece.PieceType.Rook:
                validMoves.AddRange(piece.getValidMoves);
                break;
            case ChessPiece.PieceType.Knight:
                validMoves.AddRange(piece.getValidMoves);
                break;
            case ChessPiece.PieceType.Bishop:
                validMoves.AddRange(piece.getValidMoves);
                break;
            case ChessPiece.PieceType.Queen:
                validMoves.AddRange(piece.getValidMoves);
                break;
            case ChessPiece.PieceType.King:
                validMoves.AddRange(piece.getValidMoves);
                break;
            default:
                // Bilinmeyen taþ türü, boþ liste döndür
                break;
        }
        // Diðer taþ türleri için benzer kodlarý ekleyin

        return validMoves;
    }
    public ChessMove EvaluateBoard()
    {
        // Basit bir deðerlendirme fonksiyonu kullanýlarak tahtayý deðerlendirin
        // Örnek: Materyal avantajý temel alýnabilir
        int whiteMaterial = 0;
        int blackMaterial = 0;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = board[x, y];
                if (piece != null)
                {
                    // Materyal avantajýný hesapla
                    if (piece.player == ChessPiece.Player.White)
                    {
                        whiteMaterial += GetPieceValue(piece);
                    }
                    else
                    {
                        blackMaterial += GetPieceValue(piece);
                    }
                }
            }
        }

        ChessMove bestMove = new ChessMove();
        bestMove.value = whiteMaterial - blackMaterial; // Beyazýn avantajý - siyahýn avantajý

        return bestMove;
    }
    private int GetPieceValue(ChessPiece piece)
    {
        // Taþýn deðerini belirleme, basit bir örnek olarak her taþa sabit bir deðer atama
        switch (piece.type)
        {
            case ChessPiece.PieceType.Pawn:
                return 1;
            case ChessPiece.PieceType.Rook:
                return 5;
            case ChessPiece.PieceType.Knight:
                return 3;
            case ChessPiece.PieceType.Bishop:
                return 3;
            case ChessPiece.PieceType.Queen:
                return 9;
            case ChessPiece.PieceType.King:
                return 100; // Sadece bir örnek, gerçek deðerlendirme fonksiyonlarý çok daha karmaþýktýr
            default:
                return 0;
        }
    }
    public void MakeMove(ChessMove move)
    {
        // Hamleyi tahtada gerçekleþtir
        ChessPiece piece = board[move.startX, move.startY];
        board[move.startX, move.startY] = null;
        board[move.endX, move.endY] = piece;
    }
    public void UndoMove(ChessMove move)
    {
        // Hamleyi geri al
        ChessPiece piece = board[move.endX, move.endY];
        board[move.endX, move.endY] = null;
        board[move.startX, move.startY] = piece;
    }

    // Tahtayý ekrana çizmek için bir fonksiyon
    void DrawBoardGame()
    {
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                GameObject square = Instantiate(squarePrefab, new Vector3(col, 0, row), Quaternion.identity, squareParent.transform);
                boardObject[col, row] = square;
                if ((row + col) % 2 == 0)
                {
                    square.GetComponent<Renderer>().material.color = Color.white;
                }
                else
                {
                    square.GetComponent<Renderer>().material.color = Color.black;
                }
            }
        }
    }
    public void DrawnPieces()
    {
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (board[row, col] != null)
                {
                    float posX = row * squareSize;
                    float posZ = col * squareSize;
                    Vector3 position = new Vector3(posZ, 1, posX);
                    Gizmos.DrawCube(position, new Vector3(squareSize, .1f, squareSize));
                }
            }
        }
    }
}