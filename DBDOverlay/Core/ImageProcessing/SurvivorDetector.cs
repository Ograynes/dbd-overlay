using DBDOverlay.Core.WindowControllers.KillerOverlay;
using DBDOverlay.Images.SurvivorStates;
using System.Drawing;
using System;
using System.Linq;

namespace DBDOverlay.Core.ImageProcessing
{
    public static class SurvivorDetector
    {
        public static HudObservation Classify(Bitmap image, bool eight, int threshold, Func<double> unhookedMatch = null)
        {
            if (SurvivorImageMatcher.MatchPortrait(image, image) == 0) return HudObservation.Unknown;
            var hooks = eight
                ? new[] { SurvivorStates.Hooked2v8_0, SurvivorStates.Hooked2v8_1, SurvivorStates.Hooked2v8_2, SurvivorStates.Hooked2v8_3, SurvivorStates.Hooked2v8_4 }
                : new[] { SurvivorStates.Hooked, SurvivorStates.Hooked2, SurvivorStates.Hooked3 };
            var terminal = eight
                ? new[] { SurvivorStates.Escaped_2v8_0, SurvivorStates.Escaped_2v8_1, SurvivorStates.Escaped_2v8_2, SurvivorStates.Escaped_2v8_3,
                    SurvivorStates.Sacrificed_2v8_0, SurvivorStates.Sacrificed_2v8_1, SurvivorStates.Sacrificed_2v8_2, SurvivorStates.Sacrificed_2v8_3, SurvivorStates.Dead }
                : new[] { SurvivorStates.Escaped, SurvivorStates.Sacrificed, SurvivorStates.Sacrificed2, SurvivorStates.Dead };
            double hook = hooks.Max(reference => SurvivorImageMatcher.Match(image, reference, threshold));
            double end = terminal.Max(reference => SurvivorImageMatcher.Match(image, reference, threshold));
            if (hook >= .80 && hook - end >= .08) return HudObservation.Hooked;
            if (end >= .80 && end - hook >= .08) return HudObservation.Terminal;
            if (hook < .45 && end < .45 && unhookedMatch != null && unhookedMatch() >= .92) return HudObservation.Unhooked;
            return HudObservation.Unknown;
        }
    }
}
