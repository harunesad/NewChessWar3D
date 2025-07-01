using System;
using UnityEngine;
using UnityEngine.Events;
using ChessEngine.Game.Events;

namespace ChessEngine.Game
{
    /// <summary>
    /// A component that can be found on any chess piece.
    /// Use the generic IsPiece() to check what type of chess piece it is.
    /// </summary>
    /// Author: Intuitive Gaming Solutions
    public class VisualChessPiece : MonoBehaviour
    {
        // MoveUnityEvent.
        /// <summary>
        /// Arg0: MoveInfo      - The MoveInfo about the move.
        /// </summary>
        [Serializable]
        public class MoveUnityEvent : UnityEvent<MoveInfo> { }

        // ChessPiece.
        #region Editor Serialized Fields
        [Header("Settings - Positioning")]
        [Tooltip("An offset to apply to this pieces' position.")]
        public Vector3 offset;

        [Header("Settings - Materials")]
        [Tooltip("(Optional) An editor set reference to the Renderer for this chess piece.")]
        [SerializeField] Renderer m_RendererOverride;
        [Tooltip("An editor set reference to the material of this piece when it's on the white team.")]
        [SerializeField] Material m_WhiteMaterial = null;
        [Tooltip("An editor set reference to the material of this piece when it's on the black team.")]
        [SerializeField] Material m_BlackMaterial = null;
        #endregion
        #region Editor Serialized Events
        [Header("Events")]
        [Tooltip("An event that is invoked when this chess piece is moved.\n\nArg0: MoveInfo - The MoveInfo about the move.")]
        public MoveUnityEvent Moved;
        [Tooltip("An event that is invoked when this chess piece is captured.\n\nArg0: MoveInfo - The MoveInfo about the move the piece was captured on.")]
        public MoveUnityEvent Captured;
        [Tooltip("An event that is invoked when the visual piece is initialized.")]
        public UnityEvent Initialized;
        [Tooltip("An event that is invoked just before the visual piece is destroyed.")]
        public UnityEvent Destroyed;

        [Header("Events - Transformation")]
        [Tooltip("An event that is invoked after the position of the visual chess piece is updated.\n\nArg0: VisualChessPiece - the visual chess piece involved in the event.")]
        public VisualChessPieceUnityEvent PositionUpdated;
        [Tooltip("An event that is invoked after the rotation of the visual chess piece is updated.\n\nArg0: VisualChessPiece - the visual chess piece involved in the event.")]
        public VisualChessPieceUnityEvent RotationUpdated;
        [Tooltip("An event that is invoked after the visuals (material, etc) of the visual chess piece is updated.\n\nArg0: VisualChessPiece - the visual chess piece involved in the event.\n\nArg0: VisualChessPiece - the visual chess piece involved in the event.")]
        public VisualChessPieceUnityEvent VisualsUpdated;
        #endregion

        #region Public Properties
        /// <summary>A reference to the ChessPiece this component is responsible for visualizing.</summary>
        public ChessPiece Piece { get; private set; }
        /// <summary>A reference to the VisualChessTable this piece belongs to.</summary>
        public VisualChessTable VisualTable { get; private set; }
        /// <summary>The Renderer associated with this chess piece.</summary>
        public Renderer Renderer { get; private set; }
        /// <summary>The default 'localRotation' for this visual chess piece, set in Start(), can be overridden manually.</summary>
        public Quaternion DefaultLocalRotation { get; set; }
        #endregion

        // Unity callback(s).
        #region Unity Callbacks
        void Awake()
        {
            // Find Renderer refrence if no override set.
            if (m_RendererOverride == null)
            {
                Renderer = GetComponentInChildren<Renderer>();
            }
            else { Renderer = m_RendererOverride; }
        }

        void Start()
        {
            // Store default local rotation.
            DefaultLocalRotation = transform.localRotation;
        }

        void OnDestroy()
        {
            // Unsubscribe from piece events.
            UnsubscribeFromPieceEvents();

            // Invoke the 'Destroyed' Unity event.
            Destroyed?.Invoke();
        }
        #endregion

