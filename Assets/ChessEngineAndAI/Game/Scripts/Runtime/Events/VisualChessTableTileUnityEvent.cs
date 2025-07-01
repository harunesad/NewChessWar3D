using System;
using UnityEngine.Events;

namespace ChessEngine.Game.Events
{
    /// <summary>
    /// An event with only a single argument, a reference to a VisualChessTableTile.
    /// </summary>
    /// Author: Intuitive Gaming Solutions
    [Serializable]
    public class VisualChessTableTileUnityEvent : UnityEvent<VisualChessTableTile> { }
}
