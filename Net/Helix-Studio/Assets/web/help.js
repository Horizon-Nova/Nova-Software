(() => {
  const normalize = (value) =>
    (value ?? "")
      .toString()
      .trim()
      .toLowerCase();

  const applyNavFilter = (query) => {
    const items = Array.from(document.querySelectorAll(".nav-item"));
    const q = normalize(query);

    if (!items.length) return;

    items.forEach((item) => {
      const link = item.querySelector("a");
      const text = normalize(link?.textContent);
      const matches = q.length === 0 || text.includes(q);
      item.hidden = !matches;
    });

    const groups = Array.from(document.querySelectorAll("[data-nav-group]"));
    groups.forEach((group) => {
      const groupItems = Array.from(group.querySelectorAll(".nav-item"));
      const anyVisible = groupItems.some((it) => !it.hidden);
      group.hidden = !anyVisible;
    });
  };

  document.addEventListener("DOMContentLoaded", () => {
    const input = document.querySelector("#navSearch");
    if (!input) return;

    const handler = () => applyNavFilter(input.value);
    input.addEventListener("input", handler);
    input.addEventListener("search", handler);
  });
})();
