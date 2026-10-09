"use strict";
(() => {
  let data;
  const node = selector => document.querySelector(selector);
  const compact=matchMedia("(max-width:850px)");
  const updateNavigation=()=>{node("#info-navigation").open=!compact.matches;};
  updateNavigation();compact.addEventListener("change",updateNavigation);
  function addLink(parent,text,url,telegram=false){
    const link=document.createElement("a");link.className="button";link.href=url;
    if(url.startsWith("https://")){link.target="_blank";link.rel="noopener noreferrer";}
    if(telegram){const image=document.createElement("img");image.src="/assets/icons/telegram.png";image.alt="";link.append(image);}
    link.append(document.createTextNode(text));parent.append(link);
  }
  function render(){
    const id=new URLSearchParams(location.search).get("document")||"help";
    const doc=data.documents.find(d=>d.id===id);
    node("#document-body").replaceChildren();node("#document-extras").replaceChildren();
    if(!doc){node("#document-title").textContent="Страница не найдена";node("#document-status").textContent="Выберите раздел в меню.";return;}
    document.title=doc.title+" — VideoGrabber";
    node("#document-title").textContent=doc.title;node("#document-version").textContent="Версия "+doc.version;
    node("#document-status").textContent="";
    document.querySelectorAll("#document-nav a").forEach(a=>{if(a.dataset.document===id)a.setAttribute("aria-current","page");else a.removeAttribute("aria-current");});
    for(const item of doc.sections){const section=document.createElement("section"),h=document.createElement("h3"),p=document.createElement("p");h.textContent=item.title;p.textContent=item.body;section.append(h,p);node("#document-body").append(section);}
    if(id==="contacts"){
      const actions=document.createElement("div");actions.className="contact-actions";
      addLink(actions,"Написать в Telegram",data.contacts.telegram,true);addLink(actions,"Сайт Валерия",data.contacts.website);addLink(actions,"Написать на почту","mailto:"+data.contacts.email);node("#document-extras").append(actions);
    }
    if(id==="components")renderComponents();
  }
  async function renderComponents(){
    try{
      const response=await fetch("/info/components.json",{credentials:"omit"});if(!response.ok)return;
      const inventory=await response.json();if(new URLSearchParams(location.search).get("document")!=="components")return;
      const list=document.createElement("div");list.className="components-list";
      for(const item of inventory.components){const card=document.createElement("div");card.className="component";
        const title=document.createElement("strong");title.textContent=item.name;
        const meta=document.createElement("span");meta.textContent=[item.version,item.license].filter(Boolean).join(" · ");card.append(title,meta);
        if(item.notes){const note=document.createElement("p");note.textContent=item.notes;card.append(note);}
        if(item.sourceUrl){const a=document.createElement("a");const u=new URL(item.sourceUrl);if(u.protocol==="https:"){a.href=u.href;a.textContent="Официальный источник ↗";a.target="_blank";a.rel="noopener noreferrer";card.append(document.createElement("br"),a);}}
        list.append(card);
      }
      node("#document-extras").append(list);
    }catch{/* Basic component notice remains available without inventory. */}
  }
  node("#print-document").addEventListener("click",()=>window.print());
  window.addEventListener("popstate",()=>{if(data)render();});
  fetch("/info/content.json",{credentials:"omit",cache:"no-store"}).then(async response=>{
    if(!response.ok)throw new Error();data=await response.json();
    for(const doc of data.documents){const a=document.createElement("a");a.href="/info/?document="+encodeURIComponent(doc.id);a.dataset.document=doc.id;a.textContent=doc.title;
      a.addEventListener("click",event=>{if(event.button||event.ctrlKey||event.metaKey||event.shiftKey)return;event.preventDefault();history.pushState(null,"",a.href);render();if(compact.matches)node("#info-navigation").open=false;node("#document").focus();});node("#document-nav").append(a);}
    render();
  }).catch(()=>{node("#document-title").textContent="Не удалось открыть справку";node("#document-status").textContent="Обновите страницу или напишите на velkoshkin@gmail.com.";});
})();
