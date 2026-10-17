// Nightfall - Copyright (C) 2026 DaUnknown-0
// Licensed under GPL-3.0-or-later. See LICENSE for details.

/*
 * GpuModels - where the map model packages (<map>.nfm, see GpuWorld) come from.
 *
 * Folder: BepInEx/nightfall-models. Two kinds of package live there:
 *   - the ones in GpuModelCatalog (Unknown's Atlas maps, own artwork): downloaded from the public
 *     Nightfall-Models repository at the catalogue's tag and checked against size and SHA-256, on
 *     every start, like Unknown's Atlas' own pictures;
 *   - any other <map>.nfm a player puts there himself (the vanilla maps: their textures are drawn
 *     from Among Us' own artwork and are deliberately not published). Used if the header reads.
 * A package is only "ready" once it is complete; a half-written file is never opened.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace Nightfall;

internal static class GpuModels
{
    internal static string Folder => Path.Combine(BepInEx.Paths.BepInExRootPath, "nightfall-models");
    private static string Source => $"https://raw.githubusercontent.com/DaUnknown-0/Nightfall-Models/{GpuModelCatalog.Tag}/";

    private static readonly object Gate = new();
    private static readonly Dictionary<string, (long len, long ticks, bool ok)> checkedFiles = new(StringComparer.OrdinalIgnoreCase);
    private static bool started;

    /// Path of a ready package for `key`, or null.
    public static string PathFor(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        string path = Path.Combine(Folder, key + ".nfm");
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists || fi.Length < 64) return null;
            lock (Gate)
            {
                if (checkedFiles.TryGetValue(key, out var c) && c.len == fi.Length && c.ticks == fi.LastWriteTimeUtc.Ticks)
                    return c.ok ? path : null;
            }
            bool ok = HeaderOk(path) && CatalogOk(key, fi);
            lock (Gate) checkedFiles[key] = (fi.Length, fi.LastWriteTimeUtc.Ticks, ok);
            return ok ? path : null;
        }
        catch { return null; }
    }

    private static bool HeaderOk(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var b = new byte[8];
        return fs.Read(b, 0, 8) == 8 && b[0] == 'N' && b[1] == 'F' && b[2] == 'M' && b[3] == '2' && b[4] == 2;
    }

    /// Catalogue packages must match their size (the hash is checked once at start, in Begin).
    private static bool CatalogOk(string key, FileInfo fi)
    {
        foreach (var (name, size, _) in GpuModelCatalog.Files)
            if (string.Equals(name, key + ".nfm", StringComparison.OrdinalIgnoreCase)) return fi.Length == size;
        return true;
    }

    /// Once per session, from NightfallUpdater.Start: verify the catalogue packages and fetch what is
    /// missing or damaged, one after the other in the background.
    public static void Begin(MonoBehaviour host)
    {
        if (started || host == null) return;
        started = true;
        if (NightfallPlugin.GpuWorldEnabled?.Value == false) return;
        host.StartCoroutine(Run());
    }

    private static IEnumerator Run()
    {
        var check = Task.Run(() =>
        {
            var need = new List<(string name, long size, string sha)>();
            Directory.CreateDirectory(Folder);
            foreach (var e in GpuModelCatalog.Files)
            {
                try
                {
                    string p = Path.Combine(Folder, e.Name);
                    if (!File.Exists(p) || new FileInfo(p).Length != e.Size || Sha(File.ReadAllBytes(p)) != e.Sha256)
                        need.Add(e);
                }
                catch { need.Add(e); }
            }
            return need;
        });
        while (!check.IsCompleted) yield return null;
        if (check.IsFaulted) yield break;
        foreach (var (name, size, sha) in check.Result)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var www = new UnityWebRequest();
                www.SetMethod(UnityWebRequest.UnityWebRequestMethod.Get);
                www.SetUrl(Source + Uri.EscapeDataString(name));
                www.SetRequestHeader("User-Agent", "Nightfall-Models");
                www.downloadHandler = new DownloadHandlerBuffer();
                www.timeout = 180;
                var op = www.SendWebRequest();
                while (!op.isDone) yield return null;
                byte[] data = null;
                string error = null;
                if (www.isNetworkError || www.isHttpError) error = $"HTTP {www.responseCode} {www.error}";
                else data = www.downloadHandler.data;
                www.downloadHandler.Dispose();
                www.Dispose();
                if (error == null)
                {
                    var store = Task.Run(() => Store(name, size, sha, data));
                    while (!store.IsCompleted) yield return null;
                    error = store.IsFaulted ? store.Exception?.GetBaseException().Message : store.Result;
                }
                if (error == null)
                {
                    NightfallPlugin.Logger?.LogInfo($"[Nightfall] model package {name} downloaded ({size / 1048576f:0.0} MB).");
                    break;
                }
                NightfallPlugin.Logger?.LogWarning($"[Nightfall] model package {name}: {error} (try {attempt}/3)");
                float until = Time.realtimeSinceStartup + 10f * attempt;
                while (Time.realtimeSinceStartup < until) yield return null;
            }
        }
    }

    private static string Store(string name, long size, string sha, byte[] data)
    {
        if (data == null || data.Length != size) return $"size {data?.Length ?? 0} instead of {size}";
        if (Sha(data) != sha) return "checksum does not match";
        string path = Path.Combine(Folder, name), tmp = path + ".part";
        File.WriteAllBytes(tmp, data);
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
        return null;
    }

    private static string Sha(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data)).ToLowerInvariant();
    }
}
