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
        PassiveManager manager = PassiveManager.EnsureInstance();
        PassiveTree tree = manager.Tree;

        if (tree == null || tree.Count == 0)
        {
            UIFactory.CreateLabel(rightContent,
                "패시브 트리 에셋이 없습니다.\n메뉴 Blob > Passive > 패시브 에셋 생성 을 실행하십시오.",
                28, FontStyle.Normal, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter,
                UIPalette.TextDim);
            return;
        }

        DrawPassiveHeader(manager);
        DrawBranchTabs(manager, tree);

        // 좌우 바깥 여백은 rightContent가 이미 들여 놨다.
        // 트리와 상세 사이만 한 칸 띄운다 — 양쪽이 반 칸씩 물러난다.
        float half = UIFactory.Gap * 0.5f;

        RectTransform treeArea = UIFactory.CreateSlice("Tree", rightContent,
            Vector2.zero, new Vector2(Split, BodyTop), right: half);

        RectTransform detailArea = UIFactory.CreateSlice("NodeDetail", rightContent,
            new Vector2(Split, 0f), new Vector2(1f, BodyTop), left: half);

        DrawBranchTree(treeArea, manager, tree);
        DrawPassiveDetail(detailArea, manager);
    }

    /// <summary>머리글 줄의 아래 끝.</summary>
    private const float HeaderLine = 0.900f;

    /// <summary>
    /// 계열 탭 줄.
    ///
    /// 【높이를 0.080에서 0.115로 올린다.】
    /// 두 줄(계열 이름 · 0/5)이 들어가는 칸인데 40px밖에 되지 않아
    /// 글자가 위아래로 꽉 차 보였다. 손가락 목표로도 작았다.
    /// </summary>
    private const float TabTop = 0.870f;
    private const float TabBottom = 0.755f;

    /// <summary>본문(트리·상세)의 위 끝. 탭 줄과 한 칸 띄운다.</summary>
    private const float BodyTop = TabBottom - 0.020f;

    /// <summary>좌우 분할선. 사이 간격은 양쪽이 반 칸씩 물러나 만든다.</summary>
    private const float Split = 0.638f;

    /// <summary>
    /// 머리글. 회색 띠를 깔지 않는다 —
    /// 패널 자체가 이미 한 겹이라 그 위에 또 판을 얹으면
    /// 정체 모를 회색 막대가 된다. 글자만 얹어도 머리글로 읽힌다.
    /// </summary>
    private void DrawPassiveHeader(PassiveManager manager)
    {
        UIFactory.CreateLabel(rightContent,
            $"패시브    계정 Lv.{manager.AccountLevel}", 32, FontStyle.Bold,
            new Vector2(0f, HeaderLine), new Vector2(0.55f, 1f),
            TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        UIFactory.CreateLabel(rightContent,
            $"₡ {manager.Credits:N0}", 30, FontStyle.Bold,
            new Vector2(0.55f, HeaderLine), Vector2.one,
            TextAnchor.MiddleRight, UIPalette.TextAccent);
    }

    /// <summary>계열 5개. 역행은 발견 전까지 「???」다.</summary>
    private void DrawBranchTabs(PassiveManager manager, PassiveTree tree)
    {
        var branches = (PassiveBranch[])System.Enum.GetValues(typeof(PassiveBranch));

        // 좌우 바깥 여백은 rightContent가 이미 들여 놨다.
        // 탭 사이만 반 칸씩 물러나 간격을 한 칸으로 만든다.
        float half = UIFactory.Gap * 0.5f;
        float width = 1f / branches.Length;

        for (int i = 0; i < branches.Length; i++)
        {
            PassiveBranch branch = branches[i];

            bool visible = branch != PassiveBranch.Regression || manager.DiscoveredRegression;
            bool active = branch == selectedBranch && visible;

            var min = new Vector2(i * width, TabBottom);
            var max = new Vector2((i + 1) * width, TabTop);

            Color color = !visible ? UIPalette.SlotLocked
                        : active ? PassiveBranchInfo.Color(branch)
                        : UIPalette.Subtle;

            Image cell = UIFactory.CreatePanel($"Branch_{branch}", rightContent, color, min, max);

            // 맞닿은 변만 반 칸 물러난다. 양 끝은 패널 여백에 맞춰 붙인다.
            UIFactory.Inset(cell.rectTransform,
                left: i == 0 ? 0f : half,
                bottom: 0f,
                right: i == branches.Length - 1 ? 0f : half,
                top: 0f);

            UIFactory.CreateOutline(cell,
                active ? UIPalette.Rim : UIPalette.EdgeSoft, UIFactory.Radius, active ? 3 : 2);

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

            // 위아래 두 줄로 나눈다. 가운데를 기준으로 갈라야 한쪽만 치우치지 않는다.
            UIFactory.CreateLabel(cell.transform, PassiveBranchInfo.Name(branch), 26,
                FontStyle.Bold, new Vector2(0.04f, 0.46f), new Vector2(0.96f, 0.94f),
                TextAnchor.MiddleCenter, UIPalette.TextOnGlass);

            UIFactory.CreateLabel(cell.transform,
                $"{manager.State.CountIn(tree, branch)}/{tree.CountIn(branch)}", 21,
                FontStyle.Normal, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.46f),
                TextAnchor.MiddleCenter, UIPalette.TextDim);
        }
    }

    private void DrawBranchTree(RectTransform area, PassiveManager manager, PassiveTree tree)
    {
        // 테두리를 두지 않는다. 바깥 패널이 이미 윤곽을 가지고 있어서
        // 안쪽에 또 선을 그으면 그 사이가 빈 띠처럼 보인다.
        UIFactory.CreatePanel("TreeBack", area, UIPalette.Inset,
            Vector2.zero, Vector2.one, UIFactory.Radius);

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
        UIFactory.CreatePanel("DetailBack", area, UIPalette.Inset,
            Vector2.zero, Vector2.one, UIFactory.Radius);

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
