using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI용 스프라이트를 코드로 만든다. (로드맵 6-P)
///
/// 【왜 절차 생성인가】
/// 아트가 아직 없다. 그렇다고 전부 각진 사각형으로 두면
/// 「대충 만든 화면」으로 보이고, 그 인상은 수치 검증에도 영향을 준다.
/// 둥근 모서리와 테두리만 있어도 화면이 정돈된다.
///
/// 9-슬라이스로 만드는 이유 — 스프라이트 하나로 모든 크기를 감당한다.
/// 칸마다 텍스처를 만들면 메모리와 드로우콜이 늘어난다.
///
/// 아트가 들어오면 이 파일을 지우고 Image.sprite만 바꿔 끼우면 된다.
/// </summary>
public static class UISprites
{
    private static readonly Dictionary<int, Sprite> solidCache = new();
    private static readonly Dictionary<int, Sprite> outlineCache = new();

    private static Sprite sheenCache;

    /// <summary>
    /// 위에서 아래로 사라지는 흰 띠. 【유리의 광택이다.】
    ///
    /// iOS의 Liquid Glass는 「투명한 판 + 위쪽 모서리에 맺힌 빛」으로 읽힌다.
    /// 배경 흐림(blur)은 셰이더가 있어야 하지만, 광택과 테두리만으로도
    /// 「유리판」이라는 인상의 대부분이 만들어진다.
    ///
    /// 세로로만 변하므로 가로 1픽셀이면 충분하다. 9-슬라이스로 가로를 늘린다.
    /// </summary>
    public static Sprite Sheen()
    {
        if (sheenCache != null)
            return sheenCache;

        const int Height = 64;

        var texture = new Texture2D(1, Height, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[Height];

        for (int y = 0; y < Height; y++)
        {
            // y가 0이 아래, Height-1이 위다. 위쪽이 밝다.
            float t = y / (float)(Height - 1);

            // 위 1/3에만 몰아 준다. 전체에 깔면 그냥 밝은 판이 되어 버린다.
            float a = Mathf.Pow(Mathf.Clamp01((t - 0.55f) / 0.45f), 1.6f);

            pixels[y] = new Color32(255, 255, 255, (byte)(a * 255f));
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false);

        sheenCache = Sprite.Create(texture, new Rect(0, 0, 1, Height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

        sheenCache.hideFlags = HideFlags.HideAndDontSave;

        return sheenCache;
    }

    /// <summary>속이 찬 둥근 사각형. 패널·버튼·칸의 바탕이다.</summary>
    public static Sprite Rounded(int radius)
    {
        radius = Mathf.Clamp(radius, 1, 64);

        if (solidCache.TryGetValue(radius, out Sprite cached) && cached != null)
            return cached;

        Sprite made = Build(radius, thickness: 0);

        solidCache[radius] = made;

        return made;
    }

    /// <summary>테두리만 있는 둥근 사각형. 바탕 위에 겹쳐 윤곽을 준다.</summary>
    public static Sprite RoundedOutline(int radius, int thickness)
    {
        radius = Mathf.Clamp(radius, 1, 64);
        thickness = Mathf.Clamp(thickness, 1, radius);

        int key = radius * 100 + thickness;

        if (outlineCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Sprite made = Build(radius, thickness);

        outlineCache[key] = made;

        return made;
    }

    // ── 종류 표식 ─────────────────────────────────────────────────────

    /// <summary>
    /// 아이템 종류를 알려 주는 도형.
    ///
    /// 【왜 도형인가】
    /// 실제 아이템 그림은 아직 없다. 그렇다고 칸을 글자로만 채우면
    /// 격자가 표처럼 보이고, 무엇이 무기이고 무엇이 재료인지 한눈에 안 들어온다.
    /// 색과 도형 두 축으로 구분하면 글자를 읽기 전에 분류가 먼저 읽힌다.
    ///
    /// 그림이 들어오면 ItemDefinition.Icon이 이 자리를 대신한다.
    /// </summary>
    public enum Glyph
    {
        Circle = 0,
        Diamond = 1,
        Triangle = 2,
        Hexagon = 3,
        Shield = 4,
        Square = 5,
        Cross = 6,

        // ── 생존 세 축의 표식 (Survival_System 7절) ──────────────────
        // 글자 대신 도형을 쓴다. 「체력·수분·에너지」를 적으면 좁은 화면에서
        // 두 줄로 접히고, 그 자리는 막대가 써야 한다.

        /// <summary>체력.</summary>
        Heart = 7,

        /// <summary>수분.</summary>
        Drop = 8,

        /// <summary>에너지.</summary>
        Bolt = 9
    }

    private static readonly Dictionary<Glyph, Sprite> glyphCache = new();

    /// <summary>도형 스프라이트. 128×128 한 장을 만들어 재사용한다.</summary>
    public static Sprite Of(Glyph shape)
    {
        if (glyphCache.TryGetValue(shape, out Sprite cached) && cached != null)
            return cached;

        Sprite made = BuildGlyph(shape);

        glyphCache[shape] = made;

        return made;
    }

    public static Glyph GlyphFor(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Weapon:     return Glyph.Triangle;
            case ItemKind.Armour:     return Glyph.Shield;
            case ItemKind.Backpack:   return Glyph.Square;
            case ItemKind.Imprint:    return Glyph.Hexagon;
            case ItemKind.SkillGem:   return Glyph.Diamond;
            case ItemKind.Consumable: return Glyph.Cross;
            default:                  return Glyph.Circle;
        }
    }

    private static Sprite BuildGlyph(Glyph shape)
    {
        const int Size = 128;

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // -1 ~ 1 좌표로 옮겨 도형 식을 단순하게 둔다.
                float u = (x + 0.5f) / Size * 2f - 1f;
                float v = (y + 0.5f) / Size * 2f - 1f;

                float alpha = Mathf.Clamp01(Inside(shape, u, v));

                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false);

        var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size),
            new Vector2(0.5f, 0.5f), 100f);

        sprite.hideFlags = HideFlags.HideAndDontSave;

        return sprite;
    }

