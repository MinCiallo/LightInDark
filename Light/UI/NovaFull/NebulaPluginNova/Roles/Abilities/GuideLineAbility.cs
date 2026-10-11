using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Abilities;

public class GuideLineAbility : IGameOperator
{
    SpriteRenderer guideRenderer;
    Func<bool> predicate;
    GamePlayer player;
    static private Image guideSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxeGuide.png", 100f);
    public GuideLineAbility(GamePlayer player, Func<bool> predicate)
    {
        this.player = player;
        guideRenderer = UnityHelper.CreateObject<SpriteRenderer>("Guideline", player.VanillaPlayer.transform, VVector3.Zero, LayerExpansion.GetObjectsLayer());
        guideRenderer.gameObject.SetActive(false);
        guideRenderer.transform.localScale = new(1.5f, 1.5f, 1f);
        guideRenderer.sprite = guideSprite.GetSprite();
        this.predicate = predicate;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        guideRenderer.gameObject.SetActive(predicate.Invoke());
        float angle = player.Unbox().MouseAngle;
        float degree = angle * 180f / Mathn.PI;
        guideRenderer.transform.localEulerAngles = new(0f, 0f, degree);
        float p = Mathf.Repeat(ev.ProcessTime / 1.2f, 1f);
        guideRenderer.transform.localPosition = (VVector2.Right.Rotate(degree) * (1.4f + Mathn.Sin(p * Mathn.PI * 0.5f) * 0.8f)).AsVector3(-10f);
        guideRenderer.color = new(1f, 1f, 1f, 1f - (p * p * p));
    }

    void IGameOperator.OnReleased()
    {
        GameObject.Destroy(guideRenderer.gameObject);
    }
}
