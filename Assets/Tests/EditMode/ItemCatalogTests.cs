using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Blob.Tests
{
    /// <summary>
    /// 세이브가 id로 아이템을 되찾는 카탈로그. (로드맵 8-F)
    ///
    /// 【빠진 아이템은 세이브에서 사라진다.】 카탈로그에 없는 각인을 끼고
    /// 죽으면, 다음 실행에서 그 각인은 찾지 못해 건너뛰어진다 —
    /// 죽어도 남아야 할 유일한 장착품이 소리 없이 사라진다.
    /// </summary>
    public class ItemCatalogTests
    {
        private static ItemCatalog Catalog => ItemCatalog.Load();

        [Test]
        public void 카탈로그가_Resources에_있다()
        {
            Assert.IsNotNull(Catalog,
                "Resources/ItemCatalog가 없습니다. 「Blob/Items/아이템 카탈로그 생성」을 실행하십시오.");
        }

        [Test]
        public void 폴더의_모든_아이템이_들어_있다()
        {
            List<string> onDisk = AssetDatabase
                .FindAssets("t:ItemDefinition", new[] { ItemCatalog.DefinitionFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                .Where(item => item != null)
                .Select(item => item.Id)
                .ToList();

            var missing = onDisk.Where(id => Catalog.Find(id) == null).ToList();

            Assert.IsEmpty(missing,
                "카탈로그에 없는 아이템 — 「Blob/Items/아이템 카탈로그 생성」을 다시 실행하십시오: "
                + string.Join(", ", missing));
        }

        [Test]
        public void id가_겹치지_않는다()
        {
            var duplicates = Catalog.Items
                .Where(item => item != null)
                .GroupBy(item => item.Id)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            Assert.IsEmpty(duplicates, "중복 id: " + string.Join(", ", duplicates));
        }

        [Test]
        public void 각인은_전부_되찾을_수_있다()
        {
            // 죽어도 남는 유일한 장착품이다. 하나라도 빠지면 그 각인은
            // 세이브를 거치는 순간 사라진다.
            var imprints = Catalog.Items
                .Where(item => item != null && item.Kind == ItemKind.Imprint)
                .ToList();

            Assert.IsNotEmpty(imprints);

            foreach (ItemDefinition imprint in imprints)
                Assert.AreSame(imprint, Catalog.Find(imprint.Id), imprint.Id);
        }

        [Test]
        public void 없는_id는_null이다()
        {
            // 에셋을 지우면 세이브에 없는 id가 남는다. 예외가 아니라 null이어야
            // 세이브 전체가 못 쓰게 되지 않는다.
            Assert.IsNull(Catalog.Find("no_such_item_ever"));
            Assert.IsNull(Catalog.Find(string.Empty));
            Assert.IsNull(Catalog.Find(null));
        }
    }
}
