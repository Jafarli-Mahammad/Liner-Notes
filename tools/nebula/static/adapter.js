(() => {
  const originalFetch = window.fetch.bind(window);
  const sourceCommit = document.querySelector('meta[name="graph-source-commit"]')?.content || "snapshot";
  const dataFiles = new Map([
    ["/api/repo-info", "repo-info.json"],
    ["/api/ui-config", "ui-config.json"],
    ["/api/project-health", "project-health.json"],
    ["/api/adr", "adr.json"]
  ]);

  window.fetch = async (resource, init) => {
    const rawUrl = typeof resource === "string" ? resource : resource?.url;
    if (!rawUrl) return originalFetch(resource, init);

    const requestUrl = new URL(rawUrl, window.location.href);
    let dataFile = dataFiles.get(requestUrl.pathname);
    if (requestUrl.pathname === "/api/layout") {
      dataFile = requestUrl.searchParams.get("graph") === "missed"
        ? "layout-missed.json"
        : "layout.json";
    }

    if (dataFile) {
      const staticUrl = new URL(`./data/${dataFile}`, document.baseURI);
      staticUrl.searchParams.set("v", sourceCommit);
      return originalFetch(staticUrl.toString(), { cache: "no-store" });
    }

    if (requestUrl.pathname.startsWith("/api/")) {
      return new Response("{}", {
        status: 200,
        headers: { "Content-Type": "application/json" }
      });
    }

    return originalFetch(resource, init);
  };
})();
