using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>
    /// 달 · 날씨 · 막이 계약 테스트 (결정 2-59 ~ 2-64 · 2-91).
    ///
    /// 【여기서 강제하는 것】
    ///   1. 달은 판마다 한 칸 돌고 여섯 칸이면 제자리다
    ///   2. 0장은 늘 맑음, 1장 첫 방문은 비, 눈은 겨울에만
    ///   3. 궂은 날은 계절마다 다르게 아프고, 그 계절의 막이로 막힌다
    ///   4. 6장 꽃비는 해가 없다
    ///   5. 추위는 쌓이고 녹는다
    /// </summary>
    public class WeatherTests
    {
        [TearDown]
        public void Clear() => NightClock.Reset();

        // ── 1. 달 ─────────────────────────────────────────────────────

        [Test]
        public void 달은_여섯_칸이면_제자리다()
        {
            MoonPhase moon = MoonPhase.New;
            for (int i = 0; i < MoonTable.Count; i++)
                moon = MoonTable.Next(moon);

            Assert.AreEqual(MoonPhase.New, moon);
            Assert.AreEqual(MoonPhase.WaxingCrescent, MoonTable.Next(MoonPhase.New));
        }

        [Test]
        public void 한_밤이_지나면_달이_한_칸_돈다()
        {
            NightClock.Reset();
            Assert.AreEqual(MoonPhase.New, NightClock.Moon, "새 게임은 삭에서 시작합니다.");

            NightClock.PassNight(new System.Random(1));
            Assert.AreEqual(MoonPhase.WaxingCrescent, NightClock.Moon);
        }

        [Test]
        public void 밤_시계가_세이브를_오간다()
        {
            NightClock.Set(MoonPhase.Full, WeatherSlot.Bad2, true);

            var data = new SaveData();
            NightClock.Capture(data);
            NightClock.Reset();
            NightClock.Restore(data);

            Assert.AreEqual(MoonPhase.Full, NightClock.Moon);
            Assert.AreEqual(WeatherSlot.Bad2, NightClock.Slot);
            Assert.IsTrue(NightClock.Snowy);
        }

        [Test]
        public void 망가진_세이브_값은_범위로_잘린다()
        {
            var data = new SaveData { moon = 99, weather = -3 };
            NightClock.Restore(data);

            Assert.AreEqual(MoonPhase.WaningCrescent, NightClock.Moon);
            Assert.AreEqual(WeatherSlot.Clear, NightClock.Slot);
        }

        // ── 2. 뽑기 · 장 ───────────────────────────────────────────────

        [Test]
        public void 날씨_확률은_100이다()
        {
            Assert.AreEqual(100, WeatherTable.ClearWeight + WeatherTable.CloudyWeight
                                 + WeatherTable.PrecipitationWeight + WeatherTable.Bad1Weight
                                 + WeatherTable.Bad2Weight);
        }

        [Test]
        public void 뽑으면_다섯_칸이_모두_나온다()
        {
            var seen = new System.Collections.Generic.HashSet<WeatherSlot>();
            var random = new System.Random(7);

            for (int i = 0; i < 2000; i++)
                seen.Add(WeatherTable.Roll(random));

            Assert.AreEqual(5, seen.Count);
        }

        [Test]
        public void 튜토리얼은_늘_맑다()
        {
            RaidWeather w = WeatherTable.For(0, WeatherSlot.Bad2, true, false);
            Assert.AreEqual(WeatherSlot.Clear, w.Slot);
            Assert.AreEqual(WeatherHazard.None, w.Hazard);
        }

        [Test]
        public void 일장_첫_방문은_비다()
        {
            Assert.AreEqual(WeatherSlot.Precipitation, WeatherTable.For(1, WeatherSlot.Bad2, false, true).Slot);
            Assert.AreEqual(WeatherSlot.Bad2, WeatherTable.For(1, WeatherSlot.Bad2, false, false).Slot);
        }

        [Test]
        public void 눈은_겨울에만_온다()
        {
            Assert.IsTrue(WeatherTable.For(1, WeatherSlot.Precipitation, true, false).Snow, "1장은 겨울입니다.");
            Assert.IsFalse(WeatherTable.For(2, WeatherSlot.Precipitation, true, false).Snow, "봄에 눈이 옵니다.");
        }

        // ── 3. 궂은 날 · 막이 ─────────────────────────────────────────

        [Test]
        public void 계절마다_궂은_날이_다르고_막이도_다르다()
        {
            var hazards = new System.Collections.Generic.HashSet<WeatherHazard>();
            var protections = new System.Collections.Generic.HashSet<ProtectionKind>();

            foreach (int chapter in new[] { 1, 2, 3, 4 })
            {
                RaidWeather w = WeatherTable.For(chapter, WeatherSlot.Bad1, false, false);
                Assert.AreNotEqual(WeatherHazard.None, w.Hazard, $"{chapter}장 궂은 날이 아프지 않습니다.");
                hazards.Add(w.Hazard);
                protections.Add(w.Protection);
                Assert.IsNotEmpty(w.Describe(), $"{chapter}장 궂은 날 설명이 없습니다.");
                Assert.IsNotEmpty(WeatherTable.ProtectionName(w.Protection));
            }

            Assert.AreEqual(4, hazards.Count, "네 계절이 같은 방식으로 아픕니다.");
            Assert.AreEqual(4, protections.Count, "네 계절이 같은 막이로 막힙니다.");
        }

        [Test]
        public void 막이_단계만큼_궂은_날이_약해진다()
        {
            RaidWeather bad2 = WeatherTable.For(1, WeatherSlot.Bad2, false, false);

            Assert.AreEqual(2, bad2.Deficit(0));
            Assert.AreEqual(1, bad2.Deficit(1));
            Assert.AreEqual(0, bad2.Deficit(2), "막이 2인데 아픕니다.");
            Assert.AreEqual(0, bad2.Deficit(5));

            Assert.AreEqual(0, WeatherTable.For(1, WeatherSlot.Cloudy, false, false).Deficit(0),
                "흐린 날이 아픕니다.");
        }

        [Test]
        public void 모자랄수록_더_아프다()
        {
            Assert.Greater(WeatherTable.DustInterval(1), WeatherTable.DustInterval(2));
            Assert.Less(WeatherTable.WaterDrainScale(1), WeatherTable.WaterDrainScale(2));
            Assert.Greater(WeatherTable.PlayerSightScale(1), WeatherTable.PlayerSightScale(2));
            Assert.AreEqual(1f, WeatherTable.WaterDrainScale(0), 0.001f);
            Assert.AreEqual(1f, WeatherTable.PlayerSightScale(0), 0.001f);
        }

        [Test]
        public void 막이마다_장비_수치와_소모품_상태가_있다()
        {
            foreach (ProtectionKind kind in new[] { ProtectionKind.Warmth, ProtectionKind.Shield,
                                                     ProtectionKind.Cool, ProtectionKind.Light })
            {
                Assert.AreNotEqual(EquipmentStatType.None, PlayerWeather.StatOf(kind), kind.ToString());

                StatusEffectType status = PlayerWeather.StatusOf(kind);
                Assert.AreNotEqual(StatusEffectType.None, status, kind.ToString());
                Assert.IsTrue(StatusEffectTable.IsBeneficial(status), $"{kind} 소모품이 해로운 상태로 걸립니다.");
                Assert.Greater(StatusEffectTable.Get(status).Duration, 0f, $"{kind} 소모품이 바로 사라집니다.");
            }
        }

        // ── 4. 꽃비 ───────────────────────────────────────────────────

        [Test]
        public void 육장_꽃비는_해가_없다()
        {
            RaidWeather w = WeatherTable.For(6, WeatherSlot.Bad2, false, false);

            Assert.IsTrue(w.IsFlowerRain);
            Assert.AreEqual(WeatherHazard.None, w.Hazard);
            Assert.AreEqual(0, w.Deficit(0));
        }

        // ── 5. 추위 ───────────────────────────────────────────────────

        [Test]
        public void 방한이_한_단계_모자라면_15분에_동상이다()
        {
            float stacks = 0f;
            for (int i = 0; i < 15 * 60; i++)
                stacks = WeatherTable.ColdStep(stacks, WeatherHazard.Cold, 1, false, 1f);

            Assert.AreEqual(WeatherTable.ColdMaxStacks, stacks, 0.5f);
        }

        [Test]
        public void 불_곁에서는_녹는다()
        {
            float near = WeatherTable.ColdStep(50f, WeatherHazard.Cold, 1, true, 1f);
            Assert.Less(near, 50f, "화로 곁인데 추위가 쌓입니다.");

            float calm = WeatherTable.ColdStep(50f, WeatherHazard.None, 0, false, 1f);
            Assert.Less(calm, 50f, "날이 풀렸는데 추위가 남습니다.");

            float guarded = WeatherTable.ColdStep(50f, WeatherHazard.Cold, 0, false, 1f);
            Assert.Less(guarded, 50f, "방한을 갖췄는데 추위가 녹지 않습니다.");

            Assert.AreEqual(0f, WeatherTable.ColdStep(1f, WeatherHazard.None, 0, false, 10f), 0.001f);
        }

        // ── 6. 보이는 것 ───────────────────────────────────────────────

        [Test]
        public void 미니맵_띠가_달_날씨_막이_추위를_적는다()
        {
            var c = new RaidConditions
            {
                lunar = true, moon = MoonPhase.Full,
                weather = WeatherTable.For(1, WeatherSlot.Bad2, false, false),
                traits = new RaidTrait[0]
            };

            string text = MinimapUI.WeatherText(c, 1, 1, 23.6f);

            StringAssert.Contains(MoonTable.Name(MoonPhase.Full), text);
            StringAssert.Contains("방한 1/2", text);
            StringAssert.Contains("추위 23", text);

            string calm = MinimapUI.WeatherText(RaidConditions.None, 0, 0, 0f);
            StringAssert.DoesNotContain("추위", calm);
            StringAssert.DoesNotContain("/", calm);
        }

        [Test]
        public void 비_눈_궂은_날은_입자가_있고_맑은_날은_없다()
        {
            Assert.IsFalse(WeatherEffects.SpecOf(WeatherTable.For(2, WeatherSlot.Clear, false, false)).on);
            Assert.IsTrue(WeatherEffects.SpecOf(WeatherTable.For(2, WeatherSlot.Precipitation, false, false)).on);
            Assert.IsTrue(WeatherEffects.SpecOf(WeatherTable.For(1, WeatherSlot.Precipitation, true, false)).on);
            Assert.IsTrue(WeatherEffects.SpecOf(WeatherTable.For(1, WeatherSlot.Bad1, false, false)).on, "한파에 눈이 없습니다.");
            Assert.IsTrue(WeatherEffects.SpecOf(WeatherTable.For(2, WeatherSlot.Bad1, false, false)).on, "흙비가 보이지 않습니다.");
            Assert.IsTrue(WeatherEffects.SpecOf(WeatherTable.For(6, WeatherSlot.Bad1, false, false)).on, "꽃비가 보이지 않습니다.");
            Assert.AreEqual(0f, FogOverlayUI.TintOf(RaidWeather.Calm).a, 0.001f, "맑은 날 화면이 물듭니다.");
        }
    }
}
