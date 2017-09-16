﻿using Melanchall.DryWetMidi.Common;

namespace Melanchall.DryWetMidi.Smf.Interaction
{
    internal static class MidiTimeSpanParser
    {
        #region Constants

        private const string TimeSpanGroupName = "ts";

        private static readonly string TimeSpanGroup = ParsingUtilities.GetNumberGroup(TimeSpanGroupName);

        private static readonly string[] Patterns = new[]
        {
            $@"{TimeSpanGroup}",
        };

        private const string OutOfRange = "Time span is out of range.";

        #endregion

        #region Methods

        internal static ParsingResult TryParse(string input, out MidiTimeSpan timeSpan)
        {
            timeSpan = null;

            if (string.IsNullOrWhiteSpace(input))
                return ParsingResult.EmptyInputString;

            var match = ParsingUtilities.Match(input, Patterns);
            if (match == null)
                return ParsingResult.NotMatched;

            if (!ParsingUtilities.ParseLong(match, TimeSpanGroupName, 0, out var midiTimeSpan))
                return new ParsingResult(OutOfRange);

            timeSpan = new MidiTimeSpan(midiTimeSpan);
            return ParsingResult.Parsed;
        }

        #endregion
    }
}
