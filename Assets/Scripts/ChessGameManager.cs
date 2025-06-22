using UnityEngine;
using System.Threading.Tasks; // Asenkron iþlemler için

public class ChessGameManager : MonoBehaviour
{
    // Stockfish'e baþlangýç pozisyonunu ve düþünme süresini buradan ayarlayabilirsiniz
    public string initialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"; // Baþlangýç FEN
    public int moveTimeMs = 1000; // 1 saniye düþünme süresi

    void Start()
    {
        Debug.Log("ChessGameManager started.");
        // Stockfish motorunu baþlatmak için bir metod çaðýrabilirsiniz (eðer native'de init fonksiyonu varsa)
        // StockfishNativeBridge.InitStockfish();

        // En iyi hamleyi asenkron olarak hesapla
        CalculateBestMoveAsync();
    }

    private async void CalculateBestMoveAsync()
    {
        Debug.Log("Calculating best move for FEN: " + initialFen + " with " + moveTimeMs + "ms thinking time...");

        // Native fonksiyon çaðrýsýný bir arka plan iþ parçacýðýnda yapýyoruz.
        // Bu, Unity'nin UI thread'ini bloke etmez ve uygulamanýn donmasýný engeller.
        string bestMove = await Task.Run(() => StockfishNativeBridge.GetBestMove(initialFen, moveTimeMs));

        Debug.Log("Stockfish Best Move: " + bestMove);

        // Burada bestMove'u kullanarak oyun tahtasýný güncelleyebilirsiniz.
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            CalculateBestMoveAsync();
        }
    }
    // Bir UI butonuyla tetiklemek için örnek bir metod
    public void OnCalculateMoveButtonClicked()
    {
        CalculateBestMoveAsync();
    }
}