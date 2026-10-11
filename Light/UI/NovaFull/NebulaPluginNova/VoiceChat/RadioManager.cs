using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using Nebula.VoiceChat;
using System;
using System.Collections.Generic;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.VoiceChat;

internal enum RadioKind
{
    Impostor,
    Jackal,
    Lovers,
}

internal class RadioChannel
{
    public RadioKind Kind { get; }
    public string LocalizedName { get; }
    public VColor Color { get; }

    private readonly Func<GamePlayer, bool> canHear;
    private readonly ILifespan lifespan;
    public ILifespan Lifespan => lifespan;
    public bool IsDead => lifespan.IsDeadObject;

    internal RadioChannel(RadioKind kind, string localizedName, Func<GamePlayer, bool> canHear, ILifespan lifespan, VColor color)
    {
        Kind = kind;
        LocalizedName = localizedName;
        Color = color;
        this.canHear = canHear;
        this.lifespan = lifespan;
    }

    public bool CanHear(GamePlayer player) => canHear.Invoke(player);

    public int GetHearableMask()
    {
        int mask = 0;
        foreach (var player in GamePlayer.AllPlayers)
        {
            //CanHearの述語は役職を参照するため、役職未割り当てのプレイヤーは除外する
            if (player.Role == null || player.PlayerId >= 32) continue;
            if (CanHear(player)) mask |= 1 << player.PlayerId;
        }
        return mask;
    }
}

//ラジオの一覧を保持します。ボイスチャットが無効でも利用できます。
[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal class RadioManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public void Preprocess(NebulaPreprocessor preprocess) =>
        DIManager.Instance.RegisterModule(() => new RadioManager());

    private RadioManager()
    {
        ModSingleton<RadioManager>.Instance = this;
        this.RegisterPermanently();
    }

    private readonly List<RadioChannel> radios = [];
    public IReadOnlyList<RadioChannel> AllRadios => radios;

    public RadioChannel Register(RadioKind kind, string localizedName, Func<GamePlayer, bool> canHear, ILifespan lifespan, VColor color)
    {
        var radio = new RadioChannel(kind, localizedName, canHear, lifespan, color);
        radios.Add(radio);
        return radio;
    }

    //失効したラジオを取り除きます。
    void OnUpdate(GameUpdateEvent ev) => radios.RemoveAll(r => r.IsDead);
}
