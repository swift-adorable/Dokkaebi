using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 화면의 「패시브」 탭 — 계정 축의 영구 성장.
/// (docs/Blob_Passive_System.md)
///
/// 화면 구성
///   상단 : 계열 5개 (적응 / 대사 / 회수 / 중개 / 역행)
///          역행은 발견 전까지 「???」로 잠겨 있다
///   좌   : 고른 계열의 트리. 아래에서 위로 자란다
///   우   : 고른 칸의 상세 — 효과 · 요구 레벨 · 크레딧 · 필요물품 · 배우기
///
/// 단일 트리가 아니라 계열을 나눈 이유는 1절 참조 —
/// 단일 트리는 「위로 한 줄」뿐이라 고를 것이 순서밖에 없다.
/// </summary>
public partial class InventoryScreenUI
{
    private PassiveNode selectedNode;
    private PassiveBranch selectedBranch = PassiveBranch.Adapt;

    private readonly List<PassiveNode> branchBuffer = new();

    private void DrawPassivePanel()
    {
        UIFactory.CreatePanel("Back", rightPanel, UIPalette.Panel, Vector2.zero, Vector2.one);

        PassiveManager manager = PassiveManager.EnsureInstance();
        PassiveTree tree = manager.Tree;

        if (tree == null || tree.Count == 0)
        {
            UIFactory.CreateLabel(rightPanel,
                "패시브 트리 에셋이 없습니다.\n메뉴 Blob > Passive > 패시브 에셋 생성 을 실행하십시오.",
                28, FontStyle.Normal, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter,
                UIPalette.TextDim);
            return;
        }

        DrawPassiveHeader(manager);
        DrawBranchTabs(manager, tree);

        RectTransform treeArea = UIFactory.CreateRegion("Tree", rightPanel,
            new Vector2(0.015f, 0.02f), new Vector2(0.63f, 0.80f));

        RectTransform detailArea = UIFactory.CreateRegion("NodeDetail", rightPanel,
            new Vector2(0.65f, 0.02f), new Vector2(0.985f, 0.80f));

        DrawBranchTree(treeArea, manager, tree);
        DrawPassiveDetail(detailArea, manager);
    }

    private void DrawPassiveHeader(PassiveManager manager)
    {
        UIFactory.CreatePanel("Header", rightPanel, UIPalette.Header,
            new Vector2(0f, 0.92f), new Vector2(1f, 1f));

        UIFactory.CreateLabel(rightPanel,
            $"패시브    계정 Lv.{manager.AccountLevel}", 32, FontStyle.Bold,
            new Vector2(0.02f, 0.92f), new Vector2(0.55f, 1f), TextAnchor.MiddleLeft);

        UIFactory.CreateLabel(rightPanel,
            $"₡ {manager.Credits:N0}", 30, FontStyle.Bold,
            new Vector2(0.55f, 0.92f), new Vector2(0.98f, 1f), TextAnchor.MiddleRight,
            UIPalette.TextAccent);
    }

