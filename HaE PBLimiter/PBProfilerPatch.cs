﻿using System.Reflection;
using System.Diagnostics;
using NLog;
using Sandbox.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using Torch.Utils;
using Torch.Utils.Reflected;


namespace HaE_PBLimiter
{
    [ReflectedLazy]
    internal static class PBProfilerPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        [ReflectedMethodInfo(typeof(MyProgrammableBlock), "ExecuteCode")]
        private static readonly MethodInfo _programmableRunSandboxed;

        [ReflectedMethodInfo(typeof(MyProgrammableBlock), "Compile")]
        private static readonly MethodInfo _programableRecompile;


        public static void Patch(PatchContext ctx)
        {
            ReflectedManager.Process(typeof(PBProfilerPatch));

            ctx.GetPattern(_programmableRunSandboxed).Prefixes.Add(ReflectionUtils.StaticMethod(typeof(PBProfilerPatch), nameof(PrefixProfilePb)));
            ctx.GetPattern(_programmableRunSandboxed).Suffixes.Add(ReflectionUtils.StaticMethod(typeof(PBProfilerPatch), nameof(SuffixProfilePb)));
            ctx.GetPattern(_programableRecompile).Suffixes.Add(ReflectionUtils.StaticMethod(typeof(PBProfilerPatch), nameof(PrefixRecompilePb)));

            Log.Info("Finished Patching!");
        }

        // ReSharper disable InconsistentNaming
        // ReSharper disable once SuggestBaseTypeForParameter
        private static void PrefixProfilePb(MyProgrammableBlock __instance, ref long __localTimingStart)
        {
            if (ProfilerConfig.takeIngameMeasurement)
                return;

            __localTimingStart = Stopwatch.GetTimestamp();
        }

        static FieldInfo runtimeField = typeof(MyProgrammableBlock).GetField("m_runtime", BindingFlags.Instance | BindingFlags.NonPublic);
        static PropertyInfo lastruntimeMS = typeof(MyProgrammableBlock)
            .GetNestedType("RuntimeInfo", BindingFlags.NonPublic)
            .GetProperty("LastRunTimeMs", BindingFlags.Public | BindingFlags.Instance);
        private static void SuffixProfilePb(MyProgrammableBlock __instance, ref long __localTimingStart)
        {
            double dtInSeconds;
            if (!ProfilerConfig.takeIngameMeasurement)
                dtInSeconds = (Stopwatch.GetTimestamp() - __localTimingStart) / (double)Stopwatch.Frequency;
            else
                dtInSeconds = (double)(lastruntimeMS.GetValue(runtimeField.GetValue(__instance)))/1000.0;

            PBData.AddOrUpdatePair(__instance, dtInSeconds);
        }

        private static void PrefixRecompilePb(MyProgrammableBlock __instance)
        {
            if (PBData.pbPair.TryGetValue(__instance.EntityId, out var pbData))
                pbData.SetRecompiled();
        }
    }
}
