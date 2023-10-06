using System;
using System.Text;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices.Legacy;
using Microsoft.Extensions.Logging;
using Pal.Client.Floors;

namespace Pal.Client.DependencyInjection
{
    internal sealed unsafe class GameHooks : IDisposable
    {
        private readonly ILogger<GameHooks> _logger;
        private readonly IObjectTable _objectTable;
        private readonly TerritoryState _territoryState;
        private readonly FrameworkService _frameworkService;

#pragma warning disable CS0649
        private delegate nint ActorVfxCreateDelegate(char* a1, nint a2, nint a3, float a4, char a5, ushort a6, char a7);

        [Signature("40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8", DetourName = nameof(ActorVfxCreate))]
        private Hook<ActorVfxCreateDelegate> ActorVfxCreateHook { get; init; } = null!;
#pragma warning restore CS0649

        public GameHooks(ILogger<GameHooks> logger, IObjectTable objectTable, TerritoryState territoryState, FrameworkService frameworkService)
        {
            _logger = logger;
            _objectTable = objectTable;
            _territoryState = territoryState;
            _frameworkService = frameworkService;

            _logger.LogDebug("Initializing game hooks");
            SignatureHelper.Initialise(this);
            ActorVfxCreateHook.Enable();

            _logger.LogDebug("Game hooks initialized");
        }

        /// <summary>
        /// Even with a pomander of sight, the BattleChara's position for the trap remains at {0, 0, 0} until it is activated.
        /// Upon exploding, the trap's position is moved to the exact location that the pomander of sight would have revealed.
        ///
        /// That exact position appears to be used for VFX playing when you walk into it - even if you barely walk into the
        /// outer ring of an otter/luring/impeding/landmine trap, the VFX plays at the exact center and not at your character's
        /// location.
        ///
        /// Especially at higher floors, you're more likely to walk into an undiscovered trap compared to e.g. 51-60,
        /// and you probably don't want to/can't use sight on every floor - yet the trap location is still useful information.
        ///
        /// Some (but not all) chests also count as BattleChara named 'Trap', however the effect upon opening isn't played via 
        /// ActorVfxCreate even if they explode (but probably as a Vfx with static location, doesn't matter for here).
        /// 
        /// Landmines and luring traps also don't play a VFX attached to their BattleChara.
        /// 
        /// otter:      vfx/common/eff/dk05th_stdn0t.avfx <br/>
        /// toading:    vfx/common/eff/dk05th_stdn0t.avfx <br/>
        /// enfeebling: vfx/common/eff/dk05th_stdn0t.avfx <br/>
        /// landmine:   none <br/>
        /// luring:     none <br/>
        /// impeding:   vfx/common/eff/dk05ht_ipws0t.avfx (one of silence/pacification) <br/>
        /// impeding:   vfx/common/eff/dk05ht_slet0t.avfx (the other of silence/pacification) <br/>
        /// 
        /// It is of course annoying that, when testing, almost all traps are landmines.
        /// There's also vfx/common/eff/dk01gd_inv0h.avfx for e.g. impeding when you're invulnerable, but not sure if that
        /// has other trigger conditions.
        /// </summary>
        public nint ActorVfxCreate(char* a1, nint a2, nint a3, float a4, char a5, ushort a6, char a7)
        {
            try
            {
                if (_territoryState.IsInDeepDungeon())
                {
                    var vfxPath = MemoryHelper.ReadString(new nint(a1), Encoding.ASCII, 256);
                    var obj = _objectTable.CreateObjectReference(a2);

                    /*
                    if (Service.Configuration.BetaKey == "VFX")
                        _chat.PalPrint($"{vfxPath} on {obj}");
                    */

                    if (obj is BattleChara bc && (bc.NameId == /* potd */ 5042 || bc.NameId == /* hoh */ 7395))
                    {
                        if (vfxPath == "vfx/common/eff/dk05th_stdn0t.avfx" || vfxPath == "vfx/common/eff/dk05ht_ipws0t.avfx")
                        {
                            _logger.LogDebug("VFX '{Path}' playing at {Location}", vfxPath, obj.Position);
                            _frameworkService.NextUpdateObjects.Enqueue(obj.Address);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "VFX Create Hook failed");
            }
            return ActorVfxCreateHook.Original(a1, a2, a3, a4, a5, a6, a7);
        }

        public void Dispose()
        {
            _logger.LogDebug("Disposing game hooks");
            ActorVfxCreateHook.Dispose();
        }
    }
}
