using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SkyscapeMidiDisplayer.Models;
using SkyscapeMidiDisplayer.Services;

namespace SkyscapeMidiDisplayer.Controls;

public class PianoRollControl : Control
{
    public static readonly StyledProperty<double> CurrentTimeMsProperty =
        AvaloniaProperty.Register<PianoRollControl, double>(nameof(CurrentTimeMs));

    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<PianoRollControl, double>(nameof(Speed), 200.0);

    public static readonly StyledProperty<int> MinNoteProperty =
        AvaloniaProperty.Register<PianoRollControl, int>(nameof(MinNote), 21);

    public static readonly StyledProperty<int> MaxNoteProperty =
        AvaloniaProperty.Register<PianoRollControl, int>(nameof(MaxNote), 108);

    public static readonly StyledProperty<double> DurationMsProperty =
        AvaloniaProperty.Register<PianoRollControl, double>(nameof(DurationMs));

    public static readonly StyledProperty<ObservableCollection<MidiNote>?> NotesProperty =
        AvaloniaProperty.Register<PianoRollControl, ObservableCollection<MidiNote>?>(nameof(Notes));

    public static readonly StyledProperty<AudioService?> AudioServiceProperty =
        AvaloniaProperty.Register<PianoRollControl, AudioService?>(nameof(AudioService));

