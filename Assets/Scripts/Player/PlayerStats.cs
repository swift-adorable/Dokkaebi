using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance;

    public int Level { get; private set; } = 1;
    public int CurrentXP { get; private set; }
    public int RequiredXP { get; private set; } = 10;

    private void Awake()
    {
        Instance = this;
    }

    public void AddXP(int amount)
    {
        CurrentXP += amount;

        Debug.Log($"XP : {CurrentXP}/{RequiredXP}");

        while (CurrentXP >= RequiredXP)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        CurrentXP -= RequiredXP;

        Level++;

        RequiredXP += 5;

        Debug.Log($"LEVEL UP! Lv.{Level}");

        MutationManager.Instance.OpenMutationSelection();
    }
}