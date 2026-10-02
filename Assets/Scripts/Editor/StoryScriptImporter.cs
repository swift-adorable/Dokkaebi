using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 이야기 본문(docs/Dokkaebi_Story_Script.txt)을 게임이 읽는 자리(Resources)로 옮긴다.
///
/// 【원본은 docs다.】 본문을 고치면 이 메뉴를 다시 실행한다.
/// 둘이 어긋나면 StoryScriptTests가 실패한다.
/// </summary>
public static class StoryScriptImporter
{
    public const string SourcePath = "docs/Dokkaebi_Story_Script.txt";
    public const string TargetPath = "Assets/Resources/Story/StoryScript.txt";

    [MenuItem("Dokkaebi/Story/이야기 본문 가져오기")]
    public static void Import()
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        string source = Path.Combine(root, SourcePath);

        if (!File.Exists(source))
        {
            Debug.LogError($"[StoryScript] 본문이 없습니다 — {SourcePath}");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, TargetPath)));
        File.Copy(source, Path.Combine(root, TargetPath), overwrite: true);
        AssetDatabase.ImportAsset(TargetPath);

        var passages = StoryScriptParser.Parse(File.ReadAllText(source));
        Debug.Log($"[StoryScript] 가져옴 — 토막 {passages.Count}개");
    }
}
