import * as THREE from "./vendor/three.module.js";

const visual = document.getElementById("hero-visual");
const canvas = document.getElementById("hero-three");

if (visual && canvas) {
  initThreeHero(visual, canvas).catch((error) => {
    console.warn("VideoGrabber Three.js scene disabled", error);
    canvas.hidden = true;
    visual.classList.remove("three-ready");
    canvas.dataset.context = "failed";
    reportWebgl(canvas, "initialization_failed");
  });
}

async function initThreeHero(visual, canvas) {
  try {
    const faces = await Promise.all([document.fonts.load('700 18px "Manrope"','Ссылка MP4 Курс'),
      document.fonts.load('500 15px "Onest"','Подробнее VideoGrabber Windows Telegram MP3')]);
    if (faces.some(group => group.length === 0)) throw new Error("font_faces_missing");
    canvas.dataset.fonts = "Manrope + Onest";
  } catch { canvas.dataset.fonts = "fallback"; }
  const visualTestMode = Boolean(window.__VG_VISUAL_TEST);
  const motionPreference = matchMedia("(prefers-reduced-motion: reduce)");
  let reducedMotion = motionPreference.matches;
  const staticScene = visualTestMode;
  let narrowViewport = matchMedia("(max-width: 720px)").matches;
  let compactScene = narrowViewport;
  let compactBoost = 1;

  const renderer = new THREE.WebGLRenderer({
    canvas,
    alpha: true,
    antialias: true,
    powerPreference: "high-performance",
    premultipliedAlpha: false
  });
  canvas.dataset.engine = `three.js r${THREE.REVISION}`;
  if (visualTestMode) canvas.dataset.visualTest = "true";
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.2;
  renderer.setClearColor(0x000000, 0);
  let contextLost = false;
  let graphicsFailed = false;
  let disposed = false;
  canvas.dataset.context = "initializing";

  const hideScene = () => {
    canvas.hidden = true;
    visual.classList.remove("three-ready");
  };
  const failScene = (event) => {
    graphicsFailed = true;
    stopLoop();
    hideScene();
    canvas.dataset.context = "failed";
    reportWebgl(canvas, event);
  };

  const scene = new THREE.Scene();
  let proceduralEnvironment = createProceduralEnvironment(renderer);
  scene.environment = proceduralEnvironment.texture;
  scene.environmentIntensity = 0.72;

  const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 60);
  camera.position.set(0, 0.05, 10.45);

  const root = new THREE.Group();
  root.rotation.x = -0.04;
  scene.add(root);

  const hemi = new THREE.HemisphereLight(0x8ab7ff, 0x070b18, .25);
  scene.add(hemi);

  const key = new THREE.DirectionalLight(0xbfdcff, 1.8);
  key.position.set(-4.4, 5.5, 6.8);
  scene.add(key);

  const rim = new THREE.PointLight(0x6fc2ff, 12.5, 20, 2.0);
  rim.position.set(4.2, 2.6, 4.8);
  scene.add(rim);

  const backRim = new THREE.PointLight(0xf1d5b6, 10.5, 18, 2.0);
  backRim.position.set(-4.8, 1.4, -3.2);
  scene.add(backRim);

  const warm = new THREE.PointLight(0xffa05a, 3.5, 14, 2.0);
  warm.position.set(-3.4, -2.8, 5.0);
  scene.add(warm);

  const fill = new THREE.PointLight(0x46b8ff, 5.4, 14, 2.0);
  fill.position.set(0.5, -1.4, 5.5);
  scene.add(fill);

  const accent = new THREE.Color(0x55a3ff);
  const accentTarget = accent.clone();
  const coolBlue = new THREE.Color(0x5e8cff);

  const sphereMaterial = new THREE.MeshPhysicalMaterial({
    color: 0x5aaeff, metalness: 0, roughness: .045,
    transmission: .94, thickness: .85, ior: 1.46,
    attenuationColor: new THREE.Color(0x609bdb), attenuationDistance: 1.6,
    clearcoat: 1, clearcoatRoughness: .08,
    emissive: 0x06234a, emissiveIntensity: .18,
    envMapIntensity: 2.1
  });
  const sphere = createStudioPlayObject(sphereMaterial);
  root.add(sphere);
  const mediaPanel = createStudioMediaPanel();
  root.add(mediaPanel);
  canvas.dataset.model = "studio-play";
  visual.dispatchEvent(new CustomEvent("videograbber:studio-model"));

  const atmosphereMaterial = new THREE.MeshBasicMaterial({
    color: 0x4d8cff,
    transparent: true,
    opacity: 0.075,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    toneMapped: false
  });
  const atmosphere = new THREE.Mesh(
    new THREE.SphereGeometry(1.55, 56, 40),
    atmosphereMaterial
  );
  atmosphere.position.copy(sphere.position);
  atmosphere.visible = false;
  root.add(atmosphere);

  const rimShellMaterial = new THREE.MeshBasicMaterial({
    color: 0x7e88ff,
    transparent: true,
    opacity: 0.16,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    side: THREE.BackSide,
    toneMapped: false
  });
  const rimShell = new THREE.Mesh(
    new THREE.SphereGeometry(1.61, 56, 40),
    rimShellMaterial
  );
  rimShell.position.copy(sphere.position);
  rimShell.visible = false;
  root.add(rimShell);

  const haloTexture = new THREE.CanvasTexture(makeHaloTexture());
  haloTexture.colorSpace = THREE.SRGBColorSpace;
  const halo = new THREE.Sprite(
    new THREE.SpriteMaterial({
      map: haloTexture,
      color: 0x4e74ff,
      transparent: true,
      opacity: 0.42,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      toneMapped: false
    })
  );
  halo.scale.set(4.35, 4.35, 1);
  halo.position.set(0, 0, -0.48);
  halo.visible = false;
  root.add(halo);

  const wire = new THREE.LineSegments(
    new THREE.WireframeGeometry(new THREE.SphereGeometry(1.505, 24, 16)),
    new THREE.LineBasicMaterial({
      color: 0x5aa7ff,
      transparent: true,
      opacity: 0.08,
      depthWrite: false
    })
  );
  wire.visible = false;
  root.add(wire);

  const orbitMaterials = [
    makeOrbitMaterial(0x82c9ff, 0.96),
    makeOrbitMaterial(0xc9d6e2, 0.95),
    makeOrbitMaterial(0x629eff, 0.86)
  ];
  const orbitBlue = new THREE.Color(0x65b5ff);
  const orbitPurple = new THREE.Color(0xbed5e7);
  const orbitPink = new THREE.Color(0x87bce6);

  const orbitDefinitions = [
    { radius: 2.42, tube: 0.125, rotation: [1.10, .14, -.40], material: orbitMaterials[0] },
    { radius: 2.35, tube: 0.068, rotation: [1.07, .15, -.43], material: orbitMaterials[1] },
    { radius: 2.30, tube: 0.018, rotation: [1.04, .16, -.46], material: orbitMaterials[2] }
  ];

  const orbits = orbitDefinitions.map((definition) => {
    const mesh = new THREE.Mesh(
      new THREE.TorusGeometry(
        definition.radius,
        definition.tube,
        12,
        192
      ),
      definition.material
    );
    mesh.rotation.set(...definition.rotation);
    mesh.position.set(-.50, .12, -.3);
    root.add(mesh);
    return mesh;
  });

  const coreCanvas = makeCoreTexture();
  const coreTexture = new THREE.CanvasTexture(coreCanvas);
  coreTexture.colorSpace = THREE.SRGBColorSpace;
  coreTexture.anisotropy = Math.min(renderer.capabilities.getMaxAnisotropy(), 8);
  const core = new THREE.Mesh(
    new THREE.CircleGeometry(0.58, 64),
    new THREE.MeshBasicMaterial({
      map: coreTexture,
      transparent: true,
      depthTest: false,
      depthWrite: false,
      toneMapped: false
    })
  );
  core.position.set(0, 0, 1.625);
  core.renderOrder = 20;
  core.visible = false;
  root.add(core);

  const cardSpecs = [
    {
      feature: "url",
      icon: "↗",
      title: "URL",
      subtitle: "Вставьте ссылку",
      position: [-2.45, 1.58, 0.24],
      rotation: [-0.04, 0.16, -0.14],
      accent: 0x52a7ff
    },
    {
      feature: "video",
      icon: "▶",
      title: "MP4",
      subtitle: "Видео",
      position: [2.65, 1.10, .45],
      rotation: [-0.05, -0.18, 0.13],
      accent: 0x4cbcff
    },
    {
      feature: "mp3",
      icon: "♪",
      title: "MP3",
      subtitle: "Аудио",
      position: [2.65, -.02, .55],
      rotation: [0.01, 0.2, 0.08],
      accent: 0xff9a55
    },
    {
      feature: "course",
      icon: "◇",
      title: "Курс",
      subtitle: "Уроки и материалы",
      position: [2.65, -1.14, .65],
      rotation: [0.02, -0.22, -0.07],
      accent: 0xa55cff
    },
    {
      feature: "windows",
      icon: "▦",
      title: "Windows",
      subtitle: "Удобное приложение",
      position: [-1.72, -1.82, 0.88],
      rotation: [0.04, 0.14, 0.12],
      accent: 0x4ac4ff
    },
    {
      feature: "telegram",
      icon: "➤",
      title: "Telegram",
      subtitle: "Скачивайте через бота",
      position: [2.05, -1.78, 0.34],
      rotation: [0.04, -0.18, -0.12],
      accent: 0x4da8ff
    }
  ];

  const cards = new Map();
  const pickTargets = [];
  for (const mesh of sphere.children) {
    mesh.userData.feature = "video";
    mesh.userData.storyState = "hero";
    pickTargets.push(mesh);
  }
  for (const mesh of mediaPanel.children) {
    mesh.userData.feature = "mp3";
    mesh.userData.storyState = "hero";
    pickTargets.push(mesh);
  }
  for (const spec of cardSpecs) {
    const heroVisible = ["video", "mp3", "course"].includes(spec.feature);
    const card = heroVisible ? createEngravedCard(renderer,{...spec,theme:spec.feature}) : createFeatureCard(spec, renderer);
    root.add(card.group);
    card.heroVisible = ["video", "mp3", "course"].includes(spec.feature);
    card.bodyMaterial.opacity = card.heroVisible ? .94 : 0;
    card.label.material.opacity = card.heroVisible ? 1 : 0;
    card.label.visible = card.heroVisible;
    card.group.visible = card.heroVisible;
    card.baseRotation.set(0, -.12, .02);
    card.group.rotation.copy(card.baseRotation);
    if (card.heroVisible) card.group.scale.setScalar(.72);
    cards.set(spec.feature, card);
    card.body.userData.storyState = "hero";
    card.label.userData.storyState = "hero";
    card.group.traverse(mesh => { if (mesh.isMesh) { mesh.userData.feature=spec.feature; mesh.userData.storyState="hero"; pickTargets.push(mesh); } });
  }

  const raycaster = new THREE.Raycaster();
  const rayPointer = new THREE.Vector2();
  let hoveredFeature = null;
  let pointerDown = null;

  const storyArtifacts = createStoryArtifacts(renderer);
  for (const [name, artifact] of Object.entries(storyArtifacts)) {
    root.add(artifact.group);
    prepareStoryArtifact(artifact.group);
    for (const control of artifact.controls || []) {
      control.group.traverse(mesh => {
        if (!mesh.isMesh) return;
        mesh.userData.feature = control.feature;
        mesh.userData.storyState = name;
        pickTargets.push(mesh);
      });
    }
  }
  const storyOpacity = {
    workflow: 0,
    sync: 0,
    pricing: 0,
    windows: 0
  };
  let pricingFocusPlan = null;

  const particleGroup = new THREE.Group();
  root.add(particleGroup);
  const particleMaterial = new THREE.MeshBasicMaterial({
    color: 0x4b8dff,
    transparent: true,
    opacity: 0.35,
    blending: THREE.AdditiveBlending,
    depthWrite: false
  });
  const particleGeometry = new THREE.SphereGeometry(0.035, 10, 8);
  const particlePositions = [
    [-3.5, 1.05, -0.4, 1.1],
    [3.45, 0.85, -0.5, 0.82],
    [-3.0, -1.55, -0.2, 0.68],
    [3.35, -1.35, -0.35, 0.95],
    [0.3, 2.55, -0.55, 0.74],
    [-0.4, -2.55, -0.6, 0.58]
  ];
  const particles = particlePositions.map(([x, y, z, scale]) => {
    const mesh = new THREE.Mesh(particleGeometry, particleMaterial);
    mesh.position.set(x, y, z);
    mesh.scale.setScalar(scale);
    particleGroup.add(mesh);
    return mesh;
  });

  const depthStars = createDepthStarField(narrowViewport ? 48 : 96);
  scene.add(depthStars);

  const nebulaField = createNebulaField();
  scene.add(nebulaField);

  const qualityProfiles = Object.freeze({
    high: {
      desktopDpr: 1.5,
      mobileDpr: 1.1,
      stars: true,
      particles: true,
      nebula: true,
      decorationOpacity: 1,
      environmentIntensity: 0.72
    },
    balanced: {
      desktopDpr: 1.25,
      mobileDpr: 0.95,
      stars: true,
      particles: true,
      nebula: true,
      decorationOpacity: 0.68,
      environmentIntensity: 0.58
    },
    economy: {
      desktopDpr: 1.0,
      mobileDpr: 0.8,
      stars: false,
      particles: false,
      nebula: false,
      decorationOpacity: 0.42,
      environmentIntensity: 0.44
    }
  });
  const qualityOrder = ["economy", "balanced", "high"];
  const frameSampleWindow = 45;
  const slowWindowsBeforeDowngrade = 2;
  const fastWindowsBeforeUpgrade = 5;
  const deviceMemory = Number(navigator.deviceMemory || 0);
  const hardwareConcurrency = Number(navigator.hardwareConcurrency || 0);
  let qualityName =
    narrowViewport ||
    (deviceMemory > 0 && deviceMemory <= 4) ||
    (hardwareConcurrency > 0 && hardwareConcurrency <= 4)
      ? "balanced"
      : "high";
  let effectiveDpr = 1;
  const frameSamples = [];
  let slowFrameWindows = 0;
  let fastFrameWindows = 0;

  const applyQualityDecorations = () => {
    const profile = qualityProfiles[qualityName];
    depthStars.visible = false;
    particleGroup.visible = false;
    nebulaField.visible = false;
    scene.environmentIntensity = .9;
    atmosphereMaterial.opacity = 0.075 * profile.decorationOpacity;
    rimShellMaterial.opacity = 0.16 * profile.decorationOpacity;
    halo.material.opacity = 0.42 * profile.decorationOpacity;
    wire.material.opacity = 0.08 * profile.decorationOpacity;
    nebulaField.traverse((object) => {
      if (!object.material || !Number.isFinite(object.userData.baseOpacity))
        return;
      object.material.opacity =
        object.userData.baseOpacity * profile.decorationOpacity;
    });
    canvas.dataset.quality = qualityName;
  };

  const updatePixelRatio = () => {
    const profile = qualityProfiles[qualityName];
    const cap = narrowViewport ? profile.mobileDpr : profile.desktopDpr;
    effectiveDpr = Math.min(devicePixelRatio || 1, cap);
    renderer.setPixelRatio(effectiveDpr);
    canvas.dataset.dpr = effectiveDpr.toFixed(2);
  };

  const setQuality = (nextQuality) => {
    if (
      staticScene ||
      !Object.prototype.hasOwnProperty.call(qualityProfiles, nextQuality) ||
      qualityName === nextQuality
    ) return;

    qualityName = nextQuality;
    applyQualityDecorations();
    updatePixelRatio();

    const rect = visual.getBoundingClientRect();
    renderer.setSize(
      Math.max(1, Math.round(rect.width)),
      Math.max(1, Math.round(rect.height)),
      false
    );

    visual.dispatchEvent(new CustomEvent("videograbber:3d-quality", {
      detail: {
        quality: qualityName,
        dpr: effectiveDpr
      }
    }));
  };

  const sampleFrameTime = (frameMs) => {
    if (
      staticScene ||
      document.hidden ||
      frameMs <= 0 ||
      frameMs > 1000
    ) return;

    frameSamples.push(frameMs);
    if (frameSamples.length < frameSampleWindow) return;

    const average =
      frameSamples.reduce((sum, value) => sum + value, 0) /
      frameSamples.length;
    frameSamples.length = 0;
    canvas.dataset.frameMs = average.toFixed(1);

    const slowThreshold = narrowViewport ? 41 : 24;
    const fastThreshold = narrowViewport ? 30 : 17.5;

    if (average > slowThreshold) {
      slowFrameWindows += 1;
      fastFrameWindows = 0;
    } else if (average < fastThreshold) {
      fastFrameWindows += 1;
      slowFrameWindows = 0;
    } else {
      slowFrameWindows = 0;
      fastFrameWindows = 0;
    }

    const currentIndex = qualityOrder.indexOf(qualityName);
    if (
      slowFrameWindows >= slowWindowsBeforeDowngrade &&
      currentIndex > 0
    ) {
      slowFrameWindows = 0;
      fastFrameWindows = 0;
      setQuality(qualityOrder[currentIndex - 1]);
    } else if (
      fastFrameWindows >= fastWindowsBeforeUpgrade &&
      currentIndex < qualityOrder.length - 1
    ) {
      slowFrameWindows = 0;
      fastFrameWindows = 0;
      setQuality(qualityOrder[currentIndex + 1]);
    }
  };

  applyQualityDecorations();
  canvas.dataset.frameMs = "0";

  let targetFeature = null;
  const pointer = new THREE.Vector2(0, 0);
  const targetPointer = new THREE.Vector2(0, 0);
  const cameraBase = new THREE.Vector3(0, 0.05, 10.45);
  const cameraLookTarget = new THREE.Vector3();

  const storyTargets = {
    hero: { rootScale: 1.0, rootY: 0.0, cameraZ: 10.45, orbitScale: 1.0 },
    workflow: { rootScale: 0.92, rootY: 0.03, cameraZ: 10.85, orbitScale: 0.58 },
    sync: { rootScale: 0.88, rootY: 0.03, cameraZ: 10.95, orbitScale: 0.46 },
    pricing: { rootScale: 0.90, rootY: -0.02, cameraZ: 11.10, orbitScale: 0.34 },
    windows: { rootScale: 0.96, rootY: 0.02, cameraZ: 10.72, orbitScale: 0.22 }
  };
  const storyAccentHex = {
    hero: 0x4fa3ff,
    workflow: 0x55b8ff,
    sync: 0x55d0ff,
    pricing: 0x9a68ff,
    windows: 0x5bbcff
  };
  let storyState = document.getElementById("story-stage")?.dataset.storyState || "hero";
  let storySectionProgress = 0;
  let storyRootScale = 1;
  let storyRootY = 0;
  let storyCameraZ = cameraBase.z;
  let storyOrbitScale = 1;

  let scrollTarget = 0;
  let scrollProgress = 0;
  let heroVisible = true;
  let storyStageVisible = true;
  let animationRunning = false;
  let elapsed = 0;
  let previousTime = performance.now();

  const updateScrollTarget = () => {
    const rect = visual.getBoundingClientRect();
    const travel = Math.max(rect.height * 0.88, 1);
    scrollTarget = clamp01(-rect.top / travel);
  };
  updateScrollTarget();
  window.addEventListener("scroll", updateScrollTarget, { passive: true });

  const resize = () => {
    if (disposed || contextLost || graphicsFailed || renderer.getContext().isContextLost()) return;
    narrowViewport = matchMedia("(max-width: 720px)").matches;
    const rect = visual.getBoundingClientRect();
    const width = Math.max(1, Math.round(rect.width));
    const height = Math.max(1, Math.round(rect.height));
    camera.aspect = width / height;
    // Keep the complete animated silhouette inside the canvas at every aspect ratio.
    const safeZ = sceneCameraDistance(storyState,camera.aspect);
    cameraBase.z = safeZ;
    for (const target of Object.values(storyTargets)) target.cameraZ = safeZ;
    storyCameraZ = safeZ;
    camera.position.z = safeZ;
    camera.updateProjectionMatrix();
    updatePixelRatio();
    renderer.setSize(width, height, false);
    compactScene = width < 1050;
    compactBoost = Math.max(1, Math.min(1.35, width / 450));
    layoutStoryArtifacts(storyArtifacts, compactScene);
  };

  const render = () => {
    if (disposed || contextLost || graphicsFailed || renderer.getContext().isContextLost()
      || (reducedMotion && !visualTestMode)) return false;
    try { renderer.render(scene, camera); }
    catch { failScene("render_failed"); return false; }
    if (renderer.getContext().isContextLost()) return false;
    canvas.hidden = false;
    canvas.dataset.context = "ready";
    canvas.dataset.frames = String(Number(canvas.dataset.frames || 0) + 1);
    canvas.dataset.storyState = storyState;
    canvas.dataset.transition = Object.entries(storyOpacity).every(([name, opacity]) =>
      name === storyState ? opacity > .985 : opacity < .015) ? "stable" : "moving";
    const rect = canvas.getBoundingClientRect();
    for (const [feature, card] of cards) {
      const label = visual.querySelector(`.hero-hotspot[data-feature="${feature}"]`);
      if (!label || !card.heroVisible) continue;
      const point = card.group.getWorldPosition(new THREE.Vector3()).project(camera);
      label.style.left = Math.max(38, Math.min(rect.width - 38, (point.x * .5 + .5) * rect.width)) + "px";
      label.style.top = Math.max(18, Math.min(rect.height - 18, (-.5 * point.y + .5) * rect.height)) + "px";
      label.classList.toggle("is-studio-label", storyState === "hero");
      label.dataset.engraved = String(Boolean(card.engraved));
    }
    let clipped = 0;
    let geometryClipped = 0;
    const outside = bounds => bounds && (bounds.minX < -.97 || bounds.maxX > .97 || bounds.minY < -.94 || bounds.maxY > .94);
    if (storyState === "hero") {
      for (const object of [sphere,mediaPanel,...orbits]) if (object.visible && outside(projectedSceneBounds(object,camera))) geometryClipped++;
      for (const card of cards.values()) if (card.heroVisible && outside(projectedSceneBounds(card.group,camera))) geometryClipped++;
    }
    for (const [name, artifact] of Object.entries(storyArtifacts)) {
      for (const control of artifact.controls || []) {
        const button = visual.querySelector(`.scene-control[data-feature="${control.feature}"]`);
        if (!button) continue;
        const active = name === storyState && storyOpacity[name] > .85;
        button.hidden = !active;
        if (!active) continue;
        if (outside(projectedSceneBounds(control.group,camera))) geometryClipped++;
        const point = control.group.localToWorld(control.anchor.clone()).project(camera);
        const x = (point.x * .5 + .5) * rect.width;
        const y = (-point.y * .5 + .5) * rect.height;
        if (x < 48 || x > rect.width - 48 || y < 22 || y > rect.height - 22) clipped++;
        button.style.left = x + "px"; button.style.top = y + "px";
        button.dataset.engraved = String(Boolean(control.engraved));
        const hit = control.group.localToWorld((control.pickAnchor || new THREE.Vector3(0,.39,.16)).clone()).project(camera);
        button.dataset.meshX = String(rect.left + (hit.x * .5 + .5) * rect.width);
        button.dataset.meshY = String(rect.top + (-hit.y * .5 + .5) * rect.height);
      }
    }
    canvas.dataset.controlsClipped = String(clipped);
    canvas.dataset.geometryClipped = String(geometryClipped);
    if (!visual.classList.contains("three-ready")) {
      visual.classList.add("three-ready");
      visual.dispatchEvent(new CustomEvent("videograbber:three-ready"));
    }
    return true;
  };

  const tick = (time) => {
    const frameMs = Math.max(0, time - previousTime);
    const dt = Math.min(frameMs / 1000, .25);
    const transitionBlend = 1 - Math.exp(-dt * 10);
    previousTime = time;
    elapsed += dt;
    sampleFrameTime(frameMs);

    pointer.lerp(targetPointer, reducedMotion ? 1 : 0.075);
    scrollProgress = THREE.MathUtils.lerp(scrollProgress, scrollTarget, 0.055);

    const storyTarget = storyTargets[storyState] || storyTargets.hero;
    storyRootScale = THREE.MathUtils.lerp(
      storyRootScale,
      storyTarget.rootScale,
      0.055
    );
    storyRootY = THREE.MathUtils.lerp(storyRootY, storyTarget.rootY, 0.055);
    storyCameraZ = THREE.MathUtils.lerp(
      storyCameraZ,
      storyTarget.cameraZ,
      0.055
    );
    storyOrbitScale = THREE.MathUtils.lerp(
      storyOrbitScale,
      storyTarget.orbitScale,
      0.055
    );

    const mobileFactor = narrowViewport ? 0.48 : 1;
    const cameraTargetX = pointer.x * 0.38 * mobileFactor;
    const cameraTargetY =
      cameraBase.y +
      pointer.y * 0.24 * mobileFactor +
      scrollProgress * 0.16 * mobileFactor;
    const cameraTargetZ =
      storyCameraZ -
      scrollProgress * 0.20 * mobileFactor +
      Math.abs(pointer.x) * 0.035;

    camera.position.x = THREE.MathUtils.lerp(camera.position.x, cameraTargetX, 0.055);
    camera.position.y = THREE.MathUtils.lerp(camera.position.y, cameraTargetY, 0.055);
    camera.position.z = THREE.MathUtils.lerp(camera.position.z, cameraTargetZ, 0.055);
    cameraLookTarget.set(0, -scrollProgress * 0.10 * mobileFactor, 0);
    camera.lookAt(cameraLookTarget);

    root.rotation.y = THREE.MathUtils.lerp(
      root.rotation.y,
      pointer.x * 0.18 * mobileFactor + scrollProgress * 0.035,
      0.055
    );
    root.rotation.x = THREE.MathUtils.lerp(
      root.rotation.x,
      -0.04 - pointer.y * 0.11 * mobileFactor + scrollProgress * 0.022,
      0.055
    );
    root.position.y = THREE.MathUtils.lerp(
      root.position.y,
      storyRootY - scrollProgress * 0.05 * mobileFactor,
      0.055
    );
    root.scale.setScalar(storyRootScale);
    sphere.visible = storyState === "hero";
    mediaPanel.visible = storyState === "hero";
    for (const orbit of orbits)
    {
      orbit.visible = storyState === "hero";
      orbit.scale.setScalar(storyOrbitScale);
    }

    accent.lerp(accentTarget, 0.065);

    for (const [name, artifact] of Object.entries(storyArtifacts)) {
      const targetOpacity = storyState === name ? 1 : 0;
      storyOpacity[name] = THREE.MathUtils.lerp(
        storyOpacity[name],
        targetOpacity,
        transitionBlend
      );
      const opacity = storyOpacity[name];
      artifact.group.visible = opacity > 0.015;
      const isCardScene = name === "workflow" || name === "pricing";
      const scale = isCardScene ? (compactScene ? artifact.mobileScale * compactBoost : artifact.desktopScale)
        : (narrowViewport ? artifact.mobileScale : artifact.desktopScale);
      artifact.group.scale.setScalar((0.94 + opacity * 0.06) * (scale || 1));
      setStoryArtifactOpacity(artifact.group, opacity);
    }

    const pricingArtifact = storyArtifacts.pricing;
    if (pricingArtifact?.cards) {
      for (const [plan, card] of pricingArtifact.cards) {
        const selected = storyState === "pricing" && (pricingFocusPlan === plan || hoveredFeature === "pricing-" + plan);
        const scale = card.baseScale * (selected ? 1.06 : 1);
        card.group.scale.x = THREE.MathUtils.lerp(card.group.scale.x, scale, 0.12);
        card.group.scale.y = THREE.MathUtils.lerp(card.group.scale.y, scale, 0.12);
        card.group.scale.z = THREE.MathUtils.lerp(card.group.scale.z, scale, 0.12);
        card.bodyMaterial.emissiveIntensity = THREE.MathUtils.lerp(
          card.bodyMaterial.emissiveIntensity,
          selected ? 0.78 : 0.20,
          0.10
        );
      }
    }

    sphereMaterial.emissive.copy(accent).multiplyScalar(.035);
    atmosphereMaterial.color.copy(accent).lerp(coolBlue, 0.38);
    rimShellMaterial.color.copy(accent).lerp(coolBlue, 0.18);
    halo.material.color.copy(accent).lerp(coolBlue, 0.28);
    wire.material.color.copy(accent).lerp(coolBlue, 0.45);

    orbitMaterials[0].color.copy(accent).lerp(orbitBlue, 0.28);
    orbitMaterials[1].color.copy(accent).lerp(orbitPurple, 0.45);
    orbitMaterials[2].color.copy(accent).lerp(orbitPink, 0.65);
    for (const material of orbitMaterials)
      material.emissive.copy(material.color);

    if (!reducedMotion) {
      sphere.rotation.y = -.48 + Math.sin(elapsed * .3) * .055;
      sphere.position.y = .2 + Math.sin(elapsed * .55) * .045;
      atmosphere.rotation.y -= dt * 0.050;
      rimShell.rotation.y += dt * 0.026;
      wire.rotation.y += dt * 0.036;
      core.rotation.z = 0;

      orbits[0].rotation.z = -.40 + Math.sin(elapsed * .22) * .035;
      orbits[1].rotation.z = -.43 + Math.sin(elapsed * .22) * .035;
      orbits[2].rotation.z = -.46 + Math.sin(elapsed * .22) * .035;

      let index = 0;
      for (const [feature, card] of cards) {
        const heroCardOpacity = storyState === "hero" && card.heroVisible ? 1 : 0;
        card.bodyMaterial.opacity = THREE.MathUtils.lerp(
          card.bodyMaterial.opacity,
          heroCardOpacity * 0.94,
          0.11
        );
        card.label.material.opacity = THREE.MathUtils.lerp(
          card.label.material.opacity,
          heroCardOpacity,
          0.11
        );
        card.group.visible =
          heroCardOpacity > 0 ||
          card.bodyMaterial.opacity > 0.02 ||
          card.label.material.opacity > 0.02;

        const selected = storyState === "hero" && targetFeature === feature;
        const baseScale = card.heroVisible ? (selected ? .76 : .72) : 1;
        const depthFactor = 1 + Math.max(card.basePosition.z, 0) * 0.24;
        const floatY =
          Math.sin(elapsed * 0.78 + index * 0.9) * (selected ? 0.085 : 0.055);
        const floatZ = Math.cos(elapsed * 0.56 + index * 0.7) * 0.035;

        card.group.scale.x = THREE.MathUtils.lerp(card.group.scale.x, baseScale, 0.13);
        card.group.scale.y = THREE.MathUtils.lerp(card.group.scale.y, baseScale, 0.13);
        card.group.scale.z = THREE.MathUtils.lerp(card.group.scale.z, baseScale, 0.13);
        card.bodyMaterial.emissiveIntensity = THREE.MathUtils.lerp(
          card.bodyMaterial.emissiveIntensity,
          selected ? 0.90 : 0.16,
          0.11
        );
        card.bodyMaterial.emissive
          .copy(selected ? accent : card.baseAccent)
          .multiplyScalar(selected ? 0.48 : 0.30);

            const inwardPop = selected
          ? -Math.sign(card.basePosition.x || 1) * 0.16
          : 0;
        card.group.position.x = THREE.MathUtils.lerp(
          card.group.position.x,
          card.basePosition.x + inwardPop + pointer.x * 0.045 * depthFactor,
          0.10
        );
        card.group.position.y = THREE.MathUtils.lerp(
          card.group.position.y,
          card.basePosition.y + floatY - pointer.y * 0.035 * depthFactor,
          0.12
        );
        card.group.position.z = THREE.MathUtils.lerp(
          card.group.position.z,
          card.basePosition.z + (selected ? 0.50 : 0) + floatZ,
          0.12
        );

        card.group.rotation.x = THREE.MathUtils.lerp(
          card.group.rotation.x,
          selected ? card.baseRotation.x * 0.20 : card.baseRotation.x,
          0.10
        );
        card.group.rotation.y = THREE.MathUtils.lerp(
          card.group.rotation.y,
          selected
            ? card.baseRotation.y * 0.15
            : card.baseRotation.y + pointer.x * 0.012,
          0.10
        );
        card.group.rotation.z = THREE.MathUtils.lerp(
          card.group.rotation.z,
          card.baseRotation.z + Math.sin(elapsed * 0.52 + index) * 0.010,
          0.10
        );
        for (const mesh of card.details || []) mesh.material.opacity = card.bodyMaterial.opacity;
        index += 1;
      }

      particles.forEach((particle, index) => {
        particle.position.y =
          particlePositions[index][1] + Math.sin(elapsed * 0.42 + index) * 0.10;
      });

      depthStars.rotation.y = elapsed * 0.012 - pointer.x * 0.030;
      depthStars.rotation.x = pointer.y * 0.018;
      depthStars.position.x = -pointer.x * 0.10;
      depthStars.position.y = pointer.y * 0.055 + scrollProgress * 0.07;

      nebulaField.rotation.z =
        Math.sin(elapsed * 0.045) * 0.025 + pointer.x * 0.010;
      nebulaField.position.x = -pointer.x * 0.08;
      nebulaField.position.y = pointer.y * 0.045 + scrollProgress * 0.04;

      if (storyArtifacts.workflow.group.visible)
        storyArtifacts.workflow.group.rotation.y =
          THREE.MathUtils.lerp(
            storyArtifacts.workflow.group.rotation.y,
            pointer.x * 0.025,
            0.05
          );

      if (storyArtifacts.sync.group.visible)
        storyArtifacts.sync.group.rotation.y =
          THREE.MathUtils.lerp(
            storyArtifacts.sync.group.rotation.y,
            pointer.x * 0.035,
            0.05
          );

      if (storyArtifacts.windows.group.visible)
        storyArtifacts.windows.group.rotation.y =
          THREE.MathUtils.lerp(
            storyArtifacts.windows.group.rotation.y,
            pointer.x * 0.045,
            0.05
          );
    }

    render();
  };

  let lastPaintTime = 0;

  const startLoop = () => {
    if (
      animationRunning ||
      disposed || contextLost || graphicsFailed || renderer.getContext().isContextLost() ||
      staticScene ||
      (reducedMotion && !visualTestMode) ||
      !heroVisible ||
      !storyStageVisible ||
      document.hidden
    ) {
      canvas.dataset.animation = animationRunning ? "running" : staticScene ? "visual-test" :
        reducedMotion ? "reduced-motion" : document.hidden ? "background" :
        !heroVisible ? "offscreen" : "stage-paused";
      render();
      return;
    }
    animationRunning = true;
    canvas.dataset.animation = "running";
    previousTime = performance.now();
    lastPaintTime = 0;
    frameSamples.length = 0;
    renderer.setAnimationLoop((time) => {
      if (narrowViewport && lastPaintTime && time - lastPaintTime < 32) return;
      lastPaintTime = time;
      try { tick(time); }
      catch { failScene("render_failed"); }
    });
  };

  const stopLoop = () => {
    if (!animationRunning) return;
    animationRunning = false;
    canvas.dataset.animation = "paused";
    frameSamples.length = 0;
    renderer.setAnimationLoop(null);
  };
  const applyMotionPreference = (reduced) => {
    canvas.dataset.motionChanges = String(Number(canvas.dataset.motionChanges || 0) + 1);
    canvas.dataset.motionReduced = String(reduced);
    reducedMotion = reduced;
    if (reduced && !visualTestMode) {
      stopLoop(); canvas.hidden = true; visual.classList.remove("three-ready");
    } else if (disposed || contextLost || graphicsFailed || renderer.getContext().isContextLost()) {
      stopLoop(); canvas.hidden = true; visual.classList.remove("three-ready");
    } else {
      if (render()) canvas.hidden = false;
      startLoop();
    }
  };
  const updateMotionPreference = (event) => applyMotionPreference(
    typeof event?.matches === "boolean" ? event.matches : motionPreference.matches);
  motionPreference.addEventListener("change", updateMotionPreference);

  const featureAccent = new Map(
    cardSpecs.map((spec) => [spec.feature, new THREE.Color(spec.accent)])
  );

  const setRaycastFeature = (feature) => {
    if (hoveredFeature === feature) return;
    hoveredFeature = feature || null;
    targetFeature = hoveredFeature;
    visual.dataset.raycastFeature = hoveredFeature || "";

    if (hoveredFeature && featureAccent.has(hoveredFeature))
      accentTarget.copy(featureAccent.get(hoveredFeature));
    else
      accentTarget.setRGB(0.28, 0.62, 1.0, THREE.SRGBColorSpace);

    visual.classList.toggle("three-card-hover", Boolean(hoveredFeature));
    if (!animationRunning) render();
  };

  const raycastAt = (clientX, clientY) => {
    const rect = canvas.getBoundingClientRect();
    if (
      clientX < rect.left ||
      clientX > rect.right ||
      clientY < rect.top ||
      clientY > rect.bottom
    ) {
      setRaycastFeature(null);
      return null;
    }

    rayPointer.x = ((clientX - rect.left) / rect.width) * 2 - 1;
    rayPointer.y = -((clientY - rect.top) / rect.height) * 2 + 1;
    raycaster.setFromCamera(rayPointer, camera);
    const visiblePickTargets = visibleSceneTargets(pickTargets, storyState);
    const hit = raycaster.intersectObjects(visiblePickTargets, false)[0];
    const feature = hit?.object?.userData?.feature || null;
    setRaycastFeature(feature);
    return feature;
  };

  visual.addEventListener("pointermove", (event) => {
    if (event.isPrimary === false) return;
    const rect = visual.getBoundingClientRect();
    targetPointer.x = ((event.clientX - rect.left) / rect.width - 0.5) * 2;
    targetPointer.y = ((event.clientY - rect.top) / rect.height - 0.5) * 2;
    raycastAt(event.clientX, event.clientY);

    if (pointerDown) {
      pointerDown.moved = Math.max(
        pointerDown.moved,
        Math.hypot(
          event.clientX - pointerDown.x,
          event.clientY - pointerDown.y
        )
      );
    }
  });

  visual.addEventListener("pointerdown", (event) => {
    pointerDown = null;
    if (event.button !== 0 || event.isPrimary === false || event.target.closest?.("button,a")) return;
    pointerDown = { id:event.pointerId, x: event.clientX, y: event.clientY, moved: 0 };
    raycastAt(event.clientX, event.clientY);
  });

  visual.addEventListener("pointerup", (event) => {
    const press = pointerDown;
    pointerDown = null;
    if (event.target.closest?.("button,a") || !press || press.id !== event.pointerId ||
        event.button !== 0 || event.isPrimary === false || press.moved > 10) return;
    const feature = raycastAt(event.clientX, event.clientY);
    if (!feature) return;
    if (feature.startsWith("workflow-") || feature.startsWith("pricing-") || feature.startsWith("device-") || feature === "windows-app") {
      visual.dispatchEvent(new CustomEvent("videograbber:scene-action", {detail:{feature}}));
      return;
    }

    const link = document.querySelector(
      '.hero-hotspot[data-feature="' + CSS.escape(feature) + '"]'
    );
    link?.click();
  });
  visual.addEventListener("pointercancel", () => { pointerDown = null; setRaycastFeature(null); });

  visual.addEventListener("pointerleave", () => {
    targetPointer.set(0, 0);
    pointerDown = null;
    setRaycastFeature(null);
  });

  visual.addEventListener("videograbber:story-visibility", (event) => {
    // The stage owns the preference change. Apply its value synchronously: separate
    // MediaQueryList change notifications can arrive after the renderer was paused.
    if (typeof event.detail?.reducedMotion === "boolean" &&
        (event.detail.reducedMotion !== reducedMotion || canvas.hidden !== (event.detail.reducedMotion && !visualTestMode)))
      applyMotionPreference(event.detail.reducedMotion);
    storyStageVisible = event.detail?.visible !== false;
    if (storyStageVisible && heroVisible && !document.hidden) startLoop();
    else stopLoop();
  });

  visual.addEventListener("videograbber:story-state", (event) => {
    const next = String(event.detail?.state || "hero");
    if (!Object.prototype.hasOwnProperty.call(storyTargets, next)) return;
    storyState = next;
    storySectionProgress = clamp01(event.detail?.progress || 0);
    setRaycastFeature(null);

    const storyAccent = new THREE.Color(
      storyAccentHex[next] || storyAccentHex.hero
    );
    accentTarget.copy(storyAccent);

    if (!animationRunning) render();
  });

  visual.addEventListener("videograbber:story-progress", (event) => {
    if (String(event.detail?.state || "") !== storyState) return;
    storySectionProgress = clamp01(event.detail?.progress || 0);
  });

  visual.addEventListener("videograbber:pricing-focus", (event) => {
    pricingFocusPlan = event.detail?.plan || null;
  });
  visual.addEventListener("videograbber:scene-focus", event => setRaycastFeature(event.detail?.feature || null));

  visual.addEventListener("videograbber:hero-accent", (event) => {
    const values = event.detail?.accent;
    if (Array.isArray(values) && values.length === 3) {
      accentTarget.setRGB(
        clamp01(values[0]),
        clamp01(values[1]),
        clamp01(values[2]),
        THREE.SRGBColorSpace
      );
    }
    targetFeature = event.detail?.feature || null;
    if (!animationRunning) render();
  });

  const resizeObserver = new ResizeObserver(() => {
    resize();
    render();
  });
  resizeObserver.observe(visual);

  const visibilityObserver = new IntersectionObserver(
    ([entry]) => {
      heroVisible = Boolean(entry?.isIntersecting);
      if (heroVisible) startLoop();
      else stopLoop();
    },
    { rootMargin: "180px 0px" }
  );
  visibilityObserver.observe(visual);

  document.addEventListener("visibilitychange", () => {
    if (document.hidden) stopLoop();
    else if (heroVisible) startLoop();
  });

  canvas.addEventListener(
    "webglcontextlost",
    (event) => {
      event.preventDefault();
      contextLost = true;
      stopLoop();
      hideScene();
      canvas.dataset.context = "lost";
      reportWebgl(canvas, "context_lost");
    },
    false
  );
  canvas.addEventListener("webglcontextrestored", () => {
    if (disposed) return;
    try {
      // Three restores its resources first; the procedural render target needs its own redraw.
      contextLost = false;
      graphicsFailed = false;
      proceduralEnvironment.dispose();
      proceduralEnvironment = createProceduralEnvironment(renderer);
      scene.environment = proceduralEnvironment.texture;
      resize(); updateMotionPreference();
      if (canvas.dataset.context === "ready" || (reducedMotion && !visualTestMode))
        reportWebgl(canvas, "context_restored");
    } catch { failScene("restoration_failed"); }
  });

  resize();

  if (visualTestMode) {
    storyState = "hero";
    storySectionProgress = 0;
    elapsed = 2.75;
    pointer.set(0, 0);
    targetPointer.set(0, 0);
    root.rotation.set(-0.04, 0.08, 0);
    sphere.rotation.y = -.48;
    atmosphere.rotation.y = -0.18;
    wire.rotation.y = 0.12;
    orbits[0].rotation.z = -.40;
    orbits[1].rotation.z = -.43;
    orbits[2].rotation.z = -.46;
  }

  render();
  startLoop();
  canvas.dataset.lifecycle = "active";
  window.addEventListener("pageshow", event => {
    if (event.persisted && !disposed) { canvas.dataset.lifecycle = "active"; resize(); updateMotionPreference(); }
  });

  window.addEventListener(
    "pagehide",
    event => {
      canvas.dataset.lifecycle = event.persisted ? "bfcache" : "disposed";
      stopLoop();
      if (event.persisted || disposed) return;
      disposed = true;
      visibilityObserver.disconnect();
      resizeObserver.disconnect();
      motionPreference.removeEventListener("change", updateMotionPreference);
      window.removeEventListener("scroll", updateScrollTarget);
      disposeScene(scene);
      coreTexture.dispose();
      proceduralEnvironment.dispose();
      renderer.dispose();
    }
  );
}