    /// <summary>
    /// 도형 안쪽이면 1, 밖이면 0. 경계에서 부드럽게 끊는다.
    ///
    /// 거리 함수 하나로 처리하는 이유 — 도형마다 그리기 코드를 따로 쓰면
    /// 안티에일리어싱을 매번 다시 맞춰야 한다.
    /// </summary>
    private static float Inside(Glyph shape, float u, float v)
    {
        // 경계 한 픽셀 폭. 128칸을 -1~1로 폈으므로 한 칸이 0.0156이다.
        const float Edge = 0.03f;

        float distance;

        switch (shape)
        {
            case Glyph.Diamond:
                distance = Mathf.Abs(u) + Mathf.Abs(v) - 0.82f;
                break;

            case Glyph.Square:
                distance = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) - 0.66f;
                break;

            case Glyph.Triangle:
                // 오른쪽을 보는 삼각형. 탄이 나가는 방향이다.
                distance = Mathf.Max(u - 0.72f, Mathf.Abs(v) * 1.15f - (0.72f - u) * 0.62f - 0.06f);
                break;

            case Glyph.Hexagon:
                distance = Mathf.Max(
                    Mathf.Abs(u) * 0.866f + Mathf.Abs(v) * 0.5f,
                    Mathf.Abs(v)) - 0.74f;
                break;

            case Glyph.Shield:
                // 위는 네모, 아래는 뾰족하게. 방패의 실루엣이다.
                distance = v > -0.1f
                    ? Mathf.Max(Mathf.Abs(u) - 0.62f, v - 0.74f)
                    : Mathf.Abs(u) - 0.62f * (1f + (v + 0.1f) / 0.85f);
                break;

            case Glyph.Cross:
                // 십자. 회복·소모품을 뜻한다.
                distance = Mathf.Min(
                    Mathf.Max(Mathf.Abs(u) - 0.26f, Mathf.Abs(v) - 0.74f),
                    Mathf.Max(Mathf.Abs(u) - 0.74f, Mathf.Abs(v) - 0.26f));
                break;

            case Glyph.Heart:
            {
                // 하트. 위쪽 두 원과 아래쪽 꼭짓점.
                // 살짝 내려 그린다 — 꼭짓점이 길어 무게중심이 아래로 쏠린다.
                float hv = v - 0.14f;

                float lobes = Mathf.Min(
                    Mathf.Sqrt((u + 0.33f) * (u + 0.33f) + (hv - 0.22f) * (hv - 0.22f)) - 0.40f,
                    Mathf.Sqrt((u - 0.33f) * (u - 0.33f) + (hv - 0.22f) * (hv - 0.22f)) - 0.40f);

                // 아래는 역삼각형. 두 원의 아래쪽에 맞물린다.
                float point = Mathf.Max(hv - 0.22f, Mathf.Abs(u) * 0.78f + hv * 0.62f - 0.35f);

                distance = Mathf.Min(lobes, point);
                break;
            }

            case Glyph.Drop:
            {
                // 물방울. 아래는 원, 위는 뾰족하게.
                float dv = v + 0.16f;

                float ball = Mathf.Sqrt(u * u + dv * dv) - 0.50f;

                float tip = Mathf.Max(-dv, Mathf.Abs(u) * 1.30f + dv * 0.62f - 0.42f);

                distance = Mathf.Min(ball, tip);
                break;
            }

            case Glyph.Bolt:
            {
                // 번개. 기울어진 띠 두 개를 위아래로 어긋나게 붙인다.
                float upper = Mathf.Max(
                    Mathf.Max(Mathf.Abs(u * 1.45f + v * 0.62f - 0.10f) - 0.26f, -v),
                    v - 0.86f);

                float lower = Mathf.Max(
                    Mathf.Max(Mathf.Abs(u * 1.45f + v * 0.62f + 0.10f) - 0.26f, v),
                    -v - 0.86f);

                distance = Mathf.Min(upper, lower);
                break;
            }

            default:
                distance = Mathf.Sqrt(u * u + v * v) - 0.74f;
                break;
        }

