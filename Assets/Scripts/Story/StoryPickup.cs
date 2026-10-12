using UnityEngine;

/// <summary>
/// 기억의 조각을 줍는 자리. (방(榜)은 결정 2-100에서 뺐다) 가까이 가면 저절로 줍는다. (로드맵 3단계)
///
/// 【모양은 임시다】 — 조각은 떠 있는 종잇장(납작한 흰 판).
/// 아트가 들어오면 프리팹으로 바꾼다.
/// </summary>
public class StoryPickup : MonoBehaviour
{
    public enum Kind
    {
        Piece = 0
        // 1은 옛 방(榜) — 결정 2-100에서 뺐다. 다시 쓰지 않는다.
    }

    /// <summary>줍는 거리 (m).</summary>
    public const float Radius = 1.6f;

    private string id;
    private Transform player;

    public static StoryPickup Create(Kind kind, string id, Vector3 position)
    {
        GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shape.name = $"MemoryPiece ({id})";

        Object.Destroy(shape.GetComponent<Collider>());

        shape.transform.position = position + Vector3.up * 0.9f;
        shape.transform.localScale = new Vector3(0.5f, 0.04f, 0.65f);

        var renderer = shape.GetComponent<Renderer>();

        if (renderer != null)
            renderer.material.color = new Color(0.96f, 0.94f, 0.86f);

        var pickup = shape.AddComponent<StoryPickup>();
        pickup.id = id;
        return pickup;
    }

    private void Start()
    {
        var movement = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Exclude);
        player = movement != null ? movement.transform : null;
    }

    private void Update()
    {
        transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);

        if (player == null || StoryDialogueUI.IsShowing)
            return;

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;

        if (offset.magnitude > Radius)
            return;

        StoryManager.ReportPiece(id);

        Destroy(gameObject);
    }
}
