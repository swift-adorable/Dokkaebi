using System;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// Skill System v8 문서와 코드 스키마가 어긋나지 않았는지 고정하는 테스트.
    ///
    /// 문서에만 고치고 코드를 안 고치는 드리프트를 막는 것이 목적이다.
    /// 여기서 실패하면 "문서를 바꿨는데 코드를 안 바꿨다"는 뜻이다.
    /// </summary>
    public class SkillSchemaTests
    {
        [Test]
        public void 대가_통화는_7종이다()
        {
            // v8 §5: 없음 / 탄막 밀도 / 유효 사거리 / 조작 제약 / 기능 배타
            //        / 투사체 속도 / 지속시간 / 즉시성
            Assert.AreEqual(8, Enum.GetValues(typeof(CostType)).Length,
                "None을 포함해 8개(대가 7종)여야 합니다.");

            Assert.AreEqual(0, (int)CostType.None);
            Assert.AreEqual(5, (int)CostType.ProjectileSpeed);
            Assert.AreEqual(6, (int)CostType.Duration);
            Assert.AreEqual(7, (int)CostType.Immediacy);
        }

        [Test]
        public void 스킬_분류는_4종이다()
        {
            // v8: Core / Support / 발동(Meta) / 전령(Persistent)
            Assert.AreEqual(4, Enum.GetValues(typeof(SkillCategory)).Length);
        }

        [Test]
        public void Core_계열은_부여와_기폭_둘뿐이다()
        {
            // v8: 전달 계열은 Support로 내려갔다. 투사체 행동은 Core의 몫이 아니다.
            Assert.AreEqual(3, Enum.GetValues(typeof(CoreFamily)).Length,
                "None + 부여 + 기폭 = 3이어야 합니다.");

            Assert.IsFalse(Enum.IsDefined(typeof(CoreFamily), "Delivery"),
                "전달 계열은 v8에서 폐지되었습니다.");
        }

        [Test]
        public void 동시_장착_상한은_11칸이다()
        {
            // 11 = Core 2 + Support 6 + 발동 2 + 전령 1
            // 적재(Loadout) 개념은 폐기되었으나 이 상한 자체는 그대로다.
            // 가방 용량이 그 자리를 대신 맡는다. (Skill_System.md 11-1절)
            Assert.AreEqual(
                11,
                SocketedBuild.MaxCores
                + SocketedBuild.MaxCores * SocketedBuild.SocketsPerCore
                + SocketedBuild.MaxMetas
                + SocketedBuild.MaxHeralds);

            Assert.AreEqual(
                11,
                SocketUnlockTable.Evaluate(SocketUnlockTable.FullyOpenLevel).TotalSlots,
                "각성 최고 레벨에서 열리는 자리 수가 동시 장착 상한과 달라졌습니다.");
        }

        [Test]
        public void 전령은_Persistent_분류로만_판정된다()
        {
            SkillDefinition herald = SkillTestFactory.CreatePersistent("herald_ash");
            SkillDefinition meta = SkillTestFactory.CreateMeta("meta_1");

            Assert.IsTrue(herald.IsHerald);
            Assert.IsFalse(meta.IsHerald);
        }

        [Test]
        public void 전령은_Persistent_태그가_자동으로_붙는다()
        {
            SkillDefinition herald = SkillTestFactory.CreatePersistent("herald_ice");

            Assert.IsTrue((herald.Tags & SkillTag.Persistent) != 0);
        }
    }
}
