using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Pal.Common;
using Palace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pal.Client.Properties;

namespace Pal.Client.Windows
{
    internal sealed class StatisticsWindow : Window, IDisposable, ILanguageChanged
    {
        private const string WindowId = "###PalacePalStats";
        private readonly WindowSystem _windowSystem;
        private readonly SortedDictionary<ETerritoryType, TerritoryStatistics> _territoryStatistics = new();

        public StatisticsWindow(WindowSystem windowSystem)
            : base(WindowId)
        {
            _windowSystem = windowSystem;

            LanguageChanged();

            Size = new Vector2(500, 500);
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.AlwaysAutoResize;

            foreach (ETerritoryType territory in typeof(ETerritoryType).GetEnumValues())
            {
                _territoryStatistics[territory] = new TerritoryStatistics(territory.ToString());
            }

            _windowSystem.AddWindow(this);
        }

        public void Dispose()
            => _windowSystem.RemoveWindow(this);

        public void LanguageChanged()
            => WindowName = $"{Localization.Palace_Pal} - {Localization.Statistics}{WindowId}";

        public override void Draw()
        {
            if (ImGui.BeginTabBar("Tabs"))
            {
                DrawDungeonStats("Palace of the Dead", Localization.PalaceOfTheDead, ETerritoryType.Palace_1_10,
                    ETerritoryType.Palace_191_200);
                DrawDungeonStats("Heaven on High", Localization.HeavenOnHigh, ETerritoryType.HeavenOnHigh_1_10,
                    ETerritoryType.HeavenOnHigh_91_100);
                DrawDungeonStats("Eureka Orthos", Localization.EurekaOrthos, ETerritoryType.EurekaOrthos_1_10,
                    ETerritoryType.EurekaOrthos_91_100);
            }
        }

        private void DrawDungeonStats(string id, string name, ETerritoryType minTerritory, ETerritoryType maxTerritory)
        {
            if (ImGui.BeginTabItem($"{name}###{id}"))
            {
                if (ImGui.BeginTable($"TrapHoardStatistics{id}", 4,
                        ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn(Localization.Statistics_TerritoryId);
                    ImGui.TableSetupColumn(Localization.Statistics_InstanceName);
                    ImGui.TableSetupColumn(Localization.Statistics_Traps);
                    ImGui.TableSetupColumn(Localization.Statistics_HoardCoffers);
                    ImGui.TableHeadersRow();

                    foreach (var (territoryType, stats) in _territoryStatistics
                                 .Where(x => x.Key >= minTerritory && x.Key <= maxTerritory)
                                 .OrderBy(x => x.Key.GetOrder() ?? (int)x.Key))
                    {
                        ImGui.TableNextRow();
                        if (ImGui.TableNextColumn())
                            ImGui.Text($"{(uint)territoryType}");

                        if (ImGui.TableNextColumn())
                            ImGui.Text(stats.TerritoryName);

                        if (ImGui.TableNextColumn())
                            ImGui.Text(stats.TrapCount?.ToString() ?? "-");

                        if (ImGui.TableNextColumn())
                            ImGui.Text(stats.HoardCofferCount?.ToString() ?? "-");
                    }

                    ImGui.EndTable();
                }

                ImGui.EndTabItem();
            }
        }

        internal void SetFloorData(IEnumerable<FloorStatistics> floorStatistics)
        {
            foreach (var territoryStatistics in _territoryStatistics.Values)
            {
                territoryStatistics.TrapCount = null;
                territoryStatistics.HoardCofferCount = null;
            }

            foreach (var floor in floorStatistics)
            {
                if (_territoryStatistics.TryGetValue((ETerritoryType)floor.TerritoryType,
                        out TerritoryStatistics? territoryStatistics))
                {
                    territoryStatistics.TrapCount = floor.TrapCount;
                    territoryStatistics.HoardCofferCount = floor.HoardCount;
                }
            }
        }

        private sealed class TerritoryStatistics
        {
            public string TerritoryName { get; }
            public uint? TrapCount { get; set; }
            public uint? HoardCofferCount { get; set; }

            public TerritoryStatistics(string territoryName)
            {
                TerritoryName = territoryName;
            }
        }
    }
}
