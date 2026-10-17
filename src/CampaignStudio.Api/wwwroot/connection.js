'use strict';
function senderForm(sender={}){
 showModal(`<h2>${sender.id?'Edit':'Add'} sending connection</h2><p class="muted">Use the settings from your email provider. Examples are hints, not saved credentials.</p><form id="sender-form">
 <div class="form-row">${field('Connection name','name',sender.name||'','text','A name you can recognize when scheduling.',{placeholder:'e.g. Business SMTP',required:true,autocomplete:'off'})}
 <div class="field"><label for="deliveryMode">Delivery route</label><select id="deliveryMode" name="deliveryMode"><option value="Smtp" ${sender.deliveryMode!=='Direct'?'selected':''}>Authenticated SMTP service</option><option value="Direct" ${sender.deliveryMode==='Direct'?'selected':''}>Direct to recipient MX</option></select><small>Use your provider’s SMTP service for a typical connection.</small></div></div>
 <fieldset class="connection-section" id="smtp-settings"><legend>Server & authentication</legend><div class="form-row">
 ${field('SMTP hostname','host',sender.host||'','text','Use a hostname without https://.',{placeholder:'smtp.example.com',required:true,autocomplete:'off',maxlength:253})}
 ${field('Port','port',sender.port||587,'number','587 uses STARTTLS; 465 uses implicit TLS.',{placeholder:'587',required:true,min:1,max:65535,step:1})}</div>
 <div class="form-row">${field('Username','username',sender.username||'','text','Usually the full sending email address.',{placeholder:'news@example.com',autocomplete:'username',maxlength:500})}
 ${field('Password','password','','password',sender.hasPassword?'Leave blank to keep the saved encrypted password.':'Enter a password or app password if your provider requires authentication.',{placeholder:sender.hasPassword?'Leave blank to keep saved password':'Enter SMTP password or app password',autocomplete:'new-password',maxlength:2000})}</div>
 ${sender.hasPassword?'<label class="check-label"><input type="checkbox" name="clearPassword">Remove the saved password</label>':''}</fieldset>
 <p class="note warn" id="direct-help" hidden>Direct delivery requires explicit server support, outgoing port 25 and STARTTLS. It does not use SMTP account credentials.</p>
 <fieldset class="connection-section"><legend>Sender identity</legend><div class="form-row">
 ${field('From email','fromEmail',sender.fromEmail||'','email','Use an address your provider authorizes.',{placeholder:'news@example.com',required:true,autocomplete:'off'})}
 ${field('From name','fromName',sender.fromName||'','text','The name recipients see beside your email.',{placeholder:'Example Company',autocomplete:'organization'})}</div>
 <div class="form-row">${field('Reply-to','replyTo',sender.replyTo||'','email','Optional. Leave blank to use the sender address.',{placeholder:'support@example.com',autocomplete:'off'})}
 ${field('Envelope return address','returnPath',sender.returnPath||'','email','Optional. Your provider must authorize this bounce address.',{placeholder:'bounces@example.com',autocomplete:'off'})}</div></fieldset>
 <fieldset class="connection-section"><legend>Sending limits</legend><div class="form-row connection-limits">
 ${field('Delay between sends (ms)','delayMilliseconds',sender.delayMilliseconds??1000,'number','10–600,000 milliseconds.',{placeholder:'1000',required:true,min:10,max:600000,step:1})}
 ${field('Maximum safe retries','maxRetries',sender.maxRetries??3,'number','0–5 attempts after a safe transient failure.',{placeholder:'3',required:true,min:0,max:5,step:1})}
 ${field('Concurrent connections','maxConnections',sender.maxConnections??1,'number','1–100, within server and provider limits.',{placeholder:'1',required:true,min:1,max:100,step:1})}</div></fieldset>
 <p class="note">Saving clears verification. Test the connection before live sending. Pause campaigns using this connection before editing it.</p>
 <div id="sender-error" class="note warn form-error" role="alert" tabindex="-1" hidden></div>
 <div class="actions">${btn('Cancel','close')}<button type="submit" class="btn primary">Save connection</button></div></form>`, 'connection-dialog');
 const form=document.querySelector('#sender-form'),mode=form.elements.deliveryMode,smtp=document.querySelector('#smtp-settings');
 const routeChanged=()=>{const direct=mode.value==='Direct';smtp.hidden=direct;smtp.disabled=direct;document.querySelector('#direct-help').hidden=!direct;};mode.onchange=routeChanged;routeChanged();
 let saving=false;
 form.onsubmit=async event=>{
  event.preventDefault();if(saving)return;
  const value=Object.fromEntries(new FormData(form)),error=document.querySelector('#sender-error'),button=form.querySelector('[type="submit"]');error.hidden=true;
  const direct=mode.value==='Direct';
  const body={...value,name:value.name.trim(),host:direct?'':value.host.trim(),port:direct?25:Number(value.port),username:direct?'':value.username,password:direct?'':value.password,clearPassword:!!value.clearPassword,expectedRevision:sender.revision||0};
  for(const key of ['delayMilliseconds','maxRetries','maxConnections'])body[key]=Number(value[key]);
  if(!body.name){error.textContent='Enter a connection name.';error.hidden=false;error.focus();return;}
  saving=true;button.disabled=true;button.textContent='Saving…';
  try{await data('/connections'+(sender.id?'/'+sender.id:''),{method:sender.id?'PUT':'POST',body});modal.close();await navigate('connections');toast('Connection saved. Test before sending.');}
  catch(err){if(form.isConnected){error.textContent=err.message;error.hidden=false;error.focus();}}
  finally{saving=false;if(button.isConnected){button.disabled=false;button.textContent='Save connection';}}
 };
}
