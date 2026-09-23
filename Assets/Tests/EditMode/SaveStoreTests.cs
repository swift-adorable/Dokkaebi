using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Blob.Tests
{
    /// <summary>
    /// 세이브 파일 쓰기·읽기. (로드맵 8-F)
    ///
    /// 여기서 지키는 것은 하나다 — 【진행이 소리 없이 사라지지 않는다.】
    /// 저장 중에 앱이 죽어도, 파일이 깨져도, 옛 빌드가 새 세이브를 열어도.
    /// </summary>
    public class SaveStoreTests
    {
        private string folder;
        private string path;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "BlobSaveTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, SaveStore.FileName);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }

        private static SaveData Sample(int level = 7, int credits = 1234)
        {
            return new SaveData
            {
                accountLevel = level,
                credits = credits,
                discoveredRegression = true,
                learnedPassives = new List<string> { "psv_a", "psv_b" },
                codex = new List<string> { "skill_frost" },
                imprints = new List<SavedItem>
                {
                    new SavedItem { id = "imp_charge_t2", count = 1, durability = -1 },
                    new SavedItem { id = string.Empty, count = 0 }
                }
            };
        }

        // ── 왕복 ──────────────────────────────────────────────────────

        [Test]
        public void 쓴_것을_그대로_읽는다()
        {
            SaveStore.Write(path, Sample());

            Assert.AreEqual(SaveLoadResult.Main, SaveStore.Read(path, out SaveData data));

            Assert.AreEqual(7, data.accountLevel);
            Assert.AreEqual(1234, data.credits);
            Assert.IsTrue(data.discoveredRegression);
            CollectionAssert.AreEqual(new[] { "psv_a", "psv_b" }, data.learnedPassives);
            CollectionAssert.AreEqual(new[] { "skill_frost" }, data.codex);

            // 빈 각인 칸도 자리가 남는다 — A · B 순서가 의미를 갖는다.
            Assert.AreEqual(2, data.imprints.Count);
            Assert.AreEqual("imp_charge_t2", data.imprints[0].id);
            Assert.IsTrue(data.imprints[1].IsEmpty);
        }

        [Test]
        public void 파일이_없으면_처음이다()
        {
            Assert.AreEqual(SaveLoadResult.None, SaveStore.Read(path, out SaveData data));
            Assert.IsNull(data);
        }

        [Test]
        public void 저장하면_판과_시각이_찍힌다()
        {
            SaveStore.Write(path, Sample());

            SaveStore.Read(path, out SaveData data);

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.IsFalse(string.IsNullOrEmpty(data.savedAtUtc));
        }

        // ── 백업 ──────────────────────────────────────────────────────

        [Test]
        public void 두_번째_저장부터_백업이_생긴다()
        {
            SaveStore.Write(path, Sample(level: 1));
            Assert.IsFalse(File.Exists(path + ".bak"));

            SaveStore.Write(path, Sample(level: 2));
            Assert.IsTrue(File.Exists(path + ".bak"));

            // 백업은 【직전】 저장이다.
            Assert.AreEqual(1, SaveStore.FromJson(File.ReadAllText(path + ".bak")).accountLevel);
        }

        [Test]
        public void 본_파일이_깨지면_백업에서_되살린다()
        {
            SaveStore.Write(path, Sample(level: 3));
            SaveStore.Write(path, Sample(level: 4));

            // 쓰다 만 파일 — 모바일에서 저장 중에 앱이 죽으면 이렇게 된다.
            File.WriteAllText(path, "{ \"accountLevel\": 4, \"cred");

            Assert.AreEqual(SaveLoadResult.Backup, SaveStore.Read(path, out SaveData data));
            Assert.AreEqual(3, data.accountLevel);
        }

        [Test]
        public void 둘_다_깨지면_깨졌다고_말한다()
        {
            File.WriteAllText(path, "not json");
            File.WriteAllText(path + ".bak", "also not json");

            Assert.AreEqual(SaveLoadResult.Corrupt, SaveStore.Read(path, out SaveData data));
            Assert.IsNull(data);
        }

        [Test]
        public void 깨진_파일은_비켜_두면_지워지지_않는다()
        {
            File.WriteAllText(path, "not json");
            File.WriteAllText(path + ".bak", "also not json");

            SaveStore.Quarantine(path);

            // 새로 쓴 뒤에도 깨진 둘이 어딘가 남아 있어야 한다.
            SaveStore.Write(path, Sample());

            string[] kept = Directory.GetFiles(folder, "*.corrupt-*");

            Assert.AreEqual(2, kept.Length);
        }

        [Test]
        public void 임시_파일이_남지_않는다()
        {
            SaveStore.Write(path, Sample());
            SaveStore.Write(path, Sample());

            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        // ── 판 ────────────────────────────────────────────────────────

        [Test]
        public void 더_새_판의_세이브는_읽지_않는다()
        {
            // 옛 빌드가 새 세이브를 열어 모르는 필드를 버린 채 저장하면
            // 진행이 소리 없이 사라진다.
            SaveData future = Sample();
            future.version = SaveData.CurrentVersion + 1;

            File.WriteAllText(path, SaveStore.ToJson(future));

            Assert.AreEqual(SaveLoadResult.TooNew, SaveStore.Read(path, out SaveData data));
            Assert.IsNull(data);
        }

        [Test]
        public void 지우면_백업까지_사라진다()
        {
            SaveStore.Write(path, Sample());
            SaveStore.Write(path, Sample());

            SaveStore.Delete(path);

            Assert.AreEqual(SaveLoadResult.None, SaveStore.Read(path, out _));
        }

        // ── 판 1 → 2 (결정 2-33 — 레벨이 하나가 됐다) ──────────────────

        [Test]
        public void 판1_세이브를_읽으면_레벨은_그대로이고_경험치는_0이다()
        {
            // 판 1에는 experience가 없다. accountLevel은 오르는 길이 없던 값이라
            // 그대로 각성 레벨로 쓴다.
            File.WriteAllText(path,
                "{ \"version\": 1, \"accountLevel\": 6, \"credits\": 900 }");

            Assert.AreEqual(SaveLoadResult.Main, SaveStore.Read(path, out SaveData data));

            Assert.AreEqual(6, data.accountLevel);
            Assert.AreEqual(0, data.experience);
            Assert.AreEqual(900, data.credits);
        }

        [Test]
        public void 경험치가_남는다()
        {
            SaveData data = Sample();
            data.experience = 37;

            SaveStore.Write(path, data);
            SaveStore.Read(path, out SaveData read);

            Assert.AreEqual(37, read.experience);
        }
    }
}
