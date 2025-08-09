using System;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace Pal.Client.Extensions
{
    internal static class PalImGui
    {
        /// <summary>
        /// None of the default BeginTabItem methods allow using flags without making the tab have a close button for some reason.
        /// </summary>
        internal static unsafe bool BeginTabItemWithFlags(string label, ImGuiTabItemFlags flags)
        {
            int labelLength = Encoding.UTF8.GetByteCount(label);
            byte* labelPtr = stackalloc byte[labelLength + 1];
            byte[] labelBytes = Encoding.UTF8.GetBytes(label);

            Marshal.Copy(labelBytes, 0, (IntPtr)labelPtr, labelLength);
            labelPtr[labelLength] = 0;

            return ImGuiNative.BeginTabItem(labelPtr, null, flags) != 0;
        }

        public static void RadioButtonWrapped(string label, ref int choice, int value)
        {
            ImGui.BeginGroup();
            ImGui.RadioButton($"##radio{value}", value == choice);
            ImGui.SameLine();
            ImGui.TextWrapped(label);
            ImGui.EndGroup();
            if (ImGui.IsItemClicked())
                choice = value;
        }
    }
}
