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
