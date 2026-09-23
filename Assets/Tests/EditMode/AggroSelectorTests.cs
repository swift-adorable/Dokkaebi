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

        private static AggroCandidate Enemy(int id, Faction faction, float x,
                                            bool alive = true, bool detected = true)
        {
            return new AggroCandidate
            {
                id = id, faction = faction,
                position = new Vector3(x, 0f, 0f),
                isPlayer = false, isAlive = alive,
                isDetected = detected
            };
        }

        private static AggroCandidate Player(float x, bool alive = true, bool detected = true)
        {
            return new AggroCandidate
            {
                id = AggroSelector.PlayerId, faction = Faction.Friendly,
                position = new Vector3(x, 0f, 0f),
                isPlayer = true, isAlive = alive,
                isDetected = detected
            };
        }

        /// <summary>기억을 직접 들고 여러 번 판단한다. 망각 시간을 흘릴 때 쓴다.</summary>
        private static int Pick(Faction self, List<AggroCandidate> list, ref AggroMemory memory,
                                float detect = 18f,
                                float forget = AggroSelector.DefaultForgetTime,
                                float forcedChase = 0f, float elapsed = 0f)
        {
            var pursuit = new AggroPursuit
            {
                forgetTime = forget,
                forcedChaseRange = forcedChase,
                safetyRange = detect * AggroSelector.DefaultSafetyMultiplier
            };

            return AggroSelector.Select(
                self, Vector3.zero, in pursuit, ref memory, elapsed, list, Self);
        }

        /// <summary>한 번만 판단한다. 기억이 필요 없는 테스트가 쓴다.</summary>
        private static int Pick(Faction self, List<AggroCandidate> list,
                                int current = AggroSelector.NoTarget,
                                float detect = 18f,
                                float forget = AggroSelector.DefaultForgetTime,
                                float forcedChase = 0f, float elapsed = 0f)
        {
            var memory = new AggroMemory { targetId = current, unseenTime = 0f };

            return Pick(self, list, ref memory, detect, forget, forcedChase, elapsed);
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
        public void 최후_방어선_거리를_넘으면_즉시_놓는다()
        {
            // 감지 18 · 안전 배율 3 → 54m를 넘으면 시간과 무관하게 놓는다.
            //
            // 【detected: false인 것이 핵심이다.】
            // 처음에는 detected를 기본값(true)으로 두고 「놓는다」를 기대했는데
            // 놓자마자 다시 물어서 실패했다. 그게 맞는 동작이다 —
            // 그 거리에서도 감지하고 있다면(예: 총성) 다시 무는 것이 옳다.
            //
            // 이 거리는 「추적을 그만두는 조건」이 아니다. 그 일은 망각 시간이 한다.
            // 여기 있는 건 지형에 끼거나 길을 못 찾은 적이 맵 반대편까지
            // 따라오는 사고를 막는 최후 방어선이다.
            var far = new List<AggroCandidate> { Enemy(1, Faction.Subject, 60f, detected: false) };

            Assert.AreEqual(AggroSelector.NoTarget, Pick(Faction.Wild, far, current: 1),
                "최후 방어선 밖인데 계속 물고 있습니다.");

            // 강제 추적 거리가 아무리 넓어도 최후 방어선을 이기지 못한다.
            Assert.AreEqual(AggroSelector.NoTarget,
                Pick(Faction.Facility, far, current: 1, forcedChase: 999f),
                "강제 추적이 최후 방어선을 무시했습니다. 낀 적을 떼어낼 방법이 없어집니다.");
        }

        // ── 1-2. 망각 시간 ───────────────────────────────────────────
        // 【거리 대신 시간으로 놓는 이유】
        // 거리로만 판정하면 플레이어가 적보다 느릴 때 — 짐을 잔뜩 든 순간 —
        // 교전을 피할 방법이 아예 없다. 추출 루팅에서 가장 중요한 순간이다.
        // 시간으로 두면 「엄폐물 뒤에서 버틴다」가 유효한 수가 된다.
        // 덕코프도 망각 시간으로 판정한다(8×35 · 22×16 · 30×4).
        // [확인됨 — docs/research/duckov/05_적_AI_실측치.md 2절]

        [Test]
        public void 감지를_잃은_채_망각_시간이_지나면_놓는다()
        {
            var list = new List<AggroCandidate> { Enemy(1, Faction.Subject, 5f, detected: false) };

            var memory = new AggroMemory { targetId = 1, unseenTime = 0f };

            // 8초를 4초씩 두 번에 나눠 흘린다. 첫 번째는 아직 물고 있어야 한다.
            Assert.AreEqual(1, Pick(Faction.Wild, list, ref memory, forget: 8f, elapsed: 4f),
                "4초 만에 잊었습니다. 엄폐 한 번에 추적이 끊깁니다.");

            Assert.AreEqual(AggroSelector.NoTarget,
                Pick(Faction.Wild, list, ref memory, forget: 8f, elapsed: 4f),
                "8초가 지났는데 계속 물고 있습니다.");
        }

        [Test]
        public void 다시_감지하면_망각_시간이_초기화된다()
        {
            var lost = new List<AggroCandidate> { Enemy(1, Faction.Subject, 5f, detected: false) };
            var seen = new List<AggroCandidate> { Enemy(1, Faction.Subject, 5f, detected: true) };

            var memory = new AggroMemory { targetId = 1, unseenTime = 0f };

            Pick(Faction.Wild, lost, ref memory, forget: 8f, elapsed: 7f);   // 7초 놓침
            Pick(Faction.Wild, seen, ref memory, forget: 8f, elapsed: 1f);   // 다시 봤다

            Assert.AreEqual(0f, memory.unseenTime, 0.001f, "다시 봤는데 시간이 남아 있습니다.");

            Assert.AreEqual(1, Pick(Faction.Wild, lost, ref memory, forget: 8f, elapsed: 7f),
                "다시 본 뒤에도 옛 시간이 이어졌습니다.");
        }

        [Test]
        public void 강제_추적_거리_안에서는_잊지_않는다()
        {
            // 보안기 — 40m 안에서는 보지 못해도 놓지 않는다.
            // 옛 chasesForever 불리언을 대신하는 축이다. 「끝까지」가 아니라
            // 「여기까지」라서, 플레이어에게 도망이라는 선택지가 남는다.
            var near = new List<AggroCandidate> { Enemy(1, Faction.Subject, 30f, detected: false) };

            var memory = new AggroMemory { targetId = 1, unseenTime = 0f };

            Assert.AreEqual(1,
                Pick(Faction.Wild, near, ref memory, forget: 8f, forcedChase: 40f, elapsed: 100f),
                "강제 추적 거리 안인데 잊었습니다.");

            Assert.AreEqual(0f, memory.unseenTime, 0.001f,
                "강제 추적 거리 안인데 망각 시간이 흘렀습니다.");
        }

        [Test]
        public void 강제_추적_거리_밖이면_망각_시간이_흐른다()
        {
            var outside = new List<AggroCandidate> { Enemy(1, Faction.Subject, 45f, detected: false) };

            Assert.AreEqual(AggroSelector.NoTarget,
                Pick(Faction.Wild, outside, current: 1, forget: 8f, forcedChase: 40f, elapsed: 9f),
                "강제 추적 거리 밖인데 놓지 않습니다.");
        }

        [Test]
        public void 대상이_없으면_감지_거리_안에서_가장_가까운_적대를_문다()
        {
            var list = new List<AggroCandidate>
            {
                Enemy(1, Faction.Subject, 12f),
                Enemy(2, Faction.Subject, 4f),
                Enemy(3, Faction.Subject, 30f, detected: false)   // 감지 못 함
            };

            Assert.AreEqual(2, Pick(Faction.Wild, list));
        }

        [Test]
        public void 감지하지_못한_대상은_물지_않는다()
        {
            // 【이것이 잠입이다.】 거리 안에 있어도 보지도 듣지도 못했으면 모른다.
            var list = new List<AggroCandidate> { Enemy(1, Faction.Subject, 2f, detected: false) };

            Assert.AreEqual(AggroSelector.NoTarget, Pick(Faction.Wild, list),
                "감지하지 못한 적을 물었습니다. 시야각도 소리도 의미가 없어집니다.");
        }

        [Test]
        public void 이미_문_대상은_감지를_잃어도_바로_놓지_않는다()
        {
            // 시야에서 잠깐 벗어났다고 즉시 잊으면, 엄폐물 뒤로 한 걸음만
            // 움직여도 추적이 끊긴다. 놓는 시점은 망각 시간이 정한다.
            var list = new List<AggroCandidate> { Enemy(1, Faction.Subject, 5f, detected: false) };

            Assert.AreEqual(1, Pick(Faction.Wild, list, current: 1));
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

        // ── 4. 탄의 대상 판정 ─────────────────────────────────────────
        // BulletController.CanHit이 쓰는 규칙과 같은 판정이다.
        // 탄은 MonoBehaviour라 EditMode에서 직접 못 돌리므로,
        // 【그 안에서 부르는 판정 함수】를 여기서 강제한다.

        [Test]
        public void 적_탄은_같은_소속을_맞히지_않는다()
        {
            Assert.IsFalse(FactionTable.IsHostile(Faction.Wild, Faction.Wild),
                "같은 소속이 서로를 쏩니다.");

            Assert.IsTrue(FactionTable.IsHostile(Faction.Wild, Faction.Facility),
                "다른 소속을 못 쏩니다. 난전이 성립하지 않습니다.");
        }

        [Test]
        public void 적_탄은_우호를_맞히지_않는다()
        {
            foreach (Faction f in (Faction[])System.Enum.GetValues(typeof(Faction)))
                Assert.IsFalse(FactionTable.IsHostile(f, Faction.Friendly), $"{f} → 우호");
        }

        [Test]
        public void 우호가_쏜_탄은_플레이어를_맞히지_않는다()
        {
            Assert.IsFalse(FactionTable.IsHostileToPlayer(Faction.Friendly));

            foreach (Faction f in (Faction[])System.Enum.GetValues(typeof(Faction)))
            {
                if (f == Faction.Friendly)
                    continue;

                Assert.IsTrue(FactionTable.IsHostileToPlayer(f), $"{f}가 플레이어를 못 쏩니다.");
            }
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
