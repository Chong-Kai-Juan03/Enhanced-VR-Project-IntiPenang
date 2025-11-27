using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewSites : MonoBehaviour
{
    [SerializeField] private int siteToLoad = 0;

    public int GetSiteToload()
    {
        return siteToLoad;
    }
}
