using MultiRoomAudio.Models;

namespace MultiRoomAudio.Relay;

/// <summary>
/// Interface for relay board implementations.
/// Allows for real hardware (FtdiRelayBoard) or mock implementations.
/// </summary>
public interface IRelayBoard : IDisposable
{
    /// <summary>
    /// Whether the relay board is currently connected/available.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Current relay states as an int (bit 0 = relay 1, supports up to 16 relays).
    /// </summary>
    int CurrentState { get; }

    /// <summary>
    /// Open connection to the first available device.
    /// </summary>
    bool Open();

    /// <summary>
    /// Open connection to a specific device by index.
    /// </summary>
    bool Open(int deviceIndex);

    /// <summary>
    /// Open connection to a specific device by serial number.
    /// </summary>
    bool OpenBySerial(string serialNumber);

    /// <summary>
    /// Close connection to the relay board.
    /// </summary>
    void Close();

    /// <summary>
    /// Set the state of a specific relay channel.
    /// </summary>
    /// <param name="channel">Channel number (1-16).</param>
    /// <param name="on">True to turn on, false to turn off.</param>
    bool SetRelay(int channel, bool on);

    /// <summary>
    /// Get the state of a specific relay channel.
    /// </summary>
    /// <param name="channel">Channel number (1-16).</param>
    RelayState GetRelay(int channel);

    /// <summary>
    /// Set all relay states at once.
    /// </summary>
    /// <param name="states">Value where each bit represents a relay.</param>
    bool SetAllRelays(ushort states);

    /// <summary>
    /// Turn off all relays.
    /// </summary>
    bool AllOff();
}
