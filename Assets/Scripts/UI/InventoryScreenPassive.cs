using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 화면의 「패시브」 탭 — 계정 축의 영구 성장.
/// 스크린샷의 「스킬 강화」 화면에 해당한다.
///
/// 배치 — 아래에서 위로 자란다. 가로선이 계정 레벨 구간을 나눈다.
/// 우측에 고른 칸의 상세와 「배우기」 버튼을 둔다.
///
/// 【여기에 전투 수치가 나오면 안 된다.】 방어도·피해·체력은 장비의 몫이다.
/// 규칙은 PassiveEffectType에, 검사는 PassiveStateTests에 있다.
/// </summary>
public partial class InventoryScreenUI
{
    private PassiveNode selectedNode;

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

        UIFactory.CreatePanel("Header", rightPanel, UIPalette.Header,
            new Vector2(0f, 0.92f), new Vector2(1f, 1f));

        UIFactory.CreateLabel(rightPanel,
            $"패시브    계정 Lv.{manager.AccountLevel}    ₡ {manager.Credits:N0}",
            32, FontStyle.Bold,
            new Vector2(0.03f, 0.92f), new Vector2(0.97f, 1f), TextAnchor.MiddleLeft);

        RectTransform treeArea = UIFactory.CreateRegion("Tree", rightPanel,
            new Vector2(0.02f, 0.02f), new Vector2(0.64f, 0.90f));

        RectTransform detailArea = UIFactory.CreateRegion("NodeDetail", rightPanel,
            new Vector2(0.66f, 0.02f), new Vector2(0.98f, 0.90f));

        DrawPassiveTree(treeArea, manager, tree);
        DrawPassiveDetail(detailArea, manager);
    }

    private void DrawPassiveTree(RectTransform area, PassiveManager manager, PassiveTree tree)
    {
        UIFactory.CreatePanel("TreeBack", area, UIPalette.Header, Vector2.zero, Vector2.one);

        int columns = tree.Columns;
        int rows = tree.Rows;

        // 계정 레벨 가로선. 스크린샷의 「LEVEL 5」 점선과 같은 역할이다.
        DrawLevelLines(area, tree, rows);

        IReadOnlyList<PassiveNode> nodes = tree.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            PassiveNode node = nodes[i];

            if (node == null)
                continue;

            float cellWidth = 1f / columns;
            float cellHeight = 1f / rows;

            // row 0이 맨 아래다. 트리가 아래에서 위로 자란다.
            var min = new Vector2(
                node.Column * cellWidth + 0.03f,
                node.Row * cellHeight + 0.03f);

            var max = new Vector2(
                (node.Column + 1) * cellWidth - 0.03f,
                (node.Row + 1) * cellHeight - 0.03f);

            DrawPassiveNode(area, manager, node, min, max);
        }
    }

    /// <summary>같은 행에 있는 칸들의 요구 레벨이 같으면 그 아래에 구간선을 긋는다.</summary>
    private void DrawLevelLines(RectTransform area, PassiveTree tree, int rows)
    {
        for (int row = 1; row < rows; row++)
        {
            int level = 0;
            bool uniform = true;

            IReadOnlyList<PassiveNode> nodes = tree.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null || nodes[i].Row != row)
                    continue;

                if (level == 0)
                    level = nodes[i].RequiredAccountLevel;
                else if (level != nodes[i].RequiredAccountLevel)
                    uniform = false;
            }

            if (level == 0 || !uniform)
                continue;

            float y = row / (float)rows;

            UIFactory.CreatePanel($"Line_{row}", area, new Color(1f, 1f, 1f, 0.16f),
                new Vector2(0.02f, y - 0.003f), new Vector2(0.98f, y + 0.003f));

            UIFactory.CreateLabel(area, $"LEVEL {level}", 20, FontStyle.Bold,
                new Vector2(0.02f, y + 0.004f), new Vector2(0.20f, y + 0.045f),
                TextAnchor.LowerLeft, UIPalette.TextDim);
        }
    }

    private void DrawPassiveNode(RectTransform area, PassiveManager manager,
                                 PassiveNode node, Vector2 min, Vector2 max)
    {
        bool learned = manager.State.IsLearned(node);

        PassiveError error = manager.State.CanLearn(
            node, manager.AccountLevel, manager.Credits);

        Color color = learned ? UIPalette.Action
                    : error == PassiveError.None ? UIPalette.SlotFilled
                    : UIPalette.SlotLocked;

        Image cell = UIFactory.CreatePanel($"Node_{node.Id}", area, color, min, max);

        var button = cell.gameObject.AddComponent<Button>();
        button.targetGraphic = cell;

        PassiveNode captured = node;
        button.onClick.AddListener(() => SelectPassiveNode(captured));

        if (selectedNode == node)
        {
            // 고른 칸은 안쪽에 밝은 테두리를 하나 더 그린다.
            UIFactory.CreatePanel("Focus", cell.transform, UIPalette.SlotSelected,
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.12f));
        }

        UIFactory.CreateLabel(cell.transform, node.DisplayName, 22, FontStyle.Bold,
            new Vector2(0.06f, 0.42f), new Vector2(0.94f, 0.94f), TextAnchor.LowerCenter,
            learned || error == PassiveError.None ? UIPalette.Text : UIPalette.TextDim);

        UIFactory.CreateLabel(cell.transform,
            learned ? "배움" : node.EffectText, 20, FontStyle.Normal,
            new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.40f), TextAnchor.UpperCenter,
            learned ? UIPalette.TextAccent : UIPalette.TextDim);
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

        PassiveError error = manager.State.CanLearn(
            node, manager.AccountLevel, manager.Credits);

        UIFactory.CreateLabel(area, node.DisplayName, 32, FontStyle.Bold,
            new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.97f), TextAnchor.MiddleLeft);

        UIFactory.CreateLabel(area, node.EffectText, 28, FontStyle.Bold,
            new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.85f), TextAnchor.MiddleLeft,
            UIPalette.TextAccent);

        UIFactory.CreateLabel(area, node.Description, 24, FontStyle.Normal,
            new Vector2(0.06f, 0.44f), new Vector2(0.94f, 0.72f), TextAnchor.UpperLeft,
            UIPalette.TextDim);

        UIFactory.CreateLabel(area,
            $"요구 계정 Lv.{node.RequiredAccountLevel}\n비용 ₡ {node.Cost:N0}",
            24, FontStyle.Normal,
            new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.42f), TextAnchor.UpperLeft);

        if (learned)
        {
            UIFactory.CreateLabel(area, "이미 배웠습니다.", 26, FontStyle.Bold,
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.20f),
                TextAnchor.MiddleCenter, UIPalette.TextAccent);
            return;
        }

        if (error != PassiveError.None)
        {
            UIFactory.CreateLabel(area, PassiveErrorText.Describe(error), 24, FontStyle.Normal,
                new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.24f),
                TextAnchor.MiddleLeft, UIPalette.Warning);
        }

        Button learn = UIFactory.CreateButton(area, "배우기",
            new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.14f),
            UIPalette.Action, LearnSelectedPassive, 28);

        learn.interactable = error == PassiveError.None;
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
