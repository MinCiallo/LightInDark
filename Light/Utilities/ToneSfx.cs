using System;
using System.IO;
using System.Reflection;
using Light.UI.MusicPlayer;
using UnityEngine;

namespace Light.Utilities;

public static class ToneSfx
{
    private static AudioClip? _gunload;
    private static AudioClip? _gunfire;

    public static void PlayGunload() => Play(ref _gunload, "Gunload");
    public static void PlayGunfire() => Play(ref _gunfire, "Gunfire");

    private static void Play(ref AudioClip? clip, string name)
    {
        if (clip == null) clip = Load(name);
        if (clip == null) return;

        var manager = SoundManager.Instance;
        if (manager == null) return;
        manager.PlaySound(clip, false, 0.8f);
    }

    private static AudioClip? Load(string name)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream($"Light.Resources.SFX.{name}.wav");
            if (stream == null) return null;

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return MusicClipLoader.CreateClipFromWavBytes(name, memory.ToArray());
        }
        catch
        {
            return null;
        }
    }
}
