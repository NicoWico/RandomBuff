﻿using BuiltinBuffs.Duality;
using BuiltinBuffs.Positive;
using HotDogGains.Negative;
using RandomBuff.Core.Buff;
using RandomBuff.Core.Entry;
using RandomBuffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;
using RWCustom;
using RandomBuff;

namespace BuiltinBuffs.Negative
{
    internal class ScorchingSunBuff : IgnitionPointBaseBuff<ScorchingSunBuff, ScorchingSunBuffData>
    {
        public override BuffID ID => ScorchingSunBuffEntry.ScorchingSun;

        public ScorchingSunBuff() : base()
        {
        }

        public override void Destroy()
        {
            if (ScorchingSunBuffEntry.Effect != null)
            {
                ScorchingSunBuffEntry.Effect.Destroy();
                ScorchingSunBuffEntry.Effect = null;
            }

            base.Destroy();
        }
    }

    internal class ScorchingSunBuffData : CountableBuffData
    {
        public override BuffID ID => ScorchingSunBuffEntry.ScorchingSun;
        public override int MaxCycleCount => 1;
    }

    internal class ScorchingSunBuffEntry : IBuffEntry
    {
        public static BuffID ScorchingSun = new BuffID("ScorchingSun", true);
        public static ConditionalWeakTable<Player, ScorchingSunPlayer> ScorchingSunFeatures = new ConditionalWeakTable<Player, ScorchingSunPlayer>();
        private static ScorchingSunSingleColorEffect effect;

        public static ScorchingSunSingleColorEffect Effect
        {
            get
            {
                return effect;
            }
            set
            {
                effect = value;
            }
        }

        public void OnEnable()
        {
            BuffRegister.RegisterBuff<ScorchingSunBuff, ScorchingSunBuffData, ScorchingSunBuffEntry>(ScorchingSun);
        }

        public static void HookOn()
        {
            On.Room.Update += Room_Update;
            On.Player.Update += Player_Update;
            On.ThreatTracker.ThreatOfTile += ThreatTracker_ThreatOfTile;
            /*
            #region 标签
            On.Creature.Update += Creature_Update;

            On.RainWorldGame.ExitGame += delegate (On.RainWorldGame.orig_ExitGame o, RainWorldGame s, bool death, bool quit)
            {
                o.Invoke(s, death, quit);
                ClearLabels();
            };
            On.RainWorldGame.ExitToMenu += delegate (On.RainWorldGame.orig_ExitToMenu o, RainWorldGame s)
            {
                o.Invoke(s);
                ClearLabels();
            };
            On.RainWorldGame.Win += delegate (On.RainWorldGame.orig_Win o, RainWorldGame s, bool malnourished)
            {
                o.Invoke(s, malnourished);
                ClearLabels();
            };
            On.ArenaSitting.NextLevel += delegate (On.ArenaSitting.orig_NextLevel o, ArenaSitting s, ProcessManager procmgr)
            {
                o.Invoke(s, procmgr);
                ClearLabels();
            };
            On.ArenaSitting.SessionEnded += delegate (On.ArenaSitting.orig_SessionEnded o, ArenaSitting s, ArenaGameSession session)
            {
                o.Invoke(s, session);
                ClearLabels();
            };
            #endregion
            */
        }

        private static float ThreatTracker_ThreatOfTile(On.ThreatTracker.orig_ThreatOfTile orig, ThreatTracker self, WorldCoordinate coord, bool accountThreatCreatureAccessibility)
        {
            float threat = orig.Invoke(self, coord, accountThreatCreatureAccessibility);
            if (self.AI.creature.realizedCreature != null && self.AI.creature.realizedCreature.room != null)
            {
                var creature = self.AI.creature.realizedCreature;
                var room = creature.room;

                if (OutdoorLevel(room) < 2f)
                    return threat;

                if (IsBeingExposedToSunlight(room, room.MiddleOfTile(coord)))
                {
                    IntVector2 skyTile = new IntVector2(room.Width / 2, room.Height);
                    threat += Mathf.InverseLerp(0f, 20f, Custom.ManhattanDistance(skyTile, coord.Tile));
                }

            }

            return threat;
        }

        private static void Room_Update(On.Room.orig_Update orig, Room self)
        {
            orig(self);

            if (self != null)
            {
                int num = OutdoorLevel(self);
                if (num < 0)
                {
                    return;
                }
                else
                {
                    if (self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.HeatWave) == null)
                        self.roomSettings.effects.Add(new RoomSettings.RoomEffect(RoomSettings.RoomEffect.Type.HeatWave, 0.2f * num, false));
                    else if (self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.HeatWave).amount < 0.2f * num)
                        self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.HeatWave).amount = 0.2f * num;

