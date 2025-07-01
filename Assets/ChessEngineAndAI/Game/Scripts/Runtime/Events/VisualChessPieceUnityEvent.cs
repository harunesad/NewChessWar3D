using System;
using UnityEngine.Events;

namespace ChessEngine.Game.Events
{
    /// <summary>
    /// An event with only a single argument, a reference to a VisualChessPiece.
    /// </summary>
    /// Author: Intuitive Gaming Solutions
    [Serializable]
    public class VisualChessPieceUnityEvent : UnityEvent<VisualChessPiece> { }
}
