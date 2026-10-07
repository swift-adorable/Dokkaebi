using System.Text;
using UnityEngine;

/// <summary>
/// 【임시 표시】 적 머리 위 상태 글자 (결정 2-77) — 「점화 · 중독 3 · 감전 2」. 구슬을 끼웠는데 무엇이 걸렸는지 보이게.
/// 아트 · 레이어 작업 때 아이콘으로 바꾼다. EnemyController가 코드로 붙인다(프리팹을 고치지 않는다).
/// </summary>
public class EnemyStatusTag : MonoBehaviour
{
    private static readonly StatusEffectType[] Shown =
    {
        StatusEffectType.Ignite, StatusEffectType.Poison, StatusEffectType.Corrode,
        StatusEffectType.Chill, StatusEffectType.Freeze, StatusEffectType.Shock,
        StatusEffectType.Paralyze, StatusEffectType.Bleed, StatusEffectType.Congeal
    };

    private readonly StringBuilder text = new();
    private Health health;
    private TextMesh mesh;
    private float timer;

    private void Awake()
    {
        health = GetComponent<Health>();

        var tag = new GameObject("StatusTag");
        tag.transform.SetParent(transform, false);
        tag.transform.localPosition = new Vector3(0f, 2.3f, 0f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh = tag.AddComponent<TextMesh>();
        mesh.font = font;
        mesh.fontSize = 48;
        mesh.characterSize = 0.05f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        tag.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    private void LateUpdate()
    {
        if (mesh == null || health == null)
            return;

        Camera cam = Camera.main;
        if (cam != null)
            mesh.transform.rotation = cam.transform.rotation;

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;
        timer = 0.15f;

        text.Clear();
        Color? first = null;

        foreach (StatusEffectType type in Shown)
        {
            int stacks = health.Status.StacksOf(type);
            if (stacks <= 0)
                continue;

            if (text.Length > 0)
                text.Append(" · ");

            text.Append(StatusEffectNames.Of(type));
            if (stacks > 1)
                text.Append(' ').Append(stacks);

            first ??= StatusTint.Of(type);
        }

        mesh.text = text.ToString();
        mesh.color = first ?? Color.white;
    }
}
