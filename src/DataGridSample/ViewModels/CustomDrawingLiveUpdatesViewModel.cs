using System;
using DataGridSample.Collections;
using DataGridSample.CustomDrawing;
using DataGridSample.Mvvm;
using Avalonia.Threading;

namespace DataGridSample.ViewModels;

public sealed class CustomDrawingLiveUpdatesViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();
    private SkiaAnimatedTextCellDrawOperationFactory? _factory;
    private bool _isRunning;
    private int _intervalMs = 33;
    private long _frameCount;
    private float _phase;

    public CustomDrawingLiveUpdatesViewModel()
    {
        Rows = new ObservableRangeCollection<CustomDrawingLiveUpdatesRow>();
        StartCommand = new RelayCommand(_ => Start(), _ => !IsRunning);
        StopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
        ResetRowsCommand = new RelayCommand(_ => ResetRows());

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_intervalMs)
        };
        _timer.Tick += (_, __) => Tick();

        ResetRows();
    }

    public ObservableRangeCollection<CustomDrawingLiveUpdatesRow> Rows { get; }

    public RelayCommand StartCommand { get; }

    public RelayCommand StopCommand { get; }

    public RelayCommand ResetRowsCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                StartCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(RunState));
            }
        }
    }

    public int IntervalMs
    {
        get => _intervalMs;
        set
        {
            int next = Math.Max(1, value);
            if (!SetProperty(ref _intervalMs, next))
            {
                return;
            }

            _timer.Interval = TimeSpan.FromMilliseconds(next);
        }
    }

    public long FrameCount
    {
        get => _frameCount;
        private set => SetProperty(ref _frameCount, value);
    }

    public float Phase
    {
        get => _phase;
        private set => SetProperty(ref _phase, value);
    }

    public string RunState => IsRunning ? "Running" : "Stopped";

    public void AttachFactory(SkiaAnimatedTextCellDrawOperationFactory? factory)
    {
        _factory = factory;
        if (_factory is null)
        {
            return;
        }

        _factory.SetPhase(Phase);
    }

    public void OnAttached()
    {
        Start();
    }

    public void OnDetached()
    {
        Stop();
    }

    private void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _timer.Interval = TimeSpan.FromMilliseconds(_intervalMs);
        _timer.Start();
        IsRunning = true;
    }

    private void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        _timer.Stop();
        IsRunning = false;
    }

    private void ResetRows()
    {
        FrameCount = 0;
        Phase = 0f;

        var rows = new CustomDrawingLiveUpdatesRow[300];
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = new CustomDrawingLiveUpdatesRow
            {
                Id = index + 1,
                Symbol = $"SYM{(index % 100):D3}",
                Message = CreateMessage(index)
            };
        }

        Rows.ResetWith(rows);
        _factory?.SetPhaseAndInvalidate(Phase);
    }

    private void Tick()
    {
        Phase += 0.085f;
        FrameCount++;
        _factory?.SetPhaseAndInvalidate(Phase);
    }

    private string CreateMessage(int index)
    {
        string[] words =
        {
            "latency", "metrics", "render", "cell", "cache", "virtualization",
            "foreground", "selection", "measure", "arrange", "invalidation"
        };

        int wordA = _random.Next(words.Length);
        int wordB = _random.Next(words.Length);
        int wordC = _random.Next(words.Length);

        return $"Row {index + 1}: {words[wordA]} {words[wordB]} {words[wordC]} pulse stream";
    }
}

public sealed class CustomDrawingLiveUpdatesRow
{
    public int Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
