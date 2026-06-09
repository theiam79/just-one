window.justone = {
  getOrCreatePlayerId: () => {
    let id = localStorage.getItem("justone-playerId");
    if (!id) {
      id = crypto.randomUUID();
      localStorage.setItem("justone-playerId", id);
    }
    return id;
  },
  getName: () => localStorage.getItem("justone-name") || "",
  setName: (name) => localStorage.setItem("justone-name", name),
  copyText: async (text) => {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      return false;
    }
  },
};
