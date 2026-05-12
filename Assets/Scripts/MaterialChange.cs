using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialChange : MonoBehaviour
{
    [SerializeField] MeshRenderer newRend;
    public void Change()
    {
        GetComponent<MeshRenderer>().material.color = newRend.material.color;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