    private const double PianoKeyHeight = 60;
    private const double FadeInDistance = 80;
    private HashSet<int> _pressedNotes = new();

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(20, 22, 28));
    private static readonly IBrush WhiteKeyBrush = new SolidColorBrush(Color.FromRgb(245, 245, 250));
    private static readonly IBrush BlackKeyBrush = new SolidColorBrush(Color.FromRgb(35, 35, 40));
    private static readonly IBrush WhiteKeyPressedBrush = new SolidColorBrush(Color.FromRgb(200, 220, 255));
    private static readonly IBrush BlackKeyPressedBrush = new SolidColorBrush(Color.FromRgb(60, 70, 90));
    private static readonly IBrush NoteBrush = new SolidColorBrush(Color.FromRgb(100, 180, 255));
    private static readonly IBrush NoteHighlightBrush = new SolidColorBrush(Color.FromRgb(150, 210, 255));
    private static readonly IBrush LeftHandBrush = new SolidColorBrush(Color.FromRgb(255, 140, 80));
    private static readonly IBrush RightHandBrush = new SolidColorBrush(Color.FromRgb(100, 200, 150));
    private static readonly IBrush GridLineBrush = new SolidColorBrush(Color.FromRgb(60, 65, 75), 0.5);
    private static readonly IPen GridLinePen = new Pen(GridLineBrush, 1);

    public double CurrentTimeMs
    {
        get => GetValue(CurrentTimeMsProperty);
        set => SetValue(CurrentTimeMsProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public int MinNote
    {
        get => GetValue(MinNoteProperty);
        set => SetValue(MinNoteProperty, value);
    }

    public int MaxNote
    {
        get => GetValue(MaxNoteProperty);
        set => SetValue(MaxNoteProperty, value);
    }

    public double DurationMs
    {
        get => GetValue(DurationMsProperty);
        set => SetValue(DurationMsProperty, value);
    }

    public ObservableCollection<MidiNote>? Notes
    {
        get => GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public AudioService? AudioService
    {
        get => GetValue(AudioServiceProperty);
        set => SetValue(AudioServiceProperty, value);
    }

    private int NoteRange => MaxNote - MinNote + 1;

    static PianoRollControl()
    {
        AffectsRender<PianoRollControl>(
            CurrentTimeMsProperty, SpeedProperty, MinNoteProperty, 
            MaxNoteProperty, DurationMsProperty, NotesProperty);
    }

    public PianoRollControl()
    {
        AddHandler(PointerPressedEvent, OnPointerPressed);
        AddHandler(PointerReleasedEvent, OnPointerReleased);
        AddHandler(PointerMovedEvent, OnPointerMoved);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        var noteWidth = bounds.Width / NoteRange;
        var pianoAreaHeight = PianoKeyHeight;
        var waterfallHeight = bounds.Height - pianoAreaHeight;

        var activeNotes = GetActiveNotes(waterfallHeight);

        DrawBackground(context, bounds, waterfallHeight);
        DrawWaterfallArea(context, bounds, noteWidth, waterfallHeight);
        DrawPianoKeys(context, bounds, noteWidth, pianoAreaHeight, activeNotes);
        DrawNotes(context, bounds, noteWidth, waterfallHeight);
        DrawPlayhead(context, bounds, waterfallHeight);
    }

    private HashSet<int> GetActiveNotes(double waterfallHeight)
    {
        var activeNotes = new HashSet<int>();
        if (Notes == null) return activeNotes;

        foreach (var note in Notes)
        {
            if (note.NoteNumber < MinNote || note.NoteNumber > MaxNote) continue;

            var noteStartY = waterfallHeight - (note.StartTimeMs - CurrentTimeMs) / 1000.0 * Speed;
            var noteEndY = noteStartY - note.DurationMs / 1000.0 * Speed;

            if (noteStartY >= waterfallHeight && noteEndY <= waterfallHeight)
            {
                activeNotes.Add(note.NoteNumber);
            }
        }

        return activeNotes;
    }

    private void DrawBackground(DrawingContext context, Rect bounds, double waterfallHeight)
    {
        context.FillRectangle(BackgroundBrush, bounds);
    }

    private void DrawWaterfallArea(DrawingContext context, Rect bounds, double noteWidth, double waterfallHeight)
    {
        for (int i = 0; i < NoteRange; i++)
        {
            var noteNumber = MinNote + i;
            var isBlackKey = IsBlackKey(noteNumber);
            var x = bounds.Left + i * noteWidth;
            var rect = new Rect(x, 0, noteWidth, waterfallHeight);
            var brush = isBlackKey 
                ? new SolidColorBrush(Color.FromRgb(30, 32, 38)) 
                : new SolidColorBrush(Color.FromRgb(38, 40, 48));
            context.FillRectangle(brush, rect);
        }

        for (int i = 0; i <= NoteRange; i++)
        {
            var noteNumber = MinNote + i;
            if (IsBlackKey(noteNumber)) continue;

            var x = bounds.Left + i * noteWidth;
            context.DrawLine(GridLinePen, new Point(x, 0), new Point(x, waterfallHeight));
        }
    }

    private void DrawPianoKeys(DrawingContext context, Rect bounds, double noteWidth, double pianoAreaHeight, HashSet<int> activeNotes)
    {
        var pianoTop = bounds.Height - pianoAreaHeight;

        for (int i = 0; i < NoteRange; i++)
        {
            var noteNumber = MinNote + i;
            var isBlackKey = IsBlackKey(noteNumber);
            var isActive = activeNotes.Contains(noteNumber) || _pressedNotes.Contains(noteNumber);
            var x = bounds.Left + i * noteWidth;
            var rect = new Rect(x, pianoTop, noteWidth, pianoAreaHeight);
            
            IBrush brush;
            if (isActive)
            {
                brush = isBlackKey ? BlackKeyPressedBrush : WhiteKeyPressedBrush;
            }
            else
            {
                brush = isBlackKey ? BlackKeyBrush : WhiteKeyBrush;
            }
            context.FillRectangle(brush, rect);

            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 70)), 1);
            context.DrawRectangle(borderPen, rect);

            if (!isBlackKey)
            {
                var shadowBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0), 0.1);
                var shadowRect = new Rect(x, pianoTop, noteWidth, 3);
                context.FillRectangle(shadowBrush, shadowRect);
            }
        }
    }

    private void DrawNotes(DrawingContext context, Rect bounds, double noteWidth, double waterfallHeight)
    {
        if (Notes == null) return;

        var visibleTimeRange = waterfallHeight / Speed * 1000;
        var startTime = CurrentTimeMs - visibleTimeRange * 0.1;
        var endTime = CurrentTimeMs + visibleTimeRange * 0.9;

        foreach (var note in Notes)
        {
            if (note.EndTimeMs < startTime || note.StartTimeMs > endTime) continue;
            if (note.NoteNumber < MinNote || note.NoteNumber > MaxNote) continue;

            var noteIndex = note.NoteNumber - MinNote;
            var x = bounds.Left + noteIndex * noteWidth + 2;

            var noteHeight = note.DurationMs / 1000.0 * Speed;
            noteHeight = Math.Max(12, noteHeight);

            var noteStartY = waterfallHeight - (note.StartTimeMs - CurrentTimeMs) / 1000.0 * Speed;
            var noteEndY = noteStartY - noteHeight;

            double drawStartY = Math.Max(0, noteEndY);
            double drawEndY = Math.Min(waterfallHeight, noteStartY);

            if (drawStartY >= drawEndY) continue;

            // 音键透明度 - 无淡入效果，始终完全不透明
            double fadeInAlphaTop = 1.0;
            double fadeInAlphaBottom = 1.0;

            var rect = new Rect(x, drawStartY, noteWidth - 4, drawEndY - drawStartY);
            
            var isBlackKey = IsBlackKey(note.NoteNumber);
            Color baseColor;
            
            if (note.Hand == Models.HandType.Left)
            {
                baseColor = ((SolidColorBrush)LeftHandBrush).Color;
            }
            else if (note.Hand == Models.HandType.Right)
            {
                baseColor = ((SolidColorBrush)RightHandBrush).Color;
            }
            else if (note.Color is SolidColorBrush scb)
            {
                baseColor = scb.Color;
            }
            else
            {
                baseColor = isBlackKey 
                    ? Color.FromRgb(80, 140, 200) 
                    : Color.FromRgb(100, 180, 255);
            }

            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(ColorFromRgbWithAlpha(baseColor, fadeInAlphaTop * 1.0), 0),
                    new GradientStop(ColorFromRgbWithAlpha(baseColor, fadeInAlphaBottom * 0.7), 1)
                }
            };

            context.FillRectangle(gradientBrush, rect, 3);

            var highlightAlpha = 0.4 * fadeInAlphaTop;
            var highlightBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255), highlightAlpha);
            var highlightHeight = Math.Min(2, (drawEndY - drawStartY) * 0.3);
            context.FillRectangle(highlightBrush, 
                new Rect(x + 1, drawStartY + 1, noteWidth - 6, highlightHeight), 2);
        }
    }

    private double EaseOutCubic(double t)
    {
        return 1 - Math.Pow(1 - t, 3);
    }

    private Color ColorFromRgbWithAlpha(Color baseColor, double factor)
    {
        return Color.FromArgb(
            (byte)(255 * factor),
            baseColor.R,
            baseColor.G,
            baseColor.B
        );
    }

    private Color ColorFromRgb(Color baseColor, double factor)
    {
        return Color.FromRgb(
            (byte)(baseColor.R * factor),
            (byte)(baseColor.G * factor),
            (byte)(baseColor.B * factor)
        );
    }

    private void DrawPlayhead(DrawingContext context, Rect bounds, double waterfallHeight)
    {
        var playheadPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 100, 100), 0.8), 2);
        context.DrawLine(playheadPen, new Point(0, waterfallHeight), new Point(bounds.Width, waterfallHeight));

        var glowBrush = new SolidColorBrush(Color.FromRgb(255, 100, 100), 0.2);
        var glowRect = new Rect(0, waterfallHeight - 5, bounds.Width, 10);
        context.FillRectangle(glowBrush, glowRect);
    }

    private static bool IsBlackKey(int noteNumber)
    {
        var noteInOctave = noteNumber % 12;
        return noteInOctave == 1 || noteInOctave == 3 || noteInOctave == 6 ||
               noteInOctave == 8 || noteInOctave == 10;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandlePointerAction(e.GetPosition(this));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressedNotes.Clear();
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            HandlePointerAction(e.GetPosition(this));
        }
    }

    private void HandlePointerAction(Point position)
    {
        var bounds = Bounds;
        var noteWidth = bounds.Width / NoteRange;
        var pianoAreaHeight = PianoKeyHeight;
        var pianoTop = bounds.Height - pianoAreaHeight;

        // 检查点击是否在钢琴键盘区域
        if (position.Y >= pianoTop && position.Y <= bounds.Height)
        {
            var noteIndex = (int)(position.X / noteWidth);
            if (noteIndex >= 0 && noteIndex < NoteRange)
            {
                var noteNumber = MinNote + noteIndex;
                
                // 只有当音符不在已按下集合中时才播放
                if (!_pressedNotes.Contains(noteNumber))
                {
                    _pressedNotes.Add(noteNumber);
                    InvalidateVisual();
                    
                    // 播放音效
                    AudioService?.PlayNote(noteNumber, 100, 500);
                }
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(availableSize.Width, availableSize.Height);
    }
}
