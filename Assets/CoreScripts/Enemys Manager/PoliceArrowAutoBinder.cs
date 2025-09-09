using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoliceArrowAutoBinder : MonoBehaviour
{
    public ArrowPointer_Offscreen arrow;
    public string policeTag = "Enemy";
    public float rescanInterval = 0.25f; // ลดดีเลย์

    HashSet<Transform> registered = new HashSet<Transform>();

    IEnumerator Start()
    {
        if (!arrow) arrow = FindObjectOfType<ArrowPointer_Offscreen>();
        while (true)
        {
            var objs = GameObject.FindGameObjectsWithTag(policeTag);
            foreach (var go in objs)
            {
                var t = go.transform;
                if (registered.Add(t)) arrow?.RegisterCandidate(t);
            }
            // ลบตัวหายไป
            registered.RemoveWhere(t =>
            {
                bool gone = !t;
                if (gone) return true;
                bool stillThere = false;
                foreach (var go in objs) if (go && go.transform == t) { stillThere = true; break; }
                if (!stillThere) { arrow?.UnregisterCandidate(t); return true; }
                return false;
            });
            yield return new WaitForSeconds(rescanInterval);
        }
    }
}
