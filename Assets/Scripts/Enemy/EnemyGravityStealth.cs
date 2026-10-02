using UnityEngine;

/// <summary>
/// 허깨비의 기믹 — 【끌어당기고, 가까이 오기 전까지 숨는다】.
/// (docs/Blob_Hunting_System.md 1절 · EnemyGimmickTable)
///
/// 【왜 둘이 한 컴포넌트인가】
/// 둘이 같은 답을 요구하기 때문이다 — 「거리를 벌리지 못한다」.
/// 은신은 거리를 두고 처리할 기회를 빼앗고, 중력은 벌린 거리를 되돌린다.
/// 나눠 두면 한쪽만 붙은 허깨비가 생기고, 그러면 답이 둘로 갈린다.
///
/// 【스토리와도 이어진다.】 허깨비는 4장에서 「내가 될 뻔한 것들」로 밝혀진다.
/// 끌어당기는 것이 그 유형의 성격이다 — 놓아주지 않는다.
/// </summary>
[RequireComponent(typeof(EnemyAttack))]
public class EnemyGravityStealth : MonoBehaviour
{
    private EnemyAttack attack;
    private EnemyAggro aggro;

    private Renderer[] renderers;

    /// <summary>지금 보이는가. 매 프레임 Renderer를 켜고 끄지 않으려고 들고 있는다.</summary>
    private bool visible = true;

    private void Awake()
    {
        attack = GetComponent<EnemyAttack>();
        aggro = GetComponent<EnemyAggro>();

        renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    private void OnEnable()
    {
        // 풀에서 돌아오면 다시 숨은 상태로 시작한다.
        SetVisible(true);
    }

    private void Update()
    {
        GimmickSpec spec = EnemyGimmickTable.Get(attack.Gimmick);

        if (spec.RevealRange <= 0f && spec.PullStrength <= 0f)
            return;

        Transform target = FindTarget();

        if (target == null)
        {
            SetVisible(true);
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;

        // 은신 — 가까워지면 드러난다. 【눈앞에서 나타난다.】
        if (spec.RevealRange > 0f)
            SetVisible(distance <= spec.RevealRange);

        // 중력 — 대상을 이쪽으로 당긴다. 드러난 뒤에만 당긴다.
        // 보이지도 않는데 끌려가면 무엇이 붙잡는지 알 수 없다.
        if (spec.PullStrength <= 0f || !visible || distance < 0.2f)
            return;

        Pull(target, -toTarget.normalized, spec.PullStrength);
    }

    /// <summary>
    /// 당긴다. 【CharacterController든 Rigidbody든 대상이 무엇이든 통해야 한다.】
    /// 대상의 이동 컴포넌트를 알면 허깨비가 플레이어 구현에 묶인다.
    /// </summary>
    private static void Pull(Transform target, Vector3 direction, float strength)
    {
        Vector3 delta = direction * (strength * Time.deltaTime);

        if (target.TryGetComponent(out CharacterController controller))
        {
            controller.Move(delta);
            return;
        }

        if (target.TryGetComponent(out Rigidbody body))
        {
            body.MovePosition(body.position + delta);
            return;
        }

        target.position += delta;
    }

    private Transform FindTarget()
    {
        if (aggro != null && aggro.Target != null)
            return aggro.Target;

        return EnemyManager.HasInstance ? EnemyManager.Instance.PlayerTransform : null;
    }

    private void SetVisible(bool value)
    {
        if (visible == value || renderers == null)
            return;

        visible = value;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = value;
        }
    }
}
