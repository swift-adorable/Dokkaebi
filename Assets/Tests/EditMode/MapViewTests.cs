using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>전체 지도 확대 · 이동 셈 (2026-10-08 · 결정 2-94).</summary>
    public class MapViewTests
    {
        private static readonly Vector2 View = new(1600f, 900f);

        [Test]
        public void 배율_1이면_장_하나가_다_들어온다()
        {
            Vector2 wide = MapView.FitSize(View, 2f);     // 폭이 넓은 장
            Vector2 tall = MapView.FitSize(View, 0.6f);   // 세로로 긴 장

            Assert.LessOrEqual(wide.x, View.x + 0.01f);
            Assert.LessOrEqual(wide.y, View.y + 0.01f);
            Assert.AreEqual(2f, wide.x / wide.y, 0.001f, "비율이 깨졌습니다.");
            Assert.AreEqual(View.y, tall.y, 0.01f);
            Assert.AreEqual(0.6f, tall.x / tall.y, 0.001f);
        }

        [Test]
        public void 배율은_1에서_4까지()
        {
            Assert.AreEqual(MapView.MinZoom, MapView.ClampZoom(0.2f));
            Assert.AreEqual(MapView.MaxZoom, MapView.ClampZoom(9f));
            Assert.AreEqual(2f, MapView.OpenZoom);
        }

        [Test]
        public void 그림이_화면보다_작으면_가운데에_선다()
        {
            Vector2 content = MapView.FitSize(View, 1.2f);   // 배율 1 — 폭이 남는다
            Vector2 c = MapView.ClampCenter(new Vector2(0.1f, 0.9f), View, content);

            Assert.AreEqual(0.5f, c.x, 0.001f, "남는 축에서 그림이 한쪽으로 쏠렸습니다.");
            Assert.AreEqual(0.5f, c.y, 0.001f);
        }

        [Test]
        public void 확대하면_끝이_화면_끝을_넘지_않는다()
        {
            Vector2 content = MapView.FitSize(View, 1.2f) * 3f;
            Vector2 c = MapView.ClampCenter(new Vector2(0f, 1f), View, content);

            Vector2 leftBottom = MapView.ToView(Vector2.zero, c, content);
            Assert.LessOrEqual(leftBottom.x, -View.x * 0.5f + 0.01f, "왼쪽 끝 밖이 비어 보입니다.");

            Vector2 rightTop = MapView.ToView(Vector2.one, c, content);
            Assert.GreaterOrEqual(rightTop.y, View.y * 0.5f - 0.01f, "위쪽 끝 밖이 비어 보입니다.");
        }

        [Test]
        public void 화면과_그림_자리가_서로_돌아온다()
        {
            Vector2 content = new(2400f, 1800f);
            Vector2 center = new(0.3f, 0.6f);
            Vector2 uv = new(0.42f, 0.55f);

            Vector2 back = MapView.ToUv(MapView.ToView(uv, center, content), center, content);
            Assert.AreEqual(uv.x, back.x, 0.0001f);
            Assert.AreEqual(uv.y, back.y, 0.0001f);
        }

        [Test]
        public void 휠로_확대해도_가리킨_곳이_그대로다()
        {
            Vector2 before = new(1600f, 1200f), after = new(2400f, 1800f);
            Vector2 center = new(0.5f, 0.5f), anchor = new(300f, -120f);

            Vector2 uvBefore = MapView.ToUv(anchor, center, before);
            Vector2 next = MapView.ZoomAround(center, anchor, before, after);
            Vector2 uvAfter = MapView.ToUv(anchor, next, after);

            Assert.AreEqual(uvBefore.x, uvAfter.x, 0.0001f);
            Assert.AreEqual(uvBefore.y, uvAfter.y, 0.0001f);
        }

        [Test]
        public void 양피지_지도는_길이_덩어리보다_밝다()
        {
            Color32 ground = MapRaster.ParchmentGround;
            Color32 building = MapRaster.ParchmentColorOf(MapBlockKind.Building);
            Assert.Greater(ground.r + ground.g + ground.b, building.r + building.g + building.b,
                "참고 그림처럼 길이 밝고 건물이 짙어야 합니다.");
            Assert.Less(MapRaster.ParchmentOutside.r, ground.r, "장 밖이 길보다 밝습니다.");
        }
    }
}
