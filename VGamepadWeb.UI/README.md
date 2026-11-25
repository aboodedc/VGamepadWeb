# 🎮 VGamepadWeb.UI

> **IMPORTANT NOTE:** This repository contains **ONLY the UI / Client frontend**. For the virtual gamepad to function and emulate physical controllers in Windows (via ViGEmBus), you **must** run it alongside its dedicated **C# Backend Server**.

**VGamepadWeb** is an open-source project that transforms any web browser (on a smartphone, tablet, or secondary PC) into a professional, ultra-fast, and fully customizable virtual gamepad for playing PC games.

## ✨ Features

- **Ultra-Low Latency:** Uses **WebRTC Data Channels** to transmit button presses and joystick movements instantly, bypassing slower HTTP/WebSocket protocols for true real-time gaming.
- **Multiple Themes:** Choose between Xbox, PlayStation, and Nintendo layouts. Features authentic, high-quality controller glyphs powered by [PromptFont](https://shinmera.com/docs/promptfont).
- **Fully Customizable Layout:**
  - **Drag & Drop:** Move buttons and joysticks anywhere on the screen.
  - **Resize & Reshape:** Change the size of any control and toggle between circular or rectangular button shapes.
  - **Precision Nudge:** Fine-tune control positions pixel by pixel.
  - **Toggle Visibility:** Hide buttons you don't need for specific games.
  - **Show Hitboxes:** Visually reveal touch areas while editing to ensure perfect alignment.
- **Multiplayer Ready:** Supports dynamic Player ID assignment (P1, P2, P3, P4) synced directly from the backend server.
- **Haptic Feedback:** Receives live vibration signals over WebRTC and triggers your device's vibration motor instantly.
- **Profile Management:** Save multiple layouts for different games. Import and export profiles as JSON files to share them easily.

## 🛠️ Tech Stack

- **React + TypeScript:** For a robust, scalable, and responsive user interface.
- **Vite:** For ultra-fast development and optimized production builds.
- **WebRTC (`RTCDataChannel`):** Core technology for lag-free input transmission.
- **SignalR:** Used for initial WebRTC signaling (SDP/ICE candidate exchange) and live settings synchronization.
- **Vanilla CSS:** Custom Glassmorphism UI that feels premium and responsive, optimized for touch events without ghost-clicks or delays.

## 🚀 How to Run

1. Make sure you have [Node.js](https://nodejs.org/) installed.
2. Open your terminal in the project directory and install the dependencies:
   ```bash
   npm install
   ```
3. Start the development server:
   ```bash
   npm run dev
   ```
4. A local link will be generated (e.g., `http://localhost:5173`). Open this link on your smartphone's browser (ensure both devices are on the same network).
5. **Start your C# Backend Server**, open the settings modal (⚙️) in the UI, and enter the server's IP address to connect and start playing!

## Local development and the bundled UI

From the repository root, run `dotnet run --project .\VGamepadWeb.WinForm\VGamepadWeb.WinForm.csproj`,
then click **Start Server**. The app serves the bundled UI at `http://localhost:5000`.
On your phone, use `http://YOUR_PC_IP:5000` on the same Wi-Fi network.

For frontend development, run `npm ci` and `npm run devn` in this directory.
Use the network URL shown by Vite. In **Connection**, uncheck **Use same server as website URL**
and enter `http://YOUR_PC_IP:5000`. `npm run build` updates the WinForm `wwwroot`;
rebuild/restart the WinForm app to copy those files into its output folder.

## iPhone screen modes

The Fullscreen button uses the native API only when the browser exposes it. A rejection is displayed;
the UI never reports native fullscreen merely because it was requested. **Screen options** offers
**Game mode**, which hides the application toolbar while browser bars may remain visible.
The small bottom button restores the toolbar.

For standalone use, open the same server address in Safari, choose **Share → Add to Home Screen**,
enable **Open as Web App** if offered, and launch from that icon. Standalone is identified separately
from native fullscreen. No service worker or offline cache is installed. Behavior on local HTTP and
browser/system chrome must be checked on the target iPhone and iOS version. Browser and standalone
storage may differ: export/import your layout if you need to transfer it.

## Clone Hero Drums and the generic editor

Choose **Connection → Controller layout → Clone Hero Drum**, next to Xbox, PlayStation and Nintendo.
It has its own six controls: four colored drum pads, an orange horizontal kick and a small `☰` Start.
Default Xbox commands are red `B`, yellow `Y`, blue `X`, green `A`, kick `LB`, and Start `Start`.
Bind these gamepad inputs inside Clone Hero as needed. The preset does not emulate a different device type.

In **Edit**, select a control. Buttons have independent **Width** and **Height**, optional linked
proportions, a **Gamepad command** selector, and either controller icons or editable colored pads.
Joysticks retain a diameter on the other controller layouts. Hidden controls can be restored with
**Buttons**, which lists only the selected controller's controls. Each controller layout has separate
saved settings per profile. Existing battery profiles migrate their customized pads to the dedicated
layout and gain Start; their original data is retained when switching back to the Xbox layout.
Dimensions in a responsive drums profile are reference-canvas units; the renderer scales them
to the available screen without overwriting their saved values when the phone rotates.

**Edit → Button icon** offers Drum Pad (simple), Drum Pad (with sticks), Kick and Cymbal SVGs for any button. Select **None (text only)**
to remove it, or clear **Show button text** for an icon alone. New drums layouts include pad and kick
icons; existing saved layouts remain text-only until edited. Icon choices and text visibility are
included in layout storage and JSON export/import. Icons do not receive pointer events. The simple
Drums layout retains its existing six controls; cymbals belong to the separate Pro Drums layout.

## Clone Hero Pro Drums

Choose **Connection → Controller layout → Clone Hero Pro Drums**. This is a dedicated layout with
four drum pads, three smaller cymbals above their corresponding pads, a wide orange kick below,
and Start. All nine visual controls use the existing editor, icons and immediate input transport.
The default touch rectangles have gaps between them, including cymbals versus pads. Landscape
is recommended; toolbar-free Game mode provides more room on shorter phone screens.

| Control | Default gamepad command |
| --- | --- |
| Red pad | B |
| Yellow pad | Y |
| Blue pad | X |
| Green pad | A |
| Yellow cymbal | RB |
| Blue cymbal | LS (left stick click) |
| Green cymbal | RS (right stick click) |
| Kick pedal | LB |
| Start | Start |

These nine assignments are distinct digital buttons already supported by the Xbox 360 backend.
Its current map accepts 15 named buttons (including Guide), plus LT/RT trigger values. The Pro
defaults use neither Guide nor triggers nor D-pad directions. They do not consume joystick axes.
Bind each input to its intended pad/cymbal/kick action and select the appropriate drum mode inside
Clone Hero. This frontend layout continues to emulate the selected gamepad type; it does not
identify itself as a native drum kit and does not send velocity or pressure data.

Pro settings use `gamepad_layout_v2_<profile>_pro-drums`, independently of the simple layout's
`gamepad_layout_v2_<profile>_drums`. Switching, importing/exporting JSON, hiding, resizing, remapping
and resetting Pro controls do not change the simple preset or its saved configurations. Importing
a Pro export selects Pro Drums automatically; **Reset** restores only the selected layout.
The input system supports independent simultaneous notes; the actual number of concurrent fingers
depends on the phone/browser. Physical iPhone ergonomics and final Clone Hero bindings need device
verification.

`npm test` checks input ownership, immediate protocol-compatible messages, multitouch cancellation,
legacy storage, import validation, drums sizing, and presentation capability detection. Native
fullscreen, browser rejection, standalone, editor interactions and remapped messages were also
checked in an automated Chromium session. Safari/Chrome on a physical iPhone and final Clone Hero
playability still require device testing.

## 📝 License
Please review the `LICENSE` file for details on commercial and personal usage rights.
