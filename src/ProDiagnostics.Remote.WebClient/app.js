import {
  RemoteAttachClient,
  RemoteMethods,
  RemoteStreamTopics,
} from "./remote-client.js";

const uiLib = {
  tabulator: typeof window.Tabulator === "function",
  jstree: typeof window.$ === "function" && typeof window.$.fn?.jstree === "function",
  chart: typeof window.Chart === "function",
  svgPanZoom: typeof window.svgPanZoom === "function",
};

const tabulatorRegistry = new Map();
const tabulatorHostRegistry = new WeakMap();
const chartRegistry = new Map();

const settingsStorageKey = "prodiagnostics.remote.web.settings.v1";

const defaultSettings = Object.freeze({
  maxRows: 4000,
  autoRefreshOnSelection: true,
  autoRefreshTreeOnConnect: false,
});

const state = {
  activeTab: "properties",
  scope: "combined",
  selectedNodeId: null,
  selectedNodePath: null,
  selectedNodeDisplayName: null,
  selectedSource: null,
  trees: {
    combined: [],
    visual: [],
    logical: [],
  },
  propertiesSnapshot: null,
  bindingsSnapshot: null,
  stylesSnapshot: null,
  resourcesSnapshot: null,
  assetsSnapshot: null,
  previewSnapshot: null,
  previewCapabilities: null,
  previewLastFrameHash: null,
  elements3dSnapshot: null,
  logs: [],
  events: [],
  metrics: [],
  profiler: [],
  paused: {
    preview: false,
    logs: false,
    events: false,
    metrics: true,
    profiler: true,
  },
  selectedPropertyName: null,
  selectedStyleEntryIndex: -1,
  selectedStyleResolutionIndex: -1,
  selectedStyleSetterIndex: -1,
  treeUi: {
    collapsedByScope: {
      combined: new Set(),
      visual: new Set(),
      logical: new Set(),
    },
    visibleRowsByScope: {
      combined: [],
      visual: [],
      logical: [],
    },
  },
  treeRefreshPending: {
    combined: null,
    visual: null,
    logical: null,
  },
  gridUi: Object.create(null),
  elements3d: {
    renderMode: "svg",
    scene: null,
    camera: null,
    renderer: null,
    controls: null,
    root: null,
    resizeObserver: null,
    panZoomInstance: null,
  },
  preview: {
    pointerButtons: new Set(),
    focused: false,
  },
  settings: loadSettings(),
  remoteInspect: {
    pollHandle: null,
    inFlight: false,
    unsupported: false,
  },
  streamRenderPending: {
    logs: false,
    events: false,
    metrics: false,
    profiler: false,
    preview: false,
  },
};

const client = new RemoteAttachClient();
const dom = bindDom();

initializeRichUi();
wireUiEvents();
wireClientEvents();
applySettingsToInputs();
activateScopeTab(state.scope);
activateMainTab(state.activeTab);
renderTree();
renderProperties();
renderBindings();
renderStyles();
renderResources();
renderAssets();
renderPreview();
renderLogs();
renderEvents();
renderMetrics();
renderProfiler();
renderTransport(client.getTransportStats());
renderElements3d();
renderCodePanel();
syncPauseButtons();

function bindDom() {
  return {
    statusLabel: getById("statusLabel"),
    connectForm: getById("connectForm"),
    urlInput: getById("urlInput"),
    clientNameInput: getById("clientNameInput"),
    connectBtn: getById("connectBtn"),
    disconnectBtn: getById("disconnectBtn"),

    scopeTabs: document.querySelectorAll(".scope-tab"),
    treeFilterInput: getById("treeFilterInput"),
    refreshTreeBtn: getById("refreshTreeBtn"),
    treeSummary: getById("treeSummary"),
    treeContainer: getById("treeContainer"),

    mainTabs: document.querySelectorAll(".main-tab"),
    tabPanels: document.querySelectorAll(".tab-panel"),
    tabElements3d: getById("tab-elements3d"),

    refreshPreviewBtn: getById("refreshPreviewBtn"),
    pausePreviewBtn: getById("pausePreviewBtn"),
    previewApplySettingsBtn: getById("previewApplySettingsBtn"),
    previewTransportInput: getById("previewTransportInput"),
    previewTargetFpsInput: getById("previewTargetFpsInput"),
    previewScaleInput: getById("previewScaleInput"),
    previewMaxWidthInput: getById("previewMaxWidthInput"),
    previewMaxHeightInput: getById("previewMaxHeightInput"),
    previewEnableDiffInput: getById("previewEnableDiffInput"),
    previewIncludeFrameDataInput: getById("previewIncludeFrameDataInput"),
    previewSummary: getById("previewSummary"),
    previewStage: getById("previewStage"),
    previewImage: getById("previewImage"),
    previewSvg: getById("previewSvg"),

    refreshPropertiesBtn: getById("refreshPropertiesBtn"),
    refreshStylesFromPropertiesBtn: getById("refreshStylesFromPropertiesBtn"),
    propertiesTargetInfo: getById("propertiesTargetInfo"),
    propertiesSourceInfo: getById("propertiesSourceInfo"),
    propertyEditorInfo: getById("propertyEditorInfo"),
    propertyEditorNameInput: getById("propertyEditorNameInput"),
    propertyEditorTypeInput: getById("propertyEditorTypeInput"),
    propertyEditorValueInput: getById("propertyEditorValueInput"),
    propertyEditorNullInput: getById("propertyEditorNullInput"),
    propertyEditorClearInput: getById("propertyEditorClearInput"),
    applyPropertyEditorBtn: getById("applyPropertyEditorBtn"),
    setPropertyNameInput: getById("setPropertyNameInput"),
    setPropertyValueInput: getById("setPropertyValueInput"),
    setPropertyNullInput: getById("setPropertyNullInput"),
    setPropertyClearInput: getById("setPropertyClearInput"),
    setPropertyBtn: getById("setPropertyBtn"),
    propertiesFilterInput: getById("propertiesFilterInput"),
    framesFilterInput: getById("framesFilterInput"),
    propertiesTable: getById("propertiesTable"),
    framesTable: getById("framesTable"),

    refreshBindingsBtn: getById("refreshBindingsBtn"),
    clearBindingsBtn: getById("clearBindingsBtn"),
    bindingsOnlyErrorsInput: getById("bindingsOnlyErrorsInput"),
    bindingsSummary: getById("bindingsSummary"),
    bindingsViewModelsFilterInput: getById("bindingsViewModelsFilterInput"),
    bindingsBindingsFilterInput: getById("bindingsBindingsFilterInput"),
    bindingsViewModelsTable: getById("bindingsViewModelsTable"),
    bindingsBindingsTable: getById("bindingsBindingsTable"),

    refreshElements3dBtn: getById("refreshElements3dBtn"),
    elements3dRenderModeInput: getById("elements3dRenderModeInput"),
    depthSpacingInput: getById("depthSpacingInput"),
    elementsZoomInput: getById("elementsZoomInput"),
    elements3dResetViewBtn: getById("elements3dResetViewBtn"),
    elements3dZoomInBtn: getById("elements3dZoomInBtn"),
    elements3dZoomOutBtn: getById("elements3dZoomOutBtn"),
    elements3dFitBtn: getById("elements3dFitBtn"),
    elements3dSummary: getById("elements3dSummary"),
    elements3dStage: getById("elements3dStage"),
    elements3dCanvas: getByIdOptional("elements3dCanvas"),
    elements3dSvgContainer: getById("elements3dSvgContainer"),
    elements3dDepthChart: getById("elements3dDepthChart"),

    refreshResourcesBtn: getById("refreshResourcesBtn"),
    includeResourceEntriesInput: getById("includeResourceEntriesInput"),
    resourcesSummary: getById("resourcesSummary"),
    resourcesNodesTable: getById("resourcesNodesTable"),
    resourcesEntriesTable: getById("resourcesEntriesTable"),

    refreshAssetsBtn: getById("refreshAssetsBtn"),
    assetsSummary: getById("assetsSummary"),
    assetsTable: getById("assetsTable"),

    clearEventsBtn: getById("clearEventsBtn"),
    enableDefaultEventsBtn: getById("enableDefaultEventsBtn"),
    disableAllEventsBtn: getById("disableAllEventsBtn"),
    pauseEventsBtn: getById("pauseEventsBtn"),
    eventsSummary: getById("eventsSummary"),
    eventsTable: getById("eventsTable"),

    breakpointsEnabledInput: getById("breakpointsEnabledInput"),
    applyBreakpointsEnabledBtn: getById("applyBreakpointsEnabledBtn"),
    clearBreakpointsBtn: getById("clearBreakpointsBtn"),
    breakpointPropertyNameInput: getById("breakpointPropertyNameInput"),
    addPropertyBreakpointBtn: getById("addPropertyBreakpointBtn"),
    breakpointEventNameInput: getById("breakpointEventNameInput"),
    breakpointEventOwnerInput: getById("breakpointEventOwnerInput"),
    breakpointGlobalInput: getById("breakpointGlobalInput"),
    addEventBreakpointBtn: getById("addEventBreakpointBtn"),
    breakpointsSummary: getById("breakpointsSummary"),

    clearLogsBtn: getById("clearLogsBtn"),
    pauseLogsBtn: getById("pauseLogsBtn"),
    applyLogLevelsBtn: getById("applyLogLevelsBtn"),
    logsFilterInput: getById("logsFilterInput"),
    logsLevelVerbose: getById("logsLevelVerbose"),
    logsLevelDebug: getById("logsLevelDebug"),
    logsLevelInformation: getById("logsLevelInformation"),
    logsLevelWarning: getById("logsLevelWarning"),
    logsLevelError: getById("logsLevelError"),
    logsLevelFatal: getById("logsLevelFatal"),
    logsMaxEntriesInput: getById("logsMaxEntriesInput"),
    logsSummary: getById("logsSummary"),
    logsTable: getById("logsTable"),

    pauseMetricsBtn: getById("pauseMetricsBtn"),
    clearMetricsBtn: getById("clearMetricsBtn"),
    metricsFilterInput: getById("metricsFilterInput"),
    metricsSummary: getById("metricsSummary"),
    metricsChart: getById("metricsChart"),
    metricsSeriesTable: getById("metricsSeriesTable"),
    metricsRawTable: getById("metricsRawTable"),

    pauseProfilerBtn: getById("pauseProfilerBtn"),
    clearProfilerBtn: getById("clearProfilerBtn"),
    profilerSummary: getById("profilerSummary"),
    profilerChart: getById("profilerChart"),
    profilerTable: getById("profilerTable"),

    refreshStylesBtn: getById("refreshStylesBtn"),
    stylesSummary: getById("stylesSummary"),
    stylesVisualizerSummary: getById("stylesVisualizerSummary"),
    stylesVisualizerContent: getById("stylesVisualizerContent"),
    stylesTreeTable: getById("stylesTreeTable"),
    stylesResolutionTable: getById("stylesResolutionTable"),
    stylesSettersTable: getById("stylesSettersTable"),

    refreshTransportBtn: getById("refreshTransportBtn"),
    transportSummary: getById("transportSummary"),
    transportStatsTable: getById("transportStatsTable"),
    transportTimelineTable: getById("transportTimelineTable"),

    openSourceXamlBtn: getById("openSourceXamlBtn"),
    openSourceCodeBtn: getById("openSourceCodeBtn"),
    codeSummary: getById("codeSummary"),
    sourcePreview: getById("sourcePreview"),

    autoRefreshOnSelectionInput: getById("autoRefreshOnSelectionInput"),
    autoRefreshTreeOnConnectInput: getById("autoRefreshTreeOnConnectInput"),
    maxRowsInput: getById("maxRowsInput"),
    saveSettingsBtn: getById("saveSettingsBtn"),

    footerSelection: getById("footerSelection"),
    footerScope: getById("footerScope"),
    footerSession: getById("footerSession"),
  };
}

function initializeRichUi() {
  state.elements3d.renderMode = dom.elements3dRenderModeInput?.value || "svg";
  if (!uiLib.tabulator || !uiLib.jstree || !uiLib.chart || !uiLib.svgPanZoom) {
    const missing = [];
    if (!uiLib.tabulator) {
      missing.push("Tabulator");
    }
    if (!uiLib.jstree) {
      missing.push("jsTree");
    }
    if (!uiLib.chart) {
      missing.push("Chart.js");
    }
    if (!uiLib.svgPanZoom) {
      missing.push("svg-pan-zoom");
    }

    if (missing.length > 0) {
      setStatus("connecting", "Optional UI libraries unavailable (" + missing.join(", ") + "). Using fallback UI.");
    }
  }
}

