using NUnit.Framework;

namespace Dokkaebi.Tests
{
    /// <summary>【본문에 맞춤 2】 (결정 2-112)</summary>
    public class StoryAlign2Tests
    {
        [Test]
        public void 서로_다른_잡귀는_같은_진영이라도_싸운다()
        {
            Assert.IsTrue(FactionTable.IsHostile(Faction.Wild, Faction.Wild, 0, 1), "갈래가 다르다");
            Assert.IsFalse(FactionTable.IsHostile(Faction.Wild, Faction.Wild, 2, 2), "같은 잡귀");
            Assert.IsFalse(FactionTable.IsHostile(Faction.Wild, Faction.Wild, -1, 3), "보스는 갈래로 싸우지 않는다");
            Assert.IsTrue(FactionTable.IsHostile(Faction.Wild, Faction.Facility, 1, 1), "진영이 다르면 싸운다");
            Assert.IsFalse(FactionTable.IsHostile(Faction.Friendly, Faction.Wild, 0, 1), "우호는 싸우지 않는다");
        }

        [Test]
        public void 시스템_설명_넷은_본문에_있다()
        {
            string script = System.IO.File.ReadAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "docs/Dokkaebi_Story_Script.txt"));
            var lore = LoreTable.Parse(script);

            foreach (string title in new[] { StoryManager.NoteBuilding, StoryManager.NoteCooking,
                                              StoryManager.NoteImprint, StoryManager.NoteWeight })
                Assert.IsNotEmpty(LoreTable.BodyOf(lore, LoreKind.System, title), title);
        }

        [Test]
        public void 손질_가공은_본문에서_뺐다()
        {
            string script = System.IO.File.ReadAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "docs/Dokkaebi_Story_Script.txt"));
            StringAssert.DoesNotContain("손질도 해 주마", script);
            StringAssert.DoesNotContain("가공과 회복", script);
            StringAssert.DoesNotContain("물러서며 쏘기", script);
            StringAssert.Contains("헌 지갑", script);
        }
    }
}
