using UnityEngine;

/// <summary>
/// MonoBehaviour 싱글턴 공용 베이스 클래스.
///
/// Unity는 Awake() 실행 순서를 보장하지 않으므로, 다른 컴포넌트가 먼저 Instance에
/// 접근하면 null이 될 수 있다. 이를 막기 위해 최초 접근 시 씬에서 지연 탐색(Lazy Find)한다.
/// </summary>
/// <typeparam name="T">싱글턴으로 동작할 구체 타입</typeparam>
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T instance;
    private static bool isApplicationQuitting;
    private static bool hasWarnedMissing;

    /// <summary>인스턴스 탐색 없이 현재 캐시된 참조 유무만 확인한다.</summary>
    public static bool HasInstance => !isApplicationQuitting && instance != null;

    public static T Instance
    {
        get
        {
            if (isApplicationQuitting)
                return null;

            if (instance == null)
            {
                instance = FindAnyObjectByType<T>(FindObjectsInactive.Exclude);

                if (instance == null && !hasWarnedMissing)
                {
                    // 매 프레임 로그 폭주를 막기 위해 최초 1회만 출력한다.
                    hasWarnedMissing = true;
                    GameLogger.Error(
                        $"[{typeof(T).Name}] 씬에 인스턴스가 없습니다. Hierarchy에 해당 컴포넌트를 가진 오브젝트를 배치하세요.");
                }
            }

            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            GameLogger.Warning(
                $"[{typeof(T).Name}] 중복 인스턴스를 발견하여 제거합니다. (GameObject: {gameObject.name})");

            Destroy(gameObject);
            return;
        }

        instance = (T)this;
        isApplicationQuitting = false;
        hasWarnedMissing = false;

        OnSingletonAwake();
    }

    /// <summary>
    /// 파생 클래스는 Awake()를 직접 구현하지 말고 이 메서드를 오버라이드한다.
    /// (Awake를 오버라이드할 경우 반드시 base.Awake()를 호출해야 한다.)
    /// </summary>
    protected virtual void OnSingletonAwake() { }

    protected virtual void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }
}
