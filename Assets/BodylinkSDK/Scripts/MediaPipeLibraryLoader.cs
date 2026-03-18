#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;

namespace BodylinkSDK 
{
    /// <summary>
    /// Forces Android to load OpenCV before MediaPipe to resolve DllNotFoundException (Dependency failures)
    /// </summary>
    public static class MediaPipeLibraryLoader 
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LoadLibraries() 
        {
            try 
            {
                Debug.Log("[Bodylink] Attempting to manually load native Android libraries...");
                using (AndroidJavaClass system = new AndroidJavaClass("java.lang.System")) 
                {
                    system.CallStatic("loadLibrary", "opencv_java4");
                    Debug.Log("[Bodylink] Successfully loaded opencv_java4!");
                    
                    system.CallStatic("loadLibrary", "mediapipe_jni");
                    Debug.Log("[Bodylink] Successfully loaded mediapipe_jni!");
                }
            }
            catch (System.Exception e) 
            {
                Debug.LogError("[Bodylink] Failed to manually load native libraries: " + e.Message);
            }
        }
    }
}
#endif
