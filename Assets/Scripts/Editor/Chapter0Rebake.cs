using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【0장만 다시 굽기】 (결정 2-149 · 맵 2배) — 맵(그레이박스 · 길찾기) → 장 꾸미기 → 도트 바닥을 한 번에.
/// 「장 맵 굽기 (전부)」는 일곱 장을 다 구워 오래 걸린다 — 0장만 고칠 때 쓴다.
/// </summary>
public static class Chapter0Rebake
{
    [MenuItem("Dokkaebi/Map/0장만 다시 굽기 (맵 · 꾸밈 · 도트 바닥)")]
    public static void Run()
    {
        ZoneMapBaker.EnsureLayers();
        ChapterMap map = ZoneMapTable.Of(0);
        LockSiteTable.ClearCache();
        DressPlan.ClearCache();
        // 0장이 열린 유일한 씬이면 Bake 가 닫지 못해, 옛 내용 위에 새 맵을 또 얹어 저장했다(굽기마다 Map_Ch0 가 하나씩 쌓여 19개 —
        // 옛 대숲 · 경계의 보이지 않는 충돌체가 새 집 방을 막았다). 빈 씬을 먼저 열어 0장을 닫히게 한다.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ZoneMapBaker.Bake(map);
        Scene scene = EditorSceneManager.OpenScene(ZoneMapBaker.PathFor(map), OpenSceneMode.Single);
        SceneArtDresser.Dress(scene, map);
        PixelGroundBaker.Bake(scene, map);
        ChapterArtBake.Bake(scene, 0);   // v8 — 꾸밈을 애셋으로 (씬을 열 때 다시 만들지 않게)
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Chapter0Rebake] 0장 {map.HalfSize.x * 2f:0}×{map.HalfSize.y * 2f:0}m 다시 구움");
    }
}
