using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 어그로 선택의 계약 테스트. (docs/Blob_Hunting_System.md 4절)
    ///
    /// 【여기서 강제하는 것】
    ///   1. 먼저 문 대상을 계속 문다 — 맞아도 바꾸지 않는다
    ///   2. 우호는 먼저 싸우지 않고, 누구도 우호를 먼저 치지 않는다
    ///   3. 같은 소속끼리 싸우지 않는다
    /// </summary>
    public class AggroSelectorTests
    {
        private const int Self = 100;

        private static AggroCandidate Enemy(int id, Faction faction, float x, bool alive = true)
        {
            return new AggroCandidate
            {
                id = id, faction = faction,
                position = new Vector3(x, 0f, 0f),
                isPlayer = false, isAlive = alive
            };
        }

        private static AggroCandidate Player(float x, bool alive = true)
        {
            return new AggroCandidate
            {
                id = AggroSelector.PlayerId, faction = Faction.Friendly,
                position = new Vector3(x, 0f, 0f),
                isPlayer = true, isAlive = alive
            };
        }

        private static int Pick(Faction self, List<AggroCandidate> list,
                                int current = AggroSelector.NoTarget,
                                float detect = 18f, bool infinite = false)
        {
            return AggroSelector.Select(self, Vector3.zero, detect, current, infinite, list, Self);
        }

        // ── 1. 끈질김 ─────────────────────────────────────────────────

        [Test]
        public void 먼저_문_대상을_계속_문다()
        {
            // 문서 4절 — "먼저 어그로를 끈 대상이 있으면 자기가 공격당해도
            // 그 대상을 우선한다." 더 가까운 적이 나타나도 바꾸지 않는다.
            var list = new List<AggroCandidate>
            {
                Enemy(1, Faction.Subject, 15f),   // 물고 있는 대상 — 멀다
                Enemy(2, Faction.Subject, 2f)     // 훨씬 가깝다
            };

            Assert.AreEqual(1, Pick(Faction.Wild, list, current: 1),
                "가까운 적이 생겼다고 대상을 바꿨습니다. " +
                "맞을 때마다 바뀌면 난전에서 아무도 아무것도 죽이지 못합니다.");
        }

        [Test]
        public void 물고_있던_대상이_죽으면_새로_고른다()
        {
            var list = new List<AggroCandidate>
            {
                Enemy(1, Faction.Subject, 5f, alive: false),
                Enemy(2, Faction.Subject, 9f)
            };

            Assert.AreEqual(2, Pick(Faction.Wild, list, current: 1));
        }

        [Test]
        public void 너무_멀어지면_놓는다()
        {
            // 감지 18 · 기본 목줄 배수 2 → 36을 넘으면 놓는다.
            var far = new List<AggroCandidate> { Enemy(1, Faction.Subject, 40f) };

            Assert.AreEqual(AggroSelector.NoTarget, Pick(Faction.Wild, far, current: 1),
                "목줄 밖인데 계속 물고 있습니다.");

            // 보안기는 끝까지 쫓는다. (문서 8절)
            Assert.AreEqual(1, Pick(Faction.Facility, far, current: 1, infinite: true),
                "「한 번 추적하면 끝까지」가 지켜지지 않습니다.");
        }

        [Test]
        public void 대상이_없으면_감지_거리_안에서_가장_가까운_적대를_문다()
        {
            var list = new List<AggroCandidate>
            {
                Enemy(1, Faction.Subject, 12f),
                Enemy(2, Faction.Subject, 4f),
                Enemy(3, Faction.Subject, 30f)   // 감지 밖
            };

            Assert.AreEqual(2, Pick(Faction.Wild, list));
        }

        // ── 2. 우호 ───────────────────────────────────────────────────

        [Test]
        public void 우호는_먼저_싸우지_않는다()
        {
            var list = new List<AggroCandidate> { Enemy(1, Faction.Wild, 2f), Player(3f) };

            Assert.AreEqual(AggroSelector.NoTarget, Pick(Faction.Friendly, list),
                "우호가 먼저 공격합니다. 적대만 있으면 구역이 사격장이 됩니다.");
        }

        [Test]
        public void 누구도_우호를_먼저_치지_않는다()
        {
            var list = new List<AggroCandidate> { Enemy(1, Faction.Friendly, 2f) };

            foreach (Faction f in (Faction[])System.Enum.GetValues(typeof(Faction)))
            {
                Assert.AreEqual(AggroSelector.NoTarget, Pick(f, list),
                    $"{f}가 우호를 노렸습니다.");
            }
        }

        // ── 3. 소속 ───────────────────────────────────────────────────

        [Test]
        public void 같은_소속끼리_싸우지_않는다()
        {
            var list = new List<AggroCandidate> { Enemy(1, Faction.Wild, 2f) };

            Assert.AreEqual(AggroSelector.NoTarget, Pick(Faction.Wild, list));
        }

        [Test]
        public void 다른_소속이면_플레이어가_아니어도_문다()
        {
            // 어부지리 · 뒤통수 · 난전이 여기서 나온다.
            var list = new List<AggroCandidate> { Enemy(1, Faction.Facility, 3f) };

            Assert.AreEqual(1, Pick(Faction.Wild, list));
        }

        [Test]
        public void 우호를_뺀_모든_진영이_플레이어를_노린다()
        {
            var list = new List<AggroCandidate> { Player(5f) };

            foreach (Faction f in (Faction[])System.Enum.GetValues(typeof(Faction)))
            {
                int expected = f == Faction.Friendly
                    ? AggroSelector.NoTarget
                    : AggroSelector.PlayerId;

                Assert.AreEqual(expected, Pick(f, list), $"{f}");
            }
        }

        [Test]
        public void 자기_자신은_고르지_않는다()
        {
            var list = new List<AggroCandidate> { Enemy(Self, Faction.Facility, 1f) };

            Assert.AreEqual(AggroSelector.NoTarget, Pick(Faction.Wild, list));
        }

        [Test]
        public void 더_가까운_적대와_더_먼_플레이어_중_가까운_쪽을_문다()
        {
            // 플레이어를 특별 취급하지 않는다. 그래야 난전이 성립한다.
            var list = new List<AggroCandidate> { Enemy(1, Faction.Facility, 3f), Player(10f) };

            Assert.AreEqual(1, Pick(Faction.Wild, list));
        }
    }
}
