using UnityEditor;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// 테스트용 MutationDefinition을 코드로 만들어 주는 헬퍼.
    ///
    /// SerializedObject를 쓰므로 에디터 전용이다. MonoBehaviour가 아니라
    /// ScriptableObject이므로 에디터 어셈블리에 있어도 문제가 없다.
    /// (MonoBehaviour는 에디터 어셈블리에 두면 AddComponent가 실패한다)
    /// </summary>
    public static class MutationTestFactory
    {
        public static MutationDefinition Create(
            string id,
            MutationRarity rarity = MutationRarity.Common,
            int maxStacks = 1,
            ProjectileBehaviourType behaviour = ProjectileBehaviourType.None,
            int chargesPerStack = 1,
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
            serialized.FindProperty("rarity").enumValueIndex = (int)rarity;
            serialized.FindProperty("maxStacks").intValue = maxStacks;
            serialized.FindProperty("grantedBehaviour").enumValueIndex = (int)behaviour;
            serialized.FindProperty("chargesPerStack").intValue = chargesPerStack;
            serialized.FindProperty("extraProjectilesPerStack").intValue = extraProjectiles;
            serialized.FindProperty("spreadAngle").floatValue = spreadAngle;
            serialized.FindProperty("fireIntervalMultiplier").floatValue = fireIntervalMultiplier;
            serialized.FindProperty("lifetimeMultiplier").floatValue = lifetimeMultiplier;
            serialized.FindProperty("speedMultiplier").floatValue = speedMultiplier;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }
    }
}