function reportWebgl(canvas, event) {
  window.dispatchEvent(new CustomEvent("videograbber:webgl", {detail:{event,
    state:canvas.dataset.storyState || "hero", quality:canvas.dataset.quality,
    dpr:Number(canvas.dataset.dpr || 0)}}));
}

function visibleSceneTargets(targets, storyState) {
  return targets.filter(object => {
    if (storyState && object.userData.storyState !== storyState) return false;
    for (let current = object; current; current = current.parent)
      if (!current.visible) return false;
    return true;
  });
}

function roundedTriangle(scale, reverse = false) {
  const points = [new THREE.Vector2(-1.1, -1.55), new THREE.Vector2(1.65, 0), new THREE.Vector2(-1.1, 1.55)];
  if (reverse) points.reverse();
  points.forEach(point => point.multiplyScalar(scale));
  const shape = new THREE.Shape();
  const radius = .34 * scale;
  for (let index = 0; index < points.length; index++) {
    const current = points[index];
    const previous = points[(index + points.length - 1) % points.length];
    const next = points[(index + 1) % points.length];
    const start = current.clone().add(previous.clone().sub(current).normalize().multiplyScalar(radius));
    const end = current.clone().add(next.clone().sub(current).normalize().multiplyScalar(radius));
    if (index === 0) shape.moveTo(start.x, start.y);
    else shape.lineTo(start.x, start.y);
    shape.quadraticCurveTo(current.x, current.y, end.x, end.y);
  }
  shape.closePath();
  return shape;
}