function wireUiEvents() {
  dom.connectForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    await withUiGuard(async () => {
      const url = dom.urlInput.value.trim();
      const clientName = dom.clientNameInput.value.trim();
      await client.connect({
        url,
        clientName,
      });

      dom.disconnectBtn.disabled = false;
      dom.connectBtn.disabled = true;
      dom.footerSession.textContent = `Session: ${client.sessionId ?? "n/a"}`;
      setStatus("online", `Connected to ${url}`);
      await refreshOnConnect();
    }, "Connect failed");
  });

  dom.disconnectBtn.addEventListener("click", async () => {
    await withUiGuard(async () => {
      await client.disconnect("Disconnected by user");
      dom.connectBtn.disabled = false;
      dom.disconnectBtn.disabled = true;
      setStatus("offline", "Offline");
    });
  });

  dom.scopeTabs.forEach((button) => {
    button.addEventListener("click", async () => {
      const nextScope = button.getAttribute("data-scope");
      if (!nextScope || state.scope === nextScope) {
        return;
      }

      state.scope = nextScope;
      activateScopeTab(nextScope);
      dom.footerScope.textContent = `Scope: ${nextScope}`;
      ensureTreeSelectionForScope(nextScope);
      renderTree();
      if (client.isConnected) {
        await withUiGuard(refreshElements3dSnapshot);
      } else {
        renderElements3d();
      }
      if (state.trees[nextScope].length === 0 && client.isConnected) {
        await withUiGuard(() => refreshTree(nextScope));
      } else if (state.settings.autoRefreshOnSelection && client.isConnected) {
        await withUiGuard(refreshSelectionSnapshots);
      }
    });
  });

  dom.mainTabs.forEach((tabButton) => {
    tabButton.addEventListener("click", async () => {
      const nextTab = tabButton.getAttribute("data-tab");
      if (!nextTab) {
        return;
      }

      state.activeTab = nextTab;
      activateMainTab(nextTab);
      await withUiGuard(() => refreshActiveTabIfNeeded(nextTab));
    });
  });

  dom.refreshPreviewBtn.addEventListener("click", () =>
    withUiGuard(refreshPreview),
  );
  dom.pausePreviewBtn.addEventListener("click", () =>
    withUiGuard(() => togglePause("preview"), "Preview pause update failed"),
  );
  dom.previewApplySettingsBtn.addEventListener("click", () =>
    withUiGuard(applyPreviewSettings, "Preview settings update failed"),
  );
  dom.previewTransportInput.addEventListener("change", () => renderPreview());
  wirePreviewInputEvents();

  dom.treeFilterInput.addEventListener("input", () => renderTree());
  dom.treeContainer.addEventListener("keydown", (event) => {
    if (uiLib.jstree) {
      return;
    }
    void withUiGuard(() => handleTreeKeyDown(event));
  });
  dom.refreshTreeBtn.addEventListener("click", () =>
    withUiGuard(() => refreshTree(state.scope)),
  );

  dom.refreshPropertiesBtn.addEventListener("click", () =>
    withUiGuard(refreshProperties),
  );
  dom.refreshStylesFromPropertiesBtn.addEventListener("click", () =>
    withUiGuard(refreshStyles),
  );
  dom.setPropertyBtn.addEventListener("click", () =>
    withUiGuard(setPropertyValue, "Set property failed"),
  );
  dom.applyPropertyEditorBtn.addEventListener("click", () =>
    withUiGuard(applyPropertyEditor, "Set property failed"),
  );
  dom.propertiesFilterInput.addEventListener("input", () => renderProperties());
  dom.framesFilterInput.addEventListener("input", () => renderProperties());
  dom.refreshBindingsBtn.addEventListener("click", () =>
    withUiGuard(refreshBindings),
  );
  dom.clearBindingsBtn.addEventListener("click", () => {
    state.bindingsSnapshot = null;
    renderBindings();
  });
  dom.bindingsOnlyErrorsInput.addEventListener("change", () => renderBindings());
  dom.bindingsViewModelsFilterInput.addEventListener("input", () => renderBindings());
  dom.bindingsBindingsFilterInput.addEventListener("input", () => renderBindings());

  dom.refreshElements3dBtn.addEventListener("click", () =>
    withUiGuard(refreshElements3dSnapshot),
  );
  dom.elements3dRenderModeInput.addEventListener("change", () => {
    state.elements3d.renderMode = dom.elements3dRenderModeInput.value;
    renderElements3d();
  });
  dom.depthSpacingInput.addEventListener("change", () =>
    withUiGuard(() => applyElements3dViewSettings()),
  );
  dom.elementsZoomInput.addEventListener("change", () =>
    withUiGuard(() => applyElements3dViewSettings()),
  );
  dom.elements3dResetViewBtn.addEventListener("click", () =>
    withUiGuard(() =>
      applyElements3dViewSettings(
        {
          resetProjectionView: true,
        },
        {
          resetInputs: true,
        }),
    ),
  );
  dom.elements3dZoomInBtn.addEventListener("click", () => {
    const instance = state.elements3d.panZoomInstance;
    if (!instance) {
      return;
    }

    instance.zoomIn();
    syncElements3dZoomInputFromPanZoom();
  });
  dom.elements3dZoomOutBtn.addEventListener("click", () => {
    const instance = state.elements3d.panZoomInstance;
    if (!instance) {
      return;
    }

    instance.zoomOut();
    syncElements3dZoomInputFromPanZoom();
  });
  dom.elements3dFitBtn.addEventListener("click", () => {
    const instance = state.elements3d.panZoomInstance;
    if (!instance) {
      return;
    }

    instance.resetZoom();
    instance.center();
    instance.fit();
    syncElements3dZoomInputFromPanZoom();
  });

  dom.refreshResourcesBtn.addEventListener("click", () =>
    withUiGuard(refreshResources),
  );
  dom.refreshAssetsBtn.addEventListener("click", () =>
    withUiGuard(refreshAssets),
  );

  dom.clearEventsBtn.addEventListener("click", () =>
    withUiGuard(() => executeMutation(RemoteMethods.EventsClear, {})),
  );
  dom.enableDefaultEventsBtn.addEventListener("click", () =>
    withUiGuard(() => executeMutation(RemoteMethods.EventsDefaultsEnable, {})),
  );
  dom.disableAllEventsBtn.addEventListener("click", () =>
    withUiGuard(() => executeMutation(RemoteMethods.EventsDisableAll, {})),
  );
  dom.pauseEventsBtn.addEventListener("click", () => {
    void togglePause("events");
  });

  dom.applyBreakpointsEnabledBtn.addEventListener("click", () =>
    withUiGuard(() =>
      executeMutation(RemoteMethods.BreakpointsEnabledSet, {
        isEnabled: dom.breakpointsEnabledInput.checked,
      })),
  );
  dom.clearBreakpointsBtn.addEventListener("click", () =>
    withUiGuard(() => executeMutation(RemoteMethods.BreakpointsClear, {})),
  );
  dom.addPropertyBreakpointBtn.addEventListener("click", () =>
    withUiGuard(addPropertyBreakpoint),
  );
  dom.addEventBreakpointBtn.addEventListener("click", () =>
    withUiGuard(addEventBreakpoint),
  );

  dom.clearLogsBtn.addEventListener("click", () =>
    withUiGuard(async () => {
      await executeMutation(RemoteMethods.LogsClear, {});
      state.logs.length = 0;
      renderLogs();
    }),
  );
  dom.pauseLogsBtn.addEventListener("click", () => {
    void togglePause("logs");
  });
  dom.logsFilterInput.addEventListener("input", () => renderLogs());
  [
    dom.logsLevelVerbose,
    dom.logsLevelDebug,
    dom.logsLevelInformation,
    dom.logsLevelWarning,
    dom.logsLevelError,
    dom.logsLevelFatal,
  ].forEach((input) => input.addEventListener("change", () => renderLogs()));
  dom.applyLogLevelsBtn.addEventListener("click", () =>
    withUiGuard(applyLogLevels),
  );

  dom.pauseMetricsBtn.addEventListener("click", () =>
    withUiGuard(() => togglePause("metrics"), "Metrics pause update failed"),
  );
  dom.clearMetricsBtn.addEventListener("click", () => {
    state.metrics.length = 0;
    renderMetrics();
  });
  dom.metricsFilterInput.addEventListener("input", () => renderMetrics());

  dom.pauseProfilerBtn.addEventListener("click", () =>
    withUiGuard(() => togglePause("profiler"), "Profiler pause update failed"),
  );
  dom.clearProfilerBtn.addEventListener("click", () => {
    state.profiler.length = 0;
    renderProfiler();
  });

  dom.refreshStylesBtn.addEventListener("click", () => withUiGuard(refreshStyles));
  dom.refreshTransportBtn.addEventListener("click", () =>
    renderTransport(client.getTransportStats()),
  );

  dom.openSourceXamlBtn.addEventListener("click", () =>
    openSourceReference(state.selectedSource?.xaml),
  );
  dom.openSourceCodeBtn.addEventListener("click", () =>
    openSourceReference(state.selectedSource?.code),
  );

  dom.saveSettingsBtn.addEventListener("click", () => {
    state.settings = {
      maxRows: clampInteger(dom.maxRowsInput.value, 100, 100000, defaultSettings.maxRows),
      autoRefreshOnSelection: dom.autoRefreshOnSelectionInput.checked,
      autoRefreshTreeOnConnect: dom.autoRefreshTreeOnConnectInput.checked,
    };
    saveSettings(state.settings);
    setStatus("online", "Settings saved.");
    trimStreamBuffers();
    renderLogs();
    renderEvents();
    renderMetrics();
    renderProfiler();
  });
}

function wireClientEvents() {
  client.on("status", (payload) => {
    if (payload.state === "online") {
      setStatus("online", `Connected: ${payload.url}`);
      dom.connectBtn.disabled = true;
      dom.disconnectBtn.disabled = false;
      return;
    }

    if (payload.state === "connecting") {
      stopRemoteInspectLoop();
      setStatus("connecting", `Connecting: ${payload.url}`);
      return;
    }

    stopRemoteInspectLoop();
    setStatus("offline", payload.reason ? `Offline: ${payload.reason}` : "Offline");
    dom.connectBtn.disabled = false;
    dom.disconnectBtn.disabled = true;
    state.previewSnapshot = null;
    state.previewCapabilities = null;
    state.previewLastFrameHash = null;
    state.elements3dSnapshot = null;
    renderPreview();
    renderElements3d();
  });

  client.on(`stream:${RemoteStreamTopics.Logs}`, (packet) => {
    if (state.paused.logs) {
      return;
    }

    if (packet.payload) {
      state.logs.push(packet.payload);
      trimRows(state.logs);
      scheduleStreamRender("logs");
    }
  });

  client.on(`stream:${RemoteStreamTopics.Events}`, (packet) => {
    if (state.paused.events) {
      return;
    }

    if (packet.payload) {
      state.events.push(packet.payload);
      trimRows(state.events);
      scheduleStreamRender("events");
    }
  });

  client.on(`stream:${RemoteStreamTopics.Metrics}`, (packet) => {
    if (state.paused.metrics) {
      return;
    }

    if (packet.payload) {
      state.metrics.push(packet.payload);
      trimRows(state.metrics);
      scheduleStreamRender("metrics");
    }
  });

  client.on(`stream:${RemoteStreamTopics.Profiler}`, (packet) => {
    if (state.paused.profiler) {
      return;
    }

    if (packet.payload) {
      state.profiler.push(packet.payload);
      trimRows(state.profiler);
      scheduleStreamRender("profiler");
    }
  });

  client.on(`stream:${RemoteStreamTopics.Preview}`, (packet) => {
    if (state.paused.preview) {
      return;
    }

    if (packet.payload) {
      applyPreviewPayload(packet.payload);
      scheduleStreamRender("preview");
    }
  });

  client.on("helloAck", (message) => {
    setStatus("online", `Handshake acknowledged (v${message.negotiatedProtocolVersion}).`);
  });

  client.on("helloReject", (message) => {
    setStatus("offline", `Attach rejected: ${message.reason}. ${message.details}`);
  });

  client.on("clientError", (errorText) => {
    setStatus("offline", `Client error: ${errorText}`);
  });

  client.on("transport", (snapshot) => {
    if (state.activeTab === "transport") {
      renderTransport(snapshot);
    }
  });
}

async function refreshAllTrees() {
  const activeScope = state.scope;
  await refreshTree(activeScope);
  void prefetchInactiveTrees(activeScope);
  await refreshSelectionSnapshots();
}

async function refreshOnConnect() {
  state.previewLastFrameHash = null;
  state.previewSnapshot = null;
  await syncRemotePauseStates();
  await refreshPreviewCapabilities();

  await refreshTree(state.scope);
  if (state.settings.autoRefreshTreeOnConnect) {
    void prefetchInactiveTrees(state.scope);
  }
  await refreshActiveTabIfNeeded(state.activeTab);
  startRemoteInspectLoop();
}

async function prefetchInactiveTrees(activeScope) {
  const scopes = ["combined", "visual", "logical"];
  for (const scope of scopes) {
    if (scope === activeScope || !client.isConnected) {
      continue;
    }

    if ((state.trees[scope]?.length ?? 0) > 0) {
      continue;
    }

    try {
      await refreshTree(scope);
    } catch {
      return;
    }
  }
}

function startRemoteInspectLoop() {
  state.remoteInspect.unsupported = false;
  if (state.remoteInspect.pollHandle != null) {
    return;
  }

  state.remoteInspect.pollHandle = window.setInterval(() => {
    void pollRemoteHoveredInspect();
  }, 350);
}

function stopRemoteInspectLoop() {
  if (state.remoteInspect.pollHandle != null) {
    window.clearInterval(state.remoteInspect.pollHandle);
    state.remoteInspect.pollHandle = null;
  }
  state.remoteInspect.inFlight = false;
}

async function pollRemoteHoveredInspect() {
  if (!client.isConnected || state.remoteInspect.unsupported || state.remoteInspect.inFlight) {
    return;
  }

  if (document.hidden) {
    return;
  }

  state.remoteInspect.inFlight = true;
  try {
    const scope = state.scope;
    const response = await client.request(
      RemoteMethods.InspectHovered,
      {
        scope,
        requireInspectGesture: true,
        includeDevTools: false,
      },
      { timeoutMs: 3000 },
    );

    if (!client.isConnected || scope !== state.scope) {
      return;
    }

    const payload = response.payload ?? {};
    if (!payload.changed) {
      return;
    }

    const nodePath = typeof payload.targetNodePath === "string" ? payload.targetNodePath : "";
    if (!nodePath || nodePath === state.selectedNodePath) {
      return;
    }

    const nodes = state.trees[scope] ?? [];
    if (!nodes.some((node) => node.nodePath === nodePath)) {
      await refreshTree(scope);
    }

    await selectTreeNodeByPath(nodePath, { refreshSelection: true });
  } catch (error) {
    const message = normalizeError(error).toLowerCase();
    if (
      message.includes("unsupported method") ||
      message.includes("method_not_found") ||
      message.includes("request_method_not_found")
    ) {
      state.remoteInspect.unsupported = true;
      stopRemoteInspectLoop();
      setStatus("online", "Connected (remote Ctrl+Shift inspect is unavailable on this host).");
    }
  } finally {
    state.remoteInspect.inFlight = false;
  }
}

async function refreshTree(scope = state.scope, options = {}) {
  ensureConnected();
  const force = options?.force === true;
  if (!force) {
    const pending = state.treeRefreshPending[scope];
    if (pending) {
      await pending;
      return;
    }
  }

  const refreshPromise = (async () => {
    const response = await client.request(
      RemoteMethods.TreeSnapshotGet,
      { scope, includeSourceLocations: false, includeVisualDetails: false },
      { timeoutMs: 60000 },
    );
    const nodes = Array.isArray(response.payload?.nodes) ? response.payload.nodes : [];
    state.trees[scope] = nodes;
    normalizeCollapsedStateForScope(scope, nodes);

    if (scope === state.scope) {
      ensureTreeSelectionForScope(scope);

      renderTree();
      if (client.isConnected && state.activeTab === "elements3d") {
        await refreshElements3dSnapshot();
      } else if (state.activeTab === "elements3d") {
        renderElements3d();
      }
    }
  })();

  state.treeRefreshPending[scope] = refreshPromise;
  try {
    await refreshPromise;
  } finally {
    if (state.treeRefreshPending[scope] === refreshPromise) {
      state.treeRefreshPending[scope] = null;
    }
  }
}

async function refreshSelectionSnapshots() {
  await refreshActiveTabIfNeeded(state.activeTab);
}

async function refreshProperties() {
  ensureConnected();
  const response = await client.request(RemoteMethods.PropertiesSnapshotGet, {
    scope: state.scope,
    nodeId: state.selectedNodeId,
    nodePath: state.selectedNodePath,
    includeClrProperties: true,
  });
  state.propertiesSnapshot = response.payload ?? null;
  state.selectedSource = normalizeSourceLocation(state.propertiesSnapshot?.source);
  renderProperties();
  renderCodePanel();
}

async function refreshStyles() {
  ensureConnected();
  const response = await client.request(RemoteMethods.StylesSnapshotGet, {
    scope: state.scope,
    nodeId: state.selectedNodeId,
    nodePath: state.selectedNodePath,
  });
  state.stylesSnapshot = response.payload ?? null;
  renderStyles();
}

async function refreshBindings() {
  ensureConnected();
  const response = await client.request(RemoteMethods.BindingsSnapshotGet, {
    scope: state.scope,
    nodeId: state.selectedNodeId,
    nodePath: state.selectedNodePath,
  });
  state.bindingsSnapshot = response.payload ?? null;
  renderBindings();
}

async function refreshResources() {
  ensureConnected();
  const response = await client.request(RemoteMethods.ResourcesSnapshotGet, {
    includeEntries: dom.includeResourceEntriesInput.checked,
  });
  state.resourcesSnapshot = response.payload ?? null;
  renderResources();
}

async function refreshAssets() {
  ensureConnected();
  const response = await client.request(RemoteMethods.AssetsSnapshotGet, {});
  state.assetsSnapshot = response.payload ?? null;
  renderAssets();
}

async function refreshElements3dSnapshot() {
  if (!client.isConnected) {
    renderElements3d();
    return;
  }

  const width = Math.max(320, Math.floor(dom.elements3dStage?.clientWidth || 1600));
  const height = Math.max(240, Math.floor(dom.elements3dStage?.clientHeight || 520));
  const response = await client.request(RemoteMethods.Elements3DSnapshotGet, {
    includeNodes: true,
    includeVisibleNodeIds: true,
    includeSvgSnapshot: true,
    svgWidth: width,
    svgHeight: height,
    maxSvgNodes: 2500,
  }, { timeoutMs: 60000 });
  state.elements3dSnapshot = response.payload ?? null;
  if (state.elements3dSnapshot) {
    dom.depthSpacingInput.value = String(
      clampNumber(state.elements3dSnapshot.depthSpacing, 8, 80, 22),
    );
    dom.elementsZoomInput.value = String(
      clampNumber(state.elements3dSnapshot.zoom, 0.25, 24, 1),
    );
  }
  renderElements3d();
}

