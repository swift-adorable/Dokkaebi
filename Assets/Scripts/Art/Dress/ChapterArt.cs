using UnityEngine;

/// <summary>
/// 【장 아트 뿌리】 (결정 2-127) — 켜질 때 ChapterArtBuilder로 그 장의 모습을 만들어 자식으로 단다(저장 안 함). 에디터에서도 보인다.
/// 「Dokkaebi/Art/장 꾸미기」가 씬에 놓는다. 바닥 그림(Ground)은 씬에 저장된 자식이라 그대로 둔다.
/// </summary>
[ExecuteAlways]
public class ChapterArt : MonoBehaviour
{
    [SerializeField] private int chapter;

    public int Chapter => chapter;
    public int Pieces { get; private set; }

    public void Setup(int value)
    {
        chapter = value;
        Rebuild();
    }

    private void OnEnable() => Rebuild();

    /// <summary>
    /// 【플레이 중에는 꺼질 때 치우지 않는다】 (결정 2-128) — 철수로 구역 씬이 닫힐 때 여기서 Destroy를 걸면,
    /// 그 지연 삭제가 소굴 씬을 읽는 동안 처리되면서 에디터가 멈췄다(MeshRenderer 정리 ↔ 씬 읽기 스레드가
    /// 서로 잠금을 기다림 — 프로세스 샘플로 확인). 플레이 중의 조각은 씬과 함께 사라지고, 메쉬는 씬을 바꿀 때
    /// 안 쓰는 애셋 정리가 거둔다. 에디터(플레이 아님)에서는 예전처럼 바로 치운다.
    /// </summary>
    private void OnDisable()
    {
        if (!Application.isPlaying)
            Clear();
    }

    public void Rebuild()
    {
        Clear();
        ArtKit kit = ArtKit.Load();
        ChapterMap map = ZoneMapTable.Of(chapter);
        if (kit == null || map == null)
            return;

        var builder = new ChapterArtBuilder(map, kit);
        builder.BuildAll();
        Pieces = builder.Emit(transform);
    }

    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (!c.name.StartsWith("~"))
                continue;
            if (c.TryGetComponent(out MeshFilter f) && f.sharedMesh != null)
                DestroyNow(f.sharedMesh);
            DestroyNow(c.gameObject);
        }
    }

    private static void DestroyNow(Object o)
    {
        if (Application.isPlaying)
            Destroy(o);
        else
            DestroyImmediate(o);
    }
}
