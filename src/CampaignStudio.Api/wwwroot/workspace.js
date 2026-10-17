'use strict';
const appearanceDefaults = {theme:'system',palette:'forest',reducedMotion:false};
let shellReady=false;
let workspaceAppearance = {...appearanceDefaults};
const systemScheme = window.matchMedia('(prefers-color-scheme: dark)');
function applyAppearance(value, cache=false) {
 const valid=value && ['system','light','dark'].includes(value.theme) && ['forest','indigo','ocean','rose'].includes(value.palette);
 workspaceAppearance=valid?{theme:value.theme,palette:value.palette,reducedMotion:value.reducedMotion===true}:{...appearanceDefaults};
 const root=document.documentElement;
 root.dataset.theme=workspaceAppearance.theme==='system'?(systemScheme.matches?'dark':'light'):workspaceAppearance.theme;
 root.dataset.palette=workspaceAppearance.palette;root.dataset.motion=workspaceAppearance.reducedMotion?'reduced':'system';
 if(cache)try{localStorage.setItem('studio-appearance',JSON.stringify(workspaceAppearance));}catch{}
}
try{applyAppearance(JSON.parse(localStorage.getItem('studio-appearance')));}catch{applyAppearance(appearanceDefaults);}
systemScheme.addEventListener('change',()=>{if(workspaceAppearance.theme==='system')applyAppearance(workspaceAppearance);});