async function applyElements3dViewSettings(
  mutationOverrides = {},
  options = {}) {
  if (options.resetInputs) {
    dom.depthSpacingInput.value = "22";
    dom.elementsZoomInput.value = "1";
  }

  if (!client.isConnected) {
    renderElements3d();
    return;
  }

  const payload = {
    depthSpacing: clampNumber(dom.depthSpacingInput.value, 8, 80, 22),
    zoom: clampNumber(dom.elementsZoomInput.value, 0.25, 24, 1),
    ...mutationOverrides,
  };

  await executeMutationQuiet(RemoteMethods.Elements3DFiltersSet, payload);
  await refreshElements3dSnapshot();
}

async function executeMutation(method, payload) {
  ensureConnected();
  const response = await client.request(method, payload ?? {});
  const result = response.payload ?? {};
  const message = [result.operation, result.message].filter(Boolean).join(": ");
  if (message) {
    setStatus("online", message);
  }
  return result;
}

async function executeMutationQuiet(method, payload) {
  ensureConnected();
  await client.request(method, payload ?? {}, { timeoutMs: 5000 });
}

async function setPropertyValue() {
  const propertyName = dom.setPropertyNameInput.value.trim();
  if (!propertyName) {
    throw new Error("Property name is required.");
  }

  await executeMutation(RemoteMethods.PropertiesSet, {
    scope: state.scope,
    nodePath: state.selectedNodePath,
    propertyName,
    valueText: dom.setPropertyValueInput.value,
    valueIsNull: dom.setPropertyNullInput.checked,
    clearValue: dom.setPropertyClearInput.checked,
  });
  await refreshProperties();
}

async function addPropertyBreakpoint() {
  const propertyName = dom.breakpointPropertyNameInput.value.trim();
  if (!propertyName) {
    throw new Error("Property name is required.");
  }

  await executeMutation(RemoteMethods.BreakpointsPropertyAdd, {
    scope: state.scope,
    nodePath: state.selectedNodePath,
    propertyName,
  });
}

async function addEventBreakpoint() {
  const eventName = dom.breakpointEventNameInput.value.trim();
  if (!eventName) {
    throw new Error("Event name is required.");
  }

  await executeMutation(RemoteMethods.BreakpointsEventAdd, {
    scope: state.scope,
    nodePath: state.selectedNodePath,
    eventName,
    eventOwnerType: emptyToNull(dom.breakpointEventOwnerInput.value.trim()),
    isGlobal: dom.breakpointGlobalInput.checked,
  });
}

async function applyLogLevels() {
  const maxEntries = clampInteger(
    dom.logsMaxEntriesInput.value,
    100,
    50000,
    state.settings.maxRows,
  );
  dom.logsMaxEntriesInput.value = String(maxEntries);

  await executeMutation(RemoteMethods.LogsLevelsSet, {
    showVerbose: dom.logsLevelVerbose.checked,
    showDebug: dom.logsLevelDebug.checked,
    showInformation: dom.logsLevelInformation.checked,
    showWarning: dom.logsLevelWarning.checked,
    showError: dom.logsLevelError.checked,
    showFatal: dom.logsLevelFatal.checked,
    maxEntries,
  });
}

async function refreshActiveTabIfNeeded(tab) {
  if (!client.isConnected) {
    return;
  }

  switch (tab) {
    case "preview":
      await refreshPreview();
      return;
    case "properties":
      await refreshProperties();
      return;
    case "styles":
      await refreshStyles();
      return;
    case "bindings":
      await refreshBindings();
      return;
    case "resources":
      await refreshResources();
      return;
    case "assets":
      await refreshAssets();
      return;
    case "transport":
      renderTransport(client.getTransportStats());
      return;
    case "elements3d":
      await refreshElements3dSnapshot();
      return;
    case "code":
      await refreshProperties();
      return;
    default:
      return;
  }
}

function scheduleStreamRender(channel) {
  if (!Object.prototype.hasOwnProperty.call(state.streamRenderPending, channel)) {
    return;
  }

  if (state.streamRenderPending[channel]) {
    return;
  }

  state.streamRenderPending[channel] = true;
  const flush = () => {
    state.streamRenderPending[channel] = false;
    switch (channel) {
      case "logs":
        renderLogs();
        break;
      case "events":
        renderEvents();
        break;
      case "metrics":
        renderMetrics();
        break;
      case "profiler":
        renderProfiler();
        break;
      case "preview":
        renderPreview();
        break;
      default:
        break;
    }
  };

  if (typeof window.requestAnimationFrame === "function") {
    window.requestAnimationFrame(flush);
    return;
  }

  window.setTimeout(flush, 16);
}

function renderTree() {
  const scope = state.scope;
  const nodes = state.trees[scope] ?? [];
  const index = buildTreeIndex(nodes);
  const filter = dom.treeFilterInput.value.trim().toLowerCase();
  const visibleRows = computeVisibleTreeRows(scope, index, filter);
  state.treeUi.visibleRowsByScope[scope] = visibleRows;

  dom.treeSummary.textContent = `Nodes: ${nodes.length} | Visible: ${visibleRows.length}`;
  if (uiLib.jstree) {
    renderTreeWithJsTree(scope, nodes, filter);
    return;
  }

  dom.treeContainer.innerHTML = "";

  if (visibleRows.length === 0) {
    const empty = document.createElement("div");
    empty.className = "summary-line muted no-border";
    empty.textContent = "No tree nodes match current filter.";
    dom.treeContainer.appendChild(empty);
    return;
  }

  const fragment = document.createDocumentFragment();
  for (const rowModel of visibleRows) {
    const node = rowModel.node;
    const row = document.createElement("div");
    row.className = "tree-row";
    row.setAttribute("role", "treeitem");
    row.dataset.nodePath = node.nodePath ?? "";
    row.setAttribute("aria-level", String((node.depth ?? 0) + 1));
    row.setAttribute("aria-expanded", rowModel.hasChildren ? String(!rowModel.isCollapsed) : "false");
    if (state.selectedNodePath === node.nodePath) {
      row.classList.add("selected");
      row.setAttribute("aria-selected", "true");
    } else {
      row.setAttribute("aria-selected", "false");
    }

    const main = document.createElement("div");
    main.className = "tree-cell-main";
    main.style.paddingLeft = `${(node.depth ?? 0) * 14 + 6}px`;

    if (rowModel.hasChildren) {
      const expander = document.createElement("button");
      expander.className = "tree-expander";
      expander.type = "button";
      expander.textContent = rowModel.isCollapsed ? "▶" : "▼";
      expander.title = rowModel.isCollapsed ? "Expand node" : "Collapse node";
      expander.setAttribute("aria-label", expander.title);
      expander.addEventListener("click", (event) => {
        event.stopPropagation();
        toggleTreeNodeCollapse(scope, node.nodePath);
      });
      main.appendChild(expander);
    } else {
      const spacer = document.createElement("span");
      spacer.className = "tree-expander";
      spacer.textContent = "";
      main.appendChild(spacer);
    }

    const label = document.createElement("div");
    label.className = "tree-label";
    label.textContent = node.displayName || node.type || "(node)";
    label.title = node.nodePath ?? "";
    main.appendChild(label);

    const type = document.createElement("div");
    type.className = "tree-cell-type";
    type.textContent = node.type ?? "";
    type.title = node.type ?? "";

    row.appendChild(main);
    row.appendChild(type);
    row.addEventListener("click", () => {
      dom.treeContainer.focus({ preventScroll: true });
      void withUiGuard(() => selectTreeNodeByPath(node.nodePath));
    });
    fragment.appendChild(row);
  }

  dom.treeContainer.appendChild(fragment);

  if (state.selectedNodePath) {
    const selectedRow = dom.treeContainer.querySelector(
      `[data-node-path="${escapeCssToken(state.selectedNodePath)}"]`,
    );
    if (selectedRow) {
      selectedRow.scrollIntoView({ block: "nearest" });
    }
  }
}

function renderTreeWithJsTree(scope, nodes, filter) {
  const collapsed = getCollapsedSet(scope);
  const treeData = nodes
    .filter((node) => typeof node?.nodePath === "string" && node.nodePath.length > 0)
    .map((node) => ({
      id: node.nodePath,
      parent: node.parentNodePath || "#",
      text: node.displayName || node.type || "(node)",
      icon: false,
      data: {
        nodePath: node.nodePath,
        nodeId: node.nodeId ?? null,
        type: node.type ?? "",
      },
      state: {
        opened: !collapsed.has(node.nodePath),
        selected: state.selectedNodePath === node.nodePath,
      },
    }));

  dom.treeContainer.classList.add("library-tree");
  const $tree = window.$(dom.treeContainer);
  const existing = $tree.jstree(true);
  if (!existing) {
    $tree
      .on("select_node.jstree", (_event, data) => {
        const nodePath = data?.node?.data?.nodePath || data?.node?.id;
        if (!nodePath) {
          return;
        }
        if (nodePath === state.selectedNodePath) {
          return;
        }

        void withUiGuard(() => selectTreeNodeByPath(nodePath));
      })
      .on("open_node.jstree", (_event, data) => {
        const path = data?.node?.id;
        if (path) {
          collapsed.delete(path);
        }
      })
      .on("close_node.jstree", (_event, data) => {
        const path = data?.node?.id;
        if (path) {
          collapsed.add(path);
        }
      })
      .jstree({
        core: {
          data: treeData,
          multiple: false,
          check_callback: false,
          force_text: true,
        },
        plugins: ["wholerow", "search"],
      });
  } else {
    existing.settings.core.data = treeData;
    existing.refresh(true, false);
  }

  window.setTimeout(() => {
    const tree = $tree.jstree(true);
    if (!tree) {
      return;
    }

    if (filter) {
      tree.search(filter);
    } else {
      tree.clear_search();
    }

    if (state.selectedNodePath && tree.get_node(state.selectedNodePath)) {
      tree.deselect_all(true);
      tree.select_node(state.selectedNodePath, true, true);
    }
  }, 0);
}

function buildTreeIndex(nodes) {
  const byPath = new Map();
  const childrenByPath = new Map();
  const roots = [];

  for (const node of nodes) {
    if (!node || !node.nodePath) {
      continue;
    }

    byPath.set(node.nodePath, node);
    childrenByPath.set(node.nodePath, []);
  }

  for (const node of byPath.values()) {
    const parentPath = node.parentNodePath;
    if (!parentPath || !byPath.has(parentPath)) {
      roots.push(node.nodePath);
      continue;
    }

    childrenByPath.get(parentPath).push(node.nodePath);
  }

  return {
    byPath,
    childrenByPath,
    roots,
  };
}

function normalizeCollapsedStateForScope(scope, nodes) {
  const collapsed = getCollapsedSet(scope);
  const valid = new Set(nodes.map((node) => node.nodePath).filter(Boolean));
  for (const path of [...collapsed]) {
    if (!valid.has(path)) {
      collapsed.delete(path);
    }
  }
}

function ensureTreeSelectionForScope(scope) {
  const nodes = state.trees[scope] ?? [];
  if (!state.selectedNodePath || !nodes.some((node) => node.nodePath === state.selectedNodePath)) {
    const byNodeId =
      state.selectedNodeId == null
        ? null
        : nodes.find((node) => node.nodeId === state.selectedNodeId) ?? null;
    if (byNodeId) {
      state.selectedNodePath = byNodeId.nodePath ?? null;
      state.selectedNodeDisplayName =
        byNodeId.displayName ?? byNodeId.type ?? byNodeId.nodePath ?? null;
      state.selectedSource = normalizeSourceLocation(byNodeId.source);
      if (byNodeId.nodePath) {
        expandTreeNodeAncestors(scope, byNodeId.nodePath);
      }
      updateSelectionFooter();
      return;
    }

    const first = nodes[0] ?? null;
    state.selectedNodeId = first?.nodeId ?? null;
    state.selectedNodePath = first?.nodePath ?? null;
    state.selectedNodeDisplayName = first?.displayName ?? first?.type ?? null;
    state.selectedSource = normalizeSourceLocation(first?.source);
    if (first?.nodePath) {
      expandTreeNodeAncestors(scope, first.nodePath);
    }
    updateSelectionFooter();
    return;
  }

  expandTreeNodeAncestors(scope, state.selectedNodePath);
}

function computeVisibleTreeRows(scope, index, filterText) {
  const rows = [];
  const collapsed = getCollapsedSet(scope);
  const filter = filterText.trim().toLowerCase();
  const included = new Set();

  if (filter.length > 0) {
    for (const node of index.byPath.values()) {
      const haystack = `${node.displayName ?? ""}|${node.type ?? ""}|${node.nodePath ?? ""}|${node.elementName ?? ""}`;
      if (!haystack.toLowerCase().includes(filter)) {
        continue;
      }

      let cursor = node;
      while (cursor) {
        included.add(cursor.nodePath);
        if (!cursor.parentNodePath) {
          break;
        }
        cursor = index.byPath.get(cursor.parentNodePath) ?? null;
      }
    }
  }

  const walk = (path) => {
    const node = index.byPath.get(path);
    if (!node) {
      return;
    }

    if (filter.length > 0 && !included.has(path)) {
      return;
    }

    const children = index.childrenByPath.get(path) ?? [];
    const hasChildren = children.length > 0;
    const isCollapsed = collapsed.has(path);
    rows.push({
      node,
      hasChildren,
      isCollapsed,
    });

    if (hasChildren && filter.length === 0 && isCollapsed) {
      return;
    }

    for (const childPath of children) {
      walk(childPath);
    }
  };

  for (const rootPath of index.roots) {
    walk(rootPath);
  }

  return rows;
}

function getCollapsedSet(scope) {
  return state.treeUi.collapsedByScope[scope] ?? state.treeUi.collapsedByScope.combined;
}

function toggleTreeNodeCollapse(scope, nodePath) {
  if (!nodePath) {
    return;
  }

  const collapsed = getCollapsedSet(scope);
  if (collapsed.has(nodePath)) {
    collapsed.delete(nodePath);
  } else {
    collapsed.add(nodePath);
  }
  renderTree();
}

function expandTreeNodeAncestors(scope, nodePath) {
  if (!nodePath) {
    return;
  }

  const nodes = state.trees[scope] ?? [];
  const byPath = new Map(nodes.map((node) => [node.nodePath, node]));
  const collapsed = getCollapsedSet(scope);
  let cursorPath = nodePath;
  while (cursorPath) {
    collapsed.delete(cursorPath);
    const cursor = byPath.get(cursorPath);
    cursorPath = cursor?.parentNodePath ?? null;
  }
}

async function selectTreeNodeByPath(nodePath, options = {}) {
  if (!nodePath) {
    return;
  }
  if (!options.force && nodePath === state.selectedNodePath) {
    return;
  }

  const scope = state.scope;
  const nodes = state.trees[scope] ?? [];
  const node = nodes.find((entry) => entry.nodePath === nodePath);
  if (!node) {
    return;
  }

  state.selectedNodePath = node.nodePath;
  state.selectedNodeId = node.nodeId ?? null;
  state.selectedNodeDisplayName = node.displayName ?? node.type ?? node.nodePath;
  state.selectedSource = normalizeSourceLocation(node.source);
  expandTreeNodeAncestors(scope, node.nodePath);
  updateSelectionFooter();
  if (uiLib.jstree) {
    syncJsTreeSelection(node.nodePath);
  } else {
    renderTree();
  }
  renderElements3d();
  renderCodePanel();

  const refreshSelection = options.refreshSelection ?? state.settings.autoRefreshOnSelection;
  if (refreshSelection && client.isConnected) {
    await refreshSelectionSnapshots();
  }
}

