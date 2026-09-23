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
///   우   : 고른 칸의 상세 — 효과 · 요구 레벨 · 크레딧 · 필요 재료 · 배우기
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

        // 【머리글을 먼저 그린다.】 트리가 없을 때도 「닫기」는 있어야 한다.
        // 예전에는 안내문만 그리고 돌아가서, 이 화면에 들어오면 나갈 길이 없었다.
        DrawPassiveHeader(manager);

        if (tree == null || tree.Count == 0)
        {
            UIFactory.CreateLabel(rightContent,
                "패시브 트리 에셋이 없습니다.\n메뉴 Blob > Passive > 패시브 에셋 생성 을 실행하십시오.",
                28, FontStyle.Normal, Vector2.zero, new Vector2(1f, HeaderLine),
                TextAnchor.MiddleCenter, UIPalette.TextDim);
            return;
        }

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

    // ── 트리 격자 ─────────────────────────────────────────────────────
    // 칸과 연결선이 같은 값을 써야 선이 칸에 맞는다. 한 곳에 둔다.

    /// <summary>트리가 쓰는 세로 범위. 위쪽은 계열 설명 줄이 쓴다.</summary>
    private const float TreeHeight = 0.93f;

    private const float NodeInsetX = 0.025f;
    private const float NodeInsetY = 0.02f;

    /// <summary>
    /// 머리글. 회색 띠를 깔지 않는다 —
    /// 패널 자체가 이미 한 겹이라 그 위에 또 판을 얹으면
    /// 정체 모를 회색 막대가 된다. 글자만 얹어도 머리글로 읽힌다.
    /// </summary>
    private void DrawPassiveHeader(PassiveManager manager)
    {
        // 【나가는 길은 오른쪽 위의 「닫기」 하나다.】
        // 왼쪽 위에 「←」를 따로 뒀더니 아래의 「닫기」와 둘이 되어,
        // 어느 쪽이 무엇인지 매번 읽어야 했다. 크레딧을 왼쪽으로 물리고
        // 그 자리를 닫기가 가져간다 — 오른쪽 위는 원래 닫는 자리다.
        UIFactory.CreateLabel(rightContent,
            $"패시브    계정 Lv.{manager.AccountLevel}", 32, FontStyle.Bold,
            new Vector2(0f, HeaderLine), new Vector2(0.55f, 1f),
            TextAnchor.MiddleLeft, UIPalette.TextOnGlass);

        UIFactory.CreateLabel(rightContent,
            $"₡ {manager.Credits:N0}", 30, FontStyle.Bold,
            new Vector2(0.55f, HeaderLine), new Vector2(0.86f, 1f),
            TextAnchor.MiddleRight, UIPalette.TextAccent);

        UIFactory.CreateButton(rightContent, "닫기",
            new Vector2(0.89f, HeaderLine), Vector2.one,
            UIPalette.Subtle, Close, 26);
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

        // 【계열 설명을 적지 않는다.】
        // 「얼마나 들고 나가는가」 같은 한 줄은 탭 이름을 다시 말하는 것에 가깝다.
        // 트리 위쪽 한 줄을 차지하면서 알려 주는 것이 없다.

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

        // 【선을 먼저 긋는다.】
        // 나중에 그리면 칸 위를 덮는다. 자식보다 먼저 만들어야 뒤로 간다.
        DrawBranchLinks(area, manager, columns, rows);

        for (int i = 0; i < branchBuffer.Count; i++)
        {
            PassiveNode node = branchBuffer[i];

            if (node == null)
                continue;

            float cellWidth = 1f / columns;
            float cellHeight = TreeHeight / rows;

            // row 0이 맨 아래다. 트리가 아래에서 위로 자란다.
            var min = new Vector2(
                node.Column * cellWidth + NodeInsetX,
                node.Row * cellHeight + NodeInsetY);

            var max = new Vector2(
                (node.Column + 1) * cellWidth - NodeInsetX,
                (node.Row + 1) * cellHeight - NodeInsetY);

            DrawPassiveNode(area, manager, node, min, max);
        }
    }

    // ── 연결선 ────────────────────────────────────────────────────────

    /// <summary>선 굵기(px). 가로·세로가 같아 보이려면 픽셀로 줘야 한다.</summary>
    private const float LinkThickness = 3f;

    /// <summary>
    /// 선행 관계를 선으로 잇는다.
    ///
    /// 【없으면 트리가 아니라 그냥 흩어진 칸들이다.】
    /// 지금까지는 「가방 정리 1 → 2 → 3」이 선행 관계라는 것이
    /// 눌러 봐야만 드러났다. 잠긴 칸을 눌러 "요구 조건이 있다"를 읽고
    /// 그제야 어느 칸이 먼저인지 찾아야 했다.
    ///
    /// 꺾은선으로 긋는다 — 대각선은 회전이 필요해 픽셀 굵기가 흔들린다.
    /// 아래에서 위로 자라므로 「선행에서 올라가 · 옆으로 · 다시 올라가」 세 토막이다.
    /// </summary>
    private void DrawBranchLinks(RectTransform area, PassiveManager manager,
                                 int columns, int rows)
    {
        PassiveTree tree = manager.Tree;

        float cellWidth = 1f / columns;
        float cellHeight = TreeHeight / rows;

        for (int i = 0; i < branchBuffer.Count; i++)
        {
            PassiveNode node = branchBuffer[i];

            if (node == null || node.IsRoot)
                continue;

            float childX = (node.Column + 0.5f) * cellWidth;
            float childBottom = node.Row * cellHeight + NodeInsetY;

            for (int p = 0; p < node.Prerequisites.Count; p++)
            {
                PassiveNode parent = tree.Find(node.Prerequisites[p]);

                if (parent == null || parent.Branch != node.Branch)
                    continue;

                float parentX = (parent.Column + 0.5f) * cellWidth;
                float parentTop = (parent.Row + 1) * cellHeight - NodeInsetY;

                // 이미 배운 선행으로 이어진 길만 밝힌다 — 어디까지 왔는지가 보인다.
                Color color = manager.State.IsLearned(parent)
                    ? PassiveBranchInfo.Color(node.Branch)
                    : UIPalette.Edge;

                float middle = (parentTop + childBottom) * 0.5f;

                Vertical(area, parentX, parentTop, middle, color);

                if (!Mathf.Approximately(parentX, childX))
                    Horizontal(area, parentX, childX, middle, color);

                Vertical(area, childX, middle, childBottom, color);
            }
        }
    }

    private static void Vertical(RectTransform area, float x, float from, float to, Color color)
    {
        float low = Mathf.Min(from, to);
        float high = Mathf.Max(from, to);

        if (high - low < 0.001f)
            return;

        Image line = UIFactory.CreatePanel("Link", area, color,
            new Vector2(x, low), new Vector2(x, high), radius: 0);

        // 앵커가 한 점으로 모인 축만 sizeDelta가 픽셀 굵기가 된다.
        line.rectTransform.sizeDelta = new Vector2(LinkThickness, 0f);
        line.raycastTarget = false;
    }

    private static void Horizontal(RectTransform area, float from, float to, float y, Color color)
    {
        float left = Mathf.Min(from, to);
        float right = Mathf.Max(from, to);

        Image line = UIFactory.CreatePanel("Link", area, color,
            new Vector2(left, y), new Vector2(right, y), radius: 0);

        line.rectTransform.sizeDelta = new Vector2(0f, LinkThickness);
        line.raycastTarget = false;
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

        // 【고른 칸은 테두리로 알린다.】
        // 전에는 칸 아래쪽에 파란 띠를 깔았는데, 진행 막대처럼 보여서
        // 「무언가 차오르는 중」으로 읽혔다. 고른 것과 아무 상관이 없다.
        UIFactory.CreateOutline(cell,
            selectedNode == node ? UIPalette.Rim : UIPalette.EdgeSoft,
            UIFactory.Radius,
            selectedNode == node ? 3 : 2);

        Color textColor = learned || error == PassiveError.None
            ? UIPalette.Text
            : UIPalette.TextDim;

        UIFactory.CreateLabel(cell.transform, node.DisplayName, 22, FontStyle.Bold,
            new Vector2(0.06f, 0.46f), new Vector2(0.94f, 0.95f), TextAnchor.LowerCenter,
            textColor);

        UIFactory.CreateLabel(cell.transform, learned ? "배움" : node.EffectText, 19,
            FontStyle.Normal, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.44f),
            TextAnchor.UpperCenter, learned ? UIPalette.TextAccent : UIPalette.TextDim);

        // 【「물품 필요」를 칸에 적지 않는다.】
        // 무엇이 몇 개 필요한지는 적지 못하면서 자리만 차지했다.
        // 필요 재료는 오른쪽 상세에 정확히 적혀 있고, 모자라서 못 배우는 경우는
        // 「배우기」 버튼이 꺼지고 그 위에 이유가 뜬다.
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

        // 요구 조건 — 레벨 · 크레딧 · 필요 재료.
        var lines = new List<string>(3);

        if (node.UnlockKind != PassiveUnlockKind.CreditsOnly)
            lines.Add($"요구 계정 Lv.{node.RequiredAccountLevel}");

        lines.Add($"비용 ₡ {node.Cost:N0}");

        if (node.NeedsMaterials)
            lines.Add($"필요 재료 — {node.MaterialText}");

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

        // 성공은 알리지 않는다 — 칸이 「배움」으로 바뀌는 것이 곧 결과다.
        if (!manager.TryLearn(selectedNode))
            ShowToast("배우지 못했습니다. 조건을 확인하십시오.");

        Refresh();
    }
}
