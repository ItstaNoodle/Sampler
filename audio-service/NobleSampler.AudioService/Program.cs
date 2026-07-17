using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.UseUrls("http://127.0.0.1:17891");
builder.Services.AddSingleton<SamplerEngine>();
builder.Services.AddCors();
var app = builder.Build();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

var engine = app.Services.GetRequiredService<SamplerEngine>();
await engine.StartAsync();

app.MapGet("/api/status", () => Results.Ok(engine.GetStatus()));
app.MapPost("/api/record/start", (RecordRequest request) =>
{
    engine.StartRecording(request.Bank, request.Slot, request.BufferSeconds ?? 5);
    return Results.Ok(engine.GetStatus());
});
app.MapPost("/api/record/stop", (RecordRequest request) =>
{
    var clip = engine.StopRecording(request.Bank, request.Slot);
    return Results.Ok(new { saved = clip, status = engine.GetStatus() });
});
app.MapPost("/api/play", (SlotRequest request) =>
{
    var played = engine.Play(request.Bank, request.Slot, request.Volume ?? 1f);
    return played ? Results.Ok(engine.GetStatus()) : Results.NotFound(new { error = "No clip is saved in this slot." });
});
app.MapPost("/api/stop", () =>
{
    engine.StopPlayback();
    return Results.Ok(engine.GetStatus());
});
app.MapPost("/api/clip/delete", (SlotRequest request) =>
{
    engine.DeleteClip(request.Bank, request.Slot);
    return Results.Ok(engine.GetStatus());
});
app.MapGet("/api/clips", () => Results.Ok(engine.GetClipMap()));
app.MapGet("/api/audio/devices", () => Results.Ok(engine.GetAudioDevices()));
app.MapGet("/api/audio/settings", () => Results.Ok(engine.GetAudioSettings()));
app.MapPut("/api/audio/settings", (AudioSettings request) =>
{
    engine.SetAudioSettings(request);
    return Results.Ok(engine.GetAudioSettings());
});

app.Lifetime.ApplicationStopping.Register(engine.Dispose);
app.Run();

public sealed record RecordRequest(int Bank, int Slot, int? BufferSeconds);
public sealed record SlotRequest(int Bank, int Slot, float? Volume);
public sealed record AudioSettings(string? CaptureDeviceId, string? PlaybackDeviceId);

public sealed class SamplerEngine : IDisposable
{
    private readonly object _sync = new();
    private readonly string _dataRoot;
    private WasapiLoopbackCapture? _capture;
    private CircularByteBuffer? _rollingBuffer;
    private MemoryStream? _activeRecording;
    private WaveFormat? _format;
    private IWavePlayer? _player;
    private AudioFileReader? _reader;
    private int? _recordingBank;
    private int? _recordingSlot;
    private int _maxBufferSeconds = 15;
    private readonly string _settingsPath;
    private AudioSettings _audioSettings = new(null, null);

    public SamplerEngine()
    {
        _dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NobleSampler");
        _settingsPath = Path.Combine(_dataRoot, "audio-settings.json");
        Directory.CreateDirectory(_dataRoot);
        for (var bank = 1; bank <= 4; bank++) Directory.CreateDirectory(Path.Combine(_dataRoot, $"bank-{bank}"));
        if (File.Exists(_settingsPath))
            _audioSettings = JsonSerializer.Deserialize<AudioSettings>(File.ReadAllText(_settingsPath)) ?? _audioSettings;
    }

    public Task StartAsync()
    {
        StartCaptureInternal();
        return Task.CompletedTask;
    }