function syncJsTreeSelection(nodePath) {
  if (!uiLib.jstree) {
    return;
  }

  const tree = window.$(dom.treeContainer).jstree(true);
  if (!tree || !tree.get_node(nodePath)) {
    return;
  }

  tree.deselect_all(true);
  tree.select_node(nodePath, true, true);
}

async function handleTreeKeyDown(event) {
  const navigable = new Set(["ArrowDown", "ArrowUp", "ArrowLeft", "ArrowRight", "Home", "End", "Enter"]);
  if (!navigable.has(event.key)) {
    return;
  }

  event.preventDefault();
  const visible = state.treeUi.visibleRowsByScope[state.scope] ?? [];
  if (visible.length === 0) {
    return;
  }

  let currentIndex = visible.findIndex((row) => row.node.nodePath === state.selectedNodePath);
  if (currentIndex < 0) {
    currentIndex = 0;
  }

  const current = visible[currentIndex];
  if (!current) {
    return;
  }

  switch (event.key) {
    case "ArrowDown":
      if (currentIndex < visible.length - 1) {
        await selectTreeNodeByPath(visible[currentIndex + 1].node.nodePath);
      }
      return;
    case "ArrowUp":
      if (currentIndex > 0) {
        await selectTreeNodeByPath(visible[currentIndex - 1].node.nodePath);
      }
      return;
    case "Home":
      await selectTreeNodeByPath(visible[0].node.nodePath);
      return;
    case "End":
      await selectTreeNodeByPath(visible[visible.length - 1].node.nodePath);
      return;
    case "ArrowRight":
      if (current.hasChildren && current.isCollapsed) {
        toggleTreeNodeCollapse(state.scope, current.node.nodePath);
      } else if (current.hasChildren) {
        const next = visible[currentIndex + 1];
        if (next && next.node.parentNodePath === current.node.nodePath) {
          await selectTreeNodeByPath(next.node.nodePath);
        }
      }
      return;
    case "ArrowLeft":
      if (current.hasChildren && !current.isCollapsed) {
        toggleTreeNodeCollapse(state.scope, current.node.nodePath);
      } else if (current.node.parentNodePath) {
        await selectTreeNodeByPath(current.node.parentNodePath);
      }
      return;
    case "Enter":
      await selectTreeNodeByPath(current.node.nodePath, { refreshSelection: true });
      return;
    default:
      return;
  }
}

function renderProperties() {
  const snapshot = state.propertiesSnapshot;
  const propertyFilter = dom.propertiesFilterInput.value.trim().toLowerCase();
  const frameFilter = dom.framesFilterInput.value.trim().toLowerCase();
  if (!snapshot) {
    dom.propertiesTargetInfo.textContent = "Select a tree node and refresh properties.";
    dom.propertiesSourceInfo.textContent = "";
    dom.propertyEditorInfo.textContent = "Select a property row to edit.";
    clearPropertyEditor();
    renderTable(dom.propertiesTable, [], [], { tableStateKey: "properties" });
    renderTable(dom.framesTable, [], [], { tableStateKey: "frames" });
    return;
  }

  dom.propertiesTargetInfo.textContent =
    `${snapshot.target ?? "(unknown)"} (${snapshot.targetType ?? "n/a"})`;
  dom.propertiesSourceInfo.innerHTML = renderSourceLocationHtml(snapshot.source);

  const properties = Array.isArray(snapshot.properties) ? snapshot.properties : [];
  const visibleProperties = propertyFilter
    ? properties.filter((property) => propertyMatchesFilter(property, propertyFilter))
    : properties;
  if (state.selectedPropertyName && !properties.some((property) => property.name === state.selectedPropertyName)) {
    state.selectedPropertyName = null;
  }
  if (state.selectedPropertyName && !visibleProperties.some((property) => property.name === state.selectedPropertyName)) {
    state.selectedPropertyName = visibleProperties[0]?.name ?? null;
  }

  renderTable(dom.propertiesTable, visibleProperties, [
    column("name", "Property"),
    column("valueText", "Value", (value, row) => renderPropertyValueCell(value, row)),
    column("type", "Type"),
    column("assignedType", "Assigned Type"),
    column("propertyType", "Property Type"),
    column("priority", "Priority"),
    column("group", "Group"),
    column("declaringType", "Declaring"),
    column("isAttached", "Attached"),
    column("isReadOnly", "ReadOnly"),
  ], {
    tableStateKey: "properties",
    rowKey: (row) => row.name,
    selectedRowKey: state.selectedPropertyName,
    defaultSortKey: "name",
    onRowSelected: (row) => setSelectedProperty(row),
  });

  if (!state.selectedPropertyName && visibleProperties.length > 0) {
    setSelectedProperty(visibleProperties[0]);
  } else if (!state.selectedPropertyName && visibleProperties.length === 0) {
    dom.propertyEditorInfo.textContent = "No properties match filter.";
    clearPropertyEditor();
  } else if (state.selectedPropertyName) {
    const selected = visibleProperties.find((property) => property.name === state.selectedPropertyName)
      ?? properties.find((property) => property.name === state.selectedPropertyName);
    if (selected) {
      syncPropertyEditorFromProperty(selected);
    }
  }

  const frames = Array.isArray(snapshot.frames) ? snapshot.frames : [];
  const frameRows = [];
  for (const frame of frames) {
    frameRows.push({
      description: frame.description,
      isActive: frame.isActive,
      sourceLocation: frame.sourceLocation,
      setterName: "",
      setterValue: "",
      setterActive: "",
    });

    const setters = Array.isArray(frame.setters) ? frame.setters : [];
    for (const setter of setters) {
      frameRows.push({
        description: "  ↳ " + setter.name,
        isActive: "",
        sourceLocation: setter.sourceLocation,
        setterName: setter.name,
        setterValue: setter.valueText,
        setterActive: setter.isActive,
      });
    }
  }

  const visibleFrameRows = frameFilter
    ? frameRows.filter((row) => frameRowMatchesFilter(row, frameFilter))
    : frameRows;

  renderTable(dom.framesTable, visibleFrameRows, [
    column("description", "Frame / Setter"),
    column("isActive", "Active"),
    column("setterValue", "Value"),
    column("sourceLocation", "Source"),
  ], {
    tableStateKey: "frames",
    defaultSortKey: "description",
  });
}

function renderBindings() {
  const snapshot = state.bindingsSnapshot;
  if (!snapshot) {
    dom.bindingsSummary.textContent = "Select a tree node and refresh bindings.";
    renderTable(dom.bindingsViewModelsTable, [], [], { tableStateKey: "bindings-viewmodels" });
    renderTable(dom.bindingsBindingsTable, [], [], { tableStateKey: "bindings-bindings" });
    return;
  }

  const allViewModels = Array.isArray(snapshot.viewModels) ? snapshot.viewModels : [];
  const allBindings = Array.isArray(snapshot.bindings) ? snapshot.bindings : [];
  const viewModelsFilter = dom.bindingsViewModelsFilterInput.value.trim().toLowerCase();
  const bindingsFilter = dom.bindingsBindingsFilterInput.value.trim().toLowerCase();
  const showOnlyErrors = dom.bindingsOnlyErrorsInput.checked;

  const visibleViewModels = viewModelsFilter
    ? allViewModels.filter((entry) => viewModelEntryMatchesFilter(entry, viewModelsFilter))
    : allViewModels;
  const visibleBindings = allBindings.filter((entry) => {
    if (showOnlyErrors && !entry?.hasError) {
      return false;
    }

    if (!bindingsFilter) {
      return true;
    }

    return bindingEntryMatchesFilter(entry, bindingsFilter);
  });

  const inspectedElement = snapshot.inspectedElement ?? "(none)";
  const inspectedType = snapshot.inspectedElementType ?? "";
  dom.bindingsSummary.textContent =
    `Selected: ${inspectedElement} ${inspectedType ? `(${inspectedType})` : ""} | ViewModels: ${allViewModels.length} | Visible: ${visibleViewModels.length} | Bindings: ${allBindings.length} | Visible Bindings: ${visibleBindings.length}`;

  renderTable(dom.bindingsViewModelsTable, visibleViewModels, [
    column("level", "Level"),
    column("element", "Element"),
    column("priority", "Priority"),
    column("viewModelType", "ViewModel Type"),
    column("valuePreview", "Value"),
    column("isCurrent", "Current"),
    column("sourceLocation", "Source"),
    column("nodePath", "Path"),
  ], {
    tableStateKey: "bindings-viewmodels",
    defaultSortKey: "level",
    defaultSortAsc: true,
    onRowSelected: (row) => {
      const nodePath = emptyToNull(row?.nodePath);
      if (!nodePath || nodePath === state.selectedNodePath) {
        return;
      }

      void withUiGuard(() =>
        selectTreeNodeByPath(nodePath, { refreshSelection: true }));
    },
  });

  renderTable(dom.bindingsBindingsTable, visibleBindings, [
    column("propertyName", "Property"),
    column("ownerType", "Owner"),
    column("priority", "Priority"),
    column("status", "Status"),
    column("bindingDescription", "Expression"),
    column("valuePreview", "Value"),
    column("valueType", "Type"),
    column("diagnostic", "Diagnostic"),
    column("hasError", "Error"),
    column("sourceLocation", "Source"),
    column("nodePath", "Path"),
  ], {
    tableStateKey: "bindings-bindings",
    defaultSortKey: "hasError",
    defaultSortAsc: false,
    onRowSelected: (row) => {
      const nodePath = emptyToNull(row?.nodePath);
      if (!nodePath || nodePath === state.selectedNodePath) {
        return;
      }

      void withUiGuard(() =>
        selectTreeNodeByPath(nodePath, { refreshSelection: true }));
    },
  });
}

function viewModelEntryMatchesFilter(entry, filterText) {
  const haystack = [
    entry?.level,
    entry?.element,
    entry?.priority,
    entry?.viewModelType,
    entry?.valuePreview,
    entry?.isCurrent,
    entry?.sourceLocation,
    entry?.nodePath,
  ]
    .filter((value) => value != null)
    .join("|")
    .toLowerCase();
  return haystack.includes(filterText);
}

function bindingEntryMatchesFilter(entry, filterText) {
  const haystack = [
    entry?.propertyName,
    entry?.ownerType,
    entry?.priority,
    entry?.status,
    entry?.bindingDescription,
    entry?.valuePreview,
    entry?.valueType,
    entry?.diagnostic,
    entry?.hasError,
    entry?.sourceLocation,
    entry?.nodePath,
  ]
    .filter((value) => value != null)
    .join("|")
    .toLowerCase();
  return haystack.includes(filterText);
}

function propertyMatchesFilter(property, filterText) {
  const haystack = [
    property?.name,
    property?.valueText,
    property?.type,
    property?.assignedType,
    property?.propertyType,
    property?.declaringType,
    property?.priority,
    property?.group,
    property?.isAttached,
    property?.isReadOnly,
  ]
    .filter((value) => value != null)
    .join("|")
    .toLowerCase();
  return haystack.includes(filterText);
}

function frameRowMatchesFilter(row, filterText) {
  const haystack = [
    row?.description,
    row?.setterValue,
    row?.sourceLocation,
    row?.setterName,
    row?.isActive,
    row?.setterActive,
  ]
    .filter((value) => value != null)
    .join("|")
    .toLowerCase();
  return haystack.includes(filterText);
}

function renderPropertyValueCell(value, row) {
  const container = document.createElement("div");
  container.className = "cell-editor";

  const isBoolean = isBooleanProperty(row);
  if (isBoolean) {
    const checkbox = document.createElement("input");
    checkbox.type = "checkbox";
    checkbox.checked = parseBooleanValue(value);
    checkbox.disabled = Boolean(row.isReadOnly);
    checkbox.addEventListener("click", (event) => event.stopPropagation());
    checkbox.addEventListener("change", () => {
      void withUiGuard(
        () => quickSetBooleanProperty(row, checkbox.checked),
        "Set property failed",
      );
    });
    container.appendChild(checkbox);
  }

  const text = document.createElement("span");
  text.textContent = value == null ? "" : String(value);
  text.title = text.textContent;
  container.appendChild(text);
  return container;
}

function isBooleanProperty(property) {
  const propertyType = String(property?.propertyType ?? property?.type ?? "");
  return /(^|\.)(Boolean|bool)$/i.test(propertyType);
}

function parseBooleanValue(value) {
  if (typeof value === "boolean") {
    return value;
  }
  if (typeof value === "string") {
    return value.trim().toLowerCase() === "true";
  }
  return false;
}

function clearPropertyEditor() {
  dom.propertyEditorNameInput.value = "";
  dom.propertyEditorTypeInput.value = "";
  dom.propertyEditorValueInput.value = "";
  dom.propertyEditorNullInput.checked = false;
  dom.propertyEditorClearInput.checked = false;
}

function setSelectedProperty(property) {
  if (!property?.name) {
    state.selectedPropertyName = null;
    dom.propertyEditorInfo.textContent = "Select a property row to edit.";
    clearPropertyEditor();
    return;
  }

  state.selectedPropertyName = property.name;
  syncPropertyEditorFromProperty(property);
}

function syncPropertyEditorFromProperty(property) {
  dom.propertyEditorInfo.textContent = `${property.name} (${property.propertyType ?? property.type ?? "n/a"})`;
  dom.propertyEditorNameInput.value = property.name ?? "";
  dom.propertyEditorTypeInput.value = property.propertyType ?? property.type ?? "";
  dom.propertyEditorValueInput.value = property.valueText ?? "";
  dom.propertyEditorNullInput.checked = false;
  dom.propertyEditorClearInput.checked = false;

  dom.setPropertyNameInput.value = property.name ?? "";
  dom.setPropertyValueInput.value = property.valueText ?? "";
  dom.setPropertyNullInput.checked = false;
  dom.setPropertyClearInput.checked = false;
}

async function applyPropertyEditor() {
  const propertyName = dom.propertyEditorNameInput.value.trim();
  if (!propertyName) {
    throw new Error("Select a property row first.");
  }

  await executeMutation(RemoteMethods.PropertiesSet, {
    scope: state.scope,
    nodePath: state.selectedNodePath,
    propertyName,
    valueText: dom.propertyEditorValueInput.value,
    valueIsNull: dom.propertyEditorNullInput.checked,
    clearValue: dom.propertyEditorClearInput.checked,
  });

  await refreshProperties();
}

async function quickSetBooleanProperty(property, isChecked) {
  if (!property?.name || property.isReadOnly) {
    return;
  }

  await executeMutation(RemoteMethods.PropertiesSet, {
    scope: state.scope,
    nodePath: state.selectedNodePath,
    propertyName: property.name,
    valueText: String(Boolean(isChecked)),
    valueIsNull: false,
    clearValue: false,
  });

  await refreshProperties();
}

