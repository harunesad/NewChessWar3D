using ChessEngine.Game;
using ChessEngine.Game.AI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Difficulty : MonoBehaviour
{
    public int difficult;
    bool gameStart = false;
    public Type type = Type.White;
    public Type winType;
    public enum Type
    {
        White,
        Black,
        Draw
    }
    // Singleton örneði
    private static Difficulty _instance;

    // Dýþarýdan eriþilebilir Singleton örneði
    public static Difficulty Instance
    {
        get
        {
            if (_instance == null)
            {
                // Eðer örnek oluþturulmamýþsa, yeni bir örnek oluþtur
                GameObject singletonObject = new GameObject("Difficulty");
                _instance = singletonObject.AddComponent<Difficulty>();
            }

            return _instance;
        }
    }

    // Singleton sýnýfýnýn geri kalaný
    private void Awake()
    {
        // Eðer zaten bir örnek varsa, bu örneði yok et
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            // Örneði ayarla ve sahnede koru
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }
    //private void Update()
    //{
    //    if (!gameStart && SceneManager.GetActiveScene().buildIndex == 1)
    //    {
    //        ChessAIGameManager chessAIGameManager = FindAnyObjectByType<ChessAIGameManager>();
    //        if (type == Type.White)
    //        {
    //            chessAIGameManager.blackAIThinkDepth = difficult;
    //        }
    //        else
    //        {
    //            chessAIGameManager.whiteAIThinkDepth = difficult;
    //        }
    //        Debug.Log(chessAIGameManager.whiteAIThinkDepth + " " + chessAIGameManager.blackAIThinkDepth);
    //        gameStart = true;
    //    }
    //}

    // Singleton sýnýfýnýn geri kalaný buraya eklenir

    public void SingletonMethod()
    {
        // Singleton sýnýfýnýn bir metodu
    }
}
