'use strict';
async function review(){
 state.settings=await data('/settings');
 const campaign=await saveDraft();
 const [audience,senders]=await Promise.all([data('/campaigns/'+campaign.id+'/audience'),data('/connections')]);
 const verified=senders.filter(sender=>sender.verifiedAt);
 const liveReady=state.settings.liveSendingEnabled&&state.settings.publicLinksReady&&verified.length>0;
 const missingSubject=!campaign.subject.trim(),emptyAudience=audience.total===0,blocked=missingSubject||emptyAudience;
 showModal(`<div class="eyebrow">FINAL REVIEW</div><h2>Review your campaign.</h2>
 <dl class="review-summary"><div><dt>Subject</dt><dd>${e(campaign.subject.trim()||'Not added yet')}</dd></div><div><dt>Audience</dt><dd>${audience.total.toLocaleString()} eligible ${audience.total===1?'contact':'contacts'} · ${e(campaign.listName)}</dd></div></dl>
 ${blocked?`<section class="note warn review-blockers" id="review-blockers" role="alert"><strong>Finish setup before scheduling</strong>${missingSubject?'<p>Add a subject line so recipients know what your email is about.</p><button type="button" class="btn small" data-review-focus="subject">Edit subject</button>':''}${emptyAudience?'<p>No eligible contacts match this audience. Check the selected list, segment and exclusions, or add subscribed contacts.</p><button type="button" class="btn small" data-review-focus="listName">Choose another audience</button>':''}</section>`:''}
 <form id="schedule-form" novalidate>
 <div class="form-row"><div class="field"><label for="mode">Campaign mode</label><select id="mode" name="mode"><option value="Capture">Test capture — no emails sent</option>${liveReady?'<option value="Smtp">Live SMTP sending</option>':''}</select><small id="mode-help">Capture saves personalized email files for review.</small></div>
 <div class="field"><label for="start-choice">When to start</label><select id="start-choice" name="startChoice"><option value="now">Start now</option><option value="later">Choose a date & time</option></select><small>Campaign execution requires this app or server to stay running.</small></div></div>
 <div class="field" id="schedule-time-field" hidden><label for="schedule-time">Start date & time</label><input id="schedule-time" name="when" type="datetime-local" disabled><small>Use a future time within one year. Timezone: ${e(Intl.DateTimeFormat().resolvedOptions().timeZone)}.</small></div>
 <div id="live-confirm" hidden><div class="field"><label for="senderId">Sending connection</label><select id="senderId" name="senderId" disabled>${verified.map(sender=>`<option value="${sender.id}">${e(sender.name)} · ${e(sender.fromEmail)}</option>`).join('')}</select></div><label class="check-label"><input type="checkbox" name="confirmLiveSend" disabled>I confirm this audience and authorize live sending.</label><p class="muted settings-hint">Accepted emails cannot be recalled. The selected connection must remain verified.</p></div>
 ${!liveReady?'<p class="muted settings-hint">Live sending needs a verified connection and hosted unsubscribe links. Test capture is available now.</p>':''}
 <div id="schedule-error" class="note warn form-error" role="alert" tabindex="-1" hidden></div>
 <p class="muted settings-hint" id="schedule-status" role="status">Audience is frozen when scheduled; suppression is checked again before sending.</p>
 <div class="actions"><button type="button" class="btn" id="review-back" data-action="close">Back</button><button class="btn primary" id="schedule-submit" ${blocked?'disabled aria-describedby="review-blockers"':''}>Schedule campaign</button></div></form>`, 'review-dialog');
 const form=document.querySelector('#schedule-form'),button=document.querySelector('#schedule-submit'),back=document.querySelector('#review-back');
 const mode=form.elements.mode,start=form.elements.startChoice,when=form.elements.when,sender=form.elements.senderId,confirm=form.elements.confirmLiveSend;
 const status=document.querySelector('#schedule-status'),error=document.querySelector('#schedule-error');
 const failure=message=>{error.textContent=message;error.hidden=false;error.focus();};
 document.querySelectorAll('[data-review-focus]').forEach(control=>control.onclick=()=>{modal.close();const field=document.getElementById(control.dataset.reviewFocus);field?.focus();field?.scrollIntoView({block:'center',behavior:'auto'});});
 mode.onchange=()=>{
  const live=mode.value==='Smtp';document.querySelector('#live-confirm').hidden=!live;
  sender.disabled=!live;sender.required=live;confirm.disabled=!live;confirm.required=live;
  document.querySelector('#mode-help').textContent=live?'Sends real emails through the verified connection.':'Capture saves personalized email files for review.';
  error.hidden=true;
 };
 start.onchange=()=>{const later=start.value==='later';document.querySelector('#schedule-time-field').hidden=!later;when.disabled=!later;when.required=later;error.hidden=true;if(later)when.focus();};
 let submitting=false;
 const lifetime=new AbortController();
 modal.addEventListener('cancel',event=>{if(submitting)event.preventDefault();},{signal:lifetime.signal});
 modal.addEventListener('close',()=>lifetime.abort(),{once:true});
 form.onsubmit=async event=>{
  event.preventDefault();if(blocked||submitting)return;error.hidden=true;
  const now=Date.now(),scheduledAt=start.value==='later'?new Date(when.value).getTime():now;
  if(!Number.isFinite(scheduledAt)||scheduledAt<now-1000||scheduledAt>now+366*86400000){failure('Choose a valid future date and time within one year, or select Start now.');return;}
  if(mode.value==='Smtp'&&(!sender.value||!confirm.checked)){failure('Select a verified connection and confirm that you authorize live sending.');return;}
  submitting=true;button.disabled=true;back.disabled=true;button.textContent='Scheduling…';status.textContent='Creating the campaign schedule…';
  for(const control of [mode,start,when,sender,confirm])control.disabled=true;
  const request=new AbortController(),deadline=setTimeout(()=>request.abort(),30000);
  try{
   await data('/campaigns/'+campaign.id+'/schedule',{method:'POST',signal:request.signal,body:{scheduledAt:Math.floor(scheduledAt/1000),mode:mode.value,confirmLiveSend:mode.value==='Smtp'&&confirm.checked,senderId:mode.value==='Smtp'?sender.value:null}});
   modal.close();toast('Campaign scheduled.');await showReport(campaign.id);
  }catch(err){if(form.isConnected){failure(request.signal.aborted?'The scheduling response timed out. Check Campaigns for the latest status before retrying.':err.message);status.textContent='Scheduling was not confirmed. If the connection failed, check Campaigns before retrying.';}}
  finally{
   clearTimeout(deadline);
   submitting=false;
   if(form.isConnected){button.disabled=false;back.disabled=false;button.textContent='Schedule campaign';mode.disabled=false;start.disabled=false;when.disabled=start.value!=='later';sender.disabled=confirm.disabled=mode.value!=='Smtp';}
  }
 };
}
