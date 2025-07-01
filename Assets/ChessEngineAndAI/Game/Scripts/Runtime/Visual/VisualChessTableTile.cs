using UnityEngine;
using UnityEngine.Events;
using ChessEngine.Game.Events;

namespace ChessEngine.Game
{
    /// <summary>
    /// A VisualChessTableTile component is to be attached to each individual tile that makes up a chess table, each tile holds information about intself like offset.
    /// </summary>
    /// Author: Intuitive Gaming Solutions
    public class VisualChessTableTile : MonoBehaviour
    {
        #region Editor Serialized Fields
        [Header("Settings - Materials")]
        [Tooltip("A reference to the material used for white team tiles.")]
        [SerializeField] Material m_WhiteTeamMaterial = null;
        [Tooltip("A reference to the material used for black team tiles.")]
        [SerializeField] Material m_BlackTeamMaterial = null;

        [Header("Settings - Positioning")]
        [Tooltip("The local space center position of the visual chess table tile.")]
        public Vector3 center;
        [Tooltip("The local space offset to apply when calculating the 'local position' of the tile.")]
        public Vector3 offset;
        [Tooltip("The alignment move to use when positioning the piece on the X axis.")]
        public AxisAlignment alignX = AxisAlignment.Upper;
        [Tooltip("The alignment move to use when positioning the piece on the Y axis.")]
        public AxisAlignment alignY = AxisAlignment.Center;
        [Tooltip("The alignment move to use when positioning the piece on the Z axis.")]
        public AxisAlignment alignZ = AxisAlignment.Lower;

        [Header("Settings - Dimensions")]
        [Tooltip("Is the visual chess table tile 2D?")]
        public bool is2D = false;
        [Tooltip("The length of a tile.")]
        public float tileLength = 1f;
        [Tooltip("The width of a tile.")]
        public float tileWidth = 1f;
        [Tooltip("The height (or thickness) of a tile.")]
        public float tileHeight = 0.25f;
        #endregion
        #region Editor Serialized Event(s)
        [Header("Events")]
        [Tooltip("An event that is invoked when the position of the visual chess table tile is reset.\n\nArg0: VisualChessTableTile - The visual chess table tile involved in the event.")]
        public VisualChessTableTileUnityEvent PositionReset;
        [Tooltip("An event that is invoked whenever the material is reset for the visual chess table tile.\n\nArg0: VisualChessTableTile - The visual chess table tile involved in the event.")]
        public VisualChessTableTileUnityEvent MaterialReset;

        [Header("Events - Highlighting")]
        [Tooltip("An event that is invoked whenever the visual chess table tile is highlighted due to the tile being selected.")]
        public UnityEvent SelectHighlighted;
        [Tooltip("An event that is invoked whenever the visual chess table tile is highlighted due to a valid move to the tile.")]
        public UnityEvent MoveHighlighted;
        [Tooltip("An event that is invoked whenever the visual chess table tile is highlighted due to a valid attack to the tile.")]
        public UnityEvent AttackHighlighted;
        [Tooltip("An event that is invoked whenever the visual chess table tile is unhighlighted for any reason.")]
        public UnityEvent Unhighlighted;
        #endregion
        #region Public Properties
        /// <summary>Returns a reference to the ChessTableTile that this component visualizes.</summary>
        public ChessTableTile Tile { get; private set; }
        /// <summary>A reference to the VisualChessTable this visual tile is placed on.</summary>
        public VisualChessTable VisualChessTable { get; private set; }
        /// <summary>A reference to the active GameManager in the scene, set by VisualChessTable when it instantiates the tile.</summary>
        public ChessGameManager GameManager { get; internal set; }
        /// <summary>A reference to the Renderer associated with this tile.</summary>
        public Renderer Renderer { get; private set; }
        #endregion

        // Unity callback(s).
        #region Unity Callbacks
        void Awake()
        {
            // Find Renderer reference.
            Renderer = GetComponent<Renderer>();
        }
        #endregion

        // Public method(s).
        #region Initialization & Engine Interface
        /// <summary>Initializes the visual tile, positions it at the given index on the relevant VisualChessTable, and stores the relevant ChessTableTile reference.</summary>
        /// <param name="pVisualChessTable">The visual chess table to place the tile on.</param>
        /// <param name="pTile">The chess table to spawn the tile on.</param>
        public void Initialize(VisualChessTable pVisualChessTable, ChessTableTile pTile)
        {
            Tile = pTile;
            VisualChessTable = pVisualChessTable;

            // Reset the position of the tile.
            ResetPosition();
        }

        /// <summary>Resets the position of the visual chess table tile.</summary>
        public void ResetPosition()
        {
            transform.localPosition = GetLocalPosition(VisualChessTable);

            // Invoke the PositionReset Unity event.
            PositionReset?.Invoke(this);
        }