        return 0.5f - distance / Edge;
    }

    /// <summary>
    /// 한 변이 2r+1인 정사각형을 만든다. 가운데 1픽셀이 늘어나는 9-슬라이스 중심이다.
    ///
    /// thickness가 0이면 속을 채우고, 1 이상이면 그만큼의 테두리 고리만 남긴다.
    /// </summary>
    private static Sprite Build(int radius, int thickness)
    {
        int size = radius * 2 + 1;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = Coverage(x, y, size, radius, thickness);

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false);

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: 100f,
            extrude: 0,
            meshType: SpriteMeshType.FullRect,
            border: new Vector4(radius, radius, radius, radius));

        sprite.hideFlags = HideFlags.HideAndDontSave;

        return sprite;
    }

    /// <summary>
    /// 이 픽셀이 도형에 얼마나 덮이는가(0~1).
    ///
    /// 모서리에서만 거리 계산을 한다. 경계에서 1픽셀에 걸쳐 부드럽게 끊어
    /// 계단이 보이지 않게 한다 — 안티에일리어싱이 없으면 둥근 티가 안 난다.
    /// </summary>
    private static float Coverage(int x, int y, int size, int radius, int thickness)
    {
        // 모서리 원의 중심. 가운데 영역은 항상 내부다.
        float cx = x < radius ? radius : x > size - 1 - radius ? size - 1 - radius : x;
        float cy = y < radius ? radius : y > size - 1 - radius ? size - 1 - radius : y;

        float distance = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        float outer = Mathf.Clamp01(radius - distance + 0.5f);

        if (thickness <= 0)
            return outer;

        // 안쪽을 다시 파낸다. 고리만 남는다.
        float inner = Mathf.Clamp01(radius - thickness - distance + 0.5f);

        return Mathf.Clamp01(outer - inner);
    }
}
