using UnityEngine;

public class DebugUIManager : MonoBehaviour
{
    [SerializeField] private GameObject debugPanel;

    public void TogglePanel()
    {
        debugPanel.SetActive(!debugPanel.activeSelf);
    }
}