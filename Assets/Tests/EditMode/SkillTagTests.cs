using System;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// SkillTag 테스트.
    ///
    /// 태그 게이팅은 Skill 시스템 전체의 토대이므로(마스터 프롬프트 10-2 [1]),
    /// 비트 정의가 깨지면 "죽은 선택지"가 조용히 살아난다. 정의 자체를 검증한다.
    /// </summary>
    public class SkillTagTests
    {
        [Test]
        public void 태그는_None을_제외하고_14종이다()
        {
            Array values = Enum.GetValues(typeof(SkillTag));

            int defined = 0;

            foreach (SkillTag value in values)
            {
                if (value != SkillTag.None)
                    defined++;
            }

            Assert.AreEqual(SkillTagExtensions.DefinedTagCount, defined,
                "v5 §4가 확정한 태그 개수는 14종입니다. 폐기된 소환수·지속형·근접을 되살리면 안 됩니다.");
        }

        [Test]
        public void 각_태그는_서로_겹치지_않는_단일_비트다()
        {
            int union = 0;

            foreach (SkillTag value in Enum.GetValues(typeof(SkillTag)))
            {
                if (value == SkillTag.None)
                    continue;

                int bits = (int)value;

                Assert.AreEqual(1, ((SkillTag)bits).Count(),
                    $"{value}는 단일 비트가 아닙니다.");

                Assert.AreEqual(0, union & bits, $"{value}의 비트가 다른 태그와 겹칩니다.");

                union |= bits;
            }
        }

        [Test]
        public void ContainsAll은_요구_태그를_전부_포함할_때만_참이다()
        {
            SkillTag core = SkillTag.Projectile | SkillTag.Fire | SkillTag.Duration;

            Assert.IsTrue(core.ContainsAll(SkillTag.Projectile));
            Assert.IsTrue(core.ContainsAll(SkillTag.Projectile | SkillTag.Fire));
            Assert.IsFalse(core.ContainsAll(SkillTag.Projectile | SkillTag.Zone));
        }

        [Test]
        public void 요구_태그가_None이면_항상_장착_가능하다()
        {
            // 엣지 케이스: 태그 제약이 없는 Support는 어떤 Core에도 붙는다.
            Assert.IsTrue(SkillTag.None.ContainsAll(SkillTag.None));
            Assert.IsTrue(SkillTag.Zone.ContainsAll(SkillTag.None));
        }

        [Test]
        public void ElementMask는_피해_유형_5종만_포함한다()
        {
            Assert.AreEqual(5, SkillTagExtensions.ElementMask.Count());

            Assert.IsTrue(SkillTagExtensions.ElementMask.ContainsAll(
                SkillTag.Fire | SkillTag.Cold | SkillTag.Lightning |
                SkillTag.Chaos | SkillTag.Physical));

            Assert.IsFalse(SkillTagExtensions.ElementMask.ContainsAny(SkillTag.Projectile));
        }

        [Test]
        public void 한글_태그명이_표기_순서대로_만들어진다()
        {
            SkillTag tags = SkillTag.Projectile | SkillTag.Fire | SkillTag.Duration;

            Assert.AreEqual("투사체 · 화염 · 지속시간", tags.ToKoreanString());
        }

        [Test]
        public void 태그가_없으면_빈_문자열이다()
        {
            Assert.AreEqual(string.Empty, SkillTag.None.ToKoreanString());
        }

        [Test]
        public void 태그_하나면_구분자가_붙지_않는다()
        {
            Assert.AreEqual("청산", SkillTag.Consuming.ToKoreanString());
        }

        [Test]
        public void Count는_켜진_비트_수를_센다()
        {
            Assert.AreEqual(0, SkillTag.None.Count());
            Assert.AreEqual(1, SkillTag.Zone.Count());
            Assert.AreEqual(3, (SkillTag.Zone | SkillTag.Fire | SkillTag.Duration).Count());
        }
    }
}
