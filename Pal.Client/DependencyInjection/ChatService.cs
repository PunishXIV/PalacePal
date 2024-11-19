using System;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using Pal.Client.Configuration;
using Pal.Client.Floors;

namespace Pal.Client.DependencyInjection
{
    internal sealed class ChatService : IDisposable
    {
        private readonly IChatGui _chatGui;
        private readonly TerritoryState _territoryState;
        private readonly IPalacePalConfiguration _configuration;
        private readonly IDataManager _dataManager;
        private readonly LocalizedChatMessages _localizedChatMessages;

        public ChatService(IChatGui chatGui, TerritoryState territoryState, IPalacePalConfiguration configuration,
            IDataManager dataManager)
        {
            _chatGui = chatGui;
            _territoryState = territoryState;
            _configuration = configuration;
            _dataManager = dataManager;

            _localizedChatMessages = LoadLanguageStrings();

            _chatGui.ChatMessage += OnChatMessage;
        }

        public void Dispose()
            => _chatGui.ChatMessage -= OnChatMessage;

        private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString seMessage,
            ref bool isHandled)
        {
            if (_configuration.FirstUse)
                return;

            if (type != (XivChatType)2105)
                return;

            string message = seMessage.ToString();
            //PluginLog.Debug($"Message: {message}, floorchanged: {_localizedChatMessages.FloorChanged.ToString()}");
            if (_localizedChatMessages.FloorChanged.IsMatch(message))
            {
                //PluginLog.Debug($"Floor changed");
                _territoryState.PomanderOfSight = PomanderState.Inactive;

                if (_territoryState.PomanderOfIntuition == PomanderState.FoundOnCurrentFloor)
                    _territoryState.PomanderOfIntuition = PomanderState.Inactive;
            }
            else if (message.EndsWith(_localizedChatMessages.MapRevealed))
            {
                _territoryState.PomanderOfSight = PomanderState.Active;
            }
            else if (message.EndsWith(_localizedChatMessages.AllTrapsRemoved))
            {
                _territoryState.PomanderOfSight = PomanderState.PomanderOfSafetyUsed;
            }
            else if (message.EndsWith(_localizedChatMessages.HoardNotOnCurrentFloor) ||
                     message.EndsWith(_localizedChatMessages.HoardOnCurrentFloor))
            {
                // There is no functional difference between these - if you don't open the marked coffer,
                // going to higher floors will keep the pomander active.
                _territoryState.PomanderOfIntuition = PomanderState.Active;
            }
            else if (message.EndsWith(_localizedChatMessages.HoardCofferOpened))
            {
                _territoryState.PomanderOfIntuition = PomanderState.FoundOnCurrentFloor;
            }
        }

        private LocalizedChatMessages LoadLanguageStrings()
        {
            return new LocalizedChatMessages
            {
                MapRevealed = GetLocalizedString(7256),
                AllTrapsRemoved = GetLocalizedString(7255),
                HoardOnCurrentFloor = GetLocalizedString(7272),
                HoardNotOnCurrentFloor = GetLocalizedString(7273),
                HoardCofferOpened = GetLocalizedString(7274),
                FloorChanged =
                    new Regex(GetFloorChangedRegex()),
            };
        }

        private string GetFloorChangedRegex()
        {
            //7270	57	33	0	False	Floor <Value>IntegerParameter(1)</Value>
            //7270	57	33	0	False	地下<Value>IntegerParameter(1)</Value>階
            //7270	57	33	0	False	Ebene <Value>IntegerParameter(1)</Value> betreten!
            //7270	57	33	0	False	Sous-sol <Value>IntegerParameter(1)</Value>
            //7270	57	33	0	False	地下<Value>IntegerParameter(1)</Value>层
            if (Svc.ClientState.ClientLanguage == ClientLanguage.English) return @"^Floor (\d+)";
            if (Svc.ClientState.ClientLanguage == ClientLanguage.Japanese) return @"^地下(\d+)階";
            if (Svc.ClientState.ClientLanguage == ClientLanguage.German) return @"^Ebene (\d+) betreten!";
            if (Svc.ClientState.ClientLanguage == ClientLanguage.French) return @"^Sous-sol (\d+)";
            if (Svc.ClientState.ClientLanguage == (ClientLanguage)4) return @"^地下(\d+)层";
            throw new Exception("Invalid client language: " + Svc.ClientState.ClientLanguage);

        }

        private string GetLocalizedString(uint id)
        {
            return _dataManager.GetExcelSheet<LogMessage>().GetRow(id).Text.ToString() ?? "Unknown";
        }

        private string GetLocalizedExtractedString(uint id)
        {
            return _dataManager.GetExcelSheet<LogMessage>().GetRow(id).Text.ToDalamudString().ExtractText() ?? "Unknown";
        }

        private sealed class LocalizedChatMessages
        {
            public string MapRevealed { get; init; } = "???"; //"The map for this floor has been revealed!";
            public string AllTrapsRemoved { get; init; } = "???"; // "All the traps on this floor have disappeared!";
            public string HoardOnCurrentFloor { get; init; } = "???"; // "You sense the Accursed Hoard calling you...";

            public string HoardNotOnCurrentFloor { get; init; } =
                "???"; // "You do not sense the call of the Accursed Hoard on this floor...";

            public string HoardCofferOpened { get; init; } = "???"; // "You discover a piece of the Accursed Hoard!";

            public Regex FloorChanged { get; init; } =
                new(@"This isn't a game message, but will be replaced"); // new Regex(@"^Floor (\d+)$");
        }
    }
}
