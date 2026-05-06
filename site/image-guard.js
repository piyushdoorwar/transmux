// Keep site artwork from exposing browser image actions.
(function () {
  function guardImage(img) {
    img.draggable = false;
    img.setAttribute("draggable", "false");
  }

  document.querySelectorAll("img").forEach(guardImage);

  document.addEventListener(
    "contextmenu",
    (event) => {
      if (event.target instanceof HTMLImageElement) {
        event.preventDefault();
      }
    },
    true
  );

  document.addEventListener(
    "dragstart",
    (event) => {
      if (event.target instanceof HTMLImageElement) {
        event.preventDefault();
      }
    },
    true
  );

  new MutationObserver((mutations) => {
    mutations.forEach((mutation) => {
      mutation.addedNodes.forEach((node) => {
        if (node instanceof HTMLImageElement) {
          guardImage(node);
          return;
        }

        if (node instanceof Element) {
          node.querySelectorAll("img").forEach(guardImage);
        }
      });
    });
  }).observe(document.documentElement, { childList: true, subtree: true });
})();
