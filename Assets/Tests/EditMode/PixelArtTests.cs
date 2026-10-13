using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dokkaebi.Tests
{
    /// <summary>【픽셀 캐릭터 시험】 (결정 2-115)</summary>
    public class PixelArtTests
    {
        [Test]
        public void 캐릭터_키가_목표_칸수가_되게_배율을_고른다()
        {
            // 화면 1080 · 화각 60 · 거리 15m → 보이는 높이 17.3m. 키 2m를 48칸으로 → 416줄 → 배율 약 2.6 → 3
            Assert.AreEqual(3, PixelArtView.ScaleFor(1080, 60f, 15f, 2f, 48));
            Assert.Less(PixelArtView.ScaleFor(1080, 60f, 15f, 2f, 96), PixelArtView.ScaleFor(1080, 60f, 15f, 2f, 48), "칸을 늘리면 배율이 준다");
            Assert.AreEqual(1, PixelArtView.ScaleFor(1080, 60f, 15f, 0f, 48), "잘못된 값이면 1");
            Assert.LessOrEqual(PixelArtView.ScaleFor(4000, 10f, 2f, 2f, 8), 12, "상한 12");
        }

        [Test]
        public void 몸을_젖히면_내려다봐도_앞모습이_보인다()
        {
            // 60° 내려다보는 카메라(북쪽을 본다)
            Quaternion cam = Quaternion.Euler(60f, 0f, 0f);
            Vector3 forward = cam * Vector3.forward, right = cam * Vector3.right;

            Vector3 standing = Vector3.up;
            Vector3 full = PixelArtView.LeanFor(forward, right, 1f) * Vector3.up;
            Assert.Greater(Mathf.Abs(Vector3.Dot(standing, forward)), 0.8f, "서 있으면 정수리가 보인다");
            Assert.AreEqual(0f, Vector3.Dot(full, forward), 0.001f, "다 젖히면 몸이 시선과 직각 — 그림처럼 앞모습");
            Assert.Greater(full.z, 0f, "카메라 반대쪽(앞쪽)으로 젖힌다");
        }

        [Test]
        public void 픽셀_몸은_복제본이_그리고_원본은_그림자만_남는다()
        {
            Assume.That(PixelArtView.Layer, Is.GreaterThanOrEqualTo(0), "PixelChar 층이 있어야 한다");

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            label.AddComponent<TextMesh>();
            try
            {
                PixelCharacter c = PixelCharacter.Ensure(root);
                Assert.AreEqual(1, c.PairCount, "글자는 건드리지 않는다");
                var source = root.GetComponent<MeshRenderer>();
                Assert.AreEqual(ShadowCastingMode.ShadowsOnly, source.shadowCastingMode);

                Transform clone = root.transform.Find(PixelCharacter.BodyName + "/Pixel_" + root.name);
                Assert.IsNotNull(clone);
                Assert.AreEqual(PixelArtView.Layer, clone.gameObject.layer);
                Assert.IsNull(clone.GetComponent<Collider>(), "복제본은 부딪히지 않는다");
                Assert.AreEqual(ShadowCastingMode.Off, clone.GetComponent<MeshRenderer>().shadowCastingMode);

                c.Release();
                Assert.IsNull(root.transform.Find(PixelCharacter.BodyName), "복제본도 지운다");
                Assert.AreEqual(ShadowCastingMode.On, source.shadowCastingMode, "떼면 원래대로");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
