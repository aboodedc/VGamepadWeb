import { useState, useRef, useEffect, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type { TranslationSet } from '../i18n';

export interface SlotInfo {
  slot: number;
  occupied: boolean;
  locked: boolean;
  name: string;
}

interface UseGamepadConnectionProps {
  serverUrl: string;
  serverPassword?: string;
  enableVib: boolean;
  sensitivity: number;
  t: TranslationSet;
}

const CLIENT_ID_KEY = 'gamepad_client_id';
const HUB_PATH = '/gamepadhub';

const createClientId = () => {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `${Date.now().toString(16)}-${Math.random().toString(16).slice(2)}`;
};

const getClientId = () => {
  const stored = localStorage.getItem(CLIENT_ID_KEY);
  if (stored) return stored;
  const id = createClientId();
  localStorage.setItem(CLIENT_ID_KEY, id);
  return id;
};

export const useGamepadConnection = ({
  serverUrl, serverPassword = '', enableVib, sensitivity, t
}: UseGamepadConnectionProps) => {
  const [connStatus, setConnStatus] = useState<'off' | 'ing' | 'on'>('off');
  const [latency, setLatency] = useState<number | null>(null);
  const [controllerId, setControllerId] = useState<number | null>(null);
  const [slots, setSlots] = useState<SlotInfo[] | null>(null);
  const [slotSelecting, setSlotSelecting] = useState(false);
  const [recommended, setRecommended] = useState<number | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const connRef = useRef<signalR.HubConnection | null>(null);
  const pcRef = useRef<RTCPeerConnection | null>(null);
  const dcRef = useRef<RTCDataChannel | null>(null);
  const pingRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const authFailedRef = useRef(false);

  // 语言可能在连接期间切换，事件回调里读最新文案。
  const tRef = useRef(t);
  tRef.current = t;

  // 服务端只发稳定码，文案一律在这里翻译。
  const reasonText = (reason: string) => {
    const tr = tRef.current;
    switch (reason) {
      case 'slot_locked': return tr.reasonSlotLocked;
      case 'slot_taken': return tr.reasonSlotTaken;
      case 'no_free_slot': return tr.reasonNoFreeSlot;
      case 'slot_invalid': return tr.reasonSlotInvalid;
      case 'kicked_by_admin': return tr.reasonKickedByAdmin;
      default: return tr.connectFailed;
    }
  };

  const stopPing = () => {
    if (pingRef.current) { clearInterval(pingRef.current); pingRef.current = null; }
  };

  const closePeer = () => {
    stopPing();
    const dc = dcRef.current;
    dcRef.current = null;
    dc?.close();
    const pc = pcRef.current;
    pcRef.current = null;
    pc?.close();
    setLatency(null);
    setControllerId(null);
  };

  const resetSession = async () => {
    closePeer();
    const c = connRef.current;
    connRef.current = null;
    if (c) {
      try { await c.stop(); } catch (err) { console.error(err); }
    }
    setSlots(null);
    setSlotSelecting(false);
    setRecommended(null);
  };

  const updateLiveSetting = (key: string, val: string) => {
    if (connStatus === 'on' && connRef.current?.state === signalR.HubConnectionState.Connected) {
      connRef.current.invoke('UpdateSetting', key, val).catch(console.error);
    }
  };

  useEffect(() => { updateLiveSetting('VibrationEnabled', enableVib.toString()); }, [enableVib, connStatus]);
  useEffect(() => { updateLiveSetting('JoystickSensitivity', sensitivity.toString()); }, [sensitivity, connStatus]);

  // 关页面时尽量主动释放槽位；服务端 OnDisconnectedAsync 仍会兜底。
  useEffect(() => {
    const release = () => {
      const c = connRef.current;
      if (c && c.state === signalR.HubConnectionState.Connected) {
        c.invoke('Release').catch(() => {});
      }
    };
    window.addEventListener('pagehide', release);
    return () => window.removeEventListener('pagehide', release);
  }, []);

  useEffect(() => {
    return () => {
      if (pingRef.current) clearInterval(pingRef.current);
      dcRef.current?.close();
      pcRef.current?.close();
      connRef.current?.stop();
    };
  }, []);

  const bindDataChannel = (dc: RTCDataChannel) => {
    dc.onopen = () => {
      if (dcRef.current !== dc) return;
      console.log('[WebRTC] Data Channel opened — ready to send gamepad data!');
      setConnStatus('on');
      stopPing();
      pingRef.current = setInterval(() => {
        if (dcRef.current?.readyState === 'open') {
          dcRef.current.send(`Ping:${Date.now()}`);
        }
      }, 2000);
    };
    dc.onclose = () => {
      if (dcRef.current !== dc) return;
      console.log('[WebRTC] Data Channel closed');
      stopPing();
      setLatency(null);
      setControllerId(null);
      setConnStatus('off');
    };
    dc.onmessage = (event) => {
      const msg = typeof event.data === 'string' ? event.data : '';
      if (msg.startsWith('Pong:')) {
        const sent = parseInt(msg.substring(5), 10);
        if (!isNaN(sent)) setLatency(Date.now() - sent);
      } else {
        const parts = msg.split(':');
        if (parts[0] === 'V') {
          const intensity = parseInt(parts[1], 10);
          if (navigator.vibrate) navigator.vibrate(intensity > 0 ? 100 : 0);
        } else if (parts[0] === 'ID') {
          const id = parseInt(parts[1], 10);
          if (!isNaN(id)) setControllerId(id);
        }
      }
    };
  };

  // 服务端记得「这个设备码上次坐的是哪个槽」，拿回来在选槽面板里标出来，方便一键接回。
  const refreshRecommended = async (c: signalR.HubConnection) => {
    try {
      const slot = await c.invoke<number | null>('RecommendSlot', getClientId());
      if (connRef.current === c) setRecommended(typeof slot === 'number' ? slot : null);
    } catch {
      // 老服务端没有这个方法：退化成不推荐。
      setRecommended(null);
    }
  };

  // 第一步：只建 SignalR 连接，拿到服务端推来的槽位列表后等用户选槽。
  const connect = async () => {
    await resetSession();
    setConnStatus('ing');
    setNotice(null);
    authFailedRef.current = false;

    let url = serverUrl.trim().replace(/\/$/, '');
    if (!url.endsWith(HUB_PATH)) url += HUB_PATH;

    const c = new signalR.HubConnectionBuilder()
      .withUrl(url, { skipNegotiation: true, transport: signalR.HttpTransportType.WebSockets })
      .build();

    c.onclose(() => {
      if (connRef.current !== c) return;
      connRef.current = null;
      closePeer();
      setSlots(null);
      setSlotSelecting(false);
      setConnStatus('off');
    });

    c.on('SlotChanged', (list: SlotInfo[]) => setSlots(list));

    c.on('SlotUnavailable', (reason: string) => {
      closePeer();
      setNotice(reasonText(reason));
      setConnStatus('ing');
      setSlotSelecting(true);
    });

    c.on('Kicked', (reason: string) => {
      const text = reasonText(reason);
      void (async () => {
        connRef.current = null;
        closePeer();
        setSlots(null);
        setSlotSelecting(false);
        setConnStatus('off');
        setNotice(text);
        try { await c.stop(); } catch (err) { console.error(err); }
      })();
    });

    c.on('ReceiveAnswer', async (sdpAnswer: string) => {
      const pc = pcRef.current;
      if (!pc) return;
      try { await pc.setRemoteDescription({ type: 'answer', sdp: sdpAnswer }); }
      catch (err) { console.error('[WebRTC] Failed to set remote description:', err); }
    });

    c.on('ReceiveIceCandidate', async (iceCandidateJson: string) => {
      const pc = pcRef.current;
      if (!pc) return;
      try { await pc.addIceCandidate(JSON.parse(iceCandidateJson)); }
      catch (err) { console.error('[WebRTC] Failed to add ICE candidate:', err); }
    });

    c.on('AuthFailed', () => {
      authFailedRef.current = true;
      setSlotSelecting(false);
      setNotice(tRef.current.authFailed);
    });

    try {
      await c.start();
      connRef.current = c;
      setConnStatus('ing');
      setSlotSelecting(true);
      void refreshRecommended(c);
    } catch (err) {
      console.error(err);
      setSlotSelecting(false);
      setConnStatus('off');
      if (!authFailedRef.current) setNotice(tRef.current.connectFailed);
    }
  };

  // 第二步：用户点了某个空闲槽位，才建立 WebRTC 并带槽号发 offer。
  const selectSlot = async (slot: number) => {
    const c = connRef.current;
    if (!c || c.state !== signalR.HubConnectionState.Connected) return;
    if (pcRef.current) return;
    setSlotSelecting(false);
    setNotice(null);

    const pc = new RTCPeerConnection();
    pcRef.current = pc;

    const dc = pc.createDataChannel('gamepad', { ordered: false, maxRetransmits: 0 });
    dcRef.current = dc;
    bindDataChannel(dc);

    pc.onicecandidate = (event) => {
      if (event.candidate && connRef.current?.state === signalR.HubConnectionState.Connected) {
        connRef.current.invoke('SendIceCandidate', JSON.stringify(event.candidate.toJSON())).catch(console.error);
      }
    };

    pc.onconnectionstatechange = () => {
      // closePeer 会先清掉引用再 close，迟到的 closed 事件不该反过来改连接状态。
      if (pcRef.current !== pc) return;
      if (pc.connectionState === 'failed' || pc.connectionState === 'disconnected' || pc.connectionState === 'closed') {
        console.log('[WebRTC] Connection state:', pc.connectionState);
        setLatency(null);
        setConnStatus('off');
      }
    };

    try {
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      // controllerType 位置只为老服务端兼容，固定 0；槽号才是真正的选择结果。
      await c.invoke('SendOffer', offer.sdp, 0, enableVib, sensitivity, serverPassword, getClientId(), slot);
    } catch (err) {
      console.error(err);
      closePeer();
      setNotice(tRef.current.connectFailed);
      setSlotSelecting(true);
    }
  };

  const disconnect = async () => {
    await resetSession();
    setConnStatus('off');
    setNotice(null);
  };

  const sendButton = useCallback((btn: string, pressed: boolean) => {
    const dc = dcRef.current;
    if (dc?.readyState === 'open') {
      dc.send(`B:${btn}:${pressed ? '1' : '0'}`);
    }
  }, []);

  const sendJoystick = useCallback((stick: string, x: number, y: number) => {
    const dc = dcRef.current;
    if (dc?.readyState === 'open') {
      dc.send(`J:${stick}:${x}:${y}`);
    }
  }, []);

  return {
    connStatus, latency, controllerId, slots, slotSelecting, recommended, notice,
    connect, selectSlot, disconnect, dismissNotice: () => setNotice(null),
    sendButton, sendJoystick
  };
};
