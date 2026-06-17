# VGamepadWeb

Turn your phone or any browser into a wireless gamepad for your PC — no cables, no extra hardware, just open the page and start playing.

The idea is simple: you run a small server on your Windows machine, connect your phone to the same network (or even remotely), and use the web interface as a controller. Inputs are sent in real-time using WebRTC and WebSockets, so the delay is as low as possible.

---

## How It Works

When you launch the server on your PC, it opens a local web page address. You visit that address on your phone, the virtual gamepad appears, and from that point your phone acts as a controller. The PC side reads the inputs and translates them into actual gamepad signals that Windows recognizes — so any game that supports a controller will just work.

---

## Project Structure

The project is split into three parts, each with its own responsibility:

### VGamepadWeb.UI
The web interface — what you see and touch on your phone or browser.

Built with **React 19** and **Vite 8**, written in **TypeScript**. The UI connects to the server using **SignalR** (`@microsoft/signalr`) which handles the WebSocket connection. Icons are from **Lucide React**.

The interface is designed to be usable on a mobile screen with touch controls — things like analog sticks, buttons, triggers, and a D-pad.

**Tech used:** React, Vite, TypeScript, SignalR, Lucide React
<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/f3e60cdb-276a-44bc-8dca-aacb090c82b9" />
<img width="1920" height="1080" alt="editLayout" src="https://github.com/user-attachments/assets/0cb06449-af10-4a9c-8d9b-f63a2ae3a883" />

---

### VGamepadWeb.Core
The core server library — the part that actually does the work.

Built on **.NET 10** with **ASP.NET Core**. It runs an HTTP server, handles WebSocket and WebRTC signaling, manages connected players, and translates their inputs into virtual gamepad events.

For virtual gamepad emulation, it uses the **ViGEmBus** driver via the [Nefarius.ViGEm.Client](https://github.com/nefarius/ViGEmClient) library. This makes the virtual controller appear to Windows as a real Xbox 360 controller.

For WebRTC, it uses [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery), a pure .NET WebRTC and SIP library that handles the peer connection, ICE negotiation, and data channels.

**Tech used:** .NET 10, ASP.NET Core, WebSockets, WebRTC (SIPSorcery), ViGEmBus (Nefarius.ViGEm.Client)

> ⚠️ **Note:** ViGEmBus driver must be installed on the host PC for the gamepad emulation to work. You can find it [here](https://github.com/nefarius/ViGEmBus/releases).

---

### VGamepadWeb.WinForm
The desktop launcher — a Windows Forms application that wraps around the Core server.

Built with **.NET 10 (Windows Forms)**. It gives you a simple GUI to start and stop the server, see which players are connected, and tweak settings without touching any config files or command line.

It also serves the built UI files (`wwwroot`) so everything is bundled in one place — you don't need to run the UI separately.

**Tech used:** .NET 10, Windows Forms

---

### VGamepadWeb.Console *(development only)*
This one was just for early development. It was a quick way to run the Core server from a terminal to test things without having to open the WinForm app every time. It's not meant to be used as a real entry point — the WinForm app is the proper way to run the server.

---

## Requirements

- Windows 10 or later
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases) installed on the PC (in v2.0.0 or later installed automatically)
- A phone or browser on the same network (or configured for remote access)

---

## License

This project is open-source under the [MIT License](LICENSE).  
Feel free to use it, modify it, or build on top of it.
