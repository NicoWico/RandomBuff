﻿using MonoMod.RuntimeDetour;
using Newtonsoft.Json;
using RandomBuffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Expedition;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RandomBuff.Core.Game.Settings.Conditions
{
    public class ExterminationCondition : Condition
    {
        public static List<AbstractPhysicalObject.AbstractObjectType> weaponSelections;
        static ConditionalWeakTable<Creature, CreatureDmgSourceRecord> dmgSourceMapper = new ConditionalWeakTable<Creature, CreatureDmgSourceRecord>();
        static List<Hook> creatureViolenceHooks = new List<Hook>();
        static bool explosionFromScavengerBomb;

        public override ConditionID ID => ConditionID.Extermination;

        public override int Exp => overrideExp ?? killRequirement * 4;

        [JsonProperty]
        public AbstractPhysicalObject.AbstractObjectType weaponType;
        [JsonProperty]
        public int killRequirement;
        [JsonProperty]
        public int? overrideExp;

        [JsonProperty]
        public int kills;

        static ExterminationCondition()
        {
            weaponSelections = new()
            {
                AbstractPhysicalObject.AbstractObjectType.Spear,
                AbstractPhysicalObject.AbstractObjectType.ScavengerBomb,
                AbstractPhysicalObject.AbstractObjectType.Rock,
            };
            //TODO:暂时


        }

        public override void HookOn()
        {
            base.HookOn();
            On.Creature.Die += Creature_Die;
            On.Explosion.Update += Explosion_Update;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.SafeGetTypes())
                    {
                        if (type.IsSubclassOf(typeof(Creature)))
                            _ =new Hook(type.GetMethod("Violence", System.Reflection.BindingFlags.Instance |
                                                                   System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic), Creature_Violence);
                    }
                }
                catch (Exception ex)
                {
                    BuffUtils.LogException("ExterminationCondition", ex);
                }
            }
        }



        private void Explosion_Update(On.Explosion.orig_Update orig, Explosion self, bool eu)
        {
            if (self.sourceObject != null && self.sourceObject is ScavengerBomb)
                explosionFromScavengerBomb = true;
            orig.Invoke(self, eu);
            if (explosionFromScavengerBomb)
                explosionFromScavengerBomb = false;
        }

        private void Creature_Die(On.Creature.orig_Die orig, Creature self)
        {
            if (!self.dead)
            {
                if (dmgSourceMapper.TryGetValue(self, out var record))
                {
                    BuffUtils.Log("ExterminationCondition", $"{self} killed by {record.sourceObjType}");
                    if (record.sourceObjType == weaponType)
                    {
                        kills++;
                        if (kills >= killRequirement)
                            Finished = true;
                        onLabelRefresh?.Invoke(this);
                    }
                }
            }
            orig.Invoke(self);
        }

        static void Creature_Violence(On.Creature.orig_Violence orig, Creature self, BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
        {
            //return;
            try
            {
                //BuffUtils.Log("ExterminationCondition", $"{self} violence by {source.owner.abstractPhysicalObject.type}");
                if (source != null)
                {
                    if (dmgSourceMapper.TryGetValue(self, out var record))
                    {
                        record.sourceObjType = source.owner.abstractPhysicalObject.type;
                    }
                    else
                    {
                        dmgSourceMapper.Add(self, new CreatureDmgSourceRecord() { sourceObjType = source.owner.abstractPhysicalObject.type });
                    }
                }
                else if (explosionFromScavengerBomb)
                {
                    if (dmgSourceMapper.TryGetValue(self, out var record))
                        record.sourceObjType = AbstractPhysicalObject.AbstractObjectType.ScavengerBomb;
                    else
                        dmgSourceMapper.Add(self, new CreatureDmgSourceRecord() { sourceObjType = AbstractPhysicalObject.AbstractObjectType.ScavengerBomb });
                }
                orig.Invoke(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }

        public static bool TryGetRecord(Creature creature, out CreatureDmgSourceRecord result)
        {
            if(dmgSourceMapper.TryGetValue(creature,out result))
                return true;
            return false;
        }




        public override string DisplayName(InGameTranslator translator)
        {
            
            return string.Format(BuffResourceString.Get("DisplayName_Extermination"),killRequirement,BuffResourceString.Get(ChallengeTools.ItemName(weaponType),true));
        }

        public override string DisplayProgress(InGameTranslator translator)
        {
            return $"({kills}/{killRequirement})";
        }

        public override ConditionState SetRandomParameter(SlugcatStats.Name name, float difficulty, List<Condition> conditions)
        {
            weaponType = AbstractPhysicalObject.AbstractObjectType.Spear;
            killRequirement = Random.Range(10, 50);

            return ConditionState.Ok_NoMore;
        }

        public class CreatureDmgSourceRecord
        {
            public AbstractPhysicalObject.AbstractObjectType sourceObjType;
        }
    }
}