    /// <summary>계열 5개. 역행은 발견 전까지 「???」다.</summary>
    private void DrawBranchTabs(PassiveManager manager, PassiveTree tree)
    {
        var branches = (PassiveBranch[])System.Enum.GetValues(typeof(PassiveBranch));

        float width = 1f / branches.Length;

        for (int i = 0; i < branches.Length; i++)
        {
            PassiveBranch branch = branches[i];

            bool visible = branch != PassiveBranch.Regression || manager.DiscoveredRegression;
            bool active = branch == selectedBranch && visible;

            var min = new Vector2(i * width + 0.006f, 0.82f);
            var max = new Vector2((i + 1) * width - 0.006f, 0.905f);

            Color color = !visible ? UIPalette.SlotLocked
                        : active ? PassiveBranchInfo.Color(branch)
                        : UIPalette.Subtle;

            Image cell = UIFactory.CreatePanel($"Branch_{branch}", rightPanel, color, min, max);

            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = cell;
            button.interactable = visible;

            PassiveBranch captured = branch;
            button.onClick.AddListener(() => SelectBranch(captured));

            if (!visible)
            {
                // 존재 자체를 감춘다. 이름을 보여 주면 「거기까지 갔는가」가 보상이 되지 않는다.
                UIFactory.CreateLabel(cell.transform, "? ? ?", 26, FontStyle.Bold,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, UIPalette.TextDim);
                continue;
            }

            UIFactory.CreateLabel(cell.transform, PassiveBranchInfo.Name(branch), 26,
                FontStyle.Bold, new Vector2(0.04f, 0.40f), new Vector2(0.96f, 0.96f),
                TextAnchor.LowerCenter);

            UIFactory.CreateLabel(cell.transform,
                $"{manager.State.CountIn(tree, branch)}/{tree.CountIn(branch)}", 20,
                FontStyle.Normal, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.40f),
                TextAnchor.UpperCenter, UIPalette.TextDim);
        }
    }

    private void DrawBranchTree(RectTransform area, PassiveManager manager, PassiveTree tree)
    {
        UIFactory.CreatePanel("TreeBack", area, UIPalette.Header, Vector2.zero, Vector2.one);

        if (selectedBranch == PassiveBranch.Regression && !manager.DiscoveredRegression)
        {
            UIFactory.CreateLabel(area, "발견하지 못한 계열입니다.", 28, FontStyle.Normal,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, UIPalette.TextDim);
            return;
        }

        UIFactory.CreateLabel(area, PassiveBranchInfo.Subtitle(selectedBranch), 22,
            FontStyle.Normal, new Vector2(0.03f, 0.94f), new Vector2(0.97f, 0.99f),
            TextAnchor.MiddleLeft, UIPalette.TextDim);

        // 중개 계열은 레벨을 보지 않는다. 그 사실을 화면에 적어 둔다.
        if (PassiveBranchInfo.UnlockKind(selectedBranch) == PassiveUnlockKind.CreditsOnly)
        {
            UIFactory.CreateLabel(area, "계정 레벨과 무관 · 크레딧만", 22, FontStyle.Normal,
                new Vector2(0.03f, 0.94f), new Vector2(0.97f, 0.99f),
                TextAnchor.MiddleRight, UIPalette.TextAccent);
        }

        tree.GetBranch(selectedBranch, branchBuffer);

        int columns = tree.Columns;
        int rows = tree.Rows;

        for (int i = 0; i < branchBuffer.Count; i++)
        {
            PassiveNode node = branchBuffer[i];

            if (node == null)
                continue;

            float cellWidth = 1f / columns;
            float cellHeight = 0.92f / rows;

            // row 0이 맨 아래다. 트리가 아래에서 위로 자란다.
            var min = new Vector2(
                node.Column * cellWidth + 0.025f,
                node.Row * cellHeight + 0.02f);

            var max = new Vector2(
                (node.Column + 1) * cellWidth - 0.025f,
                (node.Row + 1) * cellHeight - 0.02f);

            DrawPassiveNode(area, manager, node, min, max);
        }
    }

    private void DrawPassiveNode(RectTransform area, PassiveManager manager,
                                 PassiveNode node, Vector2 min, Vector2 max)
    {
        bool learned = manager.State.IsLearned(node);
        PassiveError error = manager.CanLearn(node);

        Color color = learned ? PassiveBranchInfo.Color(node.Branch)
                    : error == PassiveError.None ? UIPalette.SlotFilled
                    : UIPalette.SlotLocked;

        Image cell = UIFactory.CreatePanel($"Node_{node.Id}", area, color, min, max);

        var button = cell.gameObject.AddComponent<Button>();
        button.targetGraphic = cell;

        PassiveNode captured = node;
        button.onClick.AddListener(() => SelectPassiveNode(captured));

        if (selectedNode == node)
        {
            UIFactory.CreatePanel("Focus", cell.transform, UIPalette.SlotSelected,
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.10f));
        }

        Color textColor = learned || error == PassiveError.None
            ? UIPalette.Text
            : UIPalette.TextDim;

        UIFactory.CreateLabel(cell.transform, node.DisplayName, 22, FontStyle.Bold,
            new Vector2(0.06f, 0.46f), new Vector2(0.94f, 0.95f), TextAnchor.LowerCenter,
            textColor);

        UIFactory.CreateLabel(cell.transform, learned ? "배움" : node.EffectText, 19,
            FontStyle.Normal, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.44f),
            TextAnchor.UpperCenter, learned ? UIPalette.TextAccent : UIPalette.TextDim);

        // 필요물품이 있는 칸은 그 사실만 표시한다. 무엇이 필요한지는 상세에서 본다.
        if (!learned && node.NeedsMaterials)
        {
            UIFactory.CreateLabel(cell.transform, "물품 필요", 18, FontStyle.Normal,
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.20f),
                TextAnchor.UpperCenter,
                error == PassiveError.MissingMaterials ? UIPalette.Warning : UIPalette.TextDim);
        }
    }

    private void DrawPassiveDetail(RectTransform area, PassiveManager manager)
    {
        UIFactory.CreatePanel("DetailBack", area, UIPalette.Header, Vector2.zero, Vector2.one);

        if (selectedNode == null)
        {
            UIFactory.CreateLabel(area, "항목을 눌러 확인하십시오.", 26, FontStyle.Normal,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, UIPalette.TextDim);
            return;
        }

        PassiveNode node = selectedNode;

        bool learned = manager.State.IsLearned(node);
        PassiveError error = manager.CanLearn(node);

        UIFactory.CreateLabel(area, node.DisplayName, 30, FontStyle.Bold,
            new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.97f), TextAnchor.MiddleLeft);

        UIFactory.CreateLabel(area, PassiveBranchInfo.Name(node.Branch), 22, FontStyle.Normal,
            new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.88f), TextAnchor.MiddleLeft,
            UIPalette.TextDim);

        UIFactory.CreateLabel(area, node.EffectText, 26, FontStyle.Bold,
            new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.81f), TextAnchor.MiddleLeft,
            UIPalette.TextAccent);

        UIFactory.CreateLabel(area, node.Description, 22, FontStyle.Normal,
            new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.70f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        // 요구 조건 — 레벨 · 크레딧 · 필요물품.
        var lines = new List<string>(3);

        if (node.UnlockKind != PassiveUnlockKind.CreditsOnly)
            lines.Add($"요구 계정 Lv.{node.RequiredAccountLevel}");

        lines.Add($"비용 ₡ {node.Cost:N0}");

        if (node.NeedsMaterials)
            lines.Add($"필요물품 — {node.MaterialText}");

        UIFactory.CreateLabel(area, string.Join("\n", lines), 22, FontStyle.Normal,
            new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.46f), TextAnchor.UpperLeft);

        if (learned)
        {
            UIFactory.CreateLabel(area, "이미 배웠습니다.", 26, FontStyle.Bold,
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.20f),
                TextAnchor.MiddleCenter, UIPalette.TextAccent);
            return;
        }

        if (error != PassiveError.None)
        {
            UIFactory.CreateLabel(area, PassiveErrorText.Describe(error), 22, FontStyle.Normal,
                new Vector2(0.06f, 0.15f), new Vector2(0.94f, 0.23f),
                TextAnchor.MiddleLeft, UIPalette.Warning);
        }

        Button learn = UIFactory.CreateButton(area, "배우기",
            new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.14f),
            UIPalette.Action, LearnSelectedPassive, 26);

        learn.interactable = error == PassiveError.None;
    }

    private void SelectBranch(PassiveBranch branch)
    {
        selectedBranch = branch;
        selectedNode = null;

        Refresh();
    }

    private void SelectPassiveNode(PassiveNode node)
    {
        selectedNode = selectedNode == node ? null : node;

        Refresh();
    }

    private void LearnSelectedPassive()
    {
        if (selectedNode == null)
            return;

        PassiveManager manager = PassiveManager.EnsureInstance();

        if (manager.TryLearn(selectedNode))
            SetHint($"「{selectedNode.DisplayName}」 배움");

        Refresh();
    }
}
