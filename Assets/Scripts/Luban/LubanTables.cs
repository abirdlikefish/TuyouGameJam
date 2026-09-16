using System;
using System.IO;
using Luban.SimpleJSON;
using UnityEngine;

/// <summary>
/// Lazy access point for the generated Luban tables.
/// </summary>
public static class LubanTables
{
    private static cfg.Tables instance;

    public static cfg.Tables Instance => instance ??= Create();

    public static void Reload()
    {
        instance = Create();
    }

    private static cfg.Tables Create()
    {
        return new cfg.Tables(LoadJson);
    }

    private static JSONNode LoadJson(string file)
    {
        var path = Path.Combine(Application.streamingAssetsPath, "Luban", file + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Luban data file was not found: {path}", path);
        }

        return JSON.Parse(File.ReadAllText(path));
    }
}
