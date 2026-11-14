// Szyr Game Studio site: screenshot preview, accessibility settings, back-to-top.
(function () {
  "use strict";
  var root = document.documentElement;
  var KEY = "sgs-a11y-v2";
  var MODES = ["a11y-gray", "a11y-contrast", "a11y-invert", "a11y-light"]; // colour modes, one at a time
  var FONT_MIN = -1, FONT_MAX = 4; // steps of 12.5%: 87.5% .. 150%

  function reducedMotion() {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  }

  // --- Screenshot preview ---------------------------------------------------
  var dialog = document.getElementById("lightbox");
  var big = document.getElementById("lightbox-img");
  var caption = document.getElementById("lightbox-caption");
  var opener = null;

  document.querySelectorAll("[data-lightbox]").forEach(function (button) {
    button.addEventListener("click", function () {
      opener = button;
      var img = button.querySelector("img");
      big.src = img.currentSrc || img.src;
      big.alt = img.alt;
      caption.textContent = img.alt;
      dialog.showModal();
    });
  });

  // A click on the dimmed backdrop lands on the dialog element itself.
  dialog.addEventListener("click", function (e) {
    if (e.target === dialog) dialog.close();
  });
  dialog.addEventListener("close", function () {
    big.removeAttribute("src");
    if (opener) opener.focus();
  });

  // --- Accessibility widget -------------------------------------------------
  var toggle = document.getElementById("a11y-toggle");
  var panel = document.getElementById("a11y-panel");
  var status = document.getElementById("a11y-status");
  var fontStep = 0;
  try { fontStep = (JSON.parse(localStorage.getItem(KEY)) || {}).fs || 0; } catch (e) {}

  function setOpen(open) {
    panel.hidden = !open;
    toggle.setAttribute("aria-expanded", String(open));
  }
  toggle.addEventListener("click", function () { setOpen(panel.hidden); });

  function save() {
    var on = Array.prototype.filter.call(root.classList, function (c) { return c.indexOf("a11y-") === 0; });
    try { localStorage.setItem(KEY, JSON.stringify({ on: on, fs: fontStep })); } catch (e) { /* private mode: lasts this visit */ }
  }

  function sync(message) {
    root.style.fontSize = fontStep ? (100 + 12.5 * fontStep) + "%" : "";
    panel.querySelectorAll("[data-a11y-mode],[data-a11y-toggle]").forEach(function (b) {
      var cls = b.getAttribute("data-a11y-mode") || b.getAttribute("data-a11y-toggle");
      b.setAttribute("aria-pressed", String(root.classList.contains(cls)));
    });
    panel.querySelector('[data-a11y-font="1"]').disabled = fontStep >= FONT_MAX;
    panel.querySelector('[data-a11y-font="-1"]').disabled = fontStep <= FONT_MIN;
    if (message) status.textContent = message;
  }

  panel.querySelectorAll("[data-a11y-font]").forEach(function (b) {
    b.addEventListener("click", function () {
      fontStep = Math.max(FONT_MIN, Math.min(FONT_MAX, fontStep + Number(b.getAttribute("data-a11y-font"))));
      save(); sync("Rozmiar tekstu: " + (100 + 12.5 * fontStep) + "%");
    });
  });

  panel.querySelectorAll("[data-a11y-mode]").forEach(function (b) {
    b.addEventListener("click", function () {
      var cls = b.getAttribute("data-a11y-mode");
      var wasOn = root.classList.contains(cls);
      MODES.forEach(function (m) { root.classList.remove(m); });
      if (!wasOn) root.classList.add(cls);
      save(); sync(b.textContent.trim() + (wasOn ? ": wyłączone" : ": włączone"));
    });
  });

  panel.querySelectorAll("[data-a11y-toggle]").forEach(function (b) {
    b.addEventListener("click", function () {
      var on = root.classList.toggle(b.getAttribute("data-a11y-toggle"));
      save(); sync(b.textContent.trim() + (on ? ": włączone" : ": wyłączone"));
    });
  });

  panel.querySelector("[data-a11y-reset]").addEventListener("click", function () {
    Array.prototype.slice.call(root.classList).forEach(function (c) {
      if (c.indexOf("a11y-") === 0) root.classList.remove(c);
    });
    fontStep = 0;
    save(); sync("Przywrócono ustawienia domyślne");
  });

  sync();

  // Close the widget and header menus on outside click or Esc.
  var popovers = document.querySelectorAll("details[data-popover]");
  document.addEventListener("click", function (e) {
    popovers.forEach(function (d) { if (d.open && !d.contains(e.target)) d.open = false; });
    if (!panel.hidden && !document.getElementById("a11y").contains(e.target)) setOpen(false);
  });
  document.addEventListener("keydown", function (e) {
    if (e.key !== "Escape") return;
    popovers.forEach(function (d) {
      if (d.open) { d.open = false; d.querySelector("summary").focus(); }
    });
    if (!panel.hidden) { setOpen(false); toggle.focus(); }
  });

  // --- Back to top ----------------------------------------------------------
  var toTop = document.getElementById("to-top");
  function toggleToTop() { toTop.hidden = window.scrollY < 600; }
  window.addEventListener("scroll", toggleToTop, { passive: true });
  toggleToTop();

  toTop.addEventListener("click", function (e) {
    e.preventDefault();
    window.scrollTo({ top: 0, behavior: reducedMotion() ? "auto" : "smooth" });
    // Keyboard users continue from the top of the page, not from the hidden button.
    document.querySelector("header a").focus({ preventScroll: true });
  });
})();
