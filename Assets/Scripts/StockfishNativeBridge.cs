using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class StockfishNativeBridge : MonoBehaviour
{
    // Kütüphane adýný belirtiyoruz. "libstockfish.so" dosyasýndaki "stockfish" kýsmý.
    private const string LibName = "stockfish";

    // C++ tarafýndaki GetBestMove fonksiyonunun bildirimi.
    // DllImport, Unity'ye bu fonksiyonun native bir kütüphanede olduðunu söyler.
    // CharSet = CharSet.Ansi, C++'taki const char* ile uyumluluk için.
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern string GetBestMove(string fen, int movetime_ms);

    // Ýsteðe baðlý: Stockfish'in baþlatýlmasý veya temel ayarlar için
    // Eðer native_plugin.cpp'de bir Init veya UCI komutu gönderen bir fonksiyonunuz varsa
    // [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    // public static extern void InitStockfish();
    // Vb.
}
