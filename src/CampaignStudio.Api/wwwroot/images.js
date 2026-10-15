'use strict';
async function chooseImage(onChoose){
 const images=await data('/images');
 showModal(`<h2>Choose an image</h2><p>Uploaded images are embedded in outgoing emails.</p><div class="image-grid">${images.map(x=>`<button class="image-choice" data-image-id="${x.id}"><img src="${e(x.url)}" alt="${e(x.name)}"><span>${e(x.name)}</span></button>`).join('')||'<p>No images yet.</p>'}</div><div class="actions">${btn('Cancel','close')}<button class="btn primary" id="new-image">Upload / edit image</button></div>`);
 modal.querySelectorAll('[data-image-id]').forEach(n=>n.onclick=()=>{const image=images.find(x=>x.id===n.dataset.imageId);modal.close();onChoose(image);});
 document.querySelector('#new-image').onclick=()=>imageEditor(onChoose);
}
function imageEditor(onChoose=()=>{}){
 let bitmap=null;
 showModal(`<h2>Image editor</h2><p>Crop, resize, adjust brightness, or use grayscale. Choose PNG, JPEG or WebP under 5 MB.</p><div class="field"><label for="image-file">Image file</label><input id="image-file" type="file" accept="image/png,image/jpeg,image/webp"></div><div id="image-controls" hidden><div class="form-row">${field('Crop X','crop-x',0,'number')}${field('Crop Y','crop-y',0,'number')}${field('Crop width','crop-w',600,'number')}${field('Crop height','crop-h',400,'number')}</div><div class="form-row">${field('Output width (px)','image-width',600,'number')}${field('Brightness (%)','image-brightness',100,'number')}</div><label><input type="checkbox" id="image-gray">Grayscale</label><canvas id="image-canvas" style="max-width:100%;height:auto;margin:16px 0"></canvas>${field('Image name','image-name','newsletter-image.png')}<div class="actions"><button class="btn" id="image-export">Export PNG</button><button class="btn primary" id="image-save">Save & choose image</button></div></div><div class="actions">${btn('Close','close')}</div>`);
 const canvas=document.querySelector('#image-canvas');
 function draw(){
  if(!bitmap)throw Error('Choose an image first.');
  const number=id=>Number(document.getElementById(id).value);
  const x=number('crop-x'),y=number('crop-y'),w=number('crop-w'),h=number('crop-h'),width=number('image-width'),brightness=number('image-brightness');
  if(![x,y,w,h,width,brightness].every(Number.isFinite)||x<0||y<0||w<=0||h<=0||x+w>bitmap.width||y+h>bitmap.height||width<1||width>10000||width*Math.round(width*h/w)>25000000||brightness<0||brightness>200)throw Error('Check crop bounds, output size and brightness (0–200).');
  canvas.width=Math.round(width);canvas.height=Math.max(1,Math.round(width*h/w));
  const context=canvas.getContext('2d');context.filter=`brightness(${brightness}%) grayscale(${document.querySelector('#image-gray').checked?1:0})`;
  context.drawImage(bitmap,x,y,w,h,0,0,canvas.width,canvas.height);
 }
 document.querySelector('#image-file').onchange=async ev=>{
  try{const file=ev.target.files[0];if(!file)return;if(file.size>5000000||!['image/png','image/jpeg','image/webp'].includes(file.type))throw Error('Choose a supported image under 5 MB.');
   if(bitmap)bitmap.close();bitmap=await createImageBitmap(file);if(bitmap.width*bitmap.height>25000000)throw Error('Choose an image under 25 megapixels.');
   for(const [id,value]of Object.entries({'crop-x':0,'crop-y':0,'crop-w':bitmap.width,'crop-h':bitmap.height,'image-width':Math.min(1200,bitmap.width),'image-name':file.name.replace(/\.[^.]+$/,'')+'.png'}))document.getElementById(id).value=value;
   document.querySelector('#image-controls').hidden=false;draw();
  }catch(err){toast(err.message);}
 };
 document.querySelector('#image-controls').oninput=()=>{try{draw();}catch(err){toast(err.message);}};
 document.querySelector('#image-export').onclick=()=>{try{draw();canvas.toBlob(blob=>{if(!blob)return;const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=document.querySelector('#image-name').value||'image.png';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);},'image/png');}catch(err){toast(err.message);}};
 document.querySelector('#image-save').onclick=async ev=>{ev.target.disabled=true;try{draw();const image=await data('/images',{method:'POST',body:{name:document.querySelector('#image-name').value||'image.png',base64:canvas.toDataURL('image/png').split(',')[1]}});bitmap.close();modal.close();onChoose(image);toast('Image saved.');}catch(err){toast(err.message);}finally{ev.target.disabled=false;}};
}
document.addEventListener('click',ev=>{if(ev.target.closest('[data-image-editor]'))imageEditor();});
