using MultiRoomAudio.Models;

namespace MultiRoomAudio.Relay;

/// <summary>
/// Mock relay board implementation for testing without real hardware.
/// Simulates an 8/16 channel FTDI relay board in memory.
/// </summary>
public sealed class MockRelayBoard : IRelayBoard
{
    private ushort _currentState;
    private bool _isConnected;
    private bool _disposed;
    private readonly ILogger<MockRelayBoard>? _logger;
    private readonly object _lock = new();

    private readonly string _serialNumber;

    public MockRelayBoard(ILogger<MockRelayBoard>? logger = null, string? serialNumber = null)
    {
        _logger = logger;
        _serialNumber = serialNumber ?? "MOCK-FT245RL-001";
    }

    /// <summary>
    /// Check if mock mode is available (always true).
    /// </summary>
    public static bool IsLibraryAvailable() => true;

    /// <summary>
    /// Enumerate mock FTDI devices.
    /// Returns a single mock device.
    /// </summary>
    public static List<FtdiDeviceInfo> EnumerateDevices()
    {
        return new List<FtdiDeviceInfo>
        {
            new FtdiDeviceInfo(
                Index: 0,
                SerialNumber: "MOCK-FT245RL-001",
                Description: "Mock FTDI FT245RL Relay Board (8-channel)",
                IsOpen: false
            )
        };
    }

    /// <inheritdoc />
    public bool IsConnected
    {
        get
        {
            lock (_lock)
            {
                return _isConnected;
            }
        }
    }

    /// <inheritdoc />
    public int CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    /// <inheritdoc />
    public bool Open()
    {
        return Open(0);
    }

    /// <inheritdoc />
    public bool Open(int deviceIndex)
    {
        lock (_lock)
        {
            if (_isConnected)
            {
                _logger?.LogWarning("Mock relay board already open");
                return true;
            }

            _isConnected = true;
            _currentState = 0;
            _logger?.LogInformation("Mock relay board opened (device index {Index}, serial: {Serial})",
                deviceIndex, _serialNumber);
            return true;
        }
    }

    /// <inheritdoc />
    public bool OpenBySerial(string serialNumber)
    {
        lock (_lock)
        {
            if (_isConnected)
            {
                _logger?.LogWarning("Mock relay board already open");
                return true;
            }

            // Accept any serial number in mock mode
            _isConnected = true;
            _currentState = 0;
            _logger?.LogInformation("Mock relay board opened (serial: {Serial})", serialNumber);
            return true;
        }
    }

    /// <inheritdoc />
    public void Close()
    {
        lock (_lock)
        {
            if (!_isConnected)
                return;

            _currentState = 0;
            _isConnected = false;
            _logger?.LogInformation("Mock relay board closed");
        }
    }

    /// <inheritdoc />
    public bool SetRelay(int channel, bool on)
    {
        if (channel < 1 || channel > 16)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 1-16");

        lock (_lock)
        {
            if (!_isConnected)
            {
                _logger?.LogWarning("Cannot set relay - mock board not open");
                return false;
            }

            ushort bit = (ushort)(1 << (channel - 1));
            ushort newState;

            if (on)
                newState = (ushort)(_currentState | bit);
            else
                newState = (ushort)(_currentState & ~bit);

            if (newState != _currentState)
            {
                _currentState = newState;
                _logger?.LogDebug("Mock relay {Channel} set to {State} (state: 0x{StateHex:X4})",
                    channel, on ? "ON" : "OFF", _currentState);
            }

            return true;
        }
    }

    /// <inheritdoc />
    public RelayState GetRelay(int channel)
    {
        if (channel < 1 || channel > 16)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 1-16");

        lock (_lock)
        {
            if (!_isConnected)
                return RelayState.Unknown;

            ushort bit = (ushort)(1 << (channel - 1));
            return (_currentState & bit) != 0 ? RelayState.On : RelayState.Off;
        }
    }

    /// <inheritdoc />
    public bool SetAllRelays(ushort states)
    {
        lock (_lock)
        {
            if (!_isConnected)
            {
                _logger?.LogWarning("Cannot set relays - mock board not open");
                return false;
            }

            _currentState = states;
            _logger?.LogDebug("Mock relays set to 0x{State:X4}", states);
            return true;
        }
    }

    /// <inheritdoc />
    public bool AllOff()
    {
        return SetAllRelays(0x0000);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Close();
    }
}
