using UnityEngine;

/// <summary>
/// 역행 비밀 통로 입구 (결정 2-52 · 2-88 · SecretPassageRule). 장 맵이 열릴 때 ZoneMap이 세운다.
/// 가까이 가면 버튼(BunkerStation)이 뜨고, 누르면 이전 장 맵으로 넘어간다.
/// 【표시가 거의 없다】 — 이스터 에그다. 그레이박스에서는 땅에 어두운 얼룩 하나.
/// </summary>
public class SecretPassage : MonoBehaviour
{
    private int chapter;
    private string prompt;

    public string Prompt => prompt;

    /// <summary>이 장 맵에 통로를 세운다 — 입구 구역을 끝냈을 때만.</summary>
    public static SecretPassage SpawnFor(int chapter)
    {
        if (!SecretPassageRule.IsOpen(StoryManager.Progress, chapter))
            return null;

        MapAnchor entrance = SecretPassageRule.EntranceOf(chapter).Value;

        var root = new GameObject($"SecretPassage (Ch{chapter})");
        root.transform.position = entrance.Position;

        GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mark.name = "Mark";
        mark.transform.SetParent(root.transform, false);
        mark.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        mark.transform.localScale = new Vector3(1.4f, 0.01f, 1.4f);
        Destroy(mark.GetComponent<Collider>());

        if (mark.TryGetComponent(out Renderer renderer))
        {
            var color = new Color(0.10f, 0.10f, 0.12f);
            renderer.material.color = color;
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", color);
        }

        var passage = root.AddComponent<SecretPassage>();
        passage.chapter = chapter;
        passage.prompt = SecretPassageRule.PromptOf(entrance);
        root.AddComponent<BunkerStation>().Setup(BunkerStation.Kind.SecretPassage, SecretPassageRule.Radius);
        return passage;
    }

    /// <summary>지나간다 — 처음이면 역행 계열을 드러내고, 이전 장 맵의 출구로 넘어간다.</summary>
    public void Enter()
    {
        MapAnchor? exit = SecretPassageRule.ExitOf(chapter);
        if (!exit.HasValue)
            return;

        PassiveManager passive = PassiveManager.EnsureInstance();
        if (!passive.DiscoveredRegression)
        {
            passive.DiscoveredRegression = true;
            StoryDialogueUI.ShowBanner("역행 — 되짚어 가는 길을 찾았다.", 2.5f);
        }

        SceneFlow.TakePassage(chapter - 1, exit.Value.ZoneId, exit.Value.Position);
    }
}
