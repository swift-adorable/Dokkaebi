using UnityEngine;

/// <summary>
/// 기억의 조각 · 방(榜)을 줍는 자리. 가까이 가면 저절로 줍는다. (로드맵 3단계)
///
/// 【모양은 임시다】 — 조각은 떠 있는 종잇장(납작한 흰 판), 방은 세워 둔 누런 판.
/// 아트가 들어오면 프리팹으로 바꾼다.
/// </summary>
public class StoryPickup : MonoBehaviour
{
    public enum Kind
    {
        Piece = 0,
        Notice = 1
    }

    /// <summary>줍는 거리 (m).</summary>
    public const float Radius = 1.6f;

    private Kind kind;
    private string id;
    private Transform player;

    public static StoryPickup Create(Kind kind, string id, Vector3 position)
    {
        GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shape.name = kind == Kind.Piece ? $"MemoryPiece ({id})" : $"Notice ({id})";

        Object.Destroy(shape.GetComponent<Collider>());

        shape.transform.position = position + Vector3.up * (kind == Kind.Piece ? 0.9f : 0.75f);
        shape.transform.localScale = kind == Kind.Piece
            ? new Vector3(0.5f, 0.04f, 0.65f)
            : new Vector3(0.9f, 1.2f, 0.08f);

        var renderer = shape.GetComponent<Renderer>();

        if (renderer != null)
            renderer.material.color = kind == Kind.Piece
                ? new Color(0.96f, 0.94f, 0.86f)
                : new Color(0.86f, 0.72f, 0.40f);

        var pickup = shape.AddComponent<StoryPickup>();
        pickup.kind = kind;
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
        if (kind == Kind.Piece)
            transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);

        if (player == null || StoryDialogueUI.IsShowing)
            return;

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;

        if (offset.magnitude > Radius)
            return;

        if (kind == Kind.Piece)
            StoryManager.ReportPiece(id);
        else
            StoryManager.ReportNotice(id);

        Destroy(gameObject);
    }
}
