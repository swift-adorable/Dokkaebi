using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 결정 2-121 — 가게 화면의 상인 말풍선은 본문 대사를 그대로 빌린다(새로 짓지 않는다 · Story Lock),
    /// 지도 사진 덮개는 닫힌 구역만 어둡게 한다.
    /// </summary>
    public class ShopScreenTests
    {
        private static string Script()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            return File.ReadAllText(Path.Combine(root, "docs/Dokkaebi_Story_Script.txt"));
        }

        [Test]
        public void 상인_말풍선은_본문에_그대로_있다()
        {
            string script = Script();

            foreach (ShopKind kind in ShopTable.All)
            {
                KeeperTable.Keeper keeper = KeeperTable.OfShop(kind);
                StringAssert.Contains(keeper.Line, script, $"{ShopTable.NameOf(kind)} 말풍선이 본문에 없다");
                StringAssert.Contains(keeper.Name, script);
            }

            // 시설 (결정 2-123) — 주인이 있으면 대사가 본문에 그대로, 없으면(부뚜막 · 작업대) 말풍선이 없다.
            foreach (Station station in System.Enum.GetValues(typeof(Station)))
            {
                KeeperTable.Keeper keeper = KeeperTable.Of(station);
                if (keeper.IsPerson)
                {
                    StringAssert.Contains(keeper.Line, script, $"{station} 말풍선이 본문에 없다");
                    StringAssert.Contains(keeper.Name, script);
                }
                else
                {
                    Assert.IsNull(keeper.Line, $"{station}은 주인이 없다 — 말풍선을 짓지 않는다");
                }
            }
        }

        [Test]
        public void 가게마다_주인이_다르다()
        {
            Assert.AreEqual("영감", KeeperTable.OfShop(ShopKind.General).Name);
            Assert.AreEqual("빚쟁이", KeeperTable.OfShop(ShopKind.Smithy).Name);
            Assert.AreEqual("참봉", KeeperTable.OfShop(ShopKind.Apothecary).Name);
            Assert.AreEqual("길달", KeeperTable.Of(Station.Ledger).Name);
        }

        [Test]
        public void 지도_덮개는_닫힌_구역만_어둡고_열리면_비어_있다()
        {
            ChapterMap map = ZoneMapTable.Of(1);
            Assert.IsNotNull(map);

            Color32[] allOpen = MapRaster.ClosedOverlay(map, _ => true, 1);
            foreach (Color32 c in allOpen)
                Assert.AreEqual(0, c.a, "다 열렸으면 덮개는 투명하다");

            Color32[] allClosed = MapRaster.ClosedOverlay(map, _ => false, 1);
            int dark = 0;
            foreach (Color32 c in allClosed)
                if (c.a > 0)
                    dark++;

            Assert.Greater(dark, 0, "닫힌 구역은 어둡다");
            Assert.Less(dark, allClosed.Length, "구역 밖(끊긴 밤길)은 사진 그대로");
        }

        /// <summary>결정 2-124 — 가운데 무대(주인 · 계산대)는 16:9의 가운데 칸 모양을 지킨다. 4:3 · 20:9에서는 남는 자리만 빈다.</summary>
        [Test]
        public void 가운데_무대는_16대9_가운데_칸_모양이다()
        {
            float width = (StationFrame.CenterEnd - StationFrame.CenterStart) * UIFactory.ReferenceWidth;
            float height = (StationFrame.BodyTop - StationFrame.BodyBottom) * UIFactory.ReferenceHeight;
            Assert.AreEqual(width / height, StationFrame.StageAspect, 0.0001f);

            // 4:3(세로가 남는다)과 20:9(가로가 남는다) 모두 무대가 칸 안에 들어간다 — 캔버스 비율은 화면 비율 그대로.
            foreach (float screenAspect in new[] { 4f / 3f, 16f / 9f, 20f / 9f })
            {
                float columnAspect = (StationFrame.CenterEnd - StationFrame.CenterStart) * screenAspect
                                     / (StationFrame.BodyTop - StationFrame.BodyBottom);
                float stageW = Mathf.Min(1f, StationFrame.StageAspect / columnAspect);   // 칸 너비에 대한 몫
                float stageH = Mathf.Min(1f, columnAspect / StationFrame.StageAspect);   // 칸 높이에 대한 몫
                Assert.LessOrEqual(stageW, 1f);
                Assert.LessOrEqual(stageH, 1f);
                Assert.IsTrue(Mathf.Approximately(stageW, 1f) || Mathf.Approximately(stageH, 1f), "한쪽은 칸을 꽉 채운다");
            }
        }
    }
}
