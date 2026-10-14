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
                ShopKeeperTable.Keeper keeper = ShopKeeperTable.Of(kind);
                StringAssert.Contains(keeper.Line, script, $"{ShopTable.NameOf(kind)} 말풍선이 본문에 없다");
                StringAssert.Contains(keeper.Name, script);
            }
        }

        [Test]
        public void 가게마다_주인이_다르다()
        {
            Assert.AreEqual("영감", ShopKeeperTable.Of(ShopKind.General).Name);
            Assert.AreEqual("빚쟁이", ShopKeeperTable.Of(ShopKind.Smithy).Name);
            Assert.AreEqual("참봉", ShopKeeperTable.Of(ShopKind.Apothecary).Name);
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
    }
}
