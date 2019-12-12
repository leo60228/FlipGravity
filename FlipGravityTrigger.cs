using Celeste;
using System.Collections.Generic;
using Celeste.Mod;
using System;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Mono.Cecil.Cil;
using Mono.Cecil;
using Microsoft.Xna.Framework;
using Monocle;

namespace FlipGravity
{
    [Monocle.Tracked]
    public class FlipGravityTrigger : Trigger
    {
        public FlipGravityTrigger(EntityData data, Vector2 offset) : base(data, offset) {}

        public override void OnEnter(Player player) {
            FlipGravityModule.IsFlipped = !FlipGravityModule.IsFlipped;
        }
    }
}
