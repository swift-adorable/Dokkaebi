using System.Collections.Generic;

/// <summary>
/// 레벨업 선택창에 올릴 후보를 걸러내는 순수 필터. (마스터 프롬프트 10-10)
///
/// MonoBehaviour 의존이 없으므로 EditMode에서 전수 검증한다.
/// 필터 순서를 바꾸면 결과가 달라지므로 각 단계의 근거를 주석으로 고정한다.
/// </summary>
public static class SkillSelectionPool
{
    /// <summary>
    /// 적재(Loadout)에서 이번 레벨업에 제시 가능한 후보를 모은다.
    ///
    /// 걸러내는 순서:
    ///  1. null / 중복 항목
    ///  2. 요구 레벨 미달                     (v5 목록의 Lv)
    ///  3. 이미 획득                          (10-9 중복 등장 없음)
    ///  4. 카테고리별 수용 불가               (Core 2 / 소켓 / Meta 2 / 전령 1)
    ///     — Support의 태그 게이팅이 여기서 함께 적용된다. (10-2 [1])
    ///
    /// ※ 상호 배타(긴 퓨즈 ↔ 짧은 퓨즈)는 여기서 거르지 않는다.
    ///    v5 §7-4는 "획득은 자유롭되 동시 작동이 무의미하다"고 명시한다.
    ///    획득을 막으면 10-1-2(중복 금지로 다양성을 강제하지 않는다)를 위반한다.
    /// </summary>
    public static List<SkillDefinition> Build(
        IReadOnlyList<SkillDefinition> loadout,
        RunSkillState state,
        int playerLevel,
        List<SkillDefinition> result = null)
    {
        result ??= new List<SkillDefinition>();
        result.Clear();

        if (loadout == null)
            return result;

        for (int i = 0; i < loadout.Count; i++)
        {
            SkillDefinition definition = loadout[i];

            if (definition == null)
                continue;

            // 같은 에셋이 적재에 두 번 들어가 있어도 한 번만 센다.
            if (result.Contains(definition))
                continue;

            if (definition.RequiredLevel > playerLevel)
                continue;

            // state가 없으면 런 시작 직전 상태로 간주한다.
            if (state != null && !state.CanAcquire(definition))
                continue;

            result.Add(definition);
        }

        return result;
    }
}