        // Public method(s).
        #region Initialization
        /// <summary>
        /// Initializes the visualization for the chess piece pPiece on the given table, pTable.
        /// </summary>
        /// <param name="pTable">The table this chess piece belongs to.</param>
        /// <param name="pPiece">The ChessPiece this component visualizes..</param>
        public void Initialize(VisualChessTable pTable, ChessPiece pPiece)
        {
            VisualTable = pTable;
            Piece = pPiece;

            // Initialize renderer related stuff.
            UpdateVisuals();

            // Update initial position for the chess piece.
            UpdatePosition();

            // Subscribe to 'Piece' events.
            SubscribeToPieceEvents();

            // Invoke the 'Initialized' event.
            Initialized?.Invoke();
        }
        #endregion
        #region Rendering
        /// <summary>Updates the visuals (like the relevant Renderer's material if there is one) for the visual chess piece and invokes the 'VisualsUpdated' Unity event.</summary>
        public void UpdateVisuals()
        {
            if (Renderer != null)
            {
                // Set piece color/team.
                if (Piece.Color == ChessColor.White)
                {
                    Renderer.material = m_WhiteMaterial;
                }
                else { Renderer.material = m_BlackMaterial; }
            }

            // Invoke the 'VisualsUpdated' Unity event.
            VisualsUpdated?.Invoke(this);
        }
        #endregion
        #region Positioning
        /// <summary>Positions the chess piece appropriately on the chess table.</summary>
        public void UpdatePosition()
        {
            // Set the local position of the piece.
            transform.localPosition = VisualTable.GetVisualTile(Piece.Tile).GetLocalPosition(VisualTable) + offset;

            // Invoke the 'PositionUpdated' Unity event.
            PositionUpdated?.Invoke(this);
        }

        /// <summary>Resets the local rotation of the chess piece to 'DefaultLocalRotation'.</summary>
        public void ResetRotation()
        {
            // Set the local rotation of the piece to the default.
            transform.localRotation = DefaultLocalRotation;

            // Invoke the 'RotationUpdated' Unity event.
            RotationUpdated?.Invoke(this);
        }
        #endregion
        #region Generic Methods
        /// <summary>
        /// Returns true if the underlying ChessPiece is of the same type as the specified type, otherwise false.
        /// </summary>
        /// <typeparam name="T">The type to compare against the underlying ChessPiece.</typeparam>
        /// <returns>true if the underlying ChessPiece is of the same type as the specified type, otherwise false.</returns>
        public bool IsPiece<T>() where T : ChessPiece { return Piece.GetType() == typeof(T); }

        /// <summary>Returns 'Piece' as T. (This method performs no type-validity checks.)</summary>
        /// <typeparam name="T">The type of the underlying ChessPiece</typeparam>
        /// <returns>'Piece' as T.</returns>
        public T GetPiece<T>() where T : ChessPiece { return Piece as T; }
        #endregion

        // Private method(s).
        #region Piece Event Subscription & Unsubscription
        /// <summary>Subscribes to the 'Piece' reference events.</summary>
        void SubscribeToPieceEvents()
        {
            // Ensure 'Piece' reference is valid.
            if (Piece != null)
            {
                Piece.Captured += OnCaptured;
                Piece.Moved += OnMoved;
                if (Piece is Rook rook)
                    rook.Castled += OnRookCastled;
            }
            else { Debug.LogWarning("Attempted to 'VisualChessPiece.SubscribeToPieceEvents()' while 'Piece' referenced is null.", gameObject); }
        }

        /// <summary>Unsubscribes from the 'Piece' reference events.</summary>
        void UnsubscribeFromPieceEvents()
        {
            if (Piece != null)
            {
                Piece.Captured -= OnCaptured;
                Piece.Moved -= OnMoved;
                if (Piece is Rook rook)
                    rook.Castled -= OnRookCastled;
            }
        }
        #endregion

        // Private callback(s).
        #region Piece Event Callbacks
        /// <summary>Invoked whenever the chess piece is moved.</summary>
        /// <param name="pMoveInfo"></param>
        void OnMoved(MoveInfo pMoveInfo)
        {
            // Update the pieces position.
            UpdatePosition();

            // Invoke the relevant Unity event.
            Moved?.Invoke(pMoveInfo);
        }

        /// <summary>Invoked when the chess piece is captured.</summary>
        /// <param name="pMoveInfo">Information about the move that led to the capture.</param>
        void OnCaptured(MoveInfo pMoveInfo)
        {
            // Invoke the relevant Unity event.
            Captured?.Invoke(pMoveInfo);
        }

        /// <summary>Invoked right after a rook piece is involved in a castle move.</summary>
        /// <param name="pKing">The king involved in the castle with this rook piece.</param>
        /// <param name="pPreCastleRookTile">The TileIndex of the rook before castling.</param>
        /// <param name="pPostCastleRookTile">The TileIndex of the rook after castling.</param>
        void OnRookCastled(ChessPiece pKing, TileIndex pPreCastleRookTile, TileIndex pPostCastleRookTile)
        {
            // Update the pieces position.
            UpdatePosition();
        }
        #endregion
    }
}
