using System;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;

namespace BodylinkSDK
{
    public class HeadGestureDetector : BodylinkBaseGestureDetector
    {
        [Header("Gesture Settings")]
        public float movementThreshold = 0.08f;
        public float lookHoldTime = 0.5f;
        public float nodCompletionThreshold = 0.08f; // Added for better nod detection
        public float shakeCompletionThreshold = 0.06f; // Lowered for easier shake detection

        public bool EnableHeadNod = true;
        public bool EnableHeadShake = true;
        public bool EnableLookLeft = true;
        public bool EnableLookRight = true;

        private float playerOneLookTimer = 0f;
        private string playerOneCurrentLook = "";
        private float playerTwoLookTimer = 0f;
        private string playerTwoCurrentLook = "";

        private enum NodState { None, Down, Up, Complete }
        private enum ShakeState { None, Left, Right, Complete }

        private List<(Vector2 pos, float time)> playerOneHeadPositions = new List<(Vector2, float)>(20);
        private List<(Vector2 pos, float time)> playerTwoHeadPositions = new List<(Vector2, float)>(20);

        private NodState playerOneNodState = NodState.None;
        private ShakeState playerOneShakeState = ShakeState.None;
        private NodState playerTwoNodState = NodState.None;
        private ShakeState playerTwoShakeState = ShakeState.None;

        private float playerOneLastY, playerOneLastX;
        private float playerTwoLastY, playerTwoLastX;

        // Added for better gesture tracking
        private float playerOneNodStartY, playerTwoNodStartY;
        private float playerOneShakeStartX, playerTwoShakeStartX;
        private float playerOneShakeStartTime, playerTwoShakeStartTime;

        private bool IsInitialized;

        void Start()
        {
            Bodylink.Instance.OnInitialized += () =>
            {
                IsInitialized = true;
            };
            Bodylink.Instance.OnPlayerOutOfScreen += () =>
            {
                if (Bodylink.Instance.autoRecalibrate == false) return;
                IsInitialized = false;
            };
        }

        void Update()
        {
            ProcessGesture();
        }

        public override void ProcessGesture()
        {
            if (IsInitialized == false || poseLandMarkerResult.poseLandmarks == null || poseLandMarkerResult.poseLandmarks.Count == 0)
                return;

            if (isMultiplayerEnabled && poseLandMarkerResult.poseLandmarks.Count >= 2)
            {
                TriggerGesture(0);

                TriggerGesture(1);
            }
            else
            {
                TriggerGesture(0);
            }
        }

        private void TriggerGesture(int playerIndex)
        {
            var lm = poseLandMarkerResult.poseLandmarks[playerIndex].landmarks;

            float heightRatio = Bodylink.Instance.GetPlayerHeightRatio(playerIndex);
            float adjustedThreshold = movementThreshold * heightRatio;

            if (lm.Count > 7)
            {
                try
                {
                    var nose = players[playerIndex].body2D.nose;
                    var leftEar = players[playerIndex].body2D.leftEar;
                    var rightEar = players[playerIndex].body2D.rightEar;

                    Vector2 currentPos = new Vector2(nose.x, nose.y);
                    float currentTime = Time.time;

                    if (playerIndex == 1)
                    {
                        playerTwoHeadPositions.Add((currentPos, currentTime));
                        if (playerTwoHeadPositions.Count > 20)
                            playerTwoHeadPositions.RemoveAt(0);

                        DetectGestures(playerIndex, adjustedThreshold, leftEar, rightEar, currentPos);
                        // Update last positions AFTER detection
                        playerTwoLastY = currentPos.y;
                        playerTwoLastX = currentPos.x;
                    }
                    else
                    {
                        playerOneHeadPositions.Add((currentPos, currentTime));
                        if (playerOneHeadPositions.Count > 20)
                            playerOneHeadPositions.RemoveAt(0);

                        DetectGestures(playerIndex, adjustedThreshold, leftEar, rightEar, currentPos);
                        // Update last positions AFTER detection
                        playerOneLastY = currentPos.y;
                        playerOneLastX = currentPos.x;
                    }
                }
                catch (Exception) { return; }
            }
        }

