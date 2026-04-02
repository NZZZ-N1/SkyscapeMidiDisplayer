using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SkyscapeMidiDisplayer.Models;

namespace SkyscapeMidiDisplayer.Services;

public class MidiParserService
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static readonly IBrush[] TrackColors = {
        Brushes.DodgerBlue, Brushes.Crimson, Brushes.LimeGreen, Brushes.Orange,
        Brushes.Purple, Brushes.Teal, Brushes.Gold, Brushes.Magenta,
        Brushes.Cyan, Brushes.Coral, Brushes.Violet, Brushes.Turquoise,
        Brushes.Salmon, Brushes.Khaki, Brushes.Plum, Brushes.Tan
    };

    public MidiFileData? ParseMidiFile(string filePath)
    {
        try
        {
            var midiFile = MidiFile.Read(filePath);
            var tempoMap = midiFile.GetTempoMap();
            var fileData = new MidiFileData
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                TicksPerQuarterNote = midiFile.TimeDivision is TicksPerQuarterNoteTimeDivision tpqn 
                    ? tpqn.TicksPerQuarterNote 
                    : 480
            };

            var tracks = midiFile.GetTrackChunks().ToList();
            var allNotes = new List<MidiNote>();

            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                var track = tracks[trackIndex];
                var trackData = new MidiTrack
                {
                    Index = trackIndex,
                    Name = GetTrackName(track, trackIndex),
                    Color = TrackColors[trackIndex % TrackColors.Length]
                };

                var notes = GetNotesFromTrack(track, tempoMap, trackIndex, trackData.Color);
                trackData.Notes.AddRange(notes);
                fileData.Tracks.Add(trackData);
                allNotes.AddRange(notes);
            }

            if (allNotes.Count > 0)
            {
                fileData.DurationMs = allNotes.Max(n => n.EndTimeMs);
                fileData.MinNote = Math.Max(0, allNotes.Min(n => n.NoteNumber) - 2);
                fileData.MaxNote = Math.Min(127, allNotes.Max(n => n.NoteNumber) + 2);
            }

            return fileData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing MIDI file: {ex.Message}");
            return null;
        }
    }

    private string GetTrackName(TrackChunk track, int index)
    {
        var trackNameEvent = track.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault();
        return !string.IsNullOrEmpty(trackNameEvent?.Text) ? trackNameEvent.Text : $"Track {index + 1}";
    }

    private List<MidiNote> GetNotesFromTrack(TrackChunk track, TempoMap tempoMap, int trackIndex, IBrush color)
    {
        var notes = new List<MidiNote>();
        using var notesManager = track.ManageNotes();
        var timedNotes = notesManager.Objects;

        foreach (var note in timedNotes)
        {
            var startTime = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, tempoMap);
            var endTime = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time + note.Length, tempoMap);

            var midiNote = new MidiNote
            {
                NoteNumber = note.NoteNumber,
                NoteName = NoteNames[note.NoteNumber % 12],
                StartTimeMs = startTime.TotalMilliseconds,
                EndTimeMs = endTime.TotalMilliseconds,
                Velocity = note.Velocity,
                Channel = note.Channel,
                TrackIndex = trackIndex,
                Color = color,
                Hand = DetermineHand(note.NoteNumber)
            };

            notes.Add(midiNote);
        }

        return notes;
    }

    private HandType DetermineHand(int noteNumber)
    {
        const int middleC = 60;
        return noteNumber <= middleC ? HandType.Left : HandType.Right;
    }
}
