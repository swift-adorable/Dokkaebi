using System;
using UnityEngine;

/// <summary>
/// 【가 본 땅 — 안개】 (결정 2-90) — 장 맵 하나를 FogCell(4m) 칸으로 나눠 걸어 본 칸만 밝힌다.
/// 장마다 하나 · 세이브에 남는다(쓰러져도 잃지 않는다 — 아는 길은 아는 길이다).
/// 칸 순서는 (x, z) — x가 빠르다. 세이브는 비트를 base64로.
/// </summary>
public sealed class FogGrid
{
    public readonly int Width;
    public readonly int Height;
    public readonly Vector2 HalfSize;

    private readonly bool[] revealed;

    public int RevealedCount { get; private set; }

    public FogGrid(Vector2 halfSize)
    {
        HalfSize = halfSize;
        Width = Mathf.Max(1, Mathf.CeilToInt(halfSize.x * 2f / MapTable.FogCell));
        Height = Mathf.Max(1, Mathf.CeilToInt(halfSize.y * 2f / MapTable.FogCell));
        revealed = new bool[Width * Height];
    }

    public bool IsRevealed(int x, int z)
        => x >= 0 && z >= 0 && x < Width && z < Height && revealed[z * Width + x];

    /// <summary>이 자리(월드)가 밝혀졌는가.</summary>
    public bool IsRevealedAt(Vector3 world)
        => IsRevealed(CellX(world.x), CellZ(world.z));

    public int CellX(float worldX) => Mathf.FloorToInt((worldX + HalfSize.x) / MapTable.FogCell);
    public int CellZ(float worldZ) => Mathf.FloorToInt((worldZ + HalfSize.y) / MapTable.FogCell);

    /// <summary>둘레를 밝힌다. 새로 밝혀진 칸이 있으면 true.</summary>
    public bool Reveal(Vector3 world, float radius)
    {
        bool changed = false;
        int x0 = CellX(world.x - radius), x1 = CellX(world.x + radius);
        int z0 = CellZ(world.z - radius), z1 = CellZ(world.z + radius);
        float r2 = radius * radius;

        for (int z = Mathf.Max(0, z0); z <= Mathf.Min(Height - 1, z1); z++)
        for (int x = Mathf.Max(0, x0); x <= Mathf.Min(Width - 1, x1); x++)
        {
            int i = z * Width + x;
            if (revealed[i])
                continue;

            // 칸 가운데가 원 안이면 밝힌다.
            float cx = -HalfSize.x + (x + 0.5f) * MapTable.FogCell - world.x;
            float cz = -HalfSize.y + (z + 0.5f) * MapTable.FogCell - world.z;
            if (cx * cx + cz * cz > r2)
                continue;

            revealed[i] = true;
            RevealedCount++;
            changed = true;
        }

        return changed;
    }

    /// <summary>다 밝힌다 (장부방 지도 · 디버그).</summary>
    public void RevealAll()
    {
        for (int i = 0; i < revealed.Length; i++)
            revealed[i] = true;

        RevealedCount = revealed.Length;
    }

    public string Export()
    {
        var bytes = new byte[(revealed.Length + 7) / 8];

        for (int i = 0; i < revealed.Length; i++)
            if (revealed[i])
                bytes[i >> 3] |= (byte)(1 << (i & 7));

        return Convert.ToBase64String(bytes);
    }

    /// <summary>세이브에서 읽는다. 맵 크기가 바뀌어 길이가 안 맞으면 버리고 처음부터(가 본 땅이 엉뚱한 곳에 찍히지 않게).</summary>
    public bool Import(string data)
    {
        Array.Clear(revealed, 0, revealed.Length);
        RevealedCount = 0;

        if (string.IsNullOrEmpty(data))
            return false;

        byte[] bytes;
        try { bytes = Convert.FromBase64String(data); }
        catch (FormatException) { return false; }

        if (bytes.Length != (revealed.Length + 7) / 8)
            return false;

        for (int i = 0; i < revealed.Length; i++)
            if ((bytes[i >> 3] & (1 << (i & 7))) != 0)
            {
                revealed[i] = true;
                RevealedCount++;
            }

        return true;
    }
}