function renderStyles() {
  const snapshot = state.stylesSnapshot;
  if (!snapshot) {
    dom.stylesSummary.textContent = "No styles snapshot loaded.";
    dom.stylesVisualizerSummary.textContent = "Select style tree/resolution/setter rows for semantic details.";
    dom.stylesVisualizerContent.innerHTML = "";
    renderTable(dom.stylesTreeTable, [], [], { tableStateKey: "styles-tree" });
    renderTable(dom.stylesResolutionTable, [], [], { tableStateKey: "styles-resolution" });
    renderTable(dom.stylesSettersTable, [], [], { tableStateKey: "styles-setters" });
    return;
  }

  const entries = Array.isArray(snapshot.treeEntries) ? snapshot.treeEntries : [];
  const resolution = Array.isArray(snapshot.resolution) ? snapshot.resolution : [];
  const setters = Array.isArray(snapshot.setters) ? snapshot.setters : [];
  dom.stylesSummary.textContent =
    `Root: ${snapshot.inspectedRoot ?? "n/a"} | Tree: ${entries.length} | Resolution: ${resolution.length} | Setters: ${setters.length}`;

  if (state.selectedStyleEntryIndex >= entries.length) {
    state.selectedStyleEntryIndex = entries.length - 1;
  }
  if (state.selectedStyleResolutionIndex >= resolution.length) {
    state.selectedStyleResolutionIndex = resolution.length - 1;
  }
  if (state.selectedStyleSetterIndex >= setters.length) {
    state.selectedStyleSetterIndex = setters.length - 1;
  }

  renderTable(dom.stylesTreeTable, entries, [
    column("depth", "Depth"),
    column("element", "Element"),
    column("elementType", "Type"),
    column("classes", "Classes"),
    column("pseudoClasses", "Pseudo"),
    column("frameCount", "Frames"),
    column("activeFrameCount", "Active"),
    column("sourceLocation", "Source"),
  ], {
    tableStateKey: "styles-tree",
    rowKey: (_, index) => String(index),
    selectedRowKey: state.selectedStyleEntryIndex >= 0 ? String(state.selectedStyleEntryIndex) : null,
    defaultSortKey: "depth",
    defaultSortAsc: true,
    onRowSelected: (_, index) => {
      state.selectedStyleEntryIndex = index;
      renderStyleVisualizer(entries, resolution, setters);
    },
  });

  renderTable(dom.stylesResolutionTable, resolution, [
    column("order", "Order"),
    column("hostLevel", "Host Level"),
    column("host", "Host"),
    column("hostKind", "Host Kind"),
    column("propagationScope", "Scope"),
    column("logicalDistance", "Logical Δ"),
    column("visualDistance", "Visual Δ"),
    column("stylesInitialized", "Initialized"),
    column("style", "Style"),
    column("styleKind", "Style Kind"),
    column("selector", "Selector"),
    column("path", "Path"),
    column("appliedCount", "Applied"),
    column("activeCount", "Active"),
    column("sourceLocation", "Source"),
    column("notes", "Notes"),
  ], {
    tableStateKey: "styles-resolution",
    rowKey: (_, index) => String(index),
    selectedRowKey: state.selectedStyleResolutionIndex >= 0 ? String(state.selectedStyleResolutionIndex) : null,
    defaultSortKey: "order",
    defaultSortAsc: true,
    onRowSelected: (_, index) => {
      state.selectedStyleResolutionIndex = index;
      renderStyleVisualizer(entries, resolution, setters);
    },
  });

  renderTable(dom.stylesSettersTable, setters, [
    column("name", "Setter"),
    column("valueText", "Value"),
    column("isActive", "Active"),
    column("sourceLocation", "Source"),
  ], {
    tableStateKey: "styles-setters",
    rowKey: (_, index) => String(index),
    selectedRowKey: state.selectedStyleSetterIndex >= 0 ? String(state.selectedStyleSetterIndex) : null,
    defaultSortKey: "name",
    defaultSortAsc: true,
    onRowSelected: (_, index) => {
      state.selectedStyleSetterIndex = index;
      renderStyleVisualizer(entries, resolution, setters);
    },
  });

  renderStyleVisualizer(entries, resolution, setters);
}

function renderStyleVisualizer(entries, resolution, setters) {
  const entry = state.selectedStyleEntryIndex >= 0 ? entries[state.selectedStyleEntryIndex] : null;
  const trace = state.selectedStyleResolutionIndex >= 0 ? resolution[state.selectedStyleResolutionIndex] : null;
  const setter = state.selectedStyleSetterIndex >= 0 ? setters[state.selectedStyleSetterIndex] : null;
  const hasData = Boolean(entry || trace || setter);
  if (!hasData) {
    dom.stylesVisualizerSummary.textContent = "Select style tree/resolution/setter rows for semantic details.";
    dom.stylesVisualizerContent.innerHTML = "";
    return;
  }

  dom.stylesVisualizerSummary.textContent = `Entry: ${entry?.element ?? "n/a"} | Trace: ${trace?.style ?? "n/a"} | Setter: ${setter?.name ?? "n/a"}`;
  dom.stylesVisualizerContent.innerHTML = "";

  const items = [
    ["Element", entry?.element],
    ["Element Type", entry?.elementType],
    ["Classes", entry?.classes],
    ["Pseudo", entry?.pseudoClasses],
    ["Trace Host", trace?.host],
    ["Trace Scope", trace?.propagationScope],
    ["Style", trace?.style],
    ["Selector", trace?.selector],
    ["Setter", setter?.name],
    ["Setter Value", setter?.valueText],
    ["Setter Active", setter?.isActive],
    ["Source", setter?.sourceLocation ?? trace?.sourceLocation ?? entry?.sourceLocation],
  ].filter((item) => item[1] != null && String(item[1]).length > 0);

  for (const [key, value] of items) {
    const row = document.createElement("div");
    row.className = "style-visualizer-item";

    const keySpan = document.createElement("span");
    keySpan.className = "style-visualizer-key";
    keySpan.textContent = `${key}:`;

    const valueSpan = document.createElement("span");
    valueSpan.textContent = String(value);
    valueSpan.title = String(value);

    row.appendChild(keySpan);
    row.appendChild(valueSpan);
    dom.stylesVisualizerContent.appendChild(row);
  }
}

function renderResources() {
  const snapshot = state.resourcesSnapshot;
  if (!snapshot) {
    dom.resourcesSummary.textContent = "No resources loaded.";
    renderTable(dom.resourcesNodesTable, [], [], { tableStateKey: "resources-nodes" });
    renderTable(dom.resourcesEntriesTable, [], [], { tableStateKey: "resources-entries" });
    return;
  }

  const nodes = Array.isArray(snapshot.nodes) ? snapshot.nodes : [];
  const entries = Array.isArray(snapshot.entries) ? snapshot.entries : [];
  dom.resourcesSummary.textContent =
    `Nodes: ${nodes.length} | Entries: ${entries.length}`;

  renderTable(dom.resourcesNodesTable, nodes, [
    column("depth", "Depth"),
    column("kind", "Kind"),
    column("name", "Name"),
    column("secondaryText", "Secondary"),
    column("valueType", "Value Type"),
    column("valuePreview", "Preview"),
    column("sourceLocation", "Source"),
    column("nodePath", "Path"),
  ], {
    tableStateKey: "resources-nodes",
    defaultSortKey: "depth",
    defaultSortAsc: true,
  });

  renderTable(dom.resourcesEntriesTable, entries, [
    column("nodePath", "Node"),
    column("keyDisplay", "Key"),
    column("keyType", "Key Type"),
    column("valueType", "Value Type"),
    column("valuePreview", "Preview"),
    column("isDeferred", "Deferred"),
    column("sourceLocation", "Source"),
  ], {
    tableStateKey: "resources-entries",
    defaultSortKey: "keyDisplay",
    defaultSortAsc: true,
  });
}

function renderAssets() {
  const snapshot = state.assetsSnapshot;
  if (!snapshot) {
    dom.assetsSummary.textContent = "No assets loaded.";
    renderTable(dom.assetsTable, [], [], { tableStateKey: "assets" });
    return;
  }

  const assets = Array.isArray(snapshot.assets) ? snapshot.assets : [];
  dom.assetsSummary.textContent = `Assets: ${assets.length}`;
  renderTable(dom.assetsTable, assets, [
    column("assemblyName", "Assembly"),
    column("assetPath", "Path"),
    column("name", "Name"),
    column("kind", "Kind"),
    column("uri", "URI"),
    column("sourceLocation", "Source"),
  ], {
    tableStateKey: "assets",
    defaultSortKey: "assetPath",
    defaultSortAsc: true,
  });
}

async function refreshPreviewCapabilities() {
  if (!client.isConnected) {
    return;
  }

  const response = await client.request(RemoteMethods.PreviewCapabilitiesGet, {});
  state.previewCapabilities = response.payload ?? null;
  if (state.previewCapabilities) {
    const capabilities = state.previewCapabilities;
    const transports = Array.isArray(capabilities.supportedTransports)
      ? capabilities.supportedTransports
      : [];
    const selectedTransport = transports.includes(dom.previewTransportInput.value)
      ? dom.previewTransportInput.value
      : (capabilities.defaultTransport || "svg");
    dom.previewTransportInput.value = selectedTransport;
    dom.previewTargetFpsInput.value = String(
      clampInteger(capabilities.targetFps, 1, 60, 5),
    );
    dom.previewMaxWidthInput.value = String(
      clampInteger(capabilities.maxWidth, 256, 4096, 1920),
    );
    dom.previewMaxHeightInput.value = String(
      clampInteger(capabilities.maxHeight, 256, 4096, 1080),
    );
    state.paused.preview = !!capabilities.isPaused;
    syncPauseButtons();
  }
}

async function refreshPreview() {
  if (!client.isConnected) {
    renderPreview();
    return;
  }

  const transport = dom.previewTransportInput.value === "png" ? "png" : "svg";
  const scale = clampNumber(dom.previewScaleInput.value, 0.1, 4, 1);
  const maxWidth = clampInteger(dom.previewMaxWidthInput.value, 256, 4096, 1920);
  const maxHeight = clampInteger(dom.previewMaxHeightInput.value, 256, 4096, 1080);
  const includeFrameData = dom.previewIncludeFrameDataInput.checked;
  const enableDiff = dom.previewEnableDiffInput.checked;
  const response = await client.request(
    RemoteMethods.PreviewSnapshotGet,
    {
      transport,
      previousFrameHash: state.previewLastFrameHash,
      includeFrameData,
      enableDiff,
      maxWidth,
      maxHeight,
      scale,
    },
    { timeoutMs: 60000 },
  );
  applyPreviewPayload(response.payload ?? null);
  renderPreview();
}

function applyPreviewPayload(payload) {
  if (!payload || typeof payload !== "object") {
    return;
  }

  state.previewSnapshot = payload;
  if (typeof payload.isPaused === "boolean") {
    state.paused.preview = payload.isPaused;
    syncPauseButtons();
  }
  if (payload.frameHash && payload.hasChanges) {
    state.previewLastFrameHash = payload.frameHash;
  }
}

async function applyPreviewSettings() {
  ensureConnected();
  const transport = dom.previewTransportInput.value === "png" ? "png" : "svg";
  const targetFps = clampInteger(dom.previewTargetFpsInput.value, 1, 60, 5);
  const scale = clampNumber(dom.previewScaleInput.value, 0.1, 4, 1);
  const maxWidth = clampInteger(dom.previewMaxWidthInput.value, 256, 4096, 1920);
  const maxHeight = clampInteger(dom.previewMaxHeightInput.value, 256, 4096, 1080);
  const enableDiff = dom.previewEnableDiffInput.checked;
  const includeFrameData = dom.previewIncludeFrameDataInput.checked;

  dom.previewTargetFpsInput.value = String(targetFps);
  dom.previewScaleInput.value = String(scale);
  dom.previewMaxWidthInput.value = String(maxWidth);
  dom.previewMaxHeightInput.value = String(maxHeight);

  await executeMutation(RemoteMethods.PreviewSettingsSet, {
    transport,
    targetFps,
    scale,
    maxWidth,
    maxHeight,
    enableDiff,
    includeFrameData,
  });
  state.previewLastFrameHash = null;
  await refreshPreviewCapabilities();
  await refreshPreview();
}

function renderPreview() {
  const snapshot = state.previewSnapshot;
  const capabilities = state.previewCapabilities;
  const supportsInput = !!capabilities?.supportsInput;
  const transport = snapshot?.transport || dom.previewTransportInput.value || "svg";
  const statusText = snapshot?.status || capabilities?.status || "Preview is disconnected.";
  const width = Number(snapshot?.width ?? 0);
  const height = Number(snapshot?.height ?? 0);
  const frameHash = snapshot?.frameHash || "n/a";
  const changeText = snapshot
    ? (snapshot.hasChanges ? "changed" : "unchanged")
    : "n/a";
  dom.previewSummary.textContent =
    `Status: ${statusText} | ${width}x${height} | Transport: ${transport} | ` +
    `Frame: ${frameHash} (${changeText}) | Input: ${supportsInput ? "on" : "off"}`;

  if (!snapshot) {
    dom.previewImage.classList.remove("active");
    dom.previewSvg.classList.remove("active");
    dom.previewImage.removeAttribute("src");
    dom.previewSvg.innerHTML = "";
    return;
  }

  if (!snapshot.frameData) {
    return;
  }

  dom.previewImage.classList.remove("active");
  dom.previewSvg.classList.remove("active");

  if (transport === "png" || snapshot.mimeType === "image/png") {
    dom.previewImage.src = `data:image/png;base64,${snapshot.frameData}`;
    dom.previewImage.classList.add("active");
    dom.previewSvg.innerHTML = "";
    return;
  }

  dom.previewSvg.innerHTML = snapshot.frameData;
  dom.previewSvg.classList.add("active");
  dom.previewImage.removeAttribute("src");
}

function wirePreviewInputEvents() {
  if (!dom.previewStage) {
    return;
  }

  dom.previewStage.addEventListener("contextmenu", (event) => event.preventDefault());
  dom.previewStage.addEventListener("pointerdown", (event) => {
    dom.previewStage.focus();
    if (typeof dom.previewStage.setPointerCapture === "function") {
      try {
        dom.previewStage.setPointerCapture(event.pointerId);
      } catch {
        // Ignore capture failures on unsupported environments.
      }
    }
    void withUiGuard(() => sendPreviewPointerEvent("pointer_down", event));
  });
  dom.previewStage.addEventListener("pointerup", (event) => {
    if (typeof dom.previewStage.releasePointerCapture === "function") {
      try {
        dom.previewStage.releasePointerCapture(event.pointerId);
      } catch {
        // Ignore release failures on unsupported environments.
      }
    }
    void withUiGuard(() => sendPreviewPointerEvent("pointer_up", event));
  });
  dom.previewStage.addEventListener("pointercancel", (event) => {
    if (typeof dom.previewStage.releasePointerCapture === "function") {
      try {
        dom.previewStage.releasePointerCapture(event.pointerId);
      } catch {
        // Ignore release failures on unsupported environments.
      }
    }
    void withUiGuard(() => sendPreviewPointerEvent("pointer_up", event));
  });
  dom.previewStage.addEventListener("pointermove", (event) => {
    void withUiGuard(() => sendPreviewPointerEvent("pointer_move", event));
  });
  dom.previewStage.addEventListener("wheel", (event) => {
    event.preventDefault();
    void withUiGuard(() => sendPreviewWheelEvent(event));
  }, { passive: false });
  dom.previewStage.addEventListener("keydown", (event) => {
    event.preventDefault();
    void withUiGuard(() => sendPreviewKeyboardEvent("key_down", event));
    if (event.key?.length === 1 || event.key === "Enter" || event.key === " ") {
      void withUiGuard(() => sendPreviewTextEvent(event));
    }
  });
  dom.previewStage.addEventListener("keyup", (event) => {
    event.preventDefault();
    void withUiGuard(() => sendPreviewKeyboardEvent("key_up", event));
  });
}

async function sendPreviewPointerEvent(eventType, event) {
  if (!canSendPreviewInput()) {
    return;
  }

  const pointer = getPreviewPointerData(event);
  await executeMutationQuiet(RemoteMethods.PreviewInputInject, {
    eventType,
    x: pointer.x,
    y: pointer.y,
    frameWidth: pointer.frameWidth,
    frameHeight: pointer.frameHeight,
    button: mapPointerButton(event.button),
    clickCount: event.detail || 1,
    ctrl: event.ctrlKey,
    shift: event.shiftKey,
    alt: event.altKey,
    meta: event.metaKey,
  });
}

