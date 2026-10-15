using NUnit.Framework;
using UnityEngine;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 【가방이 닫혀 있어도 상세가 보인다】 (결정 2-131) — 창고 · 가게 · 시설의 「상세보기」는 가방 화면이 닫힌 채로 부른다.
    /// 상세가 꺼진 가방 판 아래에 만들어져 보이지 않았다.
    /// </summary>
    public class ItemDetailTests
    {
        [Test]
        public void 가방이_닫혀_있어도_바깥에서_연_상세가_보인다()
        {
            ItemCatalog catalog = ItemCatalog.Load();
            Assert.IsNotNull(catalog);
            ItemDefinition definition = null;
            foreach (ItemDefinition d in catalog.Items)
                if (d != null)
                {
                    definition = d;
                    break;
                }
            Assert.IsNotNull(definition);

            InventoryScreenUI screen = InventoryScreenUI.EnsureInstance();
            try
            {
                Assert.IsFalse(screen.IsOpen, "가방 화면은 닫혀 있다");

                InventoryScreenUI.OpenReadOnlyDetail(new ItemStack(definition), "창고");

                GameObject shade = null;
                foreach (Transform t in screen.GetComponentsInChildren<Transform>(true))
                    if (t.name == "DetailShade")
                        shade = t.gameObject;

                Assert.IsNotNull(shade, "상세가 만들어지지 않았다");
                Assert.IsTrue(shade.activeInHierarchy, "상세가 꺼진 가방 판 아래에 있어 보이지 않는다");
            }
            finally
            {
                Object.DestroyImmediate(screen.gameObject);
            }
        }
    }
}