const shellIcons={
 workspace:'<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>',
 settings:'<path d="m9.5 3-.6 2a7 7 0 0 0-1.3.8l-2-.5-2 3.4L5 10.2a7 7 0 0 0 0 1.6l-1.4 1.5 2 3.4 2-.5a7 7 0 0 0 1.3.8l.6 2h4l.6-2a7 7 0 0 0 1.3-.8l2 .5 2-3.4-1.4-1.5a7 7 0 0 0 0-1.6l1.4-1.5-2-3.4-2 .5a7 7 0 0 0-1.3-.8l-.6-2z"/><circle cx="11.5" cy="11" r="3"/>',
 help:'<path d="M8 7a4 4 0 0 1 8 0c0 3-4 3-4 6"/><path d="M12 17v.1"/>',
 palette:'<path d="M12 3a9 9 0 1 0 0 18h1a2 2 0 0 0 1-3.7 1.5 1.5 0 0 1 1-2.8h2a4 4 0 0 0 4-4C21 6 17 3 12 3Z"/><path d="M7.5 10h.01M10 6.8h.01M14.5 7h.01M17 10h.01"/>',
 theme:'<path d="M20.8 13a8.5 8.5 0 0 1-9.8-9.8A8.5 8.5 0 1 0 20.8 13Z"/>',
 account:'<circle cx="12" cy="8" r="4"/><path d="M4 21v-2a8 8 0 0 1 16 0v2"/>',
 book:'<path d="M12 5c-3-2-6-2-10-1v15c4-1 7-1 10 1 3-2 6-2 10-1V4c-4-1-7-1-10 1Z"/><path d="M12 5v15"/>',
 keyboard:'<rect x="2" y="5" width="20" height="14" rx="2"/><path d="M6 9h.01M10 9h.01M14 9h.01M18 9h.01M6 12h.01M10 12h.01M14 12h.01M18 12h.01M7 15h10"/>',
 update:'<path d="M20 7v5h-5M4 17v-5h5"/><path d="M6.1 6.1A8 8 0 0 1 19.5 9M4.5 15A8 8 0 0 0 18 18"/>',
 chevron:'<path d="m7 10 5 5 5-5"/>',
 plug:'<path d="m8 3 1 5m7-5-1 5M6 8h12v3a6 6 0 0 1-12 0Zm6 9v4"/>',
 external:'<path d="M14 3h7v7m0-7L10 14M10 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-5"/>'
};
function shellIcon(name){return `<svg class="menu-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${shellIcons[name]||shellIcons.help}</svg>`;}
function shellMenu(id,label,icon,items){
 const active=items.some(item=>item.route===state.view);
 return `<details class="nav-menu" id="${id}"><summary class="nav-button ${active?'active':''}" aria-expanded="false" aria-controls="${id}-items">${shellIcon(icon)}<span>${label}</span>${shellIcon('chevron')}</summary><div class="nav-dropdown" id="${id}-items">${items.map(item=>`<button type="button" class="nav-button ${state.view===item.route?'active':''}" data-nav="${item.route}" ${state.view===item.route?'aria-current="page"':''}>${shellIcon(item.icon)}<span>${item.label}</span></button>`).join('')}</div></details>`;
}
function renderUtilityNavigation(){
 document.querySelector('#utility-nav').innerHTML=shellMenu('settings-menu','Settings','settings',[
  {label:'Appearance',route:'appearance',icon:'palette'},{label:'Switch theme',route:'theme',icon:'theme'},{label:'License & account',route:'account',icon:'account'}
 ])+shellMenu('help-menu','Help','help',[
  {label:'User guide',route:'help',icon:'book'},{label:'Help & shortcuts',route:'shortcuts',icon:'keyboard'},{label:'Check for updates',route:'updates',icon:'update'}
 ]);
}
function updateVersionLabel(){document.querySelector('#footer-version').textContent=`${state.settings?.releaseChannel||'Preview'} ${state.settings?.version||'0.2.2'}`;}
async function persistAppearance(changes,button){
 if(button)button.disabled=true;
 try{const saved=await data('/preferences',{method:'PUT',body:{...workspaceAppearance,...changes}});applyAppearance(saved,true);toast('Appearance saved for this workspace.');}
 catch(err){toast(err.message);}
 finally{if(button)button.disabled=false;}
}
function appearance(){
 const palettes=[['forest','Forest','A calm green workspace.','#176b56','#e9f4ee'],['indigo','Indigo','Crisp blue and violet accents.','#4f46e5','#eef2ff'],['ocean','Ocean','Clear teal and blue tones.','#036b8e','#e2f3fb'],['rose','Rose','Warm plum and rose accents.','#9d3658','#fce8f0']];
 app.innerHTML=heading('SETTINGS','Make the workspace yours.','Choose an accent palette. Changes apply to the app, leaving your email designs as you created them.')+`<form id="appearance-form" class="card settings-card"><fieldset class="plain-fieldset"><legend>Color palette</legend><div class="palette-grid">${palettes.map(([id,name,description,color,soft])=>`<label class="palette-choice"><input type="radio" name="palette" value="${id}" ${workspaceAppearance.palette===id?'checked':''}><span class="palette-sample" style="--sample-accent:${color};--sample-soft:${soft}"><span></span><span></span><span></span></span><strong>${name}</strong><small>${description}</small></label>`).join('')}</div></fieldset><label class="check-label"><input type="checkbox" name="reducedMotion" ${workspaceAppearance.reducedMotion?'checked':''}>Reduce interface motion</label><p class="muted settings-hint">Your operating system’s reduced-motion preference is also respected. These choices are saved with this workspace, including after a desktop restart.</p><div class="actions"><button class="btn primary" type="submit">Save appearance</button><button class="btn" type="button" data-nav="theme">${shellIcon('theme')}Switch theme</button></div></form>`;
 document.querySelector('#appearance-form').onsubmit=async event=>{event.preventDefault();const form=event.currentTarget;await persistAppearance({palette:form.elements.palette.value,reducedMotion:form.elements.reducedMotion.checked},form.querySelector('[type="submit"]'));};
}
function switchTheme(){
 const themes=[['light','Light','Bright surfaces and clear contrast.'],['dark','Dark','Dim surfaces for a darker workspace.'],['system','Use system setting','Follow Windows or your browser’s light/dark preference.']];
 app.innerHTML=heading('SETTINGS','The right light for your work.','Choose Light, Dark, or the system setting. Your email preview keeps its own design colors.')+`<form id="theme-form" class="card settings-card"><fieldset class="plain-fieldset"><legend>Workspace theme</legend><div class="theme-grid">${themes.map(([id,name,description])=>`<label class="theme-choice"><input type="radio" name="theme" value="${id}" ${workspaceAppearance.theme===id?'checked':''}><span class="theme-sample ${id}" aria-hidden="true"><span></span><span></span></span><strong>${name}</strong><small>${description}</small></label>`).join('')}</div></fieldset><div class="actions"><button class="btn primary" type="submit">Apply theme</button><button class="btn" type="button" data-nav="appearance">Color palettes</button></div></form>`;
 document.querySelector('#theme-form').onsubmit=async event=>{event.preventDefault();await persistAppearance({theme:event.currentTarget.elements.theme.value},event.currentTarget.querySelector('[type="submit"]'));};
}
async function account(){
 const settings=await data('/settings');state.settings=settings;updateVersionLabel();
 app.innerHTML=heading('SETTINGS','License & account','Your application status and access to this workspace.')+`<div class="grid-2"><section class="card"><div class="section-head"><h2>Application license</h2>${badge('Preview')}</div><p>This preview does not require a license key. Paid activation and subscription billing are not available in this version.</p><dl class="settings-facts"><div><dt>Installed version</dt><dd>${e(settings.version)} · ${e(settings.releaseChannel||'Preview')}</dd></div><div><dt>Activation</dt><dd>No activation required for this preview</dd></div></dl><button type="button" class="btn" data-nav="updates">${shellIcon('update')}Check for updates</button></section><section class="card"><h2>Workspace access</h2><dl class="settings-facts"><div><dt>Access method</dt><dd>Administrative workspace key</dd></div><div><dt>Data storage</dt><dd>${e(settings.storage)}</dd></div><div><dt>Live sending</dt><dd>${settings.liveReady?'Ready with a verified sender':'Capture mode until configured'}</dd></div></dl><p class="muted">This version uses one administrative key. Individual customer accounts, roles, and password sign-in are not available. Disconnecting this tab does not stop server schedules.</p><button type="button" class="btn" data-shell="disconnect">Disconnect this tab</button><p class="muted settings-hint">In the local Windows app, reopen Campaign Studio to reconnect. A browser session needs the workspace key again.</p></section></div>`;
}
function diagnosticsText(){return `Campaign Studio ${state.settings?.version||'0.2.2'} (${state.settings?.releaseChannel||'Preview'})\nStorage: ${state.settings?.storage||'Unknown'}\nLive sending ready: ${!!state.settings?.liveReady}\nTheme: ${workspaceAppearance.theme}\nPalette: ${workspaceAppearance.palette}`;}
function shortcuts(){
 const keys=[['Ctrl + ,','Open Appearance'],['Ctrl + /','Open Help & shortcuts'],['F1','Open the user guide'],['Ctrl + Alt + 1','Open Overview'],['Ctrl + Alt + 2','Open Audience'],['Ctrl + Alt + 3','Open Campaigns'],['Ctrl + S','Save the current campaign draft'],['Escape','Close a dropdown or dialog'],['Tab / Shift + Tab','Move between controls'],['↑ / ↓ · Home / End','Move within an open navigation dropdown']];
 app.innerHTML=heading('HELP','A little help, close at hand.','Find a workflow, use a shortcut, or share a clear issue report.')+`<div class="grid-2"><section class="card"><h2>Keyboard shortcuts</h2><dl class="shortcut-list">${keys.map(([key,description])=>`<div><dt><kbd>${key}</kbd></dt><dd>${description}</dd></div>`).join('')}</dl><p class="muted settings-hint">Global shortcuts pause while a dialog is open. On macOS browsers, Command can replace Ctrl.</p></section><section class="card"><h2>Support & troubleshooting</h2><p>Start with the guide for setup, connections, drafting, audience rules, schedules and reports.</p><div class="actions"><button type="button" class="btn primary" data-nav="help">${shellIcon('book')}Read the user guide</button><a class="btn" href="https://github.com/mdmohibulalam/email-sender/issues/new" target="_blank" rel="noopener noreferrer">${shellIcon('external')}Report an issue</a></div><hr><h3>Share application details</h3><p class="muted">Copy the version, storage type and appearance settings. Access keys, sender credentials and contact data are excluded.</p><button type="button" class="btn" data-shell="diagnostics">Copy app details</button><hr><h3>Connection or delivery problem?</h3><p class="muted">Retest the sending profile under Connections. Use Reports → Attempts for the actual recipient outcome. Unknown jobs require review before any manual resend.</p></section></div>`;
}
async function checkUpdates(){
 const button=document.querySelector('#check-updates'),status=document.querySelector('#update-status'),links=document.querySelector('#release-links');if(!button||button.disabled)return;
 button.disabled=true;status.textContent='Checking published releases…';links.replaceChildren();
 try{
  const result=await data('/updates');if(!status.isConnected)return;
  status.textContent=result.message;
  if(result.latestVersion){const line=document.createElement('p');line.className='muted';line.textContent='Latest stable release: '+result.latestVersion;links.append(line);}
  if(result.releaseUrl&&/^https:\/\/github\.com\/mdmohibulalam\/email-sender\/releases\/tag\/[A-Za-z0-9.%_-]+$/.test(result.releaseUrl)){
   const link=document.createElement('a');link.className='btn primary';link.href=result.releaseUrl;link.target='_blank';link.rel='noopener noreferrer';link.textContent='View release & download';links.append(link);
  }
 }catch(err){if(status.isConnected)status.textContent=err.message;}
 finally{if(button.isConnected)button.disabled=false;}
}
async function updates(){
 app.innerHTML=heading('HELP','Keep Campaign Studio up to date.','Check the official repository for a published stable release.')+`<section class="card settings-card"><h2>Your version: ${e(state.settings?.version||'0.2.2')} · Preview</h2><p id="update-status" role="status">Ready to check.</p><div id="release-links" class="release-links"></div><div class="actions"><button type="button" class="btn" id="check-updates">${shellIcon('update')}Check again</button><a class="btn" href="https://github.com/mdmohibulalam/email-sender/actions/workflows/verify.yaml" target="_blank" rel="noopener noreferrer">${shellIcon('external')}Preview build downloads</a></div><p class="muted settings-hint">Checks contact GitHub only when requested. Updates are downloaded and installed manually. Use a successful build; back up your workspace before upgrading.</p></section>`;
 document.querySelector('#check-updates').onclick=checkUpdates;await checkUpdates();
}
function placeShellMenu(menu){
 const panel=menu.querySelector('.nav-dropdown');panel.style.left='';panel.style.right='';
 const box=panel.getBoundingClientRect(),parent=menu.getBoundingClientRect();
 const left=Math.max(12,Math.min(box.left,window.innerWidth-box.width-12));
 panel.style.left=(left-parent.left)+'px';panel.style.right='auto';
}
window.addEventListener('resize',()=>document.querySelectorAll('.nav-menu[open]').forEach(placeShellMenu));
function closeShellMenus(focus=false){document.querySelectorAll('.nav-menu[open]').forEach(menu=>{menu.open=false;if(focus)menu.querySelector('summary').focus();});}
document.addEventListener('toggle',event=>{
 const menu=event.target;if(!menu.matches?.('.nav-menu'))return;
 menu.querySelector('summary').setAttribute('aria-expanded',String(menu.open));
 if(menu.open){document.querySelectorAll('.nav-menu[open]').forEach(other=>{if(other!==menu)other.open=false;});placeShellMenu(menu);}
},true);
document.addEventListener('click',async event=>{
 if(!shellReady)return;
 const button=event.target.closest('[data-shell]');
 if(event.target.closest('.brand')&&typeof state!=='undefined'&&state.key){event.preventDefault();try{await navigate('overview');}catch(err){toast(err.message);}}
 if(!button)return;
 if(button.dataset.shell==='disconnect')showModal(`<h2>Disconnect this workspace?</h2><p>Your access key will be removed from this tab. Server schedules continue running.</p><div class="actions">${btn('Cancel','close')}<button type="button" class="btn primary" data-shell="confirm-disconnect">Disconnect</button></div>`);
 if(button.dataset.shell==='confirm-disconnect'){modal.close();state.key='';sessionStorage.removeItem('studio-key');login();toast('This tab is disconnected.');}
 if(button.dataset.shell==='diagnostics'){
  const details=diagnosticsText();try{await navigator.clipboard.writeText(details);toast('Application details copied.');}catch{showModal(`<h2>Application details</h2><p>Select and copy these details for your issue report.</p><textarea rows="7" readonly aria-label="Application details">${e(details)}</textarea><div class="actions">${btn('Close','close')}</div>`);}
 }
});
document.addEventListener('click',event=>{if(event.target.closest('[data-nav]')||!event.target.closest('.nav-menu'))closeShellMenus();});
document.addEventListener('input',event=>{
 if(event.target.id!=='guide-search')return;const query=event.target.value.trim().toLowerCase();let count=0;
 document.querySelectorAll('.guide-section').forEach(section=>{section.hidden=!section.textContent.toLowerCase().includes(query);if(!section.hidden)count++;});
 document.querySelector('#guide-count').textContent=`${count} guide sections shown`;
});
document.addEventListener('keydown',async event=>{
 if(event.key==='Escape'&&!document.querySelector('dialog[open]')){closeShellMenus(true);return;}
 const menu=event.target.closest('.nav-menu[open]');
 if(menu&&['ArrowDown','ArrowUp','Home','End'].includes(event.key)){
  const items=[...menu.querySelectorAll('button:not(:disabled)')];if(!items.length)return;event.preventDefault();let index=items.indexOf(document.activeElement);
  index=event.key==='Home'?0:event.key==='End'?items.length-1:event.key==='ArrowDown'?(index+1)%items.length:(index-1+items.length)%items.length;items[index].focus();return;
 }
 if(!shellReady||!state.key||document.querySelector('dialog[open]'))return;
 const modifier=event.ctrlKey||event.metaKey;let route=null;
 if(event.key==='F1')route='help';
 if(modifier&&!event.altKey&&event.key===',')route='appearance';
 if(modifier&&!event.altKey&&event.key==='/')route='shortcuts';
 if(modifier&&event.altKey)route=({'1':'overview','2':'audience','3':'campaigns'})[event.key]||route;
 try{
  if(route){event.preventDefault();await navigate(route);}
  else if(modifier&&!event.altKey&&event.key.toLowerCase()==='s'&&document.querySelector('#editor-form')){event.preventDefault();await saveDraft();toast('Draft saved.');}
 }catch(err){toast(err.message);}
});
