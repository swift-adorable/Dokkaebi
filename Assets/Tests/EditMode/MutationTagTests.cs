using System;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// MutationTag 테스트.
    ///
    /// 태그 게이팅은 Mutation 시스템 전체의 토대이므로(마스터 프롬프트 10-2 [1]),
    /// 비트 정의가 깨지면 "죽은 선택지"가 조용히 살아난다. 정의 자체를 검증한다.
    /// </summary>
    public class MutationTagTests
    {
        [Test]
        public void 태그는_None을_제외하고_14종이다()
        {
            Array values = Enum.GetValues(typeof(MutationTag));

            int defined = 0;

            foreach (MutationTag value in values)
            {
                if (value != MutationTag.None)
                    defined++;
            }

            Assert.AreEqual(MutationTagExtensions.DefinedTagCount, defined,
                "v5 §4가 확정한 태그 개수는 14종입니다. 폐기된 소환수·지속형·근접을 되살리면 안 됩니다.");
        }

        [Test]
        public void 각_태그는_서로_겹치지_않는_단일_비트다()
        {
            int union = 0;

            foreach (MutationTag value in Enum.GetValues(typeof(MutationTag)))
            {
                if (value == MutationTag.None)
                    continue;

                int bits = (int)value;

                Assert.AreEqual(1, ((MutationTag)bits).Count(),
                    $"{value}는 단일 비트가 아닙니다.");

                Assert.AreEqual(0, union & bits, $"{value}의 비트가 다른 태그와 겹칩니다.");

                union |= bits;
            }
        }

        [Test]
        public void ContainsAll은_요구_태그를_전부_포함할_때만_참이다()
        {
            MutationTag core = MutationTag.Projectile | MutationTag.Fire | MutationTag.Duration;

            Assert.IsTrue(core.ContainsAll(MutationTag.Projectile));
            Assert.IsTrue(core.ContainsAll(MutationTag.Projectile | MutationTag.Fire));
            Assert.IsFalse(core.ContainsAll(MutationTag.Projectile | MutationTag.Zone));
        }

        [Test]
        public void 요구_태그가_None이면_항상_장착_가능하다()
        {
            // 엣지 케이스: 태그 제약이 없는 Support는 어떤 Core에도 붙는다.
            Assert.IsTrue(MutationTag.None.ContainsAll(MutationTag.None));
            Assert.IsTrue(MutationTag.Zone.ContainsAll(MutationTag.None));
        }

        [Test]
        public void ElementMask는_피해_유형_5종만_포함한다()
        {
            Assert.AreEqual(5, MutationTagExtensions.ElementMask.Count());

            Assert.IsTrue(MutationTagExtensions.ElementMask.ContainsAll(
                MutationTag.Fire | MutationTag.Cold | MutationTag.Lightning |
                MutationTag.Chaos | MutationTag.Physical));

            Assert.IsFalse(MutationTagExtensions.ElementMask.ContainsAny(MutationTag.Projectile));
        }

        [Test]
        public void Count는_켜진_비트_수를_센다()
        {
            Assert.AreEqual(0, MutationTag.None.Count());
            Assert.AreEqual(1, MutationTag.Zone.Count());
            Assert.AreEqual(3, (MutationTag.Zone | MutationTag.Fire | MutationTag.Duration).Count());
        }
    }
}
