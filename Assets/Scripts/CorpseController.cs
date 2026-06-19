using UnityEngine;

public class CorpseController : MonoBehaviour
{
    public bool CanAbsorb { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CanAbsorb = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CanAbsorb = false;
        }
    }
}