        /// <summary>
        /// Returns the VisualChessPiece on this tile, otherwise null.
        /// Only valid when called after this tile has been placed on the board using 'Initialize'.
        /// </summary>
        /// <returns>VisualChessPiece on this tile, otherwise null.</returns>
        public VisualChessPiece GetVisualPiece()
        {
            // Try to find a chess piece on this tile.
            for (int i = 0; i < VisualChessTable.VisualPieceCount; ++i)
            {
                VisualChessPiece piece = VisualChessTable.GetVisualPieceByIndex(i);
                if (piece != null && piece.Piece.TileIndex == Tile.TileIndex)
                    return piece;
            }

            return null;
        }
        #endregion
        #region Positioning
        /// <summary>Computes and returns the local position of the chess table tile using the provided tile dimensions and offset(s).</summary>
        /// <param name="pChessTable"></param>
        /// <returns>the local position of the chess table tile using the provided tile dimensions and offset(s).</returns>
        public Vector3 GetLocalPosition(VisualChessTable pChessTable)
        {
            Vector3 localPosition;

            float xOffset = 0f;
            float yOffset = 0f;
            float zOffset = 0f;

            // Calculate offsets based on alignment modes.
            switch (alignX)
            {
                case AxisAlignment.Center:
                    xOffset = -(2 * tileWidth) + (tileWidth * Tile.TileIndex.x) + tileWidth / 2;
                    break;
                case AxisAlignment.Upper:
                case AxisAlignment.Lower:
                    xOffset = -(4 * tileWidth) + (tileWidth * Tile.TileIndex.x);
                    break;
            }

            if (is2D)
            {
                switch (alignY)
                {
                    case AxisAlignment.Center:
                        yOffset = -(2 * tileLength) + (tileLength * Tile.TileIndex.y) + tileLength / 2;
                        break;
                    case AxisAlignment.Upper:
                    case AxisAlignment.Lower:
                        yOffset = -(4 * tileLength) + (tileLength * Tile.TileIndex.y);
                        break;
                }
            }
            else
            {
                switch (alignY)
                {
                    case AxisAlignment.Center:
                        yOffset = tileHeight / 2;
                        break;
                    case AxisAlignment.Upper:
                        yOffset = tileHeight;
                        break;
                    case AxisAlignment.Lower:
                        yOffset = 0f;
                        break;
                }
            }

            if (!is2D)
            {
                switch (alignZ)
                {
                    case AxisAlignment.Center:
                        zOffset = -(2 * tileLength) + (tileLength * Tile.TileIndex.y) + tileLength / 2;
                        break;
                    case AxisAlignment.Upper:
                    case AxisAlignment.Lower:
                        zOffset = -(4 * tileLength) + (tileLength * Tile.TileIndex.y);
                        break;
                }
            }

            localPosition = new Vector3(xOffset, yOffset, zOffset) + offset;

            return localPosition;
        }

        /// <summary>
        /// Returns the exact world space position of the center of the visual chess table tile.
        /// Based on the local space 'center' field.
        /// </summary>
        /// <returns>The exact world space position of the center of the visual chess table tile.</returns>
        public Vector3 GetCenterPosition()
        {
            return transform.TransformPoint(center);
        }
        #endregion
        #region Rendering
        /// <summary>Resets the material on a given tile to it's appropriate color.</summary>
        public void ResetMaterial()
        {
            // Reset the material on this tile.
            if (Renderer != null)
                Renderer.material = Tile.Color == ChessColor.White ? m_WhiteTeamMaterial : m_BlackTeamMaterial;

            // Invoke the 'MaterialReset' Unity event.
            MaterialReset?.Invoke(this);
        }
        #endregion
        #region Selection
        /// <summary>Selects this tile on whatever GameManager is associated with this VisualChessTableTile.</summary>
        public void Select()
        {
            // if there is a valid GameManager referenced select this tile.
            if (GameManager != null)
            {
                GameManager.SelectTile(this);
            }
        }
        #endregion

        #region Interal Highlight Callback(s)
        /// <summary>Invoked to notify this component it's been highlighted due to a selection.</summary>
        internal void Internal_OnSelectHighlighted()
        {
            // Invoke the 'SelectHighlighted' Unity event.
            SelectHighlighted?.Invoke();
        }

        /// <summary>Invoked to notify this component it's been highlighted due to a valid move.</summary>
        internal void Internal_OnMoveHighlighted()
        {
            // Invoke the 'MoveHighlighted' Unity event.
            MoveHighlighted?.Invoke();
        }

        /// <summary>Invoked to notify this component it's been highlighted due to a valid attack.</summary>
        internal void Internal_OnAttackHighlighted()
        {
            // Invoke the 'AttackHighlighted' Unity event.
            AttackHighlighted?.Invoke();
        }

        /// <summary>Invoked to notify this component it's no longer highlighted.</summary>
        internal void Internal_OnUnhighlighted()
        {
            // Invoke the 'Unhighlighted' Unity event.
            Unhighlighted?.Invoke();
        }
        #endregion
    }
}
