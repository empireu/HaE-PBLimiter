﻿using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Reflection;
using System;
using Sandbox;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.SessionComponents;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using Torch;
using Torch.API.Managers;
using Torch.Server;
using Torch.Managers;
using Torch.Managers.ChatManager;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using NLog;

namespace HaE_PBLimiter
{
    public class PBTracker
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public string PBID { get { return $"{PB.CubeGrid.DisplayName}-{PB.CustomName}"; } }        
        public bool IsEnabled => PB.Enabled;
        public double AverageMS => averageMs;
        public string Owner => MySession.Static.Players.TryGetIdentity(PB.OwnerId).DisplayName;


        private int StartupTicks => ProfilerConfig.startupTicks;
        public MyProgrammableBlock PB;
        public double averageMs;
        public DateTime lastExecutionTime;
        
        private int startTick;
        private int violations;
       

        public PBTracker(MyProgrammableBlock PB, double average)
        {
            this.PB = PB;
        }

        public void UpdatePerformance(double dt)
        {
            lastExecutionTime = DateTime.Now;

            if (startTick < StartupTicks)
            {
                startTick++;
                LastPerformanceUpdateFrame = MySession.Static.GameplayFrameCounter;
                return;
            }
 

            var ms = dt * 1000;

            UpdatePerformance();

            averageMs += ProfilerConfig.tickSignificance * ms;
        }

        int LastPerformanceUpdateFrame = MySession.Static.GameplayFrameCounter;

        public void UpdatePerformance() {
            int frame = MySession.Static.GameplayFrameCounter;

            if (frame == LastPerformanceUpdateFrame) {
                return;
            }

            averageMs = averageMs * Math.Pow(1 - ProfilerConfig.tickSignificance, frame - LastPerformanceUpdateFrame);
            
            LastPerformanceUpdateFrame = frame;
        }

        public void CheckMax(double maximumAverageMS)
        {
            if (averageMs > maximumAverageMS)
            {
                DamagePB();
            }
        }

        public bool CheckMax(long owner, double maximumAverageMS)
        {
            PBPlayerTracker.players[owner].ms += averageMs;
            PBPlayerTracker.players[owner].UpdatePB(PB, maximumAverageMS);

            if (PBPlayerTracker.players[owner].ms > maximumAverageMS)
            {
                return false;
            }

            return true;
        }

        public void SetRecompiled()
        {
            averageMs = 0;
            startTick = 0;
        }

        static FieldInfo needsInstansiationField = typeof(MyProgrammableBlock).GetField("m_needsInstantiation", BindingFlags.NonPublic | BindingFlags.Instance);
        static MethodInfo Terminate = typeof(MyProgrammableBlock).GetMethod("OnProgramTermination", BindingFlags.NonPublic | BindingFlags.Instance);


        public void DamagePB()
        {
            if (PB != null && !PB.IsFunctional)
                return;

            float damage = PB.SlimBlock.BlockDefinition.MaxIntegrity - PB.SlimBlock.BlockDefinition.MaxIntegrity * PB.SlimBlock.BlockDefinition.CriticalIntegrityRatio;
            damage += (float)(damage * (violations++ * ProfilerConfig.violationsMult));
            TorchBase.Instance.Invoke(() => 
            {
                try {
                    PB.SlimBlock.DoDamage(damage, MyDamageType.Fire, true, null, 0);
                    PB.Enabled = false;
                    needsInstansiationField.SetValue(PB, false);
                    Terminate.Invoke(PB, new object[] 
                    {
                        MyProgrammableBlock.ScriptTerminationReason.InstructionOverflow
                    });
                } catch (NullReferenceException)
                { }
            });

            Player owner;
            if (PBPlayerTracker.players.TryGetValue(PB.OwnerId, out owner))
            {
                owner.ms -= averageMs;
            }
            
            averageMs = 0;
            startTick = 0;
            ulong PBOwnerID = MySession.Static.Players.TryGetSteamId(PB.OwnerId);
            
            if(PBOwnerID != 0) { PBLimiter_Logic.server?.CurrentSession.Managers.GetManager<IChatManagerServer>().SendMessageAsOther("Server", $"Your PB {PBID} has overheated due to excessive usage!", MyFontEnum.Red, PBOwnerID); }
        }
    }
}
