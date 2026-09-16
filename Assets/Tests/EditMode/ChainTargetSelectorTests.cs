using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blob.Tests
{
    /// <summary>
    /// Chain 대상 선정 테스트. (v5 §10 "같은 시퀀스에서 동일 적 재타격 불가")
    /// </summary>
    public class ChainTargetSelectorTests
    {
        private static List<Vector3> Positions(params Vector3[] values) => new(values);

        private static List<bool> Excluded(params bool[] values) => new(values);

        [Test]
        public void 반경_내_최근접_대상을_고른다()
        {
            var positions = Positions(
                new Vector3(5f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(4f, 0f, 0f));

            int index = ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 6f, positions, Excluded(false, false, false));

            Assert.AreEqual(1, index);
        }

        [Test]
        public void 이미_때린_대상은_건너뛴다()
        {
            var positions = Positions(
                new Vector3(2f, 0f, 0f),
                new Vector3(4f, 0f, 0f));

            int index = ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 6f, positions, Excluded(true, false));

            Assert.AreEqual(1, index);
        }

        [Test]
        public void 반경_밖의_대상은_고르지_않는다()
        {
            var positions = Positions(new Vector3(10f, 0f, 0f));

            int index = ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 6f, positions, Excluded(false));

            Assert.AreEqual(-1, index);
        }

        [Test]
        public void 반경_경계_위의_대상은_포함된다()
        {
            var positions = Positions(new Vector3(6f, 0f, 0f));

            int index = ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 6f, positions, Excluded(false));

            Assert.AreEqual(0, index);
        }

        [Test]
        public void 전부_제외되면_대상이_없다()
        {
            var positions = Positions(
                new Vector3(1f, 0f, 0f),
                new Vector3(2f, 0f, 0f));

            int index = ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 6f, positions, Excluded(true, true));

            Assert.AreEqual(-1, index);
        }

        [Test]
        public void 높이_차이는_거리_계산에서_무시된다()
        {
            // Top-Down이므로 y가 섞이면 바로 옆의 적을 놓친다.
            var positions = Positions(
                new Vector3(3f, 50f, 0f),
                new Vector3(4f, 0f, 0f));

            int index = ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 6f, positions, Excluded(false, false));

            Assert.AreEqual(0, index);
        }

        [Test]
        public void 빈_목록이나_null은_안전하게_처리된다()
        {
            Assert.AreEqual(-1, ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 6f, new List<Vector3>(), new List<bool>()));

            Assert.AreEqual(-1, ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 6f, null, null));
        }

        [Test]
        public void 제외_목록이_없으면_전부_후보로_본다()
        {
            var positions = Positions(new Vector3(2f, 0f, 0f));

            int index = ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 6f, positions, null);

            Assert.AreEqual(0, index);
        }

        [Test]
        public void 반경이_0_이하이면_대상이_없다()
        {
            var positions = Positions(Vector3.zero);

            Assert.AreEqual(-1, ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, 0f, positions, Excluded(false)));

            Assert.AreEqual(-1, ChainTargetSelector.SelectNearestIndex(
                Vector3.zero, -3f, positions, Excluded(false)));
        }
    }
}
