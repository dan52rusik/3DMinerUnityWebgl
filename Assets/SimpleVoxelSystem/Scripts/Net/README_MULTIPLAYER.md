# Multiplayer Bootstrap (Lobby + Private Islands)

This project now includes a lightweight NGO bootstrap:

- `NetworkBootstrap`
- `NetPlayerAvatar`
- `NetWorldPresenceSync`

## What works now

- Shared network lobby movement (host/client).
- Ownership-safe player control:
  - local player can control character/mine
  - remote players are read-only
- Presence sync:
  - lobby is shared
  - islands are private (remote players are hidden when local player is on island)

## Scene setup (once)

1. Open `Game` scene.
2. Create an empty object `NetBootstrap`.
3. Add component `SimpleVoxelSystem.Net.NetworkBootstrap`.
4. In inspector:
   - `Auto Start = Disabled` (or Host/Client for quick tests)
   - `Show Debug Gui = true`
   - `Address = 127.0.0.1`
   - `Port = 7777`
5. Press Play and use debug buttons:
   - one instance as Host
   - second instance as Client

## Important limits of this stage

- Voxel mining state is still local-authoritative and not replicated to all peers.
- Economy/store/mine placement are not yet server-authoritative.
- For Yandex release, add a dedicated server/relay layer before production launch.

