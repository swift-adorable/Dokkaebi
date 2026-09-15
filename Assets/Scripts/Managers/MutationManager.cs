using UnityEngine;

public class MutationManager : MonoBehaviour
{
    public static MutationManager Instance;

    public bool HasDoubleShot { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public void OpenMutationSelection()
    {
        Debug.Log("===== LEVEL UP =====");

        GainDoubleShot();
    }

    private void GainDoubleShot()
    {
        if (HasDoubleShot)
        {
            Debug.Log("이미 Double Shot 보유");
            return;
        }

        HasDoubleShot = true;

        Debug.Log("Double Shot 획득");
    }
}