async function sendPreviewWheelEvent(event) {
  if (!canSendPreviewInput()) {
    return;
  }

  const pointer = getPreviewPointerData(event);
  await executeMutationQuiet(RemoteMethods.PreviewInputInject, {
    eventType: "wheel",
    x: pointer.x,
    y: pointer.y,
    frameWidth: pointer.frameWidth,
    frameHeight: pointer.frameHeight,
    deltaX: Number(event.deltaX || 0),
    deltaY: Number(event.deltaY || 0),
    ctrl: event.ctrlKey,
    shift: event.shiftKey,
    alt: event.altKey,
    meta: event.metaKey,
  });
}

async function sendPreviewKeyboardEvent(eventType, event) {
  if (!canSendPreviewInput()) {
    return;
  }

  await executeMutationQuiet(RemoteMethods.PreviewInputInject, {
    eventType,
    key: event.key,
    ctrl: event.ctrlKey,
    shift: event.shiftKey,
    alt: event.altKey,
    meta: event.metaKey,
  });
}

async function sendPreviewTextEvent(event) {
  if (!canSendPreviewInput()) {
    return;
  }

  if (!event.key || event.key.length !== 1) {
    return;
  }

  await executeMutationQuiet(RemoteMethods.PreviewInputInject, {
    eventType: "text_input",
    text: event.key,
    ctrl: event.ctrlKey,
    shift: event.shiftKey,
    alt: event.altKey,
    meta: event.metaKey,
  });
}

function getPreviewPointerData(event) {
  const stageRect = dom.previewStage.getBoundingClientRect();
  const frameWidth = Math.max(1, Number(state.previewSnapshot?.width || stageRect.width || 1));
  const frameHeight = Math.max(1, Number(state.previewSnapshot?.height || stageRect.height || 1));
  const fitted = getPreviewFittedRect(stageRect.width, stageRect.height, frameWidth, frameHeight);
  const localX = (event.clientX - stageRect.left) - fitted.offsetX;
  const localY = (event.clientY - stageRect.top) - fitted.offsetY;
  const clampedX = Math.min(Math.max(localX, 0), fitted.width);
  const clampedY = Math.min(Math.max(localY, 0), fitted.height);
  const normalizedX = fitted.width > 0 ? (clampedX / fitted.width) * frameWidth : 0;
  const normalizedY = fitted.height > 0 ? (clampedY / fitted.height) * frameHeight : 0;
  return {
    x: normalizedX,
    y: normalizedY,
    frameWidth,
    frameHeight,
  };
}

function getPreviewFittedRect(stageWidth, stageHeight, frameWidth, frameHeight) {
  const safeStageWidth = Math.max(1, Number(stageWidth || 1));
  const safeStageHeight = Math.max(1, Number(stageHeight || 1));
  const safeFrameWidth = Math.max(1, Number(frameWidth || 1));
  const safeFrameHeight = Math.max(1, Number(frameHeight || 1));
  const scale = Math.min(safeStageWidth / safeFrameWidth, safeStageHeight / safeFrameHeight);
  const width = Math.max(1, safeFrameWidth * scale);
  const height = Math.max(1, safeFrameHeight * scale);
  const offsetX = (safeStageWidth - width) / 2;
  const offsetY = (safeStageHeight - height) / 2;
  return {
    width,
    height,
    offsetX,
    offsetY,
  };
}

function mapPointerButton(button) {
  switch (button) {
    case 1:
      return "middle";
    case 2:
      return "right";
    case 3:
      return "x1";
    case 4:
      return "x2";
    default:
      return "left";
  }
}

function canSendPreviewInput() {
  if (!client.isConnected) {
    return false;
  }

  if (state.previewCapabilities && state.previewCapabilities.supportsInput === false) {
    return false;
  }

  return true;
}

function renderLogs() {
  const filter = dom.logsFilterInput.value.trim().toLowerCase();
  const allowed = getEnabledLogLevels();
  const visible = state.logs.filter((entry) => {
    const level = normalizeLogLevel(entry.level);
    if (!allowed.has(level)) {
      return false;
    }

    if (!filter) {
      return true;
    }

    const haystack = `${entry.timestampUtc ?? ""}|${entry.level ?? ""}|${entry.area ?? ""}|${entry.source ?? ""}|${entry.message ?? ""}`;
    return haystack.toLowerCase().includes(filter);
  });

  dom.logsSummary.textContent = `Logs: ${state.logs.length} | Visible: ${visible.length} | Paused: ${state.paused.logs ? "yes" : "no"}`;
  renderTable(dom.logsTable, visible, [
    column("timestampUtc", "Time"),
    column("level", "Level"),
    column("area", "Area"),
    column("source", "Source"),
    column("message", "Message"),
  ], {
    tableStateKey: "logs",
    defaultSortKey: "timestampUtc",
    defaultSortAsc: false,
  });
}

function renderEvents() {
  dom.eventsSummary.textContent =
    `Events: ${state.events.length} | Paused: ${state.paused.events ? "yes" : "no"}`;
  renderTable(dom.eventsTable, state.events, [
    column("timestampUtc", "Time"),
    column("eventName", "Event"),
    column("source", "Source"),
    column("originator", "Originator"),
    column("observedRoutes", "Routes"),
    column("isHandled", "Handled"),
    column("handledBy", "Handled By"),
    column("chainLength", "Chain"),
  ], {
    tableStateKey: "events",
    defaultSortKey: "timestampUtc",
    defaultSortAsc: false,
  });
}

function renderMetrics() {
  const filter = dom.metricsFilterInput.value.trim().toLowerCase();
  const visibleRaw = filter
    ? state.metrics.filter((entry) => {
      const haystack = `${entry.meterName ?? ""}|${entry.instrumentName ?? ""}|${entry.instrumentType ?? ""}|${entry.unit ?? ""}`;
      return haystack.toLowerCase().includes(filter);
    })
    : state.metrics;

  const series = buildMetricSeries(visibleRaw);
  dom.metricsSummary.textContent =
    `Raw: ${state.metrics.length} | Visible: ${visibleRaw.length} | Series: ${series.length} | Paused: ${state.paused.metrics ? "yes" : "no"}`;

  renderMetricsChart(visibleRaw);

  renderTable(dom.metricsSeriesTable, series, [
    column("instrumentName", "Metric"),
    column("meterName", "Meter"),
    column("instrumentType", "Type"),
    column("unit", "Unit"),
    column("last", "Last"),
    column("avg", "Avg"),
    column("min", "Min"),
    column("max", "Max"),
    column("sampleCount", "Samples"),
    column("updatedAt", "Updated"),
  ], {
    tableStateKey: "metrics-series",
    defaultSortKey: "instrumentName",
    defaultSortAsc: true,
  });

  renderTable(dom.metricsRawTable, visibleRaw, [
    column("timestampUtc", "Time"),
    column("meterName", "Meter"),
    column("instrumentName", "Instrument"),
    column("instrumentType", "Type"),
    column("value", "Value"),
    column("unit", "Unit"),
    column("source", "Source"),
    column("tags", "Tags", (value) => renderTags(value)),
  ], {
    tableStateKey: "metrics-raw",
    defaultSortKey: "timestampUtc",
    defaultSortAsc: false,
  });
}

function renderProfiler() {
  let peakCpu = 0;
  let peakManaged = 0;
  let peakWorking = 0;

  for (const entry of state.profiler) {
    peakCpu = Math.max(peakCpu, toNumber(entry.cpuPercent));
    peakManaged = Math.max(peakManaged, toNumber(entry.managedHeapMb));
    peakWorking = Math.max(peakWorking, toNumber(entry.workingSetMb));
  }

  dom.profilerSummary.textContent =
    `Samples: ${state.profiler.length} | Peak CPU: ${formatNumber(peakCpu)}% | Peak Working Set: ${formatNumber(peakWorking)} MB | Peak Managed Heap: ${formatNumber(peakManaged)} MB | Paused: ${state.paused.profiler ? "yes" : "no"}`;

  renderProfilerChart(state.profiler);

  renderTable(dom.profilerTable, state.profiler, [
    column("timestampUtc", "Time"),
    column("source", "Source"),
    column("process", "Process"),
    column("cpuPercent", "CPU %"),
    column("workingSetMb", "Working Set MB"),
    column("privateMemoryMb", "Private MB"),
    column("managedHeapMb", "Managed MB"),
    column("gen0Collections", "Gen0"),
    column("gen1Collections", "Gen1"),
    column("gen2Collections", "Gen2"),
    column("activitySource", "Activity Source"),
    column("activityName", "Activity"),
    column("durationMs", "Duration ms"),
    column("tags", "Tags", (value) => renderTags(value)),
  ], {
    tableStateKey: "profiler",
    defaultSortKey: "timestampUtc",
    defaultSortAsc: false,
  });
}

function renderMetricsChart(rows) {
  const canvas = dom.metricsChart;
  if (!canvas) {
    return;
  }

  if (!uiLib.chart) {
    return;
  }

  const grouped = new Map();
  for (const row of rows) {
    const meterName = row?.meterName ?? "";
    const instrumentName = row?.instrumentName ?? "";
    const key = `${meterName}|${instrumentName}`;
    const timestamp = Date.parse(row?.timestampUtc ?? "");
    const point = {
      x: Number.isFinite(timestamp) ? timestamp : grouped.size,
      y: toNumber(row?.value),
    };

    if (!grouped.has(key)) {
      grouped.set(key, {
        label: `${instrumentName || "(instrument)"}${meterName ? ` [${meterName}]` : ""}`,
        points: [point],
      });
      continue;
    }

    grouped.get(key).points.push(point);
  }

  const datasets = Array.from(grouped.values())
    .sort((a, b) => b.points.length - a.points.length)
    .slice(0, 8)
    .map((series, index) => {
      const hue = (index * 47) % 360;
      return {
        label: series.label,
        data: series.points.slice(-120),
        borderColor: `hsl(${hue}deg 82% 45%)`,
        backgroundColor: `hsl(${hue}deg 82% 45% / 0.18)`,
        fill: false,
        pointRadius: 0,
        pointHoverRadius: 2,
        borderWidth: 1.8,
        tension: 0.2,
      };
    });

  const chart = ensureChart("metrics", canvas, {
    type: "line",
    data: {
      datasets,
    },
    options: {
      animation: false,
      maintainAspectRatio: false,
      parsing: false,
      normalized: true,
      scales: {
        x: {
          type: "linear",
          ticks: {
            callback: (value) => formatChartTimestamp(value),
            maxRotation: 0,
            autoSkip: true,
          },
          grid: {
            color: "#e6edf6",
          },
        },
        y: {
          beginAtZero: false,
          grid: {
            color: "#e6edf6",
          },
        },
      },
      plugins: {
        legend: {
          display: datasets.length > 0,
          position: "bottom",
          labels: {
            boxWidth: 10,
            usePointStyle: true,
          },
        },
        tooltip: {
          mode: "nearest",
          intersect: false,
          callbacks: {
            title: (items) => {
              if (!items?.length) {
                return "";
              }
              return formatChartTimestamp(items[0].parsed?.x);
            },
          },
        },
      },
    },
  });

  chart.data.datasets = datasets;
  chart.update("none");
}

function renderProfilerChart(rows) {
  const canvas = dom.profilerChart;
  if (!canvas) {
    return;
  }

  if (!uiLib.chart) {
    return;
  }

  const samples = rows.slice(-240).map((entry, index) => {
    const timestamp = Date.parse(entry?.timestampUtc ?? "");
    return {
      x: Number.isFinite(timestamp) ? timestamp : index,
      cpu: toNumber(entry?.cpuPercent),
      workingSet: toNumber(entry?.workingSetMb),
      managedHeap: toNumber(entry?.managedHeapMb),
    };
  });

  const cpuData = samples.map((sample) => ({ x: sample.x, y: sample.cpu }));
  const workingSetData = samples.map((sample) => ({ x: sample.x, y: sample.workingSet }));
  const managedHeapData = samples.map((sample) => ({ x: sample.x, y: sample.managedHeap }));

  const chart = ensureChart("profiler", canvas, {
    type: "line",
    data: {
      datasets: [
        {
          label: "CPU %",
          data: cpuData,
          yAxisID: "yCpu",
          borderColor: "#db3f2f",
          backgroundColor: "rgba(219, 63, 47, 0.18)",
          pointRadius: 0,
          borderWidth: 1.8,
          tension: 0.2,
        },
        {
          label: "Working Set MB",
          data: workingSetData,
          yAxisID: "yMemory",
          borderColor: "#0d79ca",
          backgroundColor: "rgba(13, 121, 202, 0.18)",
          pointRadius: 0,
          borderWidth: 1.8,
          tension: 0.2,
        },
        {
          label: "Managed Heap MB",
          data: managedHeapData,
          yAxisID: "yMemory",
          borderColor: "#1b9a65",
          backgroundColor: "rgba(27, 154, 101, 0.18)",
          pointRadius: 0,
          borderWidth: 1.8,
          tension: 0.2,
        },
      ],
    },
    options: {
      animation: false,
      maintainAspectRatio: false,
      parsing: false,
      normalized: true,
      scales: {
        x: {
          type: "linear",
          ticks: {
            callback: (value) => formatChartTimestamp(value),
            maxRotation: 0,
            autoSkip: true,
          },
          grid: {
            color: "#e6edf6",
          },
        },
        yCpu: {
          type: "linear",
          position: "left",
          beginAtZero: true,
          title: {
            display: true,
            text: "CPU %",
          },
          grid: {
            color: "#f2f5f9",
          },
        },
        yMemory: {
          type: "linear",
          position: "right",
          beginAtZero: true,
          title: {
            display: true,
            text: "Memory MB",
          },
          grid: {
            drawOnChartArea: false,
          },
        },
      },
      plugins: {
        legend: {
          position: "bottom",
          labels: {
            boxWidth: 10,
            usePointStyle: true,
          },
        },
      },
    },
  });

  chart.data.datasets[0].data = cpuData;
  chart.data.datasets[1].data = workingSetData;
  chart.data.datasets[2].data = managedHeapData;
  chart.update("none");
}

function ensureChart(key, canvas, baseConfig) {
  const existing = chartRegistry.get(key);
  if (existing && existing.canvas === canvas) {
    return existing.chart;
  }

  if (existing) {
    try {
      existing.chart.destroy();
    } catch {
      // ignore stale chart destroy failures
    }
  }

  const chart = new window.Chart(canvas, baseConfig);
  chartRegistry.set(key, {
    chart,
    canvas,
  });
  return chart;
}

