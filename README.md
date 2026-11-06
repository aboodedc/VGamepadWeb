# VGamepadWeb

English | [简体中文](README.zh-CN.md)

Turn your phone into a wireless gamepad for your PC. Run a small server on Windows, open the page on a phone in the same network, and start playing — no app to install, no extra hardware.

Input travels over a WebRTC data channel, signaling over SignalR. The PC turns it into real gamepad signals through ViGEmBus, so games see an ordinary controller plugged into the machine and need no changes.

![Server UI](README/%E6%9C%8D%E5%8A%A1%E7%AB%AF%E4%B8%AD%E6%96%87%E7%95%8C%E9%9D%A2.jpg)

---

## How it works

The server listens on a port and serves the built web page itself. Open that address on a phone, pick a controller type, connect — the pad appears on screen: sticks, buttons, D-pad, triggers. Every press goes over the WebRTC data channel to the PC, which writes it into a virtual controller created through ViGEmBus.

---

## Slots, persistence and reconnection

This is the core of the project and deserves its own section.

The server always exposes **four slots**, mapped to Windows XInput 0–3 and shown as "Controller 1" … "Controller 4". A slot serves exactly one phone at a time.

### Devices stay plugged in

A slot's virtual controller is **not** created on connect and destroyed on disconnect. It is created the first time the slot is claimed and then stays on the bus:

- phone disconnects → the server only takes back the *claim*; the device stays;
- phone reconnects → it gets the same slot, the same device, the same XInput index;
- the device is only torn down when you uninstall it, or when the app exits.

The reason is practical. Games identify players by XInput index. If the device vanished on every disconnect, the next person to connect would land on slot 1 and the game would see "player 2 suddenly became player 1". With persistent devices, whoever sat in a seat keeps that seat.

### Remembering who sat where

Each slot remembers its last occupant:

- **Device code first** — a persistent UUID the phone generates in `localStorage` on first visit;
- **Remote IP as a fallback** — for older cached pages that don't send a code.

This memory lives **only for the current server process** and is cleared on restart. It decides seating, and it is never used for authentication.

### Which slot a phone gets

Phones don't ask for a slot (the current client doesn't pick one); the server decides:

1. **The slot this phone used last** — free, and the device code (or IP) matches;
2. **A clean slot nobody has ever used** — newcomers are seated in an untouched seat rather than taking someone else's leftovers;
3. **The lowest free slot** — once every seat has been used at least once;
4. All four taken → the phone is told "no free slot" and stays on the connection screen.

### Reconnecting

The same phone reconnecting takes route 1, so:

- the slot number is unchanged;
- the virtual device is not re-created or re-plugged;
- the game still sees the same player.

### Switching controller type

The type chosen on the connection screen decides which device the server builds. If a phone connected as Xbox 360 last time and comes back as DualShock 4, the server **removes the old device and builds the new one in the same slot**:

- destroy first, create second — only four virtual devices can exist at once, so creating a fifth while the old one is still on the bus would fail;
- same slot, so the player number stays; only the controller model changes.

### Uninstalling a device

When a slot is idle but still holds a device, its card shows **Uninstall**:

- it disconnects and disposes that virtual controller (it disappears from Device Manager);
- the "who sat here" memory is **kept**, so the same phone still lands on this slot and gets a fresh device of its chosen type.

### When a slot is released

| Situation | Who releases it |
| --- | --- |
| Page closed or navigated away | the phone calls Release; the server frees the slot immediately |
| Network lost, app suspended | SignalR heartbeat timeout (about 30 s) |
| Admin clicks "Disconnect" on the card | the server kicks the phone, which shows "The server disconnected you" |

After release the card reads **Waiting for reconnect** (a device is still on the bus) or **Waiting for a device** (the slot never had one, or it was uninstalled).

---

## Server app

A Windows desktop app with a hand-drawn console UI: the whole layout is composed on a 980×580 canvas, scaled proportionally and centered, and the window is resizable.

### The screen

![Server UI in English](README/%E6%9C%8D%E5%8A%A1%E7%AB%AF%E8%8B%B1%E6%96%87%E9%A1%B5%E9%9D%A2.jpg)

- **Phone address** — the current LAN address with a copy button and a QR code the phone can scan;
- **Port / password** — read-only while the service runs, editable when stopped; an empty password means no auth;
- **Start / stop** — one button, with both color and text for the state;
- **Settings** — interface language (中文 / English / العربية, with full RTL mirroring for Arabic) and the QR code;
- **Footer** — version and the light/dark switch.

The theme follows the Windows light/dark setting by default; the footer switch overrides it and the choice is persisted.

### Slot cards

Four cards, one per slot: number, title ("Controller 1"), state badge, device type (Xbox 360 / DualShock 4), and for connected slots the phone-reported model and LAN IP.

- Connected — offers **Disconnect**;
- Idle but still holding a device — shows **Waiting for reconnect**, the device type, and **Uninstall**;
- Never had a device — shows **Waiting for a device**.

### Startup self-check

