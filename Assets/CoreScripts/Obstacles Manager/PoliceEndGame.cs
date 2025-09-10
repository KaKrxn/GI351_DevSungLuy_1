using UnityEngine;

public class PoliceEndGame : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player")) { 
            

        }
    }
}
