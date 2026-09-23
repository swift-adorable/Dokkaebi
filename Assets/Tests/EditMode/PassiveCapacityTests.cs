using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Blob.Tests
{
    /// <summary>
    /// 칸 축(칸·중량)의 계약 테스트.
    ///
    /// 가방 칸과 소지 중량은 【장비】와 【패시브】 두 곳에서 동시에 늘어나는
    /// 유일한 축이다. 겹치는 축은 방치하면 한쪽이 다른 쪽을 삼킨다 —
    /// 패시브가 커지면 "가방 티어를 무엇으로 고를까"가 사라지고,
    /// 가방 티어 6종의 성격 차이(고중량형 30kg/10칸 · 소형물형 12kg/24칸)가 묻힌다.
    ///
    /// 그래서 상한을 문서에만 두지 않고 여기서 강제한다.
    /// (docs/Blob_Passive_System.md 5절 · Blob_Audit.md)
    ///
    /// 【1/2 라는 숫자의 근거】
    /// 덕코프 퍽은 가방 공간 +31칸 / 소지 중량 +37kg를 주어 장비 최대치
    /// (가방 +30칸 / +30kg)와 거의 1:1이다. [확인됨 — escapefromduckov.net 퍽 표]
    /// 다만 Lv.17~40에 걸쳐 열리는 후반 축이라 초중반에는 장비가 사실상 전부다.
    /// Blob은 그 1:1을 그대로 쓰지 않고 절반에서 끊는다.
    /// </summary>
    public class PassiveCapacityTests
    {
        private const string PassiveRoot = "Assets/Data/ScriptableObjects/Passives";
        private const string ItemRoot = "Assets/Data/ScriptableObjects/Items";

        /// <summary>패시브 총합이 장비 최대치의 몇 배까지 허용되는가.</summary>
        private const float PassiveShareCap = 0.5f;

        private static List<T> Load<T>(string folder) where T : UnityEngine.Object
        {
            return AssetDatabase
                .FindAssets($"t:{typeof(T).Name}", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToList();
        }

        private static float PassiveTotal(PassiveEffectType effect)
        {
            return Load<PassiveNode>(PassiveRoot)
                .Where(n => n.Effect == effect)
                .Sum(n => n.Value);
        }

        /// <summary>
        /// 장비로 낼 수 있는 최대치. 슬롯이 다르면 동시에 낄 수 있으므로
        /// 슬롯별 최댓값을 더한다. (가방 + 몸통이 둘 다 칸을 준다)
        /// </summary>
        private static float EquipmentBest(EquipmentStatType stat)
        {
            List<EquipmentDefinition> all = Load<EquipmentDefinition>(ItemRoot);

            Assert.IsNotEmpty(all,
                "장비 에셋이 없습니다. Blob/Equipment/에셋 생성을 먼저 실행하세요.");

            float total = 0f;

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                float best = all
                    .Where(d => d.Slot == slot)
                    .Select(d => d.GetStat(stat))
                    .DefaultIfEmpty(0f)
                    .Max();

                if (best > 0f)
                    total += best;
            }

            return total;
        }

        // ── 상한 ──────────────────────────────────────────────────────────

        [Test]
        public void 패시브_가방칸은_장비_최대치의_절반을_넘지_않는다()
        {
            float passive = PassiveTotal(PassiveEffectType.CarrySlots);
            float equipment = EquipmentBest(EquipmentStatType.SlotCapacity);

            Assert.Greater(equipment, 0f, "장비가 주는 칸이 0입니다.");

            Assert.LessOrEqual(passive, equipment * PassiveShareCap,
                $"패시브 칸 총합 {passive}은 장비 최대치 {equipment}의 " +
                $"{PassiveShareCap:P0}({equipment * PassiveShareCap})를 넘습니다. " +
                "가방 티어를 고르는 결정이 사라집니다. " +
                "(docs/Blob_Passive_System.md 5절)");
        }

        [Test]
        public void 패시브_소지중량은_장비_최대치의_절반을_넘지_않는다()
        {
            float passive = PassiveTotal(PassiveEffectType.CarryWeight);
            float equipment = EquipmentBest(EquipmentStatType.MaxCarryWeight);

            Assert.Greater(equipment, 0f, "장비가 주는 중량이 0입니다.");

            Assert.LessOrEqual(passive, equipment * PassiveShareCap,
                $"패시브 중량 총합 {passive}kg은 장비 최대치 {equipment}kg의 " +
                $"{PassiveShareCap:P0}({equipment * PassiveShareCap}kg)를 넘습니다. " +
                "(docs/Blob_Passive_System.md 5절)");
        }

        // ── 하한 ──────────────────────────────────────────────────────────
        // 상한만 두면 "0으로 만들면 언제나 통과"가 된다.
        // 패시브가 칸을 늘릴 수 있다는 것 자체가 설계다.

        [Test]
        public void 패시브는_칸_두_축을_실제로_늘린다()
        {
            Assert.Greater(PassiveTotal(PassiveEffectType.CarrySlots), 0f,
                "패시브에 CarrySlots가 하나도 없습니다.");

            Assert.Greater(PassiveTotal(PassiveEffectType.CarryWeight), 0f,
                "패시브에 CarryWeight가 하나도 없습니다.");
        }

        // ── 맨몸 하한 ─────────────────────────────────────────────────────
        // 가방을 잃어도 게임이 성립해야 한다.

        [Test]
        public void 맨몸_기본치가_덕코프_기준을_지킨다()
        {
            Assert.GreaterOrEqual(PlayerInventory.BaseSlots, 20,
                "맨몸 20칸 미만은 첫 출격에서 아무것도 못 줍습니다. " +
                "(덕코프 기본 가방 기준)");

            Assert.GreaterOrEqual(PlayerInventory.BaseWeightLimit, 30f,
                "맨몸 30kg 미만은 무기 하나로 과중량이 됩니다.");
        }
    }
}