After pressing Start, the server doesn't just trust its own state machine — it really connects to the local port and fetches the home page:

- port not listening → the start fails loudly and the service is cleaned up;
- home page unreachable, or not the expected document → reported as an error instead of a fake "running".

### Tray and single instance

- Closing the window hides it to the notification area; the service keeps running. Only the tray menu's **Exit** stops it;
- exiting stops the service first, so no port is left half-occupied;
- only one instance may run — launching a second copy shows "Already running" instead of two tray icons fighting over the same port.

### Settings and logs

| What | Where |
| --- | --- |
| Settings (port / password / language / theme) | `%APPDATA%\VGamepadWeb\settings.json` |
| Runtime log | `%APPDATA%\VGamepadWeb\server.log` |

A corrupt settings file is not silently reset: it is logged, then defaults are used.

---

## Phone UI

### Connection screen

![Phone connection screen](README/%E5%AE%A2%E6%88%B7%E7%AB%AF%E7%99%BB%E5%BD%95%E7%95%8C%E9%9D%A2.jpg)

Until you connect, the whole page sits behind a full-screen overlay — the pad can't be touched. It holds only the decisions that matter before connecting:

- **Password** — leave empty when the server has none;
- **Controller type** — Xbox 360 (Xbox) / Xbox 360 (Nintendo Switch) / DualShock 4 (PlayStation). The first two create an Xbox 360 device and differ only in button glyphs; the third creates a DualShock 4. Locked once connected — changing it means disconnecting and reconnecting, and the server rebuilds the device in the same slot;
- **Connect**;
- **Fullscreen** — entering fullscreen also asks the browser to lock landscape orientation when it supports it; browsers that don't (iOS Safari) simply stay as they are;
- **Language** — 中文 / English / العربية;
- A "landscape recommended" line under the title — the pad is simply better sideways.

After connecting, the overlay disappears and the pad plus the top bar take over.

### Top bar

Connection state (dot + text), latency, player number `P1`, then profile, vibration, sensitivity (10%–200%), fullscreen, edit and menu.

- **Profile** — each profile keeps its own control positions and visibility, stored per profile and per button style;
- **Vibration** — the server forwards rumble intensity and the phone buzzes;
- **Sensitivity** — applied to the stick axis values, 100% is neutral;
- **Edit** — drag controls around, show touch hitboxes, show/hide individual buttons;
- **Menu** — reset layout, export, import, new profile, delete profile, about.

### Touch behaviour

It should feel like a controller, not a web page:

- the context menu, long-press selection, drag ghosts and page refresh are suppressed — they would interrupt play;
- on screen lock, app switch or window blur, everything held is released and both sticks are re-centered, so you don't come back to a stuck trigger;
- sticks and buttons handle `touchcancel`, so a system interruption can't leave an input pressed.

---

## Project layout

### VGamepadWeb.UI

The phone UI and the front end of the whole project.

**React 19 + Vite 8 + TypeScript**, with **SignalR** (`@microsoft/signalr`) for signaling and **Lucide React** for icons.

Its build output goes straight into `VGamepadWeb.WinForm/wwwroot` and is served by the desktop app — there is no separate front-end server to run.

### VGamepadWeb.Core

The server library.

**.NET 10 + ASP.NET Core**: HTTP hosting, the SignalR hub, WebRTC signaling, session and slot management, and translation of input into virtual gamepad events.

- Virtual gamepads: **ViGEmBus** via [Nefarius.ViGEm.Client](https://github.com/nefarius/ViGEmClient); Windows sees a real Xbox 360 or DualShock 4;
- WebRTC: [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery) for the peer connection, ICE negotiation and data channels.

> ⚠️ The **ViGEmBus** driver must be installed on the host. The app installs the bundled driver silently on first launch and exits with a message if that fails.

### VGamepadWeb.WinForm

The desktop app, **.NET 10 Windows Forms**, wrapping Core: start/stop the service, watch the slots, edit settings, live in the tray, and serve the built page from `wwwroot`.

### VGamepadWeb.Console *(development only)*

An early entry point for running Core from a terminal. Use the WinForm app for real use.

---

## Building from source

```powershell
# 1) Front end: output is written straight into VGamepadWeb.WinForm\wwwroot
cd VGamepadWeb.UI
npm install
npm run build

# 2) Server
cd ..
dotnet build VGamepadWeb.slnx

# 3) Run
dotnet run --project VGamepadWeb.WinForm
```

> After changing front-end sources, run `npm run build` again — otherwise the app still serves the previous bundle.

---

## Requirements

- Windows 10 or later
- [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) (SDK to build, runtime to run)
- [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases) (installed automatically since v2.0.0)
- A phone on the same Wi-Fi / LAN as the PC
- Node.js 20 or later to build the front end

> The page is served over plain **HTTP** on the local network, so no certificates are involved. That also means secure-context features such as Wake Lock are unavailable — for long sessions, raise the phone's screen timeout.

---

## License

Open source under the [MIT License](LICENSE). Use it, modify it, build on it.
