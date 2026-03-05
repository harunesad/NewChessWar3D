using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Components.Containers;

namespace BodylinkSDK
{
    public class BodylinkPlayerIdentityTracker
    {
        private Vector2[] prevCentroid = new Vector2[2];
        private bool[] initialized = new bool[2];

        private int[] stableIndex = new int[2] { 0, 1 };
        private int flipCounter = 0;
        private const int flipThreshold = 5;  // frames to confirm a swap

        // Compute centroid for each detected Mediapipe skeleton
        private Vector2 ComputeCentroid(List<NormalizedLandmark> lm)
        {
            float sumX = 0, sumY = 0;
            int count = lm.Count;

            for (int i = 0; i < count; i++)
            {
                sumX += lm[i].x;
                sumY += lm[i].y;
            }

            return new Vector2(sumX / count, sumY / count);
        }

        // Main function: Call this every frame from Bodylink.
        // Writes identity mapping into the provided output array
        // to avoid exposing mutable internal state.
        public void GetStableIdentity(List<List<NormalizedLandmark>> allPlayers, int[] stableIdentityOutput)
        {
            if (stableIdentityOutput == null || stableIdentityOutput.Length < 2)
            {
                Debug.LogError("GetStableIdentity requires an output array of length 2.");
                return;
            }

            if (allPlayers.Count < 2)
            {
                stableIdentityOutput[0] = 0;
                stableIdentityOutput[1] = -1;
                return; // only one player detected
            }

            Vector2 c0 = ComputeCentroid(allPlayers[0]);
            Vector2 c1 = ComputeCentroid(allPlayers[1]);

            // INITIALIZATION
            if (!initialized[0] || !initialized[1])
            {
                prevCentroid[0] = c0;
                prevCentroid[1] = c1;
                initialized[0] = initialized[1] = true;
                stableIndex[0] = 0;
                stableIndex[1] = 1;
                stableIdentityOutput[0] = stableIndex[0];
                stableIdentityOutput[1] = stableIndex[1];
                return;
            }

            // Compute distance from each current centroid to previous ones
            float d0toPrev0 = Vector2.Distance(c0, prevCentroid[0]);
            float d0toPrev1 = Vector2.Distance(c0, prevCentroid[1]);

            float d1toPrev0 = Vector2.Distance(c1, prevCentroid[0]);
            float d1toPrev1 = Vector2.Distance(c1, prevCentroid[1]);

            // Decide best match:
            bool sameOrder =
                d0toPrev0 + d1toPrev1 <= d0toPrev1 + d1toPrev0;

            if (!sameOrder)
            {
                flipCounter++;
                if (flipCounter > flipThreshold)
                {
                    stableIndex[0] = 1;
                    stableIndex[1] = 0;
                    flipCounter = 0;
                }
            }
            else
            {
                flipCounter = 0;
                stableIndex[0] = 0;
                stableIndex[1] = 1;
            }

            // Update previous centroids
            prevCentroid[0] = stableIndex[0] == 0 ? c0 : c1;
            prevCentroid[1] = stableIndex[1] == 1 ? c1 : c0;
            stableIdentityOutput[0] = stableIndex[0];
            stableIdentityOutput[1] = stableIndex[1];
        }
    }
}
