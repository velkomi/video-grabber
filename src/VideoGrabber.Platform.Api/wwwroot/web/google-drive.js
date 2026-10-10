"use strict";

(function (root) {
  const scope = "https://www.googleapis.com/auth/drive.file";
  const apiBase = "https://www.googleapis.com/drive/v3/";
  const uploadBase = "https://www.googleapis.com/upload/drive/v3/files";
  const chunkSize = 8 * 1024 * 1024;

  function byteCount(value) {
    if (!/^\d+$/.test(String(value))) throw new Error("cloud_size_unknown");
    return BigInt(value);
  }

  function quotaFromAbout(about) {
    const quota = about.storageQuota;
    if (!quota || quota.usage === undefined) throw new Error("cloud_quota_unknown");
    const used = byteCount(quota.usage);
    const limit = quota.limit === undefined ? null : byteCount(quota.limit);
    return { used, limit, free: limit === null ? null : (limit > used ? limit - used : 0n),
      maxFile: about.maxUploadSize === undefined ? null : byteCount(about.maxUploadSize),
      email: String(about.user?.emailAddress || ""), name: String(about.user?.displayName || "") };
  }

  function sourceUrl(value) {
    const url = new URL(value);
    if (url.protocol !== "https:" || url.username || url.password || url.hash)
      throw new Error("cloud_source_invalid");
    return url.href;
  }

  function uploadUrl(value) {
    const url = new URL(value);
    if (url.origin !== "https://www.googleapis.com" || url.pathname !== "/upload/drive/v3/files" ||
        url.username || url.password || url.hash || !url.searchParams.has("upload_id"))
      throw new Error("cloud_upload_invalid");
    return url.href;
  }

  function safeName(value, mediaType) {
    const ext = mediaType === "video/webm" ? ".webm" : ".mp4";
    const clean = String(value || "Видео").replace(/[\u0000-\u001f<>:"/\\|?*]/g, " ")
      .replace(/\s+/g, " ").trim().slice(0, 140);
    return (clean || "Видео").replace(/\.(mp4|webm)$/i, "") + ext;
  }

  function createClient({ fetch: fetcher = root.fetch?.bind(root), now = () => Date.now() } = {}) {
    let token = "", expires = 0, account = "", folder = "", active = false, generation = 0, activeAbort = null;
    function clear() { activeAbort?.abort(); token = ""; expires = 0; account = ""; folder = ""; generation++; }
    function connect(response, accountId) {
      clear();
      if (!accountId || response?.error || !response?.access_token ||
          !String(response.scope || "").split(/\s+/).includes(scope) || !(Number(response.expires_in) > 0))
        throw new Error("cloud_consent_required");
      token = response.access_token;
      expires = now() + Number(response.expires_in) * 1000;
      account = String(accountId);
    }
    function credentials(accountId) {
      if (!account || String(accountId) !== account) throw new Error("cloud_account_changed");
      if (!token || now() >= expires - 30000) throw new Error("cloud_consent_required");
      return { Authorization: "Bearer " + token };
    }
    async function drive(path, accountId, options = {}) {
      const ownGeneration = generation;
      const response = await fetcher(apiBase + path, { ...options, credentials: "omit", redirect: "error",
        headers: { ...options.headers, ...credentials(accountId) } });
      if (ownGeneration !== generation) throw new Error("cloud_account_changed");
      credentials(accountId);
      if (!response.ok) throw new Error(response.status === 401 ? "cloud_consent_required" : "cloud_request_failed");
      const result = await response.json();
      if (ownGeneration !== generation) throw new Error("cloud_account_changed");
      credentials(accountId);
      return result;
    }
    async function readQuota(accountId, signal) {
      return quotaFromAbout(await drive("about?fields=storageQuota,user,maxUploadSize", accountId, { signal }));
    }
    async function ensureFolder(accountId, signal) {
      if (folder) return folder;
      const query = "trashed=false and mimeType='application/vnd.google-apps.folder' and " +
        "appProperties has { key='videograbber' and value='downloads' }";
      const found = await drive("files?q=" + encodeURIComponent(query) + "&fields=files(id)&pageSize=1", accountId, { signal });
      folder = found.files?.[0]?.id || (await drive("files?fields=id", accountId, {
        method: "POST", signal, headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: "VideoGrabber", mimeType: "application/vnd.google-apps.folder",
          appProperties: { videograbber: "downloads" } }) })).id;
      if (!/^[A-Za-z0-9_-]+$/.test(folder || "")) throw new Error("cloud_folder_invalid");
      return folder;
    }
    async function upload({ accountId, url, bytes, mediaType, name, signal, onProgress = () => {} }) {
      if (active) throw new Error("cloud_upload_busy");
      const total = byteCount(bytes);
      if (total <= 0n || total > BigInt(Number.MAX_SAFE_INTEGER)) throw new Error("cloud_size_invalid");
      if (!["video/mp4", "video/webm"].includes(mediaType)) throw new Error("cloud_media_unsupported");
      const ownGeneration = generation;
      const check = () => {
        signal?.throwIfAborted();
        if (generation !== ownGeneration) throw new Error("cloud_account_changed");
        credentials(accountId);
      };
      let reader;
      active = true;
      activeAbort = new AbortController();
      signal = signal ? AbortSignal.any([signal, activeAbort.signal]) : activeAbort.signal;
      try {
        check();
        const quota = await readQuota(accountId, signal);
        if (quota.free !== null && total > quota.free) throw new Error("cloud_quota_exceeded");
        if (quota.maxFile !== null && total > quota.maxFile) throw new Error("cloud_file_too_large");
        // The source receives no Google or VideoGrabber credential. Browser CORS is mandatory.
        const source = await fetcher(sourceUrl(url), { signal, credentials: "omit", redirect: "error", mode: "cors" });
        if (!source.ok || !source.body || source.headers.get("Content-Length") !== total.toString() ||
            source.headers.get("Content-Type")?.split(";")[0].trim().toLowerCase() !== mediaType) {
          try { await source.body?.cancel(); } catch {}
          throw new Error("cloud_source_unavailable");
        }
        reader = source.body.getReader();
        check();
        const parent = await ensureFolder(accountId, signal);
        check();
        const initiated = await fetcher(uploadBase + "?uploadType=resumable&fields=id,name,size,webViewLink", {
          method: "POST", signal, credentials: "omit", redirect: "error",
          headers: { ...credentials(accountId), "Content-Type": "application/json",
            "X-Upload-Content-Type": mediaType, "X-Upload-Content-Length": total.toString() },
          // POST always creates a new file; there is no overwrite or sharing operation.
          body: JSON.stringify({ name: safeName(name, mediaType), parents: [parent] }) });
        if (!initiated.ok) throw new Error("cloud_upload_failed");
        const destination = uploadUrl(initiated.headers.get("Location"));
        let offset = 0, remainder = null, remainderOffset = 0;
        while (BigInt(offset) < total) {
          check();
          const size = Number(total - BigInt(offset) > BigInt(chunkSize) ? BigInt(chunkSize) : total - BigInt(offset));
          const chunk = new Uint8Array(size);
          let filled = 0;
          while (filled < size) {
            if (!remainder || remainderOffset === remainder.length) {
              const next = await reader.read();
              if (next.done) throw new Error("cloud_source_truncated");
              remainder = next.value; remainderOffset = 0;
            }
            const count = Math.min(size - filled, remainder.length - remainderOffset);
            chunk.set(remainder.subarray(remainderOffset, remainderOffset + count), filled);
            filled += count; remainderOffset += count;
          }
          const end = offset + size;
          if (BigInt(end) === total) {
            if (remainderOffset < remainder.length || !(await reader.read()).done)
              throw new Error("cloud_source_size_changed");
          }
          check();
          const sent = await fetcher(destination, { method: "PUT", signal, credentials: "omit", redirect: "error",
            headers: { ...credentials(accountId), "Content-Type": mediaType,
              "Content-Range": `bytes ${offset}-${end - 1}/${total}` }, body: chunk });
          check();
          if (BigInt(end) === total) {
            if (!sent.ok) throw new Error(sent.status === 401 ? "cloud_consent_required" : "cloud_upload_failed");
            const file = await sent.json();
            if (!/^[A-Za-z0-9_-]+$/.test(file.id || "") || byteCount(file.size) !== total)
              throw new Error("cloud_verification_failed");
            onProgress({ sent: total, total });
            return { id: file.id, name: file.name, bytes: total, url: "https://drive.google.com/file/d/" + file.id + "/view" };
          }
          if (sent.status !== 308 || sent.headers.get("Range") !== `bytes=0-${end - 1}`)
            throw new Error("cloud_upload_interrupted");
          offset = end;
          onProgress({ sent: BigInt(offset), total });
        }
        throw new Error("cloud_upload_failed");
      } finally {
        if (reader) { try { await reader.cancel(); } catch {} reader.releaseLock(); }
        active = false;
        activeAbort = null;
      }
    }
    return { connect, clear, readQuota, upload, connected: accountId => {
      try { credentials(accountId); return true; } catch { return false; }
    } };
  }
  const exported = { scope, createClient, quotaFromAbout, safeName, sourceUrl, uploadUrl };
  if (typeof module !== "undefined" && module.exports) module.exports = exported;
  else root.VideoGrabberGoogleDrive = exported;
})(typeof window === "undefined" ? globalThis : window);
