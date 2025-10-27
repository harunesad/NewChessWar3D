using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialChange : MonoBehaviour
{
    [SerializeField] MeshRenderer newRend;
    public void Change()
    {
        Material firstMaterial = newRend.materials[0];
        GetComponent<MeshRenderer>().material = firstMaterial;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