function inflatedTriangleGeometry(scale, depth) {
  const outline = roundedTriangle(scale).getSpacedPoints(360);
  outline.pop();
  const positions = [];
  const indices = [];
  const radialSteps = 28;
  const count = outline.length;
  for (const side of [1, -1]) {
    for (let row = 0; row <= radialSteps; row++) {
      const t = row / radialSteps;
      const z = side * depth * Math.sqrt(Math.max(0, 1 - t * t));
      for (const point of outline) positions.push(-.12 * scale + (point.x + .12 * scale) * t, point.y * t, z);
    }
  }
  const faceSize = (radialSteps + 1) * count;
  for (let side = 0; side < 2; side++) {
    const offset = side * faceSize;
    for (let row = 0; row < radialSteps; row++) for (let col = 0; col < count; col++) {
      const a = offset + row * count + col;
      const b = offset + row * count + (col + 1) % count;
      const c = a + count;
      const d = b + count;
      if (side === 0) indices.push(a,d,b,a,c,d);
      else indices.push(a,b,d,a,d,c);
    }
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position",new THREE.Float32BufferAttribute(positions,3));
  geometry.setIndex(indices);
  geometry.computeVertexNormals();
  return geometry;
}

function createStudioPlayObject(material) {
  const group = new THREE.Group();
  const shellGeometry = inflatedTriangleGeometry(1,.54);
  group.add(new THREE.Mesh(shellGeometry, material));
  const innerGeometry = inflatedTriangleGeometry(.54,.14);
  innerGeometry.translate(.07,0,.65);
  const titanium = new THREE.MeshPhysicalMaterial({color: 0xcad7e3, metalness: .88,
    roughness: .2, clearcoat: 1, clearcoatRoughness: .12, envMapIntensity: 2.7});
  group.add(new THREE.Mesh(innerGeometry, titanium));
  group.position.set(-.65,.2,.35);
  group.rotation.set(.12,-.48,-.08);
  group.scale.setScalar(1.55);
  return group;
}

function createStudioMediaPanel() {
  const group = new THREE.Group();
  group.position.set(.95,-.62,-.28);
  group.rotation.set(.02,-.18,-.025);
  const material = new THREE.MeshPhysicalMaterial({color:0x102340,metalness:.68,roughness:.22,
    clearcoat:1,clearcoatRoughness:.1});
  const geometry = new THREE.ExtrudeGeometry(roundedRectShape(2.65,1.68,.20),{
    depth:.1,bevelEnabled:true,bevelThickness:.05,bevelSize:.05,bevelSegments:8,curveSegments:24});
  group.add(new THREE.Mesh(geometry,material));
  const waveMaterial = new THREE.MeshPhysicalMaterial({color:0x64baff,emissive:0x155baa,
    emissiveIntensity:.28,metalness:.45,roughness:.2,clearcoat:1});
  for(let i=0;i<29;i++) {
    const height=.13 + .52*Math.pow(Math.sin(i*.7),2)*Math.exp(-Math.pow((i-14)/18,2));
    const bar = new THREE.Mesh(new THREE.CapsuleGeometry(.018,height,5,10),waveMaterial);
    bar.position.set(-1.05+i*.074,-.16,.19);
    group.add(bar);
  }
  return group;
}

function sceneCameraDistance(state, aspect) {
  return Math.max(state === "hero" ? 12.2 : state === "sync" || state === "windows" ? 15 : 10.6,14.2/aspect);
}

function projectedSceneBounds(object, camera) {
  object.updateWorldMatrix(true,true);
  let minX=Infinity,minY=Infinity,maxX=-Infinity,maxY=-Infinity;
  object.traverseVisible(mesh=>{
    if(!mesh.isMesh || !mesh.geometry)return;
    if(mesh.isInstancedMesh){if(!mesh.boundingBox)mesh.computeBoundingBox();}
    else if(!mesh.geometry.boundingBox)mesh.geometry.computeBoundingBox();
    const box=mesh.isInstancedMesh?mesh.boundingBox:mesh.geometry.boundingBox;
    if(!box)return;
    for(const x of [box.min.x,box.max.x])for(const y of [box.min.y,box.max.y])for(const z of [box.min.z,box.max.z]){
      const point=new THREE.Vector3(x,y,z).applyMatrix4(mesh.matrixWorld).project(camera);
      minX=Math.min(minX,point.x);maxX=Math.max(maxX,point.x);minY=Math.min(minY,point.y);maxY=Math.max(maxY,point.y);
    }
  });
  return Number.isFinite(minX)?{minX,minY,maxX,maxY}:null;
}

function createEngravedCard(renderer, spec) {
  const card=createFeatureCard({...spec,icon:"",title:"",subtitle:""},renderer);
  card.bodyMaterial.color.setHex(0x102638);
  card.bodyMaterial.metalness=.55;card.bodyMaterial.roughness=.40;card.bodyMaterial.clearcoat=.35;
  card.bodyMaterial.envMapIntensity=.65;card.bodyMaterial.emissiveIntensity=.07;
  card.texture.dispose();
  const face=document.createElement("canvas");face.width=1024;face.height=512;
  const ctx=face.getContext("2d");
  ctx.textAlign='center';ctx.fillStyle='#f0f7ff';ctx.font='700 190px "Manrope", sans-serif';
  ctx.fillText(spec.title,512,355,930);
  if(spec.subtitle){ctx.font='500 106px "Onest", sans-serif';ctx.fillStyle='#b8cedd';ctx.fillText(spec.subtitle,512,470,930);}
  const texture=new THREE.CanvasTexture(face);texture.colorSpace=THREE.SRGBColorSpace;
  texture.anisotropy=Math.min(renderer.capabilities.getMaxAnisotropy(),16);
  card.texture=texture;card.label.material.map=texture;card.label.material.opacity=1;
  card.label.position.z=.145;card.engraved=true;card.details=[];
  const metal=new THREE.MeshStandardMaterial({color:spec.accent,metalness:.62,roughness:.38});
  const add=(geometry,material,x,y,z=.18)=>{
    const mesh=new THREE.Mesh(geometry,material);mesh.position.set(x,y,z);card.group.add(mesh);card.details.push(mesh);return mesh;
  };
  const theme=spec.theme;
  if(theme==='mp3'){
    for(let i=0;i<23;i++){
      const h=.035+Math.abs(Math.sin(i*.78))* .19;
      add(new THREE.CapsuleGeometry(.012,h,3,8),metal.clone(),(i-11)*.045,.25);
    }
  }else if(theme==='video'){
    add(new THREE.BoxGeometry(.95,.34,.04),new THREE.MeshStandardMaterial({color:0x081725,roughness:.8}),0,.24);
    for(let i=0;i<6;i++)for(const y of [.10,.38])add(new THREE.BoxGeometry(.075,.035,.02),metal.clone(),(i-2.5)*.145,y,.21);
    const play=new THREE.Shape();play.moveTo(-.08,-.095);play.lineTo(.11,0);play.lineTo(-.08,.095);play.closePath();
    add(new THREE.ExtrudeGeometry(play,{depth:.028,bevelEnabled:true,bevelSize:.009,bevelThickness:.009,bevelSegments:4}),
      new THREE.MeshStandardMaterial({color:0xdff2ff,metalness:.25,roughness:.5}),0,.24,.22);
  }else if(theme==='course'){
    for(let i=0;i<3;i++){
      const book=new THREE.Group();book.position.set((i-1)*.23,.24,.19);book.rotation.z=(1-i)*.08;
      const pages=new THREE.Mesh(new THREE.BoxGeometry(.17,.29,.055),new THREE.MeshStandardMaterial({color:0xd9e5eb,roughness:.85}));book.add(pages);
      const cover=new THREE.Mesh(new THREE.BoxGeometry(.20,.32,.015),new THREE.MeshStandardMaterial({color:[0x427fa6,0x736aad,0x549eac][i],roughness:.52,metalness:.15}));
      cover.position.z=.045;book.add(cover);card.group.add(book);
      book.traverse(mesh=>{if(mesh.isMesh)card.details.push(mesh);});
    }
  }else if(theme==='link'){
    for(const x of [-.075,.075]){const ring=add(new THREE.TorusGeometry(.09,.025,12,32),metal.clone(),x,.24,.21);ring.rotation.z=-.4;}
  }else if(theme==='format'){
    for(let i=0;i<3;i++){const tab=add(new THREE.BoxGeometry(.14,.25,.045),metal.clone(),(i-1)*.20,.24,.20+i*.014);tab.rotation.z=(1-i)*.12;}
  }else if(theme==='file'){
    add(new THREE.BoxGeometry(.25,.28,.035),new THREE.MeshStandardMaterial({color:0xcce3ef,roughness:.65,metalness:.2}),0,.24,.20);
    for(let i=0;i<3;i++)add(new THREE.BoxGeometry(.14,.015,.008),metal.clone(),0,.29-i*.06,.224);
  }else if(theme?.startsWith('plan-')){
    const path={free:'free',start:'start',unlimited_video:'unlimited',full_course:'course'}[theme.slice(5)];
    const symbol=new THREE.TextureLoader().load('/assets/studio-plan-'+path+'.webp');symbol.colorSpace=THREE.SRGBColorSpace;
    add(new THREE.PlaneGeometry(.47,.47),new THREE.MeshBasicMaterial({map:symbol,transparent:true}),0,.25,.19);
  }
  for(const mesh of card.details){mesh.userData.feature=spec.feature;mesh.material.transparent=true;}
  for(const child of card.group.children)if(child!==card.body&&child!==card.label)child.position.y+=.06;
  return card;
}

function createFeatureCard(spec, renderer) {
  const group = new THREE.Group();
  group.position.set(...spec.position);
  group.rotation.set(...spec.rotation);

  const shape = roundedRectShape(2.12, 1.08, 0.18);
  const geometry = new THREE.ExtrudeGeometry(shape, {
    depth: 0.11,
    bevelEnabled: true,
    bevelThickness: 0.045,
    bevelSize: 0.045,
    bevelSegments: 4,
    curveSegments: 12
  });
  geometry.center();

  const accent = new THREE.Color(spec.accent);
  const bodyMaterial = new THREE.MeshPhysicalMaterial({
    color: 0x0b1834,
    emissive: accent.clone().multiplyScalar(0.04),
    emissiveIntensity: 0.12,
    metalness: 0.14,
    roughness: 0.26,
    clearcoat: 0.92,
    clearcoatRoughness: 0.14,
    transparent: true,
    opacity: 0.94,
    side: THREE.DoubleSide
  });

  const body = new THREE.Mesh(geometry, bodyMaterial);
  body.userData.feature = spec.feature;
  group.add(body);

  const labelCanvas = makeCardTexture(spec);
  const texture = new THREE.CanvasTexture(labelCanvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.anisotropy = Math.min(renderer.capabilities.getMaxAnisotropy(), 16);
  texture.minFilter = THREE.LinearMipmapLinearFilter;
  texture.magFilter = THREE.LinearFilter;
  texture.generateMipmaps = true;

  const label = new THREE.Mesh(
    new THREE.PlaneGeometry(1.94, 0.92),
    new THREE.MeshBasicMaterial({
      map: texture,
      transparent: true,
      depthWrite: false,
      toneMapped: false
    })
  );
  label.position.z = 0.115;
  label.userData.feature = spec.feature;
  group.add(label);

  return {
    group,
    body,
    label,
    bodyMaterial,
    texture,
    baseAccent: accent,
    basePosition: new THREE.Vector3(...spec.position),
    baseRotation: new THREE.Euler(...spec.rotation)
  };
}

function createStoryArtifacts(renderer) {
  return {
    workflow: createWorkflowArtifact(renderer),
    sync: createSyncArtifact(renderer),
    pricing: createPricingArtifact(renderer),
    windows: createWindowsArtifact(renderer)
  };
}

function createStoryCard(renderer, spec, scale = 0.72) {
  const card = createFeatureCard(
    {
      feature: spec.feature,
      icon: spec.icon,
      title: spec.title,
      subtitle: spec.subtitle,
      position: spec.position,
      rotation: spec.rotation || [0, 0, 0],
      accent: spec.accent
    },
    renderer
  );
  card.group.scale.setScalar(scale);
  return card;
}

function createWorkflowArtifact(renderer) {
  const group=new THREE.Group();group.position.set(0,0,.18);
  const controls=[];
  const entries=[['workflow-url','Ссылка','Как подготовить','link'],['workflow-format','Формат','Что выбрать','format'],['workflow-download','Файл','Как сохранить','file']];
  for(const [index,[feature,title,subtitle,theme]] of entries.entries()){
    const card=createEngravedCard(renderer,{feature,title,subtitle,theme,position:[(index-1)*2.15,.05,.3],rotation:[.02,(1-index)*.08,0],accent:0x67c4ef});
    card.group.scale.setScalar(.82);group.add(card.group);
    controls.push({feature,group:card.group,anchor:new THREE.Vector3(0,-.18,.20),engraved:true});
  }
  return {group,controls,desktopScale:2.25,mobileScale:1.48};
}

function createDeviceScreenTexture(renderer, kind) {
  const canvas = document.createElement("canvas");
  canvas.width = 768; canvas.height = kind === "telegram" ? 1280 : 480;
  const context = canvas.getContext("2d");
  context.fillStyle = kind === "telegram" ? "#e9f3fc" : "#09182d";
  context.fillRect(0,0,canvas.width,canvas.height);
  context.fillStyle = kind === "telegram" ? "#168dcc" : "#142e50";
  context.fillRect(0,0,canvas.width,100);
  context.fillStyle = "#ffffff";
  context.font = '700 42px "Onest", sans-serif';
  context.fillText(kind === "telegram" ? "Telegram" : "VideoGrabber",100,65);
  context.fillStyle = kind === "telegram" ? "#174566" : "#c3daf2";
  context.font = '600 46px "Onest", sans-serif';
  context.fillText(kind === "telegram" ? "VideoGrabber" : kind === "web" ? "На сайте" : "Для Windows",40,176);
  context.font = '400 32px "Onest", sans-serif';
  context.fillText(kind === "telegram" ? "Бот и Mini App" : "Видео · MP3 · Курсы",40,228);
  if (kind === "telegram") {
    context.fillStyle="#59b9eb"; context.beginPath(); context.arc(384,500,220,0,Math.PI*2); context.fill();
    context.fillStyle="#24415a"; context.font='500 36px "Onest", sans-serif';
    context.fillText("Один аккаунт",64,850);
    context.fillText("Сайт · Windows · Telegram",64,912);
    context.fillStyle="#168dcc"; context.beginPath(); context.roundRect(40,1000,688,100,26); context.fill();
    context.fillStyle="#ffffff"; context.fillText("Открыть VideoGrabber",74,1065);
  } else {
    context.fillStyle="#284f79"; context.beginPath(); context.roundRect(40,280,688,60,14); context.fill();
    context.fillStyle="#8fb1d2"; context.font='400 28px "Onest", sans-serif'; context.fillText("Ссылка на видео",65,320);
    context.fillStyle="#1578d0"; context.beginPath(); context.roundRect(470,372,258,64,16); context.fill();
    context.fillStyle="#ffffff"; context.font='600 28px "Onest", sans-serif'; context.fillText("Скачать",540,414);
  }
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.anisotropy = Math.min(renderer.capabilities.getMaxAnisotropy(),8);
  const image = new Image();
  image.onload = () => {context.drawImage(image,30,22,60,60);
    if (kind === "telegram") context.drawImage(image,224,340,320,320);
    texture.needsUpdate=true;};
  image.src = kind === "telegram" ? "/assets/icons/telegram.svg" : "/assets/videograbber-icon.png";
  return texture;
}

function createStudioDevice(width, height, screenTexture, laptop = false) {
  const group = new THREE.Group();
  const depth = laptop ? .085 : .070;
  const bevel = .012;
  const bezel = laptop ? .075 : .055;
  const aluminium = new THREE.MeshPhysicalMaterial({color:0xb3beca,metalness:.86,roughness:.46,
    clearcoat:.16,clearcoatRoughness:.45,envMapIntensity:.65});
  const graphite = new THREE.MeshStandardMaterial({color:0x151c25,metalness:.35,roughness:.7});
  const slab = (w,h,d,r=.06) => new THREE.ExtrudeGeometry(roundedRectShape(w,h,r),{
    depth:d,bevelEnabled:true,bevelThickness:bevel,bevelSize:bevel,bevelSegments:5,curveSegments:24});
  const frame = new THREE.Mesh(slab(width,height,depth,laptop ? .065 : .11),aluminium);
  frame.name='display-chassis'; group.add(frame);
  const screenGeometry = new THREE.ShapeGeometry(roundedRectShape(width-bezel,height-bezel,.075));
  const positions = screenGeometry.attributes.position, uv = screenGeometry.attributes.uv;
  for(let i=0;i<positions.count;i++)uv.setXY(i,positions.getX(i)/(width-bezel)+.5,positions.getY(i)/(height-bezel)+.5);
  const screen = new THREE.Mesh(screenGeometry,new THREE.MeshBasicMaterial({map:screenTexture,color:0xffffff}));
  screen.position.z=depth+bevel+.006; screen.name='device-screen'; group.add(screen);
  const camera = new THREE.Mesh(new THREE.BoxGeometry(laptop ? .035 : width < 1 ? .18 : .04,.020,.008),graphite);
  camera.position.set(0,height/2-.022,screen.position.z+.006); group.add(camera);
  if(laptop){
    const deckDepth=.060,deckBevel=.012,deckY=-height/2-.05;
    const deckGeometry=new THREE.ExtrudeGeometry(roundedRectShape(width+.12,1.72,.08),{
      depth:deckDepth,bevelEnabled:true,bevelThickness:deckBevel,bevelSize:deckBevel,bevelSegments:5,curveSegments:24});
    const deck=new THREE.Mesh(deckGeometry,aluminium.clone());
    deck.name='chassis-deck'; deck.rotation.x=-Math.PI/2; deck.position.set(0,deckY,.86); group.add(deck);
    const surfaceY=deckY+deckDepth+deckBevel;
    const keyGeometry=new THREE.ExtrudeGeometry(roundedRectShape((width-.38)/10-.035,.115,.025),{
      depth:.010,bevelEnabled:true,bevelThickness:.003,bevelSize:.003,bevelSegments:3,curveSegments:8});
    const keys=new THREE.InstancedMesh(keyGeometry,new THREE.MeshStandardMaterial({color:0x17212d,metalness:.1,roughness:.88}),40);
    keys.name='keyboard';
    const transform=new THREE.Object3D(); transform.rotation.x=-Math.PI/2;
    for(let i=0;i<40;i++){transform.position.set((i%10-4.5)*(width-.38)/10,surfaceY+.012,.26+Math.floor(i/10)*.185);
      transform.updateMatrix();keys.setMatrixAt(i,transform.matrix);}
    group.add(keys);
    const spacebar=new THREE.Mesh(new THREE.BoxGeometry(width*.32,.014,.09),graphite.clone());
    spacebar.position.set(0,surfaceY+.016,.97); group.add(spacebar);
    const pad=new THREE.Mesh(new THREE.ExtrudeGeometry(roundedRectShape(width*.34,.38,.045),{
      depth:.006,bevelEnabled:true,bevelThickness:.002,bevelSize:.002,bevelSegments:4,curveSegments:16}),
      new THREE.MeshStandardMaterial({color:0x8d9caa,metalness:.3,roughness:.82}));
    pad.name='trackpad';pad.rotation.x=-Math.PI/2;pad.position.set(0,surfaceY+.011,1.35);group.add(pad);
    const hinge=new THREE.Mesh(new THREE.CylinderGeometry(.034,.034,width-.15,20),graphite.clone());
    hinge.rotation.z=Math.PI/2;hinge.position.set(0,-height/2,.10);group.add(hinge);
    group.userData.deckSurfaceY=surfaceY;
  }else{
    const key=new THREE.Mesh(new THREE.BoxGeometry(.018,.22,.035),aluminium.clone());
    key.position.set(width/2+.02,height*.18,depth*.5);group.add(key);
  }
  group.userData.deviceDepth=depth;
  return group;
}

function createSyncArtifact(renderer) {
  const group=new THREE.Group(); group.position.set(0,.10,.1);
  const desktop=createStudioDevice(2.7,1.70,createDeviceScreenTexture(renderer,"windows"),true);
  desktop.position.set(-1.18,-.42,.25); desktop.rotation.set(.22,.32,0); group.add(desktop);
  const phone=createStudioDevice(.80,1.65,createDeviceScreenTexture(renderer,"telegram"));
  phone.position.set(1.60,-.38,.50); phone.rotation.set(.02,-.35,-.03); group.add(phone);
  const tablet=createStudioDevice(1.78,1.22,createDeviceScreenTexture(renderer,"web"));
  tablet.position.set(.12,1.23,-.55); tablet.rotation.set(.04,-.28,.035); group.add(tablet);
  return {group,desktopScale:1.65,mobileScale:1.62,controls:[
    {feature:"device-windows",group:desktop,anchor:new THREE.Vector3(0,-1.50,.65)},
    {feature:"device-telegram",group:phone,anchor:new THREE.Vector3(0,-1.14,.20)},
    {feature:"device-web",group:tablet,anchor:new THREE.Vector3(0,1.16,.15)}]};
}

function createPricingArtifact(renderer) {
  const group=new THREE.Group();group.position.set(0,0,.10);
  const cards=new Map(),controls=[];
  for(const [index,[plan,title]] of [['free','Free'],['start','Start'],['unlimited_video','Unlimited'],['full_course','Full Course']].entries()){
    const card=createEngravedCard(renderer,{feature:'pricing-'+plan,title,subtitle:index===0?'10 видео':'Подробнее',theme:'plan-'+plan,
      position:[(index-1.5)*2.17,.05,.25],rotation:[.02,(1.5-index)*.045,0],accent:index===2?0xa691df:0x64b9dc});
    card.group.scale.setScalar(.82);card.baseScale=.82;group.add(card.group);cards.set(plan,card);
    controls.push({feature:'pricing-'+plan,group:card.group,anchor:new THREE.Vector3(0,-.18,.20),engraved:true});
  }
  return {group,cards,controls,desktopScale:2.25,mobileScale:1.48};
}

function createWindowsArtifact(renderer) {
  const group=new THREE.Group(); group.position.set(0,.12,.1);
  const laptop=createStudioDevice(3.25,1.94,createDeviceScreenTexture(renderer,"windows"),true);
  laptop.rotation.set(.24,-.30,0); group.add(laptop);
  return {group,desktopScale:1.6,mobileScale:1.65,controls:[
    {feature:"windows-app",group:laptop,anchor:new THREE.Vector3(0,-1.95,1.05)}]};
}

function layoutStoryArtifacts(artifacts, narrow) {
  for(const [index,control] of artifacts.workflow.controls.entries()) {
    const position=narrow ? [[-1.05,.65,.28],[1.05,.65,.28],[0,-.78,.32]][index]
      : [[-2.15,.05,.28],[0,.05,.40],[2.15,.05,.28]][index];
    control.group.position.set(...position);
  }
  for(const [index,control] of artifacts.pricing.controls.entries()) {
    control.group.position.set(narrow ? (index%2?1.12:-1.12) : (index-1.5)*2.17,
      narrow ? (index<2?.82:-.82) : .05,.25);
  }
}

function makeStoryLine(points, color) {
  const curve = new THREE.CatmullRomCurve3(points);
  const geometry = new THREE.TubeGeometry(curve, 24, 0.018, 8, false);
  const material = new THREE.MeshStandardMaterial({
    color,
    emissive: new THREE.Color(color),
    emissiveIntensity: 1.0,
    metalness: 0.18,
    roughness: 0.30,
    transparent: true,
    opacity: 0.72,
    depthWrite: false
  });
  return new THREE.Mesh(geometry, material);
}

function prepareStoryArtifact(group) {
  group.visible = false;
  group.traverse((object) => {
    const materials = Array.isArray(object.material)
      ? object.material
      : object.material
        ? [object.material]
        : [];

    for (const material of materials) {
      material.transparent = true;
      material.userData.storyBaseOpacity =
        Number.isFinite(material.opacity) ? material.opacity : 1;
      material.opacity = 0;
    }
  });
}

function setStoryArtifactOpacity(group, opacity) {
  group.traverse((object) => {
    const materials = Array.isArray(object.material)
      ? object.material
      : object.material
        ? [object.material]
        : [];

    for (const material of materials) {
      const base = material.userData.storyBaseOpacity ?? 1;
      material.opacity = base * opacity;
    }
  });
}

function makeOrbitMaterial(color, opacity) {
  const base = new THREE.Color(color);
  return new THREE.MeshStandardMaterial({
    color: base,
    emissive: base.clone(),
    emissiveIntensity: .06,
    metalness: .9,
    roughness: .10,
    transparent: true,
    opacity,
    depthWrite: false
  });
}

function roundedRectShape(width, height, radius) {
  const w = width / 2;
  const h = height / 2;
  const r = Math.min(radius, w, h);
  const shape = new THREE.Shape();

  shape.moveTo(-w + r, -h);
  shape.lineTo(w - r, -h);
  shape.quadraticCurveTo(w, -h, w, -h + r);
  shape.lineTo(w, h - r);
  shape.quadraticCurveTo(w, h, w - r, h);
  shape.lineTo(-w + r, h);
  shape.quadraticCurveTo(-w, h, -w, h - r);
  shape.lineTo(-w, -h + r);
  shape.quadraticCurveTo(-w, -h, -w + r, -h);

  return shape;
}

function makeCardTexture(spec) {
  const canvas = document.createElement("canvas");
  if (!spec.title && !spec.subtitle && !spec.icon) { canvas.width=2; canvas.height=2; return canvas; }
  canvas.width = 1350;
  canvas.height = 630;
  const context = canvas.getContext("2d");
  context.scale(1.5, 1.5);

  const gradient = context.createLinearGradient(0, 0, 900, 420);
  gradient.addColorStop(0, "rgba(24,47,91,.97)");
  gradient.addColorStop(0.55, "rgba(13,27,58,.98)");
  gradient.addColorStop(1, "rgba(8,18,39,.99)");
  roundedRect(context, 6, 6, 888, 408, 62);
  context.fillStyle = gradient;
  context.fill();

  const accent = new THREE.Color(spec.accent);
  const accentCss = "#" + accent.getHexString();

  context.lineWidth = 5;
  context.strokeStyle = "rgba(134,174,255,.45)";
  context.stroke();

  context.shadowColor = accentCss;
  context.shadowBlur = 28;
  context.fillStyle = accentCss;
  roundedRect(context, 54, 88, 112, 112, 30);
  context.fill();
  context.shadowBlur = 0;

  context.fillStyle = "#ffffff";
  context.font = '800 54px "Segoe UI Symbol","Segoe UI",sans-serif';
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(spec.icon, 110, 145);

  context.textAlign = "left";
  context.fillStyle = "#f5f8ff";
  context.font = '800 55px "Segoe UI",sans-serif';
  context.fillText(spec.title, 206, 142);

  context.fillStyle = "#9fb0d0";
  context.font = '500 31px "Segoe UI",sans-serif';
  wrapCanvasText(context, spec.subtitle, 206, 196, 610, 42, 2);

  return canvas;
}

async function loadTextureSafe(loader, url) {
  try {
    return await loader.loadAsync(url);
  } catch (error) {
    console.warn("VideoGrabber 3D texture fallback", url, error);
    return null;
  }
}

function createProceduralEnvironment(renderer) {
  const room = new THREE.Scene();
  const walls = new THREE.Mesh(new THREE.BoxGeometry(14, 12, 14),
    new THREE.MeshBasicMaterial({color: 0x040912, side: THREE.BackSide}));
  room.add(walls);
  const panels = [
    {size: [6, 3], position: [.8, .2, 5.5], color: [.9, 1.4, 2.2]},
    {size: [6, 1.2], position: [-1.6, 4, 3], color: [7, 9, 12]},
    {size: [1.2, 5], position: [4.8, .6, 1], color: [1.8, 4, 8]},
    {size: [3, .6], position: [-4, -2, 3], color: [8, 5, 2.5]},
    {size: [4, 2], position: [.5, 1, -5], color: [1, 1.7, 3]}
  ];
  for (const panel of panels) {
    const light = new THREE.Mesh(new THREE.PlaneGeometry(...panel.size),
      new THREE.MeshBasicMaterial({color: new THREE.Color().setRGB(...panel.color),
        toneMapped: false, side: THREE.DoubleSide}));
    light.position.set(...panel.position);
    light.lookAt(0, 0, 0);
    room.add(light);
  }
  const generator = new THREE.PMREMGenerator(renderer);
  const target = generator.fromScene(room, .04, .1, 60);
  generator.dispose();
  disposeScene(room);
  return {texture: target.texture, dispose: () => target.dispose()};
}

function createNebulaField() {
  const group = new THREE.Group();
  const texture = new THREE.CanvasTexture(makeNebulaTexture());
  texture.colorSpace = THREE.SRGBColorSpace;

  const specs = [
    [-3.9, 1.65, -2.9, 5.6, 3.4, 0x447dff, 0.18],
    [4.1, -0.25, -3.3, 6.2, 4.0, 0x8b52ff, 0.14],
    [0.35, -3.1, -3.7, 5.2, 3.0, 0xff6fb8, 0.09]
  ];

  for (const [x, y, z, width, height, color, opacity] of specs) {
    const material = new THREE.SpriteMaterial({
      map: texture,
      color,
      transparent: true,
      opacity,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      toneMapped: false
    });
    const sprite = new THREE.Sprite(material);
    sprite.position.set(x, y, z);
    sprite.scale.set(width, height, 1);
    sprite.userData.baseOpacity = opacity;
    group.add(sprite);
  }

  return group;
}

function makeNebulaTexture() {
  const canvas = document.createElement("canvas");
  canvas.width = 512;
  canvas.height = 512;
  const context = canvas.getContext("2d");
  const gradient = context.createRadialGradient(256, 256, 18, 256, 256, 250);
  gradient.addColorStop(0, "rgba(255,255,255,.92)");
  gradient.addColorStop(0.16, "rgba(205,224,255,.52)");
  gradient.addColorStop(0.42, "rgba(152,175,255,.20)");
  gradient.addColorStop(0.72, "rgba(98,87,190,.07)");
  gradient.addColorStop(1, "rgba(20,25,70,0)");
  context.fillStyle = gradient;
  context.fillRect(0, 0, 512, 512);
  return canvas;
}

function createDepthStarField(count) {
  const geometry = new THREE.BufferGeometry();
  const positions = new Float32Array(count * 3);
  let seed = 0x51a7c0de;

  const random = () => {
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    return seed / 4294967296;
  };

  for (let index = 0; index < count; index += 1) {
    const offset = index * 3;
    let x = (random() - 0.5) * 9.4;
    const y = (random() - 0.5) * 6.2;
    const z = -3.4 + random() * 5.2;

    if (Math.abs(x) < 1.75 && Math.abs(y) < 1.65)
      x += x >= 0 ? 1.85 : -1.85;

    positions[offset] = x;
    positions[offset + 1] = y;
    positions[offset + 2] = z;
  }

  geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));

  const material = new THREE.PointsMaterial({
    color: 0x78a8ff,
    size: 0.038,
    sizeAttenuation: true,
    transparent: true,
    opacity: 0.42,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    toneMapped: false
  });

  return new THREE.Points(geometry, material);
}

