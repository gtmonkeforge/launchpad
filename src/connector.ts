import { join } from 'node:path';
import { atomicJson } from './config';
import type { ServerWebSocket } from 'bun';
export interface ComputerSettings { name: string; red: number; green: number; blue: number; turn: string; turnSpeed: number; microphone: string }
export function validateComputer(value: any): ComputerSettings {
  if (!value || typeof value.name !== 'string' || !/^[a-z0-9]{1,12}$/i.test(value.name)) throw new Error('Use a name of 1–12 letters or numbers');
  for (const key of ['red','green','blue']) if (!Number.isInteger(value[key]) || value[key]<0 || value[key]>9) throw new Error('Color values must be integers from 0 to 9');
  if (!['NONE','SNAP','SMOOTH'].includes(value.turn) || !Number.isInteger(value.turnSpeed) || value.turnSpeed<1 || value.turnSpeed>9) throw new Error('Choose a valid turn mode and speed');
  if (!['ALL CHAT','PUSH TO TALK','PUSH TO MUTE'].includes(value.microphone)) throw new Error('Choose a valid microphone mode');
  return { name: value.name.toUpperCase(), red:value.red, green:value.green, blue:value.blue, turn:value.turn, turnSpeed:value.turnSpeed, microphone:value.microphone };
}
export class Connector {
  private socket: ServerWebSocket<{}> | null = null;
  private pending = new Map<string, { resolve: (data: any) => void; reject: (error: Error) => void; timer: ReturnType<typeof setTimeout> }>();
  settings: ComputerSettings | null = null;
  private roomValue: string | null = null;
  private lastStateAt = 0;
  get room() { return Date.now() - this.lastStateAt < 20000 ? this.roomValue : null; }
  readonly token = crypto.randomUUID();
  private server: ReturnType<typeof Bun.serve>;
  constructor() {
    this.server = Bun.serve<{}>({ hostname: '127.0.0.1', port: 0, fetch: (request, server) => {
      const url = new URL(request.url);
      if (request.headers.has('Origin') || url.pathname !== '/gt-mod' || request.headers.get('X-Launchpad-Connector') !== this.token) return new Response('Forbidden', { status:403 });
      if (this.socket) return new Response('Connector already connected', { status:409 });
      return server.upgrade(request, { data:{} }) ? undefined : new Response('WebSocket required', { status:400 });
    }, websocket: {
      maxPayloadLength: 65536,
      open: socket => { if (this.socket) { socket.close(1008, 'Connector already connected'); return; } this.socket = socket; },
      close: socket => { if (socket === this.socket) { this.socket=null; this.settings=null; this.roomValue=null; this.lastStateAt=0; for (const entry of this.pending.values()) { clearTimeout(entry.timer); entry.reject(new Error('Connector disconnected')); } this.pending.clear(); } },
      message: (socket, message) => { if (socket !== this.socket) return; try {
        const packet = JSON.parse(String(message));
        if (packet.event === 'state') { this.settings = validateComputer(packet.data.settings); this.lastStateAt = Date.now(); this.roomValue = typeof packet.data.room === 'string' && /^[a-z0-9]{1,12}$/i.test(packet.data.room) ? packet.data.room : null; }
        if (packet.id && this.pending.has(packet.id)) { const entry = this.pending.get(packet.id)!; clearTimeout(entry.timer); this.pending.delete(packet.id); if (packet.error) entry.reject(new Error(String(packet.error))); else { try { if (packet.data?.settings) this.settings=validateComputer(packet.data.settings); entry.resolve(packet.data); } catch { entry.reject(new Error('Connector returned invalid Computer settings')); } } }
      } catch {} },
    }});
  }
  async publishDiscovery(game: string) {
    if (game) await atomicJson(join(game, 'BepInEx/config/launchpad-connector.json'), { url:`ws://127.0.0.1:${this.server.port}/gt-mod`, token:this.token }, 0o600);
  }
  stop() { this.socket?.close(); this.server.stop(true); }
  get connected() { return !!this.socket; }
  command(event: string, data: unknown): Promise<any> {
    if (!this.socket) return Promise.reject(new Error('Launch Gorilla Tag with Launchpad Connector first'));
    const id=crypto.randomUUID();
    return new Promise((resolve,reject) => {
      const timer=setTimeout(()=>{this.pending.delete(id);reject(new Error('Connector did not respond. Try again.'));},10000);
      this.pending.set(id,{resolve,reject,timer}); this.socket!.send(JSON.stringify({id,event,data}));
    });
  }
  notify(data: unknown) { return this.command('notification',data); }
}
