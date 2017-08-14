﻿using Melanchall.DryWetMidi.Smf;
using Melanchall.DryWetMidi.Smf.Interaction;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Melanchall.DryWetMidi.Tests
{
    [TestClass]
    public class QuantizeNotes
    {
        #region Test methods

        [TestMethod]
        [Description("Quantize notes by quarter-step grid.")]
        public void Quantize_Musical_Quarter()
        {
            var midiFile = new MidiFile(new TrackChunk())
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision(10)
            };

            using (var notesManager = midiFile.GetTrackChunks().First().ManageNotes())
            {
                var notes = notesManager.Notes;
                notes.Add(new Note(SevenBitNumber.MaxValue, 10, 0),
                          new Note(SevenBitNumber.MaxValue, 10, 1),
                          new Note(SevenBitNumber.MaxValue, 10, 8),
                          new Note(SevenBitNumber.MaxValue, 10, 5),
                          new Note(SevenBitNumber.MaxValue, 10, 15),
                          new Note(SevenBitNumber.MaxValue, 10, 19));
            }

            Quantize(midiFile, MusicalFraction.Quarter);

            var actualTimes = midiFile.GetNotes()
                                      .Select(n => n.Time)
                                      .Distinct()
                                      .ToList();
            var expectedTimes = Enumerable.Range(0, actualTimes.Count)
                                          .Select(i => i * 10L)
                                          .ToList();

            CollectionAssert.AreEqual(actualTimes, expectedTimes);
        }

        #endregion

        #region Private methods

        private static void Quantize(MidiFile midiFile, MusicalLength step)
        {
            var tempoMap = midiFile.GetTempoMap();
            var stepTicks = LengthConverter.ConvertFrom(step, 0, tempoMap);

            midiFile.ProcessNotes(n =>
            {
                var time = n.Time;
                n.Time = (long)Math.Round(time / (double)stepTicks) * stepTicks;
            });
        }

        #endregion
    }
}