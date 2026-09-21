using UnityEngine;

/// <summary>
/// 플레이어가 지금 내는 소리의 반경. (감사 A11 · 문서 8절)
///
/// 【소리의 크기는 내는 쪽이 정한다.】
/// 듣는 쪽마다 청력을 두면 같은 총성이 누구에겐 들리고 누구에겐 안 들려,
/// 유저가 「이 소리가 어디까지 갔나」를 예측할 수 없다.
/// 덕코프도 「무기의 소리 범위만큼 퍼지고 그 안의 적이 듣는다」다.
/// [확인됨 — research/duckov/08_전투_실측과_교전.md 3절]
///
/// 이 값이 없던 동안 이어폰 6종과 소음 관련 장비는 아무 일도 하지 않았다.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerNoise : MonoBehaviour
{
    /// <summary>
    /// 가만히 서 있을 때의 소리 반경(m). 0이면 완전 무음이다.
    ///
    /// 【불확실 — 문서에 수치가 없다.】
    /// 0으로 둔다. 서 있기만 해도 들킨다면 「멈춘다」가 선택지가 되지 않는다.
    /// </summary>
    [SerializeField] private float idleRadius = 0f;

    [Tooltip("걸을 때의 소리 반경(m). 적 기본 시야 18m보다 작게 둔다 — " +
             "소리가 시야보다 멀리 가면 시야각이라는 축이 무의미해진다.")]
    [Min(0f)]
    [SerializeField] private float movingRadius = 9f;

    [Tooltip("대시 직후의 소리 반경(m).")]
    [Min(0f)]
    [SerializeField] private float dashRadius = 16f;

    [Tooltip("사격 한 발이 내는 소리 반경(m). 무기별 수치가 생기면 거기서 받는다.")]
    [Min(0f)]
    [SerializeField] private float shotRadius = 22f;

    [Tooltip("한 번 낸 큰 소리가 남아 있는 시간(초). 즉시 사라지면 적이 찾아올 틈이 없다.")]
    [Min(0.05f)]
    [SerializeField] private float decayDuration = 0.6f;

    private PlayerMovement movement;
    private PlayerLoadout loadout;

    /// <summary>대시·사격처럼 순간적으로 난 큰 소리. 시간이 지나면 사그라든다.</summary>
    private float burstRadius;
    private float burstUntil;

    /// <summary>지금 이 순간의 소리 반경(m). 적의 Perception이 읽는다.</summary>
    public float Radius { get; private set; }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        loadout = GetComponent<PlayerLoadout>();
    }

    private void Update()
    {
        float scale = loadout != null ? loadout.Current.MoveSoundScale : 1f;

        bool moving = movement != null && movement.MoveDirection.sqrMagnitude > 0.01f;

        float sustainedBase = moving ? movingRadius : idleRadius;

        float sustained = sustainedBase * scale;

        if (Time.time >= burstUntil)
            burstRadius = 0f;

        // 【큰 소리는 장비로 줄이지 않는다.】
        // MoveSoundRange는 「발소리」를 줄이는 축이다. 총성까지 같이 줄이면
        // 방어구 하나로 소음기까지 대신하게 되어 부착물 축이 죽는다.
        Radius = Mathf.Max(sustained, burstRadius);
    }

    /// <summary>대시했다. 큰 소리가 한 번 난다.</summary>
    public void ReportDash() => Emit(dashRadius);

    /// <summary>쐈다. 총성이 퍼진다.</summary>
    public void ReportShot() => Emit(shotRadius);

    /// <summary>임의의 소리를 낸다. 조약돌 같은 유인 도구가 쓸 자리다.</summary>
    public void Emit(float radius)
    {
        if (radius <= burstRadius && Time.time < burstUntil)
            return;

        burstRadius = Mathf.Max(0f, radius);
        burstUntil = Time.time + decayDuration;
    }
}
