using UnityEditor;
using UnityEngine;

/// <summary>【픽셀 캐릭터 시험】 (결정 2-115) — 층 · 재료 준비, 켜고 끄기, 임시 인형 입히기, 잡귀 하나 세우기.</summary>
public static class PixelArtMenu
{
    private const string MaterialAsset = "Assets/Resources/Art/PixelComposite.mat";
    private const int PreferredLayer = 10;

    [MenuItem("Dokkaebi/Art/픽셀 층 · 재료 준비")]
    public static void Prepare()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        bool found = false;
        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == PixelArtView.LayerName)
                found = true;

        if (!found)
        {
            int slot = -1;
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(PreferredLayer).stringValue))
                slot = PreferredLayer;
            for (int i = 8; slot < 0 && i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    slot = i;
            if (slot < 0)
            {
                Debug.LogError("[PixelArt] 빈 층이 없다.");
                return;
            }
            layers.GetArrayElementAtIndex(slot).stringValue = PixelArtView.LayerName;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"[PixelArt] 층 {slot} = {PixelArtView.LayerName}");
        }

        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialAsset) == null)
        {
            Shader shader = Shader.Find("Dokkaebi/PixelComposite");
            if (shader == null)
            {
                Debug.LogError("[PixelArt] Dokkaebi/PixelComposite 셰이더가 없다.");
                return;
            }
            System.IO.Directory.CreateDirectory("Assets/Resources/Art");
            AssetDatabase.CreateAsset(new Material(shader) { name = "PixelComposite" }, MaterialAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PixelArt] 재료 {MaterialAsset}");
        }
    }

    [MenuItem("Dokkaebi/Art/픽셀 캐릭터 켜기 · 끄기")]
    public static void Toggle()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PixelArt] 플레이 중에만.");
            return;
        }
        PixelArtView.SetEnabled(!PixelArtView.Enabled);
    }

    [MenuItem("Dokkaebi/Art/카메라 가까이 · 원래대로")]
    public static void ToggleZoom()
    {
        CameraFollow.ZoomScale = CameraFollow.ZoomScale < 0.99f ? 1f : 0.55f;
        Debug.Log($"[PixelArt] 카메라 거리 ×{CameraFollow.ZoomScale:0.##}");
    }

    /// <summary>난이도 고르기 창 뒤처럼 시간이 멈춰 있을 때 — 카메라 · 걸음을 보려고 잠시 흐르게 한다(세이브는 건드리지 않는다).</summary>
    [MenuItem("Dokkaebi/Art/시간 흐르게 (시험)")]
    public static void Unpause()
    {
        if (EditorApplication.isPlaying)
            Time.timeScale = 1f;
    }

    [MenuItem("Dokkaebi/Art/화면 2배로 찍기")]
    public static void CaptureLarge()
    {
        if (!EditorApplication.isPlaying)
            return;
        System.IO.Directory.CreateDirectory("Temp/screens");
        string path = $"Temp/screens/art_{System.DateTime.Now:HHmmss}.png";
        ScreenCapture.CaptureScreenshot(path, 2);
        Debug.Log($"[Capture] {path}");
    }

    [MenuItem("Dokkaebi/Art/임시 인형 입히기")]
    public static void DressFigures()
    {
        if (!EditorApplication.isPlaying)
            return;

        foreach (PlayerMovement p in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude))
            PlaceholderFigure.Build(p.gameObject, PlaceholderFigure.Kind.Player);
        foreach (EnemyIdentity e in Object.FindObjectsByType<EnemyIdentity>(FindObjectsInactive.Exclude))
            if (e.Archetype == EnemyArchetype.Scav)
                PlaceholderFigure.Build(e.gameObject, PlaceholderFigure.Kind.Imp);
        Debug.Log("[PixelArt] 임시 인형 — 플레이어 · 잡귀");
    }

    [MenuItem("Dokkaebi/Art/곁에 잡귀 하나 세우기")]
    public static void SpawnImp()
    {
        if (!EditorApplication.isPlaying)
            return;

        PlayerMovement movement = Object.FindAnyObjectByType<PlayerMovement>();
        GameObject player = movement != null ? movement.gameObject : null;
        EnemyPrefabCatalog catalog = EnemyPrefabCatalog.Load();
        GameObject prefab = catalog != null ? catalog.Get(EnemyArchetype.Scav) : null;
        if (player == null || prefab == null)
        {
            Debug.LogWarning("[PixelArt] 플레이어 또는 잡귀 프리팹이 없다.");
            return;
        }

        Vector3 at = player.transform.position + new Vector3(2.5f, 0f, 1.5f);
        GameObject imp = PoolManager.EnsureInstance().Spawn(prefab, at, Quaternion.Euler(0f, 220f, 0f));
        if (imp != null && imp.TryGetComponent(out EnemyIdentity id))
            id.Apply(EnemyProfile.Build(EnemyArchetype.Scav, EnemyRarity.Normal, System.Array.Empty<EnemyAffix>()));
        Debug.Log("[PixelArt] 잡귀 하나");
    }
}
