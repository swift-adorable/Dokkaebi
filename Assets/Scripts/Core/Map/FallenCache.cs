using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>쓰러진 자리 하나 — 장 · 자리 · 잃은 것 (결정 2-93 · 세이브 판 16).</summary>
[Serializable]
public class SavedFallen
{
    public int chapter = -1;
    public float x;
    public float z;
    public List<SavedItem> items = new();

    public bool IsEmpty => chapter < 0 || items == null || items.Count == 0;
}

/// <summary>
/// 【쓰러진 자리 되찾기】 (결정 2-93 · 패시브 「회수 계약」 · 덕코프의 무덤과 같다)
///
///   · 패시브를 배운 채 장 맵에서 쓰러지면 잃은 가방 · 장비가 쓰러진 자리에 남는다 — 하나뿐
///   · 다른 장을 몇 번 다녀와도 남아 있다가, 【그 장에 다시 들어간 판에서 한 번】 주울 수 있다
///   · 그 판에서 못 줍거나 다시 쓰러지면 사라진다 (쓰러지면 새 자리로 바뀐다)
///   · 지도 · 미니맵에 표시한다 (사용자 결정)
/// 판에 들어가는 순간 꺼내 간다(TakeFor) — 그래서 그 판이 끝나며 저장되면 이미 없다.
/// 판 도중에 끄면 저장되지 않으니 그대로 남는다(롤백 — 세이브 규칙과 같다).
/// MonoBehaviour 의존 없음 — EditMode 테스트 대상.
/// </summary>
public static class FallenCache
{
    private static SavedFallen current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => current = null;

    public static SavedFallen Current => current != null && !current.IsEmpty ? current : null;

    /// <summary>이 장에 쓰러진 자리가 남아 있는가.</summary>
    public static bool Has(int chapter) => Current != null && current.chapter == chapter;

    /// <summary>
    /// 쓰러졌다. 남은 자리는 사라지고, 되찾기를 배웠으면 새 자리가 생긴다.
    /// chapter가 음수(장 맵이 아닌 곳)거나 잃은 것이 없으면 새 자리는 없다.
    /// </summary>
    public static void OnDeath(bool canRecover, int chapter, Vector3 at, List<SavedItem> lost)
    {
        current = null;

        if (!canRecover || chapter < 0 || lost == null || lost.Count == 0)
            return;

        current = new SavedFallen { chapter = chapter, x = at.x, z = at.z, items = new List<SavedItem>(lost) };
    }

    /// <summary>그 장의 판이 시작됐다 — 남은 자리를 꺼내 간다(이 판 한 번). 없으면 null.</summary>
    public static SavedFallen TakeFor(int chapter)
    {
        if (!Has(chapter))
            return null;

        SavedFallen taken = current;
        current = null;
        return taken;
    }

    public static void Clear() => current = null;

    public static void Capture(SaveData data) => data.fallen = Current;

    public static void Restore(SaveData data)
        => current = data.fallen != null && !data.fallen.IsEmpty ? data.fallen : null;
}
