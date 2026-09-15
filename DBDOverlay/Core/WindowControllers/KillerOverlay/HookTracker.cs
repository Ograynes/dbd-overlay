namespace DBDOverlay.Core.WindowControllers.KillerOverlay
{

    public sealed class HookTracker
    {
        private HudObservation pending;
        private int confirmations;
        public HudObservation State { get; private set; } = HudObservation.Unknown;
        public int Hooks { get; private set; }
        public HookTransition Observe(HudObservation observation)
        {
            if (observation == HudObservation.Unknown) { confirmations = 0; pending = observation; return HookTransition.None; }
            if (pending != observation) { pending = observation; confirmations = 0; }
            if (confirmations < 3) confirmations++;
            if (confirmations < 3 || observation == State || State == HudObservation.Terminal) return HookTransition.None;
            var previous = State; State = observation;
            if (observation == HudObservation.Terminal) return HookTransition.Terminal;
            if (observation == HudObservation.Hooked) { Hooks++; return HookTransition.Hooked; }
            return previous == HudObservation.Hooked ? HookTransition.Unhooked : HookTransition.None;
        }
    }
}
