using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Pal.Common;

namespace Pal.Client.Floors
{
    public sealed class TerritoryState
    {
        private readonly ClientState _clientState;
        private readonly Condition _condition;

        public TerritoryState(ClientState clientState, Condition condition)
        {
            _clientState = clientState;
            _condition = condition;
        }

        public ushort LastTerritory { get; set; }
        public PomanderState PomanderOfSight { get; set; } = PomanderState.Inactive;
        public PomanderState PomanderOfIntuition { get; set; } = PomanderState.Inactive;

        public bool IsInDeepDungeon() =>
            _clientState.IsLoggedIn
            && _condition[ConditionFlag.InDeepDungeon]
            && typeof(ETerritoryType).IsEnumDefined(_clientState.TerritoryType);

    }

    public enum PomanderState
    {
        Inactive,
        Active,
        FoundOnCurrentFloor,
        PomanderOfSafetyUsed,
    }
}
