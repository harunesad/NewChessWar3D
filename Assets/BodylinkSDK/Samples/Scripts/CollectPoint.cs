using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

public class CollectPoint : MonoBehaviour
{
    public Text countText;
    public string collectTag = "Finish";
    public string colorName = "";
    public int collectId;
    public int totalCount = 3;
    public bool snapping;
    public float snapLerpSpeed = 12f;
    int currentCount = 0;
    public bool completed = false;

    void OnEnable()
    {
        if (countText != null)
        {
            if (string.IsNullOrEmpty(colorName))
                countText.text = currentCount.ToString() + "/3";
            else
                countText.text = colorName + " : " + currentCount.ToString() + " /3";
        }
    }


    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag(collectTag))
        {
            //first check if grabbed is true or not
            var grabObject = other.gameObject.GetComponent<Collectable>();
            if (grabObject != null && grabObject.id == collectId && grabObject.isGrabbed == false)
            {
                currentCount++;
                if (currentCount == totalCount)
                {
                    completed = true;
                }
                if (countText != null)
                {
                    if (string.IsNullOrEmpty(colorName))
                        countText.text = currentCount.ToString() + "/3";
                    else
                        countText.text = colorName + " : " + currentCount.ToString() + " /3";
                }

                if (snapping)
                {
                    grabObject.GetComponent<Collider2D>().enabled = false;
                    StartCoroutine(SnapToParent(grabObject.transform));
                }
                else
                {
                    grabObject.gameObject.SetActive(false);
                }

            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(collectTag))
        {
            //first check if grabbed is true or not
            var grabObject = other.gameObject.GetComponent<Collectable>();
            if (grabObject != null && grabObject.id == collectId && grabObject.isGrabbed == false)
            {
                currentCount++;
                if (countText != null)
                {
                    if (string.IsNullOrEmpty(colorName))
                        countText.text = currentCount.ToString() + "/3";
                    else
                        countText.text = colorName + " : " + currentCount.ToString() + " /3";
                }

                if (currentCount == 3)
                {
                    completed = true;
                }

                grabObject.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator SnapToParent(Transform target)
    {
        target.SetParent(transform, true);
        while ((target.position - transform.position).sqrMagnitude > 0.0001f)
        {
            target.position = Vector3.Lerp(
                target.position,
                transform.position,
                snapLerpSpeed * Time.deltaTime);
            yield return null;
        }
        target.position = transform.position;
        target.SetParent(transform, true);
    }
}