function formatChartTimestamp(value) {
  const timestamp = Number(value);
  if (!Number.isFinite(timestamp) || timestamp <= 0) {
    return "";
  }

  const date = new Date(timestamp);
  return date.toLocaleTimeString(undefined, {
    hour12: false,
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

function renderTransport(snapshot) {
  const stats = snapshot ?? client.getTransportStats();
  dom.transportSummary.textContent =
    `Sent: ${stats.sentMessages} (${stats.sentBytes} bytes) | Received: ${stats.receivedMessages} (${stats.receivedBytes} bytes) | KeepAlive: ${stats.keepAliveCount}`;

  const statRows = [
    { name: "Connected At", value: stats.connectedAtUtc ?? "n/a" },
    { name: "Disconnected At", value: stats.disconnectedAtUtc ?? "n/a" },
    { name: "Last Send", value: stats.lastSendUtc ?? "n/a" },
    { name: "Last Receive", value: stats.lastReceiveUtc ?? "n/a" },
    { name: "Sent Messages", value: stats.sentMessages },
    { name: "Received Messages", value: stats.receivedMessages },
    { name: "Sent Bytes", value: stats.sentBytes },
    { name: "Received Bytes", value: stats.receivedBytes },
    { name: "KeepAlive Count", value: stats.keepAliveCount },
  ];

  renderTable(dom.transportStatsTable, statRows, [
    column("name", "Metric"),
    column("value", "Value"),
  ], {
    tableStateKey: "transport-stats",
    defaultSortKey: "name",
    defaultSortAsc: true,
  });

  const timeline = Array.isArray(stats.timeline) ? [...stats.timeline].reverse() : [];
  renderTable(dom.transportTimelineTable, timeline, [
    column("timestampUtc", "Timestamp"),
    column("direction", "Dir"),
    column("kindName", "Kind"),
    column("bytes", "Bytes"),
    column("summary", "Summary"),
  ], {
    tableStateKey: "transport-timeline",
    defaultSortKey: "timestampUtc",
    defaultSortAsc: false,
  });
}

function renderElements3d() {
  const snapshot = state.elements3dSnapshot;
  const svgContainer = dom.elements3dSvgContainer;
  const canvas = dom.elements3dCanvas;

  if (canvas) {
    canvas.style.display = "none";
  }

  if (!snapshot) {
    destroyElements3dPanZoom();
    clearElements3dDepthChart();
    svgContainer.classList.remove("active");
    svgContainer.innerHTML = "";
    dom.elements3dSummary.textContent = client.isConnected
      ? "No Elements 3D snapshot loaded."
      : "Elements 3D is disconnected.";
    return;
  }

  const svgSnapshot = emptyToNull(snapshot.svgSnapshot);
  if (svgSnapshot) {
    svgContainer.innerHTML = svgSnapshot;
    svgContainer.classList.add("active");
    queueElements3dPanZoomInit();
  } else {
    destroyElements3dPanZoom();
    svgContainer.classList.remove("active");
    svgContainer.innerHTML = "";
  }

  renderElements3dDepthChart(snapshot);

  const renderMode = state.elements3d.renderMode || "svg";
  const root = snapshot.inspectedRoot ?? "(none)";
  const nodeCount = Number(snapshot.nodeCount ?? snapshot.nodes?.length ?? 0);
  const visibleNodeCount = Number(
    snapshot.visibleNodeCount ?? snapshot.visibleNodeIds?.length ?? 0,
  );
  const minDepth = Number(snapshot.minVisibleDepth ?? 0);
  const maxDepth = Number(snapshot.maxVisibleDepth ?? 0);
  const zoom = Number(snapshot.zoom ?? 1);
  const spacing = Number(snapshot.depthSpacing ?? 22);
  const panZoom = state.elements3d.panZoomInstance;
  const viewportZoom = panZoom ? panZoom.getZoom() : zoom;

  dom.elements3dSummary.textContent =
    `Root: ${root} | Nodes: ${nodeCount} | Visible: ${visibleNodeCount} | ` +
    `Depth: ${minDepth}..${maxDepth} | Spacing: ${spacing.toFixed(1)} | Zoom: ${zoom.toFixed(2)} | Viewport: ${viewportZoom.toFixed(2)} | ` +
    `Mode: ${renderMode.toUpperCase()} | SVG: ${svgSnapshot ? "ready" : "unavailable"}`;
}

function wireElements3dPanZoom() {
  if (!uiLib.svgPanZoom) {
    return;
  }

  const container = dom.elements3dSvgContainer;
  if (!container) {
    return;
  }

  const svg = container.querySelector("svg");
  if (!svg) {
    return;
  }

  if (!isElements3dPanZoomRenderable(container, svg)) {
    return;
  }

  destroyElements3dPanZoom();
  try {
    state.elements3d.panZoomInstance = window.svgPanZoom(svg, {
      zoomEnabled: true,
      controlIconsEnabled: false,
      fit: true,
      center: true,
      minZoom: 0.12,
      maxZoom: 34,
      dblClickZoomEnabled: true,
      mouseWheelZoomEnabled: true,
      preventMouseEventsDefault: true,
    });

    syncElements3dZoomInputFromPanZoom();
  } catch (error) {
    console.warn("Elements3D pan-zoom initialization failed:", error);
    destroyElements3dPanZoom();
  }
}

function destroyElements3dPanZoom() {
  if (!state.elements3d.panZoomInstance) {
    return;
  }

  try {
    state.elements3d.panZoomInstance.destroy();
  } catch {
    // ignore stale pan-zoom instance failures
  }
  state.elements3d.panZoomInstance = null;
}

function syncElements3dZoomInputFromPanZoom() {
  const instance = state.elements3d.panZoomInstance;
  if (!instance || !dom.elementsZoomInput) {
    return;
  }

  const zoom = clampNumber(instance.getZoom(), 0.25, 24, 1);
  dom.elementsZoomInput.value = String(zoom);
}

function queueElements3dPanZoomInit() {
  if (!uiLib.svgPanZoom) {
    return;
  }

  window.requestAnimationFrame(() => {
    window.requestAnimationFrame(() => {
      wireElements3dPanZoom();
    });
  });
}

function isElements3dPanZoomRenderable(container, svg) {
  if (!container || !svg) {
    return false;
  }

  if (state.activeTab !== "elements3d") {
    return false;
  }

  if (dom.tabElements3d && !dom.tabElements3d.classList.contains("active")) {
    return false;
  }

  const rect = container.getBoundingClientRect();
  if (!Number.isFinite(rect.width) || !Number.isFinite(rect.height) || rect.width < 2 || rect.height < 2) {
    return false;
  }

  const viewBox = svg.getAttribute("viewBox");
  if (viewBox) {
    const parts = viewBox
      .trim()
      .split(/[\s,]+/)
      .map((part) => Number.parseFloat(part));
    if (
      parts.length === 4 &&
      Number.isFinite(parts[2]) &&
      Number.isFinite(parts[3]) &&
      parts[2] > 0 &&
      parts[3] > 0
    ) {
      return true;
    }
  }

  const width = Number.parseFloat(svg.getAttribute("width") ?? "");
  const height = Number.parseFloat(svg.getAttribute("height") ?? "");
  if (Number.isFinite(width) && Number.isFinite(height) && width > 0 && height > 0) {
    svg.setAttribute("viewBox", `0 0 ${width} ${height}`);
    return true;
  }

  return false;
}

function renderElements3dDepthChart(snapshot) {
  if (!uiLib.chart || !dom.elements3dDepthChart) {
    return;
  }

  const nodes = Array.isArray(snapshot?.nodes) ? snapshot.nodes : [];
  const depthMap = new Map();
  for (const node of nodes) {
    const depth = Number(node?.depth ?? 0);
    depthMap.set(depth, (depthMap.get(depth) ?? 0) + 1);
  }

  const sortedDepths = Array.from(depthMap.keys()).sort((a, b) => a - b);
  const labels = sortedDepths.map((depth) => `Depth ${depth}`);
  const values = sortedDepths.map((depth) => depthMap.get(depth) ?? 0);

  const chart = ensureChart("elements3d-depth", dom.elements3dDepthChart, {
    type: "bar",
    data: {
      labels,
      datasets: [
        {
          label: "Visible nodes by depth",
          data: values,
          borderWidth: 1,
          borderRadius: 4,
          backgroundColor: "rgba(12, 109, 216, 0.7)",
          borderColor: "rgba(12, 109, 216, 1)",
        },
      ],
    },
    options: {
      animation: false,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: "bottom",
        },
      },
      scales: {
        x: {
          ticks: {
            maxRotation: 0,
            autoSkip: true,
          },
          grid: {
            color: "#e6edf6",
          },
        },
        y: {
          beginAtZero: true,
          grid: {
            color: "#e6edf6",
          },
        },
      },
    },
  });

  chart.data.labels = labels;
  chart.data.datasets[0].data = values;
  chart.update("none");
}

function clearElements3dDepthChart() {
  const entry = chartRegistry.get("elements3d-depth");
  if (!entry) {
    return;
  }

  try {
    entry.chart.destroy();
  } catch {
    // ignore stale chart destroy failures
  }
  chartRegistry.delete("elements3d-depth");
}

function renderCodePanel() {
  const source = state.selectedSource;
  dom.codeSummary.textContent = state.selectedNodeDisplayName
    ? `Selection: ${state.selectedNodeDisplayName}`
    : "Selection: none";

  if (!source || (!source.xaml && !source.code)) {
    dom.sourcePreview.textContent = "No source metadata available for current selection.";
    return;
  }

  const lines = [];
  lines.push("Current Selection Source Metadata");
  lines.push("================================");
  lines.push(`Status: ${source.status ?? "n/a"}`);
  lines.push("");
  lines.push(`XAML: ${source.xaml ?? "n/a"}`);
  lines.push(`Code: ${source.code ?? "n/a"}`);
  dom.sourcePreview.textContent = lines.join("\n");
}

function activateScopeTab(scope) {
  dom.scopeTabs.forEach((button) => {
    button.classList.toggle("active", button.getAttribute("data-scope") === scope);
  });
}

function activateMainTab(tab) {
  dom.mainTabs.forEach((button) => {
    button.classList.toggle("active", button.getAttribute("data-tab") === tab);
  });
  dom.tabPanels.forEach((panel) => {
    panel.classList.toggle("active", panel.id === `tab-${tab}`);
  });

  if (tab === "elements3d") {
    queueElements3dPanZoomInit();
  }
}

async function togglePause(streamName) {
  const supportsRemotePause =
    streamName === "preview" || streamName === "metrics" || streamName === "profiler";
  const next = !state.paused[streamName];

  if (supportsRemotePause && client.isConnected) {
    await applyRemoteStreamPauseState(streamName, next);
  }

  state.paused[streamName] = next;
  syncPauseButtons();
  switch (streamName) {
    case "logs":
      renderLogs();
      break;
    case "events":
      renderEvents();
      break;
    case "metrics":
      renderMetrics();
      break;
    case "profiler":
      renderProfiler();
      break;
    case "preview":
      renderPreview();
      break;
    default:
      break;
  }
}

async function syncRemotePauseStates() {
  if (!client.isConnected) {
    return;
  }

  await applyRemoteStreamPauseState("metrics", state.paused.metrics);
  await applyRemoteStreamPauseState("profiler", state.paused.profiler);
  await applyRemoteStreamPauseState("preview", state.paused.preview);
}

async function applyRemoteStreamPauseState(streamName, isPaused) {
  if (!client.isConnected) {
    return;
  }

  const method =
    streamName === "preview"
      ? RemoteMethods.PreviewPausedSet
      : streamName === "metrics"
        ? RemoteMethods.MetricsPausedSet
        : streamName === "profiler"
          ? RemoteMethods.ProfilerPausedSet
          : null;

  if (!method) {
    return;
  }

  try {
    await executeMutation(method, { isPaused });
  } catch (error) {
    const message = normalizeError(error).toLowerCase();
    if (
      message.includes("unsupported method") ||
      message.includes("method_not_found") ||
      message.includes("request_method_not_found")
    ) {
      return;
    }

    throw error;
  }
}

function syncPauseButtons() {
  dom.pausePreviewBtn.textContent = state.paused.preview ? "Resume" : "Pause";
  dom.pauseLogsBtn.textContent = state.paused.logs ? "Resume" : "Pause";
  dom.pauseEventsBtn.textContent = state.paused.events ? "Resume" : "Pause";
  dom.pauseMetricsBtn.textContent = state.paused.metrics ? "Resume" : "Pause";
  dom.pauseProfilerBtn.textContent = state.paused.profiler ? "Resume" : "Pause";
}

function applySettingsToInputs() {
  dom.autoRefreshOnSelectionInput.checked = state.settings.autoRefreshOnSelection;
  dom.autoRefreshTreeOnConnectInput.checked = state.settings.autoRefreshTreeOnConnect;
  dom.maxRowsInput.value = String(state.settings.maxRows);
  dom.logsMaxEntriesInput.value = String(
    clampInteger(dom.logsMaxEntriesInput.value, 100, 50000, state.settings.maxRows),
  );
}

function loadSettings() {
  try {
    const raw = localStorage.getItem(settingsStorageKey);
    if (!raw) {
      return { ...defaultSettings };
    }

    const parsed = JSON.parse(raw);
    return {
      maxRows: clampInteger(parsed.maxRows, 100, 100000, defaultSettings.maxRows),
      autoRefreshOnSelection:
        typeof parsed.autoRefreshOnSelection === "boolean"
          ? parsed.autoRefreshOnSelection
          : defaultSettings.autoRefreshOnSelection,
      autoRefreshTreeOnConnect:
        typeof parsed.autoRefreshTreeOnConnect === "boolean"
          ? parsed.autoRefreshTreeOnConnect
          : defaultSettings.autoRefreshTreeOnConnect,
    };
  } catch {
    return { ...defaultSettings };
  }
}

function saveSettings(settings) {
  localStorage.setItem(settingsStorageKey, JSON.stringify(settings));
}

function trimRows(rows) {
  const max = state.settings.maxRows;
  if (rows.length <= max) {
    return;
  }

  rows.splice(0, rows.length - max);
}

function trimStreamBuffers() {
  trimRows(state.logs);
  trimRows(state.events);
  trimRows(state.metrics);
  trimRows(state.profiler);
}

function updateSelectionFooter() {
  dom.footerSelection.textContent = state.selectedNodeDisplayName
    ? `Selection: ${state.selectedNodeDisplayName}`
    : "Selection: none";
  dom.footerScope.textContent = `Scope: ${state.scope}`;
}

function getEnabledLogLevels() {
  const levels = new Set();
  if (dom.logsLevelVerbose.checked) {
    levels.add("verbose");
  }
  if (dom.logsLevelDebug.checked) {
    levels.add("debug");
  }
  if (dom.logsLevelInformation.checked) {
    levels.add("information");
  }
  if (dom.logsLevelWarning.checked) {
    levels.add("warning");
  }
  if (dom.logsLevelError.checked) {
    levels.add("error");
  }
  if (dom.logsLevelFatal.checked) {
    levels.add("fatal");
  }
  return levels;
}

function buildMetricSeries(rows) {
  const map = new Map();
  for (const row of rows) {
    const key = `${row.meterName ?? ""}|${row.instrumentName ?? ""}|${row.instrumentType ?? ""}|${row.unit ?? ""}`;
    const numericValue = toNumber(row.value);
    if (!map.has(key)) {
      map.set(key, {
        meterName: row.meterName ?? "",
        instrumentName: row.instrumentName ?? "",
        instrumentType: row.instrumentType ?? "",
        unit: row.unit ?? "",
        last: numericValue,
        min: numericValue,
        max: numericValue,
        sum: numericValue,
        sampleCount: 1,
        updatedAt: row.timestampUtc ?? "",
      });
      continue;
    }

    const series = map.get(key);
    series.last = numericValue;
    series.min = Math.min(series.min, numericValue);
    series.max = Math.max(series.max, numericValue);
    series.sum += numericValue;
    series.sampleCount += 1;
    series.updatedAt = row.timestampUtc ?? series.updatedAt;
  }

  return Array.from(map.values())
    .map((series) => ({
      ...series,
      avg: series.sampleCount > 0 ? series.sum / series.sampleCount : 0,
      last: formatNumber(series.last),
      min: formatNumber(series.min),
      max: formatNumber(series.max),
      avg: formatNumber(series.avg),
    }))
    .sort((a, b) => `${a.meterName}.${a.instrumentName}`.localeCompare(`${b.meterName}.${b.instrumentName}`));
}

function renderTable(tableElement, rows = [], columns = [], options = {}) {
  const tableKey = tableElement.id ?? options.tableStateKey ?? "grid";
  if (uiLib.tabulator && Array.isArray(columns) && columns.length > 0) {
    const tabulatorHost = ensureTabulatorHost(tableElement, tableKey);
    renderTableWithTabulator(tabulatorHost, rows, columns, {
      ...options,
      tableStateKey: tableKey,
    });
    return;
  }

  destroyTabulatorTable(tableKey);
  hideTabulatorHost(tableElement);
  renderTableFallback(tableElement, rows, columns, options);
}

function renderTableWithTabulator(tableElement, rows = [], columns = [], options = {}) {
  const tableKey = options.tableStateKey ?? tableElement.id ?? "grid";
  const gridStateKey = options.tableStateKey ?? tableKey;
  const gridState = getGridState(gridStateKey);
  if (Object.prototype.hasOwnProperty.call(options, "selectedRowKey")) {
    gridState.selectedRowKey = options.selectedRowKey;
  }

  const inputRows = Array.isArray(rows) ? rows : [];
  const rowsWithMeta = inputRows.map((rowData, index) => ({
    rowData,
    index,
    rowKey: options.rowKey ? options.rowKey(rowData, index) : `${index}`,
  }));
  const tableData = rowsWithMeta.map((entry) => {
    const row = {
      __rowKey: String(entry.rowKey),
      __rowIndex: entry.index,
      __rowData: entry.rowData,
    };
    for (const definition of columns) {
      row[definition.key] = entry.rowData?.[definition.key];
    }
    return row;
  });

  const sortableColumns = columns.filter((columnDef) => columnDef.sortable !== false);
  if (!gridState.sortKey && sortableColumns.length > 0) {
    gridState.sortKey = options.defaultSortKey ?? sortableColumns[0].key;
    gridState.sortAsc = options.defaultSortAsc ?? true;
  }

  const columnSignature = columns
    .map((definition) => `${definition.key}:${definition.label}:${definition.sortable !== false}:${Boolean(definition.format)}`)
    .join("|");

  let entry = tabulatorRegistry.get(tableKey);
  if (entry && entry.element !== tableElement) {
    destroyTabulatorTable(tableKey);
    entry = null;
  }

  const queuedRender = {
    columns,
    columnSignature,
    tableData,
    sortKey: gridState.sortKey,
    sortAsc: gridState.sortAsc,
    selectedRowKey: gridState.selectedRowKey,
  };

  if (!entry) {
    entry = {
      table: null,
      element: tableElement,
      columnSignature,
      options: null,
      built: false,
      pendingRender: queuedRender,
    };

    const table = new window.Tabulator(tableElement, {
      data: tableData,
      index: "__rowKey",
      layout: "fitDataStretch",
      maxHeight: false,
      selectableRows: typeof options.onRowSelected === "function" ? 1 : false,
      placeholder: options.emptyText ?? "No data.",
      columns: getTabulatorColumns(columns),
      initialSort: gridState.sortKey
        ? [{ column: gridState.sortKey, dir: gridState.sortAsc === false ? "desc" : "asc" }]
        : [],
      rowClick: (_event, row) => {
        const activeOptions = entry.options;
        if (!activeOptions || typeof activeOptions.onRowSelected !== "function") {
          return;
        }

        const payload = row.getData();
        const rowKey = payload?.__rowKey;
        gridState.selectedRowKey = rowKey;
        activeOptions.onRowSelected(payload?.__rowData, payload?.__rowIndex, rowKey);
        syncTabulatorSelection(entry.table, rowKey);
      },
    });

    table.on("tableBuilt", () => {
      entry.built = true;
      if (entry.pendingRender) {
        applyTabulatorRender(entry, entry.pendingRender);
        entry.pendingRender = null;
      }
    });

    table.on("dataSorted", (sorters) => {
      const sorter = Array.isArray(sorters) && sorters.length > 0 ? sorters[0] : null;
      if (!sorter || !sorter.field || sorter.field.startsWith("__")) {
        return;
      }

      gridState.sortKey = sorter.field;
      gridState.sortAsc = sorter.dir !== "desc";
    });

    entry.table = table;
    tabulatorRegistry.set(tableKey, entry);
  }

  entry.options = options;
  if (!entry.built) {
    entry.pendingRender = queuedRender;
    return;
  }

  applyTabulatorRender(entry, queuedRender);
}

function applyTabulatorRender(entry, renderState) {
  if (!entry?.table || !renderState) {
    return;
  }

  if (entry.columnSignature !== renderState.columnSignature) {
    entry.table.setColumns(getTabulatorColumns(renderState.columns));
    entry.columnSignature = renderState.columnSignature;
  }

  entry.table.setData(renderState.tableData);

  if (
    renderState.sortKey &&
    renderState.columns.some((columnDef) => columnDef.key === renderState.sortKey && columnDef.sortable !== false)
  ) {
    entry.table.setSort(renderState.sortKey, renderState.sortAsc === false ? "desc" : "asc");
  }

  syncTabulatorSelection(entry.table, renderState.selectedRowKey);
}

function syncTabulatorSelection(table, selectedRowKey) {
  if (!table) {
    return;
  }

  table.deselectRow();
  if (selectedRowKey == null || selectedRowKey === "") {
    return;
  }

  const row = table.getRow(String(selectedRowKey));
  if (row) {
    row.select();
  }
}

function getTabulatorColumns(columns) {
  return columns.map((definition) => ({
    title: definition.label,
    field: definition.key,
    headerSort: definition.sortable !== false,
    sorter: definition.sortable === false ? undefined : (left, right) => compareGridValues(left, right),
    formatter: definition.format
      ? (cell) => {
        const payload = cell.getData();
        const rawRowData = payload?.__rowData;
        const value = rawRowData?.[definition.key];
        const rendered = definition.format(value, rawRowData, {
          rowData: rawRowData,
          index: payload?.__rowIndex,
          rowKey: payload?.__rowKey,
        });
        if (rendered instanceof Node) {
          return rendered;
        }

        return rendered == null ? "" : String(rendered);
      }
      : undefined,
  }));
}

function destroyTabulatorTable(tableKey) {
  const entry = tabulatorRegistry.get(tableKey);
  if (!entry) {
    return;
  }

  try {
    entry.table?.destroy();
  } catch {
    // ignore stale table destruction issues
  }

  tabulatorRegistry.delete(tableKey);
}

function ensureTabulatorHost(tableElement, tableKey) {
  if (!(tableElement instanceof HTMLElement) || tableElement.tagName !== "TABLE") {
    return tableElement;
  }

  let host = tabulatorHostRegistry.get(tableElement);
  if (!host || !host.isConnected) {
    host = document.createElement("div");
    host.id = `${tableKey}__tabulator`;
    host.className = `${tableElement.className} tabulator-host`.trim();
    host.style.width = "100%";
    tableElement.insertAdjacentElement("afterend", host);
    tabulatorHostRegistry.set(tableElement, host);
  }

  tableElement.style.display = "none";
  host.style.display = "";
  return host;
}

function hideTabulatorHost(tableElement) {
  if (!(tableElement instanceof HTMLElement)) {
    return;
  }

  const host = tabulatorHostRegistry.get(tableElement);
  if (host) {
    host.style.display = "none";
    host.innerHTML = "";
  }

  if (tableElement.tagName === "TABLE") {
    tableElement.style.display = "";
  }
}

function renderTableFallback(tableElement, rows = [], columns = [], options = {}) {
  const gridStateKey = options.tableStateKey ?? tableElement.id ?? "grid";
  const gridState = getGridState(gridStateKey);
  if (Object.prototype.hasOwnProperty.call(options, "selectedRowKey")) {
    gridState.selectedRowKey = options.selectedRowKey;
  }

  tableElement.innerHTML = "";
  if (!Array.isArray(rows) || rows.length === 0 || !Array.isArray(columns) || columns.length === 0) {
    const empty = document.createElement("tbody");
    const row = document.createElement("tr");
    const cell = document.createElement("td");
    cell.textContent = options.emptyText ?? "No data.";
    cell.colSpan = Math.max(1, columns.length);
    row.appendChild(cell);
    empty.appendChild(row);
    tableElement.appendChild(empty);
    return;
  }

  const rowsWithMeta = rows.map((rowData, index) => ({
    rowData,
    index,
    rowKey: options.rowKey ? options.rowKey(rowData, index) : `${index}`,
  }));

  const sortableColumns = columns.filter((columnDef) => columnDef.sortable !== false);
  if (!gridState.sortKey && sortableColumns.length > 0) {
    gridState.sortKey = options.defaultSortKey ?? sortableColumns[0].key;
    gridState.sortAsc = options.defaultSortAsc ?? true;
  }

  if (gridState.sortKey) {
    const sortColumn = columns.find((columnDef) => columnDef.key === gridState.sortKey && columnDef.sortable !== false);
    if (sortColumn) {
      rowsWithMeta.sort((left, right) => {
        const leftValue = left.rowData?.[sortColumn.key];
        const rightValue = right.rowData?.[sortColumn.key];
        const compare = compareGridValues(leftValue, rightValue);
        if (compare === 0) {
          return left.index - right.index;
        }

        return gridState.sortAsc ? compare : -compare;
      });
    }
  }

  const thead = document.createElement("thead");
  const headRow = document.createElement("tr");
  for (const definition of columns) {
    const th = document.createElement("th");
    th.textContent = definition.label;
    if (definition.sortable !== false) {
      th.classList.add("sortable");
      if (gridState.sortKey === definition.key) {
        th.classList.add(gridState.sortAsc ? "sort-asc" : "sort-desc");
      }

      th.addEventListener("click", () => {
        if (gridState.sortKey === definition.key) {
          gridState.sortAsc = !gridState.sortAsc;
        } else {
          gridState.sortKey = definition.key;
          gridState.sortAsc = true;
        }

        renderTable(tableElement, rows, columns, options);
      });
    }
    headRow.appendChild(th);
  }
  thead.appendChild(headRow);
  tableElement.appendChild(thead);

  const tbody = document.createElement("tbody");
  for (const entry of rowsWithMeta) {
    const rowData = entry.rowData;
    const tr = document.createElement("tr");
    if (gridState.selectedRowKey != null && String(gridState.selectedRowKey) === String(entry.rowKey)) {
      tr.classList.add("selected");
    }

    if (typeof options.onRowSelected === "function") {
      tr.addEventListener("click", () => {
        gridState.selectedRowKey = entry.rowKey;
        options.onRowSelected(rowData, entry.index, entry.rowKey);
        renderTable(tableElement, rows, columns, options);
      });
    }

    for (const definition of columns) {
      const td = document.createElement("td");
      const rawValue = rowData?.[definition.key];
      const rendered = definition.format ? definition.format(rawValue, rowData, entry) : rawValue;
      if (rendered instanceof Node) {
        td.appendChild(rendered);
      } else {
        td.textContent = rendered == null ? "" : String(rendered);
        if (typeof rendered === "string") {
          td.title = rendered;
        }
      }
      tr.appendChild(td);
    }
    tbody.appendChild(tr);
  }

  tableElement.appendChild(tbody);
}

function getGridState(key) {
  if (!state.gridUi[key]) {
    state.gridUi[key] = {
      sortKey: null,
      sortAsc: true,
      selectedRowKey: null,
    };
  }

  return state.gridUi[key];
}

function compareGridValues(left, right) {
  const leftIsNull = left == null || left === "";
  const rightIsNull = right == null || right === "";
  if (leftIsNull && rightIsNull) {
    return 0;
  }
  if (leftIsNull) {
    return -1;
  }
  if (rightIsNull) {
    return 1;
  }

  const leftNumber = Number(left);
  const rightNumber = Number(right);
  if (Number.isFinite(leftNumber) && Number.isFinite(rightNumber)) {
    return leftNumber - rightNumber;
  }

  return String(left).localeCompare(String(right), undefined, {
    sensitivity: "base",
    numeric: true,
  });
}

function column(key, label, format, options = {}) {
  return {
    key,
    label,
    format,
    sortable: options.sortable ?? true,
  };
}

function renderSourceLocationHtml(source) {
  const normalized = normalizeSourceLocation(source);
  if (!normalized || (!normalized.xaml && !normalized.code)) {
    return normalized?.status ? escapeHtml(normalized.status) : "No source metadata.";
  }

  const parts = [];
  if (normalized.status) {
    parts.push(`<span class="pill">${escapeHtml(normalized.status)}</span>`);
  }
  if (normalized.xaml) {
    parts.push(`XAML: <span>${escapeHtml(normalized.xaml)}</span>`);
  }
  if (normalized.code) {
    parts.push(`Code: <span>${escapeHtml(normalized.code)}</span>`);
  }
  return parts.join(" | ");
}

function normalizeSourceLocation(source) {
  if (!source || typeof source !== "object") {
    return null;
  }

  return {
    xaml: emptyToNull(source.xaml),
    code: emptyToNull(source.code),
    status: emptyToNull(source.status),
  };
}

function openSourceReference(reference) {
  if (!reference) {
    setStatus("offline", "No source reference available.");
    return;
  }

  const value = String(reference).trim();
  if (!value) {
    setStatus("offline", "No source reference available.");
    return;
  }

  try {
    const fileLikePath = extractFilePath(value);
    if (fileLikePath) {
      window.open(`file://${fileLikePath}`, "_blank", "noopener,noreferrer");
      return;
    }
  } catch {
    // no-op
  }

  navigator.clipboard
    .writeText(value)
    .then(() => setStatus("online", "Source path copied to clipboard."))
    .catch(() => setStatus("offline", value));
}

function extractFilePath(value) {
  const openParen = value.indexOf("(");
  const closeParen = value.lastIndexOf(")");
  if (openParen >= 0 && closeParen > openParen + 1) {
    return value.slice(openParen + 1, closeParen);
  }

  if (value.startsWith("/") || value.includes(":\\") || value.includes(":/")) {
    return value;
  }

  return null;
}

function renderTags(tags) {
  if (!Array.isArray(tags) || tags.length === 0) {
    return "";
  }

  return tags.map((tag) => `${tag.key}=${tag.value}`).join(", ");
}

function formatNumber(value) {
  if (value == null || Number.isNaN(value)) {
    return "";
  }

  return Number(value).toLocaleString(undefined, {
    minimumFractionDigits: 0,
    maximumFractionDigits: 3,
  });
}

function toNumber(value) {
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : 0;
  }
  if (typeof value === "bigint") {
    return Number(value);
  }
  if (typeof value === "string" && value.trim().length > 0) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }
  return 0;
}

