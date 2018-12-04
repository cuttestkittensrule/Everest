﻿#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_PlayerHair : PlayerHair {

        // We're effectively in PlayerHair, but still need to "expose" private fields to our mod.
        private List<MTexture> bangs;
        private float wave;
        public float Wave => wave;

        // Celeste 1.1.3.0 (V1): private PlayerSprite sprite
        [MonoModIfFlag("V1:UserIOSave")]
        private PlayerSprite sprite;

        // Celeste 1.2.5.0 (V2): public PlayerSprite Sprite
        [MonoModIfFlag("V2:UserIOSave")]
        [MonoModLinkFrom("Celeste.PlayerSprite Celeste.PlayerHair::sprite")]
        private PlayerSprite Sprite; // Use most restrictive visibility.

        internal PlayerSprite _Sprite => sprite;

        public patch_PlayerHair(PlayerSprite sprite)
            : base(sprite) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        public override void Render() {
            if (!sprite.HasHair)
                return;

            Vector2 origin = new Vector2(5f, 5f);
            Color colorBorder = Border * Alpha;

            if (DrawPlayerSpriteOutline) {
                Color colorSprite = sprite.Color;
                Vector2 position = sprite.Position;
                sprite.Color = colorBorder;
                sprite.Position = position + new Vector2(0f, -1f);
                sprite.Render();
                sprite.Position = position + new Vector2(0f, 1f);
                sprite.Render();
                sprite.Position = position + new Vector2(-1f, 0f);
                sprite.Render();
                sprite.Position = position + new Vector2(1f, 0f);
                sprite.Render();
                sprite.Color = colorSprite;
                sprite.Position = position;
            }

            Nodes[0] = Nodes[0].Floor();
            if (colorBorder.A > 0) {
                for (int i = 0; i < sprite.HairCount; i++) {
                    MTexture hair = GetHairTexture(i);
                    Vector2 hairScale = GetHairScale(i);
                    hair.Draw(Nodes[i] + new Vector2(-1f, 0f), origin, colorBorder, hairScale);
                    hair.Draw(Nodes[i] + new Vector2(1f, 0f), origin, colorBorder, hairScale);
                    hair.Draw(Nodes[i] + new Vector2(0f, -1f), origin, colorBorder, hairScale);
                    hair.Draw(Nodes[i] + new Vector2(0f, 1f), origin, colorBorder, hairScale);
                }
            }

            for (int i = sprite.HairCount - 1; i >= 0; i--) {
                MTexture hair = GetHairTexture(i);
                hair.Draw(Nodes[i], origin, GetHairColor(i), GetHairScale(i));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public MTexture GetHairTexture(int index) {
            if (index == 0)
                return bangs[sprite.HairFrame];
            return GFX.Game["characters/player/hair00"];
        }

        [MonoModIgnore]
        private extern Vector2 GetHairScale(int index);
        public Vector2 PublicGetHairScale(int index) => GetHairScale(index);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Color GetHairColor(int index) {
            return Color * Alpha;
        }

    }
    public static class PlayerHairExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the current player hair texture for the given hair segment.
        /// </summary>
        public static MTexture GetHairTexture(this PlayerHair self, int index)
            => ((patch_PlayerHair) self).GetHairTexture(index);

        /// <summary>
        /// Get the current player hair scale for the given hair segment.
        /// </summary>
        public static Vector2 GetHairScale(this PlayerHair self, int index)
            => ((patch_PlayerHair) self).PublicGetHairScale(index);

        /// <summary>
        /// Get the current player hair color for the given hair segment.
        /// </summary>
        public static Color GetHairColor(this PlayerHair self, int index)
            => ((patch_PlayerHair) self).GetHairColor(index);

        /// <summary>
        /// Get the PlayerSprite which the PlayerHair belongs to.
        /// </summary>
        public static PlayerSprite GetSprite(this PlayerHair self)
            => ((patch_PlayerHair) self)._Sprite;

        /// <summary>
        /// Get the current wave, updated by Engine.DeltaTime * 4f each Update.
        /// </summary>
        public static float GetWave(this PlayerHair self)
            => ((patch_PlayerHair) self).Wave;

    }
}
