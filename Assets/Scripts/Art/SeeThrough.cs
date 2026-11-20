using UnityEngine;

/// <summary>
/// 【가려진 주인공 둘레 비쳐 보이기】 (v8 · 2026-11-19 사용자 「캐릭터가 벽이나 어떤 객체 뒤로 가리게 되면 캐릭터 주변만 그 객체를 약간 투명하게」)
/// 매 프레임 주인공 자리를 전역 셰이더 값으로 넣는다. Dokkaebi/SeeThroughLit 를 쓰는 것(3D 집 · 나무 · 소품 그림)이
/// 화면에서 주인공 둘레 원 안 · 주인공보다 앞이면 점 무늬로 비운다. 주인공이 없으면(소굴 · 메뉴) 끈다.
/// </summary>
public class SeeThrough : MonoBehaviour
{
    public const string ShaderName = "Dokkaebi/SeeThroughLit";
    /// <summary>화면 높이 대비 원 반지름.</summary>
    public static float Radius = 0.11f;

    private static readonly int PosId = Shader.PropertyToID("_SeeThroughPos");
    private static readonly int RadiusId = Shader.PropertyToID("_SeeThroughRadius");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("SeeThrough") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        go.AddComponent<SeeThrough>();
    }

    /// <summary>집 · 그림 재질이 쓸 셰이더 — 없으면(빌드에서 빠짐 등) URP Lit.</summary>
    public static Shader LitShader()
    {
        Shader s = Shader.Find(ShaderName);
        return s != null ? s : Shader.Find("Universal Render Pipeline/Lit");
    }

    private void LateUpdate()
    {
        Transform player = QuestSpotDirector.Player;
        if (player == null || SceneFlow.InBunker)
        {
            Shader.SetGlobalVector(PosId, Vector4.zero);
            return;
        }
        Vector3 p = player.position + Vector3.up * 0.4f;
        Shader.SetGlobalVector(PosId, new Vector4(p.x, p.y, p.z, 1f));
        Shader.SetGlobalFloat(RadiusId, Radius);
    }

    private void OnDestroy() => Shader.SetGlobalVector(PosId, Vector4.zero);
}
