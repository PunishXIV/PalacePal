using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Pal.Client.Properties;
using ECommons.DalamudServices.Legacy;

namespace Pal.Client.DependencyInjection
{
    internal sealed class Chat
    {
        private readonly IChatGui _chatGui;

        public Chat(IChatGui chatGui)
        {
            _chatGui = chatGui;
        }

        public void Error(string e)
        {
            _chatGui.PrintChat(new XivChatEntry
            {
                Message = new SeStringBuilder()
                    .AddUiForeground($"[{Localization.Palace_Pal}] ", 16)
                    .AddText(e).Build(),
                Type = XivChatType.Urgent
            });
        }

        public void Message(string message)
        {
            _chatGui.Print(new SeStringBuilder()
                .AddUiForeground($"[{Localization.Palace_Pal}] ", 57)
                .AddText(message).Build());
        }

        public void UnformattedMessage(string message)
            => _chatGui.Print(message);
    }
}
