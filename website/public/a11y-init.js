// Runs before first paint: applies the saved accessibility settings so the page does not flash.
try {
  var s = JSON.parse(localStorage.getItem("sgs-a11y-v2")) || {};
  (s.on || []).forEach(function (c) { document.documentElement.classList.add(c); });
  if (s.fs) document.documentElement.style.fontSize = (100 + 12.5 * s.fs) + "%";
} catch (e) { /* storage blocked: defaults */ }
