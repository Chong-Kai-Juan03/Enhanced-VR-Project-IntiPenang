using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HideAllHotspot : MonoBehaviour
{
    [Header("Hotspot Objects (with BoxCollider)")]
    [Tooltip("List of GameObjects to hide/show during auto tour.")]
    public GameObject[] hotspotObjects;

    /// <summary>
    /// Hide all hotspots (SetActive = false)
    /// </summary>
    /// 

    public void HideHotspots()
    {
        foreach (var obj in hotspotObjects)
        {
            if (obj != null && obj.GetComponent<BoxCollider>() != null)
            {
                obj.SetActive(false);
            }
        }
        Debug.Log("[HideAllHotspot] All hotspot objects hidden.");
    }

    /// <summary>
    /// Show all hotspots again (SetActive = true)
    /// </summary>
    public void ShowHotspots()
    {
        foreach (var obj in hotspotObjects)
        {
            if (obj != null && obj.GetComponent<BoxCollider>() != null)
            {
                obj.SetActive(true);
            }
        }
        Debug.Log("[HideAllHotspot] All hotspot objects shown.");
    }
}
