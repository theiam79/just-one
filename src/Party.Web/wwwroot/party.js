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
  // Simple per-browser on/off preferences (e.g. the confetti toggle). Storage can throw in
  // private mode or when the quota is full, so both degrade quietly like copyText above.
  getFlag: (key, fallback) => {
    try {
      const v = localStorage.getItem(key);
      return v === null ? fallback : v === "true";
    } catch {
      return fallback;
    }
  },
  setFlag: (key, value) => {
    try {
      localStorage.setItem(key, value ? "true" : "false");
    } catch {
      // No persistence available; the preference just won't stick this session.
    }
  },
};