                    if (self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.Contrast) == null)
                        self.roomSettings.effects.Add(new RoomSettings.RoomEffect(RoomSettings.RoomEffect.Type.Contrast, 0.1f * num, false));
                    else if (self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.Contrast).amount < 0.15f * num)
                        self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.Contrast).amount = 0.15f * num;

                    if (self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.Brightness) == null)
                        self.roomSettings.effects.Add(new RoomSettings.RoomEffect(RoomSettings.RoomEffect.Type.Brightness, 0.1f * num, false));
                    else if (self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.Brightness).amount < 0.15f * num)
                        self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.Brightness).amount = 0.15f * num;

                    if (self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.Bloom) == null)
                        self.roomSettings.effects.Add(new RoomSettings.RoomEffect(RoomSettings.RoomEffect.Type.Bloom, 0.1f * num, false));
                    else if (self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.Bloom).amount < 0.15f * num)
                        self.roomSettings.GetEffect(RoomSettings.RoomEffect.Type.Bloom).amount = 0.15f * num;


                    for (int k = 0; k < self.abstractRoom.creatures.Count; k++)
                    {
                        if (self.abstractRoom.creatures[k].realizedCreature != null &&
                            self.abstractRoom.creatures[k].realizedCreature.room != null)
                        {
                            Creature creature = self.abstractRoom.creatures[k].realizedCreature;

                            if (TemperatureModule.TryGetTemperatureModule(creature, out var heatModule))
                            {
                                float mul = 1f;
                                if (!IsBeingExposedToSunlight(self, creature))
                                    mul = 0.95f;

                                float heatAdd = (heatModule.coolOffRate / 40f) * Mathf.Lerp(1.1f, 1.5f, Mathf.InverseLerp(num, 1, 3)) * mul;
                                heatModule.AddTemperature(heatAdd);
                            }
                            else
                            {
                                heatModule = new CreatureTemperatureModule(creature);
                                TemperatureModule.temperatureModuleMapping.Add(creature, heatModule);
                            }
                            //creature.Hypothermia = Mathf.Min(0, creature.Hypothermia - 0.02f * num);//这行会导致拾荒飞天
                        }
                    }
                }
            }
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig.Invoke(self, eu);

            if (ScorchingSunFeatures.TryGetValue(self, out var scorchingSunPlayer))
            {
                if (self.room != null)
                {
                    int num = OutdoorLevel(self.room);

                    if (effect != null)
                    {
                        if (self.room != effect.Room)//self.room != null && 
                        {
                            effect.Reset(0.1f * num, self.room);
                            BuffPlugin.Log($"Reset ScorchingSunSingleColorEffect, Level:{num}");
                        }
                    }
                    if (effect == null)
                    {
                        effect = new ScorchingSunSingleColorEffect(1, -1f, 1f, 1f, Custom.hexToColor("000000"), Custom.hexToColor("E3AB4E"), 0.1f * num, self.room);
                        BuffPostEffectManager.AddEffect(effect);
                    }

                    if (!self.Stunned)
                    {
                        if (num > 0)
                            self.AerobicIncrease(0.03f * num);
                        //if (scorchingSunPlayer.LastStun >= scorchingSunPlayer.CoolingTime)
                        //    self.AerobicIncrease(0.03f * num);
                    }

                    float heatstroke = 0;
                    if (TemperatureModule.TryGetTemperatureModule(self, out var heatModule))
                    {
                        heatstroke = Mathf.Clamp01(heatModule.temperature / heatModule.ignitingPoint);
                    }

                    if (!self.Stunned && scorchingSunPlayer.LastStun >= scorchingSunPlayer.CoolingTime &&
                        Random.Range(0f, 3000f + (1f + 0.3f * num) * 5f * heatstroke) > 3000f)
                    {
                        scorchingSunPlayer.LastStun = 0;
                        scorchingSunPlayer.CoolingTime = Mathf.RoundToInt(Random.Range(400f, 1200f) / (1f + 0.3f * num * heatstroke));
                        ScorchingSunBuff.Instance.TriggerSelf();
                        self.Stun(80);
                    }

                    scorchingSunPlayer.LastStun++;
                }
            }
            else
            {
                ScorchingSunFeatures.Add(self, new ScorchingSunPlayer(self));
            }
        }

        private static int OutdoorLevel(Room room)
        {
            int num = -1;
            if (room.abstractRoom.skyExits > 0)
                num = 3;
            if (room.roomSettings.DangerType == RoomRain.DangerType.Rain)
                num = 2;
            else if (room.roomSettings.DangerType == RoomRain.DangerType.FloodAndRain)
                num = 1;

            for (int k = 0; k < room.borderExits.Length; k++)
            {
                if (room.borderExits[k].type == AbstractRoomNode.Type.SkyExit)
                {
                    num = 3;
                    break;
                }
            }
            return num;
        }

        private static bool IsBeingExposedToSunlight(Room room, Creature creature)
        {
            return IsBeingExposedToSunlight(room, creature.mainBodyChunk.pos);
        }

        private static bool IsBeingExposedToSunlight(Room room, Vector2 pos)
        {
            if (room.abstractRoom.skyExits < 1)
            {
                return false;
            }
            Vector2 corner = Custom.RectCollision(pos, pos + 100000f * Vector2.up, room.RoomRect).GetCorner(FloatRect.CornerLabel.D);
            if (SharedPhysics.RayTraceTilesForTerrainReturnFirstSolid(room, pos, corner) != null)
            {
                return false;
            }
            if (corner.y >= room.PixelHeight - 5f)
            {
                return true;
            }
            return false;
        }

        #region 标签
        private static void Creature_Update(On.Creature.orig_Update orig, Creature self, bool eu)
        {
            orig.Invoke(self, eu);
            if (self.room != null && !HeatOverHead.Instances.ContainsKey(self))
            {
                new HeatOverHead(self);
            }
        }

        private static void ClearLabels()
        {
            foreach (HeatOverHead heatOverHead in HeatOverHead.Instances.Values.ToList<HeatOverHead>())
            {
                heatOverHead.Destroy();
            }
        }
        #endregion
    }

    internal class ScorchingSunPlayer
    {
        WeakReference<Player> ownerRef;

        public int LastStun { get; set; }
        public int CoolingTime { get; set; }

        public ScorchingSunPlayer(Player c)
        {
            ownerRef = new WeakReference<Player>(c);
            LastStun = 0;
            CoolingTime = 400;
        }
    }

    internal class ScorchingSunSingleColorEffect : BuffPostEffectLimitTime
    {
        public ScorchingSunSingleColorEffect(int layer, float duringTime, float enterTime, float fadeTime, Color start, Color end, float maxInst, Room room) : base(layer, duringTime, enterTime, fadeTime)
        {
            this.start = start;
            this.end = end; 
            this.lastMaxInst = 0f;
            this.maxInst = maxInst;
            this.room = room;
            material = new Material(StormIsApproachingEntry.SingleColor);
        }

        protected override float LerpAlpha => Mathf.InverseLerp(0, enterTime, 1 - lifeTime);

        public override void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            base.OnRenderImage(source, destination);
            material.SetColor("singleColorStart", start);
            material.SetColor("singleColorEnd", end);
            material.SetFloat("lerpValue", Mathf.Lerp(lastMaxInst, maxInst, LerpAlpha));

            Graphics.Blit(source, destination, material);

            if (this.Faded)
                this.Fade();

            if (this.room == null)
                this.Destroy();
        }

        public void Fade()
        {
            maxInst = Mathf.Max(0f, maxInst - 0.01f);
        }

        public void Reset(float maxInst, Room room)
        {
            this.lifeTime = 1f;
            this.lastMaxInst = this.maxInst;
            this.maxInst = maxInst;
            this.room = room;
        }

        private Room room;
        private Color start;
        private Color end;
        private float maxInst;
        private float lastMaxInst;
        private bool faded;

        public Room Room 
        { 
            get 
            { 
                return room; 
            } 
        }  

        public bool Faded
        {
            get
            {
                 return faded;
            }
            set
            {
                faded = value;
            }
        }
    }

    #region 标签
    internal class HeatOverHead : CosmeticSprite
    {
        private Creature obj;

        public static bool CreatureIDLabelVisible = true;// false;

        public static bool ObjectIDLabelVisible = true;// false;

        public static bool StatsVisible = true;// false;

        public static Dictionary<PhysicalObject, HeatOverHead> Instances { get; } = new Dictionary<PhysicalObject, HeatOverHead>();

        private float Heat
        {
            get
            {
                if (TemperatureModule.TryGetTemperatureModule(obj, out var heatModule))
                    return heatModule.temperature / heatModule.coolOffRate;
                else 
                    return -1;
            }
        }

        public static bool OkayToCreateNewHeatOverHeads => CreatureIDLabelVisible || ObjectIDLabelVisible || StatsVisible;

        public HeatOverHead(Creature o)
        {
            (obj = o).room.AddObject(this);
            Instances.Add(o, this);
        }

        public override void Update(bool eu)
        {
            if (obj.room != null && room != obj.room)
            {
                room.RemoveObject(this);
                obj.room.AddObject(this);
            }
            base.Update(eu);
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser leaser, RoomCamera cam, RoomPalette pal)
        {
            leaser.sprites[0].color = Color.black;
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser leaser, RoomCamera cam)
        {
            FSprite fSprite = new FSprite("pixel")
            {
                scaleX = 44f,
                scaleY = 15f
            };
            HeatOverHeadFContainer fContainer = new HeatOverHeadFContainer();
            leaser.sprites = new FSprite[1] { fSprite };
            FContainer[] containers = new HeatOverHeadFContainer[1] { fContainer };
            leaser.containers = containers;
            HeatOverHeadFContainer fContainer2 = new HeatOverHeadFContainer();
            fContainer2.AddChild(fSprite);
            fContainer2.AddChild(new FLabel(Custom.GetDisplayFont(), $"{Heat}")
            {
                anchorX = 0.5f,
                scale = 0.75f,
                y = 1f
            });
            fContainer.AddChild(fContainer2);
            cam.ReturnFContainer("HUD").AddChild(fContainer);
            
        }
        static FLabel Lbl(string text, Color color, Vector2 pos, float scale = 1f)
        {
            return new FLabel(Custom.GetDisplayFont(), text)
            {
                anchorX = 0.5f,
                scale = scale,
                color = color,
                x = pos.x,
                y = pos.y
            };
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser leaser, RoomCamera cam, float time, Vector2 campos)
        {
            FContainer top = leaser.containers[0];
            HeatOverHeadFContainer fContainer = top.GetChildAt(0) as HeatOverHeadFContainer;
            /*if (room == null || !room.BeingViewed || obj.room == null || !obj.room.BeingViewed || 
                (!ObjectIDLabelVisible && !(obj is Creature)) || (!ObjectIDLabelVisible && !CreatureIDLabelVisible && !StatsVisible) || 
                (obj is Overseer overseer && (overseer.mode == Overseer.Mode.SittingInWall || overseer.mode == Overseer.Mode.Withdrawing || overseer.mode == Overseer.Mode.Zipping)) ||
                (obj is Fly fly && fly.BitesLeft == 0) || (obj is Player player && !player.isNPC))
            {
                hide_all();
            }
            else
            {*/
                show_all();
                BodyChunk bodyChunk = (obj as Creature)?.mainBodyChunk ?? obj.firstChunk;
                Vector2 vector = Vector2.Lerp(bodyChunk.lastPos, bodyChunk.pos, time) - campos;
                FContainer fContainer4 = top;
                FContainer fContainer5 = top;
                float x = vector.x;
                float y = vector.y + 53f;
                fContainer4.x = x;
                fContainer5.y = y;
                fContainer.isVisible = ((obj is Creature) ? CreatureIDLabelVisible : ObjectIDLabelVisible);
                if (obj is Player player2 && !player2.isNPC)
                {
                    fContainer.isVisible = fContainer.isVisible;// && Cfg.Players.Value;
                }
                if (fContainer.isVisible)
                {
                    FSprite fSprite = fContainer.GetChildAt(0) as FSprite;
                    FLabel fLabel = fContainer.GetChildAt(1) as FLabel;
                    if (obj is Creature && TemperatureModule.TryGetTemperatureModule(obj as Creature, out var heatModule))
                    {
                        fLabel.text = (heatModule.coolOffRate / heatModule.ignitingPoint).ToString();
                    }
                    float num = fLabel.textRect.xMax - fLabel.textRect.xMin;
                    fSprite.scaleX = num + 10f;
                }
            //}
            base.DrawSprites(leaser, cam, time, campos);
            void hide_all()
            {
                top.isVisible = false;
            }
            void show_all()
            {
                top.isVisible = true;
            }
        }

        public override void Destroy()
        {
            RemoveFromRoom();
            Instances.Remove(obj);
            BuffPlugin.Log($"Active label count {Instances.Count}");
            base.Destroy();
        }
    }

    internal class HeatOverHeadFContainer : FContainer
    {
        public FNode this[int i]
        {
            get
            {
                return base.GetChildAt(i);
            }
        }
    }
    #endregion
}
