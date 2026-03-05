namespace BodylinkSDK
{
    /// <summary>
    /// Types of calibration available in Bodylink SDK.
    /// </summary>
    public enum BodylinkCalibrationType
    {
        FullBody, // Full body including head, hands, and feet
        Head, // Head only
        UpperBody, // Head and upper body including hands
        Feet,
        None, // No calibration

    }
    public enum Side
    {
        Left,
        Right
    }

    public enum HandPose
    {
        None,
        Closed_Fist,
        Open_Palm,
        Pointing_Up,
        Thumb_Down,
        Thumb_Up,
        Victory,
        ILoveYou
    }
}
