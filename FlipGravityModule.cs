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
    public class FlipGravityModule : EverestModule
    {
        private static Hook disableCache;
        private static FieldInfo cacheField = typeof(MonoMod.Utils.ReflectionHelper).GetField("ResolveReflectionCache", BindingFlags.NonPublic | BindingFlags.Static);

        public static FlipGravityModule Instance;

        public override Type SettingsType => null;

        private static FieldInfo onGroundField = typeof(Player).GetField("onGround", BindingFlags.NonPublic | BindingFlags.Instance);
        private static PropertyInfo onSafeGroundProp = typeof(Player).GetProperty("OnSafeGround");

        public static bool IsFlipped = false;

        protected ILHook updateHook;

        public FlipGravityModule()
        {
            Instance = this;
        }

        public override void Load() {
            Logger.Log("FlipGravityModule", "Hooking MonoMod.Utils.ReflectionHelper._Cache");
            disableCache = new Hook(typeof(MonoMod.Utils.ReflectionHelper).GetMethod("_Cache", BindingFlags.NonPublic | BindingFlags.Static), new Func<Func<MemberReference, MemberInfo, MemberInfo>, MemberReference, MemberInfo, MemberInfo>((orig, k, v) => {
                return v;
            }));
            Logger.Log("FlipGravityModule", "Clearing cache");
            cacheField.SetValue(null, new Dictionary<string, MemberInfo>());
            Logger.Log("FlipGravityModule", "Patching NormalUpdate");
            IL.Celeste.Player.NormalUpdate += modNormalUpdate;
            Logger.Log("FlipGravityModule", "Patching orig_Update");
            updateHook = new ILHook(typeof(Player).GetMethod("orig_Update"), modUpdate);
            Logger.Log("FlipGravityModule", "Hooking Jump");
            On.Celeste.Player.Jump += jump;
            Logger.Log("FlipGravityModule", "OnLoadEntity");
            Everest.Events.Level.OnLoadEntity += LevelOnOnLoadEntity;
        }

        public override void Unload() {
            IL.Celeste.Player.NormalUpdate -= modNormalUpdate;
            updateHook.Dispose();
            disableCache.Dispose();
            On.Celeste.Player.Jump -= jump;
            Everest.Events.Level.OnLoadEntity -= LevelOnOnLoadEntity;
        }

        private static void jump(On.Celeste.Player.orig_Jump orig, Player self, bool particles, bool playSfx) {
            if (false) {
                IsFlipped = !IsFlipped;
            } else {
                orig(self, particles, playSfx);
            }
        }

        private void modUpdate(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // find out where the constant 900 (downward acceleration) is loaded into the stack
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt(onSafeGroundProp.GetMethod))) {
                // add two instructions to multiply those constants with the "gravity factor"
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<Player>>(player => antigravityCheck(player));
            } else {
                throw new Exception("couldnt match");
            }
        }

        private void antigravityCheck(Player self) {
            if (!IsFlipped) return;
            if (self.StateMachine.State == 9)
            {
                onGroundField.SetValue(self, false);
                onSafeGroundProp.SetValue(self, false);
            }
            else
            {
                if (self.Speed.Y <= 0f)
                {
                    var solid = self.CollideFirst<Solid>(self.Position - Vector2.UnitY);
                    if (solid == null)
                    {
                        var jumpthru = self.CollideFirstOutside<JumpThru>(self.Position - Vector2.UnitY);
                        if (jumpthru != null)
                        {
                            onGroundField.SetValue(self, true);
                            onSafeGroundProp.SetValue(self, jumpthru.Safe);
                        }
                        else
                        {
                            onGroundField.SetValue(self, false);
                            onSafeGroundProp.SetValue(self, false);
                        }
                    }
                    if (solid != null)
                    {
                        onGroundField.SetValue(self, true);
                        onSafeGroundProp.SetValue(self, solid.Safe);
                    }
                }
                else
                {
                    onGroundField.SetValue(self, false);
                    onSafeGroundProp.SetValue(self, false);
                }
            }
            if (self.Ducking && Input.MoveY != 1 && !self.CanUnDuck) {
                var pos = self.Position + new Vector2(0, self.Height / 0.6f);
                if (self.CanUnDuckAt(pos)) {
                    self.MoveV(self.Height / 0.6f);
                    self.Ducking = false;
                }
            }
        }

        /// <summary>
        /// Edits the NormalUpdate method in Player (handling the player state when not doing anything like climbing etc.)
        /// </summary>
        /// <param name="il">Object allowing CIL patching</param>
        private void modNormalUpdate(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // find out where the constant 900 (downward acceleration) is loaded into the stack
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(900f))) {
                Logger.Log("FlipGravityModule", $"Applying gravity to constant at {cursor.Index} in CIL code for NormalUpdate");

                // add two instructions to multiply those constants with the "gravity factor"
                cursor.EmitDelegate<Func<float>>(determineGravityFactor);
                cursor.Emit(OpCodes.Mul);
            }
        }

        /// <summary>
        /// Returns the currently configured gravity factor.
        /// </summary>
        /// <returns>The gravity factor (1 = default gravity)</returns>
        private float determineGravityFactor() {
            return IsFlipped ? -1f : 1f;
        }

        private static bool LevelOnOnLoadEntity(Level level, LevelData levelData, Vector2 offset, EntityData entityData) {
            const string prefix = "vvvvvv/";
            string entityName = entityData.Name;
            if (!entityName.StartsWith(prefix)) {
                return false;
            }

            entityName = entityName.Remove(0, prefix.Length);

            switch (entityName) {
                case "flipGravityTrigger":
                    level.Add(new FlipGravityTrigger(entityData, offset));
                    return true;
            }

            return false;
        }
    }
}
