﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Menu;
using MoreSlugcats;
using RandomBuff.Core.Buff;
using RandomBuff.Core.BuffMenu;
using RandomBuff.Core.BuffMenu.Test;
using RandomBuff.Core.Game;
using RandomBuff.Core.SaveData;
using UnityEngine;

namespace RandomBuff.Core.Hooks
{
    static  partial class CoreHooks
    {
        public static void OnModsInit()
        {
            On.ProcessManager.PreSwitchMainProcess += ProcessManager_PreSwitchMainProcess;
            On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess;


            On.PlayerProgression.WipeSaveState += PlayerProgression_WipeSaveState;
            On.PlayerProgression.WipeAll += PlayerProgression_WipeAll;

      
            On.Menu.MainMenu.ctor += MainMenu_ctor;
            On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess1;

            On.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHud;

            InGameHooksInit();


            BuffPlugin.Log("Core Hook Loaded");

            //TestStartGameMenu = new("TestStartGameMenu");

        }

  

        private static void HUD_InitSinglePlayerHud(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
        {
            orig(self, cam);
            if(self.rainWorld.BuffMode())
                self.AddPart(new BuffHud(self));
        }

        private static void ProcessManager_PostSwitchMainProcess1(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            if (ID == TestStartGameMenu)
            {
                self.currentMainLoop = new BuffGameMenu(self, ID);
            }
            orig(self, ID);
      
        }

        public static ProcessManager.ProcessID TestStartGameMenu = new ("TestStartGameMenu");
        private static void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, Menu.MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
        {
            orig(self, manager, showRegionSpecificBkg);

            float buttonWidth = MainMenu.GetButtonWidth(self.CurrLang);
            Vector2 pos = new Vector2(683f - buttonWidth / 2f, 0f);
            Vector2 size = new Vector2(buttonWidth, 30f);
            self.AddMainMenuButton(new SimpleButton(self, self.pages[0], "BUFF", "BUFF", pos, size), () =>
            {
                self.manager.RequestMainProcessSwitch(TestStartGameMenu);
                self.PlaySound(SoundID.MENU_Switch_Page_In);
            }, 0);
        }

        private static void ProcessManager_PreSwitchMainProcess(On.ProcessManager.orig_PreSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            if (self.rainWorld.BuffMode() && ID == ProcessManager.ProcessID.MainMenu)
            {
                int lastSlot = self.rainWorld.options.saveSlot;
                self.rainWorld.options.saveSlot -= 100;

                BuffPlugin.Log($"Change slot from {lastSlot} to {self.rainWorld.options.saveSlot}");
                self.rainWorld.progression.Destroy(lastSlot);
                self.rainWorld.progression = new PlayerProgression(self.rainWorld, true, true);
            }
            orig(self, ID);
        }

        private static void PlayerProgression_WipeAll(On.PlayerProgression.orig_WipeAll orig, PlayerProgression self)
        {
            orig(self);
            if (self.rainWorld.BuffMode())
                BuffDataManager.Instance.DeleteAll();

        }

        private static void PlayerProgression_WipeSaveState(On.PlayerProgression.orig_WipeSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber)
        {
            orig(self,saveStateNumber);
            if(self.rainWorld.BuffMode())
                BuffDataManager.Instance.DeleteSaveData(saveStateNumber);
        }


        private static void ProcessManager_PostSwitchMainProcess(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            if (BuffPoolManager.Instance != null &&
                self.oldProcess is RainWorldGame game &&
                (ID == ProcessManager.ProcessID.SleepScreen || ID == ProcessManager.ProcessID.Dream) &&
                BuffDataManager.Instance.GetSafeSetting(game.StoryCharacter).instance.CurrentPacket.NeedMenu)
            {
                BuffPoolManager.Instance.Destroy();
                self.currentMainLoop = new GachaMenu.GachaMenu(ID, game, self);
                ID = GachaMenu.GachaMenu.GachaMenuID;
            }

            orig(self, ID);
            if (self.currentMainLoop is RainWorldGame game2 && 
                BuffPoolManager.Instance == null &&
                game2.rainWorld.BuffMode())
            {
                BuffPoolManager.LoadGameBuff(game2);
            }
        }

        public static bool BuffMode(this RainWorld rainWorld)
        {
            return rainWorld.options.saveSlot >= 100;
        }
    }
}
