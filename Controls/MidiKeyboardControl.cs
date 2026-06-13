using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace SkyscapeMidiDisplayer.Controls;

public class MidiKeyboardControl : Control
{
    public static readonly StyledProperty<ObservableCollection<int>> ActiveNotesProperty =
        AvaloniaProperty.Register<MidiKeyboardControl, ObservableCollection<int>>(nameof(ActiveNotes));

    public ObservableCollection<int>? ActiveNotes
    {
        get => GetValue(ActiveNotesProperty);
        set => SetValue(ActiveNotesProperty, value);
    }

    private const int TotalKeys = 88;
    private const int StartNote = 21; // A0
    private const int EndNote = 108;  // C8
    
    // 白键索引映射 (MIDI note -> 白键索引)
    private static readonly int[] WhiteKeyNotes = new int[52];
    // 黑键索引映射 (MIDI note -> 黑键索引)
    private static readonly Dictionary<int, int> BlackKeyNotes = new();
    
    private readonly HashSet<int> _activeNotesSet = new();

    static MidiKeyboardControl()
    {
        // 初始化白键和黑键映射
        int whiteIndex = 0;
        int blackIndex = 0;
        
        for (int note = StartNote; note <= EndNote; note++)
        {
            int noteInOctave = note % 12;
            
            // 判断是白键还是黑键
            bool isWhiteKey = noteInOctave switch
            {
                0 => true,  // C
                2 => true,  // D
                4 => true,  // E
                5 => true,  // F
                7 => true,  // G
                9 => true,  // A
                11 => true, // B
                _ => false  // 黑键
            };
            
            if (isWhiteKey)
            {
                WhiteKeyNotes[whiteIndex++] = note;
            }
            else
            {
                BlackKeyNotes[note] = blackIndex++;
            }
        }
    }

    public MidiKeyboardControl()
    {
        AffectsRender<MidiKeyboardControl>(ActiveNotesProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ActiveNotesProperty)
        {
            if (change.OldValue is ObservableCollection<int> oldCollection)
            {
                oldCollection.CollectionChanged -= OnActiveNotesChanged;
            }

            if (change.NewValue is ObservableCollection<int> newCollection)
            {
                newCollection.CollectionChanged += OnActiveNotesChanged;
                UpdateActiveNotesSet(newCollection);
            }
        }
    }

    private void OnActiveNotesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ActiveNotes == null) return;
        
        UpdateActiveNotesSet(ActiveNotes);
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

    private void UpdateActiveNotesSet(ObservableCollection<int> notes)
    {
        _activeNotesSet.Clear();
        foreach (var note in notes)
        {
            _activeNotesSet.Add(note);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        double width = bounds.Width;
        double height = bounds.Height;
        
        double whiteKeyWidth = width / 52.0;
        double blackKeyWidth = whiteKeyWidth * 0.65;
        double blackKeyHeight = height * 0.6;

        // 绘制白键
        for (int i = 0; i < WhiteKeyNotes.Length; i++)
        {
            int note = WhiteKeyNotes[i];
            double x = i * whiteKeyWidth;
            var rect = new Rect(x, 0, whiteKeyWidth - 1, height);
            
            var brush = _activeNotesSet.Contains(note) 
                ? new SolidColorBrush(Color.Parse("#4CAF50")) 
                : new SolidColorBrush(Colors.White);
            
            context.FillRectangle(brush, rect);
            context.DrawRectangle(new Pen(Brushes.Black), rect);
        }

        // 绘制黑键
        for (int i = 0; i < WhiteKeyNotes.Length - 1; i++)
        {
            int currentNote = WhiteKeyNotes[i];
            int nextNote = WhiteKeyNotes[i + 1];
            int noteInOctave = currentNote % 12;
            
            // 根据当前白键的音名决定是否有黑键以及黑键的位置
            double? blackKeyOffset = noteInOctave switch
            {
                0 => 0.65,  // C 后面有 C#
                2 => 0.65,  // D 后面有 D#
                5 => 0.65,  // F 后面有 F#
                7 => 0.65,  // G 后面有 G#
                9 => 0.65,  // A 后面有 A#
                _ => null   // E, B 后面的黑键在下一个八度
            };
            
            if (blackKeyOffset.HasValue)
            {
                int blackNote = currentNote + 1;
                double x = (i + blackKeyOffset.Value) * whiteKeyWidth - blackKeyWidth / 2;
                var rect = new Rect(x, 0, blackKeyWidth, blackKeyHeight);
                
                var brush = _activeNotesSet.Contains(blackNote)
                    ? new SolidColorBrush(Color.Parse("#4CAF50"))
                    : new SolidColorBrush(Color.Parse("#1a1a1a"));
                
                context.FillRectangle(brush, rect);
            }
        }
    }
}