function normalizeLogLevel(level) {
  return String(level ?? "").trim().toLowerCase();
}

function ensureConnected() {
  if (!client.isConnected) {
    throw new Error("Connect to a remote endpoint first.");
  }
}

async function withUiGuard(operation, fallbackPrefix = "Operation failed") {
  try {
    await operation();
  } catch (error) {
    setStatus("offline", `${fallbackPrefix}: ${normalizeError(error)}`);
  }
}

function setStatus(stateName, text) {
  dom.statusLabel.classList.remove("offline", "connecting", "online");
  dom.statusLabel.classList.add(stateName);
  dom.statusLabel.textContent = text;
}

function clampInteger(value, min, max, fallback) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed)) {
    return fallback;
  }
  return Math.min(max, Math.max(min, parsed));
}

function clampNumber(value, min, max, fallback) {
  const parsed = Number.parseFloat(value);
  if (!Number.isFinite(parsed)) {
    return fallback;
  }
  return Math.min(max, Math.max(min, parsed));
}

function emptyToNull(value) {
  if (value == null) {
    return null;
  }

  const text = String(value).trim();
  return text.length === 0 ? null : text;
}

function normalizeError(error) {
  if (!error) {
    return "Unknown error";
  }
  if (error instanceof Error) {
    return error.message || error.name;
  }
  return String(error);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function escapeCssToken(value) {
  if (typeof CSS !== "undefined" && typeof CSS.escape === "function") {
    return CSS.escape(String(value));
  }

  return String(value).replaceAll('"', '\\"');
}

function getById(id) {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`Missing required DOM element #${id}`);
  }
  return element;
}

function getByIdOptional(id) {
  return document.getElementById(id);
}
