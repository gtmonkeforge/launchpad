import { test, expect } from 'bun:test';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { Connector, validateComputer } from '../src/connector';
const settings = {name:'GORILLA',red:9,green:4,blue:0,turn:'SNAP',turnSpeed:4,microphone:'ALL CHAT'};
test('computer settings reject invalid colors, modes and names', () => {
  expect(validateComputer({...settings,name:'monke'}).name).toBe('MONKE');
  for(const invalid of [{red:10},{blue:1.5},{green:-1},{name:'<script>'},{turnSpeed:0},{turn:'FREE'},{microphone:'OFF'}]) expect(()=>validateComputer({...settings,...invalid})).toThrow();
});
test('connector requires a local discovery token and correlates responses', async () => {
  const directory=await mkdtemp(join(tmpdir(),'connector-test-'));const connector=new Connector();let socket: WebSocket | null = null;
  try {
    await connector.publishDiscovery(directory);
    const discovery=JSON.parse(await readFile(join(directory,'BepInEx/config/launchpad-connector.json'),'utf8'));
    const response=await fetch(discovery.url.replace('ws:','http:'));expect(response.status).toBe(403);
    const originResponse=await fetch(discovery.url.replace('ws:','http:'),{headers:{Origin:'http://example.com','X-Launchpad-Connector':discovery.token}});expect(originResponse.status).toBe(403);
    const NativeWebSocket = WebSocket as unknown as { new(url: string, options: Bun.WebSocketOptions): WebSocket };
    socket=new NativeWebSocket(discovery.url,{headers:{'X-Launchpad-Connector':discovery.token}});
    await new Promise<void>((resolve,reject)=>{socket!.onopen=()=>resolve();socket!.onerror=()=>reject(new Error('Connection failed'));});
    socket.send(JSON.stringify({event:'state',data:{settings,room:'ROOM1'}}));
    await Bun.sleep(10);expect(connector.room).toBe('ROOM1');expect(connector.settings?.red).toBe(9);
    socket.onmessage=event=>{const packet=JSON.parse(String(event.data));socket!.send(JSON.stringify({id:packet.id,data:{settings}}));};
    expect((await connector.command('computer-set',settings)).settings.name).toBe('GORILLA');
    socket.onmessage=event=>{const packet=JSON.parse(String(event.data));socket!.send(JSON.stringify({id:packet.id,error:'Game rejected name'}));};
    await expect(connector.command('computer-set',settings)).rejects.toThrow('Game rejected name');
    socket.onmessage=event=>{const packet=JSON.parse(String(event.data));socket!.send(JSON.stringify({id:packet.id,data:{settings:{...settings,red:99}}}));};
    await expect(connector.command('computer-get',{})).rejects.toThrow('invalid Computer settings');
    socket.close();await Bun.sleep(10);expect(connector.connected).toBe(false);expect(connector.room).toBeNull();
  } finally {socket?.close();connector.stop();await rm(directory,{recursive:true,force:true});}
});
