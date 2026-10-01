import * as THREE from "./vendor/three.module.js";

const visual = document.getElementById("hero-visual");
const canvas = document.getElementById("hero-three");

if (visual && canvas) {
  initThreeHero(visual, canvas).catch((error) => {
    console.warn("VideoGrabber Three.js scene disabled", error);
    canvas.hidden = true;
    visual.classList.remove("three-ready");
  });
}

async function initThreeHero(visual, canvas) {
  const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const narrowViewport = matchMedia("(max-width: 720px)").matches;

  const renderer = new THREE.WebGLRenderer({
    canvas,
    alpha: true,
    antialias: true,
    powerPreference: "high-performance",
    premultipliedAlpha: false
  });
  canvas.dataset.engine = `three.js r${THREE.REVISION}`;
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.2;
  renderer.setClearColor(0x000000, 0);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 60);
  camera.position.set(0, 0.05, 10.45);

  const root = new THREE.Group();
  root.rotation.x = -0.04;
  scene.add(root);

  const hemi = new THREE.HemisphereLight(0x8ab7ff, 0x070b18, 1.35);
  scene.add(hemi);

  const key = new THREE.DirectionalLight(0xbfdcff, 4.5);
  key.position.set(-4.4, 5.5, 6.8);
  scene.add(key);

  const rim = new THREE.PointLight(0x654cff, 12.5, 20, 2.0);
  rim.position.set(4.2, 2.6, 4.8);
  scene.add(rim);

  const backRim = new THREE.PointLight(0x9b65ff, 10.5, 18, 2.0);
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

  const textureLoader = new THREE.TextureLoader();
  const [planetMap, planetBump, planetEmissive] = await Promise.all([
    loadTextureSafe(textureLoader, "/assets/videograbber-planet-map.webp"),
    loadTextureSafe(textureLoader, "/assets/videograbber-planet-bump.png"),
    loadTextureSafe(textureLoader, "/assets/videograbber-planet-emissive.webp")
  ]);

  for (const texture of [planetMap, planetEmissive]) {
    if (!texture) continue;
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.wrapS = THREE.RepeatWrapping;
    texture.anisotropy = Math.min(renderer.capabilities.getMaxAnisotropy(), 8);
  }
  if (planetBump) {
    planetBump.wrapS = THREE.RepeatWrapping;
    planetBump.anisotropy = Math.min(renderer.capabilities.getMaxAnisotropy(), 8);
  }

  const sphereMaterial = new THREE.MeshPhysicalMaterial({
    color: 0x86a6ff,
    map: planetMap || null,
    bumpMap: planetBump || null,
    bumpScale: planetBump ? 0.055 : 0,
    emissive: 0x315bff,
    emissiveMap: planetEmissive || null,
    emissiveIntensity: planetEmissive ? 1.5 : 0.32,
    metalness: 0.18,
    roughness: 0.32,
    clearcoat: 0.82,
    clearcoatRoughness: 0.18,
    sheen: 0.45,
    sheenColor: 0x4d63ff,
    sheenRoughness: 0.48
  });
  const sphere = new THREE.Mesh(
    new THREE.SphereGeometry(1.46, 80, 56),
    sphereMaterial
  );
  sphere.position.z = 0.12;
  sphere.rotation.y = -0.32;
  root.add(sphere);

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
  root.add(wire);

  const orbitMaterials = [
    makeOrbitMaterial(0x5ea8ff, 0.94),
    makeOrbitMaterial(0x9e61ff, 0.82),
    makeOrbitMaterial(0xff76c7, 0.70)
  ];
  const orbitBlue = new THREE.Color(0x65b5ff);
  const orbitPurple = new THREE.Color(0xa05cff);
  const orbitPink = new THREE.Color(0xff7bc9);

  const orbitDefinitions = [
    { radius: 2.20, tube: 0.030, rotation: [1.18, 0.12, -0.28], material: orbitMaterials[0] },
    { radius: 2.03, tube: 0.024, rotation: [1.29, -0.32, 0.46], material: orbitMaterials[1] },
    { radius: 1.87, tube: 0.020, rotation: [1.12, 0.48, 0.12], material: orbitMaterials[2] }
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
      title: "Видео",
      subtitle: "Скачайте в хорошем качестве",
      position: [2.45, 1.55, 0.92],
      rotation: [-0.05, -0.18, 0.13],
      accent: 0x4cbcff
    },
    {
      feature: "mp3",
      icon: "♪",
      title: "MP3",
      subtitle: "Извлеките аудио",
      position: [-2.68, -0.12, 0.72],
      rotation: [0.01, 0.2, 0.08],
      accent: 0xff9a55
    },
    {
      feature: "course",
      icon: "◇",
      title: "Полный курс",
      subtitle: "Курсы и закрытые страницы",
      position: [2.78, -0.06, 0.18],
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
  for (const spec of cardSpecs) {
    const card = createFeatureCard(spec, renderer);
    root.add(card.group);
    cards.set(spec.feature, card);
    pickTargets.push(card.body, card.label);
  }

  const raycaster = new THREE.Raycaster();
  const rayPointer = new THREE.Vector2();
  let hoveredFeature = null;
  let pointerDown = null;

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

  let targetFeature = null;
  const pointer = new THREE.Vector2(0, 0);
  const targetPointer = new THREE.Vector2(0, 0);
  const cameraBase = new THREE.Vector3(0, 0.05, 10.45);
  const cameraLookTarget = new THREE.Vector3();
  let scrollTarget = 0;
  let scrollProgress = 0;
  let heroVisible = true;
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
    const rect = visual.getBoundingClientRect();
    const width = Math.max(1, Math.round(rect.width));
    const height = Math.max(1, Math.round(rect.height));
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    renderer.setPixelRatio(Math.min(devicePixelRatio || 1, narrowViewport ? 1.1 : 1.5));
    renderer.setSize(width, height, false);
  };

  const render = () => {
    renderer.render(scene, camera);
    if (!visual.classList.contains("three-ready")) {
      visual.classList.add("three-ready");
      visual.dispatchEvent(new CustomEvent("videograbber:three-ready"));
    }
  };

  const tick = (time) => {
    const dt = Math.min((time - previousTime) / 1000, 0.05);
    previousTime = time;
    elapsed += dt;

    pointer.lerp(targetPointer, reducedMotion ? 1 : 0.075);
    scrollProgress = THREE.MathUtils.lerp(scrollProgress, scrollTarget, 0.055);

    const mobileFactor = narrowViewport ? 0.48 : 1;
    const cameraTargetX = pointer.x * 0.38 * mobileFactor;
    const cameraTargetY =
      cameraBase.y +
      pointer.y * 0.24 * mobileFactor +
      scrollProgress * 0.16 * mobileFactor;
    const cameraTargetZ =
      cameraBase.z -
      scrollProgress * 0.52 * mobileFactor +
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
      -scrollProgress * 0.13 * mobileFactor,
      0.055
    );

    accent.lerp(accentTarget, 0.065);
    sphereMaterial.emissive.copy(accent).multiplyScalar(
      planetEmissive ? 0.58 : 0.22
    );
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
      sphere.rotation.y += dt * 0.14;
      atmosphere.rotation.y -= dt * 0.050;
      rimShell.rotation.y += dt * 0.026;
      wire.rotation.y += dt * 0.036;
      core.rotation.z += dt * 0.018;

      orbits[0].rotation.z += dt * 0.12;
      orbits[1].rotation.z -= dt * 0.085;
      orbits[2].rotation.z += dt * 0.065;

      let index = 0;
      for (const [feature, card] of cards) {
        const selected = targetFeature === feature;
        const baseScale = selected ? 1.13 : 1;
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
    }

    render();
  };

  let lastPaintTime = 0;

  const startLoop = () => {
    if (animationRunning || reducedMotion || !heroVisible || document.hidden) {
      render();
      return;
    }
    animationRunning = true;
    previousTime = performance.now();
    lastPaintTime = 0;
    renderer.setAnimationLoop((time) => {
      if (narrowViewport && lastPaintTime && time - lastPaintTime < 32) return;
      lastPaintTime = time;
      tick(time);
    });
  };

  const stopLoop = () => {
    if (!animationRunning) return;
    animationRunning = false;
    renderer.setAnimationLoop(null);
  };

  const featureAccent = new Map(
    cardSpecs.map((spec) => [spec.feature, new THREE.Color(spec.accent)])
  );

  const setRaycastFeature = (feature) => {
    if (hoveredFeature === feature) return;
    hoveredFeature = feature || null;
    targetFeature = hoveredFeature;

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
    const hit = raycaster.intersectObjects(pickTargets, false)[0];
    const feature = hit?.object?.userData?.feature || null;
    setRaycastFeature(feature);
    return feature;
  };

  visual.addEventListener("pointermove", (event) => {
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
    pointerDown = { x: event.clientX, y: event.clientY, moved: 0 };
    raycastAt(event.clientX, event.clientY);
  });

  visual.addEventListener("pointerup", (event) => {
    const feature = raycastAt(event.clientX, event.clientY);
    const moved = pointerDown?.moved || 0;
    pointerDown = null;
    if (!feature || moved > 10) return;

    const link = document.querySelector(
      '.hero-hotspot[data-feature="' + CSS.escape(feature) + '"]'
    );
    link?.click();
  });

  visual.addEventListener("pointerleave", () => {
    targetPointer.set(0, 0);
    pointerDown = null;
    setRaycastFeature(null);
  });

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
      stopLoop();
      canvas.hidden = true;
      visual.classList.remove("three-ready");
    },
    false
  );

  resize();
  render();
  startLoop();

  window.addEventListener(
    "pagehide",
    () => {
      stopLoop();
      visibilityObserver.disconnect();
      resizeObserver.disconnect();
      window.removeEventListener("scroll", updateScrollTarget);
      disposeScene(scene);
      coreTexture.dispose();
      renderer.dispose();
    },
    { once: true }
  );
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

function makeOrbitMaterial(color, opacity) {
  const base = new THREE.Color(color);
  return new THREE.MeshStandardMaterial({
    color: base,
    emissive: base.clone(),
    emissiveIntensity: 1.15,
    metalness: 0.32,
    roughness: 0.24,
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
