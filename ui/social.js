/** Shared social panels for the website and Launchpad. */
export async function renderSocial(root, api, user, view, toast = message => {}, origin = '') {
 const esc = v => String(v ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 const field=(id,label,extra='')=>`<div class="field"><label for="${id}">${label}</label><input id="${id}" required ${extra}></div>`;
 const send=(path,data={},method='POST')=>api('/api/v1'+path,{method,headers:{'Content-Type':'application/json'},body:JSON.stringify(data)});
 const run=fn=>async e=>{e?.preventDefault();try{await fn();}catch(error){toast(error.message);}};
 let alive=true; let timer;
 let marker;
 const observer=new MutationObserver(()=>{if(!root.contains(marker)){alive=false;clearTimeout(timer);observer.disconnect();}});
 root.innerHTML=`<div data-social-root><div class="heading"><h1>${esc(view[0].toUpperCase()+view.slice(1))}</h1></div><div id="social-body" class="social-body"></div></div>`;
 marker=root.querySelector('[data-social-root]');
 observer.observe(root,{childList:true,subtree:true});
 const body=root.querySelector('#social-body');
 if(!user){body.innerHTML='<section class="panel social-panel"><h2>Connect with your people</h2><p>Sign in with MonkeForge to use friends, chat, and publishing groups.</p></section>';return;}
 const input=id=>body.querySelector('#'+id).value;
 if(view==='friends'){
  const data=await api('/api/v1/friends');if(!alive)return;
  const rows=(items,kind)=>items.map(u=>`<div class="social-row"><div><a href="${origin}/profile/${encodeURIComponent(u.id)}">${esc(u.name)}</a><small>@${esc(u.username)}${u.room_code?' · Room '+esc(u.room_code):''}</small></div><div class="actions">${kind==='incoming'?`<button class="button small" data-accept="${esc(u.id)}">Accept</button>`:''}<button class="button ghost small" data-remove="${esc(u.id)}">${kind==='friend'?'Remove':kind==='incoming'?'Decline':'Cancel'}</button></div></div>`).join('');
  body.innerHTML=`<form id="friend-form" class="panel social-panel">${field('friend-user','Username, ID, or custom ID','maxlength="100" placeholder="@username or ID"')}<button class="button">Add friend</button></form><section class="panel social-panel"><h2>Room visibility</h2><p>Your room is visible only to accepted friends while Gorilla Tag and Launchpad are connected.</p><label for="visibility">Who can see your room?</label><select id="visibility"><option value="none">None</option><option value="friends">Friends</option></select></section><section class="panel social-panel"><h2>Requests</h2>${rows(data.requests,'incoming')||'<p>No incoming requests.</p>'}${rows(data.sent,'sent')}</section><section class="panel social-panel"><h2>Your friends</h2>${rows(data.friends,'friend')||'<p>Add a friend to get started.</p>'}</section><button id="social-refresh" class="button subtle">Refresh</button>`;
  body.querySelector('#visibility').value=data.visibility;
  body.querySelector('#visibility').onchange=run(async()=>{await send('/presence',{visibility:input('visibility')},'PUT');toast('Room visibility saved.');});
  body.querySelector('#friend-form').onsubmit=run(async()=>{await send('/friends',{user_id:input('friend-user')});await renderSocial(root,api,user,view,toast);});
  body.querySelectorAll('[data-accept]').forEach(b=>b.onclick=run(async()=>{await send('/friends/'+encodeURIComponent(b.dataset.accept),{},'PUT');await renderSocial(root,api,user,view,toast);}));
  body.querySelectorAll('[data-remove]').forEach(b=>b.onclick=run(async()=>{await send('/friends/'+encodeURIComponent(b.dataset.remove),{},'DELETE');await renderSocial(root,api,user,view,toast);}));
  body.querySelector('#social-refresh').onclick=run(()=>renderSocial(root,api,user,view,toast));
 } else if(view==='groups'){
  const data=await api('/api/v1/groups');if(!alive)return;
  body.innerHTML=`<form id="create-group" class="panel social-panel"><h2>Create a publishing group</h2><p>Up to 20 members can publish under one group profile. Members use their own accounts.</p>${field('group-name','Group name','minlength="3" maxlength="60"')}<button class="button">Create group</button></form><div id="group-list"></div>`;
  const list=body.querySelector('#group-list');
  for(const group of data.items){const profile=await api('/api/v1/users/'+encodeURIComponent(group.id));if(!alive)return;const section=document.createElement('section');section.className='panel social-panel';section.innerHTML=`<h2><a href="${origin}/profile/${encodeURIComponent(group.id)}">${esc(group.name)}</a></h2><p>${profile.members.length} / 20 members · ${esc(group.role)}</p>${profile.members.map(m=>`<div class="social-row"><a href="${origin}/profile/${encodeURIComponent(m.id)}">${esc(m.name)}</a><span>${esc(m.role)}</span>${group.role==='owner'&&m.role!=='owner'?`<button class="button ghost small" data-member="${esc(m.id)}">Remove</button>`:''}</div>`).join('')}${group.role==='owner'?`<form class="member-form">${field('member-'+group.id,'Add member by username or ID')}<button class="button subtle">Add member</button></form>`:''}<a class="button subtle" href="${origin}/upload">Publish a mod</a>`;list.append(section);
   section.querySelector('.member-form')?.addEventListener('submit',run(async()=>{await send('/groups/'+group.id+'/members',{user_id:input('member-'+group.id)});await renderSocial(root,api,user,view,toast);}));
   section.querySelectorAll('[data-member]').forEach(b=>b.onclick=run(async()=>{await send('/groups/'+group.id+'/members/'+encodeURIComponent(b.dataset.member),{},'DELETE');await renderSocial(root,api,user,view,toast);}));
  }
  body.querySelector('#create-group').onsubmit=run(async()=>{await send('/groups',{name:input('group-name')});await renderSocial(root,api,user,view,toast);});
 } else {
  body.innerHTML=`<form id="start-chat" class="panel social-panel">${field('chat-user','Start a chat by username or ID','maxlength="100" placeholder="@username or ID"')}<button class="button">Start chat</button></form><div class="social-chat"><section class="panel social-panel"><h2>Conversations</h2><div id="conversations"></div></section><section class="panel social-panel"><div id="chat-header"><h2>Select a conversation</h2></div><div id="messages" class="social-messages" aria-live="polite"></div><form id="send-message" hidden><label for="message-body">Message</label><textarea id="message-body" maxlength="4000" rows="3" required></textarea><button class="button">Send</button><p id="chat-status"></p></form></section></div>`;
  let selected=null, conversations=[], loadVersion=0, history=[], nextBefore=null, historyConversation=null;
  const load=async()=>{const ticket=++loadVersion;const data=await api('/api/v1/chat');if(!alive || ticket!==loadVersion)return;conversations=data.items;body.querySelector('#conversations').innerHTML=data.items.map(c=>`<button class="social-conversation ${selected===c.id?'active':''}" data-conversation="${esc(c.id)}">${esc(c.name)}<small>${c.group_id?'Group channel':c.status==='pending'?(c.incoming?'Message request':'Awaiting approval'):c.status==='declined'?'Request declined':'Direct message'}</small></button>`).join('')||'<p>Your conversations will appear here.</p>';body.querySelectorAll('[data-conversation]').forEach(b=>b.onclick=run(async()=>{selected=b.dataset.conversation;await load();}));
   const c=conversations.find(c=>c.id===selected);if(!c)return;
   const messages=await api('/api/v1/chat/'+c.id+'/messages');if(!alive || ticket!==loadVersion)return;
   if(historyConversation!==c.id){history=[];nextBefore=null;historyConversation=c.id;}
   if(!history.length)nextBefore=messages.next_before;
   const recent=new Map([...history,...messages.items].map(m=>[m.id,m]));history=[...recent.values()].sort((a,b)=>a.id-b.id);
   body.querySelector('#chat-header').innerHTML=`<h2>${esc(c.name)}</h2>${c.status==='pending'&&c.incoming?'<p>Approve this request before replying.</p><button class="button" id="accept-chat">Accept</button> <button class="button ghost" id="decline-chat">Decline</button>':''}`;
   for(const action of ['accept','decline'])body.querySelector('#'+action+'-chat')?.addEventListener('click',run(async()=>{await send('/chat/'+c.id+'/'+action);await load();}));
   const box=body.querySelector('#messages'),nearBottom=box.scrollHeight-box.scrollTop-box.clientHeight<60;
   const messageHtml=history.map(m=>`<article class="social-message ${m.sender===user.id?'own':''}"><small>${esc(m.author.name)} · ${esc(new Date(m.created*1000).toLocaleString())}</small><p>${esc(m.body)}</p></article>`).join('')||'<p>No messages yet.</p>';if(box.innerHTML!==messageHtml)box.innerHTML=messageHtml;if(nearBottom)box.scrollTop=box.scrollHeight;
   body.querySelector('#older-messages')?.remove();
   if(nextBefore){const older=document.createElement('button');older.id='older-messages';older.className='button ghost small';older.textContent='Load older messages';box.before(older);older.onclick=run(async()=>{const conversationId=selected;const result=await api('/api/v1/chat/'+conversationId+'/messages?before='+nextBefore);if(!alive || conversationId!==selected)return;history=[...result.items,...history];nextBefore=result.next_before;await load();});}
   const canSend=c.status==='accepted'||(c.status==='pending'&&!c.incoming&&messages.items.length===0);body.querySelector('#send-message').hidden=!canSend;
   if(canSend)body.querySelector('#chat-status').textContent=c.status==='pending'?'Send one introductory message, then wait for approval.':'';
  };
  body.querySelector('#start-chat').onsubmit=run(async()=>{selected=(await send('/chat',{user_id:input('chat-user')})).id;await load();});
  body.querySelector('#send-message').onsubmit=run(async()=>{await send('/chat/'+selected+'/messages',{body:input('message-body')});body.querySelector('#message-body').value='';await load();});
  await load();const poll=async()=>{if(!alive)return;try{await load();}catch(e){toast(e.message);}if(alive)timer=setTimeout(poll,5000);};timer=setTimeout(poll,5000);
 }
}
