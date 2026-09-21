using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blob.Tests
{
    /// <summary>
    /// 개발 전용 UI가 출시 빌드로 새어 나가지 않는가.
    ///
    /// 【이 테스트가 없어서 새어 나갈 뻔했다】
    /// `#if UNITY_EDITOR || DEVELOPMENT_BUILD`는 **코드만** 감싼다.
    /// 씬에 배치된 GameObject는 그 전처리기와 무관하게 빌드에 실린다.
    /// DEBUG 버튼이 m_IsActive=1로 씬에 들어 있었고, 검증 패널을 여는
    /// 유일한 입구가 되면서 출시 빌드에 그대로 노출될 상태였다.
    ///
    /// 막는 층이 셋이고(DebugOnly · DebugOnlyStripper · 이 테스트),
    /// 이 테스트는 그중 「표시를 붙이는 것을 잊는다」를 잡는다.
    /// </summary>
    public class DebugOnlyTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        /// <summary>
        /// 출시 빌드에 남으면 안 되는 오브젝트. 경로로 못 박는다.
        ///
        /// 【왜 타입이 아니라 경로인가】
        /// 처음에는 「DebugUIManager가 붙은 오브젝트는 DebugOnly여야 한다」로 썼다가
        /// 테스트가 잡아냈다 — DebugUIManager는 **Canvas 본체**에 붙어 있어서,
        /// 그 규칙을 따르면 UI 캔버스를 통째로 지우게 된다.
        /// 오브젝트를 지우는 것과 컴포넌트를 떼는 것은 다른 일이다.
        /// 컴포넌트 쪽은 DebugOnlyStripper.StrippedComponents가 맡는다.
        ///
        /// 이름을 바꾸면 이 테스트가 깨진다 — 그게 맞다.
        /// 개발용 UI의 이름을 바꿀 때는 출시 안전성을 다시 확인해야 한다.
        /// </summary>
        private static readonly string[] RequiredDebugOnly =
        {
            "Canvas/Debug Button",
            "Canvas/Debug Panel"
        };

        /// <summary>
        /// 반대로, 이것이 붙은 오브젝트는 DebugOnly면 안 된다.
        /// 출시 빌드에서 통째로 지워지면 게임이 시작되지 않는다.
        ///
        /// 실제로 「Debug Object」에 표시를 붙일 뻔했는데,
        /// 그 오브젝트에는 SkillManager와 PlayerStats가 함께 올라가 있었다.
        /// </summary>
        private static readonly System.Type[] EssentialComponents =
        {
            typeof(SkillManager),
            typeof(PlayerStats),
            typeof(GameManager),
            typeof(EnemySpawner),
            typeof(BlobController)
        };

        private static List<GameObject> AllObjects(Scene scene)
        {
            var all = new List<GameObject>();

            foreach (GameObject root in scene.GetRootGameObjects())
                all.AddRange(root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));

            return all;
        }

        private static void WithScene(System.Action<Scene> body)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                body(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void 개발용_UI에_DebugOnly_표시가_붙어_있다()
        {
            WithScene(scene =>
            {
                List<GameObject> all = AllObjects(scene);

                foreach (string path in RequiredDebugOnly)
                {
                    string leaf = path.Substring(path.LastIndexOf('/') + 1);

                    GameObject go = all.FirstOrDefault(o => o.name == leaf);

                    Assert.IsNotNull(go,
                        $"「{path}」를 씬에서 찾지 못했습니다. 이름이 바뀌었다면 "
                        + "이 목록도 함께 고치고 출시 노출 여부를 다시 확인하십시오.");

                    Assert.IsNotNull(go.GetComponent<DebugOnly>(),
                        $"「{path}」에 DebugOnly 표시가 없습니다. "
                        + "출시 빌드에 그대로 실려 화면에 노출되고 눌립니다.");
                }
            });
        }

        /// <summary>
        /// 오브젝트째 지울 수 없는 개발용 컴포넌트는 타입으로 떼어 낸다.
        /// 그 목록에서 빠지면 출시 빌드에 치트가 살아 있게 된다.
        /// </summary>
        [Test]
        public void 개발용_컴포넌트가_제거_목록에_들어_있다()
        {
            CollectionAssert.Contains(DebugOnlyStripper.StrippedComponents,
                typeof(DebugUIManager),
                "DebugUIManager가 제거 목록에 없습니다.");

            CollectionAssert.Contains(DebugOnlyStripper.StrippedComponents,
                typeof(DebugManager),
                "DebugManager(치트 입력)가 제거 목록에 없습니다.");
        }

        [Test]
        public void DebugOnly가_붙은_오브젝트에_필수_기능이_없다()
        {
            WithScene(scene =>
            {
                foreach (GameObject go in AllObjects(scene))
                {
                    if (go.GetComponent<DebugOnly>() == null)
                        continue;

                    foreach (System.Type type in EssentialComponents)
                    {
                        Assert.IsNull(go.GetComponent(type),
                            $"「{go.name}」은 출시 빌드에서 통째로 지워지는데 "
                            + $"{type.Name}이 함께 올라가 있습니다. 게임이 시작되지 않습니다.");
                    }
                }
            });
        }

        /// <summary>
        /// 표시가 붙은 오브젝트가 하나도 없으면 누군가 전부 떼어 낸 것이다.
        /// DEBUG 버튼은 반드시 표시를 달고 있어야 한다.
        /// </summary>
        [Test]
        public void 씬에_DebugOnly_표시가_적어도_하나는_있다()
        {
            WithScene(scene =>
            {
                int marked = AllObjects(scene).Count(go => go.GetComponent<DebugOnly>() != null);

                Assert.Greater(marked, 0,
                    "DebugOnly 표시가 하나도 없습니다. DEBUG 버튼과 디버그 패널에 붙어 있어야 합니다.");
            });
        }
    }
}
