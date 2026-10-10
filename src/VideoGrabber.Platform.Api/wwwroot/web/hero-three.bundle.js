// VideoGrabber 3D source-sha256:65f2f363a99cc894ea19f27dbcd7840bb0e57c87fad060d34b7190adfd6ece92
var Uh=0,Kl=1,Nh=2;var Ur=1,Fh=2,Rs=3,fi=0,Ut=1,nn=2,Nn=0,Cs=1,Fn=2,Ql=3,jl=4,Oh=5;var Ai=100,Bh=101,zh=102,Hh=103,Vh=104,kh=200,Gh=201,Wh=202,Xh=203,ec=204,tc=205,qh=206,Yh=207,Zh=208,Jh=209,$h=210,Kh=211,Qh=212,jh=213,eu=214,Ra=0,Ca=1,Ia=2,ls=3,Pa=4,La=5,Da=6,Ua=7,nc=0,tu=1,nu=2,vn=0,ic=1,sc=2,rc=3,Nr=4,ac=5,oc=6,lc=7;var cc=300,pi=301,Ri=302,fo=303,po=304,Fr=306,Na=1e3,wn=1001,Fa=1002,Lt=1003,iu=1004;var Or=1005;var At=1006,mo=1007;var On=1008;var $t=1009,hc=1010,uc=1011,Is=1012,go=1013,Mn=1014,ln=1015,Sn=1016,_o=1017,xo=1018,Ps=1020,dc=35902,fc=35899,pc=1021,mc=1022,cn=1023,Cn=1026,mi=1027,yo=1028,vo=1029,gi=1030,Mo=1031;var So=1033,Br=33776,zr=33777,Hr=33778,Vr=33779,bo=35840,Eo=35841,To=35842,wo=35843,Ao=36196,Ro=37492,Co=37496,Io=37488,Po=37489,kr=37490,Lo=37491,Do=37808,Uo=37809,No=37810,Fo=37811,Oo=37812,Bo=37813,zo=37814,Ho=37815,Vo=37816,ko=37817,Go=37818,Wo=37819,Xo=37820,qo=37821,Yo=36492,Zo=36494,Jo=36495,$o=36283,Ko=36284,Gr=36285,Qo=36286;var er=2300,Oa=2301,Ta=2302,Ol=2303,Bl=2400,zl=2401,Hl=2402;var su=3200;var jo=0,ru=1,Qn="",yt="srgb",tr="srgb-linear",nr="linear",dt="srgb";var wa=7680;var au=519,ou=512,lu=513,cu=514,el=515,hu=516,uu=517,tl=518,du=519,gc=35044;var _c="300 es",gn=2e3,cs=2001;function rd(i){for(let e=i.length-1;e>=0;--e)if(i[e]>=65535)return!0;return!1}function ad(i){return ArrayBuffer.isView(i)&&!(i instanceof DataView)}function hs(i){return document.createElementNS("http://www.w3.org/1999/xhtml",i)}function fu(){let i=hs("canvas");return i.style.display="block",i}var qc={},us=null;function ir(...i){let e="THREE."+i.shift();us?us("log",e,...i):console.log(e,...i)}function pu(i){let e=i[0];if(typeof e=="string"&&e.startsWith("TSL:")){let t=i[1];t&&t.isStackTrace?i[0]+=" "+t.getLocation():i[1]='Stack trace not available. Enable "THREE.Node.captureStackTrace" to capture stack traces.'}return i}function ke(...i){i=pu(i);let e="THREE."+i.shift();if(us)us("warn",e,...i);else{let t=i[0];t&&t.isStackTrace?console.warn(t.getError(e)):console.warn(e,...i)}}function We(...i){i=pu(i);let e="THREE."+i.shift();if(us)us("error",e,...i);else{let t=i[0];t&&t.isStackTrace?console.error(t.getError(e)):console.error(e,...i)}}function Si(...i){let e=i.join(" ");e in qc||(qc[e]=!0,ke(...i))}function mu(i,e,t){return new Promise(function(n,s){function r(){switch(i.clientWaitSync(e,i.SYNC_FLUSH_COMMANDS_BIT,0)){case i.WAIT_FAILED:s();break;case i.TIMEOUT_EXPIRED:setTimeout(r,t);break;default:n()}}setTimeout(r,t)})}var gu={[Ra]:Ca,[Ia]:Da,[Pa]:Ua,[ls]:La,[Ca]:Ra,[Da]:Ia,[Ua]:Pa,[La]:ls},In=class{addEventListener(e,t){this._listeners===void 0&&(this._listeners={});let n=this._listeners;n[e]===void 0&&(n[e]=[]),n[e].indexOf(t)===-1&&n[e].push(t)}hasEventListener(e,t){let n=this._listeners;return n===void 0?!1:n[e]!==void 0&&n[e].indexOf(t)!==-1}removeEventListener(e,t){let n=this._listeners;if(n===void 0)return;let s=n[e];if(s!==void 0){let r=s.indexOf(t);r!==-1&&s.splice(r,1)}}dispatchEvent(e){let t=this._listeners;if(t===void 0)return;let n=t[e.type];if(n!==void 0){e.target=this;let s=n.slice(0);for(let r=0,a=s.length;r<a;r++)s[r].call(this,e);e.target=null}}},Bt=["00","01","02","03","04","05","06","07","08","09","0a","0b","0c","0d","0e","0f","10","11","12","13","14","15","16","17","18","19","1a","1b","1c","1d","1e","1f","20","21","22","23","24","25","26","27","28","29","2a","2b","2c","2d","2e","2f","30","31","32","33","34","35","36","37","38","39","3a","3b","3c","3d","3e","3f","40","41","42","43","44","45","46","47","48","49","4a","4b","4c","4d","4e","4f","50","51","52","53","54","55","56","57","58","59","5a","5b","5c","5d","5e","5f","60","61","62","63","64","65","66","67","68","69","6a","6b","6c","6d","6e","6f","70","71","72","73","74","75","76","77","78","79","7a","7b","7c","7d","7e","7f","80","81","82","83","84","85","86","87","88","89","8a","8b","8c","8d","8e","8f","90","91","92","93","94","95","96","97","98","99","9a","9b","9c","9d","9e","9f","a0","a1","a2","a3","a4","a5","a6","a7","a8","a9","aa","ab","ac","ad","ae","af","b0","b1","b2","b3","b4","b5","b6","b7","b8","b9","ba","bb","bc","bd","be","bf","c0","c1","c2","c3","c4","c5","c6","c7","c8","c9","ca","cb","cc","cd","ce","cf","d0","d1","d2","d3","d4","d5","d6","d7","d8","d9","da","db","dc","dd","de","df","e0","e1","e2","e3","e4","e5","e6","e7","e8","e9","ea","eb","ec","ed","ee","ef","f0","f1","f2","f3","f4","f5","f6","f7","f8","f9","fa","fb","fc","fd","fe","ff"],Yc=1234567,$s=Math.PI/180,ds=180/Math.PI;function Rn(){let i=Math.random()*4294967295|0,e=Math.random()*4294967295|0,t=Math.random()*4294967295|0,n=Math.random()*4294967295|0;return(Bt[i&255]+Bt[i>>8&255]+Bt[i>>16&255]+Bt[i>>24&255]+"-"+Bt[e&255]+Bt[e>>8&255]+"-"+Bt[e>>16&15|64]+Bt[e>>24&255]+"-"+Bt[t&63|128]+Bt[t>>8&255]+"-"+Bt[t>>16&255]+Bt[t>>24&255]+Bt[n&255]+Bt[n>>8&255]+Bt[n>>16&255]+Bt[n>>24&255]).toLowerCase()}function nt(i,e,t){return Math.max(e,Math.min(t,i))}function xc(i,e){return(i%e+e)%e}function od(i,e,t,n,s){return n+(i-e)*(s-n)/(t-e)}function ld(i,e,t){return i!==e?(t-i)/(e-i):0}function Ks(i,e,t){return(1-t)*i+t*e}function cd(i,e,t,n){return Ks(i,e,1-Math.exp(-t*n))}function hd(i,e=1){return e-Math.abs(xc(i,e*2)-e)}function ud(i,e,t){return i<=e?0:i>=t?1:(i=(i-e)/(t-e),i*i*(3-2*i))}function dd(i,e,t){return i<=e?0:i>=t?1:(i=(i-e)/(t-e),i*i*i*(i*(i*6-15)+10))}function fd(i,e){return i+Math.floor(Math.random()*(e-i+1))}function pd(i,e){return i+Math.random()*(e-i)}function md(i){return i*(.5-Math.random())}function gd(i){i!==void 0&&(Yc=i);let e=Yc+=1831565813;return e=Math.imul(e^e>>>15,e|1),e^=e+Math.imul(e^e>>>7,e|61),((e^e>>>14)>>>0)/4294967296}function _d(i){return i*$s}function xd(i){return i*ds}function yd(i){return i>0&&Number.isInteger(i)&&2**Math.round(Math.log2(i))===i}function vd(i){return Math.pow(2,Math.ceil(Math.log(i)/Math.LN2))}function Md(i){return Math.pow(2,Math.floor(Math.log(i)/Math.LN2))}function Sd(i,e,t,n,s){let r=Math.cos,a=Math.sin,o=r(t/2),c=a(t/2),l=r((e+n)/2),h=a((e+n)/2),d=r((e-n)/2),u=a((e-n)/2),p=r((n-e)/2),g=a((n-e)/2);switch(s){case"XYX":i.set(o*h,c*d,c*u,o*l);break;case"YZY":i.set(c*u,o*h,c*d,o*l);break;case"ZXZ":i.set(c*d,c*u,o*h,o*l);break;case"XZX":i.set(o*h,c*g,c*p,o*l);break;case"YXY":i.set(c*p,o*h,c*g,o*l);break;case"ZYZ":i.set(c*g,c*p,o*h,o*l);break;default:ke("MathUtils: .setQuaternionFromProperEuler() encountered an unknown order: "+s)}}function mn(i,e){switch(e.constructor){case Float32Array:return i;case Uint32Array:return i/4294967295;case Uint16Array:return i/65535;case Uint8Array:case Uint8ClampedArray:return i/255;case Int32Array:return Math.max(i/2147483647,-1);case Int16Array:return Math.max(i/32767,-1);case Int8Array:return Math.max(i/127,-1);default:throw new Error("THREE.MathUtils: Invalid component type.")}}function pt(i,e){switch(e.constructor){case Float32Array:return i;case Uint32Array:return Math.round(i*4294967295);case Uint16Array:return Math.round(i*65535);case Uint8Array:case Uint8ClampedArray:return Math.round(i*255);case Int32Array:return Math.round(i*2147483647);case Int16Array:return Math.round(i*32767);case Int8Array:return Math.round(i*127);default:throw new Error("THREE.MathUtils: Invalid component type.")}}var ht={DEG2RAD:$s,RAD2DEG:ds,generateUUID:Rn,clamp:nt,euclideanModulo:xc,mapLinear:od,inverseLerp:ld,lerp:Ks,damp:cd,pingpong:hd,smoothstep:ud,smootherstep:dd,randInt:fd,randFloat:pd,randFloatSpread:md,seededRandom:gd,degToRad:_d,radToDeg:xd,isPowerOfTwo:yd,ceilPowerOfTwo:vd,floorPowerOfTwo:Md,setQuaternionFromProperEuler:Sd,normalize:pt,denormalize:mn},he=class i{static{i.prototype.isVector2=!0}constructor(e=0,t=0){this.x=e,this.y=t}get width(){return this.x}set width(e){this.x=e}get height(){return this.y}set height(e){this.y=e}set(e,t){return this.x=e,this.y=t,this}setScalar(e){return this.x=e,this.y=e,this}setX(e){return this.x=e,this}setY(e){return this.y=e,this}setComponent(e,t){switch(e){case 0:this.x=t;break;case 1:this.y=t;break;default:throw new Error("THREE.Vector2: index is out of range: "+e)}return this}getComponent(e){switch(e){case 0:return this.x;case 1:return this.y;default:throw new Error("THREE.Vector2: index is out of range: "+e)}}clone(){return new this.constructor(this.x,this.y)}copy(e){return this.x=e.x,this.y=e.y,this}add(e){return this.x+=e.x,this.y+=e.y,this}addScalar(e){return this.x+=e,this.y+=e,this}addVectors(e,t){return this.x=e.x+t.x,this.y=e.y+t.y,this}addScaledVector(e,t){return this.x+=e.x*t,this.y+=e.y*t,this}sub(e){return this.x-=e.x,this.y-=e.y,this}subScalar(e){return this.x-=e,this.y-=e,this}subVectors(e,t){return this.x=e.x-t.x,this.y=e.y-t.y,this}multiply(e){return this.x*=e.x,this.y*=e.y,this}multiplyScalar(e){return this.x*=e,this.y*=e,this}divide(e){return this.x/=e.x,this.y/=e.y,this}divideScalar(e){return this.multiplyScalar(1/e)}applyMatrix3(e){let t=this.x,n=this.y,s=e.elements;return this.x=s[0]*t+s[3]*n+s[6],this.y=s[1]*t+s[4]*n+s[7],this}min(e){return this.x=Math.min(this.x,e.x),this.y=Math.min(this.y,e.y),this}max(e){return this.x=Math.max(this.x,e.x),this.y=Math.max(this.y,e.y),this}clamp(e,t){return this.x=nt(this.x,e.x,t.x),this.y=nt(this.y,e.y,t.y),this}clampScalar(e,t){return this.x=nt(this.x,e,t),this.y=nt(this.y,e,t),this}clampLength(e,t){let n=this.length();return this.divideScalar(n||1).multiplyScalar(nt(n,e,t))}floor(){return this.x=Math.floor(this.x),this.y=Math.floor(this.y),this}ceil(){return this.x=Math.ceil(this.x),this.y=Math.ceil(this.y),this}round(){return this.x=Math.round(this.x),this.y=Math.round(this.y),this}roundToZero(){return this.x=Math.trunc(this.x),this.y=Math.trunc(this.y),this}negate(){return this.x=-this.x,this.y=-this.y,this}dot(e){return this.x*e.x+this.y*e.y}cross(e){return this.x*e.y-this.y*e.x}lengthSq(){return this.x*this.x+this.y*this.y}length(){return Math.sqrt(this.x*this.x+this.y*this.y)}manhattanLength(){return Math.abs(this.x)+Math.abs(this.y)}normalize(){return this.divideScalar(this.length()||1)}angle(){return Math.atan2(-this.y,-this.x)+Math.PI}angleTo(e){let t=Math.sqrt(this.lengthSq()*e.lengthSq());if(t===0)return Math.PI/2;let n=this.dot(e)/t;return Math.acos(nt(n,-1,1))}distanceTo(e){return Math.sqrt(this.distanceToSquared(e))}distanceToSquared(e){let t=this.x-e.x,n=this.y-e.y;return t*t+n*n}manhattanDistanceTo(e){return Math.abs(this.x-e.x)+Math.abs(this.y-e.y)}setLength(e){return this.normalize().multiplyScalar(e)}lerp(e,t){return this.x+=(e.x-this.x)*t,this.y+=(e.y-this.y)*t,this}lerpVectors(e,t,n){return this.x=e.x+(t.x-e.x)*n,this.y=e.y+(t.y-e.y)*n,this}equals(e){return e.x===this.x&&e.y===this.y}fromArray(e,t=0){return this.x=e[t],this.y=e[t+1],this}toArray(e=[],t=0){return e[t]=this.x,e[t+1]=this.y,e}fromBufferAttribute(e,t){return this.x=e.getX(t),this.y=e.getY(t),this}rotateAround(e,t){let n=Math.cos(t),s=Math.sin(t),r=this.x-e.x,a=this.y-e.y;return this.x=r*n-a*s+e.x,this.y=r*s+a*n+e.y,this}random(){return this.x=Math.random(),this.y=Math.random(),this}*[Symbol.iterator](){yield this.x,yield this.y}},Pn=class{constructor(e=0,t=0,n=0,s=1){this.isQuaternion=!0,this._x=e,this._y=t,this._z=n,this._w=s}static slerpFlat(e,t,n,s,r,a,o){let c=n[s+0],l=n[s+1],h=n[s+2],d=n[s+3],u=r[a+0],p=r[a+1],g=r[a+2],v=r[a+3];if(d!==v||c!==u||l!==p||h!==g){let m=c*u+l*p+h*g+d*v;m<0&&(u=-u,p=-p,g=-g,v=-v,m=-m);let f=1-o;if(m<.9995){let S=Math.acos(m),R=Math.sin(S);f=Math.sin(f*S)/R,o=Math.sin(o*S)/R,c=c*f+u*o,l=l*f+p*o,h=h*f+g*o,d=d*f+v*o}else{c=c*f+u*o,l=l*f+p*o,h=h*f+g*o,d=d*f+v*o;let S=1/Math.sqrt(c*c+l*l+h*h+d*d);c*=S,l*=S,h*=S,d*=S}}e[t]=c,e[t+1]=l,e[t+2]=h,e[t+3]=d}static multiplyQuaternionsFlat(e,t,n,s,r,a){let o=n[s],c=n[s+1],l=n[s+2],h=n[s+3],d=r[a],u=r[a+1],p=r[a+2],g=r[a+3];return e[t]=o*g+h*d+c*p-l*u,e[t+1]=c*g+h*u+l*d-o*p,e[t+2]=l*g+h*p+o*u-c*d,e[t+3]=h*g-o*d-c*u-l*p,e}get x(){return this._x}set x(e){this._x=e,this._onChangeCallback()}get y(){return this._y}set y(e){this._y=e,this._onChangeCallback()}get z(){return this._z}set z(e){this._z=e,this._onChangeCallback()}get w(){return this._w}set w(e){this._w=e,this._onChangeCallback()}set(e,t,n,s){return this._x=e,this._y=t,this._z=n,this._w=s,this._onChangeCallback(),this}clone(){return new this.constructor(this._x,this._y,this._z,this._w)}copy(e){return this._x=e.x,this._y=e.y,this._z=e.z,this._w=e.w,this._onChangeCallback(),this}setFromEuler(e,t=!0){let n=e._x,s=e._y,r=e._z,a=e._order,o=Math.cos,c=Math.sin,l=o(n/2),h=o(s/2),d=o(r/2),u=c(n/2),p=c(s/2),g=c(r/2);switch(a){case"XYZ":this._x=u*h*d+l*p*g,this._y=l*p*d-u*h*g,this._z=l*h*g+u*p*d,this._w=l*h*d-u*p*g;break;case"YXZ":this._x=u*h*d+l*p*g,this._y=l*p*d-u*h*g,this._z=l*h*g-u*p*d,this._w=l*h*d+u*p*g;break;case"ZXY":this._x=u*h*d-l*p*g,this._y=l*p*d+u*h*g,this._z=l*h*g+u*p*d,this._w=l*h*d-u*p*g;break;case"ZYX":this._x=u*h*d-l*p*g,this._y=l*p*d+u*h*g,this._z=l*h*g-u*p*d,this._w=l*h*d+u*p*g;break;case"YZX":this._x=u*h*d+l*p*g,this._y=l*p*d+u*h*g,this._z=l*h*g-u*p*d,this._w=l*h*d-u*p*g;break;case"XZY":this._x=u*h*d-l*p*g,this._y=l*p*d-u*h*g,this._z=l*h*g+u*p*d,this._w=l*h*d+u*p*g;break;default:ke("Quaternion: .setFromEuler() encountered an unknown order: "+a)}return t===!0&&this._onChangeCallback(),this}setFromAxisAngle(e,t){let n=t/2,s=Math.sin(n);return this._x=e.x*s,this._y=e.y*s,this._z=e.z*s,this._w=Math.cos(n),this._onChangeCallback(),this}setFromRotationMatrix(e){let t=e.elements,n=t[0],s=t[4],r=t[8],a=t[1],o=t[5],c=t[9],l=t[2],h=t[6],d=t[10],u=n+o+d;if(u>0){let p=.5/Math.sqrt(u+1);this._w=.25/p,this._x=(h-c)*p,this._y=(r-l)*p,this._z=(a-s)*p}else if(n>o&&n>d){let p=2*Math.sqrt(1+n-o-d);this._w=(h-c)/p,this._x=.25*p,this._y=(s+a)/p,this._z=(r+l)/p}else if(o>d){let p=2*Math.sqrt(1+o-n-d);this._w=(r-l)/p,this._x=(s+a)/p,this._y=.25*p,this._z=(c+h)/p}else{let p=2*Math.sqrt(1+d-n-o);this._w=(a-s)/p,this._x=(r+l)/p,this._y=(c+h)/p,this._z=.25*p}return this._onChangeCallback(),this}setFromUnitVectors(e,t){let n=e.dot(t)+1;return n<1e-8?(n=0,Math.abs(e.x)>Math.abs(e.z)?(this._x=-e.y,this._y=e.x,this._z=0,this._w=n):(this._x=0,this._y=-e.z,this._z=e.y,this._w=n)):(this._x=e.y*t.z-e.z*t.y,this._y=e.z*t.x-e.x*t.z,this._z=e.x*t.y-e.y*t.x,this._w=n),this.normalize()}angleTo(e){return 2*Math.acos(Math.abs(nt(this.dot(e),-1,1)))}rotateTowards(e,t){let n=this.angleTo(e);if(n===0)return this;let s=Math.min(1,t/n);return this.slerp(e,s),this}identity(){return this.set(0,0,0,1)}invert(){return this.conjugate()}conjugate(){return this._x*=-1,this._y*=-1,this._z*=-1,this._onChangeCallback(),this}dot(e){return this._x*e._x+this._y*e._y+this._z*e._z+this._w*e._w}lengthSq(){return this._x*this._x+this._y*this._y+this._z*this._z+this._w*this._w}length(){return Math.sqrt(this._x*this._x+this._y*this._y+this._z*this._z+this._w*this._w)}normalize(){let e=this.length();return e===0?(this._x=0,this._y=0,this._z=0,this._w=1):(e=1/e,this._x=this._x*e,this._y=this._y*e,this._z=this._z*e,this._w=this._w*e),this._onChangeCallback(),this}multiply(e){return this.multiplyQuaternions(this,e)}premultiply(e){return this.multiplyQuaternions(e,this)}multiplyQuaternions(e,t){let n=e._x,s=e._y,r=e._z,a=e._w,o=t._x,c=t._y,l=t._z,h=t._w;return this._x=n*h+a*o+s*l-r*c,this._y=s*h+a*c+r*o-n*l,this._z=r*h+a*l+n*c-s*o,this._w=a*h-n*o-s*c-r*l,this._onChangeCallback(),this}slerp(e,t){let n=e._x,s=e._y,r=e._z,a=e._w,o=this.dot(e);o<0&&(n=-n,s=-s,r=-r,a=-a,o=-o);let c=1-t;if(o<.9995){let l=Math.acos(o),h=Math.sin(l);c=Math.sin(c*l)/h,t=Math.sin(t*l)/h,this._x=this._x*c+n*t,this._y=this._y*c+s*t,this._z=this._z*c+r*t,this._w=this._w*c+a*t,this._onChangeCallback()}else this._x=this._x*c+n*t,this._y=this._y*c+s*t,this._z=this._z*c+r*t,this._w=this._w*c+a*t,this.normalize();return this}slerpQuaternions(e,t,n){return this.copy(e).slerp(t,n)}random(){let e=2*Math.PI*Math.random(),t=2*Math.PI*Math.random(),n=Math.random(),s=Math.sqrt(1-n),r=Math.sqrt(n);return this.set(s*Math.sin(e),s*Math.cos(e),r*Math.sin(t),r*Math.cos(t))}equals(e){return e._x===this._x&&e._y===this._y&&e._z===this._z&&e._w===this._w}fromArray(e,t=0){return this._x=e[t],this._y=e[t+1],this._z=e[t+2],this._w=e[t+3],this._onChangeCallback(),this}toArray(e=[],t=0){return e[t]=this._x,e[t+1]=this._y,e[t+2]=this._z,e[t+3]=this._w,e}fromBufferAttribute(e,t){return this._x=e.getX(t),this._y=e.getY(t),this._z=e.getZ(t),this._w=e.getW(t),this._onChangeCallback(),this}toJSON(){return this.toArray()}_onChange(e){return this._onChangeCallback=e,this}_onChangeCallback(){}*[Symbol.iterator](){yield this._x,yield this._y,yield this._z,yield this._w}},L=class i{static{i.prototype.isVector3=!0}constructor(e=0,t=0,n=0){this.x=e,this.y=t,this.z=n}set(e,t,n){return n===void 0&&(n=this.z),this.x=e,this.y=t,this.z=n,this}setScalar(e){return this.x=e,this.y=e,this.z=e,this}setX(e){return this.x=e,this}setY(e){return this.y=e,this}setZ(e){return this.z=e,this}setComponent(e,t){switch(e){case 0:this.x=t;break;case 1:this.y=t;break;case 2:this.z=t;break;default:throw new Error("THREE.Vector3: index is out of range: "+e)}return this}getComponent(e){switch(e){case 0:return this.x;case 1:return this.y;case 2:return this.z;default:throw new Error("THREE.Vector3: index is out of range: "+e)}}clone(){return new this.constructor(this.x,this.y,this.z)}copy(e){return this.x=e.x,this.y=e.y,this.z=e.z,this}add(e){return this.x+=e.x,this.y+=e.y,this.z+=e.z,this}addScalar(e){return this.x+=e,this.y+=e,this.z+=e,this}addVectors(e,t){return this.x=e.x+t.x,this.y=e.y+t.y,this.z=e.z+t.z,this}addScaledVector(e,t){return this.x+=e.x*t,this.y+=e.y*t,this.z+=e.z*t,this}sub(e){return this.x-=e.x,this.y-=e.y,this.z-=e.z,this}subScalar(e){return this.x-=e,this.y-=e,this.z-=e,this}subVectors(e,t){return this.x=e.x-t.x,this.y=e.y-t.y,this.z=e.z-t.z,this}multiply(e){return this.x*=e.x,this.y*=e.y,this.z*=e.z,this}multiplyScalar(e){return this.x*=e,this.y*=e,this.z*=e,this}multiplyVectors(e,t){return this.x=e.x*t.x,this.y=e.y*t.y,this.z=e.z*t.z,this}applyEuler(e){return this.applyQuaternion(Zc.setFromEuler(e))}applyAxisAngle(e,t){return this.applyQuaternion(Zc.setFromAxisAngle(e,t))}applyMatrix3(e){let t=this.x,n=this.y,s=this.z,r=e.elements;return this.x=r[0]*t+r[3]*n+r[6]*s,this.y=r[1]*t+r[4]*n+r[7]*s,this.z=r[2]*t+r[5]*n+r[8]*s,this}applyNormalMatrix(e){return this.applyMatrix3(e).normalize()}applyMatrix4(e){let t=this.x,n=this.y,s=this.z,r=e.elements,a=1/(r[3]*t+r[7]*n+r[11]*s+r[15]);return this.x=(r[0]*t+r[4]*n+r[8]*s+r[12])*a,this.y=(r[1]*t+r[5]*n+r[9]*s+r[13])*a,this.z=(r[2]*t+r[6]*n+r[10]*s+r[14])*a,this}applyQuaternion(e){let t=this.x,n=this.y,s=this.z,r=e.x,a=e.y,o=e.z,c=e.w,l=2*(a*s-o*n),h=2*(o*t-r*s),d=2*(r*n-a*t);return this.x=t+c*l+a*d-o*h,this.y=n+c*h+o*l-r*d,this.z=s+c*d+r*h-a*l,this}project(e){return this.applyMatrix4(e.matrixWorldInverse).applyMatrix4(e.projectionMatrix)}unproject(e){return this.applyMatrix4(e.projectionMatrixInverse).applyMatrix4(e.matrixWorld)}transformDirection(e){let t=this.x,n=this.y,s=this.z,r=e.elements;return this.x=r[0]*t+r[4]*n+r[8]*s,this.y=r[1]*t+r[5]*n+r[9]*s,this.z=r[2]*t+r[6]*n+r[10]*s,this.normalize()}divide(e){return this.x/=e.x,this.y/=e.y,this.z/=e.z,this}divideScalar(e){return this.multiplyScalar(1/e)}min(e){return this.x=Math.min(this.x,e.x),this.y=Math.min(this.y,e.y),this.z=Math.min(this.z,e.z),this}max(e){return this.x=Math.max(this.x,e.x),this.y=Math.max(this.y,e.y),this.z=Math.max(this.z,e.z),this}clamp(e,t){return this.x=nt(this.x,e.x,t.x),this.y=nt(this.y,e.y,t.y),this.z=nt(this.z,e.z,t.z),this}clampScalar(e,t){return this.x=nt(this.x,e,t),this.y=nt(this.y,e,t),this.z=nt(this.z,e,t),this}clampLength(e,t){let n=this.length();return this.divideScalar(n||1).multiplyScalar(nt(n,e,t))}floor(){return this.x=Math.floor(this.x),this.y=Math.floor(this.y),this.z=Math.floor(this.z),this}ceil(){return this.x=Math.ceil(this.x),this.y=Math.ceil(this.y),this.z=Math.ceil(this.z),this}round(){return this.x=Math.round(this.x),this.y=Math.round(this.y),this.z=Math.round(this.z),this}roundToZero(){return this.x=Math.trunc(this.x),this.y=Math.trunc(this.y),this.z=Math.trunc(this.z),this}negate(){return this.x=-this.x,this.y=-this.y,this.z=-this.z,this}dot(e){return this.x*e.x+this.y*e.y+this.z*e.z}lengthSq(){return this.x*this.x+this.y*this.y+this.z*this.z}length(){return Math.sqrt(this.x*this.x+this.y*this.y+this.z*this.z)}manhattanLength(){return Math.abs(this.x)+Math.abs(this.y)+Math.abs(this.z)}normalize(){return this.divideScalar(this.length()||1)}setLength(e){return this.normalize().multiplyScalar(e)}lerp(e,t){return this.x+=(e.x-this.x)*t,this.y+=(e.y-this.y)*t,this.z+=(e.z-this.z)*t,this}lerpVectors(e,t,n){return this.x=e.x+(t.x-e.x)*n,this.y=e.y+(t.y-e.y)*n,this.z=e.z+(t.z-e.z)*n,this}cross(e){return this.crossVectors(this,e)}crossVectors(e,t){let n=e.x,s=e.y,r=e.z,a=t.x,o=t.y,c=t.z;return this.x=s*c-r*o,this.y=r*a-n*c,this.z=n*o-s*a,this}projectOnVector(e){let t=e.lengthSq();if(t===0)return this.set(0,0,0);let n=e.dot(this)/t;return this.copy(e).multiplyScalar(n)}projectOnPlane(e){return ul.copy(this).projectOnVector(e),this.sub(ul)}reflect(e){return this.sub(ul.copy(e).multiplyScalar(2*this.dot(e)))}angleTo(e){let t=Math.sqrt(this.lengthSq()*e.lengthSq());if(t===0)return Math.PI/2;let n=this.dot(e)/t;return Math.acos(nt(n,-1,1))}distanceTo(e){return Math.sqrt(this.distanceToSquared(e))}distanceToSquared(e){let t=this.x-e.x,n=this.y-e.y,s=this.z-e.z;return t*t+n*n+s*s}manhattanDistanceTo(e){return Math.abs(this.x-e.x)+Math.abs(this.y-e.y)+Math.abs(this.z-e.z)}setFromSpherical(e){return this.setFromSphericalCoords(e.radius,e.phi,e.theta)}setFromSphericalCoords(e,t,n){let s=Math.sin(t)*e;return this.x=s*Math.sin(n),this.y=Math.cos(t)*e,this.z=s*Math.cos(n),this}setFromCylindrical(e){return this.setFromCylindricalCoords(e.radius,e.theta,e.y)}setFromCylindricalCoords(e,t,n){return this.x=e*Math.sin(t),this.y=n,this.z=e*Math.cos(t),this}setFromMatrixPosition(e){let t=e.elements;return this.x=t[12],this.y=t[13],this.z=t[14],this}setFromMatrixScale(e){let t=this.setFromMatrixColumn(e,0).length(),n=this.setFromMatrixColumn(e,1).length(),s=this.setFromMatrixColumn(e,2).length();return this.x=t,this.y=n,this.z=s,this}setFromMatrixColumn(e,t){return this.fromArray(e.elements,t*4)}setFromMatrix3Column(e,t){return this.fromArray(e.elements,t*3)}setFromEuler(e){return this.x=e._x,this.y=e._y,this.z=e._z,this}setFromColor(e){return this.x=e.r,this.y=e.g,this.z=e.b,this}equals(e){return e.x===this.x&&e.y===this.y&&e.z===this.z}fromArray(e,t=0){return this.x=e[t],this.y=e[t+1],this.z=e[t+2],this}toArray(e=[],t=0){return e[t]=this.x,e[t+1]=this.y,e[t+2]=this.z,e}fromBufferAttribute(e,t){return this.x=e.getX(t),this.y=e.getY(t),this.z=e.getZ(t),this}random(){return this.x=Math.random(),this.y=Math.random(),this.z=Math.random(),this}randomDirection(){let e=Math.random()*Math.PI*2,t=Math.random()*2-1,n=Math.sqrt(1-t*t);return this.x=n*Math.cos(e),this.y=t,this.z=n*Math.sin(e),this}*[Symbol.iterator](){yield this.x,yield this.y,yield this.z}},ul=new L,Zc=new Pn,Je=class i{static{i.prototype.isMatrix3=!0}constructor(e,t,n,s,r,a,o,c,l){this.elements=[1,0,0,0,1,0,0,0,1],e!==void 0&&this.set(e,t,n,s,r,a,o,c,l)}set(e,t,n,s,r,a,o,c,l){let h=this.elements;return h[0]=e,h[1]=s,h[2]=o,h[3]=t,h[4]=r,h[5]=c,h[6]=n,h[7]=a,h[8]=l,this}identity(){return this.set(1,0,0,0,1,0,0,0,1),this}copy(e){let t=this.elements,n=e.elements;return t[0]=n[0],t[1]=n[1],t[2]=n[2],t[3]=n[3],t[4]=n[4],t[5]=n[5],t[6]=n[6],t[7]=n[7],t[8]=n[8],this}extractBasis(e,t,n){return e.setFromMatrix3Column(this,0),t.setFromMatrix3Column(this,1),n.setFromMatrix3Column(this,2),this}setFromMatrix4(e){let t=e.elements;return this.set(t[0],t[4],t[8],t[1],t[5],t[9],t[2],t[6],t[10]),this}multiply(e){return this.multiplyMatrices(this,e)}premultiply(e){return this.multiplyMatrices(e,this)}multiplyMatrices(e,t){let n=e.elements,s=t.elements,r=this.elements,a=n[0],o=n[3],c=n[6],l=n[1],h=n[4],d=n[7],u=n[2],p=n[5],g=n[8],v=s[0],m=s[3],f=s[6],S=s[1],R=s[4],M=s[7],T=s[2],w=s[5],P=s[8];return r[0]=a*v+o*S+c*T,r[3]=a*m+o*R+c*w,r[6]=a*f+o*M+c*P,r[1]=l*v+h*S+d*T,r[4]=l*m+h*R+d*w,r[7]=l*f+h*M+d*P,r[2]=u*v+p*S+g*T,r[5]=u*m+p*R+g*w,r[8]=u*f+p*M+g*P,this}multiplyScalar(e){let t=this.elements;return t[0]*=e,t[3]*=e,t[6]*=e,t[1]*=e,t[4]*=e,t[7]*=e,t[2]*=e,t[5]*=e,t[8]*=e,this}determinant(){let e=this.elements,t=e[0],n=e[1],s=e[2],r=e[3],a=e[4],o=e[5],c=e[6],l=e[7],h=e[8];return t*a*h-t*o*l-n*r*h+n*o*c+s*r*l-s*a*c}invert(){let e=this.elements,t=e[0],n=e[1],s=e[2],r=e[3],a=e[4],o=e[5],c=e[6],l=e[7],h=e[8],d=h*a-o*l,u=o*c-h*r,p=l*r-a*c,g=t*d+n*u+s*p;if(g===0)return this.set(0,0,0,0,0,0,0,0,0);let v=1/g;return e[0]=d*v,e[1]=(s*l-h*n)*v,e[2]=(o*n-s*a)*v,e[3]=u*v,e[4]=(h*t-s*c)*v,e[5]=(s*r-o*t)*v,e[6]=p*v,e[7]=(n*c-l*t)*v,e[8]=(a*t-n*r)*v,this}transpose(){let e,t=this.elements;return e=t[1],t[1]=t[3],t[3]=e,e=t[2],t[2]=t[6],t[6]=e,e=t[5],t[5]=t[7],t[7]=e,this}getNormalMatrix(e){return this.setFromMatrix4(e).invert().transpose()}transposeIntoArray(e){let t=this.elements;return e[0]=t[0],e[1]=t[3],e[2]=t[6],e[3]=t[1],e[4]=t[4],e[5]=t[7],e[6]=t[2],e[7]=t[5],e[8]=t[8],this}setUvTransform(e,t,n,s,r,a,o){let c=Math.cos(r),l=Math.sin(r);return this.set(n*c,n*l,-n*(c*a+l*o)+a+e,-s*l,s*c,-s*(-l*a+c*o)+o+t,0,0,1),this}scale(e,t){return Si("Matrix3: .scale() is deprecated. Use .makeScale() instead."),this.premultiply(dl.makeScale(e,t)),this}rotate(e){return Si("Matrix3: .rotate() is deprecated. Use .makeRotation() instead."),this.premultiply(dl.makeRotation(-e)),this}translate(e,t){return Si("Matrix3: .translate() is deprecated. Use .makeTranslation() instead."),this.premultiply(dl.makeTranslation(e,t)),this}makeTranslation(e,t){return e.isVector2?this.set(1,0,e.x,0,1,e.y,0,0,1):this.set(1,0,e,0,1,t,0,0,1),this}makeRotation(e){let t=Math.cos(e),n=Math.sin(e);return this.set(t,-n,0,n,t,0,0,0,1),this}makeScale(e,t){return this.set(e,0,0,0,t,0,0,0,1),this}equals(e){let t=this.elements,n=e.elements;for(let s=0;s<9;s++)if(t[s]!==n[s])return!1;return!0}fromArray(e,t=0){for(let n=0;n<9;n++)this.elements[n]=e[n+t];return this}toArray(e=[],t=0){let n=this.elements;return e[t]=n[0],e[t+1]=n[1],e[t+2]=n[2],e[t+3]=n[3],e[t+4]=n[4],e[t+5]=n[5],e[t+6]=n[6],e[t+7]=n[7],e[t+8]=n[8],e}clone(){return new this.constructor().fromArray(this.elements)}},dl=new Je,Jc=new Je().set(.4123908,.3575843,.1804808,.212639,.7151687,.0721923,.0193308,.1191948,.9505322),$c=new Je().set(3.2409699,-1.5373832,-.4986108,-.9692436,1.8759675,.0415551,.0556301,-.203977,1.0569715);function bd(){let i={enabled:!0,workingColorSpace:tr,spaces:{},convert:function(s,r,a){return this.enabled===!1||r===a||!r||!a||(this.spaces[r].transfer===dt&&(s.r=Jn(s.r),s.g=Jn(s.g),s.b=Jn(s.b)),this.spaces[r].primaries!==this.spaces[a].primaries&&(s.applyMatrix3(this.spaces[r].toXYZ),s.applyMatrix3(this.spaces[a].fromXYZ)),this.spaces[a].transfer===dt&&(s.r=os(s.r),s.g=os(s.g),s.b=os(s.b))),s},workingToColorSpace:function(s,r){return this.convert(s,this.workingColorSpace,r)},colorSpaceToWorking:function(s,r){return this.convert(s,r,this.workingColorSpace)},getPrimaries:function(s){return this.spaces[s].primaries},getTransfer:function(s){return s===Qn?nr:this.spaces[s].transfer},getToneMappingMode:function(s){return this.spaces[s].outputColorSpaceConfig.toneMappingMode||"standard"},getLuminanceCoefficients:function(s,r=this.workingColorSpace){return s.fromArray(this.spaces[r].luminanceCoefficients)},define:function(s){Object.assign(this.spaces,s)},_getMatrix:function(s,r,a){return s.copy(this.spaces[r].toXYZ).multiply(this.spaces[a].fromXYZ)},_getDrawingBufferColorSpace:function(s){return this.spaces[s].outputColorSpaceConfig.drawingBufferColorSpace},_getUnpackColorSpace:function(s=this.workingColorSpace){return this.spaces[s].workingColorSpaceConfig.unpackColorSpace},fromWorkingColorSpace:function(s,r){return Si("ColorManagement: .fromWorkingColorSpace() has been renamed to .workingToColorSpace()."),i.workingToColorSpace(s,r)},toWorkingColorSpace:function(s,r){return Si("ColorManagement: .toWorkingColorSpace() has been renamed to .colorSpaceToWorking()."),i.colorSpaceToWorking(s,r)}},e=[.64,.33,.3,.6,.15,.06],t=[.2126,.7152,.0722],n=[.3127,.329];return i.define({[tr]:{primaries:e,whitePoint:n,transfer:nr,toXYZ:Jc,fromXYZ:$c,luminanceCoefficients:t,workingColorSpaceConfig:{unpackColorSpace:yt},outputColorSpaceConfig:{drawingBufferColorSpace:yt}},[yt]:{primaries:e,whitePoint:n,transfer:dt,toXYZ:Jc,fromXYZ:$c,luminanceCoefficients:t,outputColorSpaceConfig:{drawingBufferColorSpace:yt}}}),i}var st=bd();function Jn(i){return i<.04045?i*.0773993808:Math.pow(i*.9478672986+.0521327014,2.4)}function os(i){return i<.0031308?i*12.92:1.055*Math.pow(i,.41666)-.055}var ki,Ba=class{static getDataURL(e,t="image/png"){if(/^data:/i.test(e.src)||typeof HTMLCanvasElement>"u")return e.src;let n;if(e instanceof HTMLCanvasElement)n=e;else{ki===void 0&&(ki=hs("canvas")),ki.width=e.width,ki.height=e.height;let s=ki.getContext("2d");e instanceof ImageData?s.putImageData(e,0,0):s.drawImage(e,0,0,e.width,e.height),n=ki}return n.toDataURL(t)}static sRGBToLinear(e){if(typeof HTMLImageElement<"u"&&e instanceof HTMLImageElement||typeof HTMLCanvasElement<"u"&&e instanceof HTMLCanvasElement||typeof ImageBitmap<"u"&&e instanceof ImageBitmap){let t=hs("canvas");t.width=e.width,t.height=e.height;let n=t.getContext("2d");n.drawImage(e,0,0,e.width,e.height);let s=n.getImageData(0,0,e.width,e.height),r=s.data;for(let a=0;a<r.length;a++)r[a]=Jn(r[a]/255)*255;return n.putImageData(s,0,0),t}else if(e.data){let t=e.data.slice(0);for(let n=0;n<t.length;n++)t instanceof Uint8Array||t instanceof Uint8ClampedArray?t[n]=Math.floor(Jn(t[n]/255)*255):t[n]=Jn(t[n]);return{data:t,width:e.width,height:e.height}}else return ke("ImageUtils.sRGBToLinear(): Unsupported image type. No color space conversion applied."),e}},Ed=0,fs=class{constructor(e=null){this.isTextureSource=!0,Object.defineProperty(this,"id",{value:Ed++}),this.uuid=Rn(),this.data=e,this.dataReady=!0,this.version=0}getSize(e){let t=this.data;return typeof HTMLVideoElement<"u"&&t instanceof HTMLVideoElement?e.set(t.videoWidth,t.videoHeight,0):typeof VideoFrame<"u"&&t instanceof VideoFrame?e.set(t.displayWidth,t.displayHeight,0):t!==null?e.set(t.width,t.height,t.depth||0):e.set(0,0,0),e}set needsUpdate(e){e===!0&&this.version++}toJSON(e){let t=e===void 0||typeof e=="string";if(!t&&e.images[this.uuid]!==void 0)return e.images[this.uuid];let n={uuid:this.uuid,url:""},s=this.data;if(s!==null){let r;if(Array.isArray(s)){r=[];for(let a=0,o=s.length;a<o;a++)s[a].isDataTexture?r.push(fl(s[a].image)):r.push(fl(s[a]))}else r=fl(s);n.url=r}return t||(e.images[this.uuid]=n),n}};function fl(i){return typeof HTMLImageElement<"u"&&i instanceof HTMLImageElement||typeof HTMLCanvasElement<"u"&&i instanceof HTMLCanvasElement||typeof ImageBitmap<"u"&&i instanceof ImageBitmap?Ba.getDataURL(i):i.data?{data:Array.from(i.data),width:i.width,height:i.height,type:i.data.constructor.name}:(ke("Texture: Unable to serialize Texture."),{})}var Td=0,pl=new L,Vt=class i extends In{constructor(e=i.DEFAULT_IMAGE,t=i.DEFAULT_MAPPING,n=wn,s=wn,r=At,a=On,o=cn,c=$t,l=i.DEFAULT_ANISOTROPY,h=Qn){super(),this.isTexture=!0,Object.defineProperty(this,"id",{value:Td++}),this.uuid=Rn(),this.name="",this.source=new fs(e),this.mipmaps=[],this.mapping=t,this.channel=0,this.wrapS=n,this.wrapT=s,this.magFilter=r,this.minFilter=a,this.anisotropy=l,this.format=o,this.internalFormat=null,this.type=c,this.offset=new he(0,0),this.repeat=new he(1,1),this.center=new he(0,0),this.rotation=0,this.matrixAutoUpdate=!0,this.matrix=new Je,this.generateMipmaps=!0,this.premultiplyAlpha=!1,this.flipY=!0,this.unpackAlignment=4,this.colorSpace=h,this.userData={},this.updateRanges=[],this.version=0,this.onUpdate=null,this.renderTarget=null,this.isRenderTargetTexture=!1,this.isArrayTexture=!!(e&&e.depth&&e.depth>1),this.pmremVersion=0,this.normalized=!1}get width(){return this.source.getSize(pl).x}get height(){return this.source.getSize(pl).y}get depth(){return this.source.getSize(pl).z}get image(){return this.source.data}set image(e){this.source.data=e}updateMatrix(){this.matrix.setUvTransform(this.offset.x,this.offset.y,this.repeat.x,this.repeat.y,this.rotation,this.center.x,this.center.y)}addUpdateRange(e,t){this.updateRanges.push({start:e,count:t})}clearUpdateRanges(){this.updateRanges.length=0}clone(){return new this.constructor().copy(this)}copy(e){return this.name=e.name,this.source=e.source,this.mipmaps=e.mipmaps.slice(0),this.mapping=e.mapping,this.channel=e.channel,this.wrapS=e.wrapS,this.wrapT=e.wrapT,this.magFilter=e.magFilter,this.minFilter=e.minFilter,this.anisotropy=e.anisotropy,this.format=e.format,this.internalFormat=e.internalFormat,this.type=e.type,this.normalized=e.normalized,this.offset.copy(e.offset),this.repeat.copy(e.repeat),this.center.copy(e.center),this.rotation=e.rotation,this.matrixAutoUpdate=e.matrixAutoUpdate,this.matrix.copy(e.matrix),this.generateMipmaps=e.generateMipmaps,this.premultiplyAlpha=e.premultiplyAlpha,this.flipY=e.flipY,this.unpackAlignment=e.unpackAlignment,this.colorSpace=e.colorSpace,this.renderTarget=e.renderTarget,this.isRenderTargetTexture=e.isRenderTargetTexture,this.isArrayTexture=e.isArrayTexture,this.userData=JSON.parse(JSON.stringify(e.userData)),this.needsUpdate=!0,this}setValues(e){for(let t in e){let n=e[t];if(n===void 0){ke(`Texture.setValues(): parameter '${t}' has value of undefined.`);continue}let s=this[t];if(s===void 0){ke(`Texture.setValues(): property '${t}' does not exist.`);continue}s&&n&&s.isVector2&&n.isVector2||s&&n&&s.isVector3&&n.isVector3||s&&n&&s.isMatrix3&&n.isMatrix3?s.copy(n):this[t]=n}}toJSON(e){let t=e===void 0||typeof e=="string";if(!t&&e.textures[this.uuid]!==void 0)return e.textures[this.uuid];let n={metadata:{version:4.7,type:"Texture",generator:"Texture.toJSON"},uuid:this.uuid,name:this.name,image:this.source.toJSON(e).uuid,mapping:this.mapping,channel:this.channel,repeat:[this.repeat.x,this.repeat.y],offset:[this.offset.x,this.offset.y],center:[this.center.x,this.center.y],rotation:this.rotation,wrap:[this.wrapS,this.wrapT],format:this.format,internalFormat:this.internalFormat,type:this.type,normalized:this.normalized,colorSpace:this.colorSpace,minFilter:this.minFilter,magFilter:this.magFilter,anisotropy:this.anisotropy,flipY:this.flipY,generateMipmaps:this.generateMipmaps,premultiplyAlpha:this.premultiplyAlpha,unpackAlignment:this.unpackAlignment};return Object.keys(this.userData).length>0&&(n.userData=this.userData),t||(e.textures[this.uuid]=n),n}dispose(){this.dispatchEvent({type:"dispose"})}transformUv(e){if(this.mapping!==cc)return e;if(e.applyMatrix3(this.matrix),e.x<0||e.x>1)switch(this.wrapS){case Na:e.x=e.x-Math.floor(e.x);break;case wn:e.x=e.x<0?0:1;break;case Fa:Math.abs(Math.floor(e.x)%2)===1?e.x=Math.ceil(e.x)-e.x:e.x=e.x-Math.floor(e.x);break}if(e.y<0||e.y>1)switch(this.wrapT){case Na:e.y=e.y-Math.floor(e.y);break;case wn:e.y=e.y<0?0:1;break;case Fa:Math.abs(Math.floor(e.y)%2)===1?e.y=Math.ceil(e.y)-e.y:e.y=e.y-Math.floor(e.y);break}return this.flipY&&(e.y=1-e.y),e}set needsUpdate(e){e===!0&&(this.version++,this.source.needsUpdate=!0)}set needsPMREMUpdate(e){e===!0&&this.pmremVersion++}};Vt.DEFAULT_IMAGE=null;Vt.DEFAULT_MAPPING=cc;Vt.DEFAULT_ANISOTROPY=1;var vt=class i{static{i.prototype.isVector4=!0}constructor(e=0,t=0,n=0,s=1){this.x=e,this.y=t,this.z=n,this.w=s}get width(){return this.z}set width(e){this.z=e}get height(){return this.w}set height(e){this.w=e}set(e,t,n,s){return this.x=e,this.y=t,this.z=n,this.w=s,this}setScalar(e){return this.x=e,this.y=e,this.z=e,this.w=e,this}setX(e){return this.x=e,this}setY(e){return this.y=e,this}setZ(e){return this.z=e,this}setW(e){return this.w=e,this}setComponent(e,t){switch(e){case 0:this.x=t;break;case 1:this.y=t;break;case 2:this.z=t;break;case 3:this.w=t;break;default:throw new Error("THREE.Vector4: index is out of range: "+e)}return this}getComponent(e){switch(e){case 0:return this.x;case 1:return this.y;case 2:return this.z;case 3:return this.w;default:throw new Error("THREE.Vector4: index is out of range: "+e)}}clone(){return new this.constructor(this.x,this.y,this.z,this.w)}copy(e){return this.x=e.x,this.y=e.y,this.z=e.z,this.w=e.w!==void 0?e.w:1,this}add(e){return this.x+=e.x,this.y+=e.y,this.z+=e.z,this.w+=e.w,this}addScalar(e){return this.x+=e,this.y+=e,this.z+=e,this.w+=e,this}addVectors(e,t){return this.x=e.x+t.x,this.y=e.y+t.y,this.z=e.z+t.z,this.w=e.w+t.w,this}addScaledVector(e,t){return this.x+=e.x*t,this.y+=e.y*t,this.z+=e.z*t,this.w+=e.w*t,this}sub(e){return this.x-=e.x,this.y-=e.y,this.z-=e.z,this.w-=e.w,this}subScalar(e){return this.x-=e,this.y-=e,this.z-=e,this.w-=e,this}subVectors(e,t){return this.x=e.x-t.x,this.y=e.y-t.y,this.z=e.z-t.z,this.w=e.w-t.w,this}multiply(e){return this.x*=e.x,this.y*=e.y,this.z*=e.z,this.w*=e.w,this}multiplyScalar(e){return this.x*=e,this.y*=e,this.z*=e,this.w*=e,this}applyMatrix4(e){let t=this.x,n=this.y,s=this.z,r=this.w,a=e.elements;return this.x=a[0]*t+a[4]*n+a[8]*s+a[12]*r,this.y=a[1]*t+a[5]*n+a[9]*s+a[13]*r,this.z=a[2]*t+a[6]*n+a[10]*s+a[14]*r,this.w=a[3]*t+a[7]*n+a[11]*s+a[15]*r,this}divide(e){return this.x/=e.x,this.y/=e.y,this.z/=e.z,this.w/=e.w,this}divideScalar(e){return this.multiplyScalar(1/e)}setAxisAngleFromQuaternion(e){this.w=2*Math.acos(e.w);let t=Math.sqrt(1-e.w*e.w);return t<1e-4?(this.x=1,this.y=0,this.z=0):(this.x=e.x/t,this.y=e.y/t,this.z=e.z/t),this}setAxisAngleFromRotationMatrix(e){let t,n,s,r,c=e.elements,l=c[0],h=c[4],d=c[8],u=c[1],p=c[5],g=c[9],v=c[2],m=c[6],f=c[10];if(Math.abs(h-u)<.01&&Math.abs(d-v)<.01&&Math.abs(g-m)<.01){if(Math.abs(h+u)<.1&&Math.abs(d+v)<.1&&Math.abs(g+m)<.1&&Math.abs(l+p+f-3)<.1)return this.set(1,0,0,0),this;t=Math.PI;let R=(l+1)/2,M=(p+1)/2,T=(f+1)/2,w=(h+u)/4,P=(d+v)/4,x=(g+m)/4;return R>M&&R>T?R<.01?(n=0,s=.707106781,r=.707106781):(n=Math.sqrt(R),s=w/n,r=P/n):M>T?M<.01?(n=.707106781,s=0,r=.707106781):(s=Math.sqrt(M),n=w/s,r=x/s):T<.01?(n=.707106781,s=.707106781,r=0):(r=Math.sqrt(T),n=P/r,s=x/r),this.set(n,s,r,t),this}let S=Math.sqrt((m-g)*(m-g)+(d-v)*(d-v)+(u-h)*(u-h));return Math.abs(S)<.001&&(S=1),this.x=(m-g)/S,this.y=(d-v)/S,this.z=(u-h)/S,this.w=Math.acos((l+p+f-1)/2),this}setFromMatrixPosition(e){let t=e.elements;return this.x=t[12],this.y=t[13],this.z=t[14],this.w=t[15],this}min(e){return this.x=Math.min(this.x,e.x),this.y=Math.min(this.y,e.y),this.z=Math.min(this.z,e.z),this.w=Math.min(this.w,e.w),this}max(e){return this.x=Math.max(this.x,e.x),this.y=Math.max(this.y,e.y),this.z=Math.max(this.z,e.z),this.w=Math.max(this.w,e.w),this}clamp(e,t){return this.x=nt(this.x,e.x,t.x),this.y=nt(this.y,e.y,t.y),this.z=nt(this.z,e.z,t.z),this.w=nt(this.w,e.w,t.w),this}clampScalar(e,t){return this.x=nt(this.x,e,t),this.y=nt(this.y,e,t),this.z=nt(this.z,e,t),this.w=nt(this.w,e,t),this}clampLength(e,t){let n=this.length();return this.divideScalar(n||1).multiplyScalar(nt(n,e,t))}floor(){return this.x=Math.floor(this.x),this.y=Math.floor(this.y),this.z=Math.floor(this.z),this.w=Math.floor(this.w),this}ceil(){return this.x=Math.ceil(this.x),this.y=Math.ceil(this.y),this.z=Math.ceil(this.z),this.w=Math.ceil(this.w),this}round(){return this.x=Math.round(this.x),this.y=Math.round(this.y),this.z=Math.round(this.z),this.w=Math.round(this.w),this}roundToZero(){return this.x=Math.trunc(this.x),this.y=Math.trunc(this.y),this.z=Math.trunc(this.z),this.w=Math.trunc(this.w),this}negate(){return this.x=-this.x,this.y=-this.y,this.z=-this.z,this.w=-this.w,this}dot(e){return this.x*e.x+this.y*e.y+this.z*e.z+this.w*e.w}lengthSq(){return this.x*this.x+this.y*this.y+this.z*this.z+this.w*this.w}length(){return Math.sqrt(this.x*this.x+this.y*this.y+this.z*this.z+this.w*this.w)}manhattanLength(){return Math.abs(this.x)+Math.abs(this.y)+Math.abs(this.z)+Math.abs(this.w)}normalize(){return this.divideScalar(this.length()||1)}setLength(e){return this.normalize().multiplyScalar(e)}lerp(e,t){return this.x+=(e.x-this.x)*t,this.y+=(e.y-this.y)*t,this.z+=(e.z-this.z)*t,this.w+=(e.w-this.w)*t,this}lerpVectors(e,t,n){return this.x=e.x+(t.x-e.x)*n,this.y=e.y+(t.y-e.y)*n,this.z=e.z+(t.z-e.z)*n,this.w=e.w+(t.w-e.w)*n,this}equals(e){return e.x===this.x&&e.y===this.y&&e.z===this.z&&e.w===this.w}fromArray(e,t=0){return this.x=e[t],this.y=e[t+1],this.z=e[t+2],this.w=e[t+3],this}toArray(e=[],t=0){return e[t]=this.x,e[t+1]=this.y,e[t+2]=this.z,e[t+3]=this.w,e}fromBufferAttribute(e,t){return this.x=e.getX(t),this.y=e.getY(t),this.z=e.getZ(t),this.w=e.getW(t),this}random(){return this.x=Math.random(),this.y=Math.random(),this.z=Math.random(),this.w=Math.random(),this}*[Symbol.iterator](){yield this.x,yield this.y,yield this.z,yield this.w}},za=class extends In{constructor(e=1,t=1,n={}){super(),n=Object.assign({generateMipmaps:!1,internalFormat:null,minFilter:At,depthBuffer:!0,stencilBuffer:!1,resolveColorBuffer:!0,resolveDepthBuffer:!0,resolveStencilBuffer:!0,storeMultisampledColorBuffer:!0,storeMultisampledDepthBuffer:!0,storeMultisampledStencilBuffer:!0,depthTexture:null,samples:0,count:1,depth:1,multiview:!1,useArrayDepthTexture:!1},n),this.isRenderTarget=!0,this.width=e,this.height=t,this.depth=n.depth,this.scissor=new vt(0,0,e,t),this.scissorTest=!1,this.viewport=new vt(0,0,e,t),this.textures=[];let s={width:e,height:t,depth:n.depth},r=new Vt(s),a=n.count;for(let o=0;o<a;o++)this.textures[o]=r.clone(),this.textures[o].isRenderTargetTexture=!0,this.textures[o].renderTarget=this;this._setTextureOptions(n),this.depthBuffer=n.depthBuffer,this.stencilBuffer=n.stencilBuffer,this.resolveColorBuffer=n.resolveColorBuffer,this.resolveDepthBuffer=n.resolveDepthBuffer,this.resolveStencilBuffer=n.resolveStencilBuffer,this.storeMultisampledColorBuffer=n.storeMultisampledColorBuffer,this.storeMultisampledDepthBuffer=n.storeMultisampledDepthBuffer,this.storeMultisampledStencilBuffer=n.storeMultisampledStencilBuffer,this._depthTexture=null,this.depthTexture=n.depthTexture,this.samples=n.samples,this.multiview=n.multiview,this.useArrayDepthTexture=n.useArrayDepthTexture}_setTextureOptions(e={}){let t={minFilter:At,generateMipmaps:!1,flipY:!1,internalFormat:null};e.mapping!==void 0&&(t.mapping=e.mapping),e.wrapS!==void 0&&(t.wrapS=e.wrapS),e.wrapT!==void 0&&(t.wrapT=e.wrapT),e.wrapR!==void 0&&(t.wrapR=e.wrapR),e.magFilter!==void 0&&(t.magFilter=e.magFilter),e.minFilter!==void 0&&(t.minFilter=e.minFilter),e.format!==void 0&&(t.format=e.format),e.type!==void 0&&(t.type=e.type),e.anisotropy!==void 0&&(t.anisotropy=e.anisotropy),e.colorSpace!==void 0&&(t.colorSpace=e.colorSpace),e.flipY!==void 0&&(t.flipY=e.flipY),e.generateMipmaps!==void 0&&(t.generateMipmaps=e.generateMipmaps),e.internalFormat!==void 0&&(t.internalFormat=e.internalFormat);for(let n=0;n<this.textures.length;n++)this.textures[n].setValues(t)}get texture(){return this.textures[0]}set texture(e){this.textures[0]=e}set depthTexture(e){this._depthTexture!==null&&this._depthTexture.renderTarget===this&&(this._depthTexture.renderTarget=null),e!==null&&e.renderTarget===null&&(e.renderTarget=this),this._depthTexture=e}get depthTexture(){return this._depthTexture}setSize(e,t,n=1){if(this.width!==e||this.height!==t||this.depth!==n){this.width=e,this.height=t,this.depth=n;for(let s=0,r=this.textures.length;s<r;s++)this.textures[s].image.width=e,this.textures[s].image.height=t,this.textures[s].image.depth=n,this.textures[s].isData3DTexture!==!0&&(this.textures[s].isArrayTexture=this.textures[s].image.depth>1);this.dispose()}this.viewport.set(0,0,e,t),this.scissor.set(0,0,e,t)}clone(){return new this.constructor().copy(this)}copy(e){this.width=e.width,this.height=e.height,this.depth=e.depth,this.scissor.copy(e.scissor),this.scissorTest=e.scissorTest,this.viewport.copy(e.viewport),this.textures.length=0;for(let t=0,n=e.textures.length;t<n;t++){this.textures[t]=e.textures[t].clone(),this.textures[t].isRenderTargetTexture=!0,this.textures[t].renderTarget=this;let s=Object.assign({},e.textures[t].image);this.textures[t].source=new fs(s)}if(this.depthBuffer=e.depthBuffer,this.stencilBuffer=e.stencilBuffer,this.resolveColorBuffer=e.resolveColorBuffer,this.resolveDepthBuffer=e.resolveDepthBuffer,this.resolveStencilBuffer=e.resolveStencilBuffer,this.storeMultisampledColorBuffer=e.storeMultisampledColorBuffer,this.storeMultisampledDepthBuffer=e.storeMultisampledDepthBuffer,this.storeMultisampledStencilBuffer=e.storeMultisampledStencilBuffer,e.depthTexture!==null)if(e.depthTexture.renderTarget===e){let t=e.depthTexture.clone();t.renderTarget=null,this.depthTexture=t}else this.depthTexture=e.depthTexture;return this.samples=e.samples,this.multiview=e.multiview,this.useArrayDepthTexture=e.useArrayDepthTexture,this}dispose(){this.dispatchEvent({type:"dispose"})}},Jt=class extends za{constructor(e=1,t=1,n={}){super(e,t,n),this.isWebGLRenderTarget=!0}},sr=class extends Vt{constructor(e=null,t=1,n=1,s=1){super(null),this.isDataArrayTexture=!0,this.image={data:e,width:t,height:n,depth:s},this.magFilter=Lt,this.minFilter=Lt,this.wrapR=wn,this.generateMipmaps=!1,this.flipY=!1,this.unpackAlignment=1,this.layerUpdates=new Set}copy(e){return super.copy(e),this.wrapR=e.wrapR,this}addLayerUpdate(e){this.layerUpdates.add(e)}clearLayerUpdates(){this.layerUpdates.clear()}};var Ha=class extends Vt{constructor(e=null,t=1,n=1,s=1){super(null),this.isData3DTexture=!0,this.image={data:e,width:t,height:n,depth:s},this.magFilter=Lt,this.minFilter=Lt,this.wrapR=wn,this.generateMipmaps=!1,this.flipY=!1,this.unpackAlignment=1}copy(e){return super.copy(e),this.wrapR=e.wrapR,this}};var ut=class i{static{i.prototype.isMatrix4=!0}constructor(e,t,n,s,r,a,o,c,l,h,d,u,p,g,v,m){this.elements=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],e!==void 0&&this.set(e,t,n,s,r,a,o,c,l,h,d,u,p,g,v,m)}set(e,t,n,s,r,a,o,c,l,h,d,u,p,g,v,m){let f=this.elements;return f[0]=e,f[4]=t,f[8]=n,f[12]=s,f[1]=r,f[5]=a,f[9]=o,f[13]=c,f[2]=l,f[6]=h,f[10]=d,f[14]=u,f[3]=p,f[7]=g,f[11]=v,f[15]=m,this}identity(){return this.set(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1),this}clone(){return new i().fromArray(this.elements)}copy(e){let t=this.elements,n=e.elements;return t[0]=n[0],t[1]=n[1],t[2]=n[2],t[3]=n[3],t[4]=n[4],t[5]=n[5],t[6]=n[6],t[7]=n[7],t[8]=n[8],t[9]=n[9],t[10]=n[10],t[11]=n[11],t[12]=n[12],t[13]=n[13],t[14]=n[14],t[15]=n[15],this}copyPosition(e){let t=this.elements,n=e.elements;return t[12]=n[12],t[13]=n[13],t[14]=n[14],this}setFromMatrix3(e){let t=e.elements;return this.set(t[0],t[3],t[6],0,t[1],t[4],t[7],0,t[2],t[5],t[8],0,0,0,0,1),this}extractBasis(e,t,n){return this.determinantAffine()===0?(e.set(1,0,0),t.set(0,1,0),n.set(0,0,1),this):(e.setFromMatrixColumn(this,0),t.setFromMatrixColumn(this,1),n.setFromMatrixColumn(this,2),this)}makeBasis(e,t,n){return this.set(e.x,t.x,n.x,0,e.y,t.y,n.y,0,e.z,t.z,n.z,0,0,0,0,1),this}extractRotation(e){if(e.determinantAffine()===0)return this.identity();let t=this.elements,n=e.elements,s=1/Gi.setFromMatrixColumn(e,0).length(),r=1/Gi.setFromMatrixColumn(e,1).length(),a=1/Gi.setFromMatrixColumn(e,2).length();return t[0]=n[0]*s,t[1]=n[1]*s,t[2]=n[2]*s,t[3]=0,t[4]=n[4]*r,t[5]=n[5]*r,t[6]=n[6]*r,t[7]=0,t[8]=n[8]*a,t[9]=n[9]*a,t[10]=n[10]*a,t[11]=0,t[12]=0,t[13]=0,t[14]=0,t[15]=1,this}makeRotationFromEuler(e){let t=this.elements,n=e.x,s=e.y,r=e.z,a=Math.cos(n),o=Math.sin(n),c=Math.cos(s),l=Math.sin(s),h=Math.cos(r),d=Math.sin(r);if(e.order==="XYZ"){let u=a*h,p=a*d,g=o*h,v=o*d;t[0]=c*h,t[4]=-c*d,t[8]=l,t[1]=p+g*l,t[5]=u-v*l,t[9]=-o*c,t[2]=v-u*l,t[6]=g+p*l,t[10]=a*c}else if(e.order==="YXZ"){let u=c*h,p=c*d,g=l*h,v=l*d;t[0]=u+v*o,t[4]=g*o-p,t[8]=a*l,t[1]=a*d,t[5]=a*h,t[9]=-o,t[2]=p*o-g,t[6]=v+u*o,t[10]=a*c}else if(e.order==="ZXY"){let u=c*h,p=c*d,g=l*h,v=l*d;t[0]=u-v*o,t[4]=-a*d,t[8]=g+p*o,t[1]=p+g*o,t[5]=a*h,t[9]=v-u*o,t[2]=-a*l,t[6]=o,t[10]=a*c}else if(e.order==="ZYX"){let u=a*h,p=a*d,g=o*h,v=o*d;t[0]=c*h,t[4]=g*l-p,t[8]=u*l+v,t[1]=c*d,t[5]=v*l+u,t[9]=p*l-g,t[2]=-l,t[6]=o*c,t[10]=a*c}else if(e.order==="YZX"){let u=a*c,p=a*l,g=o*c,v=o*l;t[0]=c*h,t[4]=v-u*d,t[8]=g*d+p,t[1]=d,t[5]=a*h,t[9]=-o*h,t[2]=-l*h,t[6]=p*d+g,t[10]=u-v*d}else if(e.order==="XZY"){let u=a*c,p=a*l,g=o*c,v=o*l;t[0]=c*h,t[4]=-d,t[8]=l*h,t[1]=u*d+v,t[5]=a*h,t[9]=p*d-g,t[2]=g*d-p,t[6]=o*h,t[10]=v*d+u}return t[3]=0,t[7]=0,t[11]=0,t[12]=0,t[13]=0,t[14]=0,t[15]=1,this}makeRotationFromQuaternion(e){return this.compose(wd,e,Ad)}lookAt(e,t,n){let s=this.elements;return Kt.subVectors(e,t),Kt.lengthSq()===0&&(Kt.z=1),Kt.normalize(),ni.crossVectors(n,Kt),ni.lengthSq()===0&&(Math.abs(n.z)===1?Kt.x+=1e-4:Kt.z+=1e-4,Kt.normalize(),ni.crossVectors(n,Kt)),ni.normalize(),$r.crossVectors(Kt,ni),s[0]=ni.x,s[4]=$r.x,s[8]=Kt.x,s[1]=ni.y,s[5]=$r.y,s[9]=Kt.y,s[2]=ni.z,s[6]=$r.z,s[10]=Kt.z,this}multiply(e){return this.multiplyMatrices(this,e)}premultiply(e){return this.multiplyMatrices(e,this)}multiplyMatrices(e,t){let n=e.elements,s=t.elements,r=this.elements,a=n[0],o=n[4],c=n[8],l=n[12],h=n[1],d=n[5],u=n[9],p=n[13],g=n[2],v=n[6],m=n[10],f=n[14],S=n[3],R=n[7],M=n[11],T=n[15],w=s[0],P=s[4],x=s[8],E=s[12],C=s[1],U=s[5],B=s[9],H=s[13],N=s[2],V=s[6],X=s[10],W=s[14],re=s[3],Y=s[7],Q=s[11],j=s[15];return r[0]=a*w+o*C+c*N+l*re,r[4]=a*P+o*U+c*V+l*Y,r[8]=a*x+o*B+c*X+l*Q,r[12]=a*E+o*H+c*W+l*j,r[1]=h*w+d*C+u*N+p*re,r[5]=h*P+d*U+u*V+p*Y,r[9]=h*x+d*B+u*X+p*Q,r[13]=h*E+d*H+u*W+p*j,r[2]=g*w+v*C+m*N+f*re,r[6]=g*P+v*U+m*V+f*Y,r[10]=g*x+v*B+m*X+f*Q,r[14]=g*E+v*H+m*W+f*j,r[3]=S*w+R*C+M*N+T*re,r[7]=S*P+R*U+M*V+T*Y,r[11]=S*x+R*B+M*X+T*Q,r[15]=S*E+R*H+M*W+T*j,this}multiplyScalar(e){let t=this.elements;return t[0]*=e,t[4]*=e,t[8]*=e,t[12]*=e,t[1]*=e,t[5]*=e,t[9]*=e,t[13]*=e,t[2]*=e,t[6]*=e,t[10]*=e,t[14]*=e,t[3]*=e,t[7]*=e,t[11]*=e,t[15]*=e,this}determinant(){let e=this.elements,t=e[0],n=e[4],s=e[8],r=e[12],a=e[1],o=e[5],c=e[9],l=e[13],h=e[2],d=e[6],u=e[10],p=e[14],g=e[3],v=e[7],m=e[11],f=e[15],S=c*p-l*u,R=o*p-l*d,M=o*u-c*d,T=a*p-l*h,w=a*u-c*h,P=a*d-o*h;return t*(v*S-m*R+f*M)-n*(g*S-m*T+f*w)+s*(g*R-v*T+f*P)-r*(g*M-v*w+m*P)}determinantAffine(){let e=this.elements,t=e[0],n=e[4],s=e[8],r=e[1],a=e[5],o=e[9],c=e[2],l=e[6],h=e[10];return t*(a*h-o*l)-n*(r*h-o*c)+s*(r*l-a*c)}transpose(){let e=this.elements,t;return t=e[1],e[1]=e[4],e[4]=t,t=e[2],e[2]=e[8],e[8]=t,t=e[6],e[6]=e[9],e[9]=t,t=e[3],e[3]=e[12],e[12]=t,t=e[7],e[7]=e[13],e[13]=t,t=e[11],e[11]=e[14],e[14]=t,this}setPosition(e,t,n){let s=this.elements;return e.isVector3?(s[12]=e.x,s[13]=e.y,s[14]=e.z):(s[12]=e,s[13]=t,s[14]=n),this}invert(){let e=this.elements,t=e[0],n=e[1],s=e[2],r=e[3],a=e[4],o=e[5],c=e[6],l=e[7],h=e[8],d=e[9],u=e[10],p=e[11],g=e[12],v=e[13],m=e[14],f=e[15],S=t*o-n*a,R=t*c-s*a,M=t*l-r*a,T=n*c-s*o,w=n*l-r*o,P=s*l-r*c,x=h*v-d*g,E=h*m-u*g,C=h*f-p*g,U=d*m-u*v,B=d*f-p*v,H=u*f-p*m,N=S*H-R*B+M*U+T*C-w*E+P*x;if(N===0)return this.set(0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0);let V=1/N;return e[0]=(o*H-c*B+l*U)*V,e[1]=(s*B-n*H-r*U)*V,e[2]=(v*P-m*w+f*T)*V,e[3]=(u*w-d*P-p*T)*V,e[4]=(c*C-a*H-l*E)*V,e[5]=(t*H-s*C+r*E)*V,e[6]=(m*M-g*P-f*R)*V,e[7]=(h*P-u*M+p*R)*V,e[8]=(a*B-o*C+l*x)*V,e[9]=(n*C-t*B-r*x)*V,e[10]=(g*w-v*M+f*S)*V,e[11]=(d*M-h*w-p*S)*V,e[12]=(o*E-a*U-c*x)*V,e[13]=(t*U-n*E+s*x)*V,e[14]=(v*R-g*T-m*S)*V,e[15]=(h*T-d*R+u*S)*V,this}scale(e){let t=this.elements,n=e.x,s=e.y,r=e.z;return t[0]*=n,t[4]*=s,t[8]*=r,t[1]*=n,t[5]*=s,t[9]*=r,t[2]*=n,t[6]*=s,t[10]*=r,t[3]*=n,t[7]*=s,t[11]*=r,this}getMaxScaleOnAxis(){let e=this.elements,t=e[0]*e[0]+e[1]*e[1]+e[2]*e[2],n=e[4]*e[4]+e[5]*e[5]+e[6]*e[6],s=e[8]*e[8]+e[9]*e[9]+e[10]*e[10];return Math.sqrt(Math.max(t,n,s))}makeTranslation(e,t,n){return e.isVector3?this.set(1,0,0,e.x,0,1,0,e.y,0,0,1,e.z,0,0,0,1):this.set(1,0,0,e,0,1,0,t,0,0,1,n,0,0,0,1),this}makeRotationX(e){let t=Math.cos(e),n=Math.sin(e);return this.set(1,0,0,0,0,t,-n,0,0,n,t,0,0,0,0,1),this}makeRotationY(e){let t=Math.cos(e),n=Math.sin(e);return this.set(t,0,n,0,0,1,0,0,-n,0,t,0,0,0,0,1),this}makeRotationZ(e){let t=Math.cos(e),n=Math.sin(e);return this.set(t,-n,0,0,n,t,0,0,0,0,1,0,0,0,0,1),this}makeRotationAxis(e,t){let n=Math.cos(t),s=Math.sin(t),r=1-n,a=e.x,o=e.y,c=e.z,l=r*a,h=r*o;return this.set(l*a+n,l*o-s*c,l*c+s*o,0,l*o+s*c,h*o+n,h*c-s*a,0,l*c-s*o,h*c+s*a,r*c*c+n,0,0,0,0,1),this}makeScale(e,t,n){return this.set(e,0,0,0,0,t,0,0,0,0,n,0,0,0,0,1),this}makeShear(e,t,n,s,r,a){return this.set(1,n,r,0,e,1,a,0,t,s,1,0,0,0,0,1),this}compose(e,t,n){let s=this.elements,r=t._x,a=t._y,o=t._z,c=t._w,l=r+r,h=a+a,d=o+o,u=r*l,p=r*h,g=r*d,v=a*h,m=a*d,f=o*d,S=c*l,R=c*h,M=c*d,T=n.x,w=n.y,P=n.z;return s[0]=(1-(v+f))*T,s[1]=(p+M)*T,s[2]=(g-R)*T,s[3]=0,s[4]=(p-M)*w,s[5]=(1-(u+f))*w,s[6]=(m+S)*w,s[7]=0,s[8]=(g+R)*P,s[9]=(m-S)*P,s[10]=(1-(u+v))*P,s[11]=0,s[12]=e.x,s[13]=e.y,s[14]=e.z,s[15]=1,this}decompose(e,t,n){let s=this.elements;e.x=s[12],e.y=s[13],e.z=s[14];let r=this.determinantAffine();if(r===0)return n.set(1,1,1),t.identity(),this;let a=Gi.set(s[0],s[1],s[2]).length(),o=Gi.set(s[4],s[5],s[6]).length(),c=Gi.set(s[8],s[9],s[10]).length();r<0&&(a=-a),un.copy(this);let l=1/a,h=1/o,d=1/c;return un.elements[0]*=l,un.elements[1]*=l,un.elements[2]*=l,un.elements[4]*=h,un.elements[5]*=h,un.elements[6]*=h,un.elements[8]*=d,un.elements[9]*=d,un.elements[10]*=d,t.setFromRotationMatrix(un),n.x=a,n.y=o,n.z=c,this}makePerspective(e,t,n,s,r,a,o=gn,c=!1){let l=this.elements,h=2*r/(t-e),d=2*r/(n-s),u=(t+e)/(t-e),p=(n+s)/(n-s),g,v;if(c)g=r/(a-r),v=a*r/(a-r);else if(o===gn)g=-(a+r)/(a-r),v=-2*a*r/(a-r);else if(o===cs)g=-a/(a-r),v=-a*r/(a-r);else throw new Error("THREE.Matrix4.makePerspective(): Invalid coordinate system: "+o);return l[0]=h,l[4]=0,l[8]=u,l[12]=0,l[1]=0,l[5]=d,l[9]=p,l[13]=0,l[2]=0,l[6]=0,l[10]=g,l[14]=v,l[3]=0,l[7]=0,l[11]=-1,l[15]=0,this}makeOrthographic(e,t,n,s,r,a,o=gn,c=!1){let l=this.elements,h=2/(t-e),d=2/(n-s),u=-(t+e)/(t-e),p=-(n+s)/(n-s),g,v;if(c)g=1/(a-r),v=a/(a-r);else if(o===gn)g=-2/(a-r),v=-(a+r)/(a-r);else if(o===cs)g=-1/(a-r),v=-r/(a-r);else throw new Error("THREE.Matrix4.makeOrthographic(): Invalid coordinate system: "+o);return l[0]=h,l[4]=0,l[8]=0,l[12]=u,l[1]=0,l[5]=d,l[9]=0,l[13]=p,l[2]=0,l[6]=0,l[10]=g,l[14]=v,l[3]=0,l[7]=0,l[11]=0,l[15]=1,this}equals(e){let t=this.elements,n=e.elements;for(let s=0;s<16;s++)if(t[s]!==n[s])return!1;return!0}fromArray(e,t=0){for(let n=0;n<16;n++)this.elements[n]=e[n+t];return this}toArray(e=[],t=0){let n=this.elements;return e[t]=n[0],e[t+1]=n[1],e[t+2]=n[2],e[t+3]=n[3],e[t+4]=n[4],e[t+5]=n[5],e[t+6]=n[6],e[t+7]=n[7],e[t+8]=n[8],e[t+9]=n[9],e[t+10]=n[10],e[t+11]=n[11],e[t+12]=n[12],e[t+13]=n[13],e[t+14]=n[14],e[t+15]=n[15],e}},Gi=new L,un=new ut,wd=new L(0,0,0),Ad=new L(1,1,1),ni=new L,$r=new L,Kt=new L,Kc=new ut,Qc=new Pn,_n=class i{constructor(e=0,t=0,n=0,s=i.DEFAULT_ORDER){this.isEuler=!0,this._x=e,this._y=t,this._z=n,this._order=s}get x(){return this._x}set x(e){this._x=e,this._onChangeCallback()}get y(){return this._y}set y(e){this._y=e,this._onChangeCallback()}get z(){return this._z}set z(e){this._z=e,this._onChangeCallback()}get order(){return this._order}set order(e){this._order=e,this._onChangeCallback()}set(e,t,n,s=this._order){return this._x=e,this._y=t,this._z=n,this._order=s,this._onChangeCallback(),this}clone(){return new this.constructor(this._x,this._y,this._z,this._order)}copy(e){return this._x=e._x,this._y=e._y,this._z=e._z,this._order=e._order,this._onChangeCallback(),this}setFromRotationMatrix(e,t=this._order,n=!0){let s=e.elements,r=s[0],a=s[4],o=s[8],c=s[1],l=s[5],h=s[9],d=s[2],u=s[6],p=s[10];switch(t){case"XYZ":this._y=Math.asin(nt(o,-1,1)),Math.abs(o)<.9999999?(this._x=Math.atan2(-h,p),this._z=Math.atan2(-a,r)):(this._x=Math.atan2(u,l),this._z=0);break;case"YXZ":this._x=Math.asin(-nt(h,-1,1)),Math.abs(h)<.9999999?(this._y=Math.atan2(o,p),this._z=Math.atan2(c,l)):(this._y=Math.atan2(-d,r),this._z=0);break;case"ZXY":this._x=Math.asin(nt(u,-1,1)),Math.abs(u)<.9999999?(this._y=Math.atan2(-d,p),this._z=Math.atan2(-a,l)):(this._y=0,this._z=Math.atan2(c,r));break;case"ZYX":this._y=Math.asin(-nt(d,-1,1)),Math.abs(d)<.9999999?(this._x=Math.atan2(u,p),this._z=Math.atan2(c,r)):(this._x=0,this._z=Math.atan2(-a,l));break;case"YZX":this._z=Math.asin(nt(c,-1,1)),Math.abs(c)<.9999999?(this._x=Math.atan2(-h,l),this._y=Math.atan2(-d,r)):(this._x=0,this._y=Math.atan2(o,p));break;case"XZY":this._z=Math.asin(-nt(a,-1,1)),Math.abs(a)<.9999999?(this._x=Math.atan2(u,l),this._y=Math.atan2(o,r)):(this._x=Math.atan2(-h,p),this._y=0);break;default:ke("Euler: .setFromRotationMatrix() encountered an unknown order: "+t)}return this._order=t,n===!0&&this._onChangeCallback(),this}setFromQuaternion(e,t,n){return Kc.makeRotationFromQuaternion(e),this.setFromRotationMatrix(Kc,t,n)}setFromVector3(e,t=this._order){return this.set(e.x,e.y,e.z,t)}reorder(e){return Qc.setFromEuler(this),this.setFromQuaternion(Qc,e)}equals(e){return e._x===this._x&&e._y===this._y&&e._z===this._z&&e._order===this._order}fromArray(e){return this._x=e[0],this._y=e[1],this._z=e[2],e[3]!==void 0&&(this._order=e[3]),this._onChangeCallback(),this}toArray(e=[],t=0){return e[t]=this._x,e[t+1]=this._y,e[t+2]=this._z,e[t+3]=this._order,e}_onChange(e){return this._onChangeCallback=e,this}_onChangeCallback(){}*[Symbol.iterator](){yield this._x,yield this._y,yield this._z,yield this._order}};_n.DEFAULT_ORDER="XYZ";var ps=class{constructor(){this.mask=1}set(e){this.mask=(1<<e|0)>>>0}enable(e){this.mask|=1<<e|0}enableAll(){this.mask=-1}toggle(e){this.mask^=1<<e|0}disable(e){this.mask&=~(1<<e|0)}disableAll(){this.mask=0}test(e){return(this.mask&e.mask)!==0}isEnabled(e){return(this.mask&(1<<e|0))!==0}},Rd=0,jc=new L,Wi=new Pn,Gn=new ut,Kr=new L,zs=new L,Cd=new L,Id=new Pn,eh=new L(1,0,0),th=new L(0,1,0),nh=new L(0,0,1),ih={type:"added"},Pd={type:"removed"},Xi={type:"childadded",child:null},ml={type:"childremoved",child:null},Tt=class i extends In{constructor(){super(),this.isObject3D=!0,Object.defineProperty(this,"id",{value:Rd++}),this.uuid=Rn(),this.name="",this.type="Object3D",this.parent=null,this.children=[],this.up=i.DEFAULT_UP.clone();let e=new L,t=new _n,n=new Pn,s=new L(1,1,1);function r(){n.setFromEuler(t,!1)}function a(){t.setFromQuaternion(n,void 0,!1)}t._onChange(r),n._onChange(a),Object.defineProperties(this,{position:{configurable:!0,enumerable:!0,value:e},rotation:{configurable:!0,enumerable:!0,value:t},quaternion:{configurable:!0,enumerable:!0,value:n},scale:{configurable:!0,enumerable:!0,value:s},modelViewMatrix:{value:new ut},normalMatrix:{value:new Je}}),this.matrix=new ut,this.matrixWorld=new ut,this.matrixAutoUpdate=i.DEFAULT_MATRIX_AUTO_UPDATE,this.matrixWorldAutoUpdate=i.DEFAULT_MATRIX_WORLD_AUTO_UPDATE,this.matrixWorldNeedsUpdate=!1,this.layers=new ps,this.visible=!0,this.castShadow=!1,this.receiveShadow=!1,this.frustumCulled=!0,this.renderOrder=0,this.animations=[],this.customDepthMaterial=void 0,this.customDistanceMaterial=void 0,this.static=!1,this.userData={},this.pivot=null}onBeforeShadow(){}onAfterShadow(){}onBeforeRender(){}onAfterRender(){}applyMatrix4(e){this.matrixAutoUpdate&&this.updateMatrix(),this.matrix.premultiply(e),this.matrix.decompose(this.position,this.quaternion,this.scale)}applyQuaternion(e){return this.quaternion.premultiply(e),this}setRotationFromAxisAngle(e,t){this.quaternion.setFromAxisAngle(e,t)}setRotationFromEuler(e){this.quaternion.setFromEuler(e,!0)}setRotationFromMatrix(e){this.quaternion.setFromRotationMatrix(e)}setRotationFromQuaternion(e){this.quaternion.copy(e)}rotateOnAxis(e,t){return Wi.setFromAxisAngle(e,t),this.quaternion.multiply(Wi),this}rotateOnWorldAxis(e,t){return Wi.setFromAxisAngle(e,t),this.quaternion.premultiply(Wi),this}rotateX(e){return this.rotateOnAxis(eh,e)}rotateY(e){return this.rotateOnAxis(th,e)}rotateZ(e){return this.rotateOnAxis(nh,e)}translateOnAxis(e,t){return jc.copy(e).applyQuaternion(this.quaternion),this.position.add(jc.multiplyScalar(t)),this}translateX(e){return this.translateOnAxis(eh,e)}translateY(e){return this.translateOnAxis(th,e)}translateZ(e){return this.translateOnAxis(nh,e)}localToWorld(e){return this.updateWorldMatrix(!0,!1),e.applyMatrix4(this.matrixWorld)}worldToLocal(e){return this.updateWorldMatrix(!0,!1),e.applyMatrix4(Gn.copy(this.matrixWorld).invert())}lookAt(e,t,n){e.isVector3?Kr.copy(e):Kr.set(e,t,n);let s=this.parent;this.updateWorldMatrix(!0,!1),zs.setFromMatrixPosition(this.matrixWorld),this.isCamera||this.isLight?Gn.lookAt(zs,Kr,this.up):Gn.lookAt(Kr,zs,this.up),this.quaternion.setFromRotationMatrix(Gn),s&&(Gn.extractRotation(s.matrixWorld),Wi.setFromRotationMatrix(Gn),this.quaternion.premultiply(Wi.invert()))}add(e){if(arguments.length>1){for(let t=0;t<arguments.length;t++)this.add(arguments[t]);return this}return e===this?(We("Object3D.add: object can't be added as a child of itself.",e),this):(e&&e.isObject3D?(e.removeFromParent(),e.parent=this,this.children.push(e),e.dispatchEvent(ih),Xi.child=e,this.dispatchEvent(Xi),Xi.child=null):We("Object3D.add: object not an instance of THREE.Object3D.",e),this)}remove(e){if(arguments.length>1){for(let n=0;n<arguments.length;n++)this.remove(arguments[n]);return this}let t=this.children.indexOf(e);return t!==-1&&(e.parent=null,this.children.splice(t,1),e.dispatchEvent(Pd),ml.child=e,this.dispatchEvent(ml),ml.child=null),this}removeFromParent(){let e=this.parent;return e!==null&&e.remove(this),this}clear(){return this.remove(...this.children)}attach(e){return this.updateWorldMatrix(!0,!1),Gn.copy(this.matrixWorld).invert(),e.parent!==null&&(e.parent.updateWorldMatrix(!0,!1),Gn.multiply(e.parent.matrixWorld)),e.applyMatrix4(Gn),e.removeFromParent(),e.parent=this,this.children.push(e),e.updateWorldMatrix(!1,!0),e.dispatchEvent(ih),Xi.child=e,this.dispatchEvent(Xi),Xi.child=null,this}getObjectById(e){return this.getObjectByProperty("id",e)}getObjectByName(e){return this.getObjectByProperty("name",e)}getObjectByProperty(e,t){if(this[e]===t)return this;for(let n=0,s=this.children.length;n<s;n++){let a=this.children[n].getObjectByProperty(e,t);if(a!==void 0)return a}}getObjectsByProperty(e,t,n=[]){this[e]===t&&n.push(this);let s=this.children;for(let r=0,a=s.length;r<a;r++)s[r].getObjectsByProperty(e,t,n);return n}getWorldPosition(e){return this.updateWorldMatrix(!0,!1),e.setFromMatrixPosition(this.matrixWorld)}getWorldQuaternion(e){return this.updateWorldMatrix(!0,!1),this.matrixWorld.decompose(zs,e,Cd),e}getWorldScale(e){return this.updateWorldMatrix(!0,!1),this.matrixWorld.decompose(zs,Id,e),e}getWorldDirection(e){this.updateWorldMatrix(!0,!1);let t=this.matrixWorld.elements;return e.set(t[8],t[9],t[10]).normalize()}raycast(){}intersectsFrustum(){}traverse(e){e(this);let t=this.children;for(let n=0,s=t.length;n<s;n++)t[n].traverse(e)}traverseVisible(e){if(this.visible===!1)return;e(this);let t=this.children;for(let n=0,s=t.length;n<s;n++)t[n].traverseVisible(e)}traverseAncestors(e){let t=this.parent;t!==null&&(e(t),t.traverseAncestors(e))}updateMatrix(){this.matrix.compose(this.position,this.quaternion,this.scale);let e=this.pivot;if(e!==null){let t=e.x,n=e.y,s=e.z,r=this.matrix.elements;r[12]+=t-r[0]*t-r[4]*n-r[8]*s,r[13]+=n-r[1]*t-r[5]*n-r[9]*s,r[14]+=s-r[2]*t-r[6]*n-r[10]*s}this.matrixWorldNeedsUpdate=!0}updateMatrixWorld(e){this.matrixAutoUpdate&&this.updateMatrix(),(this.matrixWorldNeedsUpdate||e)&&(this.matrixWorldAutoUpdate===!0&&(this.parent===null?this.matrixWorld.copy(this.matrix):this.matrixWorld.multiplyMatrices(this.parent.matrixWorld,this.matrix)),this.matrixWorldNeedsUpdate=!1,e=!0);let t=this.children;for(let n=0,s=t.length;n<s;n++)t[n].updateMatrixWorld(e)}updateWorldMatrix(e,t,n=!1){let s=this.parent;if(e===!0&&s!==null&&s.updateWorldMatrix(!0,!1),this.matrixAutoUpdate&&this.updateMatrix(),(this.matrixWorldNeedsUpdate||n)&&(this.matrixWorldAutoUpdate===!0&&(this.parent===null?this.matrixWorld.copy(this.matrix):this.matrixWorld.multiplyMatrices(this.parent.matrixWorld,this.matrix)),this.matrixWorldNeedsUpdate=!1,n=!0),t===!0){let r=this.children;for(let a=0,o=r.length;a<o;a++)r[a].updateWorldMatrix(!1,!0,n)}}toJSON(e){let t=e===void 0||typeof e=="string",n={};t&&(e={geometries:{},materials:{},textures:{},images:{},shapes:{},skeletons:{},animations:{},nodes:{}},n.metadata={version:4.7,type:"Object",generator:"Object3D.toJSON"});let s={};s.uuid=this.uuid,s.type=this.type,s.name=this.name,s.castShadow=this.castShadow,s.receiveShadow=this.receiveShadow,s.visible=this.visible,s.frustumCulled=this.frustumCulled,s.renderOrder=this.renderOrder,s.static=this.static,s.matrixAutoUpdate=this.matrixAutoUpdate,Object.keys(this.userData).length>0&&(s.userData=this.userData),s.layers=this.layers.mask,s.matrix=this.matrix.toArray(),s.up=this.up.toArray(),this.pivot!==null&&(s.pivot=this.pivot.toArray()),this.morphTargetDictionary!==void 0&&(s.morphTargetDictionary=Object.assign({},this.morphTargetDictionary)),this.morphTargetInfluences!==void 0&&(s.morphTargetInfluences=this.morphTargetInfluences.slice()),this.isInstancedMesh&&(s.type="InstancedMesh",s.count=this.count,s.instanceMatrix=this.instanceMatrix.toJSON(),this.instanceColor!==null&&(s.instanceColor=this.instanceColor.toJSON())),this.isBatchedMesh&&(s.type="BatchedMesh",s.perObjectFrustumCulled=this.perObjectFrustumCulled,s.sortObjects=this.sortObjects,s.drawRanges=this._drawRanges,s.reservedRanges=this._reservedRanges,s.geometryInfo=this._geometryInfo.map(o=>({...o,boundingBox:o.boundingBox?o.boundingBox.toJSON():void 0,boundingSphere:o.boundingSphere?o.boundingSphere.toJSON():void 0})),s.instanceInfo=this._instanceInfo.map(o=>({...o})),s.availableInstanceIds=this._availableInstanceIds.slice(),s.availableGeometryIds=this._availableGeometryIds.slice(),s.nextIndexStart=this._nextIndexStart,s.nextVertexStart=this._nextVertexStart,s.geometryCount=this._geometryCount,s.maxInstanceCount=this._maxInstanceCount,s.maxVertexCount=this._maxVertexCount,s.maxIndexCount=this._maxIndexCount,s.geometryInitialized=this._geometryInitialized,s.matricesTexture=this._matricesTexture.toJSON(e),s.indirectTexture=this._indirectTexture.toJSON(e),this._colorsTexture!==null&&(s.colorsTexture=this._colorsTexture.toJSON(e)),this.boundingSphere!==null&&(s.boundingSphere=this.boundingSphere.toJSON()),this.boundingBox!==null&&(s.boundingBox=this.boundingBox.toJSON()));function r(o,c){return o[c.uuid]===void 0&&(o[c.uuid]=c.toJSON(e)),c.uuid}if(this.isScene)this.background&&(this.background.isColor?s.background=this.background.toJSON():this.background.isTexture&&(s.background=this.background.toJSON(e).uuid)),this.environment&&this.environment.isTexture&&this.environment.isRenderTargetTexture!==!0&&(s.environment=this.environment.toJSON(e).uuid);else if(this.isMesh||this.isLine||this.isPoints){s.geometry=r(e.geometries,this.geometry);let o=this.geometry.parameters;if(o!==void 0&&o.shapes!==void 0){let c=o.shapes;if(Array.isArray(c))for(let l=0,h=c.length;l<h;l++){let d=c[l];r(e.shapes,d)}else r(e.shapes,c)}}if(this.isSkinnedMesh&&(s.bindMode=this.bindMode,s.bindMatrix=this.bindMatrix.toArray(),this.skeleton!==void 0&&(r(e.skeletons,this.skeleton),s.skeleton=this.skeleton.uuid)),this.material!==void 0)if(Array.isArray(this.material)){let o=[];for(let c=0,l=this.material.length;c<l;c++)o.push(r(e.materials,this.material[c]));s.material=o}else s.material=r(e.materials,this.material);if(this.children.length>0){s.children=[];for(let o=0;o<this.children.length;o++)s.children.push(this.children[o].toJSON(e).object)}if(this.animations.length>0){s.animations=[];for(let o=0;o<this.animations.length;o++){let c=this.animations[o];s.animations.push(r(e.animations,c))}}if(t){let o=a(e.geometries),c=a(e.materials),l=a(e.textures),h=a(e.images),d=a(e.shapes),u=a(e.skeletons),p=a(e.animations),g=a(e.nodes);o.length>0&&(n.geometries=o),c.length>0&&(n.materials=c),l.length>0&&(n.textures=l),h.length>0&&(n.images=h),d.length>0&&(n.shapes=d),u.length>0&&(n.skeletons=u),p.length>0&&(n.animations=p),g.length>0&&(n.nodes=g)}return n.object=s,n;function a(o){let c=[];for(let l in o){let h=o[l];delete h.metadata,c.push(h)}return c}}clone(e){return new this.constructor().copy(this,e)}copy(e,t=!0){if(this.name=e.name,this.up.copy(e.up),this.position.copy(e.position),this.rotation.order=e.rotation.order,this.quaternion.copy(e.quaternion),this.scale.copy(e.scale),this.pivot=e.pivot!==null?e.pivot.clone():null,this.matrix.copy(e.matrix),this.matrixWorld.copy(e.matrixWorld),this.matrixAutoUpdate=e.matrixAutoUpdate,this.matrixWorldAutoUpdate=e.matrixWorldAutoUpdate,this.matrixWorldNeedsUpdate=e.matrixWorldNeedsUpdate,this.layers.mask=e.layers.mask,this.visible=e.visible,this.castShadow=e.castShadow,this.receiveShadow=e.receiveShadow,this.frustumCulled=e.frustumCulled,this.renderOrder=e.renderOrder,this.static=e.static,this.animations=e.animations.slice(),this.userData=JSON.parse(JSON.stringify(e.userData)),t===!0)for(let n=0;n<e.children.length;n++){let s=e.children[n];this.add(s.clone())}return this}dispose(){this.dispatchEvent({type:"dispose"})}};Tt.DEFAULT_UP=new L(0,1,0);Tt.DEFAULT_MATRIX_AUTO_UPDATE=!0;Tt.DEFAULT_MATRIX_WORLD_AUTO_UPDATE=!0;var bt=class extends Tt{constructor(){super(),this.isGroup=!0,this.type="Group"}},Ld={type:"move"},ms=class{constructor(){this._targetRay=null,this._grip=null,this._hand=null}getHandSpace(){return this._hand===null&&(this._hand=new bt,this._hand.matrixAutoUpdate=!1,this._hand.visible=!1,this._hand.joints={},this._hand.inputState={pinching:!1}),this._hand}getTargetRaySpace(){return this._targetRay===null&&(this._targetRay=new bt,this._targetRay.matrixAutoUpdate=!1,this._targetRay.visible=!1,this._targetRay.hasLinearVelocity=!1,this._targetRay.linearVelocity=new L,this._targetRay.hasAngularVelocity=!1,this._targetRay.angularVelocity=new L),this._targetRay}getGripSpace(){return this._grip===null&&(this._grip=new bt,this._grip.matrixAutoUpdate=!1,this._grip.visible=!1,this._grip.hasLinearVelocity=!1,this._grip.linearVelocity=new L,this._grip.hasAngularVelocity=!1,this._grip.angularVelocity=new L,this._grip.eventsEnabled=!1),this._grip}dispatchEvent(e){return this._targetRay!==null&&this._targetRay.dispatchEvent(e),this._grip!==null&&this._grip.dispatchEvent(e),this._hand!==null&&this._hand.dispatchEvent(e),this}connect(e){if(e&&e.hand){let t=this._hand;if(t)for(let n of e.hand.values())this._getHandJoint(t,n)}return this.dispatchEvent({type:"connected",data:e}),this}disconnect(e){return this.dispatchEvent({type:"disconnected",data:e}),this._targetRay!==null&&(this._targetRay.visible=!1),this._grip!==null&&(this._grip.visible=!1),this._hand!==null&&(this._hand.visible=!1),this}update(e,t,n){let s=null,r=null,a=null,o=this._targetRay,c=this._grip,l=this._hand;if(e&&t.session.visibilityState!=="visible-blurred"){if(l&&e.hand){a=!0;for(let v of e.hand.values()){let m=t.getJointPose(v,n),f=this._getHandJoint(l,v);m!==null&&(f.matrix.fromArray(m.transform.matrix),f.matrix.decompose(f.position,f.rotation,f.scale),f.matrixWorldNeedsUpdate=!0,f.jointRadius=m.radius),f.visible=m!==null}let h=l.joints["index-finger-tip"],d=l.joints["thumb-tip"],u=h.position.distanceTo(d.position),p=.02,g=.005;l.inputState.pinching&&u>p+g?(l.inputState.pinching=!1,this.dispatchEvent({type:"pinchend",handedness:e.handedness,target:this})):!l.inputState.pinching&&u<=p-g&&(l.inputState.pinching=!0,this.dispatchEvent({type:"pinchstart",handedness:e.handedness,target:this}))}else c!==null&&e.gripSpace&&(r=t.getPose(e.gripSpace,n),r!==null&&(c.matrix.fromArray(r.transform.matrix),c.matrix.decompose(c.position,c.rotation,c.scale),c.matrixWorldNeedsUpdate=!0,r.linearVelocity?(c.hasLinearVelocity=!0,c.linearVelocity.copy(r.linearVelocity)):c.hasLinearVelocity=!1,r.angularVelocity?(c.hasAngularVelocity=!0,c.angularVelocity.copy(r.angularVelocity)):c.hasAngularVelocity=!1,c.eventsEnabled&&c.dispatchEvent({type:"gripUpdated",data:e,target:this})));o!==null&&(s=t.getPose(e.targetRaySpace,n),s===null&&r!==null&&(s=r),s!==null&&(o.matrix.fromArray(s.transform.matrix),o.matrix.decompose(o.position,o.rotation,o.scale),o.matrixWorldNeedsUpdate=!0,s.linearVelocity?(o.hasLinearVelocity=!0,o.linearVelocity.copy(s.linearVelocity)):o.hasLinearVelocity=!1,s.angularVelocity?(o.hasAngularVelocity=!0,o.angularVelocity.copy(s.angularVelocity)):o.hasAngularVelocity=!1,this.dispatchEvent(Ld)))}return o!==null&&(o.visible=s!==null),c!==null&&(c.visible=r!==null),l!==null&&(l.visible=a!==null),this}_getHandJoint(e,t){if(e.joints[t.jointName]===void 0){let n=new bt;n.matrixAutoUpdate=!1,n.visible=!1,e.joints[t.jointName]=n,e.add(n)}return e.joints[t.jointName]}},_u={aliceblue:15792383,antiquewhite:16444375,aqua:65535,aquamarine:8388564,azure:15794175,beige:16119260,bisque:16770244,black:0,blanchedalmond:16772045,blue:255,blueviolet:9055202,brown:10824234,burlywood:14596231,cadetblue:6266528,chartreuse:8388352,chocolate:13789470,coral:16744272,cornflowerblue:6591981,cornsilk:16775388,crimson:14423100,cyan:65535,darkblue:139,darkcyan:35723,darkgoldenrod:12092939,darkgray:11119017,darkgreen:25600,darkgrey:11119017,darkkhaki:12433259,darkmagenta:9109643,darkolivegreen:5597999,darkorange:16747520,darkorchid:10040012,darkred:9109504,darksalmon:15308410,darkseagreen:9419919,darkslateblue:4734347,darkslategray:3100495,darkslategrey:3100495,darkturquoise:52945,darkviolet:9699539,deeppink:16716947,deepskyblue:49151,dimgray:6908265,dimgrey:6908265,dodgerblue:2003199,firebrick:11674146,floralwhite:16775920,forestgreen:2263842,fuchsia:16711935,gainsboro:14474460,ghostwhite:16316671,gold:16766720,goldenrod:14329120,gray:8421504,green:32768,greenyellow:11403055,grey:8421504,honeydew:15794160,hotpink:16738740,indianred:13458524,indigo:4915330,ivory:16777200,khaki:15787660,lavender:15132410,lavenderblush:16773365,lawngreen:8190976,lemonchiffon:16775885,lightblue:11393254,lightcoral:15761536,lightcyan:14745599,lightgoldenrodyellow:16448210,lightgray:13882323,lightgreen:9498256,lightgrey:13882323,lightpink:16758465,lightsalmon:16752762,lightseagreen:2142890,lightskyblue:8900346,lightslategray:7833753,lightslategrey:7833753,lightsteelblue:11584734,lightyellow:16777184,lime:65280,limegreen:3329330,linen:16445670,magenta:16711935,maroon:8388608,mediumaquamarine:6737322,mediumblue:205,mediumorchid:12211667,mediumpurple:9662683,mediumseagreen:3978097,mediumslateblue:8087790,mediumspringgreen:64154,mediumturquoise:4772300,mediumvioletred:13047173,midnightblue:1644912,mintcream:16121850,mistyrose:16770273,moccasin:16770229,navajowhite:16768685,navy:128,oldlace:16643558,olive:8421376,olivedrab:7048739,orange:16753920,orangered:16729344,orchid:14315734,palegoldenrod:15657130,palegreen:10025880,paleturquoise:11529966,palevioletred:14381203,papayawhip:16773077,peachpuff:16767673,peru:13468991,pink:16761035,plum:14524637,powderblue:11591910,purple:8388736,rebeccapurple:6697881,red:16711680,rosybrown:12357519,royalblue:4286945,saddlebrown:9127187,salmon:16416882,sandybrown:16032864,seagreen:3050327,seashell:16774638,sienna:10506797,silver:12632256,skyblue:8900331,slateblue:6970061,slategray:7372944,slategrey:7372944,snow:16775930,springgreen:65407,steelblue:4620980,tan:13808780,teal:32896,thistle:14204888,tomato:16737095,turquoise:4251856,violet:15631086,wheat:16113331,white:16777215,whitesmoke:16119285,yellow:16776960,yellowgreen:10145074},ii={h:0,s:0,l:0},Qr={h:0,s:0,l:0};function gl(i,e,t){return t<0&&(t+=1),t>1&&(t-=1),t<1/6?i+(e-i)*6*t:t<1/2?e:t<2/3?i+(e-i)*6*(2/3-t):i}var Fe=class{constructor(e,t,n){return this.isColor=!0,this.r=1,this.g=1,this.b=1,this.set(e,t,n)}set(e,t,n){if(t===void 0&&n===void 0){let s=e;s&&s.isColor?this.copy(s):typeof s=="number"?this.setHex(s):typeof s=="string"&&this.setStyle(s)}else this.setRGB(e,t,n);return this}setScalar(e){return this.r=e,this.g=e,this.b=e,this}setHex(e,t=yt){return e=Math.floor(e),this.r=(e>>16&255)/255,this.g=(e>>8&255)/255,this.b=(e&255)/255,st.colorSpaceToWorking(this,t),this}setRGB(e,t,n,s=st.workingColorSpace){return this.r=e,this.g=t,this.b=n,st.colorSpaceToWorking(this,s),this}setHSL(e,t,n,s=st.workingColorSpace){if(e=xc(e,1),t=nt(t,0,1),n=nt(n,0,1),t===0)this.r=this.g=this.b=n;else{let r=n<=.5?n*(1+t):n+t-n*t,a=2*n-r;this.r=gl(a,r,e+1/3),this.g=gl(a,r,e),this.b=gl(a,r,e-1/3)}return st.colorSpaceToWorking(this,s),this}setStyle(e,t=yt){function n(r){r!==void 0&&parseFloat(r)<1&&ke("Color: Alpha component of "+e+" will be ignored.")}let s;if(s=/^(\w+)\(([^\)]*)\)/.exec(e)){let r,a=s[1],o=s[2];switch(a){case"rgb":case"rgba":if(r=/^\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*(\d*\.?\d+)\s*)?$/.exec(o))return n(r[4]),this.setRGB(Math.min(255,parseInt(r[1],10))/255,Math.min(255,parseInt(r[2],10))/255,Math.min(255,parseInt(r[3],10))/255,t);if(r=/^\s*(\d+)\%\s*,\s*(\d+)\%\s*,\s*(\d+)\%\s*(?:,\s*(\d*\.?\d+)\s*)?$/.exec(o))return n(r[4]),this.setRGB(Math.min(100,parseInt(r[1],10))/100,Math.min(100,parseInt(r[2],10))/100,Math.min(100,parseInt(r[3],10))/100,t);break;case"hsl":case"hsla":if(r=/^\s*(\d*\.?\d+)\s*,\s*(\d*\.?\d+)\%\s*,\s*(\d*\.?\d+)\%\s*(?:,\s*(\d*\.?\d+)\s*)?$/.exec(o))return n(r[4]),this.setHSL(parseFloat(r[1])/360,parseFloat(r[2])/100,parseFloat(r[3])/100,t);break;default:ke("Color: Unknown color model "+e)}}else if(s=/^\#([A-Fa-f\d]+)$/.exec(e)){let r=s[1],a=r.length;if(a===3)return this.setRGB(parseInt(r.charAt(0),16)/15,parseInt(r.charAt(1),16)/15,parseInt(r.charAt(2),16)/15,t);if(a===6)return this.setHex(parseInt(r,16),t);ke("Color: Invalid hex color "+e)}else if(e&&e.length>0)return this.setColorName(e,t);return this}setColorName(e,t=yt){let n=_u[e.toLowerCase()];return n!==void 0?this.setHex(n,t):ke("Color: Unknown color "+e),this}clone(){return new this.constructor(this.r,this.g,this.b)}copy(e){return this.r=e.r,this.g=e.g,this.b=e.b,this}copySRGBToLinear(e){return this.r=Jn(e.r),this.g=Jn(e.g),this.b=Jn(e.b),this}copyLinearToSRGB(e){return this.r=os(e.r),this.g=os(e.g),this.b=os(e.b),this}convertSRGBToLinear(){return this.copySRGBToLinear(this),this}convertLinearToSRGB(){return this.copyLinearToSRGB(this),this}getHex(e=yt){return st.workingToColorSpace(zt.copy(this),e),Math.round(nt(zt.r*255,0,255))*65536+Math.round(nt(zt.g*255,0,255))*256+Math.round(nt(zt.b*255,0,255))}getHexString(e=yt){return("000000"+this.getHex(e).toString(16)).slice(-6)}getHSL(e,t=st.workingColorSpace){st.workingToColorSpace(zt.copy(this),t);let n=zt.r,s=zt.g,r=zt.b,a=Math.max(n,s,r),o=Math.min(n,s,r),c,l,h=(o+a)/2;if(o===a)c=0,l=0;else{let d=a-o;switch(l=h<=.5?d/(a+o):d/(2-a-o),a){case n:c=(s-r)/d+(s<r?6:0);break;case s:c=(r-n)/d+2;break;case r:c=(n-s)/d+4;break}c/=6}return e.h=c,e.s=l,e.l=h,e}getRGB(e,t=st.workingColorSpace){return st.workingToColorSpace(zt.copy(this),t),e.r=zt.r,e.g=zt.g,e.b=zt.b,e}getStyle(e=yt){st.workingToColorSpace(zt.copy(this),e);let t=zt.r,n=zt.g,s=zt.b;return e!==yt?`color(${e} ${t.toFixed(3)} ${n.toFixed(3)} ${s.toFixed(3)})`:`rgb(${Math.round(t*255)},${Math.round(n*255)},${Math.round(s*255)})`}offsetHSL(e,t,n){return this.getHSL(ii),this.setHSL(ii.h+e,ii.s+t,ii.l+n)}add(e){return this.r+=e.r,this.g+=e.g,this.b+=e.b,this}addColors(e,t){return this.r=e.r+t.r,this.g=e.g+t.g,this.b=e.b+t.b,this}addScalar(e){return this.r+=e,this.g+=e,this.b+=e,this}sub(e){return this.r=Math.max(0,this.r-e.r),this.g=Math.max(0,this.g-e.g),this.b=Math.max(0,this.b-e.b),this}multiply(e){return this.r*=e.r,this.g*=e.g,this.b*=e.b,this}multiplyScalar(e){return this.r*=e,this.g*=e,this.b*=e,this}lerp(e,t){return this.r+=(e.r-this.r)*t,this.g+=(e.g-this.g)*t,this.b+=(e.b-this.b)*t,this}lerpColors(e,t,n){return this.r=e.r+(t.r-e.r)*n,this.g=e.g+(t.g-e.g)*n,this.b=e.b+(t.b-e.b)*n,this}lerpHSL(e,t){this.getHSL(ii),e.getHSL(Qr);let n=Ks(ii.h,Qr.h,t),s=Ks(ii.s,Qr.s,t),r=Ks(ii.l,Qr.l,t);return this.setHSL(n,s,r),this}setFromVector3(e){return this.r=e.x,this.g=e.y,this.b=e.z,this}applyMatrix3(e){let t=this.r,n=this.g,s=this.b,r=e.elements;return this.r=r[0]*t+r[3]*n+r[6]*s,this.g=r[1]*t+r[4]*n+r[7]*s,this.b=r[2]*t+r[5]*n+r[8]*s,this}equals(e){return e.r===this.r&&e.g===this.g&&e.b===this.b}fromArray(e,t=0){return this.r=e[t],this.g=e[t+1],this.b=e[t+2],this}toArray(e=[],t=0){return e[t]=this.r,e[t+1]=this.g,e[t+2]=this.b,e}fromBufferAttribute(e,t){return this.r=e.getX(t),this.g=e.getY(t),this.b=e.getZ(t),this}toJSON(){return this.getHex()}*[Symbol.iterator](){yield this.r,yield this.g,yield this.b}},zt=new Fe;Fe.NAMES=_u;var gs=class extends Tt{constructor(){super(),this.isScene=!0,this.type="Scene",this.background=null,this.environment=null,this.fog=null,this.backgroundBlurriness=0,this.backgroundIntensity=1,this.backgroundRotation=new _n,this.environmentIntensity=1,this.environmentRotation=new _n,this.overrideMaterial=null,typeof __THREE_DEVTOOLS__<"u"&&__THREE_DEVTOOLS__.dispatchEvent(new CustomEvent("observe",{detail:this}))}copy(e,t){return super.copy(e,t),e.background!==null&&(this.background=e.background.clone()),e.environment!==null&&(this.environment=e.environment.clone()),e.fog!==null&&(this.fog=e.fog.clone()),this.backgroundBlurriness=e.backgroundBlurriness,this.backgroundIntensity=e.backgroundIntensity,this.backgroundRotation.copy(e.backgroundRotation),this.environmentIntensity=e.environmentIntensity,this.environmentRotation.copy(e.environmentRotation),e.overrideMaterial!==null&&(this.overrideMaterial=e.overrideMaterial.clone()),this.matrixAutoUpdate=e.matrixAutoUpdate,this}toJSON(e){let t=super.toJSON(e);return this.fog!==null&&(t.object.fog=this.fog.toJSON()),t.object.backgroundBlurriness=this.backgroundBlurriness,t.object.backgroundIntensity=this.backgroundIntensity,t.object.backgroundRotation=this.backgroundRotation.toArray(),t.object.environmentIntensity=this.environmentIntensity,t.object.environmentRotation=this.environmentRotation.toArray(),t}},dn=new L,Wn=new L,_l=new L,Xn=new L,qi=new L,Yi=new L,sh=new L,xl=new L,yl=new L,vl=new L,Ml=new vt,Sl=new vt,bl=new vt,Zn=class i{constructor(e=new L,t=new L,n=new L){this.a=e,this.b=t,this.c=n}static getNormal(e,t,n,s){s.subVectors(n,t),dn.subVectors(e,t),s.cross(dn);let r=s.lengthSq();return r>0?s.multiplyScalar(1/Math.sqrt(r)):s.set(0,0,0)}static getBarycoord(e,t,n,s,r){dn.subVectors(s,t),Wn.subVectors(n,t),_l.subVectors(e,t);let a=dn.dot(dn),o=dn.dot(Wn),c=dn.dot(_l),l=Wn.dot(Wn),h=Wn.dot(_l),d=a*l-o*o;if(d===0)return r.set(0,0,0),null;let u=1/d,p=(l*c-o*h)*u,g=(a*h-o*c)*u;return r.set(1-p-g,g,p)}static containsPoint(e,t,n,s){return this.getBarycoord(e,t,n,s,Xn)===null?!1:Xn.x>=0&&Xn.y>=0&&Xn.x+Xn.y<=1}static getInterpolation(e,t,n,s,r,a,o,c){return this.getBarycoord(e,t,n,s,Xn)===null?(c.x=0,c.y=0,"z"in c&&(c.z=0),"w"in c&&(c.w=0),null):(c.setScalar(0),c.addScaledVector(r,Xn.x),c.addScaledVector(a,Xn.y),c.addScaledVector(o,Xn.z),c)}static getInterpolatedAttribute(e,t,n,s,r,a){return Ml.setScalar(0),Sl.setScalar(0),bl.setScalar(0),Ml.fromBufferAttribute(e,t),Sl.fromBufferAttribute(e,n),bl.fromBufferAttribute(e,s),a.setScalar(0),a.addScaledVector(Ml,r.x),a.addScaledVector(Sl,r.y),a.addScaledVector(bl,r.z),a}static isFrontFacing(e,t,n,s){return dn.subVectors(n,t),Wn.subVectors(e,t),dn.cross(Wn).dot(s)<0}set(e,t,n){return this.a.copy(e),this.b.copy(t),this.c.copy(n),this}setFromPointsAndIndices(e,t,n,s){return this.a.copy(e[t]),this.b.copy(e[n]),this.c.copy(e[s]),this}setFromAttributeAndIndices(e,t,n,s){return this.a.fromBufferAttribute(e,t),this.b.fromBufferAttribute(e,n),this.c.fromBufferAttribute(e,s),this}clone(){return new this.constructor().copy(this)}copy(e){return this.a.copy(e.a),this.b.copy(e.b),this.c.copy(e.c),this}getArea(){return dn.subVectors(this.c,this.b),Wn.subVectors(this.a,this.b),dn.cross(Wn).length()*.5}getMidpoint(e){return e.addVectors(this.a,this.b).add(this.c).multiplyScalar(1/3)}getNormal(e){return i.getNormal(this.a,this.b,this.c,e)}getPlane(e){return e.setFromCoplanarPoints(this.a,this.b,this.c)}getBarycoord(e,t){return i.getBarycoord(e,this.a,this.b,this.c,t)}getInterpolation(e,t,n,s,r){return i.getInterpolation(e,this.a,this.b,this.c,t,n,s,r)}containsPoint(e){return i.containsPoint(e,this.a,this.b,this.c)}isFrontFacing(e){return i.isFrontFacing(this.a,this.b,this.c,e)}intersectsBox(e){return e.intersectsTriangle(this)}closestPointToPoint(e,t){let n=this.a,s=this.b,r=this.c,a,o;qi.subVectors(s,n),Yi.subVectors(r,n),xl.subVectors(e,n);let c=qi.dot(xl),l=Yi.dot(xl);if(c<=0&&l<=0)return t.copy(n);yl.subVectors(e,s);let h=qi.dot(yl),d=Yi.dot(yl);if(h>=0&&d<=h)return t.copy(s);let u=c*d-h*l;if(u<=0&&c>=0&&h<=0)return a=c/(c-h),t.copy(n).addScaledVector(qi,a);vl.subVectors(e,r);let p=qi.dot(vl),g=Yi.dot(vl);if(g>=0&&p<=g)return t.copy(r);let v=p*l-c*g;if(v<=0&&l>=0&&g<=0)return o=l/(l-g),t.copy(n).addScaledVector(Yi,o);let m=h*g-p*d;if(m<=0&&d-h>=0&&p-g>=0)return sh.subVectors(r,s),o=(d-h)/(d-h+(p-g)),t.copy(s).addScaledVector(sh,o);let f=1/(m+v+u);return a=v*f,o=u*f,t.copy(n).addScaledVector(qi,a).addScaledVector(Yi,o)}equals(e){return e.a.equals(this.a)&&e.b.equals(this.b)&&e.c.equals(this.c)}},Ln=class{constructor(e=new L(1/0,1/0,1/0),t=new L(-1/0,-1/0,-1/0)){this.isBox3=!0,this.min=e,this.max=t}set(e,t){return this.min.copy(e),this.max.copy(t),this}setFromArray(e){this.makeEmpty();for(let t=0,n=e.length;t<n;t+=3)this.expandByPoint(fn.fromArray(e,t));return this}setFromBufferAttribute(e){this.makeEmpty();for(let t=0,n=e.count;t<n;t++)this.expandByPoint(fn.fromBufferAttribute(e,t));return this}setFromPoints(e){this.makeEmpty();for(let t=0,n=e.length;t<n;t++)this.expandByPoint(e[t]);return this}setFromCenterAndSize(e,t){let n=fn.copy(t).multiplyScalar(.5);return this.min.copy(e).sub(n),this.max.copy(e).add(n),this}setFromObject(e,t=!1){return this.makeEmpty(),this.expandByObject(e,t)}clone(){return new this.constructor().copy(this)}copy(e){return this.min.copy(e.min),this.max.copy(e.max),this}makeEmpty(){return this.min.x=this.min.y=this.min.z=1/0,this.max.x=this.max.y=this.max.z=-1/0,this}isEmpty(){return this.max.x<this.min.x||this.max.y<this.min.y||this.max.z<this.min.z}getCenter(e){return this.isEmpty()?e.set(0,0,0):e.addVectors(this.min,this.max).multiplyScalar(.5)}getSize(e){return this.isEmpty()?e.set(0,0,0):e.subVectors(this.max,this.min)}expandByPoint(e){return this.min.min(e),this.max.max(e),this}expandByVector(e){return this.min.sub(e),this.max.add(e),this}expandByScalar(e){return this.min.addScalar(-e),this.max.addScalar(e),this}expandByObject(e,t=!1){e.updateWorldMatrix(!1,!1);let n=e.geometry;if(n!==void 0){let r=n.getAttribute("position");if(t===!0&&r!==void 0&&e.isInstancedMesh!==!0)for(let a=0,o=r.count;a<o;a++)e.isMesh===!0?e.getVertexPosition(a,fn):fn.fromBufferAttribute(r,a),fn.applyMatrix4(e.matrixWorld),this.expandByPoint(fn);else e.boundingBox!==void 0?(e.boundingBox===null&&e.computeBoundingBox(),jr.copy(e.boundingBox)):(n.boundingBox===null&&n.computeBoundingBox(),jr.copy(n.boundingBox)),jr.applyMatrix4(e.matrixWorld),this.union(jr)}let s=e.children;for(let r=0,a=s.length;r<a;r++)this.expandByObject(s[r],t);return this}containsPoint(e){return e.x>=this.min.x&&e.x<=this.max.x&&e.y>=this.min.y&&e.y<=this.max.y&&e.z>=this.min.z&&e.z<=this.max.z}containsBox(e){return this.min.x<=e.min.x&&e.max.x<=this.max.x&&this.min.y<=e.min.y&&e.max.y<=this.max.y&&this.min.z<=e.min.z&&e.max.z<=this.max.z}getParameter(e,t){return t.set((e.x-this.min.x)/(this.max.x-this.min.x),(e.y-this.min.y)/(this.max.y-this.min.y),(e.z-this.min.z)/(this.max.z-this.min.z))}intersectsBox(e){return e.max.x>=this.min.x&&e.min.x<=this.max.x&&e.max.y>=this.min.y&&e.min.y<=this.max.y&&e.max.z>=this.min.z&&e.min.z<=this.max.z}intersectsSphere(e){return this.clampPoint(e.center,fn),fn.distanceToSquared(e.center)<=e.radius*e.radius}intersectsPlane(e){let t,n;return e.normal.x>0?(t=e.normal.x*this.min.x,n=e.normal.x*this.max.x):(t=e.normal.x*this.max.x,n=e.normal.x*this.min.x),e.normal.y>0?(t+=e.normal.y*this.min.y,n+=e.normal.y*this.max.y):(t+=e.normal.y*this.max.y,n+=e.normal.y*this.min.y),e.normal.z>0?(t+=e.normal.z*this.min.z,n+=e.normal.z*this.max.z):(t+=e.normal.z*this.max.z,n+=e.normal.z*this.min.z),t<=-e.constant&&n>=-e.constant}intersectsTriangle(e){if(this.isEmpty())return!1;this.getCenter(Hs),ea.subVectors(this.max,Hs),Zi.subVectors(e.a,Hs),Ji.subVectors(e.b,Hs),$i.subVectors(e.c,Hs),si.subVectors(Ji,Zi),ri.subVectors($i,Ji),xi.subVectors(Zi,$i);let t=[0,-si.z,si.y,0,-ri.z,ri.y,0,-xi.z,xi.y,si.z,0,-si.x,ri.z,0,-ri.x,xi.z,0,-xi.x,-si.y,si.x,0,-ri.y,ri.x,0,-xi.y,xi.x,0];return!El(t,Zi,Ji,$i,ea)||(t=[1,0,0,0,1,0,0,0,1],!El(t,Zi,Ji,$i,ea))?!1:(ta.crossVectors(si,ri),t=[ta.x,ta.y,ta.z],El(t,Zi,Ji,$i,ea))}clampPoint(e,t){return t.copy(e).clamp(this.min,this.max)}distanceToPoint(e){return this.clampPoint(e,fn).distanceTo(e)}getBoundingSphere(e){return this.isEmpty()?e.makeEmpty():(this.getCenter(e.center),e.radius=this.getSize(fn).length()*.5),e}intersect(e){return this.min.max(e.min),this.max.min(e.max),this.isEmpty()&&this.makeEmpty(),this}union(e){return this.min.min(e.min),this.max.max(e.max),this}applyMatrix4(e){return this.isEmpty()?this:(qn[0].set(this.min.x,this.min.y,this.min.z).applyMatrix4(e),qn[1].set(this.min.x,this.min.y,this.max.z).applyMatrix4(e),qn[2].set(this.min.x,this.max.y,this.min.z).applyMatrix4(e),qn[3].set(this.min.x,this.max.y,this.max.z).applyMatrix4(e),qn[4].set(this.max.x,this.min.y,this.min.z).applyMatrix4(e),qn[5].set(this.max.x,this.min.y,this.max.z).applyMatrix4(e),qn[6].set(this.max.x,this.max.y,this.min.z).applyMatrix4(e),qn[7].set(this.max.x,this.max.y,this.max.z).applyMatrix4(e),this.setFromPoints(qn),this)}translate(e){return this.min.add(e),this.max.add(e),this}equals(e){return e.min.equals(this.min)&&e.max.equals(this.max)}toJSON(){return{min:this.min.toArray(),max:this.max.toArray()}}fromJSON(e){return this.min.fromArray(e.min),this.max.fromArray(e.max),this}},qn=[new L,new L,new L,new L,new L,new L,new L,new L],fn=new L,jr=new Ln,Zi=new L,Ji=new L,$i=new L,si=new L,ri=new L,xi=new L,Hs=new L,ea=new L,ta=new L,yi=new L;function El(i,e,t,n,s){for(let r=0,a=i.length-3;r<=a;r+=3){yi.fromArray(i,r);let o=s.x*Math.abs(yi.x)+s.y*Math.abs(yi.y)+s.z*Math.abs(yi.z),c=e.dot(yi),l=t.dot(yi),h=n.dot(yi);if(Math.max(-Math.max(c,l,h),Math.min(c,l,h))>o)return!1}return!0}var wt=new L,na=new he,Dd=0,Ht=class extends In{constructor(e,t,n=!1){if(super(),Array.isArray(e))throw new TypeError("THREE.BufferAttribute: array should be a Typed Array.");this.isBufferAttribute=!0,Object.defineProperty(this,"id",{value:Dd++}),this.name="",this.array=e,this.itemSize=t,this.count=e!==void 0?e.length/t:0,this.normalized=n,this.usage=gc,this.updateRanges=[],this.gpuType=ln,this.version=0}onUploadCallback(){}set needsUpdate(e){e===!0&&this.version++}setUsage(e){return this.usage=e,this}addUpdateRange(e,t){this.updateRanges.push({start:e,count:t})}clearUpdateRanges(){this.updateRanges.length=0}copy(e){return this.name=e.name,this.array=new e.array.constructor(e.array),this.itemSize=e.itemSize,this.count=e.count,this.normalized=e.normalized,this.usage=e.usage,this.gpuType=e.gpuType,this}copyAt(e,t,n){e*=this.itemSize,n*=t.itemSize;for(let s=0,r=this.itemSize;s<r;s++)this.array[e+s]=t.array[n+s];return this}copyArray(e){return this.array.set(e),this}applyMatrix3(e){if(this.itemSize===2)for(let t=0,n=this.count;t<n;t++)na.fromBufferAttribute(this,t),na.applyMatrix3(e),this.setXY(t,na.x,na.y);else if(this.itemSize===3)for(let t=0,n=this.count;t<n;t++)wt.fromBufferAttribute(this,t),wt.applyMatrix3(e),this.setXYZ(t,wt.x,wt.y,wt.z);return this}applyMatrix4(e){for(let t=0,n=this.count;t<n;t++)wt.fromBufferAttribute(this,t),wt.applyMatrix4(e),this.setXYZ(t,wt.x,wt.y,wt.z);return this}applyNormalMatrix(e){for(let t=0,n=this.count;t<n;t++)wt.fromBufferAttribute(this,t),wt.applyNormalMatrix(e),this.setXYZ(t,wt.x,wt.y,wt.z);return this}transformDirection(e){for(let t=0,n=this.count;t<n;t++)wt.fromBufferAttribute(this,t),wt.transformDirection(e),this.setXYZ(t,wt.x,wt.y,wt.z);return this}set(e,t=0){return this.array.set(e,t),this}getComponent(e,t){let n=this.array[e*this.itemSize+t];return this.normalized&&(n=mn(n,this.array)),n}setComponent(e,t,n){return this.normalized&&(n=pt(n,this.array)),this.array[e*this.itemSize+t]=n,this}getX(e){let t=this.array[e*this.itemSize];return this.normalized&&(t=mn(t,this.array)),t}setX(e,t){return this.normalized&&(t=pt(t,this.array)),this.array[e*this.itemSize]=t,this}getY(e){let t=this.array[e*this.itemSize+1];return this.normalized&&(t=mn(t,this.array)),t}setY(e,t){return this.normalized&&(t=pt(t,this.array)),this.array[e*this.itemSize+1]=t,this}getZ(e){let t=this.array[e*this.itemSize+2];return this.normalized&&(t=mn(t,this.array)),t}setZ(e,t){return this.normalized&&(t=pt(t,this.array)),this.array[e*this.itemSize+2]=t,this}getW(e){let t=this.array[e*this.itemSize+3];return this.normalized&&(t=mn(t,this.array)),t}setW(e,t){return this.normalized&&(t=pt(t,this.array)),this.array[e*this.itemSize+3]=t,this}setXY(e,t,n){return e*=this.itemSize,this.normalized&&(t=pt(t,this.array),n=pt(n,this.array)),this.array[e+0]=t,this.array[e+1]=n,this}setXYZ(e,t,n,s){return e*=this.itemSize,this.normalized&&(t=pt(t,this.array),n=pt(n,this.array),s=pt(s,this.array)),this.array[e+0]=t,this.array[e+1]=n,this.array[e+2]=s,this}setXYZW(e,t,n,s,r){return e*=this.itemSize,this.normalized&&(t=pt(t,this.array),n=pt(n,this.array),s=pt(s,this.array),r=pt(r,this.array)),this.array[e+0]=t,this.array[e+1]=n,this.array[e+2]=s,this.array[e+3]=r,this}onUpload(e){return this.onUploadCallback=e,this}clone(){return new this.constructor(this.array,this.itemSize).copy(this)}toJSON(){let e={itemSize:this.itemSize,type:this.array.constructor.name,array:Array.from(this.array),normalized:this.normalized};return e.name=this.name,e.usage=this.usage,e.gpuType=this.gpuType,e}dispose(){this.dispatchEvent({type:"dispose"})}};var rr=class extends Ht{constructor(e,t,n){super(new Uint16Array(e),t,n)}};var ar=class extends Ht{constructor(e,t,n){super(new Uint32Array(e),t,n)}};var it=class extends Ht{constructor(e,t,n){super(new Float32Array(e),t,n)}},Ud=new Ln,Vs=new L,Tl=new L,Dn=class{constructor(e=new L,t=-1){this.isSphere=!0,this.center=e,this.radius=t}set(e,t){return this.center.copy(e),this.radius=t,this}setFromPoints(e,t){let n=this.center;t!==void 0?n.copy(t):Ud.setFromPoints(e).getCenter(n);let s=0;for(let r=0,a=e.length;r<a;r++)s=Math.max(s,n.distanceToSquared(e[r]));return this.radius=Math.sqrt(s),this}copy(e){return this.center.copy(e.center),this.radius=e.radius,this}isEmpty(){return this.radius<0}makeEmpty(){return this.center.set(0,0,0),this.radius=-1,this}containsPoint(e){return e.distanceToSquared(this.center)<=this.radius*this.radius}distanceToPoint(e){return e.distanceTo(this.center)-this.radius}intersectsSphere(e){let t=this.radius+e.radius;return e.center.distanceToSquared(this.center)<=t*t}intersectsBox(e){return e.intersectsSphere(this)}intersectsPlane(e){return Math.abs(e.distanceToPoint(this.center))<=this.radius}clampPoint(e,t){let n=this.center.distanceToSquared(e);return t.copy(e),n>this.radius*this.radius&&(t.sub(this.center).normalize(),t.multiplyScalar(this.radius).add(this.center)),t}getBoundingBox(e){return this.isEmpty()?(e.makeEmpty(),e):(e.set(this.center,this.center),e.expandByScalar(this.radius),e)}applyMatrix4(e){return this.center.applyMatrix4(e),this.radius=this.radius*e.getMaxScaleOnAxis(),this}translate(e){return this.center.add(e),this}expandByPoint(e){if(this.isEmpty())return this.center.copy(e),this.radius=0,this;Vs.subVectors(e,this.center);let t=Vs.lengthSq();if(t>this.radius*this.radius){let n=Math.sqrt(t),s=(n-this.radius)*.5;this.center.addScaledVector(Vs,s/n),this.radius+=s}return this}union(e){return e.isEmpty()?this:this.isEmpty()?(this.copy(e),this):(this.center.equals(e.center)===!0?this.radius=Math.max(this.radius,e.radius):(Tl.subVectors(e.center,this.center).setLength(e.radius),this.expandByPoint(Vs.copy(e.center).add(Tl)),this.expandByPoint(Vs.copy(e.center).sub(Tl))),this)}equals(e){return e.center.equals(this.center)&&e.radius===this.radius}clone(){return new this.constructor().copy(this)}toJSON(){return{radius:this.radius,center:this.center.toArray()}}fromJSON(e){return this.radius=e.radius,this.center.fromArray(e.center),this}},Nd=0,an=new ut,wl=new Tt,Ki=new L,Qt=new Ln,ks=new Ln,Pt=new L,Mt=class i extends In{constructor(){super(),this.isBufferGeometry=!0,Object.defineProperty(this,"id",{value:Nd++}),this.uuid=Rn(),this.name="",this.type="BufferGeometry",this.index=null,this.indirect=null,this.indirectOffset=0,this.attributes={},this.morphAttributes={},this.morphTargetsRelative=!1,this.groups=[],this.boundingBox=null,this.boundingSphere=null,this.drawRange={start:0,count:1/0},this.userData={},this._transformed=!1}getIndex(){return this.index}setIndex(e){return Array.isArray(e)?this.index=new(rd(e)?ar:rr)(e,1):this.index=e,this}setIndirect(e,t=0){return this.indirect=e,this.indirectOffset=t,this}getIndirect(){return this.indirect}getAttribute(e){return this.attributes[e]}setAttribute(e,t){return this.attributes[e]=t,this}deleteAttribute(e){return delete this.attributes[e],this}hasAttribute(e){return this.attributes[e]!==void 0}addGroup(e,t,n=0){this.groups.push({start:e,count:t,materialIndex:n})}clearGroups(){this.groups=[]}setDrawRange(e,t){this.drawRange.start=e,this.drawRange.count=t}applyMatrix4(e){let t=this.attributes.position;t!==void 0&&(t.applyMatrix4(e),t.needsUpdate=!0);let n=this.attributes.normal;if(n!==void 0){let r=new Je().getNormalMatrix(e);n.applyNormalMatrix(r),n.needsUpdate=!0}let s=this.attributes.tangent;return s!==void 0&&(s.transformDirection(e),s.needsUpdate=!0),this.boundingBox!==null&&this.computeBoundingBox(),this.boundingSphere!==null&&this.computeBoundingSphere(),this._transformed=!0,this}applyQuaternion(e){return an.makeRotationFromQuaternion(e),this.applyMatrix4(an),this}rotateX(e){return an.makeRotationX(e),this.applyMatrix4(an),this}rotateY(e){return an.makeRotationY(e),this.applyMatrix4(an),this}rotateZ(e){return an.makeRotationZ(e),this.applyMatrix4(an),this}translate(e,t,n){return an.makeTranslation(e,t,n),this.applyMatrix4(an),this}scale(e,t,n){return an.makeScale(e,t,n),this.applyMatrix4(an),this}lookAt(e){return wl.lookAt(e),wl.updateMatrix(),this.applyMatrix4(wl.matrix),this}center(){return this.computeBoundingBox(),this.boundingBox.getCenter(Ki).negate(),this.translate(Ki.x,Ki.y,Ki.z),this}setFromPoints(e){let t=this.getAttribute("position");if(t===void 0){let n=[];for(let s=0,r=e.length;s<r;s++){let a=e[s];n.push(a.x,a.y,a.z||0)}this.setAttribute("position",new it(n,3))}else{let n=Math.min(e.length,t.count);for(let s=0;s<n;s++){let r=e[s];t.setXYZ(s,r.x,r.y,r.z||0)}e.length>t.count&&ke("BufferGeometry: Buffer size too small for points data. Use .dispose() and create a new geometry."),t.needsUpdate=!0}return this}computeBoundingBox(){this.boundingBox===null&&(this.boundingBox=new Ln);let e=this.attributes.position,t=this.morphAttributes.position;if(e&&e.isGLBufferAttribute){We("BufferGeometry.computeBoundingBox(): GLBufferAttribute requires a manual bounding box.",this),this.boundingBox.set(new L(-1/0,-1/0,-1/0),new L(1/0,1/0,1/0));return}if(e!==void 0){if(this.boundingBox.setFromBufferAttribute(e),t)for(let n=0,s=t.length;n<s;n++){let r=t[n];Qt.setFromBufferAttribute(r),this.morphTargetsRelative?(Pt.addVectors(this.boundingBox.min,Qt.min),this.boundingBox.expandByPoint(Pt),Pt.addVectors(this.boundingBox.max,Qt.max),this.boundingBox.expandByPoint(Pt)):(this.boundingBox.expandByPoint(Qt.min),this.boundingBox.expandByPoint(Qt.max))}}else this.boundingBox.makeEmpty();(isNaN(this.boundingBox.min.x)||isNaN(this.boundingBox.min.y)||isNaN(this.boundingBox.min.z))&&We('BufferGeometry.computeBoundingBox(): Computed min/max have NaN values. The "position" attribute is likely to have NaN values.',this)}computeBoundingSphere(){this.boundingSphere===null&&(this.boundingSphere=new Dn);let e=this.attributes.position,t=this.morphAttributes.position;if(e&&e.isGLBufferAttribute){We("BufferGeometry.computeBoundingSphere(): GLBufferAttribute requires a manual bounding sphere.",this),this.boundingSphere.set(new L,1/0);return}if(e){let n=this.boundingSphere.center;if(Qt.setFromBufferAttribute(e),t)for(let r=0,a=t.length;r<a;r++){let o=t[r];ks.setFromBufferAttribute(o),this.morphTargetsRelative?(Pt.addVectors(Qt.min,ks.min),Qt.expandByPoint(Pt),Pt.addVectors(Qt.max,ks.max),Qt.expandByPoint(Pt)):(Qt.expandByPoint(ks.min),Qt.expandByPoint(ks.max))}Qt.getCenter(n);let s=0;for(let r=0,a=e.count;r<a;r++)Pt.fromBufferAttribute(e,r),s=Math.max(s,n.distanceToSquared(Pt));if(t)for(let r=0,a=t.length;r<a;r++){let o=t[r],c=this.morphTargetsRelative;for(let l=0,h=o.count;l<h;l++)Pt.fromBufferAttribute(o,l),c&&(Ki.fromBufferAttribute(e,l),Pt.add(Ki)),s=Math.max(s,n.distanceToSquared(Pt))}this.boundingSphere.radius=Math.sqrt(s),isNaN(this.boundingSphere.radius)&&We('BufferGeometry.computeBoundingSphere(): Computed radius is NaN. The "position" attribute is likely to have NaN values.',this)}}computeTangents(){let e=this.index,t=this.attributes;if(e===null||t.position===void 0||t.normal===void 0||t.uv===void 0){We("BufferGeometry: .computeTangents() failed. Missing required attributes (index, position, normal or uv)");return}let n=t.position,s=t.normal,r=t.uv,a=this.getAttribute("tangent");(a===void 0||a.count!==n.count)&&(a=new Ht(new Float32Array(4*n.count),4),this.setAttribute("tangent",a));let o=[],c=[];for(let x=0;x<n.count;x++)o[x]=new L,c[x]=new L;let l=new L,h=new L,d=new L,u=new he,p=new he,g=new he,v=new L,m=new L;function f(x,E,C){l.fromBufferAttribute(n,x),h.fromBufferAttribute(n,E),d.fromBufferAttribute(n,C),u.fromBufferAttribute(r,x),p.fromBufferAttribute(r,E),g.fromBufferAttribute(r,C),h.sub(l),d.sub(l),p.sub(u),g.sub(u);let U=1/(p.x*g.y-g.x*p.y);isFinite(U)&&(v.copy(h).multiplyScalar(g.y).addScaledVector(d,-p.y).multiplyScalar(U),m.copy(d).multiplyScalar(p.x).addScaledVector(h,-g.x).multiplyScalar(U),o[x].add(v),o[E].add(v),o[C].add(v),c[x].add(m),c[E].add(m),c[C].add(m))}let S=this.groups;S.length===0&&(S=[{start:0,count:e.count}]);for(let x=0,E=S.length;x<E;++x){let C=S[x],U=C.start,B=C.count;for(let H=U,N=U+B;H<N;H+=3)f(e.getX(H+0),e.getX(H+1),e.getX(H+2))}let R=new L,M=new L,T=new L,w=new L;function P(x){T.fromBufferAttribute(s,x),w.copy(T);let E=o[x];R.copy(E),R.sub(T.multiplyScalar(T.dot(E))).normalize(),M.crossVectors(w,E);let U=M.dot(c[x])<0?-1:1;a.setXYZW(x,R.x,R.y,R.z,U)}for(let x=0,E=S.length;x<E;++x){let C=S[x],U=C.start,B=C.count;for(let H=U,N=U+B;H<N;H+=3)P(e.getX(H+0)),P(e.getX(H+1)),P(e.getX(H+2))}this._transformed=!0}computeVertexNormals(){let e=this.index,t=this.getAttribute("position");if(t!==void 0){let n=this.getAttribute("normal");if(n===void 0||n.count!==t.count)n=new Ht(new Float32Array(t.count*3),3),this.setAttribute("normal",n);else for(let u=0,p=n.count;u<p;u++)n.setXYZ(u,0,0,0);let s=new L,r=new L,a=new L,o=new L,c=new L,l=new L,h=new L,d=new L;if(e)for(let u=0,p=e.count;u<p;u+=3){let g=e.getX(u+0),v=e.getX(u+1),m=e.getX(u+2);s.fromBufferAttribute(t,g),r.fromBufferAttribute(t,v),a.fromBufferAttribute(t,m),h.subVectors(a,r),d.subVectors(s,r),h.cross(d),o.fromBufferAttribute(n,g),c.fromBufferAttribute(n,v),l.fromBufferAttribute(n,m),o.add(h),c.add(h),l.add(h),n.setXYZ(g,o.x,o.y,o.z),n.setXYZ(v,c.x,c.y,c.z),n.setXYZ(m,l.x,l.y,l.z)}else for(let u=0,p=t.count;u<p;u+=3)s.fromBufferAttribute(t,u+0),r.fromBufferAttribute(t,u+1),a.fromBufferAttribute(t,u+2),h.subVectors(a,r),d.subVectors(s,r),h.cross(d),n.setXYZ(u+0,h.x,h.y,h.z),n.setXYZ(u+1,h.x,h.y,h.z),n.setXYZ(u+2,h.x,h.y,h.z);this.normalizeNormals(),n.needsUpdate=!0}}normalizeNormals(){let e=this.attributes.normal;for(let t=0,n=e.count;t<n;t++)Pt.fromBufferAttribute(e,t),Pt.normalize(),e.setXYZ(t,Pt.x,Pt.y,Pt.z)}toNonIndexed(){function e(o,c){let l=o.array,h=o.itemSize,d=o.normalized,u=new l.constructor(c.length*h),p=0,g=0;for(let v=0,m=c.length;v<m;v++){o.isInterleavedBufferAttribute?p=c[v]*o.data.stride+o.offset:p=c[v]*h;for(let f=0;f<h;f++)u[g++]=l[p++]}return new Ht(u,h,d)}if(this.index===null)return ke("BufferGeometry.toNonIndexed(): BufferGeometry is already non-indexed."),this;let t=new i,n=this.index.array,s=this.attributes;for(let o in s){let c=s[o],l=e(c,n);t.setAttribute(o,l)}let r=this.morphAttributes;for(let o in r){let c=[],l=r[o];for(let h=0,d=l.length;h<d;h++){let u=l[h],p=e(u,n);c.push(p)}t.morphAttributes[o]=c}t.morphTargetsRelative=this.morphTargetsRelative;let a=this.groups;for(let o=0,c=a.length;o<c;o++){let l=a[o];t.addGroup(l.start,l.count,l.materialIndex)}return t}toJSON(){let e={metadata:{version:4.7,type:"BufferGeometry",generator:"BufferGeometry.toJSON"}};if(e.uuid=this.uuid,e.type=this.parameters!==void 0&&this._transformed===!0?"BufferGeometry":this.type,e.name=this.name,Object.keys(this.userData).length>0&&(e.userData=this.userData),this.parameters!==void 0&&this._transformed!==!0){let c=this.parameters;for(let l in c)c[l]!==void 0&&(e[l]=c[l]);return e}e.data={attributes:{}};let t=this.index;t!==null&&(e.data.index={type:t.array.constructor.name,array:Array.prototype.slice.call(t.array)});let n=this.attributes;for(let c in n){let l=n[c];e.data.attributes[c]=l.toJSON(e.data)}let s={},r=!1;for(let c in this.morphAttributes){let l=this.morphAttributes[c],h=[];for(let d=0,u=l.length;d<u;d++){let p=l[d];h.push(p.toJSON(e.data))}h.length>0&&(s[c]=h,r=!0)}r&&(e.data.morphAttributes=s,e.data.morphTargetsRelative=this.morphTargetsRelative);let a=this.groups;a.length>0&&(e.data.groups=JSON.parse(JSON.stringify(a)));let o=this.boundingSphere;return o!==null&&(e.data.boundingSphere=o.toJSON()),e}clone(){return new this.constructor().copy(this)}copy(e){this.index=null,this.attributes={},this.morphAttributes={},this.groups=[],this.boundingBox=null,this.boundingSphere=null;let t={};this.name=e.name;let n=e.index;n!==null&&this.setIndex(n.clone());let s=e.attributes;for(let l in s){let h=s[l];this.setAttribute(l,h.clone(t))}let r=e.morphAttributes;for(let l in r){let h=[],d=r[l];for(let u=0,p=d.length;u<p;u++)h.push(d[u].clone(t));this.morphAttributes[l]=h}this.morphTargetsRelative=e.morphTargetsRelative;let a=e.groups;for(let l=0,h=a.length;l<h;l++){let d=a[l];this.addGroup(d.start,d.count,d.materialIndex)}let o=e.boundingBox;o!==null&&(this.boundingBox=o.clone());let c=e.boundingSphere;return c!==null&&(this.boundingSphere=c.clone()),this.drawRange.start=e.drawRange.start,this.drawRange.count=e.drawRange.count,this.userData=e.userData,this._transformed=e._transformed,this}dispose(){this.dispatchEvent({type:"dispose"})}},Va=class{constructor(e,t){this.isInterleavedBuffer=!0,this.array=e,this.stride=t,this.count=e!==void 0?e.length/t:0,this.usage=gc,this.updateRanges=[],this.version=0,this.uuid=Rn()}onUploadCallback(){}set needsUpdate(e){e===!0&&this.version++}setUsage(e){return this.usage=e,this}addUpdateRange(e,t){this.updateRanges.push({start:e,count:t})}clearUpdateRanges(){this.updateRanges.length=0}copy(e){return this.array=new e.array.constructor(e.array),this.count=e.count,this.stride=e.stride,this.usage=e.usage,this}copyAt(e,t,n){e*=this.stride,n*=t.stride;for(let s=0,r=this.stride;s<r;s++)this.array[e+s]=t.array[n+s];return this}set(e,t=0){return this.array.set(e,t),this}clone(e){e.arrayBuffers===void 0&&(e.arrayBuffers={}),this.array.buffer._uuid===void 0&&(this.array.buffer._uuid=Rn()),e.arrayBuffers[this.array.buffer._uuid]===void 0&&(e.arrayBuffers[this.array.buffer._uuid]=this.array.slice(0).buffer);let t=new this.array.constructor(e.arrayBuffers[this.array.buffer._uuid]),n=new this.constructor(t,this.stride);return n.setUsage(this.usage),n}onUpload(e){return this.onUploadCallback=e,this}toJSON(e){e.arrayBuffers===void 0&&(e.arrayBuffers={}),this.array.buffer._uuid===void 0&&(this.array.buffer._uuid=Rn()),e.arrayBuffers[this.array.buffer._uuid]===void 0&&(e.arrayBuffers[this.array.buffer._uuid]=Array.from(new Uint32Array(this.array.buffer)));let t={uuid:this.uuid,buffer:this.array.buffer._uuid,type:this.array.constructor.name,stride:this.stride};return t.usage=this.usage,t}},Wt=new L,or=class i{constructor(e,t,n,s=!1){this.isInterleavedBufferAttribute=!0,this.name="",this.data=e,this.itemSize=t,this.offset=n,this.normalized=s}get count(){return this.data.count}get array(){return this.data.array}set needsUpdate(e){this.data.needsUpdate=e}applyMatrix4(e){for(let t=0,n=this.data.count;t<n;t++)Wt.fromBufferAttribute(this,t),Wt.applyMatrix4(e),this.setXYZ(t,Wt.x,Wt.y,Wt.z);return this}applyNormalMatrix(e){for(let t=0,n=this.count;t<n;t++)Wt.fromBufferAttribute(this,t),Wt.applyNormalMatrix(e),this.setXYZ(t,Wt.x,Wt.y,Wt.z);return this}transformDirection(e){for(let t=0,n=this.count;t<n;t++)Wt.fromBufferAttribute(this,t),Wt.transformDirection(e),this.setXYZ(t,Wt.x,Wt.y,Wt.z);return this}getComponent(e,t){let n=this.array[e*this.data.stride+this.offset+t];return this.normalized&&(n=mn(n,this.array)),n}setComponent(e,t,n){return this.normalized&&(n=pt(n,this.array)),this.data.array[e*this.data.stride+this.offset+t]=n,this}setX(e,t){return this.normalized&&(t=pt(t,this.array)),this.data.array[e*this.data.stride+this.offset]=t,this}setY(e,t){return this.normalized&&(t=pt(t,this.array)),this.data.array[e*this.data.stride+this.offset+1]=t,this}setZ(e,t){return this.normalized&&(t=pt(t,this.array)),this.data.array[e*this.data.stride+this.offset+2]=t,this}setW(e,t){return this.normalized&&(t=pt(t,this.array)),this.data.array[e*this.data.stride+this.offset+3]=t,this}getX(e){let t=this.data.array[e*this.data.stride+this.offset];return this.normalized&&(t=mn(t,this.array)),t}getY(e){let t=this.data.array[e*this.data.stride+this.offset+1];return this.normalized&&(t=mn(t,this.array)),t}getZ(e){let t=this.data.array[e*this.data.stride+this.offset+2];return this.normalized&&(t=mn(t,this.array)),t}getW(e){let t=this.data.array[e*this.data.stride+this.offset+3];return this.normalized&&(t=mn(t,this.array)),t}setXY(e,t,n){return e=e*this.data.stride+this.offset,this.normalized&&(t=pt(t,this.array),n=pt(n,this.array)),this.data.array[e+0]=t,this.data.array[e+1]=n,this}setXYZ(e,t,n,s){return e=e*this.data.stride+this.offset,this.normalized&&(t=pt(t,this.array),n=pt(n,this.array),s=pt(s,this.array)),this.data.array[e+0]=t,this.data.array[e+1]=n,this.data.array[e+2]=s,this}setXYZW(e,t,n,s,r){return e=e*this.data.stride+this.offset,this.normalized&&(t=pt(t,this.array),n=pt(n,this.array),s=pt(s,this.array),r=pt(r,this.array)),this.data.array[e+0]=t,this.data.array[e+1]=n,this.data.array[e+2]=s,this.data.array[e+3]=r,this}clone(e){if(e===void 0){ir("InterleavedBufferAttribute.clone(): Cloning an interleaved buffer attribute will de-interleave buffer data.");let t=[];for(let n=0;n<this.count;n++){let s=n*this.data.stride+this.offset;for(let r=0;r<this.itemSize;r++)t.push(this.data.array[s+r])}return new Ht(new this.array.constructor(t),this.itemSize,this.normalized)}else return e.interleavedBuffers===void 0&&(e.interleavedBuffers={}),e.interleavedBuffers[this.data.uuid]===void 0&&(e.interleavedBuffers[this.data.uuid]=this.data.clone(e)),new i(e.interleavedBuffers[this.data.uuid],this.itemSize,this.offset,this.normalized)}toJSON(e){if(e===void 0){ir("InterleavedBufferAttribute.toJSON(): Serializing an interleaved buffer attribute will de-interleave buffer data.");let t=[];for(let n=0;n<this.count;n++){let s=n*this.data.stride+this.offset;for(let r=0;r<this.itemSize;r++)t.push(this.data.array[s+r])}return{itemSize:this.itemSize,type:this.array.constructor.name,array:t,normalized:this.normalized}}else return e.interleavedBuffers===void 0&&(e.interleavedBuffers={}),e.interleavedBuffers[this.data.uuid]===void 0&&(e.interleavedBuffers[this.data.uuid]=this.data.toJSON(e)),{isInterleavedBufferAttribute:!0,itemSize:this.itemSize,data:this.data.uuid,offset:this.offset,normalized:this.normalized}}},Al=new L,Fd=new L,Od=new Je,pn=class{constructor(e=new L(1,0,0),t=0){this.isPlane=!0,this.normal=e,this.constant=t}set(e,t){return this.normal.copy(e),this.constant=t,this}setComponents(e,t,n,s){return this.normal.set(e,t,n),this.constant=s,this}setFromNormalAndCoplanarPoint(e,t){return this.normal.copy(e),this.constant=-t.dot(this.normal),this}setFromCoplanarPoints(e,t,n){let s=Al.subVectors(n,t).cross(Fd.subVectors(e,t)).normalize();return this.setFromNormalAndCoplanarPoint(s,e),this}copy(e){return this.normal.copy(e.normal),this.constant=e.constant,this}normalize(){let e=1/this.normal.length();return this.normal.multiplyScalar(e),this.constant*=e,this}negate(){return this.constant*=-1,this.normal.negate(),this}distanceToPoint(e){return this.normal.dot(e)+this.constant}distanceToSphere(e){return this.distanceToPoint(e.center)-e.radius}projectPoint(e,t){return t.copy(e).addScaledVector(this.normal,-this.distanceToPoint(e))}intersectLine(e,t,n=!0){let s=e.delta(Al),r=this.normal.dot(s);if(r===0)return this.distanceToPoint(e.start)===0?t.copy(e.start):null;let a=-(e.start.dot(this.normal)+this.constant)/r;return n===!0&&(a<0||a>1)?null:t.copy(e.start).addScaledVector(s,a)}intersectsLine(e){let t=this.distanceToPoint(e.start),n=this.distanceToPoint(e.end);return t<0&&n>0||n<0&&t>0}intersectsBox(e){return e.intersectsPlane(this)}intersectsSphere(e){return e.intersectsPlane(this)}coplanarPoint(e){return e.copy(this.normal).multiplyScalar(-this.constant)}applyMatrix4(e,t){let n=t||Od.getNormalMatrix(e),s=this.coplanarPoint(Al).applyMatrix4(e),r=this.normal.applyMatrix3(n).normalize();return this.constant=-s.dot(r),this}translate(e){return this.constant-=e.dot(this.normal),this}equals(e){return e.normal.equals(this.normal)&&e.constant===this.constant}clone(){return new this.constructor().copy(this)}toJSON(){return{normal:this.normal.toArray(),constant:this.constant}}fromJSON(e){return this.normal.fromArray(e.normal),this.constant=e.constant,this}},Bd=0,xn=class extends In{constructor(){super(),this.isMaterial=!0,Object.defineProperty(this,"id",{value:Bd++}),this.uuid=Rn(),this.name="",this.type="Material",this.blending=Cs,this.side=fi,this.vertexColors=!1,this.opacity=1,this.transparent=!1,this.alphaHash=!1,this.blendSrc=ec,this.blendDst=tc,this.blendEquation=Ai,this.blendSrcAlpha=null,this.blendDstAlpha=null,this.blendEquationAlpha=null,this.blendColor=new Fe(0,0,0),this.blendAlpha=0,this.depthFunc=ls,this.depthTest=!0,this.depthWrite=!0,this.stencilWriteMask=255,this.stencilFunc=au,this.stencilRef=0,this.stencilFuncMask=255,this.stencilFail=wa,this.stencilZFail=wa,this.stencilZPass=wa,this.stencilWrite=!1,this.clippingPlanes=null,this.clipIntersection=!1,this.clipShadows=!1,this.shadowSide=null,this.colorWrite=!0,this.precision=null,this.polygonOffset=!1,this.polygonOffsetFactor=0,this.polygonOffsetUnits=0,this.dithering=!1,this.alphaToCoverage=!1,this.premultipliedAlpha=!1,this.forceSinglePass=!1,this.allowOverride=!0,this.visible=!0,this.toneMapped=!0,this.userData={},this.version=0,this._alphaTest=0}get alphaTest(){return this._alphaTest}set alphaTest(e){this._alphaTest>0!=e>0&&this.version++,this._alphaTest=e}onBeforeRender(){}onBeforeCompile(){}customProgramCacheKey(){return this.onBeforeCompile.toString()}setValues(e){if(e!==void 0)for(let t in e){let n=e[t];if(n===void 0){ke(`Material: parameter '${t}' has value of undefined.`);continue}let s=this[t];if(s===void 0){ke(`Material: '${t}' is not a property of THREE.${this.type}.`);continue}s&&s.isColor?s.set(n):s&&s.isVector2&&n&&n.isVector2||s&&s.isEuler&&n&&n.isEuler||s&&s.isVector3&&n&&n.isVector3?s.copy(n):this[t]=n}}toJSON(e){let t=e===void 0||typeof e=="string";t&&(e={textures:{},images:{}});let n={metadata:{version:4.7,type:"Material",generator:"Material.toJSON"}};n.uuid=this.uuid,n.type=this.type,n.blending=this.blending,n.side=this.side,n.shadowSide=this.shadowSide,n.vertexColors=this.vertexColors,n.opacity=this.opacity,n.transparent=this.transparent,n.blendSrc=this.blendSrc,n.blendDst=this.blendDst,n.blendEquation=this.blendEquation,n.blendSrcAlpha=this.blendSrcAlpha,n.blendDstAlpha=this.blendDstAlpha,n.blendEquationAlpha=this.blendEquationAlpha,n.blendColor=this.blendColor.getHex(),n.blendAlpha=this.blendAlpha,n.depthFunc=this.depthFunc,n.depthTest=this.depthTest,n.depthWrite=this.depthWrite,n.colorWrite=this.colorWrite,n.clipIntersection=this.clipIntersection,n.clipShadows=this.clipShadows,n.stencilWriteMask=this.stencilWriteMask,n.stencilFunc=this.stencilFunc,n.stencilRef=this.stencilRef,n.stencilFuncMask=this.stencilFuncMask,n.stencilFail=this.stencilFail,n.stencilZFail=this.stencilZFail,n.stencilZPass=this.stencilZPass,n.stencilWrite=this.stencilWrite,n.polygonOffset=this.polygonOffset,n.polygonOffsetFactor=this.polygonOffsetFactor,n.polygonOffsetUnits=this.polygonOffsetUnits,n.dithering=this.dithering,n.alphaTest=this.alphaTest,n.alphaHash=this.alphaHash,n.alphaToCoverage=this.alphaToCoverage,n.premultipliedAlpha=this.premultipliedAlpha,n.forceSinglePass=this.forceSinglePass,n.allowOverride=this.allowOverride,n.visible=this.visible,n.toneMapped=this.toneMapped,n.name=this.name,this.color&&this.color.isColor&&(n.color=this.color.getHex()),this.roughness!==void 0&&(n.roughness=this.roughness),this.metalness!==void 0&&(n.metalness=this.metalness),this.sheen!==void 0&&(n.sheen=this.sheen),this.sheenColor&&this.sheenColor.isColor&&(n.sheenColor=this.sheenColor.getHex()),this.sheenRoughness!==void 0&&(n.sheenRoughness=this.sheenRoughness),this.emissive&&this.emissive.isColor&&(n.emissive=this.emissive.getHex()),this.emissiveIntensity!==void 0&&(n.emissiveIntensity=this.emissiveIntensity),this.specular&&this.specular.isColor&&(n.specular=this.specular.getHex()),this.specularIntensity!==void 0&&(n.specularIntensity=this.specularIntensity),this.specularColor&&this.specularColor.isColor&&(n.specularColor=this.specularColor.getHex()),this.shininess!==void 0&&(n.shininess=this.shininess),this.clearcoat!==void 0&&(n.clearcoat=this.clearcoat),this.clearcoatRoughness!==void 0&&(n.clearcoatRoughness=this.clearcoatRoughness),this.clearcoatMap&&this.clearcoatMap.isTexture&&(n.clearcoatMap=this.clearcoatMap.toJSON(e).uuid),this.clearcoatRoughnessMap&&this.clearcoatRoughnessMap.isTexture&&(n.clearcoatRoughnessMap=this.clearcoatRoughnessMap.toJSON(e).uuid),this.clearcoatNormalMap&&this.clearcoatNormalMap.isTexture&&(n.clearcoatNormalMap=this.clearcoatNormalMap.toJSON(e).uuid,n.clearcoatNormalScale=this.clearcoatNormalScale.toArray()),this.sheenColorMap&&this.sheenColorMap.isTexture&&(n.sheenColorMap=this.sheenColorMap.toJSON(e).uuid),this.sheenRoughnessMap&&this.sheenRoughnessMap.isTexture&&(n.sheenRoughnessMap=this.sheenRoughnessMap.toJSON(e).uuid),this.dispersion!==void 0&&(n.dispersion=this.dispersion),this.retroreflectivity!==void 0&&(n.retroreflectivity=this.retroreflectivity),this.iridescence!==void 0&&(n.iridescence=this.iridescence),this.iridescenceIOR!==void 0&&(n.iridescenceIOR=this.iridescenceIOR),this.iridescenceThicknessRange!==void 0&&(n.iridescenceThicknessRange=this.iridescenceThicknessRange),this.iridescenceMap&&this.iridescenceMap.isTexture&&(n.iridescenceMap=this.iridescenceMap.toJSON(e).uuid),this.iridescenceThicknessMap&&this.iridescenceThicknessMap.isTexture&&(n.iridescenceThicknessMap=this.iridescenceThicknessMap.toJSON(e).uuid),this.anisotropy!==void 0&&(n.anisotropy=this.anisotropy),this.anisotropyRotation!==void 0&&(n.anisotropyRotation=this.anisotropyRotation),this.anisotropyMap&&this.anisotropyMap.isTexture&&(n.anisotropyMap=this.anisotropyMap.toJSON(e).uuid),this.map&&this.map.isTexture&&(n.map=this.map.toJSON(e).uuid),this.matcap&&this.matcap.isTexture&&(n.matcap=this.matcap.toJSON(e).uuid),this.alphaMap&&this.alphaMap.isTexture&&(n.alphaMap=this.alphaMap.toJSON(e).uuid),this.lightMap&&this.lightMap.isTexture&&(n.lightMap=this.lightMap.toJSON(e).uuid,n.lightMapIntensity=this.lightMapIntensity),this.aoMap&&this.aoMap.isTexture&&(n.aoMap=this.aoMap.toJSON(e).uuid,n.aoMapIntensity=this.aoMapIntensity),this.bumpMap&&this.bumpMap.isTexture&&(n.bumpMap=this.bumpMap.toJSON(e).uuid,n.bumpScale=this.bumpScale),this.normalMap&&this.normalMap.isTexture&&(n.normalMap=this.normalMap.toJSON(e).uuid,n.normalMapType=this.normalMapType,n.normalScale=this.normalScale.toArray()),this.displacementMap&&this.displacementMap.isTexture&&(n.displacementMap=this.displacementMap.toJSON(e).uuid,n.displacementScale=this.displacementScale,n.displacementBias=this.displacementBias),this.roughnessMap&&this.roughnessMap.isTexture&&(n.roughnessMap=this.roughnessMap.toJSON(e).uuid),this.metalnessMap&&this.metalnessMap.isTexture&&(n.metalnessMap=this.metalnessMap.toJSON(e).uuid),this.emissiveMap&&this.emissiveMap.isTexture&&(n.emissiveMap=this.emissiveMap.toJSON(e).uuid),this.specularMap&&this.specularMap.isTexture&&(n.specularMap=this.specularMap.toJSON(e).uuid),this.specularIntensityMap&&this.specularIntensityMap.isTexture&&(n.specularIntensityMap=this.specularIntensityMap.toJSON(e).uuid),this.specularColorMap&&this.specularColorMap.isTexture&&(n.specularColorMap=this.specularColorMap.toJSON(e).uuid),this.envMap&&this.envMap.isTexture&&(n.envMap=this.envMap.toJSON(e).uuid,this.combine!==void 0&&(n.combine=this.combine)),this.envMapRotation!==void 0&&(n.envMapRotation=this.envMapRotation.toArray()),this.envMapIntensity!==void 0&&(n.envMapIntensity=this.envMapIntensity),this.reflectivity!==void 0&&(n.reflectivity=this.reflectivity),this.refractionRatio!==void 0&&(n.refractionRatio=this.refractionRatio),this.gradientMap&&this.gradientMap.isTexture&&(n.gradientMap=this.gradientMap.toJSON(e).uuid),this.transmission!==void 0&&(n.transmission=this.transmission),this.transmissionMap&&this.transmissionMap.isTexture&&(n.transmissionMap=this.transmissionMap.toJSON(e).uuid),this.thickness!==void 0&&(n.thickness=this.thickness),this.thicknessMap&&this.thicknessMap.isTexture&&(n.thicknessMap=this.thicknessMap.toJSON(e).uuid),this.attenuationDistance!==void 0&&(n.attenuationDistance=this.attenuationDistance),this.attenuationColor!==void 0&&(n.attenuationColor=this.attenuationColor.getHex()),this.size!==void 0&&(n.size=this.size),this.sizeAttenuation!==void 0&&(n.sizeAttenuation=this.sizeAttenuation),Array.isArray(this.clippingPlanes)&&this.clippingPlanes.length>0&&(n.clippingPlanes=this.clippingPlanes.map(r=>r.toJSON())),this.rotation!==void 0&&(n.rotation=this.rotation),this.depthPacking!==void 0&&(n.depthPacking=this.depthPacking),this.linewidth!==void 0&&(n.linewidth=this.linewidth),this.linecap!==void 0&&(n.linecap=this.linecap),this.linejoin!==void 0&&(n.linejoin=this.linejoin),this.dashSize!==void 0&&(n.dashSize=this.dashSize),this.gapSize!==void 0&&(n.gapSize=this.gapSize),this.scale!==void 0&&(n.scale=this.scale),this.wireframe!==void 0&&(n.wireframe=this.wireframe),this.wireframeLinewidth!==void 0&&(n.wireframeLinewidth=this.wireframeLinewidth),this.wireframeLinecap!==void 0&&(n.wireframeLinecap=this.wireframeLinecap),this.wireframeLinejoin!==void 0&&(n.wireframeLinejoin=this.wireframeLinejoin),this.flatShading!==void 0&&(n.flatShading=this.flatShading),this.fog!==void 0&&(n.fog=this.fog),Object.keys(this.userData).length>0&&(n.userData=this.userData);function s(r){let a=[];for(let o in r){let c=r[o];delete c.metadata,a.push(c)}return a}if(t){let r=s(e.textures),a=s(e.images);r.length>0&&(n.textures=r),a.length>0&&(n.images=a)}return n}fromJSON(e,t){if(e.uuid!==void 0&&(this.uuid=e.uuid),e.name!==void 0&&(this.name=e.name),e.color!==void 0&&this.color!==void 0&&this.color.setHex(e.color),e.roughness!==void 0&&(this.roughness=e.roughness),e.metalness!==void 0&&(this.metalness=e.metalness),e.sheen!==void 0&&(this.sheen=e.sheen),e.sheenColor!==void 0&&(this.sheenColor=new Fe().setHex(e.sheenColor)),e.sheenRoughness!==void 0&&(this.sheenRoughness=e.sheenRoughness),e.emissive!==void 0&&this.emissive!==void 0&&this.emissive.setHex(e.emissive),e.specular!==void 0&&this.specular!==void 0&&this.specular.setHex(e.specular),e.specularIntensity!==void 0&&(this.specularIntensity=e.specularIntensity),e.specularColor!==void 0&&this.specularColor!==void 0&&this.specularColor.setHex(e.specularColor),e.shininess!==void 0&&(this.shininess=e.shininess),e.clearcoat!==void 0&&(this.clearcoat=e.clearcoat),e.clearcoatRoughness!==void 0&&(this.clearcoatRoughness=e.clearcoatRoughness),e.dispersion!==void 0&&(this.dispersion=e.dispersion),e.retroreflectivity!==void 0&&(this.retroreflectivity=e.retroreflectivity),e.iridescence!==void 0&&(this.iridescence=e.iridescence),e.iridescenceIOR!==void 0&&(this.iridescenceIOR=e.iridescenceIOR),e.iridescenceThicknessRange!==void 0&&(this.iridescenceThicknessRange=e.iridescenceThicknessRange),e.transmission!==void 0&&(this.transmission=e.transmission),e.thickness!==void 0&&(this.thickness=e.thickness),e.attenuationDistance!==void 0&&(this.attenuationDistance=e.attenuationDistance),e.attenuationColor!==void 0&&this.attenuationColor!==void 0&&this.attenuationColor.setHex(e.attenuationColor),e.anisotropy!==void 0&&(this.anisotropy=e.anisotropy),e.anisotropyRotation!==void 0&&(this.anisotropyRotation=e.anisotropyRotation),e.fog!==void 0&&(this.fog=e.fog),e.flatShading!==void 0&&(this.flatShading=e.flatShading),e.blending!==void 0&&(this.blending=e.blending),e.combine!==void 0&&(this.combine=e.combine),e.side!==void 0&&(this.side=e.side),e.shadowSide!==void 0&&(this.shadowSide=e.shadowSide),e.opacity!==void 0&&(this.opacity=e.opacity),e.transparent!==void 0&&(this.transparent=e.transparent),e.alphaTest!==void 0&&(this.alphaTest=e.alphaTest),e.alphaHash!==void 0&&(this.alphaHash=e.alphaHash),e.depthFunc!==void 0&&(this.depthFunc=e.depthFunc),e.depthTest!==void 0&&(this.depthTest=e.depthTest),e.depthWrite!==void 0&&(this.depthWrite=e.depthWrite),e.colorWrite!==void 0&&(this.colorWrite=e.colorWrite),e.clippingPlanes!==void 0&&(this.clippingPlanes=e.clippingPlanes.map(n=>new pn().fromJSON(n))),e.clipIntersection!==void 0&&(this.clipIntersection=e.clipIntersection),e.clipShadows!==void 0&&(this.clipShadows=e.clipShadows),e.depthPacking!==void 0&&(this.depthPacking=e.depthPacking),e.blendSrc!==void 0&&(this.blendSrc=e.blendSrc),e.blendDst!==void 0&&(this.blendDst=e.blendDst),e.blendEquation!==void 0&&(this.blendEquation=e.blendEquation),e.blendSrcAlpha!==void 0&&(this.blendSrcAlpha=e.blendSrcAlpha),e.blendDstAlpha!==void 0&&(this.blendDstAlpha=e.blendDstAlpha),e.blendEquationAlpha!==void 0&&(this.blendEquationAlpha=e.blendEquationAlpha),e.blendColor!==void 0&&this.blendColor!==void 0&&this.blendColor.setHex(e.blendColor),e.blendAlpha!==void 0&&(this.blendAlpha=e.blendAlpha),e.stencilWriteMask!==void 0&&(this.stencilWriteMask=e.stencilWriteMask),e.stencilFunc!==void 0&&(this.stencilFunc=e.stencilFunc),e.stencilRef!==void 0&&(this.stencilRef=e.stencilRef),e.stencilFuncMask!==void 0&&(this.stencilFuncMask=e.stencilFuncMask),e.stencilFail!==void 0&&(this.stencilFail=e.stencilFail),e.stencilZFail!==void 0&&(this.stencilZFail=e.stencilZFail),e.stencilZPass!==void 0&&(this.stencilZPass=e.stencilZPass),e.stencilWrite!==void 0&&(this.stencilWrite=e.stencilWrite),e.wireframe!==void 0&&(this.wireframe=e.wireframe),e.wireframeLinewidth!==void 0&&(this.wireframeLinewidth=e.wireframeLinewidth),e.wireframeLinecap!==void 0&&(this.wireframeLinecap=e.wireframeLinecap),e.wireframeLinejoin!==void 0&&(this.wireframeLinejoin=e.wireframeLinejoin),e.rotation!==void 0&&(this.rotation=e.rotation),e.linewidth!==void 0&&(this.linewidth=e.linewidth),e.linecap!==void 0&&(this.linecap=e.linecap),e.linejoin!==void 0&&(this.linejoin=e.linejoin),e.dashSize!==void 0&&(this.dashSize=e.dashSize),e.gapSize!==void 0&&(this.gapSize=e.gapSize),e.scale!==void 0&&(this.scale=e.scale),e.polygonOffset!==void 0&&(this.polygonOffset=e.polygonOffset),e.polygonOffsetFactor!==void 0&&(this.polygonOffsetFactor=e.polygonOffsetFactor),e.polygonOffsetUnits!==void 0&&(this.polygonOffsetUnits=e.polygonOffsetUnits),e.dithering!==void 0&&(this.dithering=e.dithering),e.alphaToCoverage!==void 0&&(this.alphaToCoverage=e.alphaToCoverage),e.premultipliedAlpha!==void 0&&(this.premultipliedAlpha=e.premultipliedAlpha),e.forceSinglePass!==void 0&&(this.forceSinglePass=e.forceSinglePass),e.allowOverride!==void 0&&(this.allowOverride=e.allowOverride),e.visible!==void 0&&(this.visible=e.visible),e.toneMapped!==void 0&&(this.toneMapped=e.toneMapped),e.userData!==void 0&&(this.userData=e.userData),e.vertexColors!==void 0&&(typeof e.vertexColors=="number"?this.vertexColors=e.vertexColors>0:this.vertexColors=e.vertexColors),e.size!==void 0&&(this.size=e.size),e.sizeAttenuation!==void 0&&(this.sizeAttenuation=e.sizeAttenuation),e.map!==void 0&&(this.map=t[e.map]||null),e.matcap!==void 0&&(this.matcap=t[e.matcap]||null),e.alphaMap!==void 0&&(this.alphaMap=t[e.alphaMap]||null),e.bumpMap!==void 0&&(this.bumpMap=t[e.bumpMap]||null),e.bumpScale!==void 0&&(this.bumpScale=e.bumpScale),e.normalMap!==void 0&&(this.normalMap=t[e.normalMap]||null),e.normalMapType!==void 0&&(this.normalMapType=e.normalMapType),e.normalScale!==void 0){let n=e.normalScale;Array.isArray(n)===!1&&(n=[n,n]),this.normalScale=new he().fromArray(n)}return e.displacementMap!==void 0&&(this.displacementMap=t[e.displacementMap]||null),e.displacementScale!==void 0&&(this.displacementScale=e.displacementScale),e.displacementBias!==void 0&&(this.displacementBias=e.displacementBias),e.roughnessMap!==void 0&&(this.roughnessMap=t[e.roughnessMap]||null),e.metalnessMap!==void 0&&(this.metalnessMap=t[e.metalnessMap]||null),e.emissiveMap!==void 0&&(this.emissiveMap=t[e.emissiveMap]||null),e.emissiveIntensity!==void 0&&(this.emissiveIntensity=e.emissiveIntensity),e.specularMap!==void 0&&(this.specularMap=t[e.specularMap]||null),e.specularIntensityMap!==void 0&&(this.specularIntensityMap=t[e.specularIntensityMap]||null),e.specularColorMap!==void 0&&(this.specularColorMap=t[e.specularColorMap]||null),e.envMap!==void 0&&(this.envMap=t[e.envMap]||null),e.envMapRotation!==void 0&&this.envMapRotation.fromArray(e.envMapRotation),e.envMapIntensity!==void 0&&(this.envMapIntensity=e.envMapIntensity),e.reflectivity!==void 0&&(this.reflectivity=e.reflectivity),e.refractionRatio!==void 0&&(this.refractionRatio=e.refractionRatio),e.lightMap!==void 0&&(this.lightMap=t[e.lightMap]||null),e.lightMapIntensity!==void 0&&(this.lightMapIntensity=e.lightMapIntensity),e.aoMap!==void 0&&(this.aoMap=t[e.aoMap]||null),e.aoMapIntensity!==void 0&&(this.aoMapIntensity=e.aoMapIntensity),e.gradientMap!==void 0&&(this.gradientMap=t[e.gradientMap]||null),e.clearcoatMap!==void 0&&(this.clearcoatMap=t[e.clearcoatMap]||null),e.clearcoatRoughnessMap!==void 0&&(this.clearcoatRoughnessMap=t[e.clearcoatRoughnessMap]||null),e.clearcoatNormalMap!==void 0&&(this.clearcoatNormalMap=t[e.clearcoatNormalMap]||null),e.clearcoatNormalScale!==void 0&&(this.clearcoatNormalScale=new he().fromArray(e.clearcoatNormalScale)),e.iridescenceMap!==void 0&&(this.iridescenceMap=t[e.iridescenceMap]||null),e.iridescenceThicknessMap!==void 0&&(this.iridescenceThicknessMap=t[e.iridescenceThicknessMap]||null),e.transmissionMap!==void 0&&(this.transmissionMap=t[e.transmissionMap]||null),e.thicknessMap!==void 0&&(this.thicknessMap=t[e.thicknessMap]||null),e.anisotropyMap!==void 0&&(this.anisotropyMap=t[e.anisotropyMap]||null),e.sheenColorMap!==void 0&&(this.sheenColorMap=t[e.sheenColorMap]||null),e.sheenRoughnessMap!==void 0&&(this.sheenRoughnessMap=t[e.sheenRoughnessMap]||null),this}clone(){return new this.constructor().copy(this)}copy(e){this.name=e.name,this.blending=e.blending,this.side=e.side,this.vertexColors=e.vertexColors,this.opacity=e.opacity,this.transparent=e.transparent,this.blendSrc=e.blendSrc,this.blendDst=e.blendDst,this.blendEquation=e.blendEquation,this.blendSrcAlpha=e.blendSrcAlpha,this.blendDstAlpha=e.blendDstAlpha,this.blendEquationAlpha=e.blendEquationAlpha,this.blendColor.copy(e.blendColor),this.blendAlpha=e.blendAlpha,this.depthFunc=e.depthFunc,this.depthTest=e.depthTest,this.depthWrite=e.depthWrite,this.stencilWriteMask=e.stencilWriteMask,this.stencilFunc=e.stencilFunc,this.stencilRef=e.stencilRef,this.stencilFuncMask=e.stencilFuncMask,this.stencilFail=e.stencilFail,this.stencilZFail=e.stencilZFail,this.stencilZPass=e.stencilZPass,this.stencilWrite=e.stencilWrite;let t=e.clippingPlanes,n=null;if(t!==null){let s=t.length;n=new Array(s);for(let r=0;r!==s;++r)n[r]=t[r].clone()}return this.clippingPlanes=n,this.clipIntersection=e.clipIntersection,this.clipShadows=e.clipShadows,this.shadowSide=e.shadowSide,this.colorWrite=e.colorWrite,this.precision=e.precision,this.polygonOffset=e.polygonOffset,this.polygonOffsetFactor=e.polygonOffsetFactor,this.polygonOffsetUnits=e.polygonOffsetUnits,this.dithering=e.dithering,this.alphaTest=e.alphaTest,this.alphaHash=e.alphaHash,this.alphaToCoverage=e.alphaToCoverage,this.premultipliedAlpha=e.premultipliedAlpha,this.forceSinglePass=e.forceSinglePass,this.allowOverride=e.allowOverride,this.visible=e.visible,this.toneMapped=e.toneMapped,this.userData=JSON.parse(JSON.stringify(e.userData)),this}dispose(){this.dispatchEvent({type:"dispose"})}set needsUpdate(e){e===!0&&this.version++}},bi=class extends xn{constructor(e){super(),this.isSpriteMaterial=!0,this.type="SpriteMaterial",this.color=new Fe(16777215),this.map=null,this.alphaMap=null,this.rotation=0,this.sizeAttenuation=!0,this.transparent=!0,this.fog=!0,this.setValues(e)}copy(e){return super.copy(e),this.color.copy(e.color),this.map=e.map,this.alphaMap=e.alphaMap,this.rotation=e.rotation,this.sizeAttenuation=e.sizeAttenuation,this.fog=e.fog,this}},Qi,Gs=new L,ji=new L,es=new L,ts=new he,Ws=new he,xu=new ut,ia=new L,Xs=new L,sa=new L,rh=new he,Rl=new he,ah=new he,_s=class extends Tt{constructor(e=new bi){if(super(),this.isSprite=!0,this.type="Sprite",Qi===void 0){Qi=new Mt;let t=new Float32Array([-.5,-.5,0,0,0,.5,-.5,0,1,0,.5,.5,0,1,1,-.5,.5,0,0,1]),n=new Va(t,5);Qi.setIndex([0,1,2,0,2,3]),Qi.setAttribute("position",new or(n,3,0,!1)),Qi.setAttribute("uv",new or(n,2,3,!1))}this.geometry=Qi,this.material=e,this.center=new he(.5,.5),this.count=1}intersectsFrustum(e){return e.intersectsSprite(this)}raycast(e,t){e.camera===null&&We('Sprite: "Raycaster.camera" needs to be set in order to raycast against sprites.'),ji.setFromMatrixScale(this.matrixWorld),xu.copy(e.camera.matrixWorld),this.modelViewMatrix.multiplyMatrices(e.camera.matrixWorldInverse,this.matrixWorld),es.setFromMatrixPosition(this.modelViewMatrix),e.camera.isPerspectiveCamera&&this.material.sizeAttenuation===!1&&ji.multiplyScalar(-es.z);let n=this.material.rotation,s,r;n!==0&&(r=Math.cos(n),s=Math.sin(n));let a=this.center;ra(ia.set(-.5,-.5,0),es,a,ji,s,r),ra(Xs.set(.5,-.5,0),es,a,ji,s,r),ra(sa.set(.5,.5,0),es,a,ji,s,r),rh.set(0,0),Rl.set(1,0),ah.set(1,1);let o=e.ray.intersectTriangle(ia,Xs,sa,!1,Gs);if(o===null&&(ra(Xs.set(-.5,.5,0),es,a,ji,s,r),Rl.set(0,1),o=e.ray.intersectTriangle(ia,sa,Xs,!1,Gs),o===null))return;let c=e.ray.origin.distanceTo(Gs);c<e.near||c>e.far||t.push({distance:c,point:Gs.clone(),uv:Zn.getInterpolation(Gs,ia,Xs,sa,rh,Rl,ah,new he),face:null,object:this})}copy(e,t){return super.copy(e,t),e.center!==void 0&&this.center.copy(e.center),this.material=e.material,this}};function ra(i,e,t,n,s,r){ts.subVectors(i,t).addScalar(.5).multiply(n),s!==void 0?(Ws.x=r*ts.x-s*ts.y,Ws.y=s*ts.x+r*ts.y):Ws.copy(ts),i.copy(e),i.x+=Ws.x,i.y+=Ws.y,i.applyMatrix4(xu)}var Yn=new L,Cl=new L,aa=new L,oa=new L,Ei=class{constructor(e=new L,t=new L(0,0,-1)){this.origin=e,this.direction=t}set(e,t){return this.origin.copy(e),this.direction.copy(t),this}copy(e){return this.origin.copy(e.origin),this.direction.copy(e.direction),this}at(e,t){return t.copy(this.origin).addScaledVector(this.direction,e)}lookAt(e){return this.direction.copy(e).sub(this.origin).normalize(),this}recast(e){return this.origin.copy(this.at(e,Yn)),this}closestPointToPoint(e,t){t.subVectors(e,this.origin);let n=t.dot(this.direction);return n<0?t.copy(this.origin):t.copy(this.origin).addScaledVector(this.direction,n)}distanceToPoint(e){return Math.sqrt(this.distanceSqToPoint(e))}distanceSqToPoint(e){let t=Yn.subVectors(e,this.origin).dot(this.direction);return t<0?this.origin.distanceToSquared(e):(Yn.copy(this.origin).addScaledVector(this.direction,t),Yn.distanceToSquared(e))}distanceSqToSegment(e,t,n,s){Cl.copy(e).add(t).multiplyScalar(.5),aa.copy(t).sub(e).normalize(),oa.copy(this.origin).sub(Cl);let r=e.distanceTo(t)*.5,a=-this.direction.dot(aa),o=oa.dot(this.direction),c=-oa.dot(aa),l=oa.lengthSq(),h=Math.abs(1-a*a),d,u,p,g;if(h>0)if(d=a*c-o,u=a*o-c,g=r*h,d>=0)if(u>=-g)if(u<=g){let v=1/h;d*=v,u*=v,p=d*(d+a*u+2*o)+u*(a*d+u+2*c)+l}else u=r,d=Math.max(0,-(a*u+o)),p=-d*d+u*(u+2*c)+l;else u=-r,d=Math.max(0,-(a*u+o)),p=-d*d+u*(u+2*c)+l;else u<=-g?(d=Math.max(0,-(-a*r+o)),u=d>0?-r:Math.min(Math.max(-r,-c),r),p=-d*d+u*(u+2*c)+l):u<=g?(d=0,u=Math.min(Math.max(-r,-c),r),p=u*(u+2*c)+l):(d=Math.max(0,-(a*r+o)),u=d>0?r:Math.min(Math.max(-r,-c),r),p=-d*d+u*(u+2*c)+l);else u=a>0?-r:r,d=Math.max(0,-(a*u+o)),p=-d*d+u*(u+2*c)+l;return n&&n.copy(this.origin).addScaledVector(this.direction,d),s&&s.copy(Cl).addScaledVector(aa,u),p}intersectSphere(e,t){if(e.radius<0)return null;Yn.subVectors(e.center,this.origin);let n=Yn.dot(this.direction),s=Yn.dot(Yn)-n*n,r=e.radius*e.radius;if(s>r)return null;let a=Math.sqrt(r-s),o=n-a,c=n+a;return c<0?null:o<0?this.at(c,t):this.at(o,t)}intersectsSphere(e){return e.radius<0?!1:this.distanceSqToPoint(e.center)<=e.radius*e.radius}distanceToPlane(e){let t=e.normal.dot(this.direction);if(t===0)return e.distanceToPoint(this.origin)===0?0:null;let n=-(this.origin.dot(e.normal)+e.constant)/t;return n>=0?n:null}intersectPlane(e,t){let n=this.distanceToPlane(e);return n===null?null:this.at(n,t)}intersectsPlane(e){let t=e.distanceToPoint(this.origin);return t===0||e.normal.dot(this.direction)*t<0}intersectBox(e,t){let n,s,r,a,o,c,l=1/this.direction.x,h=1/this.direction.y,d=1/this.direction.z,u=this.origin;return l>=0?(n=(e.min.x-u.x)*l,s=(e.max.x-u.x)*l):(n=(e.max.x-u.x)*l,s=(e.min.x-u.x)*l),h>=0?(r=(e.min.y-u.y)*h,a=(e.max.y-u.y)*h):(r=(e.max.y-u.y)*h,a=(e.min.y-u.y)*h),n>a||r>s||((r>n||isNaN(n))&&(n=r),(a<s||isNaN(s))&&(s=a),d>=0?(o=(e.min.z-u.z)*d,c=(e.max.z-u.z)*d):(o=(e.max.z-u.z)*d,c=(e.min.z-u.z)*d),n>c||o>s)||((o>n||n!==n)&&(n=o),(c<s||s!==s)&&(s=c),s<0)?null:this.at(n>=0?n:s,t)}intersectsBox(e){return this.intersectBox(e,Yn)!==null}intersectTriangle(e,t,n,s,r){let a=this.origin,o=this.direction,c=o.x,l=o.y,h=o.z,d=e.x-a.x,u=e.y-a.y,p=e.z-a.z,g=t.x-a.x,v=t.y-a.y,m=t.z-a.z,f=n.x-a.x,S=n.y-a.y,R=n.z-a.z,M=Math.abs(c),T=Math.abs(l),w=Math.abs(h),P,x,E,C,U,B,H,N,V,X,W,re;if(M>=T&&M>=w?(E=c,B=d,V=g,re=f,c>=0?(P=l,x=h,C=u,U=p,H=v,N=m,X=S,W=R):(P=h,x=l,C=p,U=u,H=m,N=v,X=R,W=S)):T>=w?(E=l,B=u,V=v,re=S,l>=0?(P=h,x=c,C=p,U=d,H=m,N=g,X=R,W=f):(P=c,x=h,C=d,U=p,H=g,N=m,X=f,W=R)):(E=h,B=p,V=m,re=R,h>=0?(P=c,x=l,C=d,U=u,H=g,N=v,X=f,W=S):(P=l,x=c,C=u,U=d,H=v,N=g,X=S,W=f)),E===0)return null;let Y=P/E,Q=x/E,j=1/E,Re=C-Y*B,Ce=U-Q*B,lt=H-Y*V,je=N-Q*V,ot=X-Y*re,q=W-Q*re,ne=ot*je-q*lt,ve=Re*q-Ce*ot,Oe=lt*Ce-je*Re;if(s){if(ne<0||ve<0||Oe<0)return null}else if((ne<0||ve<0||Oe<0)&&(ne>0||ve>0||Oe>0))return null;let we=ne+ve+Oe;if(we===0)return null;let He=j*(ne*B+ve*V+Oe*re);return(we>0?He<0:He>0)?null:this.at(He/we,r)}applyMatrix4(e){return this.origin.applyMatrix4(e),this.direction.transformDirection(e),this}equals(e){return e.origin.equals(this.origin)&&e.direction.equals(this.direction)}clone(){return new this.constructor().copy(this)}},Xt=class extends xn{constructor(e){super(),this.isMeshBasicMaterial=!0,this.type="MeshBasicMaterial",this.color=new Fe(16777215),this.map=null,this.lightMap=null,this.lightMapIntensity=1,this.aoMap=null,this.aoMapIntensity=1,this.specularMap=null,this.alphaMap=null,this.envMap=null,this.envMapRotation=new _n,this.combine=nc,this.reflectivity=1,this.refractionRatio=.98,this.wireframe=!1,this.wireframeLinewidth=1,this.wireframeLinecap="round",this.wireframeLinejoin="round",this.fog=!0,this.setValues(e)}copy(e){return super.copy(e),this.color.copy(e.color),this.map=e.map,this.lightMap=e.lightMap,this.lightMapIntensity=e.lightMapIntensity,this.aoMap=e.aoMap,this.aoMapIntensity=e.aoMapIntensity,this.specularMap=e.specularMap,this.alphaMap=e.alphaMap,this.envMap=e.envMap,this.envMapRotation.copy(e.envMapRotation),this.combine=e.combine,this.reflectivity=e.reflectivity,this.refractionRatio=e.refractionRatio,this.wireframe=e.wireframe,this.wireframeLinewidth=e.wireframeLinewidth,this.wireframeLinecap=e.wireframeLinecap,this.wireframeLinejoin=e.wireframeLinejoin,this.fog=e.fog,this}},oh=new ut,vi=new Ei,la=new Dn,lh=new L,ca=new L,ha=new L,ua=new L,Il=new L,da=new L,ch=new L,fa=new L,Ke=class extends Tt{constructor(e=new Mt,t=new Xt){super(),this.isMesh=!0,this.type="Mesh",this.geometry=e,this.material=t,this.morphTargetDictionary=void 0,this.morphTargetInfluences=void 0,this.count=1,this.updateMorphTargets()}copy(e,t){return super.copy(e,t),e.morphTargetInfluences!==void 0&&(this.morphTargetInfluences=e.morphTargetInfluences.slice()),e.morphTargetDictionary!==void 0&&(this.morphTargetDictionary=Object.assign({},e.morphTargetDictionary)),this.material=Array.isArray(e.material)?e.material.slice():e.material,this.geometry=e.geometry,this}updateMorphTargets(){let t=this.geometry.morphAttributes,n=Object.keys(t);if(n.length>0){let s=t[n[0]];if(s!==void 0){this.morphTargetInfluences=[],this.morphTargetDictionary={};for(let r=0,a=s.length;r<a;r++){let o=s[r].name||String(r);this.morphTargetInfluences.push(0),this.morphTargetDictionary[o]=r}}}}getVertexPosition(e,t){let n=this.geometry,s=n.attributes.position,r=n.morphAttributes.position,a=n.morphTargetsRelative;t.fromBufferAttribute(s,e);let o=this.morphTargetInfluences;if(r&&o){da.set(0,0,0);for(let c=0,l=r.length;c<l;c++){let h=o[c],d=r[c];h!==0&&(Il.fromBufferAttribute(d,e),a?da.addScaledVector(Il,h):da.addScaledVector(Il.sub(t),h))}t.add(da)}return t}intersectsFrustum(e){return e.intersectsObject(this)}raycast(e,t){let n=this.geometry,s=this.material,r=this.matrixWorld;s!==void 0&&(n.boundingSphere===null&&n.computeBoundingSphere(),la.copy(n.boundingSphere),la.applyMatrix4(r),vi.copy(e.ray).recast(e.near),!(la.containsPoint(vi.origin)===!1&&(vi.intersectSphere(la,lh)===null||vi.origin.distanceToSquared(lh)>(e.far-e.near)**2))&&(oh.copy(r).invert(),vi.copy(e.ray).applyMatrix4(oh),!(n.boundingBox!==null&&vi.intersectsBox(n.boundingBox)===!1)&&this._computeIntersections(e,t,vi)))}_computeIntersections(e,t,n){let s,r=this.geometry,a=this.material,o=r.index,c=r.attributes.position,l=r.attributes.uv,h=r.attributes.uv1,d=r.attributes.normal,u=r.groups,p=r.drawRange;if(o!==null)if(Array.isArray(a))for(let g=0,v=u.length;g<v;g++){let m=u[g],f=a[m.materialIndex],S=Math.max(m.start,p.start),R=Math.min(o.count,Math.min(m.start+m.count,p.start+p.count));for(let M=S,T=R;M<T;M+=3){let w=o.getX(M),P=o.getX(M+1),x=o.getX(M+2);s=pa(this,f,e,n,l,h,d,w,P,x),s&&(s.faceIndex=Math.floor(M/3),s.face.materialIndex=m.materialIndex,t.push(s))}}else{let g=Math.max(0,p.start),v=Math.min(o.count,p.start+p.count);for(let m=g,f=v;m<f;m+=3){let S=o.getX(m),R=o.getX(m+1),M=o.getX(m+2);s=pa(this,a,e,n,l,h,d,S,R,M),s&&(s.faceIndex=Math.floor(m/3),t.push(s))}}else if(c!==void 0)if(Array.isArray(a))for(let g=0,v=u.length;g<v;g++){let m=u[g],f=a[m.materialIndex],S=Math.max(m.start,p.start),R=Math.min(c.count,Math.min(m.start+m.count,p.start+p.count));for(let M=S,T=R;M<T;M+=3){let w=M,P=M+1,x=M+2;s=pa(this,f,e,n,l,h,d,w,P,x),s&&(s.faceIndex=Math.floor(M/3),s.face.materialIndex=m.materialIndex,t.push(s))}}else{let g=Math.max(0,p.start),v=Math.min(c.count,p.start+p.count);for(let m=g,f=v;m<f;m+=3){let S=m,R=m+1,M=m+2;s=pa(this,a,e,n,l,h,d,S,R,M),s&&(s.faceIndex=Math.floor(m/3),t.push(s))}}}};function zd(i,e,t,n,s,r,a,o){let c;if(e.side===Ut?c=n.intersectTriangle(a,r,s,!0,o):c=n.intersectTriangle(s,r,a,e.side===fi,o),c===null)return null;fa.copy(o),fa.applyMatrix4(i.matrixWorld);let l=t.ray.origin.distanceTo(fa);return l<t.near||l>t.far?null:{distance:l,point:fa.clone(),object:i}}function pa(i,e,t,n,s,r,a,o,c,l){i.getVertexPosition(o,ca),i.getVertexPosition(c,ha),i.getVertexPosition(l,ua);let h=zd(i,e,t,n,ca,ha,ua,ch);if(h){let d=new L;Zn.getBarycoord(ch,ca,ha,ua,d),s&&(h.uv=Zn.getInterpolatedAttribute(s,o,c,l,d,new he)),r&&(h.uv1=Zn.getInterpolatedAttribute(r,o,c,l,d,new he)),a&&(h.normal=Zn.getInterpolatedAttribute(a,o,c,l,d,new L),h.normal.dot(n.direction)>0&&h.normal.multiplyScalar(-1));let u={a:o,b:c,c:l,normal:new L,materialIndex:0};Zn.getNormal(ca,ha,ua,u.normal),h.face=u,h.barycoord=d}return h}var lr=class extends Vt{constructor(e=null,t=1,n=1,s,r,a,o,c,l=Lt,h=Lt,d,u){super(null,a,o,c,l,h,s,r,d,u),this.isDataTexture=!0,this.image={data:e,width:t,height:n},this.generateMipmaps=!1,this.flipY=!1,this.unpackAlignment=1}};var cr=class extends Ht{constructor(e,t,n,s=1){super(e,t,n),this.isInstancedBufferAttribute=!0,this.meshPerAttribute=s}copy(e){return super.copy(e),this.meshPerAttribute=e.meshPerAttribute,this}toJSON(){let e=super.toJSON();return e.meshPerAttribute=this.meshPerAttribute,e.isInstancedBufferAttribute=!0,e}},ns=new ut,hh=new ut,ma=[],uh=new Ln,Hd=new ut,qs=new Ke,Ys=new Dn,hr=class extends Ke{constructor(e,t,n){super(e,t),this.isInstancedMesh=!0,this.instanceMatrix=new cr(new Float32Array(n*16),16),this.instanceColor=null,this.morphTexture=null,this.count=n,this.boundingBox=null,this.boundingSphere=null;for(let s=0;s<n;s++)this.setMatrixAt(s,Hd)}computeBoundingBox(){let e=this.geometry,t=this.count;this.boundingBox===null&&(this.boundingBox=new Ln),e.boundingBox===null&&e.computeBoundingBox(),this.boundingBox.makeEmpty();for(let n=0;n<t;n++)this.getMatrixAt(n,ns),uh.copy(e.boundingBox).applyMatrix4(ns),this.boundingBox.union(uh)}computeBoundingSphere(){let e=this.geometry,t=this.count;this.boundingSphere===null&&(this.boundingSphere=new Dn),e.boundingSphere===null&&e.computeBoundingSphere(),this.boundingSphere.makeEmpty();for(let n=0;n<t;n++)this.getMatrixAt(n,ns),Ys.copy(e.boundingSphere).applyMatrix4(ns),this.boundingSphere.union(Ys)}copy(e,t){return super.copy(e,t),this.instanceMatrix.copy(e.instanceMatrix),e.morphTexture!==null&&(this.morphTexture=e.morphTexture.clone()),e.instanceColor!==null&&(this.instanceColor=e.instanceColor.clone()),this.count=e.count,e.boundingBox!==null&&(this.boundingBox=e.boundingBox.clone()),e.boundingSphere!==null&&(this.boundingSphere=e.boundingSphere.clone()),this}getColorAt(e,t){return this.instanceColor===null?t.setRGB(1,1,1):t.fromArray(this.instanceColor.array,e*3)}getMatrixAt(e,t){return t.fromArray(this.instanceMatrix.array,e*16)}getMorphAt(e,t){let n=t.morphTargetInfluences,s=this.morphTexture.source.data.data,r=n.length+1,a=e*r+1;for(let o=0;o<n.length;o++)n[o]=s[a+o]}raycast(e,t){let n=this.matrixWorld,s=this.count;if(qs.geometry=this.geometry,qs.material=this.material,qs.material!==void 0&&(this.boundingSphere===null&&this.computeBoundingSphere(),Ys.copy(this.boundingSphere),Ys.applyMatrix4(n),e.ray.intersectsSphere(Ys)!==!1))for(let r=0;r<s;r++){this.getMatrixAt(r,ns),hh.multiplyMatrices(n,ns),qs.matrixWorld=hh,qs.raycast(e,ma);for(let a=0,o=ma.length;a<o;a++){let c=ma[a];c.instanceId=r,c.object=this,t.push(c)}ma.length=0}}setColorAt(e,t){return this.instanceColor===null&&(this.instanceColor=new cr(new Float32Array(this.instanceMatrix.count*3).fill(1),3)),t.toArray(this.instanceColor.array,e*3),this}setMatrixAt(e,t){return t.toArray(this.instanceMatrix.array,e*16),this}setMorphAt(e,t){let n=t.morphTargetInfluences,s=n.length+1;this.morphTexture===null&&(this.morphTexture=new lr(new Float32Array(s*this.count),s,this.count,yo,ln));let r=this.morphTexture.source.data.data,a=0;for(let l=0;l<n.length;l++)a+=n[l];let o=this.geometry.morphTargetsRelative?1:1-a,c=s*e;return r[c]=o,r.set(n,c+1),this}updateMorphTargets(){}dispose(){super.dispose(),this.morphTexture!==null&&(this.morphTexture.dispose(),this.morphTexture=null)}},Mi=new Dn,Vd=new he(.5,.5),ga=new L,xs=class{constructor(e=new pn,t=new pn,n=new pn,s=new pn,r=new pn,a=new pn){this.planes=[e,t,n,s,r,a]}set(e,t,n,s,r,a){let o=this.planes;return o[0].copy(e),o[1].copy(t),o[2].copy(n),o[3].copy(s),o[4].copy(r),o[5].copy(a),this}copy(e){let t=this.planes;for(let n=0;n<6;n++)t[n].copy(e.planes[n]);return this}setFromProjectionMatrix(e,t=gn,n=!1){let s=this.planes,r=e.elements,a=r[0],o=r[1],c=r[2],l=r[3],h=r[4],d=r[5],u=r[6],p=r[7],g=r[8],v=r[9],m=r[10],f=r[11],S=r[12],R=r[13],M=r[14],T=r[15];if(s[0].setComponents(l-a,p-h,f-g,T-S).normalize(),s[1].setComponents(l+a,p+h,f+g,T+S).normalize(),s[2].setComponents(l+o,p+d,f+v,T+R).normalize(),s[3].setComponents(l-o,p-d,f-v,T-R).normalize(),n)s[4].setComponents(c,u,m,M).normalize(),s[5].setComponents(l-c,p-u,f-m,T-M).normalize();else if(s[4].setComponents(l-c,p-u,f-m,T-M).normalize(),t===gn)s[5].setComponents(l+c,p+u,f+m,T+M).normalize();else if(t===cs)s[5].setComponents(c,u,m,M).normalize();else throw new Error("THREE.Frustum.setFromProjectionMatrix(): Invalid coordinate system: "+t);return this}intersectsObject(e){if(e.boundingSphere!==void 0)e.boundingSphere===null&&e.computeBoundingSphere(),Mi.copy(e.boundingSphere).applyMatrix4(e.matrixWorld);else{let t=e.geometry;t.boundingSphere===null&&t.computeBoundingSphere(),Mi.copy(t.boundingSphere).applyMatrix4(e.matrixWorld)}return this.intersectsSphere(Mi)}intersectsSprite(e){Mi.center.set(0,0,0);let t=Vd.distanceTo(e.center);return Mi.radius=.7071067811865476+t,Mi.applyMatrix4(e.matrixWorld),this.intersectsSphere(Mi)}intersectsSphere(e){let t=this.planes,n=e.center,s=-e.radius;for(let r=0;r<6;r++)if(t[r].distanceToPoint(n)<s)return!1;return!0}intersectsBox(e){let t=this.planes;for(let n=0;n<6;n++){let s=t[n];if(ga.x=s.normal.x>0?e.max.x:e.min.x,ga.y=s.normal.y>0?e.max.y:e.min.y,ga.z=s.normal.z>0?e.max.z:e.min.z,s.distanceToPoint(ga)<0)return!1}return!0}containsPoint(e){let t=this.planes;for(let n=0;n<6;n++)if(t[n].distanceToPoint(e)<0)return!1;return!0}clone(){return new this.constructor().copy(this)}};var ys=class extends xn{constructor(e){super(),this.isLineBasicMaterial=!0,this.type="LineBasicMaterial",this.color=new Fe(16777215),this.map=null,this.linewidth=1,this.linecap="round",this.linejoin="round",this.fog=!0,this.setValues(e)}copy(e){return super.copy(e),this.color.copy(e.color),this.map=e.map,this.linewidth=e.linewidth,this.linecap=e.linecap,this.linejoin=e.linejoin,this.fog=e.fog,this}},ka=new L,Ga=new L,dh=new ut,Zs=new Ei,_a=new Dn,Pl=new L,fh=new L,Wa=class extends Tt{constructor(e=new Mt,t=new ys){super(),this.isLine=!0,this.type="Line",this.geometry=e,this.material=t,this.morphTargetDictionary=void 0,this.morphTargetInfluences=void 0,this.updateMorphTargets()}copy(e,t){return super.copy(e,t),this.material=Array.isArray(e.material)?e.material.slice():e.material,this.geometry=e.geometry,this}computeLineDistances(){let e=this.geometry;if(e.index===null){let t=e.attributes.position,n=[0];for(let s=1,r=t.count;s<r;s++)ka.fromBufferAttribute(t,s-1),Ga.fromBufferAttribute(t,s),n[s]=n[s-1],n[s]+=ka.distanceTo(Ga);e.setAttribute("lineDistance",new it(n,1))}else ke("Line.computeLineDistances(): Computation only possible with non-indexed BufferGeometry.");return this}intersectsFrustum(e){return e.intersectsObject(this)}raycast(e,t){let n=this.geometry,s=this.matrixWorld,r=e.params.Line.threshold,a=n.drawRange;if(n.boundingSphere===null&&n.computeBoundingSphere(),_a.copy(n.boundingSphere),_a.applyMatrix4(s),_a.radius+=r,e.ray.intersectsSphere(_a)===!1)return;dh.copy(s).invert(),Zs.copy(e.ray).applyMatrix4(dh);let o=r/((this.scale.x+this.scale.y+this.scale.z)/3),c=o*o,l=this.isLineSegments?2:1,h=n.index,u=n.attributes.position;if(h!==null){let p=Math.max(0,a.start),g=Math.min(h.count,a.start+a.count);for(let v=p,m=g-1;v<m;v+=l){let f=h.getX(v),S=h.getX(v+1),R=xa(this,e,Zs,c,f,S,v);R&&t.push(R)}if(this.isLineLoop){let v=h.getX(g-1),m=h.getX(p),f=xa(this,e,Zs,c,v,m,g-1);f&&t.push(f)}}else{let p=Math.max(0,a.start),g=Math.min(u.count,a.start+a.count);for(let v=p,m=g-1;v<m;v+=l){let f=xa(this,e,Zs,c,v,v+1,v);f&&t.push(f)}if(this.isLineLoop){let v=xa(this,e,Zs,c,g-1,p,g-1);v&&t.push(v)}}}updateMorphTargets(){let t=this.geometry.morphAttributes,n=Object.keys(t);if(n.length>0){let s=t[n[0]];if(s!==void 0){this.morphTargetInfluences=[],this.morphTargetDictionary={};for(let r=0,a=s.length;r<a;r++){let o=s[r].name||String(r);this.morphTargetInfluences.push(0),this.morphTargetDictionary[o]=r}}}}};function xa(i,e,t,n,s,r,a){let o=i.geometry.attributes.position;if(ka.fromBufferAttribute(o,s),Ga.fromBufferAttribute(o,r),t.distanceSqToSegment(ka,Ga,Pl,fh)>n)return;Pl.applyMatrix4(i.matrixWorld);let l=e.ray.origin.distanceTo(Pl);if(!(l<e.near||l>e.far))return{distance:l,point:fh.clone().applyMatrix4(i.matrixWorld),index:a,face:null,faceIndex:null,barycoord:null,object:i}}var ph=new L,mh=new L,ur=class extends Wa{constructor(e,t){super(e,t),this.isLineSegments=!0,this.type="LineSegments"}computeLineDistances(){let e=this.geometry;if(e.index===null){let t=e.attributes.position,n=[];for(let s=0,r=t.count;s<r;s+=2)ph.fromBufferAttribute(t,s),mh.fromBufferAttribute(t,s+1),n[s]=s===0?0:n[s-1],n[s+1]=n[s]+ph.distanceTo(mh);e.setAttribute("lineDistance",new it(n,1))}else ke("LineSegments.computeLineDistances(): Computation only possible with non-indexed BufferGeometry.");return this}};var vs=class extends xn{constructor(e){super(),this.isPointsMaterial=!0,this.type="PointsMaterial",this.color=new Fe(16777215),this.map=null,this.alphaMap=null,this.size=1,this.sizeAttenuation=!0,this.fog=!0,this.setValues(e)}copy(e){return super.copy(e),this.color.copy(e.color),this.map=e.map,this.alphaMap=e.alphaMap,this.size=e.size,this.sizeAttenuation=e.sizeAttenuation,this.fog=e.fog,this}},gh=new ut,Vl=new Ei,ya=new Dn,va=new L,dr=class extends Tt{constructor(e=new Mt,t=new vs){super(),this.isPoints=!0,this.type="Points",this.geometry=e,this.material=t,this.morphTargetDictionary=void 0,this.morphTargetInfluences=void 0,this.updateMorphTargets()}copy(e,t){return super.copy(e,t),this.material=Array.isArray(e.material)?e.material.slice():e.material,this.geometry=e.geometry,this}intersectsFrustum(e){return e.intersectsObject(this)}raycast(e,t){let n=this.geometry,s=this.matrixWorld,r=e.params.Points.threshold,a=n.drawRange;if(n.boundingSphere===null&&n.computeBoundingSphere(),ya.copy(n.boundingSphere),ya.applyMatrix4(s),ya.radius+=r,e.ray.intersectsSphere(ya)===!1)return;gh.copy(s).invert(),Vl.copy(e.ray).applyMatrix4(gh);let o=r/((this.scale.x+this.scale.y+this.scale.z)/3),c=o*o,l=n.index,d=n.attributes.position;if(l!==null){let u=Math.max(0,a.start),p=Math.min(l.count,a.start+a.count);for(let g=u,v=p;g<v;g++){let m=l.getX(g);va.fromBufferAttribute(d,m),_h(va,m,c,s,e,t,this)}}else{let u=Math.max(0,a.start),p=Math.min(d.count,a.start+a.count);for(let g=u,v=p;g<v;g++)va.fromBufferAttribute(d,g),_h(va,g,c,s,e,t,this)}}updateMorphTargets(){let t=this.geometry.morphAttributes,n=Object.keys(t);if(n.length>0){let s=t[n[0]];if(s!==void 0){this.morphTargetInfluences=[],this.morphTargetDictionary={};for(let r=0,a=s.length;r<a;r++){let o=s[r].name||String(r);this.morphTargetInfluences.push(0),this.morphTargetDictionary[o]=r}}}}};function _h(i,e,t,n,s,r,a){let o=Vl.distanceSqToPoint(i);if(o<t){let c=new L;Vl.closestPointToPoint(i,c),c.applyMatrix4(n);let l=s.ray.origin.distanceTo(c);if(l<s.near||l>s.far)return;r.push({distance:l,distanceToRay:Math.sqrt(o),point:c,index:e,face:null,faceIndex:null,barycoord:null,object:a})}}var fr=class extends Vt{constructor(e=[],t=pi,n,s,r,a,o,c,l,h){super(e,t,n,s,r,a,o,c,l,h),this.isCubeTexture=!0,this.flipY=!1}get images(){return this.image}set images(e){this.image=e}},Un=class extends Vt{constructor(e,t,n,s,r,a,o,c,l){super(e,t,n,s,r,a,o,c,l),this.isCanvasTexture=!0,this.needsUpdate=!0}};var oi=class extends Vt{constructor(e,t,n=Mn,s,r,a,o=Lt,c=Lt,l,h=Cn,d=1){if(h!==Cn&&h!==mi)throw new Error("THREE.DepthTexture: format must be either THREE.DepthFormat or THREE.DepthStencilFormat");let u={width:e,height:t,depth:d};super(u,s,r,a,o,c,h,n,l),this.isDepthTexture=!0,this.flipY=!1,this.generateMipmaps=!1,this.compareFunction=null}copy(e){return super.copy(e),this.source=new fs(Object.assign({},e.image)),this.compareFunction=e.compareFunction,this}toJSON(e){let t=super.toJSON(e);return t.compareFunction=this.compareFunction,t}},Xa=class extends oi{constructor(e,t=Mn,n=pi,s,r,a=Lt,o=Lt,c,l=Cn){let h={width:e,height:e,depth:1},d=[h,h,h,h,h,h];super(e,e,t,n,s,r,a,o,c,l),this.image=d,this.isCubeDepthTexture=!0,this.isCubeTexture=!0}get images(){return this.image}set images(e){this.image=e}},pr=class extends Vt{constructor(e=null){super(),this.sourceTexture=e,this.isExternalTexture=!0}copy(e){return super.copy(e),this.sourceTexture=e.sourceTexture,this}},Dt=class i extends Mt{constructor(e=1,t=1,n=1,s=1,r=1,a=1){super(),this.type="BoxGeometry",this.parameters={width:e,height:t,depth:n,widthSegments:s,heightSegments:r,depthSegments:a};let o=this;s=Math.floor(s),r=Math.floor(r),a=Math.floor(a);let c=[],l=[],h=[],d=[],u=0,p=0;g("z","y","x",-1,-1,n,t,e,a,r,0),g("z","y","x",1,-1,n,t,-e,a,r,1),g("x","z","y",1,1,e,n,t,s,a,2),g("x","z","y",1,-1,e,n,-t,s,a,3),g("x","y","z",1,-1,e,t,n,s,r,4),g("x","y","z",-1,-1,e,t,-n,s,r,5),this.setIndex(c),this.setAttribute("position",new it(l,3)),this.setAttribute("normal",new it(h,3)),this.setAttribute("uv",new it(d,2));function g(v,m,f,S,R,M,T,w,P,x,E){let C=M/P,U=T/x,B=M/2,H=T/2,N=w/2,V=P+1,X=x+1,W=0,re=0,Y=new L;for(let Q=0;Q<X;Q++){let j=Q*U-H;for(let Re=0;Re<V;Re++){let Ce=Re*C-B;Y[v]=Ce*S,Y[m]=j*R,Y[f]=N,l.push(Y.x,Y.y,Y.z),Y[v]=0,Y[m]=0,Y[f]=w>0?1:-1,h.push(Y.x,Y.y,Y.z),d.push(Re/P),d.push(1-Q/x),W+=1}}for(let Q=0;Q<x;Q++)for(let j=0;j<P;j++){let Re=u+j+V*Q,Ce=u+j+V*(Q+1),lt=u+(j+1)+V*(Q+1),je=u+(j+1)+V*Q;c.push(Re,Ce,je),c.push(Ce,lt,je),re+=6}o.addGroup(p,re,E),p+=re,u+=W}}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}static fromJSON(e){return new i(e.width,e.height,e.depth,e.widthSegments,e.heightSegments,e.depthSegments)}},Ms=class i extends Mt{constructor(e=1,t=1,n=4,s=8,r=1){super(),this.type="CapsuleGeometry",this.parameters={radius:e,height:t,capSegments:n,radialSegments:s,heightSegments:r},t=Math.max(0,t),n=Math.max(1,Math.floor(n)),s=Math.max(3,Math.floor(s)),r=Math.max(1,Math.floor(r));let a=[],o=[],c=[],l=[],h=t/2,d=Math.PI/2*e,u=t,p=2*d+u,g=n*2+r,v=s+1,m=new L,f=new L;for(let S=0;S<=g;S++){let R=0,M=0,T=0,w=0;if(S<=n){let E=S/n,C=E*Math.PI/2;M=-h-e*Math.cos(C),T=e*Math.sin(C),w=-e*Math.cos(C),R=E*d}else if(S<=n+r){let E=(S-n)/r;M=-h+E*t,T=e,w=0,R=d+E*u}else{let E=(S-n-r)/n,C=E*Math.PI/2;M=h+e*Math.sin(C),T=e*Math.cos(C),w=e*Math.sin(C),R=d+u+E*d}let P=Math.max(0,Math.min(1,R/p)),x=0;S===0?x=.5/s:S===g&&(x=-.5/s);for(let E=0;E<=s;E++){let C=E/s,U=C*Math.PI*2,B=Math.sin(U),H=Math.cos(U);f.x=-T*H,f.y=M,f.z=T*B,o.push(f.x,f.y,f.z),m.set(-T*H,w,T*B),m.normalize(),c.push(m.x,m.y,m.z),l.push(C+x,P)}if(S>0){let E=(S-1)*v;for(let C=0;C<s;C++){let U=E+C,B=E+C+1,H=S*v+C,N=S*v+C+1;a.push(U,B,H),a.push(B,N,H)}}}this.setIndex(a),this.setAttribute("position",new it(o,3)),this.setAttribute("normal",new it(c,3)),this.setAttribute("uv",new it(l,2))}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}static fromJSON(e){return new i(e.radius,e.height,e.capSegments,e.radialSegments,e.heightSegments)}},mr=class i extends Mt{constructor(e=1,t=32,n=0,s=Math.PI*2){super(),this.type="CircleGeometry",this.parameters={radius:e,segments:t,thetaStart:n,thetaLength:s},t=Math.max(3,t);let r=[],a=[],o=[],c=[],l=new L,h=new he;a.push(0,0,0),o.push(0,0,1),c.push(.5,.5);for(let d=0,u=3;d<=t;d++,u+=3){let p=n+d/t*s;l.x=e*Math.cos(p),l.y=e*Math.sin(p),a.push(l.x,l.y,l.z),o.push(0,0,1),h.x=(a[u]/e+1)/2,h.y=(a[u+1]/e+1)/2,c.push(h.x,h.y)}for(let d=1;d<=t;d++)r.push(d,d+1,0);this.setIndex(r),this.setAttribute("position",new it(a,3)),this.setAttribute("normal",new it(o,3)),this.setAttribute("uv",new it(c,2))}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}static fromJSON(e){return new i(e.radius,e.segments,e.thetaStart,e.thetaLength)}},Ss=class i extends Mt{constructor(e=1,t=1,n=1,s=32,r=1,a=!1,o=0,c=Math.PI*2){super(),this.type="CylinderGeometry",this.parameters={radiusTop:e,radiusBottom:t,height:n,radialSegments:s,heightSegments:r,openEnded:a,thetaStart:o,thetaLength:c};let l=this;s=Math.floor(s),r=Math.floor(r);let h=[],d=[],u=[],p=[],g=0,v=[],m=n/2,f=0;S(),a===!1&&(e>0&&R(!0),t>0&&R(!1)),this.setIndex(h),this.setAttribute("position",new it(d,3)),this.setAttribute("normal",new it(u,3)),this.setAttribute("uv",new it(p,2));function S(){let M=new L,T=new L,w=0,P=(t-e)/n;for(let x=0;x<=r;x++){let E=[],C=x/r,U=C*(t-e)+e;for(let B=0;B<=s;B++){let H=B/s,N=H*c+o,V=Math.sin(N),X=Math.cos(N);T.x=U*V,T.y=-C*n+m,T.z=U*X,d.push(T.x,T.y,T.z),M.set(V,P,X).normalize(),u.push(M.x,M.y,M.z),p.push(H,1-C),E.push(g++)}v.push(E)}for(let x=0;x<s;x++)for(let E=0;E<r;E++){let C=v[E][x],U=v[E+1][x],B=v[E+1][x+1],H=v[E][x+1];(e>0||E!==0)&&(h.push(C,U,H),w+=3),(t>0||E!==r-1)&&(h.push(U,B,H),w+=3)}l.addGroup(f,w,0),f+=w}function R(M){let T=g,w=new he,P=new L,x=0,E=M===!0?e:t,C=M===!0?1:-1;for(let B=1;B<=s;B++)d.push(0,m*C,0),u.push(0,C,0),p.push(.5,.5),g++;let U=g;for(let B=0;B<=s;B++){let N=B/s*c+o,V=Math.cos(N),X=Math.sin(N);P.x=E*X,P.y=m*C,P.z=E*V,d.push(P.x,P.y,P.z),u.push(0,C,0),w.x=V*.5+.5,w.y=X*.5*C+.5,p.push(w.x,w.y),g++}for(let B=0;B<s;B++){let H=T+B,N=U+B;M===!0?h.push(N,N+1,H):h.push(N+1,N,H),x+=3}l.addGroup(f,x,M===!0?1:2),f+=x}}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}static fromJSON(e){return new i(e.radiusTop,e.radiusBottom,e.height,e.radialSegments,e.heightSegments,e.openEnded,e.thetaStart,e.thetaLength)}};var jt=class{constructor(){this.type="Curve",this.arcLengthDivisions=200,this.needsUpdate=!1,this.cacheArcLengths=null}getPoint(){ke("Curve: .getPoint() not implemented.")}getPointAt(e,t){let n=this.getUtoTmapping(e);return this.getPoint(n,t)}getPoints(e=5){let t=[];for(let n=0;n<=e;n++)t.push(this.getPoint(n/e));return t}getSpacedPoints(e=5){let t=[];for(let n=0;n<=e;n++)t.push(this.getPointAt(n/e));return t}getLength(){let e=this.getLengths();return e[e.length-1]}getLengths(e=this.arcLengthDivisions){if(this.cacheArcLengths&&this.cacheArcLengths.length===e+1&&!this.needsUpdate)return this.cacheArcLengths;this.needsUpdate=!1;let t=[],n,s=this.getPoint(0),r=0;t.push(0);for(let a=1;a<=e;a++)n=this.getPoint(a/e),r+=n.distanceTo(s),t.push(r),s=n;return this.cacheArcLengths=t,t}updateArcLengths(){this.needsUpdate=!0,this.getLengths()}getUtoTmapping(e,t=null){let n=this.getLengths(),s=0,r=n.length,a;t?a=t:a=e*n[r-1];let o=0,c=r-1,l;for(;o<=c;)if(s=Math.floor(o+(c-o)/2),l=n[s]-a,l<0)o=s+1;else if(l>0)c=s-1;else{c=s;break}if(s=c,n[s]===a)return s/(r-1);let h=n[s],u=n[s+1]-h,p=(a-h)/u;return(s+p)/(r-1)}getTangent(e,t){let s=e-1e-4,r=e+1e-4;s<0&&(s=0),r>1&&(r=1);let a=this.getPoint(s),o=this.getPoint(r),c=t||(a.isVector2?new he:new L);return c.copy(o).sub(a).normalize(),c}getTangentAt(e,t){let n=this.getUtoTmapping(e);return this.getTangent(n,t)}computeFrenetFrames(e,t=!1){let n=new L,s=[],r=[],a=[],o=new L,c=new ut;for(let p=0;p<=e;p++){let g=p/e;s[p]=this.getTangentAt(g,new L)}r[0]=new L,a[0]=new L;let l=Number.MAX_VALUE,h=Math.abs(s[0].x),d=Math.abs(s[0].y),u=Math.abs(s[0].z);h<=l&&(l=h,n.set(1,0,0)),d<=l&&(l=d,n.set(0,1,0)),u<=l&&n.set(0,0,1),o.crossVectors(s[0],n).normalize(),r[0].crossVectors(s[0],o),a[0].crossVectors(s[0],r[0]);for(let p=1;p<=e;p++){if(r[p]=r[p-1].clone(),a[p]=a[p-1].clone(),o.crossVectors(s[p-1],s[p]),o.length()>Number.EPSILON){o.normalize();let g=Math.acos(nt(s[p-1].dot(s[p]),-1,1));r[p].applyMatrix4(c.makeRotationAxis(o,g))}a[p].crossVectors(s[p],r[p])}if(t===!0){let p=Math.acos(nt(r[0].dot(r[e]),-1,1));p/=e,s[0].dot(o.crossVectors(r[0],r[e]))>0&&(p=-p);for(let g=1;g<=e;g++)r[g].applyMatrix4(c.makeRotationAxis(s[g],p*g)),a[g].crossVectors(s[g],r[g])}return{tangents:s,normals:r,binormals:a}}clone(){return new this.constructor().copy(this)}copy(e){return this.arcLengthDivisions=e.arcLengthDivisions,this}toJSON(){let e={metadata:{version:4.7,type:"Curve",generator:"Curve.toJSON"}};return e.arcLengthDivisions=this.arcLengthDivisions,e.type=this.type,e}fromJSON(e){return this.arcLengthDivisions=e.arcLengthDivisions,this}},bs=class extends jt{constructor(e=0,t=0,n=1,s=1,r=0,a=Math.PI*2,o=!1,c=0){super(),this.isEllipseCurve=!0,this.type="EllipseCurve",this.aX=e,this.aY=t,this.xRadius=n,this.yRadius=s,this.aStartAngle=r,this.aEndAngle=a,this.aClockwise=o,this.aRotation=c}getPoint(e,t=new he){let n=t,s=Math.PI*2,r=this.aEndAngle-this.aStartAngle,a=Math.abs(r)<Number.EPSILON;for(;r<0;)r+=s;for(;r>s;)r-=s;r<Number.EPSILON&&(a?r=0:r=s),this.aClockwise===!0&&!a&&(r===s?r=-s:r=r-s);let o=this.aStartAngle+e*r,c=this.aX+this.xRadius*Math.cos(o),l=this.aY+this.yRadius*Math.sin(o);if(this.aRotation!==0){let h=Math.cos(this.aRotation),d=Math.sin(this.aRotation),u=c-this.aX,p=l-this.aY;c=u*h-p*d+this.aX,l=u*d+p*h+this.aY}return n.set(c,l)}copy(e){return super.copy(e),this.aX=e.aX,this.aY=e.aY,this.xRadius=e.xRadius,this.yRadius=e.yRadius,this.aStartAngle=e.aStartAngle,this.aEndAngle=e.aEndAngle,this.aClockwise=e.aClockwise,this.aRotation=e.aRotation,this}toJSON(){let e=super.toJSON();return e.aX=this.aX,e.aY=this.aY,e.xRadius=this.xRadius,e.yRadius=this.yRadius,e.aStartAngle=this.aStartAngle,e.aEndAngle=this.aEndAngle,e.aClockwise=this.aClockwise,e.aRotation=this.aRotation,e}fromJSON(e){return super.fromJSON(e),this.aX=e.aX,this.aY=e.aY,this.xRadius=e.xRadius,this.yRadius=e.yRadius,this.aStartAngle=e.aStartAngle,this.aEndAngle=e.aEndAngle,this.aClockwise=e.aClockwise,this.aRotation=e.aRotation,this}},qa=class extends bs{constructor(e,t,n,s,r,a){super(e,t,n,n,s,r,a),this.isArcCurve=!0,this.type="ArcCurve"}};function yc(){let i=0,e=0,t=0,n=0;function s(r,a,o,c){i=r,e=o,t=-3*r+3*a-2*o-c,n=2*r-2*a+o+c}return{initCatmullRom:function(r,a,o,c,l){s(a,o,l*(o-r),l*(c-a))},initNonuniformCatmullRom:function(r,a,o,c,l,h,d){let u=(a-r)/l-(o-r)/(l+h)+(o-a)/h,p=(o-a)/h-(c-a)/(h+d)+(c-o)/d;u*=h,p*=h,s(a,o,u,p)},calc:function(r){let a=r*r,o=a*r;return i+e*r+t*a+n*o}}}var xh=new L,yh=new L,Ll=new yc,Dl=new yc,Ul=new yc,gr=class extends jt{constructor(e=[],t=!1,n="centripetal",s=.5){super(),this.isCatmullRomCurve3=!0,this.type="CatmullRomCurve3",this.points=e,this.closed=t,this.curveType=n,this.tension=s}getPoint(e,t=new L){let n=t,s=this.points,r=s.length,a=(r-(this.closed?0:1))*e,o=Math.floor(a),c=a-o;this.closed?o+=o>0?0:(Math.floor(Math.abs(o)/r)+1)*r:c===0&&o===r-1&&(o=r-2,c=1);let l,h;this.closed||o>0?l=s[(o-1)%r]:(yh.subVectors(s[0],s[1]).add(s[0]),l=yh);let d=s[o%r],u=s[(o+1)%r];if(this.closed||o+2<r?h=s[(o+2)%r]:(xh.subVectors(s[r-1],s[r-2]).add(s[r-1]),h=xh),this.curveType==="centripetal"||this.curveType==="chordal"){let p=this.curveType==="chordal"?.5:.25,g=Math.pow(l.distanceToSquared(d),p),v=Math.pow(d.distanceToSquared(u),p),m=Math.pow(u.distanceToSquared(h),p);v<1e-4&&(v=1),g<1e-4&&(g=v),m<1e-4&&(m=v),Ll.initNonuniformCatmullRom(l.x,d.x,u.x,h.x,g,v,m),Dl.initNonuniformCatmullRom(l.y,d.y,u.y,h.y,g,v,m),Ul.initNonuniformCatmullRom(l.z,d.z,u.z,h.z,g,v,m)}else this.curveType==="catmullrom"&&(Ll.initCatmullRom(l.x,d.x,u.x,h.x,this.tension),Dl.initCatmullRom(l.y,d.y,u.y,h.y,this.tension),Ul.initCatmullRom(l.z,d.z,u.z,h.z,this.tension));return n.set(Ll.calc(c),Dl.calc(c),Ul.calc(c)),n}copy(e){super.copy(e),this.points=[];for(let t=0,n=e.points.length;t<n;t++){let s=e.points[t];this.points.push(s.clone())}return this.closed=e.closed,this.curveType=e.curveType,this.tension=e.tension,this}toJSON(){let e=super.toJSON();e.points=[];for(let t=0,n=this.points.length;t<n;t++){let s=this.points[t];e.points.push(s.toArray())}return e.closed=this.closed,e.curveType=this.curveType,e.tension=this.tension,e}fromJSON(e){super.fromJSON(e),this.points=[];for(let t=0,n=e.points.length;t<n;t++){let s=e.points[t];this.points.push(new L().fromArray(s))}return this.closed=e.closed,this.curveType=e.curveType,this.tension=e.tension,this}};function vh(i,e,t,n,s){let r=(n-e)*.5,a=(s-t)*.5,o=i*i,c=i*o;return(2*t-2*n+r+a)*c+(-3*t+3*n-2*r-a)*o+r*i+t}function kd(i,e){let t=1-i;return t*t*e}function Gd(i,e){return 2*(1-i)*i*e}function Wd(i,e){return i*i*e}function Qs(i,e,t,n){return kd(i,e)+Gd(i,t)+Wd(i,n)}function Xd(i,e){let t=1-i;return t*t*t*e}function qd(i,e){let t=1-i;return 3*t*t*i*e}function Yd(i,e){return 3*(1-i)*i*i*e}function Zd(i,e){return i*i*i*e}function js(i,e,t,n,s){return Xd(i,e)+qd(i,t)+Yd(i,n)+Zd(i,s)}var _r=class extends jt{constructor(e=new he,t=new he,n=new he,s=new he){super(),this.isCubicBezierCurve=!0,this.type="CubicBezierCurve",this.v0=e,this.v1=t,this.v2=n,this.v3=s}getPoint(e,t=new he){let n=t,s=this.v0,r=this.v1,a=this.v2,o=this.v3;return n.set(js(e,s.x,r.x,a.x,o.x),js(e,s.y,r.y,a.y,o.y)),n}copy(e){return super.copy(e),this.v0.copy(e.v0),this.v1.copy(e.v1),this.v2.copy(e.v2),this.v3.copy(e.v3),this}toJSON(){let e=super.toJSON();return e.v0=this.v0.toArray(),e.v1=this.v1.toArray(),e.v2=this.v2.toArray(),e.v3=this.v3.toArray(),e}fromJSON(e){return super.fromJSON(e),this.v0.fromArray(e.v0),this.v1.fromArray(e.v1),this.v2.fromArray(e.v2),this.v3.fromArray(e.v3),this}},Ya=class extends jt{constructor(e=new L,t=new L,n=new L,s=new L){super(),this.isCubicBezierCurve3=!0,this.type="CubicBezierCurve3",this.v0=e,this.v1=t,this.v2=n,this.v3=s}getPoint(e,t=new L){let n=t,s=this.v0,r=this.v1,a=this.v2,o=this.v3;return n.set(js(e,s.x,r.x,a.x,o.x),js(e,s.y,r.y,a.y,o.y),js(e,s.z,r.z,a.z,o.z)),n}copy(e){return super.copy(e),this.v0.copy(e.v0),this.v1.copy(e.v1),this.v2.copy(e.v2),this.v3.copy(e.v3),this}toJSON(){let e=super.toJSON();return e.v0=this.v0.toArray(),e.v1=this.v1.toArray(),e.v2=this.v2.toArray(),e.v3=this.v3.toArray(),e}fromJSON(e){return super.fromJSON(e),this.v0.fromArray(e.v0),this.v1.fromArray(e.v1),this.v2.fromArray(e.v2),this.v3.fromArray(e.v3),this}},xr=class extends jt{constructor(e=new he,t=new he){super(),this.isLineCurve=!0,this.type="LineCurve",this.v1=e,this.v2=t}getPoint(e,t=new he){let n=t;return e===1?n.copy(this.v2):(n.copy(this.v2).sub(this.v1),n.multiplyScalar(e).add(this.v1)),n}getPointAt(e,t){return this.getPoint(e,t)}getTangent(e,t=new he){return t.subVectors(this.v2,this.v1).normalize()}getTangentAt(e,t){return this.getTangent(e,t)}copy(e){return super.copy(e),this.v1.copy(e.v1),this.v2.copy(e.v2),this}toJSON(){let e=super.toJSON();return e.v1=this.v1.toArray(),e.v2=this.v2.toArray(),e}fromJSON(e){return super.fromJSON(e),this.v1.fromArray(e.v1),this.v2.fromArray(e.v2),this}},Za=class extends jt{constructor(e=new L,t=new L){super(),this.isLineCurve3=!0,this.type="LineCurve3",this.v1=e,this.v2=t}getPoint(e,t=new L){let n=t;return e===1?n.copy(this.v2):(n.copy(this.v2).sub(this.v1),n.multiplyScalar(e).add(this.v1)),n}getPointAt(e,t){return this.getPoint(e,t)}getTangent(e,t=new L){return t.subVectors(this.v2,this.v1).normalize()}getTangentAt(e,t){return this.getTangent(e,t)}copy(e){return super.copy(e),this.v1.copy(e.v1),this.v2.copy(e.v2),this}toJSON(){let e=super.toJSON();return e.v1=this.v1.toArray(),e.v2=this.v2.toArray(),e}fromJSON(e){return super.fromJSON(e),this.v1.fromArray(e.v1),this.v2.fromArray(e.v2),this}},yr=class extends jt{constructor(e=new he,t=new he,n=new he){super(),this.isQuadraticBezierCurve=!0,this.type="QuadraticBezierCurve",this.v0=e,this.v1=t,this.v2=n}getPoint(e,t=new he){let n=t,s=this.v0,r=this.v1,a=this.v2;return n.set(Qs(e,s.x,r.x,a.x),Qs(e,s.y,r.y,a.y)),n}copy(e){return super.copy(e),this.v0.copy(e.v0),this.v1.copy(e.v1),this.v2.copy(e.v2),this}toJSON(){let e=super.toJSON();return e.v0=this.v0.toArray(),e.v1=this.v1.toArray(),e.v2=this.v2.toArray(),e}fromJSON(e){return super.fromJSON(e),this.v0.fromArray(e.v0),this.v1.fromArray(e.v1),this.v2.fromArray(e.v2),this}},Ja=class extends jt{constructor(e=new L,t=new L,n=new L){super(),this.isQuadraticBezierCurve3=!0,this.type="QuadraticBezierCurve3",this.v0=e,this.v1=t,this.v2=n}getPoint(e,t=new L){let n=t,s=this.v0,r=this.v1,a=this.v2;return n.set(Qs(e,s.x,r.x,a.x),Qs(e,s.y,r.y,a.y),Qs(e,s.z,r.z,a.z)),n}copy(e){return super.copy(e),this.v0.copy(e.v0),this.v1.copy(e.v1),this.v2.copy(e.v2),this}toJSON(){let e=super.toJSON();return e.v0=this.v0.toArray(),e.v1=this.v1.toArray(),e.v2=this.v2.toArray(),e}fromJSON(e){return super.fromJSON(e),this.v0.fromArray(e.v0),this.v1.fromArray(e.v1),this.v2.fromArray(e.v2),this}},vr=class extends jt{constructor(e=[]){super(),this.isSplineCurve=!0,this.type="SplineCurve",this.points=e}getPoint(e,t=new he){let n=t,s=this.points,r=(s.length-1)*e,a=Math.floor(r),o=r-a,c=s[a===0?a:a-1],l=s[a],h=s[a>s.length-2?s.length-1:a+1],d=s[a>s.length-3?s.length-1:a+2];return n.set(vh(o,c.x,l.x,h.x,d.x),vh(o,c.y,l.y,h.y,d.y)),n}copy(e){super.copy(e),this.points=[];for(let t=0,n=e.points.length;t<n;t++){let s=e.points[t];this.points.push(s.clone())}return this}toJSON(){let e=super.toJSON();e.points=[];for(let t=0,n=this.points.length;t<n;t++){let s=this.points[t];e.points.push(s.toArray())}return e}fromJSON(e){super.fromJSON(e),this.points=[];for(let t=0,n=e.points.length;t<n;t++){let s=e.points[t];this.points.push(new he().fromArray(s))}return this}},kl=Object.freeze({__proto__:null,ArcCurve:qa,CatmullRomCurve3:gr,CubicBezierCurve:_r,CubicBezierCurve3:Ya,EllipseCurve:bs,LineCurve:xr,LineCurve3:Za,QuadraticBezierCurve:yr,QuadraticBezierCurve3:Ja,SplineCurve:vr}),$a=class extends jt{constructor(){super(),this.type="CurvePath",this.curves=[],this.autoClose=!1}add(e){this.curves.push(e)}closePath(){let e=this.curves[0].getPoint(0),t=this.curves[this.curves.length-1].getPoint(1);if(!e.equals(t)){let n=e.isVector2===!0?"LineCurve":"LineCurve3";this.curves.push(new kl[n](t,e))}return this}getPoint(e,t){let n=e*this.getLength(),s=this.getCurveLengths(),r=0;for(;r<s.length;){if(s[r]>=n){let a=s[r]-n,o=this.curves[r],c=o.getLength(),l=c===0?0:1-a/c;return o.getPointAt(l,t)}r++}return null}getLength(){let e=this.getCurveLengths();return e[e.length-1]}updateArcLengths(){this.needsUpdate=!0,this.cacheLengths=null,this.getCurveLengths()}getCurveLengths(){if(this.cacheLengths&&this.cacheLengths.length===this.curves.length)return this.cacheLengths;let e=[],t=0;for(let n=0,s=this.curves.length;n<s;n++)t+=this.curves[n].getLength(),e.push(t);return this.cacheLengths=e,e}getSpacedPoints(e=40){let t=[];for(let n=0;n<=e;n++)t.push(this.getPoint(n/e));return this.autoClose&&t.push(t[0]),t}getPoints(e=12){let t=[],n;for(let s=0,r=this.curves;s<r.length;s++){let a=r[s],o=a.isEllipseCurve?e*2:a.isLineCurve||a.isLineCurve3?1:a.isSplineCurve?e*a.points.length:e,c=a.getPoints(o);for(let l=0;l<c.length;l++){let h=c[l];n&&n.equals(h)||(t.push(h),n=h)}}return this.autoClose&&t.length>1&&!t[t.length-1].equals(t[0])&&t.push(t[0]),t}copy(e){super.copy(e),this.curves=[];for(let t=0,n=e.curves.length;t<n;t++){let s=e.curves[t];this.curves.push(s.clone())}return this.autoClose=e.autoClose,this}toJSON(){let e=super.toJSON();e.autoClose=this.autoClose,e.curves=[];for(let t=0,n=this.curves.length;t<n;t++){let s=this.curves[t];e.curves.push(s.toJSON())}return e}fromJSON(e){super.fromJSON(e),this.autoClose=e.autoClose,this.curves=[];for(let t=0,n=e.curves.length;t<n;t++){let s=e.curves[t];this.curves.push(new kl[s.type]().fromJSON(s))}return this}},Mr=class extends $a{constructor(e){super(),this.type="Path",this.currentPoint=new he,e&&this.setFromPoints(e)}setFromPoints(e){this.moveTo(e[0].x,e[0].y);for(let t=1,n=e.length;t<n;t++)this.lineTo(e[t].x,e[t].y);return this}moveTo(e,t){return this.currentPoint.set(e,t),this}lineTo(e,t){let n=new xr(this.currentPoint.clone(),new he(e,t));return this.curves.push(n),this.currentPoint.set(e,t),this}quadraticCurveTo(e,t,n,s){let r=new yr(this.currentPoint.clone(),new he(e,t),new he(n,s));return this.curves.push(r),this.currentPoint.set(n,s),this}bezierCurveTo(e,t,n,s,r,a){let o=new _r(this.currentPoint.clone(),new he(e,t),new he(n,s),new he(r,a));return this.curves.push(o),this.currentPoint.set(r,a),this}splineThru(e){let t=[this.currentPoint.clone()].concat(e),n=new vr(t);return this.curves.push(n),this.currentPoint.copy(e[e.length-1]),this}arc(e,t,n,s,r,a){let o=this.currentPoint.x,c=this.currentPoint.y;return this.absarc(e+o,t+c,n,s,r,a),this}absarc(e,t,n,s,r,a){return this.absellipse(e,t,n,n,s,r,a),this}ellipse(e,t,n,s,r,a,o,c){let l=this.currentPoint.x,h=this.currentPoint.y;return this.absellipse(e+l,t+h,n,s,r,a,o,c),this}absellipse(e,t,n,s,r,a,o,c){let l=new bs(e,t,n,s,r,a,o,c);if(this.curves.length>0){let d=l.getPoint(0);d.equals(this.currentPoint)||this.lineTo(d.x,d.y)}this.curves.push(l);let h=l.getPoint(1);return this.currentPoint.copy(h),this}copy(e){return super.copy(e),this.currentPoint.copy(e.currentPoint),this}toJSON(){let e=super.toJSON();return e.currentPoint=this.currentPoint.toArray(),e}fromJSON(e){return super.fromJSON(e),this.currentPoint.fromArray(e.currentPoint),this}},$n=class extends Mr{constructor(e){super(e),this.uuid=Rn(),this.type="Shape",this.holes=[]}getPointsHoles(e){let t=[];for(let n=0,s=this.holes.length;n<s;n++)t[n]=this.holes[n].getPoints(e);return t}extractPoints(e){return{shape:this.getPoints(e),holes:this.getPointsHoles(e)}}copy(e){super.copy(e),this.holes=[];for(let t=0,n=e.holes.length;t<n;t++){let s=e.holes[t];this.holes.push(s.clone())}return this}toJSON(){let e=super.toJSON();e.uuid=this.uuid,e.holes=[];for(let t=0,n=this.holes.length;t<n;t++){let s=this.holes[t];e.holes.push(s.toJSON())}return e}fromJSON(e){super.fromJSON(e),this.uuid=e.uuid,this.holes=[];for(let t=0,n=e.holes.length;t<n;t++){let s=e.holes[t];this.holes.push(new Mr().fromJSON(s))}return this}};function Jd(i,e,t=2){let n=e&&e.length,s=n?e[0]*t:i.length,r=yu(i,0,s,t,!0),a=[];if(!r||r.next===r.prev)return a;let o,c,l;if(n&&(r=ef(i,e,r,t)),i.length>80*t){o=i[0],c=i[1];let h=o,d=c;for(let u=t;u<s;u+=t){let p=i[u],g=i[u+1];p<o&&(o=p),g<c&&(c=g),p>h&&(h=p),g>d&&(d=g)}l=Math.max(h-o,d-c),l=l!==0?32767/l:0}return Sr(r,a,t,o,c,l,0),a}function yu(i,e,t,n,s){let r;if(s===df(i,e,t,n)>0)for(let a=e;a<t;a+=n)r=Mh(a/n|0,i[a],i[a+1],r);else for(let a=t-n;a>=e;a-=n)r=Mh(a/n|0,i[a],i[a+1],r);return r&&Es(r,r.next)&&(Er(r),r=r.next),r}function Ti(i,e){if(!i)return i;e||(e=i);let t=i,n;do if(n=!1,!t.steiner&&(Es(t,t.next)||St(t.prev,t,t.next)===0)){if(Er(t),t=e=t.prev,t===t.next)break;n=!0}else t=t.next;while(n||t!==e);return e}function Sr(i,e,t,n,s,r,a){if(!i)return;!a&&r&&af(i,n,s,r);let o=i;for(;i.prev!==i.next;){let c=i.prev,l=i.next;if(r?Kd(i,n,s,r):$d(i)){e.push(c.i,i.i,l.i),Er(i),i=l.next,o=l.next;continue}if(i=l,i===o){a?a===1?(i=Qd(Ti(i),e),Sr(i,e,t,n,s,r,2)):a===2&&jd(i,e,t,n,s,r):Sr(Ti(i),e,t,n,s,r,1);break}}}function $d(i){let e=i.prev,t=i,n=i.next;if(St(e,t,n)>=0)return!1;let s=e.x,r=t.x,a=n.x,o=e.y,c=t.y,l=n.y,h=Math.min(s,r,a),d=Math.min(o,c,l),u=Math.max(s,r,a),p=Math.max(o,c,l),g=n.next;for(;g!==e;){if(g.x>=h&&g.x<=u&&g.y>=d&&g.y<=p&&Js(s,o,r,c,a,l,g.x,g.y)&&St(g.prev,g,g.next)>=0)return!1;g=g.next}return!0}function Kd(i,e,t,n){let s=i.prev,r=i,a=i.next;if(St(s,r,a)>=0)return!1;let o=s.x,c=r.x,l=a.x,h=s.y,d=r.y,u=a.y,p=Math.min(o,c,l),g=Math.min(h,d,u),v=Math.max(o,c,l),m=Math.max(h,d,u),f=Gl(p,g,e,t,n),S=Gl(v,m,e,t,n),R=i.prevZ,M=i.nextZ;for(;R&&R.z>=f&&M&&M.z<=S;){if(R.x>=p&&R.x<=v&&R.y>=g&&R.y<=m&&R!==s&&R!==a&&Js(o,h,c,d,l,u,R.x,R.y)&&St(R.prev,R,R.next)>=0||(R=R.prevZ,M.x>=p&&M.x<=v&&M.y>=g&&M.y<=m&&M!==s&&M!==a&&Js(o,h,c,d,l,u,M.x,M.y)&&St(M.prev,M,M.next)>=0))return!1;M=M.nextZ}for(;R&&R.z>=f;){if(R.x>=p&&R.x<=v&&R.y>=g&&R.y<=m&&R!==s&&R!==a&&Js(o,h,c,d,l,u,R.x,R.y)&&St(R.prev,R,R.next)>=0)return!1;R=R.prevZ}for(;M&&M.z<=S;){if(M.x>=p&&M.x<=v&&M.y>=g&&M.y<=m&&M!==s&&M!==a&&Js(o,h,c,d,l,u,M.x,M.y)&&St(M.prev,M,M.next)>=0)return!1;M=M.nextZ}return!0}function Qd(i,e){let t=i;do{let n=t.prev,s=t.next.next;!Es(n,s)&&Mu(n,t,t.next,s)&&br(n,s)&&br(s,n)&&(e.push(n.i,t.i,s.i),Er(t),Er(t.next),t=i=s),t=t.next}while(t!==i);return Ti(t)}function jd(i,e,t,n,s,r){let a=i;do{let o=a.next.next;for(;o!==a.prev;){if(a.i!==o.i&&cf(a,o)){let c=Su(a,o);a=Ti(a,a.next),c=Ti(c,c.next),Sr(a,e,t,n,s,r,0),Sr(c,e,t,n,s,r,0);return}o=o.next}a=a.next}while(a!==i)}function ef(i,e,t,n){let s=[];for(let r=0,a=e.length;r<a;r++){let o=e[r]*n,c=r<a-1?e[r+1]*n:i.length,l=yu(i,o,c,n,!1);l===l.next&&(l.steiner=!0),s.push(lf(l))}s.sort(tf);for(let r=0;r<s.length;r++)t=nf(s[r],t);return t}function tf(i,e){let t=i.x-e.x;if(t===0&&(t=i.y-e.y,t===0)){let n=(i.next.y-i.y)/(i.next.x-i.x),s=(e.next.y-e.y)/(e.next.x-e.x);t=n-s}return t}function nf(i,e){let t=sf(i,e);if(!t)return e;let n=Su(t,i);return Ti(n,n.next),Ti(t,t.next)}function sf(i,e){let t=e,n=i.x,s=i.y,r=-1/0,a;if(Es(i,t))return t;do{if(Es(i,t.next))return t.next;if(s<=t.y&&s>=t.next.y&&t.next.y!==t.y){let d=t.x+(s-t.y)*(t.next.x-t.x)/(t.next.y-t.y);if(d<=n&&d>r&&(r=d,a=t.x<t.next.x?t:t.next,d===n))return a}t=t.next}while(t!==e);if(!a)return null;let o=a,c=a.x,l=a.y,h=1/0;t=a;do{if(n>=t.x&&t.x>=c&&n!==t.x&&vu(s<l?n:r,s,c,l,s<l?r:n,s,t.x,t.y)){let d=Math.abs(s-t.y)/(n-t.x);br(t,i)&&(d<h||d===h&&(t.x>a.x||t.x===a.x&&rf(a,t)))&&(a=t,h=d)}t=t.next}while(t!==o);return a}function rf(i,e){return St(i.prev,i,e.prev)<0&&St(e.next,i,i.next)<0}function af(i,e,t,n){let s=i;do s.z===0&&(s.z=Gl(s.x,s.y,e,t,n)),s.prevZ=s.prev,s.nextZ=s.next,s=s.next;while(s!==i);s.prevZ.nextZ=null,s.prevZ=null,of(s)}function of(i){let e,t=1;do{let n=i,s;i=null;let r=null;for(e=0;n;){e++;let a=n,o=0;for(let l=0;l<t&&(o++,a=a.nextZ,!!a);l++);let c=t;for(;o>0||c>0&&a;)o!==0&&(c===0||!a||n.z<=a.z)?(s=n,n=n.nextZ,o--):(s=a,a=a.nextZ,c--),r?r.nextZ=s:i=s,s.prevZ=r,r=s;n=a}r.nextZ=null,t*=2}while(e>1);return i}function Gl(i,e,t,n,s){return i=(i-t)*s|0,e=(e-n)*s|0,i=(i|i<<8)&16711935,i=(i|i<<4)&252645135,i=(i|i<<2)&858993459,i=(i|i<<1)&1431655765,e=(e|e<<8)&16711935,e=(e|e<<4)&252645135,e=(e|e<<2)&858993459,e=(e|e<<1)&1431655765,i|e<<1}function lf(i){let e=i,t=i;do(e.x<t.x||e.x===t.x&&e.y<t.y)&&(t=e),e=e.next;while(e!==i);return t}function vu(i,e,t,n,s,r,a,o){return(s-a)*(e-o)>=(i-a)*(r-o)&&(i-a)*(n-o)>=(t-a)*(e-o)&&(t-a)*(r-o)>=(s-a)*(n-o)}function Js(i,e,t,n,s,r,a,o){return!(i===a&&e===o)&&vu(i,e,t,n,s,r,a,o)}function cf(i,e){return i.next.i!==e.i&&i.prev.i!==e.i&&!hf(i,e)&&(br(i,e)&&br(e,i)&&uf(i,e)&&(St(i.prev,i,e.prev)||St(i,e.prev,e))||Es(i,e)&&St(i.prev,i,i.next)>0&&St(e.prev,e,e.next)>0)}function St(i,e,t){return(e.y-i.y)*(t.x-e.x)-(e.x-i.x)*(t.y-e.y)}function Es(i,e){return i.x===e.x&&i.y===e.y}function Mu(i,e,t,n){let s=Sa(St(i,e,t)),r=Sa(St(i,e,n)),a=Sa(St(t,n,i)),o=Sa(St(t,n,e));return!!(s!==r&&a!==o||s===0&&Ma(i,t,e)||r===0&&Ma(i,n,e)||a===0&&Ma(t,i,n)||o===0&&Ma(t,e,n))}function Ma(i,e,t){return e.x<=Math.max(i.x,t.x)&&e.x>=Math.min(i.x,t.x)&&e.y<=Math.max(i.y,t.y)&&e.y>=Math.min(i.y,t.y)}function Sa(i){return i>0?1:i<0?-1:0}function hf(i,e){let t=i;do{if(t.i!==i.i&&t.next.i!==i.i&&t.i!==e.i&&t.next.i!==e.i&&Mu(t,t.next,i,e))return!0;t=t.next}while(t!==i);return!1}function br(i,e){return St(i.prev,i,i.next)<0?St(i,e,i.next)>=0&&St(i,i.prev,e)>=0:St(i,e,i.prev)<0||St(i,i.next,e)<0}function uf(i,e){let t=i,n=!1,s=(i.x+e.x)/2,r=(i.y+e.y)/2;do t.y>r!=t.next.y>r&&t.next.y!==t.y&&s<(t.next.x-t.x)*(r-t.y)/(t.next.y-t.y)+t.x&&(n=!n),t=t.next;while(t!==i);return n}function Su(i,e){let t=Wl(i.i,i.x,i.y),n=Wl(e.i,e.x,e.y),s=i.next,r=e.prev;return i.next=e,e.prev=i,t.next=s,s.prev=t,n.next=t,t.prev=n,r.next=n,n.prev=r,n}function Mh(i,e,t,n){let s=Wl(i,e,t);return n?(s.next=n.next,s.prev=n,n.next.prev=s,n.next=s):(s.prev=s,s.next=s),s}function Er(i){i.next.prev=i.prev,i.prev.next=i.next,i.prevZ&&(i.prevZ.nextZ=i.nextZ),i.nextZ&&(i.nextZ.prevZ=i.prevZ)}function Wl(i,e,t){return{i,x:e,y:t,prev:null,next:null,z:0,prevZ:null,nextZ:null,steiner:!1}}function df(i,e,t,n){let s=0;for(let r=e,a=t-n;r<t;r+=n)s+=(i[a]-i[r])*(i[r+1]+i[a+1]),a=r;return s}var Xl=class{static triangulate(e,t,n=2){return Jd(e,t,n)}},An=class i{static area(e){let t=e.length,n=0;for(let s=t-1,r=0;r<t;s=r++)n+=e[s].x*e[r].y-e[r].x*e[s].y;return n*.5}static isClockWise(e){return i.area(e)<0}static triangulateShape(e,t){let n=[],s=[],r=[];Sh(e),bh(n,e);let a=e.length;t.forEach(Sh);for(let c=0;c<t.length;c++)s.push(a),a+=t[c].length,bh(n,t[c]);let o=Xl.triangulate(n,s);for(let c=0;c<o.length;c+=3)r.push(o.slice(c,c+3));return r}};function Sh(i){let e=i.length;e>2&&i[e-1].equals(i[0])&&i.pop()}function bh(i,e){for(let t=0;t<e.length;t++)i.push(e[t].x),i.push(e[t].y)}var yn=class i extends Mt{constructor(e=new $n([new he(.5,.5),new he(-.5,.5),new he(-.5,-.5),new he(.5,-.5)]),t={}){super(),this.type="ExtrudeGeometry",this.parameters={shapes:e,options:t},e=Array.isArray(e)?e:[e];let n=this,s=[],r=[];for(let o=0,c=e.length;o<c;o++){let l=e[o];a(l)}this.setAttribute("position",new it(s,3)),this.setAttribute("uv",new it(r,2)),this.computeVertexNormals();function a(o){let c=[],l=t.curveSegments!==void 0?t.curveSegments:12,h=t.steps!==void 0?t.steps:1,d=t.depth!==void 0?t.depth:1,u=t.bevelEnabled!==void 0?t.bevelEnabled:!0,p=t.bevelThickness!==void 0?t.bevelThickness:.2,g=t.bevelSize!==void 0?t.bevelSize:p-.1,v=t.bevelOffset!==void 0?t.bevelOffset:0,m=t.bevelSegments!==void 0?t.bevelSegments:3,f=t.extrudePath,S=t.UVGenerator!==void 0?t.UVGenerator:ff,R,M=!1,T,w,P,x;if(f){R=f.getSpacedPoints(h),M=!0,u=!1;let te=f.isCatmullRomCurve3?f.closed:!1;T=f.computeFrenetFrames(h,te),w=new L,P=new L,x=new L}u||(m=0,p=0,g=0,v=0);let E=o.extractPoints(l),C=E.shape,U=E.holes;if(!An.isClockWise(C)){C=C.reverse();for(let te=0,ae=U.length;te<ae;te++){let le=U[te];An.isClockWise(le)&&(U[te]=le.reverse())}}function H(te){let le=10000000000000001e-36,oe=te[0];for(let ue=1;ue<=te.length;ue++){let Ue=ue%te.length,Ne=te[Ue],Ge=Ne.x-oe.x,qe=Ne.y-oe.y,D=Ge*Ge+qe*qe,ct=Math.max(Math.abs(Ne.x),Math.abs(Ne.y),Math.abs(oe.x),Math.abs(oe.y)),et=le*ct*ct;if(D<=et){te.splice(Ue,1),ue--;continue}oe=Ne}}H(C),U.forEach(H);let N=U.length,V=C;for(let te=0;te<N;te++){let ae=U[te];C=C.concat(ae)}function X(te,ae,le){return ae||We("ExtrudeGeometry: vec does not exist"),te.clone().addScaledVector(ae,le)}let W=C.length;function re(te,ae,le){let oe,ue,Ue,Ne=te.x-ae.x,Ge=te.y-ae.y,qe=le.x-te.x,D=le.y-te.y,ct=Ne*Ne+Ge*Ge,et=Ne*D-Ge*qe;if(Math.abs(et)>Number.EPSILON){let A=Math.sqrt(ct),_=Math.sqrt(qe*qe+D*D),z=ae.x-Ge/A,k=ae.y+Ne/A,Z=le.x-D/_,fe=le.y+qe/_,_e=((Z-z)*D-(fe-k)*qe)/(Ne*D-Ge*qe);oe=z+Ne*_e-te.x,ue=k+Ge*_e-te.y;let J=oe*oe+ue*ue;if(J<=2)return new he(oe,ue);Ue=Math.sqrt(J/2)}else{let A=!1;Ne>Number.EPSILON?qe>Number.EPSILON&&(A=!0):Ne<-Number.EPSILON?qe<-Number.EPSILON&&(A=!0):Math.sign(Ge)===Math.sign(D)&&(A=!0),A?(oe=-Ge,ue=Ne,Ue=Math.sqrt(ct)):(oe=Ne,ue=Ge,Ue=Math.sqrt(ct/2))}return new he(oe/Ue,ue/Ue)}let Y=[];for(let te=0,ae=V.length,le=ae-1,oe=te+1;te<ae;te++,le++,oe++)le===ae&&(le=0),oe===ae&&(oe=0),Y[te]=re(V[te],V[le],V[oe]);let Q=[],j,Re=Y.concat();for(let te=0,ae=N;te<ae;te++){let le=U[te];j=[];for(let oe=0,ue=le.length,Ue=ue-1,Ne=oe+1;oe<ue;oe++,Ue++,Ne++)Ue===ue&&(Ue=0),Ne===ue&&(Ne=0),j[oe]=re(le[oe],le[Ue],le[Ne]);Q.push(j),Re=Re.concat(j)}let Ce;if(m===0)Ce=An.triangulateShape(V,U);else{let te=[],ae=[];for(let le=0;le<m;le++){let oe=le/m,ue=p*Math.cos(oe*Math.PI/2),Ue=g*Math.sin(oe*Math.PI/2)+v;for(let Ne=0,Ge=V.length;Ne<Ge;Ne++){let qe=X(V[Ne],Y[Ne],Ue);ve(qe.x,qe.y,-ue),oe===0&&te.push(qe)}for(let Ne=0,Ge=N;Ne<Ge;Ne++){let qe=U[Ne];j=Q[Ne];let D=[];for(let ct=0,et=qe.length;ct<et;ct++){let A=X(qe[ct],j[ct],Ue);ve(A.x,A.y,-ue),oe===0&&D.push(A)}oe===0&&ae.push(D)}}Ce=An.triangulateShape(te,ae)}let lt=Ce.length,je=g+v;for(let te=0;te<W;te++){let ae=u?X(C[te],Re[te],je):C[te];M?(P.copy(T.normals[0]).multiplyScalar(ae.x),w.copy(T.binormals[0]).multiplyScalar(ae.y),x.copy(R[0]).add(P).add(w),ve(x.x,x.y,x.z)):ve(ae.x,ae.y,0)}for(let te=1;te<=h;te++)for(let ae=0;ae<W;ae++){let le=u?X(C[ae],Re[ae],je):C[ae];M?(P.copy(T.normals[te]).multiplyScalar(le.x),w.copy(T.binormals[te]).multiplyScalar(le.y),x.copy(R[te]).add(P).add(w),ve(x.x,x.y,x.z)):ve(le.x,le.y,d/h*te)}for(let te=m-1;te>=0;te--){let ae=te/m,le=p*Math.cos(ae*Math.PI/2),oe=g*Math.sin(ae*Math.PI/2)+v;for(let ue=0,Ue=V.length;ue<Ue;ue++){let Ne=X(V[ue],Y[ue],oe);ve(Ne.x,Ne.y,d+le)}for(let ue=0,Ue=U.length;ue<Ue;ue++){let Ne=U[ue];j=Q[ue];for(let Ge=0,qe=Ne.length;Ge<qe;Ge++){let D=X(Ne[Ge],j[Ge],oe);M?ve(D.x,D.y+R[h-1].y,R[h-1].x+le):ve(D.x,D.y,d+le)}}}ot(),q();function ot(){let te=s.length/3;if(u){let ae=0,le=W*ae;for(let oe=0;oe<lt;oe++){let ue=Ce[oe];Oe(ue[2]+le,ue[1]+le,ue[0]+le)}ae=h+m*2,le=W*ae;for(let oe=0;oe<lt;oe++){let ue=Ce[oe];Oe(ue[0]+le,ue[1]+le,ue[2]+le)}}else{for(let ae=0;ae<lt;ae++){let le=Ce[ae];Oe(le[2],le[1],le[0])}for(let ae=0;ae<lt;ae++){let le=Ce[ae];Oe(le[0]+W*h,le[1]+W*h,le[2]+W*h)}}n.addGroup(te,s.length/3-te,0)}function q(){let te=s.length/3,ae=0;ne(V,ae),ae+=V.length;for(let le=0,oe=U.length;le<oe;le++){let ue=U[le];ne(ue,ae),ae+=ue.length}n.addGroup(te,s.length/3-te,1)}function ne(te,ae){let le=te.length;for(;--le>=0;){let oe=le,ue=le-1;ue<0&&(ue=te.length-1);for(let Ue=0,Ne=h+m*2;Ue<Ne;Ue++){let Ge=W*Ue,qe=W*(Ue+1),D=ae+oe+Ge,ct=ae+ue+Ge,et=ae+ue+qe,A=ae+oe+qe;we(D,ct,et,A)}}}function ve(te,ae,le){c.push(te),c.push(ae),c.push(le)}function Oe(te,ae,le){He(te),He(ae),He(le);let oe=s.length/3,ue=S.generateTopUV(n,s,oe-3,oe-2,oe-1);rt(ue[0]),rt(ue[1]),rt(ue[2])}function we(te,ae,le,oe){He(te),He(ae),He(oe),He(ae),He(le),He(oe);let ue=s.length/3,Ue=S.generateSideWallUV(n,s,ue-6,ue-3,ue-2,ue-1);rt(Ue[0]),rt(Ue[1]),rt(Ue[3]),rt(Ue[1]),rt(Ue[2]),rt(Ue[3])}function He(te){s.push(c[te*3+0]),s.push(c[te*3+1]),s.push(c[te*3+2])}function rt(te){r.push(te.x),r.push(te.y)}}}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}toJSON(){let e=super.toJSON(),t=this.parameters.shapes,n=this.parameters.options;return pf(t,n,e)}static fromJSON(e,t){let n=[];for(let r=0,a=e.shapes.length;r<a;r++){let o=t[e.shapes[r]];n.push(o)}let s=e.options.extrudePath;return s!==void 0&&(e.options.extrudePath=new kl[s.type]().fromJSON(s)),new i(n,e.options)}},ff={generateTopUV:function(i,e,t,n,s){let r=e[t*3],a=e[t*3+1],o=e[n*3],c=e[n*3+1],l=e[s*3],h=e[s*3+1];return[new he(r,a),new he(o,c),new he(l,h)]},generateSideWallUV:function(i,e,t,n,s,r){let a=e[t*3],o=e[t*3+1],c=e[t*3+2],l=e[n*3],h=e[n*3+1],d=e[n*3+2],u=e[s*3],p=e[s*3+1],g=e[s*3+2],v=e[r*3],m=e[r*3+1],f=e[r*3+2];return Math.abs(o-h)<Math.abs(a-l)?[new he(a,1-c),new he(l,1-d),new he(u,1-g),new he(v,1-f)]:[new he(o,1-c),new he(h,1-d),new he(p,1-g),new he(m,1-f)]}};function pf(i,e,t){if(t.shapes=[],Array.isArray(i))for(let n=0,s=i.length;n<s;n++){let r=i[n];t.shapes.push(r.uuid)}else t.shapes.push(i.uuid);return t.options=Object.assign({},e),e.extrudePath!==void 0&&(t.options.extrudePath=e.extrudePath.toJSON()),t}var Kn=class i extends Mt{constructor(e=1,t=1,n=1,s=1){super(),this.type="PlaneGeometry",this.parameters={width:e,height:t,widthSegments:n,heightSegments:s};let r=e/2,a=t/2,o=Math.floor(n),c=Math.floor(s),l=o+1,h=c+1,d=e/o,u=t/c,p=[],g=[],v=[],m=[];for(let f=0;f<h;f++){let S=f*u-a;for(let R=0;R<l;R++){let M=R*d-r;g.push(M,-S,0),v.push(0,0,1),m.push(R/o),m.push(1-f/c)}}for(let f=0;f<c;f++)for(let S=0;S<o;S++){let R=S+l*f,M=S+l*(f+1),T=S+1+l*(f+1),w=S+1+l*f;p.push(R,M,w),p.push(M,T,w)}this.setIndex(p),this.setAttribute("position",new it(g,3)),this.setAttribute("normal",new it(v,3)),this.setAttribute("uv",new it(m,2))}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}static fromJSON(e){return new i(e.width,e.height,e.widthSegments,e.heightSegments)}};var Tr=class i extends Mt{constructor(e=new $n([new he(0,.5),new he(-.5,-.5),new he(.5,-.5)]),t=12){super(),this.type="ShapeGeometry",this.parameters={shapes:e,curveSegments:t};let n=[],s=[],r=[],a=[],o=0,c=0;if(Array.isArray(e)===!1)l(e);else for(let h=0;h<e.length;h++)l(e[h]),this.addGroup(o,c,h),o+=c,c=0;this.setIndex(n),this.setAttribute("position",new it(s,3)),this.setAttribute("normal",new it(r,3)),this.setAttribute("uv",new it(a,2));function l(h){let d=s.length/3,u=h.extractPoints(t),p=u.shape,g=u.holes;An.isClockWise(p)===!1&&(p=p.reverse());for(let m=0,f=g.length;m<f;m++){let S=g[m];An.isClockWise(S)===!0&&(g[m]=S.reverse())}let v=An.triangulateShape(p,g);for(let m=0,f=g.length;m<f;m++){let S=g[m];p=p.concat(S)}for(let m=0,f=p.length;m<f;m++){let S=p[m];s.push(S.x,S.y,0),r.push(0,0,1),a.push(S.x,S.y)}for(let m=0,f=v.length;m<f;m++){let S=v[m],R=S[0]+d,M=S[1]+d,T=S[2]+d;n.push(R,M,T),c+=3}}}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}toJSON(){let e=super.toJSON(),t=this.parameters.shapes;return mf(t,e)}static fromJSON(e,t){let n=[];for(let s=0,r=e.shapes.length;s<r;s++){let a=t[e.shapes[s]];n.push(a)}return new i(n,e.curveSegments)}};function mf(i,e){if(e.shapes=[],Array.isArray(i))for(let t=0,n=i.length;t<n;t++){let s=i[t];e.shapes.push(s.uuid)}else e.shapes.push(i.uuid);return e}var li=class i extends Mt{constructor(e=1,t=32,n=16,s=0,r=Math.PI*2,a=0,o=Math.PI){super(),this.type="SphereGeometry",this.parameters={radius:e,widthSegments:t,heightSegments:n,phiStart:s,phiLength:r,thetaStart:a,thetaLength:o},t=Math.max(3,Math.floor(t)),n=Math.max(2,Math.floor(n));let c=Math.min(a+o,Math.PI),l=0,h=[],d=new L,u=new L,p=[],g=[],v=[],m=[];for(let f=0;f<=n;f++){let S=[],R=f/n,M=a+R*o,T=e*Math.cos(M),w=Math.sqrt(e*e-T*T),P=0;f===0&&a===0?P=.5/t:f===n&&c===Math.PI&&(P=-.5/t);for(let x=0;x<=t;x++){let E=x/t,C=s+E*r;d.x=-w*Math.cos(C),d.y=T,d.z=w*Math.sin(C),g.push(d.x,d.y,d.z),u.copy(d).normalize(),v.push(u.x,u.y,u.z),m.push(E+P,1-R),S.push(l++)}h.push(S)}for(let f=0;f<n;f++)for(let S=0;S<t;S++){let R=h[f][S+1],M=h[f][S],T=h[f+1][S],w=h[f+1][S+1];(f!==0||a>0)&&p.push(R,M,w),(f!==n-1||c<Math.PI)&&p.push(M,T,w)}this.setIndex(p),this.setAttribute("position",new it(g,3)),this.setAttribute("normal",new it(v,3)),this.setAttribute("uv",new it(m,2))}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}static fromJSON(e){return new i(e.radius,e.widthSegments,e.heightSegments,e.phiStart,e.phiLength,e.thetaStart,e.thetaLength)}};var wi=class i extends Mt{constructor(e=1,t=.4,n=12,s=48,r=Math.PI*2,a=0,o=Math.PI*2){super(),this.type="TorusGeometry",this.parameters={radius:e,tube:t,radialSegments:n,tubularSegments:s,arc:r,thetaStart:a,thetaLength:o},n=Math.floor(n),s=Math.floor(s);let c=[],l=[],h=[],d=[],u=new L,p=new L,g=new L;for(let v=0;v<=n;v++){let m=a+v/n*o;for(let f=0;f<=s;f++){let S=f/s*r;p.x=(e+t*Math.cos(m))*Math.cos(S),p.y=(e+t*Math.cos(m))*Math.sin(S),p.z=t*Math.sin(m),l.push(p.x,p.y,p.z),u.x=e*Math.cos(S),u.y=e*Math.sin(S),g.subVectors(p,u).normalize(),h.push(g.x,g.y,g.z),d.push(f/s),d.push(v/n)}}for(let v=1;v<=n;v++)for(let m=1;m<=s;m++){let f=(s+1)*v+m-1,S=(s+1)*(v-1)+m-1,R=(s+1)*(v-1)+m,M=(s+1)*v+m;c.push(f,S,M),c.push(S,R,M)}this.setIndex(c),this.setAttribute("position",new it(l,3)),this.setAttribute("normal",new it(h,3)),this.setAttribute("uv",new it(d,2))}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}static fromJSON(e){return new i(e.radius,e.tube,e.radialSegments,e.tubularSegments,e.arc,e.thetaStart,e.thetaLength)}};var wr=class extends Mt{constructor(e=null){if(super(),this.type="WireframeGeometry",this.parameters={geometry:e},e!==null){let t=[],n=new Set,s=new L,r=new L;if(e.index!==null){let a=e.attributes.position,o=e.index,c=e.groups;c.length===0&&(c=[{start:0,count:o.count,materialIndex:0}]);for(let l=0,h=c.length;l<h;++l){let d=c[l],u=d.start,p=d.count;for(let g=u,v=u+p;g<v;g+=3)for(let m=0;m<3;m++){let f=o.getX(g+m),S=o.getX(g+(m+1)%3);s.fromBufferAttribute(a,f),r.fromBufferAttribute(a,S),Eh(s,r,n)===!0&&(t.push(s.x,s.y,s.z),t.push(r.x,r.y,r.z))}}}else{let a=e.attributes.position;for(let o=0,c=a.count/3;o<c;o++)for(let l=0;l<3;l++){let h=3*o+l,d=3*o+(l+1)%3;s.fromBufferAttribute(a,h),r.fromBufferAttribute(a,d),Eh(s,r,n)===!0&&(t.push(s.x,s.y,s.z),t.push(r.x,r.y,r.z))}}this.setAttribute("position",new it(t,3))}}copy(e){return super.copy(e),this.parameters=Object.assign({},e.parameters),this}};function Eh(i,e,t){let n=`${i.x},${i.y},${i.z}-${e.x},${e.y},${e.z}`,s=`${e.x},${e.y},${e.z}-${i.x},${i.y},${i.z}`;return t.has(n)===!0||t.has(s)===!0?!1:(t.add(n),t.add(s),!0)}function Ci(i){let e={};for(let t in i){e[t]={};for(let n in i[t]){let s=i[t][n];if(Th(s))s.isRenderTargetTexture?(ke("UniformsUtils: Textures of render targets cannot be cloned via cloneUniforms() or mergeUniforms()."),e[t][n]=null):e[t][n]=s.clone();else if(Array.isArray(s))if(Th(s[0])){let r=[];for(let a=0,o=s.length;a<o;a++)r[a]=s[a].clone();e[t][n]=r}else e[t][n]=s.slice();else e[t][n]=s}}return e}function kt(i){let e={};for(let t=0;t<i.length;t++){let n=Ci(i[t]);for(let s in n)e[s]=n[s]}return e}function Th(i){return i&&(i.isColor||i.isMatrix3||i.isMatrix4||i.isVector2||i.isVector3||i.isVector4||i.isTexture||i.isQuaternion)}function gf(i){let e=[];for(let t=0;t<i.length;t++)e.push(i[t].clone());return e}function vc(i){let e=i.getRenderTarget();return e===null?i.outputColorSpace:e.isXRRenderTarget===!0?e.texture.colorSpace:st.workingColorSpace}var bu={clone:Ci,merge:kt},_f=`void main() {
	gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );
}`,xf=`void main() {
	gl_FragColor = vec4( 1.0, 0.0, 0.0, 1.0 );
}`,en=class extends xn{constructor(e){super(),this.isShaderMaterial=!0,this.type="ShaderMaterial",this.defines={},this.uniforms={},this.uniformsGroups=[],this.vertexShader=_f,this.fragmentShader=xf,this.linewidth=1,this.wireframe=!1,this.wireframeLinewidth=1,this.fog=!1,this.lights=!1,this.clipping=!1,this.forceSinglePass=!0,this.extensions={clipCullDistance:!1,multiDraw:!1},this.defaultAttributeValues={color:[1,1,1],uv:[0,0],uv1:[0,0]},this.index0AttributeName=void 0,this.uniformsNeedUpdate=!1,this.glslVersion=null,e!==void 0&&this.setValues(e)}copy(e){return super.copy(e),this.fragmentShader=e.fragmentShader,this.vertexShader=e.vertexShader,this.uniforms=Ci(e.uniforms),this.uniformsGroups=gf(e.uniformsGroups),this.defines=Object.assign({},e.defines),this.wireframe=e.wireframe,this.wireframeLinewidth=e.wireframeLinewidth,this.fog=e.fog,this.lights=e.lights,this.clipping=e.clipping,this.extensions=Object.assign({},e.extensions),this.glslVersion=e.glslVersion,this.defaultAttributeValues=Object.assign({},e.defaultAttributeValues),this.index0AttributeName=e.index0AttributeName,this.uniformsNeedUpdate=e.uniformsNeedUpdate,this}toJSON(e){let t=super.toJSON(e);t.glslVersion=this.glslVersion,t.uniforms={};for(let s in this.uniforms){let a=this.uniforms[s].value;a&&a.isTexture?t.uniforms[s]={type:"t",value:a.toJSON(e).uuid}:a&&a.isColor?t.uniforms[s]={type:"c",value:a.getHex()}:a&&a.isVector2?t.uniforms[s]={type:"v2",value:a.toArray()}:a&&a.isVector3?t.uniforms[s]={type:"v3",value:a.toArray()}:a&&a.isVector4?t.uniforms[s]={type:"v4",value:a.toArray()}:a&&a.isMatrix3?t.uniforms[s]={type:"m3",value:a.toArray()}:a&&a.isMatrix4?t.uniforms[s]={type:"m4",value:a.toArray()}:t.uniforms[s]={value:a}}Object.keys(this.defines).length>0&&(t.defines=this.defines),t.vertexShader=this.vertexShader,t.fragmentShader=this.fragmentShader,t.lights=this.lights,t.clipping=this.clipping;let n={};for(let s in this.extensions)this.extensions[s]===!0&&(n[s]=!0);return Object.keys(n).length>0&&(t.extensions=n),t}fromJSON(e,t){if(super.fromJSON(e,t),e.uniforms!==void 0)for(let n in e.uniforms){let s=e.uniforms[n];switch(this.uniforms[n]={},s.type){case"t":this.uniforms[n].value=t[s.value]||null;break;case"c":this.uniforms[n].value=new Fe().setHex(s.value);break;case"v2":this.uniforms[n].value=new he().fromArray(s.value);break;case"v3":this.uniforms[n].value=new L().fromArray(s.value);break;case"v4":this.uniforms[n].value=new vt().fromArray(s.value);break;case"m3":this.uniforms[n].value=new Je().fromArray(s.value);break;case"m4":this.uniforms[n].value=new ut().fromArray(s.value);break;default:this.uniforms[n].value=s.value}}if(e.defines!==void 0&&(this.defines=e.defines),e.vertexShader!==void 0&&(this.vertexShader=e.vertexShader),e.fragmentShader!==void 0&&(this.fragmentShader=e.fragmentShader),e.glslVersion!==void 0&&(this.glslVersion=e.glslVersion),e.extensions!==void 0)for(let n in e.extensions)this.extensions[n]=e.extensions[n];return e.lights!==void 0&&(this.lights=e.lights),e.clipping!==void 0&&(this.clipping=e.clipping),this}},Ka=class extends en{constructor(e){super(e),this.isRawShaderMaterial=!0,this.type="RawShaderMaterial"}},qt=class extends xn{constructor(e){super(),this.isMeshStandardMaterial=!0,this.type="MeshStandardMaterial",this.defines={STANDARD:""},this.color=new Fe(16777215),this.roughness=1,this.metalness=0,this.map=null,this.lightMap=null,this.lightMapIntensity=1,this.aoMap=null,this.aoMapIntensity=1,this.emissive=new Fe(0),this.emissiveIntensity=1,this.emissiveMap=null,this.bumpMap=null,this.bumpScale=1,this.normalMap=null,this.normalMapType=jo,this.normalScale=new he(1,1),this.displacementMap=null,this.displacementScale=1,this.displacementBias=0,this.roughnessMap=null,this.metalnessMap=null,this.alphaMap=null,this.envMap=null,this.envMapRotation=new _n,this.envMapIntensity=1,this.wireframe=!1,this.wireframeLinewidth=1,this.wireframeLinecap="round",this.wireframeLinejoin="round",this.flatShading=!1,this.fog=!0,this.setValues(e)}copy(e){return super.copy(e),this.defines={STANDARD:""},this.color.copy(e.color),this.roughness=e.roughness,this.metalness=e.metalness,this.map=e.map,this.lightMap=e.lightMap,this.lightMapIntensity=e.lightMapIntensity,this.aoMap=e.aoMap,this.aoMapIntensity=e.aoMapIntensity,this.emissive.copy(e.emissive),this.emissiveMap=e.emissiveMap,this.emissiveIntensity=e.emissiveIntensity,this.bumpMap=e.bumpMap,this.bumpScale=e.bumpScale,this.normalMap=e.normalMap,this.normalMapType=e.normalMapType,this.normalScale.copy(e.normalScale),this.displacementMap=e.displacementMap,this.displacementScale=e.displacementScale,this.displacementBias=e.displacementBias,this.roughnessMap=e.roughnessMap,this.metalnessMap=e.metalnessMap,this.alphaMap=e.alphaMap,this.envMap=e.envMap,this.envMapRotation.copy(e.envMapRotation),this.envMapIntensity=e.envMapIntensity,this.wireframe=e.wireframe,this.wireframeLinewidth=e.wireframeLinewidth,this.wireframeLinecap=e.wireframeLinecap,this.wireframeLinejoin=e.wireframeLinejoin,this.flatShading=e.flatShading,this.fog=e.fog,this}},on=class extends qt{constructor(e){super(),this.isMeshPhysicalMaterial=!0,this.defines={STANDARD:"",PHYSICAL:""},this.type="MeshPhysicalMaterial",this.anisotropyRotation=0,this.anisotropyMap=null,this.clearcoatMap=null,this.clearcoatRoughness=0,this.clearcoatRoughnessMap=null,this.clearcoatNormalScale=new he(1,1),this.clearcoatNormalMap=null,this.ior=1.5,Object.defineProperty(this,"reflectivity",{get:function(){return nt(2.5*(this.ior-1)/(this.ior+1),0,1)},set:function(t){this.ior=(1+.4*t)/(1-.4*t)}}),this.iridescenceMap=null,this.iridescenceIOR=1.3,this.iridescenceThicknessRange=[100,400],this.iridescenceThicknessMap=null,this.sheenColor=new Fe(0),this.sheenColorMap=null,this.sheenRoughness=1,this.sheenRoughnessMap=null,this.transmissionMap=null,this.thickness=0,this.thicknessMap=null,this.attenuationDistance=1/0,this.attenuationColor=new Fe(1,1,1),this.specularIntensity=1,this.specularIntensityMap=null,this.specularColor=new Fe(1,1,1),this.specularColorMap=null,this._anisotropy=0,this._clearcoat=0,this._dispersion=0,this._iridescence=0,this._retroreflectivity=0,this._sheen=0,this._transmission=0,this.setValues(e)}get anisotropy(){return this._anisotropy}set anisotropy(e){this._anisotropy>0!=e>0&&this.version++,this._anisotropy=e}get clearcoat(){return this._clearcoat}set clearcoat(e){this._clearcoat>0!=e>0&&this.version++,this._clearcoat=e}get iridescence(){return this._iridescence}set iridescence(e){this._iridescence>0!=e>0&&this.version++,this._iridescence=e}get dispersion(){return this._dispersion}set dispersion(e){this._dispersion>0!=e>0&&this.version++,this._dispersion=e}get retroreflectivity(){return this._retroreflectivity}set retroreflectivity(e){this._retroreflectivity>0!=e>0&&this.version++,this._retroreflectivity=e}get sheen(){return this._sheen}set sheen(e){this._sheen>0!=e>0&&this.version++,this._sheen=e}get transmission(){return this._transmission}set transmission(e){this._transmission>0!=e>0&&this.version++,this._transmission=e}copy(e){return super.copy(e),this.defines={STANDARD:"",PHYSICAL:""},this.anisotropy=e.anisotropy,this.anisotropyRotation=e.anisotropyRotation,this.anisotropyMap=e.anisotropyMap,this.clearcoat=e.clearcoat,this.clearcoatMap=e.clearcoatMap,this.clearcoatRoughness=e.clearcoatRoughness,this.clearcoatRoughnessMap=e.clearcoatRoughnessMap,this.clearcoatNormalMap=e.clearcoatNormalMap,this.clearcoatNormalScale.copy(e.clearcoatNormalScale),this.dispersion=e.dispersion,this.ior=e.ior,this.iridescence=e.iridescence,this.iridescenceMap=e.iridescenceMap,this.iridescenceIOR=e.iridescenceIOR,this.iridescenceThicknessRange=[...e.iridescenceThicknessRange],this.iridescenceThicknessMap=e.iridescenceThicknessMap,this.retroreflectivity=e.retroreflectivity,this.sheen=e.sheen,this.sheenColor.copy(e.sheenColor),this.sheenColorMap=e.sheenColorMap,this.sheenRoughness=e.sheenRoughness,this.sheenRoughnessMap=e.sheenRoughnessMap,this.transmission=e.transmission,this.transmissionMap=e.transmissionMap,this.thickness=e.thickness,this.thicknessMap=e.thicknessMap,this.attenuationDistance=e.attenuationDistance,this.attenuationColor.copy(e.attenuationColor),this.specularIntensity=e.specularIntensity,this.specularIntensityMap=e.specularIntensityMap,this.specularColor.copy(e.specularColor),this.specularColorMap=e.specularColorMap,this}};var Qa=class extends xn{constructor(e){super(),this.isMeshDepthMaterial=!0,this.type="MeshDepthMaterial",this.depthPacking=su,this.map=null,this.alphaMap=null,this.displacementMap=null,this.displacementScale=1,this.displacementBias=0,this.wireframe=!1,this.wireframeLinewidth=1,this.setValues(e)}copy(e){return super.copy(e),this.depthPacking=e.depthPacking,this.map=e.map,this.alphaMap=e.alphaMap,this.displacementMap=e.displacementMap,this.displacementScale=e.displacementScale,this.displacementBias=e.displacementBias,this.wireframe=e.wireframe,this.wireframeLinewidth=e.wireframeLinewidth,this}},ja=class extends xn{constructor(e){super(),this.isMeshDistanceMaterial=!0,this.type="MeshDistanceMaterial",this.map=null,this.alphaMap=null,this.displacementMap=null,this.displacementScale=1,this.displacementBias=0,this.setValues(e)}copy(e){return super.copy(e),this.map=e.map,this.alphaMap=e.alphaMap,this.displacementMap=e.displacementMap,this.displacementScale=e.displacementScale,this.displacementBias=e.displacementBias,this}};function is(i,e){return!i||i.constructor===e?i:typeof e.BYTES_PER_ELEMENT=="number"?new e(i):Array.prototype.slice.call(i)}function Nl(i){return i!==void 0&&i.inTangents!==void 0&&i.outTangents!==void 0}var ci=class{constructor(e,t,n,s){this.parameterPositions=e,this._cachedIndex=0,this.resultBuffer=s!==void 0?s:new t.constructor(n),this.sampleValues=t,this.valueSize=n,this.settings=null,this.DefaultSettings_={}}evaluate(e){let t=this.parameterPositions,n=this._cachedIndex,s=t[n],r=t[n-1];n:{e:{let a;t:{i:if(!(e<s)){for(let o=n+2;;){if(s===void 0){if(e<r)break i;return n=t.length,this._cachedIndex=n,this.copySampleValue_(n-1)}if(n===o)break;if(r=s,s=t[++n],e<s)break e}a=t.length;break t}if(!(e>=r)){let o=t[1];e<o&&(n=2,r=o);for(let c=n-2;;){if(r===void 0)return this._cachedIndex=0,this.copySampleValue_(0);if(n===c)break;if(s=r,r=t[--n-1],e>=r)break e}a=n,n=0;break t}break n}for(;n<a;){let o=n+a>>>1;e<t[o]?a=o:n=o+1}if(s=t[n],r=t[n-1],r===void 0)return this._cachedIndex=0,this.copySampleValue_(0);if(s===void 0)return n=t.length,this._cachedIndex=n,this.copySampleValue_(n-1)}this._cachedIndex=n,this.intervalChanged_(n,r,s)}return this.interpolate_(n,r,e,s)}getSettings_(){return this.settings||this.DefaultSettings_}copySampleValue_(e){let t=this.resultBuffer,n=this.sampleValues,s=this.valueSize,r=e*s;for(let a=0;a!==s;++a)t[a]=n[r+a];return t}interpolate_(){throw new Error("THREE.Interpolant: Call to abstract method.")}intervalChanged_(){}},eo=class extends ci{constructor(e,t,n,s){super(e,t,n,s),this._weightPrev=-0,this._offsetPrev=-0,this._weightNext=-0,this._offsetNext=-0,this.DefaultSettings_={endingStart:Bl,endingEnd:Bl}}intervalChanged_(e,t,n){let s=this.parameterPositions,r=e-2,a=e+1,o=s[r],c=s[a];if(o===void 0)switch(this.getSettings_().endingStart){case zl:r=e,o=2*t-n;break;case Hl:r=s.length-2,o=t+s[r]-s[r+1];break;default:r=e,o=n}if(c===void 0)switch(this.getSettings_().endingEnd){case zl:a=e,c=2*n-t;break;case Hl:a=1,c=n+s[1]-s[0];break;default:a=e-1,c=t}let l=(n-t)*.5,h=this.valueSize;this._weightPrev=l/(t-o),this._weightNext=l/(c-n),this._offsetPrev=r*h,this._offsetNext=a*h}interpolate_(e,t,n,s){let r=this.resultBuffer,a=this.sampleValues,o=this.valueSize,c=e*o,l=c-o,h=this._offsetPrev,d=this._offsetNext,u=this._weightPrev,p=this._weightNext,g=(n-t)/(s-t),v=g*g,m=v*g,f=-u*m+2*u*v-u*g,S=(1+u)*m+(-1.5-2*u)*v+(-.5+u)*g+1,R=(-1-p)*m+(1.5+p)*v+.5*g,M=p*m-p*v;for(let T=0;T!==o;++T)r[T]=f*a[h+T]+S*a[l+T]+R*a[c+T]+M*a[d+T];return r}},to=class extends ci{constructor(e,t,n,s){super(e,t,n,s)}interpolate_(e,t,n,s){let r=this.resultBuffer,a=this.sampleValues,o=this.valueSize,c=e*o,l=c-o,h=(n-t)/(s-t),d=1-h;for(let u=0;u!==o;++u)r[u]=a[l+u]*d+a[c+u]*h;return r}},no=class extends ci{constructor(e,t,n,s){super(e,t,n,s)}interpolate_(e){return this.copySampleValue_(e-1)}},io=class extends ci{interpolate_(e,t,n,s){let r=this.resultBuffer,a=this.sampleValues,o=this.valueSize,c=e*o,l=c-o,h=this.inTangents,d=this.outTangents;if(!h||!d){let g=(n-t)/(s-t),v=1-g;for(let m=0;m!==o;++m)r[m]=a[l+m]*v+a[c+m]*g;return r}let u=o*2,p=e-1;for(let g=0;g!==o;++g){let v=a[l+g],m=a[c+g],f=p*u+g*2,S=d[f],R=d[f+1],M=e*u+g*2,T=h[M],w=h[M+1],P=vf(n,t,S,T,s);r[g]=Eu(P,v,R,w,m)}return r}};function Eu(i,e,t,n,s){let r=1-i;return r*r*r*e+3*r*r*i*t+3*r*i*i*n+i*i*i*s}function yf(i,e,t,n,s){let r=1-i;return 3*r*r*(t-e)+6*r*i*(n-t)+3*i*i*(s-n)}function vf(i,e,t,n,s){let r=(i-e)/(s-e);for(let a=0;a<8;a++){let o=Eu(r,e,t,n,s)-i;if(Math.abs(o)<1e-10)break;let c=yf(r,e,t,n,s);if(Math.abs(c)<1e-10)break;r=Math.max(0,Math.min(1,r-o/c))}return r}var tn=class{constructor(e,t,n,s){if(e===void 0)throw new Error("THREE.KeyframeTrack: track name is undefined");if(t===void 0||t.length===0)throw new Error("THREE.KeyframeTrack: no keyframes in track named "+e);this.name=e,this.times=is(t,this.TimeBufferType),this.values=is(n,this.ValueBufferType),this.setInterpolation(s||this.DefaultInterpolation)}static toJSON(e){let t=e.constructor,n;if(t.toJSON!==this.toJSON)n=t.toJSON(e);else{n={name:e.name,times:is(e.times,Array),values:is(e.values,Array)};let s=e.getInterpolation();s!==e.DefaultInterpolation&&(n.interpolation=s),Nl(e.settings)&&(n.settings={inTangents:is(e.settings.inTangents,Array),outTangents:is(e.settings.outTangents,Array)})}return n.type=e.ValueTypeName,n}InterpolantFactoryMethodDiscrete(e){return new no(this.times,this.values,this.getValueSize(),e)}InterpolantFactoryMethodLinear(e){return new to(this.times,this.values,this.getValueSize(),e)}InterpolantFactoryMethodSmooth(e){return new eo(this.times,this.values,this.getValueSize(),e)}InterpolantFactoryMethodBezier(e){let t=new io(this.times,this.values,this.getValueSize(),e);return this.settings&&(t.inTangents=this.settings.inTangents,t.outTangents=this.settings.outTangents),t}setInterpolation(e){let t;switch(e){case er:t=this.InterpolantFactoryMethodDiscrete;break;case Oa:t=this.InterpolantFactoryMethodLinear;break;case Ta:t=this.InterpolantFactoryMethodSmooth;break;case Ol:t=this.InterpolantFactoryMethodBezier;break}if(t===void 0){let n="unsupported interpolation for "+this.ValueTypeName+" keyframe track named "+this.name;if(this.createInterpolant===void 0)if(e!==this.DefaultInterpolation)this.setInterpolation(this.DefaultInterpolation);else throw new Error(n);return ke("KeyframeTrack:",n),this}return this.createInterpolant=t,this}getInterpolation(){switch(this.createInterpolant){case this.InterpolantFactoryMethodDiscrete:return er;case this.InterpolantFactoryMethodLinear:return Oa;case this.InterpolantFactoryMethodSmooth:return Ta;case this.InterpolantFactoryMethodBezier:return Ol}}getValueSize(){return this.values.length/this.times.length}shift(e){if(e!==0){let t=this.times;for(let n=0,s=t.length;n!==s;++n)t[n]+=e}return this}scale(e){if(e!==1){let t=this.times;for(let n=0,s=t.length;n!==s;++n)t[n]*=e;Nl(this.settings)&&(wh(this.settings.inTangents,e),wh(this.settings.outTangents,e))}return this}trim(e,t){let n=this.times,s=n.length,r=0,a=s-1;for(;r!==s&&n[r]<e;)++r;for(;a!==-1&&n[a]>t;)--a;if(++a,r!==0||a!==s){r>=a&&(a=Math.max(a,1),r=a-1);let o=this.getValueSize();this.times=n.slice(r,a),this.values=this.values.slice(r*o,a*o)}return this}validate(){let e=!0,t=this.getValueSize();t-Math.floor(t)!==0&&(We("KeyframeTrack: Invalid value size in track.",this),e=!1);let n=this.times,s=this.values,r=n.length;r===0&&(We("KeyframeTrack: Track is empty.",this),e=!1);let a=null;for(let o=0;o!==r;o++){let c=n[o];if(typeof c=="number"&&isNaN(c)){We("KeyframeTrack: Time is not a valid number.",this,o,c),e=!1;break}if(a!==null&&a>c){We("KeyframeTrack: Out of order keys.",this,o,c,a),e=!1;break}a=c}if(s!==void 0&&ad(s))for(let o=0,c=s.length;o!==c;++o){let l=s[o];if(isNaN(l)){We("KeyframeTrack: Value is not a valid number.",this,o,l),e=!1;break}}return e}optimize(){let e=this.times.slice(),t=this.values.slice(),n=this.getValueSize(),s=this.getInterpolation()===Ta,r=e.length-1,a=1;for(let o=1;o<r;++o){let c=!1,l=e[o],h=e[o+1];if(l!==h&&(o!==1||l!==e[0]))if(s)c=!0;else{let d=o*n,u=d-n,p=d+n;for(let g=0;g!==n;++g){let v=t[d+g];if(v!==t[u+g]||v!==t[p+g]){c=!0;break}}}if(c){if(o!==a){e[a]=e[o];let d=o*n,u=a*n;for(let p=0;p!==n;++p)t[u+p]=t[d+p]}++a}}if(r>0){e[a]=e[r];for(let o=r*n,c=a*n,l=0;l!==n;++l)t[c+l]=t[o+l];++a}return a!==e.length?(this.times=e.slice(0,a),this.values=t.slice(0,a*n)):(this.times=e,this.values=t),this}clone(){let e=this.times.slice(),t=this.values.slice(),n=this.constructor,s=new n(this.name,e,t);return s.createInterpolant=this.createInterpolant,Nl(this.settings)&&(s.settings={inTangents:this.settings.inTangents.slice(),outTangents:this.settings.outTangents.slice()}),s}};function wh(i,e){for(let t=0,n=i.length;t!==n;t+=2)i[t]*=e}tn.prototype.ValueTypeName="";tn.prototype.TimeBufferType=Float32Array;tn.prototype.ValueBufferType=Float32Array;tn.prototype.DefaultInterpolation=Oa;var hi=class extends tn{constructor(e,t,n){super(e,t,n)}};hi.prototype.ValueTypeName="bool";hi.prototype.ValueBufferType=Array;hi.prototype.DefaultInterpolation=er;hi.prototype.InterpolantFactoryMethodLinear=void 0;hi.prototype.InterpolantFactoryMethodSmooth=void 0;var so=class extends tn{constructor(e,t,n,s){super(e,t,n,s)}};so.prototype.ValueTypeName="color";var ro=class extends tn{constructor(e,t,n,s){super(e,t,n,s)}};ro.prototype.ValueTypeName="number";var ao=class extends ci{constructor(e,t,n,s){super(e,t,n,s)}interpolate_(e,t,n,s){let r=this.resultBuffer,a=this.sampleValues,o=this.valueSize,c=(n-t)/(s-t),l=e*o;for(let h=l+o;l!==h;l+=4)Pn.slerpFlat(r,0,a,l-o,a,l,c);return r}},Ar=class extends tn{constructor(e,t,n,s){super(e,t,n,s)}InterpolantFactoryMethodLinear(e){return new ao(this.times,this.values,this.getValueSize(),e)}};Ar.prototype.ValueTypeName="quaternion";Ar.prototype.InterpolantFactoryMethodSmooth=void 0;var ui=class extends tn{constructor(e,t,n){super(e,t,n)}};ui.prototype.ValueTypeName="string";ui.prototype.ValueBufferType=Array;ui.prototype.DefaultInterpolation=er;ui.prototype.InterpolantFactoryMethodLinear=void 0;ui.prototype.InterpolantFactoryMethodSmooth=void 0;var oo=class extends tn{constructor(e,t,n,s){super(e,t,n,s)}};oo.prototype.ValueTypeName="vector";var Aa={enabled:!1,files:{},add:function(i,e){this.enabled!==!1&&(Ah(i)||(this.files[i]=e))},get:function(i){if(this.enabled!==!1&&!Ah(i))return this.files[i]},remove:function(i){delete this.files[i]},clear:function(){this.files={}}};function Ah(i){try{let e=i.slice(i.indexOf(":")+1);return new URL(e).protocol==="blob:"}catch{return!1}}var lo=class{constructor(e,t,n){let s=this,r=!1,a=0,o=0,c,l=[];this.onStart=void 0,this.onLoad=e,this.onProgress=t,this.onError=n,this._abortController=null,this.itemStart=function(h){o++,r===!1&&s.onStart!==void 0&&s.onStart(h,a,o),r=!0},this.itemEnd=function(h){a++,s.onProgress!==void 0&&s.onProgress(h,a,o),a===o&&(r=!1,s.onLoad!==void 0&&s.onLoad())},this.itemError=function(h){s.onError!==void 0&&s.onError(h)},this.resolveURL=function(h){return h=h.normalize("NFC"),c?c(h):h},this.setURLModifier=function(h){return c=h,this},this.addHandler=function(h,d){return l.push(h,d),this},this.removeHandler=function(h){let d=l.indexOf(h);return d!==-1&&l.splice(d,2),this},this.getHandler=function(h){for(let d=0,u=l.length;d<u;d+=2){let p=l[d],g=l[d+1];if(p.global&&(p.lastIndex=0),p.test(h))return g}return null},this.abort=function(){return this.abortController.abort(),this._abortController=null,this}}get abortController(){return this._abortController||(this._abortController=new AbortController),this._abortController}},Tu=new lo,Ts=class{constructor(e){this.manager=e!==void 0?e:Tu,this.crossOrigin="anonymous",this.withCredentials=!1,this.path="",this.resourcePath="",this.requestHeader={},typeof __THREE_DEVTOOLS__<"u"&&__THREE_DEVTOOLS__.dispatchEvent(new CustomEvent("observe",{detail:this}))}load(){}loadAsync(e,t){let n=this;return new Promise(function(s,r){n.load(e,s,t,r)})}parse(){}setCrossOrigin(e){return this.crossOrigin=e,this}setWithCredentials(e){return this.withCredentials=e,this}setPath(e){return this.path=e,this}setResourcePath(e){return this.resourcePath=e,this}setRequestHeader(e){return this.requestHeader=e,this}abort(){return this}};Ts.DEFAULT_MATERIAL_NAME="__DEFAULT";var ss=new WeakMap,co=class extends Ts{constructor(e){super(e)}load(e,t,n,s){this.path!==void 0&&(e=this.path+e),e=this.manager.resolveURL(e);let r=this,a=Aa.get(`image:${e}`);if(a!==void 0){if(a.complete===!0)r.manager.itemStart(e),setTimeout(function(){t&&t(a),r.manager.itemEnd(e)},0);else{let d=ss.get(a);d===void 0&&(d=[],ss.set(a,d)),d.push({onLoad:t,onError:s})}return a}let o=hs("img");function c(){h(),t&&t(this);let d=ss.get(this)||[];for(let u=0;u<d.length;u++){let p=d[u];p.onLoad&&p.onLoad(this)}ss.delete(this),r.manager.itemEnd(e)}function l(d){h(),s&&s(d),Aa.remove(`image:${e}`);let u=ss.get(this)||[];for(let p=0;p<u.length;p++){let g=u[p];g.onError&&g.onError(d)}ss.delete(this),r.manager.itemError(e),r.manager.itemEnd(e)}function h(){o.removeEventListener("load",c,!1),o.removeEventListener("error",l,!1)}return o.addEventListener("load",c,!1),o.addEventListener("error",l,!1),e.slice(0,5)!=="data:"&&this.crossOrigin!==void 0&&(o.crossOrigin=this.crossOrigin),Aa.add(`image:${e}`,o),r.manager.itemStart(e),o.src=e,o}};var Rr=class extends Ts{constructor(e){super(e)}load(e,t,n,s){let r=new Vt,a=new co(this.manager);return a.setCrossOrigin(this.crossOrigin),a.setPath(this.path),a.load(e,function(o){r.image=o,r.needsUpdate=!0,t!==void 0&&t(r)},n,s),r}},ws=class extends Tt{constructor(e,t=1){super(),this.isLight=!0,this.type="Light",this.color=new Fe(e),this.intensity=t}copy(e,t){return super.copy(e,t),this.color.copy(e.color),this.intensity=e.intensity,this}toJSON(e){let t=super.toJSON(e);return t.object.color=this.color.getHex(),t.object.intensity=this.intensity,t}},Cr=class extends ws{constructor(e,t,n){super(e,n),this.isHemisphereLight=!0,this.type="HemisphereLight",this.position.copy(Tt.DEFAULT_UP),this.updateMatrix(),this.groundColor=new Fe(t)}copy(e,t){return super.copy(e,t),this.groundColor.copy(e.groundColor),this}toJSON(e){let t=super.toJSON(e);return t.object.groundColor=this.groundColor.getHex(),t}},Fl=new ut,Rh=new L,Ch=new L,Ir=class{constructor(e){this.camera=e,this.intensity=1,this.bias=0,this.biasNode=null,this.normalBias=0,this.radius=1,this.blurSamples=8,this.mapSize=new he(512,512),this.mapType=$t,this.map=null,this.mapPass=null,this.matrix=new ut,this.autoUpdate=!0,this.needsUpdate=!1,this._frustum=new xs,this._frameExtents=new he(1,1),this._viewportCount=1,this._viewports=[new vt(0,0,1,1)]}getViewportCount(){return this._viewportCount}getCamera(){return this.camera}getFrustum(){return this._frustum}updateMatrices(e){let t=this.camera;Rh.setFromMatrixPosition(e.matrixWorld),t.position.copy(Rh),Ch.setFromMatrixPosition(e.target.matrixWorld),t.lookAt(Ch),t.updateMatrixWorld(),this._updateMatrix(t,this.matrix,this._frustum)}_updateMatrix(e,t,n,s){Fl.multiplyMatrices(e.projectionMatrix,e.matrixWorldInverse),n.setFromProjectionMatrix(Fl,e.coordinateSystem,e.reversedDepth);let r=this._frameExtents,a=s?s.z/r.x:1,o=s?s.w/r.y:1,c=s?s.x/r.x:0,l=s?s.y/r.y:0;e.coordinateSystem===cs||e.reversedDepth?t.set(.5*a,0,0,.5*a+c,0,.5*o,0,.5*o+l,0,0,1,0,0,0,0,1):t.set(.5*a,0,0,.5*a+c,0,.5*o,0,.5*o+l,0,0,.5,.5,0,0,0,1),t.multiply(Fl)}getViewport(e){return this._viewports[e]}getFrameExtents(){return this._frameExtents}dispose(){this.map&&this.map.dispose(),this.mapPass&&this.mapPass.dispose()}copy(e){return this.camera=e.camera.clone(),this.intensity=e.intensity,this.bias=e.bias,this.radius=e.radius,this.autoUpdate=e.autoUpdate,this.needsUpdate=e.needsUpdate,this.normalBias=e.normalBias,this.blurSamples=e.blurSamples,this.mapSize.copy(e.mapSize),this.biasNode=e.biasNode,this}clone(){return new this.constructor().copy(this)}toJSON(){let e={};return e.intensity=this.intensity,e.bias=this.bias,e.normalBias=this.normalBias,e.radius=this.radius,e.blurSamples=this.blurSamples,e.mapSize=this.mapSize.toArray(),e.camera=this.camera.toJSON(!1).object,delete e.camera.matrix,e}},ba=new L,Ea=new Pn,Tn=new L,Pr=class extends Tt{constructor(){super(),this.isCamera=!0,this.type="Camera",this.matrixWorldInverse=new ut,this.projectionMatrix=new ut,this.projectionMatrixInverse=new ut,this.coordinateSystem=gn,this._reversedDepth=!1}get reversedDepth(){return this._reversedDepth}copy(e,t){return super.copy(e,t),this.matrixWorldInverse.copy(e.matrixWorldInverse),this.projectionMatrix.copy(e.projectionMatrix),this.projectionMatrixInverse.copy(e.projectionMatrixInverse),this.coordinateSystem=e.coordinateSystem,this}getWorldDirection(e){return super.getWorldDirection(e).negate()}updateMatrixWorld(e){super.updateMatrixWorld(e),this.matrixWorld.decompose(ba,Ea,Tn),Tn.x===1&&Tn.y===1&&Tn.z===1?this.matrixWorldInverse.copy(this.matrixWorld).invert():this.matrixWorldInverse.compose(ba,Ea,Tn.set(1,1,1)).invert()}updateWorldMatrix(e,t,n=!1){super.updateWorldMatrix(e,t,n),this.matrixWorld.decompose(ba,Ea,Tn),Tn.x===1&&Tn.y===1&&Tn.z===1?this.matrixWorldInverse.copy(this.matrixWorld).invert():this.matrixWorldInverse.compose(ba,Ea,Tn.set(1,1,1)).invert()}clone(){return new this.constructor().copy(this)}},ai=new L,Ih=new he,Ph=new he,Ft=class extends Pr{constructor(e=50,t=1,n=.1,s=2e3){super(),this.isPerspectiveCamera=!0,this.type="PerspectiveCamera",this.fov=e,this.zoom=1,this.near=n,this.far=s,this.focus=10,this.aspect=t,this.view=null,this.filmGauge=35,this.filmOffset=0,this.updateProjectionMatrix()}copy(e,t){return super.copy(e,t),this.fov=e.fov,this.zoom=e.zoom,this.near=e.near,this.far=e.far,this.focus=e.focus,this.aspect=e.aspect,this.view=e.view===null?null:Object.assign({},e.view),this.filmGauge=e.filmGauge,this.filmOffset=e.filmOffset,this}setFocalLength(e){let t=.5*this.getFilmHeight()/e;this.fov=ds*2*Math.atan(t),this.updateProjectionMatrix()}getFocalLength(){let e=Math.tan($s*.5*this.fov);return .5*this.getFilmHeight()/e}getEffectiveFOV(){return ds*2*Math.atan(Math.tan($s*.5*this.fov)/this.zoom)}getFilmWidth(){return this.filmGauge*Math.min(this.aspect,1)}getFilmHeight(){return this.filmGauge/Math.max(this.aspect,1)}getViewBounds(e,t,n){ai.set(-1,-1,.5).applyMatrix4(this.projectionMatrixInverse),t.set(ai.x,ai.y).multiplyScalar(-e/ai.z),ai.set(1,1,.5).applyMatrix4(this.projectionMatrixInverse),n.set(ai.x,ai.y).multiplyScalar(-e/ai.z)}getViewSize(e,t){return this.getViewBounds(e,Ih,Ph),t.subVectors(Ph,Ih)}setViewOffset(e,t,n,s,r,a){this.aspect=e/t,this.view===null&&(this.view={enabled:!0,fullWidth:1,fullHeight:1,offsetX:0,offsetY:0,width:1,height:1}),this.view.enabled=!0,this.view.fullWidth=e,this.view.fullHeight=t,this.view.offsetX=n,this.view.offsetY=s,this.view.width=r,this.view.height=a,this.updateProjectionMatrix()}clearViewOffset(){this.view!==null&&(this.view.enabled=!1),this.updateProjectionMatrix()}updateProjectionMatrix(){let e=this.near,t=e*Math.tan($s*.5*this.fov)/this.zoom,n=2*t,s=this.aspect*n,r=-.5*s,a=this.view;if(this.view!==null&&this.view.enabled){let c=a.fullWidth,l=a.fullHeight;r+=a.offsetX*s/c,t-=a.offsetY*n/l,s*=a.width/c,n*=a.height/l}let o=this.filmOffset;o!==0&&(r+=e*o/this.getFilmWidth()),this.projectionMatrix.makePerspective(r,r+s,t,t-n,e,this.far,this.coordinateSystem,this.reversedDepth),this.projectionMatrixInverse.copy(this.projectionMatrix).invert()}toJSON(e){let t=super.toJSON(e);return t.object.fov=this.fov,t.object.zoom=this.zoom,t.object.near=this.near,t.object.far=this.far,t.object.focus=this.focus,t.object.aspect=this.aspect,this.view!==null&&(t.object.view=Object.assign({},this.view)),t.object.filmGauge=this.filmGauge,t.object.filmOffset=this.filmOffset,t}};var ql=class extends Ir{constructor(){super(new Ft(90,1,.5,500)),this.isPointLightShadow=!0}},di=class extends ws{constructor(e,t,n=0,s=2){super(e,t),this.isPointLight=!0,this.type="PointLight",this.distance=n,this.decay=s,this.shadow=new ql}get power(){return this.intensity*4*Math.PI}set power(e){this.intensity=e/(4*Math.PI)}dispose(){super.dispose(),this.shadow.dispose()}copy(e,t){return super.copy(e,t),this.distance=e.distance,this.decay=e.decay,this.shadow=e.shadow.clone(),this}toJSON(e){let t=super.toJSON(e);return t.object.distance=this.distance,t.object.decay=this.decay,t.object.shadow=this.shadow.toJSON(),t}},As=class extends Pr{constructor(e=-1,t=1,n=1,s=-1,r=.1,a=2e3){super(),this.isOrthographicCamera=!0,this.type="OrthographicCamera",this.zoom=1,this.view=null,this.left=e,this.right=t,this.top=n,this.bottom=s,this.near=r,this.far=a,this.updateProjectionMatrix()}copy(e,t){return super.copy(e,t),this.left=e.left,this.right=e.right,this.top=e.top,this.bottom=e.bottom,this.near=e.near,this.far=e.far,this.zoom=e.zoom,this.view=e.view===null?null:Object.assign({},e.view),this}setViewOffset(e,t,n,s,r,a){this.view===null&&(this.view={enabled:!0,fullWidth:1,fullHeight:1,offsetX:0,offsetY:0,width:1,height:1}),this.view.enabled=!0,this.view.fullWidth=e,this.view.fullHeight=t,this.view.offsetX=n,this.view.offsetY=s,this.view.width=r,this.view.height=a,this.updateProjectionMatrix()}clearViewOffset(){this.view!==null&&(this.view.enabled=!1),this.updateProjectionMatrix()}updateProjectionMatrix(){let e=(this.right-this.left)/(2*this.zoom),t=(this.top-this.bottom)/(2*this.zoom),n=(this.right+this.left)/2,s=(this.top+this.bottom)/2,r=n-e,a=n+e,o=s+t,c=s-t;if(this.view!==null&&this.view.enabled){let l=(this.right-this.left)/this.view.fullWidth/this.zoom,h=(this.top-this.bottom)/this.view.fullHeight/this.zoom;r+=l*this.view.offsetX,a=r+l*this.view.width,o-=h*this.view.offsetY,c=o-h*this.view.height}this.projectionMatrix.makeOrthographic(r,a,o,c,this.near,this.far,this.coordinateSystem,this.reversedDepth),this.projectionMatrixInverse.copy(this.projectionMatrix).invert()}toJSON(e){let t=super.toJSON(e);return t.object.zoom=this.zoom,t.object.left=this.left,t.object.right=this.right,t.object.top=this.top,t.object.bottom=this.bottom,t.object.near=this.near,t.object.far=this.far,this.view!==null&&(t.object.view=Object.assign({},this.view)),t}},Yl=class extends Ir{constructor(){super(new As(-5,5,5,-5,.5,500)),this.isDirectionalLightShadow=!0}},Lr=class extends ws{constructor(e,t){super(e,t),this.isDirectionalLight=!0,this.type="DirectionalLight",this.position.copy(Tt.DEFAULT_UP),this.updateMatrix(),this.target=new Tt,this.shadow=new Yl}dispose(){super.dispose(),this.shadow.dispose()}copy(e){return super.copy(e),this.target=e.target.clone(),this.shadow=e.shadow.clone(),this}toJSON(e){let t=super.toJSON(e);return t.object.shadow=this.shadow.toJSON(),t.object.target=this.target.uuid,t}};var rs=-90,as=1,ho=class extends Tt{constructor(e,t,n){super(),this.type="CubeCamera",this.renderTarget=n,this.coordinateSystem=null,this.activeMipmapLevel=0;let s=new Ft(rs,as,e,t);s.layers=this.layers,this.add(s);let r=new Ft(rs,as,e,t);r.layers=this.layers,this.add(r);let a=new Ft(rs,as,e,t);a.layers=this.layers,this.add(a);let o=new Ft(rs,as,e,t);o.layers=this.layers,this.add(o);let c=new Ft(rs,as,e,t);c.layers=this.layers,this.add(c);let l=new Ft(rs,as,e,t);l.layers=this.layers,this.add(l)}updateCoordinateSystem(){let e=this.coordinateSystem,t=this.children.concat(),[n,s,r,a,o,c]=t;for(let l of t)this.remove(l);if(e===gn)n.up.set(0,1,0),n.lookAt(1,0,0),s.up.set(0,1,0),s.lookAt(-1,0,0),r.up.set(0,0,-1),r.lookAt(0,1,0),a.up.set(0,0,1),a.lookAt(0,-1,0),o.up.set(0,1,0),o.lookAt(0,0,1),c.up.set(0,1,0),c.lookAt(0,0,-1);else if(e===cs)n.up.set(0,-1,0),n.lookAt(-1,0,0),s.up.set(0,-1,0),s.lookAt(1,0,0),r.up.set(0,0,1),r.lookAt(0,1,0),a.up.set(0,0,-1),a.lookAt(0,-1,0),o.up.set(0,-1,0),o.lookAt(0,0,1),c.up.set(0,-1,0),c.lookAt(0,0,-1);else throw new Error("THREE.CubeCamera.updateCoordinateSystem(): Invalid coordinate system: "+e);for(let l of t)this.add(l),l.updateMatrixWorld()}update(e,t){this.parent===null&&this.updateMatrixWorld();let{renderTarget:n,activeMipmapLevel:s}=this;this.coordinateSystem!==e.coordinateSystem&&(this.coordinateSystem=e.coordinateSystem,this.updateCoordinateSystem());let[r,a,o,c,l,h]=this.children,d=e.getRenderTarget(),u=e.getActiveCubeFace(),p=e.getActiveMipmapLevel(),g=e.xr.enabled;e.xr.enabled=!1;let v=n.texture.generateMipmaps;n.texture.generateMipmaps=!1;let m=!1;e.isWebGLRenderer===!0?m=e.state.buffers.depth.getReversed():m=e.reversedDepthBuffer,e.setRenderTarget(n,0,s),m&&e.autoClear===!1&&e.clearDepth(),e.render(t,r),e.setRenderTarget(n,1,s),m&&e.autoClear===!1&&e.clearDepth(),e.render(t,a),e.setRenderTarget(n,2,s),m&&e.autoClear===!1&&e.clearDepth(),e.render(t,o),e.setRenderTarget(n,3,s),m&&e.autoClear===!1&&e.clearDepth(),e.render(t,c),e.setRenderTarget(n,4,s),m&&e.autoClear===!1&&e.clearDepth(),e.render(t,l),n.texture.generateMipmaps=v,e.setRenderTarget(n,5,s),m&&e.autoClear===!1&&e.clearDepth(),e.render(t,h),e.setRenderTarget(d,u,p),e.xr.enabled=g,n.texture.needsPMREMUpdate=!0}},uo=class extends Ft{constructor(e=[]){super(),this.isArrayCamera=!0,this.isMultiViewCamera=!1,this.cameras=e}};var Mc="\\[\\]\\.:\\/",Mf=new RegExp("["+Mc+"]","g"),Sc="[^"+Mc+"]",Sf="[^"+Mc.replace("\\.","")+"]",bf=/((?:WC+[\/:])*)/.source.replace("WC",Sc),Ef=/(WCOD+)?/.source.replace("WCOD",Sf),Tf=/(?:\.(WC+)(?:\[(.+)\])?)?/.source.replace("WC",Sc),wf=/\.(WC+)(?:\[(.+)\])?/.source.replace("WC",Sc),Af=new RegExp("^"+bf+Ef+Tf+wf+"$"),Rf=["material","materials","bones","map"],Zl=class{constructor(e,t,n){let s=n||xt.parseTrackName(t);this._targetGroup=e,this._bindings=e.subscribe_(t,s)}getValue(e,t){this.bind();let n=this._targetGroup.nCachedObjects_,s=this._bindings[n];s!==void 0&&s.getValue(e,t)}setValue(e,t){let n=this._bindings;for(let s=this._targetGroup.nCachedObjects_,r=n.length;s!==r;++s)n[s].setValue(e,t)}bind(){let e=this._bindings;for(let t=this._targetGroup.nCachedObjects_,n=e.length;t!==n;++t)e[t].bind()}unbind(){let e=this._bindings;for(let t=this._targetGroup.nCachedObjects_,n=e.length;t!==n;++t)e[t].unbind()}},xt=class i{constructor(e,t,n){this.path=t,this.parsedPath=n||i.parseTrackName(t),this.node=i.findNode(e,this.parsedPath.nodeName),this.rootNode=e,this.getValue=this._getValue_unbound,this.setValue=this._setValue_unbound}static create(e,t,n){return e&&e.isAnimationObjectGroup?new i.Composite(e,t,n):new i(e,t,n)}static sanitizeNodeName(e){return e.replace(/\s/g,"_").replace(Mf,"")}static parseTrackName(e){let t=Af.exec(e);if(t===null)throw new Error("THREE.PropertyBinding: Cannot parse trackName: "+e);let n={nodeName:t[2],objectName:t[3],objectIndex:t[4],propertyName:t[5],propertyIndex:t[6]},s=n.nodeName&&n.nodeName.lastIndexOf(".");if(s!==void 0&&s!==-1){let r=n.nodeName.substring(s+1);Rf.indexOf(r)!==-1&&(n.nodeName=n.nodeName.substring(0,s),n.objectName=r)}if(n.propertyName===null||n.propertyName.length===0)throw new Error("THREE.PropertyBinding: can not parse propertyName from trackName: "+e);return n}static findNode(e,t){if(t===void 0||t===""||t==="."||t===-1||t===e.name||t===e.uuid)return e;if(e.skeleton){let n=e.skeleton.getBoneByName(t);if(n!==void 0)return n}if(e.children){let n=function(r){for(let a=0;a<r.length;a++){let o=r[a];if(o.name===t||o.uuid===t)return o;let c=n(o.children);if(c)return c}return null},s=n(e.children);if(s)return s}return null}_getValue_unavailable(){}_setValue_unavailable(){}_getValue_direct(e,t){e[t]=this.targetObject[this.propertyName]}_getValue_array(e,t){let n=this.resolvedProperty;for(let s=0,r=n.length;s!==r;++s)e[t++]=n[s]}_getValue_arrayElement(e,t){e[t]=this.resolvedProperty[this.propertyIndex]}_getValue_toArray(e,t){this.resolvedProperty.toArray(e,t)}_setValue_direct(e,t){this.targetObject[this.propertyName]=e[t]}_setValue_direct_setNeedsUpdate(e,t){this.targetObject[this.propertyName]=e[t],this.targetObject.needsUpdate=!0}_setValue_direct_setMatrixWorldNeedsUpdate(e,t){this.targetObject[this.propertyName]=e[t],this.targetObject.matrixWorldNeedsUpdate=!0}_setValue_array(e,t){let n=this.resolvedProperty;for(let s=0,r=n.length;s!==r;++s)n[s]=e[t++]}_setValue_array_setNeedsUpdate(e,t){let n=this.resolvedProperty;for(let s=0,r=n.length;s!==r;++s)n[s]=e[t++];this.targetObject.needsUpdate=!0}_setValue_array_setMatrixWorldNeedsUpdate(e,t){let n=this.resolvedProperty;for(let s=0,r=n.length;s!==r;++s)n[s]=e[t++];this.targetObject.matrixWorldNeedsUpdate=!0}_setValue_arrayElement(e,t){this.resolvedProperty[this.propertyIndex]=e[t]}_setValue_arrayElement_setNeedsUpdate(e,t){this.resolvedProperty[this.propertyIndex]=e[t],this.targetObject.needsUpdate=!0}_setValue_arrayElement_setMatrixWorldNeedsUpdate(e,t){this.resolvedProperty[this.propertyIndex]=e[t],this.targetObject.matrixWorldNeedsUpdate=!0}_setValue_fromArray(e,t){this.resolvedProperty.fromArray(e,t)}_setValue_fromArray_setNeedsUpdate(e,t){this.resolvedProperty.fromArray(e,t),this.targetObject.needsUpdate=!0}_setValue_fromArray_setMatrixWorldNeedsUpdate(e,t){this.resolvedProperty.fromArray(e,t),this.targetObject.matrixWorldNeedsUpdate=!0}_getValue_unbound(e,t){this.bind(),this.getValue(e,t)}_setValue_unbound(e,t){this.bind(),this.setValue(e,t)}bind(){let e=this.node,t=this.parsedPath,n=t.objectName,s=t.propertyName,r=t.propertyIndex;if(e||(e=i.findNode(this.rootNode,t.nodeName),this.node=e),this.getValue=this._getValue_unavailable,this.setValue=this._setValue_unavailable,!e){ke("PropertyBinding: No target node found for track: "+this.path+".");return}if(n){let l=t.objectIndex;switch(n){case"materials":if(!e.material){We("PropertyBinding: Can not bind to material as node does not have a material.",this);return}if(!e.material.materials){We("PropertyBinding: Can not bind to material.materials as node.material does not have a materials array.",this);return}e=e.material.materials;break;case"bones":if(!e.skeleton){We("PropertyBinding: Can not bind to bones as node does not have a skeleton.",this);return}e=e.skeleton.bones;for(let h=0;h<e.length;h++)if(e[h].name===l){l=h;break}break;case"map":if("map"in e){e=e.map;break}if(!e.material){We("PropertyBinding: Can not bind to material as node does not have a material.",this);return}if(!e.material.map){We("PropertyBinding: Can not bind to material.map as node.material does not have a map.",this);return}e=e.material.map;break;default:if(e[n]===void 0){We("PropertyBinding: Can not bind to objectName of node undefined.",this);return}e=e[n]}if(l!==void 0){if(e[l]===void 0){We("PropertyBinding: Trying to bind to objectIndex of objectName, but is undefined.",this,e);return}e=e[l]}}let a=e[s];if(a===void 0){let l=t.nodeName;We("PropertyBinding: Trying to update property for track: "+l+"."+s+" but it wasn't found.",e);return}let o=this.Versioning.None;this.targetObject=e,e.isMaterial===!0?o=this.Versioning.NeedsUpdate:e.isObject3D===!0&&(o=this.Versioning.MatrixWorldNeedsUpdate);let c=this.BindingType.Direct;if(r!==void 0){if(s==="morphTargetInfluences"){if(!e.geometry){We("PropertyBinding: Can not bind to morphTargetInfluences because node does not have a geometry.",this);return}if(!e.geometry.morphAttributes){We("PropertyBinding: Can not bind to morphTargetInfluences because node does not have a geometry.morphAttributes.",this);return}e.morphTargetDictionary[r]!==void 0&&(r=e.morphTargetDictionary[r])}c=this.BindingType.ArrayElement,this.resolvedProperty=a,this.propertyIndex=r}else a.fromArray!==void 0&&a.toArray!==void 0?(c=this.BindingType.HasFromToArray,this.resolvedProperty=a):Array.isArray(a)?(c=this.BindingType.EntireArray,this.resolvedProperty=a):this.propertyName=s;this.getValue=this.GetterByBindingType[c],this.setValue=this.SetterByBindingTypeAndVersioning[c][o]}unbind(){this.node=null,this.getValue=this._getValue_unbound,this.setValue=this._setValue_unbound}};xt.Composite=Zl;xt.prototype.BindingType={Direct:0,EntireArray:1,ArrayElement:2,HasFromToArray:3};xt.prototype.Versioning={None:0,NeedsUpdate:1,MatrixWorldNeedsUpdate:2};xt.prototype.GetterByBindingType=[xt.prototype._getValue_direct,xt.prototype._getValue_array,xt.prototype._getValue_arrayElement,xt.prototype._getValue_toArray];xt.prototype.SetterByBindingTypeAndVersioning=[[xt.prototype._setValue_direct,xt.prototype._setValue_direct_setNeedsUpdate,xt.prototype._setValue_direct_setMatrixWorldNeedsUpdate],[xt.prototype._setValue_array,xt.prototype._setValue_array_setNeedsUpdate,xt.prototype._setValue_array_setMatrixWorldNeedsUpdate],[xt.prototype._setValue_arrayElement,xt.prototype._setValue_arrayElement_setNeedsUpdate,xt.prototype._setValue_arrayElement_setMatrixWorldNeedsUpdate],[xt.prototype._setValue_fromArray,xt.prototype._setValue_fromArray_setNeedsUpdate,xt.prototype._setValue_fromArray_setMatrixWorldNeedsUpdate]];var k_=new Float32Array(1);var Lh=new ut,Dr=class{constructor(e,t,n=0,s=1/0){this.ray=new Ei(e,t),this.near=n,this.far=s,this.camera=null,this.layers=new ps,this.params={Mesh:{},Line:{threshold:1},LOD:{},Points:{threshold:1},Sprite:{}}}set(e,t){this.ray.set(e,t)}setFromCamera(e,t){t.isPerspectiveCamera?(this.ray.origin.setFromMatrixPosition(t.matrixWorld),this.ray.direction.set(e.x,e.y,.5).unproject(t).sub(this.ray.origin).normalize(),this.camera=t):t.isOrthographicCamera?(this.ray.origin.set(e.x,e.y,t.projectionMatrix.elements[14]).unproject(t),this.ray.direction.set(0,0,-1).transformDirection(t.matrixWorld),this.camera=t):We("Raycaster: Unsupported camera type: "+t.type)}setFromXRController(e){return Lh.identity().extractRotation(e.matrixWorld),this.ray.origin.setFromMatrixPosition(e.matrixWorld),this.ray.direction.set(0,0,-1).applyMatrix4(Lh),this}intersectObject(e,t=!0,n=[]){return Jl(e,this,n,t),n.sort(Dh),n}intersectObjects(e,t=!0,n=[]){for(let s=0,r=e.length;s<r;s++)Jl(e[s],this,n,t);return n.sort(Dh),n}};function Dh(i,e){return i.distance-e.distance}function Jl(i,e,t,n){let s=!0;if(i.layers.test(e.layers)&&i.raycast(e,t)===!1&&(s=!1),s===!0&&n===!0){let r=i.children;for(let a=0,o=r.length;a<o;a++)Jl(r[a],e,t,!0)}}var $l=class i{static{i.prototype.isMatrix2=!0}constructor(e,t,n,s){this.elements=[1,0,0,1],e!==void 0&&this.set(e,t,n,s)}identity(){return this.set(1,0,0,1),this}fromArray(e,t=0){for(let n=0;n<4;n++)this.elements[n]=e[n+t];return this}set(e,t,n,s){let r=this.elements;return r[0]=e,r[2]=t,r[1]=n,r[3]=s,this}};function bc(i,e,t,n){let s=Cf(n);switch(t){case pc:return i*e;case yo:return i*e/s.components*s.byteLength;case vo:return i*e/s.components*s.byteLength;case gi:return i*e*2/s.components*s.byteLength;case Mo:return i*e*2/s.components*s.byteLength;case mc:return i*e*3/s.components*s.byteLength;case cn:return i*e*4/s.components*s.byteLength;case So:return i*e*4/s.components*s.byteLength;case Br:case zr:return Math.floor((i+3)/4)*Math.floor((e+3)/4)*8;case Hr:case Vr:return Math.floor((i+3)/4)*Math.floor((e+3)/4)*16;case Eo:case wo:return Math.max(i,16)*Math.max(e,8)/4;case bo:case To:return Math.max(i,8)*Math.max(e,8)/2;case Ao:case Ro:case Io:case Po:return Math.floor((i+3)/4)*Math.floor((e+3)/4)*8;case Co:case kr:case Lo:return Math.floor((i+3)/4)*Math.floor((e+3)/4)*16;case Do:return Math.floor((i+3)/4)*Math.floor((e+3)/4)*16;case Uo:return Math.floor((i+4)/5)*Math.floor((e+3)/4)*16;case No:return Math.floor((i+4)/5)*Math.floor((e+4)/5)*16;case Fo:return Math.floor((i+5)/6)*Math.floor((e+4)/5)*16;case Oo:return Math.floor((i+5)/6)*Math.floor((e+5)/6)*16;case Bo:return Math.floor((i+7)/8)*Math.floor((e+4)/5)*16;case zo:return Math.floor((i+7)/8)*Math.floor((e+5)/6)*16;case Ho:return Math.floor((i+7)/8)*Math.floor((e+7)/8)*16;case Vo:return Math.floor((i+9)/10)*Math.floor((e+4)/5)*16;case ko:return Math.floor((i+9)/10)*Math.floor((e+5)/6)*16;case Go:return Math.floor((i+9)/10)*Math.floor((e+7)/8)*16;case Wo:return Math.floor((i+9)/10)*Math.floor((e+9)/10)*16;case Xo:return Math.floor((i+11)/12)*Math.floor((e+9)/10)*16;case qo:return Math.floor((i+11)/12)*Math.floor((e+11)/12)*16;case Yo:case Zo:case Jo:return Math.ceil(i/4)*Math.ceil(e/4)*16;case $o:case Ko:return Math.ceil(i/4)*Math.ceil(e/4)*8;case Gr:case Qo:return Math.ceil(i/4)*Math.ceil(e/4)*16}throw new Error(`Unable to determine texture byte length for ${t} format.`)}function Cf(i){switch(i){case $t:case hc:return{byteLength:1,components:1};case Is:case uc:case Sn:return{byteLength:2,components:1};case _o:case xo:return{byteLength:2,components:4};case Mn:case go:case ln:return{byteLength:4,components:1};case dc:case fc:return{byteLength:4,components:3}}throw new Error(`THREE.TextureUtils: Unknown texture type ${i}.`)}typeof __THREE_DEVTOOLS__<"u"&&__THREE_DEVTOOLS__.dispatchEvent(new CustomEvent("register",{detail:{revision:"186"}}));typeof window<"u"&&(window.__THREE__?ke("WARNING: Multiple instances of Three.js being imported."):window.__THREE__="186");function Yu(){let i=null,e=!1,t=null,n=null;function s(r,a){n=i.requestAnimationFrame(s),t(r,a)}return{start:function(){e!==!0&&t!==null&&i!==null&&(n=i.requestAnimationFrame(s),e=!0)},stop:function(){i!==null&&i.cancelAnimationFrame(n),e=!1},setAnimationLoop:function(r){t=r},setContext:function(r){i=r}}}function Pf(i){let e=new WeakMap;function t(o,c){let l=o.array,h=o.usage,d=l.byteLength,u=i.createBuffer();i.bindBuffer(c,u),i.bufferData(c,l,h),o.onUploadCallback();let p;if(l instanceof Float32Array)p=i.FLOAT;else if(typeof Float16Array<"u"&&l instanceof Float16Array)p=i.HALF_FLOAT;else if(l instanceof Uint16Array)o.isFloat16BufferAttribute?p=i.HALF_FLOAT:p=i.UNSIGNED_SHORT;else if(l instanceof Int16Array)p=i.SHORT;else if(l instanceof Uint32Array)p=i.UNSIGNED_INT;else if(l instanceof Int32Array)p=i.INT;else if(l instanceof Int8Array)p=i.BYTE;else if(l instanceof Uint8Array)p=i.UNSIGNED_BYTE;else if(l instanceof Uint8ClampedArray)p=i.UNSIGNED_BYTE;else throw new Error("THREE.WebGLAttributes: Unsupported buffer data format: "+l);return{buffer:u,type:p,bytesPerElement:l.BYTES_PER_ELEMENT,version:o.version,size:d}}function n(o,c,l){let h=c.array,d=c.updateRanges;if(i.bindBuffer(l,o),d.length===0)i.bufferSubData(l,0,h);else{d.sort((p,g)=>p.start-g.start);let u=0;for(let p=1;p<d.length;p++){let g=d[u],v=d[p];v.start<=g.start+g.count+1?g.count=Math.max(g.count,v.start+v.count-g.start):(++u,d[u]=v)}d.length=u+1;for(let p=0,g=d.length;p<g;p++){let v=d[p];i.bufferSubData(l,v.start*h.BYTES_PER_ELEMENT,h,v.start,v.count)}c.clearUpdateRanges()}c.onUploadCallback()}function s(o){return o.isInterleavedBufferAttribute&&(o=o.data),e.get(o)}function r(o){o.isInterleavedBufferAttribute&&(o=o.data);let c=e.get(o);c&&(i.deleteBuffer(c.buffer),e.delete(o))}function a(o,c){if(o.isInterleavedBufferAttribute&&(o=o.data),o.isGLBufferAttribute){let h=e.get(o);(!h||h.version<o.version)&&e.set(o,{buffer:o.buffer,type:o.type,bytesPerElement:o.elementSize,version:o.version});return}let l=e.get(o);if(l===void 0)e.set(o,t(o,c));else if(l.version<o.version){if(l.size!==o.array.byteLength)throw new Error("THREE.WebGLAttributes: The size of the buffer attribute's array buffer does not match the original size. Resizing buffer attributes is not supported.");n(l.buffer,o,c),l.version=o.version}}return{get:s,remove:r,update:a}}var Lf=`#ifdef USE_ALPHAHASH
	if ( diffuseColor.a < getAlphaHashThreshold( vPosition ) ) discard;
#endif`,Df=`#ifdef USE_ALPHAHASH
	const float ALPHA_HASH_SCALE = 0.05;
	float hash2D( vec2 value ) {
		return fract( 1.0e4 * sin( 17.0 * value.x + 0.1 * value.y ) * ( 0.1 + abs( sin( 13.0 * value.y + value.x ) ) ) );
	}
	float hash3D( vec3 value ) {
		return hash2D( vec2( hash2D( value.xy ), value.z ) );
	}
	float getAlphaHashThreshold( vec3 position ) {
		float maxDeriv = max(
			length( dFdx( position.xyz ) ),
			length( dFdy( position.xyz ) )
		);
		float pixScale = 1.0 / ( ALPHA_HASH_SCALE * maxDeriv );
		vec2 pixScales = vec2(
			exp2( floor( log2( pixScale ) ) ),
			exp2( ceil( log2( pixScale ) ) )
		);
		vec2 alpha = vec2(
			hash3D( floor( pixScales.x * position.xyz ) ),
			hash3D( floor( pixScales.y * position.xyz ) )
		);
		float lerpFactor = fract( log2( pixScale ) );
		float x = ( 1.0 - lerpFactor ) * alpha.x + lerpFactor * alpha.y;
		float a = min( lerpFactor, 1.0 - lerpFactor );
		vec3 cases = vec3(
			x * x / ( 2.0 * a * ( 1.0 - a ) ),
			( x - 0.5 * a ) / ( 1.0 - a ),
			1.0 - ( ( 1.0 - x ) * ( 1.0 - x ) / ( 2.0 * a * ( 1.0 - a ) ) )
		);
		float threshold = ( x < ( 1.0 - a ) )
			? ( ( x < a ) ? cases.x : cases.y )
			: cases.z;
		return clamp( threshold , 1.0e-6, 1.0 );
	}
#endif`,Uf=`#ifdef USE_ALPHAMAP
	diffuseColor.a *= texture2D( alphaMap, vAlphaMapUv ).g;
#endif`,Nf=`#ifdef USE_ALPHAMAP
	uniform sampler2D alphaMap;
#endif`,Ff=`#ifdef USE_ALPHATEST
	#ifdef ALPHA_TO_COVERAGE
	diffuseColor.a = smoothstep( alphaTest, alphaTest + fwidth( diffuseColor.a ), diffuseColor.a );
	if ( diffuseColor.a == 0.0 ) discard;
	#else
	if ( diffuseColor.a < alphaTest ) discard;
	#endif
#endif`,Of=`#ifdef USE_ALPHATEST
	uniform float alphaTest;
#endif`,Bf=`#ifdef USE_AOMAP
	float ambientOcclusion = ( texture2D( aoMap, vAoMapUv ).r - 1.0 ) * aoMapIntensity + 1.0;
	reflectedLight.indirectDiffuse *= ambientOcclusion;
	#if defined( USE_CLEARCOAT ) 
		clearcoatSpecularIndirect *= ambientOcclusion;
	#endif
	#if defined( USE_SHEEN ) 
		sheenSpecularIndirect *= ambientOcclusion;
	#endif
	#if defined( USE_ENVMAP ) && defined( STANDARD )
		float dotNV = saturate( dot( geometryNormal, geometryViewDir ) );
		reflectedLight.indirectSpecular *= computeSpecularOcclusion( dotNV, ambientOcclusion, material.roughness );
	#endif
#endif`,zf=`#ifdef USE_AOMAP
	uniform sampler2D aoMap;
	uniform float aoMapIntensity;
#endif`,Hf=`#ifdef USE_BATCHING
	#if ! defined( GL_ANGLE_multi_draw )
	#define gl_DrawID _gl_DrawID
	uniform int _gl_DrawID;
	#endif
	uniform highp sampler2D batchingTexture;
	uniform highp usampler2D batchingIdTexture;
	mat4 getBatchingMatrix( const in float i ) {
		int size = textureSize( batchingTexture, 0 ).x;
		int j = int( i ) * 4;
		int x = j % size;
		int y = j / size;
		vec4 v1 = texelFetch( batchingTexture, ivec2( x, y ), 0 );
		vec4 v2 = texelFetch( batchingTexture, ivec2( x + 1, y ), 0 );
		vec4 v3 = texelFetch( batchingTexture, ivec2( x + 2, y ), 0 );
		vec4 v4 = texelFetch( batchingTexture, ivec2( x + 3, y ), 0 );
		return mat4( v1, v2, v3, v4 );
	}
	float getIndirectIndex( const in int i ) {
		int size = textureSize( batchingIdTexture, 0 ).x;
		int x = i % size;
		int y = i / size;
		return float( texelFetch( batchingIdTexture, ivec2( x, y ), 0 ).r );
	}
#endif
#ifdef USE_BATCHING_COLOR
	uniform sampler2D batchingColorTexture;
	vec4 getBatchingColor( const in float i ) {
		int size = textureSize( batchingColorTexture, 0 ).x;
		int j = int( i );
		int x = j % size;
		int y = j / size;
		return texelFetch( batchingColorTexture, ivec2( x, y ), 0 );
	}
#endif`,Vf=`#ifdef USE_BATCHING
	mat4 batchingMatrix = getBatchingMatrix( getIndirectIndex( gl_DrawID ) );
#endif`,kf=`vec3 transformed = vec3( position );
#ifdef USE_ALPHAHASH
	vPosition = vec3( position );
#endif`,Gf=`vec3 objectNormal = vec3( normal );
#ifdef USE_TANGENT
	vec3 objectTangent = vec3( tangent.xyz );
#endif`,Wf=`float G_BlinnPhong_Implicit( ) {
	return 0.25;
}
float D_BlinnPhong( const in float shininess, const in float dotNH ) {
	return RECIPROCAL_PI * ( shininess * 0.5 + 1.0 ) * pow( dotNH, shininess );
}
vec3 BRDF_BlinnPhong( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, const in vec3 specularColor, const in float shininess ) {
	vec3 halfDir = normalize( lightDir + viewDir );
	float dotNH = saturate( dot( normal, halfDir ) );
	float dotVH = saturate( dot( viewDir, halfDir ) );
	vec3 F = F_Schlick( specularColor, 1.0, dotVH );
	float G = G_BlinnPhong_Implicit( );
	float D = D_BlinnPhong( shininess, dotNH );
	return F * ( G * D );
} // validated`,Xf=`#ifdef USE_IRIDESCENCE
	const mat3 XYZ_TO_REC709 = mat3(
		 3.2404542, -0.9692660,  0.0556434,
		-1.5371385,  1.8760108, -0.2040259,
		-0.4985314,  0.0415560,  1.0572252
	);
	vec3 Fresnel0ToIor( vec3 fresnel0 ) {
		vec3 sqrtF0 = sqrt( fresnel0 );
		return ( vec3( 1.0 ) + sqrtF0 ) / ( vec3( 1.0 ) - sqrtF0 );
	}
	vec3 IorToFresnel0( vec3 transmittedIor, float incidentIor ) {
		return pow2( ( transmittedIor - vec3( incidentIor ) ) / ( transmittedIor + vec3( incidentIor ) ) );
	}
	float IorToFresnel0( float transmittedIor, float incidentIor ) {
		return pow2( ( transmittedIor - incidentIor ) / ( transmittedIor + incidentIor ));
	}
	vec3 evalSensitivity( float OPD, vec3 shift ) {
		float phase = 2.0 * PI * OPD * 1.0e-9;
		vec3 val = vec3( 5.4856e-13, 4.4201e-13, 5.2481e-13 );
		vec3 pos = vec3( 1.6810e+06, 1.7953e+06, 2.2084e+06 );
		vec3 var = vec3( 4.3278e+09, 9.3046e+09, 6.6121e+09 );
		vec3 xyz = val * sqrt( 2.0 * PI * var ) * cos( pos * phase + shift ) * exp( - pow2( phase ) * var );
		xyz.x += 9.7470e-14 * sqrt( 2.0 * PI * 4.5282e+09 ) * cos( 2.2399e+06 * phase + shift[ 0 ] ) * exp( - 4.5282e+09 * pow2( phase ) );
		xyz /= 1.0685e-7;
		vec3 rgb = XYZ_TO_REC709 * xyz;
		return rgb;
	}
	vec3 evalIridescence( float outsideIOR, float eta2, float cosTheta1, float thinFilmThickness, vec3 baseF0 ) {
		vec3 I;
		float iridescenceIOR = mix( outsideIOR, eta2, smoothstep( 0.0, 0.03, thinFilmThickness ) );
		float sinTheta2Sq = pow2( outsideIOR / iridescenceIOR ) * ( 1.0 - pow2( cosTheta1 ) );
		float cosTheta2Sq = 1.0 - sinTheta2Sq;
		if ( cosTheta2Sq < 0.0 ) {
			return vec3( 1.0 );
		}
		float cosTheta2 = sqrt( cosTheta2Sq );
		float R0 = IorToFresnel0( iridescenceIOR, outsideIOR );
		float R12 = F_Schlick( R0, 1.0, cosTheta1 );
		float T121 = 1.0 - R12;
		float phi12 = 0.0;
		if ( iridescenceIOR < outsideIOR ) phi12 = PI;
		float phi21 = PI - phi12;
		vec3 baseIOR = Fresnel0ToIor( clamp( baseF0, 0.0, 0.9999 ) );		vec3 R1 = IorToFresnel0( baseIOR, iridescenceIOR );
		vec3 R23 = F_Schlick( R1, 1.0, cosTheta2 );
		vec3 phi23 = vec3( 0.0 );
		if ( baseIOR[ 0 ] < iridescenceIOR ) phi23[ 0 ] = PI;
		if ( baseIOR[ 1 ] < iridescenceIOR ) phi23[ 1 ] = PI;
		if ( baseIOR[ 2 ] < iridescenceIOR ) phi23[ 2 ] = PI;
		float OPD = 2.0 * iridescenceIOR * thinFilmThickness * cosTheta2;
		vec3 phi = vec3( phi21 ) + phi23;
		vec3 R123 = clamp( R12 * R23, 1e-5, 0.9999 );
		vec3 r123 = sqrt( R123 );
		vec3 Rs = pow2( T121 ) * R23 / ( vec3( 1.0 ) - R123 );
		vec3 C0 = R12 + Rs;
		I = C0;
		vec3 Cm = Rs - T121;
		for ( int m = 1; m <= 2; ++ m ) {
			Cm *= r123;
			vec3 Sm = 2.0 * evalSensitivity( float( m ) * OPD, float( m ) * phi );
			I += Cm * Sm;
		}
		return max( I, vec3( 0.0 ) );
	}
#endif`,qf=`#ifdef USE_BUMPMAP
	uniform sampler2D bumpMap;
	uniform float bumpScale;
	vec2 dHdxy_fwd() {
		vec2 dSTdx = dFdx( vBumpMapUv );
		vec2 dSTdy = dFdy( vBumpMapUv );
		float Hll = bumpScale * texture2D( bumpMap, vBumpMapUv ).x;
		float dBx = bumpScale * texture2D( bumpMap, vBumpMapUv + dSTdx ).x - Hll;
		float dBy = bumpScale * texture2D( bumpMap, vBumpMapUv + dSTdy ).x - Hll;
		return vec2( dBx, dBy );
	}
	vec3 perturbNormalArb( vec3 surf_pos, vec3 surf_norm, vec2 dHdxy, float faceDirection ) {
		vec3 vSigmaX = normalize( dFdx( surf_pos.xyz ) );
		vec3 vSigmaY = normalize( dFdy( surf_pos.xyz ) );
		vec3 vN = surf_norm;
		vec3 R1 = cross( vSigmaY, vN );
		vec3 R2 = cross( vN, vSigmaX );
		float fDet = dot( vSigmaX, R1 ) * faceDirection;
		vec3 vGrad = sign( fDet ) * ( dHdxy.x * R1 + dHdxy.y * R2 );
		return normalize( abs( fDet ) * surf_norm - vGrad );
	}
#endif`,Yf=`#if NUM_CLIPPING_PLANES > 0
	vec4 plane;
	#ifdef ALPHA_TO_COVERAGE
		float distanceToPlane, distanceGradient;
		float clipOpacity = 1.0;
		#pragma unroll_loop_start
		for ( int i = 0; i < UNION_CLIPPING_PLANES; i ++ ) {
			plane = clippingPlanes[ i ];
			distanceToPlane = - dot( vClipPosition, plane.xyz ) + plane.w;
			distanceGradient = fwidth( distanceToPlane ) / 2.0;
			clipOpacity *= smoothstep( - distanceGradient, distanceGradient, distanceToPlane );
			if ( clipOpacity == 0.0 ) discard;
		}
		#pragma unroll_loop_end
		#if UNION_CLIPPING_PLANES < NUM_CLIPPING_PLANES
			float unionClipOpacity = 1.0;
			#pragma unroll_loop_start
			for ( int i = UNION_CLIPPING_PLANES; i < NUM_CLIPPING_PLANES; i ++ ) {
				plane = clippingPlanes[ i ];
				distanceToPlane = - dot( vClipPosition, plane.xyz ) + plane.w;
				distanceGradient = fwidth( distanceToPlane ) / 2.0;
				unionClipOpacity *= 1.0 - smoothstep( - distanceGradient, distanceGradient, distanceToPlane );
			}
			#pragma unroll_loop_end
			clipOpacity *= 1.0 - unionClipOpacity;
		#endif
		diffuseColor.a *= clipOpacity;
		if ( diffuseColor.a == 0.0 ) discard;
	#else
		#pragma unroll_loop_start
		for ( int i = 0; i < UNION_CLIPPING_PLANES; i ++ ) {
			plane = clippingPlanes[ i ];
			if ( dot( vClipPosition, plane.xyz ) > plane.w ) discard;
		}
		#pragma unroll_loop_end
		#if UNION_CLIPPING_PLANES < NUM_CLIPPING_PLANES
			bool clipped = true;
			#pragma unroll_loop_start
			for ( int i = UNION_CLIPPING_PLANES; i < NUM_CLIPPING_PLANES; i ++ ) {
				plane = clippingPlanes[ i ];
				clipped = ( dot( vClipPosition, plane.xyz ) > plane.w ) && clipped;
			}
			#pragma unroll_loop_end
			if ( clipped ) discard;
		#endif
	#endif
#endif`,Zf=`#if NUM_CLIPPING_PLANES > 0
	varying vec3 vClipPosition;
	uniform vec4 clippingPlanes[ NUM_CLIPPING_PLANES ];
#endif`,Jf=`#if NUM_CLIPPING_PLANES > 0
	varying vec3 vClipPosition;
#endif`,$f=`#if NUM_CLIPPING_PLANES > 0
	vClipPosition = - mvPosition.xyz;
#endif`,Kf=`#if defined( USE_COLOR ) || defined( USE_COLOR_ALPHA )
	diffuseColor *= vColor;
#endif`,Qf=`#if defined( USE_COLOR ) || defined( USE_COLOR_ALPHA )
	varying vec4 vColor;
#endif`,jf=`#if defined( USE_COLOR ) || defined( USE_COLOR_ALPHA ) || defined( USE_INSTANCING_COLOR ) || defined( USE_BATCHING_COLOR )
	varying vec4 vColor;
#endif`,ep=`#if defined( USE_COLOR ) || defined( USE_COLOR_ALPHA ) || defined( USE_INSTANCING_COLOR ) || defined( USE_BATCHING_COLOR )
	vColor = vec4( 1.0 );
#endif
#ifdef USE_COLOR_ALPHA
	vColor *= color;
#elif defined( USE_COLOR )
	vColor.rgb *= color;
#endif
#ifdef USE_INSTANCING_COLOR
	vColor.rgb *= instanceColor.rgb;
#endif
#ifdef USE_BATCHING_COLOR
	vColor *= getBatchingColor( getIndirectIndex( gl_DrawID ) );
#endif`,tp=`#define PI 3.141592653589793
#define PI2 6.283185307179586
#define PI_HALF 1.5707963267948966
#define RECIPROCAL_PI 0.3183098861837907
#define RECIPROCAL_PI2 0.15915494309189535
#define EPSILON 1e-6
#ifndef saturate
#define saturate( a ) clamp( a, 0.0, 1.0 )
#endif
#define whiteComplement( a ) ( 1.0 - saturate( a ) )
float pow2( const in float x ) { return x*x; }
vec3 pow2( const in vec3 x ) { return x*x; }
float pow3( const in float x ) { return x*x*x; }
float pow4( const in float x ) { float x2 = x*x; return x2*x2; }
float max3( const in vec3 v ) { return max( max( v.x, v.y ), v.z ); }
float average( const in vec3 v ) { return dot( v, vec3( 0.3333333 ) ); }
highp float rand( const in vec2 uv ) {
	const highp float a = 12.9898, b = 78.233, c = 43758.5453;
	highp float dt = dot( uv.xy, vec2( a,b ) ), sn = mod( dt, PI );
	return fract( sin( sn ) * c );
}
#ifdef HIGH_PRECISION
	float precisionSafeLength( vec3 v ) { return length( v ); }
#else
	float precisionSafeLength( vec3 v ) {
		float maxComponent = max3( abs( v ) );
		return length( v / maxComponent ) * maxComponent;
	}
#endif
struct IncidentLight {
	vec3 color;
	vec3 direction;
	bool visible;
};
struct ReflectedLight {
	vec3 directDiffuse;
	vec3 directSpecular;
	vec3 indirectDiffuse;
	vec3 indirectSpecular;
};
#ifdef USE_ALPHAHASH
	varying vec3 vPosition;
#endif
vec3 transformDirection( in vec3 dir, in mat4 matrix ) {
	return normalize( ( matrix * vec4( dir, 0.0 ) ).xyz );
}
#define inverseTransformDirection transformDirectionByInverseViewMatrix
vec3 transformNormalByInverseViewMatrix( in vec3 normal, in mat4 viewMatrix ) {
	return normalize( ( vec4( normal, 0.0 ) * viewMatrix ).xyz );
}
vec3 transformDirectionByInverseViewMatrix( in vec3 dir, in mat4 viewMatrix ) {
	return normalize( ( vec4( dir, 0.0 ) * viewMatrix ).xyz );
}
bool isPerspectiveMatrix( mat4 m ) {
	return m[ 2 ][ 3 ] == - 1.0;
}
vec2 equirectUv( in vec3 dir ) {
	float u = atan( dir.z, dir.x ) * RECIPROCAL_PI2 + 0.5;
	float v = asin( clamp( dir.y, - 1.0, 1.0 ) ) * RECIPROCAL_PI + 0.5;
	return vec2( u, v );
}
vec3 BRDF_Lambert( const in vec3 diffuseColor ) {
	return RECIPROCAL_PI * diffuseColor;
}
vec3 F_Schlick( const in vec3 f0, const in float f90, const in float dotVH ) {
	float fresnel = exp2( ( - 5.55473 * dotVH - 6.98316 ) * dotVH );
	return f0 * ( 1.0 - fresnel ) + ( f90 * fresnel );
}
float F_Schlick( const in float f0, const in float f90, const in float dotVH ) {
	float fresnel = exp2( ( - 5.55473 * dotVH - 6.98316 ) * dotVH );
	return f0 * ( 1.0 - fresnel ) + ( f90 * fresnel );
} // validated`,np=`#ifdef ENVMAP_TYPE_CUBE_UV
	#define cubeUV_minMipLevel 4.0
	#define cubeUV_minTileSize 16.0
	float getFace( vec3 direction ) {
		vec3 absDirection = abs( direction );
		float face = - 1.0;
		if ( absDirection.x > absDirection.z ) {
			if ( absDirection.x > absDirection.y )
				face = direction.x > 0.0 ? 0.0 : 3.0;
			else
				face = direction.y > 0.0 ? 1.0 : 4.0;
		} else {
			if ( absDirection.z > absDirection.y )
				face = direction.z > 0.0 ? 2.0 : 5.0;
			else
				face = direction.y > 0.0 ? 1.0 : 4.0;
		}
		return face;
	}
	vec2 getUV( vec3 direction, float face ) {
		vec2 uv;
		if ( face == 0.0 ) {
			uv = vec2( direction.z, direction.y ) / abs( direction.x );
		} else if ( face == 1.0 ) {
			uv = vec2( - direction.x, - direction.z ) / abs( direction.y );
		} else if ( face == 2.0 ) {
			uv = vec2( - direction.x, direction.y ) / abs( direction.z );
		} else if ( face == 3.0 ) {
			uv = vec2( - direction.z, direction.y ) / abs( direction.x );
		} else if ( face == 4.0 ) {
			uv = vec2( - direction.x, direction.z ) / abs( direction.y );
		} else {
			uv = vec2( direction.x, direction.y ) / abs( direction.z );
		}
		return 0.5 * ( uv + 1.0 );
	}
	vec3 bilinearCubeUV( sampler2D envMap, vec3 direction, float mipInt ) {
		float face = getFace( direction );
		float filterInt = max( cubeUV_minMipLevel - mipInt, 0.0 );
		mipInt = max( mipInt, cubeUV_minMipLevel );
		float faceSize = exp2( mipInt );
		highp vec2 uv = getUV( direction, face ) * ( faceSize - 2.0 ) + 1.0;
		if ( face > 2.0 ) {
			uv.y += faceSize;
			face -= 3.0;
		}
		uv.x += face * faceSize;
		uv.x += filterInt * 3.0 * cubeUV_minTileSize;
		uv.y += 4.0 * ( exp2( CUBEUV_MAX_MIP ) - faceSize );
		uv.x *= CUBEUV_TEXEL_WIDTH;
		uv.y *= CUBEUV_TEXEL_HEIGHT;
		#ifdef texture2DGradEXT
			return texture2DGradEXT( envMap, uv, vec2( 0.0 ), vec2( 0.0 ) ).rgb;
		#else
			return texture2D( envMap, uv ).rgb;
		#endif
	}
	#define cubeUV_r0 1.0
	#define cubeUV_m0 - 2.0
	#define cubeUV_r1 0.8
	#define cubeUV_m1 - 1.0
	#define cubeUV_r4 0.4
	#define cubeUV_m4 2.0
	#define cubeUV_r5 0.305
	#define cubeUV_m5 3.0
	#define cubeUV_r6 0.21
	#define cubeUV_m6 4.0
	float roughnessToMip( float roughness ) {
		float mip = 0.0;
		if ( roughness >= cubeUV_r1 ) {
			mip = ( cubeUV_r0 - roughness ) * ( cubeUV_m1 - cubeUV_m0 ) / ( cubeUV_r0 - cubeUV_r1 ) + cubeUV_m0;
		} else if ( roughness >= cubeUV_r4 ) {
			mip = ( cubeUV_r1 - roughness ) * ( cubeUV_m4 - cubeUV_m1 ) / ( cubeUV_r1 - cubeUV_r4 ) + cubeUV_m1;
		} else if ( roughness >= cubeUV_r5 ) {
			mip = ( cubeUV_r4 - roughness ) * ( cubeUV_m5 - cubeUV_m4 ) / ( cubeUV_r4 - cubeUV_r5 ) + cubeUV_m4;
		} else if ( roughness >= cubeUV_r6 ) {
			mip = ( cubeUV_r5 - roughness ) * ( cubeUV_m6 - cubeUV_m5 ) / ( cubeUV_r5 - cubeUV_r6 ) + cubeUV_m5;
		} else {
			mip = - 2.0 * log2( 1.16 * roughness );		}
		return mip;
	}
	vec4 textureCubeUV( sampler2D envMap, vec3 sampleDir, float roughness ) {
		float mip = clamp( roughnessToMip( roughness ), cubeUV_m0, CUBEUV_MAX_MIP );
		float mipF = fract( mip );
		float mipInt = floor( mip );
		vec3 color0 = bilinearCubeUV( envMap, sampleDir, mipInt );
		if ( mipF == 0.0 ) {
			return vec4( color0, 1.0 );
		} else {
			vec3 color1 = bilinearCubeUV( envMap, sampleDir, mipInt + 1.0 );
			return vec4( mix( color0, color1, mipF ), 1.0 );
		}
	}
#endif`,ip=`vec3 transformedNormal = objectNormal;
#ifdef USE_TANGENT
	vec3 transformedTangent = objectTangent;
#endif
#ifdef USE_BATCHING
	mat3 bm = mat3( batchingMatrix );
	transformedNormal /= vec3( dot( bm[ 0 ], bm[ 0 ] ), dot( bm[ 1 ], bm[ 1 ] ), dot( bm[ 2 ], bm[ 2 ] ) );
	transformedNormal = bm * transformedNormal;
	#ifdef USE_TANGENT
		transformedTangent = bm * transformedTangent;
	#endif
#endif
#ifdef USE_INSTANCING
	mat3 im = mat3( instanceMatrix );
	transformedNormal /= vec3( dot( im[ 0 ], im[ 0 ] ), dot( im[ 1 ], im[ 1 ] ), dot( im[ 2 ], im[ 2 ] ) );
	transformedNormal = im * transformedNormal;
	#ifdef USE_TANGENT
		transformedTangent = im * transformedTangent;
	#endif
#endif
transformedNormal = normalMatrix * transformedNormal;
#ifdef FLIP_SIDED
	transformedNormal = - transformedNormal;
#endif
#ifdef USE_TANGENT
	transformedTangent = ( modelViewMatrix * vec4( transformedTangent, 0.0 ) ).xyz;
#endif`,sp=`#ifdef USE_DISPLACEMENTMAP
	uniform sampler2D displacementMap;
	uniform float displacementScale;
	uniform float displacementBias;
#endif`,rp=`#ifdef USE_DISPLACEMENTMAP
	transformed += normalize( objectNormal ) * ( texture2D( displacementMap, vDisplacementMapUv ).x * displacementScale + displacementBias );
#endif`,ap=`#ifdef USE_EMISSIVEMAP
	vec4 emissiveColor = texture2D( emissiveMap, vEmissiveMapUv );
	#ifdef DECODE_VIDEO_TEXTURE_EMISSIVE
		emissiveColor = sRGBTransferEOTF( emissiveColor );
	#endif
	totalEmissiveRadiance *= emissiveColor.rgb;
#endif`,op=`#ifdef USE_EMISSIVEMAP
	uniform sampler2D emissiveMap;
#endif`,lp="gl_FragColor = linearToOutputTexel( gl_FragColor );",cp=`vec4 LinearTransferOETF( in vec4 value ) {
	return value;
}
vec4 sRGBTransferEOTF( in vec4 value ) {
	return vec4( mix( pow( value.rgb * 0.9478672986 + vec3( 0.0521327014 ), vec3( 2.4 ) ), value.rgb * 0.0773993808, vec3( lessThanEqual( value.rgb, vec3( 0.04045 ) ) ) ), value.a );
}
vec4 sRGBTransferOETF( in vec4 value ) {
	return vec4( mix( pow( value.rgb, vec3( 0.41666 ) ) * 1.055 - vec3( 0.055 ), value.rgb * 12.92, vec3( lessThanEqual( value.rgb, vec3( 0.0031308 ) ) ) ), value.a );
}`,hp=`#ifdef USE_ENVMAP
	#ifdef ENV_WORLDPOS
		vec3 cameraToFrag;
		if ( isOrthographic ) {
			cameraToFrag = normalize( vec3( - viewMatrix[ 0 ][ 2 ], - viewMatrix[ 1 ][ 2 ], - viewMatrix[ 2 ][ 2 ] ) );
		} else {
			cameraToFrag = normalize( vWorldPosition - cameraPosition );
		}
		vec3 worldNormal = transformNormalByInverseViewMatrix( normal, viewMatrix );
		#ifdef ENVMAP_MODE_REFLECTION
			vec3 reflectVec = reflect( cameraToFrag, worldNormal );
		#else
			vec3 reflectVec = refract( cameraToFrag, worldNormal, refractionRatio );
		#endif
	#else
		vec3 reflectVec = vReflect;
	#endif
	#ifdef ENVMAP_TYPE_CUBE
		vec4 envColor = textureCube( envMap, envMapRotation * reflectVec );
		#ifdef ENVMAP_BLENDING_MULTIPLY
			outgoingLight = mix( outgoingLight, outgoingLight * envColor.xyz, specularStrength * reflectivity );
		#elif defined( ENVMAP_BLENDING_MIX )
			outgoingLight = mix( outgoingLight, envColor.xyz, specularStrength * reflectivity );
		#elif defined( ENVMAP_BLENDING_ADD )
			outgoingLight += envColor.xyz * specularStrength * reflectivity;
		#endif
	#endif
#endif`,up=`#ifdef USE_ENVMAP
	uniform float envMapIntensity;
	uniform mat3 envMapRotation;
	#ifdef ENVMAP_TYPE_CUBE
		uniform samplerCube envMap;
	#else
		uniform sampler2D envMap;
	#endif
#endif`,dp=`#ifdef USE_ENVMAP
	uniform float reflectivity;
	#if defined( USE_BUMPMAP ) || defined( USE_NORMALMAP ) || defined( PHONG ) || defined( LAMBERT )
		#define ENV_WORLDPOS
	#endif
	#ifdef ENV_WORLDPOS
		varying vec3 vWorldPosition;
		uniform float refractionRatio;
	#else
		varying vec3 vReflect;
	#endif
#endif`,fp=`#ifdef USE_ENVMAP
	#if defined( USE_BUMPMAP ) || defined( USE_NORMALMAP ) || defined( PHONG ) || defined( LAMBERT )
		#define ENV_WORLDPOS
	#endif
	#ifdef ENV_WORLDPOS
		
		varying vec3 vWorldPosition;
	#else
		varying vec3 vReflect;
		uniform float refractionRatio;
	#endif
#endif`,pp=`#ifdef USE_ENVMAP
	#ifdef ENV_WORLDPOS
		vWorldPosition = worldPosition.xyz;
	#else
		vec3 cameraToVertex;
		if ( isOrthographic ) {
			cameraToVertex = normalize( vec3( - viewMatrix[ 0 ][ 2 ], - viewMatrix[ 1 ][ 2 ], - viewMatrix[ 2 ][ 2 ] ) );
		} else {
			cameraToVertex = normalize( worldPosition.xyz - cameraPosition );
		}
		vec3 worldNormal = transformNormalByInverseViewMatrix( transformedNormal, viewMatrix );
		#ifdef ENVMAP_MODE_REFLECTION
			vReflect = reflect( cameraToVertex, worldNormal );
		#else
			vReflect = refract( cameraToVertex, worldNormal, refractionRatio );
		#endif
	#endif
#endif`,mp=`#ifdef USE_FOG
	vFogDepth = - mvPosition.z;
#endif`,gp=`#ifdef USE_FOG
	varying float vFogDepth;
#endif`,_p=`#ifdef USE_FOG
	#ifdef FOG_EXP2
		float fogFactor = 1.0 - exp( - fogDensity * fogDensity * vFogDepth * vFogDepth );
	#else
		float fogFactor = smoothstep( fogNear, fogFar, vFogDepth );
	#endif
	gl_FragColor.rgb = mix( gl_FragColor.rgb, fogColor, fogFactor );
#endif`,xp=`#ifdef USE_FOG
	uniform vec3 fogColor;
	varying float vFogDepth;
	#ifdef FOG_EXP2
		uniform float fogDensity;
	#else
		uniform float fogNear;
		uniform float fogFar;
	#endif
#endif`,yp=`#ifdef USE_GRADIENTMAP
	uniform sampler2D gradientMap;
#endif
vec3 getGradientIrradiance( vec3 normal, vec3 lightDirection ) {
	float dotNL = dot( normal, lightDirection );
	vec2 coord = vec2( dotNL * 0.5 + 0.5, 0.0 );
	#ifdef USE_GRADIENTMAP
		return vec3( texture2D( gradientMap, coord ).r );
	#else
		vec2 fw = fwidth( coord ) * 0.5;
		return mix( vec3( 0.7 ), vec3( 1.0 ), smoothstep( 0.7 - fw.x, 0.7 + fw.x, coord.x ) );
	#endif
}`,vp=`#ifdef USE_LIGHTMAP
	uniform sampler2D lightMap;
	uniform float lightMapIntensity;
#endif`,Mp=`LambertMaterial material;
material.diffuseColor = diffuseColor.rgb;
material.specularStrength = specularStrength;`,Sp=`varying vec3 vViewPosition;
struct LambertMaterial {
	vec3 diffuseColor;
	float specularStrength;
};
void RE_Direct_Lambert( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in LambertMaterial material, inout ReflectedLight reflectedLight ) {
	float dotNL = saturate( dot( geometryNormal, directLight.direction ) );
	vec3 irradiance = dotNL * directLight.color;
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectDiffuse_Lambert( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in LambertMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct				RE_Direct_Lambert
#define RE_IndirectDiffuse		RE_IndirectDiffuse_Lambert`,bp=`uniform bool receiveShadow;
uniform vec3 ambientLightColor;
#if defined( USE_LIGHT_PROBES )
	uniform vec3 lightProbe[ 9 ];
#endif
vec3 shGetIrradianceAt( in vec3 normal, in vec3 shCoefficients[ 9 ] ) {
	float x = normal.x, y = normal.y, z = normal.z;
	vec3 result = shCoefficients[ 0 ] * 0.886227;
	result += shCoefficients[ 1 ] * 2.0 * 0.511664 * y;
	result += shCoefficients[ 2 ] * 2.0 * 0.511664 * z;
	result += shCoefficients[ 3 ] * 2.0 * 0.511664 * x;
	result += shCoefficients[ 4 ] * 2.0 * 0.429043 * x * y;
	result += shCoefficients[ 5 ] * 2.0 * 0.429043 * y * z;
	result += shCoefficients[ 6 ] * ( 0.743125 * z * z - 0.247708 );
	result += shCoefficients[ 7 ] * 2.0 * 0.429043 * x * z;
	result += shCoefficients[ 8 ] * 0.429043 * ( x * x - y * y );
	return result;
}
vec3 getLightProbeIrradiance( const in vec3 lightProbe[ 9 ], const in vec3 normal ) {
	vec3 worldNormal = transformNormalByInverseViewMatrix( normal, viewMatrix );
	vec3 irradiance = shGetIrradianceAt( worldNormal, lightProbe );
	return irradiance;
}
vec3 getAmbientLightIrradiance( const in vec3 ambientLightColor ) {
	vec3 irradiance = ambientLightColor;
	return irradiance;
}
float getDistanceAttenuation( const in float lightDistance, const in float cutoffDistance, const in float decayExponent ) {
	float distanceFalloff = 1.0 / max( pow( lightDistance, decayExponent ), 0.01 );
	if ( cutoffDistance > 0.0 ) {
		distanceFalloff *= pow2( saturate( 1.0 - pow4( lightDistance / cutoffDistance ) ) );
	}
	return distanceFalloff;
}
float getSpotAttenuation( const in float coneCosine, const in float penumbraCosine, const in float angleCosine ) {
	return smoothstep( coneCosine, penumbraCosine, angleCosine );
}
#if NUM_SUN_LIGHTS > 0
	struct SunLight {
		vec3 direction;
		vec3 color;
	};
	uniform SunLight sunLights[ NUM_SUN_LIGHTS ];
	void getSunLightInfo( const in SunLight sunLight, out IncidentLight light ) {
		light.color = sunLight.color;
		light.direction = sunLight.direction;
		light.visible = true;
	}
#endif
#if NUM_DIR_LIGHTS > 0
	struct DirectionalLight {
		vec3 direction;
		vec3 color;
	};
	uniform DirectionalLight directionalLights[ NUM_DIR_LIGHTS ];
	void getDirectionalLightInfo( const in DirectionalLight directionalLight, out IncidentLight light ) {
		light.color = directionalLight.color;
		light.direction = directionalLight.direction;
		light.visible = true;
	}
#endif
#if NUM_POINT_LIGHTS > 0
	struct PointLight {
		vec3 position;
		vec3 color;
		float distance;
		float decay;
	};
	uniform PointLight pointLights[ NUM_POINT_LIGHTS ];
	void getPointLightInfo( const in PointLight pointLight, const in vec3 geometryPosition, out IncidentLight light ) {
		vec3 lVector = pointLight.position - geometryPosition;
		light.direction = normalize( lVector );
		float lightDistance = length( lVector );
		light.color = pointLight.color;
		light.color *= getDistanceAttenuation( lightDistance, pointLight.distance, pointLight.decay );
		light.visible = ( light.color != vec3( 0.0 ) );
	}
#endif
#if NUM_SPOT_LIGHTS > 0
	struct SpotLight {
		vec3 position;
		vec3 direction;
		vec3 color;
		float distance;
		float decay;
		float coneCos;
		float penumbraCos;
	};
	uniform SpotLight spotLights[ NUM_SPOT_LIGHTS ];
	void getSpotLightInfo( const in SpotLight spotLight, const in vec3 geometryPosition, out IncidentLight light ) {
		vec3 lVector = spotLight.position - geometryPosition;
		light.direction = normalize( lVector );
		float angleCos = dot( light.direction, spotLight.direction );
		float spotAttenuation = getSpotAttenuation( spotLight.coneCos, spotLight.penumbraCos, angleCos );
		if ( spotAttenuation > 0.0 ) {
			float lightDistance = length( lVector );
			light.color = spotLight.color * spotAttenuation;
			light.color *= getDistanceAttenuation( lightDistance, spotLight.distance, spotLight.decay );
			light.visible = ( light.color != vec3( 0.0 ) );
		} else {
			light.color = vec3( 0.0 );
			light.visible = false;
		}
	}
#endif
#if NUM_RECT_AREA_LIGHTS > 0
	struct RectAreaLight {
		vec3 color;
		vec3 position;
		vec3 halfWidth;
		vec3 halfHeight;
	};
	uniform sampler2D ltc_1;	uniform sampler2D ltc_2;
	uniform RectAreaLight rectAreaLights[ NUM_RECT_AREA_LIGHTS ];
#endif
#if NUM_HEMI_LIGHTS > 0
	struct HemisphereLight {
		vec3 direction;
		vec3 skyColor;
		vec3 groundColor;
	};
	uniform HemisphereLight hemisphereLights[ NUM_HEMI_LIGHTS ];
	vec3 getHemisphereLightIrradiance( const in HemisphereLight hemiLight, const in vec3 normal ) {
		float dotNL = dot( normal, hemiLight.direction );
		float hemiDiffuseWeight = 0.5 * dotNL + 0.5;
		vec3 irradiance = mix( hemiLight.groundColor, hemiLight.skyColor, hemiDiffuseWeight );
		return irradiance;
	}
#endif
#include <lightprobes_pars_fragment>`,Ep=`#ifdef USE_ENVMAP
	vec3 getIBLIrradiance( const in vec3 normal ) {
		#ifdef ENVMAP_TYPE_CUBE_UV
			vec3 worldNormal = transformNormalByInverseViewMatrix( normal, viewMatrix );
			vec4 envMapColor = textureCubeUV( envMap, envMapRotation * worldNormal, 1.0 );
			return PI * envMapColor.rgb * envMapIntensity;
		#else
			return vec3( 0.0 );
		#endif
	}
	vec3 getIBLRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness ) {
		#ifdef ENVMAP_TYPE_CUBE_UV
			vec3 reflectVec = reflect( - viewDir, normal );
			reflectVec = normalize( mix( reflectVec, normal, pow4( roughness ) ) );
			reflectVec = transformDirectionByInverseViewMatrix( reflectVec, viewMatrix );
			vec4 envMapColor = textureCubeUV( envMap, envMapRotation * reflectVec, roughness );
			return envMapColor.rgb * envMapIntensity;
		#else
			return vec3( 0.0 );
		#endif
	}
	#ifdef USE_RETROREFLECTION
		vec3 getIBLRetroRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness ) {
			#ifdef ENVMAP_TYPE_CUBE_UV
				vec3 retroVec = normalize( mix( viewDir, normal, pow4( roughness ) ) );
				retroVec = transformDirectionByInverseViewMatrix( retroVec, viewMatrix );
				vec4 envMapColor = textureCubeUV( envMap, envMapRotation * retroVec, roughness );
				return envMapColor.rgb * envMapIntensity;
			#else
				return vec3( 0.0 );
			#endif
		}
	#endif
	#ifdef USE_ANISOTROPY
		vec3 getIBLAnisotropyRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness, const in vec3 bitangent, const in float anisotropy ) {
			#ifdef ENVMAP_TYPE_CUBE_UV
				vec3 bentNormal = cross( bitangent, viewDir );
				bentNormal = normalize( cross( bentNormal, bitangent ) );
				bentNormal = normalize( mix( bentNormal, normal, pow2( pow2( 1.0 - anisotropy * ( 1.0 - roughness ) ) ) ) );
				return getIBLRadiance( viewDir, bentNormal, roughness );
			#else
				return vec3( 0.0 );
			#endif
		}
		#ifdef USE_RETROREFLECTION
			vec3 getIBLAnisotropyRetroRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness, const in vec3 bitangent, const in float anisotropy ) {
				#ifdef ENVMAP_TYPE_CUBE_UV
					vec3 bentNormal = cross( bitangent, viewDir );
					bentNormal = normalize( cross( bentNormal, bitangent ) );
					bentNormal = normalize( mix( bentNormal, normal, pow2( pow2( 1.0 - anisotropy * ( 1.0 - roughness ) ) ) ) );
					return getIBLRetroRadiance( viewDir, bentNormal, roughness );
				#else
					return vec3( 0.0 );
				#endif
			}
		#endif
	#endif
#endif`,Tp=`ToonMaterial material;
material.diffuseColor = diffuseColor.rgb;`,wp=`varying vec3 vViewPosition;
struct ToonMaterial {
	vec3 diffuseColor;
};
void RE_Direct_Toon( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in ToonMaterial material, inout ReflectedLight reflectedLight ) {
	vec3 irradiance = getGradientIrradiance( geometryNormal, directLight.direction ) * directLight.color;
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
void RE_IndirectDiffuse_Toon( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in ToonMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct				RE_Direct_Toon
#define RE_IndirectDiffuse		RE_IndirectDiffuse_Toon`,Ap=`BlinnPhongMaterial material;
material.diffuseColor = diffuseColor.rgb;
material.specularColor = specular;
material.specularShininess = shininess;
material.specularStrength = specularStrength;`,Rp=`varying vec3 vViewPosition;
struct BlinnPhongMaterial {
	vec3 diffuseColor;
	vec3 specularColor;
	float specularShininess;
	float specularStrength;
};
void RE_Direct_BlinnPhong( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in BlinnPhongMaterial material, inout ReflectedLight reflectedLight ) {
	float dotNL = saturate( dot( geometryNormal, directLight.direction ) );
	vec3 irradiance = dotNL * directLight.color;
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
	reflectedLight.directSpecular += irradiance * BRDF_BlinnPhong( directLight.direction, geometryViewDir, geometryNormal, material.specularColor, material.specularShininess ) * material.specularStrength;
}
void RE_IndirectDiffuse_BlinnPhong( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in BlinnPhongMaterial material, inout ReflectedLight reflectedLight ) {
	reflectedLight.indirectDiffuse += irradiance * BRDF_Lambert( material.diffuseColor );
}
#define RE_Direct				RE_Direct_BlinnPhong
#define RE_IndirectDiffuse		RE_IndirectDiffuse_BlinnPhong`,Cp=`PhysicalMaterial material;
material.diffuseColor = diffuseColor.rgb;
material.diffuseContribution = diffuseColor.rgb * ( 1.0 - metalnessFactor );
material.metalness = metalnessFactor;
vec3 dxy = max( abs( dFdx( nonPerturbedNormal ) ), abs( dFdy( nonPerturbedNormal ) ) );
float geometryRoughness = max( max( dxy.x, dxy.y ), dxy.z );
material.roughness = max( roughnessFactor, 0.0525 );material.roughness += geometryRoughness;
material.roughness = min( material.roughness, 1.0 );
#ifdef IOR
	material.ior = ior;
	#ifdef USE_SPECULAR
		float specularIntensityFactor = specularIntensity;
		vec3 specularColorFactor = specularColor;
		#ifdef USE_SPECULAR_COLORMAP
			specularColorFactor *= texture2D( specularColorMap, vSpecularColorMapUv ).rgb;
		#endif
		#ifdef USE_SPECULAR_INTENSITYMAP
			specularIntensityFactor *= texture2D( specularIntensityMap, vSpecularIntensityMapUv ).a;
		#endif
		material.specularF90 = mix( specularIntensityFactor, 1.0, metalnessFactor );
	#else
		float specularIntensityFactor = 1.0;
		vec3 specularColorFactor = vec3( 1.0 );
		material.specularF90 = 1.0;
	#endif
	material.specularColor = min( pow2( ( material.ior - 1.0 ) / ( material.ior + 1.0 ) ) * specularColorFactor, vec3( 1.0 ) ) * specularIntensityFactor;
	material.specularColorBlended = mix( material.specularColor, diffuseColor.rgb, metalnessFactor );
#else
	material.specularColor = vec3( 0.04 );
	material.specularColorBlended = mix( material.specularColor, diffuseColor.rgb, metalnessFactor );
	material.specularF90 = 1.0;
#endif
#ifdef USE_CLEARCOAT
	material.clearcoat = clearcoat;
	material.clearcoatRoughness = clearcoatRoughness;
	material.clearcoatF0 = vec3( 0.04 );
	material.clearcoatF90 = 1.0;
	#ifdef USE_CLEARCOATMAP
		material.clearcoat *= texture2D( clearcoatMap, vClearcoatMapUv ).x;
	#endif
	#ifdef USE_CLEARCOAT_ROUGHNESSMAP
		material.clearcoatRoughness *= texture2D( clearcoatRoughnessMap, vClearcoatRoughnessMapUv ).y;
	#endif
	material.clearcoat = saturate( material.clearcoat );	material.clearcoatRoughness = max( material.clearcoatRoughness, 0.0525 );
	material.clearcoatRoughness += geometryRoughness;
	material.clearcoatRoughness = min( material.clearcoatRoughness, 1.0 );
#endif
#ifdef USE_DISPERSION
	material.dispersion = dispersion;
#endif
#ifdef USE_RETROREFLECTION
	material.retroreflectivity = retroreflectivity;
#endif
#ifdef USE_IRIDESCENCE
	material.iridescence = iridescence;
	material.iridescenceIOR = iridescenceIOR;
	#ifdef USE_IRIDESCENCEMAP
		material.iridescence *= texture2D( iridescenceMap, vIridescenceMapUv ).r;
	#endif
	#ifdef USE_IRIDESCENCE_THICKNESSMAP
		material.iridescenceThickness = (iridescenceThicknessMaximum - iridescenceThicknessMinimum) * texture2D( iridescenceThicknessMap, vIridescenceThicknessMapUv ).g + iridescenceThicknessMinimum;
	#else
		material.iridescenceThickness = iridescenceThicknessMaximum;
	#endif
#endif
#ifdef USE_SHEEN
	material.sheenColor = sheenColor;
	#ifdef USE_SHEEN_COLORMAP
		material.sheenColor *= texture2D( sheenColorMap, vSheenColorMapUv ).rgb;
	#endif
	material.sheenRoughness = clamp( sheenRoughness, 0.0001, 1.0 );
	#ifdef USE_SHEEN_ROUGHNESSMAP
		material.sheenRoughness *= texture2D( sheenRoughnessMap, vSheenRoughnessMapUv ).a;
	#endif
#endif
#ifdef USE_ANISOTROPY
	#ifdef USE_ANISOTROPYMAP
		mat2 anisotropyMat = mat2( anisotropyVector.x, anisotropyVector.y, - anisotropyVector.y, anisotropyVector.x );
		vec3 anisotropyPolar = texture2D( anisotropyMap, vAnisotropyMapUv ).rgb;
		vec2 anisotropyV = anisotropyMat * normalize( 2.0 * anisotropyPolar.rg - vec2( 1.0 ) ) * anisotropyPolar.b;
	#else
		vec2 anisotropyV = anisotropyVector;
	#endif
	material.anisotropy = length( anisotropyV );
	if( material.anisotropy == 0.0 ) {
		anisotropyV = vec2( 1.0, 0.0 );
	} else {
		anisotropyV /= material.anisotropy;
		material.anisotropy = saturate( material.anisotropy );
	}
	material.alphaT = mix( pow2( material.roughness ), 1.0, pow2( material.anisotropy ) );
	material.anisotropyT = tbn[ 0 ] * anisotropyV.x + tbn[ 1 ] * anisotropyV.y;
	material.anisotropyB = tbn[ 1 ] * anisotropyV.x - tbn[ 0 ] * anisotropyV.y;
#endif`,Ip=`uniform sampler2D dfgLUT;
struct PhysicalMaterial {
	vec3 diffuseColor;
	vec3 diffuseContribution;
	vec3 specularColor;
	vec3 specularColorBlended;
	float roughness;
	float metalness;
	float specularF90;
	float dispersion;
	vec2 dfg;
	vec3 multiScatteringCompensation;
	#ifdef USE_RETROREFLECTION
		float retroreflectivity;
	#endif
	#ifdef USE_CLEARCOAT
		float clearcoat;
		float clearcoatRoughness;
		vec3 clearcoatF0;
		float clearcoatF90;
	#endif
	#ifdef USE_IRIDESCENCE
		float iridescence;
		float iridescenceIOR;
		float iridescenceThickness;
		vec3 iridescenceFresnel;
		vec3 iridescenceF0Dielectric;
		vec3 iridescenceF0Metallic;
	#endif
	#ifdef USE_SHEEN
		vec3 sheenColor;
		float sheenRoughness;
	#endif
	#ifdef IOR
		float ior;
	#endif
	#ifdef USE_TRANSMISSION
		float transmission;
		float transmissionAlpha;
		float thickness;
		float attenuationDistance;
		vec3 attenuationColor;
	#endif
	#ifdef USE_ANISOTROPY
		float anisotropy;
		float alphaT;
		vec3 anisotropyT;
		vec3 anisotropyB;
	#endif
};
vec3 clearcoatSpecularDirect = vec3( 0.0 );
vec3 clearcoatSpecularIndirect = vec3( 0.0 );
vec3 sheenSpecularDirect = vec3( 0.0 );
vec3 sheenSpecularIndirect = vec3(0.0 );
vec3 Schlick_to_F0( const in vec3 f, const in float f90, const in float dotVH ) {
    float x = clamp( 1.0 - dotVH, 0.0, 1.0 );
    float x2 = x * x;
    float x5 = clamp( x * x2 * x2, 0.0, 0.9999 );
    return ( f - vec3( f90 ) * x5 ) / ( 1.0 - x5 );
}
float V_GGX_SmithCorrelated( const in float alpha, const in float dotNL, const in float dotNV ) {
	float a2 = pow2( alpha );
	float gv = dotNL * sqrt( a2 + ( 1.0 - a2 ) * pow2( dotNV ) );
	float gl = dotNV * sqrt( a2 + ( 1.0 - a2 ) * pow2( dotNL ) );
	return 0.5 / max( gv + gl, EPSILON );
}
float D_GGX( const in float alpha, const in float dotNH ) {
	float a2 = pow2( alpha );
	float denom = pow2( dotNH ) * ( a2 - 1.0 ) + 1.0;
	return RECIPROCAL_PI * a2 / pow2( denom );
}
#ifdef USE_ANISOTROPY
	float V_GGX_SmithCorrelated_Anisotropic( const in float alphaT, const in float alphaB, const in float dotTV, const in float dotBV, const in float dotTL, const in float dotBL, const in float dotNV, const in float dotNL ) {
		float gv = dotNL * length( vec3( alphaT * dotTV, alphaB * dotBV, dotNV ) );
		float gl = dotNV * length( vec3( alphaT * dotTL, alphaB * dotBL, dotNL ) );
		return 0.5 / max( gv + gl, EPSILON );
	}
	float D_GGX_Anisotropic( const in float alphaT, const in float alphaB, const in float dotNH, const in float dotTH, const in float dotBH ) {
		float a2 = alphaT * alphaB;
		highp vec3 v = vec3( alphaB * dotTH, alphaT * dotBH, a2 * dotNH );
		highp float v2 = dot( v, v );
		float w2 = a2 / v2;
		return RECIPROCAL_PI * a2 * pow2 ( w2 );
	}
#endif
#ifdef USE_CLEARCOAT
	vec3 BRDF_GGX_Clearcoat( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, const in PhysicalMaterial material) {
		vec3 f0 = material.clearcoatF0;
		float f90 = material.clearcoatF90;
		float roughness = material.clearcoatRoughness;
		float alpha = pow2( roughness );
		vec3 halfDir = normalize( lightDir + viewDir );
		float dotNL = saturate( dot( normal, lightDir ) );
		float dotNV = saturate( dot( normal, viewDir ) );
		float dotNH = saturate( dot( normal, halfDir ) );
		float dotVH = saturate( dot( viewDir, halfDir ) );
		vec3 F = F_Schlick( f0, f90, dotVH );
		float V = V_GGX_SmithCorrelated( alpha, dotNL, dotNV );
		float D = D_GGX( alpha, dotNH );
		return F * ( V * D );
	}
#endif
vec3 BRDF_GGX( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, const in PhysicalMaterial material ) {
	vec3 f0 = material.specularColorBlended;
	float f90 = material.specularF90;
	float roughness = material.roughness;
	float alpha = pow2( roughness );
	vec3 halfDir = normalize( lightDir + viewDir );
	float dotNL = saturate( dot( normal, lightDir ) );
	float dotNV = saturate( dot( normal, viewDir ) );
	float dotNH = saturate( dot( normal, halfDir ) );
	float dotVH = saturate( dot( viewDir, halfDir ) );
	vec3 F = F_Schlick( f0, f90, dotVH );
	#ifdef USE_IRIDESCENCE
		F = mix( F, material.iridescenceFresnel, material.iridescence );
	#endif
	#ifdef USE_ANISOTROPY
		float dotTL = dot( material.anisotropyT, lightDir );
		float dotTV = dot( material.anisotropyT, viewDir );
		float dotTH = dot( material.anisotropyT, halfDir );
		float dotBL = dot( material.anisotropyB, lightDir );
		float dotBV = dot( material.anisotropyB, viewDir );
		float dotBH = dot( material.anisotropyB, halfDir );
		float V = V_GGX_SmithCorrelated_Anisotropic( material.alphaT, alpha, dotTV, dotBV, dotTL, dotBL, dotNV, dotNL );
		float D = D_GGX_Anisotropic( material.alphaT, alpha, dotNH, dotTH, dotBH );
	#else
		float V = V_GGX_SmithCorrelated( alpha, dotNL, dotNV );
		float D = D_GGX( alpha, dotNH );
	#endif
	return F * ( V * D );
}
vec2 LTC_Uv( const in vec3 N, const in vec3 V, const in float roughness ) {
	const float LUT_SIZE = 64.0;
	const float LUT_SCALE = ( LUT_SIZE - 1.0 ) / LUT_SIZE;
	const float LUT_BIAS = 0.5 / LUT_SIZE;
	float dotNV = saturate( dot( N, V ) );
	vec2 uv = vec2( roughness, sqrt( 1.0 - dotNV ) );
	uv = uv * LUT_SCALE + LUT_BIAS;
	return uv;
}
float LTC_ClippedSphereFormFactor( const in vec3 f ) {
	float l = length( f );
	return max( ( l * l + f.z ) / ( l + 1.0 ), 0.0 );
}
vec3 LTC_EdgeVectorFormFactor( const in vec3 v1, const in vec3 v2 ) {
	float x = dot( v1, v2 );
	float y = abs( x );
	float a = 0.8543985 + ( 0.4965155 + 0.0145206 * y ) * y;
	float b = 3.4175940 + ( 4.1616724 + y ) * y;
	float v = a / b;
	float theta_sintheta = ( x > 0.0 ) ? v : 0.5 * inversesqrt( max( 1.0 - x * x, 1e-7 ) ) - v;
	return cross( v1, v2 ) * theta_sintheta;
}
vec3 LTC_Evaluate( const in vec3 N, const in vec3 V, const in vec3 P, const in mat3 mInv, const in vec3 rectCoords[ 4 ] ) {
	vec3 v1 = rectCoords[ 1 ] - rectCoords[ 0 ];
	vec3 v2 = rectCoords[ 3 ] - rectCoords[ 0 ];
	vec3 lightNormal = cross( v1, v2 );
	if( dot( lightNormal, P - rectCoords[ 0 ] ) < 0.0 ) return vec3( 0.0 );
	vec3 T1, T2;
	T1 = normalize( V - N * dot( V, N ) );
	T2 = - cross( N, T1 );
	mat3 mat = mInv * transpose( mat3( T1, T2, N ) );
	vec3 coords[ 4 ];
	coords[ 0 ] = mat * ( rectCoords[ 0 ] - P );
	coords[ 1 ] = mat * ( rectCoords[ 1 ] - P );
	coords[ 2 ] = mat * ( rectCoords[ 2 ] - P );
	coords[ 3 ] = mat * ( rectCoords[ 3 ] - P );
	coords[ 0 ] = normalize( coords[ 0 ] );
	coords[ 1 ] = normalize( coords[ 1 ] );
	coords[ 2 ] = normalize( coords[ 2 ] );
	coords[ 3 ] = normalize( coords[ 3 ] );
	vec3 vectorFormFactor = vec3( 0.0 );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 0 ], coords[ 1 ] );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 1 ], coords[ 2 ] );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 2 ], coords[ 3 ] );
	vectorFormFactor += LTC_EdgeVectorFormFactor( coords[ 3 ], coords[ 0 ] );
	float result = LTC_ClippedSphereFormFactor( vectorFormFactor );
	return vec3( result );
}
#if defined( USE_SHEEN )
float D_Charlie( float roughness, float dotNH ) {
	float alpha = pow2( roughness );
	float invAlpha = 1.0 / alpha;
	float cos2h = dotNH * dotNH;
	float sin2h = max( 1.0 - cos2h, 0.0078125 );
	return ( 2.0 + invAlpha ) * pow( sin2h, invAlpha * 0.5 ) / ( 2.0 * PI );
}
float V_Neubelt( float dotNV, float dotNL ) {
	return saturate( 1.0 / ( 4.0 * ( dotNL + dotNV - dotNL * dotNV ) ) );
}
vec3 BRDF_Sheen( const in vec3 lightDir, const in vec3 viewDir, const in vec3 normal, vec3 sheenColor, const in float sheenRoughness ) {
	vec3 halfDir = normalize( lightDir + viewDir );
	float dotNL = saturate( dot( normal, lightDir ) );
	float dotNV = saturate( dot( normal, viewDir ) );
	float dotNH = saturate( dot( normal, halfDir ) );
	float D = D_Charlie( sheenRoughness, dotNH );
	float V = V_Neubelt( dotNV, dotNL );
	return sheenColor * ( D * V );
}
#endif
float IBLSheenBRDF( const in vec3 normal, const in vec3 viewDir, const in float roughness ) {
	float dotNV = saturate( dot( normal, viewDir ) );
	float r2 = roughness * roughness;
	float rInv = 1.0 / ( roughness + 0.1 );
	float a = -1.9362 + 1.0678 * roughness + 0.4573 * r2 - 0.8469 * rInv;
	float b = -0.6014 + 0.5538 * roughness - 0.4670 * r2 - 0.1255 * rInv;
	float DG = exp( a * dotNV + b );
	return saturate( DG );
}
vec3 EnvironmentBRDF( const in vec3 normal, const in vec3 viewDir, const in vec3 specularColor, const in float specularF90, const in float roughness ) {
	float dotNV = saturate( dot( normal, viewDir ) );
	vec2 fab = texture2D( dfgLUT, vec2( roughness, dotNV ) ).rg;
	return specularColor * fab.x + specularF90 * fab.y;
}
#ifdef USE_IRIDESCENCE
void computeMultiscatteringIridescence( const in vec2 fab, const in vec3 specularColor, const in float specularF90, const in float iridescence, const in vec3 iridescenceF0, inout vec3 singleScatter, inout vec3 multiScatter ) {
#else
void computeMultiscattering( const in vec2 fab, const in vec3 specularColor, const in float specularF90, inout vec3 singleScatter, inout vec3 multiScatter ) {
#endif
	#ifdef USE_IRIDESCENCE
		vec3 Fr = mix( specularColor, iridescenceF0, iridescence );
	#else
		vec3 Fr = specularColor;
	#endif
	vec3 FssEss = Fr * fab.x + specularF90 * fab.y;
	float Ess = fab.x + fab.y;
	float Ems = 1.0 - Ess;
	vec3 Favg = Fr + ( 1.0 - Fr ) * 0.047619;	vec3 Fms = FssEss * Favg / ( 1.0 - Ems * Favg );
	singleScatter += FssEss;
	multiScatter += Fms * Ems;
}
#if NUM_RECT_AREA_LIGHTS > 0
	void RE_Direct_RectArea_Physical( const in RectAreaLight rectAreaLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight ) {
		vec3 normal = geometryNormal;
		vec3 viewDir = geometryViewDir;
		vec3 position = geometryPosition;
		vec3 lightPos = rectAreaLight.position;
		vec3 halfWidth = rectAreaLight.halfWidth;
		vec3 halfHeight = rectAreaLight.halfHeight;
		vec3 lightColor = rectAreaLight.color;
		float roughness = material.roughness;
		vec3 rectCoords[ 4 ];
		rectCoords[ 0 ] = lightPos + halfWidth - halfHeight;		rectCoords[ 1 ] = lightPos - halfWidth - halfHeight;
		rectCoords[ 2 ] = lightPos - halfWidth + halfHeight;
		rectCoords[ 3 ] = lightPos + halfWidth + halfHeight;
		vec2 uv = LTC_Uv( normal, viewDir, roughness );
		vec4 t1 = texture2D( ltc_1, uv );
		vec4 t2 = texture2D( ltc_2, uv );
		mat3 mInv = mat3(
			vec3( t1.x, 0, t1.y ),
			vec3(    0, 1,    0 ),
			vec3( t1.z, 0, t1.w )
		);
		vec3 fresnel = ( material.specularColorBlended * t2.x + ( material.specularF90 - material.specularColorBlended ) * t2.y );
		reflectedLight.directSpecular += lightColor * fresnel * LTC_Evaluate( normal, viewDir, position, mInv, rectCoords );
		reflectedLight.directDiffuse += lightColor * material.diffuseContribution * LTC_Evaluate( normal, viewDir, position, mat3( 1.0 ), rectCoords );
		#ifdef USE_CLEARCOAT
			vec3 Ncc = geometryClearcoatNormal;
			vec2 uvClearcoat = LTC_Uv( Ncc, viewDir, material.clearcoatRoughness );
			vec4 t1Clearcoat = texture2D( ltc_1, uvClearcoat );
			vec4 t2Clearcoat = texture2D( ltc_2, uvClearcoat );
			mat3 mInvClearcoat = mat3(
				vec3( t1Clearcoat.x, 0, t1Clearcoat.y ),
				vec3(             0, 1,             0 ),
				vec3( t1Clearcoat.z, 0, t1Clearcoat.w )
			);
			vec3 fresnelClearcoat = material.clearcoatF0 * t2Clearcoat.x + ( material.clearcoatF90 - material.clearcoatF0 ) * t2Clearcoat.y;
			clearcoatSpecularDirect += lightColor * fresnelClearcoat * LTC_Evaluate( Ncc, viewDir, position, mInvClearcoat, rectCoords );
		#endif
	}
#endif
void RE_Direct_Physical( const in IncidentLight directLight, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight ) {
	float dotNL = saturate( dot( geometryNormal, directLight.direction ) );
	vec3 irradiance = dotNL * directLight.color;
	#ifdef USE_CLEARCOAT
		float dotNLcc = saturate( dot( geometryClearcoatNormal, directLight.direction ) );
		vec3 ccIrradiance = dotNLcc * directLight.color;
		clearcoatSpecularDirect += ccIrradiance * BRDF_GGX_Clearcoat( directLight.direction, geometryViewDir, geometryClearcoatNormal, material );
	#endif
	#ifdef USE_SHEEN
 
 		sheenSpecularDirect += irradiance * BRDF_Sheen( directLight.direction, geometryViewDir, geometryNormal, material.sheenColor, material.sheenRoughness );
 
 		float sheenAlbedoV = IBLSheenBRDF( geometryNormal, geometryViewDir, material.sheenRoughness );
 		float sheenAlbedoL = IBLSheenBRDF( geometryNormal, directLight.direction, material.sheenRoughness );
 
 		float sheenEnergyComp = 1.0 - max3( material.sheenColor ) * max( sheenAlbedoV, sheenAlbedoL );
 
 		irradiance *= sheenEnergyComp;
 
 	#endif
	vec3 specularBRDF = BRDF_GGX( directLight.direction, geometryViewDir, geometryNormal, material );
	#ifdef USE_RETROREFLECTION
		vec3 retroViewDir = reflect( - geometryViewDir, geometryNormal );
		vec3 retroSpecularBRDF = BRDF_GGX( directLight.direction, retroViewDir, geometryNormal, material );
		specularBRDF = mix( specularBRDF, retroSpecularBRDF, saturate( material.retroreflectivity ) );
	#endif
	reflectedLight.directSpecular += irradiance * specularBRDF * material.multiScatteringCompensation;
	vec3 halfDir = normalize( directLight.direction + geometryViewDir );
	float dotVH = saturate( dot( geometryViewDir, halfDir ) );
	vec3 F = F_Schlick( material.specularColor, material.specularF90, dotVH );
	#ifdef USE_RETROREFLECTION
		vec3 retroHalfDir = normalize( directLight.direction + retroViewDir );
		float dotRetroVH = saturate( dot( retroViewDir, retroHalfDir ) );
		vec3 retroF = F_Schlick( material.specularColor, material.specularF90, dotRetroVH );
		F = mix( F, retroF, saturate( material.retroreflectivity ) );
	#endif
	reflectedLight.directDiffuse += irradiance * BRDF_Lambert( material.diffuseContribution ) * ( 1.0 - F );
}
void RE_IndirectDiffuse_Physical( const in vec3 irradiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight ) {
	vec3 singleScattering = vec3( 0.0 );
	vec3 multiScattering = vec3( 0.0 );
	#ifdef USE_IRIDESCENCE
		computeMultiscatteringIridescence( material.dfg, material.specularColor, material.specularF90, material.iridescence, material.iridescenceF0Dielectric, singleScattering, multiScattering );
	#else
		computeMultiscattering( material.dfg, material.specularColor, material.specularF90, singleScattering, multiScattering );
	#endif
	vec3 diffuse = irradiance * BRDF_Lambert( material.diffuseContribution ) * ( 1.0 - singleScattering - multiScattering );
	#ifdef USE_SHEEN
		float sheenAlbedo = IBLSheenBRDF( geometryNormal, geometryViewDir, material.sheenRoughness );
		sheenSpecularIndirect += irradiance * material.sheenColor * sheenAlbedo * RECIPROCAL_PI;
		float sheenEnergyComp = 1.0 - max3( material.sheenColor ) * sheenAlbedo;
		diffuse *= sheenEnergyComp;
	#endif
	reflectedLight.indirectDiffuse += diffuse;
}
void RE_IndirectSpecular_Physical( const in vec3 radiance, const in vec3 irradiance, const in vec3 clearcoatRadiance, const in vec3 geometryPosition, const in vec3 geometryNormal, const in vec3 geometryViewDir, const in vec3 geometryClearcoatNormal, const in PhysicalMaterial material, inout ReflectedLight reflectedLight) {
	#ifdef USE_CLEARCOAT
		clearcoatSpecularIndirect += clearcoatRadiance * EnvironmentBRDF( geometryClearcoatNormal, geometryViewDir, material.clearcoatF0, material.clearcoatF90, material.clearcoatRoughness );
	#endif
	#ifdef USE_SHEEN
		sheenSpecularIndirect += irradiance * material.sheenColor * IBLSheenBRDF( geometryNormal, geometryViewDir, material.sheenRoughness ) * RECIPROCAL_PI;
 	#endif
	vec3 singleScatteringDielectric = vec3( 0.0 );
	vec3 multiScatteringDielectric = vec3( 0.0 );
	vec3 singleScatteringMetallic = vec3( 0.0 );
	vec3 multiScatteringMetallic = vec3( 0.0 );
	#ifdef USE_IRIDESCENCE
		computeMultiscatteringIridescence( material.dfg, material.specularColor, material.specularF90, material.iridescence, material.iridescenceF0Dielectric, singleScatteringDielectric, multiScatteringDielectric );
		computeMultiscatteringIridescence( material.dfg, material.diffuseColor, material.specularF90, material.iridescence, material.iridescenceF0Metallic, singleScatteringMetallic, multiScatteringMetallic );
	#else
		computeMultiscattering( material.dfg, material.specularColor, material.specularF90, singleScatteringDielectric, multiScatteringDielectric );
		computeMultiscattering( material.dfg, material.diffuseColor, material.specularF90, singleScatteringMetallic, multiScatteringMetallic );
	#endif
	vec3 singleScattering = mix( singleScatteringDielectric, singleScatteringMetallic, material.metalness );
	vec3 multiScattering = mix( multiScatteringDielectric, multiScatteringMetallic, material.metalness );
	vec3 totalScatteringDielectric = singleScatteringDielectric + multiScatteringDielectric;
	vec3 diffuse = material.diffuseContribution * ( 1.0 - totalScatteringDielectric );
	vec3 cosineWeightedIrradiance = irradiance * RECIPROCAL_PI;
	vec3 indirectSpecular = radiance * singleScattering;
	indirectSpecular += multiScattering * cosineWeightedIrradiance;
	vec3 indirectDiffuse = diffuse * cosineWeightedIrradiance;
	#ifdef USE_SHEEN
		float sheenAlbedo = IBLSheenBRDF( geometryNormal, geometryViewDir, material.sheenRoughness );
		float sheenEnergyComp = 1.0 - max3( material.sheenColor ) * sheenAlbedo;
		indirectSpecular *= sheenEnergyComp;
		indirectDiffuse *= sheenEnergyComp;
	#endif
	reflectedLight.indirectSpecular += indirectSpecular;
	reflectedLight.indirectDiffuse += indirectDiffuse;
}
#define RE_Direct				RE_Direct_Physical
#define RE_Direct_RectArea		RE_Direct_RectArea_Physical
#define RE_IndirectDiffuse		RE_IndirectDiffuse_Physical
#define RE_IndirectSpecular		RE_IndirectSpecular_Physical
float computeSpecularOcclusion( const in float dotNV, const in float ambientOcclusion, const in float roughness ) {
	return saturate( pow( dotNV + ambientOcclusion, exp2( - 16.0 * roughness - 1.0 ) ) - 1.0 + ambientOcclusion );
}`,Pp=`
vec3 geometryPosition = - vViewPosition;
vec3 geometryNormal = normal;
vec3 geometryViewDir = ( isOrthographic ) ? vec3( 0, 0, 1 ) : normalize( vViewPosition );
vec3 geometryClearcoatNormal = vec3( 0.0 );
#ifdef USE_CLEARCOAT
	geometryClearcoatNormal = clearcoatNormal;
#endif
#ifdef USE_IRIDESCENCE
	float dotNVi = saturate( dot( normal, geometryViewDir ) );
	if ( material.iridescenceThickness == 0.0 ) {
		material.iridescence = 0.0;
	} else {
		material.iridescence = saturate( material.iridescence );
	}
	if ( material.iridescence > 0.0 ) {
		vec3 iridescenceFresnelDielectric = evalIridescence( 1.0, material.iridescenceIOR, dotNVi, material.iridescenceThickness, material.specularColor );
		vec3 iridescenceFresnelMetallic = evalIridescence( 1.0, material.iridescenceIOR, dotNVi, material.iridescenceThickness, material.diffuseColor );
		material.iridescenceFresnel = mix( iridescenceFresnelDielectric, iridescenceFresnelMetallic, material.metalness );
		material.iridescenceF0Dielectric = Schlick_to_F0( iridescenceFresnelDielectric, 1.0, dotNVi );
		material.iridescenceF0Metallic = Schlick_to_F0( iridescenceFresnelMetallic, 1.0, dotNVi );
	}
#endif
#ifdef STANDARD
	float dotNVms = saturate( dot( geometryNormal, geometryViewDir ) );
	material.dfg = texture2D( dfgLUT, vec2( material.roughness, dotNVms ) ).rg;
	#if ( NUM_SUN_LIGHTS > 0 || NUM_DIR_LIGHTS > 0 || NUM_POINT_LIGHTS > 0 || NUM_SPOT_LIGHTS > 0 )
		float EssMs = material.dfg.x + material.dfg.y;
		material.multiScatteringCompensation = 1.0 + material.specularColorBlended * ( 1.0 / EssMs - 1.0 );
	#endif
#endif
IncidentLight directLight;
#if ( NUM_POINT_LIGHTS > 0 ) && defined( RE_Direct )
	PointLight pointLight;
	#if defined( USE_SHADOWMAP ) && NUM_POINT_LIGHT_SHADOWS > 0
	PointLightShadow pointLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_POINT_LIGHTS; i ++ ) {
		pointLight = pointLights[ i ];
		getPointLightInfo( pointLight, geometryPosition, directLight );
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_POINT_LIGHT_SHADOWS ) && ( defined( SHADOWMAP_TYPE_PCF ) || defined( SHADOWMAP_TYPE_BASIC ) )
		pointLightShadow = pointLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getPointShadow( pointShadowMap[ i ], pointLightShadow.shadowMapSize, pointLightShadow.shadowIntensity, pointLightShadow.shadowBias, pointLightShadow.shadowRadius, vPointShadowCoord[ i ], pointLightShadow.shadowCameraNear, pointLightShadow.shadowCameraFar ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_SPOT_LIGHTS > 0 ) && defined( RE_Direct )
	SpotLight spotLight;
	vec4 spotColor;
	vec3 spotLightCoord;
	bool inSpotLightMap;
	#if defined( USE_SHADOWMAP ) && NUM_SPOT_LIGHT_SHADOWS > 0
	SpotLightShadow spotLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SPOT_LIGHTS; i ++ ) {
		spotLight = spotLights[ i ];
		getSpotLightInfo( spotLight, geometryPosition, directLight );
		#if ( UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS_WITH_MAPS )
		#define SPOT_LIGHT_MAP_INDEX UNROLLED_LOOP_INDEX
		#elif ( UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS )
		#define SPOT_LIGHT_MAP_INDEX NUM_SPOT_LIGHT_MAPS
		#else
		#define SPOT_LIGHT_MAP_INDEX ( UNROLLED_LOOP_INDEX - NUM_SPOT_LIGHT_SHADOWS + NUM_SPOT_LIGHT_SHADOWS_WITH_MAPS )
		#endif
		#if ( SPOT_LIGHT_MAP_INDEX < NUM_SPOT_LIGHT_MAPS )
			spotLightCoord = vSpotLightCoord[ i ].xyz / vSpotLightCoord[ i ].w;
			inSpotLightMap = all( lessThan( abs( spotLightCoord * 2. - 1. ), vec3( 1.0 ) ) );
			spotColor = texture2D( spotLightMap[ SPOT_LIGHT_MAP_INDEX ], spotLightCoord.xy );
			directLight.color = inSpotLightMap ? directLight.color * spotColor.rgb : directLight.color;
		#endif
		#undef SPOT_LIGHT_MAP_INDEX
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS )
		spotLightShadow = spotLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getShadow( spotShadowMap[ i ], spotLightShadow.shadowMapSize, spotLightShadow.shadowIntensity, spotLightShadow.shadowBias, spotLightShadow.shadowRadius, vSpotLightCoord[ i ] ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_SUN_LIGHTS > 0 ) && defined( RE_Direct )
	SunLight sunLight;
	#if defined( USE_SHADOWMAP ) && NUM_SUN_LIGHT_SHADOWS > 0
	SunLightShadow sunLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SUN_LIGHTS; i ++ ) {
		sunLight = sunLights[ i ];
		getSunLightInfo( sunLight, directLight );
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_SUN_LIGHT_SHADOWS )
		sunLightShadow = sunLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getSunShadow( sunShadowMap[ i ], sunLightShadow, UNROLLED_LOOP_INDEX ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_DIR_LIGHTS > 0 ) && defined( RE_Direct )
	DirectionalLight directionalLight;
	#if defined( USE_SHADOWMAP ) && NUM_DIR_LIGHT_SHADOWS > 0
	DirectionalLightShadow directionalLightShadow;
	#endif
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_DIR_LIGHTS; i ++ ) {
		directionalLight = directionalLights[ i ];
		getDirectionalLightInfo( directionalLight, directLight );
		#if defined( USE_SHADOWMAP ) && ( UNROLLED_LOOP_INDEX < NUM_DIR_LIGHT_SHADOWS )
		directionalLightShadow = directionalLightShadows[ i ];
		directLight.color *= ( directLight.visible && receiveShadow ) ? getShadow( directionalShadowMap[ i ], directionalLightShadow.shadowMapSize, directionalLightShadow.shadowIntensity, directionalLightShadow.shadowBias, directionalLightShadow.shadowRadius, vDirectionalShadowCoord[ i ] ) : 1.0;
		#endif
		RE_Direct( directLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if ( NUM_RECT_AREA_LIGHTS > 0 ) && defined( RE_Direct_RectArea )
	RectAreaLight rectAreaLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_RECT_AREA_LIGHTS; i ++ ) {
		rectAreaLight = rectAreaLights[ i ];
		RE_Direct_RectArea( rectAreaLight, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
	}
	#pragma unroll_loop_end
#endif
#if defined( RE_IndirectDiffuse )
	vec3 iblIrradiance = vec3( 0.0 );
	vec3 irradiance = getAmbientLightIrradiance( ambientLightColor );
	#if defined( USE_LIGHT_PROBES )
		irradiance += getLightProbeIrradiance( lightProbe, geometryNormal );
	#endif
	#if ( NUM_HEMI_LIGHTS > 0 )
		#pragma unroll_loop_start
		for ( int i = 0; i < NUM_HEMI_LIGHTS; i ++ ) {
			irradiance += getHemisphereLightIrradiance( hemisphereLights[ i ], geometryNormal );
		}
		#pragma unroll_loop_end
	#endif
	#ifdef USE_LIGHT_PROBES_GRID
		vec3 probeWorldPos = ( ( vec4( geometryPosition, 1.0 ) - viewMatrix[ 3 ] ) * viewMatrix ).xyz;
		vec3 probeWorldNormal = transformNormalByInverseViewMatrix( geometryNormal, viewMatrix );
		irradiance += getLightProbeGridIrradiance( probeWorldPos, probeWorldNormal );
	#endif
#endif
#if defined( RE_IndirectSpecular )
	vec3 radiance = vec3( 0.0 );
	vec3 clearcoatRadiance = vec3( 0.0 );
#endif`,Lp=`#if defined( RE_IndirectDiffuse )
	#ifdef USE_LIGHTMAP
		vec4 lightMapTexel = texture2D( lightMap, vLightMapUv );
		vec3 lightMapIrradiance = lightMapTexel.rgb * lightMapIntensity;
		irradiance += lightMapIrradiance;
	#endif
	#if defined( USE_ENVMAP ) && defined( ENVMAP_TYPE_CUBE_UV )
		#if defined( STANDARD ) || defined( LAMBERT ) || defined( PHONG )
			iblIrradiance += getIBLIrradiance( geometryNormal );
		#endif
	#endif
#endif
#if defined( USE_ENVMAP ) && defined( RE_IndirectSpecular )
	#ifdef USE_ANISOTROPY
		vec3 iblRadiance = getIBLAnisotropyRadiance( geometryViewDir, geometryNormal, material.roughness, material.anisotropyB, material.anisotropy );
	#else
		vec3 iblRadiance = getIBLRadiance( geometryViewDir, geometryNormal, material.roughness );
	#endif
	#ifdef USE_RETROREFLECTION
		#ifdef USE_ANISOTROPY
			vec3 retroIBLRadiance = getIBLAnisotropyRetroRadiance( geometryViewDir, geometryNormal, material.roughness, material.anisotropyB, material.anisotropy );
		#else
			vec3 retroIBLRadiance = getIBLRetroRadiance( geometryViewDir, geometryNormal, material.roughness );
		#endif
		iblRadiance = mix( iblRadiance, retroIBLRadiance, saturate( material.retroreflectivity ) );
	#endif
	radiance += iblRadiance;
	#ifdef USE_CLEARCOAT
		clearcoatRadiance += getIBLRadiance( geometryViewDir, geometryClearcoatNormal, material.clearcoatRoughness );
	#endif
#endif`,Dp=`#if defined( RE_IndirectDiffuse )
	#if defined( LAMBERT ) || defined( PHONG )
		irradiance += iblIrradiance;
	#endif
	RE_IndirectDiffuse( irradiance, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
#endif
#if defined( RE_IndirectSpecular )
	RE_IndirectSpecular( radiance, iblIrradiance, clearcoatRadiance, geometryPosition, geometryNormal, geometryViewDir, geometryClearcoatNormal, material, reflectedLight );
#endif`,Up=`#ifdef USE_LIGHT_PROBES_GRID
uniform highp sampler3D probesSH;
uniform vec3 probesMin;
uniform vec3 probesMax;
uniform vec3 probesResolution;
vec3 getLightProbeGridIrradiance( vec3 worldPos, vec3 worldNormal ) {
	vec3 res = probesResolution;
	vec3 gridRange = probesMax - probesMin;
	vec3 resMinusOne = res - 1.0;
	vec3 probeSpacing = gridRange / resMinusOne;
	vec3 samplePos = worldPos + worldNormal * probeSpacing * 0.5;
	vec3 uvw = clamp( ( samplePos - probesMin ) / gridRange, 0.0, 1.0 );
	uvw = uvw * resMinusOne / res + 0.5 / res;
	float nz          = res.z;
	float paddedSlices = nz + 2.0;
	float atlasDepth  = 7.0 * paddedSlices;
	float uvZBase     = uvw.z * nz + 1.0;
	vec4 s0 = texture( probesSH, vec3( uvw.xy, ( uvZBase                       ) / atlasDepth ) );
	vec4 s1 = texture( probesSH, vec3( uvw.xy, ( uvZBase +       paddedSlices   ) / atlasDepth ) );
	vec4 s2 = texture( probesSH, vec3( uvw.xy, ( uvZBase + 2.0 * paddedSlices   ) / atlasDepth ) );
	vec4 s3 = texture( probesSH, vec3( uvw.xy, ( uvZBase + 3.0 * paddedSlices   ) / atlasDepth ) );
	vec4 s4 = texture( probesSH, vec3( uvw.xy, ( uvZBase + 4.0 * paddedSlices   ) / atlasDepth ) );
	vec4 s5 = texture( probesSH, vec3( uvw.xy, ( uvZBase + 5.0 * paddedSlices   ) / atlasDepth ) );
	vec4 s6 = texture( probesSH, vec3( uvw.xy, ( uvZBase + 6.0 * paddedSlices   ) / atlasDepth ) );
	vec3 c0 = s0.xyz;
	vec3 c1 = vec3( s0.w, s1.xy );
	vec3 c2 = vec3( s1.zw, s2.x );
	vec3 c3 = s2.yzw;
	vec3 c4 = s3.xyz;
	vec3 c5 = vec3( s3.w, s4.xy );
	vec3 c6 = vec3( s4.zw, s5.x );
	vec3 c7 = s5.yzw;
	vec3 c8 = s6.xyz;
	float x = worldNormal.x, y = worldNormal.y, z = worldNormal.z;
	vec3 result = c0 * 0.886227;
	result += c1 * 2.0 * 0.511664 * y;
	result += c2 * 2.0 * 0.511664 * z;
	result += c3 * 2.0 * 0.511664 * x;
	result += c4 * 2.0 * 0.429043 * x * y;
	result += c5 * 2.0 * 0.429043 * y * z;
	result += c6 * ( 0.743125 * z * z - 0.247708 );
	result += c7 * 2.0 * 0.429043 * x * z;
	result += c8 * 0.429043 * ( x * x - y * y );
	return max( result, vec3( 0.0 ) );
}
#endif`,Np=`#if defined( USE_LOGARITHMIC_DEPTH_BUFFER )
	gl_FragDepth = vIsPerspective == 0.0 ? gl_FragCoord.z : log2( vFragDepth ) * logDepthBufFC * 0.5;
#endif`,Fp=`#if defined( USE_LOGARITHMIC_DEPTH_BUFFER )
	uniform float logDepthBufFC;
	varying float vFragDepth;
	varying float vIsPerspective;
#endif`,Op=`#ifdef USE_LOGARITHMIC_DEPTH_BUFFER
	varying float vFragDepth;
	varying float vIsPerspective;
#endif`,Bp=`#ifdef USE_LOGARITHMIC_DEPTH_BUFFER
	vFragDepth = 1.0 + gl_Position.w;
	vIsPerspective = float( isPerspectiveMatrix( projectionMatrix ) );
#endif`,zp=`#ifdef USE_MAP
	vec4 sampledDiffuseColor = texture2D( map, vMapUv );
	#ifdef DECODE_VIDEO_TEXTURE
		sampledDiffuseColor = sRGBTransferEOTF( sampledDiffuseColor );
	#endif
	diffuseColor *= sampledDiffuseColor;
#endif`,Hp=`#ifdef USE_MAP
	uniform sampler2D map;
#endif`,Vp=`#if defined( USE_MAP ) || defined( USE_ALPHAMAP )
	#if defined( USE_POINTS_UV )
		vec2 uv = vUv;
	#else
		vec2 uv = ( uvTransform * vec3( gl_PointCoord.x, 1.0 - gl_PointCoord.y, 1 ) ).xy;
	#endif
#endif
#ifdef USE_MAP
	diffuseColor *= texture2D( map, uv );
#endif
#ifdef USE_ALPHAMAP
	diffuseColor.a *= texture2D( alphaMap, uv ).g;
#endif`,kp=`#if defined( USE_POINTS_UV )
	varying vec2 vUv;
#else
	#if defined( USE_MAP ) || defined( USE_ALPHAMAP )
		uniform mat3 uvTransform;
	#endif
#endif
#ifdef USE_MAP
	uniform sampler2D map;
#endif
#ifdef USE_ALPHAMAP
	uniform sampler2D alphaMap;
#endif`,Gp=`float metalnessFactor = metalness;
#ifdef USE_METALNESSMAP
	vec4 texelMetalness = texture2D( metalnessMap, vMetalnessMapUv );
	metalnessFactor *= texelMetalness.b;
#endif`,Wp=`#ifdef USE_METALNESSMAP
	uniform sampler2D metalnessMap;
#endif`,Xp=`#ifdef USE_INSTANCING_MORPH
	float morphTargetInfluences[ MORPHTARGETS_COUNT ];
	float morphTargetBaseInfluence = texelFetch( morphTexture, ivec2( 0, gl_InstanceID ), 0 ).r;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		morphTargetInfluences[i] =  texelFetch( morphTexture, ivec2( i + 1, gl_InstanceID ), 0 ).r;
	}
#endif`,qp=`#if defined( USE_MORPHCOLORS )
	vColor *= morphTargetBaseInfluence;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		#if defined( USE_COLOR_ALPHA )
			if ( morphTargetInfluences[ i ] != 0.0 ) vColor += getMorph( gl_VertexID, i, 2 ) * morphTargetInfluences[ i ];
		#elif defined( USE_COLOR )
			if ( morphTargetInfluences[ i ] != 0.0 ) vColor += getMorph( gl_VertexID, i, 2 ).rgb * morphTargetInfluences[ i ];
		#endif
	}
#endif`,Yp=`#ifdef USE_MORPHNORMALS
	objectNormal *= morphTargetBaseInfluence;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		if ( morphTargetInfluences[ i ] != 0.0 ) objectNormal += getMorph( gl_VertexID, i, 1 ).xyz * morphTargetInfluences[ i ];
	}
#endif`,Zp=`#ifdef USE_MORPHTARGETS
	#ifndef USE_INSTANCING_MORPH
		uniform float morphTargetBaseInfluence;
		uniform float morphTargetInfluences[ MORPHTARGETS_COUNT ];
	#endif
	uniform sampler2DArray morphTargetsTexture;
	uniform ivec2 morphTargetsTextureSize;
	vec4 getMorph( const in int vertexIndex, const in int morphTargetIndex, const in int offset ) {
		int texelIndex = vertexIndex * MORPHTARGETS_TEXTURE_STRIDE + offset;
		int y = texelIndex / morphTargetsTextureSize.x;
		int x = texelIndex - y * morphTargetsTextureSize.x;
		ivec3 morphUV = ivec3( x, y, morphTargetIndex );
		return texelFetch( morphTargetsTexture, morphUV, 0 );
	}
#endif`,Jp=`#ifdef USE_MORPHTARGETS
	transformed *= morphTargetBaseInfluence;
	for ( int i = 0; i < MORPHTARGETS_COUNT; i ++ ) {
		if ( morphTargetInfluences[ i ] != 0.0 ) transformed += getMorph( gl_VertexID, i, 0 ).xyz * morphTargetInfluences[ i ];
	}
#endif`,$p=`float faceDirection = gl_FrontFacing ? 1.0 : - 1.0;
#ifdef FLAT_SHADED
	vec3 fdx = dFdx( vViewPosition );
	vec3 fdy = dFdy( vViewPosition );
	vec3 normal = normalize( cross( fdx, fdy ) );
#else
	vec3 normal = normalize( vNormal );
	#ifdef DOUBLE_SIDED
		normal *= faceDirection;
	#endif
#endif
#if defined( USE_NORMALMAP_TANGENTSPACE ) || defined( USE_CLEARCOAT_NORMALMAP ) || defined( USE_ANISOTROPY )
	#ifdef USE_TANGENT
		mat3 tbn = mat3( normalize( vTangent ), normalize( vBitangent ), normal );
	#else
		mat3 tbn = getTangentFrame( - vViewPosition, normal,
		#if defined( USE_NORMALMAP )
			vNormalMapUv
		#elif defined( USE_CLEARCOAT_NORMALMAP )
			vClearcoatNormalMapUv
		#else
			vUv
		#endif
		);
	#endif
	#ifdef DOUBLE_SIDED
		tbn[0] *= faceDirection;
		tbn[1] *= faceDirection;
	#endif
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	#ifdef USE_TANGENT
		mat3 tbn2 = mat3( normalize( vTangent ), normalize( vBitangent ), normal );
	#else
		mat3 tbn2 = getTangentFrame( - vViewPosition, normal, vClearcoatNormalMapUv );
	#endif
	#ifdef DOUBLE_SIDED
		tbn2[0] *= faceDirection;
		tbn2[1] *= faceDirection;
	#endif
#endif
vec3 nonPerturbedNormal = normal;`,Kp=`#ifdef USE_NORMALMAP_OBJECTSPACE
	normal = texture2D( normalMap, vNormalMapUv ).xyz * 2.0 - 1.0;
	#ifdef FLIP_SIDED
		normal = - normal;
	#endif
	#ifdef DOUBLE_SIDED
		normal = normal * faceDirection;
	#endif
	normal = normalize( normalMatrix * normal );
#elif defined( USE_NORMALMAP_TANGENTSPACE )
	vec3 mapN = texture2D( normalMap, vNormalMapUv ).xyz * 2.0 - 1.0;
	#if defined( USE_PACKED_NORMALMAP )
		mapN = vec3( mapN.xy, sqrt( saturate( 1.0 - dot( mapN.xy, mapN.xy ) ) ) );
	#endif
	mapN.xy *= normalScale;
	normal = normalize( tbn * mapN );
#elif defined( USE_BUMPMAP )
	normal = perturbNormalArb( - vViewPosition, normal, dHdxy_fwd(), faceDirection );
#endif`,Qp=`#ifndef FLAT_SHADED
	varying vec3 vNormal;
	#ifdef USE_TANGENT
		varying vec3 vTangent;
		varying vec3 vBitangent;
	#endif
#endif`,jp=`#ifndef FLAT_SHADED
	varying vec3 vNormal;
	#ifdef USE_TANGENT
		varying vec3 vTangent;
		varying vec3 vBitangent;
	#endif
#endif`,em=`#ifndef FLAT_SHADED
	vNormal = normalize( transformedNormal );
	#ifdef USE_TANGENT
		vTangent = normalize( transformedTangent );
		vBitangent = normalize( cross( vNormal, vTangent ) * tangent.w );
		#ifdef FLIP_SIDED
			vBitangent = - vBitangent;
		#endif
	#endif
#endif`,tm=`#ifdef USE_NORMALMAP
	uniform sampler2D normalMap;
	uniform vec2 normalScale;
#endif
#ifdef USE_NORMALMAP_OBJECTSPACE
	uniform mat3 normalMatrix;
#endif
#if ! defined ( USE_TANGENT ) && ( defined ( USE_NORMALMAP_TANGENTSPACE ) || defined ( USE_CLEARCOAT_NORMALMAP ) || defined( USE_ANISOTROPY ) )
	mat3 getTangentFrame( vec3 eye_pos, vec3 surf_norm, vec2 uv ) {
		vec3 q0 = dFdx( eye_pos.xyz );
		vec3 q1 = dFdy( eye_pos.xyz );
		vec2 st0 = dFdx( uv.st );
		vec2 st1 = dFdy( uv.st );
		vec3 N = surf_norm;
		vec3 q1perp = cross( q1, N );
		vec3 q0perp = cross( N, q0 );
		vec3 T = q1perp * st0.x + q0perp * st1.x;
		vec3 B = q1perp * st0.y + q0perp * st1.y;
		float det = max( dot( T, T ), dot( B, B ) );
		float scale = ( det == 0.0 ) ? 0.0 : inversesqrt( det );
		return mat3( T * scale, B * scale, N );
	}
#endif`,nm=`#ifdef USE_CLEARCOAT
	vec3 clearcoatNormal = nonPerturbedNormal;
#endif`,im=`#ifdef USE_CLEARCOAT_NORMALMAP
	vec3 clearcoatMapN = texture2D( clearcoatNormalMap, vClearcoatNormalMapUv ).xyz * 2.0 - 1.0;
	clearcoatMapN.xy *= clearcoatNormalScale;
	clearcoatNormal = normalize( tbn2 * clearcoatMapN );
#endif`,sm=`#ifdef USE_CLEARCOATMAP
	uniform sampler2D clearcoatMap;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	uniform sampler2D clearcoatNormalMap;
	uniform vec2 clearcoatNormalScale;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	uniform sampler2D clearcoatRoughnessMap;
#endif`,rm=`#ifdef USE_IRIDESCENCEMAP
	uniform sampler2D iridescenceMap;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	uniform sampler2D iridescenceThicknessMap;
#endif`,am=`#ifdef OPAQUE
diffuseColor.a = 1.0;
#endif
#ifdef USE_TRANSMISSION
diffuseColor.a *= material.transmissionAlpha;
#endif
gl_FragColor = vec4( outgoingLight, diffuseColor.a );`,om=`vec3 packNormalToRGB( const in vec3 normal ) {
	return normalize( normal ) * 0.5 + 0.5;
}
vec3 unpackRGBToNormal( const in vec3 rgb ) {
	return 2.0 * rgb.xyz - 1.0;
}
const float PackUpscale = 256. / 255.;const float UnpackDownscale = 255. / 256.;const float ShiftRight8 = 1. / 256.;
const float Inv255 = 1. / 255.;
const vec4 PackFactors = vec4( 1.0, 256.0, 256.0 * 256.0, 256.0 * 256.0 * 256.0 );
const vec2 UnpackFactors2 = vec2( UnpackDownscale, 1.0 / PackFactors.g );
const vec3 UnpackFactors3 = vec3( UnpackDownscale / PackFactors.rg, 1.0 / PackFactors.b );
const vec4 UnpackFactors4 = vec4( UnpackDownscale / PackFactors.rgb, 1.0 / PackFactors.a );
vec4 packDepthToRGBA( const in float v ) {
	if( v <= 0.0 )
		return vec4( 0., 0., 0., 0. );
	if( v >= 1.0 )
		return vec4( 1., 1., 1., 1. );
	float vuf;
	float af = modf( v * PackFactors.a, vuf );
	float bf = modf( vuf * ShiftRight8, vuf );
	float gf = modf( vuf * ShiftRight8, vuf );
	return vec4( vuf * Inv255, gf * PackUpscale, bf * PackUpscale, af );
}
vec3 packDepthToRGB( const in float v ) {
	if( v <= 0.0 )
		return vec3( 0., 0., 0. );
	if( v >= 1.0 )
		return vec3( 1., 1., 1. );
	float vuf;
	float bf = modf( v * PackFactors.b, vuf );
	float gf = modf( vuf * ShiftRight8, vuf );
	return vec3( vuf * Inv255, gf * PackUpscale, bf );
}
vec2 packDepthToRG( const in float v ) {
	if( v <= 0.0 )
		return vec2( 0., 0. );
	if( v >= 1.0 )
		return vec2( 1., 1. );
	float vuf;
	float gf = modf( v * 256., vuf );
	return vec2( vuf * Inv255, gf );
}
float unpackRGBAToDepth( const in vec4 v ) {
	return dot( v, UnpackFactors4 );
}
float unpackRGBToDepth( const in vec3 v ) {
	return dot( v, UnpackFactors3 );
}
float unpackRGToDepth( const in vec2 v ) {
	return v.r * UnpackFactors2.r + v.g * UnpackFactors2.g;
}
vec4 pack2HalfToRGBA( const in vec2 v ) {
	vec4 r = vec4( v.x, fract( v.x * 255.0 ), v.y, fract( v.y * 255.0 ) );
	return vec4( r.x - r.y / 255.0, r.y, r.z - r.w / 255.0, r.w );
}
vec2 unpackRGBATo2Half( const in vec4 v ) {
	return vec2( v.x + ( v.y / 255.0 ), v.z + ( v.w / 255.0 ) );
}
float viewZToOrthographicDepth( const in float viewZ, const in float near, const in float far ) {
	return ( viewZ + near ) / ( near - far );
}
float orthographicDepthToViewZ( const in float depth, const in float near, const in float far ) {
	#ifdef USE_REVERSED_DEPTH_BUFFER
	
		return depth * ( far - near ) - far;
	#else
		return depth * ( near - far ) - near;
	#endif
}
float viewZToPerspectiveDepth( const in float viewZ, const in float near, const in float far ) {
	return ( ( near + viewZ ) * far ) / ( ( far - near ) * viewZ );
}
float perspectiveDepthToViewZ( const in float depth, const in float near, const in float far ) {
	
	#ifdef USE_REVERSED_DEPTH_BUFFER
		return ( near * far ) / ( ( near - far ) * depth - near );
	#else
		return ( near * far ) / ( ( far - near ) * depth - far );
	#endif
}`,lm=`#ifdef PREMULTIPLIED_ALPHA
	gl_FragColor.rgb *= gl_FragColor.a;
#endif`,cm=`vec4 mvPosition = vec4( transformed, 1.0 );
#ifdef USE_BATCHING
	mvPosition = batchingMatrix * mvPosition;
#endif
#ifdef USE_INSTANCING
	mvPosition = instanceMatrix * mvPosition;
#endif
mvPosition = modelViewMatrix * mvPosition;
gl_Position = projectionMatrix * mvPosition;`,hm=`#ifdef DITHERING
	gl_FragColor.rgb = dithering( gl_FragColor.rgb );
#endif`,um=`#ifdef DITHERING
	vec3 dithering( vec3 color ) {
		float grid_position = rand( gl_FragCoord.xy );
		vec3 dither_shift_RGB = vec3( 0.25 / 255.0, -0.25 / 255.0, 0.25 / 255.0 );
		dither_shift_RGB = mix( 2.0 * dither_shift_RGB, -2.0 * dither_shift_RGB, grid_position );
		return color + dither_shift_RGB;
	}
#endif`,dm=`float roughnessFactor = roughness;
#ifdef USE_ROUGHNESSMAP
	vec4 texelRoughness = texture2D( roughnessMap, vRoughnessMapUv );
	roughnessFactor *= texelRoughness.g;
#endif`,fm=`#ifdef USE_ROUGHNESSMAP
	uniform sampler2D roughnessMap;
#endif`,pm=`#if NUM_SPOT_LIGHT_COORDS > 0
	varying vec4 vSpotLightCoord[ NUM_SPOT_LIGHT_COORDS ];
#endif
#if NUM_SPOT_LIGHT_MAPS > 0
	uniform sampler2D spotLightMap[ NUM_SPOT_LIGHT_MAPS ];
#endif
#ifdef USE_SHADOWMAP
	#if NUM_SUN_LIGHT_SHADOWS > 0
		#define SUN_LIGHT_CASCADES 2
		#if defined( SHADOWMAP_TYPE_PCF )
			uniform sampler2DShadow sunShadowMap[ NUM_SUN_LIGHT_SHADOWS ];
		#else
			uniform sampler2D sunShadowMap[ NUM_SUN_LIGHT_SHADOWS ];
		#endif
		uniform mat4 sunShadowMatrix[ NUM_SUN_LIGHT_SHADOWS * SUN_LIGHT_CASCADES ];
		uniform vec4 sunShadowCascade[ NUM_SUN_LIGHT_SHADOWS * SUN_LIGHT_CASCADES ];
		varying vec4 vSunShadowWorldPosition;
		varying vec3 vSunShadowWorldNormal;
		struct SunLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform SunLightShadow sunLightShadows[ NUM_SUN_LIGHT_SHADOWS ];
	#endif
	#if NUM_DIR_LIGHT_SHADOWS > 0
		#if defined( SHADOWMAP_TYPE_PCF )
			uniform sampler2DShadow directionalShadowMap[ NUM_DIR_LIGHT_SHADOWS ];
		#else
			uniform sampler2D directionalShadowMap[ NUM_DIR_LIGHT_SHADOWS ];
		#endif
		varying vec4 vDirectionalShadowCoord[ NUM_DIR_LIGHT_SHADOWS ];
		struct DirectionalLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform DirectionalLightShadow directionalLightShadows[ NUM_DIR_LIGHT_SHADOWS ];
	#endif
	#if NUM_SPOT_LIGHT_SHADOWS > 0
		#if defined( SHADOWMAP_TYPE_PCF )
			uniform sampler2DShadow spotShadowMap[ NUM_SPOT_LIGHT_SHADOWS ];
		#else
			uniform sampler2D spotShadowMap[ NUM_SPOT_LIGHT_SHADOWS ];
		#endif
		struct SpotLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform SpotLightShadow spotLightShadows[ NUM_SPOT_LIGHT_SHADOWS ];
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
		#if defined( SHADOWMAP_TYPE_PCF )
			uniform samplerCubeShadow pointShadowMap[ NUM_POINT_LIGHT_SHADOWS ];
		#elif defined( SHADOWMAP_TYPE_BASIC )
			uniform samplerCube pointShadowMap[ NUM_POINT_LIGHT_SHADOWS ];
		#endif
		varying vec4 vPointShadowCoord[ NUM_POINT_LIGHT_SHADOWS ];
		struct PointLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
			float shadowCameraNear;
			float shadowCameraFar;
		};
		uniform PointLightShadow pointLightShadows[ NUM_POINT_LIGHT_SHADOWS ];
	#endif
	#if defined( SHADOWMAP_TYPE_PCF )
		float interleavedGradientNoise( vec2 position ) {
			return fract( 52.9829189 * fract( dot( position, vec2( 0.06711056, 0.00583715 ) ) ) );
		}
		vec2 vogelDiskSample( int sampleIndex, int samplesCount, float phi ) {
			const float goldenAngle = 2.399963229728653;
			float r = sqrt( ( float( sampleIndex ) + 0.5 ) / float( samplesCount ) );
			float theta = float( sampleIndex ) * goldenAngle + phi;
			return vec2( cos( theta ), sin( theta ) ) * r;
		}
	#endif
	#if defined( SHADOWMAP_TYPE_PCF )
		float getShadow( sampler2DShadow shadowMap, vec2 shadowMapSize, float shadowIntensity, float shadowBias, float shadowRadius, vec4 shadowCoord ) {
			float shadow = 1.0;
			shadowCoord.xyz /= shadowCoord.w;
			shadowCoord.z += shadowBias;
			bool inFrustum = shadowCoord.x >= 0.0 && shadowCoord.x <= 1.0 && shadowCoord.y >= 0.0 && shadowCoord.y <= 1.0;
			bool frustumTest = inFrustum && shadowCoord.z <= 1.0;
			if ( frustumTest ) {
				vec2 texelSize = vec2( 1.0 ) / shadowMapSize;
				float radius = shadowRadius * texelSize.x;
				float phi = interleavedGradientNoise( gl_FragCoord.xy ) * PI2;
				shadow = (
					texture( shadowMap, vec3( shadowCoord.xy + vogelDiskSample( 0, 5, phi ) * radius, shadowCoord.z ) ) +
					texture( shadowMap, vec3( shadowCoord.xy + vogelDiskSample( 1, 5, phi ) * radius, shadowCoord.z ) ) +
					texture( shadowMap, vec3( shadowCoord.xy + vogelDiskSample( 2, 5, phi ) * radius, shadowCoord.z ) ) +
					texture( shadowMap, vec3( shadowCoord.xy + vogelDiskSample( 3, 5, phi ) * radius, shadowCoord.z ) ) +
					texture( shadowMap, vec3( shadowCoord.xy + vogelDiskSample( 4, 5, phi ) * radius, shadowCoord.z ) )
				) * 0.2;
			}
			return mix( 1.0, shadow, shadowIntensity );
		}
	#elif defined( SHADOWMAP_TYPE_VSM )
		float getShadow( sampler2D shadowMap, vec2 shadowMapSize, float shadowIntensity, float shadowBias, float shadowRadius, vec4 shadowCoord ) {
			float shadow = 1.0;
			shadowCoord.xyz /= shadowCoord.w;
			#ifdef USE_REVERSED_DEPTH_BUFFER
				shadowCoord.z -= shadowBias;
			#else
				shadowCoord.z += shadowBias;
			#endif
			bool inFrustum = shadowCoord.x >= 0.0 && shadowCoord.x <= 1.0 && shadowCoord.y >= 0.0 && shadowCoord.y <= 1.0;
			bool frustumTest = inFrustum && shadowCoord.z <= 1.0;
			if ( frustumTest ) {
				vec2 distribution = texture2D( shadowMap, shadowCoord.xy ).rg;
				float mean = distribution.x;
				float variance = distribution.y * distribution.y;
				#ifdef USE_REVERSED_DEPTH_BUFFER
					float hard_shadow = step( mean, shadowCoord.z );
				#else
					float hard_shadow = step( shadowCoord.z, mean );
				#endif
				
				if ( hard_shadow == 1.0 ) {
					shadow = 1.0;
				} else {
					variance = max( variance, 0.0000001 );
					float d = shadowCoord.z - mean;
					float p_max = variance / ( variance + d * d );
					p_max = clamp( ( p_max - 0.3 ) / 0.65, 0.0, 1.0 );
					shadow = max( hard_shadow, p_max );
				}
			}
			return mix( 1.0, shadow, shadowIntensity );
		}
	#else
		float getShadow( sampler2D shadowMap, vec2 shadowMapSize, float shadowIntensity, float shadowBias, float shadowRadius, vec4 shadowCoord ) {
			float shadow = 1.0;
			shadowCoord.xyz /= shadowCoord.w;
			#ifdef USE_REVERSED_DEPTH_BUFFER
				shadowCoord.z -= shadowBias;
			#else
				shadowCoord.z += shadowBias;
			#endif
			bool inFrustum = shadowCoord.x >= 0.0 && shadowCoord.x <= 1.0 && shadowCoord.y >= 0.0 && shadowCoord.y <= 1.0;
			bool frustumTest = inFrustum && shadowCoord.z <= 1.0;
			if ( frustumTest ) {
				float depth = texture2D( shadowMap, shadowCoord.xy ).r;
				#ifdef USE_REVERSED_DEPTH_BUFFER
					shadow = step( depth, shadowCoord.z );
				#else
					shadow = step( shadowCoord.z, depth );
				#endif
			}
			return mix( 1.0, shadow, shadowIntensity );
		}
	#endif
	#if NUM_SUN_LIGHT_SHADOWS > 0
		float getSunShadow(
			#if defined( SHADOWMAP_TYPE_PCF )
				sampler2DShadow shadowMap,
			#else
				sampler2D shadowMap,
			#endif
			SunLightShadow sunLightShadow,
			int shadowIndex
		) {
			vec4 shadowWorldPosition = vec4( vSunShadowWorldPosition.xyz + vSunShadowWorldNormal * sunLightShadow.shadowNormalBias, 1.0 );
			float viewDepth = vSunShadowWorldPosition.w;
			int cascadeOffset = shadowIndex * SUN_LIGHT_CASCADES;
			float shadow = 1.0;
			for ( int i = SUN_LIGHT_CASCADES - 1; i >= 0; i -- ) {
				vec4 cascade = sunShadowCascade[ cascadeOffset + i ];
				if ( viewDepth >= cascade.x && viewDepth < cascade.y ) {
					float cascadeShadow = getShadow(
						shadowMap,
						sunLightShadow.shadowMapSize,
						sunLightShadow.shadowIntensity,
						sunLightShadow.shadowBias,
						sunLightShadow.shadowRadius,
						sunShadowMatrix[ cascadeOffset + i ] * shadowWorldPosition
					);
					shadow = mix( cascadeShadow, shadow, smoothstep( cascade.z, cascade.y, viewDepth ) );
				}
			}
			return shadow;
		}
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
	#if defined( SHADOWMAP_TYPE_PCF )
	float getPointShadow( samplerCubeShadow shadowMap, vec2 shadowMapSize, float shadowIntensity, float shadowBias, float shadowRadius, vec4 shadowCoord, float shadowCameraNear, float shadowCameraFar ) {
		float shadow = 1.0;
		vec3 lightToPosition = shadowCoord.xyz;
		vec3 bd3D = normalize( lightToPosition );
		vec3 absVec = abs( lightToPosition );
		float viewSpaceZ = max( max( absVec.x, absVec.y ), absVec.z );
		if ( viewSpaceZ - shadowCameraFar <= 0.0 && viewSpaceZ - shadowCameraNear >= 0.0 ) {
			#ifdef USE_REVERSED_DEPTH_BUFFER
				float dp = ( shadowCameraNear * ( shadowCameraFar - viewSpaceZ ) ) / ( viewSpaceZ * ( shadowCameraFar - shadowCameraNear ) );
				dp -= shadowBias;
			#else
				float dp = ( shadowCameraFar * ( viewSpaceZ - shadowCameraNear ) ) / ( viewSpaceZ * ( shadowCameraFar - shadowCameraNear ) );
				dp += shadowBias;
			#endif
			float texelSize = shadowRadius / shadowMapSize.x;
			vec3 absDir = abs( bd3D );
			vec3 tangent = absDir.x > absDir.z ? vec3( 0.0, 1.0, 0.0 ) : vec3( 1.0, 0.0, 0.0 );
			tangent = normalize( cross( bd3D, tangent ) );
			vec3 bitangent = cross( bd3D, tangent );
			float phi = interleavedGradientNoise( gl_FragCoord.xy ) * PI2;
			vec2 sample0 = vogelDiskSample( 0, 5, phi );
			vec2 sample1 = vogelDiskSample( 1, 5, phi );
			vec2 sample2 = vogelDiskSample( 2, 5, phi );
			vec2 sample3 = vogelDiskSample( 3, 5, phi );
			vec2 sample4 = vogelDiskSample( 4, 5, phi );
			shadow = (
				texture( shadowMap, vec4( bd3D + ( tangent * sample0.x + bitangent * sample0.y ) * texelSize, dp ) ) +
				texture( shadowMap, vec4( bd3D + ( tangent * sample1.x + bitangent * sample1.y ) * texelSize, dp ) ) +
				texture( shadowMap, vec4( bd3D + ( tangent * sample2.x + bitangent * sample2.y ) * texelSize, dp ) ) +
				texture( shadowMap, vec4( bd3D + ( tangent * sample3.x + bitangent * sample3.y ) * texelSize, dp ) ) +
				texture( shadowMap, vec4( bd3D + ( tangent * sample4.x + bitangent * sample4.y ) * texelSize, dp ) )
			) * 0.2;
		}
		return mix( 1.0, shadow, shadowIntensity );
	}
	#elif defined( SHADOWMAP_TYPE_BASIC )
	float getPointShadow( samplerCube shadowMap, vec2 shadowMapSize, float shadowIntensity, float shadowBias, float shadowRadius, vec4 shadowCoord, float shadowCameraNear, float shadowCameraFar ) {
		float shadow = 1.0;
		vec3 lightToPosition = shadowCoord.xyz;
		vec3 absVec = abs( lightToPosition );
		float viewSpaceZ = max( max( absVec.x, absVec.y ), absVec.z );
		if ( viewSpaceZ - shadowCameraFar <= 0.0 && viewSpaceZ - shadowCameraNear >= 0.0 ) {
			float dp = ( shadowCameraFar * ( viewSpaceZ - shadowCameraNear ) ) / ( viewSpaceZ * ( shadowCameraFar - shadowCameraNear ) );
			dp += shadowBias;
			vec3 bd3D = normalize( lightToPosition );
			float depth = textureCube( shadowMap, bd3D ).r;
			#ifdef USE_REVERSED_DEPTH_BUFFER
				depth = 1.0 - depth;
			#endif
			shadow = step( dp, depth );
		}
		return mix( 1.0, shadow, shadowIntensity );
	}
	#endif
	#endif
#endif`,mm=`#if NUM_SPOT_LIGHT_COORDS > 0
	uniform mat4 spotLightMatrix[ NUM_SPOT_LIGHT_COORDS ];
	varying vec4 vSpotLightCoord[ NUM_SPOT_LIGHT_COORDS ];
#endif
#ifdef USE_SHADOWMAP
	#if NUM_SUN_LIGHT_SHADOWS > 0
		varying vec4 vSunShadowWorldPosition;
		varying vec3 vSunShadowWorldNormal;
	#endif
	#if NUM_DIR_LIGHT_SHADOWS > 0
		uniform mat4 directionalShadowMatrix[ NUM_DIR_LIGHT_SHADOWS ];
		varying vec4 vDirectionalShadowCoord[ NUM_DIR_LIGHT_SHADOWS ];
		struct DirectionalLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform DirectionalLightShadow directionalLightShadows[ NUM_DIR_LIGHT_SHADOWS ];
	#endif
	#if NUM_SPOT_LIGHT_SHADOWS > 0
		struct SpotLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
		};
		uniform SpotLightShadow spotLightShadows[ NUM_SPOT_LIGHT_SHADOWS ];
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
		uniform mat4 pointShadowMatrix[ NUM_POINT_LIGHT_SHADOWS ];
		varying vec4 vPointShadowCoord[ NUM_POINT_LIGHT_SHADOWS ];
		struct PointLightShadow {
			float shadowIntensity;
			float shadowBias;
			float shadowNormalBias;
			float shadowRadius;
			vec2 shadowMapSize;
			float shadowCameraNear;
			float shadowCameraFar;
		};
		uniform PointLightShadow pointLightShadows[ NUM_POINT_LIGHT_SHADOWS ];
	#endif
#endif`,gm=`#if ( defined( USE_SHADOWMAP ) && ( NUM_DIR_LIGHT_SHADOWS > 0 || NUM_SUN_LIGHT_SHADOWS > 0 || NUM_POINT_LIGHT_SHADOWS > 0 ) ) || ( NUM_SPOT_LIGHT_COORDS > 0 )
	#ifdef HAS_NORMAL
		vec3 shadowWorldNormal = transformNormalByInverseViewMatrix( transformedNormal, viewMatrix );
	#else
		vec3 shadowWorldNormal = vec3( 0.0 );
	#endif
	vec4 shadowWorldPosition;
#endif
#if defined( USE_SHADOWMAP )
	#if NUM_SUN_LIGHT_SHADOWS > 0
		vSunShadowWorldPosition = vec4( worldPosition.xyz, - mvPosition.z );
		vSunShadowWorldNormal = shadowWorldNormal;
	#endif
	#if NUM_DIR_LIGHT_SHADOWS > 0
		#pragma unroll_loop_start
		for ( int i = 0; i < NUM_DIR_LIGHT_SHADOWS; i ++ ) {
			shadowWorldPosition = worldPosition + vec4( shadowWorldNormal * directionalLightShadows[ i ].shadowNormalBias, 0 );
			vDirectionalShadowCoord[ i ] = directionalShadowMatrix[ i ] * shadowWorldPosition;
		}
		#pragma unroll_loop_end
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0
		#pragma unroll_loop_start
		for ( int i = 0; i < NUM_POINT_LIGHT_SHADOWS; i ++ ) {
			shadowWorldPosition = worldPosition + vec4( shadowWorldNormal * pointLightShadows[ i ].shadowNormalBias, 0 );
			vPointShadowCoord[ i ] = pointShadowMatrix[ i ] * shadowWorldPosition;
		}
		#pragma unroll_loop_end
	#endif
#endif
#if NUM_SPOT_LIGHT_COORDS > 0
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SPOT_LIGHT_COORDS; i ++ ) {
		shadowWorldPosition = worldPosition;
		#if ( defined( USE_SHADOWMAP ) && UNROLLED_LOOP_INDEX < NUM_SPOT_LIGHT_SHADOWS )
			shadowWorldPosition.xyz += shadowWorldNormal * spotLightShadows[ i ].shadowNormalBias;
		#endif
		vSpotLightCoord[ i ] = spotLightMatrix[ i ] * shadowWorldPosition;
	}
	#pragma unroll_loop_end
#endif`,_m=`float getShadowMask() {
	float shadow = 1.0;
	#ifdef USE_SHADOWMAP
	#if NUM_SUN_LIGHT_SHADOWS > 0
	SunLightShadow sunLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SUN_LIGHT_SHADOWS; i ++ ) {
		sunLight = sunLightShadows[ i ];
		shadow *= receiveShadow ? getSunShadow( sunShadowMap[ i ], sunLight, UNROLLED_LOOP_INDEX ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#if NUM_DIR_LIGHT_SHADOWS > 0
	DirectionalLightShadow directionalLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_DIR_LIGHT_SHADOWS; i ++ ) {
		directionalLight = directionalLightShadows[ i ];
		shadow *= receiveShadow ? getShadow( directionalShadowMap[ i ], directionalLight.shadowMapSize, directionalLight.shadowIntensity, directionalLight.shadowBias, directionalLight.shadowRadius, vDirectionalShadowCoord[ i ] ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#if NUM_SPOT_LIGHT_SHADOWS > 0
	SpotLightShadow spotLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_SPOT_LIGHT_SHADOWS; i ++ ) {
		spotLight = spotLightShadows[ i ];
		shadow *= receiveShadow ? getShadow( spotShadowMap[ i ], spotLight.shadowMapSize, spotLight.shadowIntensity, spotLight.shadowBias, spotLight.shadowRadius, vSpotLightCoord[ i ] ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#if NUM_POINT_LIGHT_SHADOWS > 0 && ( defined( SHADOWMAP_TYPE_PCF ) || defined( SHADOWMAP_TYPE_BASIC ) )
	PointLightShadow pointLight;
	#pragma unroll_loop_start
	for ( int i = 0; i < NUM_POINT_LIGHT_SHADOWS; i ++ ) {
		pointLight = pointLightShadows[ i ];
		shadow *= receiveShadow ? getPointShadow( pointShadowMap[ i ], pointLight.shadowMapSize, pointLight.shadowIntensity, pointLight.shadowBias, pointLight.shadowRadius, vPointShadowCoord[ i ], pointLight.shadowCameraNear, pointLight.shadowCameraFar ) : 1.0;
	}
	#pragma unroll_loop_end
	#endif
	#endif
	return shadow;
}`,xm=`#ifdef USE_SKINNING
	mat4 boneMatX = getBoneMatrix( skinIndex.x );
	mat4 boneMatY = getBoneMatrix( skinIndex.y );
	mat4 boneMatZ = getBoneMatrix( skinIndex.z );
	mat4 boneMatW = getBoneMatrix( skinIndex.w );
#endif`,ym=`#ifdef USE_SKINNING
	uniform mat4 bindMatrix;
	uniform mat4 bindMatrixInverse;
	uniform highp sampler2D boneTexture;
	mat4 getBoneMatrix( const in float i ) {
		int size = textureSize( boneTexture, 0 ).x;
		int j = int( i ) * 4;
		int x = j % size;
		int y = j / size;
		vec4 v1 = texelFetch( boneTexture, ivec2( x, y ), 0 );
		vec4 v2 = texelFetch( boneTexture, ivec2( x + 1, y ), 0 );
		vec4 v3 = texelFetch( boneTexture, ivec2( x + 2, y ), 0 );
		vec4 v4 = texelFetch( boneTexture, ivec2( x + 3, y ), 0 );
		return mat4( v1, v2, v3, v4 );
	}
#endif`,vm=`#ifdef USE_SKINNING
	vec4 skinVertex = bindMatrix * vec4( transformed, 1.0 );
	vec4 skinned = vec4( 0.0 );
	skinned += boneMatX * skinVertex * skinWeight.x;
	skinned += boneMatY * skinVertex * skinWeight.y;
	skinned += boneMatZ * skinVertex * skinWeight.z;
	skinned += boneMatW * skinVertex * skinWeight.w;
	transformed = ( bindMatrixInverse * skinned ).xyz;
#endif`,Mm=`#ifdef USE_SKINNING
	mat4 skinMatrix = mat4( 0.0 );
	skinMatrix += skinWeight.x * boneMatX;
	skinMatrix += skinWeight.y * boneMatY;
	skinMatrix += skinWeight.z * boneMatZ;
	skinMatrix += skinWeight.w * boneMatW;
	skinMatrix = bindMatrixInverse * skinMatrix * bindMatrix;
	objectNormal = vec4( skinMatrix * vec4( objectNormal, 0.0 ) ).xyz;
	#ifdef USE_TANGENT
		objectTangent = vec4( skinMatrix * vec4( objectTangent, 0.0 ) ).xyz;
	#endif
#endif`,Sm=`float specularStrength;
#ifdef USE_SPECULARMAP
	vec4 texelSpecular = texture2D( specularMap, vSpecularMapUv );
	specularStrength = texelSpecular.r;
#else
	specularStrength = 1.0;
#endif`,bm=`#ifdef USE_SPECULARMAP
	uniform sampler2D specularMap;
#endif`,Em=`#if defined( TONE_MAPPING )
	gl_FragColor.rgb = toneMapping( gl_FragColor.rgb );
#endif`,Tm=`#ifndef saturate
#define saturate( a ) clamp( a, 0.0, 1.0 )
#endif
uniform float toneMappingExposure;
vec3 LinearToneMapping( vec3 color ) {
	return saturate( toneMappingExposure * color );
}
vec3 ReinhardToneMapping( vec3 color ) {
	color *= toneMappingExposure;
	return saturate( color / ( vec3( 1.0 ) + color ) );
}
vec3 CineonToneMapping( vec3 color ) {
	color *= toneMappingExposure;
	color = max( vec3( 0.0 ), color - 0.004 );
	return pow( ( color * ( 6.2 * color + 0.5 ) ) / ( color * ( 6.2 * color + 1.7 ) + 0.06 ), vec3( 2.2 ) );
}
vec3 RRTAndODTFit( vec3 v ) {
	vec3 a = v * ( v + 0.0245786 ) - 0.000090537;
	vec3 b = v * ( 0.983729 * v + 0.4329510 ) + 0.238081;
	return a / b;
}
vec3 ACESFilmicToneMapping( vec3 color ) {
	const mat3 ACESInputMat = mat3(
		vec3( 0.59719, 0.07600, 0.02840 ),		vec3( 0.35458, 0.90834, 0.13383 ),
		vec3( 0.04823, 0.01566, 0.83777 )
	);
	const mat3 ACESOutputMat = mat3(
		vec3(  1.60475, -0.10208, -0.00327 ),		vec3( -0.53108,  1.10813, -0.07276 ),
		vec3( -0.07367, -0.00605,  1.07602 )
	);
	color *= toneMappingExposure / 0.6;
	color = ACESInputMat * color;
	color = RRTAndODTFit( color );
	color = ACESOutputMat * color;
	return saturate( color );
}
const mat3 LINEAR_REC2020_TO_LINEAR_SRGB = mat3(
	vec3( 1.6605, - 0.1246, - 0.0182 ),
	vec3( - 0.5876, 1.1329, - 0.1006 ),
	vec3( - 0.0728, - 0.0083, 1.1187 )
);
const mat3 LINEAR_SRGB_TO_LINEAR_REC2020 = mat3(
	vec3( 0.6274, 0.0691, 0.0164 ),
	vec3( 0.3293, 0.9195, 0.0880 ),
	vec3( 0.0433, 0.0113, 0.8956 )
);
vec3 agxDefaultContrastApprox( vec3 x ) {
	vec3 x2 = x * x;
	vec3 x4 = x2 * x2;
	return + 15.5 * x4 * x2
		- 40.14 * x4 * x
		+ 31.96 * x4
		- 6.868 * x2 * x
		+ 0.4298 * x2
		+ 0.1191 * x
		- 0.00232;
}
vec3 AgXToneMapping( vec3 color ) {
	const mat3 AgXInsetMatrix = mat3(
		vec3( 0.856627153315983, 0.137318972929847, 0.11189821299995 ),
		vec3( 0.0951212405381588, 0.761241990602591, 0.0767994186031903 ),
		vec3( 0.0482516061458583, 0.101439036467562, 0.811302368396859 )
	);
	const mat3 AgXOutsetMatrix = mat3(
		vec3( 1.1271005818144368, - 0.1413297634984383, - 0.14132976349843826 ),
		vec3( - 0.11060664309660323, 1.157823702216272, - 0.11060664309660294 ),
		vec3( - 0.016493938717834573, - 0.016493938717834257, 1.2519364065950405 )
	);
	const float AgxMinEv = - 12.47393;	const float AgxMaxEv = 4.026069;
	color *= toneMappingExposure;
	color = LINEAR_SRGB_TO_LINEAR_REC2020 * color;
	color = AgXInsetMatrix * color;
	color = max( color, 1e-10 );	color = log2( color );
	color = ( color - AgxMinEv ) / ( AgxMaxEv - AgxMinEv );
	color = clamp( color, 0.0, 1.0 );
	color = agxDefaultContrastApprox( color );
	color = AgXOutsetMatrix * color;
	color = pow( max( vec3( 0.0 ), color ), vec3( 2.2 ) );
	color = LINEAR_REC2020_TO_LINEAR_SRGB * color;
	color = clamp( color, 0.0, 1.0 );
	return color;
}
vec3 NeutralToneMapping( vec3 color ) {
	const float StartCompression = 0.8 - 0.04;
	const float Desaturation = 0.15;
	color *= toneMappingExposure;
	float x = min( color.r, min( color.g, color.b ) );
	float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
	color -= offset;
	float peak = max( color.r, max( color.g, color.b ) );
	if ( peak < StartCompression ) return color;
	float d = 1. - StartCompression;
	float newPeak = 1. - d * d / ( peak + d - StartCompression );
	color *= newPeak / peak;
	float g = 1. - 1. / ( Desaturation * ( peak - newPeak ) + 1. );
	return mix( color, vec3( newPeak ), g );
}
vec3 CustomToneMapping( vec3 color ) { return color; }`,wm=`#ifdef USE_TRANSMISSION
	material.transmission = transmission;
	material.transmissionAlpha = 1.0;
	material.thickness = thickness;
	material.attenuationDistance = attenuationDistance;
	material.attenuationColor = attenuationColor;
	#ifdef USE_TRANSMISSIONMAP
		material.transmission *= texture2D( transmissionMap, vTransmissionMapUv ).r;
	#endif
	#ifdef USE_THICKNESSMAP
		material.thickness *= texture2D( thicknessMap, vThicknessMapUv ).g;
	#endif
	vec3 pos = vWorldPosition;
	vec3 v = normalize( cameraPosition - pos );
	vec3 n = transformNormalByInverseViewMatrix( normal, viewMatrix );
	vec4 transmitted = getIBLVolumeRefraction(
		n, v, material.roughness, material.diffuseContribution, material.specularColorBlended, material.specularF90,
		pos, modelMatrix, viewMatrix, projectionMatrix, material.dispersion, material.ior, material.thickness,
		material.attenuationColor, material.attenuationDistance );
	material.transmissionAlpha = mix( material.transmissionAlpha, transmitted.a, material.transmission );
	totalDiffuse = mix( totalDiffuse, transmitted.rgb, material.transmission );
#endif`,Am=`#ifdef USE_TRANSMISSION
	uniform float transmission;
	uniform float thickness;
	uniform float attenuationDistance;
	uniform vec3 attenuationColor;
	#ifdef USE_TRANSMISSIONMAP
		uniform sampler2D transmissionMap;
	#endif
	#ifdef USE_THICKNESSMAP
		uniform sampler2D thicknessMap;
	#endif
	uniform vec2 transmissionSamplerSize;
	uniform sampler2D transmissionSamplerMap;
	uniform mat4 modelMatrix;
	uniform mat4 projectionMatrix;
	varying vec3 vWorldPosition;
	float w0( float a ) {
		return ( 1.0 / 6.0 ) * ( a * ( a * ( - a + 3.0 ) - 3.0 ) + 1.0 );
	}
	float w1( float a ) {
		return ( 1.0 / 6.0 ) * ( a *  a * ( 3.0 * a - 6.0 ) + 4.0 );
	}
	float w2( float a ){
		return ( 1.0 / 6.0 ) * ( a * ( a * ( - 3.0 * a + 3.0 ) + 3.0 ) + 1.0 );
	}
	float w3( float a ) {
		return ( 1.0 / 6.0 ) * ( a * a * a );
	}
	float g0( float a ) {
		return w0( a ) + w1( a );
	}
	float g1( float a ) {
		return w2( a ) + w3( a );
	}
	float h0( float a ) {
		return - 1.0 + w1( a ) / ( w0( a ) + w1( a ) );
	}
	float h1( float a ) {
		return 1.0 + w3( a ) / ( w2( a ) + w3( a ) );
	}
	vec4 bicubic( sampler2D tex, vec2 uv, vec4 texelSize, float lod ) {
		uv = uv * texelSize.zw + 0.5;
		vec2 iuv = floor( uv );
		vec2 fuv = fract( uv );
		float g0x = g0( fuv.x );
		float g1x = g1( fuv.x );
		float h0x = h0( fuv.x );
		float h1x = h1( fuv.x );
		float h0y = h0( fuv.y );
		float h1y = h1( fuv.y );
		vec2 p0 = ( vec2( iuv.x + h0x, iuv.y + h0y ) - 0.5 ) * texelSize.xy;
		vec2 p1 = ( vec2( iuv.x + h1x, iuv.y + h0y ) - 0.5 ) * texelSize.xy;
		vec2 p2 = ( vec2( iuv.x + h0x, iuv.y + h1y ) - 0.5 ) * texelSize.xy;
		vec2 p3 = ( vec2( iuv.x + h1x, iuv.y + h1y ) - 0.5 ) * texelSize.xy;
		return g0( fuv.y ) * ( g0x * textureLod( tex, p0, lod ) + g1x * textureLod( tex, p1, lod ) ) +
			g1( fuv.y ) * ( g0x * textureLod( tex, p2, lod ) + g1x * textureLod( tex, p3, lod ) );
	}
	vec4 textureBicubic( sampler2D sampler, vec2 uv, float lod ) {
		vec2 fLodSize = vec2( textureSize( sampler, int( lod ) ) );
		vec2 cLodSize = vec2( textureSize( sampler, int( lod + 1.0 ) ) );
		vec2 fLodSizeInv = 1.0 / fLodSize;
		vec2 cLodSizeInv = 1.0 / cLodSize;
		vec4 fSample = bicubic( sampler, uv, vec4( fLodSizeInv, fLodSize ), floor( lod ) );
		vec4 cSample = bicubic( sampler, uv, vec4( cLodSizeInv, cLodSize ), ceil( lod ) );
		return mix( fSample, cSample, fract( lod ) );
	}
	vec3 getVolumeTransmissionRay( const in vec3 n, const in vec3 v, const in float thickness, const in float ior, const in mat4 modelMatrix ) {
		vec3 refractionVector = refract( - v, normalize( n ), 1.0 / ior );
		vec3 modelScale;
		modelScale.x = length( vec3( modelMatrix[ 0 ].xyz ) );
		modelScale.y = length( vec3( modelMatrix[ 1 ].xyz ) );
		modelScale.z = length( vec3( modelMatrix[ 2 ].xyz ) );
		return normalize( refractionVector ) * thickness * modelScale;
	}
	float applyIorToRoughness( const in float roughness, const in float ior ) {
		return roughness * clamp( ior * 2.0 - 2.0, 0.0, 1.0 );
	}
	vec4 getTransmissionSample( const in vec2 fragCoord, const in float roughness, const in float ior ) {
		float lod = log2( transmissionSamplerSize.x ) * applyIorToRoughness( roughness, ior );
		return textureBicubic( transmissionSamplerMap, fragCoord.xy, lod );
	}
	vec3 volumeAttenuation( const in float transmissionDistance, const in vec3 attenuationColor, const in float attenuationDistance ) {
		if ( isinf( attenuationDistance ) ) {
			return vec3( 1.0 );
		} else {
			vec3 attenuationCoefficient = -log( attenuationColor ) / attenuationDistance;
			vec3 transmittance = exp( - attenuationCoefficient * transmissionDistance );			return transmittance;
		}
	}
	vec4 getIBLVolumeRefraction( const in vec3 n, const in vec3 v, const in float roughness, const in vec3 diffuseColor,
		const in vec3 specularColor, const in float specularF90, const in vec3 position, const in mat4 modelMatrix,
		const in mat4 viewMatrix, const in mat4 projMatrix, const in float dispersion, const in float ior, const in float thickness,
		const in vec3 attenuationColor, const in float attenuationDistance ) {
		vec4 transmittedLight;
		vec3 transmittance;
		#ifdef USE_DISPERSION
			float halfSpread = ( ior - 1.0 ) * 0.025 * dispersion;
			vec3 iors = vec3( ior - halfSpread, ior, ior + halfSpread );
			for ( int i = 0; i < 3; i ++ ) {
				vec3 transmissionRay = getVolumeTransmissionRay( n, v, thickness, iors[ i ], modelMatrix );
				vec3 refractedRayExit = position + transmissionRay;
				vec4 ndcPos = projMatrix * viewMatrix * vec4( refractedRayExit, 1.0 );
				vec2 refractionCoords = ndcPos.xy / ndcPos.w;
				refractionCoords += 1.0;
				refractionCoords /= 2.0;
				vec4 transmissionSample = getTransmissionSample( refractionCoords, roughness, iors[ i ] );
				transmittedLight[ i ] = transmissionSample[ i ];
				transmittedLight.a += transmissionSample.a;
				transmittance[ i ] = diffuseColor[ i ] * volumeAttenuation( length( transmissionRay ), attenuationColor, attenuationDistance )[ i ];
			}
			transmittedLight.a /= 3.0;
		#else
			vec3 transmissionRay = getVolumeTransmissionRay( n, v, thickness, ior, modelMatrix );
			vec3 refractedRayExit = position + transmissionRay;
			vec4 ndcPos = projMatrix * viewMatrix * vec4( refractedRayExit, 1.0 );
			vec2 refractionCoords = ndcPos.xy / ndcPos.w;
			refractionCoords += 1.0;
			refractionCoords /= 2.0;
			transmittedLight = getTransmissionSample( refractionCoords, roughness, ior );
			transmittance = diffuseColor * volumeAttenuation( length( transmissionRay ), attenuationColor, attenuationDistance );
		#endif
		vec3 attenuatedColor = transmittance * transmittedLight.rgb;
		vec3 F = EnvironmentBRDF( n, v, specularColor, specularF90, roughness );
		float transmittanceFactor = ( transmittance.r + transmittance.g + transmittance.b ) / 3.0;
		return vec4( ( 1.0 - F ) * attenuatedColor, 1.0 - ( 1.0 - transmittedLight.a ) * transmittanceFactor );
	}
#endif`,Rm=`#if defined( USE_UV ) || defined( USE_ANISOTROPY )
	varying vec2 vUv;
#endif
#ifdef USE_MAP
	varying vec2 vMapUv;
#endif
#ifdef USE_ALPHAMAP
	varying vec2 vAlphaMapUv;
#endif
#ifdef USE_LIGHTMAP
	varying vec2 vLightMapUv;
#endif
#ifdef USE_AOMAP
	varying vec2 vAoMapUv;
#endif
#ifdef USE_BUMPMAP
	varying vec2 vBumpMapUv;
#endif
#ifdef USE_NORMALMAP
	varying vec2 vNormalMapUv;
#endif
#ifdef USE_EMISSIVEMAP
	varying vec2 vEmissiveMapUv;
#endif
#ifdef USE_METALNESSMAP
	varying vec2 vMetalnessMapUv;
#endif
#ifdef USE_ROUGHNESSMAP
	varying vec2 vRoughnessMapUv;
#endif
#ifdef USE_ANISOTROPYMAP
	varying vec2 vAnisotropyMapUv;
#endif
#ifdef USE_CLEARCOATMAP
	varying vec2 vClearcoatMapUv;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	varying vec2 vClearcoatNormalMapUv;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	varying vec2 vClearcoatRoughnessMapUv;
#endif
#ifdef USE_IRIDESCENCEMAP
	varying vec2 vIridescenceMapUv;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	varying vec2 vIridescenceThicknessMapUv;
#endif
#ifdef USE_SHEEN_COLORMAP
	varying vec2 vSheenColorMapUv;
#endif
#ifdef USE_SHEEN_ROUGHNESSMAP
	varying vec2 vSheenRoughnessMapUv;
#endif
#ifdef USE_SPECULARMAP
	varying vec2 vSpecularMapUv;
#endif
#ifdef USE_SPECULAR_COLORMAP
	varying vec2 vSpecularColorMapUv;
#endif
#ifdef USE_SPECULAR_INTENSITYMAP
	varying vec2 vSpecularIntensityMapUv;
#endif
#ifdef USE_TRANSMISSIONMAP
	uniform mat3 transmissionMapTransform;
	varying vec2 vTransmissionMapUv;
#endif
#ifdef USE_THICKNESSMAP
	uniform mat3 thicknessMapTransform;
	varying vec2 vThicknessMapUv;
#endif`,Cm=`#if defined( USE_UV ) || defined( USE_ANISOTROPY )
	varying vec2 vUv;
#endif
#ifdef USE_MAP
	uniform mat3 mapTransform;
	varying vec2 vMapUv;
#endif
#ifdef USE_ALPHAMAP
	uniform mat3 alphaMapTransform;
	varying vec2 vAlphaMapUv;
#endif
#ifdef USE_LIGHTMAP
	uniform mat3 lightMapTransform;
	varying vec2 vLightMapUv;
#endif
#ifdef USE_AOMAP
	uniform mat3 aoMapTransform;
	varying vec2 vAoMapUv;
#endif
#ifdef USE_BUMPMAP
	uniform mat3 bumpMapTransform;
	varying vec2 vBumpMapUv;
#endif
#ifdef USE_NORMALMAP
	uniform mat3 normalMapTransform;
	varying vec2 vNormalMapUv;
#endif
#ifdef USE_DISPLACEMENTMAP
	uniform mat3 displacementMapTransform;
	varying vec2 vDisplacementMapUv;
#endif
#ifdef USE_EMISSIVEMAP
	uniform mat3 emissiveMapTransform;
	varying vec2 vEmissiveMapUv;
#endif
#ifdef USE_METALNESSMAP
	uniform mat3 metalnessMapTransform;
	varying vec2 vMetalnessMapUv;
#endif
#ifdef USE_ROUGHNESSMAP
	uniform mat3 roughnessMapTransform;
	varying vec2 vRoughnessMapUv;
#endif
#ifdef USE_ANISOTROPYMAP
	uniform mat3 anisotropyMapTransform;
	varying vec2 vAnisotropyMapUv;
#endif
#ifdef USE_CLEARCOATMAP
	uniform mat3 clearcoatMapTransform;
	varying vec2 vClearcoatMapUv;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	uniform mat3 clearcoatNormalMapTransform;
	varying vec2 vClearcoatNormalMapUv;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	uniform mat3 clearcoatRoughnessMapTransform;
	varying vec2 vClearcoatRoughnessMapUv;
#endif
#ifdef USE_SHEEN_COLORMAP
	uniform mat3 sheenColorMapTransform;
	varying vec2 vSheenColorMapUv;
#endif
#ifdef USE_SHEEN_ROUGHNESSMAP
	uniform mat3 sheenRoughnessMapTransform;
	varying vec2 vSheenRoughnessMapUv;
#endif
#ifdef USE_IRIDESCENCEMAP
	uniform mat3 iridescenceMapTransform;
	varying vec2 vIridescenceMapUv;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	uniform mat3 iridescenceThicknessMapTransform;
	varying vec2 vIridescenceThicknessMapUv;
#endif
#ifdef USE_SPECULARMAP
	uniform mat3 specularMapTransform;
	varying vec2 vSpecularMapUv;
#endif
#ifdef USE_SPECULAR_COLORMAP
	uniform mat3 specularColorMapTransform;
	varying vec2 vSpecularColorMapUv;
#endif
#ifdef USE_SPECULAR_INTENSITYMAP
	uniform mat3 specularIntensityMapTransform;
	varying vec2 vSpecularIntensityMapUv;
#endif
#ifdef USE_TRANSMISSIONMAP
	uniform mat3 transmissionMapTransform;
	varying vec2 vTransmissionMapUv;
#endif
#ifdef USE_THICKNESSMAP
	uniform mat3 thicknessMapTransform;
	varying vec2 vThicknessMapUv;
#endif`,Im=`#if defined( USE_UV ) || defined( USE_ANISOTROPY )
	vUv = vec3( uv, 1 ).xy;
#endif
#ifdef USE_MAP
	vMapUv = ( mapTransform * vec3( MAP_UV, 1 ) ).xy;
#endif
#ifdef USE_ALPHAMAP
	vAlphaMapUv = ( alphaMapTransform * vec3( ALPHAMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_LIGHTMAP
	vLightMapUv = ( lightMapTransform * vec3( LIGHTMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_AOMAP
	vAoMapUv = ( aoMapTransform * vec3( AOMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_BUMPMAP
	vBumpMapUv = ( bumpMapTransform * vec3( BUMPMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_NORMALMAP
	vNormalMapUv = ( normalMapTransform * vec3( NORMALMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_DISPLACEMENTMAP
	vDisplacementMapUv = ( displacementMapTransform * vec3( DISPLACEMENTMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_EMISSIVEMAP
	vEmissiveMapUv = ( emissiveMapTransform * vec3( EMISSIVEMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_METALNESSMAP
	vMetalnessMapUv = ( metalnessMapTransform * vec3( METALNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_ROUGHNESSMAP
	vRoughnessMapUv = ( roughnessMapTransform * vec3( ROUGHNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_ANISOTROPYMAP
	vAnisotropyMapUv = ( anisotropyMapTransform * vec3( ANISOTROPYMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_CLEARCOATMAP
	vClearcoatMapUv = ( clearcoatMapTransform * vec3( CLEARCOATMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_CLEARCOAT_NORMALMAP
	vClearcoatNormalMapUv = ( clearcoatNormalMapTransform * vec3( CLEARCOAT_NORMALMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_CLEARCOAT_ROUGHNESSMAP
	vClearcoatRoughnessMapUv = ( clearcoatRoughnessMapTransform * vec3( CLEARCOAT_ROUGHNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_IRIDESCENCEMAP
	vIridescenceMapUv = ( iridescenceMapTransform * vec3( IRIDESCENCEMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_IRIDESCENCE_THICKNESSMAP
	vIridescenceThicknessMapUv = ( iridescenceThicknessMapTransform * vec3( IRIDESCENCE_THICKNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SHEEN_COLORMAP
	vSheenColorMapUv = ( sheenColorMapTransform * vec3( SHEEN_COLORMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SHEEN_ROUGHNESSMAP
	vSheenRoughnessMapUv = ( sheenRoughnessMapTransform * vec3( SHEEN_ROUGHNESSMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SPECULARMAP
	vSpecularMapUv = ( specularMapTransform * vec3( SPECULARMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SPECULAR_COLORMAP
	vSpecularColorMapUv = ( specularColorMapTransform * vec3( SPECULAR_COLORMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_SPECULAR_INTENSITYMAP
	vSpecularIntensityMapUv = ( specularIntensityMapTransform * vec3( SPECULAR_INTENSITYMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_TRANSMISSIONMAP
	vTransmissionMapUv = ( transmissionMapTransform * vec3( TRANSMISSIONMAP_UV, 1 ) ).xy;
#endif
#ifdef USE_THICKNESSMAP
	vThicknessMapUv = ( thicknessMapTransform * vec3( THICKNESSMAP_UV, 1 ) ).xy;
#endif`,Pm=`#if defined( USE_ENVMAP ) || defined( DISTANCE ) || defined ( USE_SHADOWMAP ) || defined ( USE_TRANSMISSION ) || NUM_SPOT_LIGHT_COORDS > 0
	vec4 worldPosition = vec4( transformed, 1.0 );
	#ifdef USE_BATCHING
		worldPosition = batchingMatrix * worldPosition;
	#endif
	#ifdef USE_INSTANCING
		worldPosition = instanceMatrix * worldPosition;
	#endif
	worldPosition = modelMatrix * worldPosition;
#endif`,Lm=`varying vec2 vUv;
uniform mat3 uvTransform;
void main() {
	vUv = ( uvTransform * vec3( uv, 1 ) ).xy;
	gl_Position = vec4( position.xy, 1.0, 1.0 );
}`,Dm=`uniform sampler2D t2D;
uniform float backgroundIntensity;
varying vec2 vUv;
void main() {
	vec4 texColor = texture2D( t2D, vUv );
	#ifdef DECODE_VIDEO_TEXTURE
		texColor = vec4( mix( pow( texColor.rgb * 0.9478672986 + vec3( 0.0521327014 ), vec3( 2.4 ) ), texColor.rgb * 0.0773993808, vec3( lessThanEqual( texColor.rgb, vec3( 0.04045 ) ) ) ), texColor.w );
	#endif
	texColor.rgb *= backgroundIntensity;
	gl_FragColor = texColor;
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,Um=`varying vec3 vWorldDirection;
#include <common>
void main() {
	vWorldDirection = transformDirection( position, modelMatrix );
	#include <begin_vertex>
	#include <project_vertex>
	gl_Position.z = gl_Position.w;
}`,Nm=`#ifdef ENVMAP_TYPE_CUBE
	uniform samplerCube envMap;
#elif defined( ENVMAP_TYPE_CUBE_UV )
	uniform sampler2D envMap;
#endif
uniform float backgroundBlurriness;
uniform float backgroundIntensity;
uniform mat3 backgroundRotation;
varying vec3 vWorldDirection;
#include <cube_uv_reflection_fragment>
void main() {
	#ifdef ENVMAP_TYPE_CUBE
		vec4 texColor = textureCube( envMap, backgroundRotation * vWorldDirection );
	#elif defined( ENVMAP_TYPE_CUBE_UV )
		vec4 texColor = textureCubeUV( envMap, backgroundRotation * vWorldDirection, backgroundBlurriness );
	#else
		vec4 texColor = vec4( 0.0, 0.0, 0.0, 1.0 );
	#endif
	texColor.rgb *= backgroundIntensity;
	gl_FragColor = texColor;
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,Fm=`varying vec3 vWorldDirection;
#include <common>
void main() {
	vWorldDirection = transformDirection( position, modelMatrix );
	#include <begin_vertex>
	#include <project_vertex>
	gl_Position.z = gl_Position.w;
}`,Om=`uniform samplerCube tCube;
uniform float tFlip;
uniform float opacity;
varying vec3 vWorldDirection;
void main() {
	vec4 texColor = textureCube( tCube, vec3( tFlip * vWorldDirection.x, vWorldDirection.yz ) );
	gl_FragColor = texColor;
	gl_FragColor.a *= opacity;
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,Bm=`#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
varying vec2 vHighPrecisionZW;
void main() {
	#include <uv_vertex>
	#include <batching_vertex>
	#include <skinbase_vertex>
	#include <morphinstance_vertex>
	#ifdef USE_DISPLACEMENTMAP
		#include <beginnormal_vertex>
		#include <morphnormal_vertex>
		#include <skinnormal_vertex>
	#endif
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vHighPrecisionZW = gl_Position.zw;
}`,zm=`#if DEPTH_PACKING == 3200
	uniform float opacity;
#endif
#include <common>
#include <packing>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
varying vec2 vHighPrecisionZW;
void main() {
	vec4 diffuseColor = vec4( 1.0 );
	#include <clipping_planes_fragment>
	#if DEPTH_PACKING == 3200
		diffuseColor.a = opacity;
	#endif
	#include <map_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <logdepthbuf_fragment>
	#ifdef USE_REVERSED_DEPTH_BUFFER
		float fragCoordZ = vHighPrecisionZW[ 0 ] / vHighPrecisionZW[ 1 ];
	#else
		float fragCoordZ = 0.5 * vHighPrecisionZW[ 0 ] / vHighPrecisionZW[ 1 ] + 0.5;
	#endif
	#if DEPTH_PACKING == 3200
		gl_FragColor = vec4( vec3( 1.0 - fragCoordZ ), opacity );
	#elif DEPTH_PACKING == 3201
		gl_FragColor = packDepthToRGBA( fragCoordZ );
	#elif DEPTH_PACKING == 3202
		gl_FragColor = vec4( packDepthToRGB( fragCoordZ ), 1.0 );
	#elif DEPTH_PACKING == 3203
		gl_FragColor = vec4( packDepthToRG( fragCoordZ ), 0.0, 1.0 );
	#endif
}`,Hm=`#define DISTANCE
varying vec3 vWorldPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <batching_vertex>
	#include <skinbase_vertex>
	#include <morphinstance_vertex>
	#ifdef USE_DISPLACEMENTMAP
		#include <beginnormal_vertex>
		#include <morphnormal_vertex>
		#include <skinnormal_vertex>
	#endif
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <worldpos_vertex>
	#include <clipping_planes_vertex>
	vWorldPosition = worldPosition.xyz;
}`,Vm=`#define DISTANCE
uniform vec3 referencePosition;
uniform float nearDistance;
uniform float farDistance;
varying vec3 vWorldPosition;
#include <common>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( 1.0 );
	#include <clipping_planes_fragment>
	#include <map_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	float dist = length( vWorldPosition - referencePosition );
	dist = ( dist - nearDistance ) / ( farDistance - nearDistance );
	dist = saturate( dist );
	gl_FragColor = vec4( dist, 0.0, 0.0, 1.0 );
}`,km=`varying vec3 vWorldDirection;
#include <common>
void main() {
	vWorldDirection = transformDirection( position, modelMatrix );
	#include <begin_vertex>
	#include <project_vertex>
}`,Gm=`uniform sampler2D tEquirect;
varying vec3 vWorldDirection;
#include <common>
void main() {
	vec3 direction = normalize( vWorldDirection );
	vec2 sampleUV = equirectUv( direction );
	gl_FragColor = texture2D( tEquirect, sampleUV );
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
}`,Wm=`uniform float scale;
attribute float lineDistance;
varying float vLineDistance;
#include <common>
#include <uv_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	vLineDistance = scale * lineDistance;
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <fog_vertex>
}`,Xm=`uniform vec3 diffuse;
uniform float opacity;
uniform float dashSize;
uniform float totalSize;
varying float vLineDistance;
#include <common>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <fog_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	if ( mod( vLineDistance, totalSize ) > dashSize ) {
		discard;
	}
	vec3 outgoingLight = vec3( 0.0 );
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	outgoingLight = diffuseColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
}`,qm=`#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <envmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#if defined ( USE_ENVMAP ) || defined ( USE_SKINNING )
		#include <beginnormal_vertex>
		#include <morphnormal_vertex>
		#include <skinbase_vertex>
		#include <skinnormal_vertex>
		#include <defaultnormal_vertex>
	#endif
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <worldpos_vertex>
	#include <envmap_vertex>
	#include <fog_vertex>
}`,Ym=`uniform vec3 diffuse;
uniform float opacity;
#ifndef FLAT_SHADED
	varying vec3 vNormal;
#endif
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_pars_fragment>
#include <fog_pars_fragment>
#include <specularmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <specularmap_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	#ifdef USE_LIGHTMAP
		vec4 lightMapTexel = texture2D( lightMap, vLightMapUv );
		reflectedLight.indirectDiffuse += lightMapTexel.rgb * lightMapIntensity * RECIPROCAL_PI;
	#else
		reflectedLight.indirectDiffuse += vec3( 1.0 );
	#endif
	#include <aomap_fragment>
	reflectedLight.indirectDiffuse *= diffuseColor.rgb;
	vec3 outgoingLight = reflectedLight.indirectDiffuse;
	#include <envmap_fragment>
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,Zm=`#define LAMBERT
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <envmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <envmap_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,Jm=`#define LAMBERT
uniform vec3 diffuse;
uniform vec3 emissive;
uniform float opacity;
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <cube_uv_reflection_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_pars_fragment>
#include <envmap_physical_pars_fragment>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_lambert_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <specularmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <specularmap_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_lambert_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 outgoingLight = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse + totalEmissiveRadiance;
	#include <envmap_fragment>
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,$m=`#define MATCAP
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <color_pars_vertex>
#include <displacementmap_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <fog_vertex>
	vViewPosition = - mvPosition.xyz;
}`,Km=`#define MATCAP
uniform vec3 diffuse;
uniform float opacity;
uniform sampler2D matcap;
varying vec3 vViewPosition;
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <fog_pars_fragment>
#include <normal_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	vec3 viewDir = normalize( vViewPosition );
	vec3 x = normalize( vec3( viewDir.z, 0.0, - viewDir.x ) );
	vec3 y = cross( viewDir, x );
	vec2 uv = vec2( dot( x, normal ), dot( y, normal ) ) * 0.495 + 0.5;
	#ifdef USE_MATCAP
		vec4 matcapColor = texture2D( matcap, uv );
	#else
		vec4 matcapColor = vec4( vec3( mix( 0.2, 0.8, uv.y ) ), 1.0 );
	#endif
	vec3 outgoingLight = diffuseColor.rgb * matcapColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,Qm=`#define NORMAL
#if defined( FLAT_SHADED ) || defined( USE_BUMPMAP ) || defined( USE_NORMALMAP_TANGENTSPACE )
	varying vec3 vViewPosition;
#endif
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphinstance_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
#if defined( FLAT_SHADED ) || defined( USE_BUMPMAP ) || defined( USE_NORMALMAP_TANGENTSPACE )
	vViewPosition = - mvPosition.xyz;
#endif
}`,jm=`#define NORMAL
uniform float opacity;
#if defined( FLAT_SHADED ) || defined( USE_BUMPMAP ) || defined( USE_NORMALMAP_TANGENTSPACE )
	varying vec3 vViewPosition;
#endif
#include <uv_pars_fragment>
#include <normal_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( 0.0, 0.0, 0.0, opacity );
	#include <clipping_planes_fragment>
	#include <logdepthbuf_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	gl_FragColor = vec4( normalize( normal ) * 0.5 + 0.5, diffuseColor.a );
	#ifdef OPAQUE
		gl_FragColor.a = 1.0;
	#endif
}`,eg=`#define PHONG
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <envmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphinstance_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <envmap_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,tg=`#define PHONG
uniform vec3 diffuse;
uniform vec3 emissive;
uniform vec3 specular;
uniform float shininess;
uniform float opacity;
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <cube_uv_reflection_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_pars_fragment>
#include <envmap_physical_pars_fragment>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_phong_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <specularmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <specularmap_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_phong_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 outgoingLight = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse + reflectedLight.directSpecular + reflectedLight.indirectSpecular + totalEmissiveRadiance;
	#include <envmap_fragment>
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,ng=`#define STANDARD
varying vec3 vViewPosition;
#ifdef USE_TRANSMISSION
	varying vec3 vWorldPosition;
#endif
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
#ifdef USE_TRANSMISSION
	vWorldPosition = worldPosition.xyz;
#endif
}`,ig=`#define STANDARD
#ifdef PHYSICAL
	#define IOR
	#define USE_SPECULAR
#endif
uniform vec3 diffuse;
uniform vec3 emissive;
uniform float roughness;
uniform float metalness;
uniform float opacity;
#ifdef IOR
	uniform float ior;
#endif
#ifdef USE_SPECULAR
	uniform float specularIntensity;
	uniform vec3 specularColor;
	#ifdef USE_SPECULAR_COLORMAP
		uniform sampler2D specularColorMap;
	#endif
	#ifdef USE_SPECULAR_INTENSITYMAP
		uniform sampler2D specularIntensityMap;
	#endif
#endif
#ifdef USE_CLEARCOAT
	uniform float clearcoat;
	uniform float clearcoatRoughness;
#endif
#ifdef USE_DISPERSION
	uniform float dispersion;
#endif
#ifdef USE_RETROREFLECTION
	uniform float retroreflectivity;
#endif
#ifdef USE_IRIDESCENCE
	uniform float iridescence;
	uniform float iridescenceIOR;
	uniform float iridescenceThicknessMinimum;
	uniform float iridescenceThicknessMaximum;
#endif
#ifdef USE_SHEEN
	uniform vec3 sheenColor;
	uniform float sheenRoughness;
	#ifdef USE_SHEEN_COLORMAP
		uniform sampler2D sheenColorMap;
	#endif
	#ifdef USE_SHEEN_ROUGHNESSMAP
		uniform sampler2D sheenRoughnessMap;
	#endif
#endif
#ifdef USE_ANISOTROPY
	uniform vec2 anisotropyVector;
	#ifdef USE_ANISOTROPYMAP
		uniform sampler2D anisotropyMap;
	#endif
#endif
varying vec3 vViewPosition;
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <iridescence_fragment>
#include <cube_uv_reflection_fragment>
#include <envmap_common_pars_fragment>
#include <envmap_physical_pars_fragment>
#include <fog_pars_fragment>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_physical_pars_fragment>
#include <transmission_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <clearcoat_pars_fragment>
#include <iridescence_pars_fragment>
#include <roughnessmap_pars_fragment>
#include <metalnessmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <roughnessmap_fragment>
	#include <metalnessmap_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <clearcoat_normal_fragment_begin>
	#include <clearcoat_normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_physical_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 totalDiffuse = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse;
	vec3 totalSpecular = reflectedLight.directSpecular + reflectedLight.indirectSpecular;
	#include <transmission_fragment>
	vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;
	#ifdef USE_SHEEN
 
		outgoingLight = outgoingLight + sheenSpecularDirect + sheenSpecularIndirect;
 
 	#endif
	#ifdef USE_CLEARCOAT
		float dotNVcc = saturate( dot( geometryClearcoatNormal, geometryViewDir ) );
		vec3 Fcc = F_Schlick( material.clearcoatF0, material.clearcoatF90, dotNVcc );
		outgoingLight = outgoingLight * ( 1.0 - material.clearcoat * Fcc ) + ( clearcoatSpecularDirect + clearcoatSpecularIndirect ) * material.clearcoat;
	#endif
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,sg=`#define TOON
varying vec3 vViewPosition;
#include <common>
#include <batching_pars_vertex>
#include <uv_pars_vertex>
#include <displacementmap_pars_vertex>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <normal_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <shadowmap_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <normal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <displacementmap_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	vViewPosition = - mvPosition.xyz;
	#include <worldpos_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,rg=`#define TOON
uniform vec3 diffuse;
uniform vec3 emissive;
uniform float opacity;
#include <common>
#include <dithering_pars_fragment>
#include <color_pars_fragment>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <aomap_pars_fragment>
#include <lightmap_pars_fragment>
#include <emissivemap_pars_fragment>
#include <gradientmap_pars_fragment>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <normal_pars_fragment>
#include <lights_toon_pars_fragment>
#include <shadowmap_pars_fragment>
#include <bumpmap_pars_fragment>
#include <normalmap_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	ReflectedLight reflectedLight = ReflectedLight( vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ), vec3( 0.0 ) );
	vec3 totalEmissiveRadiance = emissive;
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <color_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	#include <normal_fragment_begin>
	#include <normal_fragment_maps>
	#include <emissivemap_fragment>
	#include <lights_toon_fragment>
	#include <lights_fragment_begin>
	#include <lights_fragment_maps>
	#include <lights_fragment_end>
	#include <aomap_fragment>
	vec3 outgoingLight = reflectedLight.directDiffuse + reflectedLight.indirectDiffuse + totalEmissiveRadiance;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
	#include <dithering_fragment>
}`,ag=`uniform float size;
uniform float scale;
#include <common>
#include <color_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
#ifdef USE_POINTS_UV
	varying vec2 vUv;
	uniform mat3 uvTransform;
#endif
void main() {
	#ifdef USE_POINTS_UV
		vUv = ( uvTransform * vec3( uv, 1 ) ).xy;
	#endif
	#include <color_vertex>
	#include <morphinstance_vertex>
	#include <morphcolor_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <project_vertex>
	gl_PointSize = size;
	#ifdef USE_SIZEATTENUATION
		bool isPerspective = isPerspectiveMatrix( projectionMatrix );
		if ( isPerspective ) gl_PointSize *= ( scale / - mvPosition.z );
	#endif
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <worldpos_vertex>
	#include <fog_vertex>
}`,og=`uniform vec3 diffuse;
uniform float opacity;
#include <common>
#include <color_pars_fragment>
#include <map_particle_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <fog_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	vec3 outgoingLight = vec3( 0.0 );
	#include <logdepthbuf_fragment>
	#include <map_particle_fragment>
	#include <color_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	outgoingLight = diffuseColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
}`,lg=`#include <common>
#include <batching_pars_vertex>
#include <fog_pars_vertex>
#include <morphtarget_pars_vertex>
#include <skinning_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <shadowmap_pars_vertex>
void main() {
	#include <batching_vertex>
	#include <beginnormal_vertex>
	#include <morphinstance_vertex>
	#include <morphnormal_vertex>
	#include <skinbase_vertex>
	#include <skinnormal_vertex>
	#include <defaultnormal_vertex>
	#include <begin_vertex>
	#include <morphtarget_vertex>
	#include <skinning_vertex>
	#include <project_vertex>
	#include <logdepthbuf_vertex>
	#include <worldpos_vertex>
	#include <shadowmap_vertex>
	#include <fog_vertex>
}`,cg=`uniform vec3 color;
uniform float opacity;
#include <common>
#include <fog_pars_fragment>
#include <bsdfs>
#include <lights_pars_begin>
#include <logdepthbuf_pars_fragment>
#include <shadowmap_pars_fragment>
#include <shadowmask_pars_fragment>
void main() {
	#include <logdepthbuf_fragment>
	gl_FragColor = vec4( color, opacity * ( 1.0 - getShadowMask() ) );
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
	#include <premultiplied_alpha_fragment>
}`,hg=`uniform float rotation;
uniform vec2 center;
#include <common>
#include <uv_pars_vertex>
#include <fog_pars_vertex>
#include <logdepthbuf_pars_vertex>
#include <clipping_planes_pars_vertex>
void main() {
	#include <uv_vertex>
	vec4 mvPosition = modelViewMatrix[ 3 ];
	vec2 scale = vec2( length( modelMatrix[ 0 ].xyz ), length( modelMatrix[ 1 ].xyz ) );
	#ifndef USE_SIZEATTENUATION
		bool isPerspective = isPerspectiveMatrix( projectionMatrix );
		if ( isPerspective ) scale *= - mvPosition.z;
	#endif
	vec2 alignedPosition = ( position.xy - ( center - vec2( 0.5 ) ) ) * scale;
	vec2 rotatedPosition;
	rotatedPosition.x = cos( rotation ) * alignedPosition.x - sin( rotation ) * alignedPosition.y;
	rotatedPosition.y = sin( rotation ) * alignedPosition.x + cos( rotation ) * alignedPosition.y;
	mvPosition.xy += rotatedPosition;
	gl_Position = projectionMatrix * mvPosition;
	#include <logdepthbuf_vertex>
	#include <clipping_planes_vertex>
	#include <fog_vertex>
}`,ug=`uniform vec3 diffuse;
uniform float opacity;
#include <common>
#include <uv_pars_fragment>
#include <map_pars_fragment>
#include <alphamap_pars_fragment>
#include <alphatest_pars_fragment>
#include <alphahash_pars_fragment>
#include <fog_pars_fragment>
#include <logdepthbuf_pars_fragment>
#include <clipping_planes_pars_fragment>
void main() {
	vec4 diffuseColor = vec4( diffuse, opacity );
	#include <clipping_planes_fragment>
	vec3 outgoingLight = vec3( 0.0 );
	#include <logdepthbuf_fragment>
	#include <map_fragment>
	#include <alphamap_fragment>
	#include <alphatest_fragment>
	#include <alphahash_fragment>
	outgoingLight = diffuseColor.rgb;
	#include <opaque_fragment>
	#include <tonemapping_fragment>
	#include <colorspace_fragment>
	#include <fog_fragment>
}`,Qe={alphahash_fragment:Lf,alphahash_pars_fragment:Df,alphamap_fragment:Uf,alphamap_pars_fragment:Nf,alphatest_fragment:Ff,alphatest_pars_fragment:Of,aomap_fragment:Bf,aomap_pars_fragment:zf,batching_pars_vertex:Hf,batching_vertex:Vf,begin_vertex:kf,beginnormal_vertex:Gf,bsdfs:Wf,iridescence_fragment:Xf,bumpmap_pars_fragment:qf,clipping_planes_fragment:Yf,clipping_planes_pars_fragment:Zf,clipping_planes_pars_vertex:Jf,clipping_planes_vertex:$f,color_fragment:Kf,color_pars_fragment:Qf,color_pars_vertex:jf,color_vertex:ep,common:tp,cube_uv_reflection_fragment:np,defaultnormal_vertex:ip,displacementmap_pars_vertex:sp,displacementmap_vertex:rp,emissivemap_fragment:ap,emissivemap_pars_fragment:op,colorspace_fragment:lp,colorspace_pars_fragment:cp,envmap_fragment:hp,envmap_common_pars_fragment:up,envmap_pars_fragment:dp,envmap_pars_vertex:fp,envmap_physical_pars_fragment:Ep,envmap_vertex:pp,fog_vertex:mp,fog_pars_vertex:gp,fog_fragment:_p,fog_pars_fragment:xp,gradientmap_pars_fragment:yp,lightmap_pars_fragment:vp,lights_lambert_fragment:Mp,lights_lambert_pars_fragment:Sp,lights_pars_begin:bp,lights_toon_fragment:Tp,lights_toon_pars_fragment:wp,lights_phong_fragment:Ap,lights_phong_pars_fragment:Rp,lights_physical_fragment:Cp,lights_physical_pars_fragment:Ip,lights_fragment_begin:Pp,lights_fragment_maps:Lp,lights_fragment_end:Dp,lightprobes_pars_fragment:Up,logdepthbuf_fragment:Np,logdepthbuf_pars_fragment:Fp,logdepthbuf_pars_vertex:Op,logdepthbuf_vertex:Bp,map_fragment:zp,map_pars_fragment:Hp,map_particle_fragment:Vp,map_particle_pars_fragment:kp,metalnessmap_fragment:Gp,metalnessmap_pars_fragment:Wp,morphinstance_vertex:Xp,morphcolor_vertex:qp,morphnormal_vertex:Yp,morphtarget_pars_vertex:Zp,morphtarget_vertex:Jp,normal_fragment_begin:$p,normal_fragment_maps:Kp,normal_pars_fragment:Qp,normal_pars_vertex:jp,normal_vertex:em,normalmap_pars_fragment:tm,clearcoat_normal_fragment_begin:nm,clearcoat_normal_fragment_maps:im,clearcoat_pars_fragment:sm,iridescence_pars_fragment:rm,opaque_fragment:am,packing:om,premultiplied_alpha_fragment:lm,project_vertex:cm,dithering_fragment:hm,dithering_pars_fragment:um,roughnessmap_fragment:dm,roughnessmap_pars_fragment:fm,shadowmap_pars_fragment:pm,shadowmap_pars_vertex:mm,shadowmap_vertex:gm,shadowmask_pars_fragment:_m,skinbase_vertex:xm,skinning_pars_vertex:ym,skinning_vertex:vm,skinnormal_vertex:Mm,specularmap_fragment:Sm,specularmap_pars_fragment:bm,tonemapping_fragment:Em,tonemapping_pars_fragment:Tm,transmission_fragment:wm,transmission_pars_fragment:Am,uv_pars_fragment:Rm,uv_pars_vertex:Cm,uv_vertex:Im,worldpos_vertex:Pm,background_vert:Lm,background_frag:Dm,backgroundCube_vert:Um,backgroundCube_frag:Nm,cube_vert:Fm,cube_frag:Om,depth_vert:Bm,depth_frag:zm,distance_vert:Hm,distance_frag:Vm,equirect_vert:km,equirect_frag:Gm,linedashed_vert:Wm,linedashed_frag:Xm,meshbasic_vert:qm,meshbasic_frag:Ym,meshlambert_vert:Zm,meshlambert_frag:Jm,meshmatcap_vert:$m,meshmatcap_frag:Km,meshnormal_vert:Qm,meshnormal_frag:jm,meshphong_vert:eg,meshphong_frag:tg,meshphysical_vert:ng,meshphysical_frag:ig,meshtoon_vert:sg,meshtoon_frag:rg,points_vert:ag,points_frag:og,shadow_vert:lg,shadow_frag:cg,sprite_vert:hg,sprite_frag:ug},Ee={common:{diffuse:{value:new Fe(16777215)},opacity:{value:1},map:{value:null},mapTransform:{value:new Je},alphaMap:{value:null},alphaMapTransform:{value:new Je},alphaTest:{value:0}},specularmap:{specularMap:{value:null},specularMapTransform:{value:new Je}},envmap:{envMap:{value:null},envMapRotation:{value:new Je},reflectivity:{value:1},ior:{value:1.5},refractionRatio:{value:.98},dfgLUT:{value:null}},aomap:{aoMap:{value:null},aoMapIntensity:{value:1},aoMapTransform:{value:new Je}},lightmap:{lightMap:{value:null},lightMapIntensity:{value:1},lightMapTransform:{value:new Je}},bumpmap:{bumpMap:{value:null},bumpMapTransform:{value:new Je},bumpScale:{value:1}},normalmap:{normalMap:{value:null},normalMapTransform:{value:new Je},normalScale:{value:new he(1,1)}},displacementmap:{displacementMap:{value:null},displacementMapTransform:{value:new Je},displacementScale:{value:1},displacementBias:{value:0}},emissivemap:{emissiveMap:{value:null},emissiveMapTransform:{value:new Je}},metalnessmap:{metalnessMap:{value:null},metalnessMapTransform:{value:new Je}},roughnessmap:{roughnessMap:{value:null},roughnessMapTransform:{value:new Je}},gradientmap:{gradientMap:{value:null}},fog:{fogDensity:{value:25e-5},fogNear:{value:1},fogFar:{value:2e3},fogColor:{value:new Fe(16777215)}},lights:{ambientLightColor:{value:[]},lightProbe:{value:[]},sunLights:{value:[],properties:{direction:{},color:{}}},sunLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{}}},sunShadowMatrix:{value:[]},sunShadowCascade:{value:[]},directionalLights:{value:[],properties:{direction:{},color:{}}},directionalLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{}}},directionalShadowMatrix:{value:[]},spotLights:{value:[],properties:{color:{},position:{},direction:{},distance:{},coneCos:{},penumbraCos:{},decay:{}}},spotLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{}}},spotLightMap:{value:[]},spotLightMatrix:{value:[]},pointLights:{value:[],properties:{color:{},position:{},decay:{},distance:{}}},pointLightShadows:{value:[],properties:{shadowIntensity:1,shadowBias:{},shadowNormalBias:{},shadowRadius:{},shadowMapSize:{},shadowCameraNear:{},shadowCameraFar:{}}},pointShadowMatrix:{value:[]},hemisphereLights:{value:[],properties:{direction:{},skyColor:{},groundColor:{}}},rectAreaLights:{value:[],properties:{color:{},position:{},width:{},height:{}}},ltc_1:{value:null},ltc_2:{value:null},probesSH:{value:null},probesMin:{value:new L},probesMax:{value:new L},probesResolution:{value:new L}},points:{diffuse:{value:new Fe(16777215)},opacity:{value:1},size:{value:1},scale:{value:1},map:{value:null},alphaMap:{value:null},alphaMapTransform:{value:new Je},alphaTest:{value:0},uvTransform:{value:new Je}},sprite:{diffuse:{value:new Fe(16777215)},opacity:{value:1},center:{value:new he(.5,.5)},rotation:{value:0},map:{value:null},mapTransform:{value:new Je},alphaMap:{value:null},alphaMapTransform:{value:new Je},alphaTest:{value:0}}},zn={basic:{uniforms:kt([Ee.common,Ee.specularmap,Ee.envmap,Ee.aomap,Ee.lightmap,Ee.fog]),vertexShader:Qe.meshbasic_vert,fragmentShader:Qe.meshbasic_frag},lambert:{uniforms:kt([Ee.common,Ee.specularmap,Ee.envmap,Ee.aomap,Ee.lightmap,Ee.emissivemap,Ee.bumpmap,Ee.normalmap,Ee.displacementmap,Ee.fog,Ee.lights,{emissive:{value:new Fe(0)},envMapIntensity:{value:1}}]),vertexShader:Qe.meshlambert_vert,fragmentShader:Qe.meshlambert_frag},phong:{uniforms:kt([Ee.common,Ee.specularmap,Ee.envmap,Ee.aomap,Ee.lightmap,Ee.emissivemap,Ee.bumpmap,Ee.normalmap,Ee.displacementmap,Ee.fog,Ee.lights,{emissive:{value:new Fe(0)},specular:{value:new Fe(1118481)},shininess:{value:30},envMapIntensity:{value:1}}]),vertexShader:Qe.meshphong_vert,fragmentShader:Qe.meshphong_frag},standard:{uniforms:kt([Ee.common,Ee.envmap,Ee.aomap,Ee.lightmap,Ee.emissivemap,Ee.bumpmap,Ee.normalmap,Ee.displacementmap,Ee.roughnessmap,Ee.metalnessmap,Ee.fog,Ee.lights,{emissive:{value:new Fe(0)},roughness:{value:1},metalness:{value:0},envMapIntensity:{value:1}}]),vertexShader:Qe.meshphysical_vert,fragmentShader:Qe.meshphysical_frag},toon:{uniforms:kt([Ee.common,Ee.aomap,Ee.lightmap,Ee.emissivemap,Ee.bumpmap,Ee.normalmap,Ee.displacementmap,Ee.gradientmap,Ee.fog,Ee.lights,{emissive:{value:new Fe(0)}}]),vertexShader:Qe.meshtoon_vert,fragmentShader:Qe.meshtoon_frag},matcap:{uniforms:kt([Ee.common,Ee.bumpmap,Ee.normalmap,Ee.displacementmap,Ee.fog,{matcap:{value:null}}]),vertexShader:Qe.meshmatcap_vert,fragmentShader:Qe.meshmatcap_frag},points:{uniforms:kt([Ee.points,Ee.fog]),vertexShader:Qe.points_vert,fragmentShader:Qe.points_frag},dashed:{uniforms:kt([Ee.common,Ee.fog,{scale:{value:1},dashSize:{value:1},totalSize:{value:2}}]),vertexShader:Qe.linedashed_vert,fragmentShader:Qe.linedashed_frag},depth:{uniforms:kt([Ee.common,Ee.displacementmap]),vertexShader:Qe.depth_vert,fragmentShader:Qe.depth_frag},normal:{uniforms:kt([Ee.common,Ee.bumpmap,Ee.normalmap,Ee.displacementmap,{opacity:{value:1}}]),vertexShader:Qe.meshnormal_vert,fragmentShader:Qe.meshnormal_frag},sprite:{uniforms:kt([Ee.sprite,Ee.fog]),vertexShader:Qe.sprite_vert,fragmentShader:Qe.sprite_frag},background:{uniforms:{uvTransform:{value:new Je},t2D:{value:null},backgroundIntensity:{value:1}},vertexShader:Qe.background_vert,fragmentShader:Qe.background_frag},backgroundCube:{uniforms:{envMap:{value:null},backgroundBlurriness:{value:0},backgroundIntensity:{value:1},backgroundRotation:{value:new Je}},vertexShader:Qe.backgroundCube_vert,fragmentShader:Qe.backgroundCube_frag},cube:{uniforms:{tCube:{value:null},tFlip:{value:-1},opacity:{value:1}},vertexShader:Qe.cube_vert,fragmentShader:Qe.cube_frag},equirect:{uniforms:{tEquirect:{value:null}},vertexShader:Qe.equirect_vert,fragmentShader:Qe.equirect_frag},distance:{uniforms:kt([Ee.common,Ee.displacementmap,{referencePosition:{value:new L},nearDistance:{value:1},farDistance:{value:1e3}}]),vertexShader:Qe.distance_vert,fragmentShader:Qe.distance_frag},shadow:{uniforms:kt([Ee.lights,Ee.fog,{color:{value:new Fe(0)},opacity:{value:1}}]),vertexShader:Qe.shadow_vert,fragmentShader:Qe.shadow_frag}};zn.physical={uniforms:kt([zn.standard.uniforms,{clearcoat:{value:0},clearcoatMap:{value:null},clearcoatMapTransform:{value:new Je},clearcoatNormalMap:{value:null},clearcoatNormalMapTransform:{value:new Je},clearcoatNormalScale:{value:new he(1,1)},clearcoatRoughness:{value:0},clearcoatRoughnessMap:{value:null},clearcoatRoughnessMapTransform:{value:new Je},dispersion:{value:0},retroreflectivity:{value:0},iridescence:{value:0},iridescenceMap:{value:null},iridescenceMapTransform:{value:new Je},iridescenceIOR:{value:1.3},iridescenceThicknessMinimum:{value:100},iridescenceThicknessMaximum:{value:400},iridescenceThicknessMap:{value:null},iridescenceThicknessMapTransform:{value:new Je},sheen:{value:0},sheenColor:{value:new Fe(0)},sheenColorMap:{value:null},sheenColorMapTransform:{value:new Je},sheenRoughness:{value:1},sheenRoughnessMap:{value:null},sheenRoughnessMapTransform:{value:new Je},transmission:{value:0},transmissionMap:{value:null},transmissionMapTransform:{value:new Je},transmissionSamplerSize:{value:new he},transmissionSamplerMap:{value:null},thickness:{value:0},thicknessMap:{value:null},thicknessMapTransform:{value:new Je},attenuationDistance:{value:0},attenuationColor:{value:new Fe(0)},specularColor:{value:new Fe(1,1,1)},specularColorMap:{value:null},specularColorMapTransform:{value:new Je},specularIntensity:{value:1},specularIntensityMap:{value:null},specularIntensityMapTransform:{value:new Je},anisotropyVector:{value:new he},anisotropyMap:{value:null},anisotropyMapTransform:{value:new Je}}]),vertexShader:Qe.meshphysical_vert,fragmentShader:Qe.meshphysical_frag};var nl={r:0,b:0,g:0},dg=new ut,Zu=new Je;Zu.set(-1,0,0,0,1,0,0,0,1);function fg(i,e,t,n,s,r){let a=new Fe(0),o=s===!0?0:1,c,l,h=null,d=0,u=null;function p(S){let R=S.isScene===!0?S.background:null;if(R&&R.isTexture){let M=S.backgroundBlurriness>0;R=e.get(R,M)}return R}function g(S){let R=!1,M=p(S);M===null?m(a,o):M&&M.isColor&&(m(M,1),R=!0);let T=i.xr.getEnvironmentBlendMode();T==="additive"?t.buffers.color.setClear(0,0,0,1,r):T==="alpha-blend"&&t.buffers.color.setClear(0,0,0,0,r),(i.autoClear||R)&&(t.buffers.depth.setTest(!0),t.buffers.depth.setMask(!0),t.buffers.color.setMask(!0),i.clear(i.autoClearColor,i.autoClearDepth,i.autoClearStencil))}function v(S,R){let M=p(R);M&&(M.isCubeTexture||M.mapping===Fr)?(l===void 0&&(l=new Ke(new Dt(1,1,1),new en({name:"BackgroundCubeMaterial",uniforms:Ci(zn.backgroundCube.uniforms),vertexShader:zn.backgroundCube.vertexShader,fragmentShader:zn.backgroundCube.fragmentShader,side:Ut,depthTest:!1,depthWrite:!1,fog:!1,allowOverride:!1})),l.geometry.deleteAttribute("normal"),l.geometry.deleteAttribute("uv"),l.onBeforeRender=function(T,w,P){this.matrixWorld.copyPosition(P.matrixWorld)},Object.defineProperty(l.material,"envMap",{get:function(){return this.uniforms.envMap.value}}),n.update(l)),l.material.uniforms.envMap.value=M,l.material.uniforms.backgroundBlurriness.value=R.backgroundBlurriness,l.material.uniforms.backgroundIntensity.value=R.backgroundIntensity,l.material.uniforms.backgroundRotation.value.setFromMatrix4(dg.makeRotationFromEuler(R.backgroundRotation)).transpose(),M.isCubeTexture&&M.isRenderTargetTexture===!1&&l.material.uniforms.backgroundRotation.value.premultiply(Zu),l.material.toneMapped=st.getTransfer(M.colorSpace)!==dt,(h!==M||d!==M.version||u!==i.toneMapping)&&(l.material.needsUpdate=!0,h=M,d=M.version,u=i.toneMapping),l.layers.enableAll(),S.unshift(l,l.geometry,l.material,0,0,null)):M&&M.isTexture&&(c===void 0&&(c=new Ke(new Kn(2,2),new en({name:"BackgroundMaterial",uniforms:Ci(zn.background.uniforms),vertexShader:zn.background.vertexShader,fragmentShader:zn.background.fragmentShader,side:fi,depthTest:!1,depthWrite:!1,fog:!1,allowOverride:!1})),c.geometry.deleteAttribute("normal"),Object.defineProperty(c.material,"map",{get:function(){return this.uniforms.t2D.value}}),n.update(c)),c.material.uniforms.t2D.value=M,c.material.uniforms.backgroundIntensity.value=R.backgroundIntensity,c.material.toneMapped=st.getTransfer(M.colorSpace)!==dt,M.matrixAutoUpdate===!0&&M.updateMatrix(),c.material.uniforms.uvTransform.value.copy(M.matrix),(h!==M||d!==M.version||u!==i.toneMapping)&&(c.material.needsUpdate=!0,h=M,d=M.version,u=i.toneMapping),c.layers.enableAll(),S.unshift(c,c.geometry,c.material,0,0,null))}function m(S,R){S.getRGB(nl,vc(i)),t.buffers.color.setClear(nl.r,nl.g,nl.b,R,r)}function f(){l!==void 0&&(l.geometry.dispose(),l.material.dispose(),l=void 0),c!==void 0&&(c.geometry.dispose(),c.material.dispose(),c=void 0)}return{getClearColor:function(){return a},setClearColor:function(S,R=1){a.set(S),o=R,m(a,o)},getClearAlpha:function(){return o},setClearAlpha:function(S){o=S,m(a,o)},render:g,addToRenderList:v,dispose:f}}function pg(i,e){let t=i.getParameter(i.MAX_VERTEX_ATTRIBS),n={},s=u(null),r=s,a=!1;function o(U,B,H,N,V){let X=!1,W=d(U,N,H,B);r!==W&&(r=W,l(r.object)),X=p(U,N,H,V),X&&g(U,N,H,V),V!==null&&e.update(V,i.ELEMENT_ARRAY_BUFFER),(X||a)&&(a=!1,M(U,B,H,N),V!==null&&i.bindBuffer(i.ELEMENT_ARRAY_BUFFER,e.get(V).buffer))}function c(){return i.createVertexArray()}function l(U){return i.bindVertexArray(U)}function h(U){return i.deleteVertexArray(U)}function d(U,B,H,N){let V=N.wireframe===!0,X=n[B.id];X===void 0&&(X={},n[B.id]=X);let W=U.isInstancedMesh===!0?U.id:0,re=X[W];re===void 0&&(re={},X[W]=re);let Y=re[H.id];Y===void 0&&(Y={},re[H.id]=Y);let Q=Y[V];return Q===void 0&&(Q=u(c()),Y[V]=Q),Q}function u(U){let B=[],H=[],N=[];for(let V=0;V<t;V++)B[V]=0,H[V]=0,N[V]=0;return{geometry:null,program:null,wireframe:!1,newAttributes:B,enabledAttributes:H,attributeDivisors:N,object:U,attributes:{},index:null}}function p(U,B,H,N){let V=r.attributes,X=B.attributes,W=0,re=H.getAttributes();for(let Y in re)if(re[Y].location>=0){let j=V[Y],Re=X[Y];if(Re===void 0&&(Y==="instanceMatrix"&&U.instanceMatrix&&(Re=U.instanceMatrix),Y==="instanceColor"&&U.instanceColor&&(Re=U.instanceColor)),j===void 0||j.attribute!==Re||Re&&j.data!==Re.data)return!0;W++}return r.attributesNum!==W||r.index!==N}function g(U,B,H,N){let V={},X=B.attributes,W=0,re=H.getAttributes();for(let Y in re)if(re[Y].location>=0){let j=X[Y];j===void 0&&(Y==="instanceMatrix"&&U.instanceMatrix&&(j=U.instanceMatrix),Y==="instanceColor"&&U.instanceColor&&(j=U.instanceColor));let Re={};Re.attribute=j,j&&j.data&&(Re.data=j.data),V[Y]=Re,W++}r.attributes=V,r.attributesNum=W,r.index=N}function v(){let U=r.newAttributes;for(let B=0,H=U.length;B<H;B++)U[B]=0}function m(U){f(U,0)}function f(U,B){let H=r.newAttributes,N=r.enabledAttributes,V=r.attributeDivisors;H[U]=1,N[U]===0&&(i.enableVertexAttribArray(U),N[U]=1),V[U]!==B&&(i.vertexAttribDivisor(U,B),V[U]=B)}function S(){let U=r.newAttributes,B=r.enabledAttributes;for(let H=0,N=B.length;H<N;H++)B[H]!==U[H]&&(i.disableVertexAttribArray(H),B[H]=0)}function R(U,B,H,N,V,X,W){W===!0?i.vertexAttribIPointer(U,B,H,V,X):i.vertexAttribPointer(U,B,H,N,V,X)}function M(U,B,H,N){v();let V=N.attributes,X=H.getAttributes(),W=B.defaultAttributeValues;for(let re in X){let Y=X[re];if(Y.location>=0){let Q=V[re];if(Q===void 0&&(re==="instanceMatrix"&&U.instanceMatrix&&(Q=U.instanceMatrix),re==="instanceColor"&&U.instanceColor&&(Q=U.instanceColor)),Q!==void 0){let j=Q.normalized,Re=Q.itemSize,Ce=e.get(Q);if(Ce===void 0)continue;let lt=Ce.buffer,je=Ce.type,ot=Ce.bytesPerElement,q=je===i.INT||je===i.UNSIGNED_INT||Q.gpuType===go;if(Q.isInterleavedBufferAttribute){let ne=Q.data,ve=ne.stride,Oe=Q.offset;if(ne.isInstancedInterleavedBuffer){for(let we=0;we<Y.locationSize;we++)f(Y.location+we,ne.meshPerAttribute);U.isInstancedMesh!==!0&&N._maxInstanceCount===void 0&&(N._maxInstanceCount=ne.meshPerAttribute*ne.count)}else for(let we=0;we<Y.locationSize;we++)m(Y.location+we);i.bindBuffer(i.ARRAY_BUFFER,lt);for(let we=0;we<Y.locationSize;we++)R(Y.location+we,Re/Y.locationSize,je,j,ve*ot,(Oe+Re/Y.locationSize*we)*ot,q)}else{if(Q.isInstancedBufferAttribute){for(let ne=0;ne<Y.locationSize;ne++)f(Y.location+ne,Q.meshPerAttribute);U.isInstancedMesh!==!0&&N._maxInstanceCount===void 0&&(N._maxInstanceCount=Q.meshPerAttribute*Q.count)}else for(let ne=0;ne<Y.locationSize;ne++)m(Y.location+ne);i.bindBuffer(i.ARRAY_BUFFER,lt);for(let ne=0;ne<Y.locationSize;ne++)R(Y.location+ne,Re/Y.locationSize,je,j,Re*ot,Re/Y.locationSize*ne*ot,q)}}else if(W!==void 0){let j=W[re];if(j!==void 0)switch(j.length){case 2:i.vertexAttrib2fv(Y.location,j);break;case 3:i.vertexAttrib3fv(Y.location,j);break;case 4:i.vertexAttrib4fv(Y.location,j);break;default:i.vertexAttrib1fv(Y.location,j)}}}}S()}function T(){E();for(let U in n){let B=n[U];for(let H in B){let N=B[H];for(let V in N){let X=N[V];for(let W in X)h(X[W].object),delete X[W];delete N[V]}}delete n[U]}}function w(U){if(n[U.id]===void 0)return;let B=n[U.id];for(let H in B){let N=B[H];for(let V in N){let X=N[V];for(let W in X)h(X[W].object),delete X[W];delete N[V]}}delete n[U.id]}function P(U){for(let B in n){let H=n[B];for(let N in H){let V=H[N];if(V[U.id]===void 0)continue;let X=V[U.id];for(let W in X)h(X[W].object),delete X[W];delete V[U.id]}}}function x(U){for(let B in n){let H=n[B],N=U.isInstancedMesh===!0?U.id:0,V=H[N];if(V!==void 0){for(let X in V){let W=V[X];for(let re in W)h(W[re].object),delete W[re];delete V[X]}delete H[N],Object.keys(H).length===0&&delete n[B]}}}function E(){C(),a=!0,r!==s&&(r=s,l(r.object))}function C(){s.geometry=null,s.program=null,s.wireframe=!1}return{setup:o,reset:E,resetDefaultState:C,dispose:T,releaseStatesOfGeometry:w,releaseStatesOfObject:x,releaseStatesOfProgram:P,initAttributes:v,enableAttribute:m,disableUnusedAttributes:S}}function mg(i,e,t){let n;function s(c){n=c}function r(c,l){i.drawArrays(n,c,l),t.update(l,n,1)}function a(c,l,h){h!==0&&(i.drawArraysInstanced(n,c,l,h),t.update(l,n,h))}function o(c,l,h){if(h===0)return;e.get("WEBGL_multi_draw").multiDrawArraysWEBGL(n,c,0,l,0,h);let u=0;for(let p=0;p<h;p++)u+=l[p];t.update(u,n,1)}this.setMode=s,this.render=r,this.renderInstances=a,this.renderMultiDraw=o}function gg(i,e,t,n){let s;function r(){if(s!==void 0)return s;if(e.has("EXT_texture_filter_anisotropic")===!0){let P=e.get("EXT_texture_filter_anisotropic");s=i.getParameter(P.MAX_TEXTURE_MAX_ANISOTROPY_EXT)}else s=0;return s}function a(P){return!(P!==cn&&n.convert(P)!==i.getParameter(i.IMPLEMENTATION_COLOR_READ_FORMAT))}function o(P){let x=P===Sn&&(e.has("EXT_color_buffer_half_float")||e.has("EXT_color_buffer_float"));return!(P!==$t&&P!==ln&&!x&&n.convert(P)!==i.getParameter(i.IMPLEMENTATION_COLOR_READ_TYPE))}function c(P){if(P==="highp"){if(i.getShaderPrecisionFormat(i.VERTEX_SHADER,i.HIGH_FLOAT).precision>0&&i.getShaderPrecisionFormat(i.FRAGMENT_SHADER,i.HIGH_FLOAT).precision>0)return"highp";P="mediump"}return P==="mediump"&&i.getShaderPrecisionFormat(i.VERTEX_SHADER,i.MEDIUM_FLOAT).precision>0&&i.getShaderPrecisionFormat(i.FRAGMENT_SHADER,i.MEDIUM_FLOAT).precision>0?"mediump":"lowp"}let l=t.precision!==void 0?t.precision:"highp",h=c(l);h!==l&&(ke("WebGLRenderer:",l,"not supported, using",h,"instead."),l=h);let d=t.logarithmicDepthBuffer===!0,u=t.reversedDepthBuffer===!0&&e.has("EXT_clip_control");t.reversedDepthBuffer===!0&&u===!1&&ke("WebGLRenderer: Unable to use reversed depth buffer due to missing EXT_clip_control extension. Fallback to default depth buffer.");let p=i.getParameter(i.MAX_TEXTURE_IMAGE_UNITS),g=i.getParameter(i.MAX_VERTEX_TEXTURE_IMAGE_UNITS),v=i.getParameter(i.MAX_TEXTURE_SIZE),m=i.getParameter(i.MAX_CUBE_MAP_TEXTURE_SIZE),f=i.getParameter(i.MAX_VERTEX_ATTRIBS),S=i.getParameter(i.MAX_VERTEX_UNIFORM_VECTORS),R=i.getParameter(i.MAX_VARYING_VECTORS),M=i.getParameter(i.MAX_FRAGMENT_UNIFORM_VECTORS),T=i.getParameter(i.MAX_SAMPLES),w=i.getParameter(i.SAMPLES);return{isWebGL2:!0,getMaxAnisotropy:r,getMaxPrecision:c,textureFormatReadable:a,textureTypeReadable:o,precision:l,logarithmicDepthBuffer:d,reversedDepthBuffer:u,maxTextures:p,maxVertexTextures:g,maxTextureSize:v,maxCubemapSize:m,maxAttributes:f,maxVertexUniforms:S,maxVaryings:R,maxFragmentUniforms:M,maxSamples:T,samples:w}}function _g(i){let e=this,t=null,n=0,s=!1,r=!1,a=new pn,o=new Je,c={value:null,needsUpdate:!1};this.uniform=c,this.numPlanes=0,this.numIntersection=0,this.init=function(d,u){let p=d.length!==0||u||n!==0||s;return s=u,n=d.length,p},this.beginShadows=function(){r=!0,h(null)},this.endShadows=function(){r=!1},this.setGlobalState=function(d,u){t=h(d,u,0)},this.setState=function(d,u,p){let g=d.clippingPlanes,v=d.clipIntersection,m=d.clipShadows,f=i.get(d);if(!s||g===null||g.length===0||r&&!m)r?h(null):l();else{let S=r?0:n,R=S*4,M=f.clippingState||null;c.value=M,M=h(g,u,R,p);for(let T=0;T!==R;++T)M[T]=t[T];f.clippingState=M,this.numIntersection=v?this.numPlanes:0,this.numPlanes+=S}};function l(){c.value!==t&&(c.value=t,c.needsUpdate=n>0),e.numPlanes=n,e.numIntersection=0}function h(d,u,p,g){let v=d!==null?d.length:0,m=null;if(v!==0){if(m=c.value,g!==!0||m===null){let f=p+v*4,S=u.matrixWorldInverse;o.getNormalMatrix(S),(m===null||m.length<f)&&(m=new Float32Array(f));for(let R=0,M=p;R!==v;++R,M+=4)a.copy(d[R]).applyMatrix4(S,o),a.normal.toArray(m,M),m[M+3]=a.constant}c.value=m,c.needsUpdate=!0}return e.numPlanes=v,e.numIntersection=0,m}}var Ds=4,xg=6,yg=20,vg=256,Wr=new As,wu=new Fe,Ec=null,Tc=0,wc=0,Ac=!1,Mg=new L,Ii=new L,Ns=class{constructor(e){this._renderer=e,this._pingPongRenderTarget=null,this._lodMax=0,this._cubeSize=0,this._sizeLods=[],this._lodMeshes=[],this._backgroundBox=null,this._cubemapMaterial=null,this._equirectMaterial=null,this._blurMaterial=null,this._ggxMaterial=null}fromScene(e,t=0,n=.1,s=100,r={}){let{size:a=256,position:o=Mg}=r;Ec=this._renderer.getRenderTarget(),Tc=this._renderer.getActiveCubeFace(),wc=this._renderer.getActiveMipmapLevel(),Ac=this._renderer.xr.enabled,this._renderer.xr.enabled=!1,this._setSize(a);let c=this._allocateTargets();return c.depthBuffer=!0,this._sceneToCubeUV(e,n,s,c,o),t>0&&this._blur(c,0,0,t),this._applyPMREM(c),this._cleanup(c),c}fromEquirectangular(e,t=null){return this._fromTexture(e,t)}fromCubemap(e,t=null){return this._fromTexture(e,t)}compileCubemapShader(){this._cubemapMaterial===null&&(this._cubemapMaterial=Cu(),this._compileMaterial(this._cubemapMaterial))}compileEquirectangularShader(){this._equirectMaterial===null&&(this._equirectMaterial=Ru(),this._compileMaterial(this._equirectMaterial))}dispose(){this._dispose(),this._cubemapMaterial!==null&&this._cubemapMaterial.dispose(),this._equirectMaterial!==null&&this._equirectMaterial.dispose(),this._backgroundBox!==null&&(this._backgroundBox.geometry.dispose(),this._backgroundBox.material.dispose())}_setSize(e){this._lodMax=Math.floor(Math.log2(e)),this._cubeSize=Math.pow(2,this._lodMax)}_dispose(){this._blurMaterial!==null&&this._blurMaterial.dispose(),this._ggxMaterial!==null&&this._ggxMaterial.dispose(),this._pingPongRenderTarget!==null&&this._pingPongRenderTarget.dispose();for(let e=0;e<this._lodMeshes.length;e++)this._lodMeshes[e].geometry.dispose()}_cleanup(e){this._renderer.setRenderTarget(Ec,Tc,wc),this._renderer.xr.enabled=Ac,e.scissorTest=!1,Ls(e,0,0,e.width,e.height)}_fromTexture(e,t){e.mapping===pi||e.mapping===Ri?this._setSize(e.image.length===0?16:e.image[0].width||e.image[0].image.width):this._setSize(e.image.width/4),Ec=this._renderer.getRenderTarget(),Tc=this._renderer.getActiveCubeFace(),wc=this._renderer.getActiveMipmapLevel(),Ac=this._renderer.xr.enabled,this._renderer.xr.enabled=!1;let n=t||this._allocateTargets();return this._textureToCubeUV(e,n),this._applyPMREM(n),this._cleanup(n),n}_allocateTargets(){let e=3*Math.max(this._cubeSize,112),t=4*this._cubeSize,n={magFilter:At,minFilter:At,generateMipmaps:!1,type:Sn,format:cn,colorSpace:tr,depthBuffer:!1},s=Au(e,t,n);if(this._pingPongRenderTarget===null||this._pingPongRenderTarget.width!==e||this._pingPongRenderTarget.height!==t){this._pingPongRenderTarget!==null&&this._dispose(),this._pingPongRenderTarget=Au(e,t,n);let{_lodMax:r}=this;({lodMeshes:this._lodMeshes,sizeLods:this._sizeLods}=Sg(r)),this._blurMaterial=Eg(r,e,t),this._ggxMaterial=bg(r,e,t)}return s}_compileMaterial(e){let t=new Ke(new Mt,e);this._renderer.compile(t,Wr)}_sceneToCubeUV(e,t,n,s,r){let c=new Ft(90,1,t,n),l=[1,-1,1,1,1,1],h=[1,1,1,-1,-1,-1],d=this._renderer,u=d.autoClear,p=d.toneMapping;d.getClearColor(wu),d.toneMapping=vn,d.autoClear=!1,d.state.buffers.depth.getReversed()&&(d.setRenderTarget(s),d.clearDepth(),d.setRenderTarget(null)),this._backgroundBox===null&&(this._backgroundBox=new Ke(new Dt,new Xt({name:"PMREM.Background",side:Ut,depthWrite:!1,depthTest:!1})));let v=this._backgroundBox,m=v.material,f=!1,S=e.background;S?S.isColor&&(m.color.copy(S),e.background=null,f=!0):(m.color.copy(wu),f=!0);for(let R=0;R<6;R++){let M=R%3;M===0?(c.up.set(0,l[R],0),c.position.set(r.x,r.y,r.z),c.lookAt(r.x+h[R],r.y,r.z)):M===1?(c.up.set(0,0,l[R]),c.position.set(r.x,r.y,r.z),c.lookAt(r.x,r.y+h[R],r.z)):(c.up.set(0,l[R],0),c.position.set(r.x,r.y,r.z),c.lookAt(r.x,r.y,r.z+h[R]));let T=this._cubeSize;Ls(s,M*T,R>2?T:0,T,T),d.setRenderTarget(s),f&&d.render(v,c),d.render(e,c)}d.toneMapping=p,d.autoClear=u,e.background=S}_textureToCubeUV(e,t){let n=this._renderer,s=e.mapping===pi||e.mapping===Ri;s?(this._cubemapMaterial===null&&(this._cubemapMaterial=Cu()),this._cubemapMaterial.uniforms.flipEnvMap.value=e.isRenderTargetTexture===!1?-1:1):this._equirectMaterial===null&&(this._equirectMaterial=Ru());let r=s?this._cubemapMaterial:this._equirectMaterial,a=this._lodMeshes[0];a.material=r;let o=r.uniforms;o.envMap.value=e;let c=this._cubeSize;Ls(t,0,0,3*c,2*c),n.setRenderTarget(t),n.render(a,Wr)}_applyPMREM(e){let t=this._renderer,n=t.autoClear;t.autoClear=!1;let s=this._lodMeshes.length;for(let r=1;r<s;r++)this._applyGGXFilter(e,r-1,r);t.autoClear=n}_applyGGXFilter(e,t,n){let s=this._renderer,r=this._pingPongRenderTarget,a=this._ggxMaterial,o=this._lodMeshes[n];o.material=a;let c=a.uniforms,l=n/(this._lodMeshes.length-1),h=t/(this._lodMeshes.length-1),d=Math.sqrt(l*l-h*h),u=l*1.25,p=d*u,{_lodMax:g}=this,v=this._sizeLods[n],m=3*v*(n>g-Ds?n-g+Ds:0),f=4*(this._cubeSize-v);c.envMap.value=e.texture,c.roughness.value=p,c.mipInt.value=g-t,Ls(r,m,f,3*v,2*v),s.setRenderTarget(r),s.render(o,Wr),c.envMap.value=r.texture,c.roughness.value=0,c.mipInt.value=g-n,Ls(e,m,f,3*v,2*v),s.setRenderTarget(e),s.render(o,Wr)}_blur(e,t,n,s){let r=this._pingPongRenderTarget,a=Math.min(s,Math.PI)/Math.SQRT2;this._blurPass(e,r,t,n,a),this._blurPass(r,e,n,n,a)}_blurPass(e,t,n,s,r){let a=this._renderer,o=this._blurMaterial,c=this._lodMeshes[s];c.material=o;let l=o.uniforms;l.envMap.value=e.texture,l.sigma.value=r,l.mipInt.value=this._lodMax-n;let h=this._sizeLods[s],d=3*h*(s>this._lodMax-Ds?s-this._lodMax+Ds:0),u=4*(this._cubeSize-h);Ls(t,d,u,3*h,2*h),a.setRenderTarget(t),a.render(c,Wr)}};function Sg(i){let e=[],t=[],n=i,s=i-Ds+1+xg;for(let r=0;r<s;r++){let a=Math.pow(2,n);e.push(a);let o=1/(a-2),c=-o,l=1+o,h=[c,c,l,c,l,l,c,c,l,l,c,l],d=6,u=6,p=3,g=new Float32Array(p*u*d),v=new Float32Array(p*u*d);for(let f=0;f<d;f++){let S=f%3*2/3-1,R=f>2?0:-1,M=[S,R,0,S+2/3,R,0,S+2/3,R+1,0,S,R,0,S+2/3,R+1,0,S,R+1,0];g.set(M,p*u*f);for(let T=0;T<u;T++){let w=h[T*2]*2-1,P=h[T*2+1]*2-1;f===0?Ii.set(1,P,w):f===1?Ii.set(-w,1,-P):f===2?Ii.set(-w,P,1):f===3?Ii.set(-1,P,-w):f===4?Ii.set(-w,-1,P):Ii.set(w,P,-1),Ii.toArray(v,(f*u+T)*p)}}let m=new Mt;m.setAttribute("position",new Ht(g,p)),m.setAttribute("outputDirection",new Ht(v,p)),t.push(new Ke(m,null)),n>Ds&&n--}return{lodMeshes:t,sizeLods:e}}function Au(i,e,t){let n=new Jt(i,e,t);return n.texture.mapping=Fr,n.texture.name="PMREM.cubeUv",n.scissorTest=!0,n}function Ls(i,e,t,n,s){i.viewport.set(e,t,n,s),i.scissor.set(e,t,n,s)}function bg(i,e,t){return new en({name:"PMREMGGXConvolution",defines:{GGX_SAMPLES:vg,CUBEUV_TEXEL_WIDTH:1/e,CUBEUV_TEXEL_HEIGHT:1/t,CUBEUV_MAX_MIP:`${i}.0`},uniforms:{envMap:{value:null},roughness:{value:0},mipInt:{value:0}},vertexShader:al(),fragmentShader:`

			precision highp float;
			precision highp int;

			varying vec3 vOutputDirection;

			uniform sampler2D envMap;
			uniform float roughness;
			uniform float mipInt;

			#define ENVMAP_TYPE_CUBE_UV
			#include <cube_uv_reflection_fragment>

			#define PI 3.14159265359

			// Van der Corput radical inverse
			float radicalInverse_VdC(uint bits) {
				bits = (bits << 16u) | (bits >> 16u);
				bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
				bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
				bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
				bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
				return float(bits) * 2.3283064365386963e-10; // / 0x100000000
			}

			// Hammersley sequence
			vec2 hammersley(uint i, uint N) {
				return vec2(float(i) / float(N), radicalInverse_VdC(i));
			}

			// GGX VNDF importance sampling (Eric Heitz 2018)
			// "Sampling the GGX Distribution of Visible Normals"
			// https://jcgt.org/published/0007/04/01/
			vec3 importanceSampleGGX_VNDF(vec2 Xi, vec3 V, float roughness) {
				float alpha = roughness * roughness;

				// Section 4.1: Orthonormal basis
				vec3 T1 = vec3(1.0, 0.0, 0.0);
				vec3 T2 = cross(V, T1);

				// Section 4.2: Parameterization of projected area
				float r = sqrt(Xi.x);
				float phi = 2.0 * PI * Xi.y;
				float t1 = r * cos(phi);
				float t2 = r * sin(phi);
				float s = 0.5 * (1.0 + V.z);
				t2 = (1.0 - s) * sqrt(1.0 - t1 * t1) + s * t2;

				// Section 4.3: Reprojection onto hemisphere
				vec3 Nh = t1 * T1 + t2 * T2 + sqrt(max(0.0, 1.0 - t1 * t1 - t2 * t2)) * V;

				// Section 3.4: Transform back to ellipsoid configuration
				return normalize(vec3(alpha * Nh.x, alpha * Nh.y, max(0.0, Nh.z)));
			}

			void main() {
				vec3 N = normalize(vOutputDirection);
				vec3 V = N; // Assume view direction equals normal for pre-filtering

				vec3 prefilteredColor = vec3(0.0);
				float totalWeight = 0.0;

				// For very low roughness, just sample the environment directly
				if (roughness < 0.001) {
					gl_FragColor = vec4(bilinearCubeUV(envMap, N, mipInt), 1.0);
					return;
				}

				// Tangent space basis for VNDF sampling
				vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
				vec3 tangent = normalize(cross(up, N));
				vec3 bitangent = cross(N, tangent);

				for(uint i = 0u; i < uint(GGX_SAMPLES); i++) {
					vec2 Xi = hammersley(i, uint(GGX_SAMPLES));

					// For PMREM, V = N, so in tangent space V is always (0, 0, 1)
					vec3 H_tangent = importanceSampleGGX_VNDF(Xi, vec3(0.0, 0.0, 1.0), roughness);

					// Transform H back to world space
					vec3 H = normalize(tangent * H_tangent.x + bitangent * H_tangent.y + N * H_tangent.z);
					vec3 L = normalize(2.0 * dot(V, H) * H - V);

					float NdotL = max(dot(N, L), 0.0);

					if(NdotL > 0.0) {
						// Sample environment at fixed mip level
						// VNDF importance sampling handles the distribution filtering
						vec3 sampleColor = bilinearCubeUV(envMap, L, mipInt);

						// Weight by NdotL for the split-sum approximation
						// VNDF PDF naturally accounts for the visible microfacet distribution
						prefilteredColor += sampleColor * NdotL;
						totalWeight += NdotL;
					}
				}

				if (totalWeight > 0.0) {
					prefilteredColor = prefilteredColor / totalWeight;
				}

				gl_FragColor = vec4(prefilteredColor, 1.0);
			}
		`,blending:Nn,depthTest:!1,depthWrite:!1})}function Eg(i,e,t){return new en({name:"SphericalGaussianBlur",defines:{SAMPLES:yg,CUBEUV_TEXEL_WIDTH:1/e,CUBEUV_TEXEL_HEIGHT:1/t,CUBEUV_MAX_MIP:`${i}.0`},uniforms:{envMap:{value:null},sigma:{value:0},mipInt:{value:0}},vertexShader:al(),fragmentShader:`

			precision highp float;
			precision highp int;

			varying vec3 vOutputDirection;

			uniform sampler2D envMap;
			uniform float sigma;
			uniform float mipInt;

			#define ENVMAP_TYPE_CUBE_UV
			#include <cube_uv_reflection_fragment>

			#define PI 3.14159265359
			#define GOLDEN_ANGLE 2.39996322973

			void main() {

				if ( sigma == 0.0 ) {

					gl_FragColor = vec4( bilinearCubeUV( envMap, vOutputDirection, mipInt ), 1.0 );
					return;

				}

				vec3 outputDirection = normalize( vOutputDirection );

				vec3 up = abs( outputDirection.z ) < 0.999 ? vec3( 0.0, 0.0, 1.0 ) : vec3( 1.0, 0.0, 0.0 );
				vec3 tangent = normalize( cross( up, outputDirection ) );
				vec3 bitangent = cross( outputDirection, tangent );

				// Truncate the kernel at three standard deviations or at the antipode.
				float thetaMax = min( 3.0 * sigma, PI );
				float truncation = 1.0 - exp( - 0.5 * thetaMax * thetaMax / ( sigma * sigma ) );

				vec3 accumColor = vec3( 0.0 );
				float accumWeight = 0.0;

				for ( int i = 0; i < SAMPLES; i ++ ) {

					// Stratified inverse-CDF sampling of the Gaussian, placed on a golden-angle spiral.
					float stratum = ( float( i ) + 0.5 ) / float( SAMPLES );
					float theta = sigma * sqrt( - 2.0 * log( 1.0 - stratum * truncation ) );
					float phi = float( i ) * GOLDEN_ANGLE;

					vec3 offset = cos( phi ) * tangent + sin( phi ) * bitangent;
					vec3 sampleDirection = cos( theta ) * outputDirection + sin( theta ) * offset;

					// Correct the planar sample density to solid angle.
					float weight = sin( theta ) / theta;

					accumColor += weight * bilinearCubeUV( envMap, sampleDirection, mipInt );
					accumWeight += weight;

				}

				gl_FragColor = vec4( accumColor / accumWeight, 1.0 );

			}
		`,blending:Nn,depthTest:!1,depthWrite:!1})}function Ru(){return new en({name:"EquirectangularToCubeUV",uniforms:{envMap:{value:null}},vertexShader:al(),fragmentShader:`

			precision mediump float;
			precision mediump int;

			varying vec3 vOutputDirection;

			uniform sampler2D envMap;

			#include <common>

			void main() {

				vec3 outputDirection = normalize( vOutputDirection );
				vec2 uv = equirectUv( outputDirection );

				gl_FragColor = vec4( texture2D ( envMap, uv ).rgb, 1.0 );

			}
		`,blending:Nn,depthTest:!1,depthWrite:!1})}function Cu(){return new en({name:"CubemapToCubeUV",uniforms:{envMap:{value:null},flipEnvMap:{value:-1}},vertexShader:al(),fragmentShader:`

			precision mediump float;
			precision mediump int;

			uniform float flipEnvMap;

			varying vec3 vOutputDirection;

			uniform samplerCube envMap;

			void main() {

				gl_FragColor = textureCube( envMap, vec3( flipEnvMap * vOutputDirection.x, vOutputDirection.yz ) );

			}
		`,blending:Nn,depthTest:!1,depthWrite:!1})}function al(){return`

		precision mediump float;
		precision mediump int;

		attribute vec3 outputDirection;

		varying vec3 vOutputDirection;

		void main() {

			vOutputDirection = outputDirection;
			gl_Position = vec4( position, 1.0 );

		}
	`}var sl=class extends Jt{constructor(e=1,t={}){super(e,e,t),this.isWebGLCubeRenderTarget=!0;let n={width:e,height:e,depth:1},s=[n,n,n,n,n,n];this.texture=new fr(s),this._setTextureOptions(t),this.texture.isRenderTargetTexture=!0}fromEquirectangularTexture(e,t){this.texture.type=t.type,this.texture.colorSpace=t.colorSpace,this.texture.generateMipmaps=t.generateMipmaps,this.texture.minFilter=t.minFilter,this.texture.magFilter=t.magFilter;let n={uniforms:{tEquirect:{value:null}},vertexShader:`

				varying vec3 vWorldDirection;

				vec3 transformDirection( in vec3 dir, in mat4 matrix ) {

					return normalize( ( matrix * vec4( dir, 0.0 ) ).xyz );

				}

				void main() {

					vWorldDirection = transformDirection( position, modelMatrix );

					#include <begin_vertex>
					#include <project_vertex>

				}
			`,fragmentShader:`

				uniform sampler2D tEquirect;

				varying vec3 vWorldDirection;

				#include <common>

				void main() {

					vec3 direction = normalize( vWorldDirection );

					vec2 sampleUV = equirectUv( direction );

					gl_FragColor = texture2D( tEquirect, sampleUV );

				}
			`},s=new Dt(5,5,5),r=new en({name:"CubemapFromEquirect",uniforms:Ci(n.uniforms),vertexShader:n.vertexShader,fragmentShader:n.fragmentShader,side:Ut,blending:Nn});r.uniforms.tEquirect.value=t;let a=new Ke(s,r),o=t.minFilter;return t.minFilter===On&&(t.minFilter=At),new ho(1,10,this).update(e,a),t.minFilter=o,a.geometry.dispose(),a.material.dispose(),this}clear(e,t=!0,n=!0,s=!0){let r=e.getRenderTarget();for(let a=0;a<6;a++)e.setRenderTarget(this,a),e.clear(t,n,s);e.setRenderTarget(r)}};function Tg(i){let e=new WeakMap,t=new WeakMap,n=null;function s(u,p=!1){return u==null?null:p?a(u):r(u)}function r(u){if(u&&u.isTexture){let p=u.mapping;if(p===fo||p===po)if(e.has(u)){let g=e.get(u).texture;return o(g,u.mapping)}else{let g=u.image;if(g&&g.height>0){let v=new sl(g.height);return v.fromEquirectangularTexture(i,u),e.set(u,v),u.addEventListener("dispose",l),o(v.texture,u.mapping)}else return null}}return u}function a(u){if(u&&u.isTexture){let p=u.mapping,g=p===fo||p===po,v=p===pi||p===Ri;if(g||v){let m=t.get(u),f=m!==void 0?m.texture.pmremVersion:0;if(u.isRenderTargetTexture&&u.pmremVersion!==f)return n===null&&(n=new Ns(i)),m=g?n.fromEquirectangular(u,m):n.fromCubemap(u,m),m.texture.pmremVersion=u.pmremVersion,t.set(u,m),m.texture;if(m!==void 0)return m.texture;{let S=u.image;return g&&S&&S.height>0||v&&S&&c(S)?(n===null&&(n=new Ns(i)),m=g?n.fromEquirectangular(u):n.fromCubemap(u),m.texture.pmremVersion=u.pmremVersion,t.set(u,m),u.addEventListener("dispose",h),m.texture):null}}}return u}function o(u,p){return p===fo?u.mapping=pi:p===po&&(u.mapping=Ri),u}function c(u){let p=0,g=6;for(let v=0;v<g;v++)u[v]!==void 0&&p++;return p===g}function l(u){let p=u.target;p.removeEventListener("dispose",l);let g=e.get(p);g!==void 0&&(e.delete(p),g.dispose())}function h(u){let p=u.target;p.removeEventListener("dispose",h);let g=t.get(p);g!==void 0&&(t.delete(p),g.dispose())}function d(){e=new WeakMap,t=new WeakMap,n!==null&&(n.dispose(),n=null)}return{get:s,dispose:d}}function wg(i){let e={};function t(n){if(e[n]!==void 0)return e[n];let s=i.getExtension(n);return e[n]=s,s}return{has:function(n){return t(n)!==null},init:function(){t("EXT_color_buffer_float"),t("WEBGL_clip_cull_distance"),t("OES_texture_float_linear"),t("EXT_color_buffer_half_float"),t("WEBGL_multisampled_render_to_texture"),t("WEBGL_render_shared_exponent")},get:function(n){let s=t(n);return s===null&&Si("WebGLRenderer: "+n+" extension not supported."),s}}}function Ag(i,e,t,n){let s={},r=new WeakMap;function a(d){let u=d.target;u.index!==null&&e.remove(u.index);for(let g in u.attributes)e.remove(u.attributes[g]);u.removeEventListener("dispose",a),delete s[u.id];let p=r.get(u);p&&(e.remove(p),r.delete(u)),n.releaseStatesOfGeometry(u),u.isInstancedBufferGeometry===!0&&delete u._maxInstanceCount,t.memory.geometries--}function o(d,u){return s[u.id]===!0||(u.addEventListener("dispose",a),s[u.id]=!0,t.memory.geometries++),u}function c(d){let u=d.attributes;for(let p in u)e.update(u[p],i.ARRAY_BUFFER)}function l(d){let u=[],p=d.index,g=d.attributes.position,v=0;if(g===void 0)return;if(p!==null){let S=p.array;v=p.version;for(let R=0,M=S.length;R<M;R+=3){let T=S[R+0],w=S[R+1],P=S[R+2];u.push(T,w,w,P,P,T)}}else{let S=g.array;v=g.version;for(let R=0,M=S.length/3-1;R<M;R+=3){let T=R+0,w=R+1,P=R+2;u.push(T,w,w,P,P,T)}}let m=new(g.count>=65535?ar:rr)(u,1);m.version=v;let f=r.get(d);f&&e.remove(f),r.set(d,m)}function h(d){let u=r.get(d);if(u){let p=d.index;p!==null&&u.version<p.version&&l(d)}else l(d);return r.get(d)}return{get:o,update:c,getWireframeAttribute:h}}function Rg(i,e,t){let n;function s(d){n=d}let r,a;function o(d){r=d.type,a=d.bytesPerElement}function c(d,u){i.drawElements(n,u,r,d*a),t.update(u,n,1)}function l(d,u,p){p!==0&&(i.drawElementsInstanced(n,u,r,d*a,p),t.update(u,n,p))}function h(d,u,p){if(p===0)return;e.get("WEBGL_multi_draw").multiDrawElementsWEBGL(n,u,0,r,d,0,p);let v=0;for(let m=0;m<p;m++)v+=u[m];t.update(v,n,1)}this.setMode=s,this.setIndex=o,this.render=c,this.renderInstances=l,this.renderMultiDraw=h}function Cg(i){let e={geometries:0,textures:0},t={frame:0,calls:0,triangles:0,points:0,lines:0};function n(r,a,o){switch(t.calls++,a){case i.TRIANGLES:t.triangles+=o*(r/3);break;case i.LINES:t.lines+=o*(r/2);break;case i.LINE_STRIP:t.lines+=o*(r-1);break;case i.LINE_LOOP:t.lines+=o*r;break;case i.POINTS:t.points+=o*r;break;default:We("WebGLInfo: Unknown draw mode:",a);break}}function s(){t.calls=0,t.triangles=0,t.points=0,t.lines=0}return{memory:e,render:t,programs:null,autoReset:!0,reset:s,update:n}}function Ig(i,e,t){let n=new WeakMap,s=new vt;function r(a,o,c){let l=a.morphTargetInfluences,h=o.morphAttributes.position||o.morphAttributes.normal||o.morphAttributes.color,d=h!==void 0?h.length:0,u=n.get(o);if(u===void 0||u.count!==d){let E=function(){P.dispose(),n.delete(o),o.removeEventListener("dispose",E)};u!==void 0&&u.texture.dispose();let p=o.morphAttributes.position!==void 0,g=o.morphAttributes.normal!==void 0,v=o.morphAttributes.color!==void 0,m=o.morphAttributes.position||[],f=o.morphAttributes.normal||[],S=o.morphAttributes.color||[],R=0;p===!0&&(R=1),g===!0&&(R=2),v===!0&&(R=3);let M=o.attributes.position.count*R,T=1;M>e.maxTextureSize&&(T=Math.ceil(M/e.maxTextureSize),M=e.maxTextureSize);let w=new Float32Array(M*T*4*d),P=new sr(w,M,T,d);P.type=ln,P.needsUpdate=!0;let x=R*4;for(let C=0;C<d;C++){let U=m[C],B=f[C],H=S[C],N=M*T*4*C;for(let V=0;V<U.count;V++){let X=V*x;p===!0&&(s.fromBufferAttribute(U,V),w[N+X+0]=s.x,w[N+X+1]=s.y,w[N+X+2]=s.z,w[N+X+3]=0),g===!0&&(s.fromBufferAttribute(B,V),w[N+X+4]=s.x,w[N+X+5]=s.y,w[N+X+6]=s.z,w[N+X+7]=0),v===!0&&(s.fromBufferAttribute(H,V),w[N+X+8]=s.x,w[N+X+9]=s.y,w[N+X+10]=s.z,w[N+X+11]=H.itemSize===4?s.w:1)}}u={count:d,texture:P,size:new he(M,T)},n.set(o,u),o.addEventListener("dispose",E)}if(a.isInstancedMesh===!0&&a.morphTexture!==null)c.getUniforms().setValue(i,"morphTexture",a.morphTexture,t);else{let p=0;for(let v=0;v<l.length;v++)p+=l[v];let g=o.morphTargetsRelative?1:1-p;c.getUniforms().setValue(i,"morphTargetBaseInfluence",g),c.getUniforms().setValue(i,"morphTargetInfluences",l)}c.getUniforms().setValue(i,"morphTargetsTexture",u.texture,t),c.getUniforms().setValue(i,"morphTargetsTextureSize",u.size)}return{update:r}}function Pg(i,e,t,n,s){let r=new WeakMap;function a(l){let h=s.render.frame,d=l.geometry,u=e.get(l,d);if(r.get(u)!==h&&(e.update(u),r.set(u,h)),l.isInstancedMesh&&(l.hasEventListener("dispose",c)===!1&&l.addEventListener("dispose",c),r.get(l)!==h&&(t.update(l.instanceMatrix,i.ARRAY_BUFFER),l.instanceColor!==null&&t.update(l.instanceColor,i.ARRAY_BUFFER),r.set(l,h))),l.isSkinnedMesh){let p=l.skeleton;r.get(p)!==h&&(p.update(),r.set(p,h))}return u}function o(){r=new WeakMap}function c(l){let h=l.target;h.removeEventListener("dispose",c),n.releaseStatesOfObject(h),t.remove(h.instanceMatrix),h.instanceColor!==null&&t.remove(h.instanceColor)}return{update:a,dispose:o}}var Lg={[ic]:"LINEAR_TONE_MAPPING",[sc]:"REINHARD_TONE_MAPPING",[rc]:"CINEON_TONE_MAPPING",[Nr]:"ACES_FILMIC_TONE_MAPPING",[oc]:"AGX_TONE_MAPPING",[lc]:"NEUTRAL_TONE_MAPPING",[ac]:"CUSTOM_TONE_MAPPING"};function Dg(i,e,t,n,s,r){let a=new Jt(e,t,{type:i,depthBuffer:s,stencilBuffer:r,samples:n?4:0,storeMultisampledDepthBuffer:!1,storeMultisampledStencilBuffer:!1,resolveDepthBuffer:!1,resolveStencilBuffer:!1}),o=null,c=null,l=new Mt;l.setAttribute("position",new it([-1,3,0,-1,-1,0,3,-1,0],3)),l.setAttribute("uv",new it([0,2,0,0,2,0],2));let h=new Ka({uniforms:{tDiffuse:{value:null}},vertexShader:`
			precision highp float;

			uniform mat4 modelViewMatrix;
			uniform mat4 projectionMatrix;

			attribute vec3 position;
			attribute vec2 uv;

			varying vec2 vUv;

			void main() {
				vUv = uv;
				gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );
			}`,fragmentShader:`
			precision highp float;

			uniform sampler2D tDiffuse;

			varying vec2 vUv;

			#include <tonemapping_pars_fragment>
			#include <colorspace_pars_fragment>

			void main() {
				gl_FragColor = texture2D( tDiffuse, vUv );

				#ifdef LINEAR_TONE_MAPPING
					gl_FragColor.rgb = LinearToneMapping( gl_FragColor.rgb );
				#elif defined( REINHARD_TONE_MAPPING )
					gl_FragColor.rgb = ReinhardToneMapping( gl_FragColor.rgb );
				#elif defined( CINEON_TONE_MAPPING )
					gl_FragColor.rgb = CineonToneMapping( gl_FragColor.rgb );
				#elif defined( ACES_FILMIC_TONE_MAPPING )
					gl_FragColor.rgb = ACESFilmicToneMapping( gl_FragColor.rgb );
				#elif defined( AGX_TONE_MAPPING )
					gl_FragColor.rgb = AgXToneMapping( gl_FragColor.rgb );
				#elif defined( NEUTRAL_TONE_MAPPING )
					gl_FragColor.rgb = NeutralToneMapping( gl_FragColor.rgb );
				#elif defined( CUSTOM_TONE_MAPPING )
					gl_FragColor.rgb = CustomToneMapping( gl_FragColor.rgb );
				#endif

				#ifdef SRGB_TRANSFER
					gl_FragColor = sRGBTransferOETF( gl_FragColor );
				#endif
			}`,depthTest:!1,depthWrite:!1}),d=new Ke(l,h),u=new As(-1,1,1,-1,0,1),p=null,g=null,v=!1,m,f=null,S=[],R=!1;this.setSize=function(M,T){a.setSize(M,T),o!==null&&o.setSize(M,T),c!==null&&c.setSize(M,T);for(let w=0;w<S.length;w++){let P=S[w];P.setSize&&P.setSize(M,T)}},this.setEffects=function(M){S=M,R=S.length>0&&S[0].isRenderPass===!0;let T=a.width,w=a.height;S.length>0&&o===null&&(o=new Jt(T,w,{type:Sn,depthBuffer:!1,stencilBuffer:!1}),c=new Jt(T,w,{type:Sn,depthBuffer:!1,stencilBuffer:!1}));for(let P=0;P<S.length;P++){let x=S[P];x.setSize&&x.setSize(T,w)}},this.begin=function(M,T){if(v||M.toneMapping===vn&&S.length===0)return!1;if(f=T,T!==null){let w=T.width,P=T.height;(a.width!==w||a.height!==P)&&this.setSize(w,P)}return R===!1&&M.setRenderTarget(a),m=M.toneMapping,M.toneMapping=vn,!0},this.hasRenderPass=function(){return R},this.end=function(M,T){M.toneMapping=m,v=!0;let w=a,P=o;for(let x=0;x<S.length;x++){let E=S[x];E.enabled!==!1&&(E.render(M,P,w,T),E.needsSwap!==!1&&(w=P,P=P===o?c:o))}if(p!==M.outputColorSpace||g!==M.toneMapping){p=M.outputColorSpace,g=M.toneMapping,h.defines={},st.getTransfer(p)===dt&&(h.defines.SRGB_TRANSFER="");let x=Lg[g];x&&(h.defines[x]=""),h.needsUpdate=!0}h.uniforms.tDiffuse.value=w.texture,M.setRenderTarget(f),M.render(d,u),f=null,v=!1},this.isCompositing=function(){return v},this.dispose=function(){a.dispose(),o!==null&&o.dispose(),c!==null&&c.dispose(),l.dispose(),h.dispose()}}var Ju=new Vt,Ic=new oi(1,1),$u=new sr,Ku=new Ha,Qu=new fr,Iu=[],Pu=[],Lu=new Float32Array(16),Du=new Float32Array(9),Uu=new Float32Array(4);function Fs(i,e,t){let n=i[0];if(n<=0||n>0)return i;let s=e*t,r=Iu[s];if(r===void 0&&(r=new Float32Array(s),Iu[s]=r),e!==0){n.toArray(r,0);for(let a=1,o=0;a!==e;++a)o+=t,i[a].toArray(r,o)}return r}function Rt(i,e){if(i.length!==e.length)return!1;for(let t=0,n=i.length;t<n;t++)if(i[t]!==e[t])return!1;return!0}function Ct(i,e){for(let t=0,n=e.length;t<n;t++)i[t]=e[t]}function ol(i,e){let t=Pu[e];t===void 0&&(t=new Int32Array(e),Pu[e]=t);for(let n=0;n!==e;++n)t[n]=i.allocateTextureUnit();return t}function Ug(i,e){let t=this.cache;t[0]!==e&&(i.uniform1f(this.addr,e),t[0]=e)}function Ng(i,e){let t=this.cache;if(e.x!==void 0)(t[0]!==e.x||t[1]!==e.y)&&(i.uniform2f(this.addr,e.x,e.y),t[0]=e.x,t[1]=e.y);else{if(Rt(t,e))return;i.uniform2fv(this.addr,e),Ct(t,e)}}function Fg(i,e){let t=this.cache;if(e.x!==void 0)(t[0]!==e.x||t[1]!==e.y||t[2]!==e.z)&&(i.uniform3f(this.addr,e.x,e.y,e.z),t[0]=e.x,t[1]=e.y,t[2]=e.z);else if(e.r!==void 0)(t[0]!==e.r||t[1]!==e.g||t[2]!==e.b)&&(i.uniform3f(this.addr,e.r,e.g,e.b),t[0]=e.r,t[1]=e.g,t[2]=e.b);else{if(Rt(t,e))return;i.uniform3fv(this.addr,e),Ct(t,e)}}function Og(i,e){let t=this.cache;if(e.x!==void 0)(t[0]!==e.x||t[1]!==e.y||t[2]!==e.z||t[3]!==e.w)&&(i.uniform4f(this.addr,e.x,e.y,e.z,e.w),t[0]=e.x,t[1]=e.y,t[2]=e.z,t[3]=e.w);else{if(Rt(t,e))return;i.uniform4fv(this.addr,e),Ct(t,e)}}function Bg(i,e){let t=this.cache,n=e.elements;if(n===void 0){if(Rt(t,e))return;i.uniformMatrix2fv(this.addr,!1,e),Ct(t,e)}else{if(Rt(t,n))return;Uu.set(n),i.uniformMatrix2fv(this.addr,!1,Uu),Ct(t,n)}}function zg(i,e){let t=this.cache,n=e.elements;if(n===void 0){if(Rt(t,e))return;i.uniformMatrix3fv(this.addr,!1,e),Ct(t,e)}else{if(Rt(t,n))return;Du.set(n),i.uniformMatrix3fv(this.addr,!1,Du),Ct(t,n)}}function Hg(i,e){let t=this.cache,n=e.elements;if(n===void 0){if(Rt(t,e))return;i.uniformMatrix4fv(this.addr,!1,e),Ct(t,e)}else{if(Rt(t,n))return;Lu.set(n),i.uniformMatrix4fv(this.addr,!1,Lu),Ct(t,n)}}function Vg(i,e){let t=this.cache;t[0]!==e&&(i.uniform1i(this.addr,e),t[0]=e)}function kg(i,e){let t=this.cache;if(e.x!==void 0)(t[0]!==e.x||t[1]!==e.y)&&(i.uniform2i(this.addr,e.x,e.y),t[0]=e.x,t[1]=e.y);else{if(Rt(t,e))return;i.uniform2iv(this.addr,e),Ct(t,e)}}function Gg(i,e){let t=this.cache;if(e.x!==void 0)(t[0]!==e.x||t[1]!==e.y||t[2]!==e.z)&&(i.uniform3i(this.addr,e.x,e.y,e.z),t[0]=e.x,t[1]=e.y,t[2]=e.z);else{if(Rt(t,e))return;i.uniform3iv(this.addr,e),Ct(t,e)}}function Wg(i,e){let t=this.cache;if(e.x!==void 0)(t[0]!==e.x||t[1]!==e.y||t[2]!==e.z||t[3]!==e.w)&&(i.uniform4i(this.addr,e.x,e.y,e.z,e.w),t[0]=e.x,t[1]=e.y,t[2]=e.z,t[3]=e.w);else{if(Rt(t,e))return;i.uniform4iv(this.addr,e),Ct(t,e)}}function Xg(i,e){let t=this.cache;t[0]!==e&&(i.uniform1ui(this.addr,e),t[0]=e)}function qg(i,e){let t=this.cache;if(e.x!==void 0)(t[0]!==e.x||t[1]!==e.y)&&(i.uniform2ui(this.addr,e.x,e.y),t[0]=e.x,t[1]=e.y);else{if(Rt(t,e))return;i.uniform2uiv(this.addr,e),Ct(t,e)}}function Yg(i,e){let t=this.cache;if(e.x!==void 0)(t[0]!==e.x||t[1]!==e.y||t[2]!==e.z)&&(i.uniform3ui(this.addr,e.x,e.y,e.z),t[0]=e.x,t[1]=e.y,t[2]=e.z);else{if(Rt(t,e))return;i.uniform3uiv(this.addr,e),Ct(t,e)}}function Zg(i,e){let t=this.cache;if(e.x!==void 0)(t[0]!==e.x||t[1]!==e.y||t[2]!==e.z||t[3]!==e.w)&&(i.uniform4ui(this.addr,e.x,e.y,e.z,e.w),t[0]=e.x,t[1]=e.y,t[2]=e.z,t[3]=e.w);else{if(Rt(t,e))return;i.uniform4uiv(this.addr,e),Ct(t,e)}}function Jg(i,e,t){let n=this.cache,s=t.allocateTextureUnit();n[0]!==s&&(i.uniform1i(this.addr,s),n[0]=s);let r;this.type===i.SAMPLER_2D_SHADOW?(Ic.compareFunction=t.isReversedDepthBuffer()?tl:el,r=Ic):r=Ju,t.setTexture2D(e||r,s)}function $g(i,e,t){let n=this.cache,s=t.allocateTextureUnit();n[0]!==s&&(i.uniform1i(this.addr,s),n[0]=s),t.setTexture3D(e||Ku,s)}function Kg(i,e,t){let n=this.cache,s=t.allocateTextureUnit();n[0]!==s&&(i.uniform1i(this.addr,s),n[0]=s),t.setTextureCube(e||Qu,s)}function Qg(i,e,t){let n=this.cache,s=t.allocateTextureUnit();n[0]!==s&&(i.uniform1i(this.addr,s),n[0]=s),t.setTexture2DArray(e||$u,s)}function jg(i){switch(i){case 5126:return Ug;case 35664:return Ng;case 35665:return Fg;case 35666:return Og;case 35674:return Bg;case 35675:return zg;case 35676:return Hg;case 5124:case 35670:return Vg;case 35667:case 35671:return kg;case 35668:case 35672:return Gg;case 35669:case 35673:return Wg;case 5125:return Xg;case 36294:return qg;case 36295:return Yg;case 36296:return Zg;case 35678:case 36198:case 36298:case 36306:case 35682:return Jg;case 35679:case 36299:case 36307:return $g;case 35680:case 36300:case 36308:case 36293:return Kg;case 36289:case 36303:case 36311:case 36292:return Qg}}function e0(i,e){i.uniform1fv(this.addr,e)}function t0(i,e){let t=Fs(e,this.size,2);i.uniform2fv(this.addr,t)}function n0(i,e){let t=Fs(e,this.size,3);i.uniform3fv(this.addr,t)}function i0(i,e){let t=Fs(e,this.size,4);i.uniform4fv(this.addr,t)}function s0(i,e){let t=Fs(e,this.size,4);i.uniformMatrix2fv(this.addr,!1,t)}function r0(i,e){let t=Fs(e,this.size,9);i.uniformMatrix3fv(this.addr,!1,t)}function a0(i,e){let t=Fs(e,this.size,16);i.uniformMatrix4fv(this.addr,!1,t)}function o0(i,e){i.uniform1iv(this.addr,e)}function l0(i,e){i.uniform2iv(this.addr,e)}function c0(i,e){i.uniform3iv(this.addr,e)}function h0(i,e){i.uniform4iv(this.addr,e)}function u0(i,e){i.uniform1uiv(this.addr,e)}function d0(i,e){i.uniform2uiv(this.addr,e)}function f0(i,e){i.uniform3uiv(this.addr,e)}function p0(i,e){i.uniform4uiv(this.addr,e)}function m0(i,e,t){let n=this.cache,s=e.length,r=ol(t,s);Rt(n,r)||(i.uniform1iv(this.addr,r),Ct(n,r));let a;this.type===i.SAMPLER_2D_SHADOW?a=Ic:a=Ju;for(let o=0;o!==s;++o)t.setTexture2D(e[o]||a,r[o])}function g0(i,e,t){let n=this.cache,s=e.length,r=ol(t,s);Rt(n,r)||(i.uniform1iv(this.addr,r),Ct(n,r));for(let a=0;a!==s;++a)t.setTexture3D(e[a]||Ku,r[a])}function _0(i,e,t){let n=this.cache,s=e.length,r=ol(t,s);Rt(n,r)||(i.uniform1iv(this.addr,r),Ct(n,r));for(let a=0;a!==s;++a)t.setTextureCube(e[a]||Qu,r[a])}function x0(i,e,t){let n=this.cache,s=e.length,r=ol(t,s);Rt(n,r)||(i.uniform1iv(this.addr,r),Ct(n,r));for(let a=0;a!==s;++a)t.setTexture2DArray(e[a]||$u,r[a])}function y0(i){switch(i){case 5126:return e0;case 35664:return t0;case 35665:return n0;case 35666:return i0;case 35674:return s0;case 35675:return r0;case 35676:return a0;case 5124:case 35670:return o0;case 35667:case 35671:return l0;case 35668:case 35672:return c0;case 35669:case 35673:return h0;case 5125:return u0;case 36294:return d0;case 36295:return f0;case 36296:return p0;case 35678:case 36198:case 36298:case 36306:case 35682:return m0;case 35679:case 36299:case 36307:return g0;case 35680:case 36300:case 36308:case 36293:return _0;case 36289:case 36303:case 36311:case 36292:return x0}}var Pc=class{constructor(e,t,n){this.id=e,this.addr=n,this.cache=[],this.type=t.type,this.setValue=jg(t.type)}},Lc=class{constructor(e,t,n){this.id=e,this.addr=n,this.cache=[],this.type=t.type,this.size=t.size,this.setValue=y0(t.type)}},Dc=class{constructor(e){this.id=e,this.seq=[],this.map={}}setValue(e,t,n){let s=this.seq;for(let r=0,a=s.length;r!==a;++r){let o=s[r];o.setValue(e,t[o.id],n)}}},Rc=/(\w+)(\])?(\[|\.)?/g;function Nu(i,e){i.seq.push(e),i.map[e.id]=e}function v0(i,e,t){let n=i.name,s=n.length;for(Rc.lastIndex=0;;){let r=Rc.exec(n),a=Rc.lastIndex,o=r[1],c=r[2]==="]",l=r[3];if(c&&(o=o|0),l===void 0||l==="["&&a+2===s){Nu(t,l===void 0?new Pc(o,i,e):new Lc(o,i,e));break}else{let d=t.map[o];d===void 0&&(d=new Dc(o),Nu(t,d)),t=d}}}var Us=class{constructor(e,t){this.seq=[],this.map={};let n=e.getProgramParameter(t,e.ACTIVE_UNIFORMS);for(let a=0;a<n;++a){let o=e.getActiveUniform(t,a),c=e.getUniformLocation(t,o.name);v0(o,c,this)}let s=[],r=[];for(let a of this.seq)a.type===e.SAMPLER_2D_SHADOW||a.type===e.SAMPLER_CUBE_SHADOW||a.type===e.SAMPLER_2D_ARRAY_SHADOW?s.push(a):r.push(a);s.length>0&&(this.seq=s.concat(r))}setValue(e,t,n,s){let r=this.map[t];r!==void 0&&r.setValue(e,n,s)}setOptional(e,t,n){let s=t[n];s!==void 0&&this.setValue(e,n,s)}static upload(e,t,n,s){for(let r=0,a=t.length;r!==a;++r){let o=t[r],c=n[o.id];c.needsUpdate!==!1&&o.setValue(e,c.value,s)}}static seqWithValue(e,t){let n=[];for(let s=0,r=e.length;s!==r;++s){let a=e[s];a.id in t&&n.push(a)}return n}};function Fu(i,e,t){let n=i.createShader(e);return i.shaderSource(n,t),i.compileShader(n),n}var M0=37297,S0=0;function b0(i,e){let t=i.split(`
`),n=[],s=Math.max(e-6,0),r=Math.min(e+6,t.length);for(let a=s;a<r;a++){let o=a+1;n.push(`${o===e?">":" "} ${o}: ${t[a]}`)}return n.join(`
`)}var Ou=new Je;function E0(i){st._getMatrix(Ou,st.workingColorSpace,i);let e=`mat3( ${Ou.elements.map(t=>t.toFixed(4))} )`;switch(st.getTransfer(i)){case nr:return[e,"LinearTransferOETF"];case dt:return[e,"sRGBTransferOETF"];default:return ke("WebGLProgram: Unsupported color space: ",i),[e,"LinearTransferOETF"]}}function Bu(i,e,t){let n=i.getShaderParameter(e,i.COMPILE_STATUS),r=(i.getShaderInfoLog(e)||"").trim();if(n&&r==="")return"";let a=/ERROR: 0:(\d+)/.exec(r);if(a){let o=parseInt(a[1]);return t.toUpperCase()+`

`+r+`

`+b0(i.getShaderSource(e),o)}else return r}function T0(i,e){let t=E0(e);return[`vec4 ${i}( vec4 value ) {`,`	return ${t[1]}( vec4( value.rgb * ${t[0]}, value.a ) );`,"}"].join(`
`)}var w0={[ic]:"Linear",[sc]:"Reinhard",[rc]:"Cineon",[Nr]:"ACESFilmic",[oc]:"AgX",[lc]:"Neutral",[ac]:"Custom"};function A0(i,e){let t=w0[e];return t===void 0?(ke("WebGLProgram: Unsupported toneMapping:",e),"vec3 "+i+"( vec3 color ) { return LinearToneMapping( color ); }"):"vec3 "+i+"( vec3 color ) { return "+t+"ToneMapping( color ); }"}var il=new L;function R0(){st.getLuminanceCoefficients(il);let i=il.x.toFixed(4),e=il.y.toFixed(4),t=il.z.toFixed(4);return["float luminance( const in vec3 rgb ) {",`	const vec3 weights = vec3( ${i}, ${e}, ${t} );`,"	return dot( weights, rgb );","}"].join(`
`)}function C0(i){return[i.extensionClipCullDistance?"#extension GL_ANGLE_clip_cull_distance : require":"",i.extensionMultiDraw?"#extension GL_ANGLE_multi_draw : require":""].filter(qr).join(`
`)}function I0(i){let e=[];for(let t in i){let n=i[t];n!==!1&&e.push("#define "+t+" "+n)}return e.join(`
`)}function P0(i,e){let t={},n=i.getProgramParameter(e,i.ACTIVE_ATTRIBUTES);for(let s=0;s<n;s++){let r=i.getActiveAttrib(e,s),a=r.name,o=1;r.type===i.FLOAT_MAT2&&(o=2),r.type===i.FLOAT_MAT3&&(o=3),r.type===i.FLOAT_MAT4&&(o=4),t[a]={type:r.type,location:i.getAttribLocation(e,a),locationSize:o}}return t}function qr(i){return i!==""}function zu(i,e){let t=e.numSpotLightShadows+e.numSpotLightMaps-e.numSpotLightShadowsWithMaps;return i.replace(/NUM_SUN_LIGHTS/g,e.numSunLights).replace(/NUM_DIR_LIGHTS/g,e.numDirLights).replace(/NUM_SPOT_LIGHTS/g,e.numSpotLights).replace(/NUM_SPOT_LIGHT_MAPS/g,e.numSpotLightMaps).replace(/NUM_SPOT_LIGHT_COORDS/g,t).replace(/NUM_RECT_AREA_LIGHTS/g,e.numRectAreaLights).replace(/NUM_POINT_LIGHTS/g,e.numPointLights).replace(/NUM_HEMI_LIGHTS/g,e.numHemiLights).replace(/NUM_SUN_LIGHT_SHADOWS/g,e.numSunLightShadows).replace(/NUM_DIR_LIGHT_SHADOWS/g,e.numDirLightShadows).replace(/NUM_SPOT_LIGHT_SHADOWS_WITH_MAPS/g,e.numSpotLightShadowsWithMaps).replace(/NUM_SPOT_LIGHT_SHADOWS/g,e.numSpotLightShadows).replace(/NUM_POINT_LIGHT_SHADOWS/g,e.numPointLightShadows)}function Hu(i,e){return i.replace(/NUM_CLIPPING_PLANES/g,e.numClippingPlanes).replace(/UNION_CLIPPING_PLANES/g,e.numClippingPlanes-e.numClipIntersection)}var L0=/^[ \t]*#include +<([\w\d./]+)>/gm;function Uc(i){return i.replace(L0,U0)}var D0=new Map;function U0(i,e){let t=Qe[e];if(t===void 0){let n=D0.get(e);if(n!==void 0)t=Qe[n],ke('WebGLRenderer: Shader chunk "%s" has been deprecated. Use "%s" instead.',e,n);else throw new Error("THREE.WebGLProgram: Can not resolve #include <"+e+">")}return Uc(t)}var N0=/#pragma unroll_loop_start\s+for\s*\(\s*int\s+i\s*=\s*(\d+)\s*;\s*i\s*<\s*(\d+)\s*;\s*i\s*\+\+\s*\)\s*{([\s\S]+?)}\s+#pragma unroll_loop_end/g;function Vu(i){return i.replace(N0,F0)}function F0(i,e,t,n){let s="";for(let r=parseInt(e);r<parseInt(t);r++)s+=n.replace(/\[\s*i\s*\]/g,"[ "+r+" ]").replace(/UNROLLED_LOOP_INDEX/g,r);return s}function ku(i){let e=`precision ${i.precision} float;
	precision ${i.precision} int;
	precision ${i.precision} sampler2D;
	precision ${i.precision} samplerCube;
	precision ${i.precision} sampler3D;
	precision ${i.precision} sampler2DArray;
	precision ${i.precision} sampler2DShadow;
	precision ${i.precision} samplerCubeShadow;
	precision ${i.precision} sampler2DArrayShadow;
	precision ${i.precision} isampler2D;
	precision ${i.precision} isampler3D;
	precision ${i.precision} isamplerCube;
	precision ${i.precision} isampler2DArray;
	precision ${i.precision} usampler2D;
	precision ${i.precision} usampler3D;
	precision ${i.precision} usamplerCube;
	precision ${i.precision} usampler2DArray;
	`;return i.precision==="highp"?e+=`
#define HIGH_PRECISION`:i.precision==="mediump"?e+=`
#define MEDIUM_PRECISION`:i.precision==="lowp"&&(e+=`
#define LOW_PRECISION`),e}var O0={[Ur]:"SHADOWMAP_TYPE_PCF",[Rs]:"SHADOWMAP_TYPE_VSM"};function B0(i){return O0[i.shadowMapType]||"SHADOWMAP_TYPE_BASIC"}var z0={[pi]:"ENVMAP_TYPE_CUBE",[Ri]:"ENVMAP_TYPE_CUBE",[Fr]:"ENVMAP_TYPE_CUBE_UV"};function H0(i){return i.envMap===!1?"ENVMAP_TYPE_CUBE":z0[i.envMapMode]||"ENVMAP_TYPE_CUBE"}var V0={[Ri]:"ENVMAP_MODE_REFRACTION"};function k0(i){return i.envMap===!1?"ENVMAP_MODE_REFLECTION":V0[i.envMapMode]||"ENVMAP_MODE_REFLECTION"}var G0={[nc]:"ENVMAP_BLENDING_MULTIPLY",[tu]:"ENVMAP_BLENDING_MIX",[nu]:"ENVMAP_BLENDING_ADD"};function W0(i){return i.envMap===!1?"ENVMAP_BLENDING_NONE":G0[i.combine]||"ENVMAP_BLENDING_NONE"}function X0(i){let e=i.envMapCubeUVHeight;if(e===null)return null;let t=Math.log2(e)-2,n=1/e;return{texelWidth:1/(3*Math.max(Math.pow(2,t),112)),texelHeight:n,maxMip:t}}function q0(i,e,t,n){let s=i.getContext(),r=t.defines,a=t.vertexShader,o=t.fragmentShader,c=B0(t),l=H0(t),h=k0(t),d=W0(t),u=X0(t),p=C0(t),g=I0(r),v=s.createProgram(),m,f,S=t.glslVersion?"#version "+t.glslVersion+`
`:"";t.isRawShaderMaterial?(m=["#define SHADER_TYPE "+t.shaderType,"#define SHADER_NAME "+t.shaderName,g].filter(qr).join(`
`),m.length>0&&(m+=`
`),f=["#define SHADER_TYPE "+t.shaderType,"#define SHADER_NAME "+t.shaderName,g].filter(qr).join(`
`),f.length>0&&(f+=`
`)):(m=[ku(t),"#define SHADER_TYPE "+t.shaderType,"#define SHADER_NAME "+t.shaderName,g,t.extensionClipCullDistance?"#define USE_CLIP_DISTANCE":"",t.batching?"#define USE_BATCHING":"",t.batchingColor?"#define USE_BATCHING_COLOR":"",t.instancing?"#define USE_INSTANCING":"",t.instancingColor?"#define USE_INSTANCING_COLOR":"",t.instancingMorph?"#define USE_INSTANCING_MORPH":"",t.useFog&&t.fog?"#define USE_FOG":"",t.useFog&&t.fogExp2?"#define FOG_EXP2":"",t.map?"#define USE_MAP":"",t.envMap?"#define USE_ENVMAP":"",t.envMap?"#define "+h:"",t.lightMap?"#define USE_LIGHTMAP":"",t.aoMap?"#define USE_AOMAP":"",t.bumpMap?"#define USE_BUMPMAP":"",t.normalMap?"#define USE_NORMALMAP":"",t.normalMapObjectSpace?"#define USE_NORMALMAP_OBJECTSPACE":"",t.normalMapTangentSpace?"#define USE_NORMALMAP_TANGENTSPACE":"",t.displacementMap?"#define USE_DISPLACEMENTMAP":"",t.emissiveMap?"#define USE_EMISSIVEMAP":"",t.anisotropy?"#define USE_ANISOTROPY":"",t.anisotropyMap?"#define USE_ANISOTROPYMAP":"",t.clearcoatMap?"#define USE_CLEARCOATMAP":"",t.clearcoatRoughnessMap?"#define USE_CLEARCOAT_ROUGHNESSMAP":"",t.clearcoatNormalMap?"#define USE_CLEARCOAT_NORMALMAP":"",t.iridescenceMap?"#define USE_IRIDESCENCEMAP":"",t.iridescenceThicknessMap?"#define USE_IRIDESCENCE_THICKNESSMAP":"",t.specularMap?"#define USE_SPECULARMAP":"",t.specularColorMap?"#define USE_SPECULAR_COLORMAP":"",t.specularIntensityMap?"#define USE_SPECULAR_INTENSITYMAP":"",t.roughnessMap?"#define USE_ROUGHNESSMAP":"",t.metalnessMap?"#define USE_METALNESSMAP":"",t.alphaMap?"#define USE_ALPHAMAP":"",t.alphaHash?"#define USE_ALPHAHASH":"",t.transmission?"#define USE_TRANSMISSION":"",t.transmissionMap?"#define USE_TRANSMISSIONMAP":"",t.thicknessMap?"#define USE_THICKNESSMAP":"",t.sheenColorMap?"#define USE_SHEEN_COLORMAP":"",t.sheenRoughnessMap?"#define USE_SHEEN_ROUGHNESSMAP":"",t.mapUv?"#define MAP_UV "+t.mapUv:"",t.alphaMapUv?"#define ALPHAMAP_UV "+t.alphaMapUv:"",t.lightMapUv?"#define LIGHTMAP_UV "+t.lightMapUv:"",t.aoMapUv?"#define AOMAP_UV "+t.aoMapUv:"",t.emissiveMapUv?"#define EMISSIVEMAP_UV "+t.emissiveMapUv:"",t.bumpMapUv?"#define BUMPMAP_UV "+t.bumpMapUv:"",t.normalMapUv?"#define NORMALMAP_UV "+t.normalMapUv:"",t.displacementMapUv?"#define DISPLACEMENTMAP_UV "+t.displacementMapUv:"",t.metalnessMapUv?"#define METALNESSMAP_UV "+t.metalnessMapUv:"",t.roughnessMapUv?"#define ROUGHNESSMAP_UV "+t.roughnessMapUv:"",t.anisotropyMapUv?"#define ANISOTROPYMAP_UV "+t.anisotropyMapUv:"",t.clearcoatMapUv?"#define CLEARCOATMAP_UV "+t.clearcoatMapUv:"",t.clearcoatNormalMapUv?"#define CLEARCOAT_NORMALMAP_UV "+t.clearcoatNormalMapUv:"",t.clearcoatRoughnessMapUv?"#define CLEARCOAT_ROUGHNESSMAP_UV "+t.clearcoatRoughnessMapUv:"",t.iridescenceMapUv?"#define IRIDESCENCEMAP_UV "+t.iridescenceMapUv:"",t.iridescenceThicknessMapUv?"#define IRIDESCENCE_THICKNESSMAP_UV "+t.iridescenceThicknessMapUv:"",t.sheenColorMapUv?"#define SHEEN_COLORMAP_UV "+t.sheenColorMapUv:"",t.sheenRoughnessMapUv?"#define SHEEN_ROUGHNESSMAP_UV "+t.sheenRoughnessMapUv:"",t.specularMapUv?"#define SPECULARMAP_UV "+t.specularMapUv:"",t.specularColorMapUv?"#define SPECULAR_COLORMAP_UV "+t.specularColorMapUv:"",t.specularIntensityMapUv?"#define SPECULAR_INTENSITYMAP_UV "+t.specularIntensityMapUv:"",t.transmissionMapUv?"#define TRANSMISSIONMAP_UV "+t.transmissionMapUv:"",t.thicknessMapUv?"#define THICKNESSMAP_UV "+t.thicknessMapUv:"",t.vertexTangents&&t.flatShading===!1?"#define USE_TANGENT":"",t.vertexNormals?"#define HAS_NORMAL":"",t.vertexColors?"#define USE_COLOR":"",t.vertexAlphas?"#define USE_COLOR_ALPHA":"",t.vertexUv1s?"#define USE_UV1":"",t.vertexUv2s?"#define USE_UV2":"",t.vertexUv3s?"#define USE_UV3":"",t.pointsUvs?"#define USE_POINTS_UV":"",t.flatShading?"#define FLAT_SHADED":"",t.skinning?"#define USE_SKINNING":"",t.morphTargets?"#define USE_MORPHTARGETS":"",t.morphNormals&&t.flatShading===!1?"#define USE_MORPHNORMALS":"",t.morphColors?"#define USE_MORPHCOLORS":"",t.morphTargetsCount>0?"#define MORPHTARGETS_TEXTURE_STRIDE "+t.morphTextureStride:"",t.morphTargetsCount>0?"#define MORPHTARGETS_COUNT "+t.morphTargetsCount:"",t.doubleSided?"#define DOUBLE_SIDED":"",t.flipSided?"#define FLIP_SIDED":"",t.shadowMapEnabled?"#define USE_SHADOWMAP":"",t.shadowMapEnabled?"#define "+c:"",t.sizeAttenuation?"#define USE_SIZEATTENUATION":"",t.numLightProbes>0?"#define USE_LIGHT_PROBES":"",t.logarithmicDepthBuffer?"#define USE_LOGARITHMIC_DEPTH_BUFFER":"",t.reversedDepthBuffer?"#define USE_REVERSED_DEPTH_BUFFER":"","uniform mat4 modelMatrix;","uniform mat4 modelViewMatrix;","uniform mat4 projectionMatrix;","uniform mat4 viewMatrix;","uniform mat3 normalMatrix;","uniform vec3 cameraPosition;","uniform bool isOrthographic;","#ifdef USE_INSTANCING","	attribute mat4 instanceMatrix;","#endif","#ifdef USE_INSTANCING_COLOR","	attribute vec3 instanceColor;","#endif","#ifdef USE_INSTANCING_MORPH","	uniform sampler2D morphTexture;","#endif","attribute vec3 position;","attribute vec3 normal;","attribute vec2 uv;","#ifdef USE_UV1","	attribute vec2 uv1;","#endif","#ifdef USE_UV2","	attribute vec2 uv2;","#endif","#ifdef USE_UV3","	attribute vec2 uv3;","#endif","#ifdef USE_TANGENT","	attribute vec4 tangent;","#endif","#if defined( USE_COLOR_ALPHA )","	attribute vec4 color;","#elif defined( USE_COLOR )","	attribute vec3 color;","#endif","#ifdef USE_SKINNING","	attribute vec4 skinIndex;","	attribute vec4 skinWeight;","#endif",`
`].filter(qr).join(`
`),f=[ku(t),"#define SHADER_TYPE "+t.shaderType,"#define SHADER_NAME "+t.shaderName,g,t.useFog&&t.fog?"#define USE_FOG":"",t.useFog&&t.fogExp2?"#define FOG_EXP2":"",t.alphaToCoverage?"#define ALPHA_TO_COVERAGE":"",t.map?"#define USE_MAP":"",t.matcap?"#define USE_MATCAP":"",t.envMap?"#define USE_ENVMAP":"",t.envMap?"#define "+l:"",t.envMap?"#define "+h:"",t.envMap?"#define "+d:"",u?"#define CUBEUV_TEXEL_WIDTH "+u.texelWidth:"",u?"#define CUBEUV_TEXEL_HEIGHT "+u.texelHeight:"",u?"#define CUBEUV_MAX_MIP "+u.maxMip+".0":"",t.lightMap?"#define USE_LIGHTMAP":"",t.aoMap?"#define USE_AOMAP":"",t.bumpMap?"#define USE_BUMPMAP":"",t.normalMap?"#define USE_NORMALMAP":"",t.normalMapObjectSpace?"#define USE_NORMALMAP_OBJECTSPACE":"",t.normalMapTangentSpace?"#define USE_NORMALMAP_TANGENTSPACE":"",t.packedNormalMap?"#define USE_PACKED_NORMALMAP":"",t.emissiveMap?"#define USE_EMISSIVEMAP":"",t.anisotropy?"#define USE_ANISOTROPY":"",t.anisotropyMap?"#define USE_ANISOTROPYMAP":"",t.clearcoat?"#define USE_CLEARCOAT":"",t.clearcoatMap?"#define USE_CLEARCOATMAP":"",t.clearcoatRoughnessMap?"#define USE_CLEARCOAT_ROUGHNESSMAP":"",t.clearcoatNormalMap?"#define USE_CLEARCOAT_NORMALMAP":"",t.dispersion?"#define USE_DISPERSION":"",t.retroreflection?"#define USE_RETROREFLECTION":"",t.iridescence?"#define USE_IRIDESCENCE":"",t.iridescenceMap?"#define USE_IRIDESCENCEMAP":"",t.iridescenceThicknessMap?"#define USE_IRIDESCENCE_THICKNESSMAP":"",t.specularMap?"#define USE_SPECULARMAP":"",t.specularColorMap?"#define USE_SPECULAR_COLORMAP":"",t.specularIntensityMap?"#define USE_SPECULAR_INTENSITYMAP":"",t.roughnessMap?"#define USE_ROUGHNESSMAP":"",t.metalnessMap?"#define USE_METALNESSMAP":"",t.alphaMap?"#define USE_ALPHAMAP":"",t.alphaTest?"#define USE_ALPHATEST":"",t.alphaHash?"#define USE_ALPHAHASH":"",t.sheen?"#define USE_SHEEN":"",t.sheenColorMap?"#define USE_SHEEN_COLORMAP":"",t.sheenRoughnessMap?"#define USE_SHEEN_ROUGHNESSMAP":"",t.transmission?"#define USE_TRANSMISSION":"",t.transmissionMap?"#define USE_TRANSMISSIONMAP":"",t.thicknessMap?"#define USE_THICKNESSMAP":"",t.vertexTangents&&t.flatShading===!1?"#define USE_TANGENT":"",t.vertexColors||t.instancingColor?"#define USE_COLOR":"",t.vertexAlphas||t.batchingColor?"#define USE_COLOR_ALPHA":"",t.vertexUv1s?"#define USE_UV1":"",t.vertexUv2s?"#define USE_UV2":"",t.vertexUv3s?"#define USE_UV3":"",t.pointsUvs?"#define USE_POINTS_UV":"",t.gradientMap?"#define USE_GRADIENTMAP":"",t.flatShading?"#define FLAT_SHADED":"",t.doubleSided?"#define DOUBLE_SIDED":"",t.flipSided?"#define FLIP_SIDED":"",t.shadowMapEnabled?"#define USE_SHADOWMAP":"",t.shadowMapEnabled?"#define "+c:"",t.premultipliedAlpha?"#define PREMULTIPLIED_ALPHA":"",t.numLightProbes>0?"#define USE_LIGHT_PROBES":"",t.numLightProbeGrids>0?"#define USE_LIGHT_PROBES_GRID":"",t.decodeVideoTexture?"#define DECODE_VIDEO_TEXTURE":"",t.decodeVideoTextureEmissive?"#define DECODE_VIDEO_TEXTURE_EMISSIVE":"",t.logarithmicDepthBuffer?"#define USE_LOGARITHMIC_DEPTH_BUFFER":"",t.reversedDepthBuffer?"#define USE_REVERSED_DEPTH_BUFFER":"","uniform mat4 viewMatrix;","uniform vec3 cameraPosition;","uniform bool isOrthographic;",t.toneMapping!==vn?"#define TONE_MAPPING":"",t.toneMapping!==vn?Qe.tonemapping_pars_fragment:"",t.toneMapping!==vn?A0("toneMapping",t.toneMapping):"",t.dithering?"#define DITHERING":"",t.opaque?"#define OPAQUE":"",Qe.colorspace_pars_fragment,T0("linearToOutputTexel",t.outputColorSpace),R0(),t.useDepthPacking?"#define DEPTH_PACKING "+t.depthPacking:"",`
`].filter(qr).join(`
`)),a=Uc(a),a=zu(a,t),a=Hu(a,t),o=Uc(o),o=zu(o,t),o=Hu(o,t),a=Vu(a),o=Vu(o),t.isRawShaderMaterial!==!0&&(S=`#version 300 es
`,m=[p,"#define attribute in","#define varying out","#define texture2D texture"].join(`
`)+`
`+m,f=["#define varying in",t.glslVersion===_c?"":"layout(location = 0) out highp vec4 pc_fragColor;",t.glslVersion===_c?"":"#define gl_FragColor pc_fragColor","#define gl_FragDepthEXT gl_FragDepth","#define texture2D texture","#define textureCube texture","#define texture2DProj textureProj","#define texture2DLodEXT textureLod","#define texture2DProjLodEXT textureProjLod","#define textureCubeLodEXT textureLod","#define texture2DGradEXT textureGrad","#define texture2DProjGradEXT textureProjGrad","#define textureCubeGradEXT textureGrad"].join(`
`)+`
`+f);let R=S+m+a,M=S+f+o,T=Fu(s,s.VERTEX_SHADER,R),w=Fu(s,s.FRAGMENT_SHADER,M);s.attachShader(v,T),s.attachShader(v,w),t.index0AttributeName!==void 0?s.bindAttribLocation(v,0,t.index0AttributeName):t.hasPositionAttribute===!0&&s.bindAttribLocation(v,0,"position"),s.linkProgram(v);function P(U){if(i.debug.checkShaderErrors){let B=s.getProgramInfoLog(v)||"",H=s.getShaderInfoLog(T)||"",N=s.getShaderInfoLog(w)||"",V=B.trim(),X=H.trim(),W=N.trim(),re=!0,Y=!0;if(s.getProgramParameter(v,s.LINK_STATUS)===!1)if(re=!1,typeof i.debug.onShaderError=="function")i.debug.onShaderError(s,v,T,w);else{let Q=Bu(s,T,"vertex"),j=Bu(s,w,"fragment");We("WebGLProgram: Shader Error "+s.getError()+" - VALIDATE_STATUS "+s.getProgramParameter(v,s.VALIDATE_STATUS)+`

Material Name: `+U.name+`
Material Type: `+U.type+`

Program Info Log: `+V+`
`+Q+`
`+j)}else V!==""?ke("WebGLProgram: Program Info Log:",V):(X===""||W==="")&&(Y=!1);Y&&(U.diagnostics={runnable:re,programLog:V,vertexShader:{log:X,prefix:m},fragmentShader:{log:W,prefix:f}})}s.deleteShader(T),s.deleteShader(w),x=new Us(s,v),E=P0(s,v)}let x;this.getUniforms=function(){return x===void 0&&P(this),x};let E;this.getAttributes=function(){return E===void 0&&P(this),E};let C=t.rendererExtensionParallelShaderCompile===!1;return this.isReady=function(){return C===!1&&(C=s.getProgramParameter(v,M0)),C},this.destroy=function(){n.releaseStatesOfProgram(this),s.deleteProgram(v),this.program=void 0},this.type=t.shaderType,this.name=t.shaderName,this.id=S0++,this.cacheKey=e,this.usedTimes=1,this.program=v,this.vertexShader=T,this.fragmentShader=w,this}var Y0=0,Nc=class{constructor(){this.shaderCache=new Map,this.materialCache=new Map}update(e,t,n){let s=this._getShaderCacheForMaterial(e);return s.has(t)===!1&&(s.add(t),t.usedTimes++),s.has(n)===!1&&(s.add(n),n.usedTimes++),this}remove(e){let t=this.materialCache.get(e);for(let n of t)n.usedTimes--,n.usedTimes===0&&this.shaderCache.delete(n.code);return this.materialCache.delete(e),this}getVertexShaderStage(e){return this._getShaderStage(e.vertexShader)}getFragmentShaderStage(e){return this._getShaderStage(e.fragmentShader)}dispose(){this.shaderCache.clear(),this.materialCache.clear()}_getShaderCacheForMaterial(e){let t=this.materialCache,n=t.get(e);return n===void 0&&(n=new Set,t.set(e,n)),n}_getShaderStage(e){let t=this.shaderCache,n=t.get(e);return n===void 0&&(n=new Fc(e),t.set(e,n)),n}},Fc=class{constructor(e){this.id=Y0++,this.code=e,this.usedTimes=0}};function Z0(i){return i===gi||i===kr||i===Gr}function J0(i,e,t,n,s,r){let a=new ps,o=new Nc,c=new Set,l=[],h=new Map,d=n.logarithmicDepthBuffer,u=n.precision,p={MeshDepthMaterial:"depth",MeshDistanceMaterial:"distance",MeshNormalMaterial:"normal",MeshBasicMaterial:"basic",MeshLambertMaterial:"lambert",MeshPhongMaterial:"phong",MeshToonMaterial:"toon",MeshStandardMaterial:"physical",MeshPhysicalMaterial:"physical",MeshMatcapMaterial:"matcap",LineBasicMaterial:"basic",LineDashedMaterial:"dashed",PointsMaterial:"points",ShadowMaterial:"shadow",SpriteMaterial:"sprite"};function g(x){return c.add(x),x===0?"uv":`uv${x}`}function v(x,E,C,U,B,H){let N=U.fog,V=B.geometry,X=x.isMeshStandardMaterial||x.isMeshLambertMaterial||x.isMeshPhongMaterial?U.environment:null,W=x.isMeshStandardMaterial||x.isMeshLambertMaterial&&!x.envMap||x.isMeshPhongMaterial&&!x.envMap,re=e.get(x.envMap||X,W),Y=re&&re.mapping===Fr?re.image.height:null,Q=p[x.type];x.precision!==null&&(u=n.getMaxPrecision(x.precision),u!==x.precision&&ke("WebGLProgram.getParameters:",x.precision,"not supported, using",u,"instead."));let j=V.morphAttributes.position||V.morphAttributes.normal||V.morphAttributes.color,Re=j!==void 0?j.length:0,Ce=0;V.morphAttributes.position!==void 0&&(Ce=1),V.morphAttributes.normal!==void 0&&(Ce=2),V.morphAttributes.color!==void 0&&(Ce=3);let lt,je,ot,q;if(Q){let ft=zn[Q];lt=ft.vertexShader,je=ft.fragmentShader}else{lt=x.vertexShader,je=x.fragmentShader;let ft=o.getVertexShaderStage(x),Xe=o.getFragmentShaderStage(x);o.update(x,ft,Xe),ot=ft.id,q=Xe.id}let ne=i.getRenderTarget(),ve=i.state.buffers.depth.getReversed(),Oe=B.isInstancedMesh===!0,we=B.isBatchedMesh===!0,He=!!x.map,rt=!!x.matcap,te=!!re,ae=!!x.aoMap,le=!!x.lightMap,oe=!!x.bumpMap&&x.wireframe===!1,ue=!!x.normalMap,Ue=!!x.displacementMap,Ne=!!x.emissiveMap,Ge=!!x.metalnessMap,qe=!!x.roughnessMap,D=x.anisotropy>0,ct=x.clearcoat>0,et=x.dispersion>0,A=x.retroreflectivity>0,_=x.iridescence>0,z=x.sheen>0,k=x.transmission>0,Z=D&&!!x.anisotropyMap,fe=ct&&!!x.clearcoatMap,_e=ct&&!!x.clearcoatNormalMap,J=ct&&!!x.clearcoatRoughnessMap,ee=_&&!!x.iridescenceMap,pe=_&&!!x.iridescenceThicknessMap,Le=z&&!!x.sheenColorMap,xe=z&&!!x.sheenRoughnessMap,ge=!!x.specularMap,Pe=!!x.specularColorMap,ze=!!x.specularIntensityMap,Ze=k&&!!x.transmissionMap,O=k&&!!x.thicknessMap,Me=!!x.gradientMap,K=!!x.alphaMap,ce=x.alphaTest>0,Se=!!x.alphaHash,se=!!x.extensions,De=vn;x.toneMapped&&(ne===null||ne.isXRRenderTarget===!0)&&(De=i.toneMapping);let Ie={shaderID:Q,shaderType:x.type,shaderName:x.name,vertexShader:lt,fragmentShader:je,defines:x.defines,customVertexShaderID:ot,customFragmentShaderID:q,isRawShaderMaterial:x.isRawShaderMaterial===!0,glslVersion:x.glslVersion,precision:u,batching:we,batchingColor:we&&B._colorsTexture!==null,instancing:Oe,instancingColor:Oe&&B.instanceColor!==null,instancingMorph:Oe&&B.morphTexture!==null,outputColorSpace:ne===null?i.outputColorSpace:ne.isXRRenderTarget===!0?ne.texture.colorSpace:st.workingColorSpace,alphaToCoverage:!!x.alphaToCoverage,map:He,matcap:rt,envMap:te,envMapMode:te&&re.mapping,envMapCubeUVHeight:Y,aoMap:ae,lightMap:le,bumpMap:oe,normalMap:ue,displacementMap:Ue,emissiveMap:Ne,normalMapObjectSpace:ue&&x.normalMapType===ru,normalMapTangentSpace:ue&&x.normalMapType===jo,packedNormalMap:ue&&x.normalMapType===jo&&Z0(x.normalMap.format),metalnessMap:Ge,roughnessMap:qe,anisotropy:D,anisotropyMap:Z,clearcoat:ct,clearcoatMap:fe,clearcoatNormalMap:_e,clearcoatRoughnessMap:J,dispersion:et,retroreflection:A,iridescence:_,iridescenceMap:ee,iridescenceThicknessMap:pe,sheen:z,sheenColorMap:Le,sheenRoughnessMap:xe,specularMap:ge,specularColorMap:Pe,specularIntensityMap:ze,transmission:k,transmissionMap:Ze,thicknessMap:O,gradientMap:Me,opaque:x.transparent===!1&&x.blending===Cs&&x.alphaToCoverage===!1,alphaMap:K,alphaTest:ce,alphaHash:Se,combine:x.combine,mapUv:He&&g(x.map.channel),aoMapUv:ae&&g(x.aoMap.channel),lightMapUv:le&&g(x.lightMap.channel),bumpMapUv:oe&&g(x.bumpMap.channel),normalMapUv:ue&&g(x.normalMap.channel),displacementMapUv:Ue&&g(x.displacementMap.channel),emissiveMapUv:Ne&&g(x.emissiveMap.channel),metalnessMapUv:Ge&&g(x.metalnessMap.channel),roughnessMapUv:qe&&g(x.roughnessMap.channel),anisotropyMapUv:Z&&g(x.anisotropyMap.channel),clearcoatMapUv:fe&&g(x.clearcoatMap.channel),clearcoatNormalMapUv:_e&&g(x.clearcoatNormalMap.channel),clearcoatRoughnessMapUv:J&&g(x.clearcoatRoughnessMap.channel),iridescenceMapUv:ee&&g(x.iridescenceMap.channel),iridescenceThicknessMapUv:pe&&g(x.iridescenceThicknessMap.channel),sheenColorMapUv:Le&&g(x.sheenColorMap.channel),sheenRoughnessMapUv:xe&&g(x.sheenRoughnessMap.channel),specularMapUv:ge&&g(x.specularMap.channel),specularColorMapUv:Pe&&g(x.specularColorMap.channel),specularIntensityMapUv:ze&&g(x.specularIntensityMap.channel),transmissionMapUv:Ze&&g(x.transmissionMap.channel),thicknessMapUv:O&&g(x.thicknessMap.channel),alphaMapUv:K&&g(x.alphaMap.channel),vertexTangents:!!V.attributes.tangent&&(ue||D),vertexNormals:!!V.attributes.normal,vertexColors:x.vertexColors,vertexAlphas:x.vertexColors===!0&&!!V.attributes.color&&V.attributes.color.itemSize===4,pointsUvs:B.isPoints===!0&&!!V.attributes.uv&&(He||K),fog:!!N,useFog:x.fog===!0,fogExp2:!!N&&N.isFogExp2,flatShading:x.wireframe===!1&&(x.flatShading===!0||V.attributes.normal===void 0&&ue===!1&&(x.isMeshLambertMaterial||x.isMeshPhongMaterial||x.isMeshStandardMaterial||x.isMeshPhysicalMaterial)),sizeAttenuation:x.sizeAttenuation===!0,logarithmicDepthBuffer:d,reversedDepthBuffer:ve,skinning:B.isSkinnedMesh===!0,hasPositionAttribute:V.attributes.position!==void 0,morphTargets:V.morphAttributes.position!==void 0,morphNormals:V.morphAttributes.normal!==void 0,morphColors:V.morphAttributes.color!==void 0,morphTargetsCount:Re,morphTextureStride:Ce,numSunLights:E.sun.length,numDirLights:E.directional.length,numPointLights:E.point.length,numSpotLights:E.spot.length,numSpotLightMaps:E.spotLightMap.length,numRectAreaLights:E.rectArea.length,numHemiLights:E.hemi.length,numSunLightShadows:E.sunShadowMap.length,numDirLightShadows:E.directionalShadowMap.length,numPointLightShadows:E.pointShadowMap.length,numSpotLightShadows:E.spotShadowMap.length,numSpotLightShadowsWithMaps:E.numSpotLightShadowsWithMaps,numLightProbes:E.numLightProbes,numLightProbeGrids:H.length,numClippingPlanes:r.numPlanes,numClipIntersection:r.numIntersection,dithering:x.dithering,shadowMapEnabled:i.shadowMap.enabled&&C.length>0,shadowMapType:i.shadowMap.type,toneMapping:De,decodeVideoTexture:He&&x.map.isVideoTexture===!0&&st.getTransfer(x.map.colorSpace)===dt,decodeVideoTextureEmissive:Ne&&x.emissiveMap.isVideoTexture===!0&&st.getTransfer(x.emissiveMap.colorSpace)===dt,premultipliedAlpha:x.premultipliedAlpha,doubleSided:x.side===nn,flipSided:x.side===Ut,useDepthPacking:x.depthPacking>=0,depthPacking:x.depthPacking||0,index0AttributeName:x.index0AttributeName,extensionClipCullDistance:se&&x.extensions.clipCullDistance===!0&&t.has("WEBGL_clip_cull_distance"),extensionMultiDraw:(se&&x.extensions.multiDraw===!0||we)&&t.has("WEBGL_multi_draw"),rendererExtensionParallelShaderCompile:t.has("KHR_parallel_shader_compile"),customProgramCacheKey:x.customProgramCacheKey()};return Ie.vertexUv1s=c.has(1),Ie.vertexUv2s=c.has(2),Ie.vertexUv3s=c.has(3),c.clear(),Ie}function m(x){let E=[];if(x.shaderID?E.push(x.shaderID):(E.push(x.customVertexShaderID),E.push(x.customFragmentShaderID)),x.defines!==void 0)for(let C in x.defines)E.push(C),E.push(x.defines[C]);return x.isRawShaderMaterial===!1&&(f(E,x),S(E,x),E.push(i.outputColorSpace)),E.push(x.customProgramCacheKey),E.join()}function f(x,E){x.push(E.precision),x.push(E.outputColorSpace),x.push(E.envMapMode),x.push(E.envMapCubeUVHeight),x.push(E.mapUv),x.push(E.alphaMapUv),x.push(E.lightMapUv),x.push(E.aoMapUv),x.push(E.bumpMapUv),x.push(E.normalMapUv),x.push(E.displacementMapUv),x.push(E.emissiveMapUv),x.push(E.metalnessMapUv),x.push(E.roughnessMapUv),x.push(E.anisotropyMapUv),x.push(E.clearcoatMapUv),x.push(E.clearcoatNormalMapUv),x.push(E.clearcoatRoughnessMapUv),x.push(E.iridescenceMapUv),x.push(E.iridescenceThicknessMapUv),x.push(E.sheenColorMapUv),x.push(E.sheenRoughnessMapUv),x.push(E.specularMapUv),x.push(E.specularColorMapUv),x.push(E.specularIntensityMapUv),x.push(E.transmissionMapUv),x.push(E.thicknessMapUv),x.push(E.combine),x.push(E.fogExp2),x.push(E.sizeAttenuation),x.push(E.morphTargetsCount),x.push(E.morphAttributeCount),x.push(E.numSunLights),x.push(E.numDirLights),x.push(E.numPointLights),x.push(E.numSpotLights),x.push(E.numSpotLightMaps),x.push(E.numHemiLights),x.push(E.numRectAreaLights),x.push(E.numSunLightShadows),x.push(E.numDirLightShadows),x.push(E.numPointLightShadows),x.push(E.numSpotLightShadows),x.push(E.numSpotLightShadowsWithMaps),x.push(E.numLightProbes),x.push(E.shadowMapType),x.push(E.toneMapping),x.push(E.numClippingPlanes),x.push(E.numClipIntersection),x.push(E.depthPacking)}function S(x,E){a.disableAll(),E.instancing&&a.enable(0),E.instancingColor&&a.enable(1),E.instancingMorph&&a.enable(2),E.matcap&&a.enable(3),E.envMap&&a.enable(4),E.normalMapObjectSpace&&a.enable(5),E.normalMapTangentSpace&&a.enable(6),E.clearcoat&&a.enable(7),E.iridescence&&a.enable(8),E.alphaTest&&a.enable(9),E.vertexColors&&a.enable(10),E.vertexAlphas&&a.enable(11),E.vertexUv1s&&a.enable(12),E.vertexUv2s&&a.enable(13),E.vertexUv3s&&a.enable(14),E.vertexTangents&&a.enable(15),E.anisotropy&&a.enable(16),E.alphaHash&&a.enable(17),E.batching&&a.enable(18),E.dispersion&&a.enable(19),E.retroreflection&&a.enable(24),E.batchingColor&&a.enable(20),E.gradientMap&&a.enable(21),E.packedNormalMap&&a.enable(22),E.vertexNormals&&a.enable(23),x.push(a.mask),a.disableAll(),E.fog&&a.enable(0),E.useFog&&a.enable(1),E.flatShading&&a.enable(2),E.logarithmicDepthBuffer&&a.enable(3),E.reversedDepthBuffer&&a.enable(4),E.skinning&&a.enable(5),E.morphTargets&&a.enable(6),E.morphNormals&&a.enable(7),E.morphColors&&a.enable(8),E.premultipliedAlpha&&a.enable(9),E.shadowMapEnabled&&a.enable(10),E.doubleSided&&a.enable(11),E.flipSided&&a.enable(12),E.useDepthPacking&&a.enable(13),E.dithering&&a.enable(14),E.transmission&&a.enable(15),E.sheen&&a.enable(16),E.opaque&&a.enable(17),E.pointsUvs&&a.enable(18),E.decodeVideoTexture&&a.enable(19),E.decodeVideoTextureEmissive&&a.enable(20),E.alphaToCoverage&&a.enable(21),E.numLightProbeGrids>0&&a.enable(22),E.hasPositionAttribute&&a.enable(23),x.push(a.mask)}function R(x){let E=p[x.type],C;if(E){let U=zn[E];C=bu.clone(U.uniforms)}else C=x.uniforms;return C}function M(x,E){let C=h.get(E);return C!==void 0?++C.usedTimes:(C=new q0(i,E,x,s),l.push(C),h.set(E,C)),C}function T(x){if(--x.usedTimes===0){let E=l.indexOf(x);l[E]=l[l.length-1],l.pop(),h.delete(x.cacheKey),x.destroy()}}function w(x){o.remove(x)}function P(){o.dispose()}return{getParameters:v,getProgramCacheKey:m,getUniforms:R,acquireProgram:M,releaseProgram:T,releaseShaderCache:w,programs:l,dispose:P}}function $0(){let i=new WeakMap;function e(a){return i.has(a)}function t(a){let o=i.get(a);return o===void 0&&(o={},i.set(a,o)),o}function n(a){i.delete(a)}function s(a,o,c){i.get(a)[o]=c}function r(){i=new WeakMap}return{has:e,get:t,remove:n,update:s,dispose:r}}function K0(i,e){return i.groupOrder!==e.groupOrder?i.groupOrder-e.groupOrder:i.renderOrder!==e.renderOrder?i.renderOrder-e.renderOrder:i.material.id!==e.material.id?i.material.id-e.material.id:i.materialVariant!==e.materialVariant?i.materialVariant-e.materialVariant:i.z!==e.z?i.z-e.z:i.id-e.id}function Gu(i,e){return i.groupOrder!==e.groupOrder?i.groupOrder-e.groupOrder:i.renderOrder!==e.renderOrder?i.renderOrder-e.renderOrder:i.z!==e.z?e.z-i.z:i.id-e.id}function Wu(){let i=[],e=0,t=[],n=[],s=[];function r(){e=0,t.length=0,n.length=0,s.length=0}function a(u){let p=0;return u.isInstancedMesh&&(p+=2),u.isSkinnedMesh&&(p+=1),p}function o(u,p,g,v,m,f){let S=i[e];return S===void 0?(S={id:u.id,object:u,geometry:p,material:g,materialVariant:a(u),groupOrder:v,renderOrder:u.renderOrder,z:m,group:f},i[e]=S):(S.id=u.id,S.object=u,S.geometry=p,S.material=g,S.materialVariant=a(u),S.groupOrder=v,S.renderOrder=u.renderOrder,S.z=m,S.group=f),e++,S}function c(u,p,g,v,m,f,S){S.reversedDepth===!0&&(m=-m);let R=o(u,p,g,v,m,f);g.transmission>0?n.push(R):g.transparent===!0?s.push(R):t.push(R)}function l(u,p,g,v,m,f){let S=o(u,p,g,v,m,f);g.transmission>0?n.unshift(S):g.transparent===!0?s.unshift(S):t.unshift(S)}function h(u,p){t.length>1&&t.sort(u||K0),n.length>1&&n.sort(p||Gu),s.length>1&&s.sort(p||Gu)}function d(){for(let u=e,p=i.length;u<p;u++){let g=i[u];if(g.id===null)break;g.id=null,g.object=null,g.geometry=null,g.material=null,g.group=null}}return{opaque:t,transmissive:n,transparent:s,init:r,push:c,unshift:l,finish:d,sort:h}}function Q0(){let i=new WeakMap;function e(n,s){let r=i.get(n),a;return r===void 0?(a=new Wu,i.set(n,[a])):s>=r.length?(a=new Wu,r.push(a)):a=r[s],a}function t(){i=new WeakMap}return{get:e,dispose:t}}function j0(){let i={};return{get:function(e){if(i[e.id]!==void 0)return i[e.id];let t;switch(e.type){case"SunLight":case"DirectionalLight":t={direction:new L,color:new Fe};break;case"SpotLight":t={position:new L,direction:new L,color:new Fe,distance:0,coneCos:0,penumbraCos:0,decay:0};break;case"PointLight":t={position:new L,color:new Fe,distance:0,decay:0};break;case"HemisphereLight":t={direction:new L,skyColor:new Fe,groundColor:new Fe};break;case"RectAreaLight":t={color:new Fe,position:new L,halfWidth:new L,halfHeight:new L};break}return i[e.id]=t,t}}}function e_(){let i={};return{get:function(e){if(i[e.id]!==void 0)return i[e.id];let t;switch(e.type){case"SunLight":case"DirectionalLight":t={shadowIntensity:1,shadowBias:0,shadowNormalBias:0,shadowRadius:1,shadowMapSize:new he};break;case"SpotLight":t={shadowIntensity:1,shadowBias:0,shadowNormalBias:0,shadowRadius:1,shadowMapSize:new he};break;case"PointLight":t={shadowIntensity:1,shadowBias:0,shadowNormalBias:0,shadowRadius:1,shadowMapSize:new he,shadowCameraNear:1,shadowCameraFar:1e3};break}return i[e.id]=t,t}}}var t_=0;function n_(i,e){return(e.castShadow?2:0)-(i.castShadow?2:0)+(e.map?1:0)-(i.map?1:0)}function i_(i){let e=new j0,t=e_(),n={version:0,hash:{sunLength:-1,directionalLength:-1,pointLength:-1,spotLength:-1,rectAreaLength:-1,hemiLength:-1,numSunShadows:-1,numDirectionalShadows:-1,numPointShadows:-1,numSpotShadows:-1,numSpotMaps:-1,numLightProbes:-1},ambient:[0,0,0],probe:[],sun:[],sunShadow:[],sunShadowMap:[],sunShadowMatrix:[],sunShadowCascade:[],directional:[],directionalShadow:[],directionalShadowMap:[],directionalShadowMatrix:[],spot:[],spotLightMap:[],spotShadow:[],spotShadowMap:[],spotLightMatrix:[],rectArea:[],rectAreaLTC1:null,rectAreaLTC2:null,point:[],pointShadow:[],pointShadowMap:[],pointShadowMatrix:[],hemi:[],numSpotLightShadowsWithMaps:0,numLightProbes:0};for(let l=0;l<9;l++)n.probe.push(new L);let s=new L,r=new ut,a=new ut;function o(l){let h=0,d=0,u=0;for(let B=0;B<9;B++)n.probe[B].set(0,0,0);let p=0,g=0,v=0,m=0,f=0,S=0,R=0,M=0,T=0,w=0,P=0,x=0,E=0,C=0;l.sort(n_);for(let B=0,H=l.length;B<H;B++){let N=l[B],V=N.color,X=N.intensity,W=N.distance,re=null;if(N.shadow&&N.shadow.map&&(N.shadow.map.texture.format===gi?re=N.shadow.map.texture:re=N.shadow.map.depthTexture||N.shadow.map.texture),N.isAmbientLight)h+=V.r*X,d+=V.g*X,u+=V.b*X;else if(N.isLightProbe){for(let Y=0;Y<9;Y++)n.probe[Y].addScaledVector(N.sh.coefficients[Y],X);C++}else if(N.isSunLight){let Y=e.get(N);if(Y.color.copy(N.color).multiplyScalar(N.intensity),N.castShadow){let Q=N.shadow,j=t.get(N);j.shadowIntensity=Q.intensity,j.shadowBias=Q.bias,j.shadowNormalBias=Q.normalBias,j.shadowRadius=Q.radius,j.shadowMapSize.copy(Q.mapSize).multiply(Q.getFrameExtents()),n.sunShadow[g]=j,n.sunShadowMap[g]=re;let Re=Q.getViewportCount();for(let Ce=0;Ce<Re;Ce++)n.sunShadowMatrix[v+Ce]=Q.getMatrix(Ce),n.sunShadowCascade[v+Ce]=Q._cascadeData[Ce];v+=Re,g++}n.sun[p]=Y,p++}else if(N.isDirectionalLight){let Y=e.get(N);if(Y.color.copy(N.color).multiplyScalar(N.intensity),N.castShadow){let Q=N.shadow,j=t.get(N);j.shadowIntensity=Q.intensity,j.shadowBias=Q.bias,j.shadowNormalBias=Q.normalBias,j.shadowRadius=Q.radius,j.shadowMapSize=Q.mapSize,n.directionalShadow[m]=j,n.directionalShadowMap[m]=re,n.directionalShadowMatrix[m]=N.shadow.matrix,T++}n.directional[m]=Y,m++}else if(N.isSpotLight){let Y=e.get(N);Y.position.setFromMatrixPosition(N.matrixWorld),Y.color.copy(V).multiplyScalar(X),Y.distance=W,Y.coneCos=Math.cos(N.angle),Y.penumbraCos=Math.cos(N.angle*(1-N.penumbra)),Y.decay=N.decay,n.spot[S]=Y;let Q=N.shadow;if(N.map&&(n.spotLightMap[x]=N.map,x++,Q.updateMatrices(N),N.castShadow&&E++),n.spotLightMatrix[S]=Q.matrix,N.castShadow){let j=t.get(N);j.shadowIntensity=Q.intensity,j.shadowBias=Q.bias,j.shadowNormalBias=Q.normalBias,j.shadowRadius=Q.radius,j.shadowMapSize=Q.mapSize,n.spotShadow[S]=j,n.spotShadowMap[S]=re,P++}S++}else if(N.isRectAreaLight){let Y=e.get(N);Y.color.copy(V).multiplyScalar(X),Y.halfWidth.set(N.width*.5,0,0),Y.halfHeight.set(0,N.height*.5,0),n.rectArea[R]=Y,R++}else if(N.isPointLight){let Y=e.get(N);if(Y.color.copy(N.color).multiplyScalar(N.intensity),Y.distance=N.distance,Y.decay=N.decay,N.castShadow){let Q=N.shadow,j=t.get(N);j.shadowIntensity=Q.intensity,j.shadowBias=Q.bias,j.shadowNormalBias=Q.normalBias,j.shadowRadius=Q.radius,j.shadowMapSize=Q.mapSize,j.shadowCameraNear=Q.camera.near,j.shadowCameraFar=Q.camera.far,n.pointShadow[f]=j,n.pointShadowMap[f]=re,n.pointShadowMatrix[f]=N.shadow.matrix,w++}n.point[f]=Y,f++}else if(N.isHemisphereLight){let Y=e.get(N);Y.skyColor.copy(N.color).multiplyScalar(X),Y.groundColor.copy(N.groundColor).multiplyScalar(X),n.hemi[M]=Y,M++}}R>0&&(i.has("OES_texture_float_linear")===!0?(n.rectAreaLTC1=Ee.LTC_FLOAT_1,n.rectAreaLTC2=Ee.LTC_FLOAT_2):(n.rectAreaLTC1=Ee.LTC_HALF_1,n.rectAreaLTC2=Ee.LTC_HALF_2)),n.ambient[0]=h,n.ambient[1]=d,n.ambient[2]=u;let U=n.hash;(U.sunLength!==p||U.directionalLength!==m||U.pointLength!==f||U.spotLength!==S||U.rectAreaLength!==R||U.hemiLength!==M||U.numSunShadows!==g||U.numDirectionalShadows!==T||U.numPointShadows!==w||U.numSpotShadows!==P||U.numSpotMaps!==x||U.numLightProbes!==C)&&(n.sun.length=p,n.directional.length=m,n.spot.length=S,n.rectArea.length=R,n.point.length=f,n.hemi.length=M,n.sunShadow.length=g,n.sunShadowMap.length=g,n.sunShadowMatrix.length=v,n.sunShadowCascade.length=v,n.directionalShadow.length=T,n.directionalShadowMap.length=T,n.directionalShadowMatrix.length=T,n.pointShadow.length=w,n.pointShadowMap.length=w,n.pointShadowMatrix.length=w,n.spotShadow.length=P,n.spotShadowMap.length=P,n.spotLightMatrix.length=P+x-E,n.spotLightMap.length=x,n.numSpotLightShadowsWithMaps=E,n.numLightProbes=C,U.sunLength=p,U.directionalLength=m,U.pointLength=f,U.spotLength=S,U.rectAreaLength=R,U.hemiLength=M,U.numSunShadows=g,U.numDirectionalShadows=T,U.numPointShadows=w,U.numSpotShadows=P,U.numSpotMaps=x,U.numLightProbes=C,n.version=t_++)}function c(l,h){let d=0,u=0,p=0,g=0,v=0,m=0,f=h.matrixWorldInverse;for(let S=0,R=l.length;S<R;S++){let M=l[S];if(M.isSunLight){let T=n.sun[d];T.direction.setFromMatrixPosition(M.matrixWorld),T.direction.transformDirection(f),d++}else if(M.isDirectionalLight){let T=n.directional[u];T.direction.setFromMatrixPosition(M.matrixWorld),s.setFromMatrixPosition(M.target.matrixWorld),T.direction.sub(s),T.direction.transformDirection(f),u++}else if(M.isSpotLight){let T=n.spot[g];T.position.setFromMatrixPosition(M.matrixWorld),T.position.applyMatrix4(f),T.direction.setFromMatrixPosition(M.matrixWorld),s.setFromMatrixPosition(M.target.matrixWorld),T.direction.sub(s),T.direction.transformDirection(f),g++}else if(M.isRectAreaLight){let T=n.rectArea[v];T.position.setFromMatrixPosition(M.matrixWorld),T.position.applyMatrix4(f),a.identity(),r.copy(M.matrixWorld),r.premultiply(f),a.extractRotation(r),T.halfWidth.set(M.width*.5,0,0),T.halfHeight.set(0,M.height*.5,0),T.halfWidth.applyMatrix4(a),T.halfHeight.applyMatrix4(a),v++}else if(M.isPointLight){let T=n.point[p];T.position.setFromMatrixPosition(M.matrixWorld),T.position.applyMatrix4(f),p++}else if(M.isHemisphereLight){let T=n.hemi[m];T.direction.setFromMatrixPosition(M.matrixWorld),T.direction.transformDirection(f),m++}}}return{setup:o,setupView:c,state:n}}function Xu(i){let e=new i_(i),t=[],n=[],s=[];function r(u){d.camera=u,t.length=0,n.length=0,s.length=0}function a(u){t.push(u)}function o(u){n.push(u)}function c(u){s.push(u)}function l(){e.setup(t)}function h(u){e.setupView(t,u)}let d={lightsArray:t,shadowsArray:n,lightProbeGridArray:s,camera:null,lights:e,transmissionRenderTarget:{},textureUnits:0};return{init:r,state:d,setupLights:l,setupLightsView:h,pushLight:a,pushShadow:o,pushLightProbeGrid:c}}function s_(i){let e=new WeakMap;function t(s,r=0){let a=e.get(s),o;return a===void 0?(o=new Xu(i),e.set(s,[o])):r>=a.length?(o=new Xu(i),a.push(o)):o=a[r],o}function n(){e=new WeakMap}return{get:t,dispose:n}}var r_=`void main() {
	gl_Position = vec4( position, 1.0 );
}`,a_=`uniform sampler2D shadow_pass;
uniform vec2 resolution;
uniform float radius;
void main() {
	const float samples = float( VSM_SAMPLES );
	float mean = 0.0;
	float squared_mean = 0.0;
	float uvStride = samples <= 1.0 ? 0.0 : 2.0 / ( samples - 1.0 );
	float uvStart = samples <= 1.0 ? 0.0 : - 1.0;
	for ( float i = 0.0; i < samples; i ++ ) {
		float uvOffset = uvStart + i * uvStride;
		#ifdef HORIZONTAL_PASS
			vec2 distribution = texture2D( shadow_pass, ( gl_FragCoord.xy + vec2( uvOffset, 0.0 ) * radius ) / resolution ).rg;
			mean += distribution.x;
			squared_mean += distribution.y * distribution.y + distribution.x * distribution.x;
		#else
			float depth = texture2D( shadow_pass, ( gl_FragCoord.xy + vec2( 0.0, uvOffset ) * radius ) / resolution ).r;
			mean += depth;
			squared_mean += depth * depth;
		#endif
	}
	mean = mean / samples;
	squared_mean = squared_mean / samples;
	float std_dev = sqrt( max( 0.0, squared_mean - mean * mean ) );
	gl_FragColor = vec4( mean, std_dev, 0.0, 1.0 );
}`,o_=[new L(1,0,0),new L(-1,0,0),new L(0,1,0),new L(0,-1,0),new L(0,0,1),new L(0,0,-1)],l_=[new L(0,-1,0),new L(0,-1,0),new L(0,0,1),new L(0,0,-1),new L(0,-1,0),new L(0,-1,0)],qu=new ut,Xr=new L,Cc=new L;function c_(i,e,t){let n=new xs,s=new he,r=new he,a=new vt,o=new Qa,c=new ja,l={},h=t.maxTextureSize,d={[fi]:Ut,[Ut]:fi,[nn]:nn},u=new en({defines:{VSM_SAMPLES:8},uniforms:{shadow_pass:{value:null},resolution:{value:new he},radius:{value:4}},vertexShader:r_,fragmentShader:a_}),p=u.clone();p.defines.HORIZONTAL_PASS=1;let g=new Mt;g.setAttribute("position",new Ht(new Float32Array([-1,-1,.5,3,-1,.5,-1,3,.5]),3));let v=new Ke(g,u),m=this;this.enabled=!1,this.autoUpdate=!0,this.needsUpdate=!1,this.type=Ur;let f=this.type;this.render=function(w,P,x){if(m.enabled===!1||m.autoUpdate===!1&&m.needsUpdate===!1||w.length===0)return;this.type===Fh&&(ke("WebGLShadowMap: PCFSoftShadowMap has been removed. Using PCFShadowMap instead."),this.type=Ur);let E=i.getRenderTarget(),C=i.getActiveCubeFace(),U=i.getActiveMipmapLevel(),B=i.state;B.setBlending(Nn),B.buffers.depth.getReversed()===!0?B.buffers.color.setClear(0,0,0,0):B.buffers.color.setClear(1,1,1,1),B.buffers.depth.setTest(!0),B.setScissorTest(!1);let H=f!==this.type;H&&P.traverse(function(N){N.material&&(Array.isArray(N.material)?N.material.forEach(V=>V.needsUpdate=!0):N.material.needsUpdate=!0)});for(let N=0,V=w.length;N<V;N++){let X=w[N],W=X.shadow;if(W===void 0){ke("WebGLShadowMap:",X,"has no shadow.");continue}if(W.autoUpdate===!1&&W.needsUpdate===!1)continue;s.copy(W.mapSize);let re=W.getFrameExtents();s.multiply(re),r.copy(W.mapSize),(s.x>h||s.y>h)&&(s.x>h&&(r.x=Math.floor(h/re.x),s.x=r.x*re.x,W.mapSize.x=r.x),s.y>h&&(r.y=Math.floor(h/re.y),s.y=r.y*re.y,W.mapSize.y=r.y));let Y=i.state.buffers.depth.getReversed();if(W.camera._reversedDepth=Y,W.map===null||H===!0){if(W.map!==null&&(W.map.depthTexture!==null&&(W.map.depthTexture.dispose(),W.map.depthTexture=null),W.map.dispose()),this.type===Rs){if(X.isPointLight){ke("WebGLShadowMap: VSM shadow maps are not supported for PointLights. Use PCF or BasicShadowMap instead.");continue}W.map=new Jt(s.x,s.y,{format:gi,type:Sn,minFilter:At,magFilter:At,generateMipmaps:!1}),W.map.texture.name=X.name+".shadowMap",W.map.depthTexture=new oi(s.x,s.y,ln),W.map.depthTexture.name=X.name+".shadowMapDepth",W.map.depthTexture.format=Cn,W.map.depthTexture.compareFunction=null,W.map.depthTexture.minFilter=Lt,W.map.depthTexture.magFilter=Lt}else X.isPointLight?(W.map=new sl(s.x),W.map.depthTexture=new Xa(s.x,Mn)):(W.map=new Jt(s.x,s.y),W.map.depthTexture=new oi(s.x,s.y,Mn)),W.map.depthTexture.name=X.name+".shadowMap",W.map.depthTexture.format=Cn,this.type===Ur?(W.map.depthTexture.compareFunction=Y?tl:el,W.map.depthTexture.minFilter=At,W.map.depthTexture.magFilter=At):(W.map.depthTexture.compareFunction=null,W.map.depthTexture.minFilter=Lt,W.map.depthTexture.magFilter=Lt);W.camera.updateProjectionMatrix()}W.map.isWebGLCubeRenderTarget!==!0&&(W.map.width!==s.x||W.map.height!==s.y)&&W.map.setSize(s.x,s.y);let Q=W.map.isWebGLCubeRenderTarget?6:W.getViewportCount();X.isPointLight!==!0&&W.updateMatrices(X,x);for(let j=0;j<Q;j++){let Re=W.getCamera(j);if(X.isPointLight){let Ce=W.camera,lt=W.matrix,je=X.distance||Ce.far;je!==Ce.far&&(Ce.far=je,Ce.updateProjectionMatrix()),Xr.setFromMatrixPosition(X.matrixWorld),Ce.position.copy(Xr),Cc.copy(Ce.position),Cc.add(o_[j]),Ce.up.copy(l_[j]),Ce.lookAt(Cc),Ce.updateMatrixWorld(),lt.makeTranslation(-Xr.x,-Xr.y,-Xr.z),qu.multiplyMatrices(Ce.projectionMatrix,Ce.matrixWorldInverse),W._frustum.setFromProjectionMatrix(qu,Ce.coordinateSystem,Ce.reversedDepth)}if(W.map.isWebGLCubeRenderTarget)i.setRenderTarget(W.map,j),i.clear();else{j===0&&(i.setRenderTarget(W.map),i.clear());let Ce=W.getViewport(j);a.set(r.x*Ce.x,r.y*Ce.y,r.x*Ce.z,r.y*Ce.w),B.viewport(a)}n=W.getFrustum(j),M(P,x,Re,X,this.type)}W.isPointLightShadow!==!0&&this.type===Rs&&S(W,x),W.needsUpdate=!1}f=this.type,m.needsUpdate=!1,i.setRenderTarget(E,C,U)};function S(w,P){let x=e.update(v);u.defines.VSM_SAMPLES!==w.blurSamples&&(u.defines.VSM_SAMPLES=w.blurSamples,p.defines.VSM_SAMPLES=w.blurSamples,u.needsUpdate=!0,p.needsUpdate=!0),w.mapPass===null?w.mapPass=new Jt(s.x,s.y,{format:gi,type:Sn}):(w.mapPass.width!==w.map.width||w.mapPass.height!==w.map.height)&&w.mapPass.setSize(w.map.width,w.map.height),u.uniforms.shadow_pass.value=w.map.depthTexture,u.uniforms.resolution.value.set(w.map.width,w.map.height),u.uniforms.radius.value=w.radius,i.setRenderTarget(w.mapPass),i.clear(),i.renderBufferDirect(P,null,x,u,v,null),p.uniforms.shadow_pass.value=w.mapPass.texture,p.uniforms.resolution.value.set(w.map.width,w.map.height),p.uniforms.radius.value=w.radius,i.setRenderTarget(w.map),i.clear(),i.renderBufferDirect(P,null,x,p,v,null)}function R(w,P,x,E){let C=null,U=x.isPointLight===!0?w.customDistanceMaterial:w.customDepthMaterial;if(U!==void 0)C=U;else if(C=x.isPointLight===!0?c:o,i.localClippingEnabled&&P.clipShadows===!0&&Array.isArray(P.clippingPlanes)&&P.clippingPlanes.length!==0||P.displacementMap&&P.displacementScale!==0||P.alphaMap&&P.alphaTest>0||P.map&&P.alphaTest>0||P.alphaToCoverage===!0){let B=C.uuid,H=P.uuid,N=l[B];N===void 0&&(N={},l[B]=N);let V=N[H];V===void 0&&(V=C.clone(),N[H]=V,P.addEventListener("dispose",T)),C=V}if(C.visible=P.visible,C.wireframe=P.wireframe,E===Rs?C.side=P.shadowSide!==null?P.shadowSide:P.side:C.side=P.shadowSide!==null?P.shadowSide:d[P.side],C.alphaMap=P.alphaMap,C.alphaTest=P.alphaToCoverage===!0?.5:P.alphaTest,C.map=P.map,C.clipShadows=P.clipShadows,C.clippingPlanes=P.clippingPlanes,C.clipIntersection=P.clipIntersection,C.displacementMap=P.displacementMap,C.displacementScale=P.displacementScale,C.displacementBias=P.displacementBias,C.wireframeLinewidth=P.wireframeLinewidth,C.linewidth=P.linewidth,x.isPointLight===!0&&C.isMeshDistanceMaterial===!0){let B=i.properties.get(C);B.light=x}return C}function M(w,P,x,E,C){if(w.visible===!1)return;if(w.layers.test(P.layers)&&(w.isMesh||w.isLine||w.isPoints)&&(w.castShadow||w.receiveShadow&&C===Rs)&&(!w.frustumCulled||w.intersectsFrustum(n))){w.modelViewMatrix.multiplyMatrices(x.matrixWorldInverse,w.matrixWorld);let H=e.update(w),N=w.material;if(Array.isArray(N)){let V=H.groups;for(let X=0,W=V.length;X<W;X++){let re=V[X],Y=N[re.materialIndex];if(Y&&Y.visible){let Q=R(w,Y,E,C);w.onBeforeShadow(i,w,P,x,H,Q,re),i.renderBufferDirect(x,null,H,Q,w,re),w.onAfterShadow(i,w,P,x,H,Q,re)}}}else if(N.visible){let V=R(w,N,E,C);w.onBeforeShadow(i,w,P,x,H,V,null),i.renderBufferDirect(x,null,H,V,w,null),w.onAfterShadow(i,w,P,x,H,V,null)}}let B=w.children;for(let H=0,N=B.length;H<N;H++)M(B[H],P,x,E,C)}function T(w){w.target.removeEventListener("dispose",T);for(let x in l){let E=l[x],C=w.target.uuid;C in E&&(E[C].dispose(),delete E[C])}}}function h_(i,e){function t(){let O=!1,Me=new vt,K=null,ce=new vt(0,0,0,0);return{setMask:function(Se){K!==Se&&!O&&(i.colorMask(Se,Se,Se,Se),K=Se)},setLocked:function(Se){O=Se},setClear:function(Se,se,De,Ie,ft){ft===!0&&(Se*=Ie,se*=Ie,De*=Ie),Me.set(Se,se,De,Ie),ce.equals(Me)===!1&&(i.clearColor(Se,se,De,Ie),ce.copy(Me))},reset:function(){O=!1,K=null,ce.set(-1,0,0,0)}}}function n(){let O=!1,Me=!1,K=null,ce=null,Se=null;return{setReversed:function(se){if(Me!==se){let De=e.get("EXT_clip_control");se?De.clipControlEXT(De.LOWER_LEFT_EXT,De.ZERO_TO_ONE_EXT):De.clipControlEXT(De.LOWER_LEFT_EXT,De.NEGATIVE_ONE_TO_ONE_EXT),Me=se;let Ie=Se;Se=null,this.setClear(Ie)}},getReversed:function(){return Me},setTest:function(se){se?ne(i.DEPTH_TEST):ve(i.DEPTH_TEST)},setMask:function(se){K!==se&&!O&&(i.depthMask(se),K=se)},setFunc:function(se){if(Me&&(se=gu[se]),ce!==se){switch(se){case Ra:i.depthFunc(i.NEVER);break;case Ca:i.depthFunc(i.ALWAYS);break;case Ia:i.depthFunc(i.LESS);break;case ls:i.depthFunc(i.LEQUAL);break;case Pa:i.depthFunc(i.EQUAL);break;case La:i.depthFunc(i.GEQUAL);break;case Da:i.depthFunc(i.GREATER);break;case Ua:i.depthFunc(i.NOTEQUAL);break;default:i.depthFunc(i.LEQUAL)}ce=se}},setLocked:function(se){O=se},setClear:function(se){Se!==se&&(Se=se,Me&&(se=1-se),i.clearDepth(se))},reset:function(){O=!1,K=null,ce=null,Se=null,Me=!1}}}function s(){let O=!1,Me=null,K=null,ce=null,Se=null,se=null,De=null,Ie=null,ft=null;return{setTest:function(Xe){O||(Xe?ne(i.STENCIL_TEST):ve(i.STENCIL_TEST))},setMask:function(Xe){Me!==Xe&&!O&&(i.stencilMask(Xe),Me=Xe)},setFunc:function(Xe,Gt,Yt){(K!==Xe||ce!==Gt||Se!==Yt)&&(i.stencilFunc(Xe,Gt,Yt),K=Xe,ce=Gt,Se=Yt)},setOp:function(Xe,Gt,Yt){(se!==Xe||De!==Gt||Ie!==Yt)&&(i.stencilOp(Xe,Gt,Yt),se=Xe,De=Gt,Ie=Yt)},setLocked:function(Xe){O=Xe},setClear:function(Xe){ft!==Xe&&(i.clearStencil(Xe),ft=Xe)},reset:function(){O=!1,Me=null,K=null,ce=null,Se=null,se=null,De=null,Ie=null,ft=null}}}let r=new t,a=new n,o=new s,c=new WeakMap,l=new WeakMap,h={},d={},u={},p=new WeakMap,g=[],v=null,m=!1,f=null,S=null,R=null,M=null,T=null,w=null,P=null,x=new Fe(0,0,0),E=0,C=!1,U=null,B=null,H=null,N=null,V=null,X=i.getParameter(i.MAX_COMBINED_TEXTURE_IMAGE_UNITS),W=!1,re=0,Y=i.getParameter(i.VERSION);Y.indexOf("WebGL")!==-1?(re=parseFloat(/^WebGL (\d)/.exec(Y)[1]),W=re>=1):Y.indexOf("OpenGL ES")!==-1&&(re=parseFloat(/^OpenGL ES (\d)/.exec(Y)[1]),W=re>=2);let Q=null,j={},Re=i.getParameter(i.SCISSOR_BOX),Ce=i.getParameter(i.VIEWPORT),lt=new vt().fromArray(Re),je=new vt().fromArray(Ce);function ot(O,Me,K,ce){let Se=new Uint8Array(4),se=i.createTexture();i.bindTexture(O,se),i.texParameteri(O,i.TEXTURE_MIN_FILTER,i.NEAREST),i.texParameteri(O,i.TEXTURE_MAG_FILTER,i.NEAREST);for(let De=0;De<K;De++)O===i.TEXTURE_3D||O===i.TEXTURE_2D_ARRAY?i.texImage3D(Me,0,i.RGBA,1,1,ce,0,i.RGBA,i.UNSIGNED_BYTE,Se):i.texImage2D(Me+De,0,i.RGBA,1,1,0,i.RGBA,i.UNSIGNED_BYTE,Se);return se}let q={};q[i.TEXTURE_2D]=ot(i.TEXTURE_2D,i.TEXTURE_2D,1),q[i.TEXTURE_CUBE_MAP]=ot(i.TEXTURE_CUBE_MAP,i.TEXTURE_CUBE_MAP_POSITIVE_X,6),q[i.TEXTURE_2D_ARRAY]=ot(i.TEXTURE_2D_ARRAY,i.TEXTURE_2D_ARRAY,1,1),q[i.TEXTURE_3D]=ot(i.TEXTURE_3D,i.TEXTURE_3D,1,1),r.setClear(0,0,0,1),a.setClear(1),o.setClear(0),ne(i.DEPTH_TEST),a.setFunc(ls),oe(!1),ue(Kl),ne(i.CULL_FACE),ae(Nn);function ne(O){h[O]!==!0&&(i.enable(O),h[O]=!0)}function ve(O){h[O]!==!1&&(i.disable(O),h[O]=!1)}function Oe(O,Me){return u[O]!==Me?(i.bindFramebuffer(O,Me),u[O]=Me,O===i.DRAW_FRAMEBUFFER&&(u[i.FRAMEBUFFER]=Me),O===i.FRAMEBUFFER&&(u[i.DRAW_FRAMEBUFFER]=Me),!0):!1}function we(O,Me){let K=g,ce=!1;if(O){K=p.get(Me),K===void 0&&(K=[],p.set(Me,K));let Se=O.textures;if(K.length!==Se.length||K[0]!==i.COLOR_ATTACHMENT0){for(let se=0,De=Se.length;se<De;se++)K[se]=i.COLOR_ATTACHMENT0+se;K.length=Se.length,ce=!0}}else K[0]!==i.BACK&&(K[0]=i.BACK,ce=!0);ce&&i.drawBuffers(K)}function He(O){return v!==O?(i.useProgram(O),v=O,!0):!1}let rt={[Ai]:i.FUNC_ADD,[Bh]:i.FUNC_SUBTRACT,[zh]:i.FUNC_REVERSE_SUBTRACT};rt[Hh]=i.MIN,rt[Vh]=i.MAX;let te={[kh]:i.ZERO,[Gh]:i.ONE,[Wh]:i.SRC_COLOR,[ec]:i.SRC_ALPHA,[$h]:i.SRC_ALPHA_SATURATE,[Zh]:i.DST_COLOR,[qh]:i.DST_ALPHA,[Xh]:i.ONE_MINUS_SRC_COLOR,[tc]:i.ONE_MINUS_SRC_ALPHA,[Jh]:i.ONE_MINUS_DST_COLOR,[Yh]:i.ONE_MINUS_DST_ALPHA,[Kh]:i.CONSTANT_COLOR,[Qh]:i.ONE_MINUS_CONSTANT_COLOR,[jh]:i.CONSTANT_ALPHA,[eu]:i.ONE_MINUS_CONSTANT_ALPHA};function ae(O,Me,K,ce,Se,se,De,Ie,ft,Xe){if(O===Nn){m===!0&&(ve(i.BLEND),m=!1);return}if(m===!1&&(ne(i.BLEND),m=!0),O!==Oh){if(O!==f||Xe!==C){if((S!==Ai||T!==Ai)&&(i.blendEquation(i.FUNC_ADD),S=Ai,T=Ai),Xe)switch(O){case Cs:i.blendFuncSeparate(i.ONE,i.ONE_MINUS_SRC_ALPHA,i.ONE,i.ONE_MINUS_SRC_ALPHA);break;case Fn:i.blendFunc(i.ONE,i.ONE);break;case Ql:i.blendFuncSeparate(i.ZERO,i.ONE_MINUS_SRC_COLOR,i.ZERO,i.ONE);break;case jl:i.blendFuncSeparate(i.DST_COLOR,i.ONE_MINUS_SRC_ALPHA,i.ZERO,i.ONE);break;default:We("WebGLState: Invalid blending: ",O);break}else switch(O){case Cs:i.blendFuncSeparate(i.SRC_ALPHA,i.ONE_MINUS_SRC_ALPHA,i.ONE,i.ONE_MINUS_SRC_ALPHA);break;case Fn:i.blendFuncSeparate(i.SRC_ALPHA,i.ONE,i.ONE,i.ONE);break;case Ql:We("WebGLState: SubtractiveBlending requires material.premultipliedAlpha = true");break;case jl:We("WebGLState: MultiplyBlending requires material.premultipliedAlpha = true");break;default:We("WebGLState: Invalid blending: ",O);break}R=null,M=null,w=null,P=null,x.set(0,0,0),E=0,f=O,C=Xe}return}Se=Se||Me,se=se||K,De=De||ce,(Me!==S||Se!==T)&&(i.blendEquationSeparate(rt[Me],rt[Se]),S=Me,T=Se),(K!==R||ce!==M||se!==w||De!==P)&&(i.blendFuncSeparate(te[K],te[ce],te[se],te[De]),R=K,M=ce,w=se,P=De),(Ie.equals(x)===!1||ft!==E)&&(i.blendColor(Ie.r,Ie.g,Ie.b,ft),x.copy(Ie),E=ft),f=O,C=!1}function le(O,Me){O.side===nn?ve(i.CULL_FACE):ne(i.CULL_FACE);let K=O.side===Ut;Me&&(K=!K),oe(K),O.blending===Cs&&O.transparent===!1?ae(Nn):ae(O.blending,O.blendEquation,O.blendSrc,O.blendDst,O.blendEquationAlpha,O.blendSrcAlpha,O.blendDstAlpha,O.blendColor,O.blendAlpha,O.premultipliedAlpha),a.setFunc(O.depthFunc),a.setTest(O.depthTest),a.setMask(O.depthWrite),r.setMask(O.colorWrite);let ce=O.stencilWrite;o.setTest(ce),ce&&(o.setMask(O.stencilWriteMask),o.setFunc(O.stencilFunc,O.stencilRef,O.stencilFuncMask),o.setOp(O.stencilFail,O.stencilZFail,O.stencilZPass)),Ne(O.polygonOffset,O.polygonOffsetFactor,O.polygonOffsetUnits),O.alphaToCoverage===!0?ne(i.SAMPLE_ALPHA_TO_COVERAGE):ve(i.SAMPLE_ALPHA_TO_COVERAGE)}function oe(O){U!==O&&(O?i.frontFace(i.CW):i.frontFace(i.CCW),U=O)}function ue(O){O!==Uh?(ne(i.CULL_FACE),O!==B&&(O===Kl?i.cullFace(i.BACK):O===Nh?i.cullFace(i.FRONT):i.cullFace(i.FRONT_AND_BACK))):ve(i.CULL_FACE),B=O}function Ue(O){O!==H&&(W&&i.lineWidth(O),H=O)}function Ne(O,Me,K){O?(ne(i.POLYGON_OFFSET_FILL),(N!==Me||V!==K)&&(N=Me,V=K,a.getReversed()&&(Me=-Me),i.polygonOffset(Me,K))):ve(i.POLYGON_OFFSET_FILL)}function Ge(O){O?ne(i.SCISSOR_TEST):ve(i.SCISSOR_TEST)}function qe(O){O===void 0&&(O=i.TEXTURE0+X-1),Q!==O&&(i.activeTexture(O),Q=O)}function D(O,Me,K){K===void 0&&(Q===null?K=i.TEXTURE0+X-1:K=Q);let ce=j[K];ce===void 0&&(ce={type:void 0,texture:void 0},j[K]=ce),(ce.type!==O||ce.texture!==Me)&&(Q!==K&&(i.activeTexture(K),Q=K),i.bindTexture(O,Me||q[O]),ce.type=O,ce.texture=Me)}function ct(){let O=j[Q];O!==void 0&&O.type!==void 0&&(i.bindTexture(O.type,null),O.type=void 0,O.texture=void 0)}function et(){try{i.compressedTexImage2D(...arguments)}catch(O){We("WebGLState:",O)}}function A(){try{i.compressedTexImage3D(...arguments)}catch(O){We("WebGLState:",O)}}function _(){try{i.texSubImage2D(...arguments)}catch(O){We("WebGLState:",O)}}function z(){try{i.texSubImage3D(...arguments)}catch(O){We("WebGLState:",O)}}function k(){try{i.compressedTexSubImage2D(...arguments)}catch(O){We("WebGLState:",O)}}function Z(){try{i.compressedTexSubImage3D(...arguments)}catch(O){We("WebGLState:",O)}}function fe(){try{i.texStorage2D(...arguments)}catch(O){We("WebGLState:",O)}}function _e(){try{i.texStorage3D(...arguments)}catch(O){We("WebGLState:",O)}}function J(){try{i.texImage2D(...arguments)}catch(O){We("WebGLState:",O)}}function ee(){try{i.texImage3D(...arguments)}catch(O){We("WebGLState:",O)}}function pe(O){return d[O]!==void 0?d[O]:i.getParameter(O)}function Le(O,Me){d[O]!==Me&&(i.pixelStorei(O,Me),d[O]=Me)}function xe(O){lt.equals(O)===!1&&(i.scissor(O.x,O.y,O.z,O.w),lt.copy(O))}function ge(O){je.equals(O)===!1&&(i.viewport(O.x,O.y,O.z,O.w),je.copy(O))}function Pe(O,Me){let K=l.get(Me);K===void 0&&(K=new WeakMap,l.set(Me,K));let ce=K.get(O);ce===void 0&&(ce=i.getUniformBlockIndex(Me,O.name),K.set(O,ce))}function ze(O,Me){let ce=l.get(Me).get(O);c.get(Me)!==ce&&(i.uniformBlockBinding(Me,ce,O.__bindingPointIndex),c.set(Me,ce))}function Ze(){i.disable(i.BLEND),i.disable(i.CULL_FACE),i.disable(i.DEPTH_TEST),i.disable(i.POLYGON_OFFSET_FILL),i.disable(i.SCISSOR_TEST),i.disable(i.STENCIL_TEST),i.disable(i.SAMPLE_ALPHA_TO_COVERAGE),i.blendEquation(i.FUNC_ADD),i.blendFunc(i.ONE,i.ZERO),i.blendFuncSeparate(i.ONE,i.ZERO,i.ONE,i.ZERO),i.blendColor(0,0,0,0),i.colorMask(!0,!0,!0,!0),i.clearColor(0,0,0,0),i.depthMask(!0),i.depthFunc(i.LESS),a.setReversed(!1),i.clearDepth(1),i.stencilMask(4294967295),i.stencilFunc(i.ALWAYS,0,4294967295),i.stencilOp(i.KEEP,i.KEEP,i.KEEP),i.clearStencil(0),i.cullFace(i.BACK),i.frontFace(i.CCW),i.polygonOffset(0,0),i.activeTexture(i.TEXTURE0),i.bindFramebuffer(i.FRAMEBUFFER,null),i.bindFramebuffer(i.DRAW_FRAMEBUFFER,null),i.bindFramebuffer(i.READ_FRAMEBUFFER,null),i.useProgram(null),i.lineWidth(1),i.scissor(0,0,i.canvas.width,i.canvas.height),i.viewport(0,0,i.canvas.width,i.canvas.height),i.pixelStorei(i.PACK_ALIGNMENT,4),i.pixelStorei(i.UNPACK_ALIGNMENT,4),i.pixelStorei(i.UNPACK_FLIP_Y_WEBGL,!1),i.pixelStorei(i.UNPACK_PREMULTIPLY_ALPHA_WEBGL,!1),i.pixelStorei(i.UNPACK_COLORSPACE_CONVERSION_WEBGL,i.BROWSER_DEFAULT_WEBGL),i.pixelStorei(i.PACK_ROW_LENGTH,0),i.pixelStorei(i.PACK_SKIP_PIXELS,0),i.pixelStorei(i.PACK_SKIP_ROWS,0),i.pixelStorei(i.UNPACK_ROW_LENGTH,0),i.pixelStorei(i.UNPACK_IMAGE_HEIGHT,0),i.pixelStorei(i.UNPACK_SKIP_PIXELS,0),i.pixelStorei(i.UNPACK_SKIP_ROWS,0),i.pixelStorei(i.UNPACK_SKIP_IMAGES,0),h={},d={},Q=null,j={},u={},p=new WeakMap,g=[],v=null,m=!1,f=null,S=null,R=null,M=null,T=null,w=null,P=null,x=new Fe(0,0,0),E=0,C=!1,U=null,B=null,H=null,N=null,V=null,lt.set(0,0,i.canvas.width,i.canvas.height),je.set(0,0,i.canvas.width,i.canvas.height),r.reset(),a.reset(),o.reset()}return{buffers:{color:r,depth:a,stencil:o},enable:ne,disable:ve,bindFramebuffer:Oe,drawBuffers:we,useProgram:He,setBlending:ae,setMaterial:le,setFlipSided:oe,setCullFace:ue,setLineWidth:Ue,setPolygonOffset:Ne,setScissorTest:Ge,activeTexture:qe,bindTexture:D,unbindTexture:ct,compressedTexImage2D:et,compressedTexImage3D:A,texImage2D:J,texImage3D:ee,pixelStorei:Le,getParameter:pe,updateUBOMapping:Pe,uniformBlockBinding:ze,texStorage2D:fe,texStorage3D:_e,texSubImage2D:_,texSubImage3D:z,compressedTexSubImage2D:k,compressedTexSubImage3D:Z,scissor:xe,viewport:ge,reset:Ze}}function u_(i,e,t,n,s,r,a){let o=e.has("WEBGL_multisampled_render_to_texture")?e.get("WEBGL_multisampled_render_to_texture"):null,c=typeof navigator>"u"?!1:/OculusBrowser/g.test(navigator.userAgent),l=new he,h=new WeakMap,d=new Set,u,p=new WeakMap,g=!1;try{g=typeof OffscreenCanvas<"u"&&new OffscreenCanvas(1,1).getContext("2d")!==null}catch{}function v(A,_){return g?new OffscreenCanvas(A,_):hs("canvas")}function m(A,_,z){let k=1,Z=et(A);if((Z.width>z||Z.height>z)&&(k=z/Math.max(Z.width,Z.height)),k<1)if(typeof HTMLImageElement<"u"&&A instanceof HTMLImageElement||typeof HTMLCanvasElement<"u"&&A instanceof HTMLCanvasElement||typeof ImageBitmap<"u"&&A instanceof ImageBitmap||typeof VideoFrame<"u"&&A instanceof VideoFrame){let fe=Math.floor(k*Z.width),_e=Math.floor(k*Z.height);u===void 0&&(u=v(fe,_e));let J=_?v(fe,_e):u;return J.width=fe,J.height=_e,J.getContext("2d").drawImage(A,0,0,fe,_e),ke("WebGLRenderer: Texture has been resized from ("+Z.width+"x"+Z.height+") to ("+fe+"x"+_e+")."),J}else return"data"in A&&ke("WebGLRenderer: Image in DataTexture is too big ("+Z.width+"x"+Z.height+")."),A;return A}function f(A){return A.generateMipmaps}function S(A){i.generateMipmap(A)}function R(A){return A.isWebGLCubeRenderTarget?i.TEXTURE_CUBE_MAP:A.isWebGL3DRenderTarget?i.TEXTURE_3D:A.isWebGLArrayRenderTarget||A.isCompressedArrayTexture?i.TEXTURE_2D_ARRAY:i.TEXTURE_2D}function M(A,_,z,k,Z,fe=!1){if(A!==null){if(i[A]!==void 0)return i[A];ke("WebGLRenderer: Attempt to use non-existing WebGL internal format '"+A+"'")}let _e;k&&(_e=e.get("EXT_texture_norm16"),_e||ke("WebGLRenderer: Unable to use normalized textures without EXT_texture_norm16 extension"));let J=_;if(_===i.RED&&(z===i.FLOAT&&(J=i.R32F),z===i.HALF_FLOAT&&(J=i.R16F),z===i.UNSIGNED_BYTE&&(J=i.R8),z===i.UNSIGNED_SHORT&&_e&&(J=_e.R16_EXT),z===i.SHORT&&_e&&(J=_e.R16_SNORM_EXT)),_===i.RED_INTEGER&&(z===i.UNSIGNED_BYTE&&(J=i.R8UI),z===i.UNSIGNED_SHORT&&(J=i.R16UI),z===i.UNSIGNED_INT&&(J=i.R32UI),z===i.BYTE&&(J=i.R8I),z===i.SHORT&&(J=i.R16I),z===i.INT&&(J=i.R32I)),_===i.RG&&(z===i.FLOAT&&(J=i.RG32F),z===i.HALF_FLOAT&&(J=i.RG16F),z===i.UNSIGNED_BYTE&&(J=i.RG8),z===i.UNSIGNED_SHORT&&_e&&(J=_e.RG16_EXT),z===i.SHORT&&_e&&(J=_e.RG16_SNORM_EXT)),_===i.RG_INTEGER&&(z===i.UNSIGNED_BYTE&&(J=i.RG8UI),z===i.UNSIGNED_SHORT&&(J=i.RG16UI),z===i.UNSIGNED_INT&&(J=i.RG32UI),z===i.BYTE&&(J=i.RG8I),z===i.SHORT&&(J=i.RG16I),z===i.INT&&(J=i.RG32I)),_===i.RGB_INTEGER&&(z===i.UNSIGNED_BYTE&&(J=i.RGB8UI),z===i.UNSIGNED_SHORT&&(J=i.RGB16UI),z===i.UNSIGNED_INT&&(J=i.RGB32UI),z===i.BYTE&&(J=i.RGB8I),z===i.SHORT&&(J=i.RGB16I),z===i.INT&&(J=i.RGB32I)),_===i.RGBA_INTEGER&&(z===i.UNSIGNED_BYTE&&(J=i.RGBA8UI),z===i.UNSIGNED_SHORT&&(J=i.RGBA16UI),z===i.UNSIGNED_INT&&(J=i.RGBA32UI),z===i.BYTE&&(J=i.RGBA8I),z===i.SHORT&&(J=i.RGBA16I),z===i.INT&&(J=i.RGBA32I)),_===i.RGB&&(z===i.UNSIGNED_SHORT&&_e&&(J=_e.RGB16_EXT),z===i.SHORT&&_e&&(J=_e.RGB16_SNORM_EXT),z===i.UNSIGNED_INT_5_9_9_9_REV&&(J=i.RGB9_E5),z===i.UNSIGNED_INT_10F_11F_11F_REV&&(J=i.R11F_G11F_B10F)),_===i.RGBA){let ee=fe?nr:st.getTransfer(Z);z===i.FLOAT&&(J=i.RGBA32F),z===i.HALF_FLOAT&&(J=i.RGBA16F),z===i.UNSIGNED_BYTE&&(J=ee===dt?i.SRGB8_ALPHA8:i.RGBA8),z===i.UNSIGNED_SHORT&&_e&&(J=_e.RGBA16_EXT),z===i.SHORT&&_e&&(J=_e.RGBA16_SNORM_EXT),z===i.UNSIGNED_SHORT_4_4_4_4&&(J=i.RGBA4),z===i.UNSIGNED_SHORT_5_5_5_1&&(J=i.RGB5_A1)}return(J===i.R16F||J===i.R32F||J===i.RG16F||J===i.RG32F||J===i.RGBA16F||J===i.RGBA32F)&&e.get("EXT_color_buffer_float"),J}function T(A,_){let z;return A?_===null||_===Mn||_===Ps?z=i.DEPTH24_STENCIL8:_===ln?z=i.DEPTH32F_STENCIL8:_===Is&&(z=i.DEPTH24_STENCIL8,ke("DepthTexture: 16 bit depth attachment is not supported with stencil. Using 24-bit attachment.")):_===null||_===Mn||_===Ps?z=i.DEPTH_COMPONENT24:_===ln?z=i.DEPTH_COMPONENT32F:_===Is&&(z=i.DEPTH_COMPONENT16),z}function w(A,_){return f(A)===!0||A.isFramebufferTexture&&A.minFilter!==Lt&&A.minFilter!==At?Math.log2(Math.max(_.width,_.height))+1:A.mipmaps!==void 0&&A.mipmaps.length>0?A.mipmaps.length:A.isCompressedTexture&&Array.isArray(A.image)?_.mipmaps.length:1}function P(A){let _=A.target;_.removeEventListener("dispose",P),E(_),_.isVideoTexture&&h.delete(_),_.isHTMLTexture&&d.delete(_)}function x(A){let _=A.target;_.removeEventListener("dispose",x),U(_)}function E(A){let _=n.get(A);if(_.__webglInit===void 0)return;let z=A.source,k=p.get(z);if(k){let Z=k[_.__cacheKey];Z.usedTimes--,Z.usedTimes===0&&C(A),Object.keys(k).length===0&&p.delete(z)}n.remove(A)}function C(A){let _=n.get(A);i.deleteTexture(_.__webglTexture);let z=A.source,k=p.get(z);delete k[_.__cacheKey],a.memory.textures--}function U(A){let _=n.get(A);if(A.depthTexture&&(A.depthTexture.dispose(),n.remove(A.depthTexture)),A.isWebGLCubeRenderTarget)for(let k=0;k<6;k++){if(Array.isArray(_.__webglFramebuffer[k]))for(let Z=0;Z<_.__webglFramebuffer[k].length;Z++)i.deleteFramebuffer(_.__webglFramebuffer[k][Z]);else i.deleteFramebuffer(_.__webglFramebuffer[k]);_.__webglDepthbuffer&&i.deleteRenderbuffer(_.__webglDepthbuffer[k])}else{if(Array.isArray(_.__webglFramebuffer))for(let k=0;k<_.__webglFramebuffer.length;k++)i.deleteFramebuffer(_.__webglFramebuffer[k]);else i.deleteFramebuffer(_.__webglFramebuffer);if(_.__webglDepthbuffer&&i.deleteRenderbuffer(_.__webglDepthbuffer),_.__webglMultisampledFramebuffer&&i.deleteFramebuffer(_.__webglMultisampledFramebuffer),_.__webglColorRenderbuffer)for(let k=0;k<_.__webglColorRenderbuffer.length;k++)_.__webglColorRenderbuffer[k]&&i.deleteRenderbuffer(_.__webglColorRenderbuffer[k]);_.__webglDepthRenderbuffer&&i.deleteRenderbuffer(_.__webglDepthRenderbuffer)}let z=A.textures;for(let k=0,Z=z.length;k<Z;k++){let fe=n.get(z[k]);fe.__webglTexture&&(i.deleteTexture(fe.__webglTexture),a.memory.textures--),n.remove(z[k])}n.remove(A)}let B=0;function H(){B=0}function N(){return B}function V(A){B=A}function X(){let A=B;return A>=s.maxTextures&&ke("WebGLTextures: Trying to use "+(A+1)+" texture units while this GPU supports only "+s.maxTextures),B+=1,A}function W(A){let _=[];return _.push(A.wrapS),_.push(A.wrapT),_.push(A.wrapR||0),_.push(A.magFilter),_.push(A.minFilter),_.push(A.anisotropy),_.push(A.internalFormat),_.push(A.format),_.push(A.type),_.push(A.generateMipmaps),_.push(A.premultiplyAlpha),_.push(A.flipY),_.push(A.unpackAlignment),_.push(A.colorSpace),_.join()}function re(A,_){let z=n.get(A);if(A.isVideoTexture&&D(A),A.isRenderTargetTexture===!1&&A.isExternalTexture!==!0&&A.version>0&&z.__version!==A.version){let k=A.image;if(k===null)ke("WebGLRenderer: Texture marked for update but no image data found.");else if(k.complete===!1)ke("WebGLRenderer: Texture marked for update but image is incomplete");else{ve(z,A,_);return}}else A.isExternalTexture&&(z.__webglTexture=A.sourceTexture?A.sourceTexture:null);t.bindTexture(i.TEXTURE_2D,z.__webglTexture,i.TEXTURE0+_)}function Y(A,_){let z=n.get(A);if(A.isRenderTargetTexture===!1&&A.version>0&&z.__version!==A.version){ve(z,A,_);return}else A.isExternalTexture&&(z.__webglTexture=A.sourceTexture?A.sourceTexture:null);t.bindTexture(i.TEXTURE_2D_ARRAY,z.__webglTexture,i.TEXTURE0+_)}function Q(A,_){let z=n.get(A);if(A.isRenderTargetTexture===!1&&A.version>0&&z.__version!==A.version){ve(z,A,_);return}t.bindTexture(i.TEXTURE_3D,z.__webglTexture,i.TEXTURE0+_)}function j(A,_){let z=n.get(A);if(A.isCubeDepthTexture!==!0&&A.version>0&&z.__version!==A.version){Oe(z,A,_);return}t.bindTexture(i.TEXTURE_CUBE_MAP,z.__webglTexture,i.TEXTURE0+_)}let Re={[Na]:i.REPEAT,[wn]:i.CLAMP_TO_EDGE,[Fa]:i.MIRRORED_REPEAT},Ce={[Lt]:i.NEAREST,[iu]:i.NEAREST_MIPMAP_NEAREST,[Or]:i.NEAREST_MIPMAP_LINEAR,[At]:i.LINEAR,[mo]:i.LINEAR_MIPMAP_NEAREST,[On]:i.LINEAR_MIPMAP_LINEAR},lt={[ou]:i.NEVER,[du]:i.ALWAYS,[lu]:i.LESS,[el]:i.LEQUAL,[cu]:i.EQUAL,[tl]:i.GEQUAL,[hu]:i.GREATER,[uu]:i.NOTEQUAL};function je(A,_){if(_.type===ln&&e.has("OES_texture_float_linear")===!1&&(_.magFilter===At||_.magFilter===mo||_.magFilter===Or||_.magFilter===On||_.minFilter===At||_.minFilter===mo||_.minFilter===Or||_.minFilter===On)&&ke("WebGLRenderer: Unable to use linear filtering with floating point textures. OES_texture_float_linear not supported on this device."),i.texParameteri(A,i.TEXTURE_WRAP_S,Re[_.wrapS]),i.texParameteri(A,i.TEXTURE_WRAP_T,Re[_.wrapT]),(A===i.TEXTURE_3D||A===i.TEXTURE_2D_ARRAY)&&i.texParameteri(A,i.TEXTURE_WRAP_R,Re[_.wrapR]),i.texParameteri(A,i.TEXTURE_MAG_FILTER,Ce[_.magFilter]),i.texParameteri(A,i.TEXTURE_MIN_FILTER,Ce[_.minFilter]),_.compareFunction&&(i.texParameteri(A,i.TEXTURE_COMPARE_MODE,i.COMPARE_REF_TO_TEXTURE),i.texParameteri(A,i.TEXTURE_COMPARE_FUNC,lt[_.compareFunction])),e.has("EXT_texture_filter_anisotropic")===!0){if(_.magFilter===Lt||_.minFilter!==Or&&_.minFilter!==On||_.type===ln&&e.has("OES_texture_float_linear")===!1)return;if(_.anisotropy>1||n.get(_).__currentAnisotropy){let z=e.get("EXT_texture_filter_anisotropic");i.texParameterf(A,z.TEXTURE_MAX_ANISOTROPY_EXT,Math.min(_.anisotropy,s.getMaxAnisotropy())),n.get(_).__currentAnisotropy=_.anisotropy}}}function ot(A,_){let z=!1;A.__webglInit===void 0&&(A.__webglInit=!0,_.addEventListener("dispose",P));let k=_.source,Z=p.get(k);Z===void 0&&(Z={},p.set(k,Z));let fe=W(_);if(fe!==A.__cacheKey){Z[fe]===void 0&&(Z[fe]={texture:i.createTexture(),usedTimes:0},a.memory.textures++,z=!0),Z[fe].usedTimes++;let _e=Z[A.__cacheKey];_e!==void 0&&(Z[A.__cacheKey].usedTimes--,_e.usedTimes===0&&C(_)),A.__cacheKey=fe,A.__webglTexture=Z[fe].texture}return z}function q(A,_,z){return Math.floor(Math.floor(A/z)/_)}function ne(A,_,z,k){let fe=A.updateRanges;if(fe.length===0)t.texSubImage2D(i.TEXTURE_2D,0,0,0,_.width,_.height,z,k,_.data);else{fe.sort((Le,xe)=>Le.start-xe.start);let _e=0;for(let Le=1;Le<fe.length;Le++){let xe=fe[_e],ge=fe[Le],Pe=xe.start+xe.count,ze=q(ge.start,_.width,4),Ze=q(xe.start,_.width,4);ge.start<=Pe+1&&ze===Ze&&q(ge.start+ge.count-1,_.width,4)===ze?xe.count=Math.max(xe.count,ge.start+ge.count-xe.start):(++_e,fe[_e]=ge)}fe.length=_e+1;let J=t.getParameter(i.UNPACK_ROW_LENGTH),ee=t.getParameter(i.UNPACK_SKIP_PIXELS),pe=t.getParameter(i.UNPACK_SKIP_ROWS);t.pixelStorei(i.UNPACK_ROW_LENGTH,_.width);for(let Le=0,xe=fe.length;Le<xe;Le++){let ge=fe[Le],Pe=Math.floor(ge.start/4),ze=Math.ceil(ge.count/4),Ze=Pe%_.width,O=Math.floor(Pe/_.width),Me=ze,K=1;t.pixelStorei(i.UNPACK_SKIP_PIXELS,Ze),t.pixelStorei(i.UNPACK_SKIP_ROWS,O),t.texSubImage2D(i.TEXTURE_2D,0,Ze,O,Me,K,z,k,_.data)}A.clearUpdateRanges(),t.pixelStorei(i.UNPACK_ROW_LENGTH,J),t.pixelStorei(i.UNPACK_SKIP_PIXELS,ee),t.pixelStorei(i.UNPACK_SKIP_ROWS,pe)}}function ve(A,_,z){let k=i.TEXTURE_2D;(_.isDataArrayTexture||_.isCompressedArrayTexture)&&(k=i.TEXTURE_2D_ARRAY),_.isData3DTexture&&(k=i.TEXTURE_3D);let Z=ot(A,_),fe=_.source;t.bindTexture(k,A.__webglTexture,i.TEXTURE0+z);let _e=n.get(fe);if(fe.version!==_e.__version||Z===!0){if(t.activeTexture(i.TEXTURE0+z),(typeof ImageBitmap<"u"&&_.image instanceof ImageBitmap)===!1){let K=st.getPrimaries(st.workingColorSpace),ce=_.colorSpace===Qn?null:st.getPrimaries(_.colorSpace),Se=_.colorSpace===Qn||K===ce?i.NONE:i.BROWSER_DEFAULT_WEBGL;t.pixelStorei(i.UNPACK_FLIP_Y_WEBGL,_.flipY),t.pixelStorei(i.UNPACK_PREMULTIPLY_ALPHA_WEBGL,_.premultiplyAlpha),t.pixelStorei(i.UNPACK_COLORSPACE_CONVERSION_WEBGL,Se)}t.pixelStorei(i.UNPACK_ALIGNMENT,_.unpackAlignment);let ee=m(_.image,!1,s.maxTextureSize);ee=ct(_,ee);let pe=r.convert(_.format,_.colorSpace),Le=r.convert(_.type),xe=M(_.internalFormat,pe,Le,_.normalized,_.colorSpace,_.isVideoTexture);je(k,_);let ge,Pe=_.mipmaps,ze=_.isVideoTexture!==!0,Ze=_e.__version===void 0||Z===!0,O=fe.dataReady,Me=w(_,ee);if(_.isDepthTexture)xe=T(_.format===mi,_.type),Ze&&(ze?t.texStorage2D(i.TEXTURE_2D,1,xe,ee.width,ee.height):t.texImage2D(i.TEXTURE_2D,0,xe,ee.width,ee.height,0,pe,Le,null));else if(_.isDataTexture)if(Pe.length>0){ze&&Ze&&t.texStorage2D(i.TEXTURE_2D,Me,xe,Pe[0].width,Pe[0].height);for(let K=0,ce=Pe.length;K<ce;K++)ge=Pe[K],ze?O&&t.texSubImage2D(i.TEXTURE_2D,K,0,0,ge.width,ge.height,pe,Le,ge.data):t.texImage2D(i.TEXTURE_2D,K,xe,ge.width,ge.height,0,pe,Le,ge.data);_.generateMipmaps=!1}else ze?(Ze&&t.texStorage2D(i.TEXTURE_2D,Me,xe,ee.width,ee.height),O&&ne(_,ee,pe,Le)):t.texImage2D(i.TEXTURE_2D,0,xe,ee.width,ee.height,0,pe,Le,ee.data);else if(_.isCompressedTexture)if(_.isCompressedArrayTexture){ze&&Ze&&t.texStorage3D(i.TEXTURE_2D_ARRAY,Me,xe,Pe[0].width,Pe[0].height,ee.depth);for(let K=0,ce=Pe.length;K<ce;K++)if(ge=Pe[K],_.format!==cn)if(pe!==null)if(ze){if(O)if(_.layerUpdates.size>0){let Se=bc(ge.width,ge.height,_.format,_.type);for(let se of _.layerUpdates){let De=ge.data.subarray(se*Se/ge.data.BYTES_PER_ELEMENT,(se+1)*Se/ge.data.BYTES_PER_ELEMENT);t.compressedTexSubImage3D(i.TEXTURE_2D_ARRAY,K,0,0,se,ge.width,ge.height,1,pe,De)}}else t.compressedTexSubImage3D(i.TEXTURE_2D_ARRAY,K,0,0,0,ge.width,ge.height,ee.depth,pe,ge.data)}else t.compressedTexImage3D(i.TEXTURE_2D_ARRAY,K,xe,ge.width,ge.height,ee.depth,0,ge.data,0,0);else ke("WebGLRenderer: Attempt to load unsupported compressed texture format in .uploadTexture()");else ze?O&&t.texSubImage3D(i.TEXTURE_2D_ARRAY,K,0,0,0,ge.width,ge.height,ee.depth,pe,Le,ge.data):t.texImage3D(i.TEXTURE_2D_ARRAY,K,xe,ge.width,ge.height,ee.depth,0,pe,Le,ge.data);_.layerUpdates.size>0&&_.clearLayerUpdates()}else{ze&&Ze&&t.texStorage2D(i.TEXTURE_2D,Me,xe,Pe[0].width,Pe[0].height);for(let K=0,ce=Pe.length;K<ce;K++)ge=Pe[K],_.format!==cn?pe!==null?ze?O&&t.compressedTexSubImage2D(i.TEXTURE_2D,K,0,0,ge.width,ge.height,pe,ge.data):t.compressedTexImage2D(i.TEXTURE_2D,K,xe,ge.width,ge.height,0,ge.data):ke("WebGLRenderer: Attempt to load unsupported compressed texture format in .uploadTexture()"):ze?O&&t.texSubImage2D(i.TEXTURE_2D,K,0,0,ge.width,ge.height,pe,Le,ge.data):t.texImage2D(i.TEXTURE_2D,K,xe,ge.width,ge.height,0,pe,Le,ge.data)}else if(_.isDataArrayTexture)if(ze){if(Ze&&t.texStorage3D(i.TEXTURE_2D_ARRAY,Me,xe,ee.width,ee.height,ee.depth),O)if(_.layerUpdates.size>0){let K=bc(ee.width,ee.height,_.format,_.type);for(let ce of _.layerUpdates){let Se=ee.data.subarray(ce*K/ee.data.BYTES_PER_ELEMENT,(ce+1)*K/ee.data.BYTES_PER_ELEMENT);t.texSubImage3D(i.TEXTURE_2D_ARRAY,0,0,0,ce,ee.width,ee.height,1,pe,Le,Se)}_.clearLayerUpdates()}else t.texSubImage3D(i.TEXTURE_2D_ARRAY,0,0,0,0,ee.width,ee.height,ee.depth,pe,Le,ee.data)}else t.texImage3D(i.TEXTURE_2D_ARRAY,0,xe,ee.width,ee.height,ee.depth,0,pe,Le,ee.data);else if(_.isData3DTexture)ze?(Ze&&t.texStorage3D(i.TEXTURE_3D,Me,xe,ee.width,ee.height,ee.depth),O&&t.texSubImage3D(i.TEXTURE_3D,0,0,0,0,ee.width,ee.height,ee.depth,pe,Le,ee.data)):t.texImage3D(i.TEXTURE_3D,0,xe,ee.width,ee.height,ee.depth,0,pe,Le,ee.data);else if(_.isFramebufferTexture){if(Ze)if(ze)t.texStorage2D(i.TEXTURE_2D,Me,xe,ee.width,ee.height);else{let K=ee.width,ce=ee.height;for(let Se=0;Se<Me;Se++)t.texImage2D(i.TEXTURE_2D,Se,xe,K,ce,0,pe,Le,null),K>>=1,ce>>=1}}else if(_.isHTMLTexture){if("texElementImage2D"in i){let K=i.canvas;if(K.hasAttribute("layoutsubtree")||K.setAttribute("layoutsubtree","true"),ee.parentNode!==K){K.appendChild(ee),d.add(_),K.onpaint=ce=>{let Se=ce.changedElements;for(let se of d)Se.includes(se.image)&&(se.needsUpdate=!0)},K.requestPaint();return}if(i.texElementImage2D.length===3)i.texElementImage2D(i.TEXTURE_2D,i.RGBA8,ee);else{let Se=i.RGBA,se=i.RGBA,De=i.UNSIGNED_BYTE;i.texElementImage2D(i.TEXTURE_2D,0,Se,se,De,ee)}i.texParameteri(i.TEXTURE_2D,i.TEXTURE_MIN_FILTER,i.LINEAR),i.texParameteri(i.TEXTURE_2D,i.TEXTURE_WRAP_S,i.CLAMP_TO_EDGE),i.texParameteri(i.TEXTURE_2D,i.TEXTURE_WRAP_T,i.CLAMP_TO_EDGE)}}else if(Pe.length>0){if(ze&&Ze){let K=et(Pe[0]);t.texStorage2D(i.TEXTURE_2D,Me,xe,K.width,K.height)}for(let K=0,ce=Pe.length;K<ce;K++)ge=Pe[K],ze?O&&t.texSubImage2D(i.TEXTURE_2D,K,0,0,pe,Le,ge):t.texImage2D(i.TEXTURE_2D,K,xe,pe,Le,ge);_.generateMipmaps=!1}else if(ze){if(Ze){let K=et(ee);t.texStorage2D(i.TEXTURE_2D,Me,xe,K.width,K.height)}O&&t.texSubImage2D(i.TEXTURE_2D,0,0,0,pe,Le,ee)}else t.texImage2D(i.TEXTURE_2D,0,xe,pe,Le,ee);f(_)&&S(k),_e.__version=fe.version,_.onUpdate&&_.onUpdate(_)}A.__version=_.version}function Oe(A,_,z){if(_.image.length!==6)return;let k=ot(A,_),Z=_.source;t.bindTexture(i.TEXTURE_CUBE_MAP,A.__webglTexture,i.TEXTURE0+z);let fe=n.get(Z);if(Z.version!==fe.__version||k===!0){t.activeTexture(i.TEXTURE0+z);let _e=st.getPrimaries(st.workingColorSpace),J=_.colorSpace===Qn?null:st.getPrimaries(_.colorSpace),ee=_.colorSpace===Qn||_e===J?i.NONE:i.BROWSER_DEFAULT_WEBGL;t.pixelStorei(i.UNPACK_FLIP_Y_WEBGL,_.flipY),t.pixelStorei(i.UNPACK_PREMULTIPLY_ALPHA_WEBGL,_.premultiplyAlpha),t.pixelStorei(i.UNPACK_ALIGNMENT,_.unpackAlignment),t.pixelStorei(i.UNPACK_COLORSPACE_CONVERSION_WEBGL,ee);let pe=_.isCompressedTexture||_.image[0].isCompressedTexture,Le=_.image[0]&&_.image[0].isDataTexture,xe=[];for(let se=0;se<6;se++)!pe&&!Le?xe[se]=m(_.image[se],!0,s.maxCubemapSize):xe[se]=Le?_.image[se].image:_.image[se],xe[se]=ct(_,xe[se]);let ge=xe[0],Pe=r.convert(_.format,_.colorSpace),ze=r.convert(_.type),Ze=M(_.internalFormat,Pe,ze,_.normalized,_.colorSpace),O=_.isVideoTexture!==!0,Me=fe.__version===void 0||k===!0,K=Z.dataReady,ce=w(_,ge);je(i.TEXTURE_CUBE_MAP,_);let Se;if(pe){O&&Me&&t.texStorage2D(i.TEXTURE_CUBE_MAP,ce,Ze,ge.width,ge.height);for(let se=0;se<6;se++){Se=xe[se].mipmaps;for(let De=0;De<Se.length;De++){let Ie=Se[De];_.format!==cn?Pe!==null?O?K&&t.compressedTexSubImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,De,0,0,Ie.width,Ie.height,Pe,Ie.data):t.compressedTexImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,De,Ze,Ie.width,Ie.height,0,Ie.data):ke("WebGLRenderer: Attempt to load unsupported compressed texture format in .setTextureCube()"):O?K&&t.texSubImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,De,0,0,Ie.width,Ie.height,Pe,ze,Ie.data):t.texImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,De,Ze,Ie.width,Ie.height,0,Pe,ze,Ie.data)}}}else{if(Se=_.mipmaps,O&&Me){Se.length>0&&ce++;let se=et(xe[0]);t.texStorage2D(i.TEXTURE_CUBE_MAP,ce,Ze,se.width,se.height)}for(let se=0;se<6;se++)if(Le){O?K&&t.texSubImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,0,0,0,xe[se].width,xe[se].height,Pe,ze,xe[se].data):t.texImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,0,Ze,xe[se].width,xe[se].height,0,Pe,ze,xe[se].data);for(let De=0;De<Se.length;De++){let ft=Se[De].image[se].image;O?K&&t.texSubImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,De+1,0,0,ft.width,ft.height,Pe,ze,ft.data):t.texImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,De+1,Ze,ft.width,ft.height,0,Pe,ze,ft.data)}}else{O?K&&t.texSubImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,0,0,0,Pe,ze,xe[se]):t.texImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,0,Ze,Pe,ze,xe[se]);for(let De=0;De<Se.length;De++){let Ie=Se[De];O?K&&t.texSubImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,De+1,0,0,Pe,ze,Ie.image[se]):t.texImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+se,De+1,Ze,Pe,ze,Ie.image[se])}}}f(_)&&S(i.TEXTURE_CUBE_MAP),fe.__version=Z.version,_.onUpdate&&_.onUpdate(_)}A.__version=_.version}function we(A,_,z,k,Z,fe){let _e=r.convert(z.format,z.colorSpace),J=r.convert(z.type),ee=M(z.internalFormat,_e,J,z.normalized,z.colorSpace),pe=n.get(_),Le=n.get(z);if(Le.__renderTarget=_,!pe.__hasExternalTextures){let xe=Math.max(1,_.width>>fe),ge=Math.max(1,_.height>>fe);Z===i.TEXTURE_3D||Z===i.TEXTURE_2D_ARRAY?t.texImage3D(Z,fe,ee,xe,ge,_.depth,0,_e,J,null):t.texImage2D(Z,fe,ee,xe,ge,0,_e,J,null)}t.bindFramebuffer(i.FRAMEBUFFER,A),qe(_)?o.framebufferTexture2DMultisampleEXT(i.FRAMEBUFFER,k,Z,Le.__webglTexture,0,Ge(_)):(Z===i.TEXTURE_2D||Z>=i.TEXTURE_CUBE_MAP_POSITIVE_X&&Z<=i.TEXTURE_CUBE_MAP_NEGATIVE_Z)&&i.framebufferTexture2D(i.FRAMEBUFFER,k,Z,Le.__webglTexture,fe),t.bindFramebuffer(i.FRAMEBUFFER,null)}function He(A,_,z){if(i.bindRenderbuffer(i.RENDERBUFFER,A),_.depthBuffer){let k=_.depthTexture,Z=k&&k.isDepthTexture?k.type:null,fe=T(_.stencilBuffer,Z),_e=_.stencilBuffer?i.DEPTH_STENCIL_ATTACHMENT:i.DEPTH_ATTACHMENT;qe(_)?o.renderbufferStorageMultisampleEXT(i.RENDERBUFFER,Ge(_),fe,_.width,_.height):z?i.renderbufferStorageMultisample(i.RENDERBUFFER,Ge(_),fe,_.width,_.height):i.renderbufferStorage(i.RENDERBUFFER,fe,_.width,_.height),i.framebufferRenderbuffer(i.FRAMEBUFFER,_e,i.RENDERBUFFER,A)}else{let k=_.textures;for(let Z=0;Z<k.length;Z++){let fe=k[Z],_e=r.convert(fe.format,fe.colorSpace),J=r.convert(fe.type),ee=M(fe.internalFormat,_e,J,fe.normalized,fe.colorSpace);qe(_)?o.renderbufferStorageMultisampleEXT(i.RENDERBUFFER,Ge(_),ee,_.width,_.height):z?i.renderbufferStorageMultisample(i.RENDERBUFFER,Ge(_),ee,_.width,_.height):i.renderbufferStorage(i.RENDERBUFFER,ee,_.width,_.height)}}i.bindRenderbuffer(i.RENDERBUFFER,null)}function rt(A,_,z){let k=_.isWebGLCubeRenderTarget===!0;if(t.bindFramebuffer(i.FRAMEBUFFER,A),!(_.depthTexture&&_.depthTexture.isDepthTexture))throw new Error("THREE.WebGLTextures: renderTarget.depthTexture must be an instance of THREE.DepthTexture.");let Z=n.get(_.depthTexture);if(Z.__renderTarget=_,(!Z.__webglTexture||_.depthTexture.image.width!==_.width||_.depthTexture.image.height!==_.height)&&(_.depthTexture.image.width=_.width,_.depthTexture.image.height=_.height,_.depthTexture.needsUpdate=!0),k){if(Z.__webglInit===void 0&&(Z.__webglInit=!0,_.depthTexture.addEventListener("dispose",P)),Z.__webglTexture===void 0){Z.__webglTexture=i.createTexture(),t.bindTexture(i.TEXTURE_CUBE_MAP,Z.__webglTexture),je(i.TEXTURE_CUBE_MAP,_.depthTexture);let pe=r.convert(_.depthTexture.format),Le=r.convert(_.depthTexture.type),xe;_.depthTexture.format===Cn?xe=i.DEPTH_COMPONENT24:_.depthTexture.format===mi&&(xe=i.DEPTH24_STENCIL8);for(let ge=0;ge<6;ge++)i.texImage2D(i.TEXTURE_CUBE_MAP_POSITIVE_X+ge,0,xe,_.width,_.height,0,pe,Le,null)}}else re(_.depthTexture,0);let fe=Z.__webglTexture,_e=Ge(_),J=k?i.TEXTURE_CUBE_MAP_POSITIVE_X+z:i.TEXTURE_2D,ee=_.depthTexture.format===mi?i.DEPTH_STENCIL_ATTACHMENT:i.DEPTH_ATTACHMENT;if(_.depthTexture.format===Cn)qe(_)?o.framebufferTexture2DMultisampleEXT(i.FRAMEBUFFER,ee,J,fe,0,_e):i.framebufferTexture2D(i.FRAMEBUFFER,ee,J,fe,0);else if(_.depthTexture.format===mi)qe(_)?o.framebufferTexture2DMultisampleEXT(i.FRAMEBUFFER,ee,J,fe,0,_e):i.framebufferTexture2D(i.FRAMEBUFFER,ee,J,fe,0);else throw new Error("THREE.WebGLTextures: Unknown depthTexture format.")}function te(A){let _=n.get(A),z=A.isWebGLCubeRenderTarget===!0;if(_.__boundDepthTexture!==A.depthTexture){let k=A.depthTexture;if(_.__depthDisposeCallback&&_.__depthDisposeCallback(),k){let Z=()=>{delete _.__boundDepthTexture,delete _.__depthDisposeCallback,k.removeEventListener("dispose",Z)};k.addEventListener("dispose",Z),_.__depthDisposeCallback=Z}_.__boundDepthTexture=k}if(A.depthTexture&&!_.__autoAllocateDepthBuffer)if(z)for(let k=0;k<6;k++)rt(_.__webglFramebuffer[k],A,k);else{let k=A.texture.mipmaps;k&&k.length>0?rt(_.__webglFramebuffer[0],A,0):rt(_.__webglFramebuffer,A,0)}else if(z){_.__webglDepthbuffer=[];for(let k=0;k<6;k++)if(t.bindFramebuffer(i.FRAMEBUFFER,_.__webglFramebuffer[k]),_.__webglDepthbuffer[k]===void 0)_.__webglDepthbuffer[k]=i.createRenderbuffer(),He(_.__webglDepthbuffer[k],A,!1);else{let Z=A.stencilBuffer?i.DEPTH_STENCIL_ATTACHMENT:i.DEPTH_ATTACHMENT,fe=_.__webglDepthbuffer[k];i.bindRenderbuffer(i.RENDERBUFFER,fe),i.framebufferRenderbuffer(i.FRAMEBUFFER,Z,i.RENDERBUFFER,fe)}}else{let k=A.texture.mipmaps;if(k&&k.length>0?t.bindFramebuffer(i.FRAMEBUFFER,_.__webglFramebuffer[0]):t.bindFramebuffer(i.FRAMEBUFFER,_.__webglFramebuffer),_.__webglDepthbuffer===void 0)_.__webglDepthbuffer=i.createRenderbuffer(),He(_.__webglDepthbuffer,A,!1);else{let Z=A.stencilBuffer?i.DEPTH_STENCIL_ATTACHMENT:i.DEPTH_ATTACHMENT,fe=_.__webglDepthbuffer;i.bindRenderbuffer(i.RENDERBUFFER,fe),i.framebufferRenderbuffer(i.FRAMEBUFFER,Z,i.RENDERBUFFER,fe)}}t.bindFramebuffer(i.FRAMEBUFFER,null)}function ae(A,_,z){let k=n.get(A);_!==void 0&&we(k.__webglFramebuffer,A,A.texture,i.COLOR_ATTACHMENT0,i.TEXTURE_2D,0),z!==void 0&&te(A)}function le(A){let _=A.texture,z=n.get(A),k=n.get(_);A.addEventListener("dispose",x);let Z=A.textures,fe=A.isWebGLCubeRenderTarget===!0,_e=Z.length>1;if(_e||(k.__webglTexture===void 0&&(k.__webglTexture=i.createTexture()),k.__version=_.version,a.memory.textures++),fe){z.__webglFramebuffer=[];for(let J=0;J<6;J++)if(_.mipmaps&&_.mipmaps.length>0){z.__webglFramebuffer[J]=[];for(let ee=0;ee<_.mipmaps.length;ee++)z.__webglFramebuffer[J][ee]=i.createFramebuffer()}else z.__webglFramebuffer[J]=i.createFramebuffer()}else{if(_.mipmaps&&_.mipmaps.length>0){z.__webglFramebuffer=[];for(let J=0;J<_.mipmaps.length;J++)z.__webglFramebuffer[J]=i.createFramebuffer()}else z.__webglFramebuffer=i.createFramebuffer();if(_e)for(let J=0,ee=Z.length;J<ee;J++){let pe=n.get(Z[J]);pe.__webglTexture===void 0&&(pe.__webglTexture=i.createTexture(),a.memory.textures++)}if(A.samples>0&&qe(A)===!1){z.__webglMultisampledFramebuffer=i.createFramebuffer(),z.__webglColorRenderbuffer=[],t.bindFramebuffer(i.FRAMEBUFFER,z.__webglMultisampledFramebuffer);for(let J=0;J<Z.length;J++){let ee=Z[J];z.__webglColorRenderbuffer[J]=i.createRenderbuffer(),i.bindRenderbuffer(i.RENDERBUFFER,z.__webglColorRenderbuffer[J]);let pe=r.convert(ee.format,ee.colorSpace),Le=r.convert(ee.type),xe=M(ee.internalFormat,pe,Le,ee.normalized,ee.colorSpace,A.isXRRenderTarget===!0),ge=Ge(A);i.renderbufferStorageMultisample(i.RENDERBUFFER,ge,xe,A.width,A.height),i.framebufferRenderbuffer(i.FRAMEBUFFER,i.COLOR_ATTACHMENT0+J,i.RENDERBUFFER,z.__webglColorRenderbuffer[J])}i.bindRenderbuffer(i.RENDERBUFFER,null),A.depthBuffer&&(z.__webglDepthRenderbuffer=i.createRenderbuffer(),He(z.__webglDepthRenderbuffer,A,!0)),t.bindFramebuffer(i.FRAMEBUFFER,null)}}if(fe){t.bindTexture(i.TEXTURE_CUBE_MAP,k.__webglTexture),je(i.TEXTURE_CUBE_MAP,_);for(let J=0;J<6;J++)if(_.mipmaps&&_.mipmaps.length>0)for(let ee=0;ee<_.mipmaps.length;ee++)we(z.__webglFramebuffer[J][ee],A,_,i.COLOR_ATTACHMENT0,i.TEXTURE_CUBE_MAP_POSITIVE_X+J,ee);else we(z.__webglFramebuffer[J],A,_,i.COLOR_ATTACHMENT0,i.TEXTURE_CUBE_MAP_POSITIVE_X+J,0);f(_)&&S(i.TEXTURE_CUBE_MAP),t.unbindTexture()}else if(_e){for(let J=0,ee=Z.length;J<ee;J++){let pe=Z[J],Le=n.get(pe),xe=i.TEXTURE_2D;(A.isWebGL3DRenderTarget||A.isWebGLArrayRenderTarget)&&(xe=A.isWebGL3DRenderTarget?i.TEXTURE_3D:i.TEXTURE_2D_ARRAY),t.bindTexture(xe,Le.__webglTexture),je(xe,pe),we(z.__webglFramebuffer,A,pe,i.COLOR_ATTACHMENT0+J,xe,0),f(pe)&&S(xe)}t.unbindTexture()}else{let J=i.TEXTURE_2D;if((A.isWebGL3DRenderTarget||A.isWebGLArrayRenderTarget)&&(J=A.isWebGL3DRenderTarget?i.TEXTURE_3D:i.TEXTURE_2D_ARRAY),t.bindTexture(J,k.__webglTexture),je(J,_),_.mipmaps&&_.mipmaps.length>0)for(let ee=0;ee<_.mipmaps.length;ee++)we(z.__webglFramebuffer[ee],A,_,i.COLOR_ATTACHMENT0,J,ee);else we(z.__webglFramebuffer,A,_,i.COLOR_ATTACHMENT0,J,0);f(_)&&S(J),t.unbindTexture()}A.depthBuffer&&te(A)}function oe(A){let _=A.textures;for(let z=0,k=_.length;z<k;z++){let Z=_[z];if(f(Z)){let fe=R(A),_e=n.get(Z).__webglTexture;t.bindTexture(fe,_e),S(fe),t.unbindTexture()}}}let ue=[],Ue=[];function Ne(A){if(A.samples>0){if(qe(A)===!1){let _=A.textures,z=A.width,k=A.height,Z=i.COLOR_BUFFER_BIT,fe=A.stencilBuffer?i.DEPTH_STENCIL_ATTACHMENT:i.DEPTH_ATTACHMENT,_e=n.get(A),J=_.length>1;if(J)for(let pe=0;pe<_.length;pe++)t.bindFramebuffer(i.FRAMEBUFFER,_e.__webglMultisampledFramebuffer),i.framebufferRenderbuffer(i.FRAMEBUFFER,i.COLOR_ATTACHMENT0+pe,i.RENDERBUFFER,null),t.bindFramebuffer(i.FRAMEBUFFER,_e.__webglFramebuffer),i.framebufferTexture2D(i.DRAW_FRAMEBUFFER,i.COLOR_ATTACHMENT0+pe,i.TEXTURE_2D,null,0);t.bindFramebuffer(i.READ_FRAMEBUFFER,_e.__webglMultisampledFramebuffer);let ee=A.texture.mipmaps;ee&&ee.length>0?t.bindFramebuffer(i.DRAW_FRAMEBUFFER,_e.__webglFramebuffer[0]):t.bindFramebuffer(i.DRAW_FRAMEBUFFER,_e.__webglFramebuffer);for(let pe=0;pe<_.length;pe++){if(A.resolveDepthBuffer&&(A.depthBuffer&&(Z|=i.DEPTH_BUFFER_BIT),A.stencilBuffer&&A.resolveStencilBuffer&&(Z|=i.STENCIL_BUFFER_BIT)),J){i.framebufferRenderbuffer(i.READ_FRAMEBUFFER,i.COLOR_ATTACHMENT0,i.RENDERBUFFER,_e.__webglColorRenderbuffer[pe]);let Le=n.get(_[pe]).__webglTexture;i.framebufferTexture2D(i.DRAW_FRAMEBUFFER,i.COLOR_ATTACHMENT0,i.TEXTURE_2D,Le,0)}i.blitFramebuffer(0,0,z,k,0,0,z,k,Z,i.NEAREST),c===!0&&(ue.length=0,Ue.length=0,ue.push(i.COLOR_ATTACHMENT0+pe),A.depthBuffer&&A.storeMultisampledDepthBuffer===!1&&(ue.push(fe),Ue.push(fe),i.invalidateFramebuffer(i.DRAW_FRAMEBUFFER,Ue)),i.invalidateFramebuffer(i.READ_FRAMEBUFFER,ue))}if(t.bindFramebuffer(i.READ_FRAMEBUFFER,null),t.bindFramebuffer(i.DRAW_FRAMEBUFFER,null),J)for(let pe=0;pe<_.length;pe++){t.bindFramebuffer(i.FRAMEBUFFER,_e.__webglMultisampledFramebuffer),i.framebufferRenderbuffer(i.FRAMEBUFFER,i.COLOR_ATTACHMENT0+pe,i.RENDERBUFFER,_e.__webglColorRenderbuffer[pe]);let Le=n.get(_[pe]).__webglTexture;t.bindFramebuffer(i.FRAMEBUFFER,_e.__webglFramebuffer),i.framebufferTexture2D(i.DRAW_FRAMEBUFFER,i.COLOR_ATTACHMENT0+pe,i.TEXTURE_2D,Le,0)}t.bindFramebuffer(i.DRAW_FRAMEBUFFER,_e.__webglMultisampledFramebuffer)}else if(A.depthBuffer&&A.storeMultisampledDepthBuffer===!1&&c){let _=A.stencilBuffer?i.DEPTH_STENCIL_ATTACHMENT:i.DEPTH_ATTACHMENT;i.invalidateFramebuffer(i.DRAW_FRAMEBUFFER,[_])}}}function Ge(A){return Math.min(s.maxSamples,A.samples)}function qe(A){let _=n.get(A);return A.samples>0&&e.has("WEBGL_multisampled_render_to_texture")===!0&&_.__useRenderToTexture!==!1}function D(A){let _=a.render.frame;h.get(A)!==_&&(h.set(A,_),A.update())}function ct(A,_){let z=A.colorSpace,k=A.format,Z=A.type;return A.isCompressedTexture===!0||A.isVideoTexture===!0||z!==tr&&z!==Qn&&(st.getTransfer(z)===dt?(k!==cn||Z!==$t)&&ke("WebGLTextures: sRGB encoded textures have to use RGBAFormat and UnsignedByteType."):We("WebGLTextures: Unsupported texture color space:",z)),_}function et(A){return typeof HTMLImageElement<"u"&&A instanceof HTMLImageElement?(l.width=A.naturalWidth||A.width,l.height=A.naturalHeight||A.height):typeof VideoFrame<"u"&&A instanceof VideoFrame?(l.width=A.displayWidth,l.height=A.displayHeight):(l.width=A.width,l.height=A.height),l}this.allocateTextureUnit=X,this.resetTextureUnits=H,this.getTextureUnits=N,this.setTextureUnits=V,this.setTexture2D=re,this.setTexture2DArray=Y,this.setTexture3D=Q,this.setTextureCube=j,this.rebindTextures=ae,this.setupRenderTarget=le,this.updateRenderTargetMipmap=oe,this.updateMultisampleRenderTarget=Ne,this.setupDepthRenderbuffer=te,this.setupFrameBufferTexture=we,this.useMultisampledRTT=qe,this.isReversedDepthBuffer=function(){return t.buffers.depth.getReversed()}}function d_(i,e){function t(n,s=Qn){let r,a=st.getTransfer(s);if(n===$t)return i.UNSIGNED_BYTE;if(n===_o)return i.UNSIGNED_SHORT_4_4_4_4;if(n===xo)return i.UNSIGNED_SHORT_5_5_5_1;if(n===dc)return i.UNSIGNED_INT_5_9_9_9_REV;if(n===fc)return i.UNSIGNED_INT_10F_11F_11F_REV;if(n===hc)return i.BYTE;if(n===uc)return i.SHORT;if(n===Is)return i.UNSIGNED_SHORT;if(n===go)return i.INT;if(n===Mn)return i.UNSIGNED_INT;if(n===ln)return i.FLOAT;if(n===Sn)return i.HALF_FLOAT;if(n===pc)return i.ALPHA;if(n===mc)return i.RGB;if(n===cn)return i.RGBA;if(n===Cn)return i.DEPTH_COMPONENT;if(n===mi)return i.DEPTH_STENCIL;if(n===yo)return i.RED;if(n===vo)return i.RED_INTEGER;if(n===gi)return i.RG;if(n===Mo)return i.RG_INTEGER;if(n===So)return i.RGBA_INTEGER;if(n===Br||n===zr||n===Hr||n===Vr)if(a===dt)if(r=e.get("WEBGL_compressed_texture_s3tc_srgb"),r!==null){if(n===Br)return r.COMPRESSED_SRGB_S3TC_DXT1_EXT;if(n===zr)return r.COMPRESSED_SRGB_ALPHA_S3TC_DXT1_EXT;if(n===Hr)return r.COMPRESSED_SRGB_ALPHA_S3TC_DXT3_EXT;if(n===Vr)return r.COMPRESSED_SRGB_ALPHA_S3TC_DXT5_EXT}else return null;else if(r=e.get("WEBGL_compressed_texture_s3tc"),r!==null){if(n===Br)return r.COMPRESSED_RGB_S3TC_DXT1_EXT;if(n===zr)return r.COMPRESSED_RGBA_S3TC_DXT1_EXT;if(n===Hr)return r.COMPRESSED_RGBA_S3TC_DXT3_EXT;if(n===Vr)return r.COMPRESSED_RGBA_S3TC_DXT5_EXT}else return null;if(n===bo||n===Eo||n===To||n===wo)if(r=e.get("WEBGL_compressed_texture_pvrtc"),r!==null){if(n===bo)return r.COMPRESSED_RGB_PVRTC_4BPPV1_IMG;if(n===Eo)return r.COMPRESSED_RGB_PVRTC_2BPPV1_IMG;if(n===To)return r.COMPRESSED_RGBA_PVRTC_4BPPV1_IMG;if(n===wo)return r.COMPRESSED_RGBA_PVRTC_2BPPV1_IMG}else return null;if(n===Ao||n===Ro||n===Co||n===Io||n===Po||n===kr||n===Lo)if(r=e.get("WEBGL_compressed_texture_etc"),r!==null){if(n===Ao||n===Ro)return a===dt?r.COMPRESSED_SRGB8_ETC2:r.COMPRESSED_RGB8_ETC2;if(n===Co)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ETC2_EAC:r.COMPRESSED_RGBA8_ETC2_EAC;if(n===Io)return r.COMPRESSED_R11_EAC;if(n===Po)return r.COMPRESSED_SIGNED_R11_EAC;if(n===kr)return r.COMPRESSED_RG11_EAC;if(n===Lo)return r.COMPRESSED_SIGNED_RG11_EAC}else return null;if(n===Do||n===Uo||n===No||n===Fo||n===Oo||n===Bo||n===zo||n===Ho||n===Vo||n===ko||n===Go||n===Wo||n===Xo||n===qo)if(r=e.get("WEBGL_compressed_texture_astc"),r!==null){if(n===Do)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_4x4_KHR:r.COMPRESSED_RGBA_ASTC_4x4_KHR;if(n===Uo)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_5x4_KHR:r.COMPRESSED_RGBA_ASTC_5x4_KHR;if(n===No)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_5x5_KHR:r.COMPRESSED_RGBA_ASTC_5x5_KHR;if(n===Fo)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_6x5_KHR:r.COMPRESSED_RGBA_ASTC_6x5_KHR;if(n===Oo)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_6x6_KHR:r.COMPRESSED_RGBA_ASTC_6x6_KHR;if(n===Bo)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_8x5_KHR:r.COMPRESSED_RGBA_ASTC_8x5_KHR;if(n===zo)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_8x6_KHR:r.COMPRESSED_RGBA_ASTC_8x6_KHR;if(n===Ho)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_8x8_KHR:r.COMPRESSED_RGBA_ASTC_8x8_KHR;if(n===Vo)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_10x5_KHR:r.COMPRESSED_RGBA_ASTC_10x5_KHR;if(n===ko)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_10x6_KHR:r.COMPRESSED_RGBA_ASTC_10x6_KHR;if(n===Go)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_10x8_KHR:r.COMPRESSED_RGBA_ASTC_10x8_KHR;if(n===Wo)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_10x10_KHR:r.COMPRESSED_RGBA_ASTC_10x10_KHR;if(n===Xo)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_12x10_KHR:r.COMPRESSED_RGBA_ASTC_12x10_KHR;if(n===qo)return a===dt?r.COMPRESSED_SRGB8_ALPHA8_ASTC_12x12_KHR:r.COMPRESSED_RGBA_ASTC_12x12_KHR}else return null;if(n===Yo||n===Zo||n===Jo)if(r=e.get("EXT_texture_compression_bptc"),r!==null){if(n===Yo)return a===dt?r.COMPRESSED_SRGB_ALPHA_BPTC_UNORM_EXT:r.COMPRESSED_RGBA_BPTC_UNORM_EXT;if(n===Zo)return r.COMPRESSED_RGB_BPTC_SIGNED_FLOAT_EXT;if(n===Jo)return r.COMPRESSED_RGB_BPTC_UNSIGNED_FLOAT_EXT}else return null;if(n===$o||n===Ko||n===Gr||n===Qo)if(r=e.get("EXT_texture_compression_rgtc"),r!==null){if(n===$o)return r.COMPRESSED_RED_RGTC1_EXT;if(n===Ko)return r.COMPRESSED_SIGNED_RED_RGTC1_EXT;if(n===Gr)return r.COMPRESSED_RED_GREEN_RGTC2_EXT;if(n===Qo)return r.COMPRESSED_SIGNED_RED_GREEN_RGTC2_EXT}else return null;return n===Ps?i.UNSIGNED_INT_24_8:i[n]!==void 0?i[n]:null}return{convert:t}}var f_=`
void main() {

	gl_Position = vec4( position, 1.0 );

}`,p_=`
uniform sampler2DArray depthColor;
uniform float depthWidth;
uniform float depthHeight;

void main() {

	vec2 coord = vec2( gl_FragCoord.x / depthWidth, gl_FragCoord.y / depthHeight );

	if ( coord.x >= 1.0 ) {

		gl_FragDepth = texture( depthColor, vec3( coord.x - 1.0, coord.y, 1 ) ).r;

	} else {

		gl_FragDepth = texture( depthColor, vec3( coord.x, coord.y, 0 ) ).r;

	}

}`,Oc=class{constructor(){this.texture=null,this.mesh=null,this.depthNear=0,this.depthFar=0}init(e,t){if(this.texture===null){let n=new pr(e.texture);(e.depthNear!==t.depthNear||e.depthFar!==t.depthFar)&&(this.depthNear=e.depthNear,this.depthFar=e.depthFar),this.texture=n}}getMesh(e){if(this.texture!==null&&this.mesh===null){let t=e.cameras[0].viewport,n=new en({vertexShader:f_,fragmentShader:p_,uniforms:{depthColor:{value:this.texture},depthWidth:{value:t.z},depthHeight:{value:t.w}}});this.mesh=new Ke(new Kn(20,20),n)}return this.mesh}reset(){this.texture=null,this.mesh=null}getDepthTexture(){return this.texture}},Bc=class extends In{constructor(e,t){super();let n=this,s=null,r=1,a=null,o="local-floor",c=1,l=null,h=null,d=null,u=null,p=null,g=null,v=typeof XRWebGLBinding<"u",m=new Oc,f={},S=t.getContextAttributes(),R=null,M=null,T=[],w=[],P=new he,x=null,E=null,C=new Ft;C.viewport=new vt;let U=new Ft;U.viewport=new vt;let B=[C,U],H=new uo,N=null,V=null;this.cameraAutoUpdate=!0,this.enabled=!1,this.isPresenting=!1,this.getController=function(q){let ne=T[q];return ne===void 0&&(ne=new ms,T[q]=ne),ne.getTargetRaySpace()},this.getControllerGrip=function(q){let ne=T[q];return ne===void 0&&(ne=new ms,T[q]=ne),ne.getGripSpace()},this.getHand=function(q){let ne=T[q];return ne===void 0&&(ne=new ms,T[q]=ne),ne.getHandSpace()};function X(q){let ne=w.indexOf(q.inputSource);if(ne===-1)return;let ve=T[ne];ve!==void 0&&(ve.update(q.inputSource,q.frame,l||a),ve.dispatchEvent({type:q.type,data:q.inputSource}))}function W(){s.removeEventListener("select",X),s.removeEventListener("selectstart",X),s.removeEventListener("selectend",X),s.removeEventListener("squeeze",X),s.removeEventListener("squeezestart",X),s.removeEventListener("squeezeend",X),s.removeEventListener("end",W),s.removeEventListener("inputsourceschange",re);for(let q=0;q<T.length;q++){let ne=w[q];ne!==null&&(w[q]=null,T[q].disconnect(ne))}N=null,V=null,m.reset();for(let q in f)delete f[q];if(e.setRenderTarget(R),p=null,u=null,d=null,s=null,M=null,ot.stop(),n.isPresenting=!1,e.setPixelRatio(x),e.setSize(P.width,P.height,!1),E!==null){let q=E.camera;q.fov=E.fov,q.zoom=E.zoom,q.updateProjectionMatrix(),E=null}n.dispatchEvent({type:"sessionend"})}this.setFramebufferScaleFactor=function(q){r=q,n.isPresenting===!0&&ke("WebXRManager: Cannot change framebuffer scale while presenting.")},this.setReferenceSpaceType=function(q){o=q,n.isPresenting===!0&&ke("WebXRManager: Cannot change reference space type while presenting.")},this.getReferenceSpace=function(){return l||a},this.setReferenceSpace=function(q){l=q},this.getBaseLayer=function(){return u!==null?u:p},this.getBinding=function(){return d===null&&v&&(d=new XRWebGLBinding(s,t)),d},this.getFrame=function(){return g},this.getSession=function(){return s},this.setSession=async function(q){if(s=q,s!==null){if(R=e.getRenderTarget(),s.addEventListener("select",X),s.addEventListener("selectstart",X),s.addEventListener("selectend",X),s.addEventListener("squeeze",X),s.addEventListener("squeezestart",X),s.addEventListener("squeezeend",X),s.addEventListener("end",W),s.addEventListener("inputsourceschange",re),S.xrCompatible!==!0&&await t.makeXRCompatible(),x=e.getPixelRatio(),e.getSize(P),v&&"createProjectionLayer"in XRWebGLBinding.prototype){let ve=null,Oe=null,we=null;S.depth&&(we=S.stencil?t.DEPTH24_STENCIL8:t.DEPTH_COMPONENT24,ve=S.stencil?mi:Cn,Oe=S.stencil?Ps:Mn);let He={colorFormat:t.RGBA8,depthFormat:we,scaleFactor:r};d=this.getBinding(),u=d.createProjectionLayer(He),s.updateRenderState({layers:[u]}),e.setPixelRatio(1),e.setSize(u.textureWidth,u.textureHeight,!1),M=new Jt(u.textureWidth,u.textureHeight,{format:cn,type:$t,depthTexture:new oi(u.textureWidth,u.textureHeight,Oe,void 0,void 0,void 0,void 0,void 0,void 0,ve),stencilBuffer:S.stencil,colorSpace:e.outputColorSpace,samples:S.antialias?4:0,resolveDepthBuffer:u.ignoreDepthValues===!1,resolveStencilBuffer:u.ignoreDepthValues===!1,storeMultisampledDepthBuffer:u.ignoreDepthValues===!1,storeMultisampledStencilBuffer:u.ignoreDepthValues===!1})}else{let ve={antialias:S.antialias,alpha:!0,depth:S.depth,stencil:S.stencil,framebufferScaleFactor:r};p=new XRWebGLLayer(s,t,ve),s.updateRenderState({baseLayer:p}),e.setPixelRatio(1),e.setSize(p.framebufferWidth,p.framebufferHeight,!1),M=new Jt(p.framebufferWidth,p.framebufferHeight,{format:cn,type:$t,colorSpace:e.outputColorSpace,stencilBuffer:S.stencil,resolveDepthBuffer:p.ignoreDepthValues===!1,resolveStencilBuffer:p.ignoreDepthValues===!1,storeMultisampledDepthBuffer:p.ignoreDepthValues===!1,storeMultisampledStencilBuffer:p.ignoreDepthValues===!1})}M.isXRRenderTarget=!0,this.setFoveation(c),l=null,a=await s.requestReferenceSpace(o),ot.setContext(s),ot.start(),n.isPresenting=!0,n.dispatchEvent({type:"sessionstart"})}},this.getEnvironmentBlendMode=function(){if(s!==null)return s.environmentBlendMode},this.getDepthTexture=function(){return m.getDepthTexture()};function re(q){for(let ne=0;ne<q.removed.length;ne++){let ve=q.removed[ne],Oe=w.indexOf(ve);Oe>=0&&(w[Oe]=null,T[Oe].disconnect(ve))}for(let ne=0;ne<q.added.length;ne++){let ve=q.added[ne],Oe=w.indexOf(ve);if(Oe===-1){for(let He=0;He<T.length;He++)if(He>=w.length){w.push(ve),Oe=He;break}else if(w[He]===null){w[He]=ve,Oe=He;break}if(Oe===-1)break}let we=T[Oe];we&&we.connect(ve)}}let Y=new L,Q=new L;function j(q,ne,ve){Y.setFromMatrixPosition(ne.matrixWorld),Q.setFromMatrixPosition(ve.matrixWorld);let Oe=Y.distanceTo(Q),we=ne.projectionMatrix.elements,He=ve.projectionMatrix.elements,rt=we[14]/(we[10]-1),te=we[14]/(we[10]+1),ae=(we[9]+1)/we[5],le=(we[9]-1)/we[5],oe=(we[8]-1)/we[0],ue=(He[8]+1)/He[0],Ue=rt*oe,Ne=rt*ue,Ge=Oe/(-oe+ue),qe=Ge*-oe;if(ne.matrixWorld.decompose(q.position,q.quaternion,q.scale),q.translateX(qe),q.translateZ(Ge),q.matrixWorld.compose(q.position,q.quaternion,q.scale),q.matrixWorldInverse.copy(q.matrixWorld).invert(),we[10]===-1)q.projectionMatrix.copy(ne.projectionMatrix),q.projectionMatrixInverse.copy(ne.projectionMatrixInverse);else{let D=rt+Ge,ct=te+Ge,et=Ue-qe,A=Ne+(Oe-qe),_=ae*te/ct*D,z=le*te/ct*D;q.projectionMatrix.makePerspective(et,A,_,z,D,ct),q.projectionMatrixInverse.copy(q.projectionMatrix).invert()}}function Re(q,ne){ne===null?q.matrixWorld.copy(q.matrix):q.matrixWorld.multiplyMatrices(ne.matrixWorld,q.matrix),q.matrixWorldInverse.copy(q.matrixWorld).invert()}this.updateCamera=function(q){if(s===null)return;let ne=q.near,ve=q.far;m.texture!==null&&(m.depthNear>0&&(ne=m.depthNear),m.depthFar>0&&(ve=m.depthFar)),H.near=U.near=C.near=ne,H.far=U.far=C.far=ve,(N!==H.near||V!==H.far)&&(s.updateRenderState({depthNear:H.near,depthFar:H.far}),N=H.near,V=H.far),H.layers.mask=q.layers.mask|6,C.layers.mask=H.layers.mask&-5,U.layers.mask=H.layers.mask&-3;let Oe=q.parent,we=H.cameras;Re(H,Oe);for(let He=0;He<we.length;He++)Re(we[He],Oe);we.length===2?j(H,C,U):H.projectionMatrix.copy(C.projectionMatrix),E===null&&q.isPerspectiveCamera&&(E={camera:q,fov:q.fov,zoom:q.zoom}),Ce(q,H,Oe)};function Ce(q,ne,ve){ve===null?q.matrix.copy(ne.matrixWorld):(q.matrix.copy(ve.matrixWorld),q.matrix.invert(),q.matrix.multiply(ne.matrixWorld)),q.matrix.decompose(q.position,q.quaternion,q.scale),q.updateMatrixWorld(!0),q.projectionMatrix.copy(ne.projectionMatrix),q.projectionMatrixInverse.copy(ne.projectionMatrixInverse),q.isPerspectiveCamera&&(q.fov=ds*2*Math.atan(1/q.projectionMatrix.elements[5]),q.zoom=1)}this.getCamera=function(){return H},this.getFoveation=function(){if(!(u===null&&p===null))return c},this.setFoveation=function(q){c=q,u!==null&&(u.fixedFoveation=q),p!==null&&p.fixedFoveation!==void 0&&(p.fixedFoveation=q)},this.hasDepthSensing=function(){return m.texture!==null},this.getDepthSensingMesh=function(){return m.getMesh(H)},this.getCameraTexture=function(q){return f[q]};let lt=null;function je(q,ne){if(h=ne.getViewerPose(l||a),g=ne,h!==null){let ve=h.views;p!==null&&(e.setRenderTargetFramebuffer(M,p.framebuffer),e.setRenderTarget(M));let Oe=!1;ve.length!==H.cameras.length&&(H.cameras.length=0,Oe=!0);for(let te=0;te<ve.length;te++){let ae=ve[te],le=null;if(p!==null)le=p.getViewport(ae);else{let ue=d.getViewSubImage(u,ae);le=ue.viewport,te===0&&(e.setRenderTargetTextures(M,ue.colorTexture,ue.depthStencilTexture),e.setRenderTarget(M))}let oe=B[te];oe===void 0&&(oe=new Ft,oe.layers.enable(te),oe.viewport=new vt,B[te]=oe),oe.matrix.fromArray(ae.transform.matrix),oe.matrix.decompose(oe.position,oe.quaternion,oe.scale),oe.projectionMatrix.fromArray(ae.projectionMatrix),oe.projectionMatrixInverse.copy(oe.projectionMatrix).invert(),oe.viewport.set(le.x,le.y,le.width,le.height),te===0&&(H.matrix.copy(oe.matrix),H.matrix.decompose(H.position,H.quaternion,H.scale)),Oe===!0&&H.cameras.push(oe)}let we=s.enabledFeatures;if(we&&we.includes("depth-sensing")&&s.depthUsage=="gpu-optimized"&&v){d=n.getBinding();let te=d.getDepthInformation(ve[0]);te&&te.isValid&&te.texture&&m.init(te,s.renderState)}if(we&&we.includes("camera-access")&&v){e.state.unbindTexture(),d=n.getBinding();for(let te=0;te<ve.length;te++){let ae=ve[te].camera;if(ae){let le=f[ae];le||(le=new pr,f[ae]=le);let oe=d.getCameraImage(ae);le.sourceTexture=oe}}}}for(let ve=0;ve<T.length;ve++){let Oe=w[ve],we=T[ve];Oe!==null&&we!==void 0&&we.update(Oe,ne,l||a)}lt&&lt(q,ne),ne.detectedPlanes&&n.dispatchEvent({type:"planesdetected",data:ne}),g=null}let ot=new Yu;ot.setAnimationLoop(je),this.setAnimationLoop=function(q){lt=q},this.dispose=function(){}}},m_=new ut,ju=new Je;ju.set(-1,0,0,0,1,0,0,0,1);function g_(i,e){function t(m,f){m.matrixAutoUpdate===!0&&m.updateMatrix(),f.value.copy(m.matrix)}function n(m,f){f.color.getRGB(m.fogColor.value,vc(i)),f.isFog?(m.fogNear.value=f.near,m.fogFar.value=f.far):f.isFogExp2&&(m.fogDensity.value=f.density)}function s(m,f,S,R,M){f.isNodeMaterial?f.uniformsNeedUpdate=!1:f.isMeshBasicMaterial?r(m,f):f.isMeshLambertMaterial?(r(m,f),f.envMap&&(m.envMapIntensity.value=f.envMapIntensity)):f.isMeshToonMaterial?(r(m,f),d(m,f)):f.isMeshPhongMaterial?(r(m,f),h(m,f),f.envMap&&(m.envMapIntensity.value=f.envMapIntensity)):f.isMeshStandardMaterial?(r(m,f),u(m,f),f.isMeshPhysicalMaterial&&p(m,f,M)):f.isMeshMatcapMaterial?(r(m,f),g(m,f)):f.isMeshDepthMaterial?r(m,f):f.isMeshDistanceMaterial?(r(m,f),v(m,f)):f.isMeshNormalMaterial?r(m,f):f.isLineBasicMaterial?(a(m,f),f.isLineDashedMaterial&&o(m,f)):f.isPointsMaterial?c(m,f,S,R):f.isSpriteMaterial?l(m,f):f.isShadowMaterial?(m.color.value.copy(f.color),m.opacity.value=f.opacity):f.isShaderMaterial&&(f.uniformsNeedUpdate=!1)}function r(m,f){m.opacity.value=f.opacity,f.color&&m.diffuse.value.copy(f.color),f.emissive&&m.emissive.value.copy(f.emissive).multiplyScalar(f.emissiveIntensity),f.map&&(m.map.value=f.map,t(f.map,m.mapTransform)),f.alphaMap&&(m.alphaMap.value=f.alphaMap,t(f.alphaMap,m.alphaMapTransform)),f.bumpMap&&(m.bumpMap.value=f.bumpMap,t(f.bumpMap,m.bumpMapTransform),m.bumpScale.value=f.bumpScale,f.side===Ut&&(m.bumpScale.value*=-1)),f.normalMap&&(m.normalMap.value=f.normalMap,t(f.normalMap,m.normalMapTransform),m.normalScale.value.copy(f.normalScale),f.side===Ut&&m.normalScale.value.negate()),f.displacementMap&&(m.displacementMap.value=f.displacementMap,t(f.displacementMap,m.displacementMapTransform),m.displacementScale.value=f.displacementScale,m.displacementBias.value=f.displacementBias),f.emissiveMap&&(m.emissiveMap.value=f.emissiveMap,t(f.emissiveMap,m.emissiveMapTransform)),f.specularMap&&(m.specularMap.value=f.specularMap,t(f.specularMap,m.specularMapTransform)),f.alphaTest>0&&(m.alphaTest.value=f.alphaTest);let S=e.get(f),R=S.envMap,M=S.envMapRotation;R&&(m.envMap.value=R,m.envMapRotation.value.setFromMatrix4(m_.makeRotationFromEuler(M)).transpose(),R.isCubeTexture&&R.isRenderTargetTexture===!1&&m.envMapRotation.value.premultiply(ju),m.reflectivity.value=f.reflectivity,m.ior.value=f.ior,m.refractionRatio.value=f.refractionRatio),f.lightMap&&(m.lightMap.value=f.lightMap,m.lightMapIntensity.value=f.lightMapIntensity,t(f.lightMap,m.lightMapTransform)),f.aoMap&&(m.aoMap.value=f.aoMap,m.aoMapIntensity.value=f.aoMapIntensity,t(f.aoMap,m.aoMapTransform))}function a(m,f){m.diffuse.value.copy(f.color),m.opacity.value=f.opacity,f.map&&(m.map.value=f.map,t(f.map,m.mapTransform))}function o(m,f){m.dashSize.value=f.dashSize,m.totalSize.value=f.dashSize+f.gapSize,m.scale.value=f.scale}function c(m,f,S,R){m.diffuse.value.copy(f.color),m.opacity.value=f.opacity,m.size.value=f.size*S,m.scale.value=R*.5,f.map&&(m.map.value=f.map,t(f.map,m.uvTransform)),f.alphaMap&&(m.alphaMap.value=f.alphaMap,t(f.alphaMap,m.alphaMapTransform)),f.alphaTest>0&&(m.alphaTest.value=f.alphaTest)}function l(m,f){m.diffuse.value.copy(f.color),m.opacity.value=f.opacity,m.rotation.value=f.rotation,f.map&&(m.map.value=f.map,t(f.map,m.mapTransform)),f.alphaMap&&(m.alphaMap.value=f.alphaMap,t(f.alphaMap,m.alphaMapTransform)),f.alphaTest>0&&(m.alphaTest.value=f.alphaTest)}function h(m,f){m.specular.value.copy(f.specular),m.shininess.value=Math.max(f.shininess,1e-4)}function d(m,f){f.gradientMap&&(m.gradientMap.value=f.gradientMap)}function u(m,f){m.metalness.value=f.metalness,f.metalnessMap&&(m.metalnessMap.value=f.metalnessMap,t(f.metalnessMap,m.metalnessMapTransform)),m.roughness.value=f.roughness,f.roughnessMap&&(m.roughnessMap.value=f.roughnessMap,t(f.roughnessMap,m.roughnessMapTransform)),f.envMap&&(m.envMapIntensity.value=f.envMapIntensity)}function p(m,f,S){m.ior.value=f.ior,f.sheen>0&&(m.sheenColor.value.copy(f.sheenColor).multiplyScalar(f.sheen),m.sheenRoughness.value=f.sheenRoughness,f.sheenColorMap&&(m.sheenColorMap.value=f.sheenColorMap,t(f.sheenColorMap,m.sheenColorMapTransform)),f.sheenRoughnessMap&&(m.sheenRoughnessMap.value=f.sheenRoughnessMap,t(f.sheenRoughnessMap,m.sheenRoughnessMapTransform))),f.clearcoat>0&&(m.clearcoat.value=f.clearcoat,m.clearcoatRoughness.value=f.clearcoatRoughness,f.clearcoatMap&&(m.clearcoatMap.value=f.clearcoatMap,t(f.clearcoatMap,m.clearcoatMapTransform)),f.clearcoatRoughnessMap&&(m.clearcoatRoughnessMap.value=f.clearcoatRoughnessMap,t(f.clearcoatRoughnessMap,m.clearcoatRoughnessMapTransform)),f.clearcoatNormalMap&&(m.clearcoatNormalMap.value=f.clearcoatNormalMap,t(f.clearcoatNormalMap,m.clearcoatNormalMapTransform),m.clearcoatNormalScale.value.copy(f.clearcoatNormalScale),f.side===Ut&&m.clearcoatNormalScale.value.negate())),f.dispersion>0&&(m.dispersion.value=f.dispersion),f.retroreflectivity>0&&(m.retroreflectivity.value=f.retroreflectivity),f.iridescence>0&&(m.iridescence.value=f.iridescence,m.iridescenceIOR.value=f.iridescenceIOR,m.iridescenceThicknessMinimum.value=f.iridescenceThicknessRange[0],m.iridescenceThicknessMaximum.value=f.iridescenceThicknessRange[1],f.iridescenceMap&&(m.iridescenceMap.value=f.iridescenceMap,t(f.iridescenceMap,m.iridescenceMapTransform)),f.iridescenceThicknessMap&&(m.iridescenceThicknessMap.value=f.iridescenceThicknessMap,t(f.iridescenceThicknessMap,m.iridescenceThicknessMapTransform))),f.transmission>0&&(m.transmission.value=f.transmission,m.transmissionSamplerMap.value=S.texture,m.transmissionSamplerSize.value.set(S.width,S.height),f.transmissionMap&&(m.transmissionMap.value=f.transmissionMap,t(f.transmissionMap,m.transmissionMapTransform)),m.thickness.value=f.thickness,f.thicknessMap&&(m.thicknessMap.value=f.thicknessMap,t(f.thicknessMap,m.thicknessMapTransform)),m.attenuationDistance.value=f.attenuationDistance,m.attenuationColor.value.copy(f.attenuationColor)),f.anisotropy>0&&(m.anisotropyVector.value.set(f.anisotropy*Math.cos(f.anisotropyRotation),f.anisotropy*Math.sin(f.anisotropyRotation)),f.anisotropyMap&&(m.anisotropyMap.value=f.anisotropyMap,t(f.anisotropyMap,m.anisotropyMapTransform))),m.specularIntensity.value=f.specularIntensity,m.specularColor.value.copy(f.specularColor),f.specularColorMap&&(m.specularColorMap.value=f.specularColorMap,t(f.specularColorMap,m.specularColorMapTransform)),f.specularIntensityMap&&(m.specularIntensityMap.value=f.specularIntensityMap,t(f.specularIntensityMap,m.specularIntensityMapTransform))}function g(m,f){f.matcap&&(m.matcap.value=f.matcap)}function v(m,f){let S=e.get(f).light;m.referencePosition.value.setFromMatrixPosition(S.matrixWorld),m.nearDistance.value=S.shadow.camera.near,m.farDistance.value=S.shadow.camera.far}return{refreshFogUniforms:n,refreshMaterialUniforms:s}}function __(i,e,t,n){let s={},r={},a=[],o=i.getParameter(i.MAX_UNIFORM_BUFFER_BINDINGS);function c(M,T){let w=T.program;n.uniformBlockBinding(M,w)}function l(M,T){let w=s[M.id];w===void 0&&(m(M),w=h(M),s[M.id]=w,M.addEventListener("dispose",S));let P=T.program;n.updateUBOMapping(M,P);let x=e.render.frame;r[M.id]!==x&&(u(M),r[M.id]=x)}function h(M){let T=d();M.__bindingPointIndex=T;let w=i.createBuffer(),P=M.__size,x=M.usage;return i.bindBuffer(i.UNIFORM_BUFFER,w),i.bufferData(i.UNIFORM_BUFFER,P,x),i.bindBuffer(i.UNIFORM_BUFFER,null),i.bindBufferBase(i.UNIFORM_BUFFER,T,w),w}function d(){for(let M=0;M<o;M++)if(a.indexOf(M)===-1)return a.push(M),M;return We("WebGLRenderer: Maximum number of simultaneously usable uniforms groups reached."),0}function u(M){let T=s[M.id],w=M.uniforms,P=M.__cache;i.bindBuffer(i.UNIFORM_BUFFER,T);for(let x=0,E=w.length;x<E;x++){let C=w[x];if(Array.isArray(C))for(let U=0,B=C.length;U<B;U++)p(C[U],x,U,P);else p(C,x,0,P)}i.bindBuffer(i.UNIFORM_BUFFER,null)}function p(M,T,w,P){if(v(M,T,w,P)===!0){let x=M.__offset,E=M.value;if(Array.isArray(E)){let C=0;for(let U=0;U<E.length;U++){let B=E[U],H=f(B);g(B,M.__data,C),typeof B!="number"&&typeof B!="boolean"&&!B.isMatrix3&&!ArrayBuffer.isView(B)&&(C+=H.storage/Float32Array.BYTES_PER_ELEMENT)}}else g(E,M.__data,0);i.bufferSubData(i.UNIFORM_BUFFER,x,M.__data)}}function g(M,T,w){typeof M=="number"||typeof M=="boolean"?T[0]=M:M.isMatrix3?(T[0]=M.elements[0],T[1]=M.elements[1],T[2]=M.elements[2],T[3]=0,T[4]=M.elements[3],T[5]=M.elements[4],T[6]=M.elements[5],T[7]=0,T[8]=M.elements[6],T[9]=M.elements[7],T[10]=M.elements[8],T[11]=0):ArrayBuffer.isView(M)?T.set(new M.constructor(M.buffer,M.byteOffset,T.length)):M.toArray(T,w)}function v(M,T,w,P){let x=M.value,E=T+"_"+w;if(P[E]===void 0)return typeof x=="number"||typeof x=="boolean"?P[E]=x:ArrayBuffer.isView(x)?P[E]=x.slice():P[E]=x.clone(),!0;{let C=P[E];if(typeof x=="number"||typeof x=="boolean"){if(C!==x)return P[E]=x,!0}else{if(ArrayBuffer.isView(x))return!0;if(C.equals(x)===!1)return C.copy(x),!0}}return!1}function m(M){let T=M.uniforms,w=0,P=16;for(let E=0,C=T.length;E<C;E++){let U=Array.isArray(T[E])?T[E]:[T[E]];for(let B=0,H=U.length;B<H;B++){let N=U[B],V=Array.isArray(N.value)?N.value:[N.value];for(let X=0,W=V.length;X<W;X++){let re=V[X],Y=f(re),Q=w%P,j=Q%Y.boundary,Re=Q+j;w+=j,Re!==0&&P-Re<Y.storage&&(w+=P-Re),N.__data=new Float32Array(Y.storage/Float32Array.BYTES_PER_ELEMENT),N.__offset=w,w+=Y.storage}}}let x=w%P;return x>0&&(w+=P-x),M.__size=w,M.__cache={},this}function f(M){let T={boundary:0,storage:0};return typeof M=="number"||typeof M=="boolean"?(T.boundary=4,T.storage=4):M.isVector2?(T.boundary=8,T.storage=8):M.isVector3||M.isColor?(T.boundary=16,T.storage=12):M.isVector4?(T.boundary=16,T.storage=16):M.isMatrix3?(T.boundary=48,T.storage=48):M.isMatrix4?(T.boundary=64,T.storage=64):M.isTexture?ke("WebGLRenderer: Texture samplers can not be part of an uniforms group."):ArrayBuffer.isView(M)?(T.boundary=16,T.storage=M.byteLength):ke("WebGLRenderer: Unsupported uniform value type.",M),T}function S(M){let T=M.target;T.removeEventListener("dispose",S);let w=a.indexOf(T.__bindingPointIndex);a.splice(w,1),i.deleteBuffer(s[T.id]),delete s[T.id],delete r[T.id]}function R(){for(let M in s)i.deleteBuffer(s[M]);a=[],s={},r={}}return{bind:c,update:l,dispose:R}}var x_=new Uint16Array([12469,15057,12620,14925,13266,14620,13807,14376,14323,13990,14545,13625,14713,13328,14840,12882,14931,12528,14996,12233,15039,11829,15066,11525,15080,11295,15085,10976,15082,10705,15073,10495,13880,14564,13898,14542,13977,14430,14158,14124,14393,13732,14556,13410,14702,12996,14814,12596,14891,12291,14937,11834,14957,11489,14958,11194,14943,10803,14921,10506,14893,10278,14858,9960,14484,14039,14487,14025,14499,13941,14524,13740,14574,13468,14654,13106,14743,12678,14818,12344,14867,11893,14889,11509,14893,11180,14881,10751,14852,10428,14812,10128,14765,9754,14712,9466,14764,13480,14764,13475,14766,13440,14766,13347,14769,13070,14786,12713,14816,12387,14844,11957,14860,11549,14868,11215,14855,10751,14825,10403,14782,10044,14729,9651,14666,9352,14599,9029,14967,12835,14966,12831,14963,12804,14954,12723,14936,12564,14917,12347,14900,11958,14886,11569,14878,11247,14859,10765,14828,10401,14784,10011,14727,9600,14660,9289,14586,8893,14508,8533,15111,12234,15110,12234,15104,12216,15092,12156,15067,12010,15028,11776,14981,11500,14942,11205,14902,10752,14861,10393,14812,9991,14752,9570,14682,9252,14603,8808,14519,8445,14431,8145,15209,11449,15208,11451,15202,11451,15190,11438,15163,11384,15117,11274,15055,10979,14994,10648,14932,10343,14871,9936,14803,9532,14729,9218,14645,8742,14556,8381,14461,8020,14365,7603,15273,10603,15272,10607,15267,10619,15256,10631,15231,10614,15182,10535,15118,10389,15042,10167,14963,9787,14883,9447,14800,9115,14710,8665,14615,8318,14514,7911,14411,7507,14279,7198,15314,9675,15313,9683,15309,9712,15298,9759,15277,9797,15229,9773,15166,9668,15084,9487,14995,9274,14898,8910,14800,8539,14697,8234,14590,7790,14479,7409,14367,7067,14178,6621,15337,8619,15337,8631,15333,8677,15325,8769,15305,8871,15264,8940,15202,8909,15119,8775,15022,8565,14916,8328,14804,8009,14688,7614,14569,7287,14448,6888,14321,6483,14088,6171,15350,7402,15350,7419,15347,7480,15340,7613,15322,7804,15287,7973,15229,8057,15148,8012,15046,7846,14933,7611,14810,7357,14682,7069,14552,6656,14421,6316,14251,5948,14007,5528,15356,5942,15356,5977,15353,6119,15348,6294,15332,6551,15302,6824,15249,7044,15171,7122,15070,7050,14949,6861,14818,6611,14679,6349,14538,6067,14398,5651,14189,5311,13935,4958,15359,4123,15359,4153,15356,4296,15353,4646,15338,5160,15311,5508,15263,5829,15188,6042,15088,6094,14966,6001,14826,5796,14678,5543,14527,5287,14377,4985,14133,4586,13869,4257,15360,1563,15360,1642,15358,2076,15354,2636,15341,3350,15317,4019,15273,4429,15203,4732,15105,4911,14981,4932,14836,4818,14679,4621,14517,4386,14359,4156,14083,3795,13808,3437,15360,122,15360,137,15358,285,15355,636,15344,1274,15322,2177,15281,2765,15215,3223,15120,3451,14995,3569,14846,3567,14681,3466,14511,3305,14344,3121,14037,2800,13753,2467,15360,0,15360,1,15359,21,15355,89,15346,253,15325,479,15287,796,15225,1148,15133,1492,15008,1749,14856,1882,14685,1886,14506,1783,14324,1608,13996,1398,13702,1183]),Bn=null;function y_(){return Bn===null&&(Bn=new lr(x_,16,16,gi,Sn),Bn.name="DFG_LUT",Bn.minFilter=At,Bn.magFilter=At,Bn.wrapS=wn,Bn.wrapT=wn,Bn.generateMipmaps=!1,Bn.needsUpdate=!0),Bn}var rl=class{constructor(e={}){let{canvas:t=fu(),context:n=null,depth:s=!0,stencil:r=!1,alpha:a=!1,antialias:o=!1,premultipliedAlpha:c=!0,preserveDrawingBuffer:l=!1,powerPreference:h="default",failIfMajorPerformanceCaveat:d=!1,reversedDepthBuffer:u=!1,outputBufferType:p=$t}=e;this.isWebGLRenderer=!0;let g;if(n!==null){if(typeof WebGLRenderingContext<"u"&&n instanceof WebGLRenderingContext)throw new Error("THREE.WebGLRenderer: WebGL 1 is not supported since r163.");g=n.getContextAttributes().alpha}else g=a;let v=p,m=new Set([So,Mo,vo]),f=new Set([$t,Mn,Is,Ps,_o,xo]),S=new Uint32Array(4),R=new Int32Array(4),M=new L,T=null,w=null,P=[],x=[],E=null;this.domElement=t,this.debug={checkShaderErrors:!0,diagnostics:{keywords:!1},onShaderError:null},this.autoClear=!0,this.autoClearColor=!0,this.autoClearDepth=!0,this.autoClearStencil=!0,this.sortObjects=!0,this.clippingPlanes=[],this.localClippingEnabled=!1,this.toneMapping=vn,this.toneMappingExposure=1,this.transmissionResolutionScale=1;let C=this,U=!1,B=null,H=null,N=null,V=null;this._outputColorSpace=yt;let X=0,W=0,re=null,Y=-1,Q=null,j=new vt,Re=new vt,Ce=null,lt=new Fe(0),je=0,ot=t.width,q=t.height,ne=1,ve=null,Oe=null,we=new vt(0,0,ot,q),He=new vt(0,0,ot,q),rt=!1,te=new xs,ae=!1,le=!1,oe=new ut,ue=new L,Ue=new vt,Ne={background:null,fog:null,environment:null,overrideMaterial:null,isScene:!0},Ge=!1;function qe(){return re===null?ne:1}let D=n;function ct(y,F){return t.getContext(y,F)}let et,A,_,z,k,Z,fe,_e,J,ee,pe,Le,xe,ge,Pe,ze,Ze,O,Me,K,ce,Se,se;try{let y={alpha:!0,depth:s,stencil:r,antialias:o,premultipliedAlpha:c,preserveDrawingBuffer:l,powerPreference:h,failIfMajorPerformanceCaveat:d};if("setAttribute"in t&&t.setAttribute("data-engine",`three.js r${"186"}`),t.addEventListener("webglcontextlost",ft,!1),t.addEventListener("webglcontextrestored",Xe,!1),t.addEventListener("webglcontextcreationerror",Gt,!1),D===null){let F="webgl2";if(D=ct(F,y),D===null)throw ct(F)?new Error("THREE.WebGLRenderer: Error creating WebGL context with your selected attributes."):new Error("THREE.WebGLRenderer: Error creating WebGL context.")}De()}catch(y){throw t.removeEventListener("webglcontextlost",ft,!1),t.removeEventListener("webglcontextrestored",Xe,!1),t.removeEventListener("webglcontextcreationerror",Gt,!1),We("WebGLRenderer: "+y.message),y}function De(){et=new wg(D),et.init(),ce=new d_(D,et),A=new gg(D,et,e,ce),_=new h_(D,et),A.reversedDepthBuffer&&u&&_.buffers.depth.setReversed(!0),H=D.createFramebuffer(),N=D.createFramebuffer(),V=D.createFramebuffer(),z=new Cg(D),k=new $0,Z=new u_(D,et,_,k,A,ce,z),fe=new Tg(C),_e=new Pf(D),Se=new pg(D,_e),J=new Ag(D,_e,z,Se),ee=new Pg(D,J,_e,Se,z),O=new Ig(D,A,Z),Pe=new _g(k),pe=new J0(C,fe,et,A,Se,Pe),Le=new g_(C,k),xe=new Q0,ge=new s_(et),Ze=new fg(C,fe,_,ee,g,c),ze=new c_(C,ee,A),se=new __(D,z,A,_),Me=new mg(D,et,z),K=new Rg(D,et,z),z.programs=pe.programs,C.capabilities=A,C.extensions=et,C.properties=k,C.renderLists=xe,C.shadowMap=ze,C.state=_,C.info=z}v!==$t&&(E=new Dg(v,t.width,t.height,o,s,r));let Ie=new Bc(C,D);this.xr=Ie,this.getContext=function(){return D},this.getContextAttributes=function(){return D.getContextAttributes()},this.forceContextLoss=function(){let y=et.get("WEBGL_lose_context");y&&y.loseContext()},this.forceContextRestore=function(){let y=et.get("WEBGL_lose_context");y&&y.restoreContext()},this.getPixelRatio=function(){return ne},this.setPixelRatio=function(y){y!==void 0&&(ne=y,this.setSize(ot,q,!1))},this.getSize=function(y){return y.set(ot,q)},this.setSize=function(y,F,G=!0){if(Ie.isPresenting){ke("WebGLRenderer: Can't change size while VR device is presenting.");return}ot=y,q=F,t.width=Math.floor(y*ne),t.height=Math.floor(F*ne),G===!0&&(t.style.width=y+"px",t.style.height=F+"px"),E!==null&&E.setSize(t.width,t.height),this.setViewport(0,0,y,F)},this.getDrawingBufferSize=function(y){return y.set(ot*ne,q*ne).floor()},this.setDrawingBufferSize=function(y,F,G){ot=y,q=F,ne=G,t.width=Math.floor(y*G),t.height=Math.floor(F*G),this.setViewport(0,0,y,F)},this.setEffects=function(y){if(v===$t){We("WebGLRenderer: setEffects() requires outputBufferType set to HalfFloatType or FloatType.");return}if(y){for(let F=0;F<y.length;F++)if(y[F].isOutputPass===!0){ke("WebGLRenderer: OutputPass is not needed in setEffects(). Tone mapping and color space conversion are applied automatically.");break}}E.setEffects(y||[])},this.getCurrentViewport=function(y){return y.copy(j)},this.getViewport=function(y){return y.copy(we)},this.setViewport=function(y,F,G,b){y.isVector4?we.set(y.x,y.y,y.z,y.w):we.set(y,F,G,b),_.viewport(j.copy(we).multiplyScalar(ne).round())},this.getScissor=function(y){return y.copy(He)},this.setScissor=function(y,F,G,b){y.isVector4?He.set(y.x,y.y,y.z,y.w):He.set(y,F,G,b),_.scissor(Re.copy(He).multiplyScalar(ne).round())},this.getScissorTest=function(){return rt},this.setScissorTest=function(y){_.setScissorTest(rt=y)},this.setOpaqueSort=function(y){ve=y},this.setTransparentSort=function(y){Oe=y},this.getClearColor=function(y){return y.copy(Ze.getClearColor())},this.setClearColor=function(){Ze.setClearColor(...arguments)},this.getClearAlpha=function(){return Ze.getClearAlpha()},this.setClearAlpha=function(){Ze.setClearAlpha(...arguments)},this.clear=function(y=!0,F=!0,G=!0){let b=0;if(y){let I=!1;if(re!==null){let $=re.texture.format;I=m.has($)}if(I){let $=re.texture.type,de=f.has($),ie=Ze.getClearColor(),me=Ze.getClearAlpha(),be=ie.r,Ve=ie.g,$e=ie.b;de?(S[0]=be,S[1]=Ve,S[2]=$e,S[3]=me,D.clearBufferuiv(D.COLOR,0,S)):(R[0]=be,R[1]=Ve,R[2]=$e,R[3]=me,D.clearBufferiv(D.COLOR,0,R))}else b|=D.COLOR_BUFFER_BIT}F&&(b|=D.DEPTH_BUFFER_BIT,this.state.buffers.depth.setMask(!0)),G&&(b|=D.STENCIL_BUFFER_BIT,this.state.buffers.stencil.setMask(4294967295)),b!==0&&D.clear(b)},this.clearColor=function(){this.clear(!0,!1,!1)},this.clearDepth=function(){this.clear(!1,!0,!1)},this.clearStencil=function(){this.clear(!1,!1,!0)},this.setNodesHandler=function(y){y.setRenderer(this),B=y},this.dispose=function(){t.removeEventListener("webglcontextlost",ft,!1),t.removeEventListener("webglcontextrestored",Xe,!1),t.removeEventListener("webglcontextcreationerror",Gt,!1),Ze.dispose(),xe.dispose(),ge.dispose(),k.dispose(),fe.dispose(),ee.dispose(),Se.dispose(),se.dispose(),pe.dispose(),Ie.dispose(),Ie.removeEventListener("sessionstart",Hn),Ie.removeEventListener("sessionend",Ni),Ot.stop()};function ft(y){y.preventDefault(),ir("WebGLRenderer: Context Lost."),U=!0}function Xe(){ir("WebGLRenderer: Context Restored."),U=!1;let y=z.autoReset,F=ze.enabled,G=ze.autoUpdate,b=ze.needsUpdate,I=ze.type;De(),z.autoReset=y,ze.enabled=F,ze.autoUpdate=G,ze.needsUpdate=b,ze.type=I}function Gt(y){We("WebGLRenderer: A WebGL context could not be created. Reason: ",y.statusMessage)}function Yt(y){let F=y.target;F.removeEventListener("dispose",Yt),Bs(F)}function Bs(y){Li(y),k.remove(y)}function Li(y){let F=k.get(y).programs;F!==void 0&&(F.forEach(function(G){pe.releaseProgram(G)}),y.isShaderMaterial&&pe.releaseShaderCache(y))}this.renderBufferDirect=function(y,F,G,b,I,$){F===null&&(F=Ne);let de=I.isMesh&&I.matrixWorld.determinantAffine()<0,ie=Zr(y,F,G,b,I);_.setMaterial(b,de);let me=G.index,be=1;if(b.wireframe===!0){if(me=J.getWireframeAttribute(G),me===void 0)return;be=2}let Ve=G.drawRange,$e=G.attributes.position,Ae=Ve.start*be,Be=(Ve.start+Ve.count)*be;$!==null&&(Ae=Math.max(Ae,$.start*be),Be=Math.min(Be,($.start+$.count)*be)),me!==null?(Ae=Math.max(Ae,0),Be=Math.min(Be,me.count)):$e!=null&&(Ae=Math.max(Ae,0),Be=Math.min(Be,$e.count));let Ye=Be-Ae;if(Ye<0||Ye===1/0)return;Se.setup(I,b,ie,G,me);let ye,tt=Me;if(me!==null&&(ye=_e.get(me),tt=K,tt.setIndex(ye)),I.isMesh)b.wireframe===!0?(_.setLineWidth(b.wireframeLinewidth*qe()),tt.setMode(D.LINES)):tt.setMode(D.TRIANGLES);else if(I.isLine){let gt=b.linewidth;gt===void 0&&(gt=1),_.setLineWidth(gt*qe()),I.isLineSegments?tt.setMode(D.LINES):I.isLineLoop?tt.setMode(D.LINE_LOOP):tt.setMode(D.LINE_STRIP)}else I.isPoints?tt.setMode(D.POINTS):I.isSprite&&tt.setMode(D.TRIANGLES);if(I.isBatchedMesh)if(et.get("WEBGL_multi_draw"))tt.renderMultiDraw(I._multiDrawStarts,I._multiDrawCounts,I._multiDrawCount);else{let gt=I._multiDrawStarts,Te=I._multiDrawCounts,It=I._multiDrawCount,at=me?_e.get(me).bytesPerElement:1,Zt=k.get(b).currentProgram.getUniforms();for(let rn=0;rn<It;rn++)Zt.setValue(D,"_gl_DrawID",rn),tt.render(gt[rn]/at,Te[rn])}else if(I.isInstancedMesh)tt.renderInstances(Ae,Ye,I.count);else if(G.isInstancedBufferGeometry){let gt=G._maxInstanceCount!==void 0?G._maxInstanceCount:1/0,Te=Math.min(G.instanceCount,gt);tt.renderInstances(Ae,Ye,Te)}else tt.render(Ae,Ye)};function Di(y,F,G,b){B!==null&&y.isNodeMaterial&&B.setObject(b,y),ae===!0&&Pe.setState(y,G,!1),y.transparent===!0&&y.side===nn&&y.forceSinglePass===!1?(y.side=Ut,y.needsUpdate=!0,Bi(y,F,b),y.side=fi,y.needsUpdate=!0,Bi(y,F,b),y.side=nn):Bi(y,F,b)}this.compile=function(y,F,G=null){G===null&&(G=y),B!==null&&B.renderStart(y,F,G),w=ge.get(G),w.init(F),x.push(w),G.traverseVisible(function(I){I.isLight&&I.layers.test(F.layers)&&(w.pushLight(I),I.castShadow&&w.pushShadow(I))}),y!==G&&y.traverseVisible(function(I){I.isLight&&I.layers.test(F.layers)&&(w.pushLight(I),I.castShadow&&w.pushShadow(I))}),w.setupLights(),B!==null&&B.updateLights(w.state.lightsArray),le=this.localClippingEnabled,ae=Pe.init(this.clippingPlanes,le),ae===!0&&Pe.setGlobalState(this.clippingPlanes,F),B!==null&&ze.render(w.state.shadowsArray,G,F);let b=new Set;return y.traverse(function(I){if(!(I.isMesh||I.isPoints||I.isLine||I.isSprite))return;let $=I.material;if($)if(Array.isArray($))for(let de=0;de<$.length;de++){let ie=$[de];Di(ie,G,F,I),b.add(ie)}else Di($,G,F,I),b.add($)}),w=x.pop(),B!==null&&B.renderEnd(),b},this.compileAsync=function(y,F,G=null){let b=this.compile(y,F,G);return new Promise(I=>{function $(){if(b.forEach(function(de){let me=k.get(de).currentProgram;(me===void 0||me.isReady())&&b.delete(de)}),b.size===0){I(y);return}setTimeout($,10)}et.get("KHR_parallel_shader_compile")!==null?$():setTimeout($,10)})};let Ui=null;function sn(y){Ui&&Ui(y)}function Hn(){Ot.stop()}function Ni(){Ot.start()}let Ot=new Yu;Ot.setAnimationLoop(sn),typeof self<"u"&&Ot.setContext(self),this.setAnimationLoop=function(y){Ui=y,Ie.setAnimationLoop(y),y===null?Ot.stop():Ot.start()},Ie.addEventListener("sessionstart",Hn),Ie.addEventListener("sessionend",Ni),this.render=function(y,F){if(F!==void 0&&F.isCamera!==!0){We("WebGLRenderer.render: camera is not an instance of THREE.Camera.");return}if(U===!0)return;B!==null&&B.renderStart(y,F);let G=Ie.enabled===!0&&Ie.isPresenting===!0,b=E!==null&&(re===null||G)&&E.begin(C,re);if(y.matrixWorldAutoUpdate===!0&&y.updateMatrixWorld(),F.parent===null&&F.matrixWorldAutoUpdate===!0&&F.updateMatrixWorld(),Ie.enabled===!0&&Ie.isPresenting===!0&&(E===null||E.isCompositing()===!1)&&(Ie.cameraAutoUpdate===!0&&Ie.updateCamera(F),F=Ie.getCamera()),y.isScene===!0&&y.onBeforeRender(C,y,F,re),w=ge.get(y,x.length),w.init(F),w.state.textureUnits=Z.getTextureUnits(),x.push(w),oe.multiplyMatrices(F.projectionMatrix,F.matrixWorldInverse),te.setFromProjectionMatrix(oe,gn,F.reversedDepth),le=this.localClippingEnabled,ae=Pe.init(this.clippingPlanes,le),T=xe.get(y,P.length),T.init(),P.push(T),Ie.enabled===!0&&Ie.isPresenting===!0){let de=C.xr.getDepthSensingMesh();de!==null&&Nt(de,F,-1/0,C.sortObjects)}Nt(y,F,0,C.sortObjects),T.finish(),B!==null&&B.updateLights(w.state.lightsArray),C.sortObjects===!0&&T.sort(ve,Oe),Ge=Ie.enabled===!1||Ie.isPresenting===!1||Ie.hasDepthSensing()===!1,Ge&&Ze.addToRenderList(T,y),this.info.render.frame++,this.info.autoReset===!0&&this.info.reset(),ae===!0&&Pe.beginShadows();let I=w.state.shadowsArray;if(ze.render(I,y,F),ae===!0&&Pe.endShadows(),(b&&E.hasRenderPass())===!1){let de=T.opaque,ie=T.transmissive;if(w.setupLights(),F.isArrayCamera){let me=F.cameras;if(ie.length>0)for(let be=0,Ve=me.length;be<Ve;be++){let $e=me[be];Oi(de,ie,y,$e)}Ge&&Ze.render(y);for(let be=0,Ve=me.length;be<Ve;be++){let $e=me[be];Fi(T,y,$e,$e.viewport)}}else ie.length>0&&Oi(de,ie,y,F),Ge&&Ze.render(y),Fi(T,y,F)}re!==null&&W===0&&(Z.updateMultisampleRenderTarget(re),Z.updateRenderTargetMipmap(re)),b&&E.end(C),y.isScene===!0&&y.onAfterRender(C,y,F),Se.resetDefaultState(),Y=-1,Q=null,x.pop(),x.length>0?(w=x[x.length-1],Z.setTextureUnits(w.state.textureUnits),ae===!0&&Pe.setGlobalState(C.clippingPlanes,w.state.camera)):w=null,P.pop(),P.length>0?T=P[P.length-1]:T=null,B!==null&&B.renderEnd()};function Nt(y,F,G,b){if(y.visible===!1)return;if(y.layers.test(F.layers)){if(y.isGroup)G=y.renderOrder;else if(y.isLOD)y.autoUpdate===!0&&y.update(F);else if(y.isLightProbeGrid)w.pushLightProbeGrid(y);else if(y.isLight)w.pushLight(y),y.castShadow&&w.pushShadow(y);else if(y.isSprite){if(!y.frustumCulled||y.intersectsFrustum(te)){b&&Ue.setFromMatrixPosition(y.matrixWorld).applyMatrix4(oe);let de=ee.update(y),ie=y.material;ie.visible&&T.push(y,de,ie,G,Ue.z,null,F)}}else if((y.isMesh||y.isLine||y.isPoints)&&(!y.frustumCulled||y.intersectsFrustum(te))){let de=ee.update(y),ie=y.material;if(b&&(y.boundingSphere!==void 0?(y.boundingSphere===null&&y.computeBoundingSphere(),Ue.copy(y.boundingSphere.center)):(de.boundingSphere===null&&de.computeBoundingSphere(),Ue.copy(de.boundingSphere.center)),Ue.applyMatrix4(y.matrixWorld).applyMatrix4(oe)),Array.isArray(ie)){let me=de.groups;for(let be=0,Ve=me.length;be<Ve;be++){let $e=me[be],Ae=ie[$e.materialIndex];Ae&&Ae.visible&&T.push(y,de,Ae,G,Ue.z,$e,F)}}else ie.visible&&T.push(y,de,ie,G,Ue.z,null,F)}}let $=y.children;for(let de=0,ie=$.length;de<ie;de++)Nt($[de],F,G,b)}function Fi(y,F,G,b){let{opaque:I,transmissive:$,transparent:de}=y;w.setupLightsView(G),ae===!0&&Pe.setGlobalState(C.clippingPlanes,G),b&&_.viewport(j.copy(b)),I.length>0&&Vn(I,F,G),$.length>0&&Vn($,F,G),de.length>0&&Vn(de,F,G),_.buffers.depth.setTest(!0),_.buffers.depth.setMask(!0),_.buffers.color.setMask(!0),_.setPolygonOffset(!1)}function Oi(y,F,G,b){if((G.isScene===!0?G.overrideMaterial:null)!==null)return;if(w.state.transmissionRenderTarget[b.id]===void 0){let Ae=et.has("EXT_color_buffer_half_float")||et.has("EXT_color_buffer_float");w.state.transmissionRenderTarget[b.id]=new Jt(1,1,{generateMipmaps:!0,type:Ae?Sn:$t,minFilter:On,samples:Math.max(4,A.samples),stencilBuffer:r,resolveDepthBuffer:!1,resolveStencilBuffer:!1,storeMultisampledDepthBuffer:!1,storeMultisampledStencilBuffer:!1,colorSpace:st.workingColorSpace})}let $=w.state.transmissionRenderTarget[b.id],de=b.viewport||j;$.setSize(de.z*C.transmissionResolutionScale,de.w*C.transmissionResolutionScale);let ie=C.getRenderTarget(),me=C.getActiveCubeFace(),be=C.getActiveMipmapLevel();C.setRenderTarget($),C.getClearColor(lt),je=C.getClearAlpha(),je<1&&C.setClearColor(16777215,.5),C.clear(),Ge&&Ze.render(G);let Ve=C.toneMapping;C.toneMapping=vn;let $e=b.viewport;if(b.viewport!==void 0&&(b.viewport=void 0),w.setupLightsView(b),ae===!0&&Pe.setGlobalState(C.clippingPlanes,b),Vn(y,G,b),Z.updateMultisampleRenderTarget($),Z.updateRenderTargetMipmap($),et.has("WEBGL_multisampled_render_to_texture")===!1){let Ae=!1;for(let Be=0,Ye=F.length;Be<Ye;Be++){let ye=F[Be],{object:tt,geometry:gt,material:Te,group:It}=ye;if(Te.side===nn&&tt.layers.test(b.layers)){let at=Te.side;Te.side=Ut,Te.needsUpdate=!0,hn(tt,G,b,gt,Te,It),Te.side=at,Te.needsUpdate=!0,Ae=!0}}Ae===!0&&(Z.updateMultisampleRenderTarget($),Z.updateRenderTargetMipmap($))}C.setRenderTarget(ie,me,be),C.setClearColor(lt,je),$e!==void 0&&(b.viewport=$e),C.toneMapping=Ve}function Vn(y,F,G){let b=F.isScene===!0?F.overrideMaterial:null;for(let I=0,$=y.length;I<$;I++){let de=y[I],{object:ie,geometry:me,group:be}=de,Ve=de.material;Ve.allowOverride===!0&&b!==null&&(Ve=b),ie.layers.test(G.layers)&&hn(ie,F,G,me,Ve,be)}}function hn(y,F,G,b,I,$){B!==null&&I.isNodeMaterial&&B.setObject(y,I),y.onBeforeRender(C,F,G,b,I,$),y.modelViewMatrix.multiplyMatrices(G.matrixWorldInverse,y.matrixWorld),y.normalMatrix.getNormalMatrix(y.modelViewMatrix),I.onBeforeRender(C,F,G,b,y,$),I.transparent===!0&&I.side===nn&&I.forceSinglePass===!1?(I.side=Ut,I.needsUpdate=!0,C.renderBufferDirect(G,F,b,I,y,$),I.side=fi,I.needsUpdate=!0,C.renderBufferDirect(G,F,b,I,y,$),I.side=nn):C.renderBufferDirect(G,F,b,I,y,$),y.onAfterRender(C,F,G,b,I,$)}function Bi(y,F,G){F.isScene!==!0&&(F=Ne);let b=k.get(y),I=w.state.lights,$=w.state.shadowsArray,de=I.state.version,ie=pe.getParameters(y,I.state,$,F,G,w.state.lightProbeGridArray),me=pe.getProgramCacheKey(ie),be=b.programs;b.environment=y.isMeshStandardMaterial||y.isMeshLambertMaterial||y.isMeshPhongMaterial?F.environment:null,b.fog=F.fog;let Ve=y.isMeshStandardMaterial||y.isMeshLambertMaterial&&!y.envMap||y.isMeshPhongMaterial&&!y.envMap;b.envMap=fe.get(y.envMap||b.environment,Ve),b.envMapRotation=b.environment!==null&&y.envMap===null?F.environmentRotation:y.envMapRotation,be===void 0&&(y.addEventListener("dispose",Yt),be=new Map,b.programs=be);let $e=be.get(me);if($e!==void 0){if(b.currentProgram===$e&&b.lightsStateVersion===de)return jn(y,ie),$e}else ie.uniforms=pe.getUniforms(y),B!==null&&y.isNodeMaterial&&B.build(y,G,ie),y.onBeforeCompile(ie,C),$e=pe.acquireProgram(ie,me),be.set(me,$e),b.uniforms=ie.uniforms;let Ae=b.uniforms;return(!y.isShaderMaterial&&!y.isRawShaderMaterial||y.clipping===!0)&&(Ae.clippingPlanes=Pe.uniform),jn(y,ie),b.needsLights=Jr(y),b.lightsStateVersion=de,b.needsLights&&(Ae.ambientLightColor.value=I.state.ambient,Ae.lightProbe.value=I.state.probe,Ae.sunLights.value=I.state.sun,Ae.sunLightShadows.value=I.state.sunShadow,Ae.directionalLights.value=I.state.directional,Ae.directionalLightShadows.value=I.state.directionalShadow,Ae.spotLights.value=I.state.spot,Ae.spotLightShadows.value=I.state.spotShadow,Ae.rectAreaLights.value=I.state.rectArea,Ae.ltc_1.value=I.state.rectAreaLTC1,Ae.ltc_2.value=I.state.rectAreaLTC2,Ae.pointLights.value=I.state.point,Ae.pointLightShadows.value=I.state.pointShadow,Ae.hemisphereLights.value=I.state.hemi,Ae.sunShadowMatrix.value=I.state.sunShadowMatrix,Ae.sunShadowCascade.value=I.state.sunShadowCascade,Ae.directionalShadowMatrix.value=I.state.directionalShadowMatrix,Ae.spotLightMatrix.value=I.state.spotLightMatrix,Ae.spotLightMap.value=I.state.spotLightMap,Ae.pointShadowMatrix.value=I.state.pointShadowMatrix),b.lightProbeGrid=w.state.lightProbeGridArray.length>0,b.currentProgram=$e,b.uniformsList=null,$e}function _i(y){if(y.uniformsList===null){let F=y.currentProgram.getUniforms();y.uniformsList=Us.seqWithValue(F.seq,y.uniforms)}return y.uniformsList}function jn(y,F){let G=k.get(y);G.outputColorSpace=F.outputColorSpace,G.batching=F.batching,G.batchingColor=F.batchingColor,G.instancing=F.instancing,G.instancingColor=F.instancingColor,G.instancingMorph=F.instancingMorph,G.skinning=F.skinning,G.morphTargets=F.morphTargets,G.morphNormals=F.morphNormals,G.morphColors=F.morphColors,G.morphTargetsCount=F.morphTargetsCount,G.numClippingPlanes=F.numClippingPlanes,G.numIntersection=F.numClipIntersection,G.vertexAlphas=F.vertexAlphas,G.vertexTangents=F.vertexTangents,G.toneMapping=F.toneMapping}function bn(y,F){if(y.length===0)return null;if(y.length===1)return y[0].texture!==null?y[0]:null;M.setFromMatrixPosition(F.matrixWorld);for(let G=0,b=y.length;G<b;G++){let I=y[G];if(I.texture!==null&&I.boundingBox.containsPoint(M))return I}return null}function Zr(y,F,G,b,I){F.isScene!==!0&&(F=Ne),Z.resetTextureUnits();let $=F.fog,de=b.isMeshStandardMaterial||b.isMeshLambertMaterial||b.isMeshPhongMaterial?F.environment:null,ie=re===null?C.outputColorSpace:re.isXRRenderTarget===!0?re.texture.colorSpace:st.workingColorSpace,me=b.isMeshStandardMaterial||b.isMeshLambertMaterial&&!b.envMap||b.isMeshPhongMaterial&&!b.envMap,be=fe.get(b.envMap||de,me),Ve=b.vertexColors===!0&&!!G.attributes.color&&G.attributes.color.itemSize===4,$e=!!G.attributes.tangent&&(!!b.normalMap||b.anisotropy>0),Ae=!!G.morphAttributes.position,Be=!!G.morphAttributes.normal,Ye=!!G.morphAttributes.color,ye=vn;b.toneMapped&&(re===null||re.isXRRenderTarget===!0)&&(ye=C.toneMapping);let tt=G.morphAttributes.position||G.morphAttributes.normal||G.morphAttributes.color,gt=tt!==void 0?tt.length:0,Te=k.get(b),It=w.state.lights;if(ae===!0&&(le===!0||y!==Q)){let _t=y===Q&&b.id===Y;Pe.setState(b,y,_t)}let at=!1;b.version===Te.__version?(Te.needsLights&&Te.lightsStateVersion!==It.state.version||Te.outputColorSpace!==ie||I.isBatchedMesh&&Te.batching===!1||!I.isBatchedMesh&&Te.batching===!0||I.isBatchedMesh&&Te.batchingColor===!0&&I._colorsTexture===null||I.isBatchedMesh&&Te.batchingColor===!1&&I._colorsTexture!==null||I.isInstancedMesh&&Te.instancing===!1||!I.isInstancedMesh&&Te.instancing===!0||I.isSkinnedMesh&&Te.skinning===!1||!I.isSkinnedMesh&&Te.skinning===!0||I.isInstancedMesh&&Te.instancingColor===!0&&I.instanceColor===null||I.isInstancedMesh&&Te.instancingColor===!1&&I.instanceColor!==null||I.isInstancedMesh&&Te.instancingMorph===!0&&I.morphTexture===null||I.isInstancedMesh&&Te.instancingMorph===!1&&I.morphTexture!==null||Te.envMap!==be||b.fog===!0&&Te.fog!==$||Te.numClippingPlanes!==void 0&&(Te.numClippingPlanes!==Pe.numPlanes||Te.numIntersection!==Pe.numIntersection)||Te.vertexAlphas!==Ve||Te.vertexTangents!==$e||Te.morphTargets!==Ae||Te.morphNormals!==Be||Te.morphColors!==Ye||Te.toneMapping!==ye||Te.morphTargetsCount!==gt||!!Te.lightProbeGrid!=w.state.lightProbeGridArray.length>0)&&(at=!0):(at=!0,Te.__version=b.version);let Zt=Te.currentProgram;at===!0&&(Zt=Bi(b,F,I),B&&b.isNodeMaterial&&B.onUpdateProgram(b,Zt,Te));let rn=!1,En=!1,Hi=!1,mt=Zt.getUniforms(),Et=Te.uniforms;if(_.useProgram(Zt.program)&&(rn=!0,En=!0,Hi=!0),b.id!==Y&&(Y=b.id,En=!0),Te.needsLights){let _t=bn(w.state.lightProbeGridArray,I);Te.lightProbeGrid!==_t&&(Te.lightProbeGrid=_t,En=!0)}if(rn||Q!==y){_.buffers.depth.getReversed()&&y.reversedDepth!==!0&&(y._reversedDepth=!0,y.updateProjectionMatrix()),mt.setValue(D,"projectionMatrix",y.projectionMatrix),mt.setValue(D,"viewMatrix",y.matrixWorldInverse);let ti=mt.map.cameraPosition;ti!==void 0&&ti.setValue(D,ue.setFromMatrixPosition(y.matrixWorld)),A.logarithmicDepthBuffer&&mt.setValue(D,"logDepthBufFC",2/(Math.log(y.far+1)/Math.LN2)),(b.isMeshPhongMaterial||b.isMeshToonMaterial||b.isMeshLambertMaterial||b.isMeshBasicMaterial||b.isMeshStandardMaterial||b.isShaderMaterial)&&mt.setValue(D,"isOrthographic",y.isOrthographicCamera===!0),Q!==y&&(Q=y,En=!0,Hi=!0)}if(Te.needsLights&&(It.state.sunShadowMap.length>0&&mt.setValue(D,"sunShadowMap",It.state.sunShadowMap,Z),It.state.directionalShadowMap.length>0&&mt.setValue(D,"directionalShadowMap",It.state.directionalShadowMap,Z),It.state.spotShadowMap.length>0&&mt.setValue(D,"spotShadowMap",It.state.spotShadowMap,Z),It.state.pointShadowMap.length>0&&mt.setValue(D,"pointShadowMap",It.state.pointShadowMap,Z)),I.isSkinnedMesh){mt.setOptional(D,I,"bindMatrix"),mt.setOptional(D,I,"bindMatrixInverse");let _t=I.skeleton;_t&&(_t.boneTexture===null&&_t.computeBoneTexture(),mt.setValue(D,"boneTexture",_t.boneTexture,Z))}I.isBatchedMesh&&(mt.setOptional(D,I,"batchingTexture"),mt.setValue(D,"batchingTexture",I._matricesTexture,Z),mt.setOptional(D,I,"batchingIdTexture"),mt.setValue(D,"batchingIdTexture",I._indirectTexture,Z),mt.setOptional(D,I,"batchingColorTexture"),I._colorsTexture!==null&&mt.setValue(D,"batchingColorTexture",I._colorsTexture,Z));let ei=G.morphAttributes;if((ei.position!==void 0||ei.normal!==void 0||ei.color!==void 0)&&O.update(I,G,Zt),(En||Te.receiveShadow!==I.receiveShadow)&&(Te.receiveShadow=I.receiveShadow,mt.setValue(D,"receiveShadow",I.receiveShadow)),(b.isMeshStandardMaterial||b.isMeshLambertMaterial||b.isMeshPhongMaterial)&&b.envMap===null&&F.environment!==null&&(Et.envMapIntensity.value=F.environmentIntensity),Et.dfgLUT!==void 0&&(Et.dfgLUT.value=y_()),En){if(mt.setValue(D,"toneMappingExposure",C.toneMappingExposure),Te.needsLights&&zi(Et,Hi),$&&b.fog===!0&&Le.refreshFogUniforms(Et,$),Le.refreshMaterialUniforms(Et,b,ne,q,w.state.transmissionRenderTarget[y.id]),Te.needsLights&&Te.lightProbeGrid){let _t=Te.lightProbeGrid;Et.probesSH.value=_t.texture,Et.probesMin.value.copy(_t.boundingBox.min),Et.probesMax.value.copy(_t.boundingBox.max),Et.probesResolution.value.copy(_t.resolution)}Us.upload(D,_i(Te),Et,Z)}if(b.isShaderMaterial&&b.uniformsNeedUpdate===!0&&(Us.upload(D,_i(Te),Et,Z),b.uniformsNeedUpdate=!1),b.isSpriteMaterial&&mt.setValue(D,"center",I.center),mt.setValue(D,"modelViewMatrix",I.modelViewMatrix),mt.setValue(D,"normalMatrix",I.normalMatrix),mt.setValue(D,"modelMatrix",I.matrixWorld),b.uniformsGroups!==void 0){let _t=b.uniformsGroups;for(let ti=0,Vi=_t.length;ti<Vi;ti++){let Xc=_t[ti];se.update(Xc,Zt),se.bind(Xc,Zt)}}return Zt}function zi(y,F){y.ambientLightColor.needsUpdate=F,y.lightProbe.needsUpdate=F,y.sunLights.needsUpdate=F,y.sunLightShadows.needsUpdate=F,y.directionalLights.needsUpdate=F,y.directionalLightShadows.needsUpdate=F,y.pointLights.needsUpdate=F,y.pointLightShadows.needsUpdate=F,y.spotLights.needsUpdate=F,y.spotLightShadows.needsUpdate=F,y.rectAreaLights.needsUpdate=F,y.hemisphereLights.needsUpdate=F}function Jr(y){return y.isMeshLambertMaterial||y.isMeshToonMaterial||y.isMeshPhongMaterial||y.isMeshStandardMaterial||y.isShadowMaterial||y.isShaderMaterial&&y.lights===!0}this.getActiveCubeFace=function(){return X},this.getActiveMipmapLevel=function(){return W},this.getRenderTarget=function(){return re},this.setRenderTargetTextures=function(y,F,G){let b=k.get(y);b.__autoAllocateDepthBuffer=y.resolveDepthBuffer===!1,b.__autoAllocateDepthBuffer===!1&&(b.__useRenderToTexture=!1),k.get(y.texture).__webglTexture=F,k.get(y.depthTexture).__webglTexture=b.__autoAllocateDepthBuffer?void 0:G,b.__hasExternalTextures=!0},this.setRenderTargetFramebuffer=function(y,F){let G=k.get(y);G.__webglFramebuffer=F,G.__useDefaultFramebuffer=F===void 0},this.setRenderTarget=function(y,F=0,G=0){re=y,X=F,W=G;let b=null,I=!1,$=!1;if(y){let ie=k.get(y);if(ie.__useDefaultFramebuffer!==void 0){_.bindFramebuffer(D.FRAMEBUFFER,ie.__webglFramebuffer),j.copy(y.viewport),Re.copy(y.scissor),Ce=y.scissorTest,_.viewport(j),_.scissor(Re),_.setScissorTest(Ce),Y=-1;return}else if(ie.__webglFramebuffer===void 0)Z.setupRenderTarget(y);else if(ie.__hasExternalTextures)Z.rebindTextures(y,k.get(y.texture).__webglTexture,k.get(y.depthTexture).__webglTexture);else if(y.depthBuffer){let Ve=y.depthTexture;if(ie.__boundDepthTexture!==Ve){if(Ve!==null&&k.has(Ve)&&(y.width!==Ve.image.width||y.height!==Ve.image.height))throw new Error("THREE.WebGLRenderer: Attached DepthTexture is initialized to the incorrect size.");Z.setupDepthRenderbuffer(y)}}let me=y.texture;(me.isData3DTexture||me.isDataArrayTexture||me.isCompressedArrayTexture)&&($=!0);let be=k.get(y).__webglFramebuffer;y.isWebGLCubeRenderTarget?(Array.isArray(be[F])?b=be[F][G]:b=be[F],I=!0):y.samples>0&&Z.useMultisampledRTT(y)===!1?b=k.get(y).__webglMultisampledFramebuffer:Array.isArray(be)?b=be[G]:b=be,j.copy(y.viewport),Re.copy(y.scissor),Ce=y.scissorTest}else j.copy(we).multiplyScalar(ne).floor(),Re.copy(He).multiplyScalar(ne).floor(),Ce=rt;if(G!==0&&(b=H),_.bindFramebuffer(D.FRAMEBUFFER,b)&&_.drawBuffers(y,b),_.viewport(j),_.scissor(Re),_.setScissorTest(Ce),I){let ie=k.get(y.texture);D.framebufferTexture2D(D.FRAMEBUFFER,D.COLOR_ATTACHMENT0,D.TEXTURE_CUBE_MAP_POSITIVE_X+F,ie.__webglTexture,G)}else if($){let ie=F;for(let me=0;me<y.textures.length;me++){let be=k.get(y.textures[me]);D.framebufferTextureLayer(D.FRAMEBUFFER,D.COLOR_ATTACHMENT0+me,be.__webglTexture,G,ie)}}else if(y!==null&&G!==0){let ie=k.get(y.texture);D.framebufferTexture2D(D.FRAMEBUFFER,D.COLOR_ATTACHMENT0,D.TEXTURE_2D,ie.__webglTexture,G)}Y=-1};function kn(y){let F=k.get(y);return(F.__readFormat!==y.format||F.__readType!==y.type)&&(F.__readFormat=y.format,F.__readType=y.type,F.__formatReadable=A.textureFormatReadable(y.format),F.__typeReadable=A.textureTypeReadable(y.type)),F}this.readRenderTargetPixels=function(y,F,G,b,I,$,de,ie=0){if(!(y&&y.isWebGLRenderTarget)){We("WebGLRenderer.readRenderTargetPixels: renderTarget is not THREE.WebGLRenderTarget.");return}let me=k.get(y).__webglFramebuffer;if(y.isWebGLCubeRenderTarget&&de!==void 0&&(me=me[de]),me){_.bindFramebuffer(D.FRAMEBUFFER,me);try{let be=y.textures[ie],Ve=be.format,$e=be.type;y.textures.length>1&&D.readBuffer(D.COLOR_ATTACHMENT0+ie);let Ae=kn(be);if(Ae.__formatReadable===!1){We("WebGLRenderer.readRenderTargetPixels: renderTarget is not in RGBA or implementation defined format.");return}if(Ae.__typeReadable===!1){We("WebGLRenderer.readRenderTargetPixels: renderTarget is not in UnsignedByteType or implementation defined type.");return}F>=0&&F<=y.width-b&&G>=0&&G<=y.height-I&&D.readPixels(F,G,b,I,ce.convert(Ve),ce.convert($e),$)}finally{let be=re!==null?k.get(re).__webglFramebuffer:null;_.bindFramebuffer(D.FRAMEBUFFER,be)}}},this.readRenderTargetPixelsAsync=async function(y,F,G,b,I,$,de,ie=0){if(!(y&&y.isWebGLRenderTarget))throw new Error("THREE.WebGLRenderer.readRenderTargetPixels: renderTarget is not THREE.WebGLRenderTarget.");let me=k.get(y).__webglFramebuffer;if(y.isWebGLCubeRenderTarget&&de!==void 0&&(me=me[de]),me)if(F>=0&&F<=y.width-b&&G>=0&&G<=y.height-I){_.bindFramebuffer(D.FRAMEBUFFER,me);let be=y.textures[ie],Ve=be.format,$e=be.type;y.textures.length>1&&D.readBuffer(D.COLOR_ATTACHMENT0+ie);let Ae=kn(be);if(Ae.__formatReadable===!1)throw new Error("THREE.WebGLRenderer.readRenderTargetPixelsAsync: renderTarget is not in RGBA or implementation defined format.");if(Ae.__typeReadable===!1)throw new Error("THREE.WebGLRenderer.readRenderTargetPixelsAsync: renderTarget is not in UnsignedByteType or implementation defined type.");let Be=D.createBuffer();D.bindBuffer(D.PIXEL_PACK_BUFFER,Be),D.bufferData(D.PIXEL_PACK_BUFFER,$.byteLength,D.STREAM_READ),D.readPixels(F,G,b,I,ce.convert(Ve),ce.convert($e),0),D.bindBuffer(D.PIXEL_PACK_BUFFER,null);let Ye=re!==null?k.get(re).__webglFramebuffer:null;_.bindFramebuffer(D.FRAMEBUFFER,Ye);let ye=D.fenceSync(D.SYNC_GPU_COMMANDS_COMPLETE,0);return D.flush(),await mu(D,ye,4),D.bindBuffer(D.PIXEL_PACK_BUFFER,Be),D.getBufferSubData(D.PIXEL_PACK_BUFFER,0,$),D.bindBuffer(D.PIXEL_PACK_BUFFER,null),D.deleteBuffer(Be),D.deleteSync(ye),$}else throw new Error("THREE.WebGLRenderer.readRenderTargetPixelsAsync: requested read bounds are out of range.")},this.copyFramebufferToTexture=function(y,F=null,G=0){let b=Math.pow(2,-G),I=Math.floor(y.image.width*b),$=Math.floor(y.image.height*b),de=F!==null?F.x:0,ie=F!==null?F.y:0;Z.setTexture2D(y,0),D.copyTexSubImage2D(D.TEXTURE_2D,G,0,0,de,ie,I,$),_.unbindTexture()},this.copyTextureToTexture=function(y,F,G=null,b=null,I=0,$=0){let de,ie,me,be,Ve,$e,Ae,Be,Ye,ye=y.isCompressedTexture?y.mipmaps[$]:y.image;if(G!==null)de=G.max.x-G.min.x,ie=G.max.y-G.min.y,me=G.isBox3?G.max.z-G.min.z:1,be=G.min.x,Ve=G.min.y,$e=G.isBox3?G.min.z:0;else{let Et=Math.pow(2,-I);de=Math.floor(ye.width*Et),ie=Math.floor(ye.height*Et),y.isDataArrayTexture?me=ye.depth:y.isData3DTexture?me=Math.floor(ye.depth*Et):me=1,be=0,Ve=0,$e=0}b!==null?(Ae=b.x,Be=b.y,Ye=b.z):(Ae=0,Be=0,Ye=0);let tt=ce.convert(F.format),gt=ce.convert(F.type),Te;F.isData3DTexture?(Z.setTexture3D(F,0),Te=D.TEXTURE_3D):F.isDataArrayTexture||F.isCompressedArrayTexture?(Z.setTexture2DArray(F,0),Te=D.TEXTURE_2D_ARRAY):(Z.setTexture2D(F,0),Te=D.TEXTURE_2D),_.activeTexture(D.TEXTURE0),_.pixelStorei(D.UNPACK_FLIP_Y_WEBGL,F.flipY),_.pixelStorei(D.UNPACK_PREMULTIPLY_ALPHA_WEBGL,F.premultiplyAlpha),_.pixelStorei(D.UNPACK_ALIGNMENT,F.unpackAlignment);let It=_.getParameter(D.UNPACK_ROW_LENGTH),at=_.getParameter(D.UNPACK_IMAGE_HEIGHT),Zt=_.getParameter(D.UNPACK_SKIP_PIXELS),rn=_.getParameter(D.UNPACK_SKIP_ROWS),En=_.getParameter(D.UNPACK_SKIP_IMAGES);_.pixelStorei(D.UNPACK_ROW_LENGTH,ye.width),_.pixelStorei(D.UNPACK_IMAGE_HEIGHT,ye.height),_.pixelStorei(D.UNPACK_SKIP_PIXELS,be),_.pixelStorei(D.UNPACK_SKIP_ROWS,Ve),_.pixelStorei(D.UNPACK_SKIP_IMAGES,$e);let Hi=y.isDataArrayTexture||y.isData3DTexture,mt=F.isDataArrayTexture||F.isData3DTexture;if(y.isDepthTexture){let Et=k.get(y),ei=k.get(F),_t=k.get(Et.__renderTarget),ti=k.get(ei.__renderTarget);_.bindFramebuffer(D.READ_FRAMEBUFFER,_t.__webglFramebuffer),_.bindFramebuffer(D.DRAW_FRAMEBUFFER,ti.__webglFramebuffer);for(let Vi=0;Vi<me;Vi++)Hi&&(D.framebufferTextureLayer(D.READ_FRAMEBUFFER,D.COLOR_ATTACHMENT0,k.get(y).__webglTexture,I,$e+Vi),D.framebufferTextureLayer(D.DRAW_FRAMEBUFFER,D.COLOR_ATTACHMENT0,k.get(F).__webglTexture,$,Ye+Vi)),D.blitFramebuffer(be,Ve,de,ie,Ae,Be,de,ie,D.DEPTH_BUFFER_BIT,D.NEAREST);_.bindFramebuffer(D.READ_FRAMEBUFFER,null),_.bindFramebuffer(D.DRAW_FRAMEBUFFER,null)}else if(I!==0||y.isRenderTargetTexture||k.has(y)){let Et=k.get(y),ei=k.get(F);_.bindFramebuffer(D.READ_FRAMEBUFFER,N),_.bindFramebuffer(D.DRAW_FRAMEBUFFER,V);for(let _t=0;_t<me;_t++)Hi?D.framebufferTextureLayer(D.READ_FRAMEBUFFER,D.COLOR_ATTACHMENT0,Et.__webglTexture,I,$e+_t):D.framebufferTexture2D(D.READ_FRAMEBUFFER,D.COLOR_ATTACHMENT0,D.TEXTURE_2D,Et.__webglTexture,I),mt?D.framebufferTextureLayer(D.DRAW_FRAMEBUFFER,D.COLOR_ATTACHMENT0,ei.__webglTexture,$,Ye+_t):D.framebufferTexture2D(D.DRAW_FRAMEBUFFER,D.COLOR_ATTACHMENT0,D.TEXTURE_2D,ei.__webglTexture,$),I!==0?D.blitFramebuffer(be,Ve,de,ie,Ae,Be,de,ie,D.COLOR_BUFFER_BIT,D.NEAREST):mt?D.copyTexSubImage3D(Te,$,Ae,Be,Ye+_t,be,Ve,de,ie):D.copyTexSubImage2D(Te,$,Ae,Be,be,Ve,de,ie);_.bindFramebuffer(D.READ_FRAMEBUFFER,null),_.bindFramebuffer(D.DRAW_FRAMEBUFFER,null)}else mt?y.isDataTexture||y.isData3DTexture?D.texSubImage3D(Te,$,Ae,Be,Ye,de,ie,me,tt,gt,ye.data):F.isCompressedArrayTexture?D.compressedTexSubImage3D(Te,$,Ae,Be,Ye,de,ie,me,tt,ye.data):D.texSubImage3D(Te,$,Ae,Be,Ye,de,ie,me,tt,gt,ye):y.isDataTexture?D.texSubImage2D(D.TEXTURE_2D,$,Ae,Be,de,ie,tt,gt,ye.data):y.isCompressedTexture?D.compressedTexSubImage2D(D.TEXTURE_2D,$,Ae,Be,ye.width,ye.height,tt,ye.data):D.texSubImage2D(D.TEXTURE_2D,$,Ae,Be,de,ie,tt,gt,ye);_.pixelStorei(D.UNPACK_ROW_LENGTH,It),_.pixelStorei(D.UNPACK_IMAGE_HEIGHT,at),_.pixelStorei(D.UNPACK_SKIP_PIXELS,Zt),_.pixelStorei(D.UNPACK_SKIP_ROWS,rn),_.pixelStorei(D.UNPACK_SKIP_IMAGES,En),$===0&&F.generateMipmaps&&D.generateMipmap(Te),_.unbindTexture()},this.initRenderTarget=function(y){k.get(y).__webglFramebuffer===void 0&&Z.setupRenderTarget(y)},this.initTexture=function(y){y.isCubeTexture?Z.setTextureCube(y,0):y.isData3DTexture?Z.setTexture3D(y,0):y.isDataArrayTexture||y.isCompressedArrayTexture?Z.setTexture2DArray(y,0):Z.setTexture2D(y,0),_.unbindTexture()},this.resetState=function(){X=0,W=0,re=null,_.reset(),Se.reset()},typeof __THREE_DEVTOOLS__<"u"&&__THREE_DEVTOOLS__.dispatchEvent(new CustomEvent("observe",{detail:this}))}get coordinateSystem(){return gn}get outputColorSpace(){return this._outputColorSpace}set outputColorSpace(e){this._outputColorSpace=e;let t=this.getContext();t.drawingBufferColorSpace=st._getDrawingBufferColorSpace(e),t.unpackColorSpace=st._getUnpackColorSpace()}};var Hc=document.getElementById("hero-visual"),Yr=document.getElementById("hero-three");Hc&&Yr&&M_(Hc,Yr).catch(i=>{console.warn("VideoGrabber Three.js scene disabled",i),Yr.hidden=!0,Hc.classList.remove("three-ready"),Yr.dataset.context="failed",ll(Yr,"initialization_failed")});async function M_(i,e){try{if((await Promise.all([document.fonts.load('700 18px "Manrope"',"\u0421\u0441\u044B\u043B\u043A\u0430 MP4 \u041A\u0443\u0440\u0441"),document.fonts.load('500 15px "Onest"',"\u041F\u043E\u0434\u0440\u043E\u0431\u043D\u0435\u0435 VideoGrabber Windows Telegram MP3")])).some(I=>I.length===0))throw new Error("font_faces_missing");e.dataset.fonts="Manrope + Onest"}catch{e.dataset.fonts="fallback"}let t=!!window.__VG_VISUAL_TEST,n=matchMedia("(prefers-reduced-motion: reduce)"),s=n.matches,r=t,a=matchMedia("(max-width: 720px)").matches,o=a,c=1,l=new rl({canvas:e,alpha:!0,antialias:!0,powerPreference:"high-performance",premultipliedAlpha:!1});e.dataset.engine=`three.js r${"186"}`,t&&(e.dataset.visualTest="true"),l.outputColorSpace=yt,l.toneMapping=Nr,l.toneMappingExposure=1.2,l.setClearColor(0,0);let h=!1,d=!1,u=!1;e.dataset.context="initializing";let p=()=>{e.hidden=!0,i.classList.remove("three-ready"),document.querySelector(".promotion-gift")?.classList.remove("has-live-gift")},g=b=>{d=!0,bn(),p(),e.dataset.context="failed",ll(e,b)},v=new gs,m=td(l);v.environment=m.texture,v.environmentIntensity=.72;let f=new Ft(34,1,.1,60);f.position.set(0,.05,10.45);let S=new bt;S.rotation.x=-.04,v.add(S);let R=new Cr(9091071,461592,.25);v.add(R);let M=new Lr(12573951,1.8);M.position.set(-4.4,5.5,6.8),v.add(M);let T=new di(7324415,12.5,20,2);T.position.set(4.2,2.6,4.8),v.add(T);let w=new di(15848886,10.5,18,2);w.position.set(-4.8,1.4,-3.2),v.add(w);let P=new di(16752730,3.5,14,2);P.position.set(-3.4,-2.8,5),v.add(P);let x=new di(4634879,5.4,14,2);x.position.set(.5,-1.4,5.5),v.add(x);let E=new Fe(5612543),C=E.clone(),U=new Fe(6196479),B=new on({color:5943039,metalness:0,roughness:.045,transmission:.94,thickness:.85,ior:1.46,attenuationColor:new Fe(6331355),attenuationDistance:1.6,clearcoat:1,clearcoatRoughness:.08,emissive:402250,emissiveIntensity:.18,envMapIntensity:2.1}),H=E_(B);S.add(H);let N=T_();S.add(N),e.dataset.model="studio-play",i.dispatchEvent(new CustomEvent("videograbber:studio-model"));let V=new Xt({color:5082367,transparent:!0,opacity:.075,blending:Fn,depthWrite:!1,toneMapped:!1}),X=new Ke(new li(1.55,56,40),V);X.position.copy(H.position),X.visible=!1,S.add(X);let W=new Xt({color:8292607,transparent:!0,opacity:.16,blending:Fn,depthWrite:!1,side:Ut,toneMapped:!1}),re=new Ke(new li(1.61,56,40),W);re.position.copy(H.position),re.visible=!1,S.add(re);let Y=new Un(z_());Y.colorSpace=yt;let Q=new _s(new bi({map:Y,color:5141759,transparent:!0,opacity:.42,blending:Fn,depthWrite:!1,toneMapped:!1}));Q.scale.set(4.35,4.35,1),Q.position.set(0,0,-.48),Q.visible=!1,S.add(Q);let j=new ur(new wr(new li(1.505,24,16)),new ys({color:5941247,transparent:!0,opacity:.08,depthWrite:!1}));j.visible=!1,S.add(j);let Re=[kc(8571391,.96),kc(13227746,.95),kc(6463231,.86)],Ce=new Fe(6665727),lt=new Fe(12506599),je=new Fe(8895718),q=[{radius:2.42,tube:.125,rotation:[1.1,.14,-.4],material:Re[0]},{radius:2.35,tube:.068,rotation:[1.07,.15,-.43],material:Re[1]},{radius:2.3,tube:.018,rotation:[1.04,.16,-.46],material:Re[2]}].map(b=>{let I=new Ke(new wi(b.radius,b.tube,12,192),b.material);return I.rotation.set(...b.rotation),I.position.set(-.5,.12,-.3),S.add(I),I}),ne=H_(),ve=new Un(ne);ve.colorSpace=yt,ve.anisotropy=Math.min(l.capabilities.getMaxAnisotropy(),8);let Oe=new Ke(new mr(.58,64),new Xt({map:ve,transparent:!0,depthTest:!1,depthWrite:!1,toneMapped:!1}));Oe.position.set(0,0,1.625),Oe.renderOrder=20,Oe.visible=!1,S.add(Oe);let we=[{feature:"url",icon:"\u2197",title:"URL",subtitle:"\u0412\u0441\u0442\u0430\u0432\u044C\u0442\u0435 \u0441\u0441\u044B\u043B\u043A\u0443",position:[-2.45,1.58,.24],rotation:[-.04,.16,-.14],accent:5416959},{feature:"video",icon:"\u25B6",title:"MP4",subtitle:"\u0412\u0438\u0434\u0435\u043E",position:[2.65,1.1,.45],rotation:[-.05,-.18,.13],accent:5029119},{feature:"mp3",icon:"\u266A",title:"MP3",subtitle:"\u0410\u0443\u0434\u0438\u043E",position:[2.65,-.02,.55],rotation:[.01,.2,.08],accent:16751189},{feature:"course",icon:"\u25C7",title:"\u041A\u0443\u0440\u0441",subtitle:"\u0423\u0440\u043E\u043A\u0438 \u0438 \u043C\u0430\u0442\u0435\u0440\u0438\u0430\u043B\u044B",position:[2.65,-1.14,.65],rotation:[.02,-.22,-.07],accent:10837247},{feature:"windows",icon:"\u25A6",title:"Windows",subtitle:"\u0423\u0434\u043E\u0431\u043D\u043E\u0435 \u043F\u0440\u0438\u043B\u043E\u0436\u0435\u043D\u0438\u0435",position:[-1.72,-1.82,.88],rotation:[.04,.14,.12],accent:4900095},{feature:"telegram",icon:"\u27A4",title:"Telegram",subtitle:"\u0421\u043A\u0430\u0447\u0438\u0432\u0430\u0439\u0442\u0435 \u0447\u0435\u0440\u0435\u0437 \u0431\u043E\u0442\u0430",position:[2.05,-1.78,.34],rotation:[.04,-.18,-.12],accent:5089535}],He=new Map,rt=[];for(let b of H.children)b.userData.feature="video",b.userData.storyState="hero",rt.push(b);for(let b of N.children)b.userData.feature="mp3",b.userData.storyState="hero",rt.push(b);for(let b of we){let $=["video","mp3","course"].includes(b.feature)?Wc(l,{...b,theme:b.feature}):id(b,l);S.add($.group),$.heroVisible=["video","mp3","course"].includes(b.feature),$.bodyMaterial.opacity=$.heroVisible?.94:0,$.label.material.opacity=$.heroVisible?1:0,$.label.visible=$.heroVisible,$.group.visible=$.heroVisible,$.baseRotation.set(0,-.12,.02),$.group.rotation.copy($.baseRotation),$.heroVisible&&$.group.scale.setScalar(.72),He.set(b.feature,$),$.body.userData.storyState="hero",$.label.userData.storyState="hero",$.group.traverse(de=>{de.isMesh&&(de.userData.feature=b.feature,de.userData.storyState="hero",rt.push(de))})}let te=new Dr,ae=new he,le=null,oe=null,ue=A_(l);for(let[b,I]of Object.entries(ue)){S.add(I.group),D_(I.group);for(let $ of I.controls||[])$.group.traverse(de=>{de.isMesh&&(de.userData.feature=$.feature,de.userData.storyState=b,rt.push(de))})}let Ue={workflow:0,sync:0,pricing:0,windows:0,bonus:0},Ne=null,Ge=new bt;S.add(Ge);let qe=new Xt({color:4951551,transparent:!0,opacity:.35,blending:Fn,depthWrite:!1}),D=new li(.035,10,8),ct=[[-3.5,1.05,-.4,1.1],[3.45,.85,-.5,.82],[-3,-1.55,-.2,.68],[3.35,-1.35,-.35,.95],[.3,2.55,-.55,.74],[-.4,-2.55,-.6,.58]],et=ct.map(([b,I,$,de])=>{let ie=new Ke(D,qe);return ie.position.set(b,I,$),ie.scale.setScalar(de),Ge.add(ie),ie}),A=B_(a?48:96);v.add(A);let _=F_();v.add(_);let z=Object.freeze({high:{desktopDpr:1.5,mobileDpr:1.1,stars:!0,particles:!0,nebula:!0,decorationOpacity:1,environmentIntensity:.72},balanced:{desktopDpr:1.25,mobileDpr:.95,stars:!0,particles:!0,nebula:!0,decorationOpacity:.68,environmentIntensity:.58},economy:{desktopDpr:1,mobileDpr:.8,stars:!1,particles:!1,nebula:!1,decorationOpacity:.42,environmentIntensity:.44}}),k=["economy","balanced","high"],Z=45,fe=2,_e=5,J=Number(navigator.deviceMemory||0),ee=Number(navigator.hardwareConcurrency||0),pe=a||J>0&&J<=4||ee>0&&ee<=4?"balanced":"high",Le=1,xe=[],ge=0,Pe=0,ze=()=>{let b=z[pe];A.visible=!1,Ge.visible=!1,_.visible=!1,v.environmentIntensity=.9,V.opacity=.075*b.decorationOpacity,W.opacity=.16*b.decorationOpacity,Q.material.opacity=.42*b.decorationOpacity,j.material.opacity=.08*b.decorationOpacity,_.traverse(I=>{!I.material||!Number.isFinite(I.userData.baseOpacity)||(I.material.opacity=I.userData.baseOpacity*b.decorationOpacity)}),e.dataset.quality=pe},Ze=()=>{let b=z[pe],I=a?b.mobileDpr:b.desktopDpr;Le=Math.min(devicePixelRatio||1,I),l.setPixelRatio(Le),e.dataset.dpr=Le.toFixed(2)},O=b=>{if(r||!Object.prototype.hasOwnProperty.call(z,b)||pe===b)return;pe=b,ze(),Ze();let I=i.getBoundingClientRect();l.setSize(Math.max(1,Math.round(I.width)),Math.max(1,Math.round(I.height)),!1),i.dispatchEvent(new CustomEvent("videograbber:3d-quality",{detail:{quality:pe,dpr:Le}}))},Me=b=>{if(r||document.hidden||b<=0||b>1e3||(xe.push(b),xe.length<Z))return;let I=xe.reduce((me,be)=>me+be,0)/xe.length;xe.length=0,e.dataset.frameMs=I.toFixed(1);let $=a?41:24,de=a?30:17.5;I>$?(ge+=1,Pe=0):I<de?(Pe+=1,ge=0):(ge=0,Pe=0);let ie=k.indexOf(pe);ge>=fe&&ie>0?(ge=0,Pe=0,O(k[ie-1])):Pe>=_e&&ie<k.length-1&&(ge=0,Pe=0,O(k[ie+1]))};ze(),e.dataset.frameMs="0";let K=null,ce=new he(0,0),Se=new he(0,0),se=new L(0,.05,10.45),De=new L,Ie={hero:{rootScale:1,rootY:0,cameraZ:10.45,orbitScale:1},workflow:{rootScale:.92,rootY:.03,cameraZ:10.85,orbitScale:.58},sync:{rootScale:.88,rootY:.03,cameraZ:10.95,orbitScale:.46},pricing:{rootScale:.9,rootY:-.02,cameraZ:11.1,orbitScale:.34},windows:{rootScale:.96,rootY:.02,cameraZ:10.72,orbitScale:.22},bonus:{rootScale:1.28,rootY:0,cameraZ:10,orbitScale:.2}},ft={hero:5219327,workflow:5617919,sync:5624063,pricing:10119423,windows:6012159,bonus:5876479},Xe=document.getElementById("story-stage")?.dataset.storyState||"hero",Gt=0,Yt=1,Bs=0,Li=se.z,Di=1,Ui=0,sn=0,Hn=!0,Ni=!0,Ot=!1,Nt=0,Fi=performance.now(),Oi=()=>{let b=i.getBoundingClientRect(),I=Math.max(b.height*.88,1);Ui=Os(-b.top/I)};Oi(),window.addEventListener("scroll",Oi,{passive:!0});let Vn=()=>{if(u||h||d||l.getContext().isContextLost())return;a=matchMedia("(max-width: 720px)").matches;let b=i.getBoundingClientRect(),I=Math.max(1,Math.round(b.width)),$=Math.max(1,Math.round(b.height));f.aspect=I/$;let de=ed(Xe,f.aspect);se.z=de;for(let ie of Object.values(Ie))ie.cameraZ=de;Li=de,f.position.z=de,f.updateProjectionMatrix(),Ze(),l.setSize(I,$,!1),o=I<1050,c=Math.max(1,Math.min(1.35,I/450)),L_(ue,o)},hn=()=>{if(u||h||d||l.getContext().isContextLost()||s&&!t)return!1;try{l.render(v,f)}catch{return g("render_failed"),!1}if(l.getContext().isContextLost())return!1;e.hidden=!1,e.dataset.context="ready",e.dataset.frames=String(Number(e.dataset.frames||0)+1),e.dataset.storyState=Xe,document.querySelector(".promotion-gift")?.classList.toggle("has-live-gift",Xe==="bonus"&&Ue.bonus>.985),e.dataset.transition=Object.entries(Ue).every(([ie,me])=>ie===Xe?me>.985:me<.015)?"stable":"moving";let b=e.getBoundingClientRect();for(let[ie,me]of He){let be=i.querySelector(`.hero-hotspot[data-feature="${ie}"]`);if(!be||!me.heroVisible)continue;let Ve=me.group.getWorldPosition(new L).project(f);be.style.left=Math.max(38,Math.min(b.width-38,(Ve.x*.5+.5)*b.width))+"px",be.style.top=Math.max(18,Math.min(b.height-18,(-.5*Ve.y+.5)*b.height))+"px",be.classList.toggle("is-studio-label",Xe==="hero"),be.dataset.engraved=String(!!me.engraved)}let I=0,$=0,de=ie=>ie&&(ie.minX<-.97||ie.maxX>.97||ie.minY<-.94||ie.maxY>.94);if(Xe==="hero"){for(let ie of[H,N,...q])ie.visible&&de(Vc(ie,f))&&$++;for(let ie of He.values())ie.heroVisible&&de(Vc(ie.group,f))&&$++}for(let[ie,me]of Object.entries(ue))for(let be of me.controls||[]){let Ve=i.querySelector(`.scene-control[data-feature="${be.feature}"]`);if(!Ve)continue;let $e=ie===Xe&&Ue[ie]>.85;if(Ve.hidden=!$e,!$e)continue;de(Vc(be.group,f))&&$++;let Ae=be.group.localToWorld(be.anchor.clone()).project(f),Be=(Ae.x*.5+.5)*b.width,Ye=(-Ae.y*.5+.5)*b.height;(Be<48||Be>b.width-48||Ye<22||Ye>b.height-22)&&I++,Ve.style.left=Be+"px",Ve.style.top=Ye+"px",Ve.dataset.engraved=String(!!be.engraved);let ye=be.group.localToWorld((be.pickAnchor||new L(0,.39,.16)).clone()).project(f);Ve.dataset.meshX=String(b.left+(ye.x*.5+.5)*b.width),Ve.dataset.meshY=String(b.top+(-ye.y*.5+.5)*b.height)}return e.dataset.controlsClipped=String(I),e.dataset.geometryClipped=String($),i.classList.contains("three-ready")||(i.classList.add("three-ready"),i.dispatchEvent(new CustomEvent("videograbber:three-ready"))),!0},Bi=b=>{let I=Math.max(0,b-Fi),$=Math.min(I/1e3,.25),de=1-Math.exp(-$*10);Fi=b,Nt+=$,Me(I),ce.lerp(Se,s?1:.075),sn=ht.lerp(sn,Ui,.055);let ie=Ie[Xe]||Ie.hero;Yt=ht.lerp(Yt,ie.rootScale,.055),Bs=ht.lerp(Bs,ie.rootY,.055),Li=ht.lerp(Li,ie.cameraZ,.055),Di=ht.lerp(Di,ie.orbitScale,.055);let me=a?.48:1,be=ce.x*.38*me,Ve=se.y+ce.y*.24*me+sn*.16*me,$e=Li-sn*.2*me+Math.abs(ce.x)*.035;f.position.x=ht.lerp(f.position.x,be,.055),f.position.y=ht.lerp(f.position.y,Ve,.055),f.position.z=ht.lerp(f.position.z,$e,.055),De.set(0,-sn*.1*me,0),f.lookAt(De),S.rotation.y=ht.lerp(S.rotation.y,ce.x*.18*me+sn*.035,.055),S.rotation.x=ht.lerp(S.rotation.x,-.04-ce.y*.11*me+sn*.022,.055),S.position.y=ht.lerp(S.position.y,Bs-sn*.05*me,.055),S.scale.setScalar(Yt),H.visible=Xe==="hero",N.visible=Xe==="hero";for(let Be of q)Be.visible=Xe==="hero",Be.scale.setScalar(Di);E.lerp(C,.065);for(let[Be,Ye]of Object.entries(ue)){let ye=Xe===Be?1:0;Ue[Be]=ht.lerp(Ue[Be],ye,de);let tt=Ue[Be];Ye.group.visible=tt>.015;let Te=Be==="workflow"||Be==="pricing"?o?Ye.mobileScale*c:Ye.desktopScale:a?Ye.mobileScale:Ye.desktopScale;Ye.group.scale.setScalar((.94+tt*.06)*(Te||1)),U_(Ye.group,tt)}let Ae=ue.pricing;if(Ae?.cards)for(let[Be,Ye]of Ae.cards){let ye=Xe==="pricing"&&(Ne===Be||le==="pricing-"+Be),tt=Ye.baseScale*(ye?1.06:1);Ye.group.scale.x=ht.lerp(Ye.group.scale.x,tt,.12),Ye.group.scale.y=ht.lerp(Ye.group.scale.y,tt,.12),Ye.group.scale.z=ht.lerp(Ye.group.scale.z,tt,.12),Ye.bodyMaterial.emissiveIntensity=ht.lerp(Ye.bodyMaterial.emissiveIntensity,ye?.78:.2,.1)}B.emissive.copy(E).multiplyScalar(.035),V.color.copy(E).lerp(U,.38),W.color.copy(E).lerp(U,.18),Q.material.color.copy(E).lerp(U,.28),j.material.color.copy(E).lerp(U,.45),Re[0].color.copy(E).lerp(Ce,.28),Re[1].color.copy(E).lerp(lt,.45),Re[2].color.copy(E).lerp(je,.65);for(let Be of Re)Be.emissive.copy(Be.color);if(!s){H.rotation.y=-.48+Math.sin(Nt*.3)*.055,H.position.y=.2+Math.sin(Nt*.55)*.045,X.rotation.y-=$*.05,re.rotation.y+=$*.026,j.rotation.y+=$*.036,Oe.rotation.z=0,q[0].rotation.z=-.4+Math.sin(Nt*.22)*.035,q[1].rotation.z=-.43+Math.sin(Nt*.22)*.035,q[2].rotation.z=-.46+Math.sin(Nt*.22)*.035;let Be=0;for(let[Ye,ye]of He){let tt=Xe==="hero"&&ye.heroVisible?1:0;ye.bodyMaterial.opacity=ht.lerp(ye.bodyMaterial.opacity,tt*.94,.11),ye.label.material.opacity=ht.lerp(ye.label.material.opacity,tt,.11),ye.group.visible=tt>0||ye.bodyMaterial.opacity>.02||ye.label.material.opacity>.02;let gt=Xe==="hero"&&K===Ye,Te=ye.heroVisible?gt?.76:.72:1,It=1+Math.max(ye.basePosition.z,0)*.24,at=Math.sin(Nt*.78+Be*.9)*(gt?.085:.055),Zt=Math.cos(Nt*.56+Be*.7)*.035;ye.group.scale.x=ht.lerp(ye.group.scale.x,Te,.13),ye.group.scale.y=ht.lerp(ye.group.scale.y,Te,.13),ye.group.scale.z=ht.lerp(ye.group.scale.z,Te,.13),ye.bodyMaterial.emissiveIntensity=ht.lerp(ye.bodyMaterial.emissiveIntensity,gt?.9:.16,.11),ye.bodyMaterial.emissive.copy(gt?E:ye.baseAccent).multiplyScalar(gt?.48:.3);let rn=gt?-Math.sign(ye.basePosition.x||1)*.16:0;ye.group.position.x=ht.lerp(ye.group.position.x,ye.basePosition.x+rn+ce.x*.045*It,.1),ye.group.position.y=ht.lerp(ye.group.position.y,ye.basePosition.y+at-ce.y*.035*It,.12),ye.group.position.z=ht.lerp(ye.group.position.z,ye.basePosition.z+(gt?.5:0)+Zt,.12),ye.group.rotation.x=ht.lerp(ye.group.rotation.x,gt?ye.baseRotation.x*.2:ye.baseRotation.x,.1),ye.group.rotation.y=ht.lerp(ye.group.rotation.y,gt?ye.baseRotation.y*.15:ye.baseRotation.y+ce.x*.012,.1),ye.group.rotation.z=ht.lerp(ye.group.rotation.z,ye.baseRotation.z+Math.sin(Nt*.52+Be)*.01,.1);for(let En of ye.details||[])En.material.opacity=ye.bodyMaterial.opacity;Be+=1}et.forEach((Ye,ye)=>{Ye.position.y=ct[ye][1]+Math.sin(Nt*.42+ye)*.1}),A.rotation.y=Nt*.012-ce.x*.03,A.rotation.x=ce.y*.018,A.position.x=-ce.x*.1,A.position.y=ce.y*.055+sn*.07,_.rotation.z=Math.sin(Nt*.045)*.025+ce.x*.01,_.position.x=-ce.x*.08,_.position.y=ce.y*.045+sn*.04,ue.workflow.group.visible&&(ue.workflow.group.rotation.y=ht.lerp(ue.workflow.group.rotation.y,ce.x*.025,.05)),ue.sync.group.visible&&(ue.sync.group.rotation.y=ht.lerp(ue.sync.group.rotation.y,ce.x*.035,.05)),ue.windows.group.visible&&(ue.windows.group.rotation.y=ht.lerp(ue.windows.group.rotation.y,ce.x*.045,.05))}hn()},_i=0,jn=()=>{if(Ot||u||h||d||l.getContext().isContextLost()||r||s&&!t||!Hn||!Ni||document.hidden){e.dataset.animation=Ot?"running":r?"visual-test":s?"reduced-motion":document.hidden?"background":Hn?"stage-paused":"offscreen",hn();return}Ot=!0,e.dataset.animation="running",Fi=performance.now(),_i=0,xe.length=0,l.setAnimationLoop(b=>{if(!(a&&_i&&b-_i<32)){_i=b;try{Bi(b)}catch{g("render_failed")}}})},bn=()=>{Ot&&(Ot=!1,e.dataset.animation="paused",xe.length=0,l.setAnimationLoop(null))},Zr=b=>{e.dataset.motionChanges=String(Number(e.dataset.motionChanges||0)+1),e.dataset.motionReduced=String(b),s=b,b&&!t||u||h||d||l.getContext().isContextLost()?(bn(),e.hidden=!0,i.classList.remove("three-ready")):(hn()&&(e.hidden=!1),jn())},zi=b=>Zr(typeof b?.matches=="boolean"?b.matches:n.matches);n.addEventListener("change",zi);let Jr=new Map(we.map(b=>[b.feature,new Fe(b.accent)])),kn=b=>{le!==b&&(le=b||null,K=le,i.dataset.raycastFeature=le||"",le&&Jr.has(le)?C.copy(Jr.get(le)):C.setRGB(.28,.62,1,yt),i.classList.toggle("three-card-hover",!!le),Ot||hn())},y=(b,I)=>{let $=e.getBoundingClientRect();if(b<$.left||b>$.right||I<$.top||I>$.bottom)return kn(null),null;ae.x=(b-$.left)/$.width*2-1,ae.y=-((I-$.top)/$.height)*2+1,te.setFromCamera(ae,f);let de=S_(rt,Xe),me=te.intersectObjects(de,!1)[0]?.object?.userData?.feature||null;return kn(me),me};i.addEventListener("pointermove",b=>{if(b.isPrimary===!1)return;let I=i.getBoundingClientRect();Se.x=((b.clientX-I.left)/I.width-.5)*2,Se.y=((b.clientY-I.top)/I.height-.5)*2,y(b.clientX,b.clientY),oe&&(oe.moved=Math.max(oe.moved,Math.hypot(b.clientX-oe.x,b.clientY-oe.y)))}),i.addEventListener("pointerdown",b=>{oe=null,!(b.button!==0||b.isPrimary===!1||b.target.closest?.("button,a"))&&(oe={id:b.pointerId,x:b.clientX,y:b.clientY,moved:0},y(b.clientX,b.clientY))}),i.addEventListener("pointerup",b=>{let I=oe;if(oe=null,b.target.closest?.("button,a")||!I||I.id!==b.pointerId||b.button!==0||b.isPrimary===!1||I.moved>10)return;let $=y(b.clientX,b.clientY);if(!$)return;if($.startsWith("workflow-")||$.startsWith("pricing-")||$.startsWith("device-")||$==="windows-app"){i.dispatchEvent(new CustomEvent("videograbber:scene-action",{detail:{feature:$}}));return}document.querySelector('.hero-hotspot[data-feature="'+CSS.escape($)+'"]')?.click()}),i.addEventListener("pointercancel",()=>{oe=null,kn(null)}),i.addEventListener("pointerleave",()=>{Se.set(0,0),oe=null,kn(null)}),i.addEventListener("videograbber:story-visibility",b=>{typeof b.detail?.reducedMotion=="boolean"&&(b.detail.reducedMotion!==s||e.hidden!==(b.detail.reducedMotion&&!t))&&Zr(b.detail.reducedMotion),Ni=b.detail?.visible!==!1,Ni&&Hn&&!document.hidden?jn():bn()}),i.addEventListener("videograbber:story-state",b=>{let I=String(b.detail?.state||"hero");if(!Object.prototype.hasOwnProperty.call(Ie,I))return;Xe=I,Ie[I].cameraZ=ed(I,f.aspect),Gt=Os(b.detail?.progress||0),kn(null);let $=new Fe(ft[I]||ft.hero);C.copy($),Ot||hn()}),i.addEventListener("videograbber:story-progress",b=>{String(b.detail?.state||"")===Xe&&(Gt=Os(b.detail?.progress||0))}),i.addEventListener("videograbber:pricing-focus",b=>{Ne=b.detail?.plan||null}),i.addEventListener("videograbber:scene-focus",b=>kn(b.detail?.feature||null)),i.addEventListener("videograbber:hero-accent",b=>{let I=b.detail?.accent;Array.isArray(I)&&I.length===3&&C.setRGB(Os(I[0]),Os(I[1]),Os(I[2]),yt),K=b.detail?.feature||null,Ot||hn()});let F=new ResizeObserver(()=>{Vn(),hn()});F.observe(i);let G=new IntersectionObserver(([b])=>{Hn=!!b?.isIntersecting,Hn?jn():bn()},{rootMargin:"180px 0px"});G.observe(i),document.addEventListener("visibilitychange",()=>{document.hidden?bn():Hn&&jn()}),e.addEventListener("webglcontextlost",b=>{b.preventDefault(),h=!0,bn(),p(),e.dataset.context="lost",ll(e,"context_lost")},!1),e.addEventListener("webglcontextrestored",()=>{if(!u)try{h=!1,d=!1,m.dispose(),m=td(l),v.environment=m.texture,Vn(),zi(),(e.dataset.context==="ready"||s&&!t)&&ll(e,"context_restored")}catch{g("restoration_failed")}}),Vn(),t&&(Xe="hero",Gt=0,Nt=2.75,ce.set(0,0),Se.set(0,0),S.rotation.set(-.04,.08,0),H.rotation.y=-.48,X.rotation.y=-.18,j.rotation.y=.12,q[0].rotation.z=-.4,q[1].rotation.z=-.43,q[2].rotation.z=-.46),hn(),jn(),e.dataset.lifecycle="active",window.addEventListener("pageshow",b=>{b.persisted&&!u&&(e.dataset.lifecycle="active",Vn(),zi())}),window.addEventListener("pagehide",b=>{e.dataset.lifecycle=b.persisted?"bfcache":"disposed",bn(),!(b.persisted||u)&&(u=!0,G.disconnect(),F.disconnect(),n.removeEventListener("change",zi),window.removeEventListener("scroll",Oi),sd(v),ve.dispose(),m.dispose(),l.dispose())})}function ll(i,e){window.dispatchEvent(new CustomEvent("videograbber:webgl",{detail:{event:e,state:i.dataset.storyState||"hero",quality:i.dataset.quality,dpr:Number(i.dataset.dpr||0)}}))}function S_(i,e){return i.filter(t=>{if(e&&t.userData.storyState!==e)return!1;for(let n=t;n;n=n.parent)if(!n.visible)return!1;return!0})}function b_(i,e=!1){let t=[new he(-1.1,-1.55),new he(1.65,0),new he(-1.1,1.55)];e&&t.reverse(),t.forEach(r=>r.multiplyScalar(i));let n=new $n,s=.34*i;for(let r=0;r<t.length;r++){let a=t[r],o=t[(r+t.length-1)%t.length],c=t[(r+1)%t.length],l=a.clone().add(o.clone().sub(a).normalize().multiplyScalar(s)),h=a.clone().add(c.clone().sub(a).normalize().multiplyScalar(s));r===0?n.moveTo(l.x,l.y):n.lineTo(l.x,l.y),n.quadraticCurveTo(a.x,a.y,h.x,h.y)}return n.closePath(),n}function Gc(i,e){let t=b_(i).getSpacedPoints(360);t.pop();let n=[],s=[],r=28,a=t.length;for(let l of[1,-1])for(let h=0;h<=r;h++){let d=h/r,u=l*e*Math.sqrt(Math.max(0,1-d*d));for(let p of t)n.push(-.12*i+(p.x+.12*i)*d,p.y*d,u)}let o=(r+1)*a;for(let l=0;l<2;l++){let h=l*o;for(let d=0;d<r;d++)for(let u=0;u<a;u++){let p=h+d*a+u,g=h+d*a+(u+1)%a,v=p+a,m=g+a;l===0?s.push(p,m,g,p,v,m):s.push(p,g,m,p,m,v)}}let c=new Mt;return c.setAttribute("position",new it(n,3)),c.setIndex(s),c.computeVertexNormals(),c}function E_(i){let e=new bt,t=Gc(1,.54);e.add(new Ke(t,i));let n=Gc(.54,.14);n.translate(.07,0,.65);let s=new on({color:13293539,metalness:.88,roughness:.2,clearcoat:1,clearcoatRoughness:.12,envMapIntensity:2.7});return e.add(new Ke(n,s)),e.position.set(-.65,.2,.35),e.rotation.set(.12,-.48,-.08),e.scale.setScalar(1.55),e}function T_(){let i=new bt;i.position.set(.95,-.62,-.28),i.rotation.set(.02,-.18,-.025);let e=new on({color:1057600,metalness:.68,roughness:.22,clearcoat:1,clearcoatRoughness:.1}),t=new yn(Pi(2.65,1.68,.2),{depth:.1,bevelEnabled:!0,bevelThickness:.05,bevelSize:.05,bevelSegments:8,curveSegments:24});i.add(new Ke(t,e));let n=new on({color:6601471,emissive:1399722,emissiveIntensity:.28,metalness:.45,roughness:.2,clearcoat:1});for(let s=0;s<29;s++){let r=.13+.52*Math.pow(Math.sin(s*.7),2)*Math.exp(-Math.pow((s-14)/18,2)),a=new Ke(new Ms(.018,r,5,10),n);a.position.set(-1.05+s*.074,-.16,.19),i.add(a)}return i}function ed(i,e){return i==="bonus"?Math.max(10,8.5/e):Math.max(i==="hero"?12.2:i==="sync"||i==="windows"?15:10.6,14.2/e)}function Vc(i,e){i.updateWorldMatrix(!0,!0);let t=1/0,n=1/0,s=-1/0,r=-1/0;return i.traverseVisible(a=>{if(!a.isMesh||!a.geometry)return;a.isInstancedMesh?a.boundingBox||a.computeBoundingBox():a.geometry.boundingBox||a.geometry.computeBoundingBox();let o=a.isInstancedMesh?a.boundingBox:a.geometry.boundingBox;if(o)for(let c of[o.min.x,o.max.x])for(let l of[o.min.y,o.max.y])for(let h of[o.min.z,o.max.z]){let d=new L(c,l,h).applyMatrix4(a.matrixWorld).project(e);t=Math.min(t,d.x),s=Math.max(s,d.x),n=Math.min(n,d.y),r=Math.max(r,d.y)}}),Number.isFinite(t)?{minX:t,minY:n,maxX:s,maxY:r}:null}function Wc(i,e){let t=id({...e,icon:"",title:"",subtitle:""},i);t.bodyMaterial.color.setHex(1058360),t.bodyMaterial.metalness=.55,t.bodyMaterial.roughness=.4,t.bodyMaterial.clearcoat=.35,t.bodyMaterial.envMapIntensity=.65,t.bodyMaterial.emissiveIntensity=.07,t.texture.dispose();let n=document.createElement("canvas");n.width=1024,n.height=512;let s=n.getContext("2d");s.textAlign="center",s.fillStyle="#f0f7ff",s.font='700 190px "Manrope", sans-serif',s.fillText(e.title,512,355,930),e.subtitle&&(s.font='500 106px "Onest", sans-serif',s.fillStyle="#b8cedd",s.fillText(e.subtitle,512,470,930));let r=new Un(n);r.colorSpace=yt,r.anisotropy=Math.min(i.capabilities.getMaxAnisotropy(),16),t.texture=r,t.label.material.map=r,t.label.material.opacity=1,t.label.position.z=.145,t.engraved=!0,t.details=[];let a=new qt({color:e.accent,metalness:.62,roughness:.38}),o=(l,h,d,u,p=.18)=>{let g=new Ke(l,h);return g.position.set(d,u,p),t.group.add(g),t.details.push(g),g},c=e.theme;if(c==="mp3")for(let l=0;l<23;l++){let h=.035+Math.abs(Math.sin(l*.78))*.19;o(new Ms(.012,h,3,8),a.clone(),(l-11)*.045,.25)}else if(c==="video"){o(new Dt(.95,.34,.04),new qt({color:530213,roughness:.8}),0,.24);for(let h=0;h<6;h++)for(let d of[.1,.38])o(new Dt(.075,.035,.02),a.clone(),(h-2.5)*.145,d,.21);let l=new $n;l.moveTo(-.08,-.095),l.lineTo(.11,0),l.lineTo(-.08,.095),l.closePath(),o(new yn(l,{depth:.028,bevelEnabled:!0,bevelSize:.009,bevelThickness:.009,bevelSegments:4}),new qt({color:14676735,metalness:.25,roughness:.5}),0,.24,.22)}else if(c==="course")for(let l=0;l<3;l++){let h=new bt;h.position.set((l-1)*.23,.24,.19),h.rotation.z=(1-l)*.08;let d=new Ke(new Dt(.17,.29,.055),new qt({color:14280171,roughness:.85}));h.add(d);let u=new Ke(new Dt(.2,.32,.015),new qt({color:[4358054,7563949,5545644][l],roughness:.52,metalness:.15}));u.position.z=.045,h.add(u),t.group.add(h),h.traverse(p=>{p.isMesh&&t.details.push(p)})}else if(c==="link")for(let l of[-.075,.075]){let h=o(new wi(.09,.025,12,32),a.clone(),l,.24,.21);h.rotation.z=-.4}else if(c==="format")for(let l=0;l<3;l++){let h=o(new Dt(.14,.25,.045),a.clone(),(l-1)*.2,.24,.2+l*.014);h.rotation.z=(1-l)*.12}else if(c==="file"){o(new Dt(.25,.28,.035),new qt({color:13427695,roughness:.65,metalness:.2}),0,.24,.2);for(let l=0;l<3;l++)o(new Dt(.14,.015,.008),a.clone(),0,.29-l*.06,.224)}else if(c?.startsWith("plan-")){let l={free:"free",start:"start",unlimited_video:"unlimited",full_course:"course"}[c.slice(5)],h=new Rr().load("/assets/studio-plan-"+l+".webp");h.colorSpace=yt,o(new Kn(.47,.47),new Xt({map:h,transparent:!0}),0,.25,.19)}for(let l of t.details)l.userData.feature=e.feature,l.material.transparent=!0;for(let l of t.group.children)l!==t.body&&l!==t.label&&(l.position.y+=.06);return t}function w_(){let i=new bt,e=new on({color:2260192,metalness:.38,roughness:.16,clearcoat:1,clearcoatRoughness:.12,envMapIntensity:1.5}),t=new on({color:12967405,metalness:.94,roughness:.18,clearcoat:.8,clearcoatRoughness:.14,envMapIntensity:1.6}),n=(o,c,l)=>{let h=new Ke(new Ss(o,o,c,72),l);return h.rotation.x=Math.PI/2,h},s=new wi(.48,.032,12,64),r=Gc(.17,.045),a=(o,c,l,h,d=1)=>{let u=new bt;u.position.set(o,c,l),u.rotation.set(...h),u.scale.setScalar(d);let p=n(.51,.115,t);p.userData.bonusToken=!0,u.add(p);let g=n(.46,.128,e);g.position.z=.012,u.add(g);let v=new Ke(s,t);v.position.z=.072,u.add(v);let m=new Ke(r,t);m.position.set(.015,0,.097),m.userData.referralPlay=!0,u.add(m),i.add(u)};for(let o=0;o<5;o++)a(-.48,-.42+o*.12,-.18,[-1.08,.035*o,.09-o*.035]);for(let o=0;o<3;o++)a(.53,-.43+o*.12,-.3,[-1.08,-.04*o,-.1+o*.045]);return a(-1.04,-.52,.4,[-1.18,-.12,-.18],.92),a(.82,-.51,.61,[-1.11,.17,.15],.92),a(.08,.07,.78,[-.18,-.25,-.24],1.03),i.rotation.set(.16,-.23,.025),{group:i,controls:[],desktopScale:.85,mobileScale:1}}function id(i,e){let t=new bt;t.position.set(...i.position),t.rotation.set(...i.rotation);let n=Pi(2.12,1.08,.18),s=new yn(n,{depth:.11,bevelEnabled:!0,bevelThickness:.045,bevelSize:.045,bevelSegments:4,curveSegments:12});s.center();let r=new Fe(i.accent),a=new on({color:727092,emissive:r.clone().multiplyScalar(.04),emissiveIntensity:.12,metalness:.14,roughness:.26,clearcoat:.92,clearcoatRoughness:.14,transparent:!0,opacity:.94,side:nn}),o=new Ke(s,a);o.userData.feature=i.feature,t.add(o);let c=N_(i),l=new Un(c);l.colorSpace=yt,l.anisotropy=Math.min(e.capabilities.getMaxAnisotropy(),16),l.minFilter=On,l.magFilter=At,l.generateMipmaps=!0;let h=new Ke(new Kn(1.94,.92),new Xt({map:l,transparent:!0,depthWrite:!1,toneMapped:!1}));return h.position.z=.115,h.userData.feature=i.feature,t.add(h),{group:t,body:o,label:h,bodyMaterial:a,texture:l,baseAccent:r,basePosition:new L(...i.position),baseRotation:new _n(...i.rotation)}}function A_(i){return{workflow:R_(i),sync:C_(i),pricing:I_(i),windows:P_(i),bonus:w_()}}function R_(i){let e=new bt;e.position.set(0,0,.18);let t=[],n=[["workflow-url","\u0421\u0441\u044B\u043B\u043A\u0430","\u041A\u0430\u043A \u043F\u043E\u0434\u0433\u043E\u0442\u043E\u0432\u0438\u0442\u044C","link"],["workflow-format","\u0424\u043E\u0440\u043C\u0430\u0442","\u0427\u0442\u043E \u0432\u044B\u0431\u0440\u0430\u0442\u044C","format"],["workflow-download","\u0424\u0430\u0439\u043B","\u041A\u0430\u043A \u0441\u043E\u0445\u0440\u0430\u043D\u0438\u0442\u044C","file"]];for(let[s,[r,a,o,c]]of n.entries()){let l=Wc(i,{feature:r,title:a,subtitle:o,theme:c,position:[(s-1)*2.15,.05,.3],rotation:[.02,(1-s)*.08,0],accent:6800623});l.group.scale.setScalar(.82),e.add(l.group),t.push({feature:r,group:l.group,anchor:new L(0,-.18,.2),engraved:!0})}return{group:e,controls:t,desktopScale:2.25,mobileScale:1.48}}function cl(i,e){let t=document.createElement("canvas");t.width=768,t.height=e==="telegram"?1280:480;let n=t.getContext("2d");n.fillStyle=e==="telegram"?"#e9f3fc":"#09182d",n.fillRect(0,0,t.width,t.height),n.fillStyle=e==="telegram"?"#168dcc":"#142e50",n.fillRect(0,0,t.width,100),n.fillStyle="#ffffff",n.font='700 42px "Onest", sans-serif',n.fillText(e==="telegram"?"Telegram":"VideoGrabber",100,65),n.fillStyle=e==="telegram"?"#174566":"#c3daf2",n.font='600 46px "Onest", sans-serif',n.fillText(e==="telegram"?"VideoGrabber":e==="web"?"\u041D\u0430 \u0441\u0430\u0439\u0442\u0435":"\u0414\u043B\u044F Windows",40,176),n.font='400 32px "Onest", sans-serif',n.fillText(e==="telegram"?"\u0411\u043E\u0442 \u0438 Mini App":"\u0412\u0438\u0434\u0435\u043E \xB7 MP3 \xB7 \u041A\u0443\u0440\u0441\u044B",40,228),e==="telegram"?(n.fillStyle="#59b9eb",n.beginPath(),n.arc(384,500,220,0,Math.PI*2),n.fill(),n.fillStyle="#24415a",n.font='500 36px "Onest", sans-serif',n.fillText("\u041E\u0434\u0438\u043D \u0430\u043A\u043A\u0430\u0443\u043D\u0442",64,850),n.fillText("\u0421\u0430\u0439\u0442 \xB7 Windows \xB7 Telegram",64,912),n.fillStyle="#168dcc",n.beginPath(),n.roundRect(40,1e3,688,100,26),n.fill(),n.fillStyle="#ffffff",n.fillText("\u041E\u0442\u043A\u0440\u044B\u0442\u044C VideoGrabber",74,1065)):(n.fillStyle="#284f79",n.beginPath(),n.roundRect(40,280,688,60,14),n.fill(),n.fillStyle="#8fb1d2",n.font='400 28px "Onest", sans-serif',n.fillText("\u0421\u0441\u044B\u043B\u043A\u0430 \u043D\u0430 \u0432\u0438\u0434\u0435\u043E",65,320),n.fillStyle="#1578d0",n.beginPath(),n.roundRect(470,372,258,64,16),n.fill(),n.fillStyle="#ffffff",n.font='600 28px "Onest", sans-serif',n.fillText("\u0421\u043A\u0430\u0447\u0430\u0442\u044C",540,414));let s=new Un(t);s.colorSpace=yt,s.anisotropy=Math.min(i.capabilities.getMaxAnisotropy(),8);let r=new Image;return r.onload=()=>{n.drawImage(r,30,22,60,60),e==="telegram"&&n.drawImage(r,224,340,320,320),s.needsUpdate=!0},r.src=e==="telegram"?"/assets/icons/telegram.svg":"/assets/videograbber-icon.png",s}function hl(i,e,t,n=!1){let s=new bt,r=n?.085:.07,a=.012,o=n?.075:.055,c=new on({color:11779786,metalness:.86,roughness:.46,clearcoat:.16,clearcoatRoughness:.45,envMapIntensity:.65}),l=new qt({color:1383461,metalness:.35,roughness:.7}),h=(f,S,R,M=.06)=>new yn(Pi(f,S,M),{depth:R,bevelEnabled:!0,bevelThickness:a,bevelSize:a,bevelSegments:5,curveSegments:24}),d=new Ke(h(i,e,r,n?.065:.11),c);d.name="display-chassis",s.add(d);let u=new Tr(Pi(i-o,e-o,.075)),p=u.attributes.position,g=u.attributes.uv;for(let f=0;f<p.count;f++)g.setXY(f,p.getX(f)/(i-o)+.5,p.getY(f)/(e-o)+.5);let v=new Ke(u,new Xt({map:t,color:16777215}));v.position.z=r+a+.006,v.name="device-screen",s.add(v);let m=new Ke(new Dt(n?.035:i<1?.18:.04,.02,.008),l);if(m.position.set(0,e/2-.022,v.position.z+.006),s.add(m),n){let R=-e/2-.05,M=new yn(Pi(i+.12,1.72,.08),{depth:.06,bevelEnabled:!0,bevelThickness:.012,bevelSize:.012,bevelSegments:5,curveSegments:24}),T=new Ke(M,c.clone());T.name="chassis-deck",T.rotation.x=-Math.PI/2,T.position.set(0,R,.86),s.add(T);let w=R+.06+.012,P=new yn(Pi((i-.38)/10-.035,.115,.025),{depth:.01,bevelEnabled:!0,bevelThickness:.003,bevelSize:.003,bevelSegments:3,curveSegments:8}),x=new hr(P,new qt({color:1515821,metalness:.1,roughness:.88}),40);x.name="keyboard";let E=new Tt;E.rotation.x=-Math.PI/2;for(let H=0;H<40;H++)E.position.set((H%10-4.5)*(i-.38)/10,w+.012,.26+Math.floor(H/10)*.185),E.updateMatrix(),x.setMatrixAt(H,E.matrix);s.add(x);let C=new Ke(new Dt(i*.32,.014,.09),l.clone());C.position.set(0,w+.016,.97),s.add(C);let U=new Ke(new yn(Pi(i*.34,.38,.045),{depth:.006,bevelEnabled:!0,bevelThickness:.002,bevelSize:.002,bevelSegments:4,curveSegments:16}),new qt({color:9280682,metalness:.3,roughness:.82}));U.name="trackpad",U.rotation.x=-Math.PI/2,U.position.set(0,w+.011,1.35),s.add(U);let B=new Ke(new Ss(.034,.034,i-.15,20),l.clone());B.rotation.z=Math.PI/2,B.position.set(0,-e/2,.1),s.add(B),s.userData.deckSurfaceY=w}else{let f=new Ke(new Dt(.018,.22,.035),c.clone());f.position.set(i/2+.02,e*.18,r*.5),s.add(f)}return s.userData.deviceDepth=r,s}function C_(i){let e=new bt;e.position.set(0,.1,.1);let t=hl(2.7,1.7,cl(i,"windows"),!0);t.position.set(-1.18,-.42,.25),t.rotation.set(.22,.32,0),e.add(t);let n=hl(.8,1.65,cl(i,"telegram"));n.position.set(1.6,-.38,.5),n.rotation.set(.02,-.35,-.03),e.add(n);let s=hl(1.78,1.22,cl(i,"web"));return s.position.set(.12,1.23,-.55),s.rotation.set(.04,-.28,.035),e.add(s),{group:e,desktopScale:1.65,mobileScale:1.62,controls:[{feature:"device-windows",group:t,anchor:new L(0,-1.5,.65)},{feature:"device-telegram",group:n,anchor:new L(0,-1.14,.2)},{feature:"device-web",group:s,anchor:new L(0,1.16,.15)}]}}function I_(i){let e=new bt;e.position.set(0,0,.1);let t=new Map,n=[];for(let[s,[r,a]]of[["free","Free"],["start","Start"],["unlimited_video","Unlimited"],["full_course","Full Course"]].entries()){let o=Wc(i,{feature:"pricing-"+r,title:a,subtitle:s===0?"10 \u0432\u0438\u0434\u0435\u043E":"\u041F\u043E\u0434\u0440\u043E\u0431\u043D\u0435\u0435",theme:"plan-"+r,position:[(s-1.5)*2.17,.05,.25],rotation:[.02,(1.5-s)*.045,0],accent:s===2?10916319:6601180});o.group.scale.setScalar(.82),o.baseScale=.82,e.add(o.group),t.set(r,o),n.push({feature:"pricing-"+r,group:o.group,anchor:new L(0,-.18,.2),engraved:!0})}return{group:e,cards:t,controls:n,desktopScale:2.25,mobileScale:1.48}}function P_(i){let e=new bt;e.position.set(0,.12,.1);let t=hl(3.25,1.94,cl(i,"windows"),!0);return t.rotation.set(.24,-.3,0),e.add(t),{group:e,desktopScale:1.6,mobileScale:1.65,controls:[{feature:"windows-app",group:t,anchor:new L(0,-1.95,1.05)}]}}function L_(i,e){for(let[t,n]of i.workflow.controls.entries()){let s=e?[[-1.05,.65,.28],[1.05,.65,.28],[0,-.78,.32]][t]:[[-2.15,.05,.28],[0,.05,.4],[2.15,.05,.28]][t];n.group.position.set(...s)}for(let[t,n]of i.pricing.controls.entries())n.group.position.set(e?t%2?1.12:-1.12:(t-1.5)*2.17,e?t<2?.82:-.82:.05,.25)}function D_(i){i.visible=!1;let e=new Set;i.traverse(t=>{let n=Array.isArray(t.material)?t.material:t.material?[t.material]:[];for(let s of n)e.has(s)||(e.add(s),s.transparent=!0,s.userData.storyBaseOpacity=Number.isFinite(s.opacity)?s.opacity:1,s.opacity=0)})}function U_(i,e){i.traverse(t=>{let n=Array.isArray(t.material)?t.material:t.material?[t.material]:[];for(let s of n){let r=s.userData.storyBaseOpacity??1;s.opacity=r*e}})}function kc(i,e){let t=new Fe(i);return new qt({color:t,emissive:t.clone(),emissiveIntensity:.06,metalness:.9,roughness:.1,transparent:!0,opacity:e,depthWrite:!1})}function Pi(i,e,t){let n=i/2,s=e/2,r=Math.min(t,n,s),a=new $n;return a.moveTo(-n+r,-s),a.lineTo(n-r,-s),a.quadraticCurveTo(n,-s,n,-s+r),a.lineTo(n,s-r),a.quadraticCurveTo(n,s,n-r,s),a.lineTo(-n+r,s),a.quadraticCurveTo(-n,s,-n,s-r),a.lineTo(-n,-s+r),a.quadraticCurveTo(-n,-s,-n+r,-s),a}function N_(i){let e=document.createElement("canvas");if(!i.title&&!i.subtitle&&!i.icon)return e.width=2,e.height=2,e;e.width=1350,e.height=630;let t=e.getContext("2d");t.scale(1.5,1.5);let n=t.createLinearGradient(0,0,900,420);n.addColorStop(0,"rgba(24,47,91,.97)"),n.addColorStop(.55,"rgba(13,27,58,.98)"),n.addColorStop(1,"rgba(8,18,39,.99)"),nd(t,6,6,888,408,62),t.fillStyle=n,t.fill();let r="#"+new Fe(i.accent).getHexString();return t.lineWidth=5,t.strokeStyle="rgba(134,174,255,.45)",t.stroke(),t.shadowColor=r,t.shadowBlur=28,t.fillStyle=r,nd(t,54,88,112,112,30),t.fill(),t.shadowBlur=0,t.fillStyle="#ffffff",t.font='800 54px "Segoe UI Symbol","Segoe UI",sans-serif',t.textAlign="center",t.textBaseline="middle",t.fillText(i.icon,110,145),t.textAlign="left",t.fillStyle="#f5f8ff",t.font='800 55px "Segoe UI",sans-serif',t.fillText(i.title,206,142),t.fillStyle="#9fb0d0",t.font='500 31px "Segoe UI",sans-serif',V_(t,i.subtitle,206,196,610,42,2),e}function td(i){let e=new gs,t=new Ke(new Dt(14,12,14),new Xt({color:264466,side:Ut}));e.add(t);let n=[{size:[6,3],position:[.8,.2,5.5],color:[.9,1.4,2.2]},{size:[6,1.2],position:[-1.6,4,3],color:[7,9,12]},{size:[1.2,5],position:[4.8,.6,1],color:[1.8,4,8]},{size:[3,.6],position:[-4,-2,3],color:[8,5,2.5]},{size:[4,2],position:[.5,1,-5],color:[1,1.7,3]}];for(let a of n){let o=new Ke(new Kn(...a.size),new Xt({color:new Fe().setRGB(...a.color),toneMapped:!1,side:nn}));o.position.set(...a.position),o.lookAt(0,0,0),e.add(o)}let s=new Ns(i),r=s.fromScene(e,.04,.1,60);return s.dispose(),sd(e),{texture:r.texture,dispose:()=>r.dispose()}}function F_(){let i=new bt,e=new Un(O_());e.colorSpace=yt;let t=[[-3.9,1.65,-2.9,5.6,3.4,4488703,.18],[4.1,-.25,-3.3,6.2,4,9130751,.14],[.35,-3.1,-3.7,5.2,3,16740280,.09]];for(let[n,s,r,a,o,c,l]of t){let h=new bi({map:e,color:c,transparent:!0,opacity:l,blending:Fn,depthWrite:!1,toneMapped:!1}),d=new _s(h);d.position.set(n,s,r),d.scale.set(a,o,1),d.userData.baseOpacity=l,i.add(d)}return i}function O_(){let i=document.createElement("canvas");i.width=512,i.height=512;let e=i.getContext("2d"),t=e.createRadialGradient(256,256,18,256,256,250);return t.addColorStop(0,"rgba(255,255,255,.92)"),t.addColorStop(.16,"rgba(205,224,255,.52)"),t.addColorStop(.42,"rgba(152,175,255,.20)"),t.addColorStop(.72,"rgba(98,87,190,.07)"),t.addColorStop(1,"rgba(20,25,70,0)"),e.fillStyle=t,e.fillRect(0,0,512,512),i}function B_(i){let e=new Mt,t=new Float32Array(i*3),n=1369948382,s=()=>(n=Math.imul(n,1664525)+1013904223>>>0,n/4294967296);for(let a=0;a<i;a+=1){let o=a*3,c=(s()-.5)*9.4,l=(s()-.5)*6.2,h=-3.4+s()*5.2;Math.abs(c)<1.75&&Math.abs(l)<1.65&&(c+=c>=0?1.85:-1.85),t[o]=c,t[o+1]=l,t[o+2]=h}e.setAttribute("position",new Ht(t,3));let r=new vs({color:7907583,size:.038,sizeAttenuation:!0,transparent:!0,opacity:.42,blending:Fn,depthWrite:!1,toneMapped:!1});return new dr(e,r)}function z_(){let i=document.createElement("canvas");i.width=512,i.height=512;let e=i.getContext("2d"),t=e.createRadialGradient(256,256,70,256,256,250);return t.addColorStop(0,"rgba(90,135,255,.44)"),t.addColorStop(.34,"rgba(78,92,255,.24)"),t.addColorStop(.68,"rgba(111,69,255,.10)"),t.addColorStop(1,"rgba(35,45,110,0)"),e.fillStyle=t,e.fillRect(0,0,512,512),i}function H_(){let i=document.createElement("canvas");i.width=512,i.height=512;let e=i.getContext("2d"),t=e.createRadialGradient(210,180,40,256,256,245);return t.addColorStop(0,"#3473d6"),t.addColorStop(.45,"#17376d"),t.addColorStop(1,"#071327"),e.fillStyle=t,e.beginPath(),e.arc(256,256,238,0,Math.PI*2),e.fill(),e.strokeStyle="rgba(105,174,255,.72)",e.lineWidth=8,e.stroke(),e.shadowColor="#4c9dff",e.shadowBlur=44,e.fillStyle="#8cd4ff",e.font='900 150px "Segoe UI",sans-serif',e.textAlign="center",e.textBaseline="middle",e.fillText("VG",256,270),e.shadowBlur=0,i}function nd(i,e,t,n,s,r){i.beginPath(),i.roundRect(e,t,n,s,r)}function V_(i,e,t,n,s,r,a){let o=String(e).split(/\s+/),c=[],l="";for(let h of o){let d=l?l+" "+h:h;if(i.measureText(d).width<=s){l=d;continue}if(l&&c.push(l),l=h,c.length>=a-1)break}l&&c.length<a&&c.push(l),c.forEach((h,d)=>i.fillText(h,t,n+d*r))}function Os(i){return Math.min(1,Math.max(0,Number(i)||0))}function sd(i){i.traverse(e=>{e.geometry?.dispose?.();let t=Array.isArray(e.material)?e.material:e.material?[e.material]:[];for(let n of t){for(let s of["map","normalMap","roughnessMap","metalnessMap","emissiveMap","alphaMap"])n[s]?.dispose?.();n.dispose?.()}})}
