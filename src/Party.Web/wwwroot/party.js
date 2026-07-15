// The "justone-" localStorage keys predate the rename to Party and are deliberately kept:
// they are how a returning player rejoins their seat, so changing them would silently
// reset every existing player's identity.
window.party = {
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
