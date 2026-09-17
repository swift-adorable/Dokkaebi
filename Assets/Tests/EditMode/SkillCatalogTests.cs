using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 스킬 정의 에셋 53종의 구조 검증. (로드맵 5-E)
    ///
    /// 개별 수치를 검증하지 않는다. 수치는 플레이테스트로 바뀌기 때문이다.
    /// 대신 **바뀌면 안 되는 구조적 약속**만 고정한다.
    /// 여기서 실패하면 "에셋을 고치다 설계 규칙을 깼다"는 뜻이다.
    /// </summary>
    public class SkillCatalogTests
    {
        private static SkillCatalog catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            catalog = SkillCatalog.Load();
            Assert.IsNotNull(catalog, "SkillCatalog.asset을 찾지 못했습니다. Blob/Skill/카탈로그 다시 만들기를 실행하세요.");
        }

        private static IEnumerable<SkillDefinition> All => catalog.Definitions.Where(d => d != null);

        private static IEnumerable<SkillDefinition> Of(SkillCategory c)
            => All.Where(d => d.Category == c);

        // ── 개수 ──────────────────────────────────────────────────────────

        [Test]
        public void 총_53종이다()
        {
            Assert.AreEqual(53, catalog.Count);
        }

        [Test]
        public void 카테고리별_개수가_문서와_일치한다()
        {
            Assert.AreEqual(8, Of(SkillCategory.Core).Count(), "Core");
            Assert.AreEqual(35, Of(SkillCategory.Support).Count(), "Support");
            Assert.AreEqual(5, Of(SkillCategory.Meta).Count(), "Meta");
            Assert.AreEqual(5, Of(SkillCategory.Persistent).Count(), "전령");
        }

        [Test]
        public void 변형_수단이_본체보다_훨씬_많다()
        {
            // PoE2는 보조 젬이 스킬 젬보다 130개 많다. Blob도 같은 비율을 지켜야
            // "조합해서 만드는 게임"이 된다.
            int cores = Of(SkillCategory.Core).Count();
            int supports = Of(SkillCategory.Support).Count();

            Assert.Greater(supports, cores * 3, "Support가 Core의 3배를 넘어야 합니다.");
        }

        [Test]
        public void id가_중복되지_않는다()
        {
            // 중복은 도감·적재 저장을 조용히 망가뜨린다.
            var ids = All.Select(d => d.Id).ToList();

            CollectionAssert.AllItemsAreUnique(ids);
            Assert.IsFalse(ids.Any(string.IsNullOrWhiteSpace), "빈 id가 있습니다.");
        }

        [Test]
        public void 표시_이름과_설명이_비어_있지_않다()
        {
            foreach (SkillDefinition d in All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.DisplayName), $"{d.Id}의 이름이 비었습니다.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Description), $"{d.Id}의 설명이 비었습니다.");
            }
        }

        // ── 금지 사항 (Skill_System.md 14절) ───────────────────────────────

        [Test]
        public void 모든_Support는_대가를_가진다()
        {
            // 금지 1번 — 단순 강화만 주는 Skill을 만들지 않는다.
            foreach (SkillDefinition d in Of(SkillCategory.Support))
            {
                Assert.AreNotEqual(CostType.None, d.Cost, $"{d.Id}에 대가가 없습니다.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.CostDescription), $"{d.Id}의 대가 설명이 비었습니다.");
            }
        }

        [Test]
        public void 대가_통화는_7종_전부_실제로_쓰인다()
        {
            // 쓰이지 않는 통화는 죽은 enum이다. 문서에만 있고 코드에 없는 상태를 막는다.
            var used = Of(SkillCategory.Support).Select(d => d.Cost).Distinct().ToList();

            foreach (CostType c in System.Enum.GetValues(typeof(CostType)))
            {
                if (c == CostType.None)
                    continue;

                Assert.Contains(c, used, $"{c} 통화를 쓰는 Support가 없습니다.");
            }
        }

        [Test]
        public void Skill에_티어_체계가_없다()
        {
            // 금지 5번 — 같은 이름에 I/II/III를 붙이지 않는다. (장비는 티어를 쓴다)
            var names = All.Select(d => d.DisplayName).ToList();

            foreach (string n in names)
            {
                Assert.IsFalse(n.EndsWith(" I") || n.EndsWith(" II") || n.EndsWith(" III"),
                    $"{n} — Skill에는 티어를 두지 않습니다.");
            }
        }

        // ── 태그 게이팅 — 버려지는 선택지가 없어야 한다 ────────────────────

        [Test]
        public void 모든_Support는_붙을_수_있는_Core가_존재한다()
        {
            // ★ 태그 게이팅의 존재 이유 그 자체다.
            // 요구 태그를 만족하는 Core가 없으면 그 Support는 영원히 죽은 선택지가 된다.
            var coreTags = Of(SkillCategory.Core).Select(c => c.Tags).ToList();

            foreach (SkillDefinition s in Of(SkillCategory.Support))
            {
                if (s.RequiredTags == SkillTag.None)
                    continue;

                bool any = coreTags.Any(t => (t & s.RequiredTags) == s.RequiredTags);

                Assert.IsTrue(any, $"{s.Id}({s.DisplayName})의 요구 태그를 만족하는 Core가 없습니다.");
            }
        }

        [Test]
        public void 모든_Core는_붙일_수_있는_Support가_존재한다()
        {
            // 반대 방향. 소켓 3개가 항상 비어 있는 Core가 있으면 안 된다.
            var supports = Of(SkillCategory.Support).ToList();

            foreach (SkillDefinition c in Of(SkillCategory.Core))
            {
                int count = supports.Count(s =>
                    s.RequiredTags == SkillTag.None || (c.Tags & s.RequiredTags) == s.RequiredTags);

                Assert.GreaterOrEqual(count, SocketedBuild.SocketsPerCore,
                    $"{c.Id}({c.DisplayName})에 붙일 Support가 소켓 수보다 적습니다.");
            }
        }

        // ── Core 구조 ─────────────────────────────────────────────────────

        [Test]
        public void 부여_Core는_전부_투사체_태그와_생성_상태를_가진다()
        {
            var ailments = Of(SkillCategory.Core).Where(d => d.Family == CoreFamily.Ailment).ToList();

            Assert.AreEqual(5, ailments.Count, "부여 계열은 5종입니다.");

            foreach (SkillDefinition d in ailments)
            {
                Assert.IsTrue((d.Tags & SkillTag.Projectile) != 0,
                    $"{d.Id} — 부여 Core는 투사체 태그를 가져야 투사체 Support가 붙을 곳이 생깁니다.");
                Assert.AreNotEqual(StatusEffectType.None, d.CreatesStatus, $"{d.Id}가 만드는 상태가 없습니다.");
            }
        }

        [Test]
        public void 기폭_Core는_투사체_태그를_가지지_않는다()
        {
            var detonations = Of(SkillCategory.Core).Where(d => d.Family == CoreFamily.Detonation).ToList();

            Assert.AreEqual(3, detonations.Count, "기폭 계열은 3종입니다.");

            foreach (SkillDefinition d in detonations)
            {
                Assert.IsTrue((d.Tags & SkillTag.Projectile) == 0,
                    $"{d.Id} — 기폭 계열은 파동·잔류물이므로 투사체가 아닙니다.");
                Assert.IsTrue((d.Tags & SkillTag.AreaOfEffect) != 0, $"{d.Id}에 효과 범위 태그가 없습니다.");
            }
        }

        [Test]
        public void 상태이상_6종이_전부_어떤_Core로든_만들어진다()
        {
            // 만들 수단이 없는 상태는 죽은 어휘다.
            var created = Of(SkillCategory.Core).Select(d => d.CreatesStatus).ToList();

            foreach (StatusEffectType s in System.Enum.GetValues(typeof(StatusEffectType)))
            {
                if (s == StatusEffectType.None)
                    continue;

                Assert.Contains(s, created, $"{s}를 만드는 Core가 없습니다.");
            }
        }

        // ── 배타 구조 ─────────────────────────────────────────────────────

        [Test]
        public void 기능_배타_Support는_속성_5종_대칭이다()
        {
            // 배타형은 「차단」과 「소모」 두 형태가 있다. 다른 메커니즘이다.
            //   차단 — 그 상태를 만들 수 없다 (번제 / 감전 도약 / 격화되는 중독 / 유혈 충동)
            //   소모 — 만들어진 상태를 써 버린다 (살을 에는 서리)
            // 공통점은 단독으로 작동하지 않는다는 것이고, 그래서 통화가 같다.
            // 속성 계열만 센다. 「화염 조율」도 기능 배타 통화를 쓰지만
            // 요구 태그가 투사체라 속성 대칭의 일부가 아니다.
            const SkillTag Elements = SkillTag.Fire | SkillTag.Cold | SkillTag.Lightning
                                    | SkillTag.Chaos | SkillTag.Physical;

            var exclusives = Of(SkillCategory.Support)
                .Where(d => d.Cost == CostType.FunctionalExclusion && (d.RequiredTags & Elements) != 0)
                .ToList();

            Assert.AreEqual(5, exclusives.Count, "속성 5종 대칭이므로 5개여야 합니다.");

            foreach (SkillDefinition d in exclusives)
            {
                bool blocks = d.BlocksStatusCreation && d.BlockedStatus != StatusEffectType.None;
                bool consumes = d.ConsumesStatus != StatusEffectType.None;

                Assert.IsTrue(blocks || consumes,
                    $"{d.Id} — 기능 배타는 차단 또는 소모 중 하나를 명시해야 합니다.");
            }

            // 5종이 서로 다른 속성을 담당해야 대칭이 성립한다.
            var tags = exclusives.Select(d => d.RequiredTags).ToList();
            CollectionAssert.AllItemsAreUnique(tags);
        }

        [Test]
        public void 차단형_Support는_차단하는_상태를_명시한다()
        {
            var blockers = All.Where(d => d.BlocksStatusCreation).ToList();

            Assert.AreEqual(4, blockers.Count, "차단형은 4종입니다. (소모형 1종은 별도)");

            foreach (SkillDefinition d in blockers)
            {
                Assert.AreNotEqual(StatusEffectType.None, d.BlockedStatus, $"{d.Id}의 차단 상태가 비었습니다.");
                Assert.AreEqual(CostType.FunctionalExclusion, d.Cost,
                    $"{d.Id} — 차단은 통화도 기능 배타여야 합니다.");
            }
        }

        [Test]
        public void 배타형이_차단하는_상태는_전부_다른_수단으로_만들_수_있다()
        {
            // 배타형은 단독으로 작동하지 않는다. 상태를 만들 다른 수단이 반드시 있어야
            // "중복 금지 없이 조합이 강제되는" 구조가 성립한다.
            var creatable = Of(SkillCategory.Core).Select(d => d.CreatesStatus).ToList();

            foreach (SkillDefinition d in All.Where(x => x.BlocksStatusCreation))
                Assert.Contains(d.BlockedStatus, creatable, $"{d.Id}가 차단하는 상태를 만들 Core가 없습니다.");
        }

        [Test]
        public void 상호_배타는_양방향으로_지목한다()
        {
            // 한쪽만 지목하면 장착 순서에 따라 결과가 달라진다.
            var byId = All.ToDictionary(d => d.Id);

            foreach (SkillDefinition d in All)
            {
                foreach (string other in d.MutuallyExclusiveIds)
                {
                    Assert.IsTrue(byId.ContainsKey(other), $"{d.Id}가 존재하지 않는 {other}를 지목합니다.");

                    CollectionAssert.Contains(byId[other].MutuallyExclusiveIds, d.Id,
                        $"{other}가 {d.Id}를 마주 지목하지 않습니다.");
                }
            }
        }

        [Test]
        public void 긴_퓨즈와_짧은_퓨즈는_상호_배타다()
        {
            SkillDefinition longFuse = catalog.Find("sup_long_fuse");
            SkillDefinition shortFuse = catalog.Find("sup_short_fuse");

            Assert.IsNotNull(longFuse);
            Assert.IsNotNull(shortFuse);
            CollectionAssert.Contains(longFuse.MutuallyExclusiveIds, "sup_short_fuse");
            CollectionAssert.Contains(shortFuse.MutuallyExclusiveIds, "sup_long_fuse");
        }

        // ── Meta · 전령 ───────────────────────────────────────────────────

        [Test]
        public void 발동_스킬은_전부_발동_태그를_가진다()
        {
            foreach (SkillDefinition d in Of(SkillCategory.Meta))
                Assert.IsTrue((d.Tags & SkillTag.Trigger) != 0, $"{d.Id}에 발동 태그가 없습니다.");
        }

        [Test]
        public void 전령은_유지형_태그와_소모_상태를_가진다()
        {
            var heralds = Of(SkillCategory.Persistent).ToList();

            foreach (SkillDefinition d in heralds)
            {
                Assert.IsTrue(d.IsHerald, $"{d.Id}가 전령으로 판정되지 않습니다.");
                Assert.IsTrue((d.Tags & SkillTag.Persistent) != 0, $"{d.Id}에 유지형 태그가 없습니다.");
                Assert.AreNotEqual(StatusEffectType.None, d.ConsumesStatus,
                    $"{d.Id} — 전령은 조건부 처치 구조이므로 소모 상태가 있어야 합니다.");
            }

            // 5종이 서로 다른 상태를 담당해야 "한 속성으로 수렴하면 하나만 작동"이 성립한다.
            var states = heralds.Select(d => d.ConsumesStatus).ToList();
            CollectionAssert.AllItemsAreUnique(states);
        }

        // ── 요구 레벨 ─────────────────────────────────────────────────────

        [Test]
        public void 요구_레벨이_1_이상이고_Core는_반드시_Lv1부터_열린다()
        {
            foreach (SkillDefinition d in All)
                Assert.GreaterOrEqual(d.RequiredLevel, 1, $"{d.Id}의 요구 레벨이 1 미만입니다.");

            // Lv1에 고를 수 있는 Core가 없으면 런이 시작되지 않는다.
            Assert.IsTrue(Of(SkillCategory.Core).Any(d => d.RequiredLevel == 1),
                "요구 레벨 1인 Core가 하나도 없습니다.");
        }

        [Test]
        public void 초반_각성_레벨에서_끼울_수_있는_인자가_충분하다()
        {
            // 엣지 케이스 — 요구 레벨이 전부 높으면 초반에 주운 인자를 하나도
            // 끼우지 못한다. Lv3 시점에 열리는 자리는 Core 1 + 소켓 2 = 3개이므로
            // 그보다 넉넉한 후보가 있어야 파밍이 의미를 가진다.
            int earlyCount = All.Count(d => d.RequiredLevel <= 3);

            SocketCapacity atThree = SocketUnlockTable.Evaluate(3);

            Assert.Greater(earlyCount, atThree.TotalSlots,
                "Lv3 이하 인자가 그 시점에 열리는 자리 수보다 적습니다.");
        }
    }
}
