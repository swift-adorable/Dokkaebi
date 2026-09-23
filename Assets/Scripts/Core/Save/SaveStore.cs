using System;
using System.IO;
using UnityEngine;

/// <summary>불러오기 결과. 무엇에서 읽었는지까지 알려 준다.</summary>
public enum SaveLoadResult
{
    /// <summary>파일이 없다 — 처음 시작이다.</summary>
    None = 0,

    /// <summary>본 파일에서 읽었다.</summary>
    Main = 1,

    /// <summary>본 파일이 깨져 백업에서 읽었다.</summary>
    Backup = 2,

    /// <summary>둘 다 깨졌다. 새로 시작하되 【깨진 파일은 지우지 않는다.】</summary>
    Corrupt = 3,

    /// <summary>더 새 판의 세이브다. 읽지도 덮어쓰지도 않는다.</summary>
    TooNew = 4
}

/// <summary>
/// 세이브를 디스크에 쓰고 읽는다. MonoBehaviour 의존이 없어 경로만 바꾸면
/// EditMode에서 전부 확인된다.
///
/// 【백업 파일을 하나 둔다.】 덕코프도 macOS에서 보조 저장 폴더를 두고,
/// 본 세이브가 사라졌을 때 거기서 되살리는 길이 알려져 있다. [확인됨 — 가이드]
/// 모바일에서는 저장 도중 앱이 죽는 일이 흔하다 — 쓰다 만 파일 하나로
/// 몇십 시간이 사라지면 안 된다.
///
/// 【쓰는 순서】 임시 파일에 다 쓴다 → 지금 본 파일을 백업으로 옮긴다 →
/// 임시 파일을 본 파일로 옮긴다. 중간에 죽어도 최소한 백업은 온전하다.
/// </summary>
public static class SaveStore
{
    public const string FileName = "blob_save.json";

    private const string BackupSuffix = ".bak";
    private const string TempSuffix = ".tmp";

    /// <summary>기본 경로. 테스트는 다른 폴더를 넘긴다.</summary>
    public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

    public static string ToJson(SaveData data)
        => JsonUtility.ToJson(data, prettyPrint: true);

    /// <summary>레벨을 담았던 옛 키들. 판 1·2 → 판 3 순서.</summary>
    private static readonly string[] OldLevelKeys = { "\"accountLevel\"", "\"awakeningLevel\"" };

    /// <summary>
    /// 옛 판의 키 이름을 새 이름으로 바꾼다. JsonUtility가 읽기 전에 한다.
    ///
    /// 【문자열로 바꾸는 이유】 옛 키를 받을 필드를 SaveData에 남겨 두면 새로
    /// 저장할 때마다 쓸모없는 키가 같이 써지고, 두 이름이 한 파일에 공존한다.
    /// 판 1·2의 "accountLevel"과 판 3의 "awakeningLevel"은 판 4의 "level"이다
    /// (결정 2-33, 용어 통일 2026-09-23). 새 키가 이미 있으면 건드리지 않는다.
    /// </summary>
    public static string Migrate(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        const string newKey = "\"level\"";
        if (json.Contains(newKey))
            return json;

        foreach (var oldKey in OldLevelKeys)
        {
            if (json.Contains(oldKey))
                return json.Replace(oldKey, newKey);
        }

        return json;
    }

    /// <summary>JSON을 읽는다. 깨졌으면 null.</summary>
    public static SaveData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonUtility.FromJson<SaveData>(Migrate(json));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Write(string path, SaveData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        string directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        data.version = SaveData.CurrentVersion;
        data.savedAtUtc = DateTime.UtcNow.ToString("o");

        string temp = path + TempSuffix;
        string backup = path + BackupSuffix;

        File.WriteAllText(temp, ToJson(data));

        if (File.Exists(path))
        {
            if (File.Exists(backup))
                File.Delete(backup);

            File.Move(path, backup);
        }

        File.Move(temp, path);
    }

    /// <summary>
    /// 읽는다. 본 파일이 깨졌으면 백업을 본다.
    ///
    /// 【깨진 파일을 지우지 않는다.】 복구 도구로 살릴 수 있는 것을 우리가
    /// 먼저 없애면 안 된다. 다음 저장이 덮어쓸 때까지 그대로 둔다.
    /// </summary>
    public static SaveLoadResult Read(string path, out SaveData data)
    {
        data = null;

        bool mainExists = File.Exists(path);
        bool backupExists = File.Exists(path + BackupSuffix);

        if (!mainExists && !backupExists)
            return SaveLoadResult.None;

        SaveData main = mainExists ? FromJson(SafeRead(path)) : null;

        if (main != null)
        {
            if (main.version > SaveData.CurrentVersion)
                return SaveLoadResult.TooNew;

            data = main;
            return SaveLoadResult.Main;
        }

        SaveData backup = backupExists ? FromJson(SafeRead(path + BackupSuffix)) : null;

        if (backup != null)
        {
            if (backup.version > SaveData.CurrentVersion)
                return SaveLoadResult.TooNew;

            data = backup;
            return SaveLoadResult.Backup;
        }

        return SaveLoadResult.Corrupt;
    }

    /// <summary>
    /// 둘 다 깨졌을 때, 새로 쓰기 전에 깨진 파일을 옆으로 비켜 둔다.
    ///
    /// Write는 본 파일을 백업으로 밀고 옛 백업을 지운다. 깨진 둘을 그대로 두고
    /// 새로 쓰면 그 순간 둘 다 사라진다 — 「깨진 파일을 지우지 않는다」가
    /// 저장 한 번에 깨진다. 이름을 바꿔 두면 복구 도구가 나중에 볼 수 있다.
    /// </summary>
    public static void Quarantine(string path)
    {
        string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        foreach (string file in new[] { path, path + BackupSuffix })
        {
            if (File.Exists(file))
                File.Move(file, $"{file}.corrupt-{stamp}");
        }
    }

    /// <summary>본 파일과 백업을 지운다. 디버그 도구만 쓴다.</summary>
    public static void Delete(string path)
    {
        foreach (string file in new[] { path, path + BackupSuffix, path + TempSuffix })
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    private static string SafeRead(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
