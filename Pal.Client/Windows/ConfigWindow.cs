using Account;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using ECommons;
using Google.Protobuf;
using ImGuiNET;
using Pal.Client.Net;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pal.Client.Extensions;
using Pal.Client.Properties;
using Pal.Client.Configuration;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.Floors;

namespace Pal.Client.Windows
{
    internal sealed class ConfigWindow : Window, ILanguageChanged, IDisposable
    {
        private const string WindowId = "###PalPalaceConfig";

        private readonly ILogger<ConfigWindow> _logger;
        private readonly WindowSystem _windowSystem;
        private readonly ConfigurationManager _configurationManager;
        private readonly IPalacePalConfiguration _configuration;
        private readonly RenderAdapter _renderAdapter;
        private readonly TerritoryState _territoryState;
        private readonly FrameworkService _frameworkService;
        private readonly FloorService _floorService;
        private readonly DebugState _debugState;
        private readonly Chat _chat;
        private readonly RemoteApi _remoteApi;
        private readonly ImportService _importService;

        private int _mode;
        private int _renderer;
        private ConfigurableMarker _trapConfig = new();
        private ConfigurableMarker _hoardConfig = new();
        private ConfigurableMarker _silverConfig = new();
        private ConfigurableMarker _goldConfig = new();

        private string? _connectionText;
        private bool _switchToCommunityTab;
        private string _openImportPath = string.Empty;
        private string _saveExportPath = string.Empty;
        private string? _openImportDialogStartPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private string? _saveExportDialogStartPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private readonly FileDialogManager _importDialog;
        private readonly FileDialogManager _exportDialog;
        private ImportHistory? _lastImport;

        private CancellationTokenSource? _testConnectionCts;
        private CancellationTokenSource? _lastImportCts;

        public ConfigWindow(
            ILogger<ConfigWindow> logger,
            WindowSystem windowSystem,
            ConfigurationManager configurationManager,
            IPalacePalConfiguration configuration,
            RenderAdapter renderAdapter,
            TerritoryState territoryState,
            FrameworkService frameworkService,
            FloorService floorService,
            DebugState debugState,
            Chat chat,
            RemoteApi remoteApi,
            ImportService importService)
            : base(WindowId)
        {
            _logger = logger;
            _windowSystem = windowSystem;
            _configurationManager = configurationManager;
            _configuration = configuration;
            _renderAdapter = renderAdapter;
            _territoryState = territoryState;
            _frameworkService = frameworkService;
            _floorService = floorService;
            _debugState = debugState;
            _chat = chat;
            _remoteApi = remoteApi;
            _importService = importService;

            LanguageChanged();

            Size = new Vector2(500, 400);
            SizeCondition = ImGuiCond.FirstUseEver;
            Position = new Vector2(300, 300);
            PositionCondition = ImGuiCond.FirstUseEver;

            _importDialog = new FileDialogManager
            { AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking };
            _exportDialog = new FileDialogManager
            { AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking };

            _windowSystem.AddWindow(this);
        }

        public void Dispose()
        {
            _windowSystem.RemoveWindow(this);
            _lastImportCts?.Cancel();
            _testConnectionCts?.Cancel();
        }

        public void LanguageChanged()
        {
            var version = typeof(Plugin).Assembly.GetName().Version!.ToString(2);
            WindowName = $"{Localization.Palace_Pal} v{version}{WindowId}";
        }

        public override void OnOpen()
        {
            _mode = (int)_configuration.Mode;
            _renderer = (int)_configuration.Renderer.SelectedRenderer;
            _trapConfig = new ConfigurableMarker(_configuration.DeepDungeons.Traps);
            _hoardConfig = new ConfigurableMarker(_configuration.DeepDungeons.HoardCoffers);
            _silverConfig = new ConfigurableMarker(_configuration.DeepDungeons.SilverCoffers);
            _goldConfig = new ConfigurableMarker(_configuration.DeepDungeons.GoldCoffers);
            _connectionText = null;

            UpdateLastImport();
        }

        public override void OnClose()
        {
            _importDialog.Reset();
            _exportDialog.Reset();
            _testConnectionCts?.Cancel();
            _testConnectionCts = null;
        }