        private void DetectGestures(int playerIndex, float adjustedThreshold, NormalizedLandmark leftEar, NormalizedLandmark rightEar, Vector2 currentPos)
        {
            if (playerIndex == 0 && playerOneHeadPositions.Count < 2) return;
            else if (playerIndex == 1 && playerTwoHeadPositions.Count < 2) return;

            float currentY = currentPos.y;
            float currentX = currentPos.x;

            float playerHeightRatio = Bodylink.Instance.GetPlayerHeightRatio(playerIndex);

            // Get previous position for delta calculation
            var positions = playerIndex == 0 ? playerOneHeadPositions : playerTwoHeadPositions;
            if (positions.Count < 2) return;

            var previous = positions[positions.Count - 2];
            float deltaY = currentY - previous.pos.y;
            float deltaX = currentX - previous.pos.x;

            // -----------------------------
            // 🤨 HEAD NOD DETECTION (Improved)
            // -----------------------------
            if (EnableHeadNod)
            {
                if (playerIndex == 0)
                {
                    switch (playerOneNodState)
                    {
                        case NodState.None:
                            if (deltaY < -adjustedThreshold) // Significant downward movement
                            {
                                playerOneNodState = NodState.Down;
                                playerOneNodStartY = currentY;
                            }
                            break;

                        case NodState.Down:
                            if (deltaY > adjustedThreshold) // Start moving up
                            {
                                playerOneNodState = NodState.Up;
                            }
                            else if (currentY > playerOneNodStartY + nodCompletionThreshold) // Returned to neutral
                            {
                                playerOneNodState = NodState.None; // Reset if didn't complete
                            }
                            break;

                        case NodState.Up:
                            if (Mathf.Abs(currentY - playerOneNodStartY) < nodCompletionThreshold) // Returned to start position
                            {
                                float nodIntensity = Mathf.Abs(playerOneNodStartY - currentY) * playerHeightRatio;
                                //OnHeadNod?.Invoke(playerIndex, nodIntensity);
                                OnGestureDetect(playerIndex, "HeadNod", nodIntensity);
                                //Debug.Log("Node delta " + currentY);
                                playerOneNodState = NodState.Complete;
                            }
                            break;

                        case NodState.Complete:
                            // Small delay before allowing next nod
                            if (Mathf.Abs(deltaY) < adjustedThreshold * 0.1f)
                            {
                                playerOneNodState = NodState.None;
                            }
                            break;
                    }
                }
                else
                {
                    switch (playerTwoNodState)
                    {
                        case NodState.None:
                            if (deltaY < -adjustedThreshold)
                            {
                                playerTwoNodState = NodState.Down;
                                playerTwoNodStartY = currentY;
                            }
                            break;

                        case NodState.Down:
                            if (deltaY > adjustedThreshold)
                            {
                                playerTwoNodState = NodState.Up;
                            }
                            else if (currentY > playerTwoNodStartY + nodCompletionThreshold)
                            {
                                playerTwoNodState = NodState.None;
                            }
                            break;

                        case NodState.Up:
                            if (Mathf.Abs(currentY - playerTwoNodStartY) < nodCompletionThreshold)
                            {
                                float nodIntensity = Mathf.Abs(playerTwoNodStartY - currentY) * playerHeightRatio;
                                //OnHeadNod?.Invoke(playerIndex, nodIntensity);
                                OnGestureDetect(playerIndex, "HeadNod", nodIntensity);
                                //Debug.Log("Node delta "+currentY);
                                playerTwoNodState = NodState.Complete;
                            }
                            break;

                        case NodState.Complete:
                            if (Mathf.Abs(deltaY) < adjustedThreshold * 0.1f)
                            {
                                playerTwoNodState = NodState.None;
                            }
                            break;
                    }
                }
            }

            // -----------------------------
            // 🙅 HEAD SHAKE DETECTION (Improved)
            // -----------------------------
            if (EnableHeadShake)
            {
                if (playerIndex == 0)
                {
                    switch (playerOneShakeState)
                    {
                        case ShakeState.None:
                            if (Mathf.Abs(deltaX) > adjustedThreshold)
                            {
                                playerOneShakeState = deltaX > 0 ? ShakeState.Right : ShakeState.Left;
                                playerOneShakeStartX = currentX;
                                playerOneShakeStartTime = Time.time;
                            }
                            break;

                        case ShakeState.Left:
                            // Detect rightward travel from initial left movement
                            if ((currentX - playerOneShakeStartX) > shakeCompletionThreshold * playerHeightRatio)
                            {
                                float shakeIntensity = Mathf.Abs(playerOneShakeStartX - currentX) * playerHeightRatio;
                                OnGestureDetect(playerIndex, "HeadShake", shakeIntensity);
                                playerOneShakeState = ShakeState.Complete;
                            }
                            else if (Time.time - playerOneShakeStartTime > 1f)
                            {
                                playerOneShakeState = ShakeState.None; // Timeout
                            }
                            break;

                        case ShakeState.Right:
                            // Detect leftward travel from initial right movement
                            if ((playerOneShakeStartX - currentX) > shakeCompletionThreshold * playerHeightRatio)
                            {
                                float shakeIntensity = Mathf.Abs(playerOneShakeStartX - currentX) * playerHeightRatio;
                                OnGestureDetect(playerIndex, "HeadShake", shakeIntensity);
                                playerOneShakeState = ShakeState.Complete;
                            }
                            else if (Time.time - playerOneShakeStartTime > 1f)
                            {
                                playerOneShakeState = ShakeState.None;
                            }
                            break;

                        case ShakeState.Complete:
                            if (Mathf.Abs(deltaX) < adjustedThreshold * 0.1f)
                            {
                                playerOneShakeState = ShakeState.None;
                            }
                            break;
                    }
                }
                else
                {
                    switch (playerTwoShakeState)
                    {
                        case ShakeState.None:
                            if (Mathf.Abs(deltaX) > adjustedThreshold)
                            {
                                playerTwoShakeState = deltaX > 0 ? ShakeState.Right : ShakeState.Left;
                                playerTwoShakeStartX = currentX;
                                playerTwoShakeStartTime = Time.time;
                            }
                            break;

                        case ShakeState.Left:
                            if ((currentX - playerTwoShakeStartX) > shakeCompletionThreshold * playerHeightRatio)
                            {
                                float shakeIntensity = Mathf.Abs(playerTwoShakeStartX - currentX) * playerHeightRatio;
                                OnGestureDetect(playerIndex, "HeadShake", shakeIntensity);
                                playerTwoShakeState = ShakeState.Complete;
                            }
                            else if (Time.time - playerTwoShakeStartTime > 1f)
                            {
                                playerTwoShakeState = ShakeState.None;
                            }
                            break;

                        case ShakeState.Right:
                            if ((playerTwoShakeStartX - currentX) > shakeCompletionThreshold * playerHeightRatio)
                            {
                                float shakeIntensity = Mathf.Abs(playerTwoShakeStartX - currentX) * playerHeightRatio;
                                OnGestureDetect(playerIndex, "HeadShake", shakeIntensity);
                                playerTwoShakeState = ShakeState.Complete;
                            }
                            else if (Time.time - playerTwoShakeStartTime > 1f)
                            {
                                playerTwoShakeState = ShakeState.None;
                            }
                            break;

                        case ShakeState.Complete:
                            if (Mathf.Abs(deltaX) < adjustedThreshold * 0.1f)
                            {
                                playerTwoShakeState = ShakeState.None;
                            }
                            break;
                    }
                }
            }

            // -----------------------------
            // 👀 LOOK LEFT / RIGHT DETECTION (Improved - Less sensitive)
            // -----------------------------
            if (EnableLookLeft || EnableLookRight)
            {
                float headWidth = Mathf.Abs(rightEar.x - leftEar.x);
                float headCenter = (rightEar.x + leftEar.x) / 2f;
                float noseOffset = currentPos.x - headCenter;
                float normalizedOffset = noseOffset / (headWidth * 0.5f); // Normalize by head width

                // Only detect look when head is relatively stable (not shaking)
                bool isHeadStable = Mathf.Abs(deltaX) < adjustedThreshold * 0.3f;

                if (isHeadStable && Mathf.Abs(normalizedOffset) > 0.3f) // 30% offset from center
                {
                    if (playerIndex == 0)
                    {
                        playerOneLookTimer += Time.deltaTime;

                        if (playerOneLookTimer >= lookHoldTime)
                        {
                            if (EnableLookRight && normalizedOffset > 0 && playerOneCurrentLook != "right")
                            {
                                //OnLookRight?.Invoke(playerIndex, Mathf.Abs(normalizedOffset) * playerHeightRatio);
                                OnGestureDetect(playerIndex, "LookRight", Mathf.Abs(normalizedOffset) * playerHeightRatio);
                                playerOneCurrentLook = "right";
                            }
                            else if (EnableLookLeft && normalizedOffset < 0 && playerOneCurrentLook != "left")
                            {
                                //OnLookLeft?.Invoke(playerIndex, Mathf.Abs(normalizedOffset) * playerHeightRatio);
                                OnGestureDetect(playerIndex, "LookLeft", Mathf.Abs(normalizedOffset) * playerHeightRatio);
                                playerOneCurrentLook = "left";
                            }
                        }
                    }
                    else
                    {
                        playerTwoLookTimer += Time.deltaTime;

                        if (playerTwoLookTimer >= lookHoldTime)
                        {
                            if (EnableLookRight && normalizedOffset > 0 && playerTwoCurrentLook != "right")
                            {
                                //OnLookRight?.Invoke(playerIndex, Mathf.Abs(normalizedOffset) * playerHeightRatio);
                                OnGestureDetect(playerIndex, "LookRight", Mathf.Abs(normalizedOffset) * playerHeightRatio);
                                playerTwoCurrentLook = "right";
                            }
                            else if (EnableLookLeft && normalizedOffset < 0 && playerTwoCurrentLook != "left")
                            {
                                //OnLookLeft?.Invoke(playerIndex, Mathf.Abs(normalizedOffset) * playerHeightRatio);
                                OnGestureDetect(playerIndex, "LookLeft", Mathf.Abs(normalizedOffset) * playerHeightRatio);
                                playerTwoCurrentLook = "left";
                            }
                        }
                    }
                }
                else
                {
                    // Reset look timer when not looking significantly or head is moving
                    if (playerIndex == 0)
                    {
                        playerOneLookTimer = 0f;
                        playerOneCurrentLook = "";
                    }
                    else
                    {
                        playerTwoLookTimer = 0f;
                        playerTwoCurrentLook = "";
                    }
                }
            }
        }

        // Public method to manually reset states if needed
        public void ResetPlayerStates(int playerIndex)
        {
            if (playerIndex == 0)
            {
                playerOneNodState = NodState.None;
                playerOneShakeState = ShakeState.None;
                playerOneLookTimer = 0f;
                playerOneCurrentLook = "";
            }
            else
            {
                playerTwoNodState = NodState.None;
                playerTwoShakeState = ShakeState.None;
                playerTwoLookTimer = 0f;
                playerTwoCurrentLook = "";
            }
        }
    }
}
