using UnityEngine;

namespace ChessEngine.Game
{
    /// <summary>
    /// A simple component that orients the visual chess table's Transform
    /// based on the current turn color.
    /// This component must be attached to the same GameObject as
    /// a ChessGameManager component.
    /// </summary>
    /// Author: Intuitive Gaming Solutions
    [RequireComponent(typeof(ChessGameManager))]
    public class OrientBoardByTurn : MonoBehaviour
    {
        #region Editor Serialized Setting(s)
        [Header("Settings")]
        [Tooltip("The direction vector to rotate about.")]
        public Vector3 rotateAxis = Vector3.up;
        [Range(0f, 360f)]
        [Tooltip("The rotation angle about rotateAxis for the board during white team's turn.")]
        public float whiteRotation = 0f;
        [Range(0f, 360f)]
        [Tooltip("The rotation angle about rotateAxis for the board during black team's turn.")]
        public float blackRotation = 180f;
        [Tooltip("Should this component automatically apply the correct orientation to the board when enabled? (if there is a valid game.)")]
        public bool applyOnEnable = true;
        [Tooltip("Should this component automatically restore the cached board state when disabled?")]
        public bool restoreStateOnDisable = true;
        #endregion
        #region Public Properties
        /// <summary>A reference to the ChessGameManager associated with this component.</summary>
        public ChessGameManager Manager { get; private set; }
        /// <summary>The cached visual table position.</summary>
        public Vector3 CachedTablePosition { get; set; }
        /// <summary>The cached visual table rotation.</summary>
        public Quaternion CachedTableRotation { get; set; }
        #endregion

        #region Unity Callback(s)
        void Awake()
        {
            // Find 'Manager' reference.
            Manager = GetComponent<ChessGameManager>();
        }

        void Start()
        {
            // Cache the board state on start.
            CacheState();
        }

        void OnEnable()
        {
            // If there is a valid game going apply the offset.
            if (Manager.ChessInstance != null && Manager.ChessInstance.Table != null && Manager.ChessInstance.Table.PieceCount > 0)
            {
                ApplyColorOrientation(Manager.ChessInstance.turn);
            }

            // Subscribe to Manager event(s).
            Manager.TurnStarted.AddListener(OnTurnStarted);
        }

        void OnDisable()
        {
            // Unsubscribe from Manager event(s).
            if (Manager != null)
            {
                Manager.TurnStarted.RemoveListener(OnTurnStarted);

                // Restore the board state if enabled.
                if (restoreStateOnDisable)
                    RestoreCachedState();
            }
        }
        #endregion

        #region Public Cache Method(s)
        /// <summary>
        /// Caches the current position and rotation of the visual table (if valid).
        /// </summary>
        public void CacheState()
        {
            if (Manager.visualTable != null)
            {
                CachedTablePosition = Manager.visualTable.transform.position;
                CachedTableRotation = Manager.visualTable.transform.rotation;
            }
        }

        /// <summary>
        /// Restores the cached position and rotation to the visual table (if valid).
        /// </summary>
        public void RestoreCachedState()
        {
            if (Manager.visualTable != null)
            {
                Manager.visualTable.transform.SetPositionAndRotation(
                    CachedTablePosition,
                    CachedTableRotation
                );
            }
        }
        #endregion
        #region Public Orientaton Method(s)
        /// <summary>
        /// Applies the orientation as if it is pColor's turn to
        /// the visual chess table.
        /// </summary>
        /// <param name="pColor"></param>
        public void ApplyColorOrientation(ChessColor pColor)
        {
            // If the Manager has a visual table, set its orientation.
            if (Manager.visualTable != null)
            {
                SetFixedRotation(
                    Manager.visualTable.GetCenterPosition(),
                    pColor == ChessColor.White ? whiteRotation : blackRotation
                );
            }
        }

        /// <summary>
        /// Sets the board to a fixed rotation around a given point in space.
        /// </summary>
        /// <param name="pRotateAboutPoint">The point in world space to rotate the board around.</param>
        /// <param name="pRotationAngle">The absolute rotation angle (in degrees) to apply.</param>
        public void SetFixedRotation(Vector3 pRotateAboutPoint, float pRotationAngle)
        {
            if (Manager.visualTable != null)
            {
                // Store the original center position of the visual table.
                Vector3 originalCenterPosition = Manager.visualTable.GetCenterPosition();

                // Set the absolute rotation to a Quaternion around the specified axis.
                Manager.visualTable.transform.rotation = Quaternion.AngleAxis(pRotationAngle, rotateAxis);

                // Adjust the position to ensure the center position matches the original center position.
                Manager.visualTable.transform.position += originalCenterPosition - Manager.visualTable.GetCenterPosition();
            }
        }
        #endregion

        #region Private ChessGameManager Callback(s)
        /// <summary>
        /// Invoked whenever the team of pColor starts their turn
        /// according to the relevant 'Manager' associated with this
        /// component.
        /// </summary>
        /// <param name="pColor"></param>
        void OnTurnStarted(ChessColor pColor)
        {
            ApplyColorOrientation(pColor);
        }
        #endregion
    }
}