function makeHaloTexture() {
  const canvas = document.createElement("canvas");
  canvas.width = 512;
  canvas.height = 512;
  const context = canvas.getContext("2d");
  const gradient = context.createRadialGradient(256, 256, 70, 256, 256, 250);
  gradient.addColorStop(0, "rgba(90,135,255,.44)");
  gradient.addColorStop(0.34, "rgba(78,92,255,.24)");
  gradient.addColorStop(0.68, "rgba(111,69,255,.10)");
  gradient.addColorStop(1, "rgba(35,45,110,0)");
  context.fillStyle = gradient;
  context.fillRect(0, 0, 512, 512);
  return canvas;
}

function makeCoreTexture() {
  const canvas = document.createElement("canvas");
  canvas.width = 512;
  canvas.height = 512;
  const context = canvas.getContext("2d");

  const gradient = context.createRadialGradient(210, 180, 40, 256, 256, 245);
  gradient.addColorStop(0, "#3473d6");
  gradient.addColorStop(0.45, "#17376d");
  gradient.addColorStop(1, "#071327");

  context.fillStyle = gradient;
  context.beginPath();
  context.arc(256, 256, 238, 0, Math.PI * 2);
  context.fill();

  context.strokeStyle = "rgba(105,174,255,.72)";
  context.lineWidth = 8;
  context.stroke();

  context.shadowColor = "#4c9dff";
  context.shadowBlur = 44;
  context.fillStyle = "#8cd4ff";
  context.font = '900 150px "Segoe UI",sans-serif';
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText("VG", 256, 270);
  context.shadowBlur = 0;

  return canvas;
}

