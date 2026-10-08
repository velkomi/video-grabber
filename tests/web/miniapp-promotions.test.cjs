const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const base=__dirname+'/../../src/VideoGrabber.Platform.Api/wwwroot/';
class Element {
  constructor(doc,tag){this.ownerDocument=doc;this.tagName=tag;this.children=[];this.handlers={};this.attributes={};this.value='';this.textContent='';this.checked=false;}
  append(...items){this.children.push(...items);if(this.tagName==='select'&&!this.value)this.value=items[0]?.value||'';}
  prepend(item){this.children.unshift(item);}
  replaceChildren(...items){this.children=items;this.textContent='';if(this.tagName==='select')this.value='';}
  setAttribute(name,value){this.attributes[name]=value;}
  addEventListener(name,handler){this.handlers[name]=handler;}
  get options(){return this.children;}
  text(){return this.textContent+' '+this.children.map(x=>x.text()).join(' ');}
  all(){return[this,...this.children.flatMap(x=>x.all())];}
}
async function fixture(){
  const elements=new Map();const document={createElement(tag){return new Element(document,tag);},querySelector(selector){if(!elements.has(selector))elements.set(selector,new Element(document,selector==='#payment-product'?'select':'section'));return elements.get(selector);}};
  const state={profile:null,access:null,loadVersion:0};let account='A',balance=1000,referralCalls=0;
  const c={document,state,accountRefreshListeners:new Set(),URL,URLSearchParams,Date,Error,Set,Promise,crypto:require('node:crypto').webcrypto,sessionStorage:{getItem(){return null;},setItem(){},removeItem(){}},setTimeout};c.globalThis=c;c.window=c;
  const api=async path=>{
    if(path==='/v1/me')return{accountId:account};if(path==='/v1/access')return{};
    if(path==='/v1/referrals'){referralCalls++;return{code:'opaque',webLink:'https://example.test/',telegramLink:'https://t.me/ExampleBot',invited:0,paid:0,balances:[{currency:'RUB',availableMinor:balance,pendingMinor:0,reservedMinor:0,debtMinor:0}],history:[]};}
    if(path.startsWith('/v1/payment-products'))return{products:[{sku:'start',planId:'start',recurringAllowed:true,prices:{stars:{minorUnits:1500,currency:'XTR'}}}]};
    if(path==='/v1/promotions/quote')return{quoteId:'quote',original:{minorUnits:1500,currency:'XTR'},discount:{minorUnits:0,currency:'XTR'},bonus:{minorUnits:0,currency:'XTR'},payable:{minorUnits:1500,currency:'XTR'},expiresAt:'2099-01-01T00:00:00Z'};
    if(path==='/v1/payments')return{redirectUri:'https://example.test/invoice'};return[];
  };
  Object.assign(c,{api,setStatus(){},renderProfile(p,a){state.profile=p;state.access=a;},renderIdentities(){},renderDevices(){},renderCapabilities(){},renderDestinations(){}});
  const app=fs.readFileSync(base+'miniapp/app.js','utf8');const start=app.includes('function onAccountRefresh(')?app.indexOf('function onAccountRefresh('):app.indexOf('async function loadAll(');
  vm.createContext(c);vm.runInContext(app.slice(start,app.indexOf('async function establishSession(')),c);
  c.VideoGrabberApi={api,ready:Promise.resolve(),loadAll:c.loadAll,currentAccountId:()=>state.profile?.accountId,isPrimaryAccount:()=>true,setStatus(){},userError:()=> 'Ошибка',onAccountRefresh:c.onAccountRefresh|| (handler=>{c.accountRefreshListeners.add(handler);return()=>c.accountRefreshListeners.delete(handler);})};
  c.Telegram={WebApp:{openInvoice(_,callback){c.invoiceCallback=callback;}}};
  await c.loadAll();vm.runInContext(fs.readFileSync(base+'assets/promotions.js','utf8'),c);
  const source=fs.readFileSync(base+'miniapp/payments.js','utf8').replace('boot();','globalThis.billingBoot=boot();');vm.runInContext('(function(){'+source+'})()',c);await c.billingBoot;
  return{c,elements,get referralCalls(){return referralCalls;},switchAccount(id,amount){account=id;balance=amount;},setBalance(amount){balance=amount;}};
}
test('actual MiniApp loadAll clears old account summary/quote before rendering another account and refetches balances',async()=>{
  const f=await fixture();const card=f.elements.get('#referral-card');assert.match(card.text(),/10,00 ₽/u);
  f.switchAccount('B',2000);const pending=f.c.loadAll();await Promise.resolve();assert.doesNotMatch(card.text(),/10,00 ₽/u);
  await pending;assert.match(card.text(),/20,00 ₽/u);assert.equal(f.referralCalls,2);assert.equal(f.elements.get('#payment-buy').disabled,true);
});
test('actual invoice paid callback refetches summary through central loadAll and resets prior quote',async()=>{
  const f=await fixture();const checkout=f.elements.get('#promotion-checkout');await checkout.all().find(e=>e.tagName==='button').handlers.click();
  await f.elements.get('#payment-buy').handlers.click();assert.equal(typeof f.c.invoiceCallback,'function');f.setBalance(5000);
  await f.c.invoiceCallback('paid');assert.equal(f.referralCalls,2);assert.match(f.elements.get('#referral-card').text(),/50,00 ₽/u);assert.equal(f.elements.get('#payment-buy').disabled,true);
});
