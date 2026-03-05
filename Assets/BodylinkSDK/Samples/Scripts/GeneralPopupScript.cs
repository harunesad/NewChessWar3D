using UnityEngine;

public class GeneralPopupScript : MonoBehaviour
{
    public bool autoDeactivate;
    public float timer = 2;
    void OnEnable()
    {
        if (autoDeactivate)
            Invoke(nameof(Deactivate), timer);
    }

    void Deactivate()
    {
        gameObject.SetActive(false);
    }
}
