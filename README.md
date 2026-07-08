# Rudolf.Adapters.TrainCrew

A [Rudolf](https://github.com/haruyukitanuki/rudolf) adapter for **TRAIN CREW**.

It reads live train state out of TRAIN CREW and hands it back as Rudolf documents
(`SimulatorProfile`, `OutputDataFrame`), and forwards Rudolf input commands the other way.

State reads and input dispatch go through the in-process `TrainCrewInput.dll` (I/O 1); a best-effort Websocket
(`ws://localhost:50300/`) supplements extra fields (I/O 2). Degraded data set is provided gracefully if the Websocket
server is unavailable.

The adapter implements `IRudolfAdapter`, so consumers program against the Rudolf contract.

This is the same adapter that is used by the Tanuden Console without modifications.

## About Rudolf

This is a consumer adapter. The wire format itself (the spec and the type packages) lives in the
[main repository](https://github.com/haruyukitanuki/rudolf).

Start there for the schema, the `SimulatorProfile`/`OutputDataFrame`/`InputCommand` document
shapes, and the list of other available adapters.

## Usage

Construct the adapter, `Start()` it once, then poll `GetProfile()`/`GetCurrentFrame()` each frame and
`Dispatch()` commands as needed. It implements `IDisposable`.

```csharp
using Tanuden.Rudolf;
using Tanuden.Rudolf.Adapters.TrainCrew;
using Tanuden.Rudolf.Input;

internal static class Program
{
    private static void Main()
    {
        // STEP 1 (INIT): create the adapter and start background collection (the WebSocket supplement).
        IRudolfAdapter adapter = new TrainCrewRudolfAdapter();
        adapter.Start();

        // STEP 2 (RUN): poll once TRAIN CREW is in an active scenario.
        if (adapter.IsReady)
        {
            // Profile: emitted once per scenario (cached internally).
            SimulatorProfile? profile = adapter.GetProfile();

            // Frame: a fresh snapshot every time, so call this on your render/telemetry loop.
            OutputDataFrame? frame = adapter.GetCurrentFrame();

            // Input: forward a Rudolf command back to the sim.
            adapter.Dispatch(new SetNotchCommand { Value = -1 });
        }

        // STEP 3 (SHUTDOWN): dispose tears down the WebSocket and its subscriptions.
        // NOTE: this explicit call is skipped if STEP 2 throws. In production, guarantee cleanup with a
        // `using` declaration or a try/finally around the run loop.
        adapter.Dispose();
    }
}
```

`GetProfile()` and `GetCurrentFrame()` return `null` when TRAIN CREW is not in an active scenario, so
guard on `IsReady` (or on the null result). Serialize the returned objects with `RudolfJson.Options`
from the `Tanuden.Rudolf` package so the wire stays camelCase UTF-8 JSON.

### Dependencies

- `TrainCrewInput.dll` must be available at runtime. You can get a copy of it on
  the [game developer's webpage](https://acty-soft.com/traincrew/controller/).
- `WebSocket.Client`
- Target `net10.0`

### In-game Settings

I/O 1 and I/O 2 will need to be enabled in the game settings for this adapter to function correctly.

I/O 2 is optional however we recommend that you turn it on for the adapter to retrieve as much telemetry it
can get from the game.

## Data sources

This adapter uses 2 different APIs provided by the game to populate as many fields that Rudolf has.

Via `TrainCrewInput.dll` (I/O 1). This is the primary channel and supplies most of the frame:

- Vehicle formation and per-car flags (model, driver/conductor cab, motor, pantograph presence)
- Speed, distance travelled, gradient, and main reservoir pressure
- Power/brake notch and reverser position
- Door states (all-closed flag and per-car)
- Panel indicator lamps
- ATS class, speed, and state
- Signals ahead (aspect, distance, transponder beacons) and the next speed limit
- Turnout/switch state
- Station list (name, distance, door side, stop type, scheduled arrival/departure, stop position name)
- Diagram info (train number, bound-for, service type, run number)
- Game state (screen, crew role, drive mode, one-man)
- Sim clock and elapsed time
- Input dispatch: every control command (notch, power/brake notch, brake SAP, reverser, buttons, wiper, ATO notch,
  deadman)

Via Websocket API (I/O 2). Optional supplement; absent gracefully when the server is down:

- Absolute distance (kilometre post), filling `physics.absoluteDistance`
- ATS rich state, decoded from the ATS state bitmask
- Full-line signal aspects (`traincrew:signals` extension)
- Track circuit occupancy (`traincrew:trackCircuits` extension)
- Other trains on the line (`traincrew:otherTrains` extension)
- Interlocking routes (`traincrew:interlocking` extension)

A built-in ROM file. Static route and rolling-stock data the game APIs do not expose:

- Rolling-stock capabilities: mascon and brake handle types, power and brake notch counts, EB notch, holding-brake
  notches, and compressor cut-in/cut-out pressures
- Per-car pantograph type (single-arm or scissor) and mounting direction
- Route timing-point stations (採時駅)

## Open Source @ Tanuden

Rudolf.Adapters.TrainCrew is Open Source Software (OSS), licensed under Apache 2.0. You may freely
distribute, use and modify code provided to you in repository in accordance with it.

A copy of the license can be found at the root of the repository.

## Support

[Tanuden Discord Server](https://go.tanu.ch/tanuden-discord) | [Twitter](https://go.tanu.ch/twitter) | [YouTube](https://go.tanu.ch/tanutube)

**Tanukigawa Railway | Copyright (c) 2026 Haruyuki Tanukiji.**