        public override void Draw()
        {
            bool save = false;
            bool saveAndClose = false;
            if (ImGui.BeginTabBar("PalTabs"))
            {
                DrawDeepDungeonItemsTab(ref save, ref saveAndClose);
                DrawCommunityTab(ref saveAndClose);
                DrawImportTab();
                DrawExportTab();
                DrawRenderTab(ref save, ref saveAndClose);
                DrawDebugTab();

                ImGui.EndTabBar();
            }

            _importDialog.Draw();

            if (save || saveAndClose)
            {
                _configuration.Mode = (EMode)_mode;
                _configuration.Renderer.SelectedRenderer = (ERenderer)_renderer;
                _configuration.DeepDungeons.Traps = _trapConfig.Build();
                _configuration.DeepDungeons.HoardCoffers = _hoardConfig.Build();
                _configuration.DeepDungeons.SilverCoffers = _silverConfig.Build();
                _configuration.DeepDungeons.GoldCoffers = _goldConfig.Build();

                _configurationManager.Save(_configuration);

                if (saveAndClose)
                    IsOpen = false;
            }
        }

        private void DrawDeepDungeonItemsTab(ref bool save, ref bool saveAndClose)
        {
            if (ImGui.BeginTabItem($"{Localization.ConfigTab_DeepDungeons}###TabDeepDungeons"))
            {
                ImGui.PushID("trap");
                ImGui.Checkbox(Localization.Config_Traps_Show, ref _trapConfig.Show);
                ImGui.Indent();
                ImGui.BeginDisabled(!_trapConfig.Show);
                ImGui.Spacing();
                ImGui.ColorEdit4(Localization.Config_Traps_Color, ref _trapConfig.Color, ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox(Localization.Config_Traps_HideImpossible, ref _trapConfig.OnlyVisibleAfterPomander);
                ImGui.SameLine();
                ImGuiComponents.HelpMarker(Localization.Config_Traps_HideImpossible_ToolTip);
                ImGui.EndDisabled();
                ImGui.Unindent();
                ImGui.PopID();

                ImGui.Separator();

                ImGui.PushID("hoard");
                ImGui.Checkbox(Localization.Config_HoardCoffers_Show, ref _hoardConfig.Show);
                ImGui.Indent();
                ImGui.BeginDisabled(!_hoardConfig.Show);
                ImGui.Spacing();
                ImGui.ColorEdit4(Localization.Config_HoardCoffers_Color, ref _hoardConfig.Color,
                    ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox(Localization.Config_HoardCoffers_HideImpossible,
                    ref _hoardConfig.OnlyVisibleAfterPomander);
                ImGui.SameLine();
                ImGuiComponents.HelpMarker(Localization.Config_HoardCoffers_HideImpossible_ToolTip);
                ImGui.EndDisabled();
                ImGui.Unindent();
                ImGui.PopID();

                ImGui.Separator();

                ImGui.PushID("silver");
                ImGui.Checkbox(Localization.Config_SilverCoffer_Show, ref _silverConfig.Show);
                ImGuiComponents.HelpMarker(Localization.Config_SilverCoffers_ToolTip);
                ImGui.Indent();
                ImGui.BeginDisabled(!_silverConfig.Show);
                ImGui.Spacing();
                ImGui.ColorEdit4(Localization.Config_SilverCoffer_Color, ref _silverConfig.Color,
                    ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox(Localization.Config_SilverCoffer_Filled, ref _silverConfig.Fill);
                ImGui.EndDisabled();
                ImGui.Unindent();
                ImGui.PopID();

                ImGui.Separator();

                ImGui.PushID("gold");
                ImGui.Checkbox(Localization.Config_GoldCoffer_Show, ref _goldConfig.Show);
                ImGuiComponents.HelpMarker(Localization.Config_GoldCoffers_ToolTip);
                ImGui.Indent();
                ImGui.BeginDisabled(!_goldConfig.Show);
                ImGui.Spacing();
                ImGui.ColorEdit4(Localization.Config_GoldCoffer_Color, ref _goldConfig.Color,
                    ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox(Localization.Config_GoldCoffer_Filled, ref _goldConfig.Fill);
                ImGui.EndDisabled();
                ImGui.Unindent();
                ImGui.PopID();

                ImGui.Separator();

                save = ImGui.Button(Localization.Save);
                ImGui.SameLine();
                saveAndClose = ImGui.Button(Localization.SaveAndClose);

                ImGui.EndTabItem();
            }
        }

        private void DrawCommunityTab(ref bool saveAndClose)
        {
            if (PalImGui.BeginTabItemWithFlags($"{Localization.ConfigTab_Community}###TabCommunity",
                    _switchToCommunityTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                _switchToCommunityTab = false;

                ImGui.TextWrapped(Localization.Explanation_3);
                ImGui.TextWrapped(Localization.Explanation_4);

                PalImGui.RadioButtonWrapped(Localization.Config_UploadMyDiscoveries_ShowOtherTraps, ref _mode,
                    (int)EMode.Online);
                PalImGui.RadioButtonWrapped(Localization.Config_NeverUploadDiscoveries_ShowMyTraps, ref _mode,
                    (int)EMode.Offline);
                saveAndClose = ImGui.Button(Localization.SaveAndClose);

                ImGui.Separator();

                ImGui.BeginDisabled(_configuration.Mode != EMode.Online);
                if (ImGui.Button(Localization.Config_TestConnection))
                    TestConnection();

                if (_connectionText != null)
                    ImGui.Text(_connectionText);

                ImGui.EndDisabled();
                ImGui.EndTabItem();
            }
        }

        private void DrawImportTab()
        {
            if (ImGui.BeginTabItem($"{Localization.ConfigTab_Import}###TabImport"))
            {
                ImGui.TextWrapped(Localization.Config_ImportExplanation1);
                ImGui.TextWrapped(Localization.Config_ImportExplanation2);
                ImGui.TextWrapped(Localization.Config_ImportExplanation3);
                ImGui.Separator();
                ImGui.TextWrapped(string.Format(Localization.Config_ImportDownloadLocation,
                    "https://github.com/carvelli/PalacePal/releases/"));
                if (ImGui.Button(Localization.Config_Import_VisitGitHub))
                    GenericHelpers.ShellStart("https://github.com/carvelli/PalacePal/releases/latest");
                ImGui.Separator();
                ImGui.Text(Localization.Config_SelectImportFile);
                ImGui.SameLine();
                ImGui.InputTextWithHint("", Localization.Config_SelectImportFile_Hint, ref _openImportPath, 260);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    _importDialog.OpenFileDialog(Localization.Palace_Pal, $"{Localization.Palace_Pal} (*.pal) {{.pal}}",
                        (success, paths) =>
                        {
                            if (success && paths.Count == 1)
                            {
                                _openImportPath = paths.First();
                            }
                        }, selectionCountMax: 1, startPath: _openImportDialogStartPath, isModal: false);
                    _openImportDialogStartPath =
                        null; // only use this once, FileDialogManager will save path between calls
                }

                ImGui.BeginDisabled(string.IsNullOrEmpty(_openImportPath) || !File.Exists(_openImportPath) || _floorService.IsImportRunning);
                if (ImGui.Button(Localization.Config_StartImport))
                    DoImport(_openImportPath);
                ImGui.EndDisabled();

                ImportHistory? importHistory = _lastImport;
                if (importHistory != null)
                {
                    ImGui.Separator();
                    ImGui.TextWrapped(string.Format(Localization.Config_UndoImportExplanation1,
                        importHistory.ImportedAt.ToLocalTime(),
                        importHistory.RemoteUrl,
                        importHistory.ExportedAt.ToUniversalTime()));
                    ImGui.TextWrapped(Localization.Config_UndoImportExplanation2);

                    ImGui.BeginDisabled(_floorService.IsImportRunning);
                    if (ImGui.Button(Localization.Config_UndoImport))
                        UndoImport(importHistory.Id);
                    ImGui.EndDisabled();
                }

                ImGui.EndTabItem();
            }
        }

        private void DrawExportTab()
        {
            if (_configuration.HasRoleOnCurrentServer(RemoteApi.RemoteUrl, "export:run") &&
                ImGui.BeginTabItem($"{Localization.ConfigTab_Export}###TabExport"))
            {
                string todaysFileName = $"export-{DateTime.Today:yyyy-MM-dd}.pal";
                if (string.IsNullOrEmpty(_saveExportPath) && !string.IsNullOrEmpty(_saveExportDialogStartPath))
                    _saveExportPath = Path.Join(_saveExportDialogStartPath, todaysFileName);

                ImGui.TextWrapped(string.Format(Localization.Config_ExportSource, RemoteApi.RemoteUrl));
                ImGui.Text(Localization.Config_Export_SaveAs);
                ImGui.SameLine();
                ImGui.InputTextWithHint("", Localization.Config_SelectImportFile_Hint, ref _saveExportPath, 260);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    _importDialog.SaveFileDialog(Localization.Palace_Pal, $"{Localization.Palace_Pal} (*.pal) {{.pal}}",
                        todaysFileName, "pal", (success, path) =>
                        {
                            if (success && !string.IsNullOrEmpty(path))
                            {
                                _saveExportPath = path;
                            }
                        }, startPath: _saveExportDialogStartPath, isModal: false);
                    _saveExportDialogStartPath =
                        null; // only use this once, FileDialogManager will save path between calls
                }

                ImGui.BeginDisabled(string.IsNullOrEmpty(_saveExportPath) || File.Exists(_saveExportPath));
                if (ImGui.Button(Localization.Config_StartExport))
                    DoExport(_saveExportPath);
                ImGui.EndDisabled();

                ImGui.EndTabItem();
            }
        }

        private void DrawRenderTab(ref bool save, ref bool saveAndClose)
        {
            if (ImGui.BeginTabItem($"{Localization.ConfigTab_Renderer}###TabRenderer"))
            {
                ImGui.Text(Localization.Config_SelectRenderBackend);
                ImGui.RadioButton(
                    $"{Localization.Config_Renderer_Splatoon} ({Localization.Config_Renderer_Splatoon_Hint})",
                    ref _renderer, (int)ERenderer.Splatoon);
                ImGui.RadioButton($"{Localization.Config_Renderer_Simple} ({Localization.Config_Renderer_Simple_Hint})",
                    ref _renderer, (int)ERenderer.Simple);

                ImGui.Separator();

                save = ImGui.Button(Localization.Save);
                ImGui.SameLine();
                saveAndClose = ImGui.Button(Localization.SaveAndClose);

                ImGui.Separator();
                if (ImGui.Button(Localization.Config_Splatoon_DrawCircles))
                    _renderAdapter.DrawDebugItems(ImGui.ColorConvertFloat4ToU32(_trapConfig.Color),
                        ImGui.ColorConvertFloat4ToU32(_hoardConfig.Color));

                ImGui.EndTabItem();
            }
        }

        private void DrawDebugTab()
        {
            if (ImGui.BeginTabItem($"{Localization.ConfigTab_Debug}###TabDebug"))
            {
                if (_territoryState.IsInDeepDungeon())
                {
                    MemoryTerritory? memoryTerritory = _floorService.GetTerritoryIfReady(_territoryState.LastTerritory);
                    ImGui.Text($"You are in a deep dungeon, territory type {_territoryState.LastTerritory}.");
                    ImGui.Text($"Sync State = {memoryTerritory?.SyncState.ToString() ?? "Unknown"}");
                    ImGui.Text($"{_debugState.DebugMessage}");

                    ImGui.Indent();
                    if (memoryTerritory != null)
                    {
                        if (_trapConfig.Show)
                        {
                            int traps = memoryTerritory.Locations.Count(x => x.Type == MemoryLocation.EType.Trap);
                            ImGui.Text($"{traps} known trap{(traps == 1 ? "" : "s")}");
                        }

                        if (_hoardConfig.Show)
                        {
                            int hoardCoffers =
                                memoryTerritory.Locations.Count(x => x.Type == MemoryLocation.EType.Hoard);
                            ImGui.Text($"{hoardCoffers} known hoard coffer{(hoardCoffers == 1 ? "" : "s")}");
                        }

                        if (_silverConfig.Show)
                        {
                            int silverCoffers =
                                _floorService.EphemeralLocations.Count(x =>
                                    x.Type == MemoryLocation.EType.SilverCoffer);
                            ImGui.Text(
                                $"{silverCoffers} silver coffer{(silverCoffers == 1 ? "" : "s")} visible on current floor");
                        }

                        if (_goldConfig.Show)
                        {
                            int goldCoffers =
                                _floorService.EphemeralLocations.Count(x =>
                                    x.Type == MemoryLocation.EType.GoldCoffer);
                            ImGui.Text(
                                $"{goldCoffers} silver coffer{(goldCoffers == 1 ? "" : "s")} visible on current floor");
                        }

                        ImGui.Text($"Pomander of Sight: {_territoryState.PomanderOfSight}");
                        ImGui.Text($"Pomander of Intuition: {_territoryState.PomanderOfIntuition}");
                    }
                    else
                        ImGui.Text("Could not query current trap/coffer count.");

                    ImGui.Unindent();
                    ImGui.TextWrapped(
                        "Traps and coffers may not be discovered even after using a pomander if they're far away (around 1,5-2 rooms).");
                }
                else
                    ImGui.Text(Localization.Config_Debug_NotInADeepDungeon);

                ImGui.EndTabItem();
            }
        }

        internal void TestConnection()
        {
            Task.Run(async () =>
            {
                _connectionText = Localization.Config_TestConnection_Connecting;
                _switchToCommunityTab = true;

                _testConnectionCts?.Cancel();

                CancellationTokenSource cts = new();
                cts.CancelAfter(TimeSpan.FromSeconds(60));
                _testConnectionCts = cts;

                try
                {
                    _connectionText = await _remoteApi.VerifyConnection(cts.Token);
                }
                catch (Exception e)
                {
                    if (cts == _testConnectionCts)
                    {
                        _logger.LogError(e, "Could not establish remote connection");
                        _connectionText = e.ToString();
                    }
                    else
                        _logger.LogWarning(e,
                            "Could not establish a remote connection, but user also clicked 'test connection' again so not updating UI");
                }
            });
        }

        private void DoImport(string sourcePath)
        {
            _frameworkService.EarlyEventQueue.Enqueue(new QueuedImport(sourcePath));
        }

        private void UndoImport(Guid importId)
        {
            _frameworkService.EarlyEventQueue.Enqueue(new QueuedUndoImport(importId));
        }

        internal void UpdateLastImport()
        {
            _lastImportCts?.Cancel();
            CancellationTokenSource cts = new CancellationTokenSource();
            _lastImportCts = cts;

            Task.Run(async () =>
            {
                try
                {
                    _lastImport = await _importService.FindLast(cts.Token);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to fetch last import");
                }
            }, cts.Token);
        }

        private void DoExport(string destinationPath)
        {
            Task.Run(async () =>
            {
                try
                {
                    (bool success, ExportRoot export) = await _remoteApi.DoExport();
                    if (success)
                    {
                        await using var output = File.Create(destinationPath);
                        export.WriteTo(output);

                        _chat.Message($"Export saved as {destinationPath}.");
                    }
                    else
                    {
                        _chat.Error("Export failed due to server error.");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Export failed");
                    _chat.Error($"Export failed: {e}");
                }
            });
        }

        private sealed class ConfigurableMarker
        {
            public bool Show;
            public Vector4 Color;
            public bool OnlyVisibleAfterPomander;
            public bool Fill;

            public ConfigurableMarker()
            {
            }

            public ConfigurableMarker(MarkerConfiguration config)
            {
                Show = config.Show;
                Color = ImGui.ColorConvertU32ToFloat4(config.Color);
                OnlyVisibleAfterPomander = config.OnlyVisibleAfterPomander;
                Fill = config.Fill;
            }

            public MarkerConfiguration Build()
            {
                return new MarkerConfiguration
                {
                    Show = Show,
                    Color = ImGui.ColorConvertFloat4ToU32(Color),
                    OnlyVisibleAfterPomander = OnlyVisibleAfterPomander,
                    Fill = Fill
                };
            }
        }
    }
}