    private void StartCaptureInternal()
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = ResolveDevice(enumerator, _audioSettings.CaptureDeviceId, DataFlow.Render);
        _capture = new WasapiLoopbackCapture(device);
        _format = _capture.WaveFormat;
        _rollingBuffer = new CircularByteBuffer(_format.AverageBytesPerSecond * _maxBufferSeconds);
        _capture.DataAvailable += (_, e) =>
        {
            lock (_sync)
            {
                _rollingBuffer.Write(e.Buffer, 0, e.BytesRecorded);
                _activeRecording?.Write(e.Buffer, 0, e.BytesRecorded);
            }
        };
        _capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null) Console.Error.WriteLine(e.Exception);
        };
        _capture.StartRecording();
    }

    public object GetAudioDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new { id = d.ID, name = d.FriendlyName, isDefault = d.ID == enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID })
            .ToArray();
    }

    public AudioSettings GetAudioSettings() { lock (_sync) return _audioSettings; }

    public void SetAudioSettings(AudioSettings settings)
    {
        lock (_sync)
        {
            if (_activeRecording is not null) throw new InvalidOperationException("Cannot change audio devices while recording.");
            using var enumerator = new MMDeviceEnumerator();
            if (!string.IsNullOrWhiteSpace(settings.CaptureDeviceId)) enumerator.GetDevice(settings.CaptureDeviceId);
            if (!string.IsNullOrWhiteSpace(settings.PlaybackDeviceId)) enumerator.GetDevice(settings.PlaybackDeviceId);
            var captureChanged = settings.CaptureDeviceId != _audioSettings.CaptureDeviceId;
            _audioSettings = settings;
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            StopPlaybackInternal();
            if (captureChanged)
            {
                _capture?.StopRecording();
                _capture?.Dispose();
                _capture = null;
                StartCaptureInternal();
            }
        }
    }

    public void StartRecording(int bank, int slot, int bufferSeconds)
    {
        ValidateSlot(bank, slot);
        lock (_sync)
        {
            if (_activeRecording is not null) throw new InvalidOperationException("A recording is already active.");
            // Release any reader holding the existing slot file before it is replaced on key-up.
            StopPlaybackInternal();
            var seconds = Math.Clamp(bufferSeconds, 0, _maxBufferSeconds);
            var bytes = _format!.AverageBytesPerSecond * seconds;
            var snapshot = _rollingBuffer!.Snapshot(bytes);
            _activeRecording = new MemoryStream(snapshot.Length + _format.AverageBytesPerSecond * 30);
            _activeRecording.Write(snapshot);
            _recordingBank = bank;
            _recordingSlot = slot;
        }
    }

    public string StopRecording(int bank, int slot)
    {
        ValidateSlot(bank, slot);
        lock (_sync)
        {
            if (_activeRecording is null) throw new InvalidOperationException("No recording is active.");
            if (_recordingBank != bank || _recordingSlot != slot) throw new InvalidOperationException("The active recording belongs to another slot.");

            var path = ClipPath(bank, slot);
            _activeRecording.Position = 0;
            using (var writer = new WaveFileWriter(path, _format!))
            {
                _activeRecording.CopyTo(writer);
            }
            _activeRecording.Dispose();
            _activeRecording = null;
            _recordingBank = null;
            _recordingSlot = null;
            return path;
        }
    }

    public bool Play(int bank, int slot, float volume)
    {
        ValidateSlot(bank, slot);
        var path = ClipPath(bank, slot);
        if (!File.Exists(path)) return false;
        lock (_sync)
        {
            StopPlaybackInternal();
            _reader = new AudioFileReader(path) { Volume = Math.Clamp(volume, 0f, 2f) };
            using var enumerator = new MMDeviceEnumerator();
            var output = ResolveDevice(enumerator, _audioSettings.PlaybackDeviceId, DataFlow.Render);
            _player = new WasapiOut(output, AudioClientShareMode.Shared, false, 100);
            _player.Init(_reader);
            _player.Play();
            return true;
        }
    }

    public void StopPlayback()
    {
        lock (_sync) StopPlaybackInternal();
    }

    public void DeleteClip(int bank, int slot)
    {
        ValidateSlot(bank, slot);
        var path = ClipPath(bank, slot);
        lock (_sync)
        {
            StopPlaybackInternal();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public object GetStatus()
    {
        lock (_sync)
        {
            return new
            {
                ready = _capture is not null,
                recording = _activeRecording is not null,
                recordingBank = _recordingBank,
                recordingSlot = _recordingSlot,
                playing = _player?.PlaybackState == PlaybackState.Playing,
                format = _format?.ToString(),
                dataRoot = _dataRoot
            };
        }
    }

    public Dictionary<string, bool> GetClipMap()
    {
        var result = new Dictionary<string, bool>();
        for (var bank = 1; bank <= 4; bank++)
            for (var slot = 1; slot <= 32; slot++)
                result[$"{bank}:{slot}"] = File.Exists(ClipPath(bank, slot));
        return result;
    }

    private string ClipPath(int bank, int slot) => Path.Combine(_dataRoot, $"bank-{bank}", $"slot-{slot}.wav");

    private static MMDevice ResolveDevice(MMDeviceEnumerator enumerator, string? id, DataFlow flow) =>
        string.IsNullOrWhiteSpace(id) ? enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia) : enumerator.GetDevice(id);

    private static void ValidateSlot(int bank, int slot)
    {
        if (bank is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(bank), "Bank must be 1-4.");
        if (slot is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be 1-32.");
    }

    private void StopPlaybackInternal()
    {
        var player = _player;
        _player = null;
        if (player is not null)
        {
            player.Stop();
            player.Dispose();
        }
        _reader?.Dispose();
        _reader = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;
            StopPlaybackInternal();
            _activeRecording?.Dispose();
            _activeRecording = null;
        }
    }
}

public sealed class CircularByteBuffer
{
    private readonly byte[] _buffer;
    private int _writePosition;
    private int _length;

    public CircularByteBuffer(int capacity) => _buffer = new byte[capacity];

    public void Write(byte[] source, int offset, int count)
    {
        if (count >= _buffer.Length)
        {
            Buffer.BlockCopy(source, offset + count - _buffer.Length, _buffer, 0, _buffer.Length);
            _writePosition = 0;
            _length = _buffer.Length;
            return;
        }

        var first = Math.Min(count, _buffer.Length - _writePosition);
        Buffer.BlockCopy(source, offset, _buffer, _writePosition, first);
        var remaining = count - first;
        if (remaining > 0) Buffer.BlockCopy(source, offset + first, _buffer, 0, remaining);
        _writePosition = (_writePosition + count) % _buffer.Length;
        _length = Math.Min(_length + count, _buffer.Length);
    }

    public byte[] Snapshot(int maxBytes)
    {
        var count = Math.Min(Math.Max(maxBytes, 0), _length);
        var result = new byte[count];
        var start = (_writePosition - count + _buffer.Length) % _buffer.Length;
        var first = Math.Min(count, _buffer.Length - start);
        Buffer.BlockCopy(_buffer, start, result, 0, first);
        if (count > first) Buffer.BlockCopy(_buffer, 0, result, first, count - first);
        return result;
    }
}