function roundedRect(context, x, y, width, height, radius) {
  context.beginPath();
  context.roundRect(x, y, width, height, radius);
}

function wrapCanvasText(context, text, x, y, maxWidth, lineHeight, maxLines) {
  const words = String(text).split(/\s+/);
  const lines = [];
  let line = "";

  for (const word of words) {
    const next = line ? line + " " + word : word;
    if (context.measureText(next).width <= maxWidth) {
      line = next;
      continue;
    }
    if (line) lines.push(line);
    line = word;
    if (lines.length >= maxLines - 1) break;
  }

  if (line && lines.length < maxLines) lines.push(line);
  lines.forEach((value, index) =>
    context.fillText(value, x, y + index * lineHeight)
  );
}

function clamp01(value) {
  return Math.min(1, Math.max(0, Number(value) || 0));
}

function disposeScene(scene) {
  scene.traverse((object) => {
    object.geometry?.dispose?.();

    const materials = Array.isArray(object.material)
      ? object.material
      : object.material
        ? [object.material]
        : [];

    for (const material of materials) {
      for (const key of [
        "map",
        "normalMap",
        "roughnessMap",
        "metalnessMap",
        "emissiveMap",
        "alphaMap"
      ]) {
        material[key]?.dispose?.();
      }
      material.dispose?.();
    }
  });
}
