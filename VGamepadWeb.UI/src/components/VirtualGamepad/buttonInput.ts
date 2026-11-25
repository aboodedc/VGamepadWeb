// Input ownership is separate from transport: every change is sent synchronously.
export function createButtonInput(send: (command: string, pressed: boolean) => void,
  changed: (active: Set<string>) => void = () => {}) {
  const sources = new Map<string, { control: string; command: string }>();
  const counts = new Map<string, number>();
  const publish = () => changed(new Set([...sources.values()].map(source => source.control)));
  const release = (control: string, source: string) => {
    const key = `${control}:${source}`;
    const held = sources.get(key);
    if (!held) return;
    sources.delete(key);
    const count = (counts.get(held.command) ?? 1) - 1;
    if (count === 0) { counts.delete(held.command); send(held.command, false); }
    else counts.set(held.command, count);
    publish();
  };
  return {
    press(control: string, command: string, source: string) {
      const key = `${control}:${source}`;
      if (sources.has(key)) return;
      sources.set(key, { control, command });
      const count = counts.get(command) ?? 0;
      counts.set(command, count + 1);
      if (count === 0) send(command, true);
      publish();
    },
    release,
    releaseControl(control: string) {
      for (const [key, held] of [...sources]) {
        if (held.control === control) release(control, key.slice(control.length + 1));
      }
    },
    releaseAll() {
      if (sources.size === 0) return;
      const commands = [...counts.keys()];
      sources.clear(); counts.clear();
      for (const command of commands) send(command, false);
      publish();
    },
  };
}
