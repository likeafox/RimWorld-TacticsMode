using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if HARMONY_1_2
using Harmony;
#elif HARMONY_2
using HarmonyLib;
#endif
using Verse;
using Pawn_JobTracker = Verse.AI.Pawn_JobTracker;
using System.Reflection;
using RimWorld;

namespace TacticsMode
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
#if HARMONY_1_2
            var harmony = HarmonyInstance.Create("likeafox.rimworld.tacticsmode");
#elif HARMONY_2
            var harmony = new Harmony("likeafox.rimworld.tacticsmode");
#endif
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }
    }

    public class TacticsMode : GameComponent
    {
        private Dictionary<Pawn, bool> _inTacticsMode = new Dictionary<Pawn, bool>();
        private Dictionary<Pawn, int> _lastActionTick = new Dictionary<Pawn, int>();
        private static TacticsMode _instance;
        private List<Pawn> scribe_inTacticsMode_keys = new List<Pawn>();
        private List<bool> scribe_inTacticsMode_values = new List<bool>();

        private static TacticsMode instance { get {
                if (_instance == null)
                    throw new NullReferenceException("TacticsMode is not instantiated yet.");
                return _instance;
            } }

        public TacticsMode(Game game) : this() { }
        public TacticsMode() { _instance = this; }

        public static bool LastActionExpired(Pawn p)
        {
            int lastActionTick;
            instance._lastActionTick.TryGetValue(p, out lastActionTick);
            return Find.TickManager.TicksGame > lastActionTick + 90;
        }

        public static bool CanEverBeInTacticsMode(Pawn p)
        {
            return p.IsColonist;
        }

        public static bool HasTacticsModeToggledOn(Pawn p)
        {
            try
            {
                return instance._inTacticsMode[p];
            }
            catch { }
            return false;
        }

        public static void SetTacticsMode(Pawn p, bool v)
        {
            if (!CanEverBeInTacticsMode(p))
                Log.Warning("Tactical mode set on non-player-controlled pawn -- this will have no effect.");
            instance._inTacticsMode[p] = v;
        }

        public static bool IsInTacticsMode(Pawn p)
        {
            return CanEverBeInTacticsMode(p) && p.IsColonistPlayerControlled && HasTacticsModeToggledOn(p);
        }
        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                Clean();
            Scribe_Collections.Look(ref _inTacticsMode, "tacticalMode", LookMode.Reference, LookMode.Value,
                ref scribe_inTacticsMode_keys, ref scribe_inTacticsMode_values);
        }
        public void Clean()
        {
            var destroyed_pawns = new List<Pawn>(_inTacticsMode.Keys.Where(p => p.Destroyed));
            foreach (var p in destroyed_pawns)
                _inTacticsMode.Remove(p);
        }

        public static void TryDoTacticalAction(Pawn p)
        {
            if (IsInTacticsMode(p))
            {
                CameraJumper.TryJumpAndSelect(p);
                Find.TickManager.Pause();
                instance._lastActionTick[p] = Find.TickManager.TicksGame;
            }
        }
    }

    public static class JobTypeWhitelist
    {
        private static string[] _job_type_whitelist_names = {
            "Arrest",
            "BuildRoof",
            "Capture",
            "CarryToCryptosleepCasket",
            "Clean",
            "CutPlant",
            "CutPlantDesignated",
            "Deconstruct",
            "DeliverFood",
            "DoBill",
            "DropEquipment",
            "EnterCryptosleepCasket",
            "EnterTransporter",
            "Equip",
            "EscortPrisonerToBed",
            "ExtinguishSelf",
            "FeedPatient",
            "FillFermentingBarrel",
            "FinishFrame",
            "FixBrokenDownBuilding",
            "Flick",
            "Goto",
            "Harvest",
            "HarvestDesignated",
            "HaulCorpseToPublicPlace",
            "HaulToCell",
            "HaulToContainer",
            "HaulToTransporter",
            "Hunt",
            "Ingest",
            "Maintain",
            "ManTurret",
            "Mine",
            "Open",
            "OperateDeepDrill",
            "PlaceNoCostFrame",
            "OperateScanner",
            "PrisonerExecution",
            "RearmTurret",
            "RearmTurretAtomic",
            "Refuel",
            "RefuelAtomic",
            "ReleasePrisoner",
            "RemoveApparel",
            "RemoveFloor",
            "RemoveRoof",
            "Repair",
            "Rescue",
            "Shear",
            "Slaughter",
            "SmoothFloor",
            "SmoothWall",
            "Sow",
            "Strip",
            "TakeBeerOutOfFermentingBarrel",
            "TakeInventory",
            "TakeToBedToOperate",
            "TakeWoundedPrisonerToBed",
            "Tame",
            "TendPatient",
            "TradeWithPawn",
            "Train",
#if !POST_RW_1_4
            "TriggerFirefoamPopper",
#endif
            "Uninstall",
#if !POST_RW_1_5
            "UseArtifact",
#endif
            "UseNeurotrainer",
            "VisitSickPawn",
            "Wear"
        };
        private static JobDef getJobDef(string name)
        {
            JobDef def = null;
            try
            {
                def = (JobDef)typeof(RimWorld.JobDefOf).GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
                if (def == null) throw new Exception();
            }
            catch
            {
                Log.Error("TacticsMode Can't find JobDefOf for " + name);
            }
            return def;
        }
        private static HashSet<JobDef> _jobTypeWhitelist = null;
        public static HashSet<JobDef> hashset
        {
            get
            {
                if (_jobTypeWhitelist == null)
                    _jobTypeWhitelist = new HashSet<JobDef>(_job_type_whitelist_names.Select(getJobDef).Where(d => d != null));
                return _jobTypeWhitelist;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
    class Pawn_JobTracker_CleanupCurrentJob_Patch
    {
        static void Prefix(Pawn_JobTracker __instance)
        {
            Pawn_JobTracker tracker = __instance;
            Pawn p = (Pawn)typeof(Pawn_JobTracker).InvokeMember("pawn",
                BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic,
                null, tracker, null);
            if (p == null)
                throw new Exception("Pawn_JobTracker.pawn is null");
            if (tracker.IsCurrentJobPlayerInterruptible()
                && tracker.jobQueue.Count == 0
                && (tracker.curJob == null
                    || JobTypeWhitelist.hashset.Contains(tracker.curJob.def)
                    || TacticsMode.LastActionExpired(p)))
            {
                TacticsMode.TryDoTacticalAction(p);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    class Pawn_GetGizmos_Patch
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
        {
            foreach (Gizmo gizmo in gizmos)
                yield return gizmo;
            var tactical = new Command_Toggle
            {
                icon = ContentFinder<UnityEngine.Texture2D>.Get("Buttons/Pawn", true),
                defaultLabel = "Tactics",
                defaultDesc = "Toggle tactics mode for this colonist. The game will pause and center on the colonist when they finish a job, allowing you to micromanage them.",
                isActive = (() => TacticsMode.HasTacticsModeToggledOn(__instance)),
                toggleAction = delegate {
                    TacticsMode.SetTacticsMode(__instance, !TacticsMode.HasTacticsModeToggledOn(__instance));
                },
                hotKey = null
            };
            if (TacticsMode.CanEverBeInTacticsMode(__instance))
                yield return tactical;
        }
    }
}
