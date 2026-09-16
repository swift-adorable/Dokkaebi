using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 테스트용 MutationDefinition을 코드로 만들어 주는 헬퍼. (v5 스키마)
    ///
    /// SerializedObject를 쓰므로 에디터 전용이다. MonoBehaviour가 아니라
    /// ScriptableObject이므로 에디터 어셈블리에 있어도 문제가 없다.
    /// (MonoBehaviour는 에디터 어셈블리에 두면 AddComponent가 조용히 null을 반환한다)
    /// </summary>
    public static class MutationTestFactory
    {
        public static MutationDefinition CreateCore(
            string id,
            MutationTag tags = MutationTag.Projectile,
            int requiredLevel = 1,
            CoreFamily family = CoreFamily.Delivery,
            StatusEffectType createsStatus = StatusEffectType.None,
            ProjectileBehaviourType behaviour = ProjectileBehaviourType.None,
            int behaviourCharges = 0)
        {
            return Create(id, MutationCategory.Core, tags,
                requiredLevel: requiredLevel,
                family: family,
                createsStatus: createsStatus,
                behaviour: behaviour,
                behaviourCharges: behaviourCharges);
        }

        public static MutationDefinition CreateSupport(
            string id,
            MutationTag requiredTags = MutationTag.None,
            int requiredLevel = 1,
            CostType cost = CostType.None,
            ProjectileBehaviourType behaviour = ProjectileBehaviourType.None,
            int behaviourCharges = 0,
            int extraProjectiles = 0,
            float fireIntervalMultiplier = 1f,
            float lifetimeMultiplier = 1f,
            float speedMultiplier = 1f,
            string[] mutuallyExclusiveIds = null,
            bool blocksStatusCreation = false,
            StatusEffectType blockedStatus = StatusEffectType.None)
        {
            return Create(id, MutationCategory.Support, MutationTag.None,
                requiredTags: requiredTags,
                requiredLevel: requiredLevel,
                cost: cost,
                behaviour: behaviour,
                behaviourCharges: behaviourCharges,
                extraProjectiles: extraProjectiles,
                fireIntervalMultiplier: fireIntervalMultiplier,
                lifetimeMultiplier: lifetimeMultiplier,
                speedMultiplier: speedMultiplier,
                mutuallyExclusiveIds: mutuallyExclusiveIds,
                blocksStatusCreation: blocksStatusCreation,
                blockedStatus: blockedStatus);
        }

        public static MutationDefinition CreateMeta(
            string id,
            MetaTriggerKind kind = MetaTriggerKind.Automatic,
            int requiredLevel = 11)
        {
            return Create(id, MutationCategory.Meta, MutationTag.Trigger,
                requiredLevel: requiredLevel,
                metaKind: kind);
        }

        public static MutationDefinition CreatePersistent(
            string id,
            int nucleusCost,
            int requiredLevel = 1)
        {
            return Create(id, MutationCategory.Persistent, MutationTag.Persistent,
                requiredLevel: requiredLevel,
                nucleusCost: nucleusCost);
        }

        public static MutationDefinition Create(
            string id,
            MutationCategory category = MutationCategory.Core,
            MutationTag tags = MutationTag.None,
            MutationTag requiredTags = MutationTag.None,
            int requiredLevel = 1,
            CoreFamily family = CoreFamily.None,
            MetaTriggerKind metaKind = MetaTriggerKind.None,
            StatusEffectType createsStatus = StatusEffectType.None,
            StatusEffectType consumesStatus = StatusEffectType.None,
            GroundEffectType createsGroundEffect = GroundEffectType.None,
            bool blocksStatusCreation = false,
            StatusEffectType blockedStatus = StatusEffectType.None,
            string[] mutuallyExclusiveIds = null,
            int nucleusCost = 0,
            CostType cost = CostType.None,
            string costDescription = "",
            ProjectileBehaviourType behaviour = ProjectileBehaviourType.None,
            int behaviourCharges = 0,
            int extraProjectiles = 0,
            float spreadAngle = 8f,
            float fireIntervalMultiplier = 1f,
            float lifetimeMultiplier = 1f,
            float speedMultiplier = 1f)
        {
            var definition = ScriptableObject.CreateInstance<MutationDefinition>();
            definition.name = id;

            var serialized = new SerializedObject(definition);

            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("description").stringValue = id + " 설명";

            serialized.FindProperty("category").intValue = (int)category;
            serialized.FindProperty("coreFamily").intValue = (int)family;
            serialized.FindProperty("metaKind").intValue = (int)metaKind;
            serialized.FindProperty("requiredLevel").intValue = requiredLevel;

            serialized.FindProperty("tags").intValue = (int)tags;
            serialized.FindProperty("requiredTags").intValue = (int)requiredTags;

            serialized.FindProperty("createsStatus").intValue = (int)createsStatus;
            serialized.FindProperty("consumesStatus").intValue = (int)consumesStatus;
            serialized.FindProperty("createsGroundEffect").intValue = (int)createsGroundEffect;

            serialized.FindProperty("blocksStatusCreation").boolValue = blocksStatusCreation;
            serialized.FindProperty("blockedStatus").intValue = (int)blockedStatus;

            SerializedProperty exclusive = serialized.FindProperty("mutuallyExclusiveIds");
            int exclusiveCount = mutuallyExclusiveIds?.Length ?? 0;
            exclusive.arraySize = exclusiveCount;

            for (int i = 0; i < exclusiveCount; i++)
                exclusive.GetArrayElementAtIndex(i).stringValue = mutuallyExclusiveIds[i];

            serialized.FindProperty("nucleusCost").intValue = nucleusCost;

            serialized.FindProperty("costType").intValue = (int)cost;
            serialized.FindProperty("costDescription").stringValue = costDescription;

            serialized.FindProperty("grantedBehaviour").intValue = (int)behaviour;
            serialized.FindProperty("behaviourCharges").intValue = behaviourCharges;
            serialized.FindProperty("extraProjectiles").intValue = extraProjectiles;
            serialized.FindProperty("spreadAngle").floatValue = spreadAngle;
            serialized.FindProperty("fireIntervalMultiplier").floatValue = fireIntervalMultiplier;
            serialized.FindProperty("lifetimeMultiplier").floatValue = lifetimeMultiplier;
            serialized.FindProperty("speedMultiplier").floatValue = speedMultiplier;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }
    }
}
