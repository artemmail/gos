// Isolated browser review. Fixture responses never touch the application database.
const fs = require('fs');
const path = require('path');
const http = require('http');
const { spawn } = require('child_process');
const WebSocket = require('ws');
const assert = require('assert/strict');
const root = path.resolve(__dirname, '../dist/zakupki-fetcher-client');
const out = path.join(__dirname, 'artifacts');
fs.mkdirSync(out, { recursive: true });
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const regions = [{code:77,name:'Москва'},{code:50,name:'Московская область'},{code:78,name:'Санкт-Петербург'}];
let profile = {companyInfo:'Поставляем и обслуживаем медицинское оборудование. Опыт работы с государственными клиниками — 8 лет.',regions:[77,50],okpd2Codes:['32.50.13'],availableRegions:regions};
let vectors = [{id:'vector-1',query:'Поставка медицинского оборудования'},{id:'vector-2',query:'Обслуживание диагностического оборудования'}];
const titles = ['Поставка медицинского оборудования для оснащения городской клинической больницы','Техническое обслуживание и ремонт диагностического оборудования','Поставка расходных материалов для клинико-диагностической лаборатории','Оснащение кабинетов поликлиники медицинской мебелью'];
const notices = Array.from({length:27},(_,i)=>({id:'notice-'+i,purchaseNumber:'03731000123'+String(i).padStart(8,'0'),purchaseObjectInfo:titles[i%4],maxPrice:[2450000,890000,1675800,4250000][i%4],okpd2Code:'32.50.13',okpd2Name:'Инструменты и приспособления медицинские',region:String([77,50,78][i%3]),publishDate:'2026-10-07T10:00:00Z',collectingEnd:'2026-11-20T08:00:00Z',analysisStatus:i===0?'Completed':i===2?'Failed':'NotStarted',hasAnalysisAnswer:i===0,recommended:i===0?true:null,decisionScore:i===0?.87:null,isFavorite:i===0,similarity:null,etpName:'ЕИС'}));
let failList = false, failProfile = false;
const requests = [], checks = [], errors = [];
const analysis = id => ({noticeId:id,status:'Completed',hasAnswer:true,result:'Закупка соответствует профилю компании.',updatedAt:'2026-10-08T10:00:00Z',completedAt:'2026-10-08T10:00:00Z',recommended:true,decisionScore:.87,structuredResult:{essence:'**Поставка** медицинского оборудования для городской больницы.',summary:'Направление и регион соответствуют профилю компании. Проверьте комплектацию, сроки поставки и требования к сервису.',recommended:true,decisionScore:.87,scores:{profitability:{score:.8,shortComment:'**Условия подходят** компании.',detailedComment:'**Важный риск**\n\n- Проверить сроки\n- Проверить сервис\n\n<img src=x onerror=alert(1)>'}}},prompt:'Сопоставьте документацию с профилем компании.'});
const respond = (res, data, status=200) => {res.writeHead(status, {'Content-Type':'application/json; charset=utf-8'});res.end(JSON.stringify(data));};
const server = http.createServer(async (req,res)=>{
  const u = new URL(req.url, 'http://127.0.0.1:4338');
  if(u.pathname.startsWith('/api/') || u.pathname.startsWith('/hubs/') || (u.pathname==='/notices-fab' && u.search)) {
    let raw='';for await(const chunk of req)raw+=chunk;
    requests.push({method:req.method,url:req.url,body:raw});
    if(u.pathname.includes('/negotiate'))return respond(res,{negotiateVersion:1,connectionId:'review',connectionToken:'review',availableTransports:[{transport:'WebSockets',transferFormats:['Text','Binary']}]});
    if(u.pathname==='/api/regions')return respond(res,regions);
    if(u.pathname==='/api/okpd2-codes')return respond(res,[{code:'32.50.13',name:'Инструменты и приспособления медицинские'}]);
    if(u.pathname==='/api/user-company'){
      if(failProfile)return respond(res,{message:'Профиль недоступен'},500);
      if(req.method==='PUT')profile={...profile,...JSON.parse(raw)};
      return respond(res,profile);
    }
    if(u.pathname==='/api/queryvectors'){
      if(req.method==='POST'){const created={id:'vector-'+Date.now(),...JSON.parse(raw)};vectors.push(created);return respond(res,created);}
      return respond(res,vectors);
    }
    if(u.pathname.startsWith('/api/queryvectors/')){vectors=vectors.filter(x=>x.id!==u.pathname.split('/').pop());return respond(res,{});}
    if(/\/analysis$/.test(u.pathname))return respond(res,analysis(u.pathname.split('/')[3]));
    if(/\/analysis\/report$/.test(u.pathname)){res.writeHead(200,{'Content-Type':'application/octet-stream','Content-Disposition':'attachment; filename=review-report.docx'});return res.end('Review report fixture');}
    if(/\/favorite$/.test(u.pathname)){const n=notices.find(x=>x.id===u.pathname.split('/')[3]);if(n)n.isFavorite=req.method==='POST';return respond(res,{});}
    if(u.pathname.includes('/by-number/')){
      const n=notices.find(x=>x.purchaseNumber===u.pathname.split('/').pop())||notices[0];
      return respond(res,{id:n.id,purchaseNumber:n.purchaseNumber,purchaseObjectInfo:n.purchaseObjectInfo,rawJson:JSON.stringify({commonInfo:{purchaseNumber:n.purchaseNumber,docNumber:n.purchaseNumber,purchaseObjectInfo:n.purchaseObjectInfo,publishDtInEis:n.publishDate,href:'https://zakupki.gov.ru/',placingWay:{name:'Электронный аукцион'},etp:{name:'Единая информационная система'}},maxPrice:n.maxPrice,notificationInfo:{procedureInfo:{collectingInfo:{startDt:n.publishDate,endDt:n.collectingEnd}},contractConditionsInfo:{maxPriceInfo:{maxPrice:n.maxPrice,currency:{name:'Российский рубль',code:'RUB'}}}},purchaseResponsibleInfo:{responsibleOrgInfo:{fullName:'ГБУЗ «Городская клиническая больница»',inn:'7701234567'},responsibleInfo:{contactEmail:'review@example.test',contactPhone:'+7 (495) 000-00-00'}}})});
    }
    if(/\/attachments$/.test(u.pathname))return respond(res,[{id:'file-1',fileName:'Техническое задание на поставку оборудования.pdf',description:'Требования к комплектации и поставке',hasBinaryContent:true,hasMarkdownContent:true,fileSize:244000,documentDate:'2026-10-07T10:00:00Z'}]);
    if(u.pathname.includes('/attachments/')){res.writeHead(200,{'Content-Type':'application/octet-stream'});return res.end('Document fixture');}
    if(['/api/notices','/api/notices/favorites','/api/notices/vector-search'].includes(u.pathname)){
      if(failList)return respond(res,{message:'Тестовая ошибка соединения'},503);
      let items=notices.filter(x=>u.pathname.endsWith('favorites')?x.isFavorite:true);
      const q=u.searchParams.get('search')||'', num=u.searchParams.get('purchaseNumber');
      if(q)items=items.filter(x=>x.purchaseObjectInfo.toLowerCase().includes(q.toLowerCase()));
      if(num)items=items.filter(x=>x.purchaseNumber===num);
      const page=Number(u.searchParams.get('page')||1),pageSize=Number(u.searchParams.get('pageSize')||20),totalCount=items.length;
      items=items.slice((page-1)*pageSize,page*pageSize).map(x=>({...x,similarity:u.pathname.endsWith('vector-search')?.86:null}));
      return setTimeout(()=>respond(res,{items,totalCount,page,pageSize}),90);
    }
    if(u.pathname==='/notices-fab')return respond(res,{message:'Источник недоступен'},503);
    if(u.pathname.includes('/logout'))return respond(res,{});
    return respond(res,[]);
  }
  let file=path.resolve(root,'.'+decodeURIComponent(u.pathname));
  if(!file.startsWith(root+path.sep)||!fs.existsSync(file)||fs.statSync(file).isDirectory())file=path.join(root,'index.html');
  const type={'.html':'text/html; charset=utf-8','.js':'application/javascript','.css':'text/css','.woff':'font/woff','.woff2':'font/woff2','.ico':'image/x-icon','.svg':'image/svg+xml'}[path.extname(file)]||'application/octet-stream';
  res.writeHead(200,{'Content-Type':type});fs.createReadStream(file).pipe(res);
});
const hub=new WebSocket.WebSocketServer({server,path:'/hubs/notice-analysis'});
hub.on('connection',ws=>ws.on('message',raw=>{if(raw.toString().includes('protocol'))ws.send('{}\x1e');}));

