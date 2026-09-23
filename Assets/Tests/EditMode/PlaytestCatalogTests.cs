using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 검증 도구가 실제로 쓸 수 있는 상태인지 고정한다. (로드맵 6-P)
    ///
    /// 【왜 테스트로 묶는가】
    /// 이 두 에셋은 에디터 메뉴가 만든다. 메뉴를 다시 돌리지 않으면
    /// 목록이 비거나 낡은 채로 남는데, 그 사실은 **실제 기기에서 버튼을 눌러야만**
    /// 드러났다. 「지급이 안 된다」 「패시브 트리 에셋이 없습니다」가 둘 다 이것이었다.
    /// 수치는 보지 않는다. 비었는지만 본다 — 비면 도구가 아니다.
    /// </summary>
    public class PlaytestCatalogTests
    {
        private static PlaytestCatalog catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            catalog = PlaytestCatalog.Load();

            Assert.IsNotNull(catalog,
                "Resources/PlaytestCatalog.asset이 없습니다. 「Blob/Playtest/검증 카탈로그 생성」을 실행하십시오.");
        }

        private static IEnumerable<TestCaseData> Bundles()
        {
            PlaytestCatalog c = PlaytestCatalog.Load();

            yield return new TestCaseData((object)c.StarterKit).SetName("티어1 한 벌");
            yield return new TestCaseData((object)c.EndgameKit).SetName("티어6 한 벌");
            yield return new TestCaseData((object)c.Weapons).SetName("무기");
            yield return new TestCaseData((object)c.Imprints).SetName("각인");
            yield return new TestCaseData((object)c.KeyImprints).SetName("대표 각인");
            yield return new TestCaseData((object)c.CoreGems).SetName("핵심 젬");
            yield return new TestCaseData((object)c.SupportGems).SetName("보조 젬");
            yield return new TestCaseData((object)c.MetaGems).SetName("발동 젬");
            yield return new TestCaseData((object)c.HeraldGems).SetName("전령 젬");
            yield return new TestCaseData((object)c.ChecklistGems).SetName("검증용 젬");
            yield return new TestCaseData((object)c.Stackables).SetName("겹치는 재료");
        }

        [TestCaseSource(nameof(Bundles))]
        public void 지급_묶음이_비어_있지_않다(IReadOnlyList<ItemDefinition> bundle)
        {
            Assert.IsNotNull(bundle);
            Assert.Greater(bundle.Count, 0, "목록이 비었습니다. 검증 카탈로그를 다시 생성하십시오.");
            Assert.IsFalse(bundle.Any(i => i == null), "목록에 빈 칸이 있습니다.");
        }

        [Test]
        public void 발동_젬과_전령_젬은_범주가_맞다()
        {
            Assert.IsTrue(catalog.MetaGems.All(g => g.Skill != null && g.Skill.Category == SkillCategory.Meta),
                "발동 젬 목록에 Meta가 아닌 젬이 섞였습니다.");

            Assert.IsTrue(catalog.HeraldGems.All(g => g.Skill != null && g.Skill.Category == SkillCategory.Persistent),
                "전령 젬 목록에 Persistent가 아닌 젬이 섞였습니다.");
        }

        [Test]
        public void 과중량_재료가_지정되어_있다()
        {
            Assert.IsNotNull(catalog.BulkMaterial, "가방 채우기가 쓸 재료가 없습니다.");
        }

        /// <summary>
        /// 패시브 화면이 「에셋이 없습니다」를 띄우던 원인 중 하나를 막는다.
        /// 나머지 절반(매니저가 Start 전에 읽히는 문제)은 Tree 지연 로드가 맡는다.
        /// </summary>
        [Test]
        public void 패시브_트리를_Resources에서_불러올_수_있다()
        {
            PassiveTree tree = PassiveTree.Load();

            Assert.IsNotNull(tree,
                $"Resources/{PassiveTree.ResourcePath} 에셋이 없습니다. 「Blob/Passive/패시브 에셋 생성」을 실행하십시오.");

            Assert.Greater(tree.Count, 0, "패시브 트리에 노드가 하나도 없습니다.");
            Assert.IsFalse(tree.Nodes.Any(n => n == null), "패시브 트리에 빈 노드 참조가 있습니다.");
        }
    }
}
