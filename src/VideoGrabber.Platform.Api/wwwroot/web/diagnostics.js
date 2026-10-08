/* Local, bounded STANDARD diagnostics. Never collect text, URLs, bodies or credentials. */
(() => {
  "use strict";
  const events = [];
  let dropped = 0;
  const nativeFetch = window.fetch.bind(window);
  const id = () => crypto.randomUUID().replaceAll("-", "");
  const pageTrace = id();
  const actions = new Set(["header-login", "google-login", "email-login", "logout", "submit-download", "refresh-jobs", "jobs-more", "jobs-collapse",
    "plan-dialog-action", "plan-dialog-close", "plan-dialog-x", "info-dialog-x", "info-dialog-close", "info-dialog-action",
    "plan-free", "plan-start", "plan-unlimited", "plan-course"]);
  const routes = new Set(["/v1/me", "/v1/access", "/v1/devices", "/v1/identities", "/v1/jobs",
    "/v1/auth/refresh", "/v1/auth/logout", "/v1/auth/supabase-config", "/v1/auth/supabase-session",
    "/v1/auth/desktop/start", "/v1/auth/desktop/consume", "/v1/auth/desktop/approve",
    "/v1/subscriptions", "/v1/payments/products", "/v1/payments", "/v1/analyze"]);
  function route(path) {
    if (routes.has(path)) return path;
    if (/^\/v1\/jobs\/[^/]+$/u.test(path)) return "/v1/jobs/{id}";
    if (/^\/v1\/jobs\/[^/]+\/(cancel|download|events)$/u.test(path))
      return "/v1/jobs/{id}/" + path.split("/").at(-1);
    return "unmatched";
  }
  function record(event, outcome, fields = {}) {
    if (events.length === 500) { events.shift(); dropped++; }
    events.push(Object.freeze({schemaVersion:1, timestamp:new Date().toISOString(), service:"web",
      environment:location.hostname === "127.0.0.1" || location.hostname === "localhost" ? "local" : "browser",
      revision:"UNKNOWN", event, outcome, traceId:pageTrace, ...fields}));
  }
  window.fetch = async (input, options = {}) => {
    let target;
    try { target = new URL(input instanceof Request ? input.url : input, location.href); }
    catch { return nativeFetch(input, options); }
    if (target.origin !== location.origin || !target.pathname.startsWith("/v1/")) return nativeFetch(input, options);
    const traceId = id();
    const headers = new Headers(options.headers ?? (input instanceof Request ? input.headers : undefined));
    headers.set("X-Correlation-Id", traceId);
    const candidateMethod = String(options.method ?? (input instanceof Request ? input.method : "GET")).toUpperCase();
    const method = /^(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)$/u.test(candidateMethod) ? candidateMethod : "OTHER";
    const fields = {traceId, parentTraceId:pageTrace, method, route:route(target.pathname)};
    const start = performance.now();
    record("http.request", "started", fields);
    try {
      const response = await nativeFetch(input, {...options, headers});
      record("http.request", response.ok ? "succeeded" : "failed", {...fields,
        status:response.status, durationMs:Math.round(performance.now() - start)});
      return response;
    } catch (error) {
      record("http.request", error?.name === "AbortError" ? "cancelled" : "failed", {...fields,
        errorType:error?.name === "AbortError" ? "AbortError" : "NetworkError", durationMs:Math.round(performance.now() - start)});
      throw error;
    }
  };
  document.addEventListener("click", event => {
    const node = event.target?.closest?.("button,a,.price-card[data-plan]");
    if (!node) return;
    let action = actions.has(node.id) ? node.id : null;
    const plan = node.closest(".price-card[data-plan]")?.dataset.plan;
    if (["free", "start", "unlimited_video", "full_course"].includes(plan)) action = "plan-" + plan;
    const feature = node.dataset?.feature;
    if (["workflow-url","workflow-format","workflow-download","pricing-free","pricing-start","pricing-unlimited_video","pricing-full_course",
      "device-windows","device-web","device-telegram","windows-app"].includes(feature)) action="scene:"+feature;
    if (["link","format","file"].includes(node.dataset?.info)) action="guide:"+node.dataset.info;
    const href = node.getAttribute("href");
    if (["#how", "#app", "#pricing", "#download", "/download/windows", "/download/windows/portable", "/miniapp/"].includes(href))
      action = "navigate:" + href;
    if (action) record("ui.action", "activated", {action});
  }, true);
  document.addEventListener("change", event => {
    if (["operation", "download-target", "device-select"].includes(event.target?.id))
      record("ui.action", "changed", {action:event.target.id});
  }, true);
  window.addEventListener("error", event => record(event.error ? "javascript.error" : "resource.error", "failed", {errorType:"BrowserError"}), true);
  window.addEventListener("unhandledrejection", () => record("javascript.rejection", "failed", {errorType:"UnhandledRejection"}));
  const webglNames = new Set(["context_lost", "context_restored", "initialization_failed", "render_failed", "restoration_failed"]);
  const webglFields = detail => {
    const fields = {};
    if (["hero", "workflow", "sync", "pricing", "windows"].includes(detail.state)) fields.state = detail.state;
    if (["high", "balanced", "low"].includes(detail.quality)) fields.quality = detail.quality;
    if (Number.isFinite(detail.dpr) && detail.dpr >= 0 && detail.dpr <= 4) fields.dpr = detail.dpr;
    return fields;
  };
  try {
    const saved = JSON.parse(window.sessionStorage?.getItem("vg_webgl_incidents") || "[]");
    if (Array.isArray(saved)) for (const item of saved.slice(-20)) {
      if (!item || typeof item.event !== "string" || !webglNames.has(item.event.replace(/^webgl\./u, ""))) continue;
      const name = item.event.replace(/^webgl\./u, "");
      const fields = {...webglFields(item), previousPage:true, replayedAt:new Date().toISOString()};
      if (typeof item.timestamp === "string" && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/u.test(item.timestamp)
        && Number.isFinite(Date.parse(item.timestamp))) fields.timestamp = new Date(item.timestamp).toISOString();
      if (typeof item.traceId === "string" && /^[a-f0-9]{32}$/u.test(item.traceId)) fields.traceId = item.traceId;
      record("webgl." + name, name === "context_restored" ? "succeeded" : "failed",
        fields);
    }
  } catch { }
  window.addEventListener("videograbber:webgl", event => {
    const detail = event.detail || {};
    if (!webglNames.has(detail.event)) return;
    const fields = webglFields(detail);
    record("webgl." + detail.event, detail.event === "context_restored" ? "succeeded" : "failed", fields);
    // Keep only bounded, sanitized graphics incidents across reloads of this tab.
    try { window.sessionStorage?.setItem("vg_webgl_incidents", JSON.stringify(
      events.filter(item => item.event.startsWith("webgl.")).slice(-20))); } catch { }
  });
  function storyEvent(event) {
    const state = event.detail?.state;
    if (["hero", "workflow", "sync", "pricing", "windows"].includes(state)) record("scene.state", "changed", {state});
  }
  record("page.ready", "succeeded");
  window.VGDiagnostics = Object.freeze({snapshot:() => events.slice(), get dropped() { return dropped; }});
  function installPanel() {
    if (new URLSearchParams(location.search).get("diagnostics") !== "1") return;
    const panel = document.createElement("aside");
    panel.className = "diagnostics-panel";
    panel.setAttribute("aria-label", "Диагностика VideoGrabber");
    panel.innerHTML = '<strong>Диагностика · только на этом устройстве</strong><output></output><canvas width="480" height="100" aria-label="Количество событий и ошибок"></canvas><button type="button" class="button button-quiet">Скачать журнал JSONL</button>';
    document.body.append(panel);
    panel.querySelector("button").addEventListener("click", () => {
      const coverage = {schemaVersion:1, timestamp:new Date().toISOString(), service:"web", event:"diagnostics.coverage",
        outcome:dropped ? "incomplete" : "complete", dropped, maxEvents:500, storage:"memory", webVitals:window.__VG_WEB_VITALS ?? null};
      const blob = new Blob([[coverage, ...events].map(e => JSON.stringify(e)).join("\n") + "\n"], {type:"application/x-ndjson"});
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a"); link.href = url; link.download = "videograbber-web-diagnostics.jsonl"; link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    });
    const draw = () => {
      const failures = events.filter(e => e.outcome === "failed").length;
      panel.querySelector("output").textContent = `${events.length} событий · ${failures} ошибок · ${dropped} вытеснено`;
      const ctx = panel.querySelector("canvas").getContext("2d");
      ctx.clearRect(0, 0, 480, 100);
      const recent = events.slice(-60);
      recent.forEach((e, index) => { ctx.fillStyle = e.outcome === "failed" ? "#ff8989" : "#70c4ff";
        ctx.fillRect(index * 8, 100 - (e.durationMs ? Math.min(95, e.durationMs / 10 + 8) : 12), 5,
          e.durationMs ? Math.min(95, e.durationMs / 10 + 8) : 12); });
    };
    draw(); const timer = setInterval(draw, 1500);
    window.addEventListener("pagehide", e => { if (!e.persisted) clearInterval(timer); });
  }
  document.addEventListener("DOMContentLoaded", installPanel, {once:true});
  document.addEventListener("DOMContentLoaded", () => {
    document.querySelector("#hero-visual")?.addEventListener("videograbber:story-state", storyEvent);
    for (const [id,name] of [["info-dialog","info"],["plan-dialog","plan"]]) {
      const dialog = document.querySelector("#"+id);
      if (!dialog) continue;
      new MutationObserver(changes => {
        for (const change of changes)
          record(change.oldValue === null ? "dialog.open" : "dialog.close", "changed", {action:"dialog:"+name});
      }).observe(dialog,{attributes:true,attributeFilter:["open"],attributeOldValue:true});
    }
  }, {once:true});
})();
