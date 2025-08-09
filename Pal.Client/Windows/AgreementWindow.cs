using System;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using Pal.Client.Configuration;
using Pal.Client.Extensions;
using Pal.Client.Properties;

namespace Pal.Client.Windows
{
    internal sealed class AgreementWindow : Window, IDisposable, ILanguageChanged
    {
        private const string WindowId = "###PalPalaceAgreement";
        private readonly WindowSystem _windowSystem;
        private readonly ConfigurationManager _configurationManager;
        private readonly IPalacePalConfiguration _configuration;
        private int _choice;

        public AgreementWindow(
            WindowSystem windowSystem,
            ConfigurationManager configurationManager,
            IPalacePalConfiguration configuration)
            : base(WindowId)
        {
            _windowSystem = windowSystem;
            _configurationManager = configurationManager;
            _configuration = configuration;

            LanguageChanged();

            Flags = ImGuiWindowFlags.NoCollapse;
            Size = new Vector2(500, 500);
            SizeCondition = ImGuiCond.FirstUseEver;
            PositionCondition = ImGuiCond.FirstUseEver;
            Position = new Vector2(310, 310);

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(500, 500),
                MaximumSize = new Vector2(2000, 2000),
            };

            IsOpen = configuration.FirstUse;
            _windowSystem.AddWindow(this);
        }

        public void Dispose()
            => _windowSystem.RemoveWindow(this);

        public void LanguageChanged()
            => WindowName = $"{Localization.Palace_Pal}{WindowId}";

        public override void OnOpen()
        {
            _choice = -1;
        }

        public override void Draw()
        {
            ImGui.TextWrapped(Localization.Explanation_1);
            ImGui.TextWrapped(Localization.Explanation_2);

            ImGui.Spacing();

            ImGui.TextWrapped(Localization.Explanation_3);
            ImGui.TextWrapped(Localization.Explanation_4);

            PalImGui.RadioButtonWrapped(Localization.Config_UploadMyDiscoveries_ShowOtherTraps, ref _choice,
                (int)EMode.Online);
            PalImGui.RadioButtonWrapped(Localization.Config_NeverUploadDiscoveries_ShowMyTraps, ref _choice,
                (int)EMode.Offline);

            ImGui.Separator();

            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextWrapped(Localization.Agreement_Warning1);
            ImGui.TextWrapped(Localization.Agreement_Warning2);
            ImGui.TextWrapped(Localization.Agreement_Warning3);
            ImGui.PopStyleColor();

            ImGui.Separator();

            if (_choice == -1)
                ImGui.TextDisabled(Localization.Agreement_PickOneOption);
            ImGui.BeginDisabled(_choice == -1);
            if (ImGui.Button(Localization.Agreement_UsingThisOnMyOwnRisk))
            {
                _configuration.Mode = (EMode)_choice;
                _configuration.FirstUse = false;
                _configurationManager.Save(_configuration);

                IsOpen = false;
            }

            ImGui.EndDisabled();

            ImGui.Separator();

            if (ImGui.Button(Localization.Agreement_ViewPluginAndServerSourceCode))
                GenericHelpers.ShellStart("https://github.com/carvelli/PalPalace");
        }
    }
}
