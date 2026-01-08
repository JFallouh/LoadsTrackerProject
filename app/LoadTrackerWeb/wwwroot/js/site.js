// File: wwwroot/js/loadtracker.js
(function () {
  const table = document.getElementById("loadsTable");
  if (!table) return;

  const customer = table.dataset.customer;
  const year = parseInt(table.dataset.year, 10);
  const month = parseInt(table.dataset.month, 10);
  const canEdit = table.dataset.canedit === "1";

  // ---------- Column toggles (per-user browser localStorage) ----------
  const colToggleKey = "lt_columns";
 const toggles = document.querySelectorAll("#columnsPanel input[type=checkbox][data-col]");


  // These define your two header groups (must match your columns)
  const leftGroupCols = ["probill","bol","order","po","receiver","rcity","rprov","pickup","rad","status"];
  const rightGroupCols = ["ddate","dtime","exception","ontime","delay","comments"];

  function updateGroupColspans(state) {
    const leftTh = table.querySelector("thead .group-left");
    const rightTh = table.querySelector("thead .group-right");
    if (!leftTh || !rightTh) return;

    const visible = (c) => state[c] !== false;

    const leftCount = leftGroupCols.filter(visible).length;
    const rightCount = rightGroupCols.filter(visible).length;

    // If save column exists, it belongs to the right group visually
    const hasSave = !!table.querySelector("thead th.save-col, thead th:last-child.save-col") ||
                    !!table.querySelector("thead tr.col-row th.save-col") ||
                    !!table.querySelector("thead tr.col-row th:last-child:not([data-col])");

    leftTh.colSpan = Math.max(1, leftCount);
    rightTh.colSpan = Math.max(1, rightCount + (hasSave ? 1 : 0));
  }

  function applyColumnVisibility(state) {
    toggles.forEach(cb => {
      const col = cb.dataset.col;
      const show = state[col] !== false; // default true
      cb.checked = show;

      document.querySelectorAll(`[data-col="${col}"]`).forEach(el => {
        el.style.display = show ? "" : "none";
      });
    });

    updateGroupColspans(state);
  }

  let colState = {};
  try { colState = JSON.parse(localStorage.getItem(colToggleKey) || "{}"); } catch { colState = {}; }
  applyColumnVisibility(colState);

  toggles.forEach(cb => {
    cb.addEventListener("change", () => {
      colState[cb.dataset.col] = cb.checked;
      localStorage.setItem(colToggleKey, JSON.stringify(colState));
      applyColumnVisibility(colState);
    });
  });

  // ---------- Column widths (resizable, stored in localStorage) ----------
  const widthKey = "lt_colwidths";
  let widths = {};
  try { widths = JSON.parse(localStorage.getItem(widthKey) || "{}"); } catch { widths = {}; }

  function applyWidth(col, px) {
    // Apply to ALL header+cells that share data-col
    document.querySelectorAll(`[data-col="${col}"]`).forEach(el => {
      el.style.width = px + "px";
      el.style.maxWidth = px + "px";
    });
  }

  // Apply saved widths
  Object.keys(widths).forEach(col => {
    const px = parseInt(widths[col], 10);
    if (px > 20) applyWidth(col, px);
  });

  // Attach drag handlers to resizers
  table.querySelectorAll("thead tr.col-row th[data-col]").forEach(th => {
    const col = th.dataset.col;
    const handle = th.querySelector(".col-resizer");
    if (!handle) return;

    handle.addEventListener("mousedown", (e) => {
      e.preventDefault();

      const startX = e.clientX;
      const startWidth = th.getBoundingClientRect().width;

      function onMove(ev) {
        const dx = ev.clientX - startX;
        const newW = Math.max(40, Math.round(startWidth + dx));
        applyWidth(col, newW);
      }

      function onUp(ev) {
        const finalW = Math.round(th.getBoundingClientRect().width);
        widths[col] = finalW;
        localStorage.setItem(widthKey, JSON.stringify(widths));

        window.removeEventListener("mousemove", onMove);
        window.removeEventListener("mouseup", onUp);
      }

      window.addEventListener("mousemove", onMove);
      window.addEventListener("mouseup", onUp);
    });
  });

  // ---------- Safe row update ----------
  async function wireSaveButtons(root) {
    if (!canEdit) return;

    root.querySelectorAll("button.save").forEach(btn => {
      btn.addEventListener("click", async () => {
        const tr = btn.closest("tr");
        const id = parseInt(tr.dataset.rowid, 10);

        const ex = tr.querySelector("input.ex")?.checked === true;
        const delay = tr.querySelector("textarea.delay")?.value ?? "";
        const comments = tr.querySelector("textarea.comments")?.value ?? "";

        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        const headers = { "Content-Type": "application/json" };
        if (token) headers["RequestVerificationToken"] = token.value;

        const res = await fetch("./Loads/Update", {
          method: "POST",
          headers,
          body: JSON.stringify({
            detailLineId: id,
            exception: ex,
            userNonCarrierDelay: delay,
            comments: comments,
            year: year,
            month: month
          })
        });

        if (!res.ok) alert("Update failed.");
      });
    });
  }

  wireSaveButtons(document);

  // ---------- SignalR live refresh ----------
  const yyyymm = `${year}${String(month).padStart(2, "0")}`;

  if (window.signalR) {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("./hubs/loadtracker")
      .build();

    connection.on("rowUpdated", async (detailLineId) => {
      const url = `./Loads/Row?id=${detailLineId}&year=${year}&month=${month}`;
      const html = await fetch(url).then(r => r.ok ? r.text() : null);
      if (!html) return;

      const old = table.querySelector(`tr[data-rowid="${detailLineId}"]`);
      if (!old) return;

      const tmp = document.createElement("tbody");
      tmp.innerHTML = html.trim();
      const newRow = tmp.querySelector("tr");
      if (!newRow) return;

      old.replaceWith(newRow);

      // Re-apply state after replace
      applyColumnVisibility(colState);
      Object.keys(widths).forEach(c => applyWidth(c, widths[c]));
      wireSaveButtons(document);
    });

    connection.start()
      .then(() => connection.invoke("JoinGroup", customer, yyyymm))
      .catch(() => { /* ignore */ });
  }
})();