async function main(){
  await new Promise(resolve=>server.listen(4338,'127.0.0.1',resolve));
  const browser=spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',['--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--disable-background-networking','--remote-debugging-port=9468','--user-data-dir='+path.join(out,'browser-profile'),'about:blank'],{windowsHide:true,stdio:'ignore'});
  let socket;
  try {
    let target;for(let i=0;i<80;i++){try{target=(await(await fetch('http://127.0.0.1:9468/json/list')).json()).find(x=>x.type==='page');}catch{}if(target)break;await pause(150);}assert(target,'Chrome must start');
    socket=new WebSocket(target.webSocketDebuggerUrl);await new Promise(resolve=>socket.once('open',resolve));
    let id=0;const pending=new Map();
    socket.on('message',raw=>{const message=JSON.parse(raw);if(message.id){const p=pending.get(message.id);if(p){pending.delete(message.id);message.error?p.reject(Error(message.error.message)):p.resolve(message.result);}}if(message.method==='Runtime.exceptionThrown')errors.push(message.params.exceptionDetails.exception?.description||message.params.exceptionDetails.text);});
    const cdp=(method,params={})=>new Promise((resolve,reject)=>{const current=++id;pending.set(current,{resolve,reject});socket.send(JSON.stringify({id:current,method,params}));});
    const ev=async expression=>{const r=await cdp('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.exception?.description||r.exceptionDetails.text);return r.result.value;};
    const wait=async expression=>{for(let i=0;i<100;i++){if(await ev(expression))return;await pause(100);}throw Error('Timeout: '+expression);};
    const check=(condition,label)=>{assert(condition,label);checks.push(label);console.log('PASS '+label);};
    const click=async selector=>{await ev(`document.querySelector(${JSON.stringify(selector)}).click()`);await pause(160);};
    const input=async(selector,value)=>{await ev(`(()=>{const input=document.querySelector(${JSON.stringify(selector)});input.value=${JSON.stringify(value)};input.dispatchEvent(new Event('input',{bubbles:true}));})()`);};
    const size=async width=>{await cdp('Emulation.setDeviceMetricsOverride',{width,height:1000,deviceScaleFactor:1,mobile:false});await pause(100);};
    const navigate=async route=>{await cdp('Page.navigate',{url:'http://127.0.0.1:4338'+route});await wait('!!document.querySelector("#main-content router-outlet + *")');await pause(250);};
    const capture=async name=>{await ev('document.fonts.ready');await pause(150);const r=await cdp('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(out,name+'.png'),Buffer.from(r.data,'base64'));};
    const noOverflow=async label=>check(await ev('document.documentElement.scrollWidth <= innerWidth + 1'),label+' has no horizontal overflow');
    await cdp('Page.enable');await cdp('Runtime.enable');await cdp('Network.enable');await cdp('Network.setBlockedURLs',{urls:['*mc.yandex*']});
    await size(1440);await navigate('/');await ev('localStorage.clear()');await navigate('/');await capture('01-landing-desktop');await noOverflow('Landing desktop');
    await click('.hero-actions .button');await wait('location.pathname==="/login"');check(await ev('new URLSearchParams(location.search).get("returnUrl")==="/tenders"'),'Search entry preserves return URL at login');await capture('02-login-desktop');
    await ev('localStorage.setItem("review-preference","preserved")');await navigate('/login?error=test&returnUrl=%2Ffavorites');check(await ev('document.querySelector("[role=alert]") && localStorage.getItem("review-preference")==="preserved"'),'Login errors are visible and preferences survive');
    const token='e30.'+Buffer.from(JSON.stringify({sub:'review',displayName:'Александр Иванов',email:'review@example.test',roles:['Free'],exp:4102444800})).toString('base64url')+'.review';
    await ev(`localStorage.setItem('accessToken',${JSON.stringify(token)});localStorage.setItem('userInfo',${JSON.stringify(JSON.stringify({id:'review',displayName:'Александр Иванов',email:'review@example.test',roles:['Free']}))})`);
    await navigate('/tenders');await wait('document.querySelectorAll(".tender-card").length===20');await capture('03-search-desktop');await noOverflow('Search desktop');
    check(await ev('document.querySelector(".account strong").textContent.trim()==="Александр Иванов"'),'Cyrillic account name is decoded as UTF-8');
    check(await ev('document.querySelector(".score").textContent.includes("8,7/10")'),'List and analysis dialog use the same score scale');
    await input('#tender-search','лаборатории');await click('.search-submit');await wait('document.querySelectorAll(".tender-card").length===7');check(requests.some(x=>decodeURIComponent(x.url).includes('search=лаборатории')),'Keyword search calls API and updates results');
    await click('.filter-toggle');await input('input[placeholder="Введите точный номер"]',notices[2].purchaseNumber);await click('.number-filter .button');await wait('document.querySelectorAll(".tender-card").length===1');check(true,'Exact purchase number filter works');
    await click('.reset-button');await wait('document.querySelectorAll(".tender-card").length===20');check(await ev('!document.querySelector("#tender-search").value'),'Reset clears search and advanced criteria');
    await ev('(()=>{const select=document.querySelector(".sort-control select");select.value="maxPrice:asc";select.dispatchEvent(new Event("change",{bubbles:true}));})()');await pause(250);check(requests.at(-1).url.includes('sortField=maxPrice')&&requests.at(-1).url.includes('sortDirection=asc'),'Sort is applied on the server');
    await click('.mat-mdc-paginator-navigation-next');await wait('document.querySelectorAll(".tender-card").length===7');check(requests.at(-1).url.includes('page=2'),'Pagination requests the next server page');
    await click('.search-modes button:nth-child(2)');await click('.semantic-form .button');await wait('document.querySelectorAll(".similarity").length===20');check(requests.some(x=>x.url.includes('/vector-search?')&&x.url.includes('page=1')),'Semantic search resets pagination and shows similarity');
    await navigate('/tenders');await wait('document.querySelectorAll(".similarity").length===20');check(true,'Semantic criteria survive reload');
    await click('.reset-button');await wait('document.querySelectorAll(".tender-card").length===20');await click('.view-switch button:nth-child(2)');await navigate('/tenders');await wait('!!document.querySelector(".compact-view")');check(true,'Compact view preference survives reload');await click('.view-switch button:first-child');
    await click('.tender-card:nth-child(2) .bookmark');check(notices[1].isFavorite,'Favorite is persisted through API');await click('.workspace-nav a[href="/favorites"]');await wait('document.querySelectorAll(".tender-card").length===2');check(await ev('document.querySelectorAll(".search-modes button").length===1'),'Favorites stay scoped to saved purchases');
    await click('.tender-card:nth-child(2) .bookmark');await wait('document.querySelectorAll(".tender-card").length===1');check(!notices[1].isFavorite,'Removing a favorite updates the list');
    await click('.analysis-button');await wait('!!document.querySelector("app-notice-analysis-dialog")');check(await ev('document.querySelector(".recommendation-chip").textContent.includes("Подходит")'),'Analysis opens the real result dialog');check(await ev('!!document.querySelector(".detailed-comment strong") && document.querySelectorAll(".detailed-comment li").length===2 && !document.querySelector(".detailed-comment img") && !document.querySelector(".detailed-comment").textContent.includes("**")'),'Analysis renders Markdown and ignores embedded HTML');await capture('04-analysis-desktop');await click('mat-dialog-actions > button');
    await click('.tender-title a');await wait('!!document.querySelector("app-notice-common-info")');await capture('05-details-desktop');await noOverflow('Detail desktop');await click('.detail-actions .button');await wait('!!document.querySelector(".file-row")');await capture('06-documents-desktop');check(await ev('document.querySelector(".file-body h3").textContent.includes("Техническое")'),'Attachments render filenames and download controls');await click('mat-dialog-actions button');
    await navigate('/company-profile');await wait('!!document.querySelector("textarea")');await capture('07-company-desktop');await noOverflow('Company desktop');await input('textarea','Новый профиль компании для проверки сохранения');await click('.save-bar button');await wait('document.querySelector(".save-bar").textContent.includes("успешно")');check(profile.companyInfo==='Новый профиль компании для проверки сохранения','Company profile persists and clears dirty state');
    let guardPrompts=0;
    const rejectUnsaved=raw=>{const m=JSON.parse(raw);if(m.method==='Page.javascriptDialogOpening'){guardPrompts++;cdp('Page.handleJavaScriptDialog',{accept:false});}};
    socket.on('message',rejectUnsaved);
    await input('textarea','Несохранённое изменение');await click('.workspace-nav a[href="/tenders"]');
    check(guardPrompts===1 && await ev('location.pathname==="/company-profile"'),'Unsaved profile navigation can be cancelled');
    socket.off('message',rejectUnsaved);await click('.save-bar button');await wait('document.querySelector(".save-bar").textContent.includes("успешно")');
    await click('.directions-actions .button');await wait('!!document.querySelector("app-query-vector-dialog")');await input('app-query-vector-dialog textarea','Поставка лабораторного оборудования');await click('mat-dialog-actions button:last-child');await wait('document.querySelectorAll(".direction-row").length===3');check(vectors.length===3,'New semantic direction persists and appears in profile');
    failProfile=true;await navigate('/company-profile');await wait('!!document.querySelector(".error-state")');check(await ev('!document.querySelector(".save-bar")'),'Failed profile load cannot overwrite saved company data');failProfile=false;await click('.error-state .button');await wait('!!document.querySelector("textarea")');check(true,'Profile error can be retried');
    await navigate('/tenders');await wait('document.querySelectorAll(".tender-card").length===20');failList=true;await click('.search-submit');await wait('!!document.querySelector(".error-state")');check(await ev('!document.querySelector(".tender-card")'),'Search failure hides stale results and offers retry');await capture('08-search-error');failList=false;await click('.error-state .button');await wait('document.querySelectorAll(".tender-card").length===20');check(true,'Search error recovery restores paginator subscriptions');
    await input('#tender-search','несуществующий запрос');await click('.search-submit');await wait('!!document.querySelector(".empty-state")');await capture('09-search-empty');check(await ev('document.querySelector(".empty-state").textContent.includes("Сбросить")'),'Empty search provides a recovery action');await click('.empty-state button');await wait('document.querySelectorAll(".tender-card").length===20');
    await size(390);await capture('10-search-mobile');await noOverflow('Search mobile');await click('.mobile-toggle');await wait('!!document.querySelector(".sidebar.is-open")');await capture('11-navigation-mobile');await cdp('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});await pause(150);check(await ev('!document.querySelector(".sidebar.is-open") && document.activeElement.classList.contains("mobile-toggle")'),'Mobile navigation closes with Escape and restores focus');
    await click('.mobile-toggle');await click('.workspace-nav a[href="/company-profile"]');await wait('!!document.querySelector("textarea")');await capture('12-company-mobile');await noOverflow('Company mobile');check(await ev('!document.querySelector(".sidebar.is-open")'),'Mobile navigation closes after choosing a page');
    await navigate('/notices/'+notices[0].purchaseNumber);await wait('!!document.querySelector("app-notice-common-info")');await capture('13-details-mobile');await noOverflow('Detail mobile');
    await click('.analysis-banner .button');await wait('!!document.querySelector("app-notice-analysis-dialog")');await noOverflow('Analysis mobile');await capture('17-analysis-mobile');await click('mat-dialog-actions > button');
    await click('.detail-actions .button');await wait('!!document.querySelector(".file-row")');await capture('14-documents-mobile');await noOverflow('Documents mobile');await click('mat-dialog-actions button');
    await ev('localStorage.removeItem("accessToken");localStorage.removeItem("userInfo")');await navigate('/');await capture('15-landing-mobile');await noOverflow('Landing mobile');await navigate('/login');await capture('16-login-mobile');await noOverflow('Login mobile');
    await ev('localStorage.setItem("userInfo","invalid JSON")');await navigate('/login');check(await ev('!!document.querySelector(".login-card")'),'Corrupted cached user data does not crash startup');
    for(const width of [320,768,1024]){await size(width);await navigate('/presentation');await noOverflow('Landing '+width);await navigate('/login');await noOverflow('Login '+width);}
    check(errors.length===0,'No uncaught browser exceptions');
    fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors,requestCount:requests.length},null,2));
    console.log(`Completed ${checks.length} checks. Screenshots: ${out}`);
  } finally {
    if(socket)socket.close();browser.kill();for(const ws of hub.clients)ws.terminate();hub.close();server.close();
  }
}
main().catch(error=>{console.error(error);process.exitCode=1;});

