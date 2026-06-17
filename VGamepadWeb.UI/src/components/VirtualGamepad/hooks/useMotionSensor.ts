import { useState, useEffect, useCallback, useRef } from 'react';

export interface MotionData {
  accel: { x: number; y: number; z: number };
  gyro: { x: number; y: number; z: number };
}

export function useMotionSensor() {
  const [isSupported, setIsSupported] = useState(false);
  const [isActive, setIsActive] = useState(false);
  const [needsPermission, setNeedsPermission] = useState(true);
  const dataRef = useRef<MotionData>({ accel: { x: 0, y: 0, z: 0 }, gyro: { x: 0, y: 0, z: 0 } });

  const requestPermission = useCallback(async (): Promise<boolean> => {
    const devOrientation = DeviceOrientationEvent as unknown as {
      requestPermission?: () => Promise<'granted' | 'denied'>;
    };
    if (typeof devOrientation.requestPermission === 'function') {
      try {
        const state = await devOrientation.requestPermission();
        const granted = state === 'granted';
        setNeedsPermission(!granted);
        return granted;
      } catch {
        return false;
      }
    }
    setNeedsPermission(false);
    return true;
  }, []);

  const start = useCallback(() => setIsActive(true), []);
  const stop = useCallback(() => setIsActive(false), []);

  useEffect(() => {
    const supported = typeof window !== 'undefined' && 'DeviceMotionEvent' in window;
    setIsSupported(supported);

    if (!supported) return;

    const handleMotion = (e: DeviceMotionEvent) => {
      if (!isActive) return;
      const rot = e.rotationRate;
      const acc = e.accelerationIncludingGravity;
      if (rot && acc) {
        dataRef.current = {
          gyro: {
            x: rot.alpha ?? 0,
            y: rot.beta ?? 0,
            z: rot.gamma ?? 0,
          },
          accel: {
            x: acc.x ?? 0,
            y: acc.y ?? 0,
            z: acc.z ?? 0,
          },
        };
      }
    };

    window.addEventListener('devicemotion', handleMotion);
    return () => window.removeEventListener('devicemotion', handleMotion);
  }, [isActive]);

  return { isSupported, needsPermission, requestPermission, isActive, start, stop, dataRef };
}
