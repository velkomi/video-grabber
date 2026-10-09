(function(root,factory){const value=factory();if(typeof module==='object'&&module.exports)module.exports=value;else root.VideoGrabberDocuments=value;})(typeof globalThis!=='undefined'?globalThis:this,function(){
  "use strict";
  function singleFlight(action){
    let running=false;
    return async function(...args){
      if(running)return;
      running=true;
      try{return await action(...args);}finally{running=false;}
    };
  }
  async function confirmCourseRights(api,intentId,checked){
    if(checked!==true)throw new Error("content_rights_confirmation_required");
    const catalog=await api("/v1/documents");
    const terms=(catalog.documents||catalog).find(d=>d.id==="terms");
    if(!terms||typeof terms.version!=="string"||!/^[a-f0-9]{64}$/.test(terms.sha256))throw new Error("documents_unavailable");
    return api("/v1/consents",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({documentId:"terms",version:terms.version,documentHash:terms.sha256,decision:"accepted",purpose:"course_rights",intentId})});
  }
  return Object.freeze({confirmCourseRights,singleFlight});